namespace FlowCore.Persistence;

/// <summary>
/// In-memory implementation of IWorkflowStore for testing and simple scenarios.
/// Not suitable for production use with distributed or long-running workflows.
/// </summary>
/// <remarks>
/// Initializes a new instance of the InMemoryWorkflowStore class.
/// </remarks>
/// <param name="logger">Optional logger for diagnostic information.</param>
public class InMemoryWorkflowStore(ILogger<InMemoryWorkflowStore>? logger = null) : IWorkflowStore
{
    private readonly ConcurrentDictionary<string, ExecutionData> _executions = new();
    private readonly ConcurrentDictionary<string, LeaseInfo> _leases = new();
    private bool _disposed;

    public async Task<ExecutionCheckpoint> CreateExecutionAsync(
        string workflowId,
        Guid executionId,
        ExecutionContext context)
    {
        ThrowIfDisposed();

        var key = GetExecutionKey(workflowId, executionId);
        var checkpoint = new ExecutionCheckpoint
        {
            WorkflowId = workflowId,
            ExecutionId = executionId,
            CurrentBlockName = context.CurrentBlockName,
            LastUpdatedUtc = DateTime.UtcNow,
            State = new Dictionary<string, object>(context.State),
            History = [],
            RetryCount = 0,
            CorrelationId = context.ExecutionId.ToString(),
            Version = 1,
            OriginalInput = context.Input
        };

        var executionData = new ExecutionData
        {
            Checkpoint = checkpoint,
            Metadata = new Interfaces.ExecutionMetadata
            {
                WorkflowId = workflowId,
                ExecutionId = executionId,
                Status = WorkflowStatus.Running,
                StartedAt = DateTime.UtcNow,
                CorrelationId = context.ExecutionId.ToString()
            },
            Version = 1
        };

        if (!_executions.TryAdd(key, executionData))
        {
            throw new InvalidOperationException($"Execution {executionId} already exists for workflow {workflowId}");
        }

        logger?.LogDebug("Created execution checkpoint for workflow {WorkflowId}, execution {ExecutionId}",
            workflowId, executionId);

        return checkpoint;
    }

    /// <inheritdoc />
    public async Task<ExecutionCheckpoint?> LoadLatestCheckpointAsync(
        string workflowId,
        Guid executionId,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        var key = GetExecutionKey(workflowId, executionId);
        if (!_executions.TryGetValue(key, out var executionData))
        {
            logger?.LogDebug("Checkpoint not found for workflow {WorkflowId}, execution {ExecutionId}",
                workflowId, executionId);
            return null;
        }

        // Create a new checkpoint with updated timestamp since we can't modify the existing one
        var updatedCheckpoint = executionData.Checkpoint with
        {
            LastUpdatedUtc = DateTime.UtcNow
        };

        // Update the stored checkpoint
        executionData.Checkpoint = updatedCheckpoint;

        return updatedCheckpoint;
    }

    /// <inheritdoc />
    public async Task SaveCheckpointAsync(
        ExecutionCheckpoint checkpoint,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        var key = GetExecutionKey(checkpoint.WorkflowId, checkpoint.ExecutionId);
        if (!_executions.TryGetValue(key, out var executionData))
        {
            throw new InvalidOperationException($"Execution {checkpoint.ExecutionId} not found for workflow {checkpoint.WorkflowId}");
        }

        // Check version for concurrency control
        var expectedVersion = checkpoint.Version == 0 ? executionData.Version : checkpoint.Version;
        if (expectedVersion != executionData.Version)
        {
            throw new InvalidOperationException($"Checkpoint version mismatch for workflow {checkpoint.WorkflowId}, execution {checkpoint.ExecutionId}. Expected {executionData.Version}, got {expectedVersion}");
        }

        // Create updated checkpoint with new last updated time and incremented version
        var updatedCheckpoint = checkpoint with
        {
            LastUpdatedUtc = DateTime.UtcNow,
            Version = executionData.Version + 1
        };

        executionData.Checkpoint = updatedCheckpoint;
        executionData.Version = updatedCheckpoint.Version;

        logger?.LogDebug("Saved checkpoint for workflow {WorkflowId}, execution {ExecutionId}",
            checkpoint.WorkflowId, checkpoint.ExecutionId);
    }

    /// <inheritdoc />
    public async Task<bool> TryAcquireLeaseAsync(
        string workflowId,
        Guid executionId,
        TimeSpan leaseDuration)
    {
        ThrowIfDisposed();

        var leaseKey = GetLeaseKey(workflowId, executionId);
        var now = DateTime.UtcNow;
        var expirationTime = now.Add(leaseDuration);

        var leaseInfo = new LeaseInfo(
            workflowId,
            executionId,
            now,
            expirationTime,
            leaseDuration);

        // Try to acquire or extend the lease
        if (_leases.TryAdd(leaseKey, leaseInfo))
        {
            logger?.LogDebug("Acquired lease for workflow {WorkflowId}, execution {ExecutionId}",
                workflowId, executionId);
            return true;
        }

        // Check if existing lease can be extended
        if (_leases.TryGetValue(leaseKey, out var existingLease))
        {
            // Only extend if we're still within the original lease period
            if (now <= existingLease.ExpiresAt)
            {
                var extendedLease = existingLease with
                {
                    AcquiredAt = now,
                    ExpiresAt = expirationTime
                };

                _leases[leaseKey] = extendedLease;
                logger?.LogDebug("Extended lease for workflow {WorkflowId}, execution {ExecutionId}",
                    workflowId, executionId);
                return true;
            }
        }

        logger?.LogDebug("Failed to acquire lease for workflow {WorkflowId}, execution {ExecutionId}",
            workflowId, executionId);
        return false;
    }

    /// <inheritdoc />
    public async Task ReleaseLeaseAsync(string workflowId, Guid executionId)
    {
        ThrowIfDisposed();

        var leaseKey = GetLeaseKey(workflowId, executionId);
        _leases.TryRemove(leaseKey, out _);

        logger?.LogDebug("Released lease for workflow {WorkflowId}, execution {ExecutionId}",
            workflowId, executionId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Interfaces.ExecutionMetadata>> QueryExecutionsAsync(
        string workflowId,
        ExecutionQueryParameters parameters)
    {
        ThrowIfDisposed();

        var query = _executions.Values
            .Where(e => string.IsNullOrEmpty(workflowId) || e.Metadata.WorkflowId == workflowId)
            .Where(e => !parameters.StatusFilter.HasValue || e.Metadata.Status == parameters.StatusFilter.Value)
            .Where(e => !parameters.StartedAfter.HasValue || e.Metadata.StartedAt >= parameters.StartedAfter.Value)
            .Where(e => !parameters.StartedBefore.HasValue || e.Metadata.StartedAt <= parameters.StartedBefore.Value);

        var results = query
            .Skip(parameters.Offset)
            .Take(parameters.Limit)
            .Select(e => e.Metadata)
            .ToList();

        logger?.LogDebug("Queried executions for workflow {WorkflowId}, found {Count} results",
            workflowId, results.Count);

        return results;
    }

    /// <summary>
    /// Gets the total count of stored executions.
    /// </summary>
    /// <returns>The total number of executions in the store.</returns>
    public int GetExecutionCount()
    {
        ThrowIfDisposed();
        return _executions.Count;
    }

    /// <summary>
    /// Clears all stored executions and leases.
    /// </summary>
    /// <returns>A task representing the clear operation.</returns>
    public async Task ClearAsync()
    {
        ThrowIfDisposed();
        _executions.Clear();
        _leases.Clear();
        logger?.LogInformation("Cleared all executions and leases from InMemoryWorkflowStore");
    }

    /// <summary>
    /// Disposes of the workflow store and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _executions.Clear();
            _leases.Clear();
            _disposed = true;
        }
    }

    /// <summary>
    /// Throws an ObjectDisposedException if the store has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(InMemoryStateManager));
    }

    /// <summary>
    /// Creates a consistent key for execution data.
    /// </summary>
    private static string GetExecutionKey(string workflowId, Guid executionId) => $"{workflowId}:{executionId}";

    /// <summary>
    /// Creates a consistent key for lease data.
    /// </summary>
    private static string GetLeaseKey(string workflowId, Guid executionId) => $"lease:{workflowId}:{executionId}";

    /// <summary>
    /// Internal class for storing execution data.
    /// </summary>
    sealed class ExecutionData
    {
        public ExecutionCheckpoint Checkpoint { get; set; } = new();
        public Interfaces.ExecutionMetadata Metadata { get; set; } = new();
        public int Version { get; set; }
    }

    /// <summary>
    /// Internal record for storing lease information.
    /// </summary>
    sealed record LeaseInfo(
        string WorkflowId,
        Guid ExecutionId,
        DateTime AcquiredAt,
        DateTime ExpiresAt,
        TimeSpan LeaseDuration);
}

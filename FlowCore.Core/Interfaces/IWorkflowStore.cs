namespace FlowCore.Interfaces;

/// <summary>
/// Defines the contract for workflow execution persistence and state management.
/// Responsible for storing execution checkpoints, managing leases, and querying execution history.
/// </summary>
public interface IWorkflowStore
{
    /// <summary>
    /// Creates a new workflow execution and stores the initial checkpoint.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the execution.</param>
    /// <param name="context">The initial execution context.</param>
    /// <returns>A task representing the creation operation, containing the execution checkpoint.</returns>
    Task<ExecutionCheckpoint> CreateExecutionAsync(
        string workflowId,
        Guid executionId,
        ExecutionContext context);

    /// <summary>
    /// Loads the latest checkpoint for a workflow execution.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the execution.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the load operation, containing the latest checkpoint or null if not found.</returns>
    Task<ExecutionCheckpoint?> LoadLatestCheckpointAsync(
        string workflowId,
        Guid executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a checkpoint for a workflow execution.
    /// </summary>
    /// <param name="checkpoint">The execution checkpoint to save.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the save operation.</returns>
    Task SaveCheckpointAsync(
        ExecutionCheckpoint checkpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to acquire a lease for exclusive execution of a workflow instance.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the execution.</param>
    /// <param name="leaseDuration">The duration for which the lease should be held.</param>
    /// <returns>A task representing the lease acquisition, returning true if successful.</returns>
    Task<bool> TryAcquireLeaseAsync(
        string workflowId,
        Guid executionId,
        TimeSpan leaseDuration);

    /// <summary>
    /// Releases a previously acquired lease for a workflow execution.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the execution.</param>
    /// <returns>A task representing the lease release operation.</returns>
    Task ReleaseLeaseAsync(string workflowId, Guid executionId);

    /// <summary>
    /// Queries workflow executions based on specified parameters.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow (optional filter).</param>
    /// <param name="parameters">The query parameters for filtering and pagination.</param>
    /// <returns>A task representing the query operation, containing matching execution metadata.</returns>
    Task<IEnumerable<ExecutionMetadata>> QueryExecutionsAsync(
        string workflowId,
        ExecutionQueryParameters parameters);
}

/// <summary>
/// Represents a persisted execution checkpoint containing state and execution information.
/// </summary>
public record ExecutionCheckpoint
{
    /// <summary>
    /// Gets the unique identifier of the workflow.
    /// </summary>
    public string WorkflowId { get; init; } = default!;

    /// <summary>
    /// Gets the unique identifier of the execution.
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Gets the name of the current block being executed.
    /// </summary>
    public string CurrentBlockName { get; init; } = default!;

    /// <summary>
    /// Gets the timestamp when this checkpoint was last updated.
    /// </summary>
    public DateTime LastUpdatedUtc { get; init; }

    /// <summary>
    /// Gets the persisted execution state as key-value pairs.
    /// </summary>
    public IDictionary<string, object> State { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the execution history containing information about completed blocks.
    /// </summary>
    public IReadOnlyList<BlockExecutionInfo> History { get; init; } = Array.Empty<BlockExecutionInfo>();

    /// <summary>
    /// Gets the number of retries attempted for the current block.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Gets the correlation identifier for tracking related executions.
    /// </summary>
    public string CorrelationId { get; init; } = default!;
}

/// <summary>
/// Metadata about a workflow execution for querying and monitoring.
/// </summary>
public record ExecutionMetadata
{
    /// <summary>
    /// Gets the unique identifier of the workflow.
    /// </summary>
    public string WorkflowId { get; init; } = default!;

    /// <summary>
    /// Gets the unique identifier of the execution.
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Gets the current status of the execution.
    /// </summary>
    public WorkflowStatus Status { get; init; }

    /// <summary>
    /// Gets the timestamp when execution started.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Gets the timestamp when execution completed (null if still running).
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Gets the correlation identifier for the execution.
    /// </summary>
    public string CorrelationId { get; init; } = default!;
}

/// <summary>
/// Parameters for querying workflow executions.
/// </summary>
public record ExecutionQueryParameters
{
    /// <summary>
    /// Gets the maximum number of results to return.
    /// </summary>
    public int Limit { get; init; } = 50;

    /// <summary>
    /// Gets the number of results to skip for pagination.
    /// </summary>
    public int Offset { get; init; } = 0;

    /// <summary>
    /// Gets the execution status filter (null for all statuses).
    /// </summary>
    public WorkflowStatus? StatusFilter { get; init; }

    /// <summary>
    /// Gets the start date filter for executions (null for no filter).
    /// </summary>
    public DateTime? StartedAfter { get; init; }

    /// <summary>
    /// Gets the end date filter for executions (null for no filter).
    /// </summary>
    public DateTime? StartedBefore { get; init; }
}
namespace FlowCore.CodeExecution.Serialization;

/// <summary>
/// Service for managing code execution state persistence.
/// Handles saving and loading execution state across workflow sessions.
/// </summary>
public interface ICodeExecutionStateManager
{
    /// <summary>
    /// Saves the current execution state.
    /// </summary>
    /// <param name="state">The state to save.</param>
    /// <param name="options">Save options.</param>
    /// <returns>A unique identifier for the saved state.</returns>
    Task<string> SaveStateAsync(CodeExecutionState state, SaveOptions? options = null);

    /// <summary>
    /// Loads execution state by identifier.
    /// </summary>
    /// <param name="stateId">The identifier of the state to load.</param>
    /// <param name="options">Load options.</param>
    /// <returns>The loaded execution state.</returns>
    Task<CodeExecutionState> LoadStateAsync(string stateId, LoadOptions? options = null);

    /// <summary>
    /// Deletes saved execution state.
    /// </summary>
    /// <param name="stateId">The identifier of the state to delete.</param>
    /// <returns>True if the state was deleted, false if it didn't exist.</returns>
    Task<bool> DeleteStateAsync(string stateId);

    /// <summary>
    /// Lists all saved states for a workflow.
    /// </summary>
    /// <param name="workflowName">The name of the workflow.</param>
    /// <param name="options">List options.</param>
    /// <returns>A list of state metadata.</returns>
    Task<IEnumerable<StateMetadata>> ListStatesAsync(string workflowName, ListOptions? options = null);

    /// <summary>
    /// Creates a checkpoint of the current execution state.
    /// </summary>
    /// <param name="context">The execution context to checkpoint.</param>
    /// <param name="checkpointName">Optional name for the checkpoint.</param>
    /// <returns>The checkpoint identifier.</returns>
    Task<string> CreateCheckpointAsync(CodeExecutionContext context, string? checkpointName = null);

    /// <summary>
    /// Restores execution context from a checkpoint.
    /// </summary>
    /// <param name="checkpointId">The checkpoint identifier.</param>
    /// <param name="targetContext">The context to restore state into.</param>
    /// <returns>True if the restore was successful.</returns>
    Task<bool> RestoreFromCheckpointAsync(string checkpointId, CodeExecutionContext targetContext);

    /// <summary>
    /// Purges old execution states based on retention policy.
    /// </summary>
    /// <param name="policy">The retention policy to apply.</param>
    /// <returns>The number of states that were purged.</returns>
    Task<int> PurgeOldStatesAsync(RetentionPolicy policy);
}

/// <summary>
/// Metadata about saved execution state.
/// </summary>
public class StateMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier for the state.
    /// </summary>
    public string StateId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the execution ID.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the workflow name.
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the block name.
    /// </summary>
    public string BlockName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the state was saved.
    /// </summary>
    public DateTime SavedAt { get; set; }

    /// <summary>
    /// Gets or sets when the state was captured.
    /// </summary>
    public DateTime CapturedAt { get; set; }

    /// <summary>
    /// Gets or sets the size of the serialized state in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets tags associated with the state.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object> AdditionalMetadata { get; set; } = new();
}

/// <summary>
/// Options for saving execution state.
/// </summary>
public class SaveOptions
{
    /// <summary>
    /// Gets or sets tags to associate with the saved state.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the expiration time for the saved state.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to overwrite if state already exists.
    /// </summary>
    public bool OverwriteIfExists { get; set; }

    /// <summary>
    /// Gets or sets serialization options.
    /// </summary>
    public SerializationOptions? SerializationOptions { get; set; }

    /// <summary>
    /// Gets or sets additional metadata to store with the state.
    /// </summary>
    public Dictionary<string, object> AdditionalMetadata { get; set; } = new();
}

/// <summary>
/// Options for loading execution state.
/// </summary>
public class LoadOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to validate the loaded state.
    /// </summary>
    public bool ValidateState { get; set; } = true;

    /// <summary>
    /// Gets or sets serialization options for deserialization.
    /// </summary>
    public SerializationOptions? SerializationOptions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include expired states.
    /// </summary>
    public bool IncludeExpired { get; set; }
}

/// <summary>
/// Options for listing saved states.
/// </summary>
public class ListOptions
{
    /// <summary>
    /// Gets or sets the maximum number of states to return.
    /// </summary>
    public int? MaxResults { get; set; }

    /// <summary>
    /// Gets or sets the continuation token for paging.
    /// </summary>
    public string? ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets tags to filter by.
    /// </summary>
    public List<string> FilterTags { get; set; } = new();

    /// <summary>
    /// Gets or sets the date range to filter by.
    /// </summary>
    public DateRange? DateRange { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include expired states.
    /// </summary>
    public bool IncludeExpired { get; set; }
}

/// <summary>
/// Date range for filtering states.
/// </summary>
public class DateRange
{
    /// <summary>
    /// Gets or sets the start date.
    /// </summary>
    public DateTime? Start { get; set; }

    /// <summary>
    /// Gets or sets the end date.
    /// </summary>
    public DateTime? End { get; set; }
}

/// <summary>
/// Policy for retaining execution states.
/// </summary>
public class RetentionPolicy
{
    /// <summary>
    /// Gets or sets the maximum age of states to keep.
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets the maximum number of states to keep per workflow.
    /// </summary>
    public int? MaxStatesPerWorkflow { get; set; }

    /// <summary>
    /// Gets or sets the maximum total size of all states.
    /// </summary>
    public long? MaxTotalSize { get; set; }

    /// <summary>
    /// Gets or sets tags that should be preserved regardless of age.
    /// </summary>
    public List<string> PreserveTags { get; set; } = new();

    /// <summary>
    /// Gets the default retention policy.
    /// </summary>
    public static RetentionPolicy Default => new();

    /// <summary>
    /// Gets a conservative retention policy that keeps data longer.
    /// </summary>
    public static RetentionPolicy Conservative => new()
    {
        MaxAge = TimeSpan.FromDays(90),
        MaxStatesPerWorkflow = 1000,
        PreserveTags = new List<string> { "important", "checkpoint", "milestone" }
    };

    /// <summary>
    /// Gets an aggressive retention policy that cleans up data frequently.
    /// </summary>
    public static RetentionPolicy Aggressive => new()
    {
        MaxAge = TimeSpan.FromDays(7),
        MaxStatesPerWorkflow = 50,
        MaxTotalSize = 100 * 1024 * 1024 // 100MB
    };
}

/// <summary>
/// In-memory implementation of code execution state manager.
/// Suitable for development and testing scenarios.
/// </summary>
/// <remarks>
/// Initializes a new instance of the InMemoryCodeExecutionStateManager.
/// </remarks>
/// <param name="serializer">The serializer to use for state persistence.</param>
/// <param name="logger">Optional logger for state management operations.</param>
public class InMemoryCodeExecutionStateManager(
    ICodeExecutionStateSerializer? serializer = null,
    ILogger? logger = null) : ICodeExecutionStateManager
{
    private readonly ICodeExecutionStateSerializer _serializer = serializer ?? new JsonCodeExecutionStateSerializer(logger);
    private readonly ConcurrentDictionary<string, string> _states = new();
    private readonly ConcurrentDictionary<string, StateMetadata> _metadata = new();

    /// <summary>
    /// Saves the current execution state in memory.
    /// </summary>
    /// <param name="state">The state to save.</param>
    /// <param name="options">Save options.</param>
    /// <returns>A unique identifier for the saved state.</returns>
    public async Task<string> SaveStateAsync(CodeExecutionState state, SaveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        var saveOptions = options ?? new SaveOptions();
        var stateId = GenerateStateId(state);

        try
        {
            logger?.LogDebug("Saving execution state {ExecutionId} with ID {StateId}", state.ExecutionId, stateId);

            // Check if state already exists and if we should overwrite
            if (_states.ContainsKey(stateId) && !saveOptions.OverwriteIfExists)
            {
                throw new InvalidOperationException($"State with ID {stateId} already exists and overwrite is not allowed");
            }

            // Serialize the state
            var serializedData = await _serializer.SerializeAsync(state, saveOptions.SerializationOptions).ConfigureAwait(false);

            // Store the serialized state
            _states.AddOrUpdate(stateId, serializedData, (_, _) => serializedData);

            // Store metadata
            var metadata = new StateMetadata
            {
                StateId = stateId,
                ExecutionId = state.ExecutionId,
                WorkflowName = state.WorkflowName,
                BlockName = state.BlockName,
                SavedAt = DateTime.UtcNow,
                CapturedAt = state.CapturedAt,
                Size = serializedData.Length,
                Tags = saveOptions.Tags,
                AdditionalMetadata = saveOptions.AdditionalMetadata
            };

            if (saveOptions.ExpiresAt.HasValue)
            {
                metadata.AdditionalMetadata["ExpiresAt"] = saveOptions.ExpiresAt.Value;
            }

            _metadata.AddOrUpdate(stateId, metadata, (_, _) => metadata);

            logger?.LogDebug("Successfully saved execution state {ExecutionId} with ID {StateId}, size: {Size} bytes",
                state.ExecutionId, stateId, serializedData.Length);

            return stateId;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to save execution state {ExecutionId}", state.ExecutionId);
            throw;
        }
    }

    /// <summary>
    /// Loads execution state by identifier from memory.
    /// </summary>
    /// <param name="stateId">The identifier of the state to load.</param>
    /// <param name="options">Load options.</param>
    /// <returns>The loaded execution state.</returns>
    public async Task<CodeExecutionState> LoadStateAsync(string stateId, LoadOptions? options = null)
    {
        if (string.IsNullOrEmpty(stateId))
        {
            throw new ArgumentException("State ID cannot be null or empty", nameof(stateId));
        }

        var loadOptions = options ?? new LoadOptions();

        try
        {
            logger?.LogDebug("Loading execution state with ID {StateId}", stateId);

            // Check if state exists
            if (!_states.TryGetValue(stateId, out var serializedData))
            {
                throw new KeyNotFoundException($"State with ID {stateId} not found");
            }

            // Check if state has expired
            if (_metadata.TryGetValue(stateId, out var metadata))
            {
                if (metadata.AdditionalMetadata.TryGetValue("ExpiresAt", out var expiresAtObj) &&
                    expiresAtObj is DateTime expiresAt &&
                    DateTime.UtcNow > expiresAt &&
                    !loadOptions.IncludeExpired)
                {
                    throw new InvalidOperationException($"State with ID {stateId} has expired");
                }
            }

            // Deserialize the state
            var state = await _serializer.DeserializeAsync(serializedData, loadOptions.SerializationOptions).ConfigureAwait(false);

            // Validate the state if requested
            if (loadOptions.ValidateState && !await ValidateStateAsync(state).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Loaded state {stateId} failed validation");
            }

            logger?.LogDebug("Successfully loaded execution state {ExecutionId} with ID {StateId}", state.ExecutionId, stateId);

            return state;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to load execution state with ID {StateId}", stateId);
            throw;
        }
    }

    /// <summary>
    /// Deletes saved execution state from memory.
    /// </summary>
    /// <param name="stateId">The identifier of the state to delete.</param>
    /// <returns>True if the state was deleted, false if it didn't exist.</returns>
    public async Task<bool> DeleteStateAsync(string stateId)
    {
        if (string.IsNullOrEmpty(stateId))
        {
            return false;
        }

        try
        {
            logger?.LogDebug("Deleting execution state with ID {StateId}", stateId);

            var stateRemoved = _states.TryRemove(stateId, out _);
            var metadataRemoved = _metadata.TryRemove(stateId, out _);

            var deleted = stateRemoved || metadataRemoved;
            if (deleted)
            {
                logger?.LogDebug("Successfully deleted execution state with ID {StateId}", stateId);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to delete execution state with ID {StateId}", stateId);
            return false;
        }
    }

    /// <summary>
    /// Lists all saved states for a workflow in memory.
    /// </summary>
    /// <param name="workflowName">The name of the workflow.</param>
    /// <param name="options">List options.</param>
    /// <returns>A list of state metadata.</returns>
    public async Task<IEnumerable<StateMetadata>> ListStatesAsync(string workflowName, ListOptions? options = null)
    {
        var listOptions = options ?? new ListOptions();

        try
        {
            logger?.LogDebug("Listing execution states for workflow {WorkflowName}", workflowName);

            var query = _metadata.Values.Where(m => m.WorkflowName == workflowName);

            // Apply filters
            if (listOptions.FilterTags.Any())
            {
                query = query.Where(m => listOptions.FilterTags.All(tag => m.Tags.Contains(tag)));
            }

            if (listOptions.DateRange != null)
            {
                if (listOptions.DateRange.Start.HasValue)
                {
                    query = query.Where(m => m.CapturedAt >= listOptions.DateRange.Start.Value);
                }

                if (listOptions.DateRange.End.HasValue)
                {
                    query = query.Where(m => m.CapturedAt <= listOptions.DateRange.End.Value);
                }
            }

            // Filter expired states
            if (!listOptions.IncludeExpired)
            {
                query = query.Where(m =>
                {
                    if (m.AdditionalMetadata.TryGetValue("ExpiresAt", out var expiresAtObj) &&
                        expiresAtObj is DateTime expiresAt)
                    {
                        return DateTime.UtcNow <= expiresAt;
                    }
                    return true;
                });
            }

            // Apply limit
            if (listOptions.MaxResults.HasValue)
            {
                query = query.Take(listOptions.MaxResults.Value);
            }

            var results = query.OrderByDescending(m => m.SavedAt).ToList();

            logger?.LogDebug("Found {Count} execution states for workflow {WorkflowName}", results.Count, workflowName);

            await Task.CompletedTask.ConfigureAwait(false); // Make method async
            return results;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to list execution states for workflow {WorkflowName}", workflowName);
            throw;
        }
    }

    /// <summary>
    /// Creates a checkpoint of the current execution state.
    /// </summary>
    /// <param name="context">The execution context to checkpoint.</param>
    /// <param name="checkpointName">Optional name for the checkpoint.</param>
    /// <returns>The checkpoint identifier.</returns>
    public async Task<string> CreateCheckpointAsync(CodeExecutionContext context, string? checkpointName = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var state = await CaptureExecutionStateAsync(context).ConfigureAwait(false);

            var saveOptions = new SaveOptions
            {
                Tags = { "checkpoint", checkpointName ?? "auto" },
                AdditionalMetadata = { ["CheckpointName"] = checkpointName ?? "auto" }
            };

            return await SaveStateAsync(state, saveOptions).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create checkpoint for execution {ExecutionId}", context.ExecutionId);
            throw;
        }
    }

    /// <summary>
    /// Restores execution context from a checkpoint.
    /// </summary>
    /// <param name="checkpointId">The checkpoint identifier.</param>
    /// <param name="targetContext">The context to restore state into.</param>
    /// <returns>True if the restore was successful.</returns>
    public async Task<bool> RestoreFromCheckpointAsync(string checkpointId, CodeExecutionContext targetContext)
    {
        try
        {
            var state = await LoadStateAsync(checkpointId).ConfigureAwait(false);
            await RestoreExecutionStateAsync(state, targetContext).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to restore from checkpoint {CheckpointId}", checkpointId);
            return false;
        }
    }

    /// <summary>
    /// Purges old execution states based on retention policy.
    /// </summary>
    /// <param name="policy">The retention policy to apply.</param>
    /// <returns>The number of states that were purged.</returns>
    public async Task<int> PurgeOldStatesAsync(RetentionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        try
        {
            logger?.LogDebug("Starting purge with retention policy: MaxAge={MaxAge}, MaxStatesPerWorkflow={MaxStates}",
                policy.MaxAge, policy.MaxStatesPerWorkflow);

            var cutoffDate = DateTime.UtcNow - policy.MaxAge;
            var statesToDelete = new List<string>();

            foreach (var kvp in _metadata)
            {
                var metadata = kvp.Value;
                var shouldDelete = false;

                // Check age
                if (metadata.SavedAt < cutoffDate)
                {
                    shouldDelete = true;
                }

                // Check if it has preserve tags
                if (policy.PreserveTags.Any(metadata.Tags.Contains))
                {
                    shouldDelete = false;
                }

                // Check expiration
                if (metadata.AdditionalMetadata.TryGetValue("ExpiresAt", out var expiresAtObj) &&
                    expiresAtObj is DateTime expiresAt &&
                    DateTime.UtcNow > expiresAt)
                {
                    shouldDelete = true;
                }

                if (shouldDelete)
                {
                    statesToDelete.Add(kvp.Key);
                }
            }

            // Delete the identified states
            var deletedCount = 0;
            foreach (var stateId in statesToDelete)
            {
                if (await DeleteStateAsync(stateId).ConfigureAwait(false))
                {
                    deletedCount++;
                }
            }

            logger?.LogDebug("Purged {DeletedCount} execution states", deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to purge old execution states");
            throw;
        }
    }

    private static async Task<CodeExecutionState> CaptureExecutionStateAsync(CodeExecutionContext context)
    {
        var state = new CodeExecutionState
        {
            ExecutionId = context.ExecutionId,
            WorkflowName = context.WorkflowName,
            BlockName = context.CurrentBlockName,
            CapturedAt = DateTime.UtcNow,
            WorkflowState = new Dictionary<string, object>()
        };

        // Capture workflow state (simplified - in real implementation would access internal state)
        foreach (var parameter in context.Parameters)
        {
            state.WorkflowState[parameter.Key] = parameter.Value;
        }

        // Capture async state if available
        if (context is AsyncCodeExecutionContext asyncContext)
        {
            // In a real implementation, this would access the async state
            state.AsyncConfig = asyncContext.AsyncConfig;
        }

        await Task.CompletedTask.ConfigureAwait(false); // Make method async
        return state;
    }

    private async Task RestoreExecutionStateAsync(CodeExecutionState state, CodeExecutionContext targetContext)
    {
        // In a real implementation, this would restore the state to the target context
        // For now, we'll just log the restore operation
        logger?.LogDebug("Restoring execution state {ExecutionId} to context {TargetExecutionId}",
            state.ExecutionId, targetContext.ExecutionId);

        await Task.CompletedTask.ConfigureAwait(false); // Make method async
    }

    private static async Task<bool> ValidateStateAsync(CodeExecutionState state)
    {
        // Basic validation
        if (state.ExecutionId == Guid.Empty)
        {
            return false;
        }

        if (string.IsNullOrEmpty(state.WorkflowName))
        {
            return false;
        }

        // Additional validation can be added here
        await Task.CompletedTask.ConfigureAwait(false); // Make method async
        return true;
    }

    private static string GenerateStateId(CodeExecutionState state) => $"{state.WorkflowName}_{state.ExecutionId:N}_{state.CapturedAt:yyyyMMddHHmmss}";
}

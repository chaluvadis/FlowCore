namespace FlowCore.Persistence;
/// <summary>
/// Service that handles automatic state persistence for workflow executions.
/// Integrates with the workflow engine to save state at appropriate checkpoints.
/// </summary>
/// <remarks>
/// Initializes a new instance of the WorkflowStatePersistenceService class.
/// </remarks>
/// <param name="stateManager">The state manager to use for persistence.</param>
/// <param name="logger">Optional logger.</param>
public class WorkflowStatePersistenceService(
    IStateManager stateManager,
    ILogger<WorkflowStatePersistenceService>? logger = null)
{
    private readonly IStateManager _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));

    /// <summary>
    /// Saves a checkpoint for the workflow execution.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="context">The current execution context.</param>
    /// <param name="status">The current workflow status.</param>
    /// <param name="checkpointFrequency">The checkpoint frequency configuration.</param>
    /// <returns>A task representing the checkpoint operation.</returns>
    public async Task SaveCheckpointAsync(
        string workflowId,
        Guid executionId,
        ExecutionContext context,
        WorkflowStatus status,
        CheckpointFrequency checkpointFrequency = CheckpointFrequency.AfterEachBlock)
    {
        // Check if we should save based on frequency configuration
        if (!ShouldSaveCheckpoint(checkpointFrequency, status))
        {
            return;
        }
        try
        {
            // Create metadata for this checkpoint
            var metadata = new WorkflowStateMetadata(
                workflowId,
                executionId,
                status,
                context.CurrentBlockName,
                stateSize: 0, // Will be calculated by state manager
                workflowVersion: "1.0.0");
            // Save the state
            await _stateManager.SaveStateAsync(workflowId, executionId, new Dictionary<string, object>(context.State), metadata);
            logger?.LogDebug("Saved checkpoint for workflow {WorkflowId}, execution {ExecutionId} at block {BlockName}",
                workflowId, executionId, context.CurrentBlockName ?? "Unknown");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to save checkpoint for workflow {WorkflowId}, execution {ExecutionId}",
                workflowId, executionId);
            // Don't throw - checkpoint failures shouldn't stop workflow execution
        }
    }
    /// <summary>
    /// Loads the latest checkpoint for a workflow execution.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The loaded execution context, or null if no checkpoint found.</returns>
    public async Task<ExecutionContext?> LoadLatestCheckpointAsync(
        string workflowId,
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _stateManager.LoadStateAsync(workflowId, executionId);
            if (state == null)
            {
                logger?.LogDebug("No checkpoint found for workflow {WorkflowId}, execution {ExecutionId}",
                    workflowId, executionId);
                return null;
            }
            var metadata = await _stateManager.GetStateMetadataAsync(workflowId, executionId);
            if (metadata == null)
            {
                logger?.LogWarning("Found state but no metadata for workflow {WorkflowId}, execution {ExecutionId}",
                    workflowId, executionId);
                return null;
            }
            // Create a new execution context with the loaded state
            var context = new ExecutionContext(
                state,
                cancellationToken,
                workflowId);
            logger?.LogInformation("Loaded checkpoint for workflow {WorkflowId}, execution {ExecutionId} from block {BlockName}",
                workflowId, executionId, metadata.CurrentBlockName ?? "Unknown");
            return context;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to load checkpoint for workflow {WorkflowId}, execution {ExecutionId}",
                workflowId, executionId);
            return null;
        }
    }
    /// <summary>
    /// Cleans up old checkpoints based on the configuration.
    /// </summary>
    /// <param name="config">The state manager configuration.</param>
    /// <returns>The number of checkpoints cleaned up.</returns>
    public async Task<int> CleanupOldCheckpointsAsync(StateManagerConfig config)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - config.MaxStateAge;
            var deletedCount = await _stateManager.CleanupOldStatesAsync(cutoffDate);
            logger?.LogInformation("Cleaned up {DeletedCount} old workflow checkpoints", deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to cleanup old checkpoints");
            return 0;
        }
    }
    /// <summary>
    /// Determines if a checkpoint should be saved based on frequency configuration.
    /// </summary>
    private static bool ShouldSaveCheckpoint(CheckpointFrequency frequency, WorkflowStatus status)
    {
        return frequency switch
        {
            CheckpointFrequency.Never => false,
            CheckpointFrequency.AfterEachBlock => true,
            CheckpointFrequency.OnErrorOrCompletion =>
                status == WorkflowStatus.Failed ||
                status == WorkflowStatus.Completed ||
                status == WorkflowStatus.Cancelled,
            CheckpointFrequency.Custom => false, // Custom logic would be implemented separately
            _ => true
        };
    }
}
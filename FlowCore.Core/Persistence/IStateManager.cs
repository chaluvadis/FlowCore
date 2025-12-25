namespace FlowCore.Persistence;
/// <summary>
/// Interface for managing workflow state persistence.
/// Handles saving and loading workflow state for long-running workflows.
/// </summary>
public interface IStateManager : IDisposable
{
    /// <summary>
    /// Saves the workflow state with the specified identifier.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <param name="state">The state data to save.</param>
    /// <param name="metadata">Additional metadata about the state.</param>
    /// <returns>A task representing the save operation.</returns>
    Task SaveStateAsync(
        string workflowId,
        Guid executionId,
        IDictionary<string, object> state,
        WorkflowStateMetadata? metadata = null);
    /// <summary>
    /// Loads the workflow state for the specified identifier.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <returns>The loaded state data, or null if not found.</returns>
    Task<IDictionary<string, object>?> LoadStateAsync(string workflowId, Guid executionId);
    /// <summary>
    /// Deletes the workflow state for the specified identifier.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <returns>A task representing the delete operation.</returns>
    Task DeleteStateAsync(string workflowId, Guid executionId);
    /// <summary>
    /// Checks if state exists for the specified identifier.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <returns>True if state exists, false otherwise.</returns>
    Task<bool> StateExistsAsync(string workflowId, Guid executionId);
    /// <summary>
    /// Gets all execution IDs for a specific workflow.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <returns>A collection of execution IDs.</returns>
    Task<IEnumerable<Guid>> GetExecutionIdsAsync(string workflowId);
    /// <summary>
    /// Gets the metadata for a specific workflow execution.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <returns>The metadata for the execution, or null if not found.</returns>
    Task<WorkflowStateMetadata?> GetStateMetadataAsync(string workflowId, Guid executionId);
    /// <summary>
    /// Updates the metadata for a workflow execution.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <param name="metadata">The updated metadata.</param>
    /// <returns>A task representing the update operation.</returns>
    Task UpdateStateMetadataAsync(string workflowId, Guid executionId, WorkflowStateMetadata metadata);
    /// <summary>
    /// Cleans up old workflow states based on the specified criteria.
    /// </summary>
    /// <param name="olderThan">Delete states older than this timestamp.</param>
    /// <param name="workflowId">Optional workflow ID filter.</param>
    /// <param name="status">Optional status filter.</param>
    /// <returns>The number of states deleted.</returns>
    Task<int> CleanupOldStatesAsync(DateTime olderThan, string? workflowId = null, WorkflowStatus? status = null);
    /// <summary>
    /// Gets statistics about stored workflow states.
    /// </summary>
    /// <returns>Statistics about the stored states.</returns>
    Task<StateManagerStatistics> GetStatisticsAsync();
}
/// <summary>
/// Metadata associated with a workflow state.
/// </summary>
/// <remarks>
/// Initializes a new instance of the WorkflowStateMetadata class.
/// </remarks>
/// <param name="workflowId">The workflow ID.</param>
/// <param name="executionId">The execution ID.</param>
/// <param name="status">The workflow status.</param>
/// <param name="currentBlockName">The current block name.</param>
/// <param name="stateSize">The size of the state data.</param>
/// <param name="workflowVersion">The workflow version.</param>
/// <param name="customMetadata">Additional custom metadata.</param>
public class WorkflowStateMetadata(
    string workflowId,
    Guid executionId,
    WorkflowStatus status,
    string? currentBlockName = null,
    long stateSize = 0,
    string workflowVersion = "1.0.0",
    IDictionary<string, object>? customMetadata = null,
    DateTime? createdAt = null,
    DateTime? updatedAt = null)
{
    /// <summary>
    /// Gets the workflow ID.
    /// </summary>
    public string WorkflowId { get; } = workflowId;
    /// <summary>
    /// Gets the execution ID.
    /// </summary>
    public Guid ExecutionId { get; } = executionId;
    /// <summary>
    /// Gets the timestamp when the state was created.
    /// </summary>
    public DateTime CreatedAt { get; } = createdAt ?? DateTime.UtcNow;
    /// <summary>
    /// Gets the timestamp when the state was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; private set; } = updatedAt ?? DateTime.UtcNow;
    /// <summary>
    /// Gets the current status of the workflow execution.
    /// </summary>
    public WorkflowStatus Status { get; private set; } = status;
    /// <summary>
    /// Gets the name of the current block being executed.
    /// </summary>
    public string? CurrentBlockName { get; private set; } = currentBlockName;
    /// <summary>
    /// Gets the size of the state data in bytes.
    /// </summary>
    public long StateSize { get; } = stateSize;
    /// <summary>
    /// Gets the version of the workflow definition.
    /// </summary>
    public string WorkflowVersion { get; } = workflowVersion;
    /// <summary>
    /// Gets additional custom metadata.
    /// </summary>
    public IDictionary<string, object> CustomMetadata { get; } = customMetadata ?? new Dictionary<string, object>();

    /// <summary>
    /// Updates the timestamp to indicate the state was recently updated.
    /// </summary>
    public void MarkUpdated() => UpdatedAt = DateTime.UtcNow;
    /// <summary>
    /// Updates the current block name.
    /// </summary>
    /// <param name="blockName">The new current block name.</param>
    public void UpdateCurrentBlock(string blockName)
    {
        CurrentBlockName = blockName;
        MarkUpdated();
    }
    /// <summary>
    /// Updates the workflow status.
    /// </summary>
    /// <param name="status">The new workflow status.</param>
    public void UpdateStatus(WorkflowStatus status)
    {
        Status = status;
        MarkUpdated();
    }
}
/// <summary>
/// Statistics about the state manager.
/// </summary>
public class StateManagerStatistics
{
    /// <summary>
    /// Gets the total number of stored workflow states.
    /// </summary>
    public long TotalStates { get; internal set; }
    /// <summary>
    /// Gets the total size of all stored states in bytes.
    /// </summary>
    public long TotalSizeBytes { get; internal set; }
    /// <summary>
    /// Gets the number of active (running) workflow executions.
    /// </summary>
    public long ActiveExecutions { get; internal set; }
    /// <summary>
    /// Gets the number of completed workflow executions.
    /// </summary>
    public long CompletedExecutions { get; internal set; }
    /// <summary>
    /// Gets the number of failed workflow executions.
    /// </summary>
    public long FailedExecutions { get; internal set; }
    /// <summary>
    /// Gets the average state size in bytes.
    /// </summary>
    public long AverageStateSize => TotalStates > 0 ? TotalSizeBytes / TotalStates : 0;
    /// <summary>
    /// Gets the timestamp when statistics were generated.
    /// </summary>
    public DateTime GeneratedAt { get; internal set; } = DateTime.UtcNow;
}
/// <summary>
/// Configuration for state manager behavior.
/// </summary>
public class StateManagerConfig
{
    /// <summary>
    /// Gets the frequency for automatic state persistence.
    /// </summary>
    public CheckpointFrequency CheckpointFrequency { get; set; } = CheckpointFrequency.AfterEachBlock;
    /// <summary>
    /// Gets the maximum age for stored states before cleanup.
    /// </summary>
    public TimeSpan MaxStateAge { get; set; } = TimeSpan.FromDays(30);
    /// <summary>
    /// Gets the compression settings for state data.
    /// </summary>
    public StateCompressionConfig Compression { get; set; } = new StateCompressionConfig();
    /// <summary>
    /// Gets the encryption settings for state data.
    /// </summary>
    public StateEncryptionConfig Encryption { get; set; } = new StateEncryptionConfig();
    /// <summary>
    /// Gets whether to enable state versioning.
    /// </summary>
    public bool EnableVersioning { get; set; } = true;
    /// <summary>
    /// Gets the maximum number of versions to keep per execution.
    /// </summary>
    public int MaxVersionsPerExecution { get; set; } = 10;
}
/// <summary>
/// Frequency for automatic state persistence checkpoints.
/// </summary>
public enum CheckpointFrequency
{
    /// <summary>
    /// Never automatically save state.
    /// </summary>
    Never,
    /// <summary>
    /// Save state after each block execution.
    /// </summary>
    AfterEachBlock,
    /// <summary>
    /// Save state only on error or completion.
    /// </summary>
    OnErrorOrCompletion,
    /// <summary>
    /// Save state at custom intervals.
    /// </summary>
    Custom
}
/// <summary>
/// Configuration for state data compression.
/// </summary>
public class StateCompressionConfig
{
    /// <summary>
    /// Gets whether compression is enabled.
    /// </summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// Gets the minimum size threshold for compression.
    /// </summary>
    public long MinSizeThreshold { get; set; } = 1024; // 1KB
    /// <summary>
    /// Gets the compression algorithm to use.
    /// </summary>
    public CompressionAlgorithm Algorithm { get; set; } = CompressionAlgorithm.GZip;
}
/// <summary>
/// Configuration for state data encryption.
/// </summary>
public class StateEncryptionConfig
{
    /// <summary>
    /// Gets whether encryption is enabled.
    /// </summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// Gets the encryption key identifier.
    /// </summary>
    public string? KeyIdentifier { get; set; }
    /// <summary>
    /// Gets the encryption algorithm to use.
    /// </summary>
    public EncryptionAlgorithm Algorithm { get; set; } = EncryptionAlgorithm.AES256;
}
/// <summary>
/// Available compression algorithms.
/// </summary>
public enum CompressionAlgorithm
{
    /// <summary>
    /// No compression.
    /// </summary>
    None,
    /// <summary>
    /// GZip compression.
    /// </summary>
    GZip,
    /// <summary>
    /// Deflate compression.
    /// </summary>
    Deflate,
    /// <summary>
    /// Brotli compression.
    /// </summary>
    Brotli
}
/// <summary>
/// Available encryption algorithms.
/// </summary>
public enum EncryptionAlgorithm
{
    /// <summary>
    /// No encryption.
    /// </summary>
    None,
    /// <summary>
    /// AES-256 encryption.
    /// </summary>
    AES256,
    /// <summary>
    /// AES-128 encryption.
    /// </summary>
    AES128,
    /// <summary>
    /// ChaCha20-Poly1305 encryption.
    /// </summary>
    ChaCha20Poly1305
}

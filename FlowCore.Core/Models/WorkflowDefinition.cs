namespace FlowCore.Models;
/// <summary>
/// Defines the structure and configuration of a workflow.
/// Contains metadata, block definitions, and execution parameters.
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// Gets the unique identifier for this workflow.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the version of this workflow definition.
    /// </summary>
    public string Version { get; }
    /// <summary>
    /// Gets the display name for this workflow.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the description of what this workflow does.
    /// </summary>
    public string Description { get; }
    /// <summary>
    /// Gets the name of the starting block for workflow execution.
    /// </summary>
    public string StartBlockName { get; }
    /// <summary>
    /// Gets the collection of blocks that make up this workflow.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowBlockDefinition> Blocks { get; }
    /// <summary>
    /// Gets the global guards that apply to all blocks in the workflow.
    /// </summary>
    public IReadOnlyList<GuardDefinition> GlobalGuards { get; }
    /// <summary>
    /// Gets the block-specific guards for each block in the workflow.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<GuardDefinition>> BlockGuards { get; }
    /// <summary>
    /// Gets the metadata associated with this workflow.
    /// </summary>
    public WorkflowMetadata Metadata { get; }
    /// <summary>
    /// Gets the execution configuration for this workflow.
    /// </summary>
    public WorkflowExecutionConfig ExecutionConfig { get; }
    /// <summary>
    /// Gets the variables available to this workflow.
    /// </summary>
    public IReadOnlyDictionary<string, object> Variables { get; }
    private WorkflowDefinition(
        string id,
        string version,
        string name,
        string description,
        string startBlockName,
        IDictionary<string, WorkflowBlockDefinition> blocks,
        WorkflowMetadata metadata,
        WorkflowExecutionConfig executionConfig,
        IDictionary<string, object> variables,
        IList<GuardDefinition> globalGuards,
        IDictionary<string, IList<GuardDefinition>> blockGuards)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
        StartBlockName = startBlockName ?? throw new ArgumentNullException(nameof(startBlockName));
        Blocks = new ReadOnlyDictionary<string, WorkflowBlockDefinition>(blocks ?? throw new ArgumentNullException(nameof(blocks)));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        ExecutionConfig = executionConfig ?? throw new ArgumentNullException(nameof(executionConfig));
        Variables = new ReadOnlyDictionary<string, object>(variables ?? new Dictionary<string, object>());
        GlobalGuards = new ReadOnlyCollection<GuardDefinition>(globalGuards ?? new List<GuardDefinition>());
        BlockGuards = new ReadOnlyDictionary<string, IReadOnlyList<GuardDefinition>>(
            blockGuards?.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<GuardDefinition>)new ReadOnlyCollection<GuardDefinition>(kvp.Value ?? new List<GuardDefinition>())) ??
            new Dictionary<string, IReadOnlyList<GuardDefinition>>());
    }
    /// <summary>
    /// Creates a new workflow definition.
    /// </summary>
    /// <param name="id">The unique identifier for the workflow.</param>
    /// <param name="name">The display name for the workflow.</param>
    /// <param name="startBlockName">The name of the starting block.</param>
    /// <param name="blocks">The dictionary of blocks that make up the workflow.</param>
    /// <param name="version">The version of the workflow (optional, defaults to "1.0.0").</param>
    /// <param name="description">The description of the workflow (optional).</param>
    /// <param name="metadata">The metadata for the workflow (optional).</param>
    /// <param name="executionConfig">The execution configuration (optional).</param>
    /// <param name="variables">The variables available to the workflow (optional).</param>
    /// <returns>A new workflow definition.</returns>
    public static WorkflowDefinition Create(
        string id,
        string name,
        string startBlockName,
        IDictionary<string, WorkflowBlockDefinition> blocks,
        string? version = null,
        string? description = null,
        WorkflowMetadata? metadata = null,
        WorkflowExecutionConfig? executionConfig = null,
        IDictionary<string, object>? variables = null,
        IList<GuardDefinition>? globalGuards = null,
        IDictionary<string, IList<GuardDefinition>>? blockGuards = null) => new WorkflowDefinition(
            id,
            version ?? "1.0.0",
            name,
            description ?? string.Empty,
            startBlockName,
            blocks,
            metadata ?? new WorkflowMetadata(),
            executionConfig ?? new WorkflowExecutionConfig(),
            variables ?? new Dictionary<string, object>(),
            globalGuards ?? new List<GuardDefinition>(),
            blockGuards ?? new Dictionary<string, IList<GuardDefinition>>());
    /// <summary>
    /// Gets a block definition by name.
    /// </summary>
    /// <param name="blockName">The name of the block to retrieve.</param>
    /// <returns>The block definition, or null if not found.</returns>
    public WorkflowBlockDefinition? GetBlock(string blockName) => Blocks.TryGetValue(blockName, out var block) ? block : null;
    /// <summary>
    /// Validates that the workflow definition is valid and can be executed.
    /// </summary>
    /// <returns>True if the workflow is valid, false otherwise.</returns>
    public bool IsValid()
    {
        // Check that start block exists
        if (!Blocks.ContainsKey(StartBlockName))
        {
            return false;
        }
        // Check that all referenced blocks exist
        foreach (var block in Blocks.Values)
        {
            if (!string.IsNullOrEmpty(block.NextBlockOnSuccess) && !Blocks.ContainsKey(block.NextBlockOnSuccess))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(block.NextBlockOnFailure) && !Blocks.ContainsKey(block.NextBlockOnFailure))
            {
                return false;
            }
        }
        return true;
    }
}
/// <summary>
/// Defines a single block within a workflow.
/// </summary>
/// <remarks>
/// Initializes a new instance of the WorkflowBlockDefinition class.
/// </remarks>
public class WorkflowBlockDefinition(
    string blockId,
    string blockType,
    string assemblyName,
    string nextBlockOnSuccess,
    string nextBlockOnFailure,
    IDictionary<string, object>? configuration = null,
    string? @namespace = null,
    string? version = null,
    string? displayName = null,
    string? description = null)
{
    /// <summary>
    /// Gets the unique identifier for this block within the workflow.
    /// </summary>
    public string BlockId { get; } = blockId ?? throw new ArgumentNullException(nameof(blockId));
    /// <summary>
    /// Gets the name of the next block to execute on success.
    /// </summary>
    public string NextBlockOnSuccess { get; } = nextBlockOnSuccess ?? string.Empty;
    /// <summary>
    /// Gets the name of the next block to execute on failure.
    /// </summary>
    public string NextBlockOnFailure { get; } = nextBlockOnFailure ?? string.Empty;
    /// <summary>
    /// Gets the configuration parameters for this block.
    /// </summary>
    public IReadOnlyDictionary<string, object> Configuration { get; } = new ReadOnlyDictionary<string, object>(configuration ?? new Dictionary<string, object>());
    /// <summary>
    /// Gets the type of the block implementation.
    /// </summary>
    public string BlockType { get; } = blockType ?? throw new ArgumentNullException(nameof(blockType));
    /// <summary>
    /// Gets the assembly containing the block implementation.
    /// </summary>
    public string AssemblyName { get; } = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
    /// <summary>
    /// Gets the namespace containing the block implementation.
    /// </summary>
    public string? Namespace { get; } = @namespace;
    /// <summary>
    /// Gets the version of this block definition.
    /// </summary>
    public string Version { get; } = version ?? "1.0.0";
    /// <summary>
    /// Gets the display name for this block.
    /// </summary>
    public string DisplayName { get; } = displayName ?? blockType;
    /// <summary>
    /// Gets the description of what this block does.
    /// </summary>
    public string Description { get; } = description ?? $"Block of type {blockType}";
}
/// <summary>
/// Metadata associated with a workflow.
/// </summary>
public class WorkflowMetadata
{
    /// <summary>
    /// Gets the author of the workflow.
    /// </summary>
    public string Author { get; set; } = string.Empty;
    /// <summary>
    /// Gets the tags associated with the workflow.
    /// </summary>
    public ICollection<string> Tags { get; } = [];
    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// Gets the last modification timestamp.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// Gets additional custom metadata.
    /// </summary>
    public IDictionary<string, object> CustomMetadata { get; } = new Dictionary<string, object>();
}
/// <summary>
/// Configuration for workflow execution.
/// </summary>
public class WorkflowExecutionConfig
{
    /// <summary>
    /// Gets the maximum time allowed for workflow execution.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
    /// <summary>
    /// Gets the retry policy for failed blocks.
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = new RetryPolicy();
    /// <summary>
    /// Gets whether to persist state after each block execution.
    /// </summary>
    public bool PersistStateAfterEachBlock { get; set; } = true;
    /// <summary>
    /// Gets the maximum number of concurrent blocks allowed.
    /// </summary>
    public int MaxConcurrentBlocks { get; set; } = 1;
    /// <summary>
    /// Gets whether to enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
/// <summary>
/// Configuration for retry behavior.
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Gets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    /// <summary>
    /// Gets the initial delay before the first retry.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>
    /// Gets the maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);
    /// <summary>
    /// Gets the backoff strategy to use.
    /// </summary>
    public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;
    /// <summary>
    /// Gets the backoff multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}
/// <summary>
/// Strategies for retry backoff.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    /// No delay between retries.
    /// </summary>
    Immediate,
    /// <summary>
    /// Fixed delay between retries.
    /// </summary>
    Fixed,
    /// <summary>
    /// Linearly increasing delay between retries.
    /// </summary>
    Linear,
    /// <summary>
    /// Exponentially increasing delay between retries.
    /// </summary>
    Exponential
}
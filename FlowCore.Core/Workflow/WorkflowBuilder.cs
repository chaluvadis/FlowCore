namespace FlowCore.Workflow;

/// <summary>
/// Initializes a new instance of the FlowCoreWorkflowBuilder class.
/// </summary>
/// <param name="id">The unique identifier for the workflow.</param>
/// <param name="name">The display name for the workflow.</param>
public class FlowCoreWorkflowBuilder(string id, string name)
{
    private readonly string _id = id ?? throw new ArgumentNullException(nameof(id));
    private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));
    private string? _startBlockName;
    private string? _version;
    private string? _description;
    private readonly Dictionary<string, WorkflowBlockDefinition> _blocks = [];
    private readonly WorkflowMetadata _metadata = WorkflowMetadata.Create();
    private readonly WorkflowExecutionConfig _executionConfig = WorkflowExecutionConfig.Create();
    private readonly Dictionary<string, object> _variables = [];

    /// <summary>
    /// Creates a new instance of FlowCoreWorkflowBuilder.
    /// </summary>
    /// <param name="id">The unique identifier for the workflow.</param>
    /// <param name="name">The display name for the workflow.</param>
    /// <returns>A new FlowCoreWorkflowBuilder instance.</returns>
    public static FlowCoreWorkflowBuilder Create(string id, string name) => new(id, name);

    /// <summary>
    /// Sets the version of the workflow.
    /// </summary>
    /// <param name="version">The version string.</param>
    /// <returns>The workflow builder for method chaining.</returns>
    public FlowCoreWorkflowBuilder WithVersion(string version)
    {
        _version = version;
        return this;
    }

    /// <summary>
    /// Sets the description of the workflow.
    /// </summary>
    /// <param name="description">The description text.</param>
    /// <returns>The workflow builder for method chaining.</returns>
    public FlowCoreWorkflowBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the author of the workflow.
    /// </summary>
    /// <param name="author">The author's name.</param>
    /// <returns>The workflow builder for method chaining.</returns>
    public FlowCoreWorkflowBuilder WithAuthor(string author)
    {
        _metadata.Author = author;
        return this;
    }

    /// <summary>
    /// Adds tags to the workflow.
    /// </summary>
    /// <param name="tags">The tags to add.</param>
    /// <returns>The workflow builder for method chaining.</returns>
    public FlowCoreWorkflowBuilder WithTags(params string[] tags)
    {
        foreach (var tag in tags)
        {
            if (!string.IsNullOrEmpty(tag))
            {
                _metadata.Tags.Add(tag);
            }
        }
        return this;
    }

    /// <summary>
    /// Adds a variable to the workflow.
    /// </summary>
    /// <param name="key">The variable key.</param>
    /// <param name="value">The variable value.</param>
    /// <returns>The workflow builder for method chaining.</returns>
    public FlowCoreWorkflowBuilder WithVariable(string key, object value)
    {
        _variables[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the execution configuration for the workflow.
    /// </summary>
    /// <param name="config">The execution configuration.</param>
    /// <returns>The workflow builder for method chaining.</returns>
    public FlowCoreWorkflowBuilder WithExecutionConfig(WorkflowExecutionConfig config)
    {
        _executionConfig.Timeout = config.Timeout;
        _executionConfig.RetryPolicy = config.RetryPolicy;
        _executionConfig.PersistStateAfterEachBlock = config.PersistStateAfterEachBlock;
        _executionConfig.MaxConcurrentBlocks = config.MaxConcurrentBlocks;
        _executionConfig.EnableDetailedLogging = config.EnableDetailedLogging;
        return this;
    }

    /// <summary>
    /// Starts the workflow with the specified block.
    /// </summary>
    /// <param name="block">The block to start with.</param>
    /// <returns>A block builder for configuring the starting block.</returns>
    public FlowCoreWorkflowBlockBuilder StartWith(IWorkflowBlock block)
    {
        var blockBuilder = new FlowCoreWorkflowBlockBuilder(this, block);
        _startBlockName = block.BlockId;
        return blockBuilder;
    }

    /// <summary>
    /// Starts the workflow with a block of the specified type.
    /// </summary>
    /// <param name="blockType">The type of block to create.</param>
    /// <param name="blockId">The unique identifier for the block.</param>
    /// <returns>A block builder for configuring the starting block.</returns>
    public FlowCoreWorkflowBlockBuilder StartWith(string blockType, string blockId)
    {
        var blockBuilder = new FlowCoreWorkflowBlockBuilder(this, blockType, blockId);
        _startBlockName = blockId;
        return blockBuilder;
    }

    /// <summary>
    /// Adds a block to the workflow without connecting it.
    /// </summary>
    /// <param name="block">The block to add.</param>
    /// <returns>A block builder for configuring the block.</returns>
    public FlowCoreWorkflowBlockBuilder AddBlock(IWorkflowBlock block)
        => new(this, block);

    /// <summary>
    /// Adds a block of the specified type to the workflow without connecting it.
    /// </summary>
    /// <param name="blockType">The type of block to create.</param>
    /// <param name="blockId">The unique identifier for the block.</param>
    /// <returns>A block builder for configuring the block.</returns>
    public FlowCoreWorkflowBlockBuilder AddBlock(string blockType, string blockId)
        => new(this, blockType, blockId);

    /// <summary>
    /// Builds the workflow definition.
    /// </summary>
    /// <returns>The constructed workflow definition.</returns>
    public WorkflowDefinition Build()
    {
        if (string.IsNullOrEmpty(_startBlockName))
        {
            throw new InvalidOperationException("Workflow must have a starting block. Use StartWith() to specify the starting block.");
        }

        return WorkflowDefinition.Create(
            _id,
            _name,
            _startBlockName,
            _blocks,
            _version,
            _description,
            _metadata,
            _executionConfig,
            _variables);
    }

    /// <summary>
    /// Internal method to add a block definition to the workflow.
    /// </summary>
    internal void AddBlockDefinition(WorkflowBlockDefinition blockDefinition)
        => _blocks[blockDefinition.BlockId] = blockDefinition;

    /// <summary>
    /// Builder for configuring individual workflow blocks.
    /// </summary>
    public class FlowCoreWorkflowBlockBuilder
    {
        private readonly FlowCoreWorkflowBuilder _workflowBuilder;
        private WorkflowBlockDefinition _blockDefinition;

        /// <summary>
        /// Initializes a new instance of the FlowCoreWorkflowBlockBuilder class.
        /// </summary>
        /// <param name="workflowBuilder">The parent workflow builder.</param>
        /// <param name="block">The workflow block to configure.</param>
        public FlowCoreWorkflowBlockBuilder(FlowCoreWorkflowBuilder workflowBuilder, IWorkflowBlock block)
        {
            _workflowBuilder = workflowBuilder ?? throw new ArgumentNullException(nameof(workflowBuilder));

            _blockDefinition = WorkflowBlockDefinition.Create(
                block.BlockId,
                block.GetType().FullName ?? block.GetType().Name,
                block.GetType().Assembly.GetName().Name ?? "Unknown",
                block.NextBlockOnSuccess,
                block.NextBlockOnFailure,
                configuration: new Dictionary<string, object>(),
                version: block.Version,
                displayName: block.DisplayName,
                description: block.Description);

            _workflowBuilder.AddBlockDefinition(_blockDefinition);
        }

        /// <summary>
        /// Initializes a new instance of the FlowCoreWorkflowBlockBuilder class.
        /// </summary>
        /// <param name="workflowBuilder">The parent workflow builder.</param>
        /// <param name="blockType">The type of block to create.</param>
        /// <param name="blockId">The unique identifier for the block.</param>
        public FlowCoreWorkflowBlockBuilder(FlowCoreWorkflowBuilder workflowBuilder, string blockType, string blockId)
        {
            _workflowBuilder = workflowBuilder ?? throw new ArgumentNullException(nameof(workflowBuilder));

            _blockDefinition = WorkflowBlockDefinition.Create(
                blockId,
                blockType,
                "FlowCore", // Default assembly
                string.Empty, // Next block on success - to be set
                string.Empty, // Next block on failure - to be set
                configuration: new Dictionary<string, object>());

            _workflowBuilder.AddBlockDefinition(_blockDefinition);
        }

        /// <summary>
        /// Sets the next block to execute on success.
        /// </summary>
        /// <param name="nextBlockId">The identifier of the next block.</param>
        /// <returns>The block builder for method chaining.</returns>
        public FlowCoreWorkflowBlockBuilder OnSuccessGoTo(string nextBlockId)
        {
            UpdateBlockDefinition(nextBlockOnSuccess: nextBlockId);
            return this;
        }

        /// <summary>
        /// Sets the next block to execute on failure.
        /// </summary>
        /// <param name="nextBlockId">The identifier of the next block.</param>
        /// <returns>The block builder for method chaining.</returns>
        public FlowCoreWorkflowBlockBuilder OnFailureGoTo(string nextBlockId)
        {
            UpdateBlockDefinition(nextBlockOnFailure: nextBlockId);
            return this;
        }

        /// <summary>
        /// Sets both success and failure transitions to the same block.
        /// </summary>
        /// <param name="nextBlockId">The identifier of the next block.</param>
        /// <returns>The block builder for method chaining.</returns>
        public FlowCoreWorkflowBlockBuilder ThenGoTo(string nextBlockId)
            => OnSuccessGoTo(nextBlockId).OnFailureGoTo(nextBlockId);

        /// <summary>
        /// Adds configuration to the block.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The configuration value.</param>
        /// <returns>The block builder for method chaining.</returns>
        public FlowCoreWorkflowBlockBuilder WithConfig(string key, object value)
        {
            var config = new Dictionary<string, object>(_blockDefinition.Configuration)
            {
                [key] = value
            };

            UpdateBlockDefinition(configuration: config);
            return this;
        }

        /// <summary>
        /// Sets the display name for the block.
        /// </summary>
        /// <param name="displayName">The display name.</param>
        /// <returns>The block builder for method chaining.</returns>
        public FlowCoreWorkflowBlockBuilder WithDisplayName(string displayName)
        {
            UpdateBlockDefinition(displayName: displayName);
            return this;
        }

        /// <summary>
        /// Sets the description for the block.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <returns>The block builder for method chaining.</returns>
        public FlowCoreWorkflowBlockBuilder WithDescription(string description)
        {
            UpdateBlockDefinition(description: description);
            return this;
        }

        /// <summary>
        /// Creates an updated block definition with the specified changes.
        /// </summary>
        private WorkflowBlockDefinition CreateUpdatedDefinition(
            string? nextBlockOnSuccess = null,
            string? nextBlockOnFailure = null,
            Dictionary<string, object>? configuration = null,
            string? displayName = null,
            string? description = null) => WorkflowBlockDefinition.Create(
                _blockDefinition.BlockId,
                _blockDefinition.BlockType,
                _blockDefinition.AssemblyName,
                nextBlockOnSuccess ?? _blockDefinition.NextBlockOnSuccess,
                nextBlockOnFailure ?? _blockDefinition.NextBlockOnFailure,
                configuration ?? new Dictionary<string, object>(_blockDefinition.Configuration),
                _blockDefinition.Namespace,
                _blockDefinition.Version,
                displayName ?? _blockDefinition.DisplayName,
                description ?? _blockDefinition.Description);

        /// <summary>
        /// Updates the block definition with the specified changes and persists it.
        /// </summary>
        private void UpdateBlockDefinition(
            string? nextBlockOnSuccess = null,
            string? nextBlockOnFailure = null,
            Dictionary<string, object>? configuration = null,
            string? displayName = null,
            string? description = null)
        {
            var updated = CreateUpdatedDefinition(nextBlockOnSuccess, nextBlockOnFailure, configuration, displayName, description);
            _workflowBuilder.AddBlockDefinition(updated);
            _blockDefinition = updated;
        }

        /// <summary>
        /// Adds the block to the workflow and returns to the workflow builder.
        /// </summary>
        /// <returns>The parent workflow builder for method chaining.</returns>
        public FlowCoreWorkflowBuilder And() => _workflowBuilder;
    }
}

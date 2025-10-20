namespace LinkedListWorkflowEngine.Core;

/// <summary>
/// Initializes a new instance of the WorkflowBuilder class.
/// </summary>
/// <param name="id">The unique identifier for the workflow.</param>
/// <param name="name">The display name for the workflow.</param>
public class WorkflowBuilder(string id, string name)
{
    private readonly string _id = id ?? throw new ArgumentNullException(nameof(id));
    private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));
    private string? _startBlockName;
    private string? _version;
    private string? _description;
    private readonly Dictionary<string, WorkflowBlockDefinition> _blocks = [];
    private readonly WorkflowMetadata _metadata = new();
    private readonly WorkflowExecutionConfig _executionConfig = new();
    private readonly Dictionary<string, object> _variables = [];

    /// <summary>
    /// Sets the version of the workflow.
    /// </summary>
    /// <param name="version">The version string.</param>
    /// <returns>The workflow builder for method chaining.</returns>
    public WorkflowBuilder WithVersion(string version)
    {
        _version = version;
        return this;
    }

    /// <summary>
    /// Sets the description of the workflow.
    /// </summary>
    /// <param name="description">The description text.</param>
    /// <returns>The workflow builder for method chaining.</returns>
    public WorkflowBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the author of the workflow.
    /// </summary>
    /// <param name="author">The author's name.</param>
    /// <returns>The workflow builder for method chaining.</returns>
    public WorkflowBuilder WithAuthor(string author)
    {
        _metadata.Author = author;
        return this;
    }

    /// <summary>
    /// Adds tags to the workflow.
    /// </summary>
    /// <param name="tags">The tags to add.</param>
    /// <returns>The workflow builder for method chaining.</returns>
    public WorkflowBuilder WithTags(params string[] tags)
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
    public WorkflowBuilder WithVariable(string key, object value)
    {
        _variables[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the execution configuration for the workflow.
    /// </summary>
    /// <param name="config">The execution configuration.</param>
    /// <returns>The workflow builder for method chaining.</returns>
    public WorkflowBuilder WithExecutionConfig(WorkflowExecutionConfig config)
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
    public WorkflowBlockBuilder StartWith(IWorkflowBlock block)
    {
        var blockBuilder = new WorkflowBlockBuilder(this, block);
        _startBlockName = block.BlockId;
        return blockBuilder;
    }

    /// <summary>
    /// Starts the workflow with a block of the specified type.
    /// </summary>
    /// <param name="blockType">The type of block to create.</param>
    /// <param name="blockId">The unique identifier for the block.</param>
    /// <returns>A block builder for configuring the starting block.</returns>
    public WorkflowBlockBuilder StartWith(string blockType, string blockId)
    {
        var blockBuilder = new WorkflowBlockBuilder(this, blockType, blockId);
        _startBlockName = blockId;
        return blockBuilder;
    }

    /// <summary>
    /// Adds a block to the workflow without connecting it.
    /// </summary>
    /// <param name="block">The block to add.</param>
    /// <returns>A block builder for configuring the block.</returns>
    public WorkflowBlockBuilder AddBlock(IWorkflowBlock block)
        => new(this, block);

    /// <summary>
    /// Adds a block of the specified type to the workflow without connecting it.
    /// </summary>
    /// <param name="blockType">The type of block to create.</param>
    /// <param name="blockId">The unique identifier for the block.</param>
    /// <returns>A block builder for configuring the block.</returns>
    public WorkflowBlockBuilder AddBlock(string blockType, string blockId)
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
    public class WorkflowBlockBuilder
    {
        private readonly WorkflowBuilder _workflowBuilder;
        private readonly WorkflowBlockDefinition _blockDefinition;

        /// <summary>
        /// Initializes a new instance of the WorkflowBlockBuilder class.
        /// </summary>
        /// <param name="workflowBuilder">The parent workflow builder.</param>
        /// <param name="block">The workflow block to configure.</param>
        public WorkflowBlockBuilder(WorkflowBuilder workflowBuilder, IWorkflowBlock block)
        {
            _workflowBuilder = workflowBuilder ?? throw new ArgumentNullException(nameof(workflowBuilder));

            _blockDefinition = new WorkflowBlockDefinition(
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
        /// Initializes a new instance of the WorkflowBlockBuilder class.
        /// </summary>
        /// <param name="workflowBuilder">The parent workflow builder.</param>
        /// <param name="blockType">The type of block to create.</param>
        /// <param name="blockId">The unique identifier for the block.</param>
        public WorkflowBlockBuilder(WorkflowBuilder workflowBuilder,string blockType, string blockId)
        {
            _workflowBuilder = workflowBuilder ?? throw new ArgumentNullException(nameof(workflowBuilder));

            _blockDefinition = new WorkflowBlockDefinition(
                blockId,
                blockType,
                "LinkedListWorkflowEngine.Core", // Default assembly
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
        public WorkflowBlockBuilder OnSuccessGoTo(string nextBlockId)
        {
            var updatedDefinition = new WorkflowBlockDefinition(
                _blockDefinition.BlockId,
                _blockDefinition.BlockType,
                _blockDefinition.AssemblyName,
                nextBlockId,
                _blockDefinition.NextBlockOnFailure,
                new Dictionary<string, object>(_blockDefinition.Configuration),
                _blockDefinition.Namespace,
                _blockDefinition.Version,
                _blockDefinition.DisplayName,
                _blockDefinition.Description);

            _workflowBuilder.AddBlockDefinition(updatedDefinition);
            return this;
        }

        /// <summary>
        /// Sets the next block to execute on failure.
        /// </summary>
        /// <param name="nextBlockId">The identifier of the next block.</param>
        /// <returns>The block builder for method chaining.</returns>
        public WorkflowBlockBuilder OnFailureGoTo(string nextBlockId)
        {
            var updatedDefinition = new WorkflowBlockDefinition(
                _blockDefinition.BlockId,
                _blockDefinition.BlockType,
                _blockDefinition.AssemblyName,
                _blockDefinition.NextBlockOnSuccess,
                nextBlockId,
                new Dictionary<string, object>(_blockDefinition.Configuration),
                _blockDefinition.Namespace,
                _blockDefinition.Version,
                _blockDefinition.DisplayName,
                _blockDefinition.Description);

            _workflowBuilder.AddBlockDefinition(updatedDefinition);
            return this;
        }

        /// <summary>
        /// Sets both success and failure transitions to the same block.
        /// </summary>
        /// <param name="nextBlockId">The identifier of the next block.</param>
        /// <returns>The block builder for method chaining.</returns>
        public WorkflowBlockBuilder ThenGoTo(string nextBlockId)
            => OnSuccessGoTo(nextBlockId).OnFailureGoTo(nextBlockId);

        /// <summary>
        /// Adds configuration to the block.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The configuration value.</param>
        /// <returns>The block builder for method chaining.</returns>
        public WorkflowBlockBuilder WithConfig(string key, object value)
        {
            var config = new Dictionary<string, object>(_blockDefinition.Configuration)
            {
                [key] = value
            };

            var updatedDefinition = new WorkflowBlockDefinition(
                _blockDefinition.BlockId,
                _blockDefinition.BlockType,
                _blockDefinition.AssemblyName,
                _blockDefinition.NextBlockOnSuccess,
                _blockDefinition.NextBlockOnFailure,
                config,
                _blockDefinition.Namespace,
                _blockDefinition.Version,
                _blockDefinition.DisplayName,
                _blockDefinition.Description);

            _workflowBuilder.AddBlockDefinition(updatedDefinition);
            return this;
        }

        /// <summary>
        /// Sets the display name for the block.
        /// </summary>
        /// <param name="displayName">The display name.</param>
        /// <returns>The block builder for method chaining.</returns>
        public WorkflowBlockBuilder WithDisplayName(string displayName)
        {
            var updatedDefinition = new WorkflowBlockDefinition(
                _blockDefinition.BlockId,
                _blockDefinition.BlockType,
                _blockDefinition.AssemblyName,
                _blockDefinition.NextBlockOnSuccess,
                _blockDefinition.NextBlockOnFailure,
                new Dictionary<string, object>(_blockDefinition.Configuration),
                _blockDefinition.Namespace,
                _blockDefinition.Version,
                displayName,
                _blockDefinition.Description);

            _workflowBuilder.AddBlockDefinition(updatedDefinition);
            return this;
        }

        /// <summary>
        /// Sets the description for the block.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <returns>The block builder for method chaining.</returns>
        public WorkflowBlockBuilder WithDescription(string description)
        {
            var updatedDefinition = new WorkflowBlockDefinition(
                _blockDefinition.BlockId,
                _blockDefinition.BlockType,
                _blockDefinition.AssemblyName,
                _blockDefinition.NextBlockOnSuccess,
                _blockDefinition.NextBlockOnFailure,
                new Dictionary<string, object>(_blockDefinition.Configuration),
                _blockDefinition.Namespace,
                _blockDefinition.Version,
                _blockDefinition.DisplayName,
                description);

            _workflowBuilder.AddBlockDefinition(updatedDefinition);
            return this;
        }

        /// <summary>
        /// Adds the block to the workflow and returns to the workflow builder.
        /// </summary>
        /// <returns>The parent workflow builder for method chaining.</returns>
        public WorkflowBuilder And() => _workflowBuilder;
    }
}
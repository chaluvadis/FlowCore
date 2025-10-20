namespace LinkedListWorkflowEngine.Tests;
public class WorkflowBuilderTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        // Assert
        var definition = builder.Build();
        Assert.Equal("test-workflow", definition.Id);
        Assert.Equal("Test Workflow", definition.Name);
        Assert.Equal("1.0.0", definition.Version); // Default version
        Assert.Equal("", definition.Description); // Default empty description
        Assert.Empty(definition.Blocks); // No blocks added yet
    }
    [Fact]
    public void Constructor_WithNullId_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WorkflowBuilder(null!, "Test Workflow"));
    }
    [Fact]
    public void Constructor_WithNullName_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WorkflowBuilder("test-workflow", null!));
    }
    [Fact]
    public void WithVersion_ShouldSetVersion()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        // Act
        var result = builder.WithVersion("2.0.0");
        // Assert
        Assert.Same(builder, result); // Should return same instance for chaining
        var definition = builder.Build();
        Assert.Equal("2.0.0", definition.Version);
    }
    [Fact]
    public void WithDescription_ShouldSetDescription()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        // Act
        var result = builder.WithDescription("A test workflow description");
        // Assert
        Assert.Same(builder, result); // Should return same instance for chaining
        var definition = builder.Build();
        Assert.Equal("A test workflow description", definition.Description);
    }
    [Fact]
    public void WithAuthor_ShouldSetAuthor()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        // Act
        var result = builder.WithAuthor("Test Author");
        // Assert
        Assert.Same(builder, result); // Should return same instance for chaining
        var definition = builder.Build();
        Assert.Equal("Test Author", definition.Metadata.Author);
    }
    [Fact]
    public void WithTags_ShouldAddTags()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        // Act
        var result = builder.WithTags("tag1", "tag2", "tag3");
        // Assert
        Assert.Same(builder, result); // Should return same instance for chaining
        var definition = builder.Build();
        Assert.Equal(3, definition.Metadata.Tags.Count);
        Assert.Contains("tag1", definition.Metadata.Tags);
        Assert.Contains("tag2", definition.Metadata.Tags);
        Assert.Contains("tag3", definition.Metadata.Tags);
    }
    [Fact]
    public void WithTags_WithEmptyTags_ShouldIgnoreEmptyTags()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        // Act
        var result = builder.WithTags("tag1", "", "tag2", null!, "tag3");
        // Assert
        var definition = builder.Build();
        Assert.Equal(3, definition.Metadata.Tags.Count);
        Assert.Contains("tag1", definition.Metadata.Tags);
        Assert.Contains("tag2", definition.Metadata.Tags);
        Assert.Contains("tag3", definition.Metadata.Tags);
    }
    [Fact]
    public void WithVariable_ShouldAddVariable()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        // Act
        var result = builder.WithVariable("key1", "value1")
                           .WithVariable("key2", 42)
                           .WithVariable("key3", true);
        // Assert
        Assert.Same(builder, result); // Should return same instance for chaining
        var definition = builder.Build();
        Assert.Equal(3, definition.Variables.Count);
        Assert.Equal("value1", definition.Variables["key1"]);
        Assert.Equal(42, definition.Variables["key2"]);
        Assert.Equal(true, definition.Variables["key3"]);
    }
    [Fact]
    public void WithExecutionConfig_ShouldSetExecutionConfiguration()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        var customConfig = new WorkflowExecutionConfig
        {
            Timeout = TimeSpan.FromMinutes(10),
            PersistStateAfterEachBlock = false,
            MaxConcurrentBlocks = 5,
            EnableDetailedLogging = true,
            RetryPolicy = new RetryPolicy
            {
                MaxRetries = 5,
                InitialDelay = TimeSpan.FromSeconds(2),
                BackoffStrategy = BackoffStrategy.Fixed
            }
        };
        // Act
        var result = builder.WithExecutionConfig(customConfig);
        // Assert
        Assert.Same(builder, result); // Should return same instance for chaining
        var definition = builder.Build();
        Assert.Equal(TimeSpan.FromMinutes(10), definition.ExecutionConfig.Timeout);
        Assert.False(definition.ExecutionConfig.PersistStateAfterEachBlock);
        Assert.Equal(5, definition.ExecutionConfig.MaxConcurrentBlocks);
        Assert.True(definition.ExecutionConfig.EnableDetailedLogging);
        Assert.Equal(5, definition.ExecutionConfig.RetryPolicy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(2), definition.ExecutionConfig.RetryPolicy.InitialDelay);
        Assert.Equal(BackoffStrategy.Fixed, definition.ExecutionConfig.RetryPolicy.BackoffStrategy);
    }
    [Fact]
    public void StartWith_WithBlockTypeAndId_ShouldCreateBlockBuilder()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        // Act
        var blockBuilder = builder.StartWith("TestBlockType", "TestBlock");
        // Assert
        Assert.NotNull(blockBuilder);
        var definition = builder.Build();
        Assert.Equal("TestBlock", definition.StartBlockName);
        Assert.Single(definition.Blocks);
        Assert.True(definition.Blocks.ContainsKey("TestBlock"));
        var block = definition.Blocks["TestBlock"];
        Assert.Equal("TestBlock", block.BlockId);
        Assert.Equal("TestBlockType", block.BlockType);
        Assert.Equal("LinkedListWorkflowEngine.Core", block.AssemblyName);
    }
    [Fact]
    public void StartWith_WithIWorkflowBlock_ShouldCreateBlockBuilder()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        var mockBlock = new Mock<IWorkflowBlock>();
        mockBlock.Setup(b => b.BlockId).Returns("MockBlock");
        mockBlock.Setup(b => b.NextBlockOnSuccess).Returns("NextBlock");
        mockBlock.Setup(b => b.NextBlockOnFailure).Returns("ErrorBlock");
        mockBlock.Setup(b => b.Version).Returns("1.0.0");
        mockBlock.Setup(b => b.DisplayName).Returns("Mock Block");
        mockBlock.Setup(b => b.Description).Returns("A mock block");
        // Act
        var blockBuilder = builder.StartWith(mockBlock.Object);
        // Assert
        Assert.NotNull(blockBuilder);
        var definition = builder.Build();
        Assert.Equal("MockBlock", definition.StartBlockName);
        Assert.Single(definition.Blocks);
        Assert.True(definition.Blocks.ContainsKey("MockBlock"));
        var block = definition.Blocks["MockBlock"];
        Assert.Equal("MockBlock", block.BlockId);
        Assert.Equal("NextBlock", block.NextBlockOnSuccess);
        Assert.Equal("ErrorBlock", block.NextBlockOnFailure);
        Assert.Equal("Mock Block", block.DisplayName);
        Assert.Equal("A mock block", block.Description);
    }
    [Fact]
    public void AddBlock_ShouldAddBlockWithoutSettingAsStart()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        // Act
        var blockBuilder = builder.AddBlock("TestBlockType", "TestBlock");
        // Assert
        Assert.NotNull(blockBuilder);
        var definition = builder.Build();
        Assert.Equal("", definition.StartBlockName); // Should not be set as start block
        Assert.Single(definition.Blocks);
        Assert.True(definition.Blocks.ContainsKey("TestBlock"));
    }
    [Fact]
    public void WorkflowBlockBuilder_OnSuccessGoTo_ShouldSetSuccessTransition()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        var blockBuilder = builder.StartWith("TestBlockType", "StartBlock");
        // Act
        var result = blockBuilder.OnSuccessGoTo("NextBlock");
        // Assert
        Assert.Same(blockBuilder, result); // Should return same instance for chaining
        var definition = builder.Build();
        var block = definition.Blocks["StartBlock"];
        Assert.Equal("NextBlock", block.NextBlockOnSuccess);
        Assert.Equal("", block.NextBlockOnFailure); // Should remain empty
    }
    [Fact]
    public void WorkflowBlockBuilder_OnFailureGoTo_ShouldSetFailureTransition()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        var blockBuilder = builder.StartWith("TestBlockType", "StartBlock");
        // Act
        var result = blockBuilder.OnFailureGoTo("ErrorBlock");
        // Assert
        Assert.Same(blockBuilder, result); // Should return same instance for chaining
        var definition = builder.Build();
        var block = definition.Blocks["StartBlock"];
        Assert.Equal("", block.NextBlockOnSuccess); // Should remain empty
        Assert.Equal("ErrorBlock", block.NextBlockOnFailure);
    }
    [Fact]
    public void WorkflowBlockBuilder_ThenGoTo_ShouldSetBothTransitions()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        var blockBuilder = builder.StartWith("TestBlockType", "StartBlock");
        // Act
        var result = blockBuilder.ThenGoTo("CommonBlock");
        // Assert
        Assert.Same(blockBuilder, result); // Should return same instance for chaining
        var definition = builder.Build();
        var block = definition.Blocks["StartBlock"];
        Assert.Equal("CommonBlock", block.NextBlockOnSuccess);
        Assert.Equal("CommonBlock", block.NextBlockOnFailure);
    }
    [Fact]
    public void WorkflowBlockBuilder_WithConfig_ShouldAddConfiguration()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        var blockBuilder = builder.StartWith("TestBlockType", "StartBlock");
        // Act
        var result = blockBuilder.WithConfig("timeout", 30)
                                .WithConfig("retryCount", 3)
                                .WithConfig("enabled", true);
        // Assert
        Assert.Same(blockBuilder, result); // Should return same instance for chaining
        var definition = builder.Build();
        var block = definition.Blocks["StartBlock"];
        Assert.Equal(3, block.Configuration.Count);
        Assert.Equal(30, block.Configuration["timeout"]);
        Assert.Equal(3, block.Configuration["retryCount"]);
        Assert.Equal(true, block.Configuration["enabled"]);
    }
    [Fact]
    public void WorkflowBlockBuilder_WithDisplayName_ShouldSetDisplayName()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        var blockBuilder = builder.StartWith("TestBlockType", "StartBlock");
        // Act
        var result = blockBuilder.WithDisplayName("Custom Display Name");
        // Assert
        Assert.Same(blockBuilder, result); // Should return same instance for chaining
        var definition = builder.Build();
        var block = definition.Blocks["StartBlock"];
        Assert.Equal("Custom Display Name", block.DisplayName);
    }
    [Fact]
    public void WorkflowBlockBuilder_WithDescription_ShouldSetDescription()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        var blockBuilder = builder.StartWith("TestBlockType", "StartBlock");
        // Act
        var result = blockBuilder.WithDescription("Custom block description");
        // Assert
        Assert.Same(blockBuilder, result); // Should return same instance for chaining
        var definition = builder.Build();
        var block = definition.Blocks["StartBlock"];
        Assert.Equal("Custom block description", block.Description);
    }
    [Fact]
    public void WorkflowBlockBuilder_And_ShouldReturnWorkflowBuilder()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        var blockBuilder = builder.StartWith("TestBlockType", "StartBlock");
        // Act
        var result = blockBuilder.And();
        // Assert
        Assert.Same(builder, result); // Should return the workflow builder
    }
    [Fact]
    public void Build_WithoutStartBlock_ShouldThrowException()
    {
        // Arrange
        var builder = new WorkflowBuilder("test-workflow", "Test Workflow");
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Workflow must have a starting block", exception.Message);
    }
    [Fact]
    public void Build_WithCompleteWorkflow_ShouldCreateValidDefinition()
    {
        // Arrange
        var builder = new WorkflowBuilder("complete-workflow", "Complete Workflow")
            .WithVersion("1.0.0")
            .WithDescription("A complete workflow for testing")
            .WithAuthor("Test Author")
            .WithTags("test", "complete")
            .WithVariable("input", "test data")
            .WithVariable("count", 10);
        // Add blocks
        var startBlock = builder.StartWith("StartBlockType", "StartBlock")
            .WithDisplayName("Start Block")
            .WithDescription("The starting block");
        var processBlock = builder.AddBlock("ProcessBlockType", "ProcessBlock")
            .WithDisplayName("Process Block")
            .WithDescription("Processes the data");
        // Connect blocks
        startBlock.OnSuccessGoTo("ProcessBlock");
        processBlock.OnSuccessGoTo("EndBlock");
        var endBlock = builder.AddBlock("EndBlockType", "EndBlock")
            .WithDisplayName("End Block")
            .WithDescription("The final block");
        // Act
        var definition = builder.Build();
        // Assert
        Assert.NotNull(definition);
        Assert.Equal("complete-workflow", definition.Id);
        Assert.Equal("Complete Workflow", definition.Name);
        Assert.Equal("1.0.0", definition.Version);
        Assert.Equal("A complete workflow for testing", definition.Description);
        Assert.Equal("StartBlock", definition.StartBlockName);
        // Check metadata
        Assert.Equal("Test Author", definition.Metadata.Author);
        Assert.Equal(2, definition.Metadata.Tags.Count);
        Assert.Contains("test", definition.Metadata.Tags);
        Assert.Contains("complete", definition.Metadata.Tags);
        // Check variables
        Assert.Equal(2, definition.Variables.Count);
        Assert.Equal("test data", definition.Variables["input"]);
        Assert.Equal(10, definition.Variables["count"]);
        // Check blocks
        Assert.Equal(3, definition.Blocks.Count);
        Assert.True(definition.Blocks.ContainsKey("StartBlock"));
        Assert.True(definition.Blocks.ContainsKey("ProcessBlock"));
        Assert.True(definition.Blocks.ContainsKey("EndBlock"));
        // Check block connections
        var startBlockDef = definition.Blocks["StartBlock"];
        Assert.Equal("ProcessBlock", startBlockDef.NextBlockOnSuccess);
        Assert.Equal("", startBlockDef.NextBlockOnFailure);
        var processBlockDef = definition.Blocks["ProcessBlock"];
        Assert.Equal("EndBlock", processBlockDef.NextBlockOnSuccess);
        Assert.Equal("", processBlockDef.NextBlockOnFailure);
        var endBlockDef = definition.Blocks["EndBlock"];
        Assert.Equal("", endBlockDef.NextBlockOnSuccess);
        Assert.Equal("", endBlockDef.NextBlockOnFailure);
        // Check block properties
        Assert.Equal("Start Block", startBlockDef.DisplayName);
        Assert.Equal("The starting block", startBlockDef.Description);
        Assert.Equal("Process Block", processBlockDef.DisplayName);
        Assert.Equal("Processes the data", processBlockDef.Description);
        Assert.Equal("End Block", endBlockDef.DisplayName);
        Assert.Equal("The final block", endBlockDef.Description);
        // Validate the workflow
        Assert.True(definition.IsValid());
    }
    [Fact]
    public void Build_WithComplexBlockConfiguration_ShouldPreserveConfiguration()
    {
        // Arrange
        var builder = new WorkflowBuilder("config-workflow", "Config Workflow");
        var block = builder.StartWith("ConfigBlockType", "ConfigBlock")
            .WithConfig("timeout", 30)
            .WithConfig("retryCount", 3)
            .WithConfig("enabled", true)
            .WithConfig("threshold", 0.95)
            .WithConfig("endpoint", "https://api.example.com")
            .OnSuccessGoTo("NextBlock");
        // Act
        var definition = builder.Build();
        // Assert
        var blockDef = definition.Blocks["ConfigBlock"];
        Assert.Equal(5, blockDef.Configuration.Count);
        Assert.Equal(30, blockDef.Configuration["timeout"]);
        Assert.Equal(3, blockDef.Configuration["retryCount"]);
        Assert.Equal(true, blockDef.Configuration["enabled"]);
        Assert.Equal(0.95, blockDef.Configuration["threshold"]);
        Assert.Equal("https://api.example.com", blockDef.Configuration["endpoint"]);
        Assert.Equal("NextBlock", blockDef.NextBlockOnSuccess);
    }
    [Fact]
    public void MethodChaining_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var definition = new WorkflowBuilder("chain-workflow", "Chain Workflow")
            .WithVersion("1.0.0")
            .WithDescription("Testing method chaining")
            .WithAuthor("Chain Author")
            .WithTags("chain", "test")
            .WithVariable("var1", "value1")
            .WithVariable("var2", 42)
            .StartWith("StartType", "StartBlock")
                .WithDisplayName("Start")
                .WithDescription("Start block")
                .OnSuccessGoTo("EndBlock")
                .And()
            .AddBlock("EndType", "EndBlock")
                .WithDisplayName("End")
                .WithDescription("End block")
                .And()
            .Build();
        // Assert
        Assert.NotNull(definition);
        Assert.Equal("chain-workflow", definition.Id);
        Assert.Equal("1.0.0", definition.Version);
        Assert.Equal("Testing method chaining", definition.Description);
        Assert.Equal("Chain Author", definition.Metadata.Author);
        Assert.Equal(2, definition.Metadata.Tags.Count);
        Assert.Equal(2, definition.Variables.Count);
        Assert.Equal(2, definition.Blocks.Count);
        Assert.Equal("StartBlock", definition.StartBlockName);
        Assert.True(definition.IsValid());
    }
    [Fact]
    public void MultipleBlocks_WithComplexTransitions_ShouldHandleCorrectly()
    {
        // Arrange
        var builder = new WorkflowBuilder("multi-workflow", "Multi Block Workflow");
        // Create a workflow: Start -> Process -> (Success->End, Failure->Error)
        var startBlock = builder.StartWith("StartType", "Start")
            .OnSuccessGoTo("Process");
        var processBlock = builder.AddBlock("ProcessType", "Process")
            .OnSuccessGoTo("End")
            .OnFailureGoTo("Error");
        var endBlock = builder.AddBlock("EndType", "End");
        var errorBlock = builder.AddBlock("ErrorType", "Error");
        // Act
        var definition = builder.Build();
        // Assert
        Assert.NotNull(definition);
        Assert.Equal("Start", definition.StartBlockName);
        Assert.Equal(4, definition.Blocks.Count);
        // Check transitions
        Assert.Equal("Process", definition.Blocks["Start"].NextBlockOnSuccess);
        Assert.Equal("End", definition.Blocks["Process"].NextBlockOnSuccess);
        Assert.Equal("Error", definition.Blocks["Process"].NextBlockOnFailure);
        Assert.Equal("", definition.Blocks["End"].NextBlockOnSuccess);
        Assert.Equal("", definition.Blocks["Error"].NextBlockOnSuccess);
        Assert.True(definition.IsValid());
    }
    [Fact]
    public void Build_WithExecutionConfig_ShouldPreserveConfiguration()
    {
        // Arrange
        var customConfig = new WorkflowExecutionConfig
        {
            Timeout = TimeSpan.FromMinutes(15),
            PersistStateAfterEachBlock = false,
            MaxConcurrentBlocks = 10,
            EnableDetailedLogging = true,
            RetryPolicy = new RetryPolicy
            {
                MaxRetries = 7,
                InitialDelay = TimeSpan.FromSeconds(5),
                MaxDelay = TimeSpan.FromMinutes(2),
                BackoffStrategy = BackoffStrategy.Linear,
                BackoffMultiplier = 1.5
            }
        };
        var builder = new WorkflowBuilder("config-workflow", "Config Workflow")
            .WithExecutionConfig(customConfig);
        // Add a minimal block to make it valid
        builder.StartWith("DummyType", "DummyBlock");
        // Act
        var definition = builder.Build();
        // Assert
        Assert.Equal(TimeSpan.FromMinutes(15), definition.ExecutionConfig.Timeout);
        Assert.False(definition.ExecutionConfig.PersistStateAfterEachBlock);
        Assert.Equal(10, definition.ExecutionConfig.MaxConcurrentBlocks);
        Assert.True(definition.ExecutionConfig.EnableDetailedLogging);
        Assert.Equal(7, definition.ExecutionConfig.RetryPolicy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(5), definition.ExecutionConfig.RetryPolicy.InitialDelay);
        Assert.Equal(TimeSpan.FromMinutes(2), definition.ExecutionConfig.RetryPolicy.MaxDelay);
        Assert.Equal(BackoffStrategy.Linear, definition.ExecutionConfig.RetryPolicy.BackoffStrategy);
        Assert.Equal(1.5, definition.ExecutionConfig.RetryPolicy.BackoffMultiplier);
    }
}
namespace FlowCore.Tests;

public class JsonWorkflowEngineTests : IDisposable
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IWorkflowBlockFactory> _mockBlockFactory;
    private readonly Mock<IStateManager> _mockStateManager;
    private readonly ILogger<JsonWorkflowEngine> _logger;
    private readonly JsonWorkflowEngine _jsonWorkflowEngine;
    private readonly string _testWorkflowId = "test-json-workflow";
    public JsonWorkflowEngineTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<JsonWorkflowEngine>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockBlockFactory = new Mock<IWorkflowBlockFactory>();
        _mockStateManager = new Mock<IStateManager>();
        _jsonWorkflowEngine = new JsonWorkflowEngine(
            _mockServiceProvider.Object,
            _logger,
            _mockBlockFactory.Object,
            _mockStateManager.Object);
    }
    public void Dispose()
    {
        _mockStateManager.Object.Dispose();
    }
    [Fact]
    public async Task ExecuteFromJsonAsync_WithValidJson_ShouldExecuteSuccessfully()
    {
        // Arrange
        var jsonDefinition = CreateValidJsonWorkflowDefinition();
        var input = new { message = "test input" };
        var ct = CancellationToken.None;
        var mockBlock = new Mock<IWorkflowBlock>();
        mockBlock.Setup(b => b.ExecuteAsync(It.IsAny<Models.ExecutionContext>()))
            .ReturnsAsync(ExecutionResult.Success(null, "block output"));
        _mockBlockFactory.Setup(f => f.CreateBlock(It.IsAny<WorkflowBlockDefinition>()))
            .Returns(mockBlock.Object);
        // Act
        var result = await _jsonWorkflowEngine.ExecuteFromJsonAsync(jsonDefinition, input, ct);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(_testWorkflowId, result.WorkflowId);
        Assert.Equal("1.0.0", result.WorkflowVersion);
        Assert.Equal(WorkflowStatus.Completed, result.Status);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.FinalState);
        Assert.True(result.Duration > TimeSpan.Zero);
    }
    [Fact]
    public async Task ExecuteFromJsonAsync_WithInvalidJson_ShouldThrowException()
    {
        // Arrange
        var invalidJson = "{ invalid json content }";
        var input = new { message = "test input" };
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _jsonWorkflowEngine.ExecuteFromJsonAsync(invalidJson, input));
        Assert.Contains("Invalid JSON workflow definition", exception.Message);
    }
    [Fact]
    public async Task ExecuteFromJsonAsync_WithNullJson_ShouldThrowException()
    {
        // Arrange
        var input = new { message = "test input" };
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _jsonWorkflowEngine.ExecuteFromJsonAsync(null!, input));
    }
    [Fact]
    public async Task ExecuteFromJsonAsync_WithNullInput_ShouldThrowException()
    {
        // Arrange
        var jsonDefinition = CreateValidJsonWorkflowDefinition();
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _jsonWorkflowEngine.ExecuteFromJsonAsync(jsonDefinition, null!));
    }
    [Fact]
    public async Task ExecuteFromJsonFileAsync_WithValidFile_ShouldExecuteSuccessfully()
    {
        // Arrange
        var jsonDefinition = CreateValidJsonWorkflowDefinition();
        var tempFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFilePath, jsonDefinition);
        var input = new { message = "test input from file" };
        var mockBlock = new Mock<IWorkflowBlock>();
        mockBlock.Setup(b => b.ExecuteAsync(It.IsAny<Models.ExecutionContext>()))
            .ReturnsAsync(ExecutionResult.Success(null, "file block output"));
        _mockBlockFactory.Setup(f => f.CreateBlock(It.IsAny<WorkflowBlockDefinition>()))
            .Returns(mockBlock.Object);
        try
        {
            // Act
            var result = await _jsonWorkflowEngine.ExecuteFromJsonFileAsync(tempFilePath, input);
            // Assert
            Assert.NotNull(result);
            Assert.Equal(_testWorkflowId, result.WorkflowId);
            Assert.Equal(WorkflowStatus.Completed, result.Status);
            Assert.True(result.Succeeded);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
    [Fact]
    public async Task ExecuteFromJsonFileAsync_WithNonExistentFile_ShouldThrowException()
    {
        // Arrange
        var nonExistentFilePath = "/path/to/nonexistent/file.json";
        var input = new { message = "test input" };
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _jsonWorkflowEngine.ExecuteFromJsonFileAsync(nonExistentFilePath, input));
    }
    [Fact]
    public async Task ExecuteFromJsonFileAsync_WithNullFilePath_ShouldThrowException()
    {
        // Arrange
        var input = new { message = "test input" };
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _jsonWorkflowEngine.ExecuteFromJsonFileAsync(null!, input));
    }
    [Fact]
    public void ParseWorkflowDefinition_WithValidJson_ShouldReturnWorkflowDefinition()
    {
        // Arrange
        var jsonDefinition = CreateValidJsonWorkflowDefinition();
        // Act
        var result = _jsonWorkflowEngine.ParseWorkflowDefinition(jsonDefinition);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(_testWorkflowId, result.Id);
        Assert.Equal("1.0.0", result.Version);
        Assert.Equal("Test JSON Workflow", result.Name);
        Assert.Equal("A test workflow defined in JSON", result.Description);
        Assert.Equal("TestBlock", result.StartBlockName);
        Assert.Single(result.Blocks);
        Assert.True(result.IsValid());
    }
    [Fact]
    public void ParseWorkflowDefinition_WithInvalidJson_ShouldThrowException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _jsonWorkflowEngine.ParseWorkflowDefinition(invalidJson));
        Assert.Contains("Invalid JSON workflow definition format", exception.Message);
    }
    [Fact]
    public void ParseWorkflowDefinition_WithNullJson_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _jsonWorkflowEngine.ParseWorkflowDefinition(null!));
    }
    [Fact]
    public void ParseWorkflowDefinition_WithNullDeserialization_ShouldThrowException()
    {
        // Arrange
        var jsonDefinition = @"{
            ""id"": """ + _testWorkflowId + @""",
            ""name"": ""Test Workflow"",
            ""startBlockName"": ""TestBlock"",
            ""blocks"": null
        }";
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _jsonWorkflowEngine.ParseWorkflowDefinition(jsonDefinition));
        Assert.Contains("Failed to deserialize JSON workflow definition", exception.Message);
    }
    [Fact]
    public void ValidateJsonDefinition_WithValidJson_ShouldReturnTrue()
    {
        // Arrange
        var jsonDefinition = CreateValidJsonWorkflowDefinition();
        // Act
        var result = _jsonWorkflowEngine.ValidateJsonDefinition(jsonDefinition);
        // Assert
        Assert.True(result);
    }
    [Fact]
    public void ValidateJsonDefinition_WithInvalidJson_ShouldReturnFalse()
    {
        // Arrange
        var invalidJson = "{ invalid json content }";
        // Act
        var result = _jsonWorkflowEngine.ValidateJsonDefinition(invalidJson);
        // Assert
        Assert.False(result);
    }
    [Fact]
    public void ValidateJsonDefinition_WithNullJson_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _jsonWorkflowEngine.ValidateJsonDefinition(null!));
    }
    [Fact]
    public void ValidateJsonDefinition_WithStructurallyValidButLogicallyInvalidJson_ShouldReturnFalse()
    {
        // Arrange
        var invalidWorkflowJson = @"{
            ""id"": """ + _testWorkflowId + @""",
            ""name"": ""Test Workflow"",
            ""version"": ""1.0.0"",
            ""description"": ""Test workflow"",
            ""startBlockName"": ""NonExistentBlock"",
            ""blocks"": {
                ""TestBlock"": {
                    ""id"": ""TestBlock"",
                    ""name"": ""TestBlock"",
                    ""type"": ""TestBlockType"",
                    ""assembly"": ""TestAssembly""
                }
            }
        }";
        // Act
        var result = _jsonWorkflowEngine.ValidateJsonDefinition(invalidWorkflowJson);
        // Assert
        Assert.False(result); // Should be false because start block doesn't exist
    }
    [Fact]
    public void ParseWorkflowDefinition_WithComplexJson_ShouldHandleAllProperties()
    {
        // Arrange
        var complexJson = @"{
            ""id"": """ + _testWorkflowId + @""",
            ""name"": ""Complex Test Workflow"",
            ""version"": ""2.0.0"",
            ""description"": ""A complex workflow with all features"",
            ""startBlockName"": ""StartBlock"",
            ""variables"": {
                ""stringVar"": ""test value"",
                ""intVar"": 42,
                ""boolVar"": true
            },
            ""blocks"": {
                ""StartBlock"": {
                    ""id"": ""StartBlock"",
                    ""name"": ""StartBlock"",
                    ""type"": ""StartBlockType"",
                    ""assembly"": ""TestAssembly"",
                    ""displayName"": ""Start Block"",
                    ""description"": ""Starting block"",
                    ""nextBlockOnSuccess"": ""ProcessBlock"",
                    ""nextBlockOnFailure"": ""ErrorBlock"",
                    ""configuration"": {
                        ""timeout"": 30,
                        ""retryCount"": 3
                    }
                },
                ""ProcessBlock"": {
                    ""id"": ""ProcessBlock"",
                    ""name"": ""ProcessBlock"",
                    ""type"": ""ProcessBlockType"",
                    ""assembly"": ""TestAssembly""
                }
            },
            ""metadata"": {
                ""author"": ""Test Author"",
                ""tags"": [""test"", ""complex""],
                ""createdAt"": ""2023-12-25T10:30:45Z"",
                ""customMetadata"": {
                    ""project"": ""Test Project""
                }
            },
            ""executionConfig"": {
                ""timeout"": ""00:05:00"",
                ""persistStateAfterEachBlock"": true,
                ""maxConcurrentBlocks"": 1,
                ""enableDetailedLogging"": true,
                ""retryPolicy"": {
                    ""maxRetries"": 5,
                    ""initialDelay"": ""00:00:02"",
                    ""maxDelay"": ""00:01:00"",
                    ""backoffStrategy"": ""Exponential"",
                    ""backoffMultiplier"": 2.5
                }
            }
        }";
        // Act
        var result = _jsonWorkflowEngine.ParseWorkflowDefinition(complexJson);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(_testWorkflowId, result.Id);
        Assert.Equal("2.0.0", result.Version);
        Assert.Equal("Complex Test Workflow", result.Name);
        Assert.Equal("A complex workflow with all features", result.Description);
        Assert.Equal("StartBlock", result.StartBlockName);
        // Check variables
        Assert.Equal(3, result.Variables.Count);
        Assert.Equal("test value", result.Variables["stringVar"]);
        Assert.Equal(42, result.Variables["intVar"]);
        Assert.Equal(true, result.Variables["boolVar"]);
        // Check blocks
        Assert.Equal(2, result.Blocks.Count);
        Assert.True(result.Blocks.ContainsKey("StartBlock"));
        Assert.True(result.Blocks.ContainsKey("ProcessBlock"));
        // Check metadata
        Assert.Equal("Test Author", result.Metadata.Author);
        Assert.Equal(2, result.Metadata.Tags.Count);
        Assert.Contains("test", result.Metadata.Tags);
        Assert.Contains("complex", result.Metadata.Tags);
        Assert.Equal("Test Project", result.Metadata.CustomMetadata["project"]);
        // Check execution config
        Assert.Equal(TimeSpan.FromMinutes(5), result.ExecutionConfig.Timeout);
        Assert.True(result.ExecutionConfig.PersistStateAfterEachBlock);
        Assert.Equal(1, result.ExecutionConfig.MaxConcurrentBlocks);
        Assert.True(result.ExecutionConfig.EnableDetailedLogging);
        Assert.Equal(5, result.ExecutionConfig.RetryPolicy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(2), result.ExecutionConfig.RetryPolicy.InitialDelay);
        Assert.Equal(2.5, result.ExecutionConfig.RetryPolicy.BackoffMultiplier);
    }
    [Fact]
    public void ParseWorkflowDefinition_WithMinimalJson_ShouldUseDefaults()
    {
        // Arrange
        var minimalJson = @"{
            ""id"": """ + _testWorkflowId + @""",
            ""name"": ""Minimal Workflow"",
            ""startBlockName"": ""StartBlock"",
            ""blocks"": {
                ""StartBlock"": {
                    ""id"": ""StartBlock"",
                    ""name"": ""StartBlock"",
                    ""type"": ""StartBlockType"",
                    ""assembly"": ""TestAssembly""
                }
            }
        }";
        // Act
        var result = _jsonWorkflowEngine.ParseWorkflowDefinition(minimalJson);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(_testWorkflowId, result.Id);
        Assert.Equal("1.0.0", result.Version); // Default version
        Assert.Equal("Minimal Workflow", result.Name);
        Assert.Equal("", result.Description); // Default empty description
        Assert.Equal("StartBlock", result.StartBlockName);
        Assert.Single(result.Blocks);
        Assert.Empty(result.Variables);
        Assert.Empty(result.GlobalGuards);
        Assert.Empty(result.BlockGuards);
        Assert.Equal("", result.Metadata.Author); // Default empty author
        Assert.Empty(result.Metadata.Tags);
        Assert.Equal(TimeSpan.FromMinutes(30), result.ExecutionConfig.Timeout); // Default timeout
    }
    [Fact]
    public void ParseWorkflowDefinition_WithMissingRequiredFields_ShouldThrowException()
    {
        // Arrange
        var incompleteJson = @"{
            ""id"": """ + _testWorkflowId + @""",
            ""name"": ""Incomplete Workflow""
            // Missing startBlockName and blocks
        }";
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _jsonWorkflowEngine.ParseWorkflowDefinition(incompleteJson));
        Assert.Contains("Failed to deserialize JSON workflow definition", exception.Message);
    }
    [Fact]
    public void ValidateJsonDefinition_WithEmptyJson_ShouldReturnFalse()
    {
        // Arrange
        var emptyJson = "{}";
        // Act
        var result = _jsonWorkflowEngine.ValidateJsonDefinition(emptyJson);
        // Assert
        Assert.False(result);
    }
    [Fact]
    public void ValidateJsonDefinition_WithJsonMissingBlocks_ShouldReturnFalse()
    {
        // Arrange
        var jsonWithoutBlocks = @"{
            ""id"": """ + _testWorkflowId + @""",
            ""name"": ""Workflow Without Blocks"",
            ""startBlockName"": ""StartBlock""
        }";
        // Act
        var result = _jsonWorkflowEngine.ValidateJsonDefinition(jsonWithoutBlocks);
        // Assert
        Assert.False(result);
    }
    [Fact]
    public async Task ExecuteFromJsonAsync_WithCancellationToken_ShouldCancelExecution()
    {
        // Arrange
        var jsonDefinition = CreateValidJsonWorkflowDefinition();
        var input = new { message = "test input" };
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var mockBlock = new Mock<IWorkflowBlock>();
        mockBlock.Setup(b => b.ExecuteAsync(It.IsAny<Models.ExecutionContext>()))
            .Returns(async (Models.ExecutionContext context) =>
            {
                // Simulate some work before cancellation is detected
                await Task.Delay(100);
                return ExecutionResult.Success(null, "block output");
            });
        _mockBlockFactory.Setup(f => f.CreateBlock(It.IsAny<WorkflowBlockDefinition>()))
            .Returns(mockBlock.Object);
        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _jsonWorkflowEngine.ExecuteFromJsonAsync(jsonDefinition, input, cancellationTokenSource.Token));
    }
    private string CreateValidJsonWorkflowDefinition()
    {
        return @"{
            ""id"": """ + _testWorkflowId + @""",
            ""name"": ""Test JSON Workflow"",
            ""version"": ""1.0.0"",
            ""description"": ""A test workflow defined in JSON"",
            ""startBlockName"": ""TestBlock"",
            ""blocks"": {
                ""TestBlock"": {
                    ""id"": ""TestBlock"",
                    ""name"": ""TestBlock"",
                    ""type"": ""TestBlockType"",
                    ""assembly"": ""TestAssembly""
                }
            }
        }";
    }
}

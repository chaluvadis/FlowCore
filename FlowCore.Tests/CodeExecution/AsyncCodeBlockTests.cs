namespace FlowCore.Tests.CodeExecution;
public class AsyncCodeBlockTests : IDisposable
{
    private readonly Mock<IAsyncCodeExecutor> _mockAsyncExecutor;
    private readonly Mock<ICodeExecutor> _mockExecutor;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly ILogger<AsyncCodeBlock> _logger;
    private readonly CodeExecutionConfig _config;
    private readonly AsyncExecutionConfig _asyncConfig;
    public AsyncCodeBlockTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<AsyncCodeBlock>();
        _mockAsyncExecutor = new Mock<IAsyncCodeExecutor>();
        _mockExecutor = _mockAsyncExecutor.As<ICodeExecutor>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _config = CodeExecutionConfig.CreateInline("csharp", "return 42;", enableLogging: false);
        _asyncConfig = AsyncExecutionConfig.Default;
    }
    public void Dispose()
    {
        // Cleanup if needed
    }
    [Fact]
    public async Task CanExecuteAsync_WithValidConfig_ShouldReturnTrue()
    {
        // Arrange
        var context = new Models.ExecutionContext("input", CancellationToken.None, "test");
        _mockAsyncExecutor.Setup(e => e.SupportsAsyncExecution(_config)).Returns(true);
        var block = new AsyncCodeBlock(_mockExecutor.Object, _config, _mockServiceProvider.Object, _asyncConfig, logger: _logger);
        // Act
        var canExecute = await block.CanExecuteAsync(context);
        // Assert
        Assert.True(canExecute);
    }
    [Fact]
    public async Task CanExecuteAsync_WithInvalidMaxParallelism_ShouldReturnFalse()
    {
        // Arrange
        var context = new Models.ExecutionContext("input", CancellationToken.None, "test");
        var invalidAsyncConfig = new AsyncExecutionConfig { MaxDegreeOfParallelism = 0 };
        _mockAsyncExecutor.Setup(e => e.SupportsAsyncExecution(_config)).Returns(true);
        _mockAsyncExecutor.Setup(e => e.ValidateExecutionSafety(_config)).Returns(ValidationResult.Success());
        var block = new AsyncCodeBlock(_mockExecutor.Object, _config, _mockServiceProvider.Object, invalidAsyncConfig, logger: _logger);
        // Act
        var canExecute = await block.CanExecuteAsync(context);
        // Assert
        Assert.False(canExecute);
    }
    [Fact]
    public async Task CleanupAsync_ShouldRemoveAsyncTempKeys()
    {
        // Arrange
        var context = new Models.ExecutionContext("output", CancellationToken.None, "test");
        context.SetState("temp_1", "value1");
        context.SetState("temp_result_2", "value2");
        context.SetState("normal_key", "value3");
        var result = ExecutionResult.Success("next", "output");
        var block = new AsyncCodeBlock(_mockExecutor.Object, _config, _mockServiceProvider.Object, _asyncConfig, logger: _logger);
        // Act
        await block.CleanupAsync(context, result);
        // Assert
        Assert.False(context.ContainsState("temp_1"));
        Assert.False(context.ContainsState("temp_result_2"));
        Assert.True(context.ContainsState("normal_key"));
    }
    [Fact]
    public void Create_WithValidConfig_ShouldReturnAsyncCodeBlock()
    {
        // Arrange & Act
        var block = AsyncCodeBlock.Create(_config, _mockServiceProvider.Object, _asyncConfig, logger: _logger);
        // Assert
        Assert.NotNull(block);
        Assert.IsType<AsyncCodeBlock>(block);
        Assert.Contains(_config.Mode.ToString(), block.DisplayName);
    }
    [Fact]
    public void DisplayName_ShouldIncludeMode()
    {
        // Arrange
        var block = new AsyncCodeBlock(_mockExecutor.Object, _config, _mockServiceProvider.Object, _asyncConfig, logger: _logger);
        // Act & Assert
        Assert.Equal($"AsyncCodeBlock({_config.Mode})", block.DisplayName);
    }
    [Fact]
    public void Description_ShouldIncludeLanguageAndMode()
    {
        // Arrange
        var block = new AsyncCodeBlock(_mockExecutor.Object, _config, _mockServiceProvider.Object, _asyncConfig, logger: _logger);
        // Act & Assert
        Assert.Equal($"Executes {_config.Language} code asynchronously using {_config.Mode} mode", block.Description);
    }
}
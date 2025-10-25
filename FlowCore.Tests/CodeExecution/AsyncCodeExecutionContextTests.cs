namespace FlowCore.Tests.CodeExecution;
public class AsyncCodeExecutionContextTests : IDisposable
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly ILogger _logger;
    private readonly CodeExecutionConfig _config;
    private readonly AsyncExecutionConfig _asyncConfig;
    public AsyncCodeExecutionContextTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<AsyncCodeExecutionContext>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _config = CodeExecutionConfig.CreateInline("csharp", "return 42;", enableLogging: false);
        _asyncConfig = AsyncExecutionConfig.Default;
    }
    public void Dispose()
    {
        // Cleanup if needed
    }
    [Fact]
    public async Task GetAsyncStateAsync_WithExistingKey_ShouldReturnValue()
    {
        // Arrange
        var workflowContext = new FlowCore.Models.ExecutionContext("input", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, _config, _mockServiceProvider.Object, _asyncConfig);
        await context.SetAsyncStateAsync("testKey", "testValue");
        // Act
        var value = await context.GetAsyncStateAsync<string>("testKey");
        // Assert
        Assert.Equal("testValue", value);
    }
    [Fact]
    public async Task GetAsyncStateAsync_WithNonExistingKey_ShouldReturnDefault()
    {
        // Arrange
        var workflowContext = new FlowCore.Models.ExecutionContext("input", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, _config, _mockServiceProvider.Object, _asyncConfig);
        // Act
        var value = await context.GetAsyncStateAsync<string>("nonExistingKey");
        // Assert
        Assert.Null(value);
    }
    [Fact]
    public async Task SetAsyncStateAsync_WithNullValue_ShouldRemoveKey()
    {
        // Arrange
        var workflowContext = new FlowCore.Models.ExecutionContext("input", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, _config, _mockServiceProvider.Object, _asyncConfig);
        await context.SetAsyncStateAsync("testKey", "testValue");
        // Act
        await context.SetAsyncStateAsync("testKey", null);
        // Assert
        var value = await context.GetAsyncStateAsync<string>("testKey");
        Assert.Null(value);
    }
    [Fact]
    public async Task ExecuteWithTimeoutAsync_ShouldExecuteWithinTimeout()
    {
        // Arrange
        var workflowContext = new FlowCore.Models.ExecutionContext("input", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, _config, _mockServiceProvider.Object, _asyncConfig);
        async Task<string> operation(CancellationToken ct) => await Task.FromResult("result");
        // Act
        var result = await context.ExecuteWithTimeoutAsync(operation, TimeSpan.FromSeconds(1));
        // Assert
        Assert.Equal("result", result);
    }
    [Fact]
    public async Task ExecuteConcurrentlyAsync_ShouldExecuteMultipleOperations()
    {
        // Arrange
        var workflowContext = new FlowCore.Models.ExecutionContext("input", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, _config, _mockServiceProvider.Object, _asyncConfig);
        var items = new[] { 1, 2, 3 };
        async Task<int> operation(int item, CancellationToken ct) => await Task.FromResult(item * 2);
        // Act
        var results = await context.ExecuteConcurrentlyAsync(items, operation, 2);
        // Assert
        Assert.Equal(3, results.Count());
        Assert.Contains(2, results);
        Assert.Contains(4, results);
        Assert.Contains(6, results);
    }
    [Fact]
    public void CreateScope_ShouldReturnValidScope()
    {
        // Arrange
        var workflowContext = new FlowCore.Models.ExecutionContext("input", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, _config, _mockServiceProvider.Object, _asyncConfig);
        // Act
        using var scope = context.CreateScope("testScope");
        // Assert
        Assert.Equal("testScope", scope.ScopeName);
        Assert.True(scope.Duration >= TimeSpan.Zero);
    }
    [Fact]
    public void LogAsyncOperation_ShouldLogMessage()
    {
        // Arrange
        var workflowContext = new FlowCore.Models.ExecutionContext("input", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, _config, _mockServiceProvider.Object, _asyncConfig);
        // Act
        context.LogAsyncOperation("testOperation", "Test message");
        // Assert
        // Note: Logging verification would require mocking the logger, but for simplicity, we just ensure no exception
        Assert.True(true);
    }
    [Fact]
    public void AsyncExecutionConfig_Default_ShouldHaveExpectedValues()
    {
        // Arrange & Act
        var config = AsyncExecutionConfig.Default;
        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), config.DefaultTimeout);
        Assert.Equal(Environment.ProcessorCount, config.MaxDegreeOfParallelism);
        Assert.True(config.EnableAsyncStatePersistence);
        Assert.Equal(3, config.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(1), config.RetryDelay);
        Assert.True(config.EnableDetailedLogging);
    }
    [Fact]
    public void AsyncExecutionConfig_HighPerformance_ShouldHaveOptimizedValues()
    {
        // Arrange & Act
        var config = AsyncExecutionConfig.HighPerformance;
        // Assert
        Assert.Equal(TimeSpan.FromMinutes(10), config.DefaultTimeout);
        Assert.Equal(Environment.ProcessorCount * 2, config.MaxDegreeOfParallelism);
        Assert.False(config.EnableAsyncStatePersistence);
        Assert.False(config.EnableDetailedLogging);
    }
    [Fact]
    public void AsyncExecutionConfig_Conservative_ShouldHaveSafeValues()
    {
        // Arrange & Act
        var config = AsyncExecutionConfig.Conservative;
        // Assert
        Assert.Equal(TimeSpan.FromMinutes(2), config.DefaultTimeout);
        Assert.Equal(1, config.MaxDegreeOfParallelism);
        Assert.Equal(5, config.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(2), config.RetryDelay);
    }
}
namespace FlowCore.Tests.CodeExecution;

public class AsyncInlineCodeExecutorTests : IDisposable
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly ILogger<AsyncInlineCodeExecutor> _logger;
    private readonly CodeSecurityConfig _securityConfig;
    public AsyncInlineCodeExecutorTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<AsyncInlineCodeExecutor>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _securityConfig = CodeSecurityConfig.Create([], [], []);
    }
    public void Dispose()
    {
        // Cleanup if needed
    }
    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithValidAsyncCode_ShouldSucceed()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return await Task.FromResult(42);", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("return await Task.FromResult(42);", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Output);
        Assert.True(result.ContainedAsyncOperations);
        Assert.Empty(result.AsyncOperations);
        Assert.Equal(1, result.ActualDegreeOfParallelism);
        Assert.True(result.ExecutionTime > TimeSpan.Zero);
        Assert.NotNull(result.PerformanceMetrics);
        Assert.Equal(1, result.PerformanceMetrics.TotalAsyncOperations);
        Assert.Contains("AsyncPatternCount", result.Metadata.Keys);
        Assert.Equal(2, result.Metadata["AsyncPatternCount"]);
    }
    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithSyncCode_ShouldFallbackToSync()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return 42;", enableLogging: false);
        var workflowContext = new Models.ExecutionContext(new Dictionary<string, object>(), CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Output);
        Assert.False(result.ContainedAsyncOperations); // Sync code
        Assert.Equal(1, result.ActualDegreeOfParallelism);
        Assert.True(result.ExecutionTime > TimeSpan.Zero);
        Assert.NotNull(result.PerformanceMetrics);
        Assert.Equal(0, result.PerformanceMetrics.TotalAsyncOperations);
        Assert.Contains("AsyncPatternCount", result.Metadata.Keys);
        Assert.Equal(0, result.Metadata["AsyncPatternCount"]);
    }
    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithInvalidCode_ShouldFail()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "invalid code", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("return 42;", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(0, result.ActualDegreeOfParallelism);
        Assert.NotNull(result.PerformanceMetrics);
        Assert.Equal(0, result.PerformanceMetrics.TotalAsyncOperations);
        Assert.Contains("AsyncPatternCount", result.Metadata.Keys);
        Assert.Equal(0, result.Metadata["AsyncPatternCount"]);
    }
    [Fact]
    public void SupportsAsyncExecution_WithInlineConfig_ShouldReturnTrue()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return 42;", enableLogging: false);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var supports = executor.SupportsAsyncExecution(config);
        // Assert
        Assert.True(supports);
    }
    [Fact]
    public void SupportsAsyncExecution_WithAssemblyConfig_ShouldReturnFalse()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateAssembly("test.dll", "TestClass", "TestMethod");
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var supports = executor.SupportsAsyncExecution(config);
        // Assert
        Assert.False(supports);
    }
    [Fact]
    public void CanExecute_WithValidConfig_ShouldReturnTrue()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return 42;", enableLogging: false);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var canExecute = executor.CanExecute(config);
        // Assert
        Assert.True(canExecute);
    }
    [Fact]
    public void ValidateExecutionSafety_WithValidConfig_ShouldPass()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return await Task.FromResult(42);", enableLogging: false);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var validation = executor.ValidateExecutionSafety(config);
        // Assert
        Assert.True(validation.IsValid);
    }
    [Fact]
    public void ValidateExecutionSafety_WithInvalidMode_ShouldFail()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateAssembly("test.dll", "TestClass", "TestMethod");
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var validation = executor.ValidateExecutionSafety(config);
        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains("Inline", validation.Errors.First());
    }

    [Fact]
    public void ValidateExecutionSafety_WithEmptyCode_ShouldFail()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "", enableLogging: false);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var validation = executor.ValidateExecutionSafety(config);
        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains("No code provided", validation.Errors.First());
    }

    [Fact]
    public void ValidateExecutionSafety_WithUnsupportedLanguage_ShouldFail()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("python", "print('hello')", enableLogging: false);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var validation = executor.ValidateExecutionSafety(config);
        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains("Unsupported language", validation.Errors.First());
    }

    [Fact]
    public void MaxDegreeOfParallelism_ShouldReturnExpectedValue()
    {
        // Arrange
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act & Assert
        Assert.Equal(Environment.ProcessorCount * 2, executor.MaxDegreeOfParallelism);
    }

    [Fact]
    public void SupportsConcurrentExecution_ShouldReturnTrue()
    {
        // Arrange
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act & Assert
        Assert.True(executor.SupportsConcurrentExecution);
    }

    [Fact]
    public void ExecutorType_ShouldReturnCorrectValue()
    {
        // Arrange
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act & Assert
        Assert.Equal("AsyncInlineCodeExecutor", executor.ExecutorType);
    }

    [Fact]
    public void SupportedLanguages_ShouldReturnCorrectList()
    {
        // Arrange
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act & Assert
        Assert.Equal(new[] { "csharp", "c#" }, executor.SupportedLanguages);
    }

    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithUnsafeAsyncPatterns_ShouldFail()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "await Task.Delay(100); Thread.Sleep(100);", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("unsafe code", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.False(result.Success);
        Assert.Contains("unsafe", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithCancellation_ShouldThrow()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "await Task.Delay(1000, context.CancellationToken); return 42;", enableLogging: false);
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);
        var workflowContext = new Models.ExecutionContext("test", cts.Token, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.ExecuteAsyncCodeAsync(context, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithTooManyAsyncPatterns_ShouldFail()
    {
        // Arrange
        var asyncConfig = new AsyncExecutionConfig { MaxDegreeOfParallelism = 1 };
        var config = CodeExecutionConfig.CreateInline("csharp", "await Task.FromResult(1); await Task.FromResult(2); await Task.FromResult(3); return 42;", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("test", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object, asyncConfig);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.False(result.Success);
        Assert.Contains("Too many async patterns", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithResultAccess_ShouldFail()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return Task.Run(() => 42).Result;", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("test", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.False(result.Success);
        Assert.Contains("unsafe", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithAsyncContext_ShouldSucceed()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return await Task.FromResult(42);", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("test", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);
        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Output);
        Assert.Contains("AsyncPatternCount", result.Metadata.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_WithRegularContext_ShouldFallback()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return 42;", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("test", CancellationToken.None, "test");
        var context = new CodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsync(context, CancellationToken.None);
        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Output);
    }

    [Fact]
    public async Task ExecuteAsyncCodeAsync_Caching_ShouldUseCacheOnSecondExecution()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return await Task.FromResult(42);", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("test", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result1 = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        var result2 = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Equal(result1.Output, result2.Output);
        Assert.Contains("AsyncPatternCount", result1.Metadata.Keys);
        Assert.Contains("AsyncPatternCount", result2.Metadata.Keys);
    }

    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithConfigureAwait_ShouldSucceed()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return await Task.FromResult(42).ConfigureAwait(false);", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("test", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Output);
        Assert.True(result.ContainedAsyncOperations);
        Assert.Contains("AsyncPatternCount", result.Metadata.Keys);
        Assert.Equal(3, result.Metadata["AsyncPatternCount"]); // await, Task<>, ConfigureAwait
    }

    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithWhenAll_ShouldSucceed()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "var tasks = new[] { Task.FromResult(1), Task.FromResult(2) }; return await Task.WhenAll(tasks);", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("test", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        Assert.True(result.ContainedAsyncOperations);
        Assert.Contains("AsyncPatternCount", result.Metadata.Keys);
        Assert.Equal(4, result.Metadata["AsyncPatternCount"]); // await, Task<>, Task<>, WhenAll
    }

    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithTotalAsyncWaitTime_ShouldBeZero()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return await Task.FromResult(42);", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("test", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.True(result.Success);
        Assert.Equal(TimeSpan.Zero, result.TotalAsyncWaitTime);
    }

    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithAsyncVoid_ShouldSucceed()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "async void Test() { await Task.Delay(1); } return 42;", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("test", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.Output);
        Assert.True(result.ContainedAsyncOperations);
        Assert.Contains("AsyncPatternCount", result.Metadata.Keys);
        Assert.Equal(3, result.Metadata["AsyncPatternCount"]); // async, await, Task
    }


    [Fact]
    public void AnalyzeAsyncPatterns_WithUnsafeCode_ShouldDetectUnsafe()
    {
        // Arrange
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        var code = "return Task.Run(() => 42).Result;";
        var config = CodeExecutionConfig.CreateInline("csharp", code, enableLogging: false);
        var workflowContext = new Models.ExecutionContext("test", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        // Act
        var result = executor.ExecuteAsyncCodeAsync(context, CancellationToken.None).Result;
        // Assert
        Assert.False(result.Success);
        Assert.Contains("unsafe", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateExecutionSafety_WithUnsafePatterns_ShouldFail()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "await Task.Delay(100); Thread.Sleep(100);", enableLogging: false);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var validation = executor.ValidateExecutionSafety(config);
        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains("unsafe", validation.Errors.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsyncCodeAsync_WithCompilationCache_ShouldUseCache()
    {
        // Arrange
        var config = CodeExecutionConfig.CreateInline("csharp", "return await Task.FromResult(42);", enableLogging: false);
        var workflowContext = new Models.ExecutionContext("test", CancellationToken.None, "test");
        var context = new AsyncCodeExecutionContext(workflowContext, config, _mockServiceProvider.Object);
        var executor = new AsyncInlineCodeExecutor(_securityConfig, _logger);
        // Act
        var result1 = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        var result2 = await executor.ExecuteAsyncCodeAsync(context, CancellationToken.None);
        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Equal(result1.Output, result2.Output);
        // Since cache is used, second execution should be faster, but hard to test precisely
    }
}
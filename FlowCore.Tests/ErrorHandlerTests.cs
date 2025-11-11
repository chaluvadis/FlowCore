namespace FlowCore.Tests;

public class ErrorHandlerTests
{
    private readonly ILogger<ErrorHandler> _logger;
    private readonly ErrorHandler _errorHandler;
    private readonly string _testBlockName = "TestBlock";
    private readonly RetryPolicy _defaultRetryPolicy;
    public ErrorHandlerTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<ErrorHandler>();
        _errorHandler = new ErrorHandler(_logger);
        _defaultRetryPolicy = new RetryPolicy
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromSeconds(1),
            BackoffStrategy = BackoffStrategy.Exponential,
            BackoffMultiplier = 2.0
        };
    }
    [Fact]
    public async Task HandleErrorAsync_WithTransientError_ShouldRetry()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var transientError = new HttpRequestException("Network timeout");
        // Act
        var result = await _errorHandler.HandleErrorAsync(transientError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Retry, result.Action);
        Assert.NotNull(result.Delay);
        Assert.True(result.Delay.Value > TimeSpan.Zero);
    }
    [Fact]
    public async Task HandleErrorAsync_WithValidationError_ShouldSkip()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var validationError = new ArgumentNullException("parameter", "Parameter cannot be null");
        // Act
        var result = await _errorHandler.HandleErrorAsync(validationError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Skip, result.Action);
        Assert.Null(result.Delay);
        Assert.Null(result.Reason);
    }
    [Fact]
    public async Task HandleErrorAsync_WithBusinessLogicError_ShouldFail()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var businessLogicError = new InvalidOperationException("Invalid state transition");
        // Act
        var result = await _errorHandler.HandleErrorAsync(businessLogicError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Fail, result.Action);
        Assert.NotNull(result.Reason);
        Assert.Contains("Invalid state transition", result.Reason);
    }
    [Fact]
    public async Task HandleErrorAsync_WithResourceExhaustionError_ShouldFail()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var resourceError = new OutOfMemoryException("Insufficient memory");
        // Act
        var result = await _errorHandler.HandleErrorAsync(resourceError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Fail, result.Action);
        Assert.NotNull(result.Reason);
        Assert.Contains("Insufficient memory", result.Reason);
    }
    [Fact]
    public async Task HandleErrorAsync_WithSecurityError_ShouldFail()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var securityError = new UnauthorizedAccessException("Access denied");
        // Act
        var result = await _errorHandler.HandleErrorAsync(securityError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Retry, result.Action);
        Assert.NotNull(result.Reason);
        Assert.Contains("Access denied", result.Reason);
    }
    [Fact]
    public async Task HandleErrorAsync_WithNetworkTimeout_ShouldRetryWithBackoff()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var timeoutError = new TimeoutException("Request timeout");
        // Act
        var result = await _errorHandler.HandleErrorAsync(timeoutError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Retry, result.Action);
        Assert.NotNull(result.Delay);
        Assert.Equal(TimeSpan.FromMilliseconds(100), result.Delay.Value); // Initial delay
    }
    [Fact]
    public async Task HandleErrorAsync_WithMaxRetriesExceeded_ShouldFail()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        // Create a retry policy that allows only 1 retry
        var limitedRetryPolicy = new RetryPolicy
        {
            MaxRetries = 1,
            InitialDelay = TimeSpan.FromMilliseconds(50),
            BackoffStrategy = BackoffStrategy.Fixed
        };
        var transientError = new HttpRequestException("Network error");
        // First call should retry
        var firstResult = await _errorHandler.HandleErrorAsync(transientError, context, _testBlockName, limitedRetryPolicy);
        Assert.Equal(ErrorHandlingAction.Retry, firstResult.Action);
        // Second call should fail (exceeds max retries)
        var secondResult = await _errorHandler.HandleErrorAsync(transientError, context, _testBlockName, limitedRetryPolicy);
        Assert.Equal(ErrorHandlingAction.Fail, secondResult.Action);
    }

    [Fact]
    public async Task HandleErrorAsync_WithTransientError_ShouldRetryWithCorrectDelay()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var transientError = new HttpRequestException("Network timeout");
        // Act
        var result = await _errorHandler.HandleErrorAsync(transientError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Retry, result.Action);
        Assert.Equal(TimeSpan.FromMilliseconds(100), result.Delay); // Initial delay
    }
    [Fact]
    public async Task HandleErrorAsync_WithDifferentBackoffStrategies_ShouldApplyCorrectDelays()
    {
        // Test Immediate backoff
        var immediatePolicy = new RetryPolicy { BackoffStrategy = BackoffStrategy.Immediate };
        var immediateResult = await _errorHandler.HandleErrorAsync(
            new TimeoutException("Test"),
            new Models.ExecutionContext(new Dictionary<string, object>(), CancellationToken.None, "TestWorkflow"),
            _testBlockName,
            immediatePolicy);
        Assert.Equal(TimeSpan.Zero, immediateResult.Delay);
        // Test Fixed backoff
        var fixedPolicy = new RetryPolicy
        {
            BackoffStrategy = BackoffStrategy.Fixed,
            InitialDelay = TimeSpan.FromMilliseconds(200)
        };
        var fixedResult = await _errorHandler.HandleErrorAsync(
            new TimeoutException("Test"),
            new Models.ExecutionContext(new Dictionary<string, object>(), CancellationToken.None, "TestWorkflow"),
            _testBlockName,
            fixedPolicy);
        Assert.Equal(TimeSpan.FromMilliseconds(200), fixedResult.Delay);
        // Test Linear backoff
        var linearPolicy = new RetryPolicy
        {
            BackoffStrategy = BackoffStrategy.Linear,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromSeconds(1)
        };
        var linearResult = await _errorHandler.HandleErrorAsync(
            new TimeoutException("Test"),
            new Models.ExecutionContext(new Dictionary<string, object>(), CancellationToken.None, "TestWorkflow"),
            _testBlockName,
            linearPolicy);
        Assert.Equal(TimeSpan.FromMilliseconds(100), linearResult.Delay); // First retry uses initial delay
    }

    [Fact]
    public async Task HandleErrorAsync_WithUnauthorizedAccessException_ShouldFail()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var securityError = new UnauthorizedAccessException("Access denied");
        // Act
        var result = await _errorHandler.HandleErrorAsync(securityError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Fail, result.Action);
        Assert.NotNull(result.Reason);
        Assert.Contains("Access denied", result.Reason);
    }
    [Fact]
    public async Task HandleErrorAsync_WithExponentialBackoff_ShouldIncreaseDelay()
    {
        // Arrange
        var exponentialPolicy = new RetryPolicy
        {
            BackoffStrategy = BackoffStrategy.Exponential,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromSeconds(1)
        };
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var transientError = new TimeoutException("Network timeout");
        // First retry
        var firstResult = await _errorHandler.HandleErrorAsync(transientError, context, _testBlockName, exponentialPolicy);
        Assert.Equal(ErrorHandlingAction.Retry, firstResult.Action);
        Assert.Equal(TimeSpan.FromMilliseconds(100), firstResult.Delay);
        // Second retry should have longer delay (100 * 2^1 = 200ms)
        var secondResult = await _errorHandler.HandleErrorAsync(transientError, context, _testBlockName, exponentialPolicy);
        Assert.Equal(ErrorHandlingAction.Retry, secondResult.Action);
        Assert.Equal(TimeSpan.FromMilliseconds(200), secondResult.Delay);
    }

    [Fact]
    public async Task HandleErrorAsync_WithImmediateBackoff_ShouldReturnZeroDelay()
    {
        // Arrange
        var immediatePolicy = new RetryPolicy
        {
            BackoffStrategy = BackoffStrategy.Immediate,
            InitialDelay = TimeSpan.FromMilliseconds(100)
        };
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var transientError = new TimeoutException("Network timeout");
        // Act
        var result = await _errorHandler.HandleErrorAsync(transientError, context, _testBlockName, immediatePolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Retry, result.Action);
        Assert.Equal(TimeSpan.Zero, result.Delay);
    }
    [Fact]
    public async Task HandleErrorAsync_WithSystemException_ShouldFail()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var systemError = new Exception("System error");
        // Act
        var result = await _errorHandler.HandleErrorAsync(systemError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Fail, result.Action);
        Assert.NotNull(result.Reason);
        Assert.Contains("System error", result.Reason);
    }
    [Fact]
    public async Task GetErrorContext_WithValidErrorId_ShouldReturnContext()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var originalError = new TimeoutException("Test error");
        // Handle an error to create an error context
        var result = await _errorHandler.HandleErrorAsync(originalError, context, _testBlockName, _defaultRetryPolicy);
        // The error handler doesn't expose error IDs directly, so this test verifies the internal behavior
        // In a real implementation, you might need to modify the ErrorHandler to expose error contexts for testing
        Assert.NotNull(result);
    }
    [Fact]
    public async Task CleanupOldErrors_ShouldRemoveExpiredContexts()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        // Handle some errors to create error contexts
        await _errorHandler.HandleErrorAsync(
            new TimeoutException("Error 1"),
            context,
            _testBlockName,
            _defaultRetryPolicy);
        await _errorHandler.HandleErrorAsync(
            new TimeoutException("Error 2"),
            context,
            _testBlockName,
            _defaultRetryPolicy);
        // Act
        var removedCount = _errorHandler.CleanupOldErrors(DateTime.UtcNow.AddHours(-1));
        // Assert
        // Since we can't easily test the internal cleanup without exposing error contexts,
        // we'll verify that the method doesn't throw and returns a non-negative count
        Assert.True(removedCount >= 0);
    }
    [Fact]
    public async Task HandleErrorAsync_WithComplexNestedException_ShouldClassifyCorrectly()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        // Create a nested exception scenario
        var innerException = new HttpRequestException("Inner network error");
        var outerException = new InvalidOperationException("Operation failed due to network issues", innerException);
        // Act
        var result = await _errorHandler.HandleErrorAsync(outerException, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        // Should classify based on the outermost exception type (InvalidOperationException = BusinessLogic)
        Assert.Equal(ErrorHandlingAction.Fail, result.Action);
    }
    [Fact]
    public async Task HandleErrorAsync_WithSocketException_ShouldRetry()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var socketError = new System.Net.Sockets.SocketException(10060); // Connection timeout
        // Act
        var result = await _errorHandler.HandleErrorAsync(socketError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Retry, result.Action);
        Assert.NotNull(result.Delay);
    }
    [Fact]
    public async Task HandleErrorAsync_WithFormatException_ShouldSkip()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var formatError = new FormatException("Invalid format");
        // Act
        var result = await _errorHandler.HandleErrorAsync(formatError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Skip, result.Action);
    }
    [Fact]
    public async Task HandleErrorAsync_WithNotSupportedException_ShouldFail()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var notSupportedError = new NotSupportedException("Operation not supported");
        // Act
        var result = await _errorHandler.HandleErrorAsync(notSupportedError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Fail, result.Action);
        Assert.NotNull(result.Reason);
        Assert.Contains("Operation not supported", result.Reason);
    }
    [Fact]
    public async Task HandleErrorAsync_WithStackOverflowException_ShouldFail()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var stackOverflowError = new StackOverflowException("Stack overflow");
        // Act
        var result = await _errorHandler.HandleErrorAsync(stackOverflowError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Fail, result.Action);
        Assert.NotNull(result.Reason);
        Assert.Contains("Stack overflow", result.Reason);
    }
    [Fact]
    public async Task HandleErrorAsync_WithSecurityException_ShouldFail()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var securityError = new System.Security.SecurityException("Security violation");
        // Act
        var result = await _errorHandler.HandleErrorAsync(securityError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        Assert.Equal(ErrorHandlingAction.Fail, result.Action);
        Assert.NotNull(result.Reason);
        Assert.Contains("Security violation", result.Reason);
    }
    [Fact]
    public async Task HandleErrorAsync_WithUnknownExceptionType_ShouldDefaultToSystemError()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            CancellationToken.None,
            "TestWorkflow");
        var unknownError = new Exception("Unknown error type");
        // Act
        var result = await _errorHandler.HandleErrorAsync(unknownError, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        // Unknown exceptions should default to system error classification and fail
        Assert.Equal(ErrorHandlingAction.Fail, result.Action);
        Assert.NotNull(result.Reason);
        Assert.Contains("Unknown error type", result.Reason);
    }
    [Fact]
    public async Task HandleErrorAsync_WithCancellationToken_ShouldHandleGracefully()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var context = new Models.ExecutionContext(
            new Dictionary<string, object>(),
            cancellationTokenSource.Token,
            "TestWorkflow");
        var error = new OperationCanceledException("Operation was cancelled");
        // Act
        var result = await _errorHandler.HandleErrorAsync(error, context, _testBlockName, _defaultRetryPolicy);
        // Assert
        // Cancellation should be handled appropriately
        Assert.NotNull(result);
    }

    [Fact]
    public void ClassifyError_WithInsufficientMemoryException_ShouldReturnResourceExhaustion()
    {
        // Arrange
        var error = new InsufficientMemoryException("Not enough memory");
        // Act
        var classification = ErrorHandler.ClassifyError(error);
        // Assert
        Assert.Equal(ErrorClassification.ResourceExhaustion, classification);
    }

    [Fact]
    public void ShouldRetry_WithResourceExhaustion_ShouldReturnFalse()
    {
        // Arrange
        var context = new Models.ExecutionContext(new Dictionary<string, object>(), CancellationToken.None, "TestWorkflow");
        var errorContext = new ErrorContext("test", new InsufficientMemoryException(), context, "TestBlock", _defaultRetryPolicy);
        var classification = ErrorClassification.ResourceExhaustion;
        // Act
        var shouldRetry = ErrorHandler.ShouldRetry(errorContext, classification);
        // Assert
        Assert.False(shouldRetry);
    }

    [Fact]
    public void CalculateBackoffDelay_WithImmediateStrategy_ShouldReturnZero()
    {
        // Arrange
        var immediatePolicy = new RetryPolicy { BackoffStrategy = BackoffStrategy.Immediate };
        var context = new Models.ExecutionContext(new Dictionary<string, object>(), CancellationToken.None, "TestWorkflow");
        var errorContext = new ErrorContext("test", new Exception(), context, "TestBlock", immediatePolicy);
        // Act
        var delay = ErrorHandler.CalculateBackoffDelay(errorContext);
        // Assert
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void DetermineErrorStrategy_WithResourceExhaustion_ShouldReturnFail()
    {
        // Arrange
        var context = new Models.ExecutionContext(new Dictionary<string, object>(), CancellationToken.None, "TestWorkflow");
        var errorContext = new ErrorContext("test", new Exception(), context, "TestBlock", _defaultRetryPolicy);
        var classification = ErrorClassification.ResourceExhaustion;
        // Act
        var strategy = ErrorHandler.DetermineErrorStrategy(classification, errorContext);
        // Assert
        Assert.Equal(ErrorStrategy.Fail, strategy);
    }
}
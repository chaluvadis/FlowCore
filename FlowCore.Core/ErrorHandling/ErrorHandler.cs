namespace FlowCore.ErrorHandling;
/// <summary>
/// Comprehensive error handling and retry framework for workflow executions.
/// </summary>
public class ErrorHandler(ILogger<ErrorHandler>? logger = null)
{
    private readonly ConcurrentDictionary<string, ErrorContext> _errorContexts = new();

    /// <summary>
    /// Handles an error that occurred during workflow execution.
    /// </summary>
    /// <param name="error">The error that occurred.</param>
    /// <param name="context">The execution context when the error occurred.</param>
    /// <param name="blockName">The name of the block where the error occurred.</param>
    /// <param name="retryPolicy">The retry policy to apply.</param>
    /// <returns>An error handling result indicating how to proceed.</returns>
    public async Task<ErrorHandlingResult> HandleErrorAsync(
        Exception error,
        ExecutionContext context,
        string blockName,
        RetryPolicy retryPolicy)
    {
        var errorId = Guid.NewGuid().ToString();
        var errorContext = new ErrorContext(errorId, error, context, blockName, retryPolicy);
        _errorContexts[errorId] = errorContext;
        try
        {
            // Classify the error
            var errorClassification = ClassifyError(error);
            logger?.LogError(error, "Error in workflow block {BlockName}: {ErrorMessage}", blockName, error.Message);
            // Check if we should retry
            if (ShouldRetry(errorContext, errorClassification))
            {
                var retryResult = await ExecuteRetryAsync(errorContext);
                if (retryResult.ShouldRetry)
                {
                    return ErrorHandlingResult.Retry(retryResult.Delay);
                }
            }
            // Determine the appropriate error handling strategy
            var strategy = DetermineErrorStrategy(errorClassification, errorContext);
            return await ExecuteErrorStrategyAsync(strategy, errorContext);
        }
        catch (Exception handlingError)
        {
            logger?.LogError(handlingError, "Error occurred while handling workflow error");
            return ErrorHandlingResult.Fail("Error handling failed");
        }
    }
    /// <summary>
    /// Classifies an error into a specific category for appropriate handling.
    /// </summary>
    private static ErrorClassification ClassifyError(Exception error)
    {
        // Network-related errors
        if (error is HttpRequestException or TimeoutException or System.Net.Sockets.SocketException)
        {
            return ErrorClassification.Transient;
        }
        // Data validation errors
        if (error is ArgumentException or ArgumentNullException or ArgumentOutOfRangeException or FormatException)
        {
            return ErrorClassification.Validation;
        }
        // Business logic errors
        if (error is InvalidOperationException or NotSupportedException)
        {
            return ErrorClassification.BusinessLogic;
        }
        // Resource exhaustion errors
        if (error is OutOfMemoryException or InsufficientMemoryException or StackOverflowException)
        {
            return ErrorClassification.ResourceExhaustion;
        }
        // Security-related errors
        if (error is UnauthorizedAccessException or SecurityException)
        {
            return ErrorClassification.Security;
        }
        // Default to system error
        return ErrorClassification.System;
    }
    /// <summary>
    /// Determines if a retry should be attempted based on error context and classification.
    /// </summary>
    private static bool ShouldRetry(ErrorContext errorContext, ErrorClassification classification)
    {
        // Don't retry validation or business logic errors
        if (classification == ErrorClassification.Validation || classification == ErrorClassification.BusinessLogic)
        {
            return false;
        }
        // Check retry count against policy
        return errorContext.RetryCount < errorContext.RetryPolicy.MaxRetries;
    }
    /// <summary>
    /// Executes a retry with appropriate backoff delay.
    /// </summary>
    private async Task<RetryResult> ExecuteRetryAsync(ErrorContext errorContext)
    {
        var delay = CalculateBackoffDelay(errorContext);
        logger?.LogInformation(
            "Retrying workflow block {BlockName}, attempt {Attempt}/{MaxAttempts} after {Delay}ms",
            errorContext.BlockName,
            errorContext.RetryCount + 1,
            errorContext.RetryPolicy.MaxRetries,
            delay.TotalMilliseconds);
        // Update retry count
        errorContext.IncrementRetryCount();
        return new RetryResult(true, delay);
    }
    /// <summary>
    /// Calculates the backoff delay for retry attempts.
    /// </summary>
    private TimeSpan CalculateBackoffDelay(ErrorContext errorContext)
    {
        var baseDelay = errorContext.RetryPolicy.InitialDelay;
        var maxDelay = errorContext.RetryPolicy.MaxDelay;
        var multiplier = errorContext.RetryPolicy.BackoffMultiplier;
        var attempt = errorContext.RetryCount;
        return errorContext.RetryPolicy.BackoffStrategy switch
        {
            BackoffStrategy.Immediate => TimeSpan.Zero,
            BackoffStrategy.Fixed => baseDelay,
            BackoffStrategy.Linear => TimeSpan.FromTicks(Math.Min(baseDelay.Ticks * attempt, maxDelay.Ticks)),
            BackoffStrategy.Exponential => TimeSpan.FromTicks(Math.Min(
                (long)(baseDelay.Ticks * Math.Pow(multiplier, attempt - 1)),
                maxDelay.Ticks)),
            _ => baseDelay
        };
    }
    /// <summary>
    /// Determines the appropriate error handling strategy.
    /// </summary>
    private ErrorStrategy DetermineErrorStrategy(ErrorClassification classification, ErrorContext errorContext) => classification switch
    {
        ErrorClassification.Transient => ErrorStrategy.Retry,
        ErrorClassification.Validation => ErrorStrategy.Skip,
        ErrorClassification.BusinessLogic => ErrorStrategy.Fail,
        ErrorClassification.ResourceExhaustion => ErrorStrategy.Fail,
        ErrorClassification.Security => ErrorStrategy.Fail,
        ErrorClassification.System => ErrorStrategy.Fail,
        _ => ErrorStrategy.Fail
    };
    /// <summary>
    /// Executes the specified error handling strategy.
    /// </summary>
    private async Task<ErrorHandlingResult> ExecuteErrorStrategyAsync(ErrorStrategy strategy, ErrorContext errorContext) => strategy switch
    {
        ErrorStrategy.Retry => ErrorHandlingResult.Retry(TimeSpan.FromSeconds(1)),
        ErrorStrategy.Skip => ErrorHandlingResult.Skip(),
        ErrorStrategy.Fail => ErrorHandlingResult.Fail(errorContext.Error.Message),
        _ => ErrorHandlingResult.Fail("Unknown error strategy")
    };
    /// <summary>
    /// Gets the error context for a specific error ID.
    /// </summary>
    /// <param name="errorId">The error identifier.</param>
    /// <returns>The error context, or null if not found.</returns>
    public ErrorContext? GetErrorContext(string errorId)
    {
        _errorContexts.TryGetValue(errorId, out var context);
        return context;
    }
    /// <summary>
    /// Cleans up old error contexts.
    /// </summary>
    /// <param name="olderThan">Remove error contexts older than this timestamp.</param>
    /// <returns>The number of error contexts removed.</returns>
    public int CleanupOldErrors(DateTime olderThan)
    {
        var keysToRemove = _errorContexts
            .Where(kvp => kvp.Value.OccurredAt < olderThan)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in keysToRemove)
        {
            _errorContexts.TryRemove(key, out _);
        }
        return keysToRemove.Count;
    }
}
/// <summary>
/// Classification of error types for appropriate handling.
/// </summary>
public enum ErrorClassification
{
    /// <summary>
    /// Transient errors that may resolve themselves (network, temporary unavailability).
    /// </summary>
    Transient,
    /// <summary>
    /// Validation errors (invalid input, format errors).
    /// </summary>
    Validation,
    /// <summary>
    /// Business logic errors (invalid state, rule violations).
    /// </summary>
    BusinessLogic,
    /// <summary>
    /// Resource exhaustion errors (memory, disk space).
    /// </summary>
    ResourceExhaustion,
    /// <summary>
    /// Security-related errors (access denied, authentication failures).
    /// </summary>
    Security,
    /// <summary>
    /// System-level errors (unexpected exceptions).
    /// </summary>
    System
}
/// <summary>
/// Error handling strategies.
/// </summary>
public enum ErrorStrategy
{
    /// <summary>
    /// Retry the operation.
    /// </summary>
    Retry,
    /// <summary>
    /// Skip the operation and continue.
    /// </summary>
    Skip,
    /// <summary>
    /// Fail the operation and stop execution.
    /// </summary>
    Fail
}
/// <summary>
/// Context information about an error occurrence.
/// </summary>
public class ErrorContext(
    string errorId,
    Exception error,
    ExecutionContext executionContext,
    string blockName,
    RetryPolicy retryPolicy)
{
    public string ErrorId { get; } = errorId;
    public Exception Error { get; } = error;
    public ExecutionContext ExecutionContext { get; } = executionContext;
    public string BlockName { get; } = blockName;
    public RetryPolicy RetryPolicy { get; } = retryPolicy;
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    private volatile int _retryCount = 0;
    public int RetryCount => _retryCount;
    public void IncrementRetryCount() => Interlocked.Increment(ref _retryCount);
}
/// <summary>
/// Result of a retry operation.
/// </summary>
public class RetryResult(bool shouldRetry, TimeSpan delay)
{
    public bool ShouldRetry { get; } = shouldRetry;
    public TimeSpan Delay { get; } = delay;
}
/// <summary>
/// Result of error handling.
/// </summary>
public class ErrorHandlingResult
{
    public ErrorHandlingAction Action { get; }
    public TimeSpan? Delay { get; }
    public string? Reason { get; }
    private ErrorHandlingResult(ErrorHandlingAction action, TimeSpan? delay = null, string? reason = null)
    {
        Action = action;
        Delay = delay;
        Reason = reason;
    }
    public static ErrorHandlingResult Retry(TimeSpan delay) => new(ErrorHandlingAction.Retry, delay);
    public static ErrorHandlingResult Skip() => new(ErrorHandlingAction.Skip);
    public static ErrorHandlingResult Fail(string reason) => new(ErrorHandlingAction.Fail, reason: reason);
}
/// <summary>
/// Actions that can be taken as a result of error handling.
/// </summary>
public enum ErrorHandlingAction
{
    /// <summary>
    /// Retry the operation.
    /// </summary>
    Retry,
    /// <summary>
    /// Skip the operation and continue.
    /// </summary>
    Skip,
    /// <summary>
    /// Fail the operation and stop execution.
    /// </summary>
    Fail
}
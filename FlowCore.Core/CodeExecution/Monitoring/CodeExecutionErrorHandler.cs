namespace FlowCore.CodeExecution.Monitoring;

/// <summary>
/// Comprehensive error handler for code execution failures.
/// Provides detailed error analysis, recovery strategies, and performance monitoring.
/// </summary>
/// <remarks>
/// Initializes a new instance of the CodeExecutionErrorHandler.
/// </remarks>
/// <param name="logger">Optional logger for error handling operations.</param>
public class CodeExecutionErrorHandler(ILogger? logger = null)
{
    private readonly ConcurrentDictionary<string, ErrorStatistics> _errorStatistics = new();
    private readonly ConcurrentQueue<CodeExecutionError> _recentErrors = new();
    private readonly int _maxRecentErrors = 1000;

    /// <summary>
    /// Handles a code execution error with comprehensive analysis and recovery suggestions.
    /// </summary>
    /// <param name="error">The exception that occurred during code execution.</param>
    /// <param name="context">The execution context when the error occurred.</param>
    /// <param name="blockName">The name of the block where the error occurred.</param>
    /// <param name="retryPolicy">The retry policy to apply for error recovery.</param>
    /// <returns>An error handling result with recovery recommendations.</returns>
    public async Task<ErrorHandlingResult> HandleErrorAsync(
        Exception error,
        ExecutionContext context,
        string blockName,
        RetryPolicy retryPolicy)
    {
        var errorId = Guid.NewGuid().ToString();
        var errorStartTime = DateTime.UtcNow;

        try
        {
            logger?.LogError(error, "Code execution error in block {BlockName}", blockName);

            // Create detailed error record
            var executionError = new CodeExecutionError
            {
                ErrorId = errorId,
                Timestamp = errorStartTime,
                BlockName = blockName,
                ExceptionType = error.GetType().Name,
                Message = error.Message,
                StackTrace = error.StackTrace ?? "No stack trace available",
                ExecutionContext = new Dictionary<string, object>
                {
                    ["CurrentBlockName"] = context.CurrentBlockName ?? "Unknown",
                    ["WorkflowName"] = context.WorkflowName ?? "Unknown",
                    ["ExecutionId"] = context.ExecutionId,
                    ["StateCount"] = context.State.Count
                }
            };

            // Add to recent errors queue
            _recentErrors.Enqueue(executionError);
            while (_recentErrors.Count > _maxRecentErrors)
            {
                _recentErrors.TryDequeue(out _);
            }

            // Analyze the error
            var errorAnalysis = AnalyzeError(error, context, blockName);

            // Determine recovery strategy
            var recoveryStrategy = DetermineRecoveryStrategy(error, errorAnalysis, retryPolicy);

            // Update error statistics
            UpdateErrorStatistics(blockName, error.GetType().Name, recoveryStrategy.Action);

            var handlingTime = DateTime.UtcNow - errorStartTime;

            logger?.LogInformation("Error handling completed for block {BlockName} in {HandlingTime}. Action: {Action}",
                blockName, handlingTime, recoveryStrategy.Action);

            return new ErrorHandlingResult
            {
                ErrorId = errorId,
                Action = recoveryStrategy.Action,
                ShouldRetry = recoveryStrategy.ShouldRetry,
                RetryDelay = recoveryStrategy.RetryDelay,
                AlternativeBlockName = recoveryStrategy.AlternativeBlockName,
                ErrorAnalysis = errorAnalysis,
                RecoveryStrategy = recoveryStrategy,
                HandlingTime = handlingTime
            };
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error in error handler for block {BlockName}", blockName);

            // Return a safe default result
            return new ErrorHandlingResult
            {
                ErrorId = errorId,
                Action = ErrorHandlingAction.Fail,
                ShouldRetry = false,
                ErrorAnalysis = new ErrorAnalysis
                {
                    ErrorType = "ErrorHandlerFailure",
                    Severity = ErrorSeverity.Critical,
                    IsRecoverable = false,
                    Description = "Error handler itself failed"
                }
            };
        }
    }

    /// <summary>
    /// Analyzes an error to determine its type, severity, and recoverability.
    /// </summary>
    /// <param name="error">The exception to analyze.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="blockName">The name of the block where the error occurred.</param>
    /// <returns>Detailed error analysis.</returns>
    public ErrorAnalysis AnalyzeError(Exception error, ExecutionContext context, string blockName)
    {
        var analysis = new ErrorAnalysis
        {
            ErrorType = CategorizeError(error),
            Severity = DetermineErrorSeverity(error),
            IsRecoverable = DetermineIfRecoverable(error),
            Description = GenerateErrorDescription(error),
            SuggestedActions = GenerateSuggestedActions(error),
            Context = ExtractErrorContext(error, context, blockName)
        };

        logger?.LogDebug("Error analysis for block {BlockName}: Type={ErrorType}, Severity={Severity}, Recoverable={IsRecoverable}",
            blockName, analysis.ErrorType, analysis.Severity, analysis.IsRecoverable);

        return analysis;
    }

    /// <summary>
    /// Gets error statistics for monitoring and analysis.
    /// </summary>
    /// <param name="blockName">Optional block name to filter statistics.</param>
    /// <returns>Error statistics for the specified scope.</returns>
    public ErrorStatisticsReport GetErrorStatistics(string? blockName = null)
    {
        if (blockName != null && _errorStatistics.TryGetValue(blockName, out var blockStats))
        {
            return new ErrorStatisticsReport
            {
                Scope = $"Block: {blockName}",
                TotalErrors = blockStats.TotalErrors,
                ErrorsByType = blockStats.ErrorsByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                RecoveryActions = blockStats.RecoveryActions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                RecentErrors = [.. _recentErrors
                    .Where(e => e.BlockName == blockName)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(100)]
            };
        }

        // Aggregate statistics across all blocks
        var totalErrors = _errorStatistics.Sum(s => s.Value.TotalErrors);
        var allErrorsByType = new Dictionary<string, int>();
        var allRecoveryActions = new Dictionary<string, int>();

        foreach (var stats in _errorStatistics.Values)
        {
            foreach (var kvp in stats.ErrorsByType)
            {
                allErrorsByType[kvp.Key] = allErrorsByType.GetValueOrDefault(kvp.Key) + kvp.Value;
            }

            foreach (var kvp in stats.RecoveryActions)
            {
                allRecoveryActions[kvp.Key] = allRecoveryActions.GetValueOrDefault(kvp.Key) + kvp.Value;
            }
        }

        return new ErrorStatisticsReport
        {
            Scope = "All Blocks",
            TotalErrors = totalErrors,
            ErrorsByType = allErrorsByType,
            RecoveryActions = allRecoveryActions,
            RecentErrors = [.. _recentErrors
                .OrderByDescending(e => e.Timestamp)
                .Take(100)]
        };
    }

    private static string CategorizeError(Exception error)
    {
        var errorType = error.GetType().Name.ToLowerInvariant();
        var message = error.Message.ToLowerInvariant();

        // Categorize based on exception type and message content
        if (errorType.Contains("security") || message.Contains("security") || message.Contains("permission"))
        {
            return "SecurityError";
        }

        if (errorType.Contains("timeout") || message.Contains("timeout"))
        {
            return "TimeoutError";
        }

        if (errorType.Contains("argument") || message.Contains("argument"))
        {
            return "ArgumentError";
        }

        if (errorType.Contains("null") || message.Contains("null"))
        {
            return "NullReferenceError";
        }

        if (errorType.Contains("format") || message.Contains("format"))
        {
            return "FormatError";
        }

        if (errorType.Contains("io") || message.Contains("file") || message.Contains("directory"))
        {
            return "IOError";
        }

        if (errorType.Contains("network") || message.Contains("network") || message.Contains("http"))
        {
            return "NetworkError";
        }

        if (errorType.Contains("reflection") || message.Contains("reflection"))
        {
            return "ReflectionError";
        }

        return "GeneralError";
    }

    private static ErrorSeverity DetermineErrorSeverity(Exception error)
    {
        var errorType = error.GetType().Name.ToLowerInvariant();
        var message = error.Message.ToLowerInvariant();

        // Critical errors that should stop execution
        if (errorType.Contains("security") || errorType.Contains("unauthorized") || errorType.Contains("forbidden"))
        {
            return ErrorSeverity.Critical;
        }

        // High severity errors that indicate serious issues
        if (errorType.Contains("outofmemory") || errorType.Contains("stackoverflow") || errorType.Contains("threadabort"))
        {
            return ErrorSeverity.High;
        }

        // Medium severity errors that affect functionality
        if (errorType.Contains("io") || errorType.Contains("network") || errorType.Contains("timeout"))
        {
            return ErrorSeverity.Medium;
        }

        // Low severity errors that are usually recoverable
        if (errorType.Contains("argument") || errorType.Contains("format") || errorType.Contains("null"))
        {
            return ErrorSeverity.Low;
        }

        return ErrorSeverity.Medium; // Default to medium
    }

    private static bool DetermineIfRecoverable(Exception error)
    {
        var errorType = error.GetType().Name.ToLowerInvariant();

        // Generally recoverable errors
        var recoverableErrors = new[]
        {
            "argumentexception", "argumentnullexception", "formatException",
            "ioexception", "timeoutexception", "webexception"
        };

        // Generally not recoverable errors
        var nonRecoverableErrors = new[]
        {
            "securityexception", "unauthorizedaccessexception", "outofmemoryexception",
            "stackoverflowexception", "threadabortexception", "accessviolationexception"
        };

        if (recoverableErrors.Any(errorType.Contains))
        {
            return true;
        }

        if (nonRecoverableErrors.Any(errorType.Contains))
        {
            return false;
        }

        // Default to recoverable for unknown errors
        return true;
    }

    private static string GenerateErrorDescription(Exception error)
    {
        var errorType = error.GetType().Name;
        var message = error.Message;

        // Generate user-friendly descriptions for common errors
        if (error is ArgumentException)
        {
            return $"Invalid argument provided: {message}";
        }

        if (error is ArgumentNullException)
        {
            return $"Required argument was null: {message}";
        }

        if (error is FormatException)
        {
            return $"Data format error: {message}";
        }

        if (error is TimeoutException)
        {
            return $"Operation timed out: {message}";
        }

        if (error is SecurityException)
        {
            return $"Security violation: {message}";
        }

        if (error is OutOfMemoryException)
        {
            return $"Insufficient memory: {message}";
        }

        return $"{errorType}: {message}";
    }

    private static List<string> GenerateSuggestedActions(Exception error)
    {
        var actions = new List<string>();
        var errorType = error.GetType().Name.ToLowerInvariant();

        // Generate context-specific suggestions
        if (errorType.Contains("argument"))
        {
            actions.Add("Check that all required parameters are provided");
            actions.Add("Verify parameter types match expected types");
            actions.Add("Ensure non-null values for required parameters");
        }

        if (errorType.Contains("format"))
        {
            actions.Add("Verify data format matches expected structure");
            actions.Add("Check for invalid characters or encoding issues");
        }

        if (errorType.Contains("io"))
        {
            actions.Add("Verify file paths and permissions");
            actions.Add("Check available disk space");
            actions.Add("Ensure files are not locked by other processes");
        }

        if (errorType.Contains("network"))
        {
            actions.Add("Check network connectivity");
            actions.Add("Verify endpoint URLs and ports");
            actions.Add("Check firewall and security settings");
        }

        if (errorType.Contains("timeout"))
        {
            actions.Add("Increase timeout values if operation is expected to take longer");
            actions.Add("Check system performance and resource usage");
            actions.Add("Consider breaking large operations into smaller chunks");
        }

        if (errorType.Contains("security"))
        {
            actions.Add("Review security configuration and permissions");
            actions.Add("Check user authentication and authorization");
            actions.Add("Verify code is not attempting restricted operations");
        }

        if (errorType.Contains("null"))
        {
            actions.Add("Add null checks before using objects");
            actions.Add("Ensure required dependencies are initialized");
            actions.Add("Check for missing state data");
        }

        if (!actions.Any())
        {
            actions.Add("Review error logs for additional context");
            actions.Add("Check system resources and performance");
            actions.Add("Consider simplifying the operation or breaking it into smaller steps");
        }

        return actions;
    }

    private static Dictionary<string, object> ExtractErrorContext(Exception error, ExecutionContext context, string blockName) => new Dictionary<string, object>
    {
        ["BlockName"] = blockName,
        ["CurrentBlockName"] = context.CurrentBlockName ?? "Unknown",
        ["WorkflowName"] = context.WorkflowName ?? "Unknown",
        ["ExecutionId"] = context.ExecutionId,
        ["StateKeys"] = context.State.Keys.ToList(),
        ["HasInput"] = context.Input != null,
        ["ErrorType"] = error.GetType().Name,
        ["InnerException"] = error.InnerException?.GetType().Name ?? "None"
    };

    private static RecoveryStrategy DetermineRecoveryStrategy(Exception error, ErrorAnalysis analysis, RetryPolicy retryPolicy)
    {
        // Don't retry critical errors
        if (analysis.Severity == ErrorSeverity.Critical)
        {
            return new RecoveryStrategy
            {
                Action = ErrorHandlingAction.Fail,
                ShouldRetry = false,
                Description = "Critical error - execution cannot continue"
            };
        }

        // Don't retry security violations
        if (analysis.ErrorType == "SecurityError")
        {
            return new RecoveryStrategy
            {
                Action = ErrorHandlingAction.Fail,
                ShouldRetry = false,
                Description = "Security violation - execution blocked for security"
            };
        }

        // For recoverable errors, determine retry strategy
        if (analysis.IsRecoverable)
        {
            var shouldRetry = ShouldRetryError(error, retryPolicy);
            if (shouldRetry)
            {
                var retryDelay = CalculateRetryDelay(error, retryPolicy);
                return new RecoveryStrategy
                {
                    Action = ErrorHandlingAction.Retry,
                    ShouldRetry = true,
                    RetryDelay = retryDelay,
                    Description = $"Retry after {retryDelay.TotalSeconds} seconds"
                };
            }
            else
            {
                return new RecoveryStrategy
                {
                    Action = ErrorHandlingAction.Skip,
                    ShouldRetry = false,
                    AlternativeBlockName = "error-handler",
                    Description = "Skip failed block and continue with error handling"
                };
            }
        }

        // Default to failure for non-recoverable errors
        return new RecoveryStrategy
        {
            Action = ErrorHandlingAction.Fail,
            ShouldRetry = false,
            Description = "Non-recoverable error - execution must stop"
        };
    }

    private static bool ShouldRetryError(Exception error, RetryPolicy retryPolicy)
    {
        var errorType = error.GetType().Name.ToLowerInvariant();

        // Never retry these error types
        var nonRetryableErrors = new[]
        {
            "securityexception", "unauthorizedaccessexception", "accessviolationexception"
        };

        if (nonRetryableErrors.Any(errorType.Contains))
        {
            return false;
        }

        // For other errors, check retry policy
        return true; // Simplified - in real implementation, check retry count, etc.
    }

    private static TimeSpan CalculateRetryDelay(Exception error, RetryPolicy retryPolicy) =>
        // Simple exponential backoff
        TimeSpan.FromSeconds(Math.Min(retryPolicy.InitialDelay.TotalSeconds * Math.Pow(retryPolicy.BackoffMultiplier, 1), retryPolicy.MaxDelay.TotalSeconds));

    private void UpdateErrorStatistics(string blockName, string errorType, ErrorHandlingAction action)
    {
        var stats = _errorStatistics.GetOrAdd(blockName, _ => new ErrorStatistics());

        stats.TotalErrors++;
        stats.ErrorsByType[errorType] = stats.ErrorsByType.GetValueOrDefault(errorType) + 1;
        stats.RecoveryActions[action.ToString()] = stats.RecoveryActions.GetValueOrDefault(action.ToString()) + 1;
    }
}

/// <summary>
/// Result of error handling with recovery recommendations.
/// </summary>
public class ErrorHandlingResult
{
    public string ErrorId { get; set; } = string.Empty;
    public ErrorHandlingAction Action { get; set; }
    public bool ShouldRetry { get; set; }
    public TimeSpan RetryDelay { get; set; }
    public string? AlternativeBlockName { get; set; }
    public ErrorAnalysis ErrorAnalysis { get; set; } = new ErrorAnalysis();
    public RecoveryStrategy RecoveryStrategy { get; set; } = new RecoveryStrategy();
    public TimeSpan HandlingTime { get; set; }
}

/// <summary>
/// Analysis of an error including type, severity, and recovery options.
/// </summary>
public class ErrorAnalysis
{
    public string ErrorType { get; set; } = string.Empty;
    public ErrorSeverity Severity { get; set; }
    public bool IsRecoverable { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> SuggestedActions { get; set; } = [];
    public Dictionary<string, object> Context { get; set; } = [];
}

/// <summary>
/// Strategy for recovering from an error.
/// </summary>
public class RecoveryStrategy
{
    public ErrorHandlingAction Action { get; set; }
    public bool ShouldRetry { get; set; }
    public TimeSpan RetryDelay { get; set; }
    public string? AlternativeBlockName { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Statistics about errors for a specific scope.
/// </summary>
public class ErrorStatistics
{
    public int TotalErrors { get; set; }
    public Dictionary<string, int> ErrorsByType { get; set; } = [];
    public Dictionary<string, int> RecoveryActions { get; set; } = [];
}

/// <summary>
/// Report containing error statistics and analysis.
/// </summary>
public class ErrorStatisticsReport
{
    public string Scope { get; set; } = string.Empty;
    public int TotalErrors { get; set; }
    public Dictionary<string, int> ErrorsByType { get; set; } = [];
    public Dictionary<string, int> RecoveryActions { get; set; } = [];
    public IReadOnlyList<CodeExecutionError> RecentErrors { get; set; } = [];
}

/// <summary>
/// Record of a code execution error.
/// </summary>
public class CodeExecutionError
{
    public string ErrorId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string BlockName { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public Dictionary<string, object> ExecutionContext { get; set; } = [];
}

/// <summary>
/// Severity levels for errors.
/// </summary>
public enum ErrorSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Actions that can be taken in response to an error.
/// </summary>
public enum ErrorHandlingAction
{
    Retry,
    Skip,
    Fail,
    Redirect
}

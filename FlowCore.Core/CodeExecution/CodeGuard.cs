namespace FlowCore.CodeExecution;

/// <summary>
/// Guard implementation that uses configurable code for conditional logic.
/// Allows users to define custom guard conditions using inline C# code or pre-compiled assemblies.
/// </summary>
/// <remarks>
/// Initializes a new instance of the CodeGuard.
/// </remarks>
/// <param name="guardId">The unique identifier for this guard.</param>
/// <param name="displayName">The display name for this guard.</param>
/// <param name="description">The description of what this guard validates.</param>
/// <param name="executor">The code executor to use for evaluation.</param>
/// <param name="config">The code execution configuration.</param>
/// <param name="serviceProvider">The service provider for dependency injection.</param>
/// <param name="severity">The severity level of this guard.</param>
/// <param name="category">The category of this guard.</param>
/// <param name="failureBlockName">Optional block to transition to on failure.</param>
public class CodeGuard(
    string guardId,
    string displayName,
    string description,
    ICodeExecutor executor,
    CodeExecutionConfig config,
    IServiceProvider serviceProvider,
    GuardSeverity severity = GuardSeverity.Error,
    string category = "Custom",
    string? failureBlockName = null) : IGuard
{
    private readonly ICodeExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    private readonly CodeExecutionConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Gets the unique identifier for this guard.
    /// </summary>
    public string GuardId { get; } = guardId ?? throw new ArgumentNullException(nameof(guardId));

    /// <summary>
    /// Gets the display name for this guard.
    /// </summary>
    public string DisplayName { get; } = displayName ?? throw new ArgumentNullException(nameof(displayName));

    /// <summary>
    /// Gets the description of what this guard validates.
    /// </summary>
    public string Description { get; } = description ?? throw new ArgumentNullException(nameof(description));

    /// <summary>
    /// Gets the severity level of this guard.
    /// </summary>
    public GuardSeverity Severity { get; } = severity;

    /// <summary>
    /// Gets the category of this guard for organizational purposes.
    /// </summary>
    public string Category { get; } = category;

    /// <summary>
    /// Gets the name of the block to transition to if this guard fails.
    /// </summary>
    public string? FailureBlockName { get; } = failureBlockName;

    /// <summary>
    /// Evaluates the guard condition against the provided context.
    /// </summary>
    /// <param name="context">The execution context to evaluate against.</param>
    /// <returns>A guard result indicating whether the condition passed or failed.</returns>
    public async Task<GuardResult> EvaluateAsync(ExecutionContext context)
    {
        var evaluationStartTime = DateTime.UtcNow;

        try
        {
            // Validate that the executor can handle this configuration
            if (!_executor.CanExecute(_config))
            {
                var error = $"Executor {_executor.ExecutorType} cannot handle guard configuration mode {_config.Mode}";
                return GuardResult.Failure(
                    error,
                    FailureBlockName,
                    Severity);
            }

            // Create code execution context with access to workflow state
            var codeContext = new CodeExecutionContext(context, _config, _serviceProvider);

            // Execute the guard code
            var executionResult = await _executor.ExecuteAsync(codeContext, context.CancellationToken).ConfigureAwait(false);

            var evaluationTime = DateTime.UtcNow - evaluationStartTime;

            // Interpret the execution result as a boolean guard result
            var guardPassed = InterpretExecutionResult(executionResult);

            if (guardPassed)
            {
                return GuardResult.Success(new Dictionary<string, object>
                {
                    ["EvaluationTime"] = evaluationTime,
                    ["ExecutionMode"] = _config.Mode.ToString(),
                    ["Language"] = _config.Language
                });
            }
            else
            {
                var errorMessage = executionResult.ErrorMessage ?? "Guard condition evaluated to false";
                return GuardResult.Failure(
                    errorMessage,
                    FailureBlockName,
                    Severity,
                    new Dictionary<string, object>
                    {
                        ["EvaluationTime"] = evaluationTime,
                        ["ExecutionMode"] = _config.Mode.ToString(),
                        ["Language"] = _config.Language,
                        ["ExecutionOutput"] = executionResult.Output ?? "No output"
                    });
            }
        }
        catch (OperationCanceledException)
        {
            return GuardResult.Failure(
                "Guard evaluation was cancelled",
                FailureBlockName,
                GuardSeverity.Warning);
        }
        catch (Exception ex)
        {
            var evaluationTime = DateTime.UtcNow - evaluationStartTime;
            return GuardResult.Failure(
                $"Unexpected error during guard evaluation: {ex.Message}",
                FailureBlockName,
                Severity,
                new Dictionary<string, object>
                {
                    ["EvaluationTime"] = evaluationTime,
                    ["ExceptionType"] = ex.GetType().Name,
                    ["StackTrace"] = ex.StackTrace ?? "No stack trace"
                });
        }
    }

    /// <summary>
    /// Creates a new CodeGuard instance from configuration.
    /// </summary>
    /// <param name="guardId">The unique identifier for the guard.</param>
    /// <param name="displayName">The display name for the guard.</param>
    /// <param name="description">The description of what the guard validates.</param>
    /// <param name="config">The code execution configuration.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="severity">The severity level of the guard.</param>
    /// <param name="category">The category of the guard.</param>
    /// <param name="failureBlockName">Optional block to transition to on failure.</param>
    /// <param name="logger">Optional logger for the guard.</param>
    /// <returns>A new CodeGuard instance.</returns>
    public static CodeGuard Create(
        string guardId,
        string displayName,
        string description,
        CodeExecutionConfig config,
        IServiceProvider serviceProvider,
        GuardSeverity severity = GuardSeverity.Error,
        string category = "Custom",
        string? failureBlockName = null,
        ILogger? logger = null)
    {
        // Resolve the appropriate executor based on the configuration mode
        var executor = ResolveExecutor(config, serviceProvider, logger);

        return new CodeGuard(
            guardId,
            displayName,
            description,
            executor,
            config,
            serviceProvider,
            severity,
            category,
            failureBlockName);
    }

    private static ICodeExecutor ResolveExecutor(CodeExecutionConfig config, IServiceProvider serviceProvider, ILogger? logger) => config.Mode switch
    {
        CodeExecutionMode.Inline => new InlineCodeExecutor(
            CodeSecurityConfig.Create(config.AllowedNamespaces, config.AllowedTypes, config.BlockedNamespaces),
            logger),

        CodeExecutionMode.Assembly => new AssemblyCodeExecutor(
            CodeSecurityConfig.Create(config.AllowedNamespaces, config.AllowedTypes, config.BlockedNamespaces),
            logger),

        _ => throw new NotSupportedException($"Code execution mode {config.Mode} is not supported for guards")
    };

    private static bool InterpretExecutionResult(CodeExecutionResult executionResult)
    {
        if (!executionResult.Success)
        {
            return false; // Execution failed, so guard fails
        }

        // Try to interpret the output as a boolean
        if (executionResult.Output == null)
        {
            return true; // No output typically means success for guards
        }

        try
        {
            // If output is a boolean, use it directly
            if (executionResult.Output is bool boolResult)
            {
                return boolResult;
            }

            // If output is a string, try to parse it as boolean
            if (executionResult.Output is string stringResult)
            {
                return bool.TryParse(stringResult, out var parsed) && parsed;
            }

            // If output is a number, non-zero means true
            if (executionResult.Output is int intResult)
            {
                return intResult != 0;
            }

            if (executionResult.Output is long longResult)
            {
                return longResult != 0;
            }

            if (executionResult.Output is double doubleResult)
            {
                return Math.Abs(doubleResult) > double.Epsilon;
            }

            if (executionResult.Output is decimal decimalResult)
            {
                return decimalResult != 0;
            }

            // For other types, existence of output means true
            return true;
        }
        catch
        {
            // If we can't interpret the result, default to false (strict)
            return false;
        }
    }
}

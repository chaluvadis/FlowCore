namespace FlowCore.CodeExecution.Executors;

/// <summary>
/// Executes C# code strings using Roslyn compilation.
/// Provides secure execution with namespace and type restrictions.
/// </summary>
/// <remarks>
/// Initializes a new instance of the InlineCodeExecutor.
/// </remarks>
/// <param name="securityConfig">The security configuration for code validation.</param>
/// <param name="logger">Optional logger for execution operations.</param>
public class InlineCodeExecutor(CodeSecurityConfig securityConfig, ILogger? logger = null) : BaseInlineCodeExecutor(securityConfig)
{
    private readonly ILogger? _logger = logger;
    private static readonly ConcurrentDictionary<string, (Delegate del, DateTime timestamp)> _executionCache = new();

    /// <summary>
    /// Gets the unique identifier for this executor type.
    /// </summary>
    public override string ExecutorType => "InlineCodeExecutor";

    /// <summary>
    /// Executes the configured code with the provided execution context.
    /// </summary>
    /// <param name="context">The execution context containing workflow state and configuration.</param>
    /// <param name="ct">Token that can be used to cancel the code execution.</param>
    /// <returns>A task representing the code execution result with success status, output data, and any errors.</returns>
    public override async Task<CodeExecutionResult> ExecuteAsync(
        CodeExecutionContext context,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger?.LogDebug("Starting inline code execution");

            // Validate the code before execution
            var codeToExecute = context.GetInput<string>();
            var validationResult = ValidateCode(codeToExecute);
            if (!validationResult.IsValid)
            {
                return CodeExecutionResult.CreateFailure(
                    $"Code validation failed: {string.Join(", ", validationResult.Errors)}",
                    executionTime: DateTime.UtcNow - startTime);
            }

            // Get the code to execute
            var code = codeToExecute;
            if (string.IsNullOrEmpty(code))
            {
                return CodeExecutionResult.CreateFailure(
                    "No code provided for execution",
                    executionTime: DateTime.UtcNow - startTime);
            }

            // Execute the code using simplified execution model
            var executionResult = await ExecuteCodeAsync(code, context, ct).ConfigureAwait(false);
            var executionTime = DateTime.UtcNow - startTime;

            if (executionResult.Success)
            {
                _logger?.LogDebug("Inline code execution completed successfully in {ExecutionTime}", executionTime);
                return CodeExecutionResult.CreateSuccess(
                    executionResult.Output,
                    executionTime,
                    new Dictionary<string, object>
                    {
                        ["MethodName"] = "Execute",
                        ["ExecutionModel"] = "Simplified"
                    });
            }
            else
            {
                _logger?.LogWarning("Inline code execution failed in {ExecutionTime}: {Error}", executionTime, executionResult.ErrorMessage);
                return CodeExecutionResult.CreateFailure(
                    executionResult.ErrorMessage ?? "Code execution failed",
                    executionResult.Exception,
                    executionTime);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Inline code execution was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during inline code execution");
            return CodeExecutionResult.CreateFailure(
                $"Unexpected error: {ex.Message}",
                ex,
                DateTime.UtcNow - startTime);
        }
    }
    private async Task<ExecutionResult> ExecuteCodeAsync(
        string code,
        CodeExecutionContext context,
        CancellationToken ct)
    {
        try
        {
            // Check cache first
            var cacheKey = GenerateCacheKey(code, context.Parameters);
            if (_executionCache.TryGetValue(cacheKey, out var cached))
            {
                _logger?.LogDebug("Using cached execution delegate for code");
                return await ExecuteWithDelegateAsync(cached.del, context, ct).ConfigureAwait(false);
            }

            // For this simplified implementation, we'll use a basic expression evaluation approach
            // In a production system, you would use Roslyn (Microsoft.CodeAnalysis) for proper compilation
            // Create a simple execution wrapper
            var executionDelegate = await CreateExecutionDelegateAsync(code, context).ConfigureAwait(false);

            // Cache the delegate for future use
            _executionCache[cacheKey] = (executionDelegate, DateTime.UtcNow);

            // Evict if over limit
            if (_executionCache.Count > 50)
            {
                var oldest = _executionCache.OrderBy(kv => kv.Value.timestamp).First();
                _executionCache.TryRemove(oldest.Key, out _);
            }

            // Execute the code
            return await ExecuteWithDelegateAsync(executionDelegate, context, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during code execution preparation");
            return new ExecutionResult(false, null, ex.Message, ex);
        }
    }

    private async Task<ExecutionResult> ExecuteWithDelegateAsync(
        Delegate executionDelegate,
        CodeExecutionContext context,
        CancellationToken ct)
    {
        try
        {
            // Execute with timeout
            var timeoutMs = (int)Math.Min(_securityConfig.MaxMemoryUsage * 1000, int.MaxValue);
            var executionTask = Task.Run(() =>
            {
                try
                {
                    // Invoke the compiled delegate
                    var result = executionDelegate.DynamicInvoke(context);
                    return result;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error in code execution: {ex.Message}", ex);
                }
            }, ct);

            if (await Task.WhenAny(executionTask, Task.Delay(timeoutMs, ct)).ConfigureAwait(false) == executionTask)
            {
                var result = await executionTask.ConfigureAwait(false);
                return new ExecutionResult(true, result, null);
            }
            else
            {
                return new ExecutionResult(false, null, "Code execution timed out");
            }
        }
        catch (OutOfMemoryException ex)
        {
            _logger?.LogCritical(ex, "Out of memory during code execution");
            return new ExecutionResult(false, null, "Out of memory", ex);
        }
        catch (Exception ex)
        {
            return new ExecutionResult(false, null, ex.Message, ex);
        }
    }

    private async Task<Delegate> CreateExecutionDelegateAsync(string code, CodeExecutionContext context)
    {
        var assembly = CompileCode(code, "DynamicCode", "Execute", "object", "CodeExecutionContext");
        return (Func<CodeExecutionContext, object>)(ctx => ExecuteCompiledMethod(assembly, "DynamicCode", "Execute", ctx)!);
    }

    private static string GenerateCacheKey(string code, IReadOnlyDictionary<string, object> parameters)
    {
        var key = code.GetHashCode().ToString(CultureInfo.InvariantCulture);
        foreach (var param in parameters.OrderBy(p => p.Key))
        {
            key += $":{param.Key}:{param.Value?.GetHashCode().ToString(CultureInfo.InvariantCulture) ?? "null"}";
        }
        return key;
    }

    sealed class ExecutionResult(bool success, object? output, string? errorMessage, Exception? exception = null)
    {
        public bool Success { get; } = success;
        public object? Output { get; } = output;
        public string? ErrorMessage { get; } = errorMessage;
        public Exception? Exception { get; } = exception;
    }
}

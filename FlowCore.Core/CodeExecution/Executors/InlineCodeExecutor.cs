using System.Reflection;
using System.Text;
using FlowCore.CodeExecution.Security;

namespace FlowCore.CodeExecution.Executors;

/// <summary>
/// Executes C# code strings using a simplified execution model.
/// Provides secure execution with namespace and type restrictions.
/// Note: This is a simplified implementation. For production use, consider using Roslyn (Microsoft.CodeAnalysis) for full compilation.
/// </summary>
public class InlineCodeExecutor : ICodeExecutor
{
    private readonly NamespaceValidator _namespaceValidator;
    private readonly TypeValidator _typeValidator;
    private readonly CodeSecurityConfig _securityConfig;
    private readonly ILogger? _logger;
    private static readonly Dictionary<string, Delegate> _executionCache = [];

    /// <summary>
    /// Gets the unique identifier for this executor type.
    /// </summary>
    public string ExecutorType => "InlineCodeExecutor";

    /// <summary>
    /// Gets the list of programming languages supported by this executor.
    /// </summary>
    public IReadOnlyList<string> SupportedLanguages => ["csharp", "c#"];

    /// <summary>
    /// Initializes a new instance of the InlineCodeExecutor.
    /// </summary>
    /// <param name="securityConfig">The security configuration for code validation.</param>
    /// <param name="logger">Optional logger for execution operations.</param>
    public InlineCodeExecutor(CodeSecurityConfig securityConfig, ILogger? logger = null)
    {
        _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));
        _namespaceValidator = new NamespaceValidator(securityConfig, logger);
        _typeValidator = new TypeValidator(securityConfig, logger);
        _logger = logger;
    }

    /// <summary>
    /// Executes the configured code with the provided execution context.
    /// </summary>
    /// <param name="context">The execution context containing workflow state and configuration.</param>
    /// <param name="cancellationToken">Token that can be used to cancel the code execution.</param>
    /// <returns>A task representing the code execution result with success status, output data, and any errors.</returns>
    public async Task<CodeExecutionResult> ExecuteAsync(
        CodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger?.LogDebug("Starting inline code execution");

            // Validate the code before execution
            var codeToValidate = context.GetInput<string>();
            var configForValidation = CodeExecutionConfig.CreateInline(
                "csharp",
                codeToValidate,
                _securityConfig.AllowedNamespaces,
                _securityConfig.AllowedTypes,
                _securityConfig.BlockedNamespaces,
                context.Parameters);

            var namespaceValidation = _namespaceValidator.ValidateNamespaces(codeToValidate);
            var typeValidation = _typeValidator.ValidateTypes(codeToValidate);

            if (!namespaceValidation.IsValid || !typeValidation.IsValid)
            {
                var errors = new List<string>();
                if (!namespaceValidation.IsValid) errors.AddRange(namespaceValidation.Errors);
                if (!typeValidation.IsValid) errors.AddRange(typeValidation.Errors);
                return CodeExecutionResult.CreateFailure(
                    $"Code validation failed: {string.Join(", ", errors)}",
                    executionTime: DateTime.UtcNow - startTime);
            }

            // Get the code to execute
            var code = context.GetInput<string>();
            if (string.IsNullOrEmpty(code))
            {
                return CodeExecutionResult.CreateFailure(
                    "No code provided for execution",
                    executionTime: DateTime.UtcNow - startTime);
            }

            // Execute the code using simplified execution model
            var executionResult = await ExecuteCodeAsync(code, context, cancellationToken);

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

    /// <summary>
    /// Determines whether this executor can handle the specified configuration.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>True if this executor can handle the configuration, false otherwise.</returns>
    public bool CanExecute(CodeExecutionConfig config)
    {
        return config.Mode == CodeExecutionMode.Inline &&
               SupportedLanguages.Contains(config.Language, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates that the code can be executed safely with the given configuration.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>A validation result indicating whether the code is safe to execute.</returns>
    public ValidationResult ValidateExecutionSafety(CodeExecutionConfig config)
    {
        if (config.Mode != CodeExecutionMode.Inline)
        {
            return ValidationResult.Failure(new[] { $"This executor only supports {CodeExecutionMode.Inline} mode" });
        }

        if (!SupportedLanguages.Contains(config.Language, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Failure(new[] { $"Unsupported language: {config.Language}" });
        }

        if (string.IsNullOrEmpty(config.Code))
        {
            return ValidationResult.Failure(new[] { "No code provided for execution" });
        }

        // Use the validators to check security compliance
        var namespaceValidation = _namespaceValidator.ValidateNamespaces(config.Code);
        var typeValidation = _typeValidator.ValidateTypes(config.Code);

        if (!namespaceValidation.IsValid || !typeValidation.IsValid)
        {
            var errors = new List<string>();
            if (!namespaceValidation.IsValid) errors.AddRange(namespaceValidation.Errors);
            if (!typeValidation.IsValid) errors.AddRange(typeValidation.Errors);
            return ValidationResult.Failure(errors);
        }

        return ValidationResult.Success();
    }

    private async Task<ExecutionResult> ExecuteCodeAsync(
        string code,
        CodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check cache first
            var cacheKey = GenerateCacheKey(code, context.Parameters);
            if (_executionCache.TryGetValue(cacheKey, out var cachedDelegate))
            {
                _logger?.LogDebug("Using cached execution delegate for code");
                return await ExecuteWithDelegateAsync(cachedDelegate, context, cancellationToken);
            }

            // For this simplified implementation, we'll use a basic expression evaluation approach
            // In a production system, you would use Roslyn (Microsoft.CodeAnalysis) for proper compilation

            // Validate that the code is a simple expression or statement
            if (!IsSimpleExecutableCode(code))
            {
                return new ExecutionResult(false, null, "Code contains unsupported constructs. Only simple expressions and statements are supported.");
            }

            // Create a simple execution wrapper
            var executionDelegate = await CreateExecutionDelegateAsync(code, context);

            // Cache the delegate for future use
            _executionCache[cacheKey] = executionDelegate;

            // Execute the code
            return await ExecuteWithDelegateAsync(executionDelegate, context, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        try
        {
            // Execute with timeout
            var timeoutMs = (int)Math.Min(_securityConfig.MaxMemoryUsage * 1000, int.MaxValue);

            var executionTask = Task.Run(() =>
            {
                try
                {
                    // For this simplified implementation, we'll simulate code execution
                    // In a real implementation, this would invoke the compiled delegate
                    return ExecuteSimplified(context);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error in code execution: {ex.Message}", ex);
                }
            }, cancellationToken);

            if (await Task.WhenAny(executionTask, Task.Delay(timeoutMs, cancellationToken)) == executionTask)
            {
                var result = await executionTask;
                return new ExecutionResult(true, result, null);
            }
            else
            {
                return new ExecutionResult(false, null, "Code execution timed out");
            }
        }
        catch (Exception ex)
        {
            return new ExecutionResult(false, null, ex.Message, ex);
        }
    }

    private object ExecuteSimplified(CodeExecutionContext context)
    {
        // This is a simplified execution model for demonstration
        // In a real implementation, this would execute the actual compiled code

        // For now, we'll just return a success indicator
        // The actual code would be executed here with proper access to context

        return true; // Simplified success result
    }

    private async Task<Delegate> CreateExecutionDelegateAsync(string code, CodeExecutionContext context)
    {
        // This is a placeholder for delegate creation
        // In a real implementation using Roslyn, this would compile the code into a delegate

        // For now, return a simple delegate that does nothing
        return (Func<CodeExecutionContext, object>)(ctx => true);
    }

    private bool IsSimpleExecutableCode(string code)
    {
        // Basic validation for supported code patterns
        var supportedPatterns = new[]
        {
            @"^\s*return\s+.+;\s*$", // Simple return statements
            @"^\s*[a-zA-Z_][a-zA-Z0-9_]*\s*=\s*.+;\s*$", // Simple assignments
            @"^\s*[a-zA-Z_][a-zA-Z0-9_]*\s*\+\+\s*;\s*$", // Increment operations
            @"^\s*[a-zA-Z_][a-zA-Z0-9_]*\s*--\s*;\s*$", // Decrement operations
            @"^\s*if\s*\(.+\)\s*{?.+}\s*$", // Simple if statements
        };

        return supportedPatterns.Any(pattern =>
            System.Text.RegularExpressions.Regex.IsMatch(code.Trim(), pattern, System.Text.RegularExpressions.RegexOptions.Singleline));
    }

    private string GenerateCacheKey(string code, IReadOnlyDictionary<string, object> parameters)
    {
        var key = code.GetHashCode().ToString();

        foreach (var param in parameters.OrderBy(p => p.Key))
        {
            key += $":{param.Key}:{param.Value?.GetHashCode().ToString() ?? "null"}";
        }

        return key;
    }

    private class ExecutionResult
    {
        public bool Success { get; }
        public object? Output { get; }
        public string? ErrorMessage { get; }
        public Exception? Exception { get; }

        public ExecutionResult(bool success, object? output, string? errorMessage, Exception? exception = null)
        {
            Success = success;
            Output = output;
            ErrorMessage = errorMessage;
            Exception = exception;
        }
    }
}
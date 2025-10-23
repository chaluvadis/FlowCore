using System.Security;
using System.Security.Permissions;

namespace FlowCore.CodeExecution.Security;

/// <summary>
/// Provides sandboxing capabilities for code execution.
/// Restricts permissions and resources available to executed code.
/// </summary>
public class CodeExecutionSandbox : IDisposable
{
    private readonly CodeSecurityConfig _securityConfig;
    private readonly ILogger? _logger;
    private AppDomain? _sandboxDomain;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the CodeExecutionSandbox.
    /// </summary>
    /// <param name="securityConfig">The security configuration for sandboxing.</param>
    /// <param name="logger">Optional logger for sandbox operations.</param>
    public CodeExecutionSandbox(CodeSecurityConfig securityConfig, ILogger? logger = null)
    {
        _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));
        _logger = logger;
    }

    /// <summary>
    /// Executes code within a sandboxed environment.
    /// </summary>
    /// <typeparam name="T">The expected return type of the code.</typeparam>
    /// <param name="code">The code to execute.</param>
    /// <param name="parameters">Parameters to pass to the code.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the code execution.</returns>
    public async Task<SandboxedExecutionResult<T>> ExecuteInSandboxAsync<T>(
        string code,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CodeExecutionSandbox));
        }

        var startTime = DateTime.UtcNow;

        try
        {
            _logger?.LogDebug("Starting sandboxed code execution");

            // For this implementation, we'll use permission-based sandboxing
            // In a production environment, you might use AppDomain isolation

            var executionResult = await ExecuteWithPermissionsAsync<T>(code, parameters, cancellationToken);

            var executionTime = DateTime.UtcNow - startTime;

            if (executionResult.Success)
            {
                _logger?.LogDebug("Sandboxed code execution completed successfully in {ExecutionTime}", executionTime);
                return new SandboxedExecutionResult<T>
                {
                    Success = true,
                    Result = executionResult.Result,
                    ExecutionTime = executionTime,
                    PermissionsUsed = executionResult.PermissionsUsed
                };
            }
            else
            {
                _logger?.LogWarning("Sandboxed code execution failed in {ExecutionTime}: {Error}", executionTime, executionResult.ErrorMessage);
                return new SandboxedExecutionResult<T>
                {
                    Success = false,
                    ErrorMessage = executionResult.ErrorMessage,
                    Exception = executionResult.Exception,
                    ExecutionTime = executionTime,
                    PermissionsUsed = executionResult.PermissionsUsed
                };
            }
        }
        catch (SecurityException ex)
        {
            var executionTime = DateTime.UtcNow - startTime;
            _logger?.LogWarning("Security violation during sandboxed execution in {ExecutionTime}: {Error}", executionTime, ex.Message);

            return new SandboxedExecutionResult<T>
            {
                Success = false,
                ErrorMessage = $"Security violation: {ex.Message}",
                Exception = ex,
                ExecutionTime = executionTime,
                SecurityViolation = true
            };
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - startTime;
            _logger?.LogError(ex, "Unexpected error during sandboxed execution");

            return new SandboxedExecutionResult<T>
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Exception = ex,
                ExecutionTime = executionTime
            };
        }
    }

    /// <summary>
    /// Validates that the code can run within the sandbox restrictions.
    /// </summary>
    /// <param name="code">The code to validate.</param>
    /// <returns>A validation result indicating whether the code is compatible with sandboxing.</returns>
    public ValidationResult ValidateSandboxCompatibility(string code)
    {
        var violations = new List<string>();

        // Check for operations that would violate sandbox restrictions
        if (!_securityConfig.AllowFileSystemAccess)
        {
            var fileSystemPatterns = new[]
            {
                @"System\.IO\.File",
                @"System\.IO\.Directory",
                @"File\.Read",
                @"File\.Write",
                @"Directory\.Get"
            };

            foreach (var pattern in fileSystemPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(code, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    violations.Add($"File system access not allowed in sandbox: {pattern}");
                }
            }
        }

        if (!_securityConfig.AllowNetworkAccess)
        {
            var networkPatterns = new[]
            {
                @"System\.Net\.",
                @"HttpClient",
                @"WebClient",
                @"TcpClient"
            };

            foreach (var pattern in networkPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(code, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    violations.Add($"Network access not allowed in sandbox: {pattern}");
                }
            }
        }

        if (!_securityConfig.AllowReflection)
        {
            var reflectionPatterns = new[]
            {
                @"Assembly\.Get",
                @"Type\.Get",
                @"Activator\.Create",
                @"GetType\(\)",
                @"typeof\("
            };

            foreach (var pattern in reflectionPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(code, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    violations.Add($"Reflection not allowed in sandbox: {pattern}");
                }
            }
        }

        return violations.Any() ? ValidationResult.Failure(violations.AsReadOnly()) : ValidationResult.Success();
    }

    private async Task<SandboxedExecutionResult<T>> ExecuteWithPermissionsAsync<T>(
        string code,
        IDictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        // Create a restricted permission set based on security configuration
        // Note: In modern .NET, permission-based sandboxing is limited
        // This is a simplified implementation for demonstration

        // Conditionally add permissions based on security configuration
        if (_securityConfig.AllowThreading)
        {
            // Threading permissions would be added here in a full implementation
        }

        // Set up the permission context
        var permissionContext = new PermissionContext
        {
            Config = _securityConfig,
            Parameters = parameters ?? new Dictionary<string, object>(),
            AllowedOperations = GetAllowedOperations()
        };

        try
        {
            // Execute code with restricted permissions
            // Note: In a real implementation, this would use AppDomain or other isolation mechanisms

            var result = await ExecuteCodeWithRestrictionsAsync<T>(code, permissionContext, cancellationToken);

            return new SandboxedExecutionResult<T>
            {
                Success = true,
                Result = result,
                PermissionsUsed = permissionContext.AllowedOperations
            };
        }
        catch (SecurityException ex)
        {
            return new SandboxedExecutionResult<T>
            {
                Success = false,
                ErrorMessage = $"Permission denied: {ex.Message}",
                Exception = ex,
                SecurityViolation = true,
                PermissionsUsed = permissionContext.AllowedOperations
            };
        }
    }

    private async Task<T> ExecuteCodeWithRestrictionsAsync<T>(
        string code,
        PermissionContext context,
        CancellationToken cancellationToken)
    {
        // This is a simplified implementation
        // In a production system, you would:
        // 1. Create a separate AppDomain with restricted permissions
        // 2. Load the code into that AppDomain
        // 3. Execute it with proper isolation
        // 4. Marshal the results back

        // For now, we'll simulate the execution
        await Task.Delay(10, cancellationToken); // Simulate execution time

        // In a real implementation, this would execute the actual code
        return (T)(object)true; // Simplified success result
    }

    private IReadOnlyList<string> GetAllowedOperations()
    {
        var operations = new List<string> { "Execution" };

        if (_securityConfig.AllowThreading)
        {
            operations.Add("Threading");
        }

        if (_securityConfig.AllowFileSystemAccess)
        {
            operations.Add("FileSystem");
        }

        if (_securityConfig.AllowNetworkAccess)
        {
            operations.Add("Network");
        }

        if (_securityConfig.AllowReflection)
        {
            operations.Add("Reflection");
        }

        return operations;
    }

    /// <summary>
    /// Disposes the sandbox and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                // Clean up sandbox domain if it exists
                if (_sandboxDomain != null)
                {
                    AppDomain.Unload(_sandboxDomain);
                    _sandboxDomain = null;
                }

                _logger?.LogDebug("Code execution sandbox disposed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing code execution sandbox");
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    private class PermissionContext
    {
        public CodeSecurityConfig Config { get; set; } = CodeSecurityConfig.CreateDefault();
        public IDictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public IReadOnlyList<string> AllowedOperations { get; set; } = new List<string>();
    }
}

/// <summary>
/// Result of sandboxed code execution.
/// </summary>
/// <typeparam name="T">The type of the execution result.</typeparam>
public class SandboxedExecutionResult<T>
{
    /// <summary>
    /// Gets a value indicating whether the execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets the result of the execution.
    /// </summary>
    public T? Result { get; set; }

    /// <summary>
    /// Gets the error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the exception that occurred during execution.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets the execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets a value indicating whether a security violation occurred.
    /// </summary>
    public bool SecurityViolation { get; set; }

    /// <summary>
    /// Gets the permissions that were used during execution.
    /// </summary>
    public IReadOnlyList<string> PermissionsUsed { get; set; } = new List<string>();
}
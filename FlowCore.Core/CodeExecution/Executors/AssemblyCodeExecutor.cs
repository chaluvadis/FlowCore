namespace FlowCore.CodeExecution.Executors;

/// <summary>
/// Executes code from pre-compiled .NET assemblies.
/// Loads assemblies dynamically and executes specified methods with security validation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the AssemblyCodeExecutor.
/// </remarks>
/// <param name="securityConfig">The security configuration for assembly validation.</param>
/// <param name="logger">Optional logger for execution operations.</param>
public class AssemblyCodeExecutor(CodeSecurityConfig securityConfig, ILogger? logger = null) : ICodeExecutor
{
    private readonly CodeSecurityConfig _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));
    private static readonly ConcurrentDictionary<string, (Assembly Assembly, DateTime LastAccessed)> _assemblyCache = new();
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Gets the unique identifier for this executor type.
    /// </summary>
    public string ExecutorType => "AssemblyCodeExecutor";

    /// <summary>
    /// Gets the list of programming languages supported by this executor.
    /// </summary>
    public IReadOnlyList<string> SupportedLanguages => ["csharp", "c#", "dotnet"];

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
            logger?.LogDebug("Starting assembly code execution");

            // Get configuration from context
            var assemblyPath = GetRequiredParameter(context, "AssemblyPath");
            var typeName = GetRequiredParameter(context, "TypeName");
            var methodName = GetParameterWithDefault(context, "MethodName", "Execute");

            if (string.IsNullOrEmpty(assemblyPath))
            {
                return CodeExecutionResult.CreateFailure(
                    "AssemblyPath parameter is required for assembly execution",
                    executionTime: DateTime.UtcNow - startTime);
            }

            if (string.IsNullOrEmpty(typeName))
            {
                return CodeExecutionResult.CreateFailure(
                    "TypeName parameter is required for assembly execution",
                    executionTime: DateTime.UtcNow - startTime);
            }

            // Load and validate the assembly
            var assemblyLoadResult = await LoadAssemblyAsync(assemblyPath, cancellationToken);
            if (!assemblyLoadResult.Success)
            {
                return CodeExecutionResult.CreateFailure(
                    $"Failed to load assembly: {assemblyLoadResult.ErrorMessage}",
                    executionTime: DateTime.UtcNow - startTime);
            }

            // Validate assembly security
            var securityValidation = ValidateAssemblySecurity(assemblyLoadResult.Assembly!);
            if (!securityValidation.IsValid)
            {
                return CodeExecutionResult.CreateFailure(
                    $"Assembly security validation failed: {string.Join(", ", securityValidation.Errors)}",
                    executionTime: DateTime.UtcNow - startTime);
            }

            // Execute the specified method
            var executionResult = await ExecuteAssemblyMethodAsync(
                assemblyLoadResult.Assembly!,
                typeName,
                methodName,
                context,
                cancellationToken);

            var executionTime = DateTime.UtcNow - startTime;
            if (executionResult.Success)
            {
                logger?.LogDebug("Assembly code execution completed successfully in {ExecutionTime}", executionTime);
                return CodeExecutionResult.CreateSuccess(
                    executionResult.Output,
                    executionTime,
                    new Dictionary<string, object>
                    {
                        ["AssemblyPath"] = assemblyPath,
                        ["TypeName"] = typeName,
                        ["MethodName"] = methodName,
                        ["AssemblyName"] = assemblyLoadResult.Assembly!.GetName().Name
                    });
            }
            else
            {
                logger?.LogWarning("Assembly code execution failed in {ExecutionTime}: {Error}", executionTime, executionResult.ErrorMessage);
                return CodeExecutionResult.CreateFailure(
                    executionResult.ErrorMessage ?? "Assembly method execution failed",
                    executionResult.Exception,
                    executionTime);
            }
        }
        catch (OperationCanceledException)
        {
            logger?.LogWarning("Assembly code execution was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error during assembly code execution");
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
    public bool CanExecute(CodeExecutionConfig config) => config.Mode == CodeExecutionMode.Assembly &&
               SupportedLanguages.Contains(config.Language, StringComparer.OrdinalIgnoreCase) &&
               !string.IsNullOrEmpty(config.AssemblyPath) &&
               !string.IsNullOrEmpty(config.TypeName);

    /// <summary>
    /// Validates that the code can be executed safely with the given configuration.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>A validation result indicating whether the code is safe to execute.</returns>
    public ValidationResult ValidateExecutionSafety(CodeExecutionConfig config)
    {
        if (config.Mode != CodeExecutionMode.Assembly)
        {
            return ValidationResult.Failure([$"This executor only supports {CodeExecutionMode.Assembly} mode"]);
        }

        if (!SupportedLanguages.Contains(config.Language, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Failure([$"Unsupported language: {config.Language}"]);
        }

        if (string.IsNullOrEmpty(config.AssemblyPath))
        {
            return ValidationResult.Failure(["AssemblyPath is required for assembly execution"]);
        }

        if (string.IsNullOrEmpty(config.TypeName))
        {
            return ValidationResult.Failure(["TypeName is required for assembly execution"]);
        }

        // Check if assembly file exists
        if (!File.Exists(config.AssemblyPath))
        {
            return ValidationResult.Failure([$"Assembly file does not exist: {config.AssemblyPath}"]);
        }

        // Validate assembly path is within allowed directories if restrictions are in place
        if (!IsPathAllowed(config.AssemblyPath))
        {
            return ValidationResult.Failure([$"Assembly path is not allowed: {config.AssemblyPath}"]);
        }

        return ValidationResult.Success();
    }

    private async Task<AssemblyLoadResult> LoadAssemblyAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        try
        {
            lock (_cacheLock)
            {
                // Check cache first
                if (_assemblyCache.TryGetValue(assemblyPath, out var cachedEntry))
                {
                    logger?.LogDebug("Using cached assembly: {AssemblyPath}", assemblyPath);
                    // Update last accessed time for LRU
                    _assemblyCache[ assemblyPath ] = (cachedEntry.Assembly, DateTime.UtcNow);
                    return new AssemblyLoadResult(true, cachedEntry.Assembly, null);
                }

                // Load the assembly
                var assembly = Assembly.LoadFrom(assemblyPath);

                // Evict if cache is full (simple LRU: remove oldest)
                if (_assemblyCache.Count >= _securityConfig.MaxAssemblyCacheSize)
                {
                    var oldest = _assemblyCache.OrderBy(kvp => kvp.Value.LastAccessed).First();
                    _assemblyCache.TryRemove(oldest.Key, out _);
                    logger?.LogDebug("Evicted assembly from cache: {AssemblyPath}", oldest.Key);
                }

                // Cache the assembly
                _assemblyCache[ assemblyPath ] = (assembly, DateTime.UtcNow);

                logger?.LogDebug("Assembly loaded successfully: {AssemblyName}", assembly.GetName().Name);
                return new AssemblyLoadResult(true, assembly, null);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error loading assembly: {AssemblyPath}", assemblyPath);
            return new AssemblyLoadResult(false, null, ex.Message);
        }
    }

    private ValidationResult ValidateAssemblySecurity(Assembly assembly)
    {
        var errors = new List<string>();

        try
        {
            var assemblyName = assembly.GetName();

            // Check if assembly has a strong name (signed)
            var publicKey = assemblyName.GetPublicKey();
            if (publicKey == null || publicKey.Length == 0)
            {
                logger?.LogWarning("Assembly {AssemblyName} does not have a strong name signature", assemblyName.Name);
                // This is a warning, not an error - allow unsigned assemblies for development
            }

            // Validate against blocked assemblies if any are specified
            var assemblyFullName = assembly.FullName ?? assemblyName.Name ?? "";
            if (_securityConfig.BlockedTypes.Any(blocked => assemblyFullName.Contains(blocked)))
            {
                errors.Add($"Assembly {assemblyFullName} is blocked");
            }

            // Log assembly information for audit purposes
            logger?.LogInformation("Assembly security validation passed for {AssemblyName} v{Version}",
                assemblyName.Name, assemblyName.Version);

            return errors.Any() ? ValidationResult.Failure(errors) : ValidationResult.Success();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during assembly security validation");
            return ValidationResult.Failure([$"Security validation error: {ex.Message}"]);
        }
    }

    private async Task<ExecutionResult> ExecuteAssemblyMethodAsync(
        Assembly assembly,
        string typeName,
        string methodName,
        CodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the assembly path for sandboxing
            var assemblyPath = assembly.Location;
            if (string.IsNullOrEmpty(assemblyPath))
            {
                // Fallback to non-sandboxed execution if path not available
                return await ExecuteInCurrentDomainAsync(assembly, typeName, methodName, context, cancellationToken);
            }

            // Use sandboxed execution with AppDomain
            return await ExecuteInSandboxAsync(assemblyPath, typeName, methodName, context, cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during assembly method execution");
            return new ExecutionResult(false, null, ex.Message, ex);
        }
    }

    private async Task<ExecutionResult> ExecuteInSandboxAsync(
        string assemblyPath,
        string typeName,
        string methodName,
        CodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var timeoutMs = _securityConfig.MaxExecutionTime;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        var executionTask = Task.Run(() =>
        {
            AppDomain sandboxDomain = null;
            try
            {
                // Create a new AppDomain for isolation
                sandboxDomain = AppDomain.CreateDomain($"Sandbox_{Guid.NewGuid()}");

                // Create proxy in the sandbox domain
                var proxyType = typeof(AssemblyExecutionProxy);
                var proxy = (AssemblyExecutionProxy)sandboxDomain.CreateInstanceAndUnwrap(
                    proxyType.Assembly.FullName, proxyType.FullName);

                // Prepare parameters (note: context may not be serializable, so pass only necessary data)
                var parameters = PrepareMethodParametersForProxy(context);

                // Execute the method in the sandbox
                var result = proxy.ExecuteMethod(assemblyPath, typeName, methodName, parameters);
                return new ExecutionResult(true, result, null);
            }
            catch (Exception ex)
            {
                return new ExecutionResult(false, null, ex.Message, ex);
            }
            finally
            {
                if (sandboxDomain != null)
                {
                    AppDomain.Unload(sandboxDomain);
                }
            }
        }, cts.Token);

        if (await Task.WhenAny(executionTask, Task.Delay(timeoutMs, cancellationToken)) == executionTask)
        {
            return await executionTask;
        }
        else
        {
            cts.Cancel();
            return new ExecutionResult(false, null, "Assembly method execution timed out");
        }
    }

    private async Task<ExecutionResult> ExecuteInCurrentDomainAsync(
        Assembly assembly,
        string typeName,
        string methodName,
        CodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the type
            var type = assembly.GetType(typeName);
            if (type == null)
            {
                return new ExecutionResult(false, null, $"Type '{typeName}' not found in assembly '{assembly.GetName().Name}'");
            }

            // Get the method
            var method = FindMethod(type, methodName);
            if (method == null)
            {
                return new ExecutionResult(false, null, $"Method '{methodName}' not found in type '{typeName}'");
            }

            // Create instance if needed
            object? instance = null;
            if (!method.IsStatic)
            {
                instance = Activator.CreateInstance(type);
                if (instance == null)
                {
                    return new ExecutionResult(false, null, $"Failed to create instance of type '{typeName}'");
                }
            }

            // Prepare method parameters
            var parameters = PrepareMethodParameters(method, context);

            // Execute with timeout
            var timeoutMs = _securityConfig.MaxExecutionTime;

            var executionTask = Task.Run(() =>
            {
                try
                {
                    return method.Invoke(instance, parameters);
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException ?? ex;
                }
            }, cancellationToken);

            if (await Task.WhenAny(executionTask, Task.Delay(timeoutMs, cancellationToken)) == executionTask)
            {
                var result = await executionTask;
                return new ExecutionResult(true, result, null);
            }
            else
            {
                return new ExecutionResult(false, null, "Assembly method execution timed out");
            }
        }
        catch (Exception ex)
        {
            return new ExecutionResult(false, null, ex.Message, ex);
        }
    }

    private object?[] PrepareMethodParametersForProxy(CodeExecutionContext context)
    {
        // Since context may not be serializable, extract simple parameters
        // For now, return empty array; in a real implementation, serialize necessary data
        return [];
    }

    private MethodInfo? FindMethod(Type type, string methodName)
    {
        // Try to find the method with the exact name
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        // First try exact match
        var method = methods.FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
        if (method != null)
        {
            return method;
        }

        // Try with common suffixes from config
        foreach (var suffix in _securityConfig.MethodSuffixes)
        {
            method = methods.FirstOrDefault(m => m.Name.Equals(methodName + suffix, StringComparison.OrdinalIgnoreCase));
            if (method != null)
            {
                return method;
            }
        }

        return null;
    }

    private object?[] PrepareMethodParameters(MethodInfo method, CodeExecutionContext context)
    {
        var methodParameters = method.GetParameters();
        var args = new object?[methodParameters.Length];

        for (int i = 0; i < methodParameters.Length; i++)
        {
            var param = methodParameters[i];

            // Try to get parameter from context state first
            if (context.ContainsState(param.Name!))
            {
                try
                {
                    var value = context.GetState<object>(param.Name!);
                    args[i] = ValidateAndConvertParameter(value, param.ParameterType);
                }
                catch
                {
                    args[i] = GetDefaultValue(param.ParameterType);
                }
            }
            // Try to get from configuration parameters
            else if (context.Parameters.TryGetValue(param.Name!, out var paramValue))
            {
                args[i] = ValidateAndConvertParameter(paramValue, param.ParameterType);
            }
            // Try to inject context if parameter type matches
            else if (param.ParameterType == typeof(CodeExecutionContext))
            {
                args[i] = context;
            }
            // Try to inject workflow context if parameter type matches
            else if (param.ParameterType == typeof(ExecutionContext))
            {
                // This would need access to the original workflow context
                args[i] = GetDefaultValue(param.ParameterType);
            }
            else
            {
                args[i] = GetDefaultValue(param.ParameterType);
            }
        }

        return args;
    }

    private object? GetDefaultValue(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

    private object? ValidateAndConvertParameter(object? value, Type targetType)
    {
        if (value == null)
        {
            return targetType.IsValueType ? GetDefaultValue(targetType) : null;
        }

        try
        {
            // Check if the value is already of the correct type
            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            // Attempt safe conversion
            if (targetType == typeof(string))
            {
                return value.ToString();
            }
            else if (targetType.IsPrimitive || targetType.IsEnum)
            {
                return Convert.ChangeType(value, targetType);
            }
            else if (targetType == typeof(DateTime))
            {
                if (value is string strValue)
                {
                    return DateTime.Parse(strValue);
                }
                return Convert.ChangeType(value, targetType);
            }
            else
            {
                // For complex types, attempt conversion if possible
                return Convert.ChangeType(value, targetType);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to convert parameter value {Value} to type {TargetType}", value, targetType.Name);
            return GetDefaultValue(targetType);
        }
    }

    private string GetRequiredParameter(CodeExecutionContext context, string parameterName)
    {
        try
        {
            return context.GetParameter<string>(parameterName);
        }
        catch
        {
            return string.Empty;
        }
    }

    private string GetParameterWithDefault(CodeExecutionContext context, string parameterName, string defaultValue)
    {
        try
        {
            return context.GetParameter<string>(parameterName);
        }
        catch
        {
            return defaultValue;
        }
    }

    private bool IsPathAllowed(string assemblyPath)
    {
        try
        {
            // Normalize the path to handle relative paths and resolve any .. or . components
            var normalizedPath = Path.GetFullPath(assemblyPath);

            // Check for directory traversal attempts (e.g., .. in path)
            if (assemblyPath.Contains("..") || normalizedPath.Contains(".."))
            {
                logger?.LogWarning("Path traversal attempt detected in assembly path: {AssemblyPath}", assemblyPath);
                return false;
            }

            // Check allowed extensions
            var allowedExtensions = new[] { ".dll", ".exe" };
            var extension = Path.GetExtension(normalizedPath).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                logger?.LogWarning("Invalid file extension in assembly path: {AssemblyPath}", assemblyPath);
                return false;
            }

            // Check if the path is within allowed directories if configured
            if (_securityConfig.AllowedDirectories.Any())
            {
                var isAllowed = _securityConfig.AllowedDirectories.Any(allowedDir =>
                {
                    var fullAllowedDir = Path.GetFullPath(allowedDir);
                    return normalizedPath.StartsWith(fullAllowedDir, StringComparison.OrdinalIgnoreCase);
                });
                if (!isAllowed)
                {
                    logger?.LogWarning("Assembly path not in allowed directories: {AssemblyPath}", assemblyPath);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error validating assembly path: {AssemblyPath}", assemblyPath);
            return false;
        }
    }

    private class AssemblyLoadResult(bool success, Assembly? assembly, string? errorMessage)
    {
        public bool Success { get; } = success;
        public Assembly? Assembly { get; } = assembly;
        public string? ErrorMessage { get; } = errorMessage;
    }

    private class ExecutionResult(bool success, object? output, string? errorMessage, Exception? exception = null)
    {
        public bool Success { get; } = success;
        public object? Output { get; } = output;
        public string? ErrorMessage { get; } = errorMessage;
        public Exception? Exception { get; } = exception;
    }

    /// <summary>
    /// Proxy class for executing code in a sandboxed AppDomain.
    /// </summary>
    private class AssemblyExecutionProxy : MarshalByRefObject
    {
        public object? ExecuteMethod(string assemblyPath, string typeName, string methodName, object?[] parameters)
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);
                var type = assembly.GetType(typeName);
                if (type == null) throw new InvalidOperationException($"Type '{typeName}' not found.");

                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (method == null) throw new InvalidOperationException($"Method '{methodName}' not found.");

                object? instance = null;
                if (!method.IsStatic)
                {
                    instance = Activator.CreateInstance(type);
                }

                return method.Invoke(instance, parameters);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Execution failed: {ex.Message}", ex);
            }
        }
    }
}
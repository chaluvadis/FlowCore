namespace FlowCore.CodeExecution.Executors;
using System.Security.Cryptography;
using System.Text;
/// <summary>
/// Executes asynchronous C# code strings with full async/await pattern support using Roslyn compilation.
/// Extends the basic inline code executor with advanced async capabilities.
/// </summary>
/// <remarks>
/// Initializes a new instance of the AsyncInlineCodeExecutor.
/// </remarks>
/// <param name="securityConfig">The security configuration for code validation.</param>
/// <param name="logger">Optional logger for execution operations.</param>
public class AsyncInlineCodeExecutor : BaseInlineCodeExecutor, IAsyncCodeExecutor
{
    private static readonly ConcurrentDictionary<string, (AsyncPatternAnalysis analysis, DateTime timestamp)> _asyncPatternCache = new();
    private static readonly ConcurrentDictionary<string, (Assembly assembly, DateTime timestamp)> _compilationCache = new();
    /// <summary>
    /// Gets the unique identifier for this executor type.
    /// </summary>
    public override string ExecutorType => "AsyncInlineCodeExecutor";
    /// <summary>
    /// Gets the maximum degree of parallelism supported by this executor.
    /// </summary>
    public int MaxDegreeOfParallelism => Environment.ProcessorCount * 2;
    /// <summary>
    /// Gets a value indicating whether this executor supports concurrent execution.
    /// </summary>
    public bool SupportsConcurrentExecution => true;

    public ILogger? Logger { get; }

    public AsyncInlineCodeExecutor(CodeSecurityConfig securityConfig, ILogger? logger = null) : base(securityConfig)
    {
        Logger = logger;
    }

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
        // If this is an async context, use the enhanced async execution
        if (context is AsyncCodeExecutionContext asyncContext)
        {
            var asyncResult = await ExecuteAsyncCodeAsync(asyncContext, ct).ConfigureAwait(false);
            return ConvertToCodeExecutionResult(asyncResult);
        }
        // Fall back to basic execution for non-async contexts
        return await ExecuteBasicAsync(context, ct).ConfigureAwait(false);
    }
    /// <summary>
    /// Executes asynchronous C# code with full async/await pattern support.
    /// This method handles complex async patterns including Task-based operations, cancellation, and performance monitoring.
    /// </summary>
    /// <param name="context">The async execution context containing workflow state and configuration.</param>
    /// <param name="ct">Token that can be used to cancel the code execution.</param>
    /// <returns>A task representing the asynchronous code execution result with performance metrics.</returns>
    /// <exception cref="OperationCanceledException">Thrown when execution is cancelled via the cancellation token.</exception>
    /// <exception cref="CompilationException">Thrown when the provided code fails to compile.</exception>
    /// <exception cref="SecurityException">Thrown when code validation fails due to security constraints.</exception>
    public async Task<AsyncCodeExecutionResult> ExecuteAsyncCodeAsync(
        AsyncCodeExecutionContext context,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            using var scope = context.CreateScope("AsyncCodeExecution");
            scope.Log("Starting async code execution");
            // Get the code to execute
            var code = context.Config.Code;
            if (string.IsNullOrEmpty(code))
            {
                return AsyncCodeExecutionResult.CreateAsyncFailure(
                    "No code provided for execution",
                    executionTime: DateTime.UtcNow - startTime);
            }
            // Analyze the code for async patterns
            var asyncAnalysis = AnalyzeAsyncPatterns(code);
            scope.Log("Async pattern analysis: {AsyncPatterns} patterns found", asyncAnalysis.AsyncPatternCount);
            // Validate the code before execution
            var validationResult = await ValidateAsyncCodeAsync(code, context, ct).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return AsyncCodeExecutionResult.CreateAsyncFailure(
                    $"Async code validation failed: {string.Join(", ", validationResult.Errors)}",
                    executionTime: DateTime.UtcNow - startTime);
            }
            // Execute the async code
            var executionResult = await ExecuteAsyncCodeInternalAsync(
                code, context, asyncAnalysis, ct).ConfigureAwait(false);
            var executionTime = DateTime.UtcNow - startTime;
            if (executionResult.Success)
            {
                scope.Log("Async code execution completed successfully in {ExecutionTime}", executionTime);
                return AsyncCodeExecutionResult.CreateAsyncSuccess(
                    executionResult.Output,
                    executionTime,
                    new Dictionary<string, object>
                    {
                        ["MethodName"] = "ExecuteAsync",
                        ["ExecutionModel"] = "AsyncEnhanced",
                        ["AsyncPatternCount"] = asyncAnalysis.AsyncPatternCount
                    },
                    asyncOperations: Array.Empty<AsyncOperationInfo>(),
                    actualDegreeOfParallelism: 1,
                    containedAsyncOperations: asyncAnalysis.ContainsAsyncPatterns,
                    totalAsyncWaitTime: TimeSpan.Zero,
                    performanceMetrics: new AsyncPerformanceMetrics { TotalAsyncOperations = asyncAnalysis.AsyncPatternCount });
            }
            else
            {
                scope.Log("Async code execution failed in {ExecutionTime}: {Error}", executionTime, executionResult.ErrorMessage ?? "Unknown error");
                return AsyncCodeExecutionResult.CreateAsyncFailure(
                    executionResult.ErrorMessage ?? "Async code execution failed",
                    executionResult.Exception,
                    executionTime,
                    new Dictionary<string, object>
                    {
                        ["AsyncPatternCount"] = asyncAnalysis.AsyncPatternCount
                    },
                    performanceMetrics: new AsyncPerformanceMetrics { TotalAsyncOperations = asyncAnalysis.AsyncPatternCount });
            }
        }
        catch (OperationCanceledException)
        {
            Logger?.LogWarning("Async code execution was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unexpected error during async code execution");
            return AsyncCodeExecutionResult.CreateAsyncFailure(
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
    public override bool CanExecute(CodeExecutionConfig config)
        => config.Mode == CodeExecutionMode.Inline &&
               SupportedLanguages.Contains(config.Language, StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Determines whether this executor can handle asynchronous execution patterns.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>True if this executor supports async execution for the given config.</returns>
    public bool SupportsAsyncExecution(CodeExecutionConfig config)
    {
        if (!CanExecute(config))
        {
            return false;
        }
        // Check if the code contains async patterns
        return AnalyzeAsyncPatterns(config.Code).ContainsAsyncPatterns;
    }
    /// <summary>
    /// Validates that the code can be executed safely with the given configuration.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>A validation result indicating whether the code is safe to execute.</returns>
    public override ValidationResult ValidateExecutionSafety(CodeExecutionConfig config)
    {
        var baseResult = base.ValidateExecutionSafety(config);
        if (!baseResult.IsValid)
        {
            return baseResult;
        }
        // Additional validation for async patterns
        var asyncAnalysis = AnalyzeAsyncPatterns(config.Code);
        if (asyncAnalysis.ContainsUnsafeAsyncPatterns)
        {
            return ValidationResult.Failure(["Code contains unsafe async patterns"]);
        }
        return ValidationResult.Success();
    }
    private async Task<CodeExecutionResult> ExecuteBasicAsync(
        CodeExecutionContext context,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            Logger?.LogDebug("Starting basic async code execution");
            var code = context.Config.Code;
            if (string.IsNullOrEmpty(code))
            {
                return CodeExecutionResult.CreateFailure(
                    "No code provided for execution",
                    executionTime: DateTime.UtcNow - startTime);
            }
            var assembly = CodeCompiler.Compile(code, "BasicDynamicCode", "Execute", "object", "CodeExecutionContext");
            var output = CodeCompiler.ExecuteMethod(assembly, "BasicDynamicCode", "Execute", context);
            var executionTime = DateTime.UtcNow - startTime;
            return CodeExecutionResult.CreateSuccess(output, executionTime);
        }
        catch (Exception ex)
        {
            return CodeExecutionResult.CreateFailure($"Unexpected error: {ex.Message}", ex, DateTime.UtcNow - startTime);
        }
    }

    private async Task<ValidationResult> ValidateAsyncCodeAsync(
        string code,
        AsyncCodeExecutionContext context,
        CancellationToken ct)
    {
        // Basic security validation
        var validationResult = ValidateCode(code);
        if (!validationResult.IsValid)
        {
            return validationResult;
        }
        // Async-specific validation
        var errors = new List<string>();
        var asyncAnalysis = AnalyzeAsyncPatterns(code);
        if (asyncAnalysis.ContainsUnsafeAsyncPatterns)
        {
            errors.Add("Code contains unsafe async patterns");
        }
        if (asyncAnalysis.AsyncPatternCount > MaxDegreeOfParallelism * 2)
        {
            errors.Add($"Too many async patterns ({asyncAnalysis.AsyncPatternCount}) for configured parallelism");
        }
        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
    private async Task<ExecutionResult> ExecuteAsyncCodeInternalAsync(
        string code,
        AsyncCodeExecutionContext context,
        AsyncPatternAnalysis analysis,
        CancellationToken ct)
    {
        try
        {
            var cacheKey = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
            // Check compilation cache first
            if (_compilationCache.TryGetValue(cacheKey, out var cachedAssembly))
            {
                Logger?.LogDebug("Using cached compiled assembly for code");
                return await ExecuteWithAssemblyAsync(cachedAssembly.assembly, context, ct).ConfigureAwait(false);
            }
            var assembly = CompileAsyncCode(code, cacheKey);
            return await ExecuteWithAssemblyAsync(assembly, context, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ExecutionResult(false, null, ex.Message, ex);
        }
    }
    private static AsyncPatternAnalysis AnalyzeAsyncPatterns(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return new AsyncPatternAnalysis();
        }

        var cacheKey = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        if (_asyncPatternCache.TryGetValue(cacheKey, out var cached))
        {
            return cached.analysis;
        }
        var asyncPatterns = new[]
        {
            new Regex(@"\basync\s+\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bawait\s+\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Task\s*<\s*\w+\s*>", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bTask\.Run", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\basync\s+void", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bConfigureAwait", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bWhenAll", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bWhenAny", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };
        var unsafePatterns = new[]
        {
            new Regex(@"\.Wait\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\.Result\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bAssembly\.Load", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bThread\.Sleep", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bBlockingCollection", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };
        var asyncCount = asyncPatterns.Sum(pattern => pattern.Matches(code).Count);
        var unsafeCount = unsafePatterns.Sum(pattern => pattern.Matches(code).Count);
        var analysis = new AsyncPatternAnalysis
        {
            ContainsAsyncPatterns = asyncCount > 0,
            AsyncPatternCount = asyncCount,
            ContainsUnsafeAsyncPatterns = unsafeCount > 0,
            UnsafePatternCount = unsafeCount
        };
        CacheUtility.AddOrUpdateWithTimestamp(_asyncPatternCache, cacheKey, analysis, 100);
        return analysis;
    }
    private static CodeExecutionResult ConvertToCodeExecutionResult(AsyncCodeExecutionResult asyncResult)
    {
        if (asyncResult.Success)
        {
            return CodeExecutionResult.CreateSuccess(
                asyncResult.Output,
                asyncResult.ExecutionTime,
                asyncResult.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }
        else
        {
            return CodeExecutionResult.CreateFailure(
                asyncResult.ErrorMessage,
                asyncResult.Exception,
                asyncResult.ExecutionTime,
                asyncResult.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }
    }
    sealed class ExecutionResult(bool success, object? output, string? errorMessage, Exception? exception = null)
    {
        public bool Success { get; } = success;
        public object? Output { get; } = output;
        public string? ErrorMessage { get; } = errorMessage;
        public Exception? Exception { get; } = exception;
    }
    private static async Task<ExecutionResult> ExecuteWithAssemblyAsync(Assembly assembly, AsyncCodeExecutionContext context, CancellationToken ct)
    {
        var type = assembly.GetType("AsyncDynamicCode");
        var method = (type?.GetMethod("ExecuteAsync"))
            ?? throw new InvalidOperationException("Compiled code does not contain ExecuteAsync method");
        var instance = Activator.CreateInstance(type!);
        var task = method.Invoke(instance, [context]);
        if (task is not Task<object> typedTask)
        {
            throw new InvalidOperationException("ExecuteAsync method does not return Task<object>");
        }
        // Wait for the task with proper cancellation support
        try
        {
            var output = await typedTask.WaitAsync(ct).ConfigureAwait(false);
            return new ExecutionResult(true, output, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ExecutionResult(false, null, ex.Message, ex);
        }
    }
    sealed class AsyncPatternAnalysis
    {
        public bool ContainsAsyncPatterns { get; set; }
        public int AsyncPatternCount { get; set; }
        public bool ContainsUnsafeAsyncPatterns { get; set; }
        public int UnsafePatternCount { get; set; }
    }
    // Compilation methods inherited from BaseInlineCodeExecutor
    private Assembly CompileAsyncCode(string code, string cacheKey)
    {
        // Check cache first
        if (CacheUtility.TryGetValue(_compilationCache, cacheKey, out var cachedAssembly))
        {
            Logger?.LogDebug("Using cached compiled assembly for async code");
            return cachedAssembly;
        }
        var assembly = CodeCompiler.Compile(code, "AsyncDynamicCode", "ExecuteAsync", "async Task<object>", "AsyncCodeExecutionContext");
        // Cache the compiled assembly
        CacheUtility.AddOrUpdateWithTimestamp(_compilationCache, cacheKey, assembly, 50);
        return assembly;
    }
}

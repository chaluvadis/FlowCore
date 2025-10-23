namespace FlowCore.CodeExecution.Executors;

using Microsoft.CodeAnalysis.CSharp.Syntax;
/// <summary>
/// Executes asynchronous C# code strings with full async/await pattern support using Roslyn compilation.
/// Extends the basic inline code executor with advanced async capabilities.
/// </summary>
/// <remarks>
/// Initializes a new instance of the AsyncInlineCodeExecutor.
/// </remarks>
/// <param name="securityConfig">The security configuration for code validation.</param>
/// <param name="logger">Optional logger for execution operations.</param>
public class AsyncInlineCodeExecutor(CodeSecurityConfig securityConfig, ILogger? logger = null) : IAsyncCodeExecutor
{
    private readonly NamespaceValidator _namespaceValidator = new(securityConfig, logger);
    private readonly TypeValidator _typeValidator = new(securityConfig, logger);
    private readonly CodeSecurityConfig _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));
    private static readonly ConcurrentDictionary<string, Delegate> _executionCache = new();
    private static readonly ConcurrentDictionary<string, bool> _asyncPatternCache = new();
    /// <summary>
    /// Gets the unique identifier for this executor type.
    /// </summary>
    public string ExecutorType => "AsyncInlineCodeExecutor";
    /// <summary>
    /// Gets the list of programming languages supported by this executor.
    /// </summary>
    public IReadOnlyList<string> SupportedLanguages => ["csharp", "c#"];
    /// <summary>
    /// Gets the maximum degree of parallelism supported by this executor.
    /// </summary>
    public int MaxDegreeOfParallelism => Environment.ProcessorCount * 2;
    /// <summary>
    /// Gets a value indicating whether this executor supports concurrent execution.
    /// </summary>
    public bool SupportsConcurrentExecution => true;

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
        // If this is an async context, use the enhanced async execution
        if (context is AsyncCodeExecutionContext asyncContext)
        {
            var asyncResult = await ExecuteAsyncCodeAsync(asyncContext, cancellationToken);
            return ConvertToCodeExecutionResult(asyncResult);
        }
        // Fall back to basic execution for non-async contexts
        return await ExecuteBasicAsync(context, cancellationToken);
    }
    /// <summary>
    /// Executes asynchronous code with enhanced async context support.
    /// </summary>
    /// <param name="context">The async execution context containing workflow state and configuration.</param>
    /// <param name="cancellationToken">Token that can be used to cancel the code execution.</param>
    /// <returns>A task representing the asynchronous code execution result.</returns>
    public async Task<AsyncCodeExecutionResult> ExecuteAsyncCodeAsync(
        AsyncCodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var operationTracker = new AsyncOperationTracker();
        var performanceMonitor = new AsyncPerformanceMonitor();
        try
        {
            using var scope = context.CreateScope("AsyncCodeExecution");
            scope.Log("Starting async code execution");
            // Get the code to execute
            var code = context.GetInput<string>();
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
            var validationResult = await ValidateAsyncCodeAsync(code, context, cancellationToken);
            if (!validationResult.IsValid)
            {
                return AsyncCodeExecutionResult.CreateAsyncFailure(
                    $"Async code validation failed: {string.Join(", ", validationResult.Errors)}",
                    executionTime: DateTime.UtcNow - startTime);
            }
            // Execute the async code
            var executionResult = await ExecuteAsyncCodeInternalAsync(
                code, context, asyncAnalysis, operationTracker, performanceMonitor, cancellationToken);
            var executionTime = DateTime.UtcNow - startTime;
            var asyncOperations = operationTracker.GetCompletedOperations();
            var performanceMetrics = performanceMonitor.GetMetrics(executionTime);
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
                    asyncOperations,
                    performanceMetrics.PeakConcurrentOperations,
                    asyncAnalysis.ContainsAsyncPatterns,
                    performanceMetrics.TotalCpuTime,
                    performanceMetrics);
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
                    asyncOperations,
                    performanceMetrics);
            }
        }
        catch (OperationCanceledException)
        {
            logger?.LogWarning("Async code execution was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error during async code execution");
            return AsyncCodeExecutionResult.CreateAsyncFailure(
                $"Unexpected error: {ex.Message}",
                ex,
                DateTime.UtcNow - startTime,
                performanceMetrics: performanceMonitor.GetMetrics(DateTime.UtcNow - startTime));
        }
    }
    /// <summary>
    /// Determines whether this executor can handle the specified configuration.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>True if this executor can handle the configuration, false otherwise.</returns>
    public bool CanExecute(CodeExecutionConfig config) => config.Mode == CodeExecutionMode.Inline &&
               SupportedLanguages.Contains(config.Language, StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Determines whether this executor can handle asynchronous execution patterns.
    /// </summary>
    /// <param name="config">The code execution configuration to validate.</param>
    /// <returns>True if this executor supports async execution for the given config.</returns>
    public bool SupportsAsyncExecution(CodeExecutionConfig config)
    {
        if (!CanExecute(config))
            return false;
        // Check if the code contains async patterns
        return AnalyzeAsyncPatterns(config.Code).ContainsAsyncPatterns;
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
            return ValidationResult.Failure([$"This executor only supports {CodeExecutionMode.Inline} mode"]);
        }
        if (!SupportedLanguages.Contains(config.Language, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Failure([$"Unsupported language: {config.Language}"]);
        }
        if (string.IsNullOrEmpty(config.Code))
        {
            return ValidationResult.Failure(["No code provided for execution"]);
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
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            logger?.LogDebug("Starting basic async code execution");
            var code = context.GetInput<string>();
            if (string.IsNullOrEmpty(code))
            {
                return CodeExecutionResult.CreateFailure(
                    "No code provided for execution",
                    executionTime: DateTime.UtcNow - startTime);
            }
            // Compile and execute using Roslyn
            var compilation = CSharpCompilation.Create(
                "BasicDynamicCode",
                [CSharpSyntaxTree.ParseText(GenerateBasicClassCode(code))],
                [
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(CodeExecutionContext).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                ],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            if (!result.Success)
            {
                var errors = string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
                throw new InvalidOperationException($"Compilation failed: {errors}");
            }
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            var type = assembly.GetType("BasicDynamicCode");
            var method = type?.GetMethod("Execute");
            if (method == null)
            {
                throw new InvalidOperationException("Compiled code does not contain Execute method");
            }
            var instance = Activator.CreateInstance(type!);
            var output = method.Invoke(instance, [context]);
            var executionTime = DateTime.UtcNow - startTime;
            return CodeExecutionResult.CreateSuccess(output, executionTime);
        }
        catch (Exception ex)
        {
            return CodeExecutionResult.CreateFailure($"Unexpected error: {ex.Message}", ex, DateTime.UtcNow - startTime);
        }
    }
    private static string GenerateBasicClassCode(string code)
        => @"
            using FlowCore.CodeExecution;
            using System;
            using System.Linq;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            public class BasicDynamicCode
            {
                public object Execute(CodeExecutionContext context)
                {
                     " + code + @"
                }
            }
        ";
    private async Task<ValidationResult> ValidateAsyncCodeAsync(
        string code,
        AsyncCodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Basic security validation
        var namespaceValidation = _namespaceValidator.ValidateNamespaces(code);
        var typeValidation = _typeValidator.ValidateTypes(code);
        var errors = new List<string>();
        if (!namespaceValidation.IsValid) errors.AddRange(namespaceValidation.Errors);
        if (!typeValidation.IsValid) errors.AddRange(typeValidation.Errors);
        // Async-specific validation
        var asyncAnalysis = AnalyzeAsyncPatterns(code);
        if (asyncAnalysis.ContainsUnsafeAsyncPatterns)
        {
            errors.Add("Code contains unsafe async patterns");
        }
        if (asyncAnalysis.AsyncPatternCount > context.AsyncConfig.MaxDegreeOfParallelism * 2)
        {
            errors.Add($"Too many async patterns ({asyncAnalysis.AsyncPatternCount}) for configured parallelism");
        }
        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
    private async Task<ExecutionResult> ExecuteAsyncCodeInternalAsync(
        string code,
        AsyncCodeExecutionContext context,
        AsyncPatternAnalysis analysis,
        AsyncOperationTracker operationTracker,
        AsyncPerformanceMonitor performanceMonitor,
        CancellationToken cancellationToken)
    {
        var operation = operationTracker.StartOperation("CodeExecution");
        try
        {
            // Compile the code using Roslyn
            var compilation = CSharpCompilation.Create(
                "AsyncDynamicCode",
                [CSharpSyntaxTree.ParseText(GenerateAsyncClassCode(code))],
                [
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(CodeExecutionContext).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(AsyncCodeExecutionContext).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                ],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            if (!result.Success)
            {
                var errors = string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
                throw new InvalidOperationException($"Compilation failed: {errors}");
            }
            ms.Seek(0, SeekOrigin.Begin);

            var assembly = Assembly.Load(ms.ToArray());
            var type = assembly.GetType("AsyncDynamicCode");
            var method = (type?.GetMethod("ExecuteAsync"))
                ?? throw new InvalidOperationException("Compiled code does not contain ExecuteAsync method");

            var instance = Activator.CreateInstance(type!);
            var task = method.Invoke(instance, [context]);
            if (task is not Task<object> typedTask)
            {
                throw new InvalidOperationException("ExecuteAsync method does not return Task<object>");
            }
            // Wait for the task with cancellation
            var completedTask = await Task.WhenAny(typedTask, Task.Delay(Timeout.Infinite, cancellationToken));
            if (completedTask != typedTask)
            {
                throw new OperationCanceledException();
            }
            var output = await typedTask;
            operation.MarkCompleted();
            return new ExecutionResult(true, output, null);
        }
        catch (Exception ex)
        {
            operation.MarkCompleted(false, ex.Message);
            return new ExecutionResult(false, null, ex.Message, ex);
        }
    }
    private string GenerateAsyncClassCode(string code)
        => @"
            using FlowCore.CodeExecution;
            using System;
            using System.Linq;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            public class AsyncDynamicCode
            {
                public async Task<object> ExecuteAsync(AsyncCodeExecutionContext context)
                {
                    " + code + @"
                }
            }"
        ;
    private static AsyncPatternAnalysis AnalyzeAsyncPatterns(string code)
    {
        if (string.IsNullOrEmpty(code))
            return new AsyncPatternAnalysis();
        var cacheKey = code.GetHashCode().ToString();
        if (_asyncPatternCache.TryGetValue(cacheKey, out var cachedResult))
        {
            return new AsyncPatternAnalysis { ContainsAsyncPatterns = cachedResult };
        }
        var asyncPatterns = new[]
        {
            new Regex(@"\basync\s+\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bawait\s+\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Task\s*<\s*\w+\s*>", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Task\s*\.", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"ConfigureAwait\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"ContinueWith\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"WhenAll\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"WhenAny\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };
        var unsafePatterns = new[]
        {
            new Regex(@"Task\.Run\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Parallel\.ForEach", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Parallel\.For", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\.Wait\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\.Result\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\.GetAwaiter\(\)\.GetResult\(\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Thread\.Sleep", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Thread\.Join", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"lock\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"Monitor\.Enter", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"SemaphoreSlim\.Wait", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"ManualResetEvent\.Wait", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"AutoResetEvent\.Wait", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bConvert\.FromBase64String", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bEncoding\.GetBytes", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bEncoding\.GetString", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bActivator\.CreateInstance", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bType\.GetType", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\bAssembly\.Load", RegexOptions.Compiled | RegexOptions.IgnoreCase),
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
        _asyncPatternCache.TryAdd(cacheKey, analysis.ContainsAsyncPatterns);
        return analysis;
    }
    private static bool ContainsEncodedCode(string code)
    {
        var base64Matches = Regex.Matches(code, @"[A-Za-z0-9+/]{4,}={0,2}");
        foreach (Match match in base64Matches)
        {
            if (match.Value.Length % 4 == 0 && match.Value.Length > 20) // arbitrary threshold
            {
                try
                {
                    Convert.FromBase64String(match.Value);
                    return true;
                }
                catch
                {
                    // not base64
                }
            }
        }
        return false;
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
    private class ExecutionResult(bool success, object? output, string? errorMessage, Exception? exception = null)
    {
        public bool Success { get; } = success;
        public object? Output { get; } = output;
        public string? ErrorMessage { get; } = errorMessage;
        public Exception? Exception { get; } = exception;
    }
    private class AsyncPatternAnalysis
    {
        public bool ContainsAsyncPatterns { get; set; }
        public int AsyncPatternCount { get; set; }
        public bool ContainsUnsafeAsyncPatterns { get; set; }
        public int UnsafePatternCount { get; set; }
    }
}
/// <summary>
/// Tracks async operations during code execution.
/// </summary>
public class AsyncOperationTracker
{
    private readonly ConcurrentDictionary<string, AsyncOperationInfo> _operations = new();
    private int _operationCounter = 0;
    public AsyncOperationInfo StartOperation(string operationName)
    {
        var operationId = $"{operationName}_{Interlocked.Increment(ref _operationCounter)}";
        var operation = new AsyncOperationInfo(operationId, DateTime.UtcNow);
        _operations.TryAdd(operationId, operation);
        return operation;
    }
    public IReadOnlyList<AsyncOperationInfo> GetCompletedOperations() => _operations.Values.Where(op => op.EndTime.HasValue).ToList();
    public IReadOnlyList<AsyncOperationInfo> GetAllOperations() => _operations.Values.ToList();
}
/// <summary>
/// Monitors performance metrics during async code execution.
/// </summary>
public class AsyncPerformanceMonitor
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private int _peakConcurrentOperations = 0;
    private long _totalOperations = 0;
    private readonly object _lockObject = new();
    public void RecordConcurrentOperations(int count)
    {
        lock (_lockObject)
        {
            _peakConcurrentOperations = Math.Max(_peakConcurrentOperations, count);
            _totalOperations += count;
        }
    }
    public AsyncPerformanceMetrics GetMetrics(TimeSpan totalExecutionTime)
    {
        lock (_lockObject)
        {
            return new AsyncPerformanceMetrics
            {
                TotalAsyncOperations = (int)_totalOperations,
                PeakConcurrentOperations = _peakConcurrentOperations,
                TotalCpuTime = _stopwatch.Elapsed,
                EfficiencyRatio = totalExecutionTime.TotalMilliseconds > 0
                    ? _stopwatch.Elapsed.TotalMilliseconds / totalExecutionTime.TotalMilliseconds
                    : 0.0
            };
        }
    }
}
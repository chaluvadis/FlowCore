namespace FlowCore.CodeExecution;

/// <summary>
/// Enhanced execution context for asynchronous code execution.
/// Provides additional capabilities for async/await patterns and concurrent operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the AsyncCodeExecutionContext.
/// </remarks>
/// <param name="workflowContext">The underlying workflow execution context.</param>
/// <param name="config">The code execution configuration.</param>
/// <param name="serviceProvider">The service provider for dependency injection.</param>
/// <param name="asyncConfig">Configuration specific to async execution.</param>
public class AsyncCodeExecutionContext(
    ExecutionContext workflowContext,
    CodeExecutionConfig config,
    IServiceProvider serviceProvider,
    AsyncExecutionConfig? asyncConfig = null) : CodeExecutionContext(workflowContext, config, serviceProvider), IDisposable
{
    private readonly ConcurrentDictionary<string, object> _asyncState = new();
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    /// <summary>
    /// Gets the async execution configuration.
    /// </summary>
    public AsyncExecutionConfig AsyncConfig { get; } = asyncConfig ?? AsyncExecutionConfig.Default;

    /// <summary>
    /// Gets or sets async-specific state data that persists across await boundaries.
    /// </summary>
    /// <param name="key">The state key.</param>
    /// <returns>The state value.</returns>
    public async Task<T?> GetAsyncStateAsync<T>(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        await _stateLock.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            if (_asyncState.TryGetValue(key, out var value))
            {
                return (T?)value;
            }
            return default;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Sets async-specific state data with thread-safe operations.
    /// </summary>
    /// <param name="key">The state key.</param>
    /// <param name="value">The state value.</param>
    public async Task SetAsyncStateAsync(string key, object? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        await _stateLock.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            if (value == null)
            {
                _asyncState.TryRemove(key, out _);
            }
            else
            {
                _asyncState.AddOrUpdate(key, value, (_, _) => value);
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Executes an async operation with proper timeout and cancellation handling.
    /// </summary>
    /// <typeparam name="T">The result type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <returns>The result of the async operation.</returns>
    public async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? AsyncConfig.DefaultTimeout;

        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            CancellationToken, timeoutCts.Token);

        try
        {
            return await operation(combinedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Async operation timed out after {effectiveTimeout}");
        }
    }

    /// <summary>
    /// Executes multiple async operations concurrently with configurable parallelism.
    /// </summary>
    /// <typeparam name="T">The input type for operations.</typeparam>
    /// <typeparam name="TResult">The result type of operations.</typeparam>
    /// <param name="items">The items to process.</param>
    /// <param name="operation">The async operation to execute for each item.</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of concurrent operations.</param>
    /// <returns>The results of all operations.</returns>
    public async Task<IEnumerable<TResult>> ExecuteConcurrentlyAsync<T, TResult>(
        IEnumerable<T> items,
        Func<T, CancellationToken, Task<TResult>> operation,
        int? maxDegreeOfParallelism = null)
    {
        var degreeOfParallelism = maxDegreeOfParallelism ?? AsyncConfig.MaxDegreeOfParallelism;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = degreeOfParallelism,
            CancellationToken = CancellationToken
        };

        var results = new ConcurrentBag<TResult>();

        await Parallel.ForEachAsync(items, parallelOptions, async (item, ct) =>
        {
            var result = await operation(item, ct).ConfigureAwait(false);
            results.Add(result);
        }).ConfigureAwait(false);

        return results;
    }

    /// <summary>
    /// Creates a scoped async context for nested operations.
    /// </summary>
    /// <param name="scopeName">The name of the scope for tracking.</param>
    /// <returns>A scoped async context.</returns>
    public AsyncCodeExecutionScope CreateScope(string scopeName) => new AsyncCodeExecutionScope(this, scopeName);

    /// <summary>
    /// Logs async operation progress with correlation tracking.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="message">The log message.</param>
    /// <param name="args">Message format arguments.</param>
    public void LogAsyncOperation(string operationName, string message, params object[] args)
    {
        var correlationId = ExecutionId.ToString("N")[..8];
        LogInfo($"[{correlationId}] {operationName}: {message}", args);
    }

    /// <summary>
    /// Disposes resources used by this async context.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources used by this async context.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stateLock?.Dispose();
        }
    }
}

/// <summary>
/// Configuration for asynchronous code execution.
/// </summary>
public class AsyncExecutionConfig
{
    /// <summary>
    /// Gets the default timeout for async operations.
    /// </summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the maximum degree of parallelism for concurrent operations.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets a value indicating whether to enable async state persistence.
    /// </summary>
    public bool EnableAsyncStatePersistence { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of retries for failed async operations.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets a value indicating whether to enable detailed async logging.
    /// </summary>
    public bool EnableDetailedLogging { get; init; } = true;

    /// <summary>
    /// Gets the default async execution configuration.
    /// </summary>
    public static AsyncExecutionConfig Default => new();

    /// <summary>
    /// Creates a high-performance async execution configuration.
    /// </summary>
    public static AsyncExecutionConfig HighPerformance => new()
    {
        DefaultTimeout = TimeSpan.FromMinutes(10),
        MaxDegreeOfParallelism = Environment.ProcessorCount * 2,
        EnableAsyncStatePersistence = false,
        EnableDetailedLogging = false
    };

    /// <summary>
    /// Creates a conservative async execution configuration.
    /// </summary>
    public static AsyncExecutionConfig Conservative => new()
    {
        DefaultTimeout = TimeSpan.FromMinutes(2),
        MaxDegreeOfParallelism = 1,
        MaxRetryAttempts = 5,
        RetryDelay = TimeSpan.FromSeconds(2)
    };
}

/// <summary>
/// Represents a scoped async execution context for tracking nested operations.
/// </summary>
public class AsyncCodeExecutionScope : IDisposable
{
    private readonly AsyncCodeExecutionContext _context;
    private readonly DateTime _startTime;
    private bool _disposed;

    internal AsyncCodeExecutionScope(AsyncCodeExecutionContext context, string scopeName)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        ScopeName = scopeName ?? throw new ArgumentNullException(nameof(scopeName));
        _startTime = DateTime.UtcNow;

        _context.LogAsyncOperation(ScopeName, "Scope started");
    }

    /// <summary>
    /// Gets the name of this scope.
    /// </summary>
    public string ScopeName { get; }

    /// <summary>
    /// Gets the execution duration of this scope.
    /// </summary>
    public TimeSpan Duration => DateTime.UtcNow - _startTime;

    /// <summary>
    /// Logs a message within this scope.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Message format arguments.</param>
    public void Log(string message, params object[] args) => _context.LogAsyncOperation(ScopeName, message, args);

    public void Dispose()
    {
        if (!_disposed)
        {
            _context.LogAsyncOperation(ScopeName, "Scope completed in {Duration}", Duration);
            _disposed = true;
        }
    }
}

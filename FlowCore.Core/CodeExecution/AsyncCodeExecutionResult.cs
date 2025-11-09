namespace FlowCore.CodeExecution;

/// <summary>
/// Represents the result of executing asynchronous configurable code.
/// Extends the basic execution result with async-specific information.
/// </summary>
public class AsyncCodeExecutionResult : CodeExecutionResult
{
    /// <summary>
    /// Gets information about async operations that were executed.
    /// </summary>
    public IReadOnlyList<AsyncOperationInfo> AsyncOperations { get; }

    /// <summary>
    /// Gets the degree of parallelism that was actually used.
    /// </summary>
    public int ActualDegreeOfParallelism { get; }

    /// <summary>
    /// Gets a value indicating whether the code contained async/await patterns.
    /// </summary>
    public bool ContainedAsyncOperations { get; }

    /// <summary>
    /// Gets the total time spent waiting on async operations.
    /// </summary>
    public TimeSpan TotalAsyncWaitTime { get; }

    /// <summary>
    /// Gets performance metrics for the async execution.
    /// </summary>
    public AsyncPerformanceMetrics PerformanceMetrics { get; }

    private AsyncCodeExecutionResult(
        bool success,
        object? output,
        string? errorMessage,
        Exception? exception,
        TimeSpan executionTime,
        IDictionary<string, object>? metadata,
        IReadOnlyList<AsyncOperationInfo> asyncOperations,
        int actualDegreeOfParallelism,
        bool containedAsyncOperations,
        TimeSpan totalAsyncWaitTime,
        AsyncPerformanceMetrics performanceMetrics)
        : base(success, output, errorMessage, exception, executionTime, metadata)
    {
        AsyncOperations = asyncOperations ?? [];
        ActualDegreeOfParallelism = actualDegreeOfParallelism;
        ContainedAsyncOperations = containedAsyncOperations;
        TotalAsyncWaitTime = totalAsyncWaitTime;
        PerformanceMetrics = performanceMetrics ?? new AsyncPerformanceMetrics();
    }

    /// <summary>
    /// Creates a successful async execution result.
    /// </summary>
    /// <param name="output">The output data from the execution.</param>
    /// <param name="executionTime">The time taken for execution.</param>
    /// <param name="metadata">Additional metadata about the execution.</param>
    /// <param name="asyncOperations">Information about async operations performed.</param>
    /// <param name="actualDegreeOfParallelism">The actual degree of parallelism used.</param>
    /// <param name="containedAsyncOperations">Whether async operations were detected.</param>
    /// <param name="totalAsyncWaitTime">Total time spent waiting on async operations.</param>
    /// <param name="performanceMetrics">Performance metrics for the execution.</param>
    /// <returns>A successful async execution result.</returns>
    public static AsyncCodeExecutionResult CreateAsyncSuccess(
        object? output = null,
        TimeSpan? executionTime = null,
        IDictionary<string, object>? metadata = null,
        IReadOnlyList<AsyncOperationInfo>? asyncOperations = null,
        int actualDegreeOfParallelism = 1,
        bool containedAsyncOperations = false,
        TimeSpan? totalAsyncWaitTime = null,
        AsyncPerformanceMetrics? performanceMetrics = null) => new AsyncCodeExecutionResult(
            true,
            output,
            null,
            null,
            executionTime ?? TimeSpan.Zero,
            metadata,
            asyncOperations ?? Array.Empty<AsyncOperationInfo>(),
            actualDegreeOfParallelism,
            containedAsyncOperations,
            totalAsyncWaitTime ?? TimeSpan.Zero,
            performanceMetrics ?? new AsyncPerformanceMetrics());

    /// <summary>
    /// Creates a failed async execution result.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="executionTime">The time taken before failure.</param>
    /// <param name="metadata">Additional metadata about the failure.</param>
    /// <param name="asyncOperations">Information about async operations performed before failure.</param>
    /// <param name="performanceMetrics">Performance metrics captured before failure.</param>
    /// <returns>A failed async execution result.</returns>
    public static AsyncCodeExecutionResult CreateAsyncFailure(
        string? errorMessage = null,
        Exception? exception = null,
        TimeSpan? executionTime = null,
        IDictionary<string, object>? metadata = null,
        IReadOnlyList<AsyncOperationInfo>? asyncOperations = null,
        AsyncPerformanceMetrics? performanceMetrics = null) => new AsyncCodeExecutionResult(
            false,
            null,
            errorMessage,
            exception,
            executionTime ?? TimeSpan.Zero,
            metadata,
            asyncOperations ?? Array.Empty<AsyncOperationInfo>(),
            0,
            false,
            TimeSpan.Zero,
            performanceMetrics ?? new AsyncPerformanceMetrics());
}

/// <summary>
/// Information about an individual async operation that was executed.
/// </summary>
public class AsyncOperationInfo(
    string operationName,
    DateTime startTime,
    DateTime? endTime = null,
    bool success = true,
    string? errorMessage = null,
    IReadOnlyDictionary<string, object>? metadata = null)
{
    /// <summary>
    /// Gets the name or identifier of the async operation.
    /// </summary>
    public string OperationName { get; } = operationName ?? throw new ArgumentNullException(nameof(operationName));

    /// <summary>
    /// Gets the start time of the operation.
    /// </summary>
    public DateTime StartTime { get; } = startTime;

    /// <summary>
    /// Gets the end time of the operation.
    /// </summary>
    public DateTime? EndTime { get; } = endTime;

    /// <summary>
    /// Gets the duration of the operation.
    /// </summary>
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

    /// <summary>
    /// Gets a value indicating whether the operation completed successfully.
    /// </summary>
    public bool Success { get; } = success;

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; } = errorMessage;

    /// <summary>
    /// Gets additional metadata about the operation.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; } = metadata ?? new Dictionary<string, object>();

    /// <summary>
    /// Marks the operation as completed.
    /// </summary>
    /// <param name="success">Whether the operation was successful.</param>
    /// <param name="errorMessage">Error message if the operation failed.</param>
    /// <returns>A new AsyncOperationInfo with completion information.</returns>
    public AsyncOperationInfo MarkCompleted(bool success = true, string? errorMessage = null) => new AsyncOperationInfo(
            OperationName,
            StartTime,
            DateTime.UtcNow,
            success,
            errorMessage,
            Metadata);
}

/// <summary>
/// Performance metrics for async code execution.
/// </summary>
public class AsyncPerformanceMetrics
{
    /// <summary>
    /// Gets the number of async operations executed.
    /// </summary>
    public int TotalAsyncOperations { get; init; }

    /// <summary>
    /// Gets the number of concurrent operations that ran simultaneously.
    /// </summary>
    public int PeakConcurrentOperations { get; init; }

    /// <summary>
    /// Gets the average execution time per async operation.
    /// </summary>
    public TimeSpan AverageOperationTime { get; init; }

    /// <summary>
    /// Gets the total CPU time used during execution.
    /// </summary>
    public TimeSpan TotalCpuTime { get; init; }

    /// <summary>
    /// Gets the memory usage peak during async execution.
    /// </summary>
    public long PeakMemoryUsage { get; init; }

    /// <summary>
    /// Gets the efficiency ratio (CPU time / wall clock time).
    /// </summary>
    public double EfficiencyRatio { get; init; }

    /// <summary>
    /// Gets the number of operations that were retried.
    /// </summary>
    public int RetriedOperations { get; init; }

    /// <summary>
    /// Gets the number of operations that timed out.
    /// </summary>
    public int TimeoutOperations { get; init; }

    /// <summary>
    /// Gets additional performance counters.
    /// </summary>
    public IReadOnlyDictionary<string, double> AdditionalCounters { get; init; }

    public AsyncPerformanceMetrics()
    {
        AdditionalCounters = new Dictionary<string, double>();
    }

    /// <summary>
    /// Creates performance metrics from execution data.
    /// </summary>
    /// <param name="operations">The async operations that were executed.</param>
    /// <param name="totalExecutionTime">The total execution time.</param>
    /// <param name="peakConcurrency">The peak number of concurrent operations.</param>
    /// <param name="additionalCounters">Additional performance counters.</param>
    /// <returns>Performance metrics for the execution.</returns>
    public static AsyncPerformanceMetrics FromExecution(
        IReadOnlyList<AsyncOperationInfo> operations,
        TimeSpan totalExecutionTime,
        int peakConcurrency = 1,
        IReadOnlyDictionary<string, double>? additionalCounters = null)
    {
        var successfulOps = operations.Where(op => op.Success).ToList();
        var avgTime = successfulOps.Count > 0
            ? TimeSpan.FromTicks((long)successfulOps.Average(op => op.Duration.Ticks))
            : TimeSpan.Zero;

        return new AsyncPerformanceMetrics
        {
            TotalAsyncOperations = operations.Count,
            PeakConcurrentOperations = peakConcurrency,
            AverageOperationTime = avgTime,
            TotalCpuTime = TimeSpan.FromTicks(successfulOps.Sum(op => op.Duration.Ticks)),
            EfficiencyRatio = totalExecutionTime.TotalMilliseconds > 0
                ? successfulOps.Sum(op => op.Duration.TotalMilliseconds) / totalExecutionTime.TotalMilliseconds
                : 0.0,
            RetriedOperations = operations.Count(op => op.Metadata.ContainsKey("Retried")),
            TimeoutOperations = operations.Count(op => op.ErrorMessage?.Contains("timeout") == true),
            AdditionalCounters = additionalCounters ?? new Dictionary<string, double>()
        };
    }
}

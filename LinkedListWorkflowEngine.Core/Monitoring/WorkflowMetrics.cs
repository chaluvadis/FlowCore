namespace LinkedListWorkflowEngine.Core.Monitoring;
/// <summary>
/// Collects and exposes metrics for workflow execution monitoring.
/// Provides real-time insights into workflow performance and health.
/// </summary>
public class WorkflowMetrics : IDisposable
{
    private readonly ConcurrentDictionary<string, ExecutionTimeMetric> _executionTimeMetrics = new();
    private readonly ConcurrentDictionary<string, long> _executionCounters = new();
    private readonly ConcurrentDictionary<string, long> _activeWorkflowGauges = new();
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the WorkflowMetrics class.
    /// </summary>
    public WorkflowMetrics()
    {
        // Initialize with empty metrics - will be populated as metrics are recorded
    }
    /// <summary>
    /// Records workflow execution metrics.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="executionTime">The execution time in milliseconds.</param>
    /// <param name="status">The execution status.</param>
    /// <param name="blockCount">The number of blocks executed.</param>
    public void RecordWorkflowExecution(
        string workflowId,
        double executionTime,
        WorkflowStatus status,
        int blockCount)
    {
        var metricKey = $"workflow_{workflowId}";
        // Record execution time
        var timeMetric = _executionTimeMetrics.GetOrAdd(metricKey, _ => new ExecutionTimeMetric());
        timeMetric.RecordExecution(executionTime);
        // Record execution count
        _executionCounters.AddOrUpdate($"workflow_executions_{workflowId}", 1, (_, count) => count + 1);
    }
    /// <summary>
    /// Records block execution metrics.
    /// </summary>
    /// <param name="blockId">The block identifier.</param>
    /// <param name="blockType">The type of block.</param>
    /// <param name="executionTime">The execution time in milliseconds.</param>
    /// <param name="status">The execution status.</param>
    public void RecordBlockExecution(
        string blockId,
        string blockType,
        double executionTime,
        ExecutionStatus status)
    {
        var metricKey = $"block_{blockType}";
        // Record execution time
        var timeMetric = _executionTimeMetrics.GetOrAdd(metricKey, _ => new ExecutionTimeMetric());
        timeMetric.RecordExecution(executionTime);
        // Record execution count
        _executionCounters.AddOrUpdate($"block_executions_{blockType}", 1, (_, count) => count + 1);
    }
    /// <summary>
    /// Records guard evaluation metrics.
    /// </summary>
    /// <param name="guardType">The type of guard.</param>
    /// <param name="severity">The guard severity.</param>
    /// <param name="result">Whether the guard passed or failed.</param>
    /// <param name="evaluationTime">The evaluation time in milliseconds.</param>
    public void RecordGuardEvaluation(
        string guardType,
        GuardSeverity severity,
        bool result,
        double evaluationTime)
    {
        var metricKey = $"guard_{guardType}";
        // Record evaluation time
        var timeMetric = _executionTimeMetrics.GetOrAdd(metricKey, _ => new ExecutionTimeMetric());
        timeMetric.RecordExecution(evaluationTime);
        // Record evaluation count
        _executionCounters.AddOrUpdate($"guard_evaluations_{guardType}", 1, (_, count) => count + 1);
    }
    /// <summary>
    /// Records state persistence metrics.
    /// </summary>
    /// <param name="operation">The operation type (save, load, delete).</param>
    /// <param name="stateSize">The size of the state in bytes.</param>
    /// <param name="duration">The operation duration in milliseconds.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    public void RecordStateOperation(
        string operation,
        long stateSize,
        double duration,
        bool success)
    {
        var metricKey = $"state_{operation}";
        // Record operation time
        var timeMetric = _executionTimeMetrics.GetOrAdd(metricKey, _ => new ExecutionTimeMetric());
        timeMetric.RecordExecution(duration);
        // Record operation count
        _executionCounters.AddOrUpdate($"state_operations_{operation}", 1, (_, count) => count + 1);
    }
    /// <summary>
    /// Updates the active workflow count.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="activeCount">The current active count.</param>
    public void UpdateActiveWorkflowCount(string workflowId, long activeCount)
    {
        _activeWorkflowGauges[$"active_workflows_{workflowId}"] = activeCount;
    }
    /// <summary>
    /// Gets a snapshot of current metrics.
    /// </summary>
    /// <returns>A snapshot of current performance metrics.</returns>
    public WorkflowMetricsSnapshot GetSnapshot()
    {
        return new WorkflowMetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            TotalWorkflowExecutions = _executionCounters.Where(c => c.Key.StartsWith("workflow_executions_")).Sum(c => c.Value),
            TotalBlockExecutions = _executionCounters.Where(c => c.Key.StartsWith("block_executions_")).Sum(c => c.Value),
            TotalGuardEvaluations = _executionCounters.Where(c => c.Key.StartsWith("guard_evaluations_")).Sum(c => c.Value),
            TotalStateOperations = _executionCounters.Where(c => c.Key.StartsWith("state_operations_")).Sum(c => c.Value),
            AverageWorkflowExecutionTime = CalculateAverageExecutionTime("workflow_"),
            AverageBlockExecutionTime = CalculateAverageExecutionTime("block_"),
            AverageGuardEvaluationTime = CalculateAverageExecutionTime("guard_"),
            AverageStateOperationTime = CalculateAverageExecutionTime("state_")
        };
    }
    /// <summary>
    /// Disposes of the metrics collector.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _executionTimeMetrics.Clear();
            _executionCounters.Clear();
            _activeWorkflowGauges.Clear();
            _disposed = true;
        }
    }
    private double CalculateAverageExecutionTime(string metricPrefix)
    {
        var relevantMetrics = _executionTimeMetrics
            .Where(m => m.Key.StartsWith(metricPrefix))
            .Select(m => m.Value)
            .ToList();
        if (!relevantMetrics.Any())
            return 0;
        return relevantMetrics.Sum(m => m.AverageExecutionTime);
    }
}
/// <summary>
/// Internal class for tracking execution time metrics.
/// </summary>
internal class ExecutionTimeMetric
{
    private readonly List<double> _executionTimes = new();
    private double _totalExecutionTime;
    private int _executionCount;
    /// <summary>
    /// Gets the average execution time in milliseconds.
    /// </summary>
    public double AverageExecutionTime => _executionCount > 0 ? _totalExecutionTime / _executionCount : 0;
    /// <summary>
    /// Gets the total number of executions recorded.
    /// </summary>
    public int ExecutionCount => _executionCount;
    /// <summary>
    /// Records an execution time.
    /// </summary>
    /// <param name="executionTime">The execution time in milliseconds.</param>
    public void RecordExecution(double executionTime)
    {
        _executionTimes.Add(executionTime);
        _totalExecutionTime += executionTime;
        _executionCount++;
        // Keep only the last 1000 executions to prevent memory growth
        if (_executionTimes.Count > 1000)
        {
            _executionTimes.RemoveRange(0, _executionTimes.Count - 1000);
        }
    }
}
/// <summary>
/// Snapshot of workflow performance metrics.
/// </summary>
public class WorkflowMetricsSnapshot
{
    /// <summary>
    /// Gets the timestamp when the snapshot was taken.
    /// </summary>
    public DateTime Timestamp { get; internal set; }
    /// <summary>
    /// Gets the total number of workflow executions.
    /// </summary>
    public long TotalWorkflowExecutions { get; internal set; }
    /// <summary>
    /// Gets the total number of block executions.
    /// </summary>
    public long TotalBlockExecutions { get; internal set; }
    /// <summary>
    /// Gets the total number of guard evaluations.
    /// </summary>
    public long TotalGuardEvaluations { get; internal set; }
    /// <summary>
    /// Gets the total number of state operations.
    /// </summary>
    public long TotalStateOperations { get; internal set; }
    /// <summary>
    /// Gets the average workflow execution time in milliseconds.
    /// </summary>
    public double AverageWorkflowExecutionTime { get; internal set; }
    /// <summary>
    /// Gets the average block execution time in milliseconds.
    /// </summary>
    public double AverageBlockExecutionTime { get; internal set; }
    /// <summary>
    /// Gets the average guard evaluation time in milliseconds.
    /// </summary>
    public double AverageGuardEvaluationTime { get; internal set; }
    /// <summary>
    /// Gets the average state operation time in milliseconds.
    /// </summary>
    public double AverageStateOperationTime { get; internal set; }
    /// <summary>
    /// Gets the workflow executions per second.
    /// </summary>
    public double WorkflowsPerSecond => TotalWorkflowExecutions > 0 ?
        TotalWorkflowExecutions / (Timestamp - new DateTime(2024, 1, 1)).TotalSeconds : 0;
    /// <summary>
    /// Gets the block executions per second.
    /// </summary>
    public double BlocksPerSecond => TotalBlockExecutions > 0 ?
        TotalBlockExecutions / (Timestamp - new DateTime(2024, 1, 1)).TotalSeconds : 0;
}
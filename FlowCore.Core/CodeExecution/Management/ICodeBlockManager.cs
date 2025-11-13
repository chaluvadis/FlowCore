namespace FlowCore.CodeExecution.Management;

/// <summary>
/// Interface for managing code blocks and their execution.
/// Provides administrative capabilities for monitoring, configuration, and control.
/// </summary>
public interface ICodeBlockManager
{
    /// <summary>
    /// Gets all registered code blocks.
    /// </summary>
    /// <param name="filter">Optional filter criteria.</param>
    /// <returns>A list of code block information.</returns>
    IEnumerable<CodeBlockInfo> GetCodeBlocks(CodeBlockFilter? filter = null);

    /// <summary>
    /// Gets detailed information about a specific code block.
    /// </summary>
    /// <param name="blockId">The ID of the code block.</param>
    /// <returns>Detailed code block information.</returns>
    CodeBlockDetails? GetCodeBlockDetails(string blockId);

    /// <summary>
    /// Creates a new code block.
    /// </summary>
    /// <param name="definition">The code block definition.</param>
    /// <returns>The created code block information.</returns>
    CodeBlockInfo CreateCodeBlock(CodeBlockDefinition definition);

    /// <summary>
    /// Updates an existing code block.
    /// </summary>
    /// <param name="blockId">The ID of the code block to update.</param>
    /// <param name="definition">The updated code block definition.</param>
    /// <returns>True if the update was successful.</returns>
    bool UpdateCodeBlock(string blockId, CodeBlockDefinition definition);

    /// <summary>
    /// Deletes a code block.
    /// </summary>
    /// <param name="blockId">The ID of the code block to delete.</param>
    /// <returns>True if the deletion was successful.</returns>
    bool DeleteCodeBlock(string blockId);

    /// <summary>
    /// Enables or disables a code block.
    /// </summary>
    /// <param name="blockId">The ID of the code block.</param>
    /// <param name="enabled">Whether to enable or disable the block.</param>
    /// <returns>True if the operation was successful.</returns>
    bool SetCodeBlockEnabled(string blockId, bool enabled);

    /// <summary>
    /// Validates a code block definition.
    /// </summary>
    /// <param name="definition">The definition to validate.</param>
    /// <returns>Validation results.</returns>
    ValidationResult ValidateCodeBlock(CodeBlockDefinition definition);

    /// <summary>
    /// Gets execution statistics for code blocks.
    /// </summary>
    /// <param name="timeRange">The time range for statistics.</param>
    /// <param name="blockIds">Optional specific block IDs to include.</param>
    /// <returns>Execution statistics.</returns>
    CodeBlockExecutionStats GetExecutionStats(
        TimeRange timeRange,
        IEnumerable<string>? blockIds = null);
}

/// <summary>
/// Interface for monitoring code block execution.
/// </summary>
public interface ICodeBlockMonitor
{
    /// <summary>
    /// Starts monitoring code block execution.
    /// </summary>
    /// <param name="config">Monitoring configuration.</param>
    /// <returns>A task representing the monitoring operation.</returns>
    Task StartMonitoringAsync(MonitoringConfiguration config);

    /// <summary>
    /// Stops monitoring code block execution.
    /// </summary>
    /// <returns>A task representing the stop operation.</returns>
    Task StopMonitoringAsync();

    /// <summary>
    /// Gets real-time execution metrics.
    /// </summary>
    /// <returns>Current execution metrics.</returns>
    Task<ExecutionMetrics> GetCurrentMetricsAsync();

    /// <summary>
    /// Gets historical execution data.
    /// </summary>
    /// <param name="timeRange">The time range for data.</param>
    /// <param name="aggregation">How to aggregate the data.</param>
    /// <returns>Historical execution data.</returns>
    Task<IEnumerable<ExecutionDataPoint>> GetHistoricalDataAsync(
        TimeRange timeRange,
        DataAggregation aggregation = DataAggregation.Hourly);

    /// <summary>
    /// Event fired when execution metrics are updated.
    /// </summary>
    event EventHandler<ExecutionMetricsUpdatedEventArgs> MetricsUpdated;

    /// <summary>
    /// Event fired when an execution anomaly is detected.
    /// </summary>
    event EventHandler<ExecutionAnomalyEventArgs> AnomalyDetected;
}

/// <summary>
/// Information about a code block.
/// </summary>
public class CodeBlockInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for the code block.
    /// </summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the code block.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the code block.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the programming language used.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the execution mode.
    /// </summary>
    public CodeExecutionMode Mode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the block is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when the block was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the block was last modified.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Gets or sets the creator of the block.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets tags associated with the block.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the version of the block.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets execution statistics.
    /// </summary>
    public ExecutionSummary ExecutionSummary { get; set; } = new();
}

/// <summary>
/// Detailed information about a code block.
/// </summary>
public class CodeBlockDetails : CodeBlockInfo
{
    /// <summary>
    /// Gets or sets the code execution configuration.
    /// </summary>
    public CodeExecutionConfig? ExecutionConfig { get; set; }

    /// <summary>
    /// Gets or sets the actual source code (for inline mode).
    /// </summary>
    public string? SourceCode { get; set; }

    /// <summary>
    /// Gets or sets security configuration.
    /// </summary>
    public CodeSecurityConfig? SecurityConfig { get; set; }

    /// <summary>
    /// Gets or sets recent execution history.
    /// </summary>
    public List<ExecutionRecord> RecentExecutions { get; set; } = new();

    /// <summary>
    /// Gets or sets performance metrics.
    /// </summary>
    public PerformanceMetrics Performance { get; set; } = new();

    /// <summary>
    /// Gets or sets validation results.
    /// </summary>
    public ValidationResult? LastValidation { get; set; }
}

/// <summary>
/// Definition for creating or updating a code block.
/// </summary>
public class CodeBlockDefinition
{
    /// <summary>
    /// Gets or sets the name of the code block.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the code block.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the code execution configuration.
    /// </summary>
    public CodeExecutionConfig ExecutionConfig { get; set; } = null!;

    /// <summary>
    /// Gets or sets security configuration.
    /// </summary>
    public CodeSecurityConfig? SecurityConfig { get; set; }

    /// <summary>
    /// Gets or sets tags for the code block.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the block should be enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Filter criteria for code blocks.
/// </summary>
public class CodeBlockFilter
{
    /// <summary>
    /// Gets or sets the name pattern to match.
    /// </summary>
    public string? NamePattern { get; set; }

    /// <summary>
    /// Gets or sets the language to filter by.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the execution mode to filter by.
    /// </summary>
    public CodeExecutionMode? Mode { get; set; }

    /// <summary>
    /// Gets or sets whether to include only enabled blocks.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets tags that must be present.
    /// </summary>
    public List<string> RequiredTags { get; set; } = new();

    /// <summary>
    /// Gets or sets the date range for creation time.
    /// </summary>
    public TimeRange? CreatedRange { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of results.
    /// </summary>
    public int? MaxResults { get; set; }
}

/// <summary>
/// Time range specification.
/// </summary>
public class TimeRange
{
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTime End { get; set; }

    /// <summary>
    /// Gets the duration of the time range.
    /// </summary>
    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Creates a time range for the last specified duration.
    /// </summary>
    /// <param name="duration">The duration from now.</param>
    /// <returns>A time range ending now.</returns>
    public static TimeRange LastDuration(TimeSpan duration)
    {
        var now = DateTime.UtcNow;
        return new TimeRange { Start = now - duration, End = now };
    }

    /// <summary>
    /// Creates a time range for today.
    /// </summary>
    /// <returns>A time range for today.</returns>
    public static TimeRange Today()
    {
        var today = DateTime.Today;
        return new TimeRange { Start = today, End = today.AddDays(1) };
    }
}

/// <summary>
/// Execution statistics for code blocks.
/// </summary>
public class CodeBlockExecutionStats
{
    /// <summary>
    /// Gets or sets the time range for these statistics.
    /// </summary>
    public TimeRange TimeRange { get; set; } = new();

    /// <summary>
    /// Gets or sets the total number of executions.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of successful executions.
    /// </summary>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions.
    /// </summary>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// Gets the success rate as a percentage.
    /// </summary>
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;

    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the fastest execution time.
    /// </summary>
    public TimeSpan FastestExecution { get; set; }

    /// <summary>
    /// Gets or sets the slowest execution time.
    /// </summary>
    public TimeSpan SlowestExecution { get; set; }

    /// <summary>
    /// Gets or sets statistics per code block.
    /// </summary>
    public List<BlockExecutionStats> BlockStats { get; set; } = new();
}

/// <summary>
/// Execution statistics for a single code block.
/// </summary>
public class BlockExecutionStats
{
    /// <summary>
    /// Gets or sets the block ID.
    /// </summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the block name.
    /// </summary>
    public string BlockName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of executions.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the success rate.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    public DateTime? LastExecution { get; set; }

    /// <summary>
    /// Gets or sets common error patterns.
    /// </summary>
    public List<ErrorPattern> CommonErrors { get; set; } = new();
}

/// <summary>
/// Summary of execution activity.
/// </summary>
public class ExecutionSummary
{
    /// <summary>
    /// Gets or sets the total number of executions.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    public DateTime? LastExecution { get; set; }

    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the success rate.
    /// </summary>
    public double SuccessRate { get; set; }
}

/// <summary>
/// Performance metrics for a code block.
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Gets or sets the average CPU usage percentage.
    /// </summary>
    public double AverageCpuUsage { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile execution time.
    /// </summary>
    public TimeSpan P95ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets throughput (executions per second).
    /// </summary>
    public double Throughput { get; set; }
}

/// <summary>
/// Record of a code block execution.
/// </summary>
public class ExecutionRecord
{
    /// <summary>
    /// Gets or sets the execution ID.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets when the execution started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets when the execution ended.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the execution duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets whether the execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets additional execution metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Pattern of common errors.
/// </summary>
public class ErrorPattern
{
    /// <summary>
    /// Gets or sets the error message pattern.
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of occurrences.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the first occurrence time.
    /// </summary>
    public DateTime FirstOccurrence { get; set; }

    /// <summary>
    /// Gets or sets the last occurrence time.
    /// </summary>
    public DateTime LastOccurrence { get; set; }
}

/// <summary>
/// Configuration for monitoring code block execution.
/// </summary>
public class MonitoringConfiguration
{
    /// <summary>
    /// Gets or sets the monitoring interval.
    /// </summary>
    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets specific block IDs to monitor.
    /// </summary>
    public List<string> BlockIds { get; set; } = new();

    /// <summary>
    /// Gets or sets metrics to collect.
    /// </summary>
    public MetricsToCollect MetricsToCollect { get; set; } = MetricsToCollect.All;

    /// <summary>
    /// Gets or sets alert thresholds.
    /// </summary>
    public AlertThresholds AlertThresholds { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to enable anomaly detection.
    /// </summary>
    public bool EnableAnomalyDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets the data retention period.
    /// </summary>
    public TimeSpan DataRetentionPeriod { get; set; } = TimeSpan.FromDays(30);
}

/// <summary>
/// Types of metrics to collect.
/// </summary>
[Flags]
public enum MetricsToCollect
{
    /// <summary>
    /// No metrics.
    /// </summary>
    None = 0,

    /// <summary>
    /// Execution counts and rates.
    /// </summary>
    ExecutionCounts = 1,

    /// <summary>
    /// Performance metrics.
    /// </summary>
    Performance = 2,

    /// <summary>
    /// Error rates and patterns.
    /// </summary>
    Errors = 4,

    /// <summary>
    /// Resource usage.
    /// </summary>
    ResourceUsage = 8,

    /// <summary>
    /// All metrics.
    /// </summary>
    All = ExecutionCounts | Performance | Errors | ResourceUsage
}

/// <summary>
/// Alert thresholds for monitoring.
/// </summary>
public class AlertThresholds
{
    /// <summary>
    /// Gets or sets the maximum acceptable error rate (percentage).
    /// </summary>
    public double MaxErrorRate { get; set; } = 10.0;

    /// <summary>
    /// Gets or sets the maximum acceptable execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum acceptable memory usage (bytes).
    /// </summary>
    public long MaxMemoryUsage { get; set; } = 1024 * 1024 * 1024; // 1GB

    /// <summary>
    /// Gets or sets the minimum acceptable throughput (executions per second).
    /// </summary>
    public double MinThroughput { get; set; } = 0.1;
}

/// <summary>
/// Real-time execution metrics.
/// </summary>
public class ExecutionMetrics
{
    /// <summary>
    /// Gets or sets when these metrics were captured.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the current number of active executions.
    /// </summary>
    public int ActiveExecutions { get; set; }

    /// <summary>
    /// Gets or sets the executions per second.
    /// </summary>
    public double ExecutionsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the current error rate (percentage).
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Gets or sets the average execution time for recent executions.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the current memory usage.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the current CPU usage percentage.
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// Gets or sets metrics per code block.
    /// </summary>
    public Dictionary<string, BlockMetrics> BlockMetrics { get; set; } = new();
}

/// <summary>
/// Metrics for a specific code block.
/// </summary>
public class BlockMetrics
{
    /// <summary>
    /// Gets or sets the block ID.
    /// </summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of executions in the current period.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the success rate in the current period.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in the current period.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    public DateTime? LastExecution { get; set; }
}

/// <summary>
/// Historical execution data point.
/// </summary>
public class ExecutionDataPoint
{
    /// <summary>
    /// Gets or sets the timestamp for this data point.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the execution count for this period.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the success rate for this period.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average execution time for this period.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the error count for this period.
    /// </summary>
    public long ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets additional metrics for this period.
    /// </summary>
    public Dictionary<string, double> AdditionalMetrics { get; set; } = new();
}

/// <summary>
/// Data aggregation methods.
/// </summary>
public enum DataAggregation
{
    /// <summary>
    /// No aggregation (raw data).
    /// </summary>
    None,

    /// <summary>
    /// Aggregate by minute.
    /// </summary>
    Minutely,

    /// <summary>
    /// Aggregate by hour.
    /// </summary>
    Hourly,

    /// <summary>
    /// Aggregate by day.
    /// </summary>
    Daily,

    /// <summary>
    /// Aggregate by week.
    /// </summary>
    Weekly
}

/// <summary>
/// Event arguments for metrics updates.
/// </summary>
/// <remarks>
/// Initializes a new instance of the ExecutionMetricsUpdatedEventArgs class.
/// </remarks>
/// <param name="metrics">The updated metrics.</param>
public class ExecutionMetricsUpdatedEventArgs(ExecutionMetrics metrics) : EventArgs
{
    /// <summary>
    /// Gets the updated metrics.
    /// </summary>
    public ExecutionMetrics Metrics { get; } = metrics ?? throw new ArgumentNullException(nameof(metrics));
}

/// <summary>
/// Event arguments for execution anomalies.
/// </summary>
/// <remarks>
/// Initializes a new instance of the ExecutionAnomalyEventArgs class.
/// </remarks>
/// <param name="anomalyType">The type of anomaly.</param>
/// <param name="description">The anomaly description.</param>
/// <param name="blockId">The affected block ID.</param>
/// <param name="data">Additional anomaly data.</param>
public class ExecutionAnomalyEventArgs(
    AnomalyType anomalyType,
    string description,
    string? blockId = null,
    Dictionary<string, object>? data = null) : EventArgs
{
    /// <summary>
    /// Gets the type of anomaly detected.
    /// </summary>
    public AnomalyType AnomalyType { get; } = anomalyType;

    /// <summary>
    /// Gets the description of the anomaly.
    /// </summary>
    public string Description { get; } = description ?? throw new ArgumentNullException(nameof(description));

    /// <summary>
    /// Gets the affected block ID.
    /// </summary>
    public string? BlockId { get; } = blockId;

    /// <summary>
    /// Gets additional anomaly data.
    /// </summary>
    public Dictionary<string, object> Data { get; } = data ?? new Dictionary<string, object>();
}

/// <summary>
/// Types of execution anomalies.
/// </summary>
public enum AnomalyType
{
    /// <summary>
    /// High error rate detected.
    /// </summary>
    HighErrorRate,

    /// <summary>
    /// Slow execution detected.
    /// </summary>
    SlowExecution,

    /// <summary>
    /// High memory usage detected.
    /// </summary>
    HighMemoryUsage,

    /// <summary>
    /// Low throughput detected.
    /// </summary>
    LowThroughput,

    /// <summary>
    /// Execution timeout detected.
    /// </summary>
    ExecutionTimeout,

    /// <summary>
    /// Unusual pattern detected.
    /// </summary>
    UnusualPattern
}

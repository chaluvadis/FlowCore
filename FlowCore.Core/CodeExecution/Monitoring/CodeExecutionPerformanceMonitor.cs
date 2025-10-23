using System.Collections.Concurrent;
using System.Diagnostics;

namespace FlowCore.CodeExecution.Monitoring;

/// <summary>
/// Monitors performance metrics for code execution.
/// Tracks execution times, resource usage, and performance patterns.
/// </summary>
public class CodeExecutionPerformanceMonitor
{
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _blockMetrics = new();
    private readonly ConcurrentQueue<PerformanceSnapshot> _recentSnapshots = new();
    private readonly int _maxSnapshots = 5000;

    /// <summary>
    /// Initializes a new instance of the CodeExecutionPerformanceMonitor.
    /// </summary>
    /// <param name="logger">Optional logger for monitoring operations.</param>
    public CodeExecutionPerformanceMonitor(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Records the start of code execution for performance tracking.
    /// </summary>
    /// <param name="executionId">Unique identifier for the execution.</param>
    /// <param name="blockName">Name of the block being executed.</param>
    /// <param name="executionMode">Mode of code execution (inline/assembly).</param>
    /// <returns>A performance tracking handle for the execution.</returns>
    public PerformanceTrackingHandle StartExecutionTracking(Guid executionId, string blockName, string executionMode)
    {
        var handle = new PerformanceTrackingHandle
        {
            ExecutionId = executionId,
            BlockName = blockName,
            ExecutionMode = executionMode,
            StartTime = DateTime.UtcNow,
            Stopwatch = Stopwatch.StartNew()
        };

        _logger?.LogDebug("Started performance tracking for block {BlockName}, execution {ExecutionId}", blockName, executionId);

        return handle;
    }

    /// <summary>
    /// Records the completion of code execution and calculates metrics.
    /// </summary>
    /// <param name="handle">The performance tracking handle from StartExecutionTracking.</param>
    /// <param name="success">Whether the execution was successful.</param>
    /// <param name="result">The result of the execution (for size calculation).</param>
    /// <returns>Performance metrics for the completed execution.</returns>
    public PerformanceMetrics CompleteExecutionTracking(PerformanceTrackingHandle handle, bool success, object? result = null)
    {
        handle.Stopwatch.Stop();
        var executionTime = handle.Stopwatch.Elapsed;

        var metrics = new PerformanceMetrics
        {
            ExecutionId = handle.ExecutionId,
            BlockName = handle.BlockName,
            ExecutionMode = handle.ExecutionMode,
            StartTime = handle.StartTime,
            EndTime = DateTime.UtcNow,
            ExecutionTime = executionTime,
            Success = success,
            ResultSize = CalculateResultSize(result),
            MemoryUsage = GetCurrentMemoryUsage(),
            CpuUsage = GetCurrentCpuUsage()
        };

        // Store individual execution metrics for historical analysis
        // Note: In a full implementation, this would aggregate metrics properly

        // Add to recent snapshots
        var snapshot = new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            BlockName = handle.BlockName,
            ExecutionMode = handle.ExecutionMode,
            ExecutionTime = executionTime,
            Success = success,
            MemoryUsage = metrics.MemoryUsage
        };

        _recentSnapshots.Enqueue(snapshot);
        while (_recentSnapshots.Count > _maxSnapshots)
        {
            _recentSnapshots.TryDequeue(out _);
        }

        _logger?.LogInformation("Completed performance tracking for block {BlockName}: Success={Success}, Time={ExecutionTime}, Memory={MemoryUsage}MB",
            handle.BlockName, success, executionTime.TotalMilliseconds, metrics.MemoryUsage);

        return metrics;
    }

    /// <summary>
    /// Records a performance event (cache hit, validation, etc.).
    /// </summary>
    /// <param name="eventType">Type of performance event.</param>
    /// <param name="blockName">Name of the block related to the event.</param>
    /// <param name="duration">Duration of the event.</param>
    /// <param name="context">Additional context for the event.</param>
    public void RecordPerformanceEvent(string eventType, string blockName, TimeSpan duration, IDictionary<string, object>? context = null)
    {
        var @event = new PerformanceEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            BlockName = blockName,
            Duration = duration,
            Context = context ?? new Dictionary<string, object>()
        };

        _logger?.LogDebug("Performance event {EventType} for block {BlockName}: {Duration}", eventType, blockName, duration);

        // Could be extended to store performance events for analysis
    }

    /// <summary>
    /// Gets performance metrics for a specific block.
    /// </summary>
    /// <param name="blockName">Name of the block to get metrics for.</param>
    /// <returns>Performance metrics for the block.</returns>
    public BlockPerformanceReport GetBlockPerformanceReport(string blockName)
    {
        // For this simplified implementation, return basic information
        // In a full implementation, this would aggregate multiple execution metrics

        return new BlockPerformanceReport
        {
            BlockName = blockName,
            Message = "Performance monitoring is available but detailed metrics require enhanced implementation"
        };
    }

    /// <summary>
    /// Gets overall performance statistics across all blocks.
    /// </summary>
    /// <returns>Overall performance statistics.</returns>
    public OverallPerformanceReport GetOverallPerformanceReport()
    {
        return new OverallPerformanceReport
        {
            TotalBlocks = _blockMetrics.Count,
            TotalExecutions = _recentSnapshots.Count,
            TotalExecutionTime = _recentSnapshots.Any()
                ? TimeSpan.FromMilliseconds(_recentSnapshots.Sum(s => s.ExecutionTime.TotalMilliseconds))
                : TimeSpan.Zero,
            AverageExecutionTime = _recentSnapshots.Any()
                ? TimeSpan.FromMilliseconds(_recentSnapshots.Average(s => s.ExecutionTime.TotalMilliseconds))
                : TimeSpan.Zero,
            BlocksByPerformance = _blockMetrics
                .OrderByDescending(kvp => kvp.Value.ExecutionTime.TotalMilliseconds)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ExecutionTime),
            RecentSnapshots = _recentSnapshots
                .OrderByDescending(s => s.Timestamp)
                .Take(100)
                .ToList(),
            Message = _blockMetrics.Any() ? null : "No performance data available"
        };
    }

    /// <summary>
    /// Identifies performance bottlenecks and optimization opportunities.
    /// </summary>
    /// <returns>Performance analysis with recommendations.</returns>
    public PerformanceAnalysisResult AnalyzePerformanceBottlenecks()
    {
        if (!_blockMetrics.Any())
        {
            return new PerformanceAnalysisResult
            {
                AnalysisTimestamp = DateTime.UtcNow,
                Message = "Insufficient data for performance analysis"
            };
        }

        var slowBlocks = _blockMetrics
            .Where(kvp => kvp.Value.AverageExecutionTime.TotalMilliseconds > 1000) // Slower than 1 second
            .OrderByDescending(kvp => kvp.Value.AverageExecutionTime.TotalMilliseconds)
            .ToList();

        var highMemoryBlocks = _blockMetrics
            .Where(kvp => kvp.Value.AverageMemoryUsage > 100) // More than 100 MB average
            .OrderByDescending(kvp => kvp.Value.AverageMemoryUsage)
            .ToList();

        var recommendations = new List<string>();

        if (slowBlocks.Any())
        {
            recommendations.Add($"Optimize slow blocks: {string.Join(", ", slowBlocks.Take(3).Select(kvp => kvp.Key))}");
        }

        if (highMemoryBlocks.Any())
        {
            recommendations.Add($"Review memory usage for blocks: {string.Join(", ", highMemoryBlocks.Take(3).Select(kvp => kvp.Key))}");
        }

        // Check for caching opportunities
        var cacheableBlocks = _blockMetrics
            .Where(kvp => kvp.Value.TotalExecutions > 10 && kvp.Value.SuccessRate > 0.9)
            .OrderByDescending(kvp => kvp.Value.TotalExecutions)
            .ToList();

        if (cacheableBlocks.Any())
        {
            recommendations.Add($"Consider caching for frequently executed blocks: {string.Join(", ", cacheableBlocks.Take(3).Select(kvp => kvp.Key))}");
        }

        return new PerformanceAnalysisResult
        {
            AnalysisTimestamp = DateTime.UtcNow,
            SlowBlocks = slowBlocks.Select(kvp => kvp.Key).ToList(),
            HighMemoryBlocks = highMemoryBlocks.Select(kvp => kvp.Key).ToList(),
            CacheableBlocks = cacheableBlocks.Select(kvp => kvp.Key).ToList(),
            Recommendations = recommendations,
            OverallHealth = CalculateOverallHealth()
        };
    }


    private long CalculateResultSize(object? result)
    {
        if (result == null)
            return 0;

        try
        {
            // Estimate size based on type
            if (result is string stringResult)
                return stringResult.Length * 2; // UTF-16 characters

            if (result is byte[] byteArray)
                return byteArray.Length;

            if (result is System.Collections.ICollection collection)
                return collection.Count * 8; // Estimate 8 bytes per item

            // For other types, estimate based on string representation
            return result.ToString()?.Length * 2 ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private double GetCurrentMemoryUsage()
    {
        try
        {
            // Get current process memory usage
            var process = Process.GetCurrentProcess();
            return process.WorkingSet64 / (1024.0 * 1024.0); // Convert to MB
        }
        catch
        {
            return 0;
        }
    }

    private double GetCurrentCpuUsage()
    {
        try
        {
            // CPU usage calculation would require more complex implementation
            // For now, return a placeholder value
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private PerformanceHealth CalculateOverallHealth()
    {
        if (!_recentSnapshots.Any())
            return PerformanceHealth.Unknown;

        var totalExecutions = _recentSnapshots.Count;
        var successfulExecutions = _recentSnapshots.Count(s => s.Success);
        var successRate = totalExecutions > 0 ? (double)successfulExecutions / totalExecutions : 0;

        var averageExecutionTime = _recentSnapshots.Average(s => s.ExecutionTime.TotalMilliseconds);

        // Simple health calculation based on success rate and performance
        if (successRate >= 0.95 && averageExecutionTime < 1000)
            return PerformanceHealth.Excellent;

        if (successRate >= 0.85 && averageExecutionTime < 5000)
            return PerformanceHealth.Good;

        if (successRate >= 0.70 && averageExecutionTime < 10000)
            return PerformanceHealth.Fair;

        return PerformanceHealth.Poor;
    }
}

/// <summary>
/// Handle for tracking performance of a code execution.
/// </summary>
public class PerformanceTrackingHandle
{
    public Guid ExecutionId { get; set; }
    public string BlockName { get; set; } = string.Empty;
    public string ExecutionMode { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public Stopwatch Stopwatch { get; set; } = new Stopwatch();
}

/// <summary>
/// Performance metrics for a single code execution.
/// </summary>
public class PerformanceMetrics
{
    public Guid ExecutionId { get; set; }
    public string BlockName { get; set; } = string.Empty;
    public string ExecutionMode { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public bool Success { get; set; }
    public long ResultSize { get; set; }
    public double MemoryUsage { get; set; }
    public double CpuUsage { get; set; }
}

/// <summary>
/// Snapshot of performance data at a point in time.
/// </summary>
public class PerformanceSnapshot
{
    public DateTime Timestamp { get; set; }
    public string BlockName { get; set; } = string.Empty;
    public string ExecutionMode { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
    public bool Success { get; set; }
    public double MemoryUsage { get; set; }
}

/// <summary>
/// Performance event record.
/// </summary>
public class PerformanceEvent
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string BlockName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public IDictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Performance report for a specific block.
/// </summary>
public class BlockPerformanceReport
{
    public string BlockName { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public TimeSpan MinExecutionTime { get; set; }
    public TimeSpan MaxExecutionTime { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public double AverageMemoryUsage { get; set; }
    public long AverageResultSize { get; set; }
    public TimeSpan LastExecutionTime { get; set; }
    public double SuccessRate { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Overall performance report across all blocks.
/// </summary>
public class OverallPerformanceReport
{
    public int TotalBlocks { get; set; }
    public int TotalExecutions { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public Dictionary<string, TimeSpan> BlocksByPerformance { get; set; } = [];
    public IReadOnlyList<PerformanceSnapshot> RecentSnapshots { get; set; } = [];
    public string? Message { get; set; }
}

/// <summary>
/// Analysis result for performance bottlenecks and optimization opportunities.
/// </summary>
public class PerformanceAnalysisResult
{
    public DateTime AnalysisTimestamp { get; set; }
    public IReadOnlyList<string> SlowBlocks { get; set; } = [];
    public IReadOnlyList<string> HighMemoryBlocks { get; set; } = [];
    public IReadOnlyList<string> CacheableBlocks { get; set; } = [];
    public IReadOnlyList<string> Recommendations { get; set; } = [];
    public PerformanceHealth OverallHealth { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Overall health assessment of the code execution system.
/// </summary>
public enum PerformanceHealth
{
    Unknown,
    Excellent,
    Good,
    Fair,
    Poor
}
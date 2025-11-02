namespace FlowCore.CodeExecution.Management;

/// <summary>
/// Basic implementation of code block execution monitoring.
/// Provides real-time monitoring and alerting capabilities.
/// </summary>
public class BasicCodeBlockMonitor : ICodeBlockMonitor, IDisposable
{
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, BlockMetrics> _blockMetrics = new();
    private readonly ConcurrentQueue<ExecutionDataPoint> _historicalData = new();
    private Timer? _monitoringTimer;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;
    private MonitoringConfiguration? _configuration;
    private bool _isMonitoring;
    private readonly object _metricsLock = new();
    private DateTime _lastMetricsUpdate = DateTime.UtcNow;

    /// <summary>
    /// Event fired when execution metrics are updated.
    /// </summary>
    public event EventHandler<ExecutionMetricsUpdatedEventArgs>? MetricsUpdated;

    /// <summary>
    /// Event fired when an execution anomaly is detected.
    /// </summary>
    public event EventHandler<ExecutionAnomalyEventArgs>? AnomalyDetected;

    /// <summary>
    /// Initializes a new instance of the BasicCodeBlockMonitor.
    /// </summary>
    /// <param name="logger">Optional logger for monitoring operations.</param>
    public BasicCodeBlockMonitor(ILogger? logger = null)
    {
        _logger = logger;

        // Initialize performance counters if available and on Windows
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to initialize performance counters");
            }
        }
        else
        {
            _logger?.LogInformation("Performance counters are not supported on this platform. Using fallback metrics.");
        }
    }

    /// <summary>
    /// Starts monitoring code block execution.
    /// </summary>
    /// <param name="config">Monitoring configuration.</param>
    /// <returns>A task representing the monitoring operation.</returns>
    public async Task StartMonitoringAsync(MonitoringConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (_isMonitoring)
        {
            throw new InvalidOperationException("Monitoring is already active");
        }

        try
        {
            _configuration = config;
            _isMonitoring = true;

            _logger?.LogInformation("Starting code block execution monitoring with interval {Interval}",
                config.MonitoringInterval);

            // Start the monitoring timer
            _monitoringTimer = new Timer(MonitoringCallback, null, TimeSpan.Zero, config.MonitoringInterval);

            _logger?.LogInformation("Code block monitoring started successfully");
            await Task.CompletedTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start monitoring");
            _isMonitoring = false;
            throw;
        }
    }

    /// <summary>
    /// Stops monitoring code block execution.
    /// </summary>
    /// <returns>A task representing the stop operation.</returns>
    public async Task StopMonitoringAsync()
    {
        if (!_isMonitoring)
        {
            return;
        }

        try
        {
            _logger?.LogInformation("Stopping code block execution monitoring");

            _isMonitoring = false;
            _monitoringTimer?.Dispose();

            _logger?.LogInformation("Code block monitoring stopped successfully");
            await Task.CompletedTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop monitoring");
            throw;
        }
    }

    /// <summary>
    /// Gets real-time execution metrics.
    /// </summary>
    /// <returns>Current execution metrics.</returns>
    public async Task<ExecutionMetrics> GetCurrentMetricsAsync()
    {
        try
        {
            return await GetCurrentMetricsInternalAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get current metrics");
            await Task.CompletedTask.ConfigureAwait(false);
            return new ExecutionMetrics();
        }
    }

    private async Task<ExecutionMetrics> GetCurrentMetricsInternalAsync()
    {
        lock (_metricsLock)
        {
            var metrics = new ExecutionMetrics
            {
                Timestamp = DateTime.UtcNow,
                ActiveExecutions = _blockMetrics.Values.Sum(bm => bm.ExecutionCount),
                BlockMetrics = new Dictionary<string, BlockMetrics>(_blockMetrics)
            };

            PopulateOverallMetrics(metrics);
            PopulateSystemMetrics(metrics);

            return metrics;
        }
    }

    private void PopulateOverallMetrics(ExecutionMetrics metrics)
    {
        if (_blockMetrics.Values.Any())
        {
            metrics.ExecutionsPerSecond = CalculateExecutionsPerSecond();
            metrics.ErrorRate = CalculateOverallErrorRate();
            metrics.AverageExecutionTime = CalculateOverallAverageExecutionTime();
        }
    }

    private void PopulateSystemMetrics(ExecutionMetrics metrics)
    {
        metrics.MemoryUsage = GetMemoryUsage();
        metrics.CpuUsage = GetCpuUsage();
    }

    /// <summary>
    /// Gets historical execution data.
    /// </summary>
    /// <param name="timeRange">The time range for data.</param>
    /// <param name="aggregation">How to aggregate the data.</param>
    /// <returns>Historical execution data.</returns>
    public async Task<IEnumerable<ExecutionDataPoint>> GetHistoricalDataAsync(
        TimeRange timeRange,
        DataAggregation aggregation = DataAggregation.Hourly)
    {
        try
        {
            _logger?.LogDebug("Getting historical data for range {Start} to {End} with {Aggregation} aggregation",
                timeRange.Start, timeRange.End, aggregation);

            var relevantData = GetRelevantHistoricalData(timeRange);
            var aggregatedData = AggregateData(relevantData, aggregation);

            _logger?.LogDebug("Retrieved {Count} historical data points", aggregatedData.Count());

            await Task.CompletedTask.ConfigureAwait(false);
            return aggregatedData;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get historical data");
            throw;
        }
    }

    private List<ExecutionDataPoint> GetRelevantHistoricalData(TimeRange timeRange) => [.. _historicalData
            .Where(dp => dp.Timestamp >= timeRange.Start && dp.Timestamp <= timeRange.End)
            .OrderBy(dp => dp.Timestamp)];

    /// <summary>
    /// Records execution metrics for a code block.
    /// </summary>
    /// <param name="blockId">The block ID.</param>
    /// <param name="executionTime">The execution time.</param>
    /// <param name="success">Whether the execution was successful.</param>
    public async Task RecordExecutionAsync(string blockId, TimeSpan executionTime, bool success)
    {
        if (string.IsNullOrEmpty(blockId))
        {
            return;
        }

        try
        {
            UpdateBlockMetrics(blockId, executionTime, success);

            // Check for anomalies
            if (_configuration?.EnableAnomalyDetection == true)
            {
                await CheckForAnomaliesAsync(blockId, executionTime, success).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to record execution for block {BlockId}", blockId);
        }
    }

    private void UpdateBlockMetrics(string blockId, TimeSpan executionTime, bool success)
    {
        lock (_metricsLock)
        {
            if (!_blockMetrics.TryGetValue(blockId, out var metrics))
            {
                metrics = new BlockMetrics { BlockId = blockId };
                _blockMetrics.TryAdd(blockId, metrics);
            }

            UpdateExecutionMetrics(metrics, executionTime, success);
        }
    }

    private static void UpdateExecutionMetrics(BlockMetrics metrics, TimeSpan executionTime, bool success)
    {
        var newSuccessRate = success ? 100.0 : 0.0;
        if (metrics.ExecutionCount == 0)
        {
            metrics.SuccessRate = newSuccessRate;
        }
        else
        {
            metrics.SuccessRate = (metrics.SuccessRate * 0.9) + (newSuccessRate * 0.1);
        }
        metrics.ExecutionCount++;
        metrics.LastExecution = DateTime.UtcNow;

        // Update average execution time (exponential moving average)
        if (metrics.AverageExecutionTime == TimeSpan.Zero)
        {
            metrics.AverageExecutionTime = executionTime;
        }
        else
        {
            var avgTicks = (long)(metrics.AverageExecutionTime.Ticks * 0.9 + executionTime.Ticks * 0.1);
            metrics.AverageExecutionTime = TimeSpan.FromTicks(avgTicks);
        }
    }

    private void MonitoringCallback(object? state)
    {
        if (!_isMonitoring || _configuration == null)
        {
            return;
        }

        try
        {
            var currentMetrics = GetCurrentMetricsAsync().Result;
            StoreHistoricalDataPoint(currentMetrics);
            NotifyMetricsUpdated(currentMetrics);
            CheckSystemAnomalies(currentMetrics);

            _lastMetricsUpdate = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in monitoring callback");
        }
    }

    private void StoreHistoricalDataPoint(ExecutionMetrics metrics)
    {
        var dataPoint = CreateHistoricalDataPoint(metrics);
        _historicalData.Enqueue(dataPoint);
        LimitHistoricalDataSize();
    }

    private void NotifyMetricsUpdated(ExecutionMetrics metrics) => FireMetricsUpdatedEvent(metrics);

    private ExecutionDataPoint CreateHistoricalDataPoint(ExecutionMetrics metrics) => new ExecutionDataPoint
    {
        Timestamp = DateTime.UtcNow,
        ExecutionCount = _blockMetrics.Values.Sum(bm => bm.ExecutionCount),
        SuccessRate = metrics.ErrorRate > 0 ? 100 - metrics.ErrorRate : 100,
        AverageExecutionTime = metrics.AverageExecutionTime,
        ErrorCount = CalculateTotalErrorCount(),
        AdditionalMetrics = CreateAdditionalMetrics(metrics)
    };

    private static Dictionary<string, double> CreateAdditionalMetrics(ExecutionMetrics metrics) => new Dictionary<string, double>
    {
        ["CpuUsage"] = metrics.CpuUsage,
        ["MemoryUsage"] = metrics.MemoryUsage,
        ["ActiveExecutions"] = metrics.ActiveExecutions
    };

    private void LimitHistoricalDataSize()
    {
        while (_historicalData.Count > 10000)
        {
            _historicalData.TryDequeue(out _);
        }
    }

    private void FireMetricsUpdatedEvent(ExecutionMetrics metrics) => MetricsUpdated?.Invoke(this, new ExecutionMetricsUpdatedEventArgs(metrics));

    /// <summary>
    /// Raises an anomaly event if anomaly detection is enabled.
    /// </summary>
    private void RaiseAnomaly(AnomalyType type, string message, string? blockId = null, Dictionary<string, object>? data = null)
    {
        if (_configuration?.EnableAnomalyDetection == true)
        {
            AnomalyDetected?.Invoke(this, new ExecutionAnomalyEventArgs(type, message, blockId, data ?? new Dictionary<string, object>()));
        }
    }

    private async Task CheckForAnomaliesAsync(string blockId, TimeSpan executionTime, bool success)
    {
        if (_configuration?.AlertThresholds == null)
        {
            return;
        }

        CheckAnomalyThresholds(blockId, new AnomalyCheckData
        {
            ExecutionTime = executionTime,
            Success = success,
            Metrics = null
        });

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private void CheckSystemAnomalies(ExecutionMetrics metrics)
    {
        if (_configuration?.AlertThresholds == null)
        {
            return;
        }

        CheckAnomalyThresholds(null, new AnomalyCheckData
        {
            ExecutionTime = TimeSpan.Zero,
            Success = true,
            Metrics = metrics
        });
    }

    private void CheckAnomalyThresholds(string? blockId, AnomalyCheckData data)
    {
        if (_configuration?.AlertThresholds == null)
        {
            return;
        }

        try
        {
            CheckBlockAnomalies(blockId, data);
            CheckSystemAnomalies(data);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking anomalies for block {BlockId}", blockId);
        }
    }

    private void CheckBlockAnomalies(string? blockId, AnomalyCheckData data)
    {
        if (blockId == null)
        {
            return;
        }

        var thresholds = _configuration!.AlertThresholds;

        // Check execution time
        if (data.ExecutionTime > thresholds.MaxExecutionTime)
        {
            RaiseAnomaly(AnomalyType.SlowExecution,
                $"Execution time {data.ExecutionTime} exceeded threshold {thresholds.MaxExecutionTime}",
                blockId,
                new Dictionary<string, object>
                {
                    ["ExecutionTime"] = data.ExecutionTime,
                    ["Threshold"] = thresholds.MaxExecutionTime
                });
        }

        // Check error rate
        if (_blockMetrics.TryGetValue(blockId, out var metrics) &&
            metrics.SuccessRate < (100 - thresholds.MaxErrorRate))
        {
            RaiseAnomaly(AnomalyType.HighErrorRate,
                $"Error rate {100 - metrics.SuccessRate:F1}% exceeded threshold {thresholds.MaxErrorRate}%",
                blockId,
                new Dictionary<string, object>
                {
                    ["ErrorRate"] = 100 - metrics.SuccessRate,
                    ["Threshold"] = thresholds.MaxErrorRate
                });
        }
    }

    private void CheckSystemAnomalies(AnomalyCheckData data)
    {
        if (data.Metrics == null)
        {
            return;
        }

        var thresholds = _configuration!.AlertThresholds;

        // Check memory usage
        if (data.Metrics.MemoryUsage > thresholds.MaxMemoryUsage)
        {
            RaiseAnomaly(AnomalyType.HighMemoryUsage,
                $"Memory usage {data.Metrics.MemoryUsage:N0} bytes exceeded threshold {thresholds.MaxMemoryUsage:N0} bytes",
                data: new Dictionary<string, object>
                {
                    ["MemoryUsage"] = data.Metrics.MemoryUsage,
                    ["Threshold"] = thresholds.MaxMemoryUsage
                });
        }

        // Check throughput
        if (data.Metrics.ExecutionsPerSecond < thresholds.MinThroughput)
        {
            RaiseAnomaly(AnomalyType.LowThroughput,
                $"Throughput {data.Metrics.ExecutionsPerSecond:F2} executions/sec below threshold {thresholds.MinThroughput}",
                data: new Dictionary<string, object>
                {
                    ["Throughput"] = data.Metrics.ExecutionsPerSecond,
                    ["Threshold"] = thresholds.MinThroughput
                });
        }
    }

    sealed class AnomalyCheckData
    {
        public TimeSpan ExecutionTime { get; set; }
        public bool Success { get; set; }
        public ExecutionMetrics? Metrics { get; set; }
    }

    private double CalculateExecutionsPerSecond()
    {
        var timeSinceLastUpdate = DateTime.UtcNow - _lastMetricsUpdate;
        if (timeSinceLastUpdate.TotalSeconds < 1)
        {
            return 0;
        }

        return _blockMetrics.Values.Sum(bm => bm.ExecutionCount) / timeSinceLastUpdate.TotalSeconds;
    }

    private double CalculateOverallErrorRate() =>
        _blockMetrics.Values.Any() ? _blockMetrics.Values.Average(bm => 100 - bm.SuccessRate) : 0;

    private TimeSpan CalculateOverallAverageExecutionTime() =>
        _blockMetrics.Values.Any()
            ? TimeSpan.FromTicks((long)_blockMetrics.Values.Average(bm => bm.AverageExecutionTime.Ticks))
            : TimeSpan.Zero;

    private long GetMemoryUsage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _memoryCounter != null)
        {
            return (long)((8192 - _memoryCounter.NextValue()) * 1024 * 1024);
        }
        return GC.GetTotalMemory(false);
    }

    private double GetCpuUsage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _cpuCounter != null)
        {
            return _cpuCounter.NextValue();
        }
        return 0.0;
    }

    private long CalculateTotalErrorCount() => _blockMetrics.Values.Sum(bm => (long)(bm.ExecutionCount * (100 - bm.SuccessRate) / 100));

    private IEnumerable<ExecutionDataPoint> AggregateData(
        List<ExecutionDataPoint> data,
        DataAggregation aggregation) =>
        !data.Any()
            ? Enumerable.Empty<ExecutionDataPoint>()
            : aggregation switch
            {
                DataAggregation.None => data,
                DataAggregation.Minutely => AggregateByTimeSpan(data, TimeSpan.FromMinutes(1)),
                DataAggregation.Hourly => AggregateByTimeSpan(data, TimeSpan.FromHours(1)),
                DataAggregation.Daily => AggregateByTimeSpan(data, TimeSpan.FromDays(1)),
                DataAggregation.Weekly => AggregateByTimeSpan(data, TimeSpan.FromDays(7)),
                _ => data
            };

    private IEnumerable<ExecutionDataPoint> AggregateByTimeSpan(
        List<ExecutionDataPoint> data,
        TimeSpan interval) => data
            .GroupBy(dp => GetAggregationKey(dp.Timestamp, interval))
            .Select(CreateAggregatedDataPoint)
            .OrderBy(dp => dp.Timestamp);

    private static DateTime GetAggregationKey(DateTime timestamp, TimeSpan interval)
    {
        var ticks = timestamp.Ticks / interval.Ticks * interval.Ticks;
        return new DateTime(ticks);
    }

    private ExecutionDataPoint CreateAggregatedDataPoint(IGrouping<DateTime, ExecutionDataPoint> group) => new ExecutionDataPoint
    {
        Timestamp = group.Key,
        ExecutionCount = group.Sum(dp => dp.ExecutionCount),
        SuccessRate = group.Average(dp => dp.SuccessRate),
        AverageExecutionTime = TimeSpan.FromTicks((long)group.Average(dp => dp.AverageExecutionTime.Ticks)),
        ErrorCount = group.Sum(dp => dp.ErrorCount),
        AdditionalMetrics = AggregateAdditionalMetrics(group)
    };

    private static Dictionary<string, double> AggregateAdditionalMetrics(IGrouping<DateTime, ExecutionDataPoint> group) => group
            .SelectMany(dp => dp.AdditionalMetrics)
            .GroupBy(kvp => kvp.Key)
            .ToDictionary(g => g.Key, g => g.Average(kvp => kvp.Value));

    public void Dispose()
    {
        StopMonitoringAsync().Wait();
        _monitoringTimer?.Dispose();
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();
    }
}

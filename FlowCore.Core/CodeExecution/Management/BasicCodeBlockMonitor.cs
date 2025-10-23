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
    private readonly Timer? _monitoringTimer;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;
    private MonitoringConfiguration? _configuration;
    private bool _isMonitoring = false;
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

        // Initialize performance counters if available
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

    /// <summary>
    /// Starts monitoring code block execution.
    /// </summary>
    /// <param name="config">Monitoring configuration.</param>
    /// <returns>A task representing the monitoring operation.</returns>
    public async Task StartMonitoringAsync(MonitoringConfiguration config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        if (_isMonitoring)
            throw new InvalidOperationException("Monitoring is already active");

        try
        {
            _configuration = config;
            _isMonitoring = true;

            _logger?.LogInfo("Starting code block execution monitoring with interval {Interval}",
                config.MonitoringInterval);

            // Start the monitoring timer
            var timer = new Timer(MonitoringCallback, null, TimeSpan.Zero, config.MonitoringInterval);

            _logger?.LogInfo("Code block monitoring started successfully");
            await Task.CompletedTask;
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
            return;

        try
        {
            _logger?.LogInfo("Stopping code block execution monitoring");

            _isMonitoring = false;
            _monitoringTimer?.Dispose();

            _logger?.LogInfo("Code block monitoring stopped successfully");
            await Task.CompletedTask;
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
            lock (_metricsLock)
            {
                var metrics = new ExecutionMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    ActiveExecutions = _blockMetrics.Values.Sum(bm => bm.ExecutionCount),
                    BlockMetrics = new Dictionary<string, BlockMetrics>(_blockMetrics)
                };

                // Calculate overall metrics
                if (_blockMetrics.Values.Any())
                {
                    metrics.ExecutionsPerSecond = CalculateExecutionsPerSecond();
                    metrics.ErrorRate = CalculateOverallErrorRate();
                    metrics.AverageExecutionTime = CalculateOverallAverageExecutionTime();
                }

                // Get system metrics
                metrics.MemoryUsage = GetMemoryUsage();
                metrics.CpuUsage = GetCpuUsage();

                return metrics;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get current metrics");
            await Task.CompletedTask;
            return new ExecutionMetrics();
        }
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

            var relevantData = _historicalData
                .Where(dp => dp.Timestamp >= timeRange.Start && dp.Timestamp <= timeRange.End)
                .OrderBy(dp => dp.Timestamp)
                .ToList();

            var aggregatedData = AggregateData(relevantData, aggregation);

            _logger?.LogDebug("Retrieved {Count} historical data points", aggregatedData.Count());

            await Task.CompletedTask;
            return aggregatedData;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get historical data");
            throw;
        }
    }

    /// <summary>
    /// Records execution metrics for a code block.
    /// </summary>
    /// <param name="blockId">The block ID.</param>
    /// <param name="executionTime">The execution time.</param>
    /// <param name="success">Whether the execution was successful.</param>
    public async Task RecordExecutionAsync(string blockId, TimeSpan executionTime, bool success)
    {
        if (string.IsNullOrEmpty(blockId))
            return;

        try
        {
            lock (_metricsLock)
            {
                if (!_blockMetrics.TryGetValue(blockId, out var metrics))
                {
                    metrics = new BlockMetrics { BlockId = blockId };
                    _blockMetrics.TryAdd(blockId, metrics);
                }

                // Update metrics
                metrics.ExecutionCount++;
                metrics.LastExecution = DateTime.UtcNow;

                // Update success rate (simple moving average over last period)
                var newSuccessRate = success ? 100.0 : 0.0;
                metrics.SuccessRate = (metrics.SuccessRate * 0.9) + (newSuccessRate * 0.1);

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

            // Check for anomalies
            if (_configuration?.EnableAnomalyDetection == true)
            {
                await CheckForAnomaliesAsync(blockId, executionTime, success);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to record execution for block {BlockId}", blockId);
        }
    }

    private void MonitoringCallback(object? state)
    {
        if (!_isMonitoring || _configuration == null)
            return;

        try
        {
            // Collect current metrics
            var currentMetrics = GetCurrentMetricsAsync().Result;

            // Store historical data point
            var dataPoint = new ExecutionDataPoint
            {
                Timestamp = DateTime.UtcNow,
                ExecutionCount = _blockMetrics.Values.Sum(bm => bm.ExecutionCount),
                SuccessRate = currentMetrics.ErrorRate > 0 ? 100 - currentMetrics.ErrorRate : 100,
                AverageExecutionTime = currentMetrics.AverageExecutionTime,
                ErrorCount = CalculateTotalErrorCount(),
                AdditionalMetrics = new Dictionary<string, double>
                {
                    ["CpuUsage"] = currentMetrics.CpuUsage,
                    ["MemoryUsage"] = currentMetrics.MemoryUsage,
                    ["ActiveExecutions"] = currentMetrics.ActiveExecutions
                }
            };

            _historicalData.Enqueue(dataPoint);

            // Limit historical data size
            while (_historicalData.Count > 10000)
            {
                _historicalData.TryDequeue(out _);
            }

            // Fire metrics updated event
            MetricsUpdated?.Invoke(this, new ExecutionMetricsUpdatedEventArgs(currentMetrics));

            // Check for system-level anomalies
            CheckSystemAnomalies(currentMetrics);

            _lastMetricsUpdate = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in monitoring callback");
        }
    }

    private async Task CheckForAnomaliesAsync(string blockId, TimeSpan executionTime, bool success)
    {
        if (_configuration?.AlertThresholds == null)
            return;

        try
        {
            var thresholds = _configuration.AlertThresholds;

            // Check execution time
            if (executionTime > thresholds.MaxExecutionTime)
            {
                AnomalyDetected?.Invoke(this, new ExecutionAnomalyEventArgs(
                    AnomalyType.SlowExecution,
                    $"Execution time {executionTime} exceeded threshold {thresholds.MaxExecutionTime}",
                    blockId,
                    new Dictionary<string, object>
                    {
                        ["ExecutionTime"] = executionTime,
                        ["Threshold"] = thresholds.MaxExecutionTime
                    }));
            }

            // Check error rate for this block
            if (_blockMetrics.TryGetValue(blockId, out var metrics))
            {
                if (metrics.SuccessRate < (100 - thresholds.MaxErrorRate))
                {
                    AnomalyDetected?.Invoke(this, new ExecutionAnomalyEventArgs(
                        AnomalyType.HighErrorRate,
                        $"Error rate {100 - metrics.SuccessRate:F1}% exceeded threshold {thresholds.MaxErrorRate}%",
                        blockId,
                        new Dictionary<string, object>
                        {
                            ["ErrorRate"] = 100 - metrics.SuccessRate,
                            ["Threshold"] = thresholds.MaxErrorRate
                        }));
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking anomalies for block {BlockId}", blockId);
        }
    }

    private void CheckSystemAnomalies(ExecutionMetrics metrics)
    {
        if (_configuration?.AlertThresholds == null)
            return;

        try
        {
            var thresholds = _configuration.AlertThresholds;

            // Check memory usage
            if (metrics.MemoryUsage > thresholds.MaxMemoryUsage)
            {
                AnomalyDetected?.Invoke(this, new ExecutionAnomalyEventArgs(
                    AnomalyType.HighMemoryUsage,
                    $"Memory usage {metrics.MemoryUsage:N0} bytes exceeded threshold {thresholds.MaxMemoryUsage:N0} bytes",
                    data: new Dictionary<string, object>
                    {
                        ["MemoryUsage"] = metrics.MemoryUsage,
                        ["Threshold"] = thresholds.MaxMemoryUsage
                    }));
            }

            // Check throughput
            if (metrics.ExecutionsPerSecond < thresholds.MinThroughput)
            {
                AnomalyDetected?.Invoke(this, new ExecutionAnomalyEventArgs(
                    AnomalyType.LowThroughput,
                    $"Throughput {metrics.ExecutionsPerSecond:F2} executions/sec below threshold {thresholds.MinThroughput}",
                    data: new Dictionary<string, object>
                    {
                        ["Throughput"] = metrics.ExecutionsPerSecond,
                        ["Threshold"] = thresholds.MinThroughput
                    }));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking system anomalies");
        }
    }

    private double CalculateExecutionsPerSecond()
    {
        var timeSinceLastUpdate = DateTime.UtcNow - _lastMetricsUpdate;
        if (timeSinceLastUpdate.TotalSeconds < 1)
            return 0;

        var totalExecutions = _blockMetrics.Values.Sum(bm => bm.ExecutionCount);
        return totalExecutions / timeSinceLastUpdate.TotalSeconds;
    }

    private double CalculateOverallErrorRate()
    {
        if (!_blockMetrics.Values.Any())
            return 0;

        return _blockMetrics.Values.Average(bm => 100 - bm.SuccessRate);
    }

    private TimeSpan CalculateOverallAverageExecutionTime()
    {
        if (!_blockMetrics.Values.Any())
            return TimeSpan.Zero;

        var avgTicks = (long)_blockMetrics.Values.Average(bm => bm.AverageExecutionTime.Ticks);
        return TimeSpan.FromTicks(avgTicks);
    }

    private long GetMemoryUsage()
    {
        try
        {
            if (_memoryCounter != null)
            {
                var availableMB = _memoryCounter.NextValue();
                // Convert to used memory (rough estimate)
                return (long)((8192 - availableMB) * 1024 * 1024); // Assume 8GB total
            }

            // Fallback to GC memory
            return GC.GetTotalMemory(false);
        }
        catch
        {
            return GC.GetTotalMemory(false);
        }
    }

    private double GetCpuUsage()
    {
        try
        {
            return _cpuCounter?.NextValue() ?? 0.0;
        }
        catch
        {
            return 0.0;
        }
    }

    private long CalculateTotalErrorCount() => _blockMetrics.Values.Sum(bm => (long)(bm.ExecutionCount * (100 - bm.SuccessRate) / 100));

    private IEnumerable<ExecutionDataPoint> AggregateData(
        List<ExecutionDataPoint> data,
        DataAggregation aggregation)
    {
        if (!data.Any())
            return Enumerable.Empty<ExecutionDataPoint>();

        return aggregation switch
        {
            DataAggregation.None => data,
            DataAggregation.Minutely => AggregateByTimeSpan(data, TimeSpan.FromMinutes(1)),
            DataAggregation.Hourly => AggregateByTimeSpan(data, TimeSpan.FromHours(1)),
            DataAggregation.Daily => AggregateByTimeSpan(data, TimeSpan.FromDays(1)),
            DataAggregation.Weekly => AggregateByTimeSpan(data, TimeSpan.FromDays(7)),
            _ => data
        };
    }

    private IEnumerable<ExecutionDataPoint> AggregateByTimeSpan(
        List<ExecutionDataPoint> data,
        TimeSpan interval) => data
            .GroupBy(dp => new DateTime((dp.Timestamp.Ticks / interval.Ticks) * interval.Ticks))
            .Select(group => new ExecutionDataPoint
            {
                Timestamp = group.Key,
                ExecutionCount = group.Sum(dp => dp.ExecutionCount),
                SuccessRate = group.Average(dp => dp.SuccessRate),
                AverageExecutionTime = TimeSpan.FromTicks((long)group.Average(dp => dp.AverageExecutionTime.Ticks)),
                ErrorCount = group.Sum(dp => dp.ErrorCount),
                AdditionalMetrics = group
                    .SelectMany(dp => dp.AdditionalMetrics)
                    .GroupBy(kvp => kvp.Key)
                    .ToDictionary(g => g.Key, g => g.Average(kvp => kvp.Value))
            })
            .OrderBy(dp => dp.Timestamp);

    public void Dispose()
    {
        StopMonitoringAsync().Wait();
        _monitoringTimer?.Dispose();
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();
    }
}
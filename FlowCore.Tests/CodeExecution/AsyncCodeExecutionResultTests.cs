namespace FlowCore.Tests.CodeExecution;
public class AsyncCodeExecutionResultTests
{
    [Fact]
    public void CreateAsyncSuccess_ShouldReturnSuccessResult()
    {
        // Arrange
        var output = 42;
        var executionTime = TimeSpan.FromMilliseconds(100);
        var metadata = new Dictionary<string, object> { ["key"] = "value" };
        var asyncOperations = new[] { new AsyncOperationInfo("op1", DateTime.UtcNow) };
        var performanceMetrics = new AsyncPerformanceMetrics { TotalAsyncOperations = 1 };
        // Act
        var result = AsyncCodeExecutionResult.CreateAsyncSuccess(
            output, executionTime, metadata, asyncOperations, 2, true, TimeSpan.FromMilliseconds(50), performanceMetrics);
        // Assert
        Assert.True(result.Success);
        Assert.Equal(output, result.Output);
        Assert.Equal(executionTime, result.ExecutionTime);
        Assert.Equal(metadata, result.Metadata);
        Assert.Equal(asyncOperations, result.AsyncOperations);
        Assert.Equal(2, result.ActualDegreeOfParallelism);
        Assert.True(result.ContainedAsyncOperations);
        Assert.Equal(TimeSpan.FromMilliseconds(50), result.TotalAsyncWaitTime);
        Assert.Equal(performanceMetrics, result.PerformanceMetrics);
    }
    [Fact]
    public void CreateAsyncFailure_ShouldReturnFailureResult()
    {
        // Arrange
        var errorMessage = "Test error";
        var exception = new Exception("Test exception");
        var executionTime = TimeSpan.FromMilliseconds(50);
        var metadata = new Dictionary<string, object> { ["error"] = "details" };
        var asyncOperations = new[] { new AsyncOperationInfo("op1", DateTime.UtcNow, success: false, errorMessage: "op error") };
        var performanceMetrics = new AsyncPerformanceMetrics { TotalAsyncOperations = 1 };
        // Act
        var result = AsyncCodeExecutionResult.CreateAsyncFailure(
            errorMessage, exception, executionTime, metadata, asyncOperations, performanceMetrics);
        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.Equal(exception, result.Exception);
        Assert.Equal(executionTime, result.ExecutionTime);
        Assert.Equal(metadata, result.Metadata);
        Assert.Equal(asyncOperations, result.AsyncOperations);
        Assert.Equal(0, result.ActualDegreeOfParallelism);
        Assert.False(result.ContainedAsyncOperations);
        Assert.Equal(TimeSpan.Zero, result.TotalAsyncWaitTime);
        Assert.Equal(performanceMetrics, result.PerformanceMetrics);
    }
    [Fact]
    public void AsyncOperationInfo_MarkCompleted_ShouldUpdateEndTime()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var operation = new AsyncOperationInfo("testOp", startTime);
        // Act
        var completedOperation = operation.MarkCompleted(true);
        // Assert
        Assert.Equal("testOp", completedOperation.OperationName);
        Assert.Equal(startTime, completedOperation.StartTime);
        Assert.NotNull(completedOperation.EndTime);
        Assert.True(completedOperation.Success);
        Assert.Null(completedOperation.ErrorMessage);
        Assert.True(completedOperation.Duration > TimeSpan.Zero);
    }
    [Fact]
    public void AsyncOperationInfo_MarkCompleted_WithFailure_ShouldSetError()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var operation = new AsyncOperationInfo("testOp", startTime);
        // Act
        var completedOperation = operation.MarkCompleted(false, "Test error");
        // Assert
        Assert.Equal("testOp", completedOperation.OperationName);
        Assert.Equal(startTime, completedOperation.StartTime);
        Assert.NotNull(completedOperation.EndTime);
        Assert.False(completedOperation.Success);
        Assert.Equal("Test error", completedOperation.ErrorMessage);
        Assert.True(completedOperation.Duration > TimeSpan.Zero);
    }
    [Fact]
    public void AsyncPerformanceMetrics_FromExecution_ShouldCalculateMetrics()
    {
        // Arrange
        var operations = new[]
        {
            new AsyncOperationInfo("op1", DateTime.UtcNow.AddMilliseconds(-100), DateTime.UtcNow, true),
            new AsyncOperationInfo("op2", DateTime.UtcNow.AddMilliseconds(-50), DateTime.UtcNow, true),
            new AsyncOperationInfo("op3", DateTime.UtcNow.AddMilliseconds(-200), DateTime.UtcNow.AddMilliseconds(-100), false, "error")
        };
        var totalExecutionTime = TimeSpan.FromMilliseconds(200);
        var peakConcurrency = 2;
        var additionalCounters = new Dictionary<string, double> { ["custom"] = 1.5 };
        // Act
        var metrics = AsyncPerformanceMetrics.FromExecution(operations, totalExecutionTime, peakConcurrency, additionalCounters);
        // Assert
        Assert.Equal(3, metrics.TotalAsyncOperations);
        Assert.Equal(2, metrics.PeakConcurrentOperations);
        Assert.InRange(metrics.AverageOperationTime, TimeSpan.FromMilliseconds(74), TimeSpan.FromMilliseconds(76)); // Average of successful ops with tolerance
        Assert.Equal(TimeSpan.FromMilliseconds(150), metrics.TotalCpuTime); // Sum of successful durations
        Assert.Equal(0.75, metrics.EfficiencyRatio); // 150 / 200
        Assert.Equal(0, metrics.RetriedOperations); // No metadata for retries
        Assert.Equal(0, metrics.TimeoutOperations); // No timeout in error message
        Assert.Equal(additionalCounters, metrics.AdditionalCounters);
    }
    [Fact]
    public void AsyncPerformanceMetrics_DefaultConstructor_ShouldInitializeEmpty()
    {
        // Arrange & Act
        var metrics = new AsyncPerformanceMetrics();
        // Assert
        Assert.Equal(0, metrics.TotalAsyncOperations);
        Assert.Equal(0, metrics.PeakConcurrentOperations);
        Assert.Equal(TimeSpan.Zero, metrics.AverageOperationTime);
        Assert.Equal(TimeSpan.Zero, metrics.TotalCpuTime);
        Assert.Equal(0, metrics.PeakMemoryUsage);
        Assert.Equal(0.0, metrics.EfficiencyRatio);
        Assert.Equal(0, metrics.RetriedOperations);
        Assert.Equal(0, metrics.TimeoutOperations);
        Assert.Empty(metrics.AdditionalCounters);
    }
}
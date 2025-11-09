namespace FlowCore.Tests;

public class WorkflowStatePersistenceServiceTests : IDisposable
{
    private readonly Mock<IStateManager> _mockStateManager;
    private readonly ILogger<WorkflowStatePersistenceService> _logger;
    private readonly WorkflowStatePersistenceService _persistenceService;
    private readonly string _testWorkflowId = "test-workflow";
    private readonly Guid _testExecutionId = Guid.NewGuid();
    public WorkflowStatePersistenceServiceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<WorkflowStatePersistenceService>();
        _mockStateManager = new Mock<IStateManager>();
        _persistenceService = new WorkflowStatePersistenceService(_mockStateManager.Object, _logger);
    }
    public void Dispose()
    {
        _mockStateManager.Object.Dispose();
    }
    [Fact]
    public async Task SaveCheckpointAsync_WithAfterEachBlockFrequency_ShouldSaveCheckpoint()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object> { ["test"] = "data" },
            CancellationToken.None,
            _testWorkflowId);
        var status = WorkflowStatus.Running;
        var frequency = CheckpointFrequency.AfterEachBlock;
        // Act
        await _persistenceService.SaveCheckpointAsync(_testWorkflowId, _testExecutionId, context, status, frequency);
        // Assert
        _mockStateManager.Verify(m => m.SaveStateAsync(
            _testWorkflowId,
            _testExecutionId,
            It.Is<IDictionary<string, object>>(state => state["test"].ToString() == "data"),
            It.Is<WorkflowStateMetadata>(metadata =>
                metadata.WorkflowId == _testWorkflowId &&
                metadata.ExecutionId == _testExecutionId &&
                metadata.Status == status &&
                metadata.CurrentBlockName == context.CurrentBlockName)), Times.Once);
    }
    [Fact]
    public async Task SaveCheckpointAsync_WithNeverFrequency_ShouldNotSaveCheckpoint()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object> { ["test"] = "data" },
            CancellationToken.None,
            _testWorkflowId);
        var status = WorkflowStatus.Running;
        var frequency = CheckpointFrequency.Never;
        // Act
        await _persistenceService.SaveCheckpointAsync(_testWorkflowId, _testExecutionId, context, status, frequency);
        // Assert
        _mockStateManager.Verify(m => m.SaveStateAsync(
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<WorkflowStateMetadata>()), Times.Never);
    }
    [Fact]
    public async Task SaveCheckpointAsync_WithOnErrorOrCompletionFrequency_WhenRunning_ShouldNotSaveCheckpoint()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object> { ["test"] = "data" },
            CancellationToken.None,
            _testWorkflowId);
        var status = WorkflowStatus.Running;
        var frequency = CheckpointFrequency.OnErrorOrCompletion;
        // Act
        await _persistenceService.SaveCheckpointAsync(_testWorkflowId, _testExecutionId, context, status, frequency);
        // Assert
        _mockStateManager.Verify(m => m.SaveStateAsync(
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<WorkflowStateMetadata>()), Times.Never);
    }
    [Fact]
    public async Task SaveCheckpointAsync_WithOnErrorOrCompletionFrequency_WhenCompleted_ShouldSaveCheckpoint()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object> { ["test"] = "data" },
            CancellationToken.None,
            _testWorkflowId);
        var status = WorkflowStatus.Completed;
        var frequency = CheckpointFrequency.OnErrorOrCompletion;
        // Act
        await _persistenceService.SaveCheckpointAsync(_testWorkflowId, _testExecutionId, context, status, frequency);
        // Assert
        _mockStateManager.Verify(m => m.SaveStateAsync(
            _testWorkflowId,
            _testExecutionId,
            It.Is<IDictionary<string, object>>(state => state["test"].ToString() == "data"),
            It.Is<WorkflowStateMetadata>(metadata =>
                metadata.WorkflowId == _testWorkflowId &&
                metadata.ExecutionId == _testExecutionId &&
                metadata.Status == status)), Times.Once);
    }
    [Fact]
    public async Task SaveCheckpointAsync_WithOnErrorOrCompletionFrequency_WhenFailed_ShouldSaveCheckpoint()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object> { ["test"] = "data" },
            CancellationToken.None,
            _testWorkflowId);
        var status = WorkflowStatus.Failed;
        var frequency = CheckpointFrequency.OnErrorOrCompletion;
        // Act
        await _persistenceService.SaveCheckpointAsync(_testWorkflowId, _testExecutionId, context, status, frequency);
        // Assert
        _mockStateManager.Verify(m => m.SaveStateAsync(
            _testWorkflowId,
            _testExecutionId,
            It.Is<IDictionary<string, object>>(state => state["test"].ToString() == "data"),
            It.Is<WorkflowStateMetadata>(metadata =>
                metadata.WorkflowId == _testWorkflowId &&
                metadata.ExecutionId == _testExecutionId &&
                metadata.Status == status)), Times.Once);
    }
    [Fact]
    public async Task SaveCheckpointAsync_WhenStateManagerThrowsException_ShouldHandleGracefully()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object> { ["test"] = "data" },
            CancellationToken.None,
            _testWorkflowId);
        _mockStateManager.Setup(m => m.SaveStateAsync(
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<WorkflowStateMetadata>()))
            .ThrowsAsync(new Exception("State manager error"));
        // Act
        var exception = await Record.ExceptionAsync(() =>
            _persistenceService.SaveCheckpointAsync(_testWorkflowId, _testExecutionId, context, WorkflowStatus.Running, CheckpointFrequency.AfterEachBlock));
        // Assert
        Assert.Null(exception); // Should not throw - checkpoint failures shouldn't stop workflow execution
    }
    [Fact]
    public async Task LoadLatestCheckpointAsync_WithValidCheckpoint_ShouldReturnContext()
    {
        // Arrange
        var expectedState = new Dictionary<string, object> { ["test"] = "data" };
        var expectedMetadata = new WorkflowStateMetadata(
            _testWorkflowId,
            _testExecutionId,
            WorkflowStatus.Running,
            "TestBlock");
        _mockStateManager.Setup(m => m.LoadStateAsync(_testWorkflowId, _testExecutionId))
            .ReturnsAsync(expectedState);
        _mockStateManager.Setup(m => m.GetStateMetadataAsync(_testWorkflowId, _testExecutionId))
            .ReturnsAsync(expectedMetadata);
        // Act
        var result = await _persistenceService.LoadLatestCheckpointAsync(_testWorkflowId, _testExecutionId);
        // Assert
        Assert.NotNull(result);
        Assert.Equal("data", result.State["test"]);
        Assert.Equal(_testWorkflowId, result.WorkflowName);
        _mockStateManager.Verify(m => m.LoadStateAsync(_testWorkflowId, _testExecutionId), Times.Once);
        _mockStateManager.Verify(m => m.GetStateMetadataAsync(_testWorkflowId, _testExecutionId), Times.Once);
    }
    [Fact]
    public async Task LoadLatestCheckpointAsync_WithNoState_ShouldReturnNull()
    {
        // Arrange
        _mockStateManager.Setup(m => m.LoadStateAsync(_testWorkflowId, _testExecutionId))
            .ReturnsAsync((IDictionary<string, object>?)null);
        // Act
        var result = await _persistenceService.LoadLatestCheckpointAsync(_testWorkflowId, _testExecutionId);
        // Assert
        Assert.Null(result);
        _mockStateManager.Verify(m => m.LoadStateAsync(_testWorkflowId, _testExecutionId), Times.Once);
        _mockStateManager.Verify(m => m.GetStateMetadataAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }
    [Fact]
    public async Task LoadLatestCheckpointAsync_WithNoMetadata_ShouldReturnNull()
    {
        // Arrange
        var expectedState = new Dictionary<string, object> { ["test"] = "data" };
        _mockStateManager.Setup(m => m.LoadStateAsync(_testWorkflowId, _testExecutionId))
            .ReturnsAsync(expectedState);
        _mockStateManager.Setup(m => m.GetStateMetadataAsync(_testWorkflowId, _testExecutionId))
            .ReturnsAsync((WorkflowStateMetadata?)null);
        // Act
        var result = await _persistenceService.LoadLatestCheckpointAsync(_testWorkflowId, _testExecutionId);
        // Assert
        Assert.Null(result);
        _mockStateManager.Verify(m => m.LoadStateAsync(_testWorkflowId, _testExecutionId), Times.Once);
        _mockStateManager.Verify(m => m.GetStateMetadataAsync(_testWorkflowId, _testExecutionId), Times.Once);
    }
    [Fact]
    public async Task LoadLatestCheckpointAsync_WhenStateManagerThrowsException_ShouldReturnNull()
    {
        // Arrange
        _mockStateManager.Setup(m => m.LoadStateAsync(_testWorkflowId, _testExecutionId))
            .ThrowsAsync(new Exception("State manager error"));
        // Act
        var result = await _persistenceService.LoadLatestCheckpointAsync(_testWorkflowId, _testExecutionId);
        // Assert
        Assert.Null(result);
    }
    [Fact]
    public async Task CleanupOldCheckpointsAsync_ShouldCallStateManagerCleanup()
    {
        // Arrange
        var config = new StateManagerConfig { MaxStateAge = TimeSpan.FromDays(7) };
        var expectedDeletedCount = 5;
        _mockStateManager.Setup(m => m.CleanupOldStatesAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(expectedDeletedCount);
        // Act
        var result = await _persistenceService.CleanupOldCheckpointsAsync(config);
        // Assert
        Assert.Equal(expectedDeletedCount, result);
        _mockStateManager.Verify(m => m.CleanupOldStatesAsync(
            It.Is<DateTime>(date => date <= DateTime.UtcNow)), Times.Once);
    }
    [Fact]
    public async Task CleanupOldCheckpointsAsync_WhenStateManagerThrowsException_ShouldReturnZero()
    {
        // Arrange
        var config = new StateManagerConfig { MaxStateAge = TimeSpan.FromDays(7) };
        _mockStateManager.Setup(m => m.CleanupOldStatesAsync(It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Cleanup error"));
        // Act
        var result = await _persistenceService.CleanupOldCheckpointsAsync(config);
        // Assert
        Assert.Equal(0, result);
    }
    [Fact]
    public async Task SaveCheckpointAsync_WithComplexState_ShouldPreserveAllData()
    {
        // Arrange
        var complexState = new Dictionary<string, object>
        {
            ["stringValue"] = "test string",
            ["intValue"] = 42,
            ["boolValue"] = true,
            ["doubleValue"] = 3.14,
            ["dateTimeValue"] = new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc),
            ["guidValue"] = Guid.Parse("12345678-1234-1234-1234-123456789012"),
            ["listValue"] = new List<int> { 1, 2, 3, 4, 5 },
            ["nestedDict"] = new Dictionary<string, object>
            {
                ["innerString"] = "nested value",
                ["innerInt"] = 100
            }
        };
        var context = new Models.ExecutionContext(
            complexState,
            CancellationToken.None,
            _testWorkflowId);
        // Act
        await _persistenceService.SaveCheckpointAsync(_testWorkflowId, _testExecutionId, context, WorkflowStatus.Running, CheckpointFrequency.AfterEachBlock);
        // Assert
        _mockStateManager.Verify(m => m.SaveStateAsync(
            _testWorkflowId,
            _testExecutionId,
            It.Is<IDictionary<string, object>>(state =>
                state["stringValue"].ToString() == "test string" &&
                (int)state["intValue"] == 42 &&
                (bool)state["boolValue"] == true &&
                Math.Abs((double)state["doubleValue"] - 3.14) < 0.001 &&
                state["dateTimeValue"].Equals(new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc)) &&
                state["guidValue"].Equals(Guid.Parse("12345678-1234-1234-1234-123456789012")) &&
                state.ContainsKey("listValue") &&
                state.ContainsKey("nestedDict")),
            It.IsAny<WorkflowStateMetadata>()), Times.Once);
    }
    [Fact]
    public async Task LoadLatestCheckpointAsync_WithCancellationToken_ShouldPassToStateManager()
    {
        // Arrange
        var ct = new CancellationTokenSource().Token;
        var expectedState = new Dictionary<string, object> { ["test"] = "data" };
        var expectedMetadata = new WorkflowStateMetadata(
            _testWorkflowId,
            _testExecutionId,
            WorkflowStatus.Running,
            "TestBlock");
        _mockStateManager.Setup(m => m.LoadStateAsync(_testWorkflowId, _testExecutionId))
            .ReturnsAsync(expectedState);
        _mockStateManager.Setup(m => m.GetStateMetadataAsync(_testWorkflowId, _testExecutionId))
            .ReturnsAsync(expectedMetadata);
        // Act
        var result = await _persistenceService.LoadLatestCheckpointAsync(_testWorkflowId, _testExecutionId, ct);
        // Assert
        Assert.NotNull(result);
        _mockStateManager.Verify(m => m.LoadStateAsync(_testWorkflowId, _testExecutionId), Times.Once);
    }
    [Fact]
    public async Task SaveCheckpointAsync_WithSuspendedStatus_ShouldSaveCorrectly()
    {
        // Arrange
        var context = new Models.ExecutionContext(
            new Dictionary<string, object> { ["suspended"] = true },
            CancellationToken.None,
            _testWorkflowId);
        var status = WorkflowStatus.Suspended;
        // Act
        await _persistenceService.SaveCheckpointAsync(_testWorkflowId, _testExecutionId, context, status, CheckpointFrequency.AfterEachBlock);
        // Assert
        _mockStateManager.Verify(m => m.SaveStateAsync(
            _testWorkflowId,
            _testExecutionId,
            It.Is<IDictionary<string, object>>(state => (bool)state["suspended"] == true),
            It.Is<WorkflowStateMetadata>(metadata =>
                metadata.WorkflowId == _testWorkflowId &&
                metadata.ExecutionId == _testExecutionId &&
                metadata.Status == status)), Times.Once);
    }
    [Fact]
    public async Task LoadLatestCheckpointAsync_WithSuspendedWorkflow_ShouldLoadCorrectly()
    {
        // Arrange
        var expectedState = new Dictionary<string, object> { ["suspended"] = true };
        var expectedMetadata = new WorkflowStateMetadata(
            _testWorkflowId,
            _testExecutionId,
            WorkflowStatus.Suspended,
            "SuspendedBlock");
        _mockStateManager.Setup(m => m.LoadStateAsync(_testWorkflowId, _testExecutionId))
            .ReturnsAsync(expectedState);
        _mockStateManager.Setup(m => m.GetStateMetadataAsync(_testWorkflowId, _testExecutionId))
            .ReturnsAsync(expectedMetadata);
        // Act
        var result = await _persistenceService.LoadLatestCheckpointAsync(_testWorkflowId, _testExecutionId);
        // Assert
        Assert.NotNull(result);
        Assert.True((bool)result.State["suspended"]);
        Assert.Equal("SuspendedBlock", result.CurrentBlockName);
    }
}

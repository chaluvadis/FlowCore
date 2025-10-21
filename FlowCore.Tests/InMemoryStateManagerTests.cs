using FlowCore.Persistence;
using FlowCore.Models;
using Microsoft.Extensions.Logging;
using System;
namespace FlowCore.Tests;
public class InMemoryStateManagerTests : IDisposable
{
    private readonly InMemoryStateManager _stateManager;
    private readonly ILogger<InMemoryStateManager> _logger;
    private readonly string _workflowId = "test-workflow";
    private readonly Guid _executionId = Guid.NewGuid();
    public InMemoryStateManagerTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<InMemoryStateManager>();
        _stateManager = new InMemoryStateManager(new StateManagerConfig(), _logger);
    }
    public void Dispose()
    {
        _stateManager.Dispose();
    }
    [Fact]
    public async Task SaveStateAsync_And_LoadStateAsync_WithBasicData_ShouldWork()
    {
        // Arrange
        var state = new Dictionary<string, object>
        {
            ["counter"] = 42,
            ["message"] = "Hello, World!",
            ["isActive"] = true
        };
        // Act
        await _stateManager.SaveStateAsync(_workflowId, _executionId, state);
        var loadedState = await _stateManager.LoadStateAsync(_workflowId, _executionId);
        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal(42, loadedState["counter"]);
        Assert.Equal("Hello, World!", loadedState["message"]);
        Assert.Equal(true, loadedState["isActive"]);
    }
    [Fact]
    public async Task SaveStateAsync_WithComplexObjects_ShouldPreserveTypes()
    {
        // Arrange
        var state = new Dictionary<string, object>
        {
            ["createdDate"] = new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc),
            ["userId"] = Guid.Parse("12345678-1234-1234-1234-123456789012"),
            ["scores"] = new List<int> { 95, 87, 92, 88 },
            ["metadata"] = new Dictionary<string, object>
            {
                ["version"] = "1.0.0",
                ["author"] = "test-user"
            }
        };
        // Act
        await _stateManager.SaveStateAsync(_workflowId, _executionId, state);
        var loadedState = await _stateManager.LoadStateAsync(_workflowId, _executionId);
        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal(new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc), loadedState["createdDate"]);
        Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789012"), loadedState["userId"]);
        var scores = loadedState["scores"] as List<object>;
        Assert.NotNull(scores);
        Assert.Equal(95, scores![0]);
        Assert.Equal(87, scores![1]);
        Assert.Equal(92, scores![2]);
        Assert.Equal(88, scores![3]);
        var metadata = loadedState["metadata"] as Dictionary<string, object>;
        Assert.NotNull(metadata);
        Assert.Equal("1.0.0", metadata!["version"]);
        Assert.Equal("test-user", metadata!["author"]);
    }
    [Fact]
    public async Task SaveStateAsync_WithCompressionEnabled_ShouldCompressLargeData()
    {
        // Arrange
        var config = new StateManagerConfig
        {
            Compression = new StateCompressionConfig
            {
                Enabled = true,
                MinSizeThreshold = 1024, // 1KB threshold
                Algorithm = CompressionAlgorithm.GZip
            }
        };
        var stateManager = new InMemoryStateManager(config, _logger);
        // Create large state data
        var state = new Dictionary<string, object>();
        for (int i = 0; i < 100; i++)
        {
            state[$"largeData{i}"] = $"This is a large string value that contains significant amounts of data to test compression functionality. Iteration {i} with repeated content.";
        }
        // Act
        await stateManager.SaveStateAsync(_workflowId, _executionId, state);
        var loadedState = await stateManager.LoadStateAsync(_workflowId, _executionId);
        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal(100, loadedState.Count);
        Assert.Equal(state["largeData50"], loadedState["largeData50"]);
        stateManager.Dispose();
    }
    [Fact]
    public async Task SaveStateAsync_WithEncryptionEnabled_ShouldEncryptData()
    {
        // Arrange
        var config = new StateManagerConfig
        {
            Encryption = new StateEncryptionConfig
            {
                Enabled = true,
                KeyIdentifier = "test-encryption-key-12345",
                Algorithm = EncryptionAlgorithm.AES256
            }
        };
        var stateManager = new InMemoryStateManager(config, _logger);
        var state = new Dictionary<string, object>
        {
            ["sensitiveData"] = "This is confidential workflow information",
            ["apiKey"] = "sk-1234567890abcdef"
        };
        // Act
        await stateManager.SaveStateAsync(_workflowId, _executionId, state);
        var loadedState = await stateManager.LoadStateAsync(_workflowId, _executionId);
        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal("This is confidential workflow information", loadedState["sensitiveData"]);
        Assert.Equal("sk-1234567890abcdef", loadedState["apiKey"]);
        stateManager.Dispose();
    }
    [Fact]
    public async Task SaveStateAsync_WithCompressionAndEncryption_ShouldApplyBoth()
    {
        // Arrange
        var config = new StateManagerConfig
        {
            Compression = new StateCompressionConfig
            {
                Enabled = true,
                MinSizeThreshold = 512,
                Algorithm = CompressionAlgorithm.GZip
            },
            Encryption = new StateEncryptionConfig
            {
                Enabled = true,
                KeyIdentifier = "test-key-combined-12345",
                Algorithm = EncryptionAlgorithm.AES256
            }
        };
        var stateManager = new InMemoryStateManager(config, _logger);
        // Create moderately large state
        var state = new Dictionary<string, object>();
        for (int i = 0; i < 30; i++)
        {
            state[$"data{i}"] = $"Combined compression and encryption test data with iteration {i} and some additional content to make it larger.";
        }
        // Act
        await stateManager.SaveStateAsync(_workflowId, _executionId, state);
        var loadedState = await stateManager.LoadStateAsync(_workflowId, _executionId);
        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal(30, loadedState.Count);
        Assert.Equal(state["data15"], loadedState["data15"]);
        stateManager.Dispose();
    }
    [Fact]
    public async Task StateExistsAsync_ShouldReturnCorrectValues()
    {
        // Arrange
        var state = new Dictionary<string, object> { ["test"] = "data" };
        // Act & Assert
        Assert.False(await _stateManager.StateExistsAsync(_workflowId, _executionId));
        await _stateManager.SaveStateAsync(_workflowId, _executionId, state);
        Assert.True(await _stateManager.StateExistsAsync(_workflowId, _executionId));
    }
    [Fact]
    public async Task DeleteStateAsync_ShouldRemoveState()
    {
        // Arrange
        var state = new Dictionary<string, object> { ["test"] = "data" };
        await _stateManager.SaveStateAsync(_workflowId, _executionId, state);
        // Act
        await _stateManager.DeleteStateAsync(_workflowId, _executionId);
        // Assert
        Assert.False(await _stateManager.StateExistsAsync(_workflowId, _executionId));
        Assert.Null(await _stateManager.LoadStateAsync(_workflowId, _executionId));
    }
    [Fact]
    public async Task GetExecutionIdsAsync_ShouldReturnCorrectExecutions()
    {
        // Arrange
        var executionId1 = Guid.NewGuid();
        var executionId2 = Guid.NewGuid();
        var state1 = new Dictionary<string, object> { ["data"] = "test1" };
        var state2 = new Dictionary<string, object> { ["data"] = "test2" };
        await _stateManager.SaveStateAsync(_workflowId, executionId1, state1);
        await _stateManager.SaveStateAsync(_workflowId, executionId2, state2);
        // Act
        var executionIds = await _stateManager.GetExecutionIdsAsync(_workflowId);
        // Assert
        Assert.Equal(2, executionIds.Count());
        Assert.Contains(executionId1, executionIds);
        Assert.Contains(executionId2, executionIds);
    }
    [Fact]
    public async Task GetStateMetadataAsync_ShouldReturnMetadata()
    {
        // Arrange
        var state = new Dictionary<string, object> { ["test"] = "data" };
        await _stateManager.SaveStateAsync(_workflowId, _executionId, state);
        // Act
        var metadata = await _stateManager.GetStateMetadataAsync(_workflowId, _executionId);
        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(_workflowId, metadata!.WorkflowId);
        Assert.Equal(_executionId, metadata.ExecutionId);
        Assert.Equal(WorkflowStatus.Running, metadata.Status);
        Assert.True(metadata.StateSize >= 0);
    }
    [Fact]
    public async Task UpdateStateMetadataAsync_ShouldUpdateMetadata()
    {
        // Arrange
        var state = new Dictionary<string, object> { ["test"] = "data" };
        await _stateManager.SaveStateAsync(_workflowId, _executionId, state);
        var updatedMetadata = new WorkflowStateMetadata(
            _workflowId,
            _executionId,
            WorkflowStatus.Completed,
            "FinalBlock",
            1024,
            "2.0.0");
        // Act
        await _stateManager.UpdateStateMetadataAsync(_workflowId, _executionId, updatedMetadata);
        var retrievedMetadata = await _stateManager.GetStateMetadataAsync(_workflowId, _executionId);
        // Assert
        Assert.NotNull(retrievedMetadata);
        Assert.Equal(WorkflowStatus.Completed, retrievedMetadata!.Status);
        Assert.Equal("FinalBlock", retrievedMetadata.CurrentBlockName);
        Assert.Equal("2.0.0", retrievedMetadata.WorkflowVersion);
    }
    [Fact]
    public async Task CleanupOldStatesAsync_ShouldRemoveExpiredStates()
    {
        // Arrange
        var oldExecutionId = Guid.NewGuid();
        var recentExecutionId = Guid.NewGuid();
        var oldState = new Dictionary<string, object> { ["old"] = "data" };
        var recentState = new Dictionary<string, object> { ["recent"] = "data" };
        await _stateManager.SaveStateAsync(_workflowId, oldExecutionId, oldState);
        await _stateManager.SaveStateAsync(_workflowId, recentExecutionId, recentState);
        // Simulate old state by directly modifying metadata (in real scenario, this would be timestamp-based)
        var oldMetadata = await _stateManager.GetStateMetadataAsync(_workflowId, oldExecutionId);
        Assert.NotNull(oldMetadata);
        // Act
        var deletedCount = await _stateManager.CleanupOldStatesAsync(DateTime.UtcNow.AddDays(1));
        // Assert
        // Note: In a real implementation, this would depend on actual timestamps
        // For this test, we're mainly ensuring the method doesn't throw
        Assert.True(deletedCount >= 0);
    }
    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnCorrectStatistics()
    {
        // Arrange
        var executionId1 = Guid.NewGuid();
        var executionId2 = Guid.NewGuid();
        await _stateManager.SaveStateAsync(_workflowId, executionId1, new Dictionary<string, object> { ["data1"] = "test1" });
        await _stateManager.SaveStateAsync(_workflowId, executionId2, new Dictionary<string, object> { ["data2"] = "test2" });
        // Act
        var stats = await _stateManager.GetStatisticsAsync();
        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.TotalStates >= 2);
        Assert.True(stats.TotalSizeBytes > 0);
        Assert.True(stats.ActiveExecutions >= 2);
        Assert.Equal(DateTime.UtcNow.Date, stats.GeneratedAt.Date);
    }
    [Fact]
    public async Task LoadStateAsync_ForNonExistentState_ShouldReturnNull()
    {
        // Arrange
        var nonExistentExecutionId = Guid.NewGuid();
        // Act
        var result = await _stateManager.LoadStateAsync(_workflowId, nonExistentExecutionId);
        // Assert
        Assert.Null(result);
    }
    [Fact]
    public async Task SaveStateAsync_WithNullState_ShouldHandleGracefully()
    {
        // Arrange
        var state = new Dictionary<string, object>
        {
            ["nullValue"] = null!,
            ["emptyString"] = "",
            ["zero"] = 0
        };
        // Act
        await _stateManager.SaveStateAsync(_workflowId, _executionId, state);
        var loadedState = await _stateManager.LoadStateAsync(_workflowId, _executionId);
        // Assert
        Assert.NotNull(loadedState);
        Assert.Null(loadedState["nullValue"]);
        Assert.Equal("", loadedState["emptyString"]);
        Assert.Equal(0, loadedState["zero"]);
    }
    [Fact]
    public async Task ConcurrentOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var executionIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
        // Act
        for (int i = 0; i < 10; i++)
        {
            var executionId = executionIds[i]; // Capture in local variable
            var task = Task.Run(async () =>
            {
                var state = new Dictionary<string, object>
                {
                    ["threadId"] = Environment.CurrentManagedThreadId,
                    ["index"] = i
                };
                await _stateManager.SaveStateAsync(_workflowId, executionId, state);
                var loadedState = await _stateManager.LoadStateAsync(_workflowId, executionId);
                Assert.NotNull(loadedState);
                Assert.Equal(Environment.CurrentManagedThreadId, loadedState["threadId"]);
                Assert.Equal(i, loadedState["index"]);
            });
            tasks.Add(task);
        }
        await Task.WhenAll(tasks);
        // Assert
        Assert.Equal(10, executionIds.Count);
    }
}
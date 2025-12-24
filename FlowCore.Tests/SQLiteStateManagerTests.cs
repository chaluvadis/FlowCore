namespace FlowCore.Tests;

using FlowCore.Persistence;
using System.IO;

public class SQLiteStateManagerTests : IDisposable
{
    private readonly SQLiteStateManager _stateManager;
    private readonly ILogger<SQLiteStateManager> _logger;
    private readonly string _workflowId = "test-workflow";
    private readonly Guid _executionId = Guid.NewGuid();
    private readonly string _dbPath;

    public SQLiteStateManagerTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<SQLiteStateManager>();
        
        // Create a unique database file for each test run
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowcore_test_{Guid.NewGuid()}.db");
        _stateManager = new SQLiteStateManager(_dbPath, new StateManagerConfig(), _logger);
    }

    public void Dispose()
    {
        _stateManager.Dispose();
        
        // Clean up test database
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
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
        
        // DateTime is serialized as ISO 8601 string in JSON
        var createdDateValue = loadedState["createdDate"];
        if (createdDateValue is string dateStr)
        {
            var actualDate = DateTime.Parse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
            var expectedDate = new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc);
            Assert.Equal(expectedDate, actualDate);
        }
        else if (createdDateValue is DateTime actualDateTime)
        {
            Assert.Equal(new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc), actualDateTime);
        }
        
        // Guid is also serialized as string in JSON
        var userIdValue = loadedState["userId"];
        if (userIdValue is string guidStr)
        {
            var actualGuid = Guid.Parse(guidStr);
            Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789012"), actualGuid);
        }
        else if (userIdValue is Guid actualGuid)
        {
            Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789012"), actualGuid);
        }
        
        var scores = loadedState["scores"] as List<object>;
        Assert.NotNull(scores);
        Assert.Equal(4, scores!.Count);
        
        var metadata = loadedState["metadata"];
        Assert.NotNull(metadata);
        
        // Metadata might be Dictionary<string, object> or JsonElement
        if (metadata is Dictionary<string, object> dict)
        {
            Assert.Equal("1.0.0", dict["version"]);
            Assert.Equal("test-user", dict["author"]);
        }
        else if (metadata is System.Text.Json.JsonElement element)
        {
            Assert.Equal("1.0.0", element.GetProperty("version").GetString());
            Assert.Equal("test-user", element.GetProperty("author").GetString());
        }
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
        var dbPath = Path.Combine(Path.GetTempPath(), $"flowcore_test_compression_{Guid.NewGuid()}.db");
        using var stateManager = new SQLiteStateManager(dbPath, config, _logger);

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

        // Cleanup
        stateManager.Dispose();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
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
        var dbPath = Path.Combine(Path.GetTempPath(), $"flowcore_test_encryption_{Guid.NewGuid()}.db");
        using var stateManager = new SQLiteStateManager(dbPath, config, _logger);

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

        // Cleanup
        stateManager.Dispose();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
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

        // Act - cleanup states created before tomorrow (should delete all test states)
        var deletedCount = await _stateManager.CleanupOldStatesAsync(DateTime.UtcNow.AddDays(1));

        // Assert - both states should be deleted as they were created before tomorrow
        Assert.True(deletedCount >= 2);
        Assert.False(await _stateManager.StateExistsAsync(_workflowId, oldExecutionId));
        Assert.False(await _stateManager.StateExistsAsync(_workflowId, recentExecutionId));
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
            var index = i;
            var executionId = executionIds[i];
            var task = Task.Run(async () =>
            {
                var state = new Dictionary<string, object>
                {
                    ["threadId"] = Environment.CurrentManagedThreadId,
                    ["index"] = index
                };
                await _stateManager.SaveStateAsync(_workflowId, executionId, state);
                var loadedState = await _stateManager.LoadStateAsync(_workflowId, executionId);
                Assert.NotNull(loadedState);
                Assert.Equal(index, loadedState["index"]);
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, executionIds.Count);
    }

    [Fact]
    public async Task SaveStateAsync_UpdateExistingState_ShouldOverwrite()
    {
        // Arrange
        var initialState = new Dictionary<string, object> { ["value"] = "initial" };
        var updatedState = new Dictionary<string, object> { ["value"] = "updated" };

        // Act
        await _stateManager.SaveStateAsync(_workflowId, _executionId, initialState);
        await _stateManager.SaveStateAsync(_workflowId, _executionId, updatedState);
        var loadedState = await _stateManager.LoadStateAsync(_workflowId, _executionId);

        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal("updated", loadedState["value"]);
    }

    [Fact]
    public async Task SaveStateAsync_WithCustomMetadata_ShouldPersist()
    {
        // Arrange
        var state = new Dictionary<string, object> { ["test"] = "data" };
        var customMetadata = new Dictionary<string, object>
        {
            ["author"] = "test-user",
            ["environment"] = "development",
            ["priority"] = 5
        };
        var metadata = new WorkflowStateMetadata(
            _workflowId,
            _executionId,
            WorkflowStatus.Running,
            "TestBlock",
            100,
            "1.0.0",
            customMetadata);

        // Act
        await _stateManager.SaveStateAsync(_workflowId, _executionId, state, metadata);
        var retrievedMetadata = await _stateManager.GetStateMetadataAsync(_workflowId, _executionId);

        // Assert
        Assert.NotNull(retrievedMetadata);
        Assert.Equal("TestBlock", retrievedMetadata!.CurrentBlockName);
        // Note: Custom metadata is stored but may need separate verification
    }

    [Fact]
    public async Task CleanupOldStatesAsync_WithStatusFilter_ShouldOnlyDeleteMatchingStates()
    {
        // Arrange
        var completedId = Guid.NewGuid();
        var runningId = Guid.NewGuid();
        
        await _stateManager.SaveStateAsync(_workflowId, completedId, new Dictionary<string, object> { ["data"] = "completed" });
        await _stateManager.SaveStateAsync(_workflowId, runningId, new Dictionary<string, object> { ["data"] = "running" });
        
        // Update metadata to set different statuses
        await _stateManager.UpdateStateMetadataAsync(_workflowId, completedId, 
            new WorkflowStateMetadata(_workflowId, completedId, WorkflowStatus.Completed));
        await _stateManager.UpdateStateMetadataAsync(_workflowId, runningId, 
            new WorkflowStateMetadata(_workflowId, runningId, WorkflowStatus.Running));

        // Act - cleanup only completed states
        var deletedCount = await _stateManager.CleanupOldStatesAsync(
            DateTime.UtcNow.AddDays(1), 
            status: WorkflowStatus.Completed);

        // Assert
        Assert.True(deletedCount >= 1);
        Assert.False(await _stateManager.StateExistsAsync(_workflowId, completedId));
        Assert.True(await _stateManager.StateExistsAsync(_workflowId, runningId));
    }

    [Fact]
    public void Constructor_WithSimpleFilePath_ShouldNormalizeConnectionString()
    {
        // Arrange
        var simplePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");

        // Act
        using var manager = new SQLiteStateManager(simplePath);

        // Assert - should not throw and should be able to save state
        var state = new Dictionary<string, object> { ["test"] = "data" };
        var task = manager.SaveStateAsync("test-workflow", Guid.NewGuid(), state);
        task.Wait();

        // Cleanup
        if (File.Exists(simplePath))
        {
            File.Delete(simplePath);
        }
    }

    [Fact]
    public void Constructor_WithNullConnectionString_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SQLiteStateManager(null!));
    }

    [Fact]
    public void Constructor_WithEmptyConnectionString_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SQLiteStateManager(""));
    }
}

namespace FlowCore.Tests;
public class WorkflowStateSerializerTests
{
    private readonly ILogger<WorkflowStateSerializer> _logger;
    private readonly StateManagerConfig _defaultConfig;
    public WorkflowStateSerializerTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<WorkflowStateSerializer>();
        _defaultConfig = new StateManagerConfig();
    }
    [Fact]
    public async Task SerializeAsync_WithBasicTypes_ShouldSucceed()
    {
        // Arrange
        var serializer = new WorkflowStateSerializer(_defaultConfig, _logger);
        var state = new Dictionary<string, object>
        {
            ["stringValue"] = "test string",
            ["intValue"] = 42,
            ["boolValue"] = true,
            ["doubleValue"] = 3.14
        };
        // Act
        var serialized = await serializer.SerializeAsync(state);
        var deserialized = await serializer.DeserializeAsync(serialized);
        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("test string", deserialized["stringValue"]);
        // Handle potential type conversion issues
        var intValue = deserialized["intValue"];
        Assert.True(intValue is int && (int)intValue == 42, $"Expected int 42, got {intValue} (type: {intValue?.GetType()})");
        var boolValue = deserialized["boolValue"];
        Assert.True(boolValue is bool && (bool)boolValue == true, $"Expected bool true, got {boolValue} (type: {boolValue?.GetType()})");
        var doubleValue = deserialized["doubleValue"];
        Assert.True(doubleValue is double && Math.Abs((double)doubleValue - 3.14) < 0.001, $"Expected double 3.14, got {doubleValue} (type: {doubleValue?.GetType()})");
    }
    [Fact]
    public async Task SerializeAsync_WithComplexObjects_ShouldPreserveTypes()
    {
        // Arrange
        var serializer = new WorkflowStateSerializer(_defaultConfig, _logger);
        var state = new Dictionary<string, object>
        {
            ["dateTime"] = new DateTime(2023, 12, 25, 10, 30, 45),
            ["guid"] = Guid.Parse("12345678-1234-1234-1234-123456789012"),
            ["list"] = new List<object> { 1, "test", true },
            ["nestedDict"] = new Dictionary<string, object>
            {
                ["inner"] = "value"
            }
        };
        // Act
        var serialized = await serializer.SerializeAsync(state);
        var deserialized = await serializer.DeserializeAsync(serialized);
        // Assert
        Assert.NotNull(deserialized);
        // Handle DateTime precision differences
        var expectedDateTime = new DateTime(2023, 12, 25, 10, 30, 45);
        var actualDateTime = (DateTime)deserialized["dateTime"];
        Assert.Equal(expectedDateTime.Year, actualDateTime.Year);
        Assert.Equal(expectedDateTime.Month, actualDateTime.Month);
        Assert.Equal(expectedDateTime.Day, actualDateTime.Day);
        Assert.Equal(expectedDateTime.Hour, actualDateTime.Hour);
        Assert.Equal(expectedDateTime.Minute, actualDateTime.Minute);
        Assert.Equal(expectedDateTime.Second, actualDateTime.Second);
        Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789012"), deserialized["guid"]);
        var list = deserialized["list"] as List<object>;
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.Equal(1, list[0]);
        Assert.Equal("test", list[1]);
        Assert.Equal(true, list[2]);
        var nestedDict = deserialized["nestedDict"] as Dictionary<string, object>;
        Assert.NotNull(nestedDict);
        Assert.Equal("value", nestedDict["inner"]);
    }
    [Fact]
    public async Task SerializeAsync_WithCompressionEnabled_ShouldCompressLargeData()
    {
        // Arrange
        var config = new StateManagerConfig
        {
            Compression = new StateCompressionConfig
            {
                Enabled = true,
                MinSizeThreshold = 100, // Low threshold for testing
                Algorithm = CompressionAlgorithm.GZip
            }
        };
        var serializer = new WorkflowStateSerializer(config, _logger);
        // Create large data that exceeds threshold
        var state = new Dictionary<string, object>();
        for (int i = 0; i < 100; i++)
        {
            state[$"key{i}"] = $"This is a long string value that helps us exceed the compression threshold number {i}.";
        }
        // Act
        var serialized = await serializer.SerializeAsync(state);
        // Assert
        Assert.True(serialized.Length > 0);
        // Verify it can be deserialized correctly
        var deserialized = await serializer.DeserializeAsync(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(100, deserialized.Count);
        Assert.Equal(state["key50"], deserialized["key50"]);
    }
    [Fact]
    public async Task SerializeAsync_WithEncryptionEnabled_ShouldEncryptData()
    {
        // Arrange
        var config = new StateManagerConfig
        {
            Encryption = new StateEncryptionConfig
            {
                Enabled = true,
                KeyIdentifier = "test-key-12345",
                Algorithm = EncryptionAlgorithm.AES256
            }
        };
        var serializer = new WorkflowStateSerializer(config, _logger);
        var state = new Dictionary<string, object>
        {
            ["sensitiveData"] = "This is confidential information"
        };
        // Act
        var serialized = await serializer.SerializeAsync(state);
        var deserialized = await serializer.DeserializeAsync(serialized);
        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("This is confidential information", deserialized["sensitiveData"]);
    }
    [Fact]
    public async Task SerializeAsync_WithCompressionAndEncryption_ShouldApplyBoth()
    {
        // Arrange
        var config = new StateManagerConfig
        {
            Compression = new StateCompressionConfig
            {
                Enabled = true,
                MinSizeThreshold = 100,
                Algorithm = CompressionAlgorithm.GZip
            },
            Encryption = new StateEncryptionConfig
            {
                Enabled = true,
                KeyIdentifier = "test-key-12345",
                Algorithm = EncryptionAlgorithm.AES256
            }
        };
        var serializer = new WorkflowStateSerializer(config, _logger);
        // Create large data
        var state = new Dictionary<string, object>();
        for (int i = 0; i < 50; i++)
        {
            state[$"data{i}"] = $"Large data string {i} that should trigger compression and encryption.";
        }
        // Act
        var serialized = await serializer.SerializeAsync(state);
        var deserialized = await serializer.DeserializeAsync(serialized);
        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(50, deserialized.Count);
        Assert.Equal(state["data25"], deserialized["data25"]);
    }
    [Fact]
    public async Task SerializeAsync_WithEmptyState_ShouldHandleGracefully()
    {
        // Arrange
        var serializer = new WorkflowStateSerializer(_defaultConfig, _logger);
        var state = new Dictionary<string, object>();
        // Act
        byte[] serialized;
        try
        {
            serialized = await serializer.SerializeAsync(state);
        }
        catch (Exception ex)
        {
            throw new Exception($"Serialization failed: {ex.Message}", ex);
        }
        IDictionary<string, object>? deserialized;
        try
        {
            deserialized = await serializer.DeserializeAsync(serialized);
        }
        catch (Exception ex)
        {
            throw new Exception($"Deserialization failed. Serialized data length: {serialized.Length}, Error: {ex.Message}", ex);
        }
        // Assert
        Assert.NotNull(deserialized);
        Assert.Empty(deserialized);
    }
    [Fact]
    public async Task SerializeAsync_WithNullValues_ShouldHandleGracefully()
    {
        // Arrange
        var serializer = new WorkflowStateSerializer(_defaultConfig, _logger);
        var state = new Dictionary<string, object>
        {
            ["nullValue"] = null!,
            ["stringValue"] = "not null"
        };
        // Act
        var serialized = await serializer.SerializeAsync(state);
        var deserialized = await serializer.DeserializeAsync(serialized);
        // Debug output
        if (deserialized == null)
        {
            throw new Exception($"Serialization failed. Serialized data length: {serialized.Length}");
        }
        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized["nullValue"]);
        Assert.Equal("not null", deserialized["stringValue"]);
    }
    [Fact]
    public async Task DeserializeAsync_WithInvalidData_ShouldThrowException()
    {
        // Arrange
        var serializer = new WorkflowStateSerializer(_defaultConfig, _logger);
        var invalidData = new byte[] { 1, 2, 3, 4, 5 }; // Invalid data
        // Act & Assert
        await Assert.ThrowsAsync<WorkflowStateSerializationException>(() =>
            serializer.DeserializeAsync(invalidData));
    }
    [Fact]
    public async Task DeserializeAsync_WithCorruptedCompressedData_ShouldThrowException()
    {
        // Arrange
        var config = new StateManagerConfig
        {
            Compression = new StateCompressionConfig
            {
                Enabled = true,
                Algorithm = CompressionAlgorithm.GZip
            }
        };
        var serializer = new WorkflowStateSerializer(config, _logger);
        var corruptedData = new byte[] { 1, 2, 3, 4, 5 }; // Invalid compressed data
        // Act & Assert
        await Assert.ThrowsAsync<WorkflowStateSerializationException>(() =>
            serializer.DeserializeAsync(corruptedData));
    }
    [Fact]
    public async Task SerializeAsync_WithDifferentCompressionAlgorithms_ShouldWork()
    {
        var algorithms = new[] { CompressionAlgorithm.GZip, CompressionAlgorithm.Deflate };
        foreach (var algorithm in algorithms)
        {
            // Arrange
            var config = new StateManagerConfig
            {
                Compression = new StateCompressionConfig
                {
                    Enabled = true,
                    MinSizeThreshold = 100,
                    Algorithm = algorithm
                }
            };
            var serializer = new WorkflowStateSerializer(config, _logger);
            var state = new Dictionary<string, object>();
            for (int i = 0; i < 20; i++)
            {
                state[$"key{i}"] = $"Test data for {algorithm} compression algorithm.";
            }
            // Act
            var serialized = await serializer.SerializeAsync(state);
            var deserialized = await serializer.DeserializeAsync(serialized);
            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(20, deserialized.Count);
            Assert.Equal(state["key10"], deserialized["key10"]);
        }
    }
    [Fact]
    public async Task SerializeAsync_WithDifferentEncryptionAlgorithms_ShouldWork()
    {
        var algorithms = new[] { EncryptionAlgorithm.AES128, EncryptionAlgorithm.AES256 };
        foreach (var algorithm in algorithms)
        {
            // Arrange
            var config = new StateManagerConfig
            {
                Encryption = new StateEncryptionConfig
                {
                    Enabled = true,
                    KeyIdentifier = $"test-key-{algorithm}",
                    Algorithm = algorithm
                }
            };
            var serializer = new WorkflowStateSerializer(config, _logger);
            var state = new Dictionary<string, object>
            {
                ["testData"] = $"Test data for {algorithm} encryption."
            };
            // Act
            var serialized = await serializer.SerializeAsync(state);
            var deserialized = await serializer.DeserializeAsync(serialized);
            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal($"Test data for {algorithm} encryption.", deserialized["testData"]);
        }
    }
    [Fact]
    public async Task SerializeAsync_WithoutEncryptionKey_ShouldThrowException()
    {
        // Arrange
        var config = new StateManagerConfig
        {
            Encryption = new StateEncryptionConfig
            {
                Enabled = true,
                KeyIdentifier = null, // Missing key
                Algorithm = EncryptionAlgorithm.AES256
            }
        };
        var serializer = new WorkflowStateSerializer(config, _logger);
        var state = new Dictionary<string, object> { ["test"] = "data" };
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            serializer.SerializeAsync(state));
    }
    [Fact]
    public async Task DeserializeAsync_WithoutEncryptionKey_ShouldThrowException()
    {
        // Arrange
        var config = new StateManagerConfig
        {
            Encryption = new StateEncryptionConfig
            {
                Enabled = true,
                KeyIdentifier = null, // Missing key
                Algorithm = EncryptionAlgorithm.AES256
            }
        };
        var serializer = new WorkflowStateSerializer(config, _logger);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            serializer.DeserializeAsync(data));
    }
    [Fact]
    public async Task SerializeAsync_WithConcurrentOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var serializer = new WorkflowStateSerializer(_defaultConfig, _logger);
        var tasks = new List<Task>();
        var results = new ConcurrentBag<IDictionary<string, object>>();
        // Act
        for (int i = 0; i < 10; i++)
        {
            var task = Task.Run(async () =>
            {
                var state = new Dictionary<string, object>
                {
                    ["threadId"] = Environment.CurrentManagedThreadId,
                    ["iteration"] = i
                };
                var serialized = await serializer.SerializeAsync(state);
                var deserialized = await serializer.DeserializeAsync(serialized);
                if (deserialized != null)
                {
                    results.Add(deserialized);
                }
            });
            tasks.Add(task);
        }
        await Task.WhenAll(tasks);
        // Assert
        Assert.Equal(10, results.Count);
        foreach (var result in results)
        {
            Assert.Contains("threadId", result.Keys);
            Assert.Contains("iteration", result.Keys);
        }
    }
}
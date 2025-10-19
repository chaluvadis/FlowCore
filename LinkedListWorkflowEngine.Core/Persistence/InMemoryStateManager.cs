namespace LinkedListWorkflowEngine.Core.Persistence;
/// <summary>
/// In-memory implementation of IStateManager for testing and simple scenarios.
/// Not suitable for production use with long-running workflows.
/// </summary>
public class InMemoryStateManager : IStateManager
{
    private readonly ConcurrentDictionary<string, WorkflowExecutionState> _states = new();
    private readonly ILogger<InMemoryStateManager>? _logger;
    private readonly StateManagerConfig _config;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the InMemoryStateManager class.
    /// </summary>
    /// <param name="config">Configuration for the state manager.</param>
    /// <param name="logger">Optional logger.</param>
    public InMemoryStateManager(StateManagerConfig? config = null, ILogger<InMemoryStateManager>? logger = null)
    {
        _config = config ?? new StateManagerConfig();
        _logger = logger;
    }
    /// <summary>
    /// Saves the workflow state with the specified identifier.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <param name="state">The state data to save.</param>
    /// <param name="metadata">Additional metadata about the state.</param>
    /// <returns>A task representing the save operation.</returns>
    public async Task SaveStateAsync(
        string workflowId,
        Guid executionId,
        IDictionary<string, object> state,
        WorkflowStateMetadata? metadata = null)
    {
        ThrowIfDisposed();
        var key = GetStateKey(workflowId, executionId);
        var serializedState = await SerializeStateAsync(state);
        var executionState = new WorkflowExecutionState
        {
            WorkflowId = workflowId,
            ExecutionId = executionId,
            StateData = serializedState,
            Metadata = metadata ?? CreateMetadata(workflowId, executionId, WorkflowStatus.Running),
            LastAccessed = DateTime.UtcNow
        };
        _states[key] = executionState;
        _logger?.LogDebug("Saved state for workflow {WorkflowId}, execution {ExecutionId}",
            workflowId, executionId);
    }
    /// <summary>
    /// Loads the workflow state for the specified identifier.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <returns>The loaded state data, or null if not found.</returns>
    public async Task<IDictionary<string, object>?> LoadStateAsync(string workflowId, Guid executionId)
    {
        ThrowIfDisposed();
        var key = GetStateKey(workflowId, executionId);
        if (!_states.TryGetValue(key, out var executionState))
        {
            _logger?.LogDebug("State not found for workflow {WorkflowId}, execution {ExecutionId}",
                workflowId, executionId);
            return null;
        }
        executionState.LastAccessed = DateTime.UtcNow;
        var state = await DeserializeStateAsync(executionState.StateData);
        _logger?.LogDebug("Loaded state for workflow {WorkflowId}, execution {ExecutionId}",
            workflowId, executionId);
        return state;
    }
    /// <summary>
    /// Deletes the workflow state for the specified identifier.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <returns>A task representing the delete operation.</returns>
    public async Task DeleteStateAsync(string workflowId, Guid executionId)
    {
        ThrowIfDisposed();
        var key = GetStateKey(workflowId, executionId);
        _states.TryRemove(key, out _);
        _logger?.LogDebug("Deleted state for workflow {WorkflowId}, execution {ExecutionId}",
            workflowId, executionId);
    }
    /// <summary>
    /// Checks if state exists for the specified identifier.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <returns>True if state exists, false otherwise.</returns>
    public async Task<bool> StateExistsAsync(string workflowId, Guid executionId)
    {
        ThrowIfDisposed();
        var key = GetStateKey(workflowId, executionId);
        return _states.ContainsKey(key);
    }
    /// <summary>
    /// Gets all execution IDs for a specific workflow.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <returns>A collection of execution IDs.</returns>
    public async Task<IEnumerable<Guid>> GetExecutionIdsAsync(string workflowId)
    {
        ThrowIfDisposed();
        var executionIds = _states
            .Where(kvp => kvp.Key.StartsWith($"{workflowId}:"))
            .Select(kvp => kvp.Value.ExecutionId)
            .Distinct()
            .ToList();
        return executionIds;
    }
    /// <summary>
    /// Gets the metadata for a specific workflow execution.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <returns>The metadata for the execution, or null if not found.</returns>
    public async Task<WorkflowStateMetadata?> GetStateMetadataAsync(string workflowId, Guid executionId)
    {
        ThrowIfDisposed();
        var key = GetStateKey(workflowId, executionId);
        if (_states.TryGetValue(key, out var executionState))
        {
            executionState.LastAccessed = DateTime.UtcNow;
            return executionState.Metadata;
        }
        return null;
    }
    /// <summary>
    /// Updates the metadata for a workflow execution.
    /// </summary>
    /// <param name="workflowId">The unique identifier of the workflow.</param>
    /// <param name="executionId">The unique identifier of the workflow execution.</param>
    /// <param name="metadata">The updated metadata.</param>
    /// <returns>A task representing the update operation.</returns>
    public async Task UpdateStateMetadataAsync(string workflowId, Guid executionId, WorkflowStateMetadata metadata)
    {
        ThrowIfDisposed();
        var key = GetStateKey(workflowId, executionId);
        if (_states.TryGetValue(key, out var executionState))
        {
            executionState.Metadata = metadata;
            executionState.LastAccessed = DateTime.UtcNow;
        }
    }
    /// <summary>
    /// Cleans up old workflow states based on the specified criteria.
    /// </summary>
    /// <param name="olderThan">Delete states older than this timestamp.</param>
    /// <param name="workflowId">Optional workflow ID filter.</param>
    /// <param name="status">Optional status filter.</param>
    /// <returns>The number of states deleted.</returns>
    public async Task<int> CleanupOldStatesAsync(DateTime olderThan, string? workflowId = null, WorkflowStatus? status = null)
    {
        ThrowIfDisposed();
        var keysToRemove = _states
            .Where(kvp =>
            {
                var metadata = kvp.Value.Metadata;
                // Check age
                if (metadata.CreatedAt > olderThan)
                    return false;
                // Check workflow ID filter
                if (workflowId != null && metadata.WorkflowId != workflowId)
                    return false;
                // Check status filter
                if (status.HasValue && metadata.Status != status.Value)
                    return false;
                return true;
            })
            .Select(kvp => kvp.Key)
            .ToList();
        var deletedCount = 0;
        foreach (var key in keysToRemove)
        {
            _states.TryRemove(key, out _);
            deletedCount++;
        }
        _logger?.LogInformation("Cleaned up {DeletedCount} old workflow states", deletedCount);
        return deletedCount;
    }
    /// <summary>
    /// Gets statistics about stored workflow states.
    /// </summary>
    /// <returns>Statistics about the stored states.</returns>
    public async Task<StateManagerStatistics> GetStatisticsAsync()
    {
        ThrowIfDisposed();
        var states = _states.Values.ToList();
        var stats = new StateManagerStatistics
        {
            TotalStates = states.Count,
            TotalSizeBytes = states.Sum(s => s.StateData.Length),
            ActiveExecutions = states.Count(s => s.Metadata.Status == WorkflowStatus.Running),
            CompletedExecutions = states.Count(s => s.Metadata.Status == WorkflowStatus.Completed),
            FailedExecutions = states.Count(s => s.Metadata.Status == WorkflowStatus.Failed),
            GeneratedAt = DateTime.UtcNow
        };
        return stats;
    }
    /// <summary>
    /// Disposes of the state manager and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _states.Clear();
            _disposed = true;
        }
    }
    /// <summary>
    /// Serializes state data for storage.
    /// </summary>
    private async Task<byte[]> SerializeStateAsync(IDictionary<string, object> state)
    {
        // PLACEHOLDER: Implement proper JSON serialization with System.Text.Json
        // TODO: Add compression and encryption based on config
        // TODO: Handle complex object serialization
        using var memoryStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memoryStream, state);
        return memoryStream.ToArray();
    }
    /// <summary>
    /// Deserializes state data from storage.
    /// </summary>
    private async Task<IDictionary<string, object>?> DeserializeStateAsync(byte[] data)
    {
        try
        {
            // PLACEHOLDER: Implement proper JSON deserialization with System.Text.Json
            // TODO: Add decompression and decryption based on config
            // TODO: Handle complex object deserialization
            using var memoryStream = new MemoryStream(data);
            var state = await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(memoryStream);
            return state ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to deserialize workflow state");
            return null;
        }
    }
    /// <summary>
    /// Creates a state key from workflow and execution IDs.
    /// </summary>
    private static string GetStateKey(string workflowId, Guid executionId)
    {
        return $"{workflowId}:{executionId}";
    }
    /// <summary>
    /// Creates default metadata for a workflow execution.
    /// </summary>
    private static WorkflowStateMetadata CreateMetadata(string workflowId, Guid executionId, WorkflowStatus status)
    {
        return new WorkflowStateMetadata(
            workflowId,
            executionId,
            status,
            stateSize: 0,
            workflowVersion: "1.0.0");
    }
    /// <summary>
    /// Throws an ObjectDisposedException if the state manager has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryStateManager));
        }
    }
}
/// <summary>
/// Internal class for storing workflow execution state in memory.
/// </summary>
internal class WorkflowExecutionState
{
    /// <summary>
    /// Gets the workflow ID.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;
    /// <summary>
    /// Gets the execution ID.
    /// </summary>
    public Guid ExecutionId { get; set; }
    /// <summary>
    /// Gets the serialized state data.
    /// </summary>
    public byte[] StateData { get; set; } = Array.Empty<byte>();
    /// <summary>
    /// Gets the metadata for this execution.
    /// </summary>
    public WorkflowStateMetadata Metadata { get; set; } = new WorkflowStateMetadata("", Guid.Empty, WorkflowStatus.Running);
    /// <summary>
    /// Gets the timestamp when this state was last accessed.
    /// </summary>
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
}
/// <summary>
/// Helper class for serializing DateTime objects.
/// </summary>
internal class SerializableDateTime
{
    public long Ticks { get; set; }
    public int Kind { get; set; }
}
/// <summary>
/// Helper class for serializing TimeSpan objects.
/// </summary>
internal class SerializableTimeSpan
{
    public long Ticks { get; set; }
}
/// <summary>
/// Helper class for serializing Guid objects.
/// </summary>
internal class SerializableGuid
{
    public string Value { get; set; } = string.Empty;
}
/// <summary>
/// Custom JSON converter for object dictionaries.
/// </summary>
internal class ObjectDictionaryConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return default;
        }
        var dictionary = new Dictionary<string, object>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }
            var propertyName = reader.GetString();
            if (string.IsNullOrEmpty(propertyName))
            {
                reader.Skip();
                continue;
            }
            reader.Read();
            var value = ReadValue(ref reader, options);
            dictionary[propertyName] = value;
        }
        return dictionary;
    }
    public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key);
            WriteValue(writer, kvp.Value, options);
        }
        writer.WriteEndObject();
    }
    private object ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null!,
            JsonTokenType.StartObject => Read(ref reader, typeof(Dictionary<string, object>), options)!,
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            _ => throw new JsonException($"Unexpected token type: {reader.TokenType}")
        };
    }
    private object ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var list = new List<object>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(ReadValue(ref reader, options));
        }
        return list;
    }
    private void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string stringValue:
                writer.WriteStringValue(stringValue);
                break;
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case int intValue:
                writer.WriteNumberValue(intValue);
                break;
            case long longValue:
                writer.WriteNumberValue(longValue);
                break;
            case float floatValue:
                writer.WriteNumberValue(floatValue);
                break;
            case double doubleValue:
                writer.WriteNumberValue(doubleValue);
                break;
            case DateTime dateTimeValue:
                writer.WriteStringValue(dateTimeValue);
                break;
            case Dictionary<string, object> dictValue:
                Write(writer, dictValue, options);
                break;
            case List<object> listValue:
                writer.WriteStartArray();
                foreach (var item in listValue)
                {
                    WriteValue(writer, item, options);
                }
                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
namespace FlowCore.CodeExecution.Serialization;

/// <summary>
/// Interface for serializing and deserializing code execution state.
/// Supports persistence of workflow state across execution sessions.
/// </summary>
public interface ICodeExecutionStateSerializer
{
    /// <summary>
    /// Serializes the code execution state to a persistent format.
    /// </summary>
    /// <param name="state">The state to serialize.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>The serialized state data.</returns>
    Task<string> SerializeAsync(CodeExecutionState state, SerializationOptions? options = null);

    /// <summary>
    /// Deserializes code execution state from a persistent format.
    /// </summary>
    /// <param name="serializedData">The serialized state data.</param>
    /// <param name="options">Deserialization options.</param>
    /// <returns>The deserialized state.</returns>
    Task<CodeExecutionState> DeserializeAsync(string serializedData, SerializationOptions? options = null);

    /// <summary>
    /// Validates that serialized data can be successfully deserialized.
    /// </summary>
    /// <param name="serializedData">The serialized data to validate.</param>
    /// <returns>True if the data is valid, false otherwise.</returns>
    Task<bool> ValidateSerializedDataAsync(string serializedData);

    /// <summary>
    /// Gets the supported serialization format.
    /// </summary>
    string SupportedFormat { get; }

    /// <summary>
    /// Gets the version of the serialization format.
    /// </summary>
    string FormatVersion { get; }
}

/// <summary>
/// Represents the complete state of a code execution session that can be persisted.
/// </summary>
public class CodeExecutionState
{
    /// <summary>
    /// Gets or sets the unique identifier for this execution session.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the workflow name associated with this execution.
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the block name where this state was captured.
    /// </summary>
    public string BlockName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this state was captured.
    /// </summary>
    public DateTime CapturedAt { get; set; }

    /// <summary>
    /// Gets or sets the workflow state dictionary.
    /// </summary>
    public Dictionary<string, object> WorkflowState { get; set; } = new();

    /// <summary>
    /// Gets or sets the async-specific state (if any).
    /// </summary>
    public Dictionary<string, object> AsyncState { get; set; } = new();

    /// <summary>
    /// Gets or sets the code execution configuration used.
    /// </summary>
    public CodeExecutionConfig? ExecutionConfig { get; set; }

    /// <summary>
    /// Gets or sets the async execution configuration used.
    /// </summary>
    public AsyncExecutionConfig? AsyncConfig { get; set; }

    /// <summary>
    /// Gets or sets execution metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the serialization format version.
    /// </summary>
    public string FormatVersion { get; set; } = "1.0";

    /// <summary>
    /// Gets or sets custom data that can be persisted with the state.
    /// </summary>
    public Dictionary<string, object> CustomData { get; set; } = new();
}

/// <summary>
/// Options for controlling serialization behavior.
/// </summary>
public class SerializationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to include sensitive data in serialization.
    /// </summary>
    public bool IncludeSensitiveData { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to compress the serialized data.
    /// </summary>
    public bool CompressData { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to encrypt the serialized data.
    /// </summary>
    public bool EncryptData { get; set; }

    /// <summary>
    /// Gets or sets the encryption key to use (if encryption is enabled).
    /// </summary>
    public string? EncryptionKey { get; set; }

    /// <summary>
    /// Gets or sets the maximum size of data to serialize (in bytes).
    /// </summary>
    public long MaxDataSize { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Gets or sets keys that should be excluded from serialization.
    /// </summary>
    public HashSet<string> ExcludedKeys { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to include stack trace information.
    /// </summary>
    public bool IncludeStackTrace { get; set; }

    /// <summary>
    /// Gets the default serialization options.
    /// </summary>
    public static SerializationOptions Default => new();

    /// <summary>
    /// Gets secure serialization options that exclude sensitive data.
    /// </summary>
    public static SerializationOptions Secure => new()
    {
        IncludeSensitiveData = false,
        CompressData = true,
        EncryptData = true,
        ExcludedKeys = new HashSet<string> { "password", "secret", "key", "token", "credential" }
    };
}

/// <summary>
/// JSON-based implementation of code execution state serialization.
/// </summary>
public class JsonCodeExecutionStateSerializer : ICodeExecutionStateSerializer
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger? _logger;

    /// <summary>
    /// Gets the supported serialization format.
    /// </summary>
    public string SupportedFormat => "JSON";

    /// <summary>
    /// Gets the version of the serialization format.
    /// </summary>
    public string FormatVersion => "1.0";

    /// <summary>
    /// Initializes a new instance of the JsonCodeExecutionStateSerializer.
    /// </summary>
    /// <param name="logger">Optional logger for serialization operations.</param>
    public JsonCodeExecutionStateSerializer(ILogger? logger = null)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new ObjectToInferredTypesConverter() }
        };
    }

    /// <summary>
    /// Serializes the code execution state to a JSON format.
    /// </summary>
    /// <param name="state">The state to serialize.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>The serialized state data as JSON.</returns>
    public async Task<string> SerializeAsync(CodeExecutionState state, SerializationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        var serializationOptions = options ?? SerializationOptions.Default;

        try
        {
            _logger?.LogDebug("Starting serialization of execution state {ExecutionId}", state.ExecutionId);

            // Create a copy of the state to modify for serialization
            var stateToSerialize = PrepareStateForSerialization(state, serializationOptions);

            // Serialize to JSON
            var json = JsonSerializer.Serialize(stateToSerialize, _jsonOptions);

            // Apply post-processing (compression, encryption)
            var processedData = await PostProcessSerializedDataAsync(json, serializationOptions).ConfigureAwait(false);

            _logger?.LogDebug("Successfully serialized execution state {ExecutionId}, size: {Size} bytes",
                state.ExecutionId, processedData.Length);

            return processedData;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to serialize execution state {ExecutionId}", state.ExecutionId);
            throw new InvalidOperationException($"Failed to serialize execution state: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deserializes code execution state from JSON format.
    /// </summary>
    /// <param name="serializedData">The serialized state data as JSON.</param>
    /// <param name="options">Deserialization options.</param>
    /// <returns>The deserialized state.</returns>
    public async Task<CodeExecutionState> DeserializeAsync(string serializedData, SerializationOptions? options = null)
    {
        if (string.IsNullOrEmpty(serializedData))
        {
            throw new ArgumentException("Serialized data cannot be null or empty", nameof(serializedData));
        }

        var serializationOptions = options ?? SerializationOptions.Default;

        try
        {
            _logger?.LogDebug("Starting deserialization of execution state, data size: {Size} bytes", serializedData.Length);

            // Apply pre-processing (decompression, decryption)
            var processedData = await PreProcessSerializedDataAsync(serializedData, serializationOptions).ConfigureAwait(false);

            // Deserialize from JSON
            var state = JsonSerializer.Deserialize<CodeExecutionState>(processedData, _jsonOptions);
            if (state == null)
            {
                throw new InvalidOperationException("Deserialization resulted in null state");
            }

            // Post-process the deserialized state
            var finalState = PostProcessDeserializedState(state, serializationOptions);

            _logger?.LogDebug("Successfully deserialized execution state {ExecutionId}", finalState.ExecutionId);

            return finalState;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to deserialize execution state");
            throw new InvalidOperationException($"Failed to deserialize execution state: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates that serialized JSON data can be successfully deserialized.
    /// </summary>
    /// <param name="serializedData">The serialized data to validate.</param>
    /// <returns>True if the data is valid, false otherwise.</returns>
    public async Task<bool> ValidateSerializedDataAsync(string serializedData)
    {
        try
        {
            var state = await DeserializeAsync(serializedData).ConfigureAwait(false);
            return state != null && state.ExecutionId != Guid.Empty;
        }
        catch
        {
            return false;
        }
    }

    private CodeExecutionState PrepareStateForSerialization(CodeExecutionState state, SerializationOptions options)
    {
        var copy = new CodeExecutionState
        {
            ExecutionId = state.ExecutionId,
            WorkflowName = state.WorkflowName,
            BlockName = state.BlockName,
            CapturedAt = state.CapturedAt,
            FormatVersion = FormatVersion,
            WorkflowState = new Dictionary<string, object>(),
            AsyncState = new Dictionary<string, object>(),
            Metadata = new Dictionary<string, object>(),
            CustomData = new Dictionary<string, object>()
        };

        // Filter workflow state
        foreach (var kvp in state.WorkflowState)
        {
            if (ShouldIncludeKey(kvp.Key, kvp.Value, options))
            {
                copy.WorkflowState[kvp.Key] = kvp.Value;
            }
        }

        // Filter async state
        foreach (var kvp in state.AsyncState)
        {
            if (ShouldIncludeKey(kvp.Key, kvp.Value, options))
            {
                copy.AsyncState[kvp.Key] = kvp.Value;
            }
        }

        // Copy metadata
        foreach (var kvp in state.Metadata)
        {
            copy.Metadata[kvp.Key] = kvp.Value;
        }

        // Copy custom data
        foreach (var kvp in state.CustomData)
        {
            if (ShouldIncludeKey(kvp.Key, kvp.Value, options))
            {
                copy.CustomData[kvp.Key] = kvp.Value;
            }
        }

        // Copy configuration if not sensitive
        if (options.IncludeSensitiveData)
        {
            copy.ExecutionConfig = state.ExecutionConfig;
            copy.AsyncConfig = state.AsyncConfig;
        }

        return copy;
    }

    private static bool ShouldIncludeKey(string key, object value, SerializationOptions options)
    {
        // Check excluded keys
        if (options.ExcludedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check sensitive data patterns
        if (!options.IncludeSensitiveData)
        {
            var lowerKey = key.ToLowerInvariant();
            if (lowerKey.Contains("password") || lowerKey.Contains("secret") ||
                lowerKey.Contains("key") || lowerKey.Contains("token"))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<string> PostProcessSerializedDataAsync(string json, SerializationOptions options)
    {
        var data = json;

        // Apply compression if requested
        if (options.CompressData)
        {
            data = await CompressDataAsync(data).ConfigureAwait(false);
        }

        // Apply encryption if requested
        if (options.EncryptData && !string.IsNullOrEmpty(options.EncryptionKey))
        {
            data = await EncryptDataAsync(data, options.EncryptionKey).ConfigureAwait(false);
        }

        return data;
    }

    private static async Task<string> PreProcessSerializedDataAsync(string data, SerializationOptions options)
    {
        var processedData = data;

        // Apply decryption if needed
        if (options.EncryptData && !string.IsNullOrEmpty(options.EncryptionKey))
        {
            processedData = await DecryptDataAsync(processedData, options.EncryptionKey).ConfigureAwait(false);
        }

        // Apply decompression if needed
        if (options.CompressData)
        {
            processedData = await DecompressDataAsync(processedData).ConfigureAwait(false);
        }

        return processedData;
    }

    private CodeExecutionState PostProcessDeserializedState(CodeExecutionState state, SerializationOptions options)
    {
        // Validate the state
        if (state.ExecutionId == Guid.Empty)
        {
            state.ExecutionId = Guid.NewGuid();
        }

        if (string.IsNullOrEmpty(state.FormatVersion))
        {
            state.FormatVersion = FormatVersion;
        }

        return state;
    }

    private static async Task<string> CompressDataAsync(string data)
    {
        // Placeholder for compression implementation
        // In a real implementation, you would use GZip or another compression algorithm
        await Task.Delay(1).ConfigureAwait(false); // Simulate async operation
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(data));
    }

    private static async Task<string> DecompressDataAsync(string compressedData)
    {
        // Placeholder for decompression implementation
        await Task.Delay(1).ConfigureAwait(false); // Simulate async operation
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(compressedData));
    }

    private static async Task<string> EncryptDataAsync(string data, string key)
    {
        // Placeholder for encryption implementation
        // In a real implementation, you would use AES or another encryption algorithm
        await Task.Delay(1).ConfigureAwait(false); // Simulate async operation
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"ENCRYPTED:{data}"));
    }

    private static async Task<string> DecryptDataAsync(string encryptedData, string key)
    {
        // Placeholder for decryption implementation
        await Task.Delay(1).ConfigureAwait(false); // Simulate async operation
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encryptedData));
        return decoded.StartsWith("ENCRYPTED:") ? decoded.Substring("ENCRYPTED:".Length) : decoded;
    }
}

/// <summary>
/// Custom JSON converter for handling object types in state dictionaries.
/// </summary>
public class ObjectToInferredTypesConverter : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.Number when reader.TryGetInt64(out var l) => l,
        JsonTokenType.Number => reader.GetDouble(),
        JsonTokenType.String when reader.TryGetDateTime(out var datetime) => datetime,
        JsonTokenType.String => reader.GetString()!,
        JsonTokenType.StartObject => JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options)!,
        JsonTokenType.StartArray => JsonSerializer.Deserialize<object[]>(ref reader, options)!,
        _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
    };

    public override void Write(Utf8JsonWriter writer, object objectToWrite, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);
}

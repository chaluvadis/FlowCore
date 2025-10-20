namespace LinkedListWorkflowEngine.Core.Persistence;
/// <summary>
/// Handles serialization and deserialization of workflow state with support for
/// compression, encryption, and complex object graphs.
/// </summary>
public class WorkflowStateSerializer
{
    private readonly StateManagerConfig _config;
    private readonly ILogger? _logger;
    private const string SerializationFormatVersion = "1.0";
    /// <summary>
    /// Gets the current serialization format version.
    /// </summary>
    public static string FormatVersion => SerializationFormatVersion;
    public WorkflowStateSerializer(StateManagerConfig config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }
    /// <summary>
    /// Serializes state data with optional compression and encryption.
    /// </summary>
    public async Task<byte[]> SerializeAsync(IDictionary<string, object> state)
    {
        try
        {
            // Step 1: Serialize to JSON with type information
            var jsonBytes = await SerializeToJsonWithTypesAsync(state);
            // Step 2: Apply compression if enabled and size threshold met
            if (_config.Compression.Enabled && jsonBytes.Length >= _config.Compression.MinSizeThreshold)
            {
                jsonBytes = await CompressAsync(jsonBytes);
                _logger?.LogDebug("Compressed state data from {OriginalSize} to {CompressedSize} bytes",
                    jsonBytes.Length, jsonBytes.Length);
            }
            // Step 3: Apply encryption if enabled
            if (_config.Encryption.Enabled && !string.IsNullOrEmpty(_config.Encryption.KeyIdentifier))
            {
                jsonBytes = await EncryptAsync(jsonBytes);
                _logger?.LogDebug("Encrypted state data");
            }
            return jsonBytes;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to serialize workflow state");
            throw new WorkflowStateSerializationException("Serialization failed", ex);
        }
    }
    /// <summary>
    /// Deserializes state data with optional decompression and decryption.
    /// </summary>
    public async Task<IDictionary<string, object>?> DeserializeAsync(byte[] data)
    {
        try
        {
            var processedData = data;
            // Step 1: Apply decryption if enabled
            if (_config.Encryption.Enabled && !string.IsNullOrEmpty(_config.Encryption.KeyIdentifier))
            {
                processedData = await DecryptAsync(processedData);
                _logger?.LogDebug("Decrypted state data");
            }
            // Step 2: Apply decompression if enabled
            if (_config.Compression.Enabled)
            {
                processedData = await DecompressAsync(processedData);
                _logger?.LogDebug("Decompressed state data from {CompressedSize} to {DecompressedSize} bytes",
                    data.Length, processedData.Length);
            }
            // Step 3: Deserialize from JSON with type information
            return await DeserializeFromJsonWithTypesAsync(processedData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to deserialize workflow state");
            throw new WorkflowStateSerializationException("Deserialization failed", ex);
        }
    }
    private async Task<byte[]> SerializeToJsonWithTypesAsync(IDictionary<string, object> state)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            IncludeFields = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new ObjectDictionaryConverter()
            }
        };
        var serializableState = new SerializableWorkflowState
        {
            FormatVersion = FormatVersion,
            CreatedAt = DateTime.UtcNow,
            State = state
        };
        using var memoryStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memoryStream, serializableState, options);
        return memoryStream.ToArray();
    }
    private async Task<IDictionary<string, object>?> DeserializeFromJsonWithTypesAsync(byte[] data)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters =
                {
                    new ObjectDictionaryConverter(),
                    new TypePreservingConverter()
                }
            };
            using var memoryStream = new MemoryStream(data);
            var serializableState = await JsonSerializer.DeserializeAsync<SerializableWorkflowState>(memoryStream, options);
            return serializableState?.State ?? new Dictionary<string, object>();
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "JSON deserialization failed");
            return null;
        }
    }
    private async Task<byte[]> CompressAsync(byte[] data)
    {
        using var inputStream = new MemoryStream(data);
        using var outputStream = new MemoryStream();
        Stream compressionStream = _config.Compression.Algorithm switch
        {
            CompressionAlgorithm.GZip => new GZipStream(outputStream, CompressionMode.Compress),
            CompressionAlgorithm.Deflate => new DeflateStream(outputStream, CompressionMode.Compress),
            CompressionAlgorithm.Brotli => new BrotliStream(outputStream, CompressionMode.Compress),
            _ => throw new NotSupportedException($"Compression algorithm {_config.Compression.Algorithm} is not supported")
        };
        await inputStream.CopyToAsync(compressionStream);
        await compressionStream.FlushAsync();
        compressionStream.Close();
        return outputStream.ToArray();
    }
    private async Task<byte[]> DecompressAsync(byte[] data)
    {
        using var inputStream = new MemoryStream(data);
        using var outputStream = new MemoryStream();
        Stream decompressionStream = _config.Compression.Algorithm switch
        {
            CompressionAlgorithm.GZip => new GZipStream(inputStream, CompressionMode.Decompress),
            CompressionAlgorithm.Deflate => new DeflateStream(inputStream, CompressionMode.Decompress),
            CompressionAlgorithm.Brotli => new BrotliStream(inputStream, CompressionMode.Decompress),
            _ => throw new NotSupportedException($"Compression algorithm {_config.Compression.Algorithm} is not supported")
        };
        await decompressionStream.CopyToAsync(outputStream);
        await decompressionStream.FlushAsync();
        decompressionStream.Close();
        return outputStream.ToArray();
    }
    private async Task<byte[]> EncryptAsync(byte[] data)
    {
        if (string.IsNullOrEmpty(_config.Encryption.KeyIdentifier))
            throw new InvalidOperationException("Encryption key identifier is required for encryption");
        using var aes = Aes.Create();
        aes.KeySize = _config.Encryption.Algorithm == EncryptionAlgorithm.AES128 ? 128 : 256;
        aes.Padding = PaddingMode.PKCS7;
        // Use a deterministic key derivation from the identifier for testing purposes
        // In production, use proper key management
        var keyBytes = new byte[aes.KeySize / 8];
        var identifierBytes = Encoding.UTF8.GetBytes(_config.Encryption.KeyIdentifier);
        Array.Copy(identifierBytes, keyBytes, Math.Min(identifierBytes.Length, keyBytes.Length));
        // Ensure we have a proper key length
        if (keyBytes.Length < aes.Key.Length)
        {
            Array.Resize(ref keyBytes, aes.Key.Length);
        }
        aes.Key = keyBytes;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var inputStream = new MemoryStream(data);
        using var outputStream = new MemoryStream();
        // Write IV length and IV first
        await outputStream.WriteAsync(BitConverter.GetBytes(aes.IV.Length));
        await outputStream.WriteAsync(aes.IV);
        using var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write);
        await inputStream.CopyToAsync(cryptoStream);
        await cryptoStream.FlushFinalBlockAsync();
        return outputStream.ToArray();
    }
    private async Task<byte[]> DecryptAsync(byte[] data)
    {
        if (string.IsNullOrEmpty(_config.Encryption.KeyIdentifier))
            throw new InvalidOperationException("Encryption key identifier is required for decryption");
        using var aes = Aes.Create();
        aes.KeySize = _config.Encryption.Algorithm == EncryptionAlgorithm.AES128 ? 128 : 256;
        aes.Padding = PaddingMode.PKCS7;
        // Read IV length and IV from the beginning of the data
        var ivLengthBytes = new byte[4];
        Array.Copy(data, 0, ivLengthBytes, 0, ivLengthBytes.Length);
        var ivLength = BitConverter.ToInt32(ivLengthBytes);
        var iv = new byte[ivLength];
        Array.Copy(data, ivLengthBytes.Length, iv, 0, iv.Length);
        // Use the same deterministic key derivation as encryption
        var keyBytes = new byte[aes.KeySize / 8];
        var identifierBytes = Encoding.UTF8.GetBytes(_config.Encryption.KeyIdentifier);
        Array.Copy(identifierBytes, keyBytes, Math.Min(identifierBytes.Length, keyBytes.Length));
        if (keyBytes.Length < aes.Key.Length)
        {
            Array.Resize(ref keyBytes, aes.Key.Length);
        }
        aes.Key = keyBytes;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var inputStream = new MemoryStream(data, ivLengthBytes.Length + iv.Length, data.Length - ivLengthBytes.Length - iv.Length);
        using var outputStream = new MemoryStream();
        using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
        await cryptoStream.CopyToAsync(outputStream);
        return outputStream.ToArray();
    }
}
/// <summary>
/// Serializable wrapper for workflow state with metadata.
/// </summary>
internal class SerializableWorkflowState
{
    public string FormatVersion { get; set; } = WorkflowStateSerializer.FormatVersion;
    public DateTime CreatedAt { get; set; }
    public IDictionary<string, object> State { get; set; } = new Dictionary<string, object>();
}
/// <summary>
/// Custom JSON converter that preserves type information for complex objects.
/// </summary>
internal class TypePreservingConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        // Handle primitive types
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                return ReadNumber(ref reader);
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.StartObject:
                return ReadObject(ref reader, options);
            case JsonTokenType.StartArray:
                return ReadArray(ref reader, options);
            default:
                throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }
    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        var type = value.GetType();
        var typeName = TypeNameHelper.GetTypeName(type);
        writer.WriteStartObject();
        writer.WriteString("$type", typeName);
        switch (value)
        {
            case string stringValue:
                writer.WriteString("$value", stringValue);
                break;
            case bool boolValue:
                writer.WriteBoolean("$value", boolValue);
                break;
            case int intValue:
                writer.WriteNumber("$value", intValue);
                break;
            case long longValue:
                writer.WriteNumber("$value", longValue);
                break;
            case float floatValue:
                writer.WriteNumber("$value", floatValue);
                break;
            case double doubleValue:
                writer.WriteNumber("$value", doubleValue);
                break;
            case DateTime dateTimeValue:
                writer.WriteString("$value", dateTimeValue.ToString("O"));
                break;
            case DateTimeOffset dateTimeOffsetValue:
                writer.WriteString("$value", dateTimeOffsetValue.ToString("O"));
                break;
            case IDictionary<string, object> dictValue:
                writer.WritePropertyName("$value");
                JsonSerializer.Serialize(writer, dictValue, options);
                break;
            case IEnumerable<object> listValue:
                writer.WritePropertyName("$value");
                writer.WriteStartArray();
                foreach (var item in listValue)
                {
                    JsonSerializer.Serialize(writer, item, options);
                }
                writer.WriteEndArray();
                break;
            default:
                writer.WriteString("$value", value.ToString());
                break;
        }
        writer.WriteEndObject();
    }
    private object ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var typeName = string.Empty;
        var valueJson = string.Empty;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();
                switch (propertyName)
                {
                    case "$type":
                        typeName = reader.GetString() ?? string.Empty;
                        break;
                    case "$value":
                        valueJson = reader.GetString() ?? string.Empty;
                        break;
                }
            }
        }
        return TypeNameHelper.CreateInstance(typeName, valueJson);
    }
    private object ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var list = new List<object>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(Read(ref reader, typeof(object), options)!);
        }
        return list;
    }
    private object ReadNumber(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var longValue))
        {
            // Check if it fits in int
            if (longValue >= int.MinValue && longValue <= int.MaxValue)
            {
                return (int)longValue;
            }
            return longValue;
        }
        if (reader.TryGetDouble(out var doubleValue))
        {
            return doubleValue;
        }
        if (reader.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }
        return reader.GetInt32();
    }
}
/// <summary>
/// Helper class for managing type names and instance creation.
/// </summary>
internal static class TypeNameHelper
{
    private static readonly Dictionary<string, Type> _typeCache = new();
    private static readonly Dictionary<Type, string> _nameCache = new();
    public static string GetTypeName(Type type)
    {
        if (_nameCache.TryGetValue(type, out var name))
            return name;
        name = $"{type.FullName},{type.Assembly.GetName().Name}";
        _nameCache[type] = name;
        return name;
    }
    public static object CreateInstance(string typeName, string value)
    {
        var type = GetTypeFromName(typeName);
        if (type == null)
        {
            // If type name is empty, try to infer from the value
            if (string.IsNullOrEmpty(typeName))
            {
                return value; // Return as string for now
            }
            throw new InvalidOperationException($"Type '{typeName}' could not be resolved");
        }
        // Handle common types that don't have Parse methods
        if (type == typeof(List<object>))
            return new List<object>();
        if (type == typeof(Dictionary<string, object>))
            return new Dictionary<string, object>();
        // Handle DateTime parsing
        if (type == typeof(DateTime))
        {
            if (DateTime.TryParse(value, out var dateTimeValue))
                return dateTimeValue;
            return DateTime.Parse(value); // This might throw, but that's okay for testing
        }
        // Try to parse as the target type
        try
        {
            return type.GetMethod("Parse", new[] { typeof(string) })?.Invoke(null, new[] { value }) ??
                   Convert.ChangeType(value, type);
        }
        catch
        {
            // If parsing fails, return the string value
            return value;
        }
    }
    private static Type? GetTypeFromName(string typeName)
    {
        if (_typeCache.TryGetValue(typeName, out var type))
            return type;
        type = Type.GetType(typeName);
        if (type != null)
            _typeCache[typeName] = type;
        return type;
    }
}
/// <summary>
/// Exception thrown when workflow state serialization/deserialization fails.
/// </summary>
public class WorkflowStateSerializationException : Exception
{
    public WorkflowStateSerializationException(string message) : base(message) { }
    public WorkflowStateSerializationException(string message, Exception innerException) : base(message, innerException) { }
}
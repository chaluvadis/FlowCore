namespace FlowCore.Common;
/// <summary>
/// Configuration options for workflow block factory security.
/// </summary>
public class WorkflowBlockFactorySecurityOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether dynamic assembly loading is allowed.
    /// Default is false for security.
    /// </summary>
    public bool AllowDynamicAssemblyLoading { get; set; } = false;
    /// <summary>
    /// Gets or sets the list of allowed assembly names for dynamic loading.
    /// Only assemblies in this list can be loaded when AllowDynamicAssemblyLoading is true.
    /// </summary>
    public IReadOnlyList<string> AllowedAssemblyNames { get; set; } = new List<string>();
    /// <summary>
    /// Gets or sets a value indicating whether to validate strong-name signatures.
    /// When true, only assemblies with valid strong-name signatures can be loaded.
    /// </summary>
    public bool ValidateStrongNameSignatures { get; set; } = true;
    /// <summary>
    /// Gets or sets the list of allowed public key tokens for strong-name validation.
    /// If empty, all valid strong-name signatures are accepted.
    /// </summary>
    public IReadOnlyList<byte[]> AllowedPublicKeyTokens { get; set; } = new List<byte[]>();
}
/// <summary>
/// Enhanced implementation of the workflow block factory with caching, configuration injection, and security hardening.
/// </summary>
/// <remarks>
/// Initializes a new instance of the WorkflowBlockFactory class.
/// </remarks>
/// <param name="serviceProvider">The service provider for dependency resolution.</param>
/// <param name="securityOptions">Security configuration options for assembly loading.</param>
/// <param name="logger">Optional logger for factory operations.</param>
public class WorkflowBlockFactory(
    IServiceProvider serviceProvider,
    WorkflowBlockFactorySecurityOptions securityOptions,
    ILogger<WorkflowBlockFactory>? logger = null) : IWorkflowBlockFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ConcurrentDictionary<string, Type> _blockCache = new();
    private readonly WorkflowBlockFactorySecurityOptions _securityOptions = securityOptions ?? throw new ArgumentNullException(nameof(securityOptions));

    /// <summary>
    /// Initializes a new instance of the WorkflowBlockFactory class with default security options.
    /// Note: Dynamic assembly loading is disabled by default for security.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="logger">Optional logger for factory operations.</param>
    public WorkflowBlockFactory(IServiceProvider serviceProvider, ILogger<WorkflowBlockFactory>? logger = null)
        : this(serviceProvider, new WorkflowBlockFactorySecurityOptions(), logger)
    {
    }
    /// <summary>
    /// Creates a workflow block from its definition.
    /// </summary>
    /// <param name="blockDefinition">The definition of the block to create.</param>
    /// <returns>The created workflow block, or null if creation failed.</returns>
    public IWorkflowBlock? CreateBlock(WorkflowBlockDefinition blockDefinition)
    {
        var cacheKey = $"{blockDefinition.AssemblyName}:{blockDefinition.Namespace}:{blockDefinition.BlockType}";
        // Check cache first for improved performance
        if (_blockCache.TryGetValue(cacheKey, out var cachedBlockType))
        {
            return CreateBlockInstance(cachedBlockType, blockDefinition);
        }
        try
        {
            // Load assembly if not already loaded
            var assembly = LoadAssembly(blockDefinition.AssemblyName);
            // Get the block type with proper namespace resolution
            var blockType = FindBlockType(assembly, blockDefinition);
            if (blockType == null)
            {
                throw new TypeLoadException($"Could not load type '{blockDefinition.BlockType}' from assembly '{blockDefinition.AssemblyName}'");
            }
            // Cache the type for future use
            _blockCache[cacheKey] = blockType;
            return CreateBlockInstance(blockType, blockDefinition);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create block of type '{BlockType}' from assembly '{AssemblyName}'",
                blockDefinition.BlockType, blockDefinition.AssemblyName);
            return null;
        }
    }
    /// <summary>
    /// Loads an assembly by name with proper error handling and security validation.
    /// </summary>
    private Assembly LoadAssembly(string assemblyName)
    {
        try
        {
            // Check if dynamic assembly loading is allowed
            if (!_securityOptions.AllowDynamicAssemblyLoading)
            {
                throw new SecurityException($"Dynamic assembly loading is disabled for security reasons. Assembly '{assemblyName}' cannot be loaded.");
            }
            // Validate against allowed assembly names whitelist
            if (_securityOptions.AllowedAssemblyNames.Any() &&
                !_securityOptions.AllowedAssemblyNames.Contains(assemblyName, StringComparer.OrdinalIgnoreCase))
            {
                throw new SecurityException($"Assembly '{assemblyName}' is not in the allowed assembly names list.");
            }
            // Try to load from currently loaded assemblies first
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (loadedAssembly != null)
            {
                // Validate already loaded assembly against security criteria
                ValidateAssemblySecurity(loadedAssembly, assemblyName);
                return loadedAssembly;
            }
            // Load assembly by name with security validation
            var assembly = Assembly.Load(assemblyName);
            ValidateAssemblySecurity(assembly, assemblyName);
            return assembly;
        }
        catch (SecurityException)
        {
            throw; // Re-throw security exceptions as-is
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load assembly '{assemblyName}'", ex);
        }
    }
    /// <summary>
    /// Validates an assembly against security criteria including strong-name signatures.
    /// </summary>
    private void ValidateAssemblySecurity(Assembly assembly, string assemblyName)
    {
        // Validate strong-name signature if required
        if (_securityOptions.ValidateStrongNameSignatures)
        {
            try
            {
                var assemblyNameObj = assembly.GetName();
                var publicKey = assemblyNameObj.GetPublicKey();
                if (publicKey == null || publicKey.Length == 0)
                {
                    throw new SecurityException($"Assembly '{assemblyName}' does not have a strong-name signature, which is required for security.");
                }
                // Validate against allowed public key tokens if specified
                if (_securityOptions.AllowedPublicKeyTokens.Any())
                {
                    var publicKeyToken = assemblyNameObj.GetPublicKeyToken();
                    if (publicKeyToken == null || !_securityOptions.AllowedPublicKeyTokens.Any(token =>
                        token.Length == publicKeyToken.Length &&
                        token.SequenceEqual(publicKeyToken)))
                    {
                        throw new SecurityException($"Assembly '{assemblyName}' has a public key token that is not in the allowed list.");
                    }
                }
                logger?.LogDebug("Assembly '{AssemblyName}' passed strong-name signature validation", assemblyName);
            }
            catch (SecurityException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to validate strong-name signature for assembly '{AssemblyName}'", assemblyName);
                throw new SecurityException($"Failed to validate strong-name signature for assembly '{assemblyName}'", ex);
            }
        }
    }
    /// <summary>
    /// Finds the block type within the assembly using namespace resolution.
    /// </summary>
    private Type? FindBlockType(Assembly assembly, WorkflowBlockDefinition blockDefinition)
    {
        var typeName = blockDefinition.BlockType;
        // If namespace is specified, try with full namespace
        if (!string.IsNullOrEmpty(blockDefinition.Namespace))
        {
            var fullTypeName = $"{blockDefinition.Namespace}.{typeName}";
            var blockType = assembly.GetType(fullTypeName, false);
            if (blockType != null)
            {
                return blockType;
            }
        }
        // Try with just the type name
        var simpleType = assembly.GetType(typeName, false);
        if (simpleType != null)
        {
            return simpleType;
        }
        // Search all types in the assembly for a match
        return assembly.GetTypes()
            .FirstOrDefault(t => t.Name == typeName && typeof(IWorkflowBlock).IsAssignableFrom(t));
    }
    /// <summary>
    /// Creates a block instance and injects configuration.
    /// </summary>
    private IWorkflowBlock? CreateBlockInstance(Type blockType, WorkflowBlockDefinition blockDefinition)
    {
        try
        {
            // Try DI first
            var block = _serviceProvider.GetService(blockType) as IWorkflowBlock;
            if (block != null)
            {
                InjectConfiguration(block, blockDefinition.Configuration);
                return block;
            }
            // Fallback to Activator
            var loggerType = typeof(ILogger<>).MakeGenericType(blockType);
            var logger = _serviceProvider.GetService(loggerType);
            block = Activator.CreateInstance(blockType, logger) as IWorkflowBlock;
            if (block != null)
            {
                InjectConfiguration(block, blockDefinition.Configuration);
            }
            return block;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create instance of block type '{BlockType}'", blockType.FullName);
            return null;
        }
    }
    /// <summary>
    /// Injects configuration values into the block using reflection.
    /// </summary>
    private void InjectConfiguration(IWorkflowBlock block, IReadOnlyDictionary<string, object> configuration)
    {
        if (!configuration.Any())
            return;
        var blockType = block.GetType();
        foreach (var config in configuration)
        {
            try
            {
                var property = blockType.GetProperty(config.Key, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    var convertedValue = ConvertValue(config.Value, property.PropertyType);
                    property.SetValue(block, convertedValue);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to inject configuration '{Key}' into block '{BlockType}'",
                    config.Key, blockType.Name);
            }
        }
    }
    /// <summary>
    /// Converts a configuration value to the target type.
    /// </summary>
    private object? ConvertValue(object value, Type targetType)
    {
        if (value == null)
            return null;
        try
        {
            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }
            // Handle common type conversions
            if (targetType == typeof(string))
            {
                return value.ToString();
            }
            if (targetType == typeof(int) && value is string stringValue)
            {
                return int.Parse(stringValue);
            }
            if (targetType == typeof(bool) && value is string boolString)
            {
                return bool.Parse(boolString);
            }
            if (targetType == typeof(TimeSpan) && value is string timeSpanString)
            {
                return TimeSpan.Parse(timeSpanString);
            }
            // Use type converter as fallback
            var converter = System.ComponentModel.TypeDescriptor.GetConverter(targetType);
            if (converter.CanConvertFrom(value.GetType()))
            {
                return converter.ConvertFrom(value);
            }
            return value;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to convert configuration value '{Value}' to type '{TargetType}'",
                value, targetType.Name);
            return null;
        }
    }
}
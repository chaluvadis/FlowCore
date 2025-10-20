namespace LinkedListWorkflowEngine.Core.Common;
/// <summary>
/// Enhanced implementation of the workflow block factory with caching and configuration injection.
/// </summary>
public class WorkflowBlockFactory : IWorkflowBlockFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, Type> _blockCache = new();
    private readonly ILogger<WorkflowBlockFactory>? _logger;
    /// <summary>
    /// Initializes a new instance of the WorkflowBlockFactory class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="logger">Optional logger for factory operations.</param>
    public WorkflowBlockFactory(IServiceProvider serviceProvider, ILogger<WorkflowBlockFactory>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
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
            _logger?.LogError(ex, "Failed to create block of type '{BlockType}' from assembly '{AssemblyName}'",
                blockDefinition.BlockType, blockDefinition.AssemblyName);
            return null;
        }
    }
    /// <summary>
    /// Loads an assembly by name with proper error handling.
    /// </summary>
    private Assembly LoadAssembly(string assemblyName)
    {
        try
        {
            // Try to load from currently loaded assemblies first
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (loadedAssembly != null)
            {
                return loadedAssembly;
            }
            // Load assembly by name
            return Assembly.Load(assemblyName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load assembly '{assemblyName}'", ex);
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
            _logger?.LogError(ex, "Failed to create instance of block type '{BlockType}'", blockType.FullName);
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
                _logger?.LogWarning(ex, "Failed to inject configuration '{Key}' into block '{BlockType}'",
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
            _logger?.LogWarning(ex, "Failed to convert configuration value '{Value}' to type '{TargetType}'",
                value, targetType.Name);
            return null;
        }
    }
}
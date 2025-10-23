using FlowCore.CodeExecution;

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
        try
        {
            // Check if this is a code block definition
            if (IsCodeBlockDefinition(blockDefinition))
            {
                return CreateCodeBlock(blockDefinition);
            }

            // Handle regular block creation
            var cacheKey = $"{blockDefinition.AssemblyName}:{blockDefinition.Namespace}:{blockDefinition.BlockType}";
            // Check cache first for improved performance
            if (_blockCache.TryGetValue(cacheKey, out var cachedBlockType))
            {
                return CreateBlockInstance(cachedBlockType, blockDefinition);
            }

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
                InjectConfiguration(block, blockDefinition.Configuration, blockDefinition.NextBlockOnSuccess, blockDefinition.NextBlockOnFailure);
                return block;
            }
            // Fallback to Activator
            var loggerType = typeof(ILogger<>).MakeGenericType(blockType);
            var logger = _serviceProvider.GetService(loggerType);
            block = Activator.CreateInstance(blockType, logger) as IWorkflowBlock;
            if (block != null)
            {
                InjectConfiguration(block, blockDefinition.Configuration, blockDefinition.NextBlockOnSuccess, blockDefinition.NextBlockOnFailure);
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
    private void InjectConfiguration(IWorkflowBlock block, IReadOnlyDictionary<string, object> configuration, string nextBlockOnSuccess = "", string nextBlockOnFailure = "")
    {
        if (!configuration.Any())
        {
            InjectTransitions(block, nextBlockOnSuccess, nextBlockOnFailure);
            return;
        }

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

        // Always inject transition properties from block definition if not already set in configuration
        InjectTransitions(block, nextBlockOnSuccess, nextBlockOnFailure);
    }

    /// <summary>
    /// Injects transition properties from the block definition into the block instance.
    /// This method is called during block creation to ensure proper workflow transitions.
    /// </summary>
    /// <param name="block">The block instance to configure.</param>
    /// <param name="nextBlockOnSuccess">The next block to execute on success (optional override).</param>
    /// <param name="nextBlockOnFailure">The next block to execute on failure (optional override).</param>
    private void InjectTransitions(IWorkflowBlock block, string? nextBlockOnSuccess = null, string? nextBlockOnFailure = null)
    {
        var blockType = block.GetType();

        try
        {
            // Inject NextBlockOnSuccess if the block supports it
            var successProperty = blockType.GetProperty("NextBlockOnSuccess", BindingFlags.Public | BindingFlags.Instance);
            if (successProperty != null && successProperty.CanWrite)
            {
                var value = nextBlockOnSuccess ?? string.Empty;
                successProperty.SetValue(block, value);
                logger?.LogDebug("Injected NextBlockOnSuccess '{Value}' into block '{BlockType}'",
                    value, blockType.Name);
            }

            // Inject NextBlockOnFailure if the block supports it
            var failureProperty = blockType.GetProperty("NextBlockOnFailure", BindingFlags.Public | BindingFlags.Instance);
            if (failureProperty != null && failureProperty.CanWrite)
            {
                var value = nextBlockOnFailure ?? string.Empty;
                failureProperty.SetValue(block, value);
                logger?.LogDebug("Injected NextBlockOnFailure '{Value}' into block '{BlockType}'",
                    value, blockType.Name);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to inject transition properties into block '{BlockType}'",
                blockType.Name);
        }
    }
    /// <summary>
    /// Determines whether a block definition represents a code block.
    /// </summary>
    /// <param name="blockDefinition">The block definition to check.</param>
    /// <returns>True if this is a code block definition, false otherwise.</returns>
    private bool IsCodeBlockDefinition(WorkflowBlockDefinition blockDefinition)
    {
        // Check for explicit code block indicators in configuration
        if (blockDefinition.Configuration.TryGetValue("IsCodeBlock", out var isCodeBlockValue))
        {
            if (isCodeBlockValue is bool isCodeBlock && isCodeBlock)
            {
                return true;
            }
            if (isCodeBlockValue is string isCodeBlockStr && bool.TryParse(isCodeBlockStr, out var parsed) && parsed)
            {
                return true;
            }
        }

        // Check for code block type names
        var blockType = blockDefinition.BlockType?.ToLowerInvariant();
        if (blockType == "codeblock" || blockType == "code")
        {
            return true;
        }

        // Check for code execution configuration
        return blockDefinition.Configuration.ContainsKey("CodeConfig") ||
               blockDefinition.Configuration.ContainsKey("Code");
    }

    /// <summary>
    /// Creates a code block from its definition.
    /// </summary>
    /// <param name="blockDefinition">The definition of the code block to create.</param>
    /// <returns>The created code block, or null if creation failed.</returns>
    private IWorkflowBlock? CreateCodeBlock(WorkflowBlockDefinition blockDefinition)
    {
        try
        {
            logger?.LogDebug("Creating code block from definition: {BlockId}", blockDefinition.BlockId);

            // Parse the code execution configuration
            var codeConfig = ParseCodeExecutionConfig(blockDefinition);
            if (codeConfig == null)
            {
                logger?.LogError("Failed to parse code execution configuration for block {BlockId}", blockDefinition.BlockId);
                return null;
            }

            // Validate the code configuration
            var validationResult = ValidateCodeBlockDefinition(blockDefinition, codeConfig);
            if (!validationResult.IsValid)
            {
                logger?.LogError("Code block definition validation failed for block {BlockId}: {Errors}",
                    blockDefinition.BlockId, string.Join(", ", validationResult.Errors));
                return null;
            }

            // Create the code block
            var codeBlock = CodeBlock.Create(
                codeConfig,
                _serviceProvider,
                blockDefinition.NextBlockOnSuccess,
                blockDefinition.NextBlockOnFailure,
                logger);

            logger?.LogInformation("Successfully created code block {BlockId} with mode {Mode}",
                blockDefinition.BlockId, codeConfig.Mode);

            return codeBlock;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create code block {BlockId}", blockDefinition.BlockId);
            return null;
        }
    }

    /// <summary>
    /// Parses code execution configuration from a block definition.
    /// </summary>
    /// <param name="blockDefinition">The block definition containing configuration.</param>
    /// <returns>The parsed code execution configuration, or null if parsing failed.</returns>
    private CodeExecutionConfig? ParseCodeExecutionConfig(WorkflowBlockDefinition blockDefinition)
    {
        try
        {
            // Check for explicit CodeConfig object
            if (blockDefinition.Configuration.TryGetValue("CodeConfig", out var codeConfigValue))
            {
                if (codeConfigValue is CodeExecutionConfig config)
                {
                    return config;
                }
            }

            // Parse configuration manually
            var mode = CodeExecutionMode.Inline; // Default mode
            var language = "csharp"; // Default language
            var code = string.Empty;
            var assemblyPath = string.Empty;
            var typeName = string.Empty;
            var methodName = "Execute";
            var parameters = new Dictionary<string, object>();
            var allowedNamespaces = new List<string>();
            var allowedTypes = new List<string>();
            var blockedNamespaces = new List<string>();
            var timeout = TimeSpan.FromSeconds(30);
            var enableLogging = true;
            var validateCode = true;

            // Parse mode
            if (blockDefinition.Configuration.TryGetValue("Mode", out var modeValue))
            {
                if (modeValue is string modeStr && Enum.TryParse<CodeExecutionMode>(modeStr, true, out var parsedMode))
                {
                    mode = parsedMode;
                }
            }

            // Parse language
            if (blockDefinition.Configuration.TryGetValue("Language", out var languageValue))
            {
                language = languageValue?.ToString() ?? "csharp";
            }

            // Parse code or assembly information
            if (mode == CodeExecutionMode.Inline)
            {
                if (blockDefinition.Configuration.TryGetValue("Code", out var codeValue))
                {
                    code = codeValue?.ToString() ?? string.Empty;
                }
            }
            else if (mode == CodeExecutionMode.Assembly)
            {
                if (blockDefinition.Configuration.TryGetValue("AssemblyPath", out var assemblyPathValue))
                {
                    assemblyPath = assemblyPathValue?.ToString() ?? string.Empty;
                }
                if (blockDefinition.Configuration.TryGetValue("TypeName", out var typeNameValue))
                {
                    typeName = typeNameValue?.ToString() ?? string.Empty;
                }
                if (blockDefinition.Configuration.TryGetValue("MethodName", out var methodNameValue))
                {
                    methodName = methodNameValue?.ToString() ?? "Execute";
                }
            }

            // Parse security settings
            if (blockDefinition.Configuration.TryGetValue("AllowedNamespaces", out var allowedNamespacesValue))
            {
                if (allowedNamespacesValue is IEnumerable<string> namespaces)
                {
                    allowedNamespaces.AddRange(namespaces);
                }
            }

            if (blockDefinition.Configuration.TryGetValue("AllowedTypes", out var allowedTypesValue))
            {
                if (allowedTypesValue is IEnumerable<string> types)
                {
                    allowedTypes.AddRange(types);
                }
            }

            if (blockDefinition.Configuration.TryGetValue("BlockedNamespaces", out var blockedNamespacesValue))
            {
                if (blockedNamespacesValue is IEnumerable<string> blocked)
                {
                    blockedNamespaces.AddRange(blocked);
                }
            }

            // Parse timeout
            if (blockDefinition.Configuration.TryGetValue("Timeout", out var timeoutValue))
            {
                if (timeoutValue is string timeoutStr && TimeSpan.TryParse(timeoutStr, out var parsedTimeout))
                {
                    timeout = parsedTimeout;
                }
                else if (timeoutValue is int timeoutSeconds)
                {
                    timeout = TimeSpan.FromSeconds(timeoutSeconds);
                }
            }

            // Parse boolean flags
            if (blockDefinition.Configuration.TryGetValue("EnableLogging", out var enableLoggingValue))
            {
                enableLogging = ParseBooleanConfig(enableLoggingValue, true);
            }

            if (blockDefinition.Configuration.TryGetValue("ValidateCode", out var validateCodeValue))
            {
                validateCode = ParseBooleanConfig(validateCodeValue, true);
            }

            // Parse additional parameters
            foreach (var config in blockDefinition.Configuration)
            {
                if (!IsReservedConfigurationKey(config.Key))
                {
                    parameters[config.Key] = config.Value;
                }
            }

            // Create configuration based on mode
            return mode == CodeExecutionMode.Inline
                ? CodeExecutionConfig.CreateInline(
                    language,
                    code,
                    allowedNamespaces,
                    allowedTypes,
                    blockedNamespaces,
                    parameters,
                    timeout,
                    enableLogging,
                    validateCode)
                : CodeExecutionConfig.CreateAssembly(
                    assemblyPath,
                    typeName,
                    methodName,
                    parameters,
                    timeout,
                    enableLogging);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error parsing code execution configuration for block {BlockId}", blockDefinition.BlockId);
            return null;
        }
    }

    /// <summary>
    /// Validates a code block definition.
    /// </summary>
    /// <param name="blockDefinition">The block definition to validate.</param>
    /// <param name="codeConfig">The parsed code execution configuration.</param>
    /// <returns>A validation result indicating whether the definition is valid.</returns>
    private CodeExecution.ValidationResult ValidateCodeBlockDefinition(WorkflowBlockDefinition blockDefinition, CodeExecutionConfig codeConfig)
    {
        var errors = new List<string>();

        // Validate required fields based on mode
        if (codeConfig.Mode == CodeExecutionMode.Inline)
        {
            if (string.IsNullOrEmpty(codeConfig.Code))
            {
                errors.Add("Code is required for inline execution mode");
            }
        }
        else if (codeConfig.Mode == CodeExecutionMode.Assembly)
        {
            if (string.IsNullOrEmpty(codeConfig.AssemblyPath))
            {
                errors.Add("AssemblyPath is required for assembly execution mode");
            }
            if (string.IsNullOrEmpty(codeConfig.TypeName))
            {
                errors.Add("TypeName is required for assembly execution mode");
            }
            if (!File.Exists(codeConfig.AssemblyPath))
            {
                errors.Add($"Assembly file does not exist: {codeConfig.AssemblyPath}");
            }
        }

        // Validate security settings
        if (codeConfig.AllowedNamespaces.Any() && codeConfig.BlockedNamespaces.Any())
        {
            var conflicts = codeConfig.AllowedNamespaces.Intersect(codeConfig.BlockedNamespaces).ToList();
            if (conflicts.Any())
            {
                errors.Add($"Conflicting namespace settings: {string.Join(", ", conflicts)}");
            }
        }

        // Validate timeout
        if (codeConfig.Timeout <= TimeSpan.Zero || codeConfig.Timeout > TimeSpan.FromMinutes(10))
        {
            errors.Add("Timeout must be between 0 and 10 minutes");
        }

        return errors.Any() ? CodeExecution.ValidationResult.Failure(errors) : CodeExecution.ValidationResult.Success();
    }

    /// <summary>
    /// Parses a boolean configuration value.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="defaultValue">The default value if parsing fails.</param>
    /// <returns>The parsed boolean value or the default value.</returns>
    private bool ParseBooleanConfig(object? value, bool defaultValue)
    {
        if (value == null)
            return defaultValue;

        if (value is bool boolValue)
            return boolValue;

        if (value is string stringValue && bool.TryParse(stringValue, out var parsed))
            return parsed;

        return defaultValue;
    }

    /// <summary>
    /// Determines whether a configuration key is reserved for internal use.
    /// </summary>
    /// <param name="key">The configuration key to check.</param>
    /// <returns>True if the key is reserved, false otherwise.</returns>
    private bool IsReservedConfigurationKey(string key)
    {
        var reservedKeys = new[]
        {
            "IsCodeBlock",
            "CodeConfig",
            "Mode",
            "Language",
            "Code",
            "AssemblyPath",
            "TypeName",
            "MethodName",
            "AllowedNamespaces",
            "AllowedTypes",
            "BlockedNamespaces",
            "Timeout",
            "EnableLogging",
            "ValidateCode"
        };

        return reservedKeys.Contains(key);
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
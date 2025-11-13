namespace FlowCore.Guards;

/// <summary>
/// Factory for creating guard instances from guard definitions.
/// Supports both built-in common guards and custom guards loaded from assemblies.
/// </summary>
/// <remarks>
/// Initializes a new instance of the GuardFactory class.
/// </remarks>
/// <param name="serviceProvider">The service provider for dependency resolution.</param>
/// <param name="logger">Optional logger for factory operations.</param>
public class GuardFactory(IServiceProvider serviceProvider, ILogger<GuardFactory>? logger = null)
{
    private readonly IServiceProvider _serviceProvider
        = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ConcurrentDictionary<string, Type> _guardCache = new();

    /// <summary>
    /// Creates a guard instance from its definition.
    /// </summary>
    /// <param name="guardDefinition">The definition of the guard to create.</param>
    /// <returns>The created guard, or null if creation failed.</returns>
    public IGuard? CreateGuard(GuardDefinition guardDefinition)
    {
        try
        {
            // Check if this is a common guard
            if (IsCommonGuard(guardDefinition))
            {
                return CreateCommonGuard(guardDefinition);
            }

            // Handle custom guard creation
            var cacheKey = $"{guardDefinition.AssemblyName}:{guardDefinition.Namespace}:{guardDefinition.GuardType}";
            if (_guardCache.TryGetValue(cacheKey, out var cachedGuardType))
            {
                return CreateGuardInstance(cachedGuardType, guardDefinition);
            }

            // Load assembly
            var assembly = LoadAssembly(guardDefinition.AssemblyName);
            var guardType = FindGuardType(assembly, guardDefinition);
            if (guardType == null)
            {
                throw new TypeLoadException($"Could not load type '{guardDefinition.GuardType}' from assembly '{guardDefinition.AssemblyName}'");
            }

            _guardCache[cacheKey] = guardType;
            return CreateGuardInstance(guardType, guardDefinition);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create guard of type '{GuardType}' from assembly '{AssemblyName}'",
                guardDefinition.GuardType, guardDefinition.AssemblyName);
            return null;
        }
    }

    private static bool IsCommonGuard(GuardDefinition guardDefinition)
    {
        var commonGuards = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "businesshoursguard",
            "dataformatguard",
            "numericrangeguard",
            "requiredfieldguard",
            "authorizationguard"
        };
        return commonGuards.Contains(guardDefinition.GuardType);
    }

    private IGuard? CreateCommonGuard(GuardDefinition guardDefinition)
    {
        try
        {
            var config = guardDefinition.Configuration;
            var guardCreators = new Dictionary<string, Func<IReadOnlyDictionary<string, object>, IGuard?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["businesshoursguard"] = CreateBusinessHoursGuard,
                ["dataformatguard"] = CreateDataFormatGuard,
                ["numericrangeguard"] = CreateNumericRangeGuard,
                ["requiredfieldguard"] = CreateRequiredFieldGuard,
                ["authorizationguard"] = CreateAuthorizationGuard
            };
            return guardCreators.TryGetValue(guardDefinition.GuardType, out var creator) ? creator(config) : null;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create common guard '{GuardType}'", guardDefinition.GuardType);
            return null;
        }
    }

    private IGuard? CreateBusinessHoursGuard(IReadOnlyDictionary<string, object> config)
    {
        var startTime = config.GetValue<TimeSpan>("StartTime", TimeSpan.FromHours(9));
        var endTime = config.GetValue<TimeSpan>("EndTime", TimeSpan.FromHours(17));
        var validDays = config.GetValue<DayOfWeek[]>("ValidDays", [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
        var holidayDates = config.GetValue<string[]>("HolidayDates", []);
        return new CommonGuards.BusinessHoursGuard(startTime, endTime, validDays, holidayDates);
    }

    private static IGuard? CreateDataFormatGuard(IReadOnlyDictionary<string, object> config)
    {
        var fieldName = config.GetValue<string>("FieldName");
        var pattern = config.GetValue<string>("Pattern");
        if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(pattern))
        {
            return null;
        }

        var regexOptions = config.GetValue<RegexOptions>("RegexOptions", RegexOptions.None);
        return new CommonGuards.DataFormatGuard(fieldName, pattern, regexOptions);
    }

    private static IGuard? CreateNumericRangeGuard(IReadOnlyDictionary<string, object> config)
    {
        var fieldName = config.GetValue<string>("FieldName");
        var minValue = config.GetValue<decimal>("MinValue", 0);
        var maxValue = config.GetValue<decimal>("MaxValue", 100);
        var inclusiveMin = config.GetValue<bool>("InclusiveMin", true);
        var inclusiveMax = config.GetValue<bool>("InclusiveMax", true);
        if (string.IsNullOrEmpty(fieldName))
        {
            return null;
        }

        return new CommonGuards.NumericRangeGuard(fieldName, minValue, maxValue, inclusiveMin, inclusiveMax);
    }

    private IGuard? CreateRequiredFieldGuard(IReadOnlyDictionary<string, object> config)
    {
        var fieldNames = config.GetValue<string[]>("FieldNames");
        if (fieldNames == null || fieldNames.Length == 0)
        {
            return null;
        }

        return new CommonGuards.RequiredFieldGuard(fieldNames);
    }

    private IGuard? CreateAuthorizationGuard(IReadOnlyDictionary<string, object> config)
    {
        var requiredPermission = config.GetValue<string>("RequiredPermission");
        var allowedRoles = config.GetValue<string[]>("AllowedRoles", []);
        if (string.IsNullOrEmpty(requiredPermission))
        {
            return null;
        }

        return new CommonGuards.AuthorizationGuard(requiredPermission, allowedRoles);
    }

    private static Assembly LoadAssembly(string assemblyName)
    {
        try
        {
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (loadedAssembly != null)
            {
                return loadedAssembly;
            }

            var assembly = Assembly.Load(assemblyName);
            return assembly;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load assembly '{assemblyName}'", ex);
        }
    }

    private static Type? FindGuardType(Assembly assembly, GuardDefinition guardDefinition)
    {
        var typeName = guardDefinition.GuardType;
        if (!string.IsNullOrEmpty(guardDefinition.Namespace))
        {
            var fullTypeName = $"{guardDefinition.Namespace}.{typeName}";
            var guardType = assembly.GetType(fullTypeName, false);
            if (guardType != null)
            {
                return guardType;
            }
        }

        var simpleType = assembly.GetType(typeName, false);
        if (simpleType != null)
        {
            return simpleType;
        }

        return assembly.GetTypes()
            .FirstOrDefault(t => t.Name == typeName && typeof(IGuard).IsAssignableFrom(t));
    }

    private IGuard? CreateGuardInstance(Type guardType, GuardDefinition guardDefinition)
    {
        try
        {
            var guard = _serviceProvider.GetService(guardType) as IGuard;
            if (guard != null)
            {
                return guard;
            }

            var loggerType = typeof(ILogger<>).MakeGenericType(guardType);
            var logger = _serviceProvider.GetService(loggerType);
            guard = Activator.CreateInstance(guardType, logger) as IGuard;
            return guard;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create instance of guard type '{GuardType}'", guardType.FullName);
            return null;
        }
    }
}

public static class DictionaryExtensions
{
    public static T GetValue<T>(this IReadOnlyDictionary<string, object> dict, string key, T defaultValue = default!)
    {
        if (dict.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }

        return defaultValue;
    }
}

namespace FlowCore.CodeExecution;

/// <summary>
/// Provides caching services for code execution to improve performance.
/// Caches compiled assemblies, execution delegates, and validation results.
/// </summary>
/// <remarks>
/// Initializes a new instance of the CodeExecutionCache.
/// </remarks>
/// <param name="logger">Optional logger for cache operations.</param>
public class CodeExecutionCache(ILogger? logger = null)
{
    private readonly ConcurrentDictionary<string, CachedAssembly> _assemblyCache = new();
    private readonly ConcurrentDictionary<string, CachedDelegate> _delegateCache = new();
    private readonly ConcurrentDictionary<string, CachedValidation> _validationCache = new();

    /// <summary>
    /// Gets or adds an assembly to the cache.
    /// </summary>
    /// <param name="key">The cache key for the assembly.</param>
    /// <param name="assemblyFactory">Function to create the assembly if not cached.</param>
    /// <returns>The cached or newly created assembly.</returns>
    public async Task<Assembly> GetOrAddAssemblyAsync(string key, Func<Task<Assembly>> assemblyFactory)
    {
        if (_assemblyCache.TryGetValue(key, out var cachedAssembly))
        {
            if (!cachedAssembly.IsExpired())
            {
                logger?.LogDebug("Using cached assembly for key: {Key}", key);
                return cachedAssembly.Assembly;
            }
            else
            {
                // Remove expired entry
                _assemblyCache.TryRemove(key, out _);
            }
        }

        var assembly = await assemblyFactory().ConfigureAwait(false);
        cachedAssembly = new CachedAssembly(assembly, TimeSpan.FromMinutes(30)); // Cache for 30 minutes
        _assemblyCache[key] = cachedAssembly;

        logger?.LogDebug("Cached new assembly for key: {Key}", key);
        return assembly;
    }

    /// <summary>
    /// Gets or adds a delegate to the cache.
    /// </summary>
    /// <param name="key">The cache key for the delegate.</param>
    /// <param name="delegateFactory">Function to create the delegate if not cached.</param>
    /// <returns>The cached or newly created delegate.</returns>
    public async Task<Delegate> GetOrAddDelegateAsync(string key, Func<Task<Delegate>> delegateFactory)
    {
        if (_delegateCache.TryGetValue(key, out var cachedDelegate))
        {
            if (!cachedDelegate.IsExpired())
            {
                logger?.LogDebug("Using cached delegate for key: {Key}", key);
                return cachedDelegate.Delegate;
            }
            else
            {
                // Remove expired entry
                _delegateCache.TryRemove(key, out _);
            }
        }

        var @delegate = await delegateFactory().ConfigureAwait(false);
        cachedDelegate = new CachedDelegate(@delegate, TimeSpan.FromMinutes(15)); // Cache for 15 minutes
        _delegateCache[key] = cachedDelegate;

        logger?.LogDebug("Cached new delegate for key: {Key}", key);
        return @delegate;
    }

    /// <summary>
    /// Gets or adds a validation result to the cache.
    /// </summary>
    /// <param name="key">The cache key for the validation result.</param>
    /// <param name="validationFactory">Function to create the validation result if not cached.</param>
    /// <returns>The cached or newly created validation result.</returns>
    public ValidationResult GetOrAddValidation(string key, Func<ValidationResult> validationFactory)
    {
        if (_validationCache.TryGetValue(key, out var cachedValidation))
        {
            if (!cachedValidation.IsExpired())
            {
                logger?.LogDebug("Using cached validation result for key: {Key}", key);
                return cachedValidation.Result;
            }
            else
            {
                // Remove expired entry
                _validationCache.TryRemove(key, out _);
            }
        }

        var result = validationFactory();
        cachedValidation = new CachedValidation(result, TimeSpan.FromHours(1)); // Cache for 1 hour
        _validationCache[key] = cachedValidation;

        logger?.LogDebug("Cached new validation result for key: {Key}", key);
        return result;
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void Clear()
    {
        _assemblyCache.Clear();
        _delegateCache.Clear();
        _validationCache.Clear();
        logger?.LogInformation("Code execution cache cleared");
    }

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    /// <returns>Cache statistics including entry counts and hit rates.</returns>
    public CacheStatistics GetStatistics() => new CacheStatistics
    {
        AssemblyCacheCount = _assemblyCache.Count,
        DelegateCacheCount = _delegateCache.Count,
        ValidationCacheCount = _validationCache.Count,
        TotalEntries = _assemblyCache.Count + _delegateCache.Count + _validationCache.Count
    };

    /// <summary>
    /// Generates a cache key for code and parameters.
    /// </summary>
    /// <param name="code">The code string.</param>
    /// <param name="parameters">The parameters dictionary.</param>
    /// <returns>A unique cache key for the code and parameters.</returns>
    public static string GenerateCacheKey(string code, IReadOnlyDictionary<string, object> parameters)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append(code.GetHashCode().ToString("X8", CultureInfo.InvariantCulture));

        foreach (var param in parameters.OrderBy(p => p.Key))
        {
            keyBuilder.AppendFormat(CultureInfo.InvariantCulture, "|{0}:{1}", param.Key, param.Value == null ? "NULL" : param.Value.GetHashCode().ToString("X8", CultureInfo.InvariantCulture));
        }

        return keyBuilder.ToString();
    }

    /// <summary>
    /// Generates a cache key for assembly path and configuration.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="typeName">The type name to execute.</param>
    /// <param name="methodName">The method name to execute.</param>
    /// <returns>A unique cache key for the assembly configuration.</returns>
    public static string GenerateAssemblyCacheKey(string assemblyPath, string typeName, string methodName) => $"{assemblyPath.GetHashCode():X8}|{typeName.GetHashCode():X8}|{methodName.GetHashCode():X8}";

    private abstract class CacheEntry(TimeSpan timeToLive)
    {
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public TimeSpan TimeToLive { get; } = timeToLive;

        public bool IsExpired() => DateTime.UtcNow - CreatedAt > TimeToLive;
    }

    sealed class CachedAssembly(Assembly assembly, TimeSpan timeToLive) : CacheEntry(timeToLive)
    {
        public Assembly Assembly { get; } = assembly ?? throw new ArgumentNullException(nameof(assembly));
    }

    sealed class CachedDelegate(Delegate @delegate, TimeSpan timeToLive) : CacheEntry(timeToLive)
    {
        public Delegate Delegate { get; } = @delegate ?? throw new ArgumentNullException(nameof(@delegate));
    }

    sealed class CachedValidation(ValidationResult result, TimeSpan timeToLive) : CacheEntry(timeToLive)
    {
        public ValidationResult Result { get; } = result ?? throw new ArgumentNullException(nameof(result));
    }

    /// <summary>
    /// Statistics about the cache performance and usage.
    /// </summary>
    public class CacheStatistics
    {
        public int AssemblyCacheCount { get; set; }
        public int DelegateCacheCount { get; set; }
        public int ValidationCacheCount { get; set; }
        public int TotalEntries { get; set; }
    }
}

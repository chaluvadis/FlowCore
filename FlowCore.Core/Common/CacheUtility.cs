namespace FlowCore.Core.Common;
/// <summary>
/// Utility class for managing time-based caches with eviction.
/// </summary>
public static class CacheUtility
{
    /// <summary>
    /// Adds or updates an item in the cache with a timestamp.
    /// </summary>
    /// <typeparam name="TKey">The type of the cache key.</typeparam>
    /// <typeparam name="TValue">The type of the cache value.</typeparam>
    /// <param name="cache">The cache dictionary.</param>
    /// <param name="key">The key to add or update.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="maxSize">The maximum number of items in the cache before eviction.</param>
    public static void AddOrUpdateWithTimestamp<TKey, TValue>(
        ConcurrentDictionary<TKey, (TValue value, DateTime timestamp)> cache,
        TKey key,
        TValue value,
        int maxSize = 100)
        where TKey : notnull
    {
        cache[key] = (value, DateTime.UtcNow);
        // Evict oldest if over limit
        if (cache.Count > maxSize)
        {
            var oldest = cache.OrderBy(kv => kv.Value.timestamp).First();
            cache.TryRemove(oldest.Key, out _);
        }
    }
    /// <summary>
    /// Tries to get a value from the cache.
    /// </summary>
    /// <typeparam name="TKey">The type of the cache key.</typeparam>
    /// <typeparam name="TValue">The type of the cache value.</typeparam>
    /// <param name="cache">The cache dictionary.</param>
    /// <param name="key">The key to retrieve.</param>
    /// <param name="value">The retrieved value, if found.</param>
    /// <returns>True if the key was found, false otherwise.</returns>
    public static bool TryGetValue<TKey, TValue>(
        ConcurrentDictionary<TKey, (TValue value, DateTime timestamp)> cache,
        TKey key,
        out TValue value)
        where TKey : notnull
    {
        if (cache.TryGetValue(key, out var entry))
        {
            value = entry.value;
            return true;
        }
        value = default!;
        return false;
    }
}
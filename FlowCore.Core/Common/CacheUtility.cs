namespace FlowCore.Common;
/// <summary>
/// Unified cache management utility for time-based caches with configurable eviction policies.
/// Provides centralized cache operations across the FlowCore framework.
/// </summary>
public static class CacheUtility
{
    /// <summary>
    /// Adds or updates an item in the cache with a timestamp and configurable eviction.
    /// </summary>
    /// <typeparam name="TKey">The type of the cache key.</typeparam>
    /// <typeparam name="TValue">The type of the cache value.</typeparam>
    /// <param name="cache">The cache dictionary.</param>
    /// <param name="key">The key to add or update.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="maxSize">The maximum number of items in the cache before eviction.</param>
    /// <param name="evictionPolicy">The eviction policy to use when cache is full.</param>
    public static void AddOrUpdateWithTimestamp<TKey, TValue>(
        ConcurrentDictionary<TKey, (TValue value, DateTime timestamp)> cache,
        TKey key,
        TValue value,
        int maxSize = 100,
        CacheEvictionPolicy evictionPolicy = CacheEvictionPolicy.LRU)
        where TKey : notnull
    {
        cache[key] = (value, DateTime.UtcNow);
        // Evict items if over limit using specified policy
        if (cache.Count > maxSize)
        {
            EvictItems(cache, maxSize, evictionPolicy);
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

    /// <summary>
    /// Gets cache statistics for monitoring and diagnostics.
    /// </summary>
    /// <typeparam name="TKey">The type of the cache key.</typeparam>
    /// <typeparam name="TValue">The type of the cache value.</typeparam>
    /// <param name="cache">The cache dictionary.</param>
    /// <returns>Cache statistics including size, age distribution, and hit rates.</returns>
    public static CacheStatistics GetStatistics<TKey, TValue>(
        ConcurrentDictionary<TKey, (TValue value, DateTime timestamp)> cache)
        where TKey : notnull
    {
        var entries = cache.ToArray();
        if (!entries.Any())
        {
            return new CacheStatistics(0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        }

        var now = DateTime.UtcNow;
        var ages = entries.Select(e => now - e.Value.timestamp).ToArray();
        var oldest = ages.Max();
        var newest = ages.Min();
        var average = TimeSpan.FromTicks((long)ages.Average(a => a.Ticks));

        return new CacheStatistics(entries.Length, oldest, newest, average);
    }

    /// <summary>
    /// Cleans up expired items from the cache based on maximum age.
    /// </summary>
    /// <typeparam name="TKey">The type of the cache key.</typeparam>
    /// <typeparam name="TValue">The type of the cache value.</typeparam>
    /// <param name="cache">The cache dictionary.</param>
    /// <param name="maxAge">The maximum age of items to keep.</param>
    /// <returns>The number of items removed.</returns>
    public static int CleanupExpired<TKey, TValue>(
        ConcurrentDictionary<TKey, (TValue value, DateTime timestamp)> cache,
        TimeSpan maxAge)
        where TKey : notnull
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var expiredKeys = cache
            .Where(kv => kv.Value.timestamp < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            cache.TryRemove(key, out _);
        }

        return expiredKeys.Count;
    }

    /// <summary>
    /// Evicts items from the cache based on the specified policy.
    /// </summary>
    private static void EvictItems<TKey, TValue>(
        ConcurrentDictionary<TKey, (TValue value, DateTime timestamp)> cache,
        int targetSize,
        CacheEvictionPolicy policy)
        where TKey : notnull
    {
        var itemsToRemove = cache.Count - targetSize;
        if (itemsToRemove <= 0)
        {
            return;
        }

        IEnumerable<KeyValuePair<TKey, (TValue value, DateTime timestamp)>> itemsToEvict;

        switch (policy)
        {
            case CacheEvictionPolicy.LRU:
                // Least Recently Used - evict oldest accessed items
                itemsToEvict = cache.OrderBy(kv => kv.Value.timestamp).Take(itemsToRemove);
                break;
            case CacheEvictionPolicy.FIFO:
                // First In First Out - evict oldest items
                itemsToEvict = cache.OrderBy(kv => kv.Value.timestamp).Take(itemsToRemove);
                break;
            case CacheEvictionPolicy.Random:
                // Random eviction
                itemsToEvict = cache.OrderBy(_ => Random.Shared.Next()).Take(itemsToRemove);
                break;
            default:
                itemsToEvict = cache.OrderBy(kv => kv.Value.timestamp).Take(itemsToRemove);
                break;
        }

        foreach (var item in itemsToEvict)
        {
            cache.TryRemove(item.Key, out _);
        }
    }
}

/// <summary>
/// Cache eviction policies for managing cache size limits.
/// </summary>
public enum CacheEvictionPolicy
{
    /// <summary>
    /// Least Recently Used - evict items that haven't been accessed recently.
    /// </summary>
    LRU,
    /// <summary>
    /// First In First Out - evict oldest items first.
    /// </summary>
    FIFO,
    /// <summary>
    /// Random eviction - randomly select items to evict.
    /// </summary>
    Random
}

/// <summary>
/// Statistics about a cache for monitoring and diagnostics.
/// </summary>
public readonly record struct CacheStatistics(
    int Size,
    TimeSpan OldestItemAge,
    TimeSpan NewestItemAge,
    TimeSpan AverageItemAge);

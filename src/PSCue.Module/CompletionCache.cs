using System.Collections.Concurrent;
using PSCue.Shared;

namespace PSCue.Module;

/// <summary>
/// Thread-safe cache for completion suggestions with usage tracking.
/// </summary>
public class CompletionCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Get completions from cache if available and not expired.
    /// </summary>
    public CompletionItem[]? TryGetCompletions(string cacheKey)
    {
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (DateTime.UtcNow - entry.Timestamp < _defaultExpiration)
            {
                // Update hit count for statistics
                entry.HitCount++;
                entry.LastAccessTime = DateTime.UtcNow;
                return entry.Completions;
            }

            // Expired - remove from cache
            _cache.TryRemove(cacheKey, out _);
        }

        return null;
    }

    /// <summary>
    /// Store completions in cache.
    /// </summary>
    public void SetCompletions(string cacheKey, CompletionItem[] completions)
    {
        var entry = new CacheEntry
        {
            Completions = completions,
            Timestamp = DateTime.UtcNow,
            LastAccessTime = DateTime.UtcNow,
            HitCount = 0
        };

        _cache[cacheKey] = entry;
    }

    /// <summary>
    /// Update the usage score for a specific completion.
    /// Called by IFeedbackProvider when a command is executed.
    /// </summary>
    public void IncrementUsage(string cacheKey, string completionText)
    {
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            var completion = entry.Completions.FirstOrDefault(c => c.Text == completionText);
            if (completion != null)
            {
                // Increment score (using a simple additive model for now)
                completion.Score = Math.Min(1.0, completion.Score + 0.1);

                // Re-sort completions by score
                Array.Sort(entry.Completions, (a, b) => b.Score.CompareTo(a.Score));
            }
        }
    }

    /// <summary>
    /// Clear all cached entries.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Remove entries older than the specified age.
    /// </summary>
    public void RemoveExpired(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.Timestamp < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Get cache statistics for debugging.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            EntryCount = _cache.Count,
            TotalHits = _cache.Values.Sum(e => e.HitCount),
            OldestEntry = _cache.Values.Any()
                ? _cache.Values.Min(e => e.Timestamp)
                : DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generate a cache key from command line context.
    /// </summary>
    public static string GetCacheKey(string command, string commandLine)
    {
        // Use command + normalized command line (without the word being completed)
        // For example: "git|checkout" for "git checkout ma"
        var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
        {
            return command;
        }

        // Take up to the second-to-last part (exclude the partial word being completed)
        var contextParts = parts.Take(parts.Length - 1).ToArray();
        return string.Join("|", contextParts);
    }

    private class CacheEntry
    {
        public required CompletionItem[] Completions { get; init; }
        public required DateTime Timestamp { get; init; }
        public DateTime LastAccessTime { get; set; }
        public int HitCount { get; set; }
    }
}

/// <summary>
/// Statistics about the completion cache.
/// </summary>
public class CacheStatistics
{
    public int EntryCount { get; init; }
    public int TotalHits { get; init; }
    public DateTime OldestEntry { get; init; }
}

#pragma warning disable CA2252 // Opt into preview features for testing

using PSCue.Shared;

namespace PSCue.Module.Tests;

public class CompletionCacheTests
{
    [Fact]
    public void CacheKey_RemovesPartialWord()
    {
        // Arrange & Act
        var key1 = CompletionCache.GetCacheKey("scoop", "scoop h");
        var key2 = CompletionCache.GetCacheKey("scoop", "scoop ho");
        var key3 = CompletionCache.GetCacheKey("scoop", "scoop ");

        // Assert
        Assert.Equal("scoop", key1);
        Assert.Equal("scoop", key2);
        Assert.Equal("scoop", key3);
    }

    [Fact]
    public void CacheKey_IncludesSubcommand()
    {
        // Arrange & Act
        var key1 = CompletionCache.GetCacheKey("git", "git checkout m");
        var key2 = CompletionCache.GetCacheKey("git", "git checkout ma");
        var key3 = CompletionCache.GetCacheKey("git", "git checkout ");

        // Assert
        // For "git checkout m" -> parts = ["git", "checkout", "m"] -> remove last -> ["git", "checkout"] -> "git|checkout"
        Assert.Equal("git|checkout", key1);
        Assert.Equal("git|checkout", key2);
        // For "git checkout " -> parts = ["git", "checkout"] (RemoveEmptyEntries) -> remove last -> ["git"] -> "git"
        Assert.Equal("git", key3);
    }

    [Fact]
    public void SetAndGetCompletions_WorksCorrectly()
    {
        // Arrange
        var cache = new CompletionCache();
        var completions = new[]
        {
            new CompletionItem { Text = "help", Description = "Show help" },
            new CompletionItem { Text = "hold", Description = "Hold package" },
            new CompletionItem { Text = "home", Description = "Open homepage" }
        };

        // Act
        cache.SetCompletions("scoop", completions);
        var retrieved = cache.TryGetCompletions("scoop");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(3, retrieved.Length);
        Assert.Equal("help", retrieved[0].Text);
        Assert.Equal("hold", retrieved[1].Text);
        Assert.Equal("home", retrieved[2].Text);
    }

    [Fact]
    public void TryGetCompletions_ReturnsNull_WhenNotCached()
    {
        // Arrange
        var cache = new CompletionCache();

        // Act
        var result = cache.TryGetCompletions("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGetCompletions_IncrementsHitCount()
    {
        // Arrange
        var cache = new CompletionCache();
        var completions = new[]
        {
            new CompletionItem { Text = "help", Description = "Show help" }
        };
        cache.SetCompletions("test", completions);

        // Act
        var stats1 = cache.GetStatistics();
        cache.TryGetCompletions("test");
        cache.TryGetCompletions("test");
        var stats2 = cache.GetStatistics();

        // Assert
        Assert.Equal(0, stats1.TotalHits);
        Assert.Equal(2, stats2.TotalHits);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        // Arrange
        var cache = new CompletionCache();
        cache.SetCompletions("test1", new[] { new CompletionItem { Text = "a" } });
        cache.SetCompletions("test2", new[] { new CompletionItem { Text = "b" } });

        // Act
        cache.Clear();
        var stats = cache.GetStatistics();

        // Assert
        Assert.Equal(0, stats.EntryCount);
        Assert.Null(cache.TryGetCompletions("test1"));
        Assert.Null(cache.TryGetCompletions("test2"));
    }

    [Fact]
    public void IncrementUsage_UpdatesScore()
    {
        // Arrange
        var cache = new CompletionCache();
        var completions = new[]
        {
            new CompletionItem { Text = "help", Description = "Show help", Score = 0.0 },
            new CompletionItem { Text = "hold", Description = "Hold package", Score = 0.0 }
        };
        cache.SetCompletions("scoop", completions);

        // Act
        cache.IncrementUsage("scoop", "help");
        var retrieved = cache.TryGetCompletions("scoop");

        // Assert
        Assert.NotNull(retrieved);
        var helpItem = Array.Find(retrieved, c => c.Text == "help");
        Assert.NotNull(helpItem);
        Assert.True(helpItem.Score > 0.0);
    }

    [Fact]
    public void GetCacheEntries_FiltersCorrectly()
    {
        // Arrange
        var cache = new CompletionCache();
        cache.SetCompletions("scoop", new[] { new CompletionItem { Text = "help" } });
        cache.SetCompletions("git|checkout", new[] { new CompletionItem { Text = "main" } });

        // Act
        var allEntries = cache.GetCacheEntries();
        var filtered = cache.GetCacheEntries("scoop");

        // Assert
        Assert.Equal(2, allEntries.Length);
        Assert.Single(filtered);
        Assert.Equal("scoop", filtered[0].Key);
    }
}

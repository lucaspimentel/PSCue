#pragma warning disable CA2252 // Opt into preview features for testing

using PSCue.Shared;

namespace PSCue.Module.Tests;

/// <summary>
/// Tests for the IPC cache filtering behavior.
/// These tests verify the fix for the issues:
/// 1. "scoop h<tab>" was returning all completions instead of filtered ones
/// 2. After "scoop h<tab>", then "scoop <tab>" was returning only "h" completions
/// </summary>
public class IpcFilteringTests
{
    [Fact]
    public void CommandCompleter_FiltersCompletionsByPrefix()
    {
        // Test Issue #1: Filtering by wordToComplete should work
        // Arrange
        var commandLine = "scoop h";
        var wordToComplete = "h";

        // Act
        var completions = CommandCompleter.GetCompletions(commandLine.AsSpan(), wordToComplete.AsSpan());

        // Assert - should only return completions starting with "h"
        Assert.NotEmpty(completions);
        Assert.All(completions, c => Assert.StartsWith("h", c.CompletionText, StringComparison.OrdinalIgnoreCase));

        // Verify we get the expected scoop subcommands starting with 'h'
        var texts = completions.Select(c => c.CompletionText).ToArray();
        Assert.Contains("help", texts);
        Assert.Contains("hold", texts);
        Assert.Contains("home", texts);
    }

    [Fact]
    public void CommandCompleter_ReturnsAllCompletions_WhenPrefixIsEmpty()
    {
        // Test Issue #2: Empty prefix should return all completions
        // Arrange
        var commandLine = "scoop ";
        var wordToComplete = "";

        // Act
        var completions = CommandCompleter.GetCompletions(commandLine.AsSpan(), wordToComplete.AsSpan());

        // Assert - should return all scoop subcommands
        Assert.NotEmpty(completions);
        var texts = completions.Select(c => c.CompletionText).ToArray();

        // Verify we get all major scoop subcommands
        Assert.Contains("help", texts);
        Assert.Contains("hold", texts);
        Assert.Contains("home", texts);
        Assert.Contains("install", texts);
        Assert.Contains("uninstall", texts);
        Assert.Contains("update", texts);
        Assert.Contains("search", texts);

        // Should have many more than just 3
        Assert.True(completions.Count() >= 20, $"Expected at least 20 completions, got {completions.Count()}");
    }

    [Fact]
    public void Cache_StoresUnfilteredCompletions()
    {
        // This test verifies that the cache stores ALL completions for a context,
        // not just the filtered ones from a specific request.

        // Arrange
        var cache = new CompletionCache();
        var allScoopCompletions = new[]
        {
            new CompletionItem { Text = "help", Description = "Show help" },
            new CompletionItem { Text = "hold", Description = "Hold package" },
            new CompletionItem { Text = "home", Description = "Open homepage" },
            new CompletionItem { Text = "install", Description = "Install package" },
            new CompletionItem { Text = "uninstall", Description = "Uninstall package" },
        };

        // Act - simulate caching all completions (unfiltered)
        cache.SetCompletions("scoop", allScoopCompletions);
        var cached = cache.TryGetCompletions("scoop");

        // Assert - cache should contain ALL completions
        Assert.NotNull(cached);
        Assert.Equal(5, cached.Length);
        Assert.Equal("help", cached[0].Text);
        Assert.Equal("install", cached[3].Text);
    }

    [Fact]
    public void FilterCachedCompletions_ByWordToComplete()
    {
        // This test simulates the IPC server's filtering logic:
        // 1. Get completions from cache (unfiltered)
        // 2. Filter them by wordToComplete before returning

        // Arrange
        var cache = new CompletionCache();
        var allCompletions = new[]
        {
            new CompletionItem { Text = "help", Description = "Show help" },
            new CompletionItem { Text = "hold", Description = "Hold package" },
            new CompletionItem { Text = "home", Description = "Open homepage" },
            new CompletionItem { Text = "install", Description = "Install package" },
            new CompletionItem { Text = "uninstall", Description = "Uninstall package" },
        };
        cache.SetCompletions("scoop", allCompletions);

        // Act - simulate filtering cached completions
        var cachedCompletions = cache.TryGetCompletions("scoop");
        Assert.NotNull(cachedCompletions);

        // Scenario 1: Filter by "h"
        var filteredByH = cachedCompletions
            .Where(c => c.Text.StartsWith("h", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Assert - should get only "h" completions
        Assert.Equal(3, filteredByH.Length);
        Assert.Contains(filteredByH, c => c.Text == "help");
        Assert.Contains(filteredByH, c => c.Text == "hold");
        Assert.Contains(filteredByH, c => c.Text == "home");

        // Scenario 2: Filter by empty string (should return all)
        var filteredByEmpty = string.IsNullOrEmpty("")
            ? cachedCompletions
            : cachedCompletions.Where(c => c.Text.StartsWith("", StringComparison.OrdinalIgnoreCase)).ToArray();

        // Assert - should get all completions
        Assert.Equal(5, filteredByEmpty.Length);
    }

    [Fact]
    public void SimulateRealWorldScenario_ScoopHTabThenScoopTab()
    {
        // This test simulates the exact bug scenario:
        // 1. User types "scoop h<tab>" - cache populated with ALL completions, filtered by "h"
        // 2. User types "scoop <tab>" - should return ALL completions, not just "h" ones

        // Arrange
        var cache = new CompletionCache();

        // Step 1: First request "scoop h<tab>"
        var wordToComplete1 = "h";
        var cacheKey1 = CompletionCache.GetCacheKey("scoop", "scoop h");

        // Simulate: Generate ALL completions (this is what GenerateCompletions should do)
        var allCompletions = new[]
        {
            new CompletionItem { Text = "help", Description = "Show help" },
            new CompletionItem { Text = "hold", Description = "Hold package" },
            new CompletionItem { Text = "home", Description = "Open homepage" },
            new CompletionItem { Text = "install", Description = "Install package" },
            new CompletionItem { Text = "list", Description = "List packages" },
            new CompletionItem { Text = "search", Description = "Search packages" },
            new CompletionItem { Text = "uninstall", Description = "Uninstall package" },
            new CompletionItem { Text = "update", Description = "Update packages" },
        };

        // Cache stores ALL completions (unfiltered)
        cache.SetCompletions(cacheKey1, allCompletions);

        // Filter for response
        var response1 = allCompletions
            .Where(c => c.Text.StartsWith(wordToComplete1, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Assert step 1
        Assert.Equal(3, response1.Length); // help, hold, home

        // Step 2: Second request "scoop <tab>"
        var wordToComplete2 = "";
        var cacheKey2 = CompletionCache.GetCacheKey("scoop", "scoop ");

        // Keys should be the same
        Assert.Equal(cacheKey1, cacheKey2);

        // Get cached completions
        var cachedCompletions = cache.TryGetCompletions(cacheKey2);
        Assert.NotNull(cachedCompletions);
        Assert.Equal(8, cachedCompletions.Length); // Should have all 8 completions

        // Filter for response (empty string = no filter)
        var response2 = string.IsNullOrEmpty(wordToComplete2)
            ? cachedCompletions
            : cachedCompletions.Where(c => c.Text.StartsWith(wordToComplete2, StringComparison.OrdinalIgnoreCase)).ToArray();

        // Assert step 2 - THIS IS THE KEY TEST
        Assert.Equal(8, response2.Length); // Should return ALL completions, not just 3
        Assert.Contains(response2, c => c.Text == "help");
        Assert.Contains(response2, c => c.Text == "install");
        Assert.Contains(response2, c => c.Text == "search");
    }

    [Theory]
    [InlineData("scoop h", "h", 3)]      // help, hold, home
    [InlineData("scoop ", "", 25)]       // All scoop commands (at least 25)
    [InlineData("scoop ho", "ho", 2)]    // hold, home
    [InlineData("scoop u", "u", 3)]      // unhold, uninstall, update
    [InlineData("git ch", "ch", 2)]      // checkout, cherry-pick
    [InlineData("git ", "", 30)]         // All git commands (at least 30)
    public void CommandCompleter_FilteringBehavior(string commandLine, string wordToComplete, int minExpected)
    {
        // Act
        var completions = CommandCompleter.GetCompletions(commandLine.AsSpan(), wordToComplete.AsSpan());

        // Assert
        Assert.True(completions.Count() >= minExpected,
            $"Expected at least {minExpected} completions for '{commandLine}', got {completions.Count()}");

        // Verify all returned completions start with the prefix (if prefix is not empty)
        if (!string.IsNullOrEmpty(wordToComplete))
        {
            Assert.All(completions, c =>
                Assert.StartsWith(wordToComplete, c.CompletionText, StringComparison.OrdinalIgnoreCase));
        }
    }
}

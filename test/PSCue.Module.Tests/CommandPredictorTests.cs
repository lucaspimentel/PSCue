#pragma warning disable CA2252 // Opt into preview features for testing

namespace PSCue.Module.Tests;

public class CommandPredictorTests
{
    #region Combine Method Tests (Critical - Word Boundary Logic)

    /// <summary>
    /// Tests the Combine method with various input scenarios.
    /// Parameters: input, completionText, expectedResult
    /// </summary>
    [Theory]
    [InlineData("sco", "scoop", "scoop")]                                      // Basic overlap - completes word
    [InlineData("git chec", "checkout", "git checkout")]                       // Partial word completion
    [InlineData("claude plugin", "install", "claude plugin install")]          // CRITICAL: Word boundary respect (pluginstall bug)
    [InlineData("git ", "status", "git status")]                               // No overlap - adds space
    [InlineData("git", "git", "git")]                                          // Full overlap - no duplication
    [InlineData("git  ", "commit", "git  commit")]                             // Trailing spaces preserved
    [InlineData("", "git", "git")]                                             // Empty input returns completion
    [InlineData("git", "", "git ")]                                            // Empty completion adds space
    [InlineData("test@", "test@host", "test@host")]                            // Special characters
    [InlineData("café", "café-au-lait", "café-au-lait")]                       // Unicode handling
    [InlineData("git CHE", "checkout", "git checkout")]                        // Case insensitive matching
    [InlineData("git   commit   --am", "--amend", "git   commit   --amend")]  // Multiple spaces - last word only
    [InlineData("plugin", "install", "plugin install")]                        // Substring non-match (NOT "pluginstall")
    [InlineData("scoop update", "*", "scoop update *")]                        // Scoped commands
    [InlineData("git commit --a", "--amend", "git commit --amend")]            // Flag completion
    [InlineData("commandex", "example", "commandex example")]                  // Partial match not at word boundary
    [InlineData("git commit --am", "--amend", "git commit --amend")]           // Dash-separated words
    [InlineData("cd /usr/lo", "local", "cd /usr/lo local")]                    // Path completion (/ is word boundary)
    [InlineData("cd dotnet", "D:\\source\\dd-trace-dotnet\\", "cd D:\\source\\dd-trace-dotnet\\")] // Absolute path replaces word
    [InlineData("cd proj", "C:\\Users\\test\\projects\\", "cd C:\\Users\\test\\projects\\")] // Windows absolute path
    [InlineData("cd test", "/home/user/test/", "cd /home/user/test/")]        // Unix absolute path
    [InlineData("sl mydir", "D:\\temp\\mydir\\", "sl D:\\temp\\mydir\\")]     // Set-Location with absolute path
    [InlineData("git che", "checkout master", "git checkout master")]         // Multi-word completion
    [InlineData("git chec", "checkout -b feature", "git checkout -b feature")] // Multi-word with flag
    [InlineData("git ", "commit -m", "git commit -m")]                        // Multi-word with no partial
    [InlineData("docker", "run -it", "docker run -it")]                       // Multi-word from empty command
    public void Combine_VariousScenarios_ProducesExpectedResults(string input, string completionText, string expected)
    {
        var result = CommandPredictor.Combine(input, completionText);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Combine_LongInput_PerformsWell()
    {
        // Test: very long input still works correctly
        var longInput = "git commit -m \"this is a very long commit message that goes on and on\" --a";
        var result = CommandPredictor.Combine(longInput, "--amend");
        Assert.Contains("--amend", result);
        Assert.DoesNotContain("--a--amend", result); // Should not duplicate
    }

    #endregion
}

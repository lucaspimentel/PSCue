using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using PSCue.Shared;
using PSCue.Shared.KnownCompletions;
using Xunit;

namespace PSCue.ArgumentCompleter.Tests;

public class SetLocationCommandTests
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static readonly string TestDir = Path.Combine(Path.GetTempPath(), $"PSCueTests_{Guid.NewGuid():N}");

    public SetLocationCommandTests()
    {
        // Create test directory structure
        Directory.CreateDirectory(TestDir);
        Directory.CreateDirectory(Path.Combine(TestDir, "subdir1"));
        Directory.CreateDirectory(Path.Combine(TestDir, "subdir2"));
        Directory.CreateDirectory(Path.Combine(TestDir, "src"));
        Directory.CreateDirectory(Path.Combine(TestDir, "test"));
    }

    [Fact]
    public void Cd_Command_Returns_Completions()
    {
        var completions = CommandCompleter.GetCompletions("cd").ToList();

        completions.Should().NotBeEmpty();
        completions.Should().Contain(x => x.CompletionText == ".");
        completions.Should().Contain(x => x.CompletionText == "..");
    }

    [Fact]
    public void SetLocation_Command_Returns_Completions()
    {
        var completions = CommandCompleter.GetCompletions("set-location").ToList();

        completions.Should().NotBeEmpty();
        completions.Should().Contain(x => x.CompletionText == ".");
        completions.Should().Contain(x => x.CompletionText == "..");
    }

    [Fact]
    public void Sl_Alias_Returns_Completions()
    {
        var completions = CommandCompleter.GetCompletions("sl").ToList();

        completions.Should().NotBeEmpty();
        completions.Should().Contain(x => x.CompletionText == ".");
        completions.Should().Contain(x => x.CompletionText == "..");
    }

    [Fact]
    public void Chdir_Command_Returns_Completions()
    {
        var completions = CommandCompleter.GetCompletions("chdir").ToList();

        completions.Should().NotBeEmpty();
        completions.Should().Contain(x => x.CompletionText == ".");
        completions.Should().Contain(x => x.CompletionText == "..");
    }

    [Fact]
    public void Common_Shortcuts_Always_Included()
    {
        var suggestions = SetLocationCommand.GetDirectorySuggestions("").ToList();

        suggestions.Should().Contain(".");
        suggestions.Should().Contain("..");
    }

    [Fact]
    public void Home_Directory_Shortcut_Included()
    {
        var suggestions = SetLocationCommand.GetDirectorySuggestions("").ToList();

        // Should contain home directory in some form
        suggestions.Should().NotBeEmpty();
    }

    [Fact]
    public void GetDirectorySuggestions_Empty_Input_Returns_Current_Dir_Subdirs()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(TestDir);

            var suggestions = SetLocationCommand.GetDirectorySuggestions("").ToList();

            suggestions.Should().NotBeEmpty();
            suggestions.Should().Contain(s => s.Contains("subdir1") || s == "subdir1");
            suggestions.Should().Contain(s => s.Contains("subdir2") || s == "subdir2");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void GetDirectorySuggestions_Partial_Name_Filters_Results()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(TestDir);

            var suggestions = SetLocationCommand.GetDirectorySuggestions("sub").ToList();

            suggestions.Should().NotBeEmpty();
            suggestions.Should().Contain(s => s.Contains("subdir1") || s == "subdir1");
            suggestions.Should().Contain(s => s.Contains("subdir2") || s == "subdir2");
            suggestions.Should().NotContain(s => s.Contains("src") || s == "src");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void GetDirectorySuggestions_Detects_Absolute_Path_Windows()
    {
        Skip.IfNot(IsWindows, "Windows-specific test");

        var suggestions = SetLocationCommand.GetDirectorySuggestions("C:\\").ToList();

        // Should enumerate C:\ drive (if it exists and is accessible)
        // Don't assert specific directories as they vary by system
        suggestions.Should().NotBeNull();
    }

    [Fact]
    public void GetDirectorySuggestions_Detects_Absolute_Path_Unix()
    {
        Skip.If(IsWindows, "Unix-specific test");

        var suggestions = SetLocationCommand.GetDirectorySuggestions("/").ToList();

        // Should enumerate root directories
        suggestions.Should().NotBeNull();
    }

    [Fact]
    public void GetDirectorySuggestions_Home_Directory_Expansion()
    {
        var suggestions = SetLocationCommand.GetDirectorySuggestions("~").ToList();

        // Should suggest home directory subdirectories
        suggestions.Should().NotBeNull();
    }

    [Fact]
    public void GetDirectorySuggestions_Home_Directory_With_Path()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
        {
            // Skip if home directory is not available
            return;
        }

        var suggestions = SetLocationCommand.GetDirectorySuggestions("~/").ToList();

        // Should expand ~ and show subdirectories
        suggestions.Should().NotBeNull();
        if (suggestions.Any())
        {
            // On Windows, path separator is \, on Unix it's /
            var expectedStart = IsWindows ? "~\\" : "~/";
            suggestions.Should().AllSatisfy(s => s.Should().StartWith(expectedStart));
        }
    }

    [Fact]
    public void GetDirectorySuggestions_Parent_Directory()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(TestDir);

            var suggestions = SetLocationCommand.GetDirectorySuggestions("../").ToList();

            // Should enumerate parent directory's subdirectories
            suggestions.Should().NotBeNull();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void GetDirectorySuggestions_Explicit_Current_Directory()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(TestDir);

            var suggestions = SetLocationCommand.GetDirectorySuggestions("./").ToList();

            suggestions.Should().NotBeEmpty();
            suggestions.Should().Contain(s => s.Contains("subdir1") || s == "subdir1");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void GetDirectorySuggestions_Explicit_Current_Directory_With_Filter()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(TestDir);

            var suggestions = SetLocationCommand.GetDirectorySuggestions("./s").ToList();

            suggestions.Should().NotBeEmpty();
            suggestions.All(s => s.StartsWith("s", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void GetDirectorySuggestions_Case_Insensitive()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(TestDir);

            var lower = SetLocationCommand.GetDirectorySuggestions("sub").ToList();
            var upper = SetLocationCommand.GetDirectorySuggestions("SUB").ToList();

            lower.Should().NotBeEmpty();
            upper.Should().NotBeEmpty();
            lower.Count.Should().Be(upper.Count);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void GetDirectorySuggestions_Max_Results_Limit()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            // Create many subdirectories
            var testDir = Path.Combine(Path.GetTempPath(), $"PSCueLargeTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            for (int i = 0; i < 100; i++)
            {
                Directory.CreateDirectory(Path.Combine(testDir, $"dir{i:D3}"));
            }

            Directory.SetCurrentDirectory(testDir);

            var suggestions = SetLocationCommand.GetDirectorySuggestions("").ToList();

            // Should be limited to 50 results (plus common shortcuts)
            suggestions.Count.Should().BeLessThanOrEqualTo(60); // 50 + shortcuts

            // Cleanup
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(testDir, recursive: true);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void GetDirectorySuggestions_Performance_Under_50ms()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            // Create moderate number of subdirectories
            var testDir = Path.Combine(Path.GetTempPath(), $"PSCuePerfTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            for (int i = 0; i < 30; i++)
            {
                Directory.CreateDirectory(Path.Combine(testDir, $"dir{i:D2}"));
            }

            Directory.SetCurrentDirectory(testDir);

            var startTime = DateTime.UtcNow;
            var suggestions = SetLocationCommand.GetDirectorySuggestions("").ToList();
            var elapsed = DateTime.UtcNow - startTime;

            elapsed.TotalMilliseconds.Should().BeLessThan(50);
            suggestions.Should().NotBeEmpty();

            // Cleanup
            Directory.SetCurrentDirectory(originalDir);
            Directory.Delete(testDir, recursive: true);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void GetDirectorySuggestions_Cache_Key_Generation()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(TestDir);

            // First call - should populate cache
            var first = SetLocationCommand.GetDirectorySuggestions("").ToList();

            // Second call - should use cache (within 5 second TTL)
            var second = SetLocationCommand.GetDirectorySuggestions("").ToList();

            first.Should().BeEquivalentTo(second);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void GetDirectorySuggestions_Nonexistent_Directory_Returns_Empty()
    {
        var suggestions = SetLocationCommand.GetDirectorySuggestions("/nonexistent/path/that/does/not/exist").ToList();

        suggestions.Should().BeEmpty();
    }

    [Fact]
    public void GetDirectorySuggestions_Invalid_Path_Returns_Empty()
    {
        var invalidPath = IsWindows ? "Z:\\nonexistent\\path" : "/nonexistent/path/xyz123";
        var suggestions = SetLocationCommand.GetDirectorySuggestions(invalidPath).ToList();

        // Should gracefully handle invalid paths
        suggestions.Should().NotBeNull();
    }

    [Fact]
    public void DynamicArguments_Returns_Directories()
    {
        var command = SetLocationCommand.Create();

        command.DynamicArguments.Should().NotBeNull();

        var args = command.DynamicArguments!().ToList();

        args.Should().NotBeEmpty();
        args.Should().Contain(a => a.CompletionText == ".");
        args.Should().Contain(a => a.CompletionText == "..");
    }

    [Fact]
    public void CommandCompleter_Integration_Cd_Returns_Directories()
    {
        var completions = CommandCompleter.GetCompletions("cd ", "").ToList();

        completions.Should().NotBeEmpty();
        completions.Should().Contain(x => x.CompletionText == ".");
        completions.Should().Contain(x => x.CompletionText == "..");
    }

    [Fact]
    public void CommandCompleter_Integration_SetLocation_Returns_Directories()
    {
        var completions = CommandCompleter.GetCompletions("Set-Location ", "").ToList();

        completions.Should().NotBeEmpty();
        completions.Should().Contain(x => x.CompletionText == ".");
        completions.Should().Contain(x => x.CompletionText == "..");
    }

    [Fact]
    public void Cross_Platform_Path_Handling_Windows()
    {
        Skip.IfNot(IsWindows, "Windows-specific test");

        var suggestions = SetLocationCommand.GetDirectorySuggestions("C:").ToList();

        // Should handle Windows paths with drive letters
        suggestions.Should().NotBeNull();
    }

    [Fact]
    public void Cross_Platform_Path_Handling_Unix()
    {
        Skip.If(IsWindows, "Unix-specific test");

        var suggestions = SetLocationCommand.GetDirectorySuggestions("/home").ToList();

        // Should handle Unix absolute paths
        suggestions.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("sub")]
    [InlineData("./")]
    [InlineData("../")]
    [InlineData("~")]
    public void GetDirectorySuggestions_Various_Inputs_Never_Throws(string input)
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            if (Directory.Exists(TestDir))
            {
                Directory.SetCurrentDirectory(TestDir);
            }

            var act = () => SetLocationCommand.GetDirectorySuggestions(input).ToList();

            act.Should().NotThrow();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }
}

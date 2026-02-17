using System;
using System.IO;
using Xunit;

namespace PSCue.Module.Tests;

/// <summary>
/// Tests to prevent path corruption issues that caused duplicate entries and missing backslashes.
/// These tests ensure the fixes for backslash preservation and trailing separator consistency remain in place.
/// </summary>
public class PathCorruptionTests
{
    [Theory]
    [InlineData(@"D:\source\datadog\dd-trace-dotnet")]
    [InlineData(@"C:\Users\Test\Documents")]
    public void CommandParser_PreservesBackslashesInWindowsPaths(string windowsPath)
    {
        // Arrange
        var parser = new CommandParser();
        var commandLine = $"cd {windowsPath}";

        // Act
        var result = parser.Parse(commandLine);

        // Assert
        Assert.Equal("cd", result.Command);
        Assert.Single(result.Arguments);

        // The path should still contain all backslashes
        var parsedPath = result.Arguments[0].Text;
        Assert.Equal(windowsPath, parsedPath);

        // Count backslashes - they should match
        var originalBackslashCount = windowsPath.Count(c => c == '\\');
        var parsedBackslashCount = parsedPath.Count(c => c == '\\');
        Assert.Equal(originalBackslashCount, parsedBackslashCount);
    }

    [Fact]
    public void CommandParser_PreservesBackslashesInPathsWithSpaces()
    {
        // Arrange
        var parser = new CommandParser();
        var windowsPath = @"C:\Program Files\App";
        var commandLine = $"cd \"{windowsPath}\"";

        // Act
        var result = parser.Parse(commandLine);

        // Assert
        Assert.Equal("cd", result.Command);
        Assert.Single(result.Arguments);

        // The path should still contain all backslashes
        var parsedPath = result.Arguments[0].Text;
        Assert.Equal(windowsPath, parsedPath);
        Assert.Contains(@"\Program Files\", parsedPath);
    }

    [Fact]
    public void CommandParser_PreservesBackslashesInQuotedPaths()
    {
        // Arrange
        var parser = new CommandParser();
        var windowsPath = @"D:\source\my folder\project";
        var commandLine = $"cd \"{windowsPath}\"";

        // Act
        var result = parser.Parse(commandLine);

        // Assert
        var parsedPath = result.Arguments[0].Text;
        Assert.Equal(windowsPath, parsedPath);
        Assert.Contains(@"\source\", parsedPath);
        Assert.Contains(@"\my folder\", parsedPath);
    }

    [Fact]
    public void CommandParser_HandlesEscapedBackslash()
    {
        // Arrange
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");
        var commandLine = @"git commit -m ""test \\ message""";

        // Act
        var result = parser.Parse(commandLine);

        // Assert
        Assert.Equal(3, result.Arguments.Count);
        // Escaped backslash (\\) should become single backslash
        Assert.Equal(@"test \ message", result.Arguments[2].Text);
    }

    [Fact]
    public void CommandParser_DoesNotTreatSingleBackslashAsEscape()
    {
        // Arrange
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");
        var commandLine = @"git commit -m ""test\nmessage""";

        // Act
        var result = parser.Parse(commandLine);

        // Assert
        Assert.Equal(3, result.Arguments.Count);
        // \n is not a recognized escape sequence, both characters should be preserved
        Assert.Equal(@"test\nmessage", result.Arguments[2].Text);
    }

    [Fact]
    public void ArgumentGraph_NormalizedPaths_HaveTrailingSeparator()
    {
        // Arrange
        var graph = new ArgumentGraph();
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Act - record the same directory multiple times with different trailing separator states
        graph.RecordUsage("cd", new[] { tempDir }, workingDirectory: tempDir);
        graph.RecordUsage("cd", new[] { tempDir + Path.DirectorySeparatorChar }, workingDirectory: tempDir);
        graph.RecordUsage("cd", new[] { tempDir + Path.DirectorySeparatorChar + Path.DirectorySeparatorChar }, workingDirectory: tempDir);

        // Assert - should be deduplicated to a single entry
        var knowledge = graph.GetCommandKnowledge("cd");
        Assert.NotNull(knowledge);
        Assert.Single(knowledge.Arguments);

        // The stored path must have exactly one trailing separator
        var storedPath = knowledge.Arguments.Keys.First();
        Assert.True(
            storedPath.EndsWith(Path.DirectorySeparatorChar) || storedPath.EndsWith(Path.AltDirectorySeparatorChar),
            $"Normalized path '{storedPath}' should end with directory separator"
        );

        // Should not have double separators at the end
        var separatorString = Path.DirectorySeparatorChar.ToString();
        Assert.False(
            storedPath.EndsWith(separatorString + separatorString),
            $"Normalized path '{storedPath}' should not have double trailing separators"
        );

        // All three calls should have been merged
        Assert.Equal(3, knowledge.Arguments.Values.First().UsageCount);
    }

    [Fact]
    public void ArgumentGraph_DifferentPathFormats_DeduplicateCorrectly()
    {
        // Arrange
        var graph = new ArgumentGraph();
        var tempDir = Path.GetTempPath();
        var targetDir = Path.Combine(tempDir, "test-dedup");
        Directory.CreateDirectory(targetDir);

        try
        {
            // Act - record the same directory via different path formats
            var absolutePath = Path.GetFullPath(targetDir);
            var absoluteWithTrailing = absolutePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var relativePath = "test-dedup";

            graph.RecordUsage("cd", new[] { absolutePath }, workingDirectory: tempDir);
            graph.RecordUsage("cd", new[] { absoluteWithTrailing }, workingDirectory: tempDir);
            graph.RecordUsage("cd", new[] { relativePath }, workingDirectory: tempDir);

            // Assert - all should resolve to the same normalized path
            var knowledge = graph.GetCommandKnowledge("cd");
            Assert.NotNull(knowledge);
            Assert.Single(knowledge.Arguments);

            // Usage count should be 3 (all merged)
            var stats = knowledge.Arguments.Values.First();
            Assert.Equal(3, stats.UsageCount);

            // The normalized path should have trailing separator
            var normalizedPath = knowledge.Arguments.Keys.First();
            Assert.True(
                normalizedPath.EndsWith(Path.DirectorySeparatorChar) || normalizedPath.EndsWith(Path.AltDirectorySeparatorChar),
                $"Normalized path '{normalizedPath}' should have trailing separator"
            );
        }
        finally
        {
            try { Directory.Delete(targetDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ArgumentGraph_WithoutWorkingDirectory_SkipsNormalization()
    {
        // Arrange
        var graph = new ArgumentGraph();
        var path = @"D:\source\test";

        // Act - record without working directory (should skip normalization)
        graph.RecordUsage("cd", new[] { path }, workingDirectory: null);

        // Assert - path should be stored as-is (not normalized)
        var knowledge = graph.GetCommandKnowledge("cd");
        Assert.NotNull(knowledge);

        // When working directory is null, paths are stored without normalization
        // This is expected behavior per CLAUDE.md documentation
        var storedPath = knowledge.Arguments.Keys.First();
        Assert.Equal(path, storedPath);
    }

    [Fact]
    public void CommandParser_ComplexWindowsPath_PreservesStructure()
    {
        // Arrange - simulate the exact corruption scenario from the bug report
        var parser = new CommandParser();
        var originalPath = @"D:\source\datadog\dd-trace-dotnet-APMSVLS-58";
        var commandLine = $"cd {originalPath}";

        // Act
        var result = parser.Parse(commandLine);

        // Assert
        var parsedPath = result.Arguments[0].Text;

        // Path should be exactly as input - no corruption
        Assert.Equal(originalPath, parsedPath);

        // Verify specific structure is preserved
        Assert.StartsWith(@"D:\", parsedPath);
        Assert.Contains(@"\source\", parsedPath);
        Assert.Contains(@"\datadog\", parsedPath);
        Assert.EndsWith("dd-trace-dotnet-APMSVLS-58", parsedPath);

        // Should NOT be corrupted like: "D:sourcedatadogdd-trace-dotnet-APMSVLS-58"
        Assert.DoesNotContain("D:source", parsedPath);
        Assert.DoesNotContain("datadogdd-trace", parsedPath);
    }

    [Fact]
    public void CommandParser_EscapedQuotes_PreservesQuotesNotBackslashes()
    {
        // Arrange
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");
        var commandLine = @"git commit -m ""test \""quoted\"" message""";

        // Act
        var result = parser.Parse(commandLine);

        // Assert
        Assert.Equal(3, result.Arguments.Count);
        // Escaped quotes should be preserved, backslashes should be consumed
        Assert.Equal(@"test ""quoted"" message", result.Arguments[2].Text);
    }

    [Theory]
    [InlineData(@"C:\Users")]
    [InlineData(@"D:\source\")]
    [InlineData(@"E:\projects\app")]
    public void ArgumentGraph_AlwaysNormalizesWithTrailingSeparator(string inputPath)
    {
        // Arrange
        var graph = new ArgumentGraph();
        var workingDir = Path.GetTempPath();

        // Act
        graph.RecordUsage("cd", new[] { inputPath }, workingDirectory: workingDir);

        // Assert
        var knowledge = graph.GetCommandKnowledge("cd");
        Assert.NotNull(knowledge);

        var storedPath = knowledge.Arguments.Keys.First();
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), storedPath);
    }
}

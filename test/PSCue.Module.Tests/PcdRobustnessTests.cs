using System;
using System.Collections.Generic;
using System.IO;
using PSCue.Module;
using Xunit;

namespace PSCue.Module.Tests;

/// <summary>
/// Tests for robustness of PCD when dealing with non-existent paths.
/// Ensures pcd gracefully handles stale database entries.
/// </summary>
public class PcdRobustnessTests : IDisposable
{
    private readonly ArgumentGraph _graph;
    private readonly string _testRootDir;
    private readonly List<string> _tempDirectories;

    public PcdRobustnessTests()
    {
        _graph = new ArgumentGraph();
        _tempDirectories = new List<string>();

        // Create a temporary test directory structure
        _testRootDir = Path.Combine(Path.GetTempPath(), $"PSCue_Robustness_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRootDir);
        _tempDirectories.Add(_testRootDir);
    }

    public void Dispose()
    {
        // Cleanup temp directories
        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string CreateTempDirectory(string name)
    {
        var path = Path.Combine(_testRootDir, name);
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    [Fact]
    public void GetSuggestions_SkipsNonExistentPaths_ReturnsOnlyExisting()
    {
        // Arrange - Record paths, then delete some
        var existingPath = CreateTempDirectory("existing-dir");
        var deletedPath = Path.Combine(_testRootDir, "deleted-dir");

        // Record both in graph
        _graph.RecordUsage("cd", new[] { existingPath }, _testRootDir);
        _graph.RecordUsage("cd", new[] { deletedPath }, _testRootDir);

        // Create then delete the second path to simulate stale database
        Directory.CreateDirectory(deletedPath);
        Directory.Delete(deletedPath);

        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("dir", _testRootDir, 10);

        // Assert - Only existing path should be in suggestions
        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s => Assert.True(Directory.Exists(s.DisplayPath),
            $"Suggestion {s.DisplayPath} should exist"));

        // The deleted path should not appear
        Assert.DoesNotContain(suggestions, s => s.DisplayPath.Contains("deleted-dir"));
        Assert.Contains(suggestions, s => s.DisplayPath.Contains("existing-dir"));
    }

    [Fact]
    public void GetSuggestions_AllPathsDeleted_ReturnsEmpty()
    {
        // Arrange - Record a path then delete it
        var deletedPath = Path.Combine(_testRootDir, "temp-dir");
        Directory.CreateDirectory(deletedPath);

        _graph.RecordUsage("cd", new[] { deletedPath }, _testRootDir);

        // Delete the directory to simulate stale database
        Directory.Delete(deletedPath);

        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("temp", _testRootDir, 10);

        // Assert - Should return empty since the only match doesn't exist
        Assert.Empty(suggestions);
    }

    [Fact]
    public void GetSuggestions_MultipleMatches_FirstDeletedSecondExists_ReturnsSecond()
    {
        // Arrange - Create two matching directories
        var firstPath = Path.Combine(_testRootDir, "project-main");
        var secondPath = CreateTempDirectory("project-feature");

        Directory.CreateDirectory(firstPath);

        // Record with higher usage on the first (would normally rank first)
        _graph.RecordUsage("cd", new[] { firstPath }, _testRootDir);
        _graph.RecordUsage("cd", new[] { firstPath }, _testRootDir);
        _graph.RecordUsage("cd", new[] { firstPath }, _testRootDir);
        _graph.RecordUsage("cd", new[] { secondPath }, _testRootDir);

        // Delete the first path
        Directory.Delete(firstPath);

        var engine = new PcdCompletionEngine(_graph);

        // Act
        var suggestions = engine.GetSuggestions("project", _testRootDir, 10);

        // Assert - Should only return the second (existing) path
        Assert.NotEmpty(suggestions);
        var topResult = suggestions[0];
        Assert.Contains("project-feature", topResult.DisplayPath);
        Assert.DoesNotContain("project-main", topResult.DisplayPath);
    }
}

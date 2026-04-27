using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PSCue.Module.Tests;

/// <summary>
/// Verifies that path-storing data structures keep case-distinct paths separate on
/// case-sensitive filesystems (Linux and macOS). On Windows these tests are skipped
/// because the filesystem is case-insensitive and PathComparer collapses such paths.
/// </summary>
public class LinuxCaseSensitivityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _testDbPath;

    public LinuxCaseSensitivityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PSCue.LinuxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _testDbPath = Path.Combine(_tempDir, "linux-case-test.db");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static void RequireCaseSensitiveFs()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Test requires a case-sensitive filesystem.");
        }
    }

    [Fact]
    public void BookmarkManager_StoresCaseDistinctPathsSeparately()
    {
        RequireCaseSensitiveFs();

        var fooPath = Path.Combine(_tempDir, "Foo");
        var lowerPath = Path.Combine(_tempDir, "foo");

        var bm = new BookmarkManager();
        var (addedFoo, _) = bm.Toggle(fooPath);
        var (addedLower, _) = bm.Toggle(lowerPath);

        Assert.True(addedFoo);
        Assert.True(addedLower);
        Assert.True(bm.IsBookmarked(fooPath));
        Assert.True(bm.IsBookmarked(lowerPath));
        Assert.Equal(2, bm.GetAll().Count);
    }

    [Fact]
    public void ArgumentGraph_RecordsCaseDistinctPathsAsDistinctArguments()
    {
        RequireCaseSensitiveFs();

        var graph = new ArgumentGraph();

        graph.RecordUsage("cd", new[] { "/tmp/SomeProject" });
        graph.RecordUsage("cd", new[] { "/tmp/someproject" });

        var knowledge = graph.GetCommandKnowledge("cd");
        Assert.NotNull(knowledge);
        Assert.True(knowledge!.Arguments.ContainsKey("/tmp/SomeProject"));
        Assert.True(knowledge.Arguments.ContainsKey("/tmp/someproject"));
        Assert.Equal(1, knowledge.Arguments["/tmp/SomeProject"].UsageCount);
        Assert.Equal(1, knowledge.Arguments["/tmp/someproject"].UsageCount);
    }

    [Fact]
    public void PersistenceManager_RoundTripsCaseDistinctPaths()
    {
        RequireCaseSensitiveFs();

        var graph = new ArgumentGraph();
        graph.RecordUsage("cd", new[] { "/tmp/SomeProject" });
        graph.RecordUsage("cd", new[] { "/tmp/someproject" });

        using (var persistence = new PersistenceManager(_testDbPath))
        {
            persistence.SaveArgumentGraph(graph);
        }

        using var reopened = new PersistenceManager(_testDbPath);
        using var connection = reopened.CreateSharedConnection();
        var loaded = reopened.LoadArgumentGraph(connection);

        var knowledge = loaded.GetCommandKnowledge("cd");
        Assert.NotNull(knowledge);
        Assert.True(knowledge!.Arguments.ContainsKey("/tmp/SomeProject"));
        Assert.True(knowledge.Arguments.ContainsKey("/tmp/someproject"));
    }

    [Fact]
    public void PersistenceManager_RoundTripsCaseDistinctBookmarks()
    {
        RequireCaseSensitiveFs();

        var fooPath = Path.Combine(_tempDir, "Foo");
        var lowerPath = Path.Combine(_tempDir, "foo");

        using (var persistence = new PersistenceManager(_testDbPath))
        {
            persistence.SaveBookmark(fooPath);
            persistence.SaveBookmark(lowerPath);
        }

        using var reopened = new PersistenceManager(_testDbPath);
        using var connection = reopened.CreateSharedConnection();
        var loaded = reopened.LoadBookmarks(connection).Select(b => b.Path).ToList();

        Assert.Contains(fooPath, loaded);
        Assert.Contains(lowerPath, loaded);
    }
}

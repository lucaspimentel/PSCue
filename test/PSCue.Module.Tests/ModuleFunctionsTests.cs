using System;
using System.Linq;
using Xunit;
using PSCue.Module;

namespace PSCue.Module.Tests;

/// <summary>
/// Tests for module function APIs exposed for PowerShell.
/// These test the public APIs added to classes to support PowerShell module functions.
/// NOTE: Full integration testing is done via test-scripts/test-module-functions.ps1
/// </summary>
public class ModuleFunctionsTests
{

    // The APIs tested here are primarily used by PowerShell module functions.
    // Comprehensive integration testing is done via test-scripts/test-module-functions.ps1
    // These unit tests verify basic behavior of the public APIs.

    // CompletionCache tests removed - CompletionCache no longer exists in the architecture

    #region ArgumentGraph Tests

    [Fact]
    public void GetAllCommands_ReturnsEmptyEnumerable_WhenGraphIsEmpty()
    {
        var graph = new ArgumentGraph();
        var commands = graph.GetAllCommands().ToList();
        Assert.NotNull(commands);
        Assert.Empty(commands);
    }

    [Fact]
    public void GetAllCommands_ReturnsAllLearnedCommands()
    {
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", ["status"]);
        graph.RecordUsage("docker", ["ps", "-a"]);

        var commands = graph.GetAllCommands().ToList();
        Assert.Equal(2, commands.Count);
        Assert.Contains(commands, c => c.Key == "git");
        Assert.Contains(commands, c => c.Key == "docker");
    }

    [Fact]
    public void GetCommandKnowledge_ReturnsNull_WhenCommandNotFound()
    {
        var graph = new ArgumentGraph();
        var knowledge = graph.GetCommandKnowledge("nonexistent");
        Assert.Null(knowledge);
    }

    [Fact]
    public void GetCommandKnowledge_ReturnsKnowledge_WhenCommandExists()
    {
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", ["status", "--short"]);

        var knowledge = graph.GetCommandKnowledge("git");
        Assert.NotNull(knowledge);
        Assert.Equal("git", knowledge.Command);
        Assert.NotNull(knowledge.Arguments);
        Assert.Equal(2, knowledge.Arguments.Count);
    }

    #endregion

    #region CommandHistory Tests

    [Fact]
    public void GetRecent_ReturnsEmptyList_WhenHistoryIsEmpty()
    {
        var history = new CommandHistory();
        var recent = history.GetRecent();
        Assert.NotNull(recent);
        Assert.Empty(recent);
    }

    [Fact]
    public void GetRecent_ReturnsRecentCommands()
    {
        var history = new CommandHistory();
        history.Add(new CommandHistoryEntry
        {
            Command = "git",
            Arguments = ["status"],
            Timestamp = DateTime.UtcNow,
            Success = true
        });

        var recent = history.GetRecent();
        Assert.Single(recent);
    }

    #endregion
}

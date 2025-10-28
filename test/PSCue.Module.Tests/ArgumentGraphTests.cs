using Xunit;
using PSCue.Module;

namespace PSCue.Module.Tests;

public class ArgumentGraphTests
{
    [Fact]
    public void RecordUsage_CreatesCommandKnowledge()
    {
        // Arrange
        var graph = new ArgumentGraph();

        // Act
        graph.RecordUsage("git", new[] { "commit", "-m", "message" });

        // Assert
        var knowledge = graph.GetCommandKnowledge("git");
        Assert.NotNull(knowledge);
        Assert.Equal("git", knowledge.Command);
        Assert.Equal(1, knowledge.TotalUsageCount);
        Assert.Equal(3, knowledge.Arguments.Count);
    }

    [Fact]
    public void RecordUsage_IncrementsUsageCount()
    {
        // Arrange
        var graph = new ArgumentGraph();

        // Act
        graph.RecordUsage("git", new[] { "commit", "-m" });
        graph.RecordUsage("git", new[] { "commit", "-a" });
        graph.RecordUsage("git", new[] { "push" });

        // Assert
        var knowledge = graph.GetCommandKnowledge("git");
        Assert.NotNull(knowledge);
        Assert.Equal(3, knowledge.TotalUsageCount);

        // "commit" should have usage count of 2
        Assert.True(knowledge.Arguments.TryGetValue("commit", out var commitStats));
        Assert.Equal(2, commitStats.UsageCount);

        // "-m" should have usage count of 1
        Assert.True(knowledge.Arguments.TryGetValue("-m", out var mStats));
        Assert.Equal(1, mStats.UsageCount);
    }

    [Fact]
    public void RecordUsage_TracksCoOccurrences()
    {
        // Arrange
        var graph = new ArgumentGraph();

        // Act
        graph.RecordUsage("git", new[] { "commit", "-m", "-a" });
        graph.RecordUsage("git", new[] { "commit", "-m", "message" });

        // Assert
        var knowledge = graph.GetCommandKnowledge("git");
        Assert.NotNull(knowledge);

        var commitStats = knowledge.Arguments["commit"];
        Assert.Contains("-m", commitStats.CoOccurrences.Keys);
        Assert.Contains("-a", commitStats.CoOccurrences.Keys);
        Assert.Equal(2, commitStats.CoOccurrences["-m"]); // Appeared together twice
        Assert.Equal(1, commitStats.CoOccurrences["-a"]); // Appeared together once
    }

    [Fact]
    public void RecordUsage_TracksFlagCombinations()
    {
        // Arrange
        var graph = new ArgumentGraph();

        // Act
        graph.RecordUsage("ls", new[] { "-l", "-a", "-h" });
        graph.RecordUsage("ls", new[] { "-l", "-a" });

        // Assert
        var knowledge = graph.GetCommandKnowledge("ls");
        Assert.NotNull(knowledge);
        Assert.Contains("-l -a -h", knowledge.FlagCombinations.Keys);
        Assert.Contains("-l -a", knowledge.FlagCombinations.Keys);
    }

    [Fact]
    public void RecordUsage_IdentifiesFlags()
    {
        // Arrange
        var graph = new ArgumentGraph();

        // Act
        graph.RecordUsage("git", new[] { "commit", "-m", "message", "--amend" });

        // Assert
        var knowledge = graph.GetCommandKnowledge("git");
        Assert.NotNull(knowledge);

        Assert.False(knowledge.Arguments["commit"].IsFlag);
        Assert.False(knowledge.Arguments["message"].IsFlag);
        Assert.True(knowledge.Arguments["-m"].IsFlag);
        Assert.True(knowledge.Arguments["--amend"].IsFlag);
    }

    [Fact]
    public void GetSuggestions_ReturnsMostFrequent()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "commit", "-m" });
        graph.RecordUsage("git", new[] { "commit", "-a" });
        graph.RecordUsage("git", new[] { "commit", "-m" });
        graph.RecordUsage("git", new[] { "push" });

        // Act
        var suggestions = graph.GetSuggestions("git", Array.Empty<string>(), maxResults: 10);

        // Assert
        Assert.NotEmpty(suggestions);
        // "commit" should be first (used 3 times)
        Assert.Equal("commit", suggestions[0].Argument);
        Assert.Equal(3, suggestions[0].UsageCount);
        // "-m" should be second (used 2 times)
        Assert.Equal("-m", suggestions[1].Argument);
        Assert.Equal(2, suggestions[1].UsageCount);
    }

    [Fact]
    public void GetSuggestions_ExcludesAlreadyTyped()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "commit", "-m", "-a" });

        // Act
        var suggestions = graph.GetSuggestions("git", new[] { "commit" }, maxResults: 10);

        // Assert
        Assert.DoesNotContain(suggestions, s => s.Argument == "commit");
        Assert.Contains(suggestions, s => s.Argument == "-m");
        Assert.Contains(suggestions, s => s.Argument == "-a");
    }

    [Fact]
    public void GetSuggestions_LimitsResults()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "a", "b", "c", "d", "e", "f" });

        // Act
        var suggestions = graph.GetSuggestions("git", Array.Empty<string>(), maxResults: 3);

        // Assert
        Assert.Equal(3, suggestions.Count);
    }

    [Fact]
    public void GetSuggestions_UnknownCommand_ReturnsEmpty()
    {
        // Arrange
        var graph = new ArgumentGraph();

        // Act
        var suggestions = graph.GetSuggestions("unknown", Array.Empty<string>());

        // Assert
        Assert.Empty(suggestions);
    }

    [Fact]
    public void ArgumentStats_GetRecencyScore_RecentHigher()
    {
        // Arrange
        var recentStats = new ArgumentStats
        {
            Argument = "recent",
            LastUsed = DateTime.UtcNow.AddHours(-1)
        };

        var oldStats = new ArgumentStats
        {
            Argument = "old",
            LastUsed = DateTime.UtcNow.AddDays(-60)
        };

        // Act
        var recentScore = recentStats.GetRecencyScore(decayDays: 30);
        var oldScore = oldStats.GetRecencyScore(decayDays: 30);

        // Assert
        Assert.True(recentScore > oldScore);
        Assert.True(recentScore > 0.9); // Recent should be close to 1.0
        Assert.True(oldScore < 0.2); // Old should be much lower
    }

    [Fact]
    public void ArgumentStats_GetScore_CombinesFrequencyAndRecency()
    {
        // Arrange
        var frequentRecentStats = new ArgumentStats
        {
            Argument = "fr",
            UsageCount = 10,
            LastUsed = DateTime.UtcNow.AddHours(-1)
        };

        var infrequentOldStats = new ArgumentStats
        {
            Argument = "io",
            UsageCount = 2,
            LastUsed = DateTime.UtcNow.AddDays(-60)
        };

        // Act
        var frScore = frequentRecentStats.GetScore(totalUsageCount: 20, decayDays: 30);
        var ioScore = infrequentOldStats.GetScore(totalUsageCount: 20, decayDays: 30);

        // Assert
        Assert.True(frScore > ioScore);
        Assert.True(frScore > 0.5); // High frequency + recent
        Assert.True(ioScore < 0.15); // Low frequency + old
    }

    [Fact]
    public void GetTrackedCommands_ReturnsAllCommands()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "commit" });
        graph.RecordUsage("npm", new[] { "install" });
        graph.RecordUsage("docker", new[] { "ps" });

        // Act
        var commands = graph.GetTrackedCommands();

        // Assert
        Assert.Equal(3, commands.Count);
        Assert.Contains("git", commands);
        Assert.Contains("npm", commands);
        Assert.Contains("docker", commands);
    }

    [Fact]
    public void Clear_RemovesAllData()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "commit", "-m" });
        graph.RecordUsage("npm", new[] { "install" });

        // Act
        graph.Clear();

        // Assert
        var commands = graph.GetTrackedCommands();
        Assert.Empty(commands);
        Assert.Null(graph.GetCommandKnowledge("git"));
    }

    [Fact]
    public void GetStatistics_CalculatesCorrectly()
    {
        // Arrange
        var graph = new ArgumentGraph();
        graph.RecordUsage("git", new[] { "commit", "-m" });
        graph.RecordUsage("git", new[] { "push" });
        graph.RecordUsage("npm", new[] { "install", "react" });

        // Act
        var stats = graph.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalCommands); // git, npm
        Assert.Equal(5, stats.TotalArguments); // commit, -m, push (git: 3) + install, react (npm: 2) = 5
        Assert.Equal(3, stats.TotalUsageCount); // 2 git + 1 npm
        Assert.Equal("git", stats.MostUsedCommand);
        Assert.Equal(2, stats.MostUsedCommandCount);
    }

    [Fact]
    public void RecordUsage_IgnoresEmptyArguments()
    {
        // Arrange
        var graph = new ArgumentGraph();

        // Act
        graph.RecordUsage("git", Array.Empty<string>());

        // Assert
        var knowledge = graph.GetCommandKnowledge("git");
        Assert.Null(knowledge); // Should not create entry for empty args
    }

    [Fact]
    public void RecordUsage_EnforcesArgumentLimit()
    {
        // Arrange
        var graph = new ArgumentGraph(maxCommands: 500, maxArgumentsPerCommand: 5);

        // Act - add 10 different arguments
        for (int i = 0; i < 10; i++)
        {
            graph.RecordUsage("test", new[] { $"arg{i}" });
            Thread.Sleep(2); // Ensure different timestamps
        }

        // Assert - should only keep most recent 5
        var knowledge = graph.GetCommandKnowledge("test");
        Assert.NotNull(knowledge);
        Assert.Equal(5, knowledge.Arguments.Count);

        // Should keep most recent ones (arg5-arg9)
        for (int i = 5; i < 10; i++)
        {
            Assert.True(knowledge.Arguments.ContainsKey($"arg{i}"), $"Should contain arg{i}");
        }
    }

    [Fact]
    public void RecordUsage_EnforcesCommandLimit()
    {
        // Arrange
        var graph = new ArgumentGraph(maxCommands: 3);

        // Act - add 5 different commands
        for (int i = 0; i < 5; i++)
        {
            graph.RecordUsage($"cmd{i}", new[] { "arg" });
            Thread.Sleep(2); // Ensure different timestamps
        }

        // Assert - should only keep most recent 3
        var commands = graph.GetTrackedCommands();
        Assert.Equal(3, commands.Count);

        // Should keep cmd2, cmd3, cmd4 (most recent)
        Assert.Contains("cmd2", commands);
        Assert.Contains("cmd3", commands);
        Assert.Contains("cmd4", commands);
        Assert.DoesNotContain("cmd0", commands);
        Assert.DoesNotContain("cmd1", commands);
    }

    [Fact]
    public void RecordUsage_CaseInsensitive()
    {
        // Arrange
        var graph = new ArgumentGraph();

        // Act
        graph.RecordUsage("Git", new[] { "Commit", "-M" });
        graph.RecordUsage("git", new[] { "commit", "-m" });

        // Assert
        var knowledge = graph.GetCommandKnowledge("GIT");
        Assert.NotNull(knowledge);
        Assert.Equal(2, knowledge.TotalUsageCount);

        // Arguments should be merged case-insensitively
        Assert.Equal(2, knowledge.Arguments["commit"].UsageCount);
        Assert.Equal(2, knowledge.Arguments["-m"].UsageCount);
    }
}

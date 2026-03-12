using System.Linq;
using PSCue.Shared;
using PSCue.Shared.Completions;
using PSCue.Shared.Completions.Json;
using PSCue.Shared.KnownCompletions;

namespace PSCue.ArgumentCompleter.Tests;

public class JsonCompletionLoaderTests
{
    private static readonly string DustJsonPath = Path.Combine(AppContext.BaseDirectory, "completions", "dust.json");

    [Fact]
    public void LoadFromFile_ReturnsCommand()
    {
        var command = JsonCompletionLoader.LoadFromFile(DustJsonPath);
        Assert.NotNull(command);
        Assert.Equal("dust", command.CompletionText);
        Assert.Equal("Like du but more intuitive", command.Tooltip);
    }

    [Fact]
    public void LoadFromFile_NonExistentPath_ReturnsNull()
    {
        var command = JsonCompletionLoader.LoadFromFile("/nonexistent/path.json");
        Assert.Null(command);
    }

    [Fact]
    public void ParameterCount_MatchesHardcoded()
    {
        var hardcoded = DustCommand.Create();
        var json = JsonCompletionLoader.LoadFromFile(DustJsonPath);

        Assert.NotNull(json);
        Assert.Equal(hardcoded.Parameters.Length, json.Parameters.Length);
    }

    [Fact]
    public void AllParameters_MatchHardcoded()
    {
        var hardcoded = DustCommand.Create();
        var json = JsonCompletionLoader.LoadFromFile(DustJsonPath);
        Assert.NotNull(json);

        for (int i = 0; i < hardcoded.Parameters.Length; i++)
        {
            var h = hardcoded.Parameters[i];
            var j = json.Parameters[i];

            Assert.Equal(h.CompletionText, j.CompletionText);
            Assert.Equal(h.Tooltip, j.Tooltip);
            Assert.Equal(h.Alias, j.Alias);
            Assert.Equal(h.RequiresValue, j.RequiresValue);
            Assert.Equal(h.StaticArguments.Length, j.StaticArguments.Length);

            for (int k = 0; k < h.StaticArguments.Length; k++)
            {
                Assert.Equal(h.StaticArguments[k].CompletionText, j.StaticArguments[k].CompletionText);
                Assert.Equal(h.StaticArguments[k].Tooltip, j.StaticArguments[k].Tooltip);
            }
        }
    }

    [Fact]
    public void Completions_Dust_MatchesHardcoded()
    {
        var json = JsonCompletionLoader.LoadFromFile(DustJsonPath);
        Assert.NotNull(json);

        var hardcodedCompletions = DustCommand.Create()
            .GetCompletions(default, includeDynamicArguments: false)
            .Select(c => c.CompletionText)
            .OrderBy(c => c)
            .ToList();

        var jsonCompletions = json
            .GetCompletions(default, includeDynamicArguments: false)
            .Select(c => c.CompletionText)
            .OrderBy(c => c)
            .ToList();

        Assert.Equal(hardcodedCompletions, jsonCompletions);
    }

    [Fact]
    public void Completions_DustOutputFormat_MatchesHardcoded()
    {
        var json = JsonCompletionLoader.LoadFromFile(DustJsonPath);
        Assert.NotNull(json);

        var hardcoded = DustCommand.Create();

        var hardcodedParam = hardcoded.Parameters.First(p => p.CompletionText == "--output-format");
        var jsonParam = json.Parameters.First(p => p.CompletionText == "--output-format");

        var hardcodedArgs = hardcodedParam.StaticArguments.Select(a => a.CompletionText).OrderBy(a => a).ToList();
        var jsonArgs = jsonParam.StaticArguments.Select(a => a.CompletionText).OrderBy(a => a).ToList();

        Assert.Equal(hardcodedArgs, jsonArgs);
    }

    [Fact]
    public void Completions_DustFiletime_MatchesHardcoded()
    {
        var json = JsonCompletionLoader.LoadFromFile(DustJsonPath);
        Assert.NotNull(json);

        var hardcoded = DustCommand.Create();

        var hardcodedParam = hardcoded.Parameters.First(p => p.CompletionText == "--filetime");
        var jsonParam = json.Parameters.First(p => p.CompletionText == "--filetime");

        var hardcodedArgs = hardcodedParam.StaticArguments.Select(a => a.CompletionText).OrderBy(a => a).ToList();
        var jsonArgs = jsonParam.StaticArguments.Select(a => a.CompletionText).OrderBy(a => a).ToList();

        Assert.Equal(hardcodedArgs, jsonArgs);
    }

    [Fact]
    public void LoadFromJson_WithSubCommands()
    {
        var json = """
        {
          "completionText": "test",
          "tooltip": "A test command",
          "subCommands": [
            {
              "completionText": "sub1",
              "tooltip": "First subcommand",
              "parameters": [
                { "completionText": "--verbose", "tooltip": "Verbose output" }
              ]
            }
          ],
          "parameters": [
            { "completionText": "--help", "tooltip": "Print help" }
          ]
        }
        """;

        var command = JsonCompletionLoader.LoadFromJson(json);
        Assert.NotNull(command);
        Assert.Equal("test", command.CompletionText);
        Assert.Single(command.SubCommands);
        Assert.Equal("sub1", command.SubCommands[0].CompletionText);
        Assert.Single(command.SubCommands[0].Parameters);
        Assert.Single(command.Parameters);
    }
}

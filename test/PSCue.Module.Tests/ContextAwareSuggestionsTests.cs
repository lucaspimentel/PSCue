using Xunit;
using PSCue.Module;

namespace PSCue.Module.Tests;

public class ContextAwareSuggestionsTests
{
    [Fact]
    public void GetSuggestions_AfterParameter_SuggestsOnlyValues()
    {
        var history = new CommandHistory();
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        var analyzer = new ContextAnalyzer();
        parser.RegisterParameterRequiringValue("-f");

        // Set up CommandParser in PSCueModule for GenericPredictor to use
        PSCueModule.CommandParser = parser;

        // Record some usage
        graph.RecordParsedUsage(parser.Parse("dotnet build -f net6.0"));
        graph.RecordParsedUsage(parser.Parse("dotnet build -f net7.0"));
        graph.RecordParsedUsage(parser.Parse("dotnet build -f net8.0"));

        var predictor = new GenericPredictor(history, graph, analyzer);

        // Get suggestions after typing "dotnet build -f "
        var suggestions = predictor.GetSuggestions("dotnet build -f ");

        // Should only suggest values for -f parameter
        Assert.All(suggestions, s => Assert.Equal("parameter-value", s.Source));
        Assert.Contains(suggestions, s => s.Text == "net6.0");
        Assert.Contains(suggestions, s => s.Text == "net7.0");
        Assert.Contains(suggestions, s => s.Text == "net8.0");
    }

    [Fact]
    public void GetSuggestions_AfterParameter_DoesNotSuggestFlags()
    {
        var history = new CommandHistory();
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        var analyzer = new ContextAnalyzer();
        parser.RegisterParameterRequiringValue("-f");

        PSCueModule.CommandParser = parser;

        // Record parameter values and some flags
        graph.RecordParsedUsage(parser.Parse("dotnet build -f net6.0"));
        graph.RecordParsedUsage(parser.Parse("dotnet build --verbose"));

        var predictor = new GenericPredictor(history, graph, analyzer);

        var suggestions = predictor.GetSuggestions("dotnet build -f ");

        // Should NOT suggest --verbose flag when expecting a value
        Assert.DoesNotContain(suggestions, s => s.Text == "--verbose");
        Assert.DoesNotContain(suggestions, s => s.IsFlag);
    }

    [Fact]
    public void GetSuggestions_AfterVerb_SuggestsFlags()
    {
        var history = new CommandHistory();
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        var analyzer = new ContextAnalyzer();

        PSCueModule.CommandParser = parser;

        // Record some usage
        graph.RecordUsage("dotnet", new[] { "build", "--verbose" }, null);

        var predictor = new GenericPredictor(history, graph, analyzer);

        var suggestions = predictor.GetSuggestions("dotnet build ");

        // After verb, should suggest flags/parameters
        // (falls through to legacy path since not expecting parameter value)
        Assert.NotEmpty(suggestions);
    }

    [Fact]
    public void GetSuggestions_UnknownParameter_FallsBackToLegacyBehavior()
    {
        var history = new CommandHistory();
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        var analyzer = new ContextAnalyzer();

        PSCueModule.CommandParser = parser;

        // Record some usage
        graph.RecordUsage("dotnet", new[] { "build" }, null);

        var predictor = new GenericPredictor(history, graph, analyzer);

        // Unknown parameter -x, should fall back to legacy behavior
        var suggestions = predictor.GetSuggestions("dotnet build -x ");

        // Should not crash, may return suggestions based on legacy logic
        Assert.NotNull(suggestions);
    }

    [Fact]
    public void GetSuggestions_ParameterWithNoLearnedValues_ReturnsEmpty()
    {
        var history = new CommandHistory();
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        var analyzer = new ContextAnalyzer();
        parser.RegisterParameterRequiringValue("-f");

        PSCueModule.CommandParser = parser;

        // Don't record any usage for -f

        var predictor = new GenericPredictor(history, graph, analyzer);

        var suggestions = predictor.GetSuggestions("dotnet build -f ");

        // No learned values, should return empty
        Assert.Empty(suggestions);
    }

    [Fact]
    public void GetSuggestions_ParameterWithValue_ReturnsLearnedValues()
    {
        var history = new CommandHistory();
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        var analyzer = new ContextAnalyzer();
        parser.RegisterParameterRequiringValue("-m");

        PSCueModule.CommandParser = parser;

        // Record with -m
        graph.RecordParsedUsage(parser.Parse("git commit -m \"test message\""));

        var predictor = new GenericPredictor(history, graph, analyzer);

        // Should suggest learned value
        var suggestions = predictor.GetSuggestions("git commit -m ");

        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Text == "test message");
    }
}

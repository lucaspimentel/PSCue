using Xunit;
using PSCue.Module;

namespace PSCue.Module.Tests;

public class ParameterValueBindingTests
{
    [Fact]
    public void RecordParsedUsage_TracksParameterValuePairs()
    {
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-f");

        var parsed = parser.Parse("dotnet build -f net6.0");
        graph.RecordParsedUsage(parsed);

        var values = graph.GetParameterValues("dotnet", "-f");

        Assert.Single(values);
        Assert.Equal("net6.0", values[0]);
    }

    [Fact]
    public void RecordParsedUsage_TracksSameParameterWithDifferentValues()
    {
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-f");

        graph.RecordParsedUsage(parser.Parse("dotnet build -f net6.0"));
        graph.RecordParsedUsage(parser.Parse("dotnet build -f net7.0"));
        graph.RecordParsedUsage(parser.Parse("dotnet build -f net8.0"));

        var values = graph.GetParameterValues("dotnet", "-f");

        Assert.Equal(3, values.Count);
        Assert.Contains("net6.0", values);
        Assert.Contains("net7.0", values);
        Assert.Contains("net8.0", values);
    }

    [Fact]
    public void GetParameterValues_OrdersByUsageCount()
    {
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-f");

        // Record net6.0 three times
        graph.RecordParsedUsage(parser.Parse("dotnet build -f net6.0"));
        graph.RecordParsedUsage(parser.Parse("dotnet build -f net6.0"));
        graph.RecordParsedUsage(parser.Parse("dotnet build -f net6.0"));

        // Record net8.0 once
        graph.RecordParsedUsage(parser.Parse("dotnet build -f net8.0"));

        var values = graph.GetParameterValues("dotnet", "-f", maxResults: 10);

        Assert.Equal("net6.0", values[0]); // Most frequent should be first
        Assert.Equal("net8.0", values[1]);
    }

    [Fact]
    public void RecordParsedUsage_MultipleParametersInOneCommand()
    {
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-c");
        parser.RegisterParameterRequiringValue("-f");

        graph.RecordParsedUsage(parser.Parse("dotnet build -c Release -f net6.0"));

        var configValues = graph.GetParameterValues("dotnet", "-c");
        var frameworkValues = graph.GetParameterValues("dotnet", "-f");

        Assert.Single(configValues);
        Assert.Equal("Release", configValues[0]);

        Assert.Single(frameworkValues);
        Assert.Equal("net6.0", frameworkValues[0]);
    }

    [Fact]
    public void GetParameterValues_ReturnsEmptyForUnknownCommand()
    {
        var graph = new ArgumentGraph();

        var values = graph.GetParameterValues("unknown", "-f");

        Assert.Empty(values);
    }

    [Fact]
    public void GetParameterValues_ReturnsEmptyForUnknownParameter()
    {
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-f");

        graph.RecordParsedUsage(parser.Parse("dotnet build -f net6.0"));

        var values = graph.GetParameterValues("dotnet", "-unknown");

        Assert.Empty(values);
    }

    [Fact]
    public void RecordParsedUsage_AlsoCallsOriginalRecordUsage()
    {
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");

        graph.RecordParsedUsage(parser.Parse("git commit -m message"));

        // Original RecordUsage should still track arguments
        var suggestions = graph.GetSuggestions("git", new[] { "commit" });

        Assert.Contains(suggestions, s => s.Argument == "-m");
    }

    [Fact]
    public void RecordParsedUsage_HandlesParameterEqualsValueSyntax()
    {
        var graph = new ArgumentGraph();
        var parser = new CommandParser();

        graph.RecordParsedUsage(parser.Parse("dotnet build --configuration=Release"));

        var values = graph.GetParameterValues("dotnet", "--configuration");

        Assert.Single(values);
        Assert.Equal("Release", values[0]);
    }

    [Fact]
    public void GetParameterValues_RespectsMaxResults()
    {
        var graph = new ArgumentGraph();
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-f");

        for (int i = 0; i < 10; i++)
        {
            graph.RecordParsedUsage(parser.Parse($"dotnet build -f net{i}.0"));
        }

        var values = graph.GetParameterValues("dotnet", "-f", maxResults: 5);

        Assert.Equal(5, values.Count);
    }
}

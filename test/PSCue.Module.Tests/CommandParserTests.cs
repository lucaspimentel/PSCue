using Xunit;
using PSCue.Module;

namespace PSCue.Module.Tests;

public class CommandParserTests
{
    [Fact]
    public void Parse_SimpleCommand_ReturnsCommandOnly()
    {
        var parser = new CommandParser();
        var result = parser.Parse("git");

        Assert.Equal("git", result.Command);
        Assert.Empty(result.Arguments);
    }

    [Fact]
    public void Parse_CommandWithFlag_IdentifiesFlag()
    {
        var parser = new CommandParser();
        var result = parser.Parse("git status --short");

        Assert.Equal("git", result.Command);
        Assert.Equal(2, result.Arguments.Count);
        Assert.Equal(ArgumentType.Verb, result.Arguments[0].Type);
        Assert.Equal("status", result.Arguments[0].Text);
        Assert.Equal(ArgumentType.Flag, result.Arguments[1].Type);
        Assert.Equal("--short", result.Arguments[1].Text);
    }

    [Fact]
    public void Parse_ParameterWithValue_BindsValueToParameter()
    {
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");

        var result = parser.Parse("git commit -m \"test message\"");

        Assert.Equal("git", result.Command);
        Assert.Equal(3, result.Arguments.Count);

        // "commit" is a verb
        Assert.Equal(ArgumentType.Verb, result.Arguments[0].Type);
        Assert.Equal("commit", result.Arguments[0].Text);

        // "-m" is a parameter
        Assert.Equal(ArgumentType.Parameter, result.Arguments[1].Type);
        Assert.Equal("-m", result.Arguments[1].Text);

        // "test message" is the value
        Assert.Equal(ArgumentType.ParameterValue, result.Arguments[2].Type);
        Assert.Equal("test message", result.Arguments[2].Text);
        Assert.Equal(result.Arguments[1], result.Arguments[2].BoundParameter);
    }

    [Fact]
    public void Parse_ParameterEqualsValue_BindsValueToParameter()
    {
        var parser = new CommandParser();

        var result = parser.Parse("dotnet build --configuration=Release");

        Assert.Equal("dotnet", result.Command);
        Assert.Equal(3, result.Arguments.Count);

        Assert.Equal(ArgumentType.Verb, result.Arguments[0].Type);
        Assert.Equal("build", result.Arguments[0].Text);

        Assert.Equal(ArgumentType.Parameter, result.Arguments[1].Type);
        Assert.Equal("--configuration", result.Arguments[1].Text);

        Assert.Equal(ArgumentType.ParameterValue, result.Arguments[2].Type);
        Assert.Equal("Release", result.Arguments[2].Text);
    }

    [Fact]
    public void Parse_MultipleParameters_BindsAllValues()
    {
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-b");
        parser.RegisterParameterRequiringValue("-m");

        var result = parser.Parse("git checkout -b feature -m message");

        Assert.Equal("git", result.Command);
        Assert.Equal(5, result.Arguments.Count);

        Assert.Equal("checkout", result.Arguments[0].Text);
        Assert.Equal(ArgumentType.Verb, result.Arguments[0].Type);

        Assert.Equal("-b", result.Arguments[1].Text);
        Assert.Equal(ArgumentType.Parameter, result.Arguments[1].Type);

        Assert.Equal("feature", result.Arguments[2].Text);
        Assert.Equal(ArgumentType.ParameterValue, result.Arguments[2].Type);

        Assert.Equal("-m", result.Arguments[3].Text);
        Assert.Equal(ArgumentType.Parameter, result.Arguments[3].Type);

        Assert.Equal("message", result.Arguments[4].Text);
        Assert.Equal(ArgumentType.ParameterValue, result.Arguments[4].Type);
    }

    [Fact]
    public void Parse_HeuristicDetection_TreatsUnknownFlagWithNonFlagAsParameter()
    {
        var parser = new CommandParser();

        var result = parser.Parse("dotnet build -f net6.0");

        Assert.Equal("dotnet", result.Command);
        Assert.Equal(3, result.Arguments.Count);

        Assert.Equal(ArgumentType.Verb, result.Arguments[0].Type);
        Assert.Equal(ArgumentType.Parameter, result.Arguments[1].Type); // Heuristic: -f followed by non-flag
        Assert.Equal(ArgumentType.ParameterValue, result.Arguments[2].Type);
    }

    [Fact]
    public void Parse_KnownFlag_IdentifiesAsFlag()
    {
        var parser = new CommandParser();
        parser.RegisterFlag("--verbose");

        var result = parser.Parse("git status --verbose");

        Assert.Equal(2, result.Arguments.Count);
        Assert.Equal(ArgumentType.Flag, result.Arguments[1].Type);
    }

    [Fact]
    public void GetParameterValuePairs_ReturnsAllPairs()
    {
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");
        parser.RegisterParameterRequiringValue("-b");

        var result = parser.Parse("git commit -m message -b branch");
        var pairs = result.GetParameterValuePairs().ToList();

        Assert.Equal(2, pairs.Count);
        Assert.Contains(("-m", "message"), pairs);
        Assert.Contains(("-b", "branch"), pairs);
    }

    [Fact]
    public void GetFlags_ReturnsOnlyFlags()
    {
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");

        var result = parser.Parse("git commit -a -m message --verbose");
        var flags = result.GetFlags().ToList();

        Assert.Equal(2, flags.Count);
        Assert.Contains("-a", flags);
        Assert.Contains("--verbose", flags);
        Assert.DoesNotContain("-m", flags); // Parameter, not flag
    }

    [Fact]
    public void GetVerbs_ReturnsOnlyVerbs()
    {
        var parser = new CommandParser();

        var result = parser.Parse("git remote add origin url");
        var verbs = result.GetVerbs().ToList();

        // Only "remote" is classified as a verb (first non-flag argument)
        // "add", "origin", "url" are standalone arguments
        Assert.Single(verbs);
        Assert.Contains("remote", verbs);
    }

    [Fact]
    public void DetermineExpectedType_AfterParameter_ReturnsParameterValue()
    {
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");

        var expected = parser.DetermineExpectedType("git commit -m");

        Assert.Equal(ArgumentType.ParameterValue, expected);
    }

    [Fact]
    public void DetermineExpectedType_AfterCommand_ReturnsVerb()
    {
        var parser = new CommandParser();

        var expected = parser.DetermineExpectedType("git ");

        Assert.Equal(ArgumentType.Verb, expected);
    }

    [Fact]
    public void DetermineExpectedType_AfterVerb_ReturnsFlag()
    {
        var parser = new CommandParser();

        var expected = parser.DetermineExpectedType("git commit ");

        Assert.Equal(ArgumentType.Flag, expected);
    }

    [Fact]
    public void Parse_QuotedValue_PreservesQuotedContent()
    {
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");

        var result = parser.Parse("git commit -m \"test message with spaces\"");

        Assert.Equal(3, result.Arguments.Count);
        Assert.Equal("test message with spaces", result.Arguments[2].Text);
    }

    [Fact]
    public void Parse_EscapedCharacters_HandlesEscapes()
    {
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");

        var result = parser.Parse("git commit -m \"test\\nmessage\"");

        Assert.Equal(3, result.Arguments.Count);
        // Backslash followed by 'n' is not a recognized escape - both are preserved
        Assert.Equal("test\\nmessage", result.Arguments[2].Text);
    }

    [Fact]
    public void Parse_WindowsPath_PreservesBackslashes()
    {
        var parser = new CommandParser();

        var result = parser.Parse("cd D:\\source\\datadog\\dd-trace-dotnet");

        Assert.Equal("cd", result.Command);
        Assert.Single(result.Arguments);
        // Windows path backslashes should be preserved
        Assert.Equal("D:\\source\\datadog\\dd-trace-dotnet", result.Arguments[0].Text);
    }

    [Fact]
    public void Parse_QuotedWindowsPath_PreservesBackslashes()
    {
        var parser = new CommandParser();

        var result = parser.Parse("cd \"D:\\source\\my folder\\project\"");

        Assert.Equal("cd", result.Command);
        Assert.Single(result.Arguments);
        // Windows path backslashes should be preserved even in quotes
        Assert.Equal("D:\\source\\my folder\\project", result.Arguments[0].Text);
    }

    [Fact]
    public void Parse_EscapedQuote_HandlesEscapeCorrectly()
    {
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");

        var result = parser.Parse("git commit -m \"test \\\"quoted\\\" message\"");

        Assert.Equal(3, result.Arguments.Count);
        // Escaped quotes should be preserved
        Assert.Equal("test \"quoted\" message", result.Arguments[2].Text);
    }

    [Fact]
    public void Parse_EscapedBackslash_HandlesDoubleBackslash()
    {
        var parser = new CommandParser();
        parser.RegisterParameterRequiringValue("-m");

        var result = parser.Parse("git commit -m \"test \\\\ message\"");

        Assert.Equal(3, result.Arguments.Count);
        // Escaped backslash (\\) should become single backslash
        Assert.Equal("test \\ message", result.Arguments[2].Text);
    }
}

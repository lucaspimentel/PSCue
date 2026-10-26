using PSCue.Shared.Completions;

namespace PSCue.ArgumentCompleter.Tests;

public class DynamicArgumentsMemoTests
{
    private static ICompletionWithChildren CreateNode(string nodeKind, DynamicArgumentsFactory factory) =>
        nodeKind == "command"
            ? new Command("root") { DynamicArguments = factory }
            : new CommandParameter("--flag") { DynamicArguments = factory };

    [Theory]
    [InlineData("command")]
    [InlineData("parameter")]
    public void FactoryEvaluatedOnceAcrossFindNodeAndGetCompletions(string nodeKind)
    {
        var calls = 0;
        IEnumerable<DynamicArgument> Factory()
        {
            calls++;
            yield return new DynamicArgument("alpha");
            yield return new DynamicArgument("beta");
        }

        var node = CreateNode(nodeKind, Factory);

        Assert.Null(node.FindNode("no-such-node"));
        Assert.Equal(1, calls);

        var completions = node.GetCompletions("a", includeDynamicArguments: true);
        var item = Assert.Single(completions);
        Assert.Equal("alpha", item.CompletionText);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("command")]
    [InlineData("parameter")]
    public void MaterializedResultsPreserveFactoryOrder(string nodeKind)
    {
        var calls = 0;
        IEnumerable<DynamicArgument> Factory()
        {
            calls++;
            yield return new DynamicArgument("beta");
            yield return new DynamicArgument("alpha");
        }

        var node = CreateNode(nodeKind, Factory);

        Assert.Null(node.FindNode("no-such-node"));
        var completions = node.GetCompletions("", includeDynamicArguments: true);

        Assert.Equal(new[] { "beta", "alpha" }, completions.Select(c => c.CompletionText));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("command")]
    [InlineData("parameter")]
    public void FactoryNotEvaluatedWhenDynamicArgumentsNotRequested(string nodeKind)
    {
        var calls = 0;
        IEnumerable<DynamicArgument> Factory()
        {
            calls++;
            yield return new DynamicArgument("alpha");
        }

        var node = CreateNode(nodeKind, Factory);
        node.GetCompletions("a", includeDynamicArguments: false);

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("command")]
    [InlineData("parameter")]
    public void FactoryNotEvaluatedWhenStaticMatchShortCircuitsFindNode(string nodeKind)
    {
        var calls = 0;
        IEnumerable<DynamicArgument> Factory()
        {
            calls++;
            yield return new DynamicArgument("alpha");
        }

        var node = CreateNode(nodeKind, Factory);
        var search = nodeKind == "command" ? "root" : "--flag";

        Assert.Same(node, node.FindNode(search));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("command")]
    [InlineData("parameter")]
    public void EachNewNodeEvaluatesItsOwnFactory(string nodeKind)
    {
        var calls = 0;
        IEnumerable<DynamicArgument> Factory()
        {
            calls++;
            yield return new DynamicArgument("alpha");
        }

        var first = CreateNode(nodeKind, Factory);
        Assert.Null(first.FindNode("no-such-node"));

        var second = CreateNode(nodeKind, Factory);
        Assert.Null(second.FindNode("no-such-node"));

        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("command")]
    [InlineData("parameter")]
    public void NullFactoryBehavesAsBefore(string nodeKind)
    {
        ICompletionWithChildren node = nodeKind == "command"
            ? new Command("root")
            : new CommandParameter("--flag");

        Assert.Null(node.FindNode("no-such-node"));
        Assert.Empty(node.GetCompletions("a", includeDynamicArguments: true));
    }
}

using System.Collections.Concurrent;
using PSCue.Shared.Completions;
using PSCue.Shared.KnownCompletions;

namespace PSCue.ArgumentCompleter.Tests;

// The git command list is assembled from three independent category queries that run at the same
// time. These tests drive that composition through the fetch seam so the queries never touch a
// real git installation: what matters here is the merge order, the trimming, and the degradation
// path, none of which depend on which git version is installed.
public class GitCommandTests
{
    private const string MainCategory = "--list-cmds=main,nohelpers";
    private const string AliasCategory = "--list-cmds=alias";
    private const string ExtensionCategory = "--list-cmds=others";

    private static readonly string[] Categories = [MainCategory, AliasCategory, ExtensionCategory];

    // The three queries run at the same time, so their call order is not deterministic. Any
    // assertion about which queries ran compares sorted query names.
    private static string[] SortedCategories => [.. Categories.OrderBy(category => category, StringComparer.Ordinal)];

    private static IReadOnlyList<string> Lines(string argument) => argument switch
    {
        MainCategory => ["main-zebra", "main-alpha"],
        AliasCategory => ["alias-zulu"],
        _ => ["extension-aaa"],
    };

    private static ICompletionWithChildren CreateNode(DynamicArgumentsFactory factory, string nodeKind) =>
        nodeKind == "command"
            ? new Command("root") { DynamicArguments = factory }
            : new CommandParameter("--flag") { DynamicArguments = factory };

    [Fact]
    public void QueriesEachCategoryOnce()
    {
        var queries = new ConcurrentBag<string>();

        IReadOnlyList<string> Fetch(string argument)
        {
            queries.Add(argument);
            return Lines(argument);
        }

        var arguments = GitCommand.GetAllGitCommands(Fetch).ToArray();

        Assert.Equal(4, arguments.Length);
        Assert.Equal(SortedCategories, queries.OrderBy(query => query, StringComparer.Ordinal));
    }

    [Fact]
    public void MergesCategoriesInAFixedOrderWithTheirTooltips()
    {
        var arguments = GitCommand.GetAllGitCommands(Lines).ToArray();

        // The extensions line sorts before every other line, so a merge that relied on sorting
        // or on completion order could not produce this sequence.
        Assert.Equal(
            ["main-zebra", "main-alpha", "alias-zulu", "extension-aaa"],
            arguments.Select(argument => argument.CompletionText));
        Assert.Equal(
            ["Git command", "Git command", "Git alias", "Git extension"],
            arguments.Select(argument => argument.Tooltip));
    }

    [Fact]
    public void TrimsLinesAndSkipsBlankOnes()
    {
        IReadOnlyList<string> Fetch(string argument) => argument switch
        {
            MainCategory => ["  spaced  ", "", "   ", "\t", "padded\t"],
            AliasCategory => [],
            _ => ["ext"],
        };

        var arguments = GitCommand.GetAllGitCommands(Fetch).ToArray();

        Assert.Equal(["spaced", "padded", "ext"], arguments.Select(argument => argument.CompletionText));
    }

    [Fact]
    public void KeepsRepeatedNames()
    {
        IReadOnlyList<string> Fetch(string argument) => argument switch
        {
            MainCategory => ["repeated", "repeated"],
            AliasCategory => ["repeated"],
            _ => [],
        };

        var arguments = GitCommand.GetAllGitCommands(Fetch).ToArray();

        Assert.Equal(["repeated", "repeated", "repeated"], arguments.Select(argument => argument.CompletionText));
    }

    [Fact]
    public void EveryCategoryEmptyYieldsNothing()
    {
        static IReadOnlyList<string> Fetch(string argument) => [];

        Assert.Empty(GitCommand.GetAllGitCommands(Fetch));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryCategoryStartsBeforeAnyCategoryIsReleased(bool releaseInReverseOrder)
    {
        var fetcher = new GatedFetcher();
        string[]? merged = null;
        Exception? failure = null;

        var consumer = new Thread(() =>
        {
            try
            {
                merged = GitCommand.GetAllGitCommands(fetcher.Fetch)
                    .Select(argument => $"{argument.CompletionText}|{argument.Tooltip}")
                    .ToArray();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        consumer.Start();

        try
        {
            Assert.True(
                fetcher.AllCategoriesStarted.Wait(TimeSpan.FromSeconds(10)),
                "the three category queries did not run at the same time");

            var releaseOrder = releaseInReverseOrder ? Categories.Reverse() : Categories;
            foreach (var category in releaseOrder)
            {
                fetcher.Release(category);
            }

            Assert.True(consumer.Join(TimeSpan.FromSeconds(30)), "the merge did not finish after every category was released");
        }
        finally
        {
            fetcher.ReleaseAll();
            consumer.Join(TimeSpan.FromSeconds(30));
        }

        Assert.Null(failure);
        Assert.NotNull(merged);
        Assert.Equal(
            ["main-zebra|Git command", "main-alpha|Git command", "alias-zulu|Git alias", "extension-aaa|Git extension"],
            merged);
    }

    [Theory]
    [InlineData(MainCategory)]
    [InlineData(AliasCategory)]
    [InlineData(ExtensionCategory)]
    public void EmptyCategoryLeavesTheOthersInPlace(string emptyCategory)
    {
        IReadOnlyList<string> Fetch(string argument) => argument == emptyCategory ? [] : Lines(argument);

        var arguments = GitCommand.GetAllGitCommands(Fetch).ToArray();

        Assert.Equal(ExpectedArguments(emptyCategory, []), arguments.Select(argument => argument.CompletionText));
    }

    [Theory]
    [InlineData(MainCategory)]
    [InlineData(AliasCategory)]
    [InlineData(ExtensionCategory)]
    public void PartialCategoryOutputIsKeptInPlace(string partialCategory)
    {
        string[] partial = ["only-line"];

        IReadOnlyList<string> Fetch(string argument) => argument == partialCategory ? partial : Lines(argument);

        var arguments = GitCommand.GetAllGitCommands(Fetch).ToArray();

        Assert.Equal(ExpectedArguments(partialCategory, partial), arguments.Select(argument => argument.CompletionText));
    }

    [Theory]
    [InlineData("command")]
    [InlineData("parameter")]
    public void CategoriesAreFetchedOncePerNodeAcrossConsumers(string nodeKind)
    {
        var fetches = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

        IReadOnlyList<string> Fetch(string argument)
        {
            fetches.AddOrUpdate(argument, 1, (_, count) => count + 1);
            return Lines(argument);
        }

        DynamicArgumentsFactory factory = () => GitCommand.GetAllGitCommands(Fetch);

        var node = CreateNode(factory, nodeKind);

        Assert.Null(node.FindNode("no-such-node"));
        _ = node.GetCompletions("", includeDynamicArguments: true).ToList();
        _ = node.GetCompletions("", includeDynamicArguments: true).ToList();

        Assert.Equal(SortedCategories, fetches.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.All(fetches.Values, count => Assert.Equal(1, count));

        var otherNode = CreateNode(factory, nodeKind);
        _ = otherNode.GetCompletions("", includeDynamicArguments: true).ToList();

        Assert.All(fetches.Values, count => Assert.Equal(2, count));
    }

    // The healthy categories always contribute exactly one line, so the expected sequence is the
    // category order with the degraded category's own output substituted in its position.
    private static string[] ExpectedArguments(string degradedCategory, IReadOnlyList<string> degradedLines)
    {
        var expected = new List<string>();

        foreach (var category in Categories)
        {
            if (category == degradedCategory)
            {
                expected.AddRange(degradedLines);
            }
            else
            {
                expected.AddRange(Lines(category));
            }
        }

        return [.. expected];
    }

    // Holds each category query inside the fetch until the test releases it, so overlap can be
    // observed instead of inferred from timing.
    private sealed class GatedFetcher
    {
        private readonly object _gate = new();
        private readonly HashSet<string> _started = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ManualResetEventSlim> _releases = new(StringComparer.Ordinal)
        {
            [MainCategory] = new(false),
            [AliasCategory] = new(false),
            [ExtensionCategory] = new(false),
        };

        public ManualResetEventSlim AllCategoriesStarted { get; } = new(false);

        public IReadOnlyList<string> Fetch(string argument)
        {
            lock (_gate)
            {
                if (_started.Add(argument) && _started.Count == Categories.Length)
                {
                    AllCategoriesStarted.Set();
                }
            }

            if (!_releases[argument].Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException($"{argument} was never released");
            }

            return Lines(argument);
        }

        public void Release(string category) => _releases[category].Set();

        public void ReleaseAll()
        {
            foreach (var release in _releases.Values)
            {
                release.Set();
            }
        }
    }
}

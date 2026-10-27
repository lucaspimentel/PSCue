using PSCue.Shared.Completions;
using PSCue.Shared.KnownCompletions;

namespace PSCue.ArgumentCompleter.Tests;

// Scoop completions read the apps folders instead of launching `scoop list`, because `scoop` is a
// PowerShell script shim and one launch costs more than the whole spawn-free budget of a Tab
// press. These tests cover the folder enumeration, the root resolution, and the tree wiring
// through the factory seam, so they never depend on a Scoop installation being present.
public class ScoopCommandTests : IDisposable
{
    private const string LocalRootVariable = "SCOOP";
    private const string GlobalRootVariable = "SCOOP_GLOBAL";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pscue-scoop-" + Guid.NewGuid().ToString("N"));

    private string LocalRoot => Path.Combine(_directory, "local");

    private string GlobalRoot => Path.Combine(_directory, "global");

    public ScoopCommandTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (!Directory.Exists(_directory))
        {
            return;
        }

        // macOS keeps metadata files inside directories, so a plain delete can fail there.
        foreach (var file in Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_directory, recursive: true);
    }

    private void AddPackages(string root, params string[] names)
    {
        var appsDirectory = Path.Combine(root, "apps");
        Directory.CreateDirectory(appsDirectory);

        foreach (var name in names)
        {
            Directory.CreateDirectory(Path.Combine(appsDirectory, name));
        }
    }

    [Theory]
    [InlineData("D:\\scoop")]
    [InlineData("relative")]
    public void RootVariableWinsWhenSet(string value) =>
        Assert.Equal(value, ScoopCommand.ResolveRoot(value, @"C:\base"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RootFallsBackToTheDefaultFolderWhenTheVariableIsBlank(string? value) =>
        Assert.Equal(Path.Combine(@"C:\base", "scoop"), ScoopCommand.ResolveRoot(value, @"C:\base"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RootIsUnknownWithoutAVariableOrABaseDirectory(string? baseDirectory) =>
        Assert.Null(ScoopCommand.ResolveRoot(null, baseDirectory));

    [Fact]
    public void LocalRootComesFromScoopOrTheUserProfile() =>
        AssertRoot(LocalRootVariable, Environment.SpecialFolder.UserProfile, ScoopCommand.GetScoopRoot());

    [Fact]
    public void GlobalRootComesFromScoopGlobalOrProgramData() =>
        AssertRoot(GlobalRootVariable, Environment.SpecialFolder.CommonApplicationData, ScoopCommand.GetScoopGlobalRoot());

    private static void AssertRoot(string variable, Environment.SpecialFolder baseFolder, string? actual)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        var expected = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(baseFolder), "scoop")
            : configured;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PackagesComeFromBothRootsInOneSortedList()
    {
        AddPackages(LocalRoot, "zebra", "Alpha");
        AddPackages(GlobalRoot, "middle");

        var packages = Names(LocalRoot, GlobalRoot);

        Assert.Equal(["Alpha", "middle", "zebra"], packages);
    }

    [Fact]
    public void PackagesAreDeduplicatedAcrossRootsIgnoringCase()
    {
        AddPackages(LocalRoot, "git");
        AddPackages(GlobalRoot, "GIT", "sevenzip");

        var packages = Names(LocalRoot, GlobalRoot);

        Assert.Equal(["git", "sevenzip"], packages);
    }

    [Fact]
    public void PackagesCarryNoTooltip()
    {
        AddPackages(LocalRoot, "git");

        var arguments = ScoopCommand.GetInstalledPackages(LocalRoot, GlobalRoot).ToArray();

        var argument = Assert.Single(arguments);
        Assert.Equal("git", argument.CompletionText);
        Assert.Null(argument.Tooltip);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingRootContributesNothing(string? root)
    {
        AddPackages(LocalRoot, "git");

        Assert.Equal(["git"], Names(LocalRoot, root));
    }

    [Fact]
    public void RootWithoutAnAppsFolderContributesNothing()
    {
        AddPackages(LocalRoot, "git");
        Directory.CreateDirectory(GlobalRoot);

        Assert.Equal(["git"], Names(LocalRoot, GlobalRoot));
    }

    [Fact]
    public void NoRootsYieldNothing()
    {
        Assert.Empty(Names(Path.Combine(_directory, "absent"), GlobalRoot));
    }

    [Fact]
    public void UnreadableAppsFolderIsSkippedWhenTheOtherRootWorks()
    {
        AddPackages(LocalRoot, "git");

        // A file where the apps folder should be makes enumeration fail in a way the production
        // code has to survive: completion returns the other root's packages instead of throwing.
        Directory.CreateDirectory(GlobalRoot);
        File.WriteAllText(Path.Combine(GlobalRoot, "apps"), string.Empty);

        Assert.Equal(["git"], Names(LocalRoot, GlobalRoot));
    }

    [Fact]
    public void RootNodeListsSubcommandsWithoutPackages()
    {
        var command = ScoopCommand.Create(FakePackages);

        var completions = command.GetCompletions(string.Empty, includeDynamicArguments: true);
        var texts = completions.Select(completion => completion.CompletionText).ToArray();

        Assert.Contains("update", texts);
        Assert.Contains("install", texts);
        Assert.DoesNotContain("fake-alpha", texts);
    }

    [Fact]
    public void UpdateSubcommandListsPackages()
    {
        var command = ScoopCommand.Create(FakePackages);

        var node = Assert.IsType<Command>(command.FindNode("update"));
        var completions = node.GetCompletions(string.Empty, includeDynamicArguments: true);
        var texts = completions.Select(completion => completion.CompletionText).ToArray();

        // The parameter comes first because it is a declared parameter, then the packages.
        Assert.Equal(["*", "fake-alpha", "fake-zeta"], texts);
    }

    [Fact]
    public void PackageLookupRunsOncePerRequestEvenWhenTheNodeIsVisitedTwice()
    {
        var calls = 0;

        IEnumerable<DynamicArgument> CountingPackages()
        {
            calls++;
            return FakePackages();
        }

        var command = ScoopCommand.Create(CountingPackages);
        var node = Assert.IsType<Command>(command.FindNode("update"));

        // The full completion path for `scoop update ` looks the node up once to understand the
        // command line and once to produce suggestions, exactly like the git case.
        Assert.NotNull(node.FindNode("fake-alpha"));
        node.GetCompletions(string.Empty, includeDynamicArguments: true);
        node.GetCompletions(string.Empty, includeDynamicArguments: true);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void PackagesAreNotListedWhenDynamicArgumentsAreExcluded()
    {
        var command = ScoopCommand.Create(FakePackages);

        var node = Assert.IsType<Command>(command.FindNode("update"));
        var completions = node.GetCompletions(string.Empty, includeDynamicArguments: false);

        Assert.Equal(["*"], completions.Select(completion => completion.CompletionText));
    }

    private static IEnumerable<DynamicArgument> FakePackages()
    {
        yield return new DynamicArgument("fake-alpha");
        yield return new DynamicArgument("fake-zeta");
    }

    private static string[] Names(string? root, string? globalRoot) =>
        ScoopCommand.GetInstalledPackages(root, globalRoot)
            .Select(argument => argument.CompletionText)
            .ToArray();
}
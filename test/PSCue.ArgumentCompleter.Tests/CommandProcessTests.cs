using System.Runtime.InteropServices;
using PSCue.Shared;
using PSCue.Shared.KnownCompletions;

namespace PSCue.ArgumentCompleter.Tests;

public class CommandProcessTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pscue-command-process-" + Guid.NewGuid().ToString("N"));

    public CommandProcessTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    // Windows matches extension case either way; a case-sensitive host used to simulate it does
    // not, so the tests keep PATHEXT and the file names they create in agreement.
    private static string Ext(string extension) => IsWindows ? extension.ToUpperInvariant() : extension.ToLowerInvariant();

    private static string PathExt(params string[] extensions) => string.Join(';', extensions.Select(Ext));

    private string CreateFile(string relativePath, string contents, bool executable = false)
    {
        var path = Path.Combine(_directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);

        if (executable && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }

    [Fact]
    public void PathOrderDecidesBeforeExtensionPreference()
    {
        var shimDirectory = Path.Combine(_directory, "shim");
        var realDirectory = Path.Combine(_directory, "real");
        CreateFile($"shim/git{Ext(".cmd")}", "@echo off");
        CreateFile($"real/git{Ext(".exe")}", "binary");

        var resolved = CommandProcess.ResolveExecutable(
            "git",
            string.Join(Path.PathSeparator, shimDirectory, realDirectory),
            PathExt(".com", ".exe", ".bat", ".cmd"),
            isWindows: true);

        Assert.Equal(Path.Combine(shimDirectory, $"git{Ext(".cmd")}"), resolved);
    }

    [Fact]
    public void PathExtensionsAreTriedInOrderWithinADirectory()
    {
        var directory = Path.Combine(_directory, "shim");
        CreateFile($"shim/git{Ext(".cmd")}", "@echo off");
        CreateFile($"shim/git{Ext(".exe")}", "binary");

        var resolved = CommandProcess.ResolveExecutable(
            "git",
            directory,
            PathExt(".com", ".exe", ".bat", ".cmd"),
            isWindows: true);

        Assert.Equal(Path.Combine(directory, $"git{Ext(".exe")}"), resolved);
    }

    [Theory]
    [InlineData(".foo", ".exe", ".foo")]
    [InlineData(".exe", ".foo", ".exe")]
    public void CustomPathExtensionsAreHonored(string first, string second, string expected)
    {
        var directory = Path.Combine(_directory, "shim");
        CreateFile($"shim/git{Ext(first)}", "binary");
        CreateFile($"shim/git{Ext(second)}", "binary");

        var resolved = CommandProcess.ResolveExecutable("git", directory, PathExt(first, second), isWindows: true);

        Assert.Equal(Path.Combine(directory, $"git{Ext(expected)}"), resolved);
    }

    [Fact]
    public void MissingPathExtensionsFallBackToTheWindowsDefault()
    {
        var directory = Path.Combine(_directory, "shim");
        CreateFile("shim/git.CMD", "@echo off");

        var resolved = CommandProcess.ResolveExecutable("git", directory, pathExtensions: null, isWindows: true);

        Assert.Equal(Path.Combine(directory, "git.CMD"), resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankExecutableIsNotResolved(string executable)
    {
        Assert.Null(CommandProcess.ResolveExecutable(executable, _directory, null, isWindows: true));
    }

    [Fact]
    public void NullPathIsNotResolved()
    {
        Assert.Null(CommandProcess.ResolveExecutable("git", path: null, pathExtensions: null, isWindows: true));
    }

    [Fact]
    public void MissingExecutableIsNotResolved()
    {
        Assert.Null(CommandProcess.ResolveExecutable("git", _directory, null, isWindows: true));
    }

    [Fact]
    public void UnusablePathEntriesAreSkipped()
    {
        var realDirectory = Path.Combine(_directory, "real");
        CreateFile($"real/git{Ext(".exe")}", "binary");
        var path = string.Join(
            Path.PathSeparator,
            Path.Combine(_directory, "does-not-exist"),
            "  ",
            $"\"{realDirectory}\"");

        var resolved = CommandProcess.ResolveExecutable("git", path, PathExt(".exe"), isWindows: true);

        Assert.Equal(Path.Combine(realDirectory, $"git{Ext(".exe")}"), resolved);
    }

    [Fact]
    public void PathEntriesAndFileNamesMayContainSpaces()
    {
        var directory = Path.Combine(_directory, "shim with spaces");
        CreateFile(Path.Combine("shim with spaces", $"git{Ext(".exe")}"), "binary");

        var resolved = CommandProcess.ResolveExecutable("git", directory, PathExt(".exe"), isWindows: true);

        Assert.Equal(Path.Combine(directory, $"git{Ext(".exe")}"), resolved);
    }

    [Fact]
    public void ExplicitRelativePathIsResolvedDirectly()
    {
        var path = CreateFile(Path.Combine("explicit", "git.exe"), "binary");

        var resolved = CommandProcess.ResolveExecutable(path, path: null, pathExtensions: null, isWindows: true);

        Assert.Equal(Path.GetFullPath(path), resolved);
    }

    [Fact]
    public void ExplicitMissingPathIsNotResolved()
    {
        var path = Path.Combine(_directory, "explicit", "git.exe");

        Assert.Null(CommandProcess.ResolveExecutable(path, path: null, pathExtensions: null, isWindows: true));
    }

    [Fact]
    public void DirectoryNamedLikeTheExecutableIsNotResolved()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "git"));

        Assert.Null(CommandProcess.ResolveExecutable("git", _directory, PathExt(".exe"), isWindows: true));
    }

    [SkippableFact]
    public void NonExecutableFilesAreSkippedOnUnix()
    {
        Skip.If(IsWindows, "Unix execute permissions do not exist on Windows");

        var directory = Path.Combine(_directory, "bin");
        CreateFile("bin/git", "#!/bin/sh\necho hi\n");
        var executablePath = CreateFile("bin/other", "#!/bin/sh\necho hi\n", executable: true);

        Assert.Null(CommandProcess.ResolveExecutable("git", directory, null, isWindows: false));
        Assert.Equal(Path.GetFullPath(executablePath), CommandProcess.ResolveExecutable("other", directory, null, isWindows: false));
    }

    [SkippableFact]
    public void ExecutableScriptsAreResolvedOnUnix()
    {
        Skip.If(IsWindows, "Unix execute permissions do not exist on Windows");

        var directory = Path.Combine(_directory, "bin");
        var scriptPath = CreateFile("bin/git", "#!/bin/sh\necho hi\n", executable: true);

        var resolved = CommandProcess.ResolveExecutable("git", directory, null, isWindows: false);

        Assert.Equal(Path.GetFullPath(scriptPath), resolved);
    }

    [Theory]
    [InlineData("branch")]
    [InlineData("--list-cmds=main,nohelpers")]
    [InlineData("--list-cmds=alias")]
    [InlineData("remote")]
    [InlineData("tag")]
    public void PlainArgumentsAreNotQuotedForTheInterpreter(string argument)
    {
        var commandLine = CommandProcess.BuildInterpreterCommandLine(@"C:\shim\git.cmd", [argument]);

        Assert.Equal($"/d /v:off /s /c \"C:\\shim\\git.cmd {argument}\"", commandLine);
    }

    [Theory]
    [InlineData("--format=%(refname:short)", @"""--format=%(refname:short)""")]
    [InlineData("C:\\shim with spaces\\git.cmd", @"""C:\shim with spaces\git.cmd""")]
    [InlineData("a & b", @"""a & b""")]
    public void ArgumentsCarryingInterpreterSyntaxAreQuoted(string value, string expected)
    {
        Assert.Contains(expected, CommandProcess.BuildInterpreterCommandLine(value, []), StringComparison.Ordinal);
    }

    // Execution tests use a generous timeout so a loaded machine cannot starve the child, and
    // the hang test uses a long one so the child has ample time to flush before being killed.
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan HangTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void BranchFormatArgumentReachesTheChildWithoutShellQuotes()
    {
        var (executable, arguments, expected) = Transcript("alpha\nbeta\n");

        var lines = CommandProcess.Run(executable, TestTimeout, arguments);

        Assert.Equal(expected, lines);
    }

    [Fact]
    public void MissingExecutableReturnsNoLines()
    {
        Assert.Empty(CommandProcess.Run("pscue-does-not-exist-" + Guid.NewGuid().ToString("N"), TestTimeout));
    }

    [Fact]
    public void BlankLinesAndWhitespaceArePreserved()
    {
        var (executable, arguments, expected) = Transcript("alpha\n\n  beta  \ngamma");

        var lines = CommandProcess.Run(executable, TestTimeout, arguments);

        Assert.Equal(expected, lines);
    }

    [Fact]
    public void StandardErrorIsNotPartOfTheOutput()
    {
        var (executable, arguments, expected) = Transcript("out\n", "err\n");

        var lines = CommandProcess.Run(executable, TestTimeout, arguments);

        Assert.Equal(expected, lines);
    }

    [Fact]
    public void OutputBeforeANonZeroExitIsKept()
    {
        var (executable, arguments, expected) = Transcript("first\n", exitCode: 3);

        var lines = CommandProcess.Run(executable, TestTimeout, arguments);

        Assert.Equal(expected, lines);
    }

    [Fact]
    public void OutputFlushedBeforeAHangIsKept()
    {
        var (executable, arguments, expected) = Transcript("first\n", hang: true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var lines = CommandProcess.Run(executable, HangTimeout, arguments);

        stopwatch.Stop();
        Assert.Equal(expected, lines);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"the child was not killed promptly ({stopwatch.Elapsed})");
    }

    [SkippableFact]
    public void ExecutableImagesRunDirectlyWithArguments()
    {
        Skip.IfNot(IsWindows, "uses cmd.exe as a real image");

        var lines = CommandProcess.Run(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            TestTimeout,
            ["/d", "/c", "echo direct-image"]);

        Assert.Equal(["direct-image"], lines);
    }

    [SkippableFact]
    public void CommandScriptsRunThroughTheInterpreter()
    {
        Skip.IfNot(IsWindows, "command scripts are Windows-only");

        var scriptPath = CreateFile(Path.Combine("shim with spaces", "git.cmd"), "@echo off\r\necho shim-line\r\n");
        var lines = CommandProcess.Run(scriptPath, TestTimeout);

        Assert.Equal(["shim-line"], lines);
    }

    [SkippableFact]
    public void ExecutableScriptsRunDirectly()
    {
        Skip.If(IsWindows, "Unix execute permissions do not exist on Windows");

        var scriptPath = CreateFile("bin/git", "#!/bin/sh\necho one\necho two\n", executable: true);
        var lines = CommandProcess.Run(scriptPath, TestTimeout);

        Assert.Equal(["one", "two"], lines);
    }

    // The git command list composes three category queries, and two of them run on their own
    // thread. These tests drive that composition with one category backed by a real child process
    // and the other two backed by in-memory lines, so the failure path is covered without
    // depending on a git installation and without weakening the other categories to fakes.
    private const string GitMainCategory = "--list-cmds=main,nohelpers";

    private const string GitAliasCategory = "--list-cmds=alias";

    private const string GitExtensionCategory = "--list-cmds=others";

    private static readonly string[] GitCategories = [GitMainCategory, GitAliasCategory, GitExtensionCategory];

    [Theory]
    [InlineData(GitMainCategory)]
    [InlineData(GitAliasCategory)]
    [InlineData(GitExtensionCategory)]
    public void MissingChildLeavesTheOtherGitCategoriesIntact(string unhealthyCategory)
    {
        var fetch = GitFetchWith(
            () => CommandProcess.Run("pscue-does-not-exist-" + Guid.NewGuid().ToString("N"), TestTimeout),
            unhealthyCategory);

        var arguments = GitCommand.GetAllGitCommands(fetch).Select(argument => argument.CompletionText);

        Assert.Equal(ExpectedGitArguments(unhealthyCategory, []), arguments);
    }

    [Theory]
    [InlineData(GitMainCategory)]
    [InlineData(GitAliasCategory)]
    [InlineData(GitExtensionCategory)]
    public void GitCategoryWithANonZeroExitKeepsItsPartialOutput(string unhealthyCategory)
    {
        var (executable, payloadArguments, expected) = Transcript("partial-a\npartial-b\n", exitCode: 3);
        var fetch = GitFetchWith(
            () => CommandProcess.Run(executable, TestTimeout, payloadArguments),
            unhealthyCategory);

        var arguments = GitCommand.GetAllGitCommands(fetch).Select(argument => argument.CompletionText);

        Assert.Equal(ExpectedGitArguments(unhealthyCategory, expected), arguments);
    }

    [Theory]
    [InlineData(GitMainCategory)]
    [InlineData(GitAliasCategory)]
    [InlineData(GitExtensionCategory)]
    public void GitCategoryThatHangsKeepsItsFlushedOutput(string unhealthyCategory)
    {
        var (executable, payloadArguments, expected) = Transcript("flushed\n", hang: true);
        var fetch = GitFetchWith(
            () => CommandProcess.Run(executable, HangTimeout, payloadArguments),
            unhealthyCategory);

        var arguments = GitCommand.GetAllGitCommands(fetch).Select(argument => argument.CompletionText);

        Assert.Equal(ExpectedGitArguments(unhealthyCategory, expected), arguments);
    }

    // Routes one category through a real child process and answers the other two from memory.
    private static Func<string, IReadOnlyList<string>> GitFetchWith(
        Func<IReadOnlyList<string>> unhealthyFetch,
        string unhealthyCategory) =>
        argument => argument == unhealthyCategory
            ? unhealthyFetch()
            : argument switch
            {
                GitMainCategory => ["main-a"],
                GitAliasCategory => ["alias-a"],
                _ => ["ext-a"],
            };

    // The healthy categories contribute one line each, so the expected sequence is the fixed
    // category order with the unhealthy category's own lines substituted in its position.
    private static string[] ExpectedGitArguments(string unhealthyCategory, IReadOnlyList<string> unhealthyLines)
    {
        var expected = new List<string>();

        foreach (var category in GitCategories)
        {
            if (category == unhealthyCategory)
            {
                expected.AddRange(unhealthyLines);
            }
            else
            {
                expected.Add(category switch
                {
                    GitMainCategory => "main-a",
                    GitAliasCategory => "alias-a",
                    _ => "ext-a",
                });
            }
        }

        return [.. expected];
    }

    // Splits a payload the way ReadLine does: line terminators are dropped, blank lines and
    // surrounding whitespace survive, and a trailing newline does not add an extra line.
    private static string[] LinesOf(string text)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();

        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines.ToArray();
    }

    // Builds a payload that lets one process write a known transcript, optionally to stderr,
    // optionally exiting non-zero, and optionally hanging after flushing.
    private (string Executable, string[] Arguments, string[] Expected) Transcript(
        string stdout,
        string? stderr = null,
        int exitCode = 0,
        bool hang = false)
    {
        var expected = LinesOf(stdout);

        if (IsWindows)
        {
            // A command script is the shape the interpreter path takes in production, so covering
            // it here exercises the same cmd.exe invocation the completer uses for a .cmd shim.
            var builder = new System.Text.StringBuilder("@echo off\r\n");

            foreach (var line in expected)
            {
                builder.Append(line.Length == 0 ? "echo.\r\n" : $"echo {line}\r\n");
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                builder.Append($"echo {stderr.Trim()} 1>&2\r\n");
            }

            if (hang)
            {
                builder.Append("ping -n 21 127.0.0.1 >nul\r\n");
            }

            builder.Append($"exit /b {exitCode}\r\n");

            var payloadPath = CreateFile(Path.Combine("payload", $"payload-{Guid.NewGuid():N}.cmd"), builder.ToString());

            return (payloadPath, [], expected);
        }

        var script = new System.Text.StringBuilder($"printf '{stdout.Replace("\n", "\\n")}'");

        if (stderr is not null)
        {
            script.Append($"; printf '{stderr.Replace("\n", "\\n")}' >&2");
        }

        if (hang)
        {
            script.Append("; sleep 20");
        }

        script.Append($"; exit {exitCode}");

        return ("/bin/sh", ["-c", script.ToString()], expected);
    }
}
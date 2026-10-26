using System.Diagnostics;
using System.Text;

namespace PSCue.Shared;

/// <summary>
/// Runs a child process without going through PowerShell. Routing every lookup through a
/// fresh <c>pwsh</c> costs 300 to 450ms of interpreter startup on Windows before the command
/// it wraps even starts, which is what made git Tab completion take seconds.
/// </summary>
internal static class CommandProcess
{
    // Generous enough that a real git can finish on a loaded machine. Expiry costs the
    // completions for that lookup instead of hanging the Tab press.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // Bounds cleanup of the output readers after the child has exited or been killed. A child
    // that exited on its own gets a real window, because the readers may not be scheduled for a
    // while on a loaded machine and a short window reports an empty result for output that
    // arrived. A killed child gets the short window, since a descendant can hold the pipe open
    // indefinitely.
    private const int DrainAfterExitMilliseconds = 5000;

    private const int ReaderShutdownMilliseconds = 500;

    private static readonly string[] WindowsPathExtensions = [".COM", ".EXE", ".BAT", ".CMD"];

    // cmd.exe treats these as syntax, so arguments containing them are wrapped in quotes.
    private const string CommandInterpreterSyntaxCharacters = "&()^<>|\"%";

    public static IReadOnlyList<string> Run(string executable, params string[] arguments) =>
        Run(executable, Timeout, arguments);

    /// <summary>
    /// Runs <paramref name="executable"/> with <paramref name="arguments"/> and returns the
    /// stdout lines it produced, giving up after <paramref name="timeout"/>. A missing
    /// executable, a launch failure, or a child that outlives the timeout yields an empty or
    /// partial list instead of throwing; output written before a non-zero exit is kept.
    /// </summary>
    public static IReadOnlyList<string> Run(string executable, TimeSpan timeout, params string[] arguments)
    {
        try
        {
            var resolved = ResolveExecutable(
                executable,
                Environment.GetEnvironmentVariable("PATH"),
                Environment.GetEnvironmentVariable("PATHEXT"),
                OperatingSystem.IsWindows());

            if (resolved is null)
            {
                Logger.Write($"{executable} was not found on PATH");
                return [];
            }

            return StartAndRead(resolved, timeout, arguments);
        }
        catch (Exception ex)
        {
            Logger.Write($"{executable} failed to run: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Resolves <paramref name="executable"/> the way a shell would: PATH entries are tried in
    /// order, and the extensions of <paramref name="pathExtensions"/> are tried within each
    /// entry, so an earlier directory wins even when a later one holds a preferred extension.
    /// </summary>
    internal static string? ResolveExecutable(string executable, string? path, string? pathExtensions, bool isWindows)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            return null;
        }

        if (HasDirectoryPart(executable))
        {
            return IsLaunchable(executable, isWindows) ? Path.GetFullPath(executable) : null;
        }

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var entry in path.Split(Path.PathSeparator))
        {
            var directory = entry.Trim().Trim('"');

            if (directory.Length == 0 || !Directory.Exists(directory))
            {
                continue;
            }

            foreach (var extension in GetExtensions(executable, pathExtensions, isWindows))
            {
                var candidate = Path.Combine(directory, executable + extension);

                if (IsLaunchable(candidate, isWindows))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Reports whether the resolved path is a command script that only the Windows command
    /// interpreter can run. Executable images and Unix scripts with a shebang run directly.
    /// </summary>
    internal static bool RequiresCommandInterpreter(string resolved)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var extension = Path.GetExtension(resolved);

        return extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildInterpreterCommandLine(string resolved, IEnumerable<string> arguments)
    {
        var builder = new StringBuilder("/d /v:off /s /c \"").Append(QuoteForInterpreter(resolved));

        foreach (var argument in arguments)
        {
            builder.Append(' ').Append(QuoteForInterpreter(argument));
        }

        return builder.Append('"').ToString();
    }

    private static IReadOnlyList<string> StartAndRead(string resolved, TimeSpan timeout, string[] arguments)
    {
        using var process = new Process { StartInfo = CreateStartInfo(resolved, arguments) };

        if (!process.Start())
        {
            Logger.Write($"{resolved} could not be started");
            return [];
        }

        var gate = new object();
        var lines = new List<string>();
        var completed = true;
        using var cancellation = new CancellationTokenSource(timeout);

        var stdout = ReadLinesAsync(process.StandardOutput, lines, gate, cancellation.Token);
        var stderr = ReadToEndAsync(process.StandardError, cancellation.Token);

        try
        {
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                Logger.Write($"{resolved} did not finish within {timeout.TotalSeconds} seconds and was killed");
                TryKill(process);
                completed = false;
            }
        }
        catch (Exception ex)
        {
            Logger.Write($"Waiting for {resolved} failed: {ex.Message}");
            completed = false;
        }

        try
        {
            Task.WaitAll(
                [stdout, stderr],
                completed ? DrainAfterExitMilliseconds : ReaderShutdownMilliseconds);
        }
        catch (AggregateException ex)
        {
            Logger.Write($"Reading output from {resolved} failed: {ex.Message}");
        }

        LogStandardError(resolved, stderr);

        lock (gate)
        {
            return lines.ToArray();
        }
    }

    private static ProcessStartInfo CreateStartInfo(string resolved, string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (RequiresCommandInterpreter(resolved))
        {
            startInfo.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            startInfo.Arguments = BuildInterpreterCommandLine(resolved, arguments);
            return startInfo;
        }

        startInfo.FileName = resolved;

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    // The child's stderr is diagnostics, never completion output, so it only reaches the log.
    private static void LogStandardError(string resolved, Task<string> stderr)
    {
        if (!stderr.IsCompletedSuccessfully)
        {
            return;
        }

        var text = stderr.Result.Trim();

        if (text.Length > 0)
        {
            Logger.Write($"{resolved} wrote to stderr: {text}");
        }
    }

    private static async Task ReadLinesAsync(StreamReader reader, List<string> lines, object gate, CancellationToken token)
    {
        try
        {
            while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
            {
                lock (gate)
                {
                    lines.Add(line);
                }
            }
        }
        catch (Exception)
        {
            // A cancelled or broken read ends collection; lines read so far are kept.
        }
    }

    private static async Task<string> ReadToEndAsync(StreamReader reader, CancellationToken token)
    {
        try
        {
            return await reader.ReadToEndAsync(token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            Logger.Write($"Killing the child process failed: {ex.Message}");
        }
    }

    private static bool HasDirectoryPart(string executable) =>
        Path.IsPathRooted(executable) ||
        executable.Contains(Path.DirectorySeparatorChar) ||
        executable.Contains(Path.AltDirectorySeparatorChar);

    private static bool IsLaunchable(string candidate, bool isWindows)
    {
        try
        {
            if (!File.Exists(candidate))
            {
                return false;
            }

            // isWindows is chosen by the caller so tests can exercise both resolutions on
            // either host; the running platform is consulted as well because Unix execute
            // permissions cannot be read anywhere else.
            if (isWindows || OperatingSystem.IsWindows())
            {
                return true;
            }

            var mode = File.GetUnixFileMode(candidate);

            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string[] GetExtensions(string executable, string? pathExtensions, bool isWindows)
    {
        if (!isWindows || Path.GetExtension(executable).Length > 0)
        {
            // An explicit extension is used as-is, and Unix executables carry the bit instead.
            return [string.Empty];
        }

        if (string.IsNullOrWhiteSpace(pathExtensions))
        {
            return WindowsPathExtensions;
        }

        return pathExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // cmd.exe parses its own quoting, so this only wraps values carrying whitespace or syntax.
    private static string QuoteForInterpreter(string value) =>
        value.Length > 0 && !value.Any(IsInterpreterSyntax)
            ? value
            : $"\"{value.Replace("\"", "\\\"")}\"";

    private static bool IsInterpreterSyntax(char value) =>
        char.IsWhiteSpace(value) || CommandInterpreterSyntaxCharacters.Contains(value);
}
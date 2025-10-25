using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using PSCue.ArgumentCompleter;
using PSCue.Shared;

namespace PSCue.Benchmarks;

/// <summary>
/// Benchmarks for IPC client performance to ensure we meet 10ms startup + IPC target.
/// Using minimal iterations since we're testing I/O timeout behavior.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 5)]
[MemoryDiagnoser]
[MarkdownExporter]
public class IpcClientBenchmarks
{
    private const string TestCommand = "git";
    private const string TestCommandLine = "git checkout ma";
    private const string TestWordToComplete = "ma";
    private const int TestCursorPosition = 17;

    /// <summary>
    /// Benchmark: IPC call when server is NOT available (should timeout quickly and fallback).
    /// This measures the cost of the async IPC attempt + timeout handling.
    /// Target: less than 5ms (connection timeout)
    /// </summary>
    [Benchmark(Description = "IPC unavailable (async timeout + fallback)")]
    public async Task<int> IpcClientAsync_ServerUnavailable()
    {
        // Try to connect to IPC server (will timeout since no server is running)
        var response = await IpcClient.TryGetCompletionsAsync(
            TestCommand,
            TestCommandLine,
            TestWordToComplete,
            TestCursorPosition);

        // Should return null when server unavailable
        return response?.Completions.Length ?? 0;
    }

    /// <summary>
    /// Benchmark: Local completion logic (fallback path).
    /// This is what runs when IPC is unavailable.
    /// Target: less than 50ms total (including IPC timeout)
    /// </summary>
    [Benchmark(Description = "Local completions (fallback logic)")]
    public int LocalCompletions_GitCheckout()
    {
        var commandLine = TestCommandLine.AsSpan();
        var completions = CommandCompleter.GetCompletions(
            commandLine,
            TestWordToComplete,
            includeDynamicArguments: false); // Fast path - no git branch queries

        return completions.Count();
    }

    /// <summary>
    /// Benchmark: Full end-to-end ArgumentCompleter scenario (IPC unavailable).
    /// Simulates what happens on each Tab press when server is not running.
    /// Target: less than 50ms total
    /// </summary>
    [Benchmark(Description = "Full Tab completion (IPC unavailable)")]
    public async Task<int> FullCompletion_IpcUnavailable()
    {
        // Step 1: Try IPC (will timeout)
        var ipcResponse = await IpcClient.TryGetCompletionsAsync(
            TestCommand,
            TestCommandLine,
            TestWordToComplete,
            TestCursorPosition);

        if (ipcResponse is { Completions.Length: > 0 })
        {
            return ipcResponse.Completions.Length;
        }

        // Step 2: Fallback to local completions
        var commandLine = TestCommandLine.AsSpan();
        var completions = CommandCompleter.GetCompletions(
            commandLine,
            TestWordToComplete,
            includeDynamicArguments: false);

        return completions.Count();
    }

    /// <summary>
    /// Benchmark: Multiple completion scenarios to test different code paths.
    /// </summary>
    [Benchmark(Description = "Local completions - git commit flags")]
    public int LocalCompletions_GitCommitFlags()
    {
        var commandLine = "git commit -".AsSpan();
        var completions = CommandCompleter.GetCompletions(
            commandLine,
            "-",
            includeDynamicArguments: false);

        return completions.Count();
    }

    /// <summary>
    /// Benchmark: Scoop completions (Windows-specific, will return 0 on other platforms).
    /// </summary>
    [Benchmark(Description = "Local completions - scoop install")]
    public int LocalCompletions_ScoopInstall()
    {
        var commandLine = "scoop install ".AsSpan();
        var completions = CommandCompleter.GetCompletions(
            commandLine,
            "",
            includeDynamicArguments: false);

        return completions.Count();
    }
}

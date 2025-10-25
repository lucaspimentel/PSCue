using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using PSCue.ArgumentCompleter;
using PSCue.Module;
using PSCue.Shared;

namespace PSCue.Benchmarks;

/// <summary>
/// Benchmarks for IPC client performance WITH server running.
/// This measures the happy path where IPC is available.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MarkdownExporter]
[ExcludeFromCodeCoverage] // Suppress preview feature warnings for benchmarks
public class IpcClientWithServerBenchmarks
{
    private const string TestCommand = "git";
    private const string TestCommandLine = "git checkout ma";
    private const string TestWordToComplete = "ma";
    private const int TestCursorPosition = 17;

    private IpcServer? _server;

    /// <summary>
    /// Start IPC server before running benchmarks.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        // IpcServer starts automatically in its constructor
        _server = new IpcServer();

        // Give server time to start listening
        Thread.Sleep(100);
    }

    /// <summary>
    /// Stop IPC server after benchmarks complete.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _server?.Dispose();
    }

    /// <summary>
    /// Benchmark: IPC call when server IS available (happy path).
    /// This measures the actual async round-trip time.
    /// Target: <5ms (should be fast since server is local)
    /// </summary>
    [Benchmark(Description = "IPC available (async round-trip)")]
    public async Task<int> IpcClientAsync_ServerAvailable()
    {
        // Call IPC server (should succeed)
        var response = await IpcClient.TryGetCompletionsAsync(
            TestCommand,
            TestCommandLine,
            TestWordToComplete,
            TestCursorPosition);

        // Should return completions from server
        return response?.Completions.Length ?? 0;
    }

    /// <summary>
    /// Benchmark: Multiple sequential IPC calls (simulates rapid Tab presses).
    /// Target: <5ms per call
    /// </summary>
    [Benchmark(Description = "IPC multiple sequential calls")]
    public async Task<int> IpcClientAsync_MultipleCallsSequential()
    {
        int total = 0;

        for (int i = 0; i < 3; i++)
        {
            var response = await IpcClient.TryGetCompletionsAsync(
                TestCommand,
                TestCommandLine,
                TestWordToComplete,
                TestCursorPosition);

            total += response?.Completions.Length ?? 0;
        }

        return total;
    }

    /// <summary>
    /// Benchmark: IPC with cached result (second call should be faster).
    /// Target: <2ms (cache hit)
    /// </summary>
    [Benchmark(Description = "IPC with cache hit")]
    public async Task<int> IpcClientAsync_CacheHit()
    {
        // First call - populates cache
        await IpcClient.TryGetCompletionsAsync(
            TestCommand,
            TestCommandLine,
            TestWordToComplete,
            TestCursorPosition);

        // Second call - should hit cache
        var response = await IpcClient.TryGetCompletionsAsync(
            TestCommand,
            TestCommandLine,
            TestWordToComplete,
            TestCursorPosition);

        return response?.Completions.Length ?? 0;
    }
}

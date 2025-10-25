using BenchmarkDotNet.Running;

namespace PSCue.Benchmarks;

/// <summary>
/// PSCue Performance Benchmarks
///
/// Run with: dotnet run -c Release --project benchmark/PSCue.Benchmarks/
///
/// Performance Targets:
/// - IPC connection timeout: <10ms
/// - Local completion (no dynamic args): <50ms
/// - Full Tab completion with IPC fallback: <50ms
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<IpcClientBenchmarks>();

        Console.WriteLine();
        Console.WriteLine("=== Performance Target Summary ===");
        Console.WriteLine("IPC timeout:              <10ms");
        Console.WriteLine("Local completions:        <50ms");
        Console.WriteLine("Full Tab completion:      <50ms");
        Console.WriteLine("==================================");
    }
}

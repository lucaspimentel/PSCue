using BenchmarkDotNet.Running;

namespace PSCue.Benchmarks;

/// <summary>
/// PSCue Performance Benchmarks
///
/// Run with: dotnet run -c Release --project benchmark/PSCue.Benchmarks/
///
/// Or run specific benchmark:
/// dotnet run -c Release --project benchmark/PSCue.Benchmarks/ --filter *WithServer*
///
/// Performance Targets:
/// - IPC connection timeout: <10ms
/// - IPC round-trip (server available): <5ms
/// - Local completion (no dynamic args): <50ms
/// - Full Tab completion with IPC fallback: <50ms
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        // Run all benchmarks
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

        Console.WriteLine();
        Console.WriteLine("=== Performance Target Summary ===");
        Console.WriteLine("IPC timeout (unavailable):        <10ms");
        Console.WriteLine("IPC round-trip (available):       <5ms");
        Console.WriteLine("IPC cache hit:                    <2ms");
        Console.WriteLine("Local completions:                <50ms");
        Console.WriteLine("Full Tab completion:              <50ms");
        Console.WriteLine("======================================");
    }
}

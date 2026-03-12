using BenchmarkDotNet.Attributes;
using PSCue.Shared.Completions.Json;
using PSCue.Shared.KnownCompletions;

namespace PSCue.Benchmarks;

[MemoryDiagnoser]
public class JsonVsHardcodedBenchmark
{
    private string _dustJson = null!;
    private string _dustJsonPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _dustJsonPath = Path.Combine(AppContext.BaseDirectory, "completions", "dust.json");
        _dustJson = File.ReadAllText(_dustJsonPath);
    }

    [Benchmark(Baseline = true)]
    public object Hardcoded_DustCommand() => DustCommand.Create();

    [Benchmark]
    public object? Json_FromString() => JsonCompletionLoader.LoadFromJson(_dustJson);

    [Benchmark]
    public object? Json_FromFile() => JsonCompletionLoader.LoadFromFile(_dustJsonPath);
}

# PSCue.Benchmarks

## Description

Home for BenchmarkDotNet benchmarks of PSCue's shared completion and learning logic.

The project currently defines **no benchmarks**: `Program.cs` only invokes `BenchmarkSwitcher`, and the assembly contains no `[Benchmark]` types. The last benchmark classes were removed with the IPC layer in `e76b6dd`. Running the project prints `No [Benchmark] attribute found on 'PSCue.Benchmarks' assembly.` followed by a target summary that still lists the removed IPC layer.

## Dependencies

### NuGet Packages
- `BenchmarkDotNet` - Benchmarking framework

### Internal Dependencies
- `PSCue.ArgumentCompleter` - ArgumentCompleter benchmarks
- `PSCue.Module` - Module benchmarks
- `PSCue.Shared` - Shared logic benchmarks

## Dependents

None - this is a benchmark project.

## Performance Targets

Targets the project is meant to keep an eye on. None of them are currently verified by this
project.

| Scenario | Target |
|----------|--------|
| Tab completion (no dynamic args) | <50ms |
| Tab completion (with dynamic args) | <50ms |
| Learning feedback | <5ms |
| ML prediction | <20ms (PowerShell prediction timeout) |
| PCD tab completion | <10ms |
| PCD best-match navigation | <50ms |

## Running Benchmarks

```bash
# Run all benchmarks
dotnet run -c Release --project benchmark/PSCue.Benchmarks/

# Run specific benchmark
dotnet run -c Release --project benchmark/PSCue.Benchmarks/ --filter *ArgumentCompleter*
```

Benchmarks in this project run **in process** against the shared library, so they cannot
observe process creation, PowerShell startup, or child-process launches. For the
fresh-process cost of the `pscue-completer` executable itself, use
[`benchmark/spawn-cost`](../spawn-cost/README.md).

## Benchmark Scenarios

None are implemented. The scenarios this project would cover, all in process against the
shared library:

1. **Tab completion, static arguments** - command parsing, static matching, result formatting
2. **Tab completion, dynamic arguments** - retrieval cost and caching for git branches, scoop packages
3. **Learning feedback** - `FeedbackProvider` cost after a command runs
4. **ML prediction** - `CommandPredictor` suggestion generation against the 20ms PowerShell timeout

Process creation and child-process launches are deliberately out of reach here; measure those
with [`benchmark/spawn-cost`](../spawn-cost/README.md).

## Interpreting Results

BenchmarkDotNet prints one row per benchmark method. Example of the shape of that output:
```
| Method                  | Mean     | Error    | StdDev   | Gen0   | Allocated |
|------------------------ |---------:|---------:|---------:|-------:|----------:|
| TabCompletion_Static    | 2.34 ms  | 0.012 ms | 0.011 ms | 10.000 |   15.8 KB |
| TabCompletion_Dynamic   | 35.67 ms | 0.167 ms | 0.156 ms | 15.000 |   42.3 KB |
| LearningFeedback        | 1.12 ms  | 0.008 ms | 0.007 ms |  5.000 |    8.4 KB |
| MLPrediction            | 12.45 ms | 0.089 ms | 0.083 ms |  8.000 |   24.1 KB |
```

These numbers are illustrative, not measurements from this repository.

**Key metrics:**
- **Mean**: Average execution time - must meet targets
- **Allocated**: Memory allocated per operation - keep low to avoid GC pressure
- **Gen0**: Number of Gen0 collections - fewer is better

## Current Architecture

PSCue uses a dual-component design:
- **ArgumentCompleter** (NativeAOT exe): fresh process per Tab press, computes completions locally
- **Module** (managed DLL): Long-lived process for predictions and learning
- **No IPC**: ArgumentCompleter computes completions locally
- **SQLite**: Cross-session persistence with concurrent access support

**Key design decisions:**
- Tab completion always computes locally (no IPC overhead)
- Dynamic arguments (git branches, etc.) come from child processes on every Tab press; their cost is measured by [`benchmark/spawn-cost`](../spawn-cost/README.md)
- Learning feedback runs after every command (sub-5ms target)
- ML predictions cached for the 20ms PowerShell timeout

## Notes

- Benchmarks should be run on representative hardware
- Dynamic arguments (git branches) will vary by repository size
- Results vary by machine - focus on relative performance vs targets

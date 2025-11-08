# PSCue Performance Benchmarks

This project uses [BenchmarkDotNet](https://benchmarkdotnet.org/) to measure PSCue performance and ensure we meet our targets.

## Performance Targets

| Scenario | Target | Critical? |
|----------|--------|-----------|
| ArgumentCompleter startup | <10ms | ✅ Yes - NativeAOT startup time |
| Tab completion (no dynamic args) | <50ms | ✅ Yes - user-facing latency |
| Tab completion (with dynamic args) | <50ms | ✅ Yes - includes git branches, etc. |
| Learning feedback | <5ms | ✅ Yes - runs after every command |
| ML prediction | <20ms | ✅ Yes - PowerShell prediction timeout |
| PCD tab completion | <10ms | ✅ Yes - user-facing latency |
| PCD best-match navigation | <50ms | ✅ Yes - directory search |

## Running Benchmarks

```bash
# Run all benchmarks
dotnet run -c Release --project benchmark/PSCue.Benchmarks/

# Run specific benchmark
dotnet run -c Release --project benchmark/PSCue.Benchmarks/ --filter *ArgumentCompleter*
```

## Benchmark Scenarios

### 1. ArgumentCompleter Startup
Measures NativeAOT executable startup time. Critical for Tab completion responsiveness.

**What it measures:**
- Process creation overhead
- Module initialization
- First completion generation
- Should complete in <10ms

### 2. Tab Completion (Static Args)
Measures completion generation for commands with static arguments (no dynamic data).

**What it measures:**
- Command parsing
- Static completion matching
- Result formatting
- Should complete in <50ms

### 3. Tab Completion (Dynamic Args)
Measures completion with dynamic arguments (git branches, scoop packages, etc.).

**What it measures:**
- Dynamic data retrieval (git branch list, etc.)
- Caching effectiveness
- Total completion time
- Should complete in <50ms

### 4. Learning Feedback
Measures FeedbackProvider performance after command execution.

**What it measures:**
- Command history updates
- ArgumentGraph updates
- Privacy filtering
- Should complete in <5ms

### 5. ML Prediction
Measures CommandPredictor inline suggestion generation.

**What it measures:**
- N-gram sequence lookup
- Workflow prediction
- Generic learning suggestions
- Should complete in <20ms (PowerShell timeout)

## Interpreting Results

Example output:
```
| Method                          | Mean      | Error    | StdDev   | Gen0   | Allocated |
|-------------------------------- |----------:|---------:|---------:|-------:|----------:|
| ArgumentCompleter_Startup       |   8.23 ms | 0.045 ms | 0.042 ms |      - |    1.2 KB |
| TabCompletion_Static            |   2.34 ms | 0.012 ms | 0.011 ms | 10.000 |   15.8 KB |
| TabCompletion_Dynamic           |  35.67 ms | 0.167 ms | 0.156 ms | 15.000 |   42.3 KB |
| LearningFeedback                |   1.12 ms | 0.008 ms | 0.007 ms |  5.000 |    8.4 KB |
| MLPrediction                    |  12.45 ms | 0.089 ms | 0.083 ms |  8.000 |   24.1 KB |
```

**Key metrics:**
- **Mean**: Average execution time - must meet targets
- **Allocated**: Memory allocated per operation - keep low to avoid GC pressure
- **Gen0**: Number of Gen0 collections - fewer is better

## Current Architecture

PSCue uses a dual-component design:
- **ArgumentCompleter** (NativeAOT exe): Sub-10ms startup for Tab completion
- **Module** (managed DLL): Long-lived process for predictions and learning
- **No IPC**: ArgumentCompleter computes completions locally (fast enough)
- **SQLite**: Cross-session persistence with concurrent access support

**Key design decisions:**
- Tab completion always computes locally (no IPC overhead)
- Dynamic arguments (git branches, etc.) computed on every Tab press
- Learning feedback runs after every command (sub-5ms target)
- ML predictions cached for 20ms PowerShell timeout

## Notes

- Benchmarks should be run on representative hardware
- Dynamic arguments (git branches) will vary by repository size
- Results vary by machine - focus on relative performance vs targets
- NativeAOT provides consistent startup times across runs

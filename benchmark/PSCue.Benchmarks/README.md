# PSCue Performance Benchmarks

This project uses [BenchmarkDotNet](https://benchmarkdotnet.org/) to measure PSCue performance and ensure we meet our targets.

## Performance Targets

| Scenario | Target | Critical? |
|----------|--------|-----------|
| IPC connection timeout | <10ms | ✅ Yes - adds to every Tab press |
| Local completions (no dynamic args) | <50ms | ✅ Yes - total Tab completion time |
| Full Tab completion with IPC fallback | <50ms | ✅ Yes - user-facing latency |

## Running Benchmarks

```bash
# Run all benchmarks
dotnet run -c Release --project benchmark/PSCue.Benchmarks/

# Run specific benchmark
dotnet run -c Release --project benchmark/PSCue.Benchmarks/ --filter *IpcClientAsync*
```

## Benchmark Scenarios

### 1. IPC Unavailable (Async Timeout + Fallback)
Measures the cost of attempting IPC connection when server is not running. This is the **critical async overhead** we're testing.

**What it measures:**
- Async method overhead
- CancellationToken timeout handling
- Named pipe connection attempt + failure
- Should complete in <10ms (connection timeout)

### 2. Local Completions (Fallback Logic)
Measures the fallback path when IPC is unavailable. This is pure completion logic without I/O.

**What it measures:**
- Parsing command line
- Matching completions
- No dynamic arguments (fast path)
- Should complete in <50ms

### 3. Full Tab Completion (IPC Unavailable)
End-to-end scenario simulating real Tab press when server isn't running.

**What it measures:**
- IPC attempt (will timeout)
- Fallback to local completions
- Total user-facing latency
- Should complete in <50ms total

### 4. Git Commit Flags
Tests flag completion performance (common scenario).

### 5. Scoop Install
Tests dynamic completion scenarios (Windows-only).

## Interpreting Results

Example output:
```
| Method                                  | Mean      | Error    | StdDev   | Gen0   | Allocated |
|---------------------------------------- |----------:|---------:|---------:|-------:|----------:|
| IPC unavailable (async timeout)         |  12.05 ms | 0.045 ms | 0.042 ms |      - |    1.2 KB |
| Local completions (fallback)            |   2.34 ms | 0.012 ms | 0.011 ms | 10.000 |   15.8 KB |
| Full Tab completion (IPC unavailable)   |  14.39 ms | 0.067 ms | 0.063 ms | 10.000 |   17.0 KB |
```

**Key metrics:**
- **Mean**: Average execution time - must meet targets
- **Allocated**: Memory allocated per operation - keep low to avoid GC pressure
- **Gen0**: Number of Gen0 collections - fewer is better

## What We're Testing

The async refactoring added these changes:
- `async Task<IpcResponse?>` instead of sync + `Task.Wait()`
- `CancellationToken` for timeout handling
- `PipeOptions.Asynchronous` for true async I/O
- Async Main method

**Question:** Does the async overhead hurt the <10ms startup + IPC target?

**Expected results:**
- ✅ IPC timeout should still be ~10ms (connection timeout setting)
- ✅ Local completions should be <5ms (no I/O, pure CPU)
- ✅ Full completion should be <15ms (timeout + fallback)

If results exceed targets, we may need to:
1. Revert to sync implementation
2. Add hybrid approach (sync check + async I/O)
3. Optimize timeout values

## Notes

- Benchmarks run with IPC server **not** running to isolate async overhead
- Dynamic arguments disabled for consistent timing
- Results vary by machine - focus on relative performance vs targets

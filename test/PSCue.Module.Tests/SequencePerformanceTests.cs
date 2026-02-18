using System.Diagnostics;

namespace PSCue.Module.Tests;

/// <summary>
/// Performance tests for SequencePredictor.
/// Critical constraints:
/// - Cache lookup: less than 1ms (to fit within 20ms inline prediction budget)
/// - Total prediction flow with ML: less than 20ms (PowerShell enforced timeout)
/// </summary>
public class SequencePerformanceTests
{
    private readonly ITestOutputHelper _output;

    public SequencePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GetPredictions_CacheLookup_ShouldBeFasterThan1ms()
    {
        // Arrange - Populate with realistic data
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);
        var sequences = new Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>
                        {
                            ["git"] = Enumerable.Range(1, 10)
                                                .ToDictionary(
                                                     i => $"subcommand{i}",
                                                     i => (frequency: i * 10, lastSeen: DateTime.UtcNow)
                                                 )
                        };
        predictor.Initialize(sequences);

        // Warm up (JIT compilation)
        for (int i = 0; i < 100; i++)
        {
            predictor.GetPredictions(new[] { "git" });
        }

        // Act - Measure lookup time (average of 1000 iterations)
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            predictor.GetPredictions(new[] { "git" });
        }

        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / 1000;

        _output.WriteLine($"Average cache lookup time: {avgMs:F3}ms");

        // Assert - Should be well under 1ms
        Assert.True(avgMs < 1.0, $"Cache lookup took {avgMs:F3}ms, expected <1ms");
    }

    [Fact]
    public void GetPredictions_LargeCache_ShouldRemainFast()
    {
        // Arrange - Simulate 100 commands with 10 next-commands each (1000 total entries)
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);
        var sequences = new Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>();

        for (int cmd = 0; cmd < 100; cmd++)
        {
            var commandName = $"command{cmd}";
            sequences[commandName] = new Dictionary<string, (int frequency, DateTime lastSeen)>();

            for (int next = 0; next < 10; next++)
            {
                sequences[commandName][$"next{next}"] = (10, DateTime.UtcNow);
            }
        }

        predictor.Initialize(sequences);

        // Warm up
        for (int i = 0; i < 100; i++)
        {
            predictor.GetPredictions(new[] { "command50" });
        }

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            predictor.GetPredictions(new[] { "command50" });
        }

        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / 1000;

        _output.WriteLine($"Large cache lookup time: {avgMs:F3}ms (1000 entries)");

        // Assert - Should still be <1ms even with large cache
        Assert.True(avgMs < 1.0, $"Large cache lookup took {avgMs:F3}ms, expected <1ms");
    }

    [Fact]
    public void RecordSequence_ShouldBeFast()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2);

        // Warm up
        for (int i = 0; i < 100; i++)
        {
            predictor.RecordSequence(new[] { "git", "add" });
        }

        // Act - Measure recording time
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            predictor.RecordSequence(new[] { "git", "add", "commit" });
        }

        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / 1000;

        _output.WriteLine($"Average record time: {avgMs:F3}ms");

        // Assert - Recording should be <0.1ms (it's async for user, but let's ensure it's fast)
        Assert.True(avgMs < 0.5, $"Record took {avgMs:F3}ms, expected <0.5ms");
    }

    [Fact]
    public void FullPredictionFlow_WithGenericPredictor_ShouldBeFasterThan20ms()
    {
        // This tests the complete inline prediction flow:
        // CommandPredictor -> GenericPredictor -> SequencePredictor

        // Arrange - Set up full stack
        var history = new CommandHistory(maxSize: 100);
        var argumentGraph = new ArgumentGraph(maxCommands: 500, maxArgumentsPerCommand: 100);
        var contextAnalyzer = new ContextAnalyzer();
        var sequencePredictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);

        // Populate with realistic data
        for (int i = 0; i < 10; i++)
        {
            history.Add("git", "git add file.txt", new[] { "add", "file.txt" }, true);
            history.Add("git", "git commit -m \"message\"", new[] { "commit", "-m", "\"message\"" }, true);
            sequencePredictor.RecordSequence(new[] { "git", "add" });
            sequencePredictor.RecordSequence(new[] { "add", "commit" });
        }

        var genericPredictor = new GenericPredictor(history, argumentGraph, contextAnalyzer, sequencePredictor);

        // Warm up
        for (int i = 0; i < 50; i++)
        {
            genericPredictor.GetSuggestions("git ", maxResults: 10);
        }

        // Act - Measure full prediction flow
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            genericPredictor.GetSuggestions("git ", maxResults: 10);
        }

        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / 100;

        _output.WriteLine($"Full prediction flow time: {avgMs:F2}ms");

        // Assert - Must be <20ms (PowerShell timeout)
        Assert.True(avgMs < 20.0, $"Full prediction took {avgMs:F2}ms, expected <20ms");
    }

    [Fact]
    public void Initialize_LargeDataset_ShouldBeReasonablyFast()
    {
        // Arrange - Simulate loading 500 commands with 20 next-commands each
        var sequences = new Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>();

        for (int cmd = 0; cmd < 500; cmd++)
        {
            sequences[$"command{cmd}"] = Enumerable.Range(0, 20)
                                                   .ToDictionary(
                                                        i => $"next{i}",
                                                        i => (frequency: i + 1, lastSeen: DateTime.UtcNow)
                                                    );
        }

        var predictor = new SequencePredictor(ngramOrder: 2);

        // Act
        var sw = Stopwatch.StartNew();
        predictor.Initialize(sequences);
        sw.Stop();

        _output.WriteLine($"Initialize with 10,000 entries: {sw.ElapsedMilliseconds}ms");

        // Assert - Should complete in reasonable time (<100ms)
        Assert.True(sw.ElapsedMilliseconds < 100, $"Initialize took {sw.ElapsedMilliseconds}ms, expected <100ms");
    }

    [Fact]
    public void GetDelta_ShouldBeFast()
    {
        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2);

        // Record 1000 sequences
        for (int i = 0; i < 1000; i++)
        {
            predictor.RecordSequence(new[] { $"cmd{i % 10}", $"next{i % 5}" });
        }

        // Act
        var sw = Stopwatch.StartNew();
        var delta = predictor.GetDelta();
        sw.Stop();

        _output.WriteLine($"GetDelta time: {sw.ElapsedMilliseconds}ms");

        // Assert - Should be <10ms
        Assert.True(sw.ElapsedMilliseconds < 10, $"GetDelta took {sw.ElapsedMilliseconds}ms, expected <10ms");
        Assert.NotEmpty(delta);
    }

    [Fact]
    public async Task ConcurrentReadAccess_ShouldNotDegrade()
    {
        // Test that concurrent reads don't cause contention

        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2, minFrequency: 1);
        var sequences = new Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>
                        {
                            ["git"] = new()
                                      {
                                          ["add"] = (10, DateTime.UtcNow),
                                          ["commit"] = (8, DateTime.UtcNow),
                                          ["push"] = (5, DateTime.UtcNow)
                                      }
                        };
        predictor.Initialize(sequences);

        // Act - Multiple threads reading concurrently
        var tasks = new Task[10];
        var sw = Stopwatch.StartNew();

        for (int t = 0; t < 10; t++)
        {
            tasks[t] = Task.Run(
                () =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        predictor.GetPredictions(["git"]);
                    }
                },
                TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        var avgMsPerCall = sw.Elapsed.TotalMilliseconds / (10 * 1000);

        _output.WriteLine($"Concurrent read time: {avgMsPerCall:F3}ms per call");

        // Assert - Should still be <1ms even with concurrent access
        Assert.True(avgMsPerCall < 1.0, $"Concurrent reads took {avgMsPerCall:F3}ms, expected <1ms");
    }

    [Fact]
    public void MemoryUsage_LargeCache_ShouldBeReasonable()
    {
        // Test that memory usage doesn't explode with large caches

        // Arrange
        var predictor = new SequencePredictor(ngramOrder: 2);

        // Capture memory before
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memBefore = GC.GetTotalMemory(false);

        // Act - Add 10,000 sequences (realistic for months of usage)
        for (int i = 0; i < 10000; i++)
        {
            predictor.RecordSequence([$"cmd{i % 100}", $"next{i % 50}"]);
        }

        // Capture memory after
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memAfter = GC.GetTotalMemory(false);
        var memUsedMb = (memAfter - memBefore) / (1024.0 * 1024.0);

        _output.WriteLine($"Memory used for 10,000 sequences: {memUsedMb:F2}MB");

        // Assert - Should be <5MB for 10,000 sequences
        Assert.True(memUsedMb < 5.0, $"Memory usage was {memUsedMb:F2}MB, expected <5MB");
    }
}

using System.Diagnostics;

namespace PSCue.Module.Tests;

/// <summary>
/// Times an action as the fastest of several attempts.
/// A single sample flakes on loaded hardware, because a stall that lands mid-call inflates that one
/// call. Only the attempt in flight during a stall is charged, so the fastest of many attempts is
/// stable. The warm-up attempts keep JIT out of the measurement.
/// </summary>
internal static class BestOfNTimer
{
    /// <summary>Calls run before measurement so JIT compilation is not charged to a timed attempt.</summary>
    public const int WarmupAttempts = 10;

    /// <summary>
    /// Runs the warm-up attempts, then <paramref name="attempts"/> measured attempts, and returns the
    /// fastest measured attempt, in milliseconds.
    /// </summary>
    public static double BestMilliseconds(Action action, int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);

        for (var i = 0; i < WarmupAttempts; i++)
        {
            action();
        }

        var best = double.MaxValue;
        for (var i = 0; i < attempts; i++)
        {
            var start = Stopwatch.GetTimestamp();
            action();
            best = Math.Min(best, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        return best;
    }
}

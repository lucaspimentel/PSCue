using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace PSCue.Module;

/// <summary>
/// Workflow transition for tracking command sequences.
/// Records frequency, timing, and confidence for workflow predictions.
/// </summary>
public class WorkflowTransition
{
    /// <summary>
    /// Default timing in seconds when no timing data is available.
    /// Used as fallback when AverageTimeDelta is zero.
    /// </summary>
    private const double DefaultTimingSeconds = 60.0;

    /// <summary>
    /// The next command in the workflow.
    /// </summary>
    public string NextCommand { get; set; } = string.Empty;

    /// <summary>
    /// Number of times this transition has occurred.
    /// </summary>
    public int Frequency { get; set; }

    /// <summary>
    /// Total time delta in milliseconds (for calculating average).
    /// </summary>
    public long TotalTimeDeltaMs { get; set; }

    /// <summary>
    /// When this transition was first observed.
    /// </summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>
    /// When this transition was last observed.
    /// </summary>
    public DateTime LastSeen { get; set; }

    /// <summary>
    /// Average time between commands in this transition.
    /// </summary>
    public TimeSpan AverageTimeDelta => TimeSpan.FromMilliseconds(
        Frequency > 0 ? (double)TotalTimeDeltaMs / Frequency : 0
    );

    /// <summary>
    /// Calculates a confidence score based on frequency and recency.
    /// </summary>
    public double GetConfidence(int minFrequency, int decayDays = 30)
    {
        if (Frequency < minFrequency)
            return 0.0;

        // Base probability from frequency (0.0 to 1.0)
        double baseScore = Math.Min(1.0, Frequency / 20.0); // 20+ occurrences = 100% confidence

        // Recency factor (exponential decay)
        var ageDays = (DateTime.UtcNow - LastSeen).TotalDays;
        var recencyFactor = Math.Exp(-ageDays / decayDays);

        // Combined score: 70% frequency + 30% recency
        return (0.7 * baseScore) + (0.3 * recencyFactor);
    }

    /// <summary>
    /// Calculates a time-sensitive score based on how long since last command.
    /// Boosts suggestions if timing matches typical workflow pattern.
    /// </summary>
    public double GetTimeSensitiveScore(TimeSpan timeSinceLastCommand, int minFrequency, int decayDays = 30)
    {
        double baseConfidence = GetConfidence(minFrequency, decayDays);

        if (baseConfidence == 0.0 || Frequency == 0)
            return 0.0;

        // Calculate time proximity boost
        double avgSeconds = AverageTimeDelta.TotalSeconds;
        if (avgSeconds == 0)
            avgSeconds = DefaultTimingSeconds;

        double actualSeconds = timeSinceLastCommand.TotalSeconds;
        double ratio = actualSeconds / avgSeconds;

        // Time boost based on how close actual timing is to expected timing
        double timeBoost;
        if (ratio < 1.5)        // Within expected timeframe
            timeBoost = 1.5;
        else if (ratio < 5)     // Moderately delayed
            timeBoost = 1.2;
        else if (ratio < 30)    // Significantly delayed
            timeBoost = 1.0;
        else                     // Very old, weak relationship
            timeBoost = 0.8;

        return baseConfidence * timeBoost;
    }
}

/// <summary>
/// Workflow suggestion with command, confidence, and source information.
/// </summary>
public class WorkflowSuggestion
{
    /// <summary>
    /// The suggested next command.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score (0.0 to 1.0+).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Source of the suggestion (e.g., "Bigram", "Trigram", "Workflow").
    /// </summary>
    public string Source { get; set; } = "Workflow";

    /// <summary>
    /// Human-readable reason for the suggestion.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Learns command workflow patterns from user behavior.
/// Tracks command → next command transitions with timing and frequency data.
/// </summary>
public class WorkflowLearner : IDisposable
{
    // Thread-safe cache for fast lookups (<2ms target)
    // Key: from command, Value: dictionary of next commands with transition data
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WorkflowTransition>> _workflowGraph;

    // Delta tracking for concurrent session support (additive merging)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WorkflowTransition>> _delta;

    private readonly int _minFrequency; // Minimum occurrences to suggest
    private readonly int _maxTimeDeltaMinutes; // Maximum time between commands to consider related
    private readonly double _minConfidence; // Minimum confidence to show suggestion
    private readonly int _decayDays; // Days for score decay
    private readonly object _lock = new object();
    private bool _disposed;

    // Configuration defaults
    private const int DefaultMinFrequency = 5;
    private const int DefaultMaxTimeDelta = 15; // minutes
    private const double DefaultMinConfidence = 0.6;
    private const int DefaultDecayDays = 30;
    private const int MaxTransitionsPerCommand = 20; // Limit memory usage

    /// <summary>
    /// Initializes a new WorkflowLearner.
    /// </summary>
    public WorkflowLearner(
        int minFrequency = DefaultMinFrequency,
        int maxTimeDeltaMinutes = DefaultMaxTimeDelta,
        double minConfidence = DefaultMinConfidence,
        int decayDays = DefaultDecayDays)
    {
        if (minFrequency < 1)
            throw new ArgumentOutOfRangeException(nameof(minFrequency), "Minimum frequency must be at least 1");

        if (maxTimeDeltaMinutes < 1)
            throw new ArgumentOutOfRangeException(nameof(maxTimeDeltaMinutes), "Maximum time delta must be at least 1 minute");

        if (minConfidence < 0 || minConfidence > 1)
            throw new ArgumentOutOfRangeException(nameof(minConfidence), "Minimum confidence must be between 0 and 1");

        if (decayDays < 1)
            throw new ArgumentOutOfRangeException(nameof(decayDays), "Decay days must be at least 1");

        _minFrequency = minFrequency;
        _maxTimeDeltaMinutes = maxTimeDeltaMinutes;
        _minConfidence = minConfidence;
        _decayDays = decayDays;

        _workflowGraph = new ConcurrentDictionary<string, ConcurrentDictionary<string, WorkflowTransition>>(StringComparer.OrdinalIgnoreCase);
        _delta = new ConcurrentDictionary<string, ConcurrentDictionary<string, WorkflowTransition>>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Initializes the workflow graph from persisted data.
    /// </summary>
    public void Initialize(Dictionary<string, Dictionary<string, WorkflowTransition>> workflows)
    {
        if (workflows == null)
            throw new ArgumentNullException(nameof(workflows));

        lock (_lock)
        {
            _workflowGraph.Clear();
            foreach (var (fromCommand, transitions) in workflows)
            {
                var transitionDict = new ConcurrentDictionary<string, WorkflowTransition>(StringComparer.OrdinalIgnoreCase);
                foreach (var (toCommand, transition) in transitions)
                {
                    transitionDict[toCommand] = transition;
                }
                _workflowGraph[fromCommand] = transitionDict;
            }
        }
    }

    /// <summary>
    /// Records a workflow transition from one command to another.
    /// Called by FeedbackProvider after each successful command.
    /// </summary>
    /// <param name="fromCommand">The previous command.</param>
    /// <param name="toCommand">The current command.</param>
    /// <param name="timeDelta">Time elapsed between commands.</param>
    public void RecordTransition(string fromCommand, string toCommand, TimeSpan timeDelta)
    {
        if (string.IsNullOrWhiteSpace(fromCommand) || string.IsNullOrWhiteSpace(toCommand))
            return;

        // Ignore transitions that are too old (likely unrelated)
        if (timeDelta.TotalMinutes > _maxTimeDeltaMinutes)
            return;

        // Normalize commands (extract base command, ignore arguments)
        fromCommand = NormalizeCommand(fromCommand);
        toCommand = NormalizeCommand(toCommand);

        // Don't record self-transitions
        if (fromCommand.Equals(toCommand, StringComparison.OrdinalIgnoreCase))
            return;

        var timestamp = DateTime.UtcNow;
        var timeDeltaMs = (long)timeDelta.TotalMilliseconds;

        // Update workflow graph
        var transitions = _workflowGraph.GetOrAdd(fromCommand, _ => new ConcurrentDictionary<string, WorkflowTransition>(StringComparer.OrdinalIgnoreCase));

        transitions.AddOrUpdate(
            toCommand,
            _ => new WorkflowTransition
            {
                NextCommand = toCommand,
                Frequency = 1,
                TotalTimeDeltaMs = timeDeltaMs,
                FirstSeen = timestamp,
                LastSeen = timestamp
            },
            (_, existing) => new WorkflowTransition
            {
                NextCommand = toCommand,
                Frequency = existing.Frequency + 1,
                TotalTimeDeltaMs = existing.TotalTimeDeltaMs + timeDeltaMs,
                FirstSeen = existing.FirstSeen,
                LastSeen = timestamp
            }
        );

        // Update delta for persistence
        var deltaTransitions = _delta.GetOrAdd(fromCommand, _ => new ConcurrentDictionary<string, WorkflowTransition>(StringComparer.OrdinalIgnoreCase));

        deltaTransitions.AddOrUpdate(
            toCommand,
            _ => new WorkflowTransition
            {
                NextCommand = toCommand,
                Frequency = 1,
                TotalTimeDeltaMs = timeDeltaMs,
                FirstSeen = timestamp,
                LastSeen = timestamp
            },
            (_, existing) => new WorkflowTransition
            {
                NextCommand = toCommand,
                Frequency = existing.Frequency + 1,
                TotalTimeDeltaMs = existing.TotalTimeDeltaMs + timeDeltaMs,
                FirstSeen = existing.FirstSeen < timestamp ? existing.FirstSeen : timestamp,
                LastSeen = timestamp
            }
        );

        // Enforce memory limits (keep top N transitions per command)
        EnforceLimits(transitions);
    }

    /// <summary>
    /// Gets workflow predictions for the next command.
    /// MUST complete in <2ms to fit within performance budget.
    /// </summary>
    /// <param name="currentCommand">The current/last command.</param>
    /// <param name="timeSinceLastCommand">Time elapsed since last command.</param>
    /// <param name="maxResults">Maximum number of suggestions to return.</param>
    /// <returns>List of workflow suggestions sorted by confidence descending.</returns>
    public List<WorkflowSuggestion> GetNextCommandPredictions(
        string currentCommand,
        TimeSpan? timeSinceLastCommand = null,
        int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(currentCommand))
            return new List<WorkflowSuggestion>();

        // Normalize command
        currentCommand = NormalizeCommand(currentCommand);

        // Fast cache lookup
        if (!_workflowGraph.TryGetValue(currentCommand, out var transitions))
            return new List<WorkflowSuggestion>();

        // Calculate scores for each potential next command
        var suggestions = transitions.Values
            .Select(t =>
            {
                double score;
                if (timeSinceLastCommand.HasValue)
                {
                    // Use time-sensitive scoring if timing information available
                    score = t.GetTimeSensitiveScore(timeSinceLastCommand.Value, _minFrequency, _decayDays);
                }
                else
                {
                    // Use basic confidence scoring
                    score = t.GetConfidence(_minFrequency, _decayDays);
                }

                return new WorkflowSuggestion
                {
                    Command = t.NextCommand,
                    Confidence = score,
                    Source = "Workflow",
                    Reason = $"Used {t.Frequency}x after '{currentCommand}' (avg {t.AverageTimeDelta.TotalSeconds:F0}s apart)"
                };
            })
            .Where(s => s.Confidence >= _minConfidence)
            .OrderByDescending(s => s.Confidence)
            .Take(maxResults)
            .ToList();

        return suggestions;
    }

    /// <summary>
    /// Gets the delta (new transitions since last save) for persistence.
    /// </summary>
    public Dictionary<string, Dictionary<string, WorkflowTransition>> GetDelta()
    {
        var result = new Dictionary<string, Dictionary<string, WorkflowTransition>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fromCommand, transitions) in _delta)
        {
            result[fromCommand] = new Dictionary<string, WorkflowTransition>(StringComparer.OrdinalIgnoreCase);
            foreach (var (toCommand, transition) in transitions)
            {
                result[fromCommand][toCommand] = transition;
            }
        }

        return result;
    }

    /// <summary>
    /// Clears the delta after successful persistence.
    /// </summary>
    public void ClearDelta()
    {
        _delta.Clear();
    }

    /// <summary>
    /// Gets diagnostic information about the workflow learner state.
    /// </summary>
    public (int totalTransitions, int uniqueCommands, int deltaTransitions) GetDiagnostics()
    {
        var totalTransitions = _workflowGraph.Sum(kv => kv.Value.Count);
        var uniqueCommands = _workflowGraph.Count;
        var deltaTransitions = _delta.Sum(kv => kv.Value.Count);

        return (totalTransitions, uniqueCommands, deltaTransitions);
    }

    /// <summary>
    /// Gets all workflow transitions (for export/inspection).
    /// </summary>
    public Dictionary<string, List<WorkflowTransition>> GetAllWorkflows()
    {
        var result = new Dictionary<string, List<WorkflowTransition>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fromCommand, transitions) in _workflowGraph)
        {
            result[fromCommand] = transitions.Values
                .OrderByDescending(t => t.Frequency)
                .ToList();
        }

        return result;
    }

    /// <summary>
    /// Clears all workflow data (memory only, not database).
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _workflowGraph.Clear();
            _delta.Clear();
        }
    }

    /// <summary>
    /// Normalizes a command by extracting the base command.
    /// Examples: "git add ." → "git add", "cargo build --release" → "cargo build"
    /// </summary>
    private string NormalizeCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return string.Empty;

        var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return string.Empty;

        if (parts.Length == 1)
            return parts[0];

        // For commands with subcommands (git, docker, kubectl, etc.), return "command subcommand"
        // Add new commands here when they use subcommand pattern (e.g., "kubectl get", "docker run")
        // This list should match commands that PSCue provides explicit completions for
        var multiPartCommands = new[] { "git", "docker", "kubectl", "npm", "dotnet", "cargo", "gh", "az", "func", "scoop" };

        if (multiPartCommands.Contains(parts[0], StringComparer.OrdinalIgnoreCase) && parts.Length >= 2)
        {
            // Skip flags when extracting subcommand
            for (int i = 1; i < parts.Length; i++)
            {
                if (!parts[i].StartsWith("-"))
                {
                    return $"{parts[0]} {parts[i]}";
                }
            }

            // All remaining parts are flags, just return base command
            return parts[0];
        }

        // For simple commands, return just the command
        return parts[0];
    }

    /// <summary>
    /// Enforces memory limits by keeping only the top N transitions per command (LRU eviction).
    /// </summary>
    private void EnforceLimits(ConcurrentDictionary<string, WorkflowTransition> transitions)
    {
        if (transitions.Count <= MaxTransitionsPerCommand)
            return;

        // Keep only top N by frequency (oldest/least-frequent evicted)
        var toKeep = transitions.Values
            .OrderByDescending(t => t.Frequency)
            .ThenByDescending(t => t.LastSeen)
            .Take(MaxTransitionsPerCommand)
            .Select(t => t.NextCommand)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toRemove = transitions.Keys
            .Where(k => !toKeep.Contains(k))
            .ToList();

        foreach (var key in toRemove)
        {
            transitions.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}

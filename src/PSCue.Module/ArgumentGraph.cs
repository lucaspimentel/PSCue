using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace PSCue.Module;

/// <summary>
/// Statistics about a specific argument's usage with a command.
/// </summary>
public class ArgumentStats
{
    /// <summary>
    /// The argument text (e.g., "-m", "--message", "commit").
    /// </summary>
    public string Argument { get; set; } = string.Empty;

    /// <summary>
    /// How many times this argument has been used.
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// When this argument was first observed.
    /// </summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>
    /// When this argument was last used.
    /// </summary>
    public DateTime LastUsed { get; set; }

    /// <summary>
    /// Whether this is a flag (starts with - or --).
    /// </summary>
    public bool IsFlag { get; set; }

    /// <summary>
    /// Other arguments that commonly appear with this one.
    /// Maps argument -> co-occurrence count.
    /// </summary>
    public ConcurrentDictionary<string, int> CoOccurrences { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Calculates a recency-weighted score (0.0-1.0).
    /// More recent usage gets higher scores.
    /// </summary>
    public double GetRecencyScore(int decayDays = 30)
    {
        var age = DateTime.UtcNow - LastUsed;
        var ageDays = age.TotalDays;

        if (ageDays <= 0)
            return 1.0; // Used today

        // Exponential decay: score = e^(-age/decay)
        // After decayDays, score is ~0.37
        // After 2*decayDays, score is ~0.14
        var decayFactor = Math.Exp(-ageDays / decayDays);
        return Math.Max(0.0, Math.Min(1.0, decayFactor));
    }

    /// <summary>
    /// Calculates a combined score based on frequency and recency.
    /// </summary>
    public double GetScore(int totalUsageCount, int decayDays = 30)
    {
        if (totalUsageCount == 0)
            return 0.0;

        // Frequency score (0.0-1.0)
        var frequencyScore = (double)UsageCount / totalUsageCount;

        // Recency score (0.0-1.0)
        var recencyScore = GetRecencyScore(decayDays);

        // Weighted combination: 60% frequency, 40% recency
        return (0.6 * frequencyScore) + (0.4 * recencyScore);
    }
}

/// <summary>
/// Knowledge about a specific command's argument patterns.
/// </summary>
public class CommandKnowledge
{
    /// <summary>
    /// The command name (e.g., "git", "docker").
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Arguments and their usage statistics.
    /// </summary>
    public ConcurrentDictionary<string, ArgumentStats> Arguments { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Common flag combinations (e.g., "-la" used together).
    /// Maps combined flags string -> usage count.
    /// </summary>
    public ConcurrentDictionary<string, int> FlagCombinations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Total number of times this command has been observed.
    /// </summary>
    public int TotalUsageCount { get; set; }

    /// <summary>
    /// When this command was first observed.
    /// </summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>
    /// When this command was last used.
    /// </summary>
    public DateTime LastUsed { get; set; }
}

/// <summary>
/// Tracks argument patterns across all commands.
/// Learns which arguments/flags are commonly used with each command.
/// Thread-safe for concurrent access.
/// </summary>
public class ArgumentGraph
{
    private readonly ConcurrentDictionary<string, CommandKnowledge> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxArgumentsPerCommand;
    private readonly int _maxCommands;
    private readonly int _scoreDecayDays;

    /// <summary>
    /// Creates a new ArgumentGraph.
    /// </summary>
    /// <param name="maxCommands">Maximum number of commands to track (default: 500).</param>
    /// <param name="maxArgumentsPerCommand">Maximum arguments per command (default: 100).</param>
    /// <param name="scoreDecayDays">Days for score decay (default: 30).</param>
    public ArgumentGraph(int maxCommands = 500, int maxArgumentsPerCommand = 100, int scoreDecayDays = 30)
    {
        _maxCommands = maxCommands;
        _maxArgumentsPerCommand = maxArgumentsPerCommand;
        _scoreDecayDays = scoreDecayDays;
    }

    /// <summary>
    /// Records a command execution with its arguments.
    /// Updates usage counts, co-occurrence data, and timestamps.
    /// </summary>
    public void RecordUsage(string command, string[] arguments)
    {
        if (string.IsNullOrWhiteSpace(command) || arguments == null || arguments.Length == 0)
            return;

        var now = DateTime.UtcNow;

        // Get or create command knowledge
        var knowledge = _commands.GetOrAdd(command, cmd => new CommandKnowledge
        {
            Command = cmd,
            FirstSeen = now,
            LastUsed = now
        });

        knowledge.LastUsed = now;
        knowledge.TotalUsageCount++;

        // Track each argument
        foreach (var arg in arguments)
        {
            if (string.IsNullOrWhiteSpace(arg))
                continue;

            var stats = knowledge.Arguments.GetOrAdd(arg, a => new ArgumentStats
            {
                Argument = a,
                FirstSeen = now,
                IsFlag = a.StartsWith("-")
            });

            stats.UsageCount++;
            stats.LastUsed = now;

            // Track co-occurrences with other arguments
            foreach (var otherArg in arguments)
            {
                if (string.IsNullOrWhiteSpace(otherArg) || string.Equals(arg, otherArg, StringComparison.OrdinalIgnoreCase))
                    continue;

                stats.CoOccurrences.AddOrUpdate(otherArg, 1, (_, count) => count + 1);
            }
        }

        // Track flag combinations (consecutive flags)
        var flags = arguments.Where(a => a.StartsWith("-")).ToList();
        if (flags.Count > 1)
        {
            var combination = string.Join(" ", flags);
            knowledge.FlagCombinations.AddOrUpdate(combination, 1, (_, count) => count + 1);
        }

        // Enforce limits
        EnforceLimits(knowledge);
        EnforceCommandLimit();
    }

    /// <summary>
    /// Gets suggested arguments for a command, sorted by score (highest first).
    /// </summary>
    public List<ArgumentStats> GetSuggestions(string command, string[] alreadyTypedArgs, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(command))
            return new List<ArgumentStats>();

        if (!_commands.TryGetValue(command, out var knowledge))
            return new List<ArgumentStats>();

        var alreadyTyped = new HashSet<string>(alreadyTypedArgs ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        return knowledge.Arguments.Values
            .Where(stats => !alreadyTyped.Contains(stats.Argument))
            .Select(stats => new
            {
                Stats = stats,
                Score = stats.GetScore(knowledge.TotalUsageCount, _scoreDecayDays)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Stats.UsageCount)
            .Take(maxResults)
            .Select(x => x.Stats)
            .ToList();
    }

    /// <summary>
    /// Gets knowledge about a specific command (if tracked).
    /// </summary>
    public CommandKnowledge? GetCommandKnowledge(string command)
    {
        return _commands.TryGetValue(command, out var knowledge) ? knowledge : null;
    }

    /// <summary>
    /// Gets all tracked commands.
    /// </summary>
    public IReadOnlyList<string> GetTrackedCommands()
    {
        return _commands.Keys.ToList();
    }

    /// <summary>
    /// Clears all learned data.
    /// </summary>
    public void Clear()
    {
        _commands.Clear();
    }

    /// <summary>
    /// Gets statistics about the argument graph.
    /// </summary>
    public ArgumentGraphStatistics GetStatistics()
    {
        var stats = new ArgumentGraphStatistics
        {
            TotalCommands = _commands.Count,
            MaxCommands = _maxCommands,
            MaxArgumentsPerCommand = _maxArgumentsPerCommand
        };

        if (_commands.Count == 0)
            return stats;

        stats.TotalArguments = _commands.Values.Sum(k => k.Arguments.Count);
        stats.TotalUsageCount = _commands.Values.Sum(k => k.TotalUsageCount);

        var mostUsed = _commands.Values.OrderByDescending(k => k.TotalUsageCount).First();
        stats.MostUsedCommand = mostUsed.Command;
        stats.MostUsedCommandCount = mostUsed.TotalUsageCount;

        return stats;
    }

    /// <summary>
    /// Removes least recently used arguments if over limit.
    /// </summary>
    private void EnforceLimits(CommandKnowledge knowledge)
    {
        if (knowledge.Arguments.Count <= _maxArgumentsPerCommand)
            return;

        // Remove least recently used arguments
        var toRemove = knowledge.Arguments.Values
            .OrderBy(a => a.LastUsed)
            .Take(knowledge.Arguments.Count - _maxArgumentsPerCommand)
            .Select(a => a.Argument)
            .ToList();

        foreach (var arg in toRemove)
        {
            knowledge.Arguments.TryRemove(arg, out _);
        }
    }

    /// <summary>
    /// Removes least recently used commands if over limit.
    /// </summary>
    private void EnforceCommandLimit()
    {
        if (_commands.Count <= _maxCommands)
            return;

        // Remove least recently used commands
        var toRemove = _commands.Values
            .OrderBy(k => k.LastUsed)
            .Take(_commands.Count - _maxCommands)
            .Select(k => k.Command)
            .ToList();

        foreach (var cmd in toRemove)
        {
            _commands.TryRemove(cmd, out _);
        }
    }
}

/// <summary>
/// Statistics about the argument graph.
/// </summary>
public class ArgumentGraphStatistics
{
    public int TotalCommands { get; set; }
    public int TotalArguments { get; set; }
    public int TotalUsageCount { get; set; }
    public int MaxCommands { get; set; }
    public int MaxArgumentsPerCommand { get; set; }
    public string? MostUsedCommand { get; set; }
    public int MostUsedCommandCount { get; set; }
}

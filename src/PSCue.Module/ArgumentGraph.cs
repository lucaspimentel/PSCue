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

    // Baseline tracking for delta calculation (concurrent session support)
    // Maps: command -> (totalCount, arg -> argCount)
    private readonly ConcurrentDictionary<string, (int TotalCount, ConcurrentDictionary<string, int> ArgCounts)> _baseline
        = new(StringComparer.OrdinalIgnoreCase);

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
    /// <param name="command">The command name (e.g., "git", "cd").</param>
    /// <param name="arguments">The command arguments.</param>
    /// <param name="workingDirectory">The working directory when command was executed (optional, for path normalization).</param>
    public void RecordUsage(string command, string[] arguments, string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(command) || arguments == null || arguments.Length == 0)
            return;

        var now = DateTime.UtcNow;

        // Check if this is a navigation command that needs path normalization
        var isNavigationCommand = command.Equals("cd", StringComparison.OrdinalIgnoreCase)
            || command.Equals("Set-Location", StringComparison.OrdinalIgnoreCase)
            || command.Equals("sl", StringComparison.OrdinalIgnoreCase)
            || command.Equals("chdir", StringComparison.OrdinalIgnoreCase);

        // Normalize paths for navigation commands
        if (isNavigationCommand && arguments.Length > 0 && !string.IsNullOrWhiteSpace(workingDirectory))
        {
            arguments = NormalizeNavigationPaths(arguments, workingDirectory);
        }

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
    /// Normalizes paths in navigation command arguments to absolute paths.
    /// This allows learning to work across different contexts (e.g., "cd ~/projects" and "cd ../projects" both normalize to the same path).
    /// </summary>
    private string[] NormalizeNavigationPaths(string[] arguments, string workingDirectory)
    {
        var normalized = new string[arguments.Length];

        for (int i = 0; i < arguments.Length; i++)
        {
            var arg = arguments[i];

            // Skip flags and empty arguments
            if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith("-"))
            {
                normalized[i] = arg;
                continue;
            }

            // Try to normalize the path
            var normalizedPath = NormalizePath(arg, workingDirectory);
            normalized[i] = normalizedPath ?? arg; // Fall back to original if normalization fails
        }

        return normalized;
    }

    /// <summary>
    /// Normalizes a single path to absolute form.
    /// Handles ~, relative paths (., .., no prefix), and absolute paths.
    /// Returns null if the path is invalid.
    /// </summary>
    private static string? NormalizePath(string path, string workingDirectory)
    {
        try
        {
            // Expand ~ to home directory
            if (path.StartsWith("~/") || path == "~")
            {
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(homeDir))
                    return null;

                path = path == "~" ? homeDir : path.Replace("~", homeDir);
            }

            // If already absolute, just clean it up
            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            // Relative path - resolve against working directory
            var combined = Path.Combine(workingDirectory, path);
            return Path.GetFullPath(combined);
        }
        catch
        {
            // Path normalization failed - return null
            return null;
        }
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
    /// Gets all commands with their knowledge (for persistence and PowerShell functions).
    /// </summary>
    public IEnumerable<KeyValuePair<string, CommandKnowledge>> GetAllCommands()
    {
        return _commands;
    }

    /// <summary>
    /// Initializes a command with persisted metadata (used by PersistenceManager).
    /// </summary>
    internal void InitializeCommand(string command, int totalUsage, DateTime firstSeen, DateTime lastUsed)
    {
        var knowledge = _commands.GetOrAdd(command, cmd => new CommandKnowledge
        {
            Command = cmd,
            FirstSeen = firstSeen,
            LastUsed = lastUsed,
            TotalUsageCount = totalUsage
        });

        // Update if already exists (shouldn't happen, but handle gracefully)
        if (knowledge.TotalUsageCount == 0)
        {
            knowledge.TotalUsageCount = totalUsage;
            knowledge.FirstSeen = firstSeen;
            knowledge.LastUsed = lastUsed;
        }

        // Record baseline for delta tracking
        _baseline.TryAdd(command, (totalUsage, new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Initializes an argument with persisted stats (used by PersistenceManager).
    /// </summary>
    internal void InitializeArgument(string command, string argument, int usageCount, DateTime firstSeen, DateTime lastUsed, bool isFlag)
    {
        if (!_commands.TryGetValue(command, out var knowledge))
            return;

        var stats = knowledge.Arguments.GetOrAdd(argument, arg => new ArgumentStats
        {
            Argument = arg,
            UsageCount = usageCount,
            FirstSeen = firstSeen,
            LastUsed = lastUsed,
            IsFlag = isFlag
        });

        // Update if already exists (shouldn't happen, but handle gracefully)
        if (stats.UsageCount == 0)
        {
            stats.UsageCount = usageCount;
            stats.FirstSeen = firstSeen;
            stats.LastUsed = lastUsed;
            stats.IsFlag = isFlag;
        }

        // Record baseline for delta tracking
        if (_baseline.TryGetValue(command, out var baseline))
        {
            baseline.ArgCounts.TryAdd(argument, usageCount);
        }
    }

    /// <summary>
    /// Initializes a co-occurrence relationship (used by PersistenceManager).
    /// </summary>
    internal void InitializeCoOccurrence(string command, string argument, string coOccurredWith, int count)
    {
        if (!_commands.TryGetValue(command, out var knowledge))
            return;

        if (!knowledge.Arguments.TryGetValue(argument, out var stats))
            return;

        stats.CoOccurrences.TryAdd(coOccurredWith, count);
    }

    /// <summary>
    /// Initializes a flag combination (used by PersistenceManager).
    /// </summary>
    internal void InitializeFlagCombination(string command, string flags, int count)
    {
        if (!_commands.TryGetValue(command, out var knowledge))
            return;

        knowledge.FlagCombinations.TryAdd(flags, count);
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

    /// <summary>
    /// Gets the delta (new usage since load) for a command.
    /// Returns 0 if command wasn't in baseline (newly learned).
    /// </summary>
    internal int GetCommandDelta(string command, int currentTotal)
    {
        if (_baseline.TryGetValue(command, out var baseline))
        {
            return Math.Max(0, currentTotal - baseline.TotalCount);
        }
        return currentTotal; // Newly learned command, delta = all usage
    }

    /// <summary>
    /// Gets the delta (new usage since load) for an argument.
    /// Returns 0 if argument wasn't in baseline (newly learned).
    /// </summary>
    internal int GetArgumentDelta(string command, string argument, int currentCount)
    {
        if (_baseline.TryGetValue(command, out var baseline) &&
            baseline.ArgCounts.TryGetValue(argument, out var baselineCount))
        {
            return Math.Max(0, currentCount - baselineCount);
        }
        return currentCount; // Newly learned argument, delta = all usage
    }

    /// <summary>
    /// Updates the baseline after a successful save.
    /// Call this after persisting to database to reset deltas.
    /// </summary>
    internal void UpdateBaseline()
    {
        foreach (var cmdKv in _commands)
        {
            var command = cmdKv.Key;
            var knowledge = cmdKv.Value;

            // Update command baseline
            var argCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var argKv in knowledge.Arguments)
            {
                argCounts[argKv.Key] = argKv.Value.UsageCount;
            }

            _baseline[command] = (knowledge.TotalUsageCount, argCounts);
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

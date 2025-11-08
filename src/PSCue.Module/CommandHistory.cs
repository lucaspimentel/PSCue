using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace PSCue.Module;

/// <summary>
/// Represents a single command execution entry in the history.
/// </summary>
public class CommandHistoryEntry
{
    /// <summary>
    /// The command name (e.g., "git", "docker", "kubectl").
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// The full command line as executed.
    /// </summary>
    public string CommandLine { get; set; } = string.Empty;

    /// <summary>
    /// Arguments and flags (e.g., ["commit", "-m", "message"]).
    /// </summary>
    public string[] Arguments { get; set; } = Array.Empty<string>();

    /// <summary>
    /// When this command was executed (always UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Whether the command executed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Working directory when command was executed (optional, for context).
    /// </summary>
    public string? WorkingDirectory { get; set; }
}

/// <summary>
/// Thread-safe ring buffer for tracking recent command history.
/// Used by the generic learning system to understand user patterns.
/// </summary>
public class CommandHistory
{
    private readonly int _maxSize;
    private readonly object _lock = new();
    private readonly Queue<CommandHistoryEntry> _entries;

    /// <summary>
    /// Creates a new CommandHistory with the specified maximum size.
    /// </summary>
    /// <param name="maxSize">Maximum number of commands to keep in history (default: 100).</param>
    public CommandHistory(int maxSize = 100)
    {
        if (maxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be positive");

        _maxSize = maxSize;
        _entries = new Queue<CommandHistoryEntry>(maxSize);
    }

    /// <summary>
    /// Adds a command execution to the history.
    /// Thread-safe. If at capacity, removes oldest entry.
    /// </summary>
    public void Add(CommandHistoryEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        lock (_lock)
        {
            // If at capacity, remove oldest entry
            if (_entries.Count >= _maxSize)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }
    }

    /// <summary>
    /// Adds a command execution to the history (convenience overload).
    /// </summary>
    public void Add(string command, string commandLine, string[] arguments, bool success, string? workingDirectory = null)
    {
        Add(new CommandHistoryEntry
        {
            Command = command,
            CommandLine = commandLine,
            Arguments = arguments,
            Timestamp = DateTime.UtcNow,
            Success = success,
            WorkingDirectory = workingDirectory
        });
    }

    /// <summary>
    /// Adds a command execution to the history with a custom timestamp (for import).
    /// </summary>
    public void AddEntry(string command, string[] arguments, bool success, DateTime timestamp, string? workingDirectory = null)
    {
        Add(new CommandHistoryEntry
        {
            Command = command,
            CommandLine = $"{command} {string.Join(" ", arguments)}",
            Arguments = arguments,
            Timestamp = timestamp,
            Success = success,
            WorkingDirectory = workingDirectory
        });
    }

    /// <summary>
    /// Gets recent history entries, most recent first.
    /// Thread-safe. Returns a snapshot copy.
    /// </summary>
    /// <param name="count">Maximum number of entries to return (default: all).</param>
    public IReadOnlyList<CommandHistoryEntry> GetRecent(int? count = null)
    {
        lock (_lock)
        {
            var entries = _entries.Reverse();

            if (count.HasValue)
            {
                entries = entries.Take(count.Value);
            }

            return entries.ToList();
        }
    }

    /// <summary>
    /// Gets entries for a specific command, most recent first.
    /// </summary>
    public IReadOnlyList<CommandHistoryEntry> GetForCommand(string command, int? count = null)
    {
        if (string.IsNullOrWhiteSpace(command))
            return Array.Empty<CommandHistoryEntry>();

        lock (_lock)
        {
            var entries = _entries
                .Reverse()
                .Where(e => string.Equals(e.Command, command, StringComparison.OrdinalIgnoreCase));

            if (count.HasValue)
            {
                entries = entries.Take(count.Value);
            }

            return entries.ToList();
        }
    }

    /// <summary>
    /// Gets the most recent command executed (if any).
    /// </summary>
    public CommandHistoryEntry? GetMostRecent()
    {
        lock (_lock)
        {
            return _entries.Count > 0 ? _entries.Last() : null;
        }
    }

    /// <summary>
    /// Gets the N most recent commands executed.
    /// </summary>
    public IReadOnlyList<CommandHistoryEntry> GetMostRecent(int count)
    {
        if (count <= 0)
            return Array.Empty<CommandHistoryEntry>();

        lock (_lock)
        {
            return _entries
                .Reverse()
                .Take(count)
                .ToList();
        }
    }

    /// <summary>
    /// Clears all history.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    /// <summary>
    /// Gets the current number of entries in history.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Gets statistics about the command history.
    /// </summary>
    public HistoryStatistics GetStatistics()
    {
        lock (_lock)
        {
            var stats = new HistoryStatistics
            {
                TotalCommands = _entries.Count,
                MaxSize = _maxSize
            };

            if (_entries.Count == 0)
                return stats;

            stats.OldestEntry = _entries.First().Timestamp;
            stats.NewestEntry = _entries.Last().Timestamp;
            stats.SuccessCount = _entries.Count(e => e.Success);
            stats.FailureCount = _entries.Count(e => !e.Success);

            // Count unique commands
            stats.UniqueCommands = _entries
                .Select(e => e.Command)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            // Find most common command
            var commandGroups = _entries
                .GroupBy(e => e.Command, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (commandGroups != null)
            {
                stats.MostCommonCommand = commandGroups.Key;
                stats.MostCommonCommandCount = commandGroups.Count();
            }

            return stats;
        }
    }
}

/// <summary>
/// Statistics about command history.
/// </summary>
public class HistoryStatistics
{
    public int TotalCommands { get; set; }
    public int MaxSize { get; set; }
    public int UniqueCommands { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime? OldestEntry { get; set; }
    public DateTime? NewestEntry { get; set; }
    public string? MostCommonCommand { get; set; }
    public int MostCommonCommandCount { get; set; }

    public double SuccessRate => TotalCommands > 0 ? (double)SuccessCount / TotalCommands : 0.0;
}

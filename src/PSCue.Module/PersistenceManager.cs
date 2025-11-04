using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace PSCue.Module;

/// <summary>
/// Manages cross-session persistence of learned data using SQLite.
/// Handles concurrent access from multiple PowerShell sessions safely.
/// Uses additive merging: frequencies are summed, timestamps use max.
/// </summary>
public class PersistenceManager : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private bool _disposed;

    /// <summary>
    /// Creates a new PersistenceManager.
    /// </summary>
    /// <param name="dbPath">Path to SQLite database file. If null, uses default location.</param>
    public PersistenceManager(string? dbPath = null)
    {
        // Default location: ~/.local/share/PSCue/learned-data.db
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            var dataDir = GetDataDirectory();
            Directory.CreateDirectory(dataDir);
            _dbPath = Path.Combine(dataDir, "learned-data.db");
        }
        else
        {
            _dbPath = dbPath;
            var directory = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        // Enable WAL mode for better concurrency (multiple readers + one writer)
        _connectionString = $"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared";

        // Initialize database schema
        InitializeDatabase();
    }

    /// <summary>
    /// Gets the default data directory for PSCue.
    /// </summary>
    private static string GetDataDirectory()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "PSCue");
        }
        else
        {
            // Linux/macOS: use XDG_DATA_HOME or ~/.local/share
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            var dataHome = string.IsNullOrEmpty(xdgDataHome)
                ? Path.Combine(homeDir, ".local", "share")
                : xdgDataHome;
            return Path.Combine(dataHome, "PSCue");
        }
    }

    /// <summary>
    /// Create and configure a new database connection.
    /// </summary>
    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Set busy timeout to handle concurrent access (wait up to 5 seconds)
        using (var timeoutCommand = connection.CreateCommand())
        {
            timeoutCommand.CommandText = "PRAGMA busy_timeout=5000;";
            timeoutCommand.ExecuteNonQuery();
        }

        return connection;
    }

    /// <summary>
    /// Initializes the database schema if it doesn't exist.
    /// </summary>
    private void InitializeDatabase()
    {
        using var connection = CreateConnection();

        // Enable WAL mode for better concurrency
        using (var walCommand = connection.CreateCommand())
        {
            walCommand.CommandText = "PRAGMA journal_mode=WAL;";
            walCommand.ExecuteNonQuery();
        }

        // Create tables
        using var command = connection.CreateCommand();
        command.CommandText = @"
            -- Commands table: tracks command-level statistics
            CREATE TABLE IF NOT EXISTS commands (
                command TEXT PRIMARY KEY COLLATE NOCASE,
                total_usage_count INTEGER NOT NULL DEFAULT 0,
                first_seen TEXT NOT NULL,
                last_used TEXT NOT NULL
            );

            -- Arguments table: tracks argument usage per command
            CREATE TABLE IF NOT EXISTS arguments (
                command TEXT NOT NULL COLLATE NOCASE,
                argument TEXT NOT NULL COLLATE NOCASE,
                usage_count INTEGER NOT NULL DEFAULT 0,
                first_seen TEXT NOT NULL,
                last_used TEXT NOT NULL,
                is_flag INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (command, argument)
            );

            -- Co-occurrences table: tracks which arguments appear together
            CREATE TABLE IF NOT EXISTS co_occurrences (
                command TEXT NOT NULL COLLATE NOCASE,
                argument TEXT NOT NULL COLLATE NOCASE,
                co_occurred_with TEXT NOT NULL COLLATE NOCASE,
                count INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (command, argument, co_occurred_with)
            );

            -- Flag combinations table: tracks common flag combinations
            CREATE TABLE IF NOT EXISTS flag_combinations (
                command TEXT NOT NULL COLLATE NOCASE,
                flags TEXT NOT NULL COLLATE NOCASE,
                count INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (command, flags)
            );

            -- Command history table: recent command executions (ring buffer)
            CREATE TABLE IF NOT EXISTS command_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                command TEXT NOT NULL COLLATE NOCASE,
                command_line TEXT NOT NULL,
                arguments TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                success INTEGER NOT NULL DEFAULT 1,
                working_directory TEXT
            );

            -- Command sequences table: tracks command-to-command transitions (n-grams)
            CREATE TABLE IF NOT EXISTS command_sequences (
                prev_command TEXT NOT NULL COLLATE NOCASE,
                next_command TEXT NOT NULL COLLATE NOCASE,
                frequency INTEGER NOT NULL DEFAULT 0,
                last_seen TEXT NOT NULL,
                PRIMARY KEY (prev_command, next_command)
            );

            -- Indexes for performance
            CREATE INDEX IF NOT EXISTS idx_arguments_command ON arguments(command);
            CREATE INDEX IF NOT EXISTS idx_co_occurrences_command_arg ON co_occurrences(command, argument);
            CREATE INDEX IF NOT EXISTS idx_history_timestamp ON command_history(timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_sequences_prev ON command_sequences(prev_command);
        ";
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Saves the ArgumentGraph to the database using delta-based additive merging.
    /// Only saves the delta (new usage since last load/save) to support concurrent sessions.
    /// After saving, updates the baseline to prevent duplicate counting.
    /// Timestamps use min (first_seen) and max (last_used) to preserve history.
    /// </summary>
    public void SaveArgumentGraph(ArgumentGraph graph)
    {
        if (graph == null)
            throw new ArgumentNullException(nameof(graph));

        using var connection = CreateConnection();

        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var commandKv in graph.GetAllCommands())
            {
                var command = commandKv.Key;
                var knowledge = commandKv.Value;

                // Calculate delta for this command
                var delta = graph.GetCommandDelta(command, knowledge.TotalUsageCount);

                // Only save if there's a delta (skip if no new usage)
                if (delta > 0)
                {
                    // Upsert command-level stats (additive merge with delta)
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            INSERT INTO commands (command, total_usage_count, first_seen, last_used)
                            VALUES (@command, @usage, @firstSeen, @lastUsed)
                            ON CONFLICT (command) DO UPDATE SET
                                total_usage_count = total_usage_count + @usage,
                                first_seen = MIN(first_seen, @firstSeen),
                                last_used = MAX(last_used, @lastUsed)
                        ";
                        cmd.Parameters.AddWithValue("@command", command);
                        cmd.Parameters.AddWithValue("@usage", delta);
                        cmd.Parameters.AddWithValue("@firstSeen", knowledge.FirstSeen.ToString("O"));
                        cmd.Parameters.AddWithValue("@lastUsed", knowledge.LastUsed.ToString("O"));
                        cmd.ExecuteNonQuery();
                    }
                }

                // Upsert arguments (additive merge with delta)
                foreach (var argKv in knowledge.Arguments)
                {
                    var argument = argKv.Key;
                    var stats = argKv.Value;

                    // Calculate delta for this argument
                    var argDelta = graph.GetArgumentDelta(command, argument, stats.UsageCount);

                    // Only save if there's a delta (skip if no new usage)
                    if (argDelta > 0)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            INSERT INTO arguments (command, argument, usage_count, first_seen, last_used, is_flag)
                            VALUES (@command, @argument, @usage, @firstSeen, @lastUsed, @isFlag)
                            ON CONFLICT (command, argument) DO UPDATE SET
                                usage_count = usage_count + @usage,
                                first_seen = MIN(first_seen, @firstSeen),
                                last_used = MAX(last_used, @lastUsed)
                        ";
                        cmd.Parameters.AddWithValue("@command", command);
                        cmd.Parameters.AddWithValue("@argument", argument);
                        cmd.Parameters.AddWithValue("@usage", argDelta);
                        cmd.Parameters.AddWithValue("@firstSeen", stats.FirstSeen.ToString("O"));
                        cmd.Parameters.AddWithValue("@lastUsed", stats.LastUsed.ToString("O"));
                        cmd.Parameters.AddWithValue("@isFlag", stats.IsFlag ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }

                    // Upsert co-occurrences
                    foreach (var coKv in stats.CoOccurrences)
                    {
                        var coOccurredWith = coKv.Key;
                        var count = coKv.Value;

                        using var coCmd = connection.CreateCommand();
                        coCmd.Transaction = transaction;
                        coCmd.CommandText = @"
                            INSERT INTO co_occurrences (command, argument, co_occurred_with, count)
                            VALUES (@command, @argument, @coWith, @count)
                            ON CONFLICT (command, argument, co_occurred_with) DO UPDATE SET
                                count = @count
                        ";
                        coCmd.Parameters.AddWithValue("@command", command);
                        coCmd.Parameters.AddWithValue("@argument", argument);
                        coCmd.Parameters.AddWithValue("@coWith", coOccurredWith);
                        coCmd.Parameters.AddWithValue("@count", count);
                        coCmd.ExecuteNonQuery();
                    }
                }

                // Upsert flag combinations
                foreach (var flagKv in knowledge.FlagCombinations)
                {
                    var flags = flagKv.Key;
                    var count = flagKv.Value;

                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO flag_combinations (command, flags, count)
                        VALUES (@command, @flags, @count)
                        ON CONFLICT (command, flags) DO UPDATE SET
                            count = @count
                    ";
                    cmd.Parameters.AddWithValue("@command", command);
                    cmd.Parameters.AddWithValue("@flags", flags);
                    cmd.Parameters.AddWithValue("@count", count);
                    cmd.ExecuteNonQuery();
                }
            }

            transaction.Commit();

            // Update baseline after successful save to prevent duplicate counting
            graph.UpdateBaseline();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Saves the CommandHistory to the database.
    /// Maintains ring buffer behavior: keeps only most recent N entries.
    /// </summary>
    public void SaveCommandHistory(CommandHistory history, int maxEntries = 100)
    {
        if (history == null)
            throw new ArgumentNullException(nameof(history));

        using var connection = CreateConnection();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Get recent entries (most recent first)
            var entries = history.GetRecent(maxEntries).ToList();

            // Clear old history (keep ring buffer behavior)
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM command_history";
                cmd.ExecuteNonQuery();
            }

            // Insert recent entries
            foreach (var entry in entries)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO command_history (command, command_line, arguments, timestamp, success, working_directory)
                    VALUES (@command, @cmdLine, @args, @timestamp, @success, @workDir)
                ";
                cmd.Parameters.AddWithValue("@command", entry.Command);
                cmd.Parameters.AddWithValue("@cmdLine", entry.CommandLine);
                cmd.Parameters.AddWithValue("@args", string.Join("\n", entry.Arguments));
                cmd.Parameters.AddWithValue("@timestamp", entry.Timestamp.ToString("O"));
                cmd.Parameters.AddWithValue("@success", entry.Success ? 1 : 0);
                cmd.Parameters.AddWithValue("@workDir", entry.WorkingDirectory ?? (object)DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Saves command sequences to the database using additive merging.
    /// Frequencies are incremented, and timestamps use max (most recent).
    /// </summary>
    public void SaveCommandSequences(Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>> sequences)
    {
        if (sequences == null)
            throw new ArgumentNullException(nameof(sequences));

        using var connection = CreateConnection();

        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var (prevCommand, nextCommands) in sequences)
            {
                foreach (var (nextCommand, data) in nextCommands)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO command_sequences (prev_command, next_command, frequency, last_seen)
                        VALUES (@prevCmd, @nextCmd, @frequency, @lastSeen)
                        ON CONFLICT (prev_command, next_command) DO UPDATE SET
                            frequency = frequency + @frequency,
                            last_seen = MAX(last_seen, @lastSeen)
                    ";
                    cmd.Parameters.AddWithValue("@prevCmd", prevCommand);
                    cmd.Parameters.AddWithValue("@nextCmd", nextCommand);
                    cmd.Parameters.AddWithValue("@frequency", data.frequency);
                    cmd.Parameters.AddWithValue("@lastSeen", data.lastSeen.ToString("O"));
                    cmd.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Loads command sequences from the database.
    /// Returns a dictionary of prev_command -> (next_command -> (frequency, lastSeen)).
    /// </summary>
    public Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>> LoadCommandSequences()
    {
        var sequences = new Dictionary<string, Dictionary<string, (int frequency, DateTime lastSeen)>>(StringComparer.OrdinalIgnoreCase);

        using var connection = CreateConnection();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT prev_command, next_command, frequency, last_seen
            FROM command_sequences
        ";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var prevCommand = reader.GetString(0);
            var nextCommand = reader.GetString(1);
            var frequency = reader.GetInt32(2);
            var lastSeen = DateTime.Parse(reader.GetString(3)).ToUniversalTime();

            if (!sequences.ContainsKey(prevCommand))
            {
                sequences[prevCommand] = new Dictionary<string, (int frequency, DateTime lastSeen)>(StringComparer.OrdinalIgnoreCase);
            }

            sequences[prevCommand][nextCommand] = (frequency, lastSeen);
        }

        return sequences;
    }

    /// <summary>
    /// Loads the ArgumentGraph from the database.
    /// </summary>
    public ArgumentGraph LoadArgumentGraph(int maxCommands = 500, int maxArgumentsPerCommand = 100, int scoreDecayDays = 30)
    {
        var graph = new ArgumentGraph(maxCommands, maxArgumentsPerCommand, scoreDecayDays);

        using var connection = CreateConnection();

        // Load commands
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT command, total_usage_count, first_seen, last_used FROM commands";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var command = reader.GetString(0);
                var totalUsage = reader.GetInt32(1);
                var firstSeen = DateTime.Parse(reader.GetString(2)).ToUniversalTime();
                var lastUsed = DateTime.Parse(reader.GetString(3)).ToUniversalTime();

                // Initialize command in graph with stored metadata
                graph.InitializeCommand(command, totalUsage, firstSeen, lastUsed);
            }
        }

        // Load arguments
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT command, argument, usage_count, first_seen, last_used, is_flag
                FROM arguments
            ";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var command = reader.GetString(0);
                var argument = reader.GetString(1);
                var usageCount = reader.GetInt32(2);
                var firstSeen = DateTime.Parse(reader.GetString(3)).ToUniversalTime();
                var lastUsed = DateTime.Parse(reader.GetString(4)).ToUniversalTime();
                var isFlag = reader.GetInt32(5) == 1;

                graph.InitializeArgument(command, argument, usageCount, firstSeen, lastUsed, isFlag);
            }
        }

        // Load co-occurrences
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT command, argument, co_occurred_with, count
                FROM co_occurrences
            ";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var command = reader.GetString(0);
                var argument = reader.GetString(1);
                var coOccurredWith = reader.GetString(2);
                var count = reader.GetInt32(3);

                graph.InitializeCoOccurrence(command, argument, coOccurredWith, count);
            }
        }

        // Load flag combinations
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT command, flags, count
                FROM flag_combinations
            ";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var command = reader.GetString(0);
                var flags = reader.GetString(1);
                var count = reader.GetInt32(2);

                graph.InitializeFlagCombination(command, flags, count);
            }
        }

        return graph;
    }

    /// <summary>
    /// Loads the CommandHistory from the database.
    /// </summary>
    public CommandHistory LoadCommandHistory(int maxSize = 100)
    {
        var history = new CommandHistory(maxSize);

        using var connection = CreateConnection();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT command, command_line, arguments, timestamp, success, working_directory
            FROM command_history
            ORDER BY timestamp DESC
            LIMIT @maxSize
        ";
        cmd.Parameters.AddWithValue("@maxSize", maxSize);

        using var reader = cmd.ExecuteReader();

        var entries = new System.Collections.Generic.List<CommandHistoryEntry>();
        while (reader.Read())
        {
            var entry = new CommandHistoryEntry
            {
                Command = reader.GetString(0),
                CommandLine = reader.GetString(1),
                Arguments = reader.GetString(2).Split('\n'),
                Timestamp = DateTime.Parse(reader.GetString(3)).ToUniversalTime(),
                Success = reader.GetInt32(4) == 1,
                WorkingDirectory = reader.IsDBNull(5) ? null : reader.GetString(5)
            };
            entries.Add(entry);
        }

        // Add in reverse order (oldest first) to maintain ring buffer behavior
        entries.Reverse();
        foreach (var entry in entries)
        {
            history.Add(entry);
        }

        return history;
    }

    /// <summary>
    /// Clears all learned data from the database.
    /// </summary>
    public void Clear()
    {
        using var connection = CreateConnection();

        using var transaction = connection.BeginTransaction();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                DELETE FROM commands;
                DELETE FROM arguments;
                DELETE FROM co_occurrences;
                DELETE FROM flag_combinations;
                DELETE FROM command_history;
                DELETE FROM command_sequences;
            ";
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Exports learned data to a JSON file.
    /// </summary>
    /// <param name="path">Path to JSON file to create.</param>
    /// <param name="argumentGraph">ArgumentGraph to export.</param>
    /// <param name="commandHistory">CommandHistory to export.</param>
    public void Export(string path, ArgumentGraph argumentGraph, CommandHistory commandHistory)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Build export data structure
        var exportData = new
        {
            ExportedAt = DateTime.UtcNow,
            Version = "1.0",
            Commands = argumentGraph.GetAllCommands().Select(kvp => new
            {
                kvp.Value.Command,
                kvp.Value.TotalUsageCount,
                FirstSeen = kvp.Value.FirstSeen.ToString("o"),
                LastUsed = kvp.Value.LastUsed.ToString("o"),
                Arguments = kvp.Value.Arguments.Values.Select(arg => new
                {
                    arg.Argument,
                    arg.UsageCount,
                    FirstSeen = arg.FirstSeen.ToString("o"),
                    LastUsed = arg.LastUsed.ToString("o"),
                    arg.IsFlag,
                    CoOccurrences = arg.CoOccurrences.ToDictionary(co => co.Key, co => co.Value)
                }).ToList(),
                FlagCombinations = kvp.Value.FlagCombinations.ToDictionary(fc => fc.Key, fc => fc.Value)
            }).ToList(),
            History = commandHistory.GetRecent(100).Select(entry => new
            {
                entry.Command,
                entry.Arguments,
                Timestamp = entry.Timestamp.ToString("o"),
                entry.Success
            }).ToList()
        };

        // Write JSON
        var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Imports learned data from a JSON file.
    /// </summary>
    /// <param name="path">Path to JSON file to import.</param>
    /// <param name="argumentGraph">ArgumentGraph to import into.</param>
    /// <param name="commandHistory">CommandHistory to import into.</param>
    /// <param name="merge">If true, merge with existing data. If false, replace existing data.</param>
    public void Import(string path, ArgumentGraph argumentGraph, CommandHistory commandHistory, bool merge = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Import file not found", path);

        // Read JSON
        var json = File.ReadAllText(path);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        // If not merging, clear existing data first
        if (!merge)
        {
            Clear();
        }

        // Import commands and arguments
        if (root.TryGetProperty("Commands", out var commandsArray))
        {
            foreach (var cmdElement in commandsArray.EnumerateArray())
            {
                var command = cmdElement.GetProperty("Command").GetString();
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                var totalUsage = cmdElement.GetProperty("TotalUsageCount").GetInt32();
                var firstSeen = DateTime.Parse(cmdElement.GetProperty("FirstSeen").GetString()!);
                var lastUsed = DateTime.Parse(cmdElement.GetProperty("LastUsed").GetString()!);

                // Initialize command
                argumentGraph.InitializeCommand(command, totalUsage, firstSeen, lastUsed);

                // Import arguments
                if (cmdElement.TryGetProperty("Arguments", out var argsArray))
                {
                    foreach (var argElement in argsArray.EnumerateArray())
                    {
                        var argument = argElement.GetProperty("Argument").GetString();
                        if (string.IsNullOrWhiteSpace(argument))
                            continue;

                        var usageCount = argElement.GetProperty("UsageCount").GetInt32();
                        var argFirstSeen = DateTime.Parse(argElement.GetProperty("FirstSeen").GetString()!);
                        var argLastUsed = DateTime.Parse(argElement.GetProperty("LastUsed").GetString()!);
                        var isFlag = argElement.GetProperty("IsFlag").GetBoolean();

                        argumentGraph.InitializeArgument(command, argument, usageCount, argFirstSeen, argLastUsed, isFlag);
                    }
                }
            }
        }

        // Import command history
        if (root.TryGetProperty("History", out var historyArray))
        {
            foreach (var historyElement in historyArray.EnumerateArray())
            {
                var command = historyElement.GetProperty("Command").GetString();
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                var arguments = historyElement.GetProperty("Arguments").EnumerateArray()
                    .Select(a => a.GetString() ?? string.Empty)
                    .ToArray();
                var timestamp = DateTime.Parse(historyElement.GetProperty("Timestamp").GetString()!);
                var success = historyElement.GetProperty("Success").GetBoolean();

                commandHistory.AddEntry(command, arguments, success, timestamp);
            }
        }

        // Save imported data to database
        SaveArgumentGraph(argumentGraph);
        SaveCommandHistory(commandHistory);
    }

    /// <summary>
    /// Gets the path to the database file.
    /// </summary>
    public string DatabasePath => _dbPath;

    public void Dispose()
    {
        if (!_disposed)
        {
            // SQLite connections are pooled and disposed when closed
            // No cleanup needed here
            _disposed = true;
        }
    }
}

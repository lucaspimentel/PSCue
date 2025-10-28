using System;
using System.Collections.Generic;
using System.Linq;

namespace PSCue.Module;

/// <summary>
/// Context information extracted from recent command history.
/// Used to provide context-aware suggestions.
/// </summary>
public class CommandContext
{
    /// <summary>
    /// Recent commands that might be relevant (last N commands).
    /// </summary>
    public List<CommandHistoryEntry> RecentCommands { get; set; } = new();

    /// <summary>
    /// The most recent working directory (if available).
    /// </summary>
    public string? CurrentDirectory { get; set; }

    /// <summary>
    /// Detected command sequences (e.g., git add -> git commit -> git push).
    /// </summary>
    public List<string> DetectedSequences { get; set; } = new();

    /// <summary>
    /// Suggested next commands based on detected patterns.
    /// </summary>
    public List<string> SuggestedNextCommands { get; set; } = new();

    /// <summary>
    /// Arguments that might be relevant based on context (e.g., recent files).
    /// </summary>
    public Dictionary<string, double> ContextBoosts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Analyzes command history to extract context and detect patterns.
/// Provides context-aware suggestions based on recent activity.
/// </summary>
public class ContextAnalyzer
{
    // Common command sequences we look for
    private static readonly Dictionary<string, string[]> KnownSequences = new(StringComparer.OrdinalIgnoreCase)
    {
        // Git workflows
        ["git add"] = new[] { "git commit", "git status" },
        ["git commit"] = new[] { "git push", "git log", "git status" },
        ["git push"] = new[] { "git status", "git log" },
        ["git checkout"] = new[] { "git pull", "git status" },
        ["git pull"] = new[] { "git status", "git log" },
        ["git branch"] = new[] { "git checkout", "git switch" },
        ["git merge"] = new[] { "git push", "git log" },
        ["git stash"] = new[] { "git stash pop", "git stash list" },
        ["git clone"] = new[] { "cd" },

        // Docker workflows
        ["docker build"] = new[] { "docker run", "docker images" },
        ["docker run"] = new[] { "docker ps", "docker logs" },
        ["docker ps"] = new[] { "docker logs", "docker stop", "docker exec" },
        ["docker stop"] = new[] { "docker rm", "docker ps" },
        ["docker pull"] = new[] { "docker run", "docker images" },

        // File operations
        ["ls"] = new[] { "cd", "cat", "vim", "code" },
        ["cd"] = new[] { "ls", "pwd" },
        ["cat"] = new[] { "vim", "code", "grep" },
        ["vim"] = new[] { "git add", "cat" },
        ["mkdir"] = new[] { "cd" },

        // Build tools
        ["npm install"] = new[] { "npm start", "npm run", "npm test" },
        ["npm start"] = new[] { "npm test", "npm run" },
        ["dotnet build"] = new[] { "dotnet test", "dotnet run" },
        ["dotnet test"] = new[] { "dotnet build", "dotnet run" },
        ["cargo build"] = new[] { "cargo test", "cargo run" },
        ["cargo test"] = new[] { "cargo build" },

        // Kubernetes
        ["kubectl apply"] = new[] { "kubectl get", "kubectl describe" },
        ["kubectl get"] = new[] { "kubectl describe", "kubectl logs" },
        ["kubectl describe"] = new[] { "kubectl logs", "kubectl get" },
    };

    /// <summary>
    /// Analyzes recent command history to extract relevant context.
    /// </summary>
    public CommandContext AnalyzeContext(CommandHistory history, string currentCommand)
    {
        var context = new CommandContext();

        // Get recent commands (last 10)
        var recent = history.GetRecent(10);
        context.RecentCommands = recent.ToList();

        if (recent.Count == 0)
            return context;

        // Extract current directory from most recent command
        context.CurrentDirectory = recent.FirstOrDefault()?.WorkingDirectory;

        // Detect command sequences
        DetectSequences(recent, context);

        // Suggest next commands based on sequences
        SuggestNextCommands(recent, currentCommand, context);

        // Calculate context boosts for arguments
        CalculateContextBoosts(recent, currentCommand, context);

        return context;
    }

    /// <summary>
    /// Detects common command sequences in recent history.
    /// </summary>
    private void DetectSequences(IReadOnlyList<CommandHistoryEntry> recent, CommandContext context)
    {
        if (recent.Count < 2)
            return;

        // Look at last 5 commands for sequences
        var commands = recent.Take(5).Select(e => e.Command).ToList();

        // Check for known sequences
        for (int i = 0; i < commands.Count - 1; i++)
        {
            var current = commands[i];
            var next = commands[i + 1];
            var sequence = $"{next} -> {current}"; // Reversed because recent is newest first

            // Check if this matches a known pattern
            foreach (var (pattern, _) in KnownSequences)
            {
                if (sequence.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    context.DetectedSequences.Add(sequence);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Suggests next likely commands based on detected patterns.
    /// </summary>
    private void SuggestNextCommands(IReadOnlyList<CommandHistoryEntry> recent, string currentCommand, CommandContext context)
    {
        if (recent.Count == 0)
            return;

        var lastCommand = recent[0].CommandLine;

        // Extract just the command and first subcommand (e.g., "git add" from "git add file.txt")
        var lastCommandKey = ExtractCommandKey(lastCommand);

        // Look for known sequence patterns
        if (KnownSequences.TryGetValue(lastCommandKey, out var nextCommands))
        {
            context.SuggestedNextCommands.AddRange(nextCommands);
        }

        // If user is repeating a command, boost recently used arguments
        if (currentCommand.StartsWith(recent[0].Command, StringComparison.OrdinalIgnoreCase))
        {
            // Same command as last time - likely to use similar arguments
            context.ContextBoosts["recent_command_repeat"] = 1.3;
        }
    }

    /// <summary>
    /// Calculates context boosts for arguments based on recent activity.
    /// </summary>
    private void CalculateContextBoosts(IReadOnlyList<CommandHistoryEntry> recent, string currentCommand, CommandContext context)
    {
        // Boost arguments that were used recently (recency bias)
        var recentArgs = recent
            .Take(3)
            .SelectMany(e => e.Arguments)
            .Where(a => !string.IsNullOrWhiteSpace(a));

        foreach (var arg in recentArgs)
        {
            // Boost score for recently used arguments
            var existingBoost = context.ContextBoosts.GetValueOrDefault(arg, 1.0);
            context.ContextBoosts[arg] = Math.Max(existingBoost, 1.2);
        }

        // Boost flag combinations that were used together recently
        var recentFlags = recent
            .Take(3)
            .SelectMany(e => e.Arguments.Where(a => a.StartsWith("-")))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var flag in recentFlags)
        {
            var existingBoost = context.ContextBoosts.GetValueOrDefault(flag, 1.0);
            context.ContextBoosts[flag] = Math.Max(existingBoost, 1.15);
        }

        // Workflow-specific boosts
        ApplyWorkflowBoosts(recent, currentCommand, context);
    }

    /// <summary>
    /// Applies workflow-specific boosts based on detected patterns.
    /// </summary>
    private void ApplyWorkflowBoosts(IReadOnlyList<CommandHistoryEntry> recent, string currentCommand, CommandContext context)
    {
        if (recent.Count == 0)
            return;

        // Git workflow detection
        if (currentCommand.StartsWith("git", StringComparison.OrdinalIgnoreCase))
        {
            // If user just did "git add", boost commit-related flags
            if (recent.Any(e => e.CommandLine.StartsWith("git add", StringComparison.OrdinalIgnoreCase)))
            {
                context.ContextBoosts["-m"] = 1.5; // commit message flag
                context.ContextBoosts["--message"] = 1.5;
                context.ContextBoosts["commit"] = 1.4; // commit subcommand
            }

            // If user just did "git commit", boost push-related commands
            if (recent.Any(e => e.CommandLine.StartsWith("git commit", StringComparison.OrdinalIgnoreCase)))
            {
                context.ContextBoosts["push"] = 1.4;
                context.ContextBoosts["status"] = 1.3;
            }
        }

        // Docker workflow detection
        if (currentCommand.StartsWith("docker", StringComparison.OrdinalIgnoreCase))
        {
            // If user just built an image, boost run-related commands
            if (recent.Any(e => e.CommandLine.StartsWith("docker build", StringComparison.OrdinalIgnoreCase)))
            {
                context.ContextBoosts["run"] = 1.4;
                context.ContextBoosts["-d"] = 1.3; // detached mode
                context.ContextBoosts["-p"] = 1.3; // port mapping
            }

            // If user is looking at containers, boost logs/exec
            if (recent.Any(e => e.CommandLine.StartsWith("docker ps", StringComparison.OrdinalIgnoreCase)))
            {
                context.ContextBoosts["logs"] = 1.4;
                context.ContextBoosts["exec"] = 1.3;
                context.ContextBoosts["stop"] = 1.2;
            }
        }

        // Build tool workflow detection
        if (recent.Any(e => e.CommandLine.Contains("build", StringComparison.OrdinalIgnoreCase)))
        {
            // After build, users often test or run
            context.ContextBoosts["test"] = 1.3;
            context.ContextBoosts["run"] = 1.3;
        }
    }

    /// <summary>
    /// Extracts command key for pattern matching (e.g., "git add" from "git add file.txt").
    /// </summary>
    private string ExtractCommandKey(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return string.Empty;

        var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return string.Empty;

        if (parts.Length == 1)
            return parts[0];

        // For commands with subcommands (git, docker, kubectl, etc.), return "command subcommand"
        // For simple commands, return just the command
        var knownMultiPartCommands = new[] { "git", "docker", "kubectl", "npm", "dotnet", "cargo", "gh", "az" };

        if (knownMultiPartCommands.Contains(parts[0], StringComparer.OrdinalIgnoreCase) && parts.Length >= 2)
        {
            return $"{parts[0]} {parts[1]}";
        }

        return parts[0];
    }
}

using System.Management.Automation.Subsystem.Feedback;
using System.Management.Automation.Language;
using System.IO;

namespace PSCue.Module;

/// <summary>
/// Feedback provider that learns from command execution to improve completion suggestions.
/// Requires PowerShell 7.4+ with PSFeedbackProvider experimental feature enabled.
///
/// Enhanced with generic learning system - learns from ALL commands, not just supported ones.
///
/// Documentation:
/// https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-feedback-provider
/// </summary>
public class FeedbackProvider : IFeedbackProvider
{
    // Note: Don't store instances - get them dynamically from PSCueModule
    // This ensures we always use the current instances even after module reload
    private readonly HashSet<string> _ignorePatterns;

    /// <summary>
    /// Gets the unique identifier for this feedback provider.
    /// </summary>
    public Guid Id { get; } = new Guid("e621fe02-3c68-4e1d-9e6f-8b5c4a2d7f90");

    /// <summary>
    /// Gets the name of the feedback provider.
    /// </summary>
    public string Name => "PSCue.FeedbackProvider";

    /// <summary>
    /// Gets the description of the feedback provider.
    /// </summary>
    public string Description => "Learns from command execution to improve PSCue completion suggestions";

    /// <summary>
    /// Gets the feedback trigger type.
    /// We respond to both successful commands (for learning) and errors (for suggestions).
    /// </summary>
    public FeedbackTrigger Trigger => FeedbackTrigger.Success | FeedbackTrigger.Error;

    /// <summary>
    /// Initializes a new instance of the FeedbackProvider class.
    /// </summary>
    public FeedbackProvider()
    {
        _ignorePatterns = LoadIgnorePatterns();
    }

    /// <summary>
    /// Provides feedback based on command execution.
    /// This method is called after a command executes to learn from usage patterns (success)
    /// or provide helpful suggestions (error).
    /// </summary>
    /// <param name="context">The feedback context containing command details.</param>
    /// <param name="token">Cancellation token for the operation.</param>
    /// <returns>
    /// FeedbackItem with suggestions for the user, or null if no feedback is needed.
    /// Returns null for successful commands (silent learning).
    /// Returns suggestions for failed commands when we can help.
    /// </returns>
    public FeedbackItem? GetFeedback(FeedbackContext context, CancellationToken token)
    {
        try
        {
            // Extract command information from the context
            var commandLine = context.CommandLine;
            var ast = context.CommandLineAst;

            if (string.IsNullOrWhiteSpace(commandLine) || ast == null)
            {
                return null;
            }

            // Parse the command to extract the main command and arguments
            var commandElements = GetCommandElements(ast);
            if (commandElements.Count == 0)
            {
                return null;
            }

            var mainCommand = commandElements[0];

            // Check if command should be ignored for privacy
            if (ShouldIgnoreCommand(mainCommand, commandLine))
            {
                return null;
            }

            // Handle based on trigger type
            if (context.Trigger == FeedbackTrigger.Success)
            {
                // Learn from ALL commands (generic learning)
                LearnFromCommand(mainCommand, commandLine, commandElements, success: true);

                return null; // Silent learning
            }
            else if (context.Trigger == FeedbackTrigger.Error)
            {
                // Learn from failed commands too (marked as failed)
                LearnFromCommand(mainCommand, commandLine, commandElements, success: false);

                // Provide helpful suggestions for failed commands (only for supported commands)
                if (IsSupportedCommand(mainCommand))
                {
                    return GetErrorSuggestions(mainCommand, commandLine, commandElements, context);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            // Log errors but don't crash
            Console.Error.WriteLine($"FeedbackProvider error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts command elements from the AST.
    /// </summary>
    private static List<string> GetCommandElements(Ast ast)
    {
        var elements = new List<string>();

        // Find the command AST
        var commandAst = ast.Find(a => a is CommandAst, searchNestedScriptBlocks: false) as CommandAst;
        if (commandAst == null)
        {
            return elements;
        }

        // Extract command elements (command name + arguments)
        foreach (var element in commandAst.CommandElements)
        {
            var text = element.Extent.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                elements.Add(text);
            }
        }

        return elements;
    }

    /// <summary>
    /// Checks if the command is supported by PSCue for error suggestions.
    /// </summary>
    private static bool IsSupportedCommand(string command)
    {
        // List of commands we provide error suggestions for
        var supportedCommands = new[]
        {
            "git", "gh", "az", "azd", "func", "code",
            "scoop", "winget", "chezmoi", "tre", "lsd", "dust"
        };

        return supportedCommands.Contains(command, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Provides helpful error suggestions when a command fails.
    /// </summary>
    private FeedbackItem? GetErrorSuggestions(string mainCommand, string commandLine, List<string> commandElements, FeedbackContext context)
    {
        // Get the error message if available
        var errorMessage = context.LastError?.ErrorDetails?.Message ?? context.LastError?.Exception?.Message ?? string.Empty;

        // Common error patterns and suggestions for git
        if (mainCommand.Equals("git", StringComparison.OrdinalIgnoreCase))
        {
            return GetGitErrorSuggestions(commandLine, commandElements, errorMessage);
        }

        // Add more command-specific error handlers here as needed
        // For now, return null for other commands
        return null;
    }

    /// <summary>
    /// Provides git-specific error suggestions.
    /// </summary>
    private FeedbackItem? GetGitErrorSuggestions(string commandLine, List<string> commandElements, string errorMessage)
    {
        var suggestions = new List<string>();

        // Check for common git errors
        if (errorMessage.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("💡 Initialize a git repository: git init");
            suggestions.Add("💡 Clone an existing repository: git clone <url>");
        }
        else if (errorMessage.Contains("pathspec", StringComparison.OrdinalIgnoreCase) &&
                 errorMessage.Contains("did not match", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("💡 Check if the branch/file exists: git branch -a or git status");
            suggestions.Add("💡 Create a new branch: git checkout -b <branch-name>");
        }
        else if (errorMessage.Contains("please commit", StringComparison.OrdinalIgnoreCase) ||
                 errorMessage.Contains("uncommitted changes", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("💡 Commit your changes: git add . && git commit -m \"message\"");
            suggestions.Add("💡 Stash your changes: git stash");
            suggestions.Add("💡 Discard changes: git restore .");
        }
        else if (errorMessage.Contains("remote", StringComparison.OrdinalIgnoreCase) &&
                 errorMessage.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("💡 List remotes: git remote -v");
            suggestions.Add("💡 Add a remote: git remote add origin <url>");
        }
        else if (errorMessage.Contains("permission denied", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("💡 Check your SSH keys: ssh -T git@github.com");
            suggestions.Add("💡 Use HTTPS instead: git remote set-url origin https://...");
        }
        else if (commandElements.Count >= 2 && commandElements[1].Equals("checkout", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("💡 List all branches: git branch -a");
            suggestions.Add("💡 Create and switch to branch: git checkout -b <name>");
        }

        // If we have suggestions, return them
        if (suggestions.Count > 0)
        {
            return new FeedbackItem(
                header: "PSCue Git Suggestions",
                actions: suggestions,
                layout: FeedbackDisplayLayout.Portrait
            );
        }

        return null;
    }

    /// <summary>
    /// Loads ignore patterns from environment variable for privacy.
    /// Format: PSCUE_IGNORE_PATTERNS="aws *,*secret*,*password*"
    /// Also includes built-in patterns for common sensitive data patterns.
    /// </summary>
    private static HashSet<string> LoadIgnorePatterns()
    {
        var patterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Built-in patterns for common sensitive data
        // These are always active to protect user privacy
        var builtInPatterns = new[]
        {
            "*password*",
            "*passwd*",
            "*secret*",
            "*api*key*",      // Matches api-key, api_key, apikey, API_KEY, etc.
            "*token*",
            "*private*key*",
            "*credentials*",
            "*bearer*",
            "*oauth*"
        };

        foreach (var pattern in builtInPatterns)
        {
            patterns.Add(pattern);
        }

        // Add user-defined patterns from environment variable
        var envValue = Environment.GetEnvironmentVariable("PSCUE_IGNORE_PATTERNS");
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            foreach (var pattern in envValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    /// <summary>
    /// Checks if a command should be ignored for privacy.
    /// Detects sensitive data patterns in command names and arguments.
    /// </summary>
    private bool ShouldIgnoreCommand(string command, string commandLine)
    {
        if (_ignorePatterns.Count == 0)
            return false;

        // Check exact command name
        if (_ignorePatterns.Contains(command))
            return true;

        // Check wildcard patterns against full command line
        foreach (var pattern in _ignorePatterns)
        {
            if (pattern.Contains('*'))
            {
                // Simple wildcard matching
                var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                if (System.Text.RegularExpressions.Regex.IsMatch(commandLine, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
        }

        // Additional heuristics: Detect likely sensitive values in arguments
        // Look for long alphanumeric strings that could be API keys/tokens
        // Common patterns: 32+ chars, mix of letters/numbers/special chars
        if (ContainsSensitiveValue(commandLine))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Detects potential sensitive values like API keys or tokens in command arguments.
    /// Uses heuristics to identify long alphanumeric strings that could be secrets.
    /// </summary>
    private static bool ContainsSensitiveValue(string commandLine)
    {
        // Common patterns for API keys/tokens:
        // - Long strings (32+ chars) with mixed alphanumeric
        // - Common prefixes: sk-, pk-, ghp_, gho_, Bearer, AWS, AKIA
        // - Base64-like patterns
        // Note: We check for these outside of quoted strings to avoid false positives from commit messages

        var sensitivePatterns = new[]
        {
            @"\b(sk|pk|ghp|gho|ghs|ghr)_[A-Za-z0-9_]{10,}",   // GitHub, Stripe keys (10+ chars after prefix, allows underscores)
            @"\bAKIA[A-Z0-9]{16}",                              // AWS access keys
            @"Bearer\s+[A-Za-z0-9\-._~+/]+=*",                 // Bearer tokens (very specific pattern)
            @"\b(ey[A-Za-z0-9_-]{10,}\.){2}[A-Za-z0-9_-]{10,}", // JWT tokens (very specific pattern)
        };

        foreach (var pattern in sensitivePatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(commandLine, pattern))
            {
                return true;
            }
        }

        // For base64 and hex patterns, only match if they appear as standalone arguments (not in quotes)
        // Remove quoted content first to avoid false positives from commit messages
        var withoutQuotes = System.Text.RegularExpressions.Regex.Replace(commandLine, @"['""][^'""]*['""]", "");

        var standaloneValuePatterns = new[]
        {
            @"\b[A-Za-z0-9+/]{40,}={0,2}\b",                   // Base64-like (40+ chars, not in quotes)
            @"\b[a-f0-9]{40,}\b",                              // Hex tokens (40+ chars, not in quotes)
        };

        foreach (var pattern in standaloneValuePatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(withoutQuotes, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Learns from ANY command execution (generic learning).
    /// Updates CommandHistory, ArgumentGraph, and SequencePredictor.
    /// </summary>
    private void LearnFromCommand(string command, string commandLine, List<string> commandElements, bool success)
    {
        try
        {
            // Extract arguments (everything after the command)
            var arguments = commandElements.Skip(1).ToArray();

            // Get current instances dynamically from PSCueModule
            var commandHistory = PSCueModule.CommandHistory;
            var argumentGraph = PSCueModule.KnowledgeGraph;
            var sequencePredictor = PSCueModule.SequencePredictor;

            // Get current working directory for path normalization
            string? workingDirectory = null;
            try
            {
                workingDirectory = Directory.GetCurrentDirectory();
            }
            catch
            {
                // Ignore errors getting working directory
            }

            // Add to command history (with working directory)
            if (commandHistory != null)
            {
                commandHistory.Add(command, commandLine, arguments, success, workingDirectory);

                // Record command sequence for n-gram prediction (only for successful commands)
                if (success && sequencePredictor != null)
                {
                    var recentCommands = commandHistory.GetRecent(5).Select(e => e.Command).ToArray();
                    sequencePredictor.RecordSequence(recentCommands);
                }
            }

            // Update argument graph (only for successful commands to avoid learning bad patterns)
            if (success && argumentGraph != null && arguments.Length > 0)
            {
                argumentGraph.RecordUsage(command, arguments, workingDirectory);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in PSCue learning: {ex.Message}");
        }
    }
}

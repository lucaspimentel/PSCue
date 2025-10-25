using System.Management.Automation.Subsystem.Feedback;
using System.Management.Automation.Language;

namespace PSCue.Module;

/// <summary>
/// Feedback provider that learns from command execution to improve completion suggestions.
/// Requires PowerShell 7.4+ with PSFeedbackProvider experimental feature enabled.
///
/// Documentation:
/// https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-feedback-provider
/// </summary>
public class CommandCompleterFeedbackProvider : IFeedbackProvider
{
    private readonly Guid _guid;
    private readonly IpcServer? _ipcServer;

    /// <summary>
    /// Gets the unique identifier for this feedback provider.
    /// </summary>
    public Guid Id => _guid;

    /// <summary>
    /// Gets the name of the feedback provider.
    /// </summary>
    public string Name => "PSCue.CommandCompleterFeedbackProvider";

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
    /// Initializes a new instance of the CommandCompleterFeedbackProvider class.
    /// </summary>
    /// <param name="ipcServer">Optional IPC server instance for accessing the completion cache.</param>
    public CommandCompleterFeedbackProvider(IpcServer? ipcServer = null)
    {
        _guid = new Guid("e621fe02-3c68-4e1d-9e6f-8b5c4a2d7f90");
        _ipcServer = ipcServer;
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

            // Only process commands we provide completions for
            if (!IsSupportedCommand(mainCommand))
            {
                return null;
            }

            // Handle based on trigger type
            if (context.Trigger == FeedbackTrigger.Success)
            {
                // Learn from successful command execution
                UpdateCacheFromUsage(mainCommand, commandLine, commandElements);
                return null; // Silent learning
            }
            else if (context.Trigger == FeedbackTrigger.Error)
            {
                // Provide helpful suggestions for failed commands
                return GetErrorSuggestions(mainCommand, commandLine, commandElements, context);
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
    /// Checks if the command is supported by PSCue.
    /// </summary>
    private static bool IsSupportedCommand(string command)
    {
        // List of commands we provide completions for
        var supportedCommands = new[]
        {
            "git", "gh", "az", "azd", "func", "code",
            "scoop", "winget", "chezmoi", "tre", "lsd", "dust"
        };

        return supportedCommands.Contains(command, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Updates the completion cache based on observed command usage.
    /// </summary>
    private void UpdateCacheFromUsage(string mainCommand, string commandLine, List<string> commandElements)
    {
        if (_ipcServer == null)
        {
            return;
        }

        try
        {
            var cache = _ipcServer.GetCache();

            // Generate cache key from the command context
            // For example: "git|checkout" for "git checkout main"
            var cacheKey = CompletionCache.GetCacheKey(mainCommand, commandLine);

            // Find the specific completion item that was used
            // For example, if user typed "git checkout -b feature", we want to increment usage of "-b"
            var completionText = ExtractCompletionText(commandElements);
            if (!string.IsNullOrEmpty(completionText))
            {
                cache.IncrementUsage(cacheKey, completionText);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error updating cache from usage: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the specific completion text that was used from command elements.
    /// </summary>
    private static string? ExtractCompletionText(List<string> commandElements)
    {
        if (commandElements.Count < 2)
        {
            return null;
        }

        // Return the last meaningful element (subcommand or flag)
        // Examples:
        // - "git checkout main" → "checkout" (subcommand)
        // - "git commit -am 'msg'" → "-am" (flag)
        // - "git checkout -b feature" → "-b" (flag)

        // Look for the last flag or subcommand (skip arguments that look like values)
        for (int i = commandElements.Count - 1; i > 0; i--)
        {
            var element = commandElements[i];

            // Skip quoted strings (likely values)
            if (element.StartsWith('"') || element.StartsWith('\''))
            {
                continue;
            }

            // Return flags (start with -)
            if (element.StartsWith('-'))
            {
                return element;
            }

            // Return subcommands (at position 1, not starting with -)
            if (i == 1 && !element.StartsWith('-'))
            {
                return element;
            }
        }

        return null;
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
}

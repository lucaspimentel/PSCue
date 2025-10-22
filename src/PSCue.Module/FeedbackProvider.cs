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
    /// We respond to successful commands to learn usage patterns.
    /// </summary>
    public FeedbackTrigger Trigger => FeedbackTrigger.Success;

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
    /// This method is called after a command executes successfully to learn from usage patterns.
    /// </summary>
    /// <param name="context">The feedback context containing command details.</param>
    /// <param name="token">Cancellation token for the operation.</param>
    /// <returns>
    /// FeedbackItem with suggestions for the user, or null if no feedback is needed.
    /// For learning purposes, we typically return null (no visible feedback to user).
    /// </returns>
    public FeedbackItem? GetFeedback(FeedbackContext context, CancellationToken token)
    {
        // Only process successful command executions
        if (context.Trigger != FeedbackTrigger.Success)
        {
            return null;
        }

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

            // Only learn from commands we provide completions for
            if (!IsSupportedCommand(mainCommand))
            {
                return null;
            }

            // Update the completion cache with usage information
            UpdateCacheFromUsage(mainCommand, commandLine, commandElements);

            // Return null - we're learning silently, not providing visible feedback
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
}

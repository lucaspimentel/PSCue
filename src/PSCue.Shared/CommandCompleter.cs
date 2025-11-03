using PSCue.Shared.Completions;
using PSCue.Shared.KnownCompletions;
using PSCue.Shared.KnownCompletions.Azure;

namespace PSCue.Shared;

public static class CommandCompleter
{
    // String-based overload for PowerShell compatibility (PowerShell can't use ReadOnlySpan)
    public static IEnumerable<ICompletion> GetCompletions(string commandLine, bool includeDynamicArguments = true) =>
        GetCompletions(commandLine.AsSpan(), default, includeDynamicArguments);

    public static IEnumerable<ICompletion> GetCompletions(ReadOnlySpan<char> commandLine) =>
        GetCompletions(commandLine, default, includeDynamicArguments: true);

    public static IEnumerable<ICompletion> GetCompletions(ReadOnlySpan<char> commandLine, ReadOnlySpan<char> wordToComplete) =>
        GetCompletions(commandLine, wordToComplete, includeDynamicArguments: true);

    public static IEnumerable<ICompletion> GetCompletions(ReadOnlySpan<char> commandLine, ReadOnlySpan<char> wordToComplete, bool includeDynamicArguments)
    {
        var length = GetCommandLength(commandLine);

        if (length < 0 || length > commandLine.Length)
        {
            Logger.Write($"commandLine: {commandLine}, length: {length} is invalid");
            return [];
        }

        Span<char> mainCommand = stackalloc char[length];
        commandLine[..length].ToLowerInvariant(mainCommand);

        var isWindows = OperatingSystem.IsWindows();

        ICompletion? currentCompletion = mainCommand switch
        {
            "scoop" when isWindows => ScoopCommand.Create(),
            "winget" when isWindows => WingetCommand.Create(),
            "wt" when isWindows => WindowsTerminalCommand.Create(),
            "code" => VsCodeCommand.Create(),
            "chezmoi" => ChezmoiCommand.Create(),
            "git" => GitCommand.Create(),
            "gh" => GhCommand.Create(),
            "gt" => GtCommand.Create(),
            "claude" => ClaudeCommand.Create(),
            "tre" => TreCommand.Create(),
            "lsd" => LsdCommand.Create(),
            "dust" => DustCommand.Create(),
            "azd" => AzdCommand.Create(),
            "az" => AzCommand.Create(),
            "func" => FuncCommand.Create(),
            "cd" => SetLocationCommand.Create(),
            "set-location" => SetLocationCommand.Create(),
            "sl" => SetLocationCommand.Create(),
            "chdir" => SetLocationCommand.Create(),
            _ => null
        };

        if (currentCompletion is null)
        {
            Logger.Write($"{mainCommand} is not a known command");
            return [];
        }

        ReadOnlySpan<char> currentArgument = default;

        // Check if command line ends with a space (indicates completion of current token)
        var endsWithSpace = commandLine.Length > 0 && commandLine[^1] == ' ';

        // Collect all arguments (excluding the command itself)
        var arguments = new List<Range>();
        var argumentEnumerator = commandLine.Split(' ');
        argumentEnumerator.MoveNext(); // Skip the command
        while (argumentEnumerator.MoveNext())
        {
            arguments.Add(argumentEnumerator.Current);
        }

        // Process arguments, navigating through the completion tree
        for (int i = 0; i < arguments.Count; i++)
        {
            currentArgument = commandLine[arguments[i]].Trim();
            var isLastToken = i == arguments.Count - 1;

            if (currentCompletion.FindNode(currentArgument) is { } node)
            {
                // Check if this match is via an alias (not the primary CompletionText)
                var isAliasMatch = !Helpers.Equals(node.CompletionText, currentArgument);

                // If this is the last token, there's no trailing space, AND it's an alias match,
                // check if there are other potential matches. If so, don't navigate into it.
                // This allows prefix matching (e.g., "gt s" shows both "submit" and "sync")
                // But if the alias is unique (like "wt sp" for "split-pane"), navigate into it.
                if (isLastToken && !endsWithSpace && isAliasMatch && currentCompletion is ICompletionWithChildren cwc)
                {
                    var potentialMatches = cwc.GetCompletions(currentArgument, includeDynamicArguments: false);
                    if (potentialMatches.Count > 1)
                    {
                        // Multiple matches exist, keep currentArgument as the search term
                        break;
                    }
                }

                // If we found a parameter that doesn't have arguments, stay at the parent level
                // so we can continue to suggest other parameters
                if (node is not CommandParameter { StaticArguments.Length: 0, DynamicArguments: null })
                {
                    // For subcommands or parameters with arguments, navigate into them
                    currentCompletion = node;
                }

                currentArgument = default;
            }
            else
            {
                break;
            }
        }

        // If wordToComplete is provided and currentArgument is empty, use wordToComplete
        // This handles the case where the user has typed a partial argument (like -d)
        // that hasn't been fully parsed yet
        var searchTerm = currentArgument.IsEmpty && !wordToComplete.IsEmpty ? wordToComplete : currentArgument;

        return currentCompletion switch
        {
            ICompletionWithChildren cwc => cwc.GetCompletions(searchTerm, includeDynamicArguments).OrderBy(c => c.CompletionText),
            not null => [currentCompletion],
            null => []
        };
    }

    private static int GetCommandLength(ReadOnlySpan<char> commandLine)
    {
        var exeIndex = commandLine.IndexOf(".exe");

        if (exeIndex > 0)
        {
            return exeIndex;
        }

        var spaceIndex = commandLine.IndexOf(' ');

        if (spaceIndex > 0)
        {
            return spaceIndex;
        }

        return commandLine.Length;
    }
}

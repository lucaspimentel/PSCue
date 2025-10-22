using PSCue.Shared.Completions;
using PSCue.Shared.KnownCompletions;
using PSCue.Shared.KnownCompletions.Azure;

namespace PSCue.Shared;

public static class CommandCompleter
{
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
            "code" => VsCodeCommand.Create(),
            "chezmoi" => ChezmoiCommand.Create(),
            "git" => GitCommand.Create(),
            "gh" => GhCommand.Create(),
            "tre" => TreCommand.Create(),
            "lsd" => LsdCommand.Create(),
            "dust" => DustCommand.Create(),
            "azd" => AzdCommand.Create(),
            "az" => AzCommand.Create(),
            "func" => FuncCommand.Create(),
            _ => null
        };

        if (currentCompletion is null)
        {
            Logger.Write($"{mainCommand} is not a known command");
            return [];
        }

        ReadOnlySpan<char> currentArgument = default;
        ICompletion? parentCommand = currentCompletion;

        // Split the command line and skip the first argument
        var argumentEnumerator = commandLine.Split(' ');
        argumentEnumerator.MoveNext();

        while (argumentEnumerator.MoveNext())
        {
            currentArgument = commandLine[argumentEnumerator.Current].Trim();

            if (currentCompletion.FindNode(currentArgument) is { } node)
            {
                // If we found a parameter that doesn't have arguments, stay at the parent level
                // so we can continue to suggest other parameters
                if (node is CommandParameter { StaticArguments.Length: 0, DynamicArguments: null })
                {
                    // Track that we found the parameter, but keep currentCompletion at parent level
                    currentArgument = default;
                }
                else
                {
                    // For subcommands or parameters with arguments, navigate into them
                    currentCompletion = node;
                    currentArgument = default;

                    // Update parent reference when navigating to a new command level
                    if (node is Command)
                    {
                        parentCommand = node;
                    }
                }
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

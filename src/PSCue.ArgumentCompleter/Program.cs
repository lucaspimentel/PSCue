using PSCue.Shared;

namespace PSCue.ArgumentCompleter;

// https://learn.microsoft.com/en-us/powershell/scripting/learn/shell/tab-completion
// https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/register-argumentcompleter
// https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/tabexpansion2
// https://techcommunity.microsoft.com/blog/itopstalkblog/autocomplete-in-powershell/2604524
// https://github.com/abgox/PSCompletions/blob/main/completions/scoop/language/en-US.json

/*
Register-ArgumentCompleter -Native -CommandName $command -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)

    pwsh-argument-completer $wordToComplete, $commandAst, $cursorPosition | ForEach-Object {
        [System.Management.Automation.CompletionResult]::new(
            $_,               # completionText
            $_,               # listItemText
            'ParameterValue', # resultType
            $_                # toolTip
        )
    }
}
*/

internal static class Program
{
    public static readonly bool Debug = Environment.GetEnvironmentVariable("PSCUE_DEBUG") == "1";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (Debug)
            {
                var allArgs = string.Join(" ", args.Select(a => $"\"{a}\""));
                Logger.Write($"Received args: {allArgs}");
            }

            if (args.Length != 3)
            {
                return 0;
            }

            var wordToComplete = args[0];
            var commandAst = args[1];
            var cursorPosition = int.Parse(args[2]);

            // Convert to string before async operations (can't use ReadOnlySpan across await)
            var commandLine = cursorPosition >= 0 && cursorPosition < commandAst.Length ?
                commandAst[..cursorPosition] :
                commandAst;

            // Extract the main command for IPC request
            var firstSpaceIndex = commandLine.IndexOf(' ');
            var mainCommand = firstSpaceIndex > 0 ? commandLine[..firstSpaceIndex] : commandLine;

            // Try IPC first (fast path if CommandPredictor is loaded)
            var ipcResponse = await IpcClient.TryGetCompletionsAsync(mainCommand, commandLine, wordToComplete, cursorPosition);

            if (ipcResponse is { Completions.Length: > 0 })
            {
                // Use IPC results
                if (Debug)
                {
                    Logger.Write($"Using IPC completions (cached: {ipcResponse.Cached})");
                }

                foreach (var completion in ipcResponse.Completions)
                {
                    // completionText|toolTip
                    Output($"{completion.Text}|{completion.Description ?? completion.Text}");
                }
            }
            else
            {
                // Fallback to local completion logic
                if (Debug)
                {
                    Logger.Write("Using local completions (IPC unavailable)");
                }

                var completions = CommandCompleter.GetCompletions(commandLine.AsSpan(), wordToComplete, includeDynamicArguments: true);

                foreach (var completion in completions)
                {
                    // completionText|toolTip
                    Output($"{completion.CompletionText}|{completion.Tooltip ?? completion.CompletionText}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Logger.Write($"Error: {ex}");
            return 1;
        }
    }

    private static void Output(string text)
    {
        Console.WriteLine(text);
    }
}

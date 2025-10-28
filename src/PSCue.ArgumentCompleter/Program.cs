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

    public static int Main(string[] args)
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

            var commandLine = cursorPosition >= 0 && cursorPosition < commandAst.Length ?
                commandAst[..cursorPosition] :
                commandAst;

            // Compute completions locally with full dynamic arguments support
            // This is fast enough for Tab completion (<50ms) and ensures we always have
            // the complete set including git branches, scoop packages, etc.
            if (Debug)
            {
                Logger.Write("Computing local completions with dynamic arguments");
            }

            var completions = CommandCompleter.GetCompletions(commandLine.AsSpan(), wordToComplete, includeDynamicArguments: true);

            foreach (var completion in completions)
            {
                // completionText|toolTip
                Output($"{completion.CompletionText}|{completion.Tooltip ?? completion.CompletionText}");
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

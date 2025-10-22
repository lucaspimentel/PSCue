using System.Management.Automation.Subsystem.Prediction;
using PSCue.Module;

namespace PSCue.Cli;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: pscue-cli <command>");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  pscue-cli \"git checkout ma\"");
            Console.WriteLine("  pscue-cli \"scoop install no\"");
            Console.WriteLine("  pscue-cli \"gh pr create\"");
            return 1;
        }

        try
        {
            var predictionContext = PredictionContext.Create(args[0]);
            var predictionClient = new PredictionClient("PSCue.Cli", PredictionClientKind.Terminal);

            var predictor = new CommandCompleterPredictor();
            var suggestionPackage = predictor.GetSuggestion(predictionClient, predictionContext, CancellationToken.None);

            if (suggestionPackage.SuggestionEntries != null && suggestionPackage.SuggestionEntries.Count > 0)
            {
                foreach (var suggestion in suggestionPackage.SuggestionEntries)
                {
                    Console.WriteLine(suggestion.SuggestionText);
                }
                return 0;
            }
            else
            {
                Console.WriteLine("(no suggestions)");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}

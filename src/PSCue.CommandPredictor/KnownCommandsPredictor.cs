using System.Management.Automation.Subsystem.Prediction;

namespace PSCue.CommandPredictor;

public class KnownCommandsPredictor : ICommandPredictor
{
    /// <summary>
    /// Gets the unique identifier for a subsystem implementation.
    /// </summary>
    public Guid Id { get; } = new("ec465941-a442-4ac1-afb5-0756f7e5ebf5");

    /// <summary>
    /// Gets the name of a subsystem implementation.
    /// </summary>
    public string Name => "Lucas Known Commands";

    /// <summary>
    /// Gets the description of a subsystem implementation.
    /// </summary>
    public string Description => "A PowerShell predictor for well-known commands.";

    private string[] KnownCommands { get; } =
    [
        "dotnet build",
        "dotnet build -c release",
        "dotnet run",
        "dotnet restore",
        "dotnet new console",
        "dotnet new sln",
        "dotnet add package",
        "dotnet publish -c release",
        "dotnet pack",

        "git commit",
        "git checkout",
        "git checkout master",
        "git status",
        "git fetch --prune --prune-tags",
        "git push",
        "git pull",
        "git merge",
        "git rebase",
        "git log",
        "git branch",
        "git branch -v",
        "git branch -vv",
        "git init",

        "winget upgrade",
        "winget install",

        "scoop update && scoop status",
        "scoop update",
        "scoop update *",
        "scoop status",
        "scoop install",
    ];

    public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
    {
        var input = context.InputAst.Extent.Text;

        if (string.IsNullOrWhiteSpace(input))
        {
            return default;
        }

        var suggestions = new List<PredictiveSuggestion>();

        foreach (var command in KnownCommands)
        {
            if (command.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new PredictiveSuggestion(command));
            }
        }

        return new SuggestionPackage(suggestions);
    }
}

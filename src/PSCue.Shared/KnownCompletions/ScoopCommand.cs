// Usage: scoop <command> [<args>]
//
// Available commands are listed below.
//
// Type 'scoop help <command>' to get more help for a specific command.
//
// Command    Summary
// -------    -------
// alias      Manage scoop aliases
// bucket     Manage Scoop buckets
// cache      Show or clear the download cache
// cat        Show content of specified manifest.
// checkup    Check for potential problems
// cleanup    Cleanup apps by removing old versions
// config     Get or set configuration values
// create     Create a custom app manifest
// depends    List dependencies for an app, in the order they'll be installed
// download   Download apps in the cache folder and verify hashes
// export     Exports installed apps, buckets (and optionally configs) in JSON format
// help       Show help for a command
// hold       Hold an app to disable updates
// home       Opens the app homepage
// import     Imports apps, buckets and configs from a Scoopfile in JSON format
// info       Display information about an app
// install    Install apps
// list       List installed apps
// prefix     Returns the path to the specified app
// reset      Reset an app to resolve conflicts
// search     Search available apps
// shim       Manipulate Scoop shims
// status     Show status and check for new app versions
// unhold     Unhold an app to enable updates
// uninstall  Uninstall an app
// update     Update apps, or Scoop itself
// virustotal Look for app's hash or url on virustotal.com
// which      Locate a shim/executable (similar to 'which' on Linux)

using PSCue.Shared.Completions;

namespace PSCue.Shared.KnownCompletions;

public static class ScoopCommand
{
    private const string AppsFolderName = "apps";
    private const string DefaultRootFolderName = "scoop";

    private const string RootVariableName = "SCOOP";
    private const string GlobalRootVariableName = "SCOOP_GLOBAL";

    public static Command Create() => Create(GetInstalledPackages);

    internal static Command Create(DynamicArgumentsFactory installedPackages)
    {
        return new Command("scoop")
        {
            SubCommands =
            [
                new("alias", "Manage scoop aliases"),
                new("bucket", "Manage Scoop buckets"),
                new("cache", "Show or clear the download cache"),
                new("cat", "Show content of specified manifest."),
                new("checkup", "Check for potential problems"),
                new("cleanup", "Cleanup apps by removing old versions")
                {
                    DynamicArguments = installedPackages
                },
                new("config", "Get or set configuration values"),
                new("create", "Create a custom app manifest"),
                new("depends", "List dependencies for an app, in the order they'll be installed"),
                new("download", "Download apps in the cache folder and verify hashes"),
                new("export", "Exports installed apps, buckets (and optionally configs) in JSON format"),
                new("help", "Show help for a command"),
                new("hold", "Hold an app to disable updates")
                {
                    DynamicArguments = installedPackages
                },
                new("home", "Opens the app homepage")
                {
                    DynamicArguments = installedPackages
                },
                new("import", "Imports apps, buckets and configs from a Scoopfile in JSON format"),
                new("info", "Display information about an app")
                {
                    DynamicArguments = installedPackages
                },
                new("install", "Install apps"),
                new("list", "List installed apps"),
                new("prefix", "Returns the path to the specified app")
                {
                    DynamicArguments = installedPackages
                },
                new("reset", "Reset an app to resolve conflicts")
                {
                    DynamicArguments = installedPackages
                },
                new("search", "Search available apps"),
                new("shim", "Manipulate Scoop shims"),
                new("status", "Show status and check for new app versions")
                {
                    Parameters =
                    [
                        new("--local", "Checks the status for only the locally installed apps, and disables remote fetching/checking for Scoop and buckets (-l)") { Alias = "-l" }
                    ]
                },
                new("unhold", "Unhold an app to enable updates")
                {
                    DynamicArguments = installedPackages
                },
                new("uninstall", "Uninstall an app")
                {
                    DynamicArguments = installedPackages
                },
                new("update", "Update apps, or Scoop itself")
                {
                    Parameters = [new("*", "Update all apps")],
                    DynamicArguments = installedPackages
                },
                new("virustotal", "Look for app's hash or url on virustotal.com"),
                new("which", "Locate a shim/executable (similar to 'which' on Linux)")
            ],
        };
    }

    // Reading the apps folder directly is what keeps this lookup spawn-free. `scoop` is a
    // PowerShell script shim, so asking it to list packages costs a full pwsh startup, and
    // one-shot completion cannot afford that on every Tab press.
    private static IEnumerable<DynamicArgument> GetInstalledPackages() =>
        GetInstalledPackages(GetScoopRoot(), GetScoopGlobalRoot());

    internal static IEnumerable<DynamicArgument> GetInstalledPackages(string? root, string? globalRoot)
    {
        var packages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddPackages(packages, root);
        AddPackages(packages, globalRoot);

        return packages.Select(name => new DynamicArgument(name));
    }

    private static void AddPackages(SortedSet<string> packages, string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        var appsDirectory = Path.Combine(root, AppsFolderName);
        if (!Directory.Exists(appsDirectory))
        {
            return;
        }

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(appsDirectory))
            {
                var name = Path.GetFileName(directory);
                if (!string.IsNullOrEmpty(name))
                {
                    packages.Add(name);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A package folder we cannot read is not worth failing the whole completion for.
            Logger.Write($"Could not read '{appsDirectory}': {exception.Message}");
        }
    }

    internal static string? GetScoopRoot() =>
        ResolveRoot(Environment.GetEnvironmentVariable(RootVariableName), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    internal static string? GetScoopGlobalRoot() =>
        ResolveRoot(Environment.GetEnvironmentVariable(GlobalRootVariableName), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

    internal static string? ResolveRoot(string? rootVariable, string? defaultBaseDirectory) =>
        !string.IsNullOrWhiteSpace(rootVariable) ? rootVariable
        : string.IsNullOrWhiteSpace(defaultBaseDirectory) ? null
        : Path.Combine(defaultBaseDirectory, DefaultRootFolderName);
}

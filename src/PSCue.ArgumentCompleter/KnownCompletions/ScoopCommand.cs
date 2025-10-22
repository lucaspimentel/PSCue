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

using PSCue.ArgumentCompleter.Completions;

namespace PSCue.ArgumentCompleter.KnownCompletions;

public static class ScoopCommand
{
    public static Command Create()
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
                    DynamicArguments = GetInstalledPackages
                },
                new("config", "Get or set configuration values"),
                new("create", "Create a custom app manifest"),
                new("depends", "List dependencies for an app, in the order they'll be installed"),
                new("download", "Download apps in the cache folder and verify hashes"),
                new("export", "Exports installed apps, buckets (and optionally configs) in JSON format"),
                new("help", "Show help for a command"),
                new("hold", "Hold an app to disable updates")
                {
                    DynamicArguments = GetInstalledPackages
                },
                new("home", "Opens the app homepage")
                {
                    DynamicArguments = GetInstalledPackages
                },
                new("import", "Imports apps, buckets and configs from a Scoopfile in JSON format"),
                new("info", "Display information about an app")
                {
                    DynamicArguments = GetInstalledPackages
                },
                new("install", "Install apps"),
                new("list", "List installed apps"),
                new("prefix", "Returns the path to the specified app")
                {
                    DynamicArguments = GetInstalledPackages
                },
                new("reset", "Reset an app to resolve conflicts")
                {
                    DynamicArguments = GetInstalledPackages
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
                    DynamicArguments = GetInstalledPackages
                },
                new("uninstall", "Uninstall an app")
                {
                    DynamicArguments = GetInstalledPackages
                },
                new("update", "Update apps, or Scoop itself")
                {
                    Parameters = [new("*", "Update all apps")],
                    DynamicArguments = GetInstalledPackages
                },
                new("virustotal", "Look for app's hash or url on virustotal.com"),
                new("which", "Locate a shim/executable (similar to 'which' on Linux)")
            ],
        };
    }

    private static IEnumerable<DynamicArgument> GetInstalledPackages()
    {
        foreach (var line in Helpers.ExecuteCommand("scoop", "list"))
        {
            if (!string.IsNullOrWhiteSpace(line) &&
                !line.StartsWith("Installed apps:") &&
                !line.StartsWith("\e[32;1m"))
            {
                var index = line.IndexOf(' ');
                yield return new DynamicArgument(index == -1 ? line : line[..index]);
            }
        }
    }
}

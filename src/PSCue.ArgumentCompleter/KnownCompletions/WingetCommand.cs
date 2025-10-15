// Windows Package Manager (Preview) v1.12.240-preview
// Copyright (c) Microsoft Corporation. All rights reserved.
//
// The winget command line utility enables installing applications and other packages from the command line.
//
// usage: winget  [<command>] [<options>]
//
// The following commands are available:
//   install    Installs the given package
//   show       Shows information about a package
//   source     Manage sources of packages
//   search     Find and show basic info of packages
//   list       Display installed packages
//   upgrade    Shows and performs available upgrades
//   uninstall  Uninstalls the given package
//   hash       Helper to hash installer files
//   validate   Validates a manifest file
//   settings   Open settings or set administrator settings
//   features   Shows the status of experimental features
//   export     Exports a list of the installed packages
//   import     Installs all the packages in a file
//   pin        Manage package pins
//   configure  Configures the system into a desired state
//   download   Downloads the installer from a given package
//   repair     Repairs the selected package
//
// For more details on a specific command, pass it the help argument. [-?]
//
// The following options are available:
//   -v,--version                Display the version of the tool
//   --info                      Display general info of the tool
//   -?,--help                   Shows help about the selected command
//   --wait                      Prompts the user to press any key before exiting
//   --logs,--open-logs          Open the default logs location
//   --verbose,--verbose-logs    Enables verbose logging for winget
//   --nowarn,--ignore-warnings  Suppresses warning outputs
//   --disable-interactivity     Disable interactive prompts
//   --proxy                     Set a proxy to use for this execution
//   --no-proxy                  Disable the use of proxy for this execution
//
// More help can be found at: https://aka.ms/winget-command-help

using PSCue.ArgumentCompleter.Completions;

namespace PSCue.ArgumentCompleter.KnownCompletions;

public static class WingetCommand
{
    public static Command Create()
    {
        return new Command("winget")
        {
            SubCommands =
            [
                new("install", "Installs the given package")
                {
                    Parameters =
                    [
                        new("--query", "The query used to search for a package (-q)") { Alias = "-q" },
                        new("--manifest", "The path to the manifest of the package (-m)") { Alias = "-m" },
                        new("--id", "Filter results by id"),
                        new("--name", "Filter results by name"),
                        new("--moniker", "Filter results by moniker"),
                        new("--version", "Use the specified version; default is the latest version (-v)") { Alias = "-v" },
                        new("--source", "Find package using the specified source (-s)") { Alias = "-s" },
                        new("--scope", "Select install scope (user or machine)")
                        {
                            StaticArguments =
                            [
                                new("user", "Install for current user only"),
                                new("machine", "Install for all users")
                            ]
                        },
                        new("--architecture", "Select the architecture (-a)")
                        {
                            Alias = "-a",
                            StaticArguments =
                            [
                                new("x86", "32-bit architecture"),
                                new("x64", "64-bit architecture"),
                                new("arm", "ARM architecture"),
                                new("arm64", "ARM64 architecture")
                            ]
                        },
                        new("--installer-type", "Select the installer type")
                        {
                            StaticArguments =
                            [
                                new("msix", "MSIX package"),
                                new("msi", "MSI installer"),
                                new("appx", "APPX package"),
                                new("exe", "EXE installer"),
                                new("zip", "ZIP archive"),
                                new("inno", "Inno Setup installer"),
                                new("nullsoft", "Nullsoft installer"),
                                new("wix", "WiX installer"),
                                new("burn", "Burn installer"),
                                new("portable", "Portable installer")
                            ]
                        },
                        new("--exact", "Find package using exact match (-e)") { Alias = "-e" },
                        new("--interactive", "Request interactive installation; user input may be needed (-i)") { Alias = "-i" },
                        new("--silent", "Request silent installation (-h)") { Alias = "-h" },
                        new("--locale", "Locale to use (BCP47 format)"),
                        new("--log", "Log location (if supported) (-o)") { Alias = "-o" },
                        new("--custom", "Arguments to be passed on to the installer in addition to the defaults"),
                        new("--override", "Override arguments to be passed on to the installer"),
                        new("--location", "Location to install to (if supported) (-l)") { Alias = "-l" },
                        new("--ignore-security-hash", "Ignore the installer hash check failure"),
                        new("--allow-reboot", "Allows a reboot if applicable"),
                        new("--skip-dependencies", "Skips processing package dependencies and Windows features"),
                        new("--ignore-local-archive-malware-scan", "Ignore the malware scan performed as part of installing an archive type package from local manifest"),
                        new("--dependency-source", "Find package dependencies using the specified source"),
                        new("--accept-package-agreements", "Accept all license agreements for packages"),
                        new("--no-upgrade", "Skips upgrade if an installed version already exists"),
                        new("--header", "Optional Windows-Package-Manager REST source HTTP header"),
                        new("--authentication-mode", "Specify authentication window preference")
                        {
                            StaticArguments =
                            [
                                new("silent", "Silent authentication"),
                                new("silentPreferred", "Prefer silent authentication"),
                                new("interactive", "Interactive authentication")
                            ]
                        },
                        new("--authentication-account", "Specify the account to be used for authentication"),
                        new("--accept-source-agreements", "Accept all source agreements during source operations"),
                        new("--rename", "The value to rename the executable file (portable) (-r)") { Alias = "-r" },
                        new("--uninstall-previous", "Uninstall the previous version of the package during upgrade"),
                        new("--force", "Direct run the command and continue with non security related issues"),
                        new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                        new("--wait", "Prompts the user to press any key before exiting"),
                        new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                        new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                        new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                        new("--disable-interactivity", "Disable interactive prompts"),
                        new("--proxy", "Set a proxy to use for this execution"),
                        new("--no-proxy", "Disable the use of proxy for this execution")
                    ]
                },
                new("show", "Shows information about a package")
                {
                    Parameters =
                    [
                        new("--query", "The query used to search for a package (-q)") { Alias = "-q" },
                        new("--manifest", "The path to the manifest of the package (-m)") { Alias = "-m" },
                        new("--id", "Filter results by id"),
                        new("--name", "Filter results by name"),
                        new("--moniker", "Filter results by moniker"),
                        new("--version", "Use the specified version; default is the latest version (-v)") { Alias = "-v" },
                        new("--source", "Find package using the specified source (-s)") { Alias = "-s" },
                        new("--exact", "Find package using exact match (-e)") { Alias = "-e" },
                        new("--scope", "Select install scope (user or machine)")
                        {
                            StaticArguments =
                            [
                                new("user", "User scope"),
                                new("machine", "Machine scope")
                            ]
                        },
                        new("--architecture", "Select the architecture (-a)")
                        {
                            Alias = "-a",
                            StaticArguments =
                            [
                                new("x86", "32-bit architecture"),
                                new("x64", "64-bit architecture"),
                                new("arm", "ARM architecture"),
                                new("arm64", "ARM64 architecture")
                            ]
                        },
                        new("--installer-type", "Select the installer type"),
                        new("--locale", "Locale to use (BCP47 format)"),
                        new("--versions", "Show available versions of the package"),
                        new("--header", "Optional Windows-Package-Manager REST source HTTP header"),
                        new("--authentication-mode", "Specify authentication window preference"),
                        new("--authentication-account", "Specify the account to be used for authentication"),
                        new("--accept-source-agreements", "Accept all source agreements during source operations"),
                        new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                        new("--wait", "Prompts the user to press any key before exiting"),
                        new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                        new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                        new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                        new("--disable-interactivity", "Disable interactive prompts"),
                        new("--proxy", "Set a proxy to use for this execution"),
                        new("--no-proxy", "Disable the use of proxy for this execution")
                    ]
                },
                new("source", "Manage sources of packages")
                {
                    SubCommands =
                    [
                        new("add", "Add a new source"),
                        new("list", "List current sources"),
                        new("update", "Update current sources"),
                        new("remove", "Remove current sources"),
                        new("reset", "Reset sources"),
                        new("export", "Export current sources")
                    ],
                    Parameters =
                    [
                        new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                        new("--wait", "Prompts the user to press any key before exiting"),
                        new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                        new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                        new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                        new("--disable-interactivity", "Disable interactive prompts"),
                        new("--proxy", "Set a proxy to use for this execution"),
                        new("--no-proxy", "Disable the use of proxy for this execution")
                    ]
                },
                new("search", "Find and show basic info of packages")
                {
                    Parameters =
                    [
                        new("--query", "The query used to search for a package (-q)") { Alias = "-q" },
                        new("--id", "Filter results by id"),
                        new("--name", "Filter results by name"),
                        new("--moniker", "Filter results by moniker"),
                        new("--tag", "Filter results by tag"),
                        new("--command", "Filter results by command") { Alias = "--cmd" },
                        new("--source", "Find package using the specified source (-s)") { Alias = "-s" },
                        new("--count", "Show no more than specified number of results (between 1 and 1000) (-n)") { Alias = "-n" },
                        new("--exact", "Find package using exact match (-e)") { Alias = "-e" },
                        new("--header", "Optional Windows-Package-Manager REST source HTTP header"),
                        new("--authentication-mode", "Specify authentication window preference"),
                        new("--authentication-account", "Specify the account to be used for authentication"),
                        new("--accept-source-agreements", "Accept all source agreements during source operations"),
                        new("--versions", "Show available versions of the package"),
                        new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                        new("--wait", "Prompts the user to press any key before exiting"),
                        new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                        new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                        new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                        new("--disable-interactivity", "Disable interactive prompts"),
                        new("--proxy", "Set a proxy to use for this execution"),
                        new("--no-proxy", "Disable the use of proxy for this execution")
                    ]
                },
                new("list", "Display installed packages")
                {
                    Parameters =
                    [
                        new("--query", "The query used to search for a package (-q)") { Alias = "-q" },
                        new("--id", "Filter results by id"),
                        new("--name", "Filter results by name"),
                        new("--moniker", "Filter results by moniker"),
                        new("--source", "Find package using the specified source (-s)") { Alias = "-s" },
                        new("--tag", "Filter results by tag"),
                        new("--command", "Filter results by command") { Alias = "--cmd" },
                        new("--count", "Show no more than specified number of results (between 1 and 1000) (-n)") { Alias = "-n" },
                        new("--exact", "Find package using exact match (-e)") { Alias = "-e" },
                        new("--scope", "Select installed package scope filter (user or machine)")
                        {
                            StaticArguments =
                            [
                                new("user", "User scope"),
                                new("machine", "Machine scope")
                            ]
                        },
                        new("--header", "Optional Windows-Package-Manager REST source HTTP header"),
                        new("--authentication-mode", "Specify authentication window preference"),
                        new("--authentication-account", "Specify the account to be used for authentication"),
                        new("--accept-source-agreements", "Accept all source agreements during source operations"),
                        new("--upgrade-available", "Lists only packages which have an upgrade available"),
                        new("--include-unknown", "List packages even if their current version cannot be determined (-u)") { Alias = "-u" },
                        new("--include-pinned", "List packages even if they have a pin that prevents upgrade") { Alias = "--pinned" },
                        new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                        new("--wait", "Prompts the user to press any key before exiting"),
                        new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                        new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                        new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                        new("--disable-interactivity", "Disable interactive prompts"),
                        new("--proxy", "Set a proxy to use for this execution"),
                        new("--no-proxy", "Disable the use of proxy for this execution")
                    ]
                },
                new("upgrade", "Shows and performs available upgrades")
                {
                    Parameters =
                    [
                        new("--query", "The query used to search for a package (-q)") { Alias = "-q" },
                        new("--manifest", "The path to the manifest of the package (-m)") { Alias = "-m" },
                        new("--id", "Filter results by id"),
                        new("--name", "Filter results by name"),
                        new("--moniker", "Filter results by moniker"),
                        new("--version", "Use the specified version; default is the latest version (-v)") { Alias = "-v" },
                        new("--source", "Find package using the specified source (-s)") { Alias = "-s" },
                        new("--exact", "Find package using exact match (-e)") { Alias = "-e" },
                        new("--interactive", "Request interactive installation; user input may be needed (-i)") { Alias = "-i" },
                        new("--silent", "Request silent installation (-h)") { Alias = "-h" },
                        new("--purge", "Deletes all files and directories in the package directory (portable)"),
                        new("--log", "Log location (if supported) (-o)") { Alias = "-o" },
                        new("--custom", "Arguments to be passed on to the installer in addition to the defaults"),
                        new("--override", "Override arguments to be passed on to the installer"),
                        new("--location", "Location to install to (if supported) (-l)") { Alias = "-l" },
                        new("--scope", "Select installed package scope filter (user or machine)")
                        {
                            StaticArguments =
                            [
                                new("user", "User scope"),
                                new("machine", "Machine scope")
                            ]
                        },
                        new("--architecture", "Select the architecture (-a)")
                        {
                            Alias = "-a",
                            StaticArguments =
                            [
                                new("x86", "32-bit architecture"),
                                new("x64", "64-bit architecture"),
                                new("arm", "ARM architecture"),
                                new("arm64", "ARM64 architecture")
                            ]
                        },
                        new("--installer-type", "Select the installer type"),
                        new("--locale", "Locale to use (BCP47 format)"),
                        new("--ignore-security-hash", "Ignore the installer hash check failure"),
                        new("--allow-reboot", "Allows a reboot if applicable"),
                        new("--skip-dependencies", "Skips processing package dependencies and Windows features"),
                        new("--ignore-local-archive-malware-scan", "Ignore the malware scan performed as part of installing an archive type package from local manifest"),
                        new("--accept-package-agreements", "Accept all license agreements for packages"),
                        new("--accept-source-agreements", "Accept all source agreements during source operations"),
                        new("--header", "Optional Windows-Package-Manager REST source HTTP header"),
                        new("--authentication-mode", "Specify authentication window preference"),
                        new("--authentication-account", "Specify the account to be used for authentication"),
                        new("--all", "Upgrade all installed packages to latest if available (-r)") { Alias = "-r" },
                        new("--include-unknown", "Upgrade packages even if their current version cannot be determined (-u)") { Alias = "-u" },
                        new("--include-pinned", "Upgrade packages even if they have a non-blocking pin") { Alias = "--pinned" },
                        new("--uninstall-previous", "Uninstall the previous version of the package during upgrade"),
                        new("--force", "Direct run the command and continue with non security related issues"),
                        new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                        new("--wait", "Prompts the user to press any key before exiting"),
                        new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                        new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                        new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                        new("--disable-interactivity", "Disable interactive prompts"),
                        new("--proxy", "Set a proxy to use for this execution"),
                        new("--no-proxy", "Disable the use of proxy for this execution")
                    ]
                },
                new("uninstall", "Uninstalls the given package")
                {
                    Parameters =
                    [
                        new("--query", "The query used to search for a package (-q)") { Alias = "-q" },
                        new("--manifest", "The path to the manifest of the package (-m)") { Alias = "-m" },
                        new("--id", "Filter results by id"),
                        new("--name", "Filter results by name"),
                        new("--moniker", "Filter results by moniker"),
                        new("--product-code", "Filters using the product code"),
                        new("--version", "The version to act upon (-v)") { Alias = "-v" },
                        new("--all-versions", "Uninstall all versions") { Alias = "--all" },
                        new("--source", "Find package using the specified source (-s)") { Alias = "-s" },
                        new("--exact", "Find package using exact match (-e)") { Alias = "-e" },
                        new("--scope", "Select installed package scope filter (user or machine)")
                        {
                            StaticArguments =
                            [
                                new("user", "User scope"),
                                new("machine", "Machine scope")
                            ]
                        },
                        new("--interactive", "Request interactive installation; user input may be needed (-i)") { Alias = "-i" },
                        new("--silent", "Request silent installation (-h)") { Alias = "-h" },
                        new("--force", "Direct run the command and continue with non security related issues"),
                        new("--purge", "Deletes all files and directories in the package directory (portable)"),
                        new("--preserve", "Retains all files and directories created by the package (portable)"),
                        new("--log", "Log location (if supported) (-o)") { Alias = "-o" },
                        new("--header", "Optional Windows-Package-Manager REST source HTTP header"),
                        new("--authentication-mode", "Specify authentication window preference"),
                        new("--authentication-account", "Specify the account to be used for authentication"),
                        new("--accept-source-agreements", "Accept all source agreements during source operations"),
                        new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                        new("--wait", "Prompts the user to press any key before exiting"),
                        new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                        new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                        new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                        new("--disable-interactivity", "Disable interactive prompts"),
                        new("--proxy", "Set a proxy to use for this execution"),
                        new("--no-proxy", "Disable the use of proxy for this execution")
                    ]
                },
                new("hash", "Helper to hash installer files"),
                new("validate", "Validates a manifest file"),
                new("settings", "Open settings or set administrator settings"),
                new("features", "Shows the status of experimental features"),
                new("export", "Exports a list of the installed packages")
                {
                    Parameters =
                    [
                        new("--output", "File where the result is to be written (-o)") { Alias = "-o" },
                        new("--source", "Export packages from the specified source (-s)") { Alias = "-s" },
                        new("--include-versions", "Include package versions in export file"),
                        new("--accept-source-agreements", "Accept all source agreements during source operations"),
                        new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                        new("--wait", "Prompts the user to press any key before exiting"),
                        new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                        new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                        new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                        new("--disable-interactivity", "Disable interactive prompts"),
                        new("--proxy", "Set a proxy to use for this execution"),
                        new("--no-proxy", "Disable the use of proxy for this execution")
                    ]
                },
                new("import", "Installs all the packages in a file")
                {
                    Parameters =
                    [
                        new("--import-file", "File describing the packages to install (-i)") { Alias = "-i" },
                        new("--ignore-unavailable", "Ignore unavailable packages"),
                        new("--ignore-versions", "Ignore package versions in import file"),
                        new("--no-upgrade", "Skips upgrade if an installed version already exists"),
                        new("--accept-package-agreements", "Accept all license agreements for packages"),
                        new("--accept-source-agreements", "Accept all source agreements during source operations"),
                        new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                        new("--wait", "Prompts the user to press any key before exiting"),
                        new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                        new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                        new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                        new("--disable-interactivity", "Disable interactive prompts"),
                        new("--proxy", "Set a proxy to use for this execution"),
                        new("--no-proxy", "Disable the use of proxy for this execution")
                    ]
                },
                new("pin", "Manage package pins")
                {
                    SubCommands =
                    [
                        new("add", "Add a new pin"),
                        new("remove", "Remove a package pin"),
                        new("list", "List current pins"),
                        new("reset", "Reset pins")
                    ],
                    Parameters =
                    [
                        new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                        new("--wait", "Prompts the user to press any key before exiting"),
                        new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                        new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                        new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                        new("--disable-interactivity", "Disable interactive prompts"),
                        new("--proxy", "Set a proxy to use for this execution"),
                        new("--no-proxy", "Disable the use of proxy for this execution")
                    ]
                },
                new("configure", "Configures the system into a desired state"),
                new("download", "Downloads the installer from a given package")
                {
                    Parameters =
                    [
                        new("--query", "The query used to search for a package (-q)") { Alias = "-q" },
                        new("--download-directory", "Directory where the installers are downloaded to (-d)") { Alias = "-d" },
                        new("--manifest", "The path to the manifest of the package (-m)") { Alias = "-m" },
                        new("--id", "Filter results by id"),
                        new("--name", "Filter results by name"),
                        new("--moniker", "Filter results by moniker"),
                        new("--version", "Use the specified version; default is the latest version (-v)") { Alias = "-v" },
                        new("--source", "Find package using the specified source (-s)") { Alias = "-s" },
                        new("--scope", "Select install scope (user or machine)")
                        {
                            StaticArguments =
                            [
                                new("user", "User scope"),
                                new("machine", "Machine scope")
                            ]
                        },
                        new("--architecture", "Select the architecture (-a)")
                        {
                            Alias = "-a",
                            StaticArguments =
                            [
                                new("x86", "32-bit architecture"),
                                new("x64", "64-bit architecture"),
                                new("arm", "ARM architecture"),
                                new("arm64", "ARM64 architecture")
                            ]
                        },
                        new("--installer-type", "Select the installer type"),
                        new("--exact", "Find package using exact match (-e)") { Alias = "-e" },
                        new("--locale", "Locale to use (BCP47 format)"),
                        new("--ignore-security-hash", "Ignore the installer hash check failure"),
                        new("--skip-dependencies", "Skips processing package dependencies and Windows features"),
                        new("--header", "Optional Windows-Package-Manager REST source HTTP header"),
                        new("--authentication-mode", "Specify authentication window preference"),
                        new("--authentication-account", "Specify the account to be used for authentication"),
                        new("--accept-package-agreements", "Accept all license agreements for packages"),
                        new("--accept-source-agreements", "Accept all source agreements during source operations"),
                        new("--skip-license", "Skips retrieving Microsoft Store package offline license") { Alias = "--skip-microsoft-store-package-license" },
                        new("--platform", "Select the target platform"),
                        new("--os-version", "Target OS version"),
                        new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                        new("--wait", "Prompts the user to press any key before exiting"),
                        new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                        new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                        new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                        new("--disable-interactivity", "Disable interactive prompts"),
                        new("--proxy", "Set a proxy to use for this execution"),
                        new("--no-proxy", "Disable the use of proxy for this execution")
                    ]
                },
                new("repair", "Repairs the selected package"),
            ],
            Parameters =
            [
                new("--version", "Display the version of the tool (-v)") { Alias = "-v" },
                new("--info", "Display general info of the tool"),
                new("--help", "Shows help about the selected command (-?)") { Alias = "-?" },
                new("--wait", "Prompts the user to press any key before exiting"),
                new("--logs", "Open the default logs location") { Alias = "--open-logs" },
                new("--verbose", "Enables verbose logging for winget") { Alias = "--verbose-logs" },
                new("--nowarn", "Suppresses warning outputs") { Alias = "--ignore-warnings" },
                new("--disable-interactivity", "Disable interactive prompts"),
                new("--proxy", "Set a proxy to use for this execution"),
                new("--no-proxy", "Disable the use of proxy for this execution"),
            ]
        };
    }
}

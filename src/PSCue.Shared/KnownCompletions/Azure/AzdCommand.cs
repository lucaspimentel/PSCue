// The Azure Developer CLI is an open-source tool that helps onboard and manage your application on Azure
//
// Usage
//   azd [command]
//
// Commands
//   Configure and develop your app
//     auth        : Authenticate with Azure.
//     config      : Manage azd configurations (ex: default Azure subscription, location).
//     hooks       : Develop, test and run hooks for an application. (Beta)
//     init        : Initialize a new application.
//     restore     : Restores the application's dependencies. (Beta)
//     template    : Find and view template details. (Beta)
//
//   Manage Azure resources and app deployments
//     deploy      : Deploy the application's code to Azure.
//     down        : Delete Azure resources for an application.
//     env         : Manage environments.
//     package     : Packages the application's code to be deployed to Azure. (Beta)
//     provision   : Provision the Azure resources for an application.
//     up          : Provision Azure resources, and deploy your project with a single command.
//
//   Monitor, test and release your app
//     monitor     : Monitor a deployed application. (Beta)
//     pipeline    : Manage and configure your deployment pipelines. (Beta)
//     show        : Display information about your app and its resources.
//
//   About, help and upgrade
//     version     : Print the version number of Azure Developer CLI.
//
// Flags
//     -C, --cwd string    : Sets the current working directory.
//         --debug         : Enables debugging and diagnostics logging.
//         --no-prompt     : Accepts the default value instead of prompting, or it fails if there is no default.
//
// Global Flags
//         --docs  : Opens the documentation for azd in your web browser.
//     -h, --help  : Gets help for azd.
//
// Use azd [command] --help to view examples and more information about a specific command.
//
// Deploying a sample application
// Initialize from a sample application by running the azd init --template [template name] command in an empty directory.
// Then, run azd up to get the application up-and-running in Azure.
//
// To view a curated list of sample templates, run azd template list.
// To view all available sample templates, including those submitted by the azd community, visit: https://azure.github.io/awesome-azd.
//
// Find a bug? Want to let us know how we're doing? Fill out this brief survey: https://aka.ms/azure-dev/hats.

//////////////////////////////////////////////////////
// azd auth --help
//
// Authenticate with Azure.
//
// Usage
//   azd auth [command]
//
// Available Commands
//   login         : Log in to Azure.
//   logout        : Log out of Azure.
//
// Global Flags
//     -C, --cwd string    : Sets the current working directory.
//         --debug         : Enables debugging and diagnostics logging.
//         --docs          : Opens the documentation for azd auth in your web browser.
//     -h, --help          : Gets help for auth.
//         --no-prompt     : Accepts the default value instead of prompting, or it fails if there is no default.

//////////////////////////////////////////////////////
// azd init --help
//
// Initialize a new application in your current directory.
//
//   • Running init without flags specified will prompt you to initialize using your existing code, or from a template.
//   • To view all available sample templates, including those submitted by the azd community, visit: https://azure.github.io/awesome-azd.
//
// Usage
//   azd init [flags]
//
// Flags
//     -b, --branch string         : The template branch to initialize from. Must be used with a template argument (--template or -t).
//     -e, --environment string    : The name of the environment to use.
//     -f, --filter strings        : The tag(s) used to filter template results. Supports comma-separated values.
//         --from-code             : Initializes a new application from your existing code.
//     -l, --location string       : Azure location for the new environment
//     -s, --subscription string   : Name or ID of an Azure subscription to use for the new environment
//     -t, --template string       : Initializes a new application from a template. You can use Full URI, <owner>/<repository>, or <repository> if it's part of the azure-samples organization.
//
// Global Flags
//     -C, --cwd string    : Sets the current working directory.
//         --debug         : Enables debugging and diagnostics logging.
//         --docs          : Opens the documentation for azd init in your web browser.
//     -h, --help          : Gets help for init.
//         --no-prompt     : Accepts the default value instead of prompting, or it fails if there is no default.

using PSCue.Shared.Completions;

namespace PSCue.Shared.KnownCompletions.Azure;

public static class AzdCommand
{
    public static Command Create()
    {
        return new Command("azd")
        {
            SubCommands =
            [
                new("auth", "Authenticate with Azure.")
                {
                    SubCommands =
                    [
                        new("login", "Log in to Azure."),
                        new("logout", "Log out of Azure.")
                    ],
                    Parameters =
                    [
                        new("--cwd", "Sets the current working directory (-C)") { Alias = "-C" },
                        new("--debug", "Enables debugging and diagnostics logging"),
                        new("--docs", "Opens the documentation for azd auth in your web browser"),
                        new("--help", "Gets help for auth (-h)") { Alias = "-h" },
                        new("--no-prompt", "Accepts the default value instead of prompting, or it fails if there is no default")
                    ]
                },
                new("config", "Manage azd configurations (ex: default Azure subscription, location)."),
                new("hooks", "Develop, test and run hooks for an application. (Beta)"),
                new("init", "Initialize a new application.")
                {
                    Parameters =
                    [
                        new("--branch", "The template branch to initialize from. Must be used with a template argument (--template or -t) (-b)") { Alias = "-b" },
                        new("--environment", "The name of the environment to use (-e)") { Alias = "-e" },
                        new("--filter", "The tag(s) used to filter template results. Supports comma-separated values (-f)") { Alias = "-f" },
                        new("--from-code", "Initializes a new application from your existing code"),
                        new("--location", "Azure location for the new environment (-l)") { Alias = "-l" },
                        new("--subscription", "Name or ID of an Azure subscription to use for the new environment (-s)") { Alias = "-s" },
                        new("--template", "Initializes a new application from a template. You can use Full URI, <owner>/<repository>, or <repository> if it's part of the azure-samples organization (-t)") { Alias = "-t" },
                        // global
                        new("--cwd", "Sets the current working directory (-C)") { Alias = "-C" },
                        new("--debug", "Enables debugging and diagnostics logging"),
                        new("--docs", "Opens the documentation for azd init in your web browser"),
                        new("--help", "Gets help for init (-h)") { Alias = "-h" },
                        new("--no-prompt", "Accepts the default value instead of prompting, or it fails if there is no default")
                    ]
                },
                new("restore", "Restores the application's dependencies. (Beta)"),
                new("template", "Find and view template details. (Beta)"),
                new("deploy", "Deploy the application's code to Azure."),
                new("down", "Delete Azure resources for an application."),
                new("env", "Manage environments."),
                new("package", "Packages the application's code to be deployed to Azure. (Beta)"),
                new("provision", "Provision the Azure resources for an application."),
                new("up", "Provision Azure resources, and deploy your project with a single command."),
                new("monitor", "Monitor a deployed application. (Beta)"),
                new("pipeline", "Manage and configure your deployment pipelines. (Beta)"),
                new("show", "Display information about your app and its resources."),
                new("version", "Print the version number of Azure Developer CLI.")
            ],
            Parameters =
            [
                new("--cwd", "Sets the current working directory (-C)") { Alias = "-C" },
                new("--debug", "Enables debugging and diagnostics logging"),
                new("--no-prompt", "Accepts the default value instead of prompting, or it fails if there is no default"),
                new("--docs", "Opens the documentation for azd in your web browser"),
                new("--help", "Gets help for azd (-h)") { Alias = "-h" }
            ]
        };
    }
}

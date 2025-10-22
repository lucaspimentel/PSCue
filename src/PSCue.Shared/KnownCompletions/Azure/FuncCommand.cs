// Azure Functions Core Tools
// Core Tools Version:       4.0.7030 Commit hash: N/A +bb4c949899cd5659d6bfe8b92cc923453a2e8f88 (64-bit)
// Function Runtime Version: 4.1037.0.23568
//
// Usage: func [context] [context] <action> [-/--options]
//
// Contexts:
// azure               Commands to log in to Azure and manage resources
// azurecontainerapps  Commands for working with Container Service and Azure Functions
// durable             Commands for working with Durable Functions
// extensions          Commands for installing extensions
// function            Commands for creating and running functions locally
// host                Commands for running the Functions host locally
// kubernetes          Commands for working with Kubernetes and Azure Functions
// settings            Commands for managing environment settings for the local Functions host
// templates           Commands for listing available function templates
//
// Actions:
// start   Launches the functions runtime host
//     --port [-p]             Local port to listen on. Default: 7071
//     --cors                  A comma separated list of CORS origins with no spaces. Example: https://functions.azure.com,https://functions-staging.azure.com
//     --cors-credentials      Allow cross-origin authenticated requests (i.e. cookies and the Authentication header)
//     --timeout [-t]          Timeout for the functions host to start in seconds. Default: 20 seconds.
//     --useHttps              Bind to https://localhost:{port} rather than http://localhost:{port}. By default it creates and trusts a certificate.
//     --cert                  for use with --useHttps. The path to a pfx file that contains a private key
//     --password              to use with --cert. Either the password, or a file that contains the password for the pfx file
//     --language-worker       Arguments to configure the language worker.
//     --no-build              Do not build the current project before running. For dotnet projects only. Default is set to false.
//     --enableAuth            Enable full authentication handling pipeline.
//     --functions             A space separated list of functions to load.
//     --verbose               When false, hides system logs other than warnings and errors.
//     --dotnet-isolated-debug When specified, set to true, pauses the .NET Worker process until a debugger is attached.
//     --enable-json-output    Signals to Core Tools and other components that JSON line output console logs, when applicable, should be emitted.
//     --json-output-file      If provided, a path to the file that will be used to write the output when using --enable-json-output.
//     --runtime               If provided, determines which version of the host to start. Allowed values are 'inproc6', 'inproc8', and 'default' (which runs the out-of-process host).
//
// new     Create a new function from a template. Aliases: new, create
//     --language [-l]  Template programming language, such as C#, F#, JavaScript, etc.
//     --template [-t]  Template name
//     --name [-n]      Function name
//     --file [-f]      File Name
//     --authlevel [-a] Authorization level is applicable to templates that use Http trigger, Allowed values: [function, anonymous, admin]. Authorization level is not enforced when running functions from core tools
//     --csx            use old style csx dotnet functions
//
// init    Create a new Function App in the current folder. Initializes git repo.
//     --source-control       Run git init. Default is false.
//     --worker-runtime       Runtime framework for the functions. Options are: dotnet-isolated, dotnet, node, python, powershell, custom
//     --force                Force initializing
//     --docker               Create a Dockerfile based on the selected worker runtime
//     --docker-only          Adds a Dockerfile to an existing function app project. Will prompt for worker-runtime if not specified or set in local.settings.json
//     --csx                  use csx dotnet functions
//     --language             Initialize a language specific project. Currently supported when --worker-runtime set to node. Options are - "typescript" and "javascript"
//     --target-framework     Initialize a project with the given target framework moniker. Currently supported only when --worker-runtime set to dotnet-isolated or dotnet. Options are - net9.0, net8.0, net7.0, net6.0, net48
//     --managed-dependencies Installs managed dependencies. Currently, only the PowerShell worker runtime supports this functionality.
//     --model [-m]           Selects the programming model for the function app. Note this flag is now only applicable to Python and JavaScript/TypeScript. Options are V1 and V2 for Python; V3 and V4 for JavaScript/TypeScript. Currently,
//                            the V2 and V4 programming models are in preview.
//     --skip-npm-install     Skips the npm installation phase when using V4 programming model for NodeJS
//     --no-docs              Do not create getting started documentation file. Currently supported when --worker-runtime set to python.
//
// logs    Gets logs of Functions running on custom backends
//     --platform Hosting platform for the function app. Valid options: kubernetes
//     --name     Function name

using PSCue.Shared.Completions;

namespace PSCue.Shared.KnownCompletions.Azure;

public static class FuncCommand
{
    public static Command Create()
    {
        return new Command("func")
        {
            SubCommands =
            [
                new("azure", "Commands to log in to Azure and manage resources"),
                new("azurecontainerapps", "Commands for working with Container Service and Azure Functions"),
                new("durable", "Commands for working with Durable Functions"),
                new("extensions", "Commands for installing extensions"),
                new("function", "Commands for creating and running functions locally"),
                new("host", "Commands for running the Functions host locally"),
                new("kubernetes", "Commands for working with Kubernetes and Azure Functions"),
                new("settings", "Commands for managing environment settings for the local Functions host"),
                new("templates", "Commands for listing available function templates"),
                new("start", "Launches the functions runtime host")
                {
                    Parameters =
                    [
                        new("-p", "Local port to listen on. Default: 7071"),
                        new("--port", "Local port to listen on. Default: 7071"),
                        new("--cors", "A comma separated list of CORS origins with no spaces."),
                        new("--cors-credentials", "Allow cross-origin authenticated requests."),
                        new("--t", "Timeout for the functions host to start in seconds. Default: 20 seconds."),
                        new("--timeout", "Timeout for the functions host to start in seconds. Default: 20 seconds."),
                        new("--useHttps", "Bind to https://localhost:{port} rather than http://localhost:{port}."),
                        new("--cert", "The path to a pfx file that contains a private key."),
                        new("--password", "Either the password, or a file that contains the password for the pfx file."),
                        new("--language-worker", "Arguments to configure the language worker."),
                        new("--no-build", "Do not build the current project before running. For dotnet projects only."),
                        new("--enableAuth", "Enable full authentication handling pipeline."),
                        new("--functions", "A space separated list of functions to load."),
                        new("--verbose", "When false, hides system logs other than warnings and errors."),
                        new("--dotnet-isolated-debug", "Pauses the .NET Worker process until a debugger is attached."),
                        new("--enable-json-output", "Signals to Core Tools and other components that JSON line output console logs should be emitted."),
                        new("--json-output-file", "A path to the file that will be used to write the output when using --enable-json-output."),
                        new("--runtime", "Determines which version of the host to start. Allowed values are 'inproc6', 'inproc8', and 'default'.")
                    ]
                },
                new("new", "Create a new function from a template. Aliases: new, create")
                {
                    Parameters =
                    [
                        new("-l", "Template programming language, such as C#, F#, JavaScript, etc."),
                        new("--language", "Template programming language, such as C#, F#, JavaScript, etc."),
                        new("-t", "Template name"),
                        new("--template", "Template name"),
                        new("-n", "Function name"),
                        new("--name", "Function name"),
                        new("-f", "File Name"),
                        new("--file", "File Name"),
                        new("-a", "Authorization level is applicable to templates that use Http trigger."),
                        new("--authlevel", "Authorization level is applicable to templates that use Http trigger."),
                        new("--csx", "Use old style csx dotnet functions.")
                    ]
                },
                new("init", "Create a new Function App in the current folder. Initializes git repo.")
                {
                    Parameters =
                    [
                        new("--source-control", "Run git init. Default is false."),
                        new("--worker-runtime", "Runtime framework for the functions."),
                        new("--force", "Force initializing."),
                        new("--docker", "Create a Dockerfile based on the selected worker runtime."),
                        new("--docker-only", "Adds a Dockerfile to an existing function app project."),
                        new("--csx", "Use csx dotnet functions."),
                        new("--language", "Initialize a language specific project."),
                        new("--target-framework", "Initialize a project with the given target framework moniker."),
                        new("--managed-dependencies", "Installs managed dependencies."),
                        new("-m", "Selects the programming model for the function app."),
                        new("--model", "Selects the programming model for the function app."),
                        new("--skip-npm-install", "Skips the npm installation phase."),
                        new("--no-docs", "Do not create getting started documentation file.")
                    ]
                },
                new("logs", "Gets logs of Functions running on custom backends")
                {
                    Parameters =
                    [
                        new("--platform", "Hosting platform for the function app."),
                        new("--name", "Function name")
                    ]
                }
            ]
        };
    }
}

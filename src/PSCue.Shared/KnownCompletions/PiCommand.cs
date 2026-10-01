// Usage: pi [options] [--] [@files...] [messages...]
//
// Commands:
//   pi install <source> [-l]     Install extension source and add to settings
//   pi remove <source> [-l]      Remove extension source from settings
//   pi uninstall <source> [-l]   Alias for remove
//   pi update [source|self|pi]   Update pi, extensions, or model catalogs
//   pi list                      List installed extensions from settings
//   pi config [-l]               Open TUI to enable/disable package resources
//   pi auth <command>            Print credentials or check provider readiness
//   pi mcp <command>             Check MCP servers, sign in to or out of OAuth servers

using PSCue.Shared.Completions;

namespace PSCue.Shared.KnownCompletions;

public static class PiCommand
{
    public static Command Create()
    {
        var helpFlag = new CommandParameter("--help", "Show help (-h)") { Alias = "-h" };

        var localFlag = new CommandParameter("--local", "Use project-local .pi/ config instead of the global file (-l)") { Alias = "-l" };
        var approveFlag = new CommandParameter("--approve", "Trust project-local files for this command (-a)") { Alias = "-a" };
        var noApproveFlag = new CommandParameter("--no-approve", "Ignore project-local files for this command (-na)") { Alias = "-na" };

        var providerParam = new CommandParameter("--provider", "Provider name") { RequiresValue = true };
        var modelParam = new CommandParameter("--model", "Model pattern or ID") { RequiresValue = true };

        var authSubcommandParameters = new CommandParameter[]
        {
            providerParam,
            modelParam,
        };

        var mcpAddParameters = new CommandParameter[]
        {
            localFlag,
            new("--url", "Streamable HTTP server URL (instead of a command)") { RequiresValue = true },
            new("--env", "Environment variable for a stdio server (repeatable)") { RequiresValue = true },
            new("--cwd", "Working directory for a stdio server") { RequiresValue = true },
            new("--header", "HTTP header (repeatable)") { RequiresValue = true },
            new("--bearer-token-env-var", "Send Authorization: Bearer ${NAME}") { RequiresValue = true },
            new("--oauth-client-id", "Pre-registered OAuth client id") { RequiresValue = true },
            new("--oauth-client-secret", "OAuth client secret") { RequiresValue = true },
            new("--oauth-callback-port", "Fixed OAuth callback port") { RequiresValue = true },
            new("--oauth-client-name", "Client name sent when registering with the OAuth server") { RequiresValue = true },
            new("--exposure", "When tools are exposed to the model")
            {
                RequiresValue = true,
                StaticArguments =
                [
                    new("codemode", "Run tools through code execution (default)"),
                    new("deferred", "Tools are listed, loaded on demand"),
                    new("direct", "Expose tools directly"),
                    new("hidden", "Do not expose tools to the model"),
                ],
            },
            new("--description", "What the server offers, shown in the system prompt") { RequiresValue = true },
        };

        return new Command("pi")
        {
            SubCommands =
            [
                new("install", "Install a package and add it to settings")
                {
                    Parameters =
                    [
                        localFlag,
                        approveFlag,
                        noApproveFlag,
                        helpFlag,
                    ]
                },
                new("remove", "Remove extension source from settings (alias: uninstall)")
                {
                    Alias = "uninstall",
                    Parameters =
                    [
                        localFlag,
                        helpFlag,
                    ]
                },
                new("update", "Update pi, installed packages, or model catalogs")
                {
                    Parameters =
                    [
                        new("--self", "Update pi only (default when no target is given)"),
                        new("--extensions", "Update installed packages only"),
                        new("--models", "Refresh model catalogs only"),
                        new("--all", "Update pi and installed packages"),
                        new("--extension", "Update one package only") { RequiresValue = true },
                        approveFlag,
                        noApproveFlag,
                        new("--force", "Reinstall pi even if the current version is latest"),
                        helpFlag,
                    ]
                },
                new("list", "List installed extensions from settings")
                {
                    Parameters =
                    [
                        localFlag,
                        helpFlag,
                    ]
                },
                new("config", "Open TUI to enable/disable package resources (Tab switches scope)")
                {
                    Parameters =
                    [
                        localFlag,
                        helpFlag,
                    ]
                },
                new("auth", "Print credentials or check provider readiness")
                {
                    SubCommands =
                    [
                        new("print-api-key", "Print the API key for a provider or model")
                        {
                            Parameters = [.. authSubcommandParameters, helpFlag]
                        },
                        new("print-bearer-token", "Print the bearer token for a provider or model")
                        {
                            Parameters =
                            [
                                .. authSubcommandParameters,
                                new("--min-expiry", "Minimum remaining token lifetime") { RequiresValue = true },
                                helpFlag,
                            ]
                        },
                        new("check", "Check whether a provider is ready")
                        {
                            Parameters =
                            [
                                .. authSubcommandParameters,
                                new("--json", "Print the check result as JSON"),
                                new("--credentials", "Emit the credential, or include it in JSON output"),
                                new("--no-refresh", "Do not refresh expired OAuth credentials"),
                                helpFlag,
                            ]
                        },
                    ],
                    Parameters = [helpFlag],
                },
                new("mcp", "Check MCP servers, sign in to or out of OAuth servers")
                {
                    SubCommands =
                    [
                        new("add", "Add or replace a server in mcp.json")
                        {
                            Parameters = [.. mcpAddParameters, helpFlag]
                        },
                        new("remove", "Remove a server from mcp.json")
                        {
                            Parameters = [localFlag, helpFlag]
                        },
                        new("list", "Show state, tools, and errors (exits 1 on failure)")
                        {
                            Parameters =
                            [
                                new("--json", "Print the list as JSON"),
                                helpFlag,
                            ]
                        },
                        new("login", "Sign in to an OAuth server through the browser")
                        {
                            Parameters =
                            [
                                new("--timeout", "Login timeout in seconds") { RequiresValue = true },
                                helpFlag,
                            ]
                        },
                        new("logout", "Delete the stored OAuth credentials"),
                    ],
                    Parameters = [helpFlag],
                },
            ],
            Parameters =
            [
                new("--provider", "Provider name (default: google)") { RequiresValue = true },
                new("--model", "Model pattern or ID (supports provider/id and optional :<thinking>)") { RequiresValue = true },
                new("--api-key", "API key (defaults to env vars)") { RequiresValue = true },
                new("--system-prompt", "System prompt text") { RequiresValue = true },
                new("--append-system-prompt", "Append text or file contents to the system prompt (repeatable)") { RequiresValue = true },
                new("--mode", "Output mode")
                {
                    RequiresValue = true,
                    StaticArguments =
                    [
                        new("text", "Human-readable output (default)"),
                        new("json", "JSON output"),
                        new("rpc", "RPC mode"),
                    ]
                },
                new("--print", "Non-interactive mode: process prompt and exit (-p)") { Alias = "-p" },
                new("--continue", "Continue previous session (-c)") { Alias = "-c" },
                new("--resume", "Select a session to resume (-r)") { Alias = "-r" },
                new("--session", "Use specific session file or partial UUID") { RequiresValue = true },
                new("--session-id", "Use exact project session ID, creating it if missing") { RequiresValue = true },
                new("--fork", "Fork a session file or partial UUID into a new session") { RequiresValue = true },
                new("--session-dir", "Directory for session storage and lookup") { RequiresValue = true },
                new("--no-session", "Don't save session (ephemeral)"),
                new("--name", "Set session display name (-n)") { Alias = "-n", RequiresValue = true },
                new("--models", "Comma-separated model patterns for Ctrl+P cycling") { RequiresValue = true },
                new("--no-tools", "Disable all tools by default (built-in and extension) (-nt)") { Alias = "-nt" },
                new("--no-builtin-tools", "Disable built-in tools by default but keep extension/custom tools enabled (-nbt)") { Alias = "-nbt" },
                new("--tools", "Comma-separated allowlist of tool names (-t)") { Alias = "-t", RequiresValue = true },
                new("--exclude-tools", "Comma-separated denylist of tool names (-xt)") { Alias = "-xt", RequiresValue = true },
                new("--thinking", "Thinking level")
                {
                    RequiresValue = true,
                    StaticArguments =
                    [
                        new("off", "No thinking"),
                        new("minimal", "Minimal thinking"),
                        new("low", "Low thinking"),
                        new("medium", "Medium thinking"),
                        new("high", "High thinking"),
                        new("xhigh", "Extra high thinking"),
                        new("max", "Maximum thinking"),
                    ]
                },
                new("--extension", "Load an extension file or builtin:<name> (repeatable)") { Alias = "-e", RequiresValue = true },
                new("--no-extensions", "Disable extension discovery and built-in extensions (-ne)") { Alias = "-ne" },
                new("--skill", "Load a skill file or directory (repeatable)") { RequiresValue = true },
                new("--no-skills", "Disable skills discovery and loading (-ns)") { Alias = "-ns" },
                new("--prompt-template", "Load a prompt template file or directory (repeatable)") { RequiresValue = true },
                new("--no-prompt-templates", "Disable prompt template discovery and loading (-np)") { Alias = "-np" },
                new("--theme", "Load a theme file or directory (repeatable)") { RequiresValue = true },
                new("--use-theme", "Set the initial interactive theme for this run") { RequiresValue = true },
                new("--no-themes", "Disable theme discovery and loading"),
                new("--no-context-files", "Disable AGENTS.md and CLAUDE.md discovery and loading (-nc)") { Alias = "-nc" },
                new("--export", "Export session file to HTML and exit") { RequiresValue = true },
                new("--list-models", "List available models (with optional fuzzy search)"),
                new("--verbose", "Force verbose startup (overrides quietStartup setting)"),
                new("--tui-mode", "TUI mode")
                {
                    RequiresValue = true,
                    StaticArguments =
                    [
                        new("regular", "Regular TUI mode (default)"),
                        new("fullscreen", "Fullscreen TUI mode"),
                    ]
                },
                new("--approve", "Trust project-local files for this run (-a)") { Alias = "-a" },
                new("--no-approve", "Ignore project-local files for this run (-na)") { Alias = "-na" },
                new("--offline", "Disable startup network operations (same as PI_OFFLINE=1)"),
                new("--help", "Show help (-h)") { Alias = "-h" },
            ]
        };
    }
}

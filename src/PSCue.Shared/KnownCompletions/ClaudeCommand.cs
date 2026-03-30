// Usage: claude [options] [command] [prompt]
//
// Commands:
//   agents                      List configured agents
//   auth                        Manage authentication
//   auto-mode                   Inspect auto mode classifier configuration
//   doctor                      Check the health of your Claude Code auto-updater
//   install                     Install Claude Code native build
//   mcp                         Configure and manage MCP servers
//   plugin                      Manage Claude Code plugins
//   setup-token                 Set up a long-lived authentication token
//   update                      Check for updates and install if available (alias: upgrade)

using PSCue.Shared.Completions;

namespace PSCue.Shared.KnownCompletions;

public static class ClaudeCommand
{
    public static Command Create()
    {
        return new Command("claude")
        {
            SubCommands =
            [
                new("mcp", "Configure and manage MCP servers")
                {
                    SubCommands =
                    [
                        new("serve", "Start the Claude Code MCP server")
                        {
                            Parameters =
                            [
                                new("--debug", "Enable debug mode") { Alias = "-d" },
                                new("--verbose", "Enable verbose output"),
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("add", "Add an MCP server to Claude Code")
                        {
                            Parameters =
                            [
                                new("--scope", "Scope for the server (local, user, project)") { Alias = "-s", StaticArguments = [new("local"), new("user"), new("project")] },
                                new("--transport", "Transport type (stdio, sse, http)") { Alias = "-t", StaticArguments = [new("stdio"), new("sse"), new("http")] },
                                new("--env", "Environment variables") { Alias = "-e" },
                                new("--header", "HTTP headers") { Alias = "-H" },
                                new("--callback-port", "OAuth callback port"),
                                new("--client-id", "OAuth client ID"),
                                new("--client-secret", "OAuth client secret"),
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("remove", "Remove an MCP server")
                        {
                            Parameters =
                            [
                                new("--scope", "Scope for the server (local, user, project)") { Alias = "-s", StaticArguments = [new("local"), new("user"), new("project")] },
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("list", "List configured MCP servers"),
                        new("get", "Get details about an MCP server"),
                        new("add-json", "Add an MCP server with a JSON string")
                        {
                            Parameters =
                            [
                                new("--scope", "Scope for the server (local, user, project)") { Alias = "-s", StaticArguments = [new("local"), new("user"), new("project")] },
                                new("--client-secret", "OAuth client secret"),
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("add-from-claude-desktop", "Import MCP servers from Claude Desktop")
                        {
                            Parameters =
                            [
                                new("--scope", "Scope for the server (local, user, project)") { Alias = "-s", StaticArguments = [new("local"), new("user"), new("project")] },
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("reset-project-choices", "Reset all approved/rejected project-scoped servers"),
                        new("help", "Display help for command"),
                    ],
                    Parameters =
                    [
                        new("--help", "Display help for command") { Alias = "-h" },
                    ]
                },
                new("plugin", "Manage Claude Code plugins")
                {
                    SubCommands =
                    [
                        new("validate", "Validate a plugin or marketplace manifest")
                        {
                            Parameters =
                            [
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("marketplace", "Manage Claude Code marketplaces")
                        {
                            SubCommands =
                            [
                                new("add", "Add a marketplace")
                                {
                                    Parameters =
                                    [
                                        new("--scope", "Scope for the marketplace (local, user, project)") { Alias = "-s", StaticArguments = [new("local"), new("user"), new("project")] },
                                        new("--sparse", "Use sparse checkout for Git marketplaces"),
                                        new("--help", "Display help for command") { Alias = "-h" },
                                    ]
                                },
                                new("list", "List configured marketplaces")
                                {
                                    Parameters =
                                    [
                                        new("--json", "Output as JSON"),
                                        new("--help", "Display help for command") { Alias = "-h" },
                                    ]
                                },
                                new("remove", "Remove a marketplace") { Alias = "rm" },
                                new("update", "Update marketplaces"),
                                new("help", "Display help for command"),
                            ],
                            Parameters =
                            [
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("install", "Install a plugin from available marketplaces (alias: i)") { Alias = "i", Parameters = [new("--scope", "Scope for the plugin (local, user, project)") { Alias = "-s", StaticArguments = [new("local"), new("user"), new("project")] }, new("--help", "Display help for command") { Alias = "-h" }] },
                        new("uninstall", "Uninstall an installed plugin (alias: remove)") { Alias = "remove", Parameters = [new("--scope", "Scope for the plugin (local, user, project)") { Alias = "-s", StaticArguments = [new("local"), new("user"), new("project")] }, new("--help", "Display help for command") { Alias = "-h" }] },
                        new("enable", "Enable a disabled plugin")
                        {
                            Parameters =
                            [
                                new("--scope", "Scope for the plugin (local, user, project)") { Alias = "-s", StaticArguments = [new("local"), new("user"), new("project")] },
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("disable", "Disable an enabled plugin")
                        {
                            Parameters =
                            [
                                new("--scope", "Scope for the plugin (local, user, project)") { Alias = "-s", StaticArguments = [new("local"), new("user"), new("project")] },
                                new("--all", "Disable all plugins") { Alias = "-a" },
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("list", "List installed plugins")
                        {
                            Parameters =
                            [
                                new("--json", "Output as JSON"),
                                new("--available", "List available plugins from marketplaces"),
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("update", "Update installed plugins")
                        {
                            Parameters =
                            [
                                new("--scope", "Scope for the plugin (local, user, project)") { Alias = "-s", StaticArguments = [new("local"), new("user"), new("project")] },
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("help", "Display help for command"),
                    ],
                    Parameters =
                    [
                        new("--help", "Display help for command") { Alias = "-h" },
                    ]
                },
                new("auth", "Manage authentication")
                {
                    SubCommands =
                    [
                        new("login", "Log in to Claude")
                        {
                            Parameters =
                            [
                                new("--email", "Email address for login"),
                                new("--sso", "Log in with SSO"),
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("logout", "Log out of Claude"),
                        new("status", "Show authentication status")
                        {
                            Parameters =
                            [
                                new("--json", "Output as JSON"),
                                new("--text", "Output as text"),
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("help", "Display help for command"),
                    ],
                    Parameters =
                    [
                        new("--help", "Display help for command") { Alias = "-h" },
                    ]
                },
                new("agents", "List configured agents")
                {
                    Parameters =
                    [
                        new("--setting-sources", "Comma-separated list of setting sources"),
                        new("--help", "Display help for command") { Alias = "-h" },
                    ]
                },
                new("auto-mode", "Inspect auto mode classifier configuration")
                {
                    SubCommands =
                    [
                        new("config", "Print the effective auto mode config as JSON"),
                        new("critique", "Get AI feedback on your custom auto mode rules"),
                        new("defaults", "Print the default auto mode rules as JSON"),
                        new("help", "Display help for command"),
                    ],
                    Parameters =
                    [
                        new("--help", "Display help for command") { Alias = "-h" },
                    ]
                },
                new("setup-token", "Set up a long-lived authentication token"),
                new("doctor", "Check the health of your Claude Code auto-updater"),
                new("update", "Check for updates and install if available (alias: upgrade)") { Alias = "upgrade" },
                new("install", "Install Claude Code native build")
                {
                    Parameters =
                    [
                        new("--force", "Force installation even if already installed"),
                        new("--help", "Display help for command") { Alias = "-h" },
                    ]
                },
            ],
            Parameters =
            [
                new("--agent", "Agent for the current session"),
                new("--bare", "Minimal mode: skip hooks, LSP, plugins, auto-memory"),
                new("--betas", "Beta headers"),
                new("--brief", "Enable SendUserMessage tool for agent-to-user communication"),
                new("--chrome", "Enable Chrome integration"),
                new("--no-chrome", "Disable Chrome integration"),
                new("--debug", "Enable debug mode with optional category filtering") { Alias = "-d" },
                new("--debug-file", "Debug log file path"),
                new("--verbose", "Override verbose mode setting from config"),
                new("--print", "Print response and exit") { Alias = "-p" },
                new("--output-format", "Output format")
                {
                    StaticArguments =
                    [
                        new("text", "Plain text output"),
                        new("json", "JSON output"),
                        new("stream-json", "Streaming JSON output"),
                    ]
                },
                new("--include-partial-messages", "Include partial message chunks"),
                new("--input-format", "Input format")
                {
                    StaticArguments =
                    [
                        new("text", "Plain text input"),
                        new("stream-json", "Streaming JSON input"),
                    ]
                },
                new("--mcp-debug", "[DEPRECATED] Enable MCP debug mode"),
                new("--dangerously-skip-permissions", "Bypass all permission checks"),
                new("--allow-dangerously-skip-permissions", "Enable bypassing permissions as an option"),
                new("--disable-slash-commands", "Disable all skills"),
                new("--effort", "Effort level for the session")
                {
                    StaticArguments =
                    [
                        new("low", "Low effort"),
                        new("medium", "Medium effort"),
                        new("high", "High effort"),
                        new("max", "Maximum effort"),
                    ]
                },
                new("--file", "File resources to download"),
                new("--from-pr", "Resume session linked to PR"),
                new("--json-schema", "JSON Schema for structured output"),
                new("--max-budget-usd", "Maximum dollar amount"),
                new("--no-session-persistence", "Disable session persistence"),
                new("--replay-user-messages", "Re-emit user messages from stdin"),
                new("--allowedTools", "Comma or space-separated list of allowed tools") { Alias = "--allowed-tools" },
                new("--disallowedTools", "Comma or space-separated list of denied tools") { Alias = "--disallowed-tools" },
                new("--tools", "Specify available tools"),
                new("--mcp-config", "Load MCP servers from JSON files or strings"),
                new("--system-prompt", "System prompt to use for the session"),
                new("--append-system-prompt", "Append a system prompt to the default"),
                new("--name", "Set a display name for this session") { Alias = "-n" },
                new("--permission-mode", "Permission mode")
                {
                    StaticArguments =
                    [
                        new("acceptEdits", "Accept edit permissions"),
                        new("auto", "Auto permission mode"),
                        new("bypassPermissions", "Bypass all permissions"),
                        new("default", "Default permission mode"),
                        new("dontAsk", "Don't ask for permissions"),
                        new("plan", "Plan mode"),
                    ]
                },
                new("--continue", "Continue the most recent conversation") { Alias = "-c" },
                new("--resume", "Resume a conversation") { Alias = "-r" },
                new("--fork-session", "Create a new session ID when resuming"),
                new("--model", "Model for the current session"),
                new("--fallback-model", "Enable automatic fallback model"),
                new("--settings", "Path to a settings JSON file or JSON string"),
                new("--add-dir", "Additional directories to allow tool access to"),
                new("--ide", "Automatically connect to IDE on startup"),
                new("--strict-mcp-config", "Only use MCP servers from --mcp-config"),
                new("--session-id", "Use a specific session ID"),
                new("--agents", "JSON object defining custom agents"),
                new("--setting-sources", "Comma-separated list of setting sources"),
                new("--plugin-dir", "Load plugins from directories"),
                new("--tmux", "Create tmux session"),
                new("--worktree", "Create git worktree") { Alias = "-w" },
                new("--version", "Output the version number") { Alias = "-v" },
                new("--help", "Display help for command") { Alias = "-h" },
            ]
        };
    }
}

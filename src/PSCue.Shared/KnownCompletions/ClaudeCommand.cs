// Usage: claude [options] [command] [prompt]
//
// Commands:
//   mcp                         Configure and manage MCP servers
//   plugin                      Manage Claude Code plugins
//   migrate-installer           Migrate from global npm installation to local installation
//   setup-token                 Set up a long-lived authentication token
//   doctor                      Check the health of your Claude Code auto-updater
//   update                      Check for updates and install if available
//   install                     Install Claude Code native build

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
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("add", "Add an MCP server to Claude Code")
                        {
                            Parameters =
                            [
                                new("--transport", "Transport type (http, sse, stdio)"),
                                new("--env", "Environment variables"),
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("remove", "Remove an MCP server")
                        {
                            Parameters =
                            [
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("list", "List configured MCP servers"),
                        new("get", "Get details about an MCP server"),
                        new("add-json", "Add an MCP server with a JSON string")
                        {
                            Parameters =
                            [
                                new("--help", "Display help for command") { Alias = "-h" },
                            ]
                        },
                        new("add-from-claude-desktop", "Import MCP servers from Claude Desktop")
                        {
                            Parameters =
                            [
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
                        new("validate", "Validate a plugin or marketplace manifest"),
                        new("marketplace", "Manage Claude Code marketplaces"),
                        new("install", "Install a plugin from available marketplaces"),
                        new("i", "Install a plugin from available marketplaces"),
                        new("uninstall", "Uninstall an installed plugin"),
                        new("remove", "Uninstall an installed plugin"),
                        new("enable", "Enable a disabled plugin"),
                        new("disable", "Disable an enabled plugin"),
                        new("help", "Display help for command"),
                    ],
                    Parameters =
                    [
                        new("--help", "Display help for command") { Alias = "-h" },
                    ]
                },
                new("migrate-installer", "Migrate from global npm installation to local installation"),
                new("setup-token", "Set up a long-lived authentication token"),
                new("doctor", "Check the health of your Claude Code auto-updater"),
                new("update", "Check for updates and install if available"),
                new("install", "Install Claude Code native build")
                {
                    Parameters =
                    [
                        new("--help", "Display help for command") { Alias = "-h" },
                    ]
                },
            ],
            Parameters =
            [
                new("--debug", "Enable debug mode with optional category filtering") { Alias = "-d" },
                new("--verbose", "Override verbose mode setting from config"),
                new("--print", "Print response and exit") { Alias = "-p" },
                new("--output-format", "Output format (text, json, stream-json)"),
                new("--include-partial-messages", "Include partial message chunks"),
                new("--input-format", "Input format (text, stream-json)"),
                new("--mcp-debug", "[DEPRECATED] Enable MCP debug mode"),
                new("--dangerously-skip-permissions", "Bypass all permission checks"),
                new("--allow-dangerously-skip-permissions", "Enable bypassing permissions as an option"),
                new("--replay-user-messages", "Re-emit user messages from stdin"),
                new("--allowedTools", "Comma or space-separated list of allowed tools") { Alias = "--allowed-tools" },
                new("--disallowedTools", "Comma or space-separated list of denied tools") { Alias = "--disallowed-tools" },
                new("--mcp-config", "Load MCP servers from JSON files or strings"),
                new("--system-prompt", "System prompt to use for the session"),
                new("--append-system-prompt", "Append a system prompt to the default"),
                new("--permission-mode", "Permission mode (acceptEdits, bypassPermissions, default, plan)"),
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
                new("--version", "Output the version number") { Alias = "-v" },
                new("--help", "Display help for command") { Alias = "-h" },
            ]
        };
    }
}

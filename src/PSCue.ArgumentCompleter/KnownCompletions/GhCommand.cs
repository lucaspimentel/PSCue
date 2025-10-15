// Usage: gh <command> <subcommand> [flags]
//
// Core commands:
//    auth          Authenticate gh and git with GitHub
//    browse        Open repositories, issues, pull requests, and more in the browser
//    codespace     Connect to and manage codespaces
//    gist          Manage gists
//    issue         Manage issues
//    org           Manage organizations
//    pr            Manage pull requests
//    project       Work with GitHub Projects
//    release       Manage releases
//    repo          Manage repositories
//
// GitHub Actions commands:
//    cache         Manage GitHub Actions caches
//    run           View details about workflow runs
//    workflow      View details about GitHub Actions workflows
//
// Additional commands:
//    alias         Create command shortcuts
//    api           Make an authenticated GitHub API request
//    completion    Generate shell completion scripts
//    config        Manage configuration for gh
//    extension     Manage gh extensions
//    gpg-key       Manage GPG keys
//    label         Manage labels
//    search        Search for repositories, issues, and pull requests
//    secret        Manage GitHub secrets
//    ssh-key       Manage SSH keys
//    status        Print information about relevant issues, pull requests, and notifications
//    variable      Manage GitHub Actions variables

using PSCue.ArgumentCompleter.Completions;

namespace PSCue.ArgumentCompleter.KnownCompletions;

public static class GhCommand
{
    public static Command Create()
    {
        return new Command("gh")
        {
            SubCommands =
            [
                new("auth", "Authenticate gh and git with GitHub")
                {
                    SubCommands =
                    [
                        new("login", "Log in to a GitHub account")
                        {
                            Parameters =
                            [
                                new("--hostname", "Hostname to authenticate with (-h)") { Alias = "-h" },
                                new("--git-protocol", "Authentication protocol (-p)") { Alias = "-p" },
                                new("--scopes", "Additional authentication scopes (-s)") { Alias = "-s" },
                                new("--web", "Open browser to authenticate (-w)") { Alias = "-w" },
                            ]
                        },
                        new("logout", "Log out of a GitHub account")
                        {
                            Parameters =
                            [
                                new("--hostname", "Hostname to log out of (-h)") { Alias = "-h" },
                            ]
                        },
                        new("refresh", "Refresh stored authentication credentials")
                        {
                            Parameters =
                            [
                                new("--hostname", "Hostname to refresh (-h)") { Alias = "-h" },
                                new("--scopes", "Additional authentication scopes (-s)") { Alias = "-s" },
                            ]
                        },
                        new("setup-git", "Setup git with GitHub CLI"),
                        new("status", "Display active account and authentication state"),
                        new("switch", "Switch active GitHub account"),
                        new("token", "Print the authentication token gh uses")
                        {
                            Parameters =
                            [
                                new("--hostname", "Hostname for the token (-h)") { Alias = "-h" },
                            ]
                        },
                    ]
                },
                new("browse", "Open repositories, issues, pull requests, and more in the browser")
                {
                    Parameters =
                    [
                        new("--branch", "Select branch (-b)") { Alias = "-b" },
                        new("--commit", "Open commit (-c)") { Alias = "-c" },
                        new("--no-browser", "Don't open browser (-n)") { Alias = "-n" },
                        new("--repo", "Select repository (-R)") { Alias = "-R" },
                    ]
                },
                new("codespace", "Connect to and manage codespaces")
                {
                    SubCommands =
                    [
                        new("code", "Open a codespace in VS Code"),
                        new("create", "Create a codespace"),
                        new("delete", "Delete a codespace"),
                        new("list", "List codespaces"),
                        new("ssh", "SSH into a codespace"),
                        new("stop", "Stop a codespace"),
                    ]
                },
                new("gist", "Manage gists")
                {
                    SubCommands =
                    [
                        new("create", "Create a new gist")
                        {
                            Parameters =
                            [
                                new("--desc", "Description (-d)") { Alias = "-d" },
                                new("--filename", "Filename (-f)") { Alias = "-f" },
                                new("--public", "Make gist public (-p)") { Alias = "-p" },
                                new("--web", "Open in browser (-w)") { Alias = "-w" },
                            ]
                        },
                        new("clone", "Clone a gist"),
                        new("delete", "Delete a gist"),
                        new("edit", "Edit a gist"),
                        new("list", "List gists")
                        {
                            Parameters =
                            [
                                new("--public", "Show only public gists"),
                                new("--secret", "Show only secret gists"),
                            ]
                        },
                        new("view", "View a gist")
                        {
                            Parameters =
                            [
                                new("--web", "Open in browser (-w)") { Alias = "-w" },
                            ]
                        },
                    ]
                },
                new("issue", "Manage issues")
                {
                    SubCommands =
                    [
                        new("create", "Create a new issue")
                        {
                            Parameters =
                            [
                                new("--assignee", "Assign people by login (-a)") { Alias = "-a" },
                                new("--body", "Body text (-b)") { Alias = "-b" },
                                new("--label", "Add labels (-l)") { Alias = "-l" },
                                new("--title", "Title (-t)") { Alias = "-t" },
                                new("--web", "Open in browser (-w)") { Alias = "-w" },
                            ]
                        },
                        new("close", "Close an issue"),
                        new("comment", "Add a comment to an issue"),
                        new("delete", "Delete an issue"),
                        new("edit", "Edit an issue"),
                        new("list", "List issues")
                        {
                            Parameters =
                            [
                                new("--assignee", "Filter by assignee (-a)") { Alias = "-a" },
                                new("--author", "Filter by author (-A)") { Alias = "-A" },
                                new("--label", "Filter by label (-l)") { Alias = "-l" },
                                new("--state", "Filter by state (-s)") { Alias = "-s" },
                                new("--limit", "Maximum number to fetch (-L)") { Alias = "-L" },
                            ]
                        },
                        new("reopen", "Reopen an issue"),
                        new("status", "Show status of relevant issues"),
                        new("view", "View an issue")
                        {
                            Parameters =
                            [
                                new("--comments", "View comments (-c)") { Alias = "-c" },
                                new("--web", "Open in browser (-w)") { Alias = "-w" },
                            ]
                        },
                    ],
                    Parameters =
                    [
                        new("--repo", "Select repository (-R)") { Alias = "-R" },
                    ]
                },
                new("org", "Manage organizations")
                {
                    SubCommands =
                    [
                        new("list", "List organizations for the authenticated user"),
                    ]
                },
                new("pr", "Manage pull requests")
                {
                    SubCommands =
                    [
                        new("create", "Create a pull request")
                        {
                            Parameters =
                            [
                                new("--assignee", "Assign people by login (-a)") { Alias = "-a" },
                                new("--base", "Base branch (-B)") { Alias = "-B" },
                                new("--body", "Body text (-b)") { Alias = "-b" },
                                new("--draft", "Mark as draft (-d)") { Alias = "-d" },
                                new("--fill", "Use commit message for title and body (-f)") { Alias = "-f" },
                                new("--head", "Head branch (-H)") { Alias = "-H" },
                                new("--label", "Add labels (-l)") { Alias = "-l" },
                                new("--title", "Title (-t)") { Alias = "-t" },
                                new("--web", "Open in browser (-w)") { Alias = "-w" },
                            ]
                        },
                        new("checkout", "Check out a pull request in git"),
                        new("checks", "Show CI status for a single pull request")
                        {
                            Parameters =
                            [
                                new("--web", "Open in browser (-w)") { Alias = "-w" },
                            ]
                        },
                        new("close", "Close a pull request"),
                        new("comment", "Add a comment to a pull request"),
                        new("diff", "View changes in a pull request")
                        {
                            Parameters =
                            [
                                new("--color", "Use color in diff output"),
                                new("--patch", "Display in patch format"),
                            ]
                        },
                        new("edit", "Edit a pull request"),
                        new("list", "List pull requests")
                        {
                            Parameters =
                            [
                                new("--assignee", "Filter by assignee (-a)") { Alias = "-a" },
                                new("--author", "Filter by author (-A)") { Alias = "-A" },
                                new("--base", "Filter by base branch (-B)") { Alias = "-B" },
                                new("--label", "Filter by label (-l)") { Alias = "-l" },
                                new("--state", "Filter by state (-s)") { Alias = "-s" },
                                new("--limit", "Maximum number to fetch (-L)") { Alias = "-L" },
                            ]
                        },
                        new("merge", "Merge a pull request")
                        {
                            Parameters =
                            [
                                new("--merge", "Merge commit (-m)") { Alias = "-m" },
                                new("--rebase", "Rebase and merge (-r)") { Alias = "-r" },
                                new("--squash", "Squash and merge (-s)") { Alias = "-s" },
                                new("--delete-branch", "Delete branch after merge (-d)") { Alias = "-d" },
                            ]
                        },
                        new("ready", "Mark a pull request as ready for review"),
                        new("reopen", "Reopen a pull request"),
                        new("review", "Add a review to a pull request")
                        {
                            Parameters =
                            [
                                new("--approve", "Approve pull request (-a)") { Alias = "-a" },
                                new("--comment", "Comment on pull request (-c)") { Alias = "-c" },
                                new("--request-changes", "Request changes (-r)") { Alias = "-r" },
                            ]
                        },
                        new("status", "Show status of relevant pull requests"),
                        new("view", "View a pull request")
                        {
                            Parameters =
                            [
                                new("--comments", "View comments (-c)") { Alias = "-c" },
                                new("--web", "Open in browser (-w)") { Alias = "-w" },
                            ]
                        },
                    ],
                    Parameters =
                    [
                        new("--repo", "Select repository (-R)") { Alias = "-R" },
                    ]
                },
                new("project", "Work with GitHub Projects"),
                new("release", "Manage releases")
                {
                    SubCommands =
                    [
                        new("create", "Create a new release")
                        {
                            Parameters =
                            [
                                new("--draft", "Mark as draft (-d)") { Alias = "-d" },
                                new("--notes", "Release notes (-n)") { Alias = "-n" },
                                new("--prerelease", "Mark as prerelease (-p)") { Alias = "-p" },
                                new("--title", "Release title (-t)") { Alias = "-t" },
                            ]
                        },
                        new("delete", "Delete a release"),
                        new("download", "Download release assets"),
                        new("list", "List releases")
                        {
                            Parameters =
                            [
                                new("--limit", "Maximum number to fetch (-L)") { Alias = "-L" },
                            ]
                        },
                        new("upload", "Upload release assets"),
                        new("view", "View a release")
                        {
                            Parameters =
                            [
                                new("--web", "Open in browser (-w)") { Alias = "-w" },
                            ]
                        },
                    ]
                },
                new("repo", "Manage repositories")
                {
                    SubCommands =
                    [
                        new("create", "Create a new repository")
                        {
                            Parameters =
                            [
                                new("--public", "Make repository public"),
                                new("--private", "Make repository private"),
                                new("--internal", "Make repository internal"),
                                new("--description", "Description (-d)") { Alias = "-d" },
                                new("--homepage", "Homepage URL (-h)") { Alias = "-h" },
                                new("--clone", "Clone repository after creation"),
                            ]
                        },
                        new("clone", "Clone a repository locally"),
                        new("delete", "Delete a repository"),
                        new("fork", "Create a fork of a repository")
                        {
                            Parameters =
                            [
                                new("--clone", "Clone fork after creation"),
                                new("--remote", "Add remote for fork"),
                            ]
                        },
                        new("list", "List repositories owned by user or organization")
                        {
                            Parameters =
                            [
                                new("--limit", "Maximum number to fetch (-L)") { Alias = "-L" },
                                new("--source", "Show only non-forks"),
                                new("--fork", "Show only forks"),
                            ]
                        },
                        new("rename", "Rename a repository"),
                        new("sync", "Sync a repository"),
                        new("view", "View a repository")
                        {
                            Parameters =
                            [
                                new("--web", "Open in browser (-w)") { Alias = "-w" },
                            ]
                        },
                    ]
                },
                new("run", "View details about workflow runs")
                {
                    SubCommands =
                    [
                        new("list", "List workflow runs")
                        {
                            Parameters =
                            [
                                new("--limit", "Maximum number to fetch (-L)") { Alias = "-L" },
                                new("--workflow", "Filter by workflow (-w)") { Alias = "-w" },
                            ]
                        },
                        new("view", "View a workflow run")
                        {
                            Parameters =
                            [
                                new("--web", "Open in browser (-w)") { Alias = "-w" },
                                new("--log", "View full log"),
                            ]
                        },
                        new("watch", "Watch a workflow run"),
                        new("rerun", "Rerun a workflow run"),
                        new("cancel", "Cancel a workflow run"),
                    ]
                },
                new("workflow", "View details about GitHub Actions workflows")
                {
                    SubCommands =
                    [
                        new("list", "List workflows"),
                        new("view", "View a workflow")
                        {
                            Parameters =
                            [
                                new("--web", "Open in browser (-w)") { Alias = "-w" },
                            ]
                        },
                        new("run", "Run a workflow"),
                        new("enable", "Enable a workflow"),
                        new("disable", "Disable a workflow"),
                    ]
                },
                new("alias", "Create command shortcuts")
                {
                    SubCommands =
                    [
                        new("set", "Create a shortcut for a gh command"),
                        new("delete", "Delete an alias"),
                        new("list", "List aliases"),
                    ]
                },
                new("api", "Make an authenticated GitHub API request")
                {
                    Parameters =
                    [
                        new("--header", "Add HTTP header (-H)") { Alias = "-H" },
                        new("--method", "HTTP method (-X)") { Alias = "-X" },
                        new("--field", "Add form field (-F)") { Alias = "-F" },
                    ]
                },
                new("completion", "Generate shell completion scripts")
                {
                    Parameters =
                    [
                        new("--shell", "Shell type (bash, zsh, fish, powershell) (-s)") { Alias = "-s" },
                    ]
                },
                new("config", "Manage configuration for gh")
                {
                    SubCommands =
                    [
                        new("get", "Get configuration value"),
                        new("set", "Set configuration value"),
                        new("list", "List configuration values"),
                    ]
                },
                new("extension", "Manage gh extensions")
                {
                    SubCommands =
                    [
                        new("install", "Install an extension"),
                        new("list", "List installed extensions"),
                        new("remove", "Remove an extension"),
                        new("upgrade", "Upgrade extensions"),
                    ]
                },
                new("label", "Manage labels")
                {
                    SubCommands =
                    [
                        new("create", "Create a label"),
                        new("delete", "Delete a label"),
                        new("edit", "Edit a label"),
                        new("list", "List labels"),
                    ]
                },
                new("search", "Search for repositories, issues, and pull requests")
                {
                    SubCommands =
                    [
                        new("repos", "Search for repositories"),
                        new("issues", "Search for issues"),
                        new("prs", "Search for pull requests"),
                    ]
                },
                new("secret", "Manage GitHub secrets")
                {
                    SubCommands =
                    [
                        new("set", "Set a secret"),
                        new("delete", "Delete a secret"),
                        new("list", "List secrets"),
                    ]
                },
                new("status", "Print information about relevant issues, pull requests, and notifications"),
            ],
            Parameters =
            [
                new("--help", "Show help for command"),
                new("--version", "Show gh version"),
            ]
        };
    }
}

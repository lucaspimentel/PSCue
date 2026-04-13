using PSCue.Shared.Completions;

namespace PSCue.Shared.KnownCompletions;

public static class GhCommand
{
    public static Command Create()
    {
        // Common formatting parameters used across many subcommands
        var jsonParam = new CommandParameter("--json", "Output JSON with specified fields") { RequiresValue = true };
        var jqParam = new CommandParameter("--jq", "Filter JSON output using a jq expression (-q)") { Alias = "-q", RequiresValue = true };
        var templateParam = new CommandParameter("--template", "Format JSON output using a Go template (-t)") { Alias = "-t", RequiresValue = true };
        var webParam = new CommandParameter("--web", "Open in browser (-w)") { Alias = "-w" };
        var repoParam = new CommandParameter("--repo", "Select repository (-R)") { Alias = "-R", RequiresValue = true };
        var limitParam = new CommandParameter("--limit", "Maximum number to fetch (-L)") { Alias = "-L", RequiresValue = true };

        // Domain-specific --json parameters with known fields
        var prJsonParam = CreateJsonParam(
            "additions", "assignees", "author", "autoMergeRequest", "baseRefName", "baseRefOid",
            "body", "changedFiles", "closed", "closedAt", "closingIssuesReferences", "comments",
            "commits", "createdAt", "deletions", "files", "fullDatabaseId", "headRefName",
            "headRefOid", "headRepository", "headRepositoryOwner", "id", "isCrossRepository",
            "isDraft", "labels", "latestReviews", "maintainerCanModify", "mergeCommit",
            "mergeStateStatus", "mergeable", "mergedAt", "mergedBy", "milestone", "number",
            "potentialMergeCommit", "projectCards", "projectItems", "reactionGroups",
            "reviewDecision", "reviewRequests", "reviews", "state", "statusCheckRollup",
            "title", "updatedAt", "url");

        var issueJsonParam = CreateJsonParam(
            "assignees", "author", "body", "closed", "closedAt",
            "closedByPullRequestsReferences", "comments", "createdAt", "id", "isPinned",
            "labels", "milestone", "number", "projectCards", "projectItems", "reactionGroups",
            "state", "stateReason", "title", "updatedAt", "url");

        var runJsonParam = CreateJsonParam(
            "attempt", "conclusion", "createdAt", "databaseId", "displayTitle", "event",
            "headBranch", "headSha", "name", "number", "startedAt", "status", "updatedAt",
            "url", "workflowDatabaseId", "workflowName");

        var releaseJsonParam = CreateJsonParam(
            "apiUrl", "assets", "author", "body", "createdAt", "databaseId", "id", "isDraft",
            "isImmutable", "isPrerelease", "name", "publishedAt", "tagName", "tarballUrl",
            "targetCommitish", "uploadUrl", "url", "zipballUrl");

        var releaseListJsonParam = CreateJsonParam(
            "createdAt", "isDraft", "isImmutable", "isLatest", "isPrerelease", "name",
            "publishedAt", "tagName");

        return new Command("gh")
        {
            SubCommands =
            [
                // ── Core commands ──
                CreateAuthCommand(),
                CreateBrowseCommand(repoParam),
                CreateCodespaceCommand(),
                CreateGistCommand(webParam),
                CreateIssueCommand(repoParam, limitParam, webParam, issueJsonParam, jqParam, templateParam),
                CreateOrgCommand(),
                CreatePrCommand(repoParam, limitParam, webParam, prJsonParam, jqParam, templateParam),
                new("project", "Work with GitHub Projects"),
                CreateReleaseCommand(repoParam, limitParam, webParam, releaseJsonParam, releaseListJsonParam, jqParam, templateParam),
                CreateRepoCommand(repoParam, limitParam, webParam, jsonParam, jqParam, templateParam),

                // ── GitHub Actions commands ──
                CreateCacheCommand(repoParam),
                CreateRunCommand(repoParam, limitParam, webParam, runJsonParam, jqParam, templateParam),
                CreateWorkflowCommand(repoParam, webParam),

                // ── Additional commands ──
                CreateAliasCommand(),
                CreateApiCommand(),
                new("attestation", "Work with artifact attestations"),
                CreateCompletionCommand(),
                CreateConfigCommand(),
                CreateExtensionCommand(),
                new("gpg-key", "Manage GPG keys")
                {
                    SubCommands =
                    [
                        new("add", "Add a GPG key"),
                        new("delete", "Delete a GPG key"),
                        new("list", "List GPG keys"),
                    ],
                },
                CreateLabelCommand(repoParam),
                CreateSearchCommand(limitParam, webParam, jsonParam, jqParam, templateParam),
                CreateSecretCommand(repoParam),
                new("ssh-key", "Manage SSH keys")
                {
                    SubCommands =
                    [
                        new("add", "Add an SSH key"),
                        new("delete", "Delete an SSH key"),
                        new("list", "List SSH keys"),
                    ],
                },
                new("status", "Print information about relevant issues, pull requests, and notifications"),
                CreateVariableCommand(repoParam),
            ],
            Parameters =
            [
                new("--help", "Show help for command"),
                new("--version", "Show gh version"),
            ],
        };
    }

    private static Command CreateAuthCommand()
    {
        return new Command("auth", "Authenticate gh and git with GitHub")
        {
            SubCommands =
            [
                new("login", "Log in to a GitHub account")
                {
                    Parameters =
                    [
                        new("--hostname", "Hostname to authenticate with (-h)") { Alias = "-h", RequiresValue = true },
                        new("--git-protocol", "Authentication protocol (-p)") { Alias = "-p", RequiresValue = true },
                        new("--scopes", "Additional authentication scopes (-s)") { Alias = "-s", RequiresValue = true },
                        new("--web", "Open browser to authenticate (-w)") { Alias = "-w" },
                        new("--with-token", "Read token from standard input"),
                        new("--skip-ssh-key", "Skip SSH key prompt"),
                        new("--clipboard", "Copy device code to clipboard (-c)") { Alias = "-c" },
                        new("--insecure-storage", "Save credentials in plain text"),
                    ],
                },
                new("logout", "Log out of a GitHub account")
                {
                    Parameters =
                    [
                        new("--hostname", "Hostname to log out of (-h)") { Alias = "-h", RequiresValue = true },
                        new("--user", "Account to log out of (-u)") { Alias = "-u", RequiresValue = true },
                    ],
                },
                new("refresh", "Refresh stored authentication credentials")
                {
                    Parameters =
                    [
                        new("--hostname", "Hostname to refresh (-h)") { Alias = "-h", RequiresValue = true },
                        new("--scopes", "Additional authentication scopes (-s)") { Alias = "-s", RequiresValue = true },
                        new("--remove-scopes", "Scopes to remove (-r)") { Alias = "-r", RequiresValue = true },
                        new("--reset-scopes", "Reset scopes to default"),
                        new("--clipboard", "Copy device code to clipboard (-c)") { Alias = "-c" },
                        new("--insecure-storage", "Save credentials in plain text"),
                    ],
                },
                new("setup-git", "Setup git with GitHub CLI"),
                new("status", "Display active account and authentication state")
                {
                    Parameters =
                    [
                        new("--hostname", "Check specific hostname (-h)") { Alias = "-h", RequiresValue = true },
                        new("--show-token", "Display the auth token (-t)") { Alias = "-t" },
                        new("--active", "Display active account only (-a)") { Alias = "-a" },
                    ],
                },
                new("switch", "Switch active GitHub account")
                {
                    Parameters =
                    [
                        new("--hostname", "Hostname to switch account for (-h)") { Alias = "-h", RequiresValue = true },
                        new("--user", "Account to switch to (-u)") { Alias = "-u", RequiresValue = true },
                    ],
                },
                new("token", "Print the authentication token")
                {
                    Parameters =
                    [
                        new("--hostname", "Hostname for the token (-h)") { Alias = "-h", RequiresValue = true },
                        new("--user", "Account to output token for (-u)") { Alias = "-u", RequiresValue = true },
                    ],
                },
            ],
        };
    }

    private static Command CreateBrowseCommand(CommandParameter repoParam)
    {
        return new Command("browse", "Open repositories, issues, pull requests, and more in the browser")
        {
            Parameters =
            [
                new("--branch", "Select branch (-b)") { Alias = "-b", RequiresValue = true },
                new("--commit", "Open commit (-c)") { Alias = "-c" },
                new("--no-browser", "Print URL instead (-n)") { Alias = "-n" },
                new("--actions", "Open repository actions (-a)") { Alias = "-a" },
                new("--projects", "Open repository projects (-p)") { Alias = "-p" },
                new("--releases", "Open repository releases (-r)") { Alias = "-r" },
                new("--settings", "Open repository settings (-s)") { Alias = "-s" },
                new("--wiki", "Open repository wiki (-w)") { Alias = "-w" },
                new("--blame", "Open blame view for a file"),
                repoParam,
            ],
        };
    }

    private static Command CreateCodespaceCommand()
    {
        return new Command("codespace", "Connect to and manage codespaces")
        {
            SubCommands =
            [
                new("code", "Open a codespace in VS Code"),
                new("cp", "Copy files between local and remote"),
                new("create", "Create a codespace"),
                new("delete", "Delete codespaces"),
                new("edit", "Edit a codespace"),
                new("jupyter", "Open a codespace in JupyterLab"),
                new("list", "List codespaces"),
                new("logs", "Access codespace logs"),
                new("ports", "List ports in a codespace"),
                new("rebuild", "Rebuild a codespace"),
                new("ssh", "SSH into a codespace"),
                new("stop", "Stop a running codespace"),
                new("view", "View details about a codespace"),
            ],
        };
    }

    private static Command CreateGistCommand(CommandParameter webParam)
    {
        return new Command("gist", "Manage gists")
        {
            SubCommands =
            [
                new("create", "Create a new gist")
                {
                    Parameters =
                    [
                        new("--desc", "Description (-d)") { Alias = "-d", RequiresValue = true },
                        new("--filename", "Filename for stdin input (-f)") { Alias = "-f", RequiresValue = true },
                        new("--public", "Make gist public (-p)") { Alias = "-p" },
                        webParam,
                    ],
                },
                new("clone", "Clone a gist"),
                new("delete", "Delete a gist"),
                new("edit", "Edit a gist")
                {
                    Parameters =
                    [
                        new("--add", "Add a new file (-a)") { Alias = "-a", RequiresValue = true },
                        new("--desc", "New description (-d)") { Alias = "-d", RequiresValue = true },
                        new("--filename", "Select a file to edit (-f)") { Alias = "-f", RequiresValue = true },
                        new("--remove", "Remove a file (-r)") { Alias = "-r", RequiresValue = true },
                    ],
                },
                new("list", "List gists")
                {
                    Parameters =
                    [
                        new("--limit", "Maximum number to fetch (-L)") { Alias = "-L", RequiresValue = true },
                        new("--public", "Show only public gists"),
                        new("--secret", "Show only secret gists"),
                        new("--filter", "Filter using a regular expression") { RequiresValue = true },
                        new("--include-content", "Include file content when filtering"),
                    ],
                },
                new("view", "View a gist")
                {
                    Parameters =
                    [
                        new("--filename", "Display a single file (-f)") { Alias = "-f", RequiresValue = true },
                        new("--files", "List file names"),
                        new("--raw", "Print raw contents (-r)") { Alias = "-r" },
                        webParam,
                    ],
                },
            ],
        };
    }

    private static Command CreateIssueCommand(
        CommandParameter repoParam, CommandParameter limitParam, CommandParameter webParam,
        CommandParameter jsonParam, CommandParameter jqParam, CommandParameter templateParam)
    {
        return new Command("issue", "Manage issues")
        {
            SubCommands =
            [
                new("create", "Create a new issue")
                {
                    Parameters =
                    [
                        new("--assignee", "Assign people by login (-a)") { Alias = "-a", RequiresValue = true },
                        new("--body", "Body text (-b)") { Alias = "-b", RequiresValue = true },
                        new("--body-file", "Read body from file (-F)") { Alias = "-F", RequiresValue = true },
                        new("--editor", "Open text editor (-e)") { Alias = "-e" },
                        new("--label", "Add labels (-l)") { Alias = "-l", RequiresValue = true },
                        new("--milestone", "Add to milestone (-m)") { Alias = "-m", RequiresValue = true },
                        new("--project", "Add to project (-p)") { Alias = "-p", RequiresValue = true },
                        new("--template", "Template name (-T)") { Alias = "-T", RequiresValue = true },
                        new("--title", "Title (-t)") { Alias = "-t", RequiresValue = true },
                        webParam,
                        repoParam,
                    ],
                },
                new("close", "Close an issue")
                {
                    Parameters =
                    [
                        new("--comment", "Leave a closing comment (-c)") { Alias = "-c", RequiresValue = true },
                        new("--reason", "Reason for closing (-r)") { Alias = "-r", RequiresValue = true },
                        new("--duplicate-of", "Mark as duplicate") { RequiresValue = true },
                        repoParam,
                    ],
                },
                new("comment", "Add a comment to an issue")
                {
                    Parameters =
                    [
                        new("--body", "Comment body text (-b)") { Alias = "-b", RequiresValue = true },
                        new("--body-file", "Read body from file (-F)") { Alias = "-F", RequiresValue = true },
                        new("--editor", "Open text editor (-e)") { Alias = "-e" },
                        new("--edit-last", "Edit last comment"),
                        new("--delete-last", "Delete last comment"),
                        webParam,
                        repoParam,
                    ],
                },
                new("delete", "Delete an issue")
                {
                    Parameters =
                    [
                        new("--yes", "Confirm deletion without prompting"),
                        repoParam,
                    ],
                },
                new("edit", "Edit an issue")
                {
                    Parameters =
                    [
                        new("--title", "Set new title (-t)") { Alias = "-t", RequiresValue = true },
                        new("--body", "Set new body (-b)") { Alias = "-b", RequiresValue = true },
                        new("--body-file", "Read body from file (-F)") { Alias = "-F", RequiresValue = true },
                        new("--add-assignee", "Add assignees") { RequiresValue = true },
                        new("--remove-assignee", "Remove assignees") { RequiresValue = true },
                        new("--add-label", "Add labels") { RequiresValue = true },
                        new("--remove-label", "Remove labels") { RequiresValue = true },
                        new("--add-project", "Add to project") { RequiresValue = true },
                        new("--remove-project", "Remove from project") { RequiresValue = true },
                        new("--milestone", "Edit milestone (-m)") { Alias = "-m", RequiresValue = true },
                        new("--remove-milestone", "Remove milestone"),
                        repoParam,
                    ],
                },
                new("list", "List issues")
                {
                    Parameters =
                    [
                        new("--assignee", "Filter by assignee (-a)") { Alias = "-a", RequiresValue = true },
                        new("--author", "Filter by author (-A)") { Alias = "-A", RequiresValue = true },
                        new("--label", "Filter by label (-l)") { Alias = "-l", RequiresValue = true },
                        new("--state", "Filter by state (-s)") { Alias = "-s", RequiresValue = true },
                        new("--milestone", "Filter by milestone (-m)") { Alias = "-m", RequiresValue = true },
                        new("--search", "Search with query (-S)") { Alias = "-S", RequiresValue = true },
                        new("--mention", "Filter by mention") { RequiresValue = true },
                        new("--app", "Filter by GitHub App author") { RequiresValue = true },
                        limitParam,
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                        repoParam,
                    ],
                },
                new("reopen", "Reopen an issue")
                {
                    Parameters =
                    [
                        new("--comment", "Add a reopening comment (-c)") { Alias = "-c", RequiresValue = true },
                        repoParam,
                    ],
                },
                new("status", "Show status of relevant issues")
                {
                    Parameters =
                    [
                        jsonParam,
                        jqParam,
                        templateParam,
                        repoParam,
                    ],
                },
                new("view", "View an issue")
                {
                    Parameters =
                    [
                        new("--comments", "View comments (-c)") { Alias = "-c" },
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                        repoParam,
                    ],
                },
            ],
            Parameters =
            [
                repoParam,
            ],
        };
    }

    private static Command CreateOrgCommand()
    {
        return new Command("org", "Manage organizations")
        {
            SubCommands =
            [
                new("list", "List organizations for the authenticated user"),
            ],
        };
    }

    private static Command CreatePrCommand(
        CommandParameter repoParam, CommandParameter limitParam, CommandParameter webParam,
        CommandParameter jsonParam, CommandParameter jqParam, CommandParameter templateParam)
    {
        return new Command("pr", "Manage pull requests")
        {
            SubCommands =
            [
                new("create", "Create a pull request")
                {
                    Parameters =
                    [
                        new("--assignee", "Assign people by login (-a)") { Alias = "-a", RequiresValue = true },
                        new("--base", "Base branch (-B)") { Alias = "-B", RequiresValue = true },
                        new("--body", "Body text (-b)") { Alias = "-b", RequiresValue = true },
                        new("--body-file", "Read body from file (-F)") { Alias = "-F", RequiresValue = true },
                        new("--draft", "Mark as draft (-d)") { Alias = "-d" },
                        new("--editor", "Open text editor (-e)") { Alias = "-e" },
                        new("--fill", "Use commit info for title and body (-f)") { Alias = "-f" },
                        new("--fill-first", "Use first commit info"),
                        new("--fill-verbose", "Use commits msg+body for description"),
                        new("--head", "Head branch (-H)") { Alias = "-H", RequiresValue = true },
                        new("--label", "Add labels (-l)") { Alias = "-l", RequiresValue = true },
                        new("--milestone", "Add to milestone (-m)") { Alias = "-m", RequiresValue = true },
                        new("--no-maintainer-edit", "Disable maintainer edit"),
                        new("--project", "Add to project (-p)") { Alias = "-p", RequiresValue = true },
                        new("--reviewer", "Request reviews (-r)") { Alias = "-r", RequiresValue = true },
                        new("--template", "Template file (-T)") { Alias = "-T", RequiresValue = true },
                        new("--title", "Title (-t)") { Alias = "-t", RequiresValue = true },
                        new("--dry-run", "Print details instead of creating"),
                        webParam,
                        repoParam,
                    ],
                },
                new("checkout", "Check out a pull request in git")
                {
                    Parameters =
                    [
                        new("--branch", "Local branch name (-b)") { Alias = "-b", RequiresValue = true },
                        new("--detach", "Checkout with detached HEAD"),
                        new("--force", "Reset local branch to latest (-f)") { Alias = "-f" },
                        new("--recurse-submodules", "Update all submodules"),
                        repoParam,
                    ],
                },
                new("checks", "Show CI status for a pull request")
                {
                    Parameters =
                    [
                        new("--watch", "Watch checks until they finish"),
                        new("--fail-fast", "Exit watch mode on first failure"),
                        new("--interval", "Refresh interval in seconds (-i)") { Alias = "-i", RequiresValue = true },
                        new("--required", "Only show required checks"),
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                        repoParam,
                    ],
                },
                new("close", "Close a pull request")
                {
                    Parameters =
                    [
                        new("--comment", "Leave a closing comment (-c)") { Alias = "-c", RequiresValue = true },
                        new("--delete-branch", "Delete branch after close (-d)") { Alias = "-d" },
                        repoParam,
                    ],
                },
                new("comment", "Add a comment to a pull request")
                {
                    Parameters =
                    [
                        new("--body", "Comment body text (-b)") { Alias = "-b", RequiresValue = true },
                        new("--body-file", "Read body from file (-F)") { Alias = "-F", RequiresValue = true },
                        new("--editor", "Open text editor (-e)") { Alias = "-e" },
                        new("--edit-last", "Edit last comment"),
                        new("--delete-last", "Delete last comment"),
                        webParam,
                        repoParam,
                    ],
                },
                new("diff", "View changes in a pull request")
                {
                    Parameters =
                    [
                        new("--color", "Use color in diff output") { RequiresValue = true },
                        new("--patch", "Display in patch format"),
                        new("--name-only", "Display only names of changed files"),
                        new("--exclude", "Exclude files matching glob pattern (-e)") { Alias = "-e", RequiresValue = true },
                        webParam,
                        repoParam,
                    ],
                },
                new("edit", "Edit a pull request")
                {
                    Parameters =
                    [
                        new("--title", "Set new title (-t)") { Alias = "-t", RequiresValue = true },
                        new("--body", "Set new body (-b)") { Alias = "-b", RequiresValue = true },
                        new("--body-file", "Read body from file (-F)") { Alias = "-F", RequiresValue = true },
                        new("--base", "Change base branch (-B)") { Alias = "-B", RequiresValue = true },
                        new("--add-assignee", "Add assignees") { RequiresValue = true },
                        new("--remove-assignee", "Remove assignees") { RequiresValue = true },
                        new("--add-label", "Add labels") { RequiresValue = true },
                        new("--remove-label", "Remove labels") { RequiresValue = true },
                        new("--add-project", "Add to project") { RequiresValue = true },
                        new("--remove-project", "Remove from project") { RequiresValue = true },
                        new("--add-reviewer", "Add reviewers") { RequiresValue = true },
                        new("--remove-reviewer", "Remove reviewers") { RequiresValue = true },
                        new("--milestone", "Edit milestone (-m)") { Alias = "-m", RequiresValue = true },
                        new("--remove-milestone", "Remove milestone"),
                        repoParam,
                    ],
                },
                new("list", "List pull requests")
                {
                    Parameters =
                    [
                        new("--assignee", "Filter by assignee (-a)") { Alias = "-a", RequiresValue = true },
                        new("--author", "Filter by author (-A)") { Alias = "-A", RequiresValue = true },
                        new("--base", "Filter by base branch (-B)") { Alias = "-B", RequiresValue = true },
                        new("--head", "Filter by head branch (-H)") { Alias = "-H", RequiresValue = true },
                        new("--label", "Filter by label (-l)") { Alias = "-l", RequiresValue = true },
                        new("--state", "Filter by state (-s)") { Alias = "-s", RequiresValue = true },
                        new("--draft", "Filter by draft state (-d)") { Alias = "-d" },
                        new("--search", "Search with query (-S)") { Alias = "-S", RequiresValue = true },
                        new("--app", "Filter by GitHub App author") { RequiresValue = true },
                        limitParam,
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                        repoParam,
                    ],
                },
                new("merge", "Merge a pull request")
                {
                    Parameters =
                    [
                        new("--merge", "Merge commit (-m)") { Alias = "-m" },
                        new("--rebase", "Rebase and merge (-r)") { Alias = "-r" },
                        new("--squash", "Squash and merge (-s)") { Alias = "-s" },
                        new("--delete-branch", "Delete branch after merge (-d)") { Alias = "-d" },
                        new("--auto", "Automatically merge when requirements are met (-A)") { Alias = "-A" },
                        new("--disable-auto", "Disable auto-merge"),
                        new("--admin", "Use admin privileges to merge"),
                        new("--body", "Body text for merge commit (-b)") { Alias = "-b", RequiresValue = true },
                        new("--body-file", "Read body from file (-F)") { Alias = "-F", RequiresValue = true },
                        new("--subject", "Subject text for merge commit (-t)") { Alias = "-t", RequiresValue = true },
                        new("--match-head-commit", "Required head commit SHA") { RequiresValue = true },
                        new("--author-email", "Email for merge commit author (-A)") { RequiresValue = true },
                        repoParam,
                    ],
                },
                new("ready", "Mark a pull request as ready for review")
                {
                    Parameters =
                    [
                        new("--undo", "Convert to draft"),
                        repoParam,
                    ],
                },
                new("reopen", "Reopen a pull request")
                {
                    Parameters =
                    [
                        new("--comment", "Add a reopening comment (-c)") { Alias = "-c", RequiresValue = true },
                        repoParam,
                    ],
                },
                new("review", "Add a review to a pull request")
                {
                    Parameters =
                    [
                        new("--approve", "Approve pull request (-a)") { Alias = "-a" },
                        new("--comment", "Comment on pull request (-c)") { Alias = "-c" },
                        new("--request-changes", "Request changes (-r)") { Alias = "-r" },
                        new("--body", "Body of review (-b)") { Alias = "-b", RequiresValue = true },
                        new("--body-file", "Read body from file (-F)") { Alias = "-F", RequiresValue = true },
                        repoParam,
                    ],
                },
                new("status", "Show status of relevant pull requests")
                {
                    Parameters =
                    [
                        new("--conflict-status", "Display merge conflict status (-c)") { Alias = "-c" },
                        jsonParam,
                        jqParam,
                        templateParam,
                        repoParam,
                    ],
                },
                new("view", "View a pull request")
                {
                    Parameters =
                    [
                        new("--comments", "View comments (-c)") { Alias = "-c" },
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                        repoParam,
                    ],
                },
            ],
            Parameters =
            [
                repoParam,
            ],
        };
    }

    private static Command CreateReleaseCommand(
        CommandParameter repoParam, CommandParameter limitParam, CommandParameter webParam,
        CommandParameter jsonParam, CommandParameter releaseListJsonParam,
        CommandParameter jqParam, CommandParameter templateParam)
    {
        return new Command("release", "Manage releases")
        {
            SubCommands =
            [
                new("create", "Create a new release")
                {
                    Parameters =
                    [
                        new("--draft", "Save as draft (-d)") { Alias = "-d" },
                        new("--prerelease", "Mark as prerelease (-p)") { Alias = "-p" },
                        new("--title", "Release title (-t)") { Alias = "-t", RequiresValue = true },
                        new("--notes", "Release notes (-n)") { Alias = "-n", RequiresValue = true },
                        new("--notes-file", "Read notes from file (-F)") { Alias = "-F", RequiresValue = true },
                        new("--notes-from-tag", "Fetch notes from tag annotation"),
                        new("--notes-start-tag", "Starting point for release notes") { RequiresValue = true },
                        new("--target", "Target branch or commit") { RequiresValue = true },
                        new("--latest", "Mark as latest release"),
                        new("--generate-notes", "Auto-generate title and notes"),
                        new("--discussion-category", "Start a discussion") { RequiresValue = true },
                        new("--verify-tag", "Abort if tag doesn't exist"),
                        new("--fail-on-no-commits", "Fail if no new commits"),
                        repoParam,
                    ],
                },
                new("delete", "Delete a release")
                {
                    Parameters =
                    [
                        new("--cleanup-tag", "Delete the tag too"),
                        new("--yes", "Skip confirmation (-y)") { Alias = "-y" },
                        repoParam,
                    ],
                },
                new("delete-asset", "Delete an asset from a release")
                {
                    Parameters =
                    [
                        new("--yes", "Skip confirmation (-y)") { Alias = "-y" },
                        repoParam,
                    ],
                },
                new("download", "Download release assets")
                {
                    Parameters =
                    [
                        new("--pattern", "Download assets matching glob (-p)") { Alias = "-p", RequiresValue = true },
                        new("--dir", "Download directory (-D)") { Alias = "-D", RequiresValue = true },
                        new("--output", "Output file (-O)") { Alias = "-O", RequiresValue = true },
                        new("--archive", "Download source archive (-A)") { Alias = "-A", RequiresValue = true },
                        new("--clobber", "Overwrite existing files"),
                        new("--skip-existing", "Skip existing files"),
                        repoParam,
                    ],
                },
                new("edit", "Edit a release")
                {
                    Parameters =
                    [
                        new("--draft", "Save as draft"),
                        new("--prerelease", "Mark as prerelease"),
                        new("--title", "Release title (-t)") { Alias = "-t", RequiresValue = true },
                        new("--notes", "Release notes (-n)") { Alias = "-n", RequiresValue = true },
                        new("--notes-file", "Read notes from file (-F)") { Alias = "-F", RequiresValue = true },
                        new("--tag", "Tag name") { RequiresValue = true },
                        new("--target", "Target branch or commit") { RequiresValue = true },
                        new("--latest", "Mark as latest release"),
                        new("--discussion-category", "Start a discussion") { RequiresValue = true },
                        new("--verify-tag", "Abort if tag doesn't exist"),
                        repoParam,
                    ],
                },
                new("list", "List releases")
                {
                    Parameters =
                    [
                        new("--exclude-drafts", "Exclude draft releases"),
                        new("--exclude-pre-releases", "Exclude pre-releases"),
                        new("--order", "Order of results (-O)") { Alias = "-O", RequiresValue = true },
                        limitParam,
                        releaseListJsonParam,
                        jqParam,
                        templateParam,
                        repoParam,
                    ],
                },
                new("upload", "Upload release assets")
                {
                    Parameters =
                    [
                        new("--clobber", "Overwrite existing assets"),
                        repoParam,
                    ],
                },
                new("view", "View a release")
                {
                    Parameters =
                    [
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                        repoParam,
                    ],
                },
            ],
            Parameters =
            [
                repoParam,
            ],
        };
    }

    private static Command CreateRepoCommand(
        CommandParameter repoParam, CommandParameter limitParam, CommandParameter webParam,
        CommandParameter jsonParam, CommandParameter jqParam, CommandParameter templateParam)
    {
        return new Command("repo", "Manage repositories")
        {
            SubCommands =
            [
                new("archive", "Archive a repository"),
                new("clone", "Clone a repository locally")
                {
                    Parameters =
                    [
                        new("--upstream-remote-name", "Name for upstream remote") { RequiresValue = true },
                        new("--no-upstream", "Skip upstream remote"),
                    ],
                },
                new("create", "Create a new repository")
                {
                    Parameters =
                    [
                        new("--public", "Make repository public"),
                        new("--private", "Make repository private"),
                        new("--internal", "Make repository internal"),
                        new("--description", "Description (-d)") { Alias = "-d", RequiresValue = true },
                        new("--homepage", "Homepage URL (-h)") { Alias = "-h", RequiresValue = true },
                        new("--clone", "Clone after creation (-c)") { Alias = "-c" },
                        new("--template", "Template repository (-p)") { Alias = "-p", RequiresValue = true },
                        new("--license", "Open source license (-l)") { Alias = "-l", RequiresValue = true },
                        new("--gitignore", "Gitignore template (-g)") { Alias = "-g", RequiresValue = true },
                        new("--source", "Path to local source (-s)") { Alias = "-s", RequiresValue = true },
                        new("--remote", "Remote name (-r)") { Alias = "-r", RequiresValue = true },
                        new("--push", "Push local commits"),
                        new("--add-readme", "Add a README file"),
                        new("--disable-issues", "Disable issues"),
                        new("--disable-wiki", "Disable wiki"),
                        new("--include-all-branches", "Include all branches from template"),
                        new("--team", "Team to grant access (-t)") { Alias = "-t", RequiresValue = true },
                    ],
                },
                new("delete", "Delete a repository")
                {
                    Parameters =
                    [
                        new("--yes", "Confirm deletion"),
                    ],
                },
                new("edit", "Edit repository settings"),
                new("fork", "Create a fork of a repository")
                {
                    Parameters =
                    [
                        new("--clone", "Clone fork after creation"),
                        new("--remote", "Add remote for fork"),
                        new("--remote-name", "Name for new remote") { RequiresValue = true },
                        new("--fork-name", "Rename the forked repository") { RequiresValue = true },
                        new("--org", "Create fork in an organization") { RequiresValue = true },
                        new("--default-branch-only", "Only include default branch"),
                    ],
                },
                new("list", "List repositories")
                {
                    Parameters =
                    [
                        new("--source", "Show only non-forks"),
                        new("--fork", "Show only forks"),
                        new("--archived", "Show only archived"),
                        new("--no-archived", "Omit archived"),
                        new("--language", "Filter by language (-l)") { Alias = "-l", RequiresValue = true },
                        new("--topic", "Filter by topic") { RequiresValue = true },
                        new("--visibility", "Filter by visibility") { RequiresValue = true },
                        limitParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                    ],
                },
                new("rename", "Rename a repository"),
                new("set-default", "Configure default repository"),
                new("sync", "Sync a repository"),
                new("unarchive", "Unarchive a repository"),
                new("view", "View a repository")
                {
                    Parameters =
                    [
                        new("--branch", "View specific branch (-b)") { Alias = "-b", RequiresValue = true },
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                    ],
                },
            ],
        };
    }

    private static Command CreateCacheCommand(CommandParameter repoParam)
    {
        return new Command("cache", "Manage GitHub Actions caches")
        {
            SubCommands =
            [
                new("delete", "Delete caches")
                {
                    Parameters =
                    [
                        new("--all", "Delete all caches (-a)") { Alias = "-a" },
                        repoParam,
                    ],
                },
                new("list", "List caches")
                {
                    Parameters =
                    [
                        new("--limit", "Maximum number to fetch (-L)") { Alias = "-L", RequiresValue = true },
                        new("--sort", "Sort caches (-S)") { Alias = "-S", RequiresValue = true },
                        new("--order", "Order of results (-O)") { Alias = "-O", RequiresValue = true },
                        repoParam,
                    ],
                },
            ],
            Parameters =
            [
                repoParam,
            ],
        };
    }

    private static Command CreateRunCommand(
        CommandParameter repoParam, CommandParameter limitParam, CommandParameter webParam,
        CommandParameter jsonParam, CommandParameter jqParam, CommandParameter templateParam)
    {
        return new Command("run", "View details about workflow runs")
        {
            SubCommands =
            [
                new("cancel", "Cancel a workflow run")
                {
                    Parameters =
                    [
                        new("--force", "Force cancel"),
                        repoParam,
                    ],
                },
                new("delete", "Delete a workflow run")
                {
                    Parameters =
                    [
                        repoParam,
                    ],
                },
                new("list", "List workflow runs")
                {
                    Parameters =
                    [
                        new("--workflow", "Filter by workflow (-w)") { Alias = "-w", RequiresValue = true },
                        new("--branch", "Filter by branch (-b)") { Alias = "-b", RequiresValue = true },
                        new("--event", "Filter by event (-e)") { Alias = "-e", RequiresValue = true },
                        new("--status", "Filter by status (-s)") { Alias = "-s", RequiresValue = true },
                        new("--user", "Filter by user (-u)") { Alias = "-u", RequiresValue = true },
                        new("--commit", "Filter by commit SHA (-c)") { Alias = "-c", RequiresValue = true },
                        new("--created", "Filter by creation date") { RequiresValue = true },
                        new("--all", "Include disabled workflows (-a)") { Alias = "-a" },
                        limitParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                        repoParam,
                    ],
                },
                new("rerun", "Rerun a workflow run")
                {
                    Parameters =
                    [
                        new("--failed", "Rerun only failed jobs"),
                        new("--job", "Rerun specific job (-j)") { Alias = "-j", RequiresValue = true },
                        new("--debug", "Rerun with debug logging (-d)") { Alias = "-d" },
                        repoParam,
                    ],
                },
                new("view", "View a workflow run")
                {
                    Parameters =
                    [
                        new("--log", "View full log"),
                        new("--log-failed", "View log for failed steps"),
                        new("--job", "View specific job (-j)") { Alias = "-j", RequiresValue = true },
                        new("--attempt", "Attempt number (-a)") { Alias = "-a", RequiresValue = true },
                        new("--exit-status", "Exit with non-zero status if run failed"),
                        new("--verbose", "Show job steps (-v)") { Alias = "-v" },
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                        repoParam,
                    ],
                },
                new("watch", "Watch a workflow run")
                {
                    Parameters =
                    [
                        new("--exit-status", "Exit with non-zero status if run fails"),
                        new("--interval", "Refresh interval in seconds (-i)") { Alias = "-i", RequiresValue = true },
                        new("--compact", "Show only relevant/failed steps"),
                        repoParam,
                    ],
                },
            ],
            Parameters =
            [
                repoParam,
            ],
        };
    }

    private static Command CreateWorkflowCommand(CommandParameter repoParam, CommandParameter webParam)
    {
        return new Command("workflow", "View details about GitHub Actions workflows")
        {
            SubCommands =
            [
                new("disable", "Disable a workflow")
                {
                    Parameters = [repoParam],
                },
                new("enable", "Enable a workflow")
                {
                    Parameters = [repoParam],
                },
                new("list", "List workflows")
                {
                    Parameters = [repoParam],
                },
                new("run", "Run a workflow")
                {
                    Parameters =
                    [
                        new("--ref", "Branch or tag name (-r)") { Alias = "-r", RequiresValue = true },
                        new("--field", "Add typed parameter (-F)") { Alias = "-F", RequiresValue = true },
                        new("--raw-field", "Add string parameter (-f)") { Alias = "-f", RequiresValue = true },
                        new("--json", "Read inputs as JSON via stdin"),
                        repoParam,
                    ],
                },
                new("view", "View a workflow")
                {
                    Parameters =
                    [
                        webParam,
                        repoParam,
                    ],
                },
            ],
            Parameters =
            [
                repoParam,
            ],
        };
    }

    private static Command CreateAliasCommand()
    {
        return new Command("alias", "Create command shortcuts")
        {
            SubCommands =
            [
                new("delete", "Delete an alias"),
                new("import", "Import aliases from a YAML file"),
                new("list", "List aliases"),
                new("set", "Create a shortcut for a gh command"),
            ],
        };
    }

    private static Command CreateApiCommand()
    {
        return new Command("api", "Make an authenticated GitHub API request")
        {
            Parameters =
            [
                new("--header", "Add HTTP header (-H)") { Alias = "-H", RequiresValue = true },
                new("--method", "HTTP method (-X)") { Alias = "-X", RequiresValue = true },
                new("--field", "Add typed parameter (-F)") { Alias = "-F", RequiresValue = true },
                new("--raw-field", "Add string parameter (-f)") { Alias = "-f", RequiresValue = true },
                new("--input", "File to use as request body") { RequiresValue = true },
                new("--jq", "Filter JSON output (-q)") { Alias = "-q", RequiresValue = true },
                new("--template", "Format JSON output (-t)") { Alias = "-t", RequiresValue = true },
                new("--paginate", "Fetch all pages of results"),
                new("--slurp", "Return array of all pages"),
                new("--silent", "Do not print response body"),
                new("--verbose", "Include full HTTP request and response"),
                new("--include", "Include HTTP response headers (-i)") { Alias = "-i" },
                new("--cache", "Cache the response") { RequiresValue = true },
                new("--hostname", "GitHub hostname") { RequiresValue = true },
                new("--preview", "Opt into API previews (-p)") { Alias = "-p", RequiresValue = true },
            ],
        };
    }

    private static Command CreateCompletionCommand()
    {
        return new Command("completion", "Generate shell completion scripts")
        {
            Parameters =
            [
                new("--shell", "Shell type (-s)") { Alias = "-s", RequiresValue = true },
            ],
        };
    }

    private static Command CreateConfigCommand()
    {
        return new Command("config", "Manage configuration for gh")
        {
            SubCommands =
            [
                new("get", "Get configuration value"),
                new("set", "Set configuration value"),
                new("list", "List configuration values"),
                new("clear-cache", "Clear CLI cache"),
            ],
        };
    }

    private static Command CreateExtensionCommand()
    {
        return new Command("extension", "Manage gh extensions")
        {
            SubCommands =
            [
                new("browse", "Browse extensions in a terminal UI"),
                new("create", "Create a new extension"),
                new("exec", "Execute an installed extension"),
                new("install", "Install an extension"),
                new("list", "List installed extensions"),
                new("remove", "Remove an extension"),
                new("search", "Search for extensions"),
                new("upgrade", "Upgrade extensions"),
            ],
        };
    }

    private static Command CreateLabelCommand(CommandParameter repoParam)
    {
        return new Command("label", "Manage labels")
        {
            SubCommands =
            [
                new("create", "Create a label")
                {
                    Parameters =
                    [
                        new("--color", "Label color (-c)") { Alias = "-c", RequiresValue = true },
                        new("--description", "Label description (-d)") { Alias = "-d", RequiresValue = true },
                        new("--force", "Update label if it exists (-f)") { Alias = "-f" },
                        repoParam,
                    ],
                },
                new("delete", "Delete a label")
                {
                    Parameters =
                    [
                        new("--yes", "Confirm deletion"),
                        repoParam,
                    ],
                },
                new("edit", "Edit a label")
                {
                    Parameters =
                    [
                        new("--color", "Label color (-c)") { Alias = "-c", RequiresValue = true },
                        new("--description", "Label description (-d)") { Alias = "-d", RequiresValue = true },
                        new("--name", "New label name (-n)") { Alias = "-n", RequiresValue = true },
                        repoParam,
                    ],
                },
                new("list", "List labels")
                {
                    Parameters =
                    [
                        new("--limit", "Maximum number to fetch (-L)") { Alias = "-L", RequiresValue = true },
                        new("--web", "Open in browser (-w)") { Alias = "-w" },
                        repoParam,
                    ],
                },
            ],
        };
    }

    private static Command CreateSearchCommand(
        CommandParameter limitParam, CommandParameter webParam,
        CommandParameter jsonParam, CommandParameter jqParam, CommandParameter templateParam)
    {
        return new Command("search", "Search for repositories, issues, and pull requests")
        {
            SubCommands =
            [
                new("code", "Search within code")
                {
                    Parameters =
                    [
                        limitParam,
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                    ],
                },
                new("commits", "Search for commits")
                {
                    Parameters =
                    [
                        limitParam,
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                    ],
                },
                new("issues", "Search for issues")
                {
                    Parameters =
                    [
                        new("--assignee", "Filter by assignee") { RequiresValue = true },
                        new("--author", "Filter by author") { RequiresValue = true },
                        new("--label", "Filter by label") { RequiresValue = true },
                        new("--state", "Filter by state") { RequiresValue = true },
                        new("--language", "Filter by language") { RequiresValue = true },
                        limitParam,
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                    ],
                },
                new("prs", "Search for pull requests")
                {
                    Parameters =
                    [
                        new("--assignee", "Filter by assignee") { RequiresValue = true },
                        new("--author", "Filter by author") { RequiresValue = true },
                        new("--base", "Filter by base branch (-B)") { Alias = "-B", RequiresValue = true },
                        new("--head", "Filter by head branch (-H)") { Alias = "-H", RequiresValue = true },
                        new("--label", "Filter by label") { RequiresValue = true },
                        new("--state", "Filter by state") { RequiresValue = true },
                        new("--draft", "Filter by draft state"),
                        new("--language", "Filter by language") { RequiresValue = true },
                        limitParam,
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                    ],
                },
                new("repos", "Search for repositories")
                {
                    Parameters =
                    [
                        new("--language", "Filter by language") { RequiresValue = true },
                        new("--owner", "Filter by owner") { RequiresValue = true },
                        new("--topic", "Filter by topic") { RequiresValue = true },
                        new("--visibility", "Filter by visibility") { RequiresValue = true },
                        new("--license", "Filter by license") { RequiresValue = true },
                        new("--sort", "Sort results") { RequiresValue = true },
                        new("--order", "Order of results") { RequiresValue = true },
                        limitParam,
                        webParam,
                        jsonParam,
                        jqParam,
                        templateParam,
                    ],
                },
            ],
        };
    }

    private static Command CreateSecretCommand(CommandParameter repoParam)
    {
        return new Command("secret", "Manage GitHub secrets")
        {
            SubCommands =
            [
                new("delete", "Delete a secret")
                {
                    Parameters = [repoParam],
                },
                new("list", "List secrets")
                {
                    Parameters = [repoParam],
                },
                new("set", "Create or update a secret")
                {
                    Parameters =
                    [
                        new("--body", "Secret value (-b)") { Alias = "-b", RequiresValue = true },
                        new("--env", "Set secret for environment (-e)") { Alias = "-e", RequiresValue = true },
                        new("--org", "Set secret for organization (-o)") { Alias = "-o", RequiresValue = true },
                        new("--visibility", "Set visibility for org secret (-v)") { Alias = "-v", RequiresValue = true },
                        new("--app", "Set secret for application (-a)") { Alias = "-a", RequiresValue = true },
                        repoParam,
                    ],
                },
            ],
            Parameters =
            [
                repoParam,
            ],
        };
    }

    private static Command CreateVariableCommand(CommandParameter repoParam)
    {
        return new Command("variable", "Manage GitHub Actions variables")
        {
            SubCommands =
            [
                new("delete", "Delete a variable")
                {
                    Parameters = [repoParam],
                },
                new("get", "Get a variable")
                {
                    Parameters = [repoParam],
                },
                new("list", "List variables")
                {
                    Parameters = [repoParam],
                },
                new("set", "Create or update a variable")
                {
                    Parameters =
                    [
                        new("--body", "Variable value (-b)") { Alias = "-b", RequiresValue = true },
                        new("--env", "Set for environment (-e)") { Alias = "-e", RequiresValue = true },
                        new("--org", "Set for organization (-o)") { Alias = "-o", RequiresValue = true },
                        new("--visibility", "Set visibility for org variable (-v)") { Alias = "-v", RequiresValue = true },
                        repoParam,
                    ],
                },
            ],
            Parameters =
            [
                repoParam,
            ],
        };
    }

    private static CommandParameter CreateJsonParam(params string[] fields)
    {
        return new CommandParameter("--json", "Output JSON with specified fields")
        {
            RequiresValue = true,
            StaticArguments = fields.Select(f => new StaticArgument(f, f)).ToArray(),
        };
    }
}

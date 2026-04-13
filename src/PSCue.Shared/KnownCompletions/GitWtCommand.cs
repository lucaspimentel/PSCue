using PSCue.Shared.Completions;

namespace PSCue.Shared.KnownCompletions;

public static class GitWtCommand
{
    public static Command Create()
    {
        var globalParameters = new CommandParameter[]
        {
            new("-C", "Working directory for this command") { RequiresValue = true },
            new("--config", "User config file path") { RequiresValue = true },
            new("--verbose", "Verbose output (-v)") { Alias = "-v" },
        };

        var automationParameters = new CommandParameter[]
        {
            new("--yes", "Skip approval prompts (-y)") { Alias = "-y" },
            new("--no-hooks", "Skip hooks"),
        };

        var formatTextJson = new CommandParameter("--format", "Output format")
        {
            RequiresValue = true,
            StaticArguments =
            [
                new("text", "Human-readable text output"),
                new("json", "JSON output"),
            ],
        };

        var switchCommand = new Command("switch", "Switch to a worktree; create if needed")
        {
            Parameters =
            [
                new("--create", "Create a new branch (-c)") { Alias = "-c" },
                new("--base", "Base branch (-b)") { Alias = "-b", RequiresValue = true, DynamicArguments = GetBranches },
                new("--execute", "Command to run after switch (-x)") { Alias = "-x", RequiresValue = true },
                new("--clobber", "Remove stale paths at target"),
                new("--no-cd", "Skip directory change after switching"),
                new("--branches", "Include branches without worktrees"),
                new("--remotes", "Include remote branches"),
                formatTextJson,
                .. automationParameters,
                .. globalParameters,
            ],
            DynamicArguments = GetBranches,
        };

        var statuslineCommand = new Command("statusline", "Single-line status for shell prompts")
        {
            Parameters =
            [
                new("--format", "Output format")
                {
                    RequiresValue = true,
                    StaticArguments =
                    [
                        new("table", "Table output"),
                        new("json", "JSON output"),
                        new("claude-code", "Claude Code output"),
                    ],
                },
                .. globalParameters,
            ],
        };

        var listCommand = new Command("list", "List worktrees and their status")
        {
            SubCommands =
            [
                statuslineCommand,
            ],
            Parameters =
            [
                new("--format", "Output format")
                {
                    RequiresValue = true,
                    StaticArguments =
                    [
                        new("table", "Table output"),
                        new("json", "JSON output"),
                    ],
                },
                new("--branches", "Include branches without worktrees"),
                new("--remotes", "Include remote branches"),
                new("--full", "Show CI, diff analysis, and LLM summaries"),
                new("--progressive", "Show fast info immediately, update with slow info"),
                .. globalParameters,
            ],
        };

        var removeCommand = new Command("remove", "Remove worktree; delete branch if merged")
        {
            Parameters =
            [
                new("--no-delete-branch", "Keep branch after removal"),
                new("--force-delete", "Delete unmerged branches (-D)") { Alias = "-D" },
                new("--foreground", "Run removal in foreground"),
                new("--force", "Force worktree removal (-f)") { Alias = "-f" },
                formatTextJson,
                .. automationParameters,
                .. globalParameters,
            ],
            DynamicArguments = GetBranches,
        };

        var stageParameter = new CommandParameter("--stage", "What to stage before committing")
        {
            RequiresValue = true,
            StaticArguments =
            [
                new("all", "Stage everything: untracked files + unstaged tracked changes"),
                new("tracked", "Stage tracked changes only"),
                new("none", "Stage nothing, commit only what's already in the index"),
            ],
        };

        var mergeCommand = new Command("merge", "Merge current branch into target")
        {
            Parameters =
            [
                new("--no-squash", "Skip commit squashing"),
                new("--no-commit", "Skip commit and squash"),
                new("--no-rebase", "Skip rebase"),
                new("--no-remove", "Keep worktree after merge"),
                new("--no-ff", "Create a merge commit (no fast-forward)"),
                stageParameter,
                formatTextJson,
                .. automationParameters,
                .. globalParameters,
            ],
            DynamicArguments = GetBranches,
        };

        var commitCommand = new Command("commit", "Stage and commit with LLM-generated message")
        {
            Parameters =
            [
                new("--branch", "Branch to operate on (-b)") { Alias = "-b", RequiresValue = true, DynamicArguments = GetBranches },
                stageParameter,
                new("--show-prompt", "Show prompt without running LLM"),
                .. automationParameters,
                .. globalParameters,
            ],
        };

        var squashCommand = new Command("squash", "Squash commits since branching")
        {
            Parameters =
            [
                stageParameter,
                new("--show-prompt", "Show prompt without running LLM"),
                .. automationParameters,
                .. globalParameters,
            ],
            DynamicArguments = GetBranches,
        };

        var pushCommand = new Command("push", "Fast-forward target to current branch")
        {
            Parameters =
            [
                new("--no-ff", "Create a merge commit (no fast-forward)"),
                .. globalParameters,
            ],
            DynamicArguments = GetBranches,
        };

        var pruneCommand = new Command("prune", "Remove worktrees merged into the default branch")
        {
            Parameters =
            [
                new("--dry-run", "Show what would be removed"),
                new("--min-age", "Skip worktrees younger than this") { RequiresValue = true },
                new("--foreground", "Run removal in foreground"),
                formatTextJson,
                new("--yes", "Skip approval prompts (-y)") { Alias = "-y" },
                .. globalParameters,
            ],
        };

        var forEachCommand = new Command("for-each", "Run command in each worktree")
        {
            Parameters =
            [
                formatTextJson,
                .. globalParameters,
            ],
        };

        var stepCommand = new Command("step", "Run individual operations")
        {
            SubCommands =
            [
                commitCommand,
                squashCommand,
                pushCommand,
                new("rebase", "Rebase onto target") { DynamicArguments = GetBranches },
                new("diff", "Show all changes since branching"),
                new("copy-ignored", "Copy gitignored files to another worktree"),
                new("eval", "Evaluate a template expression"),
                forEachCommand,
                new("promote", "Swap a branch into the main worktree"),
                pruneCommand,
                new("relocate", "Move worktrees to expected paths"),
            ],
            Parameters =
            [
                .. globalParameters,
            ],
        };

        var hookShowCommand = new Command("show", "Show configured hooks");

        var hookCommand = new Command("hook", "Run configured hooks")
        {
            SubCommands =
            [
                hookShowCommand,
                new("pre-switch", "Run pre-switch hooks"),
                new("pre-start", "Run pre-start hooks"),
                new("post-start", "Run post-start hooks"),
                new("post-switch", "Run post-switch hooks"),
                new("pre-commit", "Run pre-commit hooks"),
                new("post-commit", "Run post-commit hooks"),
                new("pre-merge", "Run pre-merge hooks"),
                new("post-merge", "Run post-merge hooks"),
                new("pre-remove", "Run pre-remove hooks"),
                new("post-remove", "Run post-remove hooks"),
                new("approvals", "Manage command approvals"),
            ],
            Parameters =
            [
                .. automationParameters,
                .. globalParameters,
            ],
        };

        var configCommand = new Command("config", "Manage user & project configs")
        {
            SubCommands =
            [
                new("shell", "Shell integration setup")
                {
                    SubCommands =
                    [
                        new("install", "Install shell integration"),
                    ],
                },
                new("create", "Create configuration file")
                {
                    Parameters =
                    [
                        new("--project", "Create project config file"),
                    ],
                },
                new("show", "Show configuration files & locations"),
                new("update", "Update deprecated config settings"),
                new("plugins", "Plugin management")
                {
                    SubCommands =
                    [
                        new("claude", "Claude Code plugin"),
                        new("opencode", "OpenCode plugin"),
                    ],
                },
                new("state", "Manage internal data and cache")
                {
                    SubCommands =
                    [
                        new("logs", "Access background hook logs"),
                    ],
                },
            ],
            Parameters =
            [
                .. globalParameters,
            ],
        };

        return new Command("git-wt")
        {
            SubCommands =
            [
                switchCommand,
                listCommand,
                removeCommand,
                mergeCommand,
                stepCommand,
                hookCommand,
                configCommand,
            ],
            Parameters =
            [
                new("--help", "Print help (-h)") { Alias = "-h" },
                new("--version", "Print version (-V)") { Alias = "-V" },
                .. globalParameters,
            ],
        };
    }

    private static IEnumerable<DynamicArgument> GetBranches()
    {
        foreach (var line in Helpers.ExecuteCommand("git", "branch --format='%(refname:short)'"))
        {
            var branch = line.Trim();

            if (!string.IsNullOrWhiteSpace(branch))
            {
                yield return new DynamicArgument(branch);
            }
        }
    }
}

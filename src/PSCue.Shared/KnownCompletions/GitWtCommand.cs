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
            new("--no-verify", "Skip hooks"),
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
                .. automationParameters,
                .. globalParameters,
            ],
            DynamicArguments = GetBranches,
        };

        var listCommand = new Command("list", "List worktrees and their status")
        {
            Parameters =
            [
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
                .. automationParameters,
                .. globalParameters,
            ],
            DynamicArguments = GetBranches,
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
                new("--stage", "What to stage before committing")
                {
                    RequiresValue = true,
                    StaticArguments =
                    [
                        new("all", "Stage everything: untracked files + unstaged tracked changes"),
                        new("tracked", "Stage tracked changes only"),
                        new("none", "Stage nothing, commit only what's already in the index"),
                    ],
                },
                .. automationParameters,
                .. globalParameters,
            ],
            DynamicArguments = GetBranches,
        };

        var stepCommand = new Command("step", "Run individual operations")
        {
            SubCommands =
            [
                new("commit", "Stage and commit with LLM-generated message"),
                new("squash", "Squash commits since branching"),
                new("push", "Fast-forward target to current branch"),
                new("rebase", "Rebase onto target"),
                new("diff", "Show all changes since branching"),
                new("copy-ignored", "Copy gitignored files to another worktree"),
                new("eval", "Evaluate a template expression"),
                new("for-each", "Run command in each worktree"),
                new("promote", "Swap a branch into the main worktree"),
                new("prune", "Remove worktrees merged into the default branch"),
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
                new("--var", "Override template variable (KEY=VALUE)") { RequiresValue = true },
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

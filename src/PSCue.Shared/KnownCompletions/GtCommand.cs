// Graphite CLI (gt) - Stacked pull requests and branch management
// https://graphite.dev/docs/command-reference
//
// SETUP COMMANDS
//    gt auth                - Authenticate with Graphite for PR management
//    gt init                - Initialize Graphite in repository
//
// CORE WORKFLOW COMMANDS
//    gt create [name]       - Create new stacked branch & commit changes [aliases: c]
//    gt modify              - Modify current branch by amending/creating commit [aliases: m]
//    gt submit              - Push stack to GitHub, create/update PRs [aliases: s]
//    gt sync                - Sync all branches from remote, delete merged branches
//
// STACK NAVIGATION
//    gt bottom              - Switch to branch closest to trunk [aliases: b]
//    gt checkout [branch]   - Switch to a branch [aliases: co]
//    gt down [steps]        - Switch to parent branch [aliases: d]
//    gt top                 - Switch to stack tip [aliases: t]
//    gt trunk               - Show trunk of current branch
//    gt up [steps]          - Switch to child branch [aliases: u]
//
// BRANCH INFO
//    gt children            - Show children of current branch
//    gt info [branch]       - Display branch information
//    gt log [command]       - Visually explore stacks [aliases: l]
//    gt parent              - Show parent of current branch
//
// STACK MANAGEMENT
//    gt abort               - Abort current Graphite command
//    gt absorb              - Amend staged changes to relevant commits [aliases: ab]
//    gt continue            - Continue after rebase conflict [aliases: cont]
//    gt fold                - Fold branch changes into parent
//    gt move                - Rebase current branch onto target
//    gt reorder             - Interactively reorder branches
//    gt restack             - Ensure proper parent-child relationships [aliases: r]
//
// BRANCH MANAGEMENT
//    gt delete [name]       - Delete branch and restack children [aliases: dl]
//    gt freeze [branch]     - Freeze branch and downstack branches
//    gt get [branch]        - Sync branch from remote
//    gt pop                 - Delete current branch, retain files
//    gt rename [name]       - Rename branch [aliases: rn]
//    gt revert [sha]        - Create revert branch (experimental)
//    gt split               - Split current branch [aliases: sp]
//    gt squash              - Squash commits into single commit [aliases: sq]
//    gt track [branch]      - Start tracking with Graphite [aliases: tr]
//    gt undo                - Undo recent Graphite mutations
//    gt unfreeze [branch]   - Mark branch as unfrozen
//    gt unlink [branch]     - Unlink PR from branch
//    gt untrack [branch]    - Stop tracking with Graphite [aliases: utr]
//
// GRAPHITE WEB
//    gt dash                - Open Graphite dashboard
//    gt merge               - Merge PRs via Graphite
//    gt pr [branch]         - Open PR page in browser
//
// CONFIGURATION
//    gt aliases             - Edit command aliases
//    gt completion          - Set up tab completion
//    gt config              - Configure Graphite CLI
//    gt fish                - Set up fish tab completion
//
// LEARNING & HELP
//    gt changelog           - Show Graphite CLI changelog
//    gt demo [demoName]     - Run interactive demos
//    gt docs                - Show Graphite CLI docs
//    gt feedback [message]  - Send feedback to maintainers
//    gt guide [title]       - Read extended guides [aliases: g]

using PSCue.Shared.Completions;

namespace PSCue.Shared.KnownCompletions;

public static class GtCommand
{
    public static Command Create()
    {
        return new Command("gt")
        {
            SubCommands =
            [
                // Setup commands
                new("auth", "Authenticate with Graphite for PR management"),
                new("init", "Initialize Graphite in this repository"),

                // Core workflow commands
                new("create", "Create new stacked branch & commit changes (alias: c)")
                {
                    Alias = "c",
                    Parameters =
                    [
                        new("--all", "Stage all changes before committing (-a)") { Alias = "-a" },
                        new("--message", "Commit message (-m)") { Alias = "-m" },
                        new("--no-interactive", "Disable interactive prompts"),
                    ]
                },
                new("modify", "Modify current branch by amending/creating commit (alias: m)")
                {
                    Alias = "m",
                    Parameters =
                    [
                        new("--all", "Stage all changes (-a)") { Alias = "-a" },
                        new("--amend", "Amend last commit instead of creating new one"),
                        new("--no-edit", "Don't edit commit message"),
                        new("--message", "Commit message (-m)") { Alias = "-m" },
                        new("--no-interactive", "Disable interactive prompts"),
                    ]
                },
                new("submit", "Push stack to GitHub, create/update PRs (alias: s)")
                {
                    Alias = "s",
                    Parameters =
                    [
                        new("--stack", "Submit entire stack"),
                        new("--downstack", "Submit downstack only (-d)") { Alias = "-d" },
                        new("--draft", "Create PRs as draft"),
                        new("--dry-run", "Preview what would be submitted"),
                        new("--no-interactive", "Disable interactive prompts"),
                        new("--update-only", "Only update existing PRs"),
                    ]
                },
                new("sync", "Sync all branches from remote, delete merged branches")
                {
                    Parameters =
                    [
                        new("--force", "Force sync without prompts (-f)") { Alias = "-f" },
                        new("--delete", "Delete merged branches without prompting (-d)") { Alias = "-d" },
                        new("--pull", "Pull trunk changes (-p)") { Alias = "-p" },
                        new("--restack", "Restack all branches (-r)") { Alias = "-r" },
                    ]
                },

                // Stack navigation
                new("bottom", "Switch to branch closest to trunk (alias: b)")
                {
                    Alias = "b"
                },
                new("checkout", "Switch to a branch (interactive if no branch provided) (alias: co)")
                {
                    Alias = "co",
                    Parameters =
                    [
                        new("--branch", "Branch to checkout (-b)") { Alias = "-b" },
                        new("--detach", "Detach from Graphite tracking"),
                    ]
                },
                new("down", "Switch to parent branch (alias: d)")
                {
                    Alias = "d",
                    Parameters =
                    [
                        new("--steps", "Number of steps to go down (-n)") { Alias = "-n" },
                    ]
                },
                new("top", "Switch to stack tip (prompts if ambiguous) (alias: t)")
                {
                    Alias = "t"
                },
                new("trunk", "Show trunk of current branch"),
                new("up", "Switch to child branch (prompts if ambiguous) (alias: u)")
                {
                    Alias = "u",
                    Parameters =
                    [
                        new("--steps", "Number of steps to go up (-n)") { Alias = "-n" },
                    ]
                },

                // Branch info
                new("children", "Show children of current branch"),
                new("info", "Display information about current branch")
                {
                    Parameters =
                    [
                        new("--branch", "Branch to show info for (-b)") { Alias = "-b" },
                    ]
                },
                new("log", "Visually explore your stacks (alias: l)")
                {
                    Alias = "l",
                    SubCommands =
                    [
                        new("short", "Show abbreviated stack view"),
                        new("long", "Show detailed stack view"),
                    ],
                    Parameters =
                    [
                        new("--steps", "Number of levels to show (-n)") { Alias = "-n" },
                        new("--reverse", "Reverse order"),
                    ]
                },
                new("parent", "Show parent of current branch"),

                // Stack management
                new("abort", "Abort current Graphite command halted by rebase conflict"),
                new("absorb", "Amend staged changes to relevant commits in stack (alias: ab)")
                {
                    Alias = "ab",
                    Parameters =
                    [
                        new("--base", "Base commit to start from (-b)") { Alias = "-b" },
                        new("--force", "Force absorb without prompts (-f)") { Alias = "-f" },
                    ]
                },
                new("continue", "Continue after rebase conflict resolution (alias: cont)")
                {
                    Alias = "cont"
                },
                new("fold", "Fold branch changes into parent & restack dependencies")
                {
                    Parameters =
                    [
                        new("--keep", "Keep branch after folding"),
                        new("--no-interactive", "Disable interactive prompts"),
                    ]
                },
                new("move", "Rebase current branch onto target & restack descendants")
                {
                    Parameters =
                    [
                        new("--branch", "Target branch (-b)") { Alias = "-b" },
                        new("--onto", "Branch to rebase onto"),
                    ]
                },
                new("reorder", "Interactively reorder branches between trunk and current"),
                new("restack", "Ensure parent-child relationships, rebase if necessary (alias: r)")
                {
                    Alias = "r",
                    Parameters =
                    [
                        new("--force", "Force restack without prompts (-f)") { Alias = "-f" },
                        new("--no-interactive", "Disable interactive prompts"),
                    ]
                },

                // Branch management
                new("delete", "Delete branch and restack children (prompts if unmerged) (alias: dl)")
                {
                    Alias = "dl",
                    Parameters =
                    [
                        new("--force", "Force delete without prompts (-f)") { Alias = "-f" },
                        new("--branch", "Branch to delete (-b)") { Alias = "-b" },
                    ]
                },
                new("freeze", "Freeze branch and downstack branches")
                {
                    Parameters =
                    [
                        new("--branch", "Branch to freeze (-b)") { Alias = "-b" },
                    ]
                },
                new("get", "Sync branch from remote, resolve conflicts if needed")
                {
                    Parameters =
                    [
                        new("--downstack", "Only sync downstack branches (-d)") { Alias = "-d" },
                        new("--force", "Force sync without prompts (-f)") { Alias = "-f" },
                    ]
                },
                new("pop", "Delete current branch but retain files in working tree"),
                new("rename", "Rename branch and update metadata (alias: rn)")
                {
                    Alias = "rn",
                    Parameters =
                    [
                        new("--branch", "Branch to rename (-b)") { Alias = "-b" },
                        new("--new-name", "New branch name (-n)") { Alias = "-n" },
                    ]
                },
                new("revert", "Create branch that reverts a trunk commit (experimental)")
                {
                    Parameters =
                    [
                        new("--message", "Commit message (-m)") { Alias = "-m" },
                    ]
                },
                new("split", "Split current branch into multiple branches (alias: sp)")
                {
                    Alias = "sp",
                    SubCommands =
                    [
                        new("commit", "Split by commit"),
                        new("hunk", "Split by hunk"),
                        new("file", "Split by file"),
                    ]
                },
                new("squash", "Squash all commits in current branch into one (alias: sq)")
                {
                    Alias = "sq",
                    Parameters =
                    [
                        new("--message", "Commit message for squashed commit (-m)") { Alias = "-m" },
                        new("--no-edit", "Don't edit commit message"),
                    ]
                },
                new("track", "Start tracking branch with Graphite by selecting parent (alias: tr)")
                {
                    Alias = "tr",
                    Parameters =
                    [
                        new("--branch", "Branch to track (-b)") { Alias = "-b" },
                        new("--parent", "Parent branch (-p)") { Alias = "-p" },
                    ]
                },
                new("undo", "Undo most recent Graphite mutations"),
                new("unfreeze", "Mark branch and upstack branches as unfrozen")
                {
                    Parameters =
                    [
                        new("--branch", "Branch to unfreeze (-b)") { Alias = "-b" },
                    ]
                },
                new("unlink", "Unlink PR currently associated with branch")
                {
                    Parameters =
                    [
                        new("--branch", "Branch to unlink (-b)") { Alias = "-b" },
                    ]
                },
                new("untrack", "Stop tracking branch with Graphite (alias: utr)")
                {
                    Alias = "utr",
                    Parameters =
                    [
                        new("--branch", "Branch to untrack (-b)") { Alias = "-b" },
                    ]
                },

                // Graphite web
                new("dash", "Open your Graphite dashboard in browser"),
                new("merge", "Merge PRs from trunk to current branch via Graphite")
                {
                    Parameters =
                    [
                        new("--delete-branches", "Delete branches after merge"),
                        new("--no-interactive", "Disable interactive prompts"),
                    ]
                },
                new("pr", "Open PR page for branch or PR number in browser")
                {
                    Parameters =
                    [
                        new("--branch", "Branch to open PR for (-b)") { Alias = "-b" },
                        new("--number", "PR number to open (-n)") { Alias = "-n" },
                    ]
                },

                // Configuration
                new("aliases", "Edit your command aliases")
                {
                    Parameters =
                    [
                        new("--help", "Show aliases help"),
                    ]
                },
                new("completion", "Set up bash or zsh tab completion")
                {
                    Parameters =
                    [
                        new("--shell", "Shell type (bash, zsh)"),
                    ]
                },
                new("config", "Configure Graphite CLI")
                {
                    SubCommands =
                    [
                        new("get", "Get configuration value"),
                        new("set", "Set configuration value"),
                        new("list", "List all configuration values"),
                        new("reset", "Reset configuration to defaults"),
                    ]
                },
                new("fish", "Set up fish tab completion"),

                // Learning & help
                new("changelog", "Show Graphite CLI changelog"),
                new("demo", "Run interactive demos to learn Graphite workflow")
                {
                    SubCommands =
                    [
                        new("workflow", "Demo core workflow"),
                        new("stacking", "Demo stacking PRs"),
                        new("cleanup", "Clean up demo branches"),
                    ]
                },
                new("docs", "Show Graphite CLI documentation in browser"),
                new("feedback", "Send feedback to maintainers")
                {
                    Parameters =
                    [
                        new("--with-logs", "Attach logs to feedback"),
                    ]
                },
                new("guide", "Read extended guides on using Graphite (alias: g)")
                {
                    Alias = "g",
                    SubCommands =
                    [
                        new("workflow", "Guide for core workflow"),
                        new("stacking", "Guide for stacking PRs"),
                        new("conflicts", "Guide for resolving conflicts"),
                    ]
                },
            ],
            Parameters =
            [
                new("--help", "Show help for command"),
                new("--cwd", "Working directory for operations"),
                new("--debug", "Write debug output to terminal"),
                new("--interactive", "Enable interactive features (prompts, pagers, editors)"),
                new("--no-interactive", "Disable interactive features"),
                new("--verify", "Enable git hooks (default: true)"),
                new("--no-verify", "Disable git hooks"),
                new("--quiet", "Minimize output (implies --no-interactive) (-q)") { Alias = "-q" },
                new("--version", "Show gt version number"),
            ]
        };
    }
}

// Usage:
//   chezmoi [command]
//
// Available Commands:
//   add                  Add an existing file, directory, or symlink to the source state
//   age                  Interact with age
//   apply                Update the destination directory to match the target state
//   archive              Generate a tar archive of the target state
//   cat                  Print the target contents of a file, script, or symlink
//   cat-config           Print the configuration file
//   cd                   Launch a shell in the source directory
//   chattr               Change the attributes of a target in the source state
//   completion           Generate shell completion code
//   data                 Print the template data
//   decrypt              Decrypt file or standard input
//   destroy              Permanently delete an entry from the source state, the destination directory, and the state
//   diff                 Print the diff between the target state and the destination state
//   doctor               Check your system for potential problems
//   dump                 Generate a dump of the target state
//   dump-config          Dump the configuration values
//   edit                 Edit the source state of a target
//   edit-config          Edit the configuration file
//   edit-config-template Edit the configuration file template
//   encrypt              Encrypt file or standard input
//   execute-template     Execute the given template(s)
//   forget               Remove a target from the source state
//   generate             Generate a file for use with chezmoi
//   git                  Run git in the source directory
//   help                 Print help about a command
//   ignored              Print ignored targets
//   import               Import an archive into the source state
//   init                 Setup the source directory and update the destination directory to match the target state
//   license              Print license
//   managed              List the managed entries in the destination directory
//   merge                Perform a three-way merge between the destination state, the source state, and the target state
//   merge-all            Perform a three-way merge for each modified file
//   purge                Purge chezmoi's configuration and data
//   re-add               Re-add modified files
//   secret               Interact with a secret manager
//   source-path          Print the source path of a target
//   state                Manipulate the persistent state
//   status               Show the status of targets
//   target-path          Print the target path of a source path
//   unmanaged            List the unmanaged files in the destination directory
//   update               Pull and apply any changes
//   upgrade              Upgrade chezmoi to the latest released version
//   verify               Exit with success if the destination state matches the target state, fail otherwise
//
// Flags:
//       --cache path                                     Set cache directory (default ~/.cache/chezmoi)
//       --color bool|auto                                Colorize output (default auto)
//   -c, --config path                                    Set config file
//       --config-format <none>|json|toml|yaml            Set config file format
//       --debug                                          Include debug information in output
//   -D, --destination path                               Set destination directory (default ~)
//   -n, --dry-run                                        Do not make any modifications to the destination directory
//       --force                                          Make all changes without prompting
//   -h, --help                                           help for chezmoi
//       --interactive                                    Prompt for all changes
//   -k, --keep-going                                     Keep going as far as possible after an error
//       --mode file|symlink                              Mode (default file)
//       --no-pager                                       Do not use the pager
//       --no-tty                                         Do not attempt to get a TTY for prompts
//   -o, --output path                                    Write output to path instead of stdout
//       --persistent-state path                          Set persistent state file
//       --progress bool|auto                             Display progress bars (default auto)
//   -R, --refresh-externals always|auto|never[=always]   Refresh external cache (default auto)
//   -S, --source path                                    Set source directory (default ~/.local/share/chezmoi)
//       --source-path                                    Specify targets by source path
//       --use-builtin-age bool|auto                      Use builtin age (default auto)
//       --use-builtin-diff                               Use builtin diff
//       --use-builtin-git bool|auto                      Use builtin git (default auto)
//   -v, --verbose                                        Make output more verbose
//       --version                                        version for chezmoi
//   -W, --working-tree path                              Set working tree directory
//
// Use "chezmoi [command] --help" for more information about a command.

using PSCue.ArgumentCompleter.Completions;

namespace PSCue.ArgumentCompleter.KnownCompletions;

public static class ChezmoiCommand
{
    public static Command Create()
    {
        return new Command("chezmoi")
        {
            SubCommands =
            [
                new("add", "Add an existing file, directory, or symlink to the source state."),
                new("age", "Interact with age."),
                new("apply", "Update the destination directory to match the target state."),
                new("archive", "Generate a tar archive of the target state."),
                new("cat", "Print the target contents of a file, script, or symlink."),
                new("cat-config", "Print the configuration file."),
                new("cd", "Launch a shell in the source directory."),
                new("chattr", "Change the attributes of a target in the source state."),
                new("completion", "Generate shell completion code."),
                new("data", "Print the template data."),
                new("decrypt", "Decrypt file or standard input."),
                new("destroy", "Permanently delete an entry from the source state, the destination directory, and the state."),
                new("diff", "Print the diff between the target state and the destination state."),
                new("doctor", "Check your system for potential problems."),
                new("dump", "Generate a dump of the target state."),
                new("dump-config", "Dump the configuration values."),
                new("edit", "Edit the source state of a target."),
                new("edit-config", "Edit the configuration file."),
                new("edit-config-template", "Edit the configuration file template."),
                new("encrypt", "Encrypt file or standard input."),
                new("execute-template", "Execute the given template(s)."),
                new("forget", "Remove a target from the source state."),
                new("generate", "Generate a file for use with chezmoi."),
                new("git", "Run git in the source directory."),
                new("help", "Print help about a command."),
                new("ignored", "Print ignored targets."),
                new("import", "Import an archive into the source state."),
                new("init", "Setup the source directory and update the destination directory to match the target state."),
                new("license", "Print license."),
                new("managed", "List the managed entries in the destination directory."),
                new("merge", "Perform a three-way merge between the destination state, the source state, and the target state."),
                new("merge-all", "Perform a three-way merge for each modified file."),
                new("purge", "Purge chezmoi's configuration and data."),
                new("re-add", "Re-add modified files."),
                new("secret", "Interact with a secret manager."),
                new("source-path", "Print the source path of a target."),
                new("state", "Manipulate the persistent state."),
                new("status", "Show the status of targets."),
                new("target-path", "Print the target path of a source path."),
                new("unmanaged", "List the unmanaged files in the destination directory."),
                new("update", "Pull and apply any changes."),
                new("upgrade", "Upgrade chezmoi to the latest released version."),
                new("verify", "Exit with success if the destination state matches the target state, fail otherwise."),
            ],
            Parameters =
            [
                new("--cache", "Set cache directory (default ~/.cache/chezmoi)."),
                new("--color", "Colorize output (default auto)."),
                new("-c", "Set config file."),
                new("--config", "Set config file."),
                new("--config-format", "Set config file format."),
                new("--debug", "Include debug information in output."),
                new("-D", "Set destination directory (default ~)."),
                new("--destination", "Set destination directory (default ~)."),
                new("-n", "Do not make any modifications to the destination directory."),
                new("--dry-run", "Do not make any modifications to the destination directory."),
                new("--force", "Make all changes without prompting."),
                new("-h", "Help for chezmoi."),
                new("--help", "Help for chezmoi."),
                new("--interactive", "Prompt for all changes."),
                new("-k", "Keep going as far as possible after an error."),
                new("--keep-going", "Keep going as far as possible after an error."),
                new("--mode", "Mode (default file)."),
                new("--no-pager", "Do not use the pager."),
                new("--no-tty", "Do not attempt to get a TTY for prompts."),
                new("-o", "Write output to path instead of stdout."),
                new("--output", "Write output to path instead of stdout."),
                new("--persistent-state", "Set persistent state file."),
                new("--progress", "Display progress bars (default auto)."),
                new("-R", "Refresh external cache (default auto)."),
                new("--refresh-externals", "Refresh external cache (default auto)."),
                new("-S", "Set source directory (default ~/.local/share/chezmoi)."),
                new("--source", "Set source directory (default ~/.local/share/chezmoi)."),
                new("--source-path", "Specify targets by source path."),
                new("--use-builtin-age", "Use builtin age (default auto)."),
                new("--use-builtin-diff", "Use builtin diff."),
                new("--use-builtin-git", "Use builtin git (default auto)."),
                new("-v", "Make output more verbose."),
                new("--verbose", "Make output more verbose."),
                new("--version", "Version for chezmoi."),
                new("-W", "Set working tree directory."),
                new("--working-tree", "Set working tree directory."),
            ]
        };
    }
}

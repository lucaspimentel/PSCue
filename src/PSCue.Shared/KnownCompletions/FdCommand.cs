namespace PSCue.Shared.KnownCompletions;

using Completions;

internal static class FdCommand
{
    public static Command Create() =>
        new("fd", "A simple, fast and user-friendly alternative to find")
        {
            Parameters =
            [
                // Search options
                new("--hidden", "Search hidden files and directories (-H)") { Alias = "-H" },
                new("--no-ignore", "Do not respect .(git|fd)ignore files (-I)") { Alias = "-I" },
                new("--no-ignore-vcs", "Do not respect .gitignore files"),
                new("--no-require-git", "Do not require a git repository to respect gitignores"),
                new("--no-ignore-parent", "Do not respect .(git|fd)ignore files in parent directories"),
                new("--unrestricted", "Unrestricted search, alias for --hidden --no-ignore (-u)") { Alias = "-u" },
                new("--case-sensitive", "Case-sensitive search (-s)") { Alias = "-s" },
                new("--ignore-case", "Case-insensitive search (-i)") { Alias = "-i" },
                new("--glob", "Glob-based search (-g)") { Alias = "-g" },
                new("--regex", "Regular-expression based search (default)"),
                new("--fixed-strings", "Treat pattern as literal string (-F)") { Alias = "-F" },
                new("--and", "Additional search pattern that must also match") { RequiresValue = true },
                new("--absolute-path", "Show absolute paths (-a)") { Alias = "-a" },
                new("--list-details", "Use a long listing format with file metadata (-l)") { Alias = "-l" },
                new("--follow", "Follow symbolic links (-L)") { Alias = "-L" },
                new("--full-path", "Search full path, not just file name (-p)") { Alias = "-p" },
                new("--print0", "Separate results by NUL character (-0)") { Alias = "-0" },
                new("--prune", "Do not traverse into matched directories"),
                new("--quiet", "Print nothing, exit with 0 if match found (-q)") { Alias = "-q" },
                new("--show-errors", "Show filesystem errors"),
                new("--one-file-system", "Do not cross filesystem boundaries"),
                new("-1", "Limit to one result"),

                // Filter options
                new("--type", "Filter by type (-t)")
                {
                    Alias = "-t",
                    StaticArguments =
                    [
                        new("file", "Regular files"),
                        new("directory", "Directories"),
                        new("symlink", "Symbolic links"),
                        new("socket", "Sockets"),
                        new("pipe", "Named pipes (FIFOs)"),
                        new("block-device", "Block devices"),
                        new("char-device", "Character devices"),
                        new("executable", "Executables"),
                        new("empty", "Empty files or directories")
                    ]
                },
                new("--extension", "Filter by file extension (-e)") { Alias = "-e", RequiresValue = true },
                new("--exclude", "Exclude entries matching glob pattern (-E)") { Alias = "-E", RequiresValue = true },
                new("--max-depth", "Maximum search depth (-d)") { Alias = "-d", RequiresValue = true },
                new("--min-depth", "Minimum search depth") { RequiresValue = true },
                new("--exact-depth", "Search at exact depth") { RequiresValue = true },
                new("--size", "Limit results by file size (-S)") { Alias = "-S", RequiresValue = true },
                new("--changed-within", "Filter by modification time (newer than)") { RequiresValue = true },
                new("--changed-before", "Filter by modification time (older than)") { RequiresValue = true },
                new("--ignore-file", "Add custom ignore file") { RequiresValue = true },
                new("--ignore-contain", "Ignore dirs containing this file") { RequiresValue = true },

                // Execution options
                new("--exec", "Execute command for each result (-x)") { Alias = "-x", RequiresValue = true },
                new("--exec-batch", "Execute command with all results (-X)") { Alias = "-X", RequiresValue = true },
                new("--batch-size", "Max number of arguments per --exec-batch call") { RequiresValue = true },

                // Output options
                new("--color", "When to use colors (-c)")
                {
                    Alias = "-c",
                    StaticArguments =
                    [
                        new("auto", "Use color when output is a terminal"),
                        new("always", "Always use color"),
                        new("never", "Never use color")
                    ]
                },
                new("--hyperlink", "When to add hyperlinks")
                {
                    StaticArguments =
                    [
                        new("auto", "Use hyperlinks when output is a terminal"),
                        new("always", "Always use hyperlinks"),
                        new("never", "Never use hyperlinks")
                    ]
                },
                new("--strip-cwd-prefix", "Strip current directory prefix")
                {
                    StaticArguments =
                    [
                        new("auto", "Strip when not using --exec or --exec-batch"),
                        new("always", "Always strip prefix"),
                        new("never", "Never strip prefix")
                    ]
                },
                new("--format", "Format output with template") { RequiresValue = true },
                new("--path-separator", "Set the path separator") { RequiresValue = true },
                new("--max-results", "Limit number of results") { RequiresValue = true },

                // Performance options
                new("--threads", "Number of threads (-j)") { Alias = "-j", RequiresValue = true },
                new("--base-directory", "Change current directory (-C)") { Alias = "-C", RequiresValue = true },
                new("--search-path", "Provide paths to search") { RequiresValue = true },

                // Meta options
                new("--help", "Print help (-h)") { Alias = "-h" },
                new("--version", "Print version (-V)") { Alias = "-V" }
            ]
        };
}

namespace PSCue.Shared.KnownCompletions;

using Completions;

internal static class LsdCommand
{
    public static Command Create() =>
        new("lsd", "An ls command with a lot of pretty colors and some other stuff")
        {
            Parameters =
            [
                new("--all", "Do not ignore entries starting with . (-a)") { Alias = "-a" },
                new("--almost-all", "Do not list implied . and .. (-A)") { Alias = "-A" },
                new("--color", "When to use terminal colours")
                {
                    StaticArguments =
                    [
                        new("always", "Always use colors"),
                        new("auto", "Automatically detect if colors should be used"),
                        new("never", "Never use colors")
                    ]
                },
                new("--icon", "When to print the icons")
                {
                    StaticArguments =
                    [
                        new("always", "Always print icons"),
                        new("auto", "Automatically detect if icons should be printed"),
                        new("never", "Never print icons")
                    ]
                },
                new("--icon-theme", "Whether to use fancy or unicode icons")
                {
                    StaticArguments =
                    [
                        new("fancy", "Use fancy icons"),
                        new("unicode", "Use unicode icons")
                    ]
                },
                new("--classify", "Append indicator (one of */=>@|) at the end of the file names (-F)") { Alias = "-F" },
                new("--long", "Display extended file metadata as a table (-l)") { Alias = "-l" },
                new("--ignore-config", "Ignore the configuration file"),
                new("--config-file", "Provide a custom lsd configuration file"),
                new("--oneline", "Display one entry per line (-1)") { Alias = "-1" },
                new("--recursive", "Recurse into directories (-R)") { Alias = "-R" },
                new("--human-readable", "For ls compatibility purposes ONLY, currently set by default (-h)") { Alias = "-h" },
                new("--tree", "Recurse into directories and present the result as a tree"),
                new("--depth", "Stop recursing into directories after reaching specified depth"),
                new("--directory-only", "Display directories themselves, and not their contents (-d)") { Alias = "-d" },
                new("--permission", "How to display permissions")
                {
                    StaticArguments =
                    [
                        new("rwx", "Display as rwx"),
                        new("octal", "Display as octal"),
                        new("attributes", "Display as attributes (Windows)"),
                        new("disable", "Don't display permissions")
                    ]
                },
                new("--size", "How to display size")
                {
                    StaticArguments =
                    [
                        new("default", "Default size display"),
                        new("short", "Short size display"),
                        new("bytes", "Display in bytes")
                    ]
                },
                new("--total-size", "Display the total size of directories"),
                new("--date", "How to display date")
                {
                    StaticArguments =
                    [
                        new("date", "Display as date"),
                        new("locale", "Display using locale format"),
                        new("relative", "Display as relative time")
                    ]
                },
                new("--timesort", "Sort by time modified (-t)") { Alias = "-t" },
                new("--sizesort", "Sort by size (-S)") { Alias = "-S" },
                new("--extensionsort", "Sort by file extension (-X)") { Alias = "-X" },
                new("--gitsort", "Sort by git status (-G)") { Alias = "-G" },
                new("--versionsort", "Natural sort of (version) numbers within text (-v)") { Alias = "-v" },
                new("--sort", "Sort by TYPE instead of name")
                {
                    StaticArguments =
                    [
                        new("size", "Sort by size"),
                        new("time", "Sort by time"),
                        new("version", "Sort by version"),
                        new("extension", "Sort by extension"),
                        new("git", "Sort by git status"),
                        new("none", "No sorting")
                    ]
                },
                new("--no-sort", "Do not sort. List entries in directory order (-U)") { Alias = "-U" },
                new("--reverse", "Reverse the order of the sort (-r)") { Alias = "-r" },
                new("--group-dirs", "Sort the directories then the files")
                {
                    StaticArguments =
                    [
                        new("none", "Don't group directories"),
                        new("first", "Group directories first"),
                        new("last", "Group directories last")
                    ]
                },
                new("--group-directories-first", "Groups the directories at the top before the files"),
                new("--blocks", "Specify the blocks that will be displayed and in what order"),
                new("--classic", "Enable classic mode (display output similar to ls)"),
                new("--no-symlink", "Do not display symlink target"),
                new("--ignore-glob", "Do not display files/directories matching the glob pattern (-I)") { Alias = "-I" },
                new("--inode", "Display the index number of each file (-i)") { Alias = "-i" },
                new("--git", "Show git status on file and directory (only with --long) (-g)") { Alias = "-g" },
                new("--dereference", "Show information for the file the link references (-L)") { Alias = "-L" },
                new("--context", "Print security context (label) of each file (-Z)") { Alias = "-Z" },
                new("--hyperlink", "Attach hyperlink to filenames")
                {
                    StaticArguments =
                    [
                        new("always", "Always attach hyperlinks"),
                        new("auto", "Automatically attach hyperlinks"),
                        new("never", "Never attach hyperlinks")
                    ]
                },
                new("--header", "Display block headers"),
                new("--truncate-owner-after", "Truncate user and group names after NUM characters"),
                new("--truncate-owner-marker", "Truncation marker for truncated user or group name"),
                new("--system-protected", "Includes files with the windows system protection flag set"),
                new("--literal", "Print entry names without quoting (-N)") { Alias = "-N" },
                new("--help", "Print help information"),
                new("--version", "Print version (-V)") { Alias = "-V" }
            ]
        };
}

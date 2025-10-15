namespace PSCue.ArgumentCompleter.KnownCompletions;

using Completions;

internal static class DustCommand
{
    public static Command Create() =>
        new("dust", "Like du but more intuitive")
        {
            Parameters =
            [
                new("--depth", "Depth to show (-d)") { Alias = "-d" },
                new("--threads", "Number of threads to use (-T)") { Alias = "-T" },
                new("--config", "Specify a config file to use"),
                new("--number-of-lines", "Number of lines of output to show (-n)") { Alias = "-n" },
                new("--full-paths", "Subdirectories will not have their path shortened (-p)") { Alias = "-p" },
                new("--ignore-directory", "Exclude any file or directory with this path (-X)") { Alias = "-X" },
                new("--ignore-all-in-file", "Exclude any file or directory with a regex matching that listed in this file (-I)") { Alias = "-I" },
                new("--dereference-links", "dereference sym links - Treat sym links as directories and go into them (-L)") { Alias = "-L" },
                new("--limit-filesystem", "Only count the files and directories on the same filesystem as the supplied directory (-x)") { Alias = "-x" },
                new("--apparent-size", "Use file length instead of blocks (-s)") { Alias = "-s" },
                new("--reverse", "Print tree upside down (biggest highest) (-r)") { Alias = "-r" },
                new("--no-colors", "No colors will be printed (Useful for commands like: watch) (-c)") { Alias = "-c" },
                new("--force-colors", "Force colors print (-C)") { Alias = "-C" },
                new("--no-percent-bars", "No percent bars or percentages will be displayed (-b)") { Alias = "-b" },
                new("--bars-on-right", "percent bars moved to right side of screen (-B)") { Alias = "-B" },
                new("--min-size", "Minimum size file to include in output (-z)") { Alias = "-z" },
                new("--screen-reader", "For screen readers. Removes bars. Adds new column: depth level (-R)") { Alias = "-R" },
                new("--skip-total", "No total row will be displayed"),
                new("--filecount", "Directory 'size' is number of child files instead of disk size (-f)") { Alias = "-f" },
                new("--ignore-hidden", "Do not display hidden files (-i)") { Alias = "-i" },
                new("--invert-filter", "Exclude filepaths matching this regex (-v)") { Alias = "-v" },
                new("--filter", "Only include filepaths matching this regex (-e)") { Alias = "-e" },
                new("--file-types", "show only these file types (-t)") { Alias = "-t" },
                new("--terminal-width", "Specify width of output overriding the auto detection of terminal width (-w)") { Alias = "-w" },
                new("--no-progress", "Disable the progress indication (-P)") { Alias = "-P" },
                new("--print-errors", "Print path with errors"),
                new("--only-dir", "Only directories will be displayed (-D)") { Alias = "-D" },
                new("--only-file", "Only files will be displayed. (Finds your largest files) (-F)") { Alias = "-F" },
                new("--output-format", "Changes output display size (-o)")
                {
                    Alias = "-o",
                    StaticArguments =
                    [
                        new("si", "SI prefix (powers of 1000)"),
                        new("b", "byte (B)"),
                        new("k", "kibibyte (KiB)"),
                        new("m", "mebibyte (MiB)"),
                        new("g", "gibibyte (GiB)"),
                        new("t", "tebibyte (TiB)"),
                        new("kb", "kilobyte (kB)"),
                        new("mb", "megabyte (MB)"),
                        new("gb", "gigabyte (GB)"),
                        new("tb", "terabyte (TB)")
                    ]
                },
                new("--stack-size", "Specify memory to use as stack size (-S)") { Alias = "-S" },
                new("--output-json", "Output the directory tree as json to the current directory (-j)") { Alias = "-j" },
                new("--mtime", "Matches files modified more/less than n days ago (-M)") { Alias = "-M" },
                new("--atime", "just like -mtime, but based on file access time (-A)") { Alias = "-A" },
                new("--ctime", "just like -mtime, but based on file change time (-y)") { Alias = "-y" },
                new("--files0-from", "run dust on NUL-terminated file names specified in file"),
                new("--collapse", "Keep these directories collapsed"),
                new("--filetime", "Directory 'size' is max filetime of child files instead of disk size (-m)")
                {
                    Alias = "-m",
                    StaticArguments =
                    [
                        new("a", "last accessed time"),
                        new("c", "last changed time"),
                        new("m", "last modified time")
                    ]
                },
                new("--help", "Print help (-h)") { Alias = "-h" },
                new("--version", "Print version (-V)") { Alias = "-V" }
            ]
        };
}

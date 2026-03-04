namespace PSCue.Shared.KnownCompletions;

using Completions;

internal static class RgCommand
{
    public static Command Create() =>
        new("rg", "ripgrep — recursively search directories for a regex pattern")
        {
            Parameters =
            [
                // Input options
                new("--regexp", "Pattern to search for (-e)") { Alias = "-e", RequiresValue = true },
                new("--file", "Read patterns from file (-f)") { Alias = "-f", RequiresValue = true },
                new("--pre", "Preprocessor command for each file") { RequiresValue = true },
                new("--pre-glob", "Glob pattern for --pre filtering") { RequiresValue = true },
                new("--search-zip", "Search compressed files (-z)") { Alias = "-z" },

                // Search options
                new("--case-sensitive", "Case-sensitive search (-s)") { Alias = "-s" },
                new("--ignore-case", "Case-insensitive search (-i)") { Alias = "-i" },
                new("--smart-case", "Smart case search (-S)") { Alias = "-S" },
                new("--fixed-strings", "Treat pattern as literal string (-F)") { Alias = "-F" },
                new("--line-regexp", "Match entire lines only (-x)") { Alias = "-x" },
                new("--word-regexp", "Match whole words only (-w)") { Alias = "-w" },
                new("--invert-match", "Show non-matching lines (-v)") { Alias = "-v" },
                new("--max-count", "Limit matches per file (-m)") { Alias = "-m", RequiresValue = true },
                new("--multiline", "Enable multiline matching (-U)") { Alias = "-U" },
                new("--multiline-dotall", "Make . match newlines in multiline mode"),
                new("--pcre2", "Use PCRE2 regex engine (-P)") { Alias = "-P" },
                new("--crlf", "Treat CRLF as line terminator"),
                new("--no-unicode", "Disable Unicode mode"),
                new("--null-data", "Use NUL byte as line terminator"),
                new("--text", "Search binary files as text (-a)") { Alias = "-a" },
                new("--threads", "Number of threads (-j)") { Alias = "-j", RequiresValue = true },
                new("--stop-on-nonmatch", "Stop after first non-match in a file"),
                new("--mmap", "Use memory-mapped I/O"),
                new("--encoding", "File encoding (-E)") { Alias = "-E", RequiresValue = true },
                new("--engine", "Regex engine to use")
                {
                    StaticArguments =
                    [
                        new("default", "Use default regex engine"),
                        new("pcre2", "Use PCRE2 regex engine"),
                        new("auto", "Automatically select engine")
                    ]
                },
                new("--dfa-size-limit", "Upper limit for DFA state machine size") { RequiresValue = true },
                new("--regex-size-limit", "Upper limit for compiled regex size") { RequiresValue = true },

                // Filter options
                new("--glob", "Include/exclude files matching glob (-g)") { Alias = "-g", RequiresValue = true },
                new("--iglob", "Like --glob but case-insensitive") { RequiresValue = true },
                new("--glob-case-insensitive", "Treat all globs as case-insensitive"),
                new("--hidden", "Search hidden files and directories (-.)") { Alias = "-." },
                new("--follow", "Follow symbolic links (-L)") { Alias = "-L" },
                new("--max-depth", "Max directory traversal depth (-d)") { Alias = "-d", RequiresValue = true },
                new("--max-filesize", "Ignore files larger than this size") { RequiresValue = true },
                new("--type", "Only search files of this type (-t)") { Alias = "-t", RequiresValue = true },
                new("--type-not", "Exclude files of this type (-T)") { Alias = "-T", RequiresValue = true },
                new("--type-add", "Add a custom file type") { RequiresValue = true },
                new("--type-clear", "Clear a file type definition") { RequiresValue = true },
                new("--unrestricted", "Reduce filtering (-u: no .gitignore, -uu: +hidden, -uuu: +binary)") { Alias = "-u" },
                new("--no-ignore", "Don't respect ignore files"),
                new("--no-ignore-dot", "Don't respect .ignore files"),
                new("--no-ignore-exclude", "Don't respect local exclude rules"),
                new("--no-ignore-files", "Don't respect --ignore-file flags"),
                new("--no-ignore-global", "Don't respect global ignore files"),
                new("--no-ignore-parent", "Don't respect ignore files in parent directories"),
                new("--no-ignore-vcs", "Don't respect VCS ignore files"),
                new("--no-require-git", "Don't require a git repository for .gitignore rules"),
                new("--one-file-system", "Don't cross filesystem boundaries"),
                new("--ignore-file", "Path to additional ignore file") { RequiresValue = true },
                new("--ignore-file-case-insensitive", "Case-insensitive ignore file processing"),
                new("--binary", "Search binary files (no replacement output)"),

                // Output options
                new("--after-context", "Show N lines after each match (-A)") { Alias = "-A", RequiresValue = true },
                new("--before-context", "Show N lines before each match (-B)") { Alias = "-B", RequiresValue = true },
                new("--context", "Show N lines before and after each match (-C)") { Alias = "-C", RequiresValue = true },
                new("--color", "When to use color in output")
                {
                    StaticArguments =
                    [
                        new("never", "Never use color"),
                        new("auto", "Use color when output is a terminal"),
                        new("always", "Always use color"),
                        new("ansi", "Always use ANSI color codes")
                    ]
                },
                new("--colors", "Configure color settings") { RequiresValue = true },
                new("--count", "Show only match count per file (-c)") { Alias = "-c" },
                new("--count-matches", "Show count of individual matches per file"),
                new("--files-with-matches", "Show only file paths with matches (-l)") { Alias = "-l" },
                new("--files-without-match", "Show only file paths without matches"),
                new("--only-matching", "Show only the matching part of each line (-o)") { Alias = "-o" },
                new("--line-number", "Show line numbers (-n)") { Alias = "-n" },
                new("--no-line-number", "Suppress line numbers (-N)") { Alias = "-N" },
                new("--byte-offset", "Show byte offset of each match (-b)") { Alias = "-b" },
                new("--with-filename", "Show file path with matches (-H)") { Alias = "-H" },
                new("--no-filename", "Suppress file paths (-I)") { Alias = "-I" },
                new("--max-columns", "Truncate lines longer than this (-M)") { Alias = "-M", RequiresValue = true },
                new("--max-columns-preview", "Show preview for truncated lines"),
                new("--heading", "Group matches by file with headings"),
                new("--null", "Print NUL byte after file paths (-0)") { Alias = "-0" },
                new("--pretty", "Alias for --color always --heading --line-number (-p)") { Alias = "-p" },
                new("--quiet", "Suppress all output, useful for exit code only (-q)") { Alias = "-q" },
                new("--replace", "Replace matches with given string (-r)") { Alias = "-r", RequiresValue = true },
                new("--sort", "Sort results in ascending order")
                {
                    StaticArguments =
                    [
                        new("path", "Sort by file path"),
                        new("modified", "Sort by last modified time"),
                        new("accessed", "Sort by last accessed time"),
                        new("created", "Sort by creation time"),
                        new("none", "No sorting")
                    ]
                },
                new("--sortr", "Sort results in descending order")
                {
                    StaticArguments =
                    [
                        new("path", "Sort by file path"),
                        new("modified", "Sort by last modified time"),
                        new("accessed", "Sort by last accessed time"),
                        new("created", "Sort by creation time"),
                        new("none", "No sorting")
                    ]
                },
                new("--trim", "Trim leading whitespace from matches"),
                new("--vimgrep", "Output in vim-compatible format"),
                new("--passthru", "Print both matching and non-matching lines"),
                new("--json", "Output results in JSON Lines format"),
                new("--block-buffered", "Use block buffering for output"),
                new("--line-buffered", "Use line buffering for output"),
                new("--context-separator", "String to separate context groups") { RequiresValue = true },
                new("--field-context-separator", "String to delimit fields in context lines") { RequiresValue = true },
                new("--field-match-separator", "String to delimit fields in match lines") { RequiresValue = true },
                new("--path-separator", "Path separator to use in output") { RequiresValue = true },
                new("--hyperlink-format", "Format for hyperlinks in output") { RequiresValue = true },
                new("--hostname-bin", "Binary for getting hostname for hyperlinks") { RequiresValue = true },
                new("--include-zero", "Include zero-match files in --count output"),

                // Meta options
                new("--help", "Show help (-h)") { Alias = "-h" },
                new("--version", "Show version (-V)") { Alias = "-V" },
                new("--files", "List files that would be searched"),
                new("--type-list", "Show all supported file types"),
                new("--debug", "Show debug messages"),
                new("--trace", "Show trace messages"),
                new("--stats", "Print statistics after search"),
                new("--no-messages", "Suppress error messages"),
                new("--no-ignore-messages", "Suppress ignore file parse error messages"),
                new("--no-config", "Don't read configuration files"),
                new("--pcre2-version", "Show PCRE2 version information"),
                new("--generate", "Generate various outputs")
                {
                    StaticArguments =
                    [
                        new("man", "Generate man page"),
                        new("complete-bash", "Generate bash completions"),
                        new("complete-zsh", "Generate zsh completions"),
                        new("complete-fish", "Generate fish completions"),
                        new("complete-powershell", "Generate PowerShell completions")
                    ]
                }
            ]
        };
}

// Usage: tre [OPTIONS] [PATH]
//
// Tree command, improved.
//
// OPTIONS:
//     -a, --all                   Print all files and directories, including hidden ones
//     -c, --color <WHEN>          When to color the output [default: automatic] [possible values: automatic, always, never]
//     -d, --directories           Only list directories in output
//     -e, --editor [<COMMAND>]    Create aliases for each displayed result
//     -E, --exclude <PATTERN>     Exclude paths matching a regex pattern. Repeatable
//     -h, --help                  Print help information
//     -j, --json                  Output JSON instead of tree diagram
//     -l, --limit <LIMIT>         Limit depth of the tree in output
//     -p, --portable              Generate portable (absolute) paths for editor aliases
//     -V, --version               Print version information

using PSCue.ArgumentCompleter.Completions;

namespace PSCue.ArgumentCompleter.KnownCompletions;

public static class TreCommand
{
    public static Command Create()
    {
        return new Command("tre")
        {
            Parameters =
            [
                new("--all", "Print all files and directories, including hidden ones (-a)") { Alias = "-a" },
                new("--color", "When to color the output (-c)")
                {
                    Alias = "-c",
                    StaticArguments =
                    [
                        new("automatic", "Color when printing to a terminal"),
                        new("always", "Always use colors"),
                        new("never", "Never use colors"),
                    ]
                },
                new("--directories", "Only list directories in output (-d)") { Alias = "-d" },
                new("--editor", "Create aliases for each displayed result (-e)") { Alias = "-e" },
                new("--exclude", "Exclude paths matching a regex pattern (-E)") { Alias = "-E" },
                new("--help", "Print help information (-h)") { Alias = "-h" },
                new("--json", "Output JSON instead of tree diagram (-j)") { Alias = "-j" },
                new("--limit", "Limit depth of the tree in output (-l)") { Alias = "-l" },
                new("--portable", "Generate portable (absolute) paths for editor aliases (-p)") { Alias = "-p" },
                new("--version", "Print version information (-V)") { Alias = "-V" },
            ]
        };
    }
}

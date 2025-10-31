// Windows Terminal (wt)
//
// Usage: wt [OPTIONS] [SUBCOMMAND]
//
// Options:
//   -h,--help                      Print this help message and exit
//   -v,--version                   Display the application version
//   -M,--maximized Excludes: --fullscreen
//                                  Launch the window maximized
//   -F,--fullscreen Excludes: --maximized --focus
//                                  Launch the window in fullscreen mode
//   -f,--focus Excludes: --fullscreen
//                                  Launch the window in focus mode
//   --pos TEXT                     Specify the position for the terminal, in "x,y" format
//   --size TEXT                    Specify the number of columns and rows for the terminal, in "cr" or "c,r" format
//   -w,--window TEXT               Specify a terminal window to run the given commandline in. "0" always refers to the current window
//   -s,--saved INT                 This parameter is an internal implementation detail and should not be used
//
// Subcommands:
//   new-tab                        Create a new tab
//   nt                             An alias for the "new-tab" subcommand
//   split-pane                     Create a new split pane
//   sp                             An alias for the "split-pane" subcommand
//   focus-tab                      Move focus to another tab
//   ft                             An alias for the "focus-tab" subcommand
//   move-focus                     Move focus to the adjacent pane in the specified direction
//   mf                             An alias for the "move-focus" subcommand
//   move-pane                      Move focused pane to another tab
//   mp                             An alias for the "move-pane" subcommand
//   swap-pane                      Swap the focused pane with an adjacent pane in the specified direction
//   focus-pane                     Move focus to another pane
//   fp                             An alias for the "focus-pane" subcommand
//   --save                         Save the command line as input action

using PSCue.Shared.Completions;

namespace PSCue.Shared.KnownCompletions;

public static class WindowsTerminalCommand
{
    public static Command Create()
    {
        var newTabCommand = new Command("new-tab", "Create a new tab (alias: nt)")
        {
            Alias = "nt",
            Parameters =
            [
                new("--startingDirectory", "Set the starting directory for the new tab (-d)") { Alias = "-d" },
                new("--profile", "Use a specific profile for the new tab (-p)") { Alias = "-p" },
                new("--title", "Set the title of the new tab"),
                new("--window", "Specify which window to create this tab in (-w)") { Alias = "-w" },
                new("--tabColor", "Set the color of the tab"),
                new("--colorScheme", "Set the color scheme of the tab"),
                new("--suppressApplicationTitle", "Suppress application title"),
                new("--useApplicationTitle", "Use application title")
            ]
        };

        var splitPaneCommand = new Command("split-pane", "Create a new split pane (alias: sp)")
        {
            Alias = "sp",
            Parameters =
            [
                new("--horizontal", "Split horizontally, split to the bottom (-H)") { Alias = "-H" },
                new("--vertical", "Split vertically, split to the right (-V)") { Alias = "-V" },
                new("--duplicate", "Duplicate the pane's profile into a new pane (-D)") { Alias = "-D" },
                new("--startingDirectory", "Set the starting directory for the new pane (-d)") { Alias = "-d" },
                new("--profile", "Use a specific profile for the new pane (-p)") { Alias = "-p" },
                new("--title", "Set the title of the new pane"),
                new("--tabColor", "Set the color of the tab"),
                new("--colorScheme", "Set the color scheme of the pane"),
                new("--size", "Specify the size of the new pane (0.0-1.0 or number of cells)"),
                new("--suppressApplicationTitle", "Suppress application title"),
                new("--useApplicationTitle", "Use application title")
            ]
        };

        var focusTabCommand = new Command("focus-tab", "Move focus to another tab (alias: ft)")
        {
            Alias = "ft",
            Parameters =
            [
                new("--target", "Focus the tab at the specified index (-t)") { Alias = "-t" },
                new("--next", "Focus the next tab (-n)") { Alias = "-n" },
                new("--previous", "Focus the previous tab (-p)") { Alias = "-p" }
            ]
        };

        var moveFocusCommand = new Command("move-focus", "Move focus to the adjacent pane in the specified direction (alias: mf)")
        {
            Alias = "mf",
            Parameters =
            [
                new("up", "Move focus up"),
                new("down", "Move focus down"),
                new("left", "Move focus left"),
                new("right", "Move focus right"),
                new("previous", "Move focus to the previous pane"),
                new("previousInOrder", "Move focus to the previous pane in order"),
                new("nextInOrder", "Move focus to the next pane in order"),
                new("first", "Move focus to the first pane"),
                new("parent", "Move focus to the parent pane"),
                new("child", "Move focus to the child pane")
            ]
        };

        var movePaneCommand = new Command("move-pane", "Move focused pane to another tab (alias: mp)")
        {
            Alias = "mp",
            Parameters =
            [
                new("--tab", "Move to the tab at the specified index (-t)") { Alias = "-t" },
                new("--window", "Move to the specified window (-w)") { Alias = "-w" }
            ]
        };

        var swapPaneCommand = new Command("swap-pane", "Swap the focused pane with an adjacent pane in the specified direction")
        {
            Parameters =
            [
                new("up", "Swap with pane above"),
                new("down", "Swap with pane below"),
                new("left", "Swap with pane to the left"),
                new("right", "Swap with pane to the right"),
                new("previous", "Swap with previous pane"),
                new("previousInOrder", "Swap with previous pane in order"),
                new("nextInOrder", "Swap with next pane in order"),
                new("first", "Swap with first pane")
            ]
        };

        var focusPaneCommand = new Command("focus-pane", "Move focus to another pane (alias: fp)")
        {
            Alias = "fp",
            Parameters =
            [
                new("--target", "Focus the pane at the specified index (-t)") { Alias = "-t" }
            ]
        };

        return new Command("wt")
        {
            Parameters =
            [
                new("--help", "Print this help message and exit (-h)") { Alias = "-h" },
                new("--version", "Display the application version (-v)") { Alias = "-v" },
                new("--maximized", "Launch the window maximized (-M)") { Alias = "-M" },
                new("--fullscreen", "Launch the window in fullscreen mode (-F)") { Alias = "-F" },
                new("--focus", "Launch the window in focus mode (-f)") { Alias = "-f" },
                new("--pos", "Specify the position for the terminal, in \"x,y\" format"),
                new("--size", "Specify the number of columns and rows for the terminal, in \"cr\" or \"c,r\" format"),
                new("--window", "Specify a terminal window to run the given commandline in. \"0\" always refers to the current window (-w)") { Alias = "-w" },
                new("--saved", "This parameter is an internal implementation detail and should not be used (-s)") { Alias = "-s" }
            ],
            SubCommands =
            [
                newTabCommand,
                splitPaneCommand,
                focusTabCommand,
                moveFocusCommand,
                movePaneCommand,
                swapPaneCommand,
                focusPaneCommand,
                new("--save", "Save the command line as input action")
            ]
        };
    }
}

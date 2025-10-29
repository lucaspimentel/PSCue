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
        var newTabCommand = new Command("new-tab", "Create a new tab")
        {
            Parameters =
            [
                new("-d", "Set the starting directory for the new tab"),
                new("--startingDirectory", "Set the starting directory for the new tab"),
                new("-p", "Use a specific profile for the new tab"),
                new("--profile", "Use a specific profile for the new tab"),
                new("--title", "Set the title of the new tab"),
                new("-w", "Specify which window to create this tab in"),
                new("--window", "Specify which window to create this tab in"),
                new("--tabColor", "Set the color of the tab"),
                new("--colorScheme", "Set the color scheme of the tab"),
                new("--suppressApplicationTitle", "Suppress application title"),
                new("--useApplicationTitle", "Use application title")
            ]
        };

        var splitPaneCommand = new Command("split-pane", "Create a new split pane")
        {
            Parameters =
            [
                new("-H", "Split horizontally (split to the bottom)"),
                new("--horizontal", "Split horizontally (split to the bottom)"),
                new("-V", "Split vertically (split to the right)"),
                new("--vertical", "Split vertically (split to the right)"),
                new("-D", "Duplicate the pane's profile into a new pane"),
                new("--duplicate", "Duplicate the pane's profile into a new pane"),
                new("-d", "Set the starting directory for the new pane"),
                new("--startingDirectory", "Set the starting directory for the new pane"),
                new("-p", "Use a specific profile for the new pane"),
                new("--profile", "Use a specific profile for the new pane"),
                new("--title", "Set the title of the new pane"),
                new("--tabColor", "Set the color of the tab"),
                new("--colorScheme", "Set the color scheme of the pane"),
                new("--size", "Specify the size of the new pane (0.0-1.0 or number of cells)"),
                new("--suppressApplicationTitle", "Suppress application title"),
                new("--useApplicationTitle", "Use application title")
            ]
        };

        var focusTabCommand = new Command("focus-tab", "Move focus to another tab")
        {
            Parameters =
            [
                new("-t", "Focus the tab at the specified index"),
                new("--target", "Focus the tab at the specified index"),
                new("-n", "Focus the next tab"),
                new("--next", "Focus the next tab"),
                new("-p", "Focus the previous tab"),
                new("--previous", "Focus the previous tab")
            ]
        };

        var moveFocusCommand = new Command("move-focus", "Move focus to the adjacent pane in the specified direction")
        {
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

        var movePaneCommand = new Command("move-pane", "Move focused pane to another tab")
        {
            Parameters =
            [
                new("-t", "Move to the tab at the specified index"),
                new("--tab", "Move to the tab at the specified index"),
                new("-w", "Move to the specified window"),
                new("--window", "Move to the specified window")
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

        var focusPaneCommand = new Command("focus-pane", "Move focus to another pane")
        {
            Parameters =
            [
                new("-t", "Focus the pane at the specified index"),
                new("--target", "Focus the pane at the specified index")
            ]
        };

        return new Command("wt")
        {
            Parameters =
            [
                new("-h", "Print this help message and exit"),
                new("--help", "Print this help message and exit"),
                new("-v", "Display the application version"),
                new("--version", "Display the application version"),
                new("-M", "Launch the window maximized"),
                new("--maximized", "Launch the window maximized"),
                new("-F", "Launch the window in fullscreen mode"),
                new("--fullscreen", "Launch the window in fullscreen mode"),
                new("-f", "Launch the window in focus mode"),
                new("--focus", "Launch the window in focus mode"),
                new("--pos", "Specify the position for the terminal, in \"x,y\" format"),
                new("--size", "Specify the number of columns and rows for the terminal, in \"cr\" or \"c,r\" format"),
                new("-w", "Specify a terminal window to run the given commandline in. \"0\" always refers to the current window"),
                new("--window", "Specify a terminal window to run the given commandline in. \"0\" always refers to the current window"),
                new("-s", "This parameter is an internal implementation detail and should not be used"),
                new("--saved", "This parameter is an internal implementation detail and should not be used")
            ],
            SubCommands =
            [
                newTabCommand,
                new("nt", "An alias for the \"new-tab\" subcommand.") { Parameters = newTabCommand.Parameters },
                splitPaneCommand,
                new("sp", "An alias for the \"split-pane\" subcommand.") { Parameters = splitPaneCommand.Parameters },
                focusTabCommand,
                new("ft", "An alias for the \"focus-tab\" subcommand.") { Parameters = focusTabCommand.Parameters },
                moveFocusCommand,
                new("mf", "An alias for the \"move-focus\" subcommand.") { Parameters = moveFocusCommand.Parameters },
                movePaneCommand,
                new("mp", "An alias for the \"move-pane\" subcommand.") { Parameters = movePaneCommand.Parameters },
                swapPaneCommand,
                focusPaneCommand,
                new("fp", "An alias for the \"focus-pane\" subcommand.") { Parameters = focusPaneCommand.Parameters },
                new("--save", "Save the command line as input action")
            ]
        };
    }
}

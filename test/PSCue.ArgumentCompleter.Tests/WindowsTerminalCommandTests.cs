using System.Linq;
using System.Runtime.InteropServices;
using PSCue.Shared;

namespace PSCue.ArgumentCompleter.Tests;

public class WindowsTerminalCommandTests
{
    [SkippableFact]
    public void Wt_Command_Returns_Completions()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "-h");
        Assert.Contains(completions, x => x.CompletionText == "--help");
        Assert.Contains(completions, x => x.CompletionText == "new-tab");
        Assert.Contains(completions, x => x.CompletionText == "split-pane");
    }

    [SkippableFact]
    public void Wt_Help_Options()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt -").ToList();

        Assert.Contains(completions, x => x.CompletionText == "-h");
        Assert.Contains(completions, x => x.CompletionText == "--help");
        Assert.Contains(completions, x => x.CompletionText == "-v");
        Assert.Contains(completions, x => x.CompletionText == "--version");
    }

    [SkippableFact]
    public void Wt_Window_Options()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt --").ToList();

        Assert.Contains(completions, x => x.CompletionText == "--maximized");
        Assert.Contains(completions, x => x.CompletionText == "--fullscreen");
        Assert.Contains(completions, x => x.CompletionText == "--focus");
        Assert.Contains(completions, x => x.CompletionText == "--window");
    }

    [SkippableFact]
    public void Wt_NewTab_Subcommand()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt new-tab").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "-d");
        Assert.Contains(completions, x => x.CompletionText == "--startingDirectory");
        Assert.Contains(completions, x => x.CompletionText == "-p");
        Assert.Contains(completions, x => x.CompletionText == "--profile");
    }

    [SkippableFact]
    public void Wt_Nt_Alias()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt nt").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "-d");
        Assert.Contains(completions, x => x.CompletionText == "--startingDirectory");
    }

    [SkippableFact]
    public void Wt_SplitPane_Subcommand()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt split-pane").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "-H");
        Assert.Contains(completions, x => x.CompletionText == "--horizontal");
        Assert.Contains(completions, x => x.CompletionText == "-V");
        Assert.Contains(completions, x => x.CompletionText == "--vertical");
    }

    [SkippableFact]
    public void Wt_Sp_Alias()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt sp").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "-H");
        Assert.Contains(completions, x => x.CompletionText == "--horizontal");
    }

    [SkippableFact]
    public void Wt_FocusTab_Subcommand()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt focus-tab").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "-t");
        Assert.Contains(completions, x => x.CompletionText == "--target");
        Assert.Contains(completions, x => x.CompletionText == "-n");
        Assert.Contains(completions, x => x.CompletionText == "--next");
    }

    [SkippableFact]
    public void Wt_Ft_Alias()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt ft").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "-t");
        Assert.Contains(completions, x => x.CompletionText == "--target");
    }

    [SkippableFact]
    public void Wt_MoveFocus_Subcommand()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt move-focus").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "up");
        Assert.Contains(completions, x => x.CompletionText == "down");
        Assert.Contains(completions, x => x.CompletionText == "left");
        Assert.Contains(completions, x => x.CompletionText == "right");
    }

    [SkippableFact]
    public void Wt_Mf_Alias()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt mf").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "up");
        Assert.Contains(completions, x => x.CompletionText == "down");
    }

    [SkippableFact]
    public void Wt_MovePane_Subcommand()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt move-pane").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "-t");
        Assert.Contains(completions, x => x.CompletionText == "--tab");
        Assert.Contains(completions, x => x.CompletionText == "-w");
        Assert.Contains(completions, x => x.CompletionText == "--window");
    }

    [SkippableFact]
    public void Wt_Mp_Alias()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt mp").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "-t");
        Assert.Contains(completions, x => x.CompletionText == "--tab");
    }

    [SkippableFact]
    public void Wt_SwapPane_Subcommand()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt swap-pane").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "up");
        Assert.Contains(completions, x => x.CompletionText == "down");
        Assert.Contains(completions, x => x.CompletionText == "left");
        Assert.Contains(completions, x => x.CompletionText == "right");
    }

    [SkippableFact]
    public void Wt_FocusPane_Subcommand()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt focus-pane").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "-t");
        Assert.Contains(completions, x => x.CompletionText == "--target");
    }

    [SkippableFact]
    public void Wt_Fp_Alias()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt fp").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "-t");
        Assert.Contains(completions, x => x.CompletionText == "--target");
    }

    [SkippableFact]
    public void Wt_Case_Insensitive_Subcommands()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var lower = CommandCompleter.GetCompletions("wt new-tab").ToList();
        var upper = CommandCompleter.GetCompletions("wt NEW-TAB").ToList();

        Assert.Equal(lower.Count, upper.Count);
    }

    [SkippableFact]
    public void Wt_Partial_Match_NewTab()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt new").ToList();

        var newTab = Assert.Single(completions);
        Assert.Equal("new-tab", newTab.CompletionText);
    }

    [SkippableFact]
    public void Wt_Save_Subcommand()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "wt is Windows-only");

        var completions = CommandCompleter.GetCompletions("wt").ToList();

        Assert.Contains(completions, x => x.CompletionText == "--save");
    }
}

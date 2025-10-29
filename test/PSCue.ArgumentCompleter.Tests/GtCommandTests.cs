using System.Linq;
using PSCue.Shared;

namespace PSCue.ArgumentCompleter.Tests;

public class GtCommandTests
{
    [Fact]
    public void Gt_Command_Returns_Completions()
    {
        var completions = CommandCompleter.GetCompletions("gt").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "--help");
        Assert.Contains(completions, x => x.CompletionText == "init");
        Assert.Contains(completions, x => x.CompletionText == "create");
        Assert.Contains(completions, x => x.CompletionText == "submit");
        Assert.Contains(completions, x => x.CompletionText == "sync");
    }

    [Fact]
    public void Gt_Setup_Commands()
    {
        var completions = CommandCompleter.GetCompletions("gt").ToList();

        Assert.Contains(completions, x => x.CompletionText == "auth");
        Assert.Contains(completions, x => x.CompletionText == "init");
    }

    [Fact]
    public void Gt_Core_Workflow_Commands()
    {
        var completions = CommandCompleter.GetCompletions("gt").ToList();

        Assert.Contains(completions, x => x.CompletionText == "create");
        Assert.Contains(completions, x => x.CompletionText == "modify");
        Assert.Contains(completions, x => x.CompletionText == "submit");
        Assert.Contains(completions, x => x.CompletionText == "sync");
    }

    [Fact]
    public void Gt_Stack_Navigation_Commands()
    {
        var completions = CommandCompleter.GetCompletions("gt").ToList();

        Assert.Contains(completions, x => x.CompletionText == "bottom");
        Assert.Contains(completions, x => x.CompletionText == "checkout");
        Assert.Contains(completions, x => x.CompletionText == "down");
        Assert.Contains(completions, x => x.CompletionText == "top");
        Assert.Contains(completions, x => x.CompletionText == "up");
    }

    [Fact]
    public void Gt_Branch_Info_Commands()
    {
        var completions = CommandCompleter.GetCompletions("gt").ToList();

        Assert.Contains(completions, x => x.CompletionText == "children");
        Assert.Contains(completions, x => x.CompletionText == "info");
        Assert.Contains(completions, x => x.CompletionText == "log");
        Assert.Contains(completions, x => x.CompletionText == "parent");
    }

    [Fact]
    public void Gt_Stack_Management_Commands()
    {
        var completions = CommandCompleter.GetCompletions("gt").ToList();

        Assert.Contains(completions, x => x.CompletionText == "abort");
        Assert.Contains(completions, x => x.CompletionText == "absorb");
        Assert.Contains(completions, x => x.CompletionText == "continue");
        Assert.Contains(completions, x => x.CompletionText == "fold");
        Assert.Contains(completions, x => x.CompletionText == "move");
        Assert.Contains(completions, x => x.CompletionText == "reorder");
        Assert.Contains(completions, x => x.CompletionText == "restack");
    }

    [Fact]
    public void Gt_Branch_Management_Commands()
    {
        var completions = CommandCompleter.GetCompletions("gt").ToList();

        Assert.Contains(completions, x => x.CompletionText == "delete");
        Assert.Contains(completions, x => x.CompletionText == "freeze");
        Assert.Contains(completions, x => x.CompletionText == "get");
        Assert.Contains(completions, x => x.CompletionText == "pop");
        Assert.Contains(completions, x => x.CompletionText == "rename");
        Assert.Contains(completions, x => x.CompletionText == "split");
        Assert.Contains(completions, x => x.CompletionText == "squash");
        Assert.Contains(completions, x => x.CompletionText == "track");
        Assert.Contains(completions, x => x.CompletionText == "undo");
        Assert.Contains(completions, x => x.CompletionText == "unfreeze");
        Assert.Contains(completions, x => x.CompletionText == "untrack");
    }

    [Fact]
    public void Gt_Create_Subcommand_Options()
    {
        var completions = CommandCompleter.GetCompletions("gt create").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "--all");
        Assert.Contains(completions, x => x.CompletionText == "--message");
        Assert.Contains(completions, x => x.CompletionText == "--no-interactive");

        // Verify aliases work for matching
        var allParam = completions.FirstOrDefault(x => x.CompletionText == "--all");
        Assert.NotNull(allParam);
        Assert.Contains("(-a)", allParam.Tooltip);
    }

    [Fact]
    public void Gt_Modify_Subcommand_Options()
    {
        var completions = CommandCompleter.GetCompletions("gt modify").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "--all");
        Assert.Contains(completions, x => x.CompletionText == "--amend");
        Assert.Contains(completions, x => x.CompletionText == "--no-edit");
        Assert.Contains(completions, x => x.CompletionText == "--message");
        Assert.Contains(completions, x => x.CompletionText == "--no-interactive");
    }

    [Fact]
    public void Gt_Submit_Subcommand_Options()
    {
        var completions = CommandCompleter.GetCompletions("gt submit").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "--stack");
        Assert.Contains(completions, x => x.CompletionText == "--downstack");
        Assert.Contains(completions, x => x.CompletionText == "--draft");
        Assert.Contains(completions, x => x.CompletionText == "--dry-run");
        Assert.Contains(completions, x => x.CompletionText == "--no-interactive");
        Assert.Contains(completions, x => x.CompletionText == "--update-only");
    }

    [Fact]
    public void Gt_Sync_Subcommand_Options()
    {
        var completions = CommandCompleter.GetCompletions("gt sync").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "--force");
        Assert.Contains(completions, x => x.CompletionText == "--delete");
        Assert.Contains(completions, x => x.CompletionText == "--pull");
        Assert.Contains(completions, x => x.CompletionText == "--restack");
    }

    [Fact]
    public void Gt_Log_Subcommand_Options()
    {
        var completions = CommandCompleter.GetCompletions("gt log").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "short");
        Assert.Contains(completions, x => x.CompletionText == "long");
    }

    [Fact]
    public void Gt_Split_Subcommand_Options()
    {
        var completions = CommandCompleter.GetCompletions("gt split").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "commit");
        Assert.Contains(completions, x => x.CompletionText == "hunk");
        Assert.Contains(completions, x => x.CompletionText == "file");
    }

    [Fact]
    public void Gt_Demo_Subcommand_Options()
    {
        var completions = CommandCompleter.GetCompletions("gt demo").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "workflow");
        Assert.Contains(completions, x => x.CompletionText == "stacking");
        Assert.Contains(completions, x => x.CompletionText == "cleanup");
    }

    [Fact]
    public void Gt_Guide_Subcommand_Options()
    {
        var completions = CommandCompleter.GetCompletions("gt guide").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "workflow");
        Assert.Contains(completions, x => x.CompletionText == "stacking");
        Assert.Contains(completions, x => x.CompletionText == "conflicts");
    }

    [Fact]
    public void Gt_Config_Subcommand_Options()
    {
        var completions = CommandCompleter.GetCompletions("gt config").ToList();

        Assert.NotEmpty(completions);
        Assert.Contains(completions, x => x.CompletionText == "get");
        Assert.Contains(completions, x => x.CompletionText == "set");
        Assert.Contains(completions, x => x.CompletionText == "list");
        Assert.Contains(completions, x => x.CompletionText == "reset");
    }

    [Fact]
    public void Gt_Create_Partial_Match()
    {
        var completions = CommandCompleter.GetCompletions("gt cre").ToList();

        // Typing "gt cre" should match commands starting with "cre"
        Assert.Contains(completions, x => x.CompletionText == "create");
    }

    [Fact]
    public void Gt_Modify_Partial_Match()
    {
        var completions = CommandCompleter.GetCompletions("gt mod").ToList();

        // Typing "gt mod" should match commands starting with "mod"
        var modify = Assert.Single(completions);
        Assert.Equal("modify", modify.CompletionText);
    }

    [Fact]
    public void Gt_Submit_Partial_Match()
    {
        var completions = CommandCompleter.GetCompletions("gt sub").ToList();

        // Typing "gt sub" should match commands starting with "sub"
        Assert.Contains(completions, x => x.CompletionText == "submit");
    }

    [Fact]
    public void Gt_Checkout_Partial_Match()
    {
        var completions = CommandCompleter.GetCompletions("gt check").ToList();

        // Typing "gt check" should match commands starting with "check"
        Assert.Contains(completions, x => x.CompletionText == "checkout");
    }

    [Fact]
    public void Gt_Aliases_Have_Correct_Tooltips()
    {
        var completions = CommandCompleter.GetCompletions("gt").ToList();

        var create = completions.FirstOrDefault(x => x.CompletionText == "create");
        Assert.NotNull(create);
        Assert.Contains("alias: c", create.Tooltip);

        var modify = completions.FirstOrDefault(x => x.CompletionText == "modify");
        Assert.NotNull(modify);
        Assert.Contains("alias: m", modify.Tooltip);

        var submit = completions.FirstOrDefault(x => x.CompletionText == "submit");
        Assert.NotNull(submit);
        Assert.Contains("alias: s", submit.Tooltip);

        var bottom = completions.FirstOrDefault(x => x.CompletionText == "bottom");
        Assert.NotNull(bottom);
        Assert.Contains("alias: b", bottom.Tooltip);

        var checkout = completions.FirstOrDefault(x => x.CompletionText == "checkout");
        Assert.NotNull(checkout);
        Assert.Contains("alias: co", checkout.Tooltip);

        var down = completions.FirstOrDefault(x => x.CompletionText == "down");
        Assert.NotNull(down);
        Assert.Contains("alias: d", down.Tooltip);

        var top = completions.FirstOrDefault(x => x.CompletionText == "top");
        Assert.NotNull(top);
        Assert.Contains("alias: t", top.Tooltip);

        var up = completions.FirstOrDefault(x => x.CompletionText == "up");
        Assert.NotNull(up);
        Assert.Contains("alias: u", up.Tooltip);
    }

    [Fact]
    public void Gt_Case_Insensitive_Subcommands()
    {
        var lower = CommandCompleter.GetCompletions("gt create").ToList();
        var upper = CommandCompleter.GetCompletions("gt CREATE").ToList();

        Assert.Equal(lower.Count, upper.Count);
    }

    [Fact]
    public void Gt_Global_Options()
    {
        var completions = CommandCompleter.GetCompletions("gt").ToList();

        Assert.Contains(completions, x => x.CompletionText == "--help");
        Assert.Contains(completions, x => x.CompletionText == "--version");
        Assert.Contains(completions, x => x.CompletionText == "--debug");
        Assert.Contains(completions, x => x.CompletionText == "--quiet");
        Assert.Contains(completions, x => x.CompletionText == "--cwd");
        Assert.Contains(completions, x => x.CompletionText == "--interactive");
        Assert.Contains(completions, x => x.CompletionText == "--no-interactive");
    }

    [Fact]
    public void Gt_Web_Commands()
    {
        var completions = CommandCompleter.GetCompletions("gt").ToList();

        Assert.Contains(completions, x => x.CompletionText == "dash");
        Assert.Contains(completions, x => x.CompletionText == "merge");
        Assert.Contains(completions, x => x.CompletionText == "pr");
    }

    [Fact]
    public void Gt_Learning_Commands()
    {
        var completions = CommandCompleter.GetCompletions("gt").ToList();

        Assert.Contains(completions, x => x.CompletionText == "changelog");
        Assert.Contains(completions, x => x.CompletionText == "demo");
        Assert.Contains(completions, x => x.CompletionText == "docs");
        Assert.Contains(completions, x => x.CompletionText == "feedback");
        Assert.Contains(completions, x => x.CompletionText == "guide");
    }
}

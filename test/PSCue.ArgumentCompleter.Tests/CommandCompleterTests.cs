using System.Linq;
using System.Runtime.InteropServices;
using PSCue.Shared;

namespace PSCue.ArgumentCompleter.Tests;

public class CommandCompleterTests
{
    [SkippableFact]
    public void Scoop()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "scoop is Windows-only");

        var completions = CommandCompleter.GetCompletions("scoop").ToList();
        Assert.Contains(completions, x => x.CompletionText == "install");
        Assert.Contains(completions, x => x.CompletionText == "update");
        Assert.Contains(completions, x => x.CompletionText == "status");
        Assert.Contains(completions, x => x.CompletionText == "checkup");
    }

    [SkippableFact]
    public void Scoop_Install()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "scoop is Windows-only");

        var completions = CommandCompleter.GetCompletions("scoop in").ToList();
        Assert.Contains(completions, x => x.CompletionText == "install");
        Assert.Contains(completions, x => x.CompletionText == "info");
    }

    [SkippableFact]
    public void Scoop_Update()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "scoop is Windows-only");

        var completions = CommandCompleter.GetCompletions("scoop up").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("update", item.CompletionText);
    }

    [Fact(Skip = "Requires scoop to be installed with specific packages")]
    public void Scoop_Update_All()
    {
        var completions = CommandCompleter.GetCompletions("scoop update").ToList();
        Assert.True(completions.Count >= 2);
        var item = Assert.Single(completions, x => x.CompletionText == "*");
    }

    [Fact(Skip = "Requires scoop to be installed with zoxide package")]
    public void Scoop_Update_z()
    {
        var completions = CommandCompleter.GetCompletions("scoop update z").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("zoxide", item.CompletionText);
    }

    [Fact(Skip = "Requires scoop to be installed with bat, bottom, and broot packages")]
    public void Scoop_Update_b()
    {
        var completions = CommandCompleter.GetCompletions("scoop update b").ToList();
        Assert.Equal(3, completions.Count);
        Assert.Single(completions, x => x.CompletionText == "bat");
        Assert.Single(completions, x => x.CompletionText == "bottom");
        Assert.Single(completions, x => x.CompletionText == "broot");
    }

    [SkippableFact]
    public void Scoop_Status()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "scoop is Windows-only");

        var completions = CommandCompleter.GetCompletions("scoop st").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("status", item.CompletionText);
    }

    [SkippableFact]
    public void Scoop_Checkup()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "scoop is Windows-only");

        var completions = CommandCompleter.GetCompletions("scoop ch").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("checkup", item.CompletionText);
    }

    [Fact]
    public void Git()
    {
        var completions = CommandCompleter.GetCompletions("git").ToList();
        Assert.Contains(completions, x => x.CompletionText == "add");
        Assert.Contains(completions, x => x.CompletionText == "commit");
        Assert.Contains(completions, x => x.CompletionText == "push");
        Assert.Contains(completions, x => x.CompletionText == "pull");
        Assert.Contains(completions, x => x.CompletionText == "status");
        Assert.Contains(completions, x => x.CompletionText == "branch");
        Assert.Contains(completions, x => x.CompletionText == "checkout");
    }

    [Fact]
    public void Git_Add()
    {
        var completions = CommandCompleter.GetCompletions("git ad").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("add", item.CompletionText);
    }

    [Fact]
    public void Git_Add_Parameters()
    {
        var completions = CommandCompleter.GetCompletions("git add").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--all");
        Assert.Contains(completions, x => x.CompletionText == "--patch");
        Assert.Contains(completions, x => x.CompletionText == ".");
    }

    [Fact]
    public void Git_Commit()
    {
        var completions = CommandCompleter.GetCompletions("git com").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("commit", item.CompletionText);
    }

    [Fact]
    public void Git_Commit_Parameters()
    {
        var completions = CommandCompleter.GetCompletions("git commit").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--all");
        Assert.Contains(completions, x => x.CompletionText == "--amend");
        Assert.Contains(completions, x => x.CompletionText == "--message");
    }

    [Fact]
    public void Git_Commit_MultipleParameters()
    {
        var completions = CommandCompleter.GetCompletions("git commit -a -").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--message");
        Assert.Contains(completions, x => x.CompletionText == "--amend");
    }

    [Fact]
    public void Git_Stash()
    {
        var completions = CommandCompleter.GetCompletions("git st").ToList();
        Assert.Equal(2, completions.Count);
        Assert.Contains(completions, x => x.CompletionText == "stash");
        Assert.Contains(completions, x => x.CompletionText == "status");
    }

    [Fact]
    public void Git_Stash_Subcommands()
    {
        var completions = CommandCompleter.GetCompletions("git stash").ToList();
        Assert.Contains(completions, x => x.CompletionText == "push");
        Assert.Contains(completions, x => x.CompletionText == "pop");
        Assert.Contains(completions, x => x.CompletionText == "apply");
        Assert.Contains(completions, x => x.CompletionText == "list");
        Assert.Contains(completions, x => x.CompletionText == "clear");
    }

    [Fact]
    public void Gh()
    {
        var completions = CommandCompleter.GetCompletions("gh").ToList();
        Assert.Contains(completions, x => x.CompletionText == "auth");
        Assert.Contains(completions, x => x.CompletionText == "repo");
        Assert.Contains(completions, x => x.CompletionText == "pr");
        Assert.Contains(completions, x => x.CompletionText == "issue");
        Assert.Contains(completions, x => x.CompletionText == "release");
    }

    [Fact]
    public void Gh_Repo()
    {
        var completions = CommandCompleter.GetCompletions("gh repo").ToList();
        Assert.Contains(completions, x => x.CompletionText == "clone");
        Assert.Contains(completions, x => x.CompletionText == "create");
        Assert.Contains(completions, x => x.CompletionText == "list");
    }

    [Fact]
    public void Gh_Repo_Clone()
    {
        var completions = CommandCompleter.GetCompletions("gh repo cl").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("clone", item.CompletionText);
    }

    [Fact]
    public void Gh_Auth()
    {
        var completions = CommandCompleter.GetCompletions("gh auth").ToList();
        Assert.Contains(completions, x => x.CompletionText == "login");
        Assert.Contains(completions, x => x.CompletionText == "logout");
        Assert.Contains(completions, x => x.CompletionText == "status");
    }

    [Fact]
    public void Gh_Auth_Login()
    {
        var completions = CommandCompleter.GetCompletions("gh auth login").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--web");
        Assert.Contains(completions, x => x.CompletionText == "--hostname");
    }

    [Fact]
    public void Gh_Pr()
    {
        var completions = CommandCompleter.GetCompletions("gh pr").ToList();
        Assert.Contains(completions, x => x.CompletionText == "create");
        Assert.Contains(completions, x => x.CompletionText == "list");
        Assert.Contains(completions, x => x.CompletionText == "view");
        Assert.Contains(completions, x => x.CompletionText == "checkout");
        Assert.Contains(completions, x => x.CompletionText == "checks");
        Assert.Contains(completions, x => x.CompletionText == "diff");
    }

    [Fact]
    public void Gh_Pr_View()
    {
        var completions = CommandCompleter.GetCompletions("gh pr v").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("view", item.CompletionText);
    }

    [Fact]
    public void Gh_Pr_View_Parameters()
    {
        var completions = CommandCompleter.GetCompletions("gh pr view").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--web");
        Assert.Contains(completions, x => x.CompletionText == "--comments");
    }

    [Fact]
    public void Gh_Pr_Checks()
    {
        var completions = CommandCompleter.GetCompletions("gh pr checks").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--web");
    }

    [Fact]
    public void Gh_Pr_Diff()
    {
        var completions = CommandCompleter.GetCompletions("gh pr di").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("diff", item.CompletionText);
    }

    [Fact]
    public void Tre()
    {
        var completions = CommandCompleter.GetCompletions("tre").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--all");
        Assert.Contains(completions, x => x.CompletionText == "--directories");
        Assert.Contains(completions, x => x.CompletionText == "--json");
    }

    [Fact]
    public void Tre_All()
    {
        var completions = CommandCompleter.GetCompletions("tre --a").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("--all", item.CompletionText);
    }

    [Fact]
    public void Tre_Color()
    {
        var completions = CommandCompleter.GetCompletions("tre --color").ToList();
        Assert.Contains(completions, x => x.CompletionText == "automatic");
        Assert.Contains(completions, x => x.CompletionText == "always");
        Assert.Contains(completions, x => x.CompletionText == "never");
    }

    [Fact]
    public void Tre_MultipleParameters()
    {
        var completions = CommandCompleter.GetCompletions("tre --all --").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--directories");
        Assert.Contains(completions, x => x.CompletionText == "--json");
        Assert.Contains(completions, x => x.CompletionText == "--limit");
    }

    [Fact]
    public void Lsd()
    {
        var completions = CommandCompleter.GetCompletions("lsd").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--all");
        Assert.Contains(completions, x => x.CompletionText == "--long");
        Assert.Contains(completions, x => x.CompletionText == "--tree");
        Assert.Contains(completions, x => x.CompletionText == "--help");
    }

    [Fact]
    public void Lsd_Color()
    {
        var completions = CommandCompleter.GetCompletions("lsd --color").ToList();
        Assert.Contains(completions, x => x.CompletionText == "always");
        Assert.Contains(completions, x => x.CompletionText == "auto");
        Assert.Contains(completions, x => x.CompletionText == "never");
    }

    [Fact]
    public void Lsd_Icon()
    {
        var completions = CommandCompleter.GetCompletions("lsd --icon").ToList();
        Assert.Contains(completions, x => x.CompletionText == "always");
        Assert.Contains(completions, x => x.CompletionText == "auto");
        Assert.Contains(completions, x => x.CompletionText == "never");
    }

    [Fact]
    public void Lsd_Sort()
    {
        var completions = CommandCompleter.GetCompletions("lsd --sort").ToList();
        Assert.Contains(completions, x => x.CompletionText == "size");
        Assert.Contains(completions, x => x.CompletionText == "time");
        Assert.Contains(completions, x => x.CompletionText == "version");
        Assert.Contains(completions, x => x.CompletionText == "extension");
        Assert.Contains(completions, x => x.CompletionText == "git");
        Assert.Contains(completions, x => x.CompletionText == "none");
    }

    [Fact]
    public void Lsd_Permission()
    {
        var completions = CommandCompleter.GetCompletions("lsd --permission").ToList();
        Assert.Contains(completions, x => x.CompletionText == "rwx");
        Assert.Contains(completions, x => x.CompletionText == "octal");
        Assert.Contains(completions, x => x.CompletionText == "attributes");
        Assert.Contains(completions, x => x.CompletionText == "disable");
    }

    [Fact]
    public void Dust()
    {
        var completions = CommandCompleter.GetCompletions("dust").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--depth");
        Assert.Contains(completions, x => x.CompletionText == "--number-of-lines");
        Assert.Contains(completions, x => x.CompletionText == "--help");
    }

    [Fact]
    public void Dust_OutputFormat()
    {
        var completions = CommandCompleter.GetCompletions("dust --output-format").ToList();
        Assert.Contains(completions, x => x.CompletionText == "si");
        Assert.Contains(completions, x => x.CompletionText == "b");
        Assert.Contains(completions, x => x.CompletionText == "k");
        Assert.Contains(completions, x => x.CompletionText == "m");
        Assert.Contains(completions, x => x.CompletionText == "g");
        Assert.Contains(completions, x => x.CompletionText == "kb");
        Assert.Contains(completions, x => x.CompletionText == "mb");
        Assert.Contains(completions, x => x.CompletionText == "gb");
    }

    [Fact]
    public void Dust_Filetime()
    {
        var completions = CommandCompleter.GetCompletions("dust --filetime").ToList();
        Assert.Contains(completions, x => x.CompletionText == "a");
        Assert.Contains(completions, x => x.CompletionText == "c");
        Assert.Contains(completions, x => x.CompletionText == "m");
    }

    [SkippableFact]
    public void Winget()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget").ToList();
        Assert.Contains(completions, x => x.CompletionText == "install");
        Assert.Contains(completions, x => x.CompletionText == "search");
        Assert.Contains(completions, x => x.CompletionText == "upgrade");
        Assert.Contains(completions, x => x.CompletionText == "uninstall");
        Assert.Contains(completions, x => x.CompletionText == "list");
        Assert.Contains(completions, x => x.CompletionText == "show");
        Assert.Contains(completions, x => x.CompletionText == "source");
        Assert.Contains(completions, x => x.CompletionText == "pin");
        Assert.Contains(completions, x => x.CompletionText == "export");
        Assert.Contains(completions, x => x.CompletionText == "import");
        Assert.Contains(completions, x => x.CompletionText == "download");
    }

    [SkippableFact]
    public void Winget_Install()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget ins").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("install", item.CompletionText);
    }

    [SkippableFact]
    public void Winget_Install_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget install").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--silent");
        Assert.Contains(completions, x => x.CompletionText == "--interactive");
        Assert.Contains(completions, x => x.CompletionText == "--scope");
        Assert.Contains(completions, x => x.CompletionText == "--architecture");
        Assert.Contains(completions, x => x.CompletionText == "--version");
        Assert.Contains(completions, x => x.CompletionText == "--source");
        Assert.Contains(completions, x => x.CompletionText == "--exact");
    }

    [SkippableFact]
    public void Winget_Install_Scope()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget install --scope").ToList();
        Assert.Contains(completions, x => x.CompletionText == "user");
        Assert.Contains(completions, x => x.CompletionText == "machine");
    }

    [SkippableFact]
    public void Winget_Install_Architecture()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget install --architecture").ToList();
        Assert.Contains(completions, x => x.CompletionText == "x86");
        Assert.Contains(completions, x => x.CompletionText == "x64");
        Assert.Contains(completions, x => x.CompletionText == "arm");
        Assert.Contains(completions, x => x.CompletionText == "arm64");
    }

    [SkippableFact]
    public void Winget_Install_InstallerType()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget install --installer-type").ToList();
        Assert.Contains(completions, x => x.CompletionText == "msix");
        Assert.Contains(completions, x => x.CompletionText == "msi");
        Assert.Contains(completions, x => x.CompletionText == "exe");
        Assert.Contains(completions, x => x.CompletionText == "portable");
    }

    [SkippableFact]
    public void Winget_Upgrade()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget upg").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("upgrade", item.CompletionText);
    }

    [SkippableFact]
    public void Winget_Upgrade_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget upgrade").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--all");
        Assert.Contains(completions, x => x.CompletionText == "--silent");
        Assert.Contains(completions, x => x.CompletionText == "--interactive");
        Assert.Contains(completions, x => x.CompletionText == "--include-unknown");
        Assert.Contains(completions, x => x.CompletionText == "--include-pinned");
    }

    [SkippableFact]
    public void Winget_Uninstall()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget uni").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("uninstall", item.CompletionText);
    }

    [SkippableFact]
    public void Winget_Uninstall_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget uninstall").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--silent");
        Assert.Contains(completions, x => x.CompletionText == "--force");
        Assert.Contains(completions, x => x.CompletionText == "--purge");
        Assert.Contains(completions, x => x.CompletionText == "--preserve");
    }

    [SkippableFact]
    public void Winget_Search()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget sea").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("search", item.CompletionText);
    }

    [SkippableFact]
    public void Winget_Search_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget search").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--name");
        Assert.Contains(completions, x => x.CompletionText == "--id");
        Assert.Contains(completions, x => x.CompletionText == "--tag");
        Assert.Contains(completions, x => x.CompletionText == "--exact");
        Assert.Contains(completions, x => x.CompletionText == "--count");
    }

    [SkippableFact]
    public void Winget_List()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget li").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("list", item.CompletionText);
    }

    [SkippableFact]
    public void Winget_List_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget list").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--upgrade-available");
        Assert.Contains(completions, x => x.CompletionText == "--include-unknown");
        Assert.Contains(completions, x => x.CompletionText == "--source");
    }

    [SkippableFact]
    public void Winget_Show()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget sh").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("show", item.CompletionText);
    }

    [SkippableFact]
    public void Winget_Show_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget show").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--name");
        Assert.Contains(completions, x => x.CompletionText == "--id");
        Assert.Contains(completions, x => x.CompletionText == "--versions");
        Assert.Contains(completions, x => x.CompletionText == "--exact");
    }

    [SkippableFact]
    public void Winget_Source()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget sou").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("source", item.CompletionText);
    }

    [SkippableFact]
    public void Winget_Source_Subcommands()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget source").ToList();
        Assert.Contains(completions, x => x.CompletionText == "add");
        Assert.Contains(completions, x => x.CompletionText == "list");
        Assert.Contains(completions, x => x.CompletionText == "update");
        Assert.Contains(completions, x => x.CompletionText == "remove");
        Assert.Contains(completions, x => x.CompletionText == "reset");
        Assert.Contains(completions, x => x.CompletionText == "export");
    }

    [SkippableFact]
    public void Winget_Pin()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget pi").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("pin", item.CompletionText);
    }

    [SkippableFact]
    public void Winget_Pin_Subcommands()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget pin").ToList();
        Assert.Contains(completions, x => x.CompletionText == "add");
        Assert.Contains(completions, x => x.CompletionText == "remove");
        Assert.Contains(completions, x => x.CompletionText == "list");
        Assert.Contains(completions, x => x.CompletionText == "reset");
    }

    [SkippableFact]
    public void Winget_Export()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget exp").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("export", item.CompletionText);
    }

    [SkippableFact]
    public void Winget_Export_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget export").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--output");
        Assert.Contains(completions, x => x.CompletionText == "--source");
        Assert.Contains(completions, x => x.CompletionText == "--include-versions");
    }

    [SkippableFact]
    public void Winget_Import()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget imp").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("import", item.CompletionText);
    }

    [SkippableFact]
    public void Winget_Import_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget import").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--import-file");
        Assert.Contains(completions, x => x.CompletionText == "--ignore-unavailable");
        Assert.Contains(completions, x => x.CompletionText == "--ignore-versions");
    }

    [SkippableFact]
    public void Winget_Download()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget dow").ToList();
        var item = Assert.Single(completions);
        Assert.Equal("download", item.CompletionText);
    }

    [SkippableFact]
    public void Winget_Download_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget download").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--download-directory");
        Assert.Contains(completions, x => x.CompletionText == "--id");
        Assert.Contains(completions, x => x.CompletionText == "--version");
    }

    [SkippableFact]
    public void Winget_MultipleParameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        var completions = CommandCompleter.GetCompletions("winget install --silent --").ToList();
        Assert.Contains(completions, x => x.CompletionText == "--scope");
        Assert.Contains(completions, x => x.CompletionText == "--architecture");
        Assert.Contains(completions, x => x.CompletionText == "--exact");
    }
}

using System.Runtime.InteropServices;
using FluentAssertions;

namespace PSCue.ArgumentCompleter.Tests;

public class CommandCompleterTests
{
    [SkippableFact]
    public void Scoop()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "scoop is Windows-only");

        CommandCompleter.GetCompletions("scoop")
                        .Should().Contain(x => x.CompletionText == "install")
                        .And.Contain(x => x.CompletionText == "update")
                        .And.Contain(x => x.CompletionText == "status")
                        .And.Contain(x => x.CompletionText == "checkup");
    }

    [SkippableFact]
    public void Scoop_Install()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "scoop is Windows-only");

        CommandCompleter.GetCompletions("scoop in")
                        .Should().Contain(x => x.CompletionText == "install")
                        .And.Contain(x => x.CompletionText == "info");
    }

    [SkippableFact]
    public void Scoop_Update()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "scoop is Windows-only");

        CommandCompleter.GetCompletions("scoop up")
                        .Should().ContainSingle()
                        .Which.CompletionText.Should().Be("update");
    }

    [Fact(Skip = "Requires scoop to be installed with specific packages")]
    public void Scoop_Update_All()
    {
        CommandCompleter.GetCompletions("scoop update")
                        .Should().HaveCountGreaterThanOrEqualTo(2)
                        .And.ContainSingle(x => x.CompletionText == "*");
    }

    [Fact(Skip = "Requires scoop to be installed with zoxide package")]
    public void Scoop_Update_z()
    {
        CommandCompleter.GetCompletions("scoop update z")
                        .Should().ContainSingle()
                        .Which.CompletionText.Should().Be("zoxide");
    }

    [Fact(Skip = "Requires scoop to be installed with bat, bottom, and broot packages")]
    public void Scoop_Update_b()
    {
        CommandCompleter.GetCompletions("scoop update b")
                        .Should().HaveCount(3)
                        .And.ContainSingle(x => x.CompletionText == "bat")
                        .And.ContainSingle(x => x.CompletionText == "bottom")
                        .And.ContainSingle(x => x.CompletionText == "broot");
    }

    [SkippableFact]
    public void Scoop_Status()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "scoop is Windows-only");

        CommandCompleter.GetCompletions("scoop st")
                        .Should().ContainSingle()
                        .Which.CompletionText.Should().Be("status");
    }

    [SkippableFact]
    public void Scoop_Checkup()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "scoop is Windows-only");

        CommandCompleter.GetCompletions("scoop ch")
                        .Should().ContainSingle()
                        .Which.CompletionText.Should().Be("checkup");
    }

    [Fact]
    public void Git()
    {
        CommandCompleter.GetCompletions("git")
                        .Should().Contain(x => x.CompletionText == "add")
                        .And.Contain(x => x.CompletionText == "commit")
                        .And.Contain(x => x.CompletionText == "push")
                        .And.Contain(x => x.CompletionText == "pull")
                        .And.Contain(x => x.CompletionText == "status")
                        .And.Contain(x => x.CompletionText == "branch")
                        .And.Contain(x => x.CompletionText == "checkout");
    }

    [Fact]
    public void Git_Add()
    {
        CommandCompleter.GetCompletions("git ad")
                        .Should().ContainSingle()
                        .Which.CompletionText.Should().Be("add");
    }

    [Fact]
    public void Git_Add_Parameters()
    {
        CommandCompleter.GetCompletions("git add")
                        .Should().Contain(x => x.CompletionText == "--all")
                        .And.Contain(x => x.CompletionText == "--patch")
                        .And.Contain(x => x.CompletionText == ".");
    }

    [Fact]
    public void Git_Commit()
    {
        CommandCompleter.GetCompletions("git com")
                        .Should().ContainSingle()
                        .Which.CompletionText.Should().Be("commit");
    }

    [Fact]
    public void Git_Commit_Parameters()
    {
        CommandCompleter.GetCompletions("git commit")
                        .Should().Contain(x => x.CompletionText == "--all")
                        .And.Contain(x => x.CompletionText == "--amend")
                        .And.Contain(x => x.CompletionText == "--message");
    }

    [Fact]
    public void Git_Commit_MultipleParameters()
    {
        CommandCompleter.GetCompletions("git commit -a -")
                        .Should().Contain(x => x.CompletionText == "--message")
                        .And.Contain(x => x.CompletionText == "--amend");
    }

    [Fact]
    public void Git_Stash()
    {
        CommandCompleter.GetCompletions("git st")
                        .Should().HaveCount(2)
                        .And.Contain(x => x.CompletionText == "stash")
                        .And.Contain(x => x.CompletionText == "status");
    }

    [Fact]
    public void Git_Stash_Subcommands()
    {
        CommandCompleter.GetCompletions("git stash")
                        .Should().Contain(x => x.CompletionText == "push")
                        .And.Contain(x => x.CompletionText == "pop")
                        .And.Contain(x => x.CompletionText == "apply")
                        .And.Contain(x => x.CompletionText == "list")
                        .And.Contain(x => x.CompletionText == "clear");
    }

    [Fact]
    public void Gh()
    {
        CommandCompleter.GetCompletions("gh")
                        .Should().Contain(x => x.CompletionText == "auth")
                        .And.Contain(x => x.CompletionText == "repo")
                        .And.Contain(x => x.CompletionText == "pr")
                        .And.Contain(x => x.CompletionText == "issue")
                        .And.Contain(x => x.CompletionText == "release");
    }

    [Fact]
    public void Gh_Repo()
    {
        CommandCompleter.GetCompletions("gh repo")
                        .Should().Contain(x => x.CompletionText == "clone")
                        .And.Contain(x => x.CompletionText == "create")
                        .And.Contain(x => x.CompletionText == "list");
    }

    [Fact]
    public void Gh_Repo_Clone()
    {
        CommandCompleter.GetCompletions("gh repo cl")
                        .Should().ContainSingle()
                        .Which.CompletionText.Should().Be("clone");
    }

    [Fact]
    public void Gh_Auth()
    {
        CommandCompleter.GetCompletions("gh auth")
                        .Should().Contain(x => x.CompletionText == "login")
                        .And.Contain(x => x.CompletionText == "logout")
                        .And.Contain(x => x.CompletionText == "status");
    }

    [Fact]
    public void Gh_Auth_Login()
    {
        CommandCompleter.GetCompletions("gh auth login")
                        .Should().Contain(x => x.CompletionText == "--web")
                        .And.Contain(x => x.CompletionText == "--hostname");
    }

    [Fact]
    public void Gh_Pr()
    {
        CommandCompleter.GetCompletions("gh pr")
                        .Should().Contain(x => x.CompletionText == "create")
                        .And.Contain(x => x.CompletionText == "list")
                        .And.Contain(x => x.CompletionText == "view")
                        .And.Contain(x => x.CompletionText == "checkout")
                        .And.Contain(x => x.CompletionText == "checks")
                        .And.Contain(x => x.CompletionText == "diff");
    }

    [Fact]
    public void Gh_Pr_View()
    {
        CommandCompleter.GetCompletions("gh pr v")
                        .Should().ContainSingle()
                        .Which.CompletionText.Should().Be("view");
    }

    [Fact]
    public void Gh_Pr_View_Parameters()
    {
        CommandCompleter.GetCompletions("gh pr view")
                        .Should().Contain(x => x.CompletionText == "--web")
                        .And.Contain(x => x.CompletionText == "--comments");
    }

    [Fact]
    public void Gh_Pr_Checks()
    {
        CommandCompleter.GetCompletions("gh pr checks")
                        .Should().Contain(x => x.CompletionText == "--web");
    }

    [Fact]
    public void Gh_Pr_Diff()
    {
        CommandCompleter.GetCompletions("gh pr di")
                        .Should().ContainSingle()
                        .Which.CompletionText.Should().Be("diff");
    }

    [Fact]
    public void Tre()
    {
        CommandCompleter.GetCompletions("tre")
                        .Should().Contain(x => x.CompletionText == "--all")
                        .And.Contain(x => x.CompletionText == "--directories")
                        .And.Contain(x => x.CompletionText == "--json");
    }

    [Fact]
    public void Tre_All()
    {
        CommandCompleter.GetCompletions("tre --a")
                        .Should().ContainSingle()
                        .Which.CompletionText.Should().Be("--all");
    }

    [Fact]
    public void Tre_Color()
    {
        CommandCompleter.GetCompletions("tre --color")
                        .Should().Contain(x => x.CompletionText == "automatic")
                        .And.Contain(x => x.CompletionText == "always")
                        .And.Contain(x => x.CompletionText == "never");
    }

    [Fact]
    public void Tre_MultipleParameters()
    {
        CommandCompleter.GetCompletions("tre --all --")
                        .Should().Contain(x => x.CompletionText == "--directories")
                        .And.Contain(x => x.CompletionText == "--json")
                        .And.Contain(x => x.CompletionText == "--limit");
    }

    [Fact]
    public void Lsd()
    {
        CommandCompleter.GetCompletions("lsd")
            .Should().Contain(x => x.CompletionText == "--all")
            .And.Contain(x => x.CompletionText == "--long")
            .And.Contain(x => x.CompletionText == "--tree")
            .And.Contain(x => x.CompletionText == "--help");
    }

    [Fact]
    public void Lsd_Color()
    {
        CommandCompleter.GetCompletions("lsd --color")
            .Should().Contain(x => x.CompletionText == "always")
            .And.Contain(x => x.CompletionText == "auto")
            .And.Contain(x => x.CompletionText == "never");
    }

    [Fact]
    public void Lsd_Icon()
    {
        CommandCompleter.GetCompletions("lsd --icon")
            .Should().Contain(x => x.CompletionText == "always")
            .And.Contain(x => x.CompletionText == "auto")
            .And.Contain(x => x.CompletionText == "never");
    }

    [Fact]
    public void Lsd_Sort()
    {
        CommandCompleter.GetCompletions("lsd --sort")
            .Should().Contain(x => x.CompletionText == "size")
            .And.Contain(x => x.CompletionText == "time")
            .And.Contain(x => x.CompletionText == "version")
            .And.Contain(x => x.CompletionText == "extension")
            .And.Contain(x => x.CompletionText == "git")
            .And.Contain(x => x.CompletionText == "none");
    }

    [Fact]
    public void Lsd_Permission()
    {
        CommandCompleter.GetCompletions("lsd --permission")
            .Should().Contain(x => x.CompletionText == "rwx")
            .And.Contain(x => x.CompletionText == "octal")
            .And.Contain(x => x.CompletionText == "attributes")
            .And.Contain(x => x.CompletionText == "disable");
    }

    [Fact]
    public void Dust()
    {
        CommandCompleter.GetCompletions("dust")
            .Should().Contain(x => x.CompletionText == "--depth")
            .And.Contain(x => x.CompletionText == "--number-of-lines")
            .And.Contain(x => x.CompletionText == "--help");
    }

    [Fact]
    public void Dust_OutputFormat()
    {
        CommandCompleter.GetCompletions("dust --output-format")
            .Should().Contain(x => x.CompletionText == "si")
            .And.Contain(x => x.CompletionText == "b")
            .And.Contain(x => x.CompletionText == "k")
            .And.Contain(x => x.CompletionText == "m")
            .And.Contain(x => x.CompletionText == "g")
            .And.Contain(x => x.CompletionText == "kb")
            .And.Contain(x => x.CompletionText == "mb")
            .And.Contain(x => x.CompletionText == "gb");
    }

    [Fact]
    public void Dust_Filetime()
    {
        CommandCompleter.GetCompletions("dust --filetime")
            .Should().Contain(x => x.CompletionText == "a")
            .And.Contain(x => x.CompletionText == "c")
            .And.Contain(x => x.CompletionText == "m");
    }

    [Fact]
    public void Winget()
    {
        CommandCompleter.GetCompletions("winget")
            .Should().Contain(x => x.CompletionText == "install")
            .And.Contain(x => x.CompletionText == "search")
            .And.Contain(x => x.CompletionText == "upgrade")
            .And.Contain(x => x.CompletionText == "uninstall")
            .And.Contain(x => x.CompletionText == "list")
            .And.Contain(x => x.CompletionText == "show")
            .And.Contain(x => x.CompletionText == "source")
            .And.Contain(x => x.CompletionText == "pin")
            .And.Contain(x => x.CompletionText == "export")
            .And.Contain(x => x.CompletionText == "import")
            .And.Contain(x => x.CompletionText == "download");
    }

    [SkippableFact]
    public void Winget_Install()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget ins")
            .Should().ContainSingle()
            .Which.CompletionText.Should().Be("install");
    }

    [SkippableFact]
    public void Winget_Install_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget install")
            .Should().Contain(x => x.CompletionText == "--silent")
            .And.Contain(x => x.CompletionText == "--interactive")
            .And.Contain(x => x.CompletionText == "--scope")
            .And.Contain(x => x.CompletionText == "--architecture")
            .And.Contain(x => x.CompletionText == "--version")
            .And.Contain(x => x.CompletionText == "--source")
            .And.Contain(x => x.CompletionText == "--exact");
    }

    [SkippableFact]
    public void Winget_Install_Scope()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget install --scope")
            .Should().Contain(x => x.CompletionText == "user")
            .And.Contain(x => x.CompletionText == "machine");
    }

    [SkippableFact]
    public void Winget_Install_Architecture()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget install --architecture")
            .Should().Contain(x => x.CompletionText == "x86")
            .And.Contain(x => x.CompletionText == "x64")
            .And.Contain(x => x.CompletionText == "arm")
            .And.Contain(x => x.CompletionText == "arm64");
    }

    [SkippableFact]
    public void Winget_Install_InstallerType()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget install --installer-type")
            .Should().Contain(x => x.CompletionText == "msix")
            .And.Contain(x => x.CompletionText == "msi")
            .And.Contain(x => x.CompletionText == "exe")
            .And.Contain(x => x.CompletionText == "portable");
    }

    [SkippableFact]
    public void Winget_Upgrade()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget upg")
            .Should().ContainSingle()
            .Which.CompletionText.Should().Be("upgrade");
    }

    [SkippableFact]
    public void Winget_Upgrade_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget upgrade")
            .Should().Contain(x => x.CompletionText == "--all")
            .And.Contain(x => x.CompletionText == "--silent")
            .And.Contain(x => x.CompletionText == "--interactive")
            .And.Contain(x => x.CompletionText == "--include-unknown")
            .And.Contain(x => x.CompletionText == "--include-pinned");
    }

    [SkippableFact]
    public void Winget_Uninstall()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget uni")
            .Should().ContainSingle()
            .Which.CompletionText.Should().Be("uninstall");
    }

    [SkippableFact]
    public void Winget_Uninstall_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget uninstall")
            .Should().Contain(x => x.CompletionText == "--silent")
            .And.Contain(x => x.CompletionText == "--force")
            .And.Contain(x => x.CompletionText == "--purge")
            .And.Contain(x => x.CompletionText == "--preserve");
    }

    [SkippableFact]
    public void Winget_Search()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget sea")
            .Should().ContainSingle()
            .Which.CompletionText.Should().Be("search");
    }

    [SkippableFact]
    public void Winget_Search_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget search")
            .Should().Contain(x => x.CompletionText == "--name")
            .And.Contain(x => x.CompletionText == "--id")
            .And.Contain(x => x.CompletionText == "--tag")
            .And.Contain(x => x.CompletionText == "--exact")
            .And.Contain(x => x.CompletionText == "--count");
    }

    [SkippableFact]
    public void Winget_List()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget li")
            .Should().ContainSingle()
            .Which.CompletionText.Should().Be("list");
    }

    [SkippableFact]
    public void Winget_List_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget list")
            .Should().Contain(x => x.CompletionText == "--upgrade-available")
            .And.Contain(x => x.CompletionText == "--include-unknown")
            .And.Contain(x => x.CompletionText == "--source");
    }

    [SkippableFact]
    public void Winget_Show()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget sh")
            .Should().ContainSingle()
            .Which.CompletionText.Should().Be("show");
    }

    [SkippableFact]
    public void Winget_Show_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget show")
            .Should().Contain(x => x.CompletionText == "--name")
            .And.Contain(x => x.CompletionText == "--id")
            .And.Contain(x => x.CompletionText == "--versions")
            .And.Contain(x => x.CompletionText == "--exact");
    }

    [SkippableFact]
    public void Winget_Source()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget sou")
            .Should().ContainSingle()
            .Which.CompletionText.Should().Be("source");
    }

    [SkippableFact]
    public void Winget_Source_Subcommands()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget source")
            .Should().Contain(x => x.CompletionText == "add")
            .And.Contain(x => x.CompletionText == "list")
            .And.Contain(x => x.CompletionText == "update")
            .And.Contain(x => x.CompletionText == "remove")
            .And.Contain(x => x.CompletionText == "reset")
            .And.Contain(x => x.CompletionText == "export");
    }

    [SkippableFact]
    public void Winget_Pin()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget pi")
            .Should().ContainSingle()
            .Which.CompletionText.Should().Be("pin");
    }

    [SkippableFact]
    public void Winget_Pin_Subcommands()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget pin")
            .Should().Contain(x => x.CompletionText == "add")
            .And.Contain(x => x.CompletionText == "remove")
            .And.Contain(x => x.CompletionText == "list")
            .And.Contain(x => x.CompletionText == "reset");
    }

    [SkippableFact]
    public void Winget_Export()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget exp")
            .Should().ContainSingle()
            .Which.CompletionText.Should().Be("export");
    }

    [SkippableFact]
    public void Winget_Export_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget export")
            .Should().Contain(x => x.CompletionText == "--output")
            .And.Contain(x => x.CompletionText == "--source")
            .And.Contain(x => x.CompletionText == "--include-versions");
    }

    [SkippableFact]
    public void Winget_Import()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget imp")
            .Should().ContainSingle()
            .Which.CompletionText.Should().Be("import");
    }

    [SkippableFact]
    public void Winget_Import_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget import")
            .Should().Contain(x => x.CompletionText == "--import-file")
            .And.Contain(x => x.CompletionText == "--ignore-unavailable")
            .And.Contain(x => x.CompletionText == "--ignore-versions");
    }

    [SkippableFact]
    public void Winget_Download()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget dow")
            .Should().ContainSingle()
            .Which.CompletionText.Should().Be("download");
    }

    [SkippableFact]
    public void Winget_Download_Parameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget download")
            .Should().Contain(x => x.CompletionText == "--download-directory")
            .And.Contain(x => x.CompletionText == "--id")
            .And.Contain(x => x.CompletionText == "--version");
    }

    [SkippableFact]
    public void Winget_MultipleParameters()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "winget is Windows-only");

        CommandCompleter.GetCompletions("winget install --silent --")
            .Should().Contain(x => x.CompletionText == "--scope")
            .And.Contain(x => x.CompletionText == "--architecture")
            .And.Contain(x => x.CompletionText == "--exact");
    }
}

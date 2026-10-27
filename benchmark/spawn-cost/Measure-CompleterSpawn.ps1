#Requires -Version 7.2
<#
.SYNOPSIS
Measures pscue-completer process wall time per Tab-completion scenario and attributes the
cost to child-process spawns (pwsh) versus in-process work.

.DESCRIPTION
The ArgumentCompleter is a NativeAOT console app spawned fresh by PowerShell on every Tab
press, and it launches child processes for dynamic completions. Git now launches directly,
while scoop still runs through Helpers.ExecuteCommand, which pays a full pwsh startup.

This harness measures three things, without modifying any production source:

  1. Wall time per scenario, by running the published native executable directly.
  2. Spawn count per scenario, by putting logging shims for `git`/`scoop` on PATH and
     counting invocations.
  3. Reference cost of each distinct command when launched through
     `pwsh -NoProfile -NonInteractive -Command "<cmd>"`, which is the scoop path and the
     former git path, plus a bare startup probe.

.PARAMETER CompleterPath
Path to the published pscue-completer executable. Auto-detected when omitted.

.PARAMETER Build
Publish the completer to artifacts/spawn-cost/bin before measuring.

.PARAMETER OutputDirectory
Run output directory. Defaults to artifacts/spawn-cost/<timestamp>.

.PARAMETER WarmupCount
Warmup invocations per scenario (excluded from statistics). Default 10.

.PARAMETER RepeatCount
Measured invocations per scenario. Default 100.

.PARAMETER SpawnRepeatCount
Measured invocations per standalone spawn command probe. Default 15.

.PARAMETER Seed
Seed for the randomized scenario order.

.PARAMETER TimeoutSeconds
Per-invocation timeout. Default 30.

.PARAMETER WorkingDirectory
Directory used as the child process working directory. Defaults to the repository root.

.PARAMETER SkipSpawnProbe
Skip the PATH-shim spawn counting and standalone spawn cost probes.

.PARAMETER SelfTest
Run internal assertions on the statistics and reporting helpers and exit. No measurements.

.EXAMPLE
pwsh -NoProfile -File ./benchmark/spawn-cost/Measure-CompleterSpawn.ps1 -SelfTest

.EXAMPLE
pwsh -NoProfile -File ./benchmark/spawn-cost/Measure-CompleterSpawn.ps1 -Build -WarmupCount 5 -RepeatCount 30
#>
[CmdletBinding()]
param(
    [string]$CompleterPath,
    [switch]$Build,
    [string]$OutputDirectory,
    [int]$WarmupCount = 10,
    [int]$RepeatCount = 100,
    [int]$SpawnRepeatCount = 15,
    [int]$Seed = 12345,
    [int]$TimeoutSeconds = 30,
    [string]$WorkingDirectory,
    [switch]$SkipSpawnProbe,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Core helpers (also exercised by -SelfTest)
# ---------------------------------------------------------------------------

function Get-NearestRankPercentile {
    param([double[]]$Sorted, [double]$Percentile)
    if ($null -eq $Sorted -or $Sorted.Count -eq 0) { return [double]::NaN }
    $rank = [math]::Ceiling(($Percentile / 100.0) * $Sorted.Count)
    $index = [int]$rank - 1
    if ($index -lt 0) { $index = 0 }
    if ($index -ge $Sorted.Count) { $index = $Sorted.Count - 1 }
    return $Sorted[$index]
}

function Get-Statistics {
    param([double[]]$Values)
    if ($null -eq $Values -or $Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    $n = $sorted.Count
    $sum = 0.0
    foreach ($v in $sorted) { $sum += $v }
    $mean = $sum / $n
    $variance = 0.0
    if ($n -gt 1) {
        foreach ($v in $sorted) {
            $d = $v - $mean
            $variance += $d * $d
        }
        $variance = $variance / ($n - 1)
    }
    [pscustomobject]@{
        Count  = $n
        Mean   = $mean
        Median = if ($n % 2 -eq 1) { $sorted[[int][math]::Floor($n / 2)] } else { ($sorted[($n / 2) - 1] + $sorted[$n / 2]) / 2.0 }
        Stdev  = [math]::Sqrt($variance)
        Min    = $sorted[0]
        Max    = $sorted[$n - 1]
        P90    = Get-NearestRankPercentile -Sorted $sorted -Percentile 90
        P95    = Get-NearestRankPercentile -Sorted $sorted -Percentile 95
    }
}

function Get-TextHash {
    param([string]$Text)
    if ($null -eq $Text) { $Text = '' }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash($bytes)
    }
    finally {
        $sha.Dispose()
    }
    return [System.Convert]::ToHexString($hash).Substring(0, 16)
}

function Format-Milliseconds {
    param([double]$Value)
    if ([double]::IsNaN($Value)) { return 'n/a' }
    return ('{0:N1}' -f $Value)
}

function Format-MillisecondsOrNull {
    param($Value)
    if ($null -eq $Value) { return 'n/a' }
    return Format-Milliseconds -Value ([double]$Value)
}

# ---------------------------------------------------------------------------
# Scenario matrix
# ---------------------------------------------------------------------------

function Get-Scenarios {
    <#
      Each scenario is: Name, CommandLine, Word, Cursor, Requires, Purpose.
      Cursor is the PowerShell cursorPosition argument and always equals the command
      line length, matching how the module passes $cursorPosition.
      Requires is a command that must exist for the scenario to be meaningful.
    #>
    $defs = @(
        @{ Name = 'baseline-no-args';     Line = $null;                        Word = $null;               Requires = $null;   Purpose = 'Process creation + NativeAOT startup + trivial Main + exit (args.Length != 3 short circuit).' }
        @{ Name = 'unknown-command';      Line = 'pscue-unknown-command ';     Word = '';                  Requires = $null;   Purpose = 'Parsing and dispatch with no completion tree and no spawns.' }
        @{ Name = 'static-pi';            Line = 'pi ';                        Word = '';                  Requires = $null;   Purpose = 'Tree construction and output with no dynamic arguments.' }
        @{ Name = 'git-root';             Line = 'git ';                       Word = '';                  Requires = 'git';   Purpose = 'Main/alias/others dynamic lookups.' }
        @{ Name = 'git-no-trailing-space';Line = 'git';                        Word = '';                  Requires = 'git';   Purpose = 'Control: navigation-time token search without a trailing space.' }
        @{ Name = 'git-prefix-nomatch';   Line = 'git pscue-no-match';         Word = 'pscue-no-match';    Requires = 'git';   Purpose = 'Unsuccessful dynamic token search during navigation.' }
        @{ Name = 'git-branches';         Line = 'git checkout ';              Word = '';                  Requires = 'git';   Purpose = 'Branch lookup.' }
        @{ Name = 'git-remotes';          Line = 'git fetch ';                 Word = '';                  Requires = 'git';   Purpose = 'Remote lookup (does not run a fetch).' }
        @{ Name = 'git-tags';             Line = 'git tag ';                   Word = '';                  Requires = 'git';   Purpose = 'Tag lookup (does not create a tag).' }
        @{ Name = 'gitwt-branches';       Line = 'git-wt switch ';             Word = '';                  Requires = 'git';   Purpose = 'Worktrunk branch lookup, distinguishing the GitWt caller.' }
        @{ Name = 'gitwt-param-branches'; Line = 'git-wt switch --base ';      Word = '';                  Requires = 'git';   Purpose = 'Parameter-value dynamic lookup (CommandParameter path).' }
        @{ Name = 'scoop-root';           Line = 'scoop ';                     Word = '';                  Requires = 'scoop'; Purpose = 'Scoop root: no package lookup expected.' }
        @{ Name = 'scoop-packages';       Line = 'scoop update ';              Word = '';                  Requires = 'scoop'; Purpose = 'Triggers scoop list (does not run an update).' }
        @{ Name = 'cd-directory';         Line = 'cd ';                        Word = '';                  Requires = $null;   Purpose = 'Filesystem enumeration for directory completion.' }
    )

    $result = foreach ($d in $defs) {
        $line = $d.Line
        $word = if ($null -eq $d.Word) { $null } else { [string]$d.Word }
        [pscustomobject]@{
            Name    = $d.Name
            CommandLine = $line
            Word    = $word
            Cursor  = if ($null -eq $line) { $null } else { ([string]$line).Length }
            Requires = $d.Requires
            Purpose = $d.Purpose
        }
    }
    return @($result)
}

function Get-SpawnCommands {
    <#
      The exact child commands Helpers.ExecuteCommand runs, mirrored from the factories:
        GitCommand.GetAllGitCommands   -> git --list-cmds=main,nohelpers | alias | others
        GitCommand.GetBranches         -> git branch --format='%(refname:short)'
        GitCommand.GetRemotes          -> git remote
        GitCommand.GetTags             -> git tag
        GitWtCommand.GetBranches       -> git branch --format='%(refname:short)'
        ScoopCommand.GetInstalledPackages -> scoop list
    #>
    return @(
        [pscustomobject]@{ Owner = 'git-root';      Command = 'git --list-cmds=main,nohelpers';      Label = 'git main commands'; Requires = 'git' }
        [pscustomobject]@{ Owner = 'git-root';      Command = 'git --list-cmds=alias';               Label = 'git aliases';       Requires = 'git' }
        [pscustomobject]@{ Owner = 'git-root';      Command = 'git --list-cmds=others';              Label = 'git extensions';    Requires = 'git' }
        [pscustomobject]@{ Owner = 'git-branches';  Command = "git branch --format='%(refname:short)'"; Label = 'git branches';   Requires = 'git' }
        [pscustomobject]@{ Owner = 'git-remotes';   Command = 'git remote';                          Label = 'git remotes';       Requires = 'git' }
        [pscustomobject]@{ Owner = 'git-tags';      Command = 'git tag';                             Label = 'git tags';          Requires = 'git' }
        [pscustomobject]@{ Owner = 'scoop-packages';Command = 'scoop list';                          Label = 'scoop packages';    Requires = 'scoop' }
    )
}

# ---------------------------------------------------------------------------
# Process execution
# ---------------------------------------------------------------------------

function Invoke-Probe {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @(),
        [string]$ChildWorkingDirectory,
        [hashtable]$EnvironmentOverrides,
        [int]$Timeout = 30
    )

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FilePath
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    if ($ChildWorkingDirectory) { $psi.WorkingDirectory = $ChildWorkingDirectory }
    foreach ($a in $Arguments) { $psi.ArgumentList.Add([string]$a) }

    if ($EnvironmentOverrides) {
        foreach ($key in $EnvironmentOverrides.Keys) {
            $value = $EnvironmentOverrides[$key]
            if ($null -eq $value) {
                [void]$psi.Environment.Remove($key)
            }
            else {
                $psi.Environment[$key] = [string]$value
            }
        }
    }

    $out = ''
    $err = ''
    $elapsedMs = $null
    $exitCode = $null
    $timedOut = $false
    $errorMessage = $null
    $proc = $null

    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $proc = [System.Diagnostics.Process]::Start($psi)
        $outTask = $proc.StandardOutput.ReadToEndAsync()
        $errTask = $proc.StandardError.ReadToEndAsync()
        $exited = $proc.WaitForExit($Timeout * 1000)
        $sw.Stop()
        $elapsedMs = $sw.Elapsed.TotalMilliseconds
        if (-not $exited) {
            $timedOut = $true
            try { $proc.Kill($true) } catch { }
        }
        try { $out = $outTask.GetAwaiter().GetResult() } catch { }
        try { $err = $errTask.GetAwaiter().GetResult() } catch { }
        if ($exited) { $exitCode = $proc.ExitCode }
    }
    catch {
        $errorMessage = $_.Exception.Message
    }
    finally {
        if ($null -ne $proc) { $proc.Dispose() }
    }

    $lines = @($out -split "`r?`n" | Where-Object { $_ -ne '' })

    [pscustomobject]@{
        ElapsedMs   = $elapsedMs
        ExitCode    = $exitCode
        TimedOut    = $timedOut
        StdoutLines = $lines.Count
        StdoutHash  = Get-TextHash -Text $out
        Stdout      = $out
        Stderr      = $err
        Error       = $errorMessage
    }
}

# ---------------------------------------------------------------------------
# Shims
# ---------------------------------------------------------------------------

function New-ShimDirectory {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$LogDirectory,
        [Parameter(Mandatory)][bool]$IsWindowsPlatform
    )

    if (Test-Path $Path) { Remove-Item -Recurse -Force $Path }
    [void](New-Item -ItemType Directory -Path $Path -Force)
    if (Test-Path $LogDirectory) { Remove-Item -Recurse -Force $LogDirectory }
    [void](New-Item -ItemType Directory -Path $LogDirectory -Force)

    if ($IsWindowsPlatform) {
        # Windows shims stay deliberately simple: the goal is counting invocations,
        # not reproducing git output. A fixed small payload keeps stdout comparable.
        # Each invocation records itself in a directory of its own. Sharing one log file
        # across concurrent children is not reliable, and cmd.exe cannot build a unique name
        # here: %RANDOM% is seeded identically in every cmd process on a machine, so parallel
        # shims would all pick the same file name. mkdir is atomic, so racing shims land in
        # distinct directories.
        $gitShim = @"
@echo off
setlocal
set /a attempt=0
:attempt
set /a attempt+=1
set "target=$LogDirectory\%RANDOM%-%attempt%"
mkdir "%target%" 2>nul
if errorlevel 1 (
  if %attempt% LSS 100 goto attempt
  exit /b 0
)
echo git %* > "%target%\entry.txt"
echo shim-main
exit /b 0
"@
        Set-Content -Path (Join-Path $Path 'git.cmd') -Value $gitShim -Encoding ascii

        $scoopShim = @"
@echo off
setlocal
set /a attempt=0
:attempt
set /a attempt+=1
set "target=$LogDirectory\%RANDOM%-%attempt%"
mkdir "%target%" 2>nul
if errorlevel 1 (
  if %attempt% LSS 100 goto attempt
  exit /b 0
)
echo scoop %* > "%target%\entry.txt"
echo Installed apps:
echo shim-package 1.0.0
exit /b 0
"@
        Set-Content -Path (Join-Path $Path 'scoop.cmd') -Value $scoopShim -Encoding ascii
    }
    else {
        $gitShim = @"
#!/bin/bash
record="$LogDirectory/`$-`$RANDOM"
mkdir -p "`$record"
echo "git `$*" > "`$record/entry.txt"
case "`$*" in
  *--list-cmds=main*) echo add; echo status; echo commit ;;
  *--list-cmds=alias*) echo st ;;
  *--list-cmds=others*) echo git-lfs ;;
  *branch*) echo main; echo feature/one ;;
  *remote*) echo origin ;;
  *tag*) echo v1.0.0 ;;
  *) echo main ;;
esac
exit 0
"@
        $gitPath = Join-Path $Path 'git'
        Set-Content -Path $gitPath -Value $gitShim -Encoding utf8
        & chmod '+x' $gitPath

        $scoopShim = @"
#!/bin/bash
record="$LogDirectory/`$-`$RANDOM"
mkdir -p "`$record"
echo "scoop `$*" > "`$record/entry.txt"
echo "Installed apps:"
echo "shim-package 1.0.0"
exit 0
"@
        $scoopPath = Join-Path $Path 'scoop'
        Set-Content -Path $scoopPath -Value $scoopShim -Encoding utf8
        & chmod '+x' $scoopPath
    }
}

function Get-ShimEntries {
    param(
        [Parameter(Mandatory)][string]$LogDirectory
    )
    # Every invocation records itself, so the count does not depend on when each child ran.
    # The file is named entry.txt because Windows redirects to a fixed name inside the
    # directory mkdir reserved for the invocation.
    if (-not (Test-Path $LogDirectory)) { return @() }
    return @(Get-ChildItem -Path $LogDirectory -Recurse -Filter 'entry.txt' -File -ErrorAction SilentlyContinue |
        ForEach-Object { Get-Content -Path $_.FullName -ErrorAction SilentlyContinue } |
        Where-Object { $_ -and $_.Trim() })
}

function Clear-ShimEntries {
    param(
        [Parameter(Mandatory)][string]$LogDirectory
    )
    if (-not (Test-Path $LogDirectory)) { return }
    Get-ChildItem -Path $LogDirectory -Directory -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

function Get-SpawnCounts {
    param(
        [Parameter(Mandatory)][string]$LogDirectory
    )
    $entries = @(Get-ShimEntries -LogDirectory $LogDirectory)
    if ($entries.Count -eq 0) { return @() }
    # Group by the spawned command line, normalizing "git"/"scoop" prefixes.
    $grouped = $entries | ForEach-Object { [pscustomobject]@{ Command = $_.Trim() } }
    return @($grouped | Group-Object -Property Command | ForEach-Object {
        [pscustomobject]@{ Command = $_.Name; Count = $_.Count }
    } | Sort-Object -Property Command)
}

function Get-SpawnCommandForEntry {
    param([string]$Entry)
    # Map a logged shim invocation back to one of the known spawn commands so counts
    # can be attributed to a factory.
    if ($Entry -match '^git --list-cmds=main')   { return 'git --list-cmds=main,nohelpers' }
    if ($Entry -match '^git --list-cmds=alias')  { return 'git --list-cmds=alias' }
    if ($Entry -match '^git --list-cmds=others') { return 'git --list-cmds=others' }
    if ($Entry -match '^git branch')             { return "git branch --format='%(refname:short)'" }
    if ($Entry -match '^git remote')             { return 'git remote' }
    if ($Entry -match '^git tag')                { return 'git tag' }
    if ($Entry -match '^scoop list')             { return 'scoop list' }
    return $Entry
}

function Format-SpawnBreakdown {
    param([hashtable]$Counts)
    if ($null -eq $Counts -or $Counts.Count -eq 0) { return '' }
    # Spawned commands contain '=' and spaces, so lead with the count and separate entries
    # with '; '. Attribution never re-parses this string; it uses the structured map.
    return (($Counts.GetEnumerator() | Sort-Object -Property Key |
        ForEach-Object { "$($_.Value)x $($_.Key)" }) -join '; ')
}

# ---------------------------------------------------------------------------
# Path and environment discovery
# ---------------------------------------------------------------------------

function Get-RepoRootFromScript {
    # benchmark/spawn-cost/Measure-CompleterSpawn.ps1 -> repo root
    $scriptDir = Split-Path -Parent $PSCommandPath
    return (Resolve-Path (Join-Path $scriptDir '..' '..')).Path
}

function Find-CompleterExecutable {
    param([Parameter(Mandatory)][string]$RepoRoot)

    $leaf = if ($IsWindows) { 'pscue-completer.exe' } else { 'pscue-completer' }
    $candidates = @(
        (Join-Path $RepoRoot "artifacts/spawn-cost/bin/$leaf"),
        (Join-Path $RepoRoot $leaf),
        (Join-Path $RepoRoot "module/$leaf"),
        (Join-Path $RepoRoot "publish/$leaf")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -Path $candidate -PathType Leaf) { return (Resolve-Path $candidate).Path }
    }

    # Fall back to any published build under artifacts/bin.
    $found = Get-ChildItem -Path (Join-Path $RepoRoot 'artifacts/bin') -Recurse -Filter $leaf -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'release' } |
        Select-Object -First 1
    if ($found) { return $found.FullName }

    return $null
}

function Get-Rid {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
    $os = if ($IsWindows) { 'win' } elseif ($IsMacOS) { 'osx' } else { 'linux' }
    return "$os-$arch"
}

function Test-CommandAvailable {
    param([Parameter(Mandatory)][string]$Name)
    return $null -ne (Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

# ---------------------------------------------------------------------------
# Self test
# ---------------------------------------------------------------------------

function Assert-True {
    param($Condition, [string]$Label)
    $script:selfTestState.Checks++
    if (-not $Condition) { [void]$script:selfTestState.Failures.Add($Label) }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Label)
    Assert-True -Condition ($Expected -eq $Actual) -Label "$Label (expected '$Expected', got '$Actual')"
}

function Invoke-SelfTest {
    $script:selfTestState = @{
        Checks   = 0
        Failures = [System.Collections.Generic.List[string]]::new()
    }
    $failures = $script:selfTestState.Failures

    # Statistics
    $stats = Get-Statistics -Values @(1.0, 2.0, 3.0, 4.0)
    Assert-Equal 4 $stats.Count 'stats count'
    Assert-Equal 2.5 $stats.Mean 'stats mean'
    Assert-Equal 2.5 $stats.Median 'stats median'
    Assert-Equal 1.0 $stats.Min 'stats min'
    Assert-Equal 4.0 $stats.Max 'stats max'
    Assert-True ($stats.Stdev -gt 1.28 -and $stats.Stdev -lt 1.30) 'stats stdev ~1.2909'

    # Single sample: stdev must be 0, not NaN
    $single = Get-Statistics -Values @(7.0)
    Assert-Equal 0.0 $single.Stdev 'single-sample stdev'
    Assert-Equal 7.0 $single.P90 'single-sample p90'

    # Nearest-rank percentile: p50 of 1..10 is 5, p90 is 9, p95 is 10
    $sorted = [double[]](1..10)
    Assert-Equal 5.0 (Get-NearestRankPercentile -Sorted $sorted -Percentile 50) 'nearest-rank p50'
    Assert-Equal 9.0 (Get-NearestRankPercentile -Sorted $sorted -Percentile 90) 'nearest-rank p90'
    Assert-Equal 10.0 (Get-NearestRankPercentile -Sorted $sorted -Percentile 95) 'nearest-rank p95'

    # Median averages the middle two values for even counts; p90/p95 are nearest-rank
    Assert-Equal 2.5 (Get-Statistics -Values @(1.0, 2.0, 3.0, 4.0)).Median 'even-count median'
    Assert-Equal 2.0 (Get-Statistics -Values @(1.0, 2.0, 3.0)).Median 'odd-count median'

    # Empty input returns null rather than throwing
    Assert-True ($null -eq (Get-Statistics -Values @())) 'empty stats is null'

    # Hashing is stable and length 16
    Assert-Equal (Get-TextHash -Text 'abc') (Get-TextHash -Text 'abc') 'hash stable'
    Assert-Equal 16 (Get-TextHash -Text 'abc').Length 'hash length'
    Assert-True ((Get-TextHash -Text 'abc') -ne (Get-TextHash -Text 'abd')) 'hash differs'

    # Scenario matrix sanity
    $scenarios = Get-Scenarios
    Assert-Equal 14 $scenarios.Count 'scenario count'
    Assert-True ($scenarios.Name -contains 'git-root') 'contains git-root'
    Assert-Equal $null ($scenarios | Where-Object Name -eq 'baseline-no-args').CommandLine 'baseline has no command line'
    Assert-Equal 4 ($scenarios | Where-Object Name -eq 'git-root').Cursor 'git-root cursor'
    Assert-Equal 3 ($scenarios | Where-Object Name -eq 'static-pi').Cursor 'static-pi cursor'

    # Spawn command table sanity
    $spawnCommands = Get-SpawnCommands
    Assert-True ($spawnCommands.Count -ge 6) 'spawn command count'
    Assert-True (($spawnCommands | Where-Object Command -eq 'scoop list').Count -eq 1) 'spawn table has scoop list'

    # Spawn log mapping
    Assert-Equal 'git --list-cmds=alias' (Get-SpawnCommandForEntry -Entry 'git --list-cmds=alias') 'map alias'
    Assert-Equal "git branch --format='%(refname:short)'" (Get-SpawnCommandForEntry -Entry "git branch --format='%(refname:short)'") 'map branch'
    Assert-Equal 'scoop list' (Get-SpawnCommandForEntry -Entry 'scoop list') 'map scoop'
    Assert-Equal 'something-else' (Get-SpawnCommandForEntry -Entry 'something-else') 'map unknown passthrough'

    # Breakdown formatting sorts by command and joins with '; '
    Assert-Equal '1x a; 2x b' (Format-SpawnBreakdown -Counts @{ 'b' = 2; 'a' = 1 }) 'breakdown sorted'
    Assert-Equal '' (Format-SpawnBreakdown -Counts @{}) 'breakdown empty'

    # Shims: create them and confirm the git shim records an invocation
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("pscue-selftest-" + [guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $tempRoot -Force)
    try {
        $shimDir = Join-Path $tempRoot 'shim'
        $logDirectory = Join-Path $tempRoot 'spawns'
        New-ShimDirectory -Path $shimDir -LogDirectory $logDirectory -IsWindowsPlatform $IsWindows
        $leaf = if ($IsWindows) { 'git.cmd' } else { 'git' }
        Assert-True (Test-Path (Join-Path $shimDir $leaf)) 'git shim created'

        if (-not $IsWindows) {
            $pwshPath = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
            if ($pwshPath) {
                $shimmed = Invoke-Probe -FilePath $pwshPath `
                    -Arguments @('-NoProfile', '-NonInteractive', '-Command', 'git --list-cmds=alias') `
                    -ChildWorkingDirectory $tempRoot `
                    -EnvironmentOverrides @{ PATH = "${shimDir}$([System.IO.Path]::PathSeparator)$env:PATH" } `
                    -Timeout 60
                Assert-Equal 0 $shimmed.ExitCode 'shim invocation exit code'
                # @() keeps a single group from unwrapping: without it, .Count reads the group's
                # own member count instead of the number of groups.
                $counts = @(Get-SpawnCounts -LogDirectory $logDirectory)
                Assert-Equal 1 $counts.Count 'shim recorded one command'
                Assert-Equal 1 $counts[0].Count 'shim recorded one invocation'
            }
        }

        # Concurrent launches must all be counted. A shim that shares one log file, or that names
        # its file from %RANDOM%, drops entries when children overlap, which is exactly the case
        # the completion tree creates.
        Clear-ShimEntries -LogDirectory $logDirectory
        $shimPath = Join-Path $shimDir $leaf
        $concurrent = @(1..4 | ForEach-Object {
            Start-Process -FilePath $shimPath -ArgumentList 'branch' -PassThru
        })
        $null = $concurrent | Wait-Process -Timeout 60 -ErrorAction SilentlyContinue
        $concurrentCounts = @(Get-SpawnCounts -LogDirectory $logDirectory)
        Assert-Equal 1 $concurrentCounts.Count 'concurrent shims recorded one command'
        Assert-Equal 4 $concurrentCounts[0].Count 'concurrent shim recorded every invocation'
    }
    finally {
        Remove-Item -Recurse -Force $tempRoot -ErrorAction SilentlyContinue
    }

    $checks = $script:selfTestState.Checks
    if ($failures.Count -gt 0) {
        Write-Host "SELF TEST FAILED ($($failures.Count) of $checks checks)" -ForegroundColor Red
        foreach ($failure in $failures) { Write-Host "  - $failure" -ForegroundColor Red }
        return 1
    }

    Write-Host "SELF TEST PASSED ($checks checks)" -ForegroundColor Green
    return 0
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

$repoRoot = Get-RepoRootFromScript

if ($SelfTest) {
    exit (Invoke-SelfTest)
}

if ($RepeatCount -lt 1) {
    Write-Error "RepeatCount must be at least 1 (got $RepeatCount)."
    exit 2
}
if ($WarmupCount -lt 0) {
    Write-Error "WarmupCount must not be negative (got $WarmupCount)."
    exit 2
}
if ($SpawnRepeatCount -lt 2 -and -not $SkipSpawnProbe) {
    Write-Warning 'SpawnRepeatCount < 2 leaves no measured samples after the discarded first run; spawn-cost statistics will be n/a.'
}

$rid = Get-Rid
$isWindowsPlatform = $IsWindows

if (-not $WorkingDirectory) { $WorkingDirectory = $repoRoot }
if (-not (Test-Path -Path $WorkingDirectory -PathType Container)) {
    Write-Error "WorkingDirectory does not exist: $WorkingDirectory"
    exit 2
}

if ($Build) {
    $buildOutput = Join-Path $repoRoot 'artifacts/spawn-cost/bin'
    [void](New-Item -ItemType Directory -Path $buildOutput -Force)
    Write-Host "Publishing completer to $buildOutput ..."
    & dotnet publish (Join-Path $repoRoot 'src/PSCue.ArgumentCompleter/PSCue.ArgumentCompleter.csproj') `
        -c Release -r $rid -o $buildOutput
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
        exit 2
    }
    $buildLeaf = if ($isWindowsPlatform) { 'pscue-completer.exe' } else { 'pscue-completer' }
    $CompleterPath = Join-Path $buildOutput $buildLeaf
}

if (-not $CompleterPath) { $CompleterPath = Find-CompleterExecutable -RepoRoot $repoRoot }
if (-not $CompleterPath -or -not (Test-Path -Path $CompleterPath -PathType Leaf)) {
    Write-Error "Could not locate the pscue-completer executable. Pass -CompleterPath or use -Build."
    exit 2
}
$CompleterPath = (Resolve-Path $CompleterPath).Path

if (-not $OutputDirectory) {
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
    $OutputDirectory = Join-Path $repoRoot "artifacts/spawn-cost/$stamp"
}
[void](New-Item -ItemType Directory -Path $OutputDirectory -Force)

$dataDir = Join-Path $OutputDirectory 'data'
[void](New-Item -ItemType Directory -Path $dataDir -Force)

$pwshCommand = Get-Command pwsh -ErrorAction SilentlyContinue
$pwshPath = if ($pwshCommand) { $pwshCommand.Source } else { $null }

$completerHash = (Get-FileHash -Path $CompleterPath -Algorithm SHA256).Hash
$sourceRevision = (& git -C $repoRoot rev-parse HEAD 2>$null)
$sourceShortRevision = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
$sourceDirty = [bool](& git -C $repoRoot status --porcelain 2>$null)

$metadata = [ordered]@{
    Harness             = 'benchmark/spawn-cost/Measure-CompleterSpawn.ps1'
    StartedUtc          = (Get-Date).ToUniversalTime().ToString('o')
    RepoRoot            = $repoRoot
    WorkingDirectory    = $WorkingDirectory
    CompleterPath       = $CompleterPath
    CompleterSha256     = $completerHash
    Rid                 = $rid
    IsWindows           = $isWindowsPlatform
    IsWsl               = if ($IsLinux -and (Test-Path '/proc/sys/kernel/osrelease')) { (Get-Content '/proc/sys/kernel/osrelease' -Raw).Trim() -match 'microsoft' } else { $false }
    OsDescription       = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    Architecture        = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    PowerShellVersion   = $PSVersionTable.PSVersion.ToString()
    DotnetSdks          = @(& dotnet --list-sdks 2>$null)
    GitVersion          = (& git --version 2>$null)
    PwshPath            = $pwshPath
    GitPath             = if (Test-CommandAvailable -Name git) { (Get-Command git).Source } else { $null }
    ScoopPath           = if (Test-CommandAvailable -Name scoop) { (Get-Command scoop).Source } else { $null }
    SourceRevision      = $sourceRevision
    SourceShortRevision = $sourceShortRevision
    SourceDirty         = $sourceDirty
    Seed                = $Seed
    WarmupCount         = $WarmupCount
    RepeatCount         = $RepeatCount
    SpawnRepeatCount    = $SpawnRepeatCount
    TimeoutSeconds      = $TimeoutSeconds
}

Write-Host "Completer: $CompleterPath"
Write-Host "RID: $rid   Output: $OutputDirectory"

$allScenarios = Get-Scenarios
$scenarios = @($allScenarios | Where-Object {
    if (-not $_.Requires) { return $true }
    $available = Test-CommandAvailable -Name $_.Requires
    if (-not $available) { Write-Host "Skipping '$($_.Name)': '$($_.Requires)' not available." }
    return $available
})

$baseEnvironment = @{
    PSCUE_DATA_DIR = $dataDir
    PSCUE_DEBUG    = '0'
}

# ---- Phase 1: wall time per scenario ---------------------------------------
Write-Host ''
Write-Host "Measuring wall time ($WarmupCount warmup + $RepeatCount measured per scenario) ..."

$samples = [System.Collections.Generic.List[object]]::new()
$random = [System.Random]::new($Seed)
$order = @($scenarios | Sort-Object { $random.Next() })

foreach ($scenario in $order) {
    $arguments = @()
    if ($null -ne $scenario.CommandLine) {
        $arguments = @([string]$scenario.Word, [string]$scenario.CommandLine, [string]$scenario.Cursor)
    }

    $firstObserved = $null
    $sequence = 0

    for ($i = 0; $i -lt ($WarmupCount + $RepeatCount); $i++) {
        $isWarmup = $i -lt $WarmupCount
        $probe = Invoke-Probe -FilePath $CompleterPath -Arguments $arguments `
            -ChildWorkingDirectory $WorkingDirectory `
            -EnvironmentOverrides $baseEnvironment `
            -Timeout $TimeoutSeconds

        $record = [pscustomobject]@{
            Scenario   = $scenario.Name
            Build      = 'pristine'
            Sequence   = $sequence
            Phase      = if ($isWarmup) { 'warmup' } else { 'measured' }
            ElapsedMs  = $probe.ElapsedMs
            ExitCode   = $probe.ExitCode
            TimedOut   = $probe.TimedOut
            StdoutLines= $probe.StdoutLines
            StdoutHash = $probe.StdoutHash
            Error      = $probe.Error
        }

        if ($i -eq 0) { $firstObserved = $record }
        if (-not $isWarmup) { $samples.Add($record) }
        $sequence++
    }

    $measured = @($samples | Where-Object { $_.Scenario -eq $scenario.Name -and $_.Phase -eq 'measured' })
    $successful = @($measured | Where-Object { -not $_.TimedOut -and $null -ne $_.ElapsedMs -and $null -eq $_.Error })
    $ok = @($successful | Measure-Object -Property ElapsedMs)
    $stats = Get-Statistics -Values ([double[]]@($successful | ForEach-Object { $_.ElapsedMs }))
    $summary = if ($stats) { Format-Milliseconds $stats.Median } else { 'n/a' }
    Write-Host ("  {0,-22} median {1,9} ms   ({2}/{3} ok, {4} lines)" -f `
        $scenario.Name, $summary, $successful.Count, $measured.Count, ($firstObserved.StdoutLines))
}

# ---- Phase 2: spawn counting via PATH shims --------------------------------
$spawnCounts = [System.Collections.Generic.List[object]]::new()
$spawnBreakdownMap = @{}

if (-not $SkipSpawnProbe) {
    Write-Host ''
    Write-Host 'Counting spawns with PATH shims ...'

    $shimRoot = Join-Path $OutputDirectory 'shims'
    $shimDir = Join-Path $shimRoot 'bin'
    $shimLog = Join-Path $shimRoot 'spawns'
    New-ShimDirectory -Path $shimDir -LogDirectory $shimLog -IsWindowsPlatform $isWindowsPlatform

    $pathSeparator = [System.IO.Path]::PathSeparator
    $shimmedPath = "$shimDir$pathSeparator$env:PATH"

    foreach ($scenario in $scenarios) {
        Clear-ShimEntries -LogDirectory $shimLog

        $arguments = @()
        if ($null -ne $scenario.CommandLine) {
            $arguments = @([string]$scenario.Word, [string]$scenario.CommandLine, [string]$scenario.Cursor)
        }
        $env2 = @{
            PSCUE_DATA_DIR = $dataDir
            PSCUE_DEBUG    = '0'
            PATH           = $shimmedPath
        }

        $probe = Invoke-Probe -FilePath $CompleterPath -Arguments $arguments `
            -ChildWorkingDirectory $WorkingDirectory `
            -EnvironmentOverrides $env2 `
            -Timeout $TimeoutSeconds

        $entries = @(Get-ShimEntries -LogDirectory $shimLog)
        $total = $entries.Count

        $byCommand = @{}
        foreach ($entry in $entries) {
            $key = Get-SpawnCommandForEntry -Entry $entry.Trim()
            if (-not $byCommand.ContainsKey($key)) { $byCommand[$key] = 0 }
            $byCommand[$key]++
        }

        $record = [pscustomobject]@{
            Scenario    = $scenario.Name
            TotalSpawns = $total
            Breakdown   = (Format-SpawnBreakdown -Counts $byCommand)
            StdoutLines = $probe.StdoutLines
            ExitCode    = $probe.ExitCode
        }
        $spawnCounts.Add($record)
        $spawnBreakdownMap[$scenario.Name] = $byCommand
        Write-Host ("  {0,-22} {1,3} spawn(s)  {2}" -f $scenario.Name, $total, $record.Breakdown)
    }
}

# ---- Phase 3: standalone spawn cost ---------------------------------------
$spawnCosts = [System.Collections.Generic.List[object]]::new()

if (-not $SkipSpawnProbe -and $pwshPath) {
    Write-Host ''
    Write-Host "Measuring standalone spawn cost ($SpawnRepeatCount runs each) ..."

    $commands = @([pscustomobject]@{ Command = 'exit 0'; Label = 'pwsh startup only'; Requires = $null })
    $commands += @(Get-SpawnCommands | Where-Object {
        if (-not $_.Requires) { return $true }
        return (Test-CommandAvailable -Name $_.Requires)
    })

    foreach ($entry in $commands) {
        $times = [System.Collections.Generic.List[double]]::new()
        $failed = 0
        for ($i = 0; $i -lt $SpawnRepeatCount; $i++) {
            $probe = Invoke-Probe -FilePath $pwshPath `
                -Arguments @('-NoProfile', '-NonInteractive', '-Command', $entry.Command) `
                -ChildWorkingDirectory $WorkingDirectory `
                -EnvironmentOverrides @{ PSCUE_DEBUG = '0' } `
                -Timeout $TimeoutSeconds
            if ($probe.TimedOut -or $null -eq $probe.ElapsedMs -or $probe.ExitCode -ne 0) { $failed++ }
            elseif ($i -gt 0) { $times.Add([double]$probe.ElapsedMs) }
        }
        $stats = Get-Statistics -Values ([double[]]$times)
        $spawnCosts.Add([pscustomobject]@{
            Command = $entry.Command
            Label   = $entry.Label
            Stats   = $stats
            Failed  = $failed
        })
        $medianText = if ($stats) { Format-Milliseconds $stats.Median } else { 'n/a' }
        $sampleCount = if ($stats) { $stats.Count } else { 0 }
        Write-Host ("  {0,-42} median {1,9} ms  (n={2}, failed={3})" -f `
            $entry.Label, $medianText, $sampleCount, $failed)
    }
}

# ---- Phase 4: write outputs ----------------------------------------------
$samplesCsv = @($samples)
$samplesCsv | Export-Csv -Path (Join-Path $OutputDirectory 'samples.csv') -NoTypeInformation

$summaryRows = foreach ($scenario in $scenarios) {
    $measured = @($samples | Where-Object { $_.Scenario -eq $scenario.Name })
    $successful = @($measured | Where-Object { -not $_.TimedOut -and $null -ne $_.ElapsedMs -and $null -eq $_.Error })
    $first = @($measured | Sort-Object Sequence | Select-Object -First 1)
    $stats = Get-Statistics -Values ([double[]]@($successful | ForEach-Object { $_.ElapsedMs }))
    $spawn = @($spawnCounts | Where-Object Scenario -eq $scenario.Name | Select-Object -First 1)
    [pscustomobject]@{
        Scenario      = $scenario.Name
        CommandLine   = $scenario.CommandLine
        Runs          = $measured.Count
        Successful    = $successful.Count
        Failures      = $measured.Count - $successful.Count
        MeanMs        = if ($stats) { [math]::Round($stats.Mean, 2) } else { $null }
        MedianMs      = if ($stats) { [math]::Round($stats.Median, 2) } else { $null }
        StdevMs       = if ($stats) { [math]::Round($stats.Stdev, 2) } else { $null }
        MinMs         = if ($stats) { [math]::Round($stats.Min, 2) } else { $null }
        MaxMs         = if ($stats) { [math]::Round($stats.Max, 2) } else { $null }
        P90Ms         = if ($stats) { [math]::Round($stats.P90, 2) } else { $null }
        P95Ms         = if ($stats) { [math]::Round($stats.P95, 2) } else { $null }
        FirstObservedMs = if ($first) { [math]::Round($first.ElapsedMs, 2) } else { $null }
        StdoutLines   = if ($first) { $first.StdoutLines } else { $null }
        TotalSpawns   = if ($spawn) { $spawn.TotalSpawns } else { $null }
        SpawnBreakdown= if ($spawn) { $spawn.Breakdown } else { $null }
    }
}
$summaryRows | Export-Csv -Path (Join-Path $OutputDirectory 'summary.csv') -NoTypeInformation

$spawnCostRows = foreach ($entry in $spawnCosts) {
    [pscustomobject]@{
        Label    = $entry.Label
        Command  = $entry.Command
        Runs     = if ($entry.Stats) { $entry.Stats.Count } else { 0 }
        Failed   = $entry.Failed
        MedianMs = if ($entry.Stats) { [math]::Round($entry.Stats.Median, 2) } else { $null }
        MeanMs   = if ($entry.Stats) { [math]::Round($entry.Stats.Mean, 2) } else { $null }
        MinMs    = if ($entry.Stats) { [math]::Round($entry.Stats.Min, 2) } else { $null }
        MaxMs    = if ($entry.Stats) { [math]::Round($entry.Stats.Max, 2) } else { $null }
    }
}
if (@($spawnCostRows).Count -gt 0) {
    $spawnCostRows | Export-Csv -Path (Join-Path $OutputDirectory 'spawn-costs.csv') -NoTypeInformation
}
if ($spawnCounts.Count -gt 0) {
    $spawnCounts | Export-Csv -Path (Join-Path $OutputDirectory 'spawn-counts.csv') -NoTypeInformation
}

$metadata['FinishedUtc'] = (Get-Date).ToUniversalTime().ToString('o')
$metadata | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $OutputDirectory 'metadata.json')

# ---- Phase 5: report ------------------------------------------------------
$report = [System.Collections.Generic.List[string]]::new()
$report.Add('# pscue-completer spawn cost measurement')
$report.Add('')
$report.Add("Run: $($metadata.StartedUtc)")
$report.Add('')
$report.Add('## Environment')
$report.Add('')
$report.Add('| Item | Value |')
$report.Add('| --- | --- |')
$report.Add("| OS | $($metadata.OsDescription) |")
$report.Add("| Architecture | $($metadata.Architecture) |")
$report.Add("| RID | $($metadata.Rid) |")
$report.Add("| WSL | $($metadata.IsWsl) |")
$report.Add("| PowerShell | $($metadata.PowerShellVersion) |")
$report.Add("| Source revision | $($metadata.SourceShortRevision) (dirty: $($metadata.SourceDirty)) |")
$report.Add("| Completer SHA256 | $($metadata.CompleterSha256) |")
$report.Add("| pwsh | $($metadata.PwshPath) |")
$report.Add("| git | $($metadata.GitPath) |")
$report.Add("| scoop | $($metadata.ScoopPath) |")
$report.Add('')
$report.Add('## Scenario wall time (pristine executable)')
$report.Add('')
$report.Add('| Scenario | Command line | Median ms | P90 ms | Min ms | Max ms | N | Spawns |')
$report.Add('| --- | --- | --- | --- | --- | --- | --- | --- |')
foreach ($row in $summaryRows) {
    $cmd = if ($null -eq $row.CommandLine) { '(no args)' } else { '`' + $row.CommandLine + '`' }
    $report.Add("| $($row.Scenario) | $cmd | $(Format-MillisecondsOrNull $row.MedianMs) | $(Format-MillisecondsOrNull $row.P90Ms) | $(Format-MillisecondsOrNull $row.MinMs) | $(Format-MillisecondsOrNull $row.MaxMs) | $($row.Successful) | $($row.TotalSpawns) |")
}
$report.Add('')
$report.Add('## Standalone spawn cost')
$report.Add('')
$report.Add('Each probe runs `pwsh -NoProfile -NonInteractive -Command "<command>"`, the form `Helpers.ExecuteCommand` uses. Git no longer launches this way, so its probes are reference costs rather than the current git launch path.')
$report.Add('')
$report.Add('| Probe | Command | Median ms | Min ms | Max ms | N |')
$report.Add('| --- | --- | --- | --- | --- | --- |')
foreach ($row in $spawnCostRows) {
    $report.Add("| $($row.Label) | `'$($row.Command)`' | $(Format-MillisecondsOrNull $row.MedianMs) | $(Format-MillisecondsOrNull $row.MinMs) | $(Format-MillisecondsOrNull $row.MaxMs) | $($row.Runs) |")
}
$report.Add('')
$report.Add('These probes measure the PowerShell-wrapped form. Git now launches directly, so its rows are reference costs for the old path; only scoop still uses the wrapper.')
$report.Add('')
$report.Add('## Attribution')
$report.Add('')
$report.Add('Derived cost per scenario = observed spawn count x measured standalone cost for that command. The residual is wall time not explained by spawns.')
$report.Add('')
$report.Add('| Scenario | Wall median ms | Spawns | Derived spawn ms | Residual ms |')
$report.Add('| --- | --- | --- | --- | --- |')

$costByCommand = @{}
foreach ($row in $spawnCostRows) { $costByCommand[$row.Command] = [double]$row.MedianMs }

foreach ($row in $summaryRows) {
    $derived = 0.0
    $counts = if ($spawnBreakdownMap.ContainsKey($row.Scenario)) { $spawnBreakdownMap[$row.Scenario] } else { @{} }
    foreach ($key in $counts.Keys) {
        if ($costByCommand.ContainsKey($key)) {
            $derived += ([double]$counts[$key]) * $costByCommand[$key]
        }
    }
    $wall = if ($null -ne $row.MedianMs) { [double]$row.MedianMs } else { 0.0 }
    $residual = if ($null -ne $row.MedianMs) { $wall - $derived } else { $null }
    $report.Add("| $($row.Scenario) | $(Format-MillisecondsOrNull $row.MedianMs) | $($row.TotalSpawns) | $(Format-Milliseconds $derived) | $(Format-MillisecondsOrNull $residual) |")
}

$report.Add('')
$report.Add('## Notes and limits')
$report.Add('')
$report.Add('- Wall time is the child process lifetime only; PowerShell invocation and result conversion add latency outside the executable.')
$report.Add('- The `baseline-no-args` row is process creation + NativeAOT startup + a trivial Main + exit, not pure runtime startup.')
$report.Add('- Spawn counts come from PATH shims, so they are exact for `git`/`scoop` name resolution but the shim replaces the real command output.')
$report.Add('- `cd-directory` is exercised through the executable dispatcher, which is not how the installed module reaches `cd` completion.')
$report.Add('- Scoop scenarios are Windows-only: on other platforms the dispatcher does not build the scoop tree.')
$report.Add('- Standalone spawn cost is PowerShell startup plus command execution and I/O, not pure PowerShell startup.')
$report.Add('- Derived spawn cost is an upper bound: each probe starts a fresh, cold `pwsh`, while sequential spawns inside one completion may benefit from warm OS caches. A negative residual therefore means the spawns account for all observed wall time.')
$report.Add('- Instrumentation overhead is zero: no production source is modified by this harness.')

$report | Set-Content -Path (Join-Path $OutputDirectory 'report.md')

Write-Host ''
Write-Host "Outputs written to $OutputDirectory"
Write-Host '  metadata.json, samples.csv, summary.csv, spawn-counts.csv, spawn-costs.csv, report.md'
exit 0

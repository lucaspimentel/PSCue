#Requires -Version 7.2
<#
.SYNOPSIS
Dependency-free regression checks for the spawn-cost measurement harness.

.DESCRIPTION
Validates the harness's internal helpers via its -SelfTest mode, checks that invalid
inputs fail loudly, and (when a published completer is available) runs a short
integration measurement and asserts the expected output files are produced.

Latency thresholds are deliberately not asserted: results are machine-dependent.

.EXAMPLE
pwsh -NoProfile -File ./test/test-scripts/test-completer-timing.ps1

.EXAMPLE
pwsh -NoProfile -File ./test/test-scripts/test-completer-timing.ps1 -CompleterPath ./artifacts/spawn-cost/bin/pscue-completer
#>
[CmdletBinding()]
param(
    [string]$CompleterPath,
    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$failures = [System.Collections.Generic.List[string]]::new()

function Assert-True {
    param($Condition, [string]$Label)
    if (-not $Condition) { [void]$failures.Add($Label) }
}

$scriptDir = Split-Path -Parent $PSCommandPath
if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $scriptDir '..' '..')).Path
}

$harness = Join-Path $RepoRoot 'benchmark/spawn-cost/Measure-CompleterSpawn.ps1'
$pwshPath = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
Assert-True ($null -ne $pwshPath) 'pwsh is available'
Assert-True (Test-Path $harness) "harness script exists: $harness"

if (-not (Test-Path $harness) -or -not $pwshPath) {
    Write-Host 'FAILED' -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  - $f" -ForegroundColor Red }
    exit 1
}

# 1. Harness self-test must pass.
& $pwshPath -NoProfile -File $harness -SelfTest
Assert-True ($LASTEXITCODE -eq 0) "harness -SelfTest exit code 0 (got $LASTEXITCODE)"

# 2. Invalid repeat count must fail, not silently succeed.
& $pwshPath -NoProfile -File $harness -RepeatCount 0 *> $null
Assert-True ($LASTEXITCODE -ne 0) "invalid RepeatCount exits nonzero (got $LASTEXITCODE)"

# 3. A missing completer must fail.
& $pwshPath -NoProfile -File $harness -RepeatCount 1 `
    -CompleterPath (Join-Path ([System.IO.Path]::GetTempPath()) 'pscue-does-not-exist') *> $null
Assert-True ($LASTEXITCODE -ne 0) "missing completer exits nonzero (got $LASTEXITCODE)"

# 4. Optional short integration run.
if ($CompleterPath -and (Test-Path -Path $CompleterPath -PathType Leaf)) {
    $outDir = Join-Path ([System.IO.Path]::GetTempPath()) ("pscue-timing-it-" + [guid]::NewGuid().ToString('N'))
    try {
        & $pwshPath -NoProfile -File $harness `
            -CompleterPath $CompleterPath `
            -WarmupCount 0 -RepeatCount 2 -SpawnRepeatCount 1 `
            -OutputDirectory $outDir *> $null
        Assert-True ($LASTEXITCODE -eq 0) "integration run exit code 0 (got $LASTEXITCODE)"
        foreach ($expected in @('metadata.json', 'samples.csv', 'summary.csv', 'report.md')) {
            Assert-True (Test-Path (Join-Path $outDir $expected)) "integration produced $expected"
        }
        $report = Get-Content -Path (Join-Path $outDir 'report.md') -Raw -ErrorAction SilentlyContinue
        Assert-True ($report -match 'baseline-no-args') 'report contains baseline-no-args row'
        Assert-True ($report -match 'git-root') 'report contains git-root row'
    }
    finally {
        Remove-Item -Recurse -Force $outDir -ErrorAction SilentlyContinue
    }
}
else {
    Write-Host 'Skipping integration run (no -CompleterPath given).'
}

if ($failures.Count -gt 0) {
    Write-Host "FAILED ($($failures.Count))" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host "  - $f" -ForegroundColor Red }
    exit 1
}

Write-Host 'PASSED' -ForegroundColor Green
exit 0

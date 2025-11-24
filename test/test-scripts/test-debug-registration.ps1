# Test with debug output enabled
$env:PSCUE_DEBUG = "1"

Write-Host "Loading PSCue with debug output..." -ForegroundColor Cyan
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force

Write-Host ""
Write-Host "Testing TabExpansion2..." -ForegroundColor Cyan
$result = TabExpansion2 'git st' 6
Write-Host "Found $($result.CompletionMatches.Count) completions" -ForegroundColor Gray

# Check if completer was invoked
$testLog = Join-Path $env:TEMP "pscue-test-invocation.log"
if (Test-Path $testLog) {
    Remove-Item $testLog
}

# Check what completions TabExpansion2 is actually returning

Write-Host "Testing what completions are being returned..." -ForegroundColor Cyan
Write-Host ""

# Load module
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force
$env:PSCUE_PID = $PID

# Test TabExpansion2
Write-Host "TabExpansion2 results for 'git st':" -ForegroundColor Cyan
$result = TabExpansion2 'git st' 6

Write-Host "  Total: $($result.CompletionMatches.Count) completions" -ForegroundColor Gray
Write-Host ""

foreach ($match in $result.CompletionMatches) {
    Write-Host "  CompletionText: $($match.CompletionText)" -ForegroundColor Yellow
    Write-Host "  ListItemText:   $($match.ListItemText)" -ForegroundColor Gray
    Write-Host "  ResultType:     $($match.ResultType)" -ForegroundColor Gray
    Write-Host "  ToolTip:        $($match.ToolTip)" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "Analysis:" -ForegroundColor Cyan
if ($result.CompletionMatches.Count -gt 0) {
    $firstMatch = $result.CompletionMatches[0]

    if ($firstMatch.ResultType -eq 'ProviderItem' -or $firstMatch.ResultType -eq 'ProviderContainer') {
        Write-Host "  ⚠️  These look like FILE/DIRECTORY completions (ResultType: $($firstMatch.ResultType))" -ForegroundColor Yellow
        Write-Host "  This means PSCue completer is NOT being invoked" -ForegroundColor Yellow
    } elseif ($firstMatch.ResultType -eq 'ParameterValue') {
        Write-Host "  ✓ These look like git subcommand completions (ResultType: ParameterValue)" -ForegroundColor Green
        Write-Host "  PSCue completer IS working!" -ForegroundColor Green
    } else {
        Write-Host "  ? Unknown completion type: $($firstMatch.ResultType)" -ForegroundColor Yellow
    }
}

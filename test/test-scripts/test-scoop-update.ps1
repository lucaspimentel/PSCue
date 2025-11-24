Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1

# Set environment variable so pscue-debug can find this session
$env:PSCUE_PID = $PID

# Wait for IPC server to start
Start-Sleep -Seconds 1

# Test scoop update completion
Write-Host "Testing: scoop update <tab>" -ForegroundColor Cyan
$result = TabExpansion2 'scoop update ' 13
if ($result.CompletionMatches) {
    Write-Host "Found $($result.CompletionMatches.Count) completions:" -ForegroundColor Green
    $result.CompletionMatches | Select-Object -First 10 CompletionText, ToolTip | Format-Table -AutoSize
} else {
    Write-Host "No completions found!" -ForegroundColor Red
}

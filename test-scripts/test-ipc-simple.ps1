Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1
Start-Sleep -Milliseconds 500

Write-Host "Testing IPC connection..." -ForegroundColor Cyan
$pipeName = "PSCue-$PID"
Write-Host "Pipe name: $pipeName"

try {
    $pc = New-Object System.IO.Pipes.NamedPipeClientStream(".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    $pc.Connect(100)

    if ($pc.IsConnected) {
        Write-Host "✓ IPC Server is running and connected!" -ForegroundColor Green
        $pc.Close()
    } else {
        Write-Host "✗ Could not connect" -ForegroundColor Red
    }
    $pc.Dispose()
} catch {
    Write-Host "✗ IPC Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nTesting Tab completion..." -ForegroundColor Cyan
$result = TabExpansion2 'git che' 7
if ($result) {
    Write-Host "✓ Got $($result.CompletionMatches.Count) completions" -ForegroundColor Green
}

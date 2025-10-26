# Check if ArgumentCompleter is using IPC or local fallback

Write-Host "Testing IPC vs Local path..." -ForegroundColor Cyan
Write-Host ""

# Load module
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force
$env:PSCUE_PID = $PID
$env:PSCUE_DEBUG = "1"  # Enable debug logging in completer

# Check IPC connectivity from debug tool
Write-Host "1. Testing IPC connectivity with debug tool..." -ForegroundColor Cyan
& dotnet run --project src/PSCue.Debug/ -- ping

# Call the completer exe directly to see debug output
Write-Host ""
Write-Host "2. Calling pscue-completer.exe directly..." -ForegroundColor Cyan
$completerExe = Join-Path ~/.local/pwsh-modules/PSCue "pscue-completer.exe"
$output = & $completerExe "st" "git st" 6 2>&1
Write-Host "   Output:" -ForegroundColor Gray
foreach ($line in $output) {
    Write-Host "   $line" -ForegroundColor Gray
}

# Check the debug log
Write-Host ""
Write-Host "3. Checking completer debug log..." -ForegroundColor Cyan
$debugLogPath = [System.IO.Path]::Combine($env:TEMP, "pscue-completer-debug.log")
if (Test-Path $debugLogPath) {
    Write-Host "   Found log at: $debugLogPath" -ForegroundColor Gray
    $logContent = Get-Content $debugLogPath -Tail 20
    foreach ($line in $logContent) {
        if ($line -match "IPC") {
            Write-Host "   $line" -ForegroundColor Yellow
        } else {
            Write-Host "   $line" -ForegroundColor DarkGray
        }
    }
} else {
    Write-Host "   No debug log found - PSCUE_DEBUG may not be enabled" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Test complete!" -ForegroundColor Green

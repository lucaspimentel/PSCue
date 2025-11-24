$env:PSCUE_DEBUG = "1"

Write-Host "Testing: scoop update " -ForegroundColor Cyan
Push-Location "$PSScriptRoot\.."
dotnet run --project src/PSCue.Debug/ -- query-local "scoop update "
Pop-Location

Write-Host "`n`nLog output:" -ForegroundColor Cyan
$logPath = "$env:LOCALAPPDATA\PSCue\log.txt"
if (Test-Path $logPath) {
    Get-Content $logPath -Tail 100
} else {
    Write-Host "No log file found at: $logPath" -ForegroundColor Yellow
}

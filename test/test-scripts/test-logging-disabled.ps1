# Test that logging is disabled when PSCUE_DEBUG is not set
# This script verifies that no log file is created without PSCUE_DEBUG=1

Write-Host "Testing that logging is disabled by default..." -ForegroundColor Cyan

# Ensure PSCUE_DEBUG is not set
$env:PSCUE_DEBUG = ""

# Get the log path
$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$logPath = Join-Path $localAppData "PSCue\log.txt"

# Remove existing log file if present
if (Test-Path $logPath) {
    Write-Host "Removing existing log file: $logPath" -ForegroundColor Gray
    Remove-Item $logPath -Force
}

# Load the module
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force

# Trigger a completion (this would normally write to log in debug mode)
Write-Host "`nTriggering completion..." -ForegroundColor Yellow
$result = TabExpansion2 'git checkout ma' 17

# Check if log file was created
if (Test-Path $logPath) {
    Write-Host "`n❌ FAILED: Log file was created even though PSCUE_DEBUG was not set!" -ForegroundColor Red
    Write-Host "   Log file: $logPath" -ForegroundColor Red
    exit 1
} else {
    Write-Host "`n✅ PASSED: No log file created (filesystem I/O disabled)" -ForegroundColor Green
}

# Now test with debug enabled
Write-Host "`nTesting with PSCUE_DEBUG=1..." -ForegroundColor Cyan
$env:PSCUE_DEBUG = "1"

# Remove module and reload with debug enabled
Remove-Module PSCue -Force -ErrorAction SilentlyContinue
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force

# Trigger another completion
$result = TabExpansion2 'git checkout ma' 17

# Wait a moment for async writes
Start-Sleep -Milliseconds 100

# Check if log file exists now
if (Test-Path $logPath) {
    Write-Host "✅ PASSED: Log file created with PSCUE_DEBUG=1" -ForegroundColor Green
    Write-Host "   Log file: $logPath" -ForegroundColor Gray

    # Show last few lines
    Write-Host "`nLast 5 log entries:" -ForegroundColor Yellow
    Get-Content $logPath -Tail 5 | ForEach-Object { Write-Host "   $_" -ForegroundColor Gray }
} else {
    Write-Host "❌ FAILED: Log file not created even with PSCUE_DEBUG=1" -ForegroundColor Red
    exit 1
}

Write-Host "`n✅ All tests passed!" -ForegroundColor Green

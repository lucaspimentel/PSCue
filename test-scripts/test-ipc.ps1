#!/usr/bin/env pwsh
# Test IPC communication between ArgumentCompleter and CommandPredictor

$ErrorActionPreference = "Stop"

Write-Host "Testing PSCue IPC Communication" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Import the module
Write-Host "1. Importing PSCue module..." -ForegroundColor Yellow
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force -Verbose
Write-Host "   ✓ Module imported" -ForegroundColor Green
Write-Host ""

# Wait a moment for IPC server to start
Start-Sleep -Milliseconds 500

# Check if CommandPredictor is registered
Write-Host "2. Checking CommandPredictor registration..." -ForegroundColor Yellow
$predictors = Get-PSSubsystem -Kind CommandPredictor
$pscuePredictor = $predictors | Where-Object { $_.Name -like "*PSCue*" -or $_.Name -like "*CommandCompleter*" }

if ($pscuePredictor) {
    Write-Host "   ✓ CommandPredictor registered: $($pscuePredictor.Name)" -ForegroundColor Green
} else {
    Write-Host "   ✗ CommandPredictor not found!" -ForegroundColor Red
    Write-Host "   Available predictors:" -ForegroundColor Yellow
    $predictors | ForEach-Object { Write-Host "     - $($_.Name)" -ForegroundColor Gray }
}
Write-Host ""

# Test Tab completion (will try IPC first, fall back to local)
Write-Host "3. Testing Tab completion (ArgumentCompleter)..." -ForegroundColor Yellow
$result = TabExpansion2 'git che' 7
if ($result -and $result.CompletionMatches.Count -gt 0) {
    Write-Host "   ✓ Got $($result.CompletionMatches.Count) completions" -ForegroundColor Green
    $result.CompletionMatches | Select-Object -First 5 | ForEach-Object {
        Write-Host "     - $($_.CompletionText): $($_.ToolTip)" -ForegroundColor Gray
    }
} else {
    Write-Host "   ✗ No completions returned" -ForegroundColor Red
}
Write-Host ""

# Test inline predictions (uses CommandPredictor)
Write-Host "4. Testing inline predictions (CommandPredictor)..." -ForegroundColor Yellow
Set-PSReadLineOption -PredictionSource HistoryAndPlugin
Write-Host "   ✓ PredictionSource set to HistoryAndPlugin" -ForegroundColor Green
Write-Host "   Note: Try typing 'git checkout' in your terminal to see inline suggestions" -ForegroundColor Gray
Write-Host ""

# Check if IPC server is running by testing direct connection
Write-Host "5. Testing Named Pipe connection..." -ForegroundColor Yellow
$pipeName = "PSCue-$PID"
Write-Host "   Pipe name: $pipeName" -ForegroundColor Gray

try {
    $pipeClient = New-Object System.IO.Pipes.NamedPipeClientStream(".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    $pipeClient.Connect(100)

    if ($pipeClient.IsConnected) {
        Write-Host "   ✓ IPC server is running and accepting connections!" -ForegroundColor Green
        $pipeClient.Close()
    } else {
        Write-Host "   ✗ Could not connect to IPC server" -ForegroundColor Red
    }

    $pipeClient.Dispose()
} catch {
    Write-Host "   ✗ IPC server not available: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Note: ArgumentCompleter will fall back to local logic" -ForegroundColor Yellow
}
Write-Host ""

# Test with PSCUE_DEBUG=1 to see which path is used
Write-Host "6. Testing with PSCUE_DEBUG=1 to trace IPC usage..." -ForegroundColor Yellow
$env:PSCUE_DEBUG = "1"
$debugResult = & ~/.local/pwsh-modules/PSCue/pscue-completer.exe "che" "git che" 7 2>&1
$env:PSCUE_DEBUG = ""

if ($debugResult) {
    Write-Host "   Debug output:" -ForegroundColor Gray
    $debugResult | ForEach-Object { Write-Host "     $_" -ForegroundColor Gray }
}
Write-Host ""

Write-Host "================================" -ForegroundColor Cyan
Write-Host "IPC Test Complete!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  - CommandPredictor: $(if ($pscuePredictor) { 'Registered ✓' } else { 'Not found ✗' })" -ForegroundColor $(if ($pscuePredictor) { 'Green' } else { 'Red' })
Write-Host "  - Tab Completion: $(if ($result) { 'Working ✓' } else { 'Failed ✗' })" -ForegroundColor $(if ($result) { 'Green' } else { 'Red' })
Write-Host ""

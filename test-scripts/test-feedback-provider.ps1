#!/usr/bin/env pwsh
# Test FeedbackProvider functionality (PowerShell 7.4+ only)

$ErrorActionPreference = "Stop"

Write-Host "Testing PSCue FeedbackProvider" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan
Write-Host ""

# Check PowerShell version
$psVersion = $PSVersionTable.PSVersion
Write-Host "PowerShell Version: $psVersion" -ForegroundColor Yellow

if ($psVersion.Major -lt 7 -or ($psVersion.Major -eq 7 -and $psVersion.Minor -lt 4)) {
    Write-Host "❌ FeedbackProvider requires PowerShell 7.4 or higher" -ForegroundColor Red
    Write-Host "   Current version: $psVersion" -ForegroundColor Red
    Write-Host "   Please upgrade to PowerShell 7.4+ to use feedback providers" -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ PowerShell version is 7.4+" -ForegroundColor Green
Write-Host ""

# Check if PSFeedbackProvider experimental feature is enabled
Write-Host "Checking experimental features..." -ForegroundColor Yellow
$feedbackFeature = Get-ExperimentalFeature -Name PSFeedbackProvider -ErrorAction SilentlyContinue

if ($null -eq $feedbackFeature) {
    Write-Host "❌ PSFeedbackProvider experimental feature not found" -ForegroundColor Red
    Write-Host "   This may indicate PowerShell version < 7.4" -ForegroundColor Yellow
    exit 1
}

if ($feedbackFeature.Enabled) {
    Write-Host "✓ PSFeedbackProvider experimental feature is enabled" -ForegroundColor Green
} else {
    Write-Host "❌ PSFeedbackProvider experimental feature is NOT enabled" -ForegroundColor Red
    Write-Host "" -ForegroundColor Yellow
    Write-Host "To enable the feature, run:" -ForegroundColor Yellow
    Write-Host "  Enable-ExperimentalFeature -Name PSFeedbackProvider" -ForegroundColor Cyan
    Write-Host "  Then restart PowerShell" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host ""

# Import PSCue module
Write-Host "Importing PSCue module..." -ForegroundColor Yellow
try {
    Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force -ErrorAction Stop
    Write-Host "✓ Module imported successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to import PSCue module: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Check if FeedbackProvider is registered
Write-Host "Checking FeedbackProvider registration..." -ForegroundColor Yellow
try {
    $feedbackProviders = Get-PSSubsystem -Kind FeedbackProvider -ErrorAction Stop
    $pscueFeedback = $feedbackProviders | Where-Object { $_.Name -like "*PSCue*" -or $_.Name -like "*CommandCompleter*" }

    if ($pscueFeedback) {
        Write-Host "✓ PSCue FeedbackProvider is registered!" -ForegroundColor Green
        Write-Host "  Name: $($pscueFeedback.Name)" -ForegroundColor Gray
        Write-Host "  Description: $($pscueFeedback.Description)" -ForegroundColor Gray
        Write-Host "  ID: $($pscueFeedback.Id)" -ForegroundColor Gray
    } else {
        Write-Host "❌ PSCue FeedbackProvider not found" -ForegroundColor Red
        Write-Host "  Available feedback providers:" -ForegroundColor Yellow
        $feedbackProviders | ForEach-Object {
            Write-Host "    - $($_.Name)" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "❌ Error checking feedback providers: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test learning system
Write-Host "Testing learning system..." -ForegroundColor Yellow
Write-Host "  Note: Learning happens silently after command execution" -ForegroundColor Gray
Write-Host "  The FeedbackProvider will observe your commands and update" -ForegroundColor Gray
Write-Host "  completion scores in the background." -ForegroundColor Gray
Write-Host ""

Write-Host "To test learning manually:" -ForegroundColor Yellow
Write-Host "  1. Execute some git commands (e.g., 'git checkout main')" -ForegroundColor Cyan
Write-Host "  2. The FeedbackProvider will observe and learn from your usage" -ForegroundColor Cyan
Write-Host "  3. Frequently-used completions will get higher priority scores" -ForegroundColor Cyan
Write-Host "  4. You'll see those completions suggested first in future" -ForegroundColor Cyan
Write-Host ""

Write-Host "===============================" -ForegroundColor Cyan
Write-Host "FeedbackProvider Test Complete!" -ForegroundColor Cyan
Write-Host ""

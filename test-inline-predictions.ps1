# Test inline predictions
$ErrorActionPreference = 'Stop'

Write-Host "Loading PSCue module..." -ForegroundColor Cyan
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force -Verbose

Write-Host "`nChecking registered predictors..." -ForegroundColor Cyan
$predictors = Get-PSSubsystem -Kind CommandPredictor | Select-Object -ExpandProperty Implementations
if ($predictors) {
    Write-Host "Predictors found:" -ForegroundColor Green
    $predictors | Format-Table Id, Name, Description -AutoSize
} else {
    Write-Host "ERROR: No predictors registered!" -ForegroundColor Red
    exit 1
}

Write-Host "`nChecking PSReadLine PredictionSource..." -ForegroundColor Cyan
$predictionSource = (Get-PSReadLineOption).PredictionSource
Write-Host "Current PredictionSource: $predictionSource" -ForegroundColor Yellow
Write-Host "(Skipping Set-PSReadLineOption in script mode)" -ForegroundColor Gray

Write-Host "`nManually testing predictor GetSuggestion..." -ForegroundColor Cyan

# Create a new instance of the predictor (simpler than getting from subsystem)
$predictor = New-Object PSCue.CommandPredictor.CommandCompleterPredictor

Write-Host "Predictor instance created: $($predictor.GetType().FullName)" -ForegroundColor Green
Write-Host "  Id: $($predictor.Id)" -ForegroundColor Gray
Write-Host "  Name: $($predictor.Name)" -ForegroundColor Gray

# Test with a simple command
$testInput = "git checkout ma"
Write-Host "`nTest input: '$testInput'" -ForegroundColor Cyan

$context = [System.Management.Automation.Language.Parser]::ParseInput($testInput, [ref]$null, [ref]$null)
$predictionContext = [System.Management.Automation.Subsystem.Prediction.PredictionContext]::Create($testInput)
$predictionClient = [System.Management.Automation.Subsystem.Prediction.PredictionClient]::new("TestClient", [System.Management.Automation.Subsystem.Prediction.PredictionClientKind]::Terminal)

$suggestions = $predictor.GetSuggestion($predictionClient, $predictionContext, [System.Threading.CancellationToken]::None)

if ($suggestions -and $suggestions.SuggestionEntries -and $suggestions.SuggestionEntries.Count -gt 0) {
    Write-Host "SUCCESS! Got $($suggestions.SuggestionEntries.Count) suggestions:" -ForegroundColor Green
    foreach ($s in $suggestions.SuggestionEntries) {
        Write-Host "  - $($s.SuggestionText)" -ForegroundColor Yellow
    }
} else {
    Write-Host "WARNING: No suggestions returned" -ForegroundColor Red
    Write-Host "This could mean:" -ForegroundColor Yellow
    Write-Host "  1. The predictor logic isn't finding matches" -ForegroundColor Yellow
    Write-Host "  2. There's an error in the GetSuggestion method" -ForegroundColor Yellow
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
Write-Host "To test interactively, start a new PowerShell session and type: git checkout ma<wait>" -ForegroundColor Cyan
Write-Host "(You should see an inline suggestion appear in gray text)" -ForegroundColor Cyan

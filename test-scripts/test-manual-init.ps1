# Manually test the Init.OnImport() method
$ErrorActionPreference = 'Stop'

Write-Host "Loading assembly..." -ForegroundColor Cyan
$assembly = [System.Reflection.Assembly]::LoadFrom("$HOME/.local/pwsh-modules/PSCue/PSCue.CommandPredictor.dll")

Write-Host "Creating Init instance..." -ForegroundColor Cyan
$initType = $assembly.GetType('PSCue.CommandPredictor.Init')
$init = [Activator]::CreateInstance($initType)

Write-Host "Calling OnImport()..." -ForegroundColor Cyan
try {
    $init.OnImport()
    Write-Host "OnImport() completed without error" -ForegroundColor Green
} catch {
    Write-Host "OnImport() threw an exception:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.Exception.StackTrace -ForegroundColor Yellow
}

Write-Host "`nChecking registered predictors..." -ForegroundColor Cyan
$predictors = Get-PSSubsystem -Kind CommandPredictor
Write-Host "Predictor count: $($predictors.Count)" -ForegroundColor Cyan
if ($predictors -and $predictors.Count -gt 0) {
    Write-Host "SUCCESS! Predictors registered:" -ForegroundColor Green
    foreach ($p in $predictors) {
        Write-Host "  Type: $($p.GetType().FullName)" -ForegroundColor Yellow
        Write-Host "  Id: $($p.Id)" -ForegroundColor Yellow
        Write-Host "  Name: $($p.Name)" -ForegroundColor Yellow
        Write-Host "  Description: $($p.Description)" -ForegroundColor Yellow
        Write-Host ""
    }
    $predictors | Format-List *
} else {
    Write-Host "FAILED! No predictors registered!" -ForegroundColor Red
}

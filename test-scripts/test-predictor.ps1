# Test if the CommandPredictor is properly registered
$ErrorActionPreference = 'Stop'

Write-Host "Loading assembly..." -ForegroundColor Cyan
$assembly = [System.Reflection.Assembly]::LoadFrom("$HOME/.local/pwsh-modules/PSCue/PSCue.CommandPredictor.dll")

Write-Host "Finding IModuleAssemblyInitializer..." -ForegroundColor Cyan
$initTypes = $assembly.GetTypes() | Where-Object {
    $_.GetInterfaces().Name -contains 'IModuleAssemblyInitializer'
}

if ($initTypes) {
    Write-Host "Found IModuleAssemblyInitializer types:" -ForegroundColor Green
    $initTypes | Format-Table FullName, IsPublic
} else {
    Write-Host "No IModuleAssemblyInitializer types found!" -ForegroundColor Red
}

Write-Host "`nAll types in assembly:" -ForegroundColor Cyan
$assembly.GetTypes() | Format-Table FullName, IsPublic

Write-Host "`nTrying to import module..." -ForegroundColor Cyan
Import-Module "$HOME/.local/pwsh-modules/PSCue/PSCue.psd1" -Force -Verbose

Write-Host "`nChecking registered predictors..." -ForegroundColor Cyan
$predictors = Get-PSSubsystem -Kind CommandPredictor
if ($predictors) {
    $predictors | Format-Table Id, Name, Description -AutoSize
} else {
    Write-Host "No predictors registered!" -ForegroundColor Red
}

# Test script to verify ArgumentCompleter registration
# This checks if our completer is actually being called

Write-Host "Testing ArgumentCompleter Registration" -ForegroundColor Cyan
Write-Host ""

# 1. Load PSCue module
Write-Host "1. Loading PSCue module..." -ForegroundColor Cyan
Import-Module ~/.local/pwsh-modules/PSCue/PSCue.psd1 -Force
$env:PSCUE_PID = $PID
$env:PSCUE_DEBUG = "1"

# 2. Check registered argument completers
Write-Host ""
Write-Host "2. Checking registered argument completers for 'git'..." -ForegroundColor Cyan
$gitCompleters = (Get-ArgumentCompleter -CommandName git)
Write-Host "   Found $($gitCompleters.Count) completers for 'git'" -ForegroundColor Gray

foreach ($completer in $gitCompleters) {
    Write-Host "   - Completer: $($completer.CommandName)" -ForegroundColor Gray
    if ($completer.ScriptBlock) {
        $scriptText = $completer.ScriptBlock.ToString()
        $preview = if ($scriptText.Length -gt 100) { $scriptText.Substring(0, 100) + "..." } else { $scriptText }
        Write-Host "     Script: $preview" -ForegroundColor DarkGray
    }
}

# 3. Test TabExpansion2 directly
Write-Host ""
Write-Host "3. Testing TabExpansion2 directly..." -ForegroundColor Cyan
$result = TabExpansion2 'git st' 6
Write-Host "   Input: 'git st'" -ForegroundColor Gray
Write-Host "   Cursor position: 6" -ForegroundColor Gray
Write-Host "   Found $($result.CompletionMatches.Count) completions" -ForegroundColor Gray

foreach ($match in $result.CompletionMatches) {
    Write-Host "   - $($match.CompletionText) ($($match.ResultType))" -ForegroundColor Gray
}

# 4. Call the completer exe directly
Write-Host ""
Write-Host "4. Calling pscue-completer.exe directly..." -ForegroundColor Cyan
$completerExe = Join-Path ~/.local/pwsh-modules/PSCue "pscue-completer.exe"
if (Test-Path $completerExe) {
    $output = & $completerExe "st" "git st" 6
    Write-Host "   Input: 'st' 'git st' 6" -ForegroundColor Gray
    Write-Host "   Output:" -ForegroundColor Gray
    foreach ($line in $output) {
        Write-Host "   - $line" -ForegroundColor Gray
    }
} else {
    Write-Host "   Completer executable not found at: $completerExe" -ForegroundColor Red
}

# 5. Check if posh-git is loaded (might conflict)
Write-Host ""
Write-Host "5. Checking for conflicting modules..." -ForegroundColor Cyan
$poshGit = Get-Module posh-git
if ($poshGit) {
    Write-Host "   ⚠️  posh-git is loaded (might override completions)" -ForegroundColor Yellow
} else {
    Write-Host "   ✓ No conflicting modules detected" -ForegroundColor Green
}

Write-Host ""
Write-Host "Test complete!" -ForegroundColor Green

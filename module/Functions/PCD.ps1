# PCD - PowerShell Change Directory with PSCue smart suggestions
# Leverages PSCue's learned directory data for intelligent navigation

function Invoke-PCD {
    <#
    .SYNOPSIS
    Change directory with enhanced smart suggestions from PSCue's learned data.

    .DESCRIPTION
    PowerShell Change Directory (pcd) provides intelligent directory navigation
    with fuzzy matching, frecency scoring (frequency + recency), and optional
    recursive filesystem search.

    Features:
    - Well-known shortcuts: ~, .., .
    - Fuzzy matching: Finds directories even with typos
    - Frecency scoring: Balances frequency and recency
    - Distance scoring: Prefers nearby directories
    - Best-match navigation: Automatically finds closest match if exact path doesn't exist
    - Optional recursive search: Finds directories by name in subdirectories

    .PARAMETER Path
    The directory path to change to. Supports tab completion with learned suggestions.
    If the path doesn't exist exactly, pcd will find the best fuzzy match.
    If not specified, changes to the user's home directory.

    .EXAMPLE
    pcd D:\source\datadog
    Changes to the specified directory.

    .EXAMPLE
    pcd datadog
    If "datadog" doesn't exist in current directory, finds best match like "D:\source\datadog".

    .EXAMPLE
    pcd
    Changes to the user's home directory.

    .EXAMPLE
    pcd dat<TAB>
    Tab completion shows learned directories matching "dat" with fuzzy matching.

    .NOTES
    This function uses PSCue's PcdCompletionEngine with enhanced scoring.
    If the module is not fully initialized, falls back to native Set-Location behavior.

    Configuration via environment variables:
    - $env:PSCUE_PCD_FREQUENCY_WEIGHT (default: 0.5)
    - $env:PSCUE_PCD_RECENCY_WEIGHT (default: 0.3)
    - $env:PSCUE_PCD_DISTANCE_WEIGHT (default: 0.2)
    - $env:PSCUE_PCD_RECURSIVE_SEARCH (default: false)
    - $env:PSCUE_PCD_MAX_DEPTH (default: 3)
    #>
    [CmdletBinding()]
    [Alias('pcd')]
    param(
        [Parameter(Position = 0)]
        [string]$Path
    )

    # If no path specified, go to home directory (like native cd)
    if ([string]::IsNullOrWhiteSpace($Path)) {
        Set-Location ~
        return
    }

    # Try Set-Location first - if it works, we're done
    if (Test-Path -LiteralPath $Path -PathType Container) {
        Set-Location -LiteralPath $Path
        return
    }

    # Path doesn't exist - try to find best match using PcdCompletionEngine
    if ($null -eq [PSCue.Module.PSCueModule]::KnowledgeGraph) {
        # Not initialized - fall back to Set-Location error
        Set-Location $Path
        return
    }

    try {
        # Get current directory
        $currentDir = (Get-Location).Path

        # Read configuration from environment variables
        $frequencyWeight = if ($env:PSCUE_PCD_FREQUENCY_WEIGHT) { [double]$env:PSCUE_PCD_FREQUENCY_WEIGHT } else { 0.5 }
        $recencyWeight = if ($env:PSCUE_PCD_RECENCY_WEIGHT) { [double]$env:PSCUE_PCD_RECENCY_WEIGHT } else { 0.3 }
        $distanceWeight = if ($env:PSCUE_PCD_DISTANCE_WEIGHT) { [double]$env:PSCUE_PCD_DISTANCE_WEIGHT } else { 0.2 }
        $maxDepth = if ($env:PSCUE_PCD_MAX_DEPTH) { [int]$env:PSCUE_PCD_MAX_DEPTH } else { 3 }
        $enableRecursive = if ($env:PSCUE_PCD_RECURSIVE_SEARCH) { $env:PSCUE_PCD_RECURSIVE_SEARCH -eq 'true' } else { $false }

        # Create PcdCompletionEngine with configuration
        $engine = [PSCue.Module.PcdCompletionEngine]::new(
            [PSCue.Module.PSCueModule]::KnowledgeGraph,
            30,  # scoreDecayDays
            $frequencyWeight,
            $recencyWeight,
            $distanceWeight,
            $maxDepth,
            $enableRecursive
        )

        # Get best match - request more suggestions for better fuzzy matching
        $suggestions = $engine.GetSuggestions($Path, $currentDir, 10)

        if ($suggestions -and $suggestions.Count -gt 0) {
            $bestMatch = $suggestions[0]

            # Use the absolute path (DisplayPath) for navigation
            $absolutePath = $bestMatch.DisplayPath

            # Verify the matched path exists
            if (Test-Path -LiteralPath $absolutePath -PathType Container) {
                Write-Host "No exact match, navigating to: $absolutePath" -ForegroundColor Yellow
                Set-Location -LiteralPath $absolutePath
                return
            }
        }

        # No matches found - fall back to Set-Location error
        Set-Location $Path
    }
    catch {
        # If anything goes wrong, fall back to Set-Location
        Write-Debug "PSCue pcd best-match error: $_"
        Set-Location $Path
    }
}

# Register tab completion for Invoke-PCD and pcd alias
Register-ArgumentCompleter -CommandName 'Invoke-PCD', 'pcd' -ParameterName 'Path' -ScriptBlock {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameters)

    # Check if PSCue module is initialized
    if ($null -eq [PSCue.Module.PSCueModule]::KnowledgeGraph) {
        # Not initialized - fall back to native file system completion
        # Return nothing, let PowerShell's default completion handle it
        return
    }

    try {
        # Get current directory
        $currentDir = (Get-Location).Path

        # Read configuration from environment variables
        $frequencyWeight = if ($env:PSCUE_PCD_FREQUENCY_WEIGHT) { [double]$env:PSCUE_PCD_FREQUENCY_WEIGHT } else { 0.5 }
        $recencyWeight = if ($env:PSCUE_PCD_RECENCY_WEIGHT) { [double]$env:PSCUE_PCD_RECENCY_WEIGHT } else { 0.3 }
        $distanceWeight = if ($env:PSCUE_PCD_DISTANCE_WEIGHT) { [double]$env:PSCUE_PCD_DISTANCE_WEIGHT } else { 0.2 }
        $maxDepth = if ($env:PSCUE_PCD_MAX_DEPTH) { [int]$env:PSCUE_PCD_MAX_DEPTH } else { 3 }
        $enableRecursive = if ($env:PSCUE_PCD_RECURSIVE_SEARCH) { $env:PSCUE_PCD_RECURSIVE_SEARCH -eq 'true' } else { $false }

        # Create PcdCompletionEngine with configuration
        $engine = [PSCue.Module.PcdCompletionEngine]::new(
            [PSCue.Module.PSCueModule]::KnowledgeGraph,
            30,  # scoreDecayDays
            $frequencyWeight,
            $recencyWeight,
            $distanceWeight,
            $maxDepth,
            $enableRecursive
        )

        # Get suggestions using enhanced algorithm
        $suggestions = $engine.GetSuggestions($wordToComplete, $currentDir, 20)

        # Convert PcdSuggestion objects to CompletionResult objects
        # Filter out paths that don't exist on the filesystem
        $suggestions | Where-Object { Test-Path -LiteralPath $_.DisplayPath -PathType Container } | ForEach-Object {
            $relativePath = $_.Path          # Relative path (e.g., "..", "./src", "../sibling")
            $fullPath = $_.DisplayPath       # Full absolute path
            $matchType = $_.MatchType
            $tooltip = $_.Tooltip

            # Add match type indicator to tooltip
            $matchIndicator = switch ($matchType) {
                'WellKnown' { '[shortcut]' }
                'Exact' { '[exact]' }
                'Prefix' { '[prefix]' }
                'Fuzzy' { '[fuzzy]' }
                'Filesystem' { '[found]' }
                default { '[learned]' }
            }
            $fullTooltip = "$matchIndicator $tooltip"

            # Create CompletionResult with proper quoting if path contains spaces
            # Use absolute path for completion (what gets inserted) so navigation always works
            # But show relative path in the list for better UX
            # The fullPath already has trailing \ from PcdCompletionEngine normalization
            $completionText = if ($fullPath -match '\s') {
                # Quote paths with spaces
                "`"$fullPath`""
            } else {
                $fullPath
            }

            [System.Management.Automation.CompletionResult]::new(
                $completionText,    # completionText (what gets inserted - absolute path)
                $relativePath,      # listItemText (what's shown in list - relative path)
                'ParameterValue',   # resultType
                $fullTooltip        # toolTip (shows full path)
            )
        }
    }
    catch {
        # If anything goes wrong, fail silently and let native completion take over
        Write-Debug "PSCue pcd completion error: $_"
        return
    }
}

# Export the function (module manifest will handle this via FunctionsToExport)
Export-ModuleMember -Function Invoke-PCD -Alias pcd

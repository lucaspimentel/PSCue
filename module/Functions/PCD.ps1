# PCD - PowerShell Change Directory with PSCue smart suggestions
# Leverages PSCue's learned directory data for intelligent navigation

# Tracks the previous directory for 'pcd -' navigation (like cd -)
$script:PreviousLocation = $null

function Invoke-PCD {
    <#
    .SYNOPSIS
    Change directory with enhanced smart suggestions from PSCue's learned data.

    .DESCRIPTION
    PowerShell Change Directory (pcd) provides intelligent directory navigation
    with fuzzy matching, frecency scoring (frequency + recency), optional
    recursive filesystem search, and interactive selection mode.

    Features:
    - Well-known shortcuts: ~, .., .
    - Fuzzy matching: Finds directories even with typos
    - Frecency scoring: Balances frequency and recency
    - Distance scoring: Prefers nearby directories
    - Best-match navigation: Automatically finds closest match if exact path doesn't exist
    - Optional recursive search: Finds directories by name in subdirectories
    - Interactive selection: Browse and select from frequently visited directories

    .PARAMETER Path
    The directory path to change to. Supports tab completion with learned suggestions.
    If the path doesn't exist exactly, pcd will find the best fuzzy match.
    If not specified and -Interactive is not used, changes to the user's home directory.

    .PARAMETER Interactive
    Show an interactive selection menu to browse and select from learned directories.
    Alias: -i

    .PARAMETER Top
    Number of directories to show in interactive mode. Default is 20.
    Valid range: 5-100.

    .PARAMETER Root
    Navigate to the root of the current git repository by walking up from $PWD looking for a .git
    directory or file (supports worktrees). If not inside a git repo, navigates to the filesystem root.
    Alias: -r

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
    pcd -i
    Shows an interactive menu to browse and select from your 20 most frequently visited directories.

    .EXAMPLE
    pcd -i dotnet
    Shows an interactive menu filtered to directories containing "dotnet".

    .EXAMPLE
    pcd -Root
    Navigates to the root of the current git repository, or filesystem root if not in a repo.

    .EXAMPLE
    pcd -r
    Same as pcd -Root (alias).

    .EXAMPLE
    pcd -Interactive -Top 50
    Shows an interactive menu with up to 50 learned directories.

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
    - $env:PSCUE_PCD_RECURSIVE_SEARCH (default: true)
    - $env:PSCUE_PCD_MAX_DEPTH (default: 3)
    #>
    [CmdletBinding()]
    [Alias('pcd')]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [string]$Path,

        [Parameter(Mandatory = $false)]
        [Alias('i')]
        [switch]$Interactive,

        [Parameter(Mandatory = $false)]
        [ValidateRange(5, 100)]
        [int]$Top = 20,

        [Parameter(Mandatory = $false)]
        [Alias('r')]
        [switch]$Root
    )

    # Root mode: navigate to git repository root (or filesystem root if not in a repo)
    if ($Root) {
        $dir = $PWD.Path
        $gitRoot = $null
        while ($dir) {
            if (Test-Path -LiteralPath (Join-Path $dir '.git')) {
                $gitRoot = $dir
                break
            }
            $parent = Split-Path $dir -Parent
            if ($parent -eq $dir -or [string]::IsNullOrEmpty($parent)) { break }
            $dir = $parent
        }

        if (-not $gitRoot) {
            $gitRoot = [System.IO.Path]::GetPathRoot($PWD.Path)
        }

        $oldLocation = $PWD.Path
        Set-Location -LiteralPath $gitRoot
        if ($null -ne [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @($gitRoot), $oldLocation)
        }
        return
    }

    # Interactive mode: show selection prompt
    if ($Interactive) {
        if ($null -eq [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            Write-Error "PSCue module not initialized. Cannot show interactive selection."
            return
        }

        try {
            $selector = [PSCue.Module.PcdInteractiveSelector]::new(
                [PSCue.Module.PSCueModule]::KnowledgeGraph
            )

            $selectedPath = $selector.ShowSelectionPrompt($PWD.Path, $Top, $Path)

            if ($null -ne $selectedPath) {
                $oldLocation = $PWD.Path
                Set-Location -LiteralPath $selectedPath
                # Manually record navigation under 'cd' command so stats are updated
                # FeedbackProvider only sees 'pcd -i', not the internal Set-Location call
                [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @($selectedPath), $oldLocation)
            }
            # User cancelled (Esc) - do nothing
        }
        catch {
            Write-Error "Failed to show interactive selection: $_"
        }

        return
    }

    # Navigate to previous directory (like cd -)
    if ($Path -eq '-') {
        $oldLocation = $PWD.Path
        Set-Location -
        $newLocation = $PWD.Path
        Write-Host $newLocation
        # Record navigation
        if ($null -ne [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @($newLocation), $oldLocation)
        }
        return
    }

    # If no path specified, go to home directory (like native cd)
    if ([string]::IsNullOrWhiteSpace($Path)) {
        $oldLocation = $PWD.Path
        Set-Location ~
        # Record navigation
        if ($null -ne [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @('~'), $oldLocation)
        }
        return
    }

    # Try Set-Location first - if it works, we're done
    if (Test-Path -LiteralPath $Path -PathType Container) {
        $oldLocation = $PWD.Path
        Set-Location -LiteralPath $Path
        # Record navigation
        if ($null -ne [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @($Path), $oldLocation)
        }
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

        # Create PcdCompletionEngine using shared configuration
        $engine = [PSCue.Module.PcdCompletionEngine]::new(
            [PSCue.Module.PSCueModule]::KnowledgeGraph,
            [PSCue.Module.PcdConfiguration]::ScoreDecayDays,
            [PSCue.Module.PcdConfiguration]::FrequencyWeight,
            [PSCue.Module.PcdConfiguration]::RecencyWeight,
            [PSCue.Module.PcdConfiguration]::DistanceWeight,
            [PSCue.Module.PcdConfiguration]::TabCompletionMaxDepth,  # Use tab-specific depth (deeper for thoroughness)
            [PSCue.Module.PcdConfiguration]::EnableRecursiveSearch,
            [PSCue.Module.PcdConfiguration]::ExactMatchBoost,
            [PSCue.Module.PcdConfiguration]::FuzzyMinMatchPercentage
        )

        # Get best match - request more suggestions for better fuzzy matching
        $suggestions = $engine.GetSuggestions($Path, $currentDir, 10)

        if ($suggestions -and $suggestions.Count -gt 0) {
            # Try suggestions in order until we find one that exists
            foreach ($suggestion in $suggestions) {
                $absolutePath = $suggestion.DisplayPath

                # Verify the matched path exists before navigating
                if (Test-Path -LiteralPath $absolutePath -PathType Container) {
                    Write-Host "No exact match, navigating to: $absolutePath" -ForegroundColor Yellow
                    $oldLocation = $currentDir
                    Set-Location -LiteralPath $absolutePath
                    # Record navigation
                    [PSCue.Module.PSCueModule]::KnowledgeGraph.RecordUsage('cd', @($absolutePath), $oldLocation)
                    return
                }
            }

            # All suggestions exist in database but not on filesystem (stale data or race condition)
            Write-Host "Found $($suggestions.Count) potential match(es) for '$Path', but none exist on the filesystem." -ForegroundColor Red
            Write-Host "Suggestions were:" -ForegroundColor Yellow
            foreach ($suggestion in $suggestions) {
                Write-Host "  - $($suggestion.DisplayPath)" -ForegroundColor Gray
            }
            return
        }

        # No matches found in learned data
        Write-Host "No learned directory matches '$Path'." -ForegroundColor Red
        Write-Host "Tip: Navigate to directories to teach PSCue, or use tab completion to see suggestions." -ForegroundColor Yellow
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

        # Create PcdCompletionEngine using shared configuration
        $engine = [PSCue.Module.PcdCompletionEngine]::new(
            [PSCue.Module.PSCueModule]::KnowledgeGraph,
            [PSCue.Module.PcdConfiguration]::ScoreDecayDays,
            [PSCue.Module.PcdConfiguration]::FrequencyWeight,
            [PSCue.Module.PcdConfiguration]::RecencyWeight,
            [PSCue.Module.PcdConfiguration]::DistanceWeight,
            [PSCue.Module.PcdConfiguration]::TabCompletionMaxDepth,  # Use tab-specific depth (deeper for thoroughness)
            [PSCue.Module.PcdConfiguration]::EnableRecursiveSearch,
            [PSCue.Module.PcdConfiguration]::ExactMatchBoost,
            [PSCue.Module.PcdConfiguration]::FuzzyMinMatchPercentage
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

            # Prepare paths for display and completion
            # ListItemText: Clean display without prefixes/separators/quotes (e.g., "Screenshots")
            # CompletionText: What gets inserted, matching native cd behavior (e.g., '.\Screenshots\')

            # For list display: Use directory name only for child/sibling, full path otherwise
            $sep = [System.IO.Path]::DirectorySeparatorChar
            $listItemText = if ([System.IO.Path]::IsPathRooted($relativePath)) {
                # Absolute path - show full path in list (without trailing separator for cleaner display)
                $fullPath.TrimEnd($sep)
            } else {
                # Relative path - show clean name without .\ prefix or trailing separator
                # Remove .\ or ./ prefix if present, but preserve dot-prefixed names like .config
                $cleanPath = $relativePath
                if ($cleanPath.StartsWith(".$sep")) {
                    $cleanPath = $cleanPath.Substring(2)
                }
                $cleanPath.TrimEnd($sep)
            }

            # For completion text: Match native cd behavior exactly
            # - Relative paths: Add .\ prefix and trailing separator (e.g., .\Screenshots\)
            # - Absolute paths: Use as-is with trailing separator
            # - Add single quotes if path contains spaces
            $pathForCompletion = if ([System.IO.Path]::IsPathRooted($relativePath)) {
                # Absolute path - use as-is
                $relativePath
            } else {
                # Relative path - add .\ prefix (PowerShell style on Windows, ./ on Unix)
                if ($relativePath -eq '..') {
                    # Special case: parent directory doesn't need .\ prefix
                    '..'
                } elseif ($relativePath.StartsWith('..' + $sep)) {
                    # Sibling directory (e.g., ..\sibling) - already has proper prefix
                    $relativePath
                } else {
                    # Child directory - add .\ prefix
                    '.' + $sep + $relativePath
                }
            }

            # Ensure trailing separator for directory completion (like native cd)
            if (-not $pathForCompletion.EndsWith($sep)) {
                $pathForCompletion += $sep
            }

            # Add single quotes if path contains spaces (PowerShell style)
            $completionText = if ($pathForCompletion -match '\s') {
                "'$pathForCompletion'"
            } else {
                $pathForCompletion
            }

            [System.Management.Automation.CompletionResult]::new(
                $completionText,    # completionText (what gets inserted - relative path when possible)
                $listItemText,      # listItemText (what's shown in list - context-appropriate)
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

# Note: Function and alias are exported via FunctionsToExport/AliasesToExport in PSCue.psd1 manifest
# Export-ModuleMember is not needed when using dot-sourcing in PSCue.psm1

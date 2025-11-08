# PCD - PowerShell Change Directory with PSCue smart suggestions
# Leverages PSCue's learned directory data for intelligent navigation

function Invoke-PCD {
    <#
    .SYNOPSIS
    Change directory with smart suggestions from PSCue's learned data.

    .DESCRIPTION
    PowerShell Change Directory (pcd) provides intelligent directory navigation
    by leveraging PSCue's learned data from your cd/Set-Location command history.

    Tab completion shows frequently and recently used directories, ranked by
    PSCue's scoring algorithm (frequency + recency decay).

    .PARAMETER Path
    The directory path to change to. Supports tab completion with learned suggestions.
    If not specified, changes to the user's home directory.

    .EXAMPLE
    pcd D:\source\datadog
    Changes to the specified directory.

    .EXAMPLE
    pcd
    Changes to the user's home directory.

    .EXAMPLE
    pcd dat<TAB>
    Tab completion shows learned directories matching "dat".

    .NOTES
    This function uses PSCue's ArgumentGraph to retrieve learned directory data.
    If the module is not fully initialized, falls back to native Set-Location behavior.
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

    # Use Set-Location to change directory
    # This handles path resolution, validation, and error messages
    Set-Location $Path
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
        # Get learned directory suggestions for 'cd' command
        # Note: ArgumentGraph normalizes paths, so we get clean suggestions
        $suggestions = [PSCue.Module.PSCueModule]::KnowledgeGraph.GetSuggestions('cd', @())

        # Filter suggestions based on partial input
        if (-not [string]::IsNullOrWhiteSpace($wordToComplete)) {
            # Support both substring and StartsWith matching for flexibility
            # StartsWith is faster and more predictable for path completion
            $suggestions = $suggestions | Where-Object {
                $_.Argument -like "$wordToComplete*"
            }
        }

        # Convert to CompletionResult objects
        # Note: GetSuggestions already returns results ordered by score (frequency + recency)
        $suggestions | ForEach-Object {
            $argument = $_.Argument
            $usageCount = $_.UsageCount
            $lastUsed = $_.LastUsed
            $tooltip = "Used $usageCount times (last: $($lastUsed.ToString('yyyy-MM-dd')))"

            # Create CompletionResult with proper quoting if path contains spaces
            $completionText = if ($argument -match '\s') {
                # Quote paths with spaces
                "`"$argument`""
            } else {
                $argument
            }

            [System.Management.Automation.CompletionResult]::new(
                $completionText,    # completionText (what gets inserted)
                $argument,          # listItemText (what's shown in list)
                'ParameterValue',   # resultType
                $tooltip            # toolTip
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

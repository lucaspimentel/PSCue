# Cache Management Functions for PSCue
# PowerShell functions for cache operations

function Get-PSCueCache {
    <#
    .SYNOPSIS
        Gets cached completion entries from PSCue.

    .DESCRIPTION
        Retrieves completion entries from the PSCue cache, showing which completions
        have been cached, how often they've been used, and how old they are.

    .PARAMETER Filter
        Optional filter string to search for specific cache entries.
        Matches against cache keys (command context).

    .PARAMETER AsJson
        Returns output as JSON instead of PowerShell objects.

    .EXAMPLE
        Get-PSCueCache
        Gets all cached completion entries.

    .EXAMPLE
        Get-PSCueCache -Filter "git"
        Gets cache entries related to git commands.

    .EXAMPLE
        Get-PSCueCache | Where-Object HitCount -gt 5
        Gets frequently used cache entries.

    .EXAMPLE
        Get-PSCueCache -AsJson | Out-File cache-backup.json
        Exports cache to JSON file.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Position = 0)]
        [string]$Filter,

        [Parameter()]
        [switch]$AsJson
    )

    if ($null -eq [PSCue.Module.PSCueModule]::Cache) {
        Write-Error "PSCue cache is not initialized. Make sure the PSCue module is loaded."
        return
    }

    try {
        $entries = [PSCue.Module.PSCueModule]::Cache.GetCacheEntries($Filter)

        if ($AsJson) {
            # Convert to JSON with proper formatting (handle empty array)
            if ($null -eq $entries -or $entries.Count -eq 0) {
                return "[]"
            }
            $entries | ConvertTo-Json -Depth 10
        } else {
            # Return rich objects for PowerShell pipeline
            $entries | ForEach-Object {
                [PSCustomObject]@{
                    Key = $_.Key
                    CompletionCount = $_.Completions.Count
                    HitCount = $_.HitCount
                    Age = $_.Age
                    LastAccess = $_.LastAccessTime
                    TopCompletions = ($_.Completions | Select-Object -First 5 | ForEach-Object { $_.Text }) -join ', '
                }
            }
        }
    } catch {
        Write-Error "Failed to retrieve cache entries: $_"
    }
}

function Clear-PSCueCache {
    <#
    .SYNOPSIS
        Clears the PSCue completion cache.

    .DESCRIPTION
        Removes all cached completion entries. This does not affect learned data
        (command history or argument knowledge graph).

    .PARAMETER WhatIf
        Shows what would happen if the command runs without actually clearing the cache.

    .PARAMETER Confirm
        Prompts for confirmation before clearing the cache.

    .EXAMPLE
        Clear-PSCueCache
        Clears all cached completions after confirming.

    .EXAMPLE
        Clear-PSCueCache -Confirm:$false
        Clears cache without confirmation.

    .EXAMPLE
        Clear-PSCueCache -WhatIf
        Shows what would be cleared without actually clearing.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
    param()

    if ($null -eq [PSCue.Module.PSCueModule]::Cache) {
        Write-Error "PSCue cache is not initialized. Make sure the PSCue module is loaded."
        return
    }

    try {
        # Get count before clearing
        $stats = [PSCue.Module.PSCueModule]::Cache.GetStatistics()
        $entryCount = $stats.EntryCount

        if ($PSCmdlet.ShouldProcess("$entryCount cache entries", "Clear")) {
            [PSCue.Module.PSCueModule]::Cache.Clear()
            Write-Host "✓ Cleared $entryCount cache entries" -ForegroundColor Green
        }
    } catch {
        Write-Error "Failed to clear cache: $_"
    }
}

function Get-PSCueCacheStats {
    <#
    .SYNOPSIS
        Gets statistics about the PSCue completion cache.

    .DESCRIPTION
        Returns summary statistics about the cache including total entries,
        total hits, and age of the oldest entry.

    .PARAMETER AsJson
        Returns output as JSON instead of PowerShell objects.

    .EXAMPLE
        Get-PSCueCacheStats
        Displays cache statistics.

    .EXAMPLE
        Get-PSCueCacheStats -AsJson
        Returns statistics as JSON.

    .EXAMPLE
        (Get-PSCueCacheStats).TotalHits
        Gets just the total hit count.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$AsJson
    )

    if ($null -eq [PSCue.Module.PSCueModule]::Cache) {
        Write-Error "PSCue cache is not initialized. Make sure the PSCue module is loaded."
        return
    }

    try {
        $stats = [PSCue.Module.PSCueModule]::Cache.GetStatistics()

        $result = [PSCustomObject]@{
            TotalEntries = $stats.EntryCount
            TotalHits = $stats.TotalHits
            OldestEntry = $stats.OldestEntry
            AverageHits = if ($stats.EntryCount -gt 0) {
                [math]::Round($stats.TotalHits / $stats.EntryCount, 2)
            } else {
                0
            }
        }

        if ($AsJson) {
            $result | ConvertTo-Json
        } else {
            $result
        }
    } catch {
        Write-Error "Failed to retrieve cache statistics: $_"
    }
}

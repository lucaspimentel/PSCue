# Learning Management Functions for PSCue
# PowerShell functions for learned data operations

function Get-PSCueLearning {
    <#
    .SYNOPSIS
        Gets learned command knowledge from PSCue.

    .DESCRIPTION
        Retrieves learned data from PSCue's knowledge graph, showing which commands
        have been learned, their arguments, usage frequencies, and recency.

    .PARAMETER Command
        Optional command name to filter results. If specified, shows only learned
        data for that specific command.

    .PARAMETER AsJson
        Returns output as JSON instead of PowerShell objects.

    .EXAMPLE
        Get-PSCueLearning
        Gets all learned commands and their knowledge.

    .EXAMPLE
        Get-PSCueLearning -Command "git"
        Gets learned arguments for git command.

    .EXAMPLE
        Get-PSCueLearning | Where-Object TotalUsage -gt 10
        Gets frequently used commands.

    .EXAMPLE
        Get-PSCueLearning -Command "docker" -AsJson
        Gets docker knowledge as JSON.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Position = 0)]
        [string]$Command,

        [Parameter()]
        [switch]$AsJson
    )

    if ($null -eq [PSCue.Module.PSCueModule]::KnowledgeGraph) {
        Write-Error "PSCue learning system is not initialized. Make sure the PSCue module is loaded and learning is enabled."
        return
    }

    try {
        if ($Command) {
            # Get knowledge for specific command
            $knowledge = [PSCue.Module.PSCueModule]::KnowledgeGraph.GetCommandKnowledge($Command)

            if ($null -eq $knowledge) {
                Write-Warning "No learned data found for command: $Command"
                return
            }

            $result = [PSCustomObject]@{
                Command = $knowledge.Command
                TotalUsage = $knowledge.TotalUsageCount
                FirstSeen = $knowledge.FirstSeen
                LastUsed = $knowledge.LastUsed
                Arguments = $knowledge.Arguments.Values | ForEach-Object {
                    [PSCustomObject]@{
                        Argument = $_.Argument
                        UsageCount = $_.UsageCount
                        FirstSeen = $_.FirstSeen
                        LastUsed = $_.LastUsed
                        IsFlag = $_.IsFlag
                        Score = $_.GetScore($knowledge.TotalUsageCount, 30)
                    }
                } | Sort-Object -Property Score -Descending
            }

            if ($AsJson) {
                $result | ConvertTo-Json -Depth 10
            } else {
                $result
            }
        } else {
            # Get all learned commands - GetAllCommands() returns the ConcurrentDictionary
            # which implements IEnumerable<KeyValuePair<string, CommandKnowledge>>
            $commandsDict = [PSCue.Module.PSCueModule]::KnowledgeGraph.GetAllCommands()

            # Check if empty
            if ($commandsDict.Count -eq 0) {
                if ($AsJson) {
                    return "[]"
                } else {
                    return @()
                }
            }

            # Enumerate the dictionary to get KeyValuePairs
            $results = @($commandsDict.GetEnumerator() | ForEach-Object {
                $knowledge = $_.Value
                [PSCustomObject]@{
                    Command = $knowledge.Command
                    TotalUsage = $knowledge.TotalUsageCount
                    ArgumentCount = $knowledge.Arguments.Count
                    FirstSeen = $knowledge.FirstSeen
                    LastUsed = $knowledge.LastUsed
                    TopArguments = ($knowledge.Arguments.Values |
                        Sort-Object -Property UsageCount -Descending |
                        Select-Object -First 5 |
                        ForEach-Object { $_.Argument }) -join ', '
                }
            } | Sort-Object -Property TotalUsage -Descending)

            if ($AsJson) {
                $results | ConvertTo-Json -Depth 10
            } else {
                $results
            }
        }
    } catch {
        Write-Error "Failed to retrieve learned data: $_"
    }
}

function Clear-PSCueLearning {
    <#
    .SYNOPSIS
        Clears PSCue's learned command knowledge.

    .DESCRIPTION
        Removes all learned data including command history and argument knowledge graph.
        This also deletes the persisted database file.

    .PARAMETER WhatIf
        Shows what would happen if the command runs without actually clearing data.

    .PARAMETER Confirm
        Prompts for confirmation before clearing learned data.

    .EXAMPLE
        Clear-PSCueLearning
        Clears all learned data after confirming.

    .EXAMPLE
        Clear-PSCueLearning -Confirm:$false
        Clears learned data without confirmation.

    .EXAMPLE
        Clear-PSCueLearning -WhatIf
        Shows what would be cleared without actually clearing.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param()

    if ($null -eq [PSCue.Module.PSCueModule]::Persistence) {
        Write-Error "PSCue learning system is not initialized. Make sure the PSCue module is loaded and learning is enabled."
        return
    }

    try {
        # Get counts before clearing
        $commandCount = 0
        if ([PSCue.Module.PSCueModule]::KnowledgeGraph) {
            $commandCount = ([PSCue.Module.PSCueModule]::KnowledgeGraph.GetAllCommands() | Measure-Object).Count
        }

        $historyCount = 0
        if ([PSCue.Module.PSCueModule]::CommandHistory) {
            $historyCount = ([PSCue.Module.PSCueModule]::CommandHistory.GetRecent() | Measure-Object).Count
        }

        $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath

        if ($PSCmdlet.ShouldProcess("$commandCount learned commands, $historyCount history entries, database at $dbPath", "Clear all learned data")) {
            [PSCue.Module.PSCueModule]::Persistence.Clear()

            Write-Host "✓ Cleared learned data:" -ForegroundColor Green
            Write-Host "  - $commandCount learned commands" -ForegroundColor Gray
            Write-Host "  - $historyCount history entries" -ForegroundColor Gray
            Write-Host "  - Database: $dbPath" -ForegroundColor Gray
        }
    } catch {
        Write-Error "Failed to clear learned data: $_"
    }
}

function Export-PSCueLearning {
    <#
    .SYNOPSIS
        Exports PSCue's learned data to a JSON file.

    .DESCRIPTION
        Exports all learned command knowledge and history to a JSON file for backup,
        migration, or inspection purposes.

    .PARAMETER Path
        Path to the JSON file to create. Directory will be created if it doesn't exist.

    .EXAMPLE
        Export-PSCueLearning -Path ~/pscue-backup.json
        Exports learned data to a JSON file in the home directory.

    .EXAMPLE
        Export-PSCueLearning -Path ./learned-data-$(Get-Date -Format 'yyyy-MM-dd').json
        Exports with a dated filename.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Path
    )

    if ($null -eq [PSCue.Module.PSCueModule]::Persistence) {
        Write-Error "PSCue learning system is not initialized. Make sure the PSCue module is loaded and learning is enabled."
        return
    }

    try {
        # Resolve path
        $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)

        [PSCue.Module.PSCueModule]::Persistence.Export(
            $resolvedPath,
            [PSCue.Module.PSCueModule]::KnowledgeGraph,
            [PSCue.Module.PSCueModule]::CommandHistory
        )

        $fileSize = (Get-Item $resolvedPath).Length
        Write-Host "✓ Exported learned data to: $resolvedPath" -ForegroundColor Green
        Write-Host "  File size: $([math]::Round($fileSize / 1KB, 2)) KB" -ForegroundColor Gray
    } catch {
        Write-Error "Failed to export learned data: $_"
    }
}

function Import-PSCueLearning {
    <#
    .SYNOPSIS
        Imports PSCue's learned data from a JSON file.

    .DESCRIPTION
        Imports learned command knowledge and history from a JSON file created by
        Export-PSCueLearning. Can either merge with existing data or replace it.

    .PARAMETER Path
        Path to the JSON file to import.

    .PARAMETER Merge
        If specified, merges imported data with existing learned data (additive).
        If not specified, replaces existing data entirely.

    .EXAMPLE
        Import-PSCueLearning -Path ~/pscue-backup.json
        Imports learned data, replacing existing data.

    .EXAMPLE
        Import-PSCueLearning -Path ~/shared-learning.json -Merge
        Imports and merges with existing learned data.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Path,

        [Parameter()]
        [switch]$Merge
    )

    if ($null -eq [PSCue.Module.PSCueModule]::Persistence) {
        Write-Error "PSCue learning system is not initialized. Make sure the PSCue module is loaded and learning is enabled."
        return
    }

    try {
        # Resolve and validate path
        $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)

        if (-not (Test-Path $resolvedPath)) {
            Write-Error "File not found: $resolvedPath"
            return
        }

        $action = if ($Merge) { "Merge with" } else { "Replace" }
        $fileSize = (Get-Item $resolvedPath).Length

        if ($PSCmdlet.ShouldProcess("learned data from $resolvedPath ($([math]::Round($fileSize / 1KB, 2)) KB)", $action)) {
            [PSCue.Module.PSCueModule]::Persistence.Import(
                $resolvedPath,
                [PSCue.Module.PSCueModule]::KnowledgeGraph,
                [PSCue.Module.PSCueModule]::CommandHistory,
                $Merge
            )

            $commandCount = ([PSCue.Module.PSCueModule]::KnowledgeGraph.GetAllCommands() | Measure-Object).Count
            Write-Host "✓ Imported learned data from: $resolvedPath" -ForegroundColor Green
            Write-Host "  Total learned commands: $commandCount" -ForegroundColor Gray
            Write-Host "  Mode: $(if ($Merge) { 'Merged' } else { 'Replaced' })" -ForegroundColor Gray
        }
    } catch {
        Write-Error "Failed to import learned data: $_"
    }
}

function Save-PSCueLearning {
    <#
    .SYNOPSIS
        Forces an immediate save of learned data to disk.

    .DESCRIPTION
        Triggers an immediate save of learned data to the SQLite database, bypassing
        the automatic 5-minute save timer. Useful before system shutdown or when you
        want to ensure data is persisted immediately.

    .EXAMPLE
        Save-PSCueLearning
        Saves learned data immediately.
    #>
    [CmdletBinding()]
    param()

    if ($null -eq [PSCue.Module.PSCueModule]::Persistence) {
        Write-Error "PSCue learning system is not initialized. Make sure the PSCue module is loaded and learning is enabled."
        return
    }

    try {
        if ([PSCue.Module.PSCueModule]::KnowledgeGraph -and [PSCue.Module.PSCueModule]::CommandHistory) {
            [PSCue.Module.PSCueModule]::Persistence.SaveArgumentGraph([PSCue.Module.PSCueModule]::KnowledgeGraph)
            [PSCue.Module.PSCueModule]::Persistence.SaveCommandHistory([PSCue.Module.PSCueModule]::CommandHistory)

            $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath
            $dbSize = (Get-Item $dbPath).Length

            Write-Host "✓ Saved learned data to: $dbPath" -ForegroundColor Green
            Write-Host "  Database size: $([math]::Round($dbSize / 1KB, 2)) KB" -ForegroundColor Gray
        } else {
            Write-Warning "No learned data to save (learning may be disabled)"
        }
    } catch {
        Write-Error "Failed to save learned data: $_"
    }
}

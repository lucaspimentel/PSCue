# Workflow Management Functions for PSCue
# Functions for managing and querying learned workflow patterns

function Get-PSCueWorkflows {
    <#
    .SYNOPSIS
        Gets learned workflow patterns from PSCue.

    .DESCRIPTION
        Shows command → next command transitions that PSCue has learned from your usage patterns.
        This helps you understand what workflows PSCue has identified and will use for predictions.

    .PARAMETER Command
        Filter workflows starting from a specific command (e.g., "git add").

    .PARAMETER AsJson
        Returns output as JSON instead of PowerShell objects.

    .EXAMPLE
        Get-PSCueWorkflows
        Shows all learned workflows.

    .EXAMPLE
        Get-PSCueWorkflows -Command "git add"
        Shows workflows that start with "git add".

    .EXAMPLE
        Get-PSCueWorkflows -AsJson
        Returns workflows as JSON.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$Command,

        [Parameter()]
        [switch]$AsJson
    )

    if ($null -eq [PSCue.Module.PSCueModule]::WorkflowLearner) {
        Write-Error "PSCue workflow learner is not initialized. Make sure the PSCue module is loaded and workflow learning is enabled."
        return
    }

    try {
        $learner = [PSCue.Module.PSCueModule]::WorkflowLearner
        $allWorkflows = $learner.GetAllWorkflows()

        if ($allWorkflows.Count -eq 0) {
            Write-Host "No workflow patterns learned yet." -ForegroundColor Yellow
            Write-Host "Use commands normally and PSCue will learn your workflows automatically." -ForegroundColor Gray
            return
        }

        # Filter by command if specified
        if ($Command) {
            $filteredWorkflows = @{}
            foreach ($key in $allWorkflows.Keys) {
                if ($key -like "*$Command*") {
                    $filteredWorkflows[$key] = $allWorkflows[$key]
                }
            }
            $allWorkflows = $filteredWorkflows

            if ($allWorkflows.Count -eq 0) {
                Write-Host "No workflows found for command: $Command" -ForegroundColor Yellow
                return
            }
        }

        # Build output
        $results = @()
        foreach ($fromCmd in $allWorkflows.Keys | Sort-Object) {
            $transitions = $allWorkflows[$fromCmd] | Sort-Object -Property Frequency -Descending

            foreach ($transition in $transitions) {
                $avgTimeSec = $transition.AverageTimeDelta.TotalSeconds

                $results += [PSCustomObject]@{
                    FromCommand      = $fromCmd
                    ToCommand        = $transition.NextCommand
                    Frequency        = $transition.Frequency
                    AvgTimeDelta     = "{0:F1}s" -f $avgTimeSec
                    Confidence       = "{0:P0}" -f $transition.GetConfidence(5, 30)
                    FirstSeen        = $transition.FirstSeen.ToString("yyyy-MM-dd")
                    LastSeen         = $transition.LastSeen.ToString("yyyy-MM-dd")
                }
            }
        }

        if ($AsJson) {
            $results | ConvertTo-Json -Depth 10
        }
        else {
            $results | Format-Table -AutoSize
        }
    }
    catch {
        Write-Error "Error retrieving workflows: $_"
    }
}

function Get-PSCueWorkflowStats {
    <#
    .SYNOPSIS
        Gets statistics about learned workflow patterns.

    .DESCRIPTION
        Shows summary statistics about the workflow learning system, including
        total transitions, unique commands, and most common workflows.

    .PARAMETER Detailed
        Shows detailed statistics including top workflows and database size.

    .PARAMETER AsJson
        Returns output as JSON instead of PowerShell objects.

    .EXAMPLE
        Get-PSCueWorkflowStats
        Shows summary statistics.

    .EXAMPLE
        Get-PSCueWorkflowStats -Detailed
        Shows detailed statistics with top workflows.

    .EXAMPLE
        Get-PSCueWorkflowStats -AsJson
        Returns statistics as JSON.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$Detailed,

        [Parameter()]
        [switch]$AsJson
    )

    if ($null -eq [PSCue.Module.PSCueModule]::WorkflowLearner) {
        Write-Error "PSCue workflow learner is not initialized. Make sure the PSCue module is loaded and workflow learning is enabled."
        return
    }

    try {
        $learner = [PSCue.Module.PSCueModule]::WorkflowLearner
        $diagnostics = $learner.GetDiagnostics()

        $stats = [PSCustomObject]@{
            TotalTransitions     = $diagnostics.Item1
            UniqueCommands       = $diagnostics.Item2
            PendingTransitions   = $diagnostics.Item3
        }

        if ($Detailed) {
            # Get all workflows to find most common
            $allWorkflows = $learner.GetAllWorkflows()

            $topWorkflows = @()
            foreach ($fromCmd in $allWorkflows.Keys) {
                foreach ($transition in $allWorkflows[$fromCmd]) {
                    $topWorkflows += [PSCustomObject]@{
                        Workflow    = "$fromCmd → $($transition.NextCommand)"
                        Frequency   = $transition.Frequency
                        AvgTime     = "{0:F1}s" -f $transition.AverageTimeDelta.TotalSeconds
                    }
                }
            }

            $topWorkflows = $topWorkflows | Sort-Object -Property Frequency -Descending | Select-Object -First 10

            # Get database size if persistence is available
            $dbSize = "N/A"
            if ($null -ne [PSCue.Module.PSCueModule]::Persistence) {
                $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath
                if (Test-Path $dbPath) {
                    $dbSizeBytes = (Get-Item $dbPath).Length
                    $dbSize = "{0:N2} KB" -f ($dbSizeBytes / 1KB)
                }
            }

            $stats | Add-Member -NotePropertyName TopWorkflows -NotePropertyValue $topWorkflows
            $stats | Add-Member -NotePropertyName DatabaseSize -NotePropertyValue $dbSize
        }

        if ($AsJson) {
            $stats | ConvertTo-Json -Depth 10
        }
        else {
            if ($Detailed) {
                Write-Host "`nWorkflow Learning Statistics:" -ForegroundColor Cyan
                Write-Host "  Total Transitions: $($stats.TotalTransitions)"
                Write-Host "  Unique Commands: $($stats.UniqueCommands)"
                Write-Host "  Pending (unsaved): $($stats.PendingTransitions)"
                Write-Host "  Database Size: $($stats.DatabaseSize)"

                if ($stats.TopWorkflows.Count -gt 0) {
                    Write-Host "`nTop Workflows:" -ForegroundColor Cyan
                    $stats.TopWorkflows | Format-Table -AutoSize
                }
            }
            else {
                $stats | Format-List
            }
        }
    }
    catch {
        Write-Error "Error retrieving workflow statistics: $_"
    }
}

function Clear-PSCueWorkflows {
    <#
    .SYNOPSIS
        Clears learned workflow patterns.

    .DESCRIPTION
        Clears all workflow patterns from memory and optionally from the database.
        Use with caution - this will delete all learned workflow data.

    .PARAMETER WhatIf
        Shows what would happen if the command runs without actually clearing data.

    .PARAMETER Confirm
        Prompts for confirmation before clearing workflow data.

    .EXAMPLE
        Clear-PSCueWorkflows -WhatIf
        Shows what would be cleared without actually clearing.

    .EXAMPLE
        Clear-PSCueWorkflows -Confirm
        Prompts for confirmation before clearing workflows.

    .EXAMPLE
        Clear-PSCueWorkflows
        Clears workflow data (prompts for confirmation by default).
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param()

    if ($null -eq [PSCue.Module.PSCueModule]::WorkflowLearner) {
        Write-Error "PSCue workflow learner is not initialized. Make sure the PSCue module is loaded and workflow learning is enabled."
        return
    }

    try {
        $learner = [PSCue.Module.PSCueModule]::WorkflowLearner
        $diagnostics = $learner.GetDiagnostics()
        $totalTransitions = $diagnostics.Item1

        if ($PSCmdlet.ShouldProcess(
            "$totalTransitions workflow transitions",
            "Clear workflow data from memory and database")) {

            # Clear in-memory workflow data
            $learner.Clear()

            # Also clear delta to prevent re-saving
            $learner.ClearDelta()

            # Clear from database
            if ($null -ne [PSCue.Module.PSCueModule]::Persistence) {
                $dbPath = [PSCue.Module.PSCueModule]::Persistence.DatabasePath

                if (Test-Path $dbPath) {
                    # Load SQLite assembly
                    Add-Type -Path ([PSCue.Module.PSCueModule]::Persistence.GetType().Assembly.Location)

                    # Open database connection
                    $connectionString = "Data Source=$dbPath;Mode=ReadWrite"
                    $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
                    $connection.Open()

                    try {
                        # Delete all workflow transitions
                        $cmd = New-Object Microsoft.Data.Sqlite.SqliteCommand("DELETE FROM workflow_transitions", $connection)
                        $rowsDeleted = $cmd.ExecuteNonQuery()
                        $cmd.Dispose()

                        Write-Host "Cleared $rowsDeleted workflow transitions from database." -ForegroundColor Green
                    }
                    finally {
                        $connection.Close()
                        $connection.Dispose()
                    }
                }
            }

            Write-Host "Workflow data cleared successfully." -ForegroundColor Green
            Write-Host "PSCue will start learning new workflows from scratch." -ForegroundColor Gray
        }
    }
    catch {
        Write-Error "Error clearing workflows: $_"
    }
}

function Export-PSCueWorkflows {
    <#
    .SYNOPSIS
        Exports learned workflows to a JSON file.

    .DESCRIPTION
        Exports all learned workflow patterns to a JSON file for backup or sharing.
        The exported file can be imported on another machine or after clearing workflows.

    .PARAMETER Path
        Path to the JSON file to create.

    .EXAMPLE
        Export-PSCueWorkflows -Path ~/workflows.json
        Exports all workflows to workflows.json.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if ($null -eq [PSCue.Module.PSCueModule]::WorkflowLearner) {
        Write-Error "PSCue workflow learner is not initialized. Make sure the PSCue module is loaded and workflow learning is enabled."
        return
    }

    try {
        $learner = [PSCue.Module.PSCueModule]::WorkflowLearner
        $allWorkflows = $learner.GetAllWorkflows()

        if ($allWorkflows.Count -eq 0) {
            Write-Warning "No workflows to export."
            return
        }

        # Convert to serializable format
        $exportData = @{
            ExportedAt = (Get-Date).ToString("o")
            Version    = "1.0"
            Workflows  = @{}
        }

        foreach ($fromCmd in $allWorkflows.Keys) {
            $exportData.Workflows[$fromCmd] = @{}

            foreach ($transition in $allWorkflows[$fromCmd]) {
                $exportData.Workflows[$fromCmd][$transition.NextCommand] = @{
                    Frequency        = $transition.Frequency
                    TotalTimeDeltaMs = $transition.TotalTimeDeltaMs
                    FirstSeen        = $transition.FirstSeen.ToString("o")
                    LastSeen         = $transition.LastSeen.ToString("o")
                }
            }
        }

        $exportData | ConvertTo-Json -Depth 10 | Set-Content -Path $Path -Encoding UTF8

        Write-Host "Exported $($allWorkflows.Count) workflow patterns to: $Path" -ForegroundColor Green
    }
    catch {
        Write-Error "Error exporting workflows: $_"
    }
}

function Import-PSCueWorkflows {
    <#
    .SYNOPSIS
        Imports workflows from a JSON file.

    .DESCRIPTION
        Imports workflow patterns from a previously exported JSON file.
        Can merge with existing workflows or replace them.

    .PARAMETER Path
        Path to the JSON file to import.

    .PARAMETER Merge
        Merge imported workflows with existing data instead of replacing.
        Frequencies will be summed for matching transitions.

    .EXAMPLE
        Import-PSCueWorkflows -Path ~/workflows.json
        Replaces current workflows with imported data.

    .EXAMPLE
        Import-PSCueWorkflows -Path ~/workflows.json -Merge
        Merges imported workflows with existing data.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [switch]$Merge
    )

    if ($null -eq [PSCue.Module.PSCueModule]::WorkflowLearner) {
        Write-Error "PSCue workflow learner is not initialized. Make sure the PSCue module is loaded and workflow learning is enabled."
        return
    }

    if (-not (Test-Path $Path)) {
        Write-Error "File not found: $Path"
        return
    }

    try {
        $importData = Get-Content -Path $Path -Raw | ConvertFrom-Json

        if (-not $importData.Workflows) {
            Write-Error "Invalid workflow export file format."
            return
        }

        $learner = [PSCue.Module.PSCueModule]::WorkflowLearner

        # Clear existing data if not merging
        if (-not $Merge) {
            $learner.Clear()
        }

        # Convert imported data to WorkflowTransition objects
        $workflowDict = @{}

        foreach ($fromCmd in $importData.Workflows.PSObject.Properties.Name) {
            $workflowDict[$fromCmd] = @{}

            foreach ($toCmd in $importData.Workflows.$fromCmd.PSObject.Properties.Name) {
                $data = $importData.Workflows.$fromCmd.$toCmd

                $transition = New-Object PSCue.Module.WorkflowTransition
                $transition.NextCommand = $toCmd
                $transition.Frequency = $data.Frequency
                $transition.TotalTimeDeltaMs = $data.TotalTimeDeltaMs
                $transition.FirstSeen = [DateTime]::Parse($data.FirstSeen)
                $transition.LastSeen = [DateTime]::Parse($data.LastSeen)

                $workflowDict[$fromCmd][$toCmd] = $transition
            }
        }

        # Initialize with imported data (merges automatically if learner already has data)
        $learner.Initialize($workflowDict)

        $totalImported = ($workflowDict.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum

        if ($Merge) {
            Write-Host "Merged $totalImported workflow transitions from: $Path" -ForegroundColor Green
        }
        else {
            Write-Host "Imported $totalImported workflow transitions from: $Path" -ForegroundColor Green
        }

        Write-Host "Use Save-PSCueLearning to persist the imported workflows to the database." -ForegroundColor Gray
    }
    catch {
        Write-Error "Error importing workflows: $_"
    }
}

# Note: Functions are exported via FunctionsToExport in PSCue.psd1 manifest
# Export-ModuleMember is not needed when using dot-sourcing in PSCue.psm1

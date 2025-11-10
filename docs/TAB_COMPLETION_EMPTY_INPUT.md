# Tab Completion on Empty Input

## Problem Statement

PowerShell's standard completion system (`Register-ArgumentCompleter`, `ICommandPredictor`) does not support showing tab completions when the user presses `<Tab>` on an empty command line.

**Current Behavior**:
- `Register-ArgumentCompleter`: Only fires for specific commands/parameters. Cannot intercept Tab on empty input.
- `ICommandPredictor`: Does NOT show predictions until the user begins typing. No suggestions on empty prompt.
- `TabExpansion2`: PowerShell's `CompleteInput()` method explicitly returns empty results for empty input strings (by design).

**Desired Behavior**:
Show frequent/recent commands when user presses Tab on an empty command line, leveraging PSCue's learned command history.

---

## Research Summary

### Why Standard APIs Don't Work

1. **CompleteInput() Design**: From PowerShell source (`CommandCompletion.cs`):
   ```csharp
   if (input == null || input.Length == 0)
   {
       return s_emptyCommandCompletion;  // Empty results, all indices = -1
   }
   ```
   This is intentional and cannot be bypassed.

2. **Register-ArgumentCompleter Limitations**:
   - Requires a command name to register
   - Cannot use wildcard (`-CommandName '*'`) for PowerShell commands
   - Only works when PowerShell knows what command/parameter is being completed

3. **ICommandPredictor Limitations**:
   - Only shows gray text suggestions after user starts typing
   - Does not activate on empty prompt

---

## Solution Approaches

### Approach 1: Override TabExpansion2 (Recommended)

**How It Works**:
- Override the global `TabExpansion2` function
- Detect empty input and return custom `CompletionResult` objects
- Delegate to standard completion for non-empty input
- PSReadLine automatically handles display (InlineView/ListView/MenuComplete)

**Pros**:
- Works with standard PSReadLine completion flow
- Respects user's `Set-PSReadLineOption -PredictionViewStyle` setting
- No manual cycling logic needed
- Clean integration

**Cons**:
- Overriding `TabExpansion2` is not officially supported (but commonly done)
- Potential conflicts with other modules that override `TabExpansion2` (last one wins)
- Must handle both parameter sets (ScriptInput and AstInput)

**Implementation**:
```powershell
# Save the original TabExpansion2 if it exists
if (Get-Command TabExpansion2 -ErrorAction SilentlyContinue) {
    Rename-Item Function:\TabExpansion2 TabExpansion2_Original
}

function global:TabExpansion2 {
    [CmdletBinding(DefaultParameterSetName = 'ScriptInputSet')]
    param(
        [Parameter(ParameterSetName = 'ScriptInputSet', Mandatory, Position = 0)]
        [string]$inputScript,
        [Parameter(ParameterSetName = 'ScriptInputSet', Position = 1)]
        [int]$cursorColumn = $inputScript.Length,
        [Parameter(ParameterSetName = 'AstInputSet', Mandatory, Position = 0)]
        [System.Management.Automation.Language.Ast]$ast,
        [Parameter(ParameterSetName = 'AstInputSet', Mandatory, Position = 1)]
        [System.Management.Automation.Language.Token[]]$tokens,
        [Parameter(ParameterSetName = 'AstInputSet', Mandatory, Position = 2)]
        [System.Management.Automation.Language.IScriptPosition]$positionOfCursor,
        [Parameter(ParameterSetName = 'ScriptInputSet', Position = 2)]
        [Parameter(ParameterSetName = 'AstInputSet', Position = 3)]
        [Hashtable]$options = $null
    )

    # Handle empty input
    if ([string]::IsNullOrWhiteSpace($inputScript)) {
        if ($null -ne [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            $graph = [PSCue.Module.PSCueModule]::KnowledgeGraph
            $trackedCommands = $graph.GetTrackedCommands()

            if ($trackedCommands.Count -gt 0) {
                # Create CompletionResult objects for frequent commands
                $completions = $trackedCommands | Select-Object -First 10 | ForEach-Object {
                    [System.Management.Automation.CompletionResult]::new(
                        $_,                                                    # completionText
                        $_,                                                    # listItemText
                        [System.Management.Automation.CompletionResultType]::Command,
                        "Frequent command: $_"                                 # toolTip
                    )
                }

                # Return CommandCompletion object
                return [System.Management.Automation.CommandCompletion]::new(
                    $completions,  # completionMatches
                    -1,            # replacementIndex
                    0,             # replacementLength
                    0              # currentMatchIndex
                )
            }
        }
    }

    # Non-empty: delegate to original or default completion
    if (Get-Command TabExpansion2_Original -ErrorAction SilentlyContinue) {
        return TabExpansion2_Original @PSBoundParameters
    }
    else {
        return [System.Management.Automation.CommandCompletion]::CompleteInput(
            $inputScript,
            $cursorColumn,
            $options
        )
    }
}
```

**Behavior**:
- User presses `<Tab>` on empty line → Shows top 10 frequent commands
- PSReadLine displays according to user's settings:
  - **MenuComplete**: Arrow keys to select from list
  - **InlineView**: Right arrow to accept, Tab to cycle
  - **ListView**: F2 to show list view

---

### Approach 2: Set-PSReadLineKeyHandler (Alternative)

**How It Works**:
- Override the Tab key binding with custom script block
- Detect empty input and manually cycle through commands
- Delegate to standard completion for non-empty input

**Pros**:
- Full control over Tab behavior
- Can implement custom UI/cycling logic
- No conflicts with TabExpansion2 overrides

**Cons**:
- Must implement own cycling/state tracking
- Requires manual state management between Tab presses
- User experience may differ from standard tab completion
- Doesn't respect PSReadLine's completion style settings

**Implementation** (for reference):
```powershell
Set-PSReadLineKeyHandler -Key Tab -ScriptBlock {
    param($key, $arg)

    $line = $null
    $cursor = $null
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$line, [ref]$cursor)

    if ([string]::IsNullOrWhiteSpace($line)) {
        if ($null -ne [PSCue.Module.PSCueModule]::KnowledgeGraph) {
            $graph = [PSCue.Module.PSCueModule]::KnowledgeGraph
            $trackedCommands = $graph.GetTrackedCommands()

            if ($trackedCommands.Count -gt 0) {
                # Track index using script-scoped variable
                if ($null -eq $script:PSCueTabIndex) {
                    $script:PSCueTabIndex = 0
                }
                else {
                    $script:PSCueTabIndex = ($script:PSCueTabIndex + 1) % $trackedCommands.Count
                }

                $command = $trackedCommands[$script:PSCueTabIndex]

                # Replace line with command
                [Microsoft.PowerShell.PSConsoleReadLine]::RevertLine()
                [Microsoft.PowerShell.PSConsoleReadLine]::Insert($command)
                return
            }
        }

        # Fallback to standard completion
        [Microsoft.PowerShell.PSConsoleReadLine]::Complete($true)
    }
    else {
        # Reset index when typing starts
        $script:PSCueTabIndex = $null
        [Microsoft.PowerShell.PSConsoleReadLine]::Complete($true)
    }
}
```

---

## Data Sources for Completions

PSCue has multiple sources for command suggestions:

1. **ArgumentGraph.GetTrackedCommands()**: Returns list of all learned command names
   - Simple string list
   - No usage count information directly available

2. **ArgumentGraph.GetAllCommands()**: Returns dictionary of command → CommandKnowledge
   - Contains `TotalUsageCount`, `FirstSeen`, `LastUsed`
   - PowerShell enumeration issue: returns empty keys/null values (needs investigation)

3. **WorkflowLearner.GetNextCommandPredictions()**: Returns workflow-based predictions
   - Requires command history context
   - Returns empty list on empty input (no prior commands)

**Current Approach**: Use `GetTrackedCommands()` for simplicity. Future enhancement could sort by usage count once `GetAllCommands()` enumeration is fixed.

---

## Testing Results

### Attempt 1: Set-PSReadLineKeyHandler with Manual Cycling
- ✅ Successfully detected empty input
- ✅ Retrieved tracked commands from ArgumentGraph
- ✅ Inserted first command
- ❌ Only showed one option at a time (manual cycling)
- ❌ Doesn't respect user's PSReadLine settings

### Attempt 2: TabExpansion2 Override (Current Recommendation)
- ✅ Returns proper `CompletionResult` objects
- ✅ PSReadLine handles display automatically
- ✅ Respects user's `PredictionViewStyle` setting
- ✅ Standard Tab behavior for non-empty input
- ⏳ Pending testing

---

## Implementation Plan for PSCue

### Phase 1: Prototype (User Testing)
- Add TabExpansion2 override to user's PowerShell profile
- Test with different `Set-PSReadLineOption -PredictionViewStyle` settings
- Gather feedback on usefulness and UX

### Phase 2: Integration (If Successful)
- Add TabExpansion2 override to PSCue module initialization
- Make it opt-in via environment variable: `PSCUE_EMPTY_TAB_COMPLETION` (default: false)
- Sort commands by usage count (requires fixing GetAllCommands enumeration)
- Add workflow predictions as fallback/boost

### Phase 3: Enhancement
- Combine ArgumentGraph frequent commands + WorkflowLearner predictions
- Show usage count and last used date in tooltips
- Configurable number of suggestions (default: 10)
- Filter out rarely-used commands (min usage threshold)

---

## Configuration (Proposed)

```powershell
# Enable tab completion on empty input (default: false, opt-in)
$env:PSCUE_EMPTY_TAB_COMPLETION = "true"

# Number of suggestions to show (default: 10)
$env:PSCUE_EMPTY_TAB_MAX_SUGGESTIONS = "10"

# Minimum usage count to include in suggestions (default: 3)
$env:PSCUE_EMPTY_TAB_MIN_USAGE = "3"
```

---

## References

- **PowerShell Source**: `CommandCompletion.cs` - Empty input returns `s_emptyCommandCompletion`
- **PSReadLine Methods**: `GetBufferState()`, `Insert()`, `RevertLine()`, `Complete()`, `MenuComplete()`
- **Real-World Examples**:
  - **PSFzf**: Uses `Set-PSReadLineKeyHandler` but explicitly exits early on empty input
  - **TabExpansionPlusPlus**: Extends TabExpansion2 but doesn't handle empty input
  - **Windows PowerShell Cookbook**: Shows TabExpansion2 override pattern

---

## Related Files

- `src/PSCue.Module/ArgumentGraph.cs`: Line 474 (`GetTrackedCommands()`), Line 482 (`GetAllCommands()`)
- `src/PSCue.Module/WorkflowLearner.cs`: Line 296 (`GetNextCommandPredictions()`)
- `module/PSCue.psm1`: Module initialization (future integration point)

---

## Notes

- TabExpansion2 override is a well-known extension point (Windows PowerShell Cookbook, TabExpansionPlusPlus)
- Not officially "supported" but widely used in the PowerShell community
- Risk of conflict with other modules that override TabExpansion2 (mitigated by checking for existing override)
- Performance target: <50ms for completion generation (same as standard completions)

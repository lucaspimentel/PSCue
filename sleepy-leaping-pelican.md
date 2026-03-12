# Plan: JSON-Based External Completion Loading (Proof-of-Concept)

## Context
Currently all tab-completion definitions are hardcoded in C# (`KnownCompletions/*.cs`). Moving them to external JSON files would allow extending/updating completions without rebuilding PSCue. This PR adds JSON loading for a single command (`dust`) while keeping the hardcoded path, so we can benchmark and compare before committing to the approach.

## Branch
`lpimentel/json-completions` (created from `main`)

## Changes

### 1. JSON DTO models
**New file:** `src/PSCue.Shared/Completions/Json/CommandDefinition.cs`

Three simple DTOs mirroring the completion model (settable properties for AOT-friendly deserialization):
- `CommandDefinition` — completionText, tooltip, alias, subCommands[], parameters[]
- `ParameterDefinition` — completionText, tooltip, alias, requiresValue, staticArguments[]
- `ArgumentDefinition` — completionText, tooltip

### 2. System.Text.Json source generator context
**New file:** `src/PSCue.Shared/Completions/Json/CompletionJsonContext.cs`

`[JsonSerializable(typeof(CommandDefinition))]` partial class with camelCase naming policy. Required for NativeAOT compatibility (no reflection).

### 3. JSON-to-model mapper
**New file:** `src/PSCue.Shared/Completions/Json/JsonCompletionLoader.cs`

- `LoadFromFile(string path)` — reads file + deserializes
- `LoadFromJson(string json)` — deserializes from string (for tests)
- Private `MapCommand`/`MapParameter`/`MapArgument` methods that construct the existing `Command`/`CommandParameter`/`StaticArgument` objects

### 4. dust.json definition file
**New file:** `src/PSCue.Shared/Completions/Json/Definitions/dust.json`

1:1 translation of `DustCommand.cs` into JSON. All 35 parameters with aliases, tooltips, and static arguments for `--output-format` and `--filetime`.

### 5. Copy JSON files to output
**Modify:** `src/PSCue.Shared/PSCue.Shared.csproj`

Add `<Content>` item to copy `Completions/Json/Definitions/*.json` to output as `completions/*.json`. Verify transitive copy works for test and benchmark projects; add explicit content references if not.

### 6. A/B switching in CommandCompleter
**Modify:** `src/PSCue.Shared/CommandCompleter.cs`

- Add env var check: `PSCUE_JSON_COMPLETIONS` (comma-separated command names, e.g. `"dust"`)
- Resolve `completions/` directory relative to `AppContext.BaseDirectory`
- Static `Dictionary<string, Command?>` cache (parse JSON once, reuse)
- Before the existing switch: if command is JSON-enabled, try `LoadJsonCommand()`; fall through to hardcoded on miss

### 7. Equivalence tests
**New file:** `test/PSCue.ArgumentCompleter.Tests/JsonCompletionLoaderTests.cs`

- Deep equality test: JSON-loaded `dust` vs `DustCommand.Create()` (compare all parameters, aliases, tooltips, static arguments)
- Parameter count match test
- End-to-end completion tests: verify `CommandCompleter.GetCompletions("dust ...")` produces same results via both paths

### 8. Performance benchmark
**New file:** `benchmark/PSCue.Benchmarks/JsonVsHardcodedBenchmark.cs`

Three benchmarks with `[MemoryDiagnoser]`:
- `Hardcoded_DustCommand` — baseline: `DustCommand.Create()`
- `Json_FromString` — `JsonCompletionLoader.LoadFromJson(preloadedString)`
- `Json_FromFile` — `JsonCompletionLoader.LoadFromFile(path)` (cold, no cache)

## Files unchanged
- `src/PSCue.Shared/KnownCompletions/DustCommand.cs` — kept for A/B comparison

## Key decisions
- **File-based, not embedded resources** — aligns with the end goal of user-extensible completions
- **Env var opt-in** — default behavior unchanged; set `PSCUE_JSON_COMPLETIONS=dust` to test
- **Static cache** — JSON parsed once per process, subsequent calls use cached `Command` object
- **No dynamic arguments in JSON** — they stay as C# delegates (dust has none, so no issue)
- **camelCase JSON** — `completionText`, `tooltip`, `alias`, `staticArguments`, etc.

## Verification
```bash
# Build
dotnet build src/PSCue.Shared/ -c Release

# Run equivalence tests
dotnet test test/PSCue.ArgumentCompleter.Tests/ --filter "FullyQualifiedName~JsonCompletionLoader"

# Run all tests (ensure nothing broke)
dotnet test test/PSCue.ArgumentCompleter.Tests/

# Run benchmark
dotnet run -c Release --project benchmark/PSCue.Benchmarks/ --filter *JsonVsHardcoded*

# Manual test: set env var and verify dust completions work
$env:PSCUE_JSON_COMPLETIONS = "dust"
# Then tab-complete "dust --" and verify results
```

# Migrating Completions from Hardcoded C# to JSON

## Background

Branch `lpimentel/json-completions` proved that JSON-based completions work correctly and perform well enough. This document outlines what's needed to fully migrate.

## Benchmark Results (dust command, 35 parameters)

| Path | Mean | Allocated | Notes |
|---|---|---|---|
| Hardcoded C# | 233 ns | 3.38 KB | Object construction only |
| JSON from string | 10.8 us | 17.05 KB | Deserialization + mapping |
| JSON from file | 29.8 us | 51.99 KB | File I/O + deserialization + mapping |

JSON from file is ~128x slower per call, but at 30us it's well within the 50ms tab-completion budget. The `ConcurrentDictionary` cache means the cost is paid once per process.

## What exists today (PoC)

- `Completions/Json/CommandDefinition.cs` — DTOs (CommandDefinition, ParameterDefinition, ArgumentDefinition)
- `Completions/Json/CompletionJsonContext.cs` — System.Text.Json source generator (NativeAOT-safe)
- `Completions/Json/JsonCompletionLoader.cs` — LoadFromFile/LoadFromJson + mapping to Command/CommandParameter/StaticArgument
- `Completions/Json/Definitions/dust.json` — proof-of-concept definition
- `CommandCompleter.cs` — env var `PSCUE_JSON_COMPLETIONS` opt-in, static cache, `ProcessArguments` extraction
- Equivalence tests comparing JSON-loaded dust vs `DustCommand.Create()`

## Steps to fully migrate

### 1. Create JSON files for each command

One JSON file per command in `Completions/Json/Definitions/`:

- `scoop.json`, `winget.json`, `wt.json` (Windows-only)
- `code.json`, `chezmoi.json`, `git.json`, `gh.json`, `gt.json`, `claude.json`
- `tre.json`, `lsd.json`, `chafa.json`, `rg.json`, `fd.json`
- `azd.json`, `az.json`, `func.json`
- `set-location.json` (shared by cd, sl, chdir)

Use the existing `dust.json` as the template. Write equivalence tests for each (same pattern as `JsonCompletionLoaderTests`).

### 2. Handle SubCommands

The DTO already supports `subCommands[]`. Commands like `git`, `gh`, `scoop`, `winget` have deep nesting — verify the recursive mapping works for 3+ levels.

### 3. Handle DynamicArguments

JSON cannot express C# delegates. Commands with `DynamicArguments` (git branches, scoop packages, etc.) need a hybrid approach:

**Option A: Registry pattern** — JSON defines static completions; a separate C# registry maps command names to `DynamicArgumentsFactory` delegates. The loader merges them after deserialization.

**Option B: Named providers in JSON** — Add a `"dynamicArguments": "gitBranches"` string field to the JSON. The loader resolves it via a dictionary of known providers. Requires maintaining a provider registry in C#.

**Option C: Keep hybrid** — Commands with dynamic args stay hardcoded; commands without (dust, tre, lsd, chafa, rg, fd, etc.) use JSON. Simplest, covers most commands.

### 4. Handle platform-specific commands

Currently `scoop`, `winget`, and `wt` are gated by `when isWindows` in the switch. Options:

- Add an optional `"platform": "windows"` field to CommandDefinition
- Or handle in the loader/CommandCompleter (skip loading if platform doesn't match)

### 5. Replace the switch statement in CommandCompleter

Once all commands have JSON files:

1. Remove the `PSCUE_JSON_COMPLETIONS` env var check — JSON becomes the default
2. Replace the switch statement with a directory scan or known-commands list
3. Keep the cache (already implemented)
4. Optionally support a user-extensible completions directory (e.g., `~/.config/pscue/completions/`)

### 6. Remove hardcoded KnownCompletions classes

After each command is migrated and equivalence-tested, delete the corresponding `KnownCompletions/*.cs` file. Do this incrementally — one command at a time.

### 7. User-extensible completions (end goal)

Allow users to drop JSON files into a config directory to add completions for commands PSCue doesn't ship with. Loading order:

1. Built-in `completions/` directory (shipped with PSCue)
2. User directory (e.g., `$PSCUE_DATA_DIR/completions/` or `~/.config/pscue/completions/`)
3. User files override built-in files (same filename = replace)

## Commands without DynamicArguments (migrate first)

These are pure static completions — straightforward 1:1 JSON translation:

- dust, tre, lsd, chafa, rg, fd
- claude, code, gt
- azd, az, func
- set-location

## Commands with DynamicArguments (migrate later)

These need the hybrid approach from step 3:

- git (branches, tags, remotes, stashes)
- scoop (installed packages)
- winget (no dynamic args currently, but Windows-only)
- wt (Windows-only)
- gh (no dynamic args currently)
- chezmoi (no dynamic args currently)

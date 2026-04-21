# Changelog

## [Unreleased]

### Changed
- Enable ReadyToRun compilation for `PSCue.Module` and `PSCue.Shared` to reduce first-touch JIT cost on cold module imports
- Consolidate `Functions/*.ps1` into a single `module/Functions.ps1` to reduce cold-disk dot-sourcing time on module import
- Share a single SQLite connection across background load operations, avoiding redundant connection open and `PRAGMA busy_timeout` cycles
- Batch the `ArgumentGraph` load into a single multi-statement SQLite query, collapsing seven round-trips into one
- Skip redundant schema DDL on module import when the learning database is already at the current schema version
- Bump Microsoft.Data.Sqlite from 10.0.5 to 10.0.6
- Bump System.Security.Cryptography.Xml from 9.0.14 to 9.0.15

## [0.20.0] - 2026-04-17

### Added
- Add Ctrl+B bookmark toggle in `pcd -i` interactive selector
- Add phase-by-phase import timing logs under `PSCUE_DEBUG`

### Changed
- Bump Microsoft.NET.Test.Sdk from 18.3.0 to 18.4.0

## [0.19.2] - 2026-04-13

### Fixed
- Overhaul `gh` completer: add missing parameters across all subcommands, add missing commands (cache, gpg-key, ssh-key, variable, attestation, release edit/delete-asset, repo archive/edit/unarchive, run delete), add `--json` field suggestions for pr, issue, run, and release subcommands

## [0.19.1] - 2026-04-13

### Fixed
- Fix git-wt completer: rename `--no-verify` to `--no-hooks`, add missing `--format` to switch/list/remove/merge, add `list statusline` subcommand, add parameters to step subcommands (commit, squash, push, prune, for-each), add `config plugins` subcommand, remove non-existent `--var` from hook

## [0.19.0] - 2026-04-13

### Changed
- Move all database loading to background thread so `Import-Module` returns instantly instead of blocking for 2-4 seconds

## [0.18.0-beta] - 2026-04-10

### Added
- Add directory bookmarking for `pcd`: toggle with `pcd -b`, list with `pcd -lb`, bookmarks appear at top of tab completions and interactive mode with star indicator
- Add matched character highlighting in interactive menu (`pcd -i`): query matches are shown in green

### Changed
- Switch `pcd -i` interactive mode to full-screen alternate buffer: uses entire terminal height, restores previous content on exit

### Fixed
- Fix stale lines not being cleared in interactive menu when filtering reduces results

## [0.17.0-beta] - 2026-04-05

### Added
- Add `pcdi` alias as shorthand for `pcd -i` (interactive mode)

### Changed
- Replace Spectre.Console with custom fzf-style interactive menu in `pcd -i` / `pcdi`: typing live-filters results using subsequence matching, eliminating ~1.2MB of dependency DLLs
- Upgrade PCD fuzzy matching to fzf-style subsequence matching with boundary bonuses, enabling abbreviation queries like `ddt` → `dd-trace-dotnet`. Levenshtein distance retained as typo-tolerance fallback
- Apply subsequence matching to PCD interactive mode (`pcd -i <filter>`), replacing simple substring filtering with the same fzf-style matching used by tab completion and best-match navigation

## [0.16.0-beta] - 2026-03-30

### Changed
- Update claude CLI completions: add auto-mode subcommand, --bare, --brief, --name options, max effort level, and auto permission mode
- Add PSCue.psd1 to ship version-files

## [0.15.0-beta] - 2026-03-30

### Added
- Add git-wt (worktrunk) tab completion with subcommands, dynamic branch suggestions, and parameter aliases

### Changed
- Simplify .editorconfig for now

## [0.14.0-beta] - 2026-03-23

### Fixed
- Clear SQLite connection pool on dispose

## [0.13.0-beta] - 2026-03-23

### Added
- Expand az CLI completions with nested subcommands
- Add `--version` flag to pcd
- Add checksum verification to install-remote.ps1
- Add centralized Directory.Build.props

### Changed
- Update GitHub Actions to latest versions, pinned to commit SHAs
- Bump nuget references
- Clean up release workflow: remove redundant flags, exclude .pdb files, flatten native SQLite lib

### Fixed
- Add .NET 9.0 SDK to CI workflow

## [0.12.0-beta] - 2026-03-10

### Added
- Add fd (fd-find) tab completion support
- Add ripgrep (rg) command completer
- Add chafa command completer
- Overhaul claude command completions
- Add `-Update` parameter to install-local.ps1
- Add Scoop installation instructions to README
- Add release workflow badge to README
- Add path filter to pcd `-Interactive`
- Add dev install isolation support (`PSCUE_DATA_DIR`, `-InstallPath`)

### Changed
- Optimize NativeAOT build settings

### Fixed
- Correct filesystem casing for learned directory paths
- Prune stale directory entries from learning database
- Eliminate static mutation races in tests
- Use temp directories in PcdEnhancedTests instead of hardcoded paths
- Use PascalCase for local const MaxSequences

## [0.11.0-beta] - 2026-03-01

### Added
- Add pcd `-` to navigate to previous directory
- Add pcd `-Root` parameter for git repo root navigation
- Add path filter to pcd `-Interactive`
- Register claude command for tab completion

### Changed
- Update nuget packages

### Fixed
- Use ASCII fallbacks for non-UTF-8 consoles
- Build before deleting existing installation
- Remove orphaned cache exports and PSCUE_PID
- Fix stale references and remove phase tags

## [0.10.0-beta] - 2026-02-18

### Added
- Add cancel option to pcd interactive mode
- Enhance pcd interactive mode visual styling

### Fixed
- Use PowerShell `$PWD` instead of process CWD for path normalization
- Exclude current dir and `..` from pcd interactive selector
- Update navigation timestamps correctly
- Preserve backslashes in Windows paths
- Ensure trailing separator on normalized paths
- Remove Export-ModuleMember from dot-sourced files

## [0.9.0-beta] - 2026-02-17

### Added
- Add interactive directory selection to pcd with Spectre.Console

### Fixed
- Add Spectre.Console to install dependencies

## [0.8.0-beta] - 2026-02-17

### Added
- Add dynamic git alias and command completion support

### Fixed
- Preserve dot-prefixed dirs and expand tilde in cd
- Use temp paths in tests for cross-platform compatibility

## [0.7.0-beta] - 2025-12-22

### Changed
- Move install scripts to root directory
- Move test-scripts into test directory

### Fixed
- Ensure release includes all dependencies via `dotnet publish`
- Update install scripts for root directory

## [0.6.0-beta] - 2025-11-24

### Added
- Enable recursive search by default for pcd
- Improve pcd completion with filesystem search
- Add exact match scoring boost for PCD
- Add symlink resolution for path deduplication
- Add cache/metadata directory filtering to PCD
- Add parameter-value binding with database schema
- Add `RequiresValue` property to CommandParameter

### Changed
- Update to .NET 10

### Fixed
- Prioritize exact PCD matches and ensure trailing separators
- Improve fuzzy matching quality for PCD
- Prioritize parent dir in pcd best-match navigation
- Handle non-existent paths gracefully in pcd
- Prevent co-occurrence count overflow

## [0.5.0] - 2025-11-22

### Added
- Match native cd behavior for pcd completions

### Changed
- Bump Microsoft.PowerShell.SDK, Microsoft.Data.Sqlite, Microsoft.NET.Test.Sdk, coverlet.collector, BenchmarkDotNet

## [0.4.0] - 2025-11-10

### Added
- Add ML-based n-gram sequence prediction
- Add multi-word prediction suggestions
- Add sensitive data protection
- Add partial command predictions
- Add pcd command for smart directory navigation
- Enhance pcd with fuzzy matching and frecency scoring
- Add pcd inline predictions with relative paths
- Add dynamic workflow learning with PowerShell management functions
- Add Windows Terminal (wt) command support
- Add Graphite CLI (gt) auto-completion support
- Add Alias support to Command class
- Add `-Force` parameter to `Clear-PSCueLearning` for database recovery
- Add unconditional error logging for critical exceptions

### Changed
- Configure win-x64 and linux-x64 releases

### Fixed
- Filter partial word completions correctly
- Implement delta tracking for co-occurrences
- Allow prefix matching for ambiguous aliases in completions
- Prevent count duplication in persistence with delta tracking

## [0.2.0] - 2025-11-04

### Fixed
- Update tests for refactored architecture

## [0.1.0] - 2025-11-04

### Added
- Initial release of PSCue PowerShell completion module
- ArgumentCompleter (NativeAOT exe) for fast tab completion (<10ms startup)
- Module (managed DLL) implementing `ICommandPredictor` and `IFeedbackProvider`
- Learning system with ArgumentGraph knowledge graph, frequency and recency scoring
- Cross-session persistence with SQLite (WAL mode for concurrent access)
- Generic command learning via FeedbackProvider
- Directory-aware navigation completions for cd/Set-Location
- Completions for git, scoop, and claude commands
- IPC communication layer between ArgumentCompleter and Module
- PowerShell module functions for cache management, learning, and debugging
- Database query functions for direct SQLite access
- Path normalization and context-aware cd predictions
- SQLite busy timeout for concurrent access
- GitHub Actions CI/CD workflows
- Local and remote installation scripts

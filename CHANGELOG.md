# Changelog

## [Unreleased]

### Added
- Add git-wt (worktrunk) tab completion with subcommands, dynamic branch suggestions, and parameter aliases

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

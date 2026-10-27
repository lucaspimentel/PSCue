# Spawn cost measurement

Measures the fresh-process cost of `pscue-completer`, the NativeAOT executable PowerShell
spawns on every Tab press, and attributes that cost to child-process launches.

## Why this exists

`pscue-completer` is a one-shot process: it answers one completion request and exits. Some
completion scenarios launch child processes, and the cost of doing so is invisible to
in-process benchmarking, so it needs its own measurement.

Git lookups run `git` directly through `CommandProcess` (`src/PSCue.Shared/CommandProcess.cs`),
which resolves the executable against `PATH` and starts the image with no shell in between.
Scoop still goes through `Helpers.ExecuteCommand` (`src/PSCue.Shared/Helpers.cs:56`), which
starts a full `pwsh -NoProfile -NonInteractive` per call for a shim that is itself a `.ps1`
script, so one scoop lookup pays PowerShell startup twice.

`benchmark/PSCue.Benchmarks` uses BenchmarkDotNet against the shared library **in process**.
It cannot see process creation, PowerShell startup, or child-process launches. This harness
covers exactly that gap.

## What it measures

1. **Wall time per scenario** - runs the published native executable directly and times the
   child process lifetime.
2. **Spawn count per scenario** - puts logging shims for `git` and `scoop` on the child's
   `PATH` and counts invocations, attributing each to a factory. Each invocation records
   itself in a directory it reserves with `mkdir` (atomic, and the only reliable way to get a
   unique name here: `cmd.exe` seeds `%RANDOM%` identically in every process), so concurrent
   launches cannot lose or overwrite each other's entry.
3. **Standalone spawn cost** - times the exact
   `pwsh -NoProfile -NonInteractive -Command "<command>"` invocation, plus a bare `exit 0`
   probe for pure PowerShell startup. Git no longer launches through PowerShell, so these
   probes are reference costs for the wrapped form rather than the cost of the current git
   path; scoop still uses the wrapper. They remain useful as a before/after of the wrapper
   itself.

No production source is modified. There is no instrumented build and nothing to revert.

## Prerequisites

- PowerShell 7.2+
- .NET SDK able to publish the completer's NativeAOT target (`net10.0`)
- NativeAOT prerequisites for your platform (C toolchain)
- `git`; `scoop` is optional and Windows-only

The completer must already be published (use `-Build`, or point `-CompleterPath` at an
existing binary). The harness runs the native executable directly and does not load the
PowerShell module.

## Usage

```bash
# Validate the harness without measuring anything
pwsh -NoProfile -File ./benchmark/spawn-cost/Measure-CompleterSpawn.ps1 -SelfTest

# Publish the completer, then measure it
pwsh -NoProfile -File ./benchmark/spawn-cost/Measure-CompleterSpawn.ps1 -Build

# Measure an already-published executable
pwsh -NoProfile -File ./benchmark/spawn-cost/Measure-CompleterSpawn.ps1 \
    -CompleterPath ./artifacts/spawn-cost/bin/pscue-completer

# Full run, as documented
pwsh -NoProfile -File ./benchmark/spawn-cost/Measure-CompleterSpawn.ps1 \
    -Build -WarmupCount 10 -RepeatCount 100 -Seed 12345

# Regression checks for the harness itself
pwsh -NoProfile -File ./test/test-scripts/test-completer-timing.ps1
```

Useful switches: `-SkipSpawnProbe` (wall time only), `-RepeatCount`, `-WarmupCount`,
`-SpawnRepeatCount`, `-Seed`, `-TimeoutSeconds`, `-WorkingDirectory`, `-OutputDirectory`.

`-SpawnRepeatCount` must be at least 2: the first probe run is discarded as a cold start,
so a value of 1 leaves no measured samples.

Scenario wall time is the dominant runtime: `git ` costs ~1-7 s per invocation, so
`-RepeatCount 100` over the whole matrix takes roughly 25-40 minutes.

## Outputs

Written to `artifacts/spawn-cost/<timestamp>/` (or `-OutputDirectory`):

| File | Contents |
| --- | --- |
| `metadata.json` | Environment, source revision, completer hash, settings |
| `samples.csv` | Every invocation: scenario, phase, elapsed ms, exit code, stdout hash |
| `summary.csv` | Per-scenario statistics and observed spawn counts |
| `spawn-counts.csv` | Spawn count and per-command breakdown |
| `spawn-costs.csv` | Standalone spawn probe statistics |
| `report.md` | Environment, wall time, spawn cost, and attribution tables |

## Scenario matrix

| Scenario | Command line | Purpose |
| --- | --- | --- |
| `baseline-no-args` | (no args) | Process creation + NativeAOT startup + trivial `Main` + exit |
| `unknown-command` | `pscue-unknown-command ` | Parsing and dispatch, no tree, no spawns |
| `static-pi` | `pi ` | Tree construction and output, no dynamic arguments |
| `git-root` | `git ` | Main/alias/others lookups |
| `git-no-trailing-space` | `git` | Control for navigation-time token search |
| `git-prefix-nomatch` | `git pscue-no-match` | Unsuccessful dynamic token search |
| `git-branches` | `git checkout ` | Branch lookup |
| `git-remotes` | `git fetch ` | Remote lookup (does not run a fetch) |
| `git-tags` | `git tag ` | Tag lookup (does not create a tag) |
| `gitwt-branches` | `git-wt switch ` | Worktrunk branch lookup |
| `gitwt-param-branches` | `git-wt switch --base ` | Parameter-value lookup |
| `scoop-root` | `scoop ` | Scoop root, no package lookup |
| `scoop-packages` | `scoop update ` | Triggers `scoop list` |
| `cd-directory` | `cd ` | Filesystem enumeration |

Scenarios whose required command is missing are skipped and reported as such. Scoop
scenarios never run on non-Windows platforms: the dispatcher does not build the scoop tree
there.

## Measured results

WSL2 (Ubuntu 24.04, `linux-x64`, PowerShell 7.6.7), source `20429f1`, 20 measured
invocations per scenario after 3 warmups, 7 standalone spawn probes at 7 samples each. Full
table: `artifacts/spawn-cost/wsl-linux-x64/report.md`.

| Scenario | Median | Spawns |
| --- | --- | --- |
| baseline (no args) | 2.8 ms | 0 |
| `pi ` | 2.0 ms | 0 |
| `cd ` | 2.0 ms | 0 |
| `git` (no trailing space) | 1,767 ms | 3 |
| `git checkout ` | 1,041 ms | 2 |
| `git ` | 3,682 ms | 6 |
| `git pscue-no-match` | 4,229 ms | 6 |

Standalone: bare `pwsh -NoProfile -NonInteractive -Command "exit 0"` = **437 ms**; each
`git` probe adds ~100-200 ms on top.

Windows (`win-x64`, Windows 10.0.26200, PowerShell 7.6.7), source `3aab8a4`, same 20 samples
per scenario after 3 warmups, 8 standalone spawn probes at 4 samples each. Full table:
`artifacts/spawn-cost/windows-x64/report.md`.

| Scenario | Median | P90 | Spawns |
| --- | --- | --- | --- |
| baseline (no args) | 86.7 ms | 91.0 ms | 0 |
| `scoop ` | 86.6 ms | 91.7 ms | 0 |
| `pi ` | 86.8 ms | 111.3 ms | 0 |
| `cd ` | 94.5 ms | 97.0 ms | 0 |
| `git fetch ` | 922.5 ms | 946.9 ms | 2 |
| `git-wt switch --base ` | 928.3 ms | 959.6 ms | 2 |
| `git-wt switch ` | 931.8 ms | 955.5 ms | 2 |
| `git tag ` | 934.9 ms | 968.2 ms | 2 |
| `git checkout ` | 956.8 ms | 1,004.6 ms | 2 |
| `git` (no trailing space) | 1,374.3 ms | 1,392.3 ms | 3 |
| `git pscue-no-match` | 2,683.4 ms | 2,735.2 ms | 6 |
| `git ` | 2,691.6 ms | 2,852.9 ms | 6 |
| `scoop update ` | 3,087.9 ms | 3,157.5 ms | 2 |

Standalone: bare `pwsh ... "exit 0"` = **354 ms**; `git` probes 418-444 ms; `scoop list` =
**1,469 ms**, the most expensive single spawn. Scoop's shim is a `.ps1` run through pwsh, so
one `scoop update ` completion pays PowerShell startup twice.

### Findings from these runs

Both runs predate the fixes that followed them. They are kept as the baseline that sized the
work, so read them as the state of the code at their recorded source revision.

- A single `pwsh` startup was the unit of cost, and the completer paid it 2-6 times per Tab.
- The dynamic factory ran **twice** per request, once from `Command.FindNode` and again from
  `Command.GetCompletions` (`src/PSCue.Shared/Completions/Command.cs`). `git ` and
  `git pscue-no-match` therefore spawned 6 times where 3 would do; `git` with no trailing
  space spawned 3 because navigation never runs.
- Spawns accounted for essentially all wall time. Windows residuals were 64-150 ms, at or near
  the spawn-free floor, and most WSL git scenarios had negative residuals, meaning the
  spawns accounted for all observed wall time.

One platform difference bears on the 50 ms target: the spawn-free floor is ~2.8 ms on WSL2
but ~87 ms on Windows, so on the primary platform a Tab press is already near 2x target
before any dynamic lookup runs.

## State since these runs

Two fixes landed against this baseline, each verified on both platforms with this harness by
comparing spawn counts and `StdoutHash` across the whole scenario matrix:

- **Dynamic arguments are evaluated once per request** (`DynamicArgumentsMemo`), halving
  spawn counts: `git ` 6 to 3, `git pscue-no-match` 6 to 3, every branch/remote/tag/worktree
  scenario 2 to 1, `scoop update ` 2 to 1. Completion output is byte-identical.
- **Git is launched directly instead of through PowerShell** (`CommandProcess`), which removes
  the `pwsh` startup per git lookup. On Windows this took `git ` from ~1,479 ms to ~575 ms and
  each single-lookup scenario from ~540 ms to ~220-270 ms, with spawn counts unchanged.
- **The three `--list-cmds` categories are fetched concurrently** (`GitCommand`), merged in a
  fixed order (main, aliases, extensions) so the completion set and its order are unchanged.
  Spawn counts stay at 3, one per category. On Windows this took `git ` from ~590 ms to
  ~270 ms, `git pscue-no-match` from ~576 ms to ~326 ms, and `git` (no trailing space) from
  ~604 ms to ~340 ms; the single-lookup and spawn-free scenarios did not move beyond noise.

The remaining cost is real child processes: `git ` on Windows is still three `git`
invocations, now overlapping, and each pays git's own startup on top of the ~87 ms process
floor. `scoop update ` is still ~1.6 s on Windows because `scoop list` remains a
wrapper-launched spawn.

The remaining open step (reading the scoop apps directory instead of running `scoop list`) is
tracked in `TODO.md` under "Tab Completion Spawn Cost", with its spawn-count gate and the
recorded before/after numbers.

## Noise controls and limits

- Wall time is child process lifetime only. PowerShell invocation and completion-result
  conversion add latency outside the executable.
- The harness warms up per scenario and excludes warmups from the statistics. Each process
  is fresh, so warmups warm OS caches, not a resident runtime.
- Derived spawn cost is an upper bound: probes start a cold `pwsh` each time, while
  sequential spawns within one completion may reuse warm caches. A negative residual means
  the spawns account for all observed wall time.
- Spawn counts come from `PATH` shims, so they are exact for name resolution but the shim
  replaces the real command output; stdout comparisons across shim runs are not meaningful.
  Compare `StdoutHash` between wall-time runs instead, which use the real commands.
- `cd ` is reachable through the executable dispatcher but is not how the installed module
  reaches `cd` completion.
- Latency assertions belong nowhere in CI: results are machine-dependent.

## Cleanup

Delete the run directory you created:

```bash
rm -rf artifacts/spawn-cost/wsl-linux-x64
```

`artifacts/` is git-ignored. Nothing else is written outside it.

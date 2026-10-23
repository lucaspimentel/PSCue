# Spawn cost measurement

Measures the fresh-process cost of `pscue-completer`, the NativeAOT executable PowerShell
spawns on every Tab press, and attributes that cost to child-process launches.

## Why this exists

`pscue-completer` is a one-shot process: it answers one completion request and exits. Some
completion scenarios shell out through `Helpers.ExecuteCommand`
(`src/PSCue.Shared/Helpers.cs:56`), which starts a full `pwsh -NoProfile -NonInteractive`
per call. That is invisible to in-process benchmarking, so it needs its own measurement.

`benchmark/PSCue.Benchmarks` uses BenchmarkDotNet against the shared library **in process**.
It cannot see process creation, PowerShell startup, or child-process launches. This harness
covers exactly that gap.

## What it measures

1. **Wall time per scenario** - runs the published native executable directly and times the
   child process lifetime.
2. **Spawn count per scenario** - puts logging shims for `git` and `scoop` on the child's
   `PATH` and counts invocations, attributing each to a factory.
3. **Standalone spawn cost** - times the exact
   `pwsh -NoProfile -NonInteractive -Command "<command>"` invocation that
   `Helpers.ExecuteCommand` performs, plus a bare `exit 0` probe for pure PowerShell startup.

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
invocations per scenario after 3 warmups, 8 standalone spawn probes. Full table:
`artifacts/spawn-cost/wsl-linux-x64/report.md`.

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

Two findings follow directly from the counts:

- A single `pwsh` startup is the unit of cost, and the completer pays it 2-6 times per Tab.
- The dynamic factory runs **twice** per request, once from `FindNode`
  (`src/PSCue.Shared/Completions/Command.cs:37`) and again from `GetCompletions`
  (`:82`). `git ` and `git pscue-no-match` therefore spawn 6 times where 3 would do; `git`
  with no trailing space spawns 3 because navigation never runs.

Windows results are not collected in this run, so Windows and Scoop numbers remain pending.

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
- `cd ` is reachable through the executable dispatcher but is not how the installed module
  reaches `cd` completion.
- Latency assertions belong nowhere in CI: results are machine-dependent.

## Cleanup

Delete the run directory you created:

```bash
rm -rf artifacts/spawn-cost/wsl-linux-x64
```

`artifacts/` is git-ignored. Nothing else is written outside it.

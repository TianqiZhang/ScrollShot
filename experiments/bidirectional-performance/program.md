# Bidirectional Performance Program

## Goal

Optimize the runtime cost of the `bidirectional-current` stitching profile without regressing correctness on the tracked real dumps or on deterministic synthetic fixtures.

The optimization loop is intentionally branch-local and commit-driven. Work happens on `experiment/bidirectional-performance`, and every retained improvement is captured in a commit so the search stays reversible.

## Fixed evaluation loop

Each optimization cycle follows the same loop:

1. **Diverge** — generate performance ideas broadly.
2. **Filter** — convert viable ideas into actionable backlog items with dependencies and expected payoff.
3. **Implement one item** — make the smallest coherent change needed to test the idea.
4. **Measure** — run the fixed benchmark suite and the standard correctness checks.
5. **Decide** — keep the change only if it preserves correctness and improves the chosen speed metric enough to matter.
6. **Record** — update `backlog.json` with the outcome and commit if the change is retained.
7. Repeat step 3 until no ready backlog item remains, then return to step 1.

Stop when new ideas are exhausted or the measured runtime is already good enough that the remaining opportunities are no longer worth the complexity.

## Correctness gates

Every kept change must satisfy all of the following:

1. `dotnet test ScrollShot/ScrollShot.sln`
2. `benchmark` verification replays must succeed on every dataset in `suite.json`
3. For the tracked real dumps, `bidirectional-current` must still match ground truth exactly:
   - `GroundTruthDimensionsMatch = true`
   - `NormalizedDifferenceToGroundTruth = 0`
4. Synthetic fixtures must not materially regress relative to the current accepted baseline

If a change improves speed but breaks any gate, discard it and record the failure in `backlog.json`.

## Primary speed metric

The primary optimization metric is:

- **median `StitchElapsedMilliseconds`** across measured iterations in the fixed benchmark suite

Secondary metrics are:

- median `ReplayElapsedMilliseconds`
- median `FrameLoadElapsedMilliseconds`
- median `ComposeElapsedMilliseconds`

`StitchElapsedMilliseconds` is the main decision metric because it isolates the stitching session more directly than total replay time.

## Fixed benchmark procedure

The benchmark inputs are frozen in two suite files:

- `suite.json` — the **development speed suite** used in the inner loop
- `correctness-suite.json` — the **retention gate** used before keeping a change

The development speed suite keeps the loop short enough to iterate:

- one representative slice of the slow real dump
- deterministic `synthetic-up` and `synthetic-down`
- fixed warmup + measured iteration counts

All performance measurements must run against the **Release** build of `ScrollShot.Tooling`.
Build Release once before a measurement session, then use `--no-build` for benchmark runs so the execution path stays consistent.

Prepare the Release benchmark binary from repo root:

```powershell
dotnet build -c Release ScrollShot/ScrollShot.sln
```

Run the development suite from repo root:

```powershell
dotnet run -c Release --no-build --project ScrollShot/src/ScrollShot.Tooling -- benchmark --suite experiments/bidirectional-performance/suite.json
```

Run the full correctness suite before keeping a change:

```powershell
dotnet run -c Release --no-build --project ScrollShot/src/ScrollShot.Tooling -- benchmark --suite experiments/bidirectional-performance/correctness-suite.json
```

Each suite command will:

1. materialize deterministic synthetic datasets under `generated-datasets/`
2. run one persisted verification replay per dataset
3. run warmup iterations
4. run measured iterations without persisting per-iteration images
5. write `summary.json` under a timestamped directory in `runs/`

## Working files and folders

This program uses the following workspace:

```text
experiments/bidirectional-performance/
├── datasets/
├── program.md
├── suite.json
├── backlog.json
├── correctness-suite.json
├── generated-datasets/   # ignored, deterministic synthetic manifests and frames
└── runs/                 # ignored, benchmark outputs and summaries
```

### File roles

- `program.md` — the operating rules for the optimization program
- `datasets/` — committed representative subsets used to keep the inner-loop suite fast
- `suite.json` — the fast inner-loop speed benchmark
- `correctness-suite.json` — the broader validation benchmark
- `backlog.json` — the current filtered idea list, dependencies, and outcomes
- `generated-datasets/` — regenerated synthetic fixtures used by the suite
- `runs/` — timestamped benchmark results, including verification outputs and `summary.json`

## Backlog policy

Use `backlog.json` as the canonical record for the loop.

Each item should carry:

- stable id
- short title
- status (`idea`, `ready`, `in-progress`, `kept`, `discarded`, `blocked`)
- dependency list
- expected mechanism of improvement
- correctness risk
- measured outcome
- commit hash if retained

Ideas only move to `ready` when they are both understandable and testable in isolation.

## Expected first wave of ideas

The likely early performance candidates are:

1. avoid full-history recomposition on every frame
2. cache per-frame pixel/band derivations across rebuilds
3. reduce repeated zone estimation work when history is unchanged
4. prune impossible overlap directions earlier
5. reuse overlap intermediates across verification passes
6. reduce bitmap cloning and temporary allocations in replay/session rebuild paths

These are examples only; the live backlog remains in `backlog.json`.

## Reassessment after the first retained passes

The first structural optimization wave already landed:

- incremental composition reuse
- adjacent-pair analysis caching
- allocation-free overlap slice comparison
- SIMD absolute-difference scoring
- reuse of cached adjacent-pair overlap results during rebuild/append

On the fixed Release development suite, those five kept passes reduced the stitch-median total from `27449 ms` to `1375 ms`.

That changes the remaining opportunity set:

1. **Still practical next ideas**
   - revisit bounded search windows from recent motion estimates
    - revisit snapshot reuse only if a future design can avoid adding another detector seam for a single-digit gain
2. **Now deprioritized or discarded**
    - freeze zone consensus after stabilization
    - parallelize history rescans
    - the first frame-snapshot cache prototype
    - the coarse fingerprint prepass prototype
    - the direct band-snapshot prototype
    - broader runtime or architecture rewrites before cheaper in-process wins are exhausted

The remaining backlog should therefore bias toward **localized hot-path work inside overlap scoring and search-window reduction**, not more session-level restructuring.

## Commit policy

Use small commits to lock in retained improvements.

- commit only after correctness + benchmark validation
- one logical performance idea per commit
- include the benchmark result summary in the commit message body or adjacent notes when useful
- do not squash failed ideas into kept commits; discarded ideas stay recorded only in `backlog.json`

## Definition of “repo prepared”

The repo is considered ready for this program when:

1. the branch exists
2. the benchmark suite is runnable from source control
3. synthetic fixtures can be regenerated deterministically
4. correctness and speed metrics are emitted automatically
5. the backlog has a structured home

This repository now satisfies those prerequisites.

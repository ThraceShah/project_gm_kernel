# Web progress data exporter

The data line runs real project oracles with the independent managed GM runtime and real PK through the existing ParasolidScriptHost/PskernelSharp bindings. It never redirects PK bindings to GM and never ships SDKs, libraries, schemas or third-party reference documentation. This phase creates data only; portal publishing is separate.

## Reproduce

From the repository root, with .NET SDK 10 and the project's existing local Parasolid installation:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/RefreshWebProgress.cs -- --seconds 600 --case-seconds 180 --run-id my-current-run --output ../temp_docs/web-progress/data
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -- ../temp_docs/web-progress/data
```

Script output paths are relative to `scripts/`, regardless of the caller's current directory. Run IDs are immutable: choose a new ID for every refresh. The default selection is the explicitly marked representative set (17 recipes in registry v5). Use `--list`, `--family blend|boolean`, `--suite <registered-suite>`, `--case <exact-id>[,<exact-id>]` or `--case all`. `--seconds` is the total wall budget, `--case-seconds` bounds each process including build, `--samples 1..31` repeats complete fresh-process inputs, and `--configuration Release` chooses managed build configuration. `--registry <script-relative-json>` selects an explicit registry, and unknown IDs fail before execution. `--publish-data-to <script-relative-directory>` atomically copies this validated run and switches the destination index while preserving history; it accepts JSON/log files only and rejects binaries, symlinks and mutable run replacement. `--package` creates a ZIP of the safe output data directory. No AOT publishing is invoked.

The runner returns 0 when result data is well formed, including genuine failed or timed-out oracles, and 2 for exporter/contract failures. Every original process exit code, timeout flag, argument vector and raw log remains in the case's execution record and evidence files. A timeout kills the owned process tree and retains all previously checkpointed snapshots. Exceeding the total budget before a case starts leaves it `missing`, rather than calling it a timed-out execution.

## Contract and geometry

`web/data/index.json` uses schema version 1 and references `runs/<id>/manifest.json` relative to `data/`. Every mesh/evidence reference in a case is relative to that manifest's directory. The shared manifest and mesh field names are preserved. Additional `tools`, `afterBodies`, exact selected edge tags, generic Boolean tracking records, blend outputs/underlying faces/replaced topology, execution records, parameter SHA-256 and raw compare evidence are additive.

GM snapshots use fresh XT capture with exact source entity/identifier mapping, receive into real PK, then the existing DisplayBridge's PK facet/line rendering. PK snapshots facet the independent reference body directly, without changing its ownership. Positions/normals are actual triangle buffers, indices are face-local zero-based, and edges are real PK polyline samples. `source.tessellator` explicitly names PK even for GM geometry. The chord tolerance is 0.01 model units, independent of modeling API tolerance, session precision and acceptance thresholds. Discrete steps represent real snapshots, with no invented deformations.

Selection uses the oracle's exact selected edge tags and incident support faces. New blend faces use `PK_BODY_fix_blends`' returned face array. Generic Boolean tracking is copied before result memory is freed; deletion also records exact before IDs absent from the result. A selected edge that is deleted keeps its `selected` role and adds `deleted=true` plus the original role; deleted unselected geometry uses the `deleted` role. Independent kernel tag spaces are never equated across GM and PK.

## Acceptance and timing

Each captured operation retains original API errors/results, both full `PK_BODY_check` fault counts/entities, topology counts, strict `ParasolidBodyCompare.AssertEquivalent`, raw global/local results and local difference records with the actual check mask. Only `n_cht_pts` is diagnostic. Fifteen mass values are compared independently at accuracy .99999999 with `abs(GM-PK)/(1+abs(PK)) < 1e-7`; each saves its original values, absolute and normalized residual, threshold and units. The original oracle's broader geometry/lifecycle acceptance and exit code are an additional required gate, not replaced by the web capture checks. Unknown checks are not auto-passed.

GM and PK timings are elapsed wall-clock milliseconds measured immediately around the API, before snapshot export, comparison or logging. Input construction, XT, tessellation, comparison and I/O are outside that interval. The case aggregates captured operation intervals and reports a median over fresh-process samples; warmup is 0. These cold/JIT-sensitive samples are not stable throughput claims. `ratioGmOverPk = gmMs / pkMs` is emitted only when both sides succeed and all required current gates pass; failed correctness retains timings but no numerical ratio. This definition is the inverse of historical evaluation throughput `tPK/tGM` reports.

Provenance pins the actual base commit, dirty state, working diff hash, per-source hashes, parameter hash, PKToy revision, runtime PK version, session precision, SDK/runtime, CPU/architecture/memory, timer frequency, build/configuration and arguments. Initial data was generated while the exporter changes were uncommitted and marks that fact, rather than pretending the base commit alone contains the exporter.

## Coverage and evidence limits

`scripts/web-progress-registry.json` explicitly expands 414 named/default recipes, including the finite 96-case short-edge matrix. The first safe package has five original oracle exits of 0, with four qualified passes and one diagnostic scope mismatch, and marks 205 adapted cases `missing` and 204 registrations `unsupported`. No whole-project completion percentage follows from those five successes. This finite default registry is not an exhaustive cross-product of unbounded transforms, radii, split stations, lifecycle/proof flags, all derivative samples or every unit-test parameter; its scope and limitations travel with every run. Unsupported adapters and later expansions remain visible.

Historical source documents are not rewritten or treated as current passes. `docs/reviews/advanced-modeling/topology-event-framework-execution.md`, `docs/reviews/advanced-modeling/topology-event-framework-acceptance.md` and `docs/reviews/advanced-modeling/topology-event-framework-handoff.md` retain differing historical outcomes. The current main baseline is a1e4500; independent review also reports strict short-edge lifecycle/fresh-cut BODY_check and original inertia/probe failures under additional flags. Those flags require distinct recipes and fresh execution; the default short-edge success does not resolve them.

The first diagnostic refresh `temp_docs/web-progress/data/runs/initial-a1e4500/` is retained locally, including an unsupported tracking=true curved request and its exit 134. The distributed corrected refresh preserves the oracle's curved tracking=false option and exact fixture filter. This option difference is explicitly a harness recipe change, not a kernel algorithm fix or resolution of an underlying geometric defect.

## Automatic refresh and safe static publication

`WatchWebProgress.cs` is an ordinary .NET 10 process intended for the coordinator's existing scheduler or supervised runner. It fingerprints the actual checkout commit, relevant source/fixture changes and registry. It invokes the exporter only when those inputs change, persists state only after successful validation, and does not include generated `web/data/` changes in its dirty fingerprint. It does not fetch, checkout, start agents, install a service or publish a portal.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/WatchWebProgress.cs -- --once --seconds 1200 --case-seconds 300 --output ../temp_docs/web-progress/current-data --publish-data-to ../web/data
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/WatchWebProgress.cs -- --poll-seconds 60 --seconds 1200 --case-seconds 300 --output ../temp_docs/web-progress/current-data --publish-data-to ../web/data
```

Use the first command for a scheduled one-shot; omit `--once` only when the coordinator intentionally supervises a persistent process. No persistent watcher is left running by this task. A source-synchronized production checkout and static destination are coordinator configuration. Per-case execution evidence includes the exact source hashes used at process launch, not only the run's initial checkout hash. Manifests checkpoint the complete registry before launching oracles; the latest index is switched after collection, rather than exposing a partial denominator.

Registry v5 contains 463 finite recipes with 347 snapshot adapters and 116 unsupported registrations, plus a source-coverage audit of 88 related oracle/benchmark/unit-test entry points. The audit retains unexpanded source loops/test data as gaps. The added 42 nested evaluation registrations are deliberately unsupported by the construction exporter: the historical hot-evaluation throughput definition cannot be relabeled as the operation-only web ratio. Nested/dependent fixture chains, fixed-probe structured replay and some specialized planar edit recipes still need adapters; optional CLI Cartesian products remain unbounded. These gaps are not whole-project passes.

Boolean paths whose original options disable tracking retain those options. Their exact before/result IDs establish deletions, but a neutral `unchanged` display role with `roleKnown=false` is not proof that a face was untouched. `semanticCoverage.status=missing` explicitly records missing generic modification tracking. New blend faces always come from the returned blend IDs, not a curvature/color guess. Open surface blends use real auxiliary copies of bounded input support sheets (`[-1,-1,1,1]` UV display clip) and the actual output sheet; the visualization-only input bounds are labeled and do not modify API inputs.

Original oracle acceptance and additional export gates are independent. For example the local block fillet oracle can exit 0 while the stricter web BODY_compare fails; its original exit check stays pass, the case fails the extra gate, and its ratio remains null. Raw differences/masks explain that distinction. Missing downstream registered stages keep null snapshots. Independent review at documentation revision `a3e9a489d17d374bb756fe65e6efb5407f1f896b` concerns implementation `a1e4500`; [that review](https://github.com/ThraceShah/project_gm_kernel/blob/a3e9a489d17d374bb756fe65e6efb5407f1f896b/docs/reviews/advanced-modeling/topology-event-framework-independent-review-a1e4500.md) is an explicitly separate host/source, not this Mac's execution or timing. Fixed replay differences from that source are not fabricated into a Mac run.

A previously generated validated run can be published without executing modeling again:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/PublishWebProgress.cs -- --source ../temp_docs/web-progress/current-data --run-id <exact-run-id> --destination ../web/data
```

The publisher performs the same complete contract validation and allowlist checks. It preserves original run/source metadata and never relabels copied results as a new execution.


Capability and timing labels are explicit: `case.capabilityScope=web-export-adapter`, `adapterStatus=implemented|not-implemented`, `kernelCapability=null` where no general claim is made, and `performance.measurementKind=not-run|single-cold-sample|fresh-process-cold-median`. `correctnessQualified` gates the ratio using current checks; `stableBenchmark=false` prevents interpreting it as steady-state performance certification. A one-time additive metadata finalization was applied to the unpublished 17-case artifact; its original manifest hash, processor hash and unchanged modeling-result signature are retained in `run.provenance.metadataFinalization`. This changes no exit code, check, parameter, snapshot, API time or ratio, and does not rewrite the two previously committed runs.

`PrepareWebProgressSite.cs` keeps UI source and generated static data separate. Local `--no-build` verification exercises complete safe dist/data assembly and history retention; actual package-manager/UI building and portal publication are the deployment owner's responsibility. The supplied systemd unit leaves portalctl to that owner's final drop-in. Generation state, site-build state, and the owner's successful portal-publication state are separate; a portal failure must retry the validated cache even when generation skips an unchanged SHA. Installation and activation on qingyu2 are explicitly handed off, not claimed as completed by the Mac data task.

# Web progress data exporter

The data line runs real project oracles with the independent managed GM runtime and real PK through the existing ParasolidScriptHost/PskernelSharp bindings. It never redirects PK bindings to GM and never ships SDKs, libraries, schemas or third-party reference documentation. This phase creates data only; portal publishing is separate.

## Reproduce

From the repository root, with .NET SDK 10 and the project's existing local Parasolid installation:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/RefreshWebProgress.cs -- --seconds 600 --case-seconds 180 --run-id my-current-run --output ../temp_docs/web-progress/data
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -- ../temp_docs/web-progress/data
```

Script output paths are relative to `scripts/`, regardless of the caller's current directory. Run IDs are immutable: choose a new ID for every refresh. The default selection is five named representatives. Use `--list`, `--family blend|boolean`, `--suite solid-blend|short-edge|planar-boolean|analytic-boolean|regular-curved`, `--case <exact-id>[,<exact-id>]` or `--case all`. `--seconds` is the total wall budget, `--case-seconds` bounds each process including build, `--samples 1..31` repeats complete fresh-process inputs, and `--configuration Release` chooses managed build configuration. `--package` creates a ZIP of the safe output data directory. No AOT publishing is invoked.

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

`scripts/web-progress-registry.json` explicitly expands 414 named/default recipes, including the finite 96-case short-edge matrix. The first safe package executes five representatives and marks 205 adapted cases `missing` and 204 registrations `unsupported`. No whole-project completion percentage follows from those five successes. This finite default registry is not an exhaustive cross-product of unbounded transforms, radii, split stations, lifecycle/proof flags, all derivative samples or every unit-test parameter; its scope and limitations travel with every run. Unsupported adapters and later expansions remain visible.

Historical source documents are not rewritten or treated as current passes. `docs/topology-event-framework-execution.md`, `docs/topology-event-framework-acceptance.md` and `docs/topology-event-framework-handoff.md` retain differing historical outcomes. The current main baseline is a1e4500; independent review also reports strict short-edge lifecycle/fresh-cut BODY_check and original inertia/probe failures under additional flags. Those flags require distinct recipes and fresh execution; the default short-edge success does not resolve them.

The first diagnostic refresh `temp_docs/web-progress/data/runs/initial-a1e4500/` is retained locally, including an unsupported tracking=true curved request and its exit 134. The distributed corrected refresh preserves the oracle's curved tracking=false option and exact fixture filter. This option difference is explicitly a harness recipe change, not a kernel algorithm fix or resolution of an underlying geometric defect.

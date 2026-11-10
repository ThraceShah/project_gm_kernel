# Phase 2 data/export result

The data branch is `web/progress-data`, isolated in the sibling `project_gm_kernel-progress-data` worktree, initially created from fetched `origin/main` at `a1e45006c08491db85f4ef87ced6d236342b15e6`. The original checkout and user files are unchanged. Changes under `src/` are confined to the additive managed DisplayBridge adapter and ownership handling; no kernel algorithm, C export/ABI, acceptance threshold, frontend source/package, portal, Windows host or agent was changed or started. No AOT publish or full unrelated test suite was run.

## Current reproducible execution

Latest public manifest: `web/data/runs/20261110T180347Z-90acd4a9/manifest.json`, referenced by `web/data/index.json`. This run was actually executed on clean source commit `90acd4a9d7e766cdaa09e2ea4fb2cf7bf5d2aa45`; subsequent changes add labels, packaging and deployment handoff tooling, not a fictitious rerun. Its Native source tree is `2b6c6fc01aefcc9f38176ff89a2a0804ddaf6efb`, identical to the Native tree at `a1e4500`. DisplayBridge is independently versioned by the run's commit/source hashes.

Actual host: Apple M4, Arm64, 10 logical processors, 25,769,803,776 bytes memory; macOS 27.2.0; .NET SDK 10.0.400/runtime 10.0.11; PK 38.0.150; PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`; Release, serial oracle processes, warmup 0, one cold process sample. No stable throughput certification is claimed. Collection plus local validated publication took approximately 442.238 seconds from the run timestamp to its persisted watcher completion state, within the 1200-second total/300-second per-case budgets. This outer duration is not used in API ratios.

Exact command used for the fixed-SHA 17-case run:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/WatchWebProgress.cs -- --once --state ../temp_docs/web-progress/final-watch-state.json --seconds 1200 --case-seconds 300 --output ../temp_docs/web-progress/current-data --publish-data-to ../web/data
```

All data path arguments resolve relative to `scripts/`. Use a fresh run ID when invoking RefreshWebProgress directly; use the existing watcher state to skip an unchanged source, or `--force`/a new state path to deliberately rerun. The default selection is the 17 explicitly marked representatives. Direct rerun of only the two strict targets:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/RefreshWebProgress.cs -- --case short-edge.standard-strict-lifecycle,short-edge.transverse-witness-lifecycle --seconds 700 --case-seconds 300 --output ../temp_docs/web-progress/recheck
```

## Coverage and failures

The latest registry has **463 finite recipes**, **347 implemented snapshot adapters**, **116 unimplemented export adapters**, plus a source coverage/gap audit of **88 related entry points**. It does not claim to exhaust unbounded CLI combinations, dependent fixture chains, every inline/member test datum or every possible kernel behavior. Seventeen recipes ran: **14 pass, 3 fail, 330 missing/not run, 116 export-adapter unsupported, 0 timeout**. Unsupported explicitly means the web adapter is not implemented; it does not mean the kernel API rejects that geometry. API errors and their parameters remain independent evidence.

| Executed recipe | Case status | Original exit |
| --- | --- | --- |
| solid-blend.plane-nurbs | pass | 0 |
| short-edge.a1-r0.5-t1e-07-reversefalse-default | pass | 0 |
| blend-chain.chain-2-base-self-source | pass | 0 |
| support-chain.asymmetric43-base | pass | 0 |
| trivalent-corner.base-self | pass | 0 |
| planar-boolean.overlap-two-axes-subtract | pass | 0 |
| analytic-boolean.equal-overlap-subtract | pass | 0 |
| mixed-analytic.basic-subtract | pass | 0 |
| regular-curved.block-subtract | pass | 0 |
| mixed-curved.symmetric-subtract | pass | 0 |
| exterior-contact.0.015625-subtract | pass | 0 |
| surface-blend.open-swapFalse-r1-1 | pass | 0 |
| local-blend.block-single-fillet | fail, added strict compare | 0 |
| short-edge.standard-strict-lifecycle | fail, full check | 134 |
| short-edge.transverse-witness-lifecycle | fail, original inertia | 134 |
| solid-blend.plane-nurbs-modified-support-bore | pass | 0 |
| support-chain.asymmetric43-separate-support | pass | 0 |

The local block fillet's original acceptance check is **pass**; the separate strict web comparison is **fail**, with global/local `21921/21930`, two face pairs and non-whitelisted difference type `21959`. The extra check failure is not represented as an original nonzero process exit. Ratio is null.

The standard strict lifecycle preserves full `PK_BODY_check` bad-loop faults `13826`, entity/support classes and stages for both imported and self paths. Their second cuts were not reached; the manifest keeps two downstream operation placeholders with null meshes and missing checks. Compare success is not allowed to mask full-check failure.

The transverse native-witness lifecycle completes five captured operations and retains the original point gate. Final self first-cut inertia values are GM `0.6693401572705301`, PK `0.6693399562079415`, normalized residual `1.2044436355262826e-7`; second cut GM `0.43597604299725545`, PK `0.43597588122807274`, residual `1.1265452632651103e-7`. Both exceed the unchanged `1e-7` threshold. Symmetric inertia entries remain individual checks. Error-modulus overlap does not pass the point gate. Both strict recipe ratios are null. These are this Mac's actual values, not the independently reviewed Linux measurements.

There are **920 current checks** and **124 unique current meshes / 30,922 actual triangles**, including tool inputs. The three retained public runs together validate **1,293 case rows, 230 unique meshes, 59,278 triangles, errors=[]**. Source geometry remains real PK faceting of independently produced BReps; GM snapshots explicitly say PK tessellator/fresh XT receive. Selection, new blend and known replacements come from tags and API records. Neutral output roles have `roleKnown=false` where modification coverage is unknown. Imported PK fillet lifecycle stages are explicitly distinguished from independent GM self Fix.

Before first distribution, additive capability/timing/role labels were finalized. Original manifest/processor hashes and unchanged modeling-result/geometry-and-ID signatures are retained in `run.provenance.metadataFinalization` and `roleCoverageFinalization`. No coordinate, normal, triangle index, entity ID, selection, check, exit, API time or ratio was changed. The two earlier committed runs were not rewritten. The previous bad observer-placement run, correctly classified as exporter exit 2, remains under ignored `temp_docs/web-progress/current-data/` and was never published; the corrected fixed-SHA execution returned exporter 0 despite genuine case failures.

## Artifacts and validation

Latest run static files: **9,012,461 bytes** before the final small provenance-label refresh; all public history is approximately 17.7 MB across 374 files, with no binary/SDK/schema/document redistribution. The safe latest-run ZIP is `temp_docs/web-progress/distribution/gm-kernel-progress-data.zip`, **1,329,204 bytes**, SHA-256 `49913e4db33beba25dc5b8c93181a399a2ffb8cef04b86dc3c066b35de54565a`. The Git package retains historical runs and source/evidence; the ignored ZIP provides a small safe handoff without putting archives or SDKs in Git.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -- ../web/data
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -- --self-test
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/PublishWebProgress.cs -- --source ../web/data --run-id 20261110T180347Z-90acd4a9 --destination ../temp_docs/web-progress/distribution-copy/data --package ../temp_docs/web-progress/distribution-copy/gm-kernel-progress-data.zip
```

Nine contract/publishing checks passed: missing latest run, traversal, valid real zero values, bad local triangle index, unqualified numeric ratio, missing acceptance incorrectly marked pass, nonnumeric coordinates, atomic static publication and binary-extension rejection. A three-fresh-process planar union run passed with exits `[0,0,0]` and explicit cold-median metadata. After the final observer-only type/role changes, the scoped block-single-fillet oracle compiled and returned 0; all broader original case failures remain recorded independently.

Raw local verification logs are under `temp_docs/web-progress/`: `final-refresh-corrected.log`, `final-watch-restart.log`, `final-validator-tests.log`, `validated-distribution.log`, `metadata-and-samples-check.log`, `final-observer-compile.log`, `finalize-labels.log`, `finalize-roles.log`, `final-label-refresh.log`, `distribution-package.log`. `recovery-first.log`, `recovery-unchanged.log`, and `recovery-regenerated.log` prove a real refresh, an unchanged-source skip, then automatic regeneration after the safe cache was moved aside; old history and evidence were retained. `final-labeled-site.log` proves complete static dist/data assembly with `--no-build`, not a frontend/UI or portal claim.

## Installation handoff and remaining scope

`docs/web-progress-refresh-operations.md` contains the exact dedicated-checkout commands, required environment, budgets, units, triggers, failure/state/history and restart acceptance. Templates are `scripts/systemd/gm-kernel-progress-refresh.service` and `.timer`; `PrepareWebProgressSite.cs` performs the existing UI build and safe dist/data assembly. Generation, site preparation and portal publication states are separate, allowing a failed portal publication to retry an already validated cache without rerunning geometry.

By the coordinator's explicit handoff, Pi dispatch `ctx_f55683eac070` installs/validates the user systemd task on qingyu2 after data/UI integration into main, adds the existing portalctl hook and returns effective unit/enable/restart/publication evidence. This Mac did not install or claim a remote service, publish the portal or start another agent. No further Mac SSH retry is required.

The remaining 330 unrun and 116 adapter gaps remain visible. Full self matrix, fixed 510 pairwise replay data, dependent nested fixture adapters, arbitrary parameter products, complete generic tracking on currently tracking-disabled paths and rigorous kernel branch-certification/algorithm fixes are not claimed complete. The independent review source is linked with its own tested implementation/host and is not relabeled as a Mac run. The coordinator owns integration and deployment; this branch is ready to review and merge.

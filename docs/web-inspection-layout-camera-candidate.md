# Inspection layout and operation camera candidate

Source base: `424e85d6aac32c2afe7cad12cfd04bea4f0037ab`. Branch: `feat/web-inspection-layout-camera`. This is a source candidate for independent review, **not deployed**. No capture, refresh, kernel, acceptance mask, threshold, manifest, historical mesh or UV derivative was changed. The UV worker's changes are not included.

## Fixed small steps

- `f958c40`: one compact shared legend outside both canvases; minimal GM/PK badges with source details in the tooltip; inspection initially collapsed and expanded as a main-area overlay.
- `21fba2b`: geometry-based operation visibility scoring, shared linked framing, readable depth-tested selected-edge strokes, and the focused C# browser probe.
- `6804fa6`: relinking adopts one current GM camera pose rather than exchanging two saved poses; compact mobile entity inspector.
- The subsequent comparison/data version slice displays original evidence and groups original checks; it does not recompute qualification.

## Camera policy

`web/src/utils/cameraUtils.ts` uses selected edges first. Otherwise it uses new blend faces, modified faces, selected faces or deleted faces, in that order. Large support faces do not override those features. Selected edges contribute length-weighted samples and adjacency normals derived from the mesh triangles; result faces contribute area-weighted samples and normals.

The search includes all 26 nonzero directions of the three-axis neighborhood, including underside and side directions, plus geometry-derived outward/normal directions and bounded result-normal candidates. Each pane has at most 48 feature samples and retains every occluding triangle, with an explicit whole-model fallback above 30,000 triangles per input or when reliable role markers are absent. The score combines sampled unobstructed fraction with projected edge length or face area and prioritizes the weaker pane. This is a bounded sampling heuristic, not a proof of global optimality or complete geometric equivalence.

Linked panes use the union of both operation feature bounds and full bounds for one target, direction and distance, sized for the smaller viewport aspect ratio. They do not independently fit their targets. Unlinked panes remain independently controllable. Gesture damping settles on release, and copying a pose clears residual damping. Snapshot changes and playback retain the chosen camera. The `424e85d` run/case/step/stage ownership gate remains in place; an empty or failed destination cannot borrow a previous mesh to consume its initial fit.

Selected edges retain depth occlusion and use a 2.5px screen-space stroke, because native WebGL commonly ignores requested line widths above one pixel. Decorative axes/grid do not write depth over actual features. `web/src/utils/geometryBuilder.ts` was touched only for those selected-edge strokes; its existing UV source validation and line generation are unchanged.

## Original comparison and data provenance

The main summary names `PK_DEBUG_BODY_compare` and reports the original equivalence-check status only when its actual raw comparison evidence is readable. Evidence loading is bound to run/case/path identity and aborted on changes. Missing/unreadable evidence is explicitly unavailable; it is not inferred as a pass.

For the screenshot case, the loaded record remains global `21921`, local `21930`, facePairs `7`, with twelve diagnostic records and the recorded `n_cht_pts` whitelist. The detail panel preserves the original mask: max_diffs=1024, all_tests=false, acc_dev_tests=false, and num/tol_dev/tol/class/sense/missing/non_match tests=true. It displays the full raw global/local differences and related face/entity records without interpreting facePairs as a success count or calling the raw result zero-diff.

All 29 original checks remain: 2 API return records, 2 BODY_check records, 1 main comparison record, 15 mass-property quantities, and 9 auxiliary/topology/fault/exit records. Groups start collapsed and retain original failure values, tiny nonzero differences, thresholds, evidence and process exit information. Record counts are explicitly not capability counts.

The data version overlay reads `run.id`, `run.generatedAt`, `run.commit`, `run.scope` and `run.configuration` from the selected manifest. The tested served data is run `20261110T195008Z-610e3ce8`, collected at `2026-11-10T19:50:08.4502366+00:00`, code `610e3ce`, scope 17 executed / 463 registered representatives and samplesRequested=1. Index generation time and UI build time are not substituted for capture time. The old Current GM label is replaced by published-baseline wording. The historical single-sample 644.54x timing appears only in performance details with an explicit statement that it is not a conclusion about this UI revision or a new optimization.

## Verification and handoff

`pnpm --dir web build` passes TypeScript and Vite. The existing bundle-size advisory remains. No NativeAOT or Parasolid tests were triggered by this UI-only change.

The browser probe is `web/tests/LayoutCamera.cs`, run with .NET 10. Its optional first argument is the read-only published data directory, resolved relative to the probe file. Example from the project root:

```text
dotnet run web/tests/LayoutCamera.cs <relative-path-from-web/tests-to-published-data>
```

The probe serves the candidate build with that data directory, without copying or editing `web/data`. Baseline screenshots were built from the exact `424e85d` web source into `temp_docs/layout-camera/baseline/web` and use the same data. The baseline mode uses that directory and the optional second argument `baseline`.

The focused real-browser checks cover the screenshot Before/After states, two other real operation directions, linked and independent control/relinking, manual orbit across steps/playback, initial/open/close/Escape/focus behavior, resize, 320/390 mobile layouts, internal table scrolling, original 29 checks/tiny values/exit, raw flags, data versions, historical timing, delayed/missing/aborted comparison evidence, and delayed/missing/aborted/recovered mesh ownership and run switching. The existing inspection-only suite separately checks the original V3b ownership reproduction plus tools, selection and missing UV behavior.

Final focused probe: 57/57 checks and captures passed, zero browser errors. The existing inspection-only suite also passes 38/38 checks. Its full historical suite was not run; its obsolete ratio, legend-toggle and header-metadata assertions were updated to match the requested product behavior.

Artifacts are under `temp_docs/layout-camera`: `candidate-report.json`, `baseline-report.json`, `final.log`, `baseline.log` and `ownership-tool-selection.log`. The existing focused suite writes `temp_docs/inspection-remediation.json`. Screenshots include `baseline-before.png`, `baseline-after-focus.png`, `candidate-before.png`, `candidate-after-focus.png`, `candidate-local-blend.block-single-fillet.png`, `candidate-trivalent-corner.base-self.png`, `candidate-drawer.png`, `candidate-data-version.png`, and `candidate-320/390` views and drawers. These screenshots were opened and visually checked.

At the same 1965×1228 browser size, screenshot case and 25% opacity, the 424 baseline canvas was 823×755 (GM) / 822×755 (PK), versus 823×984 / 822×984 in the candidate. The candidate keeps both dimensions unchanged when evidence opens. Each paired entry below is GM / PK:

| Measurement | 424 baseline | Candidate |
| --- | --- | --- |
| Before selected-edge yellow pixels | 196 / 197 | 1499 / 1502 |
| Before sampled visible edge length | 334.4px / 334.4px | 675.4px / 675.4px |
| Before feature vertical extent within canvas | y=556–735 of 755 | y=491.6–492.4 of 984 |
| After sampled visible feature points | 20/48 / 17/48 | 48/48 / 48/48 |
| After projected visible feature area | 1066.1px² / 1083.5px² | 57172.9px² / 57273.1px² |
| After filled green region pixels | 1267 / 1314 | 59392 / 59376 |

The before edge was geometrically visible in the baseline replay but occupied the lower canvas area and had a weak one-pixel stroke; the candidate centers and emphasizes it. After-view geometric occlusion improves as well. The two other real direction checks (`local-blend.block-single-fillet` and `trivalent-corner.base-self`) both give 48/48 visible feature samples in both panes, with actual yellow strokes checked in screenshots; their different camera directions are retained in the JSON report. A Boolean input with no reliable modification markers explicitly uses whole-model fallback.

Pixel counts and feature projection measurements distinguish actual visible geometry from a camera-coordinate change. Baseline feature measurements replay the recorded rounded camera telemetry against the original meshes; candidate telemetry measures the current camera directly. Do not treat the rounded replay as an exact rasterization prediction.

The repository's historical `web/data` stays unchanged and the local build still bundles that historical data. **Do not publish this local dist as a replacement for the served data bundle.** Integration must retain the current published run data and use the fixed source commits serially after independent review. This candidate makes no claim that any kernel result was newly executed or passed.

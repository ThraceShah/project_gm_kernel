# c710 UI review: narrow remediation

Product fix: `2e0c8bd` on `feat/web-inspection-layout-camera`, directly after `c710171bfea776a77217a3c417bc73f7906ac4fd`. The original c710 candidate remains blocked by the independent review at `9ef8ce133bf60cf70f635bd3b016b06a9701e170`; the author tests do not overturn that result. This follow-up is ready for the original reviewer's narrow retest and has **not been deployed**.

Only `web/src/components/MeshViewer.tsx` and `web/src/components/ChecksTable.tsx` change product behavior. Camera, layout, renderer geometry/materials, UV inputs, historical data, acceptance results/masks/thresholds and kernel/capture code are unchanged. The follow-up evidence commit only updates tests and this report.

## B1: actual stroke identity and depth

The original ordinary `Line` pick radius was `(far-near)*0.0005`, approximately **0.25 model units** for this unit-sized part because its far clip is 500. Its hits competed by ray distance with `LineSegments2` hits using a **2.5 CSS-pixel** rendered stroke. An ordinary edge well outside the clicked yellow stroke could therefore win because it was closer to the camera.

Picking now uses a bounded world-space broad phase derived from projected pixel size and current geometry bounds, then projects each edge hit back into the same canvas CSS coordinates as the pointer. Ordinary-edge proximity is capped at 3 CSS pixels. Stroke hits take precedence over proximity-only hits. `LineSegments2` gets a 1.5px total-width allowance for antialiased raster pixels. Opaque foreground surfaces reject rear-edge hits; a 1.5px depth allowance covers stroke boundary rasterization. Nearly coplanar edge ties follow render order and screen distance. Transparent context that does not write depth does not suppress a visible edge. No case ID, tag or screen coordinate is special-cased in product code.

The same final reviewer probe, copied byte-for-byte from `9ef8ce1`, was run against both the preserved c710 build and the fixed build. `docs/reviews/web/ui-c710/Review.cs` SHA256 is `8f05cb5fbdbb06d10e5894c52527be8c384eac2495305535eda6cb42cc63dace`. Its embedded `candidate` JSON label remains c710 on **both** outputs because the original probe was not edited to relabel the implementation; the actual tested product source for after results is **2e0c8bd**.

| Original clicked pixel / control | c710 | Fixed |
| --- | --- | --- |
| GM original right-end-area yellow pixel (1075,648) | gm:edge:115, unchanged | gm:edge:106, selected |
| PK original right-end-area yellow pixel (1898.5,648) | pk:edge:77, unchanged | pk:edge:107, selected |
| GM 25% yellow pixel center (545.5,649.5) | gm:edge:104, unchanged | gm:edge:107, selected |
| PK 25% yellow pixel center (1369,648.5) | pk:edge:166, unchanged | pk:edge:148, selected |
| GM / PK 50% and 75% yellow controls | selected edges | same correct selected edges |

The extra focused mode in `web/tests/LayoutCamera.cs` derives endpoints and 25/50/75% points from actual yellow canvas pixels, then checks the specific corresponding source edge, including both endpoints. All 20 clicks pass across both panes at desktop 1965×1228 and mobile 390×844. It also uses independent triangle rays over the original mesh to find real foreground/transparent controls: unchanged faces gm:face:48 / pk:face:180 remain selectable; ordinary rear edges gm:edge:102 / pk:edge:181 remain pickable through transparent context; opaque support faces gm:face:66 / pk:face:111 correctly block rear edges gm:edge:104 / pk:edge:166. These controls do not rebuild or alter the captured geometry. Their oracle camera is replayed from rounded telemetry, so they are finite regression samples, not an all-device or all-pose proof.

The original selection/tool regression used a fixed central search grid in the operation-facing after view. With the erroneous large cone removed, that grid legitimately hits modified faces rather than distant unchanged edges. The test now requests the standard isometric view before searching for an actual unchanged entity, then verifies that hiding unchanged geometry clears that real selection. It passes with gm:face:50; no picking assertion was weakened. Existing tool counts/toggles, camera ownership, linkage, steps/playback and unavailable UV checks still pass.

## B2: semantic mapping to the c9b88 audit

The old five groups preserved every row but combined topology, operation faults/output count and whole-process exit into one auxiliary group. Those are different obligations. The new six groups exactly match each member ID of `coverageGroups` in the original `c9b88af` audit, rather than merely adding another heading:

| Audit group | UI group | Original rows |
| --- | --- | --- |
| primary | PK_DEBUG_BODY_compare + type / regions | 0-equivalence (1) |
| overlap-counts | 拓扑重复计数 | 0-count-0 through 0-count-4 (5) |
| operation-contract | API return / blend fault / 输出体数量 | gm-0-api, pk-0-api, both blend-fault rows, 0-body-count (5) |
| body-validity | BODY_check 有效性 | both original body-check rows (2) |
| mass | 物性量 | 0-mass-0 through 0-mass-14 (15) |
| scenario | 原进程 / 场景 exit | oracle-exit (1) |

The original 29 rows and every status/value/delta/threshold/evidence link remain unchanged and appear once. Missing/unknown categories in other cases are not forced into a fake six-group count; an explicit other-record group remains available. Primary raw comparison presentation and the default collapsed groups remain as accepted by the independent review.

## Fixed inputs, actual exits and evidence

Input: the independent review's immutable data copy, run `20261110T195008Z-610e3ce8`, case `support-chain.asymmetric43-base`, Before, default 25% opacity and linked cameras. Index SHA256 remains `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`. The historical repository `web/data` was not updated. C710 source and its compiled bundle are preserved in `temp_docs/ui-c710-review/web` and `temp_docs/ui-c710-review/c710-dist`; original reviewer inputs/logs/reports are retained in `temp_docs/ui-c710-review/original`.

| Actual command / input | Exit and result |
| --- | --- |
| `pnpm --dir web build` | 0; existing Vite chunk-size advisory only |
| final original `Review.cs`, c710 build | 1; 25/28 pass, same 3 blockers |
| final original `Review.cs -- --picking-only`, c710 build | 1; 6/8 pass, same two quarter-stroke failures |
| final original `Review.cs`, fixed build | 0; 28/28 pass |
| final original `Review.cs -- --picking-only`, fixed build | 0; 8/8 pass |
| `dotnet run web/tests/LayoutCamera.cs ../../temp_docs/ui-c710-review/data picking` | 0; 30/30 focused checks/captures |
| `node web/tests/smoke.mjs --inspection-only` | 0; 38/38 historical ownership/tool/selection regressions |

All browser runs have zero page errors. The 38 historical regression checks use the unchanged tracked historical run, and do not replace the 610 screenshot-input tests. No complete historical suite, new PK execution, NativeAOT build or live publication was performed.

Permanent raw evidence in this directory: `before-browser-report.json`, `before-picking-report.json`, `after-browser-report.json`, `after-picking-report.json`, `extended-picking-report.json`, and the before/after original probe logs. Original probe screenshots are preserved under `temp_docs/ui-c710-review/before-final-probe` and `temp_docs/ui-c710-review/after`; default Before geometry counts remain 10 faces / 20 edges / 102 triangles on both sides, with the same camera pose/target and yellow pixel counts 1499 / 1502. The correction changes which entity a click picks, not the camera or the visible geometry.

The screenshots `picked-gm-quarter.png` and `picked-pk-endpoint.png` show the corrected inspector identities after actual yellow-pixel clicks. Both were opened and checked, as was `temp_docs/ui-c710-review/picking/selected-390.png`. For the original reviewer, retest only the two failed quarter points plus the existing correct controls, the original right-end points, and the six membership groups / 29 unchanged rows against the fixed SHA; the broader previously accepted review scope remains bounded as before.

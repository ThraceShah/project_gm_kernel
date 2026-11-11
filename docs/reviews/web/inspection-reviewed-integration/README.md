# Fixed layout + real U/V integration, prepared only

Branch: `feat/web-inspection-reviewed-integration`. Real merge `5a60104f358b21fcb2c978ba9d250407178ce61e` has these exact parents:

- U/V: `e8dd252b2a1baa848c2c59ad9c9b43dd9dc845ba`. Production fix `1eb9308bc400aa7b4cfac44222fc9ec66738a4fb` was independently accepted in `39cbf9477262a04e7c092cfdad760feee39134bb`; e8dd adds tests/docs only.
- Layout: `53f9871e55574ec66a96f12904293515ce40c23c`. Original reviewer accepted the fixed viewing scope in `43cb83da1b721498b4a8d747e33b206829ac513c`. Original c710 rejection remains intact.

The only post-merge production correction is `6a433222da0775dcb5524d7751d94e8f096fe797`: Header and data/performance drawer now describe the U/V source as a render-only derived package, label its package-generation time correctly, and never call it a published benchmark baseline or performance sample. Historical run wording and original collection time remain unchanged. Subsequent commits add preparation/probes/evidence only; the built production web tree remains identical to 6a43322. The original U/V branch still ends at e8dd252; no other worktree was edited.

There were no textual merge conflicts. Overlapping App/Header/Viewer/geometry code was inspected: App retains full selection context and the 424 geometry ownership gate while adding layout's comparison/drawer state; MeshViewer retains scored shared framing, manual step/playback ownership, raster-consistent selected-stroke picking, and real U/V statistics/provenance; geometryBuilder retains both LineSegments2 selected edges and distinct true U/V colors. The trusted descriptor, full-context cache, types, all 15 U/V assets and original history data are byte-identical to e8dd. No camera, legend or picking redesign was added during integration.

## Concrete isolated artifact

Prepared directory: `temp_docs/reviewed-inspection/bundle-final`. Full 625-file inventory, byte counts, component and SHA256 for every file: `docs/reviews/web/inspection-reviewed-integration/bundle-inventory.json`.

| Component | Files | Bytes |
|---|---:|---:|
| UI only, excluding all dist/data | 3 | 905462 |
| Exact retained historical data | 607 | 27142034 |
| Independent render-only U/V package | 15 | 167168 |
| Complete prepared artifact | 625 | **28214664** |
| 30 MiB budget | | 31457280 |
| 32 MiB hard limit | | 33554432 |

No data was pruned. The build's historical bundled data was excluded from assembly; the dispatched read-only published-data copy was copied in full, then only the separate `data/display/real-uv/` namespace was added. Source: `../gm-web-focus-layout/temp_docs/ui-c710-review/data`, with exact root index SHA **`117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`**. All 607 input files were hashed and compared byte-for-byte with the final artifact, and source files were rehashed after assembly. The index is unchanged, including its existing generatedAt; run `20261110T195008Z-610e3ce8`, original code 610e3ce and original collection/configuration/single-cold-sample evidence remain intact. This is a copied historical input, not a fresh kernel run or a new live-host measurement.

Two hash conventions are explicitly recorded, to avoid passing the wrong value to the owned release tool:

| Identity | SHA256 |
|---|---|
| **Owned tool UI hash / --dist-hash** | **`e0429cf28007de740018ebfc1898a630c1535be0bcdd668e4ed38b415a811ee4`** |
| Owned tool full prepared bundle hash | `5f43710720d36735d1972666c4f80013fa00b8d3d58fc0a623b4ad2601c3c7e5` |
| Owned tool prepared data hash, including U/V | `c83ba9269e589bd2d335aee35ea068e2b3fce0e3a8be564278f0f9b0d8b35e1f` |
| Owned tool retained historical-data hash | `fbb68a2aacd7eebb910f63a51a23954eec340e9bc3960e000448c699565d7390` |
| Inventory canonical full tree hash | `5995e32f00587a76b0b2c3f3741785cb6f0b67959d0ab4833a05d710a197abf7` |

Owned hashes use the accepted tool's `ReviewedWebProgressRelease.TreeHash`: SHA256 over sorted relative-path + file-SHA256 UTF8 concatenation. The separate inventory canonical hash includes path NUL decimal-size NUL SHA256 LF; it is not the tool's --dist-hash. Individual built assets are JS `b12158b6efec88f08d2c3ba6738bedaced8fe588d4d23473ac886a2ed7aa004f` (873420 bytes), CSS `2ab99dacb75729be431d09937db1f14d7e727393e7006f5b56d5e6b4d5589b08` (31187), HTML `5d0f9a698e38660d7c68161d89c125392f8c438613e9dfa33fae2f537eedbe0e` (855).

`scripts/PrepareReviewedInspectionBundle.cs` is a **prepare-only author helper**, explicitly marked not independently reviewed. It cannot invoke portalctl/systemctl or prune data, requires a fresh output, verifies the index and all original bytes, and records the complete inventory. Its final run exits 0. An initial inventory serialization attempt failed because reflection serialization is disabled; that log and copied tree remain in `temp_docs/reviewed-inspection/prepare-first.log` and `bundle/`. The helper was corrected to JsonNode serialization; final copy bytes were independently recomputed and compared with the first layout-tested tree, proving both payloads identical. No AOT compilation was run.

## Browser and build evidence

One TypeScript/Vite production build, exit **0**: `temp_docs/reviewed-inspection/build.log`; existing Vite large-chunk warning remains. Existing .NET dependency warnings remain in probe logs.

| Isolated check against actual merged production | Exit / result | Durable evidence |
|---|---|---|
| Original fixed layout `LayoutCamera.cs`, same 117bb data | **0**, 57/57 | `layout-report.json` |
| Original fixed layout `LayoutCamera.cs ... picking` | **0**, 30/30 | `picking-report.json` |
| U/V full-selection cold/warm probe | **0**, 14/14 scenarios | `uv-context-summary.json` |
| Additional prepared-artifact intersection probe | **0**, 34/34 | `fusion-report.json` |

Commands use `MSBUILDDISABLENODEREUSE=1 dotnet run` with `web/tests/LayoutCamera.cs`, `scripts/VerifyWebUvSelection.cs`, and `scripts/VerifyReviewedInspectionIntegration.cs`; the latter defaults to the final prepared artifact. Original layout probes read the same final data bytes, and their tested initial prepared tree was verified identical to the final tree. The U/V context probe reads the built production UI/U/V files, which match the final artifact exactly. Actual product source is 6a43322; inherited c710-labelled historical evidence and the original blocked repro are not represented as integration runs.

The layout run covers external one-row shared legend, collapsed overlay drawer, desktop/320/390 space, close/Escape/Tab/focus and internal scroll, linked scored framing, manual orbit/step/playback/relink, delayed/missing/abort/recovery/run ownership, original comparison mask/global/local/diagnostics, 29 records and tiny values, collection/code/scope, and the historical ratio only in qualified details. Picking covers both panes' actual yellow endpoints and 25/50/75% pixels at desktop and mobile, specific selected edge identity, ordinary nearest faces, rear ordinary edges through transparent context, and opaque foreground occlusion. No coordinate/tag special case was introduced in production.

The additional artifact probe individually visits **all 17** original drawable legacy cases and compares their actual Before triangle totals (including tools) with untouched source meshes, while requiring U/V counts 0. It verifies six groups' exact original IDs against the independent c9b88 audit, with membership sizes 1/5/5/2/15/1 = 29. It verifies both genuine U/V Before/After meshes (96/96 and 220/130 triangles), U5/V5 per pane, and source snapshot/stage from the bound receipt summary. The accepted 14-scenario probe additionally verifies cold and already-warmed wrong stage/case/step/kernel/package/mapping rejection; Before never remains under an After selection.

Actual merged-camera U/V on/off pixel differences: GM 6648 changed pixels including cyan 344 and purple 730; PK 6613 including cyan 303 and purple 705. Camera pose remains unchanged while drawn lines go to zero and restore to U5/V5. At 320px the U/V panes measure 189/188px high, at 390px 205/204px; overlay opening does not shrink either canvas, and Escape restores the invoking button's focus. Header/drawer explicitly distinguish U/V package generation from historical capture time and preserve its null ratio/no-measurement status. All UV mesh/ISO/safe-summary links and reverse links return 200 in the prepared tree; no raw XT/vendor library/private path is included. All runs have zero uncaught page errors.

The first supplementary probe read counts across an async viewport replacement and queried a drawer before it mounted; the second compared focus with a nested label span rather than its button. Their diagnostic reports/logs are retained as `fusion-first-*` / `fusion-second-*` under `temp_docs/reviewed-inspection/`. Only readiness/target selection in that author probe changed; production and expected data/geometry requirements did not. Final supplementary exit is 0. Opened and inspected: `combined-uv-after.png`, `combined-uv-320.png`, `combined-uv-390.png` in that directory, plus layout/picking captures under `temp_docs/layout-camera/` and `temp_docs/ui-c710-review/picking/`.

## Release handoff, no deployment

Both parent acceptance identities are fixed above. The combined candidate still requires the coordinator's final independent fusion gate; parent acceptance is not asserted as combined-release acceptance. Original f3 and c710 rejection evidence remains intact. No PK/export/Capture/Refresh, Native/AOT, performance/platform suite, service/unit/portal/host proxy/main action occurred.

The independently accepted owned upgrade tool is fixed at `1b935972eff529e13b46cb9b08ad9550e0a6aeb5`, review `d32e862dc947702d4655912efc916d5c035df04b`. It was only inspected, not executed here. Its UI freeze excludes **all dist/data**, so freezing the three UI files alone does not carry the 15 U/V assets. The eventual approved data input or equivalent persistent display namespace must include the prepared `data/display/real-uv/` alongside the unchanged historical 607 files/root index; do not feed the older tracked dist/data or silently drop the U/V package. The full prepared artifact and inventory make that exact composition reviewable without changing any existing result bytes. If the deployment owner cannot preserve that independent namespace, a layout-only release must omit the new U/V entry rather than exposing a broken example; no such split or tool modification was performed in this task.

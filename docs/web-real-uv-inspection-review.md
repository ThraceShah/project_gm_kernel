# Real U/V inspection candidate

Code/data candidate: `b5858a763b4930f2f671323e74b75a55778130bf`, branch `feat/web-real-uv-inspection`, based exclusively on accepted UI `424e85d6aac32c2afe7cad12cfd04bea4f0037ab`. This report is a later documentation-only commit. The candidate is pushed for independent review and has **not been deployed**. No portal, service, unit, main branch, capture producer, kernel API, or acceptance ledger was changed.

The explicit Header link opens `?display=real-uv`. Without that query, the original index/latest run and all historical run bytes remain unchanged. The independent display manifest has one diagnostic case, empty acceptance checks, null timings/ratio, and `correctnessQualified=false`; it does not replace the live17 default or claim a new modeling pass.

## Source and identity

Read-only source run: `../gm-operation-recording-r2/temp_docs/operation-receipt-curved-eecab/data/runs/r2-curved-receipt-eecab6d`. Case: `solid-blend.plane-nurbs`, `sample-0`, `operation-1`. Producer: clean `eecab6d51cae79a687fa459af512adc3e03895f5`, `dirtyAtStart=false`. The consumer reads **receipt.kernel and receipt.stage**, never derives either from snapshot numbering.

The exporter verifies original complete mesh and XT bytes against each receipt, the original mesh's relative receipt link, all unique receipt/mesh/received-XT face identifiers, execution exit/timedOut, matching producer commit/run, and capture/helper hashes against both execution and manifest sourceHashes and immutable producer Git blobs. It then loads that exact XT with real PK. The GM source remains the producer's actual GM-exported XT; receiving it in PK provides display evaluation only. No replacement PK body is constructed.

- `scripts/WebProgressCapture.cs`: `da417ff2733163be641b97416f48014ebaca73e28f628e50203b3fe69735eaa8`.
- `scripts/WebProgressDisplayReceipts.cs`: `76e44ab354d31a9f80fcc28ac2afb2f5fa4f54eee044b8f64b0a94bd939e4204`.
- Original execution SHA: `afab2ce66467fb9690d84e02a5e7b2d82cd3dbc43f033c84b50a69046b72f279`.
- `parameterHash` remains **null / unverified** in the new display source. Actual source receipts contain a recorded value; the ISO retains it as `recordedParameterHash`, explicitly unverified, without promoting it to a verified parameter SHA.

| Receipt snapshot | Actual kernel/stage | Original mesh SHA256 | Original XT SHA256 |
|---|---|---|---|
| snapshot-0 | PK before | `555d53765eb2ef485d38c3e8da70b08f66b6e808da1ece1fc9a11b9b21d7b3b3` | `b8d68482be02624d762960c7b27117240821d08e084f9e3ea93e4d46ad1ece9b` |
| snapshot-1 | PK after | `1024e741e3f4c4daadbef671e43c3aa78a05966861e80a5d9cea41716f36baed` | `1f37d48acee672430a170bb650e27ac3f288da96d8fb837d9bc328a605722146` |
| snapshot-2 | GM before | `f19b18867e17ae47fac8b2b2a0489b27f3049da6c3f03d9e87fcd5bbd0d06b22` | `4045d6ddfa7b589597afbde6bc67dff8a667d601577ed0c99f8778a15f037dc8` |
| snapshot-3 | GM after | `ee8ef5dbb75f97a7942c5647e315b942743550c71994ec62f0992fbcc3f495f1` | `7e215d8c01fcac57e5f7d08f7e8abc374dc536e3267a6118609ce7f576bf56b3` |

Original receipt SHAs respectively: `84748e9d67c7a55211a4ed7bfe66b4f1b69451e39744ff5ab5d542d08696eec6`, `8c4ebb23c0b4e158bea00f91e2c96e09243e3d20fa157ac3c656006f6775eaf8`, `46078f12262c052b9385deaa9e4dfacafa0ed747cc797131a1d1dc6fa6d4d152`, `78c839d3446e9cbbea3c34a2a902bed90a23f06738c3a1e0f15d0382e2ca3861`.

The public package lives in `web/data/display/real-uv/`. Its `display-manifest.json` binds the complete original mesh SHA, original XT SHA, original receipt SHA, execution SHA, producer identity, and generated ISO SHA at source/snapshot level. Browser association occurs only in memory after matching exact bytes and unique faces; no `isoRef` is written into original mesh bytes. Mesh files are byte-identical copies. Their receipt references resolve to explicitly typed **render-only-receipt-summary** files carrying the original receipt hash; these summaries are not represented as original receipts. They contain no XT path or private path. All public mesh, ISO, and summary references resolve without publishing raw XT.

## Real API and support boundary

Read the Parasolid research skill and actually called MCP `parasolid_get_function_detail` for `PK_FACE_is_uvbox`, `PK_FACE_contains_vectors`, `PK_SURF_eval`, and the bounded mesh-distance API `PK_TOPOL_range_vector`; `PK_ERROR_not_on_surface` was checked with `parasolid_lookup_error`. Query endpoint: `https://qingyu1-linux.tail77e06.ts.net/apps/parasolid-query/mcp`. Dynamic execution used same-platform Linux PK **38.0.150**, PskernelSharp pinned at `d820719b6d8510d623972ab9d8efc65edbc312f9`. Unified private-env ensure/check with native profile returned `ENV_READY` (exit 0); no vendor binaries or credentials are in the commit.

`PK_FACE_is_uvbox` confirms the **trimmed face is a parametric rectangle**; it is not a general trim test. Only such nonplanar faces are exported. Every double-precision UV endpoint and adaptive chord probe is also checked through `PK_FACE_contains_vectors` and evaluated by `PK_SURF_eval`. U fixes u and varies v; V fixes v and varies u. Both coordinates and parameter evidence retain double serialization precision. There is no mesh-edge/outline fallback, fixed face identifier, forced cross-kernel face match, or Round6 conversion.

| Snapshot | Verified mesh face / persistent identifier | U / V | Omitted faces |
|---|---|---|---|
| GM before | gm:face:38 / 75 (observed, not hardcoded) | 5 / 5 | 5 |
| PK before | pk:face:111 / 36 | 5 / 5 | 5 |
| GM after | gm:face:193 / 115 | 5 / 5 | 6 |
| PK after | pk:face:146 / 98 | 5 / 5 | 6 |

After faces are the operation's genuine new curved transition faces. Their identifiers are independent. The package records each face's trim rectangle, surface class, constant parameter, varying range, UV samples, XYZ polylines, chord probes/error, and source identity. Line display chord tolerance is 0.001, using quarter/midpoint sampled adaptive deviation with initial subdivisions; **no global analytic error bound is claimed**. Mesh-to-bounded-face vertex checks use the producer's existing 0.01 display tolerance plus float32 conversion allowance, separate from the line error and all modeling acceptance tolerances. Recorded maximum supported-face mesh distances are about 0.0001327873 (GM after) and 0.0028079770 (PK after). The original float mesh must not be treated as exact trimmed-surface samples.

Planes are omitted. Other nonrectangular trims and unknown sources remain explicitly unsupported/unavailable; the UI states the number of omitted faces. This is a finite one-case rectangular-trim display closure, not arbitrary trimmed-surface support or a geometry equivalence proof.

## Verification

- Fresh package generation using `MSBUILDDISABLENODEREUSE=1 dotnet run scripts/PrepareWebUvDisplay.cs -- --source-run ../../gm-operation-recording-r2/temp_docs/operation-receipt-curved-eecab/data/runs/r2-curved-receipt-eecab6d`: exit 0. All four real receive/evaluate exports returned 0. Evidence: `temp_docs/real-uv/package-final.log`; exporter guards reject every nonzero PK return. No Capture or Refresh operation was run.
- `MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyWebUvDisplay.cs -- --source-run ../../gm-operation-recording-r2/temp_docs/operation-receipt-curved-eecab/data/runs/r2-curved-receipt-eecab6d`: exit 0. Four original-byte bindings pass; altered mesh SHA, execution source hash, and receipt face identifier each reject with child exit 1 and no ISO output. Mutations were confined to new ignored temporary copies. Evidence: `temp_docs/real-uv/source-negative-final.log`.
- TypeScript check and production build passed. Two frontend builds were needed: the first exposed an added-row narrow-screen regression, then the Header entry was moved into its existing subtitle and the corrected build passed. Logs: `temp_docs/real-uv/build.log`, `temp_docs/real-uv/build-final.log`. Later package-only link changes were copied by the same data-copy operation into the built review artifact, without rebuilding code. Existing large-bundle and NuGet dependency warnings remain; no NativeAOT compile/publish was performed.
- Unchanged `node web/tests/smoke.mjs --inspection-only`: final exit 0, **38/38**, no uncaught errors. Covers original V3b destination camera ownership/delayed loading/reverse and failure recovery, reset/orbit/snapshot/playback, actual picking and selection clearing, tool geometry/pixels/camera, unavailable old-source lines, and narrow-screen layout. Final evidence: `temp_docs/real-uv/inspection-final.log`, `temp_docs/inspection-remediation.json`. Initial mobile failure is retained in `temp_docs/real-uv/inspection.log`; final 320px panes measure 93/92px and pass the original suite.
- Actual Chromium final display checks: **16 passed, zero uncaught page errors**, recorded in `temp_docs/real-uv/browser-evidence.json`. Before: 96 triangles per side and U5/V5. After: GM 220 triangles, PK 130, each with one actual newBlend face and U5/V5; unchanged context opacity remains 0.25. Source entry, stage/snapshot provenance, and null performance are visible. Mobile 390px panes measure 124/123px with U5/V5 and no horizontal overflow.
- Actual visible after-pose canvas counts on/off: GM cyan 203→143 and purple 295→0; PK cyan 283→171 and purple 298→0. Toggle removes both directions and restoration returns U5/V5. An exploratory >100 cyan-pixel threshold was unmet for GM (actual delta 60); that measurement and the initial failed exploratory assertion are retained separately, not represented as a production geometry failure or hidden. The paired positive pixel change plus drawn-direction counts verify real lines, not a button flag.
- Browser request controls: mesh-byte tampering rejects the affected pane's geometry; ISO-byte tampering leaves its verified mesh but removes all parameter lines. Both GM and PK were exercised against the final package. Aborting the actual GM after mesh request clears old geometry, while the unaffected PK pane still has U5/V5. All 16 public summary/mesh/ISO references returned HTTP 200. No raw XT asset dependency exists.
- Screenshots were opened and inspected: `temp_docs/real-uv/final-after-visible-dual.png`, `temp_docs/real-uv/final-after-gm.png`, `temp_docs/real-uv/final-after-pk.png`, `temp_docs/real-uv/before-dual.png`, `temp_docs/real-uv/mobile-real-uv.png`. The selected Front view followed by a downward orbit exposes both bounded transition patches, with both line directions crossing inside each patch; default camera framing remains the accepted behavior. Error examples: `temp_docs/real-uv/mesh-hash.png`, `temp_docs/real-uv/iso-hash.png`, `temp_docs/real-uv/load-failure.png`.

Exact local review-artifact sizes: unchanged historical data 17,751,317 bytes, new display package **167,168 bytes**, total `web/dist` **18,781,760 bytes** (about 17.91 MiB), below 30 MiB budget and 32 MiB hard limit. These are the actual UI424 checkout contents, not a claim about replacing the currently deployed package. No old evidence was deleted to meet budget. `git diff 424e85d -- web/data/index.json web/data/runs scripts/WebProgressCapture.cs scripts/RefreshWebProgress.cs` is empty.

Remaining work is independent review and any later explicitly authorized deployment. General trim support, complete host/ledger integration, new modeling acceptance/performance runs, AOT and other-platform validation are outside this candidate and were not claimed.

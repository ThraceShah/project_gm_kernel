# Real U/V selection-context P1 remediation

Minimal implementation: `1eb9308bc400aa7b4cfac44222fc9ec66738a4fb` on `feat/web-real-uv-inspection`; repro/test-only follow-up: `df64688`. The rejected original candidate `f3fb9432ae97609f34391cffef819f62a9dd161d` and independent P1 report at `ThraceShah/gm-web-real-uv-f3-review@67b5a4edbc23582c23d75f4e8ecce9b14265d42a` remain intact. The coordinator relayed the original reviewer's **narrow P1 acceptance of fixed 1eb**, recorded at `39cbf9477262a04e7c092cfdad760feee39134bb`; this does not accept the original f3 or any combined layout/release. No deployment or layout integration occurred.

## Original inputs and failure preserved

Read the original report at `../gm-web-real-uv-review/docs/reviews/web-real-uv-f3-review.md` and its exact repro `../gm-web-real-uv-review/scripts/ReviewWebRealUvF3Consumer.cs` before changing code. The copied, unmodified repro is now `scripts/ReviewWebRealUvF3Consumer.cs`; both SHA256 values are `4278d8ef82e0fd565c6554c1cb6eb715140c0f14867074ff45d471555bd5ebb4`. The independent original consumer JSON SHA is `2a79b46e297a8b8bfee0bb66684cf36d5d74792bc20cc413d1811d04b4a61865`.

The exact repro's logical inputs are unchanged:

1. Unaltered explicit `?display=real-uv` source, initial Before.
2. Route only the case manifest with `cases[0].id = 'review.wrong-case'` and `cases[0].steps[0].id = 'operation-999'`.
3. Route only the case manifest with `cases[0].steps[0].gm.after = cases[0].steps[0].gm.before`, initially load Before, then click After.

Original mesh/ISO/display-manifest bytes and asset SHA fields are not changed by these inputs. Before editing, the original production artifact reproduced exit **1**: input 1 passed, inputs 2/3 were wrongly accepted with real Before 96-triangle U5/V5 geometry. Preserved evidence: `temp_docs/real-uv-context/f3-original-repro.log` and `temp_docs/real-uv-context/f3-failure/consumer-counterexamples.json`; original independent evidence remains in the fixed review commit. The durable compact observations and all 15 package asset hashes are in `docs/web-real-uv-context-validation.json`.

Key original inputs, unchanged after the fix:

| Asset | SHA256 |
|---|---|
| Case manifest | `4727934d55dfc32a58f78f110299c37198a98ddb441e1365a007917d8330f913` |
| Display manifest | `0875f9b8208db2d248e3a4fc912b0f5a077d75deaba8b3d1553b840dfb732714` |
| GM before mesh | `f19b18867e17ae47fac8b2b2a0489b27f3049da6c3f03d9e87fcd5bbd0d06b22` |
| GM after mesh | `ee8ef5dbb75f97a7942c5647e315b942743550c71994ec62f0992fbcc3f495f1` |
| PK before mesh | `555d53765eb2ef485d38c3e8da70b08f66b6e808da1ece1fc9a11b9b21d7b3b3` |
| PK after mesh | `1024e741e3f4c4daadbef671e43c3aa78a05966861e80a5d9cea41716f36baed` |
| GM before ISO | `6c36c4d940cf7d6178afe5f550a51f26561b27e4a2c6bb2ec7d537eaa685ec13` |
| GM after ISO | `d12092402e590ce680233bcddb1bb1fa528d99b44df5a3eeae59becb8b6a82a5` |
| PK before ISO | `94cbcddfac053c4971a4dffa77ece2daeccecf0ddc95734620d3f3c3eee5a8fb` |
| PK after ISO | `9fa3bb4313eb0cf98e9c8aadf54610697197de6a0c7d3c1d4b1f3bd231dbe6a3` |

## Independent selection binding

App now passes the actual selected run ID, case ID, step ID, independently fixed pane kernel, and user-selected stage through `fetchStepMeshes` → `fetchMeshData` → attachment. The input path is a claim to check, not a source of the expected stage. The stage UI is never rewritten to fit the asset.

`web/src/api/realUvDisplay.ts` is a reviewed, code-owned descriptor for this one finite package. It separately names the display run/case/step, source run/case/step/sample/producer, the four paths and receipt snapshot mappings, and the exact existing display-manifest SHA. Display and source case/step names are explicit fields, so a future alias would require a reviewed descriptor change; no alias is inferred from the wrong item or manifest. Both current names map to `solid-blend.plane-nurbs` / `operation-1`.

Before any path dereference or cache access, the loader checks the selected run/case/step/kernel/stage and exact path against that descriptor. During attachment it verifies the pinned display-manifest bytes, then verifies the item against the descriptor and the current independent pane/stage/snapshot. Existing mesh/ISO/face/source hash checks remain. Unknown mapping or context returns `unavailable`, null mesh data, and clears the affected pane; no verified Before can remain under After. The finite package does not define tool mappings, so injected tool references are rejected.

The mesh cache key now includes fixture mode, directory, path, run, case, step, kernel, and stage. A context mismatch is rejected **before** cache lookup. A cache hit reuses an attachment verified for that complete selection under the code-owned immutable package descriptor; it cannot upgrade a path-only Before cache entry into After. Legacy runs remain outside this descriptor and render their meshes normally; lack of matching receipt evidence still yields unavailable parameter lines.

## Verification and limits

- One frontend production build (`pnpm --dir web build`): exit 0; existing large chunk warning remains. Log: `temp_docs/real-uv-context/build.log`.
- Exact original repro, unchanged script/input/expectations: `MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ReviewWebRealUvF3Consumer.cs`, exit **0** after the fix. Original exit 1 remains recorded separately. Wrong case/step clears both panes; Before-as-After clears GM while genuine PK After remains 130 triangles and U5/V5. Log: `temp_docs/real-uv-context/fixed-original-repro.log`.
- `MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyWebUvSelection.cs`: exit **0**, **14/14** finite browser scenarios. Normal Before/After/Before draws the four existing snapshots, with 96/96 Before, 220/130 After, and U5/V5 each. Before-as-After, After-as-Before, wrong case, wrong step, wrong pane-kernel paths and wrong selected package are rejected both cold and after warming all four actual meshes. Warm negative scenarios have exactly four initial mesh requests and no extra mesh requests after mutation, proving the already-warm path cannot bypass selection checks. A separately routed wrong source mapping in the display manifest is rejected by the independent package pin. The selected Before/After button remains selected. No uncaught page errors. Full routed mutant inputs/hashes and observations: `temp_docs/real-uv-context/selection-controls.json`; durable summary: `docs/web-real-uv-context-validation.json`.
- Two initial extended-probe runs sampled DOM counts across the refresh's two viewport replacements (refreshCount, then the refreshed index). Their diagnostic failures are preserved in `temp_docs/real-uv-context/selection-controls-first.*` and `selection-controls-second.*`. Screenshot pixels already showed the unaffected PK geometry, while separately awaited count reads saw a transient empty/loading frame. The final probe waits for render readiness and reads readiness/counts/selection atomically. No production code or rejection expectation was changed for these probe synchronization fixes.
- Original UI424 ownership/tool/selection/mobile regression: `MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ReviewWebRealUvF3Regression.cs`, exit **0**, **38/38**. The copied reviewer helper SHA remains `72170216def1f4ff30df7265bbe32722bfc86016bf713c98a46e92718ca5866e`; it only selects an ephemeral port and restores the original smoke filename, preserving every existing assertion. Log: `temp_docs/real-uv-context/ui424-regression.log`; results: `temp_docs/inspection-remediation.json`. Legacy no-receipt examples continue rendering without verified U/V claims.
- Opened and inspected `temp_docs/real-uv-f3-review/consumer-before-as-after.png` and `temp_docs/real-uv-context/after-as-before-warm.png`: the requested phase remains highlighted, GM is empty with an explicit unavailable reason, and the unaffected PK pane shows its correct actual phase.
- All pre-edit asset hashes rechecked successfully. `git diff f3fb943 -- web/data scripts/ExportWebIsoparametrics.cs scripts/PrepareWebUvDisplay.cs` is empty. No original receipt, XT, mesh, ISO, source hash, Capture/Refresh, geometry, camera, legend, drawer or other worktree was changed. No PK/export/capture re-execution, native/AOT build, main/live/unit/portal or deployment action occurred.

The original f3 consumer remains rejected. The final author branch differs from independently checked 1eb only by the three C# repro/verification scripts and documentation, not by production frontend or assets. Later serial integration with the separately reviewed layout candidate and its release checks remain coordinator-owned follow-up work; no further test expansion was performed after the narrow acceptance handoff.

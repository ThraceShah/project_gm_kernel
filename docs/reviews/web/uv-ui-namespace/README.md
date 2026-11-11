# UI-owned real U/V namespace: refresh retention fix

Production fix: **`015cd308e147356fecac96bb7354ab1e92c36a03`**, on `feat/web-inspection-reviewed-integration`, directly after the rejected release candidate `3c650671f30efad266531b0512ccfa600adffe38`. Test follow-up: `8627bcd`. Later evidence-only commits do not change the production web tree. Both original parents and their independent scope acceptances remain in history; no layout/camera/legend/drawer/picking rollback or redesign occurred. This is an author-verified fixed candidate for the original fusion reviewer's narrow recheck, **not a deployment or new independent acceptance**.

## Exact production change and preserved assets

The 15 existing resources moved from `web/data/display/real-uv/` to **`web/public/display/real-uv/`**. Vite copies them to deployed **`display/real-uv/`**, outside producer `data/`, so existing fixed 1b freeze/UI-copy automatically hashes and preserves them on every refresh. There is no new overlay mechanism or publish-tool change. All 15 files are **100% byte-identical** to 3c, including index, manifest, display manifest, meshes, ISO lines and safe receipt summaries; `relocation.json` records every old/new path, byte count and original SHA. No manifest bytes required editing. The pinned display-manifest SHA remains `0875f9b8208db2d248e3a4fc912b0f5a077d75deaba8b3d1553b840dfb732714`.

The only production source edits are in `web/src/api/realUvDisplay.ts`, `web/src/api/dataLoader.ts`, and evidence links in `web/src/components/InspectionPanel.tsx`. Logical case/source/step/snapshot mappings and all sourceRun/kernel/stage/unknown parameterHash fields remain unchanged. `progressAssetUrl` resolves the UI display namespace or legacy `data/` against the current app base; it rejects leading slash, backslash and dot-segment paths. Full selection validation still precedes path dereference/cache reuse, and the cache key now includes the resolved resource URL along with run/case/step/kernel/stage. Evidence and manifest links use the same resolution. The explicit `?display=real-uv` entry remains render-only; the default index and legacy result namespace remain separate.

No geometry/XT/export/capture was regenerated. No producer, Publish, Configure, Freeze, main, live, unit, portal, service or host-proxy source was changed. Original 3c failure evidence is preserved in `original-3c-retention-report.json`, copied from the reviewer's original read-only reproduction; its first manual-overlay bundle had 625 files/28214664 bytes and hash `5f43710720d36735d1972666c4f80013fa00b8d3d58fc0a623b4ad2601c3c7e5`, while fresh 607-file producer data with old three-file UI lost 15/15 resources. It is not represented as the fixed artifact.

## Actual fixed 1b freeze and two fresh prepares

Tool source is exact **`1b935972eff529e13b46cb9b08ad9550e0a6aeb5`**. The probe reads only the whitelist from that Git object, runs the actual Freeze script, then invokes its frozen actual Publish script in **prepare mode only**. Frozen critical tool file SHAs are compared with the immutable source. No portal/systemctl invocation is made.

Fresh local test invocation, paths resolved relative to the script:

```text
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyUvUiRetention.cs -- 015cd308e147356fecac96bb7354ab1e92c36a03
```

This requires a fresh isolated probe output and preserves previous evidence. Actual child entries are retained in `tool-entries.txt`. The decisive second-round entry, from the project root, was:

```text
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/uv-static/two-round/release/tools/scripts/PublishReviewedWebProgressUi.cs -- --mode prepare --dist ../../ui --data ../../../producer-round2 --baseline-data ../../previous-static/data --work ../../round2 --ui-commit 015cd308e147356fecac96bb7354ab1e92c36a03 --dist-hash f0593760ed1e22dd73e94f609e9e2353366370b542019a90cb8f486f796a4dd6 --review 0000000000000000000000000000000000000000
```

The zero review pin is an explicit **local test fixture**, not an acceptance claim or publication authorization. This frozen fixture must not be used for publish/hook installation; the deployment owner must freeze a separately accepted release using its actual independent acceptance SHA. The readonly previous-bundle fixture is the original reviewer's local 3c fresh-data output, not a claim about the currently served UI. Both this fixture and both fresh producer copies have **no manual `data/display/` overlay**. The initial author harness placed its release inside its UI source checkout, and unmodified 1b correctly refused overlapping paths (child exit 134); that diagnostic is retained in `temp_docs/uv-static/retention-first.log` and `two-round-first/`. The final harness uses an exact detached 015 source snapshot, sibling to release output. No tool behavior was changed to pass.

Final results: Freeze exit **0**, first Prepare exit **0**, second Prepare exit **0**, retention probe exit **0**. Frozen UI has **18 non-data files** (3 frontend files + 15 static resources). Each Prepare independently copies a separate fresh 607-file producer directory with zero U/V resources into a separate fresh work directory. Both resulting bundles retain all 15 UI resources, have **no `data/display/` duplicate**, and have exactly the same full bundle hash. This is not reuse of an already-overlaid or cached output.

| Fixed artifact identity | Value |
|---|---|
| Owned tool UI / dist hash | **`f0593760ed1e22dd73e94f609e9e2353366370b542019a90cb8f486f796a4dd6`** |
| Round 1 and round 2 full bundle hash | **`8dca4b3500652fe0ff5161a8a5943abdcc5a69d0a40b472de17e95b1ca583069`** |
| Original root data/index SHA | `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990` |
| Frozen UI files / bytes | 18 / 1072905 |
| Of those, unchanged static U/V | 15 / 167168 |
| Original producer files / bytes | 607 / 27142034 |
| Complete bundle files / bytes | **625 / 28214939** |
| 30 MiB budget / 32 MiB hard limit | 31457280 / 33554432 |

Hashes use the actual owned tool convention: SHA256 over ordinal-sorted relative path + file SHA256 UTF8 concatenation. Every original historical file and the original root index is byte-identical in both inputs and both outputs, with source files rechecked after execution. Existing capture/code/time/results/ratio are unchanged. `retention-report.json` preserves tool SHAs and both actual `prepared.json` records; `bundle-inventory.json` contains the complete 625-file byte/SHA vector. The final isolated browser artifact is `temp_docs/uv-static/two-round/release/round2/bundle-2485083f33719217`.

## Minimal browser/context verification

- One TypeScript/Vite build: exit **0**, `temp_docs/uv-static/build.log`; existing chunk warning remains. Frozen UI is the exact built output. JS SHA `8e5cb715d4c469db6aef267b649a2d33e9ce58865de92c06ec296c9a0daeead7`, CSS SHA `2ab99dacb75729be431d09937db1f14d7e727393e7006f5b56d5e6b4d5589b08`, HTML SHA `c5420fb0ef2970764c040624a4430ee764bce2bb2020c1c1bf4294a009a99d0c`.
- `scripts/VerifyWebUvSelection.cs`: exit **0**, 14/14 normal/cold/warm context scenarios. Before/After/case/step/kernel/package/source-map rejection expectations are unchanged. Original probe SHA was `57b3b89e2fd512677fff9002374cd748a2b06ee4454511c435c48fa8e73952ba`; adapted SHA is `cb7b39bbd32df46cf5aabc496b618fadd1bcad54859e3e0a09faf2321eb0b64d`. The **only change** is the local assetRoot literal `data/display/real-uv` → `display/real-uv`; the route glob `**/display/real-uv/*.json*` and every assertion remain unchanged. Every one of the 13 negative scenarios has at least one recorded routed mutation, so no missed interception is counted as a pass. `context-summary.json` records those counts. The original review Consumer script remains byte-identical at `4278d8ef82e0fd565c6554c1cb6eb715140c0f14867074ff45d471555bd5ebb4`; it was not relabelled or rerun as a broader suite here.
- `scripts/VerifyUvStaticBase.cs`: exit **0**, **12/12**, against the actual second frozen Prepare artifact, mounted only at `/apps/gm-kernel-progress/`. Four real snapshots draw 96/96 Before and 220/130 After with U5/V5 and actual receipt snapshot/stage provenance. All 15 resources return 200 with exact hashes, all mesh/summary/ISO references remain inside the UI namespace, and the old producer overlay URL returns 404. A nested warm Before-as-After mutation is actually intercepted twice and rejected (GM empty/unavailable, PK actual After 130/U5/V5). No asset request escapes to the site root and there are zero uncaught browser errors. Full observations are in `base-browser-report.json`.
- The same frozen nested app renders the original 610 screenshot case using unchanged index117 data, with U/V unavailable as before. Actual yellow 25% pixels select gm:edge:107 and pk:edge:148 from their original source selected-edge sets. The drawer overlays without canvas shrink, focuses its close control, and Escape restores the invoking comparison button. `nested-uv-after.png` and `nested-legacy.png` under `temp_docs/uv-static/` were opened and visually checked.

No completed two-round/context test was repeated after the coordinator's recovery message. No 57/30/38 full suite, PK/export/Capture/Refresh, Native/AOT build or benchmark was run in this fix. Existing tool/framework NuGet/platform advisories remain. The remaining step is the original fusion reviewer's narrow second-round-retention/new-path recheck, followed only by a separately authorized deployment. Nothing was deployed in this task.

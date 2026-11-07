# Topology event framework acceptance

Status: in progress. M0 reproduced; the first common cycle consumer and the two M2 representation gates have current evidence. The complete M1 event contract and M3–M5 remain unaccepted.

The required final result is a **self-constructed** short-edge fillet with successful downstream Boolean, lifecycle and independent PK validation. Imported native products are tracked separately and never establish this gate.

## Commands and evidence

All commands below use `MSBUILDDISABLENODEREUSE=1`. Test exit codes are saved before testhost cleanup. All build invocations use existing dependencies.

| Gate | Command arguments | Log | Result |
| --- | --- | --- | --- |
| M0 focused | `dotnet test tests/KernelTests -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~BlendSupportChainTests\|FullyQualifiedName~RegularSolidBlendTests\|FullyQualifiedName~ExplicitSpineTests' --logger 'console;verbosity=normal'` | `temp_docs/topology-event-framework/m0-focused.log` | exit 0, 61/61 |
| M0 plane-nurbs | `dotnet run scripts/RegularSolidBlendOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- plane-nurbs` | `temp_docs/topology-event-framework/m0-plane-nurbs.log` | exit 0 |

## Independent states

- M0 focused tests: exit 0, 61 passed, zero failed/skipped.
- M0 plane-nurbs: exit 0, independent fillet/fresh XT/lifecycle/subsequent Boolean passed.
- Native short-edge construction: succeeded, full check zero faults.
- Imported native product/fresh XT: succeeded, full check/strict comparison/geometry passed.
- Self-constructed short-edge product: exit 134 at script level, API 5000 at Supports, zero new faces.
- Imported-product subsequent Boolean: API 157 at Arrange; `m0-short-edge.log` and exit file preserve the full failure.
- Self-product subsequent Boolean and lifecycle: not yet reached.
- Legacy topology guard: implemented at `AssembleRegularLocalBlend` entrance; regular tests enable it. Guarded independent oracle pending.
- Fixed probes and full matrix: not yet rerun.

## Remaining work

The production heterogeneous rolling network, finite interaction solver and boundary assembly are not yet implemented. No stage is accepted on declarations, trace flags or historical results alone.

## Latest checkpoint

- Real kernel version confirmed by the running oracle: PK `38.0.150`, session precision `1e-8`.
- `m2-focused.log`: exit 0, **64/64**, zero failures/skips; includes the corrected retained-hole regression, identity-copy positive and near-copy negative, and the guarded provenance/lifecycle/allocation cases.
- `m2-all-kernel.log`: exit 0, **1658/1658** KernelTests passed, zero failures/skips, 6m4s. Function catalog: 175 dispatch descriptors verified; allocation IL guard: 2473 methods, CLEAN. This run covers the first common-cycle/identity-support/remote-hole increment, before the subsequent analytic-contact parameter-map change.
- `m2-short-edge.log` and guarded `m2-short-edge-trace-fixed.log`: exit 134, self Fix 5000/Supports and imported-product Boolean 157/Arrange unchanged.
- Guarded failing imported Boolean trace: input face index 8, source face slot 26, Blend surface 395, ellipse carrier 399/generation 1, retained interval `[pi, 3*pi/2]`, endpoint events `6 -> 2`. Failure occurs while locating the end on the Blend surface (`SheetCurveUV`, SurfaceInverse). These are diagnostic run-local identities, never production dispatch keys.
- The trace script's first build (`m2-short-edge-trace.log`) exposed a misplaced using alias; it was moved before top-level statements and the corrected script was rerun. Full raw logs/exit files are preserved.
- Next concrete gaps: retain exact finite ellipse/Blend parameter correspondence at this boundary; certify multi-participant events before point merging; implement heterogeneous rolling-network geometry/events and assembly. No native product is substituted for self construction.
- First increment committed as `d8fff8a` and pushed successfully to `origin/main` (`598fac8..d8fff8a`). Subsequent analytic-contact parameter-map work remains a separate increment.

## M1 implementation under verification

`BuildRegularLocalBlend` now calls `AssembleFiniteRegularBlend`. The regular geometric provider supplies contact/end curves and endpoint associations. The assembler retains all source loops, trims adjacent finite curves and derives the orientation of each replacement use from endpoint incidence. It sends unordered directed boundaries to `ArrangeFiniteReplacementFace`; shared `ExtractSheetCycles` and `PublishFiniteSheetCycles` decide cycles/holes. Boolean's `ArrangeSheetFace` uses the same finite-use and event-identity halfedges and cycle/publishing code.

Native ICurve UV is consumed from the same CHART-branch evaluator's `SampleWitness`, including L3 exact hits. Procedural contact boundaries use their known spline-foot map and blend station/section map. UV is not reconstructed by a global inverse when this exact association exists. This does not yet remove every XYZ inverse in Boolean clipping/source splitting.

The optional internal trace connects finite carrier/generation/interval/endpoint events with source face, arrangement face/edge and cloned B-rep product curve/face. It allocates no records when disabled. `--event-arrangement` enables this trace and forbids the legacy regular assembler in the regular oracle.

Initial migrated regular-provider tests: exit 0, five geometric cases passed (`m1-first.log`). Initial migrated plane-nurbs oracle: exit 0, fresh fillet and downstream cut, independent PK and lifecycle passed (`m1-plane-nurbs.log`); this run preceded the UV association and trace changes and did not yet enable the guard.

Subsequent checks:

- `m1-focused.log`: exit 0, 61/61 after native UV association changes.
- `m1-regular-final.log`: exit 0, 18/18 regular tests with the legacy guard enabled, including allocation/rollback and provenance checks for both fillet and Boolean. An earlier provenance test (`m1-provenance.log`, exit 1) incorrectly compared stage-local face indices across Boolean compaction; it now follows the source face and actual carrier/generation/interval/event association. Production trace includes both stage-local index and source identity.
- `m1-guarded-final.log`: exit 0, `RegularSolidBlendOracle.cs ... -- plane-nurbs --event-arrangement`, fresh fillet/cut, full PK checks, strict comparisons, independent geometric validation, copy/delete/reload/transform lifecycle. The trace is actual input/arrangement/product data, not a boolean migration flag.
- `m1-short-edge.log`: exit 134, imported-product cut still 157/Arrange and self construction still 5000/Supports.

Boolean clipping now remembers known finite-cut endpoint t together with carrier generation and event identity. Arrangement/source splitting consume these associations before attempting inversion. Conflicting t for one carrier/event fails unresolved rather than selecting a nearest root. General multi-participant event certification and removal of all distance-only point merging remain open; the full M1 data contract is therefore not yet accepted.

## M2 under verification

`IdentitySupportCertificate` proves the identity UV map by complete defining-data equality: analytic chart coefficients, or B-spline degree, dimension, rationality, vertex counts, periodicity, knots and poles. It records both generations and oriented normal relation. This is a sufficient exact certificate, not approximate recognition or a general reparameterization/G1 certificate. `SameSupportChainFace` consumes it without equating the two face identities.

The former separate-record rejection test now checks both an exact independent copy and a displaced-by-`1e-10` negative case; the latter must reject and preserve the pre-fix XT exactly. The equivalent-copy oracle command is `dotnet run scripts/SupportBlendChainOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- asymmetric43-base --separate-support`: exit 0 (`m2-support-oracle.log`), self and PK both modify the separated-support input independently, with full checks, strict comparison, XT reload and subsequent cut.

The support-area single-loop rejection has been removed after wiring all retained loops into common arrangement. Whole-band separation from other finite manifold faces still protects inner boundaries; caps retain their single-area-loop provider precondition. Contact placement uses all loops. A bore through the actual modified planar support supplies a remote inner loop; the corresponding independent command is `dotnet run scripts/RegularSolidBlendOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- plane-nurbs --event-arrangement --remote-bore`: exit 0 (`m2-bore-final.log`), including geometry, strict fresh comparisons, full BODY_check, lifecycle and subsequent cutting. The bore remains in the retained half after the later cut.

Intermediate failures are retained. `m2-support-certificates.log` found the spline-foot locator being applied to equivalent analytic supports; the call is now restricted to spline supports. `m2-targeted.log` and `m2-targeted-fixed.log` caught a test helper's wrong cylinder API name at compilation. `m2-targeted-final.log` passed 15 support-chain cases and failed the hole-after-cut assertion because that test's cutter removed the hole itself; the bore moved to the retained half and the focused rerun is pending. `m2-bore-oracle.log` passed fillet/cut but found the lifecycle helper required explicit finite limits on a complete vertexless conic ring. The helper now explicitly verifies the natural full-turn conic domain for that representation while preserving explicit-limit requirements on finite curved edges; the final bore oracle passed.

## Exact analytic-contact parameter correspondence

The failing ellipse/Blend endpoint belongs to a collapsed rolling section. XYZ alone does not select a unique section coordinate there. `AnalyticSpineContact.TryParameterMap` now preserves the native affine/phase/reversal map that was previously discarded by `Equivalent`. The complete line or conic coefficients bound the spatial error over the supplied finite interval; the predicate uses the existing `1e-10` contact-recognition threshold. A finite occurrence chooses a constant periodic lift from its actual interval. The UV locator supplies `u = shift + scale*t`, fixed contact `v`, and its exact UV tangent; it avoids the singular metric inverse at a collapsed section. No approximate spline or tolerance increase is involved.

`m3-contact-map-tests.log`: exit 0, five tests passed, including the imported finite elliptic boundaries at both collapsed endpoints, both coedge directions, their UV/3D consistency and nonzero native parameter tangent. Existing shifted/wrong-shape negative coefficient tests remain.

`m3-contact-map-short-edge.log`: exit 134 **only because self Fix is still 5000/Supports**. Imported-product copy/reflection/scaling/fresh XT and its subsequent Boolean now pass: self Boolean returns 0/Publish, followed by zero-fault full BODY_check on both native and managed-fresh products, strict independent comparison and mass checks. The legacy regular template guard is enabled and the complete boundary trace is retained. This is imported-product downstream capability, not self-constructed short-edge acceptance.

`m3-map-final-focused.log`: exit 0, 69/69, zero failures/skips, after both boundary location and winding rays consume the exact map. `m3-imported-closed-loop.log`: exit 0 with `--import-only --geometry --lifecycle --event-arrangement`, all imported-product gates above passed. This mode remains explicitly insufficient for the self-construction requirement. The earlier full `m2-all-kernel.log` predates this increment.

`m3-saved-probes.log`: exit 134, all **510** immutable coordinates replayed, **39 mismatches**, native classifications changed **0**. This remains 23 API-157 failures and 16 classification differences; none were removed from the suite or accepted as equivalent. The contact-map addition repairs downstream boundary location but has not repaired all public material classification behavior.

The heterogeneous rolling-band constructor remains open. The next mathematical provider work uses exact offset-line and scaled-conic carriers with complete coefficient verification; topology must still come from finite interaction events and common arrangement.

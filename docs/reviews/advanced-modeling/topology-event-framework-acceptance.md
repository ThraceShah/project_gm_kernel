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

## M3 implementation checkpoint (not accepted)

The heterogeneous rolling-band constructor now produces the two different support-pair candidates, discovers finite support/band and band/band intersections, clips and splits them, and assembles both candidates and all original faces through shared `ArrangeFaceCutGraph`. Provider equations, finite domains, degeneracies and contact/branch logic are documented in `docs/Finite_Analytic_Rolling_Network.md`. The old regular template guard is enabled in these runs.

`m3-null-boundary-cycles.log` reaches `Select` after completing arrangement (exit 134 because material selection was not yet implemented in that run). Its trace includes the real short positive ICurve intervals; none were merged by raising tolerance. The current working implementation adds unique oriented cell selection and provenance-based publication, still awaiting validation. Self construction, self-result lifecycle and self-result Boolean are not accepted yet.

`m3-intersection-oracle.log` and the isolated initial-HEAD replay `m3-intersection-initial-baseline.log` both report **65 passed / 22 failed**, exit 1. Their pass/fail summaries are identical (`m3-intersection-current-summary.txt`, `m3-intersection-initial-baseline-summary.txt`). Those 22 strict bounded-wire class/sense/precision failures exist at the initial `598fac8` baseline; they remain failures, not a passed suite. The new skew-cylinder scalar intersection case passes independent fresh-XT finite-wire validation. Initial baseline replay first failed to build through shared dependency output paths; an isolated source copy of the existing pinned PKToy resolved it without upgrading dependencies.

The managed baseline checkout is attached as `intersection-baseline`. Its archive request was rejected because the app identifies it as the task's primary checkout. No checkout was force-removed; main-source work continues in the original project directory explicitly.

## First self-constructed target and remaining M4 gates

`m3-shared-events-regression.log`: exit 0, **63/63** tests (scalar analytic/plane SSI, regular solid blends, explicit spines and the selected contact tests). `m3-remote-bore-regression.log`: exit 0, the modified-support inner loop, fresh strict PK comparisons, lifecycle and subsequent cut still pass after the shared-event changes.

`m4-self-boolean-gates.log`: self Fix returns 0 with two products; fresh real-PK full BODY_check has zero faults. The unchanged `ParasolidBodyCompare.AssertEquivalent` accepts the independently constructed fillet and its mirrored/scaled copy and subsequent self Boolean result: only CHART-count diagnostics remain. Product curve senses are derived from selected-carrier provenance and support-side uses; the comparator was not changed. Earlier native edge-reversal and chart-subsequence experiments did not solve the issue and were removed from production code.

The command still exits 134 because its three new self-result mass point checks exceed the existing `1e-7` threshold. They are not reported as passes. The native mass diagnostic (`scripts/ShortEdgeProductDiagnostic.cs`, `m4-mass-error-bounds.log`) shows the largest default point discrepancy in an off-diagonal inertia component (`2.26895e-7` normalized), while all native integration error intervals overlap. Requiring higher requested integration accuracy does not change the numbers. Temporarily tightening PK working precision changes numerical behavior and produces worse transformed results; that experiment was removed from acceptance. Neither error intervals nor another working precision currently override the established mass gate.

Self XT reload initially failed on an ICurve because the explicit spine's bounded rolling branch was not persisted in BLENDSF LIMIT records. Finite explicit spines now store oriented native terminators, and receive accounts for spine sense when recovering their increasing parameter interval. `m4-oriented-finite-spine-limits.log` preserves fresh PK receive/full check/strict comparison (still exits 134 on the mass point gate). `m4-oriented-limit-receive.log`: exit 0, managed self-product receive succeeds. `m4-finite-limit-tests.log`: exit 0, **37/37**, including six new semantic edge-selection/network/rollback tests, both selection orders, retained positive short intervals, XT reload, source deletion and zero remaining geometry pools. The earlier failed test logs are retained; their first test helper consumed only one XT memory block and omitted the required user-field transmit setting, and then exposed the real missing finite-limit problem.

Not yet accepted: correctly mapped self-result D1/D2 geometry checks, independent-source input transformations, full parameter matrix, noninterfering/interfering extra components, reloaded-result modelling, all historical material probes, full current regression, and final M4/M5 closure. The active work continues; no success is inferred from reaching Build or Publish.

### Subsequent evidence and checkpoint

`m4-canonical-network-geometry.log`: self Fix and unchanged strict PK body comparison pass with reversed mark order; all four curved faces have complete analytic carrier/support correspondences. The actual self-result uses `u = pi - nativeU`, `v = 1 - nativeV` on its rolling surface. Mapped finite-face D0 is below `4.7e-15`, D1 below `2.1e-15`, and Richardson-checked D2 below `1.2e-11`. Both old cylindrical supports and the new cylinder are checked, not just a face selected by traversal position. The command still exits 134 on its mass point gate.

Geometry construction now orders finite curved spines before generator bands, independently of mark order. Result descriptions are then ordered by their preserved `PendingBlendSequence`; product tags come from the selected replacement cells. This changes representation/traversal conventions, not a connectivity table.

`m3-periodic-cell-witness.log`: exit 0, two additional component tests. Nonadjacent disjoint planar bands use the common network/arrangement; interference reaching opposite supports fails and restores full topology, pending state and geometry resources. A periodic cylinder witness now lifts its UV candidates coherently before averaging; complete loop winding still validates the hint.

`m3-components-independent-oracle.log`: exit 0 (`scripts/RollingNetworkOracle.cs`). Independent PK and self construction of the two separated components, fresh full BODY_check and unchanged strict comparison pass. Copy/delete-source modelling and XT reload followed by a second Boolean also pass; remaining geometry pools are zero.

`m3-all-kernel-checkpoint.log` was interrupted by concurrent testhost cleanup and is not a complete regression run. `m3-all-kernel-checkpoint2.log`: exit 1, **1668 passed / 1 failed / 1669 total**. Its partial planar-corner case exposed an applicability regression: two planar generator bands meeting across three supports need a corner provider, which this network does not supply. That mathematical precondition is now checked before construction, preserving the prior unsupported result and rollback. The existing three-selected-edge corner provider remains in use. The near-coaxial-cylinder guard test now expects a truthful unresolved `157`/`NotConverged` after real scalar SSI rather than the previous unsupported `5000`; `m3-almost-coaxial-status.log` passes, preserving the two source bodies. A complete current regression is running in `m3-all-kernel-final-increment.log`.

The first small boundary-parameter case, `--angle=0.25 --radius=0.25 --geometry --event-arrangement`, still fails with 157 in BandEvents/SSI Limits (`m4-small-parameter-limit-diagnostic.log`). Discovery and graph ordering succeed with 537 nodes / 532 segments, but an open component starts at an internal spine-chart boundary `(u=0, v=3.042337293050213e-6)` rather than a finite surface-domain end; its other end is `(u=2.5734016166196172e-9, v=1)`. The next gap is consistent certified boundary-root association across that shared chart boundary, not a larger merge epsilon. The scalar root producer still has residual-only endpoint/near-zero-edge shortcuts whose contracts must be strengthened.

`--input-rigid --event-arrangement` now transforms the unfilleted seed before semantic/geometric edge selection and both independent operations (`m4-input-rigid-first.log`). Self construction fails at BandEvents/SSI Discovery with unresolved scalar box `[-0.0004454412318718992,0] x [0.9999515222998571,0.9999515244597207]`, depth 40. This is a real preconstruction transform test, not a transformed PK finished product. Similarity options scale radius and mark/fix tolerances with length; further scaled geometry acceptance still needs its explicit per-derivative unit contract.

`m3-all-kernel-final-increment.log`: exit 0, **1671/1671**, zero failures/skips. This is the complete regression for the network, finite-spine persistence and shared arrangement increment. `m4-signed-curvature-regression.log`: exit 0, **16/16**, including both signed-normal curvature tests and all rolling-network construction/rollback tests after the subsequent curvature correction.

`m4-scalar-boundary-conditioning.log` records interval evidence at the small-case shared chart boundary: the scalar slope is about `3.738e-6`, so a residual-only `1e-13` acceptance can leave parameter uncertainty of order `2.7e-8`. The existing merge epsilon cannot establish a shared root. The root producer must retain an isolation enclosure and a monotonicity certificate; neither a larger merge threshold nor additional samples establishes identity.

Main remaining gates: certified scalar root/event boxes and internal chart-boundary joins; transformed-input discovery and contact certification; all 96 parameter cases; same-level self lifecycle including reload-and-model; the fixed 510 material probes; the mass point discrepancy (native error bounds, tighter working precision, and binary unit experiments remain diagnostics only); final regression after remaining fixes and later stages. No comparator, probe coordinates or acceptance thresholds have been weakened.

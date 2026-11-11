# Independent R3 frame review — 9b2f4c6

**Operation-layered result: the exercised default blend output passes its primary checks; its classification/internal endpoint assertions have regressions; the subsequent two-tool Boolean combination fails.** The independent complete RollingNetworkTests run reproduces **16 passed / 3 failed / 19 total, exit 1**, but these are different operations/contracts, not nineteen independent blend acceptance cases. The later Boolean failure does not reverse the earlier blend primary result. An isolated genuine-PK Boolean comparison on the identical candidate-produced input was not run, so that separate operation case remains unverified. The overall framework increment is not fully qualified while the combination and remaining contracts are open.

Acceptance interpretation version: **operation-layered v1**, applying the user's latest explicit correction after initial report commit `69fc35e`. That earlier interpretation and the unchanged raw evidence remain in history; this revision supersedes its blanket R3-not-accepted label. No test or numerical threshold is changed.

## Snapshot and isolation

- Fixed candidate `9b2f4c6c989a393fd2cbaa3e66d6ea5b52035017`, fetched from `origin/fix/topology-r3-gemini`; against `90f4d7840b4d3d8219ba29d1845ccbaff3386f5a`.
- Independent branch `ThraceShah/gm-r3-frame-review`, in the existing review worktree. Previous c301/2bdd reports, branches, native bindings and ignored evidence were retained. Read `AGENTS.md`, the candidate response, the complete two-file diff and the precise implementation-worker regression log. Only the 25-line production frame increment and its response document belong to this fixed candidate; later moving work is not reviewed here.
- No production/test/CSV/threshold changes, merge, deployment, helper agents, Windows, NativeAOT publication or performance benchmark. All temporary probes are C#/.NET 10 and use the managed kernel project reference. Only this report and reproducibility documentation are committed.
- Read the configured parasolid-research skill and used the actual query/decompilation MCP for PK_BODY_boolean_2 and the BODY_check signature, then ran a genuine PK check through PskernelSharp/ParasolidScriptHost. The queried Boolean address was `0x2837c0`; its truncated pseudocode output is largely a prologue and is not evidence for a specific native frame rule or for GM's 157 cause. Raw query evidence remains ignored. No SDK/library/license data are committed.

## Three independent regressions

Command from the project root:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~RollingNetworkTests' --logger 'console;verbosity=normal'
```

| Test / fixed-source assertion | Independent observation | Scope |
| --- | --- | --- |
| HeterogeneousBands… `(reverse:true)`, line 66 → AssertSavedProbeOutside line 463 | Original row 289 expects `5702` outside, gets `5703` on | Classification-specific regression; not waived |
| Same test `(reverse:false)`, VerifyStationaryShortEnd line 439 | No ICurve endpoint has the required exact stationary coordinate | Internal representation/proof contract; its later root assertions are not reached in the unit test; not a failed PK blend geometry comparison |
| ProjectedRollingCellFailureRestoresTargetAndBothToolsAfterUnion, line 127 | Expected injected `901` memory_full; actual `157` cant_do_intersect | Blend→two-tool Boolean integration fails at BodyBoolean2 before the intended downstream stage |

The complete run finishes normally, with no skipped cases and 5.6953 minutes total reported time. The earlier partial Opus log that stopped after fourteen cases cannot establish a complete nineteen-case result. Implementation-worker baseline evidence reports 19/19 on the against-base; the candidate result above is this review's own run.

An already-started minimal control also completed on the exact against-base in this same worktree, with the unchanged final probe and `--trace-fix --inject`: the stationary point is exact, its root hook succeeds, the Boolean reaches ProjectedCellReplacement and returns the intended **901**, and all three inputs roll back byte-identically. Only that one baseline reproduction was run; no baseline full-suite rerun is claimed. The review branch was then restored to fixed 9b before documenting/committing.

## Frame map: a genuine general consumer gap, with a specific non-cause

The candidate at `KernelRuntime.AnalyticRollingCandidate.cs:88`–98 changes the signs of the source ellipse's normal and major direction, and flips its spine sense if the normal reverses. It retains the source major direction for contact sense at lines 134–141.

Write the original carrier as `O+X cos(t)+Y sin(t)`. Let the normal flip be `α ∈ {−1,+1}` and major flip `σ ∈ {−1,+1}`. Apart from the positive offset-radius homothety, the new coefficients are `σX` and `ασY`. The required carrier parameter is therefore:

```text
t_carrier = α*u_spine + δ  modulo 2π,
δ = 0 when σ=+1, and π when σ=−1.
```

The same physical traversal requires `spineSense=α*carrierSense`. This sign handling in the producer is mathematically consistent; it does not by itself transport finite native parameters or endpoint witnesses. A periodic lift adds turns only and cannot replace a sign/phase map.

The existing Bands trace on the fixed default fixture independently records:

```text
source carrier range [0, π/2]
oldAxis = (0, -0.707106781186548, -0.707106781186548)
oldRef  = (0, -0.707106781186548,  0.707106781186548)
newFirst  = (0, 0.5, -0.5)
newSecond = (-0.5000000000000007, 0, 0)
α=-1, σ=-1; band range [π/2, 3π/2]
```

Thus this fixture requires `t_carrier=π−u_spine`. At `u=π`, directly evaluating the old carrier gives `(-1.5000000000000002,-0.5,3)`, while the mapped `t=0` gives `(-1.5,-2.5,5)`: a concrete large discrepancy if these charts are confused.

`KernelRuntime.FiniteIntersectionEvents.cs:233`–234 nevertheless initializes a carrier parameter from `band.First/Last` and only applies `LiftPeriodicEdgeParameter`. That is not a general chart transfer and must be audited for other finite occurrences/subdivisions. **It is not independently established as the cause of this fixture's three failures:** its included cap `u=π/2` is a fixed point of `π−u`, and raw/mapped carrier positions agree there. The other cap maps to `−π/2`, outside the source range, and is excluded. A general mapping counterexample at π does not prove that this cap caller used π. The implementer's initial single-cause assertion therefore exceeded the evidence.

## Proven local cause of the first Boolean 157

The ignored `FrameProbe.cs` reproduces the original default test fixture, selection, marking and two-tool Boolean options without changing production/tests. In the fixed candidate:

```text
stationary ICurve edge slot 27, curve tag 208
native trim [0.04840682208901396, 0.04848257991252286]
nearest vertex slot 16 = (-1.5000000000000002, -2, 5)
distance from (-1.5,-2,5) = 2.220446049250313E-16
TestNativePlaneEdge(x=-1.5): error 157, roots 0, vertex -1
Boolean: error 157, ProjectedCellReplacement never entered
stage Clip, faces 3/20, BoundaryIntersections, curve 208
status NotConverged, terminal interval
[0.048482579912522854, 0.04848257991252286]
```

`KernelRuntime.StationaryBoundaryEndpoint.cs:38` requires the existing endpoint to lie **exactly** on both the cutting plane and supporting plane. For `x=−1.5`, the stored vertex misses that exact constraint by one ulp. Its normal finite root route is unresolved; the algebraic stationary-end certificate cannot start. This is not merely the unit test's coordinate lookup at line 435: the same missing precondition blocks a real Boolean root.

A controlled intervention in the **temporary model input only** resets that single vertex to `(-1.5,-2,5)`, leaving its spine, ICurve program/CHART, all other vertices and all thresholds unchanged. Then:

```text
TestNativePlaneEdge: error 0, roots 1, vertex 16,
station [0.04848257991252286, 0.04848257991252286], transverse=True
Boolean certifies that endpoint and advances past the original face pair
```

This establishes a local causal link between loss of the exact endpoint constraint and the **first** 157. The diagnostic is not a production repair, not a proposed coordinate snap and not a proof that all near-tangent roots should be accepted by proximity.

The intervention **does not complete the Boolean**. Its final result remains 157, now at `Clip`, faces **8/20**, curve **214**, native parameter 0, NotConverged with `SurfaceIsoPlaneRoots.LastFailure=Subdivision` and unresolved interval `[2.6201997115083022E-14,2.620226764595542E-14]`. ProjectedCellReplacement is still not reached, so the intended injected 901 is still not exercised. In both original and intervened runs, the target and both tool bodies retain byte-identical XT after rollback. This distinguishes a lost supported path from evidence of rollback corruption.

The row 289 query still returns 5703 after that single-vertex intervention. It is a separate residual, not resolved by correcting this stationary point. The separate reported approximately `3.08e-12` deviation of another short-end vertex has not been proved to cause either final 157 or every classification change; no such sole-cause claim is made here.

## First observable loss before publication: contact endpoint versus source constraint

The earliest exposed boundary Input trace already contains the off-plane shared event, before B-rep publication. In the candidate:

| Quantity at shared event 20 | Fixed-source value |
| --- | --- |
| Source line tag 12, source edge slot 3 / source face slot 1 | Mathematical line has `x=−1.5`, `z=5` |
| Source line evaluation at native parameter 4.5 | `(-1.5,-2,5)` |
| Shared stored event used by that line | `(-1.5000000000000002,-2,5)` |
| Generated contact ellipse tag 124, trim `[π/2,π]`, endpoint evaluated at π | `(-1.5000000000000002,-2,5)` |
| Rolling surface evaluated at `(u=π,v=1)` | `(-1.5,-2,5)` |

The unmodified carrier trace and the separate surface evaluation therefore expose a real representation mismatch, not an asserted generic “numerical sensitivity.” The source line can carry the exact algebraic stationary constraint, but the shared event has the rounded contact-ellipse endpoint position. In the against-base trace, the corresponding contact range is `[0,π/2]`, its stationary contact is evaluated at zero and both the shared event and source-line evaluation are `(-1.5,-2,5)`.

Relevant producer/consumer chain: the new frame feeds `AnalyticSpineContact.Curve` and `BindAnalyticContact` (`AnalyticRollingCandidate.cs:126`–129,166–179); the latter rebuilds a principal ellipse from trigonometric coefficients. The common clipping code evaluates a cut endpoint at `RegularBooleanCuts.cs:652`–653 and substitutes a root's exact position only when an original source vertex is identified (lines 654–661), then registers the shared point/anchors (lines 663–665). A generated interior source-line crossing is not an original seed vertex. These code paths permit loss of an exact finite-root constraint while retaining a tolerance-sized geometric agreement. The first private assignment was not instrumented; the trace table establishes the first observable mismatch and its downstream causal effect, without claiming a private assignment that was not observed.

## Primary geometry is separate from remaining functionality

The acceptance units and actual evidence are separated as follows. Inputs are not silently substituted across these cases:

| Operation / input provenance | Actual API and comparison point | Result / boundary |
| --- | --- | --- |
| Import the unchanged `generated-short-edge-input.x_t` fixture into GM | PartReceiveB before marking/Fix | GM import returns 0; this probe did not separately compare that imported input to a PK receive of the identical fixture |
| Default blend on that GM input, mark radius `.5`, tolerance `1e-7`, Fix version 31 | BodyFixBlends returns 0; immediately exported uncut result receives full/strict PK checking against the independent native product | **Exercised blend output primary checks pass**; no evidence here that the one-ulp internal endpoint difference violates stored geometric tolerance; this is scoped acceptance, not every provider input |
| Independent native product provenance for that output comparison | Earlier pure-PK Seed construction and native mark/Fix, output hash below | Same nominal original model/parameters; retained independent oracle, not a new PK Fix run from the identical fixture bytes in this review; serialize/import representation differences must not be hidden |
| Classify that GM blend output at original row289 | BodyContainsVector after successful Fix | API returns 0 but `5703` differs from the existing outside assertion; **auxiliary classification contract fails**, not proof that blend output geometry is invalid |
| Inspect exact stationary endpoint on GM blend output | Internal VerifyStationaryShortEnd/TestNativePlaneEdge | Exact representation/root-consumer precondition regression, distinct from PK full/strict output checks |
| Combine GM blend output with the two original test tools | BodyBoolean2 subtract, with the original guard/options and allocation-injection callback | **Integration case fails at Boolean API 157** before injection; preceding Fix remains a passing step |
| Isolate Boolean on the identical candidate-produced target/tools supplied separately to GM and genuine PK | No such paired PK Boolean run in this bounded review | **Unverified independent Boolean case**; do not call this a global Boolean failure or infer a native result from a different input model |

The fixed GM blend target is hash `7de009139ea2dde494d83859adeb80141871dd61d7d8ca0e4354e0022f5a16dc` below; the two-tool pre-state is built exactly as the copied test code in the appendix. The diagnostic vertex-intervention case has a different pre-state and is separately labelled; it is neither the candidate's operation result nor a replacement acceptance input. The against-base control is another separately labelled integration case, not the candidate Boolean's independent PK reference.

The original probe did not persist individual tool XT hashes, so a future isolated same-input Boolean comparison must save target/tool artifacts and hashes before supplying that same pre-state to each engine. This report does not fill that missing evidence with a different native lifecycle's input. Likewise, the trace/test elapsed time includes setup and validation and is not a standalone blend/Boolean performance number.

The untouched fixed-candidate target XT was independently received into genuine PK, compared with the independently constructed native product retained from the earlier pure-PK control, and checked with full BODY_check. Both bodies have **zero faults**. The existing strict helper passes (`global=21921 local=21931 face_pairs=0`), with max_diffs 1024, all_tests=false, acc_dev_tests=false, tol_dev_tests=true and number/tolerance/class/sense/missing/nonmatch tests enabled; only internal chart-point differences are diagnostic. Success is determined by the helper's result inspection, not by face_pairs=0.

```text
independent native product SHA-256
bd75b7926956c1506e00c172816a0bfc1bb07db1660d74da65aaf1734f219ef1
untouched fixed 9b target SHA-256
7de009139ea2dde494d83859adeb80141871dd61d7d8ca0e4354e0022f5a16dc
```

That successful primary uncut check does not verify the failed two-tool lifecycle. The implementation-worker standard/transverse lifecycle exits 0 and original raw inertia improvements are retained as **implementation-worker evidence**, not new independent lifecycle runs in this review. Their blend and each Boolean step must be reported separately with its own pre-state and comparison point; aggregate exit 0 is not a substitute for such per-operation evidence, and aggregate rolling-test failure does not revoke a preceding passing blend. No full 96-case/kernel rerun or performance benchmark was started.

The user's approved classification scope is same-path comparison on all 510 original coordinates: GM direct versus PK direct, and GM XT received by PK versus native own XT receive; the seven native transport differences are reported separately. This does not delete the existing outside/finite-endpoint assertions, erase a real API 157, waive row 289/339, or convert the preceding native-only control into acceptance of GM output. This review neither changes those tests nor replaces geometry proof with sampled classifications.

## Required repair constraints and bounded handoff

- Carry a proved signed phase/period map for every carrier-to-spine occurrence. Map both finite endpoints, reverse ordering when appropriate, retain sense and occurrence/root provenance, and lift only after transferring charts. `TrySpineEdgeRange` and contact-map code already recognize different gauges; all consumers must use the correct gauge, including subdivisions, cap contacts and finite-event ownership.
- Preserve shared finite-root constraints through endpoint generation, contact reparameterization, B-rep publication, copy/receive and subsequent stationary-root certificates. A known source-line/plane relation cannot be replaced solely by a rounded conic evaluation and then assumed exact by the next consumer. A repair needs constraint/identity evidence or an enclosing certificate, not coordinate-specific snapping, dense sampling or weakened tolerances.
- Treat A+B/A−B degeneracy, one support-axis reversal, fin-order exchange, signed phase, rigid/mirror/scale transport and positive/negative sense under the existing provider's applicability conditions. The current code only flips signs of the original carrier frame; it does not rotate an arbitrary conic into the support bisector. The independent coefficient certificates remain necessary, and fallback/unsupported cases must not acquire a false frame-identity claim.
- Re-establish the actual two-tool path and the intended injected 901 with restoration of target and both tools. Fixing only the first stationary point is insufficient: the second curve214 failure remains independently observed. Retain row289 as a separate classifier investigation and keep all original assertions.
- Preserve the achieved main mass gates and strict native checks while fixing these consumers. Do not revive the discarded contact-frame experiment without its own parameter/provenance proof and regression evidence.

The new frame computation adds bounded vector arithmetic, not a new iteration or allocation mechanism. Existing compact-certificate cost remains a disclosed debt from earlier reviews; no independent-operation performance ratio or end-to-end benchmark qualification is claimed here. The exercised independent blend output is accepted within the primary-check scope above; the combination and internal/classification regressions remain open. R4–R6/self96/transformed/full-regression gates remain outside this bounded review, and the overall framework is not complete.

## Commands and evidence

Raw evidence root: `temp_docs/r3-frame-review/`. Every dotnet build/test/run used `MSBUILDDISABLENODEREUSE=1`; no C ABI changed and no NativeAOT run occurred. Completed testhost inspection found no remaining process belonging to this review; no other worker/service was killed.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/r3-frame-review/FrameProbe.cs -c Release -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --inject
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/r3-frame-review/FrameProbe.cs -c Release -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --inject --restore-vertex
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/r3-frame-review/FrameProbe.cs -c Release -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --trace-fix --no-boolean
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/r3-frame-review/NativeCheck.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true
```

| Evidence basename | Process exit | Meaning |
| --- | --- | --- |
| `rolling19` | **1** | All 19 completed, the three failures above |
| `candidate-probe2` | 0 | Diagnostic completes; actual Boolean=157, stationary hook=157, row289=5703 |
| `vertex-intervention` | 0 | Diagnostic completes; stationary hook=0, later Boolean still157, row289 still5703 |
| `fix-point-trace`, `first-carrier-trace` | 0 | Boundary/carrier observations, not passing geometry proofs |
| `native-check` | 0 | Untouched uncut candidate full check and strict real-PK comparison pass |
| `base-probe` (against-base only) | 0 | Stationary hook=0, intended projected replacement entered, injected Boolean=901, all three inputs unchanged |

An initial `candidate-probe` attempt exited 134 with schema-access error 970 before creating a model; the standalone harness's schema directory was then explicitly set relative to its own file. It is retained as harness setup evidence, not counted as a candidate modeling failure. The reproduction appendix and SHA manifest preserve ignored sources, exact artifacts, raw logs/exit codes and query provenance. The diagnostic source includes an explicitly labelled model-input intervention, which must never be mistaken for a production fix.

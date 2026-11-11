# Native modeling work reduction

The optimization package reduces repeated native interval residual evaluation in complete successful modeling operations. On this Mac's fixed comparison window, shortedge warm median API time changed from **22,593.310 ms to 14,179.107 ms** (observed **1.593×**, **37.24% less elapsed time**). Its requested Jacobian directions and all root/SSI/winding work remain unchanged. This is a useful local optimization package; **the PK performance target is not reached**: shortedge's observed paired GM/PK median is still **9465.57×**.

## Fixed source and execution

- Starting main: `610e3ce844bf99c85af6069905f9f3deaab08133`.
- Instrumented accepted-guard baseline: `ecfff3a`; Native/source tree `e314cc2307c5e77ea6f2b7685ba17836ae4ef5c8`.
- Implementation: `03931f6`; formally measured checkout: `6761600fef20602342cd9dc81bd4f7e4fb85d939`. The latter only corrects the runner's actual capture contract and saves current-run XT artifacts; both use Native/source tree `01e03aa9ed10a8145556872e98fa1677e17a4cb7`.
- Host: Apple M4, Mac ARM64, .NET SDK 10.0.400/runtime 10.0.11, real PK 38.0.150, PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`.
- The private environment preparer returned `ENV_READY` for both checkouts using the verified Mac PK38 cache. No vendor library, schema or credential belongs to this package.
- No public C ABI changes; managed JIT builds were used. No NativeAOT publication or Windows work was run.

The same current-format runner invokes the identical oracle source bytes in both checkouts. Every case runs sources in ABBA order, three fresh unoperated inputs per process. Repetition 0 is that process's cold call; repetitions 1 and 2 are warm. There are two cold and four warm pairs per source/case, twenty oracle processes and sixty GM/PK pairs in total. The API interval comes from capture timings; observer, mesh, comparison and XT file work stays outside it. Build and process wall time are separate.

Call order is GM then PK for Boolean and PK then GM for fillets. The shortedge oracle lets PK retain its independently constructed original seed while GM receives that seed's unchanged XT; fresh-input comparison checks equivalence. The other cases construct both inputs independently with identical numeric parameters. The exact arguments and API options remain in each sample's report, capture and pinned oracle.

Every oracle's original `Process.ExitCode` is recorded as **0**, with no timeout. The fixed report ended `qualified=true`; all twenty samples passed complete per-pair ownership, API return, blend fault where applicable, fresh XT receive, strict equivalence, two full BODY checks, five topology counts and fifteen mass checks. Qualification means the original operation correctness contract passed; it does not certify an exclusive CPU window. The outer background shell's exit status was not separately captured and is not substituted for the recorded child exits.

## Operation results and limits

Warm operation-only milliseconds; four pairs per source. Ratios are medians of individual same-host GM/PK pairs, not division of measurements from different machines.

| Independent operation | Baseline GM median | Candidate GM median | Candidate PK median | GM elapsed change | Candidate GM/PK |
| --- | ---: | ---: | ---: | ---: | ---: |
| Equal-overlap sphere subtract | 0.07575 | 0.07545 | 0.26760 | −0.40% | 0.285× |
| Two-axis planar subtract | 0.59570 | 0.77640 | 1.10575 | **+30.33%** | 0.801× |
| Curved block subtract | 917.27970 | 909.81715 | 1.85730 | −0.81% | 488.27× |
| Plane/NURBS solid fillet | 1540.33615 | 1524.97405 | 1.43810 | −1.00% | 1064.27× |
| Shortedge a1 / r0.5 / tolerance 1e−7 fillet | 22593.31015 | 14179.10690 | 1.49920 | **−37.24%** | **9465.57×** |

Shortedge warm GM ranges are 22,482.318–22,646.412 ms before and 14,145.562–14,332.697 ms after. The other three small changes do not establish a benefit. Planar's observed regression is retained: baseline 0.5930–0.6097 ms, candidate 0.5975–0.9793 ms. This path performs zero interval Jacobians, so this experiment does not establish a causal explanation for that submillisecond difference.

The one-minute load average ranged from about 3.51 to 11.09 during the full run, and about 3.65 to 4.81 across shortedge's ABBA rounds. Load averages include recent builds and do not measure exclusive API CPU cost. Desktop processes were not stopped. These are **fixed-window observations with small samples**, supported by deterministic work reduction, not an uncontended or general throughput certification. There was no load chasing or repeated formal ABBA to select a favorable window.

No downstream failing Boolean was used as a quick performance result. Fillets and Boolean are separate independent operations; this table does not certify any previously unsuccessful combination lifecycle. The large remaining curved/solidblend/shortedge gaps remain visible.

## Actual work reduced

The original `IntervalJacobian` replays the full `IntervalResidual` once for every variable. The common shortedge system has seven variables without UV. Values, support-type dispatch, normals, square roots and interval divisors were recalculated for each directional seed and for each tube expansion/refinement.

The candidate records that tube's original interval expression structure once. Each later box rebinds every variable, recomputes primal interval values and divisors, and reruns the original finite/positive guards. Forward derivatives propagate in expression-node order only for actual dependencies; independent seeds use the **actual zero-seed derivative**, rather than assuming a finite zero. A compressed derivative offset maps each node's dependency mask to caller-owned scratch. No result or record crosses a tube, model edit, command, generation change or rollback.

The following are the first warm repetition in matching ABBA processes; the complete per-repetition records are in the machine-readable summary.

| Counter | Baseline | Candidate |
| --- | ---: | ---: |
| Jacobian requests | 1,450,110 | 1,450,110 |
| Requested variable directions | 10,113,418 | 10,113,418 |
| Full directional residual calls | 10,113,178 | 27,774 |
| Record build attempts | 0 | 223,506 |
| Recorded nodes, including variables/constants | 0 | 33,683,402 |
| Record rebind attempts | 0 | 1,217,266 |
| SSI cells | 4,956 | 4,956 |
| Root cells | 220,895 | 220,895 |
| Root candidates / winding | 3,518 / 196 | 3,518 / 196 |
| Bound requests / evaluations / hits | 275,525 / 223,331 / 30,727 | 275,525 / 223,331 / 30,727 |
| Budget failures | 0 | 0 |
| Instrumented bound elapsed | 20,656.580 ms | 12,993.260 ms |
| Full API elapsed | 22,646.412 ms | 14,332.697 ms |

The fall in full residual calls is **not** a 99.7% reduction in all arithmetic: the recorded primal rebinds and sparse derivative arithmetic still execute, and all original enclosure, contraction, root, refinement and topology work remains. Counters are scoped to the complete modeling operation through the existing parent/child profile. They do not time individual instructions or prove exclusive CPU attribution.

Production recording is restricted to native seven/eleven/fifteen-variable systems with no UV and no rational explicit BCurve spine. Box-dependent spline hulls, rational explicit-spine bounds, three-variable and larger systems retain their current dedicated directional evaluator. This is computational capability selection, with no historical model/API/report compatibility layer.

## Mathematical and lifetime boundary

The native XT/BLEND/ICurve/CHART equations, parameter values, support identity/sense, chart planes, budgets and tolerances do not change. There is no Hermite replacement, epsilon snapping, sampled proof substitute or validation deletion.

For a fixed box, every stored primal operation uses the same `IntervalDual`/`IntervalNumber` operation and arithmetic grouping as the original residual. Cached divisors are recomputed when that box changes, retaining outward rounding. Variable leaves carry exact seed bits; each operation's dependency mask is the union of its operands. Inductively, an inactive seed consumes the same evaluated zero-seed derivative and an active seed executes the original directional rule with its already-computed operands. This explains why sparse propagation preserves the original enclosure; bitwise comparisons alone are not the general safety argument.

Every original intermediate finite check is retained as a record flag and is checked again for primal and dependent derivative evaluation. The cone's positive-radius branch is also recorded and rechecked. A failed rebind starts the next attempt again at the first variable; topological operand order prevents consumption of an old suffix. Partial matrices are never consumed after failure. The record is a local ref struct borrowing the one stable occurrence/chart/program context; there is no cross-command cache invalidation to guess.

The record and derivative spans are bounded stack storage. Capacity is `64 + 512 × occurrence count`; the scratch reserves that capacity times the current variable count. This adds temporary capacity relative to the old directional path, especially at fifteen variables; it is not claimed as reduced peak stack memory. The fifteen-variable manufactured native chain passes complete tube certification in the normal Mac test host. Custom small-stack embedding threads have not been separately measured. The original proof iteration and cell budgets are unchanged.

The accepted initialization/empty-UV guard is the Native/test slice of `7aca6f0`, correctness accepted by `957ce708`. Its original active-prefix initialization inventory remains at `perf/interval-bounds-initialization:docs/Interval_Bounds_Initialization_OptA.md`. Explicit LU RHS, assembled matrix and live spline-cache initialization remain mandatory. This reused guard is not the principal optimization claim.

## Validation and research

Twelve focused arithmetic/support tests pass: exact outward interval endpoints, sparse high-bit seeds, zero managed allocation after warmup, retained intermediate singular failure, analytic support classes/sense, native nested child coordinates/parameters and spline/offset comparison. A separate maximum recorded-chain test passes at fifteen variables. Its first test run used the decimal literal −0.3 as the expected coordinate although the supplied plane coefficient was `−0.1 × 3`; the expectation was corrected to the actual input coefficient without changing any tolerance, bound or production code. Both test records are retained.

The full KernelTests run and final independent review receipt are recorded below when settled. The baseline already fails `BlendIntersectionTests.NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative` with expected Success / actual NotConverged; the reused guard test invokes the same check with spline reuse disabled and exposes the same failure. These failures remain visible and cannot be represented as passing. Existing rollback, allocation-failure, copy/source-deletion, undo and ownership tests are not removed or weakened.

Real PK facilities were used from this Mac: `parasolid-research` was read, the native MCP returned the actual fix-blends prototype/options and its 53-call graph; further queries checked `BLE_fix` → copy/fix dispatch, handed surface evaluation/normal/curvature dispatch and `REL_cu_iso_blend_evals` setup/derivative paths. An empty `BLE__fix` call graph was not interpreted as no calls; one disassembly request reported a server objdump architecture error and is not claimed as successful decompilation. These static call-graph facts do not prove PK uses this record algorithm or quantify PK's internal proof workload. The matching Mac PK38 sessions and strict same-input operations provide the dynamic evidence. Linux addresses were never applied to the Mac runtime. No decompiled vendor source is published.

Historical measurements remain unchanged. The 04c report's inclusive ZeroMemory attribution and suggested 60 repeated bounds per root do not establish exclusive cost or repeated keys. The 6bb single-session diagnostic counted 1,446,213 Jacobians, 224,299 bounds and about 503.9 ms in explicit cache clear, mostly seven-variable systems without UV; it covered the whole session, not Fix exclusively. Requested bytes are not memory-bandwidth cost.

## Reproduction, original artifacts and integration

```sh
# Prepare the fixed baseline worktree using the authorized private environment
# preparer before building. Keep both trees clean; the runner checks this.
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/NativeModelingWorkComparison.cs -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false -- --validate-qualification
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/NativeModelingWorkComparison.cs -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false -- --baseline ../temp_docs/pk-native-work-reduction/baseline-tree --output ../temp_docs/pk-native-work-reduction/a-new-output-directory
```

All runner path arguments are relative to its script directory. Original sample directories are never overwritten. The earlier runner attempt under `temp_docs/pk-native-work-reduction/controlled/` was stopped because its qualifier incorrectly required a mesh schema field on the root capture; it is unqualified and excluded from this report. Its original records remain unchanged. The formal run is `temp_docs/pk-native-work-reduction/controlled-final/report.json`.

- Portable derived operation/work report: `docs/native-modeling-work-reduction/operation-summary.json`; includes fixed source/runner/raw-report hashes and each operation's actual work records.
- Original twenty captures, oracle logs, every repetition's real meshes and final repetition's XT files: `temp_docs/pk-native-work-reduction/controlled-final/`.
- XT filenames inside each original oracle are overwritten per repetition. The copied original bytes are therefore explicitly bound to **repetition 2 only**; earlier repetitions retain their own capture/check/mesh evidence and do not inherit those final XT bytes.
- Raw full-suite and focused logs, raw exit receipts and TRX: `temp_docs/pk-native-work-reduction/`; the archive whitelist includes the relevant evidence and analysis script, not private dependencies.

Integration touches the existing bounds call sites/profile plus two new dedicated evaluation modules. If q2's correctness changes alter the original interval normal, residual or CHART rules, the corresponding recorded expression must be assessed against those actual new definitions before merging. This branch does not edit q2's tree or import the discarded SharedPlane work.

The code is committed and pushed to the work branch. Independent acceptance, integration and dashboard publication are separate states; the fixed archive and the q2 dashboard owner's publication receipt will be recorded when complete. Remaining work toward PK parity is dominated by the still-present 220,895 root cells and roughly 1.45 million Jacobian requests in this representative shortedge operation, plus the untouched spline/curved Boolean paths.

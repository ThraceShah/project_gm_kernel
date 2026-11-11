# Native modeling performance candidate 6761600: independent review

Review branch: `review/pk-performance-6761600`. This is the performance candidate review, not the separate computational candidate `f9c5920`, an integration approval, or a deployment record.

## Status and fixed sources

Source and independent Linux correctness review: no blocking implementation defect found within the stated applicability. The immutable Mac operation package and its 20 processes / 60 pairs have now passed independent raw-evidence audit, within the fixed-window scope below. **Final delivery acceptance still awaits the author's full-suite original settlement/TRX/exit receipt**; no full-suite success or PK-target achievement is granted.

| Role | Commit | `src` tree |
|---|---|---|
| Candidate | `6761600fef20602342cd9dc81bd4f7e4fb85d939` | `01e03aa9ed10a8145556872e98fa1677e17a4cb7` |
| Controlled baseline | `ecfff3aae1dbbe9dd48adbbdf2e8e635da735938` | `e314cc2307c5e77ea6f2b7685ba17836ae4ef5c8` |
| Before baseline instrumentation | `610e3ce844bf99c85af6069905f9f3deaab08133` | examined through the complete baseline diff |

The supplied “Native tree” hashes identify `src`, not `src/ProjectGmKernel.Native` (the latter hashes are respectively `ff0cbd00d2ff50bca5213c91cac21c1bfd325d21` and `813ba414c33155aa556ab28f12d55b04e7b81e4a`). Candidate production matches `03931f6`; `6761600` changes the comparison runner's capture qualification and final-repetition XT retention. Both source diffs, actual project/user instructions, `docs/Computational_Kernel_Architecture.md`, and `docs/Pk_Native_Modeling_Work_Reduction.md` were read.

## Mathematical and lifetime review

The reference is the unchanged `IntervalDual`, `IntervalNumber`, `IntervalVector`, `NestedBlendIntervalSupport`, and `SpineParameterMap` implementation. This is an interval evaluation refactoring, not a new geometric model. Official XT Format Reference chapter 6 confirms intersection ownership/supports/chart, CHART base parameter/scale, and BLENDED_EDGE's inherited spine parameter, angular section parameter, signed offsets and sense. Local Parasolid MCP actually returned `found:true` for `PK_BODY_check` and `PK_DEBUG_BODY_compare`; real PK 38.0.150 was also executed below. Linux static addresses were not used on another platform and no vendor material is reproduced here.

| Rule | Checked correspondence |
|---|---|
| Addition, subtraction and negation | Same ordered `a+b`, `a+(-b)` and negation as `IntervalDual`; no algebraic reassociation. |
| Multiplication | Value uses `a.Zero*b.Zero`; derivative remains `da*b.Value + a.Value*db`, in that order. |
| Division | Value uses the same interval division; derivative remains `(da*b.Value-a.Value*db)/Square(b.Value)`. Rebind recomputes both value and outward-rounded square divisor. |
| Square and sqrt | Same `IntervalNumber.Square` and `IntervalDual.Sqrt`; factor grouping is `(2*x.Value)*dx` and `dx/(2*sqrtValue)`, with the same rounded sqrt value and zero-bound handling. |
| Sin/cos | Uses the original trig enclosure; derivative factors are `cosValue` and `-sinValue`. Both factors are recomputed after a box changes. |
| Norm, cross, dot, unit normal | Original square/sqrt, component ordering, left-associated dot sum and division by norm remain. Sphere, cylinder, cone, torus and nested-blend expressions match their originals. |
| CHART and explicit child spine | Original frozen chart equation `dot(point-position,chord)-(station-parameter)/scale`; line, circle and ellipse retain native parameters, exact analytic position and tangent. |
| Independent derivatives | Dependency mask only chooses storage/computation. Missing seeds return the actual **zero-seed interval derivative**, including outward width or nonfinite behavior; they are not replaced by mathematical zero. |
| Guards | Finite checks on normals, explicit conic vectors and all outputs persist as marked nodes; positive cone-radius guard is marked and rebound. Derivatives of marked nodes are checked for finiteness for every dependent seed. Invariant literal checks occur during construction. |

Production records only when there are 7–15 variables, no UV on either side of any occurrence, and no BCurve spine in any occurrence. With `n=4*m-1`, that means 2–4 occurrences and actual dimensions 7, 11, 15. Spline hull/WithValue instructions are outside retained production records; the test hook rebuilds them for a changed box. BCurve, UV systems, three-variable systems and larger systems retain the existing directional algorithm. Explicit root curves retain `BoundExplicit` routing. These exclusions are intentional applicability boundaries, not claims of equivalent benefit for all models.

`BoundSegment` captures occurrences and chart indices once. Native parameter interval `t` remains fixed throughout the tube; each expansion/refinement changes only variable boxes. Each ready-record invocation visits all nodes in order, replacing every variable and recalculating every box-dependent primal, denominator, trig factor and guard. There is no cross-tube/program/command/branch cache, retained entity reference, mutation hook or rollback state.

Before `Ready`, count starts at zero and variables/instructions are fully appended. A residual failure leaves the record unready and the next attempt constructs it again. Capacity exhaustion sets count beyond capacity; it cannot publish output indices or `Ready`. Derivative capacity is checked before setting `Ready`. A derivative or ready-rebind failure may retain the expression structure, but the next call recomputes all values and writes every live derivative before publishing a matrix. The caller checks status before consuming matrix data. Partial scratch is not a successful result.

Nodes carry a 64-bit mask; production columns are at most 14, the unchanged maximum system has 63 columns, and the existing arithmetic test exercises column 62. Popcount offsets fit `capacity*n`. Maximum production capacity is `64+512*4=2112`: the straight-line no-UV expression branches have fewer than 512 appended instructions per occurrence, including attached literals. Actual node size is 72 bytes on this Linux runtime, so the extra maximum node/derivative storage is **658,944 bytes**, plus 252 bytes for the output span and small record locals; this is bounded stack memory, not reduced memory consumption or a general thread-stack guarantee. No C ABI layout/export/ownership contract changed.

The `SkipLocalsInit` write-before-read closure was checked in the actual source: successful `EvaluateCore` writes active snapshot/nodes/counts; chart indices, state, inverse columns, boxes, center residual, image and remainder are written over active prefixes. `Assemble` retains `matrix.Clear`; LU inverse RHS and nearest-box RHS retain active-prefix clears. The live spline cache still clears before any fill; no-UV consumers cannot access it. New record constructor initializes Length/Ready; append assigns complete nodes, output rows cover root's 3 plus each child's 4 equations, and derivative node order dominates operand reads. Unary's unused right operand points to initialized node zero. No root isolation, Krawczyk expansion/refinement count, contraction threshold, nearest-foot test, reach budget, tolerance or result validation was removed or relaxed.

## Baseline and oracle protocol review

The `610e3ce` → `ecfff3a` production change consists of `SkipLocalsInit`, the any-occurrence/both-side UV read-set guard, and optional work counters. All five oracle loops construct fresh inputs inside each repetition. New clocks/profile lines are outside original API intervals; capture uses its original API stopwatch, not broad `BENCH` construction/verification clocks. The optional counter hooks mutate only the current profile. Their overhead remains present in qualified profiled timings; counter reductions do not measure removed arithmetic instructions or exclusive CPU time.

The comparison runner requires identical oracle source bytes across trees, clean tracked sources, sequential processes and ABBA source order per case. Each process runs three fresh inputs: rep 0 process-cold, reps 1/2 warm (not independent cold processes). Fixed operation order is GM then PK for sphere/planar/curved Boolean, PK then GM for solidblend/shortedge. Shortedge retains the original PK seed, sends its unchanged input XT to GM, checks GM editable receive and fresh-input equivalence, and then calls both implementations independently. Importing a separate PK result for an editable-roundtrip check does not supply the GM modeling output.

The five scenarios are fixed successful operations, not an all-model performance qualification. Solidblend is rounding only. Shortedge uses its default assembly path; it does not qualify the separate event-arrangement computational candidate or any later Boolean combination. Outputs are checked independently per operation.

`WebProgressCapture` snapshots each repetition separately before/after each API, retains independent PK copies for paired checks, and records API error/result with step ownership. Its paired checks execute full `BODY_check` on GM fresh XT and PK, five structural counts, type/region material/shell membership and strict body comparison, and all 15 mass/centroid/inertia/area values. `ParasolidBodyCompare.AssertEquivalent` checks global and local result, permits only `n_cht_pts` diagnostics and frees the compare result. Actual effective masks must be inspected in each raw evidence file; `qualified:true` or a function return code alone is insufficient. Zero face-pair records do not mean zero compared faces or a failure; equal global-count records are not mismatches.

The runner overwrites oracle XT filenames per repetition, then copies current-run files with hashes and explicitly labels them **final repetition 2 only**. Earlier reps retain captures/meshes/API/quality checks, not individually retained XT. This is a scope limit of the artifact set. The qualifier requires three owners, six uniquely owned timings, zero errors, positive finite intervals and required pass IDs, but is not a substitute for auditing numeric/raw evidence or successful operation result enums.

## Independently executed on q2

Uniform private dependency `ensure --profile native` completed with `ENV_READY`, Linux x64, PK 38.0 and corpus present; PKToy was prepared at `d820719b6d8510d623972ab9d8efc65edbc312f9`. No Mac run was started and Linux timings are not compared with Mac. No AOT publication, Windows, 96/1885/PK347 campaign or full-suite rerun was performed.

| Execution | Raw exit | Actual result |
|---|---|---|
| Filtered `NestedBlendJacobianRecordTests`, `NestedBlendBoundsInitializationContractTests`, `NestedBlendCacheEmptyReadSetTests`, Release | 0 | 19 passed, 0 failed/skipped; includes sparse high-bit arithmetic and warmed zero-allocation assertion, live/empty UV cache, stack pollution and prior failure history. |
| `scripts/ReviewIntervalJacobian6761600.cs`, Release, `KernelTestHooks=true` | 0 | Independent arithmetic across scales and box changes; positive guard and singular-box rejection, recovery and capacity exhaustion; cold/rebound full Jacobians and actual certified bounds at 7/11/15 variables; unchanged unrecorded actual bounds at 19 and maximum 63 variables. Manufactured X/Z positions lie in certified bounds. |
| `scripts/NativeModelingWorkComparison.cs --validate-qualification` | 0 | Existing current-format positive and six negative/missing/ownership controls pass; not a benchmark run. |
| `scripts/ShortEdgeBlendOracle.cs --angle=1 --radius=0.5 --tolerance=1e-07 --bench-repeat=1 --bench-profile`, own fresh capture | 0 | Real PK 38.0.150, precision 1e-8; independent GM construction from original seed; fresh input, PK product and GM fresh XT full checks all zero faults; strict global/local results 21921/21931; finite ICurve and 15 mass values pass. |

Raw local logs, exits, TRX and fresh capture remain under `temp_docs/pk-performance-review/`; no original evidence was rewritten. Review script is reproducible with `MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ReviewIntervalJacobian6761600.cs -c Release -p:KernelTestHooks=true -p:UseSharedCompilation=false -p:BuildInParallel=false`. Filtered test execution used a private process group and cleaned only its own testhost processes.

| Original local evidence (relative to project root) | SHA-256 |
|---|---|
| `temp_docs/pk-performance-review/focused.log` | `e04b14a1a19cc886ee304e4015a9797ca16d7bf95c13b5de08e01b3622910fd0` |
| `temp_docs/pk-performance-review/focused.trx` | `935d67cf41d9778f0d2246622d222b5c625c672fedb00c5c02db7cf828d099b7` |
| `temp_docs/pk-performance-review/probe-max.log` | `a6310cd130502288174682da1e50927a7485cfa92eac924dda5c287b01e27786` |
| `temp_docs/pk-performance-review/qualifier.log` | `00c528e18a1060a9e1c8a6bcaaf689547d13c0b76f347c578294f598b9ef573b` |
| `temp_docs/pk-performance-review/fresh-shortedge.log` | `402d218e433115988633fc7bf125f1549787843d323f31b07c388214fb88fb70` |
| `temp_docs/pk-performance-review/fresh-shortedge/capture.json` | `2a22471747b984c37c8ec27e2ca2aa431e8a6bc13317f33e8077020ebe84f007` |
| `temp_docs/pk-performance-review/fresh-shortedge/evidence/compare-0.json` | `67be8b69d8e6e4ec8d63351f1a7dc093345796978718b75a5eb37fc47b11cdde` |
| `temp_docs/short-edge-blend-oracle/a1-r0.5-t1E-07-reverseFalse-default-self.x_t` | `49e7796c2bd9ac5c89dd82b278751892d8454284520db43a13ec4c273d4ecf7c` |

The independent fresh comparison's effective mask has max_diffs=1024, all_tests=false, acc_dev_tests=false, tol_dev_tests=true, and num/tol/class/sense/missing/non_match all true. Counts are 2 regions / 2 shells / 10 faces / 23 edges / 15 vertices. Finite ICurve comparison covers three edges, endpoint maximum about 3.32e-12 and length delta maximum about 4.70e-12; these checks are additional to body comparison. Original oracle's worst normalized mass value is about 3.97e-9, below 1e-7. This is a single correctness execution, not a speed measurement qualification.

The documented Mac baseline has `BlendIntersectionTests.NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative` returning `NotConverged`; its reuse-disabled wrapper exposes the same behavior. In this Linux filtered execution both tests passed. This platform/run difference does not erase historical Mac failures, prove their resolution, or establish full-suite success. The author's final suite and fixed baseline comparison remain to be audited.

## Final package/performance audit

The coordinator supplied `../gm-delivery-board/temp_docs/incoming/mac-performance/mac-modeling-performance-6761600-raw.tar.gz`. Its actual SHA-256 is `85be8a3e3d349cd2a755ecd7a67722746ec5cc9a391aaf389b79a6d294ce0333` (5,287,400 bytes). Independent TarReader traversal verified 556 regular-file/directory members, 36,370,821 uncompressed file bytes, no rooted/upward/link/device member, and every extracted file's bytes against its archive stream. It was extracted only to `temp_docs/pk-performance-review/mac-original/`; its script/test/doc snapshots did not overwrite the fixed source.

The raw report hash is `0b867277df0f35e5a9be369f7cf17fcd36308a2cbe9db5bea12d0dc080ece418`. It identifies Apple M4 / macOS 27.2 / Arm64 / .NET 10.0.11, SDK 10.0.400, PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`, PK 38.0.150 / precision 1e-8; fixed-source recording lasted 11:58:28–12:08:07 UTC on 2026-11-11. The archived runner hash `0bf60027909ce7464bb61f3b61397eb52ded7e7af885956a25dcc765c5ff525b` matches the actual fixed-source runner. Each sample's oracle hash matches the corresponding fixed oracle. All twenty log/capture hashes and all 44 retained final-repetition XT hashes match.

`scripts/ReviewPerformanceEvidence6761600.cs` completed with raw exit 0. It audits actual numeric/status/ownership data rather than trusting qualification flags, and emits `docs/Pk_Performance_6761600_Evidence_Audit.json` (SHA-256 `cad1d725334d9a2959971fc777129da52dfaff38d3e5c434db41704d20a23e8b`). It verified:

- 20 original child `Process.ExitCode` values of 0, no timeout or oracle failure text; 60 uniquely owned GM/PK pairs, exact raw/report API interval and ratio agreement, ABBA source order, fixed per-case implementation order and matching GM/PK operation parameters.
- Actual success result enums: Boolean `21650`, blend no-fault `18391`; zero API errors, two zero-fault full BODY checks per pair (120 total), five equal numeric structure counts per pair and the original equivalence check.
- All 60 raw strict evidence files have the required effective mask and valid global/local outcomes; equal global-count records and the narrow chart-point diagnostic rule are treated according to the actual PK result semantics.
- All 900 mass values were independently normalized from their raw GM/PK numbers and compared with the unchanged 1e-7 threshold; worst normalized difference is `5.239703452434893e-9`.
- 312 distinct, correctly owned before/after/tool mesh files, with actual faces/edge arrays, source commits and GM fresh-XT provenance. Output mesh face/edge counts agree with that repetition's raw structural counts. Spherical inputs correctly have no finite edges.
- All report work lines equal the original logs; requested Jacobians/directions and pair/SSI/root/budget/winding/bound counters agree across sources **at the same repetition index**. Warm/cold cache history can change those counts between repetition indices, so equality was not asserted across different reps. Every per-process profile API interval matches the corresponding captured GM interval to log-print precision.

The author's summary medians and paired-ratio medians independently reproduce exactly from raw captures. The full cold/warm arrays and actual work are in the derived audit; each source/case has two cold pairs and four warm pairs. Warm results are:

| Operation | Baseline GM ms | Candidate GM ms | Candidate PK ms | GM elapsed change | Candidate median of pairwise GM/PK |
|---|---:|---:|---:|---:|---:|
| Sphere subtract | 0.07575 | 0.07545 | 0.26760 | -0.40% | 0.28479x |
| Planar subtract | 0.59570 | 0.77640 | 1.10575 | **+30.33%** | 0.80082x |
| Curved block subtract | 917.27970 | 909.81715 | 1.85730 | -0.81% | 488.26694x |
| Plane/NURBS fillet | 1540.33615 | 1524.97405 | 1.43810 | -1.00% | 1064.27179x |
| Shortedge fillet | 22593.31015 | 14179.10690 | 1.49920 | **-37.24%** | **9465.56563x** |

Shortedge's observed warm speedup is **1.5934226541x**; its GM ranges are 22482.3177–22646.4120 ms before and 14145.5623–14332.6965 ms after. Cold GM medians are 23684.20885 → 15212.37110 ms and have their own distinct paired GM/PK ratio. The candidate warm paired ratio 9465.56563x is not the ratio of medians (9457.78208x). Process cold means its first operation, not a reset machine or comparable per-implementation JIT state; fixed call order and prior snapshot/receive work matter. Profiling is enabled in both trees. The outer background shell's exit was not captured separately; neither audit exit 0 nor the summary generator's exit is substituted for it.

Planar's observed increase remains visible: baseline warm 0.5930–0.6097 ms and candidate 0.5975–0.9793 ms have overlapping ranges and zero Jacobians. Its cause is unassigned; no repeated ABBA was run to select a passing window. Sphere/curved/solidblend's small changes do not establish material benefit. Author raw load strings support the disclosed approximate 3.51–11.09 one-minute load range, and 3.65–4.81 around shortedge; load average does not certify exclusive API CPU use.

For shortedge warm rep 1, original residual directions fall from 10,113,178 to 27,774, but candidate still executes 223,506 record-build attempts, 33,683,402 nodes and 1,217,266 rebind attempts, with 1,450,110 Jacobians and 10,113,418 requested directions. Both sources retain 4,956 SSI cells, 220,895 root cells, 3,518 roots, 196 winding calls, 275,525 bound requests, 223,331 bound evaluations, 30,727 hits and zero budget failures. These are operation counts, not 99.7% arithmetic/CPU removal.

Archived focused TRX contains twelve passing record tests. The added fifteen-variable capacity test belongs to author documentation/test head `9ddc2fbaecab4aee63e8fe7cc305ce30dc23c6b7`, not the formally measured test set at `6761600`; production tree remains identical. Its first run has an actual failed TRX and external exit 1: decimal `-.3` was outside the exact supplied plane coordinate `-.1*3` enclosure. The corrected test uses the actual supplied coefficient and has a passing TRX and external exit 0; no bound, tolerance or production source was changed. Both original records remain in the tar. The sparse-test TRX/log show twelve passes, but this tar has no separate sparse-test external-exit file; none is invented.

The earlier excluded `controlled/` attempt is not part of the formal 20/60 result; its stopped qualifier/schema mismatch remains historical, without inheriting formal qualification. The full-suite was running when this tar was sealed; its mutable log is intentionally absent. A separate immutable settlement/TRX/original-exit receipt is still required before this overall review is closed.

**Scoped package conclusion:** accept the immutable evidence for these five fixed independent operations and the observed shortedge elapsed improvement, subject to the stated warm/cold, profiling, small-sample and per-repetition artifact limits. The PK performance target remains unmet for curved Boolean, solidblend and shortedge. This does not sign general-model performance, downstream combinations, computational candidate `f9c5920`, full-suite success, integration or publication.

No implementation was repaired, no thresholds/expected results changed, no author evidence removed, and no deployment/merge/hostproxy action performed. The benchmark's own qualification must not be extended to general modeling, the computational candidate's known failures, or achievement of the PK performance target.

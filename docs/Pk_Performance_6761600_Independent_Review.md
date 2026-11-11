# Native modeling performance candidate 6761600: independent review

Review branch: `review/pk-performance-6761600`. This is the performance candidate review, not the separate computational candidate `f9c5920`, an integration approval, or a deployment record.

## Status and fixed sources

Source and independent Linux correctness review: no blocking implementation defect found within the stated applicability. Final artifact/performance acceptance is **pending receipt and audit of the author's immutable Mac package and full-suite original results**. Numbers supplied in the task are not yet independently accepted measurements.

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

Pending the coordinator-provided package location and hash. Required audit: immutable tar hash and member/source scope; report/log/capture/mesh/XT hashes and provenance; all 20 original process exits and 60 pairs; every rep's checks/effective masks/15 numeric properties and owners; recomputed ABBA cold/warm distributions and GM/PK ratios; work counts and author suite raw results/baseline failures. No complete artifact acceptance is granted at this checkpoint.

The task supplied preliminary shortedge warm medians 22593.3101 → 14179.1069 ms (1.5934x, 37.24% less) and candidate pairwise GM/PK median 9465.57x. These remain supplied, unaudited values here until raw-package review. They cannot support an “exceeds PK” claim. Supplied planar movement .5957 → .7764 ms (about +30.33%, overlapping four-value ranges) must remain visible as unassigned variation/regression; sphere/curved/solidblend near -0.40/-0.81/-1% do not establish material benefit. Residual-direction count reduction must retain graph construction/node/rebind work and cannot be reported as 99.7% arithmetic/CPU removal.

No implementation was repaired, no thresholds/expected results changed, no author evidence removed, and no deployment/merge/hostproxy action performed. The benchmark's own qualification must not be extended to general modeling, the computational candidate's known failures, or achievement of the PK performance target.

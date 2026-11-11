# PK native modeling delivery

## Scope and source

This delivery starts from `610e3ce844bf99c85af6069905f9f3deaab08133` on
`feat/pk-native-modeling-delivery`. The native model remains XT, including
Parasolid BLEND, INTERSECTION/CHART parameters, finite trims, ownership and senses.
No SharedPlane or R3 support-frame change is imported.

The actual user-level and project instructions, computational/intersection
architectures, original M0–M5 plan, acceptance and handoff were read. The handoff's
completion claim does not establish current acceptance; new results below are
operation-specific. The private environment tool returned `ENV_READY` before
building (Linux x64, PK 38.0, PKToy `d820719`). No C ABI changes or AOT builds.

## Native definition and selected implementation

The official XT Format Reference, chapter 6, CHART/LIMIT definitions gives
`f_i=f_(i-1)*cos(b_i)/cos(a_i)`, `t_(i+1)=t_i+C_i*f_i` and chord-projection
parameters. Help limits locate a closed curve; they do not establish a constant
parameter period. MCP calls on this machine verified the actual
`PK_CURVE_eval`/`PK_CURVE_parameterise_vector` prototypes and error 157
(`PK_ERROR_cant_do_intersect`), and inspected the two requested internal routines.
Symbol discovery alone is not used as behavioral proof.

Selected diffs from `c301b40`, `2bdd0f3`, `f5642f2` and `3344c53` replace the
sample-only compaction check with finite interval branch certification, preserve
the original support UV sheet, and retain an already identified finite source
root's support/line constraint at an analytic contact. The key subnormal CHART
`ScaleB`/`TryPlanePoint` routines from `a5adfc0` are already in the starting
source; this does not claim that the entire commit is its ancestor or identical.
The R3 frame and unrelated diagnostic
files are excluded. Compaction does not change the native chart equation or
permit an uncertified compact representation.

## Actual evidence

Evidence under `temp_docs/pk-native-delivery/` is ignored local output. Each new
run uses a fresh name; historical measurements are not overwritten.

| Execution | Source | Actual result |
| --- | --- | --- |
| `baseline.log`, `baseline.exit` | clean `610e3ce` computation source, consumer oracle from fixed `3344c53` | exit 0; self Fix 0/two products; same-input GM/PK two-tool subtract both 0/success/one body; fresh receive/full checks 0 faults; strict compare passes; normalized15 mass `7.701450779942389e-11` |
| `interval-source-root.log`, `.exit`, `.diff` | selected interval/source-root integration | exit 0, 27/27 certificate and finite source-root tests; original stationary endpoint and reload checks pass |
| `consumer-forward.log`, `.exit` | selected integration and strengthened success qualifier | exit 0; self Fix 0/two products; both Boolean operations 0/success/one body; projected replacement reached; strict/full0; mass `7.701450779942389e-11` |
| `native-chart.log`, `.exit` | integrated computation, explicit native seam/inverse probes | exit 0; F2/F3 native CHART evaluation/inverse agrees on both sides of closure; endpoints canonicalize to the first parameter in both implementations |
| `standard.log`, `.exit` | integrated computation and isolated output directory | exit 0; independent self Fix, copy/delete source, reflection/scale, first subtract, editable XT reload and second subtract; strict/full/finite/UV/material and original mass pass |
| `consumer-reverse.log`, `.exit` | compiled integrated runner, reverse marking order | exit 0; same-input strict/full/mass, one successful body each |
| `consumer-inject.log`, `.exit` | compiled integrated runner, allocation injection | exit 0; projected replacement reached, GM 901/failed/zero bodies, all three inputs byte-identical after rollback; same-input PK subtract succeeds/full0 |
| `transverse.log`, `.exit` | integrated computation | exit 134; self Fix and both actual subtracts publish, strict/full/finite/native witness/material pass; original raw inertia gate fails on the two self cuts, independently |
| `probes-self.log`, `.exit` | all original 510 coordinates | exit 134; 15 differing rows, zero API errors, zero changed original native classifications |
| `probes-import.log`, `.exit` | all original 510 coordinates on imported product | exit 134; 16 differing rows, zero changed original native classifications |
| `full.log`, `.exit`, `.head`, `.diff` | `2922ff5` plus the then-recorded runner diff, same computation source as `a7cf09f`/`962180d` | exit 0, 1885/1885, 19m31s; own-PGID testhost cleanup only |
| `regular-bore.log`, `.exit` | integrated computation | exit 0; modified NURBS/plane support retains bore, independent fillet/lifecycle/subsequent cut, strict/full/mass |
| `support-chain.log`, `.exit` | integrated computation | exit 0; separate equivalent support records, independent fillet/lifecycle/subsequent cut, strict/full/mass |
| `input-rigid.log`, `.exit` | input-first rigid transform, phase .31, radius .6 | exit 0; self construction, finite face domains, strict/full and raw mass `6.067012969047577e-9` |
| `input-mirror.log`, `.exit` | input-first mirror and scale 1.7, reversed selection | exit 0; self construction, finite face domains, strict/full and raw mass `1.9710593449487946e-8` |
| `comparator-controls.log`, `.exit` | current shared strict comparator | exit 0; 6 positives and 5 wrong-size/trim/interior/pole negatives |

The clean baseline Boolean success is a new observation, distinct from the
reviewed failure on the old R3-based branch. Its actual target hash is
`49e7796c2bd9ac5c89dd82b278751892d8454284520db43a13ec4c273d4ecf7c`;
tool hashes are recorded by the oracle in
`temp_docs/contract-sol/native-baseline/inputs.json`. This is not evidence that
all Boolean inputs or remaining M0–M5 gates pass.

The consumer oracle's success gate requires API error 0,
Boolean success result, exactly one non-null body and nonempty output XT before
strict/full/mass checks. Failure-result, zero-body, API-error and null-body
negative controls guard against vacuous success. The injected allocation failure
remains a separate reached-callback/901/three-input byte rollback gate.

The live nonuniform CHART control covers two distinct parameter scales. F2 has
limits `[-0.25,9.152884599910056]`; genuine PK maps its closing point to `-0.25`,
as does GM. F3 has limits `[0,0.0061216696613997775]` and both inverses return `0`
at closure. Interior inverse parameters agree to rounding and position errors
are at most `4.584e-15`. These are actual native calls, not a claim that their
parameter spans can be used as additive periods for arbitrary closed ICurves.
Terminator/high-order/BlendBound gates explicitly reported NotRun by the older
evaluation oracle remain NotRun, not modeling acceptance.

`ShortEdgeBlendOracle.cs --output-name=<new-name>` now isolates each execution's
actual XT artifacts in a new subdirectory and refuses reuse of that directory.
This prevents later variants from overwriting earlier execution evidence.

## Current unresolved gates

The transverse first/second cut residuals are `1.2044611267910417e-7` and
`1.126468644220207e-7`, both in central `inertia.yz`, at the second original
requested accuracy. Both complete the same lifecycle as the passing standard
run. Native cut witnesses identify two real short occurrences, four coedge uses
and zero spatial reuses. Cutter/Blend and cutter/conic witnesses each retain four
coedge uses. All 48 separated material probes agree.

To check whether the raw point criterion alone distinguishes an incorrect
model, the independently constructed native target was reused byte-for-byte
(`bd75b7926956c1506e00c172816a0bfc1bb07db1660d74da65aaf1734f219ef1`).
`native-rechart.log` exits 0 as a **diagnostic**, not a modeling acceptance:
inserting genuine native curve samples changes its CHART from 10 to 73 points;
both actual receives pass full BODY_check and strict comparison with only the
permitted chart-count diagnostic, but native raw normalized inertia differs by
`1.5230746449408703e-7` at both original accuracies and at `1.0`.
`native-finite-pair.log` (exit 0, diagnostic only) reports stable finite-trim
moment agreement `2.3322955033568897e-14` and native modulus overlap 15/15.
Neither observation replaces the unchanged original gate or proves arbitrary
models equivalent. Official Mass Properties section 31.4.1 recommends retaining
error bounds for such comparisons. Historical rejection controls and their
limitations remain in the original acceptance record; no alternative gate is
adopted here.

This is a comparison between the original bytes and **different recharted
bytes**, not repeated mass measurement of one byte-identical model. The
supports, original nodes, topology and precision/error fields are retained;
new native samples and paired UVs change the CHART's projection gauges and can
change its reconstructed native parameter range. The finite-pair diagnostic
integrates each receive's actual finite limits and CHART cells separately.
Numerical convergence and strict/full checks do not alone establish exact
invariance of its physical properties or prove a PK implementation error.
The actual first cause of the transverse raw discrepancy is not settled here.

The earlier `native-control.log` exits 134: the opt-in `--finite-controls`
assumed that *both* refinements of this unreflected input reproduce a raw
failure, but its first 10-to-37 refinement gives `4.174732062510507e-8`.
That is a failed diagnostic assumption, not a body/modeling failure. The normal
two-refinement diagnostic and separate finite-pair check above retain this
distinction. Their actual refined XT bytes are now preserved under
`temp_docs/native-chart-control/` without rewriting input evidence.

## Remaining work

Run the integrated same-input Boolean, complete standard/transverse lifecycle,
input/parameter/direction variants and affected regression; retain and resolve
actual failures without coordinate changes or tolerance relaxation. Replay all
510 original probes with their original classifications. A full completion
candidate requires independent acceptance and the dashboard owner's new live run;
no such completion is claimed here.

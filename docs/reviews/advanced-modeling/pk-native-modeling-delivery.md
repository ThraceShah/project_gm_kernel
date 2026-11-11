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
root's support/line constraint at an analytic contact. The subnormal CHART repair
`a5adfc0` is already in the starting source. The R3 frame and unrelated diagnostic
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

## Remaining work

Run the integrated same-input Boolean, complete standard/transverse lifecycle,
input/parameter/direction variants and affected regression; retain and resolve
actual failures without coordinate changes or tolerance relaxation. Replay all
510 original probes with their original classifications. A full completion
candidate requires independent acceptance and the dashboard owner's new live run;
no such completion is claimed here.

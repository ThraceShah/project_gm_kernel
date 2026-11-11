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
| `intersections.log`, `.exit` | integrated computation | exit 0, 116 independent intersection contract cases; the separately reported native corrupt-wire boundary is not counted as passed compatibility |
| `analytic-local.log`, `.exit` | guarded axial arrangement selection | exit 0, 5/5 cylinder fillet/chamfer cases; the flags select five cases, not the whole local-operation catalog |
| `separated-network.log`, `.exit` | integrated computation | exit 0; independent nonadjacent bands, copy/delete source, two subsequent cuts separated by editable XT reload |
| `corner.log`, `.exit` | self-authored base/bored/rigid/mirror-scale/zero-single/zero-double/zero-all inputs | exit 0, all seven independent PK comparisons, including sphere-only extinction and reload/delete/classification |
| `matrix-shard-0` through `matrix-shard-7`, each `.log/.exit/.start/.end/.runner-source.sha256` | fixed computation and final sharded runner | all eight external exits 0; 96 unique cases, each managed Fix/full0/strict/finite ICurve/paired UV/mapped geometry/original raw mass passes |

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

These transverse comparisons continue from **independently constructed** native
and self Fix products, rather than from one identical serialized target. Before
the first cut, the self reflected target is
`57eecc0294734c805d3ded1ab034fc4c78dec6da6d2300a6ce85a0e553981ad2`;
the independent native reflected target is
`3047cc3e0d7366fdb655dfae534ad9bc0fe32183443f55cd333923ba891e6a99`.
They are the `-copy-reflected.x_t` and `-copy-reflected-native.x_t` artifacts in
the transverse directory. Both upstream Fix/copy comparisons pass their own
gates. The separate two-tool consumer uses exactly the same GM target/tool
bytes on both sides; its passing result does not qualify the transverse raw
failure or silently replace either transverse input.

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

The read-only `transverse-chart-fields-final.log` (exit 0) binds its two actual
inputs to SHA-256 and shows the native and GM CHART declarations. Native has
five charts with base 0/scale 1 and counts 2/2/2/2/6. GM has the same count
multiset with its retained native finite gauges, including base
`0.05326467231747403`, scale `1.0062203492663895` on the six-node carrier.
The long carrier's chordal declarations are approximately `.047664` native and
`.047405` GM; two other native declarations `.060254` are larger than GM's
`.052898`. These observations do not prove the retained gauge invalid or
overestimated GM declarations the cause. The official XT equation permits an
explicit base/scale; neither world coordinates nor native gauges are changed
just to satisfy a mass point. The first reader attempt failed compilation
(`transverse-chart-fields.log`, exit 1) before any geometry execution; it used
the wrong node-index property and was corrected in the separately saved run.

The earlier `native-control.log` exits 134: the opt-in `--finite-controls`
assumed that *both* refinements of this unreflected input reproduce a raw
failure, but its first 10-to-37 refinement gives `4.174732062510507e-8`.
That is a failed diagnostic assumption, not a body/modeling failure. The normal
two-refinement diagnostic and separate finite-pair check above retain this
distinction. Their actual refined XT bytes are now preserved under
`temp_docs/native-chart-control/` without rewriting input evidence.

## Remaining work

The complete requested operation families have current computational evidence;
the original acceptance remains incomplete at the two transverse raw inertia
gates and the 15/16 exact-probe differences. No API-error modeling failure or
managed suite failure remains in the executed scope. The user is leading the
separate inertia investigation; this task does not take it over. No new modeling
experiment is started while that finding is pending.

The 96-case run uses angles `.25/.5/1/2`, radii `.25/.5/.6`, tolerances
`1e-7/1e-5`, both mark orders and both update modes. Eight shards partition the
same enumeration by ordinal; every shard executes twelve cases. Their actual
runner source SHA-256 is
`af77db29008f9751c2a159e69d26ddc08b7ca6350baffc53d6c5a9cf20e99828`,
matching the committed runner. All 96 cases have a self full-check record and
original raw-mass result; no `FAIL` line occurs. These runs include `--geometry`;
they do not claim the separately optional native-fin-box/face-domain checks ran
on every matrix member. Those stronger checks ran on the representative
transverse lifecycle and input variants as named above. The initial serial
matrix is separately preserved with exit **143** after intentional termination
to partition the workload; it is not a full run or counted toward the 96.

The current full regression verifies 175 dispatch descriptors and the allocation
IL guard is CLEAN across 2932 methods. No NativeAOT, public C ABI change, Mac
performance run, main integration or publishing occurred. No test or oracle from
this task remains executing.

The configured Parasolid tools used the installed stdio service for the focused
research calls. The specifically requested Tailnet HTTP MCP endpoint was also
actually called: `mcp-http-direct-error157.json` contains a successful
`tools/call` result with `found:true`, code 157, external exit 0. The first
proxy-routed transport attempt has its own exit 35 and is not called successful;
the direct call retained normal TLS and changed no global configuration.

## Reproducible commands and provenance

Run from the project root. All compilation invocations use
`MSBUILDDISABLENODEREUSE=1`; the shared flags below are
`-c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true`.
Use a new output name for every invocation.

| Execution | Runner and arguments after the shared flags |
| --- | --- |
| Strict same-input Boolean | `dotnet run scripts/RollingBooleanConsumerOracle.cs -p:KernelTestHooks=true -- new-consumer-name`; add `--reverse` or `--inject` for the separate order/rollback cases |
| Standard full lifecycle | `dotnet run scripts/ShortEdgeBlendOracle.cs -- --output-name=new-standard --event-arrangement --geometry --lifecycle --proof-only-lifecycle --material-ownership` |
| Current transverse failure | `dotnet run scripts/ShortEdgeBlendOracle.cs -- --output-name=new-transverse --event-arrangement --geometry --lifecycle --material-ownership --require-native-cut-witness --require-cutter-chart-witness --reload-before-local-cut --transverse-local-cut --require-native-fin-uvbox --require-native-face-domains` |
| Matrix, each `i` from 0 to 7 | `dotnet run scripts/ShortEdgeBlendOracle.cs -- --output-name=new-matrix-i --event-arrangement --matrix --both-updates --geometry --case-shard=i/8` |
| Self/imported 510 replay | `dotnet run scripts/ShortEdgeBlendOracle.cs -- --output-name=new-probes --event-arrangement --self-saved-probes --diagnostics`; the separate imported command substitutes `--import-only --saved-probes` for `--self-saved-probes` |
| Native CHART evaluation | `dotnet run scripts/IcurveEvaluationOracle.cs` |
| Shared strict comparator controls | `dotnet run scripts/ParasolidBodyCompareOracle.cs` |
| Independent intersections | `dotnet run scripts/IntersectionOperationOracle.cs` |
| Full regression | `dotnet test tests/KernelTests -m:1 --logger 'console;verbosity=normal'` |

The first consumer and each new script were compiled with these commands.
Some subsequent consumer executions and shards 1–7 used the already compiled
apphost from that same runner source; the command table is a reproducible source
invocation, not a claim that every process rebuilt. `full.head/full.diff` records
the original regression snapshot; later commits change runner/reporting only,
not its tested computation source. UTC `.start/.end` files exist for the later
executions. Earlier baseline/standard/native-evaluation starts were not captured
as UTC fields; filesystem times are not substituted for measured start times.

Actual raw inputs/results, logs and original exits are indexed by
`temp_docs/pk-native-delivery/evidence-final.sha256`, whose digest is
`94752804be4ac709fbb4c1104b110b754787121c79a695b6f39723a5a12898c5`.
`final-runner-sources.sha256`, `environment.txt`, `environment-final-check.log`
and `matrix-96-cases.txt` provide the runner, platform and unique case inventory.
The final environment check exits 0/ENV_READY. The matrix and later runs have
exact per-process UTC files; original exits are never inferred from a checker.
The separately copied user investigation note remains a historical snapshot;
its then-in-progress matrix statement is superseded by the settled results here.

The same-input Boolean's `inputs.json` records target
`37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`,
tool hashes and the actual option-byte hash. Its actual GM/PK result XT hashes
are in `outputs.sha256`. The two transverse failing pairs and rechart controls
have exact filenames, full hashes and actual commands in the copied
`temp_docs/pk-native-delivery/inertia-handoff.txt`; that note does not qualify
their failed point gate or change the original coordinates.

## Dashboard delivery checklist

- Source candidate: current pushed delivery branch; computation changes are the
  fixed `2922ff5` integration, with later runner/report commits listed in Git.
- M0–M4 evidence: the operation table above, guarded shared boundary provenance,
  actual self products, fresh native receives, native masks/full checks, finite
  branches/incidence, source deletion, transforms, reload, subsequent cuts and
  injected rollback. The shared comparator retains max_diffs 1024, all_tests
  false, acc_dev_tests false, tol_dev_tests true and non_match_tests true; only
  n_cht_pts is diagnostic.
- M5: full 1885, independent 116 and 96-case self matrix pass; original
  transverse inertia and exact 510 equality remain red. These are distinct
  gates and may not overwrite the independently passed upstream Fix results.
- Wire comparison scope: the 116-case intersection runner's manual body mask
  uses max_diffs 64 and non_match_tests false; it separately requires the shared
  finite `AssertWireGeometry` matching plus parameter/type/branch and directed
  traversal checks. This differs from the solid-body shared mask above and is
  not represented as an identical mask. Its explicitly separated corrupt native
  wire is a native limitation, not a qualified success case.
- Display: no new same-instance DisplayBody receipt/mesh package was produced by
  this worker. The board owner can collect the necessary representative using
  the fixed accepted source and the standard lifecycle command, with a new
  `GM_WEB_CAPTURE` destination and output name. The capture's own operation,
  receipt, XT/mesh hashes and UTC must bind to that new run; old 610 data cannot
  stand in for it. XT bodies, vendor binaries and credentials must remain private.
- Performance: Linux diagnostic timings are not performance qualification; the
  separate Mac owner supplies same-host raw samples. Missing ratios stay null.
- Acceptance/publishing: request one critical independent review of this fixed
  whole candidate. It is not independently accepted, integrated or live yet.
  The coordinator and current board owner arrange the new online run after the
  accepted scope is explicit; original unresolved gates remain visible.

Copyable standard command:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ShortEdgeBlendOracle.cs -c Release \
  -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- \
  --output-name=new-standard --event-arrangement --geometry --lifecycle \
  --proof-only-lifecycle --material-ownership
```

Copyable matrix command (repeat with `i=0` through `7` and distinct names;
the eight actual original exits must each be recorded):

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ShortEdgeBlendOracle.cs -c Release \
  -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- \
  --output-name=new-matrix-0 --event-arrangement --matrix --both-updates \
  --geometry --case-shard=0/8
```

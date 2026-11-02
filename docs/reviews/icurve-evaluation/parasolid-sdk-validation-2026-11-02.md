# Parasolid SDK validation and real wire round-trip fixes

Date: 2026-11-02 UTC. Starting revision: `896e7e1b4824796f7312ebb1e27b2eb55a82968a`.

## Outcome

The supplied private SDK is usable in the Linux x64 workspace. Its dynamic
library loads, a real Parasolid session starts, and the runtime reports
**38.0.150**. The complete dependency directory contains **101 external schema
files**, including `sch_37102.sch_txt`. Combining those with the checked-in
built-in identities gives the expected 102-entry registry.

Restoring these dependencies alone changed the original 719-test suite from
690 passes / 29 failures to **719 passes / 0 failures**. The 29 earlier failures
were missing-schema or incomplete-catalog failures in this environment.

Actual SDK execution then exposed a wire topology import/export gap, a scratch
allocation failure cleanup bug, and two mistakes in the new Oracle harness.
After their fixes and 15 new regressions, the full suite passes **734/734**, with
no skipped tests. All six preserved/fresh B-surface wire structure round trips
also pass real PK receive and `PK_DEBUG_BODY_compare`.

**The complete B-surface Oracle still exits 1.** Its strict full-vector D2
comparison reproduces the already documented GATE-D contract difference.
D0/D1 and the normal D2 component agree closely, but that does not constitute
full D2 compatibility. The numerical implementation and comparison thresholds
were not changed to conceal this failure.

SDK binaries, archives, headers and schema texts remain outside tracked source
in the existing ignored dependency layout. The only SDK-produced geometry
added to the tests is the small manufactured wire fixture.

## Environment and dependency restoration

- Debian 13, Linux x64.
- .NET SDK 10.0.403, .NET runtime 10.0.14.
- Real Parasolid 38.0.150; Oracle wire transmit target 371.
- Library: `third_party/parasolid/lib/linux-x64/libpskernel.so`.
- Schemas: `third_party/parasolid/schema`.

All five supplied archive volumes matched their reported byte lengths. Their
7z member and nested tar archive were inspected before extraction. Only Linux
x64 libraries, headers and the full schema set were extracted. All ELF shared
library dependencies resolved on this host.

## Fixes

### Real PK wire topology

The real `PK_CURVE_make_wire_body` result has one void region and one faceless
shell. The shell owns the single edge. The earlier materializer only accepted
an edge directly owned by the body, so a valid real wire fell back to opaque
storage and exposed no executable edges.

`KernelRuntime.IcurveWireXt.cs` now validates and materializes this additional
narrow graph. It checks the body/region/shell/edge owner links in both
directions, creates and publishes the region and shell, and keeps the body
edge/vertex iteration chains. Extra regions, extra shells, non-void regions or
faces remain outside this materializer. Malformed owner links are invalid
input rather than successful materialization.

Fresh `XtWriter` output records:

- `BODY.shell` and `BODY.region`, with `BODY.edge = 0`.
- `SHELL.body`, `SHELL.region` and the unique `SHELL.edge`.
- `EDGE.owner = SHELL`.

The previous direct-body wire subset remains supported. This change does not
claim general B-rep import or complete wire support in every topology-query
API.

### Scratch ownership after allocation failure

`EnsureCommandScratch` previously returned `true` after attempting `Bind`, even
when allocation had failed and no scratch arena had been bound. A first-
allocation failure therefore reached a cleanup path that indexed a nonexistent
scratch table and threw `NullReferenceException`.

The helper now returns `CommandScratch.IsBound`. Callers only release scratch
they actually acquired. Empty workspace still returns `WorkspaceTooSmall`,
which public receive maps to `PK_ERROR_memory_full`. The fault-injection test
that reproduced the exception passes without changing the injected failure.

### Oracle SDK options and comparison results

The SDK marks `PK_SURF_intersect_surf_o_t.tolerance` as reserved for future use.
Setting it to `1e-8` caused error 5000 (`PK_ERROR_not_implemented`). Keeping the
original UV boxes while restoring the SDK-default options made the producer
succeed. This does not change the Oracle's D0/D1/D2 comparison tolerances.

`PK_DEBUG_BODY_compare` also returns topology count entries in `global_diffs`
when its global and local result flags both report `no_diffs`. On these wires
it returns six equal-count entries. Treating the array length as a difference
count incorrectly rejected matching bodies. The harness now requires both
explicit `no_diffs` flags and no local differences, while retaining the
independent topology/type checks and logging all count entries.

Finite jet mismatches are collected so the remaining cases and fresh exports
still execute. Any such mismatch keeps the complete Oracle failing. A
structure-only pass line is explicitly labeled as such.

## Verification

| Check | Result |
|---|---|
| Original suite after dependency restoration | 719 passed, 0 failed, 0 skipped |
| Final suite, including 15 wire regressions | 734 passed, 0 failed, 0 skipped; exit 0 |
| Allocation IL guard | 1,293 methods, CLEAN |
| Function catalog | 83 implemented dispatch descriptors verified |
| Existing analytic ICurve Oracle | Cases A, A2, B, C, D, E, F, F2, F3 passed; exit 0 |
| New B-surface wire structure round trips | 6/6 passed, including real receive and body compare |
| New B-surface Oracle full D0/D1/D2 contract | FAIL / exit 1, full-vector D2 difference retained |

The new tests cover actual real-PK XT receive, executable evaluation,
independent support equations and their first/second derivatives, removal of
the source document before transmit, reconstruction of the shell ownership,
re-receive, deletion and resource counts. Negative cases cover malformed
ownership, extra topology and four allocation-failure positions. Region,
shell, fin, geometry payload and tag counts are included in cleanup checks.

The missing old Case C fixture was generated with only
`icurve.bsurf-cylinder.depth1`; Case C subsequently ran and passed. The old
script still explicitly reports its separate shared-dependency full-hydrate
probe and GATE-T/B/A/high-order probes as `NotRun`. The new open-wire Oracle
provides actual public receive/evaluation evidence for its narrower graph.

### Real B-surface measurements

Each pair was sampled at 49 native parameters in cold and reversed warm order,
against the real producer and both real-PK re-received exports. The complete
selected edge interval was retained. The fresh pass drops the received XT
association and therefore reconstructs from runtime geometry.

| Pair | Maximum D0 error | Maximum D1 error | Maximum full D2 error | Maximum normal D2 mismatch |
|---|---:|---:|---:|---:|
| B-surface / cylinder | 1.64e-14 | 3.00e-15 | 8.66e-2 | 2.24e-15 |
| Cylinder / B-surface | 5.25e-15 | 2.84e-15 | 8.66e-2 | 2.04e-15 |
| B-surface / B-surface | 3.09e-14 | 9.89e-15 | 2.11e-1 | 4.43e-14 |

D0/D1/full-D2 errors use the original component normalization
`abs(actual - reference) / max(1, abs(reference))`. The normal mismatch is an
absolute vector norm after removing the component along the reference unit
tangent; it is diagnostic and does not replace the full-vector gate.

All six structure passes have one region, one shell, no faces, one edge and
two vertices. Both PK body-compare result flags report `no_diffs`.

### D2 contract evidence

For the B-surface/cylinder specimen, a separate nine-point diagnostic with
three decreasing difference steps found that PK's own D1 derivative converges
to the managed strict parameter D2. PK's returned D2 instead agrees with its
normal projection. This matches the existing design specification section
16.2 and GATE-D observation; it is not a newly asserted universal rule for all
Parasolid curve classes or versions.

At native parameter `7.478714950939597`:

```text
managed D2 = (0.3846426027321982, -0.3290914287507854, 0.07856634649669214)
PK D2      = (0.41329576781025407, -0.28845183248536427, 0.07701249677205291)
tangential difference = -0.049749322634318485
normal remainder      = 6.674335893081556e-16
```

With difference step `5.754782330933783e-5`, the norm of
`finite_difference(PK D1) - managed D2` is `2.6439142113648875e-10`, while
`finite_difference(PK D1) - PK D2` is approximately `0.0497493226540`.

The implementation continues to return the strict derivative required by the
current internal chart specification. Complete public PK D2 compatibility is
still open and must be addressed as an explicit API contract decision; these
results must not be summarized as full Parasolid compatibility.

## Reproduction and evidence

With the private SDK in the dependency layout above:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -m:1 -p:UseSharedCompilation=false
test_exit=$?
pkill -f '[t]esthost.dll' 2>/dev/null || true
exit "$test_exit"
```

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ParasolidXtCorpusCases/ICurveSurfaceMatrix.cs -- --case icurve.bsurf-cylinder.depth1
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/IcurveEvaluationOracle.cs
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ParametricICurveOracle.cs
```

The final command currently exits 1 because the full D2 gate remains failing.
Captured final suite/Oracle output and the independent D2 diagnostic are in
`docs/reviews/icurve-evaluation/parasolid-sdk-validation-2026-11-02-output.txt`.

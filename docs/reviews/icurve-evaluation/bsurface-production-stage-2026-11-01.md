# B-surface ICurve production stage

Date: 2026-11-01. Baseline: `a5adfc03614e9cf3f0d6b94f4ac65b82bd123f16`.

Follow-up on 2026-11-02: the private SDK is now available. The full suite passes
734/734 after the real wire topology and allocation-unwind fixes; all six real
wire structure round trips pass. Complete raw D2 compatibility remains failing
under GATE-D. See
`docs/reviews/icurve-evaluation/parasolid-sdk-validation-2026-11-02.md`.
The dependency-blocked evidence below records the original stage run.

## Delivered scope

This increment completes the regular parametric-support portion of M1 and its
runtime/cache/XT path. It does not close GATE-A, GATE-B, GATE-T or GATE-D, or add
a public surface-intersection construction API.

| Support pair | Auto | Explicit plans | Published orders |
|---|---|---|---|
| B-surface / analytic, either source order | P2 | P2, P4 | D0, D1, D2 |
| B-surface / B-surface | P4 | P4 | D0, D1, D2 |
| Analytic / analytic | Existing I1/I2 selection | Existing five plans | Existing D0–D2 contract |

The B-surface side must have original finite UV witnesses at its chart anchors.
Analytic sides can recover missing UV using the existing closed-form inverse.
Original positions, parameters, support ordering, surface senses and nullable XT
UV/error slots remain defining data. An exact chart D0 returns the imported point.
Chart-node derivatives use the selected original segment and are not averaged.

### Evaluation and publication

`ICurveSupportView` borrows validated poles, knots and a command-scratch workspace.
Only value-type `SampleWitness` data enter the caches. B-surface evaluation uses
`EvaluateInDomain`; non-periodic UV values outside the knot domain never use the
public surface evaluator's linear extension. Periodic witness lifting uses the
actual knot-domain period.

`ParametricICurveEvaluation` maintains both support UV pairs. Mixed P2 chooses
the B-surface as the parametric side while retaining the source support order.
P4 uses the full parametric position equality plus the original chart plane.
Bounded, damped Newton steps and adaptive continuation share one evaluation
budget. UV step bounds use the knot cells actually traversed. Regular interval
queries are independently tracked from both original chart ends; their roots
must agree in position, UV preimage and requested jets.

UV agreement uses local floating-point resolution, not a fixed fraction of the
entire knot domain or only a physical position tolerance. A regression constructs
distinct parallel branches separated by `2e-14` in model space and `2e-7` in UV;
two anchors on different branches must report `AmbiguousBranch`. Each branch with
its own coherent chart remains evaluable.

Derivative agreement also accounts for roundoff at a true zero derivative. A
rationally parameterized straight line can recover D2 by cancellation; comparing
only relative to that near-zero result incorrectly rejects the same branch.
The original relative threshold is retained with a machine-roundoff floor scaled
in native-parameter units. P2 and P4 regressions check the exact straight-line
D0/D1 and zero D2 without relaxing the UV-preimage or position checks.

Publication independently checks the full four-equation system, oriented
tangents and the physical size of a Newton correction. A small algebraic
residual alone is insufficient. Column equilibration removes arbitrary UV-unit
scales from the linear rank test. The final factorization is reused for the
position-quality check and analytic D1/D2 chain rules. No finite differences or
invented B-surface implicit equation are used.

Forced plans stay forced. Auto can diagnose a P2 failure and retry P4 within the
same budget. L2 and L3 retain distinct validated P2/P4 entries, including their
actual UV witnesses, so an explicit plan cannot be replaced by a hit from the
other plan. Failed requests publish neither partial jets nor new cache entries.

### Runtime and XT

`TryPrepareICurveView` prepares analytic or B-surface supports, checks original
UV/position consistency, constructs oriented chart tangents, and carries UV
witnesses into public `CurveEval`.

The B-surface XT materializer validates NURBS dimensions, pole/weight arrays,
knots and multiplicities before using `BSurfCreate`. It preserves source sense
and rejects unsupported extended parameter boxes. XT surface-form and knot-type
codes use explicit schema-to-API mappings, verified against the repository's
real Parasolid `blend.fxf.bsurf-plane.rolling-ball` fixture. They are not assumed
to have the same ordinal layout as the API enums.

The public `PartReceiveB` path can materialize an open wire consisting of one INTERSECTION edge, two
vertices/points and two dummy fins. The resulting edge owns an executable ICurve;
successful receive is checked by actual public D0–D2 evaluation, not merely by
preserving an opaque XT document. Executable import is limited to the checked
V34–V38 canonical BODY layout. Older layouts, closed wires and additional
construction geometry retain the existing opaque path, preserving their source
data. Dangling or inconsistent incidences in the supported graph are invalid
input; allocation failures remain allocation failures.

Wire export includes dummy fins and the schema's raw wire-body code `2`.
Deleting the body reclaims its fins, curve and the support surfaces created
exclusively by materialization. Ordinary decoded curves continue to borrow
their caller-owned supports. Allocation-failure and multi-part receive rollback
checks cover these distinctions.

## Deliberate limits

- Missing B-surface UV, malformed/mixed-null UV and UV/position mismatch are not
  repaired by a global closest-point search.
- The regular solver requires a unique requested jet. At knots whose degree
  and multiplicity do not guarantee that continuity, the affected order is
  unsupported. Non-periodic domain endpoints use their in-domain one-sided jet.
  The guard follows the surface evaluator's knot-side resolution.
- Near-singular parameterizations and unresolved branch correspondence fail
  without output. The parametric path does not claim interval certification or
  a global uniqueness proof.
- A knot cell must retain at least 30 bits of UV resolution. Large UV origins
  that make an ULP too large relative to the cell report
  `ParameterResolutionLost`; tolerance is not widened to merge distinct
  preimages. Exact chart-node D0 remains defining data.
- B-surface terminator limits, extended surface domains, offsets, swept/spun
  and nested blend supports are outside this increment.
- Executable XT receive is the explicitly validated open one-edge wire subset,
  with the checked V34–V38 BODY layout, not a general B-rep importer. Existing
  opaque handling remains for other graphs.
- Imported extended surface metadata is not claimed to round-trip through a
  reconstructed geometry graph beyond the supported subset.

## Evidence

The independent manufacture tests use closed-form roots and derivatives of the
original chart-plane equation, rather than using the implementation to generate
its own expected jets. They cover polynomial and rational patches, mixed support
ordering, double B-surfaces, native parameter shifts/scales, sense, one-sided
chart jets, cold/warm/query-order behavior, explicit plans, strict domains,
singular inputs, knot continuity, periodic lifts and UV-unit rescaling by
`2^80` / `2^-80`.

Test sources:

- `tests/KernelTests/ParametricICurveTests.cs`
- `tests/KernelTests/ParametricICurveBranchTests.cs`
- `tests/KernelTests/ICurveSupportViewTests.cs`
- `tests/KernelTests/IcurveBSurfaceRuntimeTests.cs`

The final full-suite run includes **96 new passing cases**, with no skips:

| Test class | Passed |
|---|---:|
| `ParametricICurveTests` | 32 |
| `ParametricICurveBranchTests` | 9 |
| `ICurveSupportViewTests` | 9 |
| `IcurveBSurfaceRuntimeTests` | 46 |

| Full-suite comparison | Passed | Failed | Total |
|---|---:|---:|---:|
| Baseline `a5adfc0` in this workspace | 594 | 29 | 623 |
| Completed increment | 690 | 29 | 719 |

The 29 failures have exactly the same test names and error messages as the
baseline. They concern unavailable schema dependencies and existing
schema/catalog expectations in this workspace. All 623 existing tests retain
their baseline outcomes; no existing error message changed. The full test
command therefore still returns a failing exit code, and is not reported as a
fully green suite. Test-build allocation IL guard: **1,291 methods, CLEAN**.

The Linux x64 **Release / NativeAOT publish passed** with .NET SDK 10.0.403,
producing the ELF shared library. The production allocation IL guard checked
**1,284 methods, CLEAN**, and the function catalog verified all 83 implemented
dispatch descriptors. The publish used GCC as the available native linker:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet publish src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj -c Release -r linux-x64 -m:1 -p:UseSharedCompilation=false -p:CppCompilerAndLinker=gcc
```

### Real Parasolid oracle

`scripts/ParametricICurveOracle.cs` produces three independent real-PK reference
cases: B-surface/cylinder, cylinder/B-surface and B-surface/B-surface. It then
exercises public managed receive and native-parameter D0–D2, preserved-source
transmit, and a second mandatory transmit after dropping the received-document
association. That second export must reconstruct XT from runtime geometry.
Both exports are received by real PK and compared with the independent producer
using topology/type checks, D0–D2 and `PK_DEBUG_BODY_compare`. Full selected native
intervals are required; comparing only a cropped overlapping interval is not a
pass. Multi-block XT payloads are copied in full.

The current workspace has no licensed Parasolid runtime/archive or target
`sch_37102.sch_txt`. The repository's configured download route was probed without
altering credentials or SSH configuration. Both ordinary and elevated probes
failed before connection with:

```text
Bad owner or permissions (system SSH proxy configuration: 20-systemd-ssh-proxy.conf)
```

The normal build of `scripts/ParametricICurveOracle.cs` was attempted and exited
1 at `third_party/PKToy/Directory.Build.targets` with:

```text
Parasolid dependencies were not found.
```

The dependency guard was not bypassed. The oracle script therefore could not
finish compilation or execute in this workspace. Live oracle results for this
increment are **not claimed**. Once build dependencies are present, the script
reports an unavailable runtime as `NotRun`/exit 2, and an API, numerical or
structural failure as exit 1. Neither outcome counts as a pass.

### Reproduction

Use a .NET 10 SDK and the repository's configured schema/Parasolid dependency.
Preserve the test exit code when cleaning up the test host:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -m:1 -p:UseSharedCompilation=false
test_exit=$?
pkill -f '[t]esthost.dll' 2>/dev/null || true
exit "$test_exit"
```

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ParametricICurveOracle.cs
```

# Topology Event Framework — R3 Spine-Frame Candidate (Gemini Worker)

This document records the evaluation of the R3 spine-frame candidate on branch `fix/topology-r3-gemini` (base `90f4d7840b4d3d8219ba29d1845ccbaff3386f5a`), establishing reproducible evidence for independent review by GPT reviewer.

## 1. Provenance and Scope

- **Patch Source**: `temp_docs/opus-fix/r3b-support-axis.patch`
- **Patch SHA256**: `02e39e5ba6fdfd6cdfe29c2c9e535ffa9ed1608a9ecd598151461881e5928b9a` (4,735 bytes)
- **Modified Production File**: `src/ProjectGmKernel.Native/Runtime/KernelRuntime.AnalyticRollingCandidate.cs`
- **Instrumentation**: Pure production logic. All temporary diagnostic hooks (`DiagAddPoint`, `DiagDisableSpineFrameRule`, coordinate filters) were excluded.
- **Scope**: Canonicalizes the spine ellipse frame for rolling blends between two equal-radius cylinder supports whose bisecting plane normal is parallel to $A+B$ and major axis parallel to $A-B$.

```csharp
// KernelRuntime.AnalyticRollingCandidate.cs
var plus = Add(a.Axis, b.Axis); var minus = Sub(a.Axis, b.Axis);
if (ProfileNormalize(ref plus) && ProfileNormalize(ref minus)
    && Math.Abs(Math.Abs(Dot(axis, plus)) - 1) <= 1e-9 && Math.Abs(Math.Abs(Dot(refDir, minus)) - 1) <= 1e-9)
{
    if (Dot(axis, plus) < 0) { axis = Scale(axis, -1); spineSense = FlipLocalSense(source.Sense); }
    if (Dot(refDir, minus) < 0) refDir = Scale(refDir, -1);
}
```

The selected carrier's original major direction (`carrierMajor`) is preserved for subsequent contact sense calculation, ensuring contact sense remains unchanged.

## 2. Primary Geometry Gate Verification

All gates were executed from the repository root using `.NET 10` in `Release` configuration with `MSBUILDDISABLENODEREUSE=1` and `PKToy d820719`.

### 2.1 Standard Lifecycle (`--proof-only-lifecycle`)
- **Command**:
  ```bash
  MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ShortEdgeBlendOracle.cs -c Release \
    -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- \
    --event-arrangement --geometry --lifecycle --material-ownership \
    --require-native-fin-uvbox --require-native-face-domains --proof-only-lifecycle
  ```
- **Exit Code**: `0` (recorded in `temp_docs/r3-gemini/r3-standard.exit`)
- **Fault Checks**:
  - Native full check: `0 faults`
  - Native own roundtrip full check: `0 faults`
  - GM fresh XT via real PK full check: `0 faults`
- **PK_DEBUG_BODY_compare**:
  - `global=21921`, `local=21931`, `face_pairs=0` (Only internal chart points `n_cht_pts` diagnostic difference; zero topology, type, sense, or missing diffs)
- **Mass Properties** (accuracy `0.99999999`):
  - Normalized 15-property relative error: `4.6504558502419685E-09` (Strict 1e-7 threshold PASS)
  - Worst term: `inertia.yz`
- **Finite Boundary Matching**:
  - Finite ICurves: `3`, `endpoint_max=5.377e-12`, `length_delta_max=3.684e-11`
  - Native finite face domains: `10 matched, both_directions=True`

### 2.2 Transverse Local Cut Lifecycle (`--transverse-local-cut`)
- **Command**:
  ```bash
  MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ShortEdgeBlendOracle.cs -c Release \
    -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- \
    --event-arrangement --geometry --lifecycle --material-ownership \
    --require-native-fin-uvbox --require-native-face-domains \
    --require-native-cut-witness --require-cutter-chart-witness \
    --reload-before-local-cut --transverse-local-cut
  ```
- **Exit Code**: `0` (recorded in `temp_docs/r3-gemini/r3-transverse.exit`)
- **Fault Checks**:
  - Native full check: `0 faults`
  - Native own roundtrip full check: `0 faults`
  - GM fresh XT via real PK full check: `0 faults`
- **PK_DEBUG_BODY_compare**:
  - `global=21921`, `local=21931`, `face_pairs=0` (Zero non-diagnostic differences)
- **Mass Properties** (accuracy `0.99999999`):
  - Volume: `535.3240435815326` (GM) vs `535.3240435756962` (PK)
  - Area: `384.7897053007782` (GM) vs `384.78970529616885` (PK)
  - Normalized 15-property relative error: `1.3333236290568892E-08`
  - Root cause resolved: 이전 self transverse cut normalized inertia error (1.204e-7 / 1.126e-7) completely resolved below strict 1e-7 threshold!
- **Finite Boundary Matching**:
  - Finite ICurves: `5`, `endpoint_max=3.669e-13`, `length_delta_max=6.031e-10`
  - Native finite face domains: `13 matched, both_directions=True`
  - Material: `12 probes agree on both sides`

## 3. Targeted Units and Static Guards

- **Targeted Unit Tests**:
  - Command: `MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~CompactBranchCertificateTests|FullyQualifiedName~BlendSupportChainTests|FullyQualifiedName~RegularSolidBlendTests|FullyQualifiedName~ExplicitSpineTests|FullyQualifiedName~FiniteNativeBoundaryRayTests|FullyQualifiedName~ContactSupportCutTests|FullyQualifiedName~FinitePlaneSeparationTests|FullyQualifiedName~NestedBlend|FullyQualifiedName~BlendIntersectionTests|FullyQualifiedName~AnalyticSpineContactTests|FullyQualifiedName~RollingSectionLimitTests'`
  - Result: `Passed: 286, Failed: 0, Skipped: 0` (Exit code: 0)
- **Allocation Guard**:
  - Command: `MSBUILDDISABLENODEREUSE=1 dotnet run scripts/AllocationBanCheck.cs -c Release`
  - Result: `checked 2888 methods: CLEAN` (Exit code: 0)
- **Function Catalog**:
  - Command: `MSBUILDDISABLENODEREUSE=1 dotnet run scripts/GenerateFunctionCatalog.cs -c Release -- --check`
  - Result: `175 implemented dispatch descriptors verified` (Exit code: 0)

## 4. Classification and Regression Status (Separation of Concerns)

Per coordinator directive, primary geometric validity is separated from point classification regression:

- **Primary Geometry Gates**: PASS (Both standard and transverse cut lifecycles exit 0, full check 0 faults, strict 1e-7 mass passed, PK_DEBUG_BODY_compare clean).
- **Point Classification / Downstream Rolling Regressions**: OPEN
  - In `RollingNetworkTests` (3 failed, 16 passed):
    - `HeterogeneousBandsPublishThroughArrangementAndRetainPositiveShortIntervals(reverse: true)`: Row 289 probe reports `5703` (PK_enclosure_on_c) instead of `5702` (PK_enclosure_outside_c). This probe is situated at distance $\approx 10^{-8}$ from the vertex, and lies within PK's 4-ulp unstable probe set.
    - `HeterogeneousBandsPublishThroughArrangementAndRetainPositiveShortIntervals(reverse: false)`: `VerifyStationaryShortEnd` line 439 failed due to exact double vertex comparison.
    - `ProjectedRollingCellFailureRestoresTargetAndBothToolsAfterUnion`: Expected 901, Actual 157.
  - These tests are **not** silenced, waived, or deleted. They remain accurately reported as open classification / boundary compatibility issues for follow-on investigation.

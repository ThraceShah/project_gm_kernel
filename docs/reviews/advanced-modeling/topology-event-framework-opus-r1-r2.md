# Topology event framework — R1/R2 repair response (Opus)

Response to [independent review a1e4500](topology-event-framework-independent-review-a1e4500.md) findings R1 and R2. This document covers this increment only. It does not claim that M0–M5 are complete. R3–R6 remain open; their current state is given below.

- Branch `fix/topology-event-framework-opus`, base `610e3ce` (algorithm identical to `a1e4500`).
- PKToy `d820719`, real PK 38.0.150, .NET SDK 10.0.302. No C ABI change and no NativeAOT build.
- The earlier Gemini uncommitted patch (endpoints plus `{.25,.5,.75}` samples per segment) was **not** applied.
- Raw logs are in `temp_docs/opus-fix/` of this worktree. They are not committed.

## R1 — root cause of `PK_FACE_state_bad_loops_c` (13826)

Evidence came from the saved failing first-cut XT, inspected with `scripts/XtLoopDiagnostics.cs` and `scripts/DumpXtNodes.cs`:

- The faulted face is the `r=1.7` support cylinder. PK classifies its loop as `5414` (winding). Its 10-chart intersection fin UV box is `[-4.97e-14, 1.5705]`; the native cut and the uncut product have `[0, 1.5705]`.
- `INTERSECTION_DATA` of the republished curve: cylinder `u` runs `7.854 → 6.283` (imported path) or `-1.5705 → 0` (self path). The published CHART witnesses in the product before the cut are `1.5705 → 0`, plus the native seam endpoint `2π` (imported), or `4.7127 → 6.283` (self).
- A splice test replaced only the CHART hvec/scale fields with those of the uncut product. It still failed. The CHART positions are therefore not the cause.
- The curve's edge interval is "trimmed" by 1–2 ulps, so `TryCompactFiniteNestedIntersection` re-charts it. The UV unwrap in that function started from a continuous mid-curve seed, and that seed chose a different 2π sheet from the face use.

Fix (`KernelRuntime.NestedCurveArc.cs`, `AnchorStationUv`): every evaluated station witness is lifted to the period of the **published** CHART witness (`TryReadChartUv`) at the same node, or at the start of the original cell containing the station. A curve whose program and published chart counts differ is not compacted.

## R2 — finite branch certificate replacing sampled checks

The sampled `NestedBranchStationMap.CertifyBranchCoverage` has been removed. Publication now requires `JointNestedBlendEvaluation.CertifyCompactBranch` (`Geometry/Evaluation/CompactBranchCertificate.cs`). The original spine is appended to the compiled program on the **same** compiled supports, and the verification spine is excluded from the published program (`NestedProgramCompiler.CopyChartSpine` / `Prefix`). For each compact cell:

1. Krawczyk boxes from the existing `BoundSegment` cover the whole original interval `[t_k,t_k+1]`. Each box proves a unique regular root and encloses the derivative with `D·e>0`. Mean-value bounds prove the original stays within the compact spine's declared `chordal_error`, and that its tangent–chord angle stays within the declared `angular_error`.
2. Boxes cover the whole compact interval `[p_k,p_k+1]` with `D·e>0`.
3. At both station ends, the complete original root state (including nested occurrences and their CHART cells) lies inside a compact uniqueness box whose interval contains the original root's compact gauge level.

The proof of equality is written out in the code comment: an open/closed argument on the level parameter with the implicit function theorem. Any uncertain or singular box, exhausted depth (40) or box budget (262144), or a mismatch of supports or nested CHART cells rejects the candidate. The original certified representation (`CloneEntity`) is then kept.

`BoundSegment` change: with a fixed `rootChart`, the point preconditioner now comes from that same cell even when the domain straddles its boundary. Before this change, the midpoint could select the neighbouring cell's gauge, and a valid link at cell starts failed.

Unit tests in `CompactBranchCertificateTests` (8/8):

- Positive: an equivalent recharting of a trimmed arc, with the chordal bound checked against the exact sagitta.
- Negatives:
  - a trim inside one original cell with a wrong end point;
  - a compact spine on different supports (radius `1+1e-6`);
  - a two-station chart between the ends of a 3π/2 arc, which takes the other branch;
  - reversed sense;
  - a wrong finite trim and shifted stations;
  - understated chordal or angular error;
  - tangential supports, where the intersection is singular.

## Current gate results

All commands run from the project root with `MSBUILDDISABLENODEREUSE=1 ... -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true`.

| Gate | Log (`temp_docs/opus-fix/`) | Exit | Result |
| --- | --- | --- | --- |
| Standard lifecycle, `--proof-only-lifecycle` | `cert4-standard` | 0 | Both paths reach second cut; 23 full checks 0 faults; normalized15: imported cut `4.909e-9` / second `4.737e-9`; self uncut `3.966e-9`, reflected `4.545e-8`, cut `2.389e-8`, second `2.240e-8` |
| Baseline of the same command (reproduced here) | `baseline-standard-lifecycle` | 134 | Both first cuts `13826` (as R1) |
| Transverse lifecycle | `cert4-transverse` | 134 | **R3 unchanged**: self cuts `1.2044614e-7` / `1.1264689e-7` fail; imported `1.6248e-8` / `1.3333e-8` pass; 25 full checks 0 faults; material probes agree |
| `CompactBranchCertificateTests` plus focused/affected filters | `unit-targeted2` | 0 | 254/254 |
| `RollingNetworkTests` | `unit-rolling2` | 0 | 19/19 |
| Allocation IL guard | `allocation` | 0 | 2883 methods CLEAN |
| Function catalog | `catalog` | 0 | 175 |

Certificate cost, standard lifecycle: two-station curves take 2–6 ms (≤257 boxes), and 10-station curves take 2.5–4.2 s (8828–10930 boxes). This is a performance cost that remains to be reduced.

## R2a/R2b — review findings on `c301b40`, fixed in `2bdd0f3`

- **R2a.** The station link used only the gauge displacement along the chord. On a low-cosine chord, a 1e-8 end offset therefore passed with a reported link of 1e-11. The link is now the spatial diameter of the compact uniqueness box that holds both the original station root and the compact root at the station parameter, computed in outward intervals with a correctly rounded square root plus one ulp. The contract states that the original end is exactly the compact root at the gauge level `p*`, and that the published end lies within the certified link bound.
- **R2b.** The mean-value radius is now `MeanValueRadius = BitIncrement(max(centre-lower, upper-centre))`. `MeasureChordBox` computes chord distance, drift and the tangent–chord cosine in outward interval arithmetic and divides by `e·e`, so a non-unit chord is handled. Angular acceptance compares the cosine lower bound against `CosineUpper`, a degree-8 Taylor upper bound of `cos` that is valid for every real angle and evaluated in intervals. No libm `acos`/`cos` enters the decision.
- **Regressions.** The review's endpoint-offset construction is rejected, with the link bound slightly above the measured distance. Odd-ulp radius boxes are covered: `1e12`/3 ulps (radius ≥ 2.44140625e-4), `-1e12`, `1e-300`. `CosineUpper ≥ cos` holds on 4001 points of `[0, π]`. Non-unit chord distance is tested. At the exact angular boundary, `0.3·(1−1e-9)` is rejected and `0.3·(1+1e-6)` is accepted. `CompactBranchCertificateTests` 18/18.
- **Production effect.** Every candidate still certifies; the largest spatial link is `7.65e-13`. All lifecycle mass values are bit-identical to `c301b40` (`r2ab-standard` exit 0, `r2ab-transverse` exit 134 as R3).

## R3 — rolling spine orientation (candidate, not committed)

**Evidence.**

- The native spine of the cylinder/cylinder rolling blend is the carrier conic with different frame signs.
- `scripts/XtTransformCarrier.cs` applies the oracle's own rigid and mirror matrices in real PK and in GM. The transformed carriers are identical, so the input representation is not the cause.
- Native frames compared with the carrier (`(axis, ref, sense)` dumps):
  - default and rigid: `(-axis, -ref, +)` against a carrier with sense `-`;
  - mirror: the carrier frame is kept, with sense `-`;
  - reverse selection: the same as default.
- Every case fits one rule. With `A`, `B` the stored axis vectors of the two equal cylinders in blend support order, the spine normal has the sign of `A+B` and the major axis has the sign of `A−B`. The sense keeps the carrier's traversal.
- An earlier rule (flip any negative-sense carrier) matched default and rigid but not mirror. It was replaced.

**Change** (`KernelRuntime.AnalyticRollingCandidate.cs`, saved as `temp_docs/opus-fix/r3b-support-axis.patch`):

- The rule chooses only the signs of the carrier's own normal and major axis; the conic is unchanged.
- It applies only when the normal is parallel to `A+B` and the major axis parallel to `A−B`. Otherwise the carrier frame is kept.
- The contact sense is still derived from the carrier's major direction.

**Gates on `2bdd0f3` plus validation plus this rule** (`r3b-*`):

- All seven lifecycles exit 0, with every full check at 0 faults. The only comparison item is `n_cht_pts`.
- Self now tracks imported in every case:

| Run | Self | Imported |
| --- | --- | --- |
| standard | 2.163e-10, 1.379e-9, 4.887e-9, 4.650e-9 | — |
| transverse (cuts) | 1.6248227e-8 / 1.3333236e-8 | 1.6247807e-8 / 1.3332703e-8 |
| mirror (cut) | 1.0384e-8 | 1.0417e-8 |
| rigid (second cut) | 9.734e-10 | 9.751e-10 |
| scale 1.7 | exit 0 | — |
| reverse | exit 0 | — |
| mirror transverse | exit 0 | — |

- No-R3 baselines in the separate worktree for mirror, rigid, scale, reverse and mirror transverse also exit 0.
- Targeted units 286/286; allocation guard 2888 CLEAN; catalog 175.

**Blocking regression.** `RollingNetworkTests.HeterogeneousBandsPublishThroughArrangementAndRetainPositiveShortIntervals(reverse: true)` asserts GM `outside` (5702) at fixed probe row 289 and gets `on` (5703).

- Cause measured: one short-edge end vertex, `(-1.49999966781, -2.49999999420, 4.50007614662)`, moves from 4.7e-14 to 3.08e-12 away from PK's vertex. The other 14 vertices are unchanged.
- Row 289 lies 1e-8 from that vertex. Its distance goes from `1e-8+3.8e-16` to `1e-8−5.4e-14`.
- Row 289 is also one of PK's 4-ulp-unstable rows.
- The test was not changed, and the R3 code is not committed.

**Contact-frame observation.** In every native case each contact ellipse's normal is anti-aligned with the spine normal and its major axis aligned. GM keeps both aligned.

- Negating the contact normal in `BindAnalyticContact` matched PK's frame, but broke three rolling tests: the stationary short-end vertex identity, API 157, and row 289.
- It was reverted (`contact-unit-rolling`). Contact orientation has dependent consumers, and they need their own analysis.

## R4 — fixed probes: evidence only, no gate change

- `scripts/ProbeUlpSensitivity.cs` receives the native fillet XT in real PK and classifies all 510 CSV coordinates verbatim and at ±1–4 ulps per axis. Result: 8 rows are unstable in PK itself within 4 ulps: `102,153,204,254,289,390,441` (exactly the native-vs-native roundtrip rows) and `339` (the only self native-vs-fresh row). Rows 254 and 289 take all three enclosure values within 4 ulps. The remaining 502 rows are stable. This shows sensitivity, not by itself the cause of the fresh row-339 difference; the independent native-only control is with GPT.
- **GM-attributable rows (PK-stable):** self `50,101,152,203,340,442,493`; imported adds `391`. At these points GM reports `on` where PK stably reports `outside`.
- `scripts/ProbeBoundaryRule.cs` sweeps the offset from vertex `(-1.5,-2,5)` in several directions, in PK and GM on the same XT (`rule-vertex-dirs.log`). PK's "on vertex" region is anisotropic: along `+z` it ends at `8.692e-9`–`8.693e-9`; along `(x+z)/√2` it ends at `9.1e-9`, then reports on-face up to about `1.2e-8`; along `(-y+z)/√2` it reports on-vertex beyond `1.1e-8`. GM uses an isotropic `1e-8` vertex test. No single tolerance reproduces PK, and none was changed. Matching these knife-edge probes requires PK's actual near-boundary semantics, which are still open.

## Open items

- R4: PK-unstable rows await a ruling. GM near-boundary semantics for the PK-stable rows remain open.
- R5 documentation reconciliation, including the `--import-only` 96-case claim.
- R6 normal Boolean proof scope.
- Self-construction 96-case matrix, transformed lifecycles (in progress) and full regression.

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

## R3 lead (not a fix)

While the transverse 6-station candidate was still rejected (before the `BoundSegment` preconditioner fix), only that one intersection curve was published with its original fine CHART instead of the compact one. The self cuts then gave `7.80e-8` / `7.39e-8` (pass), and the imported cuts gave `1.669e-7` / `1.584e-7` (fail). The PK native cut itself publishes a 6-chart curve there. The raw point residual therefore depends on the CHART representation of this intersection curve; the geometric error is not the explanation. The next step is to compare the exact station placement and the native chart for this curve. The mass gate is not changed.

## Open items

- R3 transverse self raw inertia (above).
- R4 immutable 510 probes: not rerun in this increment.
- R5 documentation reconciliation, including the `--import-only` 96-case claim.
- R6 normal Boolean proof scope.
- Self-construction 96-case matrix, transformed lifecycles and full regression.

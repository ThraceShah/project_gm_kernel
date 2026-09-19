# ICurve / Blend Evaluation Capabilities

Generated: 2026-09-19. Evidence against `docs/icurve_design/icurve_blend_evaluation_spec.md` tasks T00–T20 (partial).

## Supported (internal evaluation modules)

| Capability | Scope | Notes |
|---|---|---|
| Chart rebuild + chord plane | Original chart | §5; nodes immutable |
| Regular plans P4/P2/I3/I2/I1 | Analytic supports | §7.6 Auto: I1 if plane else **I2** |
| Diagnosed Auto plan switch | Singular/stagnation | Forced plans never switch; `PlanSwitched` detail |
| D0–D2 on regular interval | Selected plan | §16; side at nodes |
| L0 residual memo | Wired into corrector trials | §13.3; generation invalidate |
| Frozen residual scaling | Per accepted state | §14.1; trial reuses freeze |
| PredictedOnly Hermite seeds | L2 insert | Never exact-hit (§13.4) |
| Shared-spine memo | Multi-parent keys | §13.7; parent sense/range isolated |
| L1/L2/L3 caches | Session/operation | §13; epoch invalidation |
| Trust-region corrector | Five plans | §14.3–14.4; SVD + η_k tighten |
| Nested η_k / InnerAccuracyInsufficient | Corrector budget | §15; tighten before radius shrink |
| Levenberg–Marquardt step | n≤16 math | §14.5; not default corrector |
| Internal pseudo-arclength augment | Ill-conditioned plane row | §17.4; public t restored by caller |
| Continuity-cell difficulty | Seam / near-tangent | Near-tangent → Singular |
| Small LU / QR / SVD | n≤6 | `SmallLinearSolve` |
| Parameter continuation | Same original segment | §17.3; native t restored |
| Local subdivision | Same original segment | §17.5 |
| Shared evaluation budget + η_k | Top-level + terminator | §14.7 / §15 |
| Failure replay + counters | Caller-owned diagnostics | §22.4 / T20 |
| Terminator 1-surface / 2-planes + parametric 2×2 | Explicit GATE-T rule | Production gate open |
| R-blend parametric + envelope / joint | Internal + Schur lift | GATE-A/D open; not Auto |
| Plan-state migration elim↔joint | Opt-in | §12.4 |
| Affine / swept-spun elim / contact UV | Math modules | Not full prepare graph |
| BLEND_BOUND composition | Sphere + cylinder D3 | GATE-B open |
| Interval cert I3 + P2 UV | Analytic | §18.3; B-surface deferred |
| Eval detail codes | §18.5 | Incl. InnerAccuracyInsufficient |
| Runtime / oracle / XT / T20 bench | Analytic fixtures | Case F / GATEs may NotRun |

## Explicitly unsupported / gated

| Item | Status |
|---|---|
| GATE-B BlendBound role map | open |
| GATE-T terminator `t_E` vs PK | open |
| GATE-A blend arc extremes | open |
| GATE-D public high-order PK contract | open |
| Procedural / BlendBound / B-surface / offset / swept icurve prepare | Unsupported |
| Envelope / Joint as Auto production plans | not §7.6 Auto |
| Full nested UV/spine witness arena on L2 | partial (PredictedOnly XYZ only) |
| XT INTERSECTION live PK receive/compare | densified; may NotRun on Δ |
| Spindle/apple torus | Unsupported (`a ≤ b`) |
| Pseudo-arclength as public parameter | forbidden |
| T20 publish speed gates | unlocked |

## Verification

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests && pkill -f testhost.dll 2>/dev/null; true
P_SCHEMA=third_party/parasolid/schema dotnet run scripts/IcurveEvaluationOracle.cs
dotnet run scripts/IcurveEvalBenchmark.cs
```

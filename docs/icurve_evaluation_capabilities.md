# ICurve / Blend Evaluation Capabilities

Generated: 2026-09-19. Evidence against `docs/icurve_design/icurve_blend_evaluation_spec.md` tasks T00–T20 (partial).

## Supported (internal evaluation modules)

| Capability | Scope | Notes |
|---|---|---|
| Chart rebuild + chord plane | Original chart | §5; nodes immutable |
| Regular plans P4/P2/I3/I2/I1 | Analytic supports | §7; Auto: I1 if plane else P2 |
| D0–D2 on regular interval | Selected plan | §16; side at nodes |
| L1/L2/L3 caches | Session/operation | §13; epoch invalidation |
| Trust-region corrector | Five plans | §14.3–14.4; rank-deficient → SVD min-norm |
| Small LU / QR / SVD | n≤6 | `SmallLinearSolve`; Jacobi SVD + min-norm |
| Parameter continuation | Same original segment | §17.3; native t restored |
| Local subdivision | Same original segment | §17.5; Unique/Empty interval disambiguation |
| Shared evaluation budget | Top-level + terminator | §14.7 / §15; BudgetExceeded |
| Terminator 1-surface / 2-planes | Explicit GATE-T rule | Production keeps gate open |
| R-blend parametric + envelope math | Internal | GATE-A/D open; not Auto plans yet |
| BLEND_BOUND distance composition | Math only | GATE-B open; not production |
| Interval certification (I3) | Plane/sphere/cylinder/cone/ring-torus | §18.3; spindle Unavailable |
| Runtime `PK_CURVE_eval` for icurve | Analytic supports | Decode → bind → prepare → eval |
| Real Parasolid oracle | Plane∩sphere D0–D2; plane∩cone; plane∩torus; skew cyl | `scripts/IcurveEvaluationOracle.cs` |
| XT INTERSECTION writer | Help + Terminator limits, analytic supports | Node 38/40/41/204 |
| XT CHART extract → DecodeIcurve | Node 40 common layout | `TryExtractIcurveChartFromXt` |
| XT INTERSECTION materialize | Analytic supports + LIMIT + DATA | Empty-all UV → None; Terminator LIMIT roundtrip |
| Ring-torus oriented distance | `a > b > 0` sheet | Exact SDF √((ρ−a)²+z²)−b |
| T20 eval micro-benchmark | plane∩sphere/cyl/cone | cold + hot p50/p95 |

## Explicitly unsupported / gated

| Item | Status |
|---|---|
| GATE-B BlendBound role map | open |
| GATE-T terminator `t_E` vs PK | open (Unresolved production path) |
| GATE-A blend arc extremes | open |
| GATE-D public high-order PK contract | open (Runtime rejects order > 2) |
| Procedural / BlendBound icurve supports | Unsupported at prepare |
| B-surface / offset / swept supports for icurve prepare | Unsupported |
| Envelope 4×4/3×3 as `ICurveConstraintPlan` | math present; not wired into Auto/eval |
| Joint 6-unknown plan migration | math present; not production path |
| XT INTERSECTION live PK receive/compare | Attempted; receive may succeed but sample Δ NotRun |
| B-surface interval cert | BoundsUnavailable |
| Spindle/apple torus oriented distance / interval | Unsupported (`a ≤ b`) |
| Pseudo-arclength as public parameter | forbidden by §17.4 |
| T20 publish speed gates | measured locally; thresholds not locked |

## Verification

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests && pkill -f testhost.dll 2>/dev/null; true
P_SCHEMA=third_party/parasolid/schema dotnet run scripts/IcurveEvaluationOracle.cs
dotnet run scripts/IcurveEvalBenchmark.cs
```

Oracle report: `temp_docs/icurve-evaluation/oracle-latest.txt`.

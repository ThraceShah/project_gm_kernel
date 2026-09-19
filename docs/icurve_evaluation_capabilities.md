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
| L1/L2/L3 caches | Session/operation | §13; epoch invalidation |
| Trust-region corrector | Five plans | §14.3–14.4; SVD min-norm + Stagnation |
| Levenberg–Marquardt step | n≤16 math | §14.5; not default corrector |
| Small LU / QR / SVD | n≤6 | `SmallLinearSolve`; Jacobi SVD + min-norm |
| Parameter continuation | Same original segment | §17.3; native t restored |
| Local subdivision | Same original segment | §17.5; Unique/Empty interval disambiguation |
| Shared evaluation budget + η_k | Top-level + terminator | §14.7 / §15; `TightenInner` |
| Failure replay ring | Caller-owned | §22.4; no I/O in residuals |
| Terminator 1-surface / 2-planes | Explicit GATE-T rule | Production keeps gate open |
| Terminator parametric 2×2 | Opt-in math path | Falls back to 1D; GATE-T open |
| R-blend parametric + envelope math | Internal | GATE-A/D open; not Auto plans yet |
| Envelope 4×4 / 3×3 Newton | Circular-spine tube | T13; envelope-before-joint recovery |
| Joint 6-unknown lift | Opt-in after Singular | Terminator spine refused; Occurrence ids |
| Affine implicit / uniform-scale SDF | Math only | §8.4; non-uniform SDF refused |
| Swept / spun elimination residuals | Math only | §9.4; not icurve prepare |
| Contact from spine UV witness | No re-project | §10.2 |
| BLEND_BOUND distance composition | Math only | Sphere + cylinder D3; GATE-B open |
| Interval certification (I3) | Plane/sphere/cylinder/cone/ring-torus | §18.3; spindle Unavailable |
| Eval detail codes | §18.5 set | InvalidChart…PlanSwitched |
| Runtime `PK_CURVE_eval` for icurve | Analytic supports | Decode → bind → prepare → eval |
| Real Parasolid oracle | Plane∩sphere D0–D2; plane∩cone; plane∩torus; skew cyl | `scripts/IcurveEvaluationOracle.cs` |
| XT INTERSECTION writer / materialize | Analytic + LIMIT + DATA | Empty UV → None; Terminator LIMIT |
| Ring-torus oriented distance | `a > b > 0` sheet | Exact SDF |
| T20 eval micro-benchmark | fixtures + plan-cost matrix | cold + hot p50/p95/p99 |

## Explicitly unsupported / gated

| Item | Status |
|---|---|
| GATE-B BlendBound role map | open |
| GATE-T terminator `t_E` vs PK | open (Unresolved production path) |
| GATE-A blend arc extremes | open |
| GATE-D public high-order PK contract | open (Runtime rejects order > 2) |
| Procedural / BlendBound icurve supports | Unsupported at prepare |
| B-surface / offset / swept supports for icurve prepare | Unsupported |
| Envelope / Joint as Auto production plans | math + lift entry; not §7.6 Auto |
| Nested UV/spine witness arena on L2 | deferred |
| Shared-spine multi-parent L3 | deferred |
| XT INTERSECTION live PK receive/compare | densified; may still NotRun on Δ |
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

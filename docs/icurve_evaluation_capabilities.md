# ICurve / Blend Evaluation Capabilities

Generated: 2026-09-18. Evidence against `docs/icurve_design/icurve_blend_evaluation_spec.md` tasks T00–T20 (partial).

## Supported (internal evaluation modules)

| Capability | Scope | Notes |
|---|---|---|
| Chart rebuild + chord plane | Original chart | §5; nodes immutable |
| Regular plans P4/P2/I3/I2/I1 | Analytic supports | §7; Auto selection |
| D0–D2 on regular interval | Selected plan | §16; side at nodes |
| L1/L2/L3 caches | Session/operation | §13; epoch invalidation |
| Trust-region corrector | Five plans | §14.3–14.4 |
| Parameter continuation | Same original segment | §17.3; native t restored |
| Local subdivision | Same original segment | §17.5; AmbiguousBranch on ties |
| Shared evaluation budget | Top-level request | §14.7 / §15; BudgetExceeded |
| Terminator 1-surface / 2-planes | Explicit GATE-T rule | Production keeps gate open |
| R-blend parametric + envelope math | Internal | GATE-A/D open |
| BLEND_BOUND distance composition | Math only | GATE-B open; not production |
| Interval certification (I3) | Plane/sphere/cylinder | §18.3; Empty/Unique/Undetermined |
| Runtime `PK_CURVE_eval` for icurve | Analytic supports | Decode → bind → prepare → eval |

## Explicitly unsupported / gated

| Item | Status |
|---|---|
| GATE-B BlendBound role map | open |
| GATE-T terminator `t_E` vs PK | open (Unresolved production path) |
| GATE-A blend arc extremes | open |
| GATE-D public high-order PK contract | open (Runtime rejects order > 2) |
| Procedural / BlendBound icurve supports | Unsupported at prepare |
| Real Parasolid oracle for icurve/blend | **NotRun** (T19 remainder) |
| XT import → `CurveClass.ICurve` entity | decode+bind only; no XT receive path yet |
| Cone/torus/B-surface interval cert | BoundsUnavailable |
| Pseudo-arclength as public parameter | forbidden by §17.4 |
| T20 publish matrix / P95 budgets | skeleton only (`scripts/IcurveEvalBenchmark.cs`) |

## Verification

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests && pkill -f testhost.dll 2>/dev/null; true
dotnet run scripts/IcurveEvalBenchmark.cs
```

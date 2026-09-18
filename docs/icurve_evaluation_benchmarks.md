# ICurve Evaluation Benchmarks

Generated: 2026-09-19. Skeleton for `docs/icurve_design/icurve_blend_evaluation_spec.md` §22 / task T20.

## How to run

```sh
dotnet run scripts/IcurveEvalBenchmark.cs
dotnet run scripts/IcurveEvalBenchmark.cs -- --out ../temp_docs/icurve-evaluation/benchmark-latest.txt
```

## What is measured

Analytic fixtures through Runtime `PK_CURVE_eval` (decode → bind → prepare → eval):

| Fixture | Chart |
|---|---|
| plane ∩ sphere | unit circle in z=0 |
| plane ∩ cylinder | unit circle in z=0 |
| plane ∩ cone | unit circle in z=0 (R=1, k=0.5) |

Per fixture:

- cold ChartPoint D0 / RegularChartInterval D0–D2
- hot sample p50 / p95 (n=400 after warmup)

## What is not claimed

- Publish speed thresholds (require T19 oracle-closed correctness matrix)
- Memory peak / allocation P95 across named constraint plans
- Nested blend / terminator / BlendBound paths

Record machine RID, HEAD, and capability config alongside any checked-in numbers.

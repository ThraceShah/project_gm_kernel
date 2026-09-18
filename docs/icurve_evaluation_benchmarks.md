# ICurve Evaluation Benchmarks

Generated: 2026-09-18. Skeleton for `docs/icurve_design/icurve_blend_evaluation_spec.md` §22 / task T20.

## How to run

```sh
dotnet run scripts/IcurveEvalBenchmark.cs
dotnet run scripts/IcurveEvalBenchmark.cs -- --out ../temp_docs/icurve-evaluation/benchmark-latest.txt
```

## What is measured

Analytic plane ∩ cylinder unit-circle fixture:

- cold / hot `ChartPoint` D0
- cold / hot `RegularChartInterval` D0–D2 via plan I3

## What is not claimed

- Publish speed thresholds (require T19 oracle-closed correctness matrix)
- Memory peak / allocation P95 across plans
- Nested blend / terminator / BlendBound paths

Record machine RID, HEAD, and capability config alongside any checked-in numbers.

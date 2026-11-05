# Blend support and performance probe — 2026-11-05

Production geometry revision: `dde2cc3`. Variable-radius blends are deferred
at the user's request. This report concerns constant-radius geometry, without
topological blending operations.

## Support

`src/ProjectGmKernel.Native/Runtime/KernelRuntime.SurfaceBlend.cs` accepts analytic supports, equal absolute
signed radii and an offset-support intersection that is a line or circle. It
creates an independent cylinder or ring torus. There is no retained procedural
blend graph to traverse during evaluation.

General nested procedural blends are not connected. In
`src/ProjectGmKernel.Native/Runtime/KernelRuntime.Evaluation.cs`,
`EvaluateSurfaceCore` has no `SurfaceClass.BlendSurface` case. The envelope,
joint-lift, contact-frame and distance-composition helpers have manufactured
tests, but do not form a production recursive Blend evaluator. The offset
evaluator's depth limit of eight is unrelated to nested Blend support.

The new two-stage diagnostic independently calls each kernel's
`PK_SURF_create_blend` twice:

1. Planes z=0 and x=0, signed radius 0.2, create a cylinder.
2. That cylinder and plane y=0, signed radius 0.05, create a torus.

Both kernels succeed. Both tori have center (0.2,0.05,0.2), axis +Y, major
radius 0.25 and minor radius 0.05. Our reference direction is +X; PK chooses
+Z. Identical public U/V therefore produces different positions and jets.
The explicit map `PK.u = self.u + pi/2`, `PK.v = self.v` matches 100 rectangular
D0--D2 jets to 1e-9. This remains true after independently deleting the first
Blend in both kernels.

This is an analytically simplified two-stage geometry case. It is not a passed
raw-parameter compatibility case, a general nested Blend implementation, an
XT/B-rep comparison, or evidence for arbitrary-depth construction. The script
labels the discrepancy `BOUNDARY`; it verifies that the discrepancy still
reproduces before measuring the mapped result. Existing independent body/XT
comparisons for the single-stage cases remain in
`scripts/IntersectionOperationOracle.cs`.

## Measurement

Linux x64, AMD Ryzen 7 7840HS, .NET SDK 10.0.302 / runtime 10.0.10, real
Parasolid 38.0.150. This compares Release managed runtime public entry points
against real PK through P/Invoke, single thread. It does not measure the
NativeAOT C-export boundary. Tiered compilation is disabled. Each case has
eight warmup batches and 31 alternating-order measured batches. Evaluation
batches contain 4096 varying parameter queries; creation batches contain 64
create-and-delete pairs. Every timed call checks the return status; evaluation
results feed a checksum. Single-stage geometry class and 100 D0--D2 jets must
agree before timing. Managed allocation counters cover the timed self calls.

Representative completed run, median nanoseconds per call or create/delete
pair; lower is faster:

| Case | Self | PK | Self/PK |
| --- | ---: | ---: | ---: |
| Plane/plane cylinder, D0 | 150.5 | 150.0 | 1.00 |
| Cylinder, rectangular D1 | 170.2 | 152.0 | 1.12 |
| Cylinder, rectangular D2 | 195.1 | 156.5 | 1.25 |
| Cylinder blend create + delete | 621.3 | 4643.3 | 0.13 |
| Plane/sphere torus, D0 | 151.7 | 179.5 | 0.85 |
| Torus, rectangular D1 | 173.1 | 184.6 | 0.94 |
| Torus, rectangular D2 | 202.6 | 196.4 | 1.03 |
| Torus blend create + delete | 627.7 | 5297.7 | 0.12 |
| Two-stage torus, mapped rectangular D2 | 199.4 | 199.2 | 1.00 |

Rectangular D1 requests orders (1,1), four vectors including mixed derivatives;
D2 requests (2,2), nine vectors. Timed self calls allocate zero managed bytes.
Full p50/p95 values and checksums are in
`temp_docs/blend-geometry-benchmark.log`; the independent repeat is in
`temp_docs/blend-geometry-benchmark-repeat.log`. CPU scheduling/frequency is not
pinned, so small differences should not be treated as a stable speed ranking.

These results place the supported analytic evaluation paths at roughly the
same cost as PK. The cheaper create/delete subset does not imply a general
Blend construction advantage: our path has a much narrower supported domain
and does not build general procedural geometry. Flattening to a torus also
means that the second-stage result does not pay recursive evaluation cost.
General nested Blend construction, evaluation and SSI performance cannot be
compared until those production paths exist. SSI timing was not measured here.

Reproduce from the project root:

```sh
MSBUILDDISABLENODEREUSE=1 DOTNET_TieredCompilation=0 dotnet run --no-cache scripts/BlendGeometryBenchmark.cs -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false
```

The benchmark reuses the existing `IcurveEvalBenchmark` friend-assembly identity
to reach the managed entry points; production access declarations are unchanged.

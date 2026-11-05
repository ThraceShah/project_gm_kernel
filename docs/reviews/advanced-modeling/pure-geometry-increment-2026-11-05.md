# Pure geometry increment validation — 2026-11-05

This is an increment of the requested four-category geometry work, not a claim
of complete Parasolid geometry support. The accepted completion convention is
per-capability verification with explicit support and remaining boundaries.
The work list is `docs/Geometry_Completion_Plan.md`.

## Results

- All 14 VerifyAdvancedModeling steps and all 14 nested VerifyPublicModeling
  steps passed. The suite's kernel run passed 1,269 tests; XT tests passed 12.
- Subsequent analytic blend tests passed 5 additional cases, including signed
  ranges, exact contacts, source deletion, support order and failed construction.
- The final expanded IntersectionOperationOracle passed 78 independent contract
  cases. Every positive geometry output is independently constructed in both
  kernels and our text XT is received and checked by real Parasolid, followed
  by PK_DEBUG_BODY_compare. Chart/parameterization diagnostics retain the same
  documented structural gates as the preceding general-surface work.
- Release NativeAOT publication passed with 166 implemented dispatch
  descriptors; its allocation IL guard checked 1,965 methods and was CLEAN.
- Native ABI, native display and advanced native modeling smoke tests passed.
- Code generation passed the ABI check. Clang field offsets fix alignment of
  the range parameter-bound discriminator; a dedicated layout test covers it.
- Global distance tests include boundary/interior extrema, continuous and
  multiple minima, zero distance, returned parameters, native strict thresholds,
  failure outputs and zero managed allocations after warmup.

Primary logs: `temp_docs/geometry-expansion-verification.log`,
`temp_docs/advanced-modeling-verification/summary.txt`,
`temp_docs/public-modeling-verification/summary.txt`,
`temp_docs/geometry-distance-tests.log`,
`temp_docs/geometry-blend-tests.log`, and
`temp_docs/geometry-blend-oracle.log`.

## Added scope

Analytic pairs with a sphere/ring torus and skew cylinder pairs now use a
geometry-derived finite search domain. Exact coaxial torus and sphere/torus
branches include tangent circles. Both-analytic discovery remaps every witness
to both native parameter domains before publishing support-defined ICURVEs.

PK_GEOM_range_vector covers points, lines, conics, B-curves, analytic surfaces
and B-surfaces within the implemented regular-domain paths. Sphere-center,
cylinder-axis and analogous analytic projection families retain a valid
witness and region flag. PK_GEOM_range adds global minima for bounded spline
geometry and supported analytic combinations with a bounded operand. Convex
hull bounds, box separation and separating planes certify the distance gap;
local optimization alone never certifies globality.

PK_SURF_create_blend uses signed analytic support offsets and a straight or
circular spine to produce an exact cylinder or ring torus. Existing analytic
evaluation and intersection paths then apply directly. Successful oracle cases
cover positive/negative plane blends and both plane/sphere support orders.

## Remaining boundaries

Generic singular/coincident locus decomposition, general unbounded conic SSI,
offset/swept/spun/procedural-blend supports, maximum/local/array ranges,
parameter-bounded range options, general curve projection and arbitrary
minimum-family metadata remain open. General procedural and variable-radius
Blend surfaces are not connected by the analytic simplification increment.
Geometry editing and constrained construction are the next increment.

No unsupported or numerical-failure case was converted to a successful empty
result to increase coverage. Existing extreme-weight native BOUNDARY cases
remain excluded from the positive compatibility count.

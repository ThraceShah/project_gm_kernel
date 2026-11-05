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
The editing and constrained-construction subset is recorded below.

No unsupported or numerical-failure case was converted to a successful empty
result to increase coverage. Existing extreme-weight native BOUNDARY cases
remain excluded from the positive compatibility count.

## Editing, constrained surfaces and contact increment

Seven additional public APIs implement B-curve/B-surface insertion, elevation
and reparameterization. Degree elevation restores original knot continuity
using inverse Boehm steps certified by interval reinsertion. The original tag
survives; payload replacement, allocation failures and mark/goto restoration
participate in the runtime transaction system. Attached geometry is rejected.
Degrees above 16, unclamped/periodic input and the documented workspace limits
are outside this subset.

Spline-grid construction supports cubic clamped boundary vectors and explicit
corner twists. Independent native experiments established normalized adjacent
segment vector semantics and transpose-before-reversal parameter maps; the
regressions exercise nonuniform domains, rational data and D0--D2 mixed jets.

Strict convex/concave plane-numerator bounds now certify isolated NURBS plane
contacts when value and gradient are exactly zero. The contact test includes
nearby regular closed curves and a disjoint case. Exact singleton interval
operations are checked against exact BigInteger fractions across normal and
subnormal ranges; Krawczyk displacements also enclose subtraction rounding.
Tighter bounds exposed an isoparametric extraction rounding defect: a constant
coordinate could shrink by one ULP and lose a finite-domain boundary crossing.
Affine accumulation preserves these coordinates. Direct boundary fixtures,
repeated closed-edge imprint and annulus section verify the correction without
relaxing topology counts or native checks.

Exact infinite line/line, sphere/sphere and plane/plane minima supplement the
bounded distance solver, including witnesses and known continuous-family flags.
Generic maximum/local/array queries and arbitrary pair-family metadata remain
open. The blend increment remains the exact cylinder/torus simplification;
general procedural and variable-radius blends are still open. Public knot
removal, degree reduction, extension, joining, periodic editing, automatic
doubly-clamped corner twists and curve-clamped grids are also still open.

The final combined suite passed all 14 top-level and all 14 nested gates.
IntersectionOperationOracle passed 87 contracts and FreeformModelingOracle
passed 22 scenarios, with real-PK receive/full-check and independent body
comparison. CurvedSheetPartitionOracle passed all 70 fixtures. XT tests passed
12. Release NativeAOT, native ABI, display and advanced modeling smoke tests
passed with 173 implemented dispatch descriptors; the Release allocation IL
guard checked 1,994 methods and was CLEAN. The combined suite's kernel run
passed 1,295 tests before the last additional invalid-edit and distance-family
cases. The final full kernel rerun passed all 1,306 tests, with zero failures
and zero skips. Evidence is in `temp_docs/pure-geometry-final-kernel-tests.log`.

Environment: Linux x64, .NET SDK 10.0.302 and real Parasolid 38.0.150. Final
logs are `temp_docs/pure-geometry-final-verification.log`,
`temp_docs/advanced-modeling-verification/summary.txt`,
`temp_docs/public-modeling-verification/summary.txt`,
`temp_docs/pure-geometry-final-editing-tests.log` and
`temp_docs/pure-geometry-final-kernel-tests.log`.

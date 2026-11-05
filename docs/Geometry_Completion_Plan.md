# Pure geometry completion work

Requested scope: difficult intersections and type coverage; general distances
and projection; blend construction/evaluation/intersection; Parasolid geometry
editing and higher-order surface construction. Topology editing is excluded.

## Execution and acceptance

1. Extend intersection discovery and classification. Verify transverse,
   tangent, singular, coincident, disconnected, periodic and pole cases with
   independent manufactured geometry and real Parasolid comparisons.
2. Add global geometry distances and projections. Verify boundary/interior and
   multiple minima, zero distances, stationary points, parameters and witnesses.
3. Connect blend geometry construction, evaluation and intersections. Verify
   support contact, branch and radius consistency, derivatives and ownership.
4. Implement the corresponding Parasolid curve/surface editing and constrained
   construction contracts. Verify shape-preserving operations, stated error
   bounds, parameter maps and rejected invalid requests.

Every completed increment requires unit tests, independent text-XT receive and
PK_DEBUG_BODY_compare where geometry output is affected, rollback/ownership
tests, allocation checks, NativeAOT and relevant existing regressions. Publish
tested commits to the current remote branch. Explicitly retain unresolved
items; an unsupported result or a smaller positive test matrix is not evidence
that the entire requested category is complete.

## Progress

| Category | Status |
| --- | --- |
| Difficult intersections and type coverage | First analytic and tangent-circle increment verified; remaining cases below |
| General distances and projection | Point and bounded-pair minimum increment verified; remaining cases below |
| General blend geometry | Exact cylindrical/toroidal rolling-ball increment verified; general procedural blends remain open |
| Geometry editing and higher-order construction | In progress |

## First verified increment

- Geometry-derived finite-domain discovery for skew cylinders and analytic
  pairs with a sphere or ring torus. Exact coaxial torus pairs and coaxial
  sphere/torus intersections include tangent circles.
- Global bounded Bezier point/curve/surface pair distance foundation, plus
  public PK_GEOM_range_vector and minimum PK_GEOM_range entry points.
  Point queries cover analytic surfaces (including nonunique center/axis
  projections), B-curves and B-surfaces. Pair queries currently cover bounded
  spline geometry and bounded/unbounded analytic combinations where at least
  one operand is bounded.
- Native range endpoint and strict lower/upper threshold semantics verified.
  Explicit-layout discriminator offsets now come from Clang rather than
  packed-size summation, fixing the range parameter-bound ABI.
- Public PK_SURF_create_blend produces exact cylinders and ring tori for
  supported straight/circular offset-support spines. Plane/plane and both
  plane/sphere support orders pass independent native construction, D0--D2,
  original-support deletion and intersection-wire comparisons. General
  procedural/variable-radius blend construction is not claimed by these cases.

Remaining in these categories: generic singular/coincident decomposition,
unbounded conic SSI, procedural support types, maximum/local/array ranges,
parameter-bounded range options, general curve projection, and complete
minimum-family metadata for arbitrary surface pairs. These remain open rather
than being covered by the first increment's positive cases.

Verification evidence is recorded in
`docs/reviews/advanced-modeling/pure-geometry-increment-2026-11-05.md`.

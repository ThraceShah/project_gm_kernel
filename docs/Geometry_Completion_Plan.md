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
| Difficult intersections and type coverage | Analytic pairs, tangent circles and certified convex plane contacts; remaining cases below |
| General distances and projection | Point and bounded-pair minima plus exact line/line, sphere/sphere and plane/plane distances; remaining cases below |
| General blend geometry | Exact cylindrical/toroidal rolling-ball increment verified; general procedural blends remain open |
| Geometry editing and higher-order construction | Seven spline editing APIs and clamped bicubic grid construction; supported domains and remaining cases below |

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

## Editing and constrained-construction increment

- Open clamped nonperiodic B-curves and B-surfaces support knot insertion,
  degree elevation and reparameterization through seven public APIs. Elevation
  restores the original continuity after Bezier elevation, with interval
  reinsertion certifying the internal inverse-Boehm step. Attached geometry
  is rejected; edits preserve tags and participate in rollback and mark undo.
- Cubic spline grids accept clamped derivative vectors and explicit corner
  twists, including nonuniform multispan domains. Native normalized-segment
  vector semantics and transpose-before-reversal semantics are preserved.
- Plane contacts with a strictly convex/concave homogeneous numerator can be
  certified as isolated points when the value and gradient are exactly zero.
  Nearby regular loops continue through the regular solver.
- Exact infinite line pairs, sphere pairs and plane pairs supplement the
  global bounded-distance paths. Known continuous-minimum families return
  a witness with the corresponding region flag.
- Exact singleton interval arithmetic has exact-rational regression tests.
  Isoparametric extraction preserves constant homogeneous coordinates by
  affine accumulation; repeated cuts and trimmed annulus section exercise the
  boundary regression exposed by tighter bounds.

Editing remains limited to degree 16, 1024 directional workspace poles and
8192 surface poles. Public knot removal, degree reduction, extension, joining,
unclamped and periodic editing remain open. Automatic twists for doubly
clamped corners, periodic grids and curve-clamped boundaries remain open.
General procedural blends and the other remaining capabilities listed above
have not been completed by these narrower implementations. Variable-radius
blends are deferred at the user's request. Analytically simplified two-stage
blends have a separate geometry/parameterization boundary and performance
probe in `docs/reviews/advanced-modeling/blend-support-performance-2026-11-05.md`.

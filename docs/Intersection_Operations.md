# Public intersection operations

This document describes the implemented intersection scope, its numerical
failure boundary, and the verification of the three public intersection APIs.
It does not describe general NURBS/NURBS surface intersection or claim that the
whole C/D modeling roadmap is complete.

## Public contracts

| API | Results | Mutation |
| --- | --- | --- |
| `PK_CURVE_intersect_curve` | Positions, parameter on each curve, intersection classification | Read only; four independently owned return buffers |
| `PK_SURF_intersect_curve` | Positions, surface UV, curve parameter, classification | Read only; four independently owned return buffers |
| `PK_SURF_intersect_surf` | Isolated positions and basis curves with parameter intervals and classifications | Creates independent geometric curve entities; arrays are caller owned |

The ABI and option versions follow the installed SDK headers in
`third_party/parasolid/include/parasolid_kernel.h`. Functional definitions were
checked against the user-provided Parasolid Functional Description, chapter 61,
“Intersections,” pages 1081–1082. The reference documents and SDK are external
inputs and are not copied into this repository.

The implementation preserves the following observed native semantics:

- Coincident bounded curves return the two boundaries of the coincident
  interval, classified as start/end of coincidence. Ordering follows the first
  input curve, even if the other curve has the opposite direction.
- Surface/curve coincidence returns UV `(-1,-1)` at the interval boundaries;
  these are native sentinels, not endpoint surface parameters.
- Fully coincident surfaces return a successful empty result. No claim is made
  that their coincident area has been partitioned.
- Tangent surfaces can return isolated points or tangent curves as appropriate.
- Isolated curve/curve contacts, including tangency, use the native `simple`
  classification; the `tangent` vector token is used for surface/curve contact.
- Spatial boxes and parameter boxes are areas of interest. The native contract
  permits returning intersections outside them. The current implementation
  returns all supported branches and does not silently trim curves to a hint.
- An explicit seed vector selects one containing analytic branch. Seed selection
  of a generated rational NURBS branch is currently rejected explicitly.
- Surface inputs must both be orphans or belong to the same body. Result curves
  in the latter case are registered as construction geometry of that body.
- The default finite result interval of an unbounded analytic intersection line
  follows the measured PK interval, approximately `[-500,500]`.

Every return array must be released separately with `PK_MEMORY_free`. Freeing
the curve-tag array does not delete its curve entities. Independent analytic
and NURBS result curves do not borrow support-surface lifetimes.

## Implemented geometry

### Curve/curve

| Pair | Supported configurations |
| --- | --- |
| Line / line | Transverse intersection, disjoint and skew curves, coincident interval boundaries, opposite direction |
| Line / circle or ellipse | Arbitrary 3D placements; bounded intervals; tangent classification |
| Circle / circle | Coplanar, noncoplanar, tangency, coincident bounded arcs |
| Circle / ellipse | Coplanar and noncoplanar finite intersections |
| Ellipse / ellipse | Noncoplanar finite intersections; coplanar general ellipses remain unsupported |
| Line / positive-weight B-curve | All roots of a noncoincident auxiliary plane on each bounded knot span; verify the second spatial constraint |
| B-curve / same B-curve | Overlap of nonperiodically bounded parameter intervals |
| Trimmed curve / supported basis | Native parameter remapping, including reversed internal trims |

### Surface/curve

All five analytic support classes—plane, sphere, cylinder, cone, and ring torus—
support bounded lines, circles, ellipses, and the explicitly bounded B-curve
families below. The far cone sheet is rejected.
Plane/B-curve intersection supports ordinary and rational positive-weight
B-splines of degree at most 16 across all intersected knot spans. Periodic curves
are currently accepted only when the requested interval lies within their
stored fundamental domain.

Sphere, cylinder and cone/B-curve intersection supports positive-weight curves
through degree 8; ring torus/B-curve supports degree 4. Homogeneous implicit
substitution visits every intersected knot span and yields a polynomial of
degree at most 16. Complete bounded root isolation handles multiple crossings,
tangencies, clipping and coincidence; unresolved arithmetic and singular cone
apex configurations return explicit failure. See
[`BCurve_Analytic_Intersections.md`](BCurve_Analytic_Intersections.md) for the
normalization, residual bounds, native failure cases and independent wire checks.

### Surface/surface

| Pair | Implemented configurations |
| --- | --- |
| Plane / plane | Transverse line; parallel or coincident surfaces |
| Plane / sphere | Closed circle, isolated tangency, disjoint |
| Sphere / sphere | Closed circle, isolated tangency, disjoint or coincident |
| Plane / cylinder | Arbitrary regular obliquity: ellipse/circle; parallel plane: two lines or tangent line |
| Parallel cylinders | Two lines, tangent line, disjoint/coincident |
| Coaxial sphere / cylinder | Two circles, tangent circle, disjoint |
| Plane / cone | Plane perpendicular to the axis: circle, isolated apex, or disjoint |
| Plane / ring torus | Horizontal planes: complete zero/one/two-circle result; axial planes through the axis: both profile circles |
| Plane / ruled NURBS surface | Bounded positive-weight tensor-product patch: linear V with two control rows and degree 1–8 U across multiple spans, or linear U with two control rows and degree 1–8 V on a single span; exact rational result curves of degree at most 16 |
| Plane / tensor Bezier surface | Both degrees up to 16, positive weights, a single clamped span in each direction, and an exactly separable homogeneous plane numerator; complete isoparametric curves, including curved crossings and tangencies |

The ruled NURBS surface algorithm traverses every nonempty U knot span when V
is linear. The transposed ordering, with linear U and nonlinear V, currently
supports only a single nonempty V span. Both orderings access the stored
control net directly. The supported configurations include
nonplanar bilinear patches, polynomial/rational ruled patches, an
entire coincident boundary, constant-u rulings, and proportional-factor
degeneracy. In particular, a plane through a bilinear saddle discovers both
crossing loci and returns four half-branches terminated at the singular point,
matching the observed native PK branch partition.

Multiple nonlinear V spans are explicitly rejected before producing any result.
The original transposed two-span rational fixture failed the strict oracle:
the bounded calculation returned four branches while native PK returned two.
One native branch extends outside the saved U domain, and a second-span bounded
branch appears only after changing the UV area of interest. The corresponding
untransposed fixture returns all four branches in both kernels. These observed
differences remain an unresolved compatibility boundary, not a passed case.
The complete native counterexample and fixture definition are retained in
[`transposed-ruled-boundary-2026-11-03.txt`](reviews/advanced-modeling/transposed-ruled-boundary-2026-11-03.txt).

For a non-ruled tensor patch, a second exact path tests whether the homogeneous
plane numerator factors as `f(u) g(v)`. The coefficient matrix must pass an
exact-product rank-one test; a nearly separable matrix is rejected instead of
being rounded to a different zero set. Every root of both factors is isolated.
Fixing that parameter in the homogeneous control net gives an exact rational
isocurve. Roots in the other direction divide it at every crossing. A repeated
factor root returns a tangent curve, while an entirely coincident patch returns
the native successful empty result for a coincident area.

A parameter line whose rational control points are all the same is returned
as an isolated point, not a constant B-curve. Points already covered by a
crossing-curve endpoint are removed from the isolated set. A common binary
scale normalizes homogeneous input coordinates before factorization and
interpolation, so uniformly tiny or large weights cannot change a curve into
a point through product underflow. Unresolvable weight ratios or nonfinite
arithmetic return an explicit numerical error. Unit tests compare unchanged
geometry at common homogeneous scales of `1e-200` and `1e200`; native SDK limits
at these extreme scales are recorded separately from positive compatibility
cases in the independent oracle.

This includes patches curved in both parameter directions and either choice of
the fixed parameter. It is not a general bivariate zero-set solver: nonseparable
plane numerators, multiple knot spans in this new path, and periodic patches
remain unsupported. Degree limits and output capacity are checked before
publishing any curve. Positive output weights and the distances of all its
control points bound the entire intersection curve's plane residual.

## Branch discovery and numerical limits

Analytic surface/line equations have degree at most four. For a conic, two
overlapping tangent-half-angle charts cover the entire circle or ellipse while
keeping each chart parameter in `[-1,1]`; the resulting implicit polynomial
has degree at most eight. Roots at the `pi` chart transition are included.

For plane/B-curve intersection, polynomial de Boor interpolation converts the
homogeneous plane numerator on each knot span into a complete polynomial.
Positive weights make roots of that numerator exactly the roots of the
Euclidean plane equation.

`BoundedPolynomialRoots` recursively isolates all derivative roots. Between
consecutive derivative roots the polynomial is monotone, so every simple root
is bracketed, and every critical point is checked for tangency. It does not
discover roots by a fixed sampling grid. A rounded critical value that cannot
distinguish tangency from a close root pair returns numerical failure. Finite
range/degree/capacity failures are also explicit.

For a ruled NURBS patch, write its homogeneous polynomial on one u span as
`A(u) + v B(u)`. The plane equation is linear in v. Substitution gives an exact
rational spatial curve. All roots on both v boundaries and all denominator
roots partition the u domain. A midpoint determines membership only within a
whole root-free interval; this is a sign classification after root isolation,
not a sampled-curve completeness argument. Rational coefficients are converted
to homogeneous Bezier control points. Nonpositive result weights or an
unresolved common polynomial factor cause an explicit failure.

The separable tensor path uses the same complete univariate root isolation for
both Bernstein factors after conversion to the power basis. Returned geometry
owns its homogeneous poles; deleting either original support cannot invalidate
the result. Allocation-failure tests verify that a failed multi-branch call
leaves no published arrays or partially created curve entities.

These are bounded floating-point algorithms with a model-space residual check,
not arbitrary-precision algebraic certificates. Limits are 256 point results
and 64 surface-intersection curve/point results per call. Near-parallel or
ill-conditioned configurations that cannot be resolved are rejected, not
reported as successfully disjoint.

## Two-surface parameter correspondence

`KernelRuntime.TryIntersectionPairedParameters` checks the physical point
against both supports and returns independent UV pairs. Analytic inverses use
the native charts. NURBS inversion uses bounded Newton correction and accepts
only a verified 3D residual; it reports nonconvergence if no valid parameter is
found. This inversion is not used to discover or certify branches.

`KernelRuntime.UnwrapIntersectionParameters` continues periodic analytic chart
coordinates near the previous UV pair, avoiding a false discontinuity at an
angular seam. Singular poles and multiple equivalent NURBS parameterizations
remain caller-visible geometry issues.

The helpers now feed the bounded curved-sheet imprint/trim/section pipeline in
`docs/Curved_Surface_Pipeline.md`. That path builds exact-support ICURVE edges,
splits natural rectangular faces and classifies their sheet cells. The helpers
alone do not classify solids or prove a Boolean result is a closed body.

## Failure, ownership, and validation

Public outputs are initialized to zero/null. Result arrays are published only
after all allocations and geometry creation succeed. Partial return-buffer
allocations are released, and entity creation participates in the existing
command transaction. Unsupported geometry returns `PK_ERROR_not_implemented`;
unresolved numerical configurations return `PK_ERROR_cant_do_intersect`.

`tests/KernelTests/IntersectionOperationTests.cs` covers all-root discovery,
coincidence markers and UV sentinels, reverse/trimmed parameters, analytic
branches and isolated points, rational cubic roots, multiple knot spans,
bilinear hyperbola/saddle intersection, higher-degree ordinary/rational ruled
patches, single-span transposed patches, rejection of transposed multiple-span
patches with no partial outputs or new entities, paired UV reconstruction, output
ownership, allocation failure rollback, and explicit unsupported/ambiguous
results.

`scripts/IntersectionOperationOracle.cs` independently
constructs both input models, invokes both public APIs, compares result sets,
parameters and classifications, creates wire bodies from result curves,
transmits the managed wire to text XT, receives it in real PK, and runs
`PK_DEBUG_BODY_compare`. Oracle output is written to
`temp_docs/intersection-operation-oracle/`. Its positive fixtures include
one-span and original-U multiple-span rational cubic ruled patches,
single-span transposed patches, line/B-curve intersections, native parameter
and classification comparison, and strict received-wire topology/sense/tolerance
comparison. The transposed multiple-span counterexample is replayed separately
as a `BOUNDARY` diagnostic and does not increase the positive compatibility
count. Its original four-versus-two branch mismatch remains recorded even
though the public API now rejects that input.

Run the targeted tests after the normal source/catalog generators and build:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests --filter FullyQualifiedName~IntersectionOperationTests
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/IntersectionOperationOracle.cs -p:BuildProjectReferences=false -p:UseSharedCompilation=false
```

The SDK must be available through the existing `ParasolidScriptHost` setup.
Testhost cleanup follows the repository process-management rules. Actual
validation counts and any remaining oracle differences are recorded in the
stage validation report after execution; this document's support matrix is not
a substitute for those results.

## General regular-locus extension

The fallback now uses outward-rounded homogeneous Bezier bounds and exhaustive
parameter subdivision for NURBS/NURBS and analytic/NURBS pairs. Regular roots
also support NURBS boundary curve intersections. It discovers internal closed
components, joins adjacent spans with both UV pairs, and publishes independently
owned ICURVEs. Analytic charts cover plane, cylinder, cone, sphere and ring
torus. Existing exact paths keep precedence. See
[Curved_Surface_Pipeline.md](Curved_Surface_Pipeline.md) for the algorithm,
capacities, sheet arrangement and current validation workflow.

The native oracle adds polynomial/rational closed loops, NURBS/NURBS,
cross-span continuation, sphere/equator, cylinder, cone and torus intersections,
BCurve/BCurve crossings and public inverse-parameter contracts. Both kernels
construct their own reference geometry before received-wire comparison.

Complete singular-locus handling, pole/periodic face-domain splitting,
arbitrary coincident-region decomposition and continuation through singular
branches remain outside the general fallback. Unresolved or over-budget cells
fail the operation instead of hiding a missing component in an empty success.

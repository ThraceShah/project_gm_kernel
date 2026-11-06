# Trivalent equal-radius solid corners

The corner planner identifies a three-edge star from actual edge/vertex/face
incidence. The three edges must be convex, have the same constant radius and
have three distinct regular Plane/BSurface supports. Each remote endpoint is
trivalent and has a planar termination face. No model name, axis-aligned corner
or fixed coordinates choose the modeling path.

The complete signed-offset intersection is certified by `SurfaceTripleRoots`.
The three rolling bands share the resulting rolling-ball center and one contact
vertex per support. Their near boundaries are exact circular isoparametric
sections. A single analytic sphere fills the three-sided gap. All supports,
contact ICurves, shared edges and coedges are assembled at once, followed by one
atomic body replacement; sequential single-edge fillets are not a corner plan.
Plane/Plane bands remain analytic cylinders. Their remote planar cuts reuse
exact circle/ellipse construction; freeform bands retain procedural Blend
geometry and use their actual finite cap intersection.

`CreateFiniteRollingBlend` consumes the real planar termination faces and
certifies continuation past natural spline limits when required. This permits
the original degree-four/degree-two roof's non-circular remote cap curve; the
original degree-four/degree-four flat-ended family is also retained. A curved
near termination for finite continuation is not substituted by a guessed plane.

Acceptance uses complete interval evidence: positive-definite squared-distance
Hessians and a stationary-witness lower bound certify the material rolling ball;
whole-band boxes certify clearance from other faces; monotone separating
projections and complete circular-section bounds certify neighboring bands do
not overlap. The strong-convexity bound is
`d_min^2 >= d(w)^2 - |grad(|S-C|^2/2)(w)|^2 / mu`, where `mu` is an outward lower
bound for every Hessian eigenvalue over the entire natural UV rectangle.
Budget exhaustion or an unproved region fails explicitly. Unequal radii,
unsupported graphs, mixed convexity, multiple centers, short edges and
uncertified clearance have separate internal events. Once construction starts,
`not_implemented` is converted to a real failed attempt so another algorithm
cannot consume temporary geometry as a successful fallback.

The numerical offset patch is shared with other modeling operations. Its
position and Jacobian enclose `S + d unit(Su cross Sv)`; a normal that cannot be
certified causes subdivision or explicit failure, never an empty result.

Verification uses two author families: an unmodified original body authored by
this kernel and one authored by PK. Both kernels independently receive the same
original text XT, mark the same physical star and execute `fix_blends` with the
same radius and v261 procedural-representation policy. No post-operation result
is fed to the reference construction. Strict checks retain topology,
region/shell, class, sense, tolerance and missing-entity gates; only internal
CHART counts are diagnostic. Each case also checks radius/center, 27 spherical
interface G1 samples, 54 source-contact G1 samples and 108 actual finite band
boundary points against independent native geometry.

The principal matrix covers natural and genuinely extended NURBS endpoints,
rigid poses, reflection/uniform scale, independent U reversal and spatially
inclined poses. Current limitations remain explicit: general curved termination
faces, mixed-convexity corner repairs, unproved band separation and zero-band/
vanishing-face events need further plans. The exploratory non-orthogonal prism
has valid geometry and exact elliptic caps but retains a native curve-sense
representation discrepancy; it is not included in the passing principal matrix.

The final integration hook is `BuildTrivalentLocalBlendCorner`, after the axial
and planar paths and before the regular single-edge/chain path. Its products
follow the existing contract: original faces first, then three bands and sphere.
The producer, oracle and tests use the public `set_blend_constant` /
`fix_blends` protocol after that dispatch hook is installed.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/TrivalentBlendCornerOracle.cs -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release --filter FullyQualifiedName~TrivalentSolidBlendTests -p:UseSharedCompilation=false -p:BuildInParallel=false
```

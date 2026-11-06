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
Hessians and a stationary-witness lower bound certify source-support contact
distance; finite spherical-cone certificates handle other planar boundaries;
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
interface position samples, G1 on each surviving band interface, up to 54
source-contact G1 samples and up to 108 actual finite band
boundary points against independent native geometry.

The principal matrix covers natural and genuinely extended NURBS endpoints,
rigid poses, reflection/uniform scale, independent U reversal, independent
non-unit translated U/V domains and spatially inclined poses. Non-orthogonal
plane stars include exact elliptic far caps. Current limitations remain explicit: general curved termination
faces, mixed-convexity corner repairs and unproved band separation need
further plans.

A defined band-extinction event is handled by this same topology plan. Both
contact spans must collapse and an interval enclosure must certify the entire
spherical section coincides with its actual planar termination within modeling
tolerance. Its zero-width band is omitted. Shared contacts are merged, exact
line/circle carriers are identified, opposite coedges cancel, and vanished
support loops/faces are removed. The body replacement preserves only surviving
original faces. One, two and three simultaneous band extinctions produce the
independently verified 7/14/9, 5/9/6 and 4/6/4 face/edge/vertex topologies.
Sphere/plane halfspace checks use the actual spherical triangle's positive cone
when the complete rolling ball crosses a planar boundary. This also certifies
positive bands arbitrarily close to the extinction station; radii .75 and
.999999 remain positive-band cases rather than being coerced into extinction. Cylindrical
clearance is projected directly as `a + b*u + c*cos(v) + d*sin(v)` before interval
subdivision, retaining correlations under rotation. Arbitrary overrun past the
zero station, partially collapsed contact spans and freeform-band extinction
remain explicit failures; no special block or spherical-body constructor is
used for the event.

The three-section representation follows the supplied blend-definition order.
The first two rolling bands establish the patch and the third circular section
closes it with the opposite carrier sense. Curve sense, endpoint association
and both coedge directions change together, preserving the physical boundary.
`PendingBlendSequence` retains this public API order through copying and fixing;
body storage order is not used as a substitute. Independent native probes
cyclically permuting the same three edges demonstrate this convention for both
analytic and NURBS/Blend bands. The oracle declares the same geometric source
order for both kernels before applying a pose.

The default matrix passes 42 independent cases, with 84 zero-fault native/full
receive checks. Two cyclic input-order matrices add 12 cases each. Every case
retains the complete structural gates and a mass residual threshold of `1e-7`;
maximum observed mass residual is `5.0464e-8`. Actual finite boundaries,
source-support contacts and live spherical interfaces are checked independently.
Zero-band cases additionally save/reload through this kernel, delete the original
and classify interior, removed-corner and exterior points against native PK.
The base lifecycle matrix independently reloads, copies and deletes the received
source, then mirrors/scales the copy. Both author families pass fresh native
receive/full checks, strict comparison, material classification, transformed
radius/G1 checks and restoration of geometry/return-memory ownership. This tests
transformation after modeling, separately from constructing a corner in a
transformed source.

The final integration hook is `BuildTrivalentLocalBlendCorner`, after the axial
and planar paths and before the regular single-edge/chain path. Its products
follow the existing contract: original-face slots first, then surviving bands and sphere; extinct original
faces are not passed to the face-handle preservation operation.
The producer, oracle and tests use the public `set_blend_constant` /
`fix_blends` protocol after that dispatch hook is installed.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/TrivalentBlendCornerOracle.cs -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/TrivalentBlendCornerOracle.cs -c Release -p:UseSharedCompilation=false -- base --lifecycle
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release --filter FullyQualifiedName~TrivalentSolidBlendTests -p:UseSharedCompilation=false -p:BuildInParallel=false
```

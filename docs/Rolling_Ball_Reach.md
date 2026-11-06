# Certified rolling-ball support reach

`RollingBallReach` supplies a sufficient global certificate for primitive
Plane/BSurface supports. It does not construct a blend and does not establish
that a guessed foot is stationary. The caller must already have a joint
interval/Krawczyk certificate for the moving centre, foot and rolling radius.

For `g_C(q) = |S(q)-C|^2/2`, the parameter Hessian is

`H_ij = S_i dot S_j - (C-S) dot S_ij`.

The helper bounds this complete matrix over every natural Bezier span and over
the entire supplied Cartesian centre box. Adaptive UV subdivision must certify
`H_uu > 0`, `H_vv > 0` and `det(H) > 0` in every cell. The returned `strength` is
an outward lower bound obtained from `det(H)/trace(H)`. A caller may subdivide
the spine interval to tighten the centre box. All calls share the caller's
explicit cell budget; unresolved cells do not count as separation or success.

C1 continuity across interior knots is essential. The helper accepts only
nonperiodic surfaces whose internal knot multiplicities are at most degree
minus one, with positive homogeneous weights for rational surfaces. This
conservative condition can reject an accidentally smoother C0 representation.
It avoids a false global inference from separate positive span Hessians. A
manufactured C0 V-shaped surface has two global feet while each affine span
has a positive Hessian. A separate smooth parabola example has two feet and
fails the Hessian certificate itself.

The certified joint foot box must lie entirely in the natural knot rectangle.
Outside-domain feet return `Unsupported`; they are never clamped. Procedural
B-surfaces use tensor-product linear continuation outside that rectangle.
Polynomial Bezier extrapolation is a different geometry and is not used by this
helper. Periodic supports and globally nonconvex squared-distance functions
require a different certificate and remain explicit failures.

On a convex natural rectangle, a C1 function with a uniformly positive
piecewise Hessian is strongly convex. A certified stationary foot is therefore
its unique global minimum. If its distance is the rolling radius, no other
point on that rolling sphere lies on the support. A regular circular band
section consequently intersects each support only at its prescribed contact.
This excludes additional cap-curve/support crossings without applying a
transverse root test to the known tangent endpoints. The caller must still
establish the regular section, complete centre interval, cap contact roots,
finite boundary coverage and clearance from other faces. This certificate
alone does not prove a general blend is free of self-intersection.

Affine planes have a constant positive Gram matrix and are handled directly.
`ValidateC1NaturalDomain` is also exposed so other global distance/convexity
proofs can enforce the same cross-span precondition.

The local-modeling caller now uses `JointNestedBlendEvaluation.CertifySplineReach`
when a new end requires extension of an incident finite edge. Each chart is
covered in full; an interval/Krawczyk solve provides both centre and foot boxes
before this helper is called. Spine subdivision and all support-patch work
consume one shared 131,072-cell budget. Nested procedural supports and exterior
foot tubes require another certificate and return explicit failure here.
The existing corner distance certificate also checks the same C1 prerequisite.

Tests cover a manufactured moving normal-sphere family under spatial rotation,
reflection and uniform scale, C1 multi-span and positive rational supports,
the two-foot counterexamples, unsupported exterior foot/periodic data, shared
budget exhaustion and a degenerate plane.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -p:UseSharedCompilation=false --filter FullyQualifiedName~RollingBallReachTests
```

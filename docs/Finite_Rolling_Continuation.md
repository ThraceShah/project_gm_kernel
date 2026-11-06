# Certified finite rolling continuation

`CreateFiniteRollingBlend` is an internal constructor for a local modeling
consumer with explicit outward planar termination faces. It keeps the existing
public `SURF_create_blend` behavior and the B-surface's original knot domain.
The existing B-surface evaluator and interval spline bounds already implement
the same tensor-product linear extension outside that domain. Polynomial
extrapolation is a different geometry and is not substituted for it.

Each tracing direction must reach a real termination plane by more than the
entire rolling ball's projected radius. The final endpoint interval must prove
this separation. Every retained spine chart must pass the existing joint
Krawczyk tube proof over its full parameter interval, including the linear
extension. For an open branch, complete interval derivative bounds must also
prove strict monotonicity towards its two terminating planes. Adaptive
subdivision has a finite 8,192-cell budget; unresolved cells, nonmonotone
terminations and missing two-sided stopping evidence fail the constructor.

This constructor supplies a certified window containing the selected local
branch's termination cuts. The caller must still construct the actual finite
cap/Blend intersections and validate its final local replacement domain. A
curved termination face has no planar certificate here and must not be passed
as only one remote plane. It requires its own region-separation proof. The
constructor does not claim general singular continuation or all reentering
branches of the underlying unbounded supports.

Manufactured tests use degree-four/degree-two NURBS roofs with a planar support,
three roof-curvature/radius combinations and the actual two endpoint cap
planes. Both ends extend beyond the original knot interval and the original
support remains unchanged. The existing rational and polynomial spline-bound
tests continue to verify interval containment across two natural boundaries.
The spherical-corner modeling consumer supplies the final independent PK
oracle evidence; a standalone rolling sheet is not a solid fillet delivery.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests --filter 'FullyQualifiedName~RollingBlendContinuationTests|FullyQualifiedName~NestedBlendBoundsTests'
```

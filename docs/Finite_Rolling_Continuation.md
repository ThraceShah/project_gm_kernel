# Certified finite rolling continuation

`CreateFiniteRollingBlend` is an internal constructor for a local modeling
consumer with explicit planar barriers directed out of the selected finite
branch. A notch cap's material normal can point into that interval; the caller
therefore orients its tracing barrier from an interior branch witness rather
than equating material sense with termination direction. It keeps the existing
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

Contact ICurve charts retain the actual unwrapped spline-foot UV coordinates
from the same joint spine solve. A bounded inverse cannot recover an extended
foot and must not clamp it back to the original knot rectangle. Both chart
construction and finite contact UV tangents use this shared parameter mapping;
the original support geometry and public bounded inverse remain unchanged.

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

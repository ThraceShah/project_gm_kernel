# Native modeling work reduction

The work branch starts at `610e3ce844bf99c85af6069905f9f3deaab08133` on Mac ARM64 with PK 38.0.150 and .NET 10. The target is complete successful modeling operations, retaining native XT, BLEND, ICurve, CHART and parameter semantics and the existing geometry, topology, lifetime and rollback checks.

## Reused baseline and qualification

The five oracle repetition/profile changes come from `perf/modeling-api@04c7ed0`; no historical report reader or benchmark compatibility layer is included. The production initialization and empty-UV guard are the Native/test slice accepted at `7aca6f0` / review `957ce708`. Its initialization inventory is retained in Git at `perf/interval-bounds-initialization:docs/Interval_Bounds_Initialization_OptA.md`: every scratch read consumes a written active prefix; explicit LU RHS, assembled matrix and live spline cache initialization remain mandatory. The baseline checkpoint adds operation-scoped counts of Jacobian requests, requested directions and actually executed residual directions to the existing optional profile.

Historical evidence is read in place and remains unchanged. The 04c report's inclusive ZeroMemory attribution and suggested 60 repeated bounds per root are not valid exclusive-cost or repetition evidence. The 6bb single-session diagnostic counted 1,446,213 Jacobians, 224,299 bounds and about 503.9 ms in explicit cache clear, mostly seven-variable systems without UV; those counts cover the whole session, not Fix exclusively. Requested bytes do not establish memory bandwidth cost.

The baseline has a known failure in `BlendIntersectionTests.NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative` (expected Success, actual NotConverged). The reused guard test that invokes that same check with spline reuse disabled exposes the same failure; it must remain visible and cannot be represented as a new passing result.

## Current implementation boundary

The major candidate records the original interval residual for a single immutable occurrence/chart structure within `BoundSegment`. Interval values, normal expressions and denominators are invariant across directional seeds. Rebinding a new box recomputes all primal interval values and the original branch/finite guards; each direction propagates only dependent derivatives. Every modeling API retains all root isolation, enclosure certification, refinement and validation steps. The record never crosses a tube, modeling command, modification or rollback and uses bounded caller-owned stack spans.

The initial per-Jacobian recording experiment passed ten focused arithmetic/support tests and the complete strict shortedge operation with unchanged root counts, but added runtime overhead. It is not a performance result. The candidate now amortizes expression structure across the tube's expansion/refinement iterations; spline and rational explicit-spine bounds continue to use their dedicated directional rule. No speed qualification is claimed before the fixed same-host comparison.

No public C ABI changes are involved. Validation uses managed builds and existing strict PK oracle paths; NativeAOT publication is not required or run.

## Artifacts

Working logs and fresh captures are under `temp_docs/pk-native-work-reduction/`. Final fixed-source comparison samples, operation-level qualification and dashboard delivery will be recorded here when complete. A checkpoint commit does not mean final acceptance, integration or publication.

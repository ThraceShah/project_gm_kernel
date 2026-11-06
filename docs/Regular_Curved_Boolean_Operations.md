# Regular curved solid Boolean operations

This document records the original regular NURBS increment and its oracle.
The subsequent finite Cylinder and actual Blend solid extension, continuous
mixed operations and newborn-edge filleting are documented in
`docs/Mixed_Curved_Boolean_Operations.md` and `docs/Mixed_Analytic_Face_Domains.md`.

## Supported contract

`PK_BODY_boolean` and `PK_BODY_boolean_2` now have an internal regular curved
fallback after the existing analytic, rotational and planar implementations.
It constructs union, intersection and difference of finite manifold solids
bounded by planes and regular nonperiodic NURBS surfaces. The operands need
not share chart parameterizations. A curved result can participate in another
Boolean, including intersection of its existing ICURVEs with a new NURBS
support. Intersection uses the union of all tools, as specified by Parasolid.

The native acceptance fixture independently builds each solid from six NURBS
sheet faces and sews them through public APIs in each kernel. Its roof is an
exact biquadratic surface, with five planar NURBS boundary faces. No Parasolid
result geometry supplies our operands or answers.

The regular fallback implements default Boolean options; tracking and
`merge_imprinted` are explicitly unsupported. Existing specialized paths retain
their previous option support. Periodic patches, singular/tangent crossings,
coincident curved areas, arbitrary unbounded continuations and unsupported
boundary representations are outside this increment. Blend solid support is
not covered by this oracle; the separately verified Blend sheet closure is
described in `docs/Curved_Modeling_Closure.md`.

Unresolved root isolation or topology construction returns an error through the
existing atomic command boundary. It must not return an incomplete Boolean.
The regular workspace admits up to 128 input faces, 1024 input edges and 4096
input coedges; output storage and root/branch budgets are also bounded. These
are implementation limits rather than new C ABI contracts.

## Construction and classification

1. Borrow the original exact B-rep without mutating its geometry. Compute each
   support-pair intersection once and clip it to both faces using all certified
   boundary crossings and exact UV winding. Recognize planar positive-weight
   NURBS supports by a control-hull distance bound so their intersections keep
   the native LINE class.
2. Isolate intersections with existing ICURVEs using a six-parameter system
   `A(u,v) = B(s,t) = C(p,q)`. Interval exclusion and Krawczyk inclusion certify
   roots; Newton iteration provides candidates only. Distinct tool crossings
   split shared curves before each face's simultaneous arrangement.
3. Classify connected face cells using oriented ray winding against the
   original solids. UV winding uses two axes and four diagonals, with exact
   parametric-line bounds rather than polygonal approximation. Classification
   failure is not interpreted as empty material.
4. Apply the Boolean material predicate on both sides of each cell. Merge
   surviving cells of the same source face across buried tool cuts and remove
   redundant degree-two vertices only on the same exact curve and adjacent
   parameter intervals. Verify two opposite coedge uses at every result edge.
5. Recover shell components and cavity orientation using ray winding, then
   associate cavity shells with their enclosing exterior component. Clone the
   final exact supports and curves before releasing temporary intersections.
   Preserve the target body and surviving source face handles. Completely
   unchanged separate union components retain their body, face, edge and vertex
   handles.

`PK_BODY_contains_vector` also accepts these regular solids. It identifies
vertices, edges and faces before material/void region classification. Tight
Bezier bounds exclude off-surface points before global projection. Ray and UV
winding use borrowed interval geometry, so a read command creates no temporary
entities. Cavity and exterior void regions remain distinct.

## Reproduction

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/RegularCurvedBooleanOracle.cs
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/VerifyAdvancedModeling.cs
```

The focused oracle covers eleven configurations with all three operations:
block cutter, curved NURBS cutter, closed interior cut, disjoint operands,
enclosed tool, enclosed target, overlapping tools with triple crossings, mixed
overlapping/disjoint tools, a second Boolean on a previously cut solid, and
self XT save/reload followed by another NURBS or block Boolean.

XT materialization restores finite BCurve, ICurve and trimmed-curve edge
parameters from the actual endpoint vertices, respecting the curve sense.
It does not replace a trimmed edge interval by its basis curve's natural
domain. Closed/periodic or declared self-intersecting BCurves, vertexless edges
and endpoint inversions outside the existing supported projection contract
remain outside this recovery path; unsupported/tolerant edges are still
readable without an invented interval. The reloaded regression deletes its
source before classification and further modeling to verify independent
ownership.

For every result it exports our text `x_t` under
`temp_docs/regular-curved-boolean-oracle/`, receives it in real Parasolid and
requires zero faults from full `PK_BODY_check`. It compares region, shell,
face, edge and vertex counts and runs `PK_DEBUG_BODY_compare` with type, sense,
tolerance, missing and unmatched entity tests. Only internal chart-point counts
are diagnostic. Volume, area, centroid and inertia must agree to relative
`1e-7`; 125 independent point classifications per body, plus two interior
cavity queries for enclosed tools, must match enclosure, topology class and
region material semantics.

Unit tests additionally exercise allocation failure at eight budgets, successful
retry, input/output ownership, source deletion after deep copying, original face
handle preservation, unchanged component identity, unsupported-option rollback
and zero managed allocation after warmup. Manufactured root tests require all
disconnected roots without boundary seeds.

This increment changes internal algorithms and no C exports, ABI layout or
native memory ownership contract. Its verification uses managed compilation,
unit tests and the real Parasolid oracle. It does not publish NativeAOT.

## XT finite-edge recovery verification — 2026-11-06

The new regression first failed because all imported BCurve/ICurve edge
interval flags were zero. It now checks real truncated intervals, normal and
negative BCurve sense, classification after self receive and a second block
subtraction. The independent `reloaded` / `reloaded-block` oracle passes all
six union/intersection/subtraction cases against native PK, including full
`PK_BODY_check`, structure, topology, mass properties and point classification.
The expanded complete Boolean oracle passes **33/33** cases, and the focused
regular Boolean plus XT B-rep import suite passes **63/63** tests.
The intermediate self-generated XT inputs and final outputs are retained in
`temp_docs/regular-curved-boolean-oracle/`.

## Verified results — 2026-11-06

- **27/27** independent curved Boolean cases and **3381** point queries passed,
  including closed ring edges with no vertices, cavities and repeated cuts.
- The final full kernel suite passed **1392/1392**, with no skipped tests;
  the XT suite passed **12/12**.
- `VerifyAdvancedModeling` passed its production build, all 12 modeling oracle
  steps and the complete 11-step managed public-modeling regression. Its
  NURBS/Blend sheet oracle retained **79/79** passing cases.
- Production allocation IL guard: **2239** methods, clean. The testhooks build
  checked **2246** methods, also clean. Warm Boolean, classification and deletion
  measured zero managed bytes; allocation-failure and ownership tests passed.
- The implementation catalog remains at **174** descriptors. No NativeAOT
  publication or native ABI smoke was invoked in this increment.

Evidence: `temp_docs/regular-boolean-advanced-final.log`,
`temp_docs/advanced-modeling-verification/`,
`temp_docs/public-modeling-verification/`,
`temp_docs/regular-boolean-oracle-final.log` and
`temp_docs/regular-boolean-full-kernel-final.log`.

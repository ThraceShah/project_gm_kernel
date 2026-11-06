# Planar Boolean operations and topology editing

The public operations in this document run in the independent kernel. Real
Parasolid supplies the independent oracle and display tessellation, not the
production modeling implementation. Analytic sphere operations are documented
separately in `docs/Analytic_Boolean_Operations.md`. The finite regular NURBS
solid fallback and its option limits are documented in
`docs/Regular_Curved_Boolean_Operations.md`.

## Public API and supported geometry

| API | Implemented behavior |
| --- | --- |
| `PK_BODY_boolean`, `PK_BODY_boolean_2` | Union, intersection, subtraction of bounded straight-edge planar solid B-reps |
| `PK_BODY_contains_vector` | Interior/exterior/boundary classification with the appropriate region, face, edge or vertex tag |
| `PK_BODY_imprint_plane`, `PK_BODY_imprint_plane_2` | Split planar solid/sheet faces by a plane, preserving holes and original face identities |
| `PK_BODY_trim` | Select a connected component of a sheet using barrier edges and seed faces; keep or remove that selection |
| `PK_BODY_section_with_surf` | Section planar solids by a plane; return front/back bodies and newly created cap faces |
| `PK_boolean_r_f`, `PK_section_r_f`, `PK_TOPOL_track_r_f` | Release caller-owned result buffers without deleting result entities |

Planar input faces can be concave and contain inner loops. Multiple connected
output solids and bounded void regions are supported. Straight B-curve edges
and planar B-surface supports are recognized geometrically, and the latter's
surface representation is retained. This is not a Boolean implementation for
arbitrary curved NURBS surfaces or nominal-curve-free tolerant edges.

The Boolean input is a target and up to 15 tools. Intersection and subtraction
operate on the **union of the tools**, as defined by the SDK, rather than
successively intersecting the target with each tool. Inputs must be regular
manifold solids in the current partition. Nondefault specialized Boolean
options outside the documented subset are rejected explicitly.

## Construction

Each face is decomposed by the complete arrangement of its boundary lines and
the other operands' support planes. Coplanar boundaries participate in the
same arrangement. Face cells are classified against the original loops and
against material on both sides of the cell. This handles coincident faces,
touching blocks, internal cavities and through holes without retaining an
internal coincident face in the output.

Solid point classification integrates oriented solid angle over the exact
planar boundary. Algebraic polygon fans cancel within concave faces and holes;
they do not become tessellated model faces. Unresolved classification returns
an error, never an assumed empty result.

The selected cells are reconstructed by cancelling internal segments,
normalizing T junctions, reassembling outer/inner loops, and making shared
edges and vertices. Connected shells are classified as material boundaries or
cavities. Each outer component receives its own body; each closed cavity has
its own void region. The exterior shell is published before cavity shells,
including when the producer supplied cavity faces first.

For the Boolean APIs, `merge_imprinted` controls compatible coplanar grouping. The default preserves
the observed source subdivisions, which can produce more faces than a minimal
union of axis-aligned blocks.

The bounded workspaces currently admit at most 512 source faces, 8192 boundary
points and 2048 arrangement cells. Budget exhaustion returns an explicit
unsupported/error result with rollback. A reusable per-thread unmanaged
workspace accommodates nested arrangements and B-rep reconstruction. No
managed allocations occur in the production computational paths checked by
the IL allocation guard.

## Identity, tracking and returned memory

The target body tag survives when the operation produces a first result body.
Other original inputs are consumed as required by the Boolean operation;
disjoint union preserves both original bodies. Surviving source faces retain
their public tags. New intersection edges are reported with derive tracking
from the relevant original faces or edges. The independent oracle compares
the entire record kind, original classes/source geometry and product geometry,
not merely the number of records.

The legacy imprint API returns **all resulting edges in the plane**, including
edges that already existed. Repeating an imprint therefore returns its edges
again. The modern API returns one create record with that complete product
list and no original topology entries. A modern imprint that misses returns
`PK_ERROR_partial_no_intersect`; its legacy counterpart returns an empty edge
array with success. These distinctions are covered by independent SDK probes
and the retained oracle cases.

Section honors front/back/both fences. A transverse cut creates exact planar
caps; front caps use the cutting plane negatively, back caps positively. A
coincident end plane or a plane outside the body creates no new cap. Original
target face tags distinguish existing coplanar faces from new caps. The input
cutting surface remains independently owned.

Section requires default `merge_imprinted=false` and `merge_new_faces=false`.
Nondefault section merging is explicitly unsupported: native section merging
protects edges adjacent to new caps under different rules from Boolean
coplanar merging. Passing a Boolean merge flag through would silently remove
different faces and change the returned cap-face list.

For an outside point, classification returns the bounded cavity region when
the point lies in one, and the unbounded exterior region otherwise. Boundary
queries prefer a vertex over its incident edges, and an edge over its incident
faces.

All mutations run in the existing command transaction. A failure restores the
source models and clears result pointers; allocation failures are injected in
the tests. Result-free functions free buffers only and accept their already
cleared result a second time.

## Interchange and downstream editing

Results use the same pooled B-rep representation as primitive and profile
modeling. Fresh XT is generated from those records. Exported models are loaded
and checked by the real SDK. Imported negative-use line curves retain their
geometric interval and topological traversal when trimming a sheet.

The shared XT layer now transmits each fin-specific parameter curve with its
HALFEDGE owner, includes every dependency in the proper geometry chain, and
preserves SP curves' floating 2D B-curves. Valid tolerant edges without a
nominal edge curve can be materialized, copied, transformed and freshly
exported. A fin that points to a curve owned by an EDGE is rejected as corrupt.
The current planar Boolean/trim reader still rejects unsupported tolerant
boundary combinations explicitly.

## Independent verification

`scripts/BooleanOperationOracle.cs` contains 43 independent fixtures: 33
Boolean cases, nine imprint/trim/section cases, and a negative default-size-box
case. The positive cases use both kernels to perform the same public operation,
transmit the self result to XT, load it into real PK, run full `PK_BODY_check`,
and compare topology, surface/curve classes, senses and tolerances with
`PK_DEBUG_BODY_compare`. Parametrization-dependent full matching remains
diagnostic under the existing project policy.

The matrix includes rotated overlap, coincident/touching/disjoint operands,
multiple tools, internal voids, through holes, a valid translated model,
oblique imprint, both trim choices and all section fences. The negative fixture
checks that **both** producers are rejected by PK's default size box at a
10000-unit translation; that invalid input is not counted as a positive model.

`scripts/PlanarTrackingOracle.cs` adds 33 independent contract cases: 27
Boolean tracking cases, five modern imprints, and a real-PK negative-line-use
import/trim round trip. Reports and generated models are written under
`temp_docs/boolean-operation-oracle` and `temp_docs/planar-tracking-oracle`.

Unit regressions are in `tests/KernelTests/BooleanOperationTests.cs`,
`tests/KernelTests/PlanarEditingTests.cs`, and
`tests/KernelTests/PlanarMissingGeometryTests.cs`. The safety cases cover
publicly detached geometry, pre-command journal allocation failures, and
unsupported section merge options. The complete acceptance entry point
is `scripts/VerifyAdvancedModeling.cs`.

## Remaining boundaries

General curved-solid Boolean construction, arbitrary freeform face splitting,
partial-edge imprint and sewing, singular contacts, nonmanifold results and
unbounded topology are not implemented by this planar arrangement engine.
The sphere extension has its own explicit scope. These supported paths are
working modeling operations, not a claim that all C/D/E roadmap items or all
generated PK exports are complete.

The NURBS-sheet extension connects exact and general regular plane/surface
intersection to imprint, seed trim, plane-side classification and sheet B-rep
construction, including closed cuts, holes and repeated cuts on trimmed faces.
Its scope and independent oracle are documented in
`docs/Curved_Surface_Pipeline.md`; it does not broaden the solid Boolean API.

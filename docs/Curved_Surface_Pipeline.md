# Regular surface intersection, sheet partition and trimming

The regular Blend-sheet extension and corrected public interest-region
semantics are tracked in `docs/Curved_Modeling_Closure.md`. Blend sheet
boundaries retain exact circles and surface parameter curves; subsequent
plane cuts also split procedural ICURVE edges. Sheet operations still use
plane cutters; this extension does not implement general curved-solid Booleans.

Updated 2026-11-05. The pipeline connects bounded surface intersection,
parameter-space arrangement, plane-side classification and shared B-rep
construction. It retains the earlier exact intersection paths and adds a
general regular-locus solver. This stage covers sheet modeling; curved-solid
Boolean assembly and blend construction remain separate work.

## Intersection foundations

Positive-weight polynomial/rational B-splines are extracted into homogeneous
Bezier spans using outward-rounded interval arithmetic. Restricted control
hulls enclose positions and first derivatives, including zero-width parameter
boxes. Common homogeneous weight scaling is normalized before extraction.

The general solver subdivides the four-dimensional parameter domain. Hull
separation excludes empty cells; a contracting Jacobian minor proves a cell is
a regular graph. Complete boundary-root isolation and intermediate sections
admit its arcs. Searching the whole domain discovers internal closed loops
without boundary seeds. Joining uses position and both support UV pairs,
including adjacent knot spans and analytic chart seams.

Supported pairs include NURBS/NURBS and NURBS with plane, cylinder, cone,
sphere or ring torus. Analytic operands use bounded rational charts during
discovery and native parameters in the result. Curve/curve and surface/curve
operations use the same bounded roots for regular NURBS boundary crossings.
Global B-spline closest-point search uses distance bounds; Newton only improves
candidate points. Public `PK_SURF_parameterise_vector` and
`PK_CURVE_parameterise_vector` validate membership before returning parameters.

Results are ICURVEs defined by two independently owned exact supports and
verified chart witnesses with paired UV data. Chart anchors do not replace the
intersection with a polyline. Each admitted cell supplies a conservative
chordal-error bound; charts retain explicit angular bounds. Seed selection
checks actual curve membership between anchors and rejects ambiguity.

## Public sheet operations

| Operation | Behavior |
| --- | --- |
| `PK_BODY_imprint_plane` | Partition B-surface sheet faces and return cutting edges |
| `PK_BODY_imprint_plane_2` | The same partition with create tracking |
| `PK_BODY_trim` | Keep or remove the component selected by barriers and seed faces |
| `PK_BODY_section_with_surf` | Return connected front, back or both sheet components without cap faces |

The arrangement reads actual boundary loops, curve ranges and fin senses.
Shared input edges are split once. Open cuts are clipped against the material
domain; internal closed cuts produce an inner face and an outer face with a
hole. Exact isoparametric-ray intersections determine UV winding. The rotation
system traces cycles, nests holes, classifies material cells and feeds the
common B-rep builder.

The same path accepts smaller rectangular domains, multiple B-surface faces,
existing holes and previously trimmed faces. A second plane splits existing
ICURVE edges, including wrapped arcs of closed curves. Repeating a plane
preserves topology and returns its existing cut edges. Sheet export uses
shared closed-curve limit nodes and null dummy-fin loop links, as required by
real Parasolid receive and body checking.

The body and surviving source faces retain their tags. A no-hit imprint
preserves all original topology. A no-hit section retains the original handles
on an included side, or consumes the sheet if that side is excluded. Returned
geometry owns its dependencies. Allocation and numerical failures roll back
both geometry and topology and publish no partial result arrays.

## Explicit boundaries

The generic paths require a regular transverse locus, positive rational
weights and degrees at most 16. Coincident areas, isolated contacts, tangent
branches, singular crossings and unresolved numerical cells return an error;
they never become a successful empty intersection. C0 interior knots still
need crease topology and are rejected. Complete pole/periodic face-domain
decomposition, mixed analytic/B-surface sheet arrangements and general
curved-solid point classification are not included.

Capacity limits are 8,192 intersection nodes, 64 branches per pair, and 200,000
discovery cells per span pair. Sheet arrangements allow 512 faces, 4,096 edges,
4,096 points, 2,048 loops and 16,384 coedges. Root isolation and continuation
have separate bounded workspaces and iteration budgets. Exceeding a capacity
fails atomically. These limits do not certify every possible regular input
will converge within the budgets.

## Verification

Computational tests independently evaluate B-spline positions and derivatives
against bounds, discover open and internal closed branches, verify both UV
domains, test global distance minima, and check chart membership. Public tests
cover source deletion, closed-loop trimming, repeated cutting, annular section,
managed allocation counts and injected allocation failures.

The independent native oracles create the same operands and operations in
both kernels, export our text XT, receive it in real Parasolid, run full
`PK_BODY_check`, and invoke `PK_DEBUG_BODY_compare`. Region/shell semantics,
topology counts, geometry classes, senses, tolerances and missing entities are
hard gates. Native chart anchor counts and internal parameterization differences
are diagnostic only. Extreme-weight native failures remain explicit boundary
diagnostics and are excluded from compatibility counts.

Run the complete regression, native oracle and NativeAOT gates with:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyAdvancedModeling.cs
```

The suite also runs all kernel/XT tests, earlier modeling oracles, the function
catalog, allocation IL guard, NativeAOT publication and native ABI/modeling
smoke tests. Current logs are under
`temp_docs/advanced-modeling-verification/` and
`temp_docs/public-modeling-verification/`; generated intersection and sheet XT
fixtures are under `temp_docs/intersection-operation-oracle/` and
`temp_docs/curved-sheet-partition-oracle/`.

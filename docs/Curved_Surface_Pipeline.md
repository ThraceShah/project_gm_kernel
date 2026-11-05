# Exact curved-sheet partition and trimming

The existing plane/NURBS intersection solver now feeds a modeling pipeline:

1. Discover every supported exact intersection branch and boundary root.
2. Match branch endpoints to the natural face boundary with a checked 3D
   residual, then construct the boundary/intersection incidence graph.
3. Traverse that graph into face loops and classify each connected cell on the
   front or back of the cutting plane.
4. Build shared vertices, edges, fins and faces using the common B-rep builder.
   Imprint keeps the partition; seed trimming selects its faces; section
   publishes independently owned front/back sheet components.

This is a bounded connection of the existing exact intersection capabilities.
It does not implement arbitrary NURBS/NURBS intersection or curved-solid
Boolean construction.

## Public operations

| Operation | New behavior |
| --- | --- |
| `PK_BODY_imprint_plane` | Partition a natural rectangular NURBS sheet and return the exact cutting edges |
| `PK_BODY_imprint_plane_2` | The same partition with one create tracking record containing all cutting edges |
| `PK_BODY_trim` | Consume the partition through existing barrier/seed selection, keeping or removing the selected component |
| `PK_BODY_section_with_surf` | Classify cells and return front, back or both sets of sheet bodies; no cap faces are created |

The body and one surviving original face retain their tags. A missing imprint
leaves the original topology intact. A missing section preserves all original
handles on the retained side, or consumes the sheet when that side is excluded.
Other geometry is independently owned; deleting the original support or the
cutting plane cannot invalidate a returned sheet. Mutations and returned memory
participate in command rollback.

## Supported boundary

Input is a single positively oriented, nonperiodic B-surface face with its full
natural rectangular boundary. Exact boundary curve definitions, parameter
ranges and traversal senses are checked, rather than inferred from four corner
positions. Positive-weight ruled polynomial/rational patches and the existing
separable tensor intersection family can participate when their branches meet
the conditions below.

Each cut must be transverse, open, and have two distinct endpoints on the
rectangle. Multiple disjoint cuts are supported, including multiple result
components on one side. Endpoints at corners, shared endpoints, interior
junctions, isolated contacts, tangencies, boundary coincidence, closed interior
loops, periodic patches and previously trimmed input domains are rejected.
Branches that terminate at an interior knot boundary still require continuation
and joining before this path can use them. Interior knots of multiplicity at
least the degree are rejected because they require additional crease topology.

The boundary root count must equal twice the cut count. Cell boundary signs
must agree throughout the root-free boundary arcs. A missing or ambiguous
partition is an error, never a successful empty classification. Limits are 64
cut branches, 132 boundary vertices and 65 face cells. Existing solver degree,
weight and numerical restrictions also apply.

## Geometry and parameterization

Spatial intersections are discovered as exact rational Bezier branches. Their
topological edges use `ICURVE`, matching real Parasolid imprint/section output.
The two exact supports determine the geometry. Thirty-three verified chart
witnesses (two for a straight ruling) and paired UV values define each curve's independent parameterization;
they do not replace the curve with a polyline. Positive-weight Bezier subdivision
provides conservative chordal/angular chart bounds and checks monotone chord
projection. Unsupported charts fail before result publication.

Native and independent charts can use different anchor counts. Oracle reports
that count as a parameterization diagnostic. Region, shell, face, edge and
vertex counts, curve/surface classes, senses, tolerances and missing topology
remain required checks. Every received body must pass real `PK_BODY_check`.

The plane-side classification here is for connected sheet cells during section;
it does not extend `PK_BODY_contains_vector` to arbitrary curved solids.

## Verification

`tests/KernelTests/CurvedSheetPartitionTests.cs` covers curved imprint-to-trim,
modern tracking, disconnected sections, no-hit identity, singular/unsupported
boundaries and allocation failure rollback. Computational code uses stack or
unmanaged command scratch and is covered by the allocation IL guard.

`scripts/CurvedSheetPartitionOracle.cs` independently constructs polynomial and
rational bilinear, cubic and smooth multi-span inputs, straight rulings and
non-ruled tensor isocurves in both kernels. Its 48
fixtures cover imprint, both trim choices and all section fences. It saves our
text XT, receives it in real Parasolid, checks each body, matches unordered
components, and invokes `PK_DEBUG_BODY_compare` with structural gates. Generated
models and diagnostics are under `temp_docs/curved-sheet-partition-oracle/`.

The oracle is registered in `scripts/VerifyAdvancedModeling.cs`. Run it with:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/CurvedSheetPartitionOracle.cs -p:UseSharedCompilation=false -p:BuildInParallel=false
```

Validated on 2026-11-05: all 1,224 KernelTests (including 16 new partition
regressions), all 48 curved-sheet oracle cases, and all 44 existing intersection
oracle cases passed. Linux x64 NativeAOT publication, the function catalog and
the zero-allocation IL guard passed. Logs are in
`temp_docs/curved-sheet-tests.log`, `temp_docs/curved-sheet-oracle-build.log`,
`temp_docs/curved-sheet-intersection-regression.log` and
`temp_docs/curved-sheet-aot.log`.

# Public profile modeling

The profile pipeline creates topology and geometry in the managed kernel. Parasolid is used by `scripts/ProfileModelingOracle.cs` as an independent producer and a receiver/checker of the completed managed XT, never to build the managed result.

## API flow

| Operation | Public entry point | Result |
| --- | --- | --- |
| Assemble bounded curves | `PK_CURVE_make_wire_body_2` (or the single-curve `PK_CURVE_make_wire_body`) | Open or closed wire components, explicit curve bounds, vertices at joints, one void region and a shell per component |
| Attach face topology to closed loops | `PK_EDGE_make_faces_from_wire` | One bounded face; no surface is implicitly attached |
| Attach a supporting plane | `PK_PLANE_create`, `PK_FACE_attach_surfs` | Evaluatable planar face |
| Create a polygonal face directly | `PK_BODY_create_sheet_planar` | Planar sheet, including polygonal holes |
| Extrude the sheet | `PK_BODY_extrude` | New solid with lateral faces and two caps; the input sheet remains available |
| Revolve the sheet | `PK_BODY_spin` | The input body handle becomes the resulting solid; partial rotations have caps, full rotations do not |
| Check structural validity | `PK_BODY_check` with the supported options below | Fault records for invalid incidence, loops, manifold edge use, or missing geometry |

`PK_EDGE_make_faces_from_wire` takes one initial edge and sense per loop, not every edge of each loop. `shared_loop[0] = -1` starts the face; each hole uses `shared_loop[i] = 0`. The current implementation creates one face from this outer-loop/holes group. The caller must attach a surface separately. This contract was checked against the real SDK; passing null or zero for the first `shared_loop` entry is not equivalent to `-1`.

For polygon creation, `options.loops` labels every input vertex. Vertices of a loop must be consecutive, the first loop is the outer boundary, and subsequent loops are holes. Omitting the labels creates one outer loop. The `plane` option describes the input vectors in local coordinates; `basis_set` independently transforms the completed local sheet into world coordinates. Each loop implicitly closes from its final vertex to its first vertex. Duplicate consecutive vertices and self-intersecting polygon boundaries are rejected.

## Geometry and topology

Wires accept bounded lines, circles, ellipses, and B-curves. Endpoint matching uses the requested tolerance. Branching vertices with degree above two are rejected. A full periodic circle or ellipse does not acquire artificial vertices; its native interval remains recorded on the runtime edge. Multiple wire components require `allow_disjoint`.

For an open clamped B-curve, a bounded wire owns an exactly clipped B-spline
support with its original parameter interval. Boehm knot insertion removes
unrelated portions of the original curve, including C0 junctions outside the
edge. Internal C0 knots are split only when the two geometric tangents are not
G1; a change in parameter speed alone leaves a single edge. This matches the
native distinction between a geometric corner and a parameter derivative jump.
Each returned edge index refers to the original input curve, so one input can
produce several edge/index entries. The command's 512-edge capacity applies
after this splitting. Exact clipping currently uses the existing degree-16,
1024-pole, nonperiodic clamped-spline construction path. Other previously
accepted layouts retain their original support only when its whole domain has
no non-G1 knot to leak into the edge; other partial domains fail explicitly.
Returned supports survive deletion of the input curve.

`ProfileCurveBoundsTests` checks polynomial/rational clipping, original
parameters, C0 splitting, G1 preservation, input-index mapping and allocation
rollback. `BCurveAnalyticIntersectionOracle.cs` independently checks the
bounded and whole C0 wire contracts against real PK, including full body checks
after self XT import and strict structural comparisons.

Face construction requires a planar boundary. Planarity is checked from analytic data or all positive-weight B-spline poles, rather than just sampled positions. Lines and circular arcs have analytic crossing checks. Accepted spline arcs have monotone Cartesian control projections, with pairwise separating control hulls; a monotone spline plus its closing line is also supported. Ambiguous spline/ellipse hull arrangements return `PK_ERROR_not_implemented`. This is a conservative subset, not a general planar curve arrangement or a general intersection solver.

Outer and hole loops receive opposite traversal directions. A sheet has one void region and one shell, with both face sides belonging to that shell. The public boundary uses one face fin per edge. XT additionally requires the opposite dummy halfedge; the writer emits it and its vertex incidence without inventing an extra public face fin.

The runtime stores topology in unmanaged pools and uses command scratch for evaluation/validation workspaces. One command currently accepts up to 512 profile edges; larger or particularly large spline control hulls can exceed the bounded workspace. Failure returns an error and rolls back the command rather than publishing a partial body.

## Extrusion

Extrusion currently accepts one bounded planar sheet face, including holes. Both bounds must be distance bounds, and the effective end distance must be greater than the effective start distance. Each bound's `forward` flag controls its signed distance. The sweep direction must be a unit vector with a nonzero component normal to the profile plane. Oblique directions are supported.

The constructor duplicates the boundary curves for both caps and creates path edges at profile vertices. Lateral faces are planes for line segments, cylinders for perpendicular circular sweeps, and exact tensor-product B-surfaces for B-curve profiles. A B-curve's degree, knots and homogeneous poles form one parameter direction; a linear path supplies the other. Other accepted analytic curve sections use swept surfaces. Caps preserve the original outer and inner loops. Every resulting solid edge must have exactly two opposite face fins, with closed reciprocal loop incidence.

This implementation does not support extrusion to a bounding face/body, replacing the swept result with `extruded_body`, intersecting or disjoint solid results, or arbitrary nonplanar profile sheets.

Extrusion returns `PK_TOPOL_track_create_c` records mapping the original profile face to each cap and every original edge to its lateral face. This follows the SDK's tracking contract; path edges and copied vertices do not add tracking records. Release the result with `PK_TOPOL_track_r_f`, and release the local status result with `PK_TOPOL_local_r_f`. Both release functions clear their result structures after success.

## Revolution

Revolution accepts a finite nonzero angle with magnitude at most one full turn. The unit axis must lie in the profile plane. The complete accepted profile must stay strictly on one side of that axis. Control hull bounds establish this condition; an axis-touching or axis-crossing profile requires singular topology splitting and is currently rejected explicitly.

Partial revolutions create two rotated copies of the profile boundary, circular path edges, lateral faces, and two caps. Full revolutions omit the caps and retain the original profile edges as seams, with circular path edges returning to the original vertices. This matches `PK_BODY_spin`, whose base edge handles remain valid. A closed circular profile retains its vertexless seam circle. Analytic lateral geometry is retained where the sweep is a plane, cylinder, cone, or torus. B-curve profiles form exact rational tensor-product B-surfaces, combining the original homogeneous profile poles with quadratic circular arcs of at most a quarter turn. Other accepted analytic profile sections use a spun surface. These spline surfaces use exact constructions, without sampled fitting.

`PK_BODY_spin` modifies the original body handle. Original base edge and vertex handles remain live in the result. Full B-spline revolutions use a clamped rational circle with equal endpoint positions and tangents and declare its periodic parameter direction, so the seam is evaluated and checked as a smooth periodic join. The B-surface constructor validates this seam before accepting it. The command journals the body replacement and topology ownership, so allocation failure and mark rollback preserve the original model. A hole revolved through a full turn becomes a separate enclosed void region with its own boundary-shell pair; partial turns retain openings to the exterior.

## Public checker scope

`PK_BODY_check` accepts options version 6 and returns normal `PK_check_fault_t` records. The supported checks cover pool/owner consistency, fin/edge/loop reciprocity and loop closure, shell/region incidence, the two opposite uses of solid edges, and missing curve/surface/point geometry.

Use `loops`, `sh`, `corrupt`, and `missing_geom` as required. Set the unsupported geometry, B-geometry, mesh, topology/geometry intersection, size-box, face self-intersection, face/face, nominal geometry, and attribute checks to their `*_no_c` values. Use `PK_check_returns_0_c` and `PK_check_extra_faults_0_c`. Requesting an unsupported check returns `PK_ERROR_not_implemented`; it does not silently return a clean result. Full Parasolid geometric checking is an independent acceptance gate in the oracle.

## Validation entry points

- `tests/KernelTests/ProfileModelingTests.cs`: public construction chains, holes, oblique and signed distance bounds, partial/full positive and negative revolutions, curve preservation, structural faults, allocation rollback, and spin undo.
- `scripts/ProbeProfileContracts.cs`: small real-SDK probes for face creation, loop indices, surface attachment, and periodic wire topology.
- `scripts/ProfileModelingOracle.cs`: independent managed and Parasolid public-API producers, fresh managed XT receive, topology-count equality, full real `PK_BODY_check`, and `PK_DEBUG_BODY_compare` with class/sense/tolerance/missing checks enabled. Per-case XT and reports are written to `temp_docs/profile-modeling-oracle`.

On 2026-11-03, all 33 independent modeling cases passed against Parasolid 38.0.150 in the final unified A/B acceptance run. The passing run includes polygonal and curved wires, planar holes, concave profiles, oblique extrusion, rational and non-rational B-curve profiles, oriented input frames, and positive/negative partial/full revolutions. Each case passed fresh managed XT receive, exact topology-count equality, full default real `PK_BODY_check` with zero faults, and the strict class/sense/tolerance/missing-geometry structure gates of `PK_DEBUG_BODY_compare`. Extrusion cases also matched the independently generated SDK tracking records. Detailed per-case results are in `temp_docs/profile-modeling-oracle/report.txt`; the reproducible acceptance command is `dotnet run scripts/ProfileModelingOracle.cs`. The same final run also passed the NativeAOT C-export holed extrusion and full-revolution pipelines described in `docs/Display_Bridge.md`.

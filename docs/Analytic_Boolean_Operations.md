# Analytic sphere Boolean and cutting operations

This implementation extends the public Boolean path from bounded planar solids
to exact analytic sphere solids. It uses sphere and circle equations and the
kernel's shared B-rep builder; no display tessellation or external Parasolid
call participates in production modeling.

The native ABI is defined by the installed SDK header. Ownership, tracking,
entity identity, coincidence, and contact behavior were independently probed
with the real SDK and retained in the independent oracle fixtures. The
reference and implementation paths create separate models and run separate
kernels.

## Boolean support

`PK_BODY_boolean` and `PK_BODY_boolean_2` support union, intersection, and
subtraction of two complete, regular sphere solids, in arbitrary positions and
with independent sphere parameter frames.

| Spatial relation | Result construction |
| --- | --- |
| Disjoint spheres | Union returns both original bodies; intersection consumes both and returns no bodies; subtraction retains the target |
| Coincident spheres | Union/intersection retain the target sphere; subtraction is empty |
| Strict containment | Retain the appropriate existing sphere boundary; nested subtraction creates a separate cavity shell and void region |
| Transverse intersection | Two exactly trimmed spherical faces joined by one closed circle edge, with no artificial seam vertices |

The target body tag survives whenever a result body survives. A surviving
source spherical face keeps its face tag, including when the tool boundary
replaces the target boundary. A transverse intersection returns the observed
native tracking record: the two original faces derive the new circle edge.
No-change and nested-cavity cases return no tracking records.

The return status distinguishes native `success` and `no_clash` cases. In
particular, intersection with a containing tool and intersection of coincident
spheres return `no_clash` while still returning a result body.

Exact tangency is currently rejected with `PK_ERROR_not_implemented`. The
native kernel introduces point-vertex loops at such contacts, and internally
tangent subtraction can return a failed result without consuming its inputs.
Those singular cases cannot be represented by a regular two-cap construction.

The Boolean reader accepts complete sphere solids. General subsequent
Booleans involving already trimmed spherical patches, cylinder/cone solids,
mixed planar/curved solids, or more than one tool are outside this analytic
path; the existing planar path retains its independent support scope.

## Plane section

`PK_BODY_section_with_surf` cuts a complete sphere by an arbitrarily placed
plane. The `front`, `back`, and `both` fences return the selected exact spherical
cap solids. Each transverse cap has one spherical face, one planar closing
face, one closed circle edge, and no artificial seam vertices. The returned
front/back face arrays contain the closing planar faces.

For both sides, the original body and spherical face tags survive on the front
side. For a single side, they survive on that selected side. A plane outside
the sphere either preserves the whole sphere on its side or consumes it when
the opposite side was requested. The input cutting plane remains independent.
Native circle/fin storage senses are preserved as well as their physical
material orientation. Tangent sections require point-vertex loops and return
explicit `PK_ERROR_not_implemented`.

## Point classification

`PK_BODY_contains_vector` classifies complete spheres, regular two-sphere
Boolean results, nested spherical cavities, and spherical cap solids using their analytic solid
equations. Boundary classification returns the appropriate face or circle
edge. Material points return the material region, and cavity points return the
cavity's own void region rather than the unbounded exterior region.

Classification recognizes supported B-rep geometry and orientation; it does
not rely on a hidden operation-history tag. Unsupported topology returns an
explicit error instead of guessing from sampled faces.

## Mutation and allocation failures

Construction uses owned copies of support geometry. The command transaction
contains result construction, preserved-face identity swaps, target-body
replacement, and tool consumption. On failure, original bodies and faces are
restored. Return arrays and tracking buffers are released when an operation
fails before publishing them.

The regression suite injects allocation failure through the construction and
checks that source entities remain valid, no partial result is published, and
all caller-return buffers are reclaimed. Production operation and classification
code use unmanaged storage and stack spans.

## Independent validation

`scripts/AnalyticBooleanOracle.cs` creates independent input spheres in the
self kernel and in the real SDK, performs the same Boolean API operation, and
compares:

- Result status, output body count, surviving body/face identities, and tracking
  record structure and entity classes.
- Point classification at material, exterior, and boundary positions, including
  an independently generated native XT body imported into the self kernel.
- The self result transmitted to XT and received by the real SDK, with full
  `PK_BODY_check`, region/shell/face/edge/vertex counts, and strict
  `PK_DEBUG_BODY_compare` topology, tolerance, class, and sense checks.

The Boolean oracle passes 30 fixtures with full mass, area, center-of-gravity,
and inertia comparison in addition to the gates above. The fixture matrix
contains equal and unequal radii, general center directions,
coincidence, disjointness, both containment directions, concentric cavities,
translated geometry, and independently rotated sphere bases. Actual validation
results are written to `temp_docs/analytic-boolean-oracle/report.txt` and the
stage validation report after execution.

`scripts/AnalyticSectionOracle.cs` passes 30 independent sphere/plane cuts
with the same XT, native body validity, topology, classification, identity, and
mass-property gates. The scope includes all three fences, arbitrary oblique and reversed planes,
translated models, outside planes, and small caps. Its output is saved to
`temp_docs/analytic-section-oracle/report.txt`.

These exact analytic cases do not constitute a general curved-solid Boolean
engine. General curved branch discovery, face splitting, arbitrary cap
arrangements, singular contact topology, and repeated mixed-surface modeling
remain explicit limitations.

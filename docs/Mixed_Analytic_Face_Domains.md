# Mixed analytic finite face domains

## Contract

The regular solid Boolean pipeline now reads finite cylinder faces alongside
planes, regular nonperiodic NURBS and the existing Blend supports. It uses the
same support-pair intersection, finite clipping, face arrangement, material
classification and shell construction as the curved Boolean implementation.
There is no oblique-cylinder or block-hole modeling branch.

Cylinder/plane intersections retain exact Line, Circle or Ellipse geometry.
Curve use sense follows the cross product of the original ordered support
normals, including their surface senses. Reordering supports for the existing
intersection construction does not change this convention. Full ring edges
remain vertexless; cuts create actual finite analytic arcs and shared vertices.

A cylinder's `u` coordinate is periodic and its axial coordinate is unbounded
at the surface level. Its finite face domain comes from the actual loops.
Containment intersects loop curves with the exact radial half-plane at the
query angle and counts oriented crossings along the positive axial ray. This
handles both the noncontractible loops bounding a full cylindrical band and
contractible loops after angular cutting, without introducing a seam edge or
approximating the boundary by a UV polygon. Circle/ellipse closing-point
duplicates are removed only on the selected half-plane.

Solid ray classification uses the exact analytic cylinder/line equation. A
finite root-search upper bound follows from radial distance, cylinder radius
and the ray's transverse speed. Source-face witnesses still use the existing
exact winding test; sampled UV values only choose a witness-search step.

Finite circular and elliptic edges can cross `2π`. One internal helper lifts
the inverse parameter into the finite edge's equivalent turn before clipping,
fragmentation, boundary-coincidence checks and topology classification. The
public curve inverse API continues to return its principal natural parameter.
Saving and reloading a partial arc restores its finite interval before the next
operation.

## Limits and failure behavior

This increment admits Cylinder in the common reader; it does not admit general
Cone, Sphere or Torus finite face domains. Existing specialized analytic paths
retain their contracts. Pole handling, curved coincident regions and general
tangent events remain outside this increment.

Skew Cylinder/Cylinder support pairs explicitly return `not_implemented` in
the shared finite-domain intersection path. Their finite intersection branches
have not been validated here. Existing coaxial rotational Boolean handling is
unchanged.

An axial winding ray coincident with a boundary generator, a tangent or an
unresolved intersection returns an explicit error. Such a failure is not an
empty face or a successful partial Boolean. Existing workspace and branch
budgets remain in force. The nonanalytic winding branches are unchanged.

## Reproduction

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests --filter FullyQualifiedName~MixedAnalyticBooleanTests
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/MixedAnalyticBooleanOracle.cs -p:UseSharedCompilation=false -p:BuildInParallel=false
```

The independent oracle builds both operands separately through each kernel's
public APIs. It covers union, intersection and subtraction for an oblique
cylinder and a block, a rotated cylinder seam, mirrored operands, self XT
reload followed by a cut parallel to the cylinder axis, and a third cut across
the resulting partial ellipse arcs. The partial-domain cases exercise two
distinct seam choices.

Each output is freshly transmitted as text XT into
`temp_docs/mixed-analytic-boolean-oracle/` and received by real Parasolid. Both
native and received outputs must pass the full `PK_BODY_check`. Topology counts,
region/shell structure, `PK_DEBUG_BODY_compare` type/sense/tolerance/missing
tests, mass properties and independent point/topology classifications are
required. Curve sense differences are failures, not diagnostic exceptions.

The unit suite also covers source/return ownership, seven allocation-failure
budgets including live payload bytes, zero managed allocation after warmup,
classification directly on a wrapped ellipse edge and continued modeling after
two XT reloads. This changes no C ABI and is verified without NativeAOT.

Validation on this increment: the full kernel suite passes all 1,430 tests;
all 18 independent Parasolid oracle cases pass. The 16 new unit cases and the
existing almost-coaxial rejection regression also pass in a focused run. The
allocation IL guard checks 2,275 methods and reports CLEAN.

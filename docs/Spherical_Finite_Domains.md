# Certified finite spherical domains

Finite Sphere faces use the same arrangement, material classifier and B-rep
builder as regular curved faces. Their domain comes from their actual loops.
A local gnomonic chart is admitted only after rational Bezier control-hull
interval bounds prove every point of every boundary edge lies strictly in its
positive open hemisphere. Candidate evaluations choose the frame; they never
decide membership or certify a boundary.

Winding intersects each complete finite boundary curve with an exact spatial
plane and counts oriented crossings in this certified chart. Halfedge angles
use a common physical tangent frame at each shared vertex. Native longitude
seams and poles introduce no artificial topology and do not define the local
chart. Positive outer loops and negative hole loops are checked before reading
a finite Sphere face. Unproved hemispheres, complements and unsupported
boundary curves fail explicitly. A full untrimmed sphere continues through its
existing specialized paths.

The common reader, exact analytic ray classification, on-face classification,
SSI extent and sheet imprint arrangement consume this domain. Sphere support
geometry and analytic circular boundaries remain exact. This increment is a
finite-domain building block for spherical fillet corners; it does not claim
general singular SSI or global spherical region arrangements.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release --filter FullyQualifiedName~SphericalFaceDomainTests
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache -c Release scripts/SphericalFaceDomainOracle.cs -p:UseSharedCompilation=false -p:BuildInParallel=false
```

Manufactured octant tests vary the native frame and loop sense, including a
pole in the patch interior. Public imprint tests create a Sphere sheet, replace
its support with an equivalent pole-containing parameterization, cut it through
the same API and reload fresh XT. The oracle independently constructs both
kernel inputs, checks the fresh self XT with real PK, runs complete BODY_check
on both results and requires topology, region/shell, type, sense, tolerance,
missing-entity and mass-property agreement.

Rational quadratic BCurve boundaries now use a position/derivative-sized
evaluation buffer when selecting the local frame. Positive and reversed exact
octant loops exercise these boundaries directly. The independent public API
oracle also replaces each circular sheet edge by its exact rational quadratic
arc before imprinting, aligning its parameter direction with the edge rather
than inheriting the source circle's sense.

Checkpoint verification: 11 spherical-domain unit cases pass. All four
independent Parasolid imprint cases pass the complete structural checks:
circular and rational BCurve boundaries, each in its native and pole-containing
parameterization. The original 16 mixed analytic Boolean regressions were also
checked at the finite-domain introduction checkpoint.

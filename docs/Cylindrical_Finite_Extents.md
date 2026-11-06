# Certified finite cylinder face extents

`CertifiedCylindricalFaceExtent` bounds a finite Cylinder face from every
complete boundary edge. It projects positive-weight rational Bezier control
hulls onto the cylinder's axial dual. The axial coordinate is affine on the
intrinsic cylinder chart and therefore has no interior extrema on a bounded
face. All area loops and vertex loops contribute their full boundary ranges;
vertices alone never decide the result.

The Cartesian result combines that certified axial interval with the full
angular cross-sectional disk. The axial dual and radial amplitudes are formed
from the stored frame with outward interval arithmetic, so rounded basis data
need not be exactly unit or orthogonal. Source edge/vertex/face tolerances and
the accurate modeling precision expand the result conservatively.

Line, Circle, Ellipse and positive-weight BCurve spans are supported, including
TRCurve parameter maps, partial arcs and seam-crossing intervals. An unsupported
ICurve/SPCurve or a nonpositive/unbounded rational piece returns an explicit
failure; no partial box is published as success. This is a shared internal
extent for finite-obstacle modeling, not a replacement for the public PK
bounding-box API or an angular trimming solver.

`tests/KernelTests/CylindricalFaceExtentTests.cs` passes five cases: full-cylinder
ring boundaries without vertices in two axis directions, a partial periodic
seam interval, a rational boundary whose interior axial maximum exceeds all
vertices, and an explicit procedural-boundary rejection. Warm reads allocate
no managed memory.

`scripts/CylindricalFaceExtentOracle.cs` independently constructs both kernels'
full, oblique, seam-crossing and rational-bulged finite cylinders. Four cases
pass fresh text XT reception, full PK BODY_check and structural/type/sense/
tolerance/region-shell/missing comparisons. Native boundary evaluation checks
the certified box; native box queries are diagnostic because their permitted
conservatism is independent of this proof. The mathematical certificate comes
from the complete interval boundary projection, not those sampled checks.

The shared caller can dispatch Cylinder faces in `RegularFiniteFaceExtent` to
this helper. There are no C ABI changes and validation uses managed Release
builds and real PK without NativeAOT.

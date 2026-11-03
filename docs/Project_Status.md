# Project Status

Updated: 2026-11-03

## Current State

The project is an independent .NET 10 geometric kernel with NativeAOT C exports. It has working geometry evaluation, a pool-backed B-rep runtime, public profile modeling, XT interchange for an explicit geometry/topology subset, and a separate real-Parasolid display bridge. It is not a complete Parasolid replacement.

The current A/B stage and its acceptance evidence are documented in `docs/Public_Modeling_Stages_AB.md`. Generated `PK_*` stubs continue to return `PK_ERROR_not_implemented`; the presence of an export is not a capability claim.

| Area | Implemented scope | Reference |
| --- | --- | --- |
| Runtime | Session and partition management, typed entity pools, generations, return memory, command dispatch, journaling and rollback for covered operations | `docs/memory_architecture.md`, `docs/API_Dispatch_Model.md` |
| Basic geometry | Analytic curves/surfaces, rational and polynomial B-curves/B-surfaces, dependent TR/SP/offset/swept/spun geometry, original-parameter evaluation | `docs/geometry_evaluation.md`, `docs/bcurve_evaluation.md` |
| Intersection curves | The explicitly documented analytic and parametric support subsets, original chart evaluation and validation | `docs/icurve_evaluation_capabilities.md` |
| Public management | Create/ask/domain APIs, independent deep copies, similarity transforms including reflections, part geometry ownership and querying | `docs/Geometry_Public_API.md` |
| B-rep and import | Primitive solids, wires, sheets, holes, closed-edge and pole-loop topology; actual source geometry from supported XT graphs | `docs/xt_brep_materialization.md` |
| Profile modeling | Bounded wires and planar faces, polygonal holes, distance-bounded straight/oblique extrusion, signed partial/full revolution | `docs/Profile_Modeling.md` |
| Interchange | Fresh runtime-derived XT output, preserved opaque documents for unsupported import graphs, schema and corpus tooling | `docs/project_gm_kernel_xt_library.md`, `docs/xt_all_versions_support_status.md` |
| Visualization | Real PK faceting and line rendering, PKToy viewer buffers, exact mapping to independent face/edge identifiers, native and managed integration | `docs/Display_Bridge.md` |

The independent kernel creates and modifies its own geometry and topology. Real Parasolid is used for independent verification and display discretization. It is never substituted for the independent kernel's extrusion or revolution implementation.

## Verification

The A/B acceptance command is:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyPublicModeling.cs
```

It runs kernel and XT tests, independent real-PK geometry/transform/import/modeling oracles, import/copy round trips of both kernels' modeled outputs, the display bridge, NativeAOT publication, ABI smoke, and C-export modeling/display checks. Per-step logs and the actual exit status are recorded under `temp_docs/public-modeling-verification`.

The 2026-11-03 unified run passed all 14 steps with exit code 0: 910 kernel tests, 12 XT tests, 83 transform cases, 33 independent modeling cases, 79 import cases, public geometry contracts, the managed display bridge, NativeAOT publication, ABI smoke, and the native modeling/display/picking pipeline. Persistent evidence and the exact scope are in `docs/reviews/public-modeling/stages-ab-validation-2026-11-03.txt` and `docs/Public_Modeling_Stages_AB.md`.

The broader project verification entry point remains:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyKernel.cs
```

That command includes additional schema/corpus and allocation checks. Passing A/B acceptance does not by itself claim that every broader project gate or all generated API stubs work. In particular, the separately documented raw parametric ICurve D2 convention difference remains visible in its own oracle report.

For local unit tests, preserve the test exit status when cleaning up worker processes:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -m:1 -p:UseSharedCompilation=false
kernel_test_exit=$?
pkill -f '[t]esthost.dll' 2>/dev/null || true
exit "$kernel_test_exit"
```

The current real-SDK acceptance environment is Linux x64 with .NET 10.0.403 and Parasolid 38.0.150. Use the appropriate RID and NativeAOT toolchain on another platform. The private SDK and credentials are not stored in the repository.

## Remaining work

1. **C — intersection to Boolean operations:** complete branch discovery, paired surface parameters, imprint, split, trim, classification and result topology, starting with a bounded analytic-solid scope.
2. **D — general freeform modeling:** broader NURBS intersection/Boolean support, curve/surface construction and fitting, sweeps/lofts and complex sewing.
3. **E — local operations and compatibility:** fillets, chamfers, offsets/shelling, complex repair and nonmanifold models.
4. **Broaden A/B subsets:** general affine deformation, advanced copy options, arbitrary planar curve arrangements, face/body-bounded extrusion, axis-touching or axis-crossing revolution, and additional XT topology/geometry classes.

Unsupported cases return explicit errors or remain opaque at the XT boundary. Opaque preservation is not editable B-rep support, and a structural checker is not a full geometric validity certificate.

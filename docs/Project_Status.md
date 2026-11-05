# Project Status

Updated: 2026-11-05

## Current State

The project is an independent .NET 10 geometric kernel with NativeAOT C exports. It has working geometry evaluation, a pool-backed B-rep runtime, public profile modeling, XT interchange for an explicit geometry/topology subset, and a separate real-Parasolid display bridge. It is not a complete Parasolid replacement.

The A/B baseline and its acceptance evidence are documented in `docs/Public_Modeling_Stages_AB.md`. The new bounded C/D/E modeling scope, verification entry point and remaining general algorithms are documented in `docs/Modeling_Stages_CDE.md`. Generated `PK_*` stubs continue to return `PK_ERROR_not_implemented`; the presence of an export is not a capability claim.

| Area | Implemented scope | Reference |
| --- | --- | --- |
| Runtime | Session and partition management, typed entity pools, generations, return memory, command dispatch, journaling and rollback for covered operations | `docs/memory_architecture.md`, `docs/API_Dispatch_Model.md` |
| Basic geometry | Analytic curves/surfaces, rational and polynomial B-curves/B-surfaces, dependent TR/SP/offset/swept/spun geometry, original-parameter evaluation | `docs/geometry_evaluation.md`, `docs/bcurve_evaluation.md` |
| Intersection curves | The explicitly documented analytic and parametric support subsets, original chart evaluation and validation | `docs/icurve_evaluation_capabilities.md` |
| Public management | Create/ask/domain APIs, independent deep copies, similarity transforms including reflections, part geometry ownership and querying | `docs/Geometry_Public_API.md` |
| B-rep and import | Primitive solids, wires, sheets, holes, closed-edge and pole-loop topology; actual source geometry from supported XT graphs | `docs/xt_brep_materialization.md` |
| Profile modeling | Bounded wires and planar faces, polygonal holes, distance-bounded straight/oblique extrusion, signed partial/full revolution | `docs/Profile_Modeling.md` |
| Intersection operations | Exact analytic paths plus bounded regular NURBS/NURBS and analytic/NURBS discovery, closed branches, paired UV, boundary intersections and inverse parameters | `docs/Intersection_Operations.md`, `docs/Curved_Surface_Pipeline.md` |
| Pure geometry extension | Geometry-bounded analytic SSI, certified convex plane contacts, global supported minimum-distance/projection queries, exact cylindrical/toroidal blends and seven spline edits | `docs/Geometry_Completion_Plan.md` |
| Boolean and editing | Planar solid Boolean, complete-sphere scope; NURBS sheet imprint, closed-loop/holed trimming, repeated plane cuts and connected front/back section | `docs/Boolean_Operations.md`, `docs/Curved_Surface_Pipeline.md`, `docs/Analytic_Boolean_Operations.md` |
| Freeform construction | Interpolation/fitting, exact curve matching, clamped spline-grid surfaces with corner twists, rational sweep/spin surfaces, certified parallel-profile solid sweeps/lofts and complete-edge sewing | `docs/Freeform_Modeling.md` |
| Local operations | Convex/analytic offsets and hollows, independent or uniform-all-edge convex fillets/chamfers, cylinder rim operations | `docs/Local_Operations.md` |
| Interchange | Fresh runtime-derived XT output, preserved opaque documents for unsupported import graphs, schema and corpus tooling | `docs/project_gm_kernel_xt_library.md`, `docs/xt_all_versions_support_status.md` |
| Visualization | Real PK faceting and line rendering, PKToy viewer buffers, exact mapping to independent face/edge identifiers, native and managed integration | `docs/Display_Bridge.md` |

The independent kernel creates and modifies its own geometry and topology. Real Parasolid is used for independent verification and display discretization, including the new modeling operations' independent oracle paths.

## Verification

The combined advanced modeling and existing A/B acceptance command is:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyAdvancedModeling.cs
```

It checks the new independent intersection, planar/analytic Boolean, tracking, section, freeform and local-operation oracles, then the complete A/B suite and additional NativeAOT C-export modeling. Its actual exit statuses are recorded in `temp_docs/advanced-modeling-verification`; persistent evidence belongs in `docs/reviews/advanced-modeling`.

The 2026-11-05 general-surface run passed all 14 top-level gates and all 14 nested gates, including 59 independent intersection contracts, 70 curved-sheet fixtures, XT tests, NativeAOT and native smoke checks. The final full kernel run passed 1,250 tests. The implemented descriptor count is 163; the Release allocation IL guard checked 1,935 methods and was clean. Scope, comparison qualifications and final-run details are recorded in `docs/reviews/advanced-modeling/general-surface-validation-2026-11-05.md`.

The subsequent pure-geometry increments passed all 14 top-level and 14 nested
gates, with 87 intersection/distance/blend/curve-editing contracts, 22 freeform
construction scenarios and the same 70 curved-sheet fixtures. The final full
kernel rerun passed 1,306 tests; XT tests passed 12. NativeAOT and native smoke
checks passed with 173 implemented descriptors and a clean Release allocation
guard covering 1,994 methods. This run used SDK 10.0.302. Supported subsets and
remaining geometry boundaries are recorded in `docs/Geometry_Completion_Plan.md`
and `docs/reviews/advanced-modeling/pure-geometry-increment-2026-11-05.md`.

The final 2026-11-03 advanced run passed all 10 top-level gates and all 14 nested A/B gates: 1107 kernel tests, 12 XT tests, 224 cases across the seven main independent oracle matrices, four additional tolerant-geometry lifecycle cases, two additional loft rejection-contract cases, and the NativeAOT modeling/display checks. The implemented descriptor count is 161, up from the A/B baseline's 124. The production managed-allocation IL guard checked 1789 methods and was clean. See `docs/reviews/advanced-modeling/validation-2026-11-03.txt` for the exact environment, optional in-process MSBuild task configuration, raw comparison qualifications and actual exit statuses.

The A/B acceptance command is:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyPublicModeling.cs
```

It runs kernel and XT tests, independent real-PK geometry/transform/import/modeling oracles, import/copy round trips of both kernels' modeled outputs, the display bridge, NativeAOT publication, ABI smoke, and C-export modeling/display checks. Per-step logs and the actual exit status are recorded under `temp_docs/public-modeling-verification`.

The earlier A/B baseline run on 2026-11-03 passed all 14 steps with exit code 0: 910 kernel tests, 12 XT tests, 83 transform cases, 33 independent modeling cases, 79 import cases, public geometry contracts, the managed display bridge, NativeAOT publication, ABI smoke, and the native modeling/display/picking pipeline. Persistent evidence and the exact scope are in `docs/reviews/public-modeling/stages-ab-validation-2026-11-03.txt` and `docs/Public_Modeling_Stages_AB.md`.

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

1. **C — general curved topology:** arbitrary curved branch/face arrangements, singular contacts, repeated mixed-surface Boolean operations, and broader analytic pairs beyond the implemented planar and complete-sphere paths.
2. **D — general freeform modeling:** singular/coincident NURBS intersection, curved-solid Boolean construction, moving-frame/twisted/guided solid sweeps, arbitrary section changes, partial-edge sewing and complex repairs. Regular bounded NURBS intersection and sheet arrangements are implemented; they do not imply these general capabilities.
3. **E — general local operations and compatibility:** freeform/variable-radius and concave blends, arbitrary corner groups, topology-changing/general NURBS offset and hollow operations, complex repair and nonmanifold models.
4. **Broaden A/B subsets:** general affine deformation, advanced copy options, arbitrary planar curve arrangements, face/body-bounded extrusion, axis-touching or axis-crossing revolution, and additional XT topology/geometry classes.

Unsupported cases return explicit errors or remain opaque at the XT boundary. Opaque preservation is not editable B-rep support, and a structural checker is not a full geometric validity certificate.

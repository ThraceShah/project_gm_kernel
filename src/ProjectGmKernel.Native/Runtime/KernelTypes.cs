// ── Kernel-Internal Type Aliases ──────────────────────────────────
// These aliases provide type safety for the many int-based slot indices
// and tag handles used in kernel records. They prevent accidental misuse
// (e.g., passing a FaceSlot where a BodySlot is expected).
//
// Convention:
//   *Slot       = index into an entity pool array (pool-internal)
//   *Tag        = external entity handle (PK_*_t), already defined in generated code
//   Kernel*     = kernel-internal enum values mirroring PK_*_t semantics
//
// PK_*_t aliases are ABI-level and only used in the API layer (KernelExports, Generated).
// Kernel records use Kernel* aliases to maintain separation from the ABI surface.

// Pool slot indices — used in adjacency fields of topology/geometry records.
global using BodySlot = int;
global using PointSlot = int;
global using VectorSlot = int;
global using ShellSlot = int;
global using FaceUseSlot = int;
global using FaceSlot = int;
global using LoopSlot = int;
global using EdgeSlot = int;
global using FinSlot = int;
global using VertexSlot = int;
global using RegionSlot = int;
global using CurveSlot = int;
global using SurfaceSlot = int;
global using TransformSlot = int;
global using DataSlot = int;
global using PartitionSlot = int;

// Tag handles — external entity references (PK_*_t are already defined in generated code).
// These aliases are for kernel code that needs to explicitly distinguish tags from slots.
global using PointTag = int;
global using CurveTag = int;
global using SurfTag = int;
global using GeomTag = int;
global using TransfTag = int;
global using EntityTag = int;

// Kernel-internal enum aliases — mirror PK_*_t values but decouple from ABI surface.
// Values match PK_*_t for zero-cost conversion at API boundary.
global using KernelBodyType = int;       // PK_BODY_type_t
global using KernelBodyConfig = int;     // PK_BODY_config_t
global using KernelShellType = int;      // PK_SHELL_type_t
global using KernelLoopType = int;       // PK_LOOP_type_t
global using KernelFinType = int;        // PK_FIN_type_t
global using KernelEdgeType = int;       // PK_EDGE_vertex_type_t
global using KernelEdgeConvexity = int;  // PK_EDGE_convexity_t
global using KernelVertexType = int;     // PK_VERTEX_type_t
global using KernelSense = int;          // PK_TOPOL_sense_t
global using KernelBCurveForm = int;     // PK_BCURVE_form_t
global using KernelBSurfaceForm = int;   // PK_BSURF_form_t
global using KernelKnotType = int;       // PK_knot_type_t
global using KernelSelfIntersect = int;  // PK_self_intersect_t
global using KernelConvexity = int;      // PK_convexity_t
global using KernelLogical = byte;       // PK_LOGICAL_t

// XT schema metadata aliases.
global using XtNodeType = int;
global using XtFieldCount = int;
global using XtFieldIndex = int;
global using XtFieldElementCount = int;
global using XtNodeClass = int;
global using XtNodeIndex = int;

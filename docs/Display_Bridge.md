# Parasolid display bridge

The independent kernel owns model geometry and topology. `ProjectGmKernel.DisplayBridge` takes an XT snapshot, asks a separately initialized real Parasolid session for display data, and maps every displayed face or edge back to the independent kernel's entity tag. No Parasolid modeling result is imported as an implementation of our modeling operations.

## Data flow

| Step | Owner | Result |
| --- | --- | --- |
| Create, copy, transform, extrude or revolve | Independent kernel | Editable pool-backed geometry and B-rep |
| Capture display snapshot | Independent kernel | Fresh XT plus an exact persistent-identifier map |
| Receive XT and resolve identifiers | Real Parasolid | Display-only PK entities, mapped by `PK_PART_find_entity_by_ident` |
| Facet faces | Real Parasolid | Triangles, normals and source PK face associations |
| Approximate curved edge paths | Real Parasolid | Display polylines produced by the PK line renderer |
| Submit viewer buffers | Display bridge / PKToy viewer | `StripFacePart` and `EdgePart`, keyed by independent-kernel tags |
| Resolve a picked cell, PK tag or ray | Display bridge | Original body tag, original face/edge tag, entity kind |

The map comes from the same `XtWriter.NodeMap` that assigns the exported XT identifiers. It never relies on entity enumeration order, matching coordinates, or nearest-face guesses. Ray selection operates on the PK-produced display data; its result is resolved through that exact map.

## Native application integration

An application using the kernel's NativeAOT C exports should use:

```csharp
using ProjectGmKernel.DisplayBridge;

// bodyTag was created using this native library's PK_* exports.
// The application has already initialized the real PK session separately.
using var display = ParasolidDisplayBridge.CreateFromNativeLibrary(
    kernelLibraryPath, bodyTag, chordTolerance: 0.001);

// These are the existing PKToy Viewer.IContract geometry types.
var faceGeometry = display.Faces;
var edgeGeometry = display.Edges;

// After the viewer uploads the geometry with its usual Update(buffer) call:
if (display.TryResolveViewerCell(DisplayEntityKind.Face, pickedCell, out var source))
{
    // Pass source.EntityTag to the independent kernel's face query/edit APIs.
    Console.WriteLine($"Body {source.BodyTag}, face {source.EntityTag}");
}
```

`kernelLibraryPath` identifies the independent kernel, not the real SDK library. `PskernelSharp` continues to resolve `pskernel` to real Parasolid. The bridge retains its own native-library handle while a display snapshot can validate picks, then releases it on `Dispose`.

Supply the kernel's schema directory through `PARASOLID_SCHEMA_DIR` or `P_SCHEMA` in the application's startup environment before starting the native kernel session. CoreCLR and a separately loaded NativeAOT library have separate environment caches; changing only the managed cache in an already-running host does not configure the native kernel. The verification entry points arrange the startup environment automatically.

In an in-process managed kernel host, `ParasolidDisplayBridge.Create(bodyTag, chordTolerance)` uses that managed kernel session directly. These two entry points deliberately distinguish a managed session from a body created in a separate NativeAOT library.

## C display snapshot extension

The header is `src/ProjectGmKernel.DisplayBridge/project_gm_display.h`:

| Export | Contract |
| --- | --- |
| `PGM_BODY_export_display` | Capture fresh text XT, all face/edge/vertex bindings, and session/body generation stamps under one read claim |
| `PGM_DISPLAY_validate_pick` | Validate that the originating session, body and entity still exist with the captured generations and public entity class |
| `PGM_DISPLAY_snapshot_free` | Release both returned allocations and zero the structure; freeing an already zeroed structure succeeds |

Each binding contains the independent entity tag, public PK class, XT persistent identifier and entity generation. Identifiers are scoped to the captured part. The result uses the kernel's return allocator and must be freed by the matching kernel instance. On allocation failure no partial snapshot is published.

The bridge rejects opaque bodies because they have no editable independent face/edge identities to expose. See `docs/xt_brep_materialization.md` for the materialized import scope.

## Ownership and refresh

`DisplayBody` owns its received real-PK part and all unmanaged viewer buffers. If the real PK host has rollback/partition support active, the part lives in a private display partition; otherwise the bridge owns and deletes the imported part in the ordinary session. The host's current partition and graphics callbacks are restored after snapshot creation.

Dispose display objects before shutting down the real PK session. Disposing a display object does not delete or mutate the independent model. The bridge serializes its own real-PK calls; the host should serialize other calls into the same PK session, including graphics callback registration.

A display object is a geometry snapshot. After a successful model edit, build and upload a fresh display object, then dispose the previous one. Generation checks reject picks after the source entity or body is deleted, or after the independent kernel session is restarted. A still-live entity can retain its identity across edits; its old rendering is still a snapshot and must be refreshed.

## Rendering details

Face data comes from `PK_TOPOL_facet` with triangular facets, vertex normals and face associations. Exact finite line endpoints come from `PK_EDGE_ask_geometry`. Other edges use a temporary real-PK B-curve proxy followed by `PK_GEOM_render_line`; the proxy is deleted immediately and is never part of our kernel's model.

The GO callback layout depends on the line type: `L3TPPY` counts XYZ points, whereas analytic line descriptions carry scalar parameters. `ProbeDisplayLineContracts.cs` verifies the complete closed circle, its length and chord midpoint error. Reading a polyline's count as a number of doubles would truncate the path to one third; the production bridge reads all `3 * ngeom` coordinates.

Viewer positions use `System.Numerics.Vector3` floats, as required by PKToy's viewer contract. Non-finite values or coordinates outside that representation are rejected. Model geometry and the XT snapshot retain double precision.

## Reproducible acceptance

The real SDK is required for these commands:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyPublicModeling.cs
```

The display-only gate is:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/DisplayBridgeOracle.cs
# After publishing the native library, also exercise the actual C exports:
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/DisplayBridgeOracle.cs -- --native
```

The oracle covers blocks, cylinders, tori, holed sheets, holed oblique extrusions, closed circle wires, rational curve wires, copied/transformed bodies, every viewer cell mapping, every meshed face's ray pick, wire edge picks, deletion, session restart, and native snapshot allocation/free. The `--native` gate calls the published C exports to build a holed sheet, extrude it obliquely, free tracking results, deep-copy the solid, delete the source, transform the copy, and revolve the remaining profile through a full turn. Both native modeling results must pass real PK's full `BODY_check` and display/pick validation. It checks full-circle coverage and requested chord tolerance in addition to merely obtaining line data. Artifacts and logs are written under `temp_docs/display-bridge-oracle/`.

`DisplaySnapshotTests` independently covers return-buffer ownership, failure rollback, generation/class checks, and delete/undo identity behavior without requiring the private SDK.

The final unified acceptance run on 2026-11-03 passed both managed and native gates against Parasolid 38.0.150 on Linux x64. The native block had 26 mapped entities and 12 triangles; the copied/transformed holed extrusion had 50 mapped entities and 32 triangles; the holed full revolution had 32 mapped entities and 556 triangles. All display triangles and curved-edge polylines came from real PK. Both modeled solids passed the default real `PK_BODY_check`, and face/edge picks resolved to their original independent-kernel identities. See `docs/reviews/public-modeling/stages-ab-validation-2026-11-03.txt` for the persistent acceptance record.

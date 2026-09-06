using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private enum CreatePrimitiveKind
    {
        Sphere,
        Torus,
    }

    internal static bool IsSessionStarted => EnsureSession();

    internal static bool TryResolveBodySlot(EntityTag bodyTag, out BodySlot bodySlot)
    {
        bodySlot = -1;
        if (!IsValidTag(bodyTag) || (EntityClass)TagRec(bodyTag).ClassCode != EntityClass.Body)
            return false;

        bodySlot = TagRec(bodyTag).Slot;
        return true;
    }

    internal static bool TryGetReceivedXt(EntityTag partTag, out XtDocument document, out XtNodeIndex rootIndex)
        => TryGetReceivedXt(partTag, out document, out rootIndex, out _);

    // ── XT association side table (managed adapter storage) ─────
    // XT documents stay in the managed adapter layer; the kernel only keeps
    // the association tag↔document for transmit/receive round-trips.
    internal static XtAssociationTable Xt = new();

    internal static bool TryGetReceivedXt(EntityTag partTag, out XtDocument document, out XtNodeIndex rootIndex, out ProjectGmKernel.Xt.IXtSchemaModel? model)
    {
        document = null!;
        rootIndex = 0;
        model = null;
        if (!IsValidTag(partTag) || (PoolKind)TagRec(partTag).Pool != PoolKind.Body)
            return false;
        var slot = TagRec(partTag).Slot;
        var receivedDocument = Xt.GetDocument(slot);
        if (receivedDocument is null)
            return false;
        document = receivedDocument;
        rootIndex = Xt.GetRootIndex(slot);
        model = Xt.GetModel(slot);
        return true;
    }

    internal static void AttachReceivedXt(EntityTag partTag, XtDocument document, ProjectGmKernel.Xt.IXtSchemaModel? model, XtNodeIndex rootIndex, bool opaque)
    {
        if (!IsValidTag(partTag) || (PoolKind)TagRec(partTag).Pool != PoolKind.Body)
            throw new InvalidOperationException("Cannot attach XT data to a non-part entity.");
        var slot = TagRec(partTag).Slot;
        Xt.Attach(slot, document, model, rootIndex, opaque);
    }

    internal static int CreateOpaquePartCore(XtDocument document, ProjectGmKernel.Xt.IXtSchemaModel? model, XtNode root, out EntityTag partTag)
    {
        partTag = 0;
        EntityClass entityClass = root.Type switch
        {
            10 => EntityClass.Assembly,
            12 => EntityClass.Body,
            _ => (EntityClass)0,
        };
        if (entityClass == 0)
            return ParasolidConstants.PK_ERROR_bad_file_format;

        var bodySlot = TryAllocateBodies();
        if (bodySlot < 0) return -1;
        ref var body = ref Bodies[bodySlot];
        InitializeBody(ref body);
        AssignPartition(ref body.Header, CurrentPartition);
        var tag = AllocateTag(entityClass, PoolKind.Body, bodySlot, body.Header.Generation);
        if (tag < 0)
        {
            Bodies.Free(bodySlot);
            return ParasolidConstants.PK_ERROR_general_body;
        }
        if (entityClass == EntityClass.Body)
            AppendBodyToPartition(CurrentPartition, bodySlot);
        partTag = tag;
        AttachReceivedXt(tag, document, model, root.Index, opaque: true);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    internal static BodyRecord GetBodyRecord(BodySlot bodySlot) => Bodies[bodySlot];
    internal static RegionRecord GetRegionRecord(RegionSlot regionSlot) => Regions[regionSlot];
    internal static ShellRecord GetShellRecord(ShellSlot shellSlot) => Shells[shellSlot];
    internal static FaceUseRecord GetFaceUseRecord(FaceUseSlot faceUseSlot) => FaceUses[faceUseSlot];
    internal static FaceRecord GetFaceRecord(FaceSlot faceSlot) => Faces[faceSlot];
    internal static LoopRecord GetLoopRecord(LoopSlot loopSlot) => Loops[loopSlot];
    internal static FinRecord GetFinRecord(FinSlot finSlot) => Fins[finSlot];
    internal static EdgeRecord GetEdgeRecord(EdgeSlot edgeSlot) => Edges[edgeSlot];
    internal static VertexRecord GetVertexRecord(VertexSlot vertexSlot) => Vertices[vertexSlot];

    internal static SurfaceRecord GetSurfaceByTag(SurfTag surfaceTag)
    {
        var slot = GetSurfaceSlotByTag(surfaceTag);
        return slot >= 0 ? Surfaces[slot] : default;
    }

    internal static SurfaceSlot GetSurfaceSlotByTag(SurfTag surfaceTag)
    {
        return surfaceTag > 0 && State.Session != null && State.Session->Tags.TryResolve(surfaceTag, CurrentSessionId, out var record)
            && (EntityClass)record.ClassCode == EntityClass.Surface ? record.Slot
            : -1;
    }

    internal static PointRecord GetPointByTag(PointTag pointTag)
    {
        var slot = GetPointSlotByTag(pointTag);
        return slot >= 0 ? Points[slot] : default;
    }

    internal static PointSlot GetPointSlotByTag(PointTag pointTag)
    {
        return pointTag > 0 && State.Session != null && State.Session->Tags.TryResolve(pointTag, CurrentSessionId, out var record)
            && (EntityClass)record.ClassCode == EntityClass.Point ? record.Slot
            : -1;
    }

    internal static CurveRecord GetCurveByTag(CurveTag curveTag)
    {
        var slot = GetCurveSlotByTag(curveTag);
        return slot >= 0 ? Curves[slot] : default;
    }

    internal static CurveSlot GetCurveSlotByTag(CurveTag curveTag)
    {
        return curveTag > 0 && State.Session != null && State.Session->Tags.TryResolve(curveTag, CurrentSessionId, out var record)
            && (EntityClass)record.ClassCode == EntityClass.Curve ? record.Slot
            : -1;
    }

    internal static CylinderData GetCylinderData(DataSlot dataSlot) => CylinderDataPool[dataSlot];
    internal static PlaneData GetPlaneData(DataSlot dataSlot) => PlaneDataPool[dataSlot];
    internal static CircleData GetCircleData(DataSlot dataSlot) => CircleDataPool[dataSlot];
    internal static LineData GetLineData(DataSlot dataSlot) => LineDataPool[dataSlot];
    internal static ConeData GetConeData(DataSlot dataSlot) => ConeDataPool[dataSlot];
    internal static SphereData GetSphereData(DataSlot dataSlot) => SphereDataPool[dataSlot];
    internal static TorusData GetTorusData(DataSlot dataSlot) => TorusDataPool[dataSlot];

    // Pool index constants for mark/rollback
    private const int PoolHandles = 0;
    private const int PoolPoints = 1;
    private const int PoolVectors = 2;
    private const int PoolBodies = 3;
    private const int PoolShells = 4;
    private const int PoolFaceUses = 5;
    private const int PoolFaces = 6;
    private const int PoolLoops = 7;
    private const int PoolEdges = 8;
    private const int PoolFins = 9;
    private const int PoolVertices = 10;
    private const int PoolRegions = 11;
    private const int PoolCurves = 12;
    private const int PoolSurfaces = 13;
    private const int PoolTransforms = 14;
    private const int PoolCircleData = 15;
    private const int PoolCylinderData = 16;
    private const int PoolPlaneData = 17;
    private const int PoolLineData = 18;
    private const int PoolConeData = 19;
    private const int PoolSphereData = 20;
    private const int PoolTorusData = 21;
    private const int PoolBCurveData = 22;
    private const int PoolBCurveVertices = 23;
    private const int PoolBCurveKnots = 24;
    private const int PoolBCurveKnotMults = 25;
    private const int PoolBCurveExpandedKnots = 26;

    // ── Tag plumbing (new infrastructure) ───────────────────────

    private static TagMap.TagRecord TagRec(EntityTag tag)
    {
        State.Session->Tags.TryResolve(tag, CurrentSessionId, out var record);
        return record;
    }

    /// <summary>Tag for an existing slot: reads the tag cached in the record header.</summary>
    private static int GetOrAllocateTag(EntityClass entityClass, PoolKind pool, int slotIndex)
        => TagOf(pool, slotIndex);


    // ── Session lifecycle ────────────────────────────────────────

    private static int SessionStartImplementation(PK_SESSION_start_o_s* options) => SessionStartCore(options);


    private static int SessionStopImplementation() => SessionStopCore();


    // ── PK_POINT_create ──────────────────────────────────────────

    private static int PointCreateImplementation(PK_POINT_sf_s* pointSf, int* pointTag)
    {
        if (pointTag != null) *pointTag = 0;
        if (pointSf is null || pointTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        int slot;
        if (!Points.TryAllocate(out slot)) return ParasolidConstants.PK_ERROR_memory_full;
        ref var rec = ref Points[slot];
        // Zero-cost reinterpret: PK_VECTOR_s and KernelVector3 have identical layout
        rec.Position = Unsafe.As<PK_VECTOR_s, KernelVector3>(ref pointSf->position);

        var tag = AllocateTag(EntityClass.Point, PoolKind.Point, slot, rec.Header.Generation);
        if (tag < 0)
        {
            Points.Free(slot);
            return ParasolidConstants.PK_ERROR_general_body;
        }

        *pointTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── PK_ENTITY_ask_class ──────────────────────────────────────

    private static int EntityAskClassImplementation(int entityTag, int* classCode)
    {
        if (classCode is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        if (!IsValidTag(entityTag))
            return ParasolidConstants.PK_ERROR_unknown_class;

        *classCode = ToPkClass((EntityClass)TagRec(entityTag).ClassCode);
        if ((EntityClass)(EntityClass)TagRec(entityTag).ClassCode == EntityClass.Curve && Curves[TagRec(entityTag).Slot].Class != CurveClass.None)
            *classCode = (int)Curves[TagRec(entityTag).Slot].Class;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── PK_BODY_create_topology_2 ────────────────────────────────

    private static int BodyCreateTopology2Implementation(
        int nTopols, PK_CLASS_t* classes,
        int nRelations, int* parents, int* children, int* senses,
        PK_BODY_create_topology_2_o_s* options,
        PK_BODY_create_topology_2_r_s* results)
    {
        if (classes is null || nTopols <= 0)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        // Allocate a body
        var bodySlot = TryAllocateBodies();
        if (bodySlot < 0) return -1;
        ref var body = ref Bodies[bodySlot];
        body.BodyType = ParasolidConstants.PK_BODY_type_general_c;
        AssignPartition(ref body.Header, CurrentPartition);
        body.FirstShell = -1;
        body.LastShell = -1;
        body.FirstRegion = -1;
        body.LastRegion = -1;
        body.FirstFaceBody = -1;
        body.LastFaceBody = -1;
        body.FirstEdgeBody = -1;
        body.LastEdgeBody = -1;
        body.FirstVertexBody = -1;
        body.LastVertexBody = -1;

        // Allocate topology entities and record their pool slots
        // We use stack-allocated arrays for the slot mapping (max 256 topologies)
        const int MaxTopols = 256;
        if (nTopols > MaxTopols)
            return ParasolidConstants.PK_ERROR_general_body;

        int* slots = stackalloc int[nTopols];
        byte* poolKinds = stackalloc byte[nTopols]; // PoolKind for each topology

        // First pass: allocate all topology entities
        for (int i = 0; i < nTopols; i++)
        {
            var cls = classes[i];
            switch (cls)
            {
                case ParasolidConstants.PK_CLASS_shell:
                    slots[i] = TryAllocateShells();
                    if (slots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;

                    AssignPartition(ref Shells[slots[i]].Header, CurrentPartition);
                    Shells[slots[i]].Body = -1;
                    Shells[slots[i]].Region = -1;
                    Shells[slots[i]].FirstFaceUseShell = -1;
                    Shells[slots[i]].LastFaceUseShell = -1;
                    Shells[slots[i]].AcornVertex = -1;
                    Shells[slots[i]].PrevInBody = -1;
                    Shells[slots[i]].NextInBody = -1;
                    Shells[slots[i]].PrevInRegion = -1;
                    Shells[slots[i]].NextInRegion = -1;
                    poolKinds[i] = (byte)PoolKind.Shell;
                    break;
                case ParasolidConstants.PK_CLASS_face:
                    slots[i] = TryAllocateFaces();
                    if (slots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;

                    AssignPartition(ref Faces[slots[i]].Header, CurrentPartition);
                    Faces[slots[i]].BackShell = -1;
                    Faces[slots[i]].FrontShell = -1;
                    Faces[slots[i]].BackFaceUse = -1;
                    Faces[slots[i]].FrontFaceUse = -1;
                    Faces[slots[i]].FirstLoop = -1;
                    Faces[slots[i]].LastLoop = -1;
                    Faces[slots[i]].Tolerance = 0;
                    Faces[slots[i]].PrevOnSurf = -1;
                    Faces[slots[i]].NextOnSurf = -1;
                    Faces[slots[i]].PrevInBody = -1;
                    Faces[slots[i]].NextInBody = -1;
                    poolKinds[i] = (byte)PoolKind.Face;
                    break;
                case ParasolidConstants.PK_CLASS_loop:
                    slots[i] = TryAllocateLoops();
                    if (slots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;

                    AssignPartition(ref Loops[slots[i]].Header, CurrentPartition);
                    Loops[slots[i]].Face = -1;
                    Loops[slots[i]].FirstFin = -1;
                    Loops[slots[i]].LastFin = -1;
                    Loops[slots[i]].PrevInFace = -1;
                    Loops[slots[i]].NextInFace = -1;
                    poolKinds[i] = (byte)PoolKind.Loop;
                    break;
                case ParasolidConstants.PK_CLASS_edge:
                    slots[i] = TryAllocateEdges();
                    if (slots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;

                    AssignPartition(ref Edges[slots[i]].Header, CurrentPartition);
                    Edges[slots[i]].Body = -1;
                    Edges[slots[i]].StartVertex = -1;
                    Edges[slots[i]].EndVertex = -1;
                    Edges[slots[i]].FirstFinEdge = -1;
                    Edges[slots[i]].LastFinEdge = -1;
                    Edges[slots[i]].Tolerance = 0;
                    Edges[slots[i]].PrevInBody = -1;
                    Edges[slots[i]].NextInBody = -1;
                    poolKinds[i] = (byte)PoolKind.Edge;
                    break;
                case ParasolidConstants.PK_CLASS_fin:
                    slots[i] = TryAllocateFins();
                    if (slots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;

                    AssignPartition(ref Fins[slots[i]].Header, CurrentPartition);
                    Fins[slots[i]].Edge = -1;
                    Fins[slots[i]].Loop = -1;
                    Fins[slots[i]].NextInLoop = -1;
                    Fins[slots[i]].PrevInLoop = -1;
                    Fins[slots[i]].Other = -1;
                    Fins[slots[i]].Curve = -1;
                    Fins[slots[i]].Vertex = -1;
                    Fins[slots[i]].NextAtVertex = -1;
                    Fins[slots[i]].PrevAtVertex = -1;
                    Fins[slots[i]].NextOfEdge = -1;
                    Fins[slots[i]].PrevOfEdge = -1;
                    Fins[slots[i]].Sense = '+';
                    poolKinds[i] = (byte)PoolKind.Fin;
                    break;
                case ParasolidConstants.PK_CLASS_vertex:
                    slots[i] = TryAllocateVertices();
                    if (slots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;

                    AssignPartition(ref Vertices[slots[i]].Header, CurrentPartition);
                    Vertices[slots[i]].Body = -1;
                    Vertices[slots[i]].Tolerance = 0;
                    Vertices[slots[i]].FirstFinVertex = -1;
                    Vertices[slots[i]].LastFinVertex = -1;
                    Vertices[slots[i]].PrevInBody = -1;
                    Vertices[slots[i]].NextInBody = -1;
                    poolKinds[i] = (byte)PoolKind.Vertex;
                    break;
                case ParasolidConstants.PK_CLASS_region:
                    slots[i] = TryAllocateRegions();
                    if (slots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;

                    AssignPartition(ref Regions[slots[i]].Header, CurrentPartition);
                    Regions[slots[i]].Body = -1;
                    Regions[slots[i]].IsSolid = 0;
                    Regions[slots[i]].FirstShell = -1;
                    Regions[slots[i]].LastShell = -1;
                    Regions[slots[i]].Frame = -1;
                    Regions[slots[i]].PrevInBody = -1;
                    Regions[slots[i]].NextInBody = -1;
                    poolKinds[i] = (byte)PoolKind.Region;
                    break;
                default:
                    return ParasolidConstants.PK_ERROR_bad_class;
            }
        }

        for (int i = 0; i < nTopols; i++)
            if (slots[i] < 0)
                return ParasolidConstants.PK_ERROR_memory_full;

        // Second pass: wire parent-child relationships
        for (int r = 0; r < nRelations; r++)
        {
            int pi = parents[r];   // parent topology index in the classes array (-1 = body)
            int ci = children[r];  // child topology index
            int sense = senses is not null ? senses[r] : 0;

            if (ci < 0 || ci >= nTopols)
                return ParasolidConstants.PK_ERROR_bad_field_number;
            if (pi < -1 || pi >= nTopols)
                return ParasolidConstants.PK_ERROR_bad_field_number;

            // Special case: pi == -1 means the body is the parent
            if (pi == -1)
            {
                var childPool = poolKinds[ci];
                var childSlot = slots[ci];
                if (!WireRelation((byte)PoolKind.Body, bodySlot, childPool, childSlot, sense, bodySlot))
                    return ParasolidConstants.PK_ERROR_memory_full;
            }
            else
            {
                var parentPool = poolKinds[pi];
                var childPool = poolKinds[ci];
                var parentSlot = slots[pi];
                var childSlot = slots[ci];
                if (!WireRelation(parentPool, parentSlot, childPool, childSlot, sense, bodySlot))
                    return ParasolidConstants.PK_ERROR_memory_full;
            }
        }

        // Assign body-level flat iteration (all faces/edges/vertices)
        AssignBodyFlatIteration(bodySlot, nTopols, classes, slots, poolKinds);
        AppendBodyToPartition(CurrentPartition, bodySlot);

        // Build result tags
        if (results is not null)
        {
            results->body = AllocateTag(EntityClass.Body, PoolKind.Body, bodySlot, body.Header.Generation);
        }
        if (!PublishBodyTopologyTags(bodySlot)) return ParasolidConstants.PK_ERROR_memory_full;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static bool WireRelation(
        byte parentPool, int parentSlot,
        byte childPool, int childSlot,
        int sense, int bodySlot)
    {
        switch ((PoolKind)parentPool)
        {
            case PoolKind.Body when childPool == (byte)PoolKind.Shell:
                {
                    ref var shell = ref Shells[childSlot];
                    shell.Body = parentSlot;
                    shell.Region = -1;
                    AppendShellToBody(parentSlot, childSlot);
                }
                break;

            case PoolKind.Body when childPool == (byte)PoolKind.Region:
                {
                    AppendRegionToBody(parentSlot, childSlot);
                }
                break;

            case PoolKind.Region when childPool == (byte)PoolKind.Shell:
                {
                    ref var region = ref Regions[parentSlot];
                    ref var shell = ref Shells[childSlot];
                    shell.Body = region.Body;
                    AppendShellToRegion(parentSlot, childSlot);
                }
                break;

            case PoolKind.Shell when childPool == (byte)PoolKind.Face:
                {
                    if (AddFaceUse(parentSlot, childSlot, sense) < 0) return false;
                }
                break;

            case PoolKind.Face when childPool == (byte)PoolKind.Loop:
                {
                    ref var face = ref Faces[parentSlot];
                    ref var loop = ref Loops[childSlot];
                    loop.Face = parentSlot;
                    AppendLoopToFace(parentSlot, childSlot);
                }
                break;

            case PoolKind.Loop when childPool == (byte)PoolKind.Fin:
                {
                    ref var loop = ref Loops[parentSlot];
                    ref var fin = ref Fins[childSlot];
                    fin.Loop = parentSlot;
                    AppendFinToLoop(parentSlot, childSlot);
                }
                break;

            case PoolKind.Edge when childPool == (byte)PoolKind.Fin:
                {
                    ref var fin = ref Fins[childSlot];
                    fin.Edge = parentSlot;
                    AppendFinToEdge(parentSlot, childSlot);
                }
                break;

            case PoolKind.Fin when childPool == (byte)PoolKind.Edge:
                Fins[parentSlot].Edge = childSlot;
                break;
        }
        return true;
    }

    private static FaceUseSlot AddFaceUse(ShellSlot shellSlot, FaceSlot faceSlot, KernelSense sense)
    {
        var faceUseSlot = TryAllocateFaceUses();
        if (faceUseSlot < 0) return -1;
        ref var faceUse = ref FaceUses[faceUseSlot];
        faceUse.Shell = shellSlot;
        faceUse.Face = faceSlot;
        faceUse.Sense = NormalizeFaceUseSense(sense);
        faceUse.PrevInShell = -1;
        faceUse.NextInShell = -1;

        ref var shell = ref Shells[shellSlot];
        if (shell.FirstFaceUseShell < 0)
        {
            shell.FirstFaceUseShell = faceUseSlot;
            shell.LastFaceUseShell = faceUseSlot;
            faceUse.PrevInShell = faceUseSlot;
            faceUse.NextInShell = faceUseSlot;
        }
        else
        {
            var first = shell.FirstFaceUseShell;
            var last = shell.LastFaceUseShell;
            faceUse.PrevInShell = last;
            faceUse.NextInShell = first;
            FaceUses[last].NextInShell = faceUseSlot;
            FaceUses[first].PrevInShell = faceUseSlot;
            shell.LastFaceUseShell = faceUseSlot;
        }
        shell.FaceUseCount++;

        ref var face = ref Faces[faceSlot];
        if (faceUse.Sense == ParasolidConstants.PK_TOPOL_sense_negative_c)
        {
            face.BackShell = shellSlot;
            face.BackFaceUse = faceUseSlot;
        }
        else
        {
            face.FrontShell = shellSlot;
            face.FrontFaceUse = faceUseSlot;
        }

        return faceUseSlot;
    }

    private static KernelSense NormalizeFaceUseSense(KernelSense sense)
    {
        return sense == ParasolidConstants.PK_TOPOL_sense_negative_c
            ? ParasolidConstants.PK_TOPOL_sense_negative_c
            : ParasolidConstants.PK_TOPOL_sense_positive_c;
    }

    private static void AppendRegionToBody(BodySlot bodySlot, RegionSlot regionSlot)
    {
        ref var body = ref Bodies[bodySlot];
        ref var region = ref Regions[regionSlot];
        region.Body = bodySlot;

        if (body.FirstRegion < 0)
        {
            body.FirstRegion = regionSlot;
            body.LastRegion = regionSlot;
            region.PrevInBody = regionSlot;
            region.NextInBody = regionSlot;
        }
        else
        {
            var first = body.FirstRegion;
            var last = body.LastRegion;
            region.PrevInBody = last;
            region.NextInBody = first;
            Regions[last].NextInBody = regionSlot;
            Regions[first].PrevInBody = regionSlot;
            body.LastRegion = regionSlot;
        }
        body.RegionCount++;
    }

    private static void AppendShellToBody(BodySlot bodySlot, ShellSlot shellSlot)
    {
        ref var body = ref Bodies[bodySlot];
        ref var shell = ref Shells[shellSlot];
        shell.Body = bodySlot;

        if (body.FirstShell < 0)
        {
            body.FirstShell = shellSlot;
            body.LastShell = shellSlot;
            shell.PrevInBody = shellSlot;
            shell.NextInBody = shellSlot;
        }
        else
        {
            var first = body.FirstShell;
            var last = body.LastShell;
            shell.PrevInBody = last;
            shell.NextInBody = first;
            Shells[last].NextInBody = shellSlot;
            Shells[first].PrevInBody = shellSlot;
            body.LastShell = shellSlot;
        }
        body.ShellCount++;
    }

    private static void AppendShellToRegion(RegionSlot regionSlot, ShellSlot shellSlot)
    {
        ref var region = ref Regions[regionSlot];
        ref var shell = ref Shells[shellSlot];
        shell.Region = regionSlot;

        if (region.FirstShell < 0)
        {
            region.FirstShell = shellSlot;
            region.LastShell = shellSlot;
            shell.PrevInRegion = shellSlot;
            shell.NextInRegion = shellSlot;
        }
        else
        {
            var first = region.FirstShell;
            var last = region.LastShell;
            shell.PrevInRegion = last;
            shell.NextInRegion = first;
            Shells[last].NextInRegion = shellSlot;
            Shells[first].PrevInRegion = shellSlot;
            region.LastShell = shellSlot;
        }
        region.ShellCount++;
    }

    private static void AppendFaceToBody(BodySlot bodySlot, FaceSlot faceSlot)
    {
        ref var body = ref Bodies[bodySlot];
        ref var face = ref Faces[faceSlot];
        if (body.FirstFaceBody < 0)
        {
            body.FirstFaceBody = faceSlot;
            body.LastFaceBody = faceSlot;
            face.PrevInBody = faceSlot;
            face.NextInBody = faceSlot;
        }
        else
        {
            var first = body.FirstFaceBody;
            var last = body.LastFaceBody;
            face.PrevInBody = last;
            face.NextInBody = first;
            Faces[last].NextInBody = faceSlot;
            Faces[first].PrevInBody = faceSlot;
            body.LastFaceBody = faceSlot;
        }
        body.FaceCountBody++;
    }

    private static void AppendEdgeToBody(BodySlot bodySlot, EdgeSlot edgeSlot)
    {
        ref var body = ref Bodies[bodySlot];
        ref var edge = ref Edges[edgeSlot];
        edge.Body = bodySlot;
        if (body.FirstEdgeBody < 0)
        {
            body.FirstEdgeBody = edgeSlot;
            body.LastEdgeBody = edgeSlot;
            edge.PrevInBody = edgeSlot;
            edge.NextInBody = edgeSlot;
        }
        else
        {
            var first = body.FirstEdgeBody;
            var last = body.LastEdgeBody;
            edge.PrevInBody = last;
            edge.NextInBody = first;
            Edges[last].NextInBody = edgeSlot;
            Edges[first].PrevInBody = edgeSlot;
            body.LastEdgeBody = edgeSlot;
        }
        body.EdgeCountBody++;
    }

    private static void AppendVertexToBody(BodySlot bodySlot, VertexSlot vertexSlot)
    {
        ref var body = ref Bodies[bodySlot];
        ref var vertex = ref Vertices[vertexSlot];
        vertex.Body = bodySlot;
        if (body.FirstVertexBody < 0)
        {
            body.FirstVertexBody = vertexSlot;
            body.LastVertexBody = vertexSlot;
            vertex.PrevInBody = vertexSlot;
            vertex.NextInBody = vertexSlot;
        }
        else
        {
            var first = body.FirstVertexBody;
            var last = body.LastVertexBody;
            vertex.PrevInBody = last;
            vertex.NextInBody = first;
            Vertices[last].NextInBody = vertexSlot;
            Vertices[first].PrevInBody = vertexSlot;
            body.LastVertexBody = vertexSlot;
        }
        body.VertexCountBody++;
    }

    private static void AppendLoopToFace(FaceSlot faceSlot, LoopSlot loopSlot)
    {
        ref var face = ref Faces[faceSlot];
        ref var loop = ref Loops[loopSlot];
        loop.Face = faceSlot;
        if (face.FirstLoop < 0)
        {
            face.FirstLoop = loopSlot;
            face.LastLoop = loopSlot;
            loop.PrevInFace = loopSlot;
            loop.NextInFace = loopSlot;
        }
        else
        {
            var first = face.FirstLoop;
            var last = face.LastLoop;
            loop.PrevInFace = last;
            loop.NextInFace = first;
            Loops[last].NextInFace = loopSlot;
            Loops[first].PrevInFace = loopSlot;
            face.LastLoop = loopSlot;
        }
        face.LoopCount++;
    }

    private static void AppendFinToLoop(LoopSlot loopSlot, FinSlot finSlot)
    {
        ref var loop = ref Loops[loopSlot];
        ref var fin = ref Fins[finSlot];
        fin.Loop = loopSlot;
        if (loop.FirstFin < 0)
        {
            loop.FirstFin = finSlot;
            loop.LastFin = finSlot;
            fin.PrevInLoop = finSlot;
            fin.NextInLoop = finSlot;
        }
        else
        {
            var first = loop.FirstFin;
            var last = loop.LastFin;
            fin.PrevInLoop = last;
            fin.NextInLoop = first;
            Fins[last].NextInLoop = finSlot;
            Fins[first].PrevInLoop = finSlot;
            loop.LastFin = finSlot;
        }
        loop.FinCount++;
    }

    private static void AppendFinToEdge(EdgeSlot edgeSlot, FinSlot finSlot)
    {
        ref var edge = ref Edges[edgeSlot];
        ref var fin = ref Fins[finSlot];
        fin.Edge = edgeSlot;
        if (edge.FirstFinEdge < 0)
        {
            edge.FirstFinEdge = finSlot;
            edge.LastFinEdge = finSlot;
            fin.PrevOfEdge = finSlot;
            fin.NextOfEdge = finSlot;
            fin.Other = finSlot;
        }
        else
        {
            var first = edge.FirstFinEdge;
            var last = edge.LastFinEdge;
            fin.PrevOfEdge = last;
            fin.NextOfEdge = first;
            fin.Other = first;
            Fins[last].NextOfEdge = finSlot;
            Fins[last].Other = finSlot;
            Fins[first].PrevOfEdge = finSlot;
            edge.LastFinEdge = finSlot;
        }
        edge.FinCount++;
    }

    private static void AppendFinToVertex(VertexSlot vertexSlot, FinSlot finSlot)
    {
        if (vertexSlot < 0)
            return;

        ref var vertex = ref Vertices[vertexSlot];
        ref var fin = ref Fins[finSlot];
        fin.Vertex = vertexSlot;
        if (fin.Edge >= 0)
        {
            // fin aligned with edge direction points at the edge's end vertex
            var edge = Edges[fin.Edge];
            fin.Sense = fin.Vertex == edge.StartVertex ? '-' : '+';
        }
        if (vertex.FirstFinVertex < 0)
        {
            vertex.FirstFinVertex = finSlot;
            vertex.LastFinVertex = finSlot;
            fin.PrevAtVertex = finSlot;
            fin.NextAtVertex = finSlot;
        }
        else
        {
            var first = vertex.FirstFinVertex;
            var last = vertex.LastFinVertex;
            fin.PrevAtVertex = last;
            fin.NextAtVertex = first;
            Fins[last].NextAtVertex = finSlot;
            Fins[first].PrevAtVertex = finSlot;
            vertex.LastFinVertex = finSlot;
        }
    }

    private static void AppendBodyToPartition(PartitionSlot partitionSlot, BodySlot bodySlot)
    {
        var session = State.Session;
        if (session == null)
            return;

        ref var partition = ref session->Partitions[FindPartitionSlot(partitionSlot)];
        ref var body = ref Bodies[bodySlot];
        AssignPartition(ref body.Header, partitionSlot);

        if (partition.FirstBody < 0)
        {
            partition.FirstBody = bodySlot;
            partition.LastBody = bodySlot;
            body.PrevInPartition = bodySlot;
            body.NextInPartition = bodySlot;
        }
        else
        {
            var first = partition.FirstBody;
            var last = partition.LastBody;
            body.PrevInPartition = last;
            body.NextInPartition = first;
            Bodies[last].NextInPartition = bodySlot;
            Bodies[first].PrevInPartition = bodySlot;
            partition.LastBody = bodySlot;
        }

        partition.BodyCount++;
        PropagateBodyPartition(bodySlot, partitionSlot);
    }

    private static void PropagateBodyPartition(BodySlot bodySlot, PartitionSlot partitionSlot)
    {
        AssignPartition(ref Bodies[bodySlot].Header, partitionSlot);
        var body = Bodies[bodySlot];

        var regionSlot = body.FirstRegion;
        for (var i = 0; i < body.RegionCount; i++, regionSlot = Regions[regionSlot].NextInBody)
            AssignPartition(ref Regions[regionSlot].Header, partitionSlot);

        var shellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, shellSlot = Shells[shellSlot].NextInBody)
        {
            AssignPartition(ref Shells[shellSlot].Header, partitionSlot);
            var shell = Shells[shellSlot];
            var faceUseSlot = shell.FirstFaceUseShell;
            for (var j = 0; j < shell.FaceUseCount; j++, faceUseSlot = FaceUses[faceUseSlot].NextInShell)
                AssignPartition(ref FaceUses[faceUseSlot].Header, partitionSlot);
        }

        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            AssignPartition(ref Faces[faceSlot].Header, partitionSlot);
            var face = Faces[faceSlot];
            if (face.SurfTag > 0)
            {
                var surfaceSlot = GetSurfaceSlotByTag(face.SurfTag);
                if (surfaceSlot >= 0)
                    AssignPartition(ref Surfaces[surfaceSlot].Header, partitionSlot);
            }

            var loopSlot = face.FirstLoop;
            for (var j = 0; j < face.LoopCount; j++, loopSlot = Loops[loopSlot].NextInFace)
            {
                AssignPartition(ref Loops[loopSlot].Header, partitionSlot);
                var loop = Loops[loopSlot];
                var finSlot = loop.FirstFin;
                for (var k = 0; k < loop.FinCount; k++, finSlot = Fins[finSlot].NextInLoop)
                    AssignPartition(ref Fins[finSlot].Header, partitionSlot);
            }
        }

        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = Edges[edgeSlot].NextInBody)
        {
            AssignPartition(ref Edges[edgeSlot].Header, partitionSlot);
            if (Edges[edgeSlot].CurveTag > 0)
            {
                var curveSlot = GetCurveSlotByTag(Edges[edgeSlot].CurveTag);
                if (curveSlot >= 0)
                    AssignPartition(ref Curves[curveSlot].Header, partitionSlot);
            }
        }

        var vertexSlot = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertexSlot = Vertices[vertexSlot].NextInBody)
        {
            AssignPartition(ref Vertices[vertexSlot].Header, partitionSlot);
            if (Vertices[vertexSlot].PointTag > 0)
            {
                var pointSlot = GetPointSlotByTag(Vertices[vertexSlot].PointTag);
                if (pointSlot >= 0)
                    AssignPartition(ref Points[pointSlot].Header, partitionSlot);
            }
        }
    }

    private static VertexSlot EdgeFinVertex(FinSlot finSlot, EdgeRecord edge)
    {
        if (edge.StartVertex < 0 || edge.EndVertex < 0)
            return -1;

        return finSlot == edge.FirstFinEdge ? edge.StartVertex : edge.EndVertex;
    }

    private static void SetPointOwner(PointTag pointTag, VertexSlot ownerVertex)
    {
        var pointSlot = GetPointSlotByTag(pointTag);
        if (pointSlot < 0)
            return;

        ref var point = ref Points[pointSlot];
        point.OwnerVertex = ownerVertex;
        var bodySlot = Vertices[ownerVertex].Body;
        if (bodySlot < 0)
            return;

        var vertexSlot = Bodies[bodySlot].FirstVertexBody;
        for (var i = 0; i < Bodies[bodySlot].VertexCountBody; i++, vertexSlot = Vertices[vertexSlot].NextInBody)
        {
            if (vertexSlot == ownerVertex)
            {
                point.PrevInBody = VertexPointTag(Vertices[vertexSlot].PrevInBody);
                point.NextInBody = VertexPointTag(Vertices[vertexSlot].NextInBody);
                return;
            }
        }
    }

    private static void SetCurveOwner(CurveTag curveTag, EdgeSlot ownerEdge)
    {
        var curveSlot = GetCurveSlotByTag(curveTag);
        if (curveSlot < 0)
            return;

        ref var curve = ref Curves[curveSlot];
        curve.OwnerEdge = ownerEdge;
        var bodySlot = Edges[ownerEdge].Body;
        if (bodySlot < 0)
            return;

        var edgeSlot = Bodies[bodySlot].FirstEdgeBody;
        for (var i = 0; i < Bodies[bodySlot].EdgeCountBody; i++, edgeSlot = Edges[edgeSlot].NextInBody)
        {
            if (edgeSlot == ownerEdge)
            {
                curve.PrevInBody = EdgeCurveTag(Edges[edgeSlot].PrevInBody);
                curve.NextInBody = EdgeCurveTag(Edges[edgeSlot].NextInBody);
                return;
            }
        }
    }

    private static void SetSurfaceOwner(SurfTag surfaceTag, FaceSlot ownerFace)
    {
        var surfaceSlot = GetSurfaceSlotByTag(surfaceTag);
        if (surfaceSlot < 0)
            return;

        ref var surface = ref Surfaces[surfaceSlot];
        surface.OwnerFace = ownerFace;
        var bodySlot = Shells[Faces[ownerFace].BackShell >= 0 ? Faces[ownerFace].BackShell : Faces[ownerFace].FrontShell].Body;
        if (bodySlot < 0)
            return;

        var faceSlot = Bodies[bodySlot].FirstFaceBody;
        for (var i = 0; i < Bodies[bodySlot].FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            if (faceSlot == ownerFace)
            {
                surface.PrevInBody = FaceSurfaceTag(Faces[faceSlot].PrevInBody);
                surface.NextInBody = FaceSurfaceTag(Faces[faceSlot].NextInBody);
                return;
            }
        }
    }

    private static PointTag VertexPointTag(VertexSlot vertexSlot) => vertexSlot >= 0 ? Vertices[vertexSlot].PointTag : 0;
    private static CurveTag EdgeCurveTag(EdgeSlot edgeSlot) => edgeSlot >= 0 ? Edges[edgeSlot].CurveTag : 0;
    private static SurfTag FaceSurfaceTag(FaceSlot faceSlot) => faceSlot >= 0 ? Faces[faceSlot].SurfTag : 0;

    // Chains faces that share the same surface, in body order (XT face
    // next_on_surface/previous_on_surface chains).
    private static void RebuildFaceSurfaceChains(BodySlot bodySlot)
    {
        var body = Bodies[bodySlot];

        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            Faces[faceSlot].PrevOnSurf = -1;
            Faces[faceSlot].NextOnSurf = -1;
        }

        faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            ref var face = ref Faces[faceSlot];
            if (face.SurfTag <= 0 || face.NextOnSurf >= 0)
                continue;

            var otherSlot = Faces[faceSlot].NextInBody;
            for (var j = i + 1; j < body.FaceCountBody; j++, otherSlot = Faces[otherSlot].NextInBody)
            {
                if (Faces[otherSlot].SurfTag != face.SurfTag)
                    continue;

                face.NextOnSurf = otherSlot;
                Faces[otherSlot].PrevOnSurf = faceSlot;
                break;
            }
        }
        faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            ref var first = ref Faces[faceSlot];
            if (first.PrevOnSurf >= 0 || first.NextOnSurf < 0) continue;
            var last = first.NextOnSurf;
            while (Faces[last].NextOnSurf >= 0) last = Faces[last].NextOnSurf;
            Faces[last].NextOnSurf = faceSlot;
            first.PrevOnSurf = last;
        }
    }

    private static void RebuildBoundaryGeometryLinks(BodySlot bodySlot, bool resetOwners = false)
    {
        RebuildFaceSurfaceChains(bodySlot);
        var body = Bodies[bodySlot];
        if (resetOwners)
        {
            var face = body.FirstFaceBody;
            for (var i = 0; i < body.FaceCountBody; i++, face = Faces[face].NextInBody)
            {
                var slot = GetSurfaceSlotByTag(Faces[face].SurfTag);
                if (slot >= 0) Surfaces[slot].OwnerCount = 0;
            }
            var edge = body.FirstEdgeBody;
            for (var i = 0; i < body.EdgeCountBody; i++, edge = Edges[edge].NextInBody)
            {
                var slot = GetCurveSlotByTag(Edges[edge].CurveTag);
                if (slot >= 0) Curves[slot].OwnerCount = 0;
            }
            var vertex = body.FirstVertexBody;
            for (var i = 0; i < body.VertexCountBody; i++, vertex = Vertices[vertex].NextInBody)
            {
                var slot = GetPointSlotByTag(Vertices[vertex].PointTag);
                if (slot >= 0) Points[slot].OwnerCount = 0;
            }
        }

        SurfaceSlot firstSurface = -1, lastSurface = -1;
        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            var surfTag = Faces[faceSlot].SurfTag;
            var surfaceSlot = GetSurfaceSlotByTag(surfTag);
            if (surfaceSlot < 0)
                continue;

            ref var surface = ref Surfaces[surfaceSlot];
            if (surface.OwnerCount++ != 0) continue;
            surface.OwnerFace = faceSlot;
            if (lastSurface >= 0)
            {
                surface.PrevInBody = Surfaces[lastSurface].Header.Tag;
                Surfaces[lastSurface].NextInBody = surfTag;
            }
            else firstSurface = surfaceSlot;
            lastSurface = surfaceSlot;
        }
        if (lastSurface >= 0)
        {
            Surfaces[lastSurface].NextInBody = Surfaces[firstSurface].Header.Tag;
            Surfaces[firstSurface].PrevInBody = Surfaces[lastSurface].Header.Tag;
        }

        CurveSlot firstCurve = -1, lastCurve = -1;
        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = Edges[edgeSlot].NextInBody)
        {
            var curveTag = Edges[edgeSlot].CurveTag;
            var curveSlot = GetCurveSlotByTag(curveTag);
            if (curveSlot < 0)
                continue;

            ref var curve = ref Curves[curveSlot];
            if (curve.OwnerCount++ != 0) continue;
            curve.OwnerEdge = edgeSlot;
            if (lastCurve >= 0)
            {
                curve.PrevInBody = Curves[lastCurve].Header.Tag;
                Curves[lastCurve].NextInBody = curveTag;
            }
            else firstCurve = curveSlot;
            lastCurve = curveSlot;
        }
        if (lastCurve >= 0)
        {
            Curves[lastCurve].NextInBody = Curves[firstCurve].Header.Tag;
            Curves[firstCurve].PrevInBody = Curves[lastCurve].Header.Tag;
        }

        PointSlot firstPoint = -1, lastPoint = -1;
        var vertexSlot = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertexSlot = Vertices[vertexSlot].NextInBody)
        {
            var pointTag = Vertices[vertexSlot].PointTag;
            var pointSlot = GetPointSlotByTag(pointTag);
            if (pointSlot < 0)
                continue;

            ref var point = ref Points[pointSlot];
            if (point.OwnerCount++ != 0) continue;
            point.OwnerVertex = vertexSlot;
            if (lastPoint >= 0)
            {
                point.PrevInBody = Points[lastPoint].Header.Tag;
                Points[lastPoint].NextInBody = pointTag;
            }
            else firstPoint = pointSlot;
            lastPoint = pointSlot;
        }
        if (lastPoint >= 0)
        {
            Points[lastPoint].NextInBody = Points[firstPoint].Header.Tag;
            Points[firstPoint].PrevInBody = Points[lastPoint].Header.Tag;
        }
    }

    /// <summary>
    /// Assign body-level flat iteration arrays.
    /// </summary>
    private static void AssignBodyFlatIteration(
        int bodySlot, int nTopols, PK_CLASS_t* classes, int* slots, byte* poolKinds)
    {
        ref var body = ref Bodies[bodySlot];
        body.FirstFaceBody = -1;
        body.FirstEdgeBody = -1;
        body.FirstVertexBody = -1;

        for (int i = 0; i < nTopols; i++)
        {
            var slot = slots[i];
            switch ((PoolKind)poolKinds[i])
            {
                case PoolKind.Face:
                    AppendFaceToBody(bodySlot, slot);
                    break;

                case PoolKind.Edge:
                    AppendEdgeToBody(bodySlot, slot);
                    break;

                case PoolKind.Vertex:
                    AppendVertexToBody(bodySlot, slot);
                    break;

                case PoolKind.Region:
                    Regions[slot].Body = bodySlot;
                    break;
            }
        }
    }

    // ── Query APIs ───────────────────────────────────────────────

    /// <summary>
    /// Which sibling chain to follow when traversing fins.
    /// </summary>
    private enum FinChain : byte
    {
        ByLoop = 0,  // FinRecord.NextInLoop  (for LoopAskFins)
        ByEdge = 1,  // FinRecord.NextOfEdge  (for EdgeAskFins)
    }

    /// <summary>
    /// Write tags into the session return arena (bump-allocated) and set the output pointer.
    /// Returns 0 on success, -1 if the arena is exhausted.
    /// </summary>
    private static int WriteTagList(int** output, int count, int startSlot, PoolKind pool, EntityClass entityClass, FinChain finChain = FinChain.ByLoop)
    {
        if (count <= 0)
        {
            *output = null;
            return 0;
        }

        int* buffer = AllocateReturnSlice(count);
        if (buffer is null)
            return -1;

        int slot = startSlot;
        for (int i = 0; i < count; i++)
        {
            if (slot < 0)
                return -1;
            int tag = GetOrAllocateTag(entityClass, pool, slot);
            if (tag <= 0)
                return -1;
            buffer[i] = tag;
            slot = GetNextInChain(pool, slot, finChain);
        }
        *output = buffer;
        return 0;
    }

    private static int WriteBodyTopologyList(
        BodySlot bodySlot,
        int* topols,
        int* classes,
        int maxTopols)
    {
        int index = 0;
        if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Body, PoolKind.Body, bodySlot))
            return -1;

        var body = Bodies[bodySlot];
        var regionSlot = body.FirstRegion;
        for (var i = 0; i < body.RegionCount; i++, regionSlot = Regions[regionSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Region, PoolKind.Region, regionSlot))
                return -1;
        }

        var shellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, shellSlot = Shells[shellSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Shell, PoolKind.Shell, shellSlot))
                return -1;

        }

        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Face, PoolKind.Face, faceSlot))
                return -1;

            var face = Faces[faceSlot];
            var loopSlot = face.FirstLoop;
            for (var j = 0; j < face.LoopCount; j++, loopSlot = Loops[loopSlot].NextInFace)
            {
                if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Loop, PoolKind.Loop, loopSlot))
                    return -1;

                var loop = Loops[loopSlot];
                var finSlot = loop.FirstFin;
                for (var k = 0; k < loop.FinCount; k++, finSlot = Fins[finSlot].NextInLoop)
                {
                    if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Fin, PoolKind.Fin, finSlot))
                        return -1;
                }
            }
        }

        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = Edges[edgeSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Edge, PoolKind.Edge, edgeSlot))
                return -1;
        }

        var vertexSlot = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertexSlot = Vertices[vertexSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Vertex, PoolKind.Vertex, vertexSlot))
                return -1;
        }

        return index;
    }

    private static bool AppendTopology(
        ref int index,
        int maxTopols,
        int* topols,
        int* classes,
        EntityClass entityClass,
        PoolKind pool,
        int slot)
    {
        if (index == maxTopols)
            return false;

        topols[index] = GetOrAllocateTag(entityClass, pool, slot);
        if (topols[index] <= 0)
            return false;

        classes[index] = ToPkClass(entityClass);
        index++;
        return true;
    }

    private static int WriteBodyTopologyRelations(
        BodySlot bodySlot,
        int* topols,
        int topolCount,
        int* parents,
        int* children,
        int* senses,
        int maxRelations)
    {
        int relation = 0;
        int bodyTag = GetOrAllocateTag(EntityClass.Body, PoolKind.Body, bodySlot);
        if (bodyTag <= 0)
            return -1;

        var body = Bodies[bodySlot];
        var regionSlot = body.FirstRegion;
        for (var i = 0; i < body.RegionCount; i++, regionSlot = Regions[regionSlot].NextInBody)
        {
            int regionTag = GetOrAllocateTag(EntityClass.Region, PoolKind.Region, regionSlot);
            if (!AppendRelation(ref relation, maxRelations, parents, children, senses, bodyTag, regionTag, ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                return -1;

            var region = Regions[regionSlot];
            var shellSlot = region.FirstShell;
            for (var j = 0; j < region.ShellCount; j++, shellSlot = Shells[shellSlot].NextInRegion)
            {
                int shellTag = GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, shellSlot);
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, regionTag, shellTag, ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                    return -1;
            }
        }

        var bodyShellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, bodyShellSlot = Shells[bodyShellSlot].NextInBody)
        {
            if (Shells[bodyShellSlot].Region < 0)
            {
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, bodyTag, GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, bodyShellSlot), ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                    return -1;
            }

            var shell = Shells[bodyShellSlot];
            var faceUseSlot = shell.FirstFaceUseShell;
            for (var j = 0; j < shell.FaceUseCount; j++, faceUseSlot = FaceUses[faceUseSlot].NextInShell)
            {
                ref var faceUse = ref FaceUses[faceUseSlot];
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, bodyShellSlot), GetOrAllocateTag(EntityClass.Face, PoolKind.Face, faceUse.Face), faceUse.Sense, topols, topolCount))
                    return -1;
            }
        }

        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            var face = Faces[faceSlot];
            var loopSlot = face.FirstLoop;
            for (var j = 0; j < face.LoopCount; j++, loopSlot = Loops[loopSlot].NextInFace)
            {
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, GetOrAllocateTag(EntityClass.Face, PoolKind.Face, faceSlot), GetOrAllocateTag(EntityClass.Loop, PoolKind.Loop, loopSlot), ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                    return -1;

                var loop = Loops[loopSlot];
                var finSlot = loop.FirstFin;
                for (var k = 0; k < loop.FinCount; k++, finSlot = Fins[finSlot].NextInLoop)
                {
                    if (!AppendRelation(ref relation, maxRelations, parents, children, senses, GetOrAllocateTag(EntityClass.Loop, PoolKind.Loop, loopSlot), GetOrAllocateTag(EntityClass.Fin, PoolKind.Fin, finSlot), ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                        return -1;

                    if (Fins[finSlot].Edge >= 0)
                    {
                        int edgeTag = GetOrAllocateTag(EntityClass.Edge, PoolKind.Edge, Fins[finSlot].Edge);
                        int finTag = GetOrAllocateTag(EntityClass.Fin, PoolKind.Fin, finSlot);
                        if (!AppendRelation(ref relation, maxRelations, parents, children, senses, edgeTag, finTag, ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                            return -1;
                    }
                }
            }
        }

        return relation;
    }

    private static bool AppendRelation(
        ref int relation,
        int maxRelations,
        int* parents,
        int* children,
        int* senses,
        int parentTag,
        int childTag,
        KernelSense sense,
        int* topols,
        int topolCount)
    {
        if (parentTag <= 0 || childTag <= 0 || relation == maxRelations)
            return false;

        int parentIndex = FindTagIndex(topols, topolCount, parentTag);
        int childIndex = FindTagIndex(topols, topolCount, childTag);
        if (parentIndex < 0 || childIndex < 0)
            return false;

        parents[relation] = parentIndex;
        children[relation] = childIndex;
        senses[relation] = sense;
        relation++;
        return true;
    }

    private static int FindTagIndex(int* topols, int count, int tag)
    {
        for (int i = 0; i < count; i++)
        {
            if (topols[i] == tag)
                return i;
        }

        return -1;
    }

    private static int ToPkClass(EntityClass entityClass)
    {
        return entityClass switch
        {
            EntityClass.Body => ParasolidConstants.PK_CLASS_body,
            EntityClass.Assembly => ParasolidConstants.PK_CLASS_assembly,
            EntityClass.Shell => ParasolidConstants.PK_CLASS_shell,
            EntityClass.Face => ParasolidConstants.PK_CLASS_face,
            EntityClass.Loop => ParasolidConstants.PK_CLASS_loop,
            EntityClass.Fin => ParasolidConstants.PK_CLASS_fin,
            EntityClass.Edge => ParasolidConstants.PK_CLASS_edge,
            EntityClass.Vertex => ParasolidConstants.PK_CLASS_vertex,
            EntityClass.Region => ParasolidConstants.PK_CLASS_region,
            EntityClass.Point => ParasolidConstants.PK_CLASS_point,
            EntityClass.Curve => ParasolidConstants.PK_CLASS_curve,
            EntityClass.Surface => ParasolidConstants.PK_CLASS_surf,
            _ => (int)entityClass,
        };
    }

    /// <summary>
    /// Follow the sibling chain for a pool type.
    /// </summary>
    private static int GetNextInChain(PoolKind pool, int slot, FinChain finChain = FinChain.ByLoop)
    {
        return pool switch
        {
            PoolKind.Shell => Shells[slot].NextInBody,
            PoolKind.Face => Faces[slot].NextInBody,
            PoolKind.Loop => Loops[slot].NextInFace,
            PoolKind.Fin => finChain == FinChain.ByEdge ? Fins[slot].NextOfEdge : Fins[slot].NextInLoop,
            PoolKind.Edge => Edges[slot].NextInBody,
            PoolKind.Vertex => Vertices[slot].NextInBody,
            PoolKind.Region => Regions[slot].NextInBody,
            _ => -1,
        };
    }

    private static int BodyAskShellsImplementation(int bodyTag, int* nShells, int** shells)
    {
        if (nShells is null || shells is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(bodyTag) || (EntityClass)TagRec(bodyTag).ClassCode != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var body = ref Bodies[TagRec(bodyTag).Slot];
        *nShells = body.ShellCount;
        if (WriteTagList(shells, body.ShellCount, body.FirstShell, PoolKind.Shell, EntityClass.Shell, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int BodyAskFacesImplementation(int bodyTag, int* nFaces, int** faces)
    {
        if (nFaces is null || faces is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(bodyTag) || (EntityClass)TagRec(bodyTag).ClassCode != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var body = ref Bodies[TagRec(bodyTag).Slot];
        *nFaces = body.FaceCountBody;
        if (WriteTagList(faces, body.FaceCountBody, body.FirstFaceBody, PoolKind.Face, EntityClass.Face, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int BodyAskEdgesImplementation(int bodyTag, int* nEdges, int** edges)
    {
        if (nEdges is null || edges is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(bodyTag) || (EntityClass)TagRec(bodyTag).ClassCode != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var body = ref Bodies[TagRec(bodyTag).Slot];
        *nEdges = body.EdgeCountBody;
        if (WriteTagList(edges, body.EdgeCountBody, body.FirstEdgeBody, PoolKind.Edge, EntityClass.Edge, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int BodyAskVerticesImplementation(int bodyTag, int* nVertices, int** vertices)
    {
        if (nVertices is null || vertices is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(bodyTag) || (EntityClass)TagRec(bodyTag).ClassCode != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var body = ref Bodies[TagRec(bodyTag).Slot];
        *nVertices = body.VertexCountBody;
        if (WriteTagList(vertices, body.VertexCountBody, body.FirstVertexBody, PoolKind.Vertex, EntityClass.Vertex, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int BodyAskRegionsImplementation(int bodyTag, int* nRegions, int** regions)
    {
        if (nRegions is null || regions is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(bodyTag) || (EntityClass)TagRec(bodyTag).ClassCode != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var body = ref Bodies[TagRec(bodyTag).Slot];
        *nRegions = body.RegionCount;
        if (WriteTagList(regions, body.RegionCount, body.FirstRegion, PoolKind.Region, EntityClass.Region, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int RegionIsSolidImplementation(int regionTag, KernelLogical* isSolid)
    {
        if (isSolid is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(regionTag) || (EntityClass)TagRec(regionTag).ClassCode != EntityClass.Region)
            return ParasolidConstants.PK_ERROR_unknown_class;

        *isSolid = Regions[TagRec(regionTag).Slot].IsSolid;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int EntityAskPartitionImplementation(EntityTag entityTag, PartitionSlot* partition)
    {
        if (partition is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        var entityPartition = GetEntityPartition(entityTag);
        if (entityPartition < 0)
            return ParasolidConstants.PK_ERROR_unknown_class;

        *partition = entityPartition;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int SessionAskCurrentPartitionImplementation(PartitionSlot* partition)
    {
        if (partition is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        var slot = State.Session->FindThread(Environment.CurrentManagedThreadId);
        *partition = slot >= 0 ? State.Session->Threads[slot].CurrentPartition : 0;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int BodyAskTopologyImplementation(
        int bodyTag,
        PK_BODY_ask_topology_o_s* options,
        int* nTopols,
        nint* topols,
        nint* classes,
        int* nRelations,
        nint* parents,
        nint* children,
        nint* senses)
    {
        if (nTopols is null || topols is null || classes is null || nRelations is null || parents is null || children is null || senses is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(bodyTag) || (EntityClass)TagRec(bodyTag).ClassCode != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        int bodySlot = TagRec(bodyTag).Slot;
        ref var body = ref Bodies[bodySlot];
        int topolCount = 1 + body.RegionCount + body.ShellCount + body.FaceCountBody + body.EdgeCountBody + body.VertexCountBody;
        int finCount = 0;
        int faceUseCount = 0;
        var shellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, shellSlot = Shells[shellSlot].NextInBody)
        {
            faceUseCount += Shells[shellSlot].FaceUseCount;
        }
        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            var face = Faces[faceSlot];
            var loopSlot = face.FirstLoop;
            for (var j = 0; j < face.LoopCount; j++, loopSlot = Loops[loopSlot].NextInFace)
            {
                topolCount += 1 + Loops[loopSlot].FinCount;
                finCount += Loops[loopSlot].FinCount;
            }
        }

        int edgeFinRelationCount = 0;
        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = Edges[edgeSlot].NextInBody)
        {
            edgeFinRelationCount += Edges[edgeSlot].FinCount;
        }

        int relationCount = body.RegionCount + body.ShellCount + faceUseCount + finCount + edgeFinRelationCount;
        faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            ref var face = ref Faces[faceSlot];
            relationCount += face.LoopCount;
        }

        int* topolBuffer = AllocateReturnSlice(topolCount);
        int* classBuffer = AllocateReturnSlice(topolCount);
        int* parentBuffer = AllocateReturnSlice(relationCount);
        int* childBuffer = AllocateReturnSlice(relationCount);
        int* senseBuffer = AllocateReturnSlice(relationCount);
        if (topolBuffer is null || classBuffer is null || parentBuffer is null || childBuffer is null || senseBuffer is null)
            return ParasolidConstants.PK_ERROR_general_body;

        int writtenTopols = WriteBodyTopologyList(bodySlot, topolBuffer, classBuffer, topolCount);
        if (writtenTopols != topolCount)
            return ParasolidConstants.PK_ERROR_general_body;

        int writtenRelations = WriteBodyTopologyRelations(bodySlot, topolBuffer, topolCount, parentBuffer, childBuffer, senseBuffer, relationCount);
        if (writtenRelations != relationCount)
            return ParasolidConstants.PK_ERROR_general_body;

        *nTopols = topolCount;
        *topols = (nint)topolBuffer;
        *classes = (nint)classBuffer;
        *nRelations = relationCount;
        *parents = (nint)parentBuffer;
        *children = (nint)childBuffer;
        *senses = (nint)senseBuffer;
        _ = options;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int FaceAskLoopsImplementation(int faceTag, int* nLoops, int** loops)
    {
        if (nLoops is null || loops is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(faceTag) || (EntityClass)TagRec(faceTag).ClassCode != EntityClass.Face)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var face = ref Faces[TagRec(faceTag).Slot];
        *nLoops = face.LoopCount;
        if (WriteTagList(loops, face.LoopCount, face.FirstLoop, PoolKind.Loop, EntityClass.Loop, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int FaceAskSurfImplementation(int faceTag, int* surfTag)
    {
        if (surfTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(faceTag) || (EntityClass)TagRec(faceTag).ClassCode != EntityClass.Face)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var face = ref Faces[TagRec(faceTag).Slot];
        *surfTag = face.SurfTag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int FaceAskShellsImplementation(int faceTag, int* shells)
    {
        if (shells is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(faceTag) || (EntityClass)TagRec(faceTag).ClassCode != EntityClass.Face)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var face = ref Faces[TagRec(faceTag).Slot];
        shells[0] = face.BackShell >= 0 ? GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, face.BackShell) : 0;
        shells[1] = face.FrontShell >= 0 ? GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, face.FrontShell) : 0;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int LoopAskFaceImplementation(int loopTag, int* faceTag)
    {
        if (faceTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(loopTag) || (EntityClass)TagRec(loopTag).ClassCode != EntityClass.Loop)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var loop = ref Loops[TagRec(loopTag).Slot];
        *faceTag = GetOrAllocateTag(EntityClass.Face, PoolKind.Face, loop.Face);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int LoopAskFinsImplementation(int loopTag, int* nFins, int** fins)
    {
        if (nFins is null || fins is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(loopTag) || (EntityClass)TagRec(loopTag).ClassCode != EntityClass.Loop)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var loop = ref Loops[TagRec(loopTag).Slot];
        *nFins = loop.FinCount;
        if (WriteTagList(fins, loop.FinCount, loop.FirstFin, PoolKind.Fin, EntityClass.Fin, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int EdgeAskFinsImplementation(int edgeTag, int* nFins, int** fins)
    {
        if (nFins is null || fins is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(edgeTag) || (EntityClass)TagRec(edgeTag).ClassCode != EntityClass.Edge)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var edge = ref Edges[TagRec(edgeTag).Slot];
        *nFins = edge.FinCount;
        if (WriteTagList(fins, edge.FinCount, edge.FirstFinEdge, PoolKind.Fin, EntityClass.Fin, FinChain.ByEdge) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int EdgeAskCurveImplementation(int edgeTag, int* curveTag)
    {
        if (curveTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(edgeTag) || (EntityClass)TagRec(edgeTag).ClassCode != EntityClass.Edge)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var edge = ref Edges[TagRec(edgeTag).Slot];
        *curveTag = edge.CurveTag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int VertexAskPointImplementation(int vertexTag, int* pointTag)
    {
        if (pointTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(vertexTag) || (EntityClass)TagRec(vertexTag).ClassCode != EntityClass.Vertex)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var vert = ref Vertices[TagRec(vertexTag).Slot];
        *pointTag = vert.PointTag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int FinAskEdgeImplementation(int finTag, int* edgeTag)
    {
        if (edgeTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(finTag) || (EntityClass)TagRec(finTag).ClassCode != EntityClass.Fin)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var fin = ref Fins[TagRec(finTag).Slot];
        *edgeTag = GetOrAllocateTag(EntityClass.Edge, PoolKind.Edge, fin.Edge);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int FinAskLoopImplementation(int finTag, int* loopTag)
    {
        if (loopTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(finTag) || (EntityClass)TagRec(finTag).ClassCode != EntityClass.Fin)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var fin = ref Fins[TagRec(finTag).Slot];
        *loopTag = GetOrAllocateTag(EntityClass.Loop, PoolKind.Loop, fin.Loop);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int FinAskFaceImplementation(int finTag, int* faceTag)
    {
        if (faceTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(finTag) || (EntityClass)TagRec(finTag).ClassCode != EntityClass.Fin)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var fin = ref Fins[TagRec(finTag).Slot];
        var faceSlot = Loops[fin.Loop].Face;
        *faceTag = GetOrAllocateTag(EntityClass.Face, PoolKind.Face, faceSlot);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── PK_TRANSF_create ──────────────────────────────────────────

    private static int TransfCreateImplementation(PK_TRANSF_sf_s* transfSf, int* transfTag)
    {
        if (transfTag != null) *transfTag = 0;
        if (transfSf is null || transfTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        int slot;
        if (!Transforms.TryAllocate(out slot)) return ParasolidConstants.PK_ERROR_memory_full;
        ref var rec = ref Transforms[slot];
        AssignPartition(ref rec.Header, CurrentPartition);
        Unsafe.CopyBlock(ref Unsafe.As<double, byte>(ref rec.Matrix[0]),
                          ref Unsafe.As<double, byte>(ref transfSf->matrix[0]),
                          16 * sizeof(double));

        var tag = AllocateTag(EntityClass.Transform, PoolKind.Transform, slot, rec.Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        *transfTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int CylCreateImplementation(PK_CYL_sf_s* cylSf, int* cylTag)
    {
        if (cylTag != null) *cylTag = 0;
        if (cylSf is null || cylTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (cylSf->radius <= 0)
            return ParasolidConstants.PK_ERROR_distance_le_0;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        ReadAxis2(&cylSf->basis_set, out double ox, out double oy, out double oz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ);
        int dataSlot = TryAllocateCylinderData();
        if (dataSlot < 0) return ParasolidConstants.PK_ERROR_memory_full;
        ref var data = ref CylinderDataPool[dataSlot];
        data.LocationX = ox; data.LocationY = oy; data.LocationZ = oz;
        data.AxisX = axX; data.AxisY = axY; data.AxisZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;
        data.Radius = cylSf->radius;

        int surfSlot = TryAllocateSurfaces();
        if (surfSlot < 0) return -1;
        ref var surf = ref Surfaces[surfSlot];
        AssignPartition(ref surf.Header, CurrentPartition);
        surf.Class = SurfaceClass.Cylinder;
        surf.DataIndex = dataSlot;

        int tag = AllocateTag(EntityClass.Surface, PoolKind.Surface, surfSlot, surf.Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        *cylTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int CylAskImplementation(int cylTag, PK_CYL_sf_s* cylSf)
    {
        if (cylSf is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsValidTag(cylTag) || (EntityClass)TagRec(cylTag).ClassCode != EntityClass.Surface)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var surf = ref Surfaces[TagRec(cylTag).Slot];
        if (surf.Class != SurfaceClass.Cylinder)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var data = ref CylinderDataPool[surf.DataIndex];
        cylSf->basis_set.location.coord[0] = data.LocationX;
        cylSf->basis_set.location.coord[1] = data.LocationY;
        cylSf->basis_set.location.coord[2] = data.LocationZ;
        cylSf->basis_set.axis.coord[0] = data.AxisX;
        cylSf->basis_set.axis.coord[1] = data.AxisY;
        cylSf->basis_set.axis.coord[2] = data.AxisZ;
        cylSf->basis_set.ref_direction.coord[0] = data.RefDirX;
        cylSf->basis_set.ref_direction.coord[1] = data.RefDirY;
        cylSf->basis_set.ref_direction.coord[2] = data.RefDirZ;
        cylSf->radius = data.Radius;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── PK_BODY_create_solid_block ─────────────────────────────────

    private static int BodyCreateSolidBlockImplementation(double x, double y, double z, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (bodyTag != null) *bodyTag = 0;
        if (x <= 0 || y <= 0 || z <= 0)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (bodyTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        return CreateSolidBlockCore(x, y, z, basisSet, bodyTag);
    }

    internal static int CreateSolidBlockCore(double x, double y, double z, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {

        var bodySlot = TryAllocateBodies();
        if (bodySlot < 0) return -1;
        ref var body = ref Bodies[bodySlot];
        InitializeBody(ref body);
        AssignPartition(ref body.Header, CurrentPartition);

        if (!CreateSolidRegionsAndShells(bodySlot, out var voidShellSlot, out var solidShellSlot))
            return ParasolidConstants.PK_ERROR_memory_full;

        ReadAxis2(basisSet, out double ox, out double oy, out double oz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ);
        Cross(axX, axY, axZ, refX, refY, refZ, out double thirdX, out double thirdY, out double thirdZ);

        // Parasolid centres the base rectangle at the local origin.
        Span<double> px = stackalloc double[8];
        Span<double> py = stackalloc double[8];
        Span<double> pz = stackalloc double[8];

        var hx = x * 0.5;
        var hy = y * 0.5;
        px[0] = ox - hx * refX - hy * thirdX; py[0] = oy - hx * refY - hy * thirdY; pz[0] = oz - hx * refZ - hy * thirdZ;
        px[1] = ox + hx * refX - hy * thirdX; py[1] = oy + hx * refY - hy * thirdY; pz[1] = oz + hx * refZ - hy * thirdZ;
        px[2] = ox + hx * refX + hy * thirdX; py[2] = oy + hx * refY + hy * thirdY; pz[2] = oz + hx * refZ + hy * thirdZ;
        px[3] = ox - hx * refX + hy * thirdX; py[3] = oy - hx * refY + hy * thirdY; pz[3] = oz - hx * refZ + hy * thirdZ;
        for (int i = 0; i < 4; i++)
        {
            px[i + 4] = px[i] + z * axX;
            py[i + 4] = py[i] + z * axY;
            pz[i + 4] = pz[i] + z * axZ;
        }

        // Allocate 8 vertices
        Span<int> vtxSlots = stackalloc int[8];
        Span<int> pointTags = stackalloc int[8];
        for (int i = 0; i < 8; i++)
        {
            vtxSlots[i] = TryAllocateVertices();
            if (vtxSlots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;
            pointTags[i] = CreatePointTag(px[i], py[i], pz[i]);
            if (pointTags[i] <= 0)
                return ParasolidConstants.PK_ERROR_general_body;
        }
        for (int i = 0; i < 8; i++)
        {
            ref var vtx = ref Vertices[vtxSlots[i]];
            vtx.PointTag = pointTags[i];
            vtx.FirstFinVertex = -1;
            vtx.LastFinVertex = -1;
            AppendVertexToBody(bodySlot, vtxSlots[i]);
        }

        // Allocate 12 edges
        Span<int> edgeSlots = stackalloc int[12];
        Span<int> edgeCurveTags = stackalloc int[12];
        // Explicit stores avoid the runtime helper allocations generated
        // for RVA-backed constant span initializers in unoptimized builds.
        Span<int> edgeVertexPairs = stackalloc int[24];
        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) & 3;
            edgeVertexPairs[2 * i] = i;
            edgeVertexPairs[2 * i + 1] = next;
            edgeVertexPairs[2 * i + 8] = i + 4;
            edgeVertexPairs[2 * i + 9] = next + 4;
            edgeVertexPairs[2 * i + 16] = i;
            edgeVertexPairs[2 * i + 17] = i + 4;
        }
        for (int i = 0; i < 12; i++)
        {
            edgeSlots[i] = TryAllocateEdges();
            if (edgeSlots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;
            int v0 = edgeVertexPairs[i * 2];
            int v1 = edgeVertexPairs[i * 2 + 1];
            edgeCurveTags[i] = CreateLineCurveTag(px[v0], py[v0], pz[v0], px[v1] - px[v0], py[v1] - py[v0], pz[v1] - pz[v0]);
            if (edgeCurveTags[i] <= 0)
                return ParasolidConstants.PK_ERROR_general_body;
        }
        for (int i = 0; i < 12; i++)
        {
            ref var edge = ref Edges[edgeSlots[i]];
            edge.Body = bodySlot;
            edge.StartVertex = vtxSlots[edgeVertexPairs[i * 2]];
            edge.EndVertex = vtxSlots[edgeVertexPairs[i * 2 + 1]];
            edge.CurveTag = edgeCurveTags[i];
            edge.FirstFinEdge = -1;
            edge.LastFinEdge = -1;
            AppendEdgeToBody(bodySlot, edgeSlots[i]);
        }

        // Face definitions: 6 faces, each with 4 edge indices
        // Face 0: bottom (z=0)  edges 3,2,1,0 (outward normal is -axis)
        // Face 1: top (z=h)     edges 4,5,6,7
        // Face 2: front         edges 0,9,4,8
        // Face 3: right         edges 1,10,5,9
        // Face 4: back          edges 2,11,6,10
        // Face 5: left          edges 3,8,7,11
        Span<int> faceEdgeIndices = stackalloc int[24];
        for (var i = 0; i < 4; i++)
        {
            faceEdgeIndices[i] = 3 - i;
            faceEdgeIndices[i + 4] = i + 4;
            faceEdgeIndices[8 + 4 * i] = i;
            faceEdgeIndices[9 + 4 * i] = 8 + ((i + 1) & 3);
            faceEdgeIndices[10 + 4 * i] = 4 + i;
            faceEdgeIndices[11 + 4 * i] = 8 + i;
        }

        Span<int> faceSlots = stackalloc int[6];
        Span<int> loopSlots = stackalloc int[6];
        Span<int> faceSurfTags = stackalloc int[6];
        faceSurfTags[0] = CreatePlaneSurfaceTag(px[0], py[0], pz[0], -axX, -axY, -axZ, -refX, -refY, -refZ);
        faceSurfTags[1] = CreatePlaneSurfaceTag(px[4], py[4], pz[4], axX, axY, axZ, refX, refY, refZ);
        faceSurfTags[2] = CreatePlaneSurfaceTag(px[0], py[0], pz[0], -thirdX, -thirdY, -thirdZ, axX, axY, axZ);
        faceSurfTags[3] = CreatePlaneSurfaceTag(px[1], py[1], pz[1], refX, refY, refZ, axX, axY, axZ);
        faceSurfTags[4] = CreatePlaneSurfaceTag(px[2], py[2], pz[2], thirdX, thirdY, thirdZ, axX, axY, axZ);
        faceSurfTags[5] = CreatePlaneSurfaceTag(px[3], py[3], pz[3], -refX, -refY, -refZ, axX, axY, axZ);
        for (int i = 0; i < 6; i++)
        {
            if (faceSurfTags[i] <= 0)
                return ParasolidConstants.PK_ERROR_general_body;
        }

        for (int f = 0; f < 6; f++)
        {
            faceSlots[f] = TryAllocateFaces();
            loopSlots[f] = TryAllocateLoops();
            if (faceSlots[f] < 0 || loopSlots[f] < 0) return ParasolidConstants.PK_ERROR_memory_full;

            ref var face = ref Faces[faceSlots[f]];
            ref var loop = ref Loops[loopSlots[f]];

            InitializeFace(ref face);
            face.SurfTag = faceSurfTags[f];
            AppendFaceToBody(bodySlot, faceSlots[f]);

            loop.Face = faceSlots[f];
            loop.FirstFin = -1;
            loop.LastFin = -1;
            loop.PrevInFace = -1;
            loop.NextInFace = -1;
            AppendLoopToFace(faceSlots[f], loopSlots[f]);

            // Allocate 4 fins per loop
            for (int e = 0; e < 4; e++)
            {
                int ei = faceEdgeIndices[f * 4 + e];
                int finSlot = TryAllocateFins();
        if (finSlot < 0) return -1;
                ref var fin = ref Fins[finSlot];

                fin.Edge = edgeSlots[ei];
                fin.Loop = loopSlots[f];
                fin.NextInLoop = fin.PrevInLoop = -1;
                fin.Other = -1;
                fin.Curve = -1;
                fin.Vertex = -1;
                fin.NextAtVertex = fin.PrevAtVertex = -1;
                fin.NextOfEdge = fin.PrevOfEdge = -1;
                fin.Sense = '+';

                AppendFinToLoop(loopSlots[f], finSlot);
                AppendFinToEdge(edgeSlots[ei], finSlot);

                var nextEi = faceEdgeIndices[f * 4 + ((e + 1) & 3)];
                var edgeStart = edgeVertexPairs[ei * 2];
                var edgeEnd = edgeVertexPairs[ei * 2 + 1];
                var nextStart = edgeVertexPairs[nextEi * 2];
                var nextEnd = edgeVertexPairs[nextEi * 2 + 1];
                var finVertex = edgeEnd == nextStart || edgeEnd == nextEnd ? edgeEnd : edgeStart;
                AppendFinToVertex(vtxSlots[finVertex], finSlot);
            }

            if (AddFaceUse(solidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_negative_c) < 0) return ParasolidConstants.PK_ERROR_memory_full;
            if (AddFaceUse(voidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_positive_c) < 0) return ParasolidConstants.PK_ERROR_memory_full;
        }

        // Build result tag
        var tag = AllocateTag(EntityClass.Body, PoolKind.Body, bodySlot, body.Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;
        RebuildBoundaryGeometryLinks(bodySlot);
        AppendBodyToPartition(CurrentPartition, bodySlot);
        if (!PublishBodyTopologyTags(bodySlot)) return ParasolidConstants.PK_ERROR_memory_full;
        *bodyTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int BodyCreateSolidCylImplementation(double radius, double height, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (bodyTag != null) *bodyTag = 0;
        if (bodyTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (radius <= 0 || height <= 0)
            return ParasolidConstants.PK_ERROR_distance_le_0;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        return CreateSolidCylinderCore(radius, height, basisSet, bodyTag);
    }

    private static int BodyCreateSolidConeImplementation(double radius, double height, double semiAngle, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (bodyTag != null) *bodyTag = 0;
        if (bodyTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (height <= 0)
            return ParasolidConstants.PK_ERROR_distance_le_0;
        if (semiAngle <= 0 || semiAngle >= Math.PI * 0.5)
            return ParasolidConstants.PK_ERROR_bad_angle;
        if (radius < 0)
            return ParasolidConstants.PK_ERROR_distance_le_0;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        return CreateSolidConeCore(radius, height, semiAngle, basisSet, bodyTag);
    }

    private static int BodyCreateSolidPrismImplementation(double radius, double height, int nSides, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (bodyTag != null) *bodyTag = 0;
        if (bodyTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (radius <= 0 || height <= 0)
            return ParasolidConstants.PK_ERROR_distance_le_0;
        if (nSides < 3)
            return ParasolidConstants.PK_ERROR_lt_3_sides;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        return CreateSolidPrismCore(radius, height, nSides, basisSet, bodyTag);
    }

    private static int BodyCreateSolidSphereImplementation(double radius, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (bodyTag != null) *bodyTag = 0;
        if (bodyTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (radius <= 0)
            return ParasolidConstants.PK_ERROR_distance_le_0;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        return CreateSingleFaceSolidCore(CreatePrimitiveKind.Sphere, radius, 0, basisSet, bodyTag);
    }

    private static int BodyCreateSolidTorusImplementation(double majorRadius, double minorRadius, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (bodyTag != null) *bodyTag = 0;
        if (bodyTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (minorRadius <= 0 || majorRadius == 0)
            return ParasolidConstants.PK_ERROR_distance_le_0;
        if (majorRadius == minorRadius || majorRadius + minorRadius <= 0)
            return ParasolidConstants.PK_ERROR_majrad_minrad_mismatch;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        return CreateSingleFaceSolidCore(CreatePrimitiveKind.Torus, majorRadius, minorRadius, basisSet, bodyTag);
    }

    private static int PartTransmitBImplementation(int nParts, EntityTag* parts, PK_PART_transmit_o_s* options, PK_MEMORY_block_t* block)
    {
        if (nParts <= 0 || parts is null || block is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        if (options is not null)
        {
            if (options->o_t_version is < 1 or > 4)
                return ParasolidConstants.PK_ERROR_o_t_version_unknown;
            if (options->transmit_format != ParasolidConstants.PK_transmit_format_text_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_binary_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_neutral_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_applio_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_xml_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_typed_binary_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_indexio_c)
                return ParasolidConstants.PK_ERROR_field_of_wrong_type;
            if (options->transmit_format != ParasolidConstants.PK_transmit_format_text_c)
                return ParasolidConstants.PK_ERROR_bad_file_format;
            if (options->o_t_version >= 3 && options->transmit_indexed_context != 0)
                return ParasolidConstants.PK_ERROR_not_implemented;
            if (options->o_t_version >= 4 &&
                options->transmit_meshes != ParasolidConstants.PK_transmit_meshes_separate_c &&
                options->transmit_meshes != ParasolidConstants.PK_transmit_meshes_embedded_c)
                return ParasolidConstants.PK_ERROR_field_of_wrong_type;
        }

        var partList = new EntityTag[nParts];
        for (var i = 0; i < nParts; i++)
        {
            for (var j = 0; j < i; j++)
            {
                if (partList[j] == parts[i])
                    return ParasolidConstants.PK_ERROR_duplicate_parts;
            }

            if (!TryResolveBodySlot(parts[i], out _) && !TryGetReceivedXt(parts[i], out _, out _))
                return ParasolidConstants.PK_ERROR_unsuitable_entity;
            partList[i] = parts[i];
        }

        var transmitVersion = options is null ? 0 : options->transmit_version;
        var transmitUserFields = options is null || options->transmit_user_fields != 0;
        int error;
        string text;
        if (!TryEncodeReceivedParts(partList, transmitVersion, transmitUserFields, out text, out error))
            error = XtWriter.WriteText(partList, transmitVersion, out text);
        if (error != ParasolidConstants.PK_ERROR_no_errors)
            return error;

        var byteCount = System.Text.Encoding.ASCII.GetByteCount(text);
        var bytes = (byte*)State.Session->Returns.TryAllocate((nuint)byteCount);
        if (bytes is null)
            return ParasolidConstants.PK_ERROR_write_memory_full;
        System.Text.Encoding.ASCII.GetBytes(text, new Span<byte>(bytes, byteCount));

        block->next = null;
        block->n_bytes = (nuint)byteCount;
        block->bytes = bytes;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static bool TryEncodeReceivedParts(EntityTag[] parts, int transmitVersion, bool transmitUserFields, out string text, out int error)
    {
        text = "";
        error = ParasolidConstants.PK_ERROR_no_errors;
        XtDocument? document = null;
        ProjectGmKernel.Xt.IXtSchemaModel? schemaModel = null;
        var rootIndexes = new int[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!TryGetReceivedXt(parts[index], out var candidate, out rootIndexes[index], out var candidateModel))
                return false;
            if (document is null)
            {
                document = candidate;
                schemaModel = candidateModel;
            }
            else if (!ReferenceEquals(document, candidate))
                return false;
            if (!ReferenceEquals(schemaModel, candidateModel))
                return false;
        }
        if (document is null)
            return false;
        if (schemaModel is not null)
            document = schemaModel.ToDocument();
        if (!XtText.TrySelectPartRoots(document, rootIndexes, out var selected))
        {
            error = ParasolidConstants.PK_ERROR_unsuitable_entity;
            return true;
        }
        if (!XtText.TryEncodeForTransmitVersion(selected, transmitVersion, out text, transmitUserFields))
            error = ParasolidConstants.PK_ERROR_wrong_version;
        return true;
    }

    private static int PartReceiveBImplementation(PK_MEMORY_block_t block, PK_PART_receive_o_s* options, int* nParts, EntityTag** parts)
    {
        if (nParts is null || parts is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        if (options is not null)
        {
            if (options->o_t_version is < 1 or > 8)
                return ParasolidConstants.PK_ERROR_o_t_version_unknown;
            if (options->o_t_version == 5)
                return ParasolidConstants.PK_ERROR_field_of_wrong_type;
            if (options->transmit_format != ParasolidConstants.PK_transmit_format_text_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_binary_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_neutral_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_applio_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_xml_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_typed_binary_c &&
                options->transmit_format != ParasolidConstants.PK_transmit_format_indexio_c)
                return ParasolidConstants.PK_ERROR_field_of_wrong_type;
            if (options->transmit_format != ParasolidConstants.PK_transmit_format_text_c)
                return ParasolidConstants.PK_ERROR_wrong_format;
            var zeroInitializedTail = options->attdef_mismatch == 0 &&
                options->part_index == 0 && options->n_part_indices == 0 && options->part_indices is null &&
                options->n_identifiers == 0 && options->identifiers is null &&
                options->receive_indexed_context == 0 && options->key_is_partition == 0 &&
                options->receive_compound == 0 && options->receive_using_seek == 0 && options->receive_mixed == 0;
            if (!zeroInitializedTail && options->o_t_version >= 2 &&
                options->attdef_mismatch != ParasolidConstants.PK_ATTDEF_mismatch_fail_c &&
                options->attdef_mismatch != ParasolidConstants.PK_ATTDEF_mismatch_ignore_c)
                return ParasolidConstants.PK_ERROR_field_of_wrong_type;
            if (options->o_t_version >= 3 && options->part_index != 0)
                return ParasolidConstants.PK_ERROR_bad_index;
            if (options->o_t_version is 3 or 4 && options->n_part_indices != 0)
                return ParasolidConstants.PK_ERROR_bad_value;
            if (options->o_t_version >= 6 && (options->n_part_indices < 0 ||
                options->n_part_indices > 0 && options->part_indices is null))
                return ParasolidConstants.PK_ERROR_bad_index;
            if (options->o_t_version == 4 && options->n_identifiers != 0)
                return ParasolidConstants.PK_ERROR_not_a_logical;
            if (options->o_t_version >= 6 && options->n_identifiers != 0)
                return ParasolidConstants.PK_ERROR_bad_value;
            if (options->o_t_version >= 5 && options->receive_indexed_context != 0)
                return ParasolidConstants.PK_ERROR_not_implemented;
            if (options->o_t_version >= 6 && options->key_is_partition != 0)
                return ParasolidConstants.PK_ERROR_bad_value;
            if (!zeroInitializedTail && options->o_t_version >= 6 &&
                options->receive_compound != ParasolidConstants.PK_receive_compound_split_c &&
                options->receive_compound != ParasolidConstants.PK_receive_compound_keep_c &&
                options->receive_compound != ParasolidConstants.PK_receive_compound_fail_c)
                return ParasolidConstants.PK_ERROR_field_of_wrong_type;
            if (!zeroInitializedTail && options->o_t_version >= 7 &&
                options->receive_using_seek != ParasolidConstants.PK_receive_using_seek_no_c &&
                options->receive_using_seek != ParasolidConstants.PK_receive_using_seek_yes_c)
                return ParasolidConstants.PK_ERROR_field_of_wrong_type;
            if (!zeroInitializedTail && options->o_t_version >= 8 &&
                options->receive_mixed != ParasolidConstants.PK_receive_mixed_fail_c &&
                options->receive_mixed != ParasolidConstants.PK_receive_mixed_make_facet_c &&
                options->receive_mixed != ParasolidConstants.PK_receive_mixed_allow_c)
                return ParasolidConstants.PK_ERROR_field_of_wrong_type;
            if (options->o_t_version >= 8 && options->receive_mixed == ParasolidConstants.PK_receive_mixed_make_facet_c)
                return ParasolidConstants.PK_ERROR_not_implemented;
            for (var index = 0; options->o_t_version >= 6 && index < options->n_part_indices; index++)
            {
                if (options->part_indices[index] < 0 || index > 0 && options->part_indices[index] <= options->part_indices[index - 1])
                    return ParasolidConstants.PK_ERROR_bad_index;
            }
        }

        var builder = new System.Text.StringBuilder((int)Math.Min(block.n_bytes, 1_000_000));
        for (var current = &block; current is not null; current = current->next)
        {
            if (current->bytes is null || current->n_bytes == 0)
                continue;
            builder.Append(System.Text.Encoding.ASCII.GetString(current->bytes, checked((int)current->n_bytes)));
        }

        var receiveUserFields = options is not null && options->receive_user_fields != 0;
        var partIndices = options is null || options->o_t_version < 6 || options->n_part_indices == 0
            ? ReadOnlySpan<int>.Empty
            : new ReadOnlySpan<int>(options->part_indices, options->n_part_indices);
        var receiveCompound = options is null || options->o_t_version < 6 || options->receive_compound == 0
            ? ParasolidConstants.PK_receive_compound_split_c
            : options->receive_compound;
        var error = XtReader.ReadText(builder.ToString(), receiveUserFields, partIndices, receiveCompound, out var received);
        if (error != ParasolidConstants.PK_ERROR_no_errors)
            return error;

        var buffer = (EntityTag*)State.Session->Returns.TryAllocate((nuint)(received.Length * sizeof(EntityTag)));
        if (buffer is null)
            return ParasolidConstants.PK_ERROR_write_memory_full;

        for (var i = 0; i < received.Length; i++)
            buffer[i] = received[i];

        *nParts = received.Length;
        *parts = buffer;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int MemoryBlockFreeImplementation(PK_MEMORY_block_t* block)
    {
        if (block is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        var current = block;
        while (current is not null)
        {
            if (current->bytes is not null)
            {
                // Same ownership mechanism as every other returned result.
                if (!State.Session->Returns.TryFree(current->bytes))
                    return ParasolidConstants.PK_ERROR_bad_value;
                current->bytes = null;
            }

            current = current->next;
        }

        block->next = null;
        block->n_bytes = 0;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int MemoryFreeImplementation(void* pointer)
    {
        if (pointer == null) return ParasolidConstants.PK_ERROR_null_arg_address;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (!State.Session->Returns.TryFree(pointer))
            return ParasolidConstants.PK_ERROR_bad_value;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    internal static int CreateSolidCylinderCore(double radius, double height, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        var bodySlot = TryAllocateBodies();
        if (bodySlot < 0) return -1;
        ref var body = ref Bodies[bodySlot];
        InitializeBody(ref body);
        AssignPartition(ref body.Header, CurrentPartition);

        if (!CreateSolidRegionsAndShells(bodySlot, out var voidShellSlot, out var solidShellSlot))
            return ParasolidConstants.PK_ERROR_memory_full;
        ReadAxis2(basisSet, out double ox, out double oy, out double oz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ);

        int sideSurf = CreateCylinderSurfaceTag(ox, oy, oz, axX, axY, axZ, refX, refY, refZ, radius);
        int bottomSurf = CreatePlaneSurfaceTag(ox, oy, oz, -axX, -axY, -axZ, refX, refY, refZ);
        int topSurf = CreatePlaneSurfaceTag(ox + height * axX, oy + height * axY, oz + height * axZ, axX, axY, axZ, refX, refY, refZ);
        if (sideSurf <= 0 || bottomSurf <= 0 || topSurf <= 0)
            return ParasolidConstants.PK_ERROR_general_body;

        Span<int> edgeSlots = stackalloc int[2];
        Span<int> edgeCurves = stackalloc int[2];
        edgeCurves[0] = CreateCircleCurveTag(ox, oy, oz, axX, axY, axZ, refX, refY, refZ, radius);
        edgeCurves[1] = CreateCircleCurveTag(ox + height * axX, oy + height * axY, oz + height * axZ, -axX, -axY, -axZ, refX, refY, refZ, radius);
        if (edgeCurves[0] <= 0 || edgeCurves[1] <= 0)
            return ParasolidConstants.PK_ERROR_general_body;

        for (int i = 0; i < 2; i++)
        {
            edgeSlots[i] = TryAllocateEdges();
            if (edgeSlots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;
        }
        for (int i = 0; i < 2; i++)
        {
            ref var edge = ref Edges[edgeSlots[i]];
            edge.Body = bodySlot;
            edge.StartVertex = -1;
            edge.EndVertex = -1;
            edge.CurveTag = edgeCurves[i];
            edge.FirstFinEdge = -1;
            edge.LastFinEdge = -1;
            AppendEdgeToBody(bodySlot, edgeSlots[i]);
        }
        body.FirstVertexBody = -1;
        body.LastVertexBody = -1;
        body.VertexCountBody = 0;

        Span<int> faceSlots = stackalloc int[3];
        Span<int> surfTags = stackalloc int[3] { sideSurf, bottomSurf, topSurf };
        Span<int> loopCounts = stackalloc int[3] { 2, 1, 1 };

        for (int f = 0; f < 3; f++)
        {
            faceSlots[f] = TryAllocateFaces();
        }
        for (int f = 0; f < 3; f++)
        {
            ref var face = ref Faces[faceSlots[f]];
            InitializeFace(ref face);
            face.SurfTag = surfTags[f];
            AppendFaceToBody(bodySlot, faceSlots[f]);

            for (int l = 0; l < loopCounts[f]; l++)
            {
                int loopSlot = TryAllocateLoops();
        if (loopSlot < 0) return -1;
                ref var loop = ref Loops[loopSlot];
                loop.Face = faceSlots[f];
                loop.FirstFin = -1;
                loop.LastFin = -1;
                loop.PrevInFace = -1;
                loop.NextInFace = -1;
                AppendLoopToFace(faceSlots[f], loopSlot);

                int edgeIndex = f == 0 ? l : f - 1;
                AddFinToLoopAndEdge(loopSlot, faceSlots[f], edgeSlots[edgeIndex]);
            }

            if (AddFaceUse(solidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_negative_c) < 0) return ParasolidConstants.PK_ERROR_memory_full;
            if (AddFaceUse(voidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_positive_c) < 0) return ParasolidConstants.PK_ERROR_memory_full;
        }

        var tag = AllocateTag(EntityClass.Body, PoolKind.Body, bodySlot, body.Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        RebuildBoundaryGeometryLinks(bodySlot);
        AppendBodyToPartition(CurrentPartition, bodySlot);
        if (!PublishBodyTopologyTags(bodySlot)) return ParasolidConstants.PK_ERROR_memory_full;
        *bodyTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int CreateSolidConeCore(double radius, double height, double semiAngle, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        var bodySlot = TryAllocateBodies();
        if (bodySlot < 0) return -1;
        ref var body = ref Bodies[bodySlot];
        InitializeBody(ref body);
        AssignPartition(ref body.Header, CurrentPartition);

        if (!CreateSolidRegionsAndShells(bodySlot, out var voidShellSlot, out var solidShellSlot))
            return ParasolidConstants.PK_ERROR_memory_full;
        ReadAxis2(basisSet, out double ox, out double oy, out double oz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ);
        var topRadius = radius + height * Math.Tan(semiAngle);

        int sideSurf = CreateConeSurfaceTag(ox, oy, oz, axX, axY, axZ, refX, refY, refZ, radius, semiAngle);
        int topSurf = CreatePlaneSurfaceTag(ox + height * axX, oy + height * axY, oz + height * axZ, axX, axY, axZ, refX, refY, refZ);
        if (sideSurf <= 0 || topSurf <= 0)
            return ParasolidConstants.PK_ERROR_general_body;

        Span<int> edgeSlots = stackalloc int[2];
        Span<int> edgeCurves = stackalloc int[2];
        edgeCurves[0] = topRadius > 0 ? CreateCircleCurveTag(ox + height * axX, oy + height * axY, oz + height * axZ, -axX, -axY, -axZ, refX, refY, refZ, topRadius) : 0;
        edgeCurves[1] = radius > 0 ? CreateCircleCurveTag(ox, oy, oz, axX, axY, axZ, refX, refY, refZ, radius) : 0;
        var edgeCount = radius > 0 ? 2 : 1;
        for (int i = 0; i < edgeCount; i++)
        {
            if (edgeCurves[i] <= 0)
                return ParasolidConstants.PK_ERROR_general_body;

            edgeSlots[i] = TryAllocateEdges();
            if (edgeSlots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;
            ref var edge = ref Edges[edgeSlots[i]];
            edge.Body = bodySlot;
            edge.StartVertex = -1;
            edge.EndVertex = -1;
            edge.CurveTag = edgeCurves[i];
            edge.FirstFinEdge = -1;
            edge.LastFinEdge = -1;
            AppendEdgeToBody(bodySlot, edgeSlots[i]);
        }

        if (radius == 0)
            AddVertex(bodySlot, ox, oy, oz, out _);

        int faceCount = radius > 0 ? 3 : 2;
        Span<int> faceSlots = stackalloc int[3];
        Span<int> surfTags = stackalloc int[3];
        surfTags[0] = sideSurf;
        surfTags[1] = topSurf;
        if (radius > 0)
        {
            surfTags[2] = CreatePlaneSurfaceTag(ox, oy, oz, -axX, -axY, -axZ, refX, refY, refZ);
            if (surfTags[2] <= 0)
                return ParasolidConstants.PK_ERROR_general_body;
        }

        for (int f = 0; f < faceCount; f++)
            faceSlots[f] = AddFace(bodySlot, surfTags[f]);

        if (radius == 0)
        {
            AddLoopWithEdges(faceSlots[0], edgeSlots, edgeCount);
            AddLoopWithEdges(faceSlots[1], edgeSlots, 1);
            var apexLoop = AddLoop(faceSlots[1]);
            AddDegenerateFinToLoopAndVertex(apexLoop, faceSlots[1], body.FirstVertexBody);
        }
        else
        {
            AddLoopWithEdges(faceSlots[0], edgeSlots[0], 1);
            AddLoopWithEdges(faceSlots[0], edgeSlots[1], 1);
            AddLoopWithEdges(faceSlots[1], edgeSlots, 1);
            AddLoopWithEdges(faceSlots[2], edgeSlots[1], 1);
        }

        for (int f = 0; f < faceCount; f++)
        {
            if (AddFaceUse(solidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_negative_c) < 0) return ParasolidConstants.PK_ERROR_memory_full;
            if (AddFaceUse(voidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_positive_c) < 0) return ParasolidConstants.PK_ERROR_memory_full;
        }

        return FinishCreatedBody(bodySlot, bodyTag);
    }

    private static int CreateSolidPrismCore(double radius, double height, int nSides, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (nSides > 1024)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        var bodySlot = TryAllocateBodies();
        if (bodySlot < 0) return -1;
        ref var body = ref Bodies[bodySlot];
        InitializeBody(ref body);
        AssignPartition(ref body.Header, CurrentPartition);

        if (!CreateSolidRegionsAndShells(bodySlot, out var voidShellSlot, out var solidShellSlot))
            return ParasolidConstants.PK_ERROR_memory_full;
        ReadAxis2(basisSet, out double ox, out double oy, out double oz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ);
        Cross(axX, axY, axZ, refX, refY, refZ, out double yX, out double yY, out double yZ);

        Span<double> px = stackalloc double[nSides * 2];
        Span<double> py = stackalloc double[nSides * 2];
        Span<double> pz = stackalloc double[nSides * 2];
        for (int i = 0; i < nSides; i++)
        {
            var angle = Math.Tau * i / nSides;
            var cx = Math.Cos(angle) * radius;
            var cy = Math.Sin(angle) * radius;
            px[i] = ox + cx * refX + cy * yX;
            py[i] = oy + cx * refY + cy * yY;
            pz[i] = oz + cx * refZ + cy * yZ;
            px[i + nSides] = px[i] + height * axX;
            py[i + nSides] = py[i] + height * axY;
            pz[i + nSides] = pz[i] + height * axZ;
        }

        Span<int> vertexSlots = stackalloc int[nSides * 2];
        for (int i = 0; i < vertexSlots.Length; i++)
        {
            if (!AddVertex(bodySlot, px[i], py[i], pz[i], out vertexSlots[i]))
                return ParasolidConstants.PK_ERROR_general_body;
        }

        Span<int> edgeSlots = stackalloc int[nSides * 3];
        for (int i = 0; i < nSides; i++)
        {
            if (!AddLineEdge(bodySlot, vertexSlots[i], vertexSlots[(i + 1) % nSides], px[i], py[i], pz[i], px[(i + 1) % nSides], py[(i + 1) % nSides], pz[(i + 1) % nSides], out edgeSlots[i]))
                return ParasolidConstants.PK_ERROR_general_body;
            if (!AddLineEdge(bodySlot, vertexSlots[i + nSides], vertexSlots[((i + 1) % nSides) + nSides], px[i + nSides], py[i + nSides], pz[i + nSides], px[((i + 1) % nSides) + nSides], py[((i + 1) % nSides) + nSides], pz[((i + 1) % nSides) + nSides], out edgeSlots[i + nSides]))
                return ParasolidConstants.PK_ERROR_general_body;
            if (!AddLineEdge(bodySlot, vertexSlots[i], vertexSlots[i + nSides], px[i], py[i], pz[i], px[i + nSides], py[i + nSides], pz[i + nSides], out edgeSlots[i + nSides * 2]))
                return ParasolidConstants.PK_ERROR_general_body;
        }

        Span<int> faceSlots = stackalloc int[nSides + 2];
        int bottomSurf = CreatePlaneSurfaceTag(ox, oy, oz, -axX, -axY, -axZ, refX, refY, refZ);
        int topSurf = CreatePlaneSurfaceTag(ox + height * axX, oy + height * axY, oz + height * axZ, axX, axY, axZ, refX, refY, refZ);
        if (bottomSurf <= 0 || topSurf <= 0)
            return ParasolidConstants.PK_ERROR_general_body;
        faceSlots[0] = AddFace(bodySlot, bottomSurf);
        faceSlots[1] = AddFace(bodySlot, topSurf);
        AddLoopWithEdges(faceSlots[0], edgeSlots, nSides);
        AddLoopWithEdges(faceSlots[1], edgeSlots[nSides..], nSides);

        Span<int> sideEdges = stackalloc int[4];
        for (int i = 0; i < nSides; i++)
        {
            int next = (i + 1) % nSides;
            Cross(px[next] - px[i], py[next] - py[i], pz[next] - pz[i], axX, axY, axZ, out double normalX, out double normalY, out double normalZ);
            var sideSurf = CreatePlaneSurfaceTag(px[i], py[i], pz[i], normalX, normalY, normalZ, axX, axY, axZ);
            if (sideSurf <= 0)
                return ParasolidConstants.PK_ERROR_general_body;
            faceSlots[i + 2] = AddFace(bodySlot, sideSurf);

            sideEdges[0] = edgeSlots[i];
            sideEdges[1] = edgeSlots[nSides * 2 + next];
            sideEdges[2] = edgeSlots[nSides + i];
            sideEdges[3] = edgeSlots[nSides * 2 + i];
            AddLoopWithEdges(faceSlots[i + 2], sideEdges, 4);
        }

        for (int f = 0; f < faceSlots.Length; f++)
        {
            if (AddFaceUse(solidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_negative_c) < 0) return ParasolidConstants.PK_ERROR_memory_full;
            if (AddFaceUse(voidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_positive_c) < 0) return ParasolidConstants.PK_ERROR_memory_full;
        }

        return FinishCreatedBody(bodySlot, bodyTag);
    }

    private static int CreateSingleFaceSolidCore(CreatePrimitiveKind kind, double value0, double value1, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        var bodySlot = TryAllocateBodies();
        if (bodySlot < 0) return -1;
        ref var body = ref Bodies[bodySlot];
        InitializeBody(ref body);
        AssignPartition(ref body.Header, CurrentPartition);

        if (!CreateSolidRegionsAndShells(bodySlot, out var voidShellSlot, out var solidShellSlot))
            return ParasolidConstants.PK_ERROR_memory_full;
        ReadAxis2(basisSet, out double ox, out double oy, out double oz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ);

        var surfTag = kind == CreatePrimitiveKind.Sphere
            ? CreateSphereSurfaceTag(ox, oy, oz, axX, axY, axZ, refX, refY, refZ, value0)
            : CreateTorusSurfaceTag(ox, oy, oz, axX, axY, axZ, refX, refY, refZ, value0, value1);
        if (surfTag <= 0)
            return ParasolidConstants.PK_ERROR_general_body;

        var faceSlot = AddFace(bodySlot, surfTag);
        if (AddFaceUse(solidShellSlot, faceSlot, ParasolidConstants.PK_TOPOL_sense_negative_c) < 0) return ParasolidConstants.PK_ERROR_memory_full;
        if (AddFaceUse(voidShellSlot, faceSlot, ParasolidConstants.PK_TOPOL_sense_positive_c) < 0) return ParasolidConstants.PK_ERROR_memory_full;
        return FinishCreatedBody(bodySlot, bodyTag);
    }

    // ── Mark / Rollback (undo log based) ──────────────────────

    private static int MarkCreateImplementation(int* mark)
    {
        if (mark is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        var session = State.Session;
        if (session->IsMarkActive != 0)
            return ParasolidConstants.PK_ERROR_rollback_started; // single mark by design

        session->ClearUndo();
        session->MarkSequence++;
        session->SetMarkActive(1);
        session->MarkCurrentPartition = ThreadContext()->CurrentPartition;
        for (var i = 0; i < session->PartitionHighWater; i++)
            if (session->Partitions[i].Alive != 0)
            {
                session->Partitions[i].AtPmark = 1;
                session->Partitions[i].MarkHasEntities = session->Tags.HasLiveEntities(i) ? (byte)1 : (byte)0;
            }
        *mark = session->MarkSequence;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int MarkGotoImplementation(int mark)
    {
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var session = State.Session;
        if (session->IsMarkActive == 0 || session->MarkSequence != mark)
            return ParasolidConstants.PK_ERROR_bad_mark;

        RollbackMark(session);
        ThreadContext()->CurrentPartition = session->MarkCurrentPartition;
        // Other threads must not retain references to partitions removed by goto.
        for (var i = 0; i < session->ThreadCount; i++)
            if (!session->TryFindPartition(session->Threads[i].CurrentPartition, out _))
                session->Threads[i].CurrentPartition = session->DefaultPartition;
        session->SetMarkActive(0);
        session->ClearUndo();
        session->ClearDeferred();
        for (var i = 0; i < session->PartitionHighWater; i++)
            if (session->Partitions[i].Alive != 0) session->Partitions[i].AtPmark = 1;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int MarkDeleteImplementation(int mark)
    {
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var session = State.Session;
        if (session->IsMarkActive == 0 || session->MarkSequence != mark)
            return ParasolidConstants.PK_ERROR_bad_mark;

        // Keep the modifications, release deferred objects and the log.
        for (var thread = 0; thread < session->ThreadCount; thread++)
        {
            var context = session->Threads.Pointer(thread);
            for (int i = 0; i < context->DeferredCount; i++)
            {
                ref var deferred = ref context->Deferred[i];
                FinalReleaseEntity((PoolKind)deferred.Pool, deferred.Slot, deferred.Tag);
            }
        }
        session->SetMarkActive(0);
        session->ClearUndo();
        session->ClearDeferred();
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static void RollbackMark(SessionData* session)
    {
        if (session->ThreadCount == 1)
        {
            var context = session->Threads.Pointer(0);
            for (var i = context->UndoEntryCount - 1; i >= 0; i--)
                ReplayMarkEntry(session, ref context->UndoEntries[i]);
            return;
        }
        // Global reverse-chronological replay: destroy created entities,
        // restore deleted entities and field snapshots, swap back blocks.
        // A global order (not per-partition chains) keeps the two undo
        // entries of one entity — recorded before and after its partition
        // assignment propagates — adjacent and correctly ordered.
        for (var thread = 0; thread < session->ThreadCount; thread++)
            session->Threads[thread].ReplayPosition = session->Threads[thread].UndoEntryCount - 1;
        while (true)
        {
            var selected = -1;
            var sequence = -1;
            for (var thread = 0; thread < session->ThreadCount; thread++)
            {
                var index = session->Threads[thread].ReplayPosition;
                if (index >= 0 && session->Threads[thread].UndoEntries[index].Sequence > sequence)
                { selected = thread; sequence = session->Threads[thread].UndoEntries[index].Sequence; }
            }
            if (selected < 0) break;
            ref var entry = ref session->Threads[selected].UndoEntries[session->Threads[selected].ReplayPosition--];
            ReplayMarkEntry(session, ref entry);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReplayMarkEntry(SessionData* session, ref SessionData.UndoEntry entry, bool commandFailure = false)
    {
        switch (entry.Kind)
        {
            case SessionData.UndoKind.EntityCreated:
                DestroyCreatedEntity((PoolKind)entry.Pool, entry.Slot, entry.Generation, (RecordHeader*)entry.Data);
                break;
            case SessionData.UndoKind.EntityDeleted:
                RestoreDeletedEntity(session, (PoolKind)entry.Pool, entry.Slot, entry.Tag);
                break;
            case SessionData.UndoKind.FieldSnapshot:
                Buffer.MemoryCopy(entry.Data, entry.Target, entry.DataBytes, entry.DataBytes);
                break;
            case SessionData.UndoKind.PartitionCreated:
                session->Partitions[entry.Slot].Alive = 0;
                session->PartitionCount--;
                break;
            case SessionData.UndoKind.PartitionDeleted:
                session->Partitions[entry.Slot].Alive = 1;
                session->PartitionCount++;
                break;
            case SessionData.UndoKind.CurrentPartitionChanged:
                if (commandFailure) ((SessionData.ThreadContext*)entry.Data)->CurrentPartition = entry.Slot;
                break;
            case SessionData.UndoKind.BodyUnlinked:
                ref var body = ref Bodies[entry.Slot];
                ref var partition = ref session->Partitions[entry.Partition];
                body.PrevInPartition = entry.PreviousBody;
                body.NextInPartition = entry.FollowingBody;
                Bodies[entry.PreviousBody].NextInPartition = entry.Slot;
                Bodies[entry.FollowingBody].PrevInPartition = entry.Slot;
                partition.FirstBody = entry.FirstBody;
                partition.LastBody = entry.LastBody;
                partition.BodyCount++;
                break;
            case SessionData.UndoKind.GeometryReferenceReleased:
                AcquireGeometryReference((PoolKind)entry.Pool, entry.Slot);
                break;
            case SessionData.UndoKind.PartitionModified:
                session->Partitions[entry.Slot].AtPmark = (byte)entry.Generation;
                break;
            case SessionData.UndoKind.BlockReplaced:
                RollbackBlockSwap(entry.Pool, entry.Slot, entry.Data);
                break;
        }
    }

    // ── Entity delete (ownership cascade) ──────────────────────

    private static int EntityDeleteImplementation(int nEntities, int* entities)
    {
        if (entities is null || nEntities <= 0)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        var session = State.Session;
        long deleteEntries = 0;
        TagMap.TagRecord firstRecord = default;
        for (var i = 0; i < nEntities; i++)
        {
            if (!session->Tags.TryResolve(entities[i], CurrentSessionId, out var handle))
                return ParasolidConstants.PK_ERROR_unknown_class;
            var partition = PoolPartitionOf((PoolKind)handle.Pool, handle.Slot);
            var owner = session->Partitions[partition].LockOwnerThread;
            if (owner != 0 && owner != ThreadContext()->ManagedThreadId)
                return ParasolidConstants.PK_ERROR_bad_thread;
            var pool = (PoolKind)handle.Pool;
            if (pool is PoolKind.Face or PoolKind.Edge or PoolKind.Vertex or PoolKind.Loop or PoolKind.Fin
                or PoolKind.Shell or PoolKind.Region or PoolKind.FaceUse
                || (pool is PoolKind.Curve or PoolKind.Surface or PoolKind.Point && GeometryOwnerCount(pool, handle.Slot) != 0))
                return ParasolidConstants.PK_ERROR_is_attached;
            for (var j = 0; j < i; j++)
                if (entities[j] == entities[i]) return ParasolidConstants.PK_ERROR_bad_value;
            if (i == 0) firstRecord = handle;
            if (DeferDeletes)
                deleteEntries += ((PoolKind)handle.Pool == PoolKind.Body ? BodyDeletionEntries(handle.Slot) : 1) + 1;
        }
        if (DeferDeletes && (deleteEntries > int.MaxValue || !session->TryReserveDeletion((int)deleteEntries)))
            return ParasolidConstants.PK_ERROR_memory_full;
        for (int i = 0; i < nEntities; i++)
        {
            var tag = entities[i];
            var record = firstRecord;
            if (i != 0 && !session->Tags.TryResolve(tag, CurrentSessionId, out record))
                return ParasolidConstants.PK_ERROR_unknown_class;
            var pool = (PoolKind)record.Pool;
            var slot = record.Slot;
            var partition = PoolPartitionOf(pool, slot);
            if (DeferDeletes && session->Partitions[partition].AtPmark != 0)
                session->TryAppendUndo(SessionData.UndoKind.PartitionModified, 0, partition, partition, session->Partitions[partition].AtPmark, 0, null, 0);
            session->Partitions[partition].AtPmark = 0;

            if (pool == PoolKind.Body)
            {
                DeleteBodyCascade(session, slot, tag);
            }
            else
            {
                DeleteEntitySingle(session, pool, slot, tag);
            }
        }

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static long BodyDeletionEntries(BodySlot slot)
    {
        ref var body = ref Bodies[slot];
        long count = 2L + body.RegionCount + body.ShellCount + 2L * body.FaceCountBody
            + 2L * body.EdgeCountBody + 2L * body.VertexCountBody;
        var face = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, face = Faces[face].NextInBody)
        {
            count += Faces[face].LoopCount;
            var loop = Faces[face].FirstLoop;
            for (var j = 0; j < Faces[face].LoopCount; j++, loop = Loops[loop].NextInFace)
                count += Loops[loop].FinCount;
        }
        var shell = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, shell = Shells[shell].NextInBody)
            count += Shells[shell].FaceUseCount;
        return count;
    }

    /// <summary>
    /// Body delete: reclaim owned regions, shells, face uses, faces, loops,
    /// fins, edges, vertices and exclusively owned geometry, and unlink the
    /// body from its partition chain.
    /// </summary>
    private static void DeleteBodyCascade(SessionData* session, BodySlot bodySlot, EntityTag bodyTag)
    {
        ref var body = ref Bodies[bodySlot];
        var partition = body.Header.Partition;

        if (DeferDeletes)
        {
            session->TryAppendUndo(SessionData.UndoKind.BodyUnlinked, 0, partition, bodySlot, 0, 0, null, 0);
            ref var entry = ref session->UndoEntries[session->UndoEntryCount - 1];
            entry.PreviousBody = body.PrevInPartition;
            entry.FollowingBody = body.NextInPartition;
            entry.FirstBody = session->Partitions[partition].FirstBody;
            entry.LastBody = session->Partitions[partition].LastBody;
        }

        // Faces (with loops and fins), edges, vertices — body flat chains.
        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++)
        {
            var next = Faces[faceSlot].NextInBody;
            var face = Faces[faceSlot];
            var loopSlot = face.FirstLoop;
            for (var j = 0; j < face.LoopCount; j++)
            {
                var nextLoop = Loops[loopSlot].NextInFace;
                var loop = Loops[loopSlot];
                var finSlot = loop.FirstFin;
                for (var k = 0; k < loop.FinCount; k++)
                {
                    var nextFin = Fins[finSlot].NextInLoop;
                    DeleteEntitySingle(session, PoolKind.Fin, finSlot, TagOf(PoolKind.Fin, finSlot));
                    finSlot = nextFin;
                }
                DeleteEntitySingle(session, PoolKind.Loop, loopSlot, TagOf(PoolKind.Loop, loopSlot));
                loopSlot = nextLoop;
            }
            // Face-owned surface (when exclusively owned by this face).
            if (face.SurfTag > 0)
                ReleaseGeometryOwner(session, EntityClass.Surface, face.SurfTag);
            DeleteEntitySingle(session, PoolKind.Face, faceSlot, TagOf(PoolKind.Face, faceSlot));
            faceSlot = next;
        }

        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++)
        {
            var next = Edges[edgeSlot].NextInBody;
            if (Edges[edgeSlot].CurveTag > 0)
                ReleaseGeometryOwner(session, EntityClass.Curve, Edges[edgeSlot].CurveTag);
            DeleteEntitySingle(session, PoolKind.Edge, edgeSlot, TagOf(PoolKind.Edge, edgeSlot));
            edgeSlot = next;
        }

        var vertexSlot = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++)
        {
            var next = Vertices[vertexSlot].NextInBody;
            if (Vertices[vertexSlot].PointTag > 0)
                ReleaseGeometryOwner(session, EntityClass.Point, Vertices[vertexSlot].PointTag);
            DeleteEntitySingle(session, PoolKind.Vertex, vertexSlot, TagOf(PoolKind.Vertex, vertexSlot));
            vertexSlot = next;
        }

        // Shells (with face uses) and regions.
        var shellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++)
        {
            var next = Shells[shellSlot].NextInBody;
            var shell = Shells[shellSlot];
            var faceUseSlot = shell.FirstFaceUseShell;
            for (var j = 0; j < shell.FaceUseCount; j++)
            {
                var nextUse = FaceUses[faceUseSlot].NextInShell;
                DeleteEntitySingle(session, PoolKind.FaceUse, faceUseSlot, 0);
                faceUseSlot = nextUse;
            }
            DeleteEntitySingle(session, PoolKind.Shell, shellSlot, TagOf(PoolKind.Shell, shellSlot));
            shellSlot = next;
        }
        var regionSlot = body.FirstRegion;
        for (var i = 0; i < body.RegionCount; i++)
        {
            var next = Regions[regionSlot].NextInBody;
            DeleteEntitySingle(session, PoolKind.Region, regionSlot, TagOf(PoolKind.Region, regionSlot));
            regionSlot = next;
        }

        // Unlink from partition body chain.
        UnlinkBodyFromPartition(partition, bodySlot);
        if (!DeferDeletes) SessionMemoryOwner.DropBodyXt(bodySlot);
        DeleteEntitySingle(session, PoolKind.Body, bodySlot, bodyTag);
    }

    private static void UnlinkBodyFromPartition(PartitionSlot partition, BodySlot bodySlot)
    {
        var session = State.Session;
        var slot = partition >= 0 ? FindPartitionSlot(partition) : 0;
        ref var body = ref Bodies[bodySlot];
        if (body.PrevInPartition < 0 && body.NextInPartition < 0)
            return;                               // not attached (undo path)
        if (body.PrevInPartition == bodySlot)
        {
            session->Partitions[slot].FirstBody = -1;
            session->Partitions[slot].LastBody = -1;
        }
        else
        {
            Bodies[body.PrevInPartition].NextInPartition = body.NextInPartition;
            Bodies[body.NextInPartition].PrevInPartition = body.PrevInPartition;
            if (session->Partitions[slot].FirstBody == bodySlot)
                session->Partitions[slot].FirstBody = body.NextInPartition;
            if (session->Partitions[slot].LastBody == bodySlot)
                session->Partitions[slot].LastBody = body.PrevInPartition;
        }
        session->Partitions[slot].BodyCount--;
        body.PrevInPartition = -1;
        body.NextInPartition = -1;
    }

    private static int FindPartitionSlot(PartitionSlot partitionId)
    {
        var session = State.Session;
        return session->TryFindPartition(partitionId, out var slot) ? slot : 0;
    }

    /// <summary>Geometry (curve/surface/point) ownership release: refcount-1, free at zero.</summary>
    private static void ReleaseGeometryOwner(SessionData* session, EntityClass @class, EntityTag tag)
    {
        if (!session->Tags.TryResolve(tag, CurrentSessionId, out var record) || (EntityClass)record.ClassCode != @class)
            return;
        var pool = (PoolKind)record.Pool;
        var slot = record.Slot;
        if (!TryReleaseGeometryReference(pool, slot))
            return;                                // still referenced by another owner
        DeleteEntitySingle(session, pool, slot, tag);
    }

    /// <summary>Decrement a geometry record's owner count; true when it reaches zero.</summary>
    private static bool TryReleaseGeometryReference(PoolKind pool, int slot)
    {
        if (GeometryOwnerCount(pool, slot) <= 1) return true;
        if (DeferDeletes)
            State.Session->TryAppendUndo(SessionData.UndoKind.GeometryReferenceReleased, (byte)pool,
                PoolPartitionOf(pool, slot), slot, 0, 0, null, 0);
        switch (pool)
        {
            case PoolKind.Curve: Curves[slot].OwnerCount--; break;
            case PoolKind.Surface: Surfaces[slot].OwnerCount--; break;
            case PoolKind.Point: Points[slot].OwnerCount--; break;
        }
        return false;
    }

    /// <summary>Acquire a geometry ownership reference (shared geometry across a partition).</summary>
    private static void AcquireGeometryReference(PoolKind pool, int slot)
    {
        switch (pool)
        {
            case PoolKind.Curve: Curves[slot].OwnerCount++; break;
            case PoolKind.Surface: Surfaces[slot].OwnerCount++; break;
            case PoolKind.Point: Points[slot].OwnerCount++; break;
        }
    }

    private static void DeleteEntitySingle(SessionData* session, PoolKind pool, int slot, EntityTag tag)
    {
        if (DeferDeletes)
        {
            // Snapshot for rollback, then defer the physical release.
            session->TryAppendUndo(SessionData.UndoKind.EntityDeleted, (byte)pool,
                PoolPartitionOf(pool, slot), slot, PoolGenerationOf(pool, slot), tag, null, 0);
            RetireInPool(pool, slot);
            session->TryDeferRelease((byte)pool, slot, tag);
            if (tag > 0) session->Tags.Suspend(tag);
        }
        else
        {
            FinalReleaseEntity(pool, slot, tag, knownAlive: true);
        }
    }

    private static bool DeferDeletes => cachedThreadContext->DeferCommandDeletes != 0;

    private static void FinalReleaseEntity(PoolKind pool, int slot, EntityTag tag, bool knownAlive = false)
    {
        // Variable-length and typed geometry payloads die with their owner
        // record, whatever path releases it.
        if (pool == PoolKind.Curve || pool == PoolKind.Surface)
            ReleaseGeometryPayload(pool, slot);
        if (pool == PoolKind.Body && Bodies.IsAlive(slot))
            UnlinkBodyFromPartition(Bodies[slot].Header.Partition, slot);
        if (pool == PoolKind.Body) SessionMemoryOwner.DropBodyXt(slot);
        // Slots retired under an active mark come back with Alive == 0; both
        // freshly dead and retired slots end up on the free chain.
        if (knownAlive || IsSlotAlive(pool, slot))
            ReleasePoolSlot(pool, slot);
        else
            RecycleRetiredSlot(pool, slot);
        if (tag > 0) State.Session->Tags.Revoke(tag);
    }

    /// <summary>Free the typed data record owned by a curve or surface.</summary>
    private static void ReleaseGeometryPayload(PoolKind pool, int slot)
    {
        if (pool == PoolKind.Curve)
        {
            var curve = Curves[slot];
            switch (curve.Class)
            {
                case CurveClass.BCurve: FreeBCurveData(curve.DataIndex); break;
                case CurveClass.Line: LineDataPool.Free(curve.DataIndex); break;
                case CurveClass.Circle: CircleDataPool.Free(curve.DataIndex); break;
            }
        }
        else if (pool == PoolKind.Surface)
        {
            var surface = Surfaces[slot];
            switch (surface.Class)
            {
                case SurfaceClass.Plane: PlaneDataPool.Free(surface.DataIndex); break;
                case SurfaceClass.Cylinder: CylinderDataPool.Free(surface.DataIndex); break;
                case SurfaceClass.Cone: ConeDataPool.Free(surface.DataIndex); break;
                case SurfaceClass.Sphere: SphereDataPool.Free(surface.DataIndex); break;
                case SurfaceClass.Torus: TorusDataPool.Free(surface.DataIndex); break;
            }
        }
    }

    private static bool IsSlotAlive(PoolKind pool, int slot)
    {
        return pool switch
        {
            PoolKind.Point => Points.IsAlive(slot),
            PoolKind.Vector => Vectors.IsAlive(slot),
            PoolKind.Body => Bodies.IsAlive(slot),
            PoolKind.Shell => Shells.IsAlive(slot),
            PoolKind.Face => Faces.IsAlive(slot),
            PoolKind.Loop => Loops.IsAlive(slot),
            PoolKind.Edge => Edges.IsAlive(slot),
            PoolKind.Fin => Fins.IsAlive(slot),
            PoolKind.Vertex => Vertices.IsAlive(slot),
            PoolKind.Region => Regions.IsAlive(slot),
            PoolKind.Curve => Curves.IsAlive(slot),
            PoolKind.Surface => Surfaces.IsAlive(slot),
            PoolKind.Transform => Transforms.IsAlive(slot),
            PoolKind.FaceUse => FaceUses.IsAlive(slot),
            PoolKind.BCurveData => BCurveDataStore.IsAlive(slot),
            PoolKind.TorusData => TorusDataPool.IsAlive(slot),
            PoolKind.SphereData => SphereDataPool.IsAlive(slot),
            PoolKind.ConeData => ConeDataPool.IsAlive(slot),
            PoolKind.PlaneData => PlaneDataPool.IsAlive(slot),
            PoolKind.CylinderData => CylinderDataPool.IsAlive(slot),
            PoolKind.LineData => LineDataPool.IsAlive(slot),
            PoolKind.CircleData => CircleDataPool.IsAlive(slot),
            _ => false,
        };
    }

    private static void RecycleRetiredSlot(PoolKind pool, int slot)
    {
        switch (pool)
        {
            case PoolKind.Point: Points.RecycleRetired(slot); break;
            case PoolKind.Vector: Vectors.RecycleRetired(slot); break;
            case PoolKind.Body: Bodies.RecycleRetired(slot); break;
            case PoolKind.Shell: Shells.RecycleRetired(slot); break;
            case PoolKind.FaceUse: FaceUses.RecycleRetired(slot); break;
            case PoolKind.Face: Faces.RecycleRetired(slot); break;
            case PoolKind.Loop: Loops.RecycleRetired(slot); break;
            case PoolKind.Edge: Edges.RecycleRetired(slot); break;
            case PoolKind.Fin: Fins.RecycleRetired(slot); break;
            case PoolKind.Vertex: Vertices.RecycleRetired(slot); break;
            case PoolKind.Region: Regions.RecycleRetired(slot); break;
            case PoolKind.Curve: Curves.RecycleRetired(slot); break;
            case PoolKind.Surface: Surfaces.RecycleRetired(slot); break;
            case PoolKind.Transform: Transforms.RecycleRetired(slot); break;
            case PoolKind.BCurveData: BCurveDataStore.RecycleRetired(slot); break;
            case PoolKind.TorusData: TorusDataPool.RecycleRetired(slot); break;
            case PoolKind.SphereData: SphereDataPool.RecycleRetired(slot); break;
            case PoolKind.ConeData: ConeDataPool.RecycleRetired(slot); break;
            case PoolKind.PlaneData: PlaneDataPool.RecycleRetired(slot); break;
            case PoolKind.CylinderData: CylinderDataPool.RecycleRetired(slot); break;
            case PoolKind.LineData: LineDataPool.RecycleRetired(slot); break;
            case PoolKind.CircleData: CircleDataPool.RecycleRetired(slot); break;
        }
    }

    private static void DestroyCreatedEntity(PoolKind pool, int slot, EntityGeneration generation, RecordHeader* header)
    {
        if (header == null || header->Alive == 0 || header->Generation != generation) return;
        FinalReleaseEntity(pool, slot, header->Tag, knownAlive: true);
    }

    private static void RestoreDeletedEntity(SessionData* session, PoolKind pool, int slot, EntityTag tag)
    {
        RevivePoolSlot(pool, slot);
        if (tag > 0) session->Tags.Restore(tag);
    }

    private static void RollbackBlockSwap(byte pool, int slot, void* oldBlock)
    {
        // Variable-length data restore is BCurve-specific today; the BCurve
        // store holds the owner record whose handle points at the new block.
        RollbackBCurveBlock(slot, oldBlock);
    }

    private static int PoolGenerationOf(PoolKind pool, int slot)
    {
        return pool switch
        {
            PoolKind.Point => Points.GetGeneration(slot),
            PoolKind.Vector => Vectors.GetGeneration(slot),
            PoolKind.Body => Bodies.GetGeneration(slot),
            PoolKind.Shell => Shells.GetGeneration(slot),
            PoolKind.Face => Faces.GetGeneration(slot),
            PoolKind.Loop => Loops.GetGeneration(slot),
            PoolKind.Edge => Edges.GetGeneration(slot),
            PoolKind.Fin => Fins.GetGeneration(slot),
            PoolKind.Vertex => Vertices.GetGeneration(slot),
            PoolKind.Region => Regions.GetGeneration(slot),
            PoolKind.Curve => Curves.GetGeneration(slot),
            PoolKind.Surface => Surfaces.GetGeneration(slot),
            PoolKind.Transform => Transforms.GetGeneration(slot),
            PoolKind.BCurveData => BCurveDataStore.GetGeneration(slot),
            PoolKind.TorusData => TorusDataPool.GetGeneration(slot),
            PoolKind.SphereData => SphereDataPool.GetGeneration(slot),
            PoolKind.ConeData => ConeDataPool.GetGeneration(slot),
            PoolKind.PlaneData => PlaneDataPool.GetGeneration(slot),
            PoolKind.CylinderData => CylinderDataPool.GetGeneration(slot),
            PoolKind.LineData => LineDataPool.GetGeneration(slot),
            PoolKind.CircleData => CircleDataPool.GetGeneration(slot),
            PoolKind.FaceUse => FaceUses.GetGeneration(slot),
            _ => 0,
        };
    }

    private static void ReleasePoolSlot(PoolKind pool, int slot)
    {
        switch (pool)
        {
            case PoolKind.Point: Points.Free(slot); break;
            case PoolKind.Vector: Vectors.Free(slot); break;
            case PoolKind.Body: Bodies.Free(slot); break;
            case PoolKind.Shell: Shells.Free(slot); break;
            case PoolKind.FaceUse: FaceUses.Free(slot); break;
            case PoolKind.Face: Faces.Free(slot); break;
            case PoolKind.Loop: Loops.Free(slot); break;
            case PoolKind.Edge: Edges.Free(slot); break;
            case PoolKind.Fin: Fins.Free(slot); break;
            case PoolKind.Vertex: Vertices.Free(slot); break;
            case PoolKind.Region: Regions.Free(slot); break;
            case PoolKind.Curve: Curves.Free(slot); break;
            case PoolKind.Surface: Surfaces.Free(slot); break;
            case PoolKind.Transform: Transforms.Free(slot); break;
            case PoolKind.BCurveData: BCurveDataStore.Free(slot); break;
            case PoolKind.TorusData: TorusDataPool.Free(slot); break;
            case PoolKind.SphereData: SphereDataPool.Free(slot); break;
            case PoolKind.ConeData: ConeDataPool.Free(slot); break;
            case PoolKind.PlaneData: PlaneDataPool.Free(slot); break;
            case PoolKind.CylinderData: CylinderDataPool.Free(slot); break;
            case PoolKind.LineData: LineDataPool.Free(slot); break;
            case PoolKind.CircleData: CircleDataPool.Free(slot); break;
        }
    }

    private static void RetireInPool(PoolKind pool, int slot)
    {
        switch (pool)
        {
            case PoolKind.Point: Points.Retire(slot); break;
            case PoolKind.Vector: Vectors.Retire(slot); break;
            case PoolKind.Body: Bodies.Retire(slot); break;
            case PoolKind.Shell: Shells.Retire(slot); break;
            case PoolKind.FaceUse: FaceUses.Retire(slot); break;
            case PoolKind.Face: Faces.Retire(slot); break;
            case PoolKind.Loop: Loops.Retire(slot); break;
            case PoolKind.Edge: Edges.Retire(slot); break;
            case PoolKind.Fin: Fins.Retire(slot); break;
            case PoolKind.Vertex: Vertices.Retire(slot); break;
            case PoolKind.Region: Regions.Retire(slot); break;
            case PoolKind.Curve: Curves.Retire(slot); break;
            case PoolKind.Surface: Surfaces.Retire(slot); break;
            case PoolKind.Transform: Transforms.Retire(slot); break;
            case PoolKind.BCurveData: BCurveDataStore.Retire(slot); break;
            case PoolKind.TorusData: TorusDataPool.Retire(slot); break;
            case PoolKind.SphereData: SphereDataPool.Retire(slot); break;
            case PoolKind.ConeData: ConeDataPool.Retire(slot); break;
            case PoolKind.PlaneData: PlaneDataPool.Retire(slot); break;
            case PoolKind.CylinderData: CylinderDataPool.Retire(slot); break;
            case PoolKind.LineData: LineDataPool.Retire(slot); break;
            case PoolKind.CircleData: CircleDataPool.Retire(slot); break;
        }
    }

    private static void RevivePoolSlot(PoolKind pool, int slot)
    {
        switch (pool)
        {
            case PoolKind.Point: Points.MarkAlive(slot); break;
            case PoolKind.Vector: Vectors.MarkAlive(slot); break;
            case PoolKind.Body: Bodies.MarkAlive(slot); break;
            case PoolKind.Shell: Shells.MarkAlive(slot); break;
            case PoolKind.FaceUse: FaceUses.MarkAlive(slot); break;
            case PoolKind.Face: Faces.MarkAlive(slot); break;
            case PoolKind.Loop: Loops.MarkAlive(slot); break;
            case PoolKind.Edge: Edges.MarkAlive(slot); break;
            case PoolKind.Fin: Fins.MarkAlive(slot); break;
            case PoolKind.Vertex: Vertices.MarkAlive(slot); break;
            case PoolKind.Region: Regions.MarkAlive(slot); break;
            case PoolKind.Curve: Curves.MarkAlive(slot); break;
            case PoolKind.Surface: Surfaces.MarkAlive(slot); break;
            case PoolKind.Transform: Transforms.MarkAlive(slot); break;
            case PoolKind.BCurveData: BCurveDataStore.MarkAlive(slot); break;
            case PoolKind.TorusData: TorusDataPool.MarkAlive(slot); break;
            case PoolKind.SphereData: SphereDataPool.MarkAlive(slot); break;
            case PoolKind.ConeData: ConeDataPool.MarkAlive(slot); break;
            case PoolKind.PlaneData: PlaneDataPool.MarkAlive(slot); break;
            case PoolKind.CylinderData: CylinderDataPool.MarkAlive(slot); break;
            case PoolKind.LineData: LineDataPool.MarkAlive(slot); break;
            case PoolKind.CircleData: CircleDataPool.MarkAlive(slot); break;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Allocate a tag for an existing entity slot (for query result arrays).
    /// Does not allocate a new pool slot — just creates a tag pointing to an existing one.
    /// </summary>
    private static int AllocateEntityTag(EntityClass entityClass, PoolKind pool, int slotIndex)
    {
        int generation = pool switch
        {
            PoolKind.Point => Points.GetGeneration(slotIndex),
            PoolKind.Vector => Vectors.GetGeneration(slotIndex),
            PoolKind.Body => Bodies.GetGeneration(slotIndex),
            PoolKind.Shell => Shells.GetGeneration(slotIndex),
            PoolKind.Face => Faces.GetGeneration(slotIndex),
            PoolKind.Loop => Loops.GetGeneration(slotIndex),
            PoolKind.Edge => Edges.GetGeneration(slotIndex),
            PoolKind.Fin => Fins.GetGeneration(slotIndex),
            PoolKind.Vertex => Vertices.GetGeneration(slotIndex),
            PoolKind.Region => Regions.GetGeneration(slotIndex),
            PoolKind.Curve => Curves.GetGeneration(slotIndex),
            PoolKind.Surface => Surfaces.GetGeneration(slotIndex),
            PoolKind.Transform => Transforms.GetGeneration(slotIndex),
            _ => 0,
        };
        return AllocateTag(entityClass, pool, slotIndex, generation);
    }

    private static void InitializeBody(ref BodyRecord body)
    {
        body.BodyType = ParasolidConstants.PK_BODY_type_solid_c;
        body.BodyConfig = ParasolidConstants.PK_BODY_config_standard_c;
        body.FirstShell = -1;
        body.LastShell = -1;
        body.ShellCount = 0;
        body.FirstRegion = -1;
        body.LastRegion = -1;
        body.RegionCount = 0;
        body.FirstFaceBody = -1;
        body.LastFaceBody = -1;
        body.FaceCountBody = 0;
        body.FirstEdgeBody = -1;
        body.LastEdgeBody = -1;
        body.EdgeCountBody = 0;
        body.FirstVertexBody = -1;
        body.LastVertexBody = -1;
        body.VertexCountBody = 0;
        body.FirstConstructionSurface = -1;
        body.LastConstructionSurface = -1;
        body.ConstructionSurfaceCount = 0;
        body.FirstConstructionCurve = -1;
        body.LastConstructionCurve = -1;
        body.ConstructionCurveCount = 0;
        body.FirstConstructionPoint = -1;
        body.LastConstructionPoint = -1;
        body.ConstructionPointCount = 0;
        body.PrevInPartition = -1;
        body.NextInPartition = -1;
    }

    /// <summary>Calling thread's current partition (PK_PARTITION_set_current); default 0.</summary>
    private static PartitionSlot CurrentPartition
    {
        get
        {
            var session = State.Session;
            if (session == null) return 0;
            var context = ThreadContext();
            return context != null ? context->CurrentPartition : 0;
        }
    }

    private static void AssignPartition(ref RecordHeader header, PartitionSlot partition)
    {
        header.Partition = partition;
    }

    internal static PartitionSlot GetEntityPartition(EntityTag entityTag)
    {
        if (!IsValidTag(entityTag))
            return -1;

        var handle = TagRec(entityTag);
        var pool = (PoolKind)handle.Pool;
        var poolSlot = handle.Slot;
        return pool switch
        {
            PoolKind.Point => Points[poolSlot].Header.Partition,
            PoolKind.Vector => Vectors[poolSlot].Header.Partition,
            PoolKind.Body => Bodies[poolSlot].Header.Partition,
            PoolKind.Shell => Shells[poolSlot].Header.Partition,
            PoolKind.Face => Faces[poolSlot].Header.Partition,
            PoolKind.Loop => Loops[poolSlot].Header.Partition,
            PoolKind.Edge => Edges[poolSlot].Header.Partition,
            PoolKind.Fin => Fins[poolSlot].Header.Partition,
            PoolKind.Vertex => Vertices[poolSlot].Header.Partition,
            PoolKind.Region => Regions[poolSlot].Header.Partition,
            PoolKind.Curve => Curves[poolSlot].Header.Partition,
            PoolKind.Surface => Surfaces[poolSlot].Header.Partition,
            PoolKind.Transform => Transforms[poolSlot].Header.Partition,
            _ => -1,
        };
    }

    private static void InitializeShell(ref ShellRecord shell, BodySlot bodySlot)
    {
        shell.Body = bodySlot;
        shell.Region = -1;
        shell.FirstFaceUseShell = -1;
        shell.LastFaceUseShell = -1;
        shell.FaceUseCount = 0;
        shell.AcornVertex = -1;
        shell.PrevInBody = -1;
        shell.NextInBody = -1;
        shell.PrevInRegion = -1;
        shell.NextInRegion = -1;
    }

    private static void InitializeFace(ref FaceRecord face)
    {
        face.BackShell = -1;
        face.FrontShell = -1;
        face.BackFaceUse = -1;
        face.FrontFaceUse = -1;
        face.FirstLoop = -1;
        face.LastLoop = -1;
        face.LoopCount = 0;
        face.SurfTag = 0;
        face.Orientation = ParasolidConstants.PK_TOPOL_sense_none_c;
        face.Tolerance = 0;
        face.PrevOnSurf = -1;
        face.NextOnSurf = -1;
        face.PrevInBody = -1;
        face.NextInBody = -1;
    }

    private static bool CreateSolidRegionsAndShells(BodySlot bodySlot, out ShellSlot voidShellSlot, out ShellSlot solidShellSlot)
    {
        voidShellSlot = solidShellSlot = -1;
        RegionSlot voidRegionSlot = TryAllocateRegions();
        RegionSlot solidRegionSlot = TryAllocateRegions();
        if (voidRegionSlot < 0 || solidRegionSlot < 0) return false;
        ref var voidRegion = ref Regions[voidRegionSlot];
        ref var solidRegion = ref Regions[solidRegionSlot];
        voidRegion.IsSolid = 0;
        voidRegion.FirstShell = -1;
        voidRegion.LastShell = -1;
        voidRegion.ShellCount = 0;
        voidRegion.Frame = -1;
        solidRegion.IsSolid = 1;
        solidRegion.FirstShell = -1;
        solidRegion.LastShell = -1;
        solidRegion.ShellCount = 0;
        solidRegion.Frame = -1;

        AppendRegionToBody(bodySlot, voidRegionSlot);
        AppendRegionToBody(bodySlot, solidRegionSlot);

        voidShellSlot = TryAllocateShells();
        solidShellSlot = TryAllocateShells();
        if (voidShellSlot < 0 || solidShellSlot < 0) return false;
        InitializeShell(ref Shells[voidShellSlot], bodySlot);
        InitializeShell(ref Shells[solidShellSlot], bodySlot);

        AppendShellToBody(bodySlot, voidShellSlot);
        AppendShellToRegion(voidRegionSlot, voidShellSlot);
        AppendShellToBody(bodySlot, solidShellSlot);
        AppendShellToRegion(solidRegionSlot, solidShellSlot);
        return true;
    }

    private static int AddFinToLoopAndEdge(LoopSlot loopSlot, FaceSlot faceSlot, EdgeSlot edgeSlot)
    {
        int finSlot = TryAllocateFins();
        if (finSlot < 0) return -1;
        ref var fin = ref Fins[finSlot];
        fin.Edge = edgeSlot;
        fin.Loop = loopSlot;
        fin.NextInLoop = fin.PrevInLoop = -1;
        fin.Other = -1;
        fin.Curve = -1;
        fin.Vertex = -1;
        fin.NextAtVertex = fin.PrevAtVertex = -1;
        fin.NextOfEdge = fin.PrevOfEdge = -1;
        fin.Sense = '+';

        AppendFinToLoop(loopSlot, finSlot);
        AppendFinToEdge(edgeSlot, finSlot);
        if (Edges[edgeSlot].StartVertex < 0 && Edges[edgeSlot].EndVertex < 0)
            fin.Sense = finSlot == Edges[edgeSlot].FirstFinEdge ? '+' : '-';
        AppendFinToVertex(EdgeFinVertex(finSlot, Edges[edgeSlot]), finSlot);
        return finSlot;
    }

    private static int AddDegenerateFinToLoopAndVertex(LoopSlot loopSlot, FaceSlot faceSlot, VertexSlot vertexSlot)
    {
        var finSlot = TryAllocateFins();
        if (finSlot < 0) return -1;
        ref var fin = ref Fins[finSlot];
        fin.Edge = -1;
        fin.Loop = loopSlot;
        fin.NextInLoop = fin.PrevInLoop = -1;
        fin.Other = -1;
        fin.Curve = -1;
        fin.Vertex = vertexSlot;
        fin.NextAtVertex = fin.PrevAtVertex = -1;
        fin.NextOfEdge = fin.PrevOfEdge = -1;
        fin.Sense = '+';

        AppendFinToLoop(loopSlot, finSlot);
        AppendFinToVertex(vertexSlot, finSlot);
        return finSlot;
    }

    private static FaceSlot AddFace(BodySlot bodySlot, SurfTag surfTag)
    {
        var faceSlot = TryAllocateFaces();
        if (faceSlot < 0) return -1;
        ref var face = ref Faces[faceSlot];
        InitializeFace(ref face);
        face.SurfTag = surfTag;
        AppendFaceToBody(bodySlot, faceSlot);
        return faceSlot;
    }

    private static LoopSlot AddLoop(FaceSlot faceSlot)
    {
        var loopSlot = TryAllocateLoops();
        if (loopSlot < 0) return -1;
        ref var loop = ref Loops[loopSlot];
        loop.Face = faceSlot;
        loop.FirstFin = -1;
        loop.LastFin = -1;
        loop.PrevInFace = -1;
        loop.NextInFace = -1;
        AppendLoopToFace(faceSlot, loopSlot);
        return loopSlot;
    }

    private static void AddLoopWithEdges(FaceSlot faceSlot, ReadOnlySpan<int> edgeSlots, int edgeCount)
    {
        var loopSlot = AddLoop(faceSlot);
        for (int i = 0; i < edgeCount; i++)
            AddFinToLoopAndEdge(loopSlot, faceSlot, edgeSlots[i]);
    }

    private static void AddLoopWithEdges(FaceSlot faceSlot, EdgeSlot edgeSlot, int edgeCount)
    {
        var loopSlot = AddLoop(faceSlot);
        for (int i = 0; i < edgeCount; i++)
            AddFinToLoopAndEdge(loopSlot, faceSlot, edgeSlot);
    }

    private static bool AddVertex(BodySlot bodySlot, double x, double y, double z, out VertexSlot vertexSlot)
    {
        vertexSlot = TryAllocateVertices();
        if (vertexSlot < 0) return false;
        var pointTag = CreatePointTag(x, y, z);
        if (pointTag <= 0)
            return false;

        ref var vertex = ref Vertices[vertexSlot];
        vertex.PointTag = pointTag;
        vertex.FirstFinVertex = -1;
        vertex.LastFinVertex = -1;
        AppendVertexToBody(bodySlot, vertexSlot);
        return true;
    }

    private static bool AddLineEdge(BodySlot bodySlot, VertexSlot startVertex, VertexSlot endVertex, double x0, double y0, double z0, double x1, double y1, double z1, out EdgeSlot edgeSlot)
    {
        edgeSlot = TryAllocateEdges();
        if (edgeSlot < 0) return false;
        var curveTag = CreateLineCurveTag(x0, y0, z0, x1 - x0, y1 - y0, z1 - z0);
        if (curveTag <= 0)
            return false;

        ref var edge = ref Edges[edgeSlot];
        edge.Body = bodySlot;
        edge.StartVertex = startVertex;
        edge.EndVertex = endVertex;
        edge.CurveTag = curveTag;
        edge.FirstFinEdge = -1;
        edge.LastFinEdge = -1;
        AppendEdgeToBody(bodySlot, edgeSlot);
        return true;
    }


    /// <summary>
    /// Publish tags for every topology entity reachable from the body so that
    /// read-only queries never need to create identities (lazy tag creation
    /// is gone). Also records undo entries when a mark is active.
    /// </summary>
    private static bool PublishBodyTopologyTags(BodySlot bodySlot)
    {
        var session = State.Session;
        ref var body = ref Bodies[bodySlot];

        var regionSlot = body.FirstRegion;
        for (var i = 0; i < body.RegionCount; i++, regionSlot = Regions[regionSlot].NextInBody)
        {
            if (Regions[regionSlot].Header.Tag == 0)
                Regions[regionSlot].Header.Tag = AllocateTag(EntityClass.Region, PoolKind.Region, regionSlot, Regions[regionSlot].Header.Generation);
            if (Regions[regionSlot].Header.Tag <= 0) return false;
        }

        var shellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, shellSlot = Shells[shellSlot].NextInBody)
        {
            if (Shells[shellSlot].Header.Tag == 0)
                Shells[shellSlot].Header.Tag = AllocateTag(EntityClass.Shell, PoolKind.Shell, shellSlot, Shells[shellSlot].Header.Generation);
            if (Shells[shellSlot].Header.Tag <= 0) return false;
        }

        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            if (Faces[faceSlot].Header.Tag == 0)
                Faces[faceSlot].Header.Tag = AllocateTag(EntityClass.Face, PoolKind.Face, faceSlot, Faces[faceSlot].Header.Generation);
            if (Faces[faceSlot].Header.Tag <= 0) return false;
            var loopSlot = Faces[faceSlot].FirstLoop;
            for (var j = 0; j < Faces[faceSlot].LoopCount; j++, loopSlot = Loops[loopSlot].NextInFace)
            {
                if (Loops[loopSlot].Header.Tag == 0)
                    Loops[loopSlot].Header.Tag = AllocateTag(EntityClass.Loop, PoolKind.Loop, loopSlot, Loops[loopSlot].Header.Generation);
                if (Loops[loopSlot].Header.Tag <= 0) return false;
                var finSlot = Loops[loopSlot].FirstFin;
                for (var k = 0; k < Loops[loopSlot].FinCount; k++, finSlot = Fins[finSlot].NextInLoop)
                {
                    if (Fins[finSlot].Header.Tag == 0)
                        Fins[finSlot].Header.Tag = AllocateTag(EntityClass.Fin, PoolKind.Fin, finSlot, Fins[finSlot].Header.Generation);
                    if (Fins[finSlot].Header.Tag <= 0) return false;
                }
            }
        }

        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = Edges[edgeSlot].NextInBody)
        {
            if (Edges[edgeSlot].Header.Tag == 0)
                Edges[edgeSlot].Header.Tag = AllocateTag(EntityClass.Edge, PoolKind.Edge, edgeSlot, Edges[edgeSlot].Header.Generation);
            if (Edges[edgeSlot].Header.Tag <= 0) return false;
        }

        var vertexSlot = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertexSlot = Vertices[vertexSlot].NextInBody)
        {
            if (Vertices[vertexSlot].Header.Tag == 0)
                Vertices[vertexSlot].Header.Tag = AllocateTag(EntityClass.Vertex, PoolKind.Vertex, vertexSlot, Vertices[vertexSlot].Header.Generation);
            if (Vertices[vertexSlot].Header.Tag <= 0) return false;
        }

        return true;
    }

    private static int FinishCreatedBody(BodySlot bodySlot, EntityTag* bodyTag)
    {
        var tag = AllocateTag(EntityClass.Body, PoolKind.Body, bodySlot, Bodies[bodySlot].Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        RebuildBoundaryGeometryLinks(bodySlot);
        AppendBodyToPartition(CurrentPartition, bodySlot);
        if (!PublishBodyTopologyTags(bodySlot)) return ParasolidConstants.PK_ERROR_memory_full;
        *bodyTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int CreateCircleCurveTag(double cx, double cy, double cz, double axX, double axY, double axZ, double refX, double refY, double refZ, double radius)
    {
        int dataSlot = TryAllocateCircleData();
        if (dataSlot < 0) return 0;
        ref var data = ref CircleDataPool[dataSlot];
        data.CenterX = cx; data.CenterY = cy; data.CenterZ = cz;
        data.AxisX = axX; data.AxisY = axY; data.AxisZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;
        data.Radius = radius;

        int curveSlot = TryAllocateCurves();
        if (curveSlot < 0) return -1;
        ref var curve = ref Curves[curveSlot];
        AssignPartition(ref curve.Header, CurrentPartition);
        curve.Class = CurveClass.Circle;
        curve.DataIndex = dataSlot;
        curve.TMin = 0;
        curve.TMax = Math.Tau;
        curve.Sense = ParasolidConstants.PK_TOPOL_sense_positive_c;
        return AllocateTag(EntityClass.Curve, PoolKind.Curve, curveSlot, curve.Header.Generation);
    }

    private static int CreateLineCurveTag(double ox, double oy, double oz, double axX, double axY, double axZ)
    {
        var length = Math.Sqrt(axX * axX + axY * axY + axZ * axZ);
        if (length <= 0)
            return 0;

        int dataSlot = TryAllocateLineData();
        if (dataSlot < 0) return 0;
        ref var data = ref LineDataPool[dataSlot];
        data.LocationX = ox; data.LocationY = oy; data.LocationZ = oz;
        data.AxisX = axX / length; data.AxisY = axY / length; data.AxisZ = axZ / length;

        int curveSlot = TryAllocateCurves();
        if (curveSlot < 0) return -1;
        ref var curve = ref Curves[curveSlot];
        AssignPartition(ref curve.Header, CurrentPartition);
        curve.Class = CurveClass.Line;
        curve.DataIndex = dataSlot;
        curve.TMin = 0;
        curve.TMax = length;
        curve.Sense = ParasolidConstants.PK_TOPOL_sense_positive_c;
        return AllocateTag(EntityClass.Curve, PoolKind.Curve, curveSlot, curve.Header.Generation);
    }

    private static int CreatePointTag(double x, double y, double z)
    {
        int pointSlot = TryAllocatePoints();
        if (pointSlot < 0) return -1;
        ref var point = ref Points[pointSlot];
        AssignPartition(ref point.Header, CurrentPartition);
        point.Position.X = x;
        point.Position.Y = y;
        point.Position.Z = z;
        return AllocateTag(EntityClass.Point, PoolKind.Point, pointSlot, point.Header.Generation);
    }

    private static int CreateCylinderSurfaceTag(double ox, double oy, double oz, double axX, double axY, double axZ, double refX, double refY, double refZ, double radius)
    {
        int dataSlot = TryAllocateCylinderData();
        if (dataSlot < 0) return 0;
        ref var data = ref CylinderDataPool[dataSlot];
        data.LocationX = ox; data.LocationY = oy; data.LocationZ = oz;
        data.AxisX = axX; data.AxisY = axY; data.AxisZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;
        data.Radius = radius;

        int surfSlot = TryAllocateSurfaces();
        if (surfSlot < 0) return -1;
        ref var surf = ref Surfaces[surfSlot];
        AssignPartition(ref surf.Header, CurrentPartition);
        surf.Class = SurfaceClass.Cylinder;
        surf.DataIndex = dataSlot;
        surf.UMin = 0;
        surf.UMax = Math.Tau;
        surf.VMin = 0;
        surf.VMax = 0;
        return AllocateTag(EntityClass.Surface, PoolKind.Surface, surfSlot, surf.Header.Generation);
    }

    private static int CreateConeSurfaceTag(double ox, double oy, double oz, double axX, double axY, double axZ, double refX, double refY, double refZ, double radius, double semiAngle)
    {
        int dataSlot = TryAllocateConeData();
        if (dataSlot < 0) return 0;
        ref var data = ref ConeDataPool[dataSlot];
        data.LocationX = ox; data.LocationY = oy; data.LocationZ = oz;
        data.AxisX = axX; data.AxisY = axY; data.AxisZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;
        data.Radius = radius;
        data.SemiAngle = semiAngle;

        int surfSlot = TryAllocateSurfaces();
        if (surfSlot < 0) return -1;
        ref var surf = ref Surfaces[surfSlot];
        AssignPartition(ref surf.Header, CurrentPartition);
        surf.Class = SurfaceClass.Cone;
        surf.DataIndex = dataSlot;
        surf.UMin = 0;
        surf.UMax = Math.Tau;
        surf.VMin = 0;
        surf.VMax = 0;
        return AllocateTag(EntityClass.Surface, PoolKind.Surface, surfSlot, surf.Header.Generation);
    }

    private static int CreateSphereSurfaceTag(double ox, double oy, double oz, double axX, double axY, double axZ, double refX, double refY, double refZ, double radius)
    {
        int dataSlot = TryAllocateSphereData();
        if (dataSlot < 0) return 0;
        ref var data = ref SphereDataPool[dataSlot];
        data.CenterX = ox; data.CenterY = oy; data.CenterZ = oz;
        data.AxisX = axX; data.AxisY = axY; data.AxisZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;
        data.Radius = radius;

        int surfSlot = TryAllocateSurfaces();
        if (surfSlot < 0) return -1;
        ref var surf = ref Surfaces[surfSlot];
        AssignPartition(ref surf.Header, CurrentPartition);
        surf.Class = SurfaceClass.Sphere;
        surf.DataIndex = dataSlot;
        surf.UMin = 0;
        surf.UMax = Math.Tau;
        surf.VMin = -Math.PI * 0.5;
        surf.VMax = Math.PI * 0.5;
        return AllocateTag(EntityClass.Surface, PoolKind.Surface, surfSlot, surf.Header.Generation);
    }

    private static int CreateTorusSurfaceTag(double ox, double oy, double oz, double axX, double axY, double axZ, double refX, double refY, double refZ, double majorRadius, double minorRadius)
    {
        int dataSlot = TryAllocateTorusData();
        if (dataSlot < 0) return 0;
        ref var data = ref TorusDataPool[dataSlot];
        data.LocationX = ox; data.LocationY = oy; data.LocationZ = oz;
        data.AxisX = axX; data.AxisY = axY; data.AxisZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;
        data.MajorRadius = majorRadius;
        data.MinorRadius = minorRadius;

        int surfSlot = TryAllocateSurfaces();
        if (surfSlot < 0) return -1;
        ref var surf = ref Surfaces[surfSlot];
        AssignPartition(ref surf.Header, CurrentPartition);
        surf.Class = SurfaceClass.Torus;
        surf.DataIndex = dataSlot;
        surf.UMin = 0;
        surf.UMax = Math.Tau;
        surf.VMin = -Math.PI;
        surf.VMax = Math.PI;
        return AllocateTag(EntityClass.Surface, PoolKind.Surface, surfSlot, surf.Header.Generation);
    }

    private static int CreatePlaneSurfaceTag(double ox, double oy, double oz, double axX, double axY, double axZ, double refX, double refY, double refZ)
    {
        int dataSlot = TryAllocatePlaneData();
        if (dataSlot < 0) return 0;
        ref var data = ref PlaneDataPool[dataSlot];
        data.LocationX = ox; data.LocationY = oy; data.LocationZ = oz;
        data.NormalX = axX; data.NormalY = axY; data.NormalZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;

        int surfSlot = TryAllocateSurfaces();
        if (surfSlot < 0) return -1;
        ref var surf = ref Surfaces[surfSlot];
        AssignPartition(ref surf.Header, CurrentPartition);
        surf.Class = SurfaceClass.Plane;
        surf.DataIndex = dataSlot;
        surf.UMin = 0;
        surf.UMax = 0;
        surf.VMin = 0;
        surf.VMax = 0;
        return AllocateTag(EntityClass.Surface, PoolKind.Surface, surfSlot, surf.Header.Generation);
    }

    private static void ReadAxis2(PK_AXIS2_sf_s* basisSet, out double ox, out double oy, out double oz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ)
    {
        ox = 0; oy = 0; oz = 0;
        axX = 0; axY = 0; axZ = 1;
        refX = 1; refY = 0; refZ = 0;
        if (basisSet is null)
            return;

        ox = basisSet->location.coord[0];
        oy = basisSet->location.coord[1];
        oz = basisSet->location.coord[2];
        axX = basisSet->axis.coord[0];
        axY = basisSet->axis.coord[1];
        axZ = basisSet->axis.coord[2];
        refX = basisSet->ref_direction.coord[0];
        refY = basisSet->ref_direction.coord[1];
        refZ = basisSet->ref_direction.coord[2];
    }

    private static void Cross(double ax, double ay, double az, double bx, double by, double bz, out double cx, out double cy, out double cz)
    {
        cx = ay * bz - az * by;
        cy = az * bx - ax * bz;
        cz = ax * by - ay * bx;
    }

    // ── Dispatch ─────────────────────────────────────────────────

    public static int Dispatch<TCommand>(ApiId apiId, ConcurrencyKind concurrencyKind, AccessKind accessKind,
        ref TCommand command, EntityTag targetEntity = 0) where TCommand : struct, IKernelCommand
    {
        var descriptor = new CommandDescriptor
        { ApiId = apiId, ConcurrencyKind = concurrencyKind, AccessKind = accessKind, TargetEntity = targetEntity };
        return Dispatcher.Execute(ref descriptor, ref command);
    }

    // Transaction completion is INSIDE the scheduler claim. Stop/rollback
    // cannot free the session or replay a log before this command commits.
    internal static int ExecuteAuthorized<TCommand>(SessionData* session, SessionData.ThreadContext* context,
        AccessKind access, ApiId api, PartitionSlot partition,
        ref TCommand command) where TCommand : struct, IKernelCommand
    {
        if (session == null || api is ApiId.SessionStart or ApiId.SessionStop
            or ApiId.MarkCreate or ApiId.MarkGoto or ApiId.MarkDelete)
            return command.Execute();
        if (context == null) return ParasolidConstants.PK_ERROR_memory_full;
        // A point publishes exactly one record and explicitly frees it if tag
        // publication fails. It needs no undo journal unless a mark or an
        // enclosing composite command owns its lifetime.
        if (typeof(TCommand) == typeof(PointCreateCommand) && session->IsMarkActive == 0)
        {
            context->SkipCreationUndo = 1;
            context->InKernel++;
            try
            {
                ref var creation = ref Unsafe.As<TCommand, PointCreateCommand>(ref command);
                var error = PointCreateImplementation(creation.PointSf, creation.Point);
                if (error == 0) session->Partitions[context->CurrentPartition].AtPmark = 0;
                return error;
            }
            finally { context->SkipCreationUndo = 0; context->InKernel--; }
        }
        if (typeof(TCommand) == typeof(EntityDeleteCommand) && session->IsMarkActive == 0)
        {
            ref var deletion = ref Unsafe.As<TCommand, EntityDeleteCommand>(ref command);
            if (deletion.EntityCount == 1 && deletion.Entities != null
                && TryDeleteAtomicPoint(session, context, *deletion.Entities, out var result))
                return result;
        }
        var boundary = context->UndoEntryCount;
        var sessionGeneration = session->SessionGeneration;
        var deferredBoundary = context->DeferredCount;
        var returnBoundary = session->Returns.Sequence;
        context->DeferCommandDeletes = typeof(TCommand) != typeof(EntityDeleteCommand) || session->IsMarkActive != 0 ? (byte)1 : (byte)0;
        context->PartitionDeletionPending = 0;
        context->InKernel++;
        try
        {
            if (access == AccessKind.GlobalWrite && api != ApiId.EntityDelete && session->Partitions[partition].AtPmark != 0
                && !session->TryAppendUndo(SessionData.UndoKind.PartitionModified, 0, partition, partition, session->Partitions[partition].AtPmark, 0, null, 0))
                return ParasolidConstants.PK_ERROR_memory_full;
            var error = command.Execute();
            // A waiting partition-lock call drops its scheduler claim. Stop
            // may have released the old session before that call resumes.
            if (State.Session != session || session->SessionGeneration != sessionGeneration)
                return error != 0 ? error : ParasolidConstants.PK_ERROR_not_in_PK;
            if (error < 0) error = ParasolidConstants.PK_ERROR_memory_full;
            if (error == 0 && access == AccessKind.GlobalWrite && api != ApiId.EntityDelete)
                session->Partitions[partition].AtPmark = 0;
            if (error != 0)
            {
                UndoCommandEffects(session, boundary);
                session->DiscardUndoFrom(boundary);
                // Free callbacks can reenter the kernel. Their successful
                // operations belong to the remaining mark, not to this failed
                // command's already-replayed journal segment.
                context->DeferCommandDeletes = session->IsMarkActive != 0 ? (byte)1 : (byte)0;
                session->Returns.ReleaseSince(returnBoundary, context->ManagedThreadId);
                if (session->IsMarkActive == 0) session->DiscardUndoFrom(boundary);
                return error;
            }
            else if (context->PartitionDeletionPending != 0)
            {
                for (var i = boundary; i < context->UndoEntryCount; i++)
                    if (context->UndoEntries[i].Kind == SessionData.UndoKind.PartitionDeleted)
                        CommitPartitionDeletion(session, context->UndoEntries[i].Slot);
            }
            if (session->IsMarkActive == 0 && error == 0)
            {
                for (var i = deferredBoundary; i < context->DeferredCount; i++)
                {
                    var entry = context->Deferred[i];
                    FinalReleaseEntity((PoolKind)entry.Pool, entry.Slot, entry.Tag);
                }
                context->DeferredCount = deferredBoundary;
            }
            if (session->IsMarkActive == 0) session->DiscardUndoFrom(boundary);
            return error;
        }
        finally
        {
            if (State.Session == session && session->SessionGeneration == sessionGeneration)
            {
                context->InKernel--;
                context->DeferCommandDeletes = 0;
                context->PartitionDeletionPending = 0;
            }
        }
    }

    // A standalone point has no payload, topology links or fallible work
    // after validation. Composite commands and marked deletes use the full
    // transaction path; this only handles the concrete scalar delete command.
    private static bool TryDeleteAtomicPoint(SessionData* session, SessionData.ThreadContext* context,
        EntityTag tag, out int error)
    {
        error = 0;
        if (!session->Tags.TryResolve(tag, CurrentSessionId, out var record))
        { error = ParasolidConstants.PK_ERROR_unknown_class; return true; }
        if (record.Pool != (byte)PoolKind.Point) return false;
        ref var point = ref Points[record.Slot];
        if (point.OwnerCount != 0) { error = ParasolidConstants.PK_ERROR_is_attached; return true; }
        ref var partition = ref session->Partitions[point.Header.Partition];
        if (partition.LockOwnerThread != 0 && partition.LockOwnerThread != context->ManagedThreadId)
        { error = ParasolidConstants.PK_ERROR_bad_thread; return true; }
        partition.AtPmark = 0;
        Points.Free(record.Slot);
        session->Tags.Revoke(tag);
        return true;
    }

    // Explicit PK partition deletion removes the partition at every mark.
    // Delay this irreversible pruning until the surrounding command succeeds.
    private static void CommitPartitionDeletion(SessionData* session, PartitionSlot partition)
    {
        if (session->MarkCurrentPartition == partition)
            session->MarkCurrentPartition = ThreadContext()->CurrentPartition;
        for (var thread = 0; thread < session->ThreadCount; thread++)
        {
            var context = session->Threads.Pointer(thread);
            for (var i = 0; i < context->UndoEntryCount; i++)
            {
                ref var entry = ref context->UndoEntries[i];
                if (entry.Partition != partition || entry.Kind == SessionData.UndoKind.CurrentPartitionChanged) continue;
                if (entry.Kind == SessionData.UndoKind.FieldSnapshot && entry.Data != null)
                {
                    session->Blocks.Free(entry.Data);
                    context->UndoSnapshotCount--;
                }
                entry.Data = null;
                entry.Kind = (SessionData.UndoKind)SessionData.UndoBookkeeping.Cancelled;
            }
            for (var i = 0; i < context->DeferredCount;)
            {
                var entry = context->Deferred[i];
                if (PoolPartitionOf((PoolKind)entry.Pool, entry.Slot) != partition) { i++; continue; }
                FinalReleaseEntity((PoolKind)entry.Pool, entry.Slot, entry.Tag);
                context->Deferred[i] = context->Deferred[--context->DeferredCount];
            }
        }
    }
    /// <summary>
    /// Reverse the undo entries a failed command appended: destroy created
    /// entities, restore deleted ones and field snapshots. Only this
    /// command's entries (matched by thread) are touched.
    /// </summary>
    private static void UndoCommandEffects(SessionData* session, int boundary)
    {
        var threadId = Environment.CurrentManagedThreadId;
        for (int i = session->UndoEntryCount - 1; i >= boundary; i--)
        {
            ref var entry = ref session->UndoEntries[i];
            if ((byte)entry.Kind == (byte)SessionData.UndoBookkeeping.Cancelled)
                continue;
            if (entry.ThreadId != threadId)
                continue;                              // another command's entry
            ReplayMarkEntry(session, ref entry, commandFailure: true);
            if (entry.Kind == SessionData.UndoKind.EntityDeleted)
                DropDeferredRelease(session, entry.Pool, entry.Slot);
        }
    }

    private static void DropDeferredRelease(SessionData* session, byte pool, int slot)
    {
        for (int i = session->DeferredCount - 1; i >= 0; i--)
        {
            if (session->Deferred[i].Pool == pool && session->Deferred[i].Slot == slot)
            {
                for (int j = i; j < session->DeferredCount - 1; j++)
                    session->Deferred[j] = session->Deferred[j + 1];
                session->DeferredCount--;
                return;
            }
        }
    }

    public static int NotImplemented()
    {
        return ParasolidConstants.PK_ERROR_not_implemented;
    }
}

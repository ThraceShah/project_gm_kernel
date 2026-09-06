using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal static int EdgeAttachCurves(BufferCount count, EntityTag* edges, CurveTag* curves)
    {
        var command = new AttachGeometryCommand { Count = count, Topology = edges, Geometry = curves, Pool = PoolKind.Edge };
        return Dispatch(ApiId.AttachGeometry, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, count > 0 && edges != null ? edges[0] : 0);
    }

    internal static int FaceAttachSurfaces(BufferCount count, EntityTag* faces, SurfTag* surfaces, byte* senses)
    {
        var command = new AttachGeometryCommand { Count = count, Topology = faces, Geometry = surfaces, Senses = senses, Pool = PoolKind.Face };
        return Dispatch(ApiId.AttachGeometry, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, count > 0 && faces != null ? faces[0] : 0);
    }

    internal static int VertexAttachPoints(BufferCount count, EntityTag* vertices, PointTag* points)
    {
        var command = new AttachGeometryCommand { Count = count, Topology = vertices, Geometry = points, Pool = PoolKind.Vertex };
        return Dispatch(ApiId.AttachGeometry, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, count > 0 && vertices != null ? vertices[0] : 0);
    }

    internal static int TopologyDetachGeometry(EntityTag topology)
    {
        var command = new DetachGeometryCommand { Topology = topology };
        return Dispatch(ApiId.DetachGeometry, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, topology);
    }

    private struct AttachGeometryCommand : IKernelCommand
    {
        internal BufferCount Count;
        internal EntityTag* Topology;
        internal EntityTag* Geometry;
        internal byte* Senses;
        internal PoolKind Pool;

        public int Execute()
        {
            if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
            if (Count <= 0 || Topology == null || Geometry == null || (Pool == PoolKind.Face && Senses == null))
                return ParasolidConstants.PK_ERROR_bad_field_number;
            var session = State.Session;
            var geometryPool = GeometryPool(Pool);
            PartitionSlot partition = -1;
            for (var i = 0; i < Count; i++)
            {
                if (!session->Tags.TryResolve(Topology[i], CurrentSessionId, out var topol) || topol.Pool != (byte)Pool
                    || !session->Tags.TryResolve(Geometry[i], CurrentSessionId, out var geometry) || geometry.Pool != (byte)geometryPool)
                    return ParasolidConstants.PK_ERROR_unknown_class;
                var targetPartition = PoolPartitionOf(Pool, topol.Slot);
                if (partition < 0) partition = targetPartition;
                if (targetPartition != partition || PoolPartitionOf(geometryPool, geometry.Slot) != partition)
                    return ParasolidConstants.PK_ERROR_not_in_same_partition;
                var owner = session->Partitions[partition].LockOwnerThread;
                if (owner != 0 && owner != ThreadContext()->ManagedThreadId) return ParasolidConstants.PK_ERROR_bad_thread;
                if (TopologyGeometry(Pool, topol.Slot) > 0) return ParasolidConstants.PK_ERROR_is_attached;
                if (geometryPool == PoolKind.Point && Points[geometry.Slot].OwnerCount != 0)
                    return ParasolidConstants.PK_ERROR_has_parent;
                if (Senses != null && Senses[i] > 1) return ParasolidConstants.PK_ERROR_bad_value;
                var body = TopologyBody(Pool, topol.Slot);
                if (body < 0) return ParasolidConstants.PK_ERROR_bad_value;
                if (GeometryOwnerCount(geometryPool, geometry.Slot) != 0 && GeometryBody(geometryPool, geometry.Slot) != body)
                    return ParasolidConstants.PK_ERROR_not_in_same_part;
                for (var j = 0; j < i; j++)
                {
                    if (Topology[j] == Topology[i]) return ParasolidConstants.PK_ERROR_bad_value;
                    if (geometryPool == PoolKind.Point && Geometry[j] == Geometry[i])
                        return ParasolidConstants.PK_ERROR_has_parent;
                    if (Geometry[j] == Geometry[i] && TopologyBody(Pool, TagRec(Topology[j]).Slot) != body)
                        return ParasolidConstants.PK_ERROR_not_in_same_part;
                }
            }
            // Reserve every before-image before changing any existing record.
            for (var i = 0; i < Count; i++)
            {
                var slot = TagRec(Topology[i]).Slot;
                var body = TopologyBody(Pool, slot);
                if (!SnapshotBoundaryGeometry(body) || !SnapshotGeometry(geometryPool, TagRec(Geometry[i]).Slot)
                    || (Pool == PoolKind.Vertex && !session->TrySnapshot(ref Vertices[slot])))
                    return ParasolidConstants.PK_ERROR_memory_full;
            }
            for (var i = 0; i < Count; i++)
            {
                var slot = TagRec(Topology[i]).Slot;
                TopologyGeometry(Pool, slot) = Geometry[i];
                if (Pool == PoolKind.Face)
                    Faces[slot].Orientation = Senses[i] != 0 ? ParasolidConstants.PK_TOPOL_sense_positive_c : ParasolidConstants.PK_TOPOL_sense_negative_c;
            }
            for (var i = 0; i < Count; i++)
                RebuildBoundaryGeometryLinks(TopologyBody(Pool, TagRec(Topology[i]).Slot), resetOwners: true);
            session->Partitions[partition].AtPmark = 0;
            return 0;
        }
    }

    private struct DetachGeometryCommand : IKernelCommand
    {
        internal EntityTag Topology;
        public int Execute()
        {
            if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
            var session = State.Session;
            if (!session->Tags.TryResolve(Topology, CurrentSessionId, out var topol)) return ParasolidConstants.PK_ERROR_unknown_class;
            var pool = (PoolKind)topol.Pool;
            if (pool is not (PoolKind.Face or PoolKind.Edge or PoolKind.Vertex)) return ParasolidConstants.PK_ERROR_not_implemented;
            var partition = PoolPartitionOf(pool, topol.Slot);
            var owner = session->Partitions[partition].LockOwnerThread;
            if (owner != 0 && owner != ThreadContext()->ManagedThreadId) return ParasolidConstants.PK_ERROR_bad_thread;
            var geometry = TopologyGeometry(pool, topol.Slot);
            if (geometry <= 0) return 0;
            var body = TopologyBody(pool, topol.Slot);
            if (!SnapshotBoundaryGeometry(body) || (pool == PoolKind.Vertex && !session->TrySnapshot(ref Vertices[topol.Slot])))
                return ParasolidConstants.PK_ERROR_memory_full;
            var geometryPool = GeometryPool(pool);
            ClearGeometryOwner(geometryPool, TagRec(geometry).Slot);
            TopologyGeometry(pool, topol.Slot) = 0;
            RebuildBoundaryGeometryLinks(body, resetOwners: true);
            session->Partitions[partition].AtPmark = 0;
            return 0;
        }
    }

    private static PoolKind GeometryPool(PoolKind topology)
        => topology == PoolKind.Face ? PoolKind.Surface : topology == PoolKind.Edge ? PoolKind.Curve : PoolKind.Point;

    private static ref EntityTag TopologyGeometry(PoolKind pool, DataSlot slot)
    {
        if (pool == PoolKind.Face) return ref Faces[slot].SurfTag;
        if (pool == PoolKind.Edge) return ref Edges[slot].CurveTag;
        return ref Vertices[slot].PointTag;
    }

    private static BodySlot TopologyBody(PoolKind pool, DataSlot slot)
    {
        if (pool == PoolKind.Edge) return Edges[slot].Body;
        if (pool == PoolKind.Vertex) return Vertices[slot].Body;
        var shell = Faces[slot].BackShell >= 0 ? Faces[slot].BackShell : Faces[slot].FrontShell;
        return shell >= 0 ? Shells[shell].Body : -1;
    }

    private static BufferCount GeometryOwnerCount(PoolKind pool, DataSlot slot)
        => pool == PoolKind.Curve ? Curves[slot].OwnerCount : pool == PoolKind.Surface ? Surfaces[slot].OwnerCount : Points[slot].OwnerCount;

    private static BodySlot GeometryBody(PoolKind pool, DataSlot slot)
        => pool == PoolKind.Curve ? TopologyBody(PoolKind.Edge, Curves[slot].OwnerEdge)
            : pool == PoolKind.Surface ? TopologyBody(PoolKind.Face, Surfaces[slot].OwnerFace)
            : TopologyBody(PoolKind.Vertex, Points[slot].OwnerVertex);

    private static bool SnapshotGeometry(PoolKind pool, DataSlot slot)
        => pool == PoolKind.Curve ? State.Session->TrySnapshot(ref Curves[slot])
            : pool == PoolKind.Surface ? State.Session->TrySnapshot(ref Surfaces[slot])
            : State.Session->TrySnapshot(ref Points[slot]);

    private static void ClearGeometryOwner(PoolKind pool, DataSlot slot)
    {
        if (pool == PoolKind.Curve)
        {
            ref var record = ref Curves[slot];
            record.OwnerCount = 0; record.OwnerEdge = -1;
            record.PrevInBody = record.NextInBody = 0;
        }
        else if (pool == PoolKind.Surface)
        {
            ref var record = ref Surfaces[slot];
            record.OwnerCount = 0; record.OwnerFace = -1;
            record.PrevInBody = record.NextInBody = 0;
        }
        else
        {
            ref var record = ref Points[slot];
            record.OwnerCount = 0; record.OwnerVertex = -1;
            record.PrevInBody = record.NextInBody = 0;
        }
    }

    private static bool SnapshotBoundaryGeometry(BodySlot bodySlot)
    {
        var body = Bodies[bodySlot];
        var session = State.Session;
        var face = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, face = Faces[face].NextInBody)
        {
            if (!session->TrySnapshot(ref Faces[face])) return false;
            var geometry = GetSurfaceSlotByTag(Faces[face].SurfTag);
            if (geometry >= 0 && !session->TrySnapshot(ref Surfaces[geometry])) return false;
        }
        var edge = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edge = Edges[edge].NextInBody)
        {
            if (!session->TrySnapshot(ref Edges[edge])) return false;
            var geometry = GetCurveSlotByTag(Edges[edge].CurveTag);
            if (geometry >= 0 && !session->TrySnapshot(ref Curves[geometry])) return false;
        }
        var vertex = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertex = Vertices[vertex].NextInBody)
        {
            var geometry = GetPointSlotByTag(Vertices[vertex].PointTag);
            if (geometry >= 0 && !session->TrySnapshot(ref Points[geometry])) return false;
        }
        return true;
    }
}

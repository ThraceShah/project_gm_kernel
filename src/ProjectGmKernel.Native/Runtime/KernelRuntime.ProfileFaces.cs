using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BodyCreateSheetPlanar(int count, PK_VECTOR_s* vectors,
        PK_BODY_create_sheet_planar_o_s* options, EntityTag* body)
    {
        if (Dispatcher.IsExecuting) return BodyCreateSheetPlanarCore(count, vectors, options, body);
        var command = new ProfilePlanarCommand { Count = count, Vectors = vectors, Options = options, Body = body };
        return Dispatch(ApiId.BodyCreateSheetPlanar, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    private struct ProfilePlanarCommand : IKernelCommand
    {
        internal BufferCount Count;
        internal PK_VECTOR_s* Vectors;
        internal PK_BODY_create_sheet_planar_o_s* Options;
        internal EntityTag* Body;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try
            {
                var error=BodyCreateSheetPlanarCore(Count,Vectors,Options,Body);
                if(error!=0&&Body!=null)*Body=0;
                return error;
            }
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    public static int EdgeMakeFacesFromWire(int count, EntityTag* edges, byte* senses, int* sharedLoop, EntityTag* newFaces)
    {
        if (Dispatcher.IsExecuting) return EdgeMakeFacesFromWireCore(count, edges, senses, sharedLoop, newFaces);
        var command = new ProfileFacesCommand { Count = count, Edges = edges, Senses = senses, SharedLoop = sharedLoop, Faces = newFaces };
        return Dispatch(ApiId.EdgeMakeFacesFromWire, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command,
            count > 0 && edges != null ? edges[0] : 0);
    }

    private struct ProfileFacesCommand : IKernelCommand
    {
        internal BufferCount Count;
        internal EntityTag* Edges;
        internal byte* Senses;
        internal BufferOffset* SharedLoop;
        internal EntityTag* Faces;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try{return EdgeMakeFacesFromWireCore(Count,Edges,Senses,SharedLoop,Faces);}
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    private static int BodyCreateSheetPlanarCore(int count, PK_VECTOR_s* vectors,
        PK_BODY_create_sheet_planar_o_s* options, EntityTag* body)
    {
        if (body != null) *body = 0;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (count < 3 || count > ProfileEdgeLimit || vectors == null || body == null || options == null)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->o_t_version != 1) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        var origin = Vector(options->plane.location.coord[0], options->plane.location.coord[1], options->plane.location.coord[2]);
        var normal = Vector(options->plane.axis.coord[0], options->plane.axis.coord[1], options->plane.axis.coord[2]);
        if (!ProfileUnit(in normal) || !ProfileFinite(in origin)) return ParasolidConstants.PK_ERROR_bad_parameter;
        var basisOrigin=Vector(options->basis_set.location.coord[0],options->basis_set.location.coord[1],options->basis_set.location.coord[2]);
        var basisZ=Vector(options->basis_set.axis.coord[0],options->basis_set.axis.coord[1],options->basis_set.axis.coord[2]);
        var basisX=Vector(options->basis_set.ref_direction.coord[0],options->basis_set.ref_direction.coord[1],options->basis_set.ref_direction.coord[2]);
        if(!ProfileFinite(in basisOrigin)||!ProfileUnit(in basisZ)||!ProfileUnit(in basisX)||Math.Abs(ProfileDot(basisX,basisZ))>1e-10)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        var basisY=ProfileCross(basisZ,basisX);
        var frame=new KernelAffineTransform
        {
            M00=basisX.X,M01=basisY.X,M02=basisZ.X,M03=basisOrigin.X,
            M10=basisX.Y,M11=basisY.Y,M12=basisZ.Y,M13=basisOrigin.Y,
            M20=basisX.Z,M21=basisY.Z,M22=basisZ.Z,M23=basisOrigin.Z,
        };
        Span<int> offsets = stackalloc int[count + 1];
        var loops = 1; offsets[0] = 0;
        for (var i = 0; i < count; i++)
        {
            var p = Vector(vectors[i].coord[0], vectors[i].coord[1], vectors[i].coord[2]);
            if (!ProfileFinite(in p) || Math.Abs(ProfileDot(ProfileSubtract(p, origin), normal)) > 1e-8)
                return ParasolidConstants.PK_ERROR_bad_parameter;
            if (options->loops != null && i > 0 && options->loops[i] != options->loops[i-1])
            {
                if (i - offsets[loops-1] < 3) return ParasolidConstants.PK_ERROR_bad_loop;
                for (var j = 0; j < loops; j++)
                    if (options->loops[offsets[j]] == options->loops[i]) return ParasolidConstants.PK_ERROR_bad_loop;
                offsets[loops++] = i;
            }
        }
        offsets[loops] = count;
        if (count - offsets[loops-1] < 3) return ParasolidConstants.PK_ERROR_bad_loop;
        int* curves = stackalloc int[count];
        PK_INTERVAL_s* bounds = stackalloc PK_INTERVAL_s[count];
        for (var loop = 0; loop < loops; loop++)
            for (var i = offsets[loop]; i < offsets[loop+1]; i++)
            {
                var j = i + 1 == offsets[loop+1] ? offsets[loop] : i + 1;
                var dx = vectors[j].coord[0] - vectors[i].coord[0];
                var dy = vectors[j].coord[1] - vectors[i].coord[1];
                var dz = vectors[j].coord[2] - vectors[i].coord[2];
                var length = Math.Sqrt(dx*dx + dy*dy + dz*dz);
                if (!double.IsFinite(length) || length <= 1e-8) return ParasolidConstants.PK_ERROR_bad_loop;
                curves[i] = CreateLineCurveTag(vectors[i].coord[0], vectors[i].coord[1], vectors[i].coord[2], dx, dy, dz);
                if (curves[i] <= 0) return ParasolidConstants.PK_ERROR_memory_full;
                bounds[i].value[0] = 0; bounds[i].value[1] = length;
            }
        var wireOptions = new PK_CURVE_make_wire_body_o_s { o_t_version = 3, tolerance = 1e-8,
            allow_disjoint = 1, check = 1, want_edges = 1, sequential = ParasolidConstants.PK_CURVE_sequential_no_c };
        int edgeCount = 0; int* edges = null;
        var error = CurveMakeWireBodyCore(count, curves, bounds, &wireOptions, body, &edgeCount, &edges, null);
        if (error != 0) return error;
        int* seeds = stackalloc int[loops]; byte* senses = stackalloc byte[loops];
        int* shared = stackalloc int[loops]; int* faces = stackalloc int[loops];
        for (var i = 0; i < loops; i++) { seeds[i] = edges[offsets[i]]; senses[i] = 1; shared[i] = i==0?-1:0; }
        error = EdgeMakeFacesFromWireCore(loops, seeds, senses, shared, faces, attachPlane:true);
        if (error != 0) return error;
        // `plane` describes the vectors in local coordinates. `basis_set` is
        // the independent local-to-world frame applied to the completed sheet.
        var faceSlot=Bodies[TagRec(*body).Slot].FirstFaceBody;
        ref var faceRecord=ref Faces[faceSlot];
        ref var planeRecord=ref PlaneDataPool[Surfaces[GetSurfaceSlotByTag(faceRecord.SurfTag)].DataIndex];
        var normalDot=planeRecord.NormalX*normal.X+planeRecord.NormalY*normal.Y+planeRecord.NormalZ*normal.Z;
        if(normalDot<0)
            faceRecord.Orientation=faceRecord.Orientation==ParasolidConstants.PK_TOPOL_sense_negative_c
                ?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
        planeRecord.NormalX=normal.X;planeRecord.NormalY=normal.Y;planeRecord.NormalZ=normal.Z;
        planeRecord.LocationX=origin.X;planeRecord.LocationY=origin.Y;planeRecord.LocationZ=origin.Z;
        error=TryTransformEntityCore(*body,in frame);
        if(error!=0)return error;
        // The temporary source lines have no topology owners: the public wire
        // constructor duplicates its inputs, just as separate modeling calls do.
        error = EntityDelete(count, curves);
        MemoryFree(edges);
        return error;
    }

    private static int EdgeMakeFacesFromWireCore(int loopCount, EntityTag* seeds, byte* senses,
        int* sharedLoop, EntityTag* newFaces, bool attachPlane=false)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (loopCount <= 0 || loopCount > ProfileEdgeLimit || seeds == null || senses == null || newFaces == null || sharedLoop==null)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        for (var i = 0; i < loopCount; i++) newFaces[i] = 0;
        if (!State.Session->Tags.TryResolve(seeds[0], CurrentSessionId, out var seedRecord)
            || seedRecord.Pool != (byte)PoolKind.Edge) return ParasolidConstants.PK_ERROR_bad_class;
        var bodySlot = Edges[seedRecord.Slot].Body;
        ref var body = ref Bodies[bodySlot];
        if (body.BodyType != ParasolidConstants.PK_BODY_type_wire_c || body.FaceCountBody != 0
            || body.RegionCount != 1 || Regions[body.FirstRegion].IsSolid != 0)
            return ParasolidConstants.PK_ERROR_bad_wire;
        if (body.Header.Partition != CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
        if (body.EdgeCountBody > ProfileEdgeLimit) return ParasolidConstants.PK_ERROR_not_implemented;
        // Each entry identifies the initial fin of a loop. This implementation
        // creates a single bounded face: -1 starts it; holes name outer loop 0.
        for (var i = 0; i < loopCount; i++)
            if (senses[i] > 1 || sharedLoop[i]!=(i==0?-1:0))
                return ParasolidConstants.PK_ERROR_not_implemented;
        Span<int> ordered = stackalloc int[body.EdgeCountBody];
        Span<int> directions = stackalloc int[body.EdgeCountBody];
        Span<int> offsets = stackalloc int[loopCount + 1];
        var used = 0;
        for (var loop = 0; loop < loopCount; loop++)
        {
            offsets[loop] = used;
            if (!State.Session->Tags.TryResolve(seeds[loop], CurrentSessionId, out var record)
                || record.Pool != (byte)PoolKind.Edge || Edges[record.Slot].Body != bodySlot)
                return ParasolidConstants.PK_ERROR_bad_class;
            var start = Edges[record.Slot].FirstFinEdge;
            if ((Fins[start].Sense == '+') != (senses[loop] != 0)) start = Fins[start].Other;
            var fin = start;
            do
            {
                if (fin < 0 || Fins[fin].Loop >= 0 || used >= ordered.Length) return ParasolidConstants.PK_ERROR_bad_wire;
                var edge = Fins[fin].Edge;
                for (var j = 0; j < used; j++) if (ordered[j] == edge) return ParasolidConstants.PK_ERROR_bad_wire;
                ordered[used] = edge; directions[used++] = Fins[fin].Sense == '+' ? 1 : -1;
                if(Edges[edge].StartVertex<0&&Edges[edge].EndVertex<0)break;
                fin = Fins[fin].NextInLoop;
            } while (fin != start);
        }
        offsets[loopCount] = used;
        if (used != ordered.Length) return ParasolidConstants.PK_ERROR_not_implemented;
        var validation = ValidateProfileLoops(ordered, directions, offsets,
            out var origin, out var normal, out var reference);
        if (validation != 0) return validation;
        if (!SnapshotProfileTopology(bodySlot)) return ParasolidConstants.PK_ERROR_memory_full;
        if(DeferDeletes&&!State.Session->TryReserveDeletion(2*used+body.ShellCount))return ParasolidConstants.PK_ERROR_memory_full;
        var plane = attachPlane?CreatePlaneSurfaceTag(origin.X, origin.Y, origin.Z, normal.X, normal.Y, normal.Z,
            reference.X, reference.Y, reference.Z):0;
        if (attachPlane&&plane <= 0) return ParasolidConstants.PK_ERROR_memory_full;
        var face = AddFace(bodySlot, plane);
        if (face < 0) return ParasolidConstants.PK_ERROR_memory_full;
        Faces[face].Orientation = ParasolidConstants.PK_TOPOL_sense_positive_c;

        // A neutral sheet has both sides in the same void shell. Merge the
        // component shells after validating all loops and before attaching it.
        var shell = body.FirstShell;
        var nextShell = Shells[shell].NextInBody;
        for (var i = 1; i < body.ShellCount; i++)
        {
            var next = Shells[nextShell].NextInBody;
            DeleteEntitySingle(State.Session, PoolKind.Shell, nextShell, Shells[nextShell].Header.Tag);
            nextShell = next;
        }
        body.FirstShell = body.LastShell = shell; body.ShellCount = 1;
        Shells[shell].NextInBody = Shells[shell].PrevInBody = shell;
        Shells[shell].NextInRegion = Shells[shell].PrevInRegion = shell;
        ref var region = ref Regions[body.FirstRegion];
        region.FirstShell = region.LastShell = shell; region.ShellCount = 1;
        if (AddFaceUse(shell, face, ParasolidConstants.PK_TOPOL_sense_positive_c) < 0
            || AddFaceUse(shell, face, ParasolidConstants.PK_TOPOL_sense_negative_c) < 0)
            return ParasolidConstants.PK_ERROR_memory_full;
        var vertex = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertex = Vertices[vertex].NextInBody)
            Vertices[vertex].FirstFinVertex = Vertices[vertex].LastFinVertex = -1;
        for (var i = 0; i < used; i++)
        {
            ref var edge = ref Edges[ordered[i]];
            var oldFin = edge.FirstFinEdge;
            for (var j = 0; j < edge.FinCount; j++)
            {
                var next = Fins[oldFin].NextOfEdge;
                DeleteEntitySingle(State.Session, PoolKind.Fin, oldFin, Fins[oldFin].Header.Tag);
                oldFin = next;
            }
            edge.FirstFinEdge = edge.LastFinEdge = -1; edge.FinCount = 0; edge.WireShellTag = 0;
        }
        for (var loop = 0; loop < loopCount; loop++)
        {
            var slot = AddLoop(face);
            if (slot < 0) return ParasolidConstants.PK_ERROR_memory_full;
            for (var i = offsets[loop]; i < offsets[loop+1]; i++)
                if (AddProfileFin(slot, ordered[i], directions[i] > 0) < 0) return ParasolidConstants.PK_ERROR_memory_full;
        }
        body.BodyType = ParasolidConstants.PK_BODY_type_sheet_c;
        // The published wire already owns its curves and vertex points.
        // Converting its fins into face loops adds only this surface owner.
        RebuildFaceSurfaceChains(bodySlot);
        if(plane>0)
        {
            ref var geometry=ref Surfaces[GetSurfaceSlotByTag(plane)];
            geometry.OwnerCount++;geometry.OwnerFace=face;
            geometry.PrevInBody=geometry.NextInBody=plane;
        }
        if (!PublishBodyTopologyTags(bodySlot)) return ParasolidConstants.PK_ERROR_memory_full;
        for (var i = 0; i < loopCount; i++) newFaces[i] = Faces[face].Header.Tag;
        SessionMemoryOwner.DropBodyXt(bodySlot);
        return 0;
    }

    private static int ValidateProfileLoops(Span<int> edges, Span<int> directions, ReadOnlySpan<int> offsets,
        out KernelVector3 origin, out KernelVector3 normal, out KernelVector3 reference)
    {
        origin = normal = reference = default;
        var scratch = CommandScratch.Current;
        var sampleBudget=offsets.Length;
        for(var i=0;i<edges.Length;i++)
            sampleBudget+=Curves[GetCurveSlotByTag(Edges[edges[i]].CurveTag)].Class==CurveClass.Line?1:64;
        var workspace = scratch.Take(3 * sampleBudget);
        if (workspace.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var samples = MemoryMarshal.Cast<double, KernelVector3>(workspace);
            Span<int> sampleOffsets = stackalloc int[offsets.Length];
            Span<KernelVector3> value = stackalloc KernelVector3[2];
            var count = 0;
            for (var loop = 0; loop < offsets.Length - 1; loop++)
            {
                sampleOffsets[loop] = count;
                for (var i = offsets[loop]; i < offsets[loop+1]; i++)
                {
                    ref readonly var edge = ref Edges[edges[i]];
                    if (!TryProfileEdgeRange(in edge, out var a, out var b)) return ParasolidConstants.PK_ERROR_not_implemented;
                    if (directions[i] < 0) (a,b) = (b,a);
                    ref readonly var curve = ref Curves[GetCurveSlotByTag(edge.CurveTag)];
                    var divisions = curve.Class == CurveClass.Line ? 1 : 64;
                    for (var k = 0; k < divisions; k++)
                    {
                        if (EvaluateCurveCore(in curve, a + (b-a) * k/divisions, 0, false, value, out _) != AlgorithmStatus.Success)
                            return ParasolidConstants.PK_ERROR_eval_failure;
                        samples[count++] = value[0];
                    }
                }
            }
            sampleOffsets[^1] = count;
            if (sampleOffsets[1] < 3) return ParasolidConstants.PK_ERROR_bad_loop;
            origin = samples[0];
            for (var i = 0; i < sampleOffsets[1]; i++)
            {
                var j = i+1 == sampleOffsets[1] ? 0 : i+1;
                normal = ProfileAdd(normal, ProfileCross(ProfileSubtract(samples[i], origin), ProfileSubtract(samples[j], origin)));
            }
            if (!ProfileNormalize(ref normal)) return ParasolidConstants.PK_ERROR_bad_loop;
            reference = ProfileSubtract(samples[1], origin);
            if (!ProfileNormalize(ref reference)) return ParasolidConstants.PK_ERROR_bad_loop;
            var transverse = ProfileCross(normal, reference);
            for (var i = 0; i < edges.Length; i++)
                if (!ProfileCurvePlanar(Edges[edges[i]].CurveTag, origin, normal)) return ParasolidConstants.PK_ERROR_bad_parameter;
            var certified=CertifyProfileCurves(edges,normal);
            if(certified!=0)return certified;
            for (var loop = 0; loop < offsets.Length-1; loop++)
            {
                double area = 0;
                for (var i = sampleOffsets[loop]; i < sampleOffsets[loop+1]; i++)
                {
                    var j = i+1 == sampleOffsets[loop+1] ? sampleOffsets[loop] : i+1;
                    area += ProfileDot(ProfileCross(ProfileSubtract(samples[i], origin), ProfileSubtract(samples[j], origin)), normal);
                    for (var otherLoop = 0; otherLoop <= loop; otherLoop++)
                        for (var k = sampleOffsets[otherLoop]; k < sampleOffsets[otherLoop+1]; k++)
                        {
                            var l = k+1 == sampleOffsets[otherLoop+1] ? sampleOffsets[otherLoop] : k+1;
                            if (otherLoop == loop && (k >= i || l == i || j == k)) continue;
                            if (ProfileSegmentsIntersect(samples[i], samples[j], samples[k], samples[l], normal))
                                return ParasolidConstants.PK_ERROR_bad_loop;
                        }
                }
                if (Math.Abs(area) <= 1e-16) return ParasolidConstants.PK_ERROR_bad_loop;
                if (loop > 0)
                {
                    if (!ProfileContains(samples[..sampleOffsets[1]], samples[sampleOffsets[loop]], reference, transverse))
                        return ParasolidConstants.PK_ERROR_bad_loop;
                    for (var hole = 1; hole < loop; hole++)
                        if (ProfileContains(samples.Slice(sampleOffsets[hole], sampleOffsets[hole+1]-sampleOffsets[hole]), samples[sampleOffsets[loop]], reference, transverse)
                            || ProfileContains(samples.Slice(sampleOffsets[loop], sampleOffsets[loop+1]-sampleOffsets[loop]), samples[sampleOffsets[hole]], reference, transverse))
                            return ParasolidConstants.PK_ERROR_bad_loop;
                    if (area > 0)
                    {
                        // Reverse the caller's initial traversal to give holes
                        // the opposite boundary orientation to the outer loop.
                        var start = offsets[loop]; var end = offsets[loop+1]-1;
                        // Reordering requires the writable ordered edge buffer.
                        // The original span comes from a command-local array.
                        var mutable = edges;
                        while (start < end)
                        {
                            (mutable[start],mutable[end]) = (mutable[end],mutable[start]);
                            (directions[start],directions[end]) = (-directions[end],-directions[start]);
                            start++; end--;
                        }
                        if (start == end) directions[start] = -directions[start];
                    }
                }
            }
            return 0;
        }
        finally { scratch.Return(workspace); }
    }

    private static bool ProfileCurvePlanar(CurveTag tag, KernelVector3 origin, KernelVector3 normal)
    {
        ref readonly var curve = ref Curves[GetCurveSlotByTag(tag)];
        if (curve.Class == CurveClass.Line)
        {
            ref readonly var d = ref LineDataPool[curve.DataIndex];
            return Math.Abs((d.LocationX-origin.X)*normal.X + (d.LocationY-origin.Y)*normal.Y + (d.LocationZ-origin.Z)*normal.Z) <= 1e-8
                && Math.Abs(d.AxisX*normal.X+d.AxisY*normal.Y+d.AxisZ*normal.Z) <= 1e-10;
        }
        if (curve.Class == CurveClass.Circle)
        {
            ref readonly var d = ref CircleDataPool[curve.DataIndex];
            return Math.Abs((d.CenterX-origin.X)*normal.X + (d.CenterY-origin.Y)*normal.Y + (d.CenterZ-origin.Z)*normal.Z) <= 1e-8
                && Math.Abs(Math.Abs(d.AxisX*normal.X+d.AxisY*normal.Y+d.AxisZ*normal.Z)-1) <= 1e-10;
        }
        if (curve.Class == CurveClass.Ellipse)
        {
            ref readonly var d = ref EllipseDataPool[curve.DataIndex];
            return Math.Abs((d.CenterX-origin.X)*normal.X + (d.CenterY-origin.Y)*normal.Y + (d.CenterZ-origin.Z)*normal.Z) <= 1e-8
                && Math.Abs(Math.Abs(d.AxisX*normal.X+d.AxisY*normal.Y+d.AxisZ*normal.Z)-1) <= 1e-10;
        }
        if (curve.Class != CurveClass.BCurve) return false;
        ref readonly var b = ref BCurveDataStore[curve.DataIndex];
        var poles = b.PolesSpan();
        for (var i = 0; i < b.NVertices; i++)
        {
            var index = i*b.VertexDim; var weight = b.IsRational != 0 ? poles[index+3] : 1;
            if (!(weight > 0) || Math.Abs((poles[index]/weight-origin.X)*normal.X
                +(poles[index+1]/weight-origin.Y)*normal.Y+(poles[index+2]/weight-origin.Z)*normal.Z) > 1e-8) return false;
        }
        return true;
    }

    private static bool TryProfileEdgeRange(in EdgeRecord edge, out double a, out double b)
    {
        a = edge.ParameterStart; b = edge.ParameterEnd;
        if (edge.HasParameterRange != 0) return double.IsFinite(a) && double.IsFinite(b) && b > a;
        var curveSlot = GetCurveSlotByTag(edge.CurveTag);
        if (curveSlot < 0) return false;
        ref readonly var curve = ref Curves[curveSlot];
        a = curve.TMin; b = curve.TMax;
        if (curve.Class == CurveClass.Line && edge.StartVertex >= 0 && edge.EndVertex >= 0)
        {
            ref readonly var line = ref LineDataPool[curve.DataIndex];
            var p = Points[GetPointSlotByTag(Vertices[edge.StartVertex].PointTag)].Position;
            var q = Points[GetPointSlotByTag(Vertices[edge.EndVertex].PointTag)].Position;
            a = (p.X-line.LocationX)*line.AxisX+(p.Y-line.LocationY)*line.AxisY+(p.Z-line.LocationZ)*line.AxisZ;
            b = (q.X-line.LocationX)*line.AxisX+(q.Y-line.LocationY)*line.AxisY+(q.Z-line.LocationZ)*line.AxisZ;
            if (curve.Sense == ParasolidConstants.PK_TOPOL_sense_negative_c) (a, b) = (b, a);
            return b > a;
        }
        return edge.StartVertex < 0 && edge.EndVertex < 0 && curve.Class is CurveClass.Circle or CurveClass.Ellipse;
    }

    private static bool SnapshotProfileTopology(BodySlot slot)
    {
        var session = State.Session;
        ref var body = ref Bodies[slot];
        if (!session->TrySnapshot(ref body) || !SnapshotBoundaryGeometry(slot)) return false;
        var region = body.FirstRegion;
        for (var i=0; i<body.RegionCount; i++,region=Regions[region].NextInBody) if (!session->TrySnapshot(ref Regions[region])) return false;
        var shell = body.FirstShell;
        for (var i=0; i<body.ShellCount; i++,shell=Shells[shell].NextInBody) if (!session->TrySnapshot(ref Shells[shell])) return false;
        var edge = body.FirstEdgeBody;
        for (var i=0; i<body.EdgeCountBody; i++,edge=Edges[edge].NextInBody)
        {
            if (!session->TrySnapshot(ref Edges[edge])) return false;
            var fin=Edges[edge].FirstFinEdge;
            for (var j=0;j<Edges[edge].FinCount;j++,fin=Fins[fin].NextOfEdge) if(!session->TrySnapshot(ref Fins[fin]))return false;
        }
        var vertex=body.FirstVertexBody;
        for(var i=0;i<body.VertexCountBody;i++,vertex=Vertices[vertex].NextInBody) if(!session->TrySnapshot(ref Vertices[vertex]))return false;
        return true;
    }

    private static bool ProfileSegmentsIntersect(KernelVector3 a, KernelVector3 b, KernelVector3 c, KernelVector3 d, KernelVector3 n)
    {
        var ab=ProfileSubtract(b,a); var cd=ProfileSubtract(d,c);
        var ac=ProfileSubtract(c,a); var ad=ProfileSubtract(d,a);
        var ca=ProfileSubtract(a,c); var cb=ProfileSubtract(b,c);
        var s1=ProfileDot(ProfileCross(ab,ac),n); var s2=ProfileDot(ProfileCross(ab,ad),n);
        var s3=ProfileDot(ProfileCross(cd,ca),n); var s4=ProfileDot(ProfileCross(cd,cb),n);
        if (s1*s2<0 && s3*s4<0) return true;
        const double tolerance=1e-12;
        return Math.Abs(s1)<=tolerance && ProfileDot(ac,ProfileSubtract(c,b))<=tolerance
            || Math.Abs(s2)<=tolerance && ProfileDot(ad,ProfileSubtract(d,b))<=tolerance
            || Math.Abs(s3)<=tolerance && ProfileDot(ca,ProfileSubtract(a,d))<=tolerance
            || Math.Abs(s4)<=tolerance && ProfileDot(cb,ProfileSubtract(b,d))<=tolerance;
    }

    private static bool ProfileContains(ReadOnlySpan<KernelVector3> polygon, KernelVector3 point, KernelVector3 x, KernelVector3 y)
    {
        var inside=false;
        for(var i=0;i<polygon.Length;i++)
        {
            var j=(i+1)%polygon.Length;
            var a=ProfileSubtract(polygon[i],point); var b=ProfileSubtract(polygon[j],point);
            var ay=ProfileDot(a,y); var by=ProfileDot(b,y);
            if ((ay>0)!=(by>0) && ProfileDot(a,x)+ (ProfileDot(b,x)-ProfileDot(a,x))*(-ay)/(by-ay)>0) inside=!inside;
        }
        return inside;
    }

    private static KernelVector3 ProfileAdd(KernelVector3 a, KernelVector3 b) => Vector(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    private static KernelVector3 ProfileSubtract(KernelVector3 a, KernelVector3 b) => Vector(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    private static KernelVector3 ProfileScale(KernelVector3 a,double scale) => Vector(a.X*scale,a.Y*scale,a.Z*scale);
    private static double ProfileDot(KernelVector3 a,KernelVector3 b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    private static KernelVector3 ProfileCross(KernelVector3 a,KernelVector3 b) => Vector(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
    private static bool ProfileNormalize(ref KernelVector3 v) {var length=Math.Sqrt(ProfileDot(v,v));if(!double.IsFinite(length)||length<=1e-15)return false;v=ProfileScale(v,1/length);return true;}
    private static bool ProfileUnit(in KernelVector3 v) => ProfileFinite(in v)&&Math.Abs(ProfileDot(v,v)-1)<=1e-9;
    private static bool ProfileFinite(in KernelVector3 v) => double.IsFinite(v.X)&&double.IsFinite(v.Y)&&double.IsFinite(v.Z);
}

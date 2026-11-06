using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int ReadAffine(TransfTag tag, out KernelAffineTransform matrix)
    {
        matrix=default;
        if(!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if(!IsValidTag(tag) || TagRec(tag).Pool!=(byte)PoolKind.Transform) return ParasolidConstants.PK_ERROR_unknown_class;
        ref var record=ref Transforms[TagRec(tag).Slot];
        if(record.Header.Partition!=CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
        for(var i=0;i<16;i++) if(!double.IsFinite(record.Matrix[i])) return ParasolidConstants.PK_ERROR_wrong_transf;
        if(record.Matrix[12]!=0 || record.Matrix[13]!=0 || record.Matrix[14]!=0 || record.Matrix[15]==0) return ParasolidConstants.PK_ERROR_unsupported_transf;
        // PK stores equal scale in the homogeneous denominator (for example,
        // a scale of two has an identity 3x3 block and matrix[15] == .5).
        var w=record.Matrix[15];
        matrix=new() { M00=record.Matrix[0]/w,M01=record.Matrix[1]/w,M02=record.Matrix[2]/w,M03=record.Matrix[3]/w,
            M10=record.Matrix[4]/w,M11=record.Matrix[5]/w,M12=record.Matrix[6]/w,M13=record.Matrix[7]/w,
            M20=record.Matrix[8]/w,M21=record.Matrix[9]/w,M22=record.Matrix[10]/w,M23=record.Matrix[11]/w };
        return matrix.IsFinite?0:ParasolidConstants.PK_ERROR_failed_to_transform;
    }

    internal static int TryTransformEntityCore(EntityTag entity, in KernelAffineTransform transform, double tolerance=0)
    {
        ReadOnlySpan<EntityTag> entities=stackalloc EntityTag[1] { entity };
        return TryTransformEntitiesCore(entities,transform,tolerance);
    }

    // PK's modify mode requires the caller to list every defining geometry,
    // including children that are also referenced by another selected owner.
    // Omitting a child or an outside owner is PK_ERROR_bad_sharing; silently
    // walking into it would change geometry the caller did not select.
    private static int ValidateExplicitTransformClosure(ReadOnlySpan<GeomTag> geometries)
    {
        Span<GeomTag> dependencies=stackalloc GeomTag[3];
        for(var i=0;i<geometries.Length;i++)
        {
            if(!IsValidTag(geometries[i]))return ParasolidConstants.PK_ERROR_unknown_class;
            var record=TagRec(geometries[i]);var pool=(PoolKind)record.Pool;
            if(pool is not (PoolKind.Point or PoolKind.Curve or PoolKind.Surface))return ParasolidConstants.PK_ERROR_unsuitable_entity;
            if(PoolPartitionOf(pool,record.Slot)!=CurrentPartition)return ParasolidConstants.PK_ERROR_not_in_same_partition;
            if(GeometryOwnerCount(pool,record.Slot)!=0)return ParasolidConstants.PK_ERROR_is_attached;
            var count=GetGeometryDependencyTags(pool,record.Slot,dependencies);
            for(var j=0;j<count;j++)if(!geometries.Contains(dependencies[j]))return ParasolidConstants.PK_ERROR_bad_sharing;
        }
        for(var i=0;i<geometries.Length;i++)
        {
            var record=TagRec(geometries[i]);var expected=GeometricOwnerCount((PoolKind)record.Pool,record.Slot);
            if(expected==0)continue;
            var owners=0;
            for(var j=0;j<geometries.Length;j++)
            {
                if(geometries[..j].Contains(geometries[j]))continue;
                var owner=TagRec(geometries[j]);var count=GetGeometryDependencyTags((PoolKind)owner.Pool,owner.Slot,dependencies);
                for(var k=0;k<count;k++)if(dependencies[k]==geometries[i])owners++;
            }
            if(owners<expected)return ParasolidConstants.PK_ERROR_bad_sharing;
        }
        return 0;
    }

    internal static int TryTransformEntitiesCore(ReadOnlySpan<EntityTag> entities, in KernelAffineTransform transform, double tolerance=0)
    {
        if(!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if(!double.IsFinite(tolerance) || tolerance<0) return ParasolidConstants.PK_ERROR_bad_tolerance;
        if(!transform.TrySimilarity(out var scale,out var parity)) return ParasolidConstants.PK_ERROR_unsupported_transf;
        var ownsScratch=EnsureCommandScratch();
        var workspace=CommandScratchBorrow(Math.Min(8192,Math.Max(128,NextTagValue)));
        if(workspace.IsEmpty) { if(ownsScratch)ReleaseCommandScratch(State.Session); return ParasolidConstants.PK_ERROR_memory_full; }
        try
        {
            var visited=new EntityCopyContext { Pairs=MemoryMarshal.Cast<double,EntityCopyPair>(workspace) };
            for(var i=0;i<entities.Length;i++)
            {
                // A 2D B-curve gets its map from its SP-curve's support, even
                // when the caller lists that dependency before its owner.
                var curve=GetCurveSlotByTag(entities[i]);
                if(curve>=0&&Curves[curve].Class==CurveClass.BCurve)
                {
                    var data=BCurveDataStore[Curves[curve].DataIndex];
                    if(data.VertexDim-(data.IsRational!=0?1:0)==2)continue;
                }
                var error=TransformEntity(entities[i],transform,scale,parity,ref visited,0);
                if(error!=0)return error;
            }
            for(var i=0;i<entities.Length;i++)
            {
                var error=TransformEntity(entities[i],transform,scale,parity,ref visited,0);
                if(error!=0)return error;
            }
            return 0;
        }
        finally { CommandScratchReturn(workspace); if(ownsScratch)ReleaseCommandScratch(State.Session); }
    }

    // Variable-length geometry is journaled before mutation, just like its
    // metadata. Undo restores the existing block; no replacement can leave a
    // snapshot pointing at an already freed allocation.
    private static bool SnapshotPayload(DataSlot handle, nuint bytes, PartitionSlot partition)
    {
        if(handle<0 || bytes==0) return true;
        var target=DereferenceBlock(handle); var copy=State.Session->Blocks.TryAllocate(bytes);
        if(copy==null) return false;
        new ReadOnlySpan<byte>(target,checked((int)bytes)).CopyTo(new Span<byte>(copy,checked((int)bytes)));
        if(!State.Session->TryAppendUndo(SessionData.UndoKind.FieldSnapshot,0,partition,0,0,0,copy,bytes))
        { State.Session->Blocks.Free(copy); return false; }
        State.Session->UndoEntries[State.Session->UndoEntryCount-1].Target=target;
        ThreadContext()->UndoSnapshotCount++;
        return true;
    }
    private static bool FiniteVector(in KernelVector3 value) => double.IsFinite(value.X)&&double.IsFinite(value.Y)&&double.IsFinite(value.Z);
    private static bool FiniteTriple(double x,double y,double z) => double.IsFinite(x)&&double.IsFinite(y)&&double.IsFinite(z);
    private static bool PositiveFinite(double value) => double.IsFinite(value)&&value>0;
    private static KernelVector3 Scaled(in KernelVector3 value,double factor) => new() { X=value.X*factor,Y=value.Y*factor,Z=value.Z*factor };
    private static KernelSense ReverseSense(KernelSense sense) => sense==ParasolidConstants.PK_TOPOL_sense_negative_c
        ?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
    private static void TransformLocation(ref double x,ref double y,ref double z,in KernelAffineTransform m)
    { var p=m.TransformPoint(new KernelVector3 { X=x,Y=y,Z=z }); x=p.X;y=p.Y;z=p.Z; }
    private static void TransformDirection(ref double x,ref double y,ref double z,in KernelAffineTransform m,double scale,double orientation=1)
    { var p=Scaled(m.TransformUnitDirection(new KernelVector3 { X=x,Y=y,Z=z },scale),orientation); x=p.X;y=p.Y;z=p.Z; }
    private static double CurveParameterScale(CurveTag tag,double scale,int depth=0)
    {
        if(depth>128) return 1;
        var slot=GetCurveSlotByTag(tag); if(slot<0) return 1;
        var curve=Curves[slot];
        return curve.Class==CurveClass.Line?scale:curve.Class==CurveClass.TRCurve?CurveParameterScale(TrCurveDataPool[curve.DataIndex].BasisCurveTag,scale,depth+1):1;
    }

    private static int TransformEntity(EntityTag tag,in KernelAffineTransform m,double scale,double parity,ref EntityCopyContext visited,int depth)
    {
        if(tag<=0) return 0;
        if(visited.Find(tag,out _)) return 0;
        if(depth>128) return ParasolidConstants.PK_ERROR_bad_value;
        if(!IsValidTag(tag)) return ParasolidConstants.PK_ERROR_unknown_class;
        var entry=TagRec(tag); var pool=(PoolKind)entry.Pool;
        if(PoolPartitionOf(pool,entry.Slot)!=CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
        if(!visited.Add(tag,tag)) return ParasolidConstants.PK_ERROR_memory_full;
        if(pool==PoolKind.Point)
        {
            ref var point=ref Points[entry.Slot]; if(!State.Session->TrySnapshot(ref point)) return ParasolidConstants.PK_ERROR_memory_full;
            point.Position=m.TransformPoint(point.Position); return FiniteVector(point.Position)?0:ParasolidConstants.PK_ERROR_failed_to_transform;
        }
        if(pool==PoolKind.Body)
        {
            if(Xt.IsOpaque(entry.Slot)) return ParasolidConstants.PK_ERROR_not_implemented;
            var body=Bodies[entry.Slot]; int error;
            var face=body.FirstFaceBody;
            for(var i=0;i<body.FaceCountBody;i++,face=Faces[face].NextInBody)
            {
                ref var f=ref Faces[face]; if(!State.Session->TrySnapshot(ref f))return ParasolidConstants.PK_ERROR_memory_full;
                f.Tolerance*=scale;if(!double.IsFinite(f.Tolerance))return ParasolidConstants.PK_ERROR_failed_to_transform;
                error=TransformEntity(f.SurfTag,m,scale,parity,ref visited,depth+1); if(error!=0) return error;
            }
            var edge=body.FirstEdgeBody;
            for(var i=0;i<body.EdgeCountBody;i++,edge=Edges[edge].NextInBody)
            {
                ref var e=ref Edges[edge]; if(!State.Session->TrySnapshot(ref e)) return ParasolidConstants.PK_ERROR_memory_full;
                var parameterScale=CurveParameterScale(e.CurveTag,scale);
                error=TransformEntity(e.CurveTag,m,scale,parity,ref visited,depth+1); if(error!=0) return error;
                e.ParameterStart*=parameterScale; e.ParameterEnd*=parameterScale; e.Tolerance*=scale;
                if(!double.IsFinite(e.ParameterStart)||!double.IsFinite(e.ParameterEnd)||!double.IsFinite(e.Tolerance))return ParasolidConstants.PK_ERROR_failed_to_transform;
                var fin=e.FirstFinEdge;
                for(var j=0;j<e.FinCount;j++,fin=Fins[fin].NextOfEdge)
                { error=TransformEntity(Fins[fin].Curve,m,scale,parity,ref visited,depth+1); if(error!=0) return error; }
            }
            var vertex=body.FirstVertexBody;
            for(var i=0;i<body.VertexCountBody;i++,vertex=Vertices[vertex].NextInBody)
            {
                ref var v=ref Vertices[vertex]; if(!State.Session->TrySnapshot(ref v)) return ParasolidConstants.PK_ERROR_memory_full;
                v.Tolerance*=scale;if(!double.IsFinite(v.Tolerance))return ParasolidConstants.PK_ERROR_failed_to_transform;
                error=TransformEntity(v.PointTag,m,scale,parity,ref visited,depth+1); if(error!=0) return error;
            }
            var construction=body.FirstConstructionSurface>=0?Surfaces[body.FirstConstructionSurface].Header.Tag:0;
            for(var i=0;i<body.ConstructionSurfaceCount;i++,construction=GetConstructionNext(construction))
            { error=TransformEntity(construction,m,scale,parity,ref visited,depth+1);if(error!=0)return error; }
            construction=body.FirstConstructionCurve>=0?Curves[body.FirstConstructionCurve].Header.Tag:0;
            for(var i=0;i<body.ConstructionCurveCount;i++,construction=GetConstructionNext(construction))
            { error=TransformEntity(construction,m,scale,parity,ref visited,depth+1);if(error!=0)return error; }
            if(parity<0)
            { error=ReflectBodyTopology(entry.Slot);if(error!=0)return error; }
            SessionMemoryOwner.DropBodyXt(entry.Slot);
            return 0;
        }
        if(pool==PoolKind.Curve) return TransformCurve(entry.Slot,m,scale,parity,ref visited,depth+1);
        if(pool==PoolKind.Surface) return TransformSurface(entry.Slot,m,scale,parity,ref visited,depth+1);
        return ParasolidConstants.PK_ERROR_unsuitable_entity;
    }

    private static int ReflectBodyTopology(BodySlot bodySlot)
    {
        var body=Bodies[bodySlot];if(body.FaceCountBody==0)return 0;
        var face=body.FirstFaceBody;
        for(var i=0;i<body.FaceCountBody;i++,face=Faces[face].NextInBody)
        {
            var loop=Faces[face].FirstLoop;
            for(var j=0;j<Faces[face].LoopCount;j++,loop=Loops[loop].NextInFace)
            {
                ref var l=ref Loops[loop];if(!State.Session->TrySnapshot(ref l))return ParasolidConstants.PK_ERROR_memory_full;
                var fin=l.FirstFin;
                for(var k=0;k<l.FinCount;k++)
                {
                    ref var f=ref Fins[fin];var next=f.NextInLoop;
                    if(!State.Session->TrySnapshot(ref f))return ParasolidConstants.PK_ERROR_memory_full;
                    (f.NextInLoop,f.PrevInLoop)=(f.PrevInLoop,f.NextInLoop);fin=next;
                }
                (l.FirstFin,l.LastFin)=(l.LastFin,l.FirstFin);
            }
        }
        var edge=body.FirstEdgeBody;
        for(var i=0;i<body.EdgeCountBody;i++,edge=Edges[edge].NextInBody)
        {
            ref var e=ref Edges[edge];var fin=e.FirstFinEdge;var hasFace=false;
            for(var j=0;j<e.FinCount;j++,fin=Fins[fin].NextOfEdge)hasFace|=Fins[fin].Loop>=0;
            if(!hasFace)continue;
            if(!State.Session->TrySnapshot(ref e))return ParasolidConstants.PK_ERROR_memory_full;
            fin=e.FirstFinEdge;FinSlot positive=-1;
            for(var j=0;j<e.FinCount;j++)
            {
                ref var f=ref Fins[fin];var next=f.NextOfEdge;
                if(!State.Session->TrySnapshot(ref f))return ParasolidConstants.PK_ERROR_memory_full;
                f.Sense=f.Sense=='+'?'-':'+';
                f.Vertex=f.Sense=='+'?e.EndVertex:e.StartVertex;
                (f.NextOfEdge,f.PrevOfEdge)=(f.PrevOfEdge,f.NextOfEdge);f.Other=f.NextOfEdge;
                if(f.Sense=='+'&&positive<0)positive=fin;
                fin=next;
            }
            if(positive>=0)e.FirstFinEdge=positive;
            if(e.FirstFinEdge>=0)e.LastFinEdge=Fins[e.FirstFinEdge].PrevOfEdge;
        }
        // The forward endpoint of every reflected face fin changed; rebuild
        // the vertex incidence rings rather than leaving their old heads.
        var vertex=body.FirstVertexBody;
        for(var i=0;i<body.VertexCountBody;i++,vertex=Vertices[vertex].NextInBody)
        {
            ref var v=ref Vertices[vertex];if(!State.Session->TrySnapshot(ref v))return ParasolidConstants.PK_ERROR_memory_full;
            if(v.FirstFinVertex<0||Fins[v.FirstFinVertex].Edge>=0)v.FirstFinVertex=v.LastFinVertex=-1;
        }
        edge=body.FirstEdgeBody;
        for(var i=0;i<body.EdgeCountBody;i++,edge=Edges[edge].NextInBody)
        {
            var fin=Edges[edge].FirstFinEdge;
            for(var j=0;j<Edges[edge].FinCount;j++,fin=Fins[fin].NextOfEdge)
            {
                ref var f=ref Fins[fin];if(!State.Session->TrySnapshot(ref f))return ParasolidConstants.PK_ERROR_memory_full;
                f.NextAtVertex=f.PrevAtVertex=-1;var sense=f.Sense;
                if(f.Vertex>=0)AppendFinToVertex(f.Vertex,fin);
                f.Sense=sense;
            }
        }
        return 0;
    }

    private static int TransformPoles(Span<double> poles,int dimension,bool rational,in KernelAffineTransform m)
    {
        var spatial=dimension-(rational?1:0);
        if(spatial!=3) return ParasolidConstants.PK_ERROR_bad_dimension;
        for(var i=0;i<poles.Length;i+=dimension)
        {
            var weight=rational?poles[i+3]:1;
            // PK rational poles are homogeneous, so translation is multiplied
            // by the unmodified weight. Derivatives/knots retain their domain.
            var p=m.TransformVector(new KernelVector3 { X=poles[i],Y=poles[i+1],Z=poles[i+2] });
            p.X+=m.M03*weight;p.Y+=m.M13*weight;p.Z+=m.M23*weight;
            if(!FiniteVector(p)) return ParasolidConstants.PK_ERROR_failed_to_transform;
            poles[i]=p.X;poles[i+1]=p.Y;poles[i+2]=p.Z;
        }
        return 0;
    }

    private static int TransformCurve(CurveSlot slot,in KernelAffineTransform m,double scale,double parity,ref EntityCopyContext visited,int depth)
    {
        ref var curve=ref Curves[slot]; if(!State.Session->TrySnapshot(ref curve)) return ParasolidConstants.PK_ERROR_memory_full;
        var index=curve.DataIndex; int error;
        switch(curve.Class)
        {
            case CurveClass.Line:
            {
                ref var d=ref LineDataPool[index]; if(!State.Session->TrySnapshot(ref d)) return ParasolidConstants.PK_ERROR_memory_full;
                TransformLocation(ref d.LocationX,ref d.LocationY,ref d.LocationZ,m); TransformDirection(ref d.AxisX,ref d.AxisY,ref d.AxisZ,m,scale);
                curve.TMin*=scale;curve.TMax*=scale;
                return FiniteTriple(d.LocationX,d.LocationY,d.LocationZ)&&FiniteTriple(d.AxisX,d.AxisY,d.AxisZ)
                    &&double.IsFinite(curve.TMin)&&double.IsFinite(curve.TMax)?0:ParasolidConstants.PK_ERROR_failed_to_transform;
            }
            case CurveClass.Circle:
            {
                ref var d=ref CircleDataPool[index]; if(!State.Session->TrySnapshot(ref d)) return ParasolidConstants.PK_ERROR_memory_full;
                TransformLocation(ref d.CenterX,ref d.CenterY,ref d.CenterZ,m); TransformDirection(ref d.AxisX,ref d.AxisY,ref d.AxisZ,m,scale,parity);
                TransformDirection(ref d.RefDirX,ref d.RefDirY,ref d.RefDirZ,m,scale);d.Radius*=scale;
                return FiniteTriple(d.CenterX,d.CenterY,d.CenterZ)&&FiniteTriple(d.AxisX,d.AxisY,d.AxisZ)
                    &&FiniteTriple(d.RefDirX,d.RefDirY,d.RefDirZ)&&PositiveFinite(d.Radius)?0:ParasolidConstants.PK_ERROR_failed_to_transform;
            }
            case CurveClass.Ellipse:
            {
                ref var d=ref EllipseDataPool[index]; if(!State.Session->TrySnapshot(ref d)) return ParasolidConstants.PK_ERROR_memory_full;
                TransformLocation(ref d.CenterX,ref d.CenterY,ref d.CenterZ,m);TransformDirection(ref d.AxisX,ref d.AxisY,ref d.AxisZ,m,scale,parity);
                TransformDirection(ref d.RefDirX,ref d.RefDirY,ref d.RefDirZ,m,scale);d.R1*=scale;d.R2*=scale;
                return FiniteTriple(d.CenterX,d.CenterY,d.CenterZ)&&FiniteTriple(d.AxisX,d.AxisY,d.AxisZ)
                    &&FiniteTriple(d.RefDirX,d.RefDirY,d.RefDirZ)&&PositiveFinite(d.R1)&&PositiveFinite(d.R2)?0:ParasolidConstants.PK_ERROR_failed_to_transform;
            }
            case CurveClass.BCurve:
            {
                ref var d=ref BCurveDataStore[index];
                if(!SnapshotPayload(d.VertexBlock,(nuint)(d.NVertices*d.VertexDim*8),curve.Header.Partition)) return ParasolidConstants.PK_ERROR_memory_full;
                return TransformPoles(d.PolesSpan(),d.VertexDim,d.IsRational!=0,m);
            }
            case CurveClass.TRCurve:
            {
                ref var d=ref TrCurveDataPool[index]; if(!State.Session->TrySnapshot(ref d)) return ParasolidConstants.PK_ERROR_memory_full;
                var parameterScale=CurveParameterScale(d.BasisCurveTag,scale);
                error=TransformEntity(d.BasisCurveTag,m,scale,parity,ref visited,depth); if(error!=0) return error;
                d.Point1=m.TransformPoint(d.Point1);d.Point2=m.TransformPoint(d.Point2);d.Parm1*=parameterScale;d.Parm2*=parameterScale;
                curve.TMin*=parameterScale;curve.TMax*=parameterScale;
                return FiniteVector(d.Point1)&&FiniteVector(d.Point2)&&double.IsFinite(d.Parm1)&&double.IsFinite(d.Parm2)
                    &&double.IsFinite(curve.TMin)&&double.IsFinite(curve.TMax)&&curve.TMin<curve.TMax?0:ParasolidConstants.PK_ERROR_failed_to_transform;
            }
            case CurveClass.SPCurve:
            {
                var d=SpCurveDataPool[index]; error=TransformEntity(d.SurfTag,m,scale,parity,ref visited,depth); if(error!=0) return error;
                return TransformUvCurve(d.BCurveTag,d.SurfTag,scale,parity,ref visited);
            }
            case CurveClass.ICurve:
            {
                ref var d=ref ICurveDataPool[index]; if(!State.Session->TrySnapshot(ref d)) return ParasolidConstants.PK_ERROR_memory_full;
                if(d.ProgramBlock>0)
                {error=TransformNestedProgram(d.ProgramBlock,d.ProgramSurfaceCount,d.ProgramSpineCount,d.ProgramChartCount,d.ProgramScalarCount,curve.Header.Partition,m,scale,parity);if(error!=0)return error;}
                if(!SnapshotPayload(d.HvecBlock,(nuint)(d.HvecCount*3*8),curve.Header.Partition)
                    || !SnapshotPayload(d.UvValueBlock,(nuint)(d.UvValueCount*8),curve.Header.Partition)) return ParasolidConstants.PK_ERROR_memory_full;
                error=TransformEntity(d.Surface0Tag,m,scale,parity,ref visited,depth);if(error!=0)return error;
                error=TransformEntity(d.Surface1Tag,m,scale,parity,ref visited,depth);if(error!=0)return error;
                var vectors=new Span<KernelVector3>(DereferenceBlock(d.HvecBlock),d.HvecCount);
                for(var i=0;i<vectors.Length;i++) { vectors[i]=m.TransformPoint(vectors[i]); if(!FiniteVector(vectors[i])) return ParasolidConstants.PK_ERROR_failed_to_transform; }
                var uv=new Span<double>(DereferenceBlock(d.UvValueBlock),d.UvValueCount);var stride=UvStrideOf(d.UvType);
                // These are periodic chart point records. Surface extents and
                // SP-curve control nets retain their lifted parameter maps;
                // wrapping their individual bounds/poles would change them.
                for(var i=0;stride>0&&i<uv.Length;i+=stride)
                {
                    if(d.UvType is IntersectionUvType.First or IntersectionUvType.Both)
                    { if(!TransformUv(d.Surface0Tag,scale,parity,ref uv[i],ref uv[i+1],allowNull:true,canonical:true))return ParasolidConstants.PK_ERROR_failed_to_transform; }
                    if(d.UvType==IntersectionUvType.Second)
                    { if(!TransformUv(d.Surface1Tag,scale,parity,ref uv[i],ref uv[i+1],allowNull:true,canonical:true))return ParasolidConstants.PK_ERROR_failed_to_transform; }
                    else if(d.UvType==IntersectionUvType.Both)
                    { if(!TransformUv(d.Surface1Tag,scale,parity,ref uv[i+2],ref uv[i+3],allowNull:true,canonical:true))return ParasolidConstants.PK_ERROR_failed_to_transform; }
                }
                if(d.IsBlendContact!=0)
                {
                    if(!SnapshotPayload(d.ContactChartBlock,(nuint)((6*d.ChartCount-4)*8),curve.Header.Partition))return ParasolidConstants.PK_ERROR_memory_full;
                    var map=BlendContactMap(d);var scales=map.Slice(2*d.ChartCount,d.ChartCount-1);
                    var chords=MemoryMarshal.Cast<double,KernelVector3>(map[(3*d.ChartCount-1)..]);
                    for(var i=0;i<scales.Length;i++){scales[i]/=scale;chords[i]=ProfileScale(m.TransformVector(chords[i]),1/scale);}
                    if(parity<0){d.BlendContactSide=1-d.BlendContactSide;d.BlendContactReversed=(byte)(1-d.BlendContactReversed);}
                }
                if(parity<0&&d.IsBlendContact==0)
                {
                    // Each oriented support normal follows R*n. Reflection
                    // reverses their ordered cross product, so exchange the
                    // support roles (including every UV row) to retain the
                    // original chart direction and native parameter.
                    (d.Surface0Tag,d.Surface1Tag)=(d.Surface1Tag,d.Surface0Tag);
                    if(d.UvType==IntersectionUvType.Both)
                        for(var i=0;i<uv.Length;i+=4)
                        { (uv[i],uv[i+2])=(uv[i+2],uv[i]);(uv[i+1],uv[i+3])=(uv[i+3],uv[i+1]); }
                    else if(d.UvType==IntersectionUvType.First)d.UvType=IntersectionUvType.Second;
                    else if(d.UvType==IntersectionUvType.Second)d.UvType=IntersectionUvType.First;
                    (d.ParameterError,d.ParameterError1)=(d.ParameterError1,d.ParameterError);
                    (d.ParameterErrorProvided,d.ParameterError1Provided)=(d.ParameterError1Provided,d.ParameterErrorProvided);
                }
                d.BaseScale/=scale;d.ChordalError*=scale;
                return double.IsFinite(d.BaseScale)&&d.BaseScale!=0&&double.IsFinite(d.ChordalError)?0:ParasolidConstants.PK_ERROR_failed_to_transform;
            }
            default:return ParasolidConstants.PK_ERROR_not_implemented;
        }
    }

    private static bool TransformUv(SurfTag tag,double scale,double parity,ref double u,ref double v,int depth=0,bool allowNull=false,bool canonical=false)
    {
        if(allowNull&&double.IsNaN(u)&&double.IsNaN(v))return true;
        if(depth>128||!double.IsFinite(u)||!double.IsFinite(v))return false;
        var slot=GetSurfaceSlotByTag(tag);if(slot<0)return false;var s=Surfaces[slot];
        switch(s.Class)
        {
            case SurfaceClass.Plane:u*=scale;v*=scale*parity;break;
            case SurfaceClass.Cylinder:case SurfaceClass.Cone:u*=parity;v*=scale;if(canonical)CanonicalAngle(ref u);break;
            case SurfaceClass.Sphere:case SurfaceClass.Torus:u*=parity;if(canonical)CanonicalAngle(ref u);break;
            case SurfaceClass.Offset:return TransformUv(OffsetDataPool[s.DataIndex].BaseSurfTag,scale,parity,ref u,ref v,depth+1,canonical:canonical);
            case SurfaceClass.Swept:u*=CurveParameterScale(SweptDataPool[s.DataIndex].SectionCurveTag,scale);v*=scale;break;
            case SurfaceClass.Spun:u*=CurveParameterScale(SpunDataPool[s.DataIndex].ProfileCurveTag,scale);v*=parity;break;
            case SurfaceClass.BlendSurface:
                u*=CurveParameterScale(BlendedEdgeDataPool[s.DataIndex].SpineTag,scale);
                if(parity<0)v=1-v;break;
        }
        return double.IsFinite(u)&&double.IsFinite(v);
    }

    private static void CanonicalAngle(ref double angle)
    {
        if(angle>=0&&angle<=Math.Tau)return;
        angle%=Math.Tau;if(angle<0)angle+=Math.Tau;
    }

    // Offset normals are formed from Su x Sv, not the surface sense flag.
    // Under a similarity their sign is parity / det(UV-map). Analytic
    // rotational surfaces reverse u and plane/spun reverse v on reflection;
    // spline/swept parameterizations preserve UV orientation and reverse offset.
    private static double OffsetNormalOrientation(SurfTag tag,double parity,int depth=0)
    {
        if(depth>128)return double.NaN;
        var slot=GetSurfaceSlotByTag(tag);if(slot<0)return double.NaN;
        var surface=Surfaces[slot];
        return surface.Class switch
        {
            SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Cone or SurfaceClass.Sphere or SurfaceClass.Torus or SurfaceClass.Spun=>1,
            SurfaceClass.Offset=>OffsetNormalOrientation(OffsetDataPool[surface.DataIndex].BaseSurfTag,parity,depth+1),
            SurfaceClass.BSurface or SurfaceClass.Swept=>parity,
            _=>double.NaN,
        };
    }
    private static int TransformUvCurve(CurveTag tag,SurfTag surface,double scale,double parity,ref EntityCopyContext visited)
    {
        if(visited.Find(tag,out var previous))
        {
            if(previous>=0)return ParasolidConstants.PK_ERROR_bad_sharing;
            double u0=1,v0=1,u1=1,v1=1;
            if(!TransformUv(-previous,scale,parity,ref u0,ref v0)||!TransformUv(surface,scale,parity,ref u1,ref v1)
                ||u0!=u1||v0!=v1)return ParasolidConstants.PK_ERROR_bad_sharing;
            return 0;
        }
        // Remember the support's UV map when a 2D curve has several owners.
        if(!visited.Add(tag,-surface))return ParasolidConstants.PK_ERROR_memory_full;
        var slot=GetCurveSlotByTag(tag);if(slot<0||Curves[slot].Class!=CurveClass.BCurve)return ParasolidConstants.PK_ERROR_bad_dimension;
        ref var data=ref BCurveDataStore[Curves[slot].DataIndex];
        if(data.VertexDim!=(data.IsRational!=0?3:2))return ParasolidConstants.PK_ERROR_bad_dimension;
        if(!SnapshotPayload(data.VertexBlock,(nuint)(data.NVertices*data.VertexDim*8),Curves[slot].Header.Partition))return ParasolidConstants.PK_ERROR_memory_full;
        var poles=data.PolesSpan();
        for(var i=0;i<poles.Length;i+=data.VertexDim)
            if(!TransformUv(surface,scale,parity,ref poles[i],ref poles[i+1]))return ParasolidConstants.PK_ERROR_failed_to_transform;
        return 0;
    }

    private static int TransformSurface(SurfaceSlot slot,in KernelAffineTransform m,double scale,double parity,ref EntityCopyContext visited,int depth)
    {
        ref var surface=ref Surfaces[slot];if(!State.Session->TrySnapshot(ref surface))return ParasolidConstants.PK_ERROR_memory_full;
        var index=surface.DataIndex;int error;
        switch(surface.Class)
        {
            case SurfaceClass.BlendSurface:
            {
                ref var d=ref BlendedEdgeDataPool[index];if(!State.Session->TrySnapshot(ref d))return ParasolidConstants.PK_ERROR_memory_full;
                error=TransformEntity(d.Surface0Tag,m,scale,parity,ref visited,depth);if(error!=0)return error;
                error=TransformEntity(d.Surface1Tag,m,scale,parity,ref visited,depth);if(error!=0)return error;
                error=TransformEntity(d.SpineTag,m,scale,parity,ref visited,depth);if(error!=0)return error;
                error=TransformNestedProgram(d.ProgramBlock,d.ProgramSurfaceCount,d.ProgramSpineCount,d.ProgramChartCount,d.ProgramScalarCount,surface.Header.Partition,m,scale,parity);if(error!=0)return error;
                if(d.HvecCount>0)
                {
                    if(!SnapshotPayload(d.HvecBlock,(nuint)(d.HvecCount*3*8),surface.Header.Partition))return ParasolidConstants.PK_ERROR_memory_full;
                    var limits=new Span<KernelVector3>(DereferenceBlock(d.HvecBlock),d.HvecCount);
                    for(var i=0;i<limits.Length;i++)limits[i]=m.TransformPoint(limits[i]);
                }
                d.Range0*=scale;d.Range1*=scale;
                if(parity<0){(d.Surface0Tag,d.Surface1Tag)=(d.Surface1Tag,d.Surface0Tag);(d.Range0,d.Range1)=(d.Range1,d.Range0);}
                break;
            }
            case SurfaceClass.Plane:
            {
                ref var d=ref PlaneDataPool[index];if(!State.Session->TrySnapshot(ref d))return ParasolidConstants.PK_ERROR_memory_full;
                TransformLocation(ref d.LocationX,ref d.LocationY,ref d.LocationZ,m);TransformDirection(ref d.NormalX,ref d.NormalY,ref d.NormalZ,m,scale);
                TransformDirection(ref d.RefDirX,ref d.RefDirY,ref d.RefDirZ,m,scale);
                if(!FiniteTriple(d.LocationX,d.LocationY,d.LocationZ)||!FiniteTriple(d.NormalX,d.NormalY,d.NormalZ)
                    ||!FiniteTriple(d.RefDirX,d.RefDirY,d.RefDirZ))return ParasolidConstants.PK_ERROR_failed_to_transform;
                break;
            }
            case SurfaceClass.Cylinder:
            {
                ref var d=ref CylinderDataPool[index];if(!State.Session->TrySnapshot(ref d))return ParasolidConstants.PK_ERROR_memory_full;
                TransformLocation(ref d.LocationX,ref d.LocationY,ref d.LocationZ,m);TransformDirection(ref d.AxisX,ref d.AxisY,ref d.AxisZ,m,scale);
                TransformDirection(ref d.RefDirX,ref d.RefDirY,ref d.RefDirZ,m,scale);d.Radius*=scale;
                if(!FiniteTriple(d.LocationX,d.LocationY,d.LocationZ)||!FiniteTriple(d.AxisX,d.AxisY,d.AxisZ)
                    ||!FiniteTriple(d.RefDirX,d.RefDirY,d.RefDirZ)||!PositiveFinite(d.Radius))return ParasolidConstants.PK_ERROR_failed_to_transform;
                break;
            }
            case SurfaceClass.Cone:
            {
                ref var d=ref ConeDataPool[index];if(!State.Session->TrySnapshot(ref d))return ParasolidConstants.PK_ERROR_memory_full;
                TransformLocation(ref d.LocationX,ref d.LocationY,ref d.LocationZ,m);TransformDirection(ref d.AxisX,ref d.AxisY,ref d.AxisZ,m,scale);
                TransformDirection(ref d.RefDirX,ref d.RefDirY,ref d.RefDirZ,m,scale);d.Radius*=scale;
                if(!FiniteTriple(d.LocationX,d.LocationY,d.LocationZ)||!FiniteTriple(d.AxisX,d.AxisY,d.AxisZ)
                    ||!FiniteTriple(d.RefDirX,d.RefDirY,d.RefDirZ)||!double.IsFinite(d.Radius)||d.Radius<0)return ParasolidConstants.PK_ERROR_failed_to_transform;
                break;
            }
            case SurfaceClass.Sphere:
            {
                ref var d=ref SphereDataPool[index];if(!State.Session->TrySnapshot(ref d))return ParasolidConstants.PK_ERROR_memory_full;
                TransformLocation(ref d.CenterX,ref d.CenterY,ref d.CenterZ,m);TransformDirection(ref d.AxisX,ref d.AxisY,ref d.AxisZ,m,scale);
                TransformDirection(ref d.RefDirX,ref d.RefDirY,ref d.RefDirZ,m,scale);d.Radius*=scale;
                if(!FiniteTriple(d.CenterX,d.CenterY,d.CenterZ)||!FiniteTriple(d.AxisX,d.AxisY,d.AxisZ)
                    ||!FiniteTriple(d.RefDirX,d.RefDirY,d.RefDirZ)||!PositiveFinite(d.Radius))return ParasolidConstants.PK_ERROR_failed_to_transform;
                break;
            }
            case SurfaceClass.Torus:
            {
                ref var d=ref TorusDataPool[index];if(!State.Session->TrySnapshot(ref d))return ParasolidConstants.PK_ERROR_memory_full;
                TransformLocation(ref d.LocationX,ref d.LocationY,ref d.LocationZ,m);TransformDirection(ref d.AxisX,ref d.AxisY,ref d.AxisZ,m,scale);
                TransformDirection(ref d.RefDirX,ref d.RefDirY,ref d.RefDirZ,m,scale);d.MajorRadius*=scale;d.MinorRadius*=scale;
                if(!FiniteTriple(d.LocationX,d.LocationY,d.LocationZ)||!FiniteTriple(d.AxisX,d.AxisY,d.AxisZ)
                    ||!FiniteTriple(d.RefDirX,d.RefDirY,d.RefDirZ)||!PositiveFinite(d.MajorRadius)||!PositiveFinite(d.MinorRadius))return ParasolidConstants.PK_ERROR_failed_to_transform;
                break;
            }
            case SurfaceClass.BSurface:
            {
                ref var d=ref BSurfaceDataStore[index];if(!SnapshotPayload(d.VertexBlock,(nuint)(d.NUVertices*d.NVVertices*d.VertexDim*8),surface.Header.Partition))return ParasolidConstants.PK_ERROR_memory_full;
                if(parity<0)surface.Sense=ReverseSense(surface.Sense);
                return TransformPoles(d.PolesSpan(),d.VertexDim,d.IsRational!=0,m);
            }
            case SurfaceClass.Offset:
            {
                ref var d=ref OffsetDataPool[index];if(!State.Session->TrySnapshot(ref d))return ParasolidConstants.PK_ERROR_memory_full;
                var orientation=OffsetNormalOrientation(d.BaseSurfTag,parity);
                error=TransformEntity(d.BaseSurfTag,m,scale,parity,ref visited,depth);if(error!=0)return error;d.Offset*=scale*orientation;
                if(!double.IsFinite(d.Offset))return ParasolidConstants.PK_ERROR_failed_to_transform;
                surface.Sense=Surfaces[GetSurfaceSlotByTag(d.BaseSurfTag)].Sense;break;
            }
            case SurfaceClass.Swept:
            {
                ref var d=ref SweptDataPool[index];if(!State.Session->TrySnapshot(ref d))return ParasolidConstants.PK_ERROR_memory_full;
                error=TransformEntity(d.SectionCurveTag,m,scale,parity,ref visited,depth);if(error!=0)return error;
                d.Sweep=m.TransformUnitDirection(d.Sweep,scale);
                if(!FiniteVector(d.Sweep))return ParasolidConstants.PK_ERROR_failed_to_transform;
                if(parity<0)surface.Sense=ReverseSense(surface.Sense);break;
            }
            case SurfaceClass.Spun:
            {
                ref var d=ref SpunDataPool[index];if(!State.Session->TrySnapshot(ref d))return ParasolidConstants.PK_ERROR_memory_full;
                var parameterScale=CurveParameterScale(d.ProfileCurveTag,scale);
                error=TransformEntity(d.ProfileCurveTag,m,scale,parity,ref visited,depth);if(error!=0)return error;
                d.Base=m.TransformPoint(d.Base);d.Axis=m.TransformUnitDirection(d.Axis,scale);d.XAxis=m.TransformUnitDirection(d.XAxis,scale);
                d.Start=m.TransformPoint(d.Start);d.End=m.TransformPoint(d.End);d.StartParam*=parameterScale;d.EndParam*=parameterScale;
                if(!FiniteVector(d.Base)||!FiniteVector(d.Axis)||!FiniteVector(d.XAxis)||!FiniteVector(d.Start)||!FiniteVector(d.End)
                    ||!double.IsFinite(d.StartParam)||!double.IsFinite(d.EndParam))return ParasolidConstants.PK_ERROR_failed_to_transform;
                break;
            }
            default:return ParasolidConstants.PK_ERROR_not_implemented;
        }
        if(!TransformUv(surface.Header.Tag,scale,parity,ref surface.UMin,ref surface.VMin)
            ||!TransformUv(surface.Header.Tag,scale,parity,ref surface.UMax,ref surface.VMax))return ParasolidConstants.PK_ERROR_failed_to_transform;
        if(surface.UMin>surface.UMax)(surface.UMin,surface.UMax)=(surface.UMax,surface.UMin);
        if(surface.VMin>surface.VMax)(surface.VMin,surface.VMax)=(surface.VMax,surface.VMin);
        return 0;
    }
}

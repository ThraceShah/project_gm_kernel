using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct EntityCopyPair { internal EntityTag Source; internal EntityTag Copy; }
    private ref struct EntityCopyContext
    {
        internal Span<EntityCopyPair> Pairs;
        internal BufferCount Count;
        internal bool Find(EntityTag source, out EntityTag copy)
        {
            for (var i=0;i<Count;i++) if (Pairs[i].Source==source) { copy=Pairs[i].Copy; return true; }
            copy=0; return false;
        }
        internal bool Add(EntityTag source, EntityTag copy)
        {
            if (Count == Pairs.Length) return false;
            Pairs[Count++] = new() { Source=source, Copy=copy }; return true;
        }
    }

    internal static int TryCloneEntityCore(EntityTag source, out EntityTag clone)
    {
        Span<EntityTag> sources=stackalloc EntityTag[1] { source };
        Span<EntityTag> results=stackalloc EntityTag[1];
        var error=TryCloneEntitiesCore(sources,results);
        clone=error==0?results[0]:0;
        return error;
    }

    private static int CloneBoundaryCurve(in ModelingEdge edge,SurfTag first,SurfTag second,out CurveTag clone)
    {
        var source=edge.Curve;
        var record=Curves[GetCurveSlotByTag(source)];
        if(record.Class==CurveClass.ICurve&&first>0&&second>0)
        {
            var data=ICurveDataPool[record.DataIndex];
            // A BLEND_BOUND contact has its own dependency on the rolling
            // support inside the blend, in addition to the incident face.
            if(data.IsBlendContact!=0)return TryCloneEntityCore(source,out clone);
            var forward=SameChart(data.Surface0Tag,first)&&SameChart(data.Surface1Tag,second);
            var reverse=SameChart(data.Surface0Tag,second)&&SameChart(data.Surface1Tag,first);
            if(forward!=reverse||forward&&first==second)
            {
                var s0=forward?first:second;
                var s1=data.Surface1Tag!=data.Surface0Tag?(forward?second:first):s0;
                if(edge.StartVertex>=0&&edge.EndVertex>=0&&edge.StartVertex!=edge.EndVertex
                    &&TryCompactFiniteNestedIntersection(record,data,s0,s1,edge.Start,edge.End,out clone))
                    return 0;
                // Keep the native UV data and the compiled support equations
                // unchanged. Attach their dependencies to the actual face
                // uses, rather than to detached copies of the same charts.
                var context=new EntityCopyContext{Pairs=stackalloc EntityCopyPair[3]};
                context.Add(data.Surface0Tag,s0);
                if(data.Surface1Tag!=data.Surface0Tag)context.Add(data.Surface1Tag,s1);
                return CloneEntity(source,ref context,0,out clone);
            }
        }
        return TryCloneEntityCore(source,out clone);

        static bool SameChart(SurfTag a,SurfTag b)=>TryCertifySupportChart(a,b,out var certificate)&&certificate.Identity&&certificate.NormalRelation==1;
    }

    internal static int TryCloneEntitiesCore(ReadOnlySpan<EntityTag> sources,Span<EntityTag> clones)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if(clones.Length<sources.Length)return ParasolidConstants.PK_ERROR_bad_field_number;
        var ownsScratch=EnsureCommandScratch();
        var workspace = CommandScratchBorrow(Math.Min(8192, Math.Max(128, NextTagValue)));
        if (workspace.IsEmpty) { if(ownsScratch)ReleaseCommandScratch(State.Session); return ParasolidConstants.PK_ERROR_memory_full; }
        try
        {
            var context = new EntityCopyContext { Pairs = MemoryMarshal.Cast<double, EntityCopyPair>(workspace) };
            // One batch retains geometric sharing, including a dependency
            // explicitly present as another input and repeated input tags.
            for(var i=0;i<sources.Length;i++)
            {
                var error=CloneEntity(sources[i],ref context,0,out clones[i]);
                if(error!=0)return error;
            }
            return 0;
        }
        finally { CommandScratchReturn(workspace); if(ownsScratch)ReleaseCommandScratch(State.Session); }
    }

    private static int CloneEntity(EntityTag source, ref EntityCopyContext context, int depth, out EntityTag clone)
    {
        clone=0;
        if (context.Find(source,out clone)) return 0;
        if (depth>128) return ParasolidConstants.PK_ERROR_bad_value;
        if (!IsValidTag(source)) return ParasolidConstants.PK_ERROR_unknown_class;
        var entry=TagRec(source); var pool=(PoolKind)entry.Pool;
        if (PoolPartitionOf(pool, entry.Slot)!=CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
        int error;
        if (pool==PoolKind.Body) return CloneBody(source,ref context,depth,out clone);
        if (pool==PoolKind.Point)
        {
            var sf=new PK_POINT_sf_s(); var p=Points[entry.Slot].Position;
            sf.position.coord[0]=p.X; sf.position.coord[1]=p.Y; sf.position.coord[2]=p.Z;
            EntityTag result=0; error=PointCreate(&sf,&result); clone=result;
        }
        else if (pool==PoolKind.Transform)
        {
            PK_TRANSF_sf_s sf=default;
            for(var i=0;i<16;i++) sf.matrix[i]=Transforms[entry.Slot].Matrix[i];
            EntityTag result=0; error=TransfCreate(&sf,&result); clone=result;
        }
        else if(pool==PoolKind.Curve) error=CloneCurve(entry.Slot,ref context,depth+1,out clone);
        else if(pool==PoolKind.Surface) error=CloneSurface(entry.Slot,ref context,depth+1,out clone);
        else return ParasolidConstants.PK_ERROR_unsuitable_entity;
        if(error!=0) { clone=0; return error; }
        return context.Add(source,clone)?0:ParasolidConstants.PK_ERROR_memory_full;
    }

    private static int CloneData<T>(ref PagedEntityPool<T> pool, DataSlot source) where T:unmanaged
    {
        if (!pool.TryAllocate(out var copy)) return -1;
        var header=Unsafe.As<T,RecordHeader>(ref pool[copy]);
        pool[copy]=pool[source];
        Unsafe.As<T,RecordHeader>(ref pool[copy])=header;
        AssignPartition(ref Unsafe.As<T,RecordHeader>(ref pool[copy]),CurrentPartition);
        return copy;
    }
    private static int NewCopiedCurve(in CurveRecord source, DataSlot data, out EntityTag clone)
    {
        clone=0;
        if(data<0 || !Curves.TryAllocate(out var slot)) return ParasolidConstants.PK_ERROR_memory_full;
        ref var record=ref Curves[slot];
        AssignPartition(ref record.Header,CurrentPartition);
        record.Class=source.Class; record.DataIndex=data; record.TMin=source.TMin; record.TMax=source.TMax; record.Sense=source.Sense;
        record.OwnerEdge=-1;
        clone=AllocateTag(EntityClass.Curve,PoolKind.Curve,slot,record.Header.Generation);
        return clone>0?0:ParasolidConstants.PK_ERROR_memory_full;
    }
    private static int NewCopiedSurface(in SurfaceRecord source, DataSlot data, out EntityTag clone)
    {
        clone=0;
        if(data<0 || !Surfaces.TryAllocate(out var slot)) return ParasolidConstants.PK_ERROR_memory_full;
        ref var record=ref Surfaces[slot];
        AssignPartition(ref record.Header,CurrentPartition);
        record.Class=source.Class; record.DataIndex=data; record.UMin=source.UMin; record.UMax=source.UMax;
        record.VMin=source.VMin; record.VMax=source.VMax; record.Sense=source.Sense; record.OwnerFace=-1;
        clone=AllocateTag(EntityClass.Surface,PoolKind.Surface,slot,record.Header.Generation);
        return clone>0?0:ParasolidConstants.PK_ERROR_memory_full;
    }
    private static bool CopyPayload(DataSlot source, nuint bytes, ref DataSlot destination)
    {
        if(source<0 || bytes==0) { destination=-1; return true; }
        var p=State.Session->Blocks.TryAllocate(bytes);
        if(p==null) return false;
        new ReadOnlySpan<byte>(DereferenceBlock(source),checked((int)bytes)).CopyTo(new Span<byte>(p,checked((int)bytes)));
        destination=State.Session->Blocks.HandleOf(p);
        if(destination>0) return true;
        State.Session->Blocks.Free(p); destination=-1; return false;
    }

    private static int CloneCurve(CurveSlot sourceSlot, ref EntityCopyContext context, int depth, out EntityTag clone)
    {
        clone=0; var source=Curves[sourceSlot]; var data=source.DataIndex; int error;
        switch(source.Class)
        {
            case CurveClass.Line: return NewCopiedCurve(source,CloneData(ref LineDataPool,data),out clone);
            case CurveClass.Circle: return NewCopiedCurve(source,CloneData(ref CircleDataPool,data),out clone);
            case CurveClass.Ellipse: return NewCopiedCurve(source,CloneData(ref EllipseDataPool,data),out clone);
            case CurveClass.BCurve:
            {
                var from=BCurveDataStore[data]; var next=CloneData(ref BCurveDataStore,data);
                if(next<0) return ParasolidConstants.PK_ERROR_memory_full;
                ref var to=ref BCurveDataStore[next];
                to.VertexBlock=to.KnotBlock=to.KnotMultBlock=to.ExpandedKnotBlock=-1;
                error=NewCopiedCurve(source,next,out clone); if(error!=0) return error;
                return CopyPayload(from.VertexBlock,(nuint)(from.NVertices*from.VertexDim*8),ref to.VertexBlock)
                    && CopyPayload(from.KnotBlock,(nuint)(from.NKnots*8),ref to.KnotBlock)
                    && CopyPayload(from.KnotMultBlock,(nuint)(from.NKnots*4),ref to.KnotMultBlock)
                    && CopyPayload(from.ExpandedKnotBlock,(nuint)(from.ExpandedKnotCount*8),ref to.ExpandedKnotBlock)
                    ?0:ParasolidConstants.PK_ERROR_memory_full;
            }
            case CurveClass.TRCurve:
            {
                var d=TrCurveDataPool[data]; error=CloneEntity(d.BasisCurveTag,ref context,depth,out var basis); if(error!=0) return error;
                var next=CloneData(ref TrCurveDataPool,data);error=NewCopiedCurve(source,next,out clone);if(error!=0)return error;
                TrCurveDataPool[next].BasisCurveTag=basis;
                AcquireGeometryDependency(PoolKind.Curve,GetCurveSlotByTag(basis));Curves[GetCurveSlotByTag(clone)].DependenciesAcquired=1;
                return 0;
            }
            case CurveClass.SPCurve:
            {
                var d=SpCurveDataPool[data]; error=CloneEntity(d.SurfTag,ref context,depth,out var support); if(error!=0) return error;
                error=CloneEntity(d.BCurveTag,ref context,depth,out var basis); if(error!=0) return error;
                var next=CloneData(ref SpCurveDataPool,data);error=NewCopiedCurve(source,next,out clone);if(error!=0)return error;
                SpCurveDataPool[next].SurfTag=support;SpCurveDataPool[next].BCurveTag=basis;
                AcquireGeometryDependency(PoolKind.Surface,GetSurfaceSlotByTag(support));AcquireGeometryDependency(PoolKind.Curve,GetCurveSlotByTag(basis));
                Curves[GetCurveSlotByTag(clone)].DependenciesAcquired=1;return 0;
            }
            case CurveClass.ICurve:
            {
                var from=ICurveDataPool[data]; var next=CloneData(ref ICurveDataPool,data);
                if(next<0) return ParasolidConstants.PK_ERROR_memory_full;
                ref var to=ref ICurveDataPool[next]; to.HvecBlock=to.UvValueBlock=to.ProgramBlock=to.ContactChartBlock=-1;
                // The copied supports are resources of the new graph. Clear
                // inherited source tags before any fallible cloning step so
                // rollback cannot mistake a source support for an owned copy.
                to.Surface0Tag=to.Surface1Tag=0;to.OwnsSupportSurfaces=1;
                error=NewCopiedCurve(source,next,out clone); if(error!=0) return error;
                error=CloneEntity(from.Surface0Tag,ref context,depth,out to.Surface0Tag); if(error!=0) return error;
                error=CloneEntity(from.Surface1Tag,ref context,depth,out to.Surface1Tag); if(error!=0) return error;
                // Shared supports keep reference counts so the last copied
                // owner releases them, even when another copied intersection
                // or a temporary borrowed view uses the same support.
                AcquireGeometryDependency(PoolKind.Surface,GetSurfaceSlotByTag(to.Surface0Tag));
                AcquireGeometryDependency(PoolKind.Surface,GetSurfaceSlotByTag(to.Surface1Tag));
                Curves[GetCurveSlotByTag(clone)].DependenciesAcquired=1;
                return CopyPayload(from.HvecBlock,(nuint)(from.HvecCount*3*8),ref to.HvecBlock)
                    && CopyPayload(from.UvValueBlock,(nuint)(from.UvValueCount*8),ref to.UvValueBlock)
                    && (from.IsBlendContact==0||CopyPayload(from.ContactChartBlock,(nuint)((6*from.ChartCount-4)*8),ref to.ContactChartBlock))
                    && CopyPayload(from.ProgramBlock,NestedBlendProgramBytes(from.ProgramSurfaceCount,from.ProgramSpineCount,from.ProgramChartCount,from.ProgramScalarCount),ref to.ProgramBlock)?0:ParasolidConstants.PK_ERROR_memory_full;
            }
            default: return ParasolidConstants.PK_ERROR_not_implemented;
        }
    }

    private static int CloneSurface(SurfaceSlot sourceSlot, ref EntityCopyContext context, int depth, out EntityTag clone)
    {
        clone=0; var source=Surfaces[sourceSlot]; var data=source.DataIndex; int error;
        switch(source.Class)
        {
            case SurfaceClass.Plane: return NewCopiedSurface(source,CloneData(ref PlaneDataPool,data),out clone);
            case SurfaceClass.Cylinder: return NewCopiedSurface(source,CloneData(ref CylinderDataPool,data),out clone);
            case SurfaceClass.Cone: return NewCopiedSurface(source,CloneData(ref ConeDataPool,data),out clone);
            case SurfaceClass.Sphere: return NewCopiedSurface(source,CloneData(ref SphereDataPool,data),out clone);
            case SurfaceClass.Torus: return NewCopiedSurface(source,CloneData(ref TorusDataPool,data),out clone);
            case SurfaceClass.BSurface:
            {
                var from=BSurfaceDataStore[data]; var next=CloneData(ref BSurfaceDataStore,data);
                if(next<0) return ParasolidConstants.PK_ERROR_memory_full;
                ref var to=ref BSurfaceDataStore[next];
                to.VertexBlock=to.UKnotBlock=to.VKnotBlock=to.UKnotMultBlock=to.VKnotMultBlock=to.UExpandedKnotBlock=to.VExpandedKnotBlock=-1;
                error=NewCopiedSurface(source,next,out clone); if(error!=0) return error;
                return CopyPayload(from.VertexBlock,(nuint)(from.NUVertices*from.NVVertices*from.VertexDim*8),ref to.VertexBlock)
                    && CopyPayload(from.UKnotBlock,(nuint)(from.NUKnots*8),ref to.UKnotBlock)
                    && CopyPayload(from.VKnotBlock,(nuint)(from.NVKnots*8),ref to.VKnotBlock)
                    && CopyPayload(from.UKnotMultBlock,(nuint)(from.NUKnots*4),ref to.UKnotMultBlock)
                    && CopyPayload(from.VKnotMultBlock,(nuint)(from.NVKnots*4),ref to.VKnotMultBlock)
                    && CopyPayload(from.UExpandedKnotBlock,(nuint)(from.UExpandedKnotCount*8),ref to.UExpandedKnotBlock)
                    && CopyPayload(from.VExpandedKnotBlock,(nuint)(from.VExpandedKnotCount*8),ref to.VExpandedKnotBlock)?0:ParasolidConstants.PK_ERROR_memory_full;
            }
            case SurfaceClass.Offset:
            {
                var d=OffsetDataPool[data]; error=CloneEntity(d.BaseSurfTag,ref context,depth,out var support); if(error!=0) return error;
                var next=CloneData(ref OffsetDataPool,data);error=NewCopiedSurface(source,next,out clone);if(error!=0)return error;
                OffsetDataPool[next].BaseSurfTag=support;AcquireGeometryDependency(PoolKind.Surface,GetSurfaceSlotByTag(support));
                Surfaces[GetSurfaceSlotByTag(clone)].DependenciesAcquired=1;return 0;
            }
            case SurfaceClass.BlendSurface:
            {
                var from=BlendedEdgeDataPool[data];var next=CloneData(ref BlendedEdgeDataPool,data);if(next<0)return ParasolidConstants.PK_ERROR_memory_full;
                ref var to=ref BlendedEdgeDataPool[next];to.ProgramBlock=-1;to.HvecBlock=-1;to.Surface0Tag=to.Surface1Tag=to.SpineTag=0;
                error=NewCopiedSurface(source,next,out clone);if(error!=0)return error;
                error=CloneEntity(from.Surface0Tag,ref context,depth,out to.Surface0Tag);if(error!=0)return error;
                error=CloneEntity(from.Surface1Tag,ref context,depth,out to.Surface1Tag);if(error!=0)return error;
                error=CloneEntity(from.SpineTag,ref context,depth,out to.SpineTag);if(error!=0)return error;
                AcquireGeometryDependency(PoolKind.Surface,GetSurfaceSlotByTag(to.Surface0Tag));AcquireGeometryDependency(PoolKind.Surface,GetSurfaceSlotByTag(to.Surface1Tag));AcquireGeometryDependency(PoolKind.Curve,GetCurveSlotByTag(to.SpineTag));
                Surfaces[GetSurfaceSlotByTag(clone)].DependenciesAcquired=1;
                return CopyPayload(from.ProgramBlock,NestedBlendProgramBytes(from.ProgramSurfaceCount,from.ProgramSpineCount,from.ProgramChartCount,from.ProgramScalarCount),ref to.ProgramBlock)
                    &&CopyPayload(from.HvecBlock,(nuint)(from.HvecCount*3*8),ref to.HvecBlock)?0:ParasolidConstants.PK_ERROR_memory_full;
            }
            case SurfaceClass.Swept:
            {
                var d=SweptDataPool[data]; error=CloneEntity(d.SectionCurveTag,ref context,depth,out var section); if(error!=0) return error;
                var next=CloneData(ref SweptDataPool,data);error=NewCopiedSurface(source,next,out clone);if(error!=0)return error;
                SweptDataPool[next].SectionCurveTag=section;AcquireGeometryDependency(PoolKind.Curve,GetCurveSlotByTag(section));
                Surfaces[GetSurfaceSlotByTag(clone)].DependenciesAcquired=1;return 0;
            }
            case SurfaceClass.Spun:
            {
                var d=SpunDataPool[data]; error=CloneEntity(d.ProfileCurveTag,ref context,depth,out var profile); if(error!=0) return error;
                var next=CloneData(ref SpunDataPool,data);error=NewCopiedSurface(source,next,out clone);if(error!=0)return error;
                SpunDataPool[next].ProfileCurveTag=profile;AcquireGeometryDependency(PoolKind.Curve,GetCurveSlotByTag(profile));
                Surfaces[GetSurfaceSlotByTag(clone)].DependenciesAcquired=1;return 0;
            }
            default: return ParasolidConstants.PK_ERROR_not_implemented;
        }
    }

    private static int CloneBody(EntityTag source, ref EntityCopyContext context, int depth, out EntityTag clone)
    {
        clone=0; int n=0,nr=0; nint tags=0,classes=0,parents=0,children=0,senses=0;
        int cn=0,cr=0; nint copyTags=0,copyClasses=0,copyParents=0,copyChildren=0,copySenses=0;
        PK_BODY_ask_topology_o_s ask=new() { o_t_version=2, want_fins=1 }; PK_BODY_create_topology_2_r_s created=default;
        try
        {
            var error=BodyAskTopology(source,&ask,&n,&tags,&classes,&nr,&parents,&children,&senses); if(error!=0) return error;
            var original=Bodies[TagRec(source).Slot];
            if(n<=1 && original.FaceCountBody+original.EdgeCountBody+original.VertexCountBody==0) return ParasolidConstants.PK_ERROR_not_implemented;
            var options=new PK_BODY_create_topology_2_o_s { o_t_version=1, body_type=original.BodyType };
            if(((int*)tags)[0]!=source || ((int*)classes)[0]!=ParasolidConstants.PK_CLASS_body) return ParasolidConstants.PK_ERROR_general_body;
            for(var i=0;i<nr;i++) { ((int*)parents)[i]--; ((int*)children)[i]--; }
            error=BodyCreateTopology2(n-1,(PK_CLASS_t*)classes+1,nr,(int*)parents,(int*)children,(int*)senses,&options,&created); if(error!=0) return error;
            clone=created.body;
            error=BodyAskTopology(clone,&ask,&cn,&copyTags,&copyClasses,&cr,&copyParents,&copyChildren,&copySenses); if(error!=0) return error;
            if(cn!=n) return ParasolidConstants.PK_ERROR_general_body;
            for(var i=0;i<n;i++)
            {
                if(((int*)classes)[i]!=((int*)copyClasses)[i]) return ParasolidConstants.PK_ERROR_general_body;
                if(!context.Add(((int*)tags)[i],((int*)copyTags)[i])) return ParasolidConstants.PK_ERROR_memory_full;
            }
            ref var copy=ref Bodies[TagRec(clone).Slot]; copy.BodyType=original.BodyType; copy.BodyConfig=original.BodyConfig;
            for(var i=0;i<n;i++)
            {
                var from=TagRec(((int*)tags)[i]); var to=TagRec(((int*)copyTags)[i]);
                if(from.Pool==(byte)PoolKind.Face)
                {
                    var a=Faces[from.Slot]; ref var b=ref Faces[to.Slot]; b.Orientation=a.Orientation; b.Tolerance=a.Tolerance;
                    if(a.SurfTag>0) { error=CloneEntity(a.SurfTag,ref context,depth+1,out b.SurfTag); if(error!=0) return error; }
                }
                else if(from.Pool==(byte)PoolKind.Edge)
                {
                    var a=Edges[from.Slot]; ref var b=ref Edges[to.Slot]; b.Tolerance=a.Tolerance;
                    b.HasParameterRange=a.HasParameterRange; b.ParameterStart=a.ParameterStart; b.ParameterEnd=a.ParameterEnd;
                    b.PendingBlendKind=a.PendingBlendKind; b.PendingBlendFirst=a.PendingBlendFirst; b.PendingBlendSecond=a.PendingBlendSecond;
                    b.PendingBlendSequence=a.PendingBlendSequence;
                    if(a.PendingBlendFirstFace!=0)
                    {
                        if(!context.Find(a.PendingBlendFirstFace,out b.PendingBlendFirstFace))return ParasolidConstants.PK_ERROR_general_body;
                    }
                    b.StartVertex=b.EndVertex=-1;
                    if(a.StartVertex>=0)
                    {
                        if(!context.Find(Vertices[a.StartVertex].Header.Tag,out var vertexTag))return ParasolidConstants.PK_ERROR_general_body;
                        b.StartVertex=TagRec(vertexTag).Slot;
                    }
                    if(a.EndVertex>=0)
                    {
                        if(!context.Find(Vertices[a.EndVertex].Header.Tag,out var vertexTag))return ParasolidConstants.PK_ERROR_general_body;
                        b.EndVertex=TagRec(vertexTag).Slot;
                    }
                    if(a.WireShellTag>0 && !context.Find(a.WireShellTag,out b.WireShellTag)) return ParasolidConstants.PK_ERROR_general_body;
                    if(a.CurveTag>0) { error=CloneEntity(a.CurveTag,ref context,depth+1,out b.CurveTag); if(error!=0) return error; }
                    // The SDK deliberately omits dummy fins from ask_topology.
                    // Restore their actual incidence from the source record.
                    var sourceFin=a.FirstFinEdge;
                    for(var j=0;j<a.FinCount;j++,sourceFin=Fins[sourceFin].NextOfEdge)
                    {
                        var sf=Fins[sourceFin];if(sf.Loop>=0)continue;
                        var targetFin=TryAllocateFins();if(targetFin<0)return ParasolidConstants.PK_ERROR_memory_full;
                        ref var tf=ref Fins[targetFin];tf.Edge=to.Slot;tf.Loop=-1;tf.Vertex=-1;tf.Curve=-1;
                        AssignPartition(ref tf.Header,CurrentPartition);
                        if(AllocateTag(EntityClass.Fin,PoolKind.Fin,targetFin,tf.Header.Generation)<=0)return ParasolidConstants.PK_ERROR_memory_full;
                        tf.NextInLoop=tf.PrevInLoop=tf.NextAtVertex=tf.PrevAtVertex=tf.NextOfEdge=tf.PrevOfEdge=tf.Other=-1;
                        AppendFinToEdge(to.Slot,targetFin);
                        if(sf.Vertex>=0)
                        {
                            if(!context.Find(Vertices[sf.Vertex].Header.Tag,out var vertexTag))return ParasolidConstants.PK_ERROR_general_body;
                            AppendFinToVertex(TagRec(vertexTag).Slot,targetFin);
                        }
                        tf.Sense=sf.Sense;
                        if(sf.Curve>0)
                        { error=CloneEntity(sf.Curve,ref context,depth+1,out var curveTag);if(error!=0)return error;
                          error=AcquireFinCurveReference(targetFin,curveTag);if(error!=0)return error; }
                    }
                }
                else if(from.Pool==(byte)PoolKind.Loop)
                {
                    // A pole loop's null-edge fin is omitted by ask_topology.
                    // LOOP -> VERTEX recreated it; retain its optional pcurve
                    // and sense as well as the publicly visible incidence.
                    var a=Loops[from.Slot];var b=Loops[to.Slot];
                    if(a.FinCount==1&&Fins[a.FirstFin].Edge<0)
                    {
                        if(b.FinCount!=1||Fins[b.FirstFin].Edge>=0)return ParasolidConstants.PK_ERROR_general_body;
                        var sf=Fins[a.FirstFin];Fins[b.FirstFin].Sense=sf.Sense;
                        if(sf.Curve>0)
                        {
                            error=CloneEntity(sf.Curve,ref context,depth+1,out var curveTag);if(error!=0)return error;
                            error=AcquireFinCurveReference(b.FirstFin,curveTag);if(error!=0)return error;
                        }
                    }
                }
                else if(from.Pool==(byte)PoolKind.Vertex)
                {
                    var a=Vertices[from.Slot]; ref var b=ref Vertices[to.Slot]; b.Tolerance=a.Tolerance;
                    if(a.PointTag>0) { error=CloneEntity(a.PointTag,ref context,depth+1,out b.PointTag); if(error!=0) return error; }
                }
                else if(from.Pool==(byte)PoolKind.Fin)
                {
                    var a=Fins[from.Slot]; ref var b=ref Fins[to.Slot]; b.Sense=a.Sense;
                    if(a.Vertex>=0 && b.Vertex<0)
                    { if(!context.Find(Vertices[a.Vertex].Header.Tag,out var vertexTag))return ParasolidConstants.PK_ERROR_general_body;
                      AppendFinToVertex(TagRec(vertexTag).Slot,to.Slot);b.Sense=a.Sense; }
                    if(a.Curve>0) { error=CloneEntity(a.Curve,ref context,depth+1,out var curveTag); if(error!=0) return error;
                        error=AcquireFinCurveReference(to.Slot,curveTag);if(error!=0)return error; }
                }
                else if(from.Pool==(byte)PoolKind.Region) Regions[to.Slot].IsSolid=Regions[from.Slot].IsSolid;
            }
            // Dummy wire/sheet fins are absent from the public topology list.
            // Restore their next/previous incidence and opposite fin only
            // after every edge has been copied, including cross-edge links.
            var sourceEdge=original.FirstEdgeBody;
            for(var i=0;i<original.EdgeCountBody;i++,sourceEdge=Edges[sourceEdge].NextInBody)
            {
                var fin=Edges[sourceEdge].FirstFinEdge;
                for(var j=0;j<Edges[sourceEdge].FinCount;j++,fin=Fins[fin].NextOfEdge)
                {
                    var target=FindCopiedFinSlot(fin,ref context);if(target<0)return ParasolidConstants.PK_ERROR_general_body;
                    ref var copied=ref Fins[target];ref readonly var originalFin=ref Fins[fin];
                    copied.Other=FindCopiedFinSlot(originalFin.Other,ref context);
                    if(originalFin.Loop<0)
                    {
                        copied.NextInLoop=FindCopiedFinSlot(originalFin.NextInLoop,ref context);
                        copied.PrevInLoop=FindCopiedFinSlot(originalFin.PrevInLoop,ref context);
                    }
                }
            }
            RebuildBoundaryGeometryLinks(TagRec(clone).Slot,resetOwners:true);
            // A defining geometry can remain attached to the body's boundary
            // while its newly constructed parent is unattached part geometry.
            // Use the same memo as the boundary copy so those dependencies
            // remain shared within the copied part, never with the source.
            var sourceSurface=original.FirstConstructionSurface;
            if(original.ConstructionSurfaceCount>0)
            {
                error=CloneConstructionChain(Surfaces[sourceSurface].Header.Tag,original.ConstructionSurfaceCount,TagRec(clone).Slot,ref context,depth+1);
                if(error!=0)return error;
            }
            var sourceCurve=original.FirstConstructionCurve;
            if(original.ConstructionCurveCount>0)
            {
                error=CloneConstructionChain(Curves[sourceCurve].Header.Tag,original.ConstructionCurveCount,TagRec(clone).Slot,ref context,depth+1);
                if(error!=0)return error;
            }
            if(original.ConstructionPointCount>0)return ParasolidConstants.PK_ERROR_not_implemented;
            if(!PublishBodyTopologyTags(TagRec(clone).Slot))return ParasolidConstants.PK_ERROR_memory_full;
            return 0;
        }
        finally
        {
            MemoryFree((void*)tags); MemoryFree((void*)classes); MemoryFree((void*)parents); MemoryFree((void*)children); MemoryFree((void*)senses);
            MemoryFree(created.topols); MemoryFree(created.create_faults); MemoryFree(created.internal_indices);
            MemoryFree((void*)copyTags); MemoryFree((void*)copyClasses); MemoryFree((void*)copyParents); MemoryFree((void*)copyChildren); MemoryFree((void*)copySenses);
        }
    }

    private static int CloneConstructionChain(GeomTag first,BufferCount count,BodySlot target,ref EntityCopyContext context,int depth)
    {
        var source=first;
        for(var i=0;i<count;i++)
        {
            if(source<=0)return ParasolidConstants.PK_ERROR_general_body;
            var error=CloneEntity(source,ref context,depth,out var copy);if(error!=0)return error;
            error=TryRegisterConstructionGeometry(target,copy);if(error!=0)return error;
            source=GetConstructionNext(source);
        }
        return source==0?0:ParasolidConstants.PK_ERROR_general_body;
    }

    private static FinSlot FindCopiedFinSlot(FinSlot source,ref EntityCopyContext context)
    {
        if(source<0)return -1;
        ref readonly var fin=ref Fins[source];
        if(fin.Loop>=0)return context.Find(fin.Header.Tag,out var tag)?TagRec(tag).Slot:-1;
        if(!context.Find(Edges[fin.Edge].Header.Tag,out var edgeTag))return -1;
        var ordinal=0;var cursor=Edges[fin.Edge].FirstFinEdge;
        for(var i=0;i<Edges[fin.Edge].FinCount;i++,cursor=Fins[cursor].NextOfEdge)
        {
            if(cursor==source)break;
            if(Fins[cursor].Loop<0)ordinal++;
        }
        var targetEdge=TagRec(edgeTag).Slot;cursor=Edges[targetEdge].FirstFinEdge;
        for(var i=0;i<Edges[targetEdge].FinCount;i++,cursor=Fins[cursor].NextOfEdge)
            if(Fins[cursor].Loop<0 && ordinal--==0)return cursor;
        return -1;
    }
}

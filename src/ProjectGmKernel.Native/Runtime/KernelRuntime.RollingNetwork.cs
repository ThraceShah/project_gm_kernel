using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;
using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal enum RollingNetworkStage:byte{None,Read,Bands,SupportEvents,BandEvents,BoundaryIncidence,Clip,Split,Arrange,Select,Build,Publish,PatchDomain,SourceAssociations,BoundaryProofs,Subdivision,ProvedEndpointReuse}
    [ThreadStatic] internal static RollingNetworkStage LastRollingNetworkStage;
    [ThreadStatic] internal static BufferOffset LastRollingFirstFace,LastRollingSecondFace;
    internal struct RollingNetworkTrace
    {
        internal RollingNetworkStage Stage;
        internal EdgeSlot Source;
        internal FaceSlot FirstSupport,SecondSupport;
        internal SurfTag Surface,Other;
        internal CurveTag Curve;
        internal VertexSlot Vertex;
        internal BufferOffset Cell;
        internal FaceSlot CellSource;
        internal sbyte Material;
        internal SurfacePairRelation Relation;
        internal double First,Last;
    }
    [ThreadStatic] internal static Action<RollingNetworkTrace>? TraceRollingNetwork;
    private struct RollingCutFamily
    {
        internal BufferOffset FirstFace,SecondFace,FirstCurve;
        internal BufferCount Count;
        internal byte SecondIsBoundary;
    }
    private struct RollingSubdivision
    {
        internal BufferOffset Band;
        internal EdgeSlot Before,After;
        internal VertexSlot Vertex;
        internal double Parameter;
        internal double Station,StationLower,StationUpper;
    }

    private static bool CanBuildAnalyticRollingNetwork(ReadOnlySpan<EdgeSlot> selected)
    {
        if(selected.Length<2||CommonBlendChainFaces(selected)||NeedsPlanarRollingCorner(selected))return false;
        var sameCarrier=true;
        foreach(var edge in selected)
        {
            var record=Edges[edge];sameCarrier&=record.CurveTag==Edges[selected[0]].CurveTag;
            if(record.PendingBlendKind!=1||!LocalEdgeFaces(edge,out var first,out var second))return false;
            var carrier=Curves[GetCurveSlotByTag(record.CurveTag)].Class;
            var a=Surfaces[GetSurfaceSlotByTag(Faces[first].SurfTag)];var b=Surfaces[GetSurfaceSlotByTag(Faces[second].SurfTag)];
            if(carrier==CurveClass.Line&&a.Class==SurfaceClass.Plane&&b.Class==SurfaceClass.Plane)continue;
            if(carrier!=CurveClass.Ellipse||a.Class!=SurfaceClass.Cylinder||b.Class!=SurfaceClass.Cylinder)return false;
            if(CylinderDataPool[a.DataIndex].Radius!=CylinderDataPool[b.DataIndex].Radius)return false;
        }
        return !sameCarrier;
    }

    private static int BuildInteractingRollingBlends(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<EdgeSlot> selected,
        Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;
        LastRegularBlendStage=RegularBlendStage.Network;
        LastRollingNetworkStage=RollingNetworkStage.Read;
        if(NeedsPlanarRollingCorner(selected))return ParasolidConstants.PK_ERROR_not_implemented;
        if(selected.Length>64)return ParasolidConstants.PK_ERROR_not_implemented;
        var scratch=CommandScratch.Current;var memory=scratch.Take(12000);if(memory.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
        var bytes=MemoryMarshal.AsBytes(memory);var cursor=0;
        var families=ModelSpan<RollingCutFamily>(bytes,ref cursor,RegularCutLimit);var allCuts=ModelSpan<SheetCutCurve>(bytes,ref cursor,RegularCurveLimit);
        if(cursor>bytes.Length)return ParasolidConstants.PK_ERROR_memory_full;var familyCount=0;var curveCount=0;
        Span<RollingBandCandidate> candidates=stackalloc RollingBandCandidate[selected.Length];
        Span<EdgeSlot> networkEdges=stackalloc EdgeSlot[selected.Length];var orderedCount=0;var sourceEdge=Bodies[body].FirstEdgeBody;
        for(var e=0;e<Bodies[body].EdgeCountBody;e++,sourceEdge=Edges[sourceEdge].NextInBody)if(selected.Contains(sourceEdge))networkEdges[orderedCount++]=sourceEdge;
        if(orderedCount!=selected.Length)return ParasolidConstants.PK_ERROR_bad_parameter;
        // A finite curved spine supplies the interaction station before its
        // generator bands. Mark order controls product reporting separately.
        for(var i=1;i<networkEdges.Length;i++)for(var j=i;j>0
            &&Curves[GetCurveSlotByTag(Edges[networkEdges[j-1]].CurveTag)].Class==CurveClass.Line
            &&Curves[GetCurveSlotByTag(Edges[networkEdges[j]].CurveTag)].Class!=CurveClass.Line;j--)
            (networkEdges[j-1],networkEdges[j])=(networkEdges[j],networkEdges[j-1]);
        Span<RollingSubdivision> subdivisions=stackalloc RollingSubdivision[selected.Length-1];var subdivisionCount=0;
        Span<BufferOffset> groups=stackalloc BufferOffset[selected.Length],bandForGroup=stackalloc BufferOffset[selected.Length];
        for(var i=0;i<groups.Length;i++)groups[i]=i;
        for(var i=0;i<networkEdges.Length;i++)for(var j=i+1;j<networkEdges.Length;j++)
        {
            if(!TryRollingCarrierSubdivision(networkEdges[i],networkEdges[j],out var join))continue;
            var a=RollingComponent(groups,i);var b=RollingComponent(groups,j);
            if(a==b||subdivisionCount==subdivisions.Length)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            groups[Math.Max(a,b)]=Math.Min(a,b);subdivisions[subdivisionCount++]=join;
        }
        LastRollingNetworkStage=RollingNetworkStage.Bands;
        var bandCount=0;
        for(var i=0;i<selected.Length;i++)if(RollingComponent(groups,i)==i)
        {
            var error=CreateAnalyticRollingCandidate(body,networkEdges[i],out var band);if(error!=0)return error;
            var sourceLower=double.PositiveInfinity;var sourceUpper=double.NegativeInfinity;
            for(var e=i;e<networkEdges.Length;e++)if(RollingComponent(groups,e)==i)
            {
                var member=Edges[networkEdges[e]];
                if(!TryProfileEdgeRange(member,out var lower,out var upper)
                    ||!OperationIntersection.InInterval(lower,band.First,band.Last)||!OperationIntersection.InInterval(upper,band.First,band.Last))
                    return ParasolidConstants.PK_ERROR_cant_do_intersect;
                var positive=LocalSenseSign(Curves[GetCurveSlotByTag(member.CurveTag)].Sense)>0;
                if(lower<sourceLower){sourceLower=lower;band.SourceFirst=positive?member.StartVertex:member.EndVertex;}
                if(upper>sourceUpper){sourceUpper=upper;band.SourceLast=positive?member.EndVertex:member.StartVertex;}
            }
            band.SourceStart=sourceLower;band.SourceEnd=sourceUpper;
            bandForGroup[i]=bandCount;candidates[bandCount++]=band;
            TraceRollingNetwork?.Invoke(new(){Stage=LastRollingNetworkStage,Source=band.Source,FirstSupport=band.FirstSupport,SecondSupport=band.SecondSupport,Surface=band.Surface,First=band.First,Last=band.Last});
        }
        var bands=candidates[..bandCount];
        for(var i=0;i<subdivisionCount;i++)
        {
            ref var split=ref subdivisions[i];split.Band=bandForGroup[RollingComponent(groups,networkEdges.IndexOf(split.Before))];
            var band=bands[split.Band];
            split.Station=split.StationLower=split.StationUpper=split.Parameter;
            if(Surfaces[GetSurfaceSlotByTag(band.Surface)].Class==SurfaceClass.BlendSurface
                &&!TryEllipticSubdivisionStation(band,ref split))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            for(var previous=0;previous<i;previous++)if(subdivisions[previous].Band==split.Band)
            {
                var other=subdivisions[previous];
                if(!(other.Parameter<split.Parameter&&other.StationUpper<split.StationLower
                    ||split.Parameter<other.Parameter&&split.StationUpper<other.StationLower))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            }
        }
        Span<SheetCutCurve> cuts=stackalloc SheetCutCurve[GeneralIntersectionBranchLimit];
        Span<RollingPointContact> points=stackalloc RollingPointContact[128];var pointCount=0;
        Span<BufferCount> interactions=stackalloc BufferCount[bands.Length];interactions.Clear();
        LastRollingNetworkStage=RollingNetworkStage.BandEvents;
        for(var i=0;i<bands.Length;i++)for(var j=i+1;j<bands.Length;j++)
        {
            TraceRollingNetwork?.Invoke(new(){Stage=LastRollingNetworkStage,Source=bands[i].Source,Surface=bands[i].Surface,Other=bands[j].Surface,First=bands[i].First,Last=bands[i].Last});
            var error=PrepareSheetCutCurves(bands[i].Surface,bands[j].Surface,cuts,out var count);if(error!=0)return error;
            // Three interacting bands require their own joint-event solve.
            // Reject before editing topology; disjoint components are still
            // processed in the same operation regardless of component count.
            if(count>0&&(++interactions[i]>1||++interactions[j]>1))return ParasolidConstants.PK_ERROR_not_implemented;
            var sense=RollingInteractionSense(bands,i,j,subdivisions[..subdivisionCount]);
            foreach(var cut in cuts[..count])SetRollingCurveSense(cut.Curve,sense);
            error=AppendRollingCutFamily(faces.Length+i,faces.Length+j,cuts[..count],false,families,ref familyCount,allCuts,ref curveCount);if(error!=0)return error;
            foreach(var cut in cuts[..count])TraceRollingNetwork?.Invoke(new(){Stage=LastRollingNetworkStage,Source=bands[i].Source,Surface=bands[i].Surface,Other=bands[j].Surface,Curve=cut.Curve,First=cut.First,Last=cut.Last});
        }
        LastRollingNetworkStage=RollingNetworkStage.SupportEvents;
        foreach(var band in bands)
        {
            var bandIndex=-1;for(var i=0;i<bands.Length;i++)if(bands[i].Surface==band.Surface){bandIndex=i;break;}
            for(var side=0;side<2;side++)
            {
                var curve=side==0?band.FirstContact:band.SecondContact;
                var error=RollingContactRange(band,curve,out var lower,out var upper);if(error!=0)return error;
                cuts[0]=new(){Curve=curve,First=lower,Last=upper};
                error=AppendRollingCutFamily(faces.IndexOf(side==0?band.FirstSupport:band.SecondSupport),faces.Length+bandIndex,cuts[..1],true,families,ref familyCount,allCuts,ref curveCount);if(error!=0)return error;
            }
            foreach(var face in faces)
            {
                if(face==band.FirstSupport||face==band.SecondSupport)continue;
                var other=Faces[face].SurfTag;
                TraceRollingNetwork?.Invoke(new(){Stage=LastRollingNetworkStage,Source=band.Source,Surface=band.Surface,Other=other,First=band.First,Last=band.Last});
                if(TryRollingCylinderPointContact(band,face,out var contact))
                {
                    if(pointCount==points.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                    points[pointCount++]=contact;
                    TraceRollingNetwork?.Invoke(new(){Stage=LastRollingNetworkStage,Source=band.Source,Surface=band.Surface,Other=other,Vertex=contact.Vertex,Relation=SurfacePairRelation.PointContact,First=contact.First,Last=contact.Last});continue;
                }
                var error=PrepareSheetCutCurves(band.Surface,other,cuts,out var count);if(error!=0)return error;
                if(Surfaces[GetSurfaceSlotByTag(band.Surface)].Class==SurfaceClass.Cylinder)
                    foreach(var cut in cuts[..count])if(Curves[GetCurveSlotByTag(cut.Curve)].Class is CurveClass.Circle or CurveClass.Ellipse)
                        SetRollingCurveSense(cut.Curve,Curves[GetCurveSlotByTag(Edges[band.Source].CurveTag)].Sense);
                error=AppendRollingCutFamily(faces.IndexOf(face),faces.Length+bandIndex,cuts[..count],false,families,ref familyCount,allCuts,ref curveCount);if(error!=0)return error;
                foreach(var cut in cuts[..count])TraceRollingNetwork?.Invoke(new(){Stage=LastRollingNetworkStage,Source=band.Source,Surface=band.Surface,Other=other,Curve=cut.Curve,First=cut.First,Last=cut.Last});
            }
        }
        LastRollingNetworkStage=RollingNetworkStage.BoundaryIncidence;
        return BuildRollingArrangement(body,faces,selected,bands,subdivisions[..subdivisionCount],points[..pointCount],families[..familyCount],allCuts[..curveCount],descriptions,products,out made);
        }
        finally{scratch.Return(memory);}
    }

    private static int AppendRollingCutFamily(BufferOffset first,BufferOffset second,ReadOnlySpan<SheetCutCurve> cuts,bool boundary,
        Span<RollingCutFamily> families,ref BufferCount count,Span<SheetCutCurve> curves,ref BufferCount curveCount)
    {
        if(cuts.IsEmpty)return 0;
        if(count==families.Length||cuts.Length>curves.Length-curveCount)return ParasolidConstants.PK_ERROR_not_implemented;
        families[count++]=new(){FirstFace=first,SecondFace=second,FirstCurve=curveCount,Count=cuts.Length,SecondIsBoundary=boundary?(byte)1:(byte)0};
        cuts.CopyTo(curves[curveCount..]);curveCount+=cuts.Length;return 0;
    }

    private static void SetRollingCurveSense(CurveTag curve,KernelSense sense)
    {
        ref var record=ref Curves[GetCurveSlotByTag(curve)];record.Sense=sense;
        if(record.Class==CurveClass.ICurve)ICurveDataPool[record.DataIndex].Sense=sense;
    }

    private static KernelSense RollingInteractionSense(ReadOnlySpan<RollingBandCandidate> bands,BufferOffset first,BufferOffset second,
        ReadOnlySpan<RollingSubdivision> subdivisions)
    {
        var a=bands[first];var b=bands[second];var sense=Curves[GetCurveSlotByTag(Edges[a.Source].CurveTag)].Sense;
        if(a.SourceFirst!=b.SourceFirst&&a.SourceFirst!=b.SourceLast&&a.SourceLast!=b.SourceFirst&&a.SourceLast!=b.SourceLast)return sense;
        // At a common source junction, an incident mark can interrupt a
        // chain traversed in decreasing native station. The shared curve
        // then uses the opposite carrier sense; its finite events and
        // material cells are independent of that representational choice.
        foreach(ref readonly var split in subdivisions)
        {
            if(split.Band!=first&&split.Band!=second)continue;
            var between=Edges[split.Band==first?b.Source:a.Source].PendingBlendSequence;
            if(Edges[split.After].PendingBlendSequence<between&&between<Edges[split.Before].PendingBlendSequence)return FlipLocalSense(sense);
        }
        return sense;
    }

    // Two planar generator bands meeting across three supports need a corner
    // provider. Their two carrier cylinders alone do not certify a smooth
    // rolling replacement. The existing three-selected-edge corner provider
    // runs before this network consumer; no source topology is changed here.
    private static bool NeedsPlanarRollingCorner(ReadOnlySpan<EdgeSlot> selected)
    {
        for(var i=0;i<selected.Length;i++)for(var j=i+1;j<selected.Length;j++)
        {
            var a=Edges[selected[i]];var b=Edges[selected[j]];
            if(a.StartVertex!=b.StartVertex&&a.StartVertex!=b.EndVertex&&a.EndVertex!=b.StartVertex&&a.EndVertex!=b.EndVertex)continue;
            if(TryRollingCarrierSubdivision(selected[i],selected[j],out _))continue;
            if(!LocalEdgeFaces(selected[i],out var a0,out var a1)||!LocalEdgeFaces(selected[j],out var b0,out var b1))continue;
            if(a0!=b0&&a0!=b1&&a1!=b0&&a1!=b1)continue;
            if(Surfaces[GetSurfaceSlotByTag(Faces[a0].SurfTag)].Class==SurfaceClass.Plane&&Surfaces[GetSurfaceSlotByTag(Faces[a1].SurfTag)].Class==SurfaceClass.Plane
                &&Surfaces[GetSurfaceSlotByTag(Faces[b0].SurfTag)].Class==SurfaceClass.Plane&&Surfaces[GetSurfaceSlotByTag(Faces[b1].SurfTag)].Class==SurfaceClass.Plane)return true;
        }
        return false;
    }

    private static bool TryRollingCarrierSubdivision(EdgeSlot first,EdgeSlot second,out RollingSubdivision join)
    {
        join=default;var a=Edges[first];var b=Edges[second];
        if(a.CurveTag!=b.CurveTag||a.PendingBlendKind!=1||b.PendingBlendKind!=1||a.PendingBlendFirst!=b.PendingBlendFirst
            ||!LocalEdgeFaces(first,out var a0,out var a1)||!LocalEdgeFaces(second,out var b0,out var b1)
            ||!(a0==b0&&a1==b1||a0==b1&&a1==b0)||!TryProfileEdgeRange(a,out var lo,out var hi)||!TryProfileEdgeRange(b,out var nextLo,out var nextHi))return false;
        var curve=Curves[GetCurveSlotByTag(a.CurveTag)];if(curve.Class is not (CurveClass.Line or CurveClass.Ellipse))return false;
        var forward=LocalSenseSign(curve.Sense)>0;
        if(nextHi==lo){(first,second)=(second,first);(a,b)=(b,a);hi=lo;}
        else if(hi!=nextLo)return false;
        var vertex=forward?a.EndVertex:a.StartVertex;
        if(vertex<0||vertex!=(forward?b.StartVertex:b.EndVertex))return false;
        var degree=0;var edge=Bodies[a.Body].FirstEdgeBody;
        for(var i=0;i<Bodies[a.Body].EdgeCountBody;i++,edge=Edges[edge].NextInBody)
            if(Edges[edge].StartVertex==vertex||Edges[edge].EndVertex==vertex)degree++;
        if(degree!=2)return false;
        join=new(){Before=first,After=second,Vertex=vertex,Parameter=hi};return true;
    }

    private static int RollingContactRange(in RollingBandCandidate band,CurveTag curve,out double first,out double last)
    {
        first=band.First;last=band.Last;
        if(Surfaces[GetSurfaceSlotByTag(band.Surface)].Class!=SurfaceClass.BlendSurface)return 0;
        var record=Curves[GetCurveSlotByTag(curve)];
        if(!TryAnalyticBlendContactMap(band.Surface,record,record.TMin,record.TMax,out _,out var shift,out var scale))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var a=(band.First-shift)/scale;var b=(band.Last-shift)/scale;first=Math.Min(a,b);last=Math.Max(a,b);return 0;
    }
}

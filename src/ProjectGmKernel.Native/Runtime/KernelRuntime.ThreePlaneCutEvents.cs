using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int JoinThreePlaneCutRoots(in RegularBoundaryView input,BufferOffset third,scoped Span<RegularFaceCut> cuts,
        BufferOffset current,ref SheetArrangement model,out bool joined)
    {
        joined=false;
        var first=cuts[current].First;var second=cuts[current].Second;
        Span<BoundaryParameter> anchors=stackalloc BoundaryParameter[3];
        Span<FiniteCurveSurfaceRoot> roots=stackalloc FiniteCurveSurfaceRoot[3];
        Span<BoundaryEventId> ends=stackalloc BoundaryEventId[3];
        if(!FindThreePlaneCutRoot(input,cuts,first,second,third,ref model,out anchors[0],out roots[0],out ends[0])
            ||anchors[0].SourceOccurrence!=RegularInputEdges+current
            ||!FindThreePlaneCutRoot(input,cuts,first,third,second,ref model,out anchors[1],out roots[1],out ends[1])
            ||!FindThreePlaneCutRoot(input,cuts,second,third,first,ref model,out anchors[2],out roots[2],out ends[2]))return 0;

        // Each carrier lies exactly on its two plane equations, with a
        // transverse root on the third. The independent first pair
        // therefore has exactly one common point with the third plane.
        // These are three gauges of that root, not three nearby XYZ hits.
        var error=JoinProvenThreePlaneEndpoints(input,cuts,ends,roots[0].Point,ref model,out var endpoint);
        if(error!=0)return error;
        return AssociateThirdFaceStations(anchors,roots,ref model,out joined,endpoint);
    }

    private static int JoinProvenThreePlaneEndpoints(in RegularBoundaryView input,scoped Span<RegularFaceCut> cuts,
        scoped ReadOnlySpan<BoundaryEventId> ends,in KernelVector3 point,ref SheetArrangement model,out BoundaryEventId endpoint)
    {
        endpoint=-1;var separate=false;
        foreach(var end in ends)if(end>=0)
        {
            if(endpoint<0)endpoint=end;else separate|=endpoint!=end;
        }
        if(!separate)return 0;
        LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;
        if(model.NE!=0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        foreach(var end in ends)
        {
            if(end<0)continue;
            // Only synthetic cut endpoints may be joined here. Input
            // topology and already assembled edge identities are immutable.
            if(model.OriginalVertices[end]>=0||!model.LiveBoundaryPoint(end)||!(ProfileDistance(model.Points[end],point)<=model.Tolerance))
                return ParasolidConstants.PK_ERROR_cant_do_intersect;
            foreach(ref readonly var edge in input.Edges)if(edge.StartVertex==end||edge.EndVertex==end)
                return ParasolidConstants.PK_ERROR_cant_do_intersect;
        }

        var parameters=model.Parameters[..model.ParameterCount];
        for(var i=0;i<parameters.Length;i++)
        {
            ref readonly var a=ref parameters[i];if(!ends.Contains(a.Event))continue;
            for(var j=i+1;j<parameters.Length;j++)
            {
                ref readonly var b=ref parameters[j];
                if(a.Event==b.Event||a.Carrier!=b.Carrier||a.Generation!=b.Generation||!ends.Contains(b.Event))continue;
                var aRange=new IntervalNumber(a.Lower,a.Upper);var bRange=new IntervalNumber(b.Lower,b.Upper);
                var both=a.Witness!=0&&b.Witness!=0;
                var exact=a.Lower==a.Upper&&b.Lower==b.Upper&&a.Lower==b.Lower;
                var shared=both&&a.Witness==b.Witness&&a.Surface==b.Surface;
                if(!aRange.Finite||!bRange.Finite||!IntersectStationBounds(aRange,bRange).Finite||!exact&&!shared
                    ||both&&(a.SourceOccurrence!=b.SourceOccurrence||a.Surface==b.Surface&&!shared))
                    return UnresolvedBoundaryStation(a.Carrier,a.Event,a.Parameter,aRange,b.Parameter,bRange);
            }
        }

        // All cut uses, not only the three participating ones, must now
        // name the surviving identity. Retired slots cannot seed new splits.
        foreach(var end in ends)if(end>=0&&end!=endpoint&&model.LiveBoundaryPoint(end))
        {
            foreach(ref var cut in cuts)
            {
                if(cut.Cut.LowerEvent==end+1)cut.Cut.LowerEvent=endpoint+1;
                if(cut.Cut.UpperEvent==end+1)cut.Cut.UpperEvent=endpoint+1;
            }
            foreach(ref var row in parameters)if(row.Event==end)row.Event=endpoint;
            foreach(ref var tangency in model.Tangencies[..model.TangencyCount])if(tangency.Event==end)tangency.Event=endpoint;
            foreach(ref var contact in model.SurfaceContacts[..model.SurfaceContactCount])if(contact.Event==end)contact.Event=endpoint;
            model.OriginalVertices[end]=RetiredBoundaryVertex;
        }
        return 0;
    }

    private static int AssociateThirdFaceStations(scoped ReadOnlySpan<BoundaryParameter> anchors,scoped ReadOnlySpan<FiniteCurveSurfaceRoot> roots,
        ref SheetArrangement model,out bool joined,BoundaryEventId endpoint=-1)
    {
        joined=false;
        if(endpoint<0)
        {
            var add=model.AddPoint(roots[0].Point,-1,out endpoint,anchors,requireProof:true);if(add!=0)return add;
        }
        else foreach(ref readonly var root in roots)
        {
            // The finite cut use already named the event. Position only
            // checks consistency and never selects a substitute endpoint.
            if(!(ProfileDistance(model.Points[endpoint],root.Point)<=model.Tolerance))
            {LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;return ParasolidConstants.PK_ERROR_cant_do_intersect;}
        }
        for(var i=0;i<3;i++)
        {
            ref readonly var anchor=ref anchors[i];
            var error=model.AssociateParameter(endpoint,anchor.Carrier,anchor.Parameter,anchor.Lower,anchor.Upper,anchor.Surface,anchor.Witness);
            if(error!=0)return error;
            TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.ThirdFaceStation,Curve=anchor.Carrier,Generation=anchor.Generation,
                StartEvent=endpoint,EndEvent=endpoint,OtherSurface=anchor.Surface,Witness=anchor.Witness,First=anchor.Lower,Last=anchor.Upper,
                SpatialError=roots[i].SpatialError});
        }
        joined=true;return 0;
    }

    private static bool FindThreePlaneCutRoot(in RegularBoundaryView input,ReadOnlySpan<RegularFaceCut> cuts,
        BufferOffset first,BufferOffset second,BufferOffset third,ref SheetArrangement model,
        out BoundaryParameter anchor,out FiniteCurveSurfaceRoot root,out BoundaryEventId endpoint)
    {
        anchor=default;root=default;endpoint=-1;var found=false;
        for(var i=0;i<cuts.Length;i++)
        {
            ref readonly var cut=ref cuts[i];
            if(!ThirdCutHasPair(cut,first,second))continue;
            if(!TryThreePlaneCutRoot(input,cut,third,out var candidate,out var crosses))return false;
            if(!crosses)continue;
            if(found)return false;
            if(candidate.NativeEnd!=FiniteNativeEnd.None)
            {
                endpoint=(candidate.NativeEnd==FiniteNativeEnd.First?cut.Cut.LowerEvent:cut.Cut.UpperEvent)-1;
                if(!HasCutEnd(ref model,cut.Cut.Curve,endpoint,candidate.Parameter))return false;
            }
            root=candidate;anchor=ThirdCutAnchor(input,cut,third,i,0,candidate);found=true;
        }
        return found;
    }

    private static bool TryThreePlaneCutRoot(in RegularBoundaryView input,in RegularFaceCut pair,BufferOffset third,
        out FiniteCurveSurfaceRoot root,out bool crosses)
    {
        root=default;crosses=false;var curve=Curves[GetCurveSlotByTag(pair.Cut.Curve)];
        if(curve.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(curve,out var line))return false;
        var a=Surfaces[GetSurfaceSlotByTag(input.Faces[pair.First].Surface)];
        var b=Surfaces[GetSurfaceSlotByTag(input.Faces[pair.Second].Surface)];
        if(!TryPrepareSurface(a,out var first)||!TryPrepareSurface(b,out var second)||first.Kind!=SurfaceClass.Plane||second.Kind!=SurfaceClass.Plane
            ||IntervalVector.Cross(new(first.Axis),new(second.Axis)).Norm().Value.Lower<=0
            ||!ExplicitCurveExactlyOnPlane(line,first)||!ExplicitCurveExactlyOnPlane(line,second))return false;
        var edge=new ModelingEdge{Curve=pair.Cut.Curve,Start=pair.Cut.First,End=pair.Cut.Last};
        if(!TryFiniteLinePlaneEvent(edge,-1,input.Faces[third].Surface,out var result,allowExactEnd:true))return false;
        crosses=result.Count==1;root=result.FirstRoot;return true;
    }

    private static BoundaryParameter ThirdCutAnchor(in RegularBoundaryView input,in RegularFaceCut pair,BufferOffset third,
        BufferOffset cutIndex,BufferOffset rootIndex,in FiniteCurveSurfaceRoot root)
        =>new(){Carrier=pair.Cut.Curve,Generation=Curves[GetCurveSlotByTag(pair.Cut.Curve)].Header.Generation,Surface=input.Faces[third].Surface,
            Witness=1+(RegularInputEdges+cutIndex)*IntersectionPointCapacity+rootIndex,Parameter=root.Parameter,Lower=root.Lower,Upper=root.Upper};

#if KERNEL_TEST_HOOKS
    internal static int TestThreePlaneCutEvents(ReadOnlySpan<SurfTag> surfaces,ReadOnlySpan<CurveTag> carriers,bool truncate,
        Span<double> stations,out BufferCount events,double? declaredEnd=null,bool conflictingEnd=false,bool separateEnd=false)
    {
        events=0;
        Span<ModelingFace> faces=stackalloc ModelingFace[3];
        for(var i=0;i<3;i++)faces[i]=new(){Surface=surfaces[i]};
        Span<RegularFaceCut> cuts=stackalloc RegularFaceCut[4];
        cuts[0]=new(){First=0,Second=1,Cut=new(){Curve=carriers[0],First=0,Last=1}};
        cuts[1]=new(){First=0,Second=2,Cut=new(){Curve=carriers[1],First=0,Last=1}};
        cuts[2]=new(){First=1,Second=2,Cut=new(){Curve=carriers[2],First=truncate?.7:0,Last=1}};
        cuts[3]=new(){First=-1,Second=-1};
        var model=new SheetArrangement{Points=stackalloc KernelVector3[3],OriginalVertices=stackalloc VertexSlot[3],
            Parameters=stackalloc BoundaryParameter[6],Tolerance=1e-9};
        if(declaredEnd is double end)
        {
            var error=model.AddPoint(default,-1,out var first,requireProof:true);if(error!=0)return error;
            var second=first;
            if(conflictingEnd||separateEnd){error=model.AddPoint(default,-1,out second,requireProof:true);if(error!=0)return error;}
            if(end==0)cuts[0].Cut.LowerEvent=first+1;else cuts[0].Cut.UpperEvent=first+1;
            cuts[1].Cut.LowerEvent=second+1;cuts[1].Cut.First=.375;
            if(separateEnd)cuts[3].Cut.UpperEvent=second+1;
            error=model.AssociateParameter(first,carriers[0],end);if(error!=0)return error;
            error=model.AssociateParameter(second,carriers[1],.375);if(error!=0)return error;
            if(conflictingEnd){error=model.AssociateParameter(second,carriers[0],end+.125);if(error!=0)return error;}
        }
        var input=new RegularBoundaryView(default,default,default,faces);
        for(var i=0;i<3;i++)
        {
            var third=3-cuts[i].First-cuts[i].Second;
            ThirdFaceBounds cutBounds=default,faceBounds=default;
            var error=ReadThirdFaceEvents(input,third,-1,cuts,i,ref cutBounds,ref faceBounds,ref model,requireProof:true,out _);
            if(error!=0){events=model.NP;return error;}
        }
        if(separateEnd)
        {
            if(cuts[1].Cut.LowerEvent!=1||cuts[3].Cut.UpperEvent!=1||model.LiveBoundaryPoint(1))return ParasolidConstants.PK_ERROR_bad_loop;
            var error=model.AddPoint(default,-1,out var matched);
            if(error!=0||matched!=0)return ParasolidConstants.PK_ERROR_bad_loop;
        }
        for(var i=0;i<model.NP;i++)if(model.LiveBoundaryPoint(i))events++;
        for(var i=0;i<3;i++)
            stations[i]=model.KnownSourceBoundaryParameter(0,carriers[i],RegularInputEdges+i,out var station,out _)
                &&station.Witness!=0?station.Parameter:double.NaN;
        return 0;
    }
#endif
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int AssociateBoundaryCarrierSurface(ref SheetArrangement model,CurveTag carrier,double first,double last,SurfTag surface,BufferOffset inputEdge=-1)
    {
        if(model.CarrierSurfaces.IsEmpty)return 0;
        var curveGeneration=Curves[GetCurveSlotByTag(carrier)].Header.Generation;var surfaceGeneration=Surfaces[GetSurfaceSlotByTag(surface)].Header.Generation;
        foreach(var use in model.CarrierSurfaces[..model.CarrierSurfaceCount])
            if(use.Carrier==carrier&&use.Surface==surface&&use.CarrierGeneration==curveGeneration&&use.SurfaceGeneration==surfaceGeneration&&use.InputEdge==inputEdge
                &&use.Covers(first,last))return 0;
        if(model.CarrierSurfaceCount==model.CarrierSurfaces.Length)return ParasolidConstants.PK_ERROR_not_implemented;
        model.CarrierSurfaces[model.CarrierSurfaceCount++]=new(){Carrier=carrier,Surface=surface,CarrierGeneration=curveGeneration,SurfaceGeneration=surfaceGeneration,
            First=first,Last=last,InputEdge=inputEdge};return 0;
    }

    private static bool BoundaryEventOnSurface(ref SheetArrangement model,BoundaryEventId endpoint,SurfTag surface)
    {
        var original=model.OriginalVertices[endpoint];
        if(original>=0)
        {
            var body=Vertices[original].Body;var face=Bodies[body].FirstFaceBody;
            for(var f=0;f<Bodies[body].FaceCountBody;f++,face=Faces[face].NextInBody)
                if(SameSheetSupport(Faces[face].SurfTag,surface)&&FaceUsesRollingVertex(face,original))return true;
        }
        foreach(var use in model.Parameters[..model.ParameterCount])
        {
            if(use.Event!=endpoint)continue;
            var curve=GetCurveSlotByTag(use.Carrier);if(curve<0||Curves[curve].Header.Generation!=use.Generation)continue;
            if(Curves[curve].Class==CurveClass.ICurve)
            {
                var data=ICurveDataPool[Curves[curve].DataIndex];
                if(SameSheetSupport(data.Surface0Tag,surface)||SameSheetSupport(data.Surface1Tag,surface))return true;
            }
            foreach(var incidence in model.CarrierSurfaces[..model.CarrierSurfaceCount])
                if(incidence.Carrier==use.Carrier&&incidence.CarrierGeneration==use.Generation
                    &&incidence.Covers(use.Lower,use.Upper)
                    &&(incidence.InputEdge<0||use.Witness==0||incidence.InputEdge==use.SourceOccurrence)
                    &&Surfaces[GetSurfaceSlotByTag(incidence.Surface)].Header.Generation==incidence.SurfaceGeneration
                    &&SameSheetSupport(incidence.Surface,surface))return true;
        }
        return false;
    }

#if KERNEL_TEST_HOOKS
    internal static int TestBoundedCarrierIncidence(CurveTag curve,SurfTag surface,double lower,double upper,out bool incident)
    {
        incident=false;
        var model=new SheetArrangement{Points=stackalloc KernelVector3[1],OriginalVertices=stackalloc VertexSlot[1],
            Parameters=stackalloc BoundaryParameter[1],CarrierSurfaces=stackalloc BoundaryCarrierSurface[2]};
        var error=AssociateBoundaryCarrierSurface(ref model,curve,0,1,surface);if(error!=0)return error;
        error=AssociateBoundaryCarrierSurface(ref model,curve,2,3,surface);if(error!=0)return error;
        error=model.AddPoint(default,-1,out var point,requireProof:true);if(error!=0)return error;
        error=model.AssociateParameter(point,curve,lower+(upper-lower)*.5,lower,upper);if(error!=0)return error;
        incident=BoundaryEventOnSurface(ref model,point,surface);return 0;
    }

    internal static int TestFiniteCutIncidence(CurveTag cut,ReadOnlySpan<CurveTag> witnesses,ReadOnlySpan<SurfTag> surfaces,
        bool remoteUse,out bool associated,out double parameter)
    {
        associated=false;parameter=0;
        var model=new SheetArrangement{Points=stackalloc KernelVector3[1],OriginalVertices=stackalloc VertexSlot[1],
            Parameters=stackalloc BoundaryParameter[3],CarrierSurfaces=stackalloc BoundaryCarrierSurface[5],Tolerance=OperationIntersection.DistanceTolerance};
        var error=model.AddPoint(Vector(.5,0,0),-1,out var point,requireProof:true);if(error!=0)return error;
        for(var i=0;i<2;i++)
        {
            error=AssociateBoundaryCarrierSurface(ref model,cut,0,1,surfaces[i]);if(error!=0)return error;
            error=AssociateBoundaryCarrierSurface(ref model,witnesses[i],-1,1,surfaces[i]);if(error!=0)return error;
            error=model.AssociateParameter(point,witnesses[i],0);if(error!=0)return error;
        }
        error=AssociateBoundaryCarrierSurface(ref model,cut,remoteUse?2:0,remoteUse?3:1,surfaces[2]);if(error!=0)return error;
        error=AssociateCompatibleBoundaryEvent(ref model,point,cut,0,1,out associated);
        if(associated)model.KnownParameter(point,cut,out parameter);
        return error;
    }
#endif

    private static int ReadRollingBoundaryAssociations(in RegularBoundaryView input,ReadOnlySpan<EdgeSlot> original,ref SheetArrangement model)
    {
        for(var face=0;face<input.Faces.Length;face++)
        {
            var record=input.Faces[face];
            for(var l=0;l<record.LoopCount;l++)
            {
                var loop=input.Loops[record.FirstLoop+l];
                foreach(var coedge in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                {
                    var edge=input.Edges[coedge.Edge];
                    var error=AssociateBoundaryCarrierSurface(ref model,edge.Curve,edge.Start,edge.End,record.Surface,coedge.Edge);if(error!=0)return error;
                }
            }
        }
        for(var e=0;e<input.Edges.Length;e++)if(original[e]<0)
        {
            var edge=input.Edges[e];var positive=LocalSenseSign(Curves[GetCurveSlotByTag(edge.Curve)].Sense)>0;
            for(var end=0;end<2;end++)
            {
                var vertex=(end==0)==positive?edge.StartVertex:edge.EndVertex;if(vertex<0)continue;
                var error=model.AssociateParameter(vertex,edge.Curve,end==0?edge.Start:edge.End);if(error!=0)return error;
            }
        }
        return 0;
    }

    private static int ReadRollingThirdSurfaceEvents(in RegularBoundaryView input,ReadOnlySpan<FaceSlot> sourceFaces,
        ReadOnlySpan<RollingBandCandidate> bands,ReadOnlySpan<RegularFaceCut> cuts,ref SheetArrangement model)
    {
        Span<ThirdFaceBounds> faceBounds=stackalloc ThirdFaceBounds[sourceFaces.Length];faceBounds.Clear();
        for(var i=0;i<cuts.Length;i++)
        {
            ref readonly var accepted=ref cuts[i];
            if(accepted.First<sourceFaces.Length||accepted.Second<sourceFaces.Length)continue;
            var a=bands[accepted.First-sourceFaces.Length];var b=bands[accepted.Second-sourceFaces.Length];
            ThirdFaceBounds cutBounds=default;
            for(var f=0;f<sourceFaces.Length;f++)
            {
                // The original convex supports' contacts have already
                // participated in the pair's finite clipping.
                var face=sourceFaces[f];
                if(face==a.FirstSupport||face==a.SecondSupport||face==b.FirstSupport||face==b.SecondSupport)continue;
                LastRollingFirstFace=accepted.First;LastRollingSecondFace=f;
                var error=ReadThirdFaceEvents(input,f,face,cuts,i,ref cutBounds,ref faceBounds[f],ref model,requireProof:true,out var relation);
                TraceRollingNetwork?.Invoke(new(){Stage=RollingNetworkStage.BoundaryIncidence,CellSource=face,Surface=input.Faces[f].Surface,
                    Other=a.Surface,OtherBand=b.Surface,Curve=accepted.Cut.Curve,First=accepted.Cut.First,Last=accepted.Cut.Last,Relation=relation});
                if(error!=0)return error;
            }
        }
        return 0;
    }

    private static int AssociateCompatibleBoundaryEvent(ref SheetArrangement model,BoundaryEventId endpoint,CurveTag curve,
        double first,double last,out bool associated)
    {
        associated=false;SurfTag firstSupport=0;var independent=false;
        ref readonly var record=ref Curves[GetCurveSlotByTag(curve)];
        if(record.Class==CurveClass.ICurve)
        {
            var data=ICurveDataPool[record.DataIndex];firstSupport=data.Surface0Tag;independent=!SameSheetSupport(firstSupport,data.Surface1Tag);
            if(!BoundaryEventOnSurface(ref model,endpoint,firstSupport)||!BoundaryEventOnSurface(ref model,endpoint,data.Surface1Tag))return 0;
        }
        foreach(var use in model.CarrierSurfaces[..model.CarrierSurfaceCount])if(use.Carrier==curve&&use.CarrierGeneration==record.Header.Generation
            &&use.Covers(first,last)&&Surfaces[GetSurfaceSlotByTag(use.Surface)].Header.Generation==use.SurfaceGeneration)
        {
            if(!BoundaryEventOnSurface(ref model,endpoint,use.Surface))return 0;
            if(firstSupport==0)firstSupport=use.Surface;else if(!SameSheetSupport(firstSupport,use.Surface))independent=true;
        }
        if(!independent)return 0;
        LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;LastSheetCurve=curve;LastSheetParameter=endpoint;
        var point=model.Points[endpoint];var tolerance=model.VertexPrecisions.IsEmpty?model.Tolerance:Math.Max(model.Tolerance,model.VertexPrecisions[endpoint]);
        var px=new IntervalNumber(point.X)+new IntervalNumber(-tolerance,tolerance);
        var py=new IntervalNumber(point.Y)+new IntervalNumber(-tolerance,tolerance);
        var pz=new IntervalNumber(point.Z)+new IntervalNumber(-tolerance,tolerance);
        var parameter=0.0;Span<KernelVector3> value=stackalloc KernelVector3[1];
        if(record.Class==CurveClass.ICurve)
        {
            var data=ICurveDataPool[record.DataIndex];var procedural=data.ProgramBlock>0;
            var program=procedural?GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount):default;
            var spine=procedural?program.Spines[data.ProgramSpine]:default;var charts=procedural?program.Charts.Slice(spine.ChartOffset,spine.ChartCount):default;
            ICurveView view=default;
            if(!procedural){var prepared=TryPrepareICurveView(record,out view,out _);if(prepared!=AlgorithmStatus.Success)return IntersectionError(prepared);}
            var count=procedural?charts.Length:view.ChartParameters.Length;var found=false;
            if(first<(procedural?charts[0].Parameter:view.ChartParameters[0])||last>(procedural?charts[^1].Parameter:view.ChartParameters[^1]))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            for(var chart=0;chart<count-1;chart++)
            {
                // This is the native CHART gauge, restricted to its known
                // branch and finite occurrence. Inspect all possible gauges;
                // a second distinct native answer is unresolved, not nearest.
                var position=procedural?charts[chart].Position:view.ChartPositions[chart];var chord=procedural?charts[chart].Chord:view.ChartChordUnits[chart];
                var native=procedural?charts[chart].Parameter:view.ChartParameters[chart];var scale=procedural?charts[chart].Scale:view.ChartScales[chart];
                var next=procedural?charts[chart+1].Parameter:view.ChartParameters[chart+1];
                var gauge=new IntervalNumber(native)+scale*((px-position.X)*chord.X+(py-position.Y)*chord.Y+(pz-position.Z)*chord.Z);
                var lo=Math.Max(Math.Max(first,native),gauge.Lower);var hi=Math.Min(Math.Min(last,next),gauge.Upper);if(hi<lo)continue;
                var at=Math.Clamp(native+scale*Dot(chord,Sub(point,position)),lo,hi);
                var status=EvaluateCurveCore(in record,at,0,false,value,out _);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                if(OperationIntersection.Norm(Sub(point,value[0]))>tolerance)
                {
                    // A different regular component can have the same
                    // support pair. Prove separation on every possible native
                    // gauge interval of the event box; a failed point inverse
                    // is not a proof that this branch is absent.
                    status=CertifiedIntersectionCurveExtent(curve,lo,hi,out var bounds);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    if(bounds.XHi<px.Lower||bounds.XLo>px.Upper||bounds.YHi<py.Lower||bounds.YLo>py.Upper||bounds.ZHi<pz.Lower||bounds.ZLo>pz.Upper)continue;
                    return ParasolidConstants.PK_ERROR_cant_do_intersect;
                }
                if(found&&at!=parameter)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                found=true;parameter=at;
            }
            if(!found)return 0;
        }
        else
        {
            if(record.Class is not (CurveClass.Line or CurveClass.Circle or CurveClass.Ellipse))return ParasolidConstants.PK_ERROR_not_implemented;
            var vector=new PK_VECTOR_s();WriteVector(&vector,point);var error=CurveParameteriseVectorCore(curve,vector,&parameter);if(error!=0)return error;
            LiftPeriodicEdgeParameter(curve,first,last,ref parameter);if(parameter<first||parameter>last)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            var status=EvaluateCurveCore(in record,parameter,0,false,value,out _);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            if(OperationIntersection.Norm(Sub(point,value[0]))>tolerance)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        }
        var added=model.AssociateParameter(endpoint,curve,parameter);if(added!=0)return added;associated=true;return 0;
    }
}

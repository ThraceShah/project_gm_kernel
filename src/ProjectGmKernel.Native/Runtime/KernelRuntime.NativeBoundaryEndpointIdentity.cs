using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A declared cut end can rediscover an actual input vertex through a
    // different carrier. Resolve it before splitting, using the input's
    // finite native parameter, independent support incidence and complete
    // interval geometry. Isolated interior root identities are never merged
    // by this native-precision endpoint rule.
    private static int JoinNativeSourceEndpoints(in RegularBoundaryView input,Span<RegularFaceCut> cuts,ref SheetArrangement model)
    {
        Span<BoundaryEventId> ends=stackalloc BoundaryEventId[2];
        for(var p=0;p<model.NP;p++)
        {
            if(!model.LiveBoundaryPoint(p)||model.OriginalVertices[p]>=0)continue;
            var isolated=false;foreach(var row in model.Parameters[..model.ParameterCount])if(row.Event==p&&row.Witness!=0){isolated=true;break;}
            if(isolated)continue;
            for(var e=0;e<input.Edges.Length;e++)
            {
                var edge=input.Edges[e];
                if(!model.KnownSourceBoundaryParameter(p,edge.Curve,e,out var anchor,out _)
                    ||anchor.Witness!=0||anchor.Surface!=0||anchor.SourceOccurrence>=0)continue;
                var range=new IntervalNumber(anchor.Lower,anchor.Upper);if(!range.Finite)continue;
                if(range.Lower<=edge.Start&&range.Upper>=edge.End)continue;
                var positive=LocalSenseSign(Curves[GetCurveSlotByTag(edge.Curve)].Sense)>0;
                for(var side=0;side<2;side++)
                {
                    var station=side==0?edge.Start:edge.End;
                    if(station<range.Lower||station>range.Upper)continue;
                    var vertex=(side==0)==positive?edge.StartVertex:edge.EndVertex;
                    if(vertex<0||!model.LiveBoundaryPoint(vertex)||model.OriginalVertices[vertex]<0)continue;
                    var point=model.Points[vertex];
                    var precision=Math.Max(OperationIntersection.DistanceTolerance,Vertices[model.OriginalVertices[vertex]].Tolerance);
                    if(!NativeEndpointCarrierBall(edge.Curve,range,point,precision)
                        ||(new IntervalVector(model.Points[p])-new IntervalVector(point)).Norm().Value.Upper>precision)continue;
                    var supported=false;var valid=true;
                    foreach(var use in model.CarrierSurfaces[..model.CarrierSurfaceCount])
                    {
                        if(use.Carrier==edge.Curve||!model.KnownBoundaryParameter(p,use.Carrier,out var other))continue;
                        if(other.Generation!=use.CarrierGeneration||!use.Covers(other.Lower,other.Upper)
                            ||Surfaces[GetSurfaceSlotByTag(use.Surface)].Header.Generation!=use.SurfaceGeneration
                            ||!BoundaryEventOnSurface(ref model,vertex,use.Surface)
                            ||!NativeEndpointCarrierBall(use.Carrier,new(other.Lower,other.Upper),point,precision))
                        {valid=false;break;}
                        // Two different supporting surfaces select a native
                        // branch; a bare near-XYZ point supplies no incidence.
                        foreach(var second in model.CarrierSurfaces[..model.CarrierSurfaceCount])
                            if(second.Carrier==use.Carrier&&second.CarrierGeneration==use.CarrierGeneration
                                &&second.Covers(other.Lower,other.Upper)&&!SameSheetSupport(second.Surface,use.Surface))supported=true;
                    }
                    if(!valid||!supported)continue;
                    foreach(ref var row in model.Parameters[..model.ParameterCount])
                        if(row.Event==p&&row.Carrier==edge.Curve&&row.Generation==anchor.Generation
                            &&row.Witness==0&&row.Surface==0&&row.SourceOccurrence<0)
                        {row.Parameter=station;row.Lower=row.Upper=station;}
                    ends[0]=p;ends[1]=vertex;
                    var error=JoinProvenBoundaryEndpoints(input,cuts,ends,point,ref model,out _,original:vertex);
                    if(error!=0)return error;
                    break;
                }
                if(!model.LiveBoundaryPoint(p))break;
            }
        }
        return 0;
    }

    private static bool NativeEndpointCarrierBall(CurveTag tag,IntervalNumber range,in KernelVector3 point,double precision)
    {
        var slot=GetCurveSlotByTag(tag);
        if(slot<0||!range.Finite||!TryReadExplicitSpineCurve(Curves[slot],out var curve)
            ||SpineParameterMap.Interval(curve,new(range,0),out var position,out _)!=AlgorithmStatus.Success)return false;
        var distance=(position-new IntervalVector(point)).Norm().Value;
        return distance.Finite&&distance.Upper<=precision;
    }
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int PlanarLoopWinding(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,
        PK_UV_s query,double du,double dv,out BufferCount winding)
    {
        winding=0;TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(surface)],out var plane);
        var direction=Add(Scale(plane.X,du),Scale(plane.Y,dv));ProfileNormalize(ref direction);
        var axis=ProfileCross(plane.Axis,direction);var origin=Add(plane.Origin,Add(Scale(plane.X,query.param[0]),Scale(plane.Y,query.param[1])));
        var cutter=new AnalyticSurface(SurfaceClass.Plane,origin,axis,plane.Axis);
            Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
            foreach(ref readonly var coedge in loop)
            {
                var edge=edges[coedge.Edge];LastSheetCurve=edge.Curve;LastSheetPartitionStage=SheetPartitionStage.RayIntersections;
                var status=IntersectSheetCurvePlane(edge.Curve,edge.Start,edge.End,cutter,roots,out var n);LastSheetAlgorithmStatus=status;
                if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                for(var i=0;i<n;i++)
                {
                    if(roots[i].Kind!=IntersectionVectorKind.Simple)return ParasolidConstants.PK_ERROR_eval_failure;
                    var along=Dot(Sub(roots[i].Point,origin),direction);
                    if(along< -1e-10)continue;if(Math.Abs(along)<1e-10)return ParasolidConstants.PK_ERROR_eval_failure;
                    var epsilon=1e-10*Math.Max(1,edge.End-edge.Start);var t=roots[i].First;
                    if(edge.StartVertex>=0&&(Math.Abs(t-edge.Start)<epsilon||Math.Abs(t-edge.End)<epsilon))return ParasolidConstants.PK_ERROR_eval_failure;
                    var error=SheetCurveUV(surface,edge.Curve,t,coedge.Forward!=0,out _,out var tangent,out _);if(error!=0)return error;
                    var crossing=tangent.param[1]*du-tangent.param[0]*dv;
                    if(Math.Abs(crossing)<1e-10*(Math.Abs(tangent.param[0])+Math.Abs(tangent.param[1])))return ParasolidConstants.PK_ERROR_eval_failure;
                    winding+=crossing>0?1:-1;
                }
            }
            return 0;
    }

    // Only sets a witness-search step. Exact winding validates every candidate.
    private static double PlanarLoopParameterScale(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop)
    {
        var loU=double.PositiveInfinity;var loV=loU;var hiU=double.NegativeInfinity;var hiV=hiU;
        foreach(ref readonly var coedge in loop)for(var i=0;i<3;i++)
        {
            if(SheetCoedgePoint(surface,edges[coedge.Edge],coedge.Forward,i*.5,out var uv,out _,out _)!=0)return 1;
            loU=Math.Min(loU,uv.param[0]);hiU=Math.Max(hiU,uv.param[0]);loV=Math.Min(loV,uv.param[1]);hiV=Math.Max(hiV,uv.param[1]);
        }
        var scale=Math.Max(hiU-loU,hiV-loV);return scale>0&&double.IsFinite(scale)?scale:1;
    }
}

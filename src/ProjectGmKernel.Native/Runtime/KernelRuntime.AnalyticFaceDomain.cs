using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Public inverse parameterization keeps its natural principal value.
    // A finite edge can use the equivalent next turn across the periodic seam.
    private static void LiftPeriodicEdgeParameter(CurveTag curve,double first,double last,ref double parameter)
    {
        var record=Curves[GetCurveSlotByTag(curve)];
        if(record.Class is not (CurveClass.Circle or CurveClass.Ellipse))return;
        var epsilon=1e-10*Math.Max(1,last-first);
        parameter+=Math.Tau*Math.Ceiling((first-parameter-epsilon)/Math.Tau);
    }

    // A cylinder is periodic in u. An axial UV ray avoids an artificial seam:
    // its exact spatial carrier is a radial half-plane, not a UV polygon.
    // Noncontractible top/bottom loops then combine into the finite face band.
    private static int CylindricalLoopWinding(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,
        ReadOnlySpan<ModelingCoedge> loop,PK_UV_s query,out BufferCount winding)
    {
        winding=0;if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(surface)],out var cylinder))return ParasolidConstants.PK_ERROR_not_implemented;
        var radial=Add(Scale(cylinder.X,Math.Cos(query.param[0])),Scale(cylinder.Y,Math.Sin(query.param[0])));
        var cutter=new AnalyticSurface(SurfaceClass.Plane,cylinder.Origin,ProfileCross(cylinder.Axis,radial),cylinder.Axis);
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        for(var c=0;c<loop.Length;c++)
        {
            var coedge=loop[c];var edge=edges[coedge.Edge];
            var status=IntersectSheetCurvePlane(edge.Curve,edge.Start,edge.End,cutter,roots,out var count);
            if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            for(var i=0;i<count;i++)
            {
                var root=roots[i];if(Dot(Sub(root.Point,cylinder.Origin),radial)<=0)continue;
                if(root.Kind!=IntersectionVectorKind.Simple)return ParasolidConstants.PK_ERROR_eval_failure;
                var along=Dot(Sub(root.Point,cylinder.Origin),cylinder.Axis)-query.param[1];
                if(along< -1e-10)continue;if(Math.Abs(along)<1e-10)return ParasolidConstants.PK_ERROR_eval_failure;
                var duplicate=false;for(var j=0;j<i;j++)
                {
                    if(Dot(Sub(roots[j].Point,cylinder.Origin),radial)<=0||ProfileDistance(root.Point,roots[j].Point)>=1e-9)continue;
                    var periodic=Curves[GetCurveSlotByTag(edge.Curve)].Class is CurveClass.Circle or CurveClass.Ellipse;
                    if(!periodic||Math.Abs(Math.Abs(root.First-roots[j].First)-Math.Tau)>1e-10*Math.Max(1,edge.End-edge.Start))
                        return ParasolidConstants.PK_ERROR_eval_failure;
                    duplicate=true;
                }
                if(duplicate)continue;
                var epsilon=1e-10*Math.Max(1,edge.End-edge.Start);
                if(edge.StartVertex>=0&&(Math.Abs(root.First-edge.Start)<epsilon||Math.Abs(root.First-edge.End)<epsilon))
                    return ParasolidConstants.PK_ERROR_eval_failure;
                var error=SheetCurveUV(surface,edge.Curve,root.First,coedge.Forward!=0,out _,out var tangent,out _);if(error!=0)return error;
                if(Math.Abs(tangent.param[0])<=1e-10*(Math.Abs(tangent.param[0])+Math.Abs(tangent.param[1])))
                    return ParasolidConstants.PK_ERROR_eval_failure;
                winding+=tangent.param[0]<0?1:-1;
            }
        }
        return 0;
    }
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A complete coaxial circle is a constant-v boundary in the surface's
    // native angular chart. Certify its whole plane, centre and radius;
    // an isolated XYZ inverse cannot establish that correspondence.
    private static bool TryCoaxialCircleChart(SurfTag surface,in CurveRecord carrier,out double phase,out double direction,out double station)
    {
        phase=direction=station=0;if(carrier.Class!=CurveClass.Circle||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(surface)],out var support)
            ||support.Kind is not (SurfaceClass.Cylinder or SurfaceClass.Cone or SurfaceClass.Torus))return false;
        var data=CircleDataPool[carrier.DataIndex];var center=Vector(data.CenterX,data.CenterY,data.CenterZ);
        var axis=Vector(data.AxisX,data.AxisY,data.AxisZ);var reference=Vector(data.RefDirX,data.RefDirY,data.RefDirZ);
        var a=new IntervalVector(support.Axis);var delta=new IntervalVector(center)-new IntervalVector(support.Origin);
        var axial=IntervalVector.Dot(delta,a).Value;
        var arithmetic=128*SmallLinearSolve.MachineEpsilon;
        var tolerance=(arithmetic*(new IntervalVector(center).Norm().Value+new IntervalVector(support.Origin).Norm().Value
            +support.Radius+data.Radius)).Upper;
        if(!(tolerance>0&&tolerance<=1e-10)||IntervalVector.Cross(a,new IntervalVector(axis)).Norm().Value.Upper>arithmetic
            ||IntervalVector.Cross(a,delta).Norm().Value.Upper>tolerance)return false;
        var alignment=IntervalVector.Dot(a,new IntervalVector(axis)).Value;
        if(alignment.ContainsZero)return false;direction=alignment.Lower>0?1:-1;
        if(support.Kind==SurfaceClass.Torus)
        {
            var radial=new IntervalNumber(data.Radius)-support.Radius;
            // The axial provider needs the outer meridian only. Its finite
            // chart lies inside a half turn and does not cross the V seam.
            if(radial.Lower< -tolerance)return false;
            var radius=IntervalDual.Sqrt(new IntervalDual(IntervalNumber.Square(axial)+IntervalNumber.Square(radial),0)).Value;
            if((radius-support.Secondary).Magnitude>tolerance)return false;
            station=Math.Atan2(axial.Midpoint,radial.Midpoint);
        }
        else
        {
            var radius=support.Kind==SurfaceClass.Cone?new IntervalNumber(support.Radius)+axial*support.Secondary:new IntervalNumber(support.Radius);
            if(!(radius.Lower>0)||(radius-data.Radius).Magnitude>tolerance)return false;
            station=axial.Midpoint;
        }
        phase=Math.Atan2(Dot(reference,support.Y),Dot(reference,support.X));return true;
    }

    private static bool TryCoaxialRingWinding(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,
        double v,out BufferCount winding,out int error)
    {
        winding=error=0;if(loop.Length!=1)return false;var use=loop[0];var edge=edges[use.Edge];
        if(edge.StartVertex>=0||edge.EndVertex>=0||edge.End-edge.Start!=Math.Tau)return false;
        var curve=Curves[GetCurveSlotByTag(edge.Curve)];if(!TryCoaxialCircleChart(surface,curve,out _,out var direction,out var station))return false;
        var delta=station-v;
        if(Math.Abs(delta)<=32*SmallLinearSolve.MachineEpsilon*Math.Max(1,Math.Abs(station)))error=ParasolidConstants.PK_ERROR_eval_failure;
        else if(delta>0)winding=direction*LocalSenseSign(curve.Sense)*(use.Forward!=0?1:-1)<0?1:-1;
        return true;
    }

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
    private static int AxialCylindricalLoopWinding(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,
        ReadOnlySpan<ModelingCoedge> loop,PK_UV_s query,out BufferCount winding)
    {
        winding=0;if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(surface)],out var cylinder))return ParasolidConstants.PK_ERROR_not_implemented;
        var radial=Add(Scale(cylinder.X,Math.Cos(query.param[0])),Scale(cylinder.Y,Math.Sin(query.param[0])));
        var cutter=new AnalyticSurface(SurfaceClass.Plane,cylinder.Origin,ProfileCross(cylinder.Axis,radial),cylinder.Axis);
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        for(var c=0;c<loop.Length;c++)
        {
            var coedge=loop[c];var edge=edges[coedge.Edge];
            LastSheetCurve=edge.Curve;LastSheetPartitionStage=SheetPartitionStage.RayIntersections;
            var status=IntersectSheetCurvePlane(edge.Curve,edge.Start,edge.End,cutter,roots,out var count);LastSheetAlgorithmStatus=status;
            if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            for(var i=0;i<count;i++)
            {
                var root=roots[i];if(Dot(Sub(root.Point,cylinder.Origin),radial)<=0)continue;
                LastSheetParameter=root.First;
                if(root.Kind!=IntersectionVectorKind.Simple){LastSheetPartitionStage=SheetPartitionStage.RayTangency;return ParasolidConstants.PK_ERROR_eval_failure;}
                var along=Dot(Sub(root.Point,cylinder.Origin),cylinder.Axis)-query.param[1];
                if(along< -1e-10)continue;if(Math.Abs(along)<1e-10){LastSheetPartitionStage=SheetPartitionStage.RayOrigin;return ParasolidConstants.PK_ERROR_eval_failure;}
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
                {LastSheetPartitionStage=SheetPartitionStage.RayEndpoint;return ParasolidConstants.PK_ERROR_eval_failure;}
                var error=SheetCurveUV(surface,edge.Curve,root.First,coedge.Forward!=0,out _,out var tangent,out _);if(error!=0)return error;
                if(Math.Abs(tangent.param[0])<=1e-10*(Math.Abs(tangent.param[0])+Math.Abs(tangent.param[1])))
                {LastSheetPartitionStage=SheetPartitionStage.RayTangency;return ParasolidConstants.PK_ERROR_eval_failure;}
                winding+=tangent.param[0]<0?1:-1;
            }
        }
        return 0;
    }

}

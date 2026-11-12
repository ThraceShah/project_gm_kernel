using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus IntersectNativeSpCurvePlane(in IntersectionCurveSpan span,in AnalyticSurface plane,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var record=Curves[span.Slot];if(record.Class!=CurveClass.SPCurve)return AlgorithmStatus.Unsupported;
        var data=SpCurveDataPool[record.DataIndex];var support=GetSurfaceSlotByTag(data.SurfTag);var basis=GetCurveSlotByTag(data.BCurveTag);
        if(support<0||basis<0||Surfaces[support].Class!=SurfaceClass.BSurface||Curves[basis].Class!=CurveClass.BCurve)return AlgorithmStatus.Unsupported;
        var surface=GetBSurfaceView(BSurfaceDataStore[Surfaces[support].DataIndex]);var curve=GetBCurveView(BCurveDataStore[Curves[basis].DataIndex]);
        if(span.Lower<curve.Start||span.Upper>curve.End||curve.Periodic)return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> composed=stackalloc IntervalNumber[4*17];
        Span<ScalarRootIsolation> isolation=stackalloc ScalarRootIsolation[output.Length];
        for(var knot=curve.Degree;knot<curve.VertexCount;knot++)
        {
            var lo=Math.Max(span.Lower,curve.Knots[knot]);var hi=Math.Min(span.Upper,curve.Knots[knot+1]);if(!(hi>lo))continue;
            var status=SpCurveBezier.FromSpline(surface,curve,knot,lo,hi,composed,out var degree);if(status!=AlgorithmStatus.Success)return status;
            var path=new SurfaceIntersectionPatch(new BezierSurfacePatch(composed[..(4*(degree+1))],degree,0,new(lo,hi,0,1)));
            status=SurfaceIsoPlaneRoots.Find(path,plane,lo,hi,output,ref count,strictSeparation:true,isolation:isolation);if(status!=AlgorithmStatus.Success)return status;
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }
}

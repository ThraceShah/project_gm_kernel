using System.Runtime.InteropServices;
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
        var pieces=0;
        for(var knot=curve.Degree;knot<curve.VertexCount;knot++)
        {
            if(Math.Min(span.Upper,curve.Knots[knot+1])>Math.Max(span.Lower,curve.Knots[knot]))pieces++;
        }
        if(pieces==0)return AlgorithmStatus.Success;
        if(pieces>1&&(!GeneralSurfaceContinuity(surface)||!SpCurveBezier.HasC1Joins(curve.Knots,curve.Degree,curve.VertexCount)))return AlgorithmStatus.Unsupported;
        var degree=curve.Degree*(surface.UDegree+surface.VDegree);if(degree is <1 or >16)return AlgorithmStatus.Unsupported;
        var width=4*(degree+1);var perPiece=(width*sizeof(IntervalNumber)+sizeof(SurfaceParameterBox))/sizeof(double);
        if(pieces>int.MaxValue/perPiece)return AlgorithmStatus.WorkspaceTooSmall;
        var memory=pieces==1?default:CommandScratchBorrow(pieces*perPiece);if(pieces>1&&memory.IsEmpty)return AlgorithmStatus.WorkspaceTooSmall;
        try
        {
            var poleWords=pieces*width*sizeof(IntervalNumber)/sizeof(double);
            Span<IntervalNumber> composed=pieces==1?stackalloc IntervalNumber[4*17]:MemoryMarshal.Cast<double,IntervalNumber>(memory[..poleWords]);
            Span<SurfaceParameterBox> domains=pieces==1?stackalloc SurfaceParameterBox[1]:MemoryMarshal.Cast<double,SurfaceParameterBox>(memory[poleWords..]);
            var piece=0;
            for(var knot=curve.Degree;knot<curve.VertexCount;knot++)
            {
                var lo=Math.Max(span.Lower,curve.Knots[knot]);var hi=Math.Min(span.Upper,curve.Knots[knot+1]);if(!(hi>lo))continue;
                var status=SpCurveBezier.FromSpline(surface,curve,knot,lo,hi,composed.Slice(piece*width,width),out _);if(status!=AlgorithmStatus.Success)return status;
                domains[piece++]=new(lo,hi,0,1);
            }
            var path=new SurfaceIntersectionPatch(composed[..(pieces*width)],domains,degree);
            Span<ScalarRootIsolation> isolation=stackalloc ScalarRootIsolation[output.Length];
            var found=SurfaceIsoPlaneRoots.Find(path,plane,span.Lower,span.Upper,output,ref count,strictSeparation:true,isolation:isolation);
            if(found==AlgorithmStatus.Success)OperationIntersection.Sort(output[..count]);return found;
        }
        finally{CommandScratchReturn(memory);}
    }
}

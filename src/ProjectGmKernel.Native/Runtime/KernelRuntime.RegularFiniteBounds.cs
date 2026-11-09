using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A bounded planar face lies in the coordinate hull of its complete
    // boundary. Curved supports use their complete finite support domain.
    private static AlgorithmStatus RegularFiniteFaceExtent(FaceSlot face,out IntervalBox3 extent)
    {
        extent=default;var slot=GetSurfaceSlotByTag(Faces[face].SurfTag);
        if(slot<0)return AlgorithmStatus.InvalidInput;
        if(Surfaces[slot].Class==SurfaceClass.Cylinder)return CertifiedCylindricalFaceExtent(face,out extent);
        if(Surfaces[slot].Class!=SurfaceClass.Plane)return RegularSurfaceExtent(slot,out extent);
        Span<IntervalNumber> poles=stackalloc IntervalNumber[4*17],bounds=stackalloc IntervalNumber[9];
        var first=true;var loop=Faces[face].FirstLoop;
        for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            var fin=Loops[loop].FirstFin;
            for(var f=0;f<Loops[loop].FinCount;f++,fin=Fins[fin].NextInLoop)
            {
                var edge=Edges[Fins[fin].Edge];if(!TryProfileEdgeRange(edge,out var lo,out var hi))return AlgorithmStatus.Unsupported;
                var carrier=GetCurveSlotByTag(edge.CurveTag);if(carrier<0)return AlgorithmStatus.Unsupported;
                if(Curves[carrier].Class==CurveClass.ICurve)
                {
                    var owns=EnsureCommandScratch();AlgorithmStatus status;IntervalBox3 trim;
                    try{status=CertifiedIntersectionCurveExtent(edge.CurveTag,lo,hi,out trim);}
                    finally{if(owns)ReleaseCommandScratch(State.Session);}
                    if(status!=AlgorithmStatus.Success)return status;
                    bounds[0]=new(trim.XLo,trim.XHi);bounds[1]=new(trim.YLo,trim.YHi);bounds[2]=new(trim.ZLo,trim.ZHi);
                    IncludeRegularExtent(ref extent,ref first,bounds);continue;
                }
                var interval=new Generated.PK_INTERVAL_s();interval.value[0]=lo;interval.value[1]=hi;
                if(ResolveIntersectionCurve(edge.CurveTag,interval,out var curve)!=0)return AlgorithmStatus.Unsupported;
                var count=GeneralCurvePieceCount(curve);if(count<=0)return AlgorithmStatus.Unsupported;
                for(var p=0;p<count;p++)
                {
                    var status=GeneralCurvePiece(curve,p,poles,out var degree,out var domain,out var a,out var b);if(status!=AlgorithmStatus.Success)return status;
                    if(!(b>a))continue;
                    status=BezierSurfaceBounds.Bound(new(poles[..(4*(degree+1))],degree,0,domain),new(a,b,0,0),bounds);if(status!=AlgorithmStatus.Success)return status;
                    IncludeRegularExtent(ref extent,ref first,bounds);
                }
            }
        }
        return first?AlgorithmStatus.Unsupported:AlgorithmStatus.Success;
    }

    private static void IncludeRegularExtent(ref IntervalBox3 extent,ref bool first,ReadOnlySpan<IntervalNumber> bounds)
    {
        extent=first?new(bounds[0].Lower,bounds[0].Upper,bounds[1].Lower,bounds[1].Upper,bounds[2].Lower,bounds[2].Upper)
            :new(Math.Min(extent.XLo,bounds[0].Lower),Math.Max(extent.XHi,bounds[0].Upper),Math.Min(extent.YLo,bounds[1].Lower),Math.Max(extent.YHi,bounds[1].Upper),Math.Min(extent.ZLo,bounds[2].Lower),Math.Max(extent.ZHi,bounds[2].Upper));
        first=false;
    }
}

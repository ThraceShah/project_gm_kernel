using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Bounds;

internal static class BezierPlaneResidual
{
    // Apply the plane's linear functional to the original homogeneous net
    // before rational projection. Interval poles retain every rounding term;
    // XYZ hulls need not lose their common weight and origin correlation.
    internal static bool TryBound(in BezierSurfacePatch patch,in AnalyticSurface plane,in SurfaceParameterBox box,out IntervalNumber residual)
    {
        residual=default;if(plane.Kind!=SurfaceClass.Plane||patch.Poles.IsEmpty)return false;
        var count=4*(patch.UDegree+1)*(patch.VDegree+1);
        Span<IntervalNumber> poles=stackalloc IntervalNumber[count];
        for(var at=0;at<count;at+=4)
        {
            var w=patch.Poles[at+3];
            poles[at]=(patch.Poles[at]-plane.Origin.X*w)*plane.Axis.X
                +(patch.Poles[at+1]-plane.Origin.Y*w)*plane.Axis.Y
                +(patch.Poles[at+2]-plane.Origin.Z*w)*plane.Axis.Z;
            poles[at+1]=poles[at+2]=0;poles[at+3]=w;
        }
        Span<IntervalNumber> position=stackalloc IntervalNumber[3];
        var status=BezierSurfaceBounds.BoundPosition(new(poles,patch.UDegree,patch.VDegree,patch.Domain),box,position);
        if(status!=AlgorithmStatus.Success)return false;
        residual=position[0];return residual.Finite;
    }
}

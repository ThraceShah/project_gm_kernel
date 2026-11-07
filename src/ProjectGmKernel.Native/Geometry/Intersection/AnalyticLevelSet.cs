using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Intersection;

// A length-scaled implicit equation and its two chart derivatives. The
// cylinder/sphere equation is (radial^2-r^2)/(2r), so zero sets are exact;
// no fitted parametric surrogate or periodic chart inverse enters isolation.
internal static class AnalyticLevelSet
{
    internal static bool Supported(in AnalyticSurface surface)=>surface.Kind is SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Sphere;

    internal static IntervalNumber Value(ReadOnlySpan<IntervalNumber> bounds,in AnalyticSurface surface)
    {
        Components(bounds,surface,out var x,out var y,out var z);
        return surface.Kind==SurfaceClass.Plane?x*surface.Axis.X+y*surface.Axis.Y+z*surface.Axis.Z
            :(IntervalNumber.Square(x)+IntervalNumber.Square(y)+IntervalNumber.Square(z)-new IntervalNumber(surface.Radius)*surface.Radius)/(2*new IntervalNumber(surface.Radius));
    }

    internal static IntervalNumber Derivative(ReadOnlySpan<IntervalNumber> bounds,in AnalyticSurface surface,BufferOffset offset)
    {
        if(surface.Kind==SurfaceClass.Plane)return bounds[offset]*surface.Axis.X+bounds[offset+1]*surface.Axis.Y+bounds[offset+2]*surface.Axis.Z;
        Components(bounds,surface,out var x,out var y,out var z);
        var dx=bounds[offset];var dy=bounds[offset+1];var dz=bounds[offset+2];
        if(surface.Kind==SurfaceClass.Cylinder)
        {
            var height=dx*surface.Axis.X+dy*surface.Axis.Y+dz*surface.Axis.Z;
            dx-=height*surface.Axis.X;dy-=height*surface.Axis.Y;dz-=height*surface.Axis.Z;
        }
        return (x*dx+y*dy+z*dz)/surface.Radius;
    }

    private static void Components(ReadOnlySpan<IntervalNumber> bounds,in AnalyticSurface surface,out IntervalNumber x,out IntervalNumber y,out IntervalNumber z)
    {
        x=bounds[0]-surface.Origin.X;y=bounds[1]-surface.Origin.Y;z=bounds[2]-surface.Origin.Z;
        if(surface.Kind!=SurfaceClass.Cylinder)return;
        var height=x*surface.Axis.X+y*surface.Axis.Y+z*surface.Axis.Z;
        x-=height*surface.Axis.X;y-=height*surface.Axis.Y;z-=height*surface.Axis.Z;
    }
}

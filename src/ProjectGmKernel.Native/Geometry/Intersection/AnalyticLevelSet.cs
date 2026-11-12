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
        if(surface.Kind==SurfaceClass.Cylinder)RemoveAxial(surface.Axis,ref dx,ref dy,ref dz);
        return (x*dx+y*dy+z*dz)/surface.Radius;
    }

    private static void Components(ReadOnlySpan<IntervalNumber> bounds,in AnalyticSurface surface,out IntervalNumber x,out IntervalNumber y,out IntervalNumber z)
    {
        x=bounds[0]-surface.Origin.X;y=bounds[1]-surface.Origin.Y;z=bounds[2]-surface.Origin.Z;
        if(surface.Kind==SurfaceClass.Cylinder)RemoveAxial(surface.Axis,ref x,ref y,ref z);
    }

    internal static void RemoveAxial(in KernelVector3 axis,ref IntervalNumber x,ref IntervalNumber y,ref IntervalNumber z)
    {
        // Apply I-aaᵀ as one linear map. Forming delta-a*(a·delta)
        // separately counts the same coordinate twice in every interval
        // and can prevent exclusion on an oblique transported cylinder.
        var a=new IntervalNumber(axis.X);var b=new IntervalNumber(axis.Y);var c=new IntervalNumber(axis.Z);
        var ab=a*b;var ac=a*c;var bc=b*c;
        (x,y,z)=((1-a*a)*x-ab*y-ac*z,(1-b*b)*y-ab*x-bc*z,(1-c*c)*z-ac*x-bc*y);
    }
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Bounds;

/// <summary>Exact rational analytic charts over the bounded other operand.
/// The interval sqrt(1/2) encloses conic-weight rounding.</summary>
internal static class AnalyticBezierPatches
{
    internal static BufferCount Count(in AnalyticSurface surface) => surface.Kind switch
    { SurfaceClass.Plane => 1, SurfaceClass.Cylinder or SurfaceClass.Cone => 4,
        SurfaceClass.Sphere => 8, SurfaceClass.Torus when surface.Radius > surface.Secondary => 16, _ => 0 };

    internal static bool HasDomain(in AnalyticSurface surface, in IntervalBox3 bounds)
        => surface.Kind != SurfaceClass.Cone || (surface.Radius + surface.Secondary * Project(bounds, surface.Origin, surface.Axis)).Upper >= 0;

    internal static AlgorithmStatus Create(in AnalyticSurface surface, in IntervalBox3 bounds, BufferOffset index,
        Span<IntervalNumber> poles, out SplineDegree p, out SplineDegree q, out SurfaceParameterBox domain)
    {
        p = surface.Kind == SurfaceClass.Plane ? 1 : 2;
        q = surface.Kind is SurfaceClass.Sphere or SurfaceClass.Torus ? 2 : 1;
        domain = new(0, 1, 0, 1);
        if (index < 0 || index >= Count(surface) || poles.Length < 4 * (p + 1) * (q + 1)) return AlgorithmStatus.InvalidInput;
        var xBounds = Project(bounds, surface.Origin, surface.X); var yBounds = Project(bounds, surface.Origin, surface.Y);
        var zBounds = Project(bounds, surface.Origin, surface.Axis);
        var margin = Math.Max(1, Math.Max(xBounds.Upper - xBounds.Lower, Math.Max(yBounds.Upper - yBounds.Lower, zBounds.Upper - zBounds.Lower)) * .1);
        var uPart = surface.Kind == SurfaceClass.Plane ? 0 : index % 4;
        var vPart = surface.Kind == SurfaceClass.Plane ? 0 : index / 4;
        if (surface.Kind != SurfaceClass.Plane) domain = new(uPart, uPart + 1, vPart, vPart + 1);
        for (var u = 0; u <= p; u++) for (var v = 0; v <= q; v++)
        {
            IntervalNumber x, y, z, w;
            if (surface.Kind == SurfaceClass.Plane)
            {
                x = u == 0 ? xBounds.Lower - margin : xBounds.Upper + margin;
                y = v == 0 ? yBounds.Lower - margin : yBounds.Upper + margin; z = 0; w = 1;
            }
            else
            {
                CirclePole(uPart, u, out var cx, out var cy, out var cw);
                IntervalNumber radial, height, vw;
                if (surface.Kind is SurfaceClass.Sphere or SurfaceClass.Torus)
                {
                    CirclePole(surface.Kind == SurfaceClass.Sphere ? (vPart == 0 ? 3 : 0) : vPart, v, out var pr, out var pz, out vw);
                    radial = surface.Kind == SurfaceClass.Sphere ? surface.Radius * pr : surface.Radius * vw + surface.Secondary * pr;
                    height = (surface.Kind == SurfaceClass.Sphere ? surface.Radius : surface.Secondary) * pz;
                }
                else
                {
                    vw = 1; height = v == 0 ? zBounds.Lower - margin : zBounds.Upper + margin;
                    if (surface.Kind == SurfaceClass.Cone && surface.Secondary != 0)
                    {
                        var apex = -new IntervalNumber(surface.Radius) / surface.Secondary;
                        if (surface.Secondary > 0 && v == 0 && height.Lower < apex.Upper) height = apex;
                        if (surface.Secondary < 0 && v == 1 && height.Upper > apex.Lower) height = apex;
                    }
                    radial = surface.Kind == SurfaceClass.Cone ? surface.Radius + surface.Secondary * height : surface.Radius;
                }
                x = cx * radial; y = cy * radial; z = cw * height; w = cw * vw;
            }
            var at = 4 * (u * (q + 1) + v);
            poles[at] = surface.Origin.X * w + surface.X.X * x + surface.Y.X * y + surface.Axis.X * z;
            poles[at + 1] = surface.Origin.Y * w + surface.X.Y * x + surface.Y.Y * y + surface.Axis.Y * z;
            poles[at + 2] = surface.Origin.Z * w + surface.X.Z * x + surface.Y.Z * y + surface.Axis.Z * z;
            poles[at + 3] = w;
        }
        return AlgorithmStatus.Success;
    }

    internal static void CirclePole(BufferOffset quadrant, BufferOffset vertex, out IntervalNumber x, out IntervalNumber y, out IntervalNumber w)
    {
        var q = (quadrant % 4 + 4) % 4; var a = q == 0 ? 1 : q == 2 ? -1 : 0; var b = q == 1 ? 1 : q == 3 ? -1 : 0;
        if (vertex == 0) { x = a; y = b; w = 1; }
        else if (vertex == 2) { x = -b; y = a; w = 1; }
        else
        {
            var weight = Math.Sqrt(.5); w = new(Math.BitDecrement(weight), Math.BitIncrement(weight));
            x = (a - b) * w; y = (a + b) * w;
        }
    }

    private static IntervalNumber Project(in IntervalBox3 box, in KernelVector3 origin, in KernelVector3 direction)
        => (new IntervalNumber(box.XLo, box.XHi) - origin.X) * direction.X
            + (new IntervalNumber(box.YLo, box.YHi) - origin.Y) * direction.Y
            + (new IntervalNumber(box.ZLo, box.ZHi) - origin.Z) * direction.Z;
}

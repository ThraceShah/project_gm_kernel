using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int TryClassifyRotationalBody(BodySlot body, in KernelVector3 point, int* enclosure,
        EntityTag* topology, out bool handled)
    {
        handled = false; Span<EntityTag> input = stackalloc EntityTag[1] { Bodies[body].Header.Tag };
        Span<RotationalFace> buffer = stackalloc RotationalFace[RotationalFaceLimit];
        var error = ReadRotationalModel(input, buffer, out var count, out var frame);
        if (error == ParasolidConstants.PK_ERROR_not_implemented) return 0;
        handled = true; if (error != 0) return error;
        var faces = buffer[..count]; var delta = Sub(point, frame.Origin); var height = Dot(delta, frame.Axis);
        var radius = OperationIntersection.Norm(Sub(delta, Scale(frame.Axis, height)));
        if (!double.IsFinite(radius) || !double.IsFinite(height)) return ParasolidConstants.PK_ERROR_bad_parameter;
        var boundary = LocateRotationalBoundary(faces, 0, radius, height, out _);
        if (boundary != 0) { *enclosure = ParasolidConstants.PK_enclosure_on_c; *topology = boundary; return 0; }
        // At an internal axial subdivision either side has the same material
        // membership unless the point was on a face, already handled above.
        var near = false; var clusterHigh = height;
        foreach (ref readonly var f in faces) if (f.Kind == 1)
        {
            if (Math.Abs(height - f.Low) <= RotationalTolerance) { clusterHigh = Math.Max(clusterHigh, f.Low); near = true; }
            if (Math.Abs(height - f.High) <= RotationalTolerance) { clusterHigh = Math.Max(clusterHigh, f.High); near = true; }
        }
        if (near)
        {
            var next = double.PositiveInfinity;
            foreach (ref readonly var f in faces) if (f.Kind == 1)
            {
                if (f.Low > clusterHigh + RotationalTolerance) next = Math.Min(next, f.Low);
                if (f.High > clusterHigh + RotationalTolerance) next = Math.Min(next, f.High);
            }
            height = double.IsFinite(next) ? clusterHigh + (next - clusterHigh) * .5 : clusterHigh + 1;
        }
        var winding = RotationalWinding(faces, 0, radius, height);
        if (winding is not (0 or 1)) return ParasolidConstants.PK_ERROR_bad_loop;
        var inside = winding == 1; *enclosure = inside ? ParasolidConstants.PK_enclosure_inside_c : ParasolidConstants.PK_enclosure_outside_c;
        var region = Bodies[body].FirstRegion;
        for (var i = 0; i < Bodies[body].RegionCount; i++, region = Regions[region].NextInBody)
        {
            if ((Regions[region].IsSolid != 0) != inside) continue;
            if (*topology == 0) *topology = Regions[region].Header.Tag;
            if (inside) return 0;
            if (RotationalWinding(faces, 0, radius, height, region) < 0) { *topology = Regions[region].Header.Tag; return 0; }
        }
        return *topology != 0 ? 0 : ParasolidConstants.PK_ERROR_bad_loop;
    }
}

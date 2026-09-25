using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Per-thread entry counts for tests. Disarmed threads do not record calls,
/// so parallel tests cannot see each other's totals.
/// </summary>
internal static class SurfaceEvaluationCalls
{
    [ThreadStatic] private static bool armed;
    [ThreadStatic] private static int support0;
    [ThreadStatic] private static int support1;
    [ThreadStatic] private static int failOn; // 1 = next matching support0, 2 = support1
    [ThreadStatic] private static AnalyticSurface tagged0;
    [ThreadStatic] private static AnalyticSurface tagged1;

    internal static int Support0 => support0;
    internal static int Support1 => support1;

    internal static void Arm(in AnalyticSurface first, in AnalyticSurface second, int failSupport = 0)
    {
        tagged0 = first;
        tagged1 = second;
        support0 = 0;
        support1 = 0;
        failOn = failSupport;
        armed = true;
    }

    internal static void Disarm()
    {
        armed = false;
        failOn = 0;
    }

    internal static bool Note(in AnalyticSurface surface)
    {
        if (!armed) return false;
        if (Same(in surface, in tagged0))
        {
            support0++;
            return failOn == 1;
        }
        if (Same(in surface, in tagged1))
        {
            support1++;
            return failOn == 2;
        }
        return false;
    }

    private static bool Same(in AnalyticSurface a, in AnalyticSurface b)
        => a.Kind == b.Kind
            && a.Origin.X == b.Origin.X && a.Origin.Y == b.Origin.Y && a.Origin.Z == b.Origin.Z
            && a.Axis.X == b.Axis.X && a.Axis.Y == b.Axis.Y && a.Axis.Z == b.Axis.Z
            && a.Radius == b.Radius && a.Secondary == b.Secondary;
}

internal static class SurfaceEvaluation
{
    internal static AlgorithmStatus Evaluate(in AnalyticSurface surface, double u, double v,
        in SurfaceDerivativeLayout layout, Span<KernelVector3> output)
    {
        if (SurfaceEvaluationCalls.Note(in surface)) return AlgorithmStatus.NotConverged;
        if (!double.IsFinite(u) || !double.IsFinite(v)) return AlgorithmStatus.InvalidInput;
        if (output.Length < layout.Count) return AlgorithmStatus.OutputTooSmall;
        if (surface.Kind is not (SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Cone
            or SurfaceClass.Sphere or SurfaceClass.Torus)) return AlgorithmStatus.Unsupported;
        if (surface.Kind == SurfaceClass.Sphere && Math.Abs(v) > Math.PI / 2)
            return AlgorithmStatus.InvalidInput;
        if (surface.Kind == SurfaceClass.Cone && surface.Radius + v * surface.Secondary < 0)
            return AlgorithmStatus.InvalidInput;
        if (surface.Kind == SurfaceClass.Torus && surface.Radius <= surface.Secondary)
        {
            var bound = Math.Acos(-surface.Radius / surface.Secondary);
            if (Math.Abs(v) > bound) return AlgorithmStatus.InvalidInput;
        }

        var (su, cu) = Math.SinCos(u);
        var (sv, cv) = Math.SinCos(v);
        for (DerivativeOrder i = 0; i <= layout.UOrder; i++)
        {
            var (s, c) = Differentiate(su, cu, i);
            var radial = Add(Scale(surface.X, c), Scale(surface.Y, s));
            for (DerivativeOrder j = 0; j <= layout.VOrder; j++)
            {
                KernelVector3 value = default;
                switch (surface.Kind)
                {
                    case SurfaceClass.Plane:
                        if (i == 0 && j == 0) value = Add(Scale(surface.X, u), Scale(surface.Y, v));
                        else if (i == 1 && j == 0) value = surface.X;
                        else if (i == 0 && j == 1) value = surface.Y;
                        break;
                    case SurfaceClass.Cylinder:
                    case SurfaceClass.Cone:
                        var slope = surface.Kind == SurfaceClass.Cone ? surface.Secondary : 0;
                        if (j == 0) value = Scale(radial, surface.Radius + v * slope);
                        else if (j == 1) value = Scale(radial, slope);
                        if (i == 0 && j == 0) value = Add(value, Scale(surface.Axis, v));
                        else if (i == 0 && j == 1) value = Add(value, surface.Axis);
                        break;
                    case SurfaceClass.Sphere:
                    case SurfaceClass.Torus:
                        var minor = surface.Kind == SurfaceClass.Sphere ? surface.Radius : surface.Secondary;
                        var (ds, dc) = Differentiate(sv, cv, j);
                        var radius = minor * dc;
                        if (surface.Kind == SurfaceClass.Torus && j == 0) radius += surface.Radius;
                        value = Scale(radial, radius);
                        if (i == 0) value = Add(value, Scale(surface.Axis, minor * ds));
                        break;
                }
                if (i == 0 && j == 0) value = Add(value, surface.Origin);
                if (!IsFinite(value)) return AlgorithmStatus.NumericalFailure;
                output[layout.GetIndex(i, j)] = value;
            }
        }
        return AlgorithmStatus.Success;
    }
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static class SurfaceEvaluation
{
    internal static AlgorithmStatus Evaluate(in AnalyticSurface surface, double u, double v,
        in SurfaceDerivativeLayout layout, Span<KernelVector3> output)
    {
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

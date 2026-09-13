using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Direct parameter witness recovery for analytic surfaces (spec §4.2
/// RecoverParameterWitness, task T07): the closed-form inverse of the
/// parametrization used by <see cref="SurfaceEvaluation"/>. Witnesses seed the
/// P4/P2 plans and provide UV correspondence evidence; poles, the cylinder
/// axis and off-domain points return failure instead of a guessed angle.
/// </summary>
internal static class AnalyticParametricEvaluation
{
    /// <summary>
    /// Recover (u, v) whose surface evaluation reproduces
    /// <paramref name="point"/>. Angular parameters are returned in the
    /// principal branch; callers unwrap against their own anchors (§13.5).
    /// </summary>
    internal static AlgorithmStatus TryRecoverWitness(in AnalyticSurface surface, in KernelVector3 point,
        out double u, out double v)
    {
        u = v = 0;
        if (!IsFinite(point)) return AlgorithmStatus.InvalidInput;
        var r = Sub(point, surface.Origin);
        switch (surface.Kind)
        {
            case SurfaceClass.Plane:
                u = Dot(surface.X, r);
                v = Dot(surface.Y, r);
                return double.IsFinite(u) && double.IsFinite(v) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
            case SurfaceClass.Cylinder:
            case SurfaceClass.Cone:
            {
                // Off-surface points project to the nearest-sheet parameters;
                // validity checks belong to the caller's own guards.
                var axial = Dot(surface.Axis, r);
                var radial = Sub(r, Scale(surface.Axis, axial));
                if (!AngleOf(radial, surface, out u)) return AlgorithmStatus.Singular;
                v = axial;
                return AlgorithmStatus.Success;
            }
            case SurfaceClass.Sphere:
            {
                var norm = Math.Sqrt(Dot(r, r));
                if (!(norm > 0)) return AlgorithmStatus.Singular;
                var sinV = Dot(surface.Axis, r) / surface.Radius;
                if (sinV < -1 || sinV > 1) return AlgorithmStatus.InvalidInput;
                v = Math.Asin(sinV);
                var radial = Sub(r, Scale(surface.Axis, Dot(surface.Axis, r)));
                return AngleOf(radial, surface, out u) ? AlgorithmStatus.Success : AlgorithmStatus.Singular;
            }
            case SurfaceClass.Torus:
            {
                var axial = Dot(surface.Axis, r);
                var radial = Sub(r, Scale(surface.Axis, axial));
                var rho = Math.Sqrt(Dot(radial, radial));
                v = Math.Atan2(axial, rho - surface.Radius);
                return AngleOf(radial, surface, out u) ? AlgorithmStatus.Success : AlgorithmStatus.Singular;
            }
            default:
                return AlgorithmStatus.Unsupported;
        }
    }

    private static bool AngleOf(in KernelVector3 radial, in AnalyticSurface surface, out double u)
    {
        u = 0;
        var x = Dot(radial, surface.X);
        var y = Dot(radial, surface.Y);
        if (!(x * x + y * y > 0)) return false; // on the axis/pole: no unique angle
        u = Math.Atan2(y, x);
        return double.IsFinite(u);
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}

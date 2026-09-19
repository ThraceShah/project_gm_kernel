using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Zero-set jet of an analytic implicit: value, gradient and Hessian. This is
/// a statement about the zero set only — it is never a signed distance and
/// must not feed the BLEND_BOUND distance composition (spec §8.2, task T03).
/// </summary>
internal readonly struct ImplicitJet
{
    internal readonly double Value;
    internal readonly KernelVector3 Gradient;
    internal readonly double Hxx, Hxy, Hxz, Hyy, Hyz, Hzz;

    internal ImplicitJet(double value, KernelVector3 gradient,
        double hxx, double hxy, double hxz, double hyy, double hyz, double hzz)
    {
        Value = value;
        Gradient = gradient;
        Hxx = hxx; Hxy = hxy; Hxz = hxz;
        Hyy = hyy; Hyz = hyz; Hzz = hzz;
    }
}

/// <summary>
/// Analytic zero-set jets with sheet guards (spec §8.1, DERIVED formulas).
/// Cone signs follow the kernel's PK-normalized convention ρ(z) = R + k·z —
/// the XT/PK axis flip is normalized exactly once at import (spec §8.3);
/// this module never flips again.
/// </summary>
internal static class AnalyticImplicitEvaluation
{
    internal const int MaxImplicitOrder = 2;

    /// <summary>
    /// Evaluate φ, ∇φ (order ≥ 1) and H (order 2) for the analytic zero set.
    /// Guards reject points whose near-zero residual comes from a branch the
    /// analytic surface does not contain: the far cone nappe (R + k·z &lt; 0)
    /// and, for non-ring tori, points off the selected profile sheet.
    /// </summary>
    internal static AlgorithmStatus Evaluate(in AnalyticSurface surface, in KernelVector3 point,
        DerivativeOrder order, out ImplicitJet jet)
    {
        jet = default;
        if (order < 0 || order > MaxImplicitOrder) return AlgorithmStatus.InvalidInput;
        if (!IsFinite(point)) return AlgorithmStatus.InvalidInput;

        var r = Sub(point, surface.Origin);
        switch (surface.Kind)
        {
            case SurfaceClass.Plane:
                jet = Plane(in surface, in r, order);
                return AlgorithmStatus.Success;
            case SurfaceClass.Sphere:
                jet = Sphere(in surface, in r, order);
                return AlgorithmStatus.Success;
            case SurfaceClass.Cylinder:
                jet = Cylinder(in surface, in r, order);
                return AlgorithmStatus.Success;
            case SurfaceClass.Cone:
                return Cone(in surface, in r, order, out jet);
            case SurfaceClass.Torus:
                return Torus(in surface, in r, order, out jet);
            default:
                return AlgorithmStatus.Unsupported;
        }
    }

    /// <summary>
    /// Translation-invariant geometric distance to the analytic support.
    /// Unlike the algebraic implicit value, every result has length units.
    /// </summary>
    internal static AlgorithmStatus GeometricDeviation(in AnalyticSurface surface,
        in KernelVector3 point, out double deviation)
    {
        deviation = double.PositiveInfinity;
        if (!IsFinite(point)) return AlgorithmStatus.InvalidInput;
        var r = Sub(point, surface.Origin);
        var axial = Dot(surface.Axis, r);
        var radial = Sub(r, Scale(surface.Axis, axial));
        switch (surface.Kind)
        {
            case SurfaceClass.Plane:
                deviation = Math.Abs(axial);
                break;
            case SurfaceClass.Sphere:
                deviation = Math.Abs(Math.Sqrt(Dot(r, r)) - surface.Radius);
                break;
            case SurfaceClass.Cylinder:
                deviation = Math.Abs(Math.Sqrt(Dot(radial, radial)) - surface.Radius);
                break;
            case SurfaceClass.Cone:
                var generator = surface.Radius + surface.Secondary * axial;
                if (generator < 0) return AlgorithmStatus.Unsupported;
                deviation = Math.Abs(Math.Sqrt(Dot(radial, radial)) - generator);
                break;
            case SurfaceClass.Torus:
                var profileRadius = Math.Sqrt(
                    Math.Pow(Math.Sqrt(Dot(radial, radial)) - surface.Radius, 2) + axial * axial);
                deviation = Math.Abs(profileRadius - surface.Secondary);
                break;
            default:
                return AlgorithmStatus.Unsupported;
        }
        return double.IsFinite(deviation) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    private static ImplicitJet Plane(in AnalyticSurface surface, in KernelVector3 r, DerivativeOrder order)
        => new(Dot(surface.Axis, r), order >= 1 ? surface.Axis : default, 0, 0, 0, 0, 0, 0);

    private static ImplicitJet Sphere(in AnalyticSurface surface, in KernelVector3 r, DerivativeOrder order)
    {
        // φ = r·r − R²: ∇φ = 2r, H = 2I. Exact on both sheets; sense lives
        // with the caller.
        var value = Dot(r, r) - surface.Radius * surface.Radius;
        return order switch
        {
            0 => new(value, default, 0, 0, 0, 0, 0, 0),
            1 => new(value, Scale(r, 2), 2, 0, 0, 2, 0, 2),
            _ => new(value, Scale(r, 2), 2, 0, 0, 2, 0, 2),
        };
    }

    private static ImplicitJet Cylinder(in AnalyticSurface surface, in KernelVector3 r, DerivativeOrder order)
    {
        // φ = ρ² − R² with ρ² = r⊥·r⊥, r⊥ = r − (A·r)A.
        var axial = Dot(surface.Axis, r);
        var radial = Sub(r, Scale(surface.Axis, axial));
        var value = Dot(radial, radial) - surface.Radius * surface.Radius;
        if (order == 0) return new(value, default, 0, 0, 0, 0, 0, 0);
        var gradient = Scale(radial, 2);
        if (order == 1) return new(value, gradient, 0, 0, 0, 0, 0, 0);
        // H = 2(I − AAᵀ).
        var ax = surface.Axis.X; var ay = surface.Axis.Y; var az = surface.Axis.Z;
        return new(value, gradient,
            2 * (1 - ax * ax), -2 * ax * ay, -2 * ax * az,
            2 * (1 - ay * ay), -2 * ay * az,
            2 * (1 - az * az));
    }

    private static AlgorithmStatus Cone(in AnalyticSurface surface, in KernelVector3 r,
        DerivativeOrder order, out ImplicitJet jet)
    {
        // φ = ρ² − (R + k·z)²; the analytic zero set has two nappes and the
        // valid half-cone is R + k·z ≥ 0 (kernel convention). A near-zero
        // residual on the far nappe is rejected instead of accepted (spec
        // §21.1 "双锥错误半部").
        jet = default;
        var axial = Dot(surface.Axis, r);
        var radial = Sub(r, Scale(surface.Axis, axial));
        var rho = Math.Sqrt(Dot(radial, radial));
        var generator = surface.Radius + surface.Secondary * axial;
        if (generator < 0 && Math.Abs(rho * rho - generator * generator) <= 1e-9 * Math.Max(1, rho * rho))
            return AlgorithmStatus.Unsupported; // wrong-nappe candidate
        var value = rho * rho - generator * generator;
        if (order == 0)
        {
            jet = new(value, default, 0, 0, 0, 0, 0, 0);
            return AlgorithmStatus.Success;
        }
        var gradient = Sub(Scale(radial, 2), Scale(surface.Axis, 2 * surface.Secondary * generator));
        if (order == 1)
        {
            jet = new(value, gradient, 0, 0, 0, 0, 0, 0);
            return AlgorithmStatus.Success;
        }
        // H = 2(P − k²AAᵀ).
        var ax = surface.Axis.X; var ay = surface.Axis.Y; var az = surface.Axis.Z;
        var k = surface.Secondary;
        jet = new(value, gradient,
            2 * (1 - ax * ax * (1 + k * k)), -2 * ax * ay * (1 + k * k), -2 * ax * az * (1 + k * k),
            2 * (1 - ay * ay * (1 + k * k)), -2 * ay * az * (1 + k * k),
            2 * (1 - az * az * (1 + k * k)));
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus Torus(in AnalyticSurface surface, in KernelVector3 r,
        DerivativeOrder order, out ImplicitJet jet)
    {
        // φ = (q·q + a² − b²)² − 4a²ρ², factoring as
        //   [(ρ−a)² + z² − b²]·[(ρ+a)² + z² − b²].
        // The second factor is the mirror sheet, part of the algebraic zero
        // set only for non-ring tori (a ≤ b). A near-zero residual there is a
        // rejected candidate, never an accepted point (spec §8.1, §21.1).
        jet = default;
        var a = surface.Radius;
        var b = surface.Secondary;
        var axial = Dot(surface.Axis, r);
        var radial = Sub(r, Scale(surface.Axis, axial));
        var rho = Math.Sqrt(Dot(radial, radial));
        var qSq = Dot(r, r);
        var value = (qSq + a * a - b * b) * (qSq + a * a - b * b) - 4 * a * a * rho * rho;

        // Sheet guard: the actual surface satisfies the profile-circle relation
        // (ρ−a)² + z² = b². Accept near-zero φ only when the point also lies
        // on the selected profile sheet (XT25 apple/lemon/doughnut).
        var inner = (rho - a) * (rho - a) + axial * axial - b * b;
        var outer = (rho + a) * (rho + a) + axial * axial - b * b;
        var scale = Math.Max(1.0, qSq + a * a);
        var onSelectedSheet = Math.Abs(inner) <= 1e-9 * scale * b;
        var onMirrorSheet = a <= b && Math.Abs(outer) <= 1e-9 * scale * b;
        if (Math.Abs(value) <= 1e-9 * scale * scale * b * b && !onSelectedSheet)
            return onMirrorSheet ? AlgorithmStatus.Unsupported : AlgorithmStatus.InvalidInput;

        if (order == 0)
        {
            jet = new(value, default, 0, 0, 0, 0, 0, 0);
            return AlgorithmStatus.Success;
        }
        var common = qSq + a * a - b * b;
        var gradient = Sub(Scale(r, 4 * common), Scale(radial, 8 * a * a));
        if (order == 1)
        {
            jet = new(value, gradient, 0, 0, 0, 0, 0, 0);
            return AlgorithmStatus.Success;
        }
        // H = 8 r rᵀ + 4(common)·I − 8a²(P), P = I − AAᵀ.
        var hxx = 8 * r.X * r.X + 4 * common - 8 * a * a * (1 - surface.Axis.X * surface.Axis.X);
        var hyy = 8 * r.Y * r.Y + 4 * common - 8 * a * a * (1 - surface.Axis.Y * surface.Axis.Y);
        var hzz = 8 * r.Z * r.Z + 4 * common - 8 * a * a * (1 - surface.Axis.Z * surface.Axis.Z);
        var hxy = 8 * r.X * r.Y + 8 * a * a * surface.Axis.X * surface.Axis.Y;
        var hxz = 8 * r.X * r.Z + 8 * a * a * surface.Axis.X * surface.Axis.Z;
        var hyz = 8 * r.Y * r.Z + 8 * a * a * surface.Axis.Y * surface.Axis.Z;
        jet = new(value, gradient, hxx, hxy, hxz, hyy, hyz, hzz);
        return AlgorithmStatus.Success;
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Local oriented-distance quality: the distance is exact on the surface and,
/// for plane/sphere/cylinder, in a full regular neighborhood; the cone jet is
/// exact only on the surface and degrades to a first-order estimate away from
/// it. Reported per jet so a valid D0 never advertises a D2 it lacks
/// (spec §8.2, §9.2).
/// </summary>
internal enum DistanceGrade : byte
{
    Exact = 0,
    FirstOrderEstimate = 1,
}

/// <summary>
/// Oriented distance jet: value, unit normal, Hessian, foot-point witness and
/// the grade of the current evaluation. Deliberately a different type from
/// <see cref="ImplicitJet"/> — a zero-set jet never masquerades as a distance
/// (spec §8.2).
/// </summary>
internal readonly struct OrientedDistanceJet
{
    internal readonly double Distance;
    internal readonly KernelVector3 Gradient;
    internal readonly double Hxx, Hxy, Hxz, Hyy, Hyz, Hzz;
    internal readonly KernelVector3 FootPoint;
    internal readonly DistanceGrade Grade;

    internal OrientedDistanceJet(double distance, in KernelVector3 gradient,
        double hxx, double hxy, double hxz, double hyy, double hyz, double hzz,
        in KernelVector3 footPoint, DistanceGrade grade)
    {
        Distance = distance;
        Gradient = gradient;
        Hxx = hxx; Hxy = hxy; Hxz = hxz;
        Hyy = hyy; Hyz = hyz; Hzz = hzz;
        FootPoint = footPoint;
        Grade = grade;
    }
}

/// <summary>
/// Analytic local oriented distances aligned with the source surface sense
/// (spec §8.1). Valid only on the near side of the regular region around the
/// selected sheet; centers/axes (no unique normal) return
/// <see cref="AlgorithmStatus.Singular"/>. Tori and cones away from the foot
/// region are not offered here in this slice.
/// </summary>
internal static class SurfaceDistanceEvaluation
{
    internal const int MaxDistanceOrder = 2;

    /// <summary>
    /// Evaluate the signed distance jet. <paramref name="sense"/> is the source
    /// surface sense (+1/−1); the sign multiplies distance, normal and Hessian
    /// together so the jet stays consistent (spec §8.1, §8.3).
    /// </summary>
    internal static AlgorithmStatus Evaluate(in AnalyticSurface surface, in KernelVector3 point,
        KernelSense sense, DerivativeOrder order, out OrientedDistanceJet jet)
    {
        jet = default;
        if (order < 0 || order > MaxDistanceOrder) return AlgorithmStatus.InvalidInput;
        if (!IsFinite(point)) return AlgorithmStatus.InvalidInput;
        if (sense != 1 && sense != -1) return AlgorithmStatus.InvalidInput;

        var r = Sub(point, surface.Origin);
        switch (surface.Kind)
        {
            case SurfaceClass.Plane:
                jet = Plane(in surface, in r, sense, order);
                return AlgorithmStatus.Success;
            case SurfaceClass.Sphere:
                return Sphere(in surface, in r, sense, order, out jet);
            case SurfaceClass.Cylinder:
                return Cylinder(in surface, in r, sense, order, out jet);
            case SurfaceClass.Cone:
                return Cone(in surface, in r, sense, order, out jet);
            default:
                // Ring-torus and general surface distances arrive with T11.
                return AlgorithmStatus.Unsupported;
        }
    }

    private static OrientedDistanceJet Plane(in AnalyticSurface surface, in KernelVector3 r,
        KernelSense sense, DerivativeOrder order)
    {
        // d = s·(n·r); exact everywhere, zero Hessian.
        var axial = Dot(surface.Axis, r);
        var distance = sense * axial;
        var gradient = Scale(surface.Axis, sense);
        var foot = Sub(r, Scale(surface.Axis, axial));
        return new(distance, order >= 1 ? gradient : default, 0, 0, 0, 0, 0, 0,
            Add(surface.Origin, foot), DistanceGrade.Exact);
    }

    private static AlgorithmStatus Sphere(in AnalyticSurface surface, in KernelVector3 r,
        KernelSense sense, DerivativeOrder order, out OrientedDistanceJet jet)
    {
        jet = default;
        var norm = Math.Sqrt(Dot(r, r));
        if (norm <= 0) return AlgorithmStatus.Singular; // no unique normal at the center
        var distance = sense * (norm - surface.Radius);
        var normal = Scale(r, 1 / norm);
        if (order == 0)
        {
            jet = new(distance, default, 0, 0, 0, 0, 0, 0,
                Add(surface.Origin, Scale(normal, surface.Radius)), DistanceGrade.Exact);
            return AlgorithmStatus.Success;
        }
        var gradient = Scale(normal, sense);
        // H = s·(I − nnᵀ)/‖r‖.
        var scale = sense / norm;
        var hxx = scale * (1 - normal.X * normal.X);
        var hxy = -scale * normal.X * normal.Y;
        var hxz = -scale * normal.X * normal.Z;
        var hyy = scale * (1 - normal.Y * normal.Y);
        var hyz = -scale * normal.Y * normal.Z;
        var hzz = scale * (1 - normal.Z * normal.Z);
        jet = new(distance, gradient, hxx, hxy, hxz, hyy, hyz, hzz,
            Add(surface.Origin, Scale(normal, surface.Radius)), DistanceGrade.Exact);
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus Cylinder(in AnalyticSurface surface, in KernelVector3 r,
        KernelSense sense, DerivativeOrder order, out OrientedDistanceJet jet)
    {
        jet = default;
        var axial = Dot(surface.Axis, r);
        var radial = Sub(r, Scale(surface.Axis, axial));
        var rho = Math.Sqrt(Dot(radial, radial));
        if (rho <= 0) return AlgorithmStatus.Singular; // on the axis: no unique normal
        var normal = Scale(radial, 1 / rho);
        var distance = sense * (rho - surface.Radius);
        // Foot point: same axial coordinate, radius placed on the cylinder.
        var foot = Add(Scale(normal, surface.Radius), Scale(surface.Axis, axial));
        if (order == 0)
        {
            jet = new(distance, default, 0, 0, 0, 0, 0, 0,
                Add(surface.Origin, foot), DistanceGrade.Exact);
            return AlgorithmStatus.Success;
        }
        var gradient = Scale(normal, sense);
        // H = s·(P − nnᵀ)/ρ with P = I − AAᵀ.
        var ax = surface.Axis.X; var ay = surface.Axis.Y; var az = surface.Axis.Z;
        var nx = normal.X; var ny = normal.Y; var nz = normal.Z;
        var scale = sense / rho;
        var pxx = 1 - ax * ax; var pxy = -ax * ay; var pxz = -ax * az;
        var pyy = 1 - ay * ay; var pyz = -ay * az;
        var pzz = 1 - az * az;
        jet = new(distance, gradient,
            scale * (pxx - nx * nx), scale * (pxy - nx * ny), scale * (pxz - nx * nz),
            scale * (pyy - ny * ny), scale * (pyz - ny * nz),
            scale * (pzz - nz * nz),
            Add(surface.Origin, foot), DistanceGrade.Exact);
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus Cone(in AnalyticSurface surface, in KernelVector3 r,
        KernelSense sense, DerivativeOrder order, out OrientedDistanceJet jet)
    {
        // d = s·(ρ − (R + k·z))/√(1+k²): exact on the surface and in the
        // regular region of the valid half-cone; away from it the jet degrades
        // to a first-order estimate, which the grade field reports honestly.
        jet = default;
        var axial = Dot(surface.Axis, r);
        var radial = Sub(r, Scale(surface.Axis, axial));
        var rho = Math.Sqrt(Dot(radial, radial));
        if (rho <= 0) return AlgorithmStatus.Singular;
        var k = surface.Secondary;
        var generator = surface.Radius + k * axial;
        if (generator < 0) return AlgorithmStatus.Unsupported; // far nappe not offered
        var slope = Math.Sqrt(1 + k * k);
        var distance = sense * (rho - generator) / slope;
        if (order == 0)
        {
            jet = new(distance, default, 0, 0, 0, 0, 0, 0, default, DistanceGrade.FirstOrderEstimate);
            return AlgorithmStatus.Success;
        }
        var meridian = Scale(radial, 1 / rho);
        var gradient = Add(Scale(meridian, sense / slope), Scale(surface.Axis, -sense * k / slope));

        // Validate the foot-point witness against the cone definition; the
        // gradient is exact, but the linear foot is exact only on the surface.
        var foot = Sub(r, Scale(gradient, distance));
        var footAxial = Dot(surface.Axis, foot);
        var footRadial = Sub(foot, Scale(surface.Axis, footAxial));
        var footRho = Math.Sqrt(Dot(footRadial, footRadial));
        var witnessScale = Math.Max(1.0, rho);
        var grade = Math.Abs(footRho - (surface.Radius + k * footAxial)) <= 1e-9 * witnessScale
            ? DistanceGrade.Exact
            : DistanceGrade.FirstOrderEstimate;

        if (order == 1)
        {
            jet = new(distance, gradient, 0, 0, 0, 0, 0, 0, Add(surface.Origin, foot), grade);
            return AlgorithmStatus.Success;
        }
        // H = s·(P − nnᵀ)/(ρ√(1+k²)) for the local d = (ρ − R − k·z)/√(1+k²).
        var nx = meridian.X; var ny = meridian.Y; var nz = meridian.Z;
        var ax = surface.Axis.X; var ay = surface.Axis.Y; var az = surface.Axis.Z;
        var hScale = sense / (rho * slope);
        var pxx = 1 - ax * ax; var pxy = -ax * ay; var pxz = -ax * az;
        var pyy = 1 - ay * ay; var pyz = -ay * az;
        var pzz = 1 - az * az;
        jet = new(distance, gradient,
            hScale * (pxx - nx * nx), hScale * (pxy - nx * ny), hScale * (pxz - nx * nz),
            hScale * (pyy - ny * ny), hScale * (pyz - ny * nz),
            hScale * (pzz - nz * nz),
            Add(surface.Origin, foot), grade);
        return AlgorithmStatus.Success;
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static KernelVector3 Add(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
}

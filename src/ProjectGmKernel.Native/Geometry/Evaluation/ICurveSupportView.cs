using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Borrowed support for a regular intersection. B-surface poles, knots and
/// evaluation workspace stay in command scratch; only numeric UV witnesses
/// may enter the sample cache. A B-surface has no implicit analytic equation.
/// </summary>
internal readonly ref struct ICurveSupportView
{
    internal readonly AnalyticSurface Analytic;
    internal readonly BSurfaceView BSpline;
    internal readonly Span<double> Workspace;
    internal readonly bool IsBSurface;
    internal readonly double LengthScale;

    internal ICurveSupportView(scoped in AnalyticSurface analytic)
    {
        Analytic = analytic;
        BSpline = default;
        Workspace = default;
        IsBSurface = false;
        LengthScale = Math.Max(1.0, Math.Max(Math.Abs(analytic.Radius), Math.Abs(analytic.Secondary)));
    }

    internal ICurveSupportView(BSurfaceView bspline, Span<double> workspace)
    {
        Analytic = default;
        BSpline = bspline;
        Workspace = workspace;
        IsBSurface = true;
        LengthScale = ControlNetScale(in bspline);
    }

    private static double ControlNetScale(in BSurfaceView surface)
    {
        var dimension = surface.Dimension;
        if (dimension != (surface.Rational ? 4 : 3) || surface.Vertices.Length < dimension)
            return double.NaN;
        double xMin = double.PositiveInfinity, yMin = xMin, zMin = xMin;
        double xMax = double.NegativeInfinity, yMax = xMax, zMax = xMax;
        for (BufferOffset i = 0; i <= surface.Vertices.Length - dimension; i += dimension)
        {
            var weight = surface.Rational ? surface.Vertices[i + 3] : 1.0;
            if (!(weight > 0) || !double.IsFinite(weight)) return double.NaN;
            var x = surface.Vertices[i] / weight;
            var y = surface.Vertices[i + 1] / weight;
            var z = surface.Vertices[i + 2] / weight;
            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)) return double.NaN;
            xMin = Math.Min(xMin, x); xMax = Math.Max(xMax, x);
            yMin = Math.Min(yMin, y); yMax = Math.Max(yMax, y);
            zMin = Math.Min(zMin, z); zMax = Math.Max(zMax, z);
        }
        // Translation invariant, in physical units, and without squared-length overflow.
        var span = Vector(xMax - xMin, yMax - yMin, zMax - zMin);
        var maximum = Math.Max(span.X, Math.Max(span.Y, span.Z));
        if (!double.IsFinite(maximum)) return double.NaN;
        if (!(maximum > 0)) return 1;
        var scaled = Vector(span.X / maximum, span.Y / maximum, span.Z / maximum);
        return Math.Max(1.0, maximum * Math.Sqrt(Dot(scaled, scaled)));
    }
}

/// <summary>Capability dispatch shared by preparation and the parametric solver.</summary>
internal static class ICurveSupportEvaluation
{
    internal static bool IsInDomain(in ICurveSupportView surface, double u, double v)
    {
        if (!double.IsFinite(u) || !double.IsFinite(v)) return false;
        if (!surface.IsBSurface) return true;
        ref readonly var spline = ref surface.BSpline;
        return spline.UDegree >= 1 && spline.VDegree >= 1
            && spline.UVertexCount > spline.UDegree && spline.VVertexCount > spline.VDegree
            && spline.UKnots.Length == (long)spline.UVertexCount + spline.UDegree + 1
            && spline.VKnots.Length == (long)spline.VVertexCount + spline.VDegree + 1
            && spline.Vertices.Length == (long)spline.UVertexCount * spline.VVertexCount * spline.Dimension
            && spline.UEnd > spline.UStart && spline.VEnd > spline.VStart
            && BSurfaceEvaluation.IsInDomain(in spline, u, v);
    }

    internal static AlgorithmStatus Evaluate(in ICurveSupportView surface, double u, double v,
        in SurfaceDerivativeLayout layout, Span<KernelVector3> output)
    {
        if (!IsInDomain(in surface, u, v)) return AlgorithmStatus.InvalidInput;
        if (!surface.IsBSurface)
            return SurfaceEvaluation.Evaluate(in surface.Analytic, u, v, in layout, output);

        // The regular solver requires a unique jet. At an interior knot whose
        // guaranteed continuity is too low, do not silently pick a knot side.
        // Non-periodic domain ends have an unambiguous in-domain one-sided jet.
        if (!HasContinuity(surface.BSpline.UKnots, surface.BSpline.UDegree,
                surface.BSpline.UVertexCount, surface.BSpline.UPeriodic, u, layout.UOrder)
            || !HasContinuity(surface.BSpline.VKnots, surface.BSpline.VDegree,
                surface.BSpline.VVertexCount, surface.BSpline.VPeriodic, v, layout.VOrder))
            return AlgorithmStatus.Unsupported;
        return BSurfaceEvaluation.EvaluateInDomain(in surface.BSpline, u, v, in layout, surface.Workspace, output);
    }

    internal static bool TryResolveUv(in ICurveSupportView surface, in KernelVector3 point,
        bool hasWitness, double witnessU, double witnessV, out double u, out double v)
    {
        u = v = 0;
        var finiteWitness = hasWitness && IsInDomain(in surface, witnessU, witnessV);
        if (surface.IsBSurface)
        {
            if (!finiteWitness) return false;
            u = witnessU; v = witnessV;
            return true;
        }
        if (AnalyticParametricEvaluation.TryRecoverWitness(in surface.Analytic, in point, out u, out v)
            == AlgorithmStatus.Success)
        {
            if (finiteWitness) UnwrapPeriodic(in surface, ref u, ref v, witnessU, witnessV);
            return double.IsFinite(u) && double.IsFinite(v);
        }
        if (!finiteWitness) return false;
        u = witnessU; v = witnessV;
        return true;
    }

    internal static void UnwrapPeriodic(in ICurveSupportView surface, ref double u, ref double v,
        double referenceU, double referenceV)
    {
        if (surface.IsBSurface)
        {
            if (surface.BSpline.UPeriodic)
                u = Lift(u, referenceU, surface.BSpline.UEnd - surface.BSpline.UStart);
            if (surface.BSpline.VPeriodic)
                v = Lift(v, referenceV, surface.BSpline.VEnd - surface.BSpline.VStart);
            return;
        }
        if (surface.Analytic.Kind is SurfaceClass.Cylinder or SurfaceClass.Cone or SurfaceClass.Sphere or SurfaceClass.Torus)
            u = Lift(u, referenceU, 2 * Math.PI);
        if (surface.Analytic.Kind == SurfaceClass.Torus)
            v = Lift(v, referenceV, 2 * Math.PI);
    }

    /// <summary>Unit normal before applying the stored support sense.</summary>
    internal static bool TryNormal(in ICurveSupportView surface, double u, double v,
        in KernelVector3 point, out KernelVector3 normal)
    {
        normal = default;
        if (!surface.IsBSurface)
        {
            if (AnalyticImplicitEvaluation.Evaluate(in surface.Analytic, in point, 1, out var jet)
                != AlgorithmStatus.Success) return false;
            normal = Unit(jet.Gradient);
            return IsFinite(normal) && Dot(normal, normal) > 0.5;
        }
        SurfaceDerivativeLayout.TryCreate(1, 1, out var layout);
        Span<KernelVector3> values = stackalloc KernelVector3[4];
        if (Evaluate(in surface, u, v, in layout, values) != AlgorithmStatus.Success) return false;
        var du = Unit(values[layout.GetIndex(1, 0)]);
        var dv = Unit(values[layout.GetIndex(0, 1)]);
        var cross = Cross(du, dv);
        if (!IsFinite(cross) || !(Dot(cross, cross) > 1e-24)) return false;
        normal = Unit(cross);
        return true;
    }

    private static double Lift(double value, double reference, double period)
        => value + period * Math.Round((reference - value) / period);

    private static bool HasContinuity(ReadOnlySpan<double> knots, SplineDegree degree,
        BufferCount vertexCount, bool periodic, double parameter, DerivativeOrder order)
    {
        var start = knots[degree];
        var end = knots[vertexCount];
        if (periodic && (parameter < start || parameter > end))
        {
            var period = end - start;
            parameter = (parameter - start) % period;
            if (parameter < 0) parameter += period;
            parameter += start;
        }
        // KnotSpan selects the right span within its resolution to the left
        // of a knot. Several distinct knots can fall in that window. Require
        // continuity across every intervening knot, including near domain
        // ends, rather than inspecting a different span from the evaluator.
        for (KnotIndex i = 0; i < knots.Length;)
        {
            var knot = knots[i];
            var first = i++;
            while (i < knots.Length && knots[i] == knot) i++;
            if (knot < start || knot > end || knot < parameter
                || knot - parameter > BCurveEvaluation.KnotSideResolution) continue;
            // The non-periodic boundary itself has an in-domain one-sided
            // jet; this does not waive nearby interior discontinuities.
            if (!periodic && (knot == start || knot == end)) continue;
            if (degree - (i - first) < order) return false;
        }
        return true;
    }
}

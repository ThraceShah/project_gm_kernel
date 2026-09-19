using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Computation.Numerics;

/// <summary>Outcome of a local interval existence/uniqueness test (spec §18.3).</summary>
internal enum IntervalRootStatus : byte
{
    /// <summary>K(X) ∩ X = ∅ — no root in the box.</summary>
    Empty = 0,
    /// <summary>K(X) ⊂ int(X) with a conservative contraction — unique root.</summary>
    Unique = 1,
    /// <summary>Neither empty nor unique; not a certificate either way.</summary>
    Undetermined = 2,
    /// <summary>Interval Jacobian / residual capability unavailable for this cell.</summary>
    BoundsUnavailable = 3,
}

/// <summary>Axis-aligned box in R³ with outward-rounded endpoints.</summary>
internal readonly struct IntervalBox3
{
    internal readonly double XLo, XHi, YLo, YHi, ZLo, ZHi;

    internal IntervalBox3(double xLo, double xHi, double yLo, double yHi, double zLo, double zHi)
    {
        XLo = xLo; XHi = xHi; YLo = yLo; YHi = yHi; ZLo = zLo; ZHi = zHi;
    }

    internal readonly bool IsEmpty
        => !(XLo <= XHi && YLo <= YHi && ZLo <= ZHi);

    internal readonly void Midpoint(out KernelVector3 mid)
        => mid = Vector(0.5 * (XLo + XHi), 0.5 * (YLo + YHi), 0.5 * (ZLo + ZHi));

    internal readonly double RadiusNorm()
    {
        var rx = 0.5 * (XHi - XLo);
        var ry = 0.5 * (YHi - YLo);
        var rz = 0.5 * (ZHi - ZLo);
        return Math.Sqrt(rx * rx + ry * ry + rz * rz);
    }
}

/// <summary>
/// Local Moore–Krawczyk certification for the I3 residual on analytic supports
/// (spec §18.3–§18.4, task T18). Covers plane, sphere, cylinder, cone and
/// ring-torus zero sets. Ordinary Newton samples are never labelled certified —
/// only the inclusion and contraction tests below produce Unique / Empty.
/// </summary>
internal static class IntervalRootCheck
{
    /// <summary>
    /// Certify the I3 system φ₀=φ₁=p=0 on <paramref name="box"/> at fixed t,
    /// where p(x)=e·x − planeOffset. Unsupported surface pairs return
    /// <see cref="IntervalRootStatus.BoundsUnavailable"/>.
    /// </summary>
    internal static AlgorithmStatus TryCertifyI3(in AnalyticSurface support0, in AnalyticSurface support1,
        in KernelVector3 chordUnit, double planeOffset, in IntervalBox3 box,
        out IntervalRootStatus status, out IntervalBox3 image)
    {
        status = IntervalRootStatus.BoundsUnavailable;
        image = default;
        if (box.IsEmpty) return AlgorithmStatus.InvalidInput;
        if (!IsIntervalCapable(in support0) || !IsIntervalCapable(in support1))
            return AlgorithmStatus.Unsupported;

        box.Midpoint(out var x0);
        Span<double> f0 = stackalloc double[3];
        Span<double> jacobian = stackalloc double[9];
        if (PointResidual(in support0, in support1, in chordUnit, planeOffset, in x0, f0, jacobian)
            != AlgorithmStatus.Success)
            return AlgorithmStatus.NumericalFailure;

        Span<double> inverse = stackalloc double[9];
        if (!TryInvert3(jacobian, inverse))
        {
            status = IntervalRootStatus.Undetermined;
            return AlgorithmStatus.Success;
        }

        Span<double> jLo = stackalloc double[9];
        Span<double> jHi = stackalloc double[9];
        if (IntervalJacobian(in support0, in support1, in chordUnit, in box, jLo, jHi)
            != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;

        Span<double> yf = stackalloc double[3];
        MatVec3(inverse, f0, yf);
        var c0 = Vector(x0.X - yf[0], x0.Y - yf[1], x0.Z - yf[2]);

        Span<double> rLo = stackalloc double[9];
        Span<double> rHi = stackalloc double[9];
        for (BufferOffset i = 0; i < 3; i++)
        for (BufferOffset j = 0; j < 3; j++)
        {
            var (pLo, pHi) = MatMulIntervalRowCol(inverse, i, jLo, jHi, j);
            var identity = i == j ? 1.0 : 0.0;
            var lo = OutwardSubLo(identity, pHi);
            var hi = OutwardSubHi(identity, pLo);
            if (lo > hi) (lo, hi) = (hi, lo);
            rLo[i * 3 + j] = lo;
            rHi[i * 3 + j] = hi;
        }

        var dxLo = Vector(box.XLo - x0.X, box.YLo - x0.Y, box.ZLo - x0.Z);
        var dxHi = Vector(box.XHi - x0.X, box.YHi - x0.Y, box.ZHi - x0.Z);
        IntervalMatVec(rLo, rHi, in dxLo, in dxHi, out var rDxLo, out var rDxHi);

        image = Normalize(new IntervalBox3(
            OutwardAddLo(c0.X, rDxLo.X), OutwardAddHi(c0.X, rDxHi.X),
            OutwardAddLo(c0.Y, rDxLo.Y), OutwardAddHi(c0.Y, rDxHi.Y),
            OutwardAddLo(c0.Z, rDxLo.Z), OutwardAddHi(c0.Z, rDxHi.Z)));

        if (Disjoint(in image, in box))
        {
            status = IntervalRootStatus.Empty;
            return AlgorithmStatus.Success;
        }

        if (StrictlyInside(in image, in box) && image.RadiusNorm() < box.RadiusNorm() * (1 - 1e-12))
        {
            status = IntervalRootStatus.Unique;
            return AlgorithmStatus.Success;
        }

        status = IntervalRootStatus.Undetermined;
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// When two I3 Newton hits disagree spatially with comparable residual,
    /// certify tight cells around each. Prefer Unique over Empty; otherwise
    /// leave AmbiguousBranch to the caller (spec §17.5 / §18.3).
    /// </summary>
    internal static bool TryDisambiguateI3Pair(in AnalyticSurface support0, in AnalyticSurface support1,
        in KernelVector3 chordUnit, double planeOffset,
        in KernelVector3 candidateA, in KernelVector3 candidateB, double cellRadius,
        out bool preferA)
    {
        preferA = true;
        if (!(cellRadius > 0)) return false;
        var boxA = BoxAround(in candidateA, cellRadius);
        var boxB = BoxAround(in candidateB, cellRadius);
        if (TryCertifyI3(in support0, in support1, in chordUnit, planeOffset, in boxA,
                out var statusA, out _) != AlgorithmStatus.Success)
            return false;
        if (TryCertifyI3(in support0, in support1, in chordUnit, planeOffset, in boxB,
                out var statusB, out _) != AlgorithmStatus.Success)
            return false;
        if (statusA == IntervalRootStatus.Unique && statusB == IntervalRootStatus.Empty)
        {
            preferA = true;
            return true;
        }
        if (statusA == IntervalRootStatus.Empty && statusB == IntervalRootStatus.Unique)
        {
            preferA = false;
            return true;
        }
        return false;
    }

    /// <summary>Chord-plane offset for p(x)=e·x − offset matching <see cref="OriginalChartParameterMap.PlaneResidual"/>.</summary>
    internal static double ChordPlaneOffset(in KernelVector3 chordUnit, in KernelVector3 segmentAnchor,
        double segmentParameter, double segmentScale, double t)
    {
        var anchored = t - segmentParameter;
        return Dot(chordUnit, segmentAnchor) + anchored / segmentScale;
    }

    private static IntervalBox3 BoxAround(in KernelVector3 center, double radius)
        => new(center.X - radius, center.X + radius,
            center.Y - radius, center.Y + radius,
            center.Z - radius, center.Z + radius);

    private static bool IsIntervalCapable(in AnalyticSurface surface)
        => surface.Kind switch
        {
            SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Sphere
                or SurfaceClass.Cone => true,
            // Ring torus only: spindle/apple (a ≤ b) have no interval Jacobian here.
            SurfaceClass.Torus => surface.Radius > surface.Secondary && surface.Secondary > 0,
            _ => false,
        };

    private static AlgorithmStatus PointResidual(in AnalyticSurface s0, in AnalyticSurface s1,
        in KernelVector3 chordUnit, double planeOffset, in KernelVector3 x,
        Span<double> f, Span<double> jacobian)
    {
        if (AnalyticImplicitEvaluation.Evaluate(in s0, in x, 1, out var j0) != AlgorithmStatus.Success
            || AnalyticImplicitEvaluation.Evaluate(in s1, in x, 1, out var j1) != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;
        f[0] = j0.Value;
        f[1] = j1.Value;
        f[2] = Dot(chordUnit, x) - planeOffset;
        jacobian[0] = j0.Gradient.X; jacobian[1] = j0.Gradient.Y; jacobian[2] = j0.Gradient.Z;
        jacobian[3] = j1.Gradient.X; jacobian[4] = j1.Gradient.Y; jacobian[5] = j1.Gradient.Z;
        jacobian[6] = chordUnit.X; jacobian[7] = chordUnit.Y; jacobian[8] = chordUnit.Z;
        return AlgorithmStatus.Success;
    }

    private static bool TryInvert3(ReadOnlySpan<double> a, Span<double> inverse)
    {
        Span<double> work = stackalloc double[9];
        Span<int> pivots = stackalloc int[3];
        Span<double> rhs = stackalloc double[3];
        for (BufferOffset col = 0; col < 3; col++)
        {
            a.CopyTo(work);
            if (SmallLinearSolve.LuFactorize(work, 3, pivots) != AlgorithmStatus.Success)
                return false;
            rhs[0] = col == 0 ? 1 : 0;
            rhs[1] = col == 1 ? 1 : 0;
            rhs[2] = col == 2 ? 1 : 0;
            if (SmallLinearSolve.LuSolveInPlace(work, 3, pivots, rhs) != AlgorithmStatus.Success)
                return false;
            inverse[col] = rhs[0];
            inverse[3 + col] = rhs[1];
            inverse[6 + col] = rhs[2];
        }
        return true;
    }

    private static AlgorithmStatus IntervalJacobian(in AnalyticSurface s0, in AnalyticSurface s1,
        in KernelVector3 chordUnit, in IntervalBox3 box, Span<double> jLo, Span<double> jHi)
    {
        if (GradientRange(in s0, in box, out var g0Lo, out var g0Hi) != AlgorithmStatus.Success
            || GradientRange(in s1, in box, out var g1Lo, out var g1Hi) != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;
        jLo[0] = g0Lo.X; jHi[0] = g0Hi.X;
        jLo[1] = g0Lo.Y; jHi[1] = g0Hi.Y;
        jLo[2] = g0Lo.Z; jHi[2] = g0Hi.Z;
        jLo[3] = g1Lo.X; jHi[3] = g1Hi.X;
        jLo[4] = g1Lo.Y; jHi[4] = g1Hi.Y;
        jLo[5] = g1Lo.Z; jHi[5] = g1Hi.Z;
        jLo[6] = chordUnit.X; jHi[6] = chordUnit.X;
        jLo[7] = chordUnit.Y; jHi[7] = chordUnit.Y;
        jLo[8] = chordUnit.Z; jHi[8] = chordUnit.Z;
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus GradientRange(in AnalyticSurface surface, in IntervalBox3 box,
        out KernelVector3 gLo, out KernelVector3 gHi)
    {
        gLo = gHi = default;
        switch (surface.Kind)
        {
            case SurfaceClass.Plane:
                gLo = gHi = surface.Axis;
                return AlgorithmStatus.Success;
            case SurfaceClass.Sphere:
            {
                var a = Vector(OutwardMul(2, box.XLo - surface.Origin.X),
                    OutwardMul(2, box.YLo - surface.Origin.Y),
                    OutwardMul(2, box.ZLo - surface.Origin.Z));
                var b = Vector(OutwardMul(2, box.XHi - surface.Origin.X),
                    OutwardMul(2, box.YHi - surface.Origin.Y),
                    OutwardMul(2, box.ZHi - surface.Origin.Z));
                gLo = Vector(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
                gHi = Vector(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
                return AlgorithmStatus.Success;
            }
            case SurfaceClass.Cylinder:
            {
                gLo = Vector(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
                gHi = Vector(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
                for (var ix = 0; ix < 2; ix++)
                for (var iy = 0; iy < 2; iy++)
                for (var iz = 0; iz < 2; iz++)
                {
                    var p = Vector(ix == 0 ? box.XLo : box.XHi, iy == 0 ? box.YLo : box.YHi,
                        iz == 0 ? box.ZLo : box.ZHi);
                    var r = Sub(p, surface.Origin);
                    var axial = Dot(surface.Axis, r);
                    var radial = Scale(Sub(r, Scale(surface.Axis, axial)), 2);
                    gLo = Vector(Math.Min(gLo.X, radial.X), Math.Min(gLo.Y, radial.Y), Math.Min(gLo.Z, radial.Z));
                    gHi = Vector(Math.Max(gHi.X, radial.X), Math.Max(gHi.Y, radial.Y), Math.Max(gHi.Z, radial.Z));
                }
                gLo = Vector(OutwardDown(gLo.X), OutwardDown(gLo.Y), OutwardDown(gLo.Z));
                gHi = Vector(OutwardUp(gHi.X), OutwardUp(gHi.Y), OutwardUp(gHi.Z));
                return AlgorithmStatus.Success;
            }
            case SurfaceClass.Cone:
            {
                // ∇φ = 2ρ⊥ − 2k(R + k z)A is affine in the box corners, so the
                // componentwise range is attained at vertices (same enclosure
                // style as the cylinder). Wrong-nappe rejection stays in the
                // point residual path, not here.
                gLo = Vector(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
                gHi = Vector(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
                var k = surface.Secondary;
                for (var ix = 0; ix < 2; ix++)
                for (var iy = 0; iy < 2; iy++)
                for (var iz = 0; iz < 2; iz++)
                {
                    var p = Vector(ix == 0 ? box.XLo : box.XHi, iy == 0 ? box.YLo : box.YHi,
                        iz == 0 ? box.ZLo : box.ZHi);
                    var r = Sub(p, surface.Origin);
                    var axial = Dot(surface.Axis, r);
                    var radial = Sub(r, Scale(surface.Axis, axial));
                    var generator = surface.Radius + k * axial;
                    var gradient = Sub(Scale(radial, 2), Scale(surface.Axis, 2 * k * generator));
                    gLo = Vector(Math.Min(gLo.X, gradient.X), Math.Min(gLo.Y, gradient.Y), Math.Min(gLo.Z, gradient.Z));
                    gHi = Vector(Math.Max(gHi.X, gradient.X), Math.Max(gHi.Y, gradient.Y), Math.Max(gHi.Z, gradient.Z));
                }
                gLo = Vector(OutwardDown(gLo.X), OutwardDown(gLo.Y), OutwardDown(gLo.Z));
                gHi = Vector(OutwardUp(gHi.X), OutwardUp(gHi.Y), OutwardUp(gHi.Z));
                return AlgorithmStatus.Success;
            }
            case SurfaceClass.Torus:
                return TorusGradientRange(in surface, in box, out gLo, out gHi);
            default:
                return AlgorithmStatus.Unsupported;
        }
    }

    /// <summary>
    /// Natural interval extension of ∇φ = 4·common·r − 8a²·radial for the
    /// algebraic torus residual (spec §8.1 / §18.4). Ring only (a &gt; b &gt; 0);
    /// spindle/apple stay Unsupported. Sheet selection stays in the point path.
    /// </summary>
    private static AlgorithmStatus TorusGradientRange(in AnalyticSurface surface, in IntervalBox3 box,
        out KernelVector3 gLo, out KernelVector3 gHi)
    {
        gLo = gHi = default;
        var a = surface.Radius;
        var b = surface.Secondary;
        if (!(a > b) || !(b > 0)) return AlgorithmStatus.Unsupported;

        var rxLo = box.XLo - surface.Origin.X; var rxHi = box.XHi - surface.Origin.X;
        var ryLo = box.YLo - surface.Origin.Y; var ryHi = box.YHi - surface.Origin.Y;
        var rzLo = box.ZLo - surface.Origin.Z; var rzHi = box.ZHi - surface.Origin.Z;

        var (qxLo, qxHi) = SquareInterval(rxLo, rxHi);
        var (qyLo, qyHi) = SquareInterval(ryLo, ryHi);
        var (qzLo, qzHi) = SquareInterval(rzLo, rzHi);
        var qLo = OutwardAddLo(OutwardAddLo(qxLo, qyLo), qzLo);
        var qHi = OutwardAddHi(OutwardAddHi(qxHi, qyHi), qzHi);

        var ax = surface.Axis.X; var ay = surface.Axis.Y; var az = surface.Axis.Z;
        var (txLo, txHi) = MulScalarInterval(ax, rxLo, rxHi);
        var (tyLo, tyHi) = MulScalarInterval(ay, ryLo, ryHi);
        var (tzLo, tzHi) = MulScalarInterval(az, rzLo, rzHi);
        var axialLo = OutwardAddLo(OutwardAddLo(txLo, tyLo), tzLo);
        var axialHi = OutwardAddHi(OutwardAddHi(txHi, tyHi), tzHi);

        var (axAxLo, axAxHi) = MulScalarInterval(ax, axialLo, axialHi);
        var (ayAxLo, ayAxHi) = MulScalarInterval(ay, axialLo, axialHi);
        var (azAxLo, azAxHi) = MulScalarInterval(az, axialLo, axialHi);
        var radXLo = OutwardSubLo(rxLo, axAxHi); var radXHi = OutwardSubHi(rxHi, axAxLo);
        var radYLo = OutwardSubLo(ryLo, ayAxHi); var radYHi = OutwardSubHi(ryHi, ayAxLo);
        var radZLo = OutwardSubLo(rzLo, azAxHi); var radZHi = OutwardSubHi(rzHi, azAxLo);
        if (radXLo > radXHi) (radXLo, radXHi) = (radXHi, radXLo);
        if (radYLo > radYHi) (radYLo, radYHi) = (radYHi, radYLo);
        if (radZLo > radZHi) (radZLo, radZHi) = (radZHi, radZLo);

        var commonShift = a * a - b * b;
        var cLo = OutwardAddLo(qLo, commonShift);
        var cHi = OutwardAddHi(qHi, commonShift);

        // ∇φ = 4·common·r − 8a²·radial
        var (cxLo, cxHi) = MulInterval(cLo, cHi, rxLo, rxHi);
        var (cyLo, cyHi) = MulInterval(cLo, cHi, ryLo, ryHi);
        var (czLo, czHi) = MulInterval(cLo, cHi, rzLo, rzHi);
        var (rx4Lo, rx4Hi) = MulScalarInterval(4, cxLo, cxHi);
        var (ry4Lo, ry4Hi) = MulScalarInterval(4, cyLo, cyHi);
        var (rz4Lo, rz4Hi) = MulScalarInterval(4, czLo, czHi);

        var eightA2 = 8 * a * a;
        var (sxLo, sxHi) = MulScalarInterval(eightA2, radXLo, radXHi);
        var (syLo, syHi) = MulScalarInterval(eightA2, radYLo, radYHi);
        var (szLo, szHi) = MulScalarInterval(eightA2, radZLo, radZHi);

        gLo = Vector(
            Math.Min(OutwardSubLo(rx4Lo, sxHi), OutwardSubHi(rx4Hi, sxLo)),
            Math.Min(OutwardSubLo(ry4Lo, syHi), OutwardSubHi(ry4Hi, syLo)),
            Math.Min(OutwardSubLo(rz4Lo, szHi), OutwardSubHi(rz4Hi, szLo)));
        gHi = Vector(
            Math.Max(OutwardSubLo(rx4Lo, sxHi), OutwardSubHi(rx4Hi, sxLo)),
            Math.Max(OutwardSubLo(ry4Lo, syHi), OutwardSubHi(ry4Hi, syLo)),
            Math.Max(OutwardSubLo(rz4Lo, szHi), OutwardSubHi(rz4Hi, szLo)));
        return AlgorithmStatus.Success;
    }

    private static (double Lo, double Hi) SquareInterval(double lo, double hi)
    {
        if (lo > hi) (lo, hi) = (hi, lo);
        if (lo >= 0)
        {
            var a = OutwardMul(lo, lo);
            var b = OutwardMul(hi, hi);
            return (Math.Min(a, b), Math.Max(a, b));
        }
        if (hi <= 0)
        {
            var a = OutwardMul(lo, lo);
            var b = OutwardMul(hi, hi);
            return (Math.Min(a, b), Math.Max(a, b));
        }
        var neg = OutwardMul(lo, lo);
        var pos = OutwardMul(hi, hi);
        return (0, Math.Max(neg, pos));
    }

    private static void MatVec3(ReadOnlySpan<double> m, ReadOnlySpan<double> v, Span<double> result)
    {
        for (BufferOffset i = 0; i < 3; i++)
            result[i] = m[i * 3] * v[0] + m[i * 3 + 1] * v[1] + m[i * 3 + 2] * v[2];
    }

    private static (double Lo, double Hi) MatMulIntervalRowCol(ReadOnlySpan<double> y, BufferOffset row,
        ReadOnlySpan<double> jLo, ReadOnlySpan<double> jHi, BufferOffset col)
    {
        var lo = 0.0;
        var hi = 0.0;
        for (BufferOffset k = 0; k < 3; k++)
        {
            var (pLo, pHi) = MulScalarInterval(y[row * 3 + k], jLo[k * 3 + col], jHi[k * 3 + col]);
            lo = OutwardAddLo(lo, pLo);
            hi = OutwardAddHi(hi, pHi);
        }
        if (lo > hi) (lo, hi) = (hi, lo);
        return (lo, hi);
    }

    private static void IntervalMatVec(ReadOnlySpan<double> mLo, ReadOnlySpan<double> mHi,
        in KernelVector3 vLo, in KernelVector3 vHi, out KernelVector3 rLo, out KernelVector3 rHi)
    {
        Span<double> vvLo = stackalloc double[3] { vLo.X, vLo.Y, vLo.Z };
        Span<double> vvHi = stackalloc double[3] { vHi.X, vHi.Y, vHi.Z };
        Span<double> lo = stackalloc double[3];
        Span<double> hi = stackalloc double[3];
        for (BufferOffset i = 0; i < 3; i++)
        {
            var sumLo = 0.0;
            var sumHi = 0.0;
            for (BufferOffset j = 0; j < 3; j++)
            {
                var (pLo, pHi) = MulInterval(mLo[i * 3 + j], mHi[i * 3 + j], vvLo[j], vvHi[j]);
                sumLo = OutwardAddLo(sumLo, pLo);
                sumHi = OutwardAddHi(sumHi, pHi);
            }
            if (sumLo > sumHi) (sumLo, sumHi) = (sumHi, sumLo);
            lo[i] = sumLo;
            hi[i] = sumHi;
        }
        rLo = Vector(lo[0], lo[1], lo[2]);
        rHi = Vector(hi[0], hi[1], hi[2]);
    }

    private static (double Lo, double Hi) MulScalarInterval(double a, double bLo, double bHi)
    {
        var p1 = OutwardMul(a, bLo);
        var p2 = OutwardMul(a, bHi);
        return (Math.Min(p1, p2), Math.Max(p1, p2));
    }

    private static (double Lo, double Hi) MulInterval(double aLo, double aHi, double bLo, double bHi)
    {
        var p1 = OutwardMul(aLo, bLo);
        var p2 = OutwardMul(aLo, bHi);
        var p3 = OutwardMul(aHi, bLo);
        var p4 = OutwardMul(aHi, bHi);
        return (Math.Min(Math.Min(p1, p2), Math.Min(p3, p4)),
            Math.Max(Math.Max(p1, p2), Math.Max(p3, p4)));
    }

    private static bool Disjoint(in IntervalBox3 a, in IntervalBox3 b)
        => a.XHi < b.XLo || b.XHi < a.XLo
            || a.YHi < b.YLo || b.YHi < a.YLo
            || a.ZHi < b.ZLo || b.ZHi < a.ZLo;

    private static bool StrictlyInside(in IntervalBox3 inner, in IntervalBox3 outer)
        => inner.XLo > outer.XLo && inner.XHi < outer.XHi
            && inner.YLo > outer.YLo && inner.YHi < outer.YHi
            && inner.ZLo > outer.ZLo && inner.ZHi < outer.ZHi;

    private static IntervalBox3 Normalize(in IntervalBox3 box)
        => new(
            Math.Min(box.XLo, box.XHi), Math.Max(box.XLo, box.XHi),
            Math.Min(box.YLo, box.YHi), Math.Max(box.YLo, box.YHi),
            Math.Min(box.ZLo, box.ZHi), Math.Max(box.ZLo, box.ZHi));

    private static double OutwardUp(double x) => double.IsInfinity(x) ? x : Math.BitIncrement(x);
    private static double OutwardDown(double x) => double.IsInfinity(x) ? x : Math.BitDecrement(x);
    private static double OutwardAddLo(double a, double b)
    {
        var s = a + b;
        return double.IsFinite(s) ? OutwardDown(s) : s;
    }
    private static double OutwardAddHi(double a, double b)
    {
        var s = a + b;
        return double.IsFinite(s) ? OutwardUp(s) : s;
    }
    private static double OutwardSubLo(double a, double b) => OutwardAddLo(a, -b);
    private static double OutwardSubHi(double a, double b) => OutwardAddHi(a, -b);
    private static double OutwardMul(double a, double b)
    {
        var p = a * b;
        if (!double.IsFinite(p) || p == 0) return p;
        // Conservative: expand away from zero so product ranges stay enclosing.
        return p >= 0 ? OutwardUp(p) : OutwardDown(p);
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>
    /// Axis-aligned UV box for P2 residual (φ₁(S₀(u,v)), p(S₀(u,v))) on
    /// analytic supports (spec §18.3). Corner sampling builds a conservative
    /// image; Newton-only samples are never labelled Certified.
    /// </summary>
    internal readonly struct IntervalBox2
    {
        internal readonly double ULo, UHi, VLo, VHi;
        internal IntervalBox2(double uLo, double uHi, double vLo, double vHi)
        {
            ULo = uLo; UHi = uHi; VLo = vLo; VHi = vHi;
        }
        internal readonly bool IsEmpty => !(ULo <= UHi && VLo <= VHi);
        internal readonly void Midpoint(out double u, out double v)
        {
            u = 0.5 * (ULo + UHi);
            v = 0.5 * (VLo + VHi);
        }
    }

    /// <summary>
    /// Certify the P2 system on a UV box. Unsupported analytic pairs return
    /// BoundsUnavailable. Empty/Unique require a contracting Krawczyk image.
    /// </summary>
    internal static AlgorithmStatus TryCertifyP2(in AnalyticSurface support0, in AnalyticSurface support1,
        in KernelVector3 chordUnit, double planeOffset, in IntervalBox2 uvBox,
        out IntervalRootStatus status)
    {
        status = IntervalRootStatus.BoundsUnavailable;
        if (uvBox.IsEmpty) return AlgorithmStatus.InvalidInput;
        if (!IsIntervalCapable(in support1)) return AlgorithmStatus.Unsupported;
        // Parametric S0 must be an analytic class with bounded UV image sampling.
        if (support0.Kind is not (SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Sphere
            or SurfaceClass.Cone or SurfaceClass.Torus))
            return AlgorithmStatus.Unsupported;

        uvBox.Midpoint(out var u0, out var v0);
        Span<KernelVector3> jet = stackalloc KernelVector3[4];
        if (!SurfaceDerivativeLayout.TryCreate(1, 1, out var layout))
            return AlgorithmStatus.InvalidInput;
        if (SurfaceEvaluation.Evaluate(in support0, u0, v0, in layout, jet) != AlgorithmStatus.Success)
            return AlgorithmStatus.NumericalFailure;
        if (AnalyticImplicitEvaluation.Evaluate(in support1, in jet[0], 1, out var jet1)
            != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;

        Span<double> f0 = stackalloc double[2];
        f0[0] = jet1.Value;
        f0[1] = Dot(chordUnit, jet[0]) - planeOffset;
        var su = jet[layout.GetIndex(1, 0)];
        var sv = jet[layout.GetIndex(0, 1)];
        Span<double> j = stackalloc double[4];
        j[0] = Dot(jet1.Gradient, su);
        j[1] = Dot(jet1.Gradient, sv);
        j[2] = Dot(chordUnit, su);
        j[3] = Dot(chordUnit, sv);
        var det = j[0] * j[3] - j[1] * j[2];
        if (!(Math.Abs(det) > 1e-18))
        {
            status = IntervalRootStatus.Undetermined;
            return AlgorithmStatus.Success;
        }
        // Y = J^{-1}
        var inv00 = j[3] / det; var inv01 = -j[1] / det;
        var inv10 = -j[2] / det; var inv11 = j[0] / det;
        var yf0 = inv00 * f0[0] + inv01 * f0[1];
        var yf1 = inv10 * f0[0] + inv11 * f0[1];
        var c0u = u0 - yf0;
        var c0v = v0 - yf1;

        // Conservative |I − Y J| radius via corner J samples.
        var maxRad = 0.0;
        for (var cu = 0; cu < 2; cu++)
        for (var cv = 0; cv < 2; cv++)
        {
            var uu = cu == 0 ? uvBox.ULo : uvBox.UHi;
            var vv = cv == 0 ? uvBox.VLo : uvBox.VHi;
            if (SurfaceEvaluation.Evaluate(in support0, uu, vv, in layout, jet)
                != AlgorithmStatus.Success)
                continue;
            if (AnalyticImplicitEvaluation.Evaluate(in support1, in jet[0], 1, out jet1)
                != AlgorithmStatus.Success)
                continue;
            su = jet[layout.GetIndex(1, 0)];
            sv = jet[layout.GetIndex(0, 1)];
            var a00 = Dot(jet1.Gradient, su);
            var a01 = Dot(jet1.Gradient, sv);
            var a10 = Dot(chordUnit, su);
            var a11 = Dot(chordUnit, sv);
            var r00 = 1 - (inv00 * a00 + inv01 * a10);
            var r01 = -(inv00 * a01 + inv01 * a11);
            var r10 = -(inv10 * a00 + inv11 * a10);
            var r11 = 1 - (inv10 * a01 + inv11 * a11);
            maxRad = Math.Max(maxRad, Math.Abs(r00) + Math.Abs(r01));
            maxRad = Math.Max(maxRad, Math.Abs(r10) + Math.Abs(r11));
        }

        var hu = 0.5 * (uvBox.UHi - uvBox.ULo);
        var hv = 0.5 * (uvBox.VHi - uvBox.VLo);
        var kULo = c0u - maxRad * hu;
        var kUHi = c0u + maxRad * hu;
        var kVLo = c0v - maxRad * hv;
        var kVHi = c0v + maxRad * hv;

        if (kUHi < uvBox.ULo || kULo > uvBox.UHi || kVHi < uvBox.VLo || kVLo > uvBox.VHi)
        {
            status = IntervalRootStatus.Empty;
            return AlgorithmStatus.Success;
        }
        if (kULo > uvBox.ULo && kUHi < uvBox.UHi && kVLo > uvBox.VLo && kVHi < uvBox.VHi
            && maxRad < 1)
        {
            status = IntervalRootStatus.Unique;
            return AlgorithmStatus.Success;
        }
        status = IntervalRootStatus.Undetermined;
        return AlgorithmStatus.Success;
    }
}

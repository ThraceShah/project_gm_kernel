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
/// Interval-root infrastructure for analytic supports (spec §18.3–§18.4).
/// Strict certificates remain disabled until center residuals, inverses and
/// analytic Jacobian bounds are outward-enclosed end to end.
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
        // The center residual, inverse, center correction and several analytic
        // gradient bounds are still evaluated in ordinary binary64. Outward
        // rounding only the later matrix products cannot recover error already
        // lost there, so Empty/Unique would not be rigorous certificates.
        return AlgorithmStatus.Unsupported;
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
                var (xLo, xHi) = MulScalarInterval(2,
                    OutwardSubLo(box.XLo, surface.Origin.X), OutwardSubHi(box.XHi, surface.Origin.X));
                var (yLo, yHi) = MulScalarInterval(2,
                    OutwardSubLo(box.YLo, surface.Origin.Y), OutwardSubHi(box.YHi, surface.Origin.Y));
                var (zLo, zHi) = MulScalarInterval(2,
                    OutwardSubLo(box.ZLo, surface.Origin.Z), OutwardSubHi(box.ZHi, surface.Origin.Z));
                gLo = Vector(xLo, yLo, zLo);
                gHi = Vector(xHi, yHi, zHi);
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
            return (MulDown(lo, lo), MulUp(hi, hi));
        }
        if (hi <= 0)
        {
            return (MulDown(hi, hi), MulUp(lo, lo));
        }
        var neg = MulUp(lo, lo);
        var pos = MulUp(hi, hi);
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
        var lo = Math.Min(MulDown(a, bLo), MulDown(a, bHi));
        var hi = Math.Max(MulUp(a, bLo), MulUp(a, bHi));
        return (lo, hi);
    }

    private static (double Lo, double Hi) MulInterval(double aLo, double aHi, double bLo, double bHi)
        => MultiplyIntervals(aLo, aHi, bLo, bHi);

    internal static (double Lo, double Hi) MultiplyIntervals(double aLo, double aHi, double bLo, double bHi)
    {
        var lo = Math.Min(Math.Min(MulDown(aLo, bLo), MulDown(aLo, bHi)),
            Math.Min(MulDown(aHi, bLo), MulDown(aHi, bHi)));
        var hi = Math.Max(Math.Max(MulUp(aLo, bLo), MulUp(aLo, bHi)),
            Math.Max(MulUp(aHi, bLo), MulUp(aHi, bHi)));
        return (lo, hi);
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
    private static double MulDown(double a, double b)
    {
        var p = a * b;
        return double.IsFinite(p) ? OutwardDown(p) : p;
    }
    private static double MulUp(double a, double b)
    {
        var p = a * b;
        return double.IsFinite(p) ? OutwardUp(p) : p;
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
        // No conservative interval extension of S(u,v), its derivatives and
        // φ(S(u,v)) exists yet. Corner samples are not bounds (periodic
        // cylinders already provide a counterexample), so this path must not
        // issue Empty/Unique certificates.
        return AlgorithmStatus.Unsupported;
    }
}

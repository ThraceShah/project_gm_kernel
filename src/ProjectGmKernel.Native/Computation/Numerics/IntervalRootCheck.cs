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
/// Interval-root infrastructure for analytic supports (spec §18.3–§18.4, task T18).
/// Center residuals, analytic Jacobian enclosures and Krawczyk image evaluations
/// are outward-rounded end to end.
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
        if (!IsFinite(chordUnit) || !double.IsFinite(planeOffset)) return AlgorithmStatus.InvalidInput;
        if (!IsIntervalCapable(in support0) || !IsIntervalCapable(in support1))
            return AlgorithmStatus.Unsupported;

        box.Midpoint(out var rawMid);
        var mid = Vector(
            Math.Clamp(rawMid.X, box.XLo, box.XHi),
            Math.Clamp(rawMid.Y, box.YLo, box.YHi),
            Math.Clamp(rawMid.Z, box.ZLo, box.ZHi));
        if (!IsFinite(mid)) return AlgorithmStatus.InvalidInput;

        // 1. Outward-rounded interval residual [F](x0) and point Jacobian J(x0) at midpoint.
        Span<double> fLo = stackalloc double[3];
        Span<double> fHi = stackalloc double[3];
        Span<double> jMid = stackalloc double[9];
        var centerStatus = IntervalPointResidualAndJacobian(
            in support0, in support1, in chordUnit, planeOffset, in mid, fLo, fHi, jMid);
        if (centerStatus != AlgorithmStatus.Success)
            return centerStatus;

        // 2. Outward-rounded interval Jacobian [J](X) over the entire box (with sheet guards).
        Span<double> jLo = stackalloc double[9];
        Span<double> jHi = stackalloc double[9];
        var jacStatus = IntervalJacobian(in support0, in support1, in chordUnit, in box, jLo, jHi);
        if (jacStatus != AlgorithmStatus.Success)
            return jacStatus;

        // 3. Preconditioner Y ≈ J(x0)^(-1). If J(x0) is singular, Krawczyk is undetermined.
        Span<double> y = stackalloc double[9];
        if (!TryInvert3(jMid, y))
        {
            status = IntervalRootStatus.Undetermined;
            image = box;
            return AlgorithmStatus.Success;
        }

        // 4. Outward-rounded center term c = x0 − Y·[F](x0).
        var fVecLo = Vector(fLo[0], fLo[1], fLo[2]);
        var fVecHi = Vector(fHi[0], fHi[1], fHi[2]);
        IntervalMatVec(y, y, in fVecLo, in fVecHi, out var yfLo, out var yfHi);
        var cLo = Vector(
            OutwardSubLo(mid.X, yfHi.X),
            OutwardSubLo(mid.Y, yfHi.Y),
            OutwardSubLo(mid.Z, yfHi.Z));
        var cHi = Vector(
            OutwardSubHi(mid.X, yfLo.X),
            OutwardSubHi(mid.Y, yfLo.Y),
            OutwardSubHi(mid.Z, yfLo.Z));

        // 5. Outward-rounded contraction matrix [M] = I − Y·[J](X) and infinity-norm bound.
        Span<double> mLo = stackalloc double[9];
        Span<double> mHi = stackalloc double[9];
        var maxRowSum = 0.0;
        for (BufferOffset r = 0; r < 3; r++)
        {
            var rowSum = 0.0;
            for (BufferOffset c = 0; c < 3; c++)
            {
                var (prodLo, prodHi) = MatMulIntervalRowCol(y, r, jLo, jHi, c);
                var ident = r == c ? 1.0 : 0.0;
                var entryLo = OutwardSubLo(ident, prodHi);
                var entryHi = OutwardSubHi(ident, prodLo);
                if (entryLo > entryHi) (entryLo, entryHi) = (entryHi, entryLo);
                mLo[r * 3 + c] = entryLo;
                mHi[r * 3 + c] = entryHi;
                var absEntry = Math.Max(Math.Abs(entryLo), Math.Abs(entryHi));
                rowSum = OutwardAddHi(rowSum, absEntry);
            }
            if (rowSum > maxRowSum) maxRowSum = rowSum;
        }

        // 6. Outward-rounded centered box D = X − x0 and Krawczyk image K(X) = c + [M]·D.
        var dLo = Vector(
            OutwardSubLo(box.XLo, mid.X),
            OutwardSubLo(box.YLo, mid.Y),
            OutwardSubLo(box.ZLo, mid.Z));
        var dHi = Vector(
            OutwardSubHi(box.XHi, mid.X),
            OutwardSubHi(box.YHi, mid.Y),
            OutwardSubHi(box.ZHi, mid.Z));
        IntervalMatVec(mLo, mHi, in dLo, in dHi, out var mdLo, out var mdHi);

        image = Normalize(new IntervalBox3(
            OutwardAddLo(cLo.X, mdLo.X), OutwardAddHi(cHi.X, mdHi.X),
            OutwardAddLo(cLo.Y, mdLo.Y), OutwardAddHi(cHi.Y, mdHi.Y),
            OutwardAddLo(cLo.Z, mdLo.Z), OutwardAddHi(cHi.Z, mdHi.Z)));
        if (!double.IsFinite(image.XLo) || !double.IsFinite(image.XHi)
            || !double.IsFinite(image.YLo) || !double.IsFinite(image.YHi)
            || !double.IsFinite(image.ZLo) || !double.IsFinite(image.ZHi))
        {
            status = IntervalRootStatus.Undetermined;
            return AlgorithmStatus.Success;
        }

        if (Disjoint(in image, in box))
        {
            status = IntervalRootStatus.Empty;
            return AlgorithmStatus.Success;
        }
        if (StrictlyInside(in image, in box) && maxRowSum < 1.0)
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

    private static AlgorithmStatus IntervalPointResidualAndJacobian(in AnalyticSurface s0, in AnalyticSurface s1,
        in KernelVector3 chordUnit, double planeOffset, in KernelVector3 x,
        Span<double> fLo, Span<double> fHi, Span<double> jacobian)
    {
        if (AnalyticImplicitEvaluation.Evaluate(in s0, in x, 1, out var j0) != AlgorithmStatus.Success
            || AnalyticImplicitEvaluation.Evaluate(in s1, in x, 1, out var j1) != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;
        if (IntervalSurfaceResidual(in s0, in x, out fLo[0], out fHi[0]) != AlgorithmStatus.Success
            || IntervalSurfaceResidual(in s1, in x, out fLo[1], out fHi[1]) != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;

        var (exLo, exHi) = MulScalarInterval(chordUnit.X, x.X, x.X);
        var (eyLo, eyHi) = MulScalarInterval(chordUnit.Y, x.Y, x.Y);
        var (ezLo, ezHi) = MulScalarInterval(chordUnit.Z, x.Z, x.Z);
        var dotLo = OutwardAddLo(OutwardAddLo(exLo, eyLo), ezLo);
        var dotHi = OutwardAddHi(OutwardAddHi(exHi, eyHi), ezHi);
        fLo[2] = OutwardSubLo(dotLo, planeOffset);
        fHi[2] = OutwardSubHi(dotHi, planeOffset);
        if (fLo[2] > fHi[2]) (fLo[2], fHi[2]) = (fHi[2], fLo[2]);

        jacobian[0] = j0.Gradient.X; jacobian[1] = j0.Gradient.Y; jacobian[2] = j0.Gradient.Z;
        jacobian[3] = j1.Gradient.X; jacobian[4] = j1.Gradient.Y; jacobian[5] = j1.Gradient.Z;
        jacobian[6] = chordUnit.X; jacobian[7] = chordUnit.Y; jacobian[8] = chordUnit.Z;
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntervalSurfaceResidual(in AnalyticSurface surface, in KernelVector3 x,
        out double valLo, out double valHi)
    {
        valLo = valHi = 0;
        var rxLo = OutwardSubLo(x.X, surface.Origin.X); var rxHi = OutwardSubHi(x.X, surface.Origin.X);
        var ryLo = OutwardSubLo(x.Y, surface.Origin.Y); var ryHi = OutwardSubHi(x.Y, surface.Origin.Y);
        var rzLo = OutwardSubLo(x.Z, surface.Origin.Z); var rzHi = OutwardSubHi(x.Z, surface.Origin.Z);

        switch (surface.Kind)
        {
            case SurfaceClass.Plane:
            {
                var (pxLo, pxHi) = MulScalarInterval(surface.Axis.X, rxLo, rxHi);
                var (pyLo, pyHi) = MulScalarInterval(surface.Axis.Y, ryLo, ryHi);
                var (pzLo, pzHi) = MulScalarInterval(surface.Axis.Z, rzLo, rzHi);
                valLo = OutwardAddLo(OutwardAddLo(pxLo, pyLo), pzLo);
                valHi = OutwardAddHi(OutwardAddHi(pxHi, pyHi), pzHi);
                if (valLo > valHi) (valLo, valHi) = (valHi, valLo);
                return AlgorithmStatus.Success;
            }
            case SurfaceClass.Sphere:
            {
                var (sxLo, sxHi) = SquareInterval(rxLo, rxHi);
                var (syLo, syHi) = SquareInterval(ryLo, ryHi);
                var (szLo, szHi) = SquareInterval(rzLo, rzHi);
                var (r2Lo, r2Hi) = SquareInterval(surface.Radius, surface.Radius);
                var sumLo = OutwardAddLo(OutwardAddLo(sxLo, syLo), szLo);
                var sumHi = OutwardAddHi(OutwardAddHi(sxHi, syHi), szHi);
                valLo = OutwardSubLo(sumLo, r2Hi);
                valHi = OutwardSubHi(sumHi, r2Lo);
                if (valLo > valHi) (valLo, valHi) = (valHi, valLo);
                return AlgorithmStatus.Success;
            }
            case SurfaceClass.Cylinder:
            {
                IntervalAxialAndRadial(in surface, rxLo, rxHi, ryLo, ryHi, rzLo, rzHi,
                    out _, out _, out var radXLo, out var radXHi, out var radYLo, out var radYHi, out var radZLo, out var radZHi);
                var (sxLo, sxHi) = SquareInterval(radXLo, radXHi);
                var (syLo, syHi) = SquareInterval(radYLo, radYHi);
                var (szLo, szHi) = SquareInterval(radZLo, radZHi);
                var (r2Lo, r2Hi) = SquareInterval(surface.Radius, surface.Radius);
                var sumLo = OutwardAddLo(OutwardAddLo(sxLo, syLo), szLo);
                var sumHi = OutwardAddHi(OutwardAddHi(sxHi, syHi), szHi);
                valLo = OutwardSubLo(sumLo, r2Hi);
                valHi = OutwardSubHi(sumHi, r2Lo);
                if (valLo > valHi) (valLo, valHi) = (valHi, valLo);
                return AlgorithmStatus.Success;
            }
            case SurfaceClass.Cone:
            {
                IntervalAxialAndRadial(in surface, rxLo, rxHi, ryLo, ryHi, rzLo, rzHi,
                    out var axialLo, out var axialHi,
                    out var radXLo, out var radXHi, out var radYLo, out var radYHi, out var radZLo, out var radZHi);
                var (kzLo, kzHi) = MulScalarInterval(surface.Secondary, axialLo, axialHi);
                var genLo = OutwardAddLo(surface.Radius, kzLo);
                var genHi = OutwardAddHi(surface.Radius, kzHi);
                if (genLo > genHi) (genLo, genHi) = (genHi, genLo);
                if (genLo < 0) return AlgorithmStatus.Unsupported;
                var (sxLo, sxHi) = SquareInterval(radXLo, radXHi);
                var (syLo, syHi) = SquareInterval(radYLo, radYHi);
                var (szLo, szHi) = SquareInterval(radZLo, radZHi);
                var (g2Lo, g2Hi) = SquareInterval(genLo, genHi);
                var sumLo = OutwardAddLo(OutwardAddLo(sxLo, syLo), szLo);
                var sumHi = OutwardAddHi(OutwardAddHi(sxHi, syHi), szHi);
                valLo = OutwardSubLo(sumLo, g2Hi);
                valHi = OutwardSubHi(sumHi, g2Lo);
                if (valLo > valHi) (valLo, valHi) = (valHi, valLo);
                return AlgorithmStatus.Success;
            }
            case SurfaceClass.Torus:
            {
                var a = surface.Radius;
                var b = surface.Secondary;
                if (!(a > b) || !(b > 0)) return AlgorithmStatus.Unsupported;
                IntervalAxialAndRadial(in surface, rxLo, rxHi, ryLo, ryHi, rzLo, rzHi,
                    out _, out _, out var radXLo, out var radXHi, out var radYLo, out var radYHi, out var radZLo, out var radZHi);
                var (qxLo, qxHi) = SquareInterval(rxLo, rxHi);
                var (qyLo, qyHi) = SquareInterval(ryLo, ryHi);
                var (qzLo, qzHi) = SquareInterval(rzLo, rzHi);
                var qLo = OutwardAddLo(OutwardAddLo(qxLo, qyLo), qzLo);
                var qHi = OutwardAddHi(OutwardAddHi(qxHi, qyHi), qzHi);
                var (a2Lo, a2Hi) = SquareInterval(a, a);
                var (b2Lo, b2Hi) = SquareInterval(b, b);
                var shiftLo = OutwardSubLo(a2Lo, b2Hi);
                var shiftHi = OutwardSubHi(a2Hi, b2Lo);
                var cLo = OutwardAddLo(qLo, shiftLo);
                var cHi = OutwardAddHi(qHi, shiftHi);
                var (c2Lo, c2Hi) = SquareInterval(cLo, cHi);
                var (rx2Lo, rx2Hi) = SquareInterval(radXLo, radXHi);
                var (ry2Lo, ry2Hi) = SquareInterval(radYLo, radYHi);
                var (rz2Lo, rz2Hi) = SquareInterval(radZLo, radZHi);
                var rho2Lo = OutwardAddLo(OutwardAddLo(rx2Lo, ry2Lo), rz2Lo);
                var rho2Hi = OutwardAddHi(OutwardAddHi(rx2Hi, ry2Hi), rz2Hi);
                var (fourA2Lo, fourA2Hi) = MulScalarInterval(4.0, a2Lo, a2Hi);
                var (subLo, subHi) = MulInterval(fourA2Lo, fourA2Hi, rho2Lo, rho2Hi);
                valLo = OutwardSubLo(c2Lo, subHi);
                valHi = OutwardSubHi(c2Hi, subLo);
                if (valLo > valHi) (valLo, valHi) = (valHi, valLo);
                return AlgorithmStatus.Success;
            }
            default:
                return AlgorithmStatus.Unsupported;
        }
    }

    private static void IntervalAxialAndRadial(in AnalyticSurface surface,
        double rxLo, double rxHi, double ryLo, double ryHi, double rzLo, double rzHi,
        out double axialLo, out double axialHi,
        out double radXLo, out double radXHi, out double radYLo, out double radYHi, out double radZLo, out double radZHi)
    {
        var ax = surface.Axis.X; var ay = surface.Axis.Y; var az = surface.Axis.Z;
        var (txLo, txHi) = MulScalarInterval(ax, rxLo, rxHi);
        var (tyLo, tyHi) = MulScalarInterval(ay, ryLo, ryHi);
        var (tzLo, tzHi) = MulScalarInterval(az, rzLo, rzHi);
        axialLo = OutwardAddLo(OutwardAddLo(txLo, tyLo), tzLo);
        axialHi = OutwardAddHi(OutwardAddHi(txHi, tyHi), tzHi);
        if (axialLo > axialHi) (axialLo, axialHi) = (axialHi, axialLo);

        var (axAxLo, axAxHi) = MulScalarInterval(ax, axialLo, axialHi);
        var (ayAxLo, ayAxHi) = MulScalarInterval(ay, axialLo, axialHi);
        var (azAxLo, azAxHi) = MulScalarInterval(az, axialLo, axialHi);
        radXLo = OutwardSubLo(rxLo, axAxHi); radXHi = OutwardSubHi(rxHi, axAxLo);
        radYLo = OutwardSubLo(ryLo, ayAxHi); radYHi = OutwardSubHi(ryHi, ayAxLo);
        radZLo = OutwardSubLo(rzLo, azAxHi); radZHi = OutwardSubHi(rzHi, azAxLo);
        if (radXLo > radXHi) (radXLo, radXHi) = (radXHi, radXLo);
        if (radYLo > radYHi) (radYLo, radYHi) = (radYHi, radYLo);
        if (radZLo > radZHi) (radZLo, radZHi) = (radZHi, radZLo);
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
        var rxLo = OutwardSubLo(box.XLo, surface.Origin.X); var rxHi = OutwardSubHi(box.XHi, surface.Origin.X);
        var ryLo = OutwardSubLo(box.YLo, surface.Origin.Y); var ryHi = OutwardSubHi(box.YHi, surface.Origin.Y);
        var rzLo = OutwardSubLo(box.ZLo, surface.Origin.Z); var rzHi = OutwardSubHi(box.ZHi, surface.Origin.Z);
        switch (surface.Kind)
        {
            case SurfaceClass.Plane:
                gLo = gHi = surface.Axis;
                return AlgorithmStatus.Success;
            case SurfaceClass.Sphere:
            {
                var (xLo, xHi) = MulScalarInterval(2, rxLo, rxHi);
                var (yLo, yHi) = MulScalarInterval(2, ryLo, ryHi);
                var (zLo, zHi) = MulScalarInterval(2, rzLo, rzHi);
                gLo = Vector(xLo, yLo, zLo);
                gHi = Vector(xHi, yHi, zHi);
                return AlgorithmStatus.Success;
            }
            case SurfaceClass.Cylinder:
            {
                IntervalAxialAndRadial(in surface, rxLo, rxHi, ryLo, ryHi, rzLo, rzHi,
                    out _, out _, out var radXLo, out var radXHi, out var radYLo, out var radYHi, out var radZLo, out var radZHi);
                var (xLo, xHi) = MulScalarInterval(2, radXLo, radXHi);
                var (yLo, yHi) = MulScalarInterval(2, radYLo, radYHi);
                var (zLo, zHi) = MulScalarInterval(2, radZLo, radZHi);
                gLo = Vector(xLo, yLo, zLo);
                gHi = Vector(xHi, yHi, zHi);
                return AlgorithmStatus.Success;
            }
            case SurfaceClass.Cone:
            {
                IntervalAxialAndRadial(in surface, rxLo, rxHi, ryLo, ryHi, rzLo, rzHi,
                    out var axialLo, out var axialHi,
                    out var radXLo, out var radXHi, out var radYLo, out var radYHi, out var radZLo, out var radZHi);
                var k = surface.Secondary;
                var (kzLo, kzHi) = MulScalarInterval(k, axialLo, axialHi);
                var genLo = OutwardAddLo(surface.Radius, kzLo);
                var genHi = OutwardAddHi(surface.Radius, kzHi);
                if (genLo > genHi) (genLo, genHi) = (genHi, genLo);
                // Entire box must lie on the valid half-cone (R + k·z ≥ 0, §8.1 / §18.4).
                if (genLo < 0) return AlgorithmStatus.Unsupported;

                var (rx2Lo, rx2Hi) = MulScalarInterval(2, radXLo, radXHi);
                var (ry2Lo, ry2Hi) = MulScalarInterval(2, radYLo, radYHi);
                var (rz2Lo, rz2Hi) = MulScalarInterval(2, radZLo, radZHi);
                var (twoKLo, twoKHi) = MulScalarInterval(2 * k, genLo, genHi);
                var (axLo, axHi) = MulScalarInterval(surface.Axis.X, twoKLo, twoKHi);
                var (ayLo, ayHi) = MulScalarInterval(surface.Axis.Y, twoKLo, twoKHi);
                var (azLo, azHi) = MulScalarInterval(surface.Axis.Z, twoKLo, twoKHi);

                gLo = Vector(
                    Math.Min(OutwardSubLo(rx2Lo, axHi), OutwardSubHi(rx2Hi, axLo)),
                    Math.Min(OutwardSubLo(ry2Lo, ayHi), OutwardSubHi(ry2Hi, ayLo)),
                    Math.Min(OutwardSubLo(rz2Lo, azHi), OutwardSubHi(rz2Hi, azLo)));
                gHi = Vector(
                    Math.Max(OutwardSubLo(rx2Lo, axHi), OutwardSubHi(rx2Hi, axLo)),
                    Math.Max(OutwardSubLo(ry2Lo, ayHi), OutwardSubHi(ry2Hi, ayLo)),
                    Math.Max(OutwardSubLo(rz2Lo, azHi), OutwardSubHi(rz2Hi, azLo)));
                return AlgorithmStatus.Success;
            }
            case SurfaceClass.Torus:
                return TorusGradientRange(in surface, rxLo, rxHi, ryLo, ryHi, rzLo, rzHi, out gLo, out gHi);
            default:
                return AlgorithmStatus.Unsupported;
        }
    }

    /// <summary>
    /// Outward-rounded interval enclosure of ∇φ = 4(q² − a² − b²)·r⊥ + 4(q² + a² − b²)·z·A
    /// for the algebraic torus residual (spec §8.1 / §18.4). Ring only (a &gt; b &gt; 0);
    /// spindle/apple stay Unsupported. Combining the r⊥ terms avoids the large
    /// interval dependency cancellation between 4·common·r and 8a²·r⊥.
    /// </summary>
    private static AlgorithmStatus TorusGradientRange(in AnalyticSurface surface,
        double rxLo, double rxHi, double ryLo, double ryHi, double rzLo, double rzHi,
        out KernelVector3 gLo, out KernelVector3 gHi)
    {
        gLo = gHi = default;
        var a = surface.Radius;
        var b = surface.Secondary;
        if (!(a > b) || !(b > 0)) return AlgorithmStatus.Unsupported;

        var (qxLo, qxHi) = SquareInterval(rxLo, rxHi);
        var (qyLo, qyHi) = SquareInterval(ryLo, ryHi);
        var (qzLo, qzHi) = SquareInterval(rzLo, rzHi);
        var qLo = OutwardAddLo(OutwardAddLo(qxLo, qyLo), qzLo);
        var qHi = OutwardAddHi(OutwardAddHi(qxHi, qyHi), qzHi);

        IntervalAxialAndRadial(in surface, rxLo, rxHi, ryLo, ryHi, rzLo, rzHi,
            out var axialLo, out var axialHi,
            out var radXLo, out var radXHi, out var radYLo, out var radYHi, out var radZLo, out var radZHi);

        var (a2Lo, a2Hi) = SquareInterval(a, a);
        var (b2Lo, b2Hi) = SquareInterval(b, b);
        var sumAbLo = OutwardAddLo(a2Lo, b2Lo);
        var sumAbHi = OutwardAddHi(a2Hi, b2Hi);
        var diffAbLo = OutwardSubLo(a2Lo, b2Hi);
        var diffAbHi = OutwardSubHi(a2Hi, b2Lo);

        // Radial factor: cRad = 4·(q² − a² − b²)
        var cRadLo = OutwardSubLo(qLo, sumAbHi);
        var cRadHi = OutwardSubHi(qHi, sumAbLo);
        var (cRad4Lo, cRad4Hi) = MulScalarInterval(4, cRadLo, cRadHi);

        // Axial factor: cAx = 4·(q² + a² − b²)·z
        var cAxBaseLo = OutwardAddLo(qLo, diffAbLo);
        var cAxBaseHi = OutwardAddHi(qHi, diffAbHi);
        var (cAxProdLo, cAxProdHi) = MulInterval(cAxBaseLo, cAxBaseHi, axialLo, axialHi);
        var (cAx4Lo, cAx4Hi) = MulScalarInterval(4, cAxProdLo, cAxProdHi);

        var (rxTermLo, rxTermHi) = MulInterval(cRad4Lo, cRad4Hi, radXLo, radXHi);
        var (ryTermLo, ryTermHi) = MulInterval(cRad4Lo, cRad4Hi, radYLo, radYHi);
        var (rzTermLo, rzTermHi) = MulInterval(cRad4Lo, cRad4Hi, radZLo, radZHi);

        var (axTermLo, axTermHi) = MulScalarInterval(surface.Axis.X, cAx4Lo, cAx4Hi);
        var (ayTermLo, ayTermHi) = MulScalarInterval(surface.Axis.Y, cAx4Lo, cAx4Hi);
        var (azTermLo, azTermHi) = MulScalarInterval(surface.Axis.Z, cAx4Lo, cAx4Hi);

        gLo = Vector(
            Math.Min(OutwardAddLo(rxTermLo, axTermLo), OutwardAddHi(rxTermHi, axTermHi)),
            Math.Min(OutwardAddLo(ryTermLo, ayTermLo), OutwardAddHi(ryTermHi, ayTermHi)),
            Math.Min(OutwardAddLo(rzTermLo, azTermLo), OutwardAddHi(rzTermHi, azTermHi)));
        gHi = Vector(
            Math.Max(OutwardAddLo(rxTermLo, axTermLo), OutwardAddHi(rxTermHi, axTermHi)),
            Math.Max(OutwardAddLo(ryTermLo, ayTermLo), OutwardAddHi(ryTermHi, ayTermHi)),
            Math.Max(OutwardAddLo(rzTermLo, azTermLo), OutwardAddHi(rzTermHi, azTermHi)));
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

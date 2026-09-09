using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Offset surface R(u, v) = B(u, v) + d·n̂(u, v) where n̂ = (Bu×Bv)/|Bu×Bv| is
/// the natural unit normal of the base surface, whose parameterisation is
/// inherited (XT 5.2.2.8). Derivatives of n̂ up to the requested rectangular
/// order are computed exactly with truncated rectangular Taylor arithmetic
/// (jets): the base surface is evaluated one order deeper in each direction,
/// and 1/√|m|² (m = Bu×Bv) is built as the Taylor series of (1 − t)^-1/2 with
/// t = 1 − |m|²/g₀ about the evaluation point.
/// </summary>
internal static class OffsetSurfaceEvaluation
{
    /// <summary>Base surface derivative orders (u, v) needed for the given output layout.</summary>
    internal static void BaseOrders(DerivativeOrder uOrder, DerivativeOrder vOrder, out DerivativeOrder baseU, out DerivativeOrder baseV)
    {
        baseU = uOrder + 1;
        baseV = vOrder + 1;
    }

    /// <summary>
    /// Evaluates the offset surface from the base surface derivative table laid
    /// out as SurfaceDerivativeLayout(baseU, baseV).
    /// </summary>
    internal static AlgorithmStatus Evaluate(ReadOnlySpan<KernelVector3> baseDerivs, DerivativeOrder baseVOrder,
        double offset, in SurfaceDerivativeLayout layout, Span<KernelVector3> output)
    {
        if (output.Length < layout.Count) return AlgorithmStatus.OutputTooSmall;
        var jStride = baseVOrder + 1;                 // row stride of the base table
        var I = layout.UOrder;                        // jet truncation = requested layout
        var J = layout.VOrder;
        var stride = J + 1;                           // row stride of the jets
        var count = (I + 1) * stride;
        if (baseDerivs.Length < (layout.UOrder + 2) * jStride || count == 0) return AlgorithmStatus.InvalidInput;

        Span<double> xuX = stackalloc double[count], xuY = stackalloc double[count], xuZ = stackalloc double[count];
        Span<double> xvX = stackalloc double[count], xvY = stackalloc double[count], xvZ = stackalloc double[count];
        Span<double> mX = stackalloc double[count], mY = stackalloc double[count], mZ = stackalloc double[count];
        Span<double> g = stackalloc double[count], r = stackalloc double[count], t = stackalloc double[count];
        Span<double> power = stackalloc double[count], next = stackalloc double[count], sum = stackalloc double[count];
        for (var i = 0; i <= I; i++)
        for (var j = 0; j <= J; j++)
        {
            var index = i * stride + j;
            var bu = baseDerivs[(i + 1) * jStride + j];
            var bv = baseDerivs[i * jStride + j + 1];
            xuX[index] = bu.X; xuY[index] = bu.Y; xuZ[index] = bu.Z;
            xvX[index] = bv.X; xvY[index] = bv.Y; xvZ[index] = bv.Z;
        }

        // m = Bu × Bv.
        Span<double> scratch = stackalloc double[count];
        MulJet(xuY, xvZ, mX, I, J);
        MulJet(xuZ, xvY, scratch, I, J);
        for (var k = 0; k < count; k++) mX[k] -= scratch[k];
        MulJet(xuZ, xvX, mY, I, J);
        MulJet(xuX, xvZ, scratch, I, J);
        for (var k = 0; k < count; k++) mY[k] -= scratch[k];
        MulJet(xuX, xvY, mZ, I, J);
        MulJet(xuY, xvX, scratch, I, J);
        for (var k = 0; k < count; k++) mZ[k] -= scratch[k];

        // g = |m|².
        MulJet(mX, mX, g, I, J);
        MulJet(mY, mY, sum, I, J);
        for (var k = 0; k < count; k++) g[k] += sum[k];
        MulJet(mZ, mZ, sum, I, J);
        for (var k = 0; k < count; k++) g[k] += sum[k];

        // r = 1/√g via the (1 − t)^-1/2 series with t = 1 − g/g0, t[0] = 0.
        // t has zero constant term, so t^k vanishes on every component of total
        // degree < k; the series terminates exactly at total degree I + J.
        var g0 = g[0];
        if (!(g0 > 0) || !double.IsFinite(g0)) return AlgorithmStatus.Singular;
        for (var k = 1; k < count; k++) t[k] = -g[k] / g0;
        t[0] = 0;
        sum.Clear();
        sum[0] = 1;                                   // series accumulator, starts with c0 = 1
        power.Clear();
        power[0] = 1;                                 // t^0
        var scale = 1.0;                              // binom(2k, k) / 4^k
        for (var term = 1; term <= I + J; term++)
        {
            MulJet(power, t, next, I, J);
            next.CopyTo(power);
            scale = scale * (2 * term - 1.0) / (2 * term);
            for (var k = 0; k < count; k++) sum[k] += scale * power[k];
        }
        var root = 1 / Math.Sqrt(g0);
        for (var k = 0; k < count; k++) r[k] = root * sum[k];

        // n = m·r and R = B + d·n.
        Span<double> nX = stackalloc double[count], nY = stackalloc double[count], nZ = stackalloc double[count];
        MulJet(mX, r, nX, I, J);
        MulJet(mY, r, nY, I, J);
        MulJet(mZ, r, nZ, I, J);
        for (var i = 0; i <= I; i++)
        for (var j = 0; j <= J; j++)
        {
            var index = i * stride + j;
            var basePoint = baseDerivs[i * jStride + j];
            var value = Vector(basePoint.X + offset * nX[index], basePoint.Y + offset * nY[index],
                basePoint.Z + offset * nZ[index]);
            if (!IsFinite(value)) return AlgorithmStatus.NumericalFailure;
            output[layout.GetIndex(i, j)] = value;
        }
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Truncated rectangular jet product r = a·b for jets that store raw
    /// derivatives: the Leibniz rule contributes binomial(i, p)·binomial(j, q)
    /// on every mixed term.
    /// </summary>
    private static void MulJet(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> r, DerivativeOrder I, DerivativeOrder J)
    {
        var stride = J + 1;
        for (var i = 0; i <= I; i++)
        for (var j = 0; j <= J; j++)
        {
            double sum = 0;
            for (var p = 0; p <= i; p++)
            {
                var binomialP = BSurfaceEvaluation.Binomial(i, p);
                for (var q = 0; q <= j; q++)
                    sum += binomialP * BSurfaceEvaluation.Binomial(j, q) * a[(i - p) * stride + (j - q)] * b[p * stride + q];
            }
            r[i * stride + j] = sum;
        }
    }
}

using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Computation.Numerics;

/// <summary>
/// Small dense linear algebra for correction steps (spec §14.2). Row-major
/// storage, caller-owned spans, no allocation and no delegation. Factorizations
/// destroy their input matrix; reuse a factorization for multiple right-hand
/// sides, never across different evaluation points as an exact Jacobian.
/// </summary>
internal static class SmallLinearSolve
{
    /// <summary>binary64 unit roundoff complement, 2^-52 (spec §14.1; not Double.Epsilon).</summary>
    internal const double MachineEpsilon = 1.1102230246251565e-16;

    /// <summary>
    /// Partial-pivoted LU in place. <paramref name="pivots"/> holds one swap row
    /// per elimination step. Reports <see cref="AlgorithmStatus.Singular"/> when a
    /// pivot drops below n·ε·max|A|; the input matrix may then contain NaN/Inf.
    /// </summary>
    internal static AlgorithmStatus LuFactorize(Span<double> a, BufferCount n, Span<int> pivots)
    {
        var maxAbs = 0.0;
        for (BufferOffset i = 0; i < n * n; i++)
        {
            var magnitude = Math.Abs(a[i]);
            if (!double.IsFinite(magnitude)) return AlgorithmStatus.InvalidInput;
            if (magnitude > maxAbs) maxAbs = magnitude;
        }
        if (maxAbs <= 0) return AlgorithmStatus.Singular;
        var threshold = n * MachineEpsilon * maxAbs;

        for (BufferOffset k = 0; k < n; k++)
        {
            var pivotRow = k;
            var pivotMagnitude = Math.Abs(a[k * n + k]);
            for (BufferOffset i = k + 1; i < n; i++)
            {
                var magnitude = Math.Abs(a[i * n + k]);
                if (magnitude > pivotMagnitude)
                {
                    pivotMagnitude = magnitude;
                    pivotRow = i;
                }
            }
            pivots[k] = pivotRow;
            if (!(pivotMagnitude > threshold)) return AlgorithmStatus.Singular;
            if (pivotRow != k)
                for (BufferOffset j = 0; j < n; j++)
                    (a[k * n + j], a[pivotRow * n + j]) = (a[pivotRow * n + j], a[k * n + j]);
            var pivot = a[k * n + k];
            for (BufferOffset i = k + 1; i < n; i++)
            {
                a[i * n + k] /= pivot;
                var factor = a[i * n + k];
                if (factor == 0) continue;
                for (BufferOffset j = k + 1; j < n; j++)
                    a[i * n + j] -= factor * a[k * n + j];
            }
        }
        return AlgorithmStatus.Success;
    }

    /// <summary>Solve A x = b in place over <paramref name="b"/> using a LU factorization.</summary>
    internal static AlgorithmStatus LuSolveInPlace(ReadOnlySpan<double> lu, BufferCount n,
        ReadOnlySpan<int> pivots, Span<double> b)
    {
        for (BufferOffset k = 0; k < n; k++)
            if (pivots[k] != k)
                (b[k], b[pivots[k]]) = (b[pivots[k]], b[k]);
        for (BufferOffset i = 1; i < n; i++)
        {
            var sum = b[i];
            for (BufferOffset j = 0; j < i; j++)
                sum -= lu[i * n + j] * b[j];
            b[i] = sum;
        }
        for (var i = n - 1; i >= 0; i--)
        {
            var sum = b[i];
            for (var j = i + 1; j < n; j++)
                sum -= lu[i * n + j] * b[j];
            b[i] = sum / lu[i * n + i];
            if (!double.IsFinite(b[i])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Column-pivoted Householder QR in place for m×n (m ≥ n). On return the
    /// upper triangle holds R, the strict lower triangle holds Householder
    /// vectors (implicit unit diagonal), <paramref name="tau"/> holds the
    /// reflection scale factors and <paramref name="columnPivots"/> the swap
    /// history (entry k = the column swapped into position k at step k). The
    /// permutation is undone in <see cref="QrLeastSquares"/> by replaying that
    /// history in reverse. <paramref name="rankTolerance"/> scales the largest
    /// |R| for rank detection; pass 0 to accept every nonzero diagonal.
    /// </summary>
    internal static AlgorithmStatus QrFactorize(Span<double> a, BufferCount m, BufferCount n,
        Span<double> tau, Span<int> columnPivots, double rankTolerance, out BufferCount rank)
    {
        rank = 0;
        if (m < n) return AlgorithmStatus.InvalidInput;
        for (BufferOffset i = 0; i < m * n; i++)
            if (!double.IsFinite(a[i])) return AlgorithmStatus.InvalidInput;

        for (BufferOffset j = 0; j < n; j++) columnPivots[j] = j;
        var maxAbs = 0.0;
        for (BufferOffset i = 0; i < m * n; i++)
        {
            var magnitude = Math.Abs(a[i]);
            if (magnitude > maxAbs) maxAbs = magnitude;
        }
        var rankThreshold = rankTolerance * maxAbs;

        for (BufferOffset k = 0; k < n; k++)
        {
            // Pick the remaining column with the largest norm below the diagonal.
            var bestColumn = k;
            var bestNorm = ColumnNorm(a, m, n, k, k);
            for (BufferOffset j = k + 1; j < n; j++)
            {
                var norm = ColumnNorm(a, m, n, j, k);
                if (norm > bestNorm)
                {
                    bestNorm = norm;
                    bestColumn = j;
                }
            }
            columnPivots[k] = bestColumn;
            if (bestColumn != k)
                for (BufferOffset i = 0; i < m; i++)
                    (a[i * n + k], a[i * n + bestColumn]) = (a[i * n + bestColumn], a[i * n + k]);

            // Householder reflection for column k: v = x - α e1, H = I - τ v v^T.
            tau[k] = 0;
            if (bestNorm > rankThreshold)
            {
                var x0 = a[k * n + k];
                var sigma = 0.0;
                for (BufferOffset i = k + 1; i < m; i++)
                    sigma += a[i * n + k] * a[i * n + k];
                var alpha = x0 <= 0 ? bestNorm : -bestNorm;
                var v0 = x0 - alpha;
                // τ = 2 v0² / (σ + v0²); then scale v so its first entry is 1.
                tau[k] = 2 * v0 * v0 / (sigma + v0 * v0);
                a[k * n + k] = alpha;
                for (BufferOffset i = k + 1; i < m; i++)
                    a[i * n + k] /= v0;

                for (BufferOffset j = k + 1; j < n; j++)
                {
                    var dot = a[k * n + j];
                    for (BufferOffset i = k + 1; i < m; i++)
                        dot += a[i * n + k] * a[i * n + j];
                    var scale = tau[k] * dot;
                    a[k * n + j] -= scale;
                    for (BufferOffset i = k + 1; i < m; i++)
                        a[i * n + j] -= a[i * n + k] * scale;
                }
                rank = k + 1;
            }
        }
        return AlgorithmStatus.Success;
    }

    /// <summary>Apply Q^T from a QR factorization to <paramref name="b"/> in place (length m).</summary>
    internal static void QrApplyQt(ReadOnlySpan<double> qr, BufferCount m, BufferCount n,
        ReadOnlySpan<double> tau, Span<double> b)
    {
        var limit = Math.Min(m, n);
        for (BufferOffset k = 0; k < limit; k++)
        {
            var factor = tau[k];
            if (factor == 0) continue;
            var dot = b[k];
            for (BufferOffset i = k + 1; i < m; i++)
                dot += qr[i * n + k] * b[i];
            dot *= factor;
            b[k] -= dot;
            for (BufferOffset i = k + 1; i < m; i++)
                b[i] -= qr[i * n + k] * dot;
        }
    }

    /// <summary>
    /// Least-squares solve from a pivoted QR factorization: x solves
    /// min ‖A x − b‖ over the detected column space; components outside the
    /// numerical rank stay zero (pivoted-QR basic solution). b is destroyed.
    /// </summary>
    internal static AlgorithmStatus QrLeastSquares(ReadOnlySpan<double> qr, BufferCount m, BufferCount n,
        ReadOnlySpan<double> tau, BufferCount rank, ReadOnlySpan<int> columnPivots,
        Span<double> b, Span<double> x)
    {
        QrApplyQt(qr, m, n, tau, b);
        for (BufferOffset j = 0; j < n; j++) x[j] = 0;
        for (var k = rank - 1; k >= 0; k--)
        {
            var sum = b[k];
            for (var j = k + 1; j < rank; j++)
                sum -= qr[k * n + j] * x[j];
            x[k] = sum / qr[k * n + k];
            if (!double.IsFinite(x[k])) return AlgorithmStatus.NumericalFailure;
        }
        // Undo the column permutation.
        for (BufferOffset j = n - 1; j >= 0; j--)
            if (columnPivots[j] != j)
                (x[j], x[columnPivots[j]]) = (x[columnPivots[j]], x[j]);
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Square SVD via Jacobi diagonalization of AᵀA (spec §14.2). On success
    /// <paramref name="singularValues"/> holds σ descending, <paramref name="u"/>
    /// and <paramref name="v"/> are orthogonal (row-major n×n). Input
    /// <paramref name="a"/> is preserved. Rank uses
    /// <paramref name="rankTolerance"/>·σ_max.
    /// </summary>
    internal static AlgorithmStatus SvdFactorizeSquare(ReadOnlySpan<double> a, BufferCount n,
        Span<double> singularValues, Span<double> u, Span<double> v,
        double rankTolerance, out BufferCount rank)
    {
        rank = 0;
        if (n <= 0 || a.Length < n * n || singularValues.Length < n
            || u.Length < n * n || v.Length < n * n)
            return AlgorithmStatus.InvalidInput;
        for (BufferOffset i = 0; i < n * n; i++)
            if (!double.IsFinite(a[i])) return AlgorithmStatus.InvalidInput;

        if (n > 6) return AlgorithmStatus.Unsupported;

        // Scale matrix by sMax to prevent underflow/overflow in intermediate products
        var sMax = 0.0;
        for (BufferOffset i = 0; i < n * n; i++)
        {
            var absVal = Math.Abs(a[i]);
            if (absVal > sMax) sMax = absVal;
        }

        if (sMax == 0.0)
        {
            singularValues.Slice(0, n).Clear();
            u.Slice(0, n * n).Clear();
            v.Slice(0, n * n).Clear();
            for (BufferOffset i = 0; i < n; i++)
                v[i * n + i] = 1.0;
            rank = 0;
            return AlgorithmStatus.Success;
        }

        var invSMax = 1.0 / sMax;
        Span<double> bWork = stackalloc double[36];
        for (BufferOffset i = 0; i < n * n; i++)
            bWork[i] = a[i] * invSMax;

        for (BufferOffset i = 0; i < n; i++)
        for (BufferOffset j = 0; j < n; j++)
            v[i * n + j] = i == j ? 1.0 : 0.0;

        const int maxSweeps = 32;
        var converged = false;
        for (BufferOffset sweep = 0; sweep < maxSweeps; sweep++)
        {
            var rotations = 0;
            for (BufferOffset p = 0; p < n - 1; p++)
            for (BufferOffset q = p + 1; q < n; q++)
            {
                var alpha = 0.0;
                var beta = 0.0;
                var gamma = 0.0;
                for (BufferOffset k = 0; k < n; k++)
                {
                    var bp = bWork[k * n + p];
                    var bq = bWork[k * n + q];
                    alpha += bp * bp;
                    beta += bq * bq;
                    gamma += bp * bq;
                }

                if (alpha <= 0 || beta <= 0) continue;

                var normProd = Math.Sqrt(alpha) * Math.Sqrt(beta);
                if (Math.Abs(gamma) <= MachineEpsilon * normProd) continue;

                var tau = (alpha - beta) / (2.0 * gamma);
                var absTau = Math.Abs(tau);
                double t;
                if (absTau > 1e8)
                {
                    t = 0.5 / tau;
                }
                else
                {
                    t = (tau >= 0 ? 1.0 : -1.0) / (absTau + Math.Sqrt(1.0 + tau * tau));
                }
                if (!double.IsFinite(t)) t = 0.0;

                var c = 1.0 / Math.Sqrt(1.0 + t * t);
                var s = t * c;

                for (BufferOffset k = 0; k < n; k++)
                {
                    var bkp = bWork[k * n + p];
                    var bkq = bWork[k * n + q];
                    bWork[k * n + p] = c * bkp + s * bkq;
                    bWork[k * n + q] = -s * bkp + c * bkq;

                    var vkp = v[k * n + p];
                    var vkq = v[k * n + q];
                    v[k * n + p] = c * vkp + s * vkq;
                    v[k * n + q] = -s * vkp + c * vkq;
                }
                rotations++;
            }
            if (rotations == 0)
            {
                converged = true;
                break;
            }
        }

        if (!converged)
        {
            for (BufferOffset p = 0; p < n - 1; p++)
            for (BufferOffset q = p + 1; q < n; q++)
            {
                var alpha = 0.0;
                var beta = 0.0;
                var gamma = 0.0;
                for (BufferOffset k = 0; k < n; k++)
                {
                    alpha += bWork[k * n + p] * bWork[k * n + p];
                    beta += bWork[k * n + q] * bWork[k * n + q];
                    gamma += bWork[k * n + p] * bWork[k * n + q];
                }
                if (alpha > 0 && beta > 0 && Math.Abs(gamma) > 1e-10 * Math.Sqrt(alpha * beta))
                    return AlgorithmStatus.NotConverged;
            }
        }

        // Column norms of B multiplied back by sMax are the singular values.
        Span<double> colNormB = stackalloc double[6];
        for (BufferOffset j = 0; j < n; j++)
        {
            var sumSq = 0.0;
            for (BufferOffset k = 0; k < n; k++)
            {
                var bkj = bWork[k * n + j];
                sumSq += bkj * bkj;
            }
            var norm = Math.Sqrt(sumSq);
            colNormB[j] = norm;
            var sigma = norm * sMax;
            if (!double.IsFinite(sigma)) return AlgorithmStatus.NumericalFailure;
            singularValues[j] = sigma;
        }

        // Sort σ descending and permute B, colNormB and V columns.
        for (BufferOffset i = 0; i < n; i++)
        for (BufferOffset j = i + 1; j < n; j++)
        {
            if (singularValues[j] > singularValues[i])
            {
                (singularValues[i], singularValues[j]) = (singularValues[j], singularValues[i]);
                (colNormB[i], colNormB[j]) = (colNormB[j], colNormB[i]);
                for (BufferOffset k = 0; k < n; k++)
                {
                    (bWork[k * n + i], bWork[k * n + j]) = (bWork[k * n + j], bWork[k * n + i]);
                    (v[k * n + i], v[k * n + j]) = (v[k * n + j], v[k * n + i]);
                }
            }
        }

        var sigmaMax = singularValues[0];
        var threshold = rankTolerance * sigmaMax;
        for (BufferOffset j = 0; j < n; j++)
        {
            if (singularValues[j] > threshold && colNormB[j] > 0.0)
            {
                rank = j + 1;
                var invNorm = 1.0 / colNormB[j];
                for (BufferOffset i = 0; i < n; i++)
                    u[i * n + j] = bWork[i * n + j] * invNorm;
            }
            else
            {
                for (BufferOffset i = 0; i < n; i++)
                    u[i * n + j] = 0.0;
            }
        }

        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Minimum-norm solution of A x = b from a square SVD: x = V Σ⁺ Uᵀ b.
    /// Components for σ below the factorization's rank stay zero.
    /// </summary>
    internal static AlgorithmStatus SvdMinNormSolve(ReadOnlySpan<double> singularValues,
        ReadOnlySpan<double> u, ReadOnlySpan<double> v, BufferCount n, BufferCount rank,
        ReadOnlySpan<double> b, Span<double> x)
    {
        if (x.Length < n || b.Length < n) return AlgorithmStatus.InvalidInput;
        Span<double> utb = stackalloc double[6];
        if (n > 6) return AlgorithmStatus.Unsupported;
        for (BufferOffset j = 0; j < n; j++)
        {
            var sum = 0.0;
            if (j < rank)
            {
                for (BufferOffset i = 0; i < n; i++)
                    sum += u[i * n + j] * b[i];
                sum /= singularValues[j];
            }
            utb[j] = sum;
        }
        for (BufferOffset i = 0; i < n; i++)
        {
            var sum = 0.0;
            for (BufferOffset j = 0; j < rank; j++)
                sum += v[i * n + j] * utb[j];
            x[i] = sum;
            if (!double.IsFinite(x[i])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }

    /// <summary>y = A x for a row-major m×n matrix, accumulated into <paramref name="y"/>.</summary>
    internal static void Multiply(ReadOnlySpan<double> a, BufferCount m, BufferCount n,
        ReadOnlySpan<double> x, Span<double> y)
    {
        for (BufferOffset i = 0; i < m; i++)
        {
            var sum = 0.0;
            for (BufferOffset j = 0; j < n; j++)
                sum += a[i * n + j] * x[j];
            y[i] = sum;
        }
    }

    /// <summary>g = A^T x for a row-major m×n matrix, accumulated into <paramref name="g"/>.</summary>
    internal static void MultiplyTransposed(ReadOnlySpan<double> a, BufferCount m, BufferCount n,
        ReadOnlySpan<double> x, Span<double> g)
    {
        for (BufferOffset j = 0; j < n; j++) g[j] = 0;
        for (BufferOffset i = 0; i < m; i++)
        {
            var xi = x[i];
            if (xi == 0) continue;
            for (BufferOffset j = 0; j < n; j++)
                g[j] += a[i * n + j] * xi;
        }
    }

    /// <summary>Scaled 2-norm that never overflows on finite input and propagates NaN/Inf.</summary>
    internal static double Norm(ReadOnlySpan<double> values)
    {
        var scale = 0.0;
        var sum = 0.0;
        for (BufferOffset i = 0; i < values.Length; i++)
        {
            var magnitude = Math.Abs(values[i]);
            if (!double.IsFinite(magnitude)) return magnitude;
            if (magnitude == 0) continue;
            if (magnitude > scale)
            {
                sum = sum * (scale / magnitude) * (scale / magnitude) + 1;
                scale = magnitude;
            }
            else
            {
                sum += (magnitude / scale) * (magnitude / scale);
            }
        }
        return scale * Math.Sqrt(sum);
    }

    internal static double Dot(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        var sum = 0.0;
        for (BufferOffset i = 0; i < a.Length; i++) sum += a[i] * b[i];
        return sum;
    }

    private static double ColumnNorm(ReadOnlySpan<double> a, BufferCount m, BufferCount n,
        BufferCount column, BufferOffset fromRow)
    {
        var sum = 0.0;
        for (BufferOffset i = fromRow; i < m; i++)
            sum += a[i * n + column] * a[i * n + column];
        return Math.Sqrt(sum);
    }
}

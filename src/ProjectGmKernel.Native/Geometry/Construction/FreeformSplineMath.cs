namespace ProjectGmKernel.Native.Geometry.Construction;

/// <summary>Homogeneous spline operations. All workspaces belong to the caller.</summary>
internal static class FreeformSplineMath
{
    internal const int MaxDegree = 16;

    internal static bool Basis(ReadOnlySpan<double> knots, int count, int degree,
        double parameter, Span<double> values, out int first)
    {
        first = 0;
        if (degree < 1 || degree > MaxDegree || count <= degree || knots.Length != count + degree + 1
            || values.Length < degree + 1 || !double.IsFinite(parameter)
            || parameter < knots[degree] || parameter > knots[count]) return false;
        var low = degree; var high = count;
        if (parameter == knots[count]) low = count - 1;
        else
            while (high - low > 1)
            {
                var middle = (low + high) / 2;
                if (parameter < knots[middle]) high = middle; else low = middle;
            }
        Span<double> left = stackalloc double[MaxDegree + 1];
        Span<double> right = stackalloc double[MaxDegree + 1];
        values[..(degree + 1)].Clear(); values[0] = 1;
        for (var j = 1; j <= degree; j++)
        {
            left[j] = parameter - knots[low + 1 - j];
            right[j] = knots[low + j] - parameter;
            var saved = 0.0;
            for (var r = 0; r < j; r++)
            {
                var denominator = right[r + 1] + left[j - r];
                if (!(denominator > 0)) return false;
                var temporary = values[r] / denominator;
                values[r] = saved + right[r + 1] * temporary;
                saved = left[j - r] * temporary;
            }
            values[j] = saved;
        }
        first = low - degree; return true;
    }

    internal static bool Evaluate(ReadOnlySpan<double> poles, int dimension, ReadOnlySpan<double> knots,
        int degree, double parameter, Span<double> output)
    {
        Span<double> values = stackalloc double[MaxDegree + 1];
        if (!Basis(knots, poles.Length / dimension, degree, parameter, values, out var first)) return false;
        output[..dimension].Clear();
        for (var i = 0; i <= degree; i++)
            for (var d = 0; d < dimension; d++) output[d] += values[i] * poles[(first + i) * dimension + d];
        return true;
    }

    /// <summary>Global interpolation in the averaging-knot B-spline basis, with scaled partial pivoting.</summary>
    internal static bool InterpolateGlobal(ReadOnlySpan<double> parameters, ReadOnlySpan<double> points,
        int dimension, int degree, Span<double> expandedKnots, Span<double> poles, Span<double> matrix)
    {
        var count = parameters.Length;
        if (degree < 1 || degree >= count || degree > MaxDegree || points.Length != count * dimension
            || expandedKnots.Length < count + degree + 1 || poles.Length < points.Length || matrix.Length < count * count) return false;
        for (var i = 0; i < count; i++)
            if (!double.IsFinite(parameters[i]) || i > 0 && !(parameters[i] > parameters[i - 1])) return false;
        for (var i = 0; i <= degree; i++) { expandedKnots[i] = parameters[0]; expandedKnots[count + i] = parameters[^1]; }
        for (var j = 1; j < count - degree; j++)
        {
            var sum = 0.0;
            for (var k = j; k < j + degree; k++) sum += (parameters[k] - parameters[0]) / degree;
            expandedKnots[j + degree] = parameters[0] + sum;
        }
        matrix[..(count * count)].Clear(); points.CopyTo(poles);
        Span<double> basis = stackalloc double[MaxDegree + 1];
        for (var i = 0; i < count; i++)
        {
            if (!Basis(expandedKnots[..(count + degree + 1)], count, degree, parameters[i], basis, out var first)) return false;
            for (var j = 0; j <= degree; j++) matrix[i * count + first + j] = basis[j];
        }
        for (var column = 0; column < count; column++)
        {
            var pivot = column; var value = Math.Abs(matrix[column * count + column]);
            for (var i = column + 1; i < count; i++)
                if (Math.Abs(matrix[i * count + column]) > value) { pivot = i; value = Math.Abs(matrix[i * count + column]); }
            if (!(value > 1e-14) || !double.IsFinite(value)) return false;
            if (pivot != column)
            {
                for (var j = column; j < count; j++)
                    (matrix[pivot * count + j], matrix[column * count + j]) = (matrix[column * count + j], matrix[pivot * count + j]);
                for (var d = 0; d < dimension; d++)
                    (poles[pivot * dimension + d], poles[column * dimension + d]) = (poles[column * dimension + d], poles[pivot * dimension + d]);
            }
            for (var i = column + 1; i < count; i++)
            {
                var factor = matrix[i * count + column] / matrix[column * count + column];
                for (var j = column + 1; j < count; j++) matrix[i * count + j] -= factor * matrix[column * count + j];
                for (var d = 0; d < dimension; d++) poles[i * dimension + d] -= factor * poles[column * dimension + d];
            }
        }
        for (var i = count - 1; i >= 0; i--)
            for (var d = 0; d < dimension; d++)
            {
                var value = poles[i * dimension + d];
                for (var j = i + 1; j < count; j++) value -= matrix[i * count + j] * poles[j * dimension + d];
                poles[i * dimension + d] = value / matrix[i * count + i];
            }
        return true;
    }

    /// <summary>Endpoint-constrained least squares using Householder QR; no normal equations.</summary>
    internal static bool LeastSquares(ReadOnlySpan<double> parameters, ReadOnlySpan<double> points, int degree,
        int controlCount, Span<double> expandedKnots, Span<double> poles, Span<double> matrix, Span<double> rhs)
    {
        var samples = parameters.Length; var unknowns = controlCount - 2;
        if (controlCount <= degree || controlCount > samples || points.Length != 3 * samples || unknowns < 0
            || expandedKnots.Length < controlCount + degree + 1 || poles.Length < 3 * controlCount
            || matrix.Length < samples * unknowns || rhs.Length < 3 * samples) return false;
        for (var i = 0; i <= degree; i++) { expandedKnots[i] = parameters[0]; expandedKnots[controlCount + i] = parameters[^1]; }
        // Piegl/Tiller's approximation knot placement distributes observations over every supported span.
        var intervals = controlCount - degree;
        for (var j = 1; j < intervals; j++)
        {
            var location = (double)j * samples / intervals;
            var index = (int)location; var alpha = location - index;
            expandedKnots[j + degree] = (1 - alpha) * parameters[Math.Max(0, index - 1)] + alpha * parameters[Math.Min(samples - 1, index)];
        }
        points[..3].CopyTo(poles); points[^3..].CopyTo(poles.Slice(3 * (controlCount - 1), 3));
        matrix[..(samples * unknowns)].Clear(); points.CopyTo(rhs);
        Span<double> basis = stackalloc double[MaxDegree + 1];
        for (var i = 0; i < samples; i++)
        {
            if (!Basis(expandedKnots[..(controlCount + degree + 1)], controlCount, degree, parameters[i], basis, out var first)) return false;
            for (var j = 0; j <= degree; j++)
            {
                var column = first + j;
                if (column == 0 || column == controlCount - 1)
                    for (var d = 0; d < 3; d++) rhs[3 * i + d] -= basis[j] * poles[3 * column + d];
                else matrix[i * unknowns + column - 1] = basis[j];
            }
        }
        for (var column = 0; column < unknowns; column++)
        {
            double norm = 0;
            for (var i = column; i < samples; i++) norm += matrix[i * unknowns + column] * matrix[i * unknowns + column];
            norm = Math.Sqrt(norm);
            if (!(norm > 1e-14)) return false;
            var value = matrix[column * unknowns + column]; var alpha = value >= 0 ? -norm : norm;
            var v0 = value - alpha; var squared = v0 * v0;
            for (var i = column + 1; i < samples; i++) squared += matrix[i * unknowns + column] * matrix[i * unknowns + column];
            var beta = 2 / squared;
            for (var j = column + 1; j < unknowns; j++)
            {
                var dot = v0 * matrix[column * unknowns + j];
                for (var i = column + 1; i < samples; i++) dot += matrix[i * unknowns + column] * matrix[i * unknowns + j];
                dot *= beta; matrix[column * unknowns + j] -= dot * v0;
                for (var i = column + 1; i < samples; i++) matrix[i * unknowns + j] -= dot * matrix[i * unknowns + column];
            }
            for (var d = 0; d < 3; d++)
            {
                var dot = v0 * rhs[3 * column + d];
                for (var i = column + 1; i < samples; i++) dot += matrix[i * unknowns + column] * rhs[3 * i + d];
                dot *= beta; rhs[3 * column + d] -= dot * v0;
                for (var i = column + 1; i < samples; i++) rhs[3 * i + d] -= dot * matrix[i * unknowns + column];
            }
            matrix[column * unknowns + column] = alpha;
        }
        for (var i = unknowns - 1; i >= 0; i--)
            for (var d = 0; d < 3; d++)
            {
                var value = rhs[3 * i + d];
                for (var j = i + 1; j < unknowns; j++) value -= matrix[i * unknowns + j] * poles[3 * (j + 1) + d];
                poles[3 * (i + 1) + d] = value / matrix[i * unknowns + i];
            }
        return true;
    }

    /// <summary>One Boehm insertion; existing poles/knots are changed only after capacity validation.</summary>
    internal static bool Insert(Span<double> poles, Span<double> knots, int dimension, int degree,
        ref int count, double value)
    {
        if (poles.Length < (count + 1) * dimension || knots.Length < count + degree + 2
            || value < knots[degree] || value > knots[count]) return false;
        var span = degree;
        while (span + 1 < count && knots[span + 1] <= value) span++;
        if (value == knots[count]) span = count - 1;
        var multiplicity = 0;
        for (var i = 0; i < count + degree + 1; i++) if (knots[i] == value) multiplicity++;
        if (multiplicity >= degree + 1) return false;
        var knotCount = count + degree + 1;
        for (var i = count - 1; i >= span - multiplicity; i--)
            for (var d = 0; d < dimension; d++) poles[(i + 1) * dimension + d] = poles[i * dimension + d];
        for (var i = span - multiplicity; i > span - degree; i--)
        {
            var denominator = knots[i + degree] - knots[i];
            if (!(denominator > 0)) return false;
            var alpha = (value - knots[i]) / denominator;
            for (var d = 0; d < dimension; d++)
                poles[i * dimension + d] = alpha * poles[i * dimension + d] + (1 - alpha) * poles[(i - 1) * dimension + d];
        }
        for (var i = knotCount; i > span + 1; i--) knots[i] = knots[i - 1];
        knots[span + 1] = value; count++; return true;
    }

    /// <summary>Convert an open clamped spline into a common piecewise Bezier basis, with exact degree elevation.</summary>
    internal static bool MatchBezier(ReadOnlySpan<double> inputPoles, int dimension, ReadOnlySpan<double> inputKnots,
        int degree, ReadOnlySpan<double> breaks, int targetDegree, Span<double> output,
        Span<double> workPoles, Span<double> workKnots)
    {
        var count = inputPoles.Length / dimension;
        if (targetDegree < degree || targetDegree > MaxDegree || breaks.Length < 2
            || inputKnots.Length != count + degree + 1
            || inputKnots[0] != inputKnots[degree] || inputKnots[count] != inputKnots[^1]
            || inputKnots[degree] != breaks[0] || inputKnots[count] != breaks[^1]) return false;
        var refinedCount = (breaks.Length - 1) * degree + 1;
        if (workPoles.Length < refinedCount * dimension || workKnots.Length < refinedCount + degree + 1
            || output.Length < ((breaks.Length - 1) * targetDegree + 1) * dimension) return false;
        inputPoles.CopyTo(workPoles); inputKnots.CopyTo(workKnots);
        for (var b = 1; b + 1 < breaks.Length; b++)
        {
            var multiplicity = 0;
            for (var k = 0; k < count + degree + 1; k++) if (workKnots[k] == breaks[b]) multiplicity++;
            while (multiplicity++ < degree)
                if (!Insert(workPoles, workKnots, dimension, degree, ref count, breaks[b])) return false;
        }
        if (count != refinedCount) return false;
        Span<double> segment = stackalloc double[(MaxDegree + 1) * 4];
        for (var b = 0; b + 1 < breaks.Length; b++)
        {
            workPoles.Slice(b * degree * dimension, (degree + 1) * dimension).CopyTo(segment);
            for (var p = degree; p < targetDegree; p++)
            {
                for (var d = 0; d < dimension; d++) segment[(p + 1) * dimension + d] = segment[p * dimension + d];
                for (var i = p; i >= 1; i--)
                {
                    var alpha = (double)i / (p + 1);
                    for (var d = 0; d < dimension; d++)
                        segment[i * dimension + d] = alpha * segment[(i - 1) * dimension + d] + (1 - alpha) * segment[i * dimension + d];
                }
            }
            segment[..((targetDegree + 1) * dimension)].CopyTo(output[(b * targetDegree * dimension)..]);
        }
        return true;
    }

    /// <summary>Natural or endpoint-clamped cubic spline, returned as homogeneous/polynomial Bezier pieces.</summary>
    internal static bool Interpolate(ReadOnlySpan<double> parameters, ReadOnlySpan<double> points, int dimension,
        ReadOnlySpan<double> firstDerivative, ReadOnlySpan<double> lastDerivative, Span<double> poles,
        Span<double> diagonal, Span<double> secondDerivatives)
    {
        var count = parameters.Length;
        if (count < 2 || points.Length != count * dimension || diagonal.Length < count
            || secondDerivatives.Length < points.Length || poles.Length < (3 * (count - 1) + 1) * dimension) return false;
        for (var i = 0; i < count; i++)
            if (!double.IsFinite(parameters[i]) || i > 0 && !(parameters[i] > parameters[i - 1])) return false;
        secondDerivatives[..points.Length].Clear();
        var h0 = parameters[1] - parameters[0];
        diagonal[0] = firstDerivative.IsEmpty ? 1 : 2 * h0;
        if (!firstDerivative.IsEmpty)
            for (var d = 0; d < dimension; d++)
                secondDerivatives[d] = 6 * ((points[dimension + d] - points[d]) / h0 - firstDerivative[d]);
        for (var i = 1; i < count; i++)
        {
            var h = parameters[i] - parameters[i - 1];
            var hn = i == count - 1 ? 0 : parameters[i + 1] - parameters[i];
            var lower = i == count - 1 && lastDerivative.IsEmpty ? 0 : h;
            var previousUpper = i == 1 && firstDerivative.IsEmpty ? 0 : h;
            var factor = lower / diagonal[i - 1];
            diagonal[i] = (i == count - 1 ? lastDerivative.IsEmpty ? 1 : 2 * h : 2 * (h + hn)) - factor * previousUpper;
            if (!(diagonal[i] > 0) || !double.IsFinite(diagonal[i])) return false;
            for (var d = 0; d < dimension; d++)
            {
                var slope = (points[i * dimension + d] - points[(i - 1) * dimension + d]) / h;
                var rhs = i == count - 1 ? lastDerivative.IsEmpty ? 0 : 6 * (lastDerivative[d] - slope)
                    : 6 * ((points[(i + 1) * dimension + d] - points[i * dimension + d]) / hn - slope);
                secondDerivatives[i * dimension + d] = rhs - factor * secondDerivatives[(i - 1) * dimension + d];
            }
        }
        for (var i = count - 1; i >= 0; i--)
        {
            var upper = i == count - 1 || i == 0 && firstDerivative.IsEmpty ? 0 : parameters[i + 1] - parameters[i];
            for (var d = 0; d < dimension; d++)
            {
                var rhs = secondDerivatives[i * dimension + d];
                if (i < count - 1) rhs -= upper * secondDerivatives[(i + 1) * dimension + d];
                secondDerivatives[i * dimension + d] = rhs / diagonal[i];
            }
        }
        for (var i = 0; i < count - 1; i++)
        {
            var h = parameters[i + 1] - parameters[i];
            for (var d = 0; d < dimension; d++)
            {
                var a = points[i * dimension + d]; var b = points[(i + 1) * dimension + d];
                var ma = secondDerivatives[i * dimension + d]; var mb = secondDerivatives[(i + 1) * dimension + d];
                var offset = i * 3 * dimension + d;
                poles[offset] = a;
                poles[offset + dimension] = a + (b - a) / 3 - h * h * (2 * ma + mb) / 18;
                poles[offset + 2 * dimension] = b - (b - a) / 3 - h * h * (ma + 2 * mb) / 18;
                poles[offset + 3 * dimension] = b;
            }
        }
        return true;
    }
}

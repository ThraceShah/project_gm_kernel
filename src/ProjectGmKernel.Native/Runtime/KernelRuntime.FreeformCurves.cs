using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Construction;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    /// <summary>Exact bounded curve conversion to an open B-spline on [0,1], optionally reversing traversal.</summary>
    private static int FreeformBoundedCurve(CurveTag curve, double first, double last, bool reverse, out CurveTag result)
    {
        result = 0;
        var slot = GetCurveSlotByTag(curve); if (slot < 0) return ParasolidConstants.PK_ERROR_bad_class;
        if (!double.IsFinite(first) || !double.IsFinite(last) || !(last > first)) return ParasolidConstants.PK_ERROR_bad_parameter;
        ref readonly var record = ref Curves[slot];
        if (record.Class == CurveClass.BCurve)
        {
            ref readonly var data = ref BCurveDataStore[record.DataIndex];
            if (data.IsPeriodic != 0 || data.Degree > FreeformSplineMath.MaxDegree || data.NVertices > FreeformPoleLimit
                || data.VertexDim != (data.IsRational != 0 ? 4 : 3)) return ParasolidConstants.PK_ERROR_not_implemented;
            if (data.KnotMultsSpan()[0] != data.Degree + 1 || data.KnotMultsSpan()[^1] != data.Degree + 1)
                return ParasolidConstants.PK_ERROR_not_implemented;
            var inputKnots = GetBCurveView(in data).Knots;
            if (first < inputKnots[data.Degree] || last > inputKnots[data.NVertices]) return ParasolidConstants.PK_ERROR_bad_parameter;
            var capacity = data.NVertices + 2 * data.Degree;
            Span<double> poles = stackalloc double[capacity * data.VertexDim];
            Span<double> expanded = stackalloc double[capacity + data.Degree + 1];
            data.PolesSpan().CopyTo(poles); inputKnots.CopyTo(expanded); var count = data.NVertices;
            for (var end = 0; end < 2; end++)
            {
                var value = end == 0 ? first : last; var mult = 0;
                for (var k = 0; k < count + data.Degree + 1; k++) if (expanded[k] == value) mult++;
                while (mult++ < data.Degree)
                    if (!FreeformSplineMath.Insert(poles, expanded, data.VertexDim, data.Degree, ref count, value)) return ParasolidConstants.PK_ERROR_bad_knots;
            }
            var startPole = 0; var endPole = count - 1;
            if (first > expanded[data.Degree]) { var k = data.Degree + 1; while (expanded[k] < first) k++; startPole = k - 1; }
            if (last < expanded[count]) { var k = data.Degree + 1; while (expanded[k] < last) k++; endPole = k - 1; }
            var used = endPole - startPole + 1;
            Span<double> knots = stackalloc double[used + data.Degree + 1];
            Span<int> mults = stackalloc int[used + data.Degree + 1];
            var nk = 1; knots[0] = 0; mults[0] = data.Degree + 1;
            for (var k = 0; k < count + data.Degree + 1; k++)
            {
                if (!(expanded[k] > first && expanded[k] < last)) continue;
                var t = (expanded[k] - first) / (last - first);
                if (knots[nk - 1] == t) mults[nk - 1]++; else { knots[nk] = t; mults[nk++] = 1; }
            }
            knots[nk] = 1; mults[nk++] = data.Degree + 1;
            var bounded = poles.Slice(startPole * data.VertexDim, used * data.VertexDim);
            if (reverse) FreeformReverse(bounded, data.VertexDim, knots[..nk], mults[..nk]);
            CurveTag output = 0; var error = CreateFreeformCurve(data.Degree, bounded, data.VertexDim, data.IsRational != 0,
                knots[..nk], mults[..nk], false, &output); result = output; return error;
        }
        if (record.Class == CurveClass.Line)
        {
            Span<KernelVector3> point = stackalloc KernelVector3[1]; Span<double> poles = stackalloc double[6];
            for (var i = 0; i < 2; i++)
            {
                var status = EvaluateCurveCore(in record, (i == 0) != reverse ? first : last, 0, false, point, out _);
                if (status != AlgorithmStatus.Success) return EvaluationError(status);
                poles[3 * i] = point[0].X; poles[3 * i + 1] = point[0].Y; poles[3 * i + 2] = point[0].Z;
            }
            Span<double> knots = stackalloc double[2] { 0, 1 }; Span<int> mults = stackalloc int[2] { 2, 2 };
            CurveTag output = 0; var error = CreateFreeformCurve(1, poles, 3, false, knots, mults, false, &output); result = output; return error;
        }
        if (record.Class is CurveClass.Circle or CurveClass.Ellipse)
        {
            if (last - first > Math.Tau + 1e-12) return ParasolidConstants.PK_ERROR_bad_parameter;
            KernelVector3 center, x, axis; double rx, ry;
            if (record.Class == CurveClass.Circle)
            {
                ref readonly var data = ref CircleDataPool[record.DataIndex];
                center = Vector(data.CenterX, data.CenterY, data.CenterZ); x = Vector(data.RefDirX, data.RefDirY, data.RefDirZ);
                axis = Vector(data.AxisX, data.AxisY, data.AxisZ); rx = ry = data.Radius;
            }
            else
            {
                ref readonly var data = ref EllipseDataPool[record.DataIndex];
                center = Vector(data.CenterX, data.CenterY, data.CenterZ); x = Vector(data.RefDirX, data.RefDirY, data.RefDirZ);
                axis = Vector(data.AxisX, data.AxisY, data.AxisZ); rx = data.R1; ry = data.R2;
            }
            var y = ProfileCross(axis, x); var segments = (int)Math.Ceiling((last - first) / (Math.PI / 2));
            Span<double> poles = stackalloc double[(2 * segments + 1) * 4];
            Span<double> knots = stackalloc double[segments + 1]; Span<int> mults = stackalloc int[segments + 1];
            for (var i = 0; i <= segments; i++) { knots[i] = (double)i / segments; mults[i] = i == 0 || i == segments ? 3 : 2; }
            for (var i = 0; i <= 2 * segments; i++)
            {
                var angle = first + (last - first) * i / (2 * segments);
                var weight = (i & 1) == 0 ? 1 : Math.Cos((last - first) / (2 * segments));
                var p = ProfileAdd(ProfileScale(center, weight), ProfileAdd(ProfileScale(x, rx * Math.Cos(angle)), ProfileScale(y, ry * Math.Sin(angle))));
                poles[4 * i] = p.X; poles[4 * i + 1] = p.Y; poles[4 * i + 2] = p.Z; poles[4 * i + 3] = weight;
            }
            if (reverse) FreeformReverse(poles, 4, knots, mults);
            CurveTag output = 0; var error = CreateFreeformCurve(2, poles, 4, true, knots, mults, false, &output); result = output; return error;
        }
        return ParasolidConstants.PK_ERROR_not_implemented;
    }

    private static void FreeformReverse(Span<double> poles, BufferCount dimension, Span<double> knots, Span<int> mults)
    {
        var count = poles.Length / dimension;
        for (var i = 0; i < count / 2; i++)
            for (var d = 0; d < dimension; d++) (poles[i * dimension + d], poles[(count - i - 1) * dimension + d]) = (poles[(count - i - 1) * dimension + d], poles[i * dimension + d]);
        for (var i = 0; i < (knots.Length + 1) / 2; i++)
        {
            var other = knots.Length - i - 1; var temporary = knots[i]; knots[i] = 1 - knots[other]; knots[other] = 1 - temporary;
            (mults[i], mults[other]) = (mults[other], mults[i]);
        }
    }

    /// <summary>Convex-hull bound in a common rational Bezier basis, not a point-sampling sewing heuristic.</summary>
    private static bool FreeformSameCurve(CurveTag first, double a0, double a1, CurveTag second, double b0, double b1,
        bool reverse, double tolerance)
    {
        var error = FreeformBoundedCurve(first, a0, a1, false, out var a);
        if (error != 0) return false;
        error = FreeformBoundedCurve(second, b0, b1, reverse, out var b);
        if (error != 0) { EntityDeleteImplementation(1, &a); return false; }
        EntityTag* temporary = stackalloc EntityTag[2] { a, b };
        try
        {
            CurveTag* pair = stackalloc CurveTag[2] { a, b };
            Span<double> breaks = stackalloc double[FreeformPoleLimit];
            error = FreeformMatchingLayout(2, pair, breaks, out var n, out var degree, out _);
            if (error != 0) return false;
            var count = (n - 1) * degree + 1;
            Span<double> pa = stackalloc double[count * 4]; Span<double> pb = stackalloc double[count * 4];
            if (!FreeformMatchPoles(a, breaks[..n], degree, true, pa) || !FreeformMatchPoles(b, breaks[..n], degree, true, pb)) return false;
            var ratio = pa[3] / pb[3];
            for (var i = 0; i < count; i++)
            {
                if (!(pa[4 * i + 3] > 0) || !(pb[4 * i + 3] > 0)) return false;
                // Proportional weights establish identical rational basis functions.
                if (Math.Abs(pa[4 * i + 3] - ratio * pb[4 * i + 3]) > 2e-13 * pa[4 * i + 3]) return false;
                var dx = pa[4 * i] / pa[4 * i + 3] - pb[4 * i] / pb[4 * i + 3];
                var dy = pa[4 * i + 1] / pa[4 * i + 3] - pb[4 * i + 1] / pb[4 * i + 3];
                var dz = pa[4 * i + 2] / pa[4 * i + 3] - pb[4 * i + 2] / pb[4 * i + 3];
                if (Math.Sqrt(dx * dx + dy * dy + dz * dz) > tolerance) return false;
            }
            return true;
        }
        finally { EntityDeleteImplementation(2, temporary); }
    }
}

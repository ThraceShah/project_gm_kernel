using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Construction;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private const int FreeformSectionLimit = 64;
    private const int FreeformPoleLimit = 1024;
    private const int FreeformSampleLimit = 128;

    public static int BCurveCreateSpline(BufferCount count, PK_VECTOR_s* positions, PK_BCURVE_create_spline_o_s* options,
        CurveTag* curve, int* status, BufferCount* faultCount, int** faults)
    {
        if (curve != null) *curve = 0; if (status != null) *status = ParasolidConstants.PK_BCURVE_spline_failure_c;
        if (faultCount != null) *faultCount = 0; if (faults != null) *faults = null;
        if (Dispatcher.IsExecuting) return BCurveCreateSplineCore(count, positions, options, curve, status, faultCount, faults);
        var command = new FreeformCommand { Operation = ApiId.BCurveCreateSpline, Count = count, Positions = positions, Options = options,
            Output = curve, Status = status, FaultCount = faultCount, Faults = faults };
        return Dispatch(command.Operation, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }
    public static int BCurveCreateSpline2(BufferCount count, PK_VECTOR_s* positions, PK_BCURVE_create_spline_2_o_s* options, PK_BCURVE_spline_r_s* result)
    {
        if (result != null) *result = default;
        if (Dispatcher.IsExecuting) return BCurveCreateSpline2Core(count, positions, options, result);
        var command = new FreeformCommand { Operation = ApiId.BCurveCreateSpline2, Count = count, Positions = positions, Options = options, Result = result };
        return Dispatch(command.Operation, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }
    public static int BCurveSplineResultFree(PK_BCURVE_spline_r_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (result->bcurves != null) KernelRuntime.MemoryFree(result->bcurves);
        if (result->fault_values != null) KernelRuntime.MemoryFree(result->fault_values);
        *result = default; return 0;
    }
    public static int BCurveMakeMatched(BufferCount count, CurveTag* curves, PK_BCURVE_make_matched_o_s* options, CurveTag* matched)
    {
        if (Dispatcher.IsExecuting) return BCurveMakeMatchedCore(count, curves, options, matched);
        var command = new FreeformCommand { Operation = ApiId.BCurveMakeMatched, Count = count, Entities = curves, Options = options, Output = matched };
        return Dispatch(command.Operation, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, count > 0 && curves != null ? curves[0] : 0);
    }
    public static int BCurveMakeBSurfLofted(BufferCount count, CurveTag* curves, PK_BCURVE_make_bsurf_lofted_o_s* options, SurfTag* surface)
    {
        if (surface != null) *surface = 0;
        if (Dispatcher.IsExecuting) return BCurveMakeBSurfLoftedCore(count, curves, options, surface);
        var command = new FreeformCommand { Operation = ApiId.BCurveMakeBSurfLofted, Count = count, Entities = curves, Options = options, Output = surface };
        return Dispatch(command.Operation, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, count > 0 && curves != null ? curves[0] : 0);
    }
    public static int BCurveSweep(CurveTag curve, PK_VECTOR_s path, SurfTag* surface)
    {
        if (surface != null) *surface = 0;
        if (Dispatcher.IsExecuting) return BCurveSweepCore(curve, path, surface);
        var command = new FreeformCommand { Operation = ApiId.BCurveSweep, Entity = curve, Vector = path, Output = surface };
        return Dispatch(command.Operation, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, curve);
    }
    public static int BCurveSpin(CurveTag curve, PK_AXIS1_sf_s* axis, double angle, SurfTag* surface)
    {
        if (surface != null) *surface = 0;
        if (Dispatcher.IsExecuting) return BCurveSpinCore(curve, axis, angle, surface);
        var command = new FreeformCommand { Operation = ApiId.BCurveSpin, Entity = curve, Options = axis, Parameter = angle, Output = surface };
        return Dispatch(command.Operation, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, curve);
    }
    public static int BSurfMakeBCurveUIsoparam(SurfTag surface, double parameter, CurveTag* curve)
        => BSurfMakeBCurveIsoparam(surface, parameter, true, curve);
    public static int BSurfMakeBCurveVIsoparam(SurfTag surface, double parameter, CurveTag* curve)
        => BSurfMakeBCurveIsoparam(surface, parameter, false, curve);
    private static int BSurfMakeBCurveIsoparam(SurfTag surface, double parameter, bool u, CurveTag* curve)
    {
        if (curve != null) *curve = 0;
        if (Dispatcher.IsExecuting) return BSurfMakeBCurveIsoparamCore(surface, parameter, u, curve);
        var command = new FreeformCommand { Operation = u ? ApiId.BSurfMakeBCurveUIsoparam : ApiId.BSurfMakeBCurveVIsoparam,
            Entity = surface, Parameter = parameter, Output = curve };
        return Dispatch(command.Operation, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, surface);
    }

    private struct FreeformCommand : IKernelCommand
    {
        internal ApiId Operation;
        internal BufferCount Count;
        internal EntityTag Entity;
        internal EntityTag* Entities;
        internal PK_VECTOR_s* Positions;
        internal PK_VECTOR_s Vector;
        internal double Parameter;
        internal void* Options;
        internal EntityTag* Output;
        internal int* Status;
        internal BufferCount* FaultCount;
        internal int** Faults;
        internal void* Result;
        public int Execute()
        {
            var owns = EnsureCommandScratch();
            try
            {
                var error = Operation switch
                {
                    ApiId.BCurveCreateSpline => BCurveCreateSplineCore(Count, Positions, (PK_BCURVE_create_spline_o_s*)Options, Output, Status, FaultCount, Faults),
                    ApiId.BCurveCreateSpline2 => BCurveCreateSpline2Core(Count, Positions, (PK_BCURVE_create_spline_2_o_s*)Options, (PK_BCURVE_spline_r_s*)Result),
                    ApiId.BCurveMakeMatched => BCurveMakeMatchedCore(Count, Entities, (PK_BCURVE_make_matched_o_s*)Options, Output),
                    ApiId.BCurveMakeBSurfLofted => BCurveMakeBSurfLoftedCore(Count, Entities, (PK_BCURVE_make_bsurf_lofted_o_s*)Options, Output),
                    ApiId.BCurveSweep => BCurveSweepCore(Entity, Vector, Output),
                    ApiId.BCurveSpin => BCurveSpinCore(Entity, (PK_AXIS1_sf_s*)Options, Parameter, Output),
                    ApiId.BSurfMakeBCurveUIsoparam => BSurfMakeBCurveIsoparamCore(Entity, Parameter, true, Output),
                    ApiId.BSurfMakeBCurveVIsoparam => BSurfMakeBCurveIsoparamCore(Entity, Parameter, false, Output),
                    _ => ParasolidConstants.PK_ERROR_not_implemented,
                };
                if (error != 0 && Output != null)
                {
                    if (Operation == ApiId.BCurveMakeMatched && Count > 0 && Count <= FreeformSectionLimit) new Span<EntityTag>(Output, Count).Clear();
                    else *Output = 0;
                }
                return error;
            }
            finally { if (owns) ReleaseCommandScratch(State.Session); }
        }
    }

    private static int BCurveCreateSplineCore(BufferCount count, PK_VECTOR_s* positions, PK_BCURVE_create_spline_o_s* options,
        CurveTag* curve, int* status, BufferCount* faultCount, int** faults)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (curve == null || status == null || faultCount == null || faults == null || options == null || positions == null || count < 2)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->o_t_version != 3) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if (count > FreeformSampleLimit || options->is_periodic != 0 || options->n_knots != 0
            || options->clamp != ParasolidConstants.PK_BCURVE_clamp_no_c || options->have_clamp_axes != 0)
            return ParasolidConstants.PK_ERROR_not_implemented;
        var degree = options->degree == 0 ? Math.Min(3, count - 1) : options->degree;
        if (degree < 1 || degree >= count || degree > FreeformSplineMath.MaxDegree)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        Span<double> parameters = stackalloc double[count];
        Span<double> points = stackalloc double[count * 3];
        for (var i = 0; i < count; i++)
            for (var d = 0; d < 3; d++)
            {
                var value = positions[i].coord[d]; if (!double.IsFinite(value)) return ParasolidConstants.PK_ERROR_bad_vertex;
                points[i * 3 + d] = value;
            }
        var error = FreeformParameters(points, options->param_interval.value[0], options->param_interval.value[1],
            options->n_parameters, options->parameters, options->param_indices, parameters);
        if (error != 0) { *status = ParasolidConstants.PK_BCURVE_spline_bad_params_c; return error; }
        if (options->n_derivs < 0 || options->n_derivs > 2) return ParasolidConstants.PK_ERROR_not_implemented;
        if (options->n_derivs > 0 && (options->derivs == null || options->deriv_orders == null || options->deriv_indices == null))
            return ParasolidConstants.PK_ERROR_bad_parameter;
        Span<double> start = stackalloc double[3]; Span<double> end = stackalloc double[3];
        var haveStart = false; var haveEnd = false;
        for (var i = 0; i < options->n_derivs; i++)
        {
            if (degree != 3 || options->deriv_orders[i] != 1 || options->deriv_indices[i] != 0 && options->deriv_indices[i] != count - 1)
                return ParasolidConstants.PK_ERROR_not_implemented;
            var first = options->deriv_indices[i] == 0;
            if (first ? haveStart : haveEnd) return ParasolidConstants.PK_ERROR_bad_parameter;
            if (first) haveStart = true; else haveEnd = true;
            for (var d = 0; d < 3; d++)
            {
                var value = options->derivs[i].coord[d]; if (!double.IsFinite(value)) return ParasolidConstants.PK_ERROR_bad_parameter;
                if (first) start[d] = value; else end[d] = value;
            }
        }
        var poleCount = options->n_derivs == 0 ? count : 3 * (count - 1) + 1;
        Span<double> poles = stackalloc double[3 * poleCount];
        Span<double> knots = stackalloc double[count + degree + 1];
        Span<int> mults = stackalloc int[count + degree + 1];
        var knotCount = 0;
        if (options->n_derivs == 0)
        {
            var workspace = CommandScratchBorrow(count * count);
            if (workspace.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
            try
            {
                if (!FreeformSplineMath.InterpolateGlobal(parameters, points, 3, degree, knots, poles, workspace))
                    return ParasolidConstants.PK_ERROR_bad_parameter;
            }
            finally { CommandScratchReturn(workspace); }
            for (var i = 0; i < count + degree + 1; i++)
            {
                var value = knots[i];
                if (knotCount > 0 && knots[knotCount - 1] == value) mults[knotCount - 1]++;
                else { knots[knotCount] = value; mults[knotCount++] = 1; }
            }
        }
        else
        {
            Span<double> diagonal = stackalloc double[count]; Span<double> seconds = stackalloc double[count * 3];
            if (!FreeformSplineMath.Interpolate(parameters, points, 3, haveStart ? start : default, haveEnd ? end : default, poles, diagonal, seconds))
                return ParasolidConstants.PK_ERROR_bad_parameter;
            parameters.CopyTo(knots); knotCount = count;
            for (var i = 0; i < count; i++) mults[i] = i == 0 || i == count - 1 ? 4 : 3;
        }
        error = CreateFreeformCurve(degree, poles, 3, false, knots[..knotCount], mults[..knotCount], false, curve);
        if (error == 0) *status = ParasolidConstants.PK_BCURVE_spline_ok_c;
        return error;
    }

    private static int FreeformParameters(ReadOnlySpan<double> points, double first, double last, BufferCount specified,
        double* values, int* indices, Span<double> output)
    {
        if (!double.IsFinite(first) || !double.IsFinite(last) || !(last > first) || specified < 0 || specified > output.Length
            || specified > 0 && (values == null || indices == null)) return ParasolidConstants.PK_ERROR_bad_parameter;
        output.Fill(double.NaN); output[0] = first; output[^1] = last;
        Span<byte> seen = stackalloc byte[output.Length]; seen.Clear();
        for (var i = 0; i < specified; i++)
        {
            var index = indices[i]; var value = values[i];
            if (index < 0 || index >= output.Length || seen[index] != 0 || !double.IsFinite(value) || value < first || value > last)
                return ParasolidConstants.PK_ERROR_bad_parameter;
            if (index == 0 && value != first || index == output.Length - 1 && value != last) return ParasolidConstants.PK_ERROR_bad_parameter;
            output[index] = value; seen[index] = 1;
        }
        Span<double> distance = stackalloc double[output.Length]; distance[0] = 0;
        for (var i = 1; i < output.Length; i++)
        {
            var dx = points[3 * i] - points[3 * (i - 1)]; var dy = points[3 * i + 1] - points[3 * (i - 1) + 1]; var dz = points[3 * i + 2] - points[3 * (i - 1) + 2];
            var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (!double.IsFinite(length) || !(length > 1e-14)) return ParasolidConstants.PK_ERROR_bad_parameter;
            distance[i] = distance[i - 1] + length;
        }
        for (var start = 0; start < output.Length - 1;)
        {
            var stop = start + 1; while (double.IsNaN(output[stop])) stop++;
            if (!(output[stop] > output[start])) return ParasolidConstants.PK_ERROR_bad_parameter;
            for (var i = start + 1; i < stop; i++)
                output[i] = output[start] + (output[stop] - output[start]) * (distance[i] - distance[start]) / (distance[stop] - distance[start]);
            start = stop;
        }
        return 0;
    }

    private static int BCurveCreateSpline2Core(BufferCount count, PK_VECTOR_s* positions, PK_BCURVE_create_spline_2_o_s* options, PK_BCURVE_spline_r_s* result)
    {
        if (options == null || result == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->o_t_version != 2) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if (options->spline_method is not (ParasolidConstants.PK_spline_method_interpolate_c or ParasolidConstants.PK_spline_method_fit_c)
            || options->n_special != 0 || options->have_chordal_tol != 0 || options->n_breaks != 0) return ParasolidConstants.PK_ERROR_not_implemented;
        if (options->spline_method == ParasolidConstants.PK_spline_method_fit_c)
            return FreeformFitSpline(count, positions, options, result);
        var legacy = new PK_BCURVE_create_spline_o_s
        {
            o_t_version = 3, param_interval = options->param_interval, n_parameters = options->n_parameters,
            parameters = options->parameters, param_indices = options->param_indices, n_derivs = options->n_derivs,
            derivs = options->derivs, deriv_orders = options->deriv_orders, deriv_indices = options->deriv_indices,
            degree = options->degree, is_periodic = options->is_periodic, n_knots = options->n_knots,
            knots = options->knots, knot_mults = options->knot_mults, overdefined = options->overdefined,
            clamp = options->clamp, have_clamp_axes = options->have_clamp_axes, clamp_axes = options->clamp_axes,
        };
        CurveTag curve = 0; var fault = 0; BufferCount faultsCount = 0; int* faults = null;
        var error = BCurveCreateSplineCore(count, positions, &legacy, &curve, &fault, &faultsCount, &faults);
        result->fault = fault;
        if (error != 0) return error;
        var info = (PK_BCURVE_spline_cu_s*)State.Session->Returns.TryAllocate((nuint)sizeof(PK_BCURVE_spline_cu_s));
        if (info == null) return ParasolidConstants.PK_ERROR_memory_full;
        *info = new PK_BCURVE_spline_cu_s { bcurve = curve, status = ParasolidConstants.PK_BCURVE_spline_stat_exact_c };
        result->n_bcurves = 1; result->bcurves = info; return 0;
    }

    private static int FreeformFitSpline(BufferCount count, PK_VECTOR_s* positions, PK_BCURVE_create_spline_2_o_s* options,
        PK_BCURVE_spline_r_s* result)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (positions == null || count < 2 || count > FreeformSampleLimit || options->have_fit_tol == 0
            || !double.IsFinite(options->fit_tol) || options->fit_tol <= 0) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->n_derivs != 0 || options->n_knots != 0 || options->is_periodic != 0 || options->have_clamp_axes != 0
            || options->clamp != ParasolidConstants.PK_BCURVE_clamp_no_c) return ParasolidConstants.PK_ERROR_not_implemented;
        var degree = options->degree == 0 ? Math.Min(3, count - 1) : options->degree;
        if (degree < 1 || degree >= count || degree > FreeformSplineMath.MaxDegree) return ParasolidConstants.PK_ERROR_bad_parameter;
        Span<double> points = stackalloc double[count * 3];
        for (var i = 0; i < count; i++) for (var d = 0; d < 3; d++)
        {
            var value = positions[i].coord[d]; if (!double.IsFinite(value)) return ParasolidConstants.PK_ERROR_bad_vertex;
            points[3 * i + d] = value;
        }
        Span<double> parameters = stackalloc double[count];
        var error = FreeformParameters(points, options->param_interval.value[0], options->param_interval.value[1],
            options->n_parameters, options->parameters, options->param_indices, parameters);
        if (error != 0) return error;
        Span<double> poles = stackalloc double[count * 3]; Span<double> knots = stackalloc double[count + degree + 1];
        Span<int> mults = stackalloc int[count + degree + 1]; Span<double> rhs = stackalloc double[count * 3];
        var workspace = CommandScratchBorrow(count * count); if (workspace.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        var controls = degree + 1; double maximum = double.PositiveInfinity, worstParameter = parameters[0];
        try
        {
            Span<double> sample = stackalloc double[3];
            for (; controls <= count; controls++)
            {
                var ok = controls == count
                    ? FreeformSplineMath.InterpolateGlobal(parameters, points, 3, degree, knots, poles, workspace)
                    : FreeformSplineMath.LeastSquares(parameters, points, degree, controls, knots, poles, workspace, rhs);
                if (!ok) continue;
                maximum = 0;
                for (var i = 0; i < count; i++)
                {
                    if (!FreeformSplineMath.Evaluate(poles[..(3 * controls)], 3, knots[..(controls + degree + 1)], degree, parameters[i], sample))
                    { maximum = double.PositiveInfinity; break; }
                    var dx = sample[0] - points[3 * i]; var dy = sample[1] - points[3 * i + 1]; var dz = sample[2] - points[3 * i + 2];
                    var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (!double.IsFinite(distance)) { maximum = double.PositiveInfinity; break; }
                    if (distance > maximum) { maximum = distance; worstParameter = parameters[i]; }
                }
                if (maximum <= options->fit_tol) break;
            }
        }
        finally { CommandScratchReturn(workspace); }
        if (controls > count || !double.IsFinite(maximum)) return ParasolidConstants.PK_ERROR_bad_parameter;
        var knotCount = 0;
        for (var k = 0; k < controls + degree + 1; k++)
        {
            var value = knots[k];
            if (knotCount > 0 && knots[knotCount - 1] == value) mults[knotCount - 1]++;
            else { knots[knotCount] = value; mults[knotCount++] = 1; }
        }
        CurveTag curve = 0;
        error = CreateFreeformCurve(degree, poles[..(3 * controls)], 3, false, knots[..knotCount], mults[..knotCount], false, &curve);
        if (error != 0) return error;
        var info = (PK_BCURVE_spline_cu_s*)State.Session->Returns.TryAllocate((nuint)sizeof(PK_BCURVE_spline_cu_s));
        if (info == null) return ParasolidConstants.PK_ERROR_memory_full;
        *info = new PK_BCURVE_spline_cu_s { bcurve = curve, status = maximum <= 1e-12 ? ParasolidConstants.PK_BCURVE_spline_stat_exact_c
            : ParasolidConstants.PK_BCURVE_spline_stat_approx_c, distance_error = maximum, distance_t = worstParameter };
        *result = new PK_BCURVE_spline_r_s { fault = ParasolidConstants.PK_BCURVE_spline_ok_c, n_bcurves = 1, bcurves = info }; return 0;
    }

    private static int CreateFreeformCurve(SplineDegree degree, ReadOnlySpan<double> poles, BufferCount dimension, bool rational,
        ReadOnlySpan<double> knots, ReadOnlySpan<int> multiplicities, bool closed, CurveTag* curve)
    {
        fixed (double* p = poles, k = knots) fixed (int* m = multiplicities)
        {
            var definition = new PK_BCURVE_sf_s
            {
                degree = degree, n_vertices = poles.Length / dimension, vertex_dim = dimension, vertex = p,
                is_rational = (byte)(rational ? 1 : 0), n_knots = knots.Length, knot = k, knot_mult = m,
                form = ParasolidConstants.PK_BCURVE_form_arbitrary_c, knot_type = ParasolidConstants.PK_knot_bezier_ends_c,
                is_closed = (byte)(closed ? 1 : 0), self_intersecting = ParasolidConstants.PK_self_intersect_unset_c,
            };
            return BCurveCreateImplementation(&definition, curve);
        }
    }

    private static int FreeformMatchingLayout(BufferCount count, CurveTag* curves, Span<double> breaks,
        out BufferCount breakCount, out SplineDegree degree, out bool rational)
    {
        breakCount = 0; degree = 0; rational = false;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (count < 2 || count > FreeformSectionLimit || curves == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        for (var i = 0; i < count; i++)
        {
            var slot = GetCurveSlotByTag(curves[i]);
            if (slot < 0 || Curves[slot].Class != CurveClass.BCurve) return ParasolidConstants.PK_ERROR_bad_class;
            if (Curves[slot].Header.Partition != CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
            ref readonly var data = ref BCurveDataStore[Curves[slot].DataIndex];
            if (data.VertexDim != (data.IsRational != 0 ? 4 : 3) || data.IsPeriodic != 0 || data.Degree > FreeformSplineMath.MaxDegree
                || data.NVertices > FreeformPoleLimit) return ParasolidConstants.PK_ERROR_not_implemented;
            degree = Math.Max(degree, data.Degree); rational |= data.IsRational != 0;
            var k = data.KnotsSpan(); var m = data.KnotMultsSpan();
            if (m[0] != data.Degree + 1 || m[^1] != data.Degree + 1) return ParasolidConstants.PK_ERROR_not_implemented;
            if (i > 0 && (k[0] != breaks[0] || k[^1] != breaks[breakCount - 1])) return ParasolidConstants.PK_ERROR_bad_parameter;
            for (var j = 0; j < k.Length; j++)
            {
                var index = 0; while (index < breakCount && breaks[index] < k[j]) index++;
                if (index < breakCount && breaks[index] == k[j]) continue;
                if (breakCount == breaks.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                for (var b = breakCount; b > index; b--) breaks[b] = breaks[b - 1];
                breaks[index] = k[j]; breakCount++;
            }
        }
        if ((breakCount - 1) * degree + 1 > FreeformPoleLimit) return ParasolidConstants.PK_ERROR_not_implemented;
        return 0;
    }

    private static bool FreeformMatchPoles(CurveTag curve, ReadOnlySpan<double> breaks, SplineDegree degree,
        bool rational, Span<double> output)
    {
        ref readonly var data = ref BCurveDataStore[Curves[GetCurveSlotByTag(curve)].DataIndex];
        var dim = rational ? 4 : 3; var count = (breaks.Length - 1) * data.Degree + 1;
        Span<double> input = stackalloc double[data.NVertices * dim];
        var source = data.PolesSpan();
        for (var i = 0; i < data.NVertices; i++)
        {
            for (var d = 0; d < 3; d++) input[i * dim + d] = source[i * data.VertexDim + d];
            if (rational) input[i * dim + 3] = data.IsRational != 0 ? source[i * data.VertexDim + 3] : 1;
        }
        Span<double> workspace = stackalloc double[count * dim];
        Span<double> knots = stackalloc double[count + data.Degree + 1];
        return FreeformSplineMath.MatchBezier(input, dim, GetBCurveView(in data).Knots, data.Degree, breaks, degree, output, workspace, knots);
    }

    private static int BCurveMakeMatchedCore(BufferCount count, CurveTag* curves, PK_BCURVE_make_matched_o_s* options, CurveTag* matched)
    {
        if (options == null || matched == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->o_t_version != 1) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        Span<double> breaks = stackalloc double[FreeformPoleLimit];
        var error = FreeformMatchingLayout(count, curves, breaks, out var breakCount, out var degree, out var rational);
        if (error != 0) return error;
        breaks = breaks[..breakCount]; var dimension = rational ? 4 : 3;
        Span<double> poles = stackalloc double[((breakCount - 1) * degree + 1) * dimension];
        Span<int> mults = stackalloc int[breakCount];
        for (var i = 0; i < breakCount; i++) mults[i] = i == 0 || i == breakCount - 1 ? degree + 1 : degree;
        Span<CurveTag> products = stackalloc CurveTag[count]; products.Clear();
        for (var i = 0; i < count; i++)
        {
            if (!FreeformMatchPoles(curves[i], breaks, degree, rational, poles)) return ParasolidConstants.PK_ERROR_bad_knots;
            CurveTag output = 0;
            error = CreateFreeformCurve(degree, poles, dimension, rational, breaks, mults,
                BCurveDataStore[Curves[GetCurveSlotByTag(curves[i])].DataIndex].IsClosed != 0, &output);
            if (error != 0) return error;
            products[i] = output;
        }
        products.CopyTo(new Span<CurveTag>(matched, count)); return 0;
    }
}

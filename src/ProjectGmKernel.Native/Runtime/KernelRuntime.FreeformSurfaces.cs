using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Construction;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int BCurveMakeBSurfLoftedCore(BufferCount count, CurveTag* curves, PK_BCURVE_make_bsurf_lofted_o_s* options, SurfTag* surface)
    {
        if (options == null || surface == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->o_t_version != 1) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if (options->v_end != ParasolidConstants.PK_PARAM_end_natural_c || options->V_end != ParasolidConstants.PK_PARAM_end_natural_c
            || options->n_v_derivs != 0 || options->n_V_derivs != 0 || options->u_v != ParasolidConstants.PK_PARAM_twist_no_c
            || options->u_V != ParasolidConstants.PK_PARAM_twist_no_c || options->U_v != ParasolidConstants.PK_PARAM_twist_no_c
            || options->U_V != ParasolidConstants.PK_PARAM_twist_no_c || options->v_degen != ParasolidConstants.PK_PARAM_degen_no_c
            || options->V_degen != ParasolidConstants.PK_PARAM_degen_no_c || options->n_v_degen_bcurve != 0 || options->n_V_degen_bcurve != 0
            || options->n_v_degen_segs != 0 || options->n_V_degen_segs != 0 || options->how_knot != ParasolidConstants.PK_PARAM_knot_auto_c)
            return ParasolidConstants.PK_ERROR_not_implemented;
        Span<double> breaks = stackalloc double[FreeformPoleLimit];
        var error = FreeformMatchingLayout(count, curves, breaks, out var breakCount, out var degree, out var rational);
        if (error != 0) return error;
        breaks = breaks[..breakCount];
        if (options->amalgamate == 0)
        {
            ref readonly var first = ref BCurveDataStore[Curves[GetCurveSlotByTag(curves[0])].DataIndex];
            for (var i = 1; i < count; i++)
            {
                ref readonly var current = ref BCurveDataStore[Curves[GetCurveSlotByTag(curves[i])].DataIndex];
                if (current.Degree != first.Degree || !current.KnotsSpan().SequenceEqual(first.KnotsSpan())
                    || !current.KnotMultsSpan().SequenceEqual(first.KnotMultsSpan())) return ParasolidConstants.PK_ERROR_bad_knots;
            }
        }
        var dim = rational ? 4 : 3; var columns = (breakCount - 1) * degree + 1;
        var loftDegree = count == 2 && options->force_cubic == 0 ? 1 : 3;
        var rows = loftDegree == 1 ? 2 : 3 * (count - 1) + 1;
        if ((long)columns * rows > 8192) return ParasolidConstants.PK_ERROR_not_implemented;
        var samples = CommandScratchBorrow(count * columns * dim);
        if (samples.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            for (var i = 0; i < count; i++)
                if (!FreeformMatchPoles(curves[i], breaks, degree, rational, samples.Slice(i * columns * dim, columns * dim)))
                    return ParasolidConstants.PK_ERROR_bad_knots;
            Span<double> poles = stackalloc double[columns * rows * dim];
            Span<double> parameters = stackalloc double[count];
            Span<int> vm = stackalloc int[count];
            for (var i = 0; i < count; i++) { parameters[i] = (double)i / (count - 1); vm[i] = i == 0 || i == count - 1 ? loftDegree + 1 : loftDegree; }
            Span<int> um = stackalloc int[breakCount];
            for (var i = 0; i < breakCount; i++) um[i] = i == 0 || i == breakCount - 1 ? degree + 1 : degree;
            Span<double> column = stackalloc double[count * dim]; Span<double> diagonal = stackalloc double[count];
            Span<double> seconds = stackalloc double[count * dim];
            for (var c = 0; c < columns; c++)
            {
                for (var r = 0; r < count; r++) samples.Slice((r * columns + c) * dim, dim).CopyTo(column.Slice(r * dim, dim));
                if (loftDegree == 1) column.CopyTo(poles.Slice(c * rows * dim, rows * dim));
                else if (!FreeformSplineMath.Interpolate(parameters, column, dim, default, default, poles.Slice(c * rows * dim, rows * dim), diagonal, seconds))
                    return ParasolidConstants.PK_ERROR_bad_parameter;
            }
            return CreateFreeformSurface(degree, loftDegree, columns, rows, poles, dim, rational,
                breaks, um, parameters, vm, false, false, surface);
        }
        finally { CommandScratchReturn(samples); }
    }

    private static int BCurveSweepCore(CurveTag curve, PK_VECTOR_s path, SurfTag* surface)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (surface == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        var direction = Vector(path.coord[0], path.coord[1], path.coord[2]);
        if (!ProfileFinite(in direction) || ProfileDot(direction, direction) <= 1e-28) return ParasolidConstants.PK_ERROR_bad_parameter;
        return CreateFreeformRevolvedOrSwept(curve, false, default, direction, 1, surface);
    }
    private static int BCurveSpinCore(CurveTag curve, PK_AXIS1_sf_s* axis, double angle, SurfTag* surface)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (axis == null || surface == null || !double.IsFinite(angle) || angle == 0 || Math.Abs(angle) > Math.Tau)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        var origin = Vector(axis->location.coord[0], axis->location.coord[1], axis->location.coord[2]);
        var direction = Vector(axis->axis.coord[0], axis->axis.coord[1], axis->axis.coord[2]);
        if (!ProfileFinite(in origin) || !ProfileUnit(in direction)) return ParasolidConstants.PK_ERROR_bad_parameter;
        return CreateFreeformRevolvedOrSwept(curve, true, origin, direction, angle, surface);
    }
    private static int CreateFreeformRevolvedOrSwept(CurveTag curve, bool spin, KernelVector3 origin,
        KernelVector3 direction, double extent, SurfTag* surface)
    {
        var slot = GetCurveSlotByTag(curve);
        if (slot < 0 || Curves[slot].Class != CurveClass.BCurve) return ParasolidConstants.PK_ERROR_bad_class;
        ref readonly var data = ref BCurveDataStore[Curves[slot].DataIndex];
        if (data.VertexDim != (data.IsRational != 0 ? 4 : 3) || data.NVertices > FreeformPoleLimit || data.IsPeriodic != 0)
            return ParasolidConstants.PK_ERROR_not_implemented;
        var segments = spin ? (int)Math.Ceiling(Math.Abs(extent) / (Math.PI / 2)) : 1;
        var rows = spin ? 2 * segments + 1 : 2;
        var dim = spin || data.IsRational != 0 ? 4 : 3;
        Span<double> poles = stackalloc double[data.NVertices * rows * dim];
        Span<double> knots = stackalloc double[segments + 1];
        Span<int> mults = stackalloc int[segments + 1];
        var input = data.PolesSpan(); var full = spin && Math.Abs(extent) == Math.Tau;
        for (var r = 0; r <= segments; r++) { knots[r] = (double)r / segments; mults[r] = spin ? r == 0 || r == segments ? 3 : 2 : 2; }
        for (var c = 0; c < data.NVertices; c++)
        {
            var weight = data.IsRational != 0 ? input[c * data.VertexDim + 3] : 1;
            var point = Vector(input[c * data.VertexDim], input[c * data.VertexDim + 1], input[c * data.VertexDim + 2]);
            for (var r = 0; r < rows; r++)
            {
                var index = (c * rows + r) * dim;
                if (full && r == rows - 1) { poles.Slice(c * rows * dim, dim).CopyTo(poles.Slice(index, dim)); continue; }
                var angle = spin ? extent * r / (2 * segments) : 0;
                var pathWeight = spin && (r & 1) != 0 ? Math.Cos(extent / (2 * segments)) : 1;
                KernelVector3 result;
                if (spin)
                {
                    var weightedOrigin = ProfileScale(origin, weight);
                    var center = ProfileAdd(weightedOrigin, ProfileScale(direction, ProfileDot(ProfileSubtract(point, weightedOrigin), direction)));
                    var radial = ProfileSubtract(point, center);
                    result = ProfileAdd(ProfileScale(center, pathWeight), ProfileAdd(ProfileScale(radial, Math.Cos(angle)), ProfileScale(ProfileCross(direction, radial), Math.Sin(angle))));
                }
                else result = ProfileAdd(point, ProfileScale(direction, r * weight));
                poles[index] = result.X; poles[index + 1] = result.Y; poles[index + 2] = result.Z;
                if (dim == 4) poles[index + 3] = weight * pathWeight;
            }
        }
        return CreateFreeformSurface(data.Degree, spin ? 2 : 1, data.NVertices, rows, poles, dim, dim == 4,
            data.KnotsSpan(), data.KnotMultsSpan(), knots, mults, data.IsClosed != 0, full, surface);
    }

    private static int CreateFreeformSurface(SplineDegree uDegree, SplineDegree vDegree, BufferCount uCount, BufferCount vCount,
        ReadOnlySpan<double> poles, BufferCount dimension, bool rational, ReadOnlySpan<double> uKnots, ReadOnlySpan<int> uMults,
        ReadOnlySpan<double> vKnots, ReadOnlySpan<int> vMults, bool uClosed, bool vClosed, SurfTag* surface)
    {
        fixed (double* p = poles, uk = uKnots, vk = vKnots) fixed (int* um = uMults, vm = vMults)
        {
            var definition = new PK_BSURF_sf_s
            {
                u_degree = uDegree, v_degree = vDegree, n_u_vertices = uCount, n_v_vertices = vCount,
                vertex_dim = dimension, is_rational = (byte)(rational ? 1 : 0), vertex = p,
                n_u_knots = uKnots.Length, n_v_knots = vKnots.Length, u_knot = uk, v_knot = vk, u_knot_mult = um, v_knot_mult = vm,
                form = ParasolidConstants.PK_BSURF_form_arbitrary_c, u_knot_type = ParasolidConstants.PK_knot_bezier_ends_c,
                v_knot_type = ParasolidConstants.PK_knot_bezier_ends_c, is_u_closed = (byte)(uClosed ? 1 : 0),
                is_v_closed = (byte)(vClosed ? 1 : 0), self_intersecting = ParasolidConstants.PK_self_intersect_unset_c,
                convexity = ParasolidConstants.PK_convexity_unset_c,
            };
            return BSurfCreateImplementation(&definition, surface);
        }
    }

    private static int BSurfMakeBCurveIsoparamCore(SurfTag surface, double parameter, bool constantU, CurveTag* curve)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (curve == null || !double.IsFinite(parameter)) return ParasolidConstants.PK_ERROR_bad_parameter;
        var slot = GetSurfaceSlotByTag(surface);
        if (slot < 0 || Surfaces[slot].Class != SurfaceClass.BSurface) return ParasolidConstants.PK_ERROR_bad_class;
        ref readonly var data = ref BSurfaceDataStore[Surfaces[slot].DataIndex];
        var count = constantU ? data.NVVertices : data.NUVertices;
        var fixedCount = constantU ? data.NUVertices : data.NVVertices;
        var fixedDegree = constantU ? data.UDegree : data.VDegree;
        if (count > FreeformPoleLimit || fixedDegree > FreeformSplineMath.MaxDegree) return ParasolidConstants.PK_ERROR_not_implemented;
        var view = GetBSurfaceView(in data);
        Span<double> basis = stackalloc double[FreeformSplineMath.MaxDegree + 1];
        if (!FreeformSplineMath.Basis(constantU ? view.UKnots : view.VKnots, fixedCount, fixedDegree, parameter, basis, out var first))
            return ParasolidConstants.PK_ERROR_bad_parameter;
        Span<double> poles = stackalloc double[count * data.VertexDim];
        var input = data.PolesSpan();
        var pivot=0;for(var j=1;j<=fixedDegree;j++)if(basis[j]>basis[pivot])pivot=j;
        for (var i = 0; i < count; i++)
        {
            var anchor=(constantU?(first+pivot)*data.NVVertices+i:i*data.NVVertices+first+pivot)*data.VertexDim;
            for(var d=0;d<data.VertexDim;d++)poles[i*data.VertexDim+d]=input[anchor+d];
            // Affine accumulation preserves constant homogeneous coordinates:
            // rounded basis sums must not detach an isocurve from a boundary.
            for (var j = 0; j <= fixedDegree; j++)
            {
                if(j==pivot)continue;
                var index = (constantU ? (first + j) * data.NVVertices + i : i * data.NVVertices + first + j) * data.VertexDim;
                for (var d = 0; d < data.VertexDim; d++) poles[i * data.VertexDim + d] += basis[j] * (input[index + d]-input[anchor+d]);
            }
        }
        return CreateFreeformCurve(constantU ? data.VDegree : data.UDegree, poles, data.VertexDim, data.IsRational != 0,
            constantU ? data.VKnotsSpan() : data.UKnotsSpan(), constantU ? data.VKnotMultsSpan() : data.UKnotMultsSpan(),
            (constantU ? data.IsVClosed : data.IsUClosed) != 0, curve);
    }

    public static int SurfMakeSheetBody(SurfTag surface, PK_UVBOX_s box, BodyTag* body)
    {
        if (body != null) *body = 0;
        if (Dispatcher.IsExecuting) return SurfMakeSheetBodyCore(surface, box, body);
        var command = new FreeformSheetCommand { Surface = surface, Box = box, Body = body };
        return Dispatch(ApiId.SurfMakeSheetBody, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, surface);
    }
    private struct FreeformSheetCommand : IKernelCommand
    {
        internal SurfTag Surface;
        internal PK_UVBOX_s Box;
        internal BodyTag* Body;
        public int Execute()
        {
            var owns = EnsureCommandScratch();
            try { var error = SurfMakeSheetBodyCore(Surface, Box, Body); if (error != 0 && Body != null) *Body = 0; return error; }
            finally { if (owns) ReleaseCommandScratch(State.Session); }
        }
    }
    private static int SurfMakeSheetBodyCore(SurfTag surface, PK_UVBOX_s box, BodyTag* body)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (body == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        var slot = GetSurfaceSlotByTag(surface); if (slot < 0) return ParasolidConstants.PK_ERROR_bad_class;
        var u0 = box.param[0]; var u1 = box.param[2]; var v0 = box.param[1]; var v1 = box.param[3];
        if (!double.IsFinite(u0) || !double.IsFinite(u1) || !double.IsFinite(v0) || !double.IsFinite(v1) || !(u1 > u0) || !(v1 > v0))
            return ParasolidConstants.PK_ERROR_bad_parameter;
        var support = Surfaces[slot];
        if (support.Class is SurfaceClass.BSurface or SurfaceClass.BlendSurface)
        {
            if (u0 < support.UMin || u1 > support.UMax || v0 < support.VMin || v1 > support.VMax) return ParasolidConstants.PK_ERROR_bad_uvbox;
        }
        else if (support.Class is not (SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Cone or SurfaceClass.Sphere or SurfaceClass.Torus))
            return ParasolidConstants.PK_ERROR_not_implemented;
        if (support.Class is SurfaceClass.Cylinder or SurfaceClass.Cone or SurfaceClass.Sphere or SurfaceClass.Torus
            && u1 - u0 >= Math.Tau - 1e-12 || support.Class == SurfaceClass.Torus && v1 - v0 >= Math.Tau - 1e-12
            || support.Class == SurfaceClass.Sphere && (v0 <= -Math.PI / 2 || v1 >= Math.PI / 2))
            return ParasolidConstants.PK_ERROR_not_implemented;
        Span<KernelVector3> points = stackalloc KernelVector3[4];
        Span<ModelingEdge> edges = stackalloc ModelingEdge[4];
        Span<ModelingCoedge> coedges = stackalloc ModelingCoedge[4];
        Span<ModelingLoop> loops = stackalloc ModelingLoop[1];
        Span<ModelingFace> faces = stackalloc ModelingFace[1];
        Span<KernelVector3> jet = stackalloc KernelVector3[4]; SurfaceDerivativeLayout.TryCreate(1, 1, out var layout);
        // A free independent surface becomes defining geometry of the first
        // body. Geometry already used by another owner must remain with it.
        SurfTag attached = surface;
        if (support.OwnerCount != 0 || support.GeometricOwnerCount != 0 || GetConstructionBody(surface) >= 0)
        {
            var cloneError = TryCloneEntityCore(surface, out attached); if (cloneError != 0) return cloneError;
        }
        else if (!SnapshotGeometry(PoolKind.Surface, slot)) return ParasolidConstants.PK_ERROR_memory_full;
        for (var i = 0; i < 4; i++)
        {
            var u = i is 1 or 2 ? u1 : u0; var v = i >= 2 ? v1 : v0;
            var status = EvaluateSurfaceCore(in support, u, v, in layout, jet, 0);
            if (status != AlgorithmStatus.Success) return EvaluationError(status);
            var normal = ProfileCross(jet[layout.GetIndex(1, 0)], jet[layout.GetIndex(0, 1)]);
            if (!ProfileNormalize(ref normal)) return ParasolidConstants.PK_ERROR_bad_parameter;
            points[i] = jet[0];
        }
        for (var i = 0; i < 4; i++)
        {
            var constantU = (i & 1) != 0; var fixedValue = i == 0 ? v0 : i == 1 ? u1 : i == 2 ? v1 : u0;
            var start = constantU ? v0 : u0; var end = constantU ? v1 : u1;
            var error = FreeformIsoparam(attached, constantU, fixedValue, start, end, out var curve, out var a, out var b);
            if (error != 0) return error;
            Curves[GetCurveSlotByTag(curve)].Sense = i < 2 ? ParasolidConstants.PK_TOPOL_sense_positive_c : ParasolidConstants.PK_TOPOL_sense_negative_c;
            edges[i] = new ModelingEdge { Curve = curve, StartVertex = i, EndVertex = (i + 1) % 4, Start = a, End = b };
            coedges[i] = new ModelingCoedge { Edge = i, Forward = 1 };
        }
        loops[0] = new ModelingLoop { FirstCoedge = 0, CoedgeCount = 4 };
        faces[0] = new ModelingFace { Surface = attached, Sense = ParasolidConstants.PK_TOPOL_sense_positive_c, FirstLoop = 0, LoopCount = 1 };
        var finish = BuildModelingBrep(points, edges, coedges, loops, faces, false, out var result);
        if (finish == 0) finish = UnshareSheetDefiningGeometry(attached);
        if (finish == 0) finish = TryAdoptDependentGeometry(attached);
        if (finish == 0) *body = result; return finish;
    }

    private static int FreeformIsoparam(SurfTag surface, bool constantU, double fixedValue, double start, double end,
        out CurveTag curve, out double a, out double b)
    {
        curve = 0; a = start; b = end;
        ref readonly var support = ref Surfaces[GetSurfaceSlotByTag(surface)];
        if(support.Class==SurfaceClass.BlendSurface)
            return NestedBlendIsoparam(surface,constantU,fixedValue,start,end,out curve,out a,out b);
        if (support.Class == SurfaceClass.BSurface)
        {
            CurveTag product = 0; var error = BSurfMakeBCurveIsoparamCore(surface, fixedValue, constantU, &product);
            curve = product; return error;
        }
        if (support.Class is not (SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Cone or SurfaceClass.Sphere or SurfaceClass.Torus))
            return ParasolidConstants.PK_ERROR_not_implemented;
        SurfaceDerivativeLayout.TryCreate(constantU ? 0 : 2, constantU ? 2 : 0, out var layout);
        Span<KernelVector3> values = stackalloc KernelVector3[3];
        var status = EvaluateSurfaceCore(in support, constantU ? fixedValue : start, constantU ? start : fixedValue, in layout, values, 0);
        if (status != AlgorithmStatus.Success) return EvaluationError(status);
        var tangent = values[1];
        if (!ProfileNormalize(ref tangent)) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (support.Class == SurfaceClass.Plane || constantU && support.Class is SurfaceClass.Cylinder or SurfaceClass.Cone)
        {
            var line = new PK_LINE_sf_s(); WriteVector(&line.basis_set.location, in values[0]); WriteVector(&line.basis_set.axis, in tangent);
            CurveTag tag = 0; var error = LineCreateImplementation(&line, &tag); curve = tag;
            a = 0; b = (end - start) * Math.Sqrt(ProfileDot(values[1], values[1])); return error;
        }
        var center = ProfileAdd(values[0], values[2]); var radial = ProfileScale(values[2], -1);
        var radius = Math.Sqrt(ProfileDot(radial, radial)); if (!(radius > 1e-12)) return ParasolidConstants.PK_ERROR_bad_parameter;
        radial = ProfileScale(radial, 1 / radius); var axis = ProfileCross(radial, tangent);
        if (!ProfileNormalize(ref axis)) return ParasolidConstants.PK_ERROR_bad_parameter;
        curve = CreateCircleCurveTag(center.X, center.Y, center.Z, axis.X, axis.Y, axis.Z, radial.X, radial.Y, radial.Z, radius);
        a = 0; b = end - start; return curve > 0 ? 0 : ParasolidConstants.PK_ERROR_memory_full;
    }
}

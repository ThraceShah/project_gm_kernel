using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Construction;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BSurfCreateSplinewise(PK_BSURF_splinewise_sf_s* definition, SurfTag* surface)
    {
        if (surface != null) *surface = 0;
        if (Dispatcher.IsExecuting) return BSurfCreateSplinewiseCore(definition, surface);
        var command = new FreeformGridCommand { Definition = definition, Surface = surface };
        return Dispatch(ApiId.BSurfCreateSplinewise, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }
    private struct FreeformGridCommand : IKernelCommand
    {
        internal PK_BSURF_splinewise_sf_s* Definition;
        internal SurfTag* Surface;
        public int Execute()
        {
            var owns = EnsureCommandScratch();
            try { var error = BSurfCreateSplinewiseCore(Definition, Surface); if (error != 0 && Surface != null) *Surface = 0; return error; }
            finally { if (owns) ReleaseCommandScratch(State.Session); }
        }
    }
    private static int BSurfCreateSplinewiseCore(PK_BSURF_splinewise_sf_s* definition, SurfTag* surface)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (definition == null || surface == null || definition->positions == null || definition->n_u_positions < 2 || definition->n_v_positions < 2)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (definition->u_degree is not (1 or 3) || definition->v_degree is not (1 or 3)
            || definition->n_u_positions > 32 || definition->n_v_positions > 32
            || definition->u_end != ParasolidConstants.PK_PARAM_end_natural_c || definition->U_end != ParasolidConstants.PK_PARAM_end_natural_c
            || definition->v_end != ParasolidConstants.PK_PARAM_end_natural_c || definition->V_end != ParasolidConstants.PK_PARAM_end_natural_c
            || definition->u_how_knot is not (ParasolidConstants.PK_PARAM_knot_auto_c or ParasolidConstants.PK_PARAM_knot_defined_c)
            || definition->v_how_knot is not (ParasolidConstants.PK_PARAM_knot_auto_c or ParasolidConstants.PK_PARAM_knot_defined_c)
            || definition->u_v != ParasolidConstants.PK_PARAM_twist_no_c || definition->u_V != ParasolidConstants.PK_PARAM_twist_no_c
            || definition->U_v != ParasolidConstants.PK_PARAM_twist_no_c || definition->U_V != ParasolidConstants.PK_PARAM_twist_no_c)
            return ParasolidConstants.PK_ERROR_not_implemented;
        var nu = definition->n_u_positions; var nv = definition->n_v_positions;
        var ud = definition->u_degree; var vd = definition->v_degree;
        var cu = ud * (nu - 1) + 1; var cv = vd * (nv - 1) + 1;
        if (cu * cv > 8192) return ParasolidConstants.PK_ERROR_not_implemented;
        Span<double> uParameters = stackalloc double[nu]; Span<double> vParameters = stackalloc double[nv];
        Span<int> uMults = stackalloc int[nu]; Span<int> vMults = stackalloc int[nv];
        var grid = new ReadOnlySpan<PK_VECTOR_s>(definition->positions, nu * nv);
        for (var i = 0; i < grid.Length; i++)
        {
            var point = grid[i];
            for (var d = 0; d < 3; d++) if (!double.IsFinite(point.coord[d])) return ParasolidConstants.PK_ERROR_bad_vertex;
        }
        if (!FreeformGridParameters(grid, nu, nv, true, definition->u_how_knot, definition->u_knot, uParameters)
            || !FreeformGridParameters(grid, nu, nv, false, definition->v_how_knot, definition->v_knot, vParameters))
            return ParasolidConstants.PK_ERROR_bad_knots;
        for (var i = 0; i < nu; i++) uMults[i] = i == 0 || i == nu - 1 ? ud + 1 : ud;
        for (var j = 0; j < nv; j++) vMults[j] = j == 0 || j == nv - 1 ? vd + 1 : vd;
        var intermediate = CommandScratchBorrow(cu * nv * 3); if (intermediate.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            Span<double> positions = stackalloc double[Math.Max(nu, nv) * 3];
            Span<double> column = stackalloc double[Math.Max(cu, cv) * 3];
            Span<double> diagonal = stackalloc double[Math.Max(nu, nv)]; Span<double> seconds = stackalloc double[positions.Length];
            for (var j = 0; j < nv; j++)
            {
                for (var i = 0; i < nu; i++) for (var d = 0; d < 3; d++)
                {
                    var value = definition->positions[j * nu + i].coord[d];
                    if (!double.IsFinite(value)) return ParasolidConstants.PK_ERROR_bad_vertex; positions[3 * i + d] = value;
                }
                if (ud == 1) positions[..(nu * 3)].CopyTo(column);
                else if (!FreeformSplineMath.Interpolate(uParameters, positions[..(nu * 3)], 3, default, default, column, diagonal, seconds))
                    return ParasolidConstants.PK_ERROR_bad_parameter;
                for (var i = 0; i < cu; i++) column.Slice(3 * i, 3).CopyTo(intermediate.Slice((i * nv + j) * 3, 3));
            }
            Span<double> poles = stackalloc double[cu * cv * 3];
            for (var i = 0; i < cu; i++)
            {
                intermediate.Slice(i * nv * 3, nv * 3).CopyTo(positions);
                if (vd == 1) positions[..(nv * 3)].CopyTo(poles.Slice(i * cv * 3, nv * 3));
                else if (!FreeformSplineMath.Interpolate(vParameters, positions[..(nv * 3)], 3, default, default, poles.Slice(i * cv * 3, cv * 3), diagonal, seconds))
                    return ParasolidConstants.PK_ERROR_bad_parameter;
            }
            return CreateFreeformSurface(ud, vd, cu, cv, poles, 3, false, uParameters, uMults, vParameters, vMults, false, false, surface);
        }
        finally { CommandScratchReturn(intermediate); }
    }

    private static bool FreeformGridParameters(ReadOnlySpan<PK_VECTOR_s> positions, BufferCount nu, BufferCount nv,
        bool alongU, int mode, double* supplied, Span<double> parameters)
    {
        if (mode == ParasolidConstants.PK_PARAM_knot_defined_c)
        {
            if (supplied == null) return false;
            for (var i = 0; i < parameters.Length; i++)
            {
                var value = supplied[i];
                if (!double.IsFinite(value) || i > 0 && value <= parameters[i - 1]) return false;
                parameters[i] = value;
            }
            return true;
        }
        parameters[0] = 0; double total = 0;
        // The splinewise contract lays out rows at constant v. Automatic knots
        // are normalized accumulated chord lengths averaged over all parallel rows.
        for (var i = 1; i < parameters.Length; i++)
        {
            double chordSum = 0; var across = alongU ? nv : nu;
            for (var j = 0; j < across; j++)
            {
                var a = positions[alongU ? j * nu + i - 1 : (i - 1) * nu + j];
                var b = positions[alongU ? j * nu + i : i * nu + j];
                var x = b.coord[0] - a.coord[0]; var y = b.coord[1] - a.coord[1]; var z = b.coord[2] - a.coord[2];
                var scale = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
                if (!double.IsFinite(scale)) return false;
                if (scale > 0) { x /= scale; y /= scale; z /= scale; chordSum += scale * Math.Sqrt(x * x + y * y + z * z); }
            }
            if (!(chordSum > 0) || !double.IsFinite(chordSum)) return false;
            total += chordSum; parameters[i] = total;
        }
        if (!double.IsFinite(total)) return false;
        for (var i = 1; i < parameters.Length; i++)
        {
            parameters[i] /= total;
            if (!(parameters[i] > parameters[i - 1])) return false;
        }
        return true;
    }
}

using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Xt;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal static AlgorithmStatus TryMaterializeICurveSupportFromXt(XtDocument document,
        XtNodeIndex index, out SurfTag tag)
    {
        var node = FindNodeByIndex(document.Nodes, index);
        if (node is not null && node.Type == (int)XtNodeTypes.BSurface)
            return MaterializeICurveBSurface(document, node, out tag);
        return TryMaterializeAnalyticSurfaceFromXt(document, index, out tag);
    }

    private static AlgorithmStatus MaterializeICurveBSurface(XtDocument document,
        XtNode node, out SurfTag tag)
    {
        tag = 0;
        if (node.Fields.Length != 9 || node.Fields[7].Kind != XtFieldKind.Pointer
            || node.Fields[8].Kind != XtFieldKind.Pointer)
            return AlgorithmStatus.InvalidInput;
        var nurbs = FindNodeByIndex(document.Nodes, node.Fields[7].Pointer);
        if (nurbs is null || nurbs.Type != (int)XtNodeTypes.NurbsSurf || nurbs.Fields.Length != 20)
            return AlgorithmStatus.InvalidInput;

        ReadOnlySpan<XtFieldKind> kinds =
        [
            XtFieldKind.Logical, XtFieldKind.Logical, XtFieldKind.Integer, XtFieldKind.Integer,
            XtFieldKind.Integer, XtFieldKind.Integer, XtFieldKind.Unsigned, XtFieldKind.Unsigned,
            XtFieldKind.Integer, XtFieldKind.Integer, XtFieldKind.Logical, XtFieldKind.Logical,
            XtFieldKind.Logical, XtFieldKind.Unsigned, XtFieldKind.Integer,
        ];
        Span<int> values = stackalloc int[15];
        for (BufferOffset i = 0; i < values.Length; i++)
        {
            var field = nurbs.Fields[i];
            if (field.Kind != kinds[i] || field.Integer < 0 || field.Integer > int.MaxValue
                || (kinds[i] == XtFieldKind.Logical && field.Integer > 1))
                return AlgorithmStatus.InvalidInput;
            values[i] = (int)field.Integer;
        }
        if (values[2] < 1 || values[3] < 1 || values[4] <= values[2] || values[5] <= values[3]
            || values[8] < 2 || values[9] < 2 || values[14] != (values[10] != 0 ? 4 : 3)
            || values[6] is < 1 or > 6 || values[7] is < 1 or > 6
            || values[13] is < 1 or > 11)
            return AlgorithmStatus.InvalidInput;
        var poleCount = (long)values[4] * values[5];
        if (poleCount > int.MaxValue / values[14]) return AlgorithmStatus.InvalidInput;
        var scalarCount = (BufferCount)(poleCount * values[14]);
        for (BufferOffset i = 15; i < 20; i++)
            if (nurbs.Fields[i].Kind != XtFieldKind.Pointer) return AlgorithmStatus.InvalidInput;
        var poles = FindNodeByIndex(document.Nodes, nurbs.Fields[15].Pointer);
        var uMults = FindNodeByIndex(document.Nodes, nurbs.Fields[16].Pointer);
        var vMults = FindNodeByIndex(document.Nodes, nurbs.Fields[17].Pointer);
        var uKnots = FindNodeByIndex(document.Nodes, nurbs.Fields[18].Pointer);
        var vKnots = FindNodeByIndex(document.Nodes, nurbs.Fields[19].Pointer);
        if (!IsBSurfaceArray(poles, XtNodeTypes.BSplineVertices, scalarCount)
            || !IsBSurfaceArray(uMults, XtNodeTypes.KnotMultiplicities, values[8])
            || !IsBSurfaceArray(vMults, XtNodeTypes.KnotMultiplicities, values[9])
            || !IsBSurfaceArray(uKnots, XtNodeTypes.KnotSet, values[8])
            || !IsBSurfaceArray(vKnots, XtNodeTypes.KnotSet, values[9]))
            return AlgorithmStatus.InvalidInput;

        var selfIntersecting = ParasolidConstants.PK_self_intersect_unset_c;
        if (node.Fields[8].Pointer != 0)
        {
            var surfaceData = FindNodeByIndex(document.Nodes, node.Fields[8].Pointer);
            if (surfaceData is null || surfaceData.Type != (int)XtNodeTypes.SurfaceData || surfaceData.Fields.Length < 9)
                return AlgorithmStatus.InvalidInput;
            // A repeated original box records no actual extension. Distinct
            // extended boxes need their own support-domain model.
            if (!IsUnextendedBSurfaceInterval(surfaceData.Fields[0], surfaceData.Fields[2])
                || !IsUnextendedBSurfaceInterval(surfaceData.Fields[1], surfaceData.Fields[3]))
                return AlgorithmStatus.Unsupported;
            var self = surfaceData.Fields[4];
            if (self.Kind == XtFieldKind.Unsigned && self.Integer == 4)
                return AlgorithmStatus.Unsupported; // checked_ok_in_old_version has no PK equivalent
            if (self.Kind != XtFieldKind.Unsigned || self.Integer < 1 || self.Integer > 3)
                return AlgorithmStatus.InvalidInput;
            selfIntersecting += (int)self.Integer - 1;
        }

        var knotCount = (long)values[8] + values[9];
        var workspaceCount = scalarCount + knotCount + (knotCount + 1) / 2;
        if (workspaceCount > int.MaxValue) return AlgorithmStatus.WorkspaceTooSmall;
        var ownsScratch = EnsureCommandScratch();
        var checkpoint = CommandScratch.ThreadArenaOffset;
        try
        {
            var scratch = CommandScratch.Current.Take((BufferCount)workspaceCount);
            if (scratch.Length < workspaceCount) return AlgorithmStatus.WorkspaceTooSmall;
            var vertexValues = scratch[..scalarCount];
            var uKnotValues = scratch.Slice(scalarCount, values[8]);
            var vKnotValues = scratch.Slice(scalarCount + values[8], values[9]);
            var multValues = MemoryMarshal.Cast<double, int>(scratch[(scalarCount + (BufferCount)knotCount)..]);
            var uMultValues = multValues[..values[8]];
            var vMultValues = multValues.Slice(values[8], values[9]);
            if (!ReadBSurfaceReals(poles!, vertexValues) || !ReadBSurfaceReals(uKnots!, uKnotValues)
                || !ReadBSurfaceReals(vKnots!, vKnotValues) || !ReadBSurfaceMultiplicities(uMults!, uMultValues)
                || !ReadBSurfaceMultiplicities(vMults!, vMultValues))
                return AlgorithmStatus.InvalidInput;

            fixed (double* vertex = vertexValues, uKnot = uKnotValues, vKnot = vKnotValues)
            fixed (int* uMult = uMultValues, vMult = vMultValues)
            {
                var definition = new PK_BSURF_sf_s
                {
                    u_degree = values[2], v_degree = values[3], n_u_vertices = values[4], n_v_vertices = values[5],
                    vertex_dim = values[14], is_rational = (byte)values[10], vertex = vertex,
                    form = BSurfaceFormFromXt(values[13]),
                    n_u_knots = values[8], n_v_knots = values[9],
                    u_knot_mult = uMult, v_knot_mult = vMult, u_knot = uKnot, v_knot = vKnot,
                    u_knot_type = BSurfaceKnotTypeFromXt(values[6]),
                    v_knot_type = BSurfaceKnotTypeFromXt(values[7]),
                    is_u_periodic = (byte)values[0], is_v_periodic = (byte)values[1],
                    is_u_closed = (byte)values[11], is_v_closed = (byte)values[12],
                    self_intersecting = selfIntersecting, convexity = ParasolidConstants.PK_convexity_unset_c,
                };
                SurfTag created = 0;
                var error = BSurfCreate(&definition, &created);
                if (error == ParasolidConstants.PK_ERROR_memory_full) return AlgorithmStatus.WorkspaceTooSmall;
                if (error == ParasolidConstants.PK_ERROR_not_implemented) return AlgorithmStatus.Unsupported;
                if (error != ParasolidConstants.PK_ERROR_no_errors) return AlgorithmStatus.InvalidInput;
                tag = created;
                ApplySurfaceSense(node, tag);
                return AlgorithmStatus.Success;
            }
        }
        finally
        {
            CommandScratch.ThreadArenaOffset = checkpoint;
            if (ownsScratch) ReleaseCommandScratch(State.Session);
        }
    }

    private static bool IsBSurfaceArray(XtNode? node, XtNodeTypes type, BufferCount count)
        => node is not null && node.Type == (int)type && node.VariableLength == count && node.Fields.Length == count;

    private static bool IsUnextendedBSurfaceInterval(XtFieldValue original, XtFieldValue extended)
    {
        if (extended.Kind == XtFieldKind.Empty) return true;
        return original.Kind == XtFieldKind.Interval && extended.Kind == XtFieldKind.Interval
            && double.IsFinite(original.Vector.X) && double.IsFinite(original.Vector.Y)
            && original.Vector.X <= original.Vector.Y
            && original.Vector.X == extended.Vector.X && original.Vector.Y == extended.Vector.Y;
    }

    // SCH_knot_type_t and SCH_surface_form_t are one-based enums; their
    // numeric ordering is not the PK enum ordering (notably general cones).
    private static KernelKnotType BSurfaceKnotTypeFromXt(int value) => value switch
    {
        1 => ParasolidConstants.PK_knot_unset_c,
        2 => ParasolidConstants.PK_knot_non_uniform_c,
        3 => ParasolidConstants.PK_knot_uniform_c,
        4 => ParasolidConstants.PK_knot_quasi_uniform_c,
        5 => ParasolidConstants.PK_knot_piecewise_bezier_c,
        6 => ParasolidConstants.PK_knot_bezier_ends_c,
        _ => ParasolidConstants.PK_knot_unset_c,
    };

    private static KernelBSurfaceForm BSurfaceFormFromXt(int value) => value switch
    {
        1 => ParasolidConstants.PK_BSURF_form_unset_c,
        2 => ParasolidConstants.PK_BSURF_form_arbitrary_c,
        3 => ParasolidConstants.PK_BSURF_form_planar_c,
        4 => ParasolidConstants.PK_BSURF_form_cylindrical_c,
        5 => ParasolidConstants.PK_BSURF_form_conical_c,
        6 => ParasolidConstants.PK_BSURF_form_spherical_c,
        7 => ParasolidConstants.PK_BSURF_form_toroidal_c,
        8 => ParasolidConstants.PK_BSURF_form_revolved_c,
        9 => ParasolidConstants.PK_BSURF_form_ruled_c,
        10 => ParasolidConstants.PK_BSURF_form_quadric_c,
        11 => ParasolidConstants.PK_BSURF_form_swept_c,
        _ => ParasolidConstants.PK_BSURF_form_unset_c,
    };

    internal static long BSurfaceKnotTypeToXt(KernelKnotType value) => value switch
    {
        ParasolidConstants.PK_knot_non_uniform_c => 2,
        ParasolidConstants.PK_knot_uniform_c => 3,
        ParasolidConstants.PK_knot_quasi_uniform_c => 4,
        ParasolidConstants.PK_knot_piecewise_bezier_c => 5,
        ParasolidConstants.PK_knot_bezier_ends_c => 6,
        // PK smooth_seam has no SCH_knot_type_t counterpart. Preserve the
        // defining knots/multiplicities and leave this classification unset.
        _ => 1,
    };

    internal static long BSurfaceFormToXt(KernelBSurfaceForm value) => value switch
    {
        ParasolidConstants.PK_BSURF_form_arbitrary_c => 2,
        ParasolidConstants.PK_BSURF_form_planar_c => 3,
        ParasolidConstants.PK_BSURF_form_cylindrical_c => 4,
        ParasolidConstants.PK_BSURF_form_conical_c => 5,
        ParasolidConstants.PK_BSURF_form_spherical_c => 6,
        ParasolidConstants.PK_BSURF_form_toroidal_c => 7,
        ParasolidConstants.PK_BSURF_form_revolved_c => 8,
        ParasolidConstants.PK_BSURF_form_ruled_c => 9,
        ParasolidConstants.PK_BSURF_form_quadric_c => 10,
        ParasolidConstants.PK_BSURF_form_swept_c => 11,
        // PK gen_cone is not SCH_surface_form_t.ruled. The rational spline
        // definition is retained without inventing a different classification.
        _ => 1,
    };

    private static bool ReadBSurfaceReals(XtNode node, Span<double> destination)
    {
        for (BufferOffset i = 0; i < destination.Length; i++)
        {
            var field = node.Fields[i];
            if (field.Kind != XtFieldKind.Real || !double.IsFinite(field.Real)) return false;
            destination[i] = field.Real;
        }
        return true;
    }

    private static bool ReadBSurfaceMultiplicities(XtNode node, Span<int> destination)
    {
        for (BufferOffset i = 0; i < destination.Length; i++)
        {
            var field = node.Fields[i];
            if (field.Kind != XtFieldKind.Integer || field.Integer <= 0 || field.Integer > int.MaxValue) return false;
            destination[i] = (int)field.Integer;
        }
        return true;
    }
}

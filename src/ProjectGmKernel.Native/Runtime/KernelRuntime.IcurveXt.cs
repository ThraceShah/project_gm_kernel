using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Xt;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// XT → icurve helpers (spec §21.6 / T19): CHART extract and full INTERSECTION
/// materialization (analytic supports + LIMIT + INTERSECTION_DATA → DecodeIcurve
/// → bind). B-surface / offset / swept supports stay Unsupported.
/// </summary>
internal static unsafe partial class KernelRuntime
{
    private const int MaxIcurveImportHvecs = 128;
    private const int MaxIcurveImportUvValues = 512;

    /// <summary>
    /// Read CHART (node 40) hull vectors and summary fields from an XT document
    /// into a caller-owned buffer (3 doubles per hull vector). Returns false when
    /// no CHART is present, the optional-field prefix is not the common 7-slot
    /// Parasolid layout, or <paramref name="chartHvecs"/> is too small.
    /// </summary>
    internal static bool TryExtractIcurveChartFromXt(XtDocument document,
        Span<double> chartHvecs, out int chartCount,
        out double baseParameter, out double baseScale,
        out double chordalError, out double angularError)
    {
        chartCount = 0;
        baseParameter = 0;
        baseScale = 1;
        chordalError = 0;
        angularError = 0;
        if (document.Nodes is null) return false;

        XtNode? chartNode = null;
        foreach (var node in document.Nodes)
        {
            if (node.Type == (int)XtNodeTypes.Chart)
            {
                chartNode = node;
                break;
            }
        }
        if (chartNode is null) return false;
        return TryReadChartNode(chartNode, chartHvecs, out chartCount,
            out baseParameter, out baseScale, out chordalError, out angularError);
    }

    /// <summary>
    /// Materialize one INTERSECTION node into a bound <see cref="CurveClass.ICurve"/>
    /// entity. Analytic support surfaces (plane/cylinder/cone/sphere/torus) are
    /// created from the referenced XT nodes; other support classes return
    /// <see cref="AlgorithmStatus.Unsupported"/>.
    /// </summary>
    internal static AlgorithmStatus TryMaterializeICurveFromXt(XtDocument document,
        XtNodeIndex intersectionIndex, out CurveTag tag, out IcurveDecodeFailure failure)
    {
        tag = 0;
        failure = IcurveDecodeFailure.None;
        if (!IsSessionStarted || document.Nodes is null)
            return AlgorithmStatus.InvalidInput;

        var intersection = FindNodeByIndex(document.Nodes, intersectionIndex);
        if (intersection is null || intersection.Type != (int)XtNodeTypes.Intersection)
            return AlgorithmStatus.InvalidInput;
        if (intersection.Fields.Length is not (13 or 14))
            return AlgorithmStatus.Unsupported;

        var surf0Index = intersection.Fields[7].Pointer;
        var surf1Index = intersection.Fields[8].Pointer;
        var chartIndex = intersection.Fields[9].Pointer;
        var startIndex = intersection.Fields[10].Pointer;
        var endIndex = intersection.Fields[11].Pointer;
        var hasScale = intersection.Fields.Length == 14;
        var dataIndex = hasScale ? intersection.Fields[13].Pointer : intersection.Fields[12].Pointer;
        var scale = hasScale && intersection.Fields[12].Kind == XtFieldKind.Real
            ? intersection.Fields[12].Real : 0.0;
        var scaleProvided = hasScale && intersection.Fields[12].Kind == XtFieldKind.Real
            ? (KernelLogical)1 : (KernelLogical)0;

        if (TryMaterializeAnalyticSurfaceFromXt(document, surf0Index, out var surf0)
            != AlgorithmStatus.Success
            || TryMaterializeAnalyticSurfaceFromXt(document, surf1Index, out var surf1)
            != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;

        var chartNode = FindNodeByIndex(document.Nodes, chartIndex);
        var startNode = FindNodeByIndex(document.Nodes, startIndex);
        var endNode = FindNodeByIndex(document.Nodes, endIndex);
        if (chartNode is null || startNode is null || endNode is null)
            return AlgorithmStatus.InvalidInput;

        Span<double> chartBuf = stackalloc double[MaxIcurveImportHvecs * 3];
        if (!TryReadChartNode(chartNode, chartBuf, out var chartCount,
                out var baseParameter, out var baseScale, out var chordal, out var angular))
            return AlgorithmStatus.Unsupported;

        Span<double> startBuf = stackalloc double[6];
        Span<double> endBuf = stackalloc double[6];
        if (!TryReadLimitNode(startNode, startBuf, out var startType, out var startTerm, out var startCount)
            || !TryReadLimitNode(endNode, endBuf, out var endType, out var endTerm, out var endCount))
            return AlgorithmStatus.InvalidInput;

        Span<double> uvBuf = stackalloc double[MaxIcurveImportUvValues];
        var uvType = IntersectionUvType.None;
        var uvCount = 0;
        if (dataIndex != 0)
        {
            var dataNode = FindNodeByIndex(document.Nodes, dataIndex);
            if (dataNode is null || dataNode.Type != (int)XtNodeTypes.IntersectionData)
                return AlgorithmStatus.InvalidInput;
            if (!TryReadIntersectionDataNode(dataNode, uvBuf, out uvType, out uvCount))
                return AlgorithmStatus.Unsupported;
        }

        var input = new IcurveDecodeInput
        {
            Surface0Tag = surf0,
            Surface1Tag = surf1,
            BaseParameter = baseParameter,
            BaseScale = baseScale,
            ChartCount = chartCount,
            ChartHvecs = chartBuf[..(chartCount * 3)],
            Start = new IcurveLimitInput
            {
                Type = startType,
                TermUse = startTerm,
                Hvecs = startBuf[..(startCount * 3)],
            },
            End = new IcurveLimitInput
            {
                Type = endType,
                TermUse = endTerm,
                Hvecs = endBuf[..(endCount * 3)],
            },
            UvType = uvType,
            UvValues = uvCount > 0 ? uvBuf[..uvCount] : ReadOnlySpan<double>.Empty,
            ChordalError = chordal,
            AngularError = angular,
            Scale = scale,
            ScaleProvided = scaleProvided,
            SourceSchema = document.Schema.SchemaNumber,
        };

        var decode = DecodeIcurve(input, out var dataSlot, out failure, out _);
        if (decode != AlgorithmStatus.Success) return decode;
        return TryBindICurveEntity(dataSlot, out tag);
    }

    private static bool TryReadChartNode(XtNode chartNode, Span<double> chartHvecs, out int chartCount,
        out double baseParameter, out double baseScale, out double chordalError, out double angularError)
    {
        chartCount = 0;
        baseParameter = 0;
        baseScale = 1;
        chordalError = 0;
        angularError = 0;
        if (chartNode.Type != (int)XtNodeTypes.Chart || chartNode.VariableLength < 2)
            return false;
        var fields = chartNode.Fields;
        var hvecCount = chartNode.VariableLength;
        var prefix = fields.Length - hvecCount;
        if (prefix != 7) return false;
        if (fields[0].Kind != XtFieldKind.Real || fields[1].Kind != XtFieldKind.Real
            || fields[2].Kind != XtFieldKind.Integer)
            return false;
        if (fields[2].Integer != hvecCount) return false;
        if (fields[3].Kind != XtFieldKind.Real || fields[4].Kind != XtFieldKind.Real)
            return false;
        if (hvecCount > MaxIcurveImportHvecs || chartHvecs.Length < hvecCount * 3)
            return false;

        baseParameter = fields[0].Real;
        baseScale = fields[1].Real;
        chordalError = fields[3].Real;
        angularError = fields[4].Real;
        for (var i = 0; i < hvecCount; i++)
        {
            var v = fields[prefix + i];
            if (v.Kind != XtFieldKind.Vector) return false;
            chartHvecs[i * 3] = v.Vector.X;
            chartHvecs[i * 3 + 1] = v.Vector.Y;
            chartHvecs[i * 3 + 2] = v.Vector.Z;
        }
        chartCount = hvecCount;
        return true;
    }

    private static bool TryReadLimitNode(XtNode limitNode, Span<double> hvecs,
        out LimitType type, out LimitTermUse termUse, out int hvecCount)
    {
        type = LimitType.Help;
        termUse = LimitTermUse.Unset;
        hvecCount = 0;
        if (limitNode.Type != (int)XtNodeTypes.Limit || limitNode.VariableLength < 1)
            return false;
        if (limitNode.Fields.Length < 2 + limitNode.VariableLength) return false;
        if (limitNode.Fields[0].Kind != XtFieldKind.Character) return false;
        type = (LimitType)limitNode.Fields[0].Character;
        if (limitNode.Fields[1].Kind == XtFieldKind.Character)
            termUse = (LimitTermUse)limitNode.Fields[1].Character;
        else if (limitNode.Fields[1].Kind != XtFieldKind.Empty)
            return false;
        hvecCount = limitNode.VariableLength;
        if (hvecs.Length < hvecCount * 3) return false;
        for (var i = 0; i < hvecCount; i++)
        {
            var v = limitNode.Fields[2 + i];
            if (v.Kind != XtFieldKind.Vector) return false;
            hvecs[i * 3] = v.Vector.X;
            hvecs[i * 3 + 1] = v.Vector.Y;
            hvecs[i * 3 + 2] = v.Vector.Z;
        }
        return true;
    }

    private static bool TryReadIntersectionDataNode(XtNode dataNode, Span<double> uvValues,
        out IntersectionUvType uvType, out int uvCount)
    {
        uvType = IntersectionUvType.None;
        uvCount = 0;
        if (dataNode.Fields.Length < 1 || dataNode.Fields[0].Kind != XtFieldKind.Unsigned)
            return false;
        var raw = dataNode.Fields[0].Integer;
        if (raw is < 1 or > 4) return false;
        uvType = (IntersectionUvType)raw;
        uvCount = dataNode.VariableLength;
        if (uvCount < 0 || dataNode.Fields.Length != 1 + uvCount) return false;
        if (uvCount > MaxIcurveImportUvValues || uvValues.Length < uvCount) return false;
        for (var i = 0; i < uvCount; i++)
        {
            var field = dataNode.Fields[1 + i];
            // Parasolid may emit null UV slots; DecodeIcurve rejects NaN, so treat
            // Empty as unsupported for hydrate (caller can retry with UvType.None).
            if (field.Kind == XtFieldKind.Empty) return false;
            if (field.Kind != XtFieldKind.Real) return false;
            uvValues[i] = field.Real;
        }
        return true;
    }

    private static AlgorithmStatus TryMaterializeAnalyticSurfaceFromXt(XtDocument document,
        XtNodeIndex nodeIndex, out SurfTag tag)
    {
        tag = 0;
        var node = FindNodeByIndex(document.Nodes, nodeIndex);
        if (node is null) return AlgorithmStatus.InvalidInput;
        return (XtNodeTypes)node.Type switch
        {
            XtNodeTypes.Plane => MaterializePlane(node, out tag),
            XtNodeTypes.Cylinder => MaterializeCylinder(node, out tag),
            XtNodeTypes.Sphere => MaterializeSphere(node, out tag),
            XtNodeTypes.Cone => MaterializeCone(node, out tag),
            XtNodeTypes.Torus => MaterializeTorus(node, out tag),
            _ => AlgorithmStatus.Unsupported,
        };
    }

    private static AlgorithmStatus MaterializePlane(XtNode node, out SurfTag tag)
    {
        tag = 0;
        if (node.Fields.Length < 10) return AlgorithmStatus.InvalidInput;
        var sf = new PK_PLANE_sf_s();
        FillAxis2(ref sf.basis_set, node.Fields[7].Vector, node.Fields[8].Vector, node.Fields[9].Vector);
        int local = 0;
        var error = PlaneCreate(&sf, &local);
        tag = local;
        return error == 0 ? AlgorithmStatus.Success : AlgorithmStatus.Unsupported;
    }

    private static AlgorithmStatus MaterializeCylinder(XtNode node, out SurfTag tag)
    {
        tag = 0;
        if (node.Fields.Length < 11) return AlgorithmStatus.InvalidInput;
        var sf = new PK_CYL_sf_s();
        FillAxis2(ref sf.basis_set, node.Fields[7].Vector, node.Fields[8].Vector, node.Fields[10].Vector);
        sf.radius = node.Fields[9].Real;
        int local = 0;
        var error = CylCreate(&sf, &local);
        tag = local;
        return error == 0 ? AlgorithmStatus.Success : AlgorithmStatus.Unsupported;
    }

    private static AlgorithmStatus MaterializeSphere(XtNode node, out SurfTag tag)
    {
        tag = 0;
        // XtWriter.SphereNode / XtReader.ReadSphereFields:
        // [7]=centre, [8]=radius, [9]=axis, [10]=ref.
        if (node.Fields.Length < 11) return AlgorithmStatus.InvalidInput;
        if (node.Fields[8].Kind != XtFieldKind.Real) return AlgorithmStatus.InvalidInput;
        var sf = new PK_SPHERE_sf_s();
        FillAxis2(ref sf.basis_set, node.Fields[7].Vector, node.Fields[9].Vector, node.Fields[10].Vector);
        sf.radius = node.Fields[8].Real;
        int local = 0;
        var error = SphereCreate(&sf, &local);
        tag = local;
        return error == 0 ? AlgorithmStatus.Success : AlgorithmStatus.Unsupported;
    }

    private static AlgorithmStatus MaterializeCone(XtNode node, out SurfTag tag)
    {
        tag = 0;
        // XtWriter.ConeNode / XtReader.ReadConeFields:
        // [7]=location, [8]=axis, [9]=radius, [10]=sin(θ), [11]=cos(θ), [12]=ref.
        if (node.Fields.Length < 13) return AlgorithmStatus.InvalidInput;
        if (node.Fields[9].Kind != XtFieldKind.Real
            || node.Fields[10].Kind != XtFieldKind.Real
            || node.Fields[11].Kind != XtFieldKind.Real)
            return AlgorithmStatus.InvalidInput;
        var sf = new PK_CONE_sf_s();
        FillAxis2(ref sf.basis_set, node.Fields[7].Vector, node.Fields[8].Vector, node.Fields[12].Vector);
        sf.radius = node.Fields[9].Real;
        sf.semi_angle = Math.Atan2(node.Fields[10].Real, node.Fields[11].Real);
        int local = 0;
        var error = ConeCreate(&sf, &local);
        tag = local;
        return error == 0 ? AlgorithmStatus.Success : AlgorithmStatus.Unsupported;
    }

    private static AlgorithmStatus MaterializeTorus(XtNode node, out SurfTag tag)
    {
        tag = 0;
        if (node.Fields.Length < 12) return AlgorithmStatus.InvalidInput;
        var sf = new PK_TORUS_sf_s();
        FillAxis2(ref sf.basis_set, node.Fields[7].Vector, node.Fields[8].Vector, node.Fields[11].Vector);
        sf.major_radius = node.Fields[9].Real;
        sf.minor_radius = node.Fields[10].Real;
        int local = 0;
        var error = TorusCreate(&sf, &local);
        tag = local;
        return error == 0 ? AlgorithmStatus.Success : AlgorithmStatus.Unsupported;
    }

    private static void FillAxis2(ref PK_AXIS2_sf_s basis, XtVector location, XtVector axis, XtVector reference)
    {
        basis.location.coord[0] = location.X;
        basis.location.coord[1] = location.Y;
        basis.location.coord[2] = location.Z;
        basis.axis.coord[0] = axis.X;
        basis.axis.coord[1] = axis.Y;
        basis.axis.coord[2] = axis.Z;
        basis.ref_direction.coord[0] = reference.X;
        basis.ref_direction.coord[1] = reference.Y;
        basis.ref_direction.coord[2] = reference.Z;
    }

    private static XtNode? FindNodeByIndex(XtNode[] nodes, XtNodeIndex index)
    {
        if (index == 0) return null;
        for (var i = 0; i < nodes.Length; i++)
            if (nodes[i].Index == index) return nodes[i];
        return null;
    }
}

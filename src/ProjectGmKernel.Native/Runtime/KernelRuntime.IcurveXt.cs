using ProjectGmKernel.Xt;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// XT → icurve chart extract helpers (spec §21.6 / T19). Full INTERSECTION
/// entity materialization (support surfaces + LIMIT + INTERSECTION_DATA) remains
/// a follow-on; this covers the CHART payload layout emitted by Parasolid
/// transmit when optional extended_chart fields are absent.
/// </summary>
internal static unsafe partial class KernelRuntime
{
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
        if (chartNode is null || chartNode.VariableLength < 2) return false;

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
        if (chartHvecs.Length < hvecCount * 3) return false;

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
}

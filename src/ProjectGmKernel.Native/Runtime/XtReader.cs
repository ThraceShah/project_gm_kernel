using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Computation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class XtReader
{
    public static int ReadText(string text, out EntityTag[] parts)
        => ReadText(text, receiveUserFields: true, [], ParasolidConstants.PK_receive_compound_split_c, out parts);

    public static int ReadText(string text, bool receiveUserFields, out EntityTag[] parts)
        => ReadText(text, receiveUserFields, [], ParasolidConstants.PK_receive_compound_split_c, out parts);

    public static int ReadText(
        string text,
        bool receiveUserFields,
        ReadOnlySpan<int> partIndices,
        int receiveCompound,
        out EntityTag[] parts)
    {
        parts = [];
        XtDocument document;
        try
        {
            document = XtText.DecodeDocument(text);
            document = XtText.SelectUserFields(document, receiveUserFields);
            _ = document.SemanticModel;
        }
        catch (FormatException exception)
        {
            if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTICS")=="1")Console.Error.WriteLine(exception);
            return ParasolidConstants.PK_ERROR_corrupt_file;
        }
        catch (NotSupportedException)
        {
            return ParasolidConstants.PK_ERROR_bad_file_format;
        }

        var result = new List<EntityTag>();
        try
        {
            var allRootIndexes = XtPartGraph.GetRootIndexes(document);
            if (partIndices.Length != 0 && partIndices[^1] >= allRootIndexes.Length)
                return ParasolidConstants.PK_ERROR_bad_index;
            XtNodeIndex[] rootIndexes;
            if (partIndices.Length == 0)
            {
                rootIndexes = allRootIndexes;
            }
            else
            {
                rootIndexes = new XtNodeIndex[partIndices.Length];
                for (var index = 0; index < rootIndexes.Length; index++)
                    rootIndexes[index] = allRootIndexes[partIndices[index]];
            }
            var compoundError = XtPartGraph.ApplyCompoundReceiveMode(
                document,
                rootIndexes,
                receiveCompound,
                out document,
                out rootIndexes);
            if (compoundError != ParasolidConstants.PK_ERROR_no_errors)
                return compoundError;
            ProjectGmKernel.Xt.IXtSchemaModel? schemaModel = null;
            if (document.Schema.ModelerVersion / 100000 is >= 30 and <= 38)
            {
                schemaModel = ProjectGmKernel.Xt.XtGeneratedModelCodec.Decode(document);
                document = schemaModel.ToDocument();
            }
            var nodes = document.Nodes;
            foreach (var rootIndex in rootIndexes)
            {
                var root = FindPartNode(document, rootIndex)
                    ?? throw new FormatException("XT part container references a missing part.");
                EntityTag tag = 0;
                var canonicalBody = KernelRuntime.HasCanonicalICurveWireBodyLayout(document, root);
                var wire = canonicalBody
                    && root.Fields[17].Kind == XtFieldKind.Unsigned && root.Fields[17].Integer == 2;
                var wireStatus = wire && HasIntersectionWireEdge(document, root)
                    ? KernelRuntime.TryMaterializeICurveWireBodyFromXt(document, root, out tag)
                    : AlgorithmStatus.Unsupported;
                if (wireStatus == AlgorithmStatus.WorkspaceTooSmall) return ParasolidConstants.PK_ERROR_memory_full;
                if (wireStatus == AlgorithmStatus.InvalidInput) return ParasolidConstants.PK_ERROR_corrupt_file;
                var brepStatus = wireStatus == AlgorithmStatus.Success ? AlgorithmStatus.Success
                    : canonicalBody ? TryMaterializeBrep(document, root, out tag) : AlgorithmStatus.Unsupported;
                if (brepStatus == AlgorithmStatus.WorkspaceTooSmall) return ParasolidConstants.PK_ERROR_memory_full;
                if (brepStatus == AlgorithmStatus.InvalidInput) return ParasolidConstants.PK_ERROR_corrupt_file;
                var error = brepStatus == AlgorithmStatus.Success ? ParasolidConstants.PK_ERROR_no_errors
                    : ParasolidConstants.PK_ERROR_bad_file_format;
                if (error == ParasolidConstants.PK_ERROR_no_errors)
                    KernelRuntime.AttachReceivedXt(tag, document, schemaModel, root.Index, opaque: false);
                else
                    error = KernelRuntime.CreateOpaquePartCore(document, schemaModel, root, out tag);
                if (error != ParasolidConstants.PK_ERROR_no_errors)
                    return error;
                result.Add(tag);
            }
        }
        catch (FormatException exception)
        {
            if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTICS")=="1")Console.Error.WriteLine(exception);
            return ParasolidConstants.PK_ERROR_corrupt_file;
        }

        parts = result.ToArray();
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static XtNode? FindPartNode(XtDocument document, XtNodeIndex index)
    {
        if (index == 0)
            return null;
        for (var i = 0; i < document.Nodes.Length; i++)
        {
            var node = document.Nodes[i];
            if (node.Index != index)
                continue;
            var name = document.Schema.GetNode(node.Type).Name;
            if (name is "ASSEMBLY" or "BODY")
                return node;
        }
        return null;
    }

    private static bool HasIntersectionWireEdge(XtDocument document, XtNode body)
    {
        var edgeIndex = body.Fields[27].Pointer;
        if (edgeIndex == 0 && body.Fields[19].Pointer != 0)
        {
            var shell = document.Nodes.FirstOrDefault(n => n.Index == body.Fields[19].Pointer);
            if (shell is null || shell.Type != (int)XtNodeTypes.Shell || shell.Fields.Length != 9) return false;
            edgeIndex = shell.Fields[5].Pointer;
        }
        var edge = document.Nodes.FirstOrDefault(n => n.Index == edgeIndex);
        if (edge is null || edge.Type != (int)XtNodeTypes.Edge || edge.Fields.Length != 10) return false;
        var curveIndex = edge.Fields[6].Pointer;
        return document.Nodes.Any(n => n.Index == curveIndex && n.Type == (int)XtNodeTypes.Intersection);
    }

}

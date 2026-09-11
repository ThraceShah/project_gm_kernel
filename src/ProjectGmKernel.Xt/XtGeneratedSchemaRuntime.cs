namespace ProjectGmKernel.Xt;

public interface IXtSchemaModel
{
    string SchemaIdentity { get; }
    XtDocument ToDocument();
}

internal static class XtGeneratedSchemaRuntime
{
    internal static void RequireShape(XtSchemaDefinition actual, XtSchemaDefinition expected)
    {
        if(!CompatibleShape(actual,expected))throw Shape(actual,expected);
    }

    internal static bool ShapeEquals(XtSchemaDefinition actual,XtSchemaDefinition expected)
    {
        if(actual.Nodes.Length!=expected.Nodes.Length||actual.Fields.Length!=expected.Fields.Length)return false;
        for(var index=0;index<actual.Nodes.Length;index++)if(actual.Nodes[index]!=expected.Nodes[index])return false;
        for(var index=0;index<actual.Fields.Length;index++)if(actual.Fields[index]!=expected.Fields[index])return false;
        return true;
    }

    internal static bool CompatibleShape(XtSchemaDefinition actual,XtSchemaDefinition expected)
    {
        var embedded=string.Equals(actual.Info.FileName,"<embedded>",StringComparison.Ordinal);
        foreach(var node in actual.Nodes)
        {
            var target=expected.GetNode(node.Type);
            if(target.Type==0||node.Name!=target.Name||node.Transmit!=target.Transmit)return false;
            var actualFields=actual.GetFields(node);var targetFields=expected.GetFields(target);
            if(!embedded)
            {
                if(node.DeclaredFieldCount!=target.DeclaredFieldCount||node.Variable!=target.Variable||!actualFields.SequenceEqual(targetFields))return false;
            }
            else
            {
                var effective=targetFields.ToArray().Where(static field=>field.Transmit||field.ElementCount==1).ToArray();
                if(!actualFields.SequenceEqual(effective))return false;
            }
        }
        return actual.Nodes.Length!=0;
    }

    internal static void ValidateMetadata(
        XtNodeIndex nodeIndex,
        XtTableIndex transmitOrder,
        XtVariableLength variableLength,
        bool variable,
        XtRange userFields,
        int userFieldSize,
        int userFieldPoolLength,
        HashSet<int> indexes,
        HashSet<int> orders)
    {
        if (nodeIndex <= 0 || !indexes.Add(nodeIndex))
            throw new XtFormatException(XtErrorCode.ModelInvalid, $"XT node index {nodeIndex} is invalid or duplicated.");
        if (transmitOrder < 0 || !orders.Add(transmitOrder))
            throw new XtFormatException(XtErrorCode.ModelInvalid, $"XT transmit order {transmitOrder} is invalid or duplicated.");
        if (variable ? variableLength < 0 : variableLength != 0)
            throw new XtFormatException(XtErrorCode.ModelInvalid, "XT node variable length does not match its schema declaration.");
        if (userFields.Offset < 0 || userFields.Count < 0 ||
            userFields.Offset > userFieldPoolLength - userFields.Count)
            throw new XtFormatException(XtErrorCode.ModelInvalid, "XT user-field range is invalid.");
        if (userFields.Count is not 0 && userFields.Count != userFieldSize)
            throw new XtFormatException(XtErrorCode.ModelInvalid, "XT user-field count does not match model user-field size.");
    }

    internal static void ValidateVariableRange(XtRange range, int variableLength, int poolLength, string field)
    {
        if (range.Count != variableLength || range.Offset < 0 || range.Offset > poolLength - range.Count)
            throw new XtFormatException(XtErrorCode.ModelInvalid, $"Variable field {field} range does not match the node variable length.");
    }

    internal static void ValidatePointer(int index,int nodeClass,IReadOnlyDictionary<int,int> nodeTypes,XtSchemaDefinition schema,string field)
    {
        if(index<=0)return;
        // Part transmit graphs may legally retain references to entities outside
        // the selected transmit block. Validate class only when the target is
        // present in this model; preserve external node indices verbatim.
        if(!nodeTypes.TryGetValue(index,out var actualType))return;
        if(schema.GetNode(nodeClass).Type!=0&&actualType!=nodeClass)throw new XtFormatException(XtErrorCode.ModelInvalid,$"Pointer field {field} requires node type {nodeClass}, but node {index} has type {actualType}.");
    }

    private static XtFormatException Shape(XtSchemaDefinition actual, XtSchemaDefinition expected)
        => new(XtErrorCode.SchemaMismatch, $"Schema shape {actual.Identity} does not match generated binding {expected.Identity}.");
}

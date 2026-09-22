using System.Text;
using ProjectGmKernel.Xt;

namespace ProjectGmKernel.Xt.Tests;

public sealed class XtCharacterEscapeTests
{
    [Fact]
    public void CharacterFieldsUseParasolidEscapesAndDoNotConsumeTheNextToken()
    {
        // Length is the decoded character count. '\\' is one backslash, '\n' is
        // carriage return, '\r' is line feed, and a c field is not followed by a
        // space, so the terminator node type is glued to the final '.1'.
        const string logical = "\r\n\0\\Part1.2\\\\.1";
        var catalog = XtSchemaCatalog.OpenBuiltIn();
        var schema = catalog.Resolve("SCH_3701097_37102");
        var document = new XtDocument
        {
            VersionText = ": TRANSMIT FILE created by modeller version 3701097",
            HeaderSchemaIdentity = schema.Identity,
            Schema = schema,
            UserFieldSize = 0,
            Nodes =
            [
                new XtNode
                {
                    Type = 84,
                    Index = 335233,
                    Fields = logical.Select(XtFieldValue.Char).ToArray(),
                },
            ],
        };

        var encoded = XtCodec.Encode(document);
        var flat = encoded.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
        Assert.Contains("\\n\\r\\0\\\\Part1.2\\\\\\\\.11 0", flat, StringComparison.Ordinal);

        var decoded = XtCodec.Read(catalog, Encoding.UTF8.GetBytes(encoded));
        var chars = new string(decoded.Nodes[0].Fields.Select(static field => field.Character).ToArray());
        Assert.Equal(logical, chars);
        Assert.Equal(encoded, XtCodec.Encode(decoded));
    }
}

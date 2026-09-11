using ProjectGmKernel.Xt;
using System.Text.Encodings.Web;
using System.Text.Json;

if (args.Length == 0 || args.Any(static argument => argument is "-h" or "--help"))
{
    Console.Error.WriteLine("Usage: XtToJson <model.x_t> [-o <output.json>] [--compact] [--schema-dir <directory>]");
    Console.Error.WriteLine("Converts a Parasolid XT text file into a structured JSON document using the built-in V30-V38 schema bindings.");
    Console.Error.WriteLine("Files with an embedded schema also need --schema-dir (or PARASOLID_SCHEMA_DIR/P_SCHEMA) for the supporting schemas.");
    return 1;
}

var inputPath = args[0];
string? outputPath = null;
string? schemaDirectory = null;
var compact = false;
for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-o" or "--output":
            if (i + 1 >= args.Length)
                return Fail("Option -o requires a value.");
            outputPath = args[++i];
            break;
        case "--schema-dir":
            if (i + 1 >= args.Length)
                return Fail("Option --schema-dir requires a value.");
            schemaDirectory = args[++i];
            break;
        case "--compact":
            compact = true;
            break;
        default:
            return Fail($"Unknown option '{args[i]}'.");
    }
}
outputPath ??= Path.ChangeExtension(inputPath, ".json");
schemaDirectory ??= Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR") ?? Environment.GetEnvironmentVariable("P_SCHEMA");

byte[] source;
try
{
    source = File.ReadAllBytes(inputPath);
}
catch (Exception exception)
{
    return Fail($"Cannot read '{inputPath}': {exception.Message}");
}

XtDocument document;
try
{
    var catalog = string.IsNullOrWhiteSpace(schemaDirectory)
        ? XtSchemaCatalog.OpenBuiltIn()
        : XtSchemaCatalog.OpenDirectory(schemaDirectory);
    document = XtCodec.Read(catalog, source);
}
catch (XtFormatException exception) when (exception.Code == XtErrorCode.SchemaNotFound && schemaDirectory is null)
{
    return Fail($"{exception.Message} Files that embed a schema need --schema-dir (or PARASOLID_SCHEMA_DIR/P_SCHEMA) pointing at the supporting schema files.");
}
catch (Exception exception)
{
    return Fail($"Cannot parse '{inputPath}' as an XT text file: {exception.Message}");
}

IXtSchemaModel model;
try
{
    model = XtGeneratedModelCodec.Decode(document);
}
catch (XtFormatException exception) when (exception.Code == XtErrorCode.UnsupportedVersion)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine("Built-in schema bindings: " + string.Join(", ", XtBuiltInSchemas.Identities.Order(StringComparer.Ordinal)));
    return 2;
}
catch (Exception exception)
{
    return Fail($"Cannot build a typed model from '{inputPath}': {exception.Message}");
}

try
{
    using var stream = File.Create(outputPath);
    using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = !compact, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    XtGeneratedModelJson.Write(model, writer);
}
catch (Exception exception)
{
    return Fail($"Cannot write '{outputPath}': {exception.Message}");
}

var bindingIdentity = model.SchemaIdentity;
if (bindingIdentity != document.Schema.Identity)
    Console.Error.WriteLine($"Note: source schema {document.Schema.Identity} was decoded with the compatible binding {bindingIdentity}.");
Console.WriteLine($"{inputPath} -> {outputPath} ({bindingIdentity})");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

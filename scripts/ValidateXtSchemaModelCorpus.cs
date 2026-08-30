#!/usr/bin/env dotnet
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj

using ProjectGmKernel.Xt;
using System.Runtime.CompilerServices;
using Schema37102 = ProjectGmKernel.Xt.Schema.SCH_3701097_37102;

var scriptDirectory = GetScriptDirectory();
var schemaDirectory = Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR") ?? Environment.GetEnvironmentVariable("P_SCHEMA");
var catalog = string.IsNullOrWhiteSpace(schemaDirectory)
    ? XtSchemaCatalog.OpenBuiltIn()
    : XtSchemaCatalog.OpenDirectory(schemaDirectory);
catalog.LoadAll();
var fixtures = args.Length == 0
    ? Path.GetFullPath(Path.Combine(scriptDirectory, "..", "tests", "ParasolidXtCorpus", "Fixtures"))
    : Path.GetFullPath(Path.Combine(scriptDirectory, args[0]));
var outputRoot = Path.GetFullPath(Path.Combine(scriptDirectory, "..", "bin", "xt-schema-model-rebuilt"));
var paths = Directory.EnumerateFiles(fixtures, "model.x_t", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
if (paths.Length == 0) throw new InvalidOperationException("No committed XT fixtures were found.");
foreach (var path in paths)
{
    Console.Write($"{Path.GetRelativePath(fixtures, path)}: ");
    var document = XtCodec.Read(catalog, File.ReadAllBytes(path));
    var model = Schema37102.CODEC.Decode(document);
    var rebuilt = Schema37102.CODEC.Encode(model);
    var rebuiltBytes = XtCodec.Write(catalog, rebuilt);
    _ = XtCodec.Read(catalog, rebuiltBytes);
    var relativeDirectory = Path.GetDirectoryName(Path.GetRelativePath(fixtures, path))!;
    var outputDirectory = Path.Combine(outputRoot, relativeDirectory);
    Directory.CreateDirectory(outputDirectory);
    File.WriteAllBytes(Path.Combine(outputDirectory, "model.x_t"), rebuiltBytes);
    Console.WriteLine($"bodies={model.BODY.Length}, assemblies={model.ASSEMBLY.Length}, frames={model.FRAME.Length}, meshes={model.MESH.Length}, lattices={model.LATTICE.Length}, rebuilt={rebuiltBytes.Length}");
}

static string GetScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

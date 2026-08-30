#!/usr/bin/env dotnet
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj

using ProjectGmKernel.Xt;
using System.Runtime.CompilerServices;

var scriptDirectory = GetScriptDirectory();
var schemaDirectory = Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR") ?? Environment.GetEnvironmentVariable("P_SCHEMA");
if (string.IsNullOrWhiteSpace(schemaDirectory) || !Directory.Exists(schemaDirectory))
    throw new InvalidOperationException("PARASOLID_SCHEMA_DIR or P_SCHEMA must name the caller-provided schema directory.");
var catalog = XtSchemaCatalog.OpenDirectory(schemaDirectory);
catalog.LoadAll();
var fixtures = args.Length==0?Path.GetFullPath(Path.Combine(scriptDirectory, "..", "tests", "ParasolidXtCorpus", "Fixtures")):Path.GetFullPath(Path.Combine(scriptDirectory,args[0]));
var outputRoot = Path.GetFullPath(Path.Combine(scriptDirectory, "..", "bin", "xt-brep-rebuilt"));
var paths = Directory.EnumerateFiles(fixtures, "model.x_t", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
if (paths.Length == 0) throw new InvalidOperationException("No committed XT fixtures were found.");
foreach (var path in paths)
{
    Console.Write($"{Path.GetRelativePath(fixtures, path)}: ");
    var document = XtCodec.Read(catalog, File.ReadAllBytes(path));
    var model = XtBrepConverter.Decode(document);
    if(Environment.GetEnvironmentVariable("PGM_XT_DUMP_GEOMETRY")=="1")Console.Write(string.Join(',',model.Geometries.ToArray().Select(row=>row.Kind))+"; ");
    XtBrepValidator.Validate(model);
    var rebuilt = XtBrepConverter.Encode(catalog, model, document.HeaderSchemaIdentity);
    if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTIC_REMAP")=="1")rebuilt=Remap(rebuilt,document);
    var rebuiltBytes = XtCodec.Write(catalog, rebuilt);
    _ = XtCodec.Read(catalog, rebuiltBytes);
    var relativeDirectory=Path.GetDirectoryName(Path.GetRelativePath(fixtures,path))!;var outputDirectory=Path.Combine(outputRoot,relativeDirectory);Directory.CreateDirectory(outputDirectory);File.WriteAllBytes(Path.Combine(outputDirectory,"model.x_t"),rebuiltBytes);
    Console.WriteLine($"parts={model.Parts.Length}, bodies={model.Bodies.Length}, assemblies={model.Assemblies.Length}, frames={model.Frames.Length}, meshes={model.Meshes.Length}, lattices={model.Lattices.Length}, rebuilt={rebuiltBytes.Length}");
}

static string GetScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

static XtDocument Remap(XtDocument rebuilt,XtDocument original)
{
    var originalGroups=original.Nodes.GroupBy(node=>original.Schema.GetNode(node.Type).Name).ToDictionary(group=>group.Key,group=>group.ToArray());var rebuiltGroups=rebuilt.Nodes.GroupBy(node=>rebuilt.Schema.GetNode(node.Type).Name).ToDictionary(group=>group.Key,group=>group.ToArray());var map=new Dictionary<int,int>();foreach(var pair in rebuiltGroups){if(!originalGroups.TryGetValue(pair.Key,out var targets)||targets.Length!=pair.Value.Length)continue;for(var i=0;i<targets.Length;i++)map[pair.Value[i].Index]=targets[i].Index;}var next=original.Nodes.Max(node=>node.Index)+1;foreach(var node in rebuilt.Nodes)if(!map.ContainsKey(node.Index))map[node.Index]=next++;
    var clones=new List<XtNode>();foreach(var source in rebuilt.Nodes){var fields=source.Fields.ToArray();var descriptor=rebuilt.Schema.GetNode(source.Type);var offset=0;foreach(var field in rebuilt.Schema.GetFields(descriptor)){if(!field.Transmit)continue;var count=field.ElementCount>1?field.ElementCount:descriptor.Variable&&field.ElementCount==1?Math.Max(0,source.VariableLength):1;if(field.Type=='p')for(var i=0;i<count;i++)if(map.TryGetValue(fields[offset+i].Pointer,out var pointer))fields[offset+i].Pointer=pointer;offset+=count;}clones.Add(new XtNode{Type=source.Type,Index=map.GetValueOrDefault(source.Index,source.Index),VariableLength=source.VariableLength,Fields=fields,UserFields=source.UserFields.ToArray()});}
    var byIndex=clones.ToDictionary(node=>node.Index);var ordered=new List<XtNode>();foreach(var node in original.Nodes)if(byIndex.Remove(node.Index,out var clone))ordered.Add(clone);ordered.AddRange(byIndex.Values);return new XtDocument{VersionText=rebuilt.VersionText,HeaderSchemaIdentity=rebuilt.HeaderSchemaIdentity,Schema=rebuilt.Schema,UserFieldSize=rebuilt.UserFieldSize,Nodes=ordered.ToArray()};
}

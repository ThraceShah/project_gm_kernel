#!/usr/bin/env dotnet
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj

using ProjectGmKernel.Xt;
using System.Runtime.CompilerServices;
using System.Text;

var check=args.Contains("--check",StringComparer.Ordinal);string? explicitDirectory=null;
for(var i=0;i<args.Length;i++)if(args[i]=="--schema-dir"&&i+1<args.Length)explicitDirectory=args[++i];
var schemaDirectory=explicitDirectory??Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR")??Environment.GetEnvironmentVariable("P_SCHEMA");
if(string.IsNullOrWhiteSpace(schemaDirectory)||!Directory.Exists(schemaDirectory))
    throw new InvalidOperationException("Pass --schema-dir or set PARASOLID_SCHEMA_DIR/P_SCHEMA to a caller-provided schema directory.");
var catalog=XtSchemaCatalog.OpenDirectory(schemaDirectory);catalog.LoadAll();
var report=new StringBuilder().AppendLine("identity\tfile\tmodeler_version\tschema_number\tnodes\tfields");
foreach(var info in catalog.Schemas.OrderBy(static info=>info.Identity,StringComparer.Ordinal))report.Append(info.Identity).Append('\t').Append(info.FileName).Append('\t').Append(info.ModelerVersion).Append('\t').Append(info.SchemaNumber).Append('\t').Append(info.NodeCount).Append('\t').Append(info.FieldCount).AppendLine();
if(!check)
{
    var scriptDirectory=Path.GetDirectoryName(GetScriptPath())!;var output=Path.GetFullPath(Path.Combine(scriptDirectory,"..","bin","xt-schema-analysis","catalog.tsv"));Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllText(output,report.ToString(),new UTF8Encoding(false));Console.WriteLine($"Wrote private schema analysis to {Path.GetRelativePath(scriptDirectory,output)}");
}
Console.WriteLine($"Validated {catalog.Schemas.Count} external XT schemas; no schema-derived source was generated.");
static string GetScriptPath([CallerFilePath]string path="")=>path;

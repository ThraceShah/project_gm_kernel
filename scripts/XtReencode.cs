#!/usr/bin/env dotnet run
#:property AssemblyName=TopologyDump
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;

static string GetScriptPath([CallerFilePath] string path = "") => path;

if (args.Length is not 2 and not 4 || args.Length == 4 && args[2] is not "--schema" and not "--base-schema")
{
    Console.Error.WriteLine("usage: dotnet run scripts/XtReencode.cs -- INPUT.x_t OUTPUT.x_t [--schema IDENTITY | --base-schema IDENTITY]");
    return 2;
}

var scriptDirectory = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var inputPath = Path.GetFullPath(Path.Combine(scriptDirectory, args[0]));
var outputPath = Path.GetFullPath(Path.Combine(scriptDirectory, args[1]));
var source = File.ReadAllBytes(inputPath);
var output = args.Length == 2
    ? System.Text.Encoding.ASCII.GetBytes(XtText.Encode(XtText.DecodeDocument(System.Text.Encoding.ASCII.GetString(source))))
    : args[2] == "--schema"
        ? XtCorpusInspection.Transcode(source, args[3])
        : XtCorpusInspection.EmbedWithBaseSchema(source, args[3]);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllBytes(outputPath, output);
return 0;

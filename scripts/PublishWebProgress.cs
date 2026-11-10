#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Runtime.CompilerServices;
var scripts=Path.GetDirectoryName(ScriptPath())!;
var source=Path.GetFullPath(Option("--source","../temp_docs/web-progress/current-data"),scripts);
var destination=Path.GetFullPath(Option("--destination","../web/data"),scripts);
var id=Option("--run-id","");if(string.IsNullOrWhiteSpace(id)||id.Contains('/')||id.Contains('\\')||id is "." or "..")throw new ArgumentException("Exact safe --run-id is required");
WebProgressPublishing.Publish(source,id,destination);Console.WriteLine("Published validated static run "+id);return 0;
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:args[at+1];}
static string ScriptPath([CallerFilePath]string path="")=>path;

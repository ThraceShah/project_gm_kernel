#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(Source())!,".."));
if(!args.SequenceEqual(new[]{"--consumer"}))throw new ArgumentException("--consumer");
var folder="temp_docs/independent-localjet-ab0177e/"+"q2-one-nurbs";if(Directory.Exists(folder))throw new IOException("preserve previous execution");Directory.CreateDirectory(folder);
var info=new ProcessStartInfo("setsid"){RedirectStandardOutput=true,RedirectStandardError=true};info.ArgumentList.Add("dotnet");
var argv=new[]{"run","scripts/RegularSolidBlendOracle.cs","-c","Release","-p:UseSharedCompilation=false","-p:BuildInParallel=false","--","plane-nurbs","rounding-only","--output-name=localjet-independent-ab0177e-q2","--bench-profile"};
foreach(var arg in argv)info.ArgumentList.Add(arg);info.Environment["MSBUILDDISABLENODEREUSE"]="1";info.Environment["P_SCHEMA"]=Path.GetFullPath("third_party/parasolid/schema");
{info.Environment["GM_WEB_ROOT"]=Directory.GetCurrentDirectory();info.Environment["GM_WEB_CAPTURE"]=Path.GetFullPath(folder+"/capture");info.Environment["GM_WEB_COMMIT"]="1065e7c8f818ce10f6d3ee3edfe8f8bc33f3d582";info.Environment["GM_WEB_CAPTURE_COMMIT"]="1065e7c8f818ce10f6d3ee3edfe8f8bc33f3d582";info.Environment["GM_WEB_GEOMETRY_AUTHOR"]="Independent GM and PK input construction from identical parameters";}
File.WriteAllText(folder+"/command.json",new JsonObject{["argv"]=new JsonArray(argv.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray()),["scope"]="one complete Linux consumer, no Mac time qualification"}.ToJsonString());
using var process=Process.Start(info)!;File.WriteAllText(folder+"/owned-pgid.txt",process.Id+"\n");
await using var stdout=new FileStream(folder+"/stdout.log",FileMode.CreateNew);await using var stderr=new FileStream(folder+"/stderr.log",FileMode.CreateNew);
var a=process.StandardOutput.BaseStream.CopyToAsync(stdout);var b=process.StandardError.BaseStream.CopyToAsync(stderr);await process.WaitForExitAsync();await Task.WhenAll(a,b);File.WriteAllText(folder+"/process.exit",process.ExitCode+"\n");
using var cleanup=Process.Start(new ProcessStartInfo("kill"){ArgumentList={"-TERM","--","-"+process.Id},RedirectStandardError=true})!;await cleanup.WaitForExitAsync();File.WriteAllText(folder+"/owned-group-cleanup.txt",cleanup.ExitCode+"\n"+await cleanup.StandardError.ReadToEndAsync());
Console.WriteLine("Original child exit="+process.ExitCode+"; only owned PGID cleanup attempted");return process.ExitCode;
static string Source([CallerFilePath]string path="")=>path;

#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

// Execute exact capture identity/return/check prefixes with explicit native
// boundary seams. No native binding, kernel project, geometry, or PK session.
var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);
var output=Path.Combine(root,"temp_docs/operation-followup-a0-review/owner-shim");Directory.CreateDirectory(output);
var capture=File.ReadAllText(Path.Combine(root,"scripts/WebProgressCapture.cs"));
var sourceHash=Hash(capture);
if(sourceHash!="e212cbf7cd2167f2cde9d2aadbaaff69656d887d2c9536568efe433e17e1c592")throw new InvalidOperationException("Not fixed a0 Capture source");
string Slice(string start,string end){var at=capture.IndexOf(start,StringComparison.Ordinal);var stop=capture.IndexOf(end,at,StringComparison.Ordinal);if(at<0||stop<0)throw new InvalidDataException(start);return capture[at..stop];}
var fields=Slice("    internal static readonly string? Output=","    private const double ChordTolerance");
var before=Slice("    internal static void Before(","            var support=new HashSet<int>();");
var after=Slice("    internal static void After(","        Observe(()=>\n        {\n            var ordinal=Ordinals[own]-1;var records=new JsonArray();");
var complete=Slice("    private static void Complete(","            var tracks=new JsonArray();");
var addCheck=Slice("    private static void AddCheck(","    private static void Flush()");
var observe=Slice("    private static void Observe(","    private static void Write(");
var sanitize=Slice("    private static string Sanitize(","    private static void Vector(");
// Erase vendor pointer types only at cut signatures; no ABI is defined/called.
after=after.Replace("PK_FACE_array_t*","void*");complete=complete.Replace("PK_TOPOL_track_r_t*","void*");
var api=Regex.Match(before,"string operation=\"([^\"]+)\"").Groups[1].Value;
var shim="""
global using EntityTag=System.Int32;
global using BodyTag=System.Int32;
using System.Text.Json.Nodes;
internal static unsafe partial class WebProgressCapture
{
"""+"\n"+fields+
    "    private static string SanitizeOpId(string s)=>new string(s.Select(c=>char.IsLetterOrDigit(c)?char.ToLowerInvariant(c):'-').ToArray()).Trim('-');\n"+
    before+"            BeforeBoundary?.Invoke(); Flush();\n        });\n    }\n"+
    after+"    }\n"+
    complete+"            AfterBoundary?.Invoke(); Flush();\n        });\n    }\n"+
    addCheck+observe+sanitize+"""
    private static JsonNode? Finite(double? value)=>value.HasValue&&double.IsFinite(value.Value)?JsonValue.Create(value.Value):null;
    private static void Flush() { } // excluded mesh/native comparison/IO boundary
    internal static Action? BeforeBoundary,AfterBoundary;
    internal static OperationRecord Last(bool own)=>OperationsByOrdinal[Ordinals[own]-1];
    internal static void Parameters(bool own)=>SetParameters(own,new JsonObject{["radius"]=.5});
    internal static void FixturePrimary(OperationRecord op)
    {
        var names=new List<string>{"body-count","True-body-check","False-body-check","equivalence"};
        for(var i=0;i<5;i++)names.Add("count-"+i);for(var i=0;i<15;i++)names.Add("mass-"+i);
        var ordinal=op.StepRef!.GetValue<int>();
        foreach(var name in names)AddCheck(ordinal+"-"+name,"synthetic owner fixture only","pass",null,null,null,"","NOT geometry evidence",ordinal:ordinal);
    }
    internal static string[] Keys()=>ContextSteps.Keys.Order().ToArray();
}
""";
File.WriteAllText(Path.Combine(output,"CaptureShim.cs"),shim);
File.WriteAllText(Path.Combine(output,"Probe.csproj"),"""
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks><PublishAot>false</PublishAot></PropertyGroup><ItemGroup><Compile Include="../../../scripts/OperationAcceptanceLedger.cs" Link="ActualLedger.cs"/><Compile Include="../../../scripts/OperationAcceptanceRules.cs" Link="ActualRules.cs"/></ItemGroup></Project>
""");
File.WriteAllText(Path.Combine(output,"Program.cs"),"""
using System.Text.Json.Nodes;
var sample=args[0];
Environment.SetEnvironmentVariable("GM_WEB_CAPTURE",Path.Combine(AppContext.BaseDirectory,"fixture-capture-"+sample));
Environment.SetEnvironmentVariable("GM_WEB_SAMPLE_ID",sample);
var ledger=OperationAcceptanceLedger.BeginScenario("fixture-"+sample,"owner recording only");
var api=API_LITERAL;
var checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+sample+" "+label);}
unsafe void Call(bool own,string context,string operation)
{
    using var scope=WebProgressCapture.UseContext(context);
    WebProgressCapture.Before(own,own?201:101,operation:operation);
    WebProgressCapture.Parameters(own);
    WebProgressCapture.After(own,own?201:101,own?2:1,0,0,null,0);
}
WebProgressCapture.Context="caller";
Call(false,"",api);
var native=WebProgressCapture.Last(false);
Check(native.Returns["pk"]!.GetValue<int>()==0&&native.StepRef!.GetValue<int>()==0,"real Before/After prefixes created native owner and return");
using(WebProgressCapture.UseContext("imported-product"))
{
    Call(false,"imported-product","PK_BODY_boolean");Call(true,"imported-product","PK_BODY_boolean");
    Check(WebProgressCapture.Context=="imported-product","nested Fillet-style scope restores lifecycle context");
}
Check(WebProgressCapture.Context=="caller","Lifecycle normal exit restores caller");
try{using var outer=WebProgressCapture.UseContext("outer");using var inner=WebProgressCapture.UseContext("inner");throw new InvalidOperationException("fixture");}
catch(InvalidOperationException e) when(e.Message=="fixture") { }
Check(WebProgressCapture.Context=="caller","nested exception restores exact caller");
// Deliberately reproduce the old stale ambient context after native observation.
WebProgressCapture.Context="stale-imported-product";
var manual=WebProgressCapture.BeginObservedOperation("unused-manual-id","fillet","primary",api,context:"");
Check(ReferenceEquals(native,manual),"manual explicit empty key reuses actual native object despite stale ambient");
Call(true,"",api);
// Last(false) now refers to the intervening imported Lifecycle Boolean; native
// is the saved actual fillet object, not whichever API was most recent on PK.
Check(ReferenceEquals(manual,WebProgressCapture.Last(true))&&ReferenceEquals(manual,native),"actual GM Before and After prefixes use same saved manual/native object");
Check(WebProgressCapture.Context=="stale-imported-product","Fillet normal exit restores pre-call ambient");
Check(manual.Returns["gm"]!.GetValue<int>()==0&&manual.Returns["pk"]!.GetValue<int>()==0&&manual.GmMs==2&&manual.PkMs==1,"paired actual prefix returns and elapsed fields belong to shared object");
WebProgressCapture.FixturePrimary(manual);
ledger.RecordCheck(manual.Id,"auxiliary","classification","synthetic classification failure","fail");ledger.EndOperation(manual.Id);
Check(manual.PrimaryStatus=="pass"&&manual.HasAuxiliaryFailure,"synthetic primary survives auxiliary failure on same owner");
Check(manual.AuxiliaryChecks.All(x=>x["owner"]!.GetValue<string>()==manual.Id),"manual and observed API checks have same owner");
Call(false,"",api);var secondNative=WebProgressCapture.Last(false);
var secondManual=WebProgressCapture.BeginObservedOperation("unused-second","repeat","primary",api,context:"");Call(true,"",api);
Check(!ReferenceEquals(secondNative,native)&&ReferenceEquals(secondNative,secondManual)&&ReferenceEquals(secondManual,WebProgressCapture.Last(true)),"second occurrence does not reuse first rep owner");
Call(false,"different",api);var different=WebProgressCapture.Last(false);
var differentManual=WebProgressCapture.BeginObservedOperation("unused-different","different","primary",api,context:"different");Call(true,"different",api);
Check(!ReferenceEquals(different,native)&&ReferenceEquals(different,differentManual),"different explicit context does not merge with empty context");
var pending=WebProgressCapture.BeginObservedOperation("manual-first","pending","primary",api,context:"pending");
Call(true,"pending",api);Call(false,"pending",api);
Check(ReferenceEquals(pending,WebProgressCapture.Last(true))&&ReferenceEquals(pending,WebProgressCapture.Last(false)),"manual-first reservation consumed by actual Before on both sides");
WebProgressCapture.BeforeBoundary=()=>throw new IOException("injected native-boundary failure");
using(WebProgressCapture.UseContext("before-error"))WebProgressCapture.Before(true,301,operation:api);
WebProgressCapture.BeforeBoundary=null;
var beforeError=WebProgressCapture.Last(true);
Check(beforeError.AuxiliaryChecks.Any(x=>x["owner"]!.GetValue<string>()==beforeError.Id&&x["id"]!.GetValue<string>().StartsWith("capture-error-")),"actual Observe catches Before boundary error on its actual owner");
WebProgressCapture.AfterBoundary=()=>throw new IOException("injected post-return boundary failure");
unsafe{WebProgressCapture.After(true,301,3,17,0,null,0);}WebProgressCapture.AfterBoundary=null;
Check(ReferenceEquals(beforeError,WebProgressCapture.Last(true))&&beforeError.Returns["gm"]!.GetValue<int>()==17,"After records actual return before boundary exception without moving owner");
Check(beforeError.AuxiliaryChecks.Count(x=>x["id"]!.GetValue<string>().StartsWith("capture-error-"))==2,"Both boundary exceptions retained on same owner");
Check(WebProgressCapture.Context=="stale-imported-product","erroring nested scopes restore caller");
var count=ledger.Operations.Count;
var duplicate=false;try{ledger.BeginOperation(pending.Id,"duplicate");}catch(InvalidOperationException){duplicate=true;}
Check(duplicate&&ledger.Operations.Count==count,"duplicate explicit IDs remain errors");
Check(ledger.Operations.Select(x=>x.Id).Distinct().Count()==ledger.Operations.Count,"all in-process occurrences have unique raw owner IDs");
var json=new JsonObject{["sample"]=sample,["checks"]=checks,["keys"]=new JsonArray(WebProgressCapture.Keys().Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray()),["operations"]=new JsonArray(ledger.Operations.Select(x=>(JsonNode?)x.ToJsonObject()).ToArray()),["scope"]="actual managed source prefixes with native/mesh/Compare seams; NOT PK lifecycle or geometry"};
File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"result-"+sample+".json"),json.ToJsonString());Console.WriteLine("PASS checks="+checks+" sample="+sample);return 0;
""".Replace("API_LITERAL",System.Text.Json.Nodes.JsonValue.Create(api)!.ToJsonString()));
Console.WriteLine("CAPTURE_SHA256 "+sourceHash);
foreach(var part in new[]{("fields",fields),("Before-managed-prefix",before),("After-wrapper-prefix-erased-native-pointer",after),("Complete-managed-prefix-erased-native-pointer",complete),("AddCheck",addCheck),("Observe",observe)})Console.WriteLine($"SOURCE_PART {part.Item1} sha256={Hash(part.Item2)}");
foreach(var sample in new[]{"sample-0","sample-1"})
{
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=output};foreach(var arg in new[]{"run","--project","Probe.csproj","-p:UseSharedCompilation=false","--",sample})start.ArgumentList.Add(arg);start.Environment["MSBUILDDISABLENODEREUSE"]="1";
    using var process=Process.Start(start)!;process.WaitForExit();if(process.ExitCode!=0)return process.ExitCode;
}
foreach(var file in new[]{"CaptureShim.cs","Program.cs","bin/Debug/net10.0/Probe.dll","bin/Debug/net10.0/Probe.pdb"})
    Console.WriteLine("GENERATED_ARTIFACT "+file+" sha256="+Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(output,file)))));
return 0;
static string Hash(string value)=>Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
static string Here([CallerFilePath]string path="")=>path;

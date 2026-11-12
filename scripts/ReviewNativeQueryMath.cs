#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=BooleanOperationOracle
#:property AllowUnsafeBlocks=true
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
if(args.Length!=1||args[0].Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("new receipt name");
var output=Path.Combine(root,"temp_docs/native-query-independent",args[0]);if(Directory.Exists(output))throw new InvalidOperationException("immutable output");Directory.CreateDirectory(output);
var delivery=JsonNode.Parse(File.ReadAllBytes(Path.Combine(root,"docs/query-numerics-closure/results.json")))!;
var receipt=new JsonObject();var controls=new JsonArray();var checks=0;var queryCount=0;
// Reconstruct only the documented 20-double layout, then independently verify
// every historical current result against the unchanged helper at this SHA.
foreach(var report in delivery["jointMassStandardsReassessment"]!["reports"]!.AsArray())
{
    var a=new double[20];var b=new double[20];var old=report!["oldProperties"]!.AsArray();
    ReadOnlySpan<int> positions=[0,2,4,5,6,8,9,10,11,12,13,14,15,16,18];ReadOnlySpan<int> moduli=[1,3,7,7,7,17,17,17,17,17,17,17,17,17,19];
    for(var i=0;i<15;i++){var p=old[i]!;a[positions[i]]=N(p,"referenceEstimate");b[positions[i]]=N(p,"actualEstimate");a[moduli[i]]=N(p,"referenceModulus");b[moduli[i]]=N(p,"actualModulus");}
    var current=new JsonObject{["properties"]=new JsonArray()};ParasolidMassProperties.Assess(a,b,"historical-independent",1,current);
    for(var i=0;i<15;i++)foreach(var key in new[]{"agreementStatus","normalizedLower","normalizedUpper","scaleLower","scaleUpper"})
        Require(current["properties"]![i]![key]!.ToJsonString()==report["current"]!["properties"]![i]![key]!.ToJsonString(),"historical recomputation");
}
// Nonzero norms in more than one component, zero off-diagonal inertia,
// correlated worst-case boxes, units and explicit invalid-scale rejection.
foreach(var scale in new[]{.125,1.0,8.0})foreach(var mode in new[]{"cog-small","cog-large","inertia-small","inertia-large","shared-wide","identity"})
{
    var a=Block(scale);var b=(double[])a.Clone();
    if(mode.StartsWith("cog",StringComparison.Ordinal))b[4]=b[5]=b[6]=(mode=="cog-small"?5e-8:8e-8)*scale;
    if(mode.StartsWith("inertia",StringComparison.Ordinal)){var off=(mode=="inertia-small"?1e-8:2e-7)*(a[8]+a[12]+a[16]);b[9]=b[11]=off;}
    if(mode=="shared-wide")a[7]=b[7]=1e-6*scale;
    var expected=mode.EndsWith("large",StringComparison.Ordinal)?MassAgreement.Different:mode=="shared-wide"?MassAgreement.Unresolved:MassAgreement.Verified;
    var state=ParasolidMassProperties.Assess(a,b,mode,1,null);Require(state==expected,"norm control "+mode);
    controls.Add((JsonNode)new JsonObject{["kind"]="manufactured; no geometry qualification",["mode"]=mode,["lengthScale"]=scale,["expected"]=expected.ToString(),["actual"]=state.ToString()});
}
foreach(var mode in new[]{"zero-mass","scale-crosses-zero","nonfinite","subnormal","overflow"})
{
    var a=Block(1);var b=(double[])a.Clone();
    if(mode=="zero-mass")a[2]=0;if(mode=="scale-crosses-zero")a[3]=a[2];if(mode=="nonfinite")b[4]=double.NaN;
    if(mode=="subnormal")a[2]=double.Epsilon;if(mode=="overflow")a[8]=a[12]=a[16]=double.MaxValue;
    var rejected=false;try{ParasolidMassProperties.Assess(a,b,mode,1,null);}catch(InvalidOperationException){rejected=true;}
    Require(rejected,"explicit unresolved/invalid scale failure "+mode);controls.Add((JsonNode)new JsonObject{["mode"]=mode,["result"]="hard exception; no certification"});
}
receipt["mathematicalControls"]=controls;
if(!ParasolidScriptHost.TryStartSession("independent native query/math review",out var host,out var reason))throw new Exception(reason);
using(host)unsafe
{
    double precision;Check(PK_SESSION_ask_precision(&precision));receipt["actualPkPrecision"]=precision;Require(precision==1e-8,"PK precision");
    // Guard sentinels check all output segments; rotate an anisotropic tensor
    // and translate its centre to distinguish central/world frame semantics.
    PK_BODY_t block;Check(PK_BODY_create_solid_block(2,4,6,null,&block));
    try
    {
        double* values=stackalloc double[22];for(var i=0;i<22;i++)values[i]=123456789;
        var options=new PK_TOPOL_eval_mass_props_o_t{bound=PK_mass_bound_modulus_c};
        Check(PK_TOPOL_eval_mass_props(1,&block,.99999999999,&options,values+1,values+3,values+5,values+9,values+19));
        Require(values[0]==123456789&&values[21]==123456789,"20-double layout guard");
        Require(Math.Abs(values[1]-48)<1e-10&&Math.Abs(values[3]-48)<1e-10&&Math.Abs(values[19]-88)<1e-10,"block scalar slots");
        Require(Math.Abs(values[9]-208)<1e-9&&Math.Abs(values[13]-160)<1e-9&&Math.Abs(values[17]-80)<1e-9,"central inertia slots");
        var c=Math.Sqrt(.5);var matrix=new PK_TRANSF_sf_t{matrixI0J0=c,matrixI0J1=-c,matrixI1J0=c,matrixI1J1=c,matrixI2J2=1,matrixI3J3=1,matrixI0J3=5,matrixI1J3=7,matrixI2J3=3};
        PK_TRANSF_t transform;Check(PK_TRANSF_create(&matrix,&transform));
        try{int count;PK_TOPOL_t* replaced;PK_LOGICAL_t* exact;Check(PK_BODY_transform(block,transform,0,&count,&replaced,&exact));if(replaced!=null)Check(PK_MEMORY_free(replaced));if(exact!=null)Check(PK_MEMORY_free(exact));}
        finally{Check(PK_ENTITY_delete(1,&transform));}
        Check(PK_TOPOL_eval_mass_props(1,&block,.99999999999,&options,values+1,values+3,values+5,values+9,values+19));
        Require(Math.Abs(values[5]-5)<1e-10&&Math.Abs(values[6]-7)<1e-10&&Math.Abs(values[7]-6)<1e-10,"transformed cog slots");
        Require(Math.Abs(values[9]-184)<1e-8&&Math.Abs(values[13]-184)<1e-8&&Math.Abs(values[17]-80)<1e-8&&Math.Abs(values[10]-24)<1e-8&&Math.Abs(values[12]-24)<1e-8,"world Cartesian central tensor");
        receipt["nativeFrameControl"]="2x4x6 block; M=48; I=(208,160,80); 45deg Z rotation gives (184,184,80), Ixy=Iyx=24; translated cog=(5,7,6)";
    }
    finally{Check(PK_ENTITY_delete(1,&block));}
    PK_BODY_t correct,wrong;Check(PK_BODY_create_solid_sphere(1,null,&correct));Check(PK_BODY_create_solid_sphere(1.01,null,&wrong));
    try{Reject(correct,wrong,"wrong-radius","geometry/structure");}
    finally{Check(PK_ENTITY_delete(1,&wrong));Check(PK_ENTITY_delete(1,&correct));}
    var line=new PK_LINE_sf_t(new PK_AXIS1_sf_t(new(0,0,0),new(1,0,0)));PK_CURVE_t carrier;Check(PK_LINE_create(&line,&carrier));
    Check(PK_CURVE_make_wire_body(carrier,new(0,1),&correct));Check(PK_CURVE_make_wire_body(carrier,new(0,1.01),&wrong));
    try{Reject(correct,wrong,"wrong-finite-trim","unmatched wire edge");}
    finally{Check(PK_ENTITY_delete(1,&wrong));Check(PK_ENTITY_delete(1,&correct));}
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start));
    try
    {
        var products=new JsonArray();
        foreach(var group in delivery["fixedOriginal20"]!["sources"]!.AsArray().Chunk(2))
        {
            var gm=CopyProduct(group[0]!);var reference=CopyProduct(group[1]!);var own=Receive(true,gm);var pk=Receive(false,reference);
            try
            {
                var fresh=Receive(false,gm);
                try{ParasolidBodyCompare.AssertEquivalent(pk,fresh,"independent original sealed product");}
                finally{Check(PK_ENTITY_delete(1,&fresh));}
                var cases=0;
                foreach(var height in new[]{-.1,.1,.3,.5,3,5.7,5.9,6.1})foreach(var radius in new[]{0,2.6,2.8,2.95,3.01,3.5})
                {
                    var angle=cases*2.399963229728653;var point=new PK_VECTOR_t(radius*Math.Cos(angle),radius*Math.Sin(angle),height);PK_enclosure_t a,b;PK_TOPOL_t ta,tb;
                    Check(PK_BODY_contains_vector(pk,point,&a,&ta));Check(KernelRuntime.BodyContainsVector(own,*(M.PK_VECTOR_s*)&point,&b,&tb));
                    Require(a==b,"off-threshold material query "+S(group[0]!,"path")+" case "+cases);cases++;queryCount++;
                }
                products.Add((JsonNode)new JsonObject{["gmSha256"]=Hash(gm),["referenceSha256"]=Hash(reference),["queries"]=cases});
            }
            finally{Check(KernelRuntime.EntityDelete(1,&own));Check(PK_ENTITY_delete(1,&pk));}
        }
        receipt["sealedProductControls"]=products;
    }
    finally{Check(KernelRuntime.SessionStop());}
}
receipt["checks"]=checks;receipt["queries"]=queryCount;
var assembly=typeof(KernelRuntime).Assembly;receipt["nativeMvid"]=assembly.ManifestModule.ModuleVersionId.ToString();receipt["nativeDllSha256"]=Hash(File.ReadAllBytes(assembly.Location));
receipt["scriptSha256"]=Hash(File.ReadAllBytes(Source()));receipt["massHelperSha256"]=Hash(File.ReadAllBytes(Path.Combine(root,"scripts/ParasolidMassProperties.cs")));
using(var process=Process.GetCurrentProcess())
{
    var libraries=new JsonArray();
    foreach(var module in process.Modules.Cast<ProcessModule>().Where(x=>x.ModuleName.Contains("pskernel",StringComparison.Ordinal)).DistinctBy(x=>x.FileName))
    {var bytes=File.ReadAllBytes(module.FileName);libraries.Add((JsonNode)new JsonObject{["module"]=module.ModuleName,["bytes"]=bytes.Length,["sha256"]=Hash(bytes)});}
    Require(libraries.Count==1,"actual mapped PK library");receipt["actualMappedPkLibraries"]=libraries;
}
File.WriteAllText(Path.Combine(output,"receipt.json"),receipt.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"INDEPENDENT_MATH_NATIVE_OK checks={checks} queries={queryCount}");

byte[] CopyProduct(JsonNode identity)
{
    var name=Path.GetFileName(S(identity,"path"));var source=Path.Combine(root,"../gm-native-query-numerics-closure/temp_docs/native-query-replay/closure-final-fixed20",name);
    var bytes=File.ReadAllBytes(source);Require(bytes.Length==identity["bytes"]!.GetValue<int>()&&Hash(bytes)==S(identity,"sha256"),"same-byte sealed product");File.WriteAllBytes(Path.Combine(output,name),bytes);return bytes;
}
static double[] Block(double scale){var a=new double[20];a[0]=a[2]=8*Math.Pow(scale,3);a[18]=24*scale*scale;a[8]=a[12]=a[16]=16.0/3*Math.Pow(scale,5);return a;}
static unsafe PK_BODY_t Receive(bool own,byte[] bytes)
{
    fixed(byte* p=bytes){var data=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;PK_PART_t* parts;
        Check(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&data,(M.PK_PART_receive_o_s*)&options,&count,&parts):PK_PART_receive_b(data,&options,&count,&parts));
        try{if(count!=1)throw new InvalidOperationException("one product");return parts[0];}finally{Check(own?KernelRuntime.MemoryFree(parts):PK_MEMORY_free(parts));}}
}
static void Check(int error)=>ParasolidScriptHost.Check(error,"independent review");
void Reject(PK_BODY_t reference,PK_BODY_t actual,string label,string expected)
{
    var rejected=false;try{ParasolidBodyCompare.AssertEquivalent(reference,actual,label);}catch(InvalidOperationException error)when(error.Message.Contains(expected,StringComparison.Ordinal)){rejected=true;}
    Require(rejected,label+" rejection");Console.WriteLine("EXPECTED_NEGATIVE "+label+" Rejected");
}
void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);checks++;}
static double N(JsonNode node,string key)=>node[key]!.GetValue<double>();
static string S(JsonNode node,string key)=>node[key]!.GetValue<string>();
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static string Source([CallerFilePath]string path="")=>path;

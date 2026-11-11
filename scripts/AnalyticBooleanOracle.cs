#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UseWebProgressCapture=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=AnalyticBooleanOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
global using ManagedBodyTag=System.Int32;
using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var benchRepeat=1;var benchOption=args.SingleOrDefault(x=>x.StartsWith("--bench-repeat=",StringComparison.Ordinal));
if(benchOption!=null)benchRepeat=int.Parse(benchOption["--bench-repeat=".Length..],System.Globalization.CultureInfo.InvariantCulture);
Require(benchRepeat>=1&&benchRepeat<=64,"bench repeat must be in 1..64");
ModelingPerformance.Enabled=args.Contains("--bench-profile",StringComparer.Ordinal);
var output=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","analytic-boolean-oracle"));Directory.CreateDirectory(output);
var outputName=args.Single(x=>x.StartsWith("--output-name=",StringComparison.Ordinal))["--output-name=".Length..];
Require(outputName.Length>0&&outputName.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'),"one new output directory name");
output=Path.Combine(output,outputName);Require(!Directory.Exists(output),"Existing execution evidence is immutable");Directory.CreateDirectory(output);
ParasolidMassProperties.BeginEvidence(output);
var report=new StringBuilder();void Log(string line){Console.WriteLine(line);report.AppendLine(line);}
try
{
    if(!ParasolidScriptHost.TryStartSession("Independent analytic solid Boolean oracle",out var host,out var message))throw new Exception(message);
    using(host)unsafe
    {
        var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"self session");
        try
        {
            var cases=new Fixture[]{new("equal-overlap",2,2,2,0,0),new("unequal-overlap",3,2,2,0,0),new("off-axis",2,2,1.2,1.4,.7),
                new("coincident",2,2,0,0,0),new("disjoint",2,1,5,0,0),new("contained-tool",3,1,.5,0,0),new("containing-tool",1,3,.5,0,0),
                new("concentric",3,1,0,0,0),new("translated",2,2,2,0,0,100,200,-50),new("rotated-bases",2,2,2,0,0,Rotate:true)};
            var failures=0;var count=0;
            foreach(var function in new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})foreach(var fixture in cases)
            {
                var label=fixture.Name+"-"+function;if(!WebProgressCapture.Select(label))continue;count++;
                for(var benchRep=0;benchRep<benchRepeat;benchRep++)
                {
                var benchStart=System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    var gmProduceStart=System.Diagnostics.Stopwatch.GetTimestamp();
                    var own=Produce(true,fixture,function,out var ownStatus,out var ownIdentity);
                    var gmProduceMs=System.Diagnostics.Stopwatch.GetElapsedTime(gmProduceStart).TotalMilliseconds;
                    var pkProduceStart=System.Diagnostics.Stopwatch.GetTimestamp();
                    var expected=Produce(false,fixture,function,out var pkStatus,out var pkIdentity);
                    var pkProduceMs=System.Diagnostics.Stopwatch.GetElapsedTime(pkProduceStart).TotalMilliseconds;
                    Require(ownStatus==pkStatus,$"status {ownStatus}/{pkStatus}");Require(own.Length==expected.Length,$"body count {own.Length}/{expected.Length}");
                    Require(ownIdentity==pkIdentity,$"entity lifetime/tracking signature {ownIdentity}/{pkIdentity}");
                    for(var i=0;i<own.Length;i++)
                    {
                        CheckPointClassification(own[i],expected[i],fixture);
                        var bytes=TransmitManaged(own[i]);File.WriteAllBytes(Path.Combine(output,label+"-"+i+"-managed.x_t"),bytes);var received=ReceiveOracle(bytes);
                        var referenceBytes=TransmitReference(expected[i]);File.WriteAllBytes(Path.Combine(output,label+"-"+i+"-reference.x_t"),referenceBytes);
                        var imported=ReceiveManaged(referenceBytes);CheckPointClassification(imported,expected[i],fixture);
                        CheckReferenceBody(expected[i],label+" independent baseline",Log);CheckReferenceBody(received,label+" managed receive",Log);
                        for(var kind=0;kind<5;kind++)Require(OracleCount(expected[i],kind)==OracleCount(received,kind),$"{label} topology kind {kind}: {OracleCount(received,kind)}/{OracleCount(expected[i],kind)}");
                        WebProgressCapture.BindQuality(label,own[i],expected[i],received,bytes,referenceBytes);
                        CompareBodies(expected[i],received,label,Log);CompareMass(expected[i],received,label,Log);
                    }
                    Log("PASS "+label);
                    Console.WriteLine($"BENCH case={label} rep={benchRep} gm-produce-ms={gmProduceMs:F3} pk-produce-ms={pkProduceMs:F3} verify-ms={System.Diagnostics.Stopwatch.GetElapsedTime(benchStart).TotalMilliseconds-gmProduceMs-pkProduceMs:F3} case-ms={System.Diagnostics.Stopwatch.GetElapsedTime(benchStart).TotalMilliseconds:F3}");
                }
                catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.Message);}
                }
            }
            Require(failures==0,$"{failures}/{count} analytic Boolean fixtures failed");Log($"PASS all {count} independent analytic Boolean fixtures, point classifications, entity lifetimes, tracking and strict XT body comparison.");
        }
        finally{Check(KernelRuntime.SessionStop(),"self session stop");}
    }
}
catch(Exception ex){Log("FAIL: "+ex.Message);Environment.ExitCode=1;}
finally{File.WriteAllText(Path.Combine(output,"report.txt"),report.ToString());}
if(Environment.ExitCode==0&&ParasolidMassProperties.UnresolvedCount!=0)Environment.ExitCode=2;

static void PrintProfile(string operation,double apiMs)
{
    var p=ModelingPerformance.Last;var milliseconds=1000.0/System.Diagnostics.Stopwatch.Frequency;
    Console.WriteLine($"PROFILE operation={operation} api-ms={apiMs:F3} elapsed-ms={p.ElapsedTicks*milliseconds:F3} pairs={p.SurfacePairs} ssi-cells={p.SsiCells} root-cells={p.RootCells} roots={p.Roots} budget-failures={p.BudgetFailures} winding={p.WindingCalls} bound-requests={p.BoundRequests} bound-evaluations={p.BoundEvaluations} cache-hits={p.BoundCacheHits} failure-cache-hits={p.BoundFailureCacheHits} bound-ms={p.BoundTicks*milliseconds:F3}");
    Console.WriteLine($"WORK jacobians={p.JacobianCalls} directions={p.JacobianDirections} residual-directions={p.ResidualDirections} record-builds={p.JacobianRecordBuilds} record-nodes={p.JacobianRecordNodes} record-rebinds={p.JacobianRecordRebinds} direct-spline-point-columns={p.DirectSplinePointColumns}");
    foreach(var use in Enum.GetValues<RootIsolationUse>())if(p.RootCalls[(int)use]!=0||p.RootCellsByUse[(int)use]!=0)Console.WriteLine($"ROOT_WORK use={use} calls={p.RootCalls[(int)use]} cells={p.RootCellsByUse[(int)use]} bound-failures={p.RootBoundFailures[(int)use]} exclusions={p.RootLevelExclusions[(int)use]} nonmonotone={p.RootNonmonotone[(int)use]}");
    foreach(var stage in Enum.GetValues<KernelRuntime.RegularBooleanStage>())if(p.Stages[(int)stage]!=0)Console.WriteLine($"PROFILE stage={stage} ms={p.Stages[(int)stage]*milliseconds:F3}");
}
static unsafe int[] Produce(bool managed,Fixture fixture,int function,out int status,out string identity)
{
    var target=Sphere(managed,fixture,false);var tool=Sphere(managed,fixture,true);var fa=FirstFace(managed,target);var fb=FirstFace(managed,tool);
    var options=new PK_BODY_boolean_o_t{function=function,tracking=PK_LOGICAL_true};var signature=new StringBuilder();int[] bodies;
    WebProgressCapture.Before(managed,target,[],"PK_BODY_boolean_2",[tool]);
    WebProgressCapture.BooleanParameters(managed,options);
    if(managed)
    {
        var mo=Unsafe.BitCast<PK_BODY_boolean_o_t,M.PK_BODY_boolean_o_s>(options);M.PK_TOPOL_track_r_s tr=default;M.PK_boolean_r_s result=default;
        var webStarted=System.Diagnostics.Stopwatch.GetTimestamp();
        var webError=KernelRuntime.BodyBoolean2(target,1,&tool,&mo,&tr,&result);
        var webMs=System.Diagnostics.Stopwatch.GetElapsedTime(webStarted).TotalMilliseconds;
        WebProgressCapture.BooleanAfter(true,webError==0?new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray():[],webMs,webError,result.result,(PK_TOPOL_track_r_t*)&tr);
        Check(webError,"self sphere Boolean");
        if(ModelingPerformance.Enabled)PrintProfile($"Boolean {fixture.Name}-{function}",webMs);
        try
        {
            status=result.result;bodies=new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray();signature.Append(tr.n_track_records);
            for(var i=0;i<tr.n_track_records;i++)
            {
                var record=tr.track_records[i];signature.Append($":{record.track}/{record.n_original_topols}/{record.n_product_topols}");
                for(var j=0;j<record.n_original_topols;j++)signature.Append($"/{record.original_classes[j]}:{(record.original_topols[j]==fa?0:record.original_topols[j]==fb?1:2)}");
                for(var j=0;j<record.n_product_topols;j++){int cls=0;Check(KernelRuntime.EntityAskClass(record.product_topols[j],&cls),"self tracking product");signature.Append('/').Append(cls);}
            }
        }
        finally{Check(KernelRuntime.TopolTrackResultFree(&tr),"self tracking free");Check(KernelRuntime.BooleanResultFree(&result),"self result free");}
    }
    else
    {
        PK_TOPOL_track_r_t tr=default;PK_boolean_r_t result=default;
        var webStarted=System.Diagnostics.Stopwatch.GetTimestamp();
        var webError=PK_BODY_boolean_2(target,1,&tool,&options,&tr,&result);
        var webMs=System.Diagnostics.Stopwatch.GetElapsedTime(webStarted).TotalMilliseconds;
        WebProgressCapture.BooleanAfter(false,webError==0?new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray():[],webMs,webError,result.result,&tr);
        Check(webError,"PK sphere Boolean");
        try
        {
            status=result.result;bodies=new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray();signature.Append(tr.n_track_records);
            for(var i=0;i<tr.n_track_records;i++)
            {
                var record=tr.track_records[i];signature.Append($":{record.track}/{record.n_original_topols}/{record.n_product_topols}");
                for(var j=0;j<record.n_original_topols;j++)signature.Append($"/{record.original_classes[j]}:{(record.original_topols[j]==fa?0:record.original_topols[j]==fb?1:2)}");
                for(var j=0;j<record.n_product_topols;j++){int cls=0;Check(PK_ENTITY_ask_class(record.product_topols[j],&cls),"PK tracking product");signature.Append('/').Append(cls);}
            }
        }
        finally{Check(PK_TOPOL_track_r_f(&tr),"PK tracking free");Check(PK_boolean_r_f(&result),"PK result free");}
    }
    foreach(var tag in new[]{target,tool,fa,fb})
    {int cls=0;var error=managed?KernelRuntime.EntityAskClass(tag,&cls):PK_ENTITY_ask_class(tag,&cls);signature.Append('/').Append(error==0);}
    signature.Append('/').Append(bodies.Length>0&&bodies[0]==target);identity=signature.ToString();return bodies;
}
static unsafe int Sphere(bool managed,Fixture fixture,bool tool)
{
    var axis=fixture.Rotate?(tool?new PK_VECTOR1_t(Math.Sqrt(.5),Math.Sqrt(.5),0):new PK_VECTOR1_t(0,1,0)):new PK_VECTOR1_t(0,0,1);
    var reference=fixture.Rotate&&tool?new PK_VECTOR1_t(0,0,1):new PK_VECTOR1_t(1,0,0);
    var basis=new PK_AXIS2_sf_t(new(fixture.X+(tool?fixture.Dx:0),fixture.Y+(tool?fixture.Dy:0),fixture.Z+(tool?fixture.Dz:0)),axis,reference);int body=0;
    if(managed){var definition=Unsafe.BitCast<PK_AXIS2_sf_t,M.PK_AXIS2_sf_s>(basis);Check(KernelRuntime.BodyCreateSolidSphere(tool?fixture.Rb:fixture.Ra,&definition,&body),"self sphere");}
    else Check(PK_BODY_create_solid_sphere(tool?fixture.Rb:fixture.Ra,&basis,&body),"PK sphere");return body;
}
static unsafe int FirstFace(bool managed,int body)
{int n=0;int* faces=null;Check(managed?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"input face");var face=faces[0];Check(managed?KernelRuntime.MemoryFree(faces):PK_MEMORY_free(faces),"free face list");return face;}
static unsafe void CheckPointClassification(int managed,int oracle,Fixture f)
{
    var points=new[]{new PK_VECTOR_t(f.X,f.Y,f.Z),new PK_VECTOR_t(f.X+f.Dx,f.Y+f.Dy,f.Z+f.Dz),
        new PK_VECTOR_t(f.X+f.Dx/2,f.Y+f.Dy/2,f.Z+f.Dz/2),new PK_VECTOR_t(f.X+f.Ra+f.Rb+10,f.Y,f.Z),
        new PK_VECTOR_t(f.X-f.Ra,f.Y,f.Z),new PK_VECTOR_t(f.X+f.Dx,f.Y+f.Dy+f.Rb,f.Z+f.Dz)};
    foreach(var point in points)
    {
        var mp=Unsafe.BitCast<PK_VECTOR_t,M.PK_VECTOR_s>(point);int me=0,pe=0,mt=0,pt=0;
        Check(KernelRuntime.BodyContainsVector(managed,mp,&me,&mt),"self contains");Check(PK_BODY_contains_vector(oracle,point,&pe,&pt),"PK contains");
        Require(me==pe,$"point enclosure {me}/{pe} at ({point.coord[0]},{point.coord[1]},{point.coord[2]})");
        int mc=0,pc=0;Check(KernelRuntime.EntityAskClass(mt,&mc),"self classification topology");Check(PK_ENTITY_ask_class(pt,&pc),"PK classification topology");Require(mc==pc,$"classification topology class {mc}/{pc}");
    }
}

static unsafe void CheckReferenceBody(int body,string name,Action<string> log)
{
    var options=new PK_BODY_check_o_t();options.max_faults=100;int count=0;PK_check_fault_t* faults=null;
    Check(PK_BODY_check(body,&options,&count,&faults),name+" full PK_BODY_check");
    try{for(var i=0;i<count;i++)log($"{name} fault={faults[i].state} entity={faults[i].entity_1} other={faults[i].entity_2}");Require(count==0,name+" geometry/topology faults: "+count);}
    finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free PK fault list");}
}
static string ScriptPath([CallerFilePath]string path="")=>path;
static void Check(int error,string operation){if(error!=0)throw new InvalidOperationException(operation+": "+error);}
static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}

static unsafe byte[] TransmitReference(PK_BODY_t body)
{
    var options = new PK_PART_transmit_o_t { transmit_format = PK_transmit_format_text_c, transmit_version = 371 };
    var block = new PK_MEMORY_block_t();
    Check(PK_PART_transmit_b(1, &body, &options, &block), "reference text transmit");
    try
    {
        using var bytes = new MemoryStream();
        for (var current = &block; current != null; current = current->next)
            bytes.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
        return bytes.ToArray();
    }
    finally { Check(PK_MEMORY_block_f(&block), "free reference text"); }
}

static unsafe byte[] TransmitManaged(ManagedBodyTag body)
{
    var options = new M.PK_PART_transmit_o_s
    {
        o_t_version = 4,
        transmit_format = M.ParasolidConstants.PK_transmit_format_text_c,
        transmit_version = 371,
        transmit_meshes = M.ParasolidConstants.PK_transmit_meshes_separate_c,
    };
    var block = new M.PK_MEMORY_block_s();
    Check(KernelRuntime.PartTransmitB(1, &body, &options, &block), "managed text transmit");
    try
    {
        using var bytes = new MemoryStream();
        for (var current = &block; current != null; current = current->next)
            bytes.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
        return bytes.ToArray();
    }
    finally { Check(KernelRuntime.MemoryBlockFree(&block), "free managed text"); }
}

static unsafe int ReceiveManaged(byte[] bytes)
{
    fixed(byte* pointer=bytes)
    {
        var block=new M.PK_MEMORY_block_s{bytes=pointer,n_bytes=(nuint)bytes.Length};
        var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=M.ParasolidConstants.PK_transmit_format_text_c};int count=0;int* parts=null;
        Check(KernelRuntime.PartReceiveB(block,&options,&count,&parts),"self receive independent PK body");
        try{Require(count==1&&parts!=null,"self receive count");return parts[0];}
        finally{if(parts!=null)Check(KernelRuntime.MemoryFree(parts),"free self receive list");}
    }
}

static unsafe PK_BODY_t ReceiveOracle(byte[] bytes)
{
    fixed (byte* pointer = bytes)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer);
        var options = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int count = 0;
        PK_PART_t* parts = null;
        Check(PK_PART_receive_b(block, &options, &count, &parts), "PK receive managed text");
        try
        {
            Require(count == 1 && parts != null, $"PK receive returned {count} parts");
            return parts[0];
        }
        finally { if (parts != null) Check(PK_MEMORY_free(parts), "free PK receive list"); }
    }
}

static unsafe void CompareBodies(PK_BODY_t reference,PK_BODY_t actual,string label,Action<string> log)
{
    ParasolidBodyCompare.AssertEquivalent(reference,actual,label);
}

static unsafe void CompareMass(int reference,int actual,string label,Action<string> log)
{
    Require(ParasolidMassProperties.Compare(reference,actual,label)!=MassAgreement.Different,label+" mass bounds prove a difference");
}

static unsafe int OracleCount(PK_BODY_t body, int kind)
{
    int count = 0;
    int* entities = null;
    var error = kind switch
    {
        0 => PK_BODY_ask_regions(body, &count, &entities),
        1 => PK_BODY_ask_shells(body, &count, &entities),
        2 => PK_BODY_ask_faces(body, &count, &entities),
        3 => PK_BODY_ask_edges(body, &count, &entities),
        _ => PK_BODY_ask_vertices(body, &count, &entities),
    };
    Check(error, "PK topology count " + kind);
    try { return count; }
    finally { if (entities != null) Check(PK_MEMORY_free(entities), "free PK topology list"); }
}

readonly record struct Fixture(string Name,double Ra,double Rb,double Dx,double Dy,double Dz,double X=0,double Y=0,double Z=0,bool Rotate=false);

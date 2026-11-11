#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property UseWebProgressCapture=true
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;
using M=ProjectGmKernel.Native.Generated;

Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
var outputName=args.Single(x=>x.StartsWith("--output-name=",StringComparison.Ordinal))[14..];
Require(outputName.Length>0&&outputName.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'),"one new output directory name");
var directory=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","mixed-analytic-boolean-oracle",outputName);
Require(!Directory.Exists(directory),"Existing evidence is immutable");Directory.CreateDirectory(directory);ParasolidMassProperties.BeginEvidence(directory);
var filter=args.FirstOrDefault(x=>!x.StartsWith("--",StringComparison.Ordinal));
if(!ParasolidScriptHost.TryStartSession("Independent mixed analytic finite-domain Boolean",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"own session");
    try
    {
        var passed=0;var failed=0;
        foreach(var kind in new[]{"basic","seam","mirrored","reload-partial","reload-partial-seam","repeat-partial"})
        foreach(var function in new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})
        {
            var name=$"{kind}-{function}";if(!WebProgressCapture.Select(name))continue;if(filter!=null&&!name.Contains(filter,StringComparison.Ordinal))continue;
            try
            {
                Exception? ownFailure=null,nativeFailure=null;int[] own=[],reference=[];var intermediate=new Dictionary<string,int>();
                try{own=Produce(true,kind,function,directory);}catch(Exception ex){ownFailure=ex;}
                try{reference=Produce(false,kind,function,directory,intermediate);}catch(Exception ex){nativeFailure=ex;}
                foreach(var stage in intermediate)
                {
                    try
                    {
                        var path=Path.Combine(directory,name+"-"+stage.Key+"-gm.x_t");if(!File.Exists(path))continue;
                        var stageBytes=File.ReadAllBytes(path);var fresh=Receive(stageBytes,false);CheckBody(fresh,name+" "+stage.Key+" received");CheckBody(stage.Value,name+" "+stage.Key+" independent");
                        if(WebProgressCapture.Enabled)WebProgressCapture.BindArchivedQuality(name+" "+stage.Key,stage.Value,fresh,stageBytes,Transmit(stage.Value,false));
                        Compare(stage.Value,fresh,name+" "+stage.Key);Console.WriteLine("GEOMETRY_TOPOLOGY_PASS "+name+" "+stage.Key);
                    }
                    finally{var copy=stage.Value;Check(PK_ENTITY_delete(1,&copy),"delete independent intermediate copy");}
                }
                if(ownFailure!=null||nativeFailure!=null)throw new AggregateException(new[]{ownFailure,nativeFailure}.OfType<Exception>());
                Require(own.Length==reference.Length,name+" result count");
                for(var i=0;i<own.Length;i++)
                {
                    var bytes=Transmit(own[i]);File.WriteAllBytes(Path.Combine(directory,name+"-"+i+".x_t"),bytes);
                    var referenceBytes=Transmit(reference[i],false);File.WriteAllBytes(Path.Combine(directory,name+"-"+i+"-native.x_t"),referenceBytes);
                    var received=Receive(bytes,false);CheckBody(received,name+" received");CheckBody(reference[i],name+" reference");
                    WebProgressCapture.BindQuality(name,own[i],reference[i],received,bytes,referenceBytes);
                    Compare(reference[i],received,name);Classify(own[i],reference[i],name);
                }
                Console.WriteLine("GEOMETRY_TOPOLOGY_PASS "+name);passed++;
            }
            catch(Exception ex){Console.WriteLine("FAIL "+name+": "+ex.Message);failed++;}
        }
        Require(failed==0,$"{failed} mixed analytic cases failed");Console.WriteLine($"GEOMETRY_TOPOLOGY_PASS all {passed} mixed analytic Boolean cases mass_unresolved={ParasolidMassProperties.UnresolvedCount}");
        if(ParasolidMassProperties.UnresolvedCount!=0)Environment.ExitCode=2;
    }
    finally{Check(KernelRuntime.SessionStop(),"own session stop");}
}

static unsafe int[] Produce(bool own,string kind,int function,string directory,Dictionary<string,int>? intermediate=null)
{
    var target=Block(own,0,0,0,4,4,2);var tool=Cylinder(own,kind=="seam"||kind.EndsWith("-seam",StringComparison.Ordinal));
    if(kind=="mirrored"){Mirror(own,target);Mirror(own,tool);}
    if(kind.StartsWith("reload-partial",StringComparison.Ordinal)||kind=="repeat-partial")
    {
        var first=Boolean(own,target,tool,PK_boolean_subtract_c,$"{kind}-{function} first-subtract");Require(first.Length==1,"first subtraction count");target=first[0];
        SaveStage("first-subtract",own,target,kind,function,directory,intermediate);
        if(own)
        {
            var bytes=Transmit(target);
            var original=target;target=Receive(bytes,true);Check(KernelRuntime.EntityDelete(1,&original),"delete self roundtrip source");
            Require(KernelRuntime.TryResolveBodySlot(target,out var slot)&&!KernelRuntime.Xt.IsOpaque(slot),"editable analytic roundtrip");
        }
        // A plane parallel to the cylinder axis cuts its finite angular
        // domain, turning the original closed ellipses into partial arcs.
        tool=Block(own,0,1.55,-.5,6,2.9,3);
        if(kind=="repeat-partial")
        {
            var second=Boolean(own,target,tool,PK_boolean_subtract_c,$"{kind}-{function} second-subtract");Require(second.Length==1,"second subtraction count");target=second[0];
            SaveStage("second-subtract",own,target,kind,function,directory,intermediate);
            if(own){var old=target;target=Receive(Transmit(target),true);Check(KernelRuntime.EntityDelete(1,&old),"delete second serialized source");}
            tool=Block(own,1,0,-.5,3.3,6,3);
        }
    }
    File.WriteAllBytes(Path.Combine(directory,$"{kind}-{function}-{(own?"gm":"pk")}-target-before.x_t"),Transmit(target,own));
    File.WriteAllBytes(Path.Combine(directory,$"{kind}-{function}-{(own?"gm":"pk")}-tool-before.x_t"),Transmit(tool,own));
    return Boolean(own,target,tool,function,$"{kind}-{function} final");
}
static unsafe void SaveStage(string stage,bool own,int body,string kind,int function,string directory,Dictionary<string,int>? intermediate)
{
    File.WriteAllBytes(Path.Combine(directory,$"{kind}-{function}-{stage}-{(own?"gm":"pk")}.x_t"),Transmit(body,own));
    if(!own&&intermediate!=null){int copy;Check(PK_ENTITY_copy(body,&copy),"retain independent intermediate product");intermediate.Add(stage,copy);}
}
static unsafe int[] Boolean(bool own,int target,int tool,int function,string label)
{
    var options=new PK_BODY_boolean_o_t{function=function,default_tol=1e-6,tracking=PK_LOGICAL_false};int n;int* result;
    WebProgressCapture.Before(own,target,[],"PK_BODY_boolean",[tool]);
    WebProgressCapture.BooleanParameters(own,options);
    var webStarted=System.Diagnostics.Stopwatch.GetTimestamp();
    var webError=own?KernelRuntime.BodyBoolean(target,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&result):PK_BODY_boolean(target,1,&tool,&options,&n,&result);
    var webMs=System.Diagnostics.Stopwatch.GetElapsedTime(webStarted).TotalMilliseconds;
    Console.WriteLine($"STEP_API side={(own?"gm":"pk")} case={label} error={webError} count={n}");
    WebProgressCapture.BooleanAfter(own,webError==0?new ReadOnlySpan<int>(result,n).ToArray():[],webMs,webError,0,null);Check(webError,"Boolean");
    try{return new ReadOnlySpan<int>(result,n).ToArray();}finally{if(result!=null)Check(own?KernelRuntime.MemoryFree(result):PK_MEMORY_free(result),"Boolean buffers");}
}
static unsafe int Block(bool own,double x,double y,double z,double width,double depth,double height)
{
    var basis=new PK_AXIS2_sf_t{location=new(x,y,z),axis=new(0,0,1),ref_direction=new(1,0,0)};int body;
    Check(own?KernelRuntime.BodyCreateSolidBlock(width,depth,height,(M.PK_AXIS2_sf_s*)&basis,&body):PK_BODY_create_solid_block(width,depth,height,&basis,&body),"block");return body;
}
static unsafe int Cylinder(bool own,bool seam)
{
    var basis=new PK_AXIS2_sf_t{location=new(-2.25,0,-2),axis=new(.6,0,.8),ref_direction=seam?new(0,1,0):new(.8,0,-.6)};int body;
    Check(own?KernelRuntime.BodyCreateSolidCyl(.3,7.5,(M.PK_AXIS2_sf_s*)&basis,&body):PK_BODY_create_solid_cyl(.3,7.5,&basis,&body),"oblique cylinder");return body;
}
static unsafe void Mirror(bool own,int body)
{
    var sf=new M.PK_TRANSF_sf_s();sf.matrix[0]=-1;sf.matrix[5]=sf.matrix[10]=sf.matrix[15]=1;int transform;
    Check(own?KernelRuntime.TransfCreate(&sf,&transform):PK_TRANSF_create((PK_TRANSF_sf_t*)&sf,&transform),"mirror transform");
    int n;int* replaced;PK_LOGICAL_t* exact;
    Check(own?KernelRuntime.BodyTransform(body,transform,0,&n,&replaced,(byte**)&exact):PK_BODY_transform(body,transform,0,&n,&replaced,&exact),"mirror input body");
    if(replaced!=null)Check(own?KernelRuntime.MemoryFree(replaced):PK_MEMORY_free(replaced),"mirror topology buffers");
    if(exact!=null)Check(own?KernelRuntime.MemoryFree(exact):PK_MEMORY_free(exact),"mirror exact buffers");
    Check(own?KernelRuntime.EntityDelete(1,&transform):PK_ENTITY_delete(1,&transform),"delete mirror transform");
}
static unsafe byte[] Transmit(int body,bool own=true)
{
    var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=PK_transmit_format_text_c,transmit_version=371,transmit_meshes=PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s block;
    Check(own?KernelRuntime.PartTransmitB(1,&body,&options,&block):PK_PART_transmit_b(1,&body,(PK_PART_transmit_o_t*)&options,(PK_MEMORY_block_t*)&block),"text XT transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(own?KernelRuntime.MemoryBlockFree(&block):PK_MEMORY_block_f((PK_MEMORY_block_t*)&block),"XT buffers");}
}
static unsafe int Receive(byte[] bytes,bool own)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&options,&n,&parts):PK_PART_receive_b(block,&options,&n,&parts),own?"self receive":"real PK receive");
        try{Require(n==1,"one received body");return parts[0];}finally{if(parts!=null)Check(own?KernelRuntime.MemoryFree(parts):PK_MEMORY_free(parts),"receive buffers");}
    }
}
static unsafe void CheckBody(int body,string name)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&n,&faults),"full BODY_check");
    try{Console.WriteLine($"FULL {name} faults={n}");for(var i=0;i<n;i++)Console.WriteLine($"{name} fault={faults[i].state}, entity={faults[i].entity_1}/{faults[i].entity_2}");Require(n==0,name+" body faults="+n);}
    finally{if(faults!=null)PK_MEMORY_free(faults);}
}
static unsafe void Compare(int reference,int actual,string name)
{
    for(var kind=0;kind<5;kind++){var a=Count(reference,kind);var b=Count(actual,kind);Console.WriteLine($"{name} topology {kind}: native={a} self={b}");Require(a==b,name+" topology count "+kind);}
    ParasolidBodyCompare.AssertEquivalent(reference,actual,name);
    Require(ParasolidMassProperties.Compare(reference,actual,name)!=MassAgreement.Different,name+" mass bounds differ");
}
static unsafe int Count(int body,int kind)
{
    int n;int* tags;Check(kind switch{0=>PK_BODY_ask_regions(body,&n,&tags),1=>PK_BODY_ask_shells(body,&n,&tags),2=>PK_BODY_ask_faces(body,&n,&tags),3=>PK_BODY_ask_edges(body,&n,&tags),_=>PK_BODY_ask_vertices(body,&n,&tags)},"count");
    try{return n;}finally{if(tags!=null)PK_MEMORY_free(tags);}
}
static unsafe void Classify(int own,int reference,string name)
{
    var count=0;
    foreach(var x in new[]{-2.4,-1.3,0,.25,1.3,2.4})foreach(var y in new[]{-2.3,-.2,.15,.5,2.2})foreach(var z in new[]{-.3,.25,1,1.3,2.3})
    {
        var point=new PK_VECTOR_t(x,y,z);int expected,actual,nt,mt;
        Check(PK_BODY_contains_vector(reference,point,&expected,&nt),"native contains");Check(KernelRuntime.BodyContainsVector(own,*(M.PK_VECTOR_s*)&point,&actual,&mt),"self contains");
        Require(actual==expected,$"{name} enclosure ({x},{y},{z}): {actual}/{expected}");
        int nc,mc;Check(PK_ENTITY_ask_class(nt,&nc),"native containing class");Check(KernelRuntime.EntityAskClass(mt,&mc),"self containing class");Require(nc==mc,name+" containing class");count++;
        if(nc==PK_CLASS_region){PK_LOGICAL_t nativeSolid;byte selfSolid;Check(PK_REGION_is_solid(nt,&nativeSolid),"native region material");Check(KernelRuntime.RegionIsSolid(mt,&selfSolid),"self region material");Require(selfSolid==(byte)nativeSolid,name+" region material semantics");}
    }
    Console.WriteLine($"{name} classified {count} independent vectors");
}
static void Check(int error,string name){if(error!=0)throw new Exception($"{name}: error={error}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, sheet={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, t={KernelRuntime.LastSheetParameter:R}");}
static void Require(bool value,string message){if(!value)throw new Exception(message);}
static string ScriptPath([CallerFilePath]string path="")=>path;

#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UseWebProgressCapture=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=LocalOperationOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;
Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));

var directory=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","local-operation-oracle"));
var outputName=args.Single(x=>x.StartsWith("--output-name=",StringComparison.Ordinal))["--output-name=".Length..];
if(outputName.Length==0||!outputName.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'))throw new ArgumentException("one new output directory name is required");
directory=Path.Combine(directory,outputName);
if(Directory.Exists(directory))throw new InvalidOperationException("Existing execution evidence is immutable");
Directory.CreateDirectory(directory);var report=new StringBuilder();
ParasolidMassProperties.BeginEvidence(directory);
var axialArrangement=args.Contains("--axial-arrangement",StringComparer.Ordinal);
var blendArrangement=args.Contains("--blend-arrangement",StringComparer.Ordinal);
var thenBoolean=args.Contains("--then-boolean",StringComparer.Ordinal);
var booleanInject=args.Contains("--boolean-inject",StringComparer.Ordinal);
if(thenBoolean&&(!blendArrangement||!args.Contains("--lifecycle",StringComparer.Ordinal))||booleanInject&&!thenBoolean)
    throw new ArgumentException("post-Blend Boolean requires --blend-arrangement --lifecycle; injection requires --then-boolean");
var trace=axialArrangement||blendArrangement?new List<KernelRuntime.BoundaryTrace>():null;
if(trace!=null){KernelRuntime.ForbidLegacyRegularBlendAssembly=true;KernelRuntime.TraceFiniteBoundary=trace.Add;}
void Log(string message){Console.WriteLine(message);report.AppendLine(message);}
try
{
    if(!ParasolidScriptHost.TryStartSession("Independent local operation oracle",out var host,out var reason))throw new Exception(reason);
    using(host)
    unsafe
    {
        var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed session start");
        try
        {
            var faceQueryControls=args.Contains("--face-query-controls",StringComparer.Ordinal);
            if(faceQueryControls)RunFaceQueryControls(directory,Log);
            var cases=new (string Name,string Shape,string Operation,double Amount,string Selection,bool Oriented)[]
            {
                ("block-offset-out","block","offset",.4,"",false),("block-offset-in","block","offset",-.4,"",false),
                ("oriented-block-offset","block","offset",.3,"",true),("oblique-block-offset","oblique","offset",.3,"",false),
                ("prism-offset","prism","offset",.3,"",false),("block-face-offset","block","face-offset",.4,"top",false),
                ("sphere-offset","sphere","offset",.4,"",false),("torus-offset","torus","offset",-.2,"",false),
                ("cylinder-offset","cylinder","offset",.3,"",false),("cone-offset","cone","offset",.3,"",false),
                ("block-closed-hollow-in","block","hollow",-.3,"",false),("block-closed-hollow-out","block","hollow",.3,"",false),
                ("block-pierce-in","block","hollow",-.3,"top",false),("block-pierce-out","block","hollow",.3,"top",false),
                ("block-double-pierce","block","hollow",-.3,"both",false),("block-varied-wall","block","varied-hollow",-.3,"top",false),
                ("oriented-pierce","block","hollow",-.3,"top",true),("oblique-hollow","oblique","hollow",-.25,"top",false),
                ("sphere-hollow","sphere","hollow",-.3,"",false),("torus-hollow","torus","hollow",-.2,"",false),
                ("cylinder-hollow","cylinder","hollow",-.3,"",false),("cylinder-pierce","cylinder","hollow",-.3,"top",false),
                ("cone-hollow","cone","hollow",-.3,"",false),("cone-pierce","cone","hollow",-.3,"top",false),
                ("block-single-fillet","block","fillet",.4,"single",false),("block-parallel-fillet","block","fillet",.4,"parallel",false),
                ("block-all-fillet","block","fillet",.4,"all",false),("block-single-chamfer","block","chamfer",.4,"single",false),
                ("block-parallel-chamfer","block","chamfer",.4,"parallel",false),("block-all-chamfer","block","chamfer",.4,"all",false),
                ("block-unequal-chamfer","block","unequal-chamfer",.3,"single",false),("oblique-unequal-chamfer","oblique","unequal-chamfer",.3,"single",false),
                ("oriented-all-fillet","block","fillet",.4,"all",true),("prism-all-fillet","prism","fillet",.3,"all",false),
                ("prism-all-chamfer","prism","chamfer",.3,"all",false),("oblique-single-fillet","oblique","fillet",.3,"single",false),
                ("cylinder-rim-fillet","cylinder","fillet",.4,"single",false),("cylinder-both-rim-fillet","cylinder","fillet",.4,"all",false),
                ("cylinder-rim-chamfer","cylinder","chamfer",.4,"single",false),("cylinder-both-rim-chamfer","cylinder","chamfer",.4,"all",false),
                ("cylinder-unequal-chamfer","cylinder","unequal-chamfer",.3,"single",false),
            };
            var failures=0;var executed=0;
            foreach(var c in cases)
            {
                if(faceQueryControls)continue;
                if(!WebProgressCapture.Select(c.Name))continue;
                if(trace!=null&&(c.Operation is not ("fillet" or "chamfer" or "unequal-chamfer")||axialArrangement&&c.Shape!="cylinder"))continue;
                executed++;
                try
                {
                    if(args.Contains("--native-gauge-control",StringComparer.Ordinal))
                    {
                        NativeGaugeControl(c.Name,c.Shape,c.Operation,c.Amount,c.Selection,c.Oriented,directory,Log);continue;
                    }
                    var ownedBefore=(KernelRuntime.Bodies.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Points.AliveCount,KernelRuntime.ReturnOutstandingBlocks);
                    WebProgressCapture.Context=c.Name;
                    WebProgressCapture.ContextSource=new JsonObject{["kind"]="independent-construction",["shape"]=c.Shape,["operation"]=c.Operation,["amount"]=c.Amount,["selection"]=c.Selection,["oriented"]=c.Oriented};
                    var managed=Create(true,c.Shape,c.Oriented);var reference=Create(false,c.Shape,c.Oriented);
                    if(args.Contains("--seed-ownership-control",StringComparer.Ordinal))
                    {
                        Check(KernelRuntime.EntityDelete(1,&managed),"delete diagnostic seed");Check(PK_ENTITY_delete(1,&reference),"delete diagnostic native seed");
                        var seedAfter=(KernelRuntime.Bodies.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Points.AliveCount,KernelRuntime.ReturnOutstandingBlocks);
                        Require(ownedBefore==seedAfter,$"original seed cleanup before={ownedBefore} after={seedAfter}");Log("SEED_OWNERSHIP_PASS "+c.Name);continue;
                    }
                    var seedAuthor=args.SingleOrDefault(x=>x.StartsWith("--seed-author=",StringComparison.Ordinal))?["--seed-author=".Length..];
                    if(seedAuthor!=null)
                    {
                        Require(seedAuthor is "gm" or "pk","seed author must be gm or pk");
                        var input=seedAuthor=="gm"?TransmitManaged(managed):TransmitReference(reference);
                        File.WriteAllBytes(Path.Combine(directory,c.Name+"-input.x_t"),input);
                        Log($"INPUT {c.Name} author={seedAuthor} sha256={Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(input))} both operations receive identical unmodified seed bytes");
                        Check(KernelRuntime.EntityDelete(1,&managed),"delete independent managed seed");Check(PK_ENTITY_delete(1,&reference),"delete independent native seed");
                        managed=ReceiveManaged(input);reference=Receive(input);
                    }
                    trace?.Clear();
                    var a=Apply(true,managed,c.Shape,c.Operation,c.Amount,c.Selection,c.Oriented);
                    if(trace!=null)
                    {
                        var products=trace.Where(t=>t.Stage==KernelRuntime.BoundaryTraceStage.Product).ToArray();
                        Require(products.Length>0,"blend operation used the shared arrangement");
                        foreach(var p in products)Require(trace.Any(t=>t.Stage==KernelRuntime.BoundaryTraceStage.Arrangement&&t.Source==p.Source&&t.Edge==p.Edge
                            &&t.Curve==p.Curve&&t.Generation==p.Generation&&t.StartEvent==p.StartEvent&&t.EndEvent==p.EndEvent
                            &&t.First==p.First&&t.Last==p.Last),"blend product boundary provenance");
                        Log($"{c.Name} common arrangement provenance uses={products.Length}");
                    }
                    var b=Apply(false,reference,c.Shape,c.Operation,c.Amount,c.Selection,c.Oriented);
                    Require(a==b,c.Name+" returned topology/tracking contract differs: "+a+" / "+b);
                    var bytes=TransmitManaged(managed);File.WriteAllBytes(Path.Combine(directory,c.Name+"-managed.x_t"),bytes);
                    var referenceBytes=TransmitReference(reference);File.WriteAllBytes(Path.Combine(directory,c.Name+"-reference.x_t"),referenceBytes);
                    var received=Receive(bytes);
                    WebProgressCapture.BindQuality(c.Name,managed,reference,received,bytes,referenceBytes);
                    for(var kind=0;kind<5;kind++){var actual=Count(received,kind);var expected=Count(reference,kind);Require(actual==expected,$"{c.Name} topology kind {kind}: {actual}/{expected}");}
                    CheckBody(reference,c.Name+" independent",Log);CheckBody(received,c.Name+" received",Log);CompareMass(reference,received,c.Name,Log);
                    CompareMaterialSense(reference,received,c.Name);
                    CompareManagedPlaneOrientations(managed,received,c.Name,Log,args.Contains("--managed-material",StringComparer.Ordinal));
                    try{ParasolidBodyCompare.AssertEquivalent(reference,received,c.Name+" strict");}
                    catch{RecordFiniteSurfaces(reference,c.Name+" native",Log);RecordFiniteSurfaces(received,c.Name+" GM fresh",Log);RecordFiniteCurves(reference,c.Name+" native",Log);RecordFiniteCurves(received,c.Name+" GM fresh",Log);throw;}
                    Log($"UPSTREAM_OPERATION_PASS {c.Name} GM_contract={a} PK_contract={b} independent_geometry_topology=True");
                    if(args.Contains("--lifecycle",StringComparer.Ordinal))
                        (managed,reference)=VerifyLifecycle(managed,reference,c.Name,directory,Log,args.Contains("--managed-material",StringComparer.Ordinal));
                    if(thenBoolean)managed=VerifyFollowingBoolean(managed,c.Name,directory,Log,booleanInject);
                    Check(KernelRuntime.EntityDelete(1,&managed),"delete managed result");Check(PK_ENTITY_delete(1,&reference),"delete reference");Check(PK_ENTITY_delete(1,&received),"delete received");
                    var ownedAfter=(KernelRuntime.Bodies.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Points.AliveCount,KernelRuntime.ReturnOutstandingBlocks);
                    Require(ownedBefore==ownedAfter,$"Blend product geometry/return memory ownership cleanup before={ownedBefore} after={ownedAfter}");
                    Log(booleanInject?"ROLLBACK_SCENARIO_PASS "+c.Name+"; upstream Blend/lifecycle passed, GM Boolean failed as injected":"GEOMETRY_TOPOLOGY_PASS "+c.Name);
                }
                catch(Exception ex)
                {
                    failures++;Log("FAIL "+c.Name+": "+ex.Message);
                    if(trace!=null)
                    {
                        Log($"  arrangement stage={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} t={KernelRuntime.LastSheetParameter:R} uv=({KernelRuntime.LastSheetRayU:R},{KernelRuntime.LastSheetRayV:R})");
                        foreach(var use in trace)Log($"  boundary {use.Stage} face={use.Source} surface={use.Surface} edge={use.Edge}/{use.Curve} forward={use.Forward} t=[{use.First:R},{use.Last:R}]");
                    }
                }
            }
            if(faceQueryControls)Log("FACE_QUERY_CONTROLS_PASS cases=4; query fixtures, no Blend construction qualified");
            else Log($"{(booleanInject?"Post-Blend rollback controls":"Local operation geometry")}: {executed-failures} passed, {failures} failed, mass_unresolved={ParasolidMassProperties.UnresolvedCount}.");Require(failures==0,"Local operation oracle failed.");
            if(ParasolidMassProperties.UnresolvedCount!=0)Environment.ExitCode=2;
        }
        finally{Check(KernelRuntime.SessionStop(),"managed session stop");}
    }
}
catch(Exception ex){Log("FAIL: "+ex.Message);Environment.ExitCode=1;}
finally{File.WriteAllText(Path.Combine(directory,"report.txt"),report.ToString());}

static unsafe void RunFaceQueryControls(string directory,Action<string> log)
{
    foreach(var negativeFace in new[]{false,true})foreach(var negativeSurface in new[]{false,true})
    {
        var own=Create(true,"block",false);var face=Tags(true,own,false)[0];ref var record=ref KernelRuntime.Faces[KernelRuntime.ResolveTagRecord(face).Slot];
        ref var surface=ref KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(record.SurfTag)];
        record.Orientation=negativeFace?M.ParasolidConstants.PK_TOPOL_sense_negative_c:M.ParasolidConstants.PK_TOPOL_sense_positive_c;
        surface.Sense=negativeSurface?M.ParasolidConstants.PK_TOPOL_sense_negative_c:M.ParasolidConstants.PK_TOPOL_sense_positive_c;
        var label=$"face-{negativeFace}-surface-{negativeSurface}";var bytes=TransmitManaged(own);File.WriteAllBytes(Path.Combine(directory,label+".x_t"),bytes);var fresh=Receive(bytes);
        try
        {
            CompareManagedPlaneOrientations(own,fresh,label,log,false);
            int queried=0;byte orientation=0;Check(KernelRuntime.FaceAskOrientedSurf(face,&queried,&orientation),"control face query");
            if(negativeFace==negativeSurface)CheckBody(fresh,label+" geometry-preserving fixture",log);
            log($"FACE_SIGN_QUERY_CONTROL face_negative={negativeFace} surface_negative={negativeSurface} GM_PK_orientation={orientation!=0} query_only=True");
        }
        finally{Check(KernelRuntime.EntityDelete(1,&own),"delete GM face-query fixture");Check(PK_ENTITY_delete(1,&fresh),"delete PK face-query fixture");}
        Require(KernelRuntime.Bodies.AliveCount==0&&KernelRuntime.Curves.AliveCount==0&&KernelRuntime.Surfaces.AliveCount==0&&KernelRuntime.Points.AliveCount==0,"face-query fixture cleanup");
    }
}
static unsafe int Create(bool managed,string shape,bool oriented)
{
    var basis=new PK_AXIS2_sf_t(new PK_VECTOR_t(oriented?7:0,oriented?-4:0,oriented?2:0),
        oriented?new PK_VECTOR1_t(1/Math.Sqrt(14),2/Math.Sqrt(14),3/Math.Sqrt(14)):new PK_VECTOR1_t(0,0,1),
        oriented?new PK_VECTOR1_t(2/Math.Sqrt(5),-1/Math.Sqrt(5),0):new PK_VECTOR1_t(1,0,0));
    int body=0,error;
    if(shape=="oblique")
    {
        PK_VECTOR_t* points=stackalloc PK_VECTOR_t[4]{new(-2,-3,0),new(2,-3,0),new(2,3,0),new(-2,3,0)};
        var sheet=new PK_BODY_create_sheet_planar_o_t();sheet.plane.axis=new PK_VECTOR1_t(0,0,1);
        Check(managed?KernelRuntime.BodyCreateSheetPlanar(4,(M.PK_VECTOR_s*)points,(M.PK_BODY_create_sheet_planar_o_s*)&sheet,&body):PK_BODY_create_sheet_planar(4,points,&sheet,&body),"create independent oblique profile");
        var ext=new PK_BODY_extrude_o_t();ext.end_bound.distance=8;var path=new PK_VECTOR1_t(.35,0,Math.Sqrt(1-.35*.35));int output=0;
        if(managed){var tracking=new M.PK_TOPOL_track_r_s();var local=new M.PK_TOPOL_local_r_s();Check(KernelRuntime.BodyExtrude(body,*(M.PK_VECTOR_s*)&path,(M.PK_BODY_extrude_o_s*)&ext,&output,&tracking,&local),"managed oblique extrusion");KernelRuntime.TopolTrackResultFree(&tracking);KernelRuntime.TopolLocalResultFree(&local);KernelRuntime.EntityDelete(1,&body);}
        else{var tracking=new PK_TOPOL_track_r_t();var local=new PK_TOPOL_local_r_t();Check(PK_BODY_extrude(body,path,&ext,&output,&tracking,&local),"independent PK oblique extrusion");PK_TOPOL_track_r_f(&tracking);PK_TOPOL_local_r_f(&local);PK_ENTITY_delete(1,&body);}
        return output;
    }
    if(managed)
        error=shape switch
        {
            "sphere"=>KernelRuntime.BodyCreateSolidSphere(3,(M.PK_AXIS2_sf_s*)&basis,&body),"torus"=>KernelRuntime.BodyCreateSolidTorus(5,1,(M.PK_AXIS2_sf_s*)&basis,&body),
            "cylinder"=>KernelRuntime.BodyCreateSolidCyl(3,6,(M.PK_AXIS2_sf_s*)&basis,&body),"cone"=>KernelRuntime.BodyCreateSolidCone(3,6,.15,(M.PK_AXIS2_sf_s*)&basis,&body),
            "prism"=>KernelRuntime.BodyCreateSolidPrism(3,6,5,(M.PK_AXIS2_sf_s*)&basis,&body),_=>KernelRuntime.BodyCreateSolidBlock(4,6,8,(M.PK_AXIS2_sf_s*)&basis,&body),
        };
    else
        error=shape switch
        {
            "sphere"=>PK_BODY_create_solid_sphere(3,&basis,&body),"torus"=>PK_BODY_create_solid_torus(5,1,&basis,&body),"cylinder"=>PK_BODY_create_solid_cyl(3,6,&basis,&body),
            "cone"=>PK_BODY_create_solid_cone(3,6,.15,&basis,&body),"prism"=>PK_BODY_create_solid_prism(3,6,5,&basis,&body),_=>PK_BODY_create_solid_block(4,6,8,&basis,&body),
        };
    Check(error,(managed?"managed":"independent PK")+" create "+shape);return body;
}
static unsafe void NativeGaugeControl(string label,string shape,string operation,double amount,string selection,bool oriented,string directory,Action<string> log)
{
    var seed=Create(true,shape,oriented);var gmInput=TransmitManaged(seed);Check(KernelRuntime.EntityDelete(1,&seed),"delete diagnostic GM input");
    var fromGm=Receive(gmInput);var fromPk=Create(false,shape,oriented);var pkInput=TransmitReference(fromPk);
    File.WriteAllBytes(Path.Combine(directory,label+"-gm-seed.x_t"),gmInput);File.WriteAllBytes(Path.Combine(directory,label+"-pk-seed.x_t"),pkInput);
    log("NATIVE_GAUGE_CONTROL: both modifications below are real PK; no GM Blend result is qualified");
    try
    {
        CheckBody(fromPk,label+" native seed",log);CheckBody(fromGm,label+" GM seed received by PK",log);
        ParasolidBodyCompare.AssertEquivalent(fromPk,fromGm,label+" seeds strict");
        var a=Apply(false,fromGm,shape,operation,amount,selection,oriented);var b=Apply(false,fromPk,shape,operation,amount,selection,oriented);Require(a==b,"native control product return contract");
        var aBytes=TransmitReference(fromGm);var bBytes=TransmitReference(fromPk);
        File.WriteAllBytes(Path.Combine(directory,label+"-native-from-gm-seed.x_t"),aBytes);File.WriteAllBytes(Path.Combine(directory,label+"-native-from-pk-seed.x_t"),bBytes);
        var first=Receive(aBytes);var second=Receive(bBytes);
        try
        {
            CheckBody(first,label+" native result from GM seed",log);CheckBody(second,label+" native result from PK seed",log);
            CompareMaterialSense(second,first,label+" native gauge control");CompareMass(second,first,label+" native gauge control",log);
            ParasolidBodyCompare.AssertEquivalent(second,first,label+" native results strict");
        }
        finally{Check(PK_ENTITY_delete(1,&first),"delete native control fresh GM-seed result");Check(PK_ENTITY_delete(1,&second),"delete native control fresh PK-seed result");}
    }
    finally{Check(PK_ENTITY_delete(1,&fromGm),"delete native control GM-seed result");Check(PK_ENTITY_delete(1,&fromPk),"delete native control PK-seed result");}
}

static unsafe string Apply(bool managed,int body,string shape,string operation,double amount,string selection,bool oriented)
{
    var originalFaces=Tags(managed,body,false);var signature="";
    if(operation is "fillet" or "chamfer" or "unequal-chamfer")
    {
        var edges=SelectEdges(managed,body,shape,selection,oriented);
        if(Environment.GetEnvironmentVariable("GM_BLEND_REVERSE_MARKS")=="1")Array.Reverse(edges);
        var round=new PK_EDGE_set_blend_constant_o_t();var flat=new PK_EDGE_set_blend_chamfer_o_t();
        if(Environment.GetEnvironmentVariable("GM_BLEND_DIRECTIONS")=="1")
        {
            var view=managed?Receive(TransmitManaged(body)):body;
            RecordFiniteCurves(view,managed?"GM source":"PK source",Console.WriteLine);
            if(managed)Check(PK_ENTITY_delete(1,&view),"delete source diagnostic receive");
            Console.WriteLine("MARK_ORDER "+string.Join(",",edges));
        }
        var firstFace=operation=="unequal-chamfer"?ChamferFirstFace(managed,body,shape,oriented):0;
        var secondRange=operation=="unequal-chamfer"?amount*2:amount;
        fixed(int* input=edges)
        {
            int count=0;int* output=null;
            var designated=firstFace==0?null:&firstFace;
            var error=managed?(operation=="fillet"?KernelRuntime.EdgeSetBlendConstant(edges.Length,input,amount,(M.PK_EDGE_set_blend_constant_o_s*)&round,&count,&output):KernelRuntime.EdgeSetBlendChamfer(edges.Length,input,secondRange,amount,designated,(M.PK_EDGE_set_blend_chamfer_o_s*)&flat,&count,&output)):
                operation=="fillet"?PK_EDGE_set_blend_constant(edges.Length,input,amount,&round,&count,&output):PK_EDGE_set_blend_chamfer(edges.Length,input,secondRange,amount,designated,&flat,&count,&output);
            Check(error,"set "+operation);Require(count==edges.Length,"set blend edge count");Free(managed,output);
            if(Environment.GetEnvironmentVariable("GM_BLEND_DIRECTIONS")=="1")foreach(var selected in edges)
            {
                if(managed)
                {
                    var slot=KernelRuntime.ResolveTagRecord(selected).Slot;var fin=KernelRuntime.Edges[slot].FirstFinEdge;
                    var left=KernelRuntime.Faces[KernelRuntime.Loops[KernelRuntime.Fins[fin].Loop].Face].Header.Tag;
                    var right=KernelRuntime.Faces[KernelRuntime.Loops[KernelRuntime.Fins[KernelRuntime.Fins[fin].Other].Loop].Face].Header.Tag;
                    Console.WriteLine($"BLEND_SIDES GM edge={selected} left={left} right={right} first_fin={KernelRuntime.Fins[fin].Sense}");
                }
                else
                {
                    int left=0,right=0,cliff=0;PK_blend_type_t type=default;var shapeInfo=new PK_blend_edge_shape_t();var properties=new PK_blend_properties_t();
                    Check(PK_EDGE_ask_blend(selected,&type,&left,&right,&shapeInfo,&properties,&cliff),"native marked sides");Console.WriteLine($"BLEND_SIDES PK edge={selected} left={left} right={right}");
                }
            }
        }
        var fix=new PK_BODY_fix_blends_o_t();fix.local_check=PK_LOGICAL_true;int made=0;int* blends=null;int* replaced=null;int fault=0,faultEdge=0,faultTopol=0;
        WebProgressCapture.Before(managed,body,edges);
        WebProgressCapture.BlendParameters(managed,fix,amount,operation=="fillet"?round.properties.tolerance:flat.properties.tolerance,operation=="fillet"?"PK_EDGE_set_blend_constant":"PK_EDGE_set_blend_chamfer",operation=="fillet"?null:secondRange,firstFace==0?null:firstFace);
        if(managed)
        {
            M.PK_FACE_array_s* unders=null;var webStarted=System.Diagnostics.Stopwatch.GetTimestamp();var webError=KernelRuntime.BodyFixBlends(body,(M.PK_BODY_fix_blends_o_s*)&fix,&made,&blends,&unders,&replaced,&fault,&faultEdge,&faultTopol);var webMs=System.Diagnostics.Stopwatch.GetElapsedTime(webStarted).TotalMilliseconds;WebProgressCapture.After(true,body,webMs,webError,made,blends,fault,(PK_FACE_array_t*)unders,replaced);Check(webError,"managed fix blends");
            var records=new List<int>();for(var i=0;i<made;i++){records.Add(unders[i].length);Free(true,unders[i].array);}Free(true,unders);records.Sort();signature="blends:"+string.Join(",",records);
        }
        else
        {
            PK_FACE_array_t* unders=null;var webStarted=System.Diagnostics.Stopwatch.GetTimestamp();var webError=PK_BODY_fix_blends(body,&fix,&made,&blends,&unders,&replaced,&fault,&faultEdge,&faultTopol);var webMs=System.Diagnostics.Stopwatch.GetElapsedTime(webStarted).TotalMilliseconds;WebProgressCapture.After(false,body,webMs,webError,made,blends,fault,unders,replaced);Check(webError,"independent PK fix blends");
            var records=new List<int>();for(var i=0;i<made;i++){records.Add(unders[i].length);Free(false,unders[i].array);}Free(false,unders);records.Sort();signature="blends:"+string.Join(",",records);
        }
        Require(fault==PK_blend_fault_no_fault_c,"fix blend fault: "+fault);Free(managed,blends);Free(managed,replaced);
    }
    else
    {
        var chosen=selection.Length==0?Array.Empty<int>():SelectFaces(managed,body,selection,oriented);var bodyOptions=new PK_BODY_offset_o_t();var faceOptions=new PK_FACE_offset_o_t();var hollowOptions=new PK_BODY_hollow_o_t();
        fixed(int* selected=chosen)
        {
            hollowOptions.n_pierce_faces=chosen.Length;hollowOptions.pierce_faces=selected;double varied=amount*1.7;int variedFace=0;
            if(operation=="varied-hollow"){variedFace=SelectFaces(managed,body,"side",oriented)[0];hollowOptions.n_offset_faces=1;hollowOptions.offset_faces=&variedFace;hollowOptions.offset_values=&varied;}
            if(managed)
            {
                var track=new M.PK_TOPOL_track_r_s();var local=new M.PK_TOPOL_local_r_s();
                var error=operation switch
                {
                    "offset"=>KernelRuntime.BodyOffset2(body,amount,1e-6,(M.PK_BODY_offset_o_s*)&bodyOptions,&track,&local),
                    "face-offset"=>KernelRuntime.FaceOffset2(1,selected,&amount,1e-6,(M.PK_FACE_offset_o_s*)&faceOptions,&track,&local),
                    _=>KernelRuntime.BodyHollow2(body,amount,1e-6,(M.PK_BODY_hollow_o_s*)&hollowOptions,&track,&local),
                };
                Check(error,"managed "+operation);Require(local.status==M.ParasolidConstants.PK_local_status_ok_c,"managed local status");signature="tracking:"+track.n_track_records;
                for(var i=0;i<track.n_track_records;i++)Require(track.track_records[i].track==M.ParasolidConstants.PK_TOPOL_track_create_c&&track.track_records[i].n_original_topols==1&&track.track_records[i].n_product_topols==1,"managed hollow tracking record");
                KernelRuntime.TopolTrackResultFree(&track);KernelRuntime.TopolLocalResultFree(&local);
            }
            else
            {
                var track=new PK_TOPOL_track_r_t();var local=new PK_TOPOL_local_r_t();
                var error=operation switch
                {
                    "offset"=>PK_BODY_offset_2(body,amount,1e-6,&bodyOptions,&track,&local),"face-offset"=>PK_FACE_offset_2(1,selected,&amount,1e-6,&faceOptions,&track,&local),
                    _=>PK_BODY_hollow_2(body,amount,1e-6,&hollowOptions,&track,&local),
                };
                Check(error,"independent PK "+operation);Require(local.status==PK_local_status_ok_c,"PK local status");signature="tracking:"+track.n_track_records;PK_TOPOL_track_r_f(&track);PK_TOPOL_local_r_f(&local);
            }
        }
    }
    var after=Tags(managed,body,false);foreach(var face in originalFaces)Require(after.Contains(face),"original face identity lost");return signature;
}
static unsafe int ChamferFirstFace(bool managed,int body,string shape,bool oriented)
{
    if(shape!="cylinder")return SelectFaces(managed,body,"side",oriented)[0];
    foreach(var face in Tags(managed,body,false))
    {
        int surface=0,cls=0;Check(managed?KernelRuntime.FaceAskSurf(face,&surface):PK_FACE_ask_surf(face,&surface),"chamfer support surface");
        Check(managed?KernelRuntime.EntityAskClass(surface,&cls):PK_ENTITY_ask_class(surface,&cls),"chamfer support class");if(cls==PK_CLASS_cyl)return face;
    }
    throw new InvalidOperationException("missing cylindrical chamfer support");
}

static unsafe int[] Tags(bool managed,int body,bool edges)
{
    int count=0;int* values=null;Check(managed?(edges?KernelRuntime.BodyAskEdges(body,&count,&values):KernelRuntime.BodyAskFaces(body,&count,&values)):
        edges?PK_BODY_ask_edges(body,&count,&values):PK_BODY_ask_faces(body,&count,&values),"body topology tags");
    try{return new ReadOnlySpan<int>(values,count).ToArray();}finally{Free(managed,values);}
}
static (double X,double Y,double Z) Axis(bool oriented)=>oriented?(1/Math.Sqrt(14),2/Math.Sqrt(14),3/Math.Sqrt(14)):(0,0,1);
static double Dot((double X,double Y,double Z) a,(double X,double Y,double Z) b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
static unsafe int[] SelectFaces(bool managed,int body,string selection,bool oriented)
{
    var result=new List<int>();var axis=Axis(oriented);var best=0;var score=double.NegativeInfinity;
    foreach(var face in Tags(managed,body,false))
    {
        int surface=0,cls=0;byte sense=0;
        Check(managed?KernelRuntime.FaceAskOrientedSurf(face,&surface,&sense):PK_FACE_ask_oriented_surf(face,&surface,(PK_LOGICAL_t*)&sense),"face oriented surface");
        Check(managed?KernelRuntime.EntityAskClass(surface,&cls):PK_ENTITY_ask_class(surface,&cls),"surface class");if(cls!=PK_CLASS_plane)continue;
        var sf=new PK_PLANE_sf_t();Check(managed?KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&sf):PK_PLANE_ask(surface,&sf),"plane ask");
        var n=(sf.basis_set.axis.coord[0]*(sense==0?-1:1),sf.basis_set.axis.coord[1]*(sense==0?-1:1),sf.basis_set.axis.coord[2]*(sense==0?-1:1));var dot=Dot(n,axis);
        if(selection=="both"&&Math.Abs(dot)>.999)result.Add(face);
        else if(selection=="top"&&dot>.999)result.Add(face);
        else if(selection=="side")
        {
            var s=n.Item1+2*n.Item2;if(Math.Abs(dot)<.8&&s>score){score=s;best=face;}
        }
    }
    if(selection=="side"&&best!=0)result.Add(best);Require(result.Count>0,"face selection empty");return result.ToArray();
}
static unsafe int[] SelectEdges(bool managed,int body,string shape,string selection,bool oriented)
{
    var edges=Tags(managed,body,true);
    if(selection=="all")
    {
        // Both independent constructors must receive the same physical mark
        // order. Body storage order is not part of the blending request.
        var ordered=new List<(PK_EDGE_t Edge,double X,double Y,double Z)>();
        int* endpoints=stackalloc int[2];
        foreach(var edge in edges)
        {
            Check(managed?KernelRuntime.EdgeAskVertices(edge,endpoints):PK_EDGE_ask_vertices(edge,endpoints),"ordered selection vertices");
            (double X,double Y,double Z) midpoint;
            if(endpoints[0]==0&&endpoints[1]==0)
            {
                int curve=0;Check(managed?KernelRuntime.EdgeAskCurve(edge,&curve):PK_EDGE_ask_curve(edge,&curve),"ordered rim curve");var sf=new PK_CIRCLE_sf_t();
                Check(managed?KernelRuntime.CircleAsk(curve,(M.PK_CIRCLE_sf_s*)&sf):PK_CIRCLE_ask(curve,&sf),"ordered rim circle");
                midpoint=(sf.basis_set.location.coord[0],sf.basis_set.location.coord[1],sf.basis_set.location.coord[2]);
            }
            else
            {
                var a=VertexPosition(managed,endpoints[0]);var b=VertexPosition(managed,endpoints[1]);midpoint=((a.X+b.X)*.5,(a.Y+b.Y)*.5,(a.Z+b.Z)*.5);
            }
            ordered.Add((edge,Math.Round(midpoint.X,8),Math.Round(midpoint.Y,8),Math.Round(midpoint.Z,8)));
        }
        Require(ordered.Select(x=>(x.X,x.Y,x.Z)).Distinct().Count()==edges.Length,"selection midpoints must be distinct");
        return ordered.OrderBy(x=>x.X).ThenBy(x=>x.Y).ThenBy(x=>x.Z).Select(x=>x.Edge).ToArray();
    }
    var axis=shape=="oblique"?(.35,0.0,Math.Sqrt(1-.35*.35)):Axis(oriented);var candidates=new List<(int Edge,double Score)>();
    int* vertices=stackalloc int[2];
    foreach(var edge in edges)
    {
        Check(managed?KernelRuntime.EdgeAskVertices(edge,vertices):PK_EDGE_ask_vertices(edge,vertices),"edge vertices");
        if(vertices[0]==0&&vertices[1]==0)
        {
            int curve=0;Check(managed?KernelRuntime.EdgeAskCurve(edge,&curve):PK_EDGE_ask_curve(edge,&curve),"edge curve");var sf=new PK_CIRCLE_sf_t();
            Check(managed?KernelRuntime.CircleAsk(curve,(M.PK_CIRCLE_sf_s*)&sf):PK_CIRCLE_ask(curve,&sf),"rim circle");candidates.Add((edge,-Dot((sf.basis_set.location.coord[0],sf.basis_set.location.coord[1],sf.basis_set.location.coord[2]),axis)));continue;
        }
        var a=VertexPosition(managed,vertices[0]);var b=VertexPosition(managed,vertices[1]);var delta=(b.X-a.X,b.Y-a.Y,b.Z-a.Z);var len=Math.Sqrt(Dot(delta,delta));
        if(Math.Abs(Dot(delta,axis))/len<.999)continue;
        var midpoint=((a.X+b.X)*.5,(a.Y+b.Y)*.5,(a.Z+b.Z)*.5);candidates.Add((edge,midpoint.Item1+2*midpoint.Item2+.13*midpoint.Item3));
    }
    Require(candidates.Count>0,"edge selection empty");return selection=="parallel"?candidates.Select(x=>x.Edge).ToArray():new[]{candidates.OrderByDescending(x=>x.Score).First().Edge};
}
static unsafe (double X,double Y,double Z) VertexPosition(bool managed,int vertex)
{
    int point=0;Check(managed?KernelRuntime.VertexAskPoint(vertex,&point):PK_VERTEX_ask_point(vertex,&point),"vertex point");var sf=new PK_POINT_sf_t();
    Check(managed?KernelRuntime.PointAsk(point,(M.PK_POINT_sf_s*)&sf):PK_POINT_ask(point,&sf),"point ask");return(sf.position.coord[0],sf.position.coord[1],sf.position.coord[2]);
}
static unsafe void Free(bool managed,void* value){if(value!=null)Check(managed?KernelRuntime.MemoryFree(value):PK_MEMORY_free(value),"free returned memory");}
static string ScriptPath([CallerFilePath]string path="")=>path;
static void Check(int error,string label){if(error!=0)throw new InvalidOperationException(label+": "+error);}
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}

static unsafe byte[] TransmitManaged(int body)
{
    var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=M.ParasolidConstants.PK_transmit_format_text_c,transmit_version=371,transmit_meshes=M.ParasolidConstants.PK_transmit_meshes_separate_c};
    var block=new M.PK_MEMORY_block_s();Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"managed XT transmit");
    try{using var bytes=new MemoryStream();for(var p=&block;p!=null;p=p->next)bytes.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return bytes.ToArray();}finally{KernelRuntime.MemoryBlockFree(&block);}
}
static unsafe byte[] TransmitReference(int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};var block=new PK_MEMORY_block_t();Check(PK_PART_transmit_b(1,&body,&options,&block),"PK XT transmit");
    try{using var bytes=new MemoryStream();for(var p=&block;p!=null;p=p->next)bytes.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return bytes.ToArray();}finally{PK_MEMORY_block_f(&block);}
}
static unsafe int Receive(byte[] bytes)
{
    fixed(byte* p=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count=0;int* parts=null;Check(PK_PART_receive_b(block,&options,&count,&parts),"PK receive managed XT");try{Require(count==1,"receive part count");return parts[0];}finally{Free(false,parts);}}
}
static unsafe int ReceiveManaged(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new M.PK_MEMORY_block_s{bytes=p,n_bytes=(nuint)bytes.Length};var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count=0;int* parts=null;
        Check(KernelRuntime.PartReceiveB(block,(M.PK_PART_receive_o_s*)&options,&count,&parts),"GM receive original seed XT");
        try{Require(count==1,"GM seed receive part count");return parts[0];}finally{Free(true,parts);}
    }
}
static unsafe (PK_BODY_t Managed,PK_BODY_t Reference) VerifyLifecycle(PK_BODY_t managed,PK_BODY_t reference,string label,string directory,Action<string> log,bool checkManagedMaterial)
{
    int copy=0,nativeCopy=0;
    Check(KernelRuntime.EntityCopy(managed,&copy),"copy GM Blend product");Check(PK_ENTITY_copy(reference,&nativeCopy),"copy independent PK product");
    Check(KernelRuntime.EntityDelete(1,&managed),"delete GM Blend source");Check(PK_ENTITY_delete(1,&reference),"delete PK source");
    var form=new M.PK_TRANSF_sf_s();form.matrix[0]=-1.7;form.matrix[5]=form.matrix[10]=1.7;form.matrix[15]=1;form.matrix[3]=3;form.matrix[7]=5;form.matrix[11]=7;
    int transform=0,nativeTransform=0;Check(KernelRuntime.TransfCreate(&form,&transform),"GM lifecycle transform");Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&form,&nativeTransform),"PK lifecycle transform");
    int count=0;int* replaced=null;byte* exact=null;PK_LOGICAL_t* nativeExact=null;
    Check(KernelRuntime.BodyTransform(copy,transform,0,&count,&replaced,&exact),"transform GM Blend copy");Free(true,replaced);Free(true,exact);
    Check(PK_BODY_transform(nativeCopy,nativeTransform,0,&count,&replaced,&nativeExact),"transform PK Blend copy");Free(false,replaced);Free(false,nativeExact);
    Check(KernelRuntime.EntityDelete(1,&transform),"delete GM transform");Check(PK_ENTITY_delete(1,&nativeTransform),"delete PK transform");
    var bytes=TransmitManaged(copy);File.WriteAllBytes(Path.Combine(directory,label+"-lifecycle-managed.x_t"),bytes);
    var referenceBytes=TransmitReference(nativeCopy);File.WriteAllBytes(Path.Combine(directory,label+"-lifecycle-reference.x_t"),referenceBytes);
    var reloaded=ReceiveManaged(bytes);Check(KernelRuntime.EntityDelete(1,&copy),"delete pre-reload GM product");
    var received=Receive(TransmitManaged(reloaded));
    WebProgressCapture.BindQuality(label+" lifecycle",reloaded,nativeCopy,received,bytes,referenceBytes);
    try
    {
        CheckBody(nativeCopy,label+" lifecycle reference",log);CheckBody(received,label+" lifecycle received",log);
        CompareMaterialSense(nativeCopy,received,label+" lifecycle");ParasolidBodyCompare.AssertEquivalent(nativeCopy,received,label+" lifecycle strict");
        CompareManagedPlaneOrientations(reloaded,received,label+" lifecycle",log,checkManagedMaterial);
        CompareMass(nativeCopy,received,label+" lifecycle",log);log("GEOMETRY_TOPOLOGY_PASS "+label+" lifecycle copy/delete source/mirror-scale/GM reload");
    }
    finally{Check(PK_ENTITY_delete(1,&received),"delete lifecycle receive");}
    return(reloaded,nativeCopy);
}
static unsafe PK_BODY_t VerifyFollowingBoolean(PK_BODY_t managed,string label,string directory,Action<string> log,bool inject)
{
    var stage=label+"-post-blend-boolean";var source=TransmitManaged(managed);var nativeTarget=Receive(source);
    File.WriteAllBytes(Path.Combine(directory,stage+"-target-before.x_t"),source);
    var sourceHash=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(source));
    log($"BOOLEAN_INPUT stage={stage} target_sha256={sourceHash} GM_live={managed} PK_same_bytes={nativeTarget} after_independent_blend_and_lifecycle=True");
    PK_BOX_t box;Check(PK_TOPOL_find_box(nativeTarget,&box),"actual Blend product bounds");
    var dx=box.coord[3]-box.coord[0];var dy=box.coord[4]-box.coord[1];var dz=box.coord[5]-box.coord[2];
    var pad=Math.Max(dx,Math.Max(dy,dz));Require(double.IsFinite(pad)&&pad>0,"bounded nonempty Blend product");
    var frame=new PK_AXIS2_sf_t(new((box.coord[0]+box.coord[3])*.5,(box.coord[1]+box.coord[4])*.5,(box.coord[2]+box.coord[5])*.5),new(0,0,1),new(1,0,0));
    PK_BODY_t tool=0,independentTool=0,nativeTool=0;
    var ownTrack=new M.PK_TOPOL_track_r_s();var ownResult=new M.PK_boolean_r_s();var nativeTrack=new PK_TOPOL_track_r_t();var nativeResult=new PK_boolean_r_t();
    try
    {
        Check(KernelRuntime.BodyCreateSolidBlock(dx+2*pad,dy+2*pad,dz*.5+pad,(M.PK_AXIS2_sf_s*)&frame,&tool),"GM crossing tool");
        Check(PK_BODY_create_solid_block(dx+2*pad,dy+2*pad,dz*.5+pad,&frame,&independentTool),"independent PK crossing tool");
        var toolBytes=TransmitManaged(tool);nativeTool=Receive(toolBytes);File.WriteAllBytes(Path.Combine(directory,stage+"-tool-before.x_t"),toolBytes);
        CheckBody(independentTool,stage+" independent tool",log);CheckBody(nativeTool,stage+" fresh tool",log);
        if(WebProgressCapture.Enabled)WebProgressCapture.BindQuality(stage+" independent tool",tool,independentTool,nativeTool,toolBytes,WebProgressCapture.NativeSnapshotBytes(independentTool));
        ParasolidBodyCompare.AssertEquivalent(independentTool,nativeTool,stage+" independent tool");
        Check(PK_ENTITY_delete(1,&independentTool),"delete independent tool baseline");independentTool=0;
        var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};
        File.WriteAllBytes(Path.Combine(directory,stage+"-options.bin"),new ReadOnlySpan<byte>(&options,sizeof(PK_BODY_boolean_o_t)).ToArray());
        log($"BOOLEAN_OPTIONS stage={stage} version={options.o_t_version} function={options.function} tolerance={options.default_tol:R} tool_sha256={Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(toolBytes))}");
        var pools=(KernelRuntime.Bodies.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Points.AliveCount,KernelRuntime.ReturnOutstandingBlocks);
        WebProgressCapture.Context=stage;
        WebProgressCapture.ContextSource=new JsonObject{["kind"]="same-byte-fork",["upstream"]=label+" independently verified construction/lifecycle",["targetXtSha256"]=sourceHash,
            ["toolXtSha256"]=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(toolBytes))};
        WebProgressCapture.Before(true,managed,[],"PK_BODY_boolean_2",[tool]);WebProgressCapture.BooleanParameters(true,options);
        int ownError;var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            if(inject)KernelRuntime.InjectAllocationFailure(0);
            ownError=KernelRuntime.BodyBoolean2(managed,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&ownTrack,&ownResult);
        }
        finally{KernelRuntime.InjectAllocationFailure(-1);}
        var elapsed=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        log($"STEP_API stage={stage} side=gm error={ownError} result={ownResult.result} count={ownResult.n_bodies} inject={inject}");
        if(ownError!=0)log($"BOOLEAN_FAILURE stage={stage} regular={KernelRuntime.LastRegularBooleanStage} pair={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace} sheet={KernelRuntime.LastSheetPartitionStage} algorithm={KernelRuntime.LastSheetAlgorithmStatus}");
        WebProgressCapture.BooleanAfter(true,ownError==0?new ReadOnlySpan<int>(ownResult.bodies,ownResult.n_bodies).ToArray():[],elapsed,ownError,ownResult.result,(PK_TOPOL_track_r_t*)&ownTrack);
        if(ownError==0)tool=0;
        WebProgressCapture.Before(false,nativeTarget,[],"PK_BODY_boolean_2",[nativeTool]);WebProgressCapture.BooleanParameters(false,options);
        started=System.Diagnostics.Stopwatch.GetTimestamp();
        var nativeError=PK_BODY_boolean_2(nativeTarget,1,&nativeTool,&options,&nativeTrack,&nativeResult);
        elapsed=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        log($"STEP_API stage={stage} side=pk error={nativeError} result={nativeResult.result} count={nativeResult.n_bodies}");
        WebProgressCapture.BooleanAfter(false,nativeError==0?new ReadOnlySpan<int>(nativeResult.bodies,nativeResult.n_bodies).ToArray():[],elapsed,nativeError,nativeResult.result,&nativeTrack);
        Check(nativeError,"same-byte PK post-Blend Boolean");Require(nativeResult.result==PK_boolean_result_success_c&&nativeResult.n_bodies==1,"PK Boolean result contract");
        nativeTool=0;nativeTarget=nativeResult.bodies[0];
        var nativeBytes=TransmitReference(nativeTarget);File.WriteAllBytes(Path.Combine(directory,stage+"-pk-after.x_t"),nativeBytes);CheckBody(nativeTarget,stage+" independent output",log);
        if(inject)
        {
            Require(ownError==M.ParasolidConstants.PK_ERROR_memory_full&&ownResult.result==PK_boolean_result_failed_c&&ownResult.n_bodies==0&&ownResult.bodies==null,"GM injected failure result contract");
            Require(ownTrack.n_track_records==0&&ownTrack.track_records==null,"no tracking products after rollback");
            Require(source.AsSpan().SequenceEqual(TransmitManaged(managed))&&toolBytes.AsSpan().SequenceEqual(TransmitManaged(tool)),"both inputs byte-identical after rollback");
            Require(pools==(KernelRuntime.Bodies.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Points.AliveCount,KernelRuntime.ReturnOutstandingBlocks),"rollback geometry and return ownership");
            log($"ROLLBACK_CONTROL_PASS {stage} GM_error={ownError} inputs_byte_identical=True PK_success=True; no successful GM Boolean asserted");
            return managed;
        }
        Check(ownError,"GM post-Blend Boolean");Require(ownResult.result==PK_boolean_result_success_c&&ownResult.n_bodies==1,"GM Boolean result contract");managed=ownResult.bodies[0];
        var bytes=TransmitManaged(managed);File.WriteAllBytes(Path.Combine(directory,stage+"-gm-after.x_t"),bytes);var fresh=Receive(bytes);
        WebProgressCapture.BindQuality(stage,managed,nativeTarget,fresh,bytes,nativeBytes);
        try
        {
            CheckBody(fresh,stage+" fresh output",log);ParasolidBodyCompare.AssertEquivalent(nativeTarget,fresh,stage+" strict");
            CompareMaterialSense(nativeTarget,fresh,stage);CompareManagedPlaneOrientations(managed,fresh,stage,log,true);CompareMass(nativeTarget,fresh,stage,log);
            log($"GEOMETRY_TOPOLOGY_PASS {stage} GM_live={managed} GM_after_sha256={Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))} PK_after_sha256={Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(nativeBytes))}");
        }
        finally{Check(PK_ENTITY_delete(1,&fresh),"delete fresh Boolean output");}
        return managed;
    }
    finally
    {
        WebProgressCapture.Context="";WebProgressCapture.ContextSource=null;KernelRuntime.TopolTrackResultFree(&ownTrack);KernelRuntime.BooleanResultFree(&ownResult);
        PK_TOPOL_track_r_f(&nativeTrack);PK_boolean_r_f(&nativeResult);
        if(tool!=0)Check(KernelRuntime.EntityDelete(1,&tool),"delete retained Boolean tool");
        if(nativeTool!=0)Check(PK_ENTITY_delete(1,&nativeTool),"delete retained native tool");
        if(independentTool!=0)Check(PK_ENTITY_delete(1,&independentTool),"delete tool baseline");
        if(nativeTarget!=0)Check(PK_ENTITY_delete(1,&nativeTarget),"delete native Boolean product");
    }
}

static unsafe int Count(int body,int kind)
{
    int count=0;int* entities=null;var error=kind switch{0=>PK_BODY_ask_regions(body,&count,&entities),1=>PK_BODY_ask_shells(body,&count,&entities),2=>PK_BODY_ask_faces(body,&count,&entities),3=>PK_BODY_ask_edges(body,&count,&entities),_=>PK_BODY_ask_vertices(body,&count,&entities)};
    Check(error,"PK topology count");try{return count;}finally{Free(false,entities);}
}
static unsafe void CheckBody(int body,string label,Action<string> log)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int count=0;PK_check_fault_t* faults=null;Check(PK_BODY_check(body,&options,&count,&faults),label+" full PK_BODY_check");
    WebProgressCapture.RecordBodyCheck(label,body,count,faults);
    try{for(var i=0;i<count;i++)log($"{label} fault={faults[i].state} entity={faults[i].entity_1} other={faults[i].entity_2}");Require(count==0,label+" BODY_check faults: "+count);}finally{Free(false,faults);}
}
static unsafe void CompareMass(int reference,int actual,string label,Action<string> log)
{
    var status=ParasolidMassProperties.Compare(reference,actual,label);log(label+" mass agreement="+status);
    Require(status!=MassAgreement.Different,label+" mass intervals prove a difference");
}

static unsafe void CompareManagedPlaneOrientations(int own,int fresh,string label,Action<string> log,bool checkManagedMaterial)
{
    var candidates=Tags(false,fresh,false);var used=new HashSet<PK_FACE_t>();var count=0;var queryFailures=0;
    foreach(var face in Tags(true,own,false))
    {
        PK_SURF_t surface=0;byte orientation=0;PK_CLASS_t kind=0;
        Check(KernelRuntime.FaceAskOrientedSurf(face,&surface,&orientation),"GM oriented surface query");Check(KernelRuntime.EntityAskClass(surface,&kind),"GM queried surface class");
        if(kind!=PK_CLASS_plane)continue;
        var basis=new PK_PLANE_sf_t();Check(KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&basis),"GM queried plane frame");PK_FACE_t matched=0;PK_LOGICAL_t expected=default;
        foreach(var candidate in candidates)
        {
            PK_SURF_t other=0;PK_LOGICAL_t sense=default;Check(PK_FACE_ask_oriented_surf(candidate,&other,&sense),"PK same-product oriented surface query");
            Check(PK_ENTITY_ask_class(other,&kind),"PK queried surface class");if(kind!=PK_CLASS_plane)continue;
            var target=new PK_PLANE_sf_t();Check(PK_PLANE_ask(other,&target),"PK same-product plane frame");
            double normalDot=0,referenceDot=0,separation=0;
            for(var axis=0;axis<3;axis++)
            {
                normalDot+=basis.basis_set.axis.coord[axis]*target.basis_set.axis.coord[axis];
                referenceDot+=basis.basis_set.ref_direction.coord[axis]*target.basis_set.ref_direction.coord[axis];
                separation+=(basis.basis_set.location.coord[axis]-target.basis_set.location.coord[axis])*basis.basis_set.axis.coord[axis];
            }
            if(normalDot<1-1e-12||referenceDot<1-1e-12||Math.Abs(separation)>1e-9)continue;
            Require(matched==0,label+" ambiguous same-product plane correspondence");matched=candidate;expected=sense;
        }
        Require(matched!=0&&used.Add(matched),label+" same-product plane correspondence");
        Require((orientation!=0)==(expected==PK_LOGICAL_true),$"{label} managed FaceAskOrientedSurf differs from same actual XT in PK: faces={face}/{matched} GM={orientation!=0} PK={expected==PK_LOGICAL_true}");count++;
        var rawFace=KernelRuntime.Faces[KernelRuntime.ResolveTagRecord(face).Slot].Orientation;
        var rawSurface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)].Sense;
        log($"FACE_DIRECTION_API {label} GM_face={face} face_raw={rawFace} surface_raw={rawSurface} GM_public={orientation!=0} PK_same_frame={expected==PK_LOGICAL_true}");
        if(!checkManagedMaterial)continue;
        var interior=new PK_FACE_find_interior_vec_o_t();PK_VECTOR_t point;PK_UV_t uv;
        Check(PK_FACE_find_interior_vec(matched,&interior,&point,&uv),"native finite plane material witness");
        foreach(var side in new[]{-1,1})
        {
            var query=point;for(var axis=0;axis<3;axis++)query.coord[axis]+=side*1e-5*(expected==PK_LOGICAL_true?1:-1)*basis.basis_set.axis.coord[axis];
            int actual=0,native=0,topology=0;
            var actualError=KernelRuntime.BodyContainsVector(own,*(M.PK_VECTOR_s*)&query,&actual,&topology);
            var nativeError=PK_BODY_contains_vector(fresh,query,&native,&topology);
            WebProgressCapture.RecordMaterial(label,query,nativeError,native,actualError,actual,"GM-live-vs-own-fresh-PK",fresh,own);
            log($"MANAGED_MATERIAL_QUERY {label} faces={face}/{matched} side={side} point=({query.coord[0]:R},{query.coord[1]:R},{query.coord[2]:R}) GM_error={actualError} GM_class={actual} PK_error={nativeError} PK_class={native}");
            Check(nativeError,"PK finite plane material side");
            Require(native==(side<0?PK_enclosure_inside_c:PK_enclosure_outside_c),label+" native finite plane material witness must be separated");
            if(actualError!=0){queryFailures++;continue;}
            Require(actual==native,$"{label} managed material differs at actual finite plane face={face}/{matched} side={side}: GM={actual} PK={native}");
        }
    }
    Require(queryFailures==0,$"{label} managed BodyContainsVector API failures={queryFailures}; original codes and native point results retained");
    log($"MANAGED_FACE_ORIENTATION_PASS {label} same_product_planes={count} separated_material_probes={(checkManagedMaterial?2*count:0)}");
}
static unsafe void CompareMaterialSense(int reference,int actual,string label)
{
    var targets=Tags(false,actual,false);var seenFaces=new HashSet<int>();
    foreach(var face in Tags(false,reference,false))
    {
        var options=new PK_FACE_find_interior_vec_o_t();PK_VECTOR_t point;PK_UV_t uv;
        Check(PK_FACE_find_interior_vec(face,&options,&point,&uv),label+" reference face interior");
        var expected=FaceNormal(face,point,out var expectedClass);var matched=0;
        foreach(var target in targets)
        {
            if(seenFaces.Contains(target))continue;int surface=0,cls=0;Check(PK_FACE_ask_surf(target,&surface),"candidate surface");Check(PK_ENTITY_ask_class(surface,&cls),"candidate class");
            if(cls!=expectedClass)continue;
            var contains=new PK_FACE_contains_vectors_o_t{n_vectors=1,vectors=&point,is_on_surf=PK_LOGICAL_false};int containing=0;
            Check(PK_FACE_contains_vectors(target,&contains,&containing),label+" actual face containment");if(containing!=target)continue;
            Require(matched==0,label+" ambiguous face correspondence");matched=target;
        }
        Require(matched!=0,label+" missing geometrically corresponding face");seenFaces.Add(matched);
        var normal=FaceNormal(matched,point,out _);Require(VectorDot(expected,normal)>1-2e-8,label+" wrong material side at face interior");
        var targetFins=FaceFins(matched);var seenFins=new HashSet<int>();var referenceFins=FaceFins(face);
        Require(referenceFins.Length==targetFins.Length,label+" face boundary incidence mismatch");
        foreach(var fin in referenceFins)
        {
            var source=FinGeometry(fin);var found=0;
            foreach(var candidate in targetFins)
            {
                if(seenFins.Contains(candidate))continue;var target=FinGeometry(candidate);
                if(!SameOrientedBoundary(source,target))continue;
                Require(found==0,label+" ambiguous fin correspondence");found=candidate;
            }
            Require(found!=0,label+" reversed or missing face-boundary tangent");seenFins.Add(found);
            // A second normal check on each boundary segment also protects
            // faces whose interior point alone would be an insufficient match.
            var p=new PK_VECTOR_t();Check(PK_CURVE_eval(source.Curve,source.Low+(source.High-source.Low)*.371,0,&p),"boundary normal point");
            var a=FaceNormal(face,p,out _);var b=FaceNormal(matched,p,out _);Require(VectorDot(a,b)>1-2e-8,label+" wrong material normal on boundary");
        }
    }
    Require(seenFaces.Count==targets.Length,label+" unmatched extra face");
}
static unsafe (double X,double Y,double Z) FaceNormal(int face,PK_VECTOR_t position,out int cls)
{
    int surface=0;PK_LOGICAL_t orientation=default;Check(PK_FACE_ask_oriented_surf(face,&surface,&orientation),"oriented face surface");int type=0;Check(PK_ENTITY_ask_class(surface,&type),"normal surface class");cls=type;
    PK_UV_t uv;Check(PK_SURF_parameterise_vector(surface,position,&uv),"normal parameterization");PK_VECTOR_t point,normal;
    Check(PK_SURF_eval_with_normal(surface,uv,0,0,PK_LOGICAL_false,&point,&normal),"oriented surface normal");
    Require(Distance(point,position)<=2e-7,"corresponding point does not lie on surface");var sign=orientation==PK_LOGICAL_true?1:-1;
    var n=(sign*normal.coord[0],sign*normal.coord[1],sign*normal.coord[2]);var length=Math.Sqrt(VectorDot(n,n));Require(double.IsFinite(length)&&length>0,"invalid material normal");return(n.Item1/length,n.Item2/length,n.Item3/length);
}
static unsafe int[] FaceFins(int face)
{
    int count=0;int* loops=null;Check(PK_FACE_ask_loops(face,&count,&loops),"face loops");var result=new List<int>();
    try{for(var i=0;i<count;i++){int n=0;int* fins=null;Check(PK_LOOP_ask_fins(loops[i],&n,&fins),"loop fins");try{result.AddRange(new ReadOnlySpan<int>(fins,n).ToArray());}finally{Free(false,fins);}}}
    finally{Free(false,loops);}return result.ToArray();
}
static unsafe (int Curve,int Class,double Low,double High,int Direction) FinGeometry(int fin)
{
    int edge=0,curve=0,cls=0;Check(PK_FIN_ask_edge(fin,&edge),"fin edge");PK_LOGICAL_t sense=default;var bounds=new PK_INTERVAL_t();PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
    Check(PK_EDGE_ask_geometry(edge,PK_LOGICAL_true,&curve,&cls,ends,&bounds,&sense),"fin edge geometry");
    int finCurve=0;PK_LOGICAL_t orientation=default;Check(PK_FIN_ask_oriented_curve(fin,&finCurve,&orientation),"oriented fin curve");Require(finCurve==0||finCurve==curve,"local oracle expects exact fin geometry");
    PK_LOGICAL_t positive=default;Check(PK_FIN_is_positive(fin,&positive),"fin edge orientation");
    return(curve,cls,bounds.value[0],bounds.value[1],(positive==PK_LOGICAL_true?1:-1)*(sense==PK_LOGICAL_true?1:-1));
}
static unsafe bool SameOrientedBoundary((int Curve,int Class,double Low,double High,int Direction) source,(int Curve,int Class,double Low,double High,int Direction) target)
{
    if(source.Class!=target.Class)return false;PK_VECTOR_t* expected=stackalloc PK_VECTOR_t[2];PK_VECTOR_t* actual=stackalloc PK_VECTOR_t[2];
    foreach(var fraction in new[]{.211,.5,.789})
    {
        Check(PK_CURVE_eval(source.Curve,source.Low+(source.High-source.Low)*fraction,1,expected),"source boundary sample");double t=0;
        if(PK_CURVE_parameterise_vector(target.Curve,expected[0],&t)!=0)return false;
        if(target.Class==PK_CLASS_circle||target.Class==PK_CLASS_ellipse){while(t<target.Low-1e-8)t+=Math.Tau;while(t>target.High+1e-8)t-=Math.Tau;}
        if(t<target.Low-1e-8||t>target.High+1e-8)return false;
        Check(PK_CURVE_eval(target.Curve,t,1,actual),"target boundary sample");if(Distance(expected[0],actual[0])>2e-7)return false;
        var a=(expected[1].coord[0]*source.Direction,expected[1].coord[1]*source.Direction,expected[1].coord[2]*source.Direction);
        var b=(actual[1].coord[0]*target.Direction,actual[1].coord[1]*target.Direction,actual[1].coord[2]*target.Direction);
        if(VectorDot(a,b)/Math.Sqrt(VectorDot(a,a)*VectorDot(b,b))<1-2e-8)return false;
    }
    return true;
}
static unsafe double Distance(PK_VECTOR_t a,PK_VECTOR_t b)
{
    var x=a.coord[0]-b.coord[0];var y=a.coord[1]-b.coord[1];var z=a.coord[2]-b.coord[2];return Math.Sqrt(x*x+y*y+z*z);
}
static double VectorDot((double X,double Y,double Z) a,(double X,double Y,double Z) b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
static unsafe void RecordFiniteCurves(PK_BODY_t body,string label,Action<string> log)
{
    PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];PK_VECTOR_t* jet=stackalloc PK_VECTOR_t[2];
    foreach(var edge in Tags(false,body,true))
    {
        PK_CURVE_t curve;PK_CLASS_t kind;PK_INTERVAL_t range;PK_LOGICAL_t sense;
        Check(PK_EDGE_ask_geometry(edge,PK_LOGICAL_true,&curve,&kind,ends,&range,&sense),"finite curve sense evidence");
        Check(PK_CURVE_eval(curve,range.value[0]*.5+range.value[1]*.5,1,jet),"finite curve tangent evidence");
        if(kind==PK_CLASS_circle)
        {
            var sf=new PK_CIRCLE_sf_t();Check(PK_CIRCLE_ask(curve,&sf),"circle frame evidence");
            log($"CIRCLE_FRAME curve={curve} origin=({sf.basis_set.location.coord[0]:R},{sf.basis_set.location.coord[1]:R},{sf.basis_set.location.coord[2]:R}) axis=({sf.basis_set.axis.coord[0]:R},{sf.basis_set.axis.coord[1]:R},{sf.basis_set.axis.coord[2]:R})");
        }
        log($"FINITE_CURVE {label} edge={edge} curve={curve} class={kind} positive={sense==PK_LOGICAL_true} range={range.value[0]:R}/{range.value[1]:R} ends=({ends[0].coord[0]:R},{ends[0].coord[1]:R},{ends[0].coord[2]:R})/({ends[1].coord[0]:R},{ends[1].coord[1]:R},{ends[1].coord[2]:R}) tangent=({jet[1].coord[0]:R},{jet[1].coord[1]:R},{jet[1].coord[2]:R})");
    }
}
static unsafe void RecordFiniteSurfaces(PK_BODY_t body,string label,Action<string> log)
{
    var parsed=ReceiveManaged(TransmitReference(body));
    foreach(var face in Tags(true,parsed,false))
    {
        var record=KernelRuntime.Faces[KernelRuntime.ResolveTagRecord(face).Slot];var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(record.SurfTag)];
        log($"XT_SENSE {label} face={face} class={surface.Class} face_sense={record.Orientation} surface_sense={surface.Sense}");
    }
    Check(KernelRuntime.EntityDelete(1,&parsed),"delete native sense inspection");
    foreach(var face in Tags(false,body,false))
    {
        int surface=0,kind=0;PK_LOGICAL_t orientation=default;Check(PK_FACE_ask_oriented_surf(face,&surface,&orientation),"surface gauge");Check(PK_ENTITY_ask_class(surface,&kind),"surface gauge class");
        if(kind==PK_CLASS_plane)
        {
            var sf=new PK_PLANE_sf_t();Check(PK_PLANE_ask(surface,&sf),"plane frame");
            log($"SURFACE_FRAME {label} face={face} surface={surface} class={kind} orientation={orientation==PK_LOGICAL_true} origin=({sf.basis_set.location.coord[0]:R},{sf.basis_set.location.coord[1]:R},{sf.basis_set.location.coord[2]:R}) axis=({sf.basis_set.axis.coord[0]:R},{sf.basis_set.axis.coord[1]:R},{sf.basis_set.axis.coord[2]:R})");
        }
    }
}

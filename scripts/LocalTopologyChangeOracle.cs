#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseOperationAcceptanceLedger=true
#:property AssemblyName=LocalTopologyChangeOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var directory=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","local-topology-change-oracle"));
Directory.CreateDirectory(directory);var report=new StringBuilder();
var ledger = new OperationAcceptanceLedger("local-topology-change-oracle", "Local Topology Change Oracle", directory);
void Log(string text){Console.WriteLine(text);report.AppendLine(text);}
try
{
    if(!ParasolidScriptHost.TryStartSession("Local topology change oracle",out var host,out var reason))throw new Exception(reason);
    using(host)
    unsafe
    {
        var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"self session");
        try
        {
            var cases=new List<(string Name,int Mask,double Amount,string Kind,bool Oriented,bool Varied,bool Imported)>();
            foreach(var amount in new[]{-.3,.3})foreach(var mask in new[]{5,7,21,23,31})
                cases.Add(($"hollow-{mask}-{(amount<0?"in":"out")}",mask,amount,"hollow",false,false,false));
            cases.Add(("oriented-two-open",5,-.3,"hollow",true,false,false));
            cases.Add(("oriented-split-face",7,.3,"hollow",true,false,false));
            cases.Add(("varied-wall-adjacent-open",5,-.3,"hollow",false,true,false));
            cases.Add(("native-import-split-face",7,-.3,"hollow",false,false,true));
            cases.Add(("face-offset-eliminates-chamfer",0,2,"face-offset",false,false,false));
            cases.Add(("body-offset-eliminates-chamfer",0,-1,"body-offset",false,false,false));
            cases.Add(("oriented-offset-eliminates-face",0,2,"face-offset",true,false,false));
            cases.Add(("native-import-face-elimination",0,-1,"body-offset",false,false,true));
            cases.Add(("separated-walls-in",15,-.3,"hollow",false,false,false));
            cases.Add(("separated-walls-out",15,.3,"hollow",false,false,false));
            var failed=0;
            foreach(var c in cases)
            {
                var opId = $"op-{c.Name}-edit";
                var editOp = ledger.BeginOperation(opId, c.Name, "primary", c.Kind switch
                {
                    "hollow" => "PK_BODY_hollow",
                    "face-offset" => "PK_FACE_offset",
                    "body-offset" => "PK_BODY_offset",
                    _ => "PK_BODY_local"
                }, null, c.Imported ? "imported seed XT from native PK" : "locally constructed test body");
                try
                {
                    var reference=Create(false,c.Oriented,c.Kind!="hollow");
                    var own=c.Imported?ReceiveOwn(Transmit(false,reference)):Create(true,c.Oriented,c.Kind!="hollow");
                    editOp.InputHashes["gm"] = new System.Text.Json.Nodes.JsonObject { ["bodyTag"] = own };
                    editOp.InputHashes["pk"] = new System.Text.Json.Nodes.JsonObject { ["bodyTag"] = reference };
                    var baseline=0;if(c.Mask==15)Check(PK_ENTITY_copy(reference,&baseline),"independent pre-failure baseline");
                    var a=Apply(true,own,c.Mask,c.Amount,c.Kind,c.Oriented,c.Varied);
                    var b=Apply(false,reference,c.Mask,c.Amount,c.Kind,c.Oriented,c.Varied);
                    var contractPass = a == b;
                    ledger.RecordCheck(opId, "auxiliary", c.Name + "-contract", c.Name + " contract parity", contractPass ? "pass" : "fail");
                    Require(contractPass,$"{c.Name}: status/tracking/identity differs {a} / {b}");
                    Log(c.Name+" contract="+a);
                    var bytes=Transmit(true,own);File.WriteAllBytes(Path.Combine(directory,c.Name+"-own.x_t"),bytes);
                    File.WriteAllBytes(Path.Combine(directory,c.Name+"-native.x_t"),Transmit(false,reference));
                    // The native session can leave intermediate geometry after
                    // an error. Our command transaction deliberately rolls back;
                    // verify that result against the independent pre-call copy.
                    var received=ReceivePk(bytes);
                    editOp.OutputHashes["gmReceived"] = new System.Text.Json.Nodes.JsonObject { ["bodyTag"] = received };
                    Compare(baseline!=0?baseline:reference,received,c.Name,Log,ledger,opId);
                    if(baseline!=0)Check(PK_ENTITY_delete(1,&baseline),"delete pre-failure baseline");
                    ledger.EndOperation(opId);
                    if(c.Imported)
                    {
                        var lifecycleId = $"op-{c.Name}-lifecycle";
                        ledger.BeginOperation(lifecycleId, c.Name + "-lifecycle", "lifecycle", "PK_PART_receive_b+PK_BODY_transform", null, "product XT imported, copied, transformed, compared, deleted");
                        RoundTrip(bytes,reference,c.Name+"-lifecycle",directory,Log,ledger,lifecycleId);
                        ledger.EndOperation(lifecycleId);
                    }
                    Check(KernelRuntime.EntityDelete(1,&own),"delete self");Check(PK_ENTITY_delete(1,&reference),"delete native");Check(PK_ENTITY_delete(1,&received),"delete received");
                    Log("PASS "+c.Name);
                }
                catch(Exception ex)
                {
                    failed++;
                    ledger.EndOperation(opId, "fail");
                    Log("FAIL "+c.Name+": "+ex.Message);
                }
            }
            foreach(var shape in new[]{"sphere","torus","cylinder","cone"})
            {
                var label=shape+"-outward-hollow-identities";
                var shapeOpId = $"op-{label}";
                var shapeOp = ledger.BeginOperation(shapeOpId, label, "primary", "PK_BODY_hollow", null, "analytic primitive outward hollow identities");
                try
                {
                    var own=AnalyticHollow(true,shape,out var a);var reference=AnalyticHollow(false,shape,out var b);
                    shapeOp.InputHashes["gm"] = new System.Text.Json.Nodes.JsonObject { ["bodyTag"] = own };
                    shapeOp.InputHashes["pk"] = new System.Text.Json.Nodes.JsonObject { ["bodyTag"] = reference };
                    var contractPass = a == b;
                    ledger.RecordCheck(shapeOpId, "auxiliary", label + "-tracking", label + " tracking parity", contractPass ? "pass" : "fail");
                    Require(contractPass,label+" tracking differs");Log(label+" contract="+a);
                    var bytes=Transmit(true,own);File.WriteAllBytes(Path.Combine(directory,label+"-own.x_t"),bytes);
                    File.WriteAllBytes(Path.Combine(directory,label+"-native.x_t"),Transmit(false,reference));
                    var received=ReceivePk(bytes);
                    shapeOp.OutputHashes["gmReceived"] = new System.Text.Json.Nodes.JsonObject { ["bodyTag"] = received };
                    Compare(reference,received,label,Log,ledger,shapeOpId);
                    ledger.EndOperation(shapeOpId);
                    Check(KernelRuntime.EntityDelete(1,&own),"delete analytic self");Check(PK_ENTITY_delete(1,&reference),"delete analytic native");Check(PK_ENTITY_delete(1,&received),"delete analytic received");
                    Log("PASS "+label);
                }
                catch(Exception ex)
                {
                    failed++;
                    ledger.EndOperation(shapeOpId, "fail");
                    Log("FAIL "+label+": "+ex.Message);
                }
            }
            Log($"Local topology change oracle: {cases.Count+4-failed}/{cases.Count+4} passed, {failed} failed; 2 import-copy-delete-transform lifecycles are required by their parent cases.");
            Require(failed==0,"Local topology change oracle failed");
        }
        finally{Check(KernelRuntime.SessionStop(),"self stop");}
    }
}
catch(Exception ex){Log("FAIL: "+ex.Message);Environment.ExitCode=1;}
finally
{
    ledger.EndScenario(Environment.ExitCode);
    ledger.SaveSidecar(Path.Combine(directory, "ledger.json"));
    ledger.PrintConsoleSummary(Log);
    File.WriteAllText(Path.Combine(directory,"report.txt"),report.ToString());
}

static unsafe int Create(bool own,bool oriented,bool bevel)
{
    var frame=Frame(oriented);int body=0;
    Check(own?KernelRuntime.BodyCreateSolidBlock(4,6,8,(M.PK_AXIS2_sf_s*)&frame,&body):PK_BODY_create_solid_block(4,6,8,&frame,&body),"create independent block");
    if(!bevel)return body;
    var edge=0;var best=double.NegativeInfinity;int* ends=stackalloc int[2];
    foreach(var candidate in Tags(own,body,3))
    {
        Check(own?KernelRuntime.EdgeAskVertices(candidate,ends):PK_EDGE_ask_vertices(candidate,ends),"edge ends");
        var a=Vertex(own,ends[0]);var b=Vertex(own,ends[1]);
        var axis=frame.axis;var x=frame.ref_direction;
        var y=new PK_VECTOR1_t(axis.coord[1]*x.coord[2]-axis.coord[2]*x.coord[1],axis.coord[2]*x.coord[0]-axis.coord[0]*x.coord[2],axis.coord[0]*x.coord[1]-axis.coord[1]*x.coord[0]);
        double length=0,projection=0,score=0;
        for(var k=0;k<3;k++){var d=b.coord[k]-a.coord[k];length+=d*d;projection+=d*axis.coord[k];score+=((a.coord[k]+b.coord[k])*.5-frame.location.coord[k])*(x.coord[k]+2*y.coord[k]);}
        if(Math.Abs(projection)/Math.Sqrt(length)<.99)continue;if(score>best){best=score;edge=candidate;}
    }
    Require(edge!=0,"vertical edge selection");
    var options=new PK_EDGE_set_blend_chamfer_o_t();int n=0;
    Check(own?KernelRuntime.EdgeSetBlendChamfer(1,&edge,.3,.3,null,(M.PK_EDGE_set_blend_chamfer_o_s*)&options,&n,null):
        PK_EDGE_set_blend_chamfer(1,&edge,.3,.3,null,&options,&n,null),"set independent bevel");
    var fix=new PK_BODY_fix_blends_o_t();int* blends=null;int fault=0,fe=0,ft=0;
    Check(own?KernelRuntime.BodyFixBlends(body,(M.PK_BODY_fix_blends_o_s*)&fix,&n,&blends,null,null,&fault,&fe,&ft):
        PK_BODY_fix_blends(body,&fix,&n,&blends,null,null,&fault,&fe,&ft),"fix independent bevel");
    Require(n==1&&fault==PK_blend_fault_no_fault_c,"bevel result");Free(own,blends);return body;
}

static unsafe int AnalyticHollow(bool own,string shape,out string signature)
{
    int body=0;
    Check(own?shape switch
    {
        "sphere"=>KernelRuntime.BodyCreateSolidSphere(3,null,&body),"torus"=>KernelRuntime.BodyCreateSolidTorus(5,1,null,&body),
        "cylinder"=>KernelRuntime.BodyCreateSolidCyl(3,6,null,&body),_=>KernelRuntime.BodyCreateSolidCone(3,6,.15,null,&body)
    }:shape switch
    {
        "sphere"=>PK_BODY_create_solid_sphere(3,null,&body),"torus"=>PK_BODY_create_solid_torus(5,1,null,&body),
        "cylinder"=>PK_BODY_create_solid_cyl(3,6,null,&body),_=>PK_BODY_create_solid_cone(3,6,.15,null,&body)
    },"create analytic independent model");
    var originals=Tags(own,body,2);var senses=originals.ToDictionary(x=>x,x=>SurfaceOrientation(own,x));
    var options=new PK_BODY_hollow_o_t();var track=new PK_TOPOL_track_r_t();var local=new PK_TOPOL_local_r_t();
    Check(own?KernelRuntime.BodyHollow2(body,.3,1e-6,(M.PK_BODY_hollow_o_s*)&options,(M.PK_TOPOL_track_r_s*)&track,(M.PK_TOPOL_local_r_s*)&local):
        PK_BODY_hollow_2(body,.3,1e-6,&options,&track,&local),"positive analytic hollow");
    Require(local.status==PK_local_status_ok_c,"analytic local status");Require(track.n_track_records==originals.Length,"analytic hollow tracking count");
    foreach(var original in originals)Require(SurfaceOrientation(own,original)==senses[original],"original analytic face must remain outward");
    var records=new List<string>();
    for(var i=0;i<track.n_track_records;i++)
    {
        var r=track.track_records[i];Require(r.track==PK_TOPOL_track_create_c&&r.n_original_topols==1&&r.n_product_topols==1,"analytic tracking record");
        var before=senses[r.original_topols[0]];var after=SurfaceOrientation(own,r.product_topols[0]);
        Require(before.Class==after.Class&&before.Sense!=after.Sense,"new tracked analytic face must be the inner wall");
        records.Add($"{r.track}:{before.Class}:{before.Sense}:{after.Sense}");
    }
    records.Sort(StringComparer.Ordinal);signature=string.Join(';',records);
    if(own){KernelRuntime.TopolTrackResultFree((M.PK_TOPOL_track_r_s*)&track);KernelRuntime.TopolLocalResultFree((M.PK_TOPOL_local_r_s*)&local);}
    else{PK_TOPOL_track_r_f(&track);PK_TOPOL_local_r_f(&local);}return body;
}
static unsafe (int Class,byte Sense) SurfaceOrientation(bool own,int face)
{
    int surface=0,cls=0;byte sense=0;
    Check(own?KernelRuntime.FaceAskOrientedSurf(face,&surface,&sense):PK_FACE_ask_oriented_surf(face,&surface,(PK_LOGICAL_t*)&sense),"tracked face surface");
    Check(own?KernelRuntime.EntityAskClass(surface,&cls):PK_ENTITY_ask_class(surface,&cls),"tracked surface class");return(cls,sense);
}

static unsafe string Apply(bool own,int body,int mask,double amount,string kind,bool oriented,bool varied)
{
    var initial=Tags(own,body,2);var keys=initial.ToDictionary(x=>x,x=>FaceKey(own,x,oriented));
    var boundary=BoundaryKeys(own,body,keys);
    var selected=initial.Where(x=>keys[x]<6&&(mask&(1<<keys[x]))!=0).ToArray();
    var old=Transmit(own,body);var track=new PK_TOPOL_track_r_t();var local=new PK_TOPOL_local_r_t();
    var ho=new PK_BODY_hollow_o_t();var bo=new PK_BODY_offset_o_t();var fo=new PK_FACE_offset_o_t();
    var variedFace=initial.First(x=>keys[x]==5);var changed=amount*1.7;int error;
    fixed(int* p=selected)
    {
        ho.n_pierce_faces=selected.Length;ho.pierce_faces=p;
        if(varied){ho.n_offset_faces=1;ho.offset_faces=&variedFace;ho.offset_values=&changed;}
        var face=kind=="hollow"?0:initial.Single(x=>keys[x]==6);
        if(own)
            error=kind=="hollow"?KernelRuntime.BodyHollow2(body,amount,1e-6,(M.PK_BODY_hollow_o_s*)&ho,(M.PK_TOPOL_track_r_s*)&track,(M.PK_TOPOL_local_r_s*)&local):
                kind=="face-offset"?KernelRuntime.FaceOffset2(1,&face,&amount,1e-6,(M.PK_FACE_offset_o_s*)&fo,(M.PK_TOPOL_track_r_s*)&track,(M.PK_TOPOL_local_r_s*)&local):
                KernelRuntime.BodyOffset2(body,amount,1e-6,(M.PK_BODY_offset_o_s*)&bo,(M.PK_TOPOL_track_r_s*)&track,(M.PK_TOPOL_local_r_s*)&local);
        else error=kind=="hollow"?PK_BODY_hollow_2(body,amount,1e-6,&ho,&track,&local):
            kind=="face-offset"?PK_FACE_offset_2(1,&face,&amount,1e-6,&fo,&track,&local):PK_BODY_offset_2(body,amount,1e-6,&bo,&track,&local);
    }
    if(mask==15)
    {
        Require(error==PK_ERROR_not_unique,"expected PK_ERROR_not_unique, actual "+error);
        Require(track.n_track_records==0,"failure tracking must be empty");
        if(own)Require(old.SequenceEqual(Transmit(true,body)),"self failure mutated input");
        return "error:"+error;
    }
    Check(error,(own?"self":"native")+" "+kind);Require(local.status==PK_local_status_ok_c,"local status");
    var after=Tags(own,body,2);var signatures=new List<string>();
    for(var i=0;i<track.n_track_records;i++)
    {
        var r=track.track_records[i];Require(r.n_original_topols==1&&r.original_classes[0]==PK_CLASS_face,"tracking origin class/count");
        Require(keys.TryGetValue(r.original_topols[0],out var key),"tracking original face");
        Require(r.n_product_topols>0,"tracking product count");
        for(var j=0;j<r.n_product_topols;j++)
        {
            Require(after.Contains(r.product_topols[j])&&!initial.Contains(r.product_topols[j]),"tracking new face must exist");
            var expected=r.track==PK_TOPOL_track_create_c?key^1:key;
            var actual=FaceKey(own,r.product_topols[j],oriented);
            Require(actual==expected,$"{(own?"self":"native")} tracking product material normal: source={key}, kind={r.track}, expected={expected}, actual={actual}");
        }
        signatures.Add($"{r.track}:{key}:{r.n_product_topols}");
    }
    signatures.Sort(StringComparer.Ordinal);
    if(own){Check(KernelRuntime.TopolTrackResultFree((M.PK_TOPOL_track_r_s*)&track),"free self tracking");Check(KernelRuntime.TopolLocalResultFree((M.PK_TOPOL_local_r_s*)&local),"free self local");}
    else{Check(PK_TOPOL_track_r_f(&track),"free native tracking");Check(PK_TOPOL_local_r_f(&local),"free native local");}
    var afterEdges=Tags(own,body,3);var afterVertices=Tags(own,body,4);
    return string.Join(';',signatures)+";kept="+string.Join(',',initial.Where(after.Contains).Select(x=>keys[x]).Order())
        +";edges="+string.Join('|',boundary.Edges.Where(x=>afterEdges.Contains(x.Key)).Select(x=>x.Value).Order())
        +";vertices="+string.Join('|',boundary.Vertices.Where(x=>afterVertices.Contains(x.Key)).Select(x=>x.Value).Order());
}

static unsafe (Dictionary<int,string> Edges,Dictionary<int,string> Vertices) BoundaryKeys(bool own,int body,Dictionary<int,int> faces)
{
    var edges=new Dictionary<int,HashSet<int>>();var vertices=new Dictionary<int,HashSet<int>>();int* ends=stackalloc int[2];
    foreach(var face in faces)
    {
        int n=0;int* items=null;Check(own?KernelRuntime.FaceAskEdges(face.Key,&n,&items):PK_FACE_ask_edges(face.Key,&n,&items),"identity face edges");
        try
        {
            for(var i=0;i<n;i++)
            {
                if(!edges.TryGetValue(items[i],out var e))edges[items[i]]=e=new HashSet<int>();e.Add(face.Value);
                Check(own?KernelRuntime.EdgeAskVertices(items[i],ends):PK_EDGE_ask_vertices(items[i],ends),"identity edge endpoints");
                for(var j=0;j<2;j++){if(!vertices.TryGetValue(ends[j],out var v))vertices[ends[j]]=v=new HashSet<int>();v.Add(face.Value);}
            }
        }
        finally{Free(own,items);}
    }
    return(edges.ToDictionary(x=>x.Key,x=>string.Join(',',x.Value.Order())),vertices.ToDictionary(x=>x.Key,x=>string.Join(',',x.Value.Order())));
}

static unsafe PK_AXIS2_sf_t Frame(bool oriented)=>new(new PK_VECTOR_t(oriented?7:0,oriented?-4:0,oriented?2:0),
    oriented?new PK_VECTOR1_t(1/Math.Sqrt(14),2/Math.Sqrt(14),3/Math.Sqrt(14)):new PK_VECTOR1_t(0,0,1),
    oriented?new PK_VECTOR1_t(2/Math.Sqrt(5),-1/Math.Sqrt(5),0):new PK_VECTOR1_t(1,0,0));
static unsafe int FaceKey(bool own,int face,bool oriented)
{
    int surface=0;byte sense=0;Check(own?KernelRuntime.FaceAskOrientedSurf(face,&surface,&sense):PK_FACE_ask_oriented_surf(face,&surface,(PK_LOGICAL_t*)&sense),"oriented plane");
    PK_PLANE_sf_t plane=default;Check(own?KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&plane):PK_PLANE_ask(surface,&plane),"plane form");
    var basis=Frame(oriented);var x=basis.ref_direction;var z=basis.axis;
    var y=new PK_VECTOR1_t(z.coord[1]*x.coord[2]-z.coord[2]*x.coord[1],z.coord[2]*x.coord[0]-z.coord[0]*x.coord[2],z.coord[0]*x.coord[1]-z.coord[1]*x.coord[0]);
    for(var k=0;k<3;k++)
    {
        var axis=k==0?x:k==1?y:z;double dot=0;for(var j=0;j<3;j++)dot+=axis.coord[j]*plane.basis_set.axis.coord[j]*(sense==0?-1:1);
        if(Math.Abs(dot)>.999)return 2*k+(dot>0?1:0);
    }
    return 6;
}
static unsafe PK_VECTOR_t Vertex(bool own,int vertex)
{
    int point=0;Check(own?KernelRuntime.VertexAskPoint(vertex,&point):PK_VERTEX_ask_point(vertex,&point),"vertex point");PK_POINT_sf_t data=default;
    Check(own?KernelRuntime.PointAsk(point,(M.PK_POINT_sf_s*)&data):PK_POINT_ask(point,&data),"point form");return data.position;
}
static unsafe int[] Tags(bool own,int body,int kind)
{
    int n=0;int* tags=null;
    var error=own?kind switch{0=>KernelRuntime.BodyAskRegions(body,&n,&tags),1=>KernelRuntime.BodyAskShells(body,&n,&tags),2=>KernelRuntime.BodyAskFaces(body,&n,&tags),3=>KernelRuntime.BodyAskEdges(body,&n,&tags),_=>KernelRuntime.BodyAskVertices(body,&n,&tags)}:
        kind switch{0=>PK_BODY_ask_regions(body,&n,&tags),1=>PK_BODY_ask_shells(body,&n,&tags),2=>PK_BODY_ask_faces(body,&n,&tags),3=>PK_BODY_ask_edges(body,&n,&tags),_=>PK_BODY_ask_vertices(body,&n,&tags)};
    Check(error,"topology query");try{return new ReadOnlySpan<int>(tags,n).ToArray();}finally{Free(own,tags);}
}
static unsafe byte[] Transmit(bool own,int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};var block=new PK_MEMORY_block_t();
    Check(own?KernelRuntime.PartTransmitB(1,&body,(M.PK_PART_transmit_o_s*)&options,(M.PK_MEMORY_block_s*)&block):PK_PART_transmit_b(1,&body,&options,&block),"XT transmit");
    try{using var output=new MemoryStream();for(var p=&block;p!=null;p=p->next)output.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return output.ToArray();}
    finally{if(own)KernelRuntime.MemoryBlockFree((M.PK_MEMORY_block_s*)&block);else PK_MEMORY_block_f(&block);}
}
static unsafe int ReceiveOwn(byte[] bytes)=>Receive(true,bytes);
static unsafe int ReceivePk(byte[] bytes)=>Receive(false,bytes);
static unsafe int Receive(bool own,byte[] bytes)
{
    fixed(byte* data=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,data);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n=0;int* parts=null;
        Check(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&options,&n,&parts):PK_PART_receive_b(block,&options,&n,&parts),"XT receive");
        try{Require(n==1,"one received part");return parts[0];}finally{Free(own,parts);}
    }
}
static unsafe void RoundTrip(byte[] bytes,int reference,string label,string directory,Action<string> log,OperationAcceptanceLedger? ledger=null,string? opId=null)
{
    var before=(KernelRuntime.Bodies.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Points.AliveCount);
    var imported=ReceiveOwn(bytes);int copy=0,referenceCopy=0;
    Check(KernelRuntime.EntityCopy(imported,&copy),"lifecycle self copy");Check(KernelRuntime.EntityDelete(1,&imported),"lifecycle delete source");
    Check(PK_ENTITY_copy(reference,&referenceCopy),"lifecycle independent copy");
    var form=new M.PK_TRANSF_sf_s();form.matrix[0]=form.matrix[5]=form.matrix[10]=form.matrix[15]=1;
    form.matrix[3]=2;form.matrix[7]=-1;form.matrix[11]=.5;int transform=0,nativeTransform=0;
    Check(KernelRuntime.TransfCreate(&form,&transform),"lifecycle self transform create");Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&form,&nativeTransform),"lifecycle native transform create");
    int count=0;int* replaced=null;byte* exact=null;
    Check(KernelRuntime.BodyTransform(copy,transform,0,&count,&replaced,&exact),"lifecycle self transform");Free(true,replaced);Free(true,exact);
    PK_LOGICAL_t* nativeExact=null;
    Check(PK_BODY_transform(referenceCopy,nativeTransform,0,&count,&replaced,&nativeExact),"lifecycle native transform");Free(false,replaced);Free(false,nativeExact);
    var output=Transmit(true,copy);File.WriteAllBytes(Path.Combine(directory,label+"-own.x_t"),output);
    var received=ReceivePk(output);Compare(referenceCopy,received,label,log,ledger,opId);
    Check(KernelRuntime.EntityDelete(1,&copy),"lifecycle delete self copy");Check(KernelRuntime.EntityDelete(1,&transform),"lifecycle delete self transform");
    var leakPass=before==(KernelRuntime.Bodies.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Points.AliveCount);
    if(ledger!=null&&opId!=null)
    {
        ledger.RecordCheck(opId,"representation",label+"-ownership-leak",label+" geometry ownership leak",leakPass?"pass":"fail");
    }
    Require(leakPass,label+" geometry ownership leak");
    Check(PK_ENTITY_delete(1,&referenceCopy),"lifecycle delete native copy");Check(PK_ENTITY_delete(1,&nativeTransform),"lifecycle delete native transform");Check(PK_ENTITY_delete(1,&received),"lifecycle delete received");
    log("PASS "+label);
}
static unsafe void Compare(int reference,int received,string label,Action<string> log,OperationAcceptanceLedger? ledger=null,string? opId=null)
{
    for(var kind=0;kind<5;kind++)
    {
        var match=Tags(false,reference,kind).Length==Tags(false,received,kind).Length;
        if(ledger!=null&&opId!=null)
        {
            ledger.RecordCheck(opId,"primary",label+"-topology-"+kind,label+" topology kind "+kind,match?"pass":"fail",gm:Tags(false,received,kind).Length,pk:Tags(false,reference,kind).Length,threshold:0);
        }
        Require(match,label+" topology kind "+kind);
    }
    CheckBody(reference,label+" independent",log,ledger,opId,"auxiliary");CheckBody(received,label+" received",log,ledger,opId,"primary");
    var options=new PK_DEBUG_BODY_compare_o_t{max_diffs=128,all_tests=PK_LOGICAL_false,num_tests=PK_LOGICAL_true,tol_dev_tests=PK_LOGICAL_true,
        tol_tests=PK_LOGICAL_true,class_tests=PK_LOGICAL_true,sense_tests=PK_LOGICAL_true,missing_tests=PK_LOGICAL_true,acc_dev_tests=PK_LOGICAL_false,non_match_tests=PK_LOGICAL_false};
    var result=new PK_DEBUG_BODY_compare_r_t();Check(PK_DEBUG_BODY_compare(reference,received,&options,&result),"strict comparison");
    try
    {
        log($"{label}: global={result.global_result}, local={result.local_result}, face pairs={result.n_face_pairs}");
        for(var i=0;i<result.n_global_diffs;i++)log($"  global diff={result.global_diffs[i].diff} count={result.global_diffs[i].n_masters}/{result.global_diffs[i].n_similars}");
        for(var i=0;i<result.n_face_pairs;i++)for(var j=0;j<result.face_pairs[i].n_local_diffs;j++)
        {var d=result.face_pairs[i].local_diffs[j];log($"  face={result.face_pairs[i].master_face}/{result.face_pairs[i].similar_face} diff={d.diff} entity={d.master_entity}/{d.similar_entity} value={d.master_double:R}/{d.similar_double:R}");}
        var comparePass=result.global_result==PK_DEBUG_global_res_no_diffs_c&&result.local_result==PK_DEBUG_local_res_no_diffs_c;
        if(ledger!=null&&opId!=null)
        {
            ledger.RecordCheck(opId,"primary",label+"-strict-compare",label+" PK_DEBUG_BODY_compare",comparePass?"pass":"fail");
        }
        Require(comparePass,label+" strict structure/sense/tolerance/missing differs");
    }
    finally{Check(PK_DEBUG_BODY_compare_r_f(&result),"free comparison");}
    var mass=new PK_TOPOL_eval_mass_props_o_t();double* a=stackalloc double[15];double* b=stackalloc double[15];
    Check(PK_TOPOL_eval_mass_props(1,&reference,.999999999,&mass,a,a+1,a+2,a+5,a+14),"independent mass");
    Check(PK_TOPOL_eval_mass_props(1,&received,.999999999,&mass,b,b+1,b+2,b+5,b+14),"received mass");
    var massPass=true;
    for(var i=0;i<15;i++)
    {
        var ok=double.IsFinite(a[i])&&double.IsFinite(b[i])&&Math.Abs(a[i]-b[i])<=2e-7*Math.Max(1,Math.Abs(a[i]));
        if(!ok)massPass=false;
        Require(ok,label+$" mass {i}: {a[i]:R}/{b[i]:R}");
    }
    if(ledger!=null&&opId!=null)
    {
        ledger.RecordCheck(opId,"primary",label+"-mass-props",label+" mass properties",massPass?"pass":"fail",gm:b[0],pk:a[0],threshold:2e-7);
    }
    log($"{label}: volume={a[0]:R}/{b[0]:R}, area={a[14]:R}/{b[14]:R}");
}
static unsafe void CheckBody(int body,string label,Action<string> log,OperationAcceptanceLedger? ledger=null,string? opId=null,string category="primary")
{
    var options=new PK_BODY_check_o_t{max_faults=100};int n=0;PK_check_fault_t* faults=null;Check(PK_BODY_check(body,&options,&n,&faults),"full PK body check");
    try
    {
        for(var i=0;i<n;i++)log($"{label}: fault={faults[i].state} entity={faults[i].entity_1}/{faults[i].entity_2}");
        var pass=n==0;
        if(ledger!=null&&opId!=null)
        {
            ledger.RecordCheck(opId,category,label+"-body-check",label+" PK_BODY_check",pass?"pass":"fail",gm:n,pk:0,threshold:0);
        }
        Require(pass,label+" body check faults");
    }
    finally{Free(false,faults);}
}
static unsafe void Free(bool own,void* memory){if(memory!=null)Check(own?KernelRuntime.MemoryFree(memory):PK_MEMORY_free(memory),"free return memory");}
static string ScriptPath([CallerFilePath]string path="")=>path;
static void Check(int error,string label){if(error!=0)throw new InvalidOperationException(label+": "+error);}
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}

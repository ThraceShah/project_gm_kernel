#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=LocalOperationOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var directory=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","local-operation-oracle"));
Directory.CreateDirectory(directory);var report=new StringBuilder();
var axialArrangement=args.Contains("--axial-arrangement",StringComparer.Ordinal);
var blendArrangement=args.Contains("--blend-arrangement",StringComparer.Ordinal);
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
                if(trace!=null&&(c.Operation is not ("fillet" or "chamfer" or "unequal-chamfer")||axialArrangement&&c.Shape!="cylinder"))continue;
                executed++;
                try
                {
                    var managed=Create(true,c.Shape,c.Oriented);var reference=Create(false,c.Shape,c.Oriented);
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
                    File.WriteAllBytes(Path.Combine(directory,c.Name+"-reference.x_t"),TransmitReference(reference));
                    var received=Receive(bytes);
                    for(var kind=0;kind<5;kind++){var actual=Count(received,kind);var expected=Count(reference,kind);Require(actual==expected,$"{c.Name} topology kind {kind}: {actual}/{expected}");}
                    CheckBody(reference,c.Name+" independent",Log);CheckBody(received,c.Name+" received",Log);CompareMass(reference,received,c.Name,Log);
                    CompareMaterialSense(reference,received,c.Name);
                    if(trace!=null&&c.Shape=="cylinder")ParasolidBodyCompare.AssertEquivalent(reference,received,c.Name+" strict");
                    if(!CompareBodies(reference,received,c.Name+" raw",Log,true))
                    {
                        var first=CanonicalCopy(reference);var second=CanonicalCopy(received);
                        CompareMaterialSense(reference,first,c.Name+" reference normalization");CompareMaterialSense(received,second,c.Name+" received normalization");
                        CheckBody(first,c.Name+" canonical independent",Log);CheckBody(second,c.Name+" canonical received",Log);
                        CompareBodies(first,second,c.Name+" canonical",Log);CompareMass(first,second,c.Name+" canonical",Log);
                        Check(PK_ENTITY_delete(1,&first),"delete canonical independent");Check(PK_ENTITY_delete(1,&second),"delete canonical received");
                    }
                    Check(KernelRuntime.EntityDelete(1,&managed),"delete managed result");Check(PK_ENTITY_delete(1,&reference),"delete reference");Check(PK_ENTITY_delete(1,&received),"delete received");
                    Log("PASS "+c.Name);
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
            Log($"Local operation oracle: {executed-failures} passed, {failures} failed.");Require(failures==0,"Local operation oracle failed.");
        }
        finally{Check(KernelRuntime.SessionStop(),"managed session stop");}
    }
}
catch(Exception ex){Log("FAIL: "+ex.Message);Environment.ExitCode=1;}
finally{File.WriteAllText(Path.Combine(directory,"report.txt"),report.ToString());}

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

static unsafe string Apply(bool managed,int body,string shape,string operation,double amount,string selection,bool oriented)
{
    var originalFaces=Tags(managed,body,false);var signature="";
    if(operation is "fillet" or "chamfer" or "unequal-chamfer")
    {
        var edges=SelectEdges(managed,body,shape,selection,oriented);var round=new PK_EDGE_set_blend_constant_o_t();var flat=new PK_EDGE_set_blend_chamfer_o_t();
        var firstFace=operation=="unequal-chamfer"?ChamferFirstFace(managed,body,shape,oriented):0;
        var secondRange=operation=="unequal-chamfer"?amount*2:amount;
        fixed(int* input=edges)
        {
            int count=0;int* output=null;
            var designated=firstFace==0?null:&firstFace;
            var error=managed?(operation=="fillet"?KernelRuntime.EdgeSetBlendConstant(edges.Length,input,amount,(M.PK_EDGE_set_blend_constant_o_s*)&round,&count,&output):KernelRuntime.EdgeSetBlendChamfer(edges.Length,input,secondRange,amount,designated,(M.PK_EDGE_set_blend_chamfer_o_s*)&flat,&count,&output)):
                operation=="fillet"?PK_EDGE_set_blend_constant(edges.Length,input,amount,&round,&count,&output):PK_EDGE_set_blend_chamfer(edges.Length,input,secondRange,amount,designated,&flat,&count,&output);
            Check(error,"set "+operation);Require(count==edges.Length,"set blend edge count");Free(managed,output);
        }
        var fix=new PK_BODY_fix_blends_o_t();fix.local_check=PK_LOGICAL_true;int made=0;int* blends=null;int* replaced=null;int fault=0,faultEdge=0,faultTopol=0;
        if(managed)
        {
            M.PK_FACE_array_s* unders=null;Check(KernelRuntime.BodyFixBlends(body,(M.PK_BODY_fix_blends_o_s*)&fix,&made,&blends,&unders,&replaced,&fault,&faultEdge,&faultTopol),"managed fix blends");
            var records=new List<int>();for(var i=0;i<made;i++){records.Add(unders[i].length);Free(true,unders[i].array);}Free(true,unders);records.Sort();signature="blends:"+string.Join(",",records);
        }
        else
        {
            PK_FACE_array_t* unders=null;Check(PK_BODY_fix_blends(body,&fix,&made,&blends,&unders,&replaced,&fault,&faultEdge,&faultTopol),"independent PK fix blends");
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
    var edges=Tags(managed,body,true);if(selection=="all")return edges;
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
static unsafe int Count(int body,int kind)
{
    int count=0;int* entities=null;var error=kind switch{0=>PK_BODY_ask_regions(body,&count,&entities),1=>PK_BODY_ask_shells(body,&count,&entities),2=>PK_BODY_ask_faces(body,&count,&entities),3=>PK_BODY_ask_edges(body,&count,&entities),_=>PK_BODY_ask_vertices(body,&count,&entities)};
    Check(error,"PK topology count");try{return count;}finally{Free(false,entities);}
}
static unsafe void CheckBody(int body,string label,Action<string> log)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int count=0;PK_check_fault_t* faults=null;Check(PK_BODY_check(body,&options,&count,&faults),label+" full PK_BODY_check");
    try{for(var i=0;i<count;i++)log($"{label} fault={faults[i].state} entity={faults[i].entity_1} other={faults[i].entity_2}");Require(count==0,label+" BODY_check faults: "+count);}finally{Free(false,faults);}
}
static unsafe bool CompareBodies(int reference,int actual,string label,Action<string> log,bool allowCanonicalization=false)
{
    var options=new PK_DEBUG_BODY_compare_o_t{max_diffs=128,all_tests=PK_LOGICAL_false,num_tests=PK_LOGICAL_true,tol_dev_tests=PK_LOGICAL_true,tol_tests=PK_LOGICAL_true,
        class_tests=PK_LOGICAL_true,sense_tests=PK_LOGICAL_true,missing_tests=PK_LOGICAL_true,acc_dev_tests=PK_LOGICAL_false,non_match_tests=PK_LOGICAL_false};
    var result=new PK_DEBUG_BODY_compare_r_t();Check(PK_DEBUG_BODY_compare(reference,actual,&options,&result),label+" compare");
    try
    {
        log($"{label}: global={result.global_result}, local={result.local_result}, face pairs={result.n_face_pairs}");var differences=0;var other=0;
        for(var i=0;i<result.n_global_diffs;i++)log($"  global={result.global_diffs[i].diff} reference={result.global_diffs[i].n_masters} received={result.global_diffs[i].n_similars}");
        for(var i=0;i<result.n_face_pairs;i++)for(var j=0;j<result.face_pairs[i].n_local_diffs;j++){var d=result.face_pairs[i].local_diffs[j];differences++;if(d.diff!=21958&&d.diff!=21959)other++;log($"  face={result.face_pairs[i].master_face}/{result.face_pairs[i].similar_face} diff={d.diff} entity={d.master_entity}/{d.similar_entity} value={d.master_double:R}/{d.similar_double:R}");if(allowCanonicalization&&differences<3&&d.diff==21959){DumpCurve(d.master_entity,log);DumpCurve(d.similar_entity,log);}}
        Require(result.global_result==PK_DEBUG_global_res_no_diffs_c&&other==0,label+" structural/class/tolerance/missing differences");
        if(differences!=0&&allowCanonicalization){log(label+" has representation sense differences; material normals and every oriented fin are separately gated before canonicalization.");return false;}
        Require(result.local_result==PK_DEBUG_local_res_no_diffs_c&&differences==0,label+" structure/sense/tolerance differs");return true;
    }
    finally{Check(PK_DEBUG_BODY_compare_r_f(&result),"free compare result");}
}
static unsafe void CompareMass(int reference,int actual,string label,Action<string> log)
{
    var options=new PK_TOPOL_eval_mass_props_o_t();double* expected=stackalloc double[15];double* received=stackalloc double[15];
    // FD §31.1.1: accuracy approaches ONE for more integration work; it is not
    // a requested error tolerance. Low values are unsuitable for validation.
    Check(PK_TOPOL_eval_mass_props(1,&reference,.999999999,&options,expected,expected+1,expected+2,expected+5,expected+14),label+" reference mass");
    Check(PK_TOPOL_eval_mass_props(1,&actual,.999999999,&options,received,received+1,received+2,received+5,received+14),label+" received mass");
    for(var i=0;i<15;i++)Require(double.IsFinite(expected[i])&&double.IsFinite(received[i])&&Math.Abs(expected[i]-received[i])<=2e-7*Math.Max(1,Math.Abs(expected[i])),label+$" mass property {i}: {expected[i]:R}/{received[i]:R}");
    log(label+$" volume={expected[0]:R}/{received[0]:R}, area={expected[14]:R}/{received[14]:R}");
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

// Both independent results undergo exactly this same public-PK canonical
// attachment on copies. Raw bodies and their diagnostics remain untouched.
// Material orientation and every oriented fin have already been gated above.
static unsafe int CanonicalCopy(int source)
{
    int body=0;Check(PK_ENTITY_copy(source,&body),"canonical body copy");
    foreach(var faceTag in Tags(false,body,false))
    {
        var face=faceTag;
        var interiorOptions=new PK_FACE_find_interior_vec_o_t();PK_VECTOR_t point;PK_UV_t uv;
        Check(PK_FACE_find_interior_vec(face,&interiorOptions,&point,&uv),"canonical face interior");var desired=FaceNormal(face,point,out var cls);
        int old=0,geometry=0;Check(PK_FACE_ask_surf(face,&old),"canonical original surface");
        if(cls==PK_CLASS_plane)
        {
            PK_PLANE_sf_t sf;Check(PK_PLANE_ask(old,&sf),"canonical plane form");
            var normal=(sf.basis_set.axis.coord[0],sf.basis_set.axis.coord[1],sf.basis_set.axis.coord[2]);
            var flip=Math.Abs(normal.Item1)>1e-12?normal.Item1<0:Math.Abs(normal.Item2)>1e-12?normal.Item2<0:normal.Item3<0;
            if(flip)for(var i=0;i<3;i++)sf.basis_set.axis.coord[i]=-sf.basis_set.axis.coord[i];
            Check(PK_PLANE_create(&sf,&geometry),"canonical plane create");
        }
        else if(cls==PK_CLASS_cyl){PK_CYL_sf_t sf;Check(PK_CYL_ask(old,&sf),"canonical cylinder form");Check(PK_CYL_create(&sf,&geometry),"canonical cylinder create");}
        else if(cls==PK_CLASS_cone){PK_CONE_sf_t sf;Check(PK_CONE_ask(old,&sf),"canonical cone form");Check(PK_CONE_create(&sf,&geometry),"canonical cone create");}
        else if(cls==PK_CLASS_sphere){PK_SPHERE_sf_t sf;Check(PK_SPHERE_ask(old,&sf),"canonical sphere form");Check(PK_SPHERE_create(&sf,&geometry),"canonical sphere create");}
        else if(cls==PK_CLASS_torus){PK_TORUS_sf_t sf;Check(PK_TORUS_ask(old,&sf),"canonical torus form");Check(PK_TORUS_create(&sf,&geometry),"canonical torus create");}
        else throw new InvalidOperationException("canonicalization unsupported surface class "+cls);
        Check(PK_SURF_parameterise_vector(geometry,point,&uv),"canonical surface parameter");PK_VECTOR_t value,normalValue;
        Check(PK_SURF_eval_with_normal(geometry,uv,0,0,PK_LOGICAL_false,&value,&normalValue),"canonical surface normal");
        var actual=(normalValue.coord[0],normalValue.coord[1],normalValue.coord[2]);var sense=VectorDot(desired,actual)>0?PK_LOGICAL_true:PK_LOGICAL_false;
        Check(PK_TOPOL_detach_geom(face),"canonical surface detach");
        Check(PK_FACE_attach_surfs(1,&face,&geometry,&sense),"canonical surface attach");
    }
    PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];int* vertices=stackalloc int[2];
    foreach(var edgeTag in Tags(false,body,true))
    {
        var edge=edgeTag;int old=0,cls=0;var bounds=new PK_INTERVAL_t();PK_LOGICAL_t orientation=default;
        Check(PK_EDGE_ask_geometry(edge,PK_LOGICAL_true,&old,&cls,ends,&bounds,&orientation),"canonical edge geometry");int curve=0;
        Check(PK_EDGE_ask_vertices(edge,vertices),"canonical edge vertices");
        var reverse=orientation==PK_LOGICAL_false;
        if(cls==PK_CLASS_line)
        {
            var a=VertexPosition(false,vertices[0]);var b=VertexPosition(false,vertices[1]);var d=(b.X-a.X,b.Y-a.Y,b.Z-a.Z);var length=Math.Sqrt(VectorDot(d,d));
            var sf=new PK_LINE_sf_t(new PK_AXIS1_sf_t(new(a.X,a.Y,a.Z),new(d.Item1/length,d.Item2/length,d.Item3/length)));
            Check(PK_LINE_create(&sf,&curve),"canonical line create");
        }
        else
        {
            if(cls==PK_CLASS_circle)
            {
                PK_CIRCLE_sf_t sf;Check(PK_CIRCLE_ask(old,&sf),"canonical circle form");if(reverse)for(var i=0;i<3;i++)sf.basis_set.axis.coord[i]=-sf.basis_set.axis.coord[i];
                Check(PK_CIRCLE_create(&sf,&curve),"canonical circle create");
            }
            else if(cls==PK_CLASS_ellipse)
            {
                PK_ELLIPSE_sf_t sf;Check(PK_ELLIPSE_ask(old,&sf),"canonical ellipse form");if(reverse)for(var i=0;i<3;i++)sf.basis_set.axis.coord[i]=-sf.basis_set.axis.coord[i];
                Check(PK_ELLIPSE_create(&sf,&curve),"canonical ellipse create");
            }
            else throw new InvalidOperationException("canonicalization unsupported curve class "+cls);
        }
        Check(PK_TOPOL_detach_geom(edge),"canonical curve detach");
        Check(PK_EDGE_attach_curves(1,&edge,&curve),"canonical curve attach");
    }
    return body;
}
static unsafe void DumpCurve(int entity,Action<string> log)
{
    int cls=0;Check(PK_ENTITY_ask_class(entity,&cls),"diagnostic class");
    if(cls>=PK_CLASS_line&&cls<=PK_CLASS_trcurve)
    {
        int count=0;int* owners=null;Check(PK_CURVE_ask_edges(entity,&count,&owners),"diagnostic curve owners");
        if(count>0)
        {
            int curve=0;int curveClass=0;var bounds=new PK_INTERVAL_t();PK_LOGICAL_t orientation=default;PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
            Check(PK_EDGE_ask_geometry(owners[0],PK_LOGICAL_true,&curve,&curveClass,ends,&bounds,&orientation),"diagnostic orientation");
            log($"    curve={entity} owner={owners[0]} sense={(byte)orientation} t={bounds.value[0]:R},{bounds.value[1]:R}");
        }
        Free(false,owners);
    }
    if(cls==PK_CLASS_edge)
    {
        int curve=0;var bounds=new PK_INTERVAL_t();PK_LOGICAL_t sense=default;PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
        Check(PK_EDGE_ask_geometry(entity,PK_LOGICAL_true,&curve,&cls,ends,&bounds,&sense),"diagnostic edge geometry");
        log($"    edge={entity} curve={curve} sense={(byte)sense} t={bounds.value[0]:R},{bounds.value[1]:R}");entity=curve;
    }
    if(cls==PK_CLASS_ellipse)
    {
        PK_ELLIPSE_sf_t sf;Check(PK_ELLIPSE_ask(entity,&sf),"diagnostic ellipse");
        log($"    ellipse={entity} p=({sf.basis_set.location.coord[0]:R},{sf.basis_set.location.coord[1]:R},{sf.basis_set.location.coord[2]:R}) axis=({sf.basis_set.axis.coord[0]:R},{sf.basis_set.axis.coord[1]:R},{sf.basis_set.axis.coord[2]:R}) ref=({sf.basis_set.ref_direction.coord[0]:R},{sf.basis_set.ref_direction.coord[1]:R},{sf.basis_set.ref_direction.coord[2]:R})");
    }
    else if(cls==PK_CLASS_circle)
    {
        PK_CIRCLE_sf_t sf;Check(PK_CIRCLE_ask(entity,&sf),"diagnostic circle");
        log($"    circle={entity} p=({sf.basis_set.location.coord[0]:R},{sf.basis_set.location.coord[1]:R},{sf.basis_set.location.coord[2]:R}) axis=({sf.basis_set.axis.coord[0]:R},{sf.basis_set.axis.coord[1]:R},{sf.basis_set.axis.coord[2]:R})");
    }
    else log($"    entity={entity} class={cls}");
}

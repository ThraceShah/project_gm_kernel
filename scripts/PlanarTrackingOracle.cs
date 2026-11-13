#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=PlanarTrackingOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var outputName=args.Single(x=>x.StartsWith("--output-name=",StringComparison.Ordinal))["--output-name=".Length..];
if(outputName.Length==0||!outputName.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'))throw new ArgumentException("One new output name");
var nativeContract=args.Contains("--native-basic-complete",StringComparer.Ordinal);
var trackingType=args.Contains("--complete",StringComparer.Ordinal)||nativeContract?PK_boolean_track_type_comp_c:PK_boolean_track_type_basic_c;
var directory=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","planar-tracking-oracle",outputName));
if(Directory.Exists(directory))throw new ArgumentException("Existing execution evidence is immutable");
Directory.CreateDirectory(directory);var report=new StringBuilder();
void Log(string value){Console.WriteLine(value);report.AppendLine(value);}
try
{
    if(!ParasolidScriptHost.TryStartSession("Independent planar tracking contract oracle",out var host,out var reason))throw new Exception(reason);
    using(host)unsafe
    {
        var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed start");
        try
        {
            Log("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
            PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"native kernel version");Log($"PK_VERSION {version.major_revision}.{version.minor_revision}.{version.build_number}");
            Log($"MODE managed={!nativeContract} actual_tracking={trackingType} reference_tracking={(nativeContract?PK_boolean_track_type_basic_c:trackingType)}");
            var fixtures=new List<(string Name,int Operation)>();
            foreach(var operation in new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})
            foreach(var name in new[]{"coincident","overlap","aligned","touching","disjoint","rotated","cavity","through-hole","two-tools"})fixtures.Add((name,operation));
            if(!nativeContract)foreach(var name in new[]{"imprint-solid","imprint-oblique","imprint-sheet","imprint-miss","imprint-existing-edge"})fixtures.Add((name,0));
            var failures=0;
            foreach(var fixture in fixtures)
            {
                var name=fixture.Name+"-"+fixture.Operation;
                try
                {
                    var actual=Produce(!nativeContract,fixture.Name,fixture.Operation,trackingType,out var ownRecords,out var ownStatus);
                    var expected=Produce(false,fixture.Name,fixture.Operation,nativeContract?PK_boolean_track_type_basic_c:trackingType,out var pkRecords,out var pkStatus);
                    Require(ownStatus==pkStatus,$"status {ownStatus}/{pkStatus}");Require(actual.Length==expected.Length,$"body count {actual.Length}/{expected.Length}");
                    Log(name+$" tracking {(nativeContract?"native_complete":"gm")}={ownRecords.Length}, {(nativeContract?"native_basic":"pk")}={pkRecords.Length}");
                    if(!ownRecords.SequenceEqual(pkRecords))
                    {
                        foreach(var record in ownRecords.Except(pkRecords))Log((nativeContract?"  native-complete-only ":"  gm-only ")+record);
                        foreach(var record in pkRecords.Except(ownRecords))Log((nativeContract?"  native-basic-only ":"  pk-only ")+record);
                        throw new InvalidOperationException("tracking record types, original classes/geometric sources, or geometric products differ");
                    }
                    var originals=expected.OrderBy(b=>BodyKey(false,b),StringComparer.Ordinal).ToArray();
                    var received=new List<int>();
                    foreach(var body in actual)
                    {
                        var bytes=Transmit(!nativeContract,body);File.WriteAllBytes(Path.Combine(directory,name+"-"+received.Count+(nativeContract?"-native-complete.x_t":"-managed.x_t")),bytes);
                        received.Add(Receive(bytes));
                    }
                    received.Sort((a,b)=>StringComparer.Ordinal.Compare(BodyKey(false,a),BodyKey(false,b)));
                    for(var i=0;i<originals.Length;i++)
                    {
                        CheckBody(originals[i],name+" independent",Log);CheckBody(received[i],name+" imported",Log);
                        File.WriteAllBytes(Path.Combine(directory,name+"-"+i+"-reference.x_t"),Transmit(false,originals[i]));
                        CompareBodies(originals[i],received[i],name,Log);
                    }
                    Log("PASS "+name);
                }
                catch(Exception error){failures++;Log("FAIL "+name+": "+error.Message);}
            }
            try{if(!nativeContract){CheckImportedNegativeLineTrim(directory,Log);Log("PASS imported-negative-line-use-trim");}}
            catch(Exception error){failures++;Log("FAIL imported-negative-line-use-trim: "+error.Message);}
            Log($"Planar tracking oracle: {fixtures.Count+(nativeContract?0:1)-failures} passed, {failures} failed.");Require(failures==0,"tracking oracle failed");
        }
        finally{Check(KernelRuntime.SessionStop(),"managed stop");}
    }
}
catch(Exception error){Log("FAIL: "+error.Message);Environment.ExitCode=1;}
finally{File.WriteAllText(Path.Combine(directory,"report.txt"),report.ToString());}

static unsafe int[] Produce(bool managed,string fixture,int operation,int trackingType,out string[] signature,out int status)
{
    var isImprint=operation==0;var sheet=fixture=="imprint-sheet";
    var target=sheet?Sheet(managed):Block(managed,0,0,0,fixture is "cavity" or "through-hole"?4:fixture=="two-tools"?10:2,0);
    var count=isImprint?0:fixture=="two-tools"?2:1;int* tools=stackalloc int[Math.Max(1,count)];
    if(count>0)
    {
        var x=fixture switch{"overlap"=>.5,"aligned"=>1,"touching"=>2,"disjoint"=>4,"rotated"=>.4,"two-tools"=>-3,_=>0};
        tools[0]=Block(managed,x,fixture=="overlap"?.5:0,fixture=="cavity"?1:0,2,fixture=="rotated"?.37:0,fixture is "through-hole"?4:fixture=="two-tools"?10:2);
        if(count==2)tools[1]=Block(managed,3,0,0,2,0,10);
    }
    var sources=new Dictionary<int,(int Class,string Shape)>();CacheSources(managed,target,"T",sources);
    for(var i=0;i<count;i++)CacheSources(managed,tools[i],"U"+i,sources);
    if(isImprint)
    {
        var axis=sheet?new PK_VECTOR1_t(1,0,0):fixture=="imprint-oblique"?new PK_VECTOR1_t(1/Math.Sqrt(2),0,1/Math.Sqrt(2)):new PK_VECTOR1_t(0,0,1);
        var z=fixture=="imprint-miss"?3:fixture=="imprint-existing-edge"||sheet?0:1;
        var definition=new PK_PLANE_sf_t(new(new(0,0,z),axis,new(0,1,0)));int plane=0;
        Check(managed?KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&definition,&plane):PK_PLANE_create(&definition,&plane),"create imprint plane");
        var options=new PK_BODY_imprint_plane_o_t();
        if(managed)
        {
            var track=new M.PK_TOPOL_track_r_s();status=KernelRuntime.BodyImprintPlane2(target,plane,(M.PK_BODY_imprint_plane_o_s*)&options,&track);
            Require(status==(fixture=="imprint-miss"?PK_ERROR_partial_no_intersect:0),"managed modern imprint status: "+status);
            try{signature=Signature(true,(PK_TOPOL_track_r_t*)&track,sources);}finally{Check(KernelRuntime.TopolTrackResultFree(&track),"managed imprint tracking free");}
        }
        else
        {
            var track=new PK_TOPOL_track_r_t();status=PK_BODY_imprint_plane_2(target,plane,&options,&track);
            Require(status==(fixture=="imprint-miss"?PK_ERROR_partial_no_intersect:0),"PK modern imprint status: "+status);
            try{signature=Signature(false,&track,sources);}finally{Check(PK_TOPOL_track_r_f(&track),"PK imprint tracking free");}
        }
        return [target];
    }
    var boolean=new PK_BODY_boolean_o_t{function=operation,tracking=PK_LOGICAL_true,tracking_type=trackingType};
    if(managed)
    {
        var track=new M.PK_TOPOL_track_r_s();var result=new M.PK_boolean_r_s();
        Check(KernelRuntime.BodyBoolean2(target,count,tools,(M.PK_BODY_boolean_o_s*)&boolean,&track,&result),"managed tracked Boolean");
        try{signature=Signature(true,(PK_TOPOL_track_r_t*)&track,sources);status=result.result;return new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray();}
        finally{Check(KernelRuntime.TopolTrackResultFree(&track),"managed Boolean tracking free");Check(KernelRuntime.BooleanResultFree(&result),"managed Boolean result free");}
    }
    else
    {
        var track=new PK_TOPOL_track_r_t();var result=new PK_boolean_r_t();Check(PK_BODY_boolean_2(target,count,tools,&boolean,&track,&result),"PK tracked Boolean");
        try{signature=Signature(false,&track,sources);status=result.result;return new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray();}
        finally{Check(PK_TOPOL_track_r_f(&track),"PK Boolean tracking free");Check(PK_boolean_r_f(&result),"PK Boolean result free");}
    }
}
static unsafe void CacheSources(bool managed,int body,string name,Dictionary<int,(int Class,string Shape)> cache)
{
    cache[body]=(PK_CLASS_body,name+":body");
    foreach(var cls in new[]{PK_CLASS_face,PK_CLASS_edge,PK_CLASS_vertex})
        foreach(var tag in Tags(managed,body,cls))cache.Add(tag,(cls,name+":"+GeometryKey(managed,tag,cls)));
}
static unsafe string[] Signature(bool managed,PK_TOPOL_track_r_t* tracking,Dictionary<int,(int Class,string Shape)> sources)
{
    Require(tracking->n_track_records>=0,"negative tracking count");var result=new string[tracking->n_track_records];
    for(var i=0;i<result.Length;i++)
    {
        var record=tracking->track_records[i];var originals=new List<string>();var products=new List<string>();
        for(var j=0;j<record.n_original_topols;j++)
        {
            Require(sources.TryGetValue(record.original_topols[j],out var source),"tracking origin was not an original input topology");
            Require(record.original_classes[j]==source.Class,"tracking original class differs from cached pre-operation class");originals.Add(source.Shape);
        }
        for(var j=0;j<record.n_product_topols;j++)
        {
            int cls=0;Check(managed?KernelRuntime.EntityAskClass(record.product_topols[j],&cls):PK_ENTITY_ask_class(record.product_topols[j],&cls),"tracking product must be live");
            products.Add(GeometryKey(managed,record.product_topols[j],cls));
        }
        originals.Sort(StringComparer.Ordinal);products.Sort(StringComparer.Ordinal);result[i]=record.track+" ["+string.Join(";",originals)+"] -> ["+string.Join(";",products)+"]";
    }
    Array.Sort(result,StringComparer.Ordinal);return result;
}
static unsafe string GeometryKey(bool managed,int tag,int cls)
{
    if(cls==PK_CLASS_vertex)return "V:"+VertexKey(managed,tag);
    if(cls==PK_CLASS_edge)
    {
        int* vertices=stackalloc int[2];Check(managed?KernelRuntime.EdgeAskVertices(tag,vertices):PK_EDGE_ask_vertices(tag,vertices),"tracked edge vertices");
        Require(vertices[0]!=0&&vertices[1]!=0,"tracking oracle expects finite line edges");var endpoints=new[]{VertexKey(managed,vertices[0]),VertexKey(managed,vertices[1])};Array.Sort(endpoints,StringComparer.Ordinal);
        int curve=0,type=0;Check(managed?KernelRuntime.EdgeAskCurve(tag,&curve):PK_EDGE_ask_curve(tag,&curve),"tracked edge curve");
        Check(managed?KernelRuntime.EntityAskClass(curve,&type):PK_ENTITY_ask_class(curve,&type),"tracked curve class");Require(type==PK_CLASS_line,"non-line planar edge");return "E:"+string.Join("/",endpoints);
    }
    if(cls==PK_CLASS_face)
    {
        int surface=0,type=0;PK_LOGICAL_t sense=default;Check(managed?KernelRuntime.FaceAskOrientedSurf(tag,&surface,(byte*)&sense):PK_FACE_ask_oriented_surf(tag,&surface,&sense),"tracked face surface");
        Check(managed?KernelRuntime.EntityAskClass(surface,&type):PK_ENTITY_ask_class(surface,&type),"tracked surface class");Require(type==PK_CLASS_plane,"non-plane tracked face");
        PK_PLANE_sf_t plane;Check(managed?KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&plane):PK_PLANE_ask(surface,&plane),"tracked plane");
        var sign=sense==PK_LOGICAL_true?1:-1;var n=new double[3];var distance=0.0;for(var i=0;i<3;i++){n[i]=plane.basis_set.axis.coord[i]*sign;distance+=plane.basis_set.location.coord[i]*n[i];}
        int count=0;int* edges=null;Check(managed?KernelRuntime.FaceAskEdges(tag,&count,&edges):PK_FACE_ask_edges(tag,&count,&edges),"tracked face edges");
        try
        {
            var boundary=new string[count];for(var i=0;i<count;i++)boundary[i]=GeometryKey(managed,edges[i],PK_CLASS_edge);Array.Sort(boundary,StringComparer.Ordinal);
            return "F:"+Number(n[0])+","+Number(n[1])+","+Number(n[2])+":"+Number(distance)+"{"+string.Join("|",boundary)+"}";
        }
        finally{Free(managed,edges);}
    }
    throw new InvalidOperationException("unsupported tracking topology class "+cls);
}
static unsafe string VertexKey(bool managed,int vertex)
{
    int point=0;Check(managed?KernelRuntime.VertexAskPoint(vertex,&point):PK_VERTEX_ask_point(vertex,&point),"tracked vertex point");
    PK_POINT_sf_t p;Check(managed?KernelRuntime.PointAsk(point,(M.PK_POINT_sf_s*)&p):PK_POINT_ask(point,&p),"tracked point");return Number(p.position.coord[0])+","+Number(p.position.coord[1])+","+Number(p.position.coord[2]);
}
static string Number(double value)
{var rounded=Math.Round(value,8);return rounded==0?"0":rounded.ToString("0.########",CultureInfo.InvariantCulture);}
static unsafe string BodyKey(bool managed,int body)=>string.Join(";",Tags(managed,body,PK_CLASS_vertex).Select(t=>VertexKey(managed,t)).Order(StringComparer.Ordinal));
static unsafe int[] Tags(bool managed,int body,int cls)
{
    int count=0;int* tags=null;var error=managed?(cls==PK_CLASS_face?KernelRuntime.BodyAskFaces(body,&count,&tags):cls==PK_CLASS_edge?KernelRuntime.BodyAskEdges(body,&count,&tags):KernelRuntime.BodyAskVertices(body,&count,&tags)):
        cls==PK_CLASS_face?PK_BODY_ask_faces(body,&count,&tags):cls==PK_CLASS_edge?PK_BODY_ask_edges(body,&count,&tags):PK_BODY_ask_vertices(body,&count,&tags);
    Check(error,"cache input topology");try{return new ReadOnlySpan<int>(tags,count).ToArray();}finally{Free(managed,tags);}
}
static unsafe int Block(bool managed,double x,double y,double z,double size,double angle,double height=0)
{
    var basis=new PK_AXIS2_sf_t(new(x,y,z),new(0,0,1),new(Math.Cos(angle),Math.Sin(angle),0));int body=0;
    Check(managed?KernelRuntime.BodyCreateSolidBlock(size,size,height>0?height:size,(M.PK_AXIS2_sf_s*)&basis,&body):PK_BODY_create_solid_block(size,size,height>0?height:size,&basis,&body),"independent block");return body;
}
static unsafe int Sheet(bool managed)
{
    PK_VECTOR_t* points=stackalloc PK_VECTOR_t[4]{new(-1,-1,0),new(1,-1,0),new(1,1,0),new(-1,1,0)};
    var options=new PK_BODY_create_sheet_planar_o_t();options.plane=new(new(0,0,0),new(0,0,1));options.basis_set=new(new(0,0,0),new(0,0,1),new(1,0,0));int body=0;
    Check(managed?KernelRuntime.BodyCreateSheetPlanar(4,(M.PK_VECTOR_s*)points,(M.PK_BODY_create_sheet_planar_o_s*)&options,&body):PK_BODY_create_sheet_planar(4,points,&options,&body),"independent sheet");return body;
}
static unsafe void CheckImportedNegativeLineTrim(string directory,Action<string> log)
{
    // Create the representation through real public PK calls: the edge
    // direction stays fixed while its attached line parameterization reverses.
    var reference=Sheet(false);var planeForm=new PK_PLANE_sf_t(new(new(0,0,0),new(1,0,0),new(0,1,0)));int plane=0;
    Check(PK_PLANE_create(&planeForm,&plane),"negative-use imprint plane");int count=0;int* barriers=null;
    Check(PK_BODY_imprint_plane(reference,plane,1e-8,&count,&barriers),"negative-use imprint");Require(count==1,"one sheet barrier");
    var barrier=barriers[0];Free(false,barriers);var barrierShape=GeometryKey(false,barrier,PK_CLASS_edge);int reversed=0;int* vertices=stackalloc int[2];
    foreach(var edge in Tags(false,reference,PK_CLASS_edge))
    {
        Check(PK_EDGE_ask_vertices(edge,vertices),"negative-use edge endpoints");var a=VertexPosition(vertices[0]);var b=VertexPosition(vertices[1]);
        if(Math.Abs(a.coord[0]+1)>1e-10||Math.Abs(b.coord[0]+1)>1e-10)continue;
        var dx=a.coord[0]-b.coord[0];var dy=a.coord[1]-b.coord[1];var dz=a.coord[2]-b.coord[2];var length=Math.Sqrt(dx*dx+dy*dy+dz*dz);
        var form=new PK_LINE_sf_t(new(b,new(dx/length,dy/length,dz/length)));int line=0;Check(PK_LINE_create(&form,&line),"reverse parameterized line");
        reversed=edge;Check(PK_TOPOL_detach_geom(edge),"detach retained boundary line");
        var negative=PK_LOGICAL_false;var attach=new PK_EDGE_attach_curves_o_t{have_senses=PK_LOGICAL_true,senses=&negative};var tracking=new PK_ENTITY_track_r_t();
        Check(PK_EDGE_attach_curves_2(1,&reversed,&line,&attach,&tracking),"attach reverse parameterized line");Check(PK_ENTITY_track_r_f(&tracking),"free line attach tracking");break;
    }
    Require(reversed!=0,"retained edge selected");PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];var interval=new PK_INTERVAL_t();int curve=0,cls=0;PK_LOGICAL_t sense=default;
    Check(PK_EDGE_ask_geometry(reversed,PK_LOGICAL_true,&curve,&cls,ends,&interval,&sense),"negative line use check");
    Require(sense==PK_LOGICAL_false,"native edge must use line negatively");
    CheckBody(reference,"negative line source",log);var source=Transmit(false,reference);File.WriteAllBytes(Path.Combine(directory,"negative-line-source.x_t"),source);
    var actual=ReceiveManaged(source);var actualBarrier=Tags(true,actual,PK_CLASS_edge).Single(e=>GeometryKey(true,e,PK_CLASS_edge)==barrierShape);
    var point=new PK_VECTOR_t(-.5,0,0);int enclosure=0,seed=0;Check(PK_BODY_contains_vector(reference,point,&enclosure,&seed),"native trim seed");
    Require(enclosure==PK_enclosure_on_c,"native seed is on sheet");Check(PK_BODY_trim(reference,1,&barrier,1,&seed,PK_LOGICAL_true),"native trim negative-use sheet");
    var ownPoint=Unsafe.BitCast<PK_VECTOR_t,M.PK_VECTOR_s>(point);Check(KernelRuntime.BodyContainsVector(actual,ownPoint,&enclosure,&seed),"managed imported trim seed");
    Require(enclosure==PK_enclosure_on_c,"managed seed is on sheet");Check(KernelRuntime.BodyTrim(actual,1,&actualBarrier,1,&seed,1),"managed trim negative-use imported sheet");
    var received=Receive(Transmit(true,actual));CheckBody(reference,"negative line trimmed reference",log);CheckBody(received,"negative line trimmed imported",log);
    CompareBodies(reference,received,"imported-negative-line-use-trim",log);
}
static unsafe PK_VECTOR_t VertexPosition(int vertex)
{
    int point=0;Check(PK_VERTEX_ask_point(vertex,&point),"probe vertex point");PK_POINT_sf_t form;Check(PK_POINT_ask(point,&form),"probe point");return form.position;
}
static unsafe int ReceiveManaged(byte[] bytes)
{
    fixed(byte* data=bytes)
    {
        var block=new M.PK_MEMORY_block_s{bytes=data,n_bytes=(nuint)bytes.Length};var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=PK_transmit_format_text_c};int count=0;int* bodies=null;
        Check(KernelRuntime.PartReceiveB(block,&options,&count,&bodies),"managed receive independent XT");
        try{Require(count==1,"one imported body");return bodies[0];}finally{Free(true,bodies);}
    }
}
static unsafe void Free(bool managed,void* memory){if(memory!=null)Check(managed?KernelRuntime.MemoryFree(memory):PK_MEMORY_free(memory),"free result");}
static string ScriptPath([CallerFilePath]string path="")=>path;
static void Check(int error,string operation){if(error!=0)throw new InvalidOperationException(operation+": "+error);}
static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}


static unsafe byte[] Transmit(bool managed,int body)=>managed?TransmitManaged(body):TransmitReference(body);

static unsafe void CheckBody(int body,string name,Action<string> log)
{
    var options=new PK_BODY_check_o_t();options.max_faults=100;int count=0;PK_check_fault_t* faults=null;
    Check(PK_BODY_check(body,&options,&count,&faults),name+" full PK_BODY_check");
    try{for(var i=0;i<count;i++)log($"{name} fault={faults[i].state} entity={faults[i].entity_1} other={faults[i].entity_2}");Require(count==0,name+" geometry/topology faults: "+count);}
    finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free PK fault list");}
}

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

static unsafe byte[] TransmitManaged(int body)
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

static unsafe PK_BODY_t Receive(byte[] bytes)
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

static unsafe void CompareBodies(PK_BODY_t reference, PK_BODY_t actual, string label, Action<string> log)
{
    for(var kind=0;kind<5;kind++)Require(OracleCount(reference,kind)==OracleCount(actual,kind),label+" region/shell/face/edge/vertex count differs at "+kind);
    var options = new PK_DEBUG_BODY_compare_o_t
    {
        max_diffs = 64,
        all_tests = PK_LOGICAL_false,
        num_tests = PK_LOGICAL_true,
        tol_dev_tests = PK_LOGICAL_true,
        tol_tests = PK_LOGICAL_true,
        class_tests = PK_LOGICAL_true,
        sense_tests = PK_LOGICAL_true,
        missing_tests = PK_LOGICAL_true,
        // Exact parameter matching depends on the producer's parameterization.
        // Geometric tolerance/deviation and topology remain strict gates.
        acc_dev_tests = PK_LOGICAL_false,
        non_match_tests = PK_LOGICAL_false,
    };
    var result = new PK_DEBUG_BODY_compare_r_t();
    Check(PK_DEBUG_BODY_compare(reference, actual, &options, &result), label + " body compare");
    try
    {
        log($"{label} body compare: global={result.global_result}, local={result.local_result}, global entries={result.n_global_diffs}, face pairs={result.n_face_pairs}.");
        for (var i = 0; i < result.n_global_diffs; i++)
        {
            var diff = result.global_diffs[i];
            log($"  global entry={diff.diff}, master count={diff.n_masters}, received count={diff.n_similars}");
        }
        var localDifferences = 0;
        for (var i = 0; i < result.n_face_pairs; i++)
        for (var j = 0; j < result.face_pairs[i].n_local_diffs; j++)
        {
            var diff = result.face_pairs[i].local_diffs[j];
            localDifferences++;
            log($"  face pair={i}, diff={diff.diff}, master entity={diff.master_entity}, received entity={diff.similar_entity}, master value={diff.master_double:R}, received value={diff.similar_double:R}");
        }
        // PK includes equal-count records in global_diffs even when its
        // global and local result flags both report no differences.
        Require(result.global_result == PK_DEBUG_global_res_no_diffs_c
            && result.local_result == PK_DEBUG_local_res_no_diffs_c
            && localDifferences == 0, label + " body structure/sense/tolerance comparison failed");
    }
    finally { Check(PK_DEBUG_BODY_compare_r_f(&result), label + " free body compare"); }
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

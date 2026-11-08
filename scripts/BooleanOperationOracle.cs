#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=BooleanOperationOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

global using ManagedBodyTag=System.Int32;
using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var thirdFaceHole=args.Contains("--three-plane-hole");
var thirdFaceCylinder=args.Contains("--three-face-cylinder");
var threePlaneOnly=args.Contains("--three-plane-events")||thirdFaceHole||thirdFaceCylinder;
var output=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","boolean-operation-oracle"));
Directory.CreateDirectory(output);var log=new StringBuilder();
void Log(string s){Console.WriteLine(s);log.AppendLine(s);}
try
{
    if(!ParasolidScriptHost.TryStartSession("Independent Boolean modeling oracle",out var host,out var message))throw new Exception(message);
    using(host) unsafe
    {
        var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"own start");
        try
        {
            var failures=0;var count=0;
            foreach(var mode in threePlaneOnly?[PK_boolean_subtract_c]:new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})
            foreach(var fixture in threePlaneOnly?(thirdFaceHole?["guarded-three-plane-hole"]:thirdFaceCylinder?["cylindrical-third-face","guarded-cylindrical-third-face"]
                :new[]{"three-plane-cuts","guarded-three-plane-cuts"}):new[]{"coincident","overlap-two-axes","aligned","touching-face","disjoint","nested","crossing-rotated","translated-large","two-tools","closed-cavity","through-hole"})
            {
                var label=$"{fixture}-{mode}";count++;
                try
                {
                    var own=Produce(true,fixture,mode,out var ownStatus);var reference=Produce(false,fixture,mode,out var pkStatus);
                    Require(ownStatus==pkStatus,$"status own={ownStatus} PK={pkStatus}");
                    Require(own.Length==reference.Length,$"body count own={own.Length} PK={reference.Length}");
                    var received=new List<int>();
                    foreach(var body in own)
                    {
                        var bytes=TransmitManaged(body);File.WriteAllBytes(Path.Combine(output,$"{label}-{received.Count}-managed.x_t"),bytes);
                        var result=ReceiveOracle(bytes);CheckReferenceBody(result,label+" received",Log);received.Add(result);
                    }
                    received.Sort((a,b)=>Center(a).CompareTo(Center(b)));Array.Sort(reference,(a,b)=>Center(a).CompareTo(Center(b)));
                    for(var b=0;b<reference.Length;b++)
                    {
                        CheckReferenceBody(reference[b],label+" baseline",Log);
                        File.WriteAllBytes(Path.Combine(output,$"{label}-{b}-reference.x_t"),TransmitReference(reference[b]));
                        for(var k=0;k<5;k++)Require(OracleCount(reference[b],k)==OracleCount(received[b],k),$"kind{k}: expected{OracleCount(reference[b],k)} received{OracleCount(received[b],k)}");
                        if(threePlaneOnly)ParasolidBodyCompare.AssertEquivalent(reference[b],received[b],label);
                        else CompareBodies(reference[b],received[b],label,Log);
                    }
                    Log("PASS "+label);
                }
                catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.Message);}
            }
            foreach(var fixture in threePlaneOnly?[]:new[]{"imprint","oblique-imprint","trim-keep","trim-remove","section-both","section-front","section-back","section-miss","section-coincident"})
            {
                count++;
                try
                {
                    var own=ProduceEdit(true,fixture,out var ownContract);var reference=ProduceEdit(false,fixture,out var pkContract);
                    Require(ownContract==pkContract,$"{fixture} contract own={ownContract} PK={pkContract}");
                    Require(own.Length==reference.Length,$"{fixture} body counts");
                    for(var i=0;i<own.Length;i++)
                    {
                        var bytes=TransmitManaged(own[i]);File.WriteAllBytes(Path.Combine(output,$"{fixture}-{i}-managed.x_t"),bytes);
                        File.WriteAllBytes(Path.Combine(output,$"{fixture}-{i}-reference.x_t"),TransmitReference(reference[i]));
                        var actual=ReceiveOracle(bytes);CheckReferenceBody(actual,fixture+" received",Log);CheckReferenceBody(reference[i],fixture+" reference",Log);
                        for(var k=0;k<5;k++)Require(OracleCount(reference[i],k)==OracleCount(actual,k),fixture+" topology "+k);
                        CompareBodies(reference[i],actual,fixture,Log);
                    }
                    Log("PASS "+fixture);
                }
                catch(Exception ex){failures++;Log("FAIL "+fixture+": "+ex.Message);}
            }
            // PK's default size box is [-500,500]^3. A 10000-unit fixture is
            // intentionally invalid on both producers, not a geometry failure
            // to suppress in the positive Boolean gate.
            if(!threePlaneOnly)try
            {
                count++;
                var actual=ReceiveOracle(TransmitManaged(Block(true,10000,0,0,2,2,2,0)));
                var reference=Block(false,10000,0,0,2,2,2,0);
                Require(SizeBoxFault(actual)&&SizeBoxFault(reference),"both producers must report the size-box fault");
                Log("PASS outside-default-size-box negative fixture");
            }
            catch(Exception ex){failures++;Log("FAIL size-box negative fixture: "+ex.Message);}
            Require(failures==0,$"{failures}/{count} independent modeling fixtures failed");Log($"PASS all {count} independent modeling fixtures");
        }
        finally{Check(KernelRuntime.SessionStop(),"own stop");}
    }
}
catch(Exception ex){Log("FAIL: "+ex.Message);Environment.ExitCode=1;}
finally{File.WriteAllText(Path.Combine(output,"report.txt"),log.ToString());}

static unsafe int[] Produce(bool own,string fixture,int function,out int status)
{
    var x=fixture switch{"overlap-two-axes"=>.5,"aligned"=>1,"touching-face"=>2,"disjoint"=>4,"crossing-rotated"=>.4,_=>0};
    var y=fixture=="overlap-two-axes"?.5:0;
    var offset=fixture=="translated-large"?100.0:0;
    var outer=fixture is "nested" or "closed-cavity" or "through-hole"?4:fixture=="two-tools"?10:2;
    var holed=fixture=="guarded-three-plane-hole";
    var mixed=fixture is "cylindrical-third-face" or "guarded-cylindrical-third-face";
    var guarded=fixture.StartsWith("guarded-",StringComparison.Ordinal);
    var threePlane=holed||mixed||fixture is "three-plane-cuts" or "guarded-three-plane-cuts";
    int target;
    if(threePlane)
    {
        var radius=mixed?5:4;var height=mixed?6:4;
        Check(own?KernelRuntime.BodyCreateSolidCyl(radius,height,null,&target):PK_BODY_create_solid_cyl(radius,height,null,&target),"cylindrical target");
    }
    else target=Block(own,offset,0,0,outer,outer,outer,0);
    if(holed)target=ThroughBore(own,target);
    var nt=fixture=="two-tools"||threePlane?2:1;var tools=stackalloc int[nt];
    if(threePlane)
    {
        tools[0]=mixed?Block(own,0,-.5,1,12,7,2,0):Block(own,0,0,3,2,2,2,0);
        tools[1]=mixed?Block(own,0,.5,2,14,7,2,0):Block(own,1,1,2.75,2,2,2.5,0);
        if(guarded)(tools[0],tools[1])=(tools[1],tools[0]);
    }
    else
    {
        tools[0]=Block(own,offset+(fixture=="two-tools"?-3:x),y,fixture is "nested" or "closed-cavity"?1:0,2,2,fixture is "two-tools" or "through-hole"?outer:2,fixture=="crossing-rotated"?.37:0);
        if(nt==2)tools[1]=Block(own,offset+3,0,0,2,2,outer,0);
    }
    var o=new PK_BODY_boolean_o_t{function=function};
    if(own)
    {
        var options=Unsafe.BitCast<PK_BODY_boolean_o_t,M.PK_BODY_boolean_o_s>(o);M.PK_TOPOL_track_r_s tracking=default;M.PK_boolean_r_s result=default;
        if(threePlane)
        {
            var certified=new HashSet<(int Event,int Curve)>();
            var transferredLines=new HashSet<(int Event,int Curve)>();
            var published=new HashSet<(int Event,int Curve)>();
            KernelRuntime.ForbidSpatialBoundaryReuse=guarded;
            var excluded=0;
            KernelRuntime.TraceFiniteBoundary=trace=>
            {
                if(trace.Stage==KernelRuntime.BoundaryTraceStage.ThirdFaceStation)certified.Add((trace.StartEvent,trace.Curve));
                if(trace.Stage==KernelRuntime.BoundaryTraceStage.NativeCutLocator&&trace.Witness!=0&&trace.Curve!=trace.ProductCurve
                    &&KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(trace.Curve)].Class==CurveClass.Line
                    &&KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(trace.ProductCurve)].Class==CurveClass.Line)
                    transferredLines.Add((trace.StartEvent,trace.ProductCurve));
                if(trace.Stage==KernelRuntime.BoundaryTraceStage.ThirdFaceOutsideTrim&&trace.Source>=0
                    &&KernelRuntime.Faces[trace.Source].LoopCount>1&&double.IsFinite(trace.First)&&double.IsFinite(trace.Last)&&trace.Last>=trace.First)excluded++;
                if(trace.Stage==KernelRuntime.BoundaryTraceStage.Product)
                {published.Add((trace.StartEvent,trace.Curve));published.Add((trace.EndEvent,trace.Curve));}
            };
            try
            {
                var error=KernelRuntime.BodyBoolean2(target,nt,tools,&options,&tracking,&result);
                Check(error,$"{fixture} Boolean at {KernelRuntime.LastRegularBooleanStage}/{KernelRuntime.LastSheetPartitionStage}, curve {KernelRuntime.LastSheetCurve}, t={KernelRuntime.LastSheetParameter:R}");
                var joined=certified.Where(published.Contains).GroupBy(x=>x.Event).Count(x=>x.Count()==3);
                var publishedTransfers=transferredLines.Count(published.Contains);
                Require(joined>0,"a three-plane event must retain all three native stations on published edges");
                if(holed)Require(excluded>0,"a carrier root inside the planar bore must be excluded by the actual trim");
                if(!holed&&!mixed)Require(publishedTransfers>0,"a transferred source line root must reach an actual published cut edge");
                Console.WriteLine($"THIRD FACE published_three_station_events={joined} finite_trim_exclusions={excluded} published_line_transfers={publishedTransfers}");
            }
            finally{KernelRuntime.TraceFiniteBoundary=null;KernelRuntime.ForbidSpatialBoundaryReuse=false;}
        }
        else Check(KernelRuntime.BodyBoolean2(target,nt,tools,&options,&tracking,&result),"own Boolean");
        status=result.result;var bodies=new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray();
        Check(KernelRuntime.TopolTrackResultFree(&tracking),"own tracking free");Check(KernelRuntime.BooleanResultFree(&result),"own result free");return bodies;
    }
    else
    {
        PK_TOPOL_track_r_t tracking=default;PK_boolean_r_t result=default;
        Check(PK_BODY_boolean_2(target,nt,tools,&o,&tracking,&result),"PK Boolean");
        status=result.result;var bodies=new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray();
        Check(PK_TOPOL_track_r_f(&tracking),"PK tracking free");Check(PK_boolean_r_f(&result),"PK result free");return bodies;
    }
}
static unsafe int ThroughBore(bool own,int body)
{
    var tool=Block(own,1,0,-1,.5,.5,6,0);
    var o=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c};
    if(own)
    {
        var options=Unsafe.BitCast<PK_BODY_boolean_o_t,M.PK_BODY_boolean_o_s>(o);
        M.PK_TOPOL_track_r_s tracking=default;M.PK_boolean_r_s result=default;
        Check(KernelRuntime.BodyBoolean2(body,1,&tool,&options,&tracking,&result),"own through bore");
        try{Require(result.n_bodies==1,"own through bore body");return result.bodies[0];}
        finally{Check(KernelRuntime.TopolTrackResultFree(&tracking),"own bore tracking");Check(KernelRuntime.BooleanResultFree(&result),"own bore result");}
    }
    else
    {
        PK_TOPOL_track_r_t tracking=default;PK_boolean_r_t result=default;
        Check(PK_BODY_boolean_2(body,1,&tool,&o,&tracking,&result),"PK through bore");
        try{Require(result.n_bodies==1,"PK through bore body");return result.bodies[0];}
        finally{Check(PK_TOPOL_track_r_f(&tracking),"PK bore tracking");Check(PK_boolean_r_f(&result),"PK bore result");}
    }
}
static unsafe int Block(bool own,double x,double y,double z,double dx,double dy,double dz,double angle)
{
    var axis=new PK_AXIS2_sf_t(new(x,y,z),new(0,0,1),new(Math.Cos(angle),Math.Sin(angle),0));int body=0;
    if(own){var a=Unsafe.BitCast<PK_AXIS2_sf_t,M.PK_AXIS2_sf_s>(axis);Check(KernelRuntime.BodyCreateSolidBlock(dx,dy,dz,&a,&body),"own block");}
    else Check(PK_BODY_create_solid_block(dx,dy,dz,&axis,&body),"PK block");return body;
}
static unsafe int[] ProduceEdit(bool own,string fixture,out string contract)
{
    var trim=fixture.StartsWith("trim");var body=trim?Sheet(own):Block(own,0,0,0,2,2,2,0);
    var z=fixture=="section-miss"?3:fixture=="section-coincident"?0:trim?0:1;
    var axis=new PK_AXIS2_sf_t(new(0,0,z),trim?new(1,0,0):fixture=="oblique-imprint"?new(1/Math.Sqrt(2),0,1/Math.Sqrt(2)):new(0,0,1),new(0,1,0));
    int plane=0;var pd=new PK_PLANE_sf_t(axis);
    if(own){var md=Unsafe.BitCast<PK_PLANE_sf_t,M.PK_PLANE_sf_s>(pd);Check(KernelRuntime.PlaneCreate(&md,&plane),"own plane");}
    else Check(PK_PLANE_create(&pd,&plane),"PK plane");
    if(fixture.StartsWith("section"))
    {
        var options=new PK_BODY_section_o_t{fence=fixture=="section-front"?PK_section_fence_front_c:fixture=="section-back"?PK_section_fence_back_c:PK_section_fence_both_c};
        if(own)
        {
            var o=Unsafe.BitCast<PK_BODY_section_o_t,M.PK_BODY_section_o_s>(options);M.PK_section_r_s r=default;
            Check(KernelRuntime.BodySectionWithSurf(body,plane,&o,&r),"own section");
            contract=$"{r.front_bodies.length}/{r.back_bodies.length}/{r.front_faces.length}/{r.back_faces.length}";
            var bodies=new ReadOnlySpan<int>(r.front_bodies.array,r.front_bodies.length).ToArray().Concat(new ReadOnlySpan<int>(r.back_bodies.array,r.back_bodies.length).ToArray()).ToArray();
            if(bodies.Length>0)Require(bodies[0]==body,"own section target identity");KernelRuntime.SectionResultFree(&r);return bodies;
        }
        else
        {
            PK_section_r_t r=default;Check(PK_BODY_section_with_surf(body,plane,&options,&r),"PK section");
            contract=$"{r.front_bodies.length}/{r.back_bodies.length}/{r.front_faces.length}/{r.back_faces.length}";
            var bodies=new ReadOnlySpan<int>(r.front_bodies.array,r.front_bodies.length).ToArray().Concat(new ReadOnlySpan<int>(r.back_bodies.array,r.back_bodies.length).ToArray()).ToArray();
            if(bodies.Length>0)Require(bodies[0]==body,"PK section target identity");PK_section_r_f(&r);return bodies;
        }
    }
    int count=0;int* edges=null;
    Check(own?KernelRuntime.BodyImprintPlane(body,plane,1e-8,&count,&edges):PK_BODY_imprint_plane(body,plane,1e-8,&count,&edges),"imprint plane");
    contract="edges="+count;
    if(trim)
    {
        var p=new PK_VECTOR_t(-.5,0,0);int enclosure=0,face=0;
        if(own)Check(KernelRuntime.BodyContainsVector(body,Unsafe.BitCast<PK_VECTOR_t,M.PK_VECTOR_s>(p),&enclosure,&face),"own trim seed");
        else Check(PK_BODY_contains_vector(body,p,&enclosure,&face),"PK trim seed");
        Require(enclosure==PK_enclosure_on_c,"trim seed on face");var keep=fixture=="trim-keep"?(byte)1:(byte)0;
        Check(own?KernelRuntime.BodyTrim(body,count,edges,1,&face,keep):PK_BODY_trim(body,count,edges,1,&face,keep),"trim sheet");
    }
    if(own)KernelRuntime.MemoryFree(edges);else PK_MEMORY_free(edges);
    return [body];
}
static unsafe int Sheet(bool own)
{
    PK_VECTOR_t* points=stackalloc PK_VECTOR_t[4]{new(-1,-1,0),new(1,-1,0),new(1,1,0),new(-1,1,0)};
    var options=new PK_BODY_create_sheet_planar_o_t();options.plane=new(new(0,0,0),new(0,0,1));
    options.basis_set=new(new(0,0,0),new(0,0,1),new(1,0,0));int body=0;
    if(own){var o=Unsafe.BitCast<PK_BODY_create_sheet_planar_o_t,M.PK_BODY_create_sheet_planar_o_s>(options);Check(KernelRuntime.BodyCreateSheetPlanar(4,(M.PK_VECTOR_s*)points,&o,&body),"own sheet");}
    else Check(PK_BODY_create_sheet_planar(4,points,&options,&body),"PK sheet");return body;
}
static unsafe bool SizeBoxFault(int body)
{
    var o=new PK_BODY_check_o_t{max_faults=100};int n=0;PK_check_fault_t* faults=null;Check(PK_BODY_check(body,&o,&n,&faults),"negative size-box check");
    try{return new ReadOnlySpan<PK_check_fault_t>(faults,n).ToArray().Any(f=>f.state==13810);}
    finally{PK_MEMORY_free(faults);}
}
static unsafe double Center(int body)
{
    int count=0;int* vertices=null;Check(PK_BODY_ask_vertices(body,&count,&vertices),"sort vertices");
    double sum=0;for(var i=0;i<count;i++){int point=0;Check(PK_VERTEX_ask_point(vertices[i],&point),"vertex point");PK_POINT_sf_t p=default;Check(PK_POINT_ask(point,&p),"point");sum+=p.position.coord[0];}
    PK_MEMORY_free(vertices);return sum/Math.Max(1,count);
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

static unsafe void CompareBodies(PK_BODY_t reference, PK_BODY_t actual, string label, Action<string> log)
{
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

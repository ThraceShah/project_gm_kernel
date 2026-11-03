#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=AnalyticSectionOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
global using ManagedBodyTag=System.Int32;
using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var output=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","analytic-section-oracle"));Directory.CreateDirectory(output);
var report=new StringBuilder();void Log(string line){Console.WriteLine(line);report.AppendLine(line);}
try
{
    if(!ParasolidScriptHost.TryStartSession("Independent analytic sphere section oracle",out var host,out var message))throw new Exception(message);
    using(host)unsafe
    {
        var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"self session");
        try
        {
            var fixtures=new SectionFixture[]{new("equator",2,0),new("lower",2,-.5),new("upper",2,.5),new("outside-front",2,-3),new("outside-back",2,3),
                new("oblique",2,.7,Nx:1/Math.Sqrt(14),Ny:2/Math.Sqrt(14),Nz:3/Math.Sqrt(14)),new("translated",2,.5,100,-200,50),
                new("larger",3.2,-1.5),new("reversed",2,.5,Nz:-1),new("small-cap",2,1.99)};
            var failures=0;var count=0;
            foreach(var fence in new[]{PK_section_fence_both_c,PK_section_fence_front_c,PK_section_fence_back_c})foreach(var fixture in fixtures)
            {
                var label=fixture.Name+"-"+fence;count++;
                try
                {
                    var actual=Produce(true,fixture,fence,out var actualIdentity);var expected=Produce(false,fixture,fence,out var expectedIdentity);
                    Require(actualIdentity==expectedIdentity,$"source/cap identity {actualIdentity}/{expectedIdentity}");Require(actual.Length==expected.Length,"result body count");
                    for(var i=0;i<actual.Length;i++)
                    {
                        CheckPointClassification(actual[i],expected[i],fixture);
                        var managedBytes=TransmitManaged(actual[i]);File.WriteAllBytes(Path.Combine(output,label+"-"+i+"-managed.x_t"),managedBytes);var received=ReceiveOracle(managedBytes);
                        var nativeBytes=TransmitReference(expected[i]);File.WriteAllBytes(Path.Combine(output,label+"-"+i+"-reference.x_t"),nativeBytes);
                        var imported=ReceiveManaged(nativeBytes);CheckPointClassification(imported,expected[i],fixture);
                        CheckReferenceBody(expected[i],label+" independent baseline",Log);CheckReferenceBody(received,label+" managed receive",Log);
                        for(var kind=0;kind<5;kind++)Require(OracleCount(expected[i],kind)==OracleCount(received,kind),$"topology kind {kind}: {OracleCount(received,kind)}/{OracleCount(expected[i],kind)}");
                        CompareBodies(expected[i],received,label,Log);CompareMass(expected[i],received,label,Log);
                    }
                    Log("PASS "+label);
                }
                catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.Message);}
            }
            Require(failures==0,$"{failures}/{count} analytic section fixtures failed");Log($"PASS all {count} independent sphere/plane section fixtures, classification, identities, XT checks and mass properties.");
        }
        finally{Check(KernelRuntime.SessionStop(),"self session stop");}
    }
}
catch(Exception ex){Log("FAIL: "+ex.Message);Environment.ExitCode=1;}
finally{File.WriteAllText(Path.Combine(output,"report.txt"),report.ToString());}

static unsafe int[] Produce(bool managed,SectionFixture fixture,int fence,out string identity)
{
    var sphereDefinition=new PK_AXIS2_sf_t(new(fixture.X,fixture.Y,fixture.Z),new(0,0,1),new(1,0,0));int body=0,plane=0;
    var reference=PlaneReference(fixture);var planeDefinition=new PK_PLANE_sf_t(new(new(fixture.X+fixture.H*fixture.Nx,fixture.Y+fixture.H*fixture.Ny,fixture.Z+fixture.H*fixture.Nz),new(fixture.Nx,fixture.Ny,fixture.Nz),reference));
    if(managed)
    {
        var sphere=Unsafe.BitCast<PK_AXIS2_sf_t,M.PK_AXIS2_sf_s>(sphereDefinition);var planar=Unsafe.BitCast<PK_PLANE_sf_t,M.PK_PLANE_sf_s>(planeDefinition);
        Check(KernelRuntime.BodyCreateSolidSphere(fixture.Radius,&sphere,&body),"self sphere");Check(KernelRuntime.PlaneCreate(&planar,&plane),"self plane");
    }
    else{Check(PK_BODY_create_solid_sphere(fixture.Radius,&sphereDefinition,&body),"PK sphere");Check(PK_PLANE_create(&planeDefinition,&plane),"PK plane");}
    var sourceFace=Faces(managed,body)[0];var options=new PK_BODY_section_o_t{fence=fence};int[] front,back,frontCaps,backCaps;
    if(managed)
    {
        var mo=Unsafe.BitCast<PK_BODY_section_o_t,M.PK_BODY_section_o_s>(options);M.PK_section_r_s result=default;
        Check(KernelRuntime.BodySectionWithSurf(body,plane,&mo,&result),"self section");
        try{front=new ReadOnlySpan<int>(result.front_bodies.array,result.front_bodies.length).ToArray();back=new ReadOnlySpan<int>(result.back_bodies.array,result.back_bodies.length).ToArray();frontCaps=new ReadOnlySpan<int>(result.front_faces.array,result.front_faces.length).ToArray();backCaps=new ReadOnlySpan<int>(result.back_faces.array,result.back_faces.length).ToArray();}
        finally{Check(KernelRuntime.SectionResultFree(&result),"self section result free");}
    }
    else
    {
        PK_section_r_t result=default;Check(PK_BODY_section_with_surf(body,plane,&options,&result),"PK section");
        try{front=new ReadOnlySpan<int>(result.front_bodies.array,result.front_bodies.length).ToArray();back=new ReadOnlySpan<int>(result.back_bodies.array,result.back_bodies.length).ToArray();frontCaps=new ReadOnlySpan<int>(result.front_faces.array,result.front_faces.length).ToArray();backCaps=new ReadOnlySpan<int>(result.back_faces.array,result.back_faces.length).ToArray();}
        finally{Check(PK_section_r_f(&result),"PK section result free");}
    }
    var signature=new StringBuilder($"{front.Length}/{back.Length}/{frontCaps.Length}/{backCaps.Length}");int cls=0;
    foreach(var source in new[]{body,sourceFace,plane})signature.Append('/').Append((managed?KernelRuntime.EntityAskClass(source,&cls):PK_ENTITY_ask_class(source,&cls))==0);
    foreach(var side in new[]{front,back})foreach(var product in side)signature.Append('/').Append(product==body).Append('/').Append(Faces(managed,product).Contains(sourceFace));
    foreach(var cap in frontCaps.Concat(backCaps))
    {
        int surf=0;if(managed)Check(KernelRuntime.FaceAskSurf(cap,&surf),"self cap surface");else Check(PK_FACE_ask_surf(cap,&surf),"PK cap surface");
        Check(managed?KernelRuntime.EntityAskClass(surf,&cls):PK_ENTITY_ask_class(surf,&cls),"cap surface class");Require(cls==PK_CLASS_plane,"cap is not planar");
    }
    identity=signature.ToString();return front.Concat(back).ToArray();
}
static unsafe int[] Faces(bool managed,int body)
{
    int count=0;int* faces=null;Check(managed?KernelRuntime.BodyAskFaces(body,&count,&faces):PK_BODY_ask_faces(body,&count,&faces),"body faces");
    try{return new ReadOnlySpan<int>(faces,count).ToArray();}finally{Check(managed?KernelRuntime.MemoryFree(faces):PK_MEMORY_free(faces),"free body faces");}
}
static unsafe PK_VECTOR1_t PlaneReference(SectionFixture f)
{var xy=Math.Sqrt(f.Nx*f.Nx+f.Ny*f.Ny);return xy>0?new(-f.Ny/xy,f.Nx/xy,0):new(1,0,0);}
static unsafe void CheckPointClassification(int own,int expected,SectionFixture f)
{
    var points=new List<PK_VECTOR_t>();foreach(var t in new[]{0.0,-f.Radius*.5,f.Radius*.5,-f.Radius,f.Radius,f.H,-2*f.Radius,2*f.Radius})points.Add(new(f.X+t*f.Nx,f.Y+t*f.Ny,f.Z+t*f.Nz));
    if(Math.Abs(f.H)<f.Radius){var r=Math.Sqrt((f.Radius-f.H)*(f.Radius+f.H));var u=PlaneReference(f);points.Add(new(f.X+f.H*f.Nx+r*u.coord[0],f.Y+f.H*f.Ny+r*u.coord[1],f.Z+f.H*f.Nz+r*u.coord[2]));}
    foreach(var point in points)
    {
        var mp=Unsafe.BitCast<PK_VECTOR_t,M.PK_VECTOR_s>(point);int actualKind=0,expectedKind=0,actualTopology=0,expectedTopology=0;
        Check(KernelRuntime.BodyContainsVector(own,mp,&actualKind,&actualTopology),"self contains");Check(PK_BODY_contains_vector(expected,point,&expectedKind,&expectedTopology),"PK contains");
        Require(actualKind==expectedKind,$"enclosure {actualKind}/{expectedKind} at ({point.coord[0]},{point.coord[1]},{point.coord[2]})");
        int actualClass=0,expectedClass=0;Check(KernelRuntime.EntityAskClass(actualTopology,&actualClass),"self enclosure class");Check(PK_ENTITY_ask_class(expectedTopology,&expectedClass),"PK enclosure class");Require(actualClass==expectedClass,$"enclosure class {actualClass}/{expectedClass}");
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

readonly record struct SectionFixture(string Name,double Radius,double H,double X=0,double Y=0,double Z=0,double Nx=0,double Ny=0,double Nz=1);

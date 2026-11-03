#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=DisplayBridgeOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:project ../src/ProjectGmKernel.DisplayBridge/ProjectGmKernel.DisplayBridge.csproj

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ProjectGmKernel.DisplayBridge;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

// --native additionally checks the actual NativeAOT exports. No PskernelSharp
// binding is redirected: the reference SDK and our native kernel coexist.
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,".."));
if(args.Contains("--native"))
{
    var schemaDirectory=Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR");
    if(string.IsNullOrWhiteSpace(schemaDirectory))schemaDirectory=Environment.GetEnvironmentVariable("P_SCHEMA");
    if(string.IsNullOrWhiteSpace(schemaDirectory))schemaDirectory=Path.Combine("..","third_party","parasolid","schema");
    schemaDirectory=Path.GetFullPath(schemaDirectory,Path.GetDirectoryName(ScriptPath())!);
    if(!Directory.Exists(schemaDirectory))throw new DirectoryNotFoundException($"The configured XT schema directory does not exist: {schemaDirectory}");
    // Do this before ScriptHost changes CoreCLR's environment cache. NativeAOT
    // reads a separate cache and must inherit the schema path at process start.
    if(Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR")!=schemaDirectory||Environment.GetEnvironmentVariable("P_SCHEMA")!=schemaDirectory)
    {
        var start=new ProcessStartInfo("dotnet"){UseShellExecute=false};
        start.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        foreach(var argument in args)start.ArgumentList.Add(argument);
        start.Environment["PARASOLID_SCHEMA_DIR"]=schemaDirectory;start.Environment["P_SCHEMA"]=schemaDirectory;
        using var process=Process.Start(start)??throw new InvalidOperationException("Could not start the native display oracle with its schema environment.");
        process.WaitForExit();Environment.ExitCode=process.ExitCode;return;
    }
}
var output=Path.Combine(root,"temp_docs","display-bridge-oracle");Directory.CreateDirectory(output);
var report=new StringBuilder();
void Log(string text){Console.WriteLine(text);report.AppendLine(text);}
try
{
    if(!ParasolidScriptHost.TryStartSession("PK visualization and original-entity mapping",out var host,out var reason))throw new Exception(reason);
    using(host)
    unsafe
    {
        var start=new M.PK_SESSION_start_o_s {o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed start");
        try
        {
            foreach(var name in new[]{"block","cylinder","torus","hole-sheet","hole-extrusion","circle-wire","nurbs-wire"})
            {
                var body=CreateModel(name);
                using var display=ParasolidDisplayBridge.Create(body,.01);
                CheckDisplay(display,name,!name.EndsWith("wire"),true,Log);
                File.WriteAllText(Path.Combine(output,name+".x_t"),display.XtText);
                if(name is "hole-extrusion" or "circle-wire")WriteViewerData(display,Path.Combine(output,name+".json"));
                if(name=="circle-wire")
                {
                    var length=display.EdgeSegments.Sum(e=>(double)Vector3.Distance(e.A,e.B));
                    var maxDeviation=display.EdgeSegments.Max(e=>Math.Abs(2-((e.A+e.B)*.5f).Length()));
                    Log($"Circle display: length={length:R}, maximum chord midpoint deviation={maxDeviation:R}.");
                    Require(Math.Abs(length-4*Math.PI)<.1&&maxDeviation<=.011,"PK circle display must cover the entire curve within requested chord tolerance");
                }
                var sample=display.Entities.First();
                Check(KernelRuntime.EntityDelete(1,&body),"delete displayed source");
                Require(!display.TryResolveParasolidTag(sample.ParasolidTag,out _),"deleted source must invalidate pick");
                Log("PASS "+name+" stale-source rejection");
            }
            CheckCopyTransform(Log);
            var restartedBody=CreateModel("block");using var stale=ParasolidDisplayBridge.Create(restartedBody,.01);
            var oldEntity=stale.Entities.First();
            Check(KernelRuntime.SessionStop(),"stop for stale-session check");Check(KernelRuntime.SessionStart(&start),"restart");
            _=CreateModel("block");
            Require(!stale.TryResolveParasolidTag(oldEntity.ParasolidTag,out _),"session restart must invalidate old identities");
            Log("PASS session-generation rejection");
        }
        finally {Check(KernelRuntime.SessionStop(),"managed stop");}
        if(args.Contains("--native"))CheckNative(root,Log);
    }
    Log("PASS display bridge: PK tessellation, PK line approximation, exact identifiers, face/edge picking, lifecycle.");
}
catch(Exception exception){Log("FAIL: "+exception);Environment.ExitCode=1;}
finally {File.WriteAllText(Path.Combine(output,"report.txt"),report.ToString());}

static string ScriptPath([CallerFilePath]string path="")=>path;
static void Check(int error,string label){if(error!=0)throw new Exception($"{label}: {error}");}
static void Require(bool condition,string label){if(!condition)throw new Exception(label);}

static unsafe int CreateModel(string name)
{
    int body=0;
    switch(name)
    {
        case "block":Check(KernelRuntime.BodyCreateSolidBlock(2,3,4,null,&body),"create block");break;
        case "cylinder":Check(KernelRuntime.BodyCreateSolidCyl(2,3,null,&body),"create cylinder");break;
        case "torus":Check(KernelRuntime.BodyCreateSolidTorus(3,1,null,&body),"create torus");break;
        case "hole-sheet":case "hole-extrusion":
            var points=stackalloc M.PK_VECTOR_s[8];
            double* coordinates=stackalloc double[16]{-3,-2,3,-2,3,2,-3,2,-1,-.5,1,-.5,1,.5,-1,.5};
            for(var i=0;i<8;i++){points[i].coord[0]=coordinates[2*i];points[i].coord[1]=coordinates[2*i+1];}
            int* loops=stackalloc int[8]{0,0,0,0,1,1,1,1};
            var sheet=new M.PK_BODY_create_sheet_planar_o_s{o_t_version=1,loops=loops};
            sheet.plane.axis.coord[2]=1;sheet.basis_set.axis.coord[2]=1;sheet.basis_set.ref_direction.coord[0]=1;
            Check(KernelRuntime.BodyCreateSheetPlanar(8,points,&sheet,&body),"create holed sheet");
            if(name=="hole-extrusion")
            {
                var options=new M.PK_BODY_extrude_o_s{o_t_version=7};
                options.start_bound.bound=options.end_bound.bound=M.ParasolidConstants.PK_bound_distance_c;
                options.start_bound.forward=options.end_bound.forward=1;options.end_bound.distance=2;
                var direction=new M.PK_VECTOR_s();direction.coord[0]=.6;direction.coord[2]=.8;
                int solid=0;var track=new M.PK_TOPOL_track_r_s();var local=new M.PK_TOPOL_local_r_s();
                Check(KernelRuntime.BodyExtrude(body,direction,&options,&solid,&track,&local),"extrude holed sheet");
                Check(KernelRuntime.TopolTrackResultFree(&track),"free extrusion tracking");
                Check(KernelRuntime.TopolLocalResultFree(&local),"free extrusion local result");
                Check(KernelRuntime.EntityDelete(1,&body),"delete independent source profile");body=solid;
            }
            break;
        case "circle-wire":
            var circle=new M.PK_CIRCLE_sf_s{radius=2};circle.basis_set.axis.coord[2]=1;circle.basis_set.ref_direction.coord[0]=1;
            int curve=0;Check(KernelRuntime.CircleCreate(&circle,&curve),"create circle");
            var interval=new M.PK_INTERVAL_s();interval.value[1]=Math.Tau;
            Check(KernelRuntime.CurveMakeWireBody(curve,interval,&body),"create circle wire");break;
        default:
            double* poles=stackalloc double[12]{0,0,0,1,2,3,0,2,4,0,1,1};
            double* knots=stackalloc double[2]{0,1};int* multiplicity=stackalloc int[2]{3,3};
            var sf=new M.PK_BCURVE_sf_s{degree=2,n_vertices=3,vertex_dim=4,is_rational=1,vertex=poles,n_knots=2,knot=knots,knot_mult=multiplicity,
                form=M.ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=M.ParasolidConstants.PK_knot_bezier_ends_c,self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c};
            int spline=0;Check(KernelRuntime.BCurveCreate(&sf,&spline),"create rational curve");
            var bounds=new M.PK_INTERVAL_s();bounds.value[1]=1;Check(KernelRuntime.CurveMakeWireBody(spline,bounds,&body),"create rational wire");break;
    }
    return body;
}

static unsafe void CheckDisplay(DisplayBody display,string label,bool wantFaces,bool managed,Action<string> log)
{
    Require(display.Entities.Count>0,label+" identities");
    Require((display.Triangles.Count>0)==wantFaces,label+" face triangles");
    Require((display.EdgeSegments.Count>0)==display.Entities.Any(e=>e.Kind==DisplayEntityKind.Edge),label+" edge polylines");
    foreach(var entity in display.Entities)
    {
        int identifier=0;Check(PK_ENTITY_ask_identifier(entity.ParasolidTag,&identifier),"PK identifier");
        Require(identifier==entity.Identifier,label+" persistent identifier");
        Require(display.TryResolveParasolidTag(entity.ParasolidTag,out var picked)&&picked==entity,label+" live PK pick mapping");
        if(managed)
        {
            int type=0;Check(KernelRuntime.EntityAskClass(entity.EntityTag,&type),"source class");
            Require(type==(entity.Kind==DisplayEntityKind.Face?M.ParasolidConstants.PK_CLASS_face:entity.Kind==DisplayEntityKind.Edge?M.ParasolidConstants.PK_CLASS_edge:M.ParasolidConstants.PK_CLASS_vertex),label+" source class");
        }
    }
    // Exercise the same cell-index mapping PKToy's renderer uses.
    var faceBuffer=NativeMemory.Alloc((nuint)Math.Max(1,display.Faces.OutPutSize));
    var edgeBuffer=NativeMemory.Alloc((nuint)Math.Max(1,display.Edges.OutPutSize));
    try
    {
        display.Faces.Update((nint)faceBuffer);display.Edges.Update((nint)edgeBuffer);
        foreach(var entry in display.Faces.IndexTagMap)
            Require(display.TryResolveViewerCell(DisplayEntityKind.Face,entry.Key,out var entity)&&entity.EntityTag==entry.Value,label+" face cell source identity");
        foreach(var entry in display.Edges.IndexTagMap)
            Require(display.TryResolveViewerCell(DisplayEntityKind.Edge,entry.Key,out var entity)&&entity.EntityTag==entry.Value,label+" edge cell source identity");
    }
    finally {NativeMemory.Free(faceBuffer);NativeMemory.Free(edgeBuffer);}
    if(wantFaces)
    {
        var checkedFaces=new HashSet<int>();
        foreach(var triangle in display.Triangles)
        {
            if(checkedFaces.Contains(triangle.Face.EntityTag))continue;
            var normal=Vector3.Cross(triangle.B-triangle.A,triangle.C-triangle.A);if(normal.LengthSquared()<1e-12)continue;
            normal=Vector3.Normalize(normal);var center=(triangle.A+triangle.B+triangle.C)/3;
            Require(display.TryPickRay(center+.001f*normal,-normal,0,out var pick)&&pick.Entity==triangle.Face,label+" face ray source identity");
            checkedFaces.Add(triangle.Face.EntityTag);
        }
        Require(checkedFaces.Count==display.Faces.TagFaces.Count,label+" ray coverage for every meshed face");
    }
    else
    {
        var segment=display.EdgeSegments[display.EdgeSegments.Count/2];var tangent=Vector3.Normalize(segment.B-segment.A);
        var normal=Vector3.Cross(tangent,Vector3.UnitZ);if(normal.LengthSquared()<.01)normal=Vector3.Cross(tangent,Vector3.UnitY);
        normal=Vector3.Normalize(normal);var center=(segment.A+segment.B)*.5f;
        Require(display.TryPickRay(center+normal,-normal,.001,out var pick)&&pick.Entity==segment.Edge,label+" edge ray source identity");
    }
    Require(!display.TryPickRay(new(float.NaN,0,0),Vector3.UnitZ,.01,out _),label+" invalid ray rejection");
    log($"PASS {label}: entities={display.Entities.Count}, triangles={display.Triangles.Count}, PK line segments={display.EdgeSegments.Count}.");
}

static unsafe void CheckCopyTransform(Action<string> log)
{
    var source=CreateModel("block");int copy=0;Check(KernelRuntime.EntityCopy(source,&copy),"public body copy");
    var sf=new M.PK_TRANSF_sf_s();sf.matrix[0]=sf.matrix[5]=sf.matrix[10]=sf.matrix[15]=1;sf.matrix[3]=7;
    int transform=0;Check(KernelRuntime.TransfCreate(&sf,&transform),"translation create");
    int count=0;int* replaced=null;byte* exact=null;
    Check(KernelRuntime.BodyTransform(copy,transform,0,&count,&replaced,&exact),"copy transform");
    if(replaced!=null)Check(KernelRuntime.MemoryFree(replaced),"free transform results");if(exact!=null)Check(KernelRuntime.MemoryFree(exact),"free exact flags");
    using(var display=ParasolidDisplayBridge.Create(copy,.01))
    {
        CheckDisplay(display,"copied-transformed-block",true,true,log);
        Require(display.Triangles.Min(t=>Math.Min(t.A.X,Math.Min(t.B.X,t.C.X)))>5,"display uses changed geometry");
    }
    Require(KernelRuntime.IsValidTag(copy)&&KernelRuntime.IsValidTag(source),"disposing display must preserve kernel model");
    Check(KernelRuntime.EntityDelete(1,&copy),"delete copy");Check(KernelRuntime.EntityDelete(1,&source),"delete source");
    log("PASS dispose preserves source and copies");
}

static unsafe void CheckNative(string root,Action<string> log)
{
    var rid=RuntimeInformation.RuntimeIdentifier;
    var file=OperatingSystem.IsWindows()?"ProjectGmKernel.Native.dll":OperatingSystem.IsMacOS()?"ProjectGmKernel.Native.dylib":"ProjectGmKernel.Native.so";
    var path=Path.Combine(root,"src","ProjectGmKernel.Native","bin","Release","net10.0",rid,"publish",file);
    var library=NativeLibrary.Load(path);
    try
    {
        var start=(delegate* unmanaged<M.PK_SESSION_start_o_s*,int>)NativeLibrary.GetExport(library,"PK_SESSION_start");
        var stop=(delegate* unmanaged<int>)NativeLibrary.GetExport(library,"PK_SESSION_stop");
        var create=(delegate* unmanaged<double,double,double,M.PK_AXIS2_sf_s*,int*,int>)NativeLibrary.GetExport(library,"PK_BODY_create_solid_block");
        var delete=(delegate* unmanaged<int,int*,int>)NativeLibrary.GetExport(library,"PK_ENTITY_delete");
        var capture=(delegate* unmanaged<int,PgmDisplaySnapshot*,int>)NativeLibrary.GetExport(library,"PGM_BODY_export_display");
        var free=(delegate* unmanaged<PgmDisplaySnapshot*,int>)NativeLibrary.GetExport(library,"PGM_DISPLAY_snapshot_free");
        var options=new M.PK_SESSION_start_o_s{o_t_version=1};Check(start(&options),"NativeAOT start");
        try
        {
            int body=0;Check(create(1,2,3,null,&body),"NativeAOT create block");
            Require(sizeof(PgmDisplayBinding)==16&&sizeof(PgmDisplaySnapshot)==48,"display ABI structure sizes");
            PgmDisplaySnapshot snapshot=default;Check(capture(body,&snapshot),"C ABI display snapshot");
            Require(snapshot.BindingCount==26&&snapshot.Bindings!=null&&snapshot.Xt.n_bytes>0,"C ABI snapshot payload and identities");
            Check(free(&snapshot),"C ABI snapshot free");Require(snapshot.Bindings==null&&snapshot.Xt.bytes==null,"free zeroes snapshot");
            Check(free(&snapshot),"empty snapshot free");
            using var display=ParasolidDisplayBridge.CreateFromNativeLibrary(path,body,.01);
            CheckDisplay(display,"NativeAOT public body",true,false,log);var entity=display.Entities.First();
            Check(delete(1,&body),"NativeAOT delete source");Require(!display.TryResolveParasolidTag(entity.ParasolidTag,out _),"NativeAOT stale pick rejection");
            CheckNativeModeling(library,path,log);
        }
        finally {Check(stop(),"NativeAOT stop");}
    }
    finally {NativeLibrary.Free(library);}
    log("PASS NativeAOT C export/display/pick/free contract");
}

static unsafe void CheckNativeModeling(nint library,string path,Action<string> log)
{
    var sheet=(delegate* unmanaged<int,M.PK_VECTOR_s*,M.PK_BODY_create_sheet_planar_o_s*,int*,int>)NativeLibrary.GetExport(library,"PK_BODY_create_sheet_planar");
    var extrude=(delegate* unmanaged<int,M.PK_VECTOR_s,M.PK_BODY_extrude_o_s*,int*,M.PK_TOPOL_track_r_s*,M.PK_TOPOL_local_r_s*,int>)NativeLibrary.GetExport(library,"PK_BODY_extrude");
    var spin=(delegate* unmanaged<int,M.PK_AXIS1_sf_s*,double,byte,int*,int**,int**,int*,int>)NativeLibrary.GetExport(library,"PK_BODY_spin");
    var copy=(delegate* unmanaged<int,int*,int>)NativeLibrary.GetExport(library,"PK_ENTITY_copy");
    var createTransform=(delegate* unmanaged<M.PK_TRANSF_sf_s*,int*,int>)NativeLibrary.GetExport(library,"PK_TRANSF_create");
    var transform=(delegate* unmanaged<int,int,double,int*,int**,byte**,int>)NativeLibrary.GetExport(library,"PK_BODY_transform");
    var delete=(delegate* unmanaged<int,int*,int>)NativeLibrary.GetExport(library,"PK_ENTITY_delete");
    var free=(delegate* unmanaged<void*,int>)NativeLibrary.GetExport(library,"PK_MEMORY_free");
    var freeTracking=(delegate* unmanaged<M.PK_TOPOL_track_r_s*,int>)NativeLibrary.GetExport(library,"PK_TOPOL_track_r_f");
    var freeLocal=(delegate* unmanaged<M.PK_TOPOL_local_r_s*,int>)NativeLibrary.GetExport(library,"PK_TOPOL_local_r_f");

    var points=stackalloc M.PK_VECTOR_s[8];
    double* coordinates=stackalloc double[16]{2,-2,8,-2,8,2,2,2,4,-.5,6,-.5,6,.5,4,.5};
    for(var i=0;i<8;i++){points[i].coord[0]=coordinates[2*i];points[i].coord[1]=coordinates[2*i+1];}
    int* loops=stackalloc int[8]{0,0,0,0,1,1,1,1};
    var sheetOptions=new M.PK_BODY_create_sheet_planar_o_s{o_t_version=1,loops=loops};
    sheetOptions.plane.axis.coord[2]=1;sheetOptions.basis_set.axis.coord[2]=1;sheetOptions.basis_set.ref_direction.coord[0]=1;
    int profile=0,solid=0,duplicate=0,matrix=0;
    try
    {
        Check(sheet(8,points,&sheetOptions,&profile),"C ABI holed sheet");
        var options=new M.PK_BODY_extrude_o_s{o_t_version=7};
        options.start_bound.bound=options.end_bound.bound=M.ParasolidConstants.PK_bound_distance_c;
        options.start_bound.forward=options.end_bound.forward=1;options.end_bound.distance=2;
        var direction=new M.PK_VECTOR_s();direction.coord[0]=.6;direction.coord[2]=.8;
        var tracking=new M.PK_TOPOL_track_r_s();var local=new M.PK_TOPOL_local_r_s();
        Check(extrude(profile,direction,&options,&solid,&tracking,&local),"C ABI holed oblique extrusion");
        Require(tracking.n_track_records==10&&local.status==M.ParasolidConstants.PK_local_status_ok_c,"C ABI extrusion result layout");
        Check(freeTracking(&tracking),"C ABI tracking release");Check(freeTracking(&tracking),"C ABI zero tracking release");
        Check(freeLocal(&local),"C ABI local result release");
        Check(copy(solid,&duplicate),"C ABI modeled body deep copy");
        Check(delete(1,&solid),"C ABI delete source solid");solid=0;
        var sf=new M.PK_TRANSF_sf_s();sf.matrix[0]=sf.matrix[5]=sf.matrix[10]=sf.matrix[15]=1;sf.matrix[3]=20;
        Check(createTransform(&sf,&matrix),"C ABI transform create");
        int count=0;int* replaced=null;byte* exact=null;
        Check(transform(duplicate,matrix,0,&count,&replaced,&exact),"C ABI modeled copy transform");
        if(replaced!=null)Check(free(replaced),"C ABI replacement release");if(exact!=null)Check(free(exact),"C ABI exact flags release");
        using(var display=ParasolidDisplayBridge.CreateFromNativeLibrary(path,duplicate,.02))
        {
            CheckDisplay(display,"NativeAOT copied holed extrusion",true,false,log);
            Require(display.Entities.Count==50&&display.Triangles.All(t=>t.A.X>=22&&t.B.X>=22&&t.C.X>=22),"NativeAOT copied model geometry and identifiers");
            CheckReceivedBody(display.ParasolidBodyTag);
        }
        var axis=new M.PK_AXIS1_sf_s();axis.axis.coord[1]=1;
        int lateralCount=0,check=0;int* laterals=null;int* bases=null;
        Check(spin(profile,&axis,Math.Tau,1,&lateralCount,&laterals,&bases,&check),"C ABI holed full revolution");
        Require(lateralCount==8,"C ABI revolution result layout");
        if(laterals!=null)Check(free(laterals),"C ABI lateral list release");if(bases!=null)Check(free(bases),"C ABI base list release");
        using(var display=ParasolidDisplayBridge.CreateFromNativeLibrary(path,profile,.02))
        {
            CheckDisplay(display,"NativeAOT holed full revolution",true,false,log);
            CheckReceivedBody(display.ParasolidBodyTag);
        }
    }
    finally
    {
        if(profile!=0)Check(delete(1,&profile),"C ABI profile/result delete");
        if(solid!=0)Check(delete(1,&solid),"C ABI solid delete");
        if(duplicate!=0)Check(delete(1,&duplicate),"C ABI copy delete");
        if(matrix!=0)Check(delete(1,&matrix),"C ABI transform delete");
    }
    log("PASS NativeAOT public holed extrusion/revolution, deep copy, transform, PK BODY_check, display and original-entity picks");
}

static unsafe void CheckReceivedBody(int body)
{
    var options=new PK_BODY_check_o_t();int count=0;PK_check_fault_t* faults=null;
    Check(PK_BODY_check(body,&options,&count,&faults),"full PK check of NativeAOT model");
    try {Require(count==0,$"NativeAOT model has {count} real-PK BODY_check faults");}
    finally {if(faults!=null)Check(PK_MEMORY_free(faults),"free PK body check faults");}
}

static void WriteViewerData(DisplayBody display,string path)
{
    using var stream=File.Create(path);using var writer=new System.Text.Json.Utf8JsonWriter(stream);
    static void Vec(System.Text.Json.Utf8JsonWriter w,string name,Vector3 p)
    {w.WriteStartArray(name);w.WriteNumberValue(p.X);w.WriteNumberValue(p.Y);w.WriteNumberValue(p.Z);w.WriteEndArray();}
    writer.WriteStartObject();writer.WriteNumber("body",display.BodyTag);writer.WriteStartArray("triangles");
    foreach(var t in display.Triangles)
    {writer.WriteStartObject();writer.WriteNumber("face",t.Face.EntityTag);Vec(writer,"a",t.A);Vec(writer,"b",t.B);Vec(writer,"c",t.C);writer.WriteEndObject();}
    writer.WriteEndArray();writer.WriteStartArray("edges");
    foreach(var e in display.EdgeSegments)
    {writer.WriteStartObject();writer.WriteNumber("edge",e.Edge.EntityTag);Vec(writer,"a",e.A);Vec(writer,"b",e.B);writer.WriteEndObject();}
    writer.WriteEndArray();writer.WriteEndObject();
}

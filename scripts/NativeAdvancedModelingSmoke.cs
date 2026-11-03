#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static parasolid;

// ABI test only: function pointers target our NativeAOT library explicitly.
// The ordinary PskernelSharp bindings continue to target the independent SDK.
var scriptDirectory=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath(Path.Combine(scriptDirectory,".."));
var schema=Environment.GetEnvironmentVariable("P_SCHEMA")??Path.Combine("..","third_party","parasolid","schema");
schema=Path.GetFullPath(schema,scriptDirectory);
if(Environment.GetEnvironmentVariable("P_SCHEMA")!=schema||Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR")!=schema)
{
    var start=new ProcessStartInfo("dotnet"){UseShellExecute=false};start.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
    start.Environment["P_SCHEMA"]=schema;start.Environment["PARASOLID_SCHEMA_DIR"]=schema;
    using var process=Process.Start(start)??throw new Exception("Could not start native ABI check");process.WaitForExit();return process.ExitCode;
}
var name=OperatingSystem.IsWindows()?"ProjectGmKernel.Native.dll":OperatingSystem.IsMacOS()?"ProjectGmKernel.Native.dylib":"ProjectGmKernel.Native.so";
var path=Path.Combine(root,"src","ProjectGmKernel.Native","bin","Release","net10.0",RuntimeInformation.RuntimeIdentifier,"publish",name);
if(!ParasolidScriptHost.TryStartSession("Native advanced modeling ABI",out var host,out var reason))throw new Exception(reason);
using(host)
{
    var library=NativeLibrary.Load(path);
    try { unsafe { Run(library); } }
    finally { NativeLibrary.Free(library); }
}
Console.WriteLine("PASS native C exports: Boolean, section, imprint, spline/sweep, offset, hollow, fillet and chamfer; independent PK receive and full BODY_check.");
return 0;

static unsafe void Run(nint library)
{
    var start=(delegate* unmanaged[Cdecl]<PK_SESSION_start_o_t*,int>)NativeLibrary.GetExport(library,"PK_SESSION_start");
    var stop=(delegate* unmanaged[Cdecl]<int>)NativeLibrary.GetExport(library,"PK_SESSION_stop");
    var block=(delegate* unmanaged[Cdecl]<double,double,double,PK_AXIS2_sf_t*,int*,int>)NativeLibrary.GetExport(library,"PK_BODY_create_solid_block");
    var planeCreate=(delegate* unmanaged[Cdecl]<PK_PLANE_sf_t*,int*,int>)NativeLibrary.GetExport(library,"PK_PLANE_create");
    var boolean=(delegate* unmanaged[Cdecl]<int,int,int*,PK_BODY_boolean_o_t*,PK_TOPOL_track_r_t*,PK_boolean_r_t*,int>)NativeLibrary.GetExport(library,"PK_BODY_boolean_2");
    var booleanFree=(delegate* unmanaged[Cdecl]<PK_boolean_r_t*,int>)NativeLibrary.GetExport(library,"PK_boolean_r_f");
    var trackFree=(delegate* unmanaged[Cdecl]<PK_TOPOL_track_r_t*,int>)NativeLibrary.GetExport(library,"PK_TOPOL_track_r_f");
    var section=(delegate* unmanaged[Cdecl]<int,int,PK_BODY_section_o_t*,PK_section_r_t*,int>)NativeLibrary.GetExport(library,"PK_BODY_section_with_surf");
    var sectionFree=(delegate* unmanaged[Cdecl]<PK_section_r_t*,int>)NativeLibrary.GetExport(library,"PK_section_r_f");
    var imprint=(delegate* unmanaged[Cdecl]<int,int,double,int*,int**,int>)NativeLibrary.GetExport(library,"PK_BODY_imprint_plane");
    var free=(delegate* unmanaged[Cdecl]<void*,int>)NativeLibrary.GetExport(library,"PK_MEMORY_free");
    var options=new PK_SESSION_start_o_t();Check(start(&options),"native start");
    try
    {
        int a=0,b=0;var basis=new PK_AXIS2_sf_t(new(0,0,0),new(0,0,1),new(1,0,0));
        Check(block(2,2,2,&basis,&a),"native block A");basis.location.coord[0]=1;Check(block(2,2,2,&basis,&b),"native block B");
        var bo=new PK_BODY_boolean_o_t{function=PK_boolean_unite_c,tracking=1};PK_TOPOL_track_r_t tr=default;PK_boolean_r_t br=default;
        Check(boolean(a,1,&b,&bo,&tr,&br),"native Boolean");Require(br.n_bodies==1,"native Boolean body count");
        CheckBody(library,br.bodies[0],14,"native Boolean");Check(trackFree(&tr),"native track free");Check(booleanFree(&br),"native Boolean free");

        basis.location.coord[0]=0;Check(block(2,2,2,&basis,&a),"native section source");
        var pd=new PK_PLANE_sf_t(new(new(0,0,1),new(0,0,1),new(1,0,0)));int plane=0;Check(planeCreate(&pd,&plane),"native plane");
        var so=new PK_BODY_section_o_t();PK_section_r_t sr=default;Check(section(a,plane,&so,&sr),"native section");
        Require(sr.front_bodies.length==1&&sr.back_bodies.length==1&&sr.front_faces.length==1&&sr.back_faces.length==1,"native section arrays");
        CheckBody(library,sr.front_bodies.array[0],6,"native front");CheckBody(library,sr.back_bodies.array[0],6,"native back");Check(sectionFree(&sr),"native section free");
        Check(block(2,2,2,&basis,&a),"native imprint source");int count=0;int* edges=null;Check(imprint(a,plane,1e-8,&count,&edges),"native imprint");
        Require(count==4,"native imprint edges");Check(free(edges),"native imprint free");CheckBody(library,a,10,"native imprint");

        var spline=(delegate* unmanaged[Cdecl]<int,PK_VECTOR_t*,PK_BCURVE_create_spline_2_o_t*,PK_BCURVE_spline_r_t*,int>)NativeLibrary.GetExport(library,"PK_BCURVE_create_spline_2");
        var splineFree=(delegate* unmanaged[Cdecl]<PK_BCURVE_spline_r_t*,int>)NativeLibrary.GetExport(library,"PK_BCURVE_spline_r_f");
        var sweep=(delegate* unmanaged[Cdecl]<int,PK_VECTOR_t,int*,int>)NativeLibrary.GetExport(library,"PK_BCURVE_sweep");
        var uvbox=(delegate* unmanaged[Cdecl]<int,PK_UVBOX_t*,int>)NativeLibrary.GetExport(library,"PK_SURF_ask_uvbox");
        var sheet=(delegate* unmanaged[Cdecl]<int,PK_UVBOX_t,int*,int>)NativeLibrary.GetExport(library,"PK_SURF_make_sheet_body");
        PK_VECTOR_t* samples=stackalloc PK_VECTOR_t[4]{new(0,0,0),new(1,.5,0),new(2,-.3,0),new(3,0,0)};
        var co=new PK_BCURVE_create_spline_2_o_t();PK_BCURVE_spline_r_t cr=default;
        Check(spline(4,samples,&co,&cr),"native cubic interpolation");Require(cr.n_bcurves==1,"native spline result");int surface=0;
        Check(sweep(cr.bcurves[0].bcurve,new(0,0,2),&surface),"native rational surface sweep");Check(splineFree(&cr),"native spline free");
        PK_UVBOX_t box=default;Check(uvbox(surface,&box),"native surface domain");Check(sheet(surface,box,&a),"native bounded NURBS sheet");CheckBody(library,a,1,"native NURBS sweep");

        var offset=(delegate* unmanaged[Cdecl]<int,double,double,byte,int>)NativeLibrary.GetExport(library,"PK_BODY_offset");
        var hollow=(delegate* unmanaged[Cdecl]<int,double,double,byte,int*,int**,int**,int>)NativeLibrary.GetExport(library,"PK_BODY_hollow");
        Check(block(2,2,2,&basis,&a),"native offset source");Check(offset(a,.2,1e-7,1),"native offset");CheckBody(library,a,6,"native offset");
        int* originals=null;int* products=null;Check(hollow(a,-.15,1e-7,1,&count,&originals,&products),"native hollow");
        Require(count==6,"native hollow generated faces");Check(free(originals),"native hollow originals free");Check(free(products),"native hollow products free");CheckBody(library,a,12,"native hollow");

        var askEdges=(delegate* unmanaged[Cdecl]<int,int*,int**,int>)NativeLibrary.GetExport(library,"PK_BODY_ask_edges");
        var blend=(delegate* unmanaged[Cdecl]<int,int*,double,PK_EDGE_set_blend_constant_o_t*,int*,int**,int>)NativeLibrary.GetExport(library,"PK_EDGE_set_blend_constant");
        var chamfer=(delegate* unmanaged[Cdecl]<int,int*,double,double,int*,PK_EDGE_set_blend_chamfer_o_t*,int*,int**,int>)NativeLibrary.GetExport(library,"PK_EDGE_set_blend_chamfer");
        var fix=(delegate* unmanaged[Cdecl]<int,PK_BODY_fix_blends_o_t*,int*,int**,PK_FACE_array_t**,int**,int*,int*,int*,int>)NativeLibrary.GetExport(library,"PK_BODY_fix_blends");
        for(var mode=0;mode<2;mode++)
        {
            Check(block(2,2,2,&basis,&a),"native blend source");Check(askEdges(a,&count,&edges),"native blend candidate");
            var first=edges[0];Check(free(edges),"native candidates free");int* selected=null;
            if(mode==0){var bo2=new PK_EDGE_set_blend_constant_o_t();Check(blend(1,&first,.1,&bo2,&count,&selected),"native set fillet");}
            else{var cho=new PK_EDGE_set_blend_chamfer_o_t();Check(chamfer(1,&first,.1,.1,null,&cho,&count,&selected),"native set chamfer");}
            Require(count==1,"native blend selection");Check(free(selected),"native selection free");
            var fo=new PK_BODY_fix_blends_o_t();int* blends=null;PK_FACE_array_t* unders=null;int* topols=null;int fault=0,fe=0,ft=0;
            Check(fix(a,&fo,&count,&blends,&unders,&topols,&fault,&fe,&ft),"native fix blends");Require(count==1&&fault==PK_blend_fault_no_fault_c,"native blend result");
            for(var i=0;i<count;i++)Check(free(unders[i].array),"native underlying faces free");
            Check(free(blends),"native blends free");Check(free(unders),"native unders free");Check(free(topols),"native blend topols free");
            CheckBody(library,a,7,mode==0?"native fillet":"native chamfer");
        }
    }
    finally { Check(stop(),"native stop"); }
}
static unsafe void CheckBody(nint library,int body,int expectedFaces,string label)
{
    var transmit=(delegate* unmanaged[Cdecl]<int,int*,PK_PART_transmit_o_t*,PK_MEMORY_block_t*,int>)NativeLibrary.GetExport(library,"PK_PART_transmit_b");
    var free=(delegate* unmanaged[Cdecl]<PK_MEMORY_block_t*,int>)NativeLibrary.GetExport(library,"PK_MEMORY_block_f");
    var o=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block=default;
    Check(transmit(1,&body,&o,&block),label+" native XT");
    try
    {
        var receive=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n=0;int* parts=null;
        Check(PK_PART_receive_b(block,&receive,&n,&parts),label+" independent PK receive");Require(n==1,label+" received body count");
        try
        {
            var check=new PK_BODY_check_o_t{max_faults=100};int faults=0;PK_check_fault_t* found=null;
            Check(PK_BODY_check(parts[0],&check,&faults,&found),label+" full check");
            try { Require(faults==0,label+" independent PK geometry/topology faults="+faults); }
            finally { PK_MEMORY_free(found); }
            int* faces=null;Check(PK_BODY_ask_faces(parts[0],&n,&faces),label+" face count");PK_MEMORY_free(faces);Require(n==expectedFaces,label+" expected face count");
        }
        finally { PK_MEMORY_free(parts); }
    }
    finally { Check(free(&block),label+" native memory free"); }
    Console.WriteLine("PASS "+label);
}
static void Check(int error,string operation){if(error!=0)throw new Exception(operation+": "+error);}
static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
static string ScriptPath([CallerFilePath]string path="")=>path;

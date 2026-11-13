#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=CurvedSheetPartitionOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
// Applicability probe only. Neither API success nor its geometry class
// qualifies a complete sheet/body consumer.
Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
var r0=args.Length>0?double.Parse(args[0],System.Globalization.CultureInfo.InvariantCulture):.2;
var r1=args.Length>1?double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture):r0;
if(!ParasolidScriptHost.TryStartSession("Native closed explicit Blend applicability",out var host,out var reason))throw new Exception(reason);
using(host)unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version));Console.WriteLine($"PK_VERSION {version.major_revision}.{version.minor_revision}.{version.build_number}");
    var cylinder=new PK_CYL_sf_t{radius=1};cylinder.basis_set.axis=new(0,0,1);cylinder.basis_set.ref_direction=new(1,0,0);
    var plane=new PK_PLANE_sf_t();plane.basis_set.axis=new(0,.6,.8);plane.basis_set.ref_direction=new(1,0,0);
    int a,b,blend;Check(PK_CYL_create(&cylinder,&a));Check(PK_PLANE_create(&plane,&b));
    var options=new PK_SURF_create_blend_o_t();var error=PK_SURF_create_blend(a,r0,b,r1,new(1.2,0,.25),new(0,1.2,-.65),&options,&blend);
    Console.WriteLine($"CREATE error={error} entity={blend}");Check(error);int cls;Check(PK_ENTITY_ask_class(blend,&cls));Console.WriteLine("CLASS "+cls);
    if(cls==PK_CLASS_blendsf)
    {
        PK_BLENDSF_sf_t definition;Check(PK_BLENDSF_ask(blend,&definition));Check(PK_ENTITY_ask_class(definition.spine,&cls));
        PK_UVBOX_t box;Check(PK_SURF_ask_uvbox(blend,&box));
        Console.WriteLine($"SPINE class={cls} extent={definition.spine_extent.value[0]:R}/{definition.spine_extent.value[1]:R} uv={box.param[0]:R}/{box.param[1]:R}/{box.param[2]:R}/{box.param[3]:R}");
    }
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start));
    try
    {
        int ownCylinder,ownPlane,ownBlend;
        Check(KernelRuntime.CylCreate((M.PK_CYL_sf_s*)&cylinder,&ownCylinder));Check(KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&plane,&ownPlane));
        var first=new PK_VECTOR_t(1.2,0,.25);var last=new PK_VECTOR_t(0,1.2,-.65);
        var ownError=KernelRuntime.SurfCreateBlend(ownCylinder,r0,ownPlane,r1,*(M.PK_VECTOR_s*)&first,*(M.PK_VECTOR_s*)&last,(M.PK_SURF_create_blend_o_s*)&options,&ownBlend);
        Console.WriteLine($"GM_CREATE error={ownError} entity={ownBlend} geometry_topology_qualified=False");
        if(ownError!=0)Environment.ExitCode=1;
        if(ownError==0)foreach(var v in new[]{0,.15,.5,.85,1})
        {
            var uv=new PK_UV_t();uv.param[0]=.73;uv.param[1]=v;var native=new PK_VECTOR_t();var actual=new M.PK_VECTOR_s();
            Check(PK_SURF_eval(blend,uv,0,0,PK_LOGICAL_false,&native));Check(KernelRuntime.SurfEval(ownBlend,*(M.PK_UV_s*)&uv,0,0,0,&actual));
            Console.WriteLine($"SECTION radii={r0:R}/{r1:R} u=.73 v={v:R} native={native.coord[0]:R}/{native.coord[1]:R}/{native.coord[2]:R} gm={actual.coord[0]:R}/{actual.coord[1]:R}/{actual.coord[2]:R}");
        }
    }
    finally{Check(KernelRuntime.SessionStop());}
}
static void Check(int error){if(error!=0)throw new Exception("PK error="+error);}

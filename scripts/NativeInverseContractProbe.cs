#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property AssemblyName=GeometryPublicApiOracle
global using KnotMultiplicity=System.Int32;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

// Same input geometry and vector, including deliberately off-surface input.
// This characterizes inverse-query status; it is not body qualification.
if(!ParasolidScriptHost.TryStartSession("Native surface inverse contract",out var host,out var message))throw new Exception(message);
var nativeOnly=args.Contains("--native-only",StringComparer.Ordinal);
Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
using(host)unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version));
    Console.WriteLine($"PK_VERSION {version.major_revision}.{version.minor_revision}.{version.build_number}");
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start));
    try
    {
        double* poles=stackalloc double[45];double* knots=stackalloc double[2]{0,1};KnotMultiplicity* um=stackalloc KnotMultiplicity[2]{5,5};KnotMultiplicity* vm=stackalloc KnotMultiplicity[2]{3,3};
        for(var shape=0;shape<2;shape++)
        {
            for(var i=0;i<5;i++)for(var j=0;j<3;j++)
            {
                var at=3*(3*i+j);poles[at]=i*.25;poles[at+1]=j*.5;poles[at+2]=1+(i==2?(shape==0?-.2:.4):0);
            }
            var sf=new M.PK_BSURF_sf_s{u_degree=4,v_degree=2,n_u_vertices=5,n_v_vertices=3,vertex_dim=3,vertex=poles,
                n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,
                u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,form=PK_BSURF_form_arbitrary_c,
                self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
            PK_SURF_t own,native;Check(KernelRuntime.BSurfCreate(&sf,&own));Check(PK_BSURF_create((PK_BSURF_sf_t*)&sf,&native));
            var uv=new PK_UV_t();uv.param[0]=.37;uv.param[1]=.63;PK_VECTOR_t point;
            Check(PK_SURF_eval(native,uv,0,0,PK_LOGICAL_false,&point));
            foreach(var shift in new[]{0.0,.15,-.15,.01,-.01,1e-9,-1e-9,1e-7})
            {
                var input=point;input.coord[2]+=shift;PK_UV_t expected;var actual=new M.PK_UV_s();
                var nativeError=PK_SURF_parameterise_vector(native,input,&expected);
                var ownError=nativeOnly?-1:KernelRuntime.SurfParameteriseVector(own,*(M.PK_VECTOR_s*)&input,&actual);
                var nativeGap=double.NaN;var ownGap=double.NaN;PK_VECTOR_t located;
                if(nativeError==0){Check(PK_SURF_eval(native,expected,0,0,PK_LOGICAL_false,&located));nativeGap=Gap(input,located);}
                if(ownError==0){Check(KernelRuntime.SurfEval(own,actual,0,0,0,(M.PK_VECTOR_s*)&located));ownGap=Gap(input,located);}
                Console.WriteLine($"INVERSE shape={shape} shift={shift:R} PK={nativeError} GM={ownError} PK_gap={nativeGap:R} GM_gap={ownGap:R}");
                if(!nativeOnly&&ownError!=nativeError)throw new Exception("inverse status differs from independent PK");
                if(shift==0&&(nativeError!=0||nativeGap>1e-8||!nativeOnly&&(ownError!=0||ownGap>1e-8)))throw new Exception("actual on-surface inverse failed");
            }
        }
    }
    finally{Check(KernelRuntime.SessionStop());}
}
static void Check(int error){if(error!=0)throw new Exception($"API error={error}");}
static unsafe double Gap(PK_VECTOR_t a,PK_VECTOR_t b)
{var x=a.coord[0]-b.coord[0];var y=a.coord[1]-b.coord[1];var z=a.coord[2]-b.coord[2];return Math.Sqrt(x*x+y*y+z*z);}

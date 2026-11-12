#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=CurvedSheetPartitionOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;

var name=args.Single();if(name.Length==0||name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('-' or '_')))throw new ArgumentException("new output directory name");
var output=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,"..","temp_docs","bsurface-domain-oracle",name));
if(Directory.Exists(output))throw new InvalidOperationException("Existing evidence is immutable");Directory.CreateDirectory(output);
if(!ParasolidScriptHost.TryStartSession("Explicit native B-surface domains",out var host,out var reason))throw new Exception(reason);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed session");
    try
    {
        PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"native version");Console.WriteLine($"PK_VERSION {version.major_revision}.{version.minor_revision}.{version.build_number}");
        Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
        Console.WriteLine("NATIVE_EVALUATION_ENCLOSURE evaluation-roundoff=64*2^-52*max(1,abs(point-component)); exact interval proof unchanged");
        var rows=new StringBuilder("rational,gauge,domain,u,v,native_error,managed_error,x,y,z,xlo,xhi,ylo,yhi,zlo,zhi\n");var samples=0;
        double* poles=stackalloc double[36];double* uk=stackalloc double[2];double* vk=stackalloc double[2];int* mult=stackalloc int[2]{3,3};
        PK_VECTOR_t a;M.PK_VECTOR_s b;
        foreach(var rational in new[]{false,true})foreach(var shifted in new[]{false,true})
        {
            var dimension=rational?4:3;uk[0]=shifted?2:0;uk[1]=shifted?5:1;vk[0]=shifted?-3:0;vk[1]=shifted?-1:1;
            for(var i=0;i<3;i++)for(var j=0;j<3;j++)
            {
                var w=rational?1+.25*i+.5*j:1;var at=dimension*(3*i+j);
                poles[at]=i*.5*w;poles[at+1]=j*.5*w;poles[at+2]=(i==2&&j==2?1:0)*w;if(rational)poles[at+3]=w;
            }
            var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=2,n_u_vertices=3,n_v_vertices=3,vertex_dim=dimension,is_rational=rational?PK_LOGICAL_true:PK_LOGICAL_false,vertex=poles,
                n_u_knots=2,n_v_knots=2,u_knot=uk,v_knot=vk,u_knot_mult=mult,v_knot_mult=mult,form=PK_BSURF_form_arbitrary_c,
                u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
            int native,managed;Check(PK_BSURF_create(&sf,&native),"native source");Check(KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&managed),"managed source");
            var source=KernelRuntime.GetBSurfaceView(KernelRuntime.BSurfaceDataStore[KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(managed)].DataIndex]);
            var du=uk[1]-uk[0];var dv=vk[1]-vk[0];
            var domains=new SurfaceParameterBox[]{new(uk[0],uk[1],vk[0],vk[1]),new(uk[0]+2*du,uk[0]+2*du,vk[0]+2*dv,vk[0]+2*dv),
                new(uk[0]-.25*du,uk[1]+.25*du,vk[0]-.5*dv,vk[1]+.5*dv),new(uk[0]+.25*du,uk[0]+.75*du,vk[0]+.2*dv,vk[0]+.8*dv)};
            for(var domain=0;domain<domains.Length;domain++)
            {
                Require(BSurfaceDomainBounds.Position(source,domains[domain],true,out var box)==AlgorithmStatus.Success,"complete explicit domain bound");
                for(var i=0;i<=6;i++)for(var j=0;j<=6;j++)
                {
                    var u=domains[domain].U0+(domains[domain].U1-domains[domain].U0)*i/6;var v=domains[domain].V0+(domains[domain].V1-domains[domain].V0)*j/6;
                    var uv=new PK_UV_t(u,v);var ownUv=*(M.PK_UV_s*)&uv;
                    var pe=PK_SURF_eval(native,uv,0,0,PK_LOGICAL_false,&a);var ge=KernelRuntime.SurfEval(managed,ownUv,0,0,0,&b);
                    rows.AppendLine(string.Join(',',new[]{rational.ToString(),shifted.ToString(),domain.ToString(CultureInfo.InvariantCulture),F(u),F(v),pe.ToString(CultureInfo.InvariantCulture),ge.ToString(CultureInfo.InvariantCulture),F(a.coord[0]),F(a.coord[1]),F(a.coord[2]),F(box.XLo),F(box.XHi),F(box.YLo),F(box.YHi),F(box.ZLo),F(box.ZHi)}));
                    Check(pe,"native domain evaluation");Check(ge,"managed domain evaluation");
                    Require(Enclosed(a.coord[0],box.XLo,box.XHi)&&Enclosed(a.coord[1],box.YLo,box.YHi)&&Enclosed(a.coord[2],box.ZLo,box.ZHi),
                        $"native sample enclosed rational={rational} shifted={shifted} domain={domain} uv={F(u)},{F(v)} point={F(a.coord[0])},{F(a.coord[1])},{F(a.coord[2])} box={box}");
                    for(var d=0;d<3;d++)Require(Math.Abs(a.coord[d]-b.coord[d])<=1e-11,"same native parameter evaluation");
                    if(domain==1&&!rational)Require(Math.Abs(a.coord[2]-9)<1e-11,"native tensor linear extension, not polynomial value16");samples++;
                }
                Console.WriteLine($"NATIVE_DOMAIN_PASS rational={rational} shifted={shifted} domain={domain} samples=49 continuum_proof=interval-bound-not-samples");
            }
            Check(PK_ENTITY_delete(1,&native),"delete native source");Check(KernelRuntime.EntityDelete(1,&managed),"delete managed source");
        }
        File.WriteAllText(Path.Combine(output,"samples.csv"),rows.ToString());Console.WriteLine($"PASS native domain samples={samples}");
    }
    finally{Check(KernelRuntime.SessionStop(),"stop managed session");}
}
static string F(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
static bool Enclosed(double value,double lo,double hi){var roundoff=64*Math.ScaleB(1,-52)*Math.Max(1,Math.Abs(value));return value>=lo-roundoff&&value<=hi+roundoff;}
static void Check(int error,string label){if(error!=0)throw new InvalidOperationException(label+" error="+error);}
static void Require(bool value,string label){if(!value)throw new InvalidOperationException(label);}
static string Source([CallerFilePath]string path="")=>path;

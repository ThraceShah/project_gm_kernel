#!/usr/bin/env dotnet run
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property AssemblyName=IcurveEvalBenchmark

using System.Diagnostics;
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

if(!ParasolidScriptHost.TryStartSession("blend benchmark",out var session,out var message))throw new Exception(message);
using(session)
unsafe
{
    var options=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&options),"self start");
    try
    {
        Console.WriteLine($"Runtime={RuntimeInformation.FrameworkDescription}; RID={RuntimeInformation.RuntimeIdentifier}; CPU count={Environment.ProcessorCount}");
        Console.WriteLine("Release managed public API versus native PK P/Invoke; single thread; alternating order; 31 batches; p50/p95 ns per call.");
        Console.WriteLine("Only analytically simplified constant-radius blends. This is not a procedural nested-blend benchmark.");
        foreach(var sphere in new[]{false,true})
        {
            var a=Plane(0,0,1);var b=sphere?Sphere():Plane(1,0,0);
            var m=Create(true,a.Self,b.Self,sphere);var p=Create(false,a.Pk,b.Pk,sphere);
            Validate(m,p);
            Console.WriteLine(sphere?"plane/sphere -> torus":"plane/plane -> cylinder");
            foreach(var order in new[]{0,1,2})Measure("eval D"+order,()=>Eval(true,m,order,4096),()=>Eval(false,p,order,4096),4096);
            Measure("create+delete",()=>CreateDelete(true,a.Self,b.Self,sphere,64),()=>CreateDelete(false,a.Pk,b.Pk,sphere,64),64);
        }
        ProbeAnalyticNesting();
    }
    finally{Check(KernelRuntime.SessionStop(),"self stop");}
}

static void Measure(string name,Func<double> self,Func<double> pk,int count)
{
    for(var i=0;i<8;i++){_ =self();_ =pk();}
    var ours=new double[31];var theirs=new double[31];double checksum=0;long allocated=0;
    for(var i=0;i<31;i++)for(var phase=0;phase<2;phase++)
    {
        var managed=((i+phase)&1)==0;var bytes=GC.GetAllocatedBytesForCurrentThread();var begin=Stopwatch.GetTimestamp();
        checksum+=managed?self():pk();var elapsed=Stopwatch.GetTimestamp()-begin;
        if(managed){ours[i]=elapsed*1e9/Stopwatch.Frequency/count;allocated+=GC.GetAllocatedBytesForCurrentThread()-bytes;}
        else theirs[i]=elapsed*1e9/Stopwatch.Frequency/count;
    }
    Array.Sort(ours);Array.Sort(theirs);
    if(!double.IsFinite(checksum))throw new Exception("Nonfinite timed result");
    Console.WriteLine($"  {name}: self={ours[15]:F1}/{ours[29]:F1}; PK={theirs[15]:F1}/{theirs[29]:F1}; p50 self/PK={ours[15]/theirs[15]:F2}; self managed bytes={allocated}; checksum={checksum:R}");
}

static unsafe double Eval(bool self,int surface,int order,int count,double pkUShift=0)
{
    M.PK_VECTOR_s* m=stackalloc M.PK_VECTOR_s[9];PK_VECTOR_t* p=stackalloc PK_VECTOR_t[9];var sum=0.0;var error=0;
    if(self)for(var i=0;i<count;i++)
    {
        var uv=new M.PK_UV_s();uv.param[0]=.23+(i%251)*.003;uv.param[1]=.19+(i%239)*.002;
        error|=KernelRuntime.SurfEval(surface,uv,order,order,0,m);sum+=m[0].coord[0];
    }
    else for(var i=0;i<count;i++)
    {
        var uv=new PK_UV_t(.23+(i%251)*.003+pkUShift,.19+(i%239)*.002);
        error|=PK_SURF_eval(surface,uv,order,order,PK_LOGICAL_false,p);sum+=p[0].coord[0];
    }
    Check(error,"timed eval");return sum;
}

static unsafe double CreateDelete(bool self,int a,int b,bool sphere,int count)
{
    for(var i=0;i<count;i++){var surface=Create(self,a,b,sphere);Check(self?KernelRuntime.EntityDelete(1,&surface):PK_ENTITY_delete(1,&surface),"timed delete");}
    return count;
}

static unsafe int Create(bool self,int a,int b,bool sphere)
{
    var mo=new M.PK_SURF_create_blend_o_s{o_t_version=1};var po=new PK_SURF_create_blend_o_t();
    var start=new M.PK_VECTOR_s();start.coord[0]=sphere?1:0;var end=new M.PK_VECTOR_s();end.coord[1]=1;int result=0;
    Check(self?KernelRuntime.SurfCreateBlend(a,.2,b,.2,start,end,&mo,&result)
        :PK_SURF_create_blend(a,.2,b,.2,new PK_VECTOR_t(start.coord[0],0,0),new PK_VECTOR_t(0,1,0),&po,&result),"create blend");
    return result;
}

static unsafe void ProbeAnalyticNesting()
{
    var a=Plane(0,0,1);var b=Plane(1,0,0);var wall=Plane(0,1,0);
    var first=Create(true,a.Self,b.Self,false);var reference=Create(false,a.Pk,b.Pk,false);
    var mo=new M.PK_SURF_create_blend_o_s{o_t_version=1};var po=new PK_SURF_create_blend_o_t();
    var start=new PK_VECTOR_t(.45,0,.2);var end=new PK_VECTOR_t(.2,0,.45);int m=0,p=0;
    var me=KernelRuntime.SurfCreateBlend(first,.05,wall.Self,.05,*(M.PK_VECTOR_s*)&start,*(M.PK_VECTOR_s*)&end,&mo,&m);
    var pe=PK_SURF_create_blend(reference,.05,wall.Pk,.05,start,end,&po,&p);
    Console.WriteLine($"two-stage analytic probe: self error={me}, PK error={pe}");Check(me,"second self blend");Check(pe,"second PK blend");
    var mt=new M.PK_TORUS_sf_s();PK_TORUS_sf_t pt;Check(KernelRuntime.TorusAsk(m,&mt),"second self torus");Check(PK_TORUS_ask(p,&pt),"second PK torus");
    Console.WriteLine($"  torus self/PK radii={mt.major_radius:R}/{pt.major_radius:R}, {mt.minor_radius:R}/{pt.minor_radius:R}");
    for(var d=0;d<3;d++)Console.WriteLine($"  component {d}: center={mt.basis_set.location.coord[d]:R}/{pt.basis_set.location.coord[d]:R}, axis={mt.basis_set.axis.coord[d]:R}/{pt.basis_set.axis.coord[d]:R}, ref={mt.basis_set.ref_direction.coord[d]:R}/{pt.basis_set.ref_direction.coord[d]:R}");
    // This fixture has the same torus, but PK selects a different reference
    // meridian. Preserve that public parameter discrepancy as a boundary;
    // only compare geometry after the explicit analytic phase map.
    var shifted=false;try{Validate(m,p);}catch(InvalidOperationException){shifted=true;}
    if(!shifted)throw new Exception("Update the recorded raw-parameter boundary: discrepancy no longer reproduced");
    Console.WriteLine("BOUNDARY: identical native U/V jets differ; analytic reference meridians require PK u=self u+pi/2.");
    Validate(m,p,Math.PI/2);
    Check(KernelRuntime.EntityDelete(1,&first),"delete self first blend");Check(PK_ENTITY_delete(1,&reference),"delete PK first blend");
    Validate(m,p,Math.PI/2);
    Console.WriteLine("two-stage cylinder/plane -> torus: matching class and 100 mapped D0-D2 jets; remains evaluable after first blend deletion. Raw-parameter compatibility is NOT passed.");
    var measuredSelf=m;var measuredPk=p;
    Measure("two-stage result eval D2 (mapped)",()=>Eval(true,measuredSelf,2,4096),()=>Eval(false,measuredPk,2,4096,Math.PI/2),4096);
}

static unsafe void Validate(int self,int pk,double pkUShift=0)
{
    int a=0,b=0;Check(KernelRuntime.EntityAskClass(self,&a),"self class");Check(PK_ENTITY_ask_class(pk,&b),"PK class");if(a!=b)throw new Exception("Class mismatch");
    M.PK_VECTOR_s* m=stackalloc M.PK_VECTOR_s[9];PK_VECTOR_t* p=stackalloc PK_VECTOR_t[9];
    for(var i=0;i<100;i++)
    {
        var uv=new M.PK_UV_s();uv.param[0]=.23+i*.01;uv.param[1]=.19+i*.007;
        Check(KernelRuntime.SurfEval(self,uv,2,2,0,m),"self validation");Check(PK_SURF_eval(pk,new(uv.param[0]+pkUShift,uv.param[1]),2,2,PK_LOGICAL_false,p),"PK validation");
        for(var j=0;j<9;j++)for(var d=0;d<3;d++)if(Math.Abs(m[j].coord[d]-p[j].coord[d])>1e-9)throw new InvalidOperationException("D0-D2 mismatch");
    }
}

static unsafe (int Self,int Pk) Plane(double x,double y,double z)
{
    var sf=new PK_PLANE_sf_t();sf.basis_set.axis=new(x,y,z);sf.basis_set.ref_direction=x==1?new(0,1,0):new(1,0,0);int m=0,p=0;
    Check(KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&sf,&m),"self plane");Check(PK_PLANE_create(&sf,&p),"PK plane");return(m,p);
}
static unsafe (int Self,int Pk) Sphere()
{
    var sf=new PK_SPHERE_sf_t{radius=1};sf.basis_set.location=new(0,0,1);sf.basis_set.axis=new(0,0,1);sf.basis_set.ref_direction=new(1,0,0);int m=0,p=0;
    Check(KernelRuntime.SphereCreate((M.PK_SPHERE_sf_s*)&sf,&m),"self sphere");Check(PK_SPHERE_create(&sf,&p),"PK sphere");return(m,p);
}
static void Check(int error,string name){if(error!=0)throw new Exception(name+" error="+error);}

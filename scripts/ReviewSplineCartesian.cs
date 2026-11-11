#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=KernelTests
#:property KernelTestHooks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Numerics;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static parasolid;
using V=ProjectGmKernel.Native.Runtime.KernelVector3;

CheckFixtureRounding();
if(args.Contains("--fixture-only",StringComparer.Ordinal))return;
if(!ParasolidScriptHost.TryStartSession("Cartesian independent review",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var directions=0;
    foreach(var rational in new[]{false,true})foreach(var reversed in new[]{false,true})
    foreach(var sense in new[]{-1.0,1.0})foreach(var h in new[]{-.125,0,.125})
        directions+=CheckDirections(rational,reversed,sense,h);
    Console.WriteLine($"PASS real PK jets versus G Jacobian: {directions} sampled matrices, both UV normal derivatives, rational/nonrational, support swap, sense, signed h, extension");
    foreach(var reversed in new[]{false,true})foreach(var sense in new[]{-1.0,1.0})foreach(var h in new[]{-.125,.125})
        CheckKnotTube(reversed,sense,h);
    CheckKnotTube(false,-1,.75);
    Console.WriteLine("PASS C1 knot tubes: 8 crossing boxes enclose analytic positions and one-sided velocities; focal/nearest failure retained");
    CheckOriginalLifecycle();
}

static void CheckFixtureRounding()
{
    foreach(var shear in new[]{.37,.5})
    {
        // These small doubles have exact integral representations at scale 2^54.
        // A nonzero second pole difference disproves the assumed affine x net.
        var second=new BigInteger(Math.ScaleB(1+shear,54))-2*new BigInteger(Math.ScaleB(.5+shear,54))+new BigInteger(Math.ScaleB(shear,54));
        Require((shear==.5)==second.IsZero,"pole rounding mechanism");
        Console.WriteLine($"fixture mechanism shear={shear:R} exact second X pole difference={second}*2^-54");
    }
    Console.WriteLine("PASS fixture mechanism only; failed historical fixture source is absent and is not reconstructed by this example");
}

static unsafe int CheckDirections(bool rational,bool reversed,double sense,double h)
{
    var dimension=rational?4:3;var data=new double[9*dimension+12];
    for(var i=0;i<3;i++)for(var j=0;j<3;j++)
    {
        var weight=rational&&i==1&&j==1?2:1;var at=(3*i+j)*dimension;
        data[at]=(i*.5+j*.25)*weight;data[at+1]=j*.5*weight;
        data[at+2]=((i==2?.25:0)+(j==2?.125:0)+i*j*.0625)*weight;
        if(rational)data[at+3]=weight;
    }
    double[] knots=[0,0,0,1,1,1,0,0,0,1,1,1];knots.CopyTo(data,9*dimension);
    var spline=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=sense,Offset=h,UDegree=2,VDegree=2,UCount=3,VCount=3,Dimension=dimension,Rational=rational};
    var plane=new BlendSurfaceInstruction{Analytic=new(SurfaceClass.Plane,new(){Y=.3},new(){Y=1},new(){X=1}),NormalSense=-1};
    var program=new NestedBlendProgram(reversed?new[]{spline,plane}:new[]{plane,spline},
        new[]{new BlendSpineInstruction{First=0,Second=1,ChartCount=2}},
        new[]{new BlendChartInstruction{Position=new(){Y=.3},Chord=new(){X=1,Y=.25},Scale=2.7},new BlendChartInstruction{Parameter=2.7,Scale=1}},data,new double[2048]);
    double* pkKnots=stackalloc double[2]{0,1};int* mult=stackalloc int[2]{3,3};
    fixed(double* poles=data)
    {
        var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=2,n_u_vertices=3,n_v_vertices=3,vertex_dim=dimension,vertex=poles,
            is_rational=rational?PK_LOGICAL_true:PK_LOGICAL_false,n_u_knots=2,n_v_knots=2,u_knot=pkKnots,v_knot=pkKnots,u_knot_mult=mult,v_knot_mult=mult,
            form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,
            self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
        int surface;Check(PK_BSURF_create(&sf,&surface),"PK_BSURF_create review patch");
        try
        {
            var matrices=0;
            Span<IntervalNumber> matrix=stackalloc IntervalNumber[25];
            PK_VECTOR_t* jets=stackalloc PK_VECTOR_t[9];
            Span<IntervalDual> original=stackalloc IntervalDual[5],rebound=stackalloc IntervalDual[5],g=stackalloc IntervalDual[5];
            foreach(var (u,v) in new[]{(.25,.375),(-.02,1.02)})
            {
                IntervalNumber[] box=[new(.18,.22),new(.28,.32),new(.38,.42),new(u-.001,u+.001),new(v-.001,v+.001)];
                Require(JointNestedBlendEvaluation.TestCartesianSplineMatrix(program,box,matrix,default)==AlgorithmStatus.Success,"G matrix");
                Check(PK_SURF_eval(surface,new(u,v),2,2,PK_LOGICAL_false,jets),"PK_SURF_eval review jets");
                // PK rectangular output is i+(n_u_derivs+1)*j;
                // the kernel's internal rectangular storage has the other order.
                var su=Vector(jets[1]);var sv=Vector(jets[3]);var cross=Cross(su,sv);var length=Math.Sqrt(Dot(cross,cross));var normal=Scale(cross,1/length);
                var cu=Add(Cross(Vector(jets[2]),sv),Cross(su,Vector(jets[4])));
                var cv=Add(Cross(Vector(jets[4]),sv),Cross(su,Vector(jets[6])));
                var nu=Scale(Sub(cu,Scale(normal,Dot(normal,cu))),1/length);
                var nv=Scale(Sub(cv,Scale(normal,Dot(normal,cv))),1/length);
                var du=Sub(Scale(su,-1),Scale(nu,h));var dv=Sub(Scale(sv,-1),Scale(nv,h));var row=reversed?0:1;
                double[] expected=new double[25];expected[row*5]=1;expected[3*5+1]=1;expected[4*5+2]=1;
                expected[(1-row)*5+1]=-1;expected[2*5]=1;expected[2*5+1]=.25;
                expected[row*5+3]=du.X;expected[3*5+3]=du.Y;expected[4*5+3]=du.Z;
                expected[row*5+4]=dv.X;expected[3*5+4]=dv.Y;expected[4*5+4]=dv.Z;
                for(var i=0;i<25;i++)Require(matrix[i].Lower<=expected[i]&&expected[i]<=matrix[i].Upper,$"PK derivative outside G hull: entry {i}");
                for(var column=0;column<5;column++)
                {
                    Require(JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,g,column)==AlgorithmStatus.Success,"full G direction");
                    for(var i=0;i<5;i++)Require(matrix[i*5+column].Lower==g[i].Derivative.Lower&&matrix[i*5+column].Upper==g[i].Derivative.Upper,"direct columns differ from full evaluator");
                }
                Require(Math.Abs(nv.X)+Math.Abs(nv.Y)+Math.Abs(nv.Z)>0,"fixture exercises V normal derivative");
                matrices++;
            }
            return matrices;
        }
        finally{Check(PK_ENTITY_delete(1,&surface),"delete PK patch");}
    }
}

static void CheckKnotTube(bool reversed,double sense,double h)
{
    // Exact native quadratic S=(u,v,max(u-.5,0)^2), one simple interior knot.
    var data=new double[24+7+4];double[] x=[0,.25,.75,1];
    for(var i=0;i<4;i++)for(var j=0;j<2;j++){var at=3*(2*i+j);data[at]=x[i];data[at+1]=j;data[at+2]=i==3?.25:0;}
    double[] knots=[0,0,0,.5,1,1,1,0,0,1,1];knots.CopyTo(data,24);
    var spline=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=sense,Offset=h,UDegree=2,VDegree=1,UCount=4,VCount=2,Dimension=3};
    var plane=new BlendSurfaceInstruction{Analytic=new(SurfaceClass.Plane,new(){Y=.25},new(){Y=1},new(){X=1}),NormalSense=-1};
    var program=new NestedBlendProgram(reversed?new[]{spline,plane}:new[]{plane,spline},new[]{new BlendSpineInstruction{First=0,Second=1,ChartCount=2}},
        new[]{new BlendChartInstruction{Position=new(){Y=.25,Z=h},Chord=new(){X=1},Scale=2.7,FirstV=.25,SecondV=.25},
            new BlendChartInstruction{Position=new(){X=1,Y=.25,Z=h+.25},Parameter=2.7,Scale=1,FirstU=1,SecondU=1,FirstV=.25,SecondV=.25}},data,new double[2048]);
    var domain=new SurfaceParameterBox(1.35-1e-5,1.35+1e-5,0,0);Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
    var status=JointNestedBlendEvaluation.BoundSpine(program,0,domain,bounds);
    if(h==.75){Require(status!=AlgorithmStatus.Success,"negative nearest/focal fixture accepted");return;}
    Require(status==AlgorithmStatus.Success,"C1 crossing tube: "+status);
    foreach(var t in new[]{domain.U0,1.35,domain.U1})
    {
        var lo=.49;var hi=.51;
        for(var k=0;k<60;k++){var mid=(lo+hi)*.5;var w=2*Math.Max(mid-.5,0);if(mid-h*w/Math.Sqrt(1+w*w)<t/2.7)lo=mid;else hi=mid;}
        var u=(lo+hi)*.5;var slope=2*Math.Max(u-.5,0);var z=Math.Pow(Math.Max(u-.5,0),2)+h/Math.Sqrt(1+slope*slope);
        Require(bounds[0].Lower<=t/2.7&&t/2.7<=bounds[0].Upper,"C1 X enclosure");
        Require(bounds[1].Lower<=.25&&.25<=bounds[1].Upper,"C1 Y enclosure");
        Require(bounds[2].Lower<=z&&z<=bounds[2].Upper,"C1 Z enclosure");
        Require(bounds[3].Lower<=1/2.7&&1/2.7<=bounds[3].Upper,"C1 X velocity");
        Require(bounds[5].Lower<=slope/2.7&&slope/2.7<=bounds[5].Upper,"C1 Z velocity");
    }
}

static unsafe void CheckOriginalLifecycle()
{
    var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
    var directory=Path.Combine(root,"temp_docs/cartesian-independent-review/original/temp_docs/regular-solid-blend-oracle/cartesian-regular-frame-lifecycle");
    var actual=Receive(Path.Combine(directory,"plane-nurbs-lifecycle.x_t"));var reference=Receive(Path.Combine(directory,"plane-nurbs-lifecycle-reference.x_t"));
    try
    {
        foreach(var body in new[]{actual,reference})
        {
            var options=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;
            Check(PK_BODY_check(body,&options,&count,&faults),"original full BODY_check");
            try{Require(count==0,"original body faults "+count);}finally{if(faults!=null)PK_MEMORY_free(faults);}
        }
        ParasolidBodyCompare.AssertEquivalent(reference,actual,"independent archived lifecycle");
        var massOptions=new PK_TOPOL_eval_mass_props_o_t();double* a=stackalloc double[15];double* b=stackalloc double[15];
        Check(PK_TOPOL_eval_mass_props(1,&reference,.99999999,&massOptions,a,a+1,a+2,a+5,a+14),"PK reference mass");
        Check(PK_TOPOL_eval_mass_props(1,&actual,.99999999,&massOptions,b,b+1,b+2,b+5,b+14),"PK GM XT mass");
        var residual=0.0;for(var i=0;i<15;i++)residual=Math.Max(residual,Math.Abs(a[i]-b[i])/(1+Math.Abs(a[i])));
        Require(residual<1e-7,"original mass gate");Console.WriteLine($"PASS archived lifecycle: real PK38 receive, BODY_check, strict compare, normalized mass residual={residual:R}");
    }
    finally{Check(PK_ENTITY_delete(1,&actual),"delete actual");Check(PK_ENTITY_delete(1,&reference),"delete reference");}
}
static unsafe int Receive(string path)
{
    var bytes=File.ReadAllBytes(path);Console.WriteLine($"original XT {Path.GetFileName(path)} bytes={bytes.Length} sha256={Convert.ToHexStringLower(SHA256.HashData(bytes))}");
    fixed(byte* pointer=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;int* parts;
        Check(PK_PART_receive_b(block,&options,&count,&parts),"PK receive original XT");
        try{Require(count==1,"one original part");return parts[0];}finally{if(parts!=null)PK_MEMORY_free(parts);}
    }
}
static unsafe V Vector(PK_VECTOR_t value)=>new(){X=value.coord[0],Y=value.coord[1],Z=value.coord[2]};
static V Add(V a,V b)=>new(){X=a.X+b.X,Y=a.Y+b.Y,Z=a.Z+b.Z};
static V Sub(V a,V b)=>new(){X=a.X-b.X,Y=a.Y-b.Y,Z=a.Z-b.Z};
static V Scale(V a,double s)=>new(){X=a.X*s,Y=a.Y*s,Z=a.Z*s};
static double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
static V Cross(V a,V b)=>new(){X=a.Y*b.Z-a.Z*b.Y,Y=a.Z*b.X-a.X*b.Z,Z=a.X*b.Y-a.Y*b.X};
static void Check(PK_ERROR_code_t error,string label)=>ParasolidScriptHost.Check(error,label);
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
static string Source([CallerFilePath]string path="")=>path;

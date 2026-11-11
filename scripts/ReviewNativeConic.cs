#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property AssemblyName=KernelTests
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static parasolid;

// Synthetic computation views test arithmetic, not PK model construction.
var matrices=0;var maximum=0.0;var wrongUnitExclusions=0;
Span<IntervalNumber> boxes=stackalloc IntervalNumber[7],matrix=stackalloc IntervalNumber[49],unused=stackalloc IntervalNumber[49];
foreach(var ellipse in new[]{false,true})foreach(var cylinder in new[]{false,true})
foreach(var reversed in new[]{false,true})foreach(var sign in new[]{-1.0,1.0})
{
    var surfaces=Surfaces(cylinder,sign);var spines=Spines(ellipse,reversed);var charts=Charts();
    var program=new NestedBlendProgram(surfaces,spines,charts);
    double[] state=[2.1,-.4,.9,.2,.3,-.1,.31];
    for(var i=0;i<7;i++)boxes[i]=new(state[i]-1e-14,state[i]+1e-14);
    Require(JointNestedBlendEvaluation.TestConicSubstitutionMatrix(program,boxes,matrix,unused)==AlgorithmStatus.Success,"actual new Jacobian");
    for(var column=0;column<7;column++)
    {
        var x=state.Select(v=>new Complex(v,0)).ToArray();x[column]+=new Complex(0,1e-25);
        var residual=Independent(x,ellipse,cylinder,reversed,sign);
        for(var row=0;row<7;row++)
        {
            var derivative=residual[row].Imaginary/1e-25;var interval=matrix[row*7+column];
            var error=Math.Max(0,Math.Max(interval.Lower-derivative,derivative-interval.Upper));maximum=Math.Max(maximum,error);
            Require(error<2e-13,$"complex derivative row={row} column={column} error={error:R}");
        }
    }
    if(cylinder)
    {
        var q=state[6];var cx=.1+.88*Math.Cos(q)+(ellipse?-.42:-.66)*Math.Sin(q);
        var wrong=sign*(-2*(cx+.2)+2*.6*(.6*2.3+.8*(-.5)+1e-6*.6));var entry=matrix[(reversed?1:0)*7];
        Require(wrong<entry.Lower-2e-13||wrong>entry.Upper+2e-13,"control detects assuming squared axis exactly one");wrongUnitExclusions++;
    }
    matrices++;
}
Console.WriteLine($"MATH actualMatrices={matrices} independentComplexColumns={7*matrices} maxOutsideInterval={maximum:R} wrongUnitFormulaExcluded={wrongUnitExclusions}");

// A squared radial equation alone would admit rho=-r for r<0. The actual
// cylinder gate rejects that case; signed Blend Range0 still means abs(range).
foreach(var badRadius in new[]{0.0,-.7,double.NaN,double.PositiveInfinity})
{
    var surfaces=Surfaces(true,1);surfaces[3].Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},badRadius);
    var spines=Spines(false,false);var program=new NestedBlendProgram(surfaces,spines,Charts());
    IntervalNumber[] invalidBoxes=[2.1,-.4,.9,.2,.3,-.1,.31];
    Require(JointNestedBlendEvaluation.TestConicSubstitutionMatrix(program,invalidBoxes,matrix,unused)!=AlgorithmStatus.Success,"invalid radius rejected");
}
Span<IntervalNumber> bound=stackalloc IntervalNumber[9],witness=stackalloc IntervalNumber[7];
foreach(var far in new[]{false,true})
{
    var radius=far?2.5:.5;var angle=far?Math.PI:0;
    BlendSurfaceInstruction[] surfaces=[new(){IsBlend=true,Spine=0,First=1,Second=2,Range0=-radius,Range1=-radius,NormalSense=1},
        new(){Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1),NormalSense=1},
        new(){Analytic=new(SurfaceClass.Plane,default,new(){Z=1},new(){X=1}),NormalSense=1},
        new(){Analytic=new(SurfaceClass.Plane,default,new(){Z=1},new(){X=1}),NormalSense=1}];
    BlendSpineInstruction[] spines=[new(){First=1,Second=2,Curve=new(){Kind=CurveClass.Circle,First=new(){X=1},Second=new(){Y=1}},ChartCount=2},
        new(){First=0,Second=3,ChartOffset=2,ChartCount=2}];
    BlendChartInstruction[] charts=[new(){Position=new(){X=Math.Cos(angle-.02),Y=Math.Sin(angle-.02)},Parameter=angle-.02,Scale=1},
        new(){Position=new(){X=Math.Cos(angle+.02),Y=Math.Sin(angle+.02)},Parameter=angle+.02,Scale=1},
        new(){Position=new(){X=1.5},Chord=new(){Y=1},Scale=1},new(){Position=new(){X=Math.Sqrt(2.25-.02*.02),Y=.02},Parameter=.02,Scale=1}];
    var program=new NestedBlendProgram(surfaces,spines,charts);
    var status=JointNestedBlendEvaluation.BoundSpine(program,1,new(.002,.003,0,0),bound);
    if(far){Require(status!=AlgorithmStatus.Success,"farthest stationary branch must fail");Console.WriteLine($"NEAREST farStation status={status} (no success publication)");}
    else
    {
        Require(status==AlgorithmStatus.Success,"nearest consumer");var t=.0025;var x=Math.Sqrt(2.25-t*t);
        Require(Contains(bound[0],x)&&Contains(bound[1],t)&&Contains(bound[3],-t/x)&&Contains(bound[4],1),"original CHART position and t derivative");
        Require(JointNestedBlendEvaluation.BoundSharedSpineWitness(program,1,new(.002,.003,0,0),bound,witness)==AlgorithmStatus.Success,"original witness capture");
        witness[6]=new(Math.PI-.01,Math.PI+.01);
        Require(JointNestedBlendEvaluation.BoundSharedSpineWitness(program,1,new(.002,.003,0,0),bound,default,witness)!=AlgorithmStatus.Success,"wrong q witness rejected");
        Console.WriteLine("NEAREST nearStation originalChartPositionAndVelocity=true wrongQWitnessRejected=true signedRangeAbs=true");
    }
}

unsafe
{
    Require(ParasolidScriptHost.TryStartSession("Independent native conic product review",out var host,out var message),message);
    using(host)
    {
        var folder=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,"../temp_docs/conic-independent-review/originals/temp_docs/short-edge-blend-oracle/conic-quadratic-elimination"));
        var reference=Receive(Path.Combine(folder,"a1-r0.5-t1E-07-reverseFalse-default-native.x_t"),"b1b0028feca8fd51e0b4b5615d4c34ad81adbee43648dd6c5e320ef9af464655");
        var actual=Receive(Path.Combine(folder,"a1-r0.5-t1E-07-reverseFalse-default-self.x_t"),"37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba");
        foreach(var body in new[]{reference,actual})
        {
            var options=new PK_BODY_check_o_t{max_faults=100};PK_check_fault_t* faults;int count;
            Check(PK_BODY_check(body,&options,&count,&faults),"original product full BODY_check");
            try{Require(count==0,$"body={body} faults={count}");}finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free faults");}
        }
        ParasolidBodyCompare.AssertEquivalent(reference,actual,"original conic product");
        var expected=Edges(reference);var found=Edges(actual);Require(expected.Count==3&&found.Count==3,"three finite ICurves");
        var maximumDistance=0.0;
        foreach(var edge in expected)
        {
            var index=found.FindIndex(e=>Distance(e.Low,edge.Low)<1e-8&&Distance(e.High,edge.High)<1e-8||Distance(e.Low,edge.High)<1e-8&&Distance(e.High,edge.Low)<1e-8);
            Require(index>=0,"finite endpoint pair");var other=found[index];found.RemoveAt(index);
            var a=ParasolidBodyCompare.FiniteLength(edge.Curve,edge.Range,16);var b=ParasolidBodyCompare.FiniteLength(other.Curve,other.Range,16);
            Require(Math.Max(a.Upper-b.Lower,b.Upper-a.Lower)<2e-8,"finite length interval");
            maximumDistance=Math.Max(maximumDistance,Math.Max(Samples(edge,other),Samples(other,edge)));
        }
        var massOptions=new PK_TOPOL_eval_mass_props_o_t();double* aMass=stackalloc double[15];double* bMass=stackalloc double[15];
        Check(PK_TOPOL_eval_mass_props(1,&reference,.99999999999,&massOptions,aMass,aMass+1,aMass+2,aMass+5,aMass+14),"reference mass");
        Check(PK_TOPOL_eval_mass_props(1,&actual,.99999999999,&massOptions,bMass,bMass+1,bMass+2,bMass+5,bMass+14),"actual mass");
        var error=0.0;for(var i=0;i<15;i++)error=Math.Max(error,Math.Abs(aMass[i]-bMass[i])/(1+Math.Abs(aMass[i])));
        Require(error<1e-7,"15 point mass quantities");
        Console.WriteLine($"PRODUCT fullChecks=2 zeroFaults=true finiteICurves=3 samplesPerDirection=17 sampleMaxDistance={maximumDistance:R} normalized15Mass={error:R}");
    }
}
Console.WriteLine("PASS independent conic risks and original product; no GM modeling rerun or timing claim");

static BlendSurfaceInstruction[] Surfaces(bool cylinder,double sign)=>[
    new(){IsBlend=true,Spine=0,Range0=-.5,Range1=-.5,NormalSense=sign},default,default,
    new(){Analytic=new(cylinder?SurfaceClass.Cylinder:SurfaceClass.Plane,new(){X=-.2,Y=.1,Z=.3},new(){X=.6,Y=.8,Z=1e-6},new(){Z=1},.7),NormalSense=-1}];
static BlendSpineInstruction[] Spines(bool ellipse,bool reversed)=>[
    new(){Curve=new(){Kind=ellipse?CurveClass.Ellipse:CurveClass.Circle,Origin=new(){X=.1,Y=-.2,Z=.3},First=new(){X=.88,Y=.66},Second=new(){X=ellipse?-.42:-.66,Y=ellipse?.56:.88}},ChartCount=2},
    new(){First=reversed?3:0,Second=reversed?0:3,ChartOffset=2,ChartCount=2}];
static BlendChartInstruction[] Charts()=>[new(){Parameter=0,Scale=1},new(){Parameter=1,Scale=1},new(){Position=new(){X=1.7},Chord=new(){X=.2,Y=.3,Z=.4},Scale=.8},new(){Parameter=1,Scale=1}];
static Complex[] Independent(Complex[] x,bool ellipse,bool cylinder,bool reversed,double sign)
{
    // Direct, uncancelled norm-polynomials and complex-step differentiation:
    // this reference does not call Dual, H, K²R or the author's directional matrix.
    Complex[] a=[.88,.66,0],b=[ellipse?-.42:-.66,ellipse?.56:.88,0],origin=[.1,-.2,.3],o=[-.2,.1,.3],axis=[.6,.8,1e-6];
    Complex[] delta=new Complex[3],relative=new Complex[3],tangent=new Complex[3],residual=new Complex[7];
    var cylinderSquare=(Complex)0;var blendSquare=(Complex)(-.25);var foot=(Complex)0;var axial=(Complex)0;
    for(var i=0;i<3;i++){var c=origin[i]+a[i]*Complex.Cos(x[6])+b[i]*Complex.Sin(x[6]);delta[i]=x[i]-c;relative[i]=x[i]-o[i];tangent[i]=-a[i]*Complex.Sin(x[6])+b[i]*Complex.Cos(x[6]);residual[3+i]=x[3+i]-c;axial+=axis[i]*relative[i];}
    for(var i=0;i<3;i++){blendSquare+=delta[i]*delta[i];foot+=delta[i]*tangent[i];var radial=relative[i]-axis[i]*axial;cylinderSquare+=radial*radial;}
    cylinderSquare-=.7*.7;var row=reversed?1:0;
    residual[row]=sign*(blendSquare-(cylinder?cylinderSquare:0));residual[1-row]=-(cylinder?cylinderSquare:axial);
    residual[2]=.2*(x[0]-1.7)+.3*x[1]+.4*x[2]-.01/.8;residual[6]=foot;return residual;
}
static bool Contains(IntervalNumber x,double value)=>value>=x.Lower&&value<=x.Upper;
static unsafe PK_BODY_t Receive(string path,string hash)
{
    var bytes=File.ReadAllBytes(path);Require(Convert.ToHexStringLower(SHA256.HashData(bytes))==hash,"original XT SHA256");
    fixed(byte* data=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,data);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;PK_PART_t* parts;
        Check(PK_PART_receive_b(block,&options,&count,&parts),"original XT receive");try{Require(count==1,"one original body");return parts[0];}finally{Check(PK_MEMORY_free(parts),"free received array");}}
}
static unsafe List<FiniteEdge> Edges(PK_BODY_t body)
{
    int count;PK_EDGE_t* edges;Check(PK_BODY_ask_edges(body,&count,&edges),"product edges");var result=new List<FiniteEdge>();PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
    try{for(var i=0;i<count;i++){PK_CURVE_t curve;PK_CLASS_t kind;PK_INTERVAL_t range;PK_LOGICAL_t sense;Check(PK_EDGE_ask_geometry(edges[i],PK_LOGICAL_true,&curve,&kind,ends,&range,&sense),"finite geometry");if(kind!=PK_CLASS_icurve)continue;
        Require(double.IsFinite(range.value[0])&&double.IsFinite(range.value[1])&&range.value[1]>range.value[0],"finite range");PK_VECTOR_t low,high;Check(PK_CURVE_eval(curve,range.value[0],0,&low),"finite start");Check(PK_CURVE_eval(curve,range.value[1],0,&high),"finite end");result.Add(new(curve,range,low,high));}}
    finally{Check(PK_MEMORY_free(edges),"free edges");}return result;
}
static unsafe double Samples(FiniteEdge source,FiniteEdge target)
{
    var sourceRange=source.Range;var targetRange=target.Range;
    var maximum=0.0;for(var i=0;i<=16;i++){var t=sourceRange.value[0]+(sourceRange.value[1]-sourceRange.value[0])*i/16;PK_VECTOR_t p,q;double at;
        Check(PK_CURVE_eval(source.Curve,t,0,&p),"source finite sample");Check(PK_CURVE_parameterise_vector(target.Curve,p,&at),"target finite parameter");
        Require(at>=targetRange.value[0]-1e-8&&at<=targetRange.value[1]+1e-8,"sample on target trim");Check(PK_CURVE_eval(target.Curve,Math.Clamp(at,targetRange.value[0],targetRange.value[1]),0,&q),"target finite sample");maximum=Math.Max(maximum,Distance(p,q));}
    Require(maximum<1e-8,"bidirectional finite samples");return maximum;
}
static unsafe double Distance(PK_VECTOR_t a,PK_VECTOR_t b){var x=a.coord[0]-b.coord[0];var y=a.coord[1]-b.coord[1];var z=a.coord[2]-b.coord[2];return Math.Sqrt(x*x+y*y+z*z);}
static string Source([CallerFilePath]string path="")=>path;
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
static void Check(PK_ERROR_code_t error,string label){if(error!=0)throw new InvalidOperationException($"{label}: PK error={error}");}
readonly record struct FiniteEdge(PK_CURVE_t Curve,PK_INTERVAL_t Range,PK_VECTOR_t Low,PK_VECTOR_t High);

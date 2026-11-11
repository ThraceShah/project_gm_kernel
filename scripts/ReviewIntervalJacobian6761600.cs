#!/usr/bin/env dotnet run
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=KernelTests
#:property TargetFramework=net10.0
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using BufferOffset=System.Int32;
using BufferCount=System.Int32;

// Independent review probe. Run with -p:KernelTestHooks=true; no production edits.
RebindControls();
for(var occurrences=2;occurrences<=4;occurrences++)CheckChain(occurrences);
CheckChain(5);CheckChain(16);
Console.WriteLine($"PASS independent review; nodeBytes={Unsafe.SizeOf<IntervalExpressionNode>()}; max eligible record bytes={2112*(Unsafe.SizeOf<IntervalExpressionNode>()+15*Unsafe.SizeOf<IntervalNumber>())}");

static void RebindControls()
{
    Span<IntervalExpressionNode> nodes=stackalloc IntervalExpressionNode[96];
    Span<BufferCount> count=stackalloc BufferCount[1];count[0]=0;
    Span<IntervalNumber> scratch=stackalloc IntervalNumber[192],matrix=stackalloc IntervalNumber[2];
    Span<BufferOffset> outputs=stackalloc BufferOffset[1];
    var x=IntervalExpression.Variable(nodes,count,new(1.1,1.2),0);
    var y=IntervalExpression.Variable(nodes,count,new(2,2.1),1);
    IntervalExpression.SinCos(x,out var sine,out var cosine);
    var result=IntervalExpression.Sqrt(IntervalExpression.Square(x)+IntervalExpression.Square(y))+(sine-cosine*x)/(y-x);
    Require(result.Finite,"initial finite");outputs[0]=result.OutputIndex();
    Require((x+2).PositiveValue(),"initial positive guard");
    IntervalExpression.PrepareDerivatives(nodes[..count[0]]);
    IntervalNumber[][] boxes=[[new(.1,.2),new(.3,.4)],[new(1e-150),new(2e-150)],[new(1e7,1e7+.1),new(2e7,2e7+.2)]];
    foreach(var box in boxes)
    {
        Require(IntervalExpression.Rebind(nodes[..count[0]],box)==AlgorithmStatus.Success,"rebind");
        Require(IntervalExpression.Differentiate(nodes[..count[0]],outputs,2,scratch,matrix)==AlgorithmStatus.Success,"derivatives");
        for(var seed=0;seed<2;seed++)
        {
            var a=new IntervalDual(box[0],seed==0?1:0);var b=new IntervalDual(box[1],seed==1?1:0);
            IntervalDual.SinCos(a,out var s,out var c);
            var expected=IntervalDual.Sqrt(IntervalDual.Square(a)+IntervalDual.Square(b))+(s-c*a)/(b-a);
            Equal(expected.Value,nodes[outputs[0]].Zero.Value);Equal(expected.Derivative,matrix[seed]);
        }
    }
    Require(IntervalExpression.Rebind(nodes[..count[0]],[new(-4,-3),new(2,3)])==AlgorithmStatus.NotConverged,"positive guard rejects rebound box");
    Require(IntervalExpression.Rebind(nodes[..count[0]],[new(1,2),new(1,2)])==AlgorithmStatus.NotConverged,"singularity rejects rebound box");
    Require(IntervalExpression.Rebind(nodes[..count[0]],[new(1.1,1.2),new(2,2.1)])==AlgorithmStatus.Success,"recover after failed rebind");
    Require(IntervalExpression.Differentiate(nodes[..count[0]],outputs,2,scratch,matrix)==AlgorithmStatus.Success,"recover derivatives");
    Span<IntervalExpressionNode> small=stackalloc IntervalExpressionNode[1];count[0]=0;
    var only=IntervalExpression.Variable(small,count,1,0);var overflow=only+2;
    Require(count[0]>small.Length&&!overflow.Finite,"capacity failure remains explicit");
    Console.WriteLine("PASS arithmetic rebind across scale, positive/singular negative controls, recovery and capacity exhaustion");
}

static void CheckChain(BufferCount occurrences)
{
    var x=new KernelVector3{X=1};var y=new KernelVector3{Y=1};var z=new KernelVector3{Z=1};
    var surfaces=new BlendSurfaceInstruction[2+2*(occurrences-1)];
    var spines=new BlendSpineInstruction[occurrences];var charts=new BlendChartInstruction[2*occurrences];
    surfaces[0]=new(){Analytic=new(SurfaceClass.Plane,default,x,y),NormalSense=1};
    surfaces[1]=new(){Analytic=new(SurfaceClass.Plane,default,z,x),NormalSense=1};
    spines[0]=new(){First=0,Second=1,ChartCount=2};
    var step=-Math.Sqrt(.96);
    for(var i=0;i<occurrences;i++)
    {
        charts[2*i]=new(){Position=new(){X=step*i,Z=-.2*i},Chord=y,Scale=1};
        charts[2*i+1]=new(){Position=new(){X=step*i,Y=1,Z=-.2*i},Parameter=1,Scale=1};
        if(i==0)continue;
        surfaces[2*i]=new(){IsBlend=true,First=spines[i-1].First,Second=spines[i-1].Second,Spine=i-1,Range0=1,Range1=1,NormalSense=1};
        surfaces[2*i+1]=new(){Analytic=new(SurfaceClass.Plane,new(){Z=-.2*i},z,x),NormalSense=1};
        spines[i]=new(){First=2*i,Second=2*i+1,ChartOffset=2*i,ChartCount=2};
    }
    var program=new NestedBlendProgram(surfaces,spines,charts);
    Span<IntervalNumber> reference=stackalloc IntervalNumber[63*63],actual=stackalloc IntervalNumber[63*63],bounds=stackalloc IntervalNumber[9];
    foreach(var width in occurrences<=4?new[]{0.0,1e-7,.001,.01}:Array.Empty<double>())
    {
        MemoryMarshal.AsBytes(actual).Fill(0xA5);
        var status=JointNestedBlendEvaluation.TestIntervalJacobianRecord(program,occurrences-1,.37,width,reference,actual,out var n,out var a,out var b);
        Require(status==AlgorithmStatus.Success,$"chain preparation {occurrences}: {status}");
        Require(n==4*occurrences-1&&a==b,$"chain dimensions/status {n}: {a}/{b}");
        Require(a==AlgorithmStatus.Success,$"chain interval positive control {occurrences} width {width}: {a}");
        for(var i=0;i<n*n;i++)Equal(reference[i],actual[i]);
    }
    var bound=JointNestedBlendEvaluation.BoundSpine(program,occurrences-1,new(.37,.370001,0,0),bounds);
    Require(bound==AlgorithmStatus.Success,$"full certified chain bound {occurrences}: {bound}");
    Require(bounds[0].Lower<=step*(occurrences-1)&&bounds[0].Upper>=step*(occurrences-1),"manufactured X contained");
    Require(bounds[2].Lower<=-.2*(occurrences-1)&&bounds[2].Upper>=-.2*(occurrences-1),"manufactured Z contained");
    Console.WriteLine($"PASS {occurrences} occurrences / {4*occurrences-1} variables: {(occurrences<=4?"cold/rebound full Jacobian and ":"unrecorded path, ")}actual certified BoundSpine");
}

static void Equal(IntervalNumber a,IntervalNumber b)
{
    Require(BitConverter.DoubleToInt64Bits(a.Lower)==BitConverter.DoubleToInt64Bits(b.Lower)&&BitConverter.DoubleToInt64Bits(a.Upper)==BitConverter.DoubleToInt64Bits(b.Upper),$"interval differs [{a.Lower:R},{a.Upper:R}] / [{b.Lower:R},{b.Upper:R}]");
}
static void Require(bool condition,string message){if(!condition)throw new Exception(message);}

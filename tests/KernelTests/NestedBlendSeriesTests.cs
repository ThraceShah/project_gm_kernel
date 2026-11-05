using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class NestedBlendSeriesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SplineSupportAndItsParallelOffsetPreserveExactMixedJets(bool rational)
    {
        var surfaces=Surfaces();var charts=Charts();var dimension=rational?4:3;var scalars=new double[4*dimension+8];
        for(var u=0;u<2;u++)for(var v=0;v<2;v++)
        {
            var i=(2*u+v)*dimension;var weight=rational?2:1;scalars[i+1]=u*weight;scalars[i+2]=(2*v-1)*weight;if(rational)scalars[i+3]=weight;
        }
        double[] knots=[0,0,1,1,-1,-1,1,1];knots.CopyTo(scalars,4*dimension);
        surfaces[0]=new(){IsBSpline=true,SplineBasis=0,UDegree=1,VDegree=1,UCount=2,VCount=2,Dimension=dimension,Rational=rational,NormalSense=1};
        surfaces[2]=surfaces[0];surfaces[2].Offset=-.2;charts[1].FirstU=1;
        Span<double> scratch=stackalloc double[8192];var program=new NestedBlendProgram(surfaces,Spines(),charts,scalars,scratch);
        SurfaceDerivativeLayout.TryCreate(2,2,out var layout);Span<KernelVector3> actual=stackalloc KernelVector3[9],expected=stackalloc KernelVector3[9];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Evaluate(program,4,.37,.23,layout,actual));
        var reference=new NestedBlendProgram(Surfaces(),Spines(),Charts());Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Evaluate(reference,4,.37,.23,layout,expected));
        for(var i=0;i<9;i++){Near(expected[i].X,actual[i].X);Near(expected[i].Y,actual[i].Y);Near(expected[i].Z,actual[i].Z);}
    }

    [Fact]
    public void CompiledPlaneSpineBlendHasTheManufacturedPositionAndMixedJets()
    {
        var surfaces=Surfaces();var spines=Spines();var charts=Charts();var program=new NestedBlendProgram(surfaces,spines,charts);
        SurfaceDerivativeLayout.TryCreate(2,2,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[9];
        Assert.Equal(AlgorithmStatus.Success,NestedBlendSeriesEvaluation.Evaluate(program,4,.37,.23,layout,jet));
        Span<KernelVector3> joint=stackalloc KernelVector3[9];Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Evaluate(program,4,.37,.23,layout,joint));
        for(var i=0;i<9;i++){Near(jet[i].X,joint[i].X);Near(jet[i].Y,joint[i].Y);Near(jet[i].Z,joint[i].Z);}
        var angle=.23*Math.PI/2;
        for(var u=0;u<=2;u++)for(var v=0;v<=2;v++)
        {
            var value=jet[layout.GetIndex(u,v)];var factor=.2*Math.Pow(Math.PI/2,v);
            Near(u==0?-factor*Math.Sin(angle+v*Math.PI/2):0,value.X);
            Near(v==0?(u==0?.37:u==1?1:0):0,value.Y);
            Near(u==0?-factor*Math.Cos(angle+v*Math.PI/2):0,value.Z);
        }
    }

    [Fact]
    public void NearestSpineCanCrossTheOriginalClosedChartSeam()
    {
        var axis=new KernelVector3{Z=1};var reference=new KernelVector3{X=1};
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Sphere,default,axis,reference,1),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,axis,reference),NormalSense=1},new(){IsBlend=true,Spine=0,Range0=.1,Range1=.1,NormalSense=1}];
        KernelVector3[] points=[new(){X=1},new(){Y=-1},new(){X=-1},new(){Y=1},new(){X=1}];
        var charts=new BlendChartInstruction[5];for(var i=0;i<5;i++)charts[i]=new(){Position=points[i],Parameter=i*Math.Sqrt(2),Scale=1,Chord=i<4?new(){X=(points[i+1].X-points[i].X)/Math.Sqrt(2),Y=(points[i+1].Y-points[i].Y)/Math.Sqrt(2)}:default};
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartOffset=0,ChartCount=5,StartType=LimitType.Help,EndType=LimitType.Help}];var program=new NestedBlendProgram(surfaces,spines,charts);
        var angle=.0001;var point=TaylorVector.Constant(new(){X=1.2*Math.Cos(angle),Y=1.2*Math.Sin(angle),Z=.03},0);
        Assert.Equal(AlgorithmStatus.Success,NestedBlendSeriesEvaluation.Distance(program,2,point,0,out var distance,out var normal));
        Near(Math.Sqrt(.04+.0009)-.1,distance[0]);Near(.03/Math.Sqrt(.0409),normal.Z[0]);
    }

    [Fact]
    public void BlendDistancePropagatesFifthOrderPathJetsThroughTheNearestSpineConstraint()
    {
        var surfaces=Surfaces();var spines=Spines();var charts=Charts();var program=new NestedBlendProgram(surfaces,spines,charts);
        var t=TaylorJet.Variable(.37,5);var radius=.4+t*t*.1;TaylorJet.SinCos(t,out var sin,out var cos);
        var point=new TaylorVector(radius*cos,t,radius*sin);
        Assert.Equal(AlgorithmStatus.Success,NestedBlendSeriesEvaluation.Distance(program,4,point,0,out var distance,out var normal));
        Assert.Equal(AlgorithmStatus.Success,NestedBlendSeriesEvaluation.RecursiveDistance(program,4,point,0,out var reference,out _));
        for(var k=0;k<=5;k++)Near(reference[k],distance[k]);
        for(var k=0;k<=5;k++){Near((radius-.2)[k],distance[k]);Near(cos[k],normal.X[k]);Near(0,normal.Y[k]);Near(sin[k],normal.Z[k]);}
    }

    internal static BlendSurfaceInstruction[] Surfaces()
    {
        var x=new KernelVector3{X=1};var y=new KernelVector3{Y=1};var z=new KernelVector3{Z=1};
        return
        [
            new(){Analytic=new(SurfaceClass.Plane,default,x,y),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Plane,default,z,x),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Plane,new(){X=-.2},x,y),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Plane,new(){Z=-.2},z,x),NormalSense=1},
            new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1},
        ];
    }
    internal static BlendSpineInstruction[] Spines()=>[new(){First=0,Second=1,ChartOffset=0,ChartCount=2}];
    internal static BlendChartInstruction[] Charts()=>
        [new(){Position=default,Chord=new(){Y=1},Scale=1},new(){Position=new(){Y=1},Parameter=1,Scale=1}];
    private static void Near(double expected,double actual)=>Assert.InRange(Math.Abs(expected-actual),0,2e-11);
}

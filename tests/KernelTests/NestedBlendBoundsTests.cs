global using BlendSurfaceIndex=System.Int32;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class NestedBlendBoundsTests
{
    [Theory]
    [InlineData(1)] [InlineData(-1)]
    public void ImplicitCircleCurvatureRetainsTheSignedSupportingNormal(double normalSense)
    {
        var z=new KernelVector3{Z=1};var x=new KernelVector3{X=1};
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Plane,default,z,x),NormalSense=normalSense},new(){Analytic=new(SurfaceClass.Sphere,default,z,x,1),NormalSense=1}];
        Span<KernelVector3> positions=stackalloc KernelVector3[3],tangents=stackalloc KernelVector3[3],chords=stackalloc KernelVector3[2];
        Span<double> parameters=stackalloc double[3],scales=stackalloc double[2];
        for(var i=0;i<3;i++){var angle=(i-1)*.3;positions[i]=new(){X=Math.Cos(angle),Y=Math.Sin(angle)};tangents[i]=new(){X=-Math.Sin(angle),Y=Math.Cos(angle)};}
        Assert.Equal(AlgorithmStatus.Success,OriginalChartParameterMap.Build(positions,tangents,0,1,parameters,scales,chords,out _,out _));
        var charts=new BlendChartInstruction[3];
        for(var i=0;i<3;i++)charts[i]=new(){Position=positions[i],Parameter=parameters[i],Chord=i<2?chords[i]:default,Scale=i<2?scales[i]:1};
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=3,StartType=LimitType.Artificial,EndType=LimitType.Artificial}];
        var program=new NestedBlendProgram(surfaces,spines,charts);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpineCurvature(program,0,(parameters[1]+parameters[2])*.5,0,out var curvature));
        Assert.InRange(normalSense,curvature.Lower,curvature.Upper);Assert.InRange(curvature.Upper-curvature.Lower,0,1e-8);
    }

    [Fact]
    public void CertifiedTubeEnclosesPositionsAndFirstDerivativesThroughoutAParameterCell()
    {
        var program=new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());
        Check(program,4,new(.2,.6,.1,.9));
    }

    [Fact]
    public void CurvedImplicitSpineHasAContractingIntervalTube()
    {
        var z=new KernelVector3{Z=1};var x=new KernelVector3{X=1};var length=Math.Sqrt(.4);
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Sphere,default,z,x,1),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,z,x),NormalSense=1},new(){Analytic=new(SurfaceClass.Sphere,default,z,x,.8),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){Z=-.2},z,x),NormalSense=1},new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1}];
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=2}];BlendChartInstruction[] charts=[new(){Position=x,Chord=new(){X=-.2/length,Y=-.6/length},Scale=1},new(){Position=new(){X=.8,Y=-.6},Parameter=length,Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);Check(program,4,new(.2,.202,.35,.45));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SplineContactBoundsIncludeTensorLinearExtensionAcrossBothEdges(bool rational)
    {
        var dim=rational?4:3;var scalars=new double[9*dim+12];
        for(var i=0;i<3;i++)for(var j=0;j<3;j++)
        {
            var w=rational?1+.15*i+.2*j:1;var at=dim*(3*i+j);scalars[at]=i*.5*w;scalars[at+1]=j*.5*w;scalars[at+2]=(.3*i*i-.1*j*j+.07*i*j)*w;if(rational)scalars[at+3]=w;
        }
        double[] knots=[0,0,0,1,1,1];knots.CopyTo(scalars,9*dim);knots.CopyTo(scalars,9*dim+6);
        var node=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=1,UDegree=2,VDegree=2,UCount=3,VCount=3,Dimension=dim,Rational=rational};
        var program=new NestedBlendProgram([node],[],[],scalars,new double[2048]);var bounds=new IntervalNumber[18];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.IntervalSplineBounds(program,node,new(-.03,.03),new(.97,1.03),bounds));
        var source=program.Spline(node);var jet=new KernelVector3[9];SurfaceDerivativeLayout.TryCreate(2,2,out var layout);int[] indices=[0,3,1,6,4,2];
        for(var i=0;i<=6;i++)for(var j=0;j<=6;j++)
        {
            Assert.Equal(AlgorithmStatus.Success,BSurfaceEvaluation.Evaluate(source,-.03+.01*i,.97+.01*j,layout,program.Workspace,jet));
            for(var k=0;k<6;k++){var p=jet[indices[k]];Assert.InRange(p.X,bounds[3*k].Lower,bounds[3*k].Upper);Assert.InRange(p.Y,bounds[3*k+1].Lower,bounds[3*k+1].Upper);Assert.InRange(p.Z,bounds[3*k+2].Lower,bounds[3*k+2].Upper);}
        }
    }

    [Fact]
    public void ExtendedSplineFootIsNotClampedBackToTheOriginalKnotDomain()
    {
        double[] scalars=[0,0,0,0,1,0,1,0,0,1,1,0,0,0,1,1,0,0,1,1];
        var node=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=1,UDegree=1,VDegree=1,UCount=2,VCount=2,Dimension=3};
        var program=new NestedBlendProgram([node],[],[],scalars,new double[2048]);var u=1.0;var v=.4;var target=new KernelVector3{X=1+5e-13,Y=.4,Z=.1};
        Assert.Equal(AlgorithmStatus.Success,NestedSplineEvaluation.DistanceFromSeed(program,node,TaylorVector.Constant(target,0),ref u,ref v,out var distance,out _));
        Assert.True(u>1);Assert.InRange(Math.Abs(distance[0]-.1),0,1e-14);
    }

    [Fact]
    public void SingularSupportsNeverProduceASuccessfulCertifiedTube()
    {
        var surfaces=NestedBlendSeriesTests.Surfaces();surfaces[1]=surfaces[0];var program=new NestedBlendProgram(surfaces,NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());
        var bounds=new IntervalNumber[9];Assert.NotEqual(AlgorithmStatus.Success,JointNestedBlendEvaluation.Bound(program,4,new(.2,.3,.1,.9),bounds));
    }

    private static void Check(in NestedBlendProgram program,BlendSurfaceIndex surface,in SurfaceParameterBox box)
    {
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Bound(program,surface,box,bounds));
        SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> point=stackalloc KernelVector3[4];
        for(var i=0;i<=4;i++)for(var j=0;j<=4;j++)
        {
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Evaluate(program,surface,box.U0+(box.U1-box.U0)*i/4,box.V0+(box.V1-box.V0)*j/4,layout,point));
            for(var k=0;k<3;k++)
            {
                var value=point[k==0?0:k==1?layout.GetIndex(1,0):layout.GetIndex(0,1)];
                Assert.InRange(value.X,bounds[3*k].Lower,bounds[3*k].Upper);Assert.InRange(value.Y,bounds[3*k+1].Lower,bounds[3*k+1].Upper);Assert.InRange(value.Z,bounds[3*k+2].Lower,bounds[3*k+2].Upper);
            }
        }
    }
}

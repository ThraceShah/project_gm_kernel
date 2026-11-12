using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class SplineCartesianRowTests
{
    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void ReboundRowsEncloseTheOriginalNativeEquationsAwayFromARoot(bool rational,bool reversed)
    {
        var program=PlaneSplinePointJacobianTests.Program(rational,reversed);
        IntervalNumber[] box=[new(.18),new(.36),new(.19),new(.23),new(.43)];
        Span<IntervalDual> original=stackalloc IntervalDual[5],rebound=stackalloc IntervalDual[5],cartesian=stackalloc IntervalDual[5];
        for(var direction=-1;direction<=5;direction++)
        {
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian,direction));
            for(var i=0;i<5;i++)
            {
                Assert.True((original[i].Value-rebound[i].Value).ContainsZero);Assert.True((original[i].Value-rebound[i].Value).Magnitude<1e-10);
                Assert.True((original[i].Derivative-rebound[i].Derivative).ContainsZero);Assert.True((original[i].Derivative-rebound[i].Derivative).Magnitude<1e-10);
            }
        }
        Assert.True(original[reversed?0:1].Value.Magnitude>.01);
    }

    [Theory]
    [InlineData(-.17,false)] [InlineData(.17,false)] [InlineData(.17,true)]
    public void OriginalOffsetAndChartMapRemainBoundAndWrongPositionOrUvFails(double offset,bool reversed)
    {
        var program=Planar(offset,reversed);var u=.4/2.7;
        IntervalNumber[] box=[new(u),new(.3),new(offset),new(u),new(.3)];
        Span<IntervalDual> original=stackalloc IntervalDual[5],rebound=stackalloc IntervalDual[5],cartesian=stackalloc IntervalDual[5];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian));
        for(var i=0;i<5;i++){Assert.True(original[i].Value.ContainsZero);Assert.True(cartesian[i].Value.ContainsZero);}
        box[2]=new(offset+.02);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian));
        Assert.False(original[reversed?0:1].Value.ContainsZero);Assert.False(cartesian[4].Value.ContainsZero);
        box[2]=new(offset);box[3]=new(u+.1);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian));
        Assert.False(original[3].Value.ContainsZero);Assert.False(cartesian[reversed?0:1].Value.ContainsZero);
        Span<IntervalNumber> bound=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,new(.2,.6,0,0),bound));
        Assert.InRange(.2/2.7,bound[0].Lower,bound[0].Upper);Assert.InRange(.6/2.7,bound[0].Lower,bound[0].Upper);
        Assert.InRange(.3,bound[1].Lower,bound[1].Upper);Assert.InRange(offset,bound[2].Lower,bound[2].Upper);
        Assert.InRange(1/2.7,bound[3].Lower,bound[3].Upper);Assert.True(bound[4].ContainsZero);Assert.True(bound[5].ContainsZero);
    }

    [Fact]
    public void DegenerateSplineFramesRemainFailures()
    {
        var program=PlaneSplinePointJacobianTests.Program(false,false,degenerate:true);
        IntervalNumber[] box=[new(.18),new(.36),new(.19),new(.23),new(.43)];
        Span<IntervalDual> original=stackalloc IntervalDual[5],rebound=stackalloc IntervalDual[5],cartesian=stackalloc IntervalDual[5];
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ChangingUvFrameDerivativesAndTransformedInverseMatchAnAnalyticShearedParabola(bool reversed)
    {
        const double u=.25,v=.35,h=.17,shear=.5;var w=2*u;var length=Math.Sqrt(1+(1+shear*shear)*w*w);
        double[] normal=[-w/length,shear*w/length,1/length];
        var lengthU=2*(1+shear*shear)*w/length;
        double[] normalU=[-2/length+w*lengthU/(length*length),2*shear/length-shear*w*lengthU/(length*length),-lengthU/(length*length)];
        var program=Parabolic(h,reversed,shear);
        double[] root=[u+shear*v+h*normal[0],v+h*normal[1],u*u+h*normal[2],u,v];
        IntervalNumber[] box=root.Select(x=>new IntervalNumber(x)).ToArray();var row=reversed?0:1;
        Span<IntervalNumber> matrix=stackalloc IntervalNumber[25];Span<double> inverse=stackalloc double[25];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineMatrix(program,box,matrix,inverse));
        double[] analytic=new double[25];analytic[row*5]=1;analytic[3*5+1]=1;analytic[4*5+2]=1;
        analytic[(1-row)*5+1]=-1;analytic[2*5]=1;
        analytic[row*5+3]=-1-h*normalU[0];analytic[3*5+3]=-h*normalU[1];analytic[4*5+3]=-w-h*normalU[2];
        analytic[row*5+4]=-shear;analytic[3*5+4]=-1;
        for(var i=0;i<25;i++)Assert.InRange(analytic[i],matrix[i].Lower,matrix[i].Upper);
        for(var i=0;i<5;i++)for(var j=0;j<5;j++)
        {
            var product=0.0;for(var k=0;k<5;k++)product+=inverse[i*5+k]*analytic[k*5+j];
            Assert.InRange(product-(i==j?1:0),-2e-13,2e-13);
        }
        // Away from the root J_F=M*J_G+M_U*G: the frame derivative term
        // must be present, rather than assuming a constant row transform.
        box[2]=new(root[2]+.07);
        Span<IntervalDual> original=stackalloc IntervalDual[5],rebound=stackalloc IntervalDual[5],cartesian=stackalloc IntervalDual[5];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian,3));
        var expected=-h*(normalU[0]*normal[0]+normalU[1]*normal[1]+normalU[2]*normal[2])-.07*normalU[2];
        Assert.InRange(expected,original[row].Derivative.Lower,original[row].Derivative.Upper);
        Assert.InRange(expected,rebound[row].Derivative.Lower,rebound[row].Derivative.Upper);
        Assert.True(Math.Abs(expected)>1e-3);
    }

    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void DirectPointColumnsMatchFullDirectionalRowsAcrossUvBoxesAndExtension(bool rational,bool reversed)
    {
        var program=PlaneSplinePointJacobianTests.Program(rational,reversed);
        IntervalNumber[] box=[new(.18,.24),new(.33,.38),new(.16,.21),new(-.03,.04),new(.97,1.03)];
        Span<IntervalNumber> matrix=stackalloc IntervalNumber[25];
        Span<IntervalDual> original=stackalloc IntervalDual[5],rebound=stackalloc IntervalDual[5],cartesian=stackalloc IntervalDual[5];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineMatrix(program,box,matrix,default));
        for(var column=0;column<5;column++)
        {
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian,column));
            for(var row=0;row<5;row++)
            {
                Assert.Equal(cartesian[row].Derivative.Lower,matrix[row*5+column].Lower);
                Assert.Equal(cartesian[row].Derivative.Upper,matrix[row*5+column].Upper);
            }
        }
        var before=GC.GetAllocatedBytesForCurrentThread();
        var status=JointNestedBlendEvaluation.TestCartesianSplineMatrix(program,box,matrix,default);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
    }

    [Fact]
    public void ZeroOffsetDoesNotHideNonfiniteOrPotentiallySingularFrames()
    {
        var program=Parabolic(0,false);Span<IntervalNumber> matrix=stackalloc IntervalNumber[25];
        // The native sheet extends linearly, so a huge finite UV alone
        // must not be mistaken for polynomial extrapolation or overflow.
        IntervalNumber[] box=[0,.3,0,new(1e100),new(.3)];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineMatrix(program,box,matrix,default));
        program=Parabolic(0,false,height:1e200);box=[0,.3,0,new(.2),new(.3)];
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.TestCartesianSplineMatrix(program,box,matrix,default));
        program=PlaneSplinePointJacobianTests.Program(false,false,degenerate:true);
        box=[0,.3,0,new(.2,.3),new(.3,.4)];
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.TestCartesianSplineMatrix(program,box,matrix,default));
    }

    [Theory]
    [InlineData(-.17)] [InlineData(.17)]
    public void CurvedTubeRetainsNativeFootDerivativeChartWitnessAndNearestChecks(double offset)
    {
        var program=Parabolic(offset,false);const double u=.25,scale=2.7;var w=2*u;var length=Math.Sqrt(1+w*w);
        var parameter=scale*(u-offset*w/length);var z=u*u+offset/length;
        Span<IntervalNumber> bound=stackalloc IntervalNumber[9];var domain=new SurfaceParameterBox(parameter-1e-5,parameter+1e-5,0,0);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,domain,bound));
        Assert.InRange(u-offset*w/length,bound[0].Lower,bound[0].Upper);Assert.InRange(.3,bound[1].Lower,bound[1].Upper);Assert.InRange(z,bound[2].Lower,bound[2].Upper);
        Assert.InRange(1/scale,bound[3].Lower,bound[3].Upper);Assert.True(bound[4].ContainsZero);Assert.InRange(w/scale,bound[5].Lower,bound[5].Upper);
        Span<IntervalNumber> witness=stackalloc IntervalNumber[5];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSharedSpineWitness(program,0,domain,bound,witness));
        witness[3]=new(10,11);
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.BoundSharedSpineWitness(program,0,domain,bound,default,witness));
        // An offset beyond the centre of curvature has an exact normal
        // station but cannot pass the original positive nearest Hessian.
        program=Parabolic(2,false);
        Assert.NotEqual(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,new(-1e-5,1e-5,0,0),bound));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PeriodicOrCreasedSheetsRetainTheirNativeEquationPath(bool periodic)
    {
        var spline=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=1,UDegree=1,VDegree=1,UCount=3,VCount=2,Dimension=3,UPeriodic=periodic};
        var plane=new BlendSurfaceInstruction{Analytic=new(SurfaceClass.Plane,new(){Y=.3},new(){Y=1},new(){X=1}),NormalSense=1};
        double[] data=[0,0,0,0,1,0,.5,0,.2,.5,1,.2,1,0,0,1,1,0,0,0,.5,1,1,0,0,1,1];
        if(periodic){spline.UCount=2;data=[0,0,0,0,1,0,1,0,0,1,1,0,0,0,1,1,0,0,1,1];}
        var program=new NestedBlendProgram(new[]{plane,spline},new[]{new BlendSpineInstruction{First=0,Second=1,ChartCount=2}},
            new[]{new BlendChartInstruction{Chord=new(){X=1},Scale=1},new BlendChartInstruction{Parameter=1,Scale=1}},data,new double[1024]);
        IntervalNumber[] box=[new(.2),new(.3),new(.1),new(.2),new(.3)];Span<IntervalNumber> matrix=stackalloc IntervalNumber[25];
        Assert.Equal(AlgorithmStatus.Unsupported,JointNestedBlendEvaluation.TestCartesianSplineMatrix(program,box,matrix,default));
    }

    [Theory]
    [InlineData(-.17,false)] [InlineData(.17,false)] [InlineData(-.17,true)] [InlineData(.17,true)]
    public unsafe void IdenticalFullContractionBoxesReuseOnlyTheJacobianAndKeepAllSixMaps(double offset,bool curved)
    {
        var program=curved?Parabolic(offset,false):Planar(offset,false);const double u=.25;
        var parameter=curved?2.7*(u-offset*(2*u)/Math.Sqrt(1+4*u*u)):.4;
        var domain=new SurfaceParameterBox(parameter-1e-5,parameter+1e-5,0,0);
        Span<IntervalNumber> expected=stackalloc IntervalNumber[9],actual=stackalloc IntervalNumber[9];
        var enabled=ModelingPerformance.Enabled;var original=JointNestedBlendEvaluation.ReuseStableSplineJacobian;
        try
        {
            ModelingPerformance.Enabled=true;JointNestedBlendEvaluation.ReuseStableSplineJacobian=false;JointNestedBlendEvaluation.TestSplineContractionMaps=0;
            ModelingPerformance.Context reference=default,current=default;
            using(var scope=new ModelingPerformance.Scope(&reference))Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,domain,expected));
            Assert.Equal(6,JointNestedBlendEvaluation.TestSplineContractionMaps);var calls=ModelingPerformance.Last.JacobianCalls;
            JointNestedBlendEvaluation.ReuseStableSplineJacobian=true;JointNestedBlendEvaluation.TestSplineContractionMaps=0;
            using(var scope=new ModelingPerformance.Scope(&current))Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,domain,actual));
            Assert.Equal(6,JointNestedBlendEvaluation.TestSplineContractionMaps);
            Assert.InRange(ModelingPerformance.Last.JacobianCalls,1,calls);if(!curved)Assert.True(ModelingPerformance.Last.JacobianCalls<calls);
            for(var i=0;i<9;i++){Assert.Equal(expected[i].Lower,actual[i].Lower);Assert.Equal(expected[i].Upper,actual[i].Upper);}
        }
        finally{JointNestedBlendEvaluation.ReuseStableSplineJacobian=original;ModelingPerformance.Enabled=enabled;}
    }

    private static NestedBlendProgram Parabolic(double offset,bool reversed,double shear=0,double height=1)
    {
        var spline=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=-1,Offset=offset,UDegree=2,VDegree=1,UCount=3,VCount=2,Dimension=3};
        var plane=new BlendSurfaceInstruction{Analytic=new(SurfaceClass.Plane,new(){Y=.3},new(){Y=1},new(){X=1}),NormalSense=-1};
        var data=new double[18+6+4];
        for(var i=0;i<3;i++)for(var j=0;j<2;j++){var at=3*(2*i+j);data[at]=i*.5+shear*j;data[at+1]=j;data[at+2]=i==2?height:0;}
        double[] knots=[0,0,0,1,1,1,0,0,1,1];knots.CopyTo(data,18);
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=2}];
        BlendChartInstruction[] charts=[new(){Position=new(){Y=.3,Z=offset},Chord=new(){X=1},Scale=shear==0?2.7:1,FirstV=.3,SecondV=.3},new(){Position=new(){X=1,Y=.3,Z=1},Parameter=2.7,Scale=1,FirstU=1,FirstV=.3,SecondU=1,SecondV=.3}];
        return new(reversed?new[]{spline,plane}:new[]{plane,spline},spines,charts,data,new double[1024]);
    }

    private static NestedBlendProgram Planar(double offset,bool reversed)
    {
        var spline=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=-1,Offset=offset,UDegree=1,VDegree=1,UCount=2,VCount=2,Dimension=3};
        var plane=new BlendSurfaceInstruction{Analytic=new(SurfaceClass.Plane,new(){Y=.3},new(){Y=1},new(){X=1}),NormalSense=-1};
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=2}];
        BlendChartInstruction[] charts=[new(){Position=new(){Y=.3,Z=offset},Chord=new(){X=1},Scale=2.7,FirstV=.3,SecondV=.3},new(){Position=new(){X=1,Y=.3,Z=offset},Parameter=2.7,Scale=1,FirstU=1,FirstV=.3,SecondU=1,SecondV=.3}];
        double[] data=[0,0,0,0,1,0,1,0,0,1,1,0,0,0,1,1,0,0,1,1];
        return new(reversed?new[]{spline,plane}:new[]{plane,spline},spines,charts,data,new double[1024]);
    }
}

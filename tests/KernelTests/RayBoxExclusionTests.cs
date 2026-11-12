using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class RayBoxExclusionTests
{
    [Theory]
    [InlineData(-1,2,.5,1,0,0,true)]
    [InlineData(-1,1,1,1,0,0,false)]
    [InlineData(-1,-1,-1,1,1,1,false)]
    [InlineData(.5,.5,.5,1,0,0,false)]
    [InlineData(2,.5,.5,1,0,0,true)]
    [InlineData(.5,2,.5,0,0,1,true)]
    [InlineData(2,.5,.5,-1,0,0,false)]
    public void StrictSlabsRejectLateralMissesAndRetainClosedHits(double x,double y,double z,double dx,double dy,double dz,bool misses)
        =>Assert.Equal(misses,OperationIntersection.RayMissesBox(new(0,1,0,1,0,1),Vector(x,y,z),Vector(dx,dy,dz),10));

    [Fact]
    public void TheOriginalResidualGateAndEndpointRoundingRemainCandidates()
    {
        var tolerance=ParametricSurfaceIntersection.RootResidualTolerance;var box=new IntervalBox3(0,1,0,1,0,1);
        Assert.False(OperationIntersection.RayMissesBox(box,Vector(-1,1+.5*tolerance,.5),Vector(1,0,0),10));
        Assert.True(OperationIntersection.RayMissesBox(box,Vector(-1,1+4*tolerance,.5),Vector(1,0,0),10));
        Assert.False(OperationIntersection.RayMissesBox(box,Vector(-1,.5,.5),Vector(1,0,0),1));
        var scale=Math.ScaleB(1,500);var width=Math.ScaleB(1,450);
        var translated=new IntervalBox3(scale,scale+width,scale,scale+width,scale,scale+width);
        Assert.False(OperationIntersection.RayMissesBox(translated,Vector(scale-width,scale+width,scale+width),Vector(1,0,0),4*width));
        Assert.True(OperationIntersection.RayMissesBox(translated,Vector(scale-width,scale+2*width,scale+.5*width),Vector(1,0,0),4*width));
    }

    [Fact]
    public void ZeroSubnormalAndUnknownArithmeticCannotInventAnEmptyIntersection()
    {
        var box=new IntervalBox3(0,1,0,1,0,1);
        Assert.False(OperationIntersection.RayMissesBox(box,Vector(.5,.5,.5),Vector(0,-0.0,double.Epsilon),1));
        Assert.True(OperationIntersection.RayMissesBox(box,Vector(.5,2,.5),Vector(-0.0,0,double.Epsilon),1));
        Assert.False(OperationIntersection.RayMissesBox(box,Vector(.5,.5,.5),Vector(1,0,0),double.PositiveInfinity));
        Assert.False(OperationIntersection.RayMissesBox(box,Vector(double.MaxValue,.5,.5),Vector(double.MaxValue,0,0),2));
        Assert.False(OperationIntersection.RayMissesBox(box,Vector(double.NaN,2,.5),Vector(1,0,0),1));
        Assert.False(OperationIntersection.RayMissesBox(new(double.NaN,1,0,1,0,1),Vector(-1,2,.5),Vector(1,0,0),1));
        Assert.False(OperationIntersection.RayMissesBox(new(1,0,0,1,0,1),Vector(-1,2,.5),Vector(1,0,0),1));
    }

    [Fact]
    public void ACartesianHullRequiresFinitePositiveHomogeneousWeights()
    {
        double[] knots=[0,0,1,1];double[] poles=[0,0,0,1,0,1,0,1,1,0,0,1,1,1,0,1];
        var source=new BSurfaceView(1,1,2,2,4,true,false,false,poles,knots,knots);
        Assert.True(OperationIntersection.HasFinitePositiveSplineHull(source));
        poles[7]=-1;Assert.False(OperationIntersection.HasFinitePositiveSplineHull(source));
        poles[7]=0;Assert.False(OperationIntersection.HasFinitePositiveSplineHull(source));
        poles[7]=1;poles[4]=double.NaN;Assert.False(OperationIntersection.HasFinitePositiveSplineHull(source));
        poles[4]=0;knots[2]=double.PositiveInfinity;Assert.False(OperationIntersection.HasFinitePositiveSplineHull(source));
    }

    [Fact]
    public void ABlendBoxMustCoverEverySearchedChartCell()
    {
        BlendSurfaceInstruction[] surfaces=[new(),new(),new(){IsBlend=true,Spine=0}];
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartOffset=0,ChartCount=3}];
        BlendChartInstruction[] charts=[new(){Parameter=0},new(){Parameter=.5},new(){Parameter=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);
        Assert.True(OperationIntersection.CoversBlendRayDomain(program,2,new(0,1,0,1)));
        Assert.True(OperationIntersection.CoversBlendRayDomain(program,2,new(-.1,1.1,-.2,1.2)));
        Assert.False(OperationIntersection.CoversBlendRayDomain(program,2,new(0,.5,0,1)));
        Assert.False(OperationIntersection.CoversBlendRayDomain(program,2,new(double.NaN,1,0,1)));
        charts[1].Parameter=double.NaN;Assert.False(OperationIntersection.CoversBlendRayDomain(program,2,new(0,1,0,1)));
    }
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Projection;
using ProjectGmKernel.Native.Runtime;
namespace KernelTests;
[Collection("KernelTests")]
public class BezierRestrictionTests
{
    [Theory] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(8)] [InlineData(16)]
    public void RestrictedMonomialPolesEncloseTheExactBlossomAtBothDivisionChoicesAndEndpoints(SplineDegree degree)
    {
        foreach(var exponent in new[]{-500,0,500})
        {
            var scale=Math.ScaleB(1,exponent);var input=new IntervalNumber[4*(degree+1)];var output=new IntervalNumber[input.Length];
            for(var i=0;i<=degree;i++){input[4*i]=i==degree?scale:0;input[4*i+1]=i==0?scale:0;input[4*i+2]=0;input[4*i+3]=scale;}
            foreach(var (a,b) in new[]{(0.0,1.0),(.125,.375),(.625,.875),(.5,.5),(0.0,0.0),(1.0,1.0)})
            {
                BezierSurfaceBounds.TestRestrictCurve(input,degree,new(a),new(b),output,false);
                for(var k=0;k<=degree;k++)
                {
                    AssertContains(output[4*k],scale*Math.Pow(a,degree-k)*Math.Pow(b,k));
                    AssertContains(output[4*k+1],scale*Math.Pow(1-a,degree-k)*Math.Pow(1-b,k));
                    AssertContains(output[4*k+2],0);AssertContains(output[4*k+3],scale);
                }
            }
        }
    }
    [Fact]
    public void ExtensionsKeepTheGeneralBlossomAndUncertainInputEnclosesPointControls()
    {
        foreach(var degree in new[]{0,1,2,16})
        {
            var input=new IntervalNumber[4*(degree+1)];var actual=new IntervalNumber[input.Length];var original=new IntervalNumber[input.Length];
            for(var i=0;i<input.Length;i++)input[i]=new(.001*(i+1),.001*(i+1)+.0001);
            foreach(var (a,b) in new[]{(-.2,.4),(.9,1.1),(-1.0,2.0)})
            {
                BezierSurfaceBounds.TestRestrictCurve(input,degree,new(a),new(b),actual,false);BezierSurfaceBounds.TestRestrictCurve(input,degree,new(a),new(b),original,true);
                for(var i=0;i<input.Length;i++){Assert.Equal(BitConverter.DoubleToInt64Bits(original[i].Lower),BitConverter.DoubleToInt64Bits(actual[i].Lower));Assert.Equal(BitConverter.DoubleToInt64Bits(original[i].Upper),BitConverter.DoubleToInt64Bits(actual[i].Upper));}
            }
            BezierSurfaceBounds.TestRestrictCurve(input,degree,new(.2,.21),new(.7,.71),actual,false);var point=input.Select(x=>new IntervalNumber(x.Midpoint)).ToArray();
            foreach(var a in new[]{.2,.205,.21})foreach(var b in new[]{.7,.705,.71})
            {BezierSurfaceBounds.TestRestrictCurve(point,degree,new(a),new(b),original,true);for(var i=0;i<input.Length;i++){AssertContains(actual[i],original[i].Lower);AssertContains(actual[i],original[i].Upper);}}
        }
    }
    [Fact]
    public void AliasedOutputIsStableAndHotRestrictionAllocatesNothing()
    {
        var input=new IntervalNumber[68];var expected=new IntervalNumber[68];for(var i=0;i<input.Length;i++)input[i]=new(.01*i,.01*i+.001);var alias=(IntervalNumber[])input.Clone();
        BezierSurfaceBounds.TestRestrictCurve(input,16,new(.2),new(.8),expected,false);BezierSurfaceBounds.TestRestrictCurve(alias,16,new(.2),new(.8),alias,false);
        for(var i=0;i<input.Length;i++){Assert.Equal(expected[i].Lower,alias[i].Lower);Assert.Equal(expected[i].Upper,alias[i].Upper);}
        var start=GC.GetAllocatedBytesForCurrentThread();BezierSurfaceBounds.TestRestrictCurve(input,16,new(.1),new(.2),expected,false);var allocated=GC.GetAllocatedBytesForCurrentThread()-start;Assert.Equal(0,allocated);
    }
    [Fact]
    public void WholeGlobalProjectionClosesItsOriginalGapWithBothRestrictionExpressions()
    {
        double[] poles=new double[27],z=[.25,-.25,.25],knots=[0,0,0,1,1,1];for(var u=0;u<3;u++)for(var v=0;v<3;v++){var at=3*(u*3+v);poles[at]=u*.5;poles[at+1]=v*.5;poles[at+2]=z[u]+z[v]-.0625;}
        var surface=new BSurfaceView(2,2,3,3,3,false,false,false,poles,knots,knots);var target=new KernelVector3{X=.5,Y=.5,Z=-1};
        try{foreach(var old in new[]{false,true}){BezierSurfaceBounds.UseBlossomRestrictions=old;var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};Assert.Equal(AlgorithmStatus.Success,KernelRuntime.ClosestBSurface(surface,target,1e-9,ref best));Assert.InRange(best.DistanceUpper,.9375-1e-12,.9375+1e-9);Assert.InRange(Math.Abs(best.U-.5),0,1e-7);Assert.InRange(Math.Abs(best.V-.5),0,1e-7);}}
        finally{BezierSurfaceBounds.UseBlossomRestrictions=false;}
    }
    static void AssertContains(IntervalNumber box,double value){Assert.True(box.Finite);Assert.True(box.Lower<=value&&value<=box.Upper,$"{value:R} outside [{box.Lower:R},{box.Upper:R}]");}
}

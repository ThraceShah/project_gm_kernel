using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

[Collection("KernelTests")]
public class CylindricalParameterRayTests
{
    [Theory]
    [InlineData(.125)] [InlineData(1.7)] [InlineData(8.0)]
    public void NativeDualFramePhaseEnclosesAnIndependentTiltedEllipseAcrossPeriodicLifts(double scale)
    {
        var cylinder=new AnalyticSurface(SurfaceClass.Cylinder,new(){X=3,Y=5,Z=7},new(){X=1.0/3,Y=2.0/3,Z=-2.0/3},new(){X=2.0/3,Y=1.0/3,Z=2.0/3},2*scale);
        var radius=2*scale;var height=.5*scale;
        Span<BlendSpineInstruction> spine=stackalloc BlendSpineInstruction[1];Span<BlendChartInstruction> chart=stackalloc BlendChartInstruction[2];
        spine[0]=new(){Curve=new(){Kind=CurveClass.Ellipse,Origin=cylinder.Origin,First=Add(Scale(cylinder.X,radius),Scale(cylinder.Axis,height)),Second=Scale(cylinder.Y,radius)},ChartCount=2};
        chart[0]=new(){Parameter=.1};chart[1]=new(){Parameter=.101};var program=new NestedBlendProgram(default,spine,chart);
        var spatial=new SurfaceIntersectionPatch(program,0,.1,.101);Span<AnalyticSurface> support=stackalloc AnalyticSurface[1]{cylinder};
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        foreach(var slope in new[]{-.2/scale,0,.2/scale})foreach(var lift in new[]{-1,0,1})
        {
            var q=.4+lift*Math.Tau;var qv=.1*scale;var coordinate=new SurfaceIntersectionPatch(spatial,support,q,qv,slope);
            Assert.Equal(AlgorithmStatus.Success,coordinate.Bound(new(.1,.101,0,0),bounds));
            foreach(var t in new[]{.1,.1005,.101})
            {
                var phase=t-q-slope*(height*Math.Cos(t)-qv);var derivative=1+slope*height*Math.Sin(t);
                Contains(bounds[0],radius*Math.Sin(phase));Contains(bounds[3],radius*Math.Cos(phase)*derivative);
                Contains(bounds[1],radius*Math.Cos(phase));Contains(bounds[4],-radius*Math.Sin(phase)*derivative);
                Contains(bounds[2],height*Math.Cos(t));Contains(bounds[5],-height*Math.Sin(t));
            }
            var before=GC.GetAllocatedBytesForCurrentThread();var status=coordinate.Bound(new(.1,.101,0,0),bounds);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
        }
    }
    private static void Contains(IntervalNumber bound,double value)=>Assert.InRange(value,bound.Lower,bound.Upper);
}

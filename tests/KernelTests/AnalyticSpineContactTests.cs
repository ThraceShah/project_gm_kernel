using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class AnalyticSpineContactTests
{
    [Theory]
    [InlineData((int)SurfaceClass.Cylinder)] [InlineData((int)SurfaceClass.Sphere)] [InlineData((int)SurfaceClass.Plane)]
    public void AnalyticContactIsAnExactAffineImageWithReversibleNativeParameter(int representation)
    {
        var kind=(SurfaceClass)representation;
        var support=new BlendSurfaceInstruction{Analytic=new(kind,default,new(){Z=1},new(){X=1},2),NormalSense=-1};
        var spine=new ParametricSpineCurve{Kind=CurveClass.Circle,Origin=new(){Z=kind==SurfaceClass.Plane?.5:0},First=new(){X=2.5},Second=new(){Y=2.5}};
        Assert.Equal(AlgorithmStatus.Success,AnalyticSpineContact.Curve(spine,support,-.5,out var contact));
        var targetRadius=kind==SurfaceClass.Plane?2.5:2;
        Assert.InRange(Math.Abs(contact.First.X-targetRadius),0,1e-14);Assert.Equal(0,contact.Origin.Z);
        const double phase=.47;var c=Math.Cos(phase);var s=Math.Sin(phase);
        var reversed=contact;reversed.First=new(){X=targetRadius*c,Y=targetRadius*s};reversed.Second=new(){X=targetRadius*s,Y=-targetRadius*c};
        Assert.True(AnalyticSpineContact.Equivalent(contact,reversed,1e-12));
        reversed.Second.Y+=1e-6;Assert.False(AnalyticSpineContact.Equivalent(contact,reversed,1e-12));
        reversed=contact;reversed.Origin.X+=1e-6;Assert.False(AnalyticSpineContact.Equivalent(contact,reversed,1e-12));
    }

    [Fact]
    public void LineContactPreservesAxialDirectionAndDoesNotRecogniseAnOffsetLine()
    {
        var support=new BlendSurfaceInstruction{Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},2),NormalSense=1};
        var spine=new ParametricSpineCurve{Kind=CurveClass.Line,Origin=new(){X=2.5},First=new(){Z=1}};
        Assert.Equal(AlgorithmStatus.Success,AnalyticSpineContact.Curve(spine,support,.5,out var contact));Assert.Equal(2,contact.Origin.X);
        var target=contact;target.Origin.Z=7;target.First.Z=-2;Assert.True(AnalyticSpineContact.Equivalent(contact,target,1e-12));
        target.Origin.X+=1e-6;Assert.False(AnalyticSpineContact.Equivalent(contact,target,1e-12));
        target=contact;target.First.X=1e-13;
        Assert.False(AnalyticSpineContact.Equivalent(contact,target,1e-12,1e6,2e6));
    }
}

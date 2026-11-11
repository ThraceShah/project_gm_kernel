using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class NestedBlendJacobianRecordCapacityTests
{
    [Fact]
    public void LargestRecordedOccurrenceChainCertifiesItsManufacturedNativeSpine()
    {
        var surfaces=NestedBlendSeriesTests.Surfaces().ToList();var spines=NestedBlendSeriesTests.Spines().ToList();var charts=NestedBlendSeriesTests.Charts().ToList();
        BlendSurfaceIndex previous=4;var dx=-Math.Sqrt(.03);
        for(var depth=1;depth<=3;depth++)
        {
            BlendSurfaceIndex plane=surfaces.Count;surfaces.Add(new(){Analytic=new(SurfaceClass.Plane,new(){Z=-.1*depth},new(){Z=1},new(){X=1}),NormalSense=1});
            BlendSpineIndex spine=spines.Count;spines.Add(new(){First=previous,Second=plane,ChartOffset=charts.Count,ChartCount=2});
            charts.Add(new(){Position=new(){X=dx*depth,Z=-.1*depth},Chord=new(){Y=1},Scale=1});
            charts.Add(new(){Position=new(){X=dx*depth,Y=1,Z=-.1*depth},Parameter=1,Scale=1});
            BlendSurfaceIndex blend=surfaces.Count;surfaces.Add(new(){IsBlend=true,Spine=spine,First=previous,Second=plane,Range0=.2,Range1=.2,NormalSense=1});previous=blend;
        }
        var program=new NestedBlendProgram(surfaces.ToArray(),spines.ToArray(),charts.ToArray());
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,3,new(.37,.37001,0,0),bounds));
        Assert.InRange(3*dx,bounds[0].Lower,bounds[0].Upper);Assert.InRange(.37,bounds[1].Lower,bounds[1].Upper);Assert.InRange(.37001,bounds[1].Lower,bounds[1].Upper);
        Assert.InRange(-.1*3,bounds[2].Lower,bounds[2].Upper);
        for(var i=0;i<3;i++)Assert.InRange(i==1?1:0,bounds[3+i].Lower,bounds[3+i].Upper);
    }
}

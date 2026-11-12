global using GeomTag=System.Int32;
global using EntityGeneration=System.Int32;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
namespace KernelTests;

public class JointTubeWorkTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SameNativeSourceReusesTheCompleteTubeAndPreservesAnalyticDerivatives(bool reversed)
    {
        var p=Planar(.17,reversed);var entries=new JointTubeEntry[128];var cached=With(p,entries);Span<IntervalNumber> expected=stackalloc IntervalNumber[9],actual=stackalloc IntervalNumber[9];
        var domain=new SurfaceParameterBox(.2,.6,0,0);Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(p,0,domain,expected));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(cached,0,domain,actual));Bits(expected,actual);Assert.Single(entries,e=>e.Source.Tag>0);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(cached,0,domain,actual));Bits(expected,actual);
        Assert.InRange(.2/2.7,actual[0].Lower,actual[0].Upper);Assert.InRange(.6/2.7,actual[0].Lower,actual[0].Upper);Assert.InRange(1/2.7,actual[3].Lower,actual[3].Upper);
        var before=GC.GetAllocatedBytesForCurrentThread();var status=JointNestedBlendEvaluation.BoundSpine(cached,0,domain,actual);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
    }
    [Fact]
    public void DifferentSectionNeedsRecomputeTheNativeFrameInsteadOfReusingNineOutputs()
    {
        var p=new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());var entries=new JointTubeEntry[128];var cached=With(p,entries);Span<IntervalNumber> a=stackalloc IntervalNumber[9],b=stackalloc IntervalNumber[9];
        foreach(var v in new[]{new IntervalNumber(.2,.3),new IntervalNumber(.6,.7),new IntervalNumber(-.01,.01)})
        {
            var domain=new SurfaceParameterBox(.4,.4001,v.Lower,v.Upper);Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Bound(p,4,domain,a));Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Bound(cached,4,domain,b));Bits(a,b);
        }
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void SourceTagGenerationSensePreparationKindAndBlockAreSeparateIdentities(int change)
    {
        var p=Planar(.17,false);var entries=new JointTubeEntry[128];var cached=With(p,entries);Span<IntervalNumber> output=stackalloc IntervalNumber[9];var domain=new SurfaceParameterBox(.2,.6,0,0);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(cached,0,domain,output));var at=Array.FindIndex(entries,e=>e.Source.Tag>0);for(var i=0;i<entries[at].N;i++)entries[at].Boxes[i]=new(12345);
        var source=new JointTubeSource(change==1?102:101,change==2?2:1,change==3?-1:1,change==4?(byte)2:(byte)1,change==5?8:7);var different=new NestedBlendProgram(p.Surfaces,p.Spines,p.Charts,p.Scalars,p.Workspace,source,entries);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(different,0,domain,output));Assert.InRange(.3,output[1].Lower,output[1].Upper);Assert.True(output[1].Upper<12345);
    }
    [Fact]
    public void NativeChartAndWorkspaceDemandsCannotConsumeADifferentStoredTube()
    {
        var p=Planar(.17,false);var entries=new JointTubeEntry[128];var cached=With(p,entries);Span<IntervalNumber> actual=stackalloc IntervalNumber[9],expected=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(cached,0,new(.2,.6,0,0),actual));
        var domain=new SurfaceParameterBox(.7,.8,0,0);Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(cached,0,domain,actual));Assert.InRange(.7/2.7,actual[0].Lower,actual[0].Upper);
        var empty=new NestedBlendProgram(p.Surfaces,p.Spines,p.Charts,p.Scalars);var other=new NestedBlendProgram(p.Surfaces,p.Spines,p.Charts,p.Scalars,default,cached.TubeSource,entries);
        Assert.Equal(JointNestedBlendEvaluation.BoundSpine(empty,0,domain,expected),JointNestedBlendEvaluation.BoundSpine(other,0,domain,actual));
    }
    [Fact]
    public void WitnessConsumersStillRunTheirOriginalContainmentChecks()
    {
        var p=Planar(.17,false);var cached=With(p,new JointTubeEntry[128]);var domain=new SurfaceParameterBox(.2,.2001,0,0);Span<IntervalNumber> output=stackalloc IntervalNumber[9],witness=stackalloc IntervalNumber[5];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(cached,0,domain,output));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSharedSpineWitness(cached,0,domain,output,witness));witness[3]=new(10,11);
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.BoundSharedSpineWitness(cached,0,domain,output,default,witness));
    }
    [Fact]
    public void FailedJointProofNeverPublishesAReusableTube()
    {
        var p=PlaneSplinePointJacobianTests.Program(false,false,degenerate:true);var entries=new JointTubeEntry[128];var cached=With(p,entries);Span<IntervalNumber> output=stackalloc IntervalNumber[9];
        Assert.NotEqual(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(cached,0,new(.2,.3,0,0),output));Assert.All(entries,e=>Assert.Equal(0,e.Source.Tag));
    }
    private static NestedBlendProgram With(NestedBlendProgram p,Span<JointTubeEntry> work)=>new(p.Surfaces,p.Spines,p.Charts,p.Scalars,p.Workspace,new(101,1,1,1,7),work);
    private static void Bits(ReadOnlySpan<IntervalNumber> a,ReadOnlySpan<IntervalNumber> b){for(var i=0;i<a.Length;i++){Assert.Equal(BitConverter.DoubleToInt64Bits(a[i].Lower),BitConverter.DoubleToInt64Bits(b[i].Lower));Assert.Equal(BitConverter.DoubleToInt64Bits(a[i].Upper),BitConverter.DoubleToInt64Bits(b[i].Upper));}}
    private static NestedBlendProgram Planar(double offset,bool reversed)
    {
        var spline=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=-1,Offset=offset,UDegree=1,VDegree=1,UCount=2,VCount=2,Dimension=3};var plane=new BlendSurfaceInstruction{Analytic=new(SurfaceClass.Plane,new(){Y=.3},new(){Y=1},new(){X=1}),NormalSense=-1};
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=2}];BlendChartInstruction[] charts=[new(){Position=new(){Y=.3,Z=offset},Chord=new(){X=1},Scale=2.7,FirstV=.3,SecondV=.3},new(){Position=new(){X=1,Y=.3,Z=offset},Parameter=2.7,Scale=1,FirstU=1,FirstV=.3,SecondU=1,SecondV=.3}];double[] data=[0,0,0,0,1,0,1,0,0,1,1,0,0,0,1,1,0,0,1,1];return new(reversed?new[]{spline,plane}:new[]{plane,spline},spines,charts,data,new double[1024]);
    }
}

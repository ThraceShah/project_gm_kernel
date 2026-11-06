using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class ModelingPerformanceTests:IDisposable
{
    public ModelingPerformanceTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));ModelingPerformance.Enabled=true;ModelingPerformance.CacheEnabled=true;}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);ModelingPerformance.Enabled=false;ModelingPerformance.CacheEnabled=true;KernelRuntime.SessionStop();}

    [Fact]
    public void CertifiedBoundsAreBitwiseIdenticalAndNeverSurviveTheCommandScope()
    {
        var program=new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());
        var patch=new SurfaceIntersectionPatch(new SurfaceIntersectionPatch(program,4,new(.2,.4,.1,.9)),200,17,ParasolidConstants.PK_TOPOL_sense_positive_c);
        Span<IntervalNumber> expected=stackalloc IntervalNumber[9],actual=stackalloc IntervalNumber[9];var box=new SurfaceParameterBox(.2,.3,.2,.8);
        Assert.Equal(AlgorithmStatus.Success,patch.Bound(box,expected));
        for(var command=0;command<2;command++)
        {
            ModelingPerformance.Context context=default;
            using(var scope=new ModelingPerformance.Scope(&context,immutableGeometry:true))
            {
                Assert.Equal(AlgorithmStatus.Success,patch.Bound(box,actual));Assert.Equal(0,context.Profile.BoundCacheHits);
                Assert.Equal(AlgorithmStatus.Success,patch.Bound(box,actual));Assert.Equal(1,context.Profile.BoundCacheHits);
                for(var i=0;i<9;i++){Assert.Equal(expected[i].Lower,actual[i].Lower);Assert.Equal(expected[i].Upper,actual[i].Upper);}
            }
            Assert.Equal(1,ModelingPerformance.Last.BoundEvaluations);
        }
    }

    [Fact]
    public void IdentityGenerationSenseRootKindAndEveryParameterLimitParticipateInTheKey()
    {
        ModelingPerformance.Context context=default;using var scope=new ModelingPerformance.Scope(&context,immutableGeometry:true);
        Span<IntervalNumber> values=stackalloc IntervalNumber[9],output=stackalloc IntervalNumber[9];for(var i=0;i<9;i++)values[i]=i;
        var box=new SurfaceParameterBox(.2,.4,.3,.7);ModelingPerformance.StoreBound(200,17,1,4,false,box,values);
        Assert.True(ModelingPerformance.TryBound(200,17,1,4,false,box,output));
        Assert.False(ModelingPerformance.TryBound(201,17,1,4,false,box,output));Assert.False(ModelingPerformance.TryBound(200,18,1,4,false,box,output));
        Assert.False(ModelingPerformance.TryBound(200,17,-1,4,false,box,output));Assert.False(ModelingPerformance.TryBound(200,17,1,5,false,box,output));Assert.False(ModelingPerformance.TryBound(200,17,1,4,true,box,output));
        Assert.False(ModelingPerformance.TryBound(200,17,1,4,false,new(Math.BitIncrement(.2),.4,.3,.7),output));
        Assert.False(ModelingPerformance.TryBound(200,17,1,4,false,new(.2,Math.BitIncrement(.4),.3,.7),output));
        Assert.False(ModelingPerformance.TryBound(200,17,1,4,false,new(.2,.4,Math.BitIncrement(.3),.7),output));
        Assert.False(ModelingPerformance.TryBound(200,17,1,4,false,new(.2,.4,.3,Math.BitIncrement(.7)),output));
    }

    [Fact]
    public void TheRawCertifiedBoundCanBeReusedAcrossExactIsoparametricWrappers()
    {
        var program=new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());
        var patch=new SurfaceIntersectionPatch(new SurfaceIntersectionPatch(program,4,new(.2,.4,.1,.9)),200,17,1);
        var iso=new SurfaceIntersectionPatch(patch,false,.5,.2,.3);Span<IntervalNumber> expected=stackalloc IntervalNumber[9],actual=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success,iso.Bound(new(.2,.3,0,0),expected));
        ModelingPerformance.Context context=default;using var scope=new ModelingPerformance.Scope(&context,immutableGeometry:true);
        Assert.Equal(AlgorithmStatus.Success,patch.Bound(new(.2,.3,.5,.5),actual));
        Assert.Equal(AlgorithmStatus.Success,iso.Bound(new(.2,.3,0,0),actual));Assert.Equal(1,context.Profile.BoundCacheHits);
        for(var i=0;i<9;i++){Assert.Equal(expected[i].Lower,actual[i].Lower);Assert.Equal(expected[i].Upper,actual[i].Upper);}
    }

    [Fact]
    public void OptionalCacheAllocationFailurePreservesProofComputation()
    {
        var program=new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());
        var patch=new SurfaceIntersectionPatch(new SurfaceIntersectionPatch(program,4,new(.2,.4,.1,.9)),200,17,1);Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        KernelRuntime.InjectAllocationFailure(0);ModelingPerformance.Context context=default;
        using(var scope=new ModelingPerformance.Scope(&context,immutableGeometry:true))
        {
            Assert.True(context.Entries==null);Assert.Equal(AlgorithmStatus.Success,patch.Bound(new(.2,.3,.2,.8),bounds));
            Assert.Equal(0,context.Profile.BoundCacheHits);
        }
        KernelRuntime.InjectAllocationFailure(-1);
    }

    [Fact]
    public void AMutatingOperationCanProfileWithoutReusingGeometryBounds()
    {
        ModelingPerformance.Context context=default;using var scope=new ModelingPerformance.Scope(&context);
        Assert.True(context.Capture);Assert.True(context.Entries==null);
    }

    [Fact]
    public void ACachedBoundPreservesTheOriginalShortOutputError()
    {
        var program=new NestedBlendProgram(NestedBlendSeriesTests.Surfaces(),NestedBlendSeriesTests.Spines(),NestedBlendSeriesTests.Charts());
        var patch=new SurfaceIntersectionPatch(new SurfaceIntersectionPatch(program,4,new(.2,.4,.1,.9)),200,17,1);var box=new SurfaceParameterBox(.2,.3,.2,.8);
        Span<IntervalNumber> shortOutput=stackalloc IntervalNumber[8],complete=stackalloc IntervalNumber[9];
        var expected=patch.Bound(box,shortOutput);Assert.NotEqual(AlgorithmStatus.Success,expected);
        ModelingPerformance.Context context=default;using var scope=new ModelingPerformance.Scope(&context,immutableGeometry:true);
        Assert.Equal(AlgorithmStatus.Success,patch.Bound(box,complete));Assert.Equal(expected,patch.Bound(box,shortOutput));
    }
}

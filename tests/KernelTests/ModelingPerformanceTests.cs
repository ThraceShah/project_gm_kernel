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
        var box=new SurfaceParameterBox(.2,.4,.3,.7);ModelingPerformance.StoreBound(200,17,1,4,false,box,values,AlgorithmStatus.Success,double.NaN);
        Assert.True(ModelingPerformance.TryBound(200,17,1,4,false,box,output,out _,double.NaN));
        Assert.False(ModelingPerformance.TryBound(201,17,1,4,false,box,output,out _,double.NaN));Assert.False(ModelingPerformance.TryBound(200,18,1,4,false,box,output,out _,double.NaN));
        Assert.False(ModelingPerformance.TryBound(200,17,-1,4,false,box,output,out _,double.NaN));Assert.False(ModelingPerformance.TryBound(200,17,1,5,false,box,output,out _,double.NaN));Assert.False(ModelingPerformance.TryBound(200,17,1,4,true,box,output,out _,double.NaN));
        Assert.False(ModelingPerformance.TryBound(200,17,1,4,false,new(Math.BitIncrement(.2),.4,.3,.7),output,out _,double.NaN));
        Assert.False(ModelingPerformance.TryBound(200,17,1,4,false,new(.2,Math.BitIncrement(.4),.3,.7),output,out _,double.NaN));
        Assert.False(ModelingPerformance.TryBound(200,17,1,4,false,new(.2,.4,Math.BitIncrement(.3),.7),output,out _,double.NaN));
        Assert.False(ModelingPerformance.TryBound(200,17,1,4,false,new(.2,.4,.3,Math.BitIncrement(.7)),output,out _,double.NaN));
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
    public void ScalarSupportCoordinatesCannotAliasSpatialSpineBoundsOrTheOtherSide()
    {
        ModelingPerformance.Context context=default;using var scope=new ModelingPerformance.Scope(&context,immutableGeometry:true);
        Span<IntervalNumber> values=stackalloc IntervalNumber[9],output=stackalloc IntervalNumber[9];for(var i=0;i<9;i++)values[i]=100+i;
        var box=new SurfaceParameterBox(.2,.4,0,0);ModelingPerformance.StoreBound(200,17,1,4,true,box,values,AlgorithmStatus.Success,double.NaN,parameterSide:1);
        Assert.True(ModelingPerformance.TryBound(200,17,1,4,true,box,output,out _,double.NaN,parameterSide:1));
        Assert.False(ModelingPerformance.TryBound(200,17,1,4,true,box,output,out _,double.NaN));
        Assert.False(ModelingPerformance.TryBound(200,17,1,4,true,box,output,out _,double.NaN,parameterSide:2));
        Assert.False(ModelingPerformance.TryBound(200,18,1,4,true,box,output,out _,double.NaN,parameterSide:1));
        for(var i=0;i<9;i++){Assert.Equal(values[i].Lower,output[i].Lower);Assert.Equal(values[i].Upper,output[i].Upper);}
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
    public void ReadOnlyChildProofMergesWorkIntoTheMutatingOperationWithoutSharingItsCache()
    {
        ModelingPerformance.Context operation=default,proof=default;
        using(var outer=new ModelingPerformance.Scope(&operation))
        {
            ModelingPerformance.Cell(root:false);
            using(var inner=new ModelingPerformance.Scope(&proof,immutableGeometry:true))
            {
                Span<IntervalNumber> values=stackalloc IntervalNumber[9];values.Clear();
                Assert.True(proof.Entries==null);ModelingPerformance.StoreBound(200,17,1,4,false,new(0,1,0,1),values,AlgorithmStatus.Success,double.NaN);
                Assert.True(operation.Entries==null);Assert.True(proof.Entries!=null);
                ModelingPerformance.Cell(root:true);ModelingPerformance.Root();ModelingPerformance.BoundRequest();
            }
            Assert.Equal(1,operation.Profile.SsiCells);Assert.Equal(1,operation.Profile.RootCells);
            Assert.Equal(1,operation.Profile.Roots);Assert.Equal(1,operation.Profile.BoundRequests);
            Assert.True(operation.Entries==null);
        }
        Assert.Equal(1,ModelingPerformance.Last.RootCells);Assert.True(ModelingPerformance.Last.ElapsedTicks>=proof.Profile.ElapsedTicks);
    }

    [Fact]
    public void DeterministicAnalyticFailureIsMemoizedWithoutPublishingValuesAndSplineViewsAreRecomputed()
    {
        var z=new KernelVector3{Z=1};var x=new KernelVector3{X=1};var length=Math.Sqrt(.4);
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Sphere,default,z,x,1),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,z,x),NormalSense=1}];
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=2}];
        BlendChartInstruction[] charts=[new(){Position=x,Chord=new(){X=-.2/length,Y=-.6/length},Scale=1},new(){Position=new(){X=.8,Y=-.6},Parameter=length,Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);var domain=new SurfaceParameterBox(0,length,0,0);
        var patch=new SurfaceIntersectionPatch(new SurfaceIntersectionPatch(program,0,0,length),200,17,1);
        ModelingPerformance.Context context=default;using var scope=new ModelingPerformance.Scope(&context,immutableGeometry:true);
        Span<IntervalNumber> output=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.NotConverged,patch.Bound(domain,output));var evaluated=context.Profile.BoundEvaluations;
        output.Fill(new(123));Assert.Equal(AlgorithmStatus.NotConverged,patch.Bound(domain,output));
        Assert.Equal(evaluated,context.Profile.BoundEvaluations);Assert.Equal(1,context.Profile.BoundFailureCacheHits);
        foreach(var value in output){Assert.Equal(123,value.Lower);Assert.Equal(123,value.Upper);}
        // Numerical stopping accuracy depends on every support in a compiler
        // view, even when the added support is outside this root's DAG.
        var scaledProgram=new NestedBlendProgram(new[]{surfaces[0],surfaces[1],new BlendSurfaceInstruction{Analytic=new(SurfaceClass.Sphere,default,z,x,2),NormalSense=1}},spines,charts);
        var scaledPatch=new SurfaceIntersectionPatch(new SurfaceIntersectionPatch(scaledProgram,0,0,length),200,17,1);
        Assert.Equal(AlgorithmStatus.NotConverged,scaledPatch.Bound(domain,output));Assert.Equal(evaluated+1,context.Profile.BoundEvaluations);
        // A compiler view with spline storage does not admit negative entries:
        // available workspace is not part of a geometric bound's key.
        var resourceProgram=new NestedBlendProgram(new[]{surfaces[0],surfaces[1],new BlendSurfaceInstruction{IsBSpline=true,UDegree=1,VDegree=1,UCount=2,VCount=2,Dimension=3}},spines,charts,new double[]{0,0,0,1,0,0,0,1,0,1,1,0,0,0,1,1,0,0,1,1});
        var resourcePatch=new SurfaceIntersectionPatch(new SurfaceIntersectionPatch(resourceProgram,0,0,length),201,17,1);
        Assert.Equal(AlgorithmStatus.NotConverged,resourcePatch.Bound(domain,output));var before=context.Profile.BoundEvaluations;
        Assert.Equal(AlgorithmStatus.NotConverged,resourcePatch.Bound(domain,output));Assert.Equal(before+1,context.Profile.BoundEvaluations);
        Assert.False(ModelingPerformance.TryBound(200,18,1,0,true,domain,output,out _,double.NaN));
    }

    [Fact]
    public void CorrelatedDyadicKeysNeverReturnACollidingValueAndUseTheMeasuredWorkingSetCapacity()
    {
        ModelingPerformance.Context context=default;using var scope=new ModelingPerformance.Scope(&context,immutableGeometry:true);
        Span<IntervalNumber> value=stackalloc IntervalNumber[9],output=stackalloc IntervalNumber[9];
        for(var i=0;i<22000;i++)
        {
            value.Fill(new(i));var box=new SurfaceParameterBox(i/32768.0,(i+1)/32768.0,0,1);
            ModelingPerformance.StoreBound(200,17,1,4,false,box,value,AlgorithmStatus.Success,double.NaN);
        }
        var hits=0;
        for(var i=0;i<22000;i++)
        {
            var box=new SurfaceParameterBox(i/32768.0,(i+1)/32768.0,0,1);
            if(!ModelingPerformance.TryBound(200,17,1,4,false,box,output,out var status,double.NaN))continue;
            hits++;Assert.Equal(AlgorithmStatus.Success,status);foreach(var v in output){Assert.Equal(i,v.Lower);Assert.Equal(i,v.Upper);}
        }
        Assert.InRange(hits,21000,22000);
    }

    [Fact]
    public void MemoryAndUnsupportedStatusesAreNotMemoizedAndFailuresDoNotCrossACommand()
    {
        var box=new SurfaceParameterBox(.2,.4,.3,.7);Span<IntervalNumber> output=stackalloc IntervalNumber[9];
        ModelingPerformance.Context context=default;
        using(var scope=new ModelingPerformance.Scope(&context,immutableGeometry:true))
        {
            foreach(var status in new[]{AlgorithmStatus.WorkspaceTooSmall,AlgorithmStatus.Unsupported,AlgorithmStatus.InvalidInput})
            {
                ModelingPerformance.StoreBound(200,17,1,4,true,box,default,status,double.NaN);
                Assert.False(ModelingPerformance.TryBound(200,17,1,4,true,box,output,out _,double.NaN));
            }
            ModelingPerformance.StoreBound(200,17,1,4,true,box,default,AlgorithmStatus.NotConverged,1);
            Assert.True(ModelingPerformance.TryBound(200,17,1,4,true,box,output,out var failure,1));Assert.Equal(AlgorithmStatus.NotConverged,failure);
            Assert.False(ModelingPerformance.TryBound(200,17,1,4,true,box,output,out _,0));Assert.False(ModelingPerformance.TryBound(200,17,1,4,true,box,output,out _,double.NaN));
        }
        using(var scope=new ModelingPerformance.Scope(&context,immutableGeometry:true))
            Assert.False(ModelingPerformance.TryBound(200,17,1,4,true,box,output,out _,double.NaN));
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

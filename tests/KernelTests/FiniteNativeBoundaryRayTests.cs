using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class FiniteNativeBoundaryRayTests:IDisposable
{
    public FiniteNativeBoundaryRayTests()
    {KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose()=>KernelRuntime.SessionStop();

    [Fact]
    public void CircularSectionRayRetainsAShortTrimAndDoesNotOwnItsEndpoints()
    {
        const double first=.5,last=.5000000000001;
        var query=new PK_UV_s();query.param[0]=1;query.param[1]=(first+last)*.5;
        Assert.Equal(0,KernelRuntime.FiniteSectionRay(2,first,last,true,query,1,0,out var crossing,out var v));
        Assert.Equal(1,crossing);Assert.Equal(query.param[1],v);
        Assert.Equal(0,KernelRuntime.FiniteSectionRay(2,first,last,false,query,1,0,out crossing,out _));
        Assert.Equal(-1,crossing);
        Assert.Equal(0,KernelRuntime.FiniteSectionRay(2,first,last,true,query,-1,0,out crossing,out _));
        Assert.Equal(0,crossing);
        Assert.Equal(0,KernelRuntime.FiniteSectionRay(2,first,last,true,query,0,1,out crossing,out _));
        Assert.Equal(0,crossing);
        query.param[1]=last;
        Assert.Equal(ParasolidConstants.PK_ERROR_eval_failure,KernelRuntime.FiniteSectionRay(2,first,last,true,query,1,0,out _,out _));
        query.param[1]=Math.BitIncrement(last);
        Assert.Equal(0,KernelRuntime.FiniteSectionRay(2,first,last,true,query,1,0,out crossing,out _));
        Assert.Equal(0,crossing);
        query.param[0]=2;
        Assert.Equal(ParasolidConstants.PK_ERROR_eval_failure,KernelRuntime.FiniteSectionRay(2,first,last,true,query,0,1,out _,out _));
    }

    [Fact]
    public void RollingWitnessReturnsToTheSameFiniteBladeStationInBothSupportOrders()
    {
        var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","generated-short-edge-native.x_t"));
        Assert.Equal(0,XtBrepImportTests.Receive(text,out var body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));
        var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        var checkedCurves=0;
        for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var use=KernelRuntime.Edges[edge];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(use.CurveTag)];
            if(curve.Class!=CurveClass.ICurve)continue;
            var data=KernelRuntime.ICurveDataPool[curve.DataIndex];
            var a=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(data.Surface0Tag)].Class;
            var b=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(data.Surface1Tag)].Class;
            if(!(a==SurfaceClass.BlendSurface&&b==SurfaceClass.Plane||a==SurfaceClass.Plane&&b==SurfaceClass.BlendSurface))continue;
            var at=use.ParameterStart+(use.ParameterEnd-use.ParameterStart)*.5;
            Assert.Equal(0,KernelRuntime.TestRoundTripBladeWitness(use.CurveTag,use.ParameterStart,use.ParameterEnd,at,false,out var bounds,out var handled));
            Assert.True(handled);Assert.InRange(at,bounds.Lower,bounds.Upper);
            Assert.Equal(0,KernelRuntime.TestRoundTripBladeWitness(use.CurveTag,use.ParameterStart,use.ParameterEnd,at,true,out var reversed,out handled));
            Assert.True(handled);Assert.Equal(bounds,reversed);
            checkedCurves++;
        }
        Assert.True(checkedCurves>0);
    }

    [Fact]
    public void NativeChartJunctionKeepsTheCompleteBranchWitnessAndRejectsAReversedOrDistantChart()
    {
        var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","generated-short-edge-native.x_t"));
        Assert.Equal(0,XtBrepImportTests.Receive(text,out var body));Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));
        Span<IntervalNumber> witness=stackalloc IntervalNumber[7],bounds=stackalloc IntervalNumber[9];
        var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var curve=KernelRuntime.GetCurveByTag(KernelRuntime.Edges[edge].CurveTag);if(curve.Class!=CurveClass.ICurve)continue;
            var data=KernelRuntime.ICurveDataPool[curve.DataIndex];if(data.ProgramBlock<=0||data.IsBlendContact!=0)continue;
            var source=KernelRuntime.GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
            var spine=data.ProgramSpine;var definition=source.Spines[spine];var frames=source.Charts.Slice(definition.ChartOffset,definition.ChartCount);
            if(frames.Length<3)continue;var at=frames[1].Parameter;
            if(JointNestedBlendEvaluation.BoundSharedSpineWitness(source,spine,new(at,at,0,0),bounds,capture:witness)!=AlgorithmStatus.Success)continue;
            for(var i=0;i<3;i++)witness[i]+=new IntervalNumber(-1e-12,1e-12);
            var charts=source.Charts.ToArray();var target=new NestedBlendProgram(source.Surfaces,source.Spines,charts,source.Scalars,source.Workspace);
            Assert.Equal(0,KernelRuntime.TestLocateSharedChart(target,spine,frames[0].Parameter,frames[2].Parameter,witness,out var located));
            Assert.InRange(at,located.Lower,located.Upper);
            charts[definition.ChartOffset+1].Position.X+=1e-5;
            Assert.NotEqual(0,KernelRuntime.TestLocateSharedChart(target,spine,frames[0].Parameter,frames[2].Parameter,witness,out _));
            charts[definition.ChartOffset+1]=frames[1];charts[definition.ChartOffset+1].Scale=-frames[1].Scale;
            Assert.NotEqual(0,KernelRuntime.TestLocateSharedChart(target,spine,frames[0].Parameter,frames[2].Parameter,witness,out _));
            return;
        }
        Assert.Fail("No procedural rolling CHART junction with a complete seven-variable witness");
    }

    [Fact]
    public void AdjacentGaugesShareAnOrientedRollingRootButAReversedGaugeDoesNot()
    {
        var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","generated-short-edge-native.x_t"));
        Assert.Equal(0,XtBrepImportTests.Receive(text,out var body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));
        var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        Span<IntervalNumber> witness=stackalloc IntervalNumber[7],bounds=stackalloc IntervalNumber[9];
        Span<double> contacts=stackalloc double[4];
        for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var use=KernelRuntime.Edges[edge];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(use.CurveTag)];
            if(curve.Class!=CurveClass.ICurve)continue;
            var data=KernelRuntime.ICurveDataPool[curve.DataIndex];
            var a=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(data.Surface0Tag)].Class;
            var b=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(data.Surface1Tag)].Class;
            if(!(a==SurfaceClass.BlendSurface&&b==SurfaceClass.Plane||a==SurfaceClass.Plane&&b==SurfaceClass.BlendSurface))continue;
            var source=KernelRuntime.GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
            var spine=data.ProgramSpine;var definition=source.Spines[spine];
            var frames=source.Charts.Slice(definition.ChartOffset,definition.ChartCount);
            var first=Math.Max(frames[0].Parameter,use.ParameterStart);var last=Math.Min(frames[1].Parameter,use.ParameterEnd);
            if(!(last>first))continue;
            var at=(first+last)*.5;
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSharedSpineWitness(source,spine,new(at,at,0,0),bounds,capture:witness));
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpineAnchor(source,spine,at,bounds,contacts));
            var point=Vector(witness[0].Midpoint,witness[1].Midpoint,witness[2].Midpoint);
            var instructions=source.Spines.ToArray();var chartOffset=source.Charts.Length;
            instructions[spine].ChartOffset=chartOffset;instructions[spine].ChartCount=3;
            // Two different affine station maps for the same physical tube.
            var allCharts=new BlendChartInstruction[chartOffset+3];source.Charts.CopyTo(allCharts);var charts=allCharts.AsSpan(chartOffset);
            charts[0]=charts[1]=frames[0];charts[2]=frames[1];
            var delta=1e-11;var displacement=Scale(charts[0].Chord,delta/(charts[0].Scale*Dot(charts[0].Chord,charts[0].Chord)));
            charts[0].Position=Add(charts[0].Position,displacement);
            charts[1].Parameter=at;charts[1].Position=Sub(point,displacement);
            charts[1].FirstU=contacts[0];charts[1].FirstV=contacts[1];charts[1].SecondU=contacts[2];charts[1].SecondV=contacts[3];
            var target=new NestedBlendProgram(source.Surfaces,instructions,allCharts,source.Scalars,source.Workspace);
            Assert.Equal(0,KernelRuntime.TestLocateSharedChart(target,spine,first,last,witness,out var both));
            Assert.True(both.Upper<at);
            Assert.Equal(0,KernelRuntime.TestLocateSharedChart(target,spine,at,last,witness,out var right));
            Assert.True(right.Lower>at);
            charts[1].Scale=-charts[1].Scale;charts[1].Position=Add(point,displacement);
            Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,KernelRuntime.TestLocateSharedChart(target,spine,first,last,witness,out _));
            return;
        }
        Assert.Fail("No finite Blend/plane chart for the witness test");
    }

    // Unmodified historical probe rows 56, 58, 68 and 109. Independent PK
    // classifies each as on the finite product; untrimmed support SSI failed.
    [Theory]
    [InlineData(-1.5000000199996693,-2.0000000761480714,4.999999999999994)]
    [InlineData(-1.5000000999996692,-2.0000000761480714,4.999999999999994)]
    [InlineData(-1.500000009999669,-2.0000000761480714,4.999999999999994)]
    [InlineData(-1.5000000169520076,-2.000038073674117,4.999999998550392)]
    public void FiniteNativeBoundaryKeepsItsBranchInMaterialClassification(double x,double y,double z)
    {
        var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","generated-short-edge-native.x_t"));
        Assert.Equal(0,XtBrepImportTests.Receive(text,out var body));var query=new PK_VECTOR_s();query.coord[0]=x;query.coord[1]=y;query.coord[2]=z;
        int enclosure,topology;var error=KernelRuntime.BodyContainsVector(body,query,&enclosure,&topology);
        Assert.True(error==0,$"error={error} stage={KernelRuntime.LastRegularClassificationStage} entity={KernelRuntime.LastRegularClassificationEntity} curve={KernelRuntime.LastSheetCurve}");
        Assert.Equal(ParasolidConstants.PK_enclosure_on_c,enclosure);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }
}

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

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

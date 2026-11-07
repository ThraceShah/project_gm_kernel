using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class FiniteNativeBoundaryRayTests:IDisposable
{
    public FiniteNativeBoundaryRayTests()
    {KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose()=>KernelRuntime.SessionStop();

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

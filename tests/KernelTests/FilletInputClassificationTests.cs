using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class FilletInputClassificationTests:IDisposable
{
    public FilletInputClassificationTests(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose(){KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(.05,.98,ParasolidConstants.PK_enclosure_inside_c)]
    [InlineData(.05,1.01,ParasolidConstants.PK_enclosure_inside_c)]
    [InlineData(.04,.9,ParasolidConstants.PK_enclosure_on_c)]
    [InlineData(.02,.9,ParasolidConstants.PK_enclosure_outside_c)]
    public void ReloadedAndCutFilletClassifiesMaterialNearItsProceduralBoundary(double y,double z,int expected)
    {
        var body=Input();var point=new PK_VECTOR_s();point.coord[0]=.5;point.coord[1]=y;point.coord[2]=z;int enclosure,topology;
        var error=KernelRuntime.BodyContainsVector(body,point,&enclosure,&topology);
        Assert.True(error==0,$"error={error}, stage={KernelRuntime.LastRegularClassificationStage}, entity={KernelRuntime.LastRegularClassificationEntity}");Assert.Equal(expected,enclosure);
    }

    private static int Input()
    {
        var body=RegularSolidBlendTests.Roof();var edge=RegularSolidBlendTests.SelectedEdge(body);var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count,fault,fe,ft;
        Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.1,&set,&count,null));var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};
        Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&fe,&ft));
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out var received));Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));body=received;
        var tool=BooleanOperationTests.Block(.5,-.46,.7,2,1,1);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int* products;
        Assert.Equal(0,KernelRuntime.BodyBoolean(body,1,&tool,&options,&count,&products));try{Assert.Equal(1,count);Assert.Equal(body,products[0]);}finally{KernelRuntime.MemoryFree(products);}
        return body;
    }
}

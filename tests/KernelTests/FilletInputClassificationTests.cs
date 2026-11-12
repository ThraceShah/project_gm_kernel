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

    [Theory]
    [InlineData(100)] [InlineData(200)]
    public void ContactCurveUvProximityDoesNotOverrideFiniteSpatialTopology(double scale)
    {
        var body=RegularSolidBlendTests.Roof();var edge=RegularSolidBlendTests.SelectedEdge(body);var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count,fault,fe,ft;
        Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.1,&set,&count,null));var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};
        Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&fe,&ft));
        var form=new PK_TRANSF_sf_s();form.matrix[0]=form.matrix[5]=form.matrix[10]=scale;form.matrix[15]=1;int transform;
        Assert.Equal(0,KernelRuntime.TransfCreate(&form,&transform));int* replaced;byte* exact;
        Assert.Equal(0,KernelRuntime.BodyTransform(body,transform,0,&count,&replaced,&exact));if(replaced!=null)KernelRuntime.MemoryFree(replaced);if(exact!=null)KernelRuntime.MemoryFree(exact);
        var record=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];var current=record.FirstEdgeBody;var tested=0;
        for(var i=0;i<record.EdgeCountBody;i++,current=KernelRuntime.Edges[current].NextInBody)
        {
            var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[current].CurveTag)];
            if(curve.Class!=CurveClass.ICurve)continue;var contact=KernelRuntime.ICurveDataPool[curve.DataIndex];if(contact.IsBlendContact==0)continue;
            var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(contact.Surface1Tag)];var data=KernelRuntime.BlendedEdgeDataPool[surface.DataIndex];
            var root=KernelRuntime.GetNestedBlendProgram(data).Surfaces[data.ProgramRoot];var side=root.ReverseSection?contact.BlendContactSide:1-contact.BlendContactSide;
            var uv=new PK_UV_s();uv.param[0]=(surface.UMin+surface.UMax)*.5;uv.param[1]=side;var boundary=new PK_VECTOR_s();var query=new PK_VECTOR_s();
            Assert.Equal(0,KernelRuntime.SurfEval(surface.Header.Tag,uv,0,0,0,&boundary));uv.param[1]=side==0?5e-9:1-5e-9;
            Assert.Equal(0,KernelRuntime.SurfEval(surface.Header.Tag,uv,0,0,0,&query));double squared=0;
            for(var d=0;d<3;d++)squared+=(query.coord[d]-boundary.coord[d])*(query.coord[d]-boundary.coord[d]);Assert.True(squared>4e-16);
            int enclosure,topology,kind;Assert.Equal(0,KernelRuntime.BodyContainsVector(body,query,&enclosure,&topology));
            Assert.Equal(ParasolidConstants.PK_enclosure_on_c,enclosure);Assert.Equal(0,KernelRuntime.EntityAskClass(topology,&kind));Assert.Equal(ParasolidConstants.PK_CLASS_face,kind);tested++;
        }
        Assert.True(tested>0);
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

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class MixedBlendBooleanTests:IDisposable
{
    public MixedBlendBooleanTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Fact]
    public void AsymmetricPlaneCutRetainsRegularBranchesThroughChartCorners()
    {
        var body=RegularSolidBlendTests.Roof();Fillet(body,RegularSolidBlendTests.SelectedEdge(body),.1);
        var tool=BooleanOperationTests.Block(1.05,.5,-.1,1,2,2);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int count;int* products;
        var error=KernelRuntime.BodyBoolean(body,1,&tool,&options,&count,&products);
        Assert.True(error==0,$"cut={error}, stage={KernelRuntime.LastRegularBooleanStage}, pair={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace}");
        try{Assert.Equal(1,count);Assert.Equal(body,products[0]);BooleanOperationTests.Counts(body,7,15,10);}finally{KernelRuntime.MemoryFree(products);}
    }

    [Fact]
    public void BooleanNewbornCurvedEdgeCanBeFilletedWithAnExistingBlendRetained()
    {
        var body=NotchedRoundedRoof(.6,out var selected);Fillet(body,selected,.05);
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var face=KernelRuntime.Bodies[slot].FirstFaceBody;var blends=0;
        for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
            if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag)].Class==SurfaceClass.BlendSurface)blends++;
        Assert.Equal(2,blends);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void SecondFilletOverlappingAnExistingBlendRestoresTheOriginalSolid()
    {
        var body=NotchedRoundedRoof(.25,out var edge);Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out var before));
        var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count;
        Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.2,&set,&count,null));
        var blocks=KernelRuntime.BlocksLiveBytes;var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,fe,ft;int* products;
        Assert.NotEqual(0,KernelRuntime.BodyFixBlends(body,&fix,&count,&products,null,null,&fault,&fe,&ft));Assert.Equal(0,count);Assert.True(products==null);
        Assert.True(KernelRuntime.IsValidTag(edge));Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out var after));Assert.Equal(before,after);
        Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
    }

    private static int NotchedRoundedRoof(double y,out int selected)
    {
        var body=RegularSolidBlendTests.Roof();var original=RegularSolidBlendTests.SelectedEdge(body);Fillet(body,original,.1);
        var tool=BooleanOperationTests.Block(.5,y+.5,.7,2,1,1);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int count;int* products;
        var error=KernelRuntime.BodyBoolean(body,1,&tool,&options,&count,&products);
        Assert.True(error==0,$"cut={error}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, sheet={KernelRuntime.LastSheetPartitionStage}");
        try{Assert.Equal(1,count);Assert.Equal(body,products[0]);}finally{KernelRuntime.MemoryFree(products);}
        KernelRuntime.TryResolveBodySlot(body,out var slot);var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;selected=0;
        for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var record=KernelRuntime.Edges[edge];if(record.StartVertex<0||record.EndVertex<0)continue;
            var a=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[record.StartVertex].PointTag)].Position;
            var b=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[record.EndVertex].PointTag)].Position;
            if(Math.Abs(a.Y-y)+Math.Abs(b.Y-y)+Math.Abs(a.Z-1)+Math.Abs(b.Z-1)<1e-9)selected=record.Header.Tag;
        }
        Assert.NotEqual(0,selected);return body;
    }

    private static void Fillet(int body,int edge,double radius)
    {
        var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count;
        Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,radius,&set,&count,null));
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,fe,ft;
        var error=KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&fe,&ft);
        Assert.True(error==0,$"fix={error}, stage={KernelRuntime.LastRegularBlendStage}");Assert.Equal(1,count);
    }
}

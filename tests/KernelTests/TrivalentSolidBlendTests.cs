using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class TrivalentSolidBlendTests:IDisposable
{
    public TrivalentSolidBlendTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Fact]
    public void ThreeSelectedNurbsStarEdgesAreReplacedTogetherByThreeBandsAndSphere()
    {
        var body=RegularSolidBlendTests.Roof(.4);Span<int> selected=stackalloc int[3];Select(body,selected);Mark(selected,.1);
        var error=Fix(body);Assert.True(error==0,$"corner={error}, stage={KernelRuntime.LastBlendCornerStage}, event={KernelRuntime.LastBlendCornerEvent}, cert={KernelRuntime.LastCornerCertificate}");
        BooleanOperationTests.Counts(body,10,21,13);int n;int* faces;Assert.Equal(0,KernelRuntime.BodyAskFaces(body,&n,&faces));var sphere=0;var blend=0;
        try{for(var f=0;f<n;f++){int surface,type;Assert.Equal(0,KernelRuntime.FaceAskSurf(faces[f],&surface));Assert.Equal(0,KernelRuntime.EntityAskClass(surface,&type));if(type==ParasolidConstants.PK_CLASS_sphere)sphere++;if(type==ParasolidConstants.PK_CLASS_blendsf)blend++;}}finally{KernelRuntime.MemoryFree(faces);}
        Assert.Equal(1,sphere);Assert.Equal(2,blend);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }
    [Theory]
    [InlineData(0)] [InlineData(3)] [InlineData(10)] [InlineData(20)] [InlineData(40)] [InlineData(80)]
    public void FailedCornerConstructionRestoresAllThreeMarksAndGeometryPools(int budget)
    {
        var body=RegularSolidBlendTests.Roof(.4);Span<int> selected=stackalloc int[3];Select(body,selected);Mark(selected,.1);
        var nc=KernelRuntime.Curves.AliveCount;var ns=KernelRuntime.Surfaces.AliveCount;var nb=KernelRuntime.Bodies.AliveCount;var blocks=KernelRuntime.BlocksLiveBytes;
        KernelRuntime.InjectAllocationFailure(budget);int error;try{error=Fix(body);}finally{KernelRuntime.InjectAllocationFailure(-1);}
        if(error==0){BooleanOperationTests.Counts(body,10,21,13);return;}
        BooleanOperationTests.Counts(body,6,12,8);foreach(var tag in selected){Assert.True(KernelRuntime.IsValidTag(tag));Assert.Equal(1,KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(tag).Slot].PendingBlendKind);}
        Assert.Equal(nc,KernelRuntime.Curves.AliveCount);Assert.Equal(ns,KernelRuntime.Surfaces.AliveCount);Assert.Equal(nb,KernelRuntime.Bodies.AliveCount);Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);
    }
    [Fact]
    public void UnequalStarRadiiFailBeforeAnyConstructedGeometryIsPublished()
    {
        var body=RegularSolidBlendTests.Roof(.4);Span<int> selected=stackalloc int[3];Select(body,selected);Mark(selected,.1);Mark(selected[..1],.2);
        var nc=KernelRuntime.Curves.AliveCount;var ns=KernelRuntime.Surfaces.AliveCount;Assert.NotEqual(0,Fix(body));
        Assert.Equal(KernelRuntime.BlendCornerEvent.UnequalRadius,KernelRuntime.LastBlendCornerEvent);BooleanOperationTests.Counts(body,6,12,8);Assert.Equal(nc,KernelRuntime.Curves.AliveCount);Assert.Equal(ns,KernelRuntime.Surfaces.AliveCount);
    }
    private static void Mark(Span<int> selected,double radius)
    {
        var options=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int n;fixed(int* tags=selected)Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(selected.Length,tags,radius,&options,&n,null));Assert.Equal(selected.Length,n);
    }
    private static int Fix(int body)
    {
        var options=new PK_BODY_fix_blends_o_s{o_t_version=31,update=ParasolidConstants.PK_blend_edge_update_v261_c,tolerance=1e-7};int count,fault,edge,topol;
        var error=KernelRuntime.BodyFixBlends(body,&options,&count,null,null,null,&fault,&edge,&topol);Assert.Equal(error==0?4:0,count);return error;
    }
    private static void Select(int body,Span<int> selected)
    {
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var bs));var vertex=KernelRuntime.Bodies[bs].FirstVertexBody;var corner=-1;
        for(var i=0;i<KernelRuntime.Bodies[bs].VertexCountBody;i++,vertex=KernelRuntime.Vertices[vertex].NextInBody)
        {
            var p=KernelRuntime.Points[KernelRuntime.ResolveTagRecord(KernelRuntime.Vertices[vertex].PointTag).Slot].Position;if(Math.Abs(p.X)+Math.Abs(p.Y)+Math.Abs(p.Z-1)<1e-10)corner=vertex;
        }
        Assert.True(corner>=0);var count=0;var edge=KernelRuntime.Bodies[bs].FirstEdgeBody;
        for(var e=0;e<KernelRuntime.Bodies[bs].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)if(KernelRuntime.Edges[edge].StartVertex==corner||KernelRuntime.Edges[edge].EndVertex==corner){Assert.True(count<3);selected[count++]=KernelRuntime.Edges[edge].Header.Tag;}
        Assert.Equal(3,count);
    }
}

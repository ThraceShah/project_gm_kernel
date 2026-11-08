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

    [Theory]
    [InlineData(.3,.35,.06,false)]
    [InlineData(.05,.96,.035,true)]
    public void BoredCornerPreservesRemoteRingsAndRejectsAnUnresolvedCapInteraction(double x,double z,double boreRadius,bool intersects)
    {
        var body=RegularSolidBlendTests.Roof(.4);
        var axis=new PK_AXIS2_sf_s();axis.location.coord[0]=x;axis.location.coord[1]=-1;axis.location.coord[2]=z;
        axis.axis.coord[1]=1;axis.ref_direction.coord[0]=1;int tool;
        Assert.Equal(0,KernelRuntime.BodyCreateSolidCyl(boreRadius,3,&axis,&tool));
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int n;int* output;
        Assert.Equal(0,KernelRuntime.BodyBoolean(body,1,&tool,&options,&n,&output));Assert.Equal(1,n);body=output[0];KernelRuntime.MemoryFree(output);
        Span<int> selected=stackalloc int[3];Select(body,selected);Mark(selected,.1);
        var caps=PlanarBoreFaces(body);var bodySlot=KernelRuntime.ResolveTagRecord(body).Slot;var before=KernelRuntime.Bodies[bodySlot];
        var marks=selected.ToArray().Select(tag=>KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(tag).Slot]).ToArray();
        var pools=(KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Bodies.AliveCount,KernelRuntime.BlocksLiveBytes);
        var error=Fix(body);
        if(intersects)
        {
            Assert.Equal(ParasolidConstants.PK_ERROR_cant_do_intersect,error);
            Assert.Equal(KernelRuntime.BlendCornerEvent.UncertifiedClearance,KernelRuntime.LastBlendCornerEvent);
            Assert.Equal(before,KernelRuntime.Bodies[bodySlot]);Assert.Equal(caps,PlanarBoreFaces(body));
            for(var i=0;i<selected.Length;i++)
            {
                Assert.True(KernelRuntime.IsValidTag(selected[i]));
                Assert.Equal(marks[i],KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(selected[i]).Slot]);
            }
            Assert.Equal(pools,(KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Bodies.AliveCount,KernelRuntime.BlocksLiveBytes));
        }
        else
        {
            Assert.True(error==0,$"corner={error}, stage={KernelRuntime.LastBlendCornerStage}/{KernelRuntime.LastBlendCornerEvent}, cert={KernelRuntime.LastCornerCertificate}");
            Assert.Equal(caps,PlanarBoreFaces(body));
        }
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);
    }

    private static (int near,int far) PlanarBoreFaces(int body)
    {
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var bs));var face=KernelRuntime.Bodies[bs].FirstFaceBody;var near=0;var far=0;
        for(var f=0;f<KernelRuntime.Bodies[bs].FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var record=KernelRuntime.Faces[face];if(record.LoopCount!=2||KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(record.SurfTag)].Class!=SurfaceClass.Plane)continue;
            PK_PLANE_sf_s plane;Assert.Equal(0,KernelRuntime.PlaneAsk(record.SurfTag,&plane));
            if(Math.Abs(plane.basis_set.axis.coord[1])<.99)continue;
            if(plane.basis_set.location.coord[1]<.5)near=record.Header.Tag;else far=record.Header.Tag;
        }
        Assert.NotEqual(0,near);Assert.NotEqual(0,far);return(near,far);
    }

    [Theory]
    [InlineData(2,2,7,14,9,3)]
    [InlineData(1,2,5,9,6,2)]
    [InlineData(1,1,4,6,4,1)]
    public void ZeroBandEventsRemoveOnlyCertifiedCollapsedBandsAndSupportFaces(double depth,double height,int faces,int edges,int vertices,int products)
    {
        var body=BooleanOperationTests.Block(.5,depth*.5,0,1,depth,height);Span<int> selected=stackalloc int[3];Select(body,selected,height);Mark(selected,1);
        var options=new PK_BODY_fix_blends_o_s{o_t_version=31,update=ParasolidConstants.PK_blend_edge_update_v261_c,tolerance=1e-7};int count,fault,edge,topol;
        var error=KernelRuntime.BodyFixBlends(body,&options,&count,null,null,null,&fault,&edge,&topol);
        Assert.True(error==0,$"event={error}, stage={KernelRuntime.LastBlendCornerStage}/{KernelRuntime.LastBlendCornerEvent}, cert={KernelRuntime.LastCornerCertificate}/{KernelRuntime.LastCornerCertificateBand}/{KernelRuntime.LastCornerCertificateFace}");
        Assert.Equal(products,count);BooleanOperationTests.Counts(body,faces,edges,vertices);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);
    }
    [Theory]
    [InlineData(.75)] [InlineData(.999999)]
    public void PositiveBandsApproachTheExtinctionStationWithoutWholeBallFalseRejection(double radius)
    {
        var body=BooleanOperationTests.Block(.5,.5,0,1,1,1);Span<int> selected=stackalloc int[3];Select(body,selected);Mark(selected,radius);
        var error=Fix(body);Assert.True(error==0,$"error={error},stage={KernelRuntime.LastBlendCornerStage},event={KernelRuntime.LastBlendCornerEvent},cert={KernelRuntime.LastCornerCertificate}");BooleanOperationTests.Counts(body,10,21,13);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);
    }
    [Theory]
    [InlineData(5)] [InlineData(35)] [InlineData(80)]
    public void ZeroBandEventAllocationFailureRestoresOriginalFaceGraphAndMarks(int budget)
    {
        var body=BooleanOperationTests.Block(.5,.5,0,1,1,1);Span<int> selected=stackalloc int[3];Select(body,selected);Mark(selected,1);
        var nc=KernelRuntime.Curves.AliveCount;var ns=KernelRuntime.Surfaces.AliveCount;var blocks=KernelRuntime.BlocksLiveBytes;
        var options=new PK_BODY_fix_blends_o_s{o_t_version=31,update=ParasolidConstants.PK_blend_edge_update_v261_c,tolerance=1e-7};int count,fault,edge,topol;int error;
        KernelRuntime.InjectAllocationFailure(budget);try{error=KernelRuntime.BodyFixBlends(body,&options,&count,null,null,null,&fault,&edge,&topol);}finally{KernelRuntime.InjectAllocationFailure(-1);}
        if(error==0){BooleanOperationTests.Counts(body,4,6,4);Assert.Equal(1,count);return;}
        BooleanOperationTests.Counts(body,6,12,8);foreach(var tag in selected){Assert.True(KernelRuntime.IsValidTag(tag));Assert.Equal(1,KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(tag).Slot].PendingBlendKind);}
        Assert.Equal(nc,KernelRuntime.Curves.AliveCount);Assert.Equal(ns,KernelRuntime.Surfaces.AliveCount);Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);
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
    private static void Select(int body,Span<int> selected,double height=1)
    {
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var bs));var vertex=KernelRuntime.Bodies[bs].FirstVertexBody;var corner=-1;
        for(var i=0;i<KernelRuntime.Bodies[bs].VertexCountBody;i++,vertex=KernelRuntime.Vertices[vertex].NextInBody)
        {
            var p=KernelRuntime.Points[KernelRuntime.ResolveTagRecord(KernelRuntime.Vertices[vertex].PointTag).Slot].Position;if(Math.Abs(p.X)+Math.Abs(p.Y)+Math.Abs(p.Z-height)<1e-10)corner=vertex;
        }
        Assert.True(corner>=0);var count=0;var edge=KernelRuntime.Bodies[bs].FirstEdgeBody;
        for(var e=0;e<KernelRuntime.Bodies[bs].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)if(KernelRuntime.Edges[edge].StartVertex==corner||KernelRuntime.Edges[edge].EndVertex==corner){Assert.True(count<3);selected[count++]=KernelRuntime.Edges[edge].Header.Tag;}
        Assert.Equal(3,count);
    }
}

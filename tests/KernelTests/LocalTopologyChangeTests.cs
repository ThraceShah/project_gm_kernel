using System.Text;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class LocalTopologyChangeTests:IDisposable
{
    public LocalTopologyChangeTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(5,-.3,10,4,0,6)] [InlineData(5,.3,10,4,0,6)]
    [InlineData(21,-.3,9,3,0,6)] [InlineData(21,.3,9,3,0,6)]
    [InlineData(7,-.3,10,3,1,6)] [InlineData(7,.3,10,3,1,6)]
    [InlineData(23,-.3,8,2,0,6)] [InlineData(23,.3,8,2,0,6)]
    [InlineData(31,-.3,6,1,0,5)] [InlineData(31,.3,6,1,0,5)]
    public void AdjacentPierceFacesOpenSplitAndDisappear(int mask,double distance,int faceCount,int creates,int derives,int survivors)
    {
        var baseline=Counts();var body=Block();var original=Faces(body);var oldPlanes=original.ToDictionary(x=>x,Plane);
        var oldEdges=Boundary(body,false);var oldVertices=Boundary(body,true);var selected=Select(body,mask);
        var options=new PK_BODY_hollow_o_s{o_t_version=12};var track=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();
        fixed(int* p=selected){options.n_pierce_faces=selected.Length;options.pierce_faces=p;Assert.Equal(0,KernelRuntime.BodyHollow2(body,distance,1e-7,&options,&track,&local));}
        Assert.Equal(ParasolidConstants.PK_local_status_ok_c,local.status);Assert.Equal(faceCount,Faces(body).Length);
        var after=Faces(body);Assert.Equal(survivors,original.Count(after.Contains));Assert.Equal(creates+derives,track.n_track_records);
        foreach(var tag in original.Where(after.Contains))
        {
            var before=oldPlanes[tag];var now=Plane(tag);Assert.Equal(before.Normal,now.Normal);
            Assert.Equal(before.Distance+(distance>0&&!selected.Contains(tag)?distance:0),now.Distance,10);
        }
        var actualCreates=0;var actualDerives=0;
        for(var i=0;i<track.n_track_records;i++)
        {
            var r=track.track_records[i];Assert.Equal(1,r.n_original_topols);Assert.Equal(1,r.n_product_topols);
            Assert.Contains(r.original_topols[0],original);Assert.Contains(r.product_topols[0],after);Assert.DoesNotContain(r.product_topols[0],original);
            Assert.Equal(ParasolidConstants.PK_CLASS_face,r.original_classes[0]);
            if(r.track==ParasolidConstants.PK_TOPOL_track_create_c){actualCreates++;Assert.DoesNotContain(r.original_topols[0],selected);}
            else{Assert.Equal(ParasolidConstants.PK_TOPOL_track_derive_c,r.track);actualDerives++;Assert.Contains(r.original_topols[0],selected);}
        }
        var afterEdges=Boundary(body,false);var afterVertices=Boundary(body,true);
        Assert.Equal(mask==31?8:mask==23?11:12,oldEdges.Count(afterEdges.Contains));
        Assert.Equal(mask==31?4:mask==23?6:mask==21?7:8,oldVertices.Count(afterVertices.Contains));
        Assert.Equal(creates,actualCreates);Assert.Equal(derives,actualDerives);Free(&track,&local);Check(body);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(baseline,Counts());
    }

    [Theory]
    [InlineData(-.3)] [InlineData(.3)]
    public void DisconnectedHollowRejectsWithNativeNotUniqueAndRestoresAllState(double distance)
    {
        var body=Block();var selected=Select(body,15);var before=Transmit(body);var counts=Counts();var returns=KernelRuntime.ReturnOutstandingBlocks;
        var options=new PK_BODY_hollow_o_s{o_t_version=12};var track=new PK_TOPOL_track_r_s{n_track_records=19};var local=new PK_TOPOL_local_r_s();
        fixed(int* p=selected){options.n_pierce_faces=selected.Length;options.pierce_faces=p;Assert.Equal(ParasolidConstants.PK_ERROR_not_unique,KernelRuntime.BodyHollow2(body,distance,1e-7,&options,&track,&local));}
        Assert.Equal(before,Transmit(body));Assert.Equal(counts,Counts());Assert.Equal(returns,KernelRuntime.ReturnOutstandingBlocks);
        Assert.Equal(0,track.n_track_records);Assert.True(track.track_records==null);Assert.Equal(0,local.status);Check(body);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OffsetCanEliminateAConvexSupportFace(bool bodyOffset)
    {
        var body=Block();var chamfer=Chamfer(body);var original=Faces(body);var track=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();
        if(bodyOffset){var options=new PK_BODY_offset_o_s{o_t_version=12};Assert.Equal(0,KernelRuntime.BodyOffset2(body,-1,1e-7,&options,&track,&local));}
        else{var options=new PK_FACE_offset_o_s{o_t_version=11};double amount=2;Assert.Equal(0,KernelRuntime.FaceOffset2(1,&chamfer,&amount,1e-7,&options,&track,&local));}
        var after=Faces(body);var removed=chamfer;Assert.Equal(6,after.Length);Assert.DoesNotContain(chamfer,after);Assert.Equal(original.Where(x=>x!=removed).Order(),after.Order());
        Assert.Equal(0,track.n_track_records);Assert.Equal(ParasolidConstants.PK_local_status_ok_c,local.status);Free(&track,&local);Check(body);
        Assert.Equal(0,KernelRuntime.BodyOffset(body,.1,1e-7,1));Assert.Equal(after.Order(),Faces(body).Order());Check(body);
    }

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(50)] [InlineData(200)]
    public void TopologyHollowFailureInjectionAndMarkUndoRestoreInput(int budget)
    {
        var body=Block();var selected=Select(body,7);var before=Transmit(body);var counts=Counts();
        int mark=0;Assert.Equal(0,KernelRuntime.MarkCreate(&mark));
        var options=new PK_BODY_hollow_o_s{o_t_version=12};var track=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();int error;
        fixed(int* p=selected)
        {
            options.n_pierce_faces=selected.Length;options.pierce_faces=p;KernelRuntime.InjectAllocationFailure(budget);
            error=KernelRuntime.BodyHollow2(body,-.3,1e-7,&options,&track,&local);KernelRuntime.InjectAllocationFailure(-1);
        }
        if(error==0){Free(&track,&local);Check(body);Assert.Equal(0,KernelRuntime.MarkGoto(mark));}
        else{Assert.Equal(0,track.n_track_records);Assert.True(track.track_records==null);}
        Assert.Equal(counts,Counts());Assert.Equal(before,Transmit(body));Check(body);
    }

    [Fact]
    public void ZeroFaceOffsetsUseTheSameAdjacentPierceTopology()
    {
        var body=Block();var selected=Select(body,5);var zeros=new double[selected.Length];
        var options=new PK_BODY_hollow_o_s{o_t_version=12};var track=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();
        fixed(int* p=selected)fixed(double* d=zeros)
        {options.n_offset_faces=selected.Length;options.offset_faces=p;options.offset_values=d;Assert.Equal(0,KernelRuntime.BodyHollow2(body,-.3,1e-7,&options,&track,&local));}
        Assert.Equal(10,Faces(body).Length);Assert.Equal(4,track.n_track_records);Free(&track,&local);Check(body);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void PositiveAnalyticHollowRetainsOriginalOuterFacesAndTracksNewInnerWalls(int shape)
    {
        int body=0;Assert.Equal(0,shape switch
        {
            0=>KernelRuntime.BodyCreateSolidSphere(3,null,&body),1=>KernelRuntime.BodyCreateSolidTorus(5,1,null,&body),
            2=>KernelRuntime.BodyCreateSolidCyl(3,6,null,&body),_=>KernelRuntime.BodyCreateSolidCone(3,6,.15,null,&body)
        });
        var originals=Faces(body);var senses=originals.ToDictionary(x=>x,Orientation);
        var options=new PK_BODY_hollow_o_s{o_t_version=12};var tracking=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();
        Assert.Equal(0,KernelRuntime.BodyHollow2(body,.3,1e-7,&options,&tracking,&local));
        Assert.Equal(originals.Length,tracking.n_track_records);
        foreach(var face in originals)Assert.Equal(senses[face],Orientation(face));
        for(var i=0;i<tracking.n_track_records;i++)
        {
            var record=tracking.track_records[i];Assert.Equal(ParasolidConstants.PK_TOPOL_track_create_c,record.track);
            Assert.Equal(1,record.n_original_topols);Assert.Equal(1,record.n_product_topols);
            Assert.Equal((byte)(1-senses[record.original_topols[0]]),Orientation(record.product_topols[0]));
        }
        Free(&tracking,&local);
    }

    private static int Block(){int b=0;Assert.Equal(0,KernelRuntime.BodyCreateSolidBlock(4,6,8,null,&b));return b;}
    private static int[] Faces(int body)
    {
        int n=0;int* faces=null;Assert.Equal(0,KernelRuntime.BodyAskFaces(body,&n,&faces));
        try{return new ReadOnlySpan<int>(faces,n).ToArray();}finally{KernelRuntime.MemoryFree(faces);}
    }
    private static int[] Boundary(int body,bool vertices)
    {
        int n=0;int* items=null;Assert.Equal(0,vertices?KernelRuntime.BodyAskVertices(body,&n,&items):KernelRuntime.BodyAskEdges(body,&n,&items));
        try{return new ReadOnlySpan<int>(items,n).ToArray();}finally{KernelRuntime.MemoryFree(items);}
    }
    private static byte Orientation(int face)
    {int surface=0;byte sense=0;Assert.Equal(0,KernelRuntime.FaceAskOrientedSurf(face,&surface,&sense));return sense;}
    private static ((double X,double Y,double Z) Normal,double Distance) Plane(int face)
    {
        int surface=0;byte sense=0;Assert.Equal(0,KernelRuntime.FaceAskOrientedSurf(face,&surface,&sense));var plane=new PK_PLANE_sf_s();Assert.Equal(0,KernelRuntime.PlaneAsk(surface,&plane));
        var s=sense==0?-1:1;var n=(s*plane.basis_set.axis.coord[0],s*plane.basis_set.axis.coord[1],s*plane.basis_set.axis.coord[2]);
        return(n,n.Item1*plane.basis_set.location.coord[0]+n.Item2*plane.basis_set.location.coord[1]+n.Item3*plane.basis_set.location.coord[2]);
    }
    private static int[] Select(int body,int mask)
    {
        var selected=new List<int>();
        foreach(var face in Faces(body))
        {
            int surface=0;byte sense=0;Assert.Equal(0,KernelRuntime.FaceAskOrientedSurf(face,&surface,&sense));
            var plane=new PK_PLANE_sf_s();Assert.Equal(0,KernelRuntime.PlaneAsk(surface,&plane));var key=-1;
            for(var k=0;k<3;k++)if(Math.Abs(plane.basis_set.axis.coord[k])>.9)key=2*k+(plane.basis_set.axis.coord[k]*(sense==0?-1:1)>0?1:0);
            Assert.True(key>=0);if((mask&(1<<key))!=0)selected.Add(face);
        }
        return selected.ToArray();
    }
    private static int Chamfer(int body)
    {
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var edge=KernelRuntime.Edges[KernelRuntime.Bodies[slot].FirstEdgeBody].Header.Tag;
        var options=new PK_EDGE_set_blend_chamfer_o_s{o_t_version=1};int n=0;
        Assert.Equal(0,KernelRuntime.EdgeSetBlendChamfer(1,&edge,.3,.3,null,&options,&n,null));
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-6};int* faces=null;int fault=0,fe=0,ft=0;
        Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&fix,&n,&faces,null,null,&fault,&fe,&ft));Assert.Equal(1,n);
        var result=faces[0];KernelRuntime.MemoryFree(faces);return result;
    }
    private static void Free(PK_TOPOL_track_r_s* track,PK_TOPOL_local_r_s* local)
    {Assert.Equal(0,KernelRuntime.TopolTrackResultFree(track));Assert.Equal(0,KernelRuntime.TopolLocalResultFree(local));}
    private static void Check(int body)
    {
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var b=KernelRuntime.Bodies[slot];var e=b.FirstEdgeBody;
        Assert.Equal(2,b.RegionCount);Assert.Equal(2,b.ShellCount);
        for(var i=0;i<b.EdgeCountBody;i++,e=KernelRuntime.Edges[e].NextInBody)
        {var edge=KernelRuntime.Edges[e];Assert.Equal(2,edge.FinCount);Assert.NotEqual(KernelRuntime.Fins[edge.FirstFinEdge].Sense,KernelRuntime.Fins[edge.LastFinEdge].Sense);}
        Assert.Equal(2,b.VertexCountBody-b.EdgeCountBody+b.FaceCountBody);
    }
    private static string Transmit(int body)
    {
        var options=new PK_PART_transmit_o_s{o_t_version=4,transmit_format=ParasolidConstants.PK_transmit_format_text_c,transmit_version=371,transmit_meshes=ParasolidConstants.PK_transmit_meshes_separate_c};
        var block=new PK_MEMORY_block_s();Assert.Equal(0,KernelRuntime.PartTransmitB(1,&body,&options,&block));
        try{return Encoding.ASCII.GetString(new ReadOnlySpan<byte>(block.bytes,(int)block.n_bytes));}finally{KernelRuntime.MemoryBlockFree(&block);}
    }
    private static (int,int,int,int,int,int,int,int,int,int,int) Counts()=>(KernelRuntime.Bodies.AliveCount,KernelRuntime.Regions.AliveCount,
        KernelRuntime.Shells.AliveCount,KernelRuntime.Faces.AliveCount,KernelRuntime.Loops.AliveCount,KernelRuntime.Edges.AliveCount,
        KernelRuntime.Vertices.AliveCount,KernelRuntime.Fins.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Points.AliveCount);
}

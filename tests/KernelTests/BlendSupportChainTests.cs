using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class BlendSupportChainTests:IDisposable
{
    public BlendSupportChainTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RealSupportSeamsTransportIndependentlyAndSurviveReload(bool reverse)
    {
        var body=Source();Span<int> edges=stackalloc int[2];Selected(body,edges);if(reverse)edges.Reverse();
        int n;int* originalFaces;Assert.Equal(0,KernelRuntime.BodyAskFaces(body,&n,&originalFaces));var retained=new ReadOnlySpan<int>(originalFaces,n).ToArray();KernelRuntime.MemoryFree(originalFaces);
        Mark(edges,.1);Assert.Equal(0,Fix(body,out var made));Assert.Equal(3,made);BooleanOperationTests.Counts(body,13,28,17);
        foreach(var tag in retained)Assert.True(KernelRuntime.IsValidTag(tag));
        foreach(var tag in edges)Assert.False(KernelRuntime.IsValidTag(tag));
        VerifySharedSections(body);
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out var restored));VerifySharedSections(restored);BooleanOperationTests.Counts(restored,13,28,17);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.EntityDelete(1,&restored));Empty();
    }

    [Theory]
    [InlineData(0)] [InlineData(8)] [InlineData(24)] [InlineData(48)] [InlineData(96)] [InlineData(160)] [InlineData(240)] [InlineData(480)] [InlineData(960)]
    public void FailedSupportChainRollsBackAllTemporaryDomainsAndGeometry(int budget)
    {
        var body=Source();Span<int> edges=stackalloc int[2];Selected(body,edges);Mark(edges,.1);
        Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out var before));var bytes=KernelRuntime.BlocksLiveBytes;var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        KernelRuntime.InjectAllocationFailure(budget);int error,made;
        try{error=Fix(body,out made);}finally{KernelRuntime.InjectAllocationFailure(-1);}
        if(error!=0)
        {
            Assert.Equal(0,made);Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out var after));Assert.Equal(before,after);
            Assert.Equal(bytes,KernelRuntime.BlocksLiveBytes);Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
            foreach(var tag in edges){Assert.True(KernelRuntime.IsValidTag(tag));Assert.Equal(1,KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(tag).Slot].PendingBlendKind);}
        }
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Empty();
    }

    [Fact]
    public void DifferentRadiiAndIncompleteSupportChainFailWithoutPublishing()
    {
        var body=Source();Span<int> edges=stackalloc int[2];Selected(body,edges);Mark(edges[..1],.1);
        Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out var before));Assert.NotEqual(0,Fix(body,out var count));Assert.Equal(0,count);
        Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out var after));Assert.Equal(before,after);
        Mark(edges[1..],.12);Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out before));Assert.NotEqual(0,Fix(body,out count));Assert.Equal(0,count);
        Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out after));Assert.Equal(before,after);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Empty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1e-10)]
    public void ASeparateSupportRecordRequiresItsOwnContinuityCertificate(double displacement)
    {
        var body=Source();Span<int> edges=stackalloc int[2];Selected(body,edges);var edge=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(edges[0]).Slot];var fin=edge.FirstFinEdge;var face=0;
        for(var i=0;i<2;i++,fin=KernelRuntime.Fins[fin].Other)
        {
            var slot=KernelRuntime.Loops[KernelRuntime.Fins[fin].Loop].Face;var f=KernelRuntime.Faces[slot];
            if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(f.SurfTag)].Class==SurfaceClass.BSurface)face=f.Header.Tag;
        }
        Assert.NotEqual(0,face);int original,copy;Assert.Equal(0,KernelRuntime.FaceAskSurf(face,&original));
        var matrix=new PK_TRANSF_sf_s();matrix.matrix[0]=matrix.matrix[5]=matrix.matrix[10]=matrix.matrix[15]=1;matrix.matrix[11]=displacement;
        int transform;Assert.Equal(0,KernelRuntime.TransfCreate(&matrix,&transform));byte exact;
        Assert.Equal(0,KernelRuntime.GeomTransform(original,transform,0,&copy,&exact));Assert.Equal(0,KernelRuntime.EntityDelete(1,&transform));
        Assert.Equal(0,KernelRuntime.TopologyDetachGeometry(face));byte sense=1;Assert.Equal(0,KernelRuntime.FaceAttachSurfaces(1,&face,&copy,&sense));
        Mark(edges,.1);Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out var before));var error=Fix(body,out var made);
        if(displacement==0)
        {
            Assert.True(error==0,$"error={error} support={KernelRuntime.LastSupportChainStage} regular={KernelRuntime.LastRegularBlendStage}");Assert.Equal(3,made);
            BooleanOperationTests.Counts(body,13,28,17);
        }
        else
        {
            Assert.NotEqual(0,error);Assert.Equal(0,made);Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out var after));Assert.Equal(before,after);
        }
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Empty();
    }

    [Fact]
    public void WarmSupportChainFixHasNoManagedAllocation()
    {
        var body=Source();Span<int> edges=stackalloc int[2];Selected(body,edges);Mark(edges,.1);Assert.Equal(0,Fix(body,out _));Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
        body=Source();Selected(body,edges);Mark(edges,.1);var options=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int n,fault,fe,ft;
        var before=GC.GetAllocatedBytesForCurrentThread();var error=KernelRuntime.BodyFixBlends(body,&options,&n,null,null,null,&fault,&fe,&ft);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(0,error);Assert.Equal(3,n);Assert.Equal(0,allocated);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Empty();
    }

    private static int Source()
    {
        var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","smooth-support-chain.x_t"));Assert.Equal(0,XtBrepImportTests.Receive(text,out var body));
        BooleanOperationTests.Counts(body,10,20,12);return body;
    }
    private static void Selected(int body,Span<int> result)
    {
        var b=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];var edge=b.FirstEdgeBody;var count=0;
        for(var i=0;i<b.EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var e=KernelRuntime.Edges[edge];var a=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[e.StartVertex].PointTag)].Position;
            var c=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[e.EndVertex].PointTag)].Position;
            if(Math.Abs(a.Y)+Math.Abs(c.Y)+Math.Abs(a.Z-1)+Math.Abs(c.Z-1)>1e-10)continue;
            Assert.True(count<result.Length);result[count++]=e.Header.Tag;
        }
        Assert.Equal(result.Length,count);
    }
    private static void Mark(Span<int> edges,double radius)
    {
        var options=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count;fixed(int* p=edges)Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(edges.Length,p,radius,&options,&count,null));
    }
    private static int Fix(int body,out int made)
    {
        var options=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int count,fault,fe,ft;int* output;
        var error=KernelRuntime.BodyFixBlends(body,&options,&count,&output,null,null,&fault,&fe,&ft);made=count;
        if(error==0)Assert.Equal(0,KernelRuntime.MemoryFree(output));else Assert.True(output==null);return error;
    }
    private static void VerifySharedSections(int body)
    {
        var b=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];var edge=b.FirstEdgeBody;var shared=0;var blend=0;
        var f=b.FirstFaceBody;for(var i=0;i<b.FaceCountBody;i++,f=KernelRuntime.Faces[f].NextInBody)
        {
            var surface=KernelRuntime.Faces[f].SurfTag;if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)].Class!=SurfaceClass.BlendSurface)continue;
            if(blend==0)blend=surface;else Assert.Equal(blend,surface);
        }
        for(var i=0;i<b.EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var e=KernelRuntime.Edges[edge];var fin=e.FirstFinEdge;var other=KernelRuntime.Fins[fin].Other;
            var a=KernelRuntime.Faces[KernelRuntime.Loops[KernelRuntime.Fins[fin].Loop].Face];var c=KernelRuntime.Faces[KernelRuntime.Loops[KernelRuntime.Fins[other].Loop].Face];
            if(a.SurfTag!=blend||c.SurfTag!=blend)continue;shared++;Assert.Equal(2,e.FinCount);Assert.NotEqual(KernelRuntime.Fins[fin].Sense,KernelRuntime.Fins[other].Sense);
            Assert.Equal(CurveClass.Circle,KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(e.CurveTag)].Class);Assert.Equal(a.Orientation,c.Orientation);
        }
        Assert.Equal(2,shared);
    }
    private static void Empty(){Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Points.AliveCount);}
}

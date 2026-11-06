using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class BlendChainTests:IDisposable
{
    public BlendChainTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public void SubdivisionChainPreservesAllPartitionsAndCanBeReloaded(bool curvedWall,bool reverse)
    {
        var body=RegularSolidBlendTests.Roof(curvedWall:curvedWall);Span<int> edges=stackalloc int[3];Split(body,edges);
        if(reverse)edges.Reverse();Mark(edges);Fix(body,3);BooleanOperationTests.Counts(body,9,21,14);
        foreach(var edge in edges)Assert.False(KernelRuntime.IsValidTag(edge));
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out var received));BooleanOperationTests.Counts(received,9,21,14);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.EntityDelete(1,&received));
        Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Points.AliveCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(12)] [InlineData(24)] [InlineData(40)] [InlineData(80)]
    public void FailedChainRestoresSelectedEdgesAndEveryTemporaryGeometry(int budget)
    {
        var body=RegularSolidBlendTests.Roof();Span<int> edges=stackalloc int[3];Split(body,edges);Mark(edges);
        Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out var before));var bytes=KernelRuntime.BlocksLiveBytes;var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int count,fault,fe,ft;int* output;
        KernelRuntime.InjectAllocationFailure(budget);int error;
        try{error=KernelRuntime.BodyFixBlends(body,&fix,&count,&output,null,null,&fault,&fe,&ft);}finally{KernelRuntime.InjectAllocationFailure(-1);}
        if(error==0){Assert.Equal(3,count);KernelRuntime.MemoryFree(output);return;}
        Assert.Equal(0,count);Assert.True(output==null);Assert.Equal(0,XtWriter.WriteText(new[]{body},371,out var after));Assert.Equal(before,after);
        Assert.Equal(bytes,KernelRuntime.BlocksLiveBytes);Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
        foreach(var edge in edges){Assert.True(KernelRuntime.IsValidTag(edge));Assert.Equal(1,KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(edge).Slot].PendingBlendKind);}
    }

    [Fact]
    public void WarmChainPublishesWithoutManagedAllocation()
    {
        var warm=RegularSolidBlendTests.Roof();Span<int> edges=stackalloc int[3];Split(warm,edges);Mark(edges);Fix(warm,3);Assert.Equal(0,KernelRuntime.EntityDelete(1,&warm));
        var body=RegularSolidBlendTests.Roof();Split(body,edges);Mark(edges);
        var options=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int count,fault,fe,ft;
        var before=GC.GetAllocatedBytesForCurrentThread();var error=KernelRuntime.BodyFixBlends(body,&options,&count,null,null,null,&fault,&fe,&ft);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(0,error);Assert.Equal(3,count);Assert.Equal(0,allocated);
    }

    private static void Split(int body,Span<int> edges)
    {
        edges[0]=RegularSolidBlendTests.SelectedEdge(body);int vertex,tail;
        Assert.Equal(0,KernelRuntime.EdgeSplitAtParam(edges[0],.21,&vertex,&tail));edges[1]=tail;
        Assert.Equal(0,KernelRuntime.EdgeSplitAtParam(edges[1],.68,&vertex,&tail));edges[2]=tail;
    }
    private static void Mark(Span<int> edges)
    {
        var options=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count;
        fixed(int* input=edges)Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(edges.Length,input,.1,&options,&count,null));
    }
    private static void Fix(int body,int expected)
    {
        var options=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int count,fault,fe,ft;
        var error=KernelRuntime.BodyFixBlends(body,&options,&count,null,null,null,&fault,&fe,&ft);
        Assert.True(error==0,$"error={error}, blend={KernelRuntime.LastRegularBlendStage}, sheet={KernelRuntime.LastSheetPartitionStage}");Assert.Equal(expected,count);
    }
}

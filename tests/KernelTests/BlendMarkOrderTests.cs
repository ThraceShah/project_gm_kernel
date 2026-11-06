using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class BlendMarkOrderTests:IDisposable
{
    public BlendMarkOrderTests(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose(){KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void TrivalentProductOriginsFollowTheSuppliedMarkOrder(int rotation)
    {
        var body=RegularSolidBlendTests.Roof();Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));
        var vertex=KernelRuntime.Bodies[slot].FirstVertexBody;var corner=-1;
        for(var i=0;i<KernelRuntime.Bodies[slot].VertexCountBody;i++,vertex=KernelRuntime.Vertices[vertex].NextInBody)
        {var p=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[vertex].PointTag)].Position;if(Math.Abs(p.X)+Math.Abs(p.Y)+Math.Abs(p.Z-1)<1e-10)corner=vertex;}
        Assert.True(corner>=0);Span<int> incident=stackalloc int[3];var count=0;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
            if(KernelRuntime.Edges[edge].StartVertex==corner||KernelRuntime.Edges[edge].EndVertex==corner)incident[count++]=KernelRuntime.Edges[edge].Header.Tag;
        Assert.Equal(3,count);Span<int> ordered=stackalloc int[3];for(var i=0;i<3;i++)ordered[i]=incident[(i+rotation)%3];
        if(rotation>=3)(ordered[0],ordered[1])=(ordered[1],ordered[0]);
        var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};fixed(int* p=ordered)Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(3,p,.1,&set,&count,null));
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,fe,ft;int* origins;
        Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,&origins,&fault,&fe,&ft));
        try{Assert.Equal(4,count);for(var i=0;i<3;i++)Assert.Equal(ordered[i],origins[i]);}finally{KernelRuntime.MemoryFree(origins);}
    }

    [Fact]
    public void ABodyCopyRetainsPendingDefinitionOrder()
    {
        var body=RegularSolidBlendTests.Roof();int n;int* tags;Assert.Equal(0,KernelRuntime.BodyAskEdges(body,&n,&tags));
        Span<int> marked=stackalloc int[3]{tags[2],tags[0],tags[1]};KernelRuntime.MemoryFree(tags);var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};
        fixed(int* input=marked)Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(3,input,.1,&set,&n,null));
        int copy;Assert.Equal(0,KernelRuntime.EntityCopy(body,&copy));Assert.True(KernelRuntime.TryResolveBodySlot(copy,out var slot));
        Span<long> sequences=stackalloc long[3];var at=0;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
            if(KernelRuntime.Edges[edge].PendingBlendKind!=0)sequences[at++]=KernelRuntime.Edges[edge].PendingBlendSequence;
        Assert.Equal(3,at);sequences.Sort();Assert.Equal(1,sequences[0]);Assert.Equal(2,sequences[1]);Assert.Equal(3,sequences[2]);
    }
}

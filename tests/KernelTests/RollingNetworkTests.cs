using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using EdgeTag=System.Int32;
using System.Runtime.InteropServices;
using System.Text;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class RollingNetworkTests:IDisposable
{
    public RollingNetworkTests()
    {
        KernelRuntime.ForbidLegacyRegularBlendAssembly=true;KernelRuntime.SessionStop();
        var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));
    }
    public void Dispose()
    {KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.TraceFiniteBoundary=null;KernelRuntime.ForbidLegacyRegularBlendAssembly=false;KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HeterogeneousBandsPublishThroughArrangementAndRetainPositiveShortIntervals(bool reverse)
    {
        var body=Seed();Span<EdgeTag> selected=stackalloc EdgeTag[2];Selection(body,selected);if(reverse)selected.Reverse();Mark(selected);
        var trace=new List<KernelRuntime.BoundaryTrace>();KernelRuntime.TraceFiniteBoundary=trace.Add;
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int count,fault,edgeFault,topolFault;int* products;
        Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&fix,&count,&products,null,null,&fault,&edgeFault,&topolFault));
        try
        {
            Assert.Equal(2,count);Assert.NotEqual(products[0],products[1]);
            for(var i=0;i<count;i++)Assert.True(KernelRuntime.IsValidTag(products[i]));
        }
        finally{KernelRuntime.MemoryFree(products);}
        Assert.Equal(KernelRuntime.RollingNetworkStage.Publish,KernelRuntime.LastRollingNetworkStage);
        BooleanOperationTests.Counts(body,10,23,15);ShortIntervals(body);
        foreach(var product in trace.Where(t=>t.Stage==KernelRuntime.BoundaryTraceStage.Product))
            Assert.Contains(trace,t=>t.Stage==KernelRuntime.BoundaryTraceStage.Arrangement&&t.Source==product.Source&&t.Edge==product.Edge
                &&t.Curve==product.Curve&&t.Generation==product.Generation&&t.First==product.First&&t.Last==product.Last
                &&t.StartEvent==product.StartEvent&&t.EndEvent==product.EndEvent);
        var text=Transmit(body);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
        Assert.Equal(0,XtBrepImportTests.Receive(text,out var restored));ShortIntervals(restored);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&restored));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(10)] [InlineData(50)] [InlineData(200)]
    public void NetworkAllocationFailureRestoresPendingIncidenceGenerationsAndGeometry(int budget)
    {
        var body=Seed();Span<EdgeTag> selected=stackalloc EdgeTag[2];Selection(body,selected);Mark(selected);
        var before=Topology(body);var first=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(selected[0]).Slot];
        var second=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(selected[1]).Slot];
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;var blocks=KernelRuntime.BlocksLiveBytes;var returns=KernelRuntime.ReturnOutstandingBlocks;
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int count,fault,edgeFault,topolFault;int* products;
        KernelRuntime.InjectAllocationFailure(budget);var error=KernelRuntime.BodyFixBlends(body,&fix,&count,&products,null,null,&fault,&edgeFault,&topolFault);KernelRuntime.InjectAllocationFailure(-1);
        if(error==0){KernelRuntime.MemoryFree(products);BooleanOperationTests.Counts(body,10,23,15);return;}
        Assert.Equal(0,count);Assert.True(products==null);Assert.Equal(before,Topology(body));
        var a=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(selected[0]).Slot];var b=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(selected[1]).Slot];
        Assert.Equal(first.Header.Generation,a.Header.Generation);Assert.Equal(second.Header.Generation,b.Header.Generation);
        Assert.Equal(first.PendingBlendSequence,a.PendingBlendSequence);Assert.Equal(second.PendingBlendSequence,b.PendingBlendSequence);
        Assert.Equal(first.PendingBlendKind,a.PendingBlendKind);Assert.Equal(second.PendingBlendKind,b.PendingBlendKind);
        Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
        Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);Assert.Equal(returns,KernelRuntime.ReturnOutstandingBlocks);
    }

    [Fact]
    public void SeparatedNonAdjacentPlanarBandsShareTheArrangementConsumer()
    {
        var body=BooleanOperationTests.Block(0,0,0,1,1,1);Span<EdgeTag> selected=stackalloc EdgeTag[2];CubeSelection(body,selected);
        Mark(selected,.1);var trace=new List<KernelRuntime.BoundaryTrace>();KernelRuntime.TraceFiniteBoundary=trace.Add;
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int count,fault,edgeFault,topolFault;
        var error=KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&edgeFault,&topolFault);
        Assert.True(error==0,$"error={error} stage={KernelRuntime.LastRollingNetworkStage} faces={KernelRuntime.LastRollingFirstFace}/{KernelRuntime.LastRollingSecondFace} sheet={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} SSI={KernelRuntime.LastGeneralIntersectionStage}/{KernelRuntime.LastGeneralIntersectionStatus}");
        Assert.Equal(2,count);Assert.Equal(KernelRuntime.RollingNetworkStage.Publish,KernelRuntime.LastRollingNetworkStage);
        BooleanOperationTests.Counts(body,8,18,12);Assert.Contains(trace,t=>t.Stage==KernelRuntime.BoundaryTraceStage.Product&&t.Source<0);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void InterferingComponentsReachingOppositeSupportsCannotPublishAnUnresolvedSolid()
    {
        var body=BooleanOperationTests.Block(0,0,0,1,1,1);Span<EdgeTag> selected=stackalloc EdgeTag[2];CubeSelection(body,selected);
        Mark(selected,1);var before=Topology(body);var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;var blocks=KernelRuntime.BlocksLiveBytes;
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int count,fault,edgeFault,topolFault;int* products;
        Assert.NotEqual(0,KernelRuntime.BodyFixBlends(body,&fix,&count,&products,null,null,&fault,&edgeFault,&topolFault));
        Assert.Equal(0,count);Assert.True(products==null);Assert.Equal(before,Topology(body));Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);
    }

    private static int Seed()
    {
        var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","generated-short-edge-input.x_t"));
        Assert.Equal(0,XtBrepImportTests.Receive(text,out var body));return body;
    }
    private static string Transmit(int body)
    {
        var options=new PK_PART_transmit_o_s{o_t_version=4,transmit_format=ParasolidConstants.PK_transmit_format_text_c,transmit_version=371,
            transmit_user_fields=1,transmit_meshes=ParasolidConstants.PK_transmit_meshes_separate_c};PK_MEMORY_block_s block;
        Assert.Equal(0,KernelRuntime.PartTransmitB(1,&body,&options,&block));
        try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,(int)p->n_bytes));return Encoding.ASCII.GetString(stream.ToArray());}
        finally{Assert.Equal(0,KernelRuntime.MemoryBlockFree(&block));}
    }
    private static byte[] Topology(int body)
    {
        using var stream=new MemoryStream();var record=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];Write(record);
        var edge=record.FirstEdgeBody;for(var e=0;e<record.EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)Write(KernelRuntime.Edges[edge]);
        var vertex=record.FirstVertexBody;for(var v=0;v<record.VertexCountBody;v++,vertex=KernelRuntime.Vertices[vertex].NextInBody)Write(KernelRuntime.Vertices[vertex]);
        var face=record.FirstFaceBody;
        for(var f=0;f<record.FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var item=KernelRuntime.Faces[face];Write(item);var loop=item.FirstLoop;
            for(var l=0;l<item.LoopCount;l++,loop=KernelRuntime.Loops[loop].NextInFace)
            {
                var ring=KernelRuntime.Loops[loop];Write(ring);var fin=ring.FirstFin;
                for(var c=0;c<ring.FinCount;c++,fin=KernelRuntime.Fins[fin].NextInLoop)Write(KernelRuntime.Fins[fin]);
            }
        }
        return stream.ToArray();
        void Write<T>(T value)where T:unmanaged=>stream.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value,1)));
    }
    private static void Selection(int body,Span<EdgeTag> selected)
    {
        var slot=KernelRuntime.ResolveTagRecord(body).Slot;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;var junction=-1;selected.Clear();
        for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var record=KernelRuntime.Edges[edge];if(KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)].Class!=CurveClass.Ellipse)continue;
            if(!SupportPair(edge,SurfaceClass.Cylinder))continue;Assert.Equal(0,selected[0]);selected[0]=record.Header.Tag;
            foreach(var vertex in new[]{record.StartVertex,record.EndVertex})
            {
                var degree=0;var incident=KernelRuntime.Bodies[slot].FirstEdgeBody;
                for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,incident=KernelRuntime.Edges[incident].NextInBody)
                {var candidate=KernelRuntime.Edges[incident];if(candidate.StartVertex==vertex||candidate.EndVertex==vertex)degree++;}
                if(degree==4)junction=vertex;
            }
        }
        Assert.NotEqual(-1,junction);edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var record=KernelRuntime.Edges[edge];if(record.StartVertex!=junction&&record.EndVertex!=junction)continue;
            if(KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)].Class!=CurveClass.Line||!SupportPair(edge,SurfaceClass.Plane))continue;
            Assert.Equal(0,selected[1]);selected[1]=record.Header.Tag;
        }
        Assert.True(selected[0]!=0&&selected[1]!=0);
    }
    private static bool SupportPair(int edge,SurfaceClass type)
    {
        var fin=KernelRuntime.Edges[edge].FirstFinEdge;var other=KernelRuntime.Fins[fin].Other;
        var a=KernelRuntime.Loops[KernelRuntime.Fins[fin].Loop].Face;var b=KernelRuntime.Loops[KernelRuntime.Fins[other].Loop].Face;
        return KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[a].SurfTag)].Class==type
            &&KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[b].SurfTag)].Class==type;
    }
    private static void CubeSelection(int body,Span<EdgeTag> selected)
    {
        selected.Clear();var slot=KernelRuntime.ResolveTagRecord(body).Slot;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        var vertex=KernelRuntime.Bodies[slot].FirstVertexBody;var minY=double.PositiveInfinity;var minZ=double.PositiveInfinity;var maxY=double.NegativeInfinity;var maxZ=double.NegativeInfinity;
        for(var v=0;v<KernelRuntime.Bodies[slot].VertexCountBody;v++,vertex=KernelRuntime.Vertices[vertex].NextInBody)
        {var point=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[vertex].PointTag)].Position;minY=Math.Min(minY,point.Y);maxY=Math.Max(maxY,point.Y);minZ=Math.Min(minZ,point.Z);maxZ=Math.Max(maxZ,point.Z);}
        for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var record=KernelRuntime.Edges[edge];var a=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[record.StartVertex].PointTag)].Position;
            var b=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[record.EndVertex].PointTag)].Position;
            if(a.Y!=b.Y||a.Z!=b.Z)continue;
            if(a.Y==minY&&a.Z==minZ)selected[0]=record.Header.Tag;if(a.Y==maxY&&a.Z==maxZ)selected[1]=record.Header.Tag;
        }
        Assert.True(selected[0]!=0&&selected[1]!=0);
    }
    private static void Mark(ReadOnlySpan<EdgeTag> selected,double radius=.5)
    {
        var options=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};options.properties.tolerance=1e-7;int count;
        fixed(EdgeTag* edges=selected)Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(selected.Length,edges,radius,&options,&count,null));
        Assert.Equal(2,count);
    }
    private static void ShortIntervals(int body)
    {
        var slot=KernelRuntime.ResolveTagRecord(body).Slot;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;var count=0;
        PK_VECTOR_s* point=stackalloc PK_VECTOR_s[2];
        for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var record=KernelRuntime.Edges[edge];if(KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)].Class!=CurveClass.ICurve)continue;
            var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)];var data=KernelRuntime.ICurveDataPool[curve.DataIndex];
            Assert.Equal(IntersectionUvType.Both,data.UvType);Assert.Equal(4*data.ChartCount,data.UvValueCount);
            Assert.Equal(1,record.HasParameterRange);Assert.True(record.ParameterEnd>record.ParameterStart);
            Assert.Equal(0,KernelRuntime.CurveEval(record.CurveTag,record.ParameterStart,0,point));Assert.Equal(0,KernelRuntime.CurveEval(record.CurveTag,record.ParameterEnd,0,point+1));
            var squared=0.0;for(var i=0;i<3;i++){var d=point[0].coord[i]-point[1].coord[i];squared+=d*d;}
            if(squared<1e-6){Assert.InRange(Math.Sqrt(squared),7.6e-5,7.7e-5);count++;}
        }
        Assert.Equal(2,count);
    }
}

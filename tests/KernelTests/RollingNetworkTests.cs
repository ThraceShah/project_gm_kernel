using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using EdgeTag=System.Int32;
using BodyTag=System.Int32;
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
    [InlineData(1,false)] [InlineData(1,true)] [InlineData(1.7,false)] [InlineData(1.7,true)]
    public void TransportedReferenceMeridianKeepsItsIdentityAndRejectsNearbyPlanes(double scale,bool mirror)
    {
        var axis=Rotate(new(){Y=1});var reference=Rotate(new(){X=-1});
        var origin=Position(new(){X=-1.5,Z=4});var point=Position(new(){X=-2.5,Y=-1.5,Z=4});
        var planeOrigin=Position(new(){X=-2.5});
        var cylinder=new AnalyticSurface(SurfaceClass.Cylinder,origin,axis,reference,scale);
        var plane=new AnalyticSurface(SurfaceClass.Plane,planeOrigin,reference,axis);
        Assert.True(KernelRuntime.TryReferenceMeridianContact(cylinder,plane,point,out var error));Assert.InRange(error,0,1e-12);
        var displaced=EvaluationMath.Add(planeOrigin,EvaluationMath.Scale(reference,scale*1e-10));
        Assert.False(KernelRuntime.TryReferenceMeridianContact(cylinder,new(SurfaceClass.Plane,displaced,reference,axis),point,out _));
        var changed=EvaluationMath.Add(reference,EvaluationMath.Scale(axis,1e-14));
        Assert.False(KernelRuntime.TryReferenceMeridianContact(cylinder,new(SurfaceClass.Plane,planeOrigin,changed,axis),point,out _));
        return;

        KernelVector3 Position(KernelVector3 value)=>EvaluationMath.Add(EvaluationMath.Scale(Rotate(value),scale),new(){X=-.7*scale,Y=1.1*scale,Z=2.3*scale});
        KernelVector3 Rotate(KernelVector3 value)
        {
            if(mirror)value.X=-value.X;
            var x=value.X;var y=Math.Cos(.23)*value.Y-Math.Sin(.23)*value.Z;var z=Math.Sin(.23)*value.Y+Math.Cos(.23)*value.Z;
            var u=Math.Cos(-.29)*x+Math.Sin(-.29)*z;var w=-Math.Sin(-.29)*x+Math.Cos(-.29)*z;
            return new(){X=Math.Cos(.37)*u-Math.Sin(.37)*y,Y=Math.Sin(.37)*u+Math.Cos(.37)*y,Z=w};
        }
    }

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
        BooleanOperationTests.Counts(body,10,23,15);ShortIntervals(body);NativeSupportIncidence(body);
        AssertSavedProbeOutside(body,-1.499999667811987,-2.500000004201637,4.50007614662344); // row 289, ambiguous face ray
        AssertSavedProbeOutside(body,-1.4999999169520075,-2.500000008550392,4.500038073674117); // row 391, false edge ownership
        foreach(var product in trace.Where(t=>t.Stage==KernelRuntime.BoundaryTraceStage.Product))
            Assert.Contains(trace,t=>t.Stage==KernelRuntime.BoundaryTraceStage.Arrangement&&t.Source==product.Source&&t.Edge==product.Edge
                &&t.Curve==product.Curve&&t.Generation==product.Generation&&t.First==product.First&&t.Last==product.Last
                &&t.StartEvent==product.StartEvent&&t.EndEvent==product.EndEvent);
        var text=Transmit(body);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
        Assert.Equal(0,XtBrepImportTests.Receive(text,out var restored));ShortIntervals(restored);NativeSupportIncidence(restored);
        AssertSavedProbeOutside(restored,-1.499999667811987,-2.500000004201637,4.50007614662344);
        AssertSavedProbeOutside(restored,-1.4999999169520075,-2.500000008550392,4.500038073674117);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&restored));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void ShallowRollingPlaneCutsPreserveBothShortIntervalsThroughXt()
    {
        var body=Seed("generated-short-edge-shallow-input.x_t");
        Span<EdgeTag> selected=stackalloc EdgeTag[2];Selection(body,selected);Mark(selected);
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int count,fault,edgeFault,topolFault;
        var error=KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&edgeFault,&topolFault);
        Assert.True(error==0,$"error={error} stage={KernelRuntime.LastRollingNetworkStage}");
        Assert.Equal(2,count);
        Assert.Equal(KernelRuntime.RollingNetworkStage.Publish,KernelRuntime.LastRollingNetworkStage);
        BooleanOperationTests.Counts(body,10,23,15);ShortIntervals(body,1.90e-5,1.91e-5);NativeSupportIncidence(body);
        var text=Transmit(body);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
        Assert.Equal(0,XtBrepImportTests.Receive(text,out var restored));
        BooleanOperationTests.Counts(restored,10,23,15);ShortIntervals(restored,1.90e-5,1.91e-5);NativeSupportIncidence(restored);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&restored));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SubdividedSelectedCarrierRetainsTheSharedSectionAndProductProvenance(bool ellipse)
    {
        var body=Seed();Span<EdgeTag> selected=stackalloc EdgeTag[3];Selection(body,selected[..2]);
        var index=ellipse?0:1;var carrier=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(selected[index]).Slot];
        var lo=carrier.ParameterStart;var hi=carrier.ParameterEnd;
        if(carrier.HasParameterRange==0)
        {
            Assert.False(ellipse,"fixture ellipse has a finite native range");
            var p=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[carrier.StartVertex].PointTag)].Position;
            var q=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[carrier.EndVertex].PointTag)].Position;
            var length=EvaluationMath.Sub(p,q);hi=Math.Sqrt(EvaluationMath.Dot(length,length));
        }
        EntityTag vertex;EdgeTag tail;Assert.Equal(0,KernelRuntime.EdgeSplitAtParam(selected[index],lo+(hi-lo)*.5,&vertex,&tail));selected[2]=tail;
        selected.Reverse();Mark(selected);
        var trace=new List<KernelRuntime.BoundaryTrace>();KernelRuntime.TraceFiniteBoundary=trace.Add;
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int count,fault,edgeFault,topolFault;EntityTag* products;EntityTag* replaced;
        Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&fix,&count,&products,null,&replaced,&fault,&edgeFault,&topolFault));
        try
        {
            Assert.Equal(3,count);
            for(var i=0;i<count;i++)Assert.Equal(selected[i],replaced[i]);
            var a=KernelRuntime.ResolveTagRecord(products[0]).Slot;var b=KernelRuntime.ResolveTagRecord(products[2-index]).Slot;
            var slot=KernelRuntime.ResolveTagRecord(body).Slot;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;var sections=0;CurveTag sharedSection=0;
            for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
            {
                var record=KernelRuntime.Edges[edge];var first=record.FirstFinEdge;var second=KernelRuntime.Fins[first].Other;
                var x=KernelRuntime.Loops[KernelRuntime.Fins[first].Loop].Face;var y=KernelRuntime.Loops[KernelRuntime.Fins[second].Loop].Face;
                if(!(a==x&&b==y||a==y&&b==x))continue;
                sections++;sharedSection=record.CurveTag;Assert.Equal(CurveClass.Circle,KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)].Class);
                Assert.True(record.ParameterEnd>record.ParameterStart);Assert.NotEqual(KernelRuntime.Fins[first].Sense,KernelRuntime.Fins[second].Sense);
            }
            Assert.Equal(1,sections);
            var witness=Assert.Single(trace,x=>x.Stage==KernelRuntime.BoundaryTraceStage.NativeCutEndpoints
                &&trace.Any(p=>p.Stage==KernelRuntime.BoundaryTraceStage.Product&&p.ProductCurve==sharedSection&&p.Curve==x.Curve&&p.Generation==x.Generation));
            if(ellipse)
            {
                var charts=trace.Where(x=>x.Stage==KernelRuntime.BoundaryTraceStage.NativeSectionChart&&x.Curve==witness.Curve).ToArray();
                Assert.Equal(2,charts.Length);
                foreach(var chart in charts)
                {
                    Assert.Equal(chart.UvStart.param[0],chart.UvEnd.param[0]);
                    Assert.Equal(0,Math.Min(chart.UvStart.param[1],chart.UvEnd.param[1]));
                    Assert.Equal(1,Math.Max(chart.UvStart.param[1],chart.UvEnd.param[1]));
                }
            }
            Assert.True(witness.StartEvent>=0&&witness.EndEvent>=0&&witness.StartEvent!=witness.EndEvent&&witness.Last>witness.First);
            Assert.Contains(trace,x=>x.Stage==KernelRuntime.BoundaryTraceStage.Product&&x.Curve==witness.Curve&&x.Generation==witness.Generation
                &&x.First==witness.First&&x.Last==witness.Last);
        }
        finally{KernelRuntime.MemoryFree(products);KernelRuntime.MemoryFree(replaced);}
        BooleanOperationTests.Counts(body,11,26,17);ShortIntervals(body);
        var bytes=Transmit(body);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
        Assert.Equal(0,XtBrepImportTests.Receive(bytes,out var restored));BooleanOperationTests.Counts(restored,11,26,17);ShortIntervals(restored);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&restored));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void IncompatibleFiniteCapContinuesAcrossAnInteriorSelectedSubdivision(bool reverse)
    {
        var body=Seed();Span<EdgeTag> selected=stackalloc EdgeTag[3];Selection(body,selected[..2]);
        var edge=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(selected[0]).Slot];
        Assert.Equal(1,edge.HasParameterRange);
        var t=edge.ParameterStart+.997*(edge.ParameterEnd-edge.ParameterStart);
        EntityTag vertex;EdgeTag tail;
        Assert.Equal(0,KernelRuntime.EdgeSplitAtParam(selected[0],t,&vertex,&tail));selected[2]=tail;
        var occurrence=KernelRuntime.ResolveTagRecord(vertex).Slot;if(reverse)selected.Reverse();Mark(selected);
        var trace=new List<KernelRuntime.RollingNetworkTrace>();KernelRuntime.TraceRollingNetwork=trace.Add;
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int count,fault,edgeFault,topolFault;EntityTag* products;EntityTag* replaced;
        try
        {
            Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&fix,&count,&products,null,&replaced,&fault,&edgeFault,&topolFault));
            try
            {
                Assert.Equal(2,count);var contributors=new ReadOnlySpan<EntityTag>(replaced,count).ToArray();
                Assert.Contains(selected[1],contributors);
                var fragment=tail;Assert.Single(contributors,x=>x==fragment||x==edge.Header.Tag);
            }
            finally{KernelRuntime.MemoryFree(products);KernelRuntime.MemoryFree(replaced);}
        }
        finally{KernelRuntime.TraceRollingNetwork=null;}
        var witness=Assert.Single(trace,x=>x.Stage==KernelRuntime.RollingNetworkStage.ContinuedSubdivision);
        Assert.Equal(occurrence,witness.Vertex);Assert.True(witness.Last>witness.First);
        Assert.DoesNotContain(trace,x=>x.Stage==KernelRuntime.RollingNetworkStage.Subdivision);
        BooleanOperationTests.Counts(body,10,23,15);ShortIntervals(body);NativeSupportIncidence(body);
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

    private static int Seed(string fixture="generated-short-edge-input.x_t")
    {
        var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",fixture));
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

    private static void NativeSupportIncidence(int body)
    {
        var slot=KernelRuntime.ResolveTagRecord(body).Slot;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var record=KernelRuntime.Edges[edge];var carrier=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)];
            if(carrier.Class!=CurveClass.ICurve)continue;
            var data=KernelRuntime.ICurveDataPool[carrier.DataIndex];var fin=record.FirstFinEdge;var other=KernelRuntime.Fins[fin].Other;
            var a=KernelRuntime.Faces[KernelRuntime.Loops[KernelRuntime.Fins[fin].Loop].Face].SurfTag;
            var b=KernelRuntime.Faces[KernelRuntime.Loops[KernelRuntime.Fins[other].Loop].Face].SurfTag;
            Assert.True(data.Surface0Tag==a&&data.Surface1Tag==b||data.Surface0Tag==b&&data.Surface1Tag==a);
        }
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
        Assert.Equal(selected.Length,count);
    }
    private static void AssertSavedProbeOutside(BodyTag body,double x,double y,double z)
    {
        var p=new PK_VECTOR_s();p.coord[0]=x;p.coord[1]=y;p.coord[2]=z;
        int enclosure;EntityTag topology;
        Assert.Equal(0,KernelRuntime.BodyContainsVector(body,p,&enclosure,&topology));
        Assert.Equal(ParasolidConstants.PK_enclosure_outside_c,enclosure);
        Assert.True(topology>0);
    }

    private static void ShortIntervals(int body,double min=7.6e-5,double max=7.7e-5)
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
            if(squared<1e-6){Assert.InRange(Math.Sqrt(squared),min,max);count++;}
        }
        Assert.Equal(2,count);
    }
}

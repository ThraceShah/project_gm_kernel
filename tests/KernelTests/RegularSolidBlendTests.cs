using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using EdgeSlot=System.Int32;
using FaceSlot=System.Int32;
using FinSlot=System.Int32;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class RegularSolidBlendTests:IDisposable
{
    public RegularSolidBlendTests()
    {
        KernelRuntime.ForbidLegacyRegularBlendAssembly=true;
        KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));
    }
    public void Dispose(){KernelRuntime.TraceFiniteBoundary=null;KernelRuntime.ForbidLegacyRegularBlendAssembly=false;KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Fact]
    public void FilletAndBooleanProductsHaveFiniteArrangementProvenanceWithLegacyAssemblyForbidden()
    {
        var trace=new List<KernelRuntime.BoundaryTrace>();KernelRuntime.TraceFiniteBoundary=trace.Add;
        var body=RoundedRoof();CheckTrace();trace.Clear();
        var tool=BooleanOperationTests.Block(1,.5,-.1,1,2,2);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int count;int* products;
        var previous=KernelRuntime.ForbidSpatialBoundaryReuse;
        KernelRuntime.ForbidSpatialBoundaryReuse=true;
        try{Assert.Equal(0,KernelRuntime.BodyBoolean(body,1,&tool,&options,&count,&products));}
        finally{KernelRuntime.ForbidSpatialBoundaryReuse=previous;}
        try
        {
            Assert.Equal(1,count);CheckTrace();
            Assert.DoesNotContain(trace,t=>t.Stage==KernelRuntime.BoundaryTraceStage.SpatialEndpointReuse);
            var section=trace.First(t=>t.Stage==KernelRuntime.BoundaryTraceStage.NativeSectionChart);
            var ends=trace.Where(t=>t.Stage==KernelRuntime.BoundaryTraceStage.NativeCutLocator&&t.ProductCurve==section.Curve).ToArray();
            Assert.Equal(new[]{section.First,section.Last},ends.Select(t=>t.First).Order().ToArray());
            Assert.All(ends,t=>Assert.Equal(t.First,t.Last));
            foreach(var e in new[]{section.StartEvent,section.EndEvent})
            {
                var root=Assert.Single(trace,t=>t.Stage==KernelRuntime.BoundaryTraceStage.CertifiedRoot&&t.ProductCurve==section.Curve&&t.StartEvent==e);
                Assert.True(root.Witness>0);Assert.True(root.SpatialError<1e-10);
                Assert.Contains(trace,t=>t.Stage==root.Stage&&t.Curve==root.Curve&&t.Generation==root.Generation
                    &&t.Witness==root.Witness&&t.InputCoedge!=root.InputCoedge&&t.StartEvent==e&&t.First==root.First&&t.Last==root.Last);
            }
        }
        finally{KernelRuntime.MemoryFree(products);}

        void CheckTrace()
        {
            var published=trace.Where(t=>t.Stage==KernelRuntime.BoundaryTraceStage.Product).ToArray();Assert.NotEmpty(published);
            foreach(var product in published)
            {
                Assert.True(product.Last>product.First);Assert.True(product.ProductCurve>0);
                Assert.Contains(trace,t=>t.Stage==KernelRuntime.BoundaryTraceStage.Arrangement&&t.Source==product.Source&&t.Edge==product.Edge
                    &&t.Curve==product.Curve&&t.Generation==product.Generation&&t.First==product.First&&t.Last==product.Last
                    &&t.StartEvent==product.StartEvent&&t.EndEvent==product.EndEvent);
                Assert.Contains(trace,t=>t.Stage==KernelRuntime.BoundaryTraceStage.Input&&t.Edge==product.Edge&&t.Curve==product.Curve
                    &&t.Generation==product.Generation&&t.First==product.First&&t.Last==product.Last);
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)] [InlineData(7)] [InlineData(12)] [InlineData(20)]
    public void BlendAllocationBudgetEitherCompletesOrRestoresSourceAndPools(int budget)
    {
        var body=Roof();var edge=SelectedEdge(body);var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count;
        Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.1,&set,&count,null));
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;var bodies=KernelRuntime.Bodies.AliveCount;var returns=KernelRuntime.ReturnOutstandingBlocks;var blocks=KernelRuntime.BlocksLiveBytes;
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,fe,ft;int* output;
        KernelRuntime.InjectAllocationFailure(budget);var error=KernelRuntime.BodyFixBlends(body,&fix,&count,&output,null,null,&fault,&fe,&ft);KernelRuntime.InjectAllocationFailure(-1);
        if(error==0)
        {
            Assert.Equal(1,count);BooleanOperationTests.Counts(body,7,15,10);KernelRuntime.MemoryFree(output);return;
        }
        Assert.Equal(0,count);Assert.True(output==null);BooleanOperationTests.Counts(body,6,12,8);
        Assert.True(KernelRuntime.IsValidTag(edge));Assert.Equal(1,KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(edge).Slot].PendingBlendKind);
        Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);Assert.Equal(bodies,KernelRuntime.Bodies.AliveCount);Assert.Equal(returns,KernelRuntime.ReturnOutstandingBlocks);
        Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);
    }

    [Fact]
    public void RemoteBoreInModifiedSupportSurvivesFilletAndSubsequentCut()
    {
        var body=Roof();var axis=new PK_AXIS2_sf_s();axis.location.coord[0]=.3;axis.location.coord[1]=-1;axis.location.coord[2]=.35;
        axis.axis.coord[1]=1;axis.ref_direction.coord[0]=1;int tool;
        Assert.Equal(0,KernelRuntime.BodyCreateSolidCyl(.06,3,&axis,&tool));
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int count;int* output;
        Assert.Equal(0,KernelRuntime.BodyBoolean(body,1,&tool,&options,&count,&output));KernelRuntime.MemoryFree(output);
        var edge=SelectedEdge(body);var slot=KernelRuntime.ResolveTagRecord(edge).Slot;var fin=KernelRuntime.Edges[slot].FirstFinEdge;var support=-1;
        for(var i=0;i<2;i++,fin=KernelRuntime.Fins[fin].Other)
        {
            var face=KernelRuntime.Loops[KernelRuntime.Fins[fin].Loop].Face;
            if(KernelRuntime.Faces[face].LoopCount>1)support=face;
        }
        Assert.True(support>=0,"the selected edge's actual support must contain the bore loop");
        var originalLoops=KernelRuntime.Faces[support].LoopCount;var retained=KernelRuntime.Faces[support].Header.Tag;
        var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.1,&set,&count,null));
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,fe,ft;
        var error=KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&fe,&ft);
        Assert.True(error==0,$"fix={error} stage={KernelRuntime.LastRegularBlendStage} domain={KernelRuntime.LastRegularBlendDomainStage}");
        Assert.True(KernelRuntime.IsValidTag(retained));Assert.Equal(originalLoops,KernelRuntime.Faces[KernelRuntime.ResolveTagRecord(retained).Slot].LoopCount);
        tool=BooleanOperationTests.Block(1,.5,-.1,1,2,2);
        Assert.Equal(0,KernelRuntime.BodyBoolean(body,1,&tool,&options,&count,&output));KernelRuntime.MemoryFree(output);
        Assert.True(KernelRuntime.IsValidTag(retained));Assert.Equal(originalLoops,KernelRuntime.Faces[KernelRuntime.ResolveTagRecord(retained).Slot].LoopCount);
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out var restored));Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&restored));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void ContactBandCannotCrossAnUnselectedBottomFace()
    {
        var body=Roof(-5);var edge=SelectedEdge(body);var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count;
        Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.4,&set,&count,null));
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,fe,ft;int* output;
        Assert.NotEqual(0,KernelRuntime.BodyFixBlends(body,&fix,&count,&output,null,null,&fault,&fe,&ft));
        Assert.Equal(0,count);Assert.True(output==null);BooleanOperationTests.Counts(body,6,12,8);Assert.True(KernelRuntime.IsValidTag(edge));
    }

    [Fact]
    public void WarmSingleBlendAllocatesNoManagedMemory()
    {
        var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};
        int count,fault,fe,ft;var warm=Roof();var edge=SelectedEdge(warm);
        Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.1,&set,&count,null));
        Assert.Equal(0,KernelRuntime.BodyFixBlends(warm,&fix,&count,null,null,null,&fault,&fe,&ft));Assert.Equal(0,KernelRuntime.EntityDelete(1,&warm));
        var body=Roof();edge=SelectedEdge(body);Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.1,&set,&count,null));
        var before=GC.GetAllocatedBytesForCurrentThread();var error=KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&fe,&ft);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(0,error);Assert.Equal(0,allocated);Assert.Equal(1,count);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
    }

    [Fact]
    public void FilletCanBeCutThroughItsBlendAndReloadedForClassification()
    {
        var body=Roof();var edge=SelectedEdge(body);var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count;
        Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.1,&set,&count,null));
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,fe,ft;
        Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&fe,&ft));
        var tool=BooleanOperationTests.Block(1,.5,-.1,1,2,2);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int* products;
        Assert.Equal(0,KernelRuntime.BodyBoolean(body,1,&tool,&options,&count,&products));
        try{Assert.Equal(1,count);Assert.Equal(body,products[0]);BooleanOperationTests.Counts(body,7,15,10);}finally{KernelRuntime.MemoryFree(products);}
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out var restored));Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
        Assert.True(KernelRuntime.TryResolveBodySlot(restored,out var slot));Assert.False(KernelRuntime.Xt.IsOpaque(slot));
        var point=new PK_VECTOR_s();point.coord[0]=.25;point.coord[1]=point.coord[2]=.5;int enclosure,topology;
        Assert.Equal(0,KernelRuntime.BodyContainsVector(restored,point,&enclosure,&topology));Assert.Equal(ParasolidConstants.PK_enclosure_inside_c,enclosure);
        point.coord[0]=.75;Assert.Equal(0,KernelRuntime.BodyContainsVector(restored,point,&enclosure,&topology));Assert.Equal(ParasolidConstants.PK_enclosure_outside_c,enclosure);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&restored));Assert.Equal(0,KernelRuntime.Bodies.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void ContactReceiveRejectsMissingUvDataWithoutPublishingOrLeaking()
    {
        var body=RoundedRoof();var document=XtText.DecodeDocument(XtBrepImportTests.Transmit(body));
        var contact=document.Nodes.First(n=>n.Type==(int)XtNodeTypes.Intersection&&document.Nodes.Any(b=>b.Index==n.Fields[8].Pointer&&b.Type==(int)XtNodeTypes.BlendBound));
        contact.Fields[^1]=XtFieldValue.Ptr(document.Nodes.Max(n=>n.Index)+1);
        var blocks=KernelRuntime.BlocksLiveBytes;var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        Assert.Equal(ParasolidConstants.PK_ERROR_corrupt_file,XtBrepImportTests.Receive(XtText.Encode(document),out var received));Assert.Equal(0,received);
        Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void ContactReceiveAllocationFailuresReleaseUnboundPayloads()
    {
        var body=RoundedRoof();var text=XtBrepImportTests.Transmit(body);var blocks=KernelRuntime.BlocksLiveBytes;
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        for(var budget=0;budget<=24;budget++)
        {
            KernelRuntime.InjectAllocationFailure(budget);int error,received;
            try{error=XtBrepImportTests.Receive(text,out received);}finally{KernelRuntime.InjectAllocationFailure(-1);}
            if(error==0)Assert.Equal(0,KernelRuntime.EntityDelete(1,&received));else Assert.Equal(0,received);
            Assert.True(blocks==KernelRuntime.BlocksLiveBytes,$"budget={budget} error={error} bytes={blocks}/{KernelRuntime.BlocksLiveBytes} curves={curves}/{KernelRuntime.Curves.AliveCount} surfaces={surfaces}/{KernelRuntime.Surfaces.AliveCount}");Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
        }
    }

    [Fact]
    public void ContactReceiveAcceptsMoreThan256ActualChartAnchors()
    {
        const int count=257;var body=RoundedRoof();var document=XtText.DecodeDocument(XtBrepImportTests.Transmit(body));
        var contact=document.Nodes.First(n=>n.Type==(int)XtNodeTypes.Intersection&&document.Nodes.Any(b=>b.Index==n.Fields[8].Pointer&&b.Type==(int)XtNodeTypes.BlendBound));
        var boundary=document.Nodes.Single(n=>n.Index==contact.Fields[8].Pointer);var side=1-boundary.Fields[7].Integer;
        KernelRuntime.TryResolveBodySlot(body,out var bodySlot);var edge=KernelRuntime.Bodies[bodySlot].FirstEdgeBody;var tag=0;
        for(var i=0;i<KernelRuntime.Bodies[bodySlot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var record=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[edge].CurveTag)];
            if(record.Class==CurveClass.ICurve&&KernelRuntime.ICurveDataPool[record.DataIndex].IsBlendContact!=0&&KernelRuntime.ICurveDataPool[record.DataIndex].BlendContactSide==side){tag=record.Header.Tag;break;}
        }
        Assert.NotEqual(0,tag);var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(tag)];var data=KernelRuntime.ICurveDataPool[curve.DataIndex];
        var chart=document.Nodes.Single(n=>n.Index==contact.Fields[9].Pointer);var old=chart.Fields;chart.Fields=new XtFieldValue[7+count];old.AsSpan(0,7).CopyTo(chart.Fields);chart.Fields[2]=XtFieldValue.Int(count);chart.VariableLength=count;
        var uv=document.Nodes.Single(n=>n.Index==contact.Fields[^1].Pointer);uv.Fields=new XtFieldValue[1+2*count];uv.Fields[0]=XtFieldValue.Unsigned((long)IntersectionUvType.First);uv.VariableLength=2*count;
        for(var i=0;i<count;i++)
        {
            var point=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(tag,curve.TMin+(curve.TMax-curve.TMin)*i/(count-1),0,&point));
            var value=i==0?old[7]:i==count-1?old[^1]:XtFieldValue.Vec(point.coord[0],point.coord[1],point.coord[2]);chart.Fields[7+i]=value;
            point.coord[0]=value.Vector.X;point.coord[1]=value.Vector.Y;point.coord[2]=value.Vector.Z;var at=new PK_UV_s();
            // The manufactured roof has x=u, y=v, also on its linear
            // extension. Full contact charts now extend beyond the finite
            // cap; a bounded global inverse deliberately cannot recover
            // those anchors. Supply the independent manufactured UV instead.
            if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(data.Surface0Tag)].Class==SurfaceClass.BSurface){at.param[0]=point.coord[0];at.param[1]=point.coord[1];}
            else Assert.Equal(0,KernelRuntime.SurfParameteriseVector(data.Surface0Tag,point,&at));
            uv.Fields[1+2*i]=XtFieldValue.RealValue(at.param[0]);uv.Fields[2+2*i]=XtFieldValue.RealValue(at.param[1]);
        }
        var blocks=KernelRuntime.BlocksLiveBytes;Assert.Equal(0,XtBrepImportTests.Receive(XtText.Encode(document),out var received));
        Assert.True(KernelRuntime.TryResolveBodySlot(received,out var slot));Assert.False(KernelRuntime.Xt.IsOpaque(slot));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&received));Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);
    }

    [Fact]
    public void NativeContactEndpointRequiresItsUVRootAndCompletePrecisionNeighbourhood()
    {
        var body=RoundedRoof(true);
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));
        var face=KernelRuntime.Bodies[slot].FirstFaceBody;var blend=0;
        for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var tag=KernelRuntime.Faces[face].SurfTag;
            if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(tag)].Class==SurfaceClass.BlendSurface)blend=tag;
        }
        Assert.NotEqual(0,blend);var attached=blend;blend=0;Assert.Equal(0,KernelRuntime.EntityCopy(attached,&blend));
        var definition=new PK_PLANE_sf_s();definition.basis_set.location.coord[0]=.47;
        definition.basis_set.axis.coord[0]=1;definition.basis_set.ref_direction.coord[1]=1;var plane=0;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&definition,&plane));
        var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_pline_c,have_uvbox_1=1};
        var support=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(blend)];
        options.uvbox_1.param[0]=support.UMin;options.uvbox_1.param[1]=support.VMin;options.uvbox_1.param[2]=support.UMax;options.uvbox_1.param[3]=support.VMax;
        int np,nc;PK_VECTOR_s* points;int* curves;PK_INTERVAL_s* ranges;int* kinds;
        Assert.Equal(0,KernelRuntime.SurfIntersectSurf(blend,plane,&options,&np,&points,&nc,&curves,&ranges,&kinds));
        try
        {
            Assert.Equal(1,nc);Assert.Equal(CurveClass.ICurve,KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(curves[0])].Class);
            var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;var checkedEnds=0;
            for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
            {
                var record=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[edge].CurveTag)];
                if(record.Class!=CurveClass.ICurve||KernelRuntime.ICurveDataPool[record.DataIndex].IsBlendContact==0)continue;
                Assert.Equal(0,KernelRuntime.TestNativeContactEnd(edge,curves[0],plane,ranges[0].value[0],ranges[0].value[1],0,out var handled));
                Assert.True(handled);checkedEnds++;
                for(byte invalid=1;invalid<=4;invalid++)
                {
                    Assert.Equal(0,KernelRuntime.TestNativeContactEnd(edge,curves[0],plane,ranges[0].value[0],ranges[0].value[1],invalid,out handled));
                    Assert.False(handled);
                }
            }
            Assert.Equal(2,checkedEnds);
        }
        finally{KernelRuntime.MemoryFree(points);KernelRuntime.MemoryFree(curves);KernelRuntime.MemoryFree(ranges);KernelRuntime.MemoryFree(kinds);}
    }

    private static int RoundedRoof(bool curvedWall=false)
    {
        var body=Roof(.4,curvedWall);var edge=SelectedEdge(body);var set=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count;
        Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,.1,&set,&count,null));
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,fe,ft;
        Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&fe,&ft));return body;
    }

    [Theory]
    [InlineData(0,.1,false,false)]
    [InlineData(.4,.1,false,false)]
    [InlineData(-.2,.15,false,false)]
    [InlineData(.4,.1,true,false)]
    [InlineData(.4,.1,false,true)]
    [InlineData(.4,.1,true,true)]
    public void SingleNurbsEdgeProducesClosedSolidWithProceduralBlend(double bulge,double radius,bool curvedWall,bool concave)
    {
        var body=Roof(bulge,curvedWall,concave);var edge=SelectedEdge(body,concave);var original=edge;
        var boundaryTrace=bulge==0?new List<KernelRuntime.BoundaryTrace>():null;
        var retainedFins=new Dictionary<FinSlot,(FaceSlot Face,EdgeSlot Edge)>();
        if(boundaryTrace!=null)
        {
            var selected=KernelRuntime.ResolveTagRecord(original).Slot;var record=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];
            var f=record.FirstFaceBody;
            for(var i=0;i<record.FaceCountBody;i++,f=KernelRuntime.Faces[f].NextInBody)
            {
                var loop=KernelRuntime.Faces[f].FirstLoop;
                for(var l=0;l<KernelRuntime.Faces[f].LoopCount;l++,loop=KernelRuntime.Loops[loop].NextInFace)
                {
                    var fin=KernelRuntime.Loops[loop].FirstFin;
                    for(var k=0;k<KernelRuntime.Loops[loop].FinCount;k++,fin=KernelRuntime.Fins[fin].NextInLoop)
                    {
                        var old=KernelRuntime.Fins[fin].Edge;if(old!=selected)retainedFins.Add(fin,(f,old));
                    }
                }
            }
            KernelRuntime.TraceFiniteBoundary=boundaryTrace.Add;
        }
        var options=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count;
        Assert.Equal(0,KernelRuntime.EdgeSetBlendConstant(1,&edge,radius,&options,&count,null));
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-7};int fault,fe,ft;int* blends;
        var error=KernelRuntime.BodyFixBlends(body,&fix,&count,&blends,null,null,&fault,&fe,&ft);
        Assert.True(error==0,$"fix={error}, fault={fault}, blend={KernelRuntime.LastRegularBlendStage}, sheet={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, t={KernelRuntime.LastSheetParameter:R}");
        try
        {
            Assert.Equal(1,count);Assert.Equal(ParasolidConstants.PK_blend_fault_no_fault_c,fault);Assert.False(KernelRuntime.IsValidTag(original));
            int surface;Assert.Equal(0,KernelRuntime.FaceAskSurf(blends[0],&surface));
            Assert.Equal(SurfaceClass.BlendSurface,KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)].Class);
            BooleanOperationTests.Counts(body,concave?9:7,concave?21:15,concave?14:10);
            KernelRuntime.TryResolveBodySlot(body,out var slot);var e=KernelRuntime.Bodies[slot].FirstEdgeBody;
            var contacts=0;
            for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,e=KernelRuntime.Edges[e].NextInBody)
            {
                Assert.Equal(2,KernelRuntime.Edges[e].FinCount);
                Assert.NotEqual(KernelRuntime.Fins[KernelRuntime.Edges[e].FirstFinEdge].Sense,KernelRuntime.Fins[KernelRuntime.Edges[e].LastFinEdge].Sense);
                var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[e].CurveTag)];
                if(curve.Class==CurveClass.ICurve&&KernelRuntime.ICurveDataPool[curve.DataIndex].IsBlendContact!=0)contacts++;
            }
            Assert.Equal(2,contacts);
            if(boundaryTrace!=null)
            {
                foreach(var use in boundaryTrace.Where(x=>x.Stage==KernelRuntime.BoundaryTraceStage.Input&&x.SourceFin>=0))
                {
                    Assert.True(retainedFins.Remove(use.SourceFin,out var occurrence));
                    Assert.Equal(occurrence,(use.Source,use.SourceEdge));
                    Assert.Contains(boundaryTrace,x=>x.Stage==KernelRuntime.BoundaryTraceStage.Product&&x.Source==use.Source
                        &&x.Edge==use.Edge&&x.Curve==use.Curve&&x.First==use.First&&x.Last==use.Last);
                }
                Assert.Empty(retainedFins);
                var sections=boundaryTrace.Where(x=>x.Stage==KernelRuntime.BoundaryTraceStage.AnalyticSectionEnds).ToArray();
                Assert.Equal(2,sections.Length);
                foreach(var section in sections)
                {
                    Assert.Equal(0,section.First);
                    Assert.InRange(section.Last,Math.PI*.5-1e-12,Math.PI*.5+1e-12);
                    Assert.Contains(boundaryTrace,x=>x.Stage==KernelRuntime.BoundaryTraceStage.NativeSectionChart&&x.Curve==section.Curve
                        &&x.UvStart.param[0]==x.UvEnd.param[0]&&Math.Min(x.UvStart.param[1],x.UvEnd.param[1])==0
                        &&Math.Max(x.UvStart.param[1],x.UvEnd.param[1])==1);
                    Assert.Contains(boundaryTrace,x=>x.Stage==KernelRuntime.BoundaryTraceStage.Product&&x.Curve==section.Curve
                        &&x.Generation==section.Generation&&x.First==section.First&&x.Last==section.Last);
                }
            }
        }
        finally{if(blends!=null)KernelRuntime.MemoryFree(blends);}
        if(!curvedWall&&!concave&&bulge>0)RoundTripCopyMirror(ref body);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
        Assert.True(KernelRuntime.Bodies.AliveCount==0&&KernelRuntime.Curves.AliveCount==0&&KernelRuntime.Surfaces.AliveCount==0,
            $"remaining bodies={KernelRuntime.Bodies.AliveCount}, curves={KernelRuntime.Curves.AliveCount}, surfaces={KernelRuntime.Surfaces.AliveCount}");
    }

    internal static int Roof(double bulge=.4,bool curvedWall=false,bool concave=false)
    {
        var body=BooleanOperationTests.Block(.5,.5,0,1,1,1);
        if(concave)
        {
            var tool=BooleanOperationTests.Block(.5,.85,.5,2,.7,1);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int n;int* result;
            Assert.Equal(0,KernelRuntime.BodyBoolean(body,1,&tool,&options,&n,&result));try{Assert.Equal(1,n);body=result[0];}finally{KernelRuntime.MemoryFree(result);}
        }
        KernelRuntime.TryResolveBodySlot(body,out var slot);var face=KernelRuntime.Bodies[slot].FirstFaceBody;var top=0;
        var wall=0;
        for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var p=new PK_PLANE_sf_s();Assert.Equal(0,KernelRuntime.PlaneAsk(KernelRuntime.Faces[face].SurfTag,&p));
            if(Math.Abs(p.basis_set.axis.coord[2])>.9&&Math.Abs(p.basis_set.location.coord[2]-(concave?.5:1))<1e-10)top=KernelRuntime.Faces[face].Header.Tag;
            if(Math.Abs(p.basis_set.axis.coord[1])>.9&&Math.Abs(p.basis_set.location.coord[1]-(concave?.5:0))<1e-10)wall=KernelRuntime.Faces[face].Header.Tag;
        }
        Assert.NotEqual(0,top);double* poles=stackalloc double[45];double* knots=stackalloc double[2]{0,1};
        int* um=stackalloc int[2]{5,5};int* vm=stackalloc int[2]{3,3};
        for(var i=0;i<5;i++)for(var j=0;j<3;j++){var at=3*(i*3+j);poles[at]=i/4.0;poles[at+1]=(concave?.5:0)+(concave?.5:1)*j/2.0;poles[at+2]=(concave?.5:1)+(i==2&&j==1?bulge:0);}
        var sf=new PK_BSURF_sf_s{u_degree=4,v_degree=2,n_u_vertices=5,n_v_vertices=3,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,
            u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,form=ParasolidConstants.PK_BSURF_form_arbitrary_c,
            u_knot_type=ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=ParasolidConstants.PK_knot_non_uniform_c,
            self_intersecting=ParasolidConstants.PK_self_intersect_false_c,convexity=ParasolidConstants.PK_convexity_unset_c};
        int surface;Assert.Equal(0,KernelRuntime.BSurfCreate(&sf,&surface));byte sense=1;
        int old;Assert.Equal(0,KernelRuntime.FaceAskSurf(top,&old));Assert.Equal(0,KernelRuntime.TopologyDetachGeometry(top));
        Assert.Equal(0,KernelRuntime.FaceAttachSurfaces(1,&top,&surface,&sense));Assert.Equal(0,KernelRuntime.EntityDelete(1,&old));
        if(curvedWall)
        {
            for(var i=0;i<5;i++)for(var j=0;j<3;j++){var at=3*(i*3+j);poles[at]=i/4.0;poles[at+1]=(concave?.5:0)+(i==2&&j==1?(concave?-.15:-.3):0);poles[at+2]=(concave?.5:0)+(concave?.5:1)*j/2.0;}
            sense=concave?(byte)0:(byte)1;
            Assert.Equal(0,KernelRuntime.BSurfCreate(&sf,&surface));Assert.Equal(0,KernelRuntime.FaceAskSurf(wall,&old));Assert.Equal(0,KernelRuntime.TopologyDetachGeometry(wall));
            Assert.Equal(0,KernelRuntime.FaceAttachSurfaces(1,&wall,&surface,&sense));Assert.Equal(0,KernelRuntime.EntityDelete(1,&old));
        }
        return body;
    }

    private static void RoundTripCopyMirror(ref int body)
    {
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out var received));
        Assert.True(KernelRuntime.TryResolveBodySlot(received,out var slot));Assert.False(KernelRuntime.Xt.IsOpaque(slot));
        var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[edge].CurveTag)];
            if(curve.Class!=CurveClass.Line)Assert.NotEqual(0,KernelRuntime.Edges[edge].HasParameterRange);
        }
        int copy;var original=body;Assert.Equal(0,KernelRuntime.EntityCopy(received,&copy));Assert.Equal(0,KernelRuntime.EntityDelete(1,&received));Assert.Equal(0,KernelRuntime.EntityDelete(1,&original));
        var sf=new PK_TRANSF_sf_s();sf.matrix[0]=-2;sf.matrix[5]=sf.matrix[10]=2;sf.matrix[3]=3;sf.matrix[7]=5;sf.matrix[11]=7;sf.matrix[15]=1;
        int transform;Assert.Equal(0,KernelRuntime.TransfCreate(&sf,&transform));int n;int* replaced;byte* exact;
        Assert.Equal(0,KernelRuntime.BodyTransform(copy,transform,0,&n,&replaced,&exact));if(replaced!=null)KernelRuntime.MemoryFree(replaced);if(exact!=null)KernelRuntime.MemoryFree(exact);
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(copy),out received));Assert.Equal(0,KernelRuntime.EntityDelete(1,&copy));
        Assert.True(KernelRuntime.TryResolveBodySlot(received,out slot));Assert.False(KernelRuntime.Xt.IsOpaque(slot));
        var point=new PK_VECTOR_s();point.coord[0]=2.2;point.coord[1]=5.8;point.coord[2]=8;int enclosure,topology;
        Assert.Equal(0,KernelRuntime.BodyContainsVector(received,point,&enclosure,&topology));Assert.Equal(ParasolidConstants.PK_enclosure_inside_c,enclosure);
        body=received;
    }

    internal static int SelectedEdge(int body,bool concave=false)
    {
        KernelRuntime.TryResolveBodySlot(body,out var slot);var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var e=KernelRuntime.Edges[edge];var a=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[e.StartVertex].PointTag)].Position;
            var b=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[e.EndVertex].PointTag)].Position;
            var y=concave?.5:0;var z=concave?.5:1;
            if(Math.Abs(a.Y-y)+Math.Abs(b.Y-y)+Math.Abs(a.Z-z)+Math.Abs(b.Z-z)<1e-10)return e.Header.Tag;
        }
        throw new Exception("missing roof edge");
    }
}

using System.Text;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class LocalOperationTests:IDisposable
{
    public LocalOperationTests()
    {
        KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));
    }
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false,false)] [InlineData(false,true)]
    [InlineData(true,false)] [InlineData(true,true)]
    public void DirectAndReloadedNativePrimaryFinsDriveTheSameParallelRollingNetwork(bool reload,bool reverse)
    {
        var before=Counts();var body=Block();Check(body);
        if(reload)
        {
            Assert.Equal(0,XtBrepImportTests.Receive(Transmit(body),out var restored));
            Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));body=restored;Check(body);
        }
        var selected=Tags(body,1).Where(tag=>
        {
            var edge=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(tag).Slot];
            var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(edge.CurveTag)];
            return Math.Abs(KernelRuntime.LineDataPool[curve.DataIndex].AxisZ)>.99;
        }).ToArray();
        Assert.Equal(4,selected.Length);if(reverse)Array.Reverse(selected);
        var guard=KernelRuntime.ForbidLegacyRegularBlendAssembly;KernelRuntime.ForbidLegacyRegularBlendAssembly=true;
        try{SetBlend(selected,false,.4);Assert.Equal(4,Fix(body));}
        finally{KernelRuntime.ForbidLegacyRegularBlendAssembly=guard;}
        BooleanOperationTests.Counts(body,10,24,16);Check(body);
        int copy;Assert.Equal(0,KernelRuntime.EntityCopy(body,&copy));Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Check(copy);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&copy));Assert.Equal(before,Counts());
    }

    [Theory]
    [InlineData(false,false,true)] [InlineData(false,true,false)]
    [InlineData(true,false,false)] [InlineData(true,true,true)]
    public void OrientedSurfaceQueryComposesXtFaceAndSurfaceSensesWithoutMutation(bool negativeFace,bool negativeSurface,bool expected)
    {
        var body=Block();var face=Record(body).FirstFaceBody;ref var record=ref KernelRuntime.Faces[face];
        ref var geometry=ref KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(record.SurfTag)];
        record.Orientation=negativeFace?ParasolidConstants.PK_TOPOL_sense_negative_c:ParasolidConstants.PK_TOPOL_sense_positive_c;
        geometry.Sense=negativeSurface?ParasolidConstants.PK_TOPOL_sense_negative_c:ParasolidConstants.PK_TOPOL_sense_positive_c;
        var beforeFace=record.Orientation;var beforeSurface=geometry.Sense;int surface=0;byte orientation=0;
        Assert.Equal(0,KernelRuntime.FaceAskOrientedSurf(record.Header.Tag,&surface,&orientation));
        Assert.Equal(record.SurfTag,surface);Assert.Equal(expected,orientation!=0);
        Assert.Equal(beforeFace,record.Orientation);Assert.Equal(beforeSurface,geometry.Sense);
    }

    [Theory]
    [InlineData(.5)] [InlineData(-.5)]
    public void ConvexOffsetMovesPlanesAndRetainsEveryBoundaryHandle(double distance)
    {
        var body=Block();var before=Bounds(body);var faces=Tags(body,0);var edges=Tags(body,1);var vertices=Tags(body,2);
        var options=new PK_BODY_offset_o_s{o_t_version=12};var tracking=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();
        Assert.Equal(0,KernelRuntime.BodyOffset2(body,distance,1e-7,&options,&tracking,&local));
        Assert.Equal(ParasolidConstants.PK_local_status_ok_c,local.status);Assert.Equal(0,tracking.n_track_records);
        Assert.Equal(faces.Order().ToArray(),Tags(body,0).Order().ToArray());Assert.Equal(edges.Order().ToArray(),Tags(body,1).Order().ToArray());Assert.Equal(vertices.Order().ToArray(),Tags(body,2).Order().ToArray());
        var after=Bounds(body);for(var i=0;i<3;i++){Assert.Equal(before[i]-distance,after[i],10);Assert.Equal(before[i+3]+distance,after[i+3],10);}
        Free(&tracking,&local);Check(body);
    }

    [Fact]
    public void IndividualFaceOffsetRetainsOtherPlanesAndRejectsForeignFaces()
    {
        var body=Block();var record=Record(body);var face=KernelRuntime.GetFaceRecord(record.FirstFaceBody);var tag=face.Header.Tag;
        var oldPlane=new PK_PLANE_sf_s();Assert.Equal(0,KernelRuntime.PlaneAsk(face.SurfTag,&oldPlane));
        var options=new PK_FACE_offset_o_s{o_t_version=11};var tracking=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();double distance=.75;
        Assert.Equal(0,KernelRuntime.FaceOffset2(1,&tag,&distance,1e-7,&options,&tracking,&local));
        int surface=0;Assert.Equal(0,KernelRuntime.FaceAskSurf(tag,&surface));var plane=new PK_PLANE_sf_s();Assert.Equal(0,KernelRuntime.PlaneAsk(surface,&plane));
        var sign=face.Orientation==ParasolidConstants.PK_TOPOL_sense_negative_c?-1:1;
        for(var i=0;i<3;i++)Assert.Equal(oldPlane.basis_set.location.coord[i]+distance*sign*oldPlane.basis_set.axis.coord[i],plane.basis_set.location.coord[i],10);
        Free(&tracking,&local);Check(body);
        var other=Block();var foreign=Tags(other,0)[0];int* both=stackalloc int[2]{tag,foreign};double* distances=stackalloc double[2]{.1,.1};var before=Transmit(body);
        Assert.Equal(ParasolidConstants.PK_ERROR_not_in_same_body,KernelRuntime.FaceOffset2(2,both,distances,1e-7,&options,&tracking,&local));Assert.Equal(before,Transmit(body));
    }

    [Theory]
    [InlineData(false,.3)] [InlineData(false,-.3)] [InlineData(true,.3)] [InlineData(true,-.3)]
    public void HollowBuildsCavityOrAnnularPierceFaceAndTracksNewWall(bool pierce,double distance)
    {
        var body=Block();var original=Tags(body,0);var face=original[0];
        var options=new PK_BODY_hollow_o_s{o_t_version=12};if(pierce){options.n_pierce_faces=1;options.pierce_faces=&face;}
        var tracking=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();
        Assert.Equal(0,KernelRuntime.BodyHollow2(body,distance,1e-7,&options,&tracking,&local));
        var record=Record(body);Assert.Equal(pierce?11:12,record.FaceCountBody);Assert.Equal(24,record.EdgeCountBody);Assert.Equal(16,record.VertexCountBody);
        Assert.Equal(pierce?2:3,record.RegionCount);Assert.Equal(pierce?2:4,record.ShellCount);Assert.Equal(pierce?5:6,tracking.n_track_records);
        Assert.Equal(record.FirstRegion,KernelRuntime.Shells[record.FirstShell].Region);
        Assert.Equal((byte)0,KernelRuntime.Regions[record.FirstRegion].IsSolid);
        foreach(var tag in original)Assert.Contains(tag,Tags(body,0));
        for(var i=0;i<tracking.n_track_records;i++)
        {
            var track=tracking.track_records[i];Assert.Equal(ParasolidConstants.PK_TOPOL_track_create_c,track.track);
            Assert.Equal(1,track.n_original_topols);Assert.Equal(1,track.n_product_topols);Assert.Contains(track.original_topols[0],original);Assert.DoesNotContain(track.product_topols[0],original);
        }
        if(pierce){int n=0;int* loops=null;Assert.Equal(0,KernelRuntime.FaceAskLoops(face,&n,&loops));Assert.Equal(2,n);KernelRuntime.MemoryFree(loops);}
        Free(&tracking,&local);Check(body);
    }

    [Theory]
    [InlineData(0,false)] [InlineData(0,true)] [InlineData(1,false)] [InlineData(1,true)] [InlineData(2,false)] [InlineData(2,true)]
    public void AnalyticOffsetsAndHollowsKeepAnalyticSurfaces(int shape,bool hollow)
    {
        int body=0;Assert.Equal(0,shape==0?KernelRuntime.BodyCreateSolidSphere(3,null,&body):shape==1?KernelRuntime.BodyCreateSolidCyl(3,6,null,&body):KernelRuntime.BodyCreateSolidTorus(5,1,null,&body));
        if(hollow)
        {
            var options=new PK_BODY_hollow_o_s{o_t_version=12};var track=new PK_TOPOL_track_r_s();var result=new PK_TOPOL_local_r_s();
            Assert.Equal(0,KernelRuntime.BodyHollow2(body,-.2,1e-7,&options,&track,&result));Assert.Equal(3,Record(body).RegionCount);Assert.Equal(4,Record(body).ShellCount);Free(&track,&result);
        }
        else Assert.Equal(0,KernelRuntime.BodyOffset(body,.2,1e-7,1));
        var classes=SurfaceClasses(body);Assert.DoesNotContain(SurfaceClass.BSurface,classes);Assert.DoesNotContain(SurfaceClass.Offset,classes);Check(body);
    }

    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public void BlendsConstructEdgeAndCornerPatches(bool chamfer,bool all)
    {
        var before=Counts();
        var body=Block();var originalFaces=Tags(body,0);var edges=Tags(body,1);var selection=all?edges:new[]{edges[0]};
        SetBlend(selection,chamfer,.4);Assert.Equal(6,Record(body).FaceCountBody);
        var count=Fix(body);Assert.Equal(all?20:1,count);var record=Record(body);
        Assert.Equal(all?26:7,record.FaceCountBody);Assert.Equal(all?48:15,record.EdgeCountBody);Assert.Equal(all?24:10,record.VertexCountBody);
        foreach(var face in originalFaces)Assert.Contains(face,Tags(body,0));
        var classes=SurfaceClasses(body);
        if(chamfer)Assert.All(classes,x=>Assert.Equal(SurfaceClass.Plane,x));
        else{Assert.Equal(all?12:1,classes.Count(x=>x==SurfaceClass.Cylinder));Assert.Equal(all?8:0,classes.Count(x=>x==SurfaceClass.Sphere));}
        if(!all)foreach(var unselected in edges.Skip(1))Assert.Contains(unselected,Tags(body,1));
        Check(body);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(before,Counts());
    }

    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public void CylindricalRimBlendsUseTorusOrCone(bool chamfer,bool both)
    {
        int body=0;Assert.Equal(0,KernelRuntime.BodyCreateSolidCyl(3,6,null,&body));var edges=Tags(body,1);SetBlend(both?edges:new[]{edges[0]},chamfer,.4);
        Assert.Equal(both?2:1,Fix(body));Assert.Equal(both?5:4,Record(body).FaceCountBody);Assert.Equal(both?4:3,Record(body).EdgeCountBody);Assert.Equal(0,Record(body).VertexCountBody);
        Assert.Equal(both?2:1,SurfaceClasses(body).Count(x=>x==(chamfer?SurfaceClass.Cone:SurfaceClass.Torus)));Check(body);
    }

    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public void CylindricalBlendClassificationUsesFiniteFacesAndReturnsOwnedTopology(bool chamfer,bool both)
    {
        int body=0;Assert.Equal(0,KernelRuntime.BodyCreateSolidCyl(3,6,null,&body));var selected=Tags(body,1);
        SetBlend(both?selected:new[]{selected[0]},chamfer,.4);Fix(body);
        for(var reload=0;reload<2;reload++)
        {
            Classify(body,0,3,ParasolidConstants.PK_enclosure_inside_c,ParasolidConstants.PK_CLASS_region);
            Classify(body,3.01,3,ParasolidConstants.PK_enclosure_outside_c,ParasolidConstants.PK_CLASS_region);
            Classify(body,0,-.01,ParasolidConstants.PK_enclosure_outside_c,ParasolidConstants.PK_CLASS_region);
            Classify(body,3,3,ParasolidConstants.PK_enclosure_on_c,ParasolidConstants.PK_CLASS_face);
            Classify(body,0,0,ParasolidConstants.PK_enclosure_on_c,ParasolidConstants.PK_CLASS_face);
            Classify(body,0,6,ParasolidConstants.PK_enclosure_on_c,ParasolidConstants.PK_CLASS_face);
            foreach(var tag in Tags(body,1))
            {
                var edge=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(tag).Slot];var vector=new PK_VECTOR_s();
                Assert.Equal(0,KernelRuntime.CurveEval(edge.CurveTag,.713,0,&vector));int enclosure,topology;
                Assert.Equal(0,KernelRuntime.BodyContainsVector(body,vector,&enclosure,&topology));
                Assert.Equal(ParasolidConstants.PK_enclosure_on_c,enclosure);Assert.Equal(tag,topology);
            }
            if(reload==0)
            {
                Assert.Equal(0,XtBrepImportTests.Receive(Transmit(body),out var fresh));
                Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));body=fresh;
            }
        }
        static void Classify(int body,double radius,double height,int expected,int kind)
        {
            var query=new PK_VECTOR_s();query.coord[0]=radius;query.coord[2]=height;int enclosure,topology,actualKind;
            Assert.Equal(0,KernelRuntime.BodyContainsVector(body,query,&enclosure,&topology));Assert.Equal(expected,enclosure);
            Assert.Equal(0,KernelRuntime.EntityAskClass(topology,&actualKind));Assert.Equal(kind,actualKind);
            var slot=KernelRuntime.ResolveTagRecord(topology).Slot;
            if(kind==ParasolidConstants.PK_CLASS_face)Assert.Contains(topology,Tags(body,0));
            else
            {
                var region=Record(body).FirstRegion;var found=false;
                for(var i=0;i<Record(body).RegionCount;i++,region=KernelRuntime.Regions[region].NextInBody)found|=region==slot;
                Assert.True(found);Assert.Equal(expected==ParasolidConstants.PK_enclosure_inside_c,KernelRuntime.Regions[slot].IsSolid!=0);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnequalChamferRespectsDesignatedFirstSupportFace(bool swapFaces)
    {
        var body=Block();var sourceFaces=Tags(body,0);var edgeTag=Tags(body,1)[0];
        var edge=KernelRuntime.GetEdgeRecord(Record(body).FirstEdgeBody);Assert.Equal(edgeTag,edge.Header.Tag);
        var first=KernelRuntime.Loops[KernelRuntime.Fins[edge.FirstFinEdge].Loop].Face;
        var second=KernelRuntime.Loops[KernelRuntime.Fins[edge.LastFinEdge].Loop].Face;if(swapFaces)(first,second)=(second,first);
        var faceTag=KernelRuntime.GetFaceRecord(first).Header.Tag;var secondTag=KernelRuntime.GetFaceRecord(second).Header.Tag;
        var a=Normal(faceTag);var b=Normal(secondTag);var point=new PK_POINT_sf_s();
        Assert.Equal(0,KernelRuntime.PointAsk(KernelRuntime.GetVertexRecord(edge.StartVertex).PointTag,&point));
        var options=new PK_EDGE_set_blend_chamfer_o_s{o_t_version=1};int count=0;
        Assert.Equal(0,KernelRuntime.EdgeSetBlendChamfer(1,&edgeTag,.7,.3,&faceTag,&options,&count,null));Assert.Equal(1,Fix(body));
        var created=Tags(body,0).Single(f=>!sourceFaces.Contains(f));int surface=0;Assert.Equal(0,KernelRuntime.FaceAskSurf(created,&surface));
        var plane=new PK_PLANE_sf_s();Assert.Equal(0,KernelRuntime.PlaneAsk(surface,&plane));
        for(var k=0;k<2;k++)
        {
            var residual=0.0;var normal=k==0?b:a;var distance=k==0?.7:.3;
            for(var i=0;i<3;i++)residual+=(point.position.coord[i]-distance*normal[i]-plane.basis_set.location.coord[i])*plane.basis_set.axis.coord[i];
            Assert.InRange(Math.Abs(residual),0,1e-10);
        }
        Check(body);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnsupportedPartialCornerOrOversizedBlendRollsBackPendingDefinition(bool oversized)
    {
        var body=Block();var edges=Tags(body,1);var selected=oversized?edges:edges.Take(2).ToArray();
        if(!oversized)
        {
            var first=KernelRuntime.GetEdgeRecord(Record(body).FirstEdgeBody);var second=first.NextInBody;
            for(var i=1;i<edges.Length;i++,second=KernelRuntime.GetEdgeRecord(second).NextInBody)
            {
                var candidate=KernelRuntime.GetEdgeRecord(second);
                if(candidate.StartVertex==first.StartVertex||candidate.StartVertex==first.EndVertex||candidate.EndVertex==first.StartVertex||candidate.EndVertex==first.EndVertex)
                {selected[1]=candidate.Header.Tag;break;}
            }
        }
        SetBlend(selected,false,oversized?10:.4);var before=Snapshot(body);var pools=Counts();
        var options=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-6};int count=0,fault=0,edge=0,topol=0;int* faces=null;
        Assert.Equal(oversized?ParasolidConstants.PK_ERROR_radius_too_large:ParasolidConstants.PK_ERROR_not_implemented,
            KernelRuntime.BodyFixBlends(body,&options,&count,&faces,null,null,&fault,&edge,&topol));
        Assert.Equal(before,Snapshot(body));Assert.Equal(pools,Counts());Assert.Equal(0,count);Assert.True(faces==null);Check(body);
    }

    private static double[] Normal(int face)
    {
        int surface=0;byte sense=0;Assert.Equal(0,KernelRuntime.FaceAskOrientedSurf(face,&surface,&sense));
        var plane=new PK_PLANE_sf_s();Assert.Equal(0,KernelRuntime.PlaneAsk(surface,&plane));var result=new double[3];
        for(var i=0;i<3;i++)result[i]=plane.basis_set.axis.coord[i]*(sense==0?-1:1);return result;
    }

    [Fact]
    public void MarkUndoRestoresUnfixedBlendAndCanFixAgain()
    {
        var body=Block();var originalFaces=Tags(body,0);var originalEdges=Tags(body,1);SetBlend(new[]{originalEdges[0]},false,.5);var before=Counts();
        int mark=0;Assert.Equal(0,KernelRuntime.MarkCreate(&mark));Assert.Equal(1,Fix(body));Assert.Equal(0,KernelRuntime.MarkGoto(mark));
        Assert.Equal(before,Counts());Assert.Equal(originalFaces.Order(),Tags(body,0).Order());Assert.Equal(originalEdges.Order(),Tags(body,1).Order());
        Assert.Equal(1,Fix(body));Check(body);
    }

    [Fact]
    public void CopiedUnfixedBlendIsIndependentAndRetainsMappedFaceReference()
    {
        var body=Block();SetBlend(new[]{Tags(body,1)[0]},true,.4);int copy=0;Assert.Equal(0,KernelRuntime.EntityCopy(body,&copy));
        Assert.Equal(1,Fix(copy));Assert.Equal(6,Record(body).FaceCountBody);Assert.Equal(1,Fix(body));Check(copy);Check(body);
    }

    [Fact]
    public void UnfixedBlendExportRejectsInsteadOfSilentlyDroppingDefinition()
    {
        var body=Block();SetBlend(new[]{Tags(body,1)[0]},false,.4);
        var options=new PK_PART_transmit_o_s{o_t_version=4,transmit_format=ParasolidConstants.PK_transmit_format_text_c,transmit_version=371,
            transmit_meshes=ParasolidConstants.PK_transmit_meshes_separate_c};var block=new PK_MEMORY_block_s();
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.PartTransmitB(1,&body,&options,&block));Assert.Equal((ulong)0,block.n_bytes);Assert.True(block.bytes==null);
        Assert.Equal(1,Fix(body));Assert.NotEmpty(Transmit(body));
    }

    [Theory]
    [InlineData(0,0)] [InlineData(0,4)] [InlineData(0,15)] [InlineData(1,0)] [InlineData(1,4)] [InlineData(1,15)]
    [InlineData(2,0)] [InlineData(2,4)] [InlineData(2,15)]
    public void AllocationFailureRestoresBodyAndAllEntityPools(int operation,int budget)
    {
        var body=Block();if(operation==2)SetBlend(Tags(body,1),false,.4);var before=operation==2?Snapshot(body):Transmit(body);var pools=Counts();
        var track=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();var hollow=new PK_BODY_hollow_o_s{o_t_version=12};var offset=new PK_BODY_offset_o_s{o_t_version=12};
        var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-6};int count=0,fault=0,edge=0,topol=0;
        KernelRuntime.InjectAllocationFailure(budget);
        var error=operation switch
        {
            0=>KernelRuntime.BodyOffset2(body,.2,1e-7,&offset,&track,&local),
            1=>KernelRuntime.BodyHollow2(body,-.2,1e-7,&hollow,&track,&local),
            _=>KernelRuntime.BodyFixBlends(body,&fix,&count,null,null,null,&fault,&edge,&topol),
        };
        KernelRuntime.InjectAllocationFailure(-1);
        if(error==0){Free(&track,&local);Check(body);}
        else{Assert.Equal(before,operation==2?Snapshot(body):Transmit(body));Assert.Equal(pools,Counts());Assert.Equal(0,track.n_track_records);Assert.Equal(0,count);Check(body);}
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-10)]
    public void InvalidOffsetLeavesInputExactlyUnchanged(double distance)
    {
        var body=Block();var before=Transmit(body);var pools=Counts();Assert.NotEqual(0,KernelRuntime.BodyOffset(body,distance,1e-7,1));Assert.Equal(before,Transmit(body));Assert.Equal(pools,Counts());
    }

    [Theory]
    [InlineData(0,0)] [InlineData(0,1)] [InlineData(0,2)]
    [InlineData(1,0)] [InlineData(2,0)] [InlineData(2,1)]
    public void DetachedBoundaryGeometryRejectsLocalOperationsWithoutChangingState(int shape,int detachedKind)
    {
        var body=0;
        if(shape==0)body=Block();
        else Assert.Equal(0,shape==1?KernelRuntime.BodyCreateSolidSphere(3,null,&body):KernelRuntime.BodyCreateSolidCyl(3,6,null,&body));
        var edges=Tags(body,1);if(edges.Length>0)SetBlend(new[]{edges[0]},false,.3);
        var detached=Tags(body,detachedKind)[0];Assert.Equal(0,KernelRuntime.TopologyDetachGeometry(detached));
        var before=Snapshot(body);var pools=Counts();var points=KernelRuntime.Points.AliveCount;var returnedBlocks=KernelRuntime.ReturnOutstandingBlocks;
        var offset=new PK_BODY_offset_o_s{o_t_version=12};var hollow=new PK_BODY_hollow_o_s{o_t_version=12};
        var faceOffset=new PK_FACE_offset_o_s{o_t_version=11};var fix=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-6};
        var face=Tags(body,0)[0];var distance=.2;
        for(var operation=0;operation<4;operation++)
        {
            var tracking=new PK_TOPOL_track_r_s();var local=new PK_TOPOL_local_r_s();int count=17,fault=0,faultEdge=0,faultTopol=0;int* output=(int*)1;
            var error=operation switch
            {
                0=>KernelRuntime.BodyOffset2(body,distance,1e-7,&offset,&tracking,&local),
                1=>KernelRuntime.BodyHollow2(body,-distance,1e-7,&hollow,&tracking,&local),
                2=>KernelRuntime.FaceOffset2(1,&face,&distance,1e-7,&faceOffset,&tracking,&local),
                _=>KernelRuntime.BodyFixBlends(body,&fix,&count,&output,null,null,&fault,&faultEdge,&faultTopol),
            };
            Assert.Equal(ParasolidConstants.PK_ERROR_missing_geom,error);Assert.Equal(0,tracking.n_track_records);Assert.True(tracking.track_records==null);
            if(operation==3){Assert.Equal(0,count);Assert.True(output==null);}
            Assert.Equal(before,Snapshot(body));Assert.Equal(pools,Counts());Assert.Equal(points,KernelRuntime.Points.AliveCount);Assert.Equal(returnedBlocks,KernelRuntime.ReturnOutstandingBlocks);
        }
        if(edges.Length>0)
        {
            var options=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};int count=17;int* output=(int*)1;var edge=edges[0];
            Assert.Equal(ParasolidConstants.PK_ERROR_missing_geom,KernelRuntime.EdgeSetBlendConstant(1,&edge,.4,&options,&count,&output));
            Assert.Equal(0,count);Assert.True(output==null);Assert.Equal(before,Snapshot(body));Assert.Equal(returnedBlocks,KernelRuntime.ReturnOutstandingBlocks);
        }
    }

    private static int Block(){int body=0;Assert.Equal(0,KernelRuntime.BodyCreateSolidBlock(4,6,8,null,&body));return body;}
    private static BodyRecord Record(int body){Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));return KernelRuntime.GetBodyRecord(slot);}
    private static int[] Tags(int body,int kind)
    {
        int count=0;int* values=null;Assert.Equal(0,kind==0?KernelRuntime.BodyAskFaces(body,&count,&values):kind==1?KernelRuntime.BodyAskEdges(body,&count,&values):KernelRuntime.BodyAskVertices(body,&count,&values));
        try{return new ReadOnlySpan<int>(values,count).ToArray();}finally{if(values!=null)KernelRuntime.MemoryFree(values);}
    }
    private static SurfaceClass[] SurfaceClasses(int body)
    {
        var result=new SurfaceClass[Record(body).FaceCountBody];var face=Record(body).FirstFaceBody;
        for(var i=0;i<result.Length;i++,face=KernelRuntime.GetFaceRecord(face).NextInBody)result[i]=KernelRuntime.GetSurfaceByTag(KernelRuntime.GetFaceRecord(face).SurfTag).Class;return result;
    }
    private static double[] Bounds(int body)
    {
        var result=new[]{double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity,double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity};var vertex=Record(body).FirstVertexBody;
        for(var i=0;i<Record(body).VertexCountBody;i++,vertex=KernelRuntime.GetVertexRecord(vertex).NextInBody)
        {
            var sf=new PK_POINT_sf_s();Assert.Equal(0,KernelRuntime.PointAsk(KernelRuntime.GetVertexRecord(vertex).PointTag,&sf));
            for(var a=0;a<3;a++){result[a]=Math.Min(result[a],sf.position.coord[a]);result[a+3]=Math.Max(result[a+3],sf.position.coord[a]);}
        }
        return result;
    }
    private static void SetBlend(int[] edges,bool chamfer,double radius)
    {
        var round=new PK_EDGE_set_blend_constant_o_s{o_t_version=2};var flat=new PK_EDGE_set_blend_chamfer_o_s{o_t_version=1};int count=0;int* marked=null;
        fixed(int* values=edges)Assert.Equal(0,chamfer?KernelRuntime.EdgeSetBlendChamfer(edges.Length,values,radius,radius,null,&flat,&count,&marked):KernelRuntime.EdgeSetBlendConstant(edges.Length,values,radius,&round,&count,&marked));
        Assert.Equal(edges.Length,count);Assert.Equal(edges,new ReadOnlySpan<int>(marked,count).ToArray());KernelRuntime.MemoryFree(marked);
    }
    private static int Fix(int body)
    {
        var options=new PK_BODY_fix_blends_o_s{o_t_version=31,tolerance=1e-6};int count=0,fault=0,edge=0,topol=0;int* blends=null;int* replaced=null;PK_FACE_array_s* unders=null;
        Assert.Equal(0,KernelRuntime.BodyFixBlends(body,&options,&count,&blends,&unders,&replaced,&fault,&edge,&topol));Assert.Equal(ParasolidConstants.PK_blend_fault_no_fault_c,fault);
        for(var i=0;i<count;i++){Assert.InRange(unders[i].length,2,3);Assert.True(replaced[i]>0);int cls=0;Assert.Equal(0,KernelRuntime.EntityAskClass(blends[i],&cls));Assert.Equal(ParasolidConstants.PK_CLASS_face,cls);KernelRuntime.MemoryFree(unders[i].array);}
        KernelRuntime.MemoryFree(blends);KernelRuntime.MemoryFree(replaced);KernelRuntime.MemoryFree(unders);return count;
    }
    private static void Free(PK_TOPOL_track_r_s* track,PK_TOPOL_local_r_s* local){Assert.Equal(0,KernelRuntime.TopolTrackResultFree(track));Assert.Equal(0,KernelRuntime.TopolLocalResultFree(local));}
    private static void Check(int body)
    {
        var record=Record(body);var edge=record.FirstEdgeBody;
        for(var i=0;i<record.EdgeCountBody;i++,edge=KernelRuntime.GetEdgeRecord(edge).NextInBody)
        {
            var e=KernelRuntime.GetEdgeRecord(edge);Assert.Equal(2,e.FinCount);Assert.Equal('+',KernelRuntime.GetFinRecord(e.FirstFinEdge).Sense);Assert.NotEqual(KernelRuntime.GetFinRecord(e.FirstFinEdge).Sense,KernelRuntime.GetFinRecord(e.LastFinEdge).Sense);
        }
        var options=new PK_BODY_check_o_s{o_t_version=6,max_faults=32,geom=ParasolidConstants.PK_check_geom_no_c,bgeom=ParasolidConstants.PK_check_bgeom_no_c,
            mesh=ParasolidConstants.PK_check_mesh_no_c,top_geo=ParasolidConstants.PK_check_top_geo_no_c,size_box=ParasolidConstants.PK_check_size_box_no_c,
            fa_X=ParasolidConstants.PK_check_fa_X_no_c,fa_fa=ParasolidConstants.PK_check_fa_fa_no_c,nmnl_geom=ParasolidConstants.PK_check_nmnl_geom_no_c,
            attribs=ParasolidConstants.PK_check_attribs_no_c,returns=ParasolidConstants.PK_check_returns_0_c,extra_faults=ParasolidConstants.PK_check_extra_faults_0_c,
            loops=ParasolidConstants.PK_check_loops_yes_c,sh=ParasolidConstants.PK_check_sh_yes_c,corrupt=ParasolidConstants.PK_check_corrupt_yes_c,missing_geom=ParasolidConstants.PK_check_missing_geom_yes_c};
        int count=0;PK_check_fault_s* faults=null;Assert.Equal(0,KernelRuntime.BodyCheck(body,&options,&count,&faults));Assert.Equal(0,count);if(faults!=null)KernelRuntime.MemoryFree(faults);
    }
    private static string Transmit(int body)
    {
        var options=new PK_PART_transmit_o_s{o_t_version=4,transmit_format=ParasolidConstants.PK_transmit_format_text_c,transmit_version=371,transmit_meshes=ParasolidConstants.PK_transmit_meshes_separate_c};
        var block=new PK_MEMORY_block_s();Assert.Equal(0,KernelRuntime.PartTransmitB(1,&body,&options,&block));try{return Encoding.ASCII.GetString(new ReadOnlySpan<byte>(block.bytes,(int)block.n_bytes));}finally{KernelRuntime.MemoryBlockFree(&block);}
    }
    private static (int,int,int,int,int,int,int,int,int,int) Counts()=>(KernelRuntime.Bodies.AliveCount,KernelRuntime.Regions.AliveCount,KernelRuntime.Shells.AliveCount,
        KernelRuntime.Faces.AliveCount,KernelRuntime.Loops.AliveCount,KernelRuntime.Edges.AliveCount,KernelRuntime.Vertices.AliveCount,KernelRuntime.Fins.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount);
    private static string Snapshot(int body)
    {
        var record=Record(body);var text=new StringBuilder();text.Append(record.Header.Tag).Append(':').Append(record.FirstRegion).Append(':').Append(record.FirstShell);
        var face=record.FirstFaceBody;
        for(var i=0;i<record.FaceCountBody;i++,face=KernelRuntime.GetFaceRecord(face).NextInBody)
        {
            var f=KernelRuntime.GetFaceRecord(face);text.Append('|').Append(f.Header.Tag).Append(',').Append(f.SurfTag).Append(',').Append(f.Orientation).Append(',').Append(f.FirstLoop).Append(',').Append(f.LoopCount);
        }
        var edge=record.FirstEdgeBody;
        for(var i=0;i<record.EdgeCountBody;i++,edge=KernelRuntime.GetEdgeRecord(edge).NextInBody)
        {
            var e=KernelRuntime.GetEdgeRecord(edge);text.Append('|').Append(e.Header.Tag).Append(',').Append(e.CurveTag).Append(',').Append(e.StartVertex).Append(',').Append(e.EndVertex)
                .Append(',').Append(e.FirstFinEdge).Append(',').Append(e.LastFinEdge).Append(',').Append(e.PendingBlendKind).Append(',').Append(e.PendingBlendSequence).Append(',').Append(e.PendingBlendFirstFace)
                .Append(',').Append(e.PendingBlendFirst.ToString("R")).Append(',').Append(e.PendingBlendSecond.ToString("R"));
        }
        var vertex=record.FirstVertexBody;
        for(var i=0;i<record.VertexCountBody;i++,vertex=KernelRuntime.GetVertexRecord(vertex).NextInBody)
        {
            var v=KernelRuntime.GetVertexRecord(vertex);text.Append('|').Append(v.Header.Tag).Append(',').Append(v.PointTag);
            if(v.PointTag>0)
            {
                var p=new PK_POINT_sf_s();Assert.Equal(0,KernelRuntime.PointAsk(v.PointTag,&p));
                for(var j=0;j<3;j++)text.Append(',').Append(p.position.coord[j].ToString("R"));
            }
        }
        return text.ToString();
    }
}

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using BodyTag=System.Int32;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class RegularCurvedBooleanTests:IDisposable
{
    public RegularCurvedBooleanTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Fact]
    public void UncertifiedCylinderJunctionLeavesEveryInputAndGeometryPoolUnchanged()
    {
        int target;
        Assert.Equal(0,KernelRuntime.BodyCreateSolidCyl(5+Math.ScaleB(1,-25),6,null,&target));
        var first=BooleanOperationTests.Block(0,.5,2,14,7,2);
        var second=BooleanOperationTests.Block(0,-.5,1,12,7,2);
        var bodies=new[]{target,first,second};
        var before=bodies.Select(XtBrepImportTests.Transmit).ToArray();
        var generations=bodies.Select(x=>KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(x).Slot].Header.Generation).ToArray();
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        var blocks=KernelRuntime.BlocksLiveBytes;var returns=KernelRuntime.ReturnOutstandingBlocks;
        var tools=stackalloc int[2]{first,second};
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);
        PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
        var previous=KernelRuntime.ForbidSpatialBoundaryReuse;
        KernelRuntime.ForbidSpatialBoundaryReuse=false;
        try
        {
            Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.BodyBoolean2(target,2,tools,&options,&tracking,&result));
            Assert.Equal(KernelRuntime.SheetPartitionStage.BoundaryAssociation,KernelRuntime.LastSheetPartitionStage);
        }
        finally{KernelRuntime.ForbidSpatialBoundaryReuse=previous;}
        Assert.Equal(0,result.n_bodies);Assert.True(result.bodies==null);Assert.True(tracking.track_records==null);
        Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
        Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);Assert.Equal(returns,KernelRuntime.ReturnOutstandingBlocks);
        for(var i=0;i<bodies.Length;i++)
        {
            Assert.Equal(generations[i],KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(bodies[i]).Slot].Header.Generation);
            Assert.Equal(before[i],XtBrepImportTests.Transmit(bodies[i]));
        }
    }

    [Fact]
    public void AxialContactMeetingACylinderEndFailsBeforePublishingAnUnprovedImprint()
    {
        BodyTag target;Assert.Equal(0,KernelRuntime.BodyCreateSolidCyl(4,4,null,&target));
        var tool=BooleanOperationTests.Block(5,0,-1,2,2,2);
        var input=new[]{target,tool};var before=input.Select(XtBrepImportTests.Transmit).ToArray();
        var generations=input.Select(body=>KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot].Header.Generation).ToArray();
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        var blocks=KernelRuntime.BlocksLiveBytes;var returns=KernelRuntime.ReturnOutstandingBlocks;
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);
        PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
        var previous=KernelRuntime.ForbidSpatialBoundaryReuse;KernelRuntime.ForbidSpatialBoundaryReuse=false;
        try
        {
            Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.BodyBoolean2(target,1,&tool,&options,&tracking,&result));
            Assert.Equal(KernelRuntime.RegularBooleanStage.Cuts,KernelRuntime.LastRegularBooleanStage);
        }
        finally{KernelRuntime.ForbidSpatialBoundaryReuse=previous;}
        Assert.Equal(0,result.n_bodies);Assert.True(result.bodies==null);Assert.True(tracking.track_records==null);
        Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
        Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);Assert.Equal(returns,KernelRuntime.ReturnOutstandingBlocks);
        for(var i=0;i<input.Length;i++)
        {
            Assert.Equal(generations[i],KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(input[i]).Slot].Header.Generation);
            Assert.Equal(before[i],XtBrepImportTests.Transmit(input[i]));
        }
    }

    [Fact]
    public void AxialContactHasTwoUsesAndSurvivesCopyAndReload()
    {
        BodyTag target;Assert.Equal(0,KernelRuntime.BodyCreateSolidCyl(4,4,null,&target));
        var tool=BooleanOperationTests.Block(5,0,1,2,2,2);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);
        var previous=KernelRuntime.ForbidSpatialBoundaryReuse;KernelRuntime.ForbidSpatialBoundaryReuse=false;
        try
        {
            PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
            Assert.Equal(0,KernelRuntime.BodyBoolean2(target,1,&tool,&options,&tracking,&result));
            Assert.Equal(ParasolidConstants.PK_boolean_result_success_c,result.result);Assert.Equal(1,result.n_bodies);
            Assert.Equal(target,result.bodies[0]);Assert.Equal(0,KernelRuntime.TopolTrackResultFree(&tracking));
            Assert.Equal(0,KernelRuntime.BooleanResultFree(&result));
            CheckContact(target);
            BodyTag copy;Assert.Equal(0,KernelRuntime.EntityCopy(target,&copy));Assert.Equal(0,KernelRuntime.EntityDelete(1,&target));
            SessionMemoryOwner.DropBodyXt(KernelRuntime.ResolveTagRecord(copy).Slot);
            Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(copy),out var restored));
            Assert.Equal(0,KernelRuntime.EntityDelete(1,&copy));CheckContact(restored);
            var outside=BooleanOperationTests.Block(11,0,1,2,2,2);
            Assert.Equal(0,KernelRuntime.BodyBoolean2(restored,1,&outside,&options,&tracking,&result));
            Assert.Equal(ParasolidConstants.PK_boolean_result_no_clash_c,result.result);Assert.Equal(restored,result.bodies[0]);
            Assert.Equal(0,KernelRuntime.TopolTrackResultFree(&tracking));Assert.Equal(0,KernelRuntime.BooleanResultFree(&result));
            CheckContact(restored);
            var crossing=BooleanOperationTests.Block(3.5,0,1.5,2,2,1);
            var before=XtBrepImportTests.Transmit(restored);var beforeTool=XtBrepImportTests.Transmit(crossing);
            Assert.NotEqual(0,KernelRuntime.BodyBoolean2(restored,1,&crossing,&options,&tracking,&result));
            Assert.Equal(0,result.n_bodies);Assert.True(result.bodies==null);Assert.True(tracking.track_records==null);
            Assert.Equal(before,XtBrepImportTests.Transmit(restored));Assert.Equal(beforeTool,XtBrepImportTests.Transmit(crossing));
            Assert.Equal(0,KernelRuntime.EntityDelete(1,&crossing));
            Assert.Equal(0,KernelRuntime.EntityDelete(1,&restored));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
        }
        finally{KernelRuntime.ForbidSpatialBoundaryReuse=previous;}

        static void CheckContact(BodyTag body)
        {
            BooleanOperationTests.Counts(body,3,3,2);
            var point=new PK_VECTOR_s();point.coord[0]=4;point.coord[2]=2;int enclosure,topology;
            Assert.Equal(0,KernelRuntime.BodyContainsVector(body,point,&enclosure,&topology));Assert.Equal(ParasolidConstants.PK_enclosure_on_c,enclosure);
            int type;Assert.Equal(0,KernelRuntime.EntityAskClass(topology,&type));Assert.Equal(ParasolidConstants.PK_CLASS_edge,type);
            var edge=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(topology).Slot];Assert.Equal(2,edge.FinCount);
            var first=KernelRuntime.Fins[edge.FirstFinEdge];var last=KernelRuntime.Fins[edge.LastFinEdge];
            Assert.Equal(first.Loop,last.Loop);Assert.NotEqual(first.Sense,last.Sense);
        }
    }

    [Fact]
    public void AllocationFailureDuringAxialContactPublicationRestoresBothBodies()
    {
        BodyTag target;Assert.Equal(0,KernelRuntime.BodyCreateSolidCyl(4,4,null,&target));
        var tool=BooleanOperationTests.Block(5,0,1,2,2,2);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);
        var before=XtBrepImportTests.Transmit(target);var beforeTool=XtBrepImportTests.Transmit(tool);
        var blocks=KernelRuntime.BlocksLiveBytes;var returns=KernelRuntime.ReturnOutstandingBlocks;
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        var previous=KernelRuntime.ForbidSpatialBoundaryReuse;KernelRuntime.ForbidSpatialBoundaryReuse=false;
        var publicationFailed=false;
        try
        {
            for(var budget=0;budget<128&&!publicationFailed;budget++)
            {
                PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
                KernelRuntime.InjectAllocationFailure(budget);
                var error=KernelRuntime.BodyBoolean2(target,1,&tool,&options,&tracking,&result);
                KernelRuntime.InjectAllocationFailure(-1);
                Assert.NotEqual(0,error);
                Assert.Equal(0,result.n_bodies);Assert.True(result.bodies==null);Assert.True(tracking.track_records==null);
                publicationFailed=KernelRuntime.LastRegularBooleanStage==KernelRuntime.RegularBooleanStage.Publish;
                Assert.Equal(before,XtBrepImportTests.Transmit(target));Assert.Equal(beforeTool,XtBrepImportTests.Transmit(tool));
                Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);Assert.Equal(returns,KernelRuntime.ReturnOutstandingBlocks);
                Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
            }
        }
        finally{KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.ForbidSpatialBoundaryReuse=previous;}
        Assert.True(publicationFailed,"the budget sweep must reach a failure in actual publication");
    }

    [Fact]
    public void CurvedInputIsASewnSolidWithAnExactBiquadraticRoof()
    {
        var body=RoofSolid();BooleanOperationTests.Counts(body,6,12,8);
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));Assert.Equal(ParasolidConstants.PK_BODY_type_solid_c,KernelRuntime.Bodies[slot].BodyType);
    }

    [Theory]
    [InlineData(.5,.5,.5,ParasolidConstants.PK_enclosure_inside_c,ParasolidConstants.PK_CLASS_region)]
    [InlineData(.5,.5,1.3,ParasolidConstants.PK_enclosure_outside_c,ParasolidConstants.PK_CLASS_region)]
    [InlineData(0,0,0,ParasolidConstants.PK_enclosure_on_c,ParasolidConstants.PK_CLASS_vertex)]
    [InlineData(.5,0,0,ParasolidConstants.PK_enclosure_on_c,ParasolidConstants.PK_CLASS_edge)]
    [InlineData(.5,.5,1.2,ParasolidConstants.PK_enclosure_on_c,ParasolidConstants.PK_CLASS_face)]
    public void CurvedSolidClassificationReturnsTheAppropriateTopology(double x,double y,double z,int expected,int expectedClass)
    {
        var body=RoofSolid();var point=new PK_VECTOR_s();point.coord[0]=x;point.coord[1]=y;point.coord[2]=z;int enclosure,topology;
        Assert.Equal(0,KernelRuntime.BodyContainsVector(body,point,&enclosure,&topology));Assert.Equal(expected,enclosure);
        int cls;Assert.Equal(0,KernelRuntime.EntityAskClass(topology,&cls));Assert.Equal(expectedClass,cls);
    }

    [Theory]
    [InlineData(ParasolidConstants.PK_boolean_unite_c)]
    [InlineData(ParasolidConstants.PK_boolean_intersect_c)]
    [InlineData(ParasolidConstants.PK_boolean_subtract_c)]
    public void TransverseBlockBooleanPreservesTheCurvedRoof(int function)
    {
        var target=RoofSolid();var tool=BooleanOperationTests.Block(.25,.25,.25,1,1,1);
        var options=BooleanOperationTests.Options(function);int count;int* bodies;
        var error=KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&bodies);
        var conflict=KernelRuntime.LastBoundaryParameterConflict;
        Assert.True(error==0,$"Boolean error={error}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, sheet={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, t={KernelRuntime.LastSheetParameter:R}, status={KernelRuntime.LastSheetAlgorithmStatus}, conflict={conflict.Event}: {conflict.Previous:R} [{conflict.PreviousLower:R},{conflict.PreviousUpper:R}] vs {conflict.Incoming:R} [{conflict.Lower:R},{conflict.Upper:R}]");
        try
        {
            Assert.Equal(1,count);Assert.Equal(target,bodies[0]);Assert.True(KernelRuntime.TryResolveBodySlot(target,out var slot));
            var face=KernelRuntime.Bodies[slot].FirstFaceBody;var curved=false;
            for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
            {
                var support=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag)];
                if(support.Class==SurfaceClass.BSurface&&KernelRuntime.BSurfaceDataStore[support.DataIndex].UDegree==2)curved=true;
            }
            Assert.True(curved);Assert.False(KernelRuntime.IsValidTag(tool));
        }
        finally{if(bodies!=null)KernelRuntime.MemoryFree(bodies);}
    }

    [Theory]
    [InlineData(ParasolidConstants.PK_boolean_unite_c,19,51,34)]
    [InlineData(ParasolidConstants.PK_boolean_intersect_c,13,33,22)]
    [InlineData(ParasolidConstants.PK_boolean_subtract_c,16,42,28)]
    public void OverlappingToolsCancelBuriedCutsAndRedundantVertices(int function,int faces,int edges,int vertices)
    {
        var target=RoofSolid();int* tools=stackalloc int[2]{BooleanOperationTests.Block(.25,.35,.2,1,1,1.1),BooleanOperationTests.Block(.83,.62,.17,.8,.7,.91)};
        var options=BooleanOperationTests.Options(function);int count;int* bodies;
        var error=KernelRuntime.BodyBoolean(target,2,tools,&options,&count,&bodies);
        Assert.True(error==0,$"Boolean error={error} stage={KernelRuntime.LastRegularBooleanStage} faces={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace} sheet={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} t={KernelRuntime.LastSheetParameter:R} status={KernelRuntime.LastSheetAlgorithmStatus}");
        try{Assert.Equal(1,count);BooleanOperationTests.Counts(bodies[0],faces,edges,vertices);}
        finally{KernelRuntime.MemoryFree(bodies);}
    }

    [Theory]
    [InlineData(ParasolidConstants.PK_boolean_unite_c,12,25,16)]
    [InlineData(ParasolidConstants.PK_boolean_intersect_c,2,1,0)]
    [InlineData(ParasolidConstants.PK_boolean_subtract_c,7,13,8)]
    public void ClosedInteriorIntersectionPreservesVertexFreeRingTopology(int function,int faces,int edges,int vertices)
    {
        var target=RoofSolid();var tool=BooleanOperationTests.Block(.5,.5,1.08,2,2,1);var options=BooleanOperationTests.Options(function);int count;int* bodies;
        Assert.Equal(0,KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&bodies));
        try{Assert.Equal(1,count);BooleanOperationTests.Counts(bodies[0],faces,edges,vertices);}
        finally{KernelRuntime.MemoryFree(bodies);}
    }

    [Fact]
    public void SeparateUnionComponentPreservesAllItsTopologyHandles()
    {
        var target=RoofSolid();int* tools=stackalloc int[2]{BooleanOperationTests.Block(.25,.35,.2,1,1,1.1),BooleanOperationTests.Block(3,3,.25,1,1,1)};
        int nf,ne,nv;int* faces;int* edges;int* vertices;
        Assert.Equal(0,KernelRuntime.BodyAskFaces(tools[1],&nf,&faces));Assert.Equal(0,KernelRuntime.BodyAskEdges(tools[1],&ne,&edges));Assert.Equal(0,KernelRuntime.BodyAskVertices(tools[1],&nv,&vertices));
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_unite_c);int count;int* bodies;
        Assert.Equal(0,KernelRuntime.BodyBoolean(target,2,tools,&options,&count,&bodies));
        try
        {
            Assert.Equal(2,count);Assert.Equal(target,bodies[0]);Assert.Equal(tools[1],bodies[1]);
            for(var i=0;i<nf;i++)Assert.True(KernelRuntime.IsValidTag(faces[i]));for(var i=0;i<ne;i++)Assert.True(KernelRuntime.IsValidTag(edges[i]));for(var i=0;i<nv;i++)Assert.True(KernelRuntime.IsValidTag(vertices[i]));
        }
        finally{KernelRuntime.MemoryFree(faces);KernelRuntime.MemoryFree(edges);KernelRuntime.MemoryFree(vertices);KernelRuntime.MemoryFree(bodies);}
    }

    [Fact]
    public void UnsupportedTrackingFailsAtomically()
    {
        var target=RoofSolid();var tool=BooleanOperationTests.Block(.25,.25,.25,1,1,1);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);options.tracking=1;
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;var blocks=KernelRuntime.ReturnOutstandingBlocks;
        PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.BodyBoolean2(target,1,&tool,&options,&tracking,&result));
        Assert.Equal(0,result.n_bodies);Assert.True(result.bodies==null);BooleanOperationTests.Counts(target,6,12,8);BooleanOperationTests.Counts(tool,6,12,8);
        Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);Assert.Equal(blocks,KernelRuntime.ReturnOutstandingBlocks);
    }

    [Fact]
    public void CavityClassificationReturnsItsBoundedVoidRegionWithoutCreatingGeometry()
    {
        var target=RoofSolid();var tool=BooleanOperationTests.Block(.5,.5,.25,.2,.2,.2);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int count;int* bodies;
        Assert.Equal(0,KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&bodies));KernelRuntime.MemoryFree(bodies);
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        var point=new PK_VECTOR_s();point.coord[0]=point.coord[1]=.5;point.coord[2]=.35;int enclosure,cavity,exterior;
        Assert.Equal(0,KernelRuntime.BodyContainsVector(target,point,&enclosure,&cavity));Assert.Equal(ParasolidConstants.PK_enclosure_outside_c,enclosure);
        byte solid;Assert.Equal(0,KernelRuntime.RegionIsSolid(cavity,&solid));Assert.Equal(0,solid);
        point.coord[0]=point.coord[1]=point.coord[2]=-1;Assert.Equal(0,KernelRuntime.BodyContainsVector(target,point,&enclosure,&exterior));Assert.NotEqual(cavity,exterior);
        Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(7)] [InlineData(25)] [InlineData(80)] [InlineData(250)] [InlineData(800)] [InlineData(2500)]
    public void BooleanAllocationFailuresRestoreBothInputsAndReleaseOutputs(int budget)
    {
        var target=RoofSolid();var tool=BooleanOperationTests.Block(.25,.25,.25,1,1,1);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;var bodiesBefore=KernelRuntime.Bodies.AliveCount;var blocks=KernelRuntime.ReturnOutstandingBlocks;
        int count=9;int* bodies=(int*)1;KernelRuntime.InjectAllocationFailure(budget);var error=KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&bodies);KernelRuntime.InjectAllocationFailure(-1);
        if(error!=0)
        {
            Assert.Equal(0,count);Assert.True(bodies==null);BooleanOperationTests.Counts(target,6,12,8);BooleanOperationTests.Counts(tool,6,12,8);
            Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);Assert.Equal(bodiesBefore,KernelRuntime.Bodies.AliveCount);Assert.Equal(blocks,KernelRuntime.ReturnOutstandingBlocks);
            Assert.Equal(0,KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&bodies));
        }
        try{Assert.Equal(1,count);BooleanOperationTests.Counts(bodies[0],9,21,14);}
        finally{if(bodies!=null)KernelRuntime.MemoryFree(bodies);}
    }

    [Fact]
    public void CopiedResultOwnsAllGeometryAfterTheOriginalIsDeleted()
    {
        var target=RoofSolid();int faceCount;int* faces;Assert.Equal(0,KernelRuntime.BodyAskFaces(target,&faceCount,&faces));
        var tool=BooleanOperationTests.Block(.25,.25,.25,1,1,1);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int count;int* result;
        Assert.Equal(0,KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&result));
        try
        {
            for(var i=0;i<faceCount;i++)Assert.True(KernelRuntime.IsValidTag(faces[i]));int copy;Assert.Equal(0,KernelRuntime.EntityCopy(target,&copy));Assert.Equal(0,KernelRuntime.EntityDelete(1,&target));
            var point=new PK_VECTOR_s();point.coord[0]=point.coord[1]=.9;point.coord[2]=.5;int enclosure,topology;
            Assert.Equal(0,KernelRuntime.BodyContainsVector(copy,point,&enclosure,&topology));Assert.Equal(ParasolidConstants.PK_enclosure_inside_c,enclosure);
            Assert.Equal(0,KernelRuntime.EntityDelete(1,&copy));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Bodies.AliveCount);
        }
        finally{KernelRuntime.MemoryFree(faces);KernelRuntime.MemoryFree(result);}
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SavedTrimmedCurvedResultRestoresFiniteEdgeRangesAndContinuesModeling(bool negativeSense)
    {
        var target=RoofSolid();var tool=BooleanOperationTests.Block(.25,.25,.25,1,1,1);
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int count;int* bodies;
        Assert.Equal(0,KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&bodies));KernelRuntime.MemoryFree(bodies);
        if(negativeSense)
        {
            Assert.True(KernelRuntime.TryResolveBodySlot(target,out var source));var reverse=KernelRuntime.Bodies[source].FirstEdgeBody;
            var reversing=new HashSet<int>();
            for(var i=0;i<KernelRuntime.Bodies[source].EdgeCountBody;i++,reverse=KernelRuntime.Edges[reverse].NextInBody)
            {
                var c=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[reverse].CurveTag)];
                if(c.Class==CurveClass.BCurve&&c.Sense!=ParasolidConstants.PK_TOPOL_sense_negative_c)reversing.Add(c.Header.Tag);
            }
            Assert.NotEmpty(reversing);reverse=KernelRuntime.Bodies[source].FirstEdgeBody;
            for(var i=0;i<KernelRuntime.Bodies[source].EdgeCountBody;i++,reverse=KernelRuntime.Edges[reverse].NextInBody)
            {
                ref var e=ref KernelRuntime.Edges[reverse];ref var c=ref KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(e.CurveTag)];
                if(!reversing.Contains(e.CurveTag))continue;c.Sense=ParasolidConstants.PK_TOPOL_sense_negative_c;
                (e.StartVertex,e.EndVertex)=(e.EndVertex,e.StartVertex);var fin=e.FirstFinEdge;
                for(var j=0;j<e.FinCount;j++,fin=KernelRuntime.Fins[fin].NextOfEdge)KernelRuntime.Fins[fin].Sense=KernelRuntime.Fins[fin].Sense=='+'?'-':'+';
            }
        }
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(target),out var received));
        Assert.True(KernelRuntime.TryResolveBodySlot(target,out var original));Assert.True(KernelRuntime.TryResolveBodySlot(received,out var restored));
        Assert.False(KernelRuntime.Xt.IsOpaque(restored));
        var edge=KernelRuntime.Bodies[original].FirstEdgeBody;var trimmed=0;var intersections=0;
        for(var i=0;i<KernelRuntime.Bodies[original].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var before=KernelRuntime.Edges[edge];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(before.CurveTag)];
            if(curve.Class is not (CurveClass.BCurve or CurveClass.ICurve))continue;
            Assert.NotEqual(0,before.HasParameterRange);if(curve.Class==CurveClass.ICurve)intersections++;
            if(before.ParameterStart>curve.TMin+1e-9||before.ParameterEnd<curve.TMax-1e-9)trimmed++;
            var candidate=KernelRuntime.Bodies[restored].FirstEdgeBody;var found=false;
            var p=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[before.StartVertex].PointTag)].Position;
            var q=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[before.EndVertex].PointTag)].Position;
            for(var j=0;j<KernelRuntime.Bodies[restored].EdgeCountBody;j++,candidate=KernelRuntime.Edges[candidate].NextInBody)
            {
                var after=KernelRuntime.Edges[candidate];if(after.StartVertex<0||after.EndVertex<0)continue;
                if(KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(after.CurveTag)].Class!=curve.Class)continue;
                var a=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[after.StartVertex].PointTag)].Position;
                var b=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[after.EndVertex].PointTag)].Position;
                if(Distance(p,a)>1e-9||Distance(q,b)>1e-9)continue;
                Assert.True(after.HasParameterRange!=0,$"Missing interval on {curve.Class} sense={curve.Sense}, curve={before.CurveTag}/{after.CurveTag}, range={before.ParameterStart:R}/{before.ParameterEnd:R}");Assert.InRange(Math.Abs(before.ParameterStart-after.ParameterStart),0,1e-8);
                Assert.InRange(Math.Abs(before.ParameterEnd-after.ParameterEnd),0,1e-8);found=true;break;
            }
            Assert.True(found,"Restored finite edge has corresponding endpoints and curve class");
        }
        Assert.True(trimmed>0);Assert.True(intersections>0);
        var point=new PK_VECTOR_s();point.coord[0]=point.coord[1]=.9;point.coord[2]=.5;int enclosure,topology;
        Assert.Equal(0,KernelRuntime.BodyContainsVector(received,point,&enclosure,&topology));Assert.Equal(ParasolidConstants.PK_enclosure_inside_c,enclosure);
        tool=BooleanOperationTests.Block(.75,.75,.5,.3,.3,.4);
        Assert.Equal(0,KernelRuntime.BodyBoolean(received,1,&tool,&options,&count,&bodies));
        try{Assert.Equal(1,count);Assert.Equal(received,bodies[0]);}finally{KernelRuntime.MemoryFree(bodies);}
    }

    private static double Distance(KernelVector3 a,KernelVector3 b)
        =>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)+(a.Z-b.Z)*(a.Z-b.Z));

    [Fact]
    public void WarmBooleanAndClassificationAllocateNoManagedMemory()
    {
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);
        for(var i=0;i<2;i++){var warm=RoofSolid();var tool=BooleanOperationTests.Block(.25,.25,.25,1,1,1);Assert.Equal(0,BooleanClassifyDelete(warm,tool,&options));}
        var body=RoofSolid();var lastTool=BooleanOperationTests.Block(.25,.25,.25,1,1,1);var before=GC.GetAllocatedBytesForCurrentThread();
        var error=BooleanClassifyDelete(body,lastTool,&options);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(0,error);Assert.Equal(0,allocated);
    }

    private static int BooleanClassifyDelete(int target,int tool,PK_BODY_boolean_o_s* options)
    {
        int count;int* result;var error=KernelRuntime.BodyBoolean(target,1,&tool,options,&count,&result);if(error!=0)return error;
        var point=new PK_VECTOR_s();point.coord[0]=point.coord[1]=.9;point.coord[2]=.5;int enclosure,topology;
        error=KernelRuntime.BodyContainsVector(result[0],point,&enclosure,&topology);if(error==0)error=KernelRuntime.EntityDelete(count,result);
        var free=KernelRuntime.MemoryFree(result);return error==0?free:error;
    }

    [Theory]
    [InlineData(15903)] [InlineData(15901)] [InlineData(15902)]
    public void TwoTransverseSplineSolidsRetainCertifiedNativeBoundaryRoots(int function)
    {
        var previous=KernelRuntime.ForbidSpatialBoundaryReuse;KernelRuntime.ForbidSpatialBoundaryReuse=false;
        try
        {
            var target=RoofSolid();var tool=RoofSolid(.3,.2,.05,.2);
            var options=BooleanOperationTests.Options(function);int count;int* bodies;
            var error=KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&bodies);
            Assert.True(error==0,$"error={error}, stage={KernelRuntime.LastRegularBooleanStage}, pair={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace}, sheet={KernelRuntime.LastSheetPartitionStage}, status={KernelRuntime.LastSheetAlgorithmStatus}");
            try{Assert.Equal(1,count);Assert.Equal(target,bodies[0]);Assert.Equal(0,KernelRuntime.EntityDelete(1,&target));}
            finally{if(bodies!=null)KernelRuntime.MemoryFree(bodies);}
            Assert.Equal(0,KernelRuntime.Bodies.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
        }
        finally{KernelRuntime.ForbidSpatialBoundaryReuse=previous;}
    }

    internal static int RoofSolid(double x=0,double y=0,double z=0,double bulge=.8)
    {
        int* sheets=stackalloc int[6];double* poles=stackalloc double[27];double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2];
        for(var face=0;face<6;face++)
        {
            var n=face==1?3:2;var degree=n-1;mult[0]=mult[1]=n;
            for(var i=0;i<n;i++)for(var j=0;j<n;j++)
            {
                var u=(double)i/degree;var v=(double)j/degree;var at=3*(i*n+j);
                poles[at]=x+(face switch{0 or 1 or 4=>u,2=>0,3=>1,_=>v});
                poles[at+1]=y+(face switch{0=>1-v,1 or 2=>v,3=>u,4=>0,_=>1});
                poles[at+2]=z+(face switch{0=>0,1=>1+(i==1&&j==1?bulge:0),2 or 5=>u,_=>v});
            }
            var definition=new PK_BSURF_sf_s{u_degree=degree,v_degree=degree,n_u_vertices=n,n_v_vertices=n,vertex_dim=3,vertex=poles,
                n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,
                form=ParasolidConstants.PK_BSURF_form_arbitrary_c,u_knot_type=ParasolidConstants.PK_knot_non_uniform_c,v_knot_type=ParasolidConstants.PK_knot_non_uniform_c,
                self_intersecting=ParasolidConstants.PK_self_intersect_false_c,convexity=ParasolidConstants.PK_convexity_unset_c};
            int surface;Assert.Equal(0,KernelRuntime.BSurfCreate(&definition,&surface));var box=new PK_UVBOX_s();box.param[2]=box.param[3]=1;
            Assert.Equal(0,KernelRuntime.SurfMakeSheetBody(surface,box,sheets+face));Assert.Equal(0,KernelRuntime.EntityDelete(1,&surface));
        }
        var options=new PK_BODY_sew_bodies_o_s{o_t_version=8,treat_as_manifold=1,prefered_body_type=ParasolidConstants.PK_BODY_sewing_solid_c,
            duplicate_removal=ParasolidConstants.PK_BODY_sewing_remove_none_c,number_of_iterations=1,assembly_sewing=ParasolidConstants.PK_BODY_sewing_assy_no_c,
            general_sewing=ParasolidConstants.PK_BODY_sewing_gen_no_c,sew_up_inner_loops=ParasolidConstants.PK_LOOP_sew_up_loop_c,reduce_edge_tolerance=ParasolidConstants.PK_EDGE_reduce_tol_no_c};
        int sewnCount,remaining,problems;int* sewn;int* unsewn;PK_BODY_problem_group_s* groups;
        Assert.Equal(0,KernelRuntime.BodySewBodies(6,sheets,1e-8,&options,&sewnCount,&sewn,&remaining,&unsewn,&problems,&groups));
        try{Assert.Equal(1,sewnCount);Assert.Equal(0,remaining);Assert.Equal(0,problems);return sewn[0];}
        finally{if(sewn!=null)KernelRuntime.MemoryFree(sewn);if(unsewn!=null)KernelRuntime.MemoryFree(unsewn);if(groups!=null)KernelRuntime.MemoryFree(groups);}
    }
}

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class PlanarMissingGeometryTests:IDisposable
{
    public PlanarMissingGeometryTests()
    {KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DispatcherPrologueAllocationFailure_ClearsLegacyOutputsBeforeCoreExecution(bool imprint)
    {
        var body=BooleanOperationTests.Block(0,0,0,2,2,2);var tool=BooleanOperationTests.Block(.5,.5,0,2,2,2);
        var definition=new PK_PLANE_sf_s();definition.basis_set.location.coord[2]=1;definition.basis_set.axis.coord[2]=1;definition.basis_set.ref_direction.coord[0]=1;
        int plane=0,mark=0;Assert.Equal(0,KernelRuntime.PlaneCreate(&definition,&plane));Assert.Equal(0,KernelRuntime.MarkCreate(&mark));
        var session=KernelRuntime.State.Session;Assert.Equal(0,session->UndoEntryCount);var capacity=session->UndoEntryCapacity;
        // Expose no spare journal slots while retaining its backing allocation.
        // The next partition-before-image must grow the journal, before the
        // operation core can initialize any caller output.
        session->UndoEntryCapacity=0;var before=KernelRuntime.ReturnOutstandingBlocks;
        int count=17;int* returned=(int*)1;int error;
        try
        {
            KernelRuntime.InjectAllocationFailure(0);var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_intersect_c);
            error=imprint?KernelRuntime.BodyImprintPlane(body,plane,1e-8,&count,&returned)
                :KernelRuntime.BodyBoolean(body,1,&tool,&options,&count,&returned);
        }
        finally{KernelRuntime.InjectAllocationFailure(-1);session->UndoEntryCapacity=capacity;}
        Assert.Equal(ParasolidConstants.PK_ERROR_memory_full,error);Assert.Equal(0,count);Assert.True(returned==null);
        Assert.Equal(before,KernelRuntime.ReturnOutstandingBlocks);BooleanOperationTests.Counts(body,6,12,8);BooleanOperationTests.Counts(tool,6,12,8);
        Assert.Equal(0,KernelRuntime.MarkGoto(mark));
    }

    [Theory]
    [InlineData(1,0)] [InlineData(0,1)] [InlineData(1,1)]
    public void UnimplementedSectionMergeModes_AreRejectedBeforeChangingTheModel(byte mergeImprinted,byte mergeNewFaces)
    {
        var body=BooleanOperationTests.Block(0,0,0,2,2,2);var definition=new PK_PLANE_sf_s();definition.basis_set.location.coord[2]=1;definition.basis_set.axis.coord[2]=1;definition.basis_set.ref_direction.coord[0]=1;
        int plane=0;Assert.Equal(0,KernelRuntime.PlaneCreate(&definition,&plane));var options=AnalyticSectionTests.Options(ParasolidConstants.PK_section_fence_both_c);
        options.merge_imprinted=mergeImprinted;options.merge_new_faces=mergeNewFaces;var before=KernelRuntime.ReturnOutstandingBlocks;
        PK_section_r_s result=new(){front_bodies=new(){length=17,array=(int*)1}};
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.BodySectionWithSurf(body,plane,&options,&result));
        Assert.True(result.front_bodies.array==null&&result.back_bodies.array==null&&result.front_faces.array==null&&result.back_faces.array==null);
        Assert.Equal(before,KernelRuntime.ReturnOutstandingBlocks);Assert.True(KernelRuntime.IsValidTag(body)&&KernelRuntime.IsValidTag(plane));BooleanOperationTests.Counts(body,6,12,8);
    }

    [Theory]
    [InlineData(0,0)] [InlineData(0,1)] [InlineData(0,2)] [InlineData(0,3)]
    [InlineData(1,0)] [InlineData(1,1)] [InlineData(1,2)] [InlineData(1,3)]
    [InlineData(2,0)] [InlineData(2,1)] [InlineData(2,2)] [InlineData(2,3)]
    public void DetachedVertexEdgeOrFaceGeometry_FailsWithoutDereferencingInvalidPoolsOrConsumingInputs(int geometry,int operation)
    {
        var body=BooleanOperationTests.Block(0,0,0,2,2,2);var tool=BooleanOperationTests.Block(.5,.5,0,2,2,2);
        var planeDefinition=new PK_PLANE_sf_s();planeDefinition.basis_set.location.coord[2]=1;planeDefinition.basis_set.axis.coord[2]=1;planeDefinition.basis_set.ref_direction.coord[0]=1;
        int plane=0;Assert.Equal(0,KernelRuntime.PlaneCreate(&planeDefinition,&plane));
        int count=0;int* topology=null;
        Assert.Equal(0,geometry==0?KernelRuntime.BodyAskVertices(body,&count,&topology)
            :geometry==1?KernelRuntime.BodyAskEdges(body,&count,&topology):KernelRuntime.BodyAskFaces(body,&count,&topology));
        var detached=topology[0];Assert.Equal(0,KernelRuntime.MemoryFree(topology));Assert.Equal(0,KernelRuntime.TopologyDetachGeometry(detached));
        var before=KernelRuntime.ReturnOutstandingBlocks;
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_intersect_c);int error;
        if(operation==0)
        {
            count=17;int* results=(int*)1;error=KernelRuntime.BodyBoolean(body,1,&tool,&options,&count,&results);
            Assert.Equal(0,count);Assert.True(results==null);
        }
        else if(operation==1)
        {
            PK_TOPOL_track_r_s tracking=new(){n_track_records=17,track_records=(PK_TOPOL_track_record_r_s*)1};
            PK_boolean_r_s result=new(){n_bodies=17,bodies=(int*)1};error=KernelRuntime.BodyBoolean2(body,1,&tool,&options,&tracking,&result);
            Assert.Equal(0,result.n_bodies);Assert.True(result.bodies==null);Assert.Equal(0,tracking.n_track_records);Assert.True(tracking.track_records==null);
        }
        else if(operation==2)
        {
            var section=AnalyticSectionTests.Options(ParasolidConstants.PK_section_fence_both_c);
            PK_section_r_s result=new(){front_bodies=new(){length=17,array=(int*)1},back_faces=new(){length=17,array=(int*)1}};
            error=KernelRuntime.BodySectionWithSurf(body,plane,&section,&result);
            Assert.True(result.front_bodies.array==null&&result.back_bodies.array==null&&result.front_faces.array==null&&result.back_faces.array==null);
        }
        else
        {
            int enclosure=17,entity=17;var point=new PK_VECTOR_s();point.coord[2]=1;
            error=KernelRuntime.BodyContainsVector(body,point,&enclosure,&entity);
            Assert.Equal(ParasolidConstants.PK_enclosure_unset_c,enclosure);Assert.Equal(0,entity);
        }
        Assert.Equal(ParasolidConstants.PK_ERROR_missing_geom,error);Assert.Equal(before,KernelRuntime.ReturnOutstandingBlocks);
        Assert.True(KernelRuntime.IsValidTag(body)&&KernelRuntime.IsValidTag(tool)&&KernelRuntime.IsValidTag(plane)&&KernelRuntime.IsValidTag(detached));
        BooleanOperationTests.Counts(body,6,12,8);BooleanOperationTests.Counts(tool,6,12,8);
    }
}

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class AnalyticBooleanTests:IDisposable
{
    public AnalyticBooleanTests(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(15903)] [InlineData(15901)] [InlineData(15902)]
    public void TransverseSpheres_CreateAnalyticRingPreserveFacesAndTrackItsTwoSources(int operation)
    {
        var target=Sphere(0,0,0,2);var tool=Sphere(2,0,0,2);var first=Face(target);var second=Face(tool);
        var options=BooleanOperationTests.Options(operation);options.tracking=1;PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
        Assert.Equal(0,KernelRuntime.BodyBoolean2(target,1,&tool,&options,&tracking,&result));
        try
        {
            Assert.Equal(ParasolidConstants.PK_boolean_result_success_c,result.result);Assert.Equal(1,result.n_bodies);Assert.Equal(target,result.bodies[0]);
            BooleanOperationTests.Counts(target,2,1,0);Assert.False(KernelRuntime.IsValidTag(tool));Assert.True(KernelRuntime.IsValidTag(first));Assert.True(KernelRuntime.IsValidTag(second));
            Assert.Equal(1,tracking.n_track_records);var record=tracking.track_records[0];Assert.Equal(ParasolidConstants.PK_TOPOL_track_derive_c,record.track);
            Assert.Equal(2,record.n_original_topols);Assert.Equal(first,record.original_topols[0]);Assert.Equal(second,record.original_topols[1]);Assert.Equal(1,record.n_product_topols);
            int edgeClass=0;Assert.Equal(0,KernelRuntime.EntityAskClass(record.product_topols[0],&edgeClass));Assert.Equal(ParasolidConstants.PK_CLASS_edge,edgeClass);
            int curve=0;Assert.Equal(0,KernelRuntime.EdgeAskCurve(record.product_topols[0],&curve));var circle=new PK_CIRCLE_sf_s();Assert.Equal(0,KernelRuntime.CircleAsk(curve,&circle));
            Assert.InRange(Math.Abs(circle.radius-Math.Sqrt(3)),0,1e-12);Assert.Equal(1,circle.basis_set.location.coord[0]);
            var inside=ParasolidConstants.PK_enclosure_inside_c;var outside=ParasolidConstants.PK_enclosure_outside_c;
            Assert.Equal(operation==15901?outside:inside,Enclosure(target,-1,0,0,out _));
            Assert.Equal(operation==15902?outside:inside,Enclosure(target,1,0,0,out _));
            Assert.Equal(operation==15903?inside:outside,Enclosure(target,3,0,0,out _));
            Assert.Equal(ParasolidConstants.PK_enclosure_on_c,Enclosure(target,1,Math.Sqrt(3),0,out var boundary));Assert.Equal(record.product_topols[0],boundary);
        }
        finally{Assert.Equal(0,KernelRuntime.TopolTrackResultFree(&tracking));Assert.Equal(0,KernelRuntime.BooleanResultFree(&result));}
    }

    [Fact]
    public void NestedSubtraction_BuildsSeparateCavityAndReturnsItsVoidRegion()
    {
        var target=Sphere(0,0,0,3);var tool=Sphere(.5,0,0,1);var first=Face(target);var second=Face(tool);
        var options=BooleanOperationTests.Options(15902);options.tracking=1;PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
        Assert.Equal(0,KernelRuntime.BodyBoolean2(target,1,&tool,&options,&tracking,&result));
        try
        {
            BooleanOperationTests.Counts(target,2,0,0);int n=0;int* tags=null;
            Assert.Equal(0,KernelRuntime.BodyAskRegions(target,&n,&tags));Assert.Equal(3,n);KernelRuntime.MemoryFree(tags);
            Assert.Equal(0,KernelRuntime.BodyAskShells(target,&n,&tags));Assert.Equal(4,n);KernelRuntime.MemoryFree(tags);Assert.Equal(0,tracking.n_track_records);
            Assert.True(KernelRuntime.IsValidTag(first)&&KernelRuntime.IsValidTag(second));
            Assert.Equal(ParasolidConstants.PK_enclosure_outside_c,Enclosure(target,.5,0,0,out var cavity));
            Assert.Equal(ParasolidConstants.PK_enclosure_outside_c,Enclosure(target,5,0,0,out var exterior));Assert.NotEqual(cavity,exterior);
            Assert.Equal(ParasolidConstants.PK_enclosure_inside_c,Enclosure(target,-2,0,0,out _));
            Assert.Equal(ParasolidConstants.PK_enclosure_on_c,Enclosure(target,.5,1,0,out var boundary));Assert.Equal(second,boundary);
        }
        finally{KernelRuntime.TopolTrackResultFree(&tracking);KernelRuntime.BooleanResultFree(&result);}
    }

    [Theory]
    [InlineData(15903,1,0,21650)]
    [InlineData(15901,1,0,21651)]
    [InlineData(15902,0,0,21650)]
    [InlineData(15903,2,5,21651)]
    [InlineData(15901,0,5,21651)]
    [InlineData(15902,1,5,21651)]
    public void CoincidentAndDisjointSpheres_MatchNativeStatusAndConsumption(int operation,int expected,double distance,int status)
    {
        var target=Sphere(0,0,0,2);var tool=Sphere(distance,0,0,2);var options=BooleanOperationTests.Options(operation);
        PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
        Assert.Equal(0,KernelRuntime.BodyBoolean2(target,1,&tool,&options,&tracking,&result));
        try
        {
            Assert.Equal(expected,result.n_bodies);Assert.Equal(status,result.result);Assert.Equal(expected>0,KernelRuntime.IsValidTag(target));
            Assert.Equal(expected==2,KernelRuntime.IsValidTag(tool));if(expected>0)Assert.Equal(target,result.bodies[0]);if(expected==2)Assert.Equal(tool,result.bodies[1]);
        }
        finally{KernelRuntime.TopolTrackResultFree(&tracking);KernelRuntime.BooleanResultFree(&result);}
    }

    [Theory]
    [InlineData(15903,1,3)] [InlineData(15901,3,1)]
    public void ToolBoundaryReplacingTarget_PreservesToolFaceAndTargetBody(int operation,double targetRadius,double toolRadius)
    {
        var target=Sphere(0,0,0,targetRadius);var tool=Sphere(.5,0,0,toolRadius);var oldTargetFace=Face(target);var oldToolFace=Face(tool);
        var options=BooleanOperationTests.Options(operation);int n=0;int* results=null;Assert.Equal(0,KernelRuntime.BodyBoolean(target,1,&tool,&options,&n,&results));
        try{Assert.Equal(1,n);Assert.Equal(target,results[0]);Assert.False(KernelRuntime.IsValidTag(oldTargetFace));Assert.True(KernelRuntime.IsValidTag(oldToolFace));Assert.Equal(oldToolFace,Face(target));}
        finally{KernelRuntime.MemoryFree(results);}
    }

    [Fact]
    public void AllocationFailuresRollbackBodiesFacesAndAllReturnedBuffers()
    {
        var succeeded=false;
        for(var fault=0;fault<55;fault++)
        {
            var target=Sphere(0,0,0,2);var tool=Sphere(2,0,0,2);var first=Face(target);var second=Face(tool);var before=KernelRuntime.ReturnOutstandingBlocks;
            var options=BooleanOperationTests.Options(15902);options.tracking=1;PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
            KernelRuntime.InjectAllocationFailure(fault);var error=KernelRuntime.BodyBoolean2(target,1,&tool,&options,&tracking,&result);KernelRuntime.InjectAllocationFailure(-1);
            if(error!=0)
            {
                Assert.Equal(0,result.n_bodies);Assert.True(result.bodies==null);Assert.Equal(0,tracking.n_track_records);Assert.True(tracking.track_records==null);
                Assert.True(KernelRuntime.IsValidTag(first)&&KernelRuntime.IsValidTag(second));BooleanOperationTests.Counts(target,1,0,0);BooleanOperationTests.Counts(tool,1,0,0);
            }
            else {succeeded=true;KernelRuntime.TopolTrackResultFree(&tracking);KernelRuntime.BooleanResultFree(&result);}
            Assert.Equal(before,KernelRuntime.ReturnOutstandingBlocks);Assert.Equal(0,KernelRuntime.EntityDelete(1,&target));if(KernelRuntime.IsValidTag(tool))Assert.Equal(0,KernelRuntime.EntityDelete(1,&tool));
            Assert.Equal(0,KernelRuntime.Bodies.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
        }
        Assert.True(succeeded);
    }

    [Theory]
    [InlineData(15903)] [InlineData(15901)] [InlineData(15902)]
    public void SingularContactIsExplicitlyUnsupportedAndDoesNotConsumeInputs(int operation)
    {
        var target=Sphere(0,0,0,2);var tool=Sphere(4,0,0,2);var options=BooleanOperationTests.Options(operation);int n=3;int* returned=(int*)1;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.BodyBoolean(target,1,&tool,&options,&n,&returned));Assert.Equal(0,n);Assert.True(returned==null);
        Assert.True(KernelRuntime.IsValidTag(target)&&KernelRuntime.IsValidTag(tool));BooleanOperationTests.Counts(target,1,0,0);BooleanOperationTests.Counts(tool,1,0,0);
    }
    [Theory]
    [InlineData(15903)] [InlineData(15901)] [InlineData(15902)]
    public void EquivalentCurveAndSurfaceSenseStorage_DoesNotChangeMaterialClassification(int operation)
    {
        var target=Sphere(0,0,0,2);var tool=Sphere(2,0,0,2);var options=BooleanOperationTests.Options(operation);int n=0;int* returned=null;
        Assert.Equal(0,KernelRuntime.BodyBoolean(target,1,&tool,&options,&n,&returned));KernelRuntime.MemoryFree(returned);
        var slot=KernelRuntime.ResolveTagRecord(target).Slot;var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        var curve=KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[edge].CurveTag);KernelRuntime.Curves[curve].Sense=ParasolidConstants.PK_TOPOL_sense_negative_c;
        var firstFin=KernelRuntime.Edges[edge].FirstFinEdge;var secondFin=KernelRuntime.Edges[edge].LastFinEdge;
        KernelRuntime.Fins[firstFin].Sense=KernelRuntime.Fins[firstFin].Sense=='+'?'-':'+';KernelRuntime.Fins[secondFin].Sense=KernelRuntime.Fins[secondFin].Sense=='+'?'-':'+';
        var face=KernelRuntime.Bodies[slot].FirstFaceBody;var surface=KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag);
        KernelRuntime.Faces[face].Orientation=ParasolidConstants.PK_TOPOL_sense_negative_c;KernelRuntime.Surfaces[surface].Sense=ParasolidConstants.PK_TOPOL_sense_negative_c;
        Assert.Equal(operation==15901?ParasolidConstants.PK_enclosure_outside_c:ParasolidConstants.PK_enclosure_inside_c,Enclosure(target,-1,0,0,out _));
        Assert.Equal(operation==15902?ParasolidConstants.PK_enclosure_outside_c:ParasolidConstants.PK_enclosure_inside_c,Enclosure(target,1,0,0,out _));
        Assert.Equal(operation==15903?ParasolidConstants.PK_enclosure_inside_c:ParasolidConstants.PK_enclosure_outside_c,Enclosure(target,3,0,0,out _));
    }

    internal static int Sphere(double x,double y,double z,double radius)
    {var axis=new PK_AXIS2_sf_s();axis.location.coord[0]=x;axis.location.coord[1]=y;axis.location.coord[2]=z;axis.axis.coord[2]=1;axis.ref_direction.coord[0]=1;int body=0;Assert.Equal(0,KernelRuntime.BodyCreateSolidSphere(radius,&axis,&body));return body;}
    private static int Face(int body){int n=0;int* faces=null;Assert.Equal(0,KernelRuntime.BodyAskFaces(body,&n,&faces));var face=faces[0];KernelRuntime.MemoryFree(faces);return face;}
    private static int Enclosure(int body,double x,double y,double z,out int topology)
    {var p=new PK_VECTOR_s();p.coord[0]=x;p.coord[1]=y;p.coord[2]=z;int enclosure=0,entity=0;Assert.Equal(0,KernelRuntime.BodyContainsVector(body,p,&enclosure,&entity));topology=entity;return enclosure;}
}

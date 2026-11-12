using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class IntersectionEventBooleanTests:IDisposable
{
    public IntersectionEventBooleanTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(15903,false)] [InlineData(15901,false)] [InlineData(15902,false)]
    [InlineData(15903,true)] [InlineData(15901,true)] [InlineData(15902,true)]
    public void CoincidentSupportWithDifferentTrimLoopsUsesTheFullFiniteOverlay(int function,bool reload)
    {
        var target=RegularCurvedBooleanTests.RoofSolid();var tool=RegularCurvedBooleanTests.RoofSolid();
        Subtract(target,BooleanOperationTests.Block(.1,.5,.25,.6,2,2));Subtract(tool,BooleanOperationTests.Block(.9,.5,.25,.6,2,2));
        if(reload){Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(tool),out var loaded));Assert.Equal(0,KernelRuntime.EntityDelete(1,&tool));tool=loaded;}
        var options=BooleanOperationTests.Options(function);options.tracking=1;int count;int* products;
        var error=KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&products);
        Assert.True(error==0,$"error={error}, stage={KernelRuntime.LastRegularBooleanStage}, pair={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace}, curve={KernelRuntime.LastSheetCurve}, t={KernelRuntime.LastSheetParameter}, sheet={KernelRuntime.LastSheetPartitionStage}, status={KernelRuntime.LastSheetAlgorithmStatus}, uv={KernelRuntime.LastSheetRayU}/{KernelRuntime.LastSheetRayV}");
        try
        {
            Assert.Equal(1,count);CheckPoint(target,.2,.5,.5,function==15903);CheckPoint(target,.5,.5,.5,function!=15902);CheckPoint(target,.8,.5,.5,function!=15901);
            Assert.True(KernelRuntime.TryResolveBodySlot(target,out var slot));var vertex=KernelRuntime.Bodies[slot].FirstVertexBody;var merged=0;var accurateNurbs=0;
            for(var v=0;v<KernelRuntime.Bodies[slot].VertexCountBody;v++,vertex=KernelRuntime.Vertices[vertex].NextInBody)
            {
                var record=KernelRuntime.Vertices[vertex];var point=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(record.PointTag)].Position;
                if(record.Tolerance>0){merged++;Assert.Equal(1e-7,record.Tolerance);Assert.Equal(.25,point.Z,12);Assert.True(Math.Abs(point.X)<1e-12||Math.Abs(point.X-1)<1e-12);}
                if(Math.Abs(point.Z-1)<1e-12&&(Math.Abs(point.X-.4)<1e-12||Math.Abs(point.X-.6)<1e-12)){accurateNurbs++;Assert.Equal(0,record.Tolerance);}
            }
            Assert.Equal(function==15903?4:2,merged);Assert.True(accurateNurbs>=2);
        }
        finally{if(products!=null)KernelRuntime.MemoryFree(products);}
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&target));Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);
    }

    private static void CheckPoint(int body,double x,double y,double z,bool inside)
    {
        var p=new PK_VECTOR_s();p.coord[0]=x;p.coord[1]=y;p.coord[2]=z;int enclosure,topology;
        Assert.Equal(0,KernelRuntime.BodyContainsVector(body,p,&enclosure,&topology));Assert.Equal(inside?ParasolidConstants.PK_enclosure_inside_c:ParasolidConstants.PK_enclosure_outside_c,enclosure);
    }

    private static void Subtract(int target,int tool)
    {
        var options=BooleanOperationTests.Options(15902);int n;int* products;
        Assert.Equal(0,KernelRuntime.BodyBoolean(target,1,&tool,&options,&n,&products));Assert.Equal(1,n);KernelRuntime.MemoryFree(products);
    }

    [Theory]
    [InlineData(15903,false,1)] [InlineData(15901,false,1)] [InlineData(15902,false,0)]
    [InlineData(15903,true,1)] [InlineData(15901,true,1)] [InlineData(15902,true,0)]
    public void CoincidentFreeformDomainsHaveTwoSidedMaterialSemantics(int function,bool reload,int expected)
    {
        var target=RegularCurvedBooleanTests.RoofSolid();int tool;
        if(reload)Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(target),out tool));
        else Assert.Equal(0,KernelRuntime.EntityCopy(target,&tool));
        var options=BooleanOperationTests.Options(function);options.tracking=1;PK_TOPOL_track_r_s tracking=default;PK_boolean_r_s result=default;
        var error=KernelRuntime.BodyBoolean2(target,1,&tool,&options,&tracking,&result);var count=result.n_bodies;var products=result.bodies;
        Assert.True(error==0,$"error={error}, stage={KernelRuntime.LastRegularBooleanStage}, pair={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace}, curve={KernelRuntime.LastSheetCurve}, t={KernelRuntime.LastSheetParameter}, sheet={KernelRuntime.LastSheetPartitionStage}");
        try
        {
            Assert.Equal(expected,count);Assert.Equal(expected==0?0:12,tracking.n_track_records);
            for(var i=0;i<tracking.n_track_records;i++)
            {
                var record=tracking.track_records[i];Assert.Equal(2,record.n_original_topols);Assert.Equal(1,record.n_product_topols);
                Assert.Equal(ParasolidConstants.PK_CLASS_edge,record.original_classes[0]);Assert.Equal(ParasolidConstants.PK_CLASS_edge,record.original_classes[1]);
                Assert.True(KernelRuntime.IsValidTag(record.product_topols[0]));
            }
            if(expected!=0){Assert.Equal(target,products[0]);BooleanOperationTests.Counts(target,6,12,8);Assert.Equal(0,KernelRuntime.EntityDelete(1,&target));}
        }
        finally{KernelRuntime.TopolTrackResultFree(&tracking);KernelRuntime.BooleanResultFree(&result);}
        Assert.Equal(0,KernelRuntime.Bodies.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }
}

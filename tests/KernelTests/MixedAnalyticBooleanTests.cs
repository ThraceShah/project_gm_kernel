using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class MixedAnalyticBooleanTests:IDisposable
{
    public MixedAnalyticBooleanTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(15902,false,7,14,8,1)] [InlineData(15902,true,7,14,8,1)]
    [InlineData(15901,false,3,2,0,1)] [InlineData(15901,true,3,2,0,1)]
    [InlineData(15903,false,10,16,8,2)] [InlineData(15903,true,10,16,8,2)]
    public void ObliqueCylinderBooleanPreservesAnalyticFacesAcrossTheSeam(int function,bool seam,int expectedFaces,int expectedEdges,int expectedVertices,int expectedCylinders)
    {
        var body=BooleanOperationTests.Block(0,0,0,4,4,2);var tool=ObliqueCylinder(seam);
        var options=BooleanOperationTests.Options(function);int n;int* bodies;
        var error=KernelRuntime.BodyBoolean(body,1,&tool,&options,&n,&bodies);
        Assert.True(error==0,$"Boolean={error}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, sheet={KernelRuntime.LastSheetPartitionStage}");
        try
        {
            Assert.Equal(1,n);Assert.Equal(body,bodies[0]);BooleanOperationTests.Counts(body,expectedFaces,expectedEdges,expectedVertices);
            int nf;int* faces;Assert.Equal(0,KernelRuntime.BodyAskFaces(body,&nf,&faces));
            try{var cylinders=0;for(var i=0;i<nf;i++){int surface;Assert.Equal(0,KernelRuntime.FaceAskSurf(faces[i],&surface));if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)].Class==SurfaceClass.Cylinder)cylinders++;}Assert.Equal(expectedCylinders,cylinders);}
            finally{KernelRuntime.MemoryFree(faces);}
            CheckPoint(body,0,0,1,function==ParasolidConstants.PK_boolean_subtract_c?ParasolidConstants.PK_enclosure_outside_c:ParasolidConstants.PK_enclosure_inside_c);
            CheckPoint(body,1.5,1.5,1,function==ParasolidConstants.PK_boolean_intersect_c?ParasolidConstants.PK_enclosure_outside_c:ParasolidConstants.PK_enclosure_inside_c);
        }
        finally{if(bodies!=null)KernelRuntime.MemoryFree(bodies);}
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Bodies.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReloadedPartialCylinderCanBeCutAgainAcrossItsAngularBoundary(bool seam)
    {
        var body=BooleanOperationTests.Block(0,0,0,4,4,2);var tool=ObliqueCylinder(seam);
        Subtract(body,tool);Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out var loaded));Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));body=loaded;
        tool=BooleanOperationTests.Block(0,1.55,-.5,6,2.9,3);Subtract(body,tool);BooleanOperationTests.Counts(body,8,18,12);
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out loaded));Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));body=loaded;
        AssertWrappedArcClassification(body);
        tool=BooleanOperationTests.Block(1,0,-.5,3.3,6,3);Subtract(body,tool);
        CheckPoint(body,-1.5,-1.5,1,ParasolidConstants.PK_enclosure_inside_c);CheckPoint(body,.5,-1.5,1,ParasolidConstants.PK_enclosure_outside_c);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Bodies.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    private static void AssertWrappedArcClassification(int body)
    {
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;var wrapped=0;
        for(var i=0;i<KernelRuntime.Bodies[slot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var e=KernelRuntime.Edges[edge];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(e.CurveTag)];
            if(curve.Class!=CurveClass.Ellipse||e.ParameterEnd<=Math.Tau+1e-8)continue;
            var t=(Math.Max(e.ParameterStart,Math.Tau)+e.ParameterEnd)*.5;var point=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.CurveEval(e.CurveTag,t,0,&point));int enclosure,topology;
            Assert.Equal(0,KernelRuntime.BodyContainsVector(body,point,&enclosure,&topology));Assert.Equal(ParasolidConstants.PK_enclosure_on_c,enclosure);
            Assert.Equal(e.Header.Tag,topology);wrapped++;
        }
        Assert.True(wrapped>0);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(7)] [InlineData(25)] [InlineData(80)] [InlineData(250)] [InlineData(1000)]
    public void MixedBooleanAllocationFailuresRestoreBothInputsAndPayloads(int budget)
    {
        var body=BooleanOperationTests.Block(0,0,0,4,4,2);var tool=ObliqueCylinder();
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        var bytes=KernelRuntime.BlocksLiveBytes;var returns=KernelRuntime.ReturnOutstandingBlocks;
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int n;int* products;
        KernelRuntime.InjectAllocationFailure(budget);var error=KernelRuntime.BodyBoolean(body,1,&tool,&options,&n,&products);KernelRuntime.InjectAllocationFailure(-1);
        if(error!=0)
        {
            Assert.Equal(0,n);Assert.True(products==null);BooleanOperationTests.Counts(body,6,12,8);BooleanOperationTests.Counts(tool,3,2,0);
            Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);Assert.Equal(bytes,KernelRuntime.BlocksLiveBytes);Assert.Equal(returns,KernelRuntime.ReturnOutstandingBlocks);
            Assert.Equal(0,KernelRuntime.EntityDelete(1,&tool));
        }
        else{Assert.Equal(1,n);KernelRuntime.MemoryFree(products);BooleanOperationTests.Counts(body,7,14,8);}
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void WarmMixedBooleanAndClassificationAllocateNoManagedMemory()
    {
        var warm=BooleanOperationTests.Block(0,0,0,4,4,2);Subtract(warm,ObliqueCylinder());CheckPoint(warm,1.5,1.5,1,ParasolidConstants.PK_enclosure_inside_c);Assert.Equal(0,KernelRuntime.EntityDelete(1,&warm));
        var body=BooleanOperationTests.Block(0,0,0,4,4,2);var tool=ObliqueCylinder();var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);
        var point=new PK_VECTOR_s();point.coord[0]=point.coord[1]=1.5;point.coord[2]=1;int n,enclosure,topology;int* products;
        var before=GC.GetAllocatedBytesForCurrentThread();var error=KernelRuntime.BodyBoolean(body,1,&tool,&options,&n,&products);
        if(error==0)error=KernelRuntime.BodyContainsVector(body,point,&enclosure,&topology);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(0,error);Assert.Equal(0,allocated);if(products!=null)KernelRuntime.MemoryFree(products);Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));
    }

    private static void Subtract(int body,int tool)
    {
        var options=BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);int n;int* products;
        var error=KernelRuntime.BodyBoolean(body,1,&tool,&options,&n,&products);
        Assert.True(error==0,$"Boolean={error}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, sheet={KernelRuntime.LastSheetPartitionStage}");
        try{Assert.Equal(1,n);}finally{if(products!=null)KernelRuntime.MemoryFree(products);}
    }

    internal static int ObliqueCylinder(bool seam=false)
    {
        var basis=new PK_AXIS2_sf_s();basis.location.coord[0]=-2.25;basis.location.coord[2]=-2;
        basis.axis.coord[0]=.6;basis.axis.coord[2]=.8;
        if(seam)basis.ref_direction.coord[1]=1;else{basis.ref_direction.coord[0]=.8;basis.ref_direction.coord[2]=-.6;}
        int body;Assert.Equal(0,KernelRuntime.BodyCreateSolidCyl(.3,7.5,&basis,&body));return body;
    }

    private static void CheckPoint(int body,double x,double y,double z,int expected)
    {
        var point=new PK_VECTOR_s();point.coord[0]=x;point.coord[1]=y;point.coord[2]=z;int enclosure,topology;
        Assert.Equal(0,KernelRuntime.BodyContainsVector(body,point,&enclosure,&topology));Assert.Equal(expected,enclosure);
    }
}

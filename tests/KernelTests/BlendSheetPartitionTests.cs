using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class BlendSheetPartitionTests:IDisposable
{
    public BlendSheetPartitionTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Fact]
    public void ConsecutiveTransverseCutsSplitExactBlendEdgesAndKeepTheSupportType()
    {
        var body=Sheet();Imprint(body,Plane(1,.5),1);BooleanOperationTests.Counts(body,2,7,6);
        Imprint(body,Plane(0,-.12),2);BooleanOperationTests.Counts(body,4,12,9);
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var face=KernelRuntime.Bodies[slot].FirstFaceBody;
        for(var i=0;i<4;i++,face=KernelRuntime.Faces[face].NextInBody)
            Assert.Equal(SurfaceClass.BlendSurface,KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag)].Class);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TrimmedBlendRemainsEditableByASecondCut(bool keep)
    {
        var body=Sheet();var plane=Plane(1,.5);int count;int* edges;
        Check(KernelRuntime.BodyImprintPlane(body,plane,1e-8,&count,&edges));
        try
        {
            Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var seed=KernelRuntime.Faces[KernelRuntime.Bodies[slot].FirstFaceBody].Header.Tag;
            Assert.Equal(0,KernelRuntime.BodyTrim(body,count,edges,1,&seed,keep?(byte)1:(byte)0));
        }
        finally{KernelRuntime.MemoryFree(edges);}
        BooleanOperationTests.Counts(body,1,4,4);Imprint(body,Plane(0,-.12),1);BooleanOperationTests.Counts(body,2,7,6);
    }

    [Theory]
    [InlineData(18200)] [InlineData(18201)] [InlineData(18202)]
    public void SectionReturnsRealConnectedTrimmedBlendSheets(int fence)
    {
        var body=Sheet();var plane=Plane(1,.5);var options=new PK_BODY_section_o_s{o_t_version=5,fence=fence,default_tol=1e-8};PK_section_r_s result;
        Check(KernelRuntime.BodySectionWithSurf(body,plane,&options,&result));
        try
        {
            Assert.Equal(fence==18201?0:1,result.front_bodies.length);Assert.Equal(fence==18200?0:1,result.back_bodies.length);
            Assert.Equal(0,result.front_faces.length);Assert.Equal(0,result.back_faces.length);
            if(result.front_bodies.length!=0)Imprint(result.front_bodies.array[0],Plane(0,-.12),1);
            if(result.back_bodies.length!=0)Imprint(result.back_bodies.array[0],Plane(0,-.12),1);
        }
        finally{KernelRuntime.SectionResultFree(&result);}
    }

    [Fact]
    public void FailedSecondCutRestoresTopologyGeometryAndReturnedMemory()
    {
        var body=Sheet();Imprint(body,Plane(1,.5),1);var cutter=Plane(0,-.12);
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;var blocks=KernelRuntime.ReturnOutstandingBlocks;
        int n=7;int* edges=(int*)1;KernelRuntime.InjectAllocationFailure(0);
        var error=KernelRuntime.BodyImprintPlane(body,cutter,1e-8,&n,&edges);KernelRuntime.InjectAllocationFailure(-1);
        Assert.NotEqual(0,error);Assert.Equal(0,n);Assert.True(edges==null);BooleanOperationTests.Counts(body,2,7,6);
        Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);Assert.Equal(blocks,KernelRuntime.ReturnOutstandingBlocks);
        Imprint(body,cutter,2);BooleanOperationTests.Counts(body,4,12,9);
    }

    [Fact]
    public void BlendPartitionAndDeletionAllocateNoManagedMemoryAfterWarmup()
    {
        var first=Plane(1,.5);var second=Plane(0,-.12);
        for(var i=0;i<2;i++){var warm=Sheet();Assert.Equal(0,CutAndDelete(warm,first,second));}
        var body=Sheet();var before=GC.GetAllocatedBytesForCurrentThread();var error=CutAndDelete(body,first,second);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(0,error);Assert.Equal(0,allocated);
    }

    [Theory]
    [InlineData(1)] [InlineData(7)] [InlineData(25)] [InlineData(80)] [InlineData(250)]
    public void SecondCutAllocationFailuresAreAtomicAtDifferentConstructionDepths(int budget)
    {
        var body=Sheet();Imprint(body,Plane(1,.5),1);var cutter=Plane(0,-.12);
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;var blocks=KernelRuntime.ReturnOutstandingBlocks;
        int n;int* edges;KernelRuntime.InjectAllocationFailure(budget);var error=KernelRuntime.BodyImprintPlane(body,cutter,1e-8,&n,&edges);KernelRuntime.InjectAllocationFailure(-1);
        if(error!=0)
        {
            Assert.Equal(0,n);Assert.True(edges==null);BooleanOperationTests.Counts(body,2,7,6);
            Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);Assert.Equal(blocks,KernelRuntime.ReturnOutstandingBlocks);
            Imprint(body,cutter,2);
        }
        else{Assert.Equal(2,n);KernelRuntime.MemoryFree(edges);}
        BooleanOperationTests.Counts(body,4,12,9);
    }

    [Fact]
    public void RepeatedSecondCutPreservesExistingTopologyHandles()
    {
        var body=Sheet();Imprint(body,Plane(1,.5),1);var second=Plane(0,-.12);Imprint(body,second,2);
        int n;int* before;Assert.Equal(0,KernelRuntime.BodyAskEdges(body,&n,&before));
        try{Imprint(body,second,2);BooleanOperationTests.Counts(body,4,12,9);for(var i=0;i<n;i++)Assert.True(KernelRuntime.IsValidTag(before[i]));}
        finally{KernelRuntime.MemoryFree(before);}
    }

    private static int CutAndDelete(int body,int first,int second)
    {
        int count;int* edges;var error=KernelRuntime.BodyImprintPlane(body,first,1e-8,&count,&edges);
        if(error!=0)return error;KernelRuntime.MemoryFree(edges);
        error=KernelRuntime.BodyImprintPlane(body,second,1e-8,&count,&edges);if(error!=0)return error;
        KernelRuntime.MemoryFree(edges);return KernelRuntime.EntityDelete(1,&body);
    }

    private static int Sheet()
    {
        var surface=BlendIntersectionTests.CreateBlend();var box=new PK_UVBOX_s();box.param[0]=box.param[1]=.1;box.param[2]=box.param[3]=.9;
        int body;Assert.Equal(0,KernelRuntime.SurfMakeSheetBody(surface,box,&body));
        return body;
    }
    private static int Plane(int axis,double at)
    {
        var definition=new PK_PLANE_sf_s();definition.basis_set.location.coord[axis]=at;definition.basis_set.axis.coord[axis]=1;
        definition.basis_set.ref_direction.coord[(axis+1)%3]=1;int surface;Assert.Equal(0,KernelRuntime.PlaneCreate(&definition,&surface));return surface;
    }
    private static void Imprint(int body,int plane,int expected)
    {
        int n;int* edges;Check(KernelRuntime.BodyImprintPlane(body,plane,1e-8,&n,&edges));
        try{Assert.Equal(expected,n);}finally{KernelRuntime.MemoryFree(edges);}
    }
    private static void Check(int error)=>Assert.True(error==0,$"error={error} stage={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} t={KernelRuntime.LastSheetParameter:R} status={KernelRuntime.LastSheetAlgorithmStatus}");
}

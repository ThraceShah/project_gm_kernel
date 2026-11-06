using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class GeneralSheetPartitionTests : IDisposable
{
    public GeneralSheetPartitionTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Fact]
    public void InternalClosedCutCreatesAnInnerFaceAndAnOuterFaceWithAHole()
    {
        var body=Sheet();var plane=GeneralSurfaceIntersectionTests.Plane();int n=0;int* edges=null;
        Assert.Equal(0,KernelRuntime.BodyImprintPlane(body,plane,1e-8,&n,&edges));
        try
        {
            Assert.Equal(1,n);BooleanOperationTests.Counts(body,2,5,4);
            Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var face=KernelRuntime.Bodies[slot].FirstFaceBody;
            var one=0;var two=0;for(var i=0;i<2;i++,face=KernelRuntime.Faces[face].NextInBody){if(KernelRuntime.Faces[face].LoopCount==1)one++;if(KernelRuntime.Faces[face].LoopCount==2)two++;}
            Assert.Equal(1,one);Assert.Equal(1,two);
        }
        finally{KernelRuntime.MemoryFree(edges);}
    }

    [Fact]
    public void ClosedCutAllocationFailuresRestoreTopologyGeometryAndReturnMemory()
    {
        var passed=false;var failed=false;
        for(var budget=0;budget<400;budget+=13)
        {
            var body=Sheet();var plane=GeneralSurfaceIntersectionTests.Plane();
            var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;var blocks=KernelRuntime.ReturnOutstandingBlocks;
            int n=0;int* edges=null;KernelRuntime.InjectAllocationFailure(budget);
            var error=KernelRuntime.BodyImprintPlane(body,plane,1e-8,&n,&edges);KernelRuntime.InjectAllocationFailure(-1);
            if(error!=0)
            {
                failed=true;Assert.True(edges==null);Assert.Equal(0,n);BooleanOperationTests.Counts(body,1,4,4);
                Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
            }
            else {passed=true;BooleanOperationTests.Counts(body,2,5,4);KernelRuntime.MemoryFree(edges);}
            Assert.Equal(blocks,KernelRuntime.ReturnOutstandingBlocks);
            KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));
        }
        Assert.True(failed&&passed);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ClosedIntersectionIsARealTrimBarrier(bool keepInner)
    {
        var body=Sheet();var plane=GeneralSurfaceIntersectionTests.Plane();int n=0;int* edges=null;
        Assert.Equal(0,KernelRuntime.BodyImprintPlane(body,plane,1e-8,&n,&edges));
        try
        {
            Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var face=KernelRuntime.Bodies[slot].FirstFaceBody;int seed=0;
            for(var i=0;i<2;i++,face=KernelRuntime.Faces[face].NextInBody)if(KernelRuntime.Faces[face].LoopCount==1)seed=KernelRuntime.Faces[face].Header.Tag;
            Assert.NotEqual(0,seed);Assert.Equal(0,KernelRuntime.BodyTrim(body,n,edges,1,&seed,keepInner?(byte)1:(byte)0));
            BooleanOperationTests.Counts(body,1,keepInner?1:5,keepInner?0:4);
        }
        finally{KernelRuntime.MemoryFree(edges);}
    }

    [Fact]
    public void ASecondPlaneSplitsExistingClosedEdgesWithoutIntroducingAnArtificialSeam()
    {
        var body=Sheet();Imprint(body,GeneralSurfaceIntersectionTests.Plane(),1);
        var plane=XPlane();Imprint(body,plane,3);BooleanOperationTests.Counts(body,4,11,8);
        int count=0;int* before=null;Assert.Equal(0,KernelRuntime.BodyAskEdges(body,&count,&before));
        try
        {
            Imprint(body,plane,3);BooleanOperationTests.Counts(body,4,11,8);
            for(var i=0;i<count;i++)Assert.True(KernelRuntime.IsValidTag(before[i]));
        }
        finally{KernelRuntime.MemoryFree(before);}
    }

    [Theory]
    [InlineData(-1.0)] [InlineData(1.0)]
    public void NearParallelSecondPlaneUsesCompleteFiniteIntersectionProof(double direction)
    {
        var body=Sheet();Imprint(body,GeneralSurfaceIntersectionTests.Plane(),1);
        var definition=new PK_PLANE_sf_s();definition.basis_set.location.coord[2]=.1;
        definition.basis_set.axis.coord[0]=direction*Math.ScaleB(1,-50);definition.basis_set.axis.coord[2]=1;
        definition.basis_set.ref_direction.coord[0]=1;int plane;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&definition,&plane));
        // The new plane cuts the surface in a second complete loop, but its
        // infinite intersection line with the old loop's plane is far away.
        Imprint(body,plane,1);BooleanOperationTests.Counts(body,3,6,4);
    }

    [Theory]
    [InlineData(18200)] [InlineData(18201)] [InlineData(18202)]
    public void SectionOfAnAlreadyTrimmedAnnulusReturnsConnectedMaterialCells(int fence)
    {
        var body=Sheet();var horizontal=GeneralSurfaceIntersectionTests.Plane();int n=0;int* edges=null;
        Assert.Equal(0,KernelRuntime.BodyImprintPlane(body,horizontal,1e-8,&n,&edges));
        Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));var face=KernelRuntime.Bodies[slot].FirstFaceBody;int inner=0;
        for(var i=0;i<2;i++,face=KernelRuntime.Faces[face].NextInBody)if(KernelRuntime.Faces[face].LoopCount==1)inner=KernelRuntime.Faces[face].Header.Tag;
        Assert.Equal(0,KernelRuntime.BodyTrim(body,n,edges,1,&inner,0));KernelRuntime.MemoryFree(edges);
        var plane=XPlane();var options=new PK_BODY_section_o_s{o_t_version=5,fence=fence,default_tol=1e-8};PK_section_r_s result=default;
        var error=KernelRuntime.BodySectionWithSurf(body,plane,&options,&result);Assert.True(error==0,$"section error={error} stage={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} t={KernelRuntime.LastSheetParameter:R} status={KernelRuntime.LastSheetAlgorithmStatus}");
        try
        {
            Assert.Equal(fence==18201?0:1,result.front_bodies.length);Assert.Equal(fence==18200?0:1,result.back_bodies.length);
            if(result.front_bodies.length!=0)BooleanOperationTests.Counts(result.front_bodies.array[0],1,6,6);
            if(result.back_bodies.length!=0)BooleanOperationTests.Counts(result.back_bodies.array[0],1,6,6);
        }
        finally{KernelRuntime.SectionResultFree(&result);}
    }

    private static int Sheet()
    {
        var surface=GeneralSurfaceIntersectionTests.Surface(false);var box=new PK_UVBOX_s();box.param[2]=box.param[3]=1;int body=0;
        Assert.Equal(0,KernelRuntime.SurfMakeSheetBody(surface,box,&body));return body;
    }
    private static int XPlane()
    {
        var definition=new PK_PLANE_sf_s();definition.basis_set.location.coord[0]=.5;definition.basis_set.axis.coord[0]=1;definition.basis_set.ref_direction.coord[1]=1;
        int plane=0;Assert.Equal(0,KernelRuntime.PlaneCreate(&definition,&plane));return plane;
    }
    private static void Imprint(int body,int plane,int expected)
    {
        int n=0;int* edges=null;var error=KernelRuntime.BodyImprintPlane(body,plane,1e-8,&n,&edges);Assert.True(error==0,$"imprint error={error} stage={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} t={KernelRuntime.LastSheetParameter:R} status={KernelRuntime.LastSheetAlgorithmStatus}");
        try{Assert.Equal(expected,n);}finally{KernelRuntime.MemoryFree(edges);}
    }
}

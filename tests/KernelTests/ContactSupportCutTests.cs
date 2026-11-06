using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class ContactSupportCutTests:IDisposable
{
    public ContactSupportCutTests(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose(){KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ConvexIsolatedContactKeepsTwoSupportParameters(bool reverse)
    {
        var first=GeneralSurfaceIntersectionTests.Surface(false);var second=Plane(-.0625);if(reverse)(first,second)=(second,first);
        Span<SurfaceIntersectionNode> points=stackalloc SurfaceIntersectionNode[4];
        Assert.Equal(0,KernelRuntime.TestPrepareRegularContactSupportCuts(first,second,points,out var count,out var relation,out var handled));
        Assert.True(handled);Assert.Equal(SurfacePairRelation.PointContact,relation);Assert.Equal(1,count);
        var uv=new PK_UV_s();PK_VECTOR_s p;
        for(var side=0;side<2;side++)
        {
            uv.param[0]=side==0?points[0].U:points[0].S;uv.param[1]=side==0?points[0].V:points[0].T;
            Assert.Equal(0,KernelRuntime.SurfEval(side==0?first:second,uv,0,0,0,&p));
            Assert.Equal(points[0].Point.X,p.coord[0],12);Assert.Equal(points[0].Point.Y,p.coord[1],12);Assert.Equal(points[0].Point.Z,p.coord[2],12);
        }
    }

    [Fact]
    public void RegularPositiveHeightFallsThroughAndSeparatedHullIsProvedEmpty()
    {
        var surface=GeneralSurfaceIntersectionTests.Surface(false);Span<SurfaceIntersectionNode> points=stackalloc SurfaceIntersectionNode[4];
        Assert.Equal(0,KernelRuntime.TestPrepareRegularContactSupportCuts(surface,Plane(-.046875),points,out var count,out var relation,out var handled));
        Assert.False(handled);Assert.Equal(SurfacePairRelation.Unresolved,relation);Assert.Equal(0,count);
        Assert.Equal(0,KernelRuntime.TestPrepareRegularContactSupportCuts(surface,Plane(-1),points,out count,out relation,out handled));
        Assert.True(handled);Assert.Equal(SurfacePairRelation.Empty,relation);Assert.Equal(0,count);
    }

    [Fact]
    public void MissingPointCapacityCannotPublishAnEmptyEvent()
    {
        var surface=GeneralSurfaceIntersectionTests.Surface(false);
        Assert.NotEqual(0,KernelRuntime.TestPrepareRegularContactSupportCuts(surface,Plane(-.0625),default,out var count,out var relation,out var handled));
        Assert.True(handled);Assert.Equal(0,count);Assert.Equal(SurfacePairRelation.Unresolved,relation);
    }
    private static int Plane(double z)
    {
        var definition=new PK_PLANE_sf_s();definition.basis_set.location.coord[2]=z;definition.basis_set.axis.coord[2]=1;definition.basis_set.ref_direction.coord[0]=1;int plane;
        Assert.Equal(0,KernelRuntime.PlaneCreate(&definition,&plane));return plane;
    }
}

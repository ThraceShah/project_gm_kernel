using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class SurfaceBlendTests:IDisposable
{
    public SurfaceBlendTests(){KernelRuntime.SessionStop();var o=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&o));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(.2)] [InlineData(-.2)]
    public void PlaneBlendHasTheRequestedRollingBallContactsAndOwnsItsGeometry(double radius)
    {
        var a=Plane(false);var b=Plane(true);var options=new PK_SURF_create_blend_o_s{o_t_version=1};int blend=0;
        Assert.Equal(0,KernelRuntime.SurfCreateBlend(a,radius,b,radius,Point(0,0,0),Point(0,1,0),&options,&blend));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&a));Assert.Equal(0,KernelRuntime.EntityDelete(1,&b));
        var uv=new PK_UV_s();uv.param[0]=radius>0?Math.PI:0;uv.param[1]=.37;PK_VECTOR_s* jet=stackalloc PK_VECTOR_s[4];
        Assert.Equal(0,KernelRuntime.SurfEval(blend,uv,1,1,0,jet));Near(0,jet[0].coord[2]);Near(0,jet[1].coord[2]);Near(0,jet[2].coord[2]);
        uv.param[0]=radius>0?Math.PI*1.5:Math.PI*.5;
        Assert.Equal(0,KernelRuntime.SurfEval(blend,uv,1,1,0,jet));Near(0,jet[0].coord[0]);Near(0,jet[1].coord[0]);Near(0,jet[2].coord[0]);
        var sf=new PK_CYL_sf_s();Assert.Equal(0,KernelRuntime.CylAsk(blend,&sf));Near(Math.Abs(radius),sf.radius);Near(radius,sf.basis_set.location.coord[0]);Near(radius,sf.basis_set.location.coord[2]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SpherePlaneBlendIsAnExactTorusInEitherSupportOrder(bool sphereFirst)
    {
        var plane=Plane(false);var definition=new PK_SPHERE_sf_s{radius=1};definition.basis_set.location.coord[2]=1;definition.basis_set.axis.coord[2]=1;definition.basis_set.ref_direction.coord[0]=1;
        int sphere=0;Assert.Equal(0,KernelRuntime.SphereCreate(&definition,&sphere));var options=new PK_SURF_create_blend_o_s{o_t_version=1};int blend=0;
        Assert.Equal(0,KernelRuntime.SurfCreateBlend(sphereFirst?sphere:plane,.2,sphereFirst?plane:sphere,.2,Point(1,0,0),Point(0,sphereFirst?-1:1,0),&options,&blend));
        var actual=new PK_TORUS_sf_s();Assert.Equal(0,KernelRuntime.TorusAsk(blend,&actual));Near(Math.Sqrt(.8),actual.major_radius);Near(.2,actual.minor_radius);Near(.2,actual.basis_set.location.coord[2]);
        var uv=new PK_UV_s();uv.param[0]=.73;uv.param[1]=-Math.PI/2;var point=new PK_VECTOR_s();Assert.Equal(0,KernelRuntime.SurfEval(blend,uv,0,0,0,&point));Near(0,point.coord[2]);
        uv.param[1]=Math.Atan2(2.0/3,-Math.Sqrt(5)/3);Assert.Equal(0,KernelRuntime.SurfEval(blend,uv,0,0,0,&point));
        Near(1,point.coord[0]*point.coord[0]+point.coord[1]*point.coord[1]+(point.coord[2]-1)*(point.coord[2]-1));
    }

    [Fact]
    public void ReversedStraightSpineFailsWithoutPublishingGeometry()
    {
        var a=Plane(false);var b=Plane(true);var surfaces=KernelRuntime.Surfaces.AliveCount;var options=new PK_SURF_create_blend_o_s{o_t_version=1};int blend=999;
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_value,KernelRuntime.SurfCreateBlend(a,.2,b,.2,Point(0,0,0),Point(0,-1,0),&options,&blend));Assert.Equal(0,blend);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
    }

    private static int Plane(bool x)
    {var sf=new PK_PLANE_sf_s();sf.basis_set.axis.coord[x?0:2]=1;sf.basis_set.ref_direction.coord[x?1:0]=1;int tag=0;Assert.Equal(0,KernelRuntime.PlaneCreate(&sf,&tag));return tag;}
    private static PK_VECTOR_s Point(double x,double y,double z){var p=new PK_VECTOR_s();p.coord[0]=x;p.coord[1]=y;p.coord[2]=z;return p;}
    private static void Near(double a,double b)=>Assert.InRange(Math.Abs(a-b),0,1e-9);
}

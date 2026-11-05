using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class ConvexPlaneContactTests:IDisposable
{
    public ConvexPlaneContactTests(){KernelRuntime.SessionStop();var o=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&o));}
    public void Dispose(){KernelRuntime.SessionStop();}
    [Theory]
    [InlineData(0,1,0)] [InlineData(.015625,0,1)] [InlineData(-.015625,0,0)]
    public void AnIsolatedContactIsDistinguishedFromAClosedLoopAndAnEmptySet(double shift,int expectedPoints,int expectedCurves)
    {
        var surface=GeneralSurfaceIntersectionTests.Surface(false);var sf=new PK_PLANE_sf_s();sf.basis_set.location.coord[2]=-.0625+shift;sf.basis_set.axis.coord[2]=1;sf.basis_set.ref_direction.coord[0]=1;
        int plane=0;Assert.Equal(0,KernelRuntime.PlaneCreate(&sf,&plane));var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_pline_c};
        int np=0,nc=0;PK_VECTOR_s* points=null;int* curves=null;PK_INTERVAL_s* ranges=null;int* kinds=null;
        Assert.Equal(0,KernelRuntime.SurfIntersectSurf(surface,plane,&options,&np,&points,&nc,&curves,&ranges,&kinds));
        try
        {
            Assert.Equal(expectedPoints,np);Assert.Equal(expectedCurves,nc);
            if(np>0){Assert.Equal(.5,points[0].coord[0]);Assert.Equal(.5,points[0].coord[1]);Assert.Equal(-.0625,points[0].coord[2]);}
        }
        finally{if(points!=null)KernelRuntime.MemoryFree(points);if(curves!=null)KernelRuntime.MemoryFree(curves);if(ranges!=null)KernelRuntime.MemoryFree(ranges);if(kinds!=null)KernelRuntime.MemoryFree(kinds);}
    }
}

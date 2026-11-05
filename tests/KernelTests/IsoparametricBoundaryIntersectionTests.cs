using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class IsoparametricBoundaryIntersectionTests:IDisposable
{
    public IsoparametricBoundaryIntersectionTests(){KernelRuntime.SessionStop();var o=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&o));}
    public void Dispose(){KernelRuntime.SessionStop();}
    [Theory]
    [InlineData(.0954915028125263)] [InlineData(.8454915028125263)]
    [InlineData(.08856351567218329)] [InlineData(.8525784340995433)]
    public void RoundedIsoCurvesMeetTheirNaturalBoundaryWithinModelTolerance(double v)
    {
        var surface=GeneralSurfaceIntersectionTests.Surface(false);int edge=0,ray=0;
        Assert.Equal(0,KernelRuntime.BSurfMakeBCurveUIsoparam(surface,1,&edge));Assert.Equal(0,KernelRuntime.BSurfMakeBCurveVIsoparam(surface,v,&ray));
        var interval=new PK_INTERVAL_s();interval.value[1]=1;var options=new PK_CURVE_intersect_curve_o_s{o_t_version=1};
        int n=0;PK_VECTOR_s* points=null;double* a=null;double* b=null;int* kinds=null;
        Assert.Equal(0,KernelRuntime.CurveIntersectCurve(ray,interval,edge,interval,&options,&n,&points,&a,&b,&kinds));
        try{Assert.Equal(1,n);Assert.InRange(Math.Abs(a[0]-1),0,1e-8);Assert.InRange(Math.Abs(b[0]-v),0,1e-8);}
        finally{if(points!=null)KernelRuntime.MemoryFree(points);if(a!=null)KernelRuntime.MemoryFree(a);if(b!=null)KernelRuntime.MemoryFree(b);if(kinds!=null)KernelRuntime.MemoryFree(kinds);}
    }
}

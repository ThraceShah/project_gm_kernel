using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class GeneralSurfaceIntersectionTests : IDisposable
{
    public GeneralSurfaceIntersectionTests() { KernelRuntime.SessionStop(); var o = new PK_SESSION_start_o_s { o_t_version = 1 }; Assert.Equal(0, KernelRuntime.SessionStart(&o)); }
    public void Dispose() { KernelRuntime.InjectAllocationFailure(-1); KernelRuntime.SessionStop(); }

    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(true,true)]
    public void GeneralClosedIntersectionPublishesAnIndependentEvaluableICurve(bool nurbsTool,bool crossSpan)
    {
        var first = Surface(false,crossSpan); var second = nurbsTool ? Surface(true,remap:true) : Plane();
        var options = new PK_SURF_intersect_surf_o_s { o_t_version = 3, tolerance = 1e-8, mixed_curve_category = ParasolidConstants.PK_mixed_intersection_pline_c };
        int np = 0, nc = 0; PK_VECTOR_s* points = null; int* curves = null; PK_INTERVAL_s* ranges = null; int* kinds = null;
        Assert.Equal(0, KernelRuntime.SurfIntersectSurf(first, second, &options, &np, &points, &nc, &curves, &ranges, &kinds));
        try
        {
            Assert.Equal(0, np); Assert.Equal(1, nc);
            int cls = 0; Assert.Equal(0, KernelRuntime.EntityAskClass(curves[0], &cls)); Assert.Equal(ParasolidConstants.PK_CLASS_icurve, cls);
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &first)); Assert.Equal(0, KernelRuntime.EntityDelete(1, &second));
            var point = new PK_VECTOR_s();
            for (var i = 0; i <= 40; i++)
            {
                var parameter = ranges[0].value[0] + (ranges[0].value[1] - ranges[0].value[0]) * i / 40;
                Assert.Equal(0, KernelRuntime.CurveEval(curves[0], parameter, 0, &point));
                var x = point.coord[0]; var y = point.coord[1]; var z = point.coord[2];
                Assert.InRange(Math.Abs(z - ((x - .5) * (x - .5) + (y - .5) * (y - .5) - .0625)), 0, 1e-8);
                Assert.InRange(Math.Abs(z - (nurbsTool ? .08 * x * y : 0)), 0, 1e-8);
            }
            Assert.Equal(0, KernelRuntime.EntityDelete(nc, curves)); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
        }
        finally { KernelRuntime.MemoryFree(points); KernelRuntime.MemoryFree(curves); KernelRuntime.MemoryFree(ranges); KernelRuntime.MemoryFree(kinds); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SeedSelectionVerifiesMembershipBetweenChartAnchors(bool offCurve)
    {
        var first=Surface(false);var second=Plane();
        var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_pline_c,have_vector=1};
        options.vector.coord[0]=.5+.25*Math.Cos(.379);options.vector.coord[1]=.5+.25*Math.Sin(.379);options.vector.coord[2]=offCurve?.01:0;
        int np=0,nc=0;PK_VECTOR_s* points=null;int* curves=null;PK_INTERVAL_s* ranges=null;int* kinds=null;
        var before=KernelRuntime.Curves.AliveCount;
        var error=KernelRuntime.SurfIntersectSurf(first,second,&options,&np,&points,&nc,&curves,&ranges,&kinds);
        try
        {
            Assert.Equal(offCurve?ParasolidConstants.PK_ERROR_not_on_curve:0,error);
            Assert.Equal(offCurve?0:1,nc);Assert.True(KernelRuntime.IsValidTag(first)&&KernelRuntime.IsValidTag(second));
            if(offCurve)Assert.Equal(before,KernelRuntime.Curves.AliveCount);
            else {double t=0;Assert.Equal(0,KernelRuntime.CurveParameteriseVector(curves[0],options.vector,&t));}
        }
        finally{KernelRuntime.MemoryFree(points);KernelRuntime.MemoryFree(curves);KernelRuntime.MemoryFree(ranges);KernelRuntime.MemoryFree(kinds);}
    }

    [Fact]
    public void UnsupportedSpindleTorusDoesNotBecomeAnEmptyIntersection()
    {
        var first=Surface(false);var definition=new PK_TORUS_sf_s{major_radius=.3,minor_radius=.6};
        definition.basis_set.axis.coord[2]=1;definition.basis_set.ref_direction.coord[0]=1;
        int second=0;Assert.Equal(0,KernelRuntime.TorusCreate(&definition,&second));
        var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_pline_c};
        int np=0,nc=0;PK_VECTOR_s* points=null;int* curves=null;PK_INTERVAL_s* ranges=null;int* kinds=null;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.SurfIntersectSurf(first,second,&options,&np,&points,&nc,&curves,&ranges,&kinds));
        Assert.Equal(0,np);Assert.Equal(0,nc);Assert.True(points==null&&curves==null&&ranges==null&&kinds==null);
    }

    [Fact]
    public void GeneralIntersectionAndDeletionAllocateNoManagedMemoryAfterWarmup()
    {
        var first=Surface(false);var second=Surface(true);
        var options=new PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=ParasolidConstants.PK_mixed_intersection_pline_c};
        var error=0;long before=0;
        for(var i=0;i<4;i++)
        {
            if(i==1)before=GC.GetAllocatedBytesForCurrentThread();
            int np=0,nc=0;PK_VECTOR_s* points=null;int* curves=null;PK_INTERVAL_s* ranges=null;int* kinds=null;
            error|=KernelRuntime.SurfIntersectSurf(first,second,&options,&np,&points,&nc,&curves,&ranges,&kinds);
            if(nc!=1)error|=1;error|=KernelRuntime.EntityDelete(nc,curves);
            if(points!=null)error|=KernelRuntime.MemoryFree(points);
            if(curves!=null)error|=KernelRuntime.MemoryFree(curves);
            if(ranges!=null)error|=KernelRuntime.MemoryFree(ranges);
            if(kinds!=null)error|=KernelRuntime.MemoryFree(kinds);
        }
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(0,error);Assert.Equal(0,allocated);
        Assert.Equal(2,KernelRuntime.Surfaces.AliveCount);
    }

    internal static int Surface(bool tool,bool crossSpan=false,bool remap=false)
    {
        double* poles = stackalloc double[36]; double* z = stackalloc double[3] { .25, -.25, .25 };
        double* xs=stackalloc double[4]{0,.25,.75,1};double* zs=stackalloc double[4]{.25,0,0,.25};var nu=crossSpan?4:3;
        for (var u = 0; u < nu; u++) for (var v = 0; v < 3; v++)
        {
            var at = (u * 3 + v) * 3; poles[at] = crossSpan?xs[u]:u * .5; poles[at + 1] = v * .5;
            poles[at + 2] = tool ? .08 * poles[at] * v * .5 : (crossSpan?zs[u]:z[u]) + z[v] - .0625;
        }
        double* knots = stackalloc double[2] { 0, 1 }; int* mult = stackalloc int[2] { 3, 3 };
        double* uk=stackalloc double[3]{0,.5,1};int* um=stackalloc int[3]{3,1,3};double* remapped=stackalloc double[2]{-2,3};
        var definition = new PK_BSURF_sf_s { u_degree = 2, v_degree = 2, n_u_vertices = nu, n_v_vertices = 3,
            vertex_dim = 3, vertex = poles, n_u_knots = 2, n_v_knots = 2, u_knot = knots, v_knot = knots,
            u_knot_mult = mult, v_knot_mult = mult, form = ParasolidConstants.PK_BSURF_form_arbitrary_c,
            u_knot_type = ParasolidConstants.PK_knot_non_uniform_c, v_knot_type = ParasolidConstants.PK_knot_non_uniform_c,
            self_intersecting = ParasolidConstants.PK_self_intersect_false_c, convexity = ParasolidConstants.PK_convexity_unset_c };
        if(crossSpan){definition.n_u_knots=3;definition.u_knot=uk;definition.u_knot_mult=um;}
        if(remap){definition.u_knot=remapped;definition.v_knot=remapped;}
        int tag = 0; Assert.Equal(0, KernelRuntime.BSurfCreate(&definition, &tag)); return tag;
    }
    internal static int Plane()
    {
        var definition = new PK_PLANE_sf_s(); definition.basis_set.axis.coord[2] = 1; definition.basis_set.ref_direction.coord[0] = 1;
        int tag = 0; Assert.Equal(0, KernelRuntime.PlaneCreate(&definition, &tag)); return tag;
    }
}

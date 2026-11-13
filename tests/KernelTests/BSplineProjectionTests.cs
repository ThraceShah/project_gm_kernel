using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Projection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class BSplineProjectionTests : IDisposable
{
    public BSplineProjectionTests() { KernelRuntime.SessionStop(); var start = new PK_SESSION_start_o_s { o_t_version = 1 }; Assert.Equal(0, KernelRuntime.SessionStart(&start)); }
    public void Dispose() => KernelRuntime.SessionStop();

    [Fact]
    public void SurfaceInverseReturnsVerifiedOriginalUVAndRejectsOffSurfaceInput()
    {
        var surface = GeneralSurfaceIntersectionTests.Surface(false); var original = new PK_UV_s(); original.param[0] = .17; original.param[1] = .82;
        var point = new PK_VECTOR_s(); Assert.Equal(0, KernelRuntime.SurfEval(surface, original, 0, 0, 0, &point));
        var recovered = new PK_UV_s(); Assert.Equal(0, KernelRuntime.SurfParameteriseVector(surface, point, &recovered));
        Assert.InRange(Math.Abs(recovered.param[0] - original.param[0]), 0, 1e-8);
        Assert.InRange(Math.Abs(recovered.param[1] - original.param[1]), 0, 1e-8);
        var on=point;
        foreach(var shift in new[]{-.01,.01})
        {
            point=on;point.coord[2]+=shift;
            Assert.Equal(ParasolidConstants.PK_ERROR_not_on_surface,KernelRuntime.SurfParameteriseVector(surface,point,&recovered));
        }
        point=on;point.coord[2]+=.01;
        var before=GC.GetAllocatedBytesForCurrentThread();var status=KernelRuntime.SurfParameteriseVector(surface,point,&recovered);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_on_surface,status);Assert.Equal(0,allocated);
        point.coord[2] = -2;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_on_surface, KernelRuntime.SurfParameteriseVector(surface, point, &recovered));
    }

    [Fact]
    public void GlobalProjectionClosesDistanceGapAtAnInteriorMinimum()
    {
        double[] poles = new double[27], z = [.25, -.25, .25], knots = [0,0,0,1,1,1];
        for (var u = 0; u < 3; u++) for (var v = 0; v < 3; v++)
        { var at = (u * 3 + v) * 3; poles[at] = u * .5; poles[at + 1] = v * .5; poles[at + 2] = z[u] + z[v] - .0625; }
        var surface = new BSurfaceView(2,2,3,3,3,false,false,false,poles,knots,knots);
        var best = new ProjectionPoint { DistanceUpper = double.PositiveInfinity };
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.ClosestBSurface(surface, new() { X=.5,Y=.5,Z=-1 }, 1e-9, ref best));
        Assert.InRange(Math.Abs(best.U - .5), 0, 1e-7); Assert.InRange(Math.Abs(best.V - .5), 0, 1e-7);
        Assert.InRange(best.DistanceUpper, .9375 - 1e-12, .9375 + 1e-9);
    }

    [Fact]
    public void CurveInverseUsesTheWholeSplineDomain()
    {
        double* poles = stackalloc double[9] { 0,0,0, .5,0,0, 1,1,0 }; double* knots = stackalloc double[2] { 0,1 }; int* mult = stackalloc int[2] { 3,3 };
        var definition = new PK_BCURVE_sf_s { degree=2,n_vertices=3,vertex_dim=3,vertex=poles,n_knots=2,knot=knots,knot_mult=mult,
            form=ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=ParasolidConstants.PK_knot_bezier_ends_c,self_intersecting=ParasolidConstants.PK_self_intersect_false_c };
        int curve=0; Assert.Equal(0, KernelRuntime.BCurveCreate(&definition,&curve));
        var point = new PK_VECTOR_s(); Assert.Equal(0,KernelRuntime.CurveEval(curve,.237,0,&point)); double t=0;
        Assert.Equal(0,KernelRuntime.CurveParameteriseVector(curve,point,&t)); Assert.InRange(Math.Abs(t-.237),0,1e-8);
    }
}

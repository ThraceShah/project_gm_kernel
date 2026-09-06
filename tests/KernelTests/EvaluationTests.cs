using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public unsafe class EvaluationTests : IDisposable
{
    public EvaluationTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Fact]
    public void Circle_UnitTangentAndTenDerivatives_ThroughExport()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidCyl(3, 5, null, &body));
        var curve = FirstCurve(body, CurveClass.Circle);
        delegate* unmanaged<int, double, int, PK_VECTOR_s*, PK_VECTOR_s*, int> evaluate = &KernelExports.PK_CURVE_eval_with_tangent;
        PK_VECTOR_s* output = stackalloc PK_VECTOR_s[12];
        output[11].coord[0] = 987;
        PK_VECTOR_s tangent;
        Assert.Equal(0, evaluate(curve, 0, 10, output, &tangent));
        Assert.Equal(3, output[0].coord[0], 12);
        Assert.Equal(3, output[1].coord[1], 12);
        Assert.Equal(-3, output[2].coord[0], 12);
        Assert.Equal(-3, output[10].coord[0], 12);
        Assert.Equal(1, tangent.coord[1], 12);
        Assert.Equal(987, output[11].coord[0]);
        Assert.Equal(0, evaluate(curve, Math.Tau, 0, output, &tangent));
        Assert.Equal(1, tangent.coord[1], 12);
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_parameter, evaluate(curve, 0, -1, output, &tangent));
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_parameter, evaluate(curve, double.NaN, 0, output, &tangent));
    }

    [Fact]
    public void Line_EvaluatesBeyondEdgeRange_AndHigherDerivativesAreZero()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body));
        var curve = FirstCurve(body, CurveClass.Line);
        var record = KernelRuntime.GetCurveByTag(curve);
        var line = KernelRuntime.GetLineData(record.DataIndex);
        PK_VECTOR_s* output = stackalloc PK_VECTOR_s[11];
        delegate* unmanaged<int, double, int, PK_VECTOR_s*, int> evaluate = &KernelExports.PK_CURVE_eval;
        Assert.Equal(0, evaluate(curve, -100, 10, output));
        Assert.Equal(line.LocationX - 100 * line.AxisX, output[0].coord[0], 12);
        Assert.Equal(line.LocationY - 100 * line.AxisY, output[0].coord[1], 12);
        Assert.Equal(line.LocationZ - 100 * line.AxisZ, output[0].coord[2], 12);
        for (var i = 2; i <= 10; i++)
        for (var j = 0; j < 3; j++) Assert.Equal(0, output[i].coord[j]);
    }

    [Fact]
    public void SurfaceExport_ByValueUv_UFastAndTriangularPacking()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidSphere(3, null, &body));
        var surface = FirstSurface(body);
        delegate* unmanaged<int, PK_UV_s, int, int, byte, PK_VECTOR_s*, int> evaluate = &KernelExports.PK_SURF_eval;
        PK_VECTOR_s* rectangular = stackalloc PK_VECTOR_s[10];
        PK_VECTOR_s* triangular = stackalloc PK_VECTOR_s[7];
        rectangular[9].coord[0] = triangular[6].coord[0] = 987;
        Assert.Equal(0, evaluate(surface, Uv(0, 0), 2, 2, 0, rectangular));
        Assert.Equal(3, rectangular[0].coord[0]);
        Assert.Equal(3, rectangular[1].coord[1]);
        Assert.Equal(-3, rectangular[2].coord[0]);
        Assert.Equal(3, rectangular[3].coord[2]);
        Assert.Equal(-3, rectangular[6].coord[0]);
        Assert.Equal(0, evaluate(surface, Uv(0, 0), 2, 2, 1, triangular));
        ReadOnlySpan<int> map = [0, 1, 2, 3, 4, 6];
        for (var i = 0; i < 6; i++)
        for (var j = 0; j < 3; j++) Assert.Equal(rectangular[map[i]].coord[j], triangular[i].coord[j]);
        Assert.Equal(987, triangular[6].coord[0]);
        Assert.Equal(987, rectangular[9].coord[0]);
    }

    [Fact]
    public void InvalidInputs_DoNotOverwriteOutputs_AndDeletedTagsAreRejected()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidSphere(3, null, &body));
        var surface = FirstSurface(body);
        PK_VECTOR_s result = default;
        result.coord[0] = 987;
        Assert.Equal(ParasolidConstants.PK_ERROR_num_derivs_not_equal,
            KernelRuntime.SurfEval(surface, Uv(0, 0), 2, 1, 1, &result));
        Assert.Equal(ParasolidConstants.PK_ERROR_too_many_derivatives,
            KernelRuntime.SurfEval(surface, Uv(0, 0), 11, 0, 0, &result));
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_parameter,
            KernelRuntime.SurfEval(surface, Uv(0, Math.PI), 0, 0, 0, &result));
        Assert.Equal(ParasolidConstants.PK_ERROR_wrong_entity,
            KernelRuntime.CurveEval(surface, 0, 0, &result));
        Assert.Equal(987, result.coord[0]);
        Assert.Equal(ParasolidConstants.PK_ERROR_is_attached, KernelRuntime.EntityDelete(1, &surface));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.Equal(ParasolidConstants.PK_ERROR_not_a_tag,
            KernelRuntime.SurfEval(surface, Uv(0, 0), 0, 0, 0, &result));
    }

    [Fact]
    public void EvaluationHotPath_HasNoManagedAllocations()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidCyl(3, 5, null, &body));
        var curve = FirstCurve(body, CurveClass.Circle);
        var surface = FirstSurface(body);
        PK_VECTOR_s* output = stackalloc PK_VECTOR_s[121];
        PK_VECTOR_s tangent;
        var uv = Uv(0.2, 0.3);
        delegate* unmanaged<int, double, int, PK_VECTOR_s*, PK_VECTOR_s*, int> curveEval = &KernelExports.PK_CURVE_eval_with_tangent;
        delegate* unmanaged<int, PK_UV_s, int, int, byte, PK_VECTOR_s*, int> surfEval = &KernelExports.PK_SURF_eval;
        for (var i = 0; i < 100; i++)
        {
            curveEval(curve, 0.2, 10, output, &tangent);
            surfEval(surface, uv, 10, 10, 0, output);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = 0;
        for (var i = 0; i < 1000; i++)
        {
            error |= curveEval(curve, 0.2, 10, output, &tangent);
            error |= surfEval(surface, uv, 10, 10, 0, output);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, error);
        Assert.Equal(0, allocated);
    }

    private static int FirstSurface(int body)
    {
        int count;
        int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &faces));
        int surface;
        Assert.Equal(0, KernelRuntime.FaceAskSurf(faces[0], &surface));
        return surface;
    }

    private static int FirstCurve(int body, CurveClass kind)
    {
        int count;
        int* edges;
        Assert.Equal(0, KernelRuntime.BodyAskEdges(body, &count, &edges));
        for (var i = 0; i < count; i++)
        {
            int curve;
            Assert.Equal(0, KernelRuntime.EdgeAskCurve(edges[i], &curve));
            if (KernelRuntime.GetCurveByTag(curve).Class == kind) return curve;
        }
        throw new InvalidOperationException("No expected curve.");
    }

    private static PK_UV_s Uv(double u, double v)
    {
        PK_UV_s result = default;
        result.param[0] = u;
        result.param[1] = v;
        return result;
    }
}

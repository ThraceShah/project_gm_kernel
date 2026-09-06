using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public unsafe class BCurveEvaluationTests : IDisposable
{
    public BCurveEvaluationTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Fact]
    public void CreateCopiesDefinition_AndExportsEvaluateQuadratic()
    {
        double[] poles = [0, 0, 0, 1, 2, 0, 3, 1, 2];
        double[] knots = [0, 4];
        int[] mult = [3, 3];
        var curve = Create(2, poles, knots, mult);
        poles.AsSpan().Fill(999);
        knots.AsSpan().Fill(999);
        mult.AsSpan().Fill(999);
        int kind;
        Assert.Equal(0, KernelRuntime.EntityAskClass(curve, &kind));
        Assert.Equal(ParasolidConstants.PK_CLASS_bcurve, kind);
        var output = stackalloc PK_VECTOR_s[11];
        delegate* unmanaged<int, double, int, PK_VECTOR_s*, int> eval = &KernelExports.PK_CURVE_eval;
        Assert.Equal(0, eval(curve, 2, 10, output));
        Assert.Equal(1.25, output[0].coord[0], 13);
        Assert.Equal(1.25, output[0].coord[1], 13);
        Assert.Equal(0.5, output[0].coord[2], 13);
        Assert.Equal(0.75, output[1].coord[0], 13);
        Assert.Equal(0.125, output[2].coord[0], 13);
        for (var i = 3; i <= 10; i++)
            Assert.Equal(0, output[i].coord[0]);
    }

    [Fact]
    public void RationalCurve_HasFullInternalDerivatives_AndPkDegreeTruncation()
    {
        var w = Math.Sqrt(0.5);
        double[] poles = [1, 0, 0, 1, w, w, 0, w, 0, 1, 0, 1];
        var curve = Create(2, poles, [0, 4], [3, 3], rational: true);
        var output = stackalloc PK_VECTOR_s[4];
        Assert.Equal(0, KernelRuntime.CurveEval(curve, 2, 3, output));
        Assert.Equal(w, output[0].coord[0], 13);
        Assert.Equal(w, output[0].coord[1], 13);
        Assert.Equal(0, output[3].coord[0]);
        var record = KernelRuntime.GetCurveByTag(curve);
        var view = KernelRuntime.GetBCurveView(in KernelRuntime.BCurveDataStore[record.DataIndex]);
        Span<double> workspace = stackalloc double[36];
        Span<KernelVector3> exact = stackalloc KernelVector3[4];
        Span<KernelVector3> plus = stackalloc KernelVector3[3];
        Span<KernelVector3> minus = stackalloc KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, BCurveEvaluation.Evaluate(in view, 2, 3, exact, workspace, out _));
        Assert.Equal(AlgorithmStatus.Success, BCurveEvaluation.Evaluate(in view, 2.0001, 2, plus, workspace, out _));
        Assert.Equal(AlgorithmStatus.Success, BCurveEvaluation.Evaluate(in view, 1.9999, 2, minus, workspace, out _));
        Assert.True(Math.Abs(exact[3].X) > 0.01);
        Assert.InRange(Math.Abs(exact[3].X - (plus[2].X - minus[2].X) / 0.0002), 0, 1e-8);
    }

    [Fact]
    public void RepeatedKnot_UsesRightPiece_AndEndpointsExtrapolateLinearly()
    {
        var curve = Create(2, [0, 0, 0, 1, 0, 0, 2, 0, 0, 2, 1, 0, 2, 2, 0], [0, 2, 4], [3, 2, 3]);
        var output = stackalloc PK_VECTOR_s[3];
        Assert.Equal(0, KernelRuntime.CurveEval(curve, 2, 2, output));
        Assert.Equal(0, output[1].coord[0]);
        Assert.Equal(1, output[1].coord[1]);
        Assert.Equal(0, KernelRuntime.CurveEval(curve, Math.BitDecrement(2), 1, output));
        Assert.Equal(1, output[1].coord[1], 13);
        Assert.Equal(0, KernelRuntime.CurveEval(curve, 2 - 1e-8, 1, output));
        Assert.Equal(1, output[1].coord[0], 13);
        Assert.Equal(0, KernelRuntime.CurveEval(curve, -1, 2, output));
        Assert.Equal(-1, output[0].coord[0]);
        Assert.Equal(0, output[2].coord[0]);
        Assert.Equal(0, KernelRuntime.CurveEval(curve, 5, 2, output));
        Assert.Equal(3, output[0].coord[1]);
    }

    [Fact]
    public void PeriodicCurve_WrapsBothWays()
    {
        var curve = Create(3, [1, 0, 0, 0, 1, 0, -1, 0, 0, 0, -1, 0, 1, 0, 0, 0, 1, 0, -1, 0, 0],
            [-3, -2, -1, 0, 1, 2, 3, 4, 5, 6, 7], [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1], periodic: true);
        var expected = stackalloc PK_VECTOR_s[4];
        var actual = stackalloc PK_VECTOR_s[4];
        Assert.Equal(0, KernelRuntime.CurveEval(curve, 0.25, 3, expected));
        foreach (var t in new[] { -7.75, 8.25 })
        {
            Assert.Equal(0, KernelRuntime.CurveEval(curve, t, 3, actual));
            for (var i = 0; i <= 3; i++)
                for (var j = 0; j < 3; j++) Assert.Equal(expected[i].coord[j], actual[i].coord[j], 13);
        }
    }

    [Fact]
    public void StationaryEndpoint_CanEvaluatePosition_ButHasNoUnitTangent()
    {
        var curve = Create(2, [0, 0, 0, 0, 0, 0, 2, 0, 0], [0, 4], [3, 3]);
        PK_VECTOR_s output, tangent;
        Assert.Equal(0, KernelRuntime.CurveEval(curve, 0, 0, &output));
        Assert.Equal(ParasolidConstants.PK_ERROR_at_singularity,
            KernelRuntime.CurveEvalWithTangent(curve, 0, 0, &output, &tangent));
        Assert.Equal(0, KernelRuntime.CurveEvalWithTangent(curve, 1, 0, &output, &tangent));
        Assert.Equal(1, tangent.coord[0]);
        var slow = Create(1, [0, 0, 0, 1, 1, 1], [0, 1e12], [2, 2]);
        Assert.Equal(0, KernelRuntime.CurveEval(slow, 5e11, 0, &output));
        Assert.Equal(ParasolidConstants.PK_ERROR_at_singularity,
            KernelRuntime.CurveEvalWithTangent(slow, 5e11, 0, &output, &tangent));
    }

    [Fact]
    public void MarksRestorePayloadCounts_AndRestoreDeletedCurve()
    {
        var existing = Create(1, [0, 0, 0, 1, 2, 3], [0, 4], [2, 2]);
        var dataAlive = KernelRuntime.BCurveDataStore.AliveCount;
        var blockBytes = KernelRuntime.BlocksLiveBytes;
        int mark;
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        var added = Create(2, [0, 0, 0, 1, 2, 0, 3, 1, 2], [0, 4], [3, 3]);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &existing));
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.Equal(dataAlive, KernelRuntime.BCurveDataStore.AliveCount);
        Assert.Equal(blockBytes, KernelRuntime.BlocksLiveBytes);
        PK_VECTOR_s output;
        Assert.Equal(0, KernelRuntime.CurveEval(existing, 1, 0, &output));
        Assert.Equal(ParasolidConstants.PK_ERROR_not_a_tag, KernelRuntime.CurveEval(added, 1, 0, &output));
    }

    [Fact]
    public void InvalidKnotsAndWeights_DoNotAllocatePayload()
    {
        double* poles = stackalloc double[8] { 0, 0, 0, 1, 1, 2, 3, -1 };
        double* knots = stackalloc double[2] { 0, 4 };
        int* mult = stackalloc int[2] { 2, 2 };
        var sf = new PK_BCURVE_sf_s
        {
            degree = 1,
            n_vertices = 2,
            vertex_dim = 4,
            is_rational = 1,
            vertex = poles,
            n_knots = 2,
            knot = knots,
            knot_mult = mult,
            form = ParasolidConstants.PK_BCURVE_form_unset_c,
            knot_type = ParasolidConstants.PK_knot_unset_c,
            self_intersecting = ParasolidConstants.PK_self_intersect_unset_c
        };
        var curve = 987;
        Assert.Equal(ParasolidConstants.PK_ERROR_weight_le_0, KernelRuntime.BCurveCreate(&sf, &curve));
        poles[7] = 1;
        mult[1] = 1;
        Assert.Equal(ParasolidConstants.PK_ERROR_wrong_number_knots, KernelRuntime.BCurveCreate(&sf, &curve));
        mult[1] = 2;
        sf.is_periodic = sf.is_closed = 1;
        Assert.Equal(ParasolidConstants.PK_ERROR_periodic_open, KernelRuntime.BCurveCreate(&sf, &curve));
        Assert.Equal(987, curve);
        Assert.Equal(0, KernelRuntime.BCurveDataStore.AliveCount);
        Assert.Equal(0UL, KernelRuntime.BlocksLiveBytes);
    }

    [Fact]
    public void BCurveEvaluationExport_HasNoHotPathManagedAllocations()
    {
        var curve = Create(3, [0, 0, 0, 1, 1.1, 2.2, 0, 1.1, 1.8, -0.9, 0.9, 0.9, 4, 1, 2, 1], [0, 8], [4, 4], rational: true);
        var output = stackalloc PK_VECTOR_s[11];
        PK_VECTOR_s tangent;
        delegate* unmanaged<int, double, int, PK_VECTOR_s*, PK_VECTOR_s*, int> eval = &KernelExports.PK_CURVE_eval_with_tangent;
        for (var i = 0; i < 100; i++) eval(curve, 2.25, 10, output, &tangent);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = 0;
        for (var i = 0; i < 1000; i++) error |= eval(curve, 2.25, 10, output, &tangent);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, error);
        Assert.Equal(0, allocated);
    }

    private static int Create(int degree, double[] poles, double[] knots, int[] multiplicities, bool rational = false, bool periodic = false)
    {
        fixed (double* p = poles)
        fixed (double* k = knots)
        fixed (int* m = multiplicities)
        {
            var dimension = rational ? 4 : 3;
            var sf = new PK_BCURVE_sf_s
            {
                degree = degree,
                n_vertices = poles.Length / dimension,
                vertex_dim = dimension,
                vertex = p,
                n_knots = knots.Length,
                knot = k,
                knot_mult = m,
                is_rational = (byte)(rational ? 1 : 0),
                is_periodic = (byte)(periodic ? 1 : 0),
                is_closed = (byte)(periodic ? 1 : 0),
                form = ParasolidConstants.PK_BCURVE_form_unset_c,
                knot_type = ParasolidConstants.PK_knot_unset_c,
                self_intersecting = ParasolidConstants.PK_self_intersect_unset_c
            };
            int curve;
            delegate* unmanaged<PK_BCURVE_sf_s*, int*, int> create = &KernelExports.PK_BCURVE_create;
            Assert.Equal(0, create(&sf, &curve));
            return curve;
        }
    }
}

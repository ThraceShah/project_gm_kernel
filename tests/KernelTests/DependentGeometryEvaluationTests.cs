using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

/// <summary>
/// Evaluation of the dependent geometry classes: ellipse, trimmed curve,
/// surface parameter curve, B-surface, offset/swept/spun surfaces. Each test
/// checks the evaluator against an independently computed closed form
/// (Cox-de Boor basis recursion, trigonometric formulas or central differences).
/// </summary>
public unsafe class DependentGeometryEvaluationTests : IDisposable
{
    public DependentGeometryEvaluationTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Fact]
    public void Ellipse_PositionTangentAndDerivatives_MatchClosedForm()
    {
        var (r1, r2) = (4.0, 1.5);
        var ellipse = CreateEllipse(r1, r2);
        var output = stackalloc PK_VECTOR_s[11];
        PK_VECTOR_s tangent;
        for (var sample = 0; sample <= 16; sample++)
        {
            var t = Math.Tau * sample / 16 - 3.1;
            Assert.Equal(0, KernelRuntime.CurveEvalWithTangent(ellipse, t, 10, output, &tangent));
            Assert.Equal(r1 * Math.Cos(t), output[0].coord[0], 13);
            Assert.Equal(r2 * Math.Sin(t), output[0].coord[1], 13);
            Assert.Equal(1, Length(tangent), 13);
            // First derivative against the central difference of positions.
            Assert.InRange(Math.Abs(output[1].coord[0] - r1 * (Math.Cos(t + 1e-6) - Math.Cos(t - 1e-6)) / 2e-6), 0, 1e-6);
            // Second and third derivatives follow the trigonometric cycle.
            Assert.Equal(-r1 * Math.Cos(t), output[2].coord[0], 12);
            Assert.Equal(-r2 * Math.Sin(t), output[2].coord[1], 12);
            Assert.Equal(r1 * Math.Sin(t), output[3].coord[0], 12);
            Assert.Equal(-r2 * Math.Cos(t), output[3].coord[1], 12);
        }
        Assert.Equal(ParasolidConstants.PK_ERROR_distance_le_0, CreateRawEllipse(0, 1.5, out _));
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_parameter, CreateRawEllipse(1.0, 4.0, out _));
    }

    [Fact]
    public void TrimmedCurve_DelegatesToBasisParameter_AndStoresInterval()
    {
        var basis = CreateEllipse(3, 3);   // a circle as an ellipse with equal radii
        int trimmed;
        var interval = new PK_INTERVAL_s();
        interval.value[0] = 1.0;
        interval.value[1] = 4.0;
        var sf = new PK_TRCURVE_sf_s { basis_curve = basis, t_int = interval };
        Assert.Equal(0, KernelRuntime.TrCurveCreate(&sf, &trimmed));
        var record = KernelRuntime.GetCurveByTag(trimmed);
        Assert.Equal(CurveClass.TRCurve, record.Class);
        Assert.Equal(1.0, record.TMin, 14);
        Assert.Equal(4.0, record.TMax, 14);
        var output = stackalloc PK_VECTOR_s[3];
        var basisOutput = stackalloc PK_VECTOR_s[3];
        Assert.Equal(0, KernelRuntime.CurveEval(trimmed, 2.5, 2, output));
        Assert.Equal(0, KernelRuntime.CurveEval(basis, 2.5, 2, basisOutput));
        for (var i = 0; i <= 2; i++)
        for (var axis = 0; axis < 3; axis++)
            Assert.Equal(basisOutput[i].coord[axis], output[i].coord[axis], 13);
        // Outside the trimmed interval the basis curve extension is still used.
        Assert.Equal(0, KernelRuntime.CurveEval(trimmed, 7.0, 0, output));
        Assert.Equal(0, KernelRuntime.CurveEval(basis, 7.0, 0, basisOutput));
        Assert.Equal(basisOutput[0].coord[0], output[0].coord[0], 13);
        // Reversed interval: negative sense, unchanged parameterisation.
        var reversed = new PK_INTERVAL_s();
        reversed.value[0] = 4.0;
        reversed.value[1] = 1.0;
        sf = new PK_TRCURVE_sf_s { basis_curve = basis, t_int = reversed };
        Assert.Equal(0, KernelRuntime.TrCurveCreate(&sf, &trimmed));
        Assert.Equal(ParasolidConstants.PK_TOPOL_sense_negative_c, KernelRuntime.GetCurveByTag(trimmed).Sense);
    }

    [Fact]
    public void SpCurve_PlainAndRational2DBCurves_TraceHelixOnCylinder()
    {
        int surface;
        var cyl = new PK_CYL_sf_s { radius = 2 };
        cyl.basis_set.axis.coord[2] = 1;
        cyl.basis_set.ref_direction.coord[0] = 1;
        Assert.Equal(0, KernelRuntime.CylCreate(&cyl, &surface));
        foreach (var rational in new[] { false, true })
        {
            // (u, v) moves from (0, 0) to (tau, 5): a helix of one turn.
            var dim = rational ? 3 : 2;
            var poles = rational ? new[] { 0.0, 0, 1, Math.Tau, 5, 1 } : new[] { 0.0, 0, Math.Tau, 5 };
            var bcurve = Create2DBCurve(1, poles, [0, 2], [2, 2], rational);
            int spCurve;
            var spSf = new PK_SPCURVE_sf_s { surf = surface, curve = bcurve };
            Assert.Equal(0, KernelRuntime.SpCurveCreate(&spSf, &spCurve));
            var output = stackalloc PK_VECTOR_s[3];
            for (var sample = 0; sample <= 8; sample++)
            {
                var t = 2.0 * sample / 8;
                Assert.Equal(0, KernelRuntime.CurveEval(spCurve, t, 2, output));
                var u = Math.Tau * t / 2;
                Assert.Equal(2 * Math.Cos(u), output[0].coord[0], 12);
                Assert.Equal(2 * Math.Sin(u), output[0].coord[1], 12);
                Assert.Equal(5.0 * t / 2, output[0].coord[2], 12);
                // dP/dt = Pu·u' + Pv·v' with u' = tau/2, v' = 2.5.
                var du = Math.Tau / 2;
                Assert.Equal(-2 * Math.Sin(u) * du, output[1].coord[0], 10);
                Assert.Equal(2 * Math.Cos(u) * du, output[1].coord[1], 10);
                Assert.Equal(2.5, output[1].coord[2], 10);
                // Second derivative: only Puu·u'^2 survives.
                Assert.Equal(-2 * Math.Cos(u) * du * du, output[2].coord[0], 9);
                Assert.Equal(0, output[2].coord[2], 12);
            }
        }
    }

    [Fact]
    public void BSurface_BezierPatch_MatchesBasisRecursion_AndZeroFillsAboveDegree()
    {
        // Cubic Bézier patch: 4x4 poles, degree (3, 3), clamped knots.
        var poles = new double[4 * 4 * 3];
        for (var i = 0; i < 4; i++)
        for (var j = 0; j < 4; j++)
        {
            poles[(i * 4 + j) * 3] = i - 0.4 * j;
            poles[(i * 4 + j) * 3 + 1] = 0.3 * i * i + j;
            poles[(i * 4 + j) * 3 + 2] = 0.5 * i * j - 0.2 * j * j;
        }
        var surface = CreateBSurface(3, 3, 4, 4, poles, [0, 1], [4, 4], [0, 1], [4, 4]);
        var output = stackalloc PK_VECTOR_s[121];
        for (var sampleU = 0; sampleU <= 4; sampleU++)
        for (var sampleV = 0; sampleV <= 4; sampleV++)
        {
            var (u, v) = (sampleU / 4.0, sampleV / 4.0);
            Assert.Equal(0, KernelRuntime.SurfEval(surface, Uv(u, v), 10, 10, 0, output));
            for (var orderU = 0; orderU <= 10; orderU++)
            for (var orderV = 0; orderV <= 10; orderV++)
            {
                var index = orderV * 11 + orderU;
                if (orderU > 3 || orderV > 3)
                {
                    for (var axis = 0; axis < 3; axis++) Assert.Equal(0, output[index].coord[axis]);
                    continue;
                }
                var expected = SplineSurfaceDerivative(poles, 4, 4, [0, 0, 0, 0, 1, 1, 1, 1], [0, 0, 0, 0, 1, 1, 1, 1], 3, 3, u, v, orderU, orderV);
                if (Math.Abs(expected.X - output[index].coord[0]) > 1e-9 || Math.Abs(expected.Y - output[index].coord[1]) > 1e-9 || Math.Abs(expected.Z - output[index].coord[2]) > 1e-9)
                    Console.WriteLine($"mismatch at u={u} v={v} order=({orderU},{orderV}) expected=({expected.X:R},{expected.Y:R},{expected.Z:R}) actual=({output[index].coord[0]:R},{output[index].coord[1]:R},{output[index].coord[2]:R})");
                Assert.Equal(expected.X, output[index].coord[0], 10);
                Assert.Equal(expected.Y, output[index].coord[1], 10);
                Assert.Equal(expected.Z, output[index].coord[2], 10);
            }
        }
    }

    [Fact]
    public void BSurface_PeriodicUDirection_WrapsSeamSmoothly()
    {
        // Periodic in u: quadratic, 4 distinct poles on the unit circle plus a
        // 2-pole wrap, uniform unclamped knots [0..8] over domain [2, 6].
        double[] circle = [1, 0, 0, 0.5, 0.8660254037844386, 0, -0.5, 0.8660254037844386, 0, -1, 0, 0];
        var poles = new double[6 * 3 * 3];
        for (var i = 0; i < 6; i++)
        for (var j = 0; j < 3; j++)
        {
            poles[(i * 3 + j) * 3] = circle[(i % 4) * 3];
            poles[(i * 3 + j) * 3 + 1] = circle[(i % 4) * 3 + 1];
            poles[(i * 3 + j) * 3 + 2] = j * 0.5;
        }
        var surface = CreateBSurface(2, 2, 6, 3, poles,
            [0, 1, 2, 3, 4, 5, 6, 7, 8], [1, 1, 1, 1, 1, 1, 1, 1, 1], [0, 1], [3, 3],
            isUPeriodic: true);
        var output = stackalloc PK_VECTOR_s[9];
        var wrapped = stackalloc PK_VECTOR_s[9];
        foreach (var u in new[] { 2.5, 4.0, 5.75 })
        {
            Assert.Equal(0, KernelRuntime.SurfEval(surface, Uv(u, 0.5), 2, 2, 0, output));
            Assert.Equal(0, KernelRuntime.SurfEval(surface, Uv(u - 4.0, 0.5), 2, 2, 0, wrapped));
            for (var i = 0; i < 9; i++)
            for (var axis = 0; axis < 3; axis++)
                Assert.Equal(output[i].coord[axis], wrapped[i].coord[axis], 9);
        }
        // Position check against the reference recursion at several u.
        double[] referencePoles = new double[6 * 3 * 3];
        for (var i = 0; i < 6; i++)
        for (var j = 0; j < 3; j++)
        {
            referencePoles[(i * 3 + j) * 3] = poles[(i * 3 + j) * 3];
            referencePoles[(i * 3 + j) * 3 + 1] = poles[(i * 3 + j) * 3 + 1];
            referencePoles[(i * 3 + j) * 3 + 2] = poles[(i * 3 + j) * 3 + 2];
        }
        for (var sample = 0; sample <= 8; sample++)
        {
            var u = 2.0 + 4.0 * sample / 8;
            Assert.Equal(0, KernelRuntime.SurfEval(surface, Uv(u, 0.0), 0, 0, 0, output));
            var expected = SplineSurfaceDerivative(referencePoles, 6, 3, [0, 1, 2, 3, 4, 5, 6, 7, 8], [0, 0, 0, 1, 1, 1], 2, 2, u, 0.0, 0, 0);
            Assert.Equal(expected.X, output[0].coord[0], 10);
            Assert.Equal(expected.Y, output[0].coord[1], 10);
            Assert.Equal(expected.Z, output[0].coord[2], 10);
        }
    }

    [Fact]
    public void SweptSurface_SectionPlusVTimesDirection()
    {
        var section = CreateEllipse(3, 1);
        int swept;
        var direction = new PK_VECTOR_s();
        direction.coord[2] = 1;
        var sweptSf = new PK_SWEPT_sf_s { curve = section, direction = direction };
        Assert.Equal(0, KernelRuntime.SweptCreate(&sweptSf, &swept));
        var output = stackalloc PK_VECTOR_s[16];
        for (var sample = 0; sample <= 4; sample++)
        {
            var u = 0.7 * sample;
            for (var v = -2.0; v <= 2.0; v += 1.5)
            {
                Assert.Equal(0, KernelRuntime.SurfEval(swept, Uv(u, v), 3, 3, 0, output));
                Assert.Equal(3 * Math.Cos(u), output[0].coord[0], 12);
                Assert.Equal(Math.Sin(u), output[0].coord[1], 12);
                Assert.Equal(v, output[0].coord[2], 12);
                // Su is the section derivative; Sv is the constant direction.
                Assert.Equal(-3 * Math.Sin(u), output[1].coord[0], 11);
                Assert.Equal(Math.Cos(u), output[1].coord[1], 11);
                Assert.Equal(1, output[4].coord[2], 12);    // (0, 1) in u-fast packing
                Assert.Equal(0, output[5].coord[0], 12);    // (1, 1) = Suv = 0
                for (var j = 2; j <= 3; j++)
                for (var i = 0; i <= 3; i++)
                for (var axis = 0; axis < 3; axis++)
                    Assert.Equal(0, output[j * 4 + i].coord[axis]);
            }
        }
    }

    [Fact]
    public void SpunSurface_ProfileRotatedAroundAxis()
    {
        var profile = CreateEllipse(2, 1);
        int spun;
        var axis = new PK_AXIS1_sf_s();
        axis.axis.coord[2] = 1;
        axis.location.coord[0] = 0.25;
        axis.location.coord[1] = -0.5;
        var spunSf = new PK_SPUN_sf_s { curve = profile, axis = axis };
        Assert.Equal(0, KernelRuntime.SpunCreate(&spunSf, &spun));
        var output = stackalloc PK_VECTOR_s[9];
        for (var sample = 0; sample <= 3; sample++)
        {
            var u = 0.9 * sample;
            for (var sampleV = 0; sampleV <= 4; sampleV++)
            {
                var v = Math.Tau * sampleV / 4 - 2.2;
                Assert.Equal(0, KernelRuntime.SurfEval(spun, Uv(u, v), 2, 2, 0, output));
                // R = Z + W cos v + (A×W) sin v with W = C(u) − Z, all z kept.
                var (x, y) = (2 * Math.Cos(u) - 0.25, Math.Sin(u) + 0.5);
                var (sin, cos) = Math.SinCos(v);
                Assert.Equal(0.25 + x * cos - y * sin, output[0].coord[0], 11);
                Assert.Equal(-0.5 + x * sin + y * cos, output[0].coord[1], 11);
                Assert.Equal(0, output[0].coord[2], 11);
                // Su has no z component: the profile plane is z = 0.
                Assert.Equal(0, output[1].coord[2], 11);
                // Sv = −W sin v + (A×W) cos v; its z component vanishes too.
                Assert.Equal(0, output[3].coord[2], 11);
                // Su agrees with the central difference of positions.
                var forward = SpunPoint(u + 1e-6, v);
                var backward = SpunPoint(u - 1e-6, v);
                Assert.InRange(Math.Abs(output[1].coord[0] - (forward.X - backward.X) / 2e-6), 0, 1e-5);
            }
        }

        static KernelVector3 SpunPoint(double u, double v)
        {
            var (x, y) = (2 * Math.Cos(u) - 0.25, Math.Sin(u) + 0.5);
            var (sin, cos) = Math.SinCos(v);
            return new KernelVector3
            {
                X = 0.25 + x * cos - y * sin,
                Y = -0.5 + x * sin + y * cos,
                Z = 0,
            };
        }
    }

    [Fact]
    public void OffsetSurface_SphereBaseClosedForm_NestingAndFiniteDifference()
    {
        // The offset of a sphere is a concentric sphere with radius r + d.
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidSphere(3, null, &body));
        var sphere = FirstSurface(body);
        int offset;
        var offsetSf = new PK_OFFSET_sf_s { underlying_surface = sphere, offset_distance = 0.5 };
        Assert.Equal(0, KernelRuntime.OffsetCreate(&offsetSf, &offset));
        var output = stackalloc PK_VECTOR_s[121];
        Assert.Equal(0, KernelRuntime.SurfEval(offset, Uv(0.3, 0.4), 2, 2, 0, output));
        Assert.Equal(3.5 * Math.Cos(0.4) * Math.Cos(0.3), output[0].coord[0], 11);
        Assert.Equal(3.5 * Math.Cos(0.4) * Math.Sin(0.3), output[0].coord[1], 11);
        Assert.Equal(3.5 * Math.Sin(0.4), output[0].coord[2], 11);

        // Offset of an offset doubles down on the distance.
        int nestedOffset;
        var nestedSf = new PK_OFFSET_sf_s { underlying_surface = offset, offset_distance = 0.25 };
        Assert.Equal(0, KernelRuntime.OffsetCreate(&nestedSf, &nestedOffset));
        var nested = stackalloc PK_VECTOR_s[1];
        Assert.Equal(0, KernelRuntime.SurfEval(nestedOffset, Uv(0.3, 0.4), 0, 0, 0, nested));
        Assert.Equal(3.75 * Math.Cos(0.4) * Math.Cos(0.3), nested[0].coord[0], 10);
        Assert.Equal(3.75 * Math.Sin(0.4), nested[0].coord[2], 10);

        // Offset of a Bézier patch: first derivatives match central differences.
        var poles = new double[4 * 4 * 3];
        for (var i = 0; i < 4; i++)
        for (var j = 0; j < 4; j++)
        {
            poles[(i * 4 + j) * 3] = i;
            poles[(i * 4 + j) * 3 + 1] = j;
            poles[(i * 4 + j) * 3 + 2] = 0.4 * i * j + 0.1 * i * i;
        }
        var bsurf = CreateBSurface(3, 3, 4, 4, poles, [0, 1], [4, 4], [0, 1], [4, 4]);
        int bsurfOffset;
        var bsurfOffsetSf = new PK_OFFSET_sf_s { underlying_surface = bsurf, offset_distance = -0.3 };
        Assert.Equal(0, KernelRuntime.OffsetCreate(&bsurfOffsetSf, &bsurfOffset));
        var (u0, v0) = (0.4, 0.6);
        Assert.Equal(0, KernelRuntime.SurfEval(bsurfOffset, Uv(u0, v0), 1, 1, 0, output));
        var plus = stackalloc PK_VECTOR_s[1];
        var minus = stackalloc PK_VECTOR_s[1];
        Assert.Equal(0, KernelRuntime.SurfEval(bsurfOffset, Uv(u0 + 1e-6, v0), 0, 0, 0, plus));
        Assert.Equal(0, KernelRuntime.SurfEval(bsurfOffset, Uv(u0 - 1e-6, v0), 0, 0, 0, minus));
        for (var axis = 0; axis < 3; axis++)
            Assert.InRange(Math.Abs(output[1].coord[axis] - (plus[0].coord[axis] - minus[0].coord[axis]) / 2e-6), 0, 1e-5);
    }

    [Fact]
    public void DependentGeometry_EvaluatesAfterCreation_AndDeleteReleases()
    {
        var poles = new double[2 * 2 * 3] { 0, 0, 0, 1, 0, 0.2, 0, 1, 0, 1, 1, 0.4 };
        var bsurf = CreateBSurface(1, 1, 2, 2, poles, [0, 1], [2, 2], [0, 1], [2, 2]);
        var output = stackalloc PK_VECTOR_s[1];
        Assert.Equal(0, KernelRuntime.SurfEval(bsurf, Uv(0.5, 0.5), 0, 0, 0, output));
        Assert.Equal(0.5, output[0].coord[0], 13);
        Assert.Equal(0.5, output[0].coord[1], 13);
        Assert.Equal(0.15, output[0].coord[2], 13);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &bsurf));
        Assert.Equal(ParasolidConstants.PK_ERROR_not_a_tag, KernelRuntime.SurfEval(bsurf, Uv(0.5, 0.5), 0, 0, 0, output));
    }

    // ── Fixtures ─────────────────────────────────────────────────────

    private static int FirstSurface(int body)
    {
        int count;
        int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &faces));
        int surface;
        Assert.Equal(0, KernelRuntime.FaceAskSurf(faces[0], &surface));
        KernelRuntime.MemoryFree(faces);
        return surface;
    }

    private static int CreateEllipse(double r1, double r2)
    {
        Assert.Equal(0, CreateRawEllipse(r1, r2, out var tag));
        return tag;
    }

    private static int CreateRawEllipse(double r1, double r2, out int tag)
    {
        var sf = new PK_ELLIPSE_sf_s { R1 = r1, R2 = r2 };
        sf.basis_set.axis.coord[2] = 1;
        sf.basis_set.ref_direction.coord[0] = 1;
        fixed (int* t = &tag)
        {
            return KernelRuntime.EllipseCreate(&sf, t);
        }
    }

    private static int Create2DBCurve(int degree, double[] poles, double[] knots, int[] mult, bool rational)
    {
        fixed (double* p = poles)
        fixed (double* k = knots)
        fixed (int* m = mult)
        {
            var dimension = rational ? 3 : 2;
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
                form = ParasolidConstants.PK_BCURVE_form_unset_c,
                knot_type = ParasolidConstants.PK_knot_unset_c,
                self_intersecting = ParasolidConstants.PK_self_intersect_unset_c
            };
            int curve;
            Assert.Equal(0, KernelRuntime.BCurveCreate(&sf, &curve));
            return curve;
        }
    }

    private static int CreateBSurface(int uDegree, int vDegree, int nU, int nV, double[] poles,
        double[] uKnots, int[] uMult, double[] vKnots, int[] vMult, bool isUPeriodic = false)
    {
        fixed (double* p = poles)
        fixed (double* uk = uKnots)
        fixed (int* um = uMult)
        fixed (double* vk = vKnots)
        fixed (int* vm = vMult)
        {
            var sf = new PK_BSURF_sf_s
            {
                u_degree = uDegree,
                v_degree = vDegree,
                n_u_vertices = nU,
                n_v_vertices = nV,
                vertex_dim = 3,
                is_rational = 0,
                vertex = p,
                n_u_knots = uKnots.Length,
                n_v_knots = vKnots.Length,
                u_knot = uk,
                v_knot = vk,
                u_knot_mult = um,
                v_knot_mult = vm,
                is_u_periodic = (byte)(isUPeriodic ? 1 : 0),
                form = ParasolidConstants.PK_BSURF_form_unset_c,
                u_knot_type = ParasolidConstants.PK_knot_unset_c,
                v_knot_type = ParasolidConstants.PK_knot_unset_c,
                self_intersecting = ParasolidConstants.PK_self_intersect_unset_c,
                convexity = ParasolidConstants.PK_convexity_unset_c
            };
            int surface;
            Assert.Equal(0, KernelRuntime.BSurfCreate(&sf, &surface));
            return surface;
        }
    }

    /// <summary>
    /// Reference B-spline surface derivative: repeated pole reduction
    /// (Q_i = p·(P_{i+1} − P_i)/(k[i+p+1] − k[i+1]), knots trimmed at both
    /// ends), then plain Cox-de Boor evaluation of the reduced surface.
    /// Poles are u-major.
    /// </summary>
    private static KernelVector3 SplineSurfaceDerivative(double[] poles, int nU, int nV, double[] uKnots, double[] vKnots,
        int uDegree, int vDegree, double u, double v, int orderU, int orderV)
    {
        if (orderU > uDegree || orderV > vDegree) return default;
        var current = new KernelVector3[nU * nV];
        for (var k = 0; k < nU * nV; k++)
            current[k] = new KernelVector3 { X = poles[k * 3], Y = poles[k * 3 + 1], Z = poles[k * 3 + 2] };
        var cu = nU;
        var cv = nV;
        var ku = (double[])uKnots.Clone();
        var kv = (double[])vKnots.Clone();
        for (var a = 0; a < orderU; a++)
        {
            var degree = uDegree - a;
            var reduced = new KernelVector3[(cu - 1) * cv];
            for (var i = 0; i + 1 < cu; i++)
            for (var j = 0; j < cv; j++)
            {
                var factor = (double)degree / (ku[i + degree + 1] - ku[i + 1]);
                var upper = current[(i + 1) * cv + j];
                var lower = current[i * cv + j];
                reduced[i * cv + j] = new KernelVector3
                {
                    X = factor * (upper.X - lower.X),
                    Y = factor * (upper.Y - lower.Y),
                    Z = factor * (upper.Z - lower.Z),
                };
            }
            current = reduced;
            cu--;
            ku = ku[1..^1];
        }
        for (var b = 0; b < orderV; b++)
        {
            var degree = vDegree - b;
            var reduced = new KernelVector3[cu * (cv - 1)];
            for (var i = 0; i < cu; i++)
            for (var j = 0; j + 1 < cv; j++)
            {
                var factor = (double)degree / (kv[j + degree + 1] - kv[j + 1]);
                var upper = current[i * cv + j + 1];
                var lower = current[i * cv + j];
                reduced[i * (cv - 1) + j] = new KernelVector3
                {
                    X = factor * (upper.X - lower.X),
                    Y = factor * (upper.Y - lower.Y),
                    Z = factor * (upper.Z - lower.Z),
                };
            }
            current = reduced;
            cv--;
            kv = kv[1..^1];
        }
        var value = new KernelVector3();
        for (var i = 0; i < cu; i++)
        for (var j = 0; j < cv; j++)
        {
            var factor = Basis(ku, i, uDegree - orderU, u) * Basis(kv, j, vDegree - orderV, v);
            value = new KernelVector3
            {
                X = value.X + factor * current[i * cv + j].X,
                Y = value.Y + factor * current[i * cv + j].Y,
                Z = value.Z + factor * current[i * cv + j].Z,
            };
        }
        return value;
    }

    private static double Basis(double[] knots, int i, int degree, double u)
    {
        if (degree == 0)
        {
            if (u >= knots[i] && u < knots[i + 1]) return 1;
            return u == knots[i + 1] && knots[i] < knots[i + 1] && knots[i + 1] == knots[^1] ? 1 : 0;
        }
        if (knots[i + degree] == knots[i] && knots[i + degree + 1] == knots[i + 1]) return 0;
        var left = knots[i + degree] > knots[i] ? (u - knots[i]) / (knots[i + degree] - knots[i]) * Basis(knots, i, degree - 1, u) : 0;
        var right = knots[i + degree + 1] > knots[i + 1] ? (knots[i + degree + 1] - u) / (knots[i + degree + 1] - knots[i + 1]) * Basis(knots, i + 1, degree - 1, u) : 0;
        return left + right;
    }

    private static double Length(PK_VECTOR_s v)
        => Math.Sqrt(v.coord[0] * v.coord[0] + v.coord[1] * v.coord[1] + v.coord[2] * v.coord[2]);

    private static PK_UV_s Uv(double u, double v)
    {
        PK_UV_s result = default;
        result.param[0] = u;
        result.param[1] = v;
        return result;
    }
}

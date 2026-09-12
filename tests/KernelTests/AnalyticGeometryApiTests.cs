using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using Xunit;

namespace KernelTests;

/// <summary>
/// Public create/ask contracts for standalone analytic geometry. Error codes
/// mirror probed Parasolid V38 behaviour (scripts/ProbeXtGeometryNodes.cs).
/// </summary>
[Collection("KernelTests")]
public unsafe class AnalyticGeometryApiTests : IDisposable
{
    public AnalyticGeometryApiTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Fact]
    public void LineCreateAsk_RoundTripsStandardForm()
    {
        var sf = new PK_LINE_sf_s();
        sf.basis_set.location.coord[0] = 1;
        sf.basis_set.location.coord[1] = -2;
        sf.basis_set.location.coord[2] = 3;
        sf.basis_set.axis.coord[0] = 0;
        sf.basis_set.axis.coord[1] = 1;
        sf.basis_set.axis.coord[2] = 0;

        int line;
        Assert.Equal(0, KernelRuntime.LineCreate(&sf, &line));

        var asked = new PK_LINE_sf_s();
        Assert.Equal(0, KernelRuntime.LineAsk(line, &asked));
        Assert.Equal(1, asked.basis_set.location.coord[0]);
        Assert.Equal(-2, asked.basis_set.location.coord[1]);
        Assert.Equal(3, asked.basis_set.location.coord[2]);
        Assert.Equal(0, asked.basis_set.axis.coord[0]);
        Assert.Equal(1, asked.basis_set.axis.coord[1]);
        Assert.Equal(0, asked.basis_set.axis.coord[2]);
    }

    [Fact]
    public void CircleCreateAsk_RoundTripsStandardForm()
    {
        var sf = CircleSf(4);
        int circle;
        Assert.Equal(0, KernelRuntime.CircleCreate(&sf, &circle));
        Assert.Equal(0, AskRadius(circle, out var radius));
        Assert.Equal(4, radius, 14);
        var asked = new PK_CIRCLE_sf_s();
        Assert.Equal(0, KernelRuntime.CircleAsk(circle, &asked));
        AssertStandardForm(asked.basis_set, 1, 2, 3, 0, 0, 1, 1, 0, 0);
        Assert.Equal(4, asked.radius, 14);
    }

    [Fact]
    public void PlaneCreateAsk_RoundTripsStandardForm()
    {
        var sf = PlaneSf(0.5, -0.5, 7, 0, 1, 0, 0, 0, 1);
        int plane;
        Assert.Equal(0, KernelRuntime.PlaneCreate(&sf, &plane));
        var asked = new PK_PLANE_sf_s();
        Assert.Equal(0, KernelRuntime.PlaneAsk(plane, &asked));
        AssertStandardForm(asked.basis_set, 0.5, -0.5, 7, 0, 1, 0, 0, 0, 1);
    }

    [Fact]
    public void ConeCreateAsk_RoundTripsStandardForm()
    {
        var sf = ConeSf(2, Math.PI / 6);
        int cone;
        Assert.Equal(0, KernelRuntime.ConeCreate(&sf, &cone));
        var asked = new PK_CONE_sf_s();
        Assert.Equal(0, KernelRuntime.ConeAsk(cone, &asked));
        AssertStandardForm(asked.basis_set, 1, 1, 0, 0, 0, 1, 1, 0, 0);
        Assert.Equal(2, asked.radius, 14);
        Assert.Equal(Math.PI / 6, asked.semi_angle, 14);
    }

    [Fact]
    public void SphereCreateAsk_RoundTripsStandardForm()
    {
        var sf = SphereSf(1.5);
        int sphere;
        Assert.Equal(0, KernelRuntime.SphereCreate(&sf, &sphere));
        var asked = new PK_SPHERE_sf_s();
        Assert.Equal(0, KernelRuntime.SphereAsk(sphere, &asked));
        AssertStandardForm(asked.basis_set, -3, 0, 2, 1, 0, 0, 0, 1, 0);
        Assert.Equal(1.5, asked.radius, 14);
    }

    [Fact]
    public void TorusCreateAsk_RoundTripsStandardForm()
    {
        var sf = TorusSf(5, 2);
        int torus;
        Assert.Equal(0, KernelRuntime.TorusCreate(&sf, &torus));
        var asked = new PK_TORUS_sf_s();
        Assert.Equal(0, KernelRuntime.TorusAsk(torus, &asked));
        AssertStandardForm(asked.basis_set, 0, 0, 0, 0, 0, 1, 1, 0, 0);
        Assert.Equal(5, asked.major_radius, 14);
        Assert.Equal(2, asked.minor_radius, 14);
    }

    [Fact]
    public void TorusCreate_AcceptsAppleLemonShape()
    {
        var sf = TorusSf(1, 2);
        int torus;
        Assert.Equal(0, KernelRuntime.TorusCreate(&sf, &torus));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void CircleCreate_RejectsNonPositiveRadius(double radius)
    {
        int tag;
        Assert.Equal(ParasolidConstants.PK_ERROR_radius_le_0, CreateCircle(radius, &tag));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void SphereCreate_RejectsNonPositiveRadius(double radius)
    {
        var sf = SphereSf(radius);
        int tag;
        Assert.Equal(ParasolidConstants.PK_ERROR_radius_le_0, KernelRuntime.SphereCreate(&sf, &tag));
    }

    [Fact]
    public void ConeCreate_RejectsSemiAngleOutsideOpenInterval()
    {
        var sf = ConeSf(1, 0);
        int tag;
        sf.semi_angle = 0;
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_angle, KernelRuntime.ConeCreate(&sf, &tag));
        sf.semi_angle = Math.PI;
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_angle, KernelRuntime.ConeCreate(&sf, &tag));
        sf.semi_angle = Math.PI / 2;
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_angle, KernelRuntime.ConeCreate(&sf, &tag));
        sf.semi_angle = -0.25;
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_angle, KernelRuntime.ConeCreate(&sf, &tag));
    }

    [Fact]
    public void ConeCreate_RejectsNegativeRadiusButAcceptsZero()
    {
        var sf = ConeSf(0, Math.PI / 4);
        int tag;
        sf.radius = -1;
        Assert.Equal(ParasolidConstants.PK_ERROR_radius_lt_0, KernelRuntime.ConeCreate(&sf, &tag));
        sf.radius = 0;
        Assert.Equal(0, KernelRuntime.ConeCreate(&sf, &tag));
    }

    [Fact]
    public void AnalyticCreate_RejectsNonUnitAxis()
    {
        int tag;
        var line = new PK_LINE_sf_s();
        line.basis_set.axis.coord[0] = 2;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_a_unit_vector, KernelRuntime.LineCreate(&line, &tag));

        var circle = CircleSf(1);
        circle.basis_set.axis.coord[1] = 3;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_a_unit_vector, KernelRuntime.CircleCreate(&circle, &tag));

        var plane = PlaneSf(0, 0, 0, 0, 0, 2, 1, 0, 0);
        Assert.Equal(ParasolidConstants.PK_ERROR_not_a_unit_vector, KernelRuntime.PlaneCreate(&plane, &tag));

        var torus = TorusSf(3, 1);
        torus.basis_set.axis.coord[2] = 2;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_a_unit_vector, KernelRuntime.TorusCreate(&torus, &tag));
    }

    [Fact]
    public void PlaneCreate_RejectsRefDirectionNotOrthogonalToAxis()
    {
        var orthogonalUnit = Math.Sqrt(0.5);
        var sf = PlaneSf(0, 0, 0, 0, 0, 1, orthogonalUnit, 0, orthogonalUnit);
        int tag;
        Assert.Equal(ParasolidConstants.PK_ERROR_vectors_not_orthogonal, KernelRuntime.PlaneCreate(&sf, &tag));
    }

    [Fact]
    public void Ask_ReturnsUnknownClassForWrongGeometry()
    {
        var circleSf = CircleSf(1);
        int circle;
        Assert.Equal(0, KernelRuntime.CircleCreate(&circleSf, &circle));

        var lineSf = new PK_LINE_sf_s();
        var line = new PK_LINE_sf_s();
        Assert.Equal(ParasolidConstants.PK_ERROR_unknown_class, KernelRuntime.LineAsk(circle, &line));

        int cyl;
        var cylSf = new PK_CYL_sf_s();
        FillBasis(ref cylSf.basis_set, 0, 0, 0, 0, 0, 1, 1, 0, 0);
        cylSf.radius = 1;
        Assert.Equal(0, KernelRuntime.CylCreate(&cylSf, &cyl));
        var circleAsk = new PK_CIRCLE_sf_s();
        Assert.Equal(ParasolidConstants.PK_ERROR_unknown_class, KernelRuntime.CircleAsk(cyl, &circleAsk));
    }

    [Fact]
    public void CreateAsk_RejectsNullStandardForm()
    {
        int tag;
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_field_number, KernelRuntime.LineCreate(null, &tag));
        var line = new PK_LINE_sf_s();
        Assert.Equal(ParasolidConstants.PK_ERROR_unknown_class, KernelRuntime.LineAsk(0, &line));
    }

    [Fact]
    public void CreatedGeometry_DeletesAndInvalidates()
    {
        var sf = CircleSf(1);
        int circle;
        Assert.Equal(0, KernelRuntime.CircleCreate(&sf, &circle));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &circle));
        var asked = new PK_CIRCLE_sf_s();
        Assert.Equal(ParasolidConstants.PK_ERROR_unknown_class, KernelRuntime.CircleAsk(circle, &asked));
    }

    private static void FillBasis(ref PK_AXIS2_sf_s basis,
        double lx, double ly, double lz,
        double ax, double ay, double az,
        double rx, double ry, double rz)
    {
        basis.location.coord[0] = lx;
        basis.location.coord[1] = ly;
        basis.location.coord[2] = lz;
        basis.axis.coord[0] = ax;
        basis.axis.coord[1] = ay;
        basis.axis.coord[2] = az;
        basis.ref_direction.coord[0] = rx;
        basis.ref_direction.coord[1] = ry;
        basis.ref_direction.coord[2] = rz;
    }

    private static PK_CIRCLE_sf_s CircleSf(double radius)
    {
        var sf = new PK_CIRCLE_sf_s();
        FillBasis(ref sf.basis_set, 1, 2, 3, 0, 0, 1, 1, 0, 0);
        sf.radius = radius;
        return sf;
    }

    private static PK_PLANE_sf_s PlaneSf(
        double lx, double ly, double lz,
        double ax, double ay, double az,
        double rx, double ry, double rz)
    {
        var sf = new PK_PLANE_sf_s();
        FillBasis(ref sf.basis_set, lx, ly, lz, ax, ay, az, rx, ry, rz);
        return sf;
    }

    private static PK_CONE_sf_s ConeSf(double radius, double semiAngle)
    {
        var sf = new PK_CONE_sf_s();
        FillBasis(ref sf.basis_set, 1, 1, 0, 0, 0, 1, 1, 0, 0);
        sf.radius = radius;
        sf.semi_angle = semiAngle;
        return sf;
    }

    private static PK_SPHERE_sf_s SphereSf(double radius)
    {
        var sf = new PK_SPHERE_sf_s();
        FillBasis(ref sf.basis_set, -3, 0, 2, 1, 0, 0, 0, 1, 0);
        sf.radius = radius;
        return sf;
    }

    private static PK_TORUS_sf_s TorusSf(double majorRadius, double minorRadius)
    {
        var sf = new PK_TORUS_sf_s();
        FillBasis(ref sf.basis_set, 0, 0, 0, 0, 0, 1, 1, 0, 0);
        sf.major_radius = majorRadius;
        sf.minor_radius = minorRadius;
        return sf;
    }

    private static int CreateCircle(double radius, int* tag)
    {
        var sf = CircleSf(radius);
        return KernelRuntime.CircleCreate(&sf, tag);
    }

    private static int AskRadius(int surface, out double radius)
    {
        var asked = new PK_CIRCLE_sf_s();
        var error = KernelRuntime.CircleAsk(surface, &asked);
        radius = asked.radius;
        return error;
    }

    private static void AssertStandardForm(
        PK_AXIS2_sf_s basis,
        double lx, double ly, double lz,
        double ax, double ay, double az,
        double rx, double ry, double rz)
    {
        Assert.Equal(lx, basis.location.coord[0], 14);
        Assert.Equal(ly, basis.location.coord[1], 14);
        Assert.Equal(lz, basis.location.coord[2], 14);
        Assert.Equal(ax, basis.axis.coord[0], 14);
        Assert.Equal(ay, basis.axis.coord[1], 14);
        Assert.Equal(az, basis.axis.coord[2], 14);
        Assert.Equal(rx, basis.ref_direction.coord[0], 14);
        Assert.Equal(ry, basis.ref_direction.coord[1], 14);
        Assert.Equal(rz, basis.ref_direction.coord[2], 14);
    }
}

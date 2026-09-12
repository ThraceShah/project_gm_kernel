#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

// Experiment: which replacements does PK_FACE_replace_surfs accept on a block
// top face (plane, plane with shifted UV, bilinear bsurf with matched UV box)?

using System.Runtime.CompilerServices;
using static parasolid;

unsafe
{
    if (!ParasolidScriptHost.TryStartSession("replace surf probe", out var host, out var message))
        throw new InvalidOperationException(message);
    using (host)
    {
        // (a) identical plane
        {
            PK_BODY_t body;
            Check(PK_BODY_create_solid_block(2, 3, 4, null, &body), "block");
            PK_FACE_t face = TopFace(body);
            PK_PLANE_sf_t sf = new(new PK_AXIS2_sf_t(
                new PK_VECTOR_t(0, 0, 4), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)));
            PK_PLANE_t plane;
            Check(PK_PLANE_create(&sf, &plane), "plane");
            PK_local_check_t check;
            var e = PK_FACE_replace_surfs(1, &face, &plane, 0.0, PK_LOGICAL_false, &check);
            Console.WriteLine($"identical plane, tol 0: {e}");
        }
        // (b) plane with shifted location (same geometry, different pvec)
        {
            PK_BODY_t body;
            Check(PK_BODY_create_solid_block(2, 3, 4, null, &body), "block");
            PK_FACE_t face = TopFace(body);
            PK_PLANE_sf_t sf = new(new PK_AXIS2_sf_t(
                new PK_VECTOR_t(-1, -1.5, 4), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)));
            PK_PLANE_t plane;
            Check(PK_PLANE_create(&sf, &plane), "plane");
            PK_local_check_t check;
            var e = PK_FACE_replace_surfs(1, &face, &plane, 0.0, PK_LOGICAL_false, &check);
            Console.WriteLine($"shifted plane, tol 0: {e}");
        }
        // (c) bilinear bsurf with UV box matching the old plane UV
        {
            PK_BODY_t body;
            Check(PK_BODY_create_solid_block(2, 3, 4, null, &body), "block");
            PK_FACE_t face = TopFace(body);
            PK_BSURF_t bsurf = BSurf([0, 2], [0, 3]);
            PK_local_check_t check;
            var e = PK_FACE_replace_surfs(1, &face, &bsurf, 0.0, PK_LOGICAL_false, &check);
            Console.WriteLine($"bsurf matched box, tol 0: {e}");
        }
        // (d) bsurf [0,1]x[0,1]
        {
            PK_BODY_t body;
            Check(PK_BODY_create_solid_block(2, 3, 4, null, &body), "block");
            PK_FACE_t face = TopFace(body);
            PK_BSURF_t bsurf = BSurf([0, 1], [0, 1]);
            PK_local_check_t check;
            var e = PK_FACE_replace_surfs(1, &face, &bsurf, 0.0, PK_LOGICAL_false, &check);
            Console.WriteLine($"bsurf unit box, tol 0: {e}");
        }
        // (e2) cubic Bézier planar patch, matched box
        {
            PK_BODY_t body;
            Check(PK_BODY_create_solid_block(2, 3, 4, null, &body), "block");
            PK_FACE_t face = TopFace(body);
            PK_BSURF_t bsurf = CubicPlanarBSurf([0, 2], [0, 3]);
            PK_local_check_t check;
            var e = PK_FACE_replace_surfs(1, &face, &bsurf, 0.0, PK_LOGICAL_false, &check);
            Console.WriteLine($"cubic planar bsurf, tol 0: {e}");
        }
        // (f) cylinder replacing a cylinder side face
        {
            PK_BODY_t body;
            Check(PK_BODY_create_solid_cyl(2, 3, null, &body), "cyl");
            int count;
            PK_FACE_t* faces;
            Check(PK_BODY_ask_faces(body, &count, &faces), "faces");
            PK_FACE_t face = 0;
            for (var i = 0; i < count; i++)
            {
                PK_SURF_t surf;
                if (PK_FACE_ask_surf(faces[i], &surf) != 0) continue;
                PK_CLASS_t cls;
                if (PK_ENTITY_ask_class(surf, &cls) != 0) continue;
                if (cls == PK_CLASS_cyl) { face = faces[i]; break; }
            }
            Check(PK_MEMORY_free(faces), "free faces");
            PK_CYL_sf_t sf = new(new PK_AXIS2_sf_t(
                new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)), 2.0);
            PK_CYL_t cyl;
            Check(PK_CYL_create(&sf, &cyl), "cyl create");
            PK_local_check_t check;
            var e = PK_FACE_replace_surfs(1, &face, &cyl, 0.0, PK_LOGICAL_false, &check);
            Console.WriteLine($"identical cylinder: {e}");
        }
        // (e) bsurf matched box, tolerance 1e-4, local check true
        {
            PK_BODY_t body;
            Check(PK_BODY_create_solid_block(2, 3, 4, null, &body), "block");
            PK_FACE_t face = TopFace(body);
            PK_BSURF_t bsurf = BSurf([0, 2], [0, 3]);
            PK_local_check_t check;
            var e = PK_FACE_replace_surfs(1, &face, &bsurf, 1e-4, PK_LOGICAL_true, &check);
            Console.WriteLine($"bsurf matched box, tol 1e-4, local check: {e}");
        }
    }
}

static unsafe PK_FACE_t TopFace(PK_BODY_t body)
{
    int count;
    PK_FACE_t* faces;
    Check(PK_BODY_ask_faces(body, &count, &faces), "faces");
    PK_FACE_t face = 0;
    for (var i = 0; i < count; i++)
    {
        PK_SURF_t surf;
        if (PK_FACE_ask_surf(faces[i], &surf) != 0) continue;
        PK_PLANE_sf_t sf;
        if (PK_PLANE_ask(surf, &sf) != 0) continue;
        if (Math.Abs(sf.basis_set.location.coord[2] - 4) < 1e-9 && Math.Abs(sf.basis_set.axis.coord[2] - 1) < 1e-9)
        {
            face = faces[i];
            break;
        }
    }
    Check(PK_MEMORY_free(faces), "free faces");
    AssertTrue(face != 0, "top face");
    return face;
}

static unsafe PK_BSURF_t CubicPlanarBSurf(double[] uKnots, double[] vKnots)
{
    // 4x4 cubic Bezier poles, a linear (planar) map over the UV box.
    var poles = new double[4 * 4 * 3];
    for (var i = 0; i < 4; i++)
        for (var j = 0; j < 4; j++)
        {
            var x = -1.0 + 2.0 * i / 3.0;
            var y = -1.5 + 3.0 * j / 3.0;
            var k = (i * 4 + j) * 3;
            poles[k] = x; poles[k + 1] = y; poles[k + 2] = 4;
        }
    int[] um = [4, 4];
    int[] vm = [4, 4];
    fixed (double* p = poles)
    fixed (double* uk = uKnots)
    fixed (double* vk = vKnots)
    fixed (int* um2 = um)
    fixed (int* vm2 = vm)
    {
        var sf = new PK_BSURF_sf_t
        {
            u_degree = 3, v_degree = 3, n_u_vertices = 4, n_v_vertices = 4,
            vertex_dim = 3, is_rational = 0, vertex = p,
            n_u_knots = 2, n_v_knots = 2, u_knot = uk, v_knot = vk,
            u_knot_mult = um2, v_knot_mult = vm2,
            form = PK_BSURF_form_unset_c,
            u_knot_type = PK_knot_unset_c,
            v_knot_type = PK_knot_unset_c,
            self_intersecting = PK_self_intersect_unset_c,
            convexity = PK_convexity_unset_c,
        };
        PK_BSURF_t surface;
        Check(PK_BSURF_create(&sf, &surface), "cubic bsurf create");
        return surface;
    }
}

static unsafe PK_BSURF_t BSurf(double[] uKnots, double[] vKnots)
{
    double[] poles = new double[12];
    (double X, double Y)[] corners = { (-1, -1.5), (1, -1.5), (-1, 1.5), (1, 1.5) };
    for (var i = 0; i < 4; i++)
    {
        poles[3 * i] = corners[i].X;
        poles[3 * i + 1] = corners[i].Y;
        poles[3 * i + 2] = 4;
    }
    int[] mults = [2, 2];
    fixed (double* p = poles)
    fixed (double* uk = uKnots)
    fixed (double* vk = vKnots)
    fixed (int* um = mults)
    fixed (int* vm = mults)
    {
        var sf = new PK_BSURF_sf_t
        {
            u_degree = 1, v_degree = 1, n_u_vertices = 2, n_v_vertices = 2,
            vertex_dim = 3, is_rational = 0, vertex = p,
            n_u_knots = 2, n_v_knots = 2, u_knot = uk, v_knot = vk,
            u_knot_mult = um, v_knot_mult = vm,
            form = PK_BSURF_form_unset_c,
            u_knot_type = PK_knot_unset_c,
            v_knot_type = PK_knot_unset_c,
            self_intersecting = PK_self_intersect_unset_c,
            convexity = PK_convexity_unset_c,
        };
        PK_BSURF_t surface;
        Check(PK_BSURF_create(&sf, &surface), "bsurf create");
        return surface;
    }
}

static void AssertTrue(bool c, string l) { if (!c) throw new InvalidOperationException(l); }
static void Check(int e, string l) { if (e != 0) throw new InvalidOperationException($"{l}: error={e}"); }

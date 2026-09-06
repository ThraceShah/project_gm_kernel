#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Runtime;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

static string ScriptPath([CallerFilePath] string path = "") => path;
var directory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "evaluation-oracle"));
Directory.CreateDirectory(directory);
var roundtripFailures = new List<string>();
var numericalOnly = args.Contains("--numerical-only");
var memoryReview = args.Contains("--memory-review");
if (numericalOnly) Console.WriteLine("Numerical-only run: XT receive/compare is not checked.");

unsafe
{
    if (!ParasolidScriptHost.TryStartSession("evaluation oracle", out var session, out var message,
        configureRollback: memoryReview ? MarkOracleStorage.Register : null))
        throw new InvalidOperationException(message);
    using (session)
    {
        var start = new M.PK_SESSION_start_o_s { o_t_version = 1 };
        Check(KernelRuntime.SessionStart(&start), "our session");
        try
        {
            if (memoryReview)
            {
                CheckPartitionLockProtocol();
            }
            foreach (var kind in new[] { "block", "cylinder", "cone", "sphere", "torus" })
            foreach (var rotated in new[] { false, true })
            {
                var basis = Frame(2, -3, 7, 1.0 / 3, 2.0 / 3, 2.0 / 3, 0, 1 / Math.Sqrt(2), -1 / Math.Sqrt(2));
                var managedBasis = new M.PK_AXIS2_sf_s();
                for (var i = 0; i < 3; i++)
                {
                    managedBasis.location.coord[i] = basis.location.coord[i];
                    managedBasis.axis.coord[i] = basis.axis.coord[i];
                    managedBasis.ref_direction.coord[i] = basis.ref_direction.coord[i];
                }
                var bp = rotated ? &basis : null;
                var mp = rotated ? &managedBasis : null;
                var label = rotated ? kind + "-rotated" : kind;
                int ours, reference;
                switch (kind)
                {
                    case "block":
                        Check(KernelRuntime.BodyCreateSolidBlock(2, 3, 4, mp, &ours), kind);
                        Check(PK_BODY_create_solid_block(2, 3, 4, bp, &reference), kind);
                        break;
                    case "cylinder":
                        Check(KernelRuntime.BodyCreateSolidCyl(3, 5, mp, &ours), kind);
                        Check(PK_BODY_create_solid_cyl(3, 5, bp, &reference), kind);
                        break;
                    case "cone":
                        Check(KernelRuntime.BodyCreateSolidCone(2, 5, 0.25, mp, &ours), kind);
                        Check(PK_BODY_create_solid_cone(2, 5, 0.25, bp, &reference), kind);
                        break;
                    case "sphere":
                        Check(KernelRuntime.BodyCreateSolidSphere(3, mp, &ours), kind);
                        Check(PK_BODY_create_solid_sphere(3, bp, &reference), kind);
                        break;
                    default:
                        Check(KernelRuntime.BodyCreateSolidTorus(5, 2, mp, &ours), kind);
                        Check(PK_BODY_create_solid_torus(5, 2, bp, &reference), kind);
                        break;
                }
                if (memoryReview)
                {
                    // Delete/restore a live body, then reuse slots from another
                    // body. Both kernels perform the same lifecycle operations.
                    int ourMark, referenceMark;
                    Check(KernelRuntime.MarkCreate(&ourMark), "our mark");
                    Check(PK_MARK_create(&referenceMark), "reference mark");
                    Check(KernelRuntime.EntityDelete(1, &ours), "our delete under mark");
                    Check(PK_ENTITY_delete(1, &reference), "reference delete under mark");
                    Check(KernelRuntime.MarkGoto(ourMark), "our restore");
                    Check(PK_MARK_goto(referenceMark), "reference restore");
                    int ourTemporary, referenceTemporary;
                    Check(KernelRuntime.BodyCreateSolidSphere(1, null, &ourTemporary), "our temporary body");
                    Check(PK_BODY_create_solid_sphere(1, null, &referenceTemporary), "reference temporary body");
                    Check(KernelRuntime.EntityDelete(1, &ourTemporary), "our temporary delete");
                    Check(PK_ENTITY_delete(1, &referenceTemporary), "reference temporary delete");
                }
                else
                {
                    CheckBodyEvaluations(ours, label);
                    Console.WriteLine(label + ": numerical evaluation passed");
                }
                if (numericalOnly) continue;
                try
                {
                    CheckRoundtrip(ours, reference, Path.Combine(directory, label + ".x_t"));
                    Console.WriteLine(label + ": XT body comparison passed");
                    if (memoryReview)
                    {
                        Check(KernelRuntime.EntityDelete(1, &ours), "our final delete");
                        Check(PK_ENTITY_delete(1, &reference), "reference final delete");
                    }
                }
                catch (InvalidOperationException failure)
                {
                    roundtripFailures.Add(label + ": " + failure.Message);
                    Console.WriteLine(roundtripFailures[^1]);
                }
            }
        }
        finally { Check(KernelRuntime.SessionStop(), "our stop"); }
    }
}

if (roundtripFailures.Count != 0)
    throw new InvalidOperationException(string.Join(Environment.NewLine, roundtripFailures));
foreach (var (kind, stats) in ComparisonStats.ByKind)
{
    var tangent = kind.StartsWith("Curve.", StringComparison.Ordinal) ? $" max_tangent={stats.Tangent:R}" : "";
    Console.WriteLine($"{kind}: grid_samples={stats.GridSamples} scalar_comparisons={stats.Comparisons} max_point={stats.Point:R}{tangent} max_derivative={stats.Derivative:R} (absolute tolerance=1e-13)");
}

static void Check(int error, string operation)
{
    if (error != 0) throw new InvalidOperationException($"{operation}: error={error}");
}

static void Equal(int expected, int actual, string label)
{
    if (expected != actual) throw new InvalidOperationException($"{label}: expected={expected}, actual={actual}");
}

static unsafe void CheckPartitionLockProtocol()
{
    int ours, reference;
    Check(KernelRuntime.PartitionCreateEmpty(&ours), "our partition");
    Check(PK_PARTITION_create_empty(&reference), "reference partition");
    int ourMark, referenceMark;
    Check(KernelRuntime.MarkCreate(&ourMark), "our partition checkpoint");
    Check(PK_MARK_create(&referenceMark), "reference partition checkpoint");
    var ourOptions = new M.PK_THREAD_lock_partitions_o_s
    { o_t_version = 1, want_locked_partitions = 1, want_unavailable_partitions = 1 };
    var options = new PK_THREAD_lock_partitions_o_t
    { want_locked_partitions = 1, want_unavailable_partitions = 1 };
    M.PK_THREAD_lock_partitions_r_s ourResult;
    PK_THREAD_lock_partitions_r_t result;
    var expectedError = PK_THREAD_lock_partitions(1, &reference, PK_THREAD_lock_all_c, PK_THREAD_wait_no_c, &options, &result);
    var actualError = KernelRuntime.ThreadLockPartitions(1, &ours, PK_THREAD_lock_all_c, PK_THREAD_wait_no_c, &ourOptions, &ourResult);
    Check(expectedError, "reference partition lock");
    Equal(expectedError, actualError, "partition lock error");
    Equal(result.status, ourResult.status, "partition lock status");
    Equal(result.n_locked_partitions, ourResult.n_locked_partitions, "partition lock count");
    Equal(reference, result.locked_partitions[0], "reference returned partition");
    Equal(ours, ourResult.locked_partitions[0], "our returned partition");
    Check(PK_THREAD_lock_partitions_r_f(&result), "reference lock result free");
    Check(KernelRuntime.ThreadLockPartitionsResultFree(&ourResult), "our lock result free");
    var unlock = new PK_THREAD_unlock_partitions_o_t();
    var ourUnlock = new M.PK_THREAD_unlock_partitions_o_s { o_t_version = 1 };
    int referenceCount, ourCount;
    int* referencePartitions;
    int* ourPartitions;
    Check(PK_THREAD_unlock_partitions(&unlock, &referenceCount, &referencePartitions), "reference unlock");
    Check(KernelRuntime.ThreadUnlockPartitions(&ourUnlock, &ourCount, &ourPartitions), "our unlock");
    Equal(referenceCount, ourCount, "unlock count");
    Check(PK_MEMORY_free(referencePartitions), "reference unlock free");
    Check(KernelRuntime.MemoryFree(ourPartitions), "our unlock free");
    Check(KernelRuntime.MarkGoto(ourMark), "our partition checkpoint restore");
    Check(PK_MARK_goto(referenceMark), "reference partition checkpoint restore");
    Console.WriteLine("PK partition lock/unlock: oracle comparison passed");
}

static unsafe void Compare(M.PK_VECTOR_s* ours, PK_VECTOR_t* reference, int count, string label,
    ComparisonStats stats, bool tangent = false)
{
    for (var i = 0; i < count; i++)
    for (var axis = 0; axis < 3; axis++)
    {
        var a = ours[i].coord[axis];
        var b = reference[i].coord[axis];
        var error = Math.Abs(a - b);
        stats.Comparisons++;
        if (tangent) stats.Tangent = Math.Max(stats.Tangent, error);
        else if (i == 0) stats.Point = Math.Max(stats.Point, error);
        else stats.Derivative = Math.Max(stats.Derivative, error);
        if (!double.IsFinite(a) || !double.IsFinite(b) || error > 1e-13)
            throw new InvalidOperationException($"{label}: derivative={i}, axis={axis}, ours={a:R}, oracle={b:R}");
    }
}

static unsafe void CheckBodyEvaluations(int body, string label)
{
    int count;
    int* entities;
    Check(KernelRuntime.BodyAskEdges(body, &count, &entities), "edges");
    var ours = stackalloc M.PK_VECTOR_s[122];
    var expected = stackalloc PK_VECTOR_t[122];
    for (var i = 0; i < count; i++)
    {
        int curve;
        Check(KernelRuntime.EdgeAskCurve(entities[i], &curve), "curve");
        var reference = MakeCurve(curve);
        var kind = KernelRuntime.GetCurveByTag(curve).Class;
        var stats = ComparisonStats.For("Curve." + kind);
        for (var sample = 0; sample <= 256; sample++)
        {
            var t = kind == CurveClass.Line ? -10.0 + 20.0 * sample / 256 : -2 * Math.Tau + 4 * Math.Tau * sample / 256;
            stats.GridSamples++;
            for (var order = 0; order <= 10; order++)
            {
                M.PK_VECTOR_s tangent;
                PK_VECTOR_t expectedTangent;
                Check(KernelRuntime.CurveEval(curve, t, order, ours), "curve eval");
                Check(PK_CURVE_eval(reference, t, order, expected), "oracle curve eval");
                Compare(ours, expected, order + 1, $"{label} curve={curve} t={t} order={order}", stats);
                Check(KernelRuntime.CurveEvalWithTangent(curve, t, order, ours, &tangent), "tangent eval");
                Check(PK_CURVE_eval_with_tangent(reference, t, order, expected, &expectedTangent), "oracle tangent");
                Compare(ours, expected, order + 1, $"{label} curve={curve} t={t} tangent derivatives order={order}", stats);
                Compare(&tangent, &expectedTangent, 1, $"{label} curve={curve} t={t} unit tangent", stats, tangent: true);
            }
        }
        Equal(PK_CURVE_eval(reference, 0, 11, expected), KernelRuntime.CurveEval(curve, 0, 11, ours), "curve max order");
        Equal(PK_CURVE_eval(0, 0, 0, expected), KernelRuntime.CurveEval(0, 0, 0, ours), "invalid curve tag");
        Check(PK_ENTITY_delete(1, &reference), "delete reference curve");
    }
    Check(KernelRuntime.BodyAskFaces(body, &count, &entities), "faces");
    for (var i = 0; i < count; i++)
    {
        int surface;
        Check(KernelRuntime.FaceAskSurf(entities[i], &surface), "surface");
        var reference = MakeSurface(surface);
        var record = KernelRuntime.GetSurfaceByTag(surface);
        var stats = ComparisonStats.For("Surface." + record.Class);
        Equal(PK_CURVE_eval(reference, 0, 0, expected), KernelRuntime.CurveEval(surface, 0, 0, ours), "wrong curve class");
        foreach (var u in new[] { -2.0, 0.0, 0.25, Math.Tau, 19.7 })
        foreach (var v in new[] { -100.0, -Math.PI / 2, 0.0, 0.4, 1.0, Math.PI / 2, 2.0 })
        foreach (var (du, dv) in new[] { (0, 0), (1, 1), (2, 2), (2, 1), (1, 2), (5, 5), (10, 10), (11, 0), (-1, -1) })
        foreach (byte triangular in new byte[] { 0, 1 })
        {
            var uv = new PK_UV_t();
            uv.param[0] = u; uv.param[1] = v;
            var muv = new M.PK_UV_s();
            muv.param[0] = u; muv.param[1] = v;
            var expectedError = PK_SURF_eval(reference, uv, du, dv, triangular, expected);
            var error = KernelRuntime.SurfEval(surface, muv, du, dv, triangular, ours);
            var context = $"{label} surface={surface} uv={u},{v} orders={du},{dv} triangular={triangular}";
            Equal(expectedError, error, context);
            if (error != 0) continue;
            var nu = Math.Max(0, du); var nv = Math.Max(0, dv);
            var length = triangular != 0 ? (nu + 1) * (nu + 2) / 2 : (nu + 1) * (nv + 1);
            Compare(ours, expected, length, context, stats);
        }
        var minU = record.Class == SurfaceClass.Plane ? -5 : -Math.Tau;
        var maxU = record.Class == SurfaceClass.Plane ? 5 : Math.Tau;
        var minV = -5.0;
        var maxV = 5.0;
        if (record.Class == SurfaceClass.Sphere) { minV = -Math.PI / 2; maxV = Math.PI / 2; }
        else if (record.Class == SurfaceClass.Torus) { minV = -Math.PI; maxV = Math.PI; }
        else if (record.Class == SurfaceClass.Cone)
        {
            var cone = KernelRuntime.GetConeData(record.DataIndex);
            minV = -cone.Radius / Math.Tan(cone.SemiAngle);
        }
        for (var iu = 0; iu <= 64; iu++)
        for (var iv = 0; iv <= 64; iv++)
        {
            var uv = new PK_UV_t();
            uv.param[0] = minU + (maxU - minU) * iu / 64;
            uv.param[1] = minV + (maxV - minV) * iv / 64;
            var muv = new M.PK_UV_s();
            muv.param[0] = uv.param[0]; muv.param[1] = uv.param[1];
            stats.GridSamples++;
            for (byte triangular = 0; triangular <= 1; triangular++)
            {
                var context = $"{label} {record.Class} surface={surface} grid={iu},{iv} uv={uv.param[0]:R},{uv.param[1]:R} triangular={triangular}";
                Check(PK_SURF_eval(reference, uv, 10, 10, triangular, expected), "oracle " + context);
                Check(KernelRuntime.SurfEval(surface, muv, 10, 10, triangular, ours), context);
                Compare(ours, expected, triangular == 0 ? 121 : 66, context, stats);
            }
        }
        Check(PK_ENTITY_delete(1, &reference), "delete reference surface");
    }
}

static unsafe PK_AXIS2_sf_t Frame(double ox, double oy, double oz, double ax, double ay, double az, double rx, double ry, double rz)
{
    var frame = new PK_AXIS2_sf_t();
    frame.location.coord[0] = ox; frame.location.coord[1] = oy; frame.location.coord[2] = oz;
    frame.axis.coord[0] = ax; frame.axis.coord[1] = ay; frame.axis.coord[2] = az;
    frame.ref_direction.coord[0] = rx; frame.ref_direction.coord[1] = ry; frame.ref_direction.coord[2] = rz;
    return frame;
}

static unsafe int MakeCurve(int curve)
{
    var record = KernelRuntime.GetCurveByTag(curve);
    int reference;
    if (record.Class == CurveClass.Line)
    {
        var d = KernelRuntime.GetLineData(record.DataIndex);
        var sf = new PK_LINE_sf_t();
        sf.basis_set.location.coord[0] = d.LocationX; sf.basis_set.location.coord[1] = d.LocationY; sf.basis_set.location.coord[2] = d.LocationZ;
        sf.basis_set.axis.coord[0] = d.AxisX; sf.basis_set.axis.coord[1] = d.AxisY; sf.basis_set.axis.coord[2] = d.AxisZ;
        Check(PK_LINE_create(&sf, &reference), "oracle line");
    }
    else
    {
        var d = KernelRuntime.GetCircleData(record.DataIndex);
        var sf = new PK_CIRCLE_sf_t { radius = d.Radius,
            basis_set = Frame(d.CenterX, d.CenterY, d.CenterZ, d.AxisX, d.AxisY, d.AxisZ, d.RefDirX, d.RefDirY, d.RefDirZ) };
        Check(PK_CIRCLE_create(&sf, &reference), "oracle circle");
    }
    return reference;
}

static unsafe int MakeSurface(int surface)
{
    var record = KernelRuntime.GetSurfaceByTag(surface);
    int reference;
    switch (record.Class)
    {
        case SurfaceClass.Plane:
            var p = KernelRuntime.GetPlaneData(record.DataIndex);
            var plane = new PK_PLANE_sf_t { basis_set = Frame(p.LocationX, p.LocationY, p.LocationZ, p.NormalX, p.NormalY, p.NormalZ, p.RefDirX, p.RefDirY, p.RefDirZ) };
            Check(PK_PLANE_create(&plane, &reference), "oracle plane"); break;
        case SurfaceClass.Cylinder:
            var c = KernelRuntime.GetCylinderData(record.DataIndex);
            var cyl = new PK_CYL_sf_t { radius = c.Radius, basis_set = Frame(c.LocationX, c.LocationY, c.LocationZ, c.AxisX, c.AxisY, c.AxisZ, c.RefDirX, c.RefDirY, c.RefDirZ) };
            Check(PK_CYL_create(&cyl, &reference), "oracle cylinder"); break;
        case SurfaceClass.Cone:
            var d = KernelRuntime.GetConeData(record.DataIndex);
            var cone = new PK_CONE_sf_t { radius = d.Radius, semi_angle = d.SemiAngle, basis_set = Frame(d.LocationX, d.LocationY, d.LocationZ, d.AxisX, d.AxisY, d.AxisZ, d.RefDirX, d.RefDirY, d.RefDirZ) };
            Check(PK_CONE_create(&cone, &reference), "oracle cone"); break;
        case SurfaceClass.Sphere:
            var s = KernelRuntime.GetSphereData(record.DataIndex);
            var sphere = new PK_SPHERE_sf_t { radius = s.Radius, basis_set = Frame(s.CenterX, s.CenterY, s.CenterZ, s.AxisX, s.AxisY, s.AxisZ, s.RefDirX, s.RefDirY, s.RefDirZ) };
            Check(PK_SPHERE_create(&sphere, &reference), "oracle sphere"); break;
        case SurfaceClass.Torus:
            var t = KernelRuntime.GetTorusData(record.DataIndex);
            var torus = new PK_TORUS_sf_t { major_radius = t.MajorRadius, minor_radius = t.MinorRadius, basis_set = Frame(t.LocationX, t.LocationY, t.LocationZ, t.AxisX, t.AxisY, t.AxisZ, t.RefDirX, t.RefDirY, t.RefDirZ) };
            Check(PK_TORUS_create(&torus, &reference), "oracle torus"); break;
        default: throw new InvalidOperationException("Unexpected surface.");
    }
    return reference;
}

static unsafe void CheckRoundtrip(int body, int reference, string path)
{
    var options = new M.PK_PART_transmit_o_s { o_t_version = 4, transmit_format = M.ParasolidConstants.PK_transmit_format_text_c,
        transmit_meshes = M.ParasolidConstants.PK_transmit_meshes_separate_c };
    var block = new M.PK_MEMORY_block_s();
    Check(KernelRuntime.PartTransmitB(1, &body, &options, &block), "transmit");
    try
    {
        using var file = File.Create(path);
        for (var b = &block; b != null; b = b->next) file.Write(new ReadOnlySpan<byte>(b->bytes, checked((int)b->n_bytes)));
    }
    finally { Check(KernelRuntime.MemoryBlockFree(&block), "free transmit"); }
    var bytes = File.ReadAllBytes(path);
    fixed (byte* data = bytes)
    {
        var input = new PK_MEMORY_block_t(null, (ulong)bytes.Length, data);
        var receiveOptions = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int count;
        int* parts;
        Check(PK_PART_receive_b(input, &receiveOptions, &count, &parts), "oracle receive");
        try
        {
            Equal(1, count, "part count");
            var compareOptions = new PK_DEBUG_BODY_compare_o_t { max_diffs = 64, all_tests = 0, acc_dev_tests = 0, non_match_tests = 0 };
            var result = new PK_DEBUG_BODY_compare_r_t();
            Check(PK_DEBUG_BODY_compare(reference, parts[0], &compareOptions, &result), "body compare");
            try
            {
                if (result.global_result != PK_DEBUG_global_res_no_diffs_c || result.local_result != PK_DEBUG_local_res_no_diffs_c)
                {
                    for (var i = 0; i < result.n_global_diffs; i++)
                        Console.WriteLine($"global diff={result.global_diffs[i].diff} masters={result.global_diffs[i].n_masters} similars={result.global_diffs[i].n_similars}");
                    for (var i = 0; i < result.n_face_pairs; i++)
                    {
                        var pair = result.face_pairs[i];
                        for (var j = 0; j < pair.n_local_diffs; j++)
                        {
                            var diff = pair.local_diffs[j];
                            Console.WriteLine($"faces={pair.master_face}/{pair.similar_face} diff={diff.diff} entities={diff.master_entity}/{diff.similar_entity}");
                        }
                    }
                    throw new InvalidOperationException($"{path}: global={result.global_result} local={result.local_result} global_diffs={result.n_global_diffs} face_pairs={result.n_face_pairs}");
                }
            }
            finally { Check(PK_DEBUG_BODY_compare_r_f(&result), "free compare"); }
        }
        finally { Check(PK_MEMORY_free(parts), "free parts"); }
    }
}

sealed class ComparisonStats
{
    internal static readonly SortedDictionary<string, ComparisonStats> ByKind = new();
    internal long GridSamples;
    internal long Comparisons;
    internal double Point;
    internal double Tangent;
    internal double Derivative;

    internal static ComparisonStats For(string kind)
    {
        if (!ByKind.TryGetValue(kind, out var stats)) ByKind.Add(kind, stats = new ComparisonStats());
        return stats;
    }
}

// Partitioned Parasolid rollback requires a delta-storage frustrum. This
// oracle-only adapter uses the PKToy callback types; session setup remains in
// ParasolidScriptHost. No ABI declarations or session initialization copied.
static unsafe class MarkOracleStorage
{
    private static readonly Dictionary<uint, MemoryStream> Marks = new();
    private static uint nextDelta;
    public static void Register()
    {
        var callbacks = new PK_DELTA_frustrum_t
        { open_for_write_fn = &OpenWrite, open_for_read_fn = &OpenRead, close_fn = &Close,
            write_fn = &Write, read_fn = &Read, delete_fn = &Delete };
        var error = PK_DELTA_register_callbacks(callbacks);
        if (error != 0) throw new InvalidOperationException($"reference mark storage: error={error}");
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OpenWrite(int mark, uint* delta)
    {
        *delta = ++nextDelta;
        Marks.Add(*delta, new MemoryStream());
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OpenRead(uint delta)
    {
        if (!Marks.TryGetValue(delta, out var stream)) return PK_ERROR_bad_value;
        stream.Position = 0;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Close(uint mark) => 0;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Write(uint mark, uint count, byte* bytes)
    {
        if (!Marks.TryGetValue(mark, out var stream) || count > int.MaxValue) return PK_ERROR_bad_value;
        stream.Write(new ReadOnlySpan<byte>(bytes, (int)count));
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Read(uint mark, uint count, byte* bytes)
    {
        if (!Marks.TryGetValue(mark, out var stream) || count > int.MaxValue || stream.Length - stream.Position < count)
            return PK_ERROR_bad_value;
        stream.ReadExactly(new Span<byte>(bytes, (int)count));
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Delete(uint mark)
    {
        if (Marks.Remove(mark, out var stream)) stream.Dispose();
        return 0;
    }
}

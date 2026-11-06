using static parasolid;

// Both inputs are real-PK bodies: an independent reference and our XT reimport.
internal static unsafe class ParasolidBodyCompare
{
    public static void AssertEquivalent(PK_BODY_t reference, PK_BODY_t actual, string label)
    {
        PK_BODY_type_t referenceType, actualType;
        Check(PK_BODY_ask_type(reference, &referenceType), "reference type");
        Check(PK_BODY_ask_type(actual, &actualType), "actual type");
        Require(referenceType == actualType, label + " body type differs");
        AssertRegions(reference, actual, label);
        var options = new PK_DEBUG_BODY_compare_o_t
        {
            max_diffs = 1024,
            all_tests = PK_LOGICAL_false,
            acc_dev_tests = PK_LOGICAL_false,
            tol_dev_tests = PK_LOGICAL_true,
            non_match_tests = PK_LOGICAL_true,
        };
        PK_DEBUG_BODY_compare_r_t result;
        Check(PK_DEBUG_BODY_compare(reference, actual, &options, &result), label + " compare");
        try
        {
            Console.WriteLine($"{label}: global={result.global_result} local={result.local_result} face_pairs={result.n_face_pairs}");
            var chartOnly = result.local_result == PK_DEBUG_local_res_no_diffs_c;
            var localCount = 0;
            var differences = 0;
            for (var i = 0; i < result.n_global_diffs; i++)
            {
                var diff = result.global_diffs[i];
                if (diff.n_masters != diff.n_similars)
                    Console.WriteLine($"  global diff={diff.diff} counts={diff.n_masters}/{diff.n_similars}");
            }
            for (var f = 0; f < result.n_face_pairs; f++)
                for (var i = 0; i < result.face_pairs[f].n_local_diffs; i++)
                {
                    var pair = result.face_pairs[f];
                    var diff = pair.local_diffs[i];
                    localCount++;
                    // FD 106.7: chart points do not affect the shape of a curve.
                    if (diff.diff == PK_DEBUG_diff_n_cht_pts_c) chartOnly = true;
                    else differences++;
                    Console.WriteLine($"  faces={pair.master_face}/{pair.similar_face} diff={diff.diff} entities={diff.master_entity}/{diff.similar_entity} counts={diff.master_int}/{diff.similar_int} values={diff.master_double:R}/{diff.similar_double:R}");
                }
            Require(result.global_result == PK_DEBUG_global_res_no_diffs_c && differences == 0
                && (result.local_result == PK_DEBUG_local_res_no_diffs_c || chartOnly && localCount > 0),
                label + " body geometry/structure/sense/tolerance/matching differs");
        }
        finally { Check(PK_DEBUG_BODY_compare_r_f(&result), "free compare"); }
        if (referenceType == PK_BODY_type_wire_c)
            AssertWireGeometry(reference, actual, label);
    }

    private static void AssertRegions(PK_BODY_t reference, PK_BODY_t actual, string label)
    {
        int aCount, bCount;
        PK_REGION_t* a;
        PK_REGION_t* b;
        Check(PK_BODY_ask_regions(reference, &aCount, &a), "reference regions");
        Check(PK_BODY_ask_regions(actual, &bCount, &b), "actual regions");
        try
        {
            Require(aCount == bCount, label + " region count differs");
            Span<long> aKeys = aCount <= 64 ? stackalloc long[aCount] : new long[aCount];
            Span<long> bKeys = bCount <= 64 ? stackalloc long[bCount] : new long[bCount];
            for (var i = 0; i < aCount; i++) { aKeys[i] = RegionKey(a[i]); bKeys[i] = RegionKey(b[i]); }
            aKeys.Sort(); bKeys.Sort();
            Require(aKeys.SequenceEqual(bKeys), label + " region material/shell membership differs");
        }
        finally { Free(a); Free(b); }
    }

    private static long RegionKey(PK_REGION_t region)
    {
        PK_LOGICAL_t solid;
        int count;
        PK_SHELL_t* shells;
        Check(PK_REGION_is_solid(region, &solid), "region material");
        Check(PK_REGION_ask_shells(region, &count, &shells), "region shells");
        Free(shells);
        return ((solid == PK_LOGICAL_true ? 1L : 0L) << 32) | (uint)count;
    }

    // Body compare has no local geometry tests for wires. Match bounded edges
    // independently, then sample each edge in both directions against the other
    // bounded edge (not its untrimmed support curve). Sampling is not a proof of
    // arbitrary curve equality; API-specific parameter/jet tests remain required.
    public static void AssertWireGeometry(PK_BODY_t reference, PK_BODY_t actual, string label)
    {
        int aCount, bCount;
        PK_EDGE_t* a;
        PK_EDGE_t* b;
        double tolerance;
        Check(PK_SESSION_ask_precision(&tolerance), "session precision");
        Check(PK_BODY_ask_edges(reference, &aCount, &a), "reference wire edges");
        Check(PK_BODY_ask_edges(actual, &bCount, &b), "actual wire edges");
        try
        {
            Require(aCount == bCount, label + " wire edge count differs");
            Span<bool> used = bCount <= 256 ? stackalloc bool[bCount] : new bool[bCount];
            used.Clear();
            for (var i = 0; i < aCount; i++)
            {
                var matched = false;
                for (var j = 0; j < bCount && !matched; j++)
                    if (!used[j] && EdgesMatch(a[i], b[j], tolerance))
                    { used[j] = true; matched = true; }
                Require(matched, $"{label} unmatched wire edge={a[i]}: bounded geometry/class/sense/precision differs");
            }
            Console.WriteLine($"{label}: {aCount} bounded wire edges matched, bidirectional samples passed");
        }
        finally { Free(a); Free(b); }
    }

    private static bool EdgesMatch(PK_EDGE_t a, PK_EDGE_t b, double tolerance)
    {
        PK_CURVE_t aCurve, bCurve;
        PK_CLASS_t aClass, bClass;
        PK_INTERVAL_t aRange, bRange;
        PK_LOGICAL_t aSense, bSense;
        PK_VECTOR_t* ends = stackalloc PK_VECTOR_t[4];
        Check(PK_EDGE_ask_geometry(a, PK_LOGICAL_true, &aCurve, &aClass, ends, &aRange, &aSense), "reference edge geometry");
        Check(PK_EDGE_ask_geometry(b, PK_LOGICAL_true, &bCurve, &bClass, ends + 2, &bRange, &bSense), "actual edge geometry");
        if (aCurve == 0 || bCurve == 0 || aClass != bClass || aSense != bSense)
        {
            Console.WriteLine($"  wire candidate edges={a}/{b} curves={aCurve}/{bCurve} classes={aClass}/{bClass} senses={aSense == PK_LOGICAL_true}/{bSense == PK_LOGICAL_true}");
            return false;
        }
        double aPrecision, bPrecision;
        Check(PK_EDGE_ask_precision(a, &aPrecision), "reference edge precision");
        Check(PK_EDGE_ask_precision(b, &bPrecision), "actual edge precision");
        if (Math.Abs(aPrecision - bPrecision) > tolerance)
        {
            Console.WriteLine($"  wire candidate edges={a}/{b} precision={aPrecision:R}/{bPrecision:R}");
            return false;
        }
        PK_VERTEX_t* vertices = stackalloc PK_VERTEX_t[4];
        Check(PK_EDGE_ask_vertices(a, vertices), "reference edge vertices");
        Check(PK_EDGE_ask_vertices(b, vertices + 2), "actual edge vertices");
        for (var end = 0; end < 2; end++)
        {
            if ((vertices[end] == 0) != (vertices[end + 2] == 0)) return false;
            // A closed edge without vertices has no topological seam point.
            // Its support-curve parameter origin is not an endpoint contract.
            if (vertices[end] == 0) continue;
            double aVertexPrecision, bVertexPrecision;
            Check(PK_VERTEX_ask_precision(vertices[end], &aVertexPrecision), "reference vertex precision");
            Check(PK_VERTEX_ask_precision(vertices[end + 2], &bVertexPrecision), "actual vertex precision");
            if (Math.Abs(aVertexPrecision - bVertexPrecision) > tolerance) return false;
            Check(PK_CURVE_eval(aCurve, aRange.value[end], 0, ends), "reference endpoint");
            Check(PK_CURVE_eval(bCurve, bRange.value[end], 0, ends + 1), "actual endpoint");
            if (Distance(ends[0], ends[1]) > tolerance)
            {
                Console.WriteLine($"  wire candidate edges={a}/{b} endpoint={end} distance={Distance(ends[0], ends[1]):R} ranges={aRange.value[0]:R},{aRange.value[1]:R}/{bRange.value[0]:R},{bRange.value[1]:R}");
                return false;
            }
        }
        return SamplesOnEdge(aCurve, aRange, b, tolerance) && SamplesOnEdge(bCurve, bRange, a, tolerance);
    }

    private static bool SamplesOnEdge(PK_CURVE_t curve, PK_INTERVAL_t range, PK_EDGE_t target, double tolerance)
    {
        if (!double.IsFinite(range.value[0]) || !double.IsFinite(range.value[1])) return false;
        var options = new PK_TOPOL_range_vector_o_t { opt_level = PK_range_opt_accuracy_c };
        for (var i = 0; i <= 64; i++)
        {
            var t = range.value[0] + (range.value[1] - range.value[0]) * i / 64;
            PK_VECTOR_t point;
            Check(PK_CURVE_eval(curve, t, 0, &point), "wire sample");
            PK_range_result_t status;
            PK_range_1_r_t result;
            Check(PK_TOPOL_range_vector(target, point, &options, &status, &result), "bounded edge distance");
            if (status != PK_range_result_found_c || !double.IsFinite(result.distance) || result.distance > tolerance)
            {
                Console.WriteLine($"  wire sample curve={curve} target={target} t={t:R} status={status} distance={result.distance:R} tolerance={tolerance:R}");
                return false;
            }
        }
        return true;
    }

    private static double Distance(PK_VECTOR_t a, PK_VECTOR_t b)
    {
        var squared = 0.0;
        for (var i = 0; i < 3; i++) squared += (a.coord[i] - b.coord[i]) * (a.coord[i] - b.coord[i]);
        return Math.Sqrt(squared);
    }

    private static void Free(void* pointer) { if (pointer != null) Check(PK_MEMORY_free(pointer), "free comparison array"); }
    private static void Check(PK_ERROR_code_t error, string label) => ParasolidScriptHost.Check(error, label);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

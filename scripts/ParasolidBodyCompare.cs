using static parasolid;
using System.Text.Json.Nodes;

// Both inputs are real-PK bodies: an independent reference and our XT reimport.
internal static unsafe class ParasolidBodyCompare
{
    internal static Action<JsonObject>? Observer;
    // PK returns an arc-length bound, separately from its nominal value.
    // Summing bounds on a partition keeps the same complete native trim.
    internal static (double Value,double Lower,double Upper) FiniteLength(PK_CURVE_t curve,PK_INTERVAL_t range,int pieces)
    {
        var start=range.value[0];var end=range.value[1];var first=start;double value=0,lower=0,upper=0;
        for(var i=0;i<pieces;i++)
        {
            var last=i==pieces-1?end:start+(end-start)*(i+1)/pieces;
            Require(last>first,"finite length subdivision is representable");
            double nominal;PK_INTERVAL_t bounds;
            Check(PK_CURVE_find_length(curve,new(first,last),&nominal,&bounds),"finite curve length");
            Require(double.IsFinite(nominal)&&nominal>0&&double.IsFinite(bounds.value[0])&&double.IsFinite(bounds.value[1])
                &&bounds.value[0]<=nominal&&nominal<=bounds.value[1],"native length enclosure contains nominal length");
            value+=nominal;lower=Math.BitDecrement(lower+bounds.value[0]);upper=Math.BitIncrement(upper+bounds.value[1]);first=last;
        }
        return (value,lower,upper);
    }

    public static void AssertEquivalent(PK_BODY_t reference, PK_BODY_t actual, string label)
    {
        var observation=Observer==null?null:new JsonObject{["label"]=label,["referenceBody"]=reference,["actualBody"]=actual,
            ["globalDiffs"]=new JsonArray(),["localDiffs"]=new JsonArray(),["facePairRecords"]=new JsonArray()};
        try{AssertEquivalentCore(reference,actual,label,observation);if(observation!=null)observation["status"]="Verified";}
        catch(Exception error){if(observation!=null){observation["status"]="Rejected";observation["error"]=error.Message;}throw;}
        finally{if(observation!=null)try{Observer?.Invoke(observation);}catch(Exception error){Console.Error.WriteLine("COMPARISON_OBSERVATION_ERROR "+error);}}
    }

    private static void AssertEquivalentCore(PK_BODY_t reference,PK_BODY_t actual,string label,JsonObject? observation)
    {
        PK_BODY_type_t referenceType, actualType;
        Check(PK_BODY_ask_type(reference, &referenceType), "reference type");
        Check(PK_BODY_ask_type(actual, &actualType), "actual type");
        if(observation!=null){observation["referenceType"]=referenceType;observation["actualType"]=actualType;}
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
        if(observation!=null)observation["mask"]=new JsonObject{["o_t_version"]=options.o_t_version,["max_diffs"]=options.max_diffs,["all_tests"]=options.all_tests==PK_LOGICAL_true,
            ["acc_dev_tests"]=options.acc_dev_tests==PK_LOGICAL_true,["tol_dev_tests"]=options.tol_dev_tests==PK_LOGICAL_true,["num_tests"]=options.num_tests==PK_LOGICAL_true,
            ["class_tests"]=options.class_tests==PK_LOGICAL_true,["sense_tests"]=options.sense_tests==PK_LOGICAL_true,["tol_tests"]=options.tol_tests==PK_LOGICAL_true,
            ["missing_tests"]=options.missing_tests==PK_LOGICAL_true,["non_match_tests"]=options.non_match_tests==PK_LOGICAL_true};
        var nativeError=PK_DEBUG_BODY_compare(reference,actual,&options,&result);
        if(observation!=null)observation["nativeError"]=nativeError;
        Check(nativeError,label+" compare");
        try
        {
            Console.WriteLine($"{label}: global={result.global_result} local={result.local_result} face_pairs={result.n_face_pairs}");
            var provedLocal = result.local_result == PK_DEBUG_local_res_no_diffs_c;
            if(observation!=null){observation["globalResult"]=result.global_result;observation["localResult"]=result.local_result;observation["facePairs"]=result.n_face_pairs;}
            if(observation!=null)for(var f=0;f<result.n_face_pairs;f++)observation["facePairRecords"]!.AsArray().Add((JsonNode)new JsonObject{
                ["masterFace"]=result.face_pairs[f].master_face,["similarFace"]=result.face_pairs[f].similar_face,["localDiffCount"]=result.face_pairs[f].n_local_diffs});
            var localCount = 0;
            var differences = 0;
            for (var i = 0; i < result.n_global_diffs; i++)
            {
                var diff = result.global_diffs[i];
                observation?["globalDiffs"]!.AsArray().Add((JsonNode)new JsonObject{["type"]=diff.diff,["masters"]=diff.n_masters,["similars"]=diff.n_similars});
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
                    Console.WriteLine($"  faces={pair.master_face}/{pair.similar_face} diff={diff.diff} entities={diff.master_entity}/{diff.similar_entity} counts={diff.master_int}/{diff.similar_int} values={diff.master_double:R}/{diff.similar_double:R}");
                    var item=observation==null?null:new JsonObject{["masterFace"]=pair.master_face,["similarFace"]=pair.similar_face,["type"]=diff.diff,
                        ["masterEntity"]=diff.master_entity,["similarEntity"]=diff.similar_entity,["masterClass"]=diff.master_class,["similarClass"]=diff.similar_class,
                        ["masterLogical"]=diff.master_logical.Value,["similarLogical"]=diff.similar_logical.Value,
                        ["masterInt"]=diff.master_int,["similarInt"]=diff.similar_int,["masterDouble"]=double.IsFinite(diff.master_double)?diff.master_double:null,
                        ["similarDouble"]=double.IsFinite(diff.similar_double)?diff.similar_double:null,["masterDoubleBits"]=BitConverter.DoubleToInt64Bits(diff.master_double).ToString("x16"),
                        ["similarDoubleBits"]=BitConverter.DoubleToInt64Bits(diff.similar_double).ToString("x16"),["diagnostic"]=diff.diff==PK_DEBUG_diff_n_cht_pts_c};
                    if(item!=null)observation!["localDiffs"]!.AsArray().Add((JsonNode)item);
                    if (diff.diff == PK_DEBUG_diff_n_cht_pts_c) provedLocal = true;
                    else if (diff.diff == PK_DEBUG_diff_curve_sense_c && referenceType == PK_BODY_type_solid_c)
                    {
                        var proof=item==null?null:new JsonObject();if(item!=null)item["conditionalFiniteProof"]=proof;
                        var established=ProveFiniteAnalyticDirection(reference,actual,diff.master_entity,diff.similar_entity,in result,proof);
                        if(proof!=null)proof["proved"]=established;
                        if(established)provedLocal=true;else differences++;
                    }
                    else differences++;
                }
            Require(result.global_result == PK_DEBUG_global_res_no_diffs_c && differences == 0
                && (result.local_result == PK_DEBUG_local_res_no_diffs_c
                    || provedLocal && localCount > 0 && result.n_face_pairs < options.max_diffs),
                label + " body geometry/structure/sense/tolerance/matching differs");
        }
        finally { Check(PK_DEBUG_BODY_compare_r_f(&result), "free compare"); }
        if (referenceType == PK_BODY_type_wire_c)
            AssertWireGeometry(reference, actual, label);
    }

    // XT 5.2.9 stores edge/curve orientation in curve.sense. FD's
    // PK_DEBUG_diff_t reports that raw field; it does not certify the finite
    // parameter map or incident directed uses. Only these analytic classes
    // have a complete coefficient proof here. No output geometry is changed.
    private static bool ProveFiniteAnalyticDirection(PK_BODY_t reference, PK_BODY_t actual, PK_CURVE_t aCurve, PK_CURVE_t bCurve,
        in PK_DEBUG_BODY_compare_r_t comparison,JsonObject? proof=null)
    {
        var a = UniqueCurveEdge(reference, aCurve); var b = UniqueCurveEdge(actual, bCurve);
        if (a == 0 || b == 0) return false;
        PK_CURVE_t ac, bc; PK_CLASS_t ak, bk; PK_INTERVAL_t ar, br; PK_LOGICAL_t ass, bs;
        PK_VECTOR_t* ends = stackalloc PK_VECTOR_t[4];
        Check(PK_EDGE_ask_geometry(a, PK_LOGICAL_true, &ac, &ak, ends, &ar, &ass), "direction reference edge");
        Check(PK_EDGE_ask_geometry(b, PK_LOGICAL_true, &bc, &bk, ends + 2, &br, &bs), "direction actual edge");
        if (ac != aCurve || bc != bCurve || ak != bk || ass == bs || !FiniteRange(ar) || !FiniteRange(br)) return false;
        double ap, bp;
        Check(PK_EDGE_ask_precision(a, &ap), "direction reference precision"); Check(PK_EDGE_ask_precision(b, &bp), "direction actual precision");
        if (!(ap > 0) || !double.IsFinite(ap) || ap != bp) return false;
        var tolerance = Math.Min(1e-9, ap);
        if(proof!=null){proof["sourceEdge"]=a;proof["targetEdge"]=b;proof["class"]=ak;proof["edgePrecision"]=ap;proof["tolerance"]=tolerance;
            proof["sourceRange"]=new JsonArray(ar.value[0],ar.value[1]);proof["targetRange"]=new JsonArray(br.value[0],br.value[1]);proof["sourceSense"]=ass==PK_LOGICAL_true;proof["targetSense"]=bs==PK_LOGICAL_true;}
        double scale, shift, bound, speed; double turns = 0;
        if (ak == PK_CLASS_line)
        {
            PK_LINE_sf_t af, bf; Check(PK_LINE_ask(ac, &af), "direction reference line"); Check(PK_LINE_ask(bc, &bf), "direction actual line");
            var ao = Read(af.basis_set.location); var bo = Read(bf.basis_set.location);
            var ad = Read(af.basis_set.axis); var bd = Read(bf.basis_set.axis);
            var norm = Dot(bd, bd); if (!(norm > 0)) return false;
            scale = Dot(ad, bd) / norm; shift = Dot(ao - bo, bd) / norm;
            if (!double.IsFinite(scale) || scale == 0 || !double.IsFinite(shift)) return false;
            var tangentDot = Dot(Lift(ad), Lift(bd));
            if (scale > 0 ? tangentDot.Low <= 0 : tangentDot.High >= 0) return false;
            var residual = Lift(ao) - Lift(bo) - Lift(bd) * new Enclosure(shift)
                + (Lift(ad) - Lift(bd) * new Enclosure(scale)) * new Enclosure(ar.value[0], ar.value[1]);
            bound = residual.L1; speed = Lift(bd).L1;
        }
        else if (ak == PK_CLASS_circle || ak == PK_CLASS_ellipse)
        {
            if (!Conic(ac, ak, out var ao, out var ax, out var ay, out var axi, out var ayi)
                || !Conic(bc, bk, out var bo, out var bx, out var by, out var bxi, out var byi)) return false;
            if (ar.value[1] - ar.value[0] > Math.Tau + 1e-12 || br.value[1] - br.value[0] > Math.Tau + 1e-12) return false;
            var xx = Dot(bx, bx); var yy = Dot(by, by); if (!(xx > 0) || !(yy > 0)) return false;
            shift = Math.Atan2(Dot(ax, by) / yy, Dot(ax, bx) / xx);
            var tangent = bx * -Math.Sin(shift) + by * Math.Cos(shift);
            scale = Dot(ay, tangent) >= 0 ? 1 : -1;
            SinCosEnclosure(shift, out var sin, out var cos);
            var cosineResidual = axi - bxi * cos - byi * sin;
            var sineResidual = ayi - (byi * cos - bxi * sin) * new Enclosure(scale);
            var derivativeError = Up(cosineResidual.L1 + sineResidual.L1);
            var gramLower = Math.BitDecrement(Math.Min(Dot(axi, axi).Low, Dot(ayi, ayi).Low) - Dot(axi, ayi).Abs);
            if (!(gramLower > 0) || Up(derivativeError * derivativeError) >= gramLower) return false;
            bound = Up((Lift(ao) - Lift(bo)).L1 + derivativeError);
            speed = Up(bxi.L1 + byi.L1);
        }
        else return false;
        var first = new Enclosure(shift) + new Enclosure(scale) * new Enclosure(ar.value[0]);
        var last = new Enclosure(shift) + new Enclosure(scale) * new Enclosure(ar.value[1]);
        if (scale < 0) (first, last) = (last, first);
        if (ak != PK_CLASS_line)
        {
            turns = Math.Round((br.value[0] - first.Low) / Math.Tau);
            if (!double.IsFinite(turns) || Math.Abs(turns) > 8) return false;
            // Math.Tau is binary64, not exact 2*pi. This enclosure covers the
            // true integer period used in the coefficient identity.
            var period = new Enclosure(turns) * new Enclosure(Math.Tau, Up(Math.Tau));
            first += period; last += period;
        }
        var trimError = Math.Max((first - new Enclosure(br.value[0])).Abs, (last - new Enclosure(br.value[1])).Abs);
        var total = Up(bound + Up(trimError * speed));
        if(proof!=null){proof["shift"]=shift;proof["scale"]=scale;proof["turns"]=turns;proof["coefficientBound"]=double.IsFinite(bound)?bound:null;
            proof["trimParameterError"]=double.IsFinite(trimError)?trimError:null;proof["speedBound"]=double.IsFinite(speed)?speed:null;proof["totalBound"]=double.IsFinite(total)?total:null;}
        if (!double.IsFinite(total) || total > tolerance) return false;
        var mapSign = scale > 0 ? 1 : -1;
        var reversedEdge = (ass == PK_LOGICAL_true ? 1 : -1) * mapSign != (bs == PK_LOGICAL_true ? 1 : -1);
        var endpoints=proof==null?null:new JsonArray();var fins=proof==null?null:new JsonArray();
        if(proof!=null){proof["endpoints"]=endpoints;proof["directedFinUses"]=fins;proof["edgeReversed"]=reversedEdge;}
        if (!ProveEndpointUses(a, b, reversedEdge, tolerance,endpoints) || !ProveFinUses(a, b, ac, bc, ass, bs, mapSign, in comparison,fins)) return false;
        Console.WriteLine($"  FINITE_DIRECTION_VERIFIED curves={ac}/{bc} edges={a}/{b} class={ak} map={shift:R}+({scale:R})*t+({turns:R})*2pi trims=[{ar.value[0]:R},{ar.value[1]:R}]/[{br.value[0]:R},{br.value[1]:R}] full_coefficient_and_trim_bound={total:R} tolerance={tolerance:R} edge_reversed={reversedEdge} incident_uses=2");
        return true;
    }

    private static bool FiniteRange(PK_INTERVAL_t range) => double.IsFinite(range.value[0]) && double.IsFinite(range.value[1]) && range.value[0] < range.value[1];

    private static PK_EDGE_t UniqueCurveEdge(PK_BODY_t body, PK_CURVE_t curve)
    {
        int count; PK_EDGE_t* edges; Check(PK_BODY_ask_edges(body, &count, &edges), "direction body edges");
        PK_EDGE_t found = 0;
        try
        {
            for (var i = 0; i < count; i++)
            {
                PK_CURVE_t candidate; Check(PK_EDGE_ask_curve(edges[i], &candidate), "direction edge curve");
                if (candidate != curve) continue; if (found != 0) return 0; found = edges[i];
            }
            return found;
        }
        finally { Free(edges); }
    }

    private static bool ProveEndpointUses(PK_EDGE_t a, PK_EDGE_t b, bool reverse, double tolerance,JsonArray? observation=null)
    {
        PK_VERTEX_t* vertices = stackalloc PK_VERTEX_t[4];
        Check(PK_EDGE_ask_vertices(a, vertices), "direction reference vertices"); Check(PK_EDGE_ask_vertices(b, vertices + 2), "direction actual vertices");
        if (vertices[0] == 0 || vertices[1] == 0 || vertices[2] == 0 || vertices[3] == 0)
            return vertices[0] == 0 && vertices[1] == 0 && vertices[2] == 0 && vertices[3] == 0;
        for (var end = 0; end < 2; end++)
        {
            var av = vertices[end]; var bv = vertices[2 + (reverse ? 1 - end : end)];
            double ap, bp; Check(PK_VERTEX_ask_precision(av, &ap), "direction reference vertex precision"); Check(PK_VERTEX_ask_precision(bv, &bp), "direction actual vertex precision");
            if (!(ap > 0) || !double.IsFinite(ap) || ap != bp) return false;
            PK_POINT_t at, bt; PK_POINT_sf_t af, bf;
            Check(PK_VERTEX_ask_point(av, &at), "direction reference point"); Check(PK_VERTEX_ask_point(bv, &bt), "direction actual point");
            Check(PK_POINT_ask(at, &af), "direction reference position"); Check(PK_POINT_ask(bt, &bf), "direction actual position");
            var distance=(Lift(Read(af.position))-Lift(Read(bf.position))).L1;
            observation?.Add((JsonNode)new JsonObject{["sourceVertex"]=av,["targetVertex"]=bv,["precision"]=ap,["bound"]=double.IsFinite(distance)?distance:null,["tolerance"]=Math.Min(tolerance,ap)});
            if (distance > Math.Min(tolerance, ap)) return false;
        }
        return true;
    }

    private static bool ProveFinUses(PK_EDGE_t a, PK_EDGE_t b, PK_CURVE_t ac, PK_CURVE_t bc, PK_LOGICAL_t ass, PK_LOGICAL_t bs, int mapSign,
        in PK_DEBUG_BODY_compare_r_t comparison,JsonArray? observation=null)
    {
        int an, bn; PK_FIN_t* af; PK_FIN_t* bf;
        Check(PK_EDGE_ask_fins(a, &an, &af), "direction reference fins"); Check(PK_EDGE_ask_fins(b, &bn, &bf), "direction actual fins");
        try
        {
            if (an != 2 || bn != 2) return false;
            for (var i = 0; i < 2; i++)
            {
                PK_FACE_t face; Check(PK_FIN_ask_face(af[i], &face), "direction reference incident face");
                PK_FACE_t paired = 0;
                for (var p = 0; p < comparison.n_face_pairs; p++) if (comparison.face_pairs[p].master_face == face)
                { if (paired != 0 && paired != comparison.face_pairs[p].similar_face) return false; paired = comparison.face_pairs[p].similar_face; }
                if (paired == 0) return false;
                PK_FIN_t target = 0;
                for (var j = 0; j < 2; j++)
                {
                    PK_FACE_t other; Check(PK_FIN_ask_face(bf[j], &other), "direction actual incident face");
                    if (other != paired) continue; if (target != 0) return false; target = bf[j];
                }
                if (target == 0 || !FinDirection(af[i], ac, ass, out var ad) || !FinDirection(target, bc, bs, out var bd) || ad * mapSign != bd) return false;
                observation?.Add((JsonNode)new JsonObject{["sourceFace"]=face,["targetFace"]=paired,["sourceFin"]=af[i],["targetFin"]=target,
                    ["sourceNaturalDirection"]=ad,["targetNaturalDirection"]=bd,["mapSign"]=mapSign,["finCarrierConsistent"]=true});
                Console.WriteLine($"    DIRECTED_FIN_USE faces={face}/{paired} fins={af[i]}/{target} natural_directions={ad}/{bd} map_sign={mapSign}");
            }
            return true;
        }
        finally { Free(af); Free(bf); }
    }

    private static bool FinDirection(PK_FIN_t fin, PK_CURVE_t curve, PK_LOGICAL_t edgeSense, out int direction)
    {
        PK_LOGICAL_t positive, oriented; PK_CURVE_t finCurve;
        Check(PK_FIN_is_positive(fin, &positive), "direction fin/edge sense"); Check(PK_FIN_ask_oriented_curve(fin, &finCurve, &oriented), "direction fin/curve sense");
        direction = (positive == PK_LOGICAL_true ? 1 : -1) * (edgeSense == PK_LOGICAL_true ? 1 : -1);
        return finCurve == 0 || finCurve == curve && direction == (oriented == PK_LOGICAL_true ? 1 : -1);
    }

    private static bool Conic(PK_CURVE_t curve, PK_CLASS_t kind, out Vector center, out Vector x, out Vector y,
        out VectorEnclosure xi, out VectorEnclosure yi)
    {
        PK_AXIS2_sf_t basis; double r1, r2;
        if (kind == PK_CLASS_circle)
        { PK_CIRCLE_sf_t sf; Check(PK_CIRCLE_ask(curve, &sf), "direction circle"); basis = sf.basis_set; r1 = r2 = sf.radius; }
        else
        { PK_ELLIPSE_sf_t sf; Check(PK_ELLIPSE_ask(curve, &sf), "direction ellipse"); basis = sf.basis_set; r1 = sf.R1; r2 = sf.R2; }
        center = Read(basis.location); var axis = Read(basis.axis); var reference = Read(basis.ref_direction);
        x = reference * r1; y = Cross(axis, reference) * r2;
        xi = Lift(reference) * new Enclosure(r1); yi = Cross(Lift(axis), Lift(reference)) * new Enclosure(r2);
        return r1 > 0 && r2 > 0 && double.IsFinite(r1) && double.IsFinite(r2);
    }

    private readonly record struct Vector(double X, double Y, double Z)
    {
        public static Vector operator +(Vector a, Vector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector operator -(Vector a, Vector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector operator *(Vector a, double b) => new(a.X * b, a.Y * b, a.Z * b);
    }
    private static Vector Read(PK_VECTOR_t v) => new(v.coord[0], v.coord[1], v.coord[2]);
    private static Vector Read(PK_VECTOR1_t v) => new(v.coord[0], v.coord[1], v.coord[2]);
    private static Vector Cross(Vector a, Vector b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static VectorEnclosure Cross(VectorEnclosure a, VectorEnclosure b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static double Dot(Vector a, Vector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static Enclosure Dot(VectorEnclosure a, VectorEnclosure b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static double Up(double value) => Math.BitIncrement(value);
    private readonly record struct Enclosure(double Low, double High)
    {
        internal Enclosure(double value) : this(value, value) { }
        internal double Abs => Math.Max(Math.Abs(Low), Math.Abs(High));
        public static Enclosure operator +(Enclosure a, Enclosure b) => new(Math.BitDecrement(a.Low + b.Low), Up(a.High + b.High));
        public static Enclosure operator -(Enclosure a, Enclosure b) => a + new Enclosure(-b.High, -b.Low);
        public static Enclosure operator *(Enclosure a, Enclosure b)
        {
            var x = a.Low * b.Low; var y = a.Low * b.High; var z = a.High * b.Low; var w = a.High * b.High;
            return new(Math.BitDecrement(Math.Min(Math.Min(x, y), Math.Min(z, w))), Up(Math.Max(Math.Max(x, y), Math.Max(z, w))));
        }
        public static Enclosure operator /(Enclosure a, double positive) => new(Math.BitDecrement(a.Low / positive), Up(a.High / positive));
    }
    private readonly record struct VectorEnclosure(Enclosure X, Enclosure Y, Enclosure Z)
    {
        internal double L1 => Up(X.Abs + Up(Y.Abs + Z.Abs));
        public static VectorEnclosure operator +(VectorEnclosure a, VectorEnclosure b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static VectorEnclosure operator -(VectorEnclosure a, VectorEnclosure b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static VectorEnclosure operator *(VectorEnclosure a, Enclosure b) => new(a.X * b, a.Y * b, a.Z * b);
    }
    private static VectorEnclosure Lift(Vector v) => new(new(v.X), new(v.Y), new(v.Z));
    private static void SinCosEnclosure(double angle, out Enclosure sin, out Enclosure cos)
    {
        // Atan2 supplies |angle| <= pi. 24 Taylor terms and |angle| <= 4
        // bound both omitted remainders below 1e-25; every operation rounds out.
        if (!double.IsFinite(angle) || Math.Abs(angle) > 4) { sin = cos = new(double.NaN); return; }
        var a = new Enclosure(angle); var minusSquared = new Enclosure(-1) * a * a;
        var st = a; var ct = new Enclosure(1); sin = st; cos = ct;
        for (var k = 1; k <= 24; k++)
        {
            st = st * minusSquared / ((2.0 * k) * (2.0 * k + 1)); ct = ct * minusSquared / ((2.0 * k - 1) * (2.0 * k));
            sin += st; cos += ct;
        }
        sin += new Enclosure(-1e-25, 1e-25); cos += new Enclosure(-1e-25, 1e-25);
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

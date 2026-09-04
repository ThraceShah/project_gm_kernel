using ProjectGmKernel.Native.Runtime;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace KernelTests;

/// <summary>
/// Geometry definition tests: the procedural-geometry records in
/// GeometryRecords.cs must stay in lockstep with the authoritative XT
/// schema (third_party/parasolid/schema/sch_37102.sch_txt) and the
/// Parasolid class tokens (parasolid_tokens.h).
/// </summary>
public sealed class GeometryDefinitionTests
{
    // ── Class token values (parasolid_tokens.h TYCU*/TYSU*) ──────────

    [Fact]
    public void CurveClass_MatchesParasolidTokens()
    {
        Assert.Equal(0, (int)CurveClass.None);
        Assert.Equal(3001, (int)CurveClass.Line);
        Assert.Equal(3002, (int)CurveClass.Circle);
        Assert.Equal(3003, (int)CurveClass.Ellipse);
        Assert.Equal(3004, (int)CurveClass.ICurve);   // TYCUIN intersection curve
        Assert.Equal(3005, (int)CurveClass.BCurve);    // TYCUPA parametric curve
        Assert.Equal(3006, (int)CurveClass.SPCurve);   // TYCUSP surface parameter curve
        Assert.Equal(3007, (int)CurveClass.FCurve);    // TYCUFG foreign curve
        Assert.Equal(3008, (int)CurveClass.CpCurve);   // TYCUCP constant-parameter curve
        Assert.Equal(3009, (int)CurveClass.TRCurve);   // TYCUTR trimmed curve
    }

    [Fact]
    public void SurfaceClass_MatchesParasolidTokens()
    {
        Assert.Equal(0, (int)SurfaceClass.None);
        Assert.Equal(4001, (int)SurfaceClass.Plane);
        Assert.Equal(4002, (int)SurfaceClass.Cylinder);
        Assert.Equal(4003, (int)SurfaceClass.Cone);
        Assert.Equal(4004, (int)SurfaceClass.Sphere);
        Assert.Equal(4005, (int)SurfaceClass.Torus);
        Assert.Equal(4006, (int)SurfaceClass.BSurface);   // TYSUPA parametric surface
        Assert.Equal(4007, (int)SurfaceClass.BlendSurface); // TYSUBL blending surface
        Assert.Equal(4008, (int)SurfaceClass.Offset);     // TYSUOF offset surface
        Assert.Equal(4009, (int)SurfaceClass.Swept);      // TYSUSE swept surface
        Assert.Equal(4010, (int)SurfaceClass.Spun);       // TYSUSU swung/spun surface
        Assert.Equal(4011, (int)SurfaceClass.FSurface);   // TYSUFG foreign surface
    }

    [Fact]
    public void BlendAndLimitEnums_MatchXtSchemaCharacters()
    {
        Assert.Equal((byte)'R', (byte)BlendType.RollingBall);
        Assert.Equal((byte)'E', (byte)BlendType.CliffEdge);
        Assert.Equal((byte)'H', (byte)LimitType.Help);
        Assert.Equal((byte)'T', (byte)LimitType.Terminator);
        Assert.Equal((byte)'L', (byte)LimitType.Artificial);
        Assert.Equal((byte)'B', (byte)LimitType.SpineBoundary);
        Assert.Equal((byte)'F', (byte)LimitTermUse.First);
        Assert.Equal((byte)'S', (byte)LimitTermUse.Second);
        Assert.Equal((byte)'?', (byte)LimitTermUse.Unset);
        Assert.Equal(1, (int)IntersectionUvType.None);
        Assert.Equal(2, (int)IntersectionUvType.First);
        Assert.Equal(3, (int)IntersectionUvType.Second);
        Assert.Equal(4, (int)IntersectionUvType.Both);
        Assert.Equal((byte)'V', (byte)OffsetCheckState.Valid);
        Assert.Equal((byte)'I', (byte)OffsetCheckState.Invalid);
        Assert.Equal((byte)'U', (byte)OffsetCheckState.Unchecked);
    }

    // ── Record shape vs the live XT schema ───────────────────────────
    //
    // Every payload field of the schema node must either map to record
    // members (reflection) or be listed as intentionally not modelled.

    private static readonly string[] HeaderFields =
    [
        "node_id", "attributes_features", "owner", "next", "previous", "geometric_owner", "sense",
    ];

    [Fact]
    public void BlendedEdgeData_CoversSchemaNode56()
        => AssertRecordCoversSchemaNode(56, typeof(BlendedEdgeData), new()
        {
            ["blend_type"] = ["BlendType"],
            ["surface"] = ["Surface0Tag", "Surface1Tag"],
            ["spine"] = ["SpineTag"],
            ["range"] = ["Range0", "Range1"],
            ["thumb_weight"] = ["ThumbWeight0", "ThumbWeight1"],
            ["boundary"] = ["Boundary0Tag", "Boundary1Tag"],
            ["start"] = ["StartLimit"],
            ["end"] = ["EndLimit"],
            ["approx_spine"] = ["ApproxSpineTag"],
            ["approx_spine_ctol"] = ["ApproxSpineCtol"],
        }, []);

    [Fact]
    public void BlendedVertexData_CoversSchemaNode57()
        => AssertRecordCoversSchemaNode(57, typeof(BlendedVertexData), new()
        {
            ["blend_type"] = ["BlendType"],
            ["surface"] = ["Surface0Tag", "Surface1Tag", "Surface2Tag"],
            ["sub_surface"] = ["SubSurface0Tag", "SubSurface1Tag", "SubSurface2Tag"],
            ["boundary"] = ["Boundary0Tag", "Boundary1Tag", "Boundary2Tag"],
            ["range"] = ["Range0", "Range1", "Range2"],
            ["thumb_weight"] = ["ThumbWeight0", "ThumbWeight1", "ThumbWeight2"],
            ["centre"] = ["Centre"],
        }, []);

    [Fact]
    public void BlendOverlapData_CoversSchemaNode58()
        => AssertRecordCoversSchemaNode(58, typeof(BlendOverlapData), new()
        {
            ["surface"] = ["Surface0Tag", "Surface1Tag"],
            ["sub_surface"] = ["SubSurface0Tag", "SubSurface1Tag", "SubSurface2Tag", "SubSurface3Tag"],
            ["range"] = ["Range0", "Range1", "Range2", "Range3"],
            ["thumb_weight"] = ["ThumbWeight0", "ThumbWeight1", "ThumbWeight2", "ThumbWeight3"],
            ["blend_type"] = ["BlendType0", "BlendType1"],
            ["overlap_type"] = ["OverlapType"],
            ["swap_u_v"] = ["SwapUV"],
        }, []);

    [Fact]
    public void BlendBoundData_CoversSchemaNode59()
        => AssertRecordCoversSchemaNode(59, typeof(BlendBoundData), new()
        {
            ["boundary"] = ["Boundary"],
            ["blend"] = ["BlendTag"],
        }, []);

    [Fact]
    public void ICurveData_CoversSchemaNode38()
        => AssertRecordCoversSchemaNode(38, typeof(ICurveData), new()
        {
            ["surface"] = ["Surface0Tag", "Surface1Tag"],
            // CHART (node 40) summary
            ["chart"] = ["BaseParameter", "BaseScale", "ChartCount", "ChartHvecOffset"],
            ["start"] = ["StartLimit"],
            ["end"] = ["EndLimit"],
            ["scale"] = ["Scale"],
            // INTERSECTION_DATA (node 204)
            ["intersection_data"] = ["UvType", "UvValueCount", "UvValueOffset"],
        }, []);

    [Fact]
    public void LimitRecord_CoversSchemaNode41()
        => AssertRecordCoversSchemaNode(41, typeof(LimitRecord), new()
        {
            ["type"] = ["Type"],
            ["term_use"] = ["TermUse"],
            ["hvec"] = ["HvecOffset", "HvecCount"],
        }, []);

    [Fact]
    public void OffsetCurveData_CoversSchemaNode46()
        => AssertRecordCoversSchemaNode(46, typeof(OffsetCurveData), new()
        {
            ["surface"] = ["SurfTag"],
            ["curve"] = ["CurveTag"],
            ["offset"] = ["Offset"],
        }, []);

    [Fact]
    public void CPCurveData_CoversSchemaNode48()
        => AssertRecordCoversSchemaNode(48, typeof(CPCurveData), new()
        {
            ["bezier"] = ["BezierSegments"],
            ["bspline"] = ["Bspline"],
        }, []);

    [Fact]
    public void OffsetData_CoversSchemaNode60()
        => AssertRecordCoversSchemaNode(60, typeof(OffsetData), new()
        {
            ["check"] = ["Check"],
            ["surface"] = ["BaseSurfTag"],
            ["offset"] = ["Offset"],
            ["scale"] = ["Scale"],
        }, ["true_offset", "uint", "vint", "u_start", "u_end", "v_start", "v_end", "tree"]);

    [Fact]
    public void SweptData_CoversSchemaNode67()
        => AssertRecordCoversSchemaNode(67, typeof(SweptData), new()
        {
            ["section"] = ["SectionCurveTag"],
            ["sweep"] = ["Sweep"],
            ["scale"] = ["Scale"],
        }, []);

    [Fact]
    public void SpunData_CoversSchemaNode68()
        => AssertRecordCoversSchemaNode(68, typeof(SpunData), new()
        {
            ["profile"] = ["ProfileCurveTag"],
            ["base"] = ["Base"],
            ["axis"] = ["Axis"],
            ["start"] = ["Start"],
            ["end"] = ["End"],
            ["start_param"] = ["StartParam"],
            ["end_param"] = ["EndParam"],
            ["x_axis"] = ["XAxis"],
            ["scale"] = ["Scale"],
        }, []);

    [Fact]
    public void TrimmedCurveData_CoversSchemaNode133()
        => AssertRecordCoversSchemaNode(133, typeof(TrimmedCurveData), new()
        {
            ["basis_curve"] = ["BasisCurveTag"],
            ["point_1"] = ["Point1"],
            ["point_2"] = ["Point2"],
            ["parm_1"] = ["Parm1"],
            ["parm_2"] = ["Parm2"],
        }, []);

    [Fact]
    public void SPCurveData_CoversSchemaNode137()
        => AssertRecordCoversSchemaNode(137, typeof(SPCurveData), new()
        {
            ["surface"] = ["SurfTag"],
            ["b_curve"] = ["BCurveTag"],
        }, ["original", "tolerance_to_original", "periodic", "class", "chart", "scale", "parameter_scale"]);

    private static void AssertRecordCoversSchemaNode(
        int nodeType,
        Type recordType,
        Dictionary<string, string[]> mappedMembers,
        string[] unmappedFields)
    {
        var schemaFields = ReadSchemaNodeFields(nodeType);
        var recordMembers = recordType.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(static field => field.Name)
            .ToHashSet(StringComparer.Ordinal);

        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in schemaFields)
        {
            if (HeaderFields.Contains(field))
                continue;
            if (unmappedFields.Contains(field))
                continue;
            Assert.True(mappedMembers.ContainsKey(field),
                $"{recordType.Name}: schema node {nodeType} field '{field}' is neither mapped nor declared unmapped");
            foreach (var member in mappedMembers[field])
                Assert.True(recordMembers.Contains(member),
                    $"{recordType.Name}: mapped member '{member}' for schema field '{field}' is missing");
            expected.UnionWith(mappedMembers[field]);
        }

        Assert.Subset(recordMembers, expected);
        // Every declared mapping key must exist in the schema payload.
        var schemaPayload = schemaFields.Where(field => !HeaderFields.Contains(field)).ToHashSet(StringComparer.Ordinal);
        foreach (var key in mappedMembers.Keys)
            Assert.True(schemaPayload.Contains(key),
                $"{recordType.Name}: mapping key '{key}' is not a payload field of schema node {nodeType}");
    }

    private static List<string> ReadSchemaNodeFields(int nodeType)
    {
        var schemaPath = FindSchemaPath();
        var fields = new List<string>();
        var inNode = false;
        foreach (var rawLine in File.ReadLines(schemaPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            if (char.IsAsciiDigit(line[0]))
            {
                var separator = line.IndexOf(' ');
                if (separator > 0 && int.TryParse(line.AsSpan(0, separator), out var type))
                {
                    if (inNode)
                        break;
                    inNode = type == nodeType;
                }
                continue;
            }
            if (!inNode)
                continue;
            var nameEnd = line.IndexOf(';');
            if (nameEnd > 0)
                fields.Add(line[..nameEnd]);
        }

        Assert.NotEmpty(fields);
        return fields;
    }

    private static string FindSchemaPath([CallerFilePath] string sourcePath = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        var path = Path.Combine(repositoryRoot, "third_party", "parasolid", "schema", "sch_37102.sch_txt");
        Assert.True(File.Exists(path), "schema file is missing: " + path);
        return path;
    }
}

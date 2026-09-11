using System.Text;
using ProjectGmKernel.Xt;
using Schema37102 = ProjectGmKernel.Xt.Schema.SCH_3701097_37102;

namespace ProjectGmKernel.Xt.Tests;

public sealed class XtSchemaCatalogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pgm-xt-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ExplicitDirectoryLoadsAndLosslessCodecRoundTrips()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "sch_test.sch_txt"), MinimalSchema);
        var catalog = XtSchemaCatalog.OpenDirectory(_directory);
        catalog.LoadAll();
        var schema = catalog.Resolve("SCH_3000000_30000");
        var document = new XtDocument
        {
            VersionText = ": TRANSMIT FILE created by test modeller version 3000000",
            HeaderSchemaIdentity = schema.Identity,
            Schema = schema,
            UserFieldSize = 0,
            Nodes =
            [
                new XtNode { Type = 12, Index = 1, Fields = [XtFieldValue.Int(42), XtFieldValue.Ptr(0)] },
            ],
        };

        var bytes = XtCodec.Write(catalog, document);
        var decoded = XtCodec.Read(catalog, bytes);
        var second = XtCodec.Write(catalog, decoded);

        Assert.Equal(schema.Identity, decoded.Schema.Identity);
        Assert.Equal(42, decoded.Nodes[0].Fields[0].Integer);
        Assert.Equal(bytes, second);
    }

    [Fact]
    public void MissingDirectoryAndMalformedStatisticsFailExplicitly()
    {
        Assert.Throws<XtFormatException>(() => XtSchemaCatalog.OpenDirectory(Path.Combine(_directory, "missing")));
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "sch_bad.sch_txt"), MinimalSchema.Replace("2 2 2 0", "2 3 2 0", StringComparison.Ordinal));
        var catalog = XtSchemaCatalog.OpenDirectory(_directory);
        Assert.Throws<XtFormatException>(catalog.LoadAll);
    }

    [Fact]
    public void CatalogDoesNotScanNestedSchemasOrProvideFallback()
    {
        Directory.CreateDirectory(_directory);
        var nested = Path.Combine(_directory, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "sch_test.sch_txt"), MinimalSchema);

        var catalog = XtSchemaCatalog.OpenDirectory(_directory);

        Assert.DoesNotContain(catalog.Schemas, static schema => schema.Identity == "SCH_3000000_30000");
        Assert.Throws<XtFormatException>(() => catalog.Resolve("SCH_3000000_99999"));
    }

    [Fact]
    public void BuiltInCatalogRequiresNoSchemaDirectory()
    {
        var catalog = XtSchemaCatalog.OpenBuiltIn();
        Assert.Equal(37102, catalog.Resolve("SCH_3701097_37102").SchemaNumber);
        Assert.Equal(37102, catalog.Resolve("SCH_3800150_37102").SchemaNumber);
    }

    [Fact]
    public void ProcessGeometryTypesMapSchemaFieldsDirectly()
    {
        AssertFields<Schema37102.INTERSECTION>("surface", "chart", "start", "end", "intersection_data", "scale");
        AssertFields<Schema37102.BLENDED_EDGE>("blend_type", "surface", "spine", "range", "thumb_weight", "boundary", "start", "end");
        AssertFields<Schema37102.NURBS_CURVE>("degree", "n_vertices", "vertex_dim", "n_knots", "periodic", "rational", "bspline_vertices", "knot_mult", "knots");
        AssertFields<Schema37102.NURBS_SURF>("u_degree", "v_degree", "n_u_vertices", "n_v_vertices", "rational", "bspline_vertices", "u_knot_mult", "v_knot_mult", "u_knots", "v_knots");
        AssertFields<Schema37102.SP_CURVE>("chart", "surface", "b_curve", "original", "tolerance_to_original");
        AssertFields<Schema37102.TRIMMED_CURVE>("basis_curve", "point_1", "point_2", "parm_1", "parm_2");
        AssertFields<Schema37102.SWEPT_SURF>("section", "sweep", "scale");
        AssertFields<Schema37102.SPUN_SURF>("profile", "base", "axis", "start", "end", "start_param", "end_param", "x_axis", "scale");
    }

    [Fact]
    public void GeneratedModelEncodesZeroedRowsAsValuesAndSentinelsAsNull()
    {
        var builder = new Schema37102.MODEL_BUILDER(new Schema37102.COUNTS { BODY = 1 });
        builder.BODY[0]._xt_index = 1;
        builder.BODY[0]._xt_order = 0;
        builder.BODY[0].res_size = XtSchemaField.NullReal;
        builder.BODY[0].next = new Schema37102.BODYRef { Index = XtSchemaField.NullPointer };
        var document = builder.FinalizeModel().ToDocument();
        Assert.Contains(Assert.Single(document.Nodes).Fields, static field => field.Kind == XtFieldKind.Empty);
        var redecoded = Schema37102.CODEC.Decode(document);
        Assert.Equal(XtSchemaField.NullReal, redecoded.BODY[0].res_size);
        Assert.Equal(XtSchemaField.NullPointer, redecoded.BODY[0].next.Index);
        Assert.Equal(0, redecoded.BODY[0].highest_node_id);
    }


    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static void AssertFields<T>(params string[] expected)
    {
        var actual = typeof(T).GetFields().Select(static field => field.Name).ToArray();
        foreach (var name in expected) Assert.Contains(name, actual);
        Assert.DoesNotContain("A", actual);
        Assert.DoesNotContain("Kind", actual);
    }

    private const string MinimalSchema = """
T
1
: SCHEMA FILE created by modeller version 3000000/30000;
2 2 2 0
1 NULLP; Null; 1 0 0
12 BODY; Body; 1 2 0
value; d; 1 0 0
owner; p; 1 12 0
** end of schema SCH_3000000_30000
""";

}

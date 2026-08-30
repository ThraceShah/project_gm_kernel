using System.Text;
using ProjectGmKernel.Xt;

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

        Assert.Empty(catalog.Schemas);
        Assert.Throws<XtFormatException>(() => catalog.Resolve("SCH_3000000_30000"));
    }

    [Fact]
    public void BuilderRejectsInvalidTopologyBeforeFreeze()
    {
        var builder = new XtBrepBuilder(new XtBrepCounts(Parts: 1, Bodies: 1));
        builder.Parts[0] = new XtPartRow { Kind = XtPartKind.Body, Body = 0, Assembly = -1, Name = -1 };
        builder.Bodies[0] = new XtBodyRow
        {
            Kind = XtBodyKind.Solid,
            Regions = new XtRange { Offset = 0, Count = 1 },
        };
        Assert.Throws<XtFormatException>(builder.FinalizeModel);
    }

    [Fact]
    public void CanonicalModelEncodesWithoutSourceNodeGraph()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "sch_test.sch_txt"), MinimalSchema);
        var catalog = XtSchemaCatalog.OpenDirectory(_directory);
        var builder = new XtBrepBuilder(new XtBrepCounts(Parts: 1, Bodies: 1));
        builder.Parts[0] = new XtPartRow { Kind = XtPartKind.Body, IsRoot = 1, Body = 0, Assembly = -1, Name = -1, Next = -1, Previous = -1 };
        builder.Bodies[0] = new XtBodyRow
        {
            Kind = XtBodyKind.Empty,
            HighestNodeId = 1,
            ReferenceInstance = -1,
            Owner = -1,
            Child = -1,
            Surface = -1,
            Curve = -1,
            Point = -1,
            BoundarySurface = -1,
            BoundaryCurve = -1,
            BoundaryPoint = -1,
            BoundaryMesh = -1,
            BoundaryPolyline = -1,
        };
        var model = builder.FinalizeModel();

        var document = XtBrepConverter.Encode(catalog, model, "SCH_3000000_30000");
        var bytes = XtCodec.Write(catalog, document);
        var decoded = XtCodec.Read(catalog, bytes);

        Assert.Single(decoded.Nodes);
        Assert.Equal("BODY", decoded.Schema.GetNode(decoded.Nodes[0].Type).Name);
    }

    [Fact]
    public void BuilderRejectsCyclicAssemblyAndInvalidRationalSpline()
    {
        var assemblyBuilder = new XtBrepBuilder(new XtBrepCounts(Parts: 1, Assemblies: 1, Instances: 1));
        assemblyBuilder.Parts[0] = new XtPartRow { Kind = XtPartKind.Assembly, Body = -1, Assembly = 0, Name = -1, Next = -1, Previous = -1 };
        assemblyBuilder.Assemblies[0] = new XtAssemblyRow { Instances = new XtRange { Offset = 0, Count = 1 }, Name = -1, ReferenceInstance = -1, Next = -1, Previous = -1 };
        assemblyBuilder.Instances[0] = new XtInstanceRow { Owner = 0, Part = 0, Transform = -1, NextInPart = -1, PreviousInPart = -1, NextOfPart = -1, PreviousOfPart = -1 };
        Assert.Throws<XtFormatException>(assemblyBuilder.FinalizeModel);

        var splineBuilder = new XtBrepBuilder(new XtBrepCounts(Geometries: 1, ControlPoints: 1));
        splineBuilder.Geometries[0] = new XtGeometryRow
        {
            Kind = XtGeometryKind.BCurve,
            ControlPoints = new XtRange { Offset = 0, Count = 1 },
            Rational = 1,
            Basis = -1,
            Profile = -1,
            Spine = -1,
            Chart = -1,
            StartLimit = -1,
            EndLimit = -1,
            IntersectionData = -1,
            GeometricOwner = -1,
            OwnerKind = XtOwnerKind.None,
            Owner = -1,
            Next = -1,
            Previous = -1,
        };
        Assert.Throws<XtFormatException>(splineBuilder.FinalizeModel);
    }

    [Fact]
    public void BuilderRepresentsPersistentFrameSemantics()
    {
        var builder = new XtBrepBuilder(new XtBrepCounts(Geometries: 1, Frames: 1));
        builder.Geometries[0] = new XtGeometryRow
        {
            Kind = XtGeometryKind.Line,
            Basis = -1,
            Profile = -1,
            Spine = -1,
            Chart = -1,
            StartLimit = -1,
            EndLimit = -1,
            IntersectionData = -1,
            GeometricOwner = -1,
            OwnerKind = XtOwnerKind.None,
            Owner = -1,
            Next = -1,
            Previous = -1,
        };
        builder.Frames[0] = new XtFrameRow
        {
            Geometry = 0,
            GeometryKind = XtOwnerKind.Geometry,
            GeometryEntity = 0,
            Next = -1,
            Previous = -1,
            NextOnGeometry = -1,
            PreviousOnGeometry = -1,
            OwnerKind = XtOwnerKind.Geometry,
            Owner = 0,
            Sense = 1,
        };

        var model = builder.FinalizeModel();

        Assert.Equal(XtOwnerKind.Geometry, model.Frames[0].OwnerKind);
        Assert.Equal(0, model.Frames[0].Geometry);
    }


    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
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

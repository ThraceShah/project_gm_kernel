#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

var cases = new CorpusCaseSpec[]
{
    new(
        "body.sheet.circle.typical",
        "PK_BODY_create_sheet_circle",
        "Planar sheet bounded by a circular edge.",
        new[] { "body", "body/sheet", "body/sheet/circle", "parameters/typical" },
        CreateSheetCircle,
        null,
        null,
        "{\"radius\":2.0,\"basisSet\":null}",
        typedAsk: AssertSheet,
        typeCoverage: new[] { "body.type.sheet" }),
    new(
        "body.sheet.rectangle.typical",
        "PK_BODY_create_sheet_rectangle",
        "Planar rectangular sheet with positive dimensions.",
        new[] { "body", "body/sheet", "body/sheet/rectangle", "parameters/typical" },
        CreateSheetRectangle,
        null,
        null,
        "{\"x\":2.0,\"y\":3.0,\"basisSet\":null}",
        typedAsk: AssertSheet,
        typeCoverage: new[] { "body.type.sheet" }),
    new(
        "body.sheet.polygon.pentagon",
        "PK_BODY_create_sheet_polygon",
        "Planar five-sided polygon sheet with a finite radius.",
        new[] { "body", "body/sheet", "body/sheet/polygon", "parameters/n-sides" },
        CreateSheetPolygon,
        null,
        null,
        "{\"radius\":2.0,\"nSides\":5,\"basisSet\":null}",
        typedAsk: AssertSheet,
        typeCoverage: new[] { "body.type.sheet" }),
    new(
        "body.sheet.planar.rectangle",
        "PK_BODY_create_sheet_planar",
        "Planar sheet created from an explicit closed vector loop.",
        new[] { "body", "body/sheet", "body/sheet/planar", "topology/loop" },
        CreateSheetPlanar,
        null,
        null,
        "{\"vertices\":[[-2.0,-1.0,0.0],[2.0,-1.0,0.0],[2.0,1.0,0.0],[-2.0,1.0,0.0]],\"closed\":true}",
        typedAsk: AssertSheet,
        typeCoverage: new[] { "body.type.sheet", "topology.loop.outer", "topology.shell.open", "topology.face.outer-loop" }),
};

return ParasolidXtCorpusHost.RunGroup("sheet-bodies", cases, args);

static unsafe void AssertSheet(PK_BODY_t body)
{
    PK_BODY_type_t type;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_type(body, &type), "PK_BODY_ask_type sheet");
    if (type != PK_BODY_type_sheet_c)
        throw new InvalidOperationException("expected sheet body, got " + type);
}

static unsafe PK_BODY_t CreateSheetCircle()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_circle(2.0, null, &body), "PK_BODY_create_sheet_circle");
    return body;
}

static unsafe PK_BODY_t CreateSheetRectangle()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(2.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle");
    return body;
}

static unsafe PK_BODY_t CreateSheetPolygon()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_polygon(2.0, 5, null, &body), "PK_BODY_create_sheet_polygon");
    return body;
}

static unsafe PK_BODY_t CreateSheetPlanar()
{
    var vectors = stackalloc PK_VECTOR_t[4]
    {
        new PK_VECTOR_t(-2.0, -1.0, 0.0),
        new PK_VECTOR_t(2.0, -1.0, 0.0),
        new PK_VECTOR_t(2.0, 1.0, 0.0),
        new PK_VECTOR_t(-2.0, 1.0, 0.0),
    };
    var options = new PK_BODY_create_sheet_planar_o_t();
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_planar(4, vectors, &options, &body), "PK_BODY_create_sheet_planar");
    return body;
}

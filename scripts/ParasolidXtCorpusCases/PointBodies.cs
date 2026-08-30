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
        "body.minimum.point",
        "PK_POINT_make_minimum_body + PK_POINT_create",
        "Minimum body whose single vertex is attached to a constructed point.",
        new[] { "body", "body/minimum", "point", "topology/vertex", "producer" },
        CreateMinimumPointBody,
        new CorpusBodyCounts(1, 1, 0, 0, 1),
        null,
        "{\"point\":[1.25,-0.5,2.0],\"operation\":\"make-minimum-body\"}",
        typedAsk: AssertMinimum,
        typeCoverage: new[] { "body.type.minimum", "topology.vertex.isolated" }),
};

return ParasolidXtCorpusHost.RunGroup("point-bodies", cases, args);

static unsafe void AssertMinimum(PK_BODY_t body)
{
    PK_BODY_type_t type;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_type(body, &type), "PK_BODY_ask_type minimum");
    if (type != PK_BODY_type_minimum_c)
        throw new InvalidOperationException("expected minimum body, got " + type);
}

static unsafe PK_BODY_t CreateMinimumPointBody()
{
    var standardForm = new PK_POINT_sf_t(new PK_VECTOR_t(1.25, -0.5, 2.0));
    PK_POINT_t point;
    ParasolidXtCorpusHost.Check(PK_POINT_create(&standardForm, &point), "PK_POINT_create minimum body source");

    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_POINT_make_minimum_body(point, &body), "PK_POINT_make_minimum_body");
    return body;
}

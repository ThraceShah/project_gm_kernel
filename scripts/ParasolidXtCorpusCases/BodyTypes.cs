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
        "body.acorn.two-points",
        "PK_POINT_make_minimum_body + PK_REGION_combine_bodies",
        "Acorn body formed by combining two disconnected minimum bodies.",
        new[] { "body", "body/acorn", "body/minimum", "topology", "topology/acorn", "topology/vertex" },
        CreateAcorn,
        new CorpusBodyCounts(1, 2, 0, 0, 2),
        null,
        "{\"sources\":[{\"point\":[0.0,0.0,0.0]},{\"point\":[5.0,0.0,0.0]}],\"operation\":\"PK_REGION_combine_bodies\",\"expectedBodyType\":\"acorn\"}",
        typeCoverage: new[] { "body.type.acorn", "topology.acorn", "topology.vertex.isolated" }),
    new(
        "body.compound.two-solids",
        "PK_BODY_make_compound",
        "Compound body containing two independently receivable solid children.",
        new[] { "body", "body/compound", "topology", "compound/children" },
        CreateCompound,
        parameters: "{\"sources\":[\"solid-block\",\"solid-sphere\"],\"expectedChildren\":2}",
        typedAsk: AssertCompound,
        typeCoverage: new[] { "body.type.compound" },
        compound: true),
};

return ParasolidXtCorpusHost.RunGroup("body-types", cases, args);

static unsafe PK_BODY_t CreateAcorn()
{
    var first = CreateMinimumBody(new PK_VECTOR_t(0.0, 0.0, 0.0));
    var second = CreateMinimumBody(new PK_VECTOR_t(5.0, 0.0, 0.0));

    int regionCount;
    PK_REGION_t* regions;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_regions(first, &regionCount, &regions), "PK_BODY_ask_regions acorn target");
    PK_REGION_t targetRegion;
    try
    {
        if (regionCount != 1)
            throw new InvalidOperationException("minimum target has unexpected region count: " + regionCount);
        targetRegion = regions[0];
    }
    finally
    {
        if (regions is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(regions), "PK_MEMORY_free acorn regions");
    }

    ParasolidXtCorpusHost.Check(PK_REGION_combine_bodies(targetRegion, second), "PK_REGION_combine_bodies acorn");

    PK_BODY_type_t bodyType;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_type(first, &bodyType), "PK_BODY_ask_type acorn");
    if (bodyType != PK_BODY_type_acorn_c)
        throw new InvalidOperationException("combined minimum bodies have body type " + bodyType + ", expected acorn");

    PK_SHELL_t* shells;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_shells(first, &regionCount, &shells), "PK_BODY_ask_shells acorn");
    try
    {
        if (regionCount != 2)
            throw new InvalidOperationException("acorn has unexpected shell count: " + regionCount);
        for (var i = 0; i < regionCount; i++)
        {
            PK_VERTEX_t vertex;
            ParasolidXtCorpusHost.Check(PK_SHELL_ask_acorn_vertex(shells[i], &vertex), "PK_SHELL_ask_acorn_vertex");
            if (vertex <= 0)
                throw new InvalidOperationException("acorn shell has no vertex");
        }
    }
    finally
    {
        if (shells is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(shells), "PK_MEMORY_free acorn shells");
    }

    return first;
}

static unsafe PK_BODY_t CreateMinimumBody(PK_VECTOR_t position)
{
    var pointForm = new PK_POINT_sf_t(position);
    PK_POINT_t point;
    ParasolidXtCorpusHost.Check(PK_POINT_create(&pointForm, &point), "PK_POINT_create acorn source");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_POINT_make_minimum_body(point, &body), "PK_POINT_make_minimum_body acorn source");
    return body;
}

static unsafe PK_BODY_t CreateCompound()
{
    PK_BODY_t block;
    PK_BODY_t sphere;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &block), "PK_BODY_create_solid_block compound");
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_sphere(1.5, null, &sphere), "PK_BODY_create_solid_sphere compound");
    var bodies = stackalloc PK_BODY_t[2] { block, sphere };
    var options = new PK_BODY_make_compound_o_t();
    PK_BODY_t compound;
    ParasolidXtCorpusHost.Check(PK_BODY_make_compound(2, bodies, &options, &compound), "PK_BODY_make_compound");
    return compound;
}

static unsafe void AssertCompound(PK_BODY_t body)
{
    PK_BODY_type_t bodyType;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_type(body, &bodyType), "PK_BODY_ask_type compound");
    if (bodyType != PK_BODY_type_compound_c)
        throw new InvalidOperationException("compound producer returned body type " + bodyType);
    var options = new PK_BODY_ask_children_o_t();
    int childCount;
    PK_BODY_t* children;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_children(body, &options, &childCount, &children), "PK_BODY_ask_children compound");
    try
    {
        if (childCount != 2)
            throw new InvalidOperationException("compound has unexpected child count " + childCount);
    }
    finally
    {
        if (children is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(children), "PK_MEMORY_free compound children");
    }
}

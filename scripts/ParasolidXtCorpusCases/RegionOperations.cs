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
        "region.make-void",
        "PK_REGION_make_void + PK_BODY_create_solid_block",
        "Change the type of a valid solid block region to void.",
        new[] { "region", "region/type", "void", "mutator" },
        MakeVoid,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"regionIndex\":0,\"type\":\"void\"}",
        typeCoverage: new[] { "topology.region.void", "topology.shell.closed" }),
    new(
        "region.make-solid",
        "PK_REGION_make_solid + PK_BODY_create_solid_block",
        "Reassert the solid type on a valid solid block region.",
        new[] { "region", "region/type", "solid", "mutator", "idempotent" },
        MakeSolid,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"regionIndex\":0,\"type\":\"solid\"}",
        typeCoverage: new[] { "topology.region.solid", "topology.shell.closed" }),
};

return ParasolidXtCorpusHost.RunGroup("region-operations", cases, args);


static unsafe PK_BODY_t MakeVoid()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(region void)");
    ParasolidXtCorpusHost.Check(PK_REGION_make_void(FirstRegion(body)), "PK_REGION_make_void");
    return body;
}

static unsafe PK_BODY_t MakeSolid()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(region solid)");
    ParasolidXtCorpusHost.Check(PK_REGION_make_solid(FirstRegion(body)), "PK_REGION_make_solid");
    return body;
}

static unsafe PK_REGION_t FirstRegion(PK_BODY_t body)
{
    int count;
    PK_REGION_t* regions;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_regions(body, &count, &regions), "PK_BODY_ask_regions(region operations)");
    try
    {
        if (count == 0)
            throw new InvalidOperationException("body has no region");
        return regions[0];
    }
    finally
    {
        if (regions is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(regions), "PK_MEMORY_free(region operations regions)");
    }
}

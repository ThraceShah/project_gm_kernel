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
        "template.body.block",
        "PK_BODY_create_solid_block",
        "minimal block template for new case-groups",
        new[] { "body/solid", "topology/box" },
        CreateBlock,
        new CorpusBodyCounts(2, 2, 6, 12, 8)),
};

return ParasolidXtCorpusHost.RunGroup("template-smoke", cases, args);

static unsafe PK_BODY_t CreateBlock()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1, 2, 3, null, &body), "PK_BODY_create_solid_block");
    return body;
}

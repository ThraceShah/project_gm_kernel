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
        "user-field.body.single-slot",
        "PK_SESSION_start(user_field=1) + PK_ENTITY_set_user_field",
        "A one-slot integer user-field payload round-tripped on a body.",
        new[] { "user-field", "body", "single-slot", "boundary-length", "transmit" },
        CreateSingleSlot,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"sessionUserFieldLength\":1,\"value\":[31415],\"transmitUserFields\":true}",
        1,
        true,
        typeCoverage: new[] { "userfield.payload.single-slot", "userfield.owner.body" }),
};

return ParasolidXtCorpusHost.RunGroup("user-field-single", cases, args);

static unsafe PK_BODY_t CreateSingleSlot()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block single user field");
    int length;
    ParasolidXtCorpusHost.Check(PK_SESSION_ask_user_field_len(&length), "PK_SESSION_ask_user_field_len single");
    if (length != 1)
        throw new InvalidOperationException("expected one-slot user field session, got " + length);
    var values = stackalloc int[1] { 31415 };
    ParasolidXtCorpusHost.Check(PK_ENTITY_set_user_field(body, values), "PK_ENTITY_set_user_field single");
    var roundTrip = stackalloc int[1];
    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_user_field(body, roundTrip), "PK_ENTITY_ask_user_field single");
    if (roundTrip[0] != values[0])
        throw new InvalidOperationException("single-slot user field mismatch");
    return body;
}

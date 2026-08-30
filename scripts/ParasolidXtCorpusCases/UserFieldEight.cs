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
        "user-field.body.eight-slot",
        "PK_SESSION_start(user_field=8) + PK_ENTITY_set_user_field",
        "An eight-slot integer user-field payload round-tripped on a body.",
        new[] { "user-field", "body", "eight-slot", "boundary-length", "transmit" },
        CreateEightSlot,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"sessionUserFieldLength\":8,\"value\":[1,2,3,5,8,13,21,34],\"transmitUserFields\":true}",
        8,
        true,
        typeCoverage: new[] { "userfield.payload.eight-slot", "userfield.owner.body" }),
    new(
        "user-field.face.eight-slot",
        "PK_SESSION_start(user_field=8) + PK_ENTITY_set_user_field",
        "An eight-slot integer user-field payload round-tripped on a face owner.",
        new[] { "user-field", "face", "eight-slot", "boundary-length", "transmit" },
        CreateFaceEightSlot,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"sessionUserFieldLength\":8,\"owner\":\"face\",\"value\":[8,7,6,5,4,3,2,1],\"transmitUserFields\":true}",
        8,
        true,
        typeCoverage: new[] { "userfield.payload.eight-slot", "userfield.owner.face" }),
};

return ParasolidXtCorpusHost.RunGroup("user-field-eight", cases, args);

static unsafe PK_BODY_t CreateEightSlot()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block eight user field");
    int length;
    ParasolidXtCorpusHost.Check(PK_SESSION_ask_user_field_len(&length), "PK_SESSION_ask_user_field_len eight");
    if (length != 8)
        throw new InvalidOperationException("expected eight-slot user field session, got " + length);
    var values = stackalloc int[8] { 1, 2, 3, 5, 8, 13, 21, 34 };
    ParasolidXtCorpusHost.Check(PK_ENTITY_set_user_field(body, values), "PK_ENTITY_set_user_field eight");
    var roundTrip = stackalloc int[8];
    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_user_field(body, roundTrip), "PK_ENTITY_ask_user_field eight");
    for (var i = 0; i < 8; i++)
        if (roundTrip[i] != values[i])
            throw new InvalidOperationException("eight-slot user field mismatch at slot " + i);
    return body;
}

static unsafe PK_BODY_t CreateFaceEightSlot()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block face eight user field");
    int count; PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &count, &faces), "PK_BODY_ask_faces face eight user field");
    try
    {
        if (count < 1) throw new InvalidOperationException("body has no face for eight-slot user field");
        var values = stackalloc int[8] { 8, 7, 6, 5, 4, 3, 2, 1 };
        ParasolidXtCorpusHost.Check(PK_ENTITY_set_user_field(faces[0], values), "PK_ENTITY_set_user_field face eight");
        var roundTrip = stackalloc int[8];
        ParasolidXtCorpusHost.Check(PK_ENTITY_ask_user_field(faces[0], roundTrip), "PK_ENTITY_ask_user_field face eight");
        for (var i = 0; i < 8; i++) if (roundTrip[i] != values[i]) throw new InvalidOperationException("face eight-slot user field mismatch at slot " + i);
    }
    finally { if (faces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free face eight faces"); }
    return body;
}

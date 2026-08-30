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
        "group.body.faces-lifecycle",
        "PK_GROUP_create_from_entities + PK_GROUP_add_entities + PK_GROUP_set_entity_label + PK_GROUP_remove_entities",
        "A face group created on a solid body, extended, labelled and reduced before transmit.",
        new[] { "group", "body", "face", "create", "add", "label", "remove", "persistent-state" },
        CreateFaceGroupBody,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"owner\":\"body.solid.block.typical\",\"entityClass\":\"face\",\"initialMembers\":1,\"addedMembers\":1,\"removedMembers\":1,\"label\":101}"),
    new(
        "group.body.faces-options",
        "PK_GROUP_create_from_entities_2 + PK_GROUP_set_entity_label",
        "An option-bearing face group with inclusive membership policy.",
        new[] { "group", "body", "face", "create", "options", "label", "persistent-state" },
        CreateFaceGroupBodyWithOptions,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"owner\":\"body.solid.block.typical\",\"entityClass\":\"face\",\"membership\":\"inclusive\",\"label\":202}"),
};

return ParasolidXtCorpusHost.RunGroup("groups", cases, args);

static unsafe PK_BODY_t CreateFaceGroupBody()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block group");
    int faceCount;
    PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces group");
    try
    {
        if (faceCount < 2)
            throw new InvalidOperationException("expected at least two faces, got " + faceCount);
        var members = stackalloc PK_ENTITY_t[1] { faces[0] };
        PK_GROUP_t group;
        ParasolidXtCorpusHost.Check(PK_GROUP_create_from_entities(body, PK_CLASS_face, 1, members, &group), "PK_GROUP_create_from_entities");
        var second = stackalloc PK_ENTITY_t[1] { faces[1] };
        ParasolidXtCorpusHost.Check(PK_GROUP_add_entities(group, 1, second), "PK_GROUP_add_entities");
        ParasolidXtCorpusHost.Check(PK_GROUP_set_entity_label(group, faces[0], 101), "PK_GROUP_set_entity_label");
        int removed;
        ParasolidXtCorpusHost.Check(PK_GROUP_remove_entities(group, 1, second, &removed), "PK_GROUP_remove_entities");
        if (removed != 1)
            throw new InvalidOperationException("expected one group member removed, got " + removed);
        return body;
    }
    finally
    {
        if (faces is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free group faces");
    }
}

static unsafe PK_BODY_t CreateFaceGroupBodyWithOptions()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block group options");
    int faceCount;
    PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces group options");
    try
    {
        var members = stackalloc PK_ENTITY_t[1] { faces[0] };
        var options = new PK_GROUP_create_from_ents_o_t
        {
            membership = PK_GROUP_membership_inclusive_c,
            split_empty = PK_GROUP_split_never_c,
            merge_empty = PK_GROUP_merge_empty_no_c,
            merge_populated = PK_GROUP_merge_empty_no_c,
            delete_dependants = PK_GROUP_dependants_keep_c,
        };
        PK_GROUP_t group;
        ParasolidXtCorpusHost.Check(PK_GROUP_create_from_entities_2(body, PK_CLASS_face, 1, members, &options, &group), "PK_GROUP_create_from_entities_2");
        ParasolidXtCorpusHost.Check(PK_GROUP_set_entity_label(group, faces[0], 202), "PK_GROUP_set_entity_label options");
        return body;
    }
    finally
    {
        if (faces is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free group options faces");
    }
}

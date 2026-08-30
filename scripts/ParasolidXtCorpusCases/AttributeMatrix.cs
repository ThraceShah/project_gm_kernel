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
        "attribute.face.named-integer",
        "PK_ATTDEF_create_2 + PK_ATTRIB_set_named_ints",
        "Named integer field attached to a face owner.",
        new[] { "attribute", "named-field", "field/integer", "owner/face" },
        CreateNamedFaceInteger,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        parameters: "{\"owner\":\"face\",\"field\":\"code\",\"value\":71}",
        typeCoverage: new[] { "attribute.named.owner.face" }),
    new(
        "attribute.body.multiple-definitions",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty",
        "Two independent attribute definitions and values attached to one body.",
        new[] { "attribute", "multiple-definitions", "owner/body" },
        CreateMultipleBodyDefinitions,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        parameters: "{\"owner\":\"body\",\"definitions\":[\"integer\",\"real\"]}",
        typeCoverage: new[] { "attribute.definition.multiple" }),
};

return ParasolidXtCorpusHost.RunGroup("attribute-matrix", cases, args);

static unsafe PK_BODY_t CreateNamedFaceInteger()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block named face");
    PK_FACE_t face;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_first_face(body, &face), "PK_BODY_ask_first_face named face");
    var ownerTypes = stackalloc PK_CLASS_t[1] { PK_CLASS_face };
    var fieldTypes = stackalloc PK_ATTRIB_field_t[1] { PK_ATTRIB_field_integer_c };
    var definitionName = stackalloc byte[64];
    var fieldName = stackalloc byte[32];
    CopyAscii(definitionName, "corpus_named_face_integer");
    CopyAscii(fieldName, "code");
    var names = stackalloc byte*[1]; names[0] = fieldName;
    var fieldNames = new PK_field_names_t(names);
    var definition = new PK_ATTDEF_sf_2_t(definitionName, PK_ATTDEF_class_zero1_c, 1, ownerTypes, 1, fieldTypes, PK_LOGICAL_false, fieldNames, null);
    PK_ATTDEF_t attdef;
    ParasolidXtCorpusHost.Check(PK_ATTDEF_create_2(&definition, &attdef), "PK_ATTDEF_create_2 named face");
    PK_ATTRIB_t attrib;
    ParasolidXtCorpusHost.Check(PK_ATTRIB_create_empty(face, attdef, &attrib), "PK_ATTRIB_create_empty named face");
    var value = stackalloc int[1] { 71 };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_named_ints(attrib, "code", 1, value), "PK_ATTRIB_set_named_ints named face");
    int count; int* readBack;
    ParasolidXtCorpusHost.Check(PK_ATTRIB_ask_named_ints(attrib, "code", &count, &readBack), "PK_ATTRIB_ask_named_ints named face");
    try
    {
        if (count != 1 || readBack is null || readBack[0] != value[0]) throw new InvalidOperationException("named face integer mismatch");
    }
    finally { if (readBack is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(readBack), "PK_MEMORY_free named face integer"); }
    return body;
}

static unsafe PK_BODY_t CreateMultipleBodyDefinitions()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block multiple definitions");
    var integerOwner = stackalloc PK_CLASS_t[1] { PK_CLASS_body };
    var integerField = stackalloc PK_ATTRIB_field_t[1] { PK_ATTRIB_field_integer_c };
    var integerName = stackalloc byte[64]; CopyAscii(integerName, "corpus_multiple_integer");
    var integerDef = new PK_ATTDEF_sf_t(integerName, PK_ATTDEF_class_zero1_c, 1, integerOwner, 1, integerField);
    PK_ATTDEF_t integerAttdef; ParasolidXtCorpusHost.Check(PK_ATTDEF_create(&integerDef, &integerAttdef), "PK_ATTDEF_create multiple integer");
    PK_ATTRIB_t integerAttrib; ParasolidXtCorpusHost.Check(PK_ATTRIB_create_empty(body, integerAttdef, &integerAttrib), "PK_ATTRIB_create_empty multiple integer");
    var integer = stackalloc int[1] { 7 }; ParasolidXtCorpusHost.Check(PK_ATTRIB_set_ints(integerAttrib, 0, 1, integer), "PK_ATTRIB_set_ints multiple integer");

    var realField = stackalloc PK_ATTRIB_field_t[1] { PK_ATTRIB_field_real_c };
    var realName = stackalloc byte[64]; CopyAscii(realName, "corpus_multiple_real");
    var realDef = new PK_ATTDEF_sf_t(realName, PK_ATTDEF_class_zero1_c, 1, integerOwner, 1, realField);
    PK_ATTDEF_t realAttdef; ParasolidXtCorpusHost.Check(PK_ATTDEF_create(&realDef, &realAttdef), "PK_ATTDEF_create multiple real");
    PK_ATTRIB_t realAttrib; ParasolidXtCorpusHost.Check(PK_ATTRIB_create_empty(body, realAttdef, &realAttrib), "PK_ATTRIB_create_empty multiple real");
    var real = stackalloc double[1] { 2.5 }; ParasolidXtCorpusHost.Check(PK_ATTRIB_set_doubles(realAttrib, 0, 1, real), "PK_ATTRIB_set_doubles multiple real");
    return body;
}


static unsafe void CopyAscii(byte* destination, string value)
{
    for (var i = 0; i < value.Length; i++) destination[i] = (byte)value[i];
}

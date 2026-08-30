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
        "attribute.body.integer",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_ints",
        "A custom integer attribute attached to a solid body.",
        new[] { "attribute", "attdef", "custom", "owner/body", "field/integer", "action/transmit" },
        CreateBodyIntegerAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_body_integer\",\"ownerTypes\":[\"body\"],\"fields\":[\"integer\"]},\"value\":42}",
        typeCoverage: new[] { "attribute.field.integer", "attribute.owner.body" }),
    new(
        "attribute.body.real",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_doubles",
        "A custom real attribute attached to a solid body.",
        new[] { "attribute", "attdef", "custom", "owner/body", "field/real", "action/transmit" },
        CreateBodyRealAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_body_real\",\"ownerTypes\":[\"body\"],\"fields\":[\"real\"]},\"value\":3.141592653589793}",
        typeCoverage: new[] { "attribute.field.real", "attribute.owner.body" }),
    new(
        "attribute.body.string",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_string",
        "A custom string attribute attached to a solid body.",
        new[] { "attribute", "attdef", "custom", "owner/body", "field/string", "action/transmit" },
        CreateBodyStringAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_body_string\",\"ownerTypes\":[\"body\"],\"fields\":[\"string\"]},\"value\":\"parasolid-corpus\"}",
        typeCoverage: new[] { "attribute.field.string", "attribute.owner.body" }),
    new(
        "attribute.body.vector",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_vectors",
        "A custom vector attribute attached to a solid body.",
        new[] { "attribute", "attdef", "custom", "owner/body", "field/vector", "action/transmit" },
        CreateBodyVectorAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_body_vector\",\"ownerTypes\":[\"body\"],\"fields\":[\"vector\"]},\"value\":[1.0,2.0,3.0]}",
        typeCoverage: new[] { "attribute.field.vector", "attribute.owner.body" }),
    new(
        "attribute.body.delete",
        "PK_ATTRIB_create_empty + PK_ATTRIB_set_ints + PK_ENTITY_delete_attribs",
        "A body attribute is created, populated and deleted before transmit.",
        new[] { "attribute", "owner/body", "field/integer", "delete", "mutator" },
        CreateAndDeleteBodyAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":\"corpus_body_delete\",\"field\":\"integer\",\"operation\":\"create-set-delete\"}",
        typeCoverage: new[] { "attribute.owner.body" }),
    new(
        "attribute.part.delete",
        "PK_ATTRIB_create_empty + PK_ATTRIB_set_ints + PK_PART_delete_attribs",
        "A part-level attribute is deleted through the part attribute API.",
        new[] { "attribute", "owner/part", "field/integer", "delete", "mutator" },
        CreateAndDeletePartAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":\"corpus_part_delete\",\"field\":\"integer\",\"operation\":\"create-set-delete\"}",
        typeCoverage: new[] { "attribute.owner.part" }),
    new(
        "attribute.body.axis",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_axes",
        "A custom axis attribute attached to a solid body.",
        new[] { "attribute", "attdef", "custom", "owner/body", "field/axis", "action/transmit" },
        CreateBodyAxisAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_body_axis\",\"ownerTypes\":[\"body\"],\"fields\":[\"axis\"]},\"value\":{\"location\":[0.0,0.0,0.0],\"direction\":[0.0,0.0,1.0]}}",
        typeCoverage: new[] { "attribute.field.axis", "attribute.owner.body" }),
    new(
        "attribute.body.coordinate",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_vectors",
        "A coordinate field stored as one vector on a body owner.",
        new[] { "attribute", "attdef", "custom", "owner/body", "field/coordinate", "action/transmit" },
        CreateBodyCoordinateAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_body_coordinate\",\"fields\":[\"coordinate\"]},\"value\":[1.0,2.0,3.0]}",
        typeCoverage: new[] { "attribute.field.coordinate", "attribute.owner.body" }),
    new(
        "attribute.body.direction",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_vectors",
        "A direction field stored as one vector on a body owner.",
        new[] { "attribute", "attdef", "custom", "owner/body", "field/direction", "action/transmit" },
        CreateBodyDirectionAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_body_direction\",\"fields\":[\"direction\"]},\"value\":[0.0,0.0,1.0]}",
        typeCoverage: new[] { "attribute.field.direction", "attribute.owner.body" }),
    new(
        "attribute.body.pointer",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_pointers",
        "An opaque pointer field referencing a stable native address.",
        new[] { "attribute", "attdef", "custom", "owner/body", "field/pointer", "action/transmit" },
        CreateBodyPointerAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_body_pointer\",\"fields\":[\"pointer\"]},\"value\":\"body-handle\"}",
        typeCoverage: new[] { "attribute.field.pointer", "attribute.owner.body" }),
    new(
        "attribute.body.ustring",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_ustring",
        "A Unicode string field with non-ASCII payload.",
        new[] { "attribute", "attdef", "custom", "owner/body", "field/ustring", "action/transmit" },
        CreateBodyUStringAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_body_ustring\",\"fields\":[\"ustring\"]},\"value\":\"几何\"}",
        typeCoverage: new[] { "attribute.field.ustring" }),
    new(
        "attribute.body.multi-field",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_ints + PK_ATTRIB_set_doubles",
        "A single body attribute definition containing independent integer and real fields.",
        new[] { "attribute", "attdef", "custom", "owner/body", "multi-field", "field/integer", "field/real" },
        CreateBodyMultiFieldAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_body_multi\",\"fields\":[\"integer\",\"real\"]},\"values\":[42,3.5]}",
        typeCoverage: new[] { "attribute.field.integer", "attribute.field.real", "attribute.definition.multi-field" }),
    new(
        "attribute.face.integer",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_ints",
        "Integer attribute owned by a face.",
        new[] { "attribute", "owner/face", "field/integer", "action/transmit" },
        CreateFaceIntegerAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"owner\":\"face\",\"field\":\"integer\",\"value\":17}",
        typeCoverage: new[] { "attribute.owner.face" }),
    new(
        "attribute.edge.integer",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_ints",
        "Integer attribute owned by an edge.",
        new[] { "attribute", "owner/edge", "field/integer", "action/transmit" },
        CreateEdgeIntegerAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"owner\":\"edge\",\"field\":\"integer\",\"value\":19}",
        typeCoverage: new[] { "attribute.owner.edge" }),
    new(
        "attribute.loop.integer",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_ints",
        "Integer attribute owned by a loop.",
        new[] { "attribute", "owner/loop", "field/integer", "action/transmit" },
        CreateLoopIntegerAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"owner\":\"loop\",\"field\":\"integer\",\"value\":23}",
        typeCoverage: new[] { "attribute.owner.loop" }),
    new(
        "attribute.vertex.integer",
        "PK_ATTDEF_create + PK_ATTRIB_create_empty + PK_ATTRIB_set_ints",
        "Integer attribute owned by a vertex.",
        new[] { "attribute", "owner/vertex", "field/integer", "action/transmit" },
        CreateVertexIntegerAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"owner\":\"vertex\",\"field\":\"integer\",\"value\":29}",
        typeCoverage: new[] { "attribute.owner.vertex" }),
};

return ParasolidXtCorpusHost.RunGroup("attributes", cases, args);

static unsafe PK_BODY_t CreateBodyIntegerAttribute()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body),
        "PK_BODY_create_solid_block(attribute.body.integer)");

    var ownerTypes = stackalloc PK_CLASS_t[1] { PK_CLASS_body };
    var fieldTypes = stackalloc PK_ATTRIB_field_t[1] { PK_ATTRIB_field_integer_c };
    var name = stackalloc byte[32];
    var text = "corpus_body_integer";
    for (var i = 0; i < text.Length; i++)
        name[i] = (byte)text[i];

    var definition = new PK_ATTDEF_sf_t(
        name,
        PK_ATTDEF_class_zero1_c,
        1,
        ownerTypes,
        1,
        fieldTypes);
    PK_ATTDEF_t attdef;
    ParasolidXtCorpusHost.Check(PK_ATTDEF_create(&definition, &attdef), "PK_ATTDEF_create");

    PK_LOGICAL_t mayOwn;
    ParasolidXtCorpusHost.Check(PK_ENTITY_may_own_attdef(body, attdef, &mayOwn), "PK_ENTITY_may_own_attdef");
    if ((int)mayOwn != 1)
        throw new InvalidOperationException("body is not a legal owner for corpus_body_integer");

    PK_ATTRIB_t attrib;
    ParasolidXtCorpusHost.Check(PK_ATTRIB_create_empty(body, attdef, &attrib), "PK_ATTRIB_create_empty");
    var value = stackalloc int[1] { 42 };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_ints(attrib, 0, 1, value), "PK_ATTRIB_set_ints");
    return body;
}

static unsafe PK_ATTRIB_t CreateBodyAttribute(PK_ATTRIB_field_t field, string definitionName, out PK_BODY_t body)
{
    PK_BODY_t createdBody;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &createdBody),
        "PK_BODY_create_solid_block(attribute " + definitionName + ")");
    body = createdBody;
    var ownerTypes = stackalloc PK_CLASS_t[1] { PK_CLASS_body };
    var fieldTypes = stackalloc PK_ATTRIB_field_t[1] { field };
    var name = stackalloc byte[64];
    for (var i = 0; i < definitionName.Length; i++)
        name[i] = (byte)definitionName[i];
    var definition = new PK_ATTDEF_sf_t(name, PK_ATTDEF_class_zero1_c, 1, ownerTypes, 1, fieldTypes);
    PK_ATTDEF_t attdef;
    ParasolidXtCorpusHost.Check(PK_ATTDEF_create(&definition, &attdef), "PK_ATTDEF_create " + definitionName);
    PK_LOGICAL_t mayOwn;
    ParasolidXtCorpusHost.Check(PK_ENTITY_may_own_attdef(body, attdef, &mayOwn), "PK_ENTITY_may_own_attdef " + definitionName);
    if ((int)mayOwn != 1)
        throw new InvalidOperationException("body is not a legal owner for " + definitionName);
    PK_ATTRIB_t attrib;
    ParasolidXtCorpusHost.Check(PK_ATTRIB_create_empty(body, attdef, &attrib), "PK_ATTRIB_create_empty " + definitionName);
    return attrib;
}

static unsafe PK_BODY_t CreateBodyRealAttribute()
{
    var attrib = CreateBodyAttribute(PK_ATTRIB_field_real_c, "corpus_body_real", out var body);
    var value = stackalloc double[1] { 3.141592653589793 };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_doubles(attrib, 0, 1, value), "PK_ATTRIB_set_doubles");
    return body;
}

static unsafe PK_BODY_t CreateBodyStringAttribute()
{
    var attrib = CreateBodyAttribute(PK_ATTRIB_field_string_c, "corpus_body_string", out var body);
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_string(attrib, 0, "parasolid-corpus"), "PK_ATTRIB_set_string");
    return body;
}

static unsafe PK_BODY_t CreateBodyVectorAttribute()
{
    var attrib = CreateBodyAttribute(PK_ATTRIB_field_vector_c, "corpus_body_vector", out var body);
    var value = stackalloc PK_VECTOR_t[1] { new PK_VECTOR_t(1.0, 2.0, 3.0) };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_vectors(attrib, 0, 1, value), "PK_ATTRIB_set_vectors");
    return body;
}

static unsafe PK_BODY_t CreateAndDeleteBodyAttribute()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(attribute delete)");
    var ownerTypes = stackalloc PK_CLASS_t[1] { PK_CLASS_body };
    var fieldTypes = stackalloc PK_ATTRIB_field_t[1] { PK_ATTRIB_field_integer_c };
    var name = stackalloc byte[64];
    var text = "corpus_body_delete";
    for (var i = 0; i < text.Length; i++)
        name[i] = (byte)text[i];
    var definition = new PK_ATTDEF_sf_t(name, PK_ATTDEF_class_zero1_c, 1, ownerTypes, 1, fieldTypes);
    PK_ATTDEF_t attdef;
    ParasolidXtCorpusHost.Check(PK_ATTDEF_create(&definition, &attdef), "PK_ATTDEF_create delete");
    PK_ATTRIB_t attrib;
    ParasolidXtCorpusHost.Check(PK_ATTRIB_create_empty(body, attdef, &attrib), "PK_ATTRIB_create_empty delete");
    var value = stackalloc int[1] { 7 };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_ints(attrib, 0, 1, value), "PK_ATTRIB_set_ints delete");
    int deleted;
    ParasolidXtCorpusHost.Check(PK_ENTITY_delete_attribs(body, attdef, &deleted), "PK_ENTITY_delete_attribs");
    if (deleted != 1)
        throw new InvalidOperationException("expected one attribute deleted, got " + deleted);
    return body;
}

static unsafe PK_BODY_t CreateAndDeletePartAttribute()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(part attribute delete)");
    var ownerTypes = stackalloc PK_CLASS_t[1] { PK_CLASS_body };
    var fieldTypes = stackalloc PK_ATTRIB_field_t[1] { PK_ATTRIB_field_integer_c };
    var name = stackalloc byte[64];
    var text = "corpus_part_delete";
    for (var i = 0; i < text.Length; i++)
        name[i] = (byte)text[i];
    var definition = new PK_ATTDEF_sf_t(name, PK_ATTDEF_class_zero1_c, 1, ownerTypes, 1, fieldTypes);
    PK_ATTDEF_t attdef;
    ParasolidXtCorpusHost.Check(PK_ATTDEF_create(&definition, &attdef), "PK_ATTDEF_create part delete");
    PK_ATTRIB_t attrib;
    ParasolidXtCorpusHost.Check(PK_ATTRIB_create_empty(body, attdef, &attrib), "PK_ATTRIB_create_empty part delete");
    var value = stackalloc int[1] { 11 };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_ints(attrib, 0, 1, value), "PK_ATTRIB_set_ints part delete");
    var definitions = stackalloc PK_ATTDEF_t[1] { attdef };
    var options = new PK_PART_delete_attribs_o_t(PK_CLASS_part);
    int deleted;
    ParasolidXtCorpusHost.Check(PK_PART_delete_attribs(body, 1, definitions, &options, &deleted), "PK_PART_delete_attribs");
    if (deleted != 1)
        throw new InvalidOperationException("expected one part attribute deleted, got " + deleted);
    return body;
}

static unsafe PK_BODY_t CreateBodyAxisAttribute()
{
    var attrib = CreateBodyAttribute(PK_ATTRIB_field_axis_c, "corpus_body_axis", out var body);
    var axes = stackalloc PK_AXIS1_sf_t[1]
    {
        new PK_AXIS1_sf_t(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0))
    };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_axes(attrib, 0, 1, axes), "PK_ATTRIB_set_axes");
    return body;
}

static unsafe PK_BODY_t CreateBodyCoordinateAttribute()
{
    var attrib = CreateBodyAttribute(PK_ATTRIB_field_coordinate_c, "corpus_body_coordinate", out var body);
    var value = stackalloc PK_VECTOR_t[1] { new PK_VECTOR_t(1.0, 2.0, 3.0) };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_vectors(attrib, 0, 1, value), "PK_ATTRIB_set_vectors coordinate");
    return body;
}

static unsafe PK_BODY_t CreateBodyDirectionAttribute()
{
    var attrib = CreateBodyAttribute(PK_ATTRIB_field_direction_c, "corpus_body_direction", out var body);
    var value = stackalloc PK_VECTOR_t[1] { new PK_VECTOR_t(0.0, 0.0, 1.0) };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_vectors(attrib, 0, 1, value), "PK_ATTRIB_set_vectors direction");
    return body;
}

static unsafe PK_BODY_t CreateBodyPointerAttribute()
{
    var attrib = CreateBodyAttribute(PK_ATTRIB_field_pointer_c, "corpus_body_pointer", out var body);
    var marker = 0x4B45524E;
    var value = stackalloc PK_POINTER_t[1] { (PK_POINTER_t)(nint)(&marker) };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_pointers(attrib, 0, 1, value), "PK_ATTRIB_set_pointers pointer");
    return body;
}

static unsafe PK_BODY_t CreateBodyUStringAttribute()
{
    var attrib = CreateBodyAttribute(PK_ATTRIB_field_ustring_c, "corpus_body_ustring", out var body);
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_ustring(attrib, 0, "几何"), "PK_ATTRIB_set_ustring");
    return body;
}

static unsafe PK_BODY_t CreateBodyMultiFieldAttribute()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block multi-field attribute");
    var ownerTypes = stackalloc PK_CLASS_t[1] { PK_CLASS_body };
    var fieldTypes = stackalloc PK_ATTRIB_field_t[2] { PK_ATTRIB_field_integer_c, PK_ATTRIB_field_real_c };
    var name = stackalloc byte[64];
    var text = "corpus_body_multi";
    for (var i = 0; i < text.Length; i++) name[i] = (byte)text[i];
    var definition = new PK_ATTDEF_sf_t(name, PK_ATTDEF_class_zero1_c, 1, ownerTypes, 2, fieldTypes);
    PK_ATTDEF_t attdef;
    ParasolidXtCorpusHost.Check(PK_ATTDEF_create(&definition, &attdef), "PK_ATTDEF_create multi-field");
    PK_ATTRIB_t attrib;
    ParasolidXtCorpusHost.Check(PK_ATTRIB_create_empty(body, attdef, &attrib), "PK_ATTRIB_create_empty multi-field");
    var integer = stackalloc int[1] { 42 };
    var real = stackalloc double[1] { 3.5 };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_ints(attrib, 0, 1, integer), "PK_ATTRIB_set_ints multi-field");
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_doubles(attrib, 1, 1, real), "PK_ATTRIB_set_doubles multi-field");
    return body;
}

static unsafe PK_BODY_t CreateFaceIntegerAttribute() => CreateTopologyOwnerIntegerAttribute(PK_CLASS_face, "face", 17);
static unsafe PK_BODY_t CreateEdgeIntegerAttribute() => CreateTopologyOwnerIntegerAttribute(PK_CLASS_edge, "edge", 19);
static unsafe PK_BODY_t CreateLoopIntegerAttribute() => CreateTopologyOwnerIntegerAttribute(PK_CLASS_loop, "loop", 23);
static unsafe PK_BODY_t CreateVertexIntegerAttribute() => CreateTopologyOwnerIntegerAttribute(PK_CLASS_vertex, "vertex", 29);

static unsafe PK_BODY_t CreateTopologyOwnerIntegerAttribute(PK_CLASS_t ownerClass, string ownerName, int value)
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block topology attribute");
    PK_ENTITY_t owner;
    if (ownerClass == PK_CLASS_face)
    {
        int count; PK_FACE_t* values;
        ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &count, &values), "PK_BODY_ask_faces attribute owner");
        try { owner = values[0]; } finally { ParasolidXtCorpusHost.Check(PK_MEMORY_free(values), "PK_MEMORY_free attribute faces"); }
    }
    else if (ownerClass == PK_CLASS_edge)
    {
        int count; PK_EDGE_t* values;
        ParasolidXtCorpusHost.Check(PK_BODY_ask_edges(body, &count, &values), "PK_BODY_ask_edges attribute owner");
        try { owner = values[0]; } finally { ParasolidXtCorpusHost.Check(PK_MEMORY_free(values), "PK_MEMORY_free attribute edges"); }
    }
    else if (ownerClass == PK_CLASS_loop)
    {
        int count; PK_LOOP_t* values;
        ParasolidXtCorpusHost.Check(PK_BODY_ask_loops(body, &count, &values), "PK_BODY_ask_loops attribute owner");
        try { owner = values[0]; } finally { ParasolidXtCorpusHost.Check(PK_MEMORY_free(values), "PK_MEMORY_free attribute loops"); }
    }
    else
    {
        int count; PK_VERTEX_t* values;
        ParasolidXtCorpusHost.Check(PK_BODY_ask_vertices(body, &count, &values), "PK_BODY_ask_vertices attribute owner");
        try { owner = values[0]; } finally { ParasolidXtCorpusHost.Check(PK_MEMORY_free(values), "PK_MEMORY_free attribute vertices"); }
    }

    var ownerTypes = stackalloc PK_CLASS_t[1] { ownerClass };
    var fieldTypes = stackalloc PK_ATTRIB_field_t[1] { PK_ATTRIB_field_integer_c };
    var name = stackalloc byte[64];
    var text = "corpus_" + ownerName + "_integer";
    for (var i = 0; i < text.Length; i++) name[i] = (byte)text[i];
    var definition = new PK_ATTDEF_sf_t(name, PK_ATTDEF_class_zero1_c, 1, ownerTypes, 1, fieldTypes);
    PK_ATTDEF_t attdef;
    ParasolidXtCorpusHost.Check(PK_ATTDEF_create(&definition, &attdef), "PK_ATTDEF_create topology owner");
    PK_ATTRIB_t attrib;
    ParasolidXtCorpusHost.Check(PK_ATTRIB_create_empty(owner, attdef, &attrib), "PK_ATTRIB_create_empty topology owner");
    var integer = stackalloc int[1] { value };
    ParasolidXtCorpusHost.Check(PK_ATTRIB_set_ints(attrib, 0, 1, integer), "PK_ATTRIB_set_ints topology owner");
    return body;
}

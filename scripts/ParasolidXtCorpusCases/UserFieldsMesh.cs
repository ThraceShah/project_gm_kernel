#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;
using System.Runtime.InteropServices;

// User fields require a non-zero PK_SESSION_start_o_t.user_field value.  The
// shared corpus host exposes that option per case-group; the four-slot integer
// round trip below exercises it alongside the stable facet-body mesh cases.
var cases = new CorpusCaseSpec[]
{
    new(
        "mesh.facet-body.block.distance-tolerance",
        "PK_BODY_make_facet_body",
        "Facet body converted from a solid block with an explicit distance tolerance.",
        new[] { "mesh", "mesh/facet-body", "mesh/mfacet", "mesh/mvertex", "body/solid/block", "parameters/distance-tolerance", "owner/body" },
        CreateFacetBlockDistanceTolerance,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"distanceTolerance\":0.01,\"angularTolerance\":null,\"maxFacetWidth\":null,\"maxChordLength\":null,\"retainAttributes\":false,\"retainGroups\":false,\"trackFaces\":false,\"trackEdges\":\"no\",\"partialConversion\":\"any\"}",
        inspectSchema: false),
    new(
        "mesh.facet-body.sphere.angular-tolerance",
        "PK_BODY_make_facet_body",
        "Facet body converted from a sphere with an explicit angular tolerance.",
        new[] { "mesh", "mesh/facet-body", "mesh/mfacet", "mesh/mvertex", "body/solid/sphere", "parameters/angular-tolerance", "owner/body" },
        CreateFacetSphereAngularTolerance,
        new CorpusBodyCounts(2, 2, 1, 0, 0),
        null,
        "{\"source\":\"body.solid.sphere.typical\",\"distanceTolerance\":null,\"angularTolerance\":0.2,\"maxFacetWidth\":null,\"maxChordLength\":null,\"retainAttributes\":false,\"retainGroups\":false,\"trackFaces\":false,\"trackEdges\":\"no\",\"partialConversion\":\"any\"}",
        inspectSchema: false),
    new(
        "user-field.body.integer",
        "PK_ENTITY_set_user_field + PK_PART_transmit_b",
        "A body user field written in an explicitly sized session and transmitted with the part.",
        new[] { "user-field", "body", "integer", "session/user-field-length", "transmit" },
        CreateUserFieldBlock,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"sessionUserFieldLength\":4,\"value\":12345,\"transmitUserFields\":true}",
        4,
        true,
        typeCoverage: new[] { "userfield.payload.nonzero" }),
    new(
        "user-field.body-and-face.integer",
        "PK_ENTITY_set_user_field + PK_ENTITY_ask_user_field + PK_PART_transmit_b",
        "Independent integer user fields are written to a body and one of its faces before transmit.",
        new[] { "user-field", "body", "face", "integer", "multiple-entities", "session/user-field-length", "transmit" },
        CreateUserFieldBodyAndFace,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"sessionUserFieldLength\":4,\"values\":{\"body\":[12345,0,0,0],\"firstFace\":[54321,0,0,0]},\"transmitUserFields\":true}",
        4,
        true,
        typeCoverage: new[] { "userfield.payload.nonzero", "userfield.owner.body", "userfield.owner.face" }),
    new(
        "user-field.body.zero",
        "PK_ENTITY_set_user_field + PK_PART_transmit_b",
        "A zero-filled user-field payload is transmitted and received without loss.",
        new[] { "user-field", "body", "zero-payload", "session/user-field-length", "transmit" },
        CreateUserFieldZeroBlock,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"sessionUserFieldLength\":4,\"value\":[0,0,0,0],\"transmitUserFields\":true}",
        4,
        true,
        typeCoverage: new[] { "userfield.payload.zero" }),
    new(
        "attribute.body.named-real",
        "PK_ATTDEF_create_2 + PK_ATTRIB_set_named_doubles + PK_ATTRIB_ask_named_doubles",
        "A named real attribute field is populated and read back on a solid body.",
        new[] { "attribute", "attdef", "named-field", "owner/body", "field/real", "action/transmit" },
        CreateNamedRealAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_named_real\",\"ownerTypes\":[\"body\"],\"fields\":[{\"name\":\"temperature\",\"type\":\"real\"}]},\"value\":273.15}"),
    new(
        "attribute.body.named-string",
        "PK_ATTDEF_create_2 + PK_ATTRIB_set_named_string + PK_ATTRIB_ask_named_string",
        "A named string attribute field is populated and read back on a solid body.",
        new[] { "attribute", "attdef", "named-field", "owner/body", "field/string", "action/transmit" },
        CreateNamedStringAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_named_string\",\"ownerTypes\":[\"body\"],\"fields\":[{\"name\":\"label\",\"type\":\"string\"}]},\"value\":\"parasolid-named\"}"),
    new(
        "attribute.body.named-vector",
        "PK_ATTDEF_create_2 + PK_ATTRIB_set_named_vectors + PK_ATTRIB_ask_named_vectors",
        "A named vector attribute field is populated and read back on a solid body.",
        new[] { "attribute", "attdef", "named-field", "owner/body", "field/vector", "action/transmit" },
        CreateNamedVectorAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_named_vector\",\"ownerTypes\":[\"body\"],\"fields\":[{\"name\":\"direction\",\"type\":\"vector\"}]},\"value\":[1.0,2.0,3.0]}"),
    new(
        "attribute.body.named-integer",
        "PK_ATTDEF_create_2 + PK_ATTRIB_set_named_ints + PK_ATTRIB_ask_named_ints",
        "A named integer attribute field is populated and read back on a solid body.",
        new[] { "attribute", "attdef", "named-field", "owner/body", "field/integer", "action/transmit" },
        CreateNamedIntegerAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_named_integer\",\"ownerTypes\":[\"body\"],\"fields\":[{\"name\":\"code\",\"type\":\"integer\"}]},\"value\":37}"),
    new(
        "attribute.body.named-axis",
        "PK_ATTDEF_create_2 + PK_ATTRIB_set_named_axes + PK_ATTRIB_ask_named_axes",
        "A named axis attribute field is populated and read back on a solid body.",
        new[] { "attribute", "attdef", "named-field", "owner/body", "field/axis", "action/transmit" },
        CreateNamedAxisAttribute,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"definition\":{\"name\":\"corpus_named_axis\",\"ownerTypes\":[\"body\"],\"fields\":[{\"name\":\"axis\",\"type\":\"axis\"}]},\"value\":{\"location\":[0.0,0.0,0.0],\"direction\":[0.0,0.0,1.0]}}"),
};

return ParasolidXtCorpusHost.RunGroup("user-fields-mesh", cases, args);

static unsafe PK_BODY_t CreateFacetBlockDistanceTolerance()
{
    ParasolidXtCorpusHost.Check(
        PK_SESSION_set_facet_geometry(PK_facet_geometry_all_c),
        "PK_SESSION_set_facet_geometry(all)");

    PK_BODY_t source;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &source),
        "PK_BODY_create_solid_block(mesh source)");

    var options = new PK_BODY_make_facet_body_o_t
    {
        o_t_version = 2,
        is_max_facet_width = PK_LOGICAL_false,
        max_facet_width = 0.0,
        is_distance_tolerance = PK_LOGICAL_true,
        distance_tolerance = 0.01,
        is_angular_tolerance = PK_LOGICAL_false,
        angular_tolerance = 0.0,
        is_max_chord_length = PK_LOGICAL_false,
        max_chord_length = 0.0,
        retain_attributes = PK_LOGICAL_false,
        retain_groups = PK_LOGICAL_false,
        track_faces = PK_LOGICAL_false,
        track_edges = PK_track_edges_no_c,
        return_redundant = PK_LOGICAL_false,
        partial_conversion = PK_ERROR_on_fail_any_c,
    };

    return MakeFacetBody(source, &options, "block");
}

static unsafe PK_BODY_t CreateFacetSphereAngularTolerance()
{
    ParasolidXtCorpusHost.Check(
        PK_SESSION_set_facet_geometry(PK_facet_geometry_all_c),
        "PK_SESSION_set_facet_geometry(all)");

    PK_BODY_t source;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_solid_sphere(2.0, null, &source),
        "PK_BODY_create_solid_sphere(mesh source)");

    var options = new PK_BODY_make_facet_body_o_t
    {
        o_t_version = 2,
        is_max_facet_width = PK_LOGICAL_false,
        max_facet_width = 0.0,
        is_distance_tolerance = PK_LOGICAL_false,
        distance_tolerance = 0.0,
        is_angular_tolerance = PK_LOGICAL_true,
        angular_tolerance = 0.2,
        is_max_chord_length = PK_LOGICAL_false,
        max_chord_length = 0.0,
        retain_attributes = PK_LOGICAL_false,
        retain_groups = PK_LOGICAL_false,
        track_faces = PK_LOGICAL_false,
        track_edges = PK_track_edges_no_c,
        return_redundant = PK_LOGICAL_false,
        partial_conversion = PK_ERROR_on_fail_any_c,
    };

    return MakeFacetBody(source, &options, "sphere");
}

static unsafe PK_BODY_t CreateUserFieldBlock()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block user field");
    int userFieldLength;
    ParasolidXtCorpusHost.Check(PK_SESSION_ask_user_field_len(&userFieldLength), "PK_SESSION_ask_user_field_len");
    if (userFieldLength < 1)
        throw new InvalidOperationException("session user-field length is " + userFieldLength);
    var values = stackalloc int[4] { 12345, 0, 0, 0 };
    ParasolidXtCorpusHost.Check(PK_ENTITY_set_user_field(body, values), "PK_ENTITY_set_user_field");
    var roundTrip = stackalloc int[4];
    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_user_field(body, roundTrip), "PK_ENTITY_ask_user_field");
    if (roundTrip[0] != values[0])
        throw new InvalidOperationException("user field mismatch: expected " + values[0] + ", got " + roundTrip[0]);
    return body;
}

static unsafe PK_BODY_t CreateUserFieldZeroBlock()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block zero user field");
    var values = stackalloc int[4];
    ParasolidXtCorpusHost.Check(PK_ENTITY_set_user_field(body, values), "PK_ENTITY_set_user_field zero");
    return body;
}

static unsafe PK_BODY_t CreateUserFieldBodyAndFace()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body),
        "PK_BODY_create_solid_block body and face user field");
    int userFieldLength;
    ParasolidXtCorpusHost.Check(PK_SESSION_ask_user_field_len(&userFieldLength), "PK_SESSION_ask_user_field_len");
    if (userFieldLength < 1)
        throw new InvalidOperationException("session user-field length is " + userFieldLength);

    PK_FACE_t face;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_first_face(body, &face), "PK_BODY_ask_first_face");
    if (face <= 0)
        throw new InvalidOperationException("body has no face for user-field case");

    var bodyValues = stackalloc int[4] { 12345, 0, 0, 0 };
    var faceValues = stackalloc int[4] { 54321, 0, 0, 0 };
    ParasolidXtCorpusHost.Check(PK_ENTITY_set_user_field(body, bodyValues), "PK_ENTITY_set_user_field body");
    ParasolidXtCorpusHost.Check(PK_ENTITY_set_user_field(face, faceValues), "PK_ENTITY_set_user_field face");
    var bodyRoundTrip = stackalloc int[4];
    var faceRoundTrip = stackalloc int[4];
    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_user_field(body, bodyRoundTrip), "PK_ENTITY_ask_user_field body");
    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_user_field(face, faceRoundTrip), "PK_ENTITY_ask_user_field face");
    if (bodyRoundTrip[0] != bodyValues[0] || faceRoundTrip[0] != faceValues[0])
        throw new InvalidOperationException("body/face user-field values differ after ask");
    return body;
}

static unsafe PK_BODY_t CreateNamedRealAttribute()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body),
        "PK_BODY_create_solid_block named real attribute");

    var attrib = CreateNamedAttribute(body, PK_ATTRIB_field_real_c, "corpus_named_real", "temperature");
    var value = stackalloc double[1] { 273.15 };
    ParasolidXtCorpusHost.Check(
        PK_ATTRIB_set_named_doubles(attrib, "temperature", 1, value),
        "PK_ATTRIB_set_named_doubles");

    int count;
    double* readBack;
    ParasolidXtCorpusHost.Check(
        PK_ATTRIB_ask_named_doubles(attrib, "temperature", &count, &readBack),
        "PK_ATTRIB_ask_named_doubles");
    try
    {
        if (count != 1 || readBack is null || readBack[0] != value[0])
            throw new InvalidOperationException("named real field round trip mismatch");
    }
    finally
    {
        if (readBack is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(readBack), "PK_MEMORY_free named real values");
    }

    return body;
}

static unsafe PK_BODY_t CreateNamedStringAttribute()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body),
        "PK_BODY_create_solid_block named string attribute");
    var attrib = CreateNamedAttribute(body, PK_ATTRIB_field_string_c, "corpus_named_string", "label");
    ParasolidXtCorpusHost.Check(
        PK_ATTRIB_set_named_string(attrib, "label", "parasolid-named"),
        "PK_ATTRIB_set_named_string");

    sbyte* readBack;
    ParasolidXtCorpusHost.Check(
        PK_ATTRIB_ask_named_string(attrib, "label", &readBack),
        "PK_ATTRIB_ask_named_string");
    try
    {
        var actual = Marshal.PtrToStringAnsi((nint)readBack);
        if (!string.Equals(actual, "parasolid-named", StringComparison.Ordinal))
            throw new InvalidOperationException("named string field round trip mismatch: " + actual);
    }
    finally
    {
        if (readBack is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(readBack), "PK_MEMORY_free named string value");
    }

    return body;
}

static unsafe PK_BODY_t CreateNamedVectorAttribute()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body),
        "PK_BODY_create_solid_block named vector attribute");
    var attrib = CreateNamedAttribute(body, PK_ATTRIB_field_vector_c, "corpus_named_vector", "direction");
    var value = stackalloc PK_VECTOR_t[1] { new PK_VECTOR_t(1.0, 2.0, 3.0) };
    ParasolidXtCorpusHost.Check(
        PK_ATTRIB_set_named_vectors(attrib, "direction", 1, value),
        "PK_ATTRIB_set_named_vectors");

    int count;
    PK_VECTOR_t* readBack;
    ParasolidXtCorpusHost.Check(
        PK_ATTRIB_ask_named_vectors(attrib, "direction", &count, &readBack),
        "PK_ATTRIB_ask_named_vectors");
    try
    {
        if (count != 1 || readBack is null || readBack[0].coord[0] != value[0].coord[0] ||
            readBack[0].coord[1] != value[0].coord[1] || readBack[0].coord[2] != value[0].coord[2])
            throw new InvalidOperationException("named vector field round trip mismatch");
    }
    finally
    {
        if (readBack is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(readBack), "PK_MEMORY_free named vector values");
    }

    return body;
}

static unsafe PK_BODY_t CreateNamedIntegerAttribute()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body),
        "PK_BODY_create_solid_block named integer attribute");
    var attrib = CreateNamedAttribute(body, PK_ATTRIB_field_integer_c, "corpus_named_integer", "code");
    var value = stackalloc int[1] { 37 };
    ParasolidXtCorpusHost.Check(
        PK_ATTRIB_set_named_ints(attrib, "code", 1, value),
        "PK_ATTRIB_set_named_ints");

    int count;
    int* readBack;
    ParasolidXtCorpusHost.Check(
        PK_ATTRIB_ask_named_ints(attrib, "code", &count, &readBack),
        "PK_ATTRIB_ask_named_ints");
    try
    {
        if (count != 1 || readBack is null || readBack[0] != value[0])
            throw new InvalidOperationException("named integer field round trip mismatch");
    }
    finally
    {
        if (readBack is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(readBack), "PK_MEMORY_free named integer values");
    }

    return body;
}

static unsafe PK_BODY_t CreateNamedAxisAttribute()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body),
        "PK_BODY_create_solid_block named axis attribute");
    var attrib = CreateNamedAttribute(body, PK_ATTRIB_field_axis_c, "corpus_named_axis", "axis");
    var value = stackalloc PK_AXIS1_sf_t[1]
    {
        new PK_AXIS1_sf_t(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0))
    };
    ParasolidXtCorpusHost.Check(
        PK_ATTRIB_set_named_axes(attrib, "axis", 1, value),
        "PK_ATTRIB_set_named_axes");

    int count;
    PK_AXIS1_sf_t* readBack;
    ParasolidXtCorpusHost.Check(
        PK_ATTRIB_ask_named_axes(attrib, "axis", &count, &readBack),
        "PK_ATTRIB_ask_named_axes");
    try
    {
        if (count != 1 || readBack is null || readBack[0].location.coord[0] != value[0].location.coord[0] ||
            readBack[0].location.coord[1] != value[0].location.coord[1] ||
            readBack[0].location.coord[2] != value[0].location.coord[2] ||
            readBack[0].axis.coord[0] != value[0].axis.coord[0] ||
            readBack[0].axis.coord[1] != value[0].axis.coord[1] ||
            readBack[0].axis.coord[2] != value[0].axis.coord[2])
            throw new InvalidOperationException("named axis field round trip mismatch");
    }
    finally
    {
        if (readBack is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(readBack), "PK_MEMORY_free named axis values");
    }

    return body;
}

static unsafe PK_ATTRIB_t CreateNamedAttribute(
    PK_BODY_t body,
    PK_ATTRIB_field_t fieldType,
    string definitionText,
    string fieldText)
{
    var ownerTypes = stackalloc PK_CLASS_t[1] { PK_CLASS_body };
    var fieldTypes = stackalloc PK_ATTRIB_field_t[1] { fieldType };
    var definitionName = stackalloc byte[64];
    for (var i = 0; i < definitionText.Length; i++)
        definitionName[i] = (byte)definitionText[i];

    var namedField = stackalloc byte[64];
    for (var i = 0; i < fieldText.Length; i++)
        namedField[i] = (byte)fieldText[i];
    var namedFields = stackalloc byte*[1];
    namedFields[0] = namedField;
    var fieldNames = new PK_field_names_t(namedFields);
    var definition = new PK_ATTDEF_sf_2_t(
        definitionName,
        PK_ATTDEF_class_zero1_c,
        1,
        ownerTypes,
        1,
        fieldTypes,
        PK_LOGICAL_false,
        fieldNames,
        null);
    PK_ATTDEF_t attdef;
    ParasolidXtCorpusHost.Check(PK_ATTDEF_create_2(&definition, &attdef), "PK_ATTDEF_create_2 " + definitionText);

    PK_LOGICAL_t mayOwn;
    ParasolidXtCorpusHost.Check(PK_ENTITY_may_own_attdef(body, attdef, &mayOwn), "PK_ENTITY_may_own_attdef " + definitionText);
    if ((int)mayOwn != 1)
        throw new InvalidOperationException("body is not a legal owner for " + definitionText);

    PK_ATTRIB_t attrib;
    ParasolidXtCorpusHost.Check(PK_ATTRIB_create_empty(body, attdef, &attrib), "PK_ATTRIB_create_empty " + definitionText);
    return attrib;
}

static unsafe PK_BODY_t MakeFacetBody(
    PK_BODY_t source,
    PK_BODY_make_facet_body_o_t* options,
    string sourceName)
{
    PK_BODY_t facetBody;
    PK_TOPOL_track_r_t tracking = default;
    PK_TOPOL_track_r_t redundant = default;
    ParasolidXtCorpusHost.Check(
        PK_BODY_make_facet_body(source, 0, options, &facetBody, &tracking, &redundant),
        "PK_BODY_make_facet_body(" + sourceName + ")");
    if (facetBody <= 0)
        throw new InvalidOperationException("PK_BODY_make_facet_body returned an invalid body");
    return facetBody;
}

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
        "body.wire.helical-curve.right-handed",
        "PK_POINT_make_helical_curve + PK_CURVE_make_wire_body",
        "One-turn right-handed helix promoted to a persistent wire body.",
        new[] { "body", "body/wire", "curve", "curve/helical", "hand/right", "parameters/one-turn" },
        CreateRightHandedHelix,
        null,
        null,
        "{\"point\":[2.0,0.0,0.0],\"axis\":{\"location\":[0.0,0.0,0.0],\"direction\":[0.0,0.0,1.0]},\"hand\":\"right\",\"turns\":[0.0,1.0],\"helicalPitch\":2.0,\"spiralPitch\":0.0,\"tolerance\":0.00001}",
        typeCoverage: new[] { "body.type.wire" },
        inspectSchema: false),
    new(
        "body.wire.helical-curve.left-tapered",
        "PK_POINT_make_helical_curve + PK_CURVE_make_wire_body",
        "Two-turn left-handed tapered helix with a positive spiral pitch.",
        new[] { "body", "body/wire", "curve", "curve/helical", "hand/left", "parameters/multi-turn", "parameters/tapered" },
        CreateLeftTaperedHelix,
        null,
        null,
        "{\"point\":[2.0,0.0,0.0],\"axis\":{\"location\":[0.0,0.0,0.0],\"direction\":[0.0,0.0,1.0]},\"hand\":\"left\",\"turns\":[0.0,2.0],\"helicalPitch\":1.5,\"spiralPitch\":0.5,\"tolerance\":0.00001}",
        typeCoverage: new[] { "body.type.wire" },
        inspectSchema: false),
};

return ParasolidXtCorpusHost.RunGroup("helical-curves", cases, args);

static unsafe PK_BODY_t CreateRightHandedHelix()
{
    var pointForm = new PK_POINT_sf_t(new PK_VECTOR_t(2.0, 0.0, 0.0));
    PK_POINT_t point;
    ParasolidXtCorpusHost.Check(PK_POINT_create(&pointForm, &point), "PK_POINT_create right-handed helix");

    var axis = new PK_AXIS1_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0));
    PK_CURVE_t curve;
    ParasolidXtCorpusHost.Check(
        PK_POINT_make_helical_curve(
            point,
            &axis,
            PK_HAND_right_c,
            new PK_INTERVAL_t(0.0, 1.0),
            2.0,
            0.0,
            0.00001,
            &curve),
        "PK_POINT_make_helical_curve(right)");

    var interval = new PK_INTERVAL_t();
    ParasolidXtCorpusHost.Check(PK_CURVE_ask_interval(curve, &interval), "PK_CURVE_ask_interval(right)");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_CURVE_make_wire_body(curve, interval, &body), "PK_CURVE_make_wire_body(right)");
    return body;
}

static unsafe PK_BODY_t CreateLeftTaperedHelix()
{
    var pointForm = new PK_POINT_sf_t(new PK_VECTOR_t(2.0, 0.0, 0.0));
    PK_POINT_t point;
    ParasolidXtCorpusHost.Check(PK_POINT_create(&pointForm, &point), "PK_POINT_create left tapered helix");

    var axis = new PK_AXIS1_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0));
    PK_CURVE_t curve;
    ParasolidXtCorpusHost.Check(
        PK_POINT_make_helical_curve(
            point,
            &axis,
            PK_HAND_left_c,
            new PK_INTERVAL_t(0.0, 2.0),
            1.5,
            0.5,
            0.00001,
            &curve),
        "PK_POINT_make_helical_curve(left tapered)");

    var interval = new PK_INTERVAL_t();
    ParasolidXtCorpusHost.Check(PK_CURVE_ask_interval(curve, &interval), "PK_CURVE_ask_interval(left tapered)");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_CURVE_make_wire_body(curve, interval, &body), "PK_CURVE_make_wire_body(left tapered)");
    return body;
}

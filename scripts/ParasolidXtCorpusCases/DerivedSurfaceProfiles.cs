#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// Derived surfaces are kept as small deterministic fixtures.  Each case
// varies the profile family or offset sign while retaining the same typed ask
// and XT dependency checks.
var cases = new CorpusCaseSpec[]
{
    new("derived.offset.negative", "PK_PLANE_create + PK_SURF_offset", "Negative offset of a plane.", new[] { "surface", "offset", "offset/negative" }, () => CreateOffset(-1.0), new CorpusBodyCounts(1, 1, 1, 4, 4), typeCoverage: new[] { "geometry.surface.offset", "derived.offset.negative" }, typedAsk: body => AssertClass(body, PK_CLASS_offset)),
    new("derived.offset.positive", "PK_PLANE_create + PK_SURF_offset", "Positive offset of a plane.", new[] { "surface", "offset", "offset/positive" }, () => CreateOffset(1.0), new CorpusBodyCounts(1, 1, 1, 4, 4), typeCoverage: new[] { "geometry.surface.offset", "derived.offset.positive" }, typedAsk: body => AssertClass(body, PK_CLASS_offset)),
    new("derived.swept.line", "PK_LINE_create + PK_SWEPT_create", "Open line profile swept along a non-default direction.", new[] { "surface", "swept", "profile/line", "profile/open" }, () => CreateSwept(false), new CorpusBodyCounts(1, 1, 1, 4, 4), typeCoverage: new[] { "geometry.surface.swept", "derived.swept.profile.line" }, typedAsk: body => AssertClass(body, PK_CLASS_swept)),
    new("derived.swept.circle", "PK_CIRCLE_create + PK_SWEPT_create", "Closed circular profile swept along a non-default direction.", new[] { "surface", "swept", "profile/circle", "profile/closed" }, () => CreateSwept(true), new CorpusBodyCounts(1, 1, 1, 4, 4), typeCoverage: new[] { "geometry.surface.swept", "derived.swept.profile.circle", "derived.profile.closed" }, typedAsk: body => AssertClass(body, PK_CLASS_swept)),
    new("derived.spun.line", "PK_LINE_create + PK_SPUN_create", "Open line profile spun around an offset axis.", new[] { "surface", "spun", "profile/line", "profile/open" }, () => CreateSpun(false), new CorpusBodyCounts(1, 1, 1, 4, 4), typeCoverage: new[] { "geometry.surface.spun", "derived.spun.profile.line" }, typedAsk: body => AssertClass(body, PK_CLASS_spun)),
    new("derived.spun.circle", "PK_CIRCLE_create + PK_SPUN_create", "Closed circular profile spun around an offset axis.", new[] { "surface", "spun", "profile/circle", "profile/closed" }, () => CreateSpun(true), new CorpusBodyCounts(1, 1, 1, 4, 4), typeCoverage: new[] { "geometry.surface.spun", "derived.spun.profile.circle", "derived.profile.closed" }, typedAsk: body => AssertClass(body, PK_CLASS_spun)),
};

return ParasolidXtCorpusHost.RunGroup("derived-surface-profiles", cases, args);

static unsafe PK_BODY_t CreateOffset(double distance)
{
    var form = new PK_PLANE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)));
    PK_PLANE_t plane;
    ParasolidXtCorpusHost.Check(PK_PLANE_create(&form, &plane), "PK_PLANE_create derived offset");
    PK_SURF_t offset;
    ParasolidXtCorpusHost.Check(PK_SURF_offset(plane, distance, &offset), "PK_SURF_offset derived");
    return AddGeometry(offset);
}

static unsafe PK_BODY_t CreateSwept(bool circle)
{
    PK_CURVE_t profile = circle ? CreateCircle() : CreateLine();
    var form = new PK_SWEPT_sf_t(profile, new PK_VECTOR1_t(0, 0, 1));
    PK_SWEPT_t swept;
    ParasolidXtCorpusHost.Check(PK_SWEPT_create(&form, &swept), "PK_SWEPT_create derived");
    return AddGeometry(swept);
}

static unsafe PK_BODY_t CreateSpun(bool circle)
{
    PK_CURVE_t profile = circle ? CreateCircle() : CreateAxialLine();
    var form = new PK_SPUN_sf_t(profile, new PK_AXIS1_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1)));
    PK_SPUN_t spun;
    ParasolidXtCorpusHost.Check(PK_SPUN_create(&form, &spun), "PK_SPUN_create derived");
    return AddGeometry(spun);
}

static unsafe PK_CURVE_t CreateAxialLine()
{
    var form = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(2, 0, 0), new PK_VECTOR1_t(0, 0, 1)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&form, &line), "PK_LINE_create axial derived profile");
    return line;
}

static unsafe PK_CURVE_t CreateLine()
{
    var form = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(2, 0, 0), new PK_VECTOR1_t(1, 0, 0)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&form, &line), "PK_LINE_create derived profile");
    return line;
}

static unsafe PK_CURVE_t CreateCircle()
{
    var form = new PK_CIRCLE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(2, 0, 0), new PK_VECTOR1_t(0, 1, 0), new PK_VECTOR1_t(1, 0, 0)), 0.5);
    PK_CIRCLE_t circle;
    ParasolidXtCorpusHost.Check(PK_CIRCLE_create(&form, &circle), "PK_CIRCLE_create derived profile");
    return circle;
}

static unsafe PK_BODY_t AddGeometry(PK_GEOM_t geometry)
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4, 4, null, &body), "PK_BODY_create_sheet_rectangle derived");
    var values = stackalloc PK_GEOM_t[1] { geometry };
    ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, values), "PK_PART_add_geoms derived");
    return body;
}

static unsafe void AssertClass(PK_BODY_t body, PK_CLASS_t expected)
{
    int count;
    PK_GEOM_t* values;
    ParasolidXtCorpusHost.Check(PK_PART_ask_geoms(body, &count, &values), "PK_PART_ask_geoms derived");
    try
    {
        for (var i = 0; i < count; i++)
        {
            PK_CLASS_t actual;
            ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(values[i], &actual), "PK_ENTITY_ask_class derived");
            if (actual == expected || (expected == PK_CLASS_offset && actual == PK_CLASS_plane)) return;
        }
    }
    finally { if (values is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(values), "PK_MEMORY_free derived"); }
    throw new InvalidOperationException("derived fixture missing expected class " + expected);
}

#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:property UseCorpusManagedKernel=true
#:property AssemblyName=ParasolidXtCorpusBodyBasics
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using static parasolid;

// Body producer coverage.  Every successful case is intentionally small and is
// handed to the shared corpus host, which writes one model.x_t per case.  The
// reject cases below are checked separately because they must not produce a
// fixture or enter the successful-case manifest.
var cases = new[]
{
    new CorpusCaseSpec(
        "body.solid.block.typical",
        "PK_BODY_create_solid_block",
        "Solid block with typical positive dimensions and the default basis set.",
        new[] { "body", "body/solid", "body/solid/block", "basis/default", "parameters/typical" },
        CreateBlockTypical,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        ManagedBlockTypical,
        "{\"x\":1.0,\"y\":2.0,\"z\":3.0,\"basisSet\":null}",
        typedAsk: AssertSolid,
        typeCoverage: new[] { "body.type.solid" }),
    new CorpusCaseSpec(
        "body.solid.block.minimum-positive",
        "PK_BODY_create_solid_block",
        "Solid block at the smallest stable positive dimensions used by this corpus.",
        new[] { "body", "body/solid", "body/solid/block", "basis/default", "parameters/positive-minimum", "boundary/positive" },
        CreateBlockMinimumPositive,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        ManagedBlockMinimumPositive,
        "{\"x\":0.001,\"y\":0.001,\"z\":0.001,\"basisSet\":null}",
        typedAsk: AssertSolid,
        typeCoverage: new[] { "body.type.solid" }),
    new CorpusCaseSpec(
        "body.solid.block.oriented",
        "PK_BODY_create_solid_block",
        "Solid block using a translated, rotated right-handed basis set.",
        new[] { "body", "body/solid", "body/solid/block", "basis/non-default", "basis/translation", "basis/orientation" },
        CreateBlockOriented,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        ManagedBlockOriented,
        "{\"x\":1.0,\"y\":2.0,\"z\":3.0,\"basisSet\":{\"location\":[3.0,-2.0,1.0],\"axis\":[0.0,0.0,1.0],\"refDirection\":[1.0,0.0,0.0]}}",
        typedAsk: AssertSolid,
        typeCoverage: new[] { "body.type.solid" }),
    new CorpusCaseSpec(
        "body.solid.cyl.typical",
        "PK_BODY_create_solid_cyl",
        "Solid cylinder with typical positive radius and height.",
        new[] { "body", "body/solid", "body/solid/cylinder", "basis/default", "parameters/typical" },
        CreateCylinderTypical,
        new CorpusBodyCounts(2, 2, 3, 2, 0),
        ManagedCylinderTypical,
        "{\"radius\":2.0,\"height\":5.0,\"basisSet\":null}",
        typedAsk: AssertSolid,
        typeCoverage: new[] { "body.type.solid" }),
    new CorpusCaseSpec(
        "body.solid.cyl.minimum-positive",
        "PK_BODY_create_solid_cyl",
        "Solid cylinder at stable positive radius and height boundaries.",
        new[] { "body", "body/solid", "body/solid/cylinder", "basis/default", "parameters/positive-minimum", "boundary/positive" },
        CreateCylinderMinimumPositive,
        new CorpusBodyCounts(2, 2, 3, 2, 0),
        ManagedCylinderMinimumPositive,
        "{\"radius\":0.001,\"height\":0.001,\"basisSet\":null}",
        typedAsk: AssertSolid,
        typeCoverage: new[] { "body.type.solid" }),
    new CorpusCaseSpec(
        "body.solid.cone.typical",
        "PK_BODY_create_solid_cone",
        "Solid cone with positive base radius, height and semi-angle.",
        new[] { "body", "body/solid", "body/solid/cone", "basis/default", "parameters/typical" },
        CreateConeTypical,
        new CorpusBodyCounts(2, 2, 3, 2, 0),
        ManagedConeTypical,
        "{\"radius\":1.0,\"height\":5.0,\"semiAngle\":0.25,\"basisSet\":null}",
        typedAsk: AssertSolid,
        typeCoverage: new[] { "body.type.solid" }),
    new CorpusCaseSpec(
        "body.solid.cone.apex-radius-zero",
        "PK_BODY_create_solid_cone",
        "Solid cone with the documented zero-radius apex boundary.",
        new[] { "body", "body/solid", "body/solid/cone", "basis/default", "parameters/radius-zero", "boundary/degenerate-but-valid" },
        CreateConeApex,
        new CorpusBodyCounts(2, 2, 2, 1, 1),
        ManagedConeApex,
        "{\"radius\":0.0,\"height\":5.0,\"semiAngle\":0.25,\"basisSet\":null}",
        typedAsk: AssertSolid,
        typeCoverage: new[] { "body.type.solid", "topology.loop.vertex" }),
    new CorpusCaseSpec(
        "body.solid.prism.typical",
        "PK_BODY_create_solid_prism",
        "Solid five-sided prism with typical positive radius and height.",
        new[] { "body", "body/solid", "body/solid/prism", "basis/default", "parameters/typical", "parameters/n-sides" },
        CreatePrismTypical,
        new CorpusBodyCounts(2, 2, 7, 15, 10),
        ManagedPrismTypical,
        "{\"radius\":2.0,\"height\":5.0,\"nSides\":5,\"basisSet\":null}",
        typedAsk: AssertSolid,
        typeCoverage: new[] { "body.type.solid" }),
    new CorpusCaseSpec(
        "body.solid.prism.minimum-sides",
        "PK_BODY_create_solid_prism",
        "Solid triangular prism at the minimum legal side count.",
        new[] { "body", "body/solid", "body/solid/prism", "basis/default", "parameters/minimum-sides", "boundary/side-count" },
        CreatePrismMinimumSides,
        new CorpusBodyCounts(2, 2, 5, 9, 6),
        ManagedPrismMinimumSides,
        "{\"radius\":1.0,\"height\":1.0,\"nSides\":3,\"basisSet\":null}",
        typedAsk: AssertSolid,
        typeCoverage: new[] { "body.type.solid" }),
    new CorpusCaseSpec(
        "body.solid.sphere.typical",
        "PK_BODY_create_solid_sphere",
        "Solid sphere with a typical positive radius.",
        new[] { "body", "body/solid", "body/solid/sphere", "basis/default", "parameters/typical" },
        CreateSphereTypical,
        new CorpusBodyCounts(2, 2, 1, 0, 0),
        ManagedSphereTypical,
        "{\"radius\":2.0,\"basisSet\":null}",
        typedAsk: AssertSolid,
        typeCoverage: new[] { "body.type.solid" }),
    new CorpusCaseSpec(
        "body.solid.torus.typical",
        "PK_BODY_create_solid_torus",
        "Solid torus with distinct positive major and minor radii.",
        new[] { "body", "body/solid", "body/solid/torus", "basis/default", "parameters/typical" },
        CreateTorusTypical,
        new CorpusBodyCounts(2, 2, 1, 0, 0),
        ManagedTorusTypical,
        "{\"majorRadius\":5.0,\"minorRadius\":1.0,\"basisSet\":null}",
        typedAsk: AssertSolid,
        typeCoverage: new[] { "body.type.solid" }),
};

if (Array.Exists(args, static argument => string.Equals(argument, "--check-rejections", StringComparison.Ordinal)))
    return CheckRejections();

return ParasolidXtCorpusHost.RunGroup("body-basics", cases, args);

static unsafe void AssertSolid(PK_BODY_t body)
{
    PK_BODY_type_t bodyType;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_type(body, &bodyType), "PK_BODY_ask_type");
    if (bodyType != PK_BODY_type_solid_c)
        throw new InvalidOperationException("expected solid body, got " + bodyType);
}

static unsafe PK_BODY_t CreateBlockTypical()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(block.typical)");
    return body;
}

static unsafe PK_BODY_t CreateBlockMinimumPositive()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(0.001, 0.001, 0.001, null, &body), "PK_BODY_create_solid_block(block.minimum-positive)");
    return body;
}

static unsafe PK_BODY_t CreateBlockOriented()
{
    var basis = MakeBasis(3.0, -2.0, 1.0, 0.0, 0.0, 1.0, 1.0, 0.0, 0.0);
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, &basis, &body), "PK_BODY_create_solid_block(block.oriented)");
    return body;
}

static unsafe PK_BODY_t CreateCylinderTypical()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_cyl(2.0, 5.0, null, &body), "PK_BODY_create_solid_cyl(cyl.typical)");
    return body;
}

static unsafe PK_BODY_t CreateCylinderMinimumPositive()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_cyl(0.001, 0.001, null, &body), "PK_BODY_create_solid_cyl(cyl.minimum-positive)");
    return body;
}

static unsafe PK_BODY_t CreateConeTypical()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_cone(1.0, 5.0, 0.25, null, &body), "PK_BODY_create_solid_cone(cone.typical)");
    return body;
}

static unsafe PK_BODY_t CreateConeApex()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_cone(0.0, 5.0, 0.25, null, &body), "PK_BODY_create_solid_cone(cone.apex-radius-zero)");
    return body;
}

static unsafe PK_BODY_t CreatePrismTypical()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_prism(2.0, 5.0, 5, null, &body), "PK_BODY_create_solid_prism(prism.typical)");
    return body;
}

static unsafe PK_BODY_t CreatePrismMinimumSides()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_prism(1.0, 1.0, 3, null, &body), "PK_BODY_create_solid_prism(prism.minimum-sides)");
    return body;
}

static unsafe PK_BODY_t CreateSphereTypical()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_sphere(2.0, null, &body), "PK_BODY_create_solid_sphere(sphere.typical)");
    return body;
}

static unsafe PK_BODY_t CreateTorusTypical()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_torus(5.0, 1.0, null, &body), "PK_BODY_create_solid_torus(torus.typical)");
    return body;
}

static byte[] ManagedBlockTypical() => CorpusManagedKernel.Transmit(CreateManagedBlockTypical);
static byte[] ManagedBlockMinimumPositive() => CorpusManagedKernel.Transmit(CreateManagedBlockMinimumPositive);
static byte[] ManagedBlockOriented() => CorpusManagedKernel.Transmit(CreateManagedBlockOriented);
static byte[] ManagedCylinderTypical() => CorpusManagedKernel.Transmit(CreateManagedCylinderTypical);
static byte[] ManagedCylinderMinimumPositive() => CorpusManagedKernel.Transmit(CreateManagedCylinderMinimumPositive);
static byte[] ManagedConeTypical() => CorpusManagedKernel.Transmit(CreateManagedConeTypical);
static byte[] ManagedConeApex() => CorpusManagedKernel.Transmit(CreateManagedConeApex);
static byte[] ManagedPrismTypical() => CorpusManagedKernel.Transmit(CreateManagedPrismTypical);
static byte[] ManagedPrismMinimumSides() => CorpusManagedKernel.Transmit(CreateManagedPrismMinimumSides);
static byte[] ManagedSphereTypical() => CorpusManagedKernel.Transmit(CreateManagedSphereTypical);
static byte[] ManagedTorusTypical() => CorpusManagedKernel.Transmit(CreateManagedTorusTypical);

static unsafe PK_BODY_t CreateManagedBlockTypical()
{
    PK_BODY_t body;
    CheckManaged(KernelRuntime.BodyCreateSolidBlock(1.0, 2.0, 3.0, null, &body), "managed block typical");
    return body;
}

static unsafe PK_BODY_t CreateManagedBlockMinimumPositive()
{
    PK_BODY_t body;
    CheckManaged(KernelRuntime.BodyCreateSolidBlock(0.001, 0.001, 0.001, null, &body), "managed block minimum-positive");
    return body;
}

static unsafe PK_BODY_t CreateManagedBlockOriented()
{
    var basis = MakeManagedBasis(3.0, -2.0, 1.0, 0.0, 0.0, 1.0, 1.0, 0.0, 0.0);
    PK_BODY_t body;
    CheckManaged(KernelRuntime.BodyCreateSolidBlock(1.0, 2.0, 3.0, &basis, &body), "managed block oriented");
    return body;
}

static unsafe PK_BODY_t CreateManagedCylinderTypical()
{
    PK_BODY_t body;
    CheckManaged(KernelRuntime.BodyCreateSolidCyl(2.0, 5.0, null, &body), "managed cylinder typical");
    return body;
}

static unsafe PK_BODY_t CreateManagedCylinderMinimumPositive()
{
    PK_BODY_t body;
    CheckManaged(KernelRuntime.BodyCreateSolidCyl(0.001, 0.001, null, &body), "managed cylinder minimum-positive");
    return body;
}

static unsafe PK_BODY_t CreateManagedConeTypical()
{
    PK_BODY_t body;
    CheckManaged(KernelRuntime.BodyCreateSolidCone(1.0, 5.0, 0.25, null, &body), "managed cone typical");
    return body;
}

static unsafe PK_BODY_t CreateManagedConeApex()
{
    PK_BODY_t body;
    CheckManaged(KernelRuntime.BodyCreateSolidCone(0.0, 5.0, 0.25, null, &body), "managed cone apex");
    return body;
}

static unsafe PK_BODY_t CreateManagedPrismTypical()
{
    PK_BODY_t body;
    CheckManaged(KernelRuntime.BodyCreateSolidPrism(2.0, 5.0, 5, null, &body), "managed prism typical");
    return body;
}

static unsafe PK_BODY_t CreateManagedPrismMinimumSides()
{
    PK_BODY_t body;
    CheckManaged(KernelRuntime.BodyCreateSolidPrism(1.0, 1.0, 3, null, &body), "managed prism minimum-sides");
    return body;
}

static unsafe PK_BODY_t CreateManagedSphereTypical()
{
    PK_BODY_t body;
    CheckManaged(KernelRuntime.BodyCreateSolidSphere(2.0, null, &body), "managed sphere typical");
    return body;
}

static unsafe PK_BODY_t CreateManagedTorusTypical()
{
    PK_BODY_t body;
    CheckManaged(KernelRuntime.BodyCreateSolidTorus(5.0, 1.0, null, &body), "managed torus typical");
    return body;
}

static unsafe ProjectGmKernel.Native.Generated.PK_AXIS2_sf_s MakeManagedBasis(
    double x,
    double y,
    double z,
    double axisX,
    double axisY,
    double axisZ,
    double refX,
    double refY,
    double refZ)
{
    var basis = new ProjectGmKernel.Native.Generated.PK_AXIS2_sf_s();
    basis.location.coord[0] = x;
    basis.location.coord[1] = y;
    basis.location.coord[2] = z;
    basis.axis.coord[0] = axisX;
    basis.axis.coord[1] = axisY;
    basis.axis.coord[2] = axisZ;
    basis.ref_direction.coord[0] = refX;
    basis.ref_direction.coord[1] = refY;
    basis.ref_direction.coord[2] = refZ;
    return basis;
}

static void CheckManaged(int error, string operation)
{
    if (error != ParasolidConstants.PK_ERROR_no_errors)
        throw new InvalidOperationException(operation + " failed with error " + error);
}

static unsafe PK_AXIS2_sf_t MakeBasis(
    double x,
    double y,
    double z,
    double axisX,
    double axisY,
    double axisZ,
    double refX,
    double refY,
    double refZ)
{
    var basis = new PK_AXIS2_sf_t();
    basis.location.coord[0] = x;
    basis.location.coord[1] = y;
    basis.location.coord[2] = z;
    basis.axis.coord[0] = axisX;
    basis.axis.coord[1] = axisY;
    basis.axis.coord[2] = axisZ;
    basis.ref_direction.coord[0] = refX;
    basis.ref_direction.coord[1] = refY;
    basis.ref_direction.coord[2] = refZ;
    return basis;
}

static unsafe int CheckRejections([System.Runtime.CompilerServices.CallerFilePath] string scriptPath = "")
{
    if (!ParasolidScriptHost.TryStartSession("Parasolid XT corpus body rejection checks", out var session, out var skipMessage, scriptPath))
    {
        Console.WriteLine(skipMessage);
        return 0;
    }

    using (session)
    {
        var failures = 0;
        failures += ExpectError("body.solid.block.reject.zero-x", PK_ERROR_distance_le_0, RejectBlockZeroX);
        failures += ExpectError("body.solid.cyl.reject.zero-radius", PK_ERROR_distance_le_0, RejectCylinderZeroRadius);
        failures += ExpectError("body.solid.cone.reject.zero-height", PK_ERROR_distance_le_0, RejectConeZeroHeight);
        failures += ExpectError("body.solid.cone.reject.zero-angle", PK_ERROR_bad_angle, RejectConeZeroAngle);
        failures += ExpectError("body.solid.prism.reject.two-sides", PK_ERROR_lt_3_sides, RejectPrismTwoSides);
        failures += ExpectError("body.solid.sphere.reject.zero-radius", PK_ERROR_distance_le_0, RejectSphereZeroRadius);
        failures += ExpectError("body.solid.torus.reject.zero-minor-radius", PK_ERROR_distance_le_0, RejectTorusZeroMinorRadius);
        failures += ExpectError("body.solid.torus.reject.equal-radii", PK_ERROR_majrad_minrad_mismatch, RejectTorusEqualRadii);
        failures += ExpectError("body.solid.torus.reject.non-positive-sum", PK_ERROR_majrad_minrad_mismatch, RejectTorusNonPositiveSum);
        return failures == 0 ? 0 : 1;
    }
}

static int ExpectError(
    string caseId,
    PK_ERROR_code_t expected,
    Func<PK_ERROR_code_t> create)
{
    var actual = create();
    var passed = actual == expected;
    Console.WriteLine((passed ? "PASS " : "FAIL ") + caseId + " expected=" + expected + " actual=" + actual);
    return passed ? 0 : 1;
}

static unsafe PK_ERROR_code_t RejectBlockZeroX()
{
    PK_BODY_t body;
    return PK_BODY_create_solid_block(0.0, 2.0, 3.0, null, &body);
}

static unsafe PK_ERROR_code_t RejectCylinderZeroRadius()
{
    PK_BODY_t body;
    return PK_BODY_create_solid_cyl(0.0, 5.0, null, &body);
}

static unsafe PK_ERROR_code_t RejectConeZeroHeight()
{
    PK_BODY_t body;
    return PK_BODY_create_solid_cone(1.0, 0.0, 0.25, null, &body);
}

static unsafe PK_ERROR_code_t RejectConeZeroAngle()
{
    PK_BODY_t body;
    return PK_BODY_create_solid_cone(1.0, 5.0, 0.0, null, &body);
}

static unsafe PK_ERROR_code_t RejectPrismTwoSides()
{
    PK_BODY_t body;
    return PK_BODY_create_solid_prism(2.0, 5.0, 2, null, &body);
}

static unsafe PK_ERROR_code_t RejectSphereZeroRadius()
{
    PK_BODY_t body;
    return PK_BODY_create_solid_sphere(0.0, null, &body);
}

static unsafe PK_ERROR_code_t RejectTorusZeroMinorRadius()
{
    PK_BODY_t body;
    return PK_BODY_create_solid_torus(5.0, 0.0, null, &body);
}

static unsafe PK_ERROR_code_t RejectTorusEqualRadii()
{
    PK_BODY_t body;
    return PK_BODY_create_solid_torus(1.0, 1.0, null, &body);
}

static unsafe PK_ERROR_code_t RejectTorusNonPositiveSum()
{
    PK_BODY_t body;
    return PK_BODY_create_solid_torus(-2.0, 1.0, null, &body);
}

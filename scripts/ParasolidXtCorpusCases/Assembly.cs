#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

var cases = new CorpusAssemblyCaseSpec[]
{
    new(
        "assembly.empty",
        "PK_ASSEMBLY_create_empty",
        "An empty assembly part with no instances or child parts.",
        new[] { "assembly", "assembly/empty", "instance/none", "part/identity" },
        CreateEmptyAssembly,
        new CorpusAssemblyCounts(0, 0),
        "{\"instances\":[],\"parts\":[]}",
        new[] { "assembly.empty" }),
    new(
        "assembly.single-block-identity-instance",
        "PK_ASSEMBLY_create_empty + PK_TRANSF_create + PK_INSTANCE_create",
        "A one-level assembly containing one solid block through an identity transform.",
        new[] { "assembly", "assembly/one-level", "instance/single", "part/shared", "transform/identity" },
        CreateSingleBlockInstance,
        new CorpusAssemblyCounts(1, 1),
        "{\"instances\":[{\"part\":\"body.solid.block.typical\",\"transform\":\"identity\"}]}",
        new[] { "assembly.shared-part" }),
    new(
        "assembly.single-block-levelized",
        "PK_ASSEMBLY_make_level_assembly + PK_INSTANCE_create",
        "A one-level assembly normalized through the level-assembly producer.",
        new[] { "assembly", "assembly/levelized", "instance/single", "part/shared" },
        CreateLevelizedAssembly,
        new CorpusAssemblyCounts(1, 1),
        "{\"instances\":[{\"part\":\"body.solid.block.typical\"}],\"operation\":\"make-level-assembly\"}"),
    new(
        "assembly.single-block-transformed",
        "PK_ASSEMBLY_transform + PK_INSTANCE_create",
        "A one-level assembly translated by a persistent assembly transform.",
        new[] { "assembly", "assembly/one-level", "instance/single", "transform", "translation" },
        CreateTransformedAssembly,
        new CorpusAssemblyCounts(1, 1),
        "{\"instances\":[{\"part\":\"body.solid.block.typical\"}],\"translation\":[2.0,1.0,0.0]}"),
    new(
        "assembly.instance-transformed",
        "PK_INSTANCE_transform + PK_INSTANCE_create",
        "A single instance translated independently inside its owning assembly.",
        new[] { "assembly", "instance", "instance/transform", "translation" },
        CreateInstanceTransformedAssembly,
        new CorpusAssemblyCounts(1, 1),
        "{\"instanceTransform\":\"translation\",\"translation\":[1.0,0.0,2.0]}"),
    new(
        "assembly.instance-replaced-transform",
        "PK_INSTANCE_replace_transf + PK_INSTANCE_create",
        "A single instance whose transform is replaced after creation.",
        new[] { "assembly", "instance", "instance/replace-transform", "translation" },
        CreateInstanceReplacedTransformAssembly,
        new CorpusAssemblyCounts(1, 1),
        "{\"initialTransform\":\"identity\",\"replacement\":\"translation\",\"translation\":[-1.0,2.0,0.0]}"),
    new(
        "assembly.instance-reflection",
        "PK_TRANSF_create_reflection + PK_INSTANCE_create",
        "A single instance using a reflection transform across the YZ plane.",
        new[] { "assembly", "instance", "transform", "reflection" },
        CreateReflectedAssembly,
        new CorpusAssemblyCounts(1, 1),
        "{\"reflectionPlane\":{\"position\":[0.0,0.0,0.0],\"normal\":[1.0,0.0,0.0]}}",
        new[] { "assembly.transform.reflection" }),
    new(
        "assembly.instance-rotation",
        "PK_TRANSF_create_rotation + PK_INSTANCE_transform",
        "A single instance carrying a persistent rotation transform.",
        new[] { "assembly", "instance", "transform", "rotation" },
        CreateRotatedAssembly,
        new CorpusAssemblyCounts(1, 1),
        "{\"rotationAxis\":[0.0,0.0,1.0],\"angle\":0.5}",
        new[] { "assembly.transform.rotation" }),
    new(
        "assembly.shared-two-instances",
        "PK_ASSEMBLY_create_empty + PK_INSTANCE_create",
        "Two instances reference one shared solid body with independent transforms.",
        new[] { "assembly", "instance/multiple", "part/shared", "transform/translation" },
        CreateSharedTwoInstanceAssembly,
        new CorpusAssemblyCounts(2, 2),
        "{\"instances\":2,\"parts\":1,\"sharedPart\":true}",
        new[] { "assembly.multiple-instances", "assembly.shared-part" }),
};

return ParasolidXtCorpusHost.RunAssemblyGroup("assembly", cases, args);

static unsafe PK_ASSEMBLY_t CreateEmptyAssembly()
{
    PK_ASSEMBLY_t assembly;
    ParasolidXtCorpusHost.Check(PK_ASSEMBLY_create_empty(&assembly), "PK_ASSEMBLY_create_empty");
    return assembly;
}

static unsafe PK_ASSEMBLY_t CreateSingleBlockInstance()
{
    PK_ASSEMBLY_t assembly;
    ParasolidXtCorpusHost.Check(PK_ASSEMBLY_create_empty(&assembly), "PK_ASSEMBLY_create_empty");

    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(instance part)");

    var transformForm = new PK_TRANSF_sf_t(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create(&transformForm, &transform), "PK_TRANSF_create(identity)");

    var instanceForm = new PK_INSTANCE_sf_t(assembly, transform, body);
    PK_INSTANCE_t instance;
    ParasolidXtCorpusHost.Check(PK_INSTANCE_create(&instanceForm, &instance), "PK_INSTANCE_create");
    return assembly;
}

static unsafe PK_ASSEMBLY_t CreateLevelizedAssembly()
{
    var assembly = CreateSingleBlockInstance();
    PK_ASSEMBLY_t levelAssembly;
    ParasolidXtCorpusHost.Check(PK_ASSEMBLY_make_level_assembly(assembly, &levelAssembly), "PK_ASSEMBLY_make_level_assembly");
    return levelAssembly;
}

static unsafe PK_ASSEMBLY_t CreateTransformedAssembly()
{
    var assembly = CreateSingleBlockInstance();
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(
        PK_TRANSF_create_translation(new PK_VECTOR_t(2.0, 1.0, 0.0), &transform),
        "PK_TRANSF_create_translation assembly");
    ParasolidXtCorpusHost.Check(PK_ASSEMBLY_transform(assembly, transform), "PK_ASSEMBLY_transform");
    return assembly;
}

static unsafe PK_ASSEMBLY_t CreateInstanceTransformedAssembly()
{
    var (assembly, instance) = CreateAssemblyAndInstance();
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create_translation(new PK_VECTOR_t(1.0, 0.0, 2.0), &transform), "PK_TRANSF_create_translation instance");
    ParasolidXtCorpusHost.Check(PK_INSTANCE_transform(instance, transform), "PK_INSTANCE_transform");
    return assembly;
}

static unsafe PK_ASSEMBLY_t CreateInstanceReplacedTransformAssembly()
{
    var (assembly, instance) = CreateAssemblyAndInstance();
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create_translation(new PK_VECTOR_t(-1.0, 2.0, 0.0), &transform), "PK_TRANSF_create_translation replace");
    ParasolidXtCorpusHost.Check(PK_INSTANCE_replace_transf(instance, transform), "PK_INSTANCE_replace_transf");
    return assembly;
}

static unsafe (PK_ASSEMBLY_t Assembly, PK_INSTANCE_t Instance) CreateAssemblyAndInstance()
{
    PK_ASSEMBLY_t assembly;
    ParasolidXtCorpusHost.Check(PK_ASSEMBLY_create_empty(&assembly), "PK_ASSEMBLY_create_empty instance helper");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block instance helper");
    var identity = new PK_TRANSF_sf_t(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create(&identity, &transform), "PK_TRANSF_create instance helper");
    var instanceForm = new PK_INSTANCE_sf_t(assembly, transform, body);
    PK_INSTANCE_t instance;
    ParasolidXtCorpusHost.Check(PK_INSTANCE_create(&instanceForm, &instance), "PK_INSTANCE_create instance helper");
    return (assembly, instance);
}

static unsafe PK_ASSEMBLY_t CreateReflectedAssembly()
{
    PK_ASSEMBLY_t assembly;
    ParasolidXtCorpusHost.Check(PK_ASSEMBLY_create_empty(&assembly), "PK_ASSEMBLY_create_empty reflection");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block reflection");
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create_reflection(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(1.0, 0.0, 0.0), &transform), "PK_TRANSF_create_reflection");
    var instanceForm = new PK_INSTANCE_sf_t(assembly, transform, body);
    PK_INSTANCE_t instance;
    ParasolidXtCorpusHost.Check(PK_INSTANCE_create(&instanceForm, &instance), "PK_INSTANCE_create reflection");
    return assembly;
}

static unsafe PK_ASSEMBLY_t CreateRotatedAssembly()
{
    var (assembly, instance) = CreateAssemblyAndInstance();
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create_rotation(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0), 0.5, &transform), "PK_TRANSF_create_rotation assembly instance");
    ParasolidXtCorpusHost.Check(PK_INSTANCE_transform(instance, transform), "PK_INSTANCE_transform rotation");
    return assembly;
}

static unsafe PK_ASSEMBLY_t CreateSharedTwoInstanceAssembly()
{
    PK_ASSEMBLY_t assembly;
    ParasolidXtCorpusHost.Check(PK_ASSEMBLY_create_empty(&assembly), "PK_ASSEMBLY_create_empty shared instances");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block shared instance part");
    var identity = new PK_TRANSF_sf_t(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
    PK_TRANSF_t firstTransform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create(&identity, &firstTransform), "PK_TRANSF_create shared identity");
    var firstForm = new PK_INSTANCE_sf_t(assembly, firstTransform, body);
    PK_INSTANCE_t first;
    ParasolidXtCorpusHost.Check(PK_INSTANCE_create(&firstForm, &first), "PK_INSTANCE_create shared first");
    PK_TRANSF_t secondTransform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create_translation(new PK_VECTOR_t(3.0, 0.0, 0.0), &secondTransform), "PK_TRANSF_create shared translation");
    var secondForm = new PK_INSTANCE_sf_t(assembly, secondTransform, body);
    PK_INSTANCE_t second;
    ParasolidXtCorpusHost.Check(PK_INSTANCE_create(&secondForm, &second), "PK_INSTANCE_create shared second");
    return assembly;
}

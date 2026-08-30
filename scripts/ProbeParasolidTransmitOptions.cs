#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

if (!ParasolidScriptHost.TryStartSession("Parasolid transmit-options probe", out var session, out var skipMessage))
{
    Console.WriteLine(skipMessage);
    return 0;
}

unsafe
{
    using (session)
    {
        PK_BODY_t body;
        Check(PK_BODY_create_solid_block(1, 2, 3, null, &body), "PK_BODY_create_solid_block");
        for (var version = 0; version <= 6; version++)
        {
            Probe(body, version, "zero-tail", PK_transmit_format_text_c, PK_LOGICAL_false, 0, PK_LOGICAL_false, 0);
            Probe(body, version, "valid-current", PK_transmit_format_text_c, PK_LOGICAL_true, 371, PK_LOGICAL_false, PK_transmit_meshes_separate_c);
            Probe(body, version, "invalid-format", 123, PK_LOGICAL_true, 371, PK_LOGICAL_false, PK_transmit_meshes_separate_c);
            Probe(body, version, "invalid-user-fields", PK_transmit_format_text_c, 2, 371, PK_LOGICAL_false, PK_transmit_meshes_separate_c);
            Probe(body, version, "invalid-version", PK_transmit_format_text_c, PK_LOGICAL_true, -1, PK_LOGICAL_false, PK_transmit_meshes_separate_c);
            Probe(body, version, "invalid-nmnl", PK_transmit_format_text_c, PK_LOGICAL_true, 371, 2, PK_transmit_meshes_separate_c);
            Probe(body, version, "zero-meshes", PK_transmit_format_text_c, PK_LOGICAL_true, 371, PK_LOGICAL_false, 0);
            Probe(body, version, "embedded-meshes", PK_transmit_format_text_c, PK_LOGICAL_true, 371, PK_LOGICAL_false, PK_transmit_meshes_embedded_c);
            Probe(body, version, "invalid-meshes", PK_transmit_format_text_c, PK_LOGICAL_true, 371, PK_LOGICAL_false, 123);
        }
    }
}
return 0;

static unsafe void Probe(
    PK_PART_t body,
    int optionsVersion,
    string label,
    PK_transmit_format_t format,
    PK_LOGICAL_t userFields,
    int transmitVersion,
    PK_LOGICAL_t nominalGeometry,
    PK_transmit_meshes_t meshes)
{
    var options = new PK_PART_transmit_o_t
    {
        o_t_version = optionsVersion,
        transmit_format = format,
        transmit_user_fields = userFields,
        transmit_version = transmitVersion,
        transmit_nmnl_geometry = nominalGeometry,
        transmit_meshes = meshes,
    };
    var block = new PK_MEMORY_block_t();
    var error = PK_PART_transmit_b(1, &body, &options, &block);
    Console.WriteLine($"version={optionsVersion} {label}: error={error} bytes={(error == 0 ? block.n_bytes : 0)}");
    if (error == 0)
        Check(PK_MEMORY_block_f(&block), "PK_MEMORY_block_f");
}

static void Check(int error, string operation)
{
    if (error != PK_ERROR_no_errors)
        throw new InvalidOperationException(operation + " failed with error " + error);
}

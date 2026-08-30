#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

if (!ParasolidScriptHost.TryStartSession("Parasolid receive-options probe", out var session, out var skipMessage))
{
    Console.WriteLine(skipMessage);
    return 0;
}

unsafe
{
    using (session)
    {
        PK_BODY_t blockBody;
        PK_BODY_t sphereBody;
        Check(PK_BODY_create_solid_block(1, 2, 3, null, &blockBody), "PK_BODY_create_solid_block");
        Check(PK_BODY_create_solid_sphere(2, null, &sphereBody), "PK_BODY_create_solid_sphere");
        int blockIdentifier;
        int sphereIdentifier;
        Check(PK_ENTITY_ask_identifier(blockBody, &blockIdentifier), "PK_ENTITY_ask_identifier block");
        Check(PK_ENTITY_ask_identifier(sphereBody, &sphereIdentifier), "PK_ENTITY_ask_identifier sphere");
        Console.WriteLine($"source identifiers: block={blockIdentifier} sphere={sphereIdentifier}");
        var sourceParts = stackalloc PK_PART_t[2] { blockBody, sphereBody };
        var source = Transmit(2, sourceParts);

        Probe(source, "default", 0, []);
        for (var partIndex = -1; partIndex <= 4; partIndex++)
            Probe(source, "part_index=" + partIndex, partIndex, []);
        Probe(source, "indices=[]", 0, []);
        Probe(source, "indices=[0]", 0, [0]);
        Probe(source, "indices=[1]", 0, [1]);
        Probe(source, "indices=[2]", 0, [2]);
        Probe(source, "indices=[3]", 0, [3]);
        Probe(source, "indices=[0,1]", 0, [0, 1]);
        Probe(source, "indices=[1,0]", 0, [1, 0]);
        Probe(source, "indices=[1,2]", 0, [1, 2]);
        Probe(source, "indices=[2,1]", 0, [2, 1]);
        Probe(source, "indices=[1,1]", 0, [1, 1]);
        Probe(source, "part_index=1 indices=[2]", 1, [2]);
        ProbeIdentifiers(source, "identifiers=[block]", [blockIdentifier]);
        ProbeIdentifiers(source, "identifiers=[sphere]", [sphereIdentifier]);
        ProbeIdentifiers(source, "identifiers=[block,sphere]", [blockIdentifier, sphereIdentifier]);
        ProbeIdentifiers(source, "identifiers=[sphere,block]", [sphereIdentifier, blockIdentifier]);
        ProbeIdentifiers(source, "identifiers=[block,block]", [blockIdentifier, blockIdentifier]);
        ProbeIdentifiers(source, "identifiers=[0]", [0]);
        ProbeIdentifiers(source, "identifiers=[max]", [int.MaxValue]);
        ProbeAdvanced(source, "advanced=zero-defaults", 0, 0, 0, PK_LOGICAL_false);
        ProbeAdvanced(source, "attdef=fail", PK_ATTDEF_mismatch_fail_c, PK_receive_using_seek_no_c, PK_receive_mixed_fail_c, PK_LOGICAL_false);
        ProbeAdvanced(source, "attdef=ignore", PK_ATTDEF_mismatch_ignore_c, PK_receive_using_seek_no_c, PK_receive_mixed_fail_c, PK_LOGICAL_false);
        ProbeAdvanced(source, "attdef=invalid", 123, PK_receive_using_seek_no_c, PK_receive_mixed_fail_c, PK_LOGICAL_false);
        ProbeAdvanced(source, "seek=no", PK_ATTDEF_mismatch_ignore_c, PK_receive_using_seek_no_c, PK_receive_mixed_fail_c, PK_LOGICAL_false);
        ProbeAdvanced(source, "seek=yes", PK_ATTDEF_mismatch_ignore_c, PK_receive_using_seek_yes_c, PK_receive_mixed_fail_c, PK_LOGICAL_false);
        ProbeAdvanced(source, "seek=invalid", PK_ATTDEF_mismatch_ignore_c, 123, PK_receive_mixed_fail_c, PK_LOGICAL_false);
        ProbeAdvanced(source, "mixed=fail", PK_ATTDEF_mismatch_ignore_c, PK_receive_using_seek_no_c, PK_receive_mixed_fail_c, PK_LOGICAL_false);
        ProbeAdvanced(source, "mixed=make-facet", PK_ATTDEF_mismatch_ignore_c, PK_receive_using_seek_no_c, PK_receive_mixed_make_facet_c, PK_LOGICAL_false);
        ProbeAdvanced(source, "mixed=allow", PK_ATTDEF_mismatch_ignore_c, PK_receive_using_seek_no_c, PK_receive_mixed_allow_c, PK_LOGICAL_false);
        ProbeAdvanced(source, "mixed=invalid", PK_ATTDEF_mismatch_ignore_c, PK_receive_using_seek_no_c, 123, PK_LOGICAL_false);
        ProbeAdvanced(source, "key-is-partition=true", PK_ATTDEF_mismatch_ignore_c, PK_receive_using_seek_no_c, PK_receive_mixed_fail_c, PK_LOGICAL_true);
        ProbeFormat(source, "format=text", PK_transmit_format_text_c);
        ProbeFormat(source, "format=neutral", PK_transmit_format_neutral_c);
        ProbeFormat(source, "format=binary", PK_transmit_format_binary_c);
        ProbeFormat(source, "format=invalid", 123);
        for (var optionsVersion = 0; optionsVersion <= 14; optionsVersion++)
        {
            ProbeVersionZeros(source, optionsVersion);
            ProbeVersionFields(source, optionsVersion, "valid-current", PK_ATTDEF_mismatch_ignore_c, PK_receive_compound_keep_c, PK_receive_using_seek_no_c, PK_receive_mixed_fail_c);
            ProbeVersionFields(source, optionsVersion, "zero-attdef", 0, PK_receive_compound_keep_c, PK_receive_using_seek_no_c, PK_receive_mixed_fail_c);
            ProbeVersionFields(source, optionsVersion, "zero-compound", PK_ATTDEF_mismatch_ignore_c, 0, PK_receive_using_seek_no_c, PK_receive_mixed_fail_c);
            ProbeVersionFields(source, optionsVersion, "zero-seek", PK_ATTDEF_mismatch_ignore_c, PK_receive_compound_keep_c, 0, PK_receive_mixed_fail_c);
            ProbeVersionFields(source, optionsVersion, "zero-mixed", PK_ATTDEF_mismatch_ignore_c, PK_receive_compound_keep_c, PK_receive_using_seek_no_c, 0);
            ProbeVersionFields(source, optionsVersion, "invalid-attdef", 123, PK_receive_compound_keep_c, PK_receive_using_seek_no_c, PK_receive_mixed_fail_c);
            ProbeVersionFields(source, optionsVersion, "invalid-compound", PK_ATTDEF_mismatch_ignore_c, 123, PK_receive_using_seek_no_c, PK_receive_mixed_fail_c);
            ProbeVersionFields(source, optionsVersion, "invalid-seek", PK_ATTDEF_mismatch_ignore_c, PK_receive_compound_keep_c, 123, PK_receive_mixed_fail_c);
            ProbeVersionFields(source, optionsVersion, "invalid-mixed", PK_ATTDEF_mismatch_ignore_c, PK_receive_compound_keep_c, PK_receive_using_seek_no_c, 123);
            ProbeVersionSelectors(source, optionsVersion, "part-index", 1, [], [], PK_LOGICAL_false);
            ProbeVersionSelectors(source, optionsVersion, "part-indices", 0, [1], [], PK_LOGICAL_false);
            ProbeVersionSelectors(source, optionsVersion, "identifiers", 0, [], [0], PK_LOGICAL_false);
            ProbeVersionSelectors(source, optionsVersion, "key-is-partition", 0, [], [], PK_LOGICAL_true);
        }

        var compoundOptions = new PK_BODY_make_compound_o_t();
        PK_BODY_t compound;
        Check(PK_BODY_make_compound(2, sourceParts, &compoundOptions, &compound), "PK_BODY_make_compound");
        var compoundSource = Transmit(1, &compound);
        ProbeCompound(compoundSource, "compound=split", PK_receive_compound_split_c);
        ProbeCompound(compoundSource, "compound=keep", PK_receive_compound_keep_c);
        ProbeCompound(compoundSource, "compound=fail", PK_receive_compound_fail_c);
    }
}
return 0;

static unsafe byte[] Transmit(int count, PK_PART_t* parts)
{
    var options = new PK_PART_transmit_o_t
    {
        o_t_version = 4,
        transmit_format = PK_transmit_format_text_c,
        transmit_version = 371,
        transmit_meshes = PK_transmit_meshes_separate_c,
    };
    var block = new PK_MEMORY_block_t();
    Check(PK_PART_transmit_b(count, parts, &options, &block), "PK_PART_transmit_b");
    try
    {
        using var stream = new MemoryStream();
        for (var current = &block; current is not null; current = current->next)
        {
            if (current->bytes is not null && current->n_bytes != 0)
                stream.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
        }
        return stream.ToArray();
    }
    finally
    {
        Check(PK_MEMORY_block_f(&block), "PK_MEMORY_block_f");
    }
}

static unsafe void Probe(byte[] source, string label, int partIndex, int[] partIndices)
{
    fixed (byte* sourcePointer = source)
    fixed (int* indicesPointer = partIndices)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)source.Length, sourcePointer);
        var options = new PK_PART_receive_o_t
        {
            o_t_version = 8,
            transmit_format = PK_transmit_format_text_c,
            attdef_mismatch = PK_ATTDEF_mismatch_ignore_c,
            part_index = partIndex,
            n_part_indices = partIndices.Length,
            part_indices = partIndices.Length == 0 ? null : indicesPointer,
            receive_compound = PK_receive_compound_keep_c,
            receive_using_seek = PK_receive_using_seek_no_c,
            receive_mixed = PK_receive_mixed_fail_c,
        };
        int count;
        PK_PART_t* parts;
        var error = PK_PART_receive_b(block, &options, &count, &parts);
        Console.WriteLine($"{label}: error={error} count={(error == 0 ? count : 0)}");
        if (error == 0 && parts is not null)
            CleanupReceived(count, parts);
    }
}

static unsafe void ProbeIdentifiers(byte[] source, string label, int[] identifiers)
{
    fixed (byte* sourcePointer = source)
    fixed (int* identifiersPointer = identifiers)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)source.Length, sourcePointer);
        var options = new PK_PART_receive_o_t
        {
            o_t_version = 8,
            transmit_format = PK_transmit_format_text_c,
            attdef_mismatch = PK_ATTDEF_mismatch_ignore_c,
            n_identifiers = identifiers.Length,
            identifiers = identifiersPointer,
            receive_compound = PK_receive_compound_keep_c,
            receive_using_seek = PK_receive_using_seek_no_c,
            receive_mixed = PK_receive_mixed_fail_c,
        };
        int count;
        PK_PART_t* parts;
        var error = PK_PART_receive_b(block, &options, &count, &parts);
        Console.WriteLine($"{label}: error={error} count={(error == 0 ? count : 0)}");
        if (error == 0 && parts is not null)
            CleanupReceived(count, parts);
    }
}

static unsafe void ProbeCompound(byte[] source, string label, PK_receive_compound_t compoundMode)
{
    fixed (byte* sourcePointer = source)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)source.Length, sourcePointer);
        var options = new PK_PART_receive_o_t
        {
            o_t_version = 8,
            transmit_format = PK_transmit_format_text_c,
            attdef_mismatch = PK_ATTDEF_mismatch_ignore_c,
            receive_compound = compoundMode,
            receive_using_seek = PK_receive_using_seek_no_c,
            receive_mixed = PK_receive_mixed_fail_c,
        };
        int count;
        PK_PART_t* parts;
        var error = PK_PART_receive_b(block, &options, &count, &parts);
        var types = new List<string>();
        if (error == 0)
        {
            for (var index = 0; index < count; index++)
            {
                PK_BODY_type_t bodyType;
                var askError = PK_BODY_ask_type(parts[index], &bodyType);
                types.Add(askError == 0 ? bodyType.ToString() : "ask-error-" + askError);
            }
        }
        Console.WriteLine($"{label}: error={error} count={(error == 0 ? count : 0)} types=[{string.Join(',', types)}]");
        if (error == 0 && parts is not null)
            CleanupReceived(count, parts);
    }
}

static unsafe void ProbeAdvanced(
    byte[] source,
    string label,
    PK_ATTDEF_mismatch_t attdefMismatch,
    PK_receive_using_seek_t receiveUsingSeek,
    PK_receive_mixed_t receiveMixed,
    PK_LOGICAL_t keyIsPartition)
{
    fixed (byte* sourcePointer = source)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)source.Length, sourcePointer);
        var options = new PK_PART_receive_o_t
        {
            o_t_version = 8,
            transmit_format = PK_transmit_format_text_c,
            attdef_mismatch = attdefMismatch,
            key_is_partition = keyIsPartition,
            receive_compound = PK_receive_compound_keep_c,
            receive_using_seek = receiveUsingSeek,
            receive_mixed = receiveMixed,
        };
        int count;
        PK_PART_t* parts;
        var error = PK_PART_receive_b(block, &options, &count, &parts);
        Console.WriteLine($"{label}: error={error} count={(error == 0 ? count : 0)}");
        if (error == 0 && parts is not null)
            CleanupReceived(count, parts);
    }
}

static unsafe void ProbeFormat(byte[] source, string label, PK_transmit_format_t format)
{
    fixed (byte* sourcePointer = source)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)source.Length, sourcePointer);
        var options = new PK_PART_receive_o_t
        {
            o_t_version = 8,
            transmit_format = format,
            attdef_mismatch = PK_ATTDEF_mismatch_ignore_c,
            receive_compound = PK_receive_compound_keep_c,
            receive_using_seek = PK_receive_using_seek_no_c,
            receive_mixed = PK_receive_mixed_fail_c,
        };
        int count;
        PK_PART_t* parts;
        var error = PK_PART_receive_b(block, &options, &count, &parts);
        Console.WriteLine($"{label}: error={error} count={(error == 0 ? count : 0)}");
        if (error == 0 && parts is not null)
            CleanupReceived(count, parts);
    }
}

static unsafe void ProbeVersionZeros(byte[] source, int optionsVersion)
{
    fixed (byte* sourcePointer = source)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)source.Length, sourcePointer);
        var options = new PK_PART_receive_o_t
        {
            o_t_version = optionsVersion,
            transmit_format = PK_transmit_format_text_c,
        };
        int count;
        PK_PART_t* parts;
        var error = PK_PART_receive_b(block, &options, &count, &parts);
        Console.WriteLine($"version={optionsVersion} zero-new-fields: error={error} count={(error == 0 ? count : 0)}");
        if (error == 0 && parts is not null)
            CleanupReceived(count, parts);
    }
}

static unsafe void ProbeVersionFields(
    byte[] source,
    int optionsVersion,
    string label,
    PK_ATTDEF_mismatch_t attdefMismatch,
    PK_receive_compound_t receiveCompound,
    PK_receive_using_seek_t receiveUsingSeek,
    PK_receive_mixed_t receiveMixed)
{
    fixed (byte* sourcePointer = source)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)source.Length, sourcePointer);
        var options = new PK_PART_receive_o_t
        {
            o_t_version = optionsVersion,
            transmit_format = PK_transmit_format_text_c,
            attdef_mismatch = attdefMismatch,
            receive_compound = receiveCompound,
            receive_using_seek = receiveUsingSeek,
            receive_mixed = receiveMixed,
        };
        int count;
        PK_PART_t* parts;
        var error = PK_PART_receive_b(block, &options, &count, &parts);
        Console.WriteLine($"version={optionsVersion} {label}: error={error} count={(error == 0 ? count : 0)}");
        if (error == 0 && parts is not null)
            CleanupReceived(count, parts);
    }
}

static unsafe void ProbeVersionSelectors(
    byte[] source,
    int optionsVersion,
    string label,
    int partIndex,
    int[] partIndices,
    int[] identifiers,
    PK_LOGICAL_t keyIsPartition)
{
    fixed (byte* sourcePointer = source)
    fixed (int* partIndicesPointer = partIndices)
    fixed (int* identifiersPointer = identifiers)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)source.Length, sourcePointer);
        var options = new PK_PART_receive_o_t
        {
            o_t_version = optionsVersion,
            transmit_format = PK_transmit_format_text_c,
            attdef_mismatch = PK_ATTDEF_mismatch_ignore_c,
            part_index = partIndex,
            n_part_indices = partIndices.Length,
            part_indices = partIndices.Length == 0 ? null : partIndicesPointer,
            n_identifiers = identifiers.Length,
            identifiers = identifiers.Length == 0 ? null : identifiersPointer,
            key_is_partition = keyIsPartition,
            receive_compound = PK_receive_compound_keep_c,
            receive_using_seek = PK_receive_using_seek_no_c,
            receive_mixed = PK_receive_mixed_fail_c,
        };
        int count;
        PK_PART_t* parts;
        var error = PK_PART_receive_b(block, &options, &count, &parts);
        Console.WriteLine($"version={optionsVersion} {label}: error={error} count={(error == 0 ? count : 0)}");
        if (error == 0 && parts is not null)
            CleanupReceived(count, parts);
    }
}

static unsafe void CleanupReceived(int count, PK_PART_t* parts)
{
    Check(PK_ENTITY_delete(count, parts), "PK_ENTITY_delete received parts");
    Check(PK_MEMORY_free(parts), "PK_MEMORY_free received parts");
}

static void Check(int error, string operation)
{
    if (error != PK_ERROR_no_errors)
        throw new InvalidOperationException(operation + " failed with error " + error);
}

#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;
using static parasolid;

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDirectory = Path.GetDirectoryName(GetScriptPath()) ?? ".";
if (args.Length == 0)
{
    Console.Error.WriteLine("usage: dotnet run scripts/ParasolidXtFixtureOracle.cs -- PATH [PATH ...]");
    return 2;
}
var paths = args.Select(path => Path.GetFullPath(Path.Combine(scriptDirectory, path))).ToArray();
foreach (var path in paths)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine("fixture is missing: " + Path.GetRelativePath(scriptDirectory, path));
        return 2;
    }
}
var sources = paths.Select(File.ReadAllBytes).ToArray();
var userFieldSize = 0;
for (var index = 0; index < sources.Length; index++)
{
    try
    {
        userFieldSize = Math.Max(userFieldSize, XtCorpusInspection.GetUserFieldSize(sources[index]));
    }
    catch (Exception exception)
    {
        throw new FormatException("managed decode failed for " + Path.GetRelativePath(scriptDirectory, paths[index]) + ": " + exception.Message, exception);
    }
}
if (!ParasolidScriptHost.TryStartSession("Parasolid XT fixture oracle", out var session, out var skipMessage, userFieldLength: userFieldSize))
{
    Console.WriteLine(skipMessage);
    return 0;
}

var failures = 0;
unsafe
{
    using (session)
    {
        Check(PK_SESSION_set_facet_geometry(PK_facet_geometry_all_c), "PK_SESSION_set_facet_geometry(all)");
        for (var fixtureIndex = 0; fixtureIndex < paths.Length; fixtureIndex++)
        {
            var label = Path.GetRelativePath(scriptDirectory, paths[fixtureIndex]);
            try
            {
                var expected = Receive(MemoryPayload(sources[fixtureIndex]), label + " source");
                try
                {
                    var incompatibility = XtCorpusInspection.GetTranscodeIncompatibility(sources[fixtureIndex], "SCH_3701097_37102");
                    if (incompatibility is not null)
                        throw new InvalidOperationException("historical-to-current adapter rejected the fixture: " + incompatibility);
                    var managed = XtCorpusInspection.RoundTrip(sources[fixtureIndex], 0, userFields: true);
                    var actual = Receive(managed, label + " managed");
                    try { CompareParts(expected, actual, label); }
                    finally { FreeParts(actual); }
                }
                finally
                {
                    FreeParts(expected);
                }
                Console.WriteLine("PASS " + label);
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine("FAIL " + label + ": " + exception.Message);
            }
        }
    }
}
return failures == 0 ? 0 : 1;

static unsafe ReceivedParts Receive(byte[] bytes, string label)
{
    fixed (byte* pointer = bytes)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer);
        var options = new PK_PART_receive_o_t
        {
            o_t_version = 8,
            transmit_format = PK_transmit_format_text_c,
            receive_user_fields = PK_LOGICAL_true,
            attdef_mismatch = PK_ATTDEF_mismatch_ignore_c,
            receive_compound = PK_receive_compound_keep_c,
        };
        int count;
        PK_PART_t* parts;
        Check(PK_PART_receive_b(block, &options, &count, &parts), "PK_PART_receive_b " + label);
        return new ReceivedParts(count, parts);
    }
}

static byte[] MemoryPayload(byte[] bytes)
{
    if (bytes.Length < 2 || bytes[0] != (byte)'*' || bytes[1] != (byte)'*')
        return bytes;
    var marker = "**END_OF_HEADER"u8;
    var markerIndex = bytes.AsSpan().IndexOf(marker);
    if (markerIndex < 0)
        throw new FormatException("XT physical file header terminator is missing");
    var payloadOffset = bytes.AsSpan(markerIndex + marker.Length).IndexOf((byte)'\n');
    if (payloadOffset < 0)
        throw new FormatException("XT physical file header has no payload");
    payloadOffset += markerIndex + marker.Length + 1;
    return bytes.AsSpan(payloadOffset).ToArray();
}

static unsafe void CompareParts(ReceivedParts expected, ReceivedParts actual, string label)
{
    if (expected.Count != actual.Count)
        throw new InvalidOperationException($"part count differs: {expected.Count} != {actual.Count}");
    for (var index = 0; index < expected.Count; index++)
    {
        PK_CLASS_t expectedClass;
        PK_CLASS_t actualClass;
        Check(PK_ENTITY_ask_class(expected.Parts[index], &expectedClass), "PK_ENTITY_ask_class expected " + label);
        Check(PK_ENTITY_ask_class(actual.Parts[index], &actualClass), "PK_ENTITY_ask_class actual " + label);
        if (expectedClass != actualClass)
            throw new InvalidOperationException($"part class differs: {expectedClass} != {actualClass}");
        if (expectedClass == PK_CLASS_body)
            CompareBodies(expected.Parts[index], actual.Parts[index], label);
        else if (expectedClass == PK_CLASS_assembly)
            CompareAssemblies(expected.Parts[index], actual.Parts[index], label);
    }
}

static unsafe void CompareBodies(PK_BODY_t expected, PK_BODY_t actual, string label)
{
    PK_BODY_type_t expectedType;
    PK_BODY_type_t actualType;
    Check(PK_BODY_ask_type(expected, &expectedType), "PK_BODY_ask_type expected " + label);
    Check(PK_BODY_ask_type(actual, &actualType), "PK_BODY_ask_type actual " + label);
    if (expectedType != actualType)
        throw new InvalidOperationException($"body type differs: {expectedType} != {actualType}");
    if (expectedType == PK_BODY_type_compound_c)
        throw new NotSupportedException("compound fixture comparison requires child detachment");

    var options = new PK_DEBUG_BODY_compare_o_t
    {
        max_diffs = 64,
        all_tests = PK_LOGICAL_false,
        acc_dev_tests = PK_LOGICAL_false,
        non_match_tests = PK_LOGICAL_false,
    };
    var results = new PK_DEBUG_BODY_compare_r_t();
    Check(PK_DEBUG_BODY_compare(expected, actual, &options, &results), "PK_DEBUG_BODY_compare " + label);
    try
    {
        if (results.global_result != PK_DEBUG_global_res_no_diffs_c)
            throw new InvalidOperationException($"body differs: global={results.global_result} local={results.local_result} diffs={results.n_global_diffs}");
    }
    finally
    {
        Check(PK_DEBUG_BODY_compare_r_f(&results), "PK_DEBUG_BODY_compare_r_f " + label);
    }
}

static unsafe void CompareAssemblies(PK_ASSEMBLY_t expected, PK_ASSEMBLY_t actual, string label)
{
    int expectedCount;
    int actualCount;
    PK_INSTANCE_t* expectedInstances;
    PK_INSTANCE_t* actualInstances;
    Check(PK_ASSEMBLY_ask_instances(expected, &expectedCount, &expectedInstances), "PK_ASSEMBLY_ask_instances expected " + label);
    Check(PK_ASSEMBLY_ask_instances(actual, &actualCount, &actualInstances), "PK_ASSEMBLY_ask_instances actual " + label);
    try
    {
        if (expectedCount != actualCount)
            throw new InvalidOperationException($"assembly instance count differs: {expectedCount} != {actualCount}");
    }
    finally
    {
        if (expectedInstances is not null) Check(PK_MEMORY_free(expectedInstances), "PK_MEMORY_free expected instances");
        if (actualInstances is not null) Check(PK_MEMORY_free(actualInstances), "PK_MEMORY_free actual instances");
    }
}

static unsafe void FreeParts(ReceivedParts value)
{
    if (value.Parts is not null)
        Check(PK_MEMORY_free(value.Parts), "PK_MEMORY_free parts");
}

static void Check(int error, string operation)
{
    if (error != PK_ERROR_no_errors)
        throw new InvalidOperationException(operation + " failed with error " + error);
}

readonly unsafe struct ReceivedParts
{
    public ReceivedParts(int count, PK_PART_t* parts)
    {
        Count = count;
        Parts = parts;
    }

    public int Count { get; }
    public PK_PART_t* Parts { get; }
}

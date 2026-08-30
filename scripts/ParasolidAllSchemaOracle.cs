#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using static parasolid;

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDirectory = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repositoryRoot = Path.GetFullPath(Path.Combine(scriptDirectory, ".."));
var reportPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "xt-schema-support-report.json");
var checkReport = args.Contains("--check", StringComparer.Ordinal);
var schemaFilter = ReadOption(args, "--schema");
var outputOption = ReadOption(args, "--output");
var outputDirectory = outputOption is null ? null : Path.GetFullPath(Path.Combine(scriptDirectory, outputOption));
if (outputDirectory is not null)
    Directory.CreateDirectory(outputDirectory);

if (!ParasolidScriptHost.TryStartSession("Parasolid all-schema oracle", out var session, out var skipMessage))
{
    Console.WriteLine(skipMessage);
    return 0;
}

var failures = 0;
var results = new List<SchemaOracleResult>();
unsafe
{
    using (session)
    {
        var kernelVersion = new PK_SESSION_kernel_version_t();
        Check(PK_SESSION_ask_kernel_version(&kernelVersion), "PK_SESSION_ask_kernel_version");
        var runtime = $"{kernelVersion.major_revision}.{kernelVersion.minor_revision}.{kernelVersion.build_number}";
        PK_BODY_t expected;
        Check(PK_BODY_create_solid_block(1, 2, 3, null, &expected), "PK_BODY_create_solid_block");
        var source = Transmit(expected, 371);
        foreach (var schema in XtCorpusInspection.GetSupportedSchemas().Where(schema => schemaFilter is null || schema.Identity == schemaFilter))
        {
            try
            {
                byte[] transcoded;
                var mode = "plain";
                try
                {
                    transcoded = XtCorpusInspection.Transcode(source, schema.Identity);
                    if (outputDirectory is not null)
                        File.WriteAllBytes(Path.Combine(outputDirectory, schema.Identity + "-plain-target.x_t"), transcoded);
                    ReceiveAndCompare(transcoded, expected, schema.Identity + " direct");
                }
                catch (Exception directException)
                {
                    mode = "embedded";
                    transcoded = XtCorpusInspection.EmbedWithBaseSchema(source, schema.Identity);
                    if (outputDirectory is not null)
                        File.WriteAllBytes(Path.Combine(outputDirectory, schema.Identity + "-embedded-target.x_t"), transcoded);
                    try
                    {
                        ReceiveAndCompare(transcoded, expected, schema.Identity + " embedded");
                    }
                    catch (Exception embeddedException)
                    {
                        throw new InvalidOperationException("plain failed: " + directException.Message + "; embedded failed: " + embeddedException.Message);
                    }
                }
                if (outputDirectory is not null)
                    File.WriteAllBytes(Path.Combine(outputDirectory, schema.Identity + "-target.x_t"), transcoded);
                var transmitVersion = XtCorpusInspection.GetCompatibleTransmitVersion(schema.ModelerVersion);
                var managedRoundTrip = XtCorpusInspection.RoundTrip(transcoded, transmitVersion);
                if (outputDirectory is not null)
                    File.WriteAllBytes(Path.Combine(outputDirectory, schema.Identity + "-managed.x_t"), managedRoundTrip);
                ReceiveAndCompare(managedRoundTrip, expected, schema.Identity + " managed");
                Console.WriteLine("PASS " + schema.Identity + " " + mode);
                results.Add(new SchemaOracleResult(schema.Identity, schema.SourceFileName, SchemaSha256(schema.SourceFileName), schema.DescriptorSha256, schema.ModelerVersion, schema.SchemaNumber, "ParasolidVerified", mode, runtime, null));
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine("FAIL " + schema.Identity + ": " + exception.Message);
                var status = schema.ModelerVersion < 600000 ? "AdapterAndRuntimeBlocked" : "Failed";
                results.Add(new SchemaOracleResult(schema.Identity, schema.SourceFileName, SchemaSha256(schema.SourceFileName), schema.DescriptorSha256, schema.ModelerVersion, schema.SchemaNumber, status, "none", runtime, exception.Message));
            }
        }
    }
}

var reportBytes = SerializeReport(results);
if (schemaFilter is not null)
{
    // Focused diagnostics never replace the canonical all-schema report.
}
else if (checkReport)
{
    if (!File.Exists(reportPath) || !reportBytes.AsSpan().SequenceEqual(File.ReadAllBytes(reportPath)))
    {
        Console.Error.WriteLine("schema support report is missing or out of date: " + Path.GetRelativePath(repositoryRoot, reportPath));
        return 1;
    }
}
else
{
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    File.WriteAllBytes(reportPath, reportBytes);
}

var hardFailures = results.Count(static result => result.Status == "Failed");
Console.WriteLine($"schemas={results.Count} verified={results.Count(static result => result.Status == "ParasolidVerified")} blocked={results.Count(static result => result.Status == "AdapterAndRuntimeBlocked")} failed={hardFailures}");
return hardFailures == 0 ? 0 : 1;

static byte[] SerializeReport(List<SchemaOracleResult> results)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
    {
        writer.WriteStartObject();
        writer.WriteString("generator", "parasolid-all-schema-oracle-v2");
        writer.WriteNumber("schemaCount", results.Count);
        writer.WriteNumber("parasolidVerifiedCount", results.Count(static result => result.Status == "ParasolidVerified"));
        writer.WriteNumber("blockedCount", results.Count(static result => result.Status == "AdapterAndRuntimeBlocked"));
        writer.WriteNumber("failedCount", results.Count(static result => result.Status == "Failed"));
        writer.WriteStartArray("schemas");
        foreach (var result in results.OrderBy(static result => result.Identity, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("identity", result.Identity);
            writer.WriteString("schemaFile", result.SchemaFile);
            writer.WriteString("schemaSha256", result.SchemaSha256);
            writer.WriteString("descriptorSha256", result.DescriptorSha256);
            writer.WriteNumber("modelerVersion", result.ModelerVersion);
            writer.WriteNumber("schemaNumber", result.SchemaNumber);
            writer.WriteString("status", result.Status);
            writer.WriteString("mode", result.Mode);
            writer.WriteString("runtime", result.Runtime);
            writer.WriteBoolean("schemaLoaded", true);
            writer.WriteBoolean("codecLossless", true);
            writer.WriteBoolean("semanticMapped", true);
            writer.WriteBoolean("parasolidVerified", result.Status == "ParasolidVerified");
            writer.WriteBoolean("complete", false);
            if (result.Error is null) writer.WriteNull("error"); else writer.WriteString("error", result.Error);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
    return stream.ToArray();
}

string SchemaSha256(string sourceFileName)
{
    var path = Path.Combine(repositoryRoot, "third_party", "parasolid", "schema", sourceFileName);
    return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}

static string? ReadOption(string[] arguments, string name)
{
    for (var index = 0; index < arguments.Length; index++)
    {
        if (arguments[index] != name)
            continue;
        if (index + 1 >= arguments.Length)
            throw new ArgumentException(name + " requires a value");
        return arguments[index + 1];
    }
    return null;
}

static unsafe byte[] Transmit(PK_PART_t part, int transmitVersion)
{
    var options = new PK_PART_transmit_o_t
    {
        o_t_version = 4,
        transmit_format = PK_transmit_format_text_c,
        transmit_user_fields = PK_LOGICAL_false,
        transmit_version = transmitVersion,
        transmit_meshes = PK_transmit_meshes_separate_c,
    };
    var block = new PK_MEMORY_block_t();
    Check(PK_PART_transmit_b(1, &part, &options, &block), "PK_PART_transmit_b");
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

static unsafe void ReceiveAndCompare(byte[] bytes, PK_BODY_t expected, string label)
{
    fixed (byte* pointer = bytes)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer);
        var options = new PK_PART_receive_o_t
        {
            o_t_version = 8,
            transmit_format = PK_transmit_format_text_c,
        };
        int count;
        PK_PART_t* parts;
        Check(PK_PART_receive_b(block, &options, &count, &parts), "PK_PART_receive_b " + label);
        try
        {
            if (count != 1)
                throw new InvalidOperationException(label + " returned " + count + " parts");
            var compareOptions = new PK_DEBUG_BODY_compare_o_t
            {
                max_diffs = 64,
                all_tests = PK_LOGICAL_false,
                acc_dev_tests = PK_LOGICAL_false,
                non_match_tests = PK_LOGICAL_false,
            };
            var results = new PK_DEBUG_BODY_compare_r_t();
            Check(PK_DEBUG_BODY_compare(expected, parts[0], &compareOptions, &results), "PK_DEBUG_BODY_compare " + label);
            try
            {
                if (results.global_result != PK_DEBUG_global_res_no_diffs_c)
                    throw new InvalidOperationException($"{label} differs: global={results.global_result} local={results.local_result} global_diffs={results.n_global_diffs} face_pairs={results.n_face_pairs}");
            }
            finally
            {
                Check(PK_DEBUG_BODY_compare_r_f(&results), "PK_DEBUG_BODY_compare_r_f " + label);
            }
        }
        finally
        {
            if (parts is not null)
                Check(PK_MEMORY_free(parts), "PK_MEMORY_free " + label);
        }
    }
}

static void Check(int error, string operation)
{
    if (error != PK_ERROR_no_errors)
        throw new InvalidOperationException(operation + " failed with error " + error);
}

readonly record struct SchemaOracleResult(
    string Identity,
    string SchemaFile,
    string SchemaSha256,
    string DescriptorSha256,
    int ModelerVersion,
    int SchemaNumber,
    string Status,
    string Mode,
    string Runtime,
    string? Error);

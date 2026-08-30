#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using static parasolid;

static string GetScriptPath([CallerFilePath] string path = "") => path;

var firstVersion = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 0;
var lastVersion = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 400;
var scriptDirectory = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var outputDirectory = args.Length > 2 ? Path.GetFullPath(Path.Combine(scriptDirectory, args[2])) : null;
if (firstVersion < 0 || lastVersion < firstVersion)
{
    Console.Error.WriteLine("usage: dotnet run scripts/ProbeParasolidTransmitVersions.cs -- [FIRST [LAST]]");
    return 2;
}

if (!ParasolidScriptHost.TryStartSession("Parasolid transmit-version probe", out var session, out var skipMessage))
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
        for (var version = firstVersion; version <= lastVersion; version++)
        {
            var options = new PK_PART_transmit_o_t
            {
                o_t_version = 4,
                transmit_format = PK_transmit_format_text_c,
                transmit_user_fields = PK_LOGICAL_false,
                transmit_version = version,
                transmit_nmnl_geometry = PK_LOGICAL_false,
                transmit_indexed_context = null,
                transmit_meshes = PK_transmit_meshes_separate_c,
            };
            var block = new PK_MEMORY_block_t();
            var error = PK_PART_transmit_b(1, &body, &options, &block);
            if (error != PK_ERROR_no_errors)
                continue;
            try
            {
                var text = ReadText(&block);
                Console.WriteLine(version.ToString(CultureInfo.InvariantCulture) + "\t" + ReadSchemaIdentity(text));
                if (outputDirectory is not null)
                {
                    Directory.CreateDirectory(outputDirectory);
                    File.WriteAllText(Path.Combine(outputDirectory, version.ToString(CultureInfo.InvariantCulture) + ".x_t"), text);
                }
            }
            finally
            {
                Check(PK_MEMORY_block_f(&block), "PK_MEMORY_block_f");
            }
        }
    }
}

return 0;

static unsafe string ReadText(PK_MEMORY_block_t* first)
{
    var builder = new StringBuilder();
    for (var current = first; current is not null; current = current->next)
    {
        if (current->bytes is not null && current->n_bytes != 0)
            builder.Append(Encoding.ASCII.GetString(current->bytes, checked((int)current->n_bytes)));
    }
    return builder.ToString();
}

static string ReadSchemaIdentity(string text)
{
    var compact = text.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
    var position = 1;
    var versionLength = ReadLength(compact, ref position);
    position += versionLength;
    var schemaLength = ReadLength(compact, ref position);
    if (position + schemaLength > compact.Length)
        throw new FormatException("Invalid schema identity length in transmit output.");
    return compact.Substring(position, schemaLength);

    static int ReadLength(string value, ref int position)
    {
        while (position < value.Length && char.IsWhiteSpace(value[position])) position++;
        var start = position;
        while (position < value.Length && char.IsAsciiDigit(value[position])) position++;
        if (start == position) throw new FormatException("Missing transmit string length.");
        var result = int.Parse(value.AsSpan(start, position - start), CultureInfo.InvariantCulture);
        while (position < value.Length && char.IsWhiteSpace(value[position])) position++;
        return result;
    }
}

static void Check(int error, string operation)
{
    if (error != PK_ERROR_no_errors)
        throw new InvalidOperationException(operation + " failed with " + error);
}

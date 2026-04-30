#!/usr/bin/env dotnet run
using System.Text;
using System.Text.RegularExpressions;

var repoRoot = Directory.GetCurrentDirectory();
var headerPath = Path.Combine(repoRoot, "docs", "parasolid_inc", "parasolid_kernel.h");
var nativeOutputPath = Path.Combine(repoRoot, "src", "ProjectGmKernel.Native", "Generated", "ParasolidHeader.generated.cs");
var interopOutputPath = Path.Combine(repoRoot, "src", "ProjectGmKernel.Interop", "Generated", "ParasolidNative.generated.cs");

var header = File.ReadAllText(headerPath);

int ExtractIntConstant(string name)
{
    var match = Regex.Match(header, $@"#define\s+{Regex.Escape(name)}\s+\(\([^)]+\)\s+([0-9]+)\)");
    if (!match.Success)
    {
        throw new InvalidOperationException($"Constant not found: {name}");
    }

    return int.Parse(match.Groups[1].Value);
}

static string RenderNativeFile(Dictionary<string, int> constants) => $$"""
using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Generated;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Generated_PK_SESSION_start_o_t
{
    public int o_t_version;
    public byte* journal_file;
    public int user_field;
    public int reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Generated_PK_VECTOR_t
{
    public double x;
    public double y;
    public double z;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Generated_PK_POINT_sf_t
{
    public Generated_PK_VECTOR_t position;
}

internal static class GeneratedParasolidConstants
{
    public const int PK_ENTITY_null = {{constants["PK_ENTITY_null"]}};
    public const int PK_ERROR_not_in_PK = {{constants["PK_ERROR_not_in_PK"]}};
    public const int PK_ERROR_unknown_class = {{constants["PK_ERROR_unknown_class"]}};
    public const int PK_ERROR_bad_field_number = {{constants["PK_ERROR_bad_field_number"]}};
    public const int PK_ERROR_o_t_version_incorrect = {{constants["PK_ERROR_o_t_version_incorrect"]}};
}
""";

static string RenderInteropFile() => """
using System.Runtime.InteropServices;

namespace ProjectGmKernel.Interop.Generated;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PK_SESSION_start_o_t
{
    public int o_t_version;
    public byte* journal_file;
    public int user_field;
    public int reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct PK_VECTOR_t
{
    public double x;
    public double y;
    public double z;
}

[StructLayout(LayoutKind.Sequential)]
public struct PK_POINT_sf_t
{
    public PK_VECTOR_t position;
}

public static unsafe class ParasolidNative
{
    [DllImport("ProjectGmKernel.Native", EntryPoint = "PK_SESSION_start")]
    public static extern int PK_SESSION_start(PK_SESSION_start_o_t* options);

    [DllImport("ProjectGmKernel.Native", EntryPoint = "PK_SESSION_stop")]
    public static extern int PK_SESSION_stop();

    [DllImport("ProjectGmKernel.Native", EntryPoint = "PK_POINT_create")]
    public static extern int PK_POINT_create(PK_POINT_sf_t* pointSf, int* point);

    [DllImport("ProjectGmKernel.Native", EntryPoint = "PK_ENTITY_ask_class")]
    public static extern int PK_ENTITY_ask_class(int entity, int* @class);
}
""";

var constants = new Dictionary<string, int>
{
    ["PK_ENTITY_null"] = ExtractIntConstant("PK_ENTITY_null"),
    ["PK_ERROR_not_in_PK"] = ExtractIntConstant("PK_ERROR_not_in_PK"),
    ["PK_ERROR_unknown_class"] = ExtractIntConstant("PK_ERROR_unknown_class"),
    ["PK_ERROR_bad_field_number"] = ExtractIntConstant("PK_ERROR_bad_field_number"),
    ["PK_ERROR_o_t_version_incorrect"] = ExtractIntConstant("PK_ERROR_o_t_version_incorrect"),
};

Directory.CreateDirectory(Path.GetDirectoryName(nativeOutputPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(interopOutputPath)!);
File.WriteAllText(nativeOutputPath, RenderNativeFile(constants), new UTF8Encoding(false));
File.WriteAllText(interopOutputPath, RenderInteropFile(), new UTF8Encoding(false));

Console.WriteLine("Generated:");
Console.WriteLine(Path.GetRelativePath(repoRoot, nativeOutputPath));
Console.WriteLine(Path.GetRelativePath(repoRoot, interopOutputPath));

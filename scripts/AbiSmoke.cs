#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

const int PK_CLASS_point = 2501;

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repoRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."));
var rid = RuntimeInformation.ProcessArchitecture switch
{
    Architecture.Arm64 when OperatingSystem.IsMacOS() => "osx-arm64",
    Architecture.X64 when OperatingSystem.IsMacOS() => "osx-x64",
    Architecture.Arm64 when OperatingSystem.IsLinux() => "linux-arm64",
    Architecture.X64 when OperatingSystem.IsLinux() => "linux-x64",
    Architecture.X64 when OperatingSystem.IsWindows() => "win-x64",
    _ => throw new NotSupportedException($"Unsupported ABI smoke host: {RuntimeInformation.OSDescription} {RuntimeInformation.ProcessArchitecture}"),
};

var libraryName = OperatingSystem.IsMacOS()
    ? "ProjectGmKernel.Native.dylib"
    : OperatingSystem.IsWindows()
        ? "ProjectGmKernel.Native.dll"
        : "ProjectGmKernel.Native.so";
var libraryPath = Path.Combine(repoRoot, "src", "ProjectGmKernel.Native", "bin", "Release", "net10.0", rid, "publish", libraryName);

if (!File.Exists(libraryPath))
    throw new FileNotFoundException("Native library is missing. Run VerifyKernel.cs or publish first.", libraryPath);

var handle = NativeLibrary.Load(libraryPath);
try
{
    unsafe
    {
        var sessionStart = (delegate* unmanaged[Cdecl]<PK_SESSION_start_o_s*, int>)NativeLibrary.GetExport(handle, "PK_SESSION_start");
        var sessionStop = (delegate* unmanaged[Cdecl]<int>)NativeLibrary.GetExport(handle, "PK_SESSION_stop");
        var pointCreate = (delegate* unmanaged[Cdecl]<PK_POINT_sf_s*, int*, int>)NativeLibrary.GetExport(handle, "PK_POINT_create");
        var entityAskClass = (delegate* unmanaged[Cdecl]<int, int*, int>)NativeLibrary.GetExport(handle, "PK_ENTITY_ask_class");
        var bodyCreateSolidBlock = (delegate* unmanaged[Cdecl]<double, double, double, PK_AXIS2_sf_s*, int*, int>)NativeLibrary.GetExport(handle, "PK_BODY_create_solid_block");
        var bodyAskFaces = (delegate* unmanaged[Cdecl]<int, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_faces");
        var bodyAskTopology = (delegate* unmanaged[Cdecl]<int, PK_BODY_ask_topology_o_s*, int*, nint*, nint*, int*, nint*, nint*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_topology");

        var startOptions = new PK_SESSION_start_o_s { o_t_version = 1 };
        Check(sessionStart(&startOptions), "PK_SESSION_start");

        var pointSf = new PK_POINT_sf_s();
        pointSf.position.coord0 = 1;
        pointSf.position.coord1 = 2;
        pointSf.position.coord2 = 3;
        int point;
        Check(pointCreate(&pointSf, &point), "PK_POINT_create");

        int pointClass;
        Check(entityAskClass(point, &pointClass), "PK_ENTITY_ask_class(point)");
        Require(pointClass == PK_CLASS_point, "point class");

        int body;
        Check(bodyCreateSolidBlock(1, 2, 3, null, &body), "PK_BODY_create_solid_block");

        int faceCount;
        nint faces;
        Check(bodyAskFaces(body, &faceCount, &faces), "PK_BODY_ask_faces");
        Require(faceCount == 6, "solid block face count");
        Require(faces != 0, "faces pointer");

        int topolCount;
        nint topols;
        nint classes;
        int relationCount;
        nint parents;
        nint children;
        nint senses;
        Check(bodyAskTopology(body, null, &topolCount, &topols, &classes, &relationCount, &parents, &children, &senses), "PK_BODY_ask_topology");
        Require(topolCount == 58, "solid block topology count");
        Require(relationCount == 61, "solid block relation count");

        Check(sessionStop(), "PK_SESSION_stop");
    }

    Console.WriteLine("ABI smoke passed");
    return 0;
}
finally
{
    NativeLibrary.Free(handle);
}

static void Check(int error, string name)
{
    if (error != 0)
        throw new InvalidOperationException($"{name} failed with error {error}");
}

static void Require(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException($"Unexpected {name}");
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct PK_SESSION_start_o_s
{
    public int o_t_version;
    public byte* journal_file;
    public int user_field;
    public int reserved;
}

[StructLayout(LayoutKind.Sequential)]
struct PK_VECTOR_s
{
    public double coord0;
    public double coord1;
    public double coord2;
}

[StructLayout(LayoutKind.Sequential)]
struct PK_POINT_sf_s
{
    public PK_VECTOR_s position;
}

[StructLayout(LayoutKind.Sequential)]
struct PK_AXIS2_sf_s
{
    public PK_VECTOR_s location;
    public PK_VECTOR_s axis;
    public PK_VECTOR_s ref_direction;
}

[StructLayout(LayoutKind.Sequential)]
struct PK_BODY_ask_topology_o_s
{
    public int o_t_version;
    public byte want_fins;
    public int frame_handling;
}

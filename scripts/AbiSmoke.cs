#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

const int PK_CLASS_point = 2501;
const int PK_TOPOL_sense_negative_c = 18541;
const int PK_TOPOL_sense_positive_c = 18542;
const int PK_transmit_format_text_c = 18220;
const int PK_transmit_meshes_separate_c = 26130;

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
        var bodyCreateSolidCyl = (delegate* unmanaged[Cdecl]<double, double, PK_AXIS2_sf_s*, int*, int>)NativeLibrary.GetExport(handle, "PK_BODY_create_solid_cyl");
        var bodyAskShells = (delegate* unmanaged[Cdecl]<int, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_shells");
        var bodyAskFaces = (delegate* unmanaged[Cdecl]<int, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_faces");
        var bodyAskEdges = (delegate* unmanaged[Cdecl]<int, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_edges");
        var bodyAskVertices = (delegate* unmanaged[Cdecl]<int, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_vertices");
        var bodyAskRegions = (delegate* unmanaged[Cdecl]<int, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_regions");
        var bodyAskTopology = (delegate* unmanaged[Cdecl]<int, PK_BODY_ask_topology_o_s*, int*, nint*, nint*, int*, nint*, nint*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_topology");
        var regionIsSolid = (delegate* unmanaged[Cdecl]<int, byte*, int>)NativeLibrary.GetExport(handle, "PK_REGION_is_solid");
        var faceAskShells = (delegate* unmanaged[Cdecl]<int, int*, int>)NativeLibrary.GetExport(handle, "PK_FACE_ask_shells");
        var cylCreate = (delegate* unmanaged[Cdecl]<PK_CYL_sf_s*, int*, int>)NativeLibrary.GetExport(handle, "PK_CYL_create");
        var cylAsk = (delegate* unmanaged[Cdecl]<int, PK_CYL_sf_s*, int>)NativeLibrary.GetExport(handle, "PK_CYL_ask");
        var faceAskSurf = (delegate* unmanaged[Cdecl]<int, int*, int>)NativeLibrary.GetExport(handle, "PK_FACE_ask_surf");
        var edgeAskCurve = (delegate* unmanaged[Cdecl]<int, int*, int>)NativeLibrary.GetExport(handle, "PK_EDGE_ask_curve");
        var curveEval = (delegate* unmanaged[Cdecl]<int, double, int, PK_VECTOR_s*, int>)NativeLibrary.GetExport(handle, "PK_CURVE_eval");
        var curveEvalWithTangent = (delegate* unmanaged[Cdecl]<int, double, int, PK_VECTOR_s*, PK_VECTOR_s*, int>)NativeLibrary.GetExport(handle, "PK_CURVE_eval_with_tangent");
        var surfEval = (delegate* unmanaged[Cdecl]<int, PK_UV_s, int, int, byte, PK_VECTOR_s*, int>)NativeLibrary.GetExport(handle, "PK_SURF_eval");
        NativeLibrary.GetExport(handle, "PK_SURF_eval_handed");
        NativeLibrary.GetExport(handle, "PK_SURF_eval_with_normal");
        NativeLibrary.GetExport(handle, "PK_SESSION_register_polling_cb");
        var partTransmitB = (delegate* unmanaged[Cdecl]<int, int*, PK_PART_transmit_o_s*, PK_MEMORY_block_s*, int>)NativeLibrary.GetExport(handle, "PK_PART_transmit_b");
        var partReceiveB = (delegate* unmanaged[Cdecl]<PK_MEMORY_block_s, PK_PART_receive_o_s*, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_PART_receive_b");
        var memoryBlockFree = (delegate* unmanaged[Cdecl]<PK_MEMORY_block_s*, int>)NativeLibrary.GetExport(handle, "PK_MEMORY_block_f");
        var memoryFree = (delegate* unmanaged[Cdecl]<void*, int>)NativeLibrary.GetExport(handle, "PK_MEMORY_free");

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

        int surface;
        Check(faceAskSurf(((int*)faces)[0], &surface), "face surface");
        var derivatives = stackalloc PK_VECTOR_s[9];
        var uv = new PK_UV_s { u = 0.25, v = 0.75 };
        Check(surfEval(surface, uv, 2, 2, 0, derivatives), "PK_SURF_eval by-value UV");
        var du = derivatives[1];
        var dv = derivatives[3];
        Require(Math.Abs(du.coord0 * du.coord0 + du.coord1 * du.coord1 + du.coord2 * du.coord2 - 1) < 1e-12, "plane du");
        Require(Math.Abs(dv.coord0 * dv.coord0 + dv.coord1 * dv.coord1 + dv.coord2 * dv.coord2 - 1) < 1e-12, "plane dv");
        var position = derivatives[0];
        Check(surfEval(surface, new PK_UV_s(), 0, 0, 0, derivatives), "plane origin");
        Require(Math.Abs(position.coord0 - derivatives[0].coord0 - uv.u * du.coord0 - uv.v * dv.coord0) < 1e-12, "UV x");
        Require(Math.Abs(position.coord1 - derivatives[0].coord1 - uv.u * du.coord1 - uv.v * dv.coord1) < 1e-12, "UV y");
        Require(Math.Abs(position.coord2 - derivatives[0].coord2 - uv.u * du.coord2 - uv.v * dv.coord2) < 1e-12, "UV z");
        int evalEdgeCount;
        nint evalEdges;
        Check(bodyAskEdges(body, &evalEdgeCount, &evalEdges), "evaluation edges");
        int curve;
        Check(edgeAskCurve(((int*)evalEdges)[0], &curve), "edge curve");
        Check(curveEval(curve, 0.25, 2, derivatives), "PK_CURVE_eval");
        PK_VECTOR_s tangent;
        Check(curveEvalWithTangent(curve, 0.25, 0, derivatives, &tangent), "PK_CURVE_eval_with_tangent");
        Require(Math.Abs(tangent.coord0 * tangent.coord0 + tangent.coord1 * tangent.coord1 + tangent.coord2 * tangent.coord2 - 1) < 1e-12, "unit tangent");

        int shellCount;
        nint shells;
        Check(bodyAskShells(body, &shellCount, &shells), "PK_BODY_ask_shells(block)");
        Require(shellCount == 2, "solid block shell count");

        int regionCount;
        nint regions;
        Check(bodyAskRegions(body, &regionCount, &regions), "PK_BODY_ask_regions(block)");
        Require(regionCount == 2, "solid block region count");
        byte isSolid;
        Check(regionIsSolid(((int*)regions)[0], &isSolid), "PK_REGION_is_solid(void)");
        Require(isSolid == 0, "first block region void");
        Check(regionIsSolid(((int*)regions)[1], &isSolid), "PK_REGION_is_solid(solid)");
        Require(isSolid == 1, "second block region solid");

        int* faceShells = stackalloc int[2];
        Check(faceAskShells(((int*)faces)[0], faceShells), "PK_FACE_ask_shells(block)");
        Require(faceShells[0] == ((int*)shells)[1], "block face back shell");
        Require(faceShells[1] == ((int*)shells)[0], "block face front shell");

        int topolCount;
        nint topols;
        nint classes;
        int relationCount;
        nint parents;
        nint children;
        nint senses;
        Check(bodyAskTopology(body, null, &topolCount, &topols, &classes, &relationCount, &parents, &children, &senses), "PK_BODY_ask_topology");
        Require(topolCount == 61, "solid block topology count");
        Require(relationCount == 70, "solid block relation count");
        Require(CountSense((int*)senses, relationCount, PK_TOPOL_sense_negative_c) == 6, "solid block negative face uses");
        Require(CountSense((int*)senses, relationCount, PK_TOPOL_sense_positive_c) == 6, "solid block positive face uses");

        int cylBody;
        Check(bodyCreateSolidCyl(2, 5, null, &cylBody), "PK_BODY_create_solid_cyl");
        Check(bodyAskRegions(cylBody, &regionCount, &regions), "PK_BODY_ask_regions(cylinder)");
        Require(regionCount == 2, "cylinder region count");
        Check(bodyAskShells(cylBody, &shellCount, &shells), "PK_BODY_ask_shells(cylinder)");
        Require(shellCount == 2, "cylinder shell count");
        Check(bodyAskFaces(cylBody, &faceCount, &faces), "PK_BODY_ask_faces(cylinder)");
        Require(faceCount == 3, "cylinder face count");
        Check(bodyAskEdges(cylBody, &faceCount, &faces), "PK_BODY_ask_edges(cylinder)");
        Require(faceCount == 2, "cylinder edge count");
        Check(bodyAskVertices(cylBody, &faceCount, &faces), "PK_BODY_ask_vertices(cylinder)");
        Require(faceCount == 0, "cylinder vertex count");

        var transmitOptions = new PK_PART_transmit_o_s
        {
            o_t_version = 4,
            transmit_format = PK_transmit_format_text_c,
            transmit_meshes = PK_transmit_meshes_separate_c,
        };
        var memoryBlock = new PK_MEMORY_block_s();
        Check(partTransmitB(1, &cylBody, &transmitOptions, &memoryBlock), "PK_PART_transmit_b(cylinder)");
        Require(memoryBlock.n_bytes > 0, "transmit memory block size");
        Require(memoryBlock.bytes != null, "transmit memory block bytes");

        var receiveOptions = new PK_PART_receive_o_s
        {
            o_t_version = 8,
            transmit_format = PK_transmit_format_text_c,
        };
        int receivedCount;
        nint receivedParts;
        Check(partReceiveB(memoryBlock, &receiveOptions, &receivedCount, &receivedParts), "PK_PART_receive_b(cylinder)");
        Require(receivedCount == 1, "received part count");
        int receivedBody = ((int*)receivedParts)[0];
        Check(bodyAskFaces(receivedBody, &faceCount, &faces), "PK_BODY_ask_faces(received cylinder)");
        Require(faceCount == 3, "received cylinder face count");
        Check(bodyAskVertices(receivedBody, &faceCount, &faces), "PK_BODY_ask_vertices(received cylinder)");
        Require(faceCount == 0, "received cylinder vertex count");
        Check(memoryFree((void*)receivedParts), "PK_MEMORY_free(received parts)");
        Check(memoryBlockFree(&memoryBlock), "PK_MEMORY_block_f");
        Require(memoryBlock.n_bytes == 0, "freed memory block size");

        var cylSf = new PK_CYL_sf_s();
        cylSf.basis_set.location.coord0 = 1;
        cylSf.basis_set.location.coord1 = 2;
        cylSf.basis_set.location.coord2 = 3;
        cylSf.basis_set.axis.coord0 = 0;
        cylSf.basis_set.axis.coord1 = 0;
        cylSf.basis_set.axis.coord2 = 1;
        cylSf.basis_set.ref_direction.coord0 = 1;
        cylSf.basis_set.ref_direction.coord1 = 0;
        cylSf.basis_set.ref_direction.coord2 = 0;
        cylSf.radius = 7;
        int cyl;
        Check(cylCreate(&cylSf, &cyl), "PK_CYL_create");
        var cylAsked = new PK_CYL_sf_s();
        Check(cylAsk(cyl, &cylAsked), "PK_CYL_ask");
        Require(cylAsked.basis_set.location.coord0 == 1, "cylinder location x");
        Require(cylAsked.basis_set.location.coord1 == 2, "cylinder location y");
        Require(cylAsked.basis_set.location.coord2 == 3, "cylinder location z");
        Require(cylAsked.radius == 7, "cylinder radius");

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

static unsafe int CountSense(int* senses, int count, int value)
{
    var found = 0;
    for (var i = 0; i < count; i++)
    {
        if (senses[i] == value)
            found++;
    }

    return found;
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
struct PK_UV_s
{
    public double u;
    public double v;
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

[StructLayout(LayoutKind.Sequential)]
struct PK_CYL_sf_s
{
    public PK_AXIS2_sf_s basis_set;
    public double radius;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct PK_MEMORY_block_s
{
    public PK_MEMORY_block_s* next;
    public nuint n_bytes;
    public byte* bytes;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct PK_PART_transmit_o_s
{
    public int o_t_version;
    public int transmit_format;
    public byte transmit_user_fields;
    public int transmit_version;
    public byte transmit_nmnl_geometry;
    public nint transmit_indexed_context;
    public int transmit_meshes;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct PK_PART_receive_o_s
{
    public int o_t_version;
    public int transmit_format;
    public byte receive_user_fields;
    public int attdef_mismatch;
    public int part_index;
    public int n_part_indices;
    public int* part_indices;
    public int n_identifiers;
    public int* identifiers;
    public nint receive_indexed_context;
    public byte key_is_partition;
    public int receive_compound;
    public int receive_using_seek;
    public int receive_mixed;
}

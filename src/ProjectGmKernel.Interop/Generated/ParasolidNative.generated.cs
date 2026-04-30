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
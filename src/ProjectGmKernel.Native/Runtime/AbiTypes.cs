using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

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

[StructLayout(LayoutKind.Sequential)]
public struct PK_POINT_array_t
{
    public nint array;
    public int length;
}

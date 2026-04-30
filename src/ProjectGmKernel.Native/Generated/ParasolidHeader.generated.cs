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
    public const int PK_ENTITY_null = 0;
    public const int PK_ERROR_not_in_PK = 5001;
    public const int PK_ERROR_unknown_class = 5002;
    public const int PK_ERROR_bad_field_number = 5013;
    public const int PK_ERROR_o_t_version_incorrect = 5043;
}
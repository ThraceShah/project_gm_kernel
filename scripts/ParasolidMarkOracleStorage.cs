using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static parasolid;

// Partitioned Parasolid rollback requires a delta-storage frustrum. This
// oracle-only adapter uses the PKToy callback types; session setup remains in
// ParasolidScriptHost. No ABI declarations or session initialization copied.
static unsafe class MarkOracleStorage
{
    private static readonly Dictionary<uint, MemoryStream> Marks = new();
    private static uint nextDelta;
    public static void Register()
    {
        var callbacks = new PK_DELTA_frustrum_t
        { open_for_write_fn = &OpenWrite, open_for_read_fn = &OpenRead, close_fn = &Close,
            write_fn = &Write, read_fn = &Read, delete_fn = &Delete };
        var error = PK_DELTA_register_callbacks(callbacks);
        if (error != 0) throw new InvalidOperationException($"reference mark storage: error={error}");
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OpenWrite(int mark, uint* delta)
    {
        *delta = ++nextDelta;
        Marks.Add(*delta, new MemoryStream());
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OpenRead(uint delta)
    {
        if (!Marks.TryGetValue(delta, out var stream)) return PK_ERROR_bad_value;
        stream.Position = 0;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Close(uint mark) => 0;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Write(uint mark, uint count, byte* bytes)
    {
        if (!Marks.TryGetValue(mark, out var stream) || count > int.MaxValue) return PK_ERROR_bad_value;
        stream.Write(new ReadOnlySpan<byte>(bytes, (int)count));
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Read(uint mark, uint count, byte* bytes)
    {
        if (!Marks.TryGetValue(mark, out var stream) || count > int.MaxValue || stream.Length - stream.Position < count)
            return PK_ERROR_bad_value;
        stream.ReadExactly(new Span<byte>(bytes, (int)count));
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Delete(uint mark)
    {
        if (Marks.Remove(mark, out var stream)) stream.Dispose();
        return 0;
    }
}

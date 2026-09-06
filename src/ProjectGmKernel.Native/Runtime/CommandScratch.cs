using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Per-thread command scratch arena. Commands borrow workspace slices for one
/// invocation; nested calls use checkpoints and everything returns when the
/// outermost command finishes. Backed by the session's unmanaged scratch page.
/// </summary>
internal unsafe ref struct CommandScratch
{
    private static readonly byte* Unused = (byte*)0;

    private byte* arena;
    private nuint bytes;
    private nuint offset;

    /// <summary>Thread-local arena anchor; re-anchored per command dispatch.</summary>
    [ThreadStatic]
    internal static byte* ThreadArenaBase;
    [ThreadStatic]
    internal static nuint ThreadArenaBytes;
    [ThreadStatic]
    internal static nuint ThreadArenaOffset;

    public static CommandScratch Current => new()
    {
        arena = ThreadArenaBase,
        bytes = ThreadArenaBytes,
        offset = ThreadArenaOffset,
    };

    /// <summary>Borrow a workspace for a nested computation with checkpointing.</summary>
    public Span<double> Take(int doubles)
    {
        var slice = new Span<double>((byte*)0, 0);
        var needed = (nuint)(doubles * sizeof(double));
        if (arena != null && offset + needed <= bytes)
        {
            slice = new Span<double>(arena + offset, doubles);
            offset += needed;
            ThreadArenaOffset = offset;
        }
        return slice;
    }

    public void Return(Span<double> workspace)
    {
        if (workspace.IsEmpty) return;
        offset -= (nuint)(workspace.Length * sizeof(double));
        ThreadArenaOffset = offset;
    }

    public Span<double> AvailableSpanOfDoubles()
        => arena == null ? Span<double>.Empty : new Span<double>(arena + offset, (int)((bytes - offset) / sizeof(double)));

    /// <summary>Bind the calling thread's scratch arena for this command.</summary>
    public static void Bind(SessionData* session, int threadSlot)
    {
        if (session->TryAcquireScratch(threadSlot, out var arena))
        {
            ThreadArenaBase = arena->Base;
            ThreadArenaBytes = arena->Bytes;
            ThreadArenaOffset = 0;
        }
    }

    /// <summary>Release the thread arena at command end.</summary>
    public static void Unbind(SessionData* session, int threadSlot)
    {
        ThreadArenaBase = null;
        ThreadArenaBytes = ThreadArenaOffset = 0;
        if (session != null) session->ReleaseScratch(threadSlot);
    }

    public static bool IsBound => ThreadArenaBase != null;
}

namespace ProjectGmKernel.Native.Runtime;

internal enum ApiId : ushort
{
    SessionStart = 1,
    SessionStop = 2,
    PointCreate = 3,
    EntityAskClass = 4,
    BodyCreateTopology2 = 5,
    BodyAskShells = 6,
    BodyAskFaces = 7,
    BodyAskEdges = 8,
    BodyAskVertices = 9,
    FaceAskLoops = 10,
    FaceAskSurf = 11,
    LoopAskFace = 12,
    LoopAskFins = 13,
    EdgeAskFins = 14,
    EdgeAskCurve = 15,
    VertexAskPoint = 16,
    FinAskEdge = 17,
    FinAskLoop = 18,
    FinAskFace = 19,
    MarkCreate = 20,
    MarkGoto = 21,
    MarkDelete = 22,
    EntityDelete = 23,
    TransfCreate = 24,
    BodyCreateSolidBlock = 25,
    GeneratedStub = 65535,
}

internal enum ConcurrencyKind : byte
{
    Exclusive = 1,
    Concurrent = 2,
    Local = 3,
}

internal enum AccessKind : byte
{
    SessionControl = 1,
    GlobalWrite = 2,
    ReadOnly = 3,
}

internal enum CommandState : byte
{
    Empty = 0,
    Queued = 1,
    Running = 2,
    Completed = 3,
}

internal struct CommandDescriptor
{
    public ApiId ApiId;
    public ConcurrencyKind ConcurrencyKind;
    public AccessKind AccessKind;
    public int SessionId;
    public long SequenceNo;
    public int CallerThreadId;
}

internal struct CommandQueueSlot
{
    public CommandState State;
    public CommandDescriptor Descriptor;
}

internal sealed class SessionDispatchState
{
    private readonly System.Threading.Lock sync = new();
    private readonly CommandQueueSlot[] slots = new CommandQueueSlot[64];
    private long nextSequence;
    private int tail;

    public T Execute<T>(ref CommandDescriptor descriptor, Func<T> action)
    {
        using var scope = sync.EnterScope();

        descriptor.SequenceNo = ++nextSequence;
        descriptor.CallerThreadId = Environment.CurrentManagedThreadId;

        ref var slot = ref slots[tail];
        slot.Descriptor = descriptor;
        slot.State = CommandState.Queued;

        slot.State = CommandState.Running;
        try
        {
            return action();
        }
        finally
        {
            slot.State = CommandState.Completed;
            tail++;
            if (tail == slots.Length)
            {
                tail = 0;
            }
        }
    }
}

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
    BodyAskTopology = 26,
    BodyAskRegions = 27,
    RegionIsSolid = 28,
    FaceAskShells = 29,
    BodyCreateSolidCyl = 30,
    CylCreate = 31,
    CylAsk = 32,
    PartTransmitB = 33,
    PartReceiveB = 34,
    MemoryBlockFree = 35,
    MemoryFree = 36,
    EntityAskPartition = 37,
    SessionAskCurrentPartition = 38,
    BodyCreateSolidCone = 39,
    BodyCreateSolidPrism = 40,
    BodyCreateSolidSphere = 41,
    BodyCreateSolidTorus = 42,
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
    public PartitionSlot PartitionId;
    public long SequenceNo;
    public int CallerThreadId;
}

internal struct CommandQueueSlot
{
    public CommandState State;
    public CommandDescriptor Descriptor;
}

internal interface IKernelCommand
{
    int Execute();
}

internal sealed class SessionDispatchState
{
    private readonly System.Threading.Lock sync = new();
    private readonly CommandQueueSlot[] slots = new CommandQueueSlot[64];
    private long nextSequence;
    private int tail;

    public int Execute<TCommand>(ref CommandDescriptor descriptor, ref TCommand command)
        where TCommand : struct, IKernelCommand
    {
        using var scope = sync.EnterScope();

        ref var slot = ref Enqueue(ref descriptor);

        slot.State = CommandState.Running;
        try
        {
            return command.Execute();
        }
        finally
        {
            Complete(ref slot);
        }
    }

    private ref CommandQueueSlot Enqueue(ref CommandDescriptor descriptor)
    {
        descriptor.SequenceNo = ++nextSequence;
        descriptor.CallerThreadId = Environment.CurrentManagedThreadId;

        ref var slot = ref slots[tail];
        slot.Descriptor = descriptor;
        slot.State = CommandState.Queued;
        return ref slot;
    }

    private void Complete(ref CommandQueueSlot slot)
    {
        slot.State = CommandState.Completed;
        tail++;
        if (tail == slots.Length)
            tail = 0;
    }
}

using System.Runtime.CompilerServices;
using System.Threading;

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
    CurveEval = 43,
    CurveEvalWithTangent = 44,
    SurfEval = 45,
    BCurveCreate = 46,
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
    public EntityTag TargetEntity;
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

/// <summary>
/// Command scheduler. The gate protects authorization state only; commands
/// execute on their calling thread outside it.
///
/// Exclusive commands drain the kernel. Concurrent (read-only) commands run
/// simultaneously and against writers of other partitions. Local commands
/// behave as exclusive without locked partitions (Parasolid default), as
/// writers on their declared partition from non-locking threads, and run
/// unclaimed on a partition locked to the calling thread. A queued exclusive
/// blocks new conflicting claims so it cannot be starved.
/// </summary>
internal sealed class SessionDispatchState
{
    private static object Gate => KernelRuntime.SessionDispatchGate;
    private int active;
    private int exclusiveWaiting;
    private bool exclusive;
    private int waiters;
    [ThreadStatic] private static int depth;
    public bool IsExecuting => depth != 0;

    // A waiting PK partition-lock call must not keep the exclusive command
    // claim: its owner needs to enter PK_THREAD_unlock_partitions to wake it.
    internal void WaitForPartitionUnlock()
    {
        active--;
        exclusive = false;
        Monitor.PulseAll(Gate);
        WaitForSignal();
        exclusiveWaiting++;
        try { while (exclusive || active != 0) WaitForSignal(); }
        finally { exclusiveWaiting--; }
        active++;
        exclusive = true;
    }

    private unsafe struct Claim
    {
        internal SessionData* Session;
        internal PartitionSlot Partition;
        internal bool Exclusive;
        internal bool Write;
        internal bool NonLocking;
    }

    public unsafe int Execute<TCommand>(ref CommandDescriptor descriptor, ref TCommand command)
        where TCommand : struct, IKernelCommand
    {
        if (depth != 0) return command.Execute();
        var claim = Acquire(ref descriptor);
        depth = 1;
        try
        {
            return KernelRuntime.ExecuteAuthorized(claim.Session, descriptor.AccessKind,
                descriptor.ApiId, ref command);
        }
        finally
        {
            depth = 0;
            lock (Gate)
            {
                active--;
                if (claim.Exclusive) exclusive = false;
                else if (claim.Session != null)
                {
                    ref var partition = ref claim.Session->Partitions[claim.Partition];
                    if (claim.Write) partition.WriterCount--;
                    else partition.SharedReaders--;
                    if (claim.NonLocking) claim.Session->NonLockingLocalActive--;
                }
                if (waiters != 0) Monitor.PulseAll(Gate);
            }
        }
    }

    private unsafe Claim Acquire(ref CommandDescriptor descriptor)
    {
        lock (Gate)
        {
            var wantsExclusive = descriptor.ConcurrencyKind == ConcurrencyKind.Exclusive
                || descriptor.AccessKind == AccessKind.SessionControl;
            if (wantsExclusive) exclusiveWaiting++;
            try
            {
                while (true)
                {
                    // Read the owner only under the same gate as session stop.
                    // An exclusive stop may have run while this caller waited.
                    var session = KernelRuntime.State.Session;
                    var context = session != null ? KernelRuntime.ThreadContext() : null;
                    var partitionId = context != null ? context->CurrentPartition : 0;
                    if (descriptor.TargetEntity > 0 && session != null)
                    {
                        var entityPartition = KernelRuntime.GetEntityPartition(descriptor.TargetEntity);
                        if (entityPartition >= 0) partitionId = entityPartition;
                    }
                    PartitionSlot partition = 0;
                    if (session != null) session->TryFindPartition(partitionId, out partition);
                    if (partition < 0) partition = 0; // API validation returns bad partition
                    var write = descriptor.AccessKind == AccessKind.GlobalWrite;
                    var owner = session != null ? session->Partitions[partition].LockOwnerThread : 0;
                    var mine = context != null && owner == context->ManagedThreadId;
                    var nonLocking = write && !mine;
                    var canEnter = !exclusive && (wantsExclusive ? active == 0 : exclusiveWaiting == 0);
                    if (!wantsExclusive && session != null)
                    {
                        ref var target = ref session->Partitions[partition];
                        canEnter &= (owner == 0 || mine) && target.WriterCount == 0;
                        if (write) canEnter &= target.SharedReaders == 0;
                        if (nonLocking) canEnter &= session->NonLockingLocalActive == 0;
                    }
                    if (canEnter)
                    {
                        active++;
                        descriptor.PartitionId = partition;
                        descriptor.CallerThreadId = Environment.CurrentManagedThreadId;
                        if (wantsExclusive) exclusive = true;
                        else if (session != null)
                        {
                            if (write) session->Partitions[partition].WriterCount++;
                            else session->Partitions[partition].SharedReaders++;
                            if (nonLocking) session->NonLockingLocalActive++;
                        }
                        return new Claim { Session = session, Partition = partition, Exclusive = wantsExclusive,
                            Write = write, NonLocking = nonLocking };
                    }
                    WaitForSignal();
                }
            }
            finally { if (wantsExclusive) exclusiveWaiting--; }
        }
    }

    private void WaitForSignal()
    {
        waiters++;
        try { Monitor.Wait(Gate); }
        finally { waiters--; }
    }
}

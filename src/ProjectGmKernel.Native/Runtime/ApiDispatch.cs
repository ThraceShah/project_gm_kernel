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
    ThreadChainStart = 47,
    ThreadChainStop = 48,
    AttachGeometry = 49,
    DetachGeometry = 50,
    EllipseCreate = 51,
    TrCurveCreate = 52,
    SpCurveCreate = 53,
    BSurfCreate = 54,
    OffsetCreate = 55,
    SweptCreate = 56,
    SpunCreate = 57,
    GeneratedStub = 65535,
}

internal enum ConcurrencyKind : byte
{
    Exclusive = 1,
    Concurrent = 2,
    Local = 3,
    Unprotected = 4,
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
    private KernelThreadId exclusiveChainOwner;
    private BufferCount concurrentChains;
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
        internal SessionData.ThreadContext* Context;
        internal PartitionSlot Partition;
        internal bool Exclusive;
        internal bool Write;
        internal bool NonLocking;
    }

    public unsafe int Execute<TCommand>(ref CommandDescriptor descriptor, ref TCommand command)
        where TCommand : struct, IKernelCommand
    {
        if (depth != 0) return command.Execute();
        if (descriptor.ConcurrencyKind == ConcurrencyKind.Unprotected)
        {
            // SDK thread introspection must remain available while another
            // thread retains an exclusive chain. Stop shares this gate.
            lock (Gate)
            {
                depth = 1;
                try { return command.Execute(); }
                finally { depth = 0; }
            }
        }
        var claim = Acquire(ref descriptor);
        depth = 1;
        try
        {
            return KernelRuntime.ExecuteAuthorized(claim.Session, claim.Context, descriptor.AccessKind,
                descriptor.ApiId, descriptor.PartitionId, ref command);
        }
        finally
        {
            depth = 0;
            lock (Gate)
            {
                active--;
                var context = claim.Session == KernelRuntime.State.Session ? claim.Context : KernelRuntime.ThreadContext();
                if (context != null)
                {
                    if (descriptor.ApiId != ApiId.ThreadChainStart && context->ChainType != 0 && context->ChainLength > 0)
                        context->ChainRemaining--;
                    ReleaseChain(context);
                    if (context->ChainType != 0 && (context->ChainLength == 0 || context->ChainRemaining > 0))
                    {
                        context->ChainHeld = 1;
                        if (context->ChainType == ProjectGmKernel.Native.Generated.ParasolidConstants.PK_THREAD_chain_exclusive_c)
                            exclusiveChainOwner = context->ManagedThreadId;
                        else concurrentChains++;
                    }
                }
                else { exclusiveChainOwner = 0; concurrentChains = 0; }
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
            var session = KernelRuntime.State.Session;
            var context = session != null ? KernelRuntime.ThreadContext() : null;
            var wantsExclusive = descriptor.ConcurrencyKind == ConcurrencyKind.Exclusive
                || descriptor.AccessKind == AccessKind.SessionControl;
            wantsExclusive |= descriptor.ConcurrencyKind == ConcurrencyKind.Local
                && (context == null || context->LockCount == 0);
            if (context != null)
            {
                wantsExclusive |= context->ChainType == ProjectGmKernel.Native.Generated.ParasolidConstants.PK_THREAD_chain_exclusive_c;
                // Concurrent chains drop their retained read claim around
                // exclusive functions, as required by the SDK.
                if (wantsExclusive && context->ChainType != ProjectGmKernel.Native.Generated.ParasolidConstants.PK_THREAD_chain_exclusive_c)
                    ReleaseChain(context);
            }
            if (wantsExclusive) exclusiveWaiting++;
            try
            {
                while (true)
                {
                    if (context != null && context->ChainType != 0 && context->ChainLength > 0 && context->ChainRemaining == 0)
                        context->ChainRemaining = context->ChainLength;
                    var partitionId = context != null ? context->CurrentPartition : 0;
                    if (descriptor.TargetEntity > 0 && session != null)
                    {
                        var entityPartition = KernelRuntime.GetEntityPartition(descriptor.TargetEntity);
                        if (entityPartition >= 0) partitionId = entityPartition;
                    }
                    // Selection is validated on set; delete/goto repair all
                    // affected contexts before releasing their exclusive claim.
                    PartitionSlot partition = partitionId;
                    var write = descriptor.AccessKind == AccessKind.GlobalWrite;
                    var owner = session != null ? session->Partitions[partition].LockOwnerThread : 0;
                    var mine = context != null && owner == context->ManagedThreadId;
                    var nonLocking = write && !mine;
                    var canEnter = !exclusive && (wantsExclusive ? active == 0 : exclusiveWaiting == 0);
                    var myChain = context != null && context->ChainHeld != 0;
                    canEnter &= exclusiveChainOwner == 0 || (context != null && exclusiveChainOwner == context->ManagedThreadId);
                    if (wantsExclusive) canEnter &= concurrentChains == 0;
                    if (descriptor.ConcurrencyKind == ConcurrencyKind.Local) canEnter &= owner == 0 || mine;
                    if (myChain && !wantsExclusive && !exclusive) canEnter = exclusiveChainOwner == 0;
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
                        if (context != null) context->ExecutionIsExclusive = wantsExclusive ? (byte)1 : (byte)0;
                        descriptor.PartitionId = partition;
                        descriptor.CallerThreadId = Environment.CurrentManagedThreadId;
                        if (wantsExclusive) exclusive = true;
                        else if (session != null)
                        {
                            if (write) session->Partitions[partition].WriterCount++;
                            else session->Partitions[partition].SharedReaders++;
                            if (nonLocking) session->NonLockingLocalActive++;
                        }
                        return new Claim { Session = session, Context = context, Partition = partition, Exclusive = wantsExclusive,
                            Write = write, NonLocking = nonLocking };
                    }
                    WaitForSignal();
                    // Session stop/start may have run while the gate was released.
                    session = KernelRuntime.State.Session;
                    context = session != null ? KernelRuntime.ThreadContext() : null;
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

    private unsafe void ReleaseChain(SessionData.ThreadContext* context)
    {
        if (context->ChainHeld == 0) return;
        if (exclusiveChainOwner == context->ManagedThreadId) exclusiveChainOwner = 0;
        else concurrentChains--;
        context->ChainHeld = 0;
    }
}

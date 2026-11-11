using System.Diagnostics;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

[InlineArray(16)] internal struct ModelingStageTicks{private long value;}
internal struct ModelingProfile
{
    internal long ElapsedTicks,SsiCells,RootCells,Roots,BudgetFailures,SurfacePairs,WindingCalls;
    internal long BoundRequests,BoundEvaluations,BoundCacheHits,BoundTicks;
    internal long JacobianCalls,JacobianDirections,ResidualDirections,JacobianRecordBuilds,JacobianRecordNodes,JacobianRecordRebinds;
    internal ModelingStageTicks Stages;
}

// Optional diagnostics and certified bounds live for one immutable-input
// modeling command only. No cache survives a transform, receive or edit.
internal static unsafe class ModelingPerformance
{
    internal static bool Enabled;
    internal static bool CacheEnabled=true;
    [ThreadStatic] internal static ModelingProfile Last;
    [ThreadStatic] private static Context* current;
    private const BufferCount CacheCapacity=4096;

    internal struct Context
    {
        internal BoundEntry* Entries;
        internal bool Capture;
        internal long Started,StageStarted;
        internal KernelRuntime.RegularBooleanStage Stage;
        internal ModelingProfile Profile;
    }
    internal struct BoundEntry
    {
        internal GeomTag Tag;
        internal EntityGeneration Generation;
        internal KernelSense Sense;
        internal BufferOffset Root;
        internal byte Kind;
        internal SurfaceParameterBox Box;
        internal SurfaceBoundValues Values;
    }

    internal readonly struct Scope:IDisposable
    {
        private readonly Context* previous;
        private readonly Context* context;
        internal Scope(Context* target,bool immutableGeometry=false)
        {
            context=target;previous=current;*target=default;target->Capture=Enabled;
            if(target->Capture)target->Started=target->StageStarted=Stopwatch.GetTimestamp();
            if(CacheEnabled&&immutableGeometry)
            {
                // Separate optional native storage cannot consume the proof
                // solver's fixed scratch budget. Allocation failure is a miss.
                target->Entries=(BoundEntry*)KernelRuntime.State.Session->Memory->TryAllocate((nuint)(CacheCapacity*sizeof(BoundEntry)));
                if(target->Entries!=null)new Span<BoundEntry>(target->Entries,CacheCapacity).Clear();
            }
            current=target;
        }
        public void Dispose()
        {
            if(context->Capture)
            {
                var ended=Stopwatch.GetTimestamp();context->Profile.Stages[(int)context->Stage]+=ended-context->StageStarted;
                context->Profile.ElapsedTicks=ended-context->Started;Last=context->Profile;
                if(previous!=null&&previous->Capture)
                {
                    // The parent's elapsed/stage clocks already include this
                    // child. Merge work counters, never time the API twice.
                    ref var parent=ref previous->Profile;var child=context->Profile;
                    parent.SsiCells+=child.SsiCells;parent.RootCells+=child.RootCells;parent.Roots+=child.Roots;
                    parent.BudgetFailures+=child.BudgetFailures;parent.SurfacePairs+=child.SurfacePairs;parent.WindingCalls+=child.WindingCalls;
                    parent.BoundRequests+=child.BoundRequests;parent.BoundEvaluations+=child.BoundEvaluations;
                    parent.BoundCacheHits+=child.BoundCacheHits;parent.BoundTicks+=child.BoundTicks;
                    parent.JacobianCalls+=child.JacobianCalls;parent.JacobianDirections+=child.JacobianDirections;parent.ResidualDirections+=child.ResidualDirections;
                    parent.JacobianRecordBuilds+=child.JacobianRecordBuilds;parent.JacobianRecordNodes+=child.JacobianRecordNodes;parent.JacobianRecordRebinds+=child.JacobianRecordRebinds;
                }
            }
            else Last=default;
            current=previous;
            if(context->Entries!=null)KernelRuntime.State.Session->Memory->Free(context->Entries);
        }
    }

    internal static void Stage(KernelRuntime.RegularBooleanStage stage)
    {
        if(current==null||!current->Capture)return;
        var now=Stopwatch.GetTimestamp();current->Profile.Stages[(int)current->Stage]+=now-current->StageStarted;
        current->Stage=stage;current->StageStarted=now;
    }
    internal static void Cell(bool root)
    {if(current!=null&&current->Capture){if(root)current->Profile.RootCells++;else current->Profile.SsiCells++;}}
    internal static void Root(){if(current!=null&&current->Capture)current->Profile.Roots++;}
    internal static void BudgetFailure(){if(current!=null&&current->Capture)current->Profile.BudgetFailures++;}
    internal static void Pair(){if(current!=null&&current->Capture)current->Profile.SurfacePairs++;}
    internal static void Winding(){if(current!=null&&current->Capture)current->Profile.WindingCalls++;}
    internal static void Jacobian(BufferCount variables)
    {if(current!=null&&current->Capture){current->Profile.JacobianCalls++;current->Profile.JacobianDirections+=variables;}}
    internal static void ResidualDirection()
    {if(current!=null&&current->Capture)current->Profile.ResidualDirections++;}
    internal static void JacobianRecord(BufferCount nodes,bool rebind)
    {if(current!=null&&current->Capture){if(rebind)current->Profile.JacobianRecordRebinds++;else{current->Profile.JacobianRecordBuilds++;current->Profile.JacobianRecordNodes+=nodes;}}}
    internal static void BoundRequest(){if(current!=null&&current->Capture)current->Profile.BoundRequests++;}
    internal static long StartBound()=>current!=null&&current->Capture?Stopwatch.GetTimestamp():0;
    internal static void EndBound(long started)
    {if(started!=0){current->Profile.BoundEvaluations++;current->Profile.BoundTicks+=Stopwatch.GetTimestamp()-started;}}

    internal static bool TryBound(GeomTag tag,EntityGeneration generation,KernelSense sense,BufferOffset root,bool spine,
        in SurfaceParameterBox box,Span<IntervalNumber> output,byte parameterSide=0)
    {
        if(tag<=0||current==null||current->Entries==null||output.Length<9)return false;
        var kind=parameterSide==0?(spine?(byte)2:(byte)1):(byte)(parameterSide+2);
        ref var entry=ref current->Entries[BoundIndex(tag,generation,sense,root,kind,box)];
        if(entry.Tag!=tag||entry.Generation!=generation||entry.Sense!=sense||entry.Root!=root||entry.Kind!=kind
            ||entry.Box.U0!=box.U0||entry.Box.U1!=box.U1||entry.Box.V0!=box.V0||entry.Box.V1!=box.V1)return false;
        ((ReadOnlySpan<IntervalNumber>)entry.Values).CopyTo(output);
        if(current->Capture)current->Profile.BoundCacheHits++;return true;
    }
    internal static void StoreBound(GeomTag tag,EntityGeneration generation,KernelSense sense,BufferOffset root,bool spine,
        in SurfaceParameterBox box,ReadOnlySpan<IntervalNumber> output,byte parameterSide=0)
    {
        if(tag<=0||current==null||current->Entries==null)return;
        var kind=parameterSide==0?(spine?(byte)2:(byte)1):(byte)(parameterSide+2);
        ref var entry=ref current->Entries[BoundIndex(tag,generation,sense,root,kind,box)];
        entry.Tag=tag;entry.Generation=generation;entry.Sense=sense;entry.Root=root;entry.Kind=kind;entry.Box=box;
        output[..9].CopyTo(entry.Values);
    }
    private static BufferOffset BoundIndex(GeomTag tag,EntityGeneration generation,KernelSense sense,BufferOffset root,byte kind,in SurfaceParameterBox box)
    {
        var hash=BitConverter.DoubleToInt64Bits(box.U0)*31+BitConverter.DoubleToInt64Bits(box.U1)*997
            +BitConverter.DoubleToInt64Bits(box.V0)*8191+BitConverter.DoubleToInt64Bits(box.V1)*131071;
        hash^=(long)tag*2654435761+generation*65537L+sense*257L+root*17L+kind-1;hash^=hash>>32;hash^=hash>>16;
        return (BufferOffset)hash&(CacheCapacity-1);
    }
}

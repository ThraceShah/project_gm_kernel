using System.Diagnostics;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

[InlineArray(16)] internal struct ModelingStageTicks{private long value;}
[InlineArray(16)] internal struct RootUseCounts{private long value;}
internal enum RootIsolationUse:byte{Other,SpatialCurve,NativeSupportCoordinate,SurfaceBoundary,Contact,Meridian,Chain,Seam,SpatialLineBlend,SpatialConicBlend,SpatialImplicitBlend}
[InlineArray(8)] internal struct EvaluationCostCounts{private long value;}
internal enum EvaluationCost:byte{Point,Tube,Frame,IntervalJacobian,SplineJets,BezierHull,IntervalWeight,NumericSpline}
internal struct ModelingProfile
{
    internal EvaluationCostCounts EvaluationCalls,EvaluationElapsed,EvaluationOwnElapsed;
    internal long SplineContractions,SplineStableBoxes,SplineStableUv,AllContractions,AllStableBoxes;
    internal long ElapsedTicks,SsiCells,RootCells,Roots,BudgetFailures,SurfacePairs,WindingCalls;
    internal long BoundRequests,BoundEvaluations,BoundCacheHits,BoundFailureCacheHits,BoundTicks;
    internal long JacobianCalls,JacobianDirections,ResidualDirections,JacobianRecordBuilds,JacobianRecordNodes,JacobianRecordRebinds,DirectSplinePointColumns;
    internal RootUseCounts RootCalls,RootCellsByUse,RootBoundFailures,RootLevelExclusions,RootNonmonotone;
    internal ModelingStageTicks Stages;
}

// Optional diagnostics and certified bounds live for one immutable-input
// modeling command only. No cache survives a transform, receive or edit.
internal static unsafe partial class ModelingPerformance
{
    internal static bool Enabled;
    internal static bool CacheEnabled=true;
    [ThreadStatic] internal static ModelingProfile Last;
    [ThreadStatic] private static Context* current;
    private const BufferCount CacheCapacity=32768,CacheProbeLimit=32;

    internal struct Context
    {
        internal BoundEntry* Entries;
        internal JointTubeEntry* Tubes;
        internal bool TubeAttempted;
        internal bool Capture,CacheAllowed,CacheAttempted;
        internal long Started,StageStarted;
        internal KernelRuntime.RegularBooleanStage Stage;
        internal RootIsolationUse RootUse;
        internal ModelingProfile Profile;
    }
    internal struct BoundEntry
    {
        internal GeomTag Tag;
        internal EntityGeneration Generation;
        internal KernelSense Sense;
        internal BufferOffset Root;
        internal byte Kind;
        internal AlgorithmStatus Status;
        internal double FailureScale;
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
            target->CacheAllowed=CacheEnabled&&immutableGeometry;
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
                    parent.BoundCacheHits+=child.BoundCacheHits;parent.BoundFailureCacheHits+=child.BoundFailureCacheHits;parent.BoundTicks+=child.BoundTicks;
                    parent.JacobianCalls+=child.JacobianCalls;parent.JacobianDirections+=child.JacobianDirections;parent.ResidualDirections+=child.ResidualDirections;parent.DirectSplinePointColumns+=child.DirectSplinePointColumns;
                    parent.JacobianRecordBuilds+=child.JacobianRecordBuilds;parent.JacobianRecordNodes+=child.JacobianRecordNodes;parent.JacobianRecordRebinds+=child.JacobianRecordRebinds;
                    parent.AllContractions+=child.AllContractions;parent.AllStableBoxes+=child.AllStableBoxes;
                    parent.SplineContractions+=child.SplineContractions;parent.SplineStableBoxes+=child.SplineStableBoxes;parent.SplineStableUv+=child.SplineStableUv;
                    for(var i=0;i<8;i++){parent.EvaluationCalls[i]+=child.EvaluationCalls[i];parent.EvaluationElapsed[i]+=child.EvaluationElapsed[i];parent.EvaluationOwnElapsed[i]+=child.EvaluationOwnElapsed[i];}
                    for(var use=0;use<16;use++)
                    {
                        parent.RootCalls[use]+=child.RootCalls[use];parent.RootCellsByUse[use]+=child.RootCellsByUse[use];
                        parent.RootBoundFailures[use]+=child.RootBoundFailures[use];parent.RootLevelExclusions[use]+=child.RootLevelExclusions[use];parent.RootNonmonotone[use]+=child.RootNonmonotone[use];
                    }
                }
            }
            else Last=default;
            current=previous;
            if(context->Entries!=null)KernelRuntime.State.Session->Memory->Free(context->Entries);
            if(context->Tubes!=null)KernelRuntime.State.Session->Memory->Free(context->Tubes);
        }
    }

    internal static void SplineContraction(bool same,bool sameUv,bool spline)
    {if(current!=null&&current->Capture){current->Profile.AllContractions++;if(same)current->Profile.AllStableBoxes++;if(spline){current->Profile.SplineContractions++;if(same)current->Profile.SplineStableBoxes++;if(sameUv)current->Profile.SplineStableUv++;}}}

    internal static void Stage(KernelRuntime.RegularBooleanStage stage)
    {
        if(current==null||!current->Capture)return;
        var now=Stopwatch.GetTimestamp();current->Profile.Stages[(int)current->Stage]+=now-current->StageStarted;
        current->Stage=stage;current->StageStarted=now;
    }
    internal readonly struct RootIsolationScope:IDisposable
    {
        private readonly Context* context;
        private readonly RootIsolationUse previous;
        internal RootIsolationScope(RootIsolationUse use)
        {
            context=current;previous=context==null?RootIsolationUse.Other:context->RootUse;
            if(context!=null){context->RootUse=use;if(context->Capture)context->Profile.RootCalls[(int)use]++;}
        }
        public void Dispose(){if(context!=null)context->RootUse=previous;}
    }
    internal static void Cell(bool root)
    {if(current!=null&&current->Capture){if(root){current->Profile.RootCells++;current->Profile.RootCellsByUse[(int)current->RootUse]++;}else current->Profile.SsiCells++;}}
    internal static void RootBoundFailed(){if(current!=null&&current->Capture)current->Profile.RootBoundFailures[(int)current->RootUse]++;}
    internal static void RootExcluded(){if(current!=null&&current->Capture)current->Profile.RootLevelExclusions[(int)current->RootUse]++;}
    internal static void RootNonmonotone(){if(current!=null&&current->Capture)current->Profile.RootNonmonotone[(int)current->RootUse]++;}
    internal static void Root(){if(current!=null&&current->Capture)current->Profile.Roots++;}
    internal static void BudgetFailure(){if(current!=null&&current->Capture)current->Profile.BudgetFailures++;}
    internal static void Pair(){if(current!=null&&current->Capture)current->Profile.SurfacePairs++;}
    internal static void Winding(){if(current!=null&&current->Capture)current->Profile.WindingCalls++;}
    internal static void Jacobian(BufferCount variables)
    {if(current!=null&&current->Capture){current->Profile.JacobianCalls++;current->Profile.JacobianDirections+=variables;}}
    internal static void ResidualDirection()
    {if(current!=null&&current->Capture)current->Profile.ResidualDirections++;}
    internal static void SplinePointColumns(){if(current!=null&&current->Capture)current->Profile.DirectSplinePointColumns+=3;}
    internal static void JacobianRecord(BufferCount nodes,bool rebind)
    {if(current!=null&&current->Capture){if(rebind)current->Profile.JacobianRecordRebinds++;else{current->Profile.JacobianRecordBuilds++;current->Profile.JacobianRecordNodes+=nodes;}}}
    internal static void BoundRequest(){if(current!=null&&current->Capture)current->Profile.BoundRequests++;}
    internal static long StartBound()=>current!=null&&current->Capture?Stopwatch.GetTimestamp():0;
    internal static void EndBound(long started)
    {if(started!=0){current->Profile.BoundEvaluations++;current->Profile.BoundTicks+=Stopwatch.GetTimestamp()-started;}}

    internal static bool TryBound(GeomTag tag,EntityGeneration generation,KernelSense sense,BufferOffset root,bool spine,
        in SurfaceParameterBox box,Span<IntervalNumber> output,out AlgorithmStatus status,double failureScale,byte parameterSide=0)
    {
        status=AlgorithmStatus.NotRun;
        if(tag<=0||current==null||output.Length<9||!EnsureEntries())return false;
        var kind=parameterSide==0?(spine?(byte)2:(byte)1):(byte)(parameterSide+2);
        var start=BoundIndex(tag,generation,sense,root,kind,box);
        for(var probe=0;probe<CacheProbeLimit;probe++)
        {
            ref var entry=ref current->Entries[(start+probe)&(CacheCapacity-1)];
            if(entry.Tag==0)return false;
            if(!SameBound(entry,tag,generation,sense,root,kind,box))continue;
            if(entry.Status!=AlgorithmStatus.Success&&entry.FailureScale!=failureScale)return false;
            status=entry.Status;
            if(status==AlgorithmStatus.Success)
            {
                ((ReadOnlySpan<IntervalNumber>)entry.Values).CopyTo(output);
                if(current->Capture)current->Profile.BoundCacheHits++;
            }
            else if(current->Capture)current->Profile.BoundFailureCacheHits++;
            return true;
        }
        return false;
    }
    // Negative entries retain failure, never become an empty/root-free proof.
    // Callers admit NotConverged only for fixed-stack analytic systems whose
    // result cannot change with available spline workspace or allocation state.
    internal static void StoreBound(GeomTag tag,EntityGeneration generation,KernelSense sense,BufferOffset root,bool spine,
        in SurfaceParameterBox box,ReadOnlySpan<IntervalNumber> output,AlgorithmStatus status,double failureScale,byte parameterSide=0)
    {
        if(tag<=0||current==null||status is not (AlgorithmStatus.Success or AlgorithmStatus.NotConverged)
            ||status==AlgorithmStatus.NotConverged&&!double.IsFinite(failureScale)||!EnsureEntries())return;
        var kind=parameterSide==0?(spine?(byte)2:(byte)1):(byte)(parameterSide+2);
        var start=BoundIndex(tag,generation,sense,root,kind,box);var selected=start;
        for(var probe=0;probe<CacheProbeLimit;probe++)
        {
            var index=(start+probe)&(CacheCapacity-1);ref var candidate=ref current->Entries[index];
            if(candidate.Tag==0||SameBound(candidate,tag,generation,sense,root,kind,box)){selected=index;break;}
        }
        ref var entry=ref current->Entries[selected];
        entry.Tag=tag;entry.Generation=generation;entry.Sense=sense;entry.Root=root;entry.Kind=kind;entry.Box=box;entry.Status=status;entry.FailureScale=failureScale;
        if(status==AlgorithmStatus.Success)output[..9].CopyTo(entry.Values);
    }
    internal static Span<JointTubeEntry> JointTubeWorkspace()
    {
        const BufferCount capacity=128;
        if(current==null||!current->CacheAllowed)return default;
        if(current->Tubes!=null)return new(current->Tubes,capacity);
        if(current->TubeAttempted)return default;current->TubeAttempted=true;
        current->Tubes=(JointTubeEntry*)KernelRuntime.State.Session->Memory->TryAllocate((nuint)(capacity*sizeof(JointTubeEntry)));
        if(current->Tubes==null)return default;var entries=new Span<JointTubeEntry>(current->Tubes,capacity);entries.Clear();return entries;
    }
    private static bool EnsureEntries()
    {
        if(current->Entries!=null)return true;
        if(!current->CacheAllowed||current->CacheAttempted)return false;
        current->CacheAttempted=true;
        // Separate optional native storage cannot consume the proof solver's
        // fixed scratch budget. Failure stays a miss for this whole command.
        current->Entries=(BoundEntry*)KernelRuntime.State.Session->Memory->TryAllocate((nuint)(CacheCapacity*sizeof(BoundEntry)));
        if(current->Entries==null)return false;
        new Span<BoundEntry>(current->Entries,CacheCapacity).Clear();return true;
    }
    private static bool SameBound(in BoundEntry entry,GeomTag tag,EntityGeneration generation,KernelSense sense,BufferOffset root,byte kind,in SurfaceParameterBox box)
        =>entry.Tag==tag&&entry.Generation==generation&&entry.Sense==sense&&entry.Root==root&&entry.Kind==kind
            &&entry.Box.U0==box.U0&&entry.Box.U1==box.U1&&entry.Box.V0==box.V0&&entry.Box.V1==box.V1;

    private static BufferOffset BoundIndex(GeomTag tag,EntityGeneration generation,KernelSense sense,BufferOffset root,byte kind,in SurfaceParameterBox box)
    {
        var hash=Mix((uint)tag|((ulong)(uint)generation<<32));
        hash=Mix(hash^(uint)sense^((ulong)(uint)root<<32)^((ulong)kind<<56));
        hash=Mix(hash^(ulong)BitConverter.DoubleToInt64Bits(box.U0));hash=Mix(hash^(ulong)BitConverter.DoubleToInt64Bits(box.U1));
        hash=Mix(hash^(ulong)BitConverter.DoubleToInt64Bits(box.V0));hash=Mix(hash^(ulong)BitConverter.DoubleToInt64Bits(box.V1));
        return (BufferOffset)(hash&(CacheCapacity-1));
    }
    private static ulong Mix(ulong value)
    {
        // Avalanche the high mantissa bits too: dyadic subdivision endpoints
        // commonly have zero low bits and collide under a linear XOR hash.
        unchecked{value^=value>>30;value*=0xbf58476d1ce4e5b9UL;value^=value>>27;value*=0x94d049bb133111ebUL;return value^(value>>31);}
    }
}

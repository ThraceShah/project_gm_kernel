using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

// Diagnostic-only instrumentation for the zero-clear attribution experiment
// (branch perf/zero-clear-diagnostic; source base 0ba5a1e7). Counts
// IntervalJacobian entries / cache.Clear reach / successes / failures with
// per-clear Stopwatch ticks, bucketed by n and by whether the program nodes
// carry any FirstUv/SecondUv >= 0, plus BoundSegment entry / evaluated /
// success and expansion/refinement iteration totals.
//
// Contract: no clear, proof, tolerance, budget or evaluation algorithm is
// modified; counters only add increments and two timestamps around the
// existing cache.Clear(). The timer strictly wraps cache.Clear(); module
// registration happens before any kernel call and the first clear's ticks
// are tracked separately so first-call effects can be excluded in analysis.
//
// Threading: every participating thread registers exactly one state object
// on first counter use (one allocation per thread, never per entry); the
// hot path thereafter only reads the TLS reference. The one-shot dump
// aggregates ALL registered states under a lock, so it is correct even when
// ProcessExit fires on a thread other than the computing owner; the dump
// records owner thread ids and the dump thread id. ThreadStatic alone is
// NOT relied on for dump correctness. Dump guarantees: normal return,
// early exit (Environment.Exit) and managed unhandled exceptions run
// ProcessExit handlers; Environment.FailFast, native crashes and signals
// do NOT guarantee a dump (verified/limited in ClearAttributionSmoke).
//
// Counting perturbs wall time: outputs are attribution only, never
// qualified GM/PK performance numbers. Clear ticks can include GC
// suspension or scheduling pauses and are not pure CPU; the dump also
// reports process CPU time and process wall as an upper-bound cross-check.
// BoundSegment failures are derived as entered - succeeded; out-of-range n
// is never clamped and is preserved as a diagnostic count.
internal static partial class JointNestedBlendEvaluation
{
    [InlineArray(128)] internal struct AttributionBuckets{private long value;}

    internal struct BucketState
    {
        internal AttributionBuckets Entered,ClearReached,ClearTicks,Succeeded,Failed;
    }

    internal sealed class ThreadAttribution
    {
        internal int OwnerThreadId;
        internal long JacEntered,ClearReached,ClearTicks,JacSucceeded,JacFailed,OutOfRange;
        internal long FirstClearTicks;internal bool HasFirstClear;
        internal long BoundEntered,BoundEvaluated,BoundSucceeded,ExpansionIterations,RefinementIterations;
        internal BucketState Buckets;
    }

    [ThreadStatic] private static ThreadAttribution? local;
    private static readonly object AttributionGate=new();
    private static readonly List<ThreadAttribution> attributionThreads=[];
    private static int attributionDumped;

    // First access per thread allocates and registers one state object; the
    // hot path performs no per-entry allocation or logging afterwards.
    private static ThreadAttribution Attribution
    {
        get
        {
            var state=local;
            if(state!=null)return state;
            state=new ThreadAttribution{OwnerThreadId=Environment.CurrentManagedThreadId};
            lock(AttributionGate)attributionThreads.Add(state);
            local=state;
            return state;
        }
    }

    private static int AttributionIndex(ThreadAttribution state,BufferCount n,bool hasUv)
    {
        if(n<0||n>63){state.OutOfRange++;return -1;}
        return n*2+(hasUv?1:0);
    }

    private static bool HasAnyUv(ReadOnlySpan<Occurrence> nodes)
    {
        foreach(var node in nodes)if(node.FirstUv>=0||node.SecondUv>=0)return true;
        return false;
    }

    [ModuleInitializer]
    internal static void RegisterAttributionDump()
    {
        AppDomain.CurrentDomain.ProcessExit+=(_,_)=>DumpAttribution();
        // Best-effort evidence preservation: on this runtime a managed
        // unhandled exception aborts without ProcessExit (exit 134), so the
        // handler dumps synchronously on the faulting thread first.
        // Environment.FailFast, native crashes and signals remain outside
        // any dump guarantee (verified in ClearAttributionSmoke).
        AppDomain.CurrentDomain.UnhandledException+=(_,_)=>DumpAttribution();
    }

    internal static void DumpAttribution()
    {
        lock(AttributionGate)
        {
            if(attributionDumped!=0)return;attributionDumped=1;
        }
        var dumpThread=Environment.CurrentManagedThreadId;
        var nanoseconds=1_000_000_000.0/Stopwatch.Frequency;
        using var process=Process.GetCurrentProcess();
        long jacEntered=0,clearReached=0,clearTicks=0,jacSucceeded=0,jacFailed=0,outOfRange=0;
        long firstClearTicks=0;var hasFirstClear=false;
        long boundEntered=0,boundEvaluated=0,boundSucceeded=0,expansion=0,refinement=0;
        List<ThreadAttribution> snapshot;
        lock(AttributionGate)snapshot=[..attributionThreads];
        foreach(var state in snapshot)
        {
            jacEntered+=state.JacEntered;clearReached+=state.ClearReached;clearTicks+=state.ClearTicks;
            jacSucceeded+=state.JacSucceeded;jacFailed+=state.JacFailed;outOfRange+=state.OutOfRange;
            if(state.HasFirstClear&&(!hasFirstClear||state.FirstClearTicks<firstClearTicks)){hasFirstClear=true;firstClearTicks=state.FirstClearTicks;}
            boundEntered+=state.BoundEntered;boundEvaluated+=state.BoundEvaluated;boundSucceeded+=state.BoundSucceeded;
            expansion+=state.ExpansionIterations;refinement+=state.RefinementIterations;
            Console.WriteLine($"ATTRIBUTION thread owner={state.OwnerThreadId} jac-entered={state.JacEntered} clear-reached={state.ClearReached} jac-succeeded={state.JacSucceeded} jac-failed={state.JacFailed} clear-ticks={state.ClearTicks} clear-ns={(long)(state.ClearTicks*nanoseconds)} first-clear-ticks={(state.HasFirstClear?state.FirstClearTicks:0)} bound-entered={state.BoundEntered} bound-evaluated={state.BoundEvaluated} bound-succeeded={state.BoundSucceeded} bound-failed-derived={state.BoundEntered-state.BoundSucceeded} expansion-iters={state.ExpansionIterations} refinement-iters={state.RefinementIterations} out-of-range-n={state.OutOfRange}");
            for(var index=0;index<128;index++)
            {
                if(state.Buckets.Entered[index]==0&&state.Buckets.ClearReached[index]==0)continue;
                Console.WriteLine($"ATTRIBUTION thread={state.OwnerThreadId} bucket n={index/2} uv={(index%2)==1} entered={state.Buckets.Entered[index]} clear-reached={state.Buckets.ClearReached[index]} clear-ticks={state.Buckets.ClearTicks[index]} clear-ns={(long)(state.Buckets.ClearTicks[index]*nanoseconds)} succeeded={state.Buckets.Succeeded[index]} failed={state.Buckets.Failed[index]}");
            }
        }
        Console.WriteLine($"ATTRIBUTION total dump-thread={dumpThread} owners={string.Join(",",snapshot.Select(s=>s.OwnerThreadId))} jac-entered={jacEntered} clear-reached={clearReached} jac-succeeded={jacSucceeded} jac-failed={jacFailed} clear-ticks={clearTicks} clear-ns={(long)(clearTicks*nanoseconds)} first-clear-ticks={(hasFirstClear?firstClearTicks:0)} clear-ticks-excluding-first={clearTicks-(hasFirstClear?firstClearTicks:0)} bound-entered={boundEntered} bound-evaluated={boundEvaluated} bound-succeeded={boundSucceeded} bound-failed-derived={boundEntered-boundSucceeded} expansion-iters={expansion} refinement-iters={refinement} out-of-range-n={outOfRange} process-cpu-ms={process.TotalProcessorTime.TotalMilliseconds:F3} process-wall-ms={(DateTime.UtcNow-process.StartTime.ToUniversalTime()).TotalMilliseconds:F3} stopwatch-frequency={Stopwatch.Frequency}");
    }
}

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

// Diagnostic-only instrumentation for the zero-clear attribution experiment
// (branch perf/zero-clear-diagnostic). Counts IntervalJacobian entries /
// cache.Clear reach / successes / failures with raw per-clear Stopwatch
// ticks, bucketed by n and by whether the program nodes carry any
// FirstUv/SecondUv >= 0, plus BoundSegment entry / evaluated / success and
// expansion/refinement iteration totals.
//
// Contract: no clear, proof, tolerance, budget or evaluation algorithm is
// modified; hook sites only add increments and two timestamps around the
// existing cache.Clear(). Fixed-capacity preallocated static value-type
// state; zero allocation anywhere in the hot path call chain (passes the
// AllocationBanCheck IL guard unchanged); single computing owner thread is
// verified and extra computing threads are rejected and counted (their
// events are not attributed). The dump is one boundary call from
// SessionStop (the oracle's finally always reaches it) and writes raw
// numeric lines only; formatting/analysis is an independent tool outside.
// Dump guarantees: normal return and finally-covered early/exception paths
// reach SessionStop; Environment.FailFast, native crashes and signals carry
// NO dump guarantee. The clear timer strictly wraps cache.Clear(); first
// clear ticks are reported separately. Ticks can include GC/scheduling
// pauses and are raw diagnostics only - never cost shares or speedup
// claims. Counting perturbs wall time; all outputs are attribution only.
internal static partial class JointNestedBlendEvaluation
{
    [InlineArray(128)] internal struct AttributionBuckets{private long value;}

    internal struct BucketState
    {
        internal AttributionBuckets Entered,ClearReached,ClearTicks,Succeeded,Failed;
    }

    internal struct AttributionState
    {
        internal int OwnerThreadId,ForeignThreadTouches;
        internal long JacEntered,ClearReached,ClearTicks,JacSucceeded,JacFailed,OutOfRange;
        internal long FirstClearTicks;internal bool HasFirstClear;
        internal long BoundEntered,BoundEvaluated,BoundSucceeded,ExpansionIterations,RefinementIterations;
        internal BucketState Buckets;
    }

    internal static AttributionState attribution;

    // Owner verification: the first computing thread claims the state;
    // events from any other thread are rejected and only counted, so a
    // ForeignThreadTouches of 0 (expected for the single-threaded session)
    // proves exact attribution.
    private static bool AttributionAllowed()
    {
        var id=Environment.CurrentManagedThreadId;
        if(attribution.OwnerThreadId==0){attribution.OwnerThreadId=id;return true;}
        if(attribution.OwnerThreadId==id)return true;
        attribution.ForeignThreadTouches++;
        return false;
    }

    private static int AttributionIndex(BufferCount n,bool hasUv)
    {
        // No clamping: out-of-range n is preserved as a diagnostic count and
        // never masks bucket corruption.
        if(n<0||n>63){attribution.OutOfRange++;return -1;}
        return n*2+(hasUv?1:0);
    }

    private static bool HasAnyUv(ReadOnlySpan<Occurrence> nodes)
    {
        foreach(var node in nodes)if(node.FirstUv>=0||node.SecondUv>=0)return true;
        return false;
    }

    // One boundary call from SessionStop: raw numeric lines only.
    internal static void DumpAttribution()
    {
        var dumpThread=Environment.CurrentManagedThreadId;
        var nanoseconds=1_000_000_000.0/Stopwatch.Frequency;
        using var process=Process.GetCurrentProcess();
        Console.WriteLine($"ATTRIBUTION total owner-thread={attribution.OwnerThreadId} dump-thread={dumpThread} foreign-thread-touches={attribution.ForeignThreadTouches} jac-entered={attribution.JacEntered} clear-reached={attribution.ClearReached} jac-succeeded={attribution.JacSucceeded} jac-failed={attribution.JacFailed} clear-ticks={attribution.ClearTicks} clear-ns={(long)(attribution.ClearTicks*nanoseconds)} first-clear-ticks={(attribution.HasFirstClear?attribution.FirstClearTicks:0)} clear-ticks-excluding-first={attribution.ClearTicks-(attribution.HasFirstClear?attribution.FirstClearTicks:0)} bound-entered={attribution.BoundEntered} bound-evaluated={attribution.BoundEvaluated} bound-succeeded={attribution.BoundSucceeded} bound-failed-derived={attribution.BoundEntered-attribution.BoundSucceeded} expansion-iters={attribution.ExpansionIterations} refinement-iters={attribution.RefinementIterations} out-of-range-n={attribution.OutOfRange} process-cpu-ms={process.TotalProcessorTime.TotalMilliseconds:F3} stopwatch-frequency={Stopwatch.Frequency}");
        for(var index=0;index<128;index++)
        {
            if(attribution.Buckets.Entered[index]==0&&attribution.Buckets.ClearReached[index]==0)continue;
            Console.WriteLine($"ATTRIBUTION bucket n={index/2} uv={(index%2)==1} entered={attribution.Buckets.Entered[index]} clear-reached={attribution.Buckets.ClearReached[index]} clear-ticks={attribution.Buckets.ClearTicks[index]} clear-ns={(long)(attribution.Buckets.ClearTicks[index]*nanoseconds)} succeeded={attribution.Buckets.Succeeded[index]} failed={attribution.Buckets.Failed[index]}");
        }
    }
}

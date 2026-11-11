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
// existing cache.Clear(). Thread-static state keeps the single-threaded
// session isolated; one-shot ProcessExit dump; no per-entry logging or
// allocation. Counting perturbs wall time, so all numbers are attribution
// only and never qualified GM/PK performance figures. The clear timer can
// include GC suspension or scheduling pauses and is not pure CPU; the dump
// also reports process CPU time and process wall as an upper-bound
// cross-check. Early returns and failures keep counters (per-site hooks);
// BoundSegment failures are derived as entered - succeeded.
internal static partial class JointNestedBlendEvaluation
{
    [InlineArray(128)] internal struct AttributionBuckets{private long value;}

    internal struct AttributionState
    {
        internal AttributionBuckets Entered,ClearReached,ClearTicks,Succeeded,Failed;
    }

    [ThreadStatic] internal static AttributionState State;
    [ThreadStatic] internal static long JacEnteredTotal,ClearReachedTotal,ClearTicksTotal,JacSucceededTotal,JacFailedTotal;
    [ThreadStatic] internal static long BoundEntered,BoundEvaluated,BoundSucceeded,ExpansionIterations,RefinementIterations;
    private static int dumped;

    private static int AttributionIndex(BufferCount n,bool hasUv)=>Math.Clamp(n,0,63)*2+(hasUv?1:0);

    private static bool HasAnyUv(ReadOnlySpan<Occurrence> nodes)
    {
        foreach(var node in nodes)if(node.FirstUv>=0||node.SecondUv>=0)return true;
        return false;
    }

    [ModuleInitializer]
    internal static void RegisterAttributionDump()=>AppDomain.CurrentDomain.ProcessExit+=(_,_)=>DumpAttribution();

    internal static void DumpAttribution()
    {
        if(Interlocked.Exchange(ref dumped,1)!=0)return;
        var nanoseconds=1_000_000_000.0/Stopwatch.Frequency;
        using var process=Process.GetCurrentProcess();
        Console.WriteLine($"ATTRIBUTION total jac-entered={JacEnteredTotal} clear-reached={ClearReachedTotal} jac-succeeded={JacSucceededTotal} jac-failed={JacFailedTotal} clear-ticks={ClearTicksTotal} clear-ns={(long)(ClearTicksTotal*nanoseconds)} bound-entered={BoundEntered} bound-evaluated={BoundEvaluated} bound-succeeded={BoundSucceeded} bound-failed-derived={BoundEntered-BoundSucceeded} expansion-iters={ExpansionIterations} refinement-iters={RefinementIterations} process-cpu-ms={process.TotalProcessorTime.TotalMilliseconds:F3} process-wall-ms={(DateTime.UtcNow-process.StartTime.ToUniversalTime()).TotalMilliseconds:F3} stopwatch-frequency={Stopwatch.Frequency}");
        for(var index=0;index<128;index++)
        {
            if(State.Entered[index]==0&&State.ClearReached[index]==0)continue;
            Console.WriteLine($"ATTRIBUTION bucket n={index/2} uv={(index%2)==1} entered={State.Entered[index]} clear-reached={State.ClearReached[index]} clear-ticks={State.ClearTicks[index]} clear-ns={(long)(State.ClearTicks[index]*nanoseconds)} succeeded={State.Succeeded[index]} failed={State.Failed[index]}");
        }
    }
}

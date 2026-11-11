#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;

// BCL/managed smoke for the zero-clear attribution instrumentation. Mirrors
// the kernel pattern exactly (first-use per-thread registration, one-shot
// locked aggregation dump, ThreadStatic local reference) and validates:
//   --scenario=overhead  empty double-timestamp pair cost + bucket exactness
//   --scenario=normal    counts then Main returns; dump must aggregate
//   --scenario=early     counts then Environment.Exit; dump must fire
//   --scenario=exception counts then throws unhandled; dump must fire
//   --scenario=cross     counts on main AND on a worker thread; the worker
//                        calls Environment.Exit; dump must show BOTH owners
//                        and must not rely on the dump thread's own TLS
//   --scenario=failfast  counts then Environment.FailFast; dump is NOT
//                        guaranteed (observed absence expected)
// Pure BCL; no kernel reference; allocation only at first registration.

var scenario=args.Length>0?args[0].Split('=')[^1]:"overhead";

if(scenario=="overhead")
{
    const long iterations=5_000_000;
    long accumulated=0;
    var warmupStart=Stopwatch.GetTimestamp();
    for(var i=0;i<100_000;i++){var a=Stopwatch.GetTimestamp();accumulated+=Stopwatch.GetTimestamp()-a;}
    var loopStart=Stopwatch.GetTimestamp();
    for(var i=0;i<iterations;i++){var start=Stopwatch.GetTimestamp();accumulated+=Stopwatch.GetTimestamp()-start;}
    var loopTicks=Stopwatch.GetTimestamp()-loopStart;
    var nsPerPair=loopTicks*1_000_000_000.0/Stopwatch.Frequency/iterations;
    Console.WriteLine($"SMOKE timestamp-pair-ns={nsPerPair:F3} accumulated-ticks={accumulated} loop-ticks={loopTicks} stopwatch-frequency={Stopwatch.Frequency}");
    if(accumulated<0||accumulated>loopTicks)throw new InvalidOperationException("accumulated clear-clock ticks must stay within the measured loop wall");
    var buckets=new BucketsWrapped();
    long expectedA=0,expectedB=0;
    for(var i=0;i<iterations;i++){buckets.Entered[(i*7)%128]++;expectedA++;buckets.Ticks[(i*7)%128]+=i&3;expectedB+=i&3;}
    var actualA=0L;for(var i=0;i<128;i++)actualA+=buckets.Entered[i];
    var actualB=0L;for(var i=0;i<128;i++)actualB+=buckets.Ticks[i];
    Console.WriteLine($"SMOKE bucket-exact={(actualA==expectedA&&actualB==expectedB)} entered={actualA} ticks={actualB}");
    if(actualA!=expectedA||actualB!=expectedB)throw new InvalidOperationException("bucket counter pattern lost increments");
    Console.WriteLine("PASS attribution smoke overhead");
    return 0;
}

// Mirror of the kernel attribution pattern.
State.Register();
switch(scenario)
{
    case "normal":
        State.Add(10,20,30);
        Console.WriteLine("SMOKE scenario=normal counted");
        return 0;
    case "early":
        State.Add(1,2,3);
        Console.WriteLine("SMOKE scenario=early counted");
        Environment.Exit(7);
        return 7;
    case "exception":
        State.Add(4,5,6);
        Console.WriteLine("SMOKE scenario=exception counted");
        AppDomain.CurrentDomain.UnhandledException+=(_,_)=>State.Dump();
        throw new InvalidOperationException("smoke unhandled exception");
    case "cross":
        State.Add(100,200,300);
        var worker=new Thread(()=>{State.Add(1,2,3);Console.WriteLine("SMOKE scenario=cross worker counted owner="+Environment.CurrentManagedThreadId);Environment.Exit(0);});
        worker.Start();
        Thread.Sleep(60_000);
        Console.WriteLine("SMOKE scenario=cross main did not expect to survive the worker exit");
        return 9;
    case "failfast":
        State.Add(7,8,9);
        Console.WriteLine("SMOKE scenario=failfast counted");
        Environment.FailFast("smoke failfast");
        return 11;
    default:
        Console.WriteLine("SMOKE unknown scenario "+scenario);
        return 2;
}

internal static class State
{
    [InlineArray(128)] internal struct Bucket128{private long value;}
    internal sealed class ThreadState
    {
        internal int OwnerThreadId;
        internal long Entered,Ticks,Count;
    }
    [ThreadStatic] private static ThreadState? local;
    private static readonly object Gate=new();
    private static readonly List<ThreadState> threads=[];
    private static int dumped;
    internal static void Register()=>AppDomain.CurrentDomain.ProcessExit+=(_,_)=>Dump();
    internal static ThreadState Local
    {
        get
        {
            var state=local;
            if(state!=null)return state;
            state=new ThreadState{OwnerThreadId=Environment.CurrentManagedThreadId};
            lock(Gate)threads.Add(state);
            local=state;
            return state;
        }
    }
    internal static void Add(long entered,long ticks,long count)
    {
        var state=Local;state.Entered+=entered;state.Ticks+=ticks;state.Count+=count;
    }
    internal static void Dump()
    {
        lock(Gate){if(dumped!=0)return;dumped=1;}
        var dumpThread=Environment.CurrentManagedThreadId;
        List<ThreadState> snapshot;
        lock(Gate)snapshot=[..threads];
        long entered=0,ticks=0,count=0;
        foreach(var state in snapshot){entered+=state.Entered;ticks+=state.Ticks;count+=state.Count;
            Console.WriteLine($"SMOKE-DUMP thread owner={state.OwnerThreadId} entered={state.Entered} ticks={state.Ticks} count={state.Count}");}
        Console.WriteLine($"SMOKE-DUMP total dump-thread={dumpThread} owners={string.Join(",",snapshot.Select(s=>s.OwnerThreadId))} entered={entered} ticks={ticks} count={count}");
    }
}

[InlineArray(128)] struct Buckets{private long value;}
struct BucketsWrapped{internal Buckets Entered,Ticks;}

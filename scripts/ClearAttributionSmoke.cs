#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;

// BCL/managed smoke for the zero-clear attribution instrumentation (runs
// before the kernel diagnostic): measures the empty double-timestamp
// overhead that the per-clear timer adds, and validates the bucket counter
// pattern (inline-array indexed increments) for exactness. Pure BCL; no
// kernel reference, no allocation in the measured loops.

const long iterations=5_000_000;

// 1) Empty double-timestamp overhead: accumulate deltas exactly like the
//    instrumentation does around cache.Clear().
long accumulated=0;
var warmupStart=Stopwatch.GetTimestamp();
for(var i=0;i<100_000;i++){var a=Stopwatch.GetTimestamp();accumulated+=Stopwatch.GetTimestamp()-a;}
var loopStart=Stopwatch.GetTimestamp();
for(var i=0;i<iterations;i++){var start=Stopwatch.GetTimestamp();accumulated+=Stopwatch.GetTimestamp()-start;}
var loopTicks=Stopwatch.GetTimestamp()-loopStart;
var nsPerPair=loopTicks*1_000_000_000.0/Stopwatch.Frequency/iterations;
Console.WriteLine($"SMOKE timestamp-pair-ns={nsPerPair:F3} accumulated-ticks={accumulated} loop-ticks={loopTicks} stopwatch-frequency={Stopwatch.Frequency}");
if(accumulated<0||accumulated>loopTicks)throw new InvalidOperationException("accumulated clear-clock ticks must stay within the measured loop wall");

// 2) Bucket counter exactness: the same inline-array pattern the kernel
//    instrumentation uses (63*2+uv index space), incremented at known spots.
var buckets=new Buckets();
long expectedA=0,expectedB=0;
for(var i=0;i<iterations;i++){buckets.Entered[(i*7)%128]++;expectedA++;buckets.Ticks[(i*7)%128]+=i&3;expectedB+=i&3;}
var actualA=0L;for(var i=0;i<128;i++)actualA+=buckets.Entered[i];
var actualB=0L;for(var i=0;i<128;i++)actualB+=buckets.Ticks[i];
Console.WriteLine($"SMOKE bucket-exact={(actualA==expectedA&&actualB==expectedB)} entered={actualA} ticks={actualB}");
if(actualA!=expectedA||actualB!=expectedB)throw new InvalidOperationException("bucket counter pattern lost increments");
Console.WriteLine("PASS attribution smoke");
return 0;

[InlineArray(128)] struct Bucket128{private long value;}
struct Buckets{internal Bucket128 Entered,Ticks;}

#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
var here=Path.GetDirectoryName(Here())!;
var evidence=args.Length>0?Path.GetFullPath(args[0],here):Path.GetFullPath("../../../../temp_docs/zero-attribution-0ba",here);
foreach(var name in new[]{"trace-shortedge.speedscope.json","trace-solidblend.speedscope.json"})
{
    var file=Path.GetFullPath("../performance-review-04c/"+name,evidence);var bytes=File.ReadAllBytes(file);var json=JsonNode.Parse(bytes)!;
    var frames=json["shared"]!["frames"]!.AsArray().Select(n=>n!["name"]!.GetValue<string>()).ToArray();Console.WriteLine($"FILE {name} sha={Convert.ToHexStringLower(SHA256.HashData(bytes))}");
    foreach(var p in json["profiles"]!.AsArray())
    {
        var stack=new List<int>();var last=p!["startValue"]!.GetValue<double>();var leafMs=new Dictionary<string,double>();var zeroSpans=new List<double>();var zeroPath=new Dictionary<string,double>();var gaps=new List<double>();
        foreach(var e in p["events"]!.AsArray())
        {
            var at=e!["at"]!.GetValue<double>();var dt=at-last;if(dt<0)throw new Exception("unordered events");
            if(stack.Count>0&&dt>0)
            {
                var leaf=frames[stack[^1]];leafMs[leaf]=leafMs.GetValueOrDefault(leaf)+dt;gaps.Add(dt);
                var z=stack.FindLastIndex(i=>frames[i].Contains("ZeroMemoryInternal"));
                if(z>=0){zeroSpans.Add(dt);var path=string.Join(" -> ",stack.Skip(Math.Max(0,z-2)).Select(i=>frames[i].Split('(')[0]));zeroPath[path]=zeroPath.GetValueOrDefault(path)+dt;}
            }
            var frame=e["frame"]!.GetValue<int>();if(e["type"]!.GetValue<string>()=="O")stack.Add(frame);else{if(stack.Count==0||stack[^1]!=frame)throw new Exception("stack mismatch");stack.RemoveAt(stack.Count-1);}last=at;
        }
        if(leafMs.Count==0)continue;
        Console.WriteLine($"PROFILE {p["name"]} eventIntervals={gaps.Count} totalMs={gaps.Sum():F6} zeroIntervals={zeroSpans.Count} zeroMs={zeroSpans.Sum():F6} maxInterval={gaps.Max():F6} maxZeroInterval={(zeroSpans.Count>0?zeroSpans.Max():0):F6} finalStack={stack.Count}; intervals are NOT calls or exclusive CPU");
        foreach(var kv in leafMs.OrderByDescending(k=>k.Value).Take(5))Console.WriteLine($"LEAF ms={kv.Value:F6} {kv.Key}");
        foreach(var kv in zeroPath.OrderByDescending(k=>k.Value).Take(3))Console.WriteLine($"ZERO_PATH ms={kv.Value:F6} {kv.Key}");
    }
}
static string Here([CallerFilePath]string p="")=>p;

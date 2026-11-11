#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using static parasolid;

var folder=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,"../temp_docs/independent-1654"));
var original=Path.Combine(folder,"original");
var manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(folder,"archive.json")))!;
var pairs=new[]{
    ("mirror8-single-Fix","temp_docs/short-edge-blend-oracle/active-mirror8/a1-r0.5-t1E-07-reverseFalse-default-input-mirror-s8-native.x_t","temp_docs/short-edge-blend-oracle/active-mirror8/a1-r0.5-t1E-07-reverseFalse-default-input-mirror-s8-self.x_t"),
    ("NURBS-lifecycle","temp_docs/regular-solid-blend-oracle/active-nurbs-lifecycle/plane-nurbs-lifecycle-reference.x_t","temp_docs/regular-solid-blend-oracle/active-nurbs-lifecycle/plane-nurbs-lifecycle.x_t")};
unsafe
{
    Require(ParasolidScriptHost.TryStartSession("1654 independent sealed Mac products",out var host,out var message),message);
    using(host)
    {
        var comparisons=new JsonArray();ParasolidBodyCompare.Observer=o=>comparisons.Add((JsonNode)o);
        foreach(var pair in pairs)
        {
            var a=Receive(pair.Item2);var b=Receive(pair.Item3);
            foreach(var body in new[]{a,b})
            {
                var options=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;
                Check(PK_BODY_check(body,&options,&count,&faults),"full BODY_check");
                try{Require(count==0,$"{pair.Item1} body={body} full faults={count}");}finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free faults");}
            }
            ParasolidBodyCompare.AssertEquivalent(a,b,pair.Item1);
            var expected=Edges(a);var actual=Edges(b);Require(expected.Count==actual.Count,"finite ICurve count");
            var maximum=0.0;
            foreach(var edge in expected)
            {
                var index=actual.FindIndex(other=>(Distance(edge.Start,other.Start)<1e-6&&Distance(edge.End,other.End)<1e-6)||(Distance(edge.Start,other.End)<1e-6&&Distance(edge.End,other.Start)<1e-6));
                Require(index>=0,"finite endpoint pair");var other=actual[index];actual.RemoveAt(index);
                var first=ParasolidBodyCompare.FiniteLength(edge.Curve,edge.Range,16);var second=ParasolidBodyCompare.FiniteLength(other.Curve,other.Range,16);
                Require(Math.Max(first.Upper-second.Lower,second.Upper-first.Lower)<1e-5,"full finite ICurve length interval");
                maximum=Math.Max(maximum,Math.Max(Sample(edge,other),Sample(other,edge)));
            }
            Console.WriteLine($"PRODUCT {pair.Item1} fullChecks=2 zeroFaults=true strictCompare=true finiteICurves={expected.Count} samplesEachDirection=17 maxDistance={maximum:R}");
            PK_ENTITY_t* entities=stackalloc PK_ENTITY_t[2]{a,b};Check(PK_ENTITY_delete(2,entities),"delete reviewed products");
        }
        File.WriteAllText(Path.Combine(folder,"product-comparisons.json"),comparisons.ToJsonString());
    }
}
Console.WriteLine("PASS independent Linux PK receive/full/strict/finite review of sealed Mac products; no GM lifecycle or Mac DLL execution");

unsafe PK_BODY_t Receive(string relative)
{
    var file=Path.Combine(original,relative);var bytes=File.ReadAllBytes(file);var sha=Convert.ToHexStringLower(SHA256.HashData(bytes));
    var entry=manifest["members"]!.AsArray().Single(x=>(string?)x!["path"]==relative)!;Require(sha==(string?)entry["sha256"],"sealed member hash");Console.WriteLine($"XT {relative} SHA256={sha}");
    fixed(byte* data=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,data);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;PK_PART_t* parts;
        Check(PK_PART_receive_b(block,&options,&count,&parts),"sealed Mac XT receive");try{Require(count==1,"one product body");return parts[0];}finally{Check(PK_MEMORY_free(parts),"free received parts");}}
}
static unsafe List<FiniteEdge> Edges(PK_BODY_t body)
{
    int count;PK_EDGE_t* edges;Check(PK_BODY_ask_edges(body,&count,&edges),"body finite edges");var result=new List<FiniteEdge>();PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
    try{for(var i=0;i<count;i++){PK_CURVE_t curve;PK_CLASS_t kind;PK_INTERVAL_t range;PK_LOGICAL_t sense;Check(PK_EDGE_ask_geometry(edges[i],PK_LOGICAL_true,&curve,&kind,ends,&range,&sense),"finite edge geometry");if(kind!=PK_CLASS_icurve)continue;
        Require(double.IsFinite(range.value[0])&&double.IsFinite(range.value[1])&&range.value[1]>range.value[0],"finite range");PK_VECTOR_t low,high;Check(PK_CURVE_eval(curve,range.value[0],0,&low),"finite start");Check(PK_CURVE_eval(curve,range.value[1],0,&high),"finite end");result.Add(new(curve,range,low,high));}}
    finally{Check(PK_MEMORY_free(edges),"free edges");}return result;
}
static unsafe double Sample(FiniteEdge source,FiniteEdge target)
{
    var first=source.Range;var second=target.Range;var maximum=0.0;
    for(var i=0;i<=16;i++)
    {
        var t=first.value[0]+(first.value[1]-first.value[0])*i/16;PK_VECTOR_t p,q;double at;
        Check(PK_CURVE_eval(source.Curve,t,0,&p),"finite source sample");Check(PK_CURVE_parameterise_vector(target.Curve,p,&at),"target parameter");
        Require(at>=second.value[0]-1e-7&&at<=second.value[1]+1e-7,"sample on finite target trim");
        Check(PK_CURVE_eval(target.Curve,Math.Clamp(at,second.value[0],second.value[1]),0,&q),"finite target sample");maximum=Math.Max(maximum,Distance(p,q));
    }
    Require(maximum<1e-6,"bidirectional finite samples");return maximum;
}
static unsafe double Distance(PK_VECTOR_t a,PK_VECTOR_t b){var x=a.coord[0]-b.coord[0];var y=a.coord[1]-b.coord[1];var z=a.coord[2]-b.coord[2];return Math.Sqrt(x*x+y*y+z*z);}
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
static void Check(PK_ERROR_code_t error,string label){if(error!=0)throw new InvalidOperationException($"{label}: PK error={error}");}
static string Source([CallerFilePath]string path="")=>path;
readonly record struct FiniteEdge(PK_CURVE_t Curve,PK_INTERVAL_t Range,PK_VECTOR_t Start,PK_VECTOR_t End);

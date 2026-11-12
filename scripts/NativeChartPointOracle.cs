#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=CurvedSheetPartitionOracle
#:property AllowUnsafeBlocks=true
using static parasolid;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Geometry.Evaluation;
using M=ProjectGmKernel.Native.Generated;

if(args.Length<2)throw new ArgumentException("new output name, project-relative XT inputs");
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
var name=args[0];if(name.Length==0||name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('-' or '_')))throw new ArgumentException("output name");
var output=Path.Combine(root,"temp_docs","native-chart-point-oracle",name);
if(Directory.Exists(output))throw new InvalidOperationException("immutable output");Directory.CreateDirectory(output);
Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
if(!ParasolidScriptHost.TryStartSession("Native CHART anchor contract",out var host,out var reason))throw new Exception(reason);
using(host)unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"version");Console.WriteLine($"PK_VERSION {version.major_revision}.{version.minor_revision}.{version.build_number}");
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed start");
    try
    {
        using var report=new StreamWriter(Path.Combine(output,"chart-points.tsv"));
        report.WriteLine("source\tcurve\tnativeCurve\tnode\tparameter\tshift\tnativeError\tmanagedError\tnativeAnchorDistance\tmanagedAnchorDistance\tpositionDifference");
        var curves=0;var nodes=0;var failed=0;
        foreach(var relative in args[1..])
        {
            var path=Path.GetFullPath(Path.Combine(root,relative));if(Path.IsPathRooted(relative)||!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new ArgumentException("project-relative source");
            var bytes=File.ReadAllBytes(path);var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));File.WriteAllBytes(Path.Combine(output,hash+".x_t"),bytes);
            Console.WriteLine($"SOURCE {relative} sha256={hash}");var own=Receive(true,bytes);var native=Receive(false,bytes);
            int count;int* edges;Check(PK_BODY_ask_edges(native,&count,&edges),"native edges");
            try
            {
                var candidates=new HashSet<int>();for(var e=0;e<count;e++){int curve,kind;Check(PK_EDGE_ask_curve(edges[e],&curve),"native edge curve");Check(PK_ENTITY_ask_class(curve,&kind),"curve class");if(kind==PK_CLASS_icurve)candidates.Add(curve);}
                var seen=new HashSet<int>();var body=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(own).Slot];var edge=body.FirstEdgeBody;
                for(var e=0;e<body.EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
                {
                    var tag=KernelRuntime.Edges[edge].CurveTag;if(!seen.Add(tag))continue;var record=KernelRuntime.GetCurveByTag(tag);if(record.Class!=CurveClass.ICurve)continue;
                    var data=KernelRuntime.ICurveDataPool[record.DataIndex];if(data.IsBlendContact!=0||data.ProgramBlock<=0)continue;
                    var program=KernelRuntime.GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
                    var definition=program.Spines[data.ProgramSpine];var chart=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);
                    var middle=record.TMin+(record.TMax-record.TMin)*.5;M.PK_VECTOR_s actual;Check(KernelRuntime.CurveEval(tag,middle,0,&actual),"managed matching point");
                    var matches=new List<int>();foreach(var candidate in candidates){PK_INTERVAL_t range;Check(PK_CURVE_ask_interval(candidate,&range),"native range");if(middle<range.value[0]||middle>range.value[1])continue;PK_VECTOR_t p;if(PK_CURVE_eval(candidate,middle,0,&p)==0&&Distance(p,actual)<1e-8)matches.Add(candidate);}
                    if(matches.Count!=1)throw new Exception($"ambiguous native curve correspondence curve={tag} matches={matches.Count}");var reference=matches[0];curves++;
                    for(var i=0;i<chart.Length;i++)
                    {
                        var node=chart[i];if(node.Parameter<record.TMin||node.Parameter>record.TMax)continue;nodes++;
                        foreach(var shift in new[]{-1,0,1})
                        {
                            var t=shift<0?Math.BitDecrement(node.Parameter):shift>0?Math.BitIncrement(node.Parameter):node.Parameter;if(t<record.TMin||t>record.TMax)continue;
                            PK_VECTOR_t p;M.PK_VECTOR_s q;var pe=PK_CURVE_eval(reference,t,0,&p);var ge=KernelRuntime.CurveEval(tag,t,0,&q);
                            var anchor=new PK_VECTOR_t(node.Position.X,node.Position.Y,node.Position.Z);var pd=pe==0?Distance(p,*(M.PK_VECTOR_s*)&anchor):double.NaN;var gd=ge==0?Distance(anchor,q):double.NaN;var delta=pe==0&&ge==0?Distance(p,q):double.NaN;
                            report.WriteLine($"{relative}\t{tag}\t{reference}\t{i}\t{t:R}\t{shift}\t{pe}\t{ge}\t{pd:R}\t{gd:R}\t{delta:R}");
                            if(pe!=0||ge!=0||delta>1e-8||shift==0&&gd!=0)failed++;
                        }
                    }
                }
            }
            finally{Check(PK_MEMORY_free(edges),"release native edges");Check(PK_ENTITY_delete(1,&native),"delete native body");Check(KernelRuntime.EntityDelete(1,&own),"delete managed body");}
        }
        Console.WriteLine($"CHART_SUMMARY curves={curves} nodes={nodes} failedQueries={failed}");if(curves==0||failed!=0)throw new Exception("native CHART query contract");
    }
    finally{Check(KernelRuntime.SessionStop(),"managed stop");}
}
static unsafe int Receive(bool own,byte[] bytes)
{
    fixed(byte* p=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;Check(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&options,&n,&parts):PK_PART_receive_b(block,&options,&n,&parts),"receive");try{if(n!=1)throw new Exception("one part");return parts[0];}finally{Check(own?KernelRuntime.MemoryFree(parts):PK_MEMORY_free(parts),"free parts");}}
}
static unsafe double Distance(PK_VECTOR_t a,M.PK_VECTOR_s b){var sum=0.0;for(var i=0;i<3;i++){var d=a.coord[i]-b.coord[i];sum+=d*d;}return Math.Sqrt(sum);}
static void Check(int error,string label){if(error!=0)throw new Exception(label+" error="+error);}
static string Source([CallerFilePath]string file="")=>file;

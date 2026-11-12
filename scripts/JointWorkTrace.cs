global using BlendSurfaceIndex = System.Int32;
global using BlendSpineIndex = System.Int32;
global using BufferCount = System.Int32;
global using BufferOffset = System.Int32;
global using DataSlot = System.Int32;
global using SurfaceSlot = System.Int32;
global using CurveSlot = System.Int32;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

// Temporary bounded diagnostics. Removed before selecting a production change.
internal static class JointWorkTrace
{
    private static readonly string? DirectoryPath=Environment.GetEnvironmentVariable("GM_JOINT_TRACE");
    private static readonly Dictionary<string,JsonObject> definitions=new(StringComparer.Ordinal);
    private static readonly Dictionary<long,string> sourceScopes=[];
    private static readonly Stack<JsonObject> active=[];
    private static StreamWriter? calls,programs;
    private static long count;
    internal static bool Enabled=>DirectoryPath!=null;
    internal static void Begin(in NestedBlendProgram program,BlendSurfaceIndex surface,BlendSpineIndex spine,in SurfaceParameterBox domain,BufferCount outputLength,
        ReadOnlySpan<BufferCount> reachBudget,BufferCount curvature,BufferOffset curvatureSide,BufferCount support,BufferOffset parameterSide,
        BufferCount capture,ReadOnlySpan<IntervalNumber> require,BufferOffset rootChart,BufferCount anchors,BufferCount acceleration,
        in KernelVector3 normal,BufferCount captureCharts,ReadOnlySpan<BufferOffset> requireCharts)
    {
        if(!Enabled)return;
        if(++count>300000)throw new InvalidOperationException("Joint trace bounded limit exceeded; not a complete trace");
        if(calls==null){System.IO.Directory.CreateDirectory(DirectoryPath!);var id=Environment.ProcessId;calls=new(Path.Combine(DirectoryPath!,id+"-calls.jsonl"),false){AutoFlush=true};programs=new(Path.Combine(DirectoryPath!,id+"-definitions.jsonl"),false){AutoFlush=true};}
        var context=ModelingPerformance.JointProbeContext;
        string sources;
        if(context.Immutable&&sourceScopes.TryGetValue(context.Scope,out var cached))sources=cached;
        else{var source=JointProbeSources();sources=Define(source);if(context.Immutable)sourceScopes.Add(context.Scope,sources);}
        var definition=new JsonObject{["sourceBlock"]=program.SourceBlock,["surfaces"]=Array(program.Surfaces),["spines"]=Array(program.Spines),["charts"]=Array(program.Charts),["scalars"]=Array(program.Scalars)};
        var fingerprint=Define(definition);
        active.Push(new JsonObject{["ordinal"]=count,["scope"]=context.Scope,["immutable"]=context.Immutable,["stage"]=context.Stage.ToString(),["rootUse"]=context.RootUse.ToString(),["sources"]=sources,["definition"]=fingerprint,["surface"]=surface,["spine"]=spine,["domain"]=Node(domain),["needs"]=new JsonObject{["outputLength"]=outputLength,["workspaceLength"]=program.Workspace.Length,["reachBudget"]=Array(reachBudget),["curvature"]=curvature,["curvatureSide"]=curvatureSide,["support"]=support,["parameterSide"]=parameterSide,["capture"]=capture,["require"]=Array(require),["rootChart"]=rootChart,["anchors"]=anchors,["acceleration"]=acceleration,["normal"]=Node(normal),["captureCharts"]=captureCharts,["requireCharts"]=Array(requireCharts)},["caller"]=new JsonArray(new StackTrace(1,false).GetFrames().Take(8).Select(f=>(JsonNode?)JsonValue.Create(f.GetMethod()?.Name)).ToArray())});
    }
    internal static void Prepared(ReadOnlySpan<JointNestedBlendEvaluation.Occurrence> nodes,ReadOnlySpan<BufferOffset> charts,ReadOnlySpan<TaylorJet> snapshot,BufferCount n)
    {if(!Enabled)return;var row=active.Peek();row["n"]=n;row["occurrences"]=Array(nodes);row["fixedCharts"]=Array(charts);var center=new JsonArray();for(var i=0;i<n;i++)center.Add((JsonNode?)JsonValue.Create(Bits(snapshot[i][0])));row["center"]=center;}
    internal static void Tube(ReadOnlySpan<IntervalNumber> boxes,ReadOnlySpan<IntervalNumber> derivative)
    {if(!Enabled)return;active.Peek()["tube"]=new JsonObject{["boxes"]=Array(boxes),["derivative"]=Array(derivative)};}
    internal static void End(AlgorithmStatus status)
    {if(!Enabled)return;var row=active.Pop();row["status"]=status.ToString();calls!.WriteLine(row.ToJsonString());}
    private static string Define(JsonObject value)
    {
        var text=value.ToJsonString();var key=Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        if(definitions.TryGetValue(key,out var previous)){if(previous.ToJsonString()!=text)throw new InvalidOperationException("trace hash collision");}
        else{definitions.Add(key,value);programs!.WriteLine(new JsonObject{["sha256"]=key,["value"]=value.DeepClone()}.ToJsonString());}return key;
    }
    private static JsonArray Array<T>(ReadOnlySpan<T> values) where T:struct{var result=new JsonArray();foreach(var value in values)result.Add(Node(value));return result;}
    internal static JsonNode? Node(object? value)
    {
        if(value==null)return null;if(value is double d)return JsonValue.Create(Bits(d));if(value is bool b)return JsonValue.Create(b);if(value is int i)return JsonValue.Create(i);if(value is byte bt)return JsonValue.Create(bt);if(value is long l)return JsonValue.Create(l);var type=value.GetType();if(type.IsEnum)return JsonValue.Create(Convert.ToInt64(value));if(type.IsPrimitive)return JsonValue.Create(Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture));
        var result=new JsonObject();foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).OrderBy(f=>f.MetadataToken))result[field.Name]=Node(field.GetValue(value));return result;
    }
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Install(){if(!Enabled)return;JointWorkProbe.OnBegin=Begin;JointWorkProbe.OnPrepared=Prepared;JointWorkProbe.OnTube=Tube;JointWorkProbe.OnEnd=End;}
    private static string Bits(double value)=>unchecked((ulong)BitConverter.DoubleToInt64Bits(value)).ToString("x16");
    internal static JsonObject JointProbeSources()
    {
        var surfaces=new JsonArray();var curves=new JsonArray();
        for(SurfaceSlot slot=0;slot<KernelRuntime.Surfaces.AllocatedCount;slot++)if(KernelRuntime.Surfaces.IsAlive(slot))
        {
            var source=KernelRuntime.Surfaces[slot];surfaces.Add((JsonNode)new JsonObject{["record"]=Node(source),["programBlock"]=source.Class==SurfaceClass.BlendSurface?KernelRuntime.BlendedEdgeDataPool[source.DataIndex].ProgramBlock:-1});
        }
        for(CurveSlot slot=0;slot<KernelRuntime.Curves.AllocatedCount;slot++)if(KernelRuntime.Curves.IsAlive(slot))
        {
            var source=KernelRuntime.Curves[slot];curves.Add((JsonNode)new JsonObject{["record"]=Node(source),["programBlock"]=source.Class==CurveClass.ICurve?KernelRuntime.ICurveDataPool[source.DataIndex].ProgramBlock:-1});
        }
        return new(){["scope"]="All live geometry source/generation/sense/ownership records (conservative superset), complete program definitions separately",["surfaces"]=surfaces,["curves"]=curves};
    }
}

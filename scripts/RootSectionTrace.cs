using System.Text.Json.Nodes;
using System.Security.Cryptography;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using BufferOffset=System.Int32;
using BufferCount=System.Int32;
internal static class RootSectionTrace
{
    private static readonly JsonArray Rows=new();
    internal static void Install(){SurfacePlaneIntersection.TraceSections=Record;AppDomain.CurrentDomain.ProcessExit+=(_,_)=>Save();}
    private static void Record(long invocation,in SurfaceIntersectionPatch patch,in AnalyticSurface support,BufferOffset axis,double at,double first,double last,double tolerance,bool strict,bool certified,byte consumer,BufferCount remaining,AlgorithmStatus status,ReadOnlySpan<OperationIntersectionPoint> points,ReadOnlySpan<ScalarRootIsolation> isolations,long cells,long jacobians,long bounds)
    {
        var id=patch.RootSectionTraceIdentity;var plane=new JsonArray();foreach(var v in new[]{support.Origin.X,support.Origin.Y,support.Origin.Z,support.Axis.X,support.Axis.Y,support.Axis.Z,support.X.X,support.X.Y,support.X.Z,support.Y.X,support.Y.Y,support.Y.Z,support.Radius,support.Secondary})plane.Add((JsonNode)JsonValue.Create(Bits(v))!);
        var payload=new JsonArray();foreach(var p in points){Add(payload,Bits(p.Point.X));Add(payload,Bits(p.Point.Y));Add(payload,Bits(p.Point.Z));Add(payload,Bits(p.First));Add(payload,Bits(p.Second));Add(payload,Bits(p.U));Add(payload,Bits(p.V));Add(payload,(long)p.Kind);}foreach(var r in isolations){Add(payload,Bits(r.Root.Lower));Add(payload,Bits(r.Root.Upper));Add(payload,Bits(r.MonotoneDomain.Lower));Add(payload,Bits(r.MonotoneDomain.Upper));Add(payload,r.Increasing?1L:0L);Add(payload,r.Certified?1L:0L);}
        Rows.Add((JsonNode)new JsonObject{["invocation"]=invocation,["tag"]=id.Tag,["generation"]=id.Generation,["sense"]=id.Sense,["root"]=id.Root,["blend"]=id.Blend,["spine"]=id.Spine,["domain"]=new JsonArray(Bits(patch.Domain.U0),Bits(patch.Domain.U1),Bits(patch.Domain.V0),Bits(patch.Domain.V1)),["supportKind"]=(int)support.Kind,["supportBits"]=plane,["axis"]=axis,["at"]=Bits(at),["first"]=Bits(first),["last"]=Bits(last),["tolerance"]=Bits(tolerance),["strict"]=strict,["certified"]=certified,["consumer"]=consumer,["outerRemaining"]=remaining,["rootBudget"]=32768,["rootCapacity"]=32,["initialCount"]=0,["status"]=status.ToString(),["roots"]=points.Length,["payload"]=payload,["cells"]=cells,["jacobians"]=jacobians,["bounds"]=bounds});
    }
    private static void Save()
    {
        var directory=Environment.GetEnvironmentVariable("GM_ROOT_SECTION_TRACE");if(string.IsNullOrEmpty(directory))return;Directory.CreateDirectory(directory);var path=Path.Combine(directory,"root-sections.json");if(File.Exists(path))throw new IOException("preserve diagnostic requests");File.WriteAllText(path,new JsonObject{["scope"]="Instrumented source, original math and root requests recomputed. Each invocation is a single pure SurfacePlaneIntersection.Find call. No cross-invocation/owner sharing or CPU/timing claim.",["pid"]=Environment.ProcessId,["rows"]=Rows}.ToJsonString());using var stream=File.OpenRead(path);Console.WriteLine($"ROOT_SECTIONS rows={Rows.Count} sha256={Convert.ToHexStringLower(SHA256.HashData(stream))}");
    }
    private static void Add(JsonArray array,long value)=>array.Add((JsonNode)JsonValue.Create(value)!);
    private static long Bits(double value)=>BitConverter.DoubleToInt64Bits(value);
}

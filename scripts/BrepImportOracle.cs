#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=BrepImportOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

global using ManagedBodyTag = System.Int32;

using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Xt;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

var modelingDirectory = args.Length == 0 ? null
    : Path.GetFullPath(args[0], Path.GetDirectoryName(ScriptPath())!);
var referenceInputs = args.Length > 1 && args[1] == "--reference";
var inputSuffix = referenceInputs ? "-reference.x_t" : "-managed.x_t";
var directory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "brep-import-oracle"));
if (modelingDirectory is not null) directory = Path.Combine(directory,
    referenceInputs ? "modeling-reference-integration" : "modeling-integration");
var report = new StringBuilder();
void Log(string text) { Console.WriteLine(text); report.AppendLine(text); }
try
{
    if (!ParasolidScriptHost.TryStartSession("B-rep import", out var host, out var reason)) throw new InvalidOperationException(reason);
    using (host)
    {
        unsafe
        {
            var start = new M.PK_SESSION_start_o_s { o_t_version = 1 };
            Check(KernelRuntime.SessionStart(&start), "managed start");
            try
            {
                var failures = 0;
                var caseCount = 0;
                if (modelingDirectory is null)
                {
                    foreach (var kind in Enumerable.Range(0, 13))
                    {
                        var name = new[] { "posed-block", "posed-prism", "posed-cylinder", "posed-cone", "posed-sphere", "posed-torus", "sheet-hole", "bsurface-sheet", "swept-sheet", "spun-sheet", "posed-apex-cone", "construction-swept", "construction-shared-chain" }[kind];
                        try { RunCase(kind, name, directory, Log); }
                        catch (Exception ex) { Log($"FAIL {name}: {ex.Message}"); failures++; }
                        caseCount++;
                    }
                }
                else
                {
                    foreach (var path in Directory.GetFiles(modelingDirectory, "*" + inputSuffix).Order())
                    {
                        var name = Path.GetFileName(path)[..^inputSuffix.Length];
                        try
                        {
                            var bytes = File.ReadAllBytes(path);
                            var reference = ReceiveOracle(bytes);
                            VerifyImportedBody(reference, bytes, name, directory, Log);
                        }
                        catch (Exception ex) { Log($"FAIL {name}: {ex.Message}"); failures++; }
                        caseCount++;
                    }
                }
                Require(caseCount > 0, "No B-rep import cases were found.");
                Require(failures == 0, $"{failures} B-rep import case(s) failed.");
                var origin = modelingDirectory is null ? "real-PK producers" : referenceInputs
                    ? "real-PK modeling reference files" : "independent-kernel modeling outputs with real-PK receive baselines";
                Log($"PASS: {caseCount} {origin} imported as runtime topology, evaluated, regenerated without cached XT, independently deep-copied, received by PK and compared.");
            }
            finally { Check(KernelRuntime.SessionStop(), "managed stop"); }
        }
    }
}
catch (Exception ex) { Log($"FAIL: {ex.Message}"); Environment.ExitCode = 1; }
finally { Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "oracle-latest.txt"), report.ToString()); }

static string ScriptPath([CallerFilePath] string path = "") => path;

static unsafe void RunCase(int kind, string name, string directory, Action<string> log)
{
    var baseline = CreateReference(kind);
    var bytes = TransmitReference(baseline);
    VerifyImportedBody(baseline, bytes, name, directory, log);
}

static unsafe void VerifyImportedBody(PK_BODY_t baseline, byte[] bytes, string name, string directory, Action<string> log)
{
    var before = ManagedPoolCounts();
    var beforeBlocks = KernelRuntime.State.Session->Blocks.Statistics;
    Directory.CreateDirectory(directory);
    File.WriteAllBytes(Path.Combine(directory, name + "-reference.x_t"), bytes);
    var source = XtText.DecodeDocument(Encoding.ASCII.GetString(bytes));
    var withConstruction = source.Nodes.Any(n => n.Type == (int)XtNodeTypes.Body
        && (n.Fields[4].Pointer != 0 || n.Fields[5].Pointer != 0));
    var body = ReceiveManaged(bytes);
    Require(KernelRuntime.TryResolveBodySlot(body, out var slot) && !KernelRuntime.Xt.IsOpaque(slot), name + " remained opaque");
    int referenceType, actualType, referenceConfig, actualConfig;
    Check(PK_BODY_ask_type(baseline, &referenceType), "PK body type");
    Check(KernelRuntime.BodyAskType(body, &actualType), "managed body type");
    Check(PK_BODY_ask_config(baseline, &referenceConfig), "PK body config");
    Check(KernelRuntime.BodyAskConfig(body, &actualConfig), "managed body config");
    Require(referenceType == actualType && referenceConfig == actualConfig, name + " body type/config differs");
    Require(OracleCounts(baseline) == ManagedCounts(body), name + " runtime topology counts differ");
    CompareGeometry(baseline, body, log);
    if (withConstruction) ComparePartGeometry(baseline, body, managed: true, log);
    foreach (var fins in new[] { false, true }) CompareTopologyGraph(baseline, body, fins);
    KernelRuntime.Xt.Drop(slot);
    var fresh = TransmitManaged(body);
    File.WriteAllBytes(Path.Combine(directory, name + "-fresh.x_t"), fresh);
    var actual = ReceiveOracle(fresh);
    Require(OracleCounts(baseline) == OracleCounts(actual), name + " fresh PK topology counts differ");
    CompareBodies(baseline, actual, name, log);
    if (withConstruction) ComparePartGeometry(baseline, actual, managed: false, log);
    int copied;
    Check(KernelRuntime.EntityCopy(body, &copied), name + " public deep copy");
    Check(KernelRuntime.EntityDelete(1, &body), "delete imported body");
    CompareGeometry(baseline, copied, log);
    if (withConstruction) ComparePartGeometry(baseline, copied, managed: true, log);
    foreach (var fins in new[] { false, true }) CompareTopologyGraph(baseline, copied, fins);
    Check(KernelRuntime.BodyAskType(copied, &actualType), "copied body type");
    Check(KernelRuntime.BodyAskConfig(copied, &actualConfig), "copied body config");
    Require(referenceType == actualType && referenceConfig == actualConfig, name + " copied body type/config differs");
    var copiedBytes = TransmitManaged(copied);
    File.WriteAllBytes(Path.Combine(directory, name + "-copied.x_t"), copiedBytes);
    var copiedOracle = ReceiveOracle(copiedBytes);
    Require(OracleCounts(baseline) == OracleCounts(copiedOracle), name + " copied PK topology counts differ");
    CompareBodies(baseline, copiedOracle, name + " copied after original deletion", log);
    if (withConstruction) ComparePartGeometry(baseline, copiedOracle, managed: false, log);
    Check(KernelRuntime.EntityDelete(1, &copied), "delete copied body");
    Require(before.SequenceEqual(ManagedPoolCounts()), name + " import/delete leaked runtime records or payload blocks");
    var afterBlocks = KernelRuntime.State.Session->Blocks.Statistics;
    Require(beforeBlocks.LiveBlocks == afterBlocks.LiveBlocks && beforeBlocks.LiveBytes == afterBlocks.LiveBytes,
        name + " import/delete leaked variable-length geometry payloads");
    Check(PK_ENTITY_delete(1, &baseline), "delete producer");
    Check(PK_ENTITY_delete(1, &actual), "delete roundtrip");
    Check(PK_ENTITY_delete(1, &copiedOracle), "delete copied roundtrip");
    log($"PASS {name}: actual geometry, public topology graph, fresh XT, independent deep copy after original deletion, and PK structure/sense/tolerance comparison.");
}

static unsafe int[] ManagedPoolCounts() =>
[
    KernelRuntime.Bodies.AliveCount, KernelRuntime.Regions.AliveCount, KernelRuntime.Shells.AliveCount,
    KernelRuntime.Faces.AliveCount, KernelRuntime.FaceUses.AliveCount, KernelRuntime.Loops.AliveCount,
    KernelRuntime.Edges.AliveCount, KernelRuntime.Fins.AliveCount, KernelRuntime.Vertices.AliveCount,
    KernelRuntime.Points.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount,
    KernelRuntime.LineDataPool.AliveCount, KernelRuntime.CircleDataPool.AliveCount, KernelRuntime.EllipseDataPool.AliveCount,
    KernelRuntime.PlaneDataPool.AliveCount, KernelRuntime.CylinderDataPool.AliveCount, KernelRuntime.ConeDataPool.AliveCount,
    KernelRuntime.SphereDataPool.AliveCount, KernelRuntime.TorusDataPool.AliveCount, KernelRuntime.BCurveDataStore.AliveCount,
    KernelRuntime.BSurfaceDataStore.AliveCount, KernelRuntime.SweptDataPool.AliveCount, KernelRuntime.SpunDataPool.AliveCount,
    KernelRuntime.TrCurveDataPool.AliveCount, KernelRuntime.SpCurveDataPool.AliveCount, KernelRuntime.OffsetDataPool.AliveCount,
    KernelRuntime.State.Session->Tags.LiveOrKeptCount,
];

static unsafe PK_BODY_t CreateReference(int kind)
{
    if (kind >= 11) return CreateConstructionReference(kind == 12);
    var axis = new PK_AXIS2_sf_t(new PK_VECTOR_t(11,-7,5),new PK_VECTOR1_t(.6,0,.8),new PK_VECTOR1_t(0,1,0));
    PK_BODY_t body = 0;
    var error = kind switch
    {
        0 => PK_BODY_create_solid_block(2.25,4.5,7.75,&axis,&body),
        1 => PK_BODY_create_solid_prism(2.75,5.25,5,&axis,&body),
        2 => PK_BODY_create_solid_cyl(2.75,5.25,&axis,&body),
        3 => PK_BODY_create_solid_cone(2.75,4.25,.3,&axis,&body),
        4 => PK_BODY_create_solid_sphere(3.25,&axis,&body),
        5 => PK_BODY_create_solid_torus(4.25,1.15,&axis,&body),
        10 => PK_BODY_create_solid_cone(0,5,.25,&axis,&body),
        _ => 0,
    };
    Check(error,"PK primitive");
    if (kind < 6 || kind == 10) return body;
    if (kind == 6)
    {
        var vectors = stackalloc PK_VECTOR_t[8]
        {
            new(0,0,2),new(8,0,2),new(8,7,2),new(0,7,2),
            new(2,2,2),new(2,4,2),new(5,4,2),new(5,2,2),
        };
        var loops = stackalloc int[8] {0,0,0,0,1,1,1,1};
        var options = new PK_BODY_create_sheet_planar_o_t
        {
            loops = loops,
            plane = new PK_AXIS1_sf_t(new PK_VECTOR_t(0,0,2),new PK_VECTOR1_t(0,0,1)),
            basis_set = new PK_AXIS2_sf_t(new PK_VECTOR_t(0,0,2),new PK_VECTOR1_t(0,0,1),new PK_VECTOR1_t(1,0,0)),
        };
        Check(PK_BODY_create_sheet_planar(8,vectors,&options,&body),"PK holed sheet");
        return body;
    }
    PK_SURF_t surface = 0;
    var box = new PK_UVBOX_t(0,0,1,1);
    if (kind == 7)
    {
        var poles = stackalloc double[12] {0,0,1, 2,0,1.3, 0,3,1.5, 2,3,1.1};
        var knots = stackalloc double[2] {0,1}; var mults = stackalloc int[2] {2,2};
        var sf = new PK_BSURF_sf_t(1,1,2,2,3,PK_LOGICAL_false,poles,PK_BSURF_form_arbitrary_c,2,2,
            mults,mults,knots,knots,PK_knot_non_uniform_c,PK_knot_non_uniform_c,PK_LOGICAL_false,PK_LOGICAL_false,
            PK_LOGICAL_false,PK_LOGICAL_false,PK_self_intersect_false_c,PK_convexity_arbitrary_c);
        Check(PK_BSURF_create(&sf,&surface),"PK B-surface");
    }
    else
    {
        var ellipse = new PK_ELLIPSE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(3,0,0),new PK_VECTOR1_t(0,1,0),new PK_VECTOR1_t(1,0,0)),1,.5);
        PK_CURVE_t section; Check(PK_ELLIPSE_create(&ellipse,&section),"PK section");
        if(kind==8)
        {
            var sf = new PK_SWEPT_sf_t(section,new PK_VECTOR1_t(0,1,0));
            Check(PK_SWEPT_create(&sf,&surface),"PK swept");
        }
        else
        {
            var sf = new PK_SPUN_sf_t(section,new PK_AXIS1_sf_t(new PK_VECTOR_t(0,0,0),new PK_VECTOR1_t(0,0,1)));
            Check(PK_SPUN_create(&sf,&surface),"PK spun");
        }
    }
    Check(PK_SURF_make_sheet_body(surface,box,&body),"PK surface sheet");
    return body;
}

static unsafe PK_BODY_t CreateConstructionReference(bool sharedChain)
{
    var poles = stackalloc double[9] { 2,0,0, 3,1,1, 2,0,2 };
    var knots = stackalloc double[2] { 0,1 }; var mults = stackalloc int[2] { 3,3 };
    var definition = new PK_BCURVE_sf_t(2,3,3,PK_LOGICAL_false,poles,PK_BCURVE_form_arbitrary_c,
        2,mults,knots,PK_knot_bezier_ends_c,PK_LOGICAL_false,PK_LOGICAL_false,PK_self_intersect_false_c);
    int curve, body;
    Check(PK_BCURVE_create(&definition,&curve), "construction profile");
    var inputCurve = curve;
    Check(PK_CURVE_make_wire_body(curve,new PK_INTERVAL_t(0,1),&body), "construction wire");
    int edgeCount; int* edges;
    Check(PK_BODY_ask_edges(body,&edgeCount,&edges), "construction edges");
    Require(edgeCount == 1, "construction wire edge count");
    Check(PK_EDGE_ask_curve(edges[0],&curve), "construction attached profile");
    Check(PK_MEMORY_free(edges), "construction edges free");
    if (inputCurve != curve) Check(PK_ENTITY_delete(1,&inputCurve), "construction input curve delete");
    var sweep = new PK_SWEPT_sf_t(curve,new PK_VECTOR1_t(0,1,0));
    int swept; Check(PK_SWEPT_create(&sweep,&swept), "construction swept");
    if (sharedChain)
    {
        var offset = new PK_OFFSET_sf_t(swept,.1);
        int root; Check(PK_OFFSET_create(&offset,&root), "construction offset");
        var spun = new PK_SPUN_sf_t(curve,new PK_AXIS1_sf_t(new PK_VECTOR_t(0,0,0),new PK_VECTOR1_t(0,0,1)));
        Check(PK_SPUN_create(&spun,&root), "construction spun");
        var start = .2; var end = .8; int error;
        CRTRCU(&curve,&start,&end,&root,&error);
        Check(error, "construction trimmed curve");
    }
    return body;
}

static unsafe void ComparePartGeometry(int reference, int actual, bool managed, Action<string> log)
{
    var expected = PartGeometrySamples(reference, managed: false);
    var observed = PartGeometrySamples(actual, managed);
    Require(expected.Count == observed.Count, $"construction/dependency geometry count differs: PK={expected.Count}, actual={observed.Count}");
    for (var i = 0; i < expected.Count; i++)
    {
        Require(expected[i].Length == observed[i].Length, "construction/dependency geometry class differs");
        for (var j = 0; j < expected[i].Length; j++)
        {
            var delta = Math.Abs(expected[i][j] - observed[i][j]);
            Require(double.IsFinite(delta) && delta <= 2e-9 * Math.Max(1,Math.Abs(expected[i][j])),
                $"construction/dependency geometry {i}/{j}: PK={expected[i][j]:R}, actual={observed[i][j]:R}");
        }
    }
    log($"  {expected.Count} construction members and defining geometries preserve explicit part membership, class, owner counts and native-parameter jets.");
}

static unsafe List<double[]> PartGeometrySamples(int body, bool managed)
{
    int count; int* geometry;
    Check(managed ? KernelRuntime.PartAskGeoms(body,&count,&geometry) : PK_PART_ask_geoms(body,&count,&geometry), "part construction geometry");
    var construction = new HashSet<int>(new ReadOnlySpan<int>(geometry,count).ToArray());
    Require(construction.Count == count, "duplicate part construction geometry");
    if (geometry != null) Check(managed ? KernelRuntime.MemoryFree(geometry) : PK_MEMORY_free(geometry), "part geometry free");
    var tags = new HashSet<int>(construction);
    var pending = new Queue<int>(tags);
    Span<int> defining = stackalloc int[2];
    while (pending.TryDequeue(out var tag))
    {
        if (managed)
        {
            var entity = KernelRuntime.ResolveTagRecord(tag);
            var n = KernelRuntime.GetGeometryDependencyTags((PoolKind)entity.Pool,entity.Slot,defining);
            for (var i = 0; i < n; i++) if (tags.Add(defining[i])) pending.Enqueue(defining[i]);
        }
        else
        {
            int n; int* dependencies;
            Check(PK_GEOM_ask_dependents(tag,&n,&dependencies), "part defining geometry");
            for (var i = 0; i < n; i++) if (tags.Add(dependencies[i])) pending.Enqueue(dependencies[i]);
            if (dependencies != null) Check(PK_MEMORY_free(dependencies), "part defining geometry free");
        }
    }
    var rows = new List<double[]>();
    var values = stackalloc PK_VECTOR_t[4];
    foreach (var tag in tags)
    {
        int cls, owners = 0;
        Check(managed ? KernelRuntime.EntityAskClass(tag,&cls) : PK_ENTITY_ask_class(tag,&cls), "part geometry class");
        var length = cls == PK_CLASS_point ? 1 : cls is PK_CLASS_line or PK_CLASS_circle or PK_CLASS_ellipse
            or PK_CLASS_bcurve or PK_CLASS_trcurve or PK_CLASS_spcurve ? 3 : 4;
        if (length == 1)
        {
            if (managed)
            {
                M.PK_POINT_sf_s point; Check(KernelRuntime.PointAsk(tag,&point), "part point");
                values[0] = new PK_VECTOR_t(point.position.coord[0],point.position.coord[1],point.position.coord[2]);
            }
            else { PK_POINT_sf_t point; Check(PK_POINT_ask(tag,&point), "part point"); values[0] = point.position; }
        }
        else
        {
            if (managed)
                owners = length == 3 ? KernelRuntime.GetCurveByTag(tag).GeometricOwnerCount : KernelRuntime.GetSurfaceByTag(tag).GeometricOwnerCount;
            else
            {
                int* ownerTags; Check(PK_GEOM_ask_geom_owners(tag,&owners,&ownerTags), "part geometry owners");
                if (ownerTags != null) Check(PK_MEMORY_free(ownerTags), "part geometry owners free");
            }
            if (length == 3)
                Check(managed ? KernelRuntime.CurveEval(tag,.37,2,(M.PK_VECTOR_s*)values) : PK_CURVE_eval(tag,.37,2,values), "part curve eval");
            else if (managed)
            {
                var uv = new M.PK_UV_s(); uv.param[0] = .31; uv.param[1] = .27;
                Check(KernelRuntime.SurfEval(tag,uv,1,1,0,(M.PK_VECTOR_s*)values), "part surface eval");
            }
            else Check(PK_SURF_eval(tag,new PK_UV_t(.31,.27),1,1,PK_LOGICAL_false,values), "part surface eval");
        }
        var row = new double[3+length*3]; row[0] = cls; row[1] = construction.Contains(tag) ? 1 : 0; row[2] = owners;
        for (var i = 0; i < length; i++) for (var j = 0; j < 3; j++) row[3+3*i+j] = values[i].coord[j];
        rows.Add(row);
    }
    rows.Sort((a,b) => { for (var i = 0; i < Math.Min(a.Length,b.Length); i++) { var c = Math.Round(a[i],8).CompareTo(Math.Round(b[i],8)); if (c != 0) return c; } return a.Length.CompareTo(b.Length); });
    return rows;
}

static unsafe void CompareGeometry(PK_BODY_t reference, ManagedBodyTag managed, Action<string> log)
{
    var expected = Samples(reference, false);
    var actual = Samples(managed, true);
    Require(expected.Count == actual.Count,"sample count differs");
    var maximum = 0.0;
    for(var i=0;i<expected.Count;i++)
    {
        Require(expected[i].Length == actual[i].Length,"sample class differs");
        for(var j=0;j<expected[i].Length;j++)
        {
            var delta=Math.Abs(expected[i][j]-actual[i][j]);
            Require(double.IsFinite(delta) && delta <= 2e-9*Math.Max(1,Math.Abs(expected[i][j])), $"geometry sample {i}/{j}: PK={expected[i][j]:R}, managed={actual[i][j]:R}");
            maximum=Math.Max(maximum,delta);
        }
    }
    log($"  {expected.Count} native-parameter point/curve/surface samples; maximum absolute difference={maximum:R}");
}

static unsafe List<double[]> Samples(int body, bool managed)
{
    var rows = new List<double[]>();
    var values = stackalloc PK_VECTOR_t[4];
    for(var kind=0;kind<3;kind++)
    {
        int count=0; int* entities=null;
        var error = (managed,kind) switch
        {
            (true,0)=>KernelRuntime.BodyAskVertices(body,&count,&entities), (true,1)=>KernelRuntime.BodyAskEdges(body,&count,&entities),
            (true,_)=>KernelRuntime.BodyAskFaces(body,&count,&entities), (false,0)=>PK_BODY_ask_vertices(body,&count,&entities),
            (false,1)=>PK_BODY_ask_edges(body,&count,&entities), _=>PK_BODY_ask_faces(body,&count,&entities),
        };
        Check(error,"sample list");
        var entityTags = new ReadOnlySpan<int>(entities,count).ToArray();
        if(entities!=null)Check(managed?KernelRuntime.MemoryFree(entities):PK_MEMORY_free(entities),"sample list free");
        foreach(var entity in entityTags)
        {
            int geometry=0;
            var publicFinCount = 0;
            var length=kind==0?1:kind==1?3:4;
            if(kind==0)
            {
                if(managed)
                {
                    M.PK_POINT_sf_s sf;
                    Check(KernelRuntime.VertexAskPoint(entity,&geometry),"vertex point"); Check(KernelRuntime.PointAsk(geometry,&sf),"point ask");
                    values[0]=new PK_VECTOR_t(sf.position.coord[0],sf.position.coord[1],sf.position.coord[2]);
                }
                else { PK_POINT_sf_t sf; Check(PK_VERTEX_ask_point(entity,&geometry),"PK vertex point"); Check(PK_POINT_ask(geometry,&sf),"PK point ask"); values[0]=sf.position; }
            }
            else if(kind==1)
            {
                int* fins = null;
                Check(managed ? KernelRuntime.EdgeAskFins(entity, &publicFinCount, &fins)
                    : PK_EDGE_ask_fins(entity, &publicFinCount, &fins), "edge public fins");
                if (fins != null) Check(managed ? KernelRuntime.MemoryFree(fins) : PK_MEMORY_free(fins), "free public fins");
                if(managed)
                {
                    Check(KernelRuntime.EdgeAskCurve(entity,&geometry),"edge curve");
                    Check(KernelRuntime.CurveEval(geometry,.37,2,(M.PK_VECTOR_s*)values),"curve eval");
                }
                else { Check(PK_EDGE_ask_curve(entity,&geometry),"PK edge curve"); Check(PK_CURVE_eval(geometry,.37,2,values),"PK curve eval"); }
            }
            else
            {
                if(managed)
                {
                    Check(KernelRuntime.FaceAskSurf(entity,&geometry),"face surface");
                    var uv=new M.PK_UV_s(); uv.param[0]=.31;uv.param[1]=.27;
                    Check(KernelRuntime.SurfEval(geometry,uv,1,1,0,(M.PK_VECTOR_s*)values),"surface eval");
                }
                else { Check(PK_FACE_ask_surf(entity,&geometry),"PK face surface"); Check(PK_SURF_eval(geometry,new PK_UV_t(.31,.27),1,1,PK_LOGICAL_false,values),"PK surface eval"); }
            }
            var row = new double[length*3+1+(kind==1?1:0)]; row[0]=kind;
            for(var i=0;i<length;i++) for(var j=0;j<3;j++)row[1+3*i+j]=values[i].coord[j];
            if (kind == 1) row[^1] = publicFinCount;
            rows.Add(row);
        }
    }
    rows.Sort((a,b)=> { for(var i=0;i<Math.Min(a.Length,b.Length);i++){var c=Math.Round(a[i],8).CompareTo(Math.Round(b[i],8));if(c!=0)return c;}return a.Length.CompareTo(b.Length); });
    return rows;
}

static unsafe void CompareTopologyGraph(PK_BODY_t reference, ManagedBodyTag managed, bool fins)
{
    var po = new PK_BODY_ask_topology_o_t { want_fins=(byte)(fins?1:0) };
    var mo = new M.PK_BODY_ask_topology_o_s { o_t_version=2,want_fins=(byte)(fins?1:0) };
    int pn,mn,pr,mr; int* pt;int*pc;int*pp;int*pch;int*ps;
    nint mt,mc,mp,mch,ms;
    Check(PK_BODY_ask_topology(reference,&po,&pn,&pt,&pc,&pr,&pp,&pch,&ps),"PK topology graph");
    Check(KernelRuntime.BodyAskTopology(managed,&mo,&mn,&mt,&mc,&mr,&mp,&mch,&ms),"managed topology graph");
    try
    {
        Require(pn==mn && pr==mr,$"topology count fins={fins}: PK={pn}/{pr}, managed={mn}/{mr}");
        var expected=new Dictionary<(int,int,int),int>();var actual=new Dictionary<(int,int,int),int>();
        for(var i=0;i<pr;i++){var key=(pc[pp[i]],pc[pch[i]],ps[i]);expected[key]=expected.GetValueOrDefault(key)+1;}
        for(var i=0;i<mr;i++){var key=(((int*)mc)[((int*)mp)[i]],((int*)mc)[((int*)mch)[i]],((int*)ms)[i]);actual[key]=actual.GetValueOrDefault(key)+1;}
        Require(expected.Count==actual.Count && expected.All(p=>actual.GetValueOrDefault(p.Key)==p.Value),
            $"topology relation classes/senses differ fins={fins}: PK={string.Join(';', expected.Select(p=>$"{p.Key}={p.Value}"))}; managed={string.Join(';', actual.Select(p=>$"{p.Key}={p.Value}"))}");
        var expectedRegions = RegionSignatures(false, pn, pt, pc, pr, pp, pch, ps);
        var actualRegions = RegionSignatures(true, mn, (int*)mt, (int*)mc, mr, (int*)mp, (int*)mch, (int*)ms);
        Require(expectedRegions.SequenceEqual(actualRegions), $"region material/shell/face-use incidence differs fins={fins}");
    }
    finally
    {
        Check(PK_MEMORY_free(pt),"free");Check(PK_MEMORY_free(pc),"free");Check(PK_MEMORY_free(pp),"free");Check(PK_MEMORY_free(pch),"free");Check(PK_MEMORY_free(ps),"free");
        Check(KernelRuntime.MemoryFree((void*)mt),"free");Check(KernelRuntime.MemoryFree((void*)mc),"free");Check(KernelRuntime.MemoryFree((void*)mp),"free");Check(KernelRuntime.MemoryFree((void*)mch),"free");Check(KernelRuntime.MemoryFree((void*)ms),"free");
    }
}

static unsafe string[] RegionSignatures(bool managed, int count, int* tags, int* classes,
    int relations, int* parents, int* children, int* senses)
{
    var signatures = new List<string>();
    for (var region = 0; region < count; region++)
    {
        if (classes[region] != PK_CLASS_region) continue;
        var solid = false;
        if (managed)
        {
            byte value;
            Check(KernelRuntime.RegionIsSolid(tags[region], &value), "region material");
            solid = value != 0;
        }
        else
        {
            PK_LOGICAL_t value;
            Check(PK_REGION_is_solid(tags[region], &value), "region material");
            solid = value == PK_LOGICAL_true;
        }
        var shellCount = 0; var positiveUses = 0; var negativeUses = 0;
        for (var i = 0; i < relations; i++)
        {
            if (children[i] == region && classes[parents[i]] == PK_CLASS_body)
                Require(senses[i] == (solid ? PK_TOPOL_sense_positive_c : PK_TOPOL_sense_negative_c), "body-region sense disagrees with region material");
            if (parents[i] != region || classes[children[i]] != PK_CLASS_shell) continue;
            shellCount++;
            for (var j = 0; j < relations; j++)
            {
                if (parents[j] != children[i] || classes[children[j]] != PK_CLASS_face) continue;
                if (senses[j] == PK_TOPOL_sense_positive_c) positiveUses++;
                if (senses[j] == PK_TOPOL_sense_negative_c) negativeUses++;
            }
        }
        int queriedCount; int* shells = null;
        Check(managed ? KernelRuntime.RegionAskShells(tags[region], &queriedCount, &shells)
            : PK_REGION_ask_shells(tags[region], &queriedCount, &shells), "region shells");
        Require(queriedCount == shellCount, "region shell query disagrees with topology graph");
        if (shells != null) Check(managed ? KernelRuntime.MemoryFree(shells) : PK_MEMORY_free(shells), "free region shells");
        signatures.Add($"{solid}/{shellCount}/{positiveUses}/{negativeUses}");
    }
    return signatures.OrderBy(s => s, StringComparer.Ordinal).ToArray();
}

static unsafe byte[] TransmitReference(PK_BODY_t body)
{
    var options = new PK_PART_transmit_o_t { transmit_format = PK_transmit_format_text_c, transmit_version = 371 };
    var block = new PK_MEMORY_block_t();
    Check(PK_PART_transmit_b(1, &body, &options, &block), "reference text transmit");
    try
    {
        using var bytes = new MemoryStream();
        for (var current = &block; current != null; current = current->next)
            bytes.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
        return bytes.ToArray();
    }
    finally { Check(PK_MEMORY_block_f(&block), "free reference text"); }
}

static unsafe byte[] TransmitManaged(ManagedBodyTag body)
{
    var options = new M.PK_PART_transmit_o_s
    {
        o_t_version = 4,
        transmit_format = M.ParasolidConstants.PK_transmit_format_text_c,
        transmit_version = 371,
        transmit_meshes = M.ParasolidConstants.PK_transmit_meshes_separate_c,
    };
    var block = new M.PK_MEMORY_block_s();
    Check(KernelRuntime.PartTransmitB(1, &body, &options, &block), "managed text transmit");
    try
    {
        using var bytes = new MemoryStream();
        for (var current = &block; current != null; current = current->next)
            bytes.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
        return bytes.ToArray();
    }
    finally { Check(KernelRuntime.MemoryBlockFree(&block), "free managed text"); }
}

static unsafe ManagedBodyTag ReceiveManaged(byte[] bytes)
{
    fixed (byte* pointer = bytes)
    {
        var block = new M.PK_MEMORY_block_s { bytes = pointer, n_bytes = (nuint)bytes.Length };
        var options = new M.PK_PART_receive_o_s
        {
            o_t_version = 1,
            transmit_format = M.ParasolidConstants.PK_transmit_format_text_c,
        };
        int count = 0;
        ManagedBodyTag* parts = null;
        Check(KernelRuntime.PartReceiveB(block, &options, &count, &parts), "managed public receive");
        try
        {
            Require(count == 1 && parts != null, $"managed receive returned {count} parts");
            return parts[0];
        }
        finally { if (parts != null) Check(KernelRuntime.MemoryFree(parts), "free managed receive list"); }
    }
}

static unsafe PK_BODY_t ReceiveOracle(byte[] bytes)
{
    fixed (byte* pointer = bytes)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer);
        var options = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int count = 0;
        PK_PART_t* parts = null;
        Check(PK_PART_receive_b(block, &options, &count, &parts), "PK receive managed text");
        try
        {
            Require(count == 1 && parts != null, $"PK receive returned {count} parts");
            return parts[0];
        }
        finally { if (parts != null) Check(PK_MEMORY_free(parts), "free PK receive list"); }
    }
}


static unsafe void CompareBodies(PK_BODY_t reference, PK_BODY_t actual, string label, Action<string> log)
{
    var options = new PK_DEBUG_BODY_compare_o_t
    {
        max_diffs = 64,
        all_tests = PK_LOGICAL_false,
        num_tests = PK_LOGICAL_true,
        tol_dev_tests = PK_LOGICAL_true,
        tol_tests = PK_LOGICAL_true,
        class_tests = PK_LOGICAL_true,
        sense_tests = PK_LOGICAL_true,
        missing_tests = PK_LOGICAL_true,
        // Exact deviation and matching depend on internal parameterization.
        // Curve D0-D2, surface D0/Du/Dv/Duv in native parameters and topology
        // are separately strict gates.
        acc_dev_tests = PK_LOGICAL_false,
        non_match_tests = PK_LOGICAL_false,
    };
    var result = new PK_DEBUG_BODY_compare_r_t();
    Check(PK_DEBUG_BODY_compare(reference, actual, &options, &result), label + " body compare");
    try
    {
        log($"{label} body compare: global={result.global_result}, local={result.local_result}, global entries={result.n_global_diffs}, face pairs={result.n_face_pairs}.");
        for (var i = 0; i < result.n_global_diffs; i++)
        {
            var diff = result.global_diffs[i];
            log($"  global entry={diff.diff}, master count={diff.n_masters}, received count={diff.n_similars}");
        }
        var localDifferences = 0;
        for (var i = 0; i < result.n_face_pairs; i++)
        for (var j = 0; j < result.face_pairs[i].n_local_diffs; j++)
        {
            var diff = result.face_pairs[i].local_diffs[j];
            localDifferences++;
            log($"  face pair={i}, diff={diff.diff}, master entity={diff.master_entity}, received entity={diff.similar_entity}, master value={diff.master_double:R}, received value={diff.similar_double:R}");
        }
        // PK includes equal-count records in global_diffs even when its
        // global and local result flags both report no differences.
        Require(result.global_result == PK_DEBUG_global_res_no_diffs_c
            && result.local_result == PK_DEBUG_local_res_no_diffs_c
            && localDifferences == 0, label + " body structure/sense/tolerance comparison failed");
    }
    finally { Check(PK_DEBUG_BODY_compare_r_f(&result), label + " free body compare"); }
}

static unsafe BodyCounts OracleCounts(PK_BODY_t body)
    => new(OracleCount(body, 0), OracleCount(body, 1), OracleCount(body, 2), OracleCount(body, 3), OracleCount(body, 4));

static unsafe BodyCounts ManagedCounts(ManagedBodyTag body)
    => new(ManagedCount(body, 0), ManagedCount(body, 1), ManagedCount(body, 2), ManagedCount(body, 3), ManagedCount(body, 4));

static unsafe int OracleCount(PK_BODY_t body, int kind)
{
    int count = 0;
    int* entities = null;
    var error = kind switch
    {
        0 => PK_BODY_ask_regions(body, &count, &entities),
        1 => PK_BODY_ask_shells(body, &count, &entities),
        2 => PK_BODY_ask_faces(body, &count, &entities),
        3 => PK_BODY_ask_edges(body, &count, &entities),
        _ => PK_BODY_ask_vertices(body, &count, &entities),
    };
    Check(error, "PK topology count " + kind);
    try { return count; }
    finally { if (entities != null) Check(PK_MEMORY_free(entities), "free PK topology list"); }
}

static unsafe int ManagedCount(ManagedBodyTag body, int kind)
{
    int count = 0;
    int* entities = null;
    var error = kind switch
    {
        0 => KernelRuntime.BodyAskRegions(body, &count, &entities),
        1 => KernelRuntime.BodyAskShells(body, &count, &entities),
        2 => KernelRuntime.BodyAskFaces(body, &count, &entities),
        3 => KernelRuntime.BodyAskEdges(body, &count, &entities),
        _ => KernelRuntime.BodyAskVertices(body, &count, &entities),
    };
    Check(error, "managed topology count " + kind);
    try { return count; }
    finally { if (entities != null) Check(KernelRuntime.MemoryFree(entities), "free managed topology list"); }
}

static void Check(int error, string operation)
{
    if (error != 0) throw new InvalidOperationException($"{operation} failed: {error}");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

readonly record struct BodyCounts(int Regions, int Shells, int Faces, int Edges, int Vertices);

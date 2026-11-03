using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using Viewer.IContract;
using static parasolid;

namespace ProjectGmKernel.DisplayBridge;

public enum DisplayEntityKind { Face, Edge, Vertex }
public readonly record struct DisplayEntity(BodyTag BodyTag,EntityTag EntityTag,DisplayEntityKind Kind,ParasolidEntityTag ParasolidTag,PersistentIdentifier Identifier);
public readonly record struct DisplayTriangle(Vector3 A,Vector3 B,Vector3 C,Vector3 NormalA,Vector3 NormalB,Vector3 NormalC,DisplayEntity Face);
public readonly record struct DisplaySegment(Vector3 A,Vector3 B,DisplayEntity Edge);
public readonly record struct DisplayPick(DisplayEntity Entity,double Distance,Vector3 Position);

/// <summary>
/// Imports an immutable XT snapshot into owned real-PK entities. PK alone
/// facets faces and approximates display polylines. The PK partition never feeds
/// modeling results back into the independent kernel.
/// </summary>
public static unsafe class ParasolidDisplayBridge
{
    internal static readonly object ParasolidGate=new();

    /// <summary>Both the managed kernel session and a real PK session must already be active.</summary>
    public static DisplayBody Create(int body,double chordTolerance=1e-3)
    {
        if(!double.IsFinite(chordTolerance)||chordTolerance<=0)throw new ArgumentOutOfRangeException(nameof(chordTolerance));
        CheckManaged(XtWriter.CaptureDisplay(body,out var text,out var bindings,out var sessionGeneration,out var bodyGeneration),"capture display snapshot");
        return CreateSnapshot(body,text,bindings,chordTolerance,
            binding=>XtWriter.IsCurrentDisplayBinding(binding,sessionGeneration,bodyGeneration));
    }

    /// <summary>Connect a body created through the independent NativeAOT C ABI.</summary>
    public static DisplayBody CreateFromNativeLibrary(string libraryPath,int body,double chordTolerance=1e-3)
    {
        if(!double.IsFinite(chordTolerance)||chordTolerance<=0)throw new ArgumentOutOfRangeException(nameof(chordTolerance));
        var library=NativeLibrary.Load(libraryPath);bool retained=false;
        try
        {
            var capture=(delegate* unmanaged<int,PgmDisplaySnapshot*,int>)NativeLibrary.GetExport(library,"PGM_BODY_export_display");
            var free=(delegate* unmanaged<PgmDisplaySnapshot*,int>)NativeLibrary.GetExport(library,"PGM_DISPLAY_snapshot_free");
            var validation=NativeLibrary.GetExport(library,"PGM_DISPLAY_validate_pick");
            PgmDisplaySnapshot snapshot=default;
            CheckManaged(capture(body,&snapshot),"capture NativeAOT display snapshot");
            try
            {
                var text=Encoding.UTF8.GetString(new ReadOnlySpan<byte>(snapshot.Xt.bytes,checked((int)snapshot.Xt.n_bytes)));
                var bindings=new XtWriter.DisplayBinding[snapshot.BindingCount];
                for(var i=0;i<bindings.Length;i++)
                {
                    var entry=snapshot.Bindings[i];bindings[i]=new(body,entry.Entity,entry.Class,entry.Identifier,entry.Generation);
                }
                var session=snapshot.SessionGeneration;var bodyGeneration=snapshot.BodyGeneration;
                var display=CreateSnapshot(body,text,bindings,chordTolerance,binding=>ValidateNative(validation,session,body,bodyGeneration,binding));
                display.ReleaseSource=()=>NativeLibrary.Free(library);retained=true;return display;
            }
            finally { free(&snapshot); }
        }
        finally { if(!retained)NativeLibrary.Free(library); }
    }
    private static bool ValidateNative(nint function,int session,int body,int bodyGeneration,XtWriter.DisplayBinding binding)
    {
        byte valid=0;var validate=(delegate* unmanaged<int,int,int,int,int,int,byte*,int>)function;
        return validate(session,body,bodyGeneration,binding.Entity,binding.Generation,binding.Class,&valid)==0&&valid!=0;
    }

    private static DisplayBody CreateSnapshot(int body,string text,XtWriter.DisplayBinding[] bindings,double tolerance,Func<XtWriter.DisplayBinding,bool> validate)
    {
        lock(ParasolidGate)
        {
            PK_PARTITION_t previous=0,partition=0;PK_PART_t* parts=null;int partCount=0;bool success=false;
            var partitionError=PK_SESSION_ask_curr_partition(&previous);
            var partitioned=partitionError==0;
            if(!partitioned&&partitionError!=PK_ERROR_rollback_not_started)Check(partitionError,"ask PK partition");
            try
            {
                // Partition APIs require the host's rollback storage. A viewer
                // must also work in a normal PK session without changing its
                // rollback configuration: then ownership is the imported part.
                if(partitioned)
                {
                    Check(PK_PARTITION_create_empty(&partition),"create display partition");
                    Check(PK_PARTITION_set_current(partition),"select display partition");
                }
                var bytes=Encoding.UTF8.GetBytes(text);
                fixed(byte* pointer=bytes)
                {
                    var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);
                    var options=new PK_PART_receive_o_t { transmit_format=PK_transmit_format_text_c };
                    Check(PK_PART_receive_b(block,&options,&partCount,&parts),"receive independent-kernel XT");
                }
                if(partCount!=1)throw new InvalidDataException("A display body snapshot must contain exactly one part.");
                var remoteBody=parts[0];var originByRemote=new Dictionary<int,XtWriter.DisplayBinding>();
                var entities=new Dictionary<int,DisplayEntity>();
                foreach(var binding in bindings)
                {
                    PK_ENTITY_t remote=0;Check(PK_PART_find_entity_by_ident(remoteBody,binding.Class,binding.Identifier,&remote),"resolve XT persistent identifier");
                    if(remote==0||!originByRemote.TryAdd(remote,binding))throw new InvalidDataException("The received XT identifier map is incomplete or ambiguous.");
                    var kind=binding.Class switch { PK_CLASS_face=>DisplayEntityKind.Face,PK_CLASS_edge=>DisplayEntityKind.Edge,PK_CLASS_vertex=>DisplayEntityKind.Vertex,_=>throw new InvalidDataException("Unsupported display entity class.") };
                    entities.Add(remote,new(body,binding.Entity,kind,remote,binding.Identifier));
                }
                var triangles=Facet(remoteBody,entities,tolerance);
                var paths=RenderEdges(entities,tolerance);
                var segments=new List<DisplaySegment>();
                foreach(var pair in paths)
                {
                    var entity=entities[pair.Key];
                    for(var i=1;i<pair.Value.Count;i++)if(pair.Value[i-1]!=pair.Value[i])segments.Add(new(pair.Value[i-1],pair.Value[i],entity));
                }
                var display=new DisplayBody(body,remoteBody,partition,text,entities,triangles,segments.ToArray(),paths,
                    entity=>originByRemote.TryGetValue(entity.ParasolidTag,out var binding)&&validate(binding));
                success=true;return display;
            }
            finally
            {
                if(partitioned)PK_PARTITION_set_current(previous);
                if(!success)
                {
                    if(partition!=0) { var options=new PK_PARTITION_delete_o_t();PK_PARTITION_delete(partition,&options); }
                    else if(parts!=null&&partCount>0)PK_ENTITY_delete(partCount,parts);
                }
                if(parts!=null)PK_MEMORY_free(parts);
            }
        }
    }

    private static DisplayTriangle[] Facet(PK_BODY_t body,Dictionary<int,DisplayEntity> entities,double tolerance)
    {
        var options=new PK_TOPOL_facet_o_t();
        options.control.max_facet_sides=3;options.control.match=PK_facet_match_topol_c;
        options.control.is_surface_plane_tol=PK_LOGICAL_true;options.control.surface_plane_tol=tolerance;
        options.control.is_curve_chord_tol=PK_LOGICAL_true;options.control.curve_chord_tol=tolerance;
        options.choice.facet_face=PK_LOGICAL_true;options.choice.vertex_normal=PK_LOGICAL_true;options.choice.normal_vec=PK_LOGICAL_true;
        PK_TOPOL_facet_r_t tables=default;
        Check(PK_TOPOL_facet(1,&body,null,0,&options,&tables),"facet display faces");
        try
        {
            var count=tables.number_of_facets;var fins=new int[checked(count*3)];var lengths=new int[count];
            for(var i=0;i<tables.facet_fin.length;i++)
            {
                var relation=tables.facet_fin.data[i];
                if((uint)relation.facet>=(uint)count||lengths[relation.facet]>=3)throw new InvalidDataException("PK returned a non-triangular display facet.");
                fins[relation.facet*3+lengths[relation.facet]++]=relation.fin;
            }
            var result=new DisplayTriangle[count];
            for(var i=0;i<count;i++)
            {
                if(lengths[i]!=3||i>=tables.facet_face.length||!entities.TryGetValue(tables.facet_face.face[i],out var face)||face.Kind!=DisplayEntityKind.Face)
                    throw new InvalidDataException("A PK facet has no matching source face.");
                var a=FacetVertex(fins[3*i],tables,out var na);var b=FacetVertex(fins[3*i+1],tables,out var nb);var c=FacetVertex(fins[3*i+2],tables,out var nc);
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),na+nb+nc)<0)(b,c,nb,nc)=(c,b,nc,nb);
                result[i]=new(a,b,c,na,nb,nc,face);
            }
            return result;
        }
        finally { PK_TOPOL_facet_r_f(&tables); }
    }
    private static Vector3 FacetVertex(int fin,in PK_TOPOL_facet_r_t tables,out Vector3 normal)
    {
        if((uint)fin>=(uint)tables.fin_vertex.length)throw new InvalidDataException("Invalid PK facet fin.");
        var vertex=tables.fin_vertex.vertex[fin];
        if((uint)vertex>=(uint)tables.vertex_point.length||(uint)vertex>=(uint)tables.vertex_normal.length)throw new InvalidDataException("Invalid PK facet vertex.");
        var point=tables.vertex_point.point[vertex];var norm=tables.vertex_normal.normal[vertex];
        if((uint)point>=(uint)tables.point_vec.length||(uint)norm>=(uint)tables.normal_vec.length)throw new InvalidDataException("Invalid PK facet position or normal.");
        normal=ReadVector(tables.normal_vec.vec[norm].coord);return ReadVector(tables.point_vec.vec[point].coord);
    }
    private static Vector3 ReadVector(double* p)
    {
        var result=new Vector3((float)p[0],(float)p[1],(float)p[2]);
        if(!float.IsFinite(result.X)||!float.IsFinite(result.Y)||!float.IsFinite(result.Z))throw new InvalidDataException("Display coordinates exceed the viewer's finite range.");
        return result;
    }

    // The PK line renderer returns analytic circles as circle descriptions.
    // A temporary PK B-curve display proxy makes PK perform the polyline
    // approximation too. Only the original received edge participates in ID
    // mapping; proxies are deleted immediately and never enter our model.
    private static Dictionary<int,List<Vector3>> RenderEdges(Dictionary<int,DisplayEntity> entities,double tolerance)
    {
        var collector=new LineCollector();PK_SESSION_frustrum_t previous;
        Check(PK_SESSION_ask_frustrum(&previous),"ask graphics callbacks");var callbacks=previous;
        callbacks.gosgmt=&LineSegment;callbacks.goopsg=&LineOpen;callbacks.goclsg=&LineClose;
        activeLines=collector;
        try
        {
            Check(PK_SESSION_register_frustrum(&callbacks),"install display callbacks");
            PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
            foreach(var entity in entities.Values)
            {
                if(entity.Kind!=DisplayEntityKind.Edge)continue;
                PK_CURVE_t curve=0,proxy=0;PK_CLASS_t curveClass=0;PK_INTERVAL_t interval=default;PK_LOGICAL_t sense=default;
                Check(PK_EDGE_ask_geometry(entity.ParasolidTag,PK_LOGICAL_true,&curve,&curveClass,ends,&interval,&sense),"ask display edge geometry");
                collector.Begin(entity.ParasolidTag);
                if(curveClass==PK_CLASS_line)
                {
                    // Exact finite endpoints come from PK itself; straight
                    // segments require no curve discretization or proxy.
                    collector.Points[entity.ParasolidTag].Add(ReadVector(ends[0].coord));
                    collector.Points[entity.ParasolidTag].Add(ReadVector(ends[1].coord));
                    continue;
                }
                PK_LOGICAL_t exact=default;
                Check(PK_CURVE_eval(curve,interval.value[0],0,ends),"evaluate PK display start");
                Check(PK_CURVE_eval(curve,interval.value[1],0,ends+1),"evaluate PK display end");
                collector.Points[entity.ParasolidTag].Add(ReadVector(ends[0].coord));
                Check(PK_CURVE_make_bcurve(curve,interval,PK_LOGICAL_false,PK_LOGICAL_false,tolerance*0.1,&proxy,&exact),"make PK display curve");
                try
                {
                    var options=new PK_GEOM_render_line_o_t { bcurve=PK_render_bcurve_polyline_c,is_curve_chord_tol=PK_LOGICAL_true,curve_chord_tol=tolerance };
                    var renderError=PK_GEOM_render_line(1,&proxy,null,&options);
                    if(collector.Failure!=null)throw new InvalidDataException("PK graphics callback failed.",collector.Failure);
                    Check(renderError,"render PK display polyline");
                    var path=collector.Points[entity.ParasolidTag];var end=ReadVector(ends[1].coord);
                    if(path[^1]!=end)path.Add(end);
                    if(collector.Points[entity.ParasolidTag].Count<2)throw new InvalidDataException("PK returned no display polyline for an edge.");
                }
                finally { PK_ENTITY_delete(1,&proxy); }
            }
            return collector.Points;
        }
        finally { activeLines=null;PK_SESSION_register_frustrum(&previous); }
    }
    private sealed class LineCollector
    {
        internal readonly Dictionary<int,List<Vector3>> Points=[];
        internal int CurrentEdge;
        internal Exception? Failure;
        internal void Begin(int edge) { CurrentEdge=edge;Points.Add(edge,[]); }
    }
    private static LineCollector? activeLines;
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static void LineOpen(int* type,int* count,int* tags,int* n,double* data,int* nl,int* lt,int* fail) { *fail=CONTIN; }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static void LineClose(int* type,int* count,int* tags,int* n,double* data,int* nl,int* lt,int* fail) { *fail=CONTIN; }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static void LineSegment(int* type,int* count,int* tags,int* n,double* data,int* nl,int* lt,int* fail)
    {
        *fail=CONTIN;var collector=activeLines;if(collector==null)return;
        try
        {
            if(*type!=SGTPGC)return;
            if(*nl<2||*n<1||(lt[1]!=L3TPPY&&lt[1]!=L3TPSL)||(lt[1]==L3TPSL&&*n<6))
                throw new InvalidDataException($"PK display proxy returned line type {(*nl>=2?lt[1]:-1)} with {*n} geometry elements.");
            // PK may recognize a linear B-curve and emit an exact straight
            // segment: its first two vectors are the rendered endpoints.
            // L3TPPY counts XYZ points. Analytic segment descriptions use
            // scalar parameters instead. Treating the polyline count as
            // doubles truncates a valid PK path to its first third.
            var coordinateCount=lt[1]==L3TPSL?6:checked(*n*3);
            lock(collector)
            {
                var points=collector.Points[collector.CurrentEdge];
                for(var i=0;i<coordinateCount;i+=3)
                {
                    var point=ReadVector(data+i);
                    if(points.Count==0||points[^1]!=point)points.Add(point);
                }
            }
        }
        catch(Exception exception) { collector.Failure=exception;*fail=ABORT; }
    }
    private static void Check(int error,string operation)
    { if(error!=0)throw new InvalidOperationException($"Parasolid {operation} failed ({error})."); }
    private static void CheckManaged(int error,string operation)
    { if(error!=0)throw new InvalidOperationException($"Independent kernel {operation} failed ({error})."); }
}

/// <summary>A disposable, read-only rendering of one kernel body.</summary>
public sealed class DisplayBody:IDisposable
{
    private readonly int partition;
    private readonly Dictionary<int,DisplayEntity> entities;
    private readonly Func<DisplayEntity,bool> validate;
    private readonly object lifetimeGate=new();
    private bool disposed;
    internal Action? ReleaseSource;
    public int BodyTag { get; }
    public int ParasolidBodyTag { get; }
    public string XtText { get; }
    public IReadOnlyList<DisplayTriangle> Triangles { get; }
    public IReadOnlyList<DisplaySegment> EdgeSegments { get; }
    public StripFacePart Faces { get; }=new();
    public EdgePart Edges { get; }=new();
    public IReadOnlyCollection<DisplayEntity> Entities=>entities.Values;

    internal DisplayBody(int body,int remoteBody,int partition,string text,Dictionary<int,DisplayEntity> entities,
        DisplayTriangle[] triangles,DisplaySegment[] segments,Dictionary<int,List<Vector3>> paths,Func<DisplayEntity,bool> validate)
    {
        BodyTag=body;ParasolidBodyTag=remoteBody;this.partition=partition;XtText=text;this.entities=entities;Triangles=triangles;EdgeSegments=segments;this.validate=validate;
        foreach(var triangle in triangles)
        {
            if(!Faces.TagFaces.TryGetValue(triangle.Face.EntityTag,out var face)) { face=new();face.SetColor(0.67,0.75,0.88,1.0);Faces.TagFaces.Add(triangle.Face.EntityTag,face); }
            face.InsertNextPoint(triangle.A,triangle.NormalA);face.InsertNextPoint(triangle.B,triangle.NormalB);face.InsertNextPoint(triangle.C,triangle.NormalC);face.InsertNextStrip();
        }
        foreach(var pair in paths)
        {
            var builder=new EdgeBuilder();foreach(var point in pair.Value)builder.InsertNextPoint(point);
            Edges.TagEdges.Add(entities[pair.Key].EntityTag,builder.Build());
        }
        Faces.Modified();Edges.Modified();
    }
    public bool TryResolveParasolidTag(int tag,out DisplayEntity entity)
    {
        lock(lifetimeGate)
        {
            if(!disposed&&entities.TryGetValue(tag,out entity)&&validate(entity))return true;
            entity=default;return false;
        }
    }
    public bool TryResolveViewerCell(DisplayEntityKind kind,int cell,out DisplayEntity entity)
    {
        lock(lifetimeGate)
        {
        entity=default;if(disposed)return false;
        var map=kind==DisplayEntityKind.Face?Faces.IndexTagMap:kind==DisplayEntityKind.Edge?Edges.IndexTagMap:null;
        if(map==null||!map.TryGetValue(cell,out var source))return false;
        foreach(var candidate in entities.Values)if(candidate.Kind==kind&&candidate.EntityTag==source&&validate(candidate)) { entity=candidate;return true; }
        return false;
        }
    }
    public bool TryPickRay(Vector3 origin,Vector3 direction,double edgeTolerance,out DisplayPick pick)
    {
        lock(lifetimeGate)
        {
        pick=default;
        if(disposed||!float.IsFinite(origin.X)||!float.IsFinite(origin.Y)||!float.IsFinite(origin.Z)
            ||!float.IsFinite(direction.LengthSquared())||direction.LengthSquared()==0||!double.IsFinite(edgeTolerance)||edgeTolerance<0)return false;
        direction=Vector3.Normalize(direction);var distance=double.PositiveInfinity;DisplayEntity selected=default;Vector3 point=default;
        foreach(var triangle in Triangles)
        {
            if(RayTriangle(origin,direction,triangle,out var t)&&t<distance)
            { distance=t;selected=triangle.Face;point=origin+(float)t*direction; }
        }
        var nearestOffset=double.PositiveInfinity;
        foreach(var edge in EdgeSegments)
        {
            var segment=edge.B-edge.A;var length=segment.LengthSquared();if(length==0)continue;
            var w=origin-edge.A;var b=Vector3.Dot(direction,segment);var d=Vector3.Dot(direction,w);var e=Vector3.Dot(segment,w);
            var denominator=length-b*b;var u=Math.Clamp(denominator>1e-20?(e-b*d)/denominator:e/length,0,1);
            var onSegment=edge.A+u*segment;var t=Vector3.Dot(onSegment-origin,direction);if(t<0||t>distance+edgeTolerance)continue;
            var offset=Vector3.Distance(onSegment,origin+t*direction);
            if(offset<=edgeTolerance&&offset<nearestOffset) { nearestOffset=offset;selected=edge.Edge;point=onSegment; }
        }
        if(selected.EntityTag==0||!validate(selected))return false;
        pick=new(selected,Vector3.Dot(point-origin,direction),point);return true;
        }
    }
    private static bool RayTriangle(Vector3 origin,Vector3 direction,in DisplayTriangle triangle,out double distance)
    {
        var a=triangle.B-triangle.A;var b=triangle.C-triangle.A;var h=Vector3.Cross(direction,b);var determinant=Vector3.Dot(a,h);
        distance=0;if(Math.Abs(determinant)<1e-12)return false;
        var inverse=1/determinant;var s=origin-triangle.A;var u=inverse*Vector3.Dot(s,h);if(u<0||u>1)return false;
        var q=Vector3.Cross(s,a);var v=inverse*Vector3.Dot(direction,q);if(v<0||u+v>1)return false;
        distance=inverse*Vector3.Dot(b,q);return distance>=0;
    }
    public unsafe void Dispose()
    {
        lock(lifetimeGate)
        {
            if(disposed)return;disposed=true;
            try
            {
                lock(ParasolidDisplayBridge.ParasolidGate)
                {
                    if(partition!=0)
                    {
                        var options=new PK_PARTITION_delete_o_t();parasolid.PK_PARTITION_delete(partition,&options);
                    }
                    else { var part=ParasolidBodyTag;parasolid.PK_ENTITY_delete(1,&part); }
                }
            }
            finally
            {
                foreach(var face in Faces.TagFaces.Values) { face.Points.Dispose();face.Normals.Dispose();face.Indices.Dispose(); }
                foreach(var edge in Edges.TagEdges.Values) { edge.Points.Dispose();edge.Indices.Dispose(); }
                Faces.TagFaces.Clear();Faces.IndexTagMap.Clear();Faces.TagIndexMap.Clear();
                Edges.TagEdges.Clear();Edges.IndexTagMap.Clear();Edges.TagIndexMap.Clear();
                ReleaseSource?.Invoke();ReleaseSource=null;
            }
        }
    }
}

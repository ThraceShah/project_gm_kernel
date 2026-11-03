#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
using static parasolid;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

if(!ParasolidScriptHost.TryStartSession("display facet tables",out var host,out var reason))throw new InvalidOperationException(reason);
using(host)Run();
static unsafe void Run()
{
    PK_BODY_t block,wire;Check(PK_BODY_create_solid_block(2,3,4,null,&block));
    var circleForm=new PK_CIRCLE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0,0,0),new PK_VECTOR1_t(0,0,1),new PK_VECTOR1_t(1,0,0)),2);
    PK_CURVE_t circle;Check(PK_CIRCLE_create(&circleForm,&circle));Check(PK_CURVE_make_wire_body(circle,new PK_INTERVAL_t(0,Math.Tau),&wire));
    foreach(var part in new[]{block,wire})
    {
        var options=new PK_TOPOL_facet_o_t();options.choice.facet_face=PK_LOGICAL_true;options.choice.edge_fin=PK_LOGICAL_true;
        options.choice.vertex_normal=PK_LOGICAL_true;options.choice.normal_vec=PK_LOGICAL_true;options.control.wire_edges=PK_facet_wire_edges_yes_c;
        PK_TOPOL_facet_r_t tables=default;Check(PK_TOPOL_facet(1,&part,null,0,&options,&tables));
        try
        {
            Console.WriteLine($"BODY {part}: facets{tables.number_of_facets},fins{tables.number_of_fins},vertices{tables.number_of_vertices}; facet_fin{tables.facet_fin.length},edge_fin{tables.edge_fin.length},fin_fin{tables.fin_fin.length},fin_vertex{tables.fin_vertex.length},vertex_point{tables.vertex_point.length},points{tables.point_vec.length}");
            for(var i=0;i<Math.Min(15,tables.facet_fin.length);i++)Console.WriteLine($"facetfin: {tables.facet_fin.data[i].facet},{tables.facet_fin.data[i].fin}");
            for(var i=0;i<Math.Min(15,tables.edge_fin.length);i++)Console.WriteLine($"edgefin: {tables.edge_fin.data[i].edge},{tables.edge_fin.data[i].fin}");
            for(var i=0;i<Math.Min(15,tables.fin_vertex.length);i++)Console.WriteLine($"fin{i}: vertex{tables.fin_vertex.vertex[i]}, pairedfin{(i<tables.fin_fin.length?tables.fin_fin.fin[i]:-1)}");
        }
        finally { Check(PK_TOPOL_facet_r_f(&tables)); }
        PK_SESSION_frustrum_t previous;Check(PK_SESSION_ask_frustrum(&previous));var replacement=previous;
        replacement.gosgmt=&Go.Segment;replacement.goopsg=&Go.Open;replacement.goclsg=&Go.Close;
        Check(PK_SESSION_register_frustrum(&replacement));
        try
        {
            var line=new PK_TOPOL_render_line_o_t { edge=PK_render_edge_yes_c,silhouette=PK_render_silhouette_no_c,
                bcurve=PK_render_bcurve_polyline_c,hierarch=PK_render_hierarch_no_geom_c };
            Check(PK_TOPOL_render_line(1,&part,null,0,&line));
            if(part==block)
            {
                int n;int* edges;Check(PK_BODY_ask_edges(block,&n,&edges));
                PK_CURVE_t source,proxy;PK_CLASS_t type;PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];PK_INTERVAL_t bounds;PK_LOGICAL_t sense,exact;
                Check(PK_EDGE_ask_geometry(edges[0],PK_LOGICAL_true,&source,&type,ends,&bounds,&sense));
                Console.WriteLine($"LINE range {bounds.value[0]:R}..{bounds.value[1]:R}, class{type}, curve{source}");
                Check(PK_CURVE_make_bcurve(source,bounds,PK_LOGICAL_false,PK_LOGICAL_false,1e-3,&proxy,&exact));
                var geometryOptions=new PK_GEOM_render_line_o_t { bcurve=PK_render_bcurve_polyline_c,is_curve_chord_tol=PK_LOGICAL_true,curve_chord_tol=.01 };
                Check(PK_GEOM_render_line(1,&proxy,null,&geometryOptions));Check(PK_ENTITY_delete(1,&proxy));Check(PK_MEMORY_free(edges));
            }
            if(part==wire)
            {
                PK_CURVE_t displayCurve;PK_LOGICAL_t exact;
                Check(PK_CURVE_make_bcurve(circle,new PK_INTERVAL_t(0,Math.Tau),PK_LOGICAL_false,PK_LOGICAL_false,1e-5,&displayCurve,&exact));
                var geometryOptions=new PK_GEOM_render_line_o_t { bcurve=PK_render_bcurve_polyline_c,is_curve_chord_tol=PK_LOGICAL_true,curve_chord_tol=1e-3 };
                Check(PK_GEOM_render_line(1,&displayCurve,null,&geometryOptions));Check(PK_ENTITY_delete(1,&displayCurve));
            }
        }
        finally { Check(PK_SESSION_register_frustrum(&previous)); }
    }
}
static void Check(int code){if(code!=0)throw new InvalidOperationException($"PK error {code}");}
static unsafe class Go
{
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    public static void Segment(int* type,int* count,int* tags,int* n,double* data,int* nl,int* lt,int* fail)
    { Console.WriteLine($"SEG {(*type)} tags={string.Join(',',new ReadOnlySpan<int>(tags,*count).ToArray())} geom={(*n)} line={string.Join(',',new ReadOnlySpan<int>(lt,*nl).ToArray())} coords={string.Join(',',new ReadOnlySpan<double>(data,Math.Min(*n,12)).ToArray())}");*fail=0; }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    public static void Open(int* type,int* count,int* tags,int* n,double* data,int* nl,int* lt,int* fail)
    {Console.WriteLine($"OPEN {(*type)} tags={string.Join(',',new ReadOnlySpan<int>(tags,*count).ToArray())} geom={*n} coords={string.Join(',',new ReadOnlySpan<double>(data,Math.Min(*n,12)).ToArray())}");*fail=0;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    public static void Close(int* type,int* count,int* tags,int* n,double* data,int* nl,int* lt,int* fail){*fail=0;}
}

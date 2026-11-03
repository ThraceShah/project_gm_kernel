#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=ProfileModelingOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

// Two independent producers execute the same public modeling operations.
// Our kernel never receives a reference body to create its result. Only the
// completed managed XT is passed to real PK for receive/check/body comparison.
global using ManagedBodyTag=System.Int32;
using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var output=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","profile-modeling-oracle"));
Directory.CreateDirectory(output);
var log=new StringBuilder();
void Log(string text){Console.WriteLine(text);log.AppendLine(text);}
try
{
    if(!ParasolidScriptHost.TryStartSession("Independent profile modeling oracle",out var host,out var message))throw new Exception(message);
    using(host)
    unsafe
    {
        var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed session start");
        try
        {
            var cases=new (string Name,int Shape,int Operation,double Amount)[]
            {
                ("polygon-sheet",0,0,0),("polygon-hole-sheet",1,0,0),("concave-sheet",2,0,0),
                ("polygon-extrude",0,1,0),("hole-oblique-extrude",1,1,.6),("concave-extrude",2,1,.3),
                ("arc-wire",3,3,0),("circle-wire",5,3,0),("nurbs-wire",4,3,0),
                ("arc-extrude",3,1,0),("circle-extrude",5,1,0),("nurbs-extrude",4,1,.25),
                ("polygon-partial-spin",6,2,.8),("polygon-negative-spin",6,2,-.8),
                ("polygon-full-spin",6,2,Math.Tau),("polygon-negative-full-spin",6,2,-Math.Tau),
                ("cone-full-spin",7,2,Math.Tau),("circle-full-spin",8,2,Math.Tau),
                ("nurbs-full-spin",9,2,Math.Tau),
                ("hole-partial-spin",10,2,.8),("hole-full-spin",10,2,Math.Tau),
                ("oriented-sheet",11,0,0),("oriented-oblique-extrude",11,1,.6),
                ("rational-nurbs-extrude",12,1,.25),("nurbs-partial-spin",9,2,.8),
                ("nurbs-negative-full-spin",9,2,-Math.Tau),("rational-nurbs-partial-spin",13,2,.8),
                ("rational-nurbs-negative-spin",13,2,-.8),("rational-nurbs-full-spin",13,2,Math.Tau),
                ("rational-nurbs-negative-full-spin",13,2,-Math.Tau),
                ("arc-partial-spin",14,2,.8),("arc-full-spin",14,2,Math.Tau),("circle-partial-spin",8,2,.8),
            };
            var failures=0;
            foreach(var c in cases)
            {
                try
                {
                    var ours=CreateManaged(c.Shape,c.Operation,c.Amount,out var managedTracking);
                    var reference=CreateReference(c.Shape,c.Operation,c.Amount,out var referenceTracking);
                    Require(managedTracking==referenceTracking,c.Name+" topology tracking contract mismatch");
                    var bytes=TransmitManaged(ours);
                    File.WriteAllBytes(Path.Combine(output,c.Name+"-managed.x_t"),bytes);
                    File.WriteAllBytes(Path.Combine(output,c.Name+"-reference.x_t"),TransmitReference(reference));
                    var received=ReceiveOracle(bytes);
                    for(var kind=0;kind<5;kind++)
                    {
                        var expected=OracleCount(reference,kind);var actual=OracleCount(received,kind);
                        Log($"{c.Name} topology kind {kind}: reference={expected}, received={actual}");
                        Require(expected==actual,$"{c.Name} topology kind{kind}: expected{expected}, actual{actual}");
                    }
                    CheckReferenceBody(reference,c.Name+" independent baseline",Log);
                    CheckReferenceBody(received,c.Name+" managed receive",Log);
                    CompareBodies(reference,received,c.Name,Log);
                    Log("PASS "+c.Name);
                }
                catch(Exception ex){failures++;Log("FAIL "+c.Name+": "+ex.Message);}
            }
            Require(failures==0,$"{failures}/{cases.Length} modeling cases failed.");
            Log($"PASS all {cases.Length} independent modeling cases.");
        }
        finally{Check(KernelRuntime.SessionStop(),"managed session stop");}
    }
}
catch(Exception ex){Log("FAIL: "+ex.Message);Environment.ExitCode=1;}
finally{File.WriteAllText(Path.Combine(output,"report.txt"),log.ToString());}

static unsafe int CreateManaged(int shape,int operation,double amount,out string trackingSignature)
{
    trackingSignature="";
    var body=shape is 3 or 4 or 5 or 8 or 9 or 12 or 13 or 14?ManagedCurved(shape,operation==3):ManagedPolygon(shape);
    if(operation==1)
    {
        var options=new M.PK_BODY_extrude_o_s{o_t_version=7};
        options.start_bound.bound=options.end_bound.bound=M.ParasolidConstants.PK_bound_distance_c;
        options.start_bound.forward=options.end_bound.forward=1;options.end_bound.distance=2;
        var path=new M.PK_VECTOR_s();path.coord[0]=amount;path.coord[shape==11?1:2]=Math.Sqrt(1-amount*amount);
        int output=0;var tracking=new M.PK_TOPOL_track_r_s();var results=new M.PK_TOPOL_local_r_s();
        Check(KernelRuntime.BodyExtrude(body,path,&options,&output,&tracking,&results),"managed public extrude");body=output;
        try
        {
            Require(results.status==M.ParasolidConstants.PK_local_status_ok_c,"managed extrusion local check");
            var records=new List<string>();
            for(var i=0;i<tracking.n_track_records;i++)
            {
                var record=tracking.track_records[i];var classes=new int[record.n_product_topols];
                for(var j=0;j<classes.Length;j++){int cls=0;Check(KernelRuntime.EntityAskClass(record.product_topols[j],&cls),"managed tracking product class");classes[j]=cls;}
                records.Add(DescribeTracking(record.track,new ReadOnlySpan<int>(record.original_classes,record.n_original_topols),classes));
            }
            records.Sort(StringComparer.Ordinal);trackingSignature=string.Join(";",records);
        }
        finally{Check(KernelRuntime.TopolTrackResultFree(&tracking),"free managed tracking");Check(KernelRuntime.TopolLocalResultFree(&results),"free managed local result");}
    }
    else if(operation==2)
    {
        var axis=new M.PK_AXIS1_sf_s();axis.axis.coord[2]=1;
        int count=0,result=0;Check(KernelRuntime.BodySpin(body,&axis,amount,1,&count,null,null,&result),"managed public spin");
        Require(result==M.ParasolidConstants.PK_local_check_ok_c,"managed spin local check");
    }
    return body;
}

static unsafe int CreateReference(int shape,int operation,double amount,out string trackingSignature)
{
    trackingSignature="";
    var body=shape is 3 or 4 or 5 or 8 or 9 or 12 or 13 or 14?ReferenceCurved(shape,operation==3):ReferencePolygon(shape);
    if(operation==1)
    {
        var options=new PK_BODY_extrude_o_t();options.end_bound.distance=2;
        var path=shape==11?new PK_VECTOR1_t(amount,Math.Sqrt(1-amount*amount),0):new PK_VECTOR1_t(amount,0,Math.Sqrt(1-amount*amount));
        int output=0;var tracking=new PK_TOPOL_track_r_t();var results=new PK_TOPOL_local_r_t();
        Check(PK_BODY_extrude(body,path,&options,&output,&tracking,&results),"PK independent extrude");
        try
        {
            Require(results.status==PK_local_status_ok_c,"PK independent extrude local check");
            var records=new List<string>();
            for(var i=0;i<tracking.n_track_records;i++)
            {
                var record=tracking.track_records[i];var classes=new int[record.n_product_topols];
                for(var j=0;j<classes.Length;j++){int cls=0;Check(PK_ENTITY_ask_class(record.product_topols[j],&cls),"PK tracking product class");classes[j]=cls;}
                records.Add(DescribeTracking(record.track,new ReadOnlySpan<int>(record.original_classes,record.n_original_topols),classes));
            }
            records.Sort(StringComparer.Ordinal);trackingSignature=string.Join(";",records);
        }
        finally{Check(PK_TOPOL_track_r_f(&tracking),"free PK tracking");Check(PK_TOPOL_local_r_f(&results),"free PK local result");}
        body=output;
    }
    else if(operation==2)
    {
        var axis=new PK_AXIS1_sf_t(new PK_VECTOR_t(0,0,0),new PK_VECTOR1_t(0,0,1));
        int count=0;PK_local_check_t result=default;
        Check(PK_BODY_spin(body,&axis,amount,PK_LOGICAL_true,&count,null,null,&result),"PK independent spin");
        Require(result==PK_local_check_ok_c,"PK independent spin local check");
    }
    return body;
}

static string DescribeTracking(int kind,ReadOnlySpan<int> originals,int[] products)
{
    var source=originals.ToArray();Array.Sort(source);Array.Sort(products);
    return kind+":"+string.Join(",",source)+"->"+string.Join(",",products);
}

static (double X,double Y,double Z)[] Polygon(int shape)=>shape switch
{
    1=>[(-3,-2,0),(3,-2,0),(3,2,0),(-3,2,0),(-1,-.5,0),(1,-.5,0),(1,.5,0),(-1,.5,0)],
    2=>[(0,0,0),(3,0,0),(3,1,0),(1,1,0),(1,3,0),(0,3,0)],
    6=>[(2,0,0),(4,0,0),(4,0,3),(2,0,3)],
    7=>[(2,0,0),(4,0,0),(3,0,2),(2,0,2)],
    10=>[(2,0,0),(6,0,0),(6,0,4),(2,0,4),(3,0,1),(4.5,0,1),(4.5,0,2.5),(3,0,2.5)],
    _=>[(-3,-2,0),(3,-2,0),(3,2,0),(-3,2,0)],
};
static unsafe int ManagedPolygon(int shape)
{
    var points=Polygon(shape);M.PK_VECTOR_s* values=stackalloc M.PK_VECTOR_s[points.Length];
    for(var i=0;i<points.Length;i++){values[i].coord[0]=points[i].X;values[i].coord[1]=points[i].Y;values[i].coord[2]=points[i].Z;}
    int* loops=stackalloc int[8]{0,0,0,0,1,1,1,1};
    var options=new M.PK_BODY_create_sheet_planar_o_s{o_t_version=1};options.plane.axis.coord[shape is 6 or 7 or 10?1:2]=1;
    options.basis_set.axis.coord[2]=1;options.basis_set.ref_direction.coord[0]=1;if(shape is 1 or 10)options.loops=loops;
    if(shape==11){options.basis_set.axis.coord[2]=0;options.basis_set.axis.coord[1]=1;options.basis_set.location.coord[0]=5;options.basis_set.location.coord[1]=6;options.basis_set.location.coord[2]=7;}
    int body=0;Check(KernelRuntime.BodyCreateSheetPlanar(points.Length,values,&options,&body),"managed public planar sheet");return body;
}
static unsafe int ReferencePolygon(int shape)
{
    var points=Polygon(shape);PK_VECTOR_t* values=stackalloc PK_VECTOR_t[points.Length];
    for(var i=0;i<points.Length;i++)values[i]=new PK_VECTOR_t(points[i].X,points[i].Y,points[i].Z);
    int* loops=stackalloc int[8]{0,0,0,0,1,1,1,1};var options=new PK_BODY_create_sheet_planar_o_t();
    if(shape is 6 or 7 or 10)options.plane.axis=new PK_VECTOR1_t(0,1,0);
    if(shape==11){options.basis_set.axis=new PK_VECTOR1_t(0,1,0);options.basis_set.location=new PK_VECTOR_t(5,6,7);}
    if(shape is 1 or 10)options.loops=loops;
    int body=0;Check(PK_BODY_create_sheet_planar(points.Length,values,&options,&body),"PK independent planar sheet");return body;
}

static unsafe int ManagedCurved(int shape,bool wireOnly)
{
    var spline=shape is 4 or 9 or 12 or 13;var spin=shape is 8 or 9 or 13 or 14;var closed=shape is 5 or 8;var rational=shape is 12 or 13;
    int* curves=stackalloc int[2];M.PK_INTERVAL_s* bounds=stackalloc M.PK_INTERVAL_s[2];
    if(spline)
    {
        var dimension=rational?4:3;double* poles=stackalloc double[3*dimension];
        for(var i=0;i<3;i++)
        {
            var weight=rational&&i==1?.6:1;var offset=i*dimension;
            poles[offset]=(spin?2+i:2*i-2)*weight;poles[offset+1]=!spin&&i==1?3*weight:0;
            poles[offset+2]=spin&&i==1?1.5*weight:0;if(rational)poles[offset+3]=weight;
        }
        double* knots=stackalloc double[2]{0,1};int* mults=stackalloc int[2]{3,3};
        var definition=new M.PK_BCURVE_sf_s{degree=2,n_vertices=3,vertex_dim=dimension,is_rational=(byte)(rational?1:0),vertex=poles,n_knots=2,knot=knots,knot_mult=mults,
            knot_type=M.ParasolidConstants.PK_knot_bezier_ends_c,form=M.ParasolidConstants.PK_BCURVE_form_arbitrary_c,
            self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c};
        Check(KernelRuntime.BCurveCreate(&definition,&curves[0]),"managed public BCurve");bounds[0].value[1]=1;
        curves[1]=ManagedLine(spin?4:2,0,0,-1,0,0);bounds[1].value[1]=spin?2:4;
    }
    else
    {
        var definition=new M.PK_CIRCLE_sf_s{radius=spin?.5:2};definition.basis_set.axis.coord[spin?1:2]=spin?-1:1;
        definition.basis_set.location.coord[0]=spin?3:0;definition.basis_set.ref_direction.coord[0]=1;
        Check(KernelRuntime.CircleCreate(&definition,&curves[0]),"managed public circle");bounds[0].value[1]=closed?Math.Tau:Math.PI;
        if(!closed){curves[1]=ManagedLine(spin?2.5:-2,0,0,1,0,0);bounds[1].value[1]=spin?1:4;}
    }
    var options=new M.PK_CURVE_make_wire_body_o_s{o_t_version=3,tolerance=1e-8,check=1,want_edges=1,
        sequential=M.ParasolidConstants.PK_CURVE_sequential_no_c};
    int body=0,n=0;int* edges=null;Check(KernelRuntime.CurveMakeWireBody2(closed?1:2,curves,bounds,&options,&body,&n,&edges,null),"managed public wire");
    if(!wireOnly)
    {
        int shared=-1,face=0,surface=0;byte sense=1;
        Check(KernelRuntime.EdgeMakeFacesFromWire(1,edges,&sense,&shared,&face),"managed public wire to face");
        var plane=new M.PK_PLANE_sf_s();plane.basis_set.location.coord[0]=spin?3:0;
        plane.basis_set.axis.coord[spin?1:2]=spline?(spin?1:-1):(spin?-1:1);plane.basis_set.ref_direction.coord[0]=1;
        Check(KernelRuntime.PlaneCreate(&plane,&surface),"managed public plane");
        Check(KernelRuntime.FaceAttachSurfaces(1,&face,&surface,&sense),"managed public face attach surface");
    }
    KernelRuntime.MemoryFree(edges);return body;
}

static unsafe int ReferenceCurved(int shape,bool wireOnly)
{
    var spline=shape is 4 or 9 or 12 or 13;var spin=shape is 8 or 9 or 13 or 14;var closed=shape is 5 or 8;var rational=shape is 12 or 13;
    int* curves=stackalloc int[2];PK_INTERVAL_t* bounds=stackalloc PK_INTERVAL_t[2];
    if(spline)
    {
        var dimension=rational?4:3;double* poles=stackalloc double[3*dimension];
        for(var i=0;i<3;i++)
        {
            var weight=rational&&i==1?.6:1;var offset=i*dimension;
            poles[offset]=(spin?2+i:2*i-2)*weight;poles[offset+1]=!spin&&i==1?3*weight:0;
            poles[offset+2]=spin&&i==1?1.5*weight:0;if(rational)poles[offset+3]=weight;
        }
        double* knots=stackalloc double[2]{0,1};int* mults=stackalloc int[2]{3,3};
        var definition=new PK_BCURVE_sf_t{degree=2,n_vertices=3,vertex_dim=dimension,is_rational=rational?PK_LOGICAL_true:PK_LOGICAL_false,vertex=poles,n_knots=2,knot=knots,knot_mult=mults,
            knot_type=PK_knot_bezier_ends_c,form=PK_BCURVE_form_arbitrary_c,self_intersecting=PK_self_intersect_false_c};
        Check(PK_BCURVE_create(&definition,&curves[0]),"PK independent BCurve");bounds[0]=new PK_INTERVAL_t(0,1);
        curves[1]=ReferenceLine(spin?4:2,0,0,-1,0,0);bounds[1]=new PK_INTERVAL_t(0,spin?2:4);
    }
    else
    {
        var definition=new PK_CIRCLE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(spin?3:0,0,0),spin?new PK_VECTOR1_t(0,-1,0):new PK_VECTOR1_t(0,0,1),new PK_VECTOR1_t(1,0,0)),spin?.5:2);
        Check(PK_CIRCLE_create(&definition,&curves[0]),"PK independent circle");bounds[0]=new PK_INTERVAL_t(0,closed?Math.Tau:Math.PI);
        if(!closed){curves[1]=ReferenceLine(spin?2.5:-2,0,0,1,0,0);bounds[1]=new PK_INTERVAL_t(0,spin?1:4);}
    }
    var options=new PK_CURVE_make_wire_body_o_t{tolerance=1e-8,want_edges=PK_LOGICAL_true};
    int body=0,n=0;int* edges=null;Check(PK_CURVE_make_wire_body_2(closed?1:2,curves,bounds,&options,&body,&n,&edges,null),"PK independent wire");
    if(!wireOnly)
    {
        int shared=-1,face=0,surface=0;PK_LOGICAL_t sense=PK_LOGICAL_true;
        Check(PK_EDGE_make_faces_from_wire(1,edges,&sense,&shared,&face),"PK independent wire to face");
        var axis=spline?(spin?new PK_VECTOR1_t(0,1,0):new PK_VECTOR1_t(0,0,-1)):(spin?new PK_VECTOR1_t(0,-1,0):new PK_VECTOR1_t(0,0,1));
        var plane=new PK_PLANE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(spin?3:0,0,0),axis,new PK_VECTOR1_t(1,0,0)));
        Check(PK_PLANE_create(&plane,&surface),"PK independent plane");Check(PK_FACE_attach_surfs(1,&face,&surface,&sense),"PK independent attach surface");
    }
    Check(PK_MEMORY_free(edges),"free PK edge list");return body;
}

static unsafe int ManagedLine(double x,double y,double z,double dx,double dy,double dz)
{
    var definition=new M.PK_LINE_sf_s();definition.basis_set.location.coord[0]=x;definition.basis_set.location.coord[1]=y;definition.basis_set.location.coord[2]=z;
    definition.basis_set.axis.coord[0]=dx;definition.basis_set.axis.coord[1]=dy;definition.basis_set.axis.coord[2]=dz;
    int line=0;Check(KernelRuntime.LineCreate(&definition,&line),"managed public line");return line;
}
static unsafe int ReferenceLine(double x,double y,double z,double dx,double dy,double dz)
{
    var definition=new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(x,y,z),new PK_VECTOR1_t(dx,dy,dz)));
    int line=0;Check(PK_LINE_create(&definition,&line),"PK independent line");return line;
}
static unsafe void CheckReferenceBody(int body,string name,Action<string> log)
{
    var options=new PK_BODY_check_o_t();options.max_faults=100;int count=0;PK_check_fault_t* faults=null;
    Check(PK_BODY_check(body,&options,&count,&faults),name+" full PK_BODY_check");
    try{for(var i=0;i<count;i++)log($"{name} fault={faults[i].state} entity={faults[i].entity_1} other={faults[i].entity_2}");Require(count==0,name+" geometry/topology faults: "+count);}
    finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free PK fault list");}
}
static string ScriptPath([CallerFilePath]string path="")=>path;
static void Check(int error,string operation){if(error!=0)throw new InvalidOperationException(operation+": "+error);}
static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}

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
        // Exact parameter matching depends on the producer's parameterization.
        // Geometric tolerance/deviation and topology remain strict gates.
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

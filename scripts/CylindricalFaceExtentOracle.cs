#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using System.Runtime.CompilerServices;
using M=ProjectGmKernel.Native.Generated;

var directory=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","cylinder-face-extent-oracle");Directory.CreateDirectory(directory);
if(!ParasolidScriptHost.TryStartSession("Independent certified finite cylindrical extents",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"own session");
    try
    {
        var passed=0;var failed=0;
        foreach(var kind in new[]{"full-z","full-oblique","seam","rational-bulge"})
        {
            var name=kind;if(args.Length!=0&&!name.Contains(args[0],StringComparison.Ordinal))continue;
            try
            {
                var own=Produce(true,kind);var reference=Produce(false,kind);
                var face=OwnCylinderFace(own);Require(KernelRuntime.CertifiedCylindricalFaceExtent(face,out var bounds)==AlgorithmStatus.Success,"complete cylinder extent");VerifyNativeBoundary(reference,bounds);Console.WriteLine($"{kind} certified box=({bounds.XLo:R},{bounds.XHi:R}) ({bounds.YLo:R},{bounds.YHi:R}) ({bounds.ZLo:R},{bounds.ZHi:R})");
                {
                    var bytes=Transmit(own);File.WriteAllBytes(Path.Combine(directory,name+".x_t"),bytes);
                    var received=Receive(bytes,false);CheckBody(received,name+" received");CheckBody(reference,name+" reference");
                    Compare(reference,received,name);
                }
                Console.WriteLine("PASS "+name);passed++;
            }
            catch(Exception ex){Console.WriteLine("FAIL "+name+": "+ex.Message);failed++;}
        }
        Require(failed==0,$"{failed} cylindrical extent cases failed");Console.WriteLine($"PASS all {passed} cylindrical extent cases");
    }
    finally{Check(KernelRuntime.SessionStop(),"own session stop");}
}

static unsafe int Produce(bool own,string kind)
{
    var basis=new PK_AXIS2_sf_t{location=new(2,-3,4),axis=new(0,0,1),ref_direction=new(1,0,0)};int body;
    if(kind=="full-oblique")basis=new(){location=new(.2,-.3,.4),axis=new(.6,0,.8),ref_direction=new(0,1,0)};
    if(kind.StartsWith("full-",StringComparison.Ordinal))
    {Check(own?KernelRuntime.BodyCreateSolidCyl(.75,2,(M.PK_AXIS2_sf_s*)&basis,&body):PK_BODY_create_solid_cyl(.75,2,&basis,&body),"full cylinder");return body;}
    basis.location=new(0,0,0);var definition=new PK_CYL_sf_t{basis_set=basis,radius=1};int cylinder;Check(own?KernelRuntime.CylCreate((M.PK_CYL_sf_s*)&definition,&cylinder):PK_CYL_create(&definition,&cylinder),"cylinder support");
    var uv=new M.PK_UVBOX_s();uv.param[0]=kind=="seam"?5.8:0;uv.param[2]=kind=="seam"?6.8:Math.PI*.5;uv.param[1]=kind=="seam"?-1:0;uv.param[3]=kind=="seam"?2:1;
    Check(own?KernelRuntime.SurfMakeSheetBody(cylinder,uv,&body):PK_SURF_make_sheet_body(cylinder,*(PK_UVBOX_t*)&uv,&body),"partial cylinder sheet");
    if(kind=="rational-bulge")WarpBoundary(own,body);
    return body;
}
static unsafe int OwnCylinderFace(int body)
{
    Require(KernelRuntime.TryResolveBodySlot(body,out var slot),"own extent body");var face=KernelRuntime.Bodies[slot].FirstFaceBody;
    for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag)].Class==SurfaceClass.Cylinder)return face;
    throw new Exception("No cylindrical face");
}
static unsafe void WarpBoundary(bool own,int body)
{
    int count;int* edges;Check(own?KernelRuntime.BodyAskEdges(body,&count,&edges):PK_BODY_ask_edges(body,&count,&edges),"boundary edges");
    double* poles=stackalloc double[12];double* knots=stackalloc double[2];int* mult=stackalloc int[2]{3,3};PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];var changed=0;
    try
    {
        for(var e=0;e<count;e++)
        {
            int curve,kind;PK_INTERVAL_t range;PK_LOGICAL_t sense;var reverse=false;
            if(own){Check(KernelRuntime.EdgeAskCurve(edges[e],&curve),"own boundary curve");Check(KernelRuntime.EntityAskClass(curve,&kind),"own class");var edge=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(edges[e]).Slot];range.value[0]=edge.ParameterStart;range.value[1]=edge.ParameterEnd;reverse=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(curve)].Sense==PK_TOPOL_sense_negative_c;}
            else{Check(PK_EDGE_ask_geometry(edges[e],PK_LOGICAL_true,&curve,&kind,ends,&range,&sense),"native boundary geometry");reverse=sense==PK_LOGICAL_false;}
            if(kind!=PK_CLASS_circle)continue;PK_CIRCLE_sf_t sf;Check(own?KernelRuntime.CircleAsk(curve,(M.PK_CIRCLE_sf_s*)&sf):PK_CIRCLE_ask(curve,&sf),"boundary circle");if(Math.Abs(sf.basis_set.location.coord[2]-1)>1e-12)continue;
            var middle=(range.value[0]+range.value[1])*.5;var weight=Math.Cos((range.value[1]-range.value[0])*.5);Require(weight>0,"positive rational boundary");var x=sf.basis_set.ref_direction;var a=sf.basis_set.axis;var y=new PK_VECTOR_t(a.coord[1]*x.coord[2]-a.coord[2]*x.coord[1],a.coord[2]*x.coord[0]-a.coord[0]*x.coord[2],a.coord[0]*x.coord[1]-a.coord[1]*x.coord[0]);
            for(var i=0;i<3;i++){var angle=i==0?range.value[0]:i==1?middle:range.value[1];var w=i==1?weight:1;for(var d=0;d<3;d++)poles[4*i+d]=w*sf.basis_set.location.coord[d]+sf.radius*(Math.Cos(angle)*x.coord[d]+Math.Sin(angle)*y.coord[d]);poles[4*i+3]=w;}
            poles[6]+=2*weight;if(reverse)for(var d=0;d<4;d++)(poles[d],poles[8+d])=(poles[8+d],poles[d]);knots[0]=range.value[0];knots[1]=range.value[1];
            var definition=new PK_BCURVE_sf_t{degree=2,n_vertices=3,vertex_dim=4,is_rational=PK_LOGICAL_true,vertex=poles,n_knots=2,knot=knots,knot_mult=mult,form=PK_BCURVE_form_arbitrary_c,knot_type=PK_knot_bezier_ends_c,self_intersecting=PK_self_intersect_unset_c};int created;
            Check(own?KernelRuntime.BCurveCreate((M.PK_BCURVE_sf_s*)&definition,&created):PK_BCURVE_create(&definition,&created),"rational axial bulge");Check(own?KernelRuntime.TopologyDetachGeometry(edges[e]):PK_TOPOL_detach_geom(edges[e]),"detach top circle");Check(own?KernelRuntime.EdgeAttachCurves(1,edges+e,&created):PK_EDGE_attach_curves(1,edges+e,&created),"attach bulged boundary");changed++;
        }
    }
    finally{if(edges!=null)Check(own?KernelRuntime.MemoryFree(edges):PK_MEMORY_free(edges),"boundary edge buffer");}
    Require(changed==1,"one rational bulged boundary");
}
static unsafe void VerifyNativeBoundary(int body,IntervalBox3 box)
{
    int count;int* faces;Check(PK_BODY_ask_faces(body,&count,&faces),"native cylinder faces");var checkedFaces=0;
    try
    {
        for(var f=0;f<count;f++)
        {
            int surface,kind;Check(PK_FACE_ask_surf(faces[f],&surface),"native face support");Check(PK_ENTITY_ask_class(surface,&kind),"native surface class");if(kind!=PK_CLASS_cyl)continue;checkedFaces++;
            PK_BOX_t nativeBox;Check(PK_TOPOL_find_box(faces[f],&nativeBox),"native box diagnostic");Console.WriteLine($"PK finite face box=({nativeBox.coord[0]:R},{nativeBox.coord[3]:R}) ({nativeBox.coord[1]:R},{nativeBox.coord[4]:R}) ({nativeBox.coord[2]:R},{nativeBox.coord[5]:R})");
            int n;int* edges;Check(PK_FACE_ask_edges(faces[f],&n,&edges),"native finite boundary");
            try
            {
                PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];PK_VECTOR_t point;
                for(var e=0;e<n;e++){int curve,cls;PK_INTERVAL_t range;PK_LOGICAL_t sense;Check(PK_EDGE_ask_geometry(edges[e],PK_LOGICAL_true,&curve,&cls,ends,&range,&sense),"native finite interval");for(var i=0;i<=64;i++){Check(PK_CURVE_eval(curve,range.value[0]+(range.value[1]-range.value[0])*i/64,0,&point),"native boundary point");Require(point.coord[0]>=box.XLo-1e-10&&point.coord[0]<=box.XHi+1e-10&&point.coord[1]>=box.YLo-1e-10&&point.coord[1]<=box.YHi+1e-10&&point.coord[2]>=box.ZLo-1e-10&&point.coord[2]<=box.ZHi+1e-10,"native boundary lies in certified extent");}}
            }
            finally{if(edges!=null)PK_MEMORY_free(edges);}
        }
    }
    finally{if(faces!=null)PK_MEMORY_free(faces);}
    Require(checkedFaces==1,"one finite cylinder tested");
}
static unsafe byte[] Transmit(int body)
{
    var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=PK_transmit_format_text_c,transmit_version=371,transmit_meshes=PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s block;
    Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"self text XT transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(KernelRuntime.MemoryBlockFree(&block),"XT buffers");}
}
static unsafe int Receive(byte[] bytes,bool own)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&options,&n,&parts):PK_PART_receive_b(block,&options,&n,&parts),own?"self receive":"real PK receive");
        try{Require(n==1,"one received body");return parts[0];}finally{if(parts!=null)Check(own?KernelRuntime.MemoryFree(parts):PK_MEMORY_free(parts),"receive buffers");}
    }
}
static unsafe void CheckBody(int body,string name)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&n,&faults),"full BODY_check");
    try{for(var i=0;i<n;i++)Console.WriteLine($"{name} fault={faults[i].state}, entity={faults[i].entity_1}/{faults[i].entity_2}");Require(n==0,name+" body faults="+n);}
    finally{if(faults!=null)PK_MEMORY_free(faults);}
}
static unsafe void Compare(int reference,int actual,string name)
{
    for(var kind=0;kind<5;kind++){var a=Count(reference,kind);var b=Count(actual,kind);Console.WriteLine($"{name} topology {kind}: native={a} self={b}");Require(a==b,name+" topology count "+kind);}
    var options=new PK_DEBUG_BODY_compare_o_t{max_diffs=1024,all_tests=PK_LOGICAL_false,num_tests=PK_LOGICAL_true,tol_dev_tests=PK_LOGICAL_true,tol_tests=PK_LOGICAL_true,
        class_tests=PK_LOGICAL_true,sense_tests=PK_LOGICAL_true,missing_tests=PK_LOGICAL_true,acc_dev_tests=PK_LOGICAL_false,non_match_tests=PK_LOGICAL_true};PK_DEBUG_BODY_compare_r_t result;
    Check(PK_DEBUG_BODY_compare(reference,actual,&options,&result),"structural compare");
    try
    {
        var structural=0;Console.WriteLine($"{name} compare={result.global_result}/{result.local_result}, face_pairs={result.n_face_pairs}");
        for(var i=0;i<result.n_global_diffs;i++)Console.WriteLine($"global diff={result.global_diffs[i].diff}, entities={result.global_diffs[i].n_masters}/{result.global_diffs[i].n_similars}");
        for(var f=0;f<result.n_face_pairs;f++)for(var i=0;i<result.face_pairs[f].n_local_diffs;i++)
        {
            var d=result.face_pairs[f].local_diffs[i];var chart=d.diff==PK_DEBUG_diff_n_cht_pts_c;
            Console.WriteLine($"{(chart?"CHART":"STRUCTURE")} face={f}, diff={d.diff}, entity={d.master_entity}/{d.similar_entity}, class={d.master_class}/{d.similar_class}, ints={d.master_int}/{d.similar_int}, values={d.master_double:R}/{d.similar_double:R}");
            if(!chart){structural++;DumpEntity(d.master_entity);DumpEntity(d.similar_entity);}
        }
        Require(result.global_result==PK_DEBUG_global_res_no_diffs_c&&structural==0,name+" structural differences");
    }
    finally{PK_DEBUG_BODY_compare_r_f(&result);}
    var massOptions=new PK_TOPOL_eval_mass_props_o_t();double* aMass=stackalloc double[15];double* bMass=stackalloc double[15];
    Check(PK_TOPOL_eval_mass_props(1,&reference,.99999999,&massOptions,aMass,aMass+1,aMass+2,aMass+5,aMass+14),"native mass");
    Check(PK_TOPOL_eval_mass_props(1,&actual,.99999999,&massOptions,bMass,bMass+1,bMass+2,bMass+5,bMass+14),"received mass");
    for(var i=0;i<14;i++)Require(Math.Abs(aMass[i]-bMass[i])<1e-7*(1+Math.Abs(aMass[i])),name+" mass component "+i);
}
static unsafe int Count(int body,int kind)
{
    int n;int* tags;Check(kind switch{0=>PK_BODY_ask_regions(body,&n,&tags),1=>PK_BODY_ask_shells(body,&n,&tags),2=>PK_BODY_ask_faces(body,&n,&tags),3=>PK_BODY_ask_edges(body,&n,&tags),_=>PK_BODY_ask_vertices(body,&n,&tags)},"count");
    try{return n;}finally{if(tags!=null)PK_MEMORY_free(tags);}
}
static unsafe void DumpEntity(int entity)
{
    int cls;if(entity==0||PK_ENTITY_ask_class(entity,&cls)!=0)return;Console.WriteLine($"DIAGNOSTIC entity={entity}, class={cls}");
    if(cls==PK_CLASS_line)
    {
        PK_LINE_sf_t line;Check(PK_LINE_ask(entity,&line),"diagnostic line");Console.WriteLine($"  location=({line.basis_set.location.coord[0]:R},{line.basis_set.location.coord[1]:R},{line.basis_set.location.coord[2]:R}), axis=({line.basis_set.axis.coord[0]:R},{line.basis_set.axis.coord[1]:R},{line.basis_set.axis.coord[2]:R})");
        int n;int* edges;Check(PK_CURVE_ask_edges(entity,&n,&edges),"diagnostic owners");try{for(var i=0;i<n;i++)DumpEntity(edges[i]);}finally{if(edges!=null)PK_MEMORY_free(edges);}return;
    }
    if(cls==PK_CLASS_edge)
    {
        int curve,type;PK_INTERVAL_t interval;PK_LOGICAL_t sense;PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
        Check(PK_EDGE_ask_geometry(entity,PK_LOGICAL_true,&curve,&type,ends,&interval,&sense),"diagnostic edge");
        Console.WriteLine($"  curve={curve}, type={type}, sense={(byte)sense}, range=[{interval.value[0]:R},{interval.value[1]:R}], ends=({ends[0].coord[0]:R},{ends[0].coord[1]:R},{ends[0].coord[2]:R})/({ends[1].coord[0]:R},{ends[1].coord[1]:R},{ends[1].coord[2]:R})");
        if(type==PK_CLASS_line){PK_LINE_sf_t line;Check(PK_LINE_ask(curve,&line),"diagnostic line");Console.WriteLine($"  axis=({line.basis_set.axis.coord[0]:R},{line.basis_set.axis.coord[1]:R},{line.basis_set.axis.coord[2]:R})");}
        if(type==PK_CLASS_ellipse){PK_ELLIPSE_sf_t ellipse;Check(PK_ELLIPSE_ask(curve,&ellipse),"diagnostic ellipse");Console.WriteLine($"  axis=({ellipse.basis_set.axis.coord[0]:R},{ellipse.basis_set.axis.coord[1]:R},{ellipse.basis_set.axis.coord[2]:R}), ref=({ellipse.basis_set.ref_direction.coord[0]:R},{ellipse.basis_set.ref_direction.coord[1]:R},{ellipse.basis_set.ref_direction.coord[2]:R})");}
    }
}
static void Check(int error,string name){if(error!=0)throw new Exception($"{name}: error={error}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, sheet={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, t={KernelRuntime.LastSheetParameter:R}");}
static void Require(bool value,string message){if(!value)throw new Exception(message);}
static string ScriptPath([CallerFilePath]string path="")=>path;

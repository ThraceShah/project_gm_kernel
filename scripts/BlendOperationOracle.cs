#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=IcurveEvalBenchmark
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using M=ProjectGmKernel.Native.Generated;

VerifyRangeLayout();if(args.Contains("--abi-only"))return 0;
if(!ParasolidScriptHost.TryStartSession("Blend operations",out var session,out var message))throw new Exception(message);
using(session)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"self start");
    try
    {
        var native=OpenBlend();PK_UVBOX_t box;Check(PK_SURF_ask_uvbox(native,&box),"source box");var u=(box.param[0]+box.param[2])*.5;var uv=new PK_UV_t(u,.5);
        PK_VECTOR_t* jet=stackalloc PK_VECTOR_t[3];PK_VECTOR_t normal;Check(PK_SURF_eval_with_normal(native,uv,1,1,PK_LOGICAL_true,jet,&normal),"source jets");
        int sheet;var patch=box;patch.param[0]+=.1*(box.param[2]-box.param[0]);patch.param[2]-=.1*(box.param[2]-box.param[0]);patch.param[1]=.1;patch.param[3]=.9;Check(PK_SURF_make_sheet_body(native,patch,&sheet),"source sheet");
        var sourceBytes=Transmit(sheet);File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","blend-operation-source.x_t"),sourceBytes);
        var own=ImportSurface(sourceBytes);int copied;Check(KernelRuntime.EntityCopy(own,&copied),"standalone own source");own=copied;
        Check(PK_ENTITY_copy(native,&copied),"standalone native source");native=copied;
        var tangent=Unit(jet[1]);var sideways=Unit(jet[2]);var axis=Unit(new(tangent.coord[0]+.37*normal.coord[0]+.21*sideways.coord[0],tangent.coord[1]+.37*normal.coord[1]+.21*sideways.coord[1],tangent.coord[2]+.37*normal.coord[2]+.21*sideways.coord[2]));
        var planeSf=new PK_PLANE_sf_t();planeSf.basis_set.location=jet[0];planeSf.basis_set.axis=new(axis.coord[0],axis.coord[1],axis.coord[2]);var reference=Unit(Cross(axis,new(0,0,1)));planeSf.basis_set.ref_direction=new(reference.coord[0],reference.coord[1],reference.coord[2]);int plane,ownPlane;
        Check(PK_PLANE_create(&planeSf,&plane),"native cutter plane");Check(KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&planeSf,&ownPlane),"own cutter plane");
        var options=new PK_SURF_intersect_surf_o_t();
        Intersect(native,own,plane,ownPlane,options,"blend-plane");
        Projection(native,own,box);PairDistance(native,own,box);
        double* poles=stackalloc double[12];var secondAxis=Cross(axis,reference);
        for(var i=0;i<2;i++)for(var j=0;j<2;j++)for(var d=0;d<3;d++)poles[3*(2*i+j)+d]=jet[0].coord[d]+(i==0?-2:2)*reference.coord[d]+(j==0?-2:2)*secondAxis.coord[d];
        double* knots=stackalloc double[2]{-2,2};int* mult=stackalloc int[2]{2,2};var nurbsSf=new PK_BSURF_sf_t{u_degree=1,v_degree=1,n_u_vertices=2,n_v_vertices=2,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
        int nurbs,ownNurbs;Check(PK_BSURF_create(&nurbsSf,&nurbs),"native NURBS cutter");Check(KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&nurbsSf,&ownNurbs),"own NURBS cutter");Intersect(native,own,nurbs,ownNurbs,options,"blend-nurbs");
        var matrix=new M.PK_TRANSF_sf_s();var c=Math.Cos(Math.PI);var s=Math.Sin(Math.PI);matrix.matrix[0]=matrix.matrix[15]=1;matrix.matrix[5]=matrix.matrix[10]=c;matrix.matrix[6]=-s;matrix.matrix[9]=s;matrix.matrix[7]=jet[0].coord[1]-c*jet[0].coord[1]+s*jet[0].coord[2]+.007;matrix.matrix[11]=jet[0].coord[2]-s*jet[0].coord[1]-c*jet[0].coord[2]+.007;
        int nt,mt;Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&matrix,&nt),"native second Blend transform");Check(KernelRuntime.TransfCreate(&matrix,&mt),"own second Blend transform");int second,ownSecond;PK_LOGICAL_t exact;byte ownExact;
        Check(PK_GEOM_transform(native,nt,0,&second,&exact),"native second Blend");Check(KernelRuntime.GeomTransform(own,mt,0,&ownSecond,&ownExact),"own second Blend");
        Intersect(native,own,second,ownSecond,options,"blend-blend");
        c=Math.Cos(.4);s=Math.Sin(.4);matrix.matrix[5]=matrix.matrix[10]=c;matrix.matrix[6]=-s;matrix.matrix[9]=s;matrix.matrix[7]=jet[0].coord[1]-c*jet[0].coord[1]+s*jet[0].coord[2];matrix.matrix[11]=jet[0].coord[2]-s*jet[0].coord[1]-c*jet[0].coord[2];
        Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&matrix,&nt),"native artificial-limit transform");Check(KernelRuntime.TransfCreate(&matrix,&mt),"own artificial-limit transform");Check(PK_GEOM_transform(native,nt,0,&second,&exact),"native artificial-limit support");Check(KernelRuntime.GeomTransform(own,mt,0,&ownSecond,&ownExact),"own artificial-limit support");ArtificialLimitFailure(native,second,own,ownSecond);
    }
    finally{Check(KernelRuntime.SessionStop(),"self stop");}
}
Console.WriteLine("PASS Blend public intersections and global projection");return 0;

static unsafe void Intersect(int a,int ownA,int b,int ownB,PK_SURF_intersect_surf_o_t options,string name)
{
    int copied;Check(PK_ENTITY_copy(a,&copied),"native independent first");a=copied;Check(PK_ENTITY_copy(b,&copied),"native independent second");b=copied;Check(KernelRuntime.EntityCopy(ownA,&copied),"own independent first");ownA=copied;Check(KernelRuntime.EntityCopy(ownB,&copied),"own independent second");ownB=copied;
    int np,nc;PK_VECTOR_t* points;int* curves;PK_INTERVAL_t* bounds;int* types;var watch=Stopwatch.StartNew();var nativeError=PK_SURF_intersect_surf(a,b,&options,&np,&points,&nc,&curves,&bounds,&types);
    Check(nativeError,name+" native intersect");Console.WriteLine($"{name} native np={np} nc={nc} ms={watch.Elapsed.TotalMilliseconds:F3}");
    for(var curve=0;curve<nc;curve++)
    {
        var low=double.PositiveInfinity;var high=double.NegativeInfinity;int cls;PK_ENTITY_ask_class(curves[curve],&cls);
        for(var j=0;j<=16;j++){PK_VECTOR_t p;PK_UV_t parameters;Check(PK_CURVE_eval(curves[curve],bounds[curve].value[0]+(bounds[curve].value[1]-bounds[curve].value[0])*j/16,0,&p),"native preflight sample");Check(PK_SURF_parameterise_vector(a,p,&parameters),"native first-surface membership");low=Math.Min(low,parameters.param[1]);high=Math.Max(high,parameters.param[1]);Check(PK_SURF_parameterise_vector(b,p,&parameters),"native second-surface membership");}
        Console.WriteLine($"native curve={curve} class={cls} first V range={low:R}..{high:R}");
    }
    int mp,mc;M.PK_VECTOR_s* ownPoints;int* ownCurves;M.PK_INTERVAL_s* ownBounds;int* ownTypes;watch.Restart();var error=KernelRuntime.SurfIntersectSurf(ownA,ownB,(M.PK_SURF_intersect_surf_o_s*)&options,&mp,&ownPoints,&mc,&ownCurves,&ownBounds,&ownTypes);Console.WriteLine($"{name} own error={error} np={mp} nc={mc} ms={watch.Elapsed.TotalMilliseconds:F3}");Check(error,name+" own intersect");
    try
    {
        if(np!=mp||nc!=mc||nc==0)throw new Exception(name+" intersection counts differ");
        M.PK_VECTOR_s* ownJet=stackalloc M.PK_VECTOR_s[2];PK_VECTOR_t* referenceJet=stackalloc PK_VECTOR_t[2];
        for(var i=0;i<mc;i++)
        {
            int body,nativeBody;Check(KernelRuntime.CurveMakeWireBody(ownCurves[i],ownBounds[i],&body),"own intersection wire");Check(PK_CURVE_make_wire_body(curves[i],bounds[i],&nativeBody),"native intersection wire");Compare(body,nativeBody,name+"-"+i);
            for(var j=0;j<=16;j++)
            {
                var t=ownBounds[i].value[0]+(ownBounds[i].value[1]-ownBounds[i].value[0])*j/16;Check(KernelRuntime.CurveEval(ownCurves[i],t,1,ownJet),"own intersection sample");var p=ownJet[0];double parameter;
                Check(PK_CURVE_parameterise_vector(curves[i],*(PK_VECTOR_t*)&p,&parameter),"native intersection membership");Check(PK_CURVE_eval(curves[i],parameter,1,referenceJet),"native intersection sample");for(var d=0;d<3;d++)if(Math.Abs(referenceJet[0].coord[d]-p.coord[d])>1e-8)throw new Exception(name+" spatial curve mismatch");
                var parameters=new M.PK_UV_s();Check(KernelRuntime.SurfParameteriseVector(ownA,p,&parameters),"own first-surface inverse");Check(KernelRuntime.SurfParameteriseVector(ownB,p,&parameters),"own second-surface inverse");
                var tangent=Unit(*(PK_VECTOR_t*)&ownJet[1]);var nativeTangent=Unit(referenceJet[1]);var dot=0.0;for(var d=0;d<3;d++)dot+=tangent.coord[d]*nativeTangent.coord[d];if(Math.Abs(dot)<1-1e-6)throw new Exception(name+" tangent mismatch");
                if(j==0||j==16){PK_VECTOR_t start,end;Check(PK_CURVE_eval(curves[i],bounds[i].value[0],0,&start),"native first end");Check(PK_CURVE_eval(curves[i],bounds[i].value[1],0,&end),"native last end");var closed=Distance(start,end)<1e-8;if(!closed&&Math.Min(Distance(*(PK_VECTOR_t*)&p,start),Distance(*(PK_VECTOR_t*)&p,end))>1e-8)throw new Exception(name+" endpoint mismatch");}
            }
        }
    }
    finally
    {
        if(points!=null)PK_MEMORY_free(points);if(curves!=null)PK_MEMORY_free(curves);if(bounds!=null)PK_MEMORY_free(bounds);if(types!=null)PK_MEMORY_free(types);
        if(ownPoints!=null)KernelRuntime.MemoryFree(ownPoints);if(ownCurves!=null)KernelRuntime.MemoryFree(ownCurves);if(ownBounds!=null)KernelRuntime.MemoryFree(ownBounds);if(ownTypes!=null)KernelRuntime.MemoryFree(ownTypes);
    }
}
static unsafe void Projection(int native,int own,PK_UVBOX_t box)
{
    var u=(box.param[0]+box.param[2])*.5;var uv=new PK_UV_t(u,.4);PK_VECTOR_t p,n;Check(PK_SURF_eval_with_normal(native,uv,0,0,PK_LOGICAL_true,&p,&n),"projection seed");for(var d=0;d<3;d++)p.coord[d]+=.07*n.coord[d];
    var options=new PK_GEOM_range_vector_o_t{have_tolerance=PK_LOGICAL_true,tolerance=1e-6};int status;PK_range_1_r_t expected;Check(PK_GEOM_range_vector(native,p,&options,&status,&expected),"native projection");var actual=new M.PK_range_1_r_s();int found;var watch=Stopwatch.StartNew();Check(KernelRuntime.GeomRangeVector(own,*(M.PK_VECTOR_s*)&p,(M.PK_GEOM_range_vector_o_s*)&options,&found,&actual),"own projection");
    Console.WriteLine($"projection native={expected.distance:R} own={actual.distance:R} ms={watch.Elapsed.TotalMilliseconds:F3}");if(found!=status||Math.Abs(actual.distance-expected.distance)>2e-6)throw new Exception("projection mismatch");
}
static unsafe int OpenBlend()
{
    double* poles=stackalloc double[18];for(var i=0;i<3;i++)for(var j=0;j<2;j++){var at=3*(2*i+j);poles[at]=i-1;poles[at+1]=2*j-1;poles[at+2]=(i==1?-.3:.3)+.1*(i-1)*(2*j-1);}
    double* knots=stackalloc double[2]{-1,1};int* um=stackalloc int[2]{3,3};int* vm=stackalloc int[2]{2,2};var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=1,n_u_vertices=3,n_v_vertices=2,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};int source;Check(PK_BSURF_create(&sf,&source),"native open NURBS");
    var plane=new PK_PLANE_sf_t();plane.basis_set.axis=new(0,1,0);plane.basis_set.ref_direction=new(1,0,0);int wall;Check(PK_PLANE_create(&plane,&wall),"native open wall");var options=new PK_SURF_create_blend_o_t();int blend;Check(PK_SURF_create_blend(source,.1,wall,.1,new(.5,0,.075),new(-.5,0,.075),&options,&blend),"native open Blend");return blend;
}
static unsafe byte[] Transmit(int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;Check(PK_PART_transmit_b(1,&body,&options,&block),"native transmit");try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}finally{PK_MEMORY_block_f(&block);}
}
static unsafe int ImportSurface(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var input=new M.PK_MEMORY_block_s{n_bytes=(nuint)bytes.Length,bytes=p};var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=PK_transmit_format_text_c};int count;int* parts;Check(KernelRuntime.PartReceiveB(input,&options,&count,&parts),"own source receive");try{if(count!=1||!KernelRuntime.TryResolveBodySlot(parts[0],out var slot)||KernelRuntime.Bodies[slot].FirstFaceBody<0)throw new Exception("opaque source");return KernelRuntime.Faces[KernelRuntime.Bodies[slot].FirstFaceBody].SurfTag;}finally{KernelRuntime.MemoryFree(parts);}
    }
}
static unsafe void Compare(int body,int reference,string name)
{
    var folder=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","blend-operations");Directory.CreateDirectory(folder);var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=PK_transmit_format_text_c,transmit_version=371,transmit_meshes=PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s block;Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"own operation transmit");byte[] bytes;
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));bytes=stream.ToArray();File.WriteAllBytes(Path.Combine(folder,name+".x_t"),bytes);}finally{KernelRuntime.MemoryBlockFree(&block);}
    fixed(byte* p=bytes)
    {
        var receive=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};var memory=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);int count;int* parts;Check(PK_PART_receive_b(memory,&receive,&count,&parts),"native operation receive");
        try
        {
            if(count!=1)throw new Exception("operation receive count");var check=new PK_BODY_check_o_t{max_faults=100};int faults;PK_check_fault_t* list;Check(PK_BODY_check(parts[0],&check,&faults,&list),"native operation BODY_check");for(var i=0;i<faults;i++)Console.WriteLine($"{name} fault={list[i].state}, entities={list[i].entity_1}/{list[i].entity_2}");if(list!=null)PK_MEMORY_free(list);if(faults!=0)throw new Exception(name+" BODY_check faults");
            var settings=new PK_DEBUG_BODY_compare_o_t{max_diffs=64,all_tests=PK_LOGICAL_false,num_tests=PK_LOGICAL_true,tol_dev_tests=PK_LOGICAL_true,tol_tests=PK_LOGICAL_true,class_tests=PK_LOGICAL_true,sense_tests=PK_LOGICAL_true,missing_tests=PK_LOGICAL_true,acc_dev_tests=PK_LOGICAL_false,non_match_tests=PK_LOGICAL_false};PK_DEBUG_BODY_compare_r_t result;Check(PK_DEBUG_BODY_compare(reference,parts[0],&settings,&result),"operation native compare");
            try{Console.WriteLine($"{name} BODY_check=0 compare={result.global_result}/{result.local_result}");if(result.global_result!=PK_DEBUG_global_res_no_diffs_c||result.local_result!=PK_DEBUG_local_res_no_diffs_c)for(var i=0;i<result.n_global_diffs;i++)Console.WriteLine($"diff={result.global_diffs[i].diff} counts={result.global_diffs[i].n_masters}/{result.global_diffs[i].n_similars}");if(result.global_result!=PK_DEBUG_global_res_no_diffs_c||result.local_result!=PK_DEBUG_local_res_no_diffs_c)throw new Exception(name+" structural compare failed");}finally{PK_DEBUG_BODY_compare_r_f(&result);}
        }
        finally{PK_MEMORY_free(parts);}
    }
}
static unsafe PK_VECTOR_t Unit(PK_VECTOR_t a){var length=Math.Sqrt(a.coord[0]*a.coord[0]+a.coord[1]*a.coord[1]+a.coord[2]*a.coord[2]);return new(a.coord[0]/length,a.coord[1]/length,a.coord[2]/length);}
static unsafe PK_VECTOR_t Cross(PK_VECTOR_t a,PK_VECTOR_t b)=>new(a.coord[1]*b.coord[2]-a.coord[2]*b.coord[1],a.coord[2]*b.coord[0]-a.coord[0]*b.coord[2],a.coord[0]*b.coord[1]-a.coord[1]*b.coord[0]);
static void Check(int error,string name){if(error!=0)throw new Exception(name+" error="+error);}
static string ScriptPath([CallerFilePath]string path="")=>path;

static unsafe double Distance(PK_VECTOR_t a,PK_VECTOR_t b){var sum=0.0;for(var d=0;d<3;d++)sum+=(a.coord[d]-b.coord[d])*(a.coord[d]-b.coord[d]);return Math.Sqrt(sum);}

static unsafe void PairDistance(int native,int own,PK_UVBOX_t box)
{
 var uv=new PK_UV_t((box.param[0]+box.param[2])*.5,.4);PK_VECTOR_t point,normal;Check(PK_SURF_eval_with_normal(native,uv,0,0,PK_LOGICAL_true,&point,&normal),"pair distance seed");for(var d=0;d<3;d++)point.coord[d]+=.07*normal.coord[d];
 var definition=new PK_SPHERE_sf_t{radius=.01};definition.basis_set.location=point;definition.basis_set.axis=new(0,0,1);definition.basis_set.ref_direction=new(1,0,0);int sphere,ownSphere;Check(PK_SPHERE_create(&definition,&sphere),"native distance sphere");Check(KernelRuntime.SphereCreate((M.PK_SPHERE_sf_s*)&definition,&ownSphere),"own distance sphere");
 var options=new PK_GEOM_range_o_t{have_tolerance=PK_LOGICAL_true,tolerance=1e-6};int nativeStatus,ownStatus;PK_range_2_r_t expected;var actual=new M.PK_range_2_r_s();Check(PK_GEOM_range(native,sphere,&options,&nativeStatus,&expected),"native Blend-sphere distance");var watch=Stopwatch.StartNew();Check(KernelRuntime.GeomRange(own,ownSphere,(M.PK_GEOM_range_o_s*)&options,&ownStatus,&actual),"own Blend-sphere distance");Console.WriteLine($"pair distance native={expected.distance:R} own={actual.distance:R} ms={watch.Elapsed.TotalMilliseconds:F3}");if(nativeStatus!=ownStatus||Math.Abs(expected.distance-actual.distance)>2e-6)throw new Exception("Blend-sphere distance mismatch");
}

static unsafe void ArtificialLimitFailure(int nativeFirst,int nativeSecond,int first,int second)
{
 int a,b;Check(PK_ENTITY_copy(nativeFirst,&a),"diagnostic native first");Check(PK_ENTITY_copy(nativeSecond,&b),"diagnostic native second");var nativeOptions=new PK_SURF_intersect_surf_o_t();int nativePoints,nativeCurves;PK_VECTOR_t* positions;int* nativeTags;PK_INTERVAL_t* nativeIntervals;int* nativeTypes;Check(PK_SURF_intersect_surf(a,b,&nativeOptions,&nativePoints,&positions,&nativeCurves,&nativeTags,&nativeIntervals,&nativeTypes),"native extension diagnostic");var rejected=0;
 try{for(var i=0;i<nativeCurves;i++){PK_VECTOR_t p;Check(PK_CURVE_eval(nativeTags[i],(nativeIntervals[i].value[0]+nativeIntervals[i].value[1])*.5,0,&p),"native extension point");PK_UV_t uv;if(PK_SURF_parameterise_vector(a,p,&uv)==PK_ERROR_not_on_surface)rejected++;}Console.WriteLine($"native extension diagnostic curves={nativeCurves} rejected by native parameterisation={rejected}");if(nativeCurves!=2||rejected!=1)throw new Exception("native extension diagnostic fixture changed");}
 finally{if(positions!=null)PK_MEMORY_free(positions);if(nativeTags!=null)PK_MEMORY_free(nativeTags);if(nativeIntervals!=null)PK_MEMORY_free(nativeIntervals);if(nativeTypes!=null)PK_MEMORY_free(nativeTypes);}
 var options=new M.PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-5,mixed_curve_category=PK_mixed_intersection_pline_c};int np,nc;M.PK_VECTOR_s* points;int* curves;M.PK_INTERVAL_s* intervals;int* types;var watch=Stopwatch.StartNew();var error=KernelRuntime.SurfIntersectSurf(first,second,&options,&np,&points,&nc,&curves,&intervals,&types);
 Console.WriteLine($"artificial-limit diagnostic error={error} np={np} nc={nc} ms={watch.Elapsed.TotalMilliseconds:F3}");if(error!=M.ParasolidConstants.PK_ERROR_cant_do_intersect||np!=0||nc!=0||points!=null||curves!=null||intervals!=null||types!=null)throw new Exception("artificial spine limit returned a partial successful result");
}

static unsafe void VerifyRangeLayout()
{
 var native=new PK_GEOM_range_o_t();var own=new M.PK_GEOM_range_o_s();var nativeBounds=(byte*)&native.param_boundI0-(byte*)&native;var ownBounds=(byte*)own.param_bound-(byte*)&own;
 var nativeOptimization=(byte*)&native.opt_level-(byte*)&native;var ownOptimization=(byte*)&own.opt_level-(byte*)&own;
 Console.WriteLine($"range options ABI native/own: size={sizeof(PK_GEOM_range_o_t)}/{sizeof(M.PK_GEOM_range_o_s)}, param_bound={nativeBounds}/{ownBounds}, opt_level={nativeOptimization}/{ownOptimization}");
 if(sizeof(PK_GEOM_range_o_t)!=sizeof(M.PK_GEOM_range_o_s)||nativeBounds!=ownBounds||nativeOptimization!=ownOptimization)throw new Exception("geometry range options ABI layout differs from the real PK binding");
}

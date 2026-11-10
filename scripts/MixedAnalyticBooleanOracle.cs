#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property UseWebProgressCapture=true
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;
using M=ProjectGmKernel.Native.Generated;

var directory=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","mixed-analytic-boolean-oracle");Directory.CreateDirectory(directory);
if(!ParasolidScriptHost.TryStartSession("Independent mixed analytic finite-domain Boolean",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"own session");
    try
    {
        var passed=0;var failed=0;
        foreach(var kind in new[]{"basic","seam","mirrored","reload-partial","reload-partial-seam","repeat-partial"})
        foreach(var function in new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})
        {
            var name=$"{kind}-{function}";if(!WebProgressCapture.Select(name))continue;if(args.Length!=0&&!name.Contains(args[0],StringComparison.Ordinal))continue;
            try
            {
                var own=Produce(true,kind,function);var reference=Produce(false,kind,function);
                Require(own.Length==reference.Length,name+" result count");
                for(var i=0;i<own.Length;i++)
                {
                    var bytes=Transmit(own[i]);File.WriteAllBytes(Path.Combine(directory,name+"-"+i+".x_t"),bytes);
                    var received=Receive(bytes,false);CheckBody(received,name+" received");CheckBody(reference[i],name+" reference");
                    Compare(reference[i],received,name);Classify(own[i],reference[i],name);
                }
                Console.WriteLine("PASS "+name);passed++;
            }
            catch(Exception ex){Console.WriteLine("FAIL "+name+": "+ex.Message);failed++;}
        }
        Require(failed==0,$"{failed} mixed analytic cases failed");Console.WriteLine($"PASS all {passed} mixed analytic Boolean cases");
    }
    finally{Check(KernelRuntime.SessionStop(),"own session stop");}
}

static unsafe int[] Produce(bool own,string kind,int function)
{
    var target=Block(own,0,0,0,4,4,2);var tool=Cylinder(own,kind=="seam"||kind.EndsWith("-seam",StringComparison.Ordinal));
    if(kind=="mirrored"){Mirror(own,target);Mirror(own,tool);}
    if(kind.StartsWith("reload-partial",StringComparison.Ordinal)||kind=="repeat-partial")
    {
        var first=Boolean(own,target,tool,PK_boolean_subtract_c);Require(first.Length==1,"first subtraction count");target=first[0];
        if(own)
        {
            var bytes=Transmit(target);File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","mixed-analytic-boolean-oracle","reload-source.x_t"),bytes);
            var original=target;target=Receive(bytes,true);Check(KernelRuntime.EntityDelete(1,&original),"delete self roundtrip source");
            Require(KernelRuntime.TryResolveBodySlot(target,out var slot)&&!KernelRuntime.Xt.IsOpaque(slot),"editable analytic roundtrip");
        }
        // A plane parallel to the cylinder axis cuts its finite angular
        // domain, turning the original closed ellipses into partial arcs.
        tool=Block(own,0,1.55,-.5,6,2.9,3);
        if(kind=="repeat-partial")
        {
            var second=Boolean(own,target,tool,PK_boolean_subtract_c);Require(second.Length==1,"second subtraction count");target=second[0];
            if(own){var old=target;target=Receive(Transmit(target),true);Check(KernelRuntime.EntityDelete(1,&old),"delete second serialized source");}
            tool=Block(own,1,0,-.5,3.3,6,3);
        }
    }
    return Boolean(own,target,tool,function);
}
static unsafe int[] Boolean(bool own,int target,int tool,int function)
{
    var options=new PK_BODY_boolean_o_t{function=function,default_tol=1e-6,tracking=PK_LOGICAL_false};int n;int* result;
    WebProgressCapture.Before(own,target,[],"PK_BODY_boolean",[tool]);
    WebProgressCapture.BooleanParameters(own,options);
    var webStarted=System.Diagnostics.Stopwatch.GetTimestamp();
    var webError=own?KernelRuntime.BodyBoolean(target,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&result):PK_BODY_boolean(target,1,&tool,&options,&n,&result);
    var webMs=System.Diagnostics.Stopwatch.GetElapsedTime(webStarted).TotalMilliseconds;
    WebProgressCapture.BooleanAfter(own,webError==0?new ReadOnlySpan<int>(result,n).ToArray():[],webMs,webError,0,null);Check(webError,"Boolean");
    try{return new ReadOnlySpan<int>(result,n).ToArray();}finally{if(result!=null)Check(own?KernelRuntime.MemoryFree(result):PK_MEMORY_free(result),"Boolean buffers");}
}
static unsafe int Block(bool own,double x,double y,double z,double width,double depth,double height)
{
    var basis=new PK_AXIS2_sf_t{location=new(x,y,z),axis=new(0,0,1),ref_direction=new(1,0,0)};int body;
    Check(own?KernelRuntime.BodyCreateSolidBlock(width,depth,height,(M.PK_AXIS2_sf_s*)&basis,&body):PK_BODY_create_solid_block(width,depth,height,&basis,&body),"block");return body;
}
static unsafe int Cylinder(bool own,bool seam)
{
    var basis=new PK_AXIS2_sf_t{location=new(-2.25,0,-2),axis=new(.6,0,.8),ref_direction=seam?new(0,1,0):new(.8,0,-.6)};int body;
    Check(own?KernelRuntime.BodyCreateSolidCyl(.3,7.5,(M.PK_AXIS2_sf_s*)&basis,&body):PK_BODY_create_solid_cyl(.3,7.5,&basis,&body),"oblique cylinder");return body;
}
static unsafe void Mirror(bool own,int body)
{
    var sf=new M.PK_TRANSF_sf_s();sf.matrix[0]=-1;sf.matrix[5]=sf.matrix[10]=sf.matrix[15]=1;int transform;
    Check(own?KernelRuntime.TransfCreate(&sf,&transform):PK_TRANSF_create((PK_TRANSF_sf_t*)&sf,&transform),"mirror transform");
    int n;int* replaced;PK_LOGICAL_t* exact;
    Check(own?KernelRuntime.BodyTransform(body,transform,0,&n,&replaced,(byte**)&exact):PK_BODY_transform(body,transform,0,&n,&replaced,&exact),"mirror input body");
    if(replaced!=null)Check(own?KernelRuntime.MemoryFree(replaced):PK_MEMORY_free(replaced),"mirror topology buffers");
    if(exact!=null)Check(own?KernelRuntime.MemoryFree(exact):PK_MEMORY_free(exact),"mirror exact buffers");
    Check(own?KernelRuntime.EntityDelete(1,&transform):PK_ENTITY_delete(1,&transform),"delete mirror transform");
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
static unsafe void Classify(int own,int reference,string name)
{
    var count=0;
    foreach(var x in new[]{-2.4,-1.3,0,.25,1.3,2.4})foreach(var y in new[]{-2.3,-.2,.15,.5,2.2})foreach(var z in new[]{-.3,.25,1,1.3,2.3})
    {
        var point=new PK_VECTOR_t(x,y,z);int expected,actual,nt,mt;
        Check(PK_BODY_contains_vector(reference,point,&expected,&nt),"native contains");Check(KernelRuntime.BodyContainsVector(own,*(M.PK_VECTOR_s*)&point,&actual,&mt),"self contains");
        Require(actual==expected,$"{name} enclosure ({x},{y},{z}): {actual}/{expected}");
        int nc,mc;Check(PK_ENTITY_ask_class(nt,&nc),"native containing class");Check(KernelRuntime.EntityAskClass(mt,&mc),"self containing class");Require(nc==mc,name+" containing class");count++;
        if(nc==PK_CLASS_region){PK_LOGICAL_t nativeSolid;byte selfSolid;Check(PK_REGION_is_solid(nt,&nativeSolid),"native region material");Check(KernelRuntime.RegionIsSolid(mt,&selfSolid),"self region material");Require(selfSolid==(byte)nativeSolid,name+" region material semantics");}
    }
    Console.WriteLine($"{name} classified {count} independent vectors");
}
static void Check(int error,string name){if(error!=0)throw new Exception($"{name}: error={error}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, sheet={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, t={KernelRuntime.LastSheetParameter:R}");}
static void Require(bool value,string message){if(!value)throw new Exception(message);}
static string ScriptPath([CallerFilePath]string path="")=>path;

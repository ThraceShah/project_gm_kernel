#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;
using M=ProjectGmKernel.Native.Generated;

var directory=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","regular-curved-boolean-oracle");Directory.CreateDirectory(directory);
if(!ParasolidScriptHost.TryStartSession("Independent regular curved solid Booleans",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"own session");
    try
    {
        var passed=0;var failures=0;
        foreach(var kind in new[]{"block","nurbs","closed-cut","disjoint","enclosed-tool","enclosed-target","two-tools","mixed-disjoint","continuous"})
        foreach(var function in new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})
        {
            var name=$"{kind}-{function}";if(args.Length!=0&&!name.Contains(args[0],StringComparison.Ordinal))continue;
            try
            {
                var ours=Produce(true,kind,function);var reference=Produce(false,kind,function);Require(ours.Length==reference.Length,name+" body count");
                var matched=new bool[reference.Length];
                for(var i=0;i<ours.Length;i++)
                {
                    var bytes=Transmit(ours[i]);File.WriteAllBytes(Path.Combine(directory,name+"-"+i+".x_t"),bytes);var received=Receive(bytes);CheckBody(received,name+" received");
                    var found=-1;var center=Center(received);
                    for(var j=0;j<reference.Length;j++)if(!matched[j]&&Distance(center,Center(reference[j]))<1e-7){found=j;break;}
                    Require(found>=0,name+" body correspondence");matched[found]=true;CheckBody(reference[found],name+" reference");Compare(reference[found],received,name);Classify(ours[i],reference[found],name);
                }
                passed++;Console.WriteLine("PASS "+name);
            }
            catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+": "+ex.Message);}
        }
        Require(failures==0,$"{failures} regular curved Boolean cases failed");Console.WriteLine($"PASS all {passed} regular curved Boolean cases");
    }
    finally{Check(KernelRuntime.SessionStop(),"own session stop");}
}

static unsafe int[] Produce(bool own,string kind,int function)
{
    var target=Roof(own,0,0,0,.8);int tool;
    if(kind=="nurbs")tool=Roof(own,.3,.2,.05,.2);
    else if(kind=="closed-cut")tool=Block(own,.5,.5,1.08,2,2,1);
    else if(kind=="disjoint")tool=Block(own,3,3,.25,1,1,1);
    else if(kind=="enclosed-tool")tool=Block(own,.5,.5,.25,.2,.2,.2);
    else if(kind=="enclosed-target")tool=Block(own,.5,.5,-.25,3,3,3);
    else tool=Block(own,.25,.25,.25,1,1,1);
    var options=new PK_BODY_boolean_o_t{function=function,default_tol=1e-5,tracking=PK_LOGICAL_false};int count;int* bodies;
    if(kind=="continuous")
    {
        var first=options;first.function=PK_boolean_subtract_c;
        Check(own?KernelRuntime.BodyBoolean(target,1,&tool,(M.PK_BODY_boolean_o_s*)&first,&count,&bodies):PK_BODY_boolean(target,1,&tool,&first,&count,&bodies),"first continuous subtraction");
        try{Require(count==1,"continuous source component");target=bodies[0];}finally{Check(own?KernelRuntime.MemoryFree(bodies):PK_MEMORY_free(bodies),"first continuous result");}
        tool=Roof(own,.4,.35,.1,.2);
    }
    int* tools=stackalloc int[2]{tool,0};var toolCount=1;
    if(kind is "two-tools" or "mixed-disjoint")
    {
        tools[0]=Block(own,.25,.35,.2,1,1,1.1);
        tools[1]=kind=="two-tools"?Block(own,.83,.62,.17,.8,.7,.91):Block(own,3,3,.25,1,1,1);toolCount=2;
        Check(own?KernelRuntime.EntityDelete(1,&tool):PK_ENTITY_delete(1,&tool),"unused initial tool");
    }
    Check(own?KernelRuntime.BodyBoolean(target,toolCount,tools,(M.PK_BODY_boolean_o_s*)&options,&count,&bodies):PK_BODY_boolean(target,toolCount,tools,&options,&count,&bodies),$"{(own?"own":"native")} {kind} Boolean");
    try{var result=new int[count];new ReadOnlySpan<int>(bodies,count).CopyTo(result);if(kind=="mixed-disjoint"&&function==PK_boolean_unite_c)Require(result.Contains(tools[1]),"disjoint tool body identity");return result;}
    finally{if(bodies!=null)Check(own?KernelRuntime.MemoryFree(bodies):PK_MEMORY_free(bodies),"Boolean buffers");}
}
static unsafe int Roof(bool own,double dx,double dy,double dz,double bulge)
{
    int* sheets=stackalloc int[6];double* poles=stackalloc double[27];double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2];
    for(var face=0;face<6;face++)
    {
        var n=face==1?3:2;var degree=n-1;mult[0]=mult[1]=n;
        for(var i=0;i<n;i++)for(var j=0;j<n;j++)
        {
            var u=(double)i/degree;var v=(double)j/degree;var at=3*(i*n+j);
            poles[at]=dx+(face switch{0 or 1 or 4=>u,2=>0,3=>1,_=>v});
            poles[at+1]=dy+(face switch{0=>1-v,1 or 2=>v,3=>u,4=>0,_=>1});
            poles[at+2]=dz+(face switch{0=>0,1=>1+(i==1&&j==1?bulge:0),2 or 5=>u,_=>v});
        }
        var sf=new PK_BSURF_sf_t{u_degree=degree,v_degree=degree,n_u_vertices=n,n_v_vertices=n,vertex_dim=3,vertex=poles,
            n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,form=PK_BSURF_form_arbitrary_c,
            u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
        int surface;Check(own?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&surface):PK_BSURF_create(&sf,&surface),"roof support");
        var box=new PK_UVBOX_t();box.param[2]=box.param[3]=1;
        Check(own?KernelRuntime.SurfMakeSheetBody(surface,*(M.PK_UVBOX_s*)&box,sheets+face):PK_SURF_make_sheet_body(surface,box,sheets+face),"roof face");
        if(own)Check(KernelRuntime.EntityDelete(1,&surface),"own source support deletion");
    }
    var options=new PK_BODY_sew_bodies_o_t{prefered_body_type=PK_BODY_sewing_solid_c,treat_as_manifold=PK_LOGICAL_true,number_of_iterations=1};
    int count,remaining,problems;int* bodies;int* unsewn;PK_BODY_problem_group_t* groups;
    Check(own?KernelRuntime.BodySewBodies(6,sheets,1e-8,(M.PK_BODY_sew_bodies_o_s*)&options,&count,&bodies,&remaining,&unsewn,&problems,(M.PK_BODY_problem_group_s**)&groups)
        :PK_BODY_sew_bodies(6,sheets,1e-8,&options,&count,&bodies,&remaining,&unsewn,&problems,&groups),"roof sewing");
    try{Require(count==1&&remaining==0&&problems==0,"closed roof solid");return bodies[0];}
    finally{if(bodies!=null)Check(own?KernelRuntime.MemoryFree(bodies):PK_MEMORY_free(bodies),"sewn buffer");if(unsewn!=null)Check(own?KernelRuntime.MemoryFree(unsewn):PK_MEMORY_free(unsewn),"unsewn buffer");if(groups!=null)Check(own?KernelRuntime.MemoryFree(groups):PK_MEMORY_free(groups),"sewing problem buffer");}
}
static unsafe int Block(bool own,double x,double y,double z,double width,double depth,double height)
{
    var basis=new PK_AXIS2_sf_t();basis.location=new(x,y,z);basis.axis=new(0,0,1);basis.ref_direction=new(1,0,0);int body;
    Check(own?KernelRuntime.BodyCreateSolidBlock(width,depth,height,(M.PK_AXIS2_sf_s*)&basis,&body):PK_BODY_create_solid_block(width,depth,height,&basis,&body),"block");return body;
}
static unsafe byte[] Transmit(int body)
{
    var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=PK_transmit_format_text_c,transmit_version=371,transmit_meshes=PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s block;
    Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"own text XT transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(KernelRuntime.MemoryBlockFree(&block),"XT buffers");}
}
static unsafe int Receive(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(PK_PART_receive_b(block,&options,&n,&parts),"real PK receive");try{Require(n==1,"one received solid");return parts[0];}finally{PK_MEMORY_free(parts);}
    }
}
static unsafe void CheckBody(int body,string name)
{
    int type;Check(PK_BODY_ask_type(body,&type),"body type");Require(type==PK_BODY_type_solid_c,name+" solid type");
    var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&n,&faults),"full BODY_check");
    try{for(var i=0;i<n;i++)Console.WriteLine($"{name} fault={faults[i].state}, entity={faults[i].entity_1}/{faults[i].entity_2}");Require(n==0,name+" body faults="+n);}
    finally{if(faults!=null)PK_MEMORY_free(faults);}
}
static unsafe void Compare(int reference,int actual,string name)
{
    for(var kind=0;kind<5;kind++){var a=Count(reference,kind);var b=Count(actual,kind);Console.WriteLine($"{name} topology {kind}: native={a} own={b}");Require(a==b,name+" topology count "+kind);}
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
            Console.WriteLine($"{(chart?"CHART":"STRUCTURE")} face={f}, diff={d.diff}, entity={d.master_entity}/{d.similar_entity}, class={d.master_class}/{d.similar_class}, int={d.master_int}/{d.similar_int}, value={d.master_double:R}/{d.similar_double:R}");if(!chart)structural++;
        }
        Require(result.global_result==PK_DEBUG_global_res_no_diffs_c&&structural==0,name+" structural differences");
    }
    finally{PK_DEBUG_BODY_compare_r_f(&result);}
    var massOptions=new PK_TOPOL_eval_mass_props_o_t();double* aMass=stackalloc double[15];double* bMass=stackalloc double[15];
    Check(PK_TOPOL_eval_mass_props(1,&reference,.99999999,&massOptions,aMass,aMass+1,aMass+2,aMass+5,aMass+14),"native mass properties");
    Check(PK_TOPOL_eval_mass_props(1,&actual,.99999999,&massOptions,bMass,bMass+1,bMass+2,bMass+5,bMass+14),"received mass properties");
    for(var i=0;i<14;i++)Require(Math.Abs(aMass[i]-bMass[i])<1e-7*(1+Math.Abs(aMass[i])),name+" mass component "+i);
}
static unsafe int Count(int body,int kind)
{
    int n;int* tags;Check(kind switch{0=>PK_BODY_ask_regions(body,&n,&tags),1=>PK_BODY_ask_shells(body,&n,&tags),2=>PK_BODY_ask_faces(body,&n,&tags),3=>PK_BODY_ask_edges(body,&n,&tags),_=>PK_BODY_ask_vertices(body,&n,&tags)},"count");
    try{return n;}finally{if(tags!=null)PK_MEMORY_free(tags);}
}
static unsafe void Classify(int own,int reference,string name)
{
    var count=0;
    foreach(var x in new[]{-.2,.25,.5,.75,1.3})foreach(var y in new[]{-.2,.25,.5,.75,1.3})foreach(var z in new[]{-.2,.25,.5,1.0,1.3})
        At(x,y,z);
    if(name.StartsWith("enclosed-tool-",StringComparison.Ordinal)){At(.5,.5,.35);At(.5,.5,.3);}
    Console.WriteLine($"{name} classified {count} independent vectors");
    void At(double x,double y,double z)
    {
        var point=new PK_VECTOR_t(x,y,z);int expected,actual,nt,mt;
        Check(PK_BODY_contains_vector(reference,point,&expected,&nt),"native contains");
        Check(KernelRuntime.BodyContainsVector(own,*(M.PK_VECTOR_s*)&point,&actual,&mt),$"own contains ({x},{y},{z})");
        Require(expected==actual,$"{name} enclosure at ({x},{y},{z}): native={expected}, own={actual}");
        int nc,mc;Check(PK_ENTITY_ask_class(nt,&nc),"native containing topology");Check(KernelRuntime.EntityAskClass(mt,&mc),"own containing topology");
        Require(nc==mc,$"{name} containing class at ({x},{y},{z}): {nc}/{mc}");
        if(nc==PK_CLASS_region){PK_LOGICAL_t nativeSolid;byte ownSolid;Check(PK_REGION_is_solid(nt,&nativeSolid),"native region");Check(KernelRuntime.RegionIsSolid(mt,&ownSolid),"own region");Require(nativeSolid==ownSolid,name+" material region semantics");}
        count++;
    }
}
static unsafe PK_VECTOR_t Center(int body)
{
    var options=new PK_TOPOL_eval_mass_props_o_t();double* values=stackalloc double[15];
    Check(PK_TOPOL_eval_mass_props(1,&body,.99999999,&options,values,values+1,values+2,values+5,values+14),"body centroid");
    return new(values[2],values[3],values[4]);
}
static unsafe double Distance(PK_VECTOR_t a,PK_VECTOR_t b){var sum=0.0;for(var d=0;d<3;d++)sum+=Math.Pow(a.coord[d]-b.coord[d],2);return Math.Sqrt(sum);}
static void Check(int error,string name){if(error!=0)throw new Exception($"{name}: error={error}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, sheet={KernelRuntime.LastSheetPartitionStage}");}
static void Require(bool value,string message){if(!value)throw new Exception(message);}
static string ScriptPath([CallerFilePath]string path="")=>path;

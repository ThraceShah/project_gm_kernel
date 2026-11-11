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

if(args.Contains("--measurement-identity-only",StringComparer.Ordinal)){_ = WebProgressCapture.Enabled;return;}


var benchRepeat=1;var benchOption=args.SingleOrDefault(x=>x.StartsWith("--bench-repeat=",StringComparison.Ordinal));
if(benchOption!=null)benchRepeat=int.Parse(benchOption["--bench-repeat=".Length..],System.Globalization.CultureInfo.InvariantCulture);
Require(benchRepeat>=1&&benchRepeat<=64,"bench repeat must be in 1..64");
ModelingPerformance.Enabled=args.Contains("--bench-profile",StringComparer.Ordinal);
var directory=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","regular-curved-boolean-oracle");Directory.CreateDirectory(directory);
if(!ParasolidScriptHost.TryStartSession("Independent regular curved solid Booleans",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"own session");
    try
    {
        if(args.Contains("--precision-evidence",StringComparer.Ordinal)){CharacterizeImprintPrecision();return;}
        var passed=0;var failures=0;
        foreach(var kind in new[]{"block","nurbs","closed-cut","disjoint","enclosed-tool","enclosed-target","two-tools","mixed-disjoint","continuous","reloaded","reloaded-block","same-domain","same-domain-reloaded","same-domain-partial","same-domain-partial-reloaded"})
        foreach(var function in new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})
        {
            var name=$"{kind}-{function}";if(!WebProgressCapture.Select(name))continue;if(args.Length!=0&&!name.Contains(args[0],StringComparison.Ordinal))continue;
            for(var benchRep=0;benchRep<benchRepeat;benchRep++)
            {
            var benchStart=System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var gmProduceStart=System.Diagnostics.Stopwatch.GetTimestamp();
                var ours=Produce(true,kind,function);
                var gmProduceMs=System.Diagnostics.Stopwatch.GetElapsedTime(gmProduceStart).TotalMilliseconds;
                var pkProduceStart=System.Diagnostics.Stopwatch.GetTimestamp();
                var reference=Produce(false,kind,function);
                var pkProduceMs=System.Diagnostics.Stopwatch.GetElapsedTime(pkProduceStart).TotalMilliseconds;
                Require(ours.Length==reference.Length,name+" body count");
                var matched=new bool[reference.Length];
                for(var i=0;i<ours.Length;i++)
                {
                    var bytes=Transmit(ours[i]);File.WriteAllBytes(Path.Combine(directory,name+"-"+i+".x_t"),bytes);var received=Receive(bytes);CheckBody(received,name+" received");
                    var found=-1;var center=Center(received);
                    for(var j=0;j<reference.Length;j++)if(!matched[j]&&Distance(center,Center(reference[j]))<1e-7){found=j;break;}
                    Require(found>=0,name+" body correspondence");matched[found]=true;CheckBody(reference[found],name+" reference");Compare(reference[found],received,name);Classify(ours[i],reference[found],name);
                }
                passed++;Console.WriteLine("PASS "+name);
                Console.WriteLine($"BENCH case={name} rep={benchRep} gm-produce-ms={gmProduceMs:F3} pk-produce-ms={pkProduceMs:F3} verify-ms={System.Diagnostics.Stopwatch.GetElapsedTime(benchStart).TotalMilliseconds-gmProduceMs-pkProduceMs:F3} case-ms={System.Diagnostics.Stopwatch.GetElapsedTime(benchStart).TotalMilliseconds:F3}");
            }
            catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+": "+ex);}
            }
        }
        Require(failures==0,$"{failures} regular curved Boolean cases failed");Console.WriteLine($"PASS all {passed} regular curved Boolean cases");
    }
    finally{Check(KernelRuntime.SessionStop(),"own session stop");}
}

static unsafe int[] Produce(bool own,string kind,int function,double tolerance=1e-5,double scale=1)
{
    var target=Roof(own,0,0,0,.8);int tool;
    if(kind.StartsWith("same-domain",StringComparison.Ordinal))
    {
        tool=Roof(own,0,0,0,.8);
        if(kind.Contains("partial",StringComparison.Ordinal)){target=TrimSource(own,target,Block(own,.1,.5,.25,.6,2,2));tool=TrimSource(own,tool,Block(own,.9,.5,.25,.6,2,2));}
        if(own&&kind.EndsWith("reloaded",StringComparison.Ordinal)){var old=tool;tool=ReceiveOwn(Transmit(tool));Check(KernelRuntime.EntityDelete(1,&old),"delete serialized same-domain source");}
    }
    else if(kind=="nurbs")tool=Roof(own,.3,.2,.05,.2);
    else if(kind=="closed-cut")tool=Block(own,.5,.5,1.08,2,2,1);
    else if(kind=="disjoint")tool=Block(own,3,3,.25,1,1,1);
    else if(kind=="enclosed-tool")tool=Block(own,.5,.5,.25,.2,.2,.2);
    else if(kind=="enclosed-target")tool=Block(own,.5,.5,-.25,3,3,3);
    else tool=Block(own,.25,.25,.25,1,1,1);
    if(scale!=1){ScaleSource(own,target,scale);ScaleSource(own,tool,scale);}
    var options=new PK_BODY_boolean_o_t{function=function,default_tol=tolerance,tracking=PK_LOGICAL_false};int count;int* bodies;
    if(kind is "continuous" or "reloaded" or "reloaded-block")
    {
        var first=options;first.function=PK_boolean_subtract_c;
        Check(own?KernelRuntime.BodyBoolean(target,1,&tool,(M.PK_BODY_boolean_o_s*)&first,&count,&bodies):PK_BODY_boolean(target,1,&tool,&first,&count,&bodies),"first continuous subtraction");
        try{Require(count==1,"continuous source component");target=bodies[0];}finally{Check(own?KernelRuntime.MemoryFree(bodies):PK_MEMORY_free(bodies),"first continuous result");}
        if(own&&kind.StartsWith("reloaded",StringComparison.Ordinal))
        {
            var bytes=Transmit(target);
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","regular-curved-boolean-oracle",$"{kind}-{function}-source.x_t"),bytes);
            var old=target;target=ReceiveOwn(bytes);Check(KernelRuntime.EntityDelete(1,&old),"delete serialized source");
            Require(KernelRuntime.TryResolveBodySlot(target,out var restored)&&!KernelRuntime.Xt.IsOpaque(restored),"editable self-received curved solid");
            var point=new M.PK_VECTOR_s();point.coord[0]=point.coord[1]=.9;point.coord[2]=.5;int enclosure,topology;
            Check(KernelRuntime.BodyContainsVector(target,point,&enclosure,&topology),"classification after self receive");Require(enclosure==PK_enclosure_inside_c,"restored material classification");
        }
        tool=kind=="reloaded-block"?Block(own,.75,.75,.5,.3,.3,.4):Roof(own,.4,.35,.1,.2);
    }
    int* tools=stackalloc int[2]{tool,0};var toolCount=1;
    if(kind is "two-tools" or "mixed-disjoint")
    {
        tools[0]=Block(own,.25,.35,.2,1,1,1.1);
        tools[1]=kind=="two-tools"?Block(own,.83,.62,.17,.8,.7,.91):Block(own,3,3,.25,1,1,1);toolCount=2;
        Check(own?KernelRuntime.EntityDelete(1,&tool):PK_ENTITY_delete(1,&tool),"unused initial tool");
    }
    if(WebProgressCapture.Enabled)
    {
        // Preserve the oracle's tracking=false option: this GM curved path rejects tracking=true.
        WebProgressCapture.Before(own,target,[],"PK_BODY_boolean_2",new ReadOnlySpan<int>(tools,toolCount).ToArray());
    WebProgressCapture.BooleanParameters(own,options);
        PK_TOPOL_track_r_t tracking=default;PK_boolean_r_t result=default;
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        var error=own?KernelRuntime.BodyBoolean2(target,toolCount,tools,(M.PK_BODY_boolean_o_s*)&options,(M.PK_TOPOL_track_r_s*)&tracking,(M.PK_boolean_r_s*)&result):PK_BODY_boolean_2(target,toolCount,tools,&options,&tracking,&result);
        var ms=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if(own&&ModelingPerformance.Enabled)PrintProfile($"Boolean {kind}-{function}",ms);
        try
        {
            var output=error==0?new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray():[];
            WebProgressCapture.BooleanAfter(own,output,ms,error,result.result,&tracking);Check(error,"web tracked Boolean");return output;
        }
        finally {if(own){KernelRuntime.TopolTrackResultFree((M.PK_TOPOL_track_r_s*)&tracking);KernelRuntime.BooleanResultFree((M.PK_boolean_r_s*)&result);}else{PK_TOPOL_track_r_f(&tracking);PK_boolean_r_f(&result);}}
    }
    Check(own?KernelRuntime.BodyBoolean(target,toolCount,tools,(M.PK_BODY_boolean_o_s*)&options,&count,&bodies):PK_BODY_boolean(target,toolCount,tools,&options,&count,&bodies),$"{(own?"own":"native")} {kind} Boolean");
    try{var result=new int[count];new ReadOnlySpan<int>(bodies,count).CopyTo(result);if(kind=="mixed-disjoint"&&function==PK_boolean_unite_c)Require(result.Contains(tools[1]),"disjoint tool body identity");return result;}
    finally{if(bodies!=null)Check(own?KernelRuntime.MemoryFree(bodies):PK_MEMORY_free(bodies),"Boolean buffers");}
}
static void PrintProfile(string operation,double apiMs)
{
    var p=ModelingPerformance.Last;var milliseconds=1000.0/System.Diagnostics.Stopwatch.Frequency;
    Console.WriteLine($"PROFILE operation={operation} api-ms={apiMs:F3} elapsed-ms={p.ElapsedTicks*milliseconds:F3} pairs={p.SurfacePairs} ssi-cells={p.SsiCells} root-cells={p.RootCells} roots={p.Roots} budget-failures={p.BudgetFailures} winding={p.WindingCalls} bound-requests={p.BoundRequests} bound-evaluations={p.BoundEvaluations} cache-hits={p.BoundCacheHits} bound-ms={p.BoundTicks*milliseconds:F3}");
    Console.WriteLine($"WORK jacobians={p.JacobianCalls} directions={p.JacobianDirections} residual-directions={p.ResidualDirections} record-builds={p.JacobianRecordBuilds} record-nodes={p.JacobianRecordNodes} record-rebinds={p.JacobianRecordRebinds}");
    foreach(var stage in Enum.GetValues<KernelRuntime.RegularBooleanStage>())if(p.Stages[(int)stage]!=0)Console.WriteLine($"PROFILE stage={stage} ms={p.Stages[(int)stage]*milliseconds:F3}");
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
static unsafe int ReceiveOwn(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new M.PK_MEMORY_block_s{bytes=p,n_bytes=(nuint)bytes.Length};var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(KernelRuntime.PartReceiveB(block,&options,&n,&parts),"self receive");try{Require(n==1,"one self-received solid");return parts[0];}finally{KernelRuntime.MemoryFree(parts);}
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
    Span<int> nativeCounts=stackalloc int[5],selfCounts=stackalloc int[5];
    for(var kind=0;kind<5;kind++){nativeCounts[kind]=Count(reference,kind);selfCounts[kind]=Count(actual,kind);Console.WriteLine($"{name} topology {kind}: native={nativeCounts[kind]} own={selfCounts[kind]}");}
    for(var kind=0;kind<5;kind++)Require(nativeCounts[kind]==selfCounts[kind],name+" topology count "+kind);
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
static void Check(int error,string name){if(error!=0)throw new Exception($"{name}: error={error}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace}, sheet={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, status={KernelRuntime.LastSheetAlgorithmStatus}");}
static void Require(bool value,string message){if(!value)throw new Exception(message);}
static string ScriptPath([CallerFilePath]string path="")=>path;

static unsafe int TrimSource(bool own,int target,int tool)
{
    var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-5,tracking=PK_LOGICAL_false};int n;int* products;
    Check(own?KernelRuntime.BodyBoolean(target,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&products):PK_BODY_boolean(target,1,&tool,&options,&n,&products),"different finite trim source");
    try{Require(n==1,"one finite trim source");if(!own)RequireAccurateSourceVertices(products[0]);return products[0];}
    finally{if(products!=null)Check(own?KernelRuntime.MemoryFree(products):PK_MEMORY_free(products),"finite trim source results");}
}
static unsafe void ScaleSource(bool own,int body,double scale)
{
    var form=new M.PK_TRANSF_sf_s();form.matrix[0]=form.matrix[5]=form.matrix[10]=scale;form.matrix[15]=1;
    form.matrix[3]=.375*scale;form.matrix[7]=-.25*scale;form.matrix[11]=.5*scale;int transform;
    Check(own?KernelRuntime.TransfCreate(&form,&transform):PK_TRANSF_create((PK_TRANSF_sf_t*)&form,&transform),"precision source transform");
    int n;int* replacements;PK_LOGICAL_t* exact;
    Check(own?KernelRuntime.BodyTransform(body,transform,0,&n,&replacements,(byte**)&exact):PK_BODY_transform(body,transform,0,&n,&replacements,&exact),"scale precision source");
    if(replacements!=null)Check(own?KernelRuntime.MemoryFree(replacements):PK_MEMORY_free(replacements),"precision replacement list");
    if(exact!=null)Check(own?KernelRuntime.MemoryFree(exact):PK_MEMORY_free(exact),"precision exact list");
    Check(own?KernelRuntime.EntityDelete(1,&transform):PK_ENTITY_delete(1,&transform),"precision source transform release");
}
static unsafe void RequireAccurateSourceVertices(int body)
{
    double sigma;Check(PK_SESSION_ask_precision(&sigma),"source session precision");int n;int* vertices;
    Check(PK_BODY_ask_vertices(body,&n,&vertices),"source vertices");
    try{for(var i=0;i<n;i++){double value;Check(PK_VERTEX_ask_precision(vertices[i],&value),"source vertex precision");Require(value<=sigma*.500000000001,"ordinary transversal source junction remains accurate");}}
    finally{if(vertices!=null)PK_MEMORY_free(vertices);}
}
static unsafe void CharacterizeImprintPrecision()
{
    double original;Check(PK_SESSION_ask_precision(&original),"original native precision");var cases=0;
    try
    {
        foreach(var requested in new[]{1e-9,1e-8,1e-7,1e-6})foreach(var tolerance in new[]{1e-9,1e-8,1e-5,1e-3})foreach(var scale in new[]{.125,1,8})
        {
            Check(PK_SESSION_set_precision(requested),"set characterization precision");double sigma;Check(PK_SESSION_ask_precision(&sigma),"actual characterization precision");Require(sigma==requested,"session precision readback");
            var products=Produce(false,"same-domain-partial",PK_boolean_subtract_c,tolerance,scale);Require(products.Length==1,"one characterization solid");var body=products[0];CheckBody(body,"precision characterization");
            int n;int* vertices;Check(PK_BODY_ask_vertices(body,&n,&vertices),"event precision vertices");var welds=0;var accurate=0;
            var dx=scale==1?0:.375*scale;var dy=scale==1?0:-.25*scale;var dz=scale==1?0:.5*scale;
            try
            {
                for(var i=0;i<n;i++)
                {
                    int tag;double precision;PK_POINT_sf_t point;Check(PK_VERTEX_ask_point(vertices[i],&tag),"event point");Check(PK_POINT_ask(tag,&point),"event position");Check(PK_VERTEX_ask_precision(vertices[i],&precision),"event vertex precision");
                    var x=point.position.coord[0];var y=point.position.coord[1];var z=point.position.coord[2];var epsilon=1e-9*Math.Max(1,scale);
                    var side=Math.Abs(y-dy)<epsilon||Math.Abs(y-dy-scale)<epsilon;
                    var welded=side&&Math.Abs(x-dx-scale)<epsilon&&Math.Abs(z-dz-.25*scale)<epsilon;
                    var nurbs=side&&Math.Abs(x-dx-.6*scale)<epsilon&&Math.Abs(z-dz-scale)<epsilon;
                    if(welded){welds++;Require(Math.Abs(precision-Math.Max(1e-7,sigma*.5))<1e-18,"PK38 planar imprint metadata minimum");}
                    if(nurbs){accurate++;Require(Math.Abs(precision-sigma*.5)<1e-18,"true NURBS junction remains accurate");}
                }
            }
            finally{if(vertices!=null)PK_MEMORY_free(vertices);}
            Require(welds==2&&accurate==2,"both manufactured imprint events and NURBS counterexamples");
            int edges;int* list;Check(PK_BODY_ask_edges(body,&edges,&list),"event edge precisions");
            try{for(var i=0;i<edges;i++){double precision;Check(PK_EDGE_ask_precision(list[i],&precision),"event edge precision");Require(precision<=sigma*.500000000001,"imprint vertex minimum is not inherited from tolerant edges");}}
            finally{if(list!=null)PK_MEMORY_free(list);}
            Console.WriteLine($"PASS precision sigma={sigma:R} default_tol={tolerance:R} scale={scale:R}: planar_welds={welds} radius={Math.Max(1e-7,sigma*.5):R}, accurate_nurbs={accurate}, accurate_incident_edges={edges}");
            Check(PK_ENTITY_delete(1,&body),"release precision result");cases++;
        }
    }
    finally{Check(PK_SESSION_set_precision(original),"restore native precision");}
    Require(cases==48,"full precision mutation matrix");Console.WriteLine("PASS all 48 independent PK38 imprint precision mutations");
}

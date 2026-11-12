#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using M=ProjectGmKernel.Native.Generated;

var directory=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","convex-plane-contact-oracle");Directory.CreateDirectory(directory);
var ssiOnly=args.Contains("--ssi-only",StringComparer.Ordinal);var selected=args.Where(a=>!a.StartsWith("--",StringComparison.Ordinal)).ToArray();
if(!ParasolidScriptHost.TryStartSession("Independent convex plane contact consumers",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"own session");
    try
    {
        foreach(var height in new[]{-.015625,0,.015625,Math.ScaleB(1,-16),Math.ScaleB(1,-20),Math.ScaleB(1,-24)})
        {
            if(selected.Length>0&&!selected.Contains(height.ToString("R",System.Globalization.CultureInfo.InvariantCulture),StringComparer.Ordinal))continue;
            Console.WriteLine($"CASE height={height:R}");
            Ssi(height);
            if(ssiOnly)continue;
            foreach(var function in new[]{PK_boolean_subtract_c,PK_boolean_intersect_c})
            {
                var own=BowlSolid(true);var native=BowlSolid(false);CheckBody(native,"source native");var received=Receive(Transmit(own));
                try{CheckBody(received,"source received");Compare(native,received,"source");}finally{Check(PK_ENTITY_delete(1,&received),"delete source receive");}
                File.WriteAllBytes(Path.Combine(directory,$"source-{height:R}-{function}.x_t"),Transmit(own));
                var ownTool=Block(true,0,0,-1,2,2,1.5+height);var nativeTool=Block(false,0,0,-1,2,2,1.5+height);
                var options=new PK_BODY_boolean_o_t{function=function,default_tol=1e-9,tracking=PK_LOGICAL_false};int mn=0,pn=0;int* mb=null;int* pb=null;
                var started=Stopwatch.GetTimestamp();var me=KernelRuntime.BodyBoolean(own,1,&ownTool,(M.PK_BODY_boolean_o_s*)&options,&mn,&mb);
                Console.WriteLine($"BOOLEAN own h={height:R} op={function} error={me} n={mn} seconds={Stopwatch.GetElapsedTime(started).TotalSeconds:F6} stage={KernelRuntime.LastRegularBooleanStage} faces={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace}");
                var pe=PK_BODY_boolean(native,1,&nativeTool,&options,&pn,&pb);Console.WriteLine($"BOOLEAN PK h={height:R} op={function} error={pe} n={pn}");
                Require(me==0&&pe==0,$"h={height:R} op={function} Boolean errors {me}/{pe}");Require(mn==pn,$"h={height:R} op={function} result count {mn}/{pn}");
                for(var i=0;i<mn;i++)
                {
                    var bytes=Transmit(mb[i]);var name=$"h{height:R}-op{function}-{i}";File.WriteAllBytes(Path.Combine(directory,name+"-self.x_t"),bytes);File.WriteAllBytes(Path.Combine(directory,name+"-pk.x_t"),TransmitNative(pb[i]));
                    received=Receive(bytes);try{CheckBody(pb[i],"result native");CheckBody(received,"result received");Compare(pb[i],received,name,height>0&&function==PK_boolean_intersect_c?Math.PI*height*height*.5:double.NaN);}finally{Check(PK_ENTITY_delete(1,&received),"delete result receive");}
                    var restored=ReceiveSelf(bytes);received=Receive(Transmit(restored));try{CheckBody(received,"reloaded result received");Compare(pb[i],received,name+" reloaded",height>0&&function==PK_boolean_intersect_c?Math.PI*height*height*.5:double.NaN);}finally{Check(PK_ENTITY_delete(1,&received),"delete reload receive");Check(KernelRuntime.EntityDelete(1,&restored),"delete restored self");}
                }
                Free(true,mb);Free(false,pb);
                Console.WriteLine($"PASS solid height={height:R} op={function}");
                // Each operation starts with fresh geometry and cache state.
                Check(KernelRuntime.SessionStop(),"reset self");Check(KernelRuntime.SessionStart(&start),"restart self");
            }
        }
    }
    finally{Check(KernelRuntime.SessionStop(),"self session stop");}
}

static unsafe void Ssi(double height)
{
    var own=BowlSurface(true);var native=BowlSurface(false);var planeDefinition=new PK_PLANE_sf_t{basis_set=new(){location=new(0,0,.5+height),axis=new(0,0,1),ref_direction=new(1,0,0)}};int ownPlane,nativePlane;
    Check(KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&planeDefinition,&ownPlane),"own SSI plane");Check(PK_PLANE_create(&planeDefinition,&nativePlane),"native SSI plane");
    var options=new PK_SURF_intersect_surf_o_t{tolerance=1e-5,mixed_curve_category=PK_mixed_intersection_pline_c};
    int mp=0,mc=0,pp=0,pc=0;M.PK_VECTOR_s* mv=null;PK_VECTOR_t* pv=null;int* mt=null;int* pt=null;M.PK_INTERVAL_s* mr=null;PK_INTERVAL_t* pr=null;int* mk=null;int* pk=null;
    var started=Stopwatch.GetTimestamp();var me=KernelRuntime.SurfIntersectSurf(own,ownPlane,(M.PK_SURF_intersect_surf_o_s*)&options,&mp,&mv,&mc,&mt,&mr,&mk);
    Console.WriteLine($"SSI own h={height:R} error={me} points={mp} curves={mc} seconds={Stopwatch.GetElapsedTime(started).TotalSeconds:F6}");
    var pe=PK_SURF_intersect_surf(native,nativePlane,&options,&pp,&pv,&pc,&pt,&pr,&pk);Console.WriteLine($"SSI PK h={height:R} error={pe} points={pp} curves={pc}");
    Require(me==0&&pe==0,$"SSI errors {me}/{pe}");Require(mp==pp&&mc==pc,$"SSI topology {mp}/{pp} points, {mc}/{pc} branches");
    Require(mp==(height==0?1:0)&&mc==(height>0?1:0),"manufactured SSI event topology");
    if(height==0)Require(Math.Abs(mv[0].coord[0])+Math.Abs(mv[0].coord[1])+Math.Abs(mv[0].coord[2]-.5)<1e-12,"manufactured isolated contact");
    if(me==0)for(var i=0;i<mc;i++)
    {
        var range=mr[i];var previous=0.0;var angle=0.0;var residual=0.0;var lastPoint=new M.PK_VECTOR_s();var firstPoint=new M.PK_VECTOR_s();
        for(var s=0;s<=128;s++)
        {
            var p=new M.PK_VECTOR_s();var parameter=range.value[0]+(range.value[1]-range.value[0])*s/128;var error=KernelRuntime.CurveEval(mt[i],parameter,0,&p);
            if(error!=0){var report=KernelRuntime.LastICurveEvalReport;Console.WriteLine($"SSI EVAL FAILURE sample={s} t={parameter:R} range={range.value[0]:R}/{range.value[1]:R} status={report.Status} detail={report.Detail} iterations={report.NewtonIterations} residual={report.Residual:R} segment={report.Segment} plan={report.Plan}");}Check(error,"SSI complete branch evaluation");
            var x=p.coord[0];var y=p.coord[1];residual=Math.Max(residual,Math.Max(Math.Abs(x*x+y*y-height),Math.Abs(p.coord[2]-(.5+height))));var a=Math.Atan2(y,x);
            if(s==0)firstPoint=p;else{var d=a-previous;while(d>Math.PI)d-=Math.Tau;while(d< -Math.PI)d+=Math.Tau;angle+=d;}previous=a;lastPoint=p;
        }
        var gap=Math.Sqrt(Math.Pow(lastPoint.coord[0]-firstPoint.coord[0],2)+Math.Pow(lastPoint.coord[1]-firstPoint.coord[1],2)+Math.Pow(lastPoint.coord[2]-firstPoint.coord[2],2));
        Console.WriteLine($"SSI branch={i} radius2 residual={residual:R} angle={angle:R} endpoint-gap={gap:R}");
        Require(residual<1e-11&&Math.Abs(Math.Abs(angle)-Math.Tau)<1e-8&&gap<1e-10,"complete closed manufactured branch");
    }
    if(me==0)for(var i=0;i<mc;i++){var tag=mt[i];Check(KernelRuntime.EntityDelete(1,&tag),"delete own SSI curve");}
    Free(true,mv);Free(true,mt);Free(true,mr);Free(true,mk);Free(false,pv);Free(false,pt);Free(false,pr);Free(false,pk);
    Check(KernelRuntime.EntityDelete(1,&own),"delete own SSI support");Check(KernelRuntime.EntityDelete(1,&ownPlane),"delete own plane");
}

static unsafe int BowlSurface(bool own)
{
    double* poles=stackalloc double[27];double* a=stackalloc double[3]{.25,-.25,.25};
    for(var i=0;i<3;i++)for(var j=0;j<3;j++){var at=3*(3*i+j);poles[at]=i*.5-.5;poles[at+1]=j*.5-.5;poles[at+2]=.5+a[i]+a[j];}
    return Surface(own,poles,3,3);
}
static unsafe int Surface(bool own,double* poles,int nu,int nv)
{
    double* knots=stackalloc double[2]{0,1};int* um=stackalloc int[2]{nu,nu};int* vm=stackalloc int[2]{nv,nv};
    var definition=new PK_BSURF_sf_t{u_degree=nu-1,v_degree=nv-1,n_u_vertices=nu,n_v_vertices=nv,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,
        u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};int surface;
    Check(own?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&definition,&surface):PK_BSURF_create(&definition,&surface),"NURBS support");return surface;
}
static unsafe int BowlSolid(bool own)
{
    int* sheets=stackalloc int[6];double* poles=stackalloc double[27];double* a=stackalloc double[3]{.25,-.25,.25};var box=new PK_UVBOX_t();box.param[2]=box.param[3]=1;
    for(var face=0;face<6;face++)
    {
        var nu=face<2?(face==0?3:2):3;var nv=face==0?3:2;
        for(var i=0;i<nu;i++)for(var j=0;j<nv;j++)
        {
            var at=3*(i*nv+j);var u=(double)i/(nu-1);var v=(double)j/(nv-1);
            if(face==0){poles[at]=u-.5;poles[at+1]=.5-v;poles[at+2]=.5+a[i]+a[j];}
            else if(face==1){poles[at]=u-.5;poles[at+1]=v-.5;poles[at+2]=2;}
            else
            {
                poles[at]=face switch{2=>u-.5,3=>.5-u,4=>-.5,_=>.5};poles[at+1]=face switch{2=>-.5,3=>.5,4=>.5-u,_=>u-.5};
                poles[at+2]=(1-v)*(.75+a[i])+2*v;
            }
        }
        var surface=Surface(own,poles,nu,nv);Check(own?KernelRuntime.SurfMakeSheetBody(surface,*(M.PK_UVBOX_s*)&box,sheets+face):PK_SURF_make_sheet_body(surface,box,sheets+face),"bounded source sheet");
    }
    var options=new PK_BODY_sew_bodies_o_t{prefered_body_type=PK_BODY_sewing_solid_c,treat_as_manifold=PK_LOGICAL_true,number_of_iterations=1};int n,nRemaining,nProblems;int* bodies;int* remaining;PK_BODY_problem_group_t* problems;
    Check(own?KernelRuntime.BodySewBodies(6,sheets,1e-9,(M.PK_BODY_sew_bodies_o_s*)&options,&n,&bodies,&nRemaining,&remaining,&nProblems,(M.PK_BODY_problem_group_s**)&problems):PK_BODY_sew_bodies(6,sheets,1e-9,&options,&n,&bodies,&nRemaining,&remaining,&nProblems,&problems),"sew bowl solid");
    try{Require(n==1&&nRemaining==0&&nProblems==0,"one closed bowl solid");return bodies[0];}finally{Free(own,bodies);Free(own,remaining);Free(own,problems);}
}
static unsafe int Block(bool own,double x,double y,double z,double width,double depth,double height)
{
    var basis=new PK_AXIS2_sf_t{location=new(x,y,z),axis=new(0,0,1),ref_direction=new(1,0,0)};int body;
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
static unsafe int ReceiveSelf(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new M.PK_MEMORY_block_s{bytes=p,n_bytes=(nuint)bytes.Length};var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(KernelRuntime.PartReceiveB(block,(M.PK_PART_receive_o_s*)&options,&n,&parts),"self original receive");try{Require(n==1,"one original solid");return parts[0];}finally{Free(true,parts);}
    }
}
static unsafe byte[] TransmitNative(int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1,&body,&options,&block),"native original transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(PK_MEMORY_block_f(&block),"native original transmit buffers");}
}

static unsafe void CheckBody(int body,string name)
{
    int type;Check(PK_BODY_ask_type(body,&type),"body type");Require(type==PK_BODY_type_solid_c,name+" solid type");
    var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&n,&faults),"full BODY_check");
    try
    {
        for(var i=0;i<n;i++)
        {
            int faultClass=0;var classError=PK_ENTITY_ask_class(faults[i].entity_1,&faultClass);
            Console.WriteLine($"{name} fault={faults[i].state}, entity={faults[i].entity_1}/{faults[i].entity_2}, class={faultClass}, classError={classError}");
        }
        Require(n==0,name+" body faults="+n);Console.WriteLine($"{name} full BODY_check: 0 faults");
    }
    finally{if(faults!=null)PK_MEMORY_free(faults);}
}

static unsafe void Compare(int reference,int actual,string name,double expectedVolume=double.NaN)
{
    for(var kind=0;kind<5;kind++){var a=Count(reference,kind);var b=Count(actual,kind);Console.WriteLine($"{name} topology {kind}: native={a} own={b}");Require(a==b,name+" topology count "+kind);}
    var nativeRegions=RegionSignature(reference);var ownRegions=RegionSignature(actual);Console.WriteLine($"{name} material regions: native={nativeRegions}, own={ownRegions}");
    Require(nativeRegions==ownRegions,name+" material and shell semantics");
    var options=new PK_DEBUG_BODY_compare_o_t{max_diffs=1024,all_tests=PK_LOGICAL_false,num_tests=PK_LOGICAL_true,tol_dev_tests=PK_LOGICAL_true,tol_tests=PK_LOGICAL_true,
        class_tests=PK_LOGICAL_true,sense_tests=PK_LOGICAL_true,missing_tests=PK_LOGICAL_true,acc_dev_tests=PK_LOGICAL_false,non_match_tests=PK_LOGICAL_true};PK_DEBUG_BODY_compare_r_t result;
    Check(PK_DEBUG_BODY_compare(reference,actual,&options,&result),"structural compare");var structuralOk=false;
    try
    {
        var structural=0;Console.WriteLine($"{name} compare={result.global_result}/{result.local_result}, face_pairs={result.n_face_pairs}");
        for(var i=0;i<result.n_global_diffs;i++)Console.WriteLine($"global diff={result.global_diffs[i].diff}, entities={result.global_diffs[i].n_masters}/{result.global_diffs[i].n_similars}");
        for(var f=0;f<result.n_face_pairs;f++)for(var i=0;i<result.face_pairs[f].n_local_diffs;i++)
        {
            var d=result.face_pairs[f].local_diffs[i];var chart=d.diff==PK_DEBUG_diff_n_cht_pts_c;
            Console.WriteLine($"{(chart?"CHART":"STRUCTURE")} face={f}, diff={d.diff}, entity={d.master_entity}/{d.similar_entity}, class={d.master_class}/{d.similar_class}, int={d.master_int}/{d.similar_int}, value={d.master_double:R}/{d.similar_double:R}");if(!chart)structural++;
        }
        structuralOk=result.global_result==PK_DEBUG_global_res_no_diffs_c&&structural==0;
    }
    finally{PK_DEBUG_BODY_compare_r_f(&result);}
    var massOptions=new PK_TOPOL_eval_mass_props_o_t();double* aMass=stackalloc double[15];double* bMass=stackalloc double[15];
    Check(PK_TOPOL_eval_mass_props(1,&reference,.99999999,&massOptions,aMass,aMass+1,aMass+2,aMass+5,aMass+14),"native mass properties");
    Check(PK_TOPOL_eval_mass_props(1,&actual,.99999999,&massOptions,bMass,bMass+1,bMass+2,bMass+5,bMass+14),"received mass properties");
    var maximum=0.0;
    for(var i=0;i<15;i++)maximum=Math.Max(maximum,Math.Abs(aMass[i]-bMass[i])/(1+Math.Abs(aMass[i])));
    Console.WriteLine($"{name} PK mass: volume={aMass[0]:R}/{bMass[0]:R}, area={aMass[14]:R}/{bMass[14]:R}, max normalized residual={maximum:R}");
    Require(maximum<1e-7,name+" mass properties");Require(structuralOk,name+" structural differences");
    if(double.IsFinite(expectedVolume))
    {
        var nativeError=Math.Abs(aMass[0]/expectedVolume-1);var ownError=Math.Abs(bMass[0]/expectedVolume-1);
        Console.WriteLine($"{name} analytic cap volume={expectedVolume:R}, relative residual native={nativeError:R}, own={ownError:R}");
        Require(expectedVolume>0&&nativeError<1e-5&&ownError<1e-5,name+" nonzero analytical cap volume");
    }
}

static unsafe int Count(int body,int kind)
{
    int n;int* tags;Check(kind switch{0=>PK_BODY_ask_regions(body,&n,&tags),1=>PK_BODY_ask_shells(body,&n,&tags),2=>PK_BODY_ask_faces(body,&n,&tags),3=>PK_BODY_ask_edges(body,&n,&tags),_=>PK_BODY_ask_vertices(body,&n,&tags)},"count");
    try{return n;}finally{if(tags!=null)PK_MEMORY_free(tags);}
}
static unsafe string RegionSignature(int body)
{
    int n;int* regions;Check(PK_BODY_ask_regions(body,&n,&regions),"region semantics");
    try
    {
        var records=new string[n];
        for(var i=0;i<n;i++)
        {
            PK_LOGICAL_t solid;int shells;int* tags;Check(PK_REGION_is_solid(regions[i],&solid),"material region flag");
            Check(PK_REGION_ask_shells(regions[i],&shells,&tags),"region shell membership");
            try{records[i]=$"{(solid==PK_LOGICAL_true?1:0)}:{shells}";}finally{if(tags!=null)PK_MEMORY_free(tags);}
        }
        Array.Sort(records,StringComparer.Ordinal);return string.Join(",",records);
    }
    finally{PK_MEMORY_free(regions);}
}
static unsafe void Free(bool own,void* memory){if(memory!=null)Check(own?KernelRuntime.MemoryFree(memory):PK_MEMORY_free(memory),"return buffer");}
static void Check(int error,string name){if(error!=0)throw new Exception($"{name}: error={error}, support-chain={KernelRuntime.LastSupportChainStage}, blend={KernelRuntime.LastRegularBlendStage}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace}, sheet={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, parameter={KernelRuntime.LastSheetParameter:R}, algorithm={KernelRuntime.LastSheetAlgorithmStatus}");}
static void Require(bool value,string message){if(!value)throw new Exception(message);}
static string ScriptPath([CallerFilePath]string path="")=>path;

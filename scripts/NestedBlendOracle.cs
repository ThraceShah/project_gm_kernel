#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=IcurveEvalBenchmark
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
global using BenchmarkSurfaceIndex=System.Int32;
using static parasolid;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Xt;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;
using M=ProjectGmKernel.Native.Generated;

if(!ParasolidScriptHost.TryStartSession("nested blend",out var session,out var message))throw new Exception(message);
using(session)
unsafe
{
    var nurbs=ProbeClosedNurbs();
    var nurbsFirst=nurbs;var nurbsSecond=0;
    for(var depth=2;depth<=3;depth++){nurbs=Next(nurbs,.05/Math.Pow(3,depth-1));if(nurbs==0)throw new Exception("NURBS nested fixture depth="+depth);if(depth==2)nurbsSecond=nurbs;}
    var tsf=new PK_TORUS_sf_t{major_radius=2,minor_radius=.5};tsf.basis_set.axis=new(0,0,1);tsf.basis_set.ref_direction=new(1,0,0);int torus=0;Check(PK_TORUS_create(&tsf,&torus),"torus");
    var plane=Plane(new(0,0,0),new(0,.6,.8),new(1,0,0));
    var blend=Blend(torus,plane,.1,new(2.5,0,0),new(2,.5,-.375));
    if(blend==0)return 1;
    var box=new PK_UVBOX_t();Check(PK_SURF_ask_uvbox(blend,&box),"uvbox");
    Console.WriteLine($"box={box.param[0]:R},{box.param[1]:R}..{box.param[2]:R},{box.param[3]:R}");
    var bs=new PK_BLENDSF_sf_t();Check(PK_BLENDSF_ask(blend,&bs),"ask blend");
    Console.WriteLine($"support={bs.geom_1}/{bs.geom_2}, radius={bs.radii[0]:R}/{bs.radii[1]:R}, spine={bs.spine} extent={bs.spine_extent.value[0]:R}..{bs.spine_extent.value[1]:R}");
    for(var i=0;i<3;i++)
    {
        PK_VECTOR_t* jet=stackalloc PK_VECTOR_t[3];var uv=new PK_UV_t(box.param[0]+(box.param[2]-box.param[0])*(i+1)/4,.5);
        Check(PK_SURF_eval(blend,uv,1,1,PK_LOGICAL_true,jet),"sample");Console.WriteLine($"sample {uv.param[0]:R}/{uv.param[1]:R}: {jet[0].coord[0]:R},{jet[0].coord[1]:R},{jet[0].coord[2]:R}");
        PK_VECTOR_t normal,center;Check(PK_SURF_eval_with_normal(blend,uv,1,1,PK_LOGICAL_true,jet,&normal),"native normal");Check(PK_CURVE_eval(bs.spine,uv.param[0],0,&center),"native center");
        var outward=0.0;var handed=0.0;for(var d=0;d<3;d++){outward+=normal.coord[d]*(jet[0].coord[d]-center.coord[d]);handed+=normal.coord[d]*(jet[1].coord[(d+1)%3]*jet[2].coord[(d+2)%3]-jet[1].coord[(d+2)%3]*jet[2].coord[(d+1)%3]);}
        Console.WriteLine($"native normal dot radial={outward:R}, dot cross(du,dv)={handed:R}");
    }
    var firstBlend=blend;var secondBlend=0;
    for(var depth=2;depth<=3;depth++){blend=Next(blend,.1/Math.Pow(3,depth-1));if(blend==0)return 2;if(depth==2)secondBlend=blend;}
    Check(PK_SURF_ask_uvbox(blend,&box),"depth3 box");Console.WriteLine($"depth3 box={box.param[0]:R},{box.param[1]:R}..{box.param[2]:R},{box.param[3]:R}");
    var patch=box;patch.param[0]+=(box.param[2]-box.param[0])*.35;patch.param[2]-=(box.param[2]-box.param[0])*.35;patch.param[1]=.1;patch.param[3]=.9;
    int body;Check(PK_SURF_make_sheet_body(blend,patch,&body),"nested sheet");
    var output=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","nested-blend-native"));Directory.CreateDirectory(output);
    var tx=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;Check(PK_PART_transmit_b(1,&body,&tx,&block),"native transmit");
    try{using var stream=File.Create(Path.Combine(output,"depth3.x_t"));for(var part=&block;part!=null;part=part->next)stream.Write(new ReadOnlySpan<byte>(part->bytes,checked((int)part->n_bytes)));}finally{Check(PK_MEMORY_block_f(&block),"native transmit free");}
    for(var order=0;order<=2;order++)
    {
        PK_VECTOR_t* values=stackalloc PK_VECTOR_t[9];var watch=Stopwatch.StartNew();double checksum=0;
        for(var i=0;i<10000;i++){var uv=new PK_UV_t(patch.param[0]+(patch.param[2]-patch.param[0])*(i%251)/250,.1+.8*(i%239)/238);Check(PK_SURF_eval(blend,uv,order,order,PK_LOGICAL_false,values),"native timed eval");checksum+=values[0].coord[0];}
        Console.WriteLine($"depth3 D{order}: {watch.Elapsed.TotalNanoseconds/10000:F1} ns checksum={checksum:R}");
    }
    var document=XtText.DecodeDocument(File.ReadAllText(Path.Combine(output,"depth3.x_t")));
    var compiled=Compile(document);
    var program=new NestedBlendProgram(compiled.Surfaces,compiled.Spines,compiled.Charts);
    var ownOptions=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&ownOptions),"self start");
    CheckNativeImport(nurbs);
    var selfSurface=Materialize(compiled);Console.WriteLine("self runtime root="+selfSurface);
    IndependentConstruction(firstBlend,secondBlend,blend);
    IndependentConstruction(nurbsFirst,nurbsSecond,nurbs,true);
    var nativeBytes=File.ReadAllBytes(Path.Combine(output,"depth3.x_t"));
    fixed(byte* bytes=nativeBytes)
    {
        var input=new M.PK_MEMORY_block_s{n_bytes=(nuint)nativeBytes.Length,bytes=bytes};var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int length=0;int* parts=null;
        var received=KernelRuntime.PartReceiveB(input,(M.PK_PART_receive_o_s*)&options,&length,&parts);Console.WriteLine($"production native-XT receive={received}, parts={length}");Check(received,"self receive native fixture");
        try
        {
            if(length!=1||!KernelRuntime.TryResolveBodySlot(parts[0],out var slot))throw new Exception("self receive body");
            var face=KernelRuntime.Bodies[slot].FirstFaceBody;if(face<0)throw new Exception("native graph remained opaque");var tag=KernelRuntime.Faces[face].SurfTag;
            var uv=new M.PK_UV_s();uv.param[0]=(patch.param[0]+patch.param[2])*.5;uv.param[1]=.37;M.PK_VECTOR_s* point=stackalloc M.PK_VECTOR_s[4];Check(KernelRuntime.SurfEval(tag,uv,1,1,0,point),"production imported Blend eval");
            Console.WriteLine($"production imported Blend point={point[0].coord[0]:R},{point[0].coord[1]:R},{point[0].coord[2]:R}");
        }
        finally{if(parts!=null)Check(KernelRuntime.MemoryFree(parts),"self receive parts free");}
    }
    var selfBox=new M.PK_UVBOX_s();for(var k=0;k<4;k++)selfBox.param[k]=patch.param[k];int selfBody=0;
    Check(KernelRuntime.SurfMakeSheetBody(selfSurface,selfBox,&selfBody),"self sheet");
    var selfTransmit=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=PK_transmit_format_text_c,transmit_version=371,transmit_meshes=PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s selfBlock;
    Check(KernelRuntime.PartTransmitB(1,&selfBody,&selfTransmit,&selfBlock),"self transmit");byte[] selfBytes;
    try{using var stream=new MemoryStream();for(var p=&selfBlock;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));selfBytes=stream.ToArray();File.WriteAllBytes(Path.Combine(output,"self-depth3.x_t"),selfBytes);}finally{Check(KernelRuntime.MemoryBlockFree(&selfBlock),"self block free");}
    fixed(byte* bytes=selfBytes)
    {
        var receive=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};var memory=new PK_MEMORY_block_t(null,(ulong)selfBytes.Length,bytes);int np=0;int* parts=null;
        var received=PK_PART_receive_b(memory,&receive,&np,&parts);Console.WriteLine($"self XT native receive={received}, parts={np}");Check(received,"receive self");
        if(np==1)
        {
            var check=new PK_BODY_check_o_t{max_faults=100};int nf=0;PK_check_fault_t* faults=null;Check(PK_BODY_check(parts[0],&check,&nf,&faults),"received BODY_check");Console.WriteLine("received BODY_check faults="+nf);
            for(var i=0;i<nf;i++)Console.WriteLine($"  fault={faults[i].state} entity={faults[i].entity_1}/{faults[i].entity_2}");
            if(faults!=null)Check(PK_MEMORY_free(faults),"check free");
            if(nf!=0)throw new Exception("received BODY_check faults="+nf);
            var compare=new PK_DEBUG_BODY_compare_o_t{max_diffs=64,all_tests=PK_LOGICAL_false,num_tests=PK_LOGICAL_true,tol_dev_tests=PK_LOGICAL_true,tol_tests=PK_LOGICAL_true,class_tests=PK_LOGICAL_true,sense_tests=PK_LOGICAL_true,missing_tests=PK_LOGICAL_true,acc_dev_tests=PK_LOGICAL_false,non_match_tests=PK_LOGICAL_false};PK_DEBUG_BODY_compare_r_t compared;
            Check(PK_DEBUG_BODY_compare(body,parts[0],&compare,&compared),"independent body compare");
            try
            {
                Console.WriteLine($"compare global={compared.global_result} local={compared.local_result} pairs={compared.n_face_pairs}");
                for(var i=0;i<compared.n_global_diffs;i++)Console.WriteLine($"  global diff={compared.global_diffs[i].diff} counts={compared.global_diffs[i].n_masters}/{compared.global_diffs[i].n_similars}");
                for(var f=0;f<compared.n_face_pairs;f++)for(var d=0;d<compared.face_pairs[f].n_local_diffs;d++){var diff=compared.face_pairs[f].local_diffs[d];Console.WriteLine($"  face={f} diff={diff.diff} entities={diff.master_entity}/{diff.similar_entity} values={diff.master_double:R}/{diff.similar_double:R}");}
                if(compared.global_result!=PK_DEBUG_global_res_no_diffs_c||compared.local_result!=PK_DEBUG_local_res_no_diffs_c)throw new Exception("structural native body comparison failed");
            }
            finally{Check(PK_DEBUG_BODY_compare_r_f(&compared),"compare free");}
        }
        if(parts!=null)Check(PK_MEMORY_free(parts),"parts free");
    }
    SurfaceDerivativeLayout.TryCreate(2,2,out var layout);Span<KernelVector3> actual=stackalloc KernelVector3[9];PK_VECTOR_t* expected=stackalloc PK_VECTOR_t[9];
    for(var i=0;i<5;i++)
    {
        var uv=new PK_UV_t(patch.param[0]+(patch.param[2]-patch.param[0])*(i+1)/6,.37);
        var status=JointNestedBlendEvaluation.Evaluate(program,compiled.Root,uv.param[0],uv.param[1],layout,actual);
        Console.WriteLine($"self graph {uv.param[0]:R}: {status}");if(status!=AlgorithmStatus.Success)continue;
        Check(PK_SURF_eval(blend,uv,2,2,PK_LOGICAL_false,expected),"comparison");
        for(var u=0;u<=2;u++)for(var v=0;v<=2;v++)
        {var p=actual[layout.GetIndex(u,v)];Console.WriteLine($"  jet {u}/{v}: delta={p.X-expected[v*3+u].coord[0]:R},{p.Y-expected[v*3+u].coord[1]:R},{p.Z-expected[v*3+u].coord[2]:R}");}
        if(i==2)
        {
            foreach(var h in new[]{1e-3,1e-4,1e-5,1e-6})
            {
                PK_VECTOR_t* left=stackalloc PK_VECTOR_t[3];PK_VECTOR_t* right=stackalloc PK_VECTOR_t[3];
                Check(PK_SURF_eval(blend,new(uv.param[0]-h,uv.param[1]),1,1,PK_LOGICAL_true,left),"left FD");Check(PK_SURF_eval(blend,new(uv.param[0]+h,uv.param[1]),1,1,PK_LOGICAL_true,right),"right FD");
                var self=actual[layout.GetIndex(2,0)];var error=0.0;var nativeError=0.0;
                for(var d=0;d<3;d++){var fd=(right[1].coord[d]-left[1].coord[d])/(2*h);var a=d==0?self.X:d==1?self.Y:self.Z;error=Math.Max(error,Math.Abs(a-fd));nativeError=Math.Max(nativeError,Math.Abs(expected[2].coord[d]-fd));}
                Console.WriteLine($"FD u2 h={h:R} self-error={error:R} PK-error={nativeError:R}");
            }
        }
    }
    for(var order=0;order<=2;order++)
    {
        SurfaceDerivativeLayout.TryCreate(order,order,out var requested);var watch=Stopwatch.StartNew();double sum=0;
        for(var i=0;i<32;i++){var u=patch.param[0]+(patch.param[2]-patch.param[0])*(i+.5)/32;var status=JointNestedBlendEvaluation.Evaluate(program,compiled.Root,u,.37,requested,actual);if(status!=AlgorithmStatus.Success)throw new Exception("timed self="+status);sum+=actual[0].X;}
        Console.WriteLine($"self graph D{order}: {watch.Elapsed.TotalNanoseconds/32:F1} ns checksum={sum:R}");
        M.PK_VECTOR_s* runtime=stackalloc M.PK_VECTOR_s[9];watch.Restart();sum=0;
        for(var i=0;i<256;i++){var uv=new M.PK_UV_s();uv.param[0]=patch.param[0]+(patch.param[2]-patch.param[0])*(i+.5)/256;uv.param[1]=.37;Check(KernelRuntime.SurfEval(selfSurface,uv,order,order,0,runtime),"public self eval");sum+=runtime[0].coord[0];}
        Console.WriteLine($"self public D{order}: {watch.Elapsed.TotalNanoseconds/256:F1} ns checksum={sum:R}");
    }
    Check(KernelRuntime.SessionStop(),"self stop");
}
Console.WriteLine("PASS: independent three-level Blend construction, evaluation, inversion, XT and similarity oracles.");
return 0;
static unsafe int Plane(PK_VECTOR_t location,PK_VECTOR_t axis,PK_VECTOR_t reference)
{var sf=new PK_PLANE_sf_t();sf.basis_set.location=location;for(var d=0;d<3;d++){sf.basis_set.axis.coord[d]=axis.coord[d];sf.basis_set.ref_direction.coord[d]=reference.coord[d];}int tag=0;Check(PK_PLANE_create(&sf,&tag),"plane");return tag;}
static unsafe int Blend(int a,int b,double radius,PK_VECTOR_t start,PK_VECTOR_t end)
{var o=new PK_SURF_create_blend_o_t();int result=0;var error=PK_SURF_create_blend(a,radius,b,radius,start,end,&o,&result);Console.WriteLine($"create {a}/{b}, r={radius:R}: error={error} result={result}");if(error!=0)return 0;int cls;Check(PK_ENTITY_ask_class(result,&cls),"class");Console.WriteLine("class="+cls);return result;}
static unsafe int Next(int source,double radius)
{
    PK_UVBOX_t box;Check(PK_SURF_ask_uvbox(source,&box),"next box");
    var u=box.param[0]+(box.param[2]-box.param[0])*.5;PK_VECTOR_t middle,start,end;
    Check(PK_SURF_eval(source,new(u,.5),0,0,PK_LOGICAL_false,&middle),"middle");
    Check(PK_SURF_eval(source,new(u-.1,.5),0,0,PK_LOGICAL_false,&start),"start");
    Check(PK_SURF_eval(source,new(u+.1,.5),0,0,PK_LOGICAL_false,&end),"end");
    for(var axis=0;axis<3;axis++)for(var sign=0;sign<2;sign++)for(var reverse=0;reverse<2;reverse++)
    {
        var normal=new PK_VECTOR_t();normal.coord[axis]=1;var reference=new PK_VECTOR_t();reference.coord[(axis+1)%3]=1;
        var wall=Plane(middle,normal,reference);var found=Blend(source,wall,sign==0?radius:-radius,reverse==0?start:end,reverse==0?end:start);
        if(found==0)continue;int cls;Check(PK_ENTITY_ask_class(found,&cls),"nested class");
        if(cls==PK_CLASS_blendsf){Console.WriteLine($"NESTED source={source} result={found} axis={axis} sign={sign} reverse={reverse}");return found;}
    }
    return 0;
}
static void Check(int error,string name){if(error!=0)throw new Exception(name+" error="+error);}
static string ScriptPath([CallerFilePath]string path="")=>path;

static unsafe int ProbeClosedNurbs()
{
    double* poles=stackalloc double[27];double* squared=stackalloc double[3]{1,-1,1};
    for(var u=0;u<3;u++)for(var v=0;v<3;v++){var i=3*(3*u+v);var x=u-1.0;var y=v-1.0;poles[i]=x;poles[i+1]=y;poles[i+2]=.6*squared[u]+1.1*squared[v]+.15*x*y;}
    double* knots=stackalloc double[2]{-1,1};int* mult=stackalloc int[2]{3,3};
    var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=2,n_u_vertices=3,n_v_vertices=3,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,
        form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
    int surface=0;Check(PK_BSURF_create(&sf,&surface),"closed NURBS surface");var plane=Plane(new(0,0,.4),new(0,0,1),new(1,0,0));
    var result=Blend(surface,plane,.05,new(.8,0,.4),new(.6,.4,.4));Console.WriteLine("closed NURBS native result="+result);if(result==0)throw new Exception("NURBS fixture");return result;
}

static unsafe void CheckNativeImport(int native)
{
    PK_UVBOX_t box;Check(PK_SURF_ask_uvbox(native,&box),"NURBS native box");var patch=box;var width=box.param[2]-box.param[0];patch.param[0]=box.param[0]+.1*width;patch.param[2]=box.param[0]+.3*width;patch.param[1]=.1;patch.param[3]=.9;
    int body;Check(PK_SURF_make_sheet_body(native,patch,&body),"NURBS sheet");
    var tx=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;Check(PK_PART_transmit_b(1,&body,&tx,&block),"NURBS transmit");byte[] bytes;
    try{using var stream=new MemoryStream();for(var part=&block;part!=null;part=part->next)stream.Write(new ReadOnlySpan<byte>(part->bytes,checked((int)part->n_bytes)));bytes=stream.ToArray();}finally{Check(PK_MEMORY_block_f(&block),"NURBS native free");}
    File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","nurbs-blend.x_t"),bytes);
    fixed(byte* p=bytes)
    {
        var memory=new M.PK_MEMORY_block_s{n_bytes=(nuint)bytes.Length,bytes=p};var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=PK_transmit_format_text_c};int count;int* parts;
        Check(KernelRuntime.PartReceiveB(memory,&options,&count,&parts),"NURBS self receive");
        try
        {
            if(count!=1||!KernelRuntime.TryResolveBodySlot(parts[0],out var slot)||KernelRuntime.Bodies[slot].FirstFaceBody<0)throw new Exception("NURBS opaque");
            var tag=KernelRuntime.Faces[KernelRuntime.Bodies[slot].FirstFaceBody].SurfTag;
            for(var i=1;i<16;i++)
            {
                var u=patch.param[0]+(patch.param[2]-patch.param[0])*i/16;var v=.1+.8*(i%7)/6;
                var uv=new M.PK_UV_s();uv.param[0]=u;uv.param[1]=v;M.PK_VECTOR_s* actual=stackalloc M.PK_VECTOR_s[9];PK_VECTOR_t* expected=stackalloc PK_VECTOR_t[9];
                var evaluated=KernelRuntime.SurfEval(tag,uv,1,1,1,actual);
                if(evaluated!=0)
                {
                    var data=KernelRuntime.BlendedEdgeDataPool[KernelRuntime.GetSurfaceByTag(tag).DataIndex];var stored=KernelRuntime.GetNestedBlendProgram(data);
                    var program=new NestedBlendProgram(stored.Surfaces.ToArray(),stored.Spines.ToArray(),stored.Charts.ToArray(),stored.Scalars.ToArray(),new double[1000000]);
                    SurfaceDerivativeLayout.TryCreate(2,2,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[9];Console.WriteLine($"NURBS status sample={i} u={u:R}: {JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,u,v,layout,jet)}");
                }
                Check(evaluated,"NURBS self eval");Check(PK_SURF_eval(native,new(u,v),1,1,PK_LOGICAL_true,expected),"NURBS PK eval");
                var error=0.0;for(var j=0;j<3;j++)for(var d=0;d<3;d++)error=Math.Max(error,Math.Abs(actual[j].coord[d]-expected[j].coord[d]));
                if(error>1e-8)throw new Exception("NURBS D0/D1 error="+error);
                var inverted=new M.PK_UV_s();Check(KernelRuntime.SurfParameteriseVector(tag,actual[0],&inverted),"NURBS own inverse");
                if(Math.Abs(inverted.param[0]-u)>1e-8||Math.Abs(inverted.param[1]-v)>1e-8)throw new Exception("NURBS inverse mismatch");
            }
            Console.WriteLine("NURBS production receive + D0/D1 passed");
            RoundTrip(parts[0],body,"nurbs-depth3");
        }
        finally{Check(KernelRuntime.MemoryFree(parts),"NURBS own free");}
    }
}

static unsafe void IndependentConstruction(int nativeBlend,int nativeSecond,int nativeThird,bool nurbs=false)
{
    int baseSurface=0,plane=0;var psf=new M.PK_PLANE_sf_s();psf.basis_set.ref_direction.coord[0]=1;
    var options=new M.PK_SURF_create_blend_o_s{o_t_version=1};var start=new M.PK_VECTOR_s();var end=new M.PK_VECTOR_s();var baseRadius=nurbs?.05:.1;
    if(nurbs)
    {
        double* poles=stackalloc double[27];double* squared=stackalloc double[3]{1,-1,1};
        for(var u=0;u<3;u++)for(var v=0;v<3;v++){var i=3*(3*u+v);var x=u-1.0;var y=v-1.0;poles[i]=x;poles[i+1]=y;poles[i+2]=.6*squared[u]+1.1*squared[v]+.15*x*y;}
        double* knots=stackalloc double[2]{-1,1};int* mult=stackalloc int[2]{3,3};
        var sf=new M.PK_BSURF_sf_s{u_degree=2,v_degree=2,n_u_vertices=3,n_v_vertices=3,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,
            form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
        Check(KernelRuntime.BSurfCreate(&sf,&baseSurface),"independent NURBS surface");psf.basis_set.location.coord[2]=.4;psf.basis_set.axis.coord[2]=1;
        start.coord[0]=.8;start.coord[2]=.4;end.coord[0]=.6;end.coord[1]=.4;end.coord[2]=.4;
    }
    else
    {
        var tsf=new M.PK_TORUS_sf_s{major_radius=2,minor_radius=.5};tsf.basis_set.axis.coord[2]=1;tsf.basis_set.ref_direction.coord[0]=1;
        Check(KernelRuntime.TorusCreate(&tsf,&baseSurface),"independent self torus");psf.basis_set.axis.coord[1]=.6;psf.basis_set.axis.coord[2]=.8;
        start.coord[0]=2.5;end.coord[0]=2;end.coord[1]=.5;end.coord[2]=-.375;
    }
    Check(KernelRuntime.PlaneCreate(&psf,&plane),"independent self plane");int result=0;
    var watch=Stopwatch.StartNew();var error=KernelRuntime.SurfCreateBlend(baseSurface,baseRadius,plane,baseRadius,start,end,&options,&result);Console.WriteLine($"independent public construct nurbs={nurbs} error={error} result={result} ms={watch.Elapsed.TotalMilliseconds:F3}");Check(error,"independent self general Blend");
    var definition=new M.PK_BLENDSF_sf_s();Check(KernelRuntime.BlendSfAsk(result,&definition),"independent self definition");PK_BLENDSF_sf_t reference;Check(PK_BLENDSF_ask(nativeBlend,&reference),"independent native definition");
    for(var i=1;i<=12;i++)
    {
        var parameter=reference.spine_extent.value[0]+(reference.spine_extent.value[1]-reference.spine_extent.value[0])*i/13;PK_VECTOR_t center;Check(PK_CURVE_eval(reference.spine,parameter,0,&center),"independent center");
        double u=0;Check(KernelRuntime.CurveParameteriseVector(definition.spine,*(M.PK_VECTOR_s*)&center,&u),"independent own spine parameter");
        var uv=new M.PK_UV_s();uv.param[0]=u;uv.param[1]=.37;var actual=new M.PK_VECTOR_s();PK_VECTOR_t expected;
        Check(KernelRuntime.SurfEval(result,uv,0,0,0,&actual),"independent self point");Check(PK_SURF_eval(nativeBlend,new(parameter,.37),0,0,PK_LOGICAL_false,&expected),"independent native point");
        for(var d=0;d<3;d++)if(Math.Abs(actual.coord[d]-expected.coord[d])>1e-8)throw new Exception($"independent Blend point mismatch sample={i} component={d} delta={actual.coord[d]-expected.coord[d]:R}");
    }
    Console.WriteLine("independent public construction: 12 corresponding physical sections matched");
    var ownParent=result;var nativeParent=nativeBlend;
    for(var depth=2;depth<=3;depth++)
    {
        PK_BLENDSF_sf_t parent;Check(PK_BLENDSF_ask(nativeParent,&parent),"nested native parent definition");var t=(parent.spine_extent.value[0]+parent.spine_extent.value[1])*.5;
        PK_VECTOR_t middle,from,to;Check(PK_SURF_eval(nativeParent,new(t,.5),0,0,PK_LOGICAL_false,&middle),"nested middle");
        Check(PK_SURF_eval(nativeParent,new(t-.1,.5),0,0,PK_LOGICAL_false,&from),"nested from");Check(PK_SURF_eval(nativeParent,new(t+.1,.5),0,0,PK_LOGICAL_false,&to),"nested to");
        var wall=new M.PK_PLANE_sf_s();wall.basis_set.location=*(M.PK_VECTOR_s*)&middle;wall.basis_set.axis.coord[0]=1;wall.basis_set.ref_direction.coord[1]=1;int tool=0;Check(KernelRuntime.PlaneCreate(&wall,&tool),"nested self wall");
        var radius=baseRadius/Math.Pow(3,depth-1);int next=0;watch.Restart();
        error=KernelRuntime.SurfCreateBlend(ownParent,radius,tool,radius,depth==3&&!nurbs?*(M.PK_VECTOR_s*)&to:*(M.PK_VECTOR_s*)&from,depth==3&&!nurbs?*(M.PK_VECTOR_s*)&from:*(M.PK_VECTOR_s*)&to,&options,&next);
        Console.WriteLine($"independent public depth={depth} error={error} result={next} ms={watch.Elapsed.TotalMilliseconds:F3}");Check(error,"independent nested construction");
        ownParent=next;nativeParent=depth==2?nativeSecond:nativeThird;
        var own=new M.PK_BLENDSF_sf_s();Check(KernelRuntime.BlendSfAsk(ownParent,&own),"nested own definition");PK_BLENDSF_sf_t native;Check(PK_BLENDSF_ask(nativeParent,&native),"nested native definition");
        for(var i=1;i<=12;i++)
        {
            var fraction=nurbs&&depth==3?.1+.2*i/13.0:i/13.0;var parameter=native.spine_extent.value[0]+(native.spine_extent.value[1]-native.spine_extent.value[0])*fraction;PK_VECTOR_t center,expected;
            Check(PK_CURVE_eval(native.spine,parameter,0,&center),"nested native center");double ownParameter=0;
            Check(KernelRuntime.CurveParameteriseVector(own.spine,*(M.PK_VECTOR_s*)&center,&ownParameter),"nested own correspondence");
            var uv=new M.PK_UV_s();uv.param[0]=ownParameter;uv.param[1]=.37;var actual=new M.PK_VECTOR_s();
            Check(KernelRuntime.SurfEval(ownParent,uv,0,0,0,&actual),"nested own point");Check(PK_SURF_eval(nativeParent,new(parameter,.37),0,0,PK_LOGICAL_false,&expected),"nested reference point");
            for(var d=0;d<3;d++)if(Math.Abs(actual.coord[d]-expected.coord[d])>1e-8)throw new Exception($"independent nested point mismatch depth={depth} sample={i} component={d} delta={actual.coord[d]-expected.coord[d]:R}");
        }
        Console.WriteLine($"independent public depth={depth}: 12 physical sections matched");
    }
    var ownBox=new M.PK_UVBOX_s();var nativeBox=new PK_UVBOX_t();Check(PK_SURF_ask_uvbox(nativeParent,&nativeBox),"independent output native box");
    var uWidth=nativeBox.param[2]-nativeBox.param[0];var lower=nurbs?.1:.35;var upper=nurbs?.15:.65;
    nativeBox.param[2]=nativeBox.param[0]+upper*uWidth;nativeBox.param[0]+=lower*uWidth;nativeBox.param[1]=.1;nativeBox.param[3]=.9;
    PK_BLENDSF_sf_t referenceFinal;Check(PK_BLENDSF_ask(nativeParent,&referenceFinal),"independent final definition");var ownFinal=new M.PK_BLENDSF_sf_s();Check(KernelRuntime.BlendSfAsk(ownParent,&ownFinal),"own final definition");
    for(var side=0;side<2;side++)
    {
        PK_VECTOR_t center;Check(PK_CURVE_eval(referenceFinal.spine,nativeBox.param[side==0?0:2],0,&center),"output native boundary center");double parameter;
        Check(KernelRuntime.CurveParameteriseVector(ownFinal.spine,*(M.PK_VECTOR_s*)&center,&parameter),"output own boundary center");ownBox.param[side==0?0:2]=parameter;
    }
    if(ownBox.param[0]>ownBox.param[2])throw new Exception("independent output interval crosses chart seam");ownBox.param[1]=.1;ownBox.param[3]=.9;
    int ownBody,nativeBody;Check(KernelRuntime.SurfMakeSheetBody(ownParent,ownBox,&ownBody),"independently constructed own sheet");Check(PK_SURF_make_sheet_body(nativeParent,nativeBox,&nativeBody),"independently constructed native sheet");
    RoundTrip(ownBody,nativeBody,nurbs?"independent-nurbs-depth3":"independent-torus-depth3");
    var transformDefinition=new M.PK_TRANSF_sf_s();transformDefinition.matrix[0]=-2;transformDefinition.matrix[5]=transformDefinition.matrix[10]=2;
    transformDefinition.matrix[3]=3;transformDefinition.matrix[7]=5;transformDefinition.matrix[11]=7;transformDefinition.matrix[15]=1;
    int ownTransform,nativeTransform;Check(KernelRuntime.TransfCreate(&transformDefinition,&ownTransform),"own nested transform");Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&transformDefinition,&nativeTransform),"native nested transform");
    int changed;int* replacement;byte* exact;Check(KernelRuntime.BodyTransform(ownBody,ownTransform,0,&changed,&replacement,&exact),"own nested body transform");
    if(replacement!=null)Check(KernelRuntime.MemoryFree(replacement),"own transform replacement free");if(exact!=null)Check(KernelRuntime.MemoryFree(exact),"own transform exact free");
    int* nativeReplacement;PK_LOGICAL_t* nativeExact;Check(PK_BODY_transform(nativeBody,nativeTransform,0,&changed,&nativeReplacement,&nativeExact),"native nested body transform");
    if(nativeReplacement!=null)Check(PK_MEMORY_free(nativeReplacement),"native replacement free");if(nativeExact!=null)Check(PK_MEMORY_free(nativeExact),"native exact free");
    RoundTrip(ownBody,nativeBody,nurbs?"reflected-nurbs-depth3":"reflected-torus-depth3");
}

static unsafe void RoundTrip(int ownBody,int nativeBody,string name)
{
    var output=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","nested-blend-native");Directory.CreateDirectory(output);
    var nativeOptions=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t nativeBlock;Check(PK_PART_transmit_b(1,&nativeBody,&nativeOptions,&nativeBlock),"native baseline transmit");
    try{using var stream=File.Create(Path.Combine(output,name+".native.x_t"));for(var p=&nativeBlock;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));}finally{Check(PK_MEMORY_block_f(&nativeBlock),"native baseline free");}
    var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=PK_transmit_format_text_c,transmit_version=371,transmit_meshes=PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s block;Check(KernelRuntime.PartTransmitB(1,&ownBody,&options,&block),"roundtrip own transmit");byte[] bytes;
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));bytes=stream.ToArray();File.WriteAllBytes(Path.Combine(output,name+".x_t"),bytes);}finally{Check(KernelRuntime.MemoryBlockFree(&block),"roundtrip own free");}
    fixed(byte* p=bytes)
    {
        var ownInput=new M.PK_MEMORY_block_s{n_bytes=(nuint)bytes.Length,bytes=p};var ownReceive=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=PK_transmit_format_text_c};int ownCount;int* ownParts;
        Check(KernelRuntime.PartReceiveB(ownInput,&ownReceive,&ownCount,&ownParts),"fresh self receive");
        try
        {
            if(ownCount!=1||!KernelRuntime.TryResolveBodySlot(ownParts[0],out var imported)||KernelRuntime.Bodies[imported].FirstFaceBody<0||!KernelRuntime.TryResolveBodySlot(ownBody,out var original))throw new Exception("fresh self receive stayed opaque");
            var a=KernelRuntime.Faces[KernelRuntime.Bodies[original].FirstFaceBody].SurfTag;var b=KernelRuntime.Faces[KernelRuntime.Bodies[imported].FirstFaceBody].SurfTag;
            var source=KernelRuntime.GetSurfaceByTag(a);var uv=new M.PK_UV_s();uv.param[0]=(source.UMin+source.UMax)*.5;uv.param[1]=.37;
            var expected=new M.PK_VECTOR_s();var actual=new M.PK_VECTOR_s();Check(KernelRuntime.SurfEval(a,uv,0,0,0,&expected),"source roundtrip sample");Check(KernelRuntime.SurfEval(b,uv,0,0,0,&actual),"fresh self sample");
            for(var d=0;d<3;d++)if(Math.Abs(expected.coord[d]-actual.coord[d])>1e-8)throw new Exception("fresh self point mismatch");
        }
        finally{if(ownParts!=null)Check(KernelRuntime.MemoryFree(ownParts),"fresh self parts free");}
        var input=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var receive=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;int* parts;Check(PK_PART_receive_b(input,&receive,&count,&parts),"roundtrip native receive");
        try
        {
            if(count!=1)throw new Exception("roundtrip count");var checks=new PK_BODY_check_o_t{max_faults=100};int faults;PK_check_fault_t* list;Check(PK_BODY_check(parts[0],&checks,&faults,&list),"roundtrip body check");
            try{for(var i=0;i<faults;i++){int cls;Check(PK_ENTITY_ask_class(list[i].entity_1,&cls),"fault class");Console.WriteLine($"{name} fault={list[i].state} entities={list[i].entity_1}/{list[i].entity_2} class={cls}");}if(faults!=0)throw new Exception(name+" BODY_check faults="+faults);}finally{if(list!=null)Check(PK_MEMORY_free(list),"roundtrip fault free");}
            var compare=new PK_DEBUG_BODY_compare_o_t{max_diffs=64,all_tests=PK_LOGICAL_false,num_tests=PK_LOGICAL_true,tol_dev_tests=PK_LOGICAL_true,tol_tests=PK_LOGICAL_true,class_tests=PK_LOGICAL_true,sense_tests=PK_LOGICAL_true,missing_tests=PK_LOGICAL_true,acc_dev_tests=PK_LOGICAL_false,non_match_tests=PK_LOGICAL_false};PK_DEBUG_BODY_compare_r_t result;
            Check(PK_DEBUG_BODY_compare(nativeBody,parts[0],&compare,&result),"roundtrip native compare");
            try
            {
                Console.WriteLine($"{name} BODY_check=0; compare global={result.global_result} local={result.local_result}");
                for(var i=0;i<result.n_global_diffs;i++)Console.WriteLine($"global diff={result.global_diffs[i].diff} counts={result.global_diffs[i].n_masters}/{result.global_diffs[i].n_similars}");
                for(var i=0;i<result.n_face_pairs;i++)for(var j=0;j<result.face_pairs[i].n_local_diffs;j++){var diff=result.face_pairs[i].local_diffs[j];Console.WriteLine($"face={i} diff={diff.diff} entities={diff.master_entity}/{diff.similar_entity} values={diff.master_double:R}/{diff.similar_double:R}");}
                if(result.global_result!=PK_DEBUG_global_res_no_diffs_c||result.local_result!=PK_DEBUG_local_res_no_diffs_c)throw new Exception(name+" structural comparison failed");
            }
            finally{Check(PK_DEBUG_BODY_compare_r_f(&result),"roundtrip compare free");}
        }
        finally{Check(PK_MEMORY_free(parts),"roundtrip received parts free");}
    }
}

static Compiled Compile(ProjectGmKernel.Xt.XtDocument document)
{
    var surfaces=new List<BlendSurfaceInstruction>();var spines=new List<BlendSpineInstruction>();var charts=new List<BlendChartInstruction>();
    var surfaceMap=new Dictionary<int,int>();var spineMap=new Dictionary<int,int>();
    ProjectGmKernel.Xt.XtNode Node(int id)=>document.Nodes.First(n=>n.Index==id);
    int Surface(int id)
    {
        if(surfaceMap.TryGetValue(id,out var existing))return existing;
        var node=Node(id);var f=node.Fields;var index=surfaces.Count;surfaceMap[id]=index;surfaces.Add(default);
        var instruction=new BlendSurfaceInstruction{NormalSense=f[6].Character=='-'?-1:1};
        if(node.Type==56)
        {
            instruction.IsBlend=true;instruction.First=Surface(f[8].Pointer);instruction.Second=Surface(f[9].Pointer);instruction.Spine=Spine(f[10].Pointer);instruction.Range0=f[11].Real;instruction.Range1=f[12].Real;
            surfaces[index]=instruction;
            var si=spines[instruction.Spine];var first=charts[si.ChartOffset];var last=charts[si.ChartOffset+si.ChartCount-1];var t=(first.Parameter+last.Parameter)*.5;
            var view=new NestedBlendProgram(surfaces.ToArray(),spines.ToArray(),charts.ToArray());SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[4];
            var status=NestedBlendSeriesEvaluation.Evaluate(view,index,t,.5,layout,jet);if(status!=AlgorithmStatus.Success)throw new Exception($"compile normal node={id} status={status}");
            status=NestedBlendSeriesEvaluation.Spine(view,instruction.Spine,t,0,0,out var center);if(status!=AlgorithmStatus.Success)throw new Exception("compile spine");
            var sign=Dot(Cross(jet[layout.GetIndex(1,0)],jet[layout.GetIndex(0,1)]),Sub(jet[0],center.Value));
            instruction.NormalSense*=Math.Sign(sign);
            Console.WriteLine($"compiled blend node={id} tube-normal sign={instruction.NormalSense:R}");
        }
        else
        {
            var kind=node.Type switch{50=>SurfaceClass.Plane,51=>SurfaceClass.Cylinder,52=>SurfaceClass.Cone,53=>SurfaceClass.Sphere,54=>SurfaceClass.Torus,_=>throw new Exception("unsupported node="+node.Type)};
            if(kind is not (SurfaceClass.Plane or SurfaceClass.Torus))throw new Exception("extend fixture compiler for analytic kind="+kind);
            var origin=f[7].Vector;var axis=f[8].Vector;var reference=f[kind==SurfaceClass.Torus?11:9].Vector;
            instruction.Analytic=new AnalyticSurface(kind,new(){X=origin.X,Y=origin.Y,Z=origin.Z},new(){X=axis.X,Y=axis.Y,Z=axis.Z},new(){X=reference.X,Y=reference.Y,Z=reference.Z},kind==SurfaceClass.Torus?f[9].Real:0,kind==SurfaceClass.Torus?f[10].Real:0);
        }
        surfaces[index]=instruction;return index;
    }
    int Spine(int id)
    {
        if(spineMap.TryGetValue(id,out var existing))return existing;
        var node=Node(id);var f=node.Fields;var a=Surface(f[7].Pointer);var b=Surface(f[8].Pointer);var chart=Node(f[9].Pointer).Fields;var count=(int)chart[2].Integer;
        var positions=new KernelVector3[count];var tangents=new KernelVector3[count];var parameters=new double[count];var scales=new double[count-1];var chords=new KernelVector3[count-1];
        var program=new NestedBlendProgram(surfaces.ToArray(),spines.ToArray(),charts.ToArray());
        for(var i=0;i<count;i++)
        {
            var p=chart[7+i].Vector;positions[i]=new(){X=p.X,Y=p.Y,Z=p.Z};var point=ProjectGmKernel.Native.Computation.Numerics.TaylorVector.Constant(positions[i],0);
            var status=NestedBlendSeriesEvaluation.Distance(program,a,point,0,out _,out var n0);if(status!=AlgorithmStatus.Success)throw new Exception($"compile chart first id={id} status={status}");
            status=NestedBlendSeriesEvaluation.Distance(program,b,point,0,out _,out var n1);if(status!=AlgorithmStatus.Success)throw new Exception("compile chart second");
            tangents[i]=Scale(Unit(Cross(n0.Value,n1.Value)),f[6].Character=='-'?-1:1);
        }
        var built=OriginalChartParameterMap.Build(positions,tangents,chart[0].Real,chart[1].Real,parameters,scales,chords,out _,out var failure);
        if(built!=AlgorithmStatus.Success)throw new Exception($"chart id={id} failure={failure}");
        var result=spines.Count;spineMap[id]=result;spines.Add(new(){First=a,Second=b,ChartOffset=charts.Count,ChartCount=count,StartType=(LimitType)Node(f[10].Pointer).Fields[0].Character,EndType=(LimitType)Node(f[11].Pointer).Fields[0].Character,ChordalError=chart[3].Real,AngularError=chart[4].Real});
        for(var i=0;i<count;i++)charts.Add(new(){Position=positions[i],Parameter=parameters[i],Chord=i<count-1?chords[i]:default,Scale=i<count-1?scales[i]:1});return result;
    }
    var root=Surface(document.Nodes.First(n=>n.Type==14).Fields[7].Pointer);
    return new(surfaces.ToArray(),spines.ToArray(),charts.ToArray(),root);
}
static unsafe int Materialize(Compiled compiled)
{
    var surfaces=new int[compiled.Surfaces.Length];var spines=new int[compiled.Spines.Length];
    int Surface(int index)
    {
        if(surfaces[index]!=0)return surfaces[index];var source=compiled.Surfaces[index];int tag=0;
        if(source.IsBlend)
        {
            var first=Surface(source.First);var second=Surface(source.Second);var spine=Spine(source.Spine);
            var program=new NestedBlendProgram(compiled.Surfaces,compiled.Spines,compiled.Charts);
            Check(KernelRuntime.BindNestedBlend(first,second,spine,program,index,&tag),"bind self Blend");
        }
        else
        {
            var a=source.Analytic;var basis=new M.PK_AXIS2_sf_s();basis.location.coord[0]=a.Origin.X;basis.location.coord[1]=a.Origin.Y;basis.location.coord[2]=a.Origin.Z;
            basis.axis.coord[0]=a.Axis.X;basis.axis.coord[1]=a.Axis.Y;basis.axis.coord[2]=a.Axis.Z;basis.ref_direction.coord[0]=a.X.X;basis.ref_direction.coord[1]=a.X.Y;basis.ref_direction.coord[2]=a.X.Z;
            if(a.Kind==SurfaceClass.Plane){var sf=new M.PK_PLANE_sf_s{basis_set=basis};Check(KernelRuntime.PlaneCreate(&sf,&tag),"self plane");}
            else if(a.Kind==SurfaceClass.Torus){var sf=new M.PK_TORUS_sf_s{basis_set=basis,major_radius=a.Radius,minor_radius=a.Secondary};Check(KernelRuntime.TorusCreate(&sf,&tag),"self torus");}
            else throw new Exception("unexpected fixture primitive");
        }
        surfaces[index]=tag;return tag;
    }
    int Spine(int index)
    {
        if(spines[index]!=0)return spines[index];var source=compiled.Spines[index];var a=Surface(source.First);var b=Surface(source.Second);int tag=0;
        var program=new NestedBlendProgram(compiled.Surfaces,compiled.Spines,compiled.Charts);
        Check(KernelRuntime.BindNestedBlendSpine(a,b,program,index,&tag),"bind self spine");spines[index]=tag;return tag;
    }
    return Surface(compiled.Root);
}
sealed record Compiled(BlendSurfaceInstruction[] Surfaces,BlendSpineInstruction[] Spines,BlendChartInstruction[] Charts,BenchmarkSurfaceIndex Root);

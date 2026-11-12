#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=IcurveEvalBenchmark
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property UseWebProgressCapture=true
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Xt;
using M=ProjectGmKernel.Native.Generated;

if(!ParasolidScriptHost.TryStartSession("Blend coverage",out var session,out var message))throw new Exception(message);
using(session)unsafe
{
 var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"self start");
 try
 {
  if(WebProgressCapture.Enabled&&Environment.GetEnvironmentVariable("GM_WEB_SELECT")?.StartsWith("open-",StringComparison.Ordinal)==true){OpenCoverage();return 0;}
  var folder=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","nested-blend-native");var document=XtText.DecodeDocument(File.ReadAllText(Path.Combine(folder,"depth3.x_t")));var count=0;
  foreach(var node in document.Nodes)
  {
   if(node.Type==38){node.Fields[6]=XtFieldValue.Char('-');node.Fields[^1]=XtFieldValue.Ptr(0);count++;}
   if(node.Type==56){(node.Fields[8],node.Fields[9])=(node.Fields[9],node.Fields[8]);(node.Fields[11],node.Fields[12])=(node.Fields[12],node.Fields[11]);}
  }
  if(count<3)throw new Exception("negative fixture lacks three spines");var bytes=System.Text.Encoding.ASCII.GetBytes(XtText.Encode(document));File.WriteAllBytes(Path.Combine(folder,"negative-depth3.x_t"),bytes);
  var nativeBody=Receive(bytes);var native=Surface(nativeBody);var own=ImportSurface(bytes);CheckJets(native,own,.35,.65);var ownBox=new M.PK_UVBOX_s();Check(KernelRuntime.SurfAskUvbox(own,&ownBox),"negative own range");var negativeWidth=ownBox.param[2]-ownBox.param[0];ownBox.param[0]+=.35*negativeWidth;ownBox.param[2]-=.35*negativeWidth;ownBox.param[1]=.1;ownBox.param[3]=.9;int ownSheet,nativeSheet;Check(KernelRuntime.SurfMakeSheetBody(own,ownBox,&ownSheet),"negative own sheet");Check(PK_SURF_make_sheet_body(native,*(PK_UVBOX_t*)&ownBox,&nativeSheet),"negative native sheet");
  // Explicit display observer for this case only (no operation: the sheet is the
  // real evaluated input and the comparison is a sense/geometry check, so roles
  // stay neutral and no blend/tracking is claimed).
  var negativeSelected=WebProgressCapture.Select("negative-depth3");
  if(negativeSelected){WebProgressCapture.Before(true,ownSheet,[],"negative-depth3-sense",[]);WebProgressCapture.Before(false,nativeSheet,[],"negative-depth3-sense",[]);}
  var negativeStarted=System.Diagnostics.Stopwatch.GetTimestamp();
  Compare(ownSheet,nativeSheet,"negative-depth3");
  if(negativeSelected){var ms=System.Diagnostics.Stopwatch.GetElapsedTime(negativeStarted).TotalMilliseconds;WebProgressCapture.BooleanAfter(true,[ownSheet],ms,0,0,null);WebProgressCapture.BooleanAfter(false,[nativeSheet],ms,0,0,null);}
  Console.WriteLine("PASS three-level negative sense");
  OpenCoverage();
 }
 finally{Check(KernelRuntime.SessionStop(),"self stop");}
}
Console.WriteLine("PASS Blend open, swapped supports, signed radii and negative sense coverage");return 0;
static unsafe void OpenCoverage()
{
 double* poles=stackalloc double[18];for(var i=0;i<3;i++)for(var j=0;j<2;j++){var at=3*(2*i+j);poles[at]=i-1;poles[at+1]=2*j-1;poles[at+2]=(i==1?-.3:.3)+.1*(i-1)*(2*j-1);}
 double* knots=stackalloc double[2]{-1,1};int* um=stackalloc int[2]{3,3};int* vm=stackalloc int[2]{2,2};var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=1,n_u_vertices=3,n_v_vertices=2,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};int source,ownSource;Check(PK_BSURF_create(&sf,&source),"native open source");Check(KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&ownSource),"own open source");
 var wall=new PK_PLANE_sf_t();wall.basis_set.axis=new(0,1,0);wall.basis_set.ref_direction=new(1,0,0);int plane,ownPlane;Check(PK_PLANE_create(&wall,&plane),"native wall");Check(KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&wall,&ownPlane),"own wall");
 foreach(var swap in new[]{false,true})foreach(var firstSign in new[]{1.0,-1.0})foreach(var secondSign in new[]{1.0,-1.0})
 {
  var webLabel=$"open-swap{swap}-r{firstSign}-{secondSign}";var webSelected=WebProgressCapture.Select(webLabel);if(!webSelected)continue;
  if(webSelected)WebProgressCapture.BeforeSurfaces(false,swap?plane:source,swap?source:plane);
  var a=new PK_VECTOR_t(.5,0,.075);var b=new PK_VECTOR_t(-.5,0,.075);var options=new PK_SURF_create_blend_o_t();int native=0;var webPkStarted=System.Diagnostics.Stopwatch.GetTimestamp();var error=PK_SURF_create_blend(swap?plane:source,.1*firstSign,swap?source:plane,.1*secondSign,a,b,&options,&native);
  var webPkMs=System.Diagnostics.Stopwatch.GetElapsedTime(webPkStarted).TotalMilliseconds;
  if(error!=0){(a,b)=(b,a);webPkStarted=System.Diagnostics.Stopwatch.GetTimestamp();var retry=PK_SURF_create_blend(swap?plane:source,.1*firstSign,swap?source:plane,.1*secondSign,a,b,&options,&native);webPkMs+=System.Diagnostics.Stopwatch.GetElapsedTime(webPkStarted).TotalMilliseconds;Check(retry,"native open reversed hints");}
  if(webSelected)WebProgressCapture.BeforeSurfaces(true,swap?ownPlane:ownSource,swap?ownSource:ownPlane);
  int own;var webGmStarted=System.Diagnostics.Stopwatch.GetTimestamp();var webGmError=KernelRuntime.SurfCreateBlend(swap?ownPlane:ownSource,.1*firstSign,swap?ownSource:ownPlane,.1*secondSign,*(M.PK_VECTOR_s*)&a,*(M.PK_VECTOR_s*)&b,(M.PK_SURF_create_blend_o_s*)&options,&own);var webGmMs=System.Diagnostics.Stopwatch.GetElapsedTime(webGmStarted).TotalMilliseconds;Check(webGmError,"own open construction");
  var nativeDefinition=new PK_BLENDSF_sf_t();Check(PK_BLENDSF_ask(native,&nativeDefinition),"native definition");var ownDefinition=new M.PK_BLENDSF_sf_s();Check(KernelRuntime.BlendSfAsk(own,&ownDefinition),"own definition");
  Console.WriteLine($"case swap={swap} signs={firstSign}/{secondSign} native supports={nativeDefinition.geom_1}/{nativeDefinition.geom_2} input={source}/{plane} radii={nativeDefinition.radii[0]}/{nativeDefinition.radii[1]}");
  var ownBox=new M.PK_UVBOX_s();Check(KernelRuntime.SurfAskUvbox(own,&ownBox),"own range");var nativeBox=new PK_UVBOX_t();nativeBox.param[1]=.15;nativeBox.param[3]=.85;
  for(var i=0;i<=16;i++)
  {
   var u=ownBox.param[0]+(ownBox.param[2]-ownBox.param[0])*(.4+.2*i/16);var center=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(ownDefinition.spine,u,0,&center),"own open spine");double t;Check(PK_CURVE_parameterise_vector(nativeDefinition.spine,*(PK_VECTOR_t*)&center,&t),"native open inverse");
   if(i==0)nativeBox.param[0]=t;if(i==16)nativeBox.param[2]=t;
   for(var j=0;j<=4;j++){var uv=new M.PK_UV_s();uv.param[0]=u;uv.param[1]=.15+.7*j/4;var actual=new M.PK_VECTOR_s();PK_VECTOR_t expected;Check(KernelRuntime.SurfEval(own,uv,0,0,0,&actual),"own open point");Check(PK_SURF_eval(native,new(t,uv.param[1]),0,0,PK_LOGICAL_false,&expected),"native open point");for(var d=0;d<3;d++)if(Math.Abs(expected.coord[d]-actual.coord[d])>1e-9){PK_VECTOR_t reversed;Check(PK_SURF_eval(native,new(t,1-uv.param[1]),0,0,PK_LOGICAL_false,&reversed),"reverse sample");Console.WriteLine($"reverse diff={reversed.coord[d]-actual.coord[d]:R}");throw new Exception($"open mismatch i={i} j={j} d={d} own={actual.coord[d]:R} pk={expected.coord[d]:R}");};}
  }
  if(nativeBox.param[2]<nativeBox.param[0])(nativeBox.param[0],nativeBox.param[2])=(nativeBox.param[2],nativeBox.param[0]);
  var width=ownBox.param[2]-ownBox.param[0];var lower=ownBox.param[0];ownBox.param[0]=lower+.4*width;ownBox.param[2]=lower+.6*width;ownBox.param[1]=.15;ownBox.param[3]=.85;
  int ownSheet,nativeSheet;Check(KernelRuntime.SurfMakeSheetBody(own,ownBox,&ownSheet),"own open sheet");Check(PK_SURF_make_sheet_body(native,nativeBox,&nativeSheet),"native open sheet");if(webSelected){WebProgressCapture.SurfaceAfter(true,ownSheet,webGmMs);WebProgressCapture.SurfaceAfter(false,nativeSheet,webPkMs);}Compare(ownSheet,nativeSheet,$"open-swap{swap}-r{firstSign}-{secondSign}");
  Console.WriteLine($"PASS open swap={swap}, radii={firstSign}/{secondSign}");
 }
}
static unsafe void CheckJets(int native,int own,double lo,double hi)
{
 PK_UVBOX_t box;Check(PK_SURF_ask_uvbox(native,&box),"native box");PK_VECTOR_t* expected=stackalloc PK_VECTOR_t[3];M.PK_VECTOR_s* actual=stackalloc M.PK_VECTOR_s[3];
 for(var i=0;i<31;i++)for(var j=0;j<3;j++){var uv=new PK_UV_t(box.param[0]+(box.param[2]-box.param[0])*(lo+(hi-lo)*(i+.5)/31),.1+.4*j);Check(PK_SURF_eval(native,uv,1,1,PK_LOGICAL_true,expected),"native jets");Check(KernelRuntime.SurfEval(own,*(M.PK_UV_s*)&uv,1,1,1,actual),"own jets");for(var k=0;k<3;k++)for(var d=0;d<3;d++)if(Math.Abs(expected[k].coord[d]-actual[k].coord[d])>1e-8*(1+Math.Abs(expected[k].coord[d])))throw new Exception("jet mismatch");}
}
static unsafe int Receive(byte[] bytes)
{
 fixed(byte* p=bytes){var memory=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;int* parts;Check(PK_PART_receive_b(memory,&options,&count,&parts),"native receive");try{if(count!=1)throw new Exception("receive count");return parts[0];}finally{PK_MEMORY_free(parts);}}
}
static unsafe int Surface(int body){int count;int* faces;Check(PK_BODY_ask_faces(body,&count,&faces),"native faces");try{int surface;Check(PK_FACE_ask_surf(faces[0],&surface),"native face surf");return surface;}finally{PK_MEMORY_free(faces);}}
static unsafe int ImportSurface(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var input=new M.PK_MEMORY_block_s{n_bytes=(nuint)bytes.Length,bytes=p};var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=PK_transmit_format_text_c};int count;int* parts;Check(KernelRuntime.PartReceiveB(input,&options,&count,&parts),"own source receive");try{if(count!=1||!KernelRuntime.TryResolveBodySlot(parts[0],out var slot)||KernelRuntime.Bodies[slot].FirstFaceBody<0)throw new Exception("opaque source");return KernelRuntime.Faces[KernelRuntime.Bodies[slot].FirstFaceBody].SurfTag;}finally{KernelRuntime.MemoryFree(parts);}
    }
}
static unsafe void Compare(int body,int reference,string name)
{
    var folder=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","blend-coverage");Directory.CreateDirectory(folder);var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=PK_transmit_format_text_c,transmit_version=371,transmit_meshes=PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s block;Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"own operation transmit");byte[] bytes;
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
static void Check(int error,string name){if(error!=0)throw new Exception(name+" error="+error);}
static string ScriptPath([CallerFilePath]string path="")=>path;

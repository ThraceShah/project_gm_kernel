#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=IcurveEvalBenchmark
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property UseWebProgressCapture=true
using static parasolid;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;

if(!ParasolidScriptHost.TryStartSession("extended Blend native fixtures",out var session,out var message))throw new Exception(message);
using(session)unsafe
{
 var ownStart=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&ownStart),"own start");
 try{
 var sf=new PK_TORUS_sf_t{major_radius=2,minor_radius=.5};sf.basis_set.axis=new(0,0,1);sf.basis_set.ref_direction=new(1,0,0);int torus;Check(PK_TORUS_create(&sf,&torus),"torus");int ownTorus;Check(KernelRuntime.TorusCreate((M.PK_TORUS_sf_s*)&sf,&ownTorus),"own torus");FixtureState.Own[torus]=ownTorus;
 var wall=Plane(new(0,0,0),new(0,.6,.8),new(1,0,0));var first=Blend(torus,wall,.1,new(2.5,0,0),new(2,.5,-.375));if(first==0)throw new Exception("first");
 PK_UVBOX_t box;Check(PK_SURF_ask_uvbox(first,&box),"first range");
 const double distance=.03,radius=.01;var transform=new M.PK_TRANSF_sf_s();transform.matrix[0]=transform.matrix[5]=transform.matrix[10]=transform.matrix[15]=1;transform.matrix[7]=.6*distance;transform.matrix[11]=.8*distance;
 int tr,second;PK_LOGICAL_t exact;Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&transform,&tr),"translation");Check(PK_GEOM_transform(first,tr,0,&second,&exact),"second support");int ownTransform,ownSecond;byte ownExact;Check(KernelRuntime.TransfCreate(&transform,&ownTransform),"own translation");Check(KernelRuntime.GeomTransform(FixtureState.Own[first],ownTransform,0,&ownSecond,&ownExact),"own second support");FixtureState.Own[second]=ownSecond;
 var u=box.param[0]+(box.param[2]-box.param[0])*.25;PK_VECTOR_t a,b;Check(PK_SURF_eval(first,new(u-.05,.5),0,0,PK_LOGICAL_false,&a),"hint");Check(PK_SURF_eval(first,new(u+.05,.5),0,0,PK_LOGICAL_false,&b),"hint");for(var d=0;d<3;d++){var shift=d==1?.3*distance:d==2?.4*distance:0;a.coord[d]+=shift;b.coord[d]+=shift;}
 var result=Blend(first,second,radius,a,b);var third=Next(result,radius/3);Save(third,"both-supports-depth3.x_t");
 foreach(var scale in new[]{.001,100.0})ScaledFixture(scale);
 }finally{Check(KernelRuntime.SessionStop(),"own stop");}
}
Console.WriteLine("PASS independent both-supports Blend and scaled XT oracles");return 0;
static unsafe void Save(int surface,string name)
{
 PK_UVBOX_t box;Check(PK_SURF_ask_uvbox(surface,&box),"fixture box");var width=box.param[2]-box.param[0];box.param[0]+=.35*width;box.param[2]-=.35*width;box.param[1]=.1;box.param[3]=.9;int body;Check(PK_SURF_make_sheet_body(surface,box,&body),"fixture sheet");
 var folder=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","nested-blend-native");Directory.CreateDirectory(folder);var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;Check(PK_PART_transmit_b(1,&body,&options,&block),"fixture transmit");try{using var stream=File.Create(Path.Combine(folder,name));for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));}finally{PK_MEMORY_block_f(&block);}Console.WriteLine("saved "+name);Independent(surface,FixtureState.Own[surface],name);
}
static unsafe int Plane(PK_VECTOR_t location,PK_VECTOR_t axis,PK_VECTOR_t reference)
{var sf=new PK_PLANE_sf_t();sf.basis_set.location=location;for(var d=0;d<3;d++){sf.basis_set.axis.coord[d]=axis.coord[d];sf.basis_set.ref_direction.coord[d]=reference.coord[d];}int tag=0;Check(PK_PLANE_create(&sf,&tag),"plane");int own;Check(KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&sf,&own),"own plane");FixtureState.Own[tag]=own;return tag;}
static unsafe int Blend(int a,int b,double radius,PK_VECTOR_t start,PK_VECTOR_t end)
{var o=new PK_SURF_create_blend_o_t();int result=0;var error=PK_SURF_create_blend(a,radius,b,radius,start,end,&o,&result);Console.WriteLine($"create {a}/{b}, r={radius:R}: error={error} result={result}");Check(error,"native parallel Blend");int own;Check(KernelRuntime.SurfCreateBlend(FixtureState.Own[a],radius,FixtureState.Own[b],radius,*(M.PK_VECTOR_s*)&start,*(M.PK_VECTOR_s*)&end,(M.PK_SURF_create_blend_o_s*)&o,&own),"own parallel Blend");FixtureState.Own[result]=own;int cls;Check(PK_ENTITY_ask_class(result,&cls),"class");if(cls!=PK_CLASS_blendsf)throw new Exception("fixture was simplified to a different surface class");Console.WriteLine("class="+cls);return result;}
static unsafe int Next(int source,double radius)
{
    PK_UVBOX_t box;Check(PK_SURF_ask_uvbox(source,&box),"next box");
    var u=box.param[0]+(box.param[2]-box.param[0])*.5;PK_VECTOR_t middle,start,end;
    Check(PK_SURF_eval(source,new(u,.5),0,0,PK_LOGICAL_false,&middle),"middle");
    Check(PK_SURF_eval(source,new(u-.1,.5),0,0,PK_LOGICAL_false,&start),"start");
    Check(PK_SURF_eval(source,new(u+.1,.5),0,0,PK_LOGICAL_false,&end),"end");
    var wall=Plane(middle,new(1,0,0),new(0,1,0));return Blend(source,wall,radius,start,end);
}
static void Check(int error,string name){if(error!=0)throw new Exception(name+" error="+error);}
static string ScriptPath([CallerFilePath]string path="")=>path;


static unsafe void Independent(int native,int own,string label)
{
 var expected=new PK_BLENDSF_sf_t();Check(PK_BLENDSF_ask(native,&expected),"native independent spine");var actual=new M.PK_BLENDSF_sf_s();Check(KernelRuntime.BlendSfAsk(own,&actual),"own independent spine");PK_UVBOX_t box;Check(PK_SURF_ask_uvbox(native,&box),"native independent box");double middle=0;
 for(var i=0;i<13;i++){var t=box.param[0]+(box.param[2]-box.param[0])*(.35+.3*(i+.5)/13);PK_VECTOR_t center;Check(PK_CURVE_eval(expected.spine,t,0,&center),"native independent center");double u;Check(KernelRuntime.CurveParameteriseVector(actual.spine,*(M.PK_VECTOR_s*)&center,&u),"own independent inverse");if(i==6)middle=u;
 for(var j=0;j<3;j++){PK_VECTOR_t p;var q=new M.PK_VECTOR_s();var uv=new M.PK_UV_s();uv.param[0]=u;uv.param[1]=.1+.4*j;Check(PK_SURF_eval(native,new(t,uv.param[1]),0,0,PK_LOGICAL_false,&p),"native independent point");Check(KernelRuntime.SurfEval(own,uv,0,0,0,&q),"own independent point");for(var d=0;d<3;d++)if(Math.Abs(q.coord[d]-p.coord[d])>1e-8)throw new Exception("independent both-supports mismatch");}}
 var ownBox=new M.PK_UVBOX_s();Check(KernelRuntime.SurfAskUvbox(own,&ownBox),"own independent range");var half=(ownBox.param[2]-ownBox.param[0])*.005;ownBox.param[0]=middle-half;ownBox.param[2]=middle+half;ownBox.param[1]=.15;ownBox.param[3]=.85;
 for(var i=0;i<2;i++){var p=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(actual.spine,ownBox.param[2*i],0,&p),"own independent end");double t;Check(PK_CURVE_parameterise_vector(expected.spine,*(PK_VECTOR_t*)&p,&t),"native independent inverse");box.param[2*i]=t;}if(box.param[2]<box.param[0])(box.param[0],box.param[2])=(box.param[2],box.param[0]);box.param[1]=.15;box.param[3]=.85;
 int body,reference;Check(KernelRuntime.SurfMakeSheetBody(own,ownBox,&body),"own independent sheet");Check(PK_SURF_make_sheet_body(native,box,&reference),"native independent sheet");
 var captureLabel=System.IO.Path.GetFileNameWithoutExtension(label);
 if(WebProgressCapture.Enabled&&WebProgressCapture.Select(captureLabel))
 {
  int nf;int* faces;Check(PK_BODY_ask_faces(reference,&nf,&faces),"capture faces");var nativeFace=nf>0?faces[0]:0;Check(PK_MEMORY_free(faces),"capture faces free");
  var ownFace=0;if(KernelRuntime.TryResolveBodySlot(body,out var slot)){var fs=KernelRuntime.Bodies[slot].FirstFaceBody;if(fs>=0)ownFace=KernelRuntime.Faces[fs].Header.Tag;}
  var points=new List<double>();var normals=new List<double>();PK_VECTOR_t* vals=stackalloc PK_VECTOR_t[6];PK_VECTOR_t nrm;
  for(var i=0;i<16;i++)
  {
   var u=box.param[0]+(box.param[2]-box.param[0])*(i+.5)/16;var v=box.param[1]+(box.param[3]-box.param[1])*((i*7)%16+.5)/16;
   if(PK_SURF_eval_with_normal(native,new(u,v),1,1,PK_LOGICAL_true,vals,&nrm)!=0)continue;
   points.Add(vals[0].coord[0]);points.Add(vals[0].coord[1]);points.Add(vals[0].coord[2]);normals.Add(nrm.coord[0]);normals.Add(nrm.coord[1]);normals.Add(nrm.coord[2]);
  }
  var note=$"扩展嵌套 blend 真实曲面 {captureLabel}（native 参数域 {box.param[0]:R}..{box.param[2]:R} × {box.param[1]:R}..{box.param[3]:R}）；显示 {points.Count/3} 个实际求值点与原生法向；采样渲染≠连续等价，几何未因显示改变。";
  WebProgressCapture.Evaluation(true,body,ownFace,points.ToArray(),normals.ToArray(),captureLabel,note);
  WebProgressCapture.Evaluation(false,reference,nativeFace,points.ToArray(),normals.ToArray(),captureLabel,note);
 }
 Compare(body,reference,label+"-independent");
}
static unsafe void ScaledFixture(double scale)
{
 var folder=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","nested-blend-native");var bytes=File.ReadAllBytes(Path.Combine(folder,"depth3.x_t"));int native,own;
 fixed(byte* p=bytes){var memory=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var receive=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;int* parts;Check(PK_PART_receive_b(memory,&receive,&count,&parts),"scale native receive");native=parts[0];PK_MEMORY_free(parts);var block=new M.PK_MEMORY_block_s{bytes=p,n_bytes=(nuint)bytes.Length};Check(KernelRuntime.PartReceiveB(block,(M.PK_PART_receive_o_s*)&receive,&count,&parts),"scale own receive");own=parts[0];KernelRuntime.MemoryFree(parts);}
 var sf=new M.PK_TRANSF_sf_s();sf.matrix[0]=sf.matrix[5]=sf.matrix[10]=scale;sf.matrix[15]=1;sf.matrix[3]=.3*scale;sf.matrix[7]=-.2*scale;sf.matrix[11]=.4*scale;int nt,mt;Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&sf,&nt),"native scale transform");Check(KernelRuntime.TransfCreate(&sf,&mt),"own scale transform");int countReplaced;int* replacements;PK_LOGICAL_t* exact;Check(PK_BODY_transform(native,nt,0,&countReplaced,&replacements,&exact),"native scale body");if(replacements!=null)PK_MEMORY_free(replacements);if(exact!=null)PK_MEMORY_free(exact);byte* ownExact;Check(KernelRuntime.BodyTransform(own,mt,0,&countReplaced,&replacements,&ownExact),"own scale body");if(replacements!=null)KernelRuntime.MemoryFree(replacements);if(ownExact!=null)KernelRuntime.MemoryFree(ownExact);
 var label=scale<1?"small-depth3":"large-depth3";
 if(WebProgressCapture.Enabled&&WebProgressCapture.Select(label))
 {
  int nf;int* faces;Check(PK_BODY_ask_faces(native,&nf,&faces),"scaled capture faces");var nativeFace=nf>0?faces[0]:0;Check(PK_MEMORY_free(faces),"scaled capture faces free");
  var ownFace=0;if(KernelRuntime.TryResolveBodySlot(own,out var slot)){var fs=KernelRuntime.Bodies[slot].FirstFaceBody;if(fs>=0)ownFace=KernelRuntime.Faces[fs].Header.Tag;}
  var note=$"扩展嵌套 blend 真实缩放片体 {label}（scale={scale:R}；由 depth3 fixture 真实 receive+transform 得到）；显示真实片体与选中面，采样/几何未因显示改变。";
  WebProgressCapture.Evaluation(true,own,ownFace,[],[],label,note);
  WebProgressCapture.Evaluation(false,native,nativeFace,[],[],label,note);
 }
 Compare(own,native,label);
 var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t output;Check(PK_PART_transmit_b(1,&native,&options,&output),"native scaled fixture");try{using var stream=File.Create(Path.Combine(folder,label+".x_t"));for(var item=&output;item!=null;item=item->next)stream.Write(new ReadOnlySpan<byte>(item->bytes,checked((int)item->n_bytes)));}finally{PK_MEMORY_block_f(&output);}
}
static unsafe void Compare(int body,int reference,string name)
{
    var folder=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","blend-extended");Directory.CreateDirectory(folder);var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=PK_transmit_format_text_c,transmit_version=371,transmit_meshes=PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s block;Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"own operation transmit");byte[] bytes;
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

static class FixtureState{internal static readonly Dictionary<int,int> Own=[];}

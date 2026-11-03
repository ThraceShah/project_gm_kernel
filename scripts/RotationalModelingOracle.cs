#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=RotationalModelingOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
global using ManagedBodyTag=System.Int32;
using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var output=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","rotational-modeling-oracle"));Directory.CreateDirectory(output);
var report=new StringBuilder();void Log(string line){Console.WriteLine(line);report.AppendLine(line);}
try
{
 if(!ParasolidScriptHost.TryStartSession("Independent rotational Boolean oracle",out var host,out var message))throw new Exception(message);
 using(host)unsafe
 {
  var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"self session");
  try
  {
   var cases=new Fixture[]{new("overlap",3,0,4,2,2,4),new("inside",3,0,6,1,2,2),new("through",3,0,4,1,-1,6),
    new("same-radius",2,0,4,2,2,4),new("touch-cap",2,0,2,2,2,2),new("disjoint",3,0,2,1,3,2),new("coincident",2,0,4,2,0,4),
    new("target-inside",1,2,2,3,0,6),new("same-bottom",3,0,4,2,0,3),new("same-caps",3,0,4,2,0,4),
    new("oblique",3,0,4,2,2,4,Oblique:true),new("reversed-tool-axis",3,0,4,2,2,4,Reverse:true)};
   var failures=0;var count=0;
   foreach(var operation in new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})foreach(var f in cases)
   {
    var label=f.Name+"-"+operation;count++;
    try
    {
     var a=Cylinder(true,f,false);var b=Cylinder(true,f,true);var ra=Cylinder(false,f,false);var rb=Cylinder(false,f,true);
     var own=Boolean(true,a,new[]{b},operation,out var os,out var ot);var expected=Boolean(false,ra,new[]{rb},operation,out var es,out var et);
     Require(os==es,$"status {os}/{es}");Require(ot==et,$"tracking {ot}/{et}");ValidateResults(own,expected,f,label,output,Log);Log("PASS "+label);
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.ToString());}
   }
   foreach(var operation in new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})
   {
    var label="repeated-cavity-through-"+operation;count++;
    try
    {
     var f=new Fixture(label,3,0,6,1,2,2);var own=Boolean(true,Cylinder(true,f,false),new[]{Cylinder(true,f,true)},PK_boolean_subtract_c,out _,out _)[0];
     var reference=Boolean(false,Cylinder(false,f,false),new[]{Cylinder(false,f,true)},PK_boolean_subtract_c,out _,out _)[0];
     var tool=new Fixture(label,3,0,6,2,3,4);
     var result=Boolean(true,own,new[]{Cylinder(true,tool,true)},operation,out var os,out var ot);
     var expected=Boolean(false,reference,new[]{Cylinder(false,tool,true)},operation,out var es,out var et);
     Require(os==es,$"status {os}/{es}");Require(ot==et,$"tracking {ot}/{et}");ValidateResults(result,expected,tool,label,output,Log);Log("PASS "+label);
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.ToString());}
   }
   foreach(var operation in new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})
   {
    var label="two-tools-"+operation;count++;
    try
    {
     var f=new Fixture(label,3,0,8,2,1,2);var g=f with{Zb=5};
     var own=Boolean(true,Cylinder(true,f,false),new[]{Cylinder(true,f,true),Cylinder(true,g,true)},operation,out var os,out var ot);
     var expected=Boolean(false,Cylinder(false,f,false),new[]{Cylinder(false,f,true),Cylinder(false,g,true)},operation,out var es,out var et);
     Require(os==es,$"status {os}/{es}");Require(ot==et,$"tracking {ot}/{et}");ValidateResults(own,expected,f,label,output,Log);Log("PASS "+label);
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.ToString());}
   }
   foreach(var operation in new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})
   {
    var label="native-import-copy-transform-"+operation;count++;
    try
    {
     var f=new Fixture(label,3,0,6,1,2,2);var original=Boolean(false,Cylinder(false,f,false),new[]{Cylinder(false,f,true)},PK_boolean_subtract_c,out _,out _)[0];
     var own=CopyTranslate(true,ReceiveManaged(TransmitReference(original)));var reference=CopyTranslate(false,original);
     var tool=new Fixture(label,3,0,6,2,3,4,Shift:true);
     var result=Boolean(true,own,new[]{Cylinder(true,tool,true)},operation,out var os,out var ot);
     var expected=Boolean(false,reference,new[]{Cylinder(false,tool,true)},operation,out var es,out var et);
     Require(os==es,$"status {os}/{es}");Require(ot==et,$"tracking {ot}/{et}");ValidateResults(result,expected,tool,label,output,Log);Log("PASS "+label);
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.ToString());}
   }
   foreach(var relation in new[]{0,1,2,3})foreach(var operation in new[]{PK_boolean_unite_c,PK_boolean_intersect_c,PK_boolean_subtract_c})
   {
    var label="multi-lifetime-"+relation+"-"+operation;count++;
    try
    {
     var f=new Fixture(label,2,0,2,2,relation==1?1:relation==2?2:3,2);var g=f with{Rb=1,Zb=relation==3?4:7};
     var own=Boolean(true,Cylinder(true,f,false),new[]{Cylinder(true,f,true),Cylinder(true,g,true)},operation,out var os,out var ot);
     var expected=Boolean(false,Cylinder(false,f,false),new[]{Cylinder(false,f,true),Cylinder(false,g,true)},operation,out var es,out var et);
     Require(os==es,$"status {os}/{es}");Require(ot==et,$"tracking/lifetimes {ot}/{et}");ValidateResults(own,expected,f,label,output,Log);Log("PASS "+label);
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.ToString());}
   }
   if(!args.Contains("--boolean-only")) foreach(var kind in new[]{0,1,2})foreach(var z in new[]{3d,4d,7d})foreach(var fence in new[]{PK_section_fence_front_c,PK_section_fence_back_c,PK_section_fence_both_c})
   {
    var label=$"axial-section-{kind}-{z}-{fence}";count++;
    try
    {
     var f=new Fixture(label,3,0,6,1,kind==1?-1:2,kind==1?8:2);var own=Cylinder(true,f,false);var reference=Cylinder(false,f,false);
     if(kind!=0){own=Boolean(true,own,new[]{Cylinder(true,f,true)},PK_boolean_subtract_c,out _,out _)[0];reference=Boolean(false,reference,new[]{Cylinder(false,f,true)},PK_boolean_subtract_c,out _,out _)[0];}
     var outputs=Section(true,own,z,fence,out var os);var expected=Section(false,reference,z,fence,out var es);Require(os==es,$"section signature {os}/{es}");
     ValidateResults(outputs,expected,f,label,output,Log);Log("PASS "+label);
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.ToString());}
   }
   if(!args.Contains("--boolean-only")) foreach(var cavity in new[]{false,true})foreach(var fence in new[]{PK_section_fence_front_c,PK_section_fence_back_c,PK_section_fence_both_c})
   {
    var label=$"owned-plane-section-{cavity}-{fence}";count++;
    try
    {
     var f=new Fixture(label,3,0,6,1,2,2);var own=Cylinder(true,f,false);var reference=Cylinder(false,f,false);
     if(cavity){own=Boolean(true,own,new[]{Cylinder(true,f,true)},PK_boolean_subtract_c,out _,out _)[0];reference=Boolean(false,reference,new[]{Cylinder(false,f,true)},PK_boolean_subtract_c,out _,out _)[0];}
     var outputs=Section(true,own,cavity?2:6,fence,out var os,true);var expected=Section(false,reference,cavity?2:6,fence,out var es,true);Require(os==es,$"owned section signature {os}/{es}");
     ValidateResults(outputs,expected,f,label,output,Log);Log("PASS "+label);
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.ToString());}
   }
   if(!args.Contains("--boolean-only")) foreach(var reverse in new[]{false,true})foreach(var fence in new[]{PK_section_fence_front_c,PK_section_fence_back_c,PK_section_fence_both_c})
   {
    var label=$"oblique-axis-section-{reverse}-{fence}";count++;
    try
    {
     var f=new Fixture(label,3,0,6,1,2,2,Oblique:true);var own=Cylinder(true,f,false);var reference=Cylinder(false,f,false);
     var outputs=Section(true,own,3,fence,out var os,false,reverse,f);var expected=Section(false,reference,3,fence,out var es,false,reverse,f);Require(os==es,$"oblique section signature {os}/{es}");
     ValidateResults(outputs,expected,f,label,output,Log);Log("PASS "+label);
    }
    catch(Exception ex){failures++;Log("FAIL "+label+": "+ex.ToString());}
   }
   Require(failures==0,$"{failures}/{count} rotational fixtures failed");Log($"PASS all {count} independent rotational modeling fixtures, tracking, point classification, full body checks and strict XT comparison.");
  }
  finally{Check(KernelRuntime.SessionStop(),"self session stop");}
 }
}
catch(Exception ex){Log("FAIL: "+ex.Message);Environment.ExitCode=1;}
finally{File.WriteAllText(Path.Combine(output,"report.txt"),report.ToString());}

static unsafe int[] Section(bool managed,int body,double z,int fence,out string signature,bool owned=false,bool reverse=false,Fixture? placed=null)
{
 var f=placed??new Fixture("section",3,0,6,1,2,2);var n=f.Oblique?new PK_VECTOR1_t(1d/3,2d/3,2d/3):new PK_VECTOR1_t(0,0,1);if(reverse)n=new PK_VECTOR1_t(-n.coord[0],-n.coord[1],-n.coord[2]);
 var reference=f.Oblique?new PK_VECTOR1_t(2/Math.Sqrt(5),-1/Math.Sqrt(5),0):new PK_VECTOR1_t(1,0,0);
 var definition=new PK_PLANE_sf_t(new PK_AXIS2_sf_t(Position(f,0,z),n,reference));int plane=0;
 if(!owned)Check(managed?KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&definition,&plane):PK_PLANE_create(&definition,&plane),"section plane");
 else
 {
  int nf=0;int* faces=null;Check(managed?KernelRuntime.BodyAskFaces(body,&nf,&faces):PK_BODY_ask_faces(body,&nf,&faces),"owned tool faces");
  for(var i=0;i<nf;i++){int surface=0,geometryClass=0;Check(managed?KernelRuntime.FaceAskSurf(faces[i],&surface):PK_FACE_ask_surf(faces[i],&surface),"owned support");Check(managed?KernelRuntime.EntityAskClass(surface,&geometryClass):PK_ENTITY_ask_class(surface,&geometryClass),"owned support type");if(geometryClass!=PK_CLASS_plane)continue;PK_PLANE_sf_t p=default;if(managed)Check(KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&p),"owned plane form");else Check(PK_PLANE_ask(surface,&p),"owned plane form");if(Math.Abs(p.basis_set.location.coord[2]-z)<1e-9){plane=surface;break;}}
  if(managed)KernelRuntime.MemoryFree(faces);else PK_MEMORY_free(faces);Require(plane!=0,"missing owned cutting plane");
 }
 var options=new PK_BODY_section_o_t{fence=fence};var bodies=new List<int>();
 if(managed)
 {
  var o=Unsafe.BitCast<PK_BODY_section_o_t,M.PK_BODY_section_o_s>(options);M.PK_section_r_s r=default;Check(KernelRuntime.BodySectionWithSurf(body,plane,&o,&r),"self section");
  try{signature=$"{r.front_bodies.length}/{r.front_faces.length}:{r.back_bodies.length}/{r.back_faces.length}";for(var i=0;i<r.front_bodies.length;i++)bodies.Add(r.front_bodies.array[i]);for(var i=0;i<r.back_bodies.length;i++)bodies.Add(r.back_bodies.array[i]);}finally{KernelRuntime.SectionResultFree(&r);}
 }
 else
 {
  PK_section_r_t r=default;Check(PK_BODY_section_with_surf(body,plane,&options,&r),"PK section");
  try{signature=$"{r.front_bodies.length}/{r.front_faces.length}:{r.back_bodies.length}/{r.back_faces.length}";for(var i=0;i<r.front_bodies.length;i++)bodies.Add(r.front_bodies.array[i]);for(var i=0;i<r.back_bodies.length;i++)bodies.Add(r.back_bodies.array[i]);}finally{PK_section_r_f(&r);}
 }
 int cls=0;
 var sourceError=managed?KernelRuntime.EntityAskClass(body,&cls):PK_ENTITY_ask_class(body,&cls);
 Require(sourceError==PK_ERROR_no_errors||sourceError==PK_ERROR_not_a_tag,$"section source ask_class error {sourceError}");
 var toolError=managed?KernelRuntime.EntityAskClass(plane,&cls):PK_ENTITY_ask_class(plane,&cls);
 Require(toolError==PK_ERROR_no_errors||toolError==PK_ERROR_not_a_tag,$"section tool ask_class error {toolError}");
 signature+=$"/source-ask-class={sourceError}/first-is-source={bodies.Count>0&&bodies[0]==body}/tool-ask-class={toolError}";
 return bodies.ToArray();
}
static unsafe int Cylinder(bool managed,Fixture f,bool tool)
{
 var z=tool?f.Zb:f.Za;var h=tool?f.Hb:f.Ha;var reverse=tool&&f.Reverse;var axis=f.Oblique?new PK_VECTOR1_t(1d/3,2d/3,2d/3):new PK_VECTOR1_t(0,0,1);
 var reference=f.Oblique?new PK_VECTOR1_t(2/Math.Sqrt(5),-1/Math.Sqrt(5),0):new PK_VECTOR1_t(1,0,0);var p=Position(f,0,z+(reverse?h:0));
 if(reverse)axis=new PK_VECTOR1_t(-axis.coord[0],-axis.coord[1],-axis.coord[2]);
 var basis=new PK_AXIS2_sf_t(p,axis,reference);int body=0;
 if(managed){var m=Unsafe.BitCast<PK_AXIS2_sf_t,M.PK_AXIS2_sf_s>(basis);Check(KernelRuntime.BodyCreateSolidCyl(tool?f.Rb:f.Ra,h,&m,&body),"self cylinder");}
 else Check(PK_BODY_create_solid_cyl(tool?f.Rb:f.Ra,h,&basis,&body),"PK cylinder");return body;
}
static unsafe PK_VECTOR_t Position(Fixture f,double r,double z)
{var p=f.Oblique?new PK_VECTOR_t(12+z/3+r*2/Math.Sqrt(5),-5+2*z/3-r/Math.Sqrt(5),8+2*z/3):new PK_VECTOR_t(r,0,z);if(f.Shift){p.coord[0]+=2;p.coord[1]-=1;p.coord[2]+=.5;}return p;}
static unsafe int CopyTranslate(bool managed,int body)
{
 int copy=0;Check(managed?KernelRuntime.EntityCopy(body,&copy):PK_ENTITY_copy(body,&copy),"copy imported body");Check(managed?KernelRuntime.EntityDelete(1,&body):PK_ENTITY_delete(1,&body),"delete original imported body");
 var form=new M.PK_TRANSF_sf_s();form.matrix[0]=form.matrix[5]=form.matrix[10]=form.matrix[15]=1;form.matrix[3]=2;form.matrix[7]=-1;form.matrix[11]=.5;int transform=0,count=0;int* replaced=null;
 if(managed){Check(KernelRuntime.TransfCreate(&form,&transform),"self transform create");byte* exact=null;Check(KernelRuntime.BodyTransform(copy,transform,0,&count,&replaced,&exact),"self copy translate");if(replaced!=null)KernelRuntime.MemoryFree(replaced);if(exact!=null)KernelRuntime.MemoryFree(exact);Check(KernelRuntime.EntityDelete(1,&transform),"self delete transform");}
 else{Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&form,&transform),"PK transform create");PK_LOGICAL_t* exact=null;Check(PK_BODY_transform(copy,transform,0,&count,&replaced,&exact),"PK copy translate");if(replaced!=null)PK_MEMORY_free(replaced);if(exact!=null)PK_MEMORY_free(exact);Check(PK_ENTITY_delete(1,&transform),"PK delete transform");}return copy;
}
static unsafe int[] Boolean(bool managed,int target,int[] tools,int function,out int status,out string tracking)
{
 var origins=new Dictionary<int,string>();foreach(var body in tools.Prepend(target))CaptureOrigins(managed,body,origins);
 var options=new PK_BODY_boolean_o_t{function=function,tracking=PK_LOGICAL_true};var signatures=new List<string>();int[] outputs;
 fixed(int* p=tools)
 {
  if(managed)
  {
   var o=Unsafe.BitCast<PK_BODY_boolean_o_t,M.PK_BODY_boolean_o_s>(options);M.PK_TOPOL_track_r_s tr=default;M.PK_boolean_r_s result=default;
   Check(KernelRuntime.BodyBoolean2(target,tools.Length,p,&o,&tr,&result),"self Boolean");
   try{status=result.result;outputs=new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray();for(var i=0;i<tr.n_track_records;i++){var rec=tr.track_records[i];var keys=new List<string>();for(var j=0;j<rec.n_original_topols;j++)keys.Add(rec.original_classes[j]+":"+origins[rec.original_topols[j]]);keys.Sort();Require(rec.n_product_topols==1,"tracking products");signatures.Add(rec.track+"/"+string.Join("|",keys)+"->"+EntityKey(true,rec.product_topols[0],PK_CLASS_edge));}}
   finally{KernelRuntime.TopolTrackResultFree(&tr);KernelRuntime.BooleanResultFree(&result);}
  }
  else
  {
   PK_TOPOL_track_r_t tr=default;PK_boolean_r_t result=default;Check(PK_BODY_boolean_2(target,tools.Length,p,&options,&tr,&result),"PK Boolean");
   try{status=result.result;outputs=new ReadOnlySpan<int>(result.bodies,result.n_bodies).ToArray();for(var i=0;i<tr.n_track_records;i++){var rec=tr.track_records[i];var keys=new List<string>();for(var j=0;j<rec.n_original_topols;j++)keys.Add(rec.original_classes[j]+":"+origins[rec.original_topols[j]]);keys.Sort();Require(rec.n_product_topols==1,"tracking products");signatures.Add(rec.track+"/"+string.Join("|",keys)+"->"+EntityKey(false,rec.product_topols[0],PK_CLASS_edge));}}
   finally{PK_TOPOL_track_r_f(&tr);PK_boolean_r_f(&result);}
  }
 }
 signatures.Sort();var inputs=tools.Prepend(target).ToArray();var lives=new List<string>();foreach(var input in inputs){int cls=0;var e=managed?KernelRuntime.EntityAskClass(input,&cls):PK_ENTITY_ask_class(input,&cls);lives.Add(e==0?"alive":"dead");}
 tracking=string.Join(";",signatures)+"/lifetime="+string.Join(",",lives)+"/output="+string.Join(",",outputs.Select(x=>Array.IndexOf(inputs,x)));return outputs;
}
static unsafe void CaptureOrigins(bool managed,int body,Dictionary<int,string> origins)
{
 foreach(var cls in new[]{PK_CLASS_face,PK_CLASS_edge}){int n=0;int* tags=null;Check(managed?(cls==PK_CLASS_face?KernelRuntime.BodyAskFaces(body,&n,&tags):KernelRuntime.BodyAskEdges(body,&n,&tags)):(cls==PK_CLASS_face?PK_BODY_ask_faces(body,&n,&tags):PK_BODY_ask_edges(body,&n,&tags)),"origin topology");try{for(var i=0;i<n;i++)origins[tags[i]]=EntityKey(managed,tags[i],cls);}finally{if(managed)KernelRuntime.MemoryFree(tags);else PK_MEMORY_free(tags);}}
}
static unsafe string EntityKey(bool managed,int entity,int cls)
{
 int geometry=0;Check(managed?(cls==PK_CLASS_face?KernelRuntime.FaceAskSurf(entity,&geometry):KernelRuntime.EdgeAskCurve(entity,&geometry)):(cls==PK_CLASS_face?PK_FACE_ask_surf(entity,&geometry):PK_EDGE_ask_curve(entity,&geometry)),"key geometry");
 int type=0;Check(managed?KernelRuntime.EntityAskClass(geometry,&type):PK_ENTITY_ask_class(geometry,&type),"key class");
 if(type==PK_CLASS_circle){PK_CIRCLE_sf_t d=default;if(managed){M.PK_CIRCLE_sf_s m=default;Check(KernelRuntime.CircleAsk(geometry,&m),"circle");d=Unsafe.BitCast<M.PK_CIRCLE_sf_s,PK_CIRCLE_sf_t>(m);}else Check(PK_CIRCLE_ask(geometry,&d),"circle");return $"circle:{d.radius:F8}:{d.basis_set.location.coord[0]:F8},{d.basis_set.location.coord[1]:F8},{d.basis_set.location.coord[2]:F8}";}
 if(type==PK_CLASS_cyl){PK_CYL_sf_t d=default;if(managed){M.PK_CYL_sf_s m=default;Check(KernelRuntime.CylAsk(geometry,&m),"cylinder");d=Unsafe.BitCast<M.PK_CYL_sf_s,PK_CYL_sf_t>(m);}else Check(PK_CYL_ask(geometry,&d),"cylinder");return $"cyl:{d.radius:F8}";}
 PK_PLANE_sf_t plane=default;if(managed){M.PK_PLANE_sf_s m=default;Check(KernelRuntime.PlaneAsk(geometry,&m),"plane");plane=Unsafe.BitCast<M.PK_PLANE_sf_s,PK_PLANE_sf_t>(m);}else Check(PK_PLANE_ask(geometry,&plane),"plane");
 return $"plane:{plane.basis_set.location.coord[0]:F8},{plane.basis_set.location.coord[1]:F8},{plane.basis_set.location.coord[2]:F8}";
}
static unsafe void ValidateResults(int[] own,int[] expected,Fixture fixture,string label,string output,Action<string> log)
{
 Require(own.Length==expected.Length,$"output count {own.Length}/{expected.Length}");var remaining=expected.ToList();
 for(var i=0;i<own.Length;i++)
 {
  var bytes=TransmitManaged(own[i]);File.WriteAllBytes(Path.Combine(output,label+"-"+i+"-managed.x_t"),bytes);var received=ReceiveOracle(bytes);
  var m=MassKey(received);var match=remaining.OrderBy(b=>Distance(m,MassKey(b))).First();Require(Distance(m,MassKey(match))<1e-6,"no geometric match for result body");remaining.Remove(match);
  CheckReferenceBody(received,label+" received",log);CheckReferenceBody(match,label+" baseline",log);
  for(var k=0;k<5;k++)Require(OracleCount(match,k)==OracleCount(received,k),$"topology kind {k}: {OracleCount(received,k)}/{OracleCount(match,k)}");
  CompareBodies(match,received,label,log);CompareMass(match,received,label,log);
  var referenceBytes=TransmitReference(match);File.WriteAllBytes(Path.Combine(output,label+"-"+i+"-reference.x_t"),referenceBytes);var imported=ReceiveManaged(referenceBytes);
  foreach(var body in new[]{own[i],imported})foreach(var r in new[]{0d,.5,1d,1.5,2d,2.5,3d,4d})foreach(var z in new[]{-1d,0d,1d,2d,3d,4d,5d,6d,7d,9d})
  {
   var p=Position(fixture,r,z);var mp=Unsafe.BitCast<PK_VECTOR_t,M.PK_VECTOR_s>(p);int me=0,pe=0,mt=0,pt=0;
   Check(KernelRuntime.BodyContainsVector(body,mp,&me,&mt),"self classify r="+r+" z="+z+" nativeImported="+(body==imported));Check(PK_BODY_contains_vector(match,p,&pe,&pt),"PK classify");Require(me==pe,$"classification r={r} z={z}: {me}/{pe}");
   int mc=0,pc=0;Check(KernelRuntime.EntityAskClass(mt,&mc),"self boundary class");Check(PK_ENTITY_ask_class(pt,&pc),"PK boundary class");Require(mc==pc,$"boundary dimension r={r} z={z}: {mc}/{pc}");
  }
 }
}
static unsafe double[] MassKey(int body){var o=new PK_TOPOL_eval_mass_props_o_t();double* x=stackalloc double[15];Check(PK_TOPOL_eval_mass_props(1,&body,.999999999,&o,x,x+1,x+2,x+5,x+14),"mass key");return new[]{x[0],x[2],x[3],x[4]};}
static double Distance(double[] a,double[] b)=>Enumerable.Range(0,a.Length).Max(i=>Math.Abs(a[i]-b[i])/Math.Max(1,Math.Abs(a[i])));

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

static unsafe int ReceiveManaged(byte[] bytes)
{
    fixed(byte* pointer=bytes)
    {
        var block=new M.PK_MEMORY_block_s{bytes=pointer,n_bytes=(nuint)bytes.Length};
        var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=M.ParasolidConstants.PK_transmit_format_text_c};int count=0;int* parts=null;
        Check(KernelRuntime.PartReceiveB(block,&options,&count,&parts),"self receive independent PK body");
        try{Require(count==1&&parts!=null,"self receive count");return parts[0];}
        finally{if(parts!=null)Check(KernelRuntime.MemoryFree(parts),"free self receive list");}
    }
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
            DumpCircleUse(diff.master_entity,log);DumpCircleUse(diff.similar_entity,log);
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

static unsafe void DumpCircleUse(int entity,Action<string> log)
{
 int cls=0;Check(PK_ENTITY_ask_class(entity,&cls),"diagnostic entity");int curve=entity,edge=0;
 if(cls==PK_CLASS_edge)edge=entity;else {int n=0;int* owners=null;Check(PK_CURVE_ask_edges(entity,&n,&owners),"diagnostic owners");if(n>0)edge=owners[0];if(owners!=null)PK_MEMORY_free(owners);}
 PK_LOGICAL_t sense=default;PK_INTERVAL_t interval=default;PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];Check(PK_EDGE_ask_geometry(edge,PK_LOGICAL_true,&curve,&cls,ends,&interval,&sense),"diagnostic geometry");PK_CIRCLE_sf_t d=default;Check(PK_CIRCLE_ask(curve,&d),"diagnostic circle");
 log($"    circle r={d.radius} center={d.basis_set.location.coord[0]},{d.basis_set.location.coord[1]},{d.basis_set.location.coord[2]} axis={d.basis_set.axis.coord[0]},{d.basis_set.axis.coord[1]},{d.basis_set.axis.coord[2]} use={(int)sense}");
}
static unsafe void CompareMass(int reference,int actual,string label,Action<string> log)
{
    var options=new PK_TOPOL_eval_mass_props_o_t();double* expected=stackalloc double[15];double* received=stackalloc double[15];
    // FD §31.1.1: accuracy approaches ONE for more integration work; it is not
    // a requested error tolerance. Low values are unsuitable for validation.
    Check(PK_TOPOL_eval_mass_props(1,&reference,.999999999,&options,expected,expected+1,expected+2,expected+5,expected+14),label+" reference mass");
    Check(PK_TOPOL_eval_mass_props(1,&actual,.999999999,&options,received,received+1,received+2,received+5,received+14),label+" received mass");
    for(var i=0;i<15;i++)Require(double.IsFinite(expected[i])&&double.IsFinite(received[i])&&Math.Abs(expected[i]-received[i])<=2e-7*Math.Max(1,Math.Abs(expected[i])),label+$" mass property {i}: {expected[i]:R}/{received[i]:R}");
    log(label+$" volume={expected[0]:R}/{received[0]:R}, area={expected[14]:R}/{received[14]:R}");
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

readonly record struct Fixture(string Name,double Ra,double Za,double Ha,double Rb,double Zb,double Hb,bool Oblique=false,bool Reverse=false,bool Shift=false);

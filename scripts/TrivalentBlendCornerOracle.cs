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
using System.Diagnostics;
using M=ProjectGmKernel.Native.Generated;
unsafe
{
 if(!ParasolidScriptHost.TryStartSession("trivalent equal radius corner",out var host,out var message))throw new Exception(message);
 using(host)
 {
  var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"self session");
  try
  {
   var directory=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","trivalent-corner-oracle");Directory.CreateDirectory(directory);
   var consumer=args.Contains("--consumer");var refillet=args.Contains("--refillet");var orderText=args.FirstOrDefault(a=>a.StartsWith("--order=",StringComparison.Ordinal));var permutation=orderText==null?0:int.Parse(orderText.AsSpan(8));Require(permutation>=0&&permutation<3,"cyclic three-edge order");var filtered=args.Where(a=>!a.StartsWith("--",StringComparison.Ordinal)).ToArray();var cases=filtered.Length==0?new[]{"base","rigid","mirror-scale","extended-base","extended-rigid","extended-mirror-scale","uv-reversed","uv-scaled","tilted","extended-tilted","skew-base","skew-rigid","skew-mirror-scale","skew-tilted","zero-single","zero-double","zero-all","zero-near","zero-single-mirror-scale","zero-double-tilted","zero-all-mirror-scale"}:filtered;
   foreach(var name in cases)
   {
    foreach(var authorOwn in args.Contains("--native-author")?new[]{false}:args.Contains("--self-author")?new[]{true}:new[]{false,true})
    {
    var label=name+(permutation==0?"":"-order"+permutation)+(authorOwn?"-self-author":"-native-author");var author=name.StartsWith("zero-",StringComparison.Ordinal)?Block(authorOwn,.5,name.StartsWith("zero-single",StringComparison.Ordinal)?1:.5,0,1,name.StartsWith("zero-single",StringComparison.Ordinal)?2:1,name.StartsWith("zero-all",StringComparison.Ordinal)||name.StartsWith("zero-near",StringComparison.Ordinal)?1:2):name.StartsWith("skew-",StringComparison.Ordinal)?SkewPrism(authorOwn):Roof(authorOwn,name.StartsWith("extended-",StringComparison.Ordinal),name=="uv-reversed",name=="uv-scaled");var source=authorOwn?Transmit(author):TransmitNative(author);Check(authorOwn?KernelRuntime.EntityDelete(1,&author):PK_ENTITY_delete(1,&author),"delete unmodified author");
    File.WriteAllBytes(Path.Combine(directory,label+"-source.x_t"),source);
    var native=Corner(false,name,source,permutation);CheckBody(native,label+" native");File.WriteAllBytes(Path.Combine(directory,label+"-reference.x_t"),TransmitNative(native));
    var own=Corner(true,name,source,permutation);var bytes=Transmit(own);File.WriteAllBytes(Path.Combine(directory,label+"-self.x_t"),bytes);var received=Receive(bytes);CheckBody(received,label+" received");Compare(native,received,label);
    ValidateCorner(own,native,(name.StartsWith("zero-near",StringComparison.Ordinal)?.999999:name.StartsWith("zero-",StringComparison.Ordinal)?1:.1)*(name.EndsWith("mirror-scale",StringComparison.Ordinal)?1.7:1));
    if(name is "zero-single" or "zero-double" or "zero-all")CheckExtinctionReload(ref own,native,name);
    if(consumer){Require(name=="base"||name=="extended-base","consumer probe uses untransformed source");ConsumeCorner(ref own,ref native,label,directory,refillet);}
    Check(PK_ENTITY_delete(1,&received),"delete received");Check(PK_ENTITY_delete(1,&native),"delete reference");Check(KernelRuntime.EntityDelete(1,&own),"delete self corner");
    Require(KernelRuntime.Bodies.AliveCount==0&&KernelRuntime.Curves.AliveCount==0&&KernelRuntime.Surfaces.AliveCount==0,"corner construction geometry lifecycle");
    Console.WriteLine("PASS "+label);
    }
   }
  }
  finally{Check(KernelRuntime.SessionStop(),"stop self session");}
 }
}
static unsafe int Corner(bool own,string name,byte[] source,int permutation)
{
 var height=name.StartsWith("zero-",StringComparison.Ordinal)&&!name.StartsWith("zero-all",StringComparison.Ordinal)&&!name.StartsWith("zero-near",StringComparison.Ordinal)?2:1;var radius=name.StartsWith("zero-near",StringComparison.Ordinal)?.999999:name.StartsWith("zero-",StringComparison.Ordinal)?1:.1;var expectedProducts=name.StartsWith("zero-single",StringComparison.Ordinal)?3:name.StartsWith("zero-double",StringComparison.Ordinal)?2:name.StartsWith("zero-all",StringComparison.Ordinal)?1:4;
 var body=own?ReceiveManaged(source):Receive(source);int n;int* vertices;Check(own?KernelRuntime.BodyAskVertices(body,&n,&vertices):PK_BODY_ask_vertices(body,&n,&vertices),"source vertices");var corner=0;
 var best=double.NegativeInfinity;
 try{for(var i=0;i<n;i++){int tag;PK_POINT_sf_t p;Check(own?KernelRuntime.VertexAskPoint(vertices[i],&tag):PK_VERTEX_ask_point(vertices[i],&tag),"vertex geometry");Check(own?KernelRuntime.PointAsk(tag,(M.PK_POINT_sf_s*)&p):PK_POINT_ask(tag,&p),"vertex position");if(name.StartsWith("skew-",StringComparison.Ordinal)){var score=p.position.coord[0]-.01*p.position.coord[1];if(Math.Abs(p.position.coord[2]-1)<1e-10&&score>best){best=score;corner=vertices[i];}}else if(Math.Abs(p.position.coord[0])+Math.Abs(p.position.coord[1])+Math.Abs(p.position.coord[2]-height)<1e-10)corner=vertices[i];}}finally{Free(own,vertices);}
 Require(corner!=0,"manufactured selected vertex");int* edges;Check(own?KernelRuntime.BodyAskEdges(body,&n,&edges):PK_BODY_ask_edges(body,&n,&edges),"source edges");int* ends=stackalloc int[2];int* selected=stackalloc int[3];var count=0;
 try{for(var i=0;i<n;i++){Check(own?KernelRuntime.EdgeAskVertices(edges[i],ends):PK_EDGE_ask_vertices(edges[i],ends),"incident graph");if(ends[0]==corner||ends[1]==corner){Require(count<3,"trivalent fixture");selected[count++]=edges[i];}}}finally{Free(own,edges);}Require(count==3,"three selected incident edges");
 // Explicit geometric source order shared by both kernels before pose.
 PK_VECTOR_t* remote=stackalloc PK_VECTOR_t[3];for(var i=0;i<3;i++)
 {
  Check(own?KernelRuntime.EdgeAskVertices(selected[i],ends):PK_EDGE_ask_vertices(selected[i],ends),"ordered source endpoints");var vertex=ends[0]==corner?ends[1]:ends[0];int point;PK_POINT_sf_t geometry;Check(own?KernelRuntime.VertexAskPoint(vertex,&point):PK_VERTEX_ask_point(vertex,&point),"ordered source vertex");Check(own?KernelRuntime.PointAsk(point,(M.PK_POINT_sf_s*)&geometry):PK_POINT_ask(point,&geometry),"ordered source position");remote[i]=geometry.position;
 }
 for(var i=0;i<2;i++)for(var j=i+1;j<3;j++)
 {
  var compare=remote[j].coord[2].CompareTo(remote[i].coord[2]);if(compare==0)compare=remote[j].coord[0].CompareTo(remote[i].coord[0]);if(compare==0)compare=remote[j].coord[1].CompareTo(remote[i].coord[1]);if(compare>0){(selected[i],selected[j])=(selected[j],selected[i]);(remote[i],remote[j])=(remote[j],remote[i]);}
 }
 for(var i=0;i<permutation;i++){var first=selected[0];selected[0]=selected[1];selected[1]=selected[2];selected[2]=first;}
 Console.WriteLine($"SELECTED {(own?"self":"native")} order={permutation}: {selected[0]},{selected[1]},{selected[2]}");

 var scale=name.EndsWith("mirror-scale",StringComparison.Ordinal)?1.7:1.0;
 if(name.EndsWith("rigid",StringComparison.Ordinal)||name.EndsWith("mirror-scale",StringComparison.Ordinal)||name.EndsWith("tilted",StringComparison.Ordinal))
 {
  var c=Math.Cos(.47);var s=Math.Sin(.47);var tilt=name.EndsWith("tilted",StringComparison.Ordinal)?.31:0;var ca=Math.Cos(tilt);var sa=Math.Sin(tilt);var parity=name.EndsWith("mirror-scale",StringComparison.Ordinal)?-1:1;var form=new M.PK_TRANSF_sf_s();form.matrix[0]=scale*c*parity;form.matrix[1]=-scale*s*ca;form.matrix[2]=scale*s*sa;form.matrix[4]=scale*s*parity;form.matrix[5]=scale*c*ca;form.matrix[6]=-scale*c*sa;form.matrix[9]=scale*sa;form.matrix[10]=scale*ca;form.matrix[15]=1;form.matrix[3]=3;form.matrix[7]=-2;form.matrix[11]=5;
  int transform;Check(own?KernelRuntime.TransfCreate(&form,&transform):PK_TRANSF_create((PK_TRANSF_sf_t*)&form,&transform),"similarity");int* replaced;
  if(own){byte* exact;Check(KernelRuntime.BodyTransform(body,transform,0,&n,&replaced,&exact),"self similarity");Free(true,replaced);Free(true,exact);}else{PK_LOGICAL_t* exact;Check(PK_BODY_transform(body,transform,0,&n,&replaced,&exact),"native similarity");Free(false,replaced);Free(false,exact);}
  Check(own?KernelRuntime.EntityDelete(1,&transform):PK_ENTITY_delete(1,&transform),"delete transform");
 }
 var set=new PK_EDGE_set_blend_constant_o_t();set.properties.tolerance=1e-7;int* marked;Check(own?KernelRuntime.EdgeSetBlendConstant(3,selected,radius*scale,(M.PK_EDGE_set_blend_constant_o_s*)&set,&n,&marked):PK_EDGE_set_blend_constant(3,selected,radius*scale,&set,&n,&marked),"set three equal radii");Free(own,marked);
 var began=Stopwatch.GetTimestamp();
 if(own)
 {
  var fix=new M.PK_BODY_fix_blends_o_s{o_t_version=31,update=PK_blend_edge_update_v261_c,tolerance=1e-7};int* blends;M.PK_FACE_array_s* under;int* replaced;int fault,edge,topol;
  var error=KernelRuntime.BodyFixBlends(body,&fix,&n,&blends,&under,&replaced,&fault,&edge,&topol);
  Console.WriteLine($"SELF corner error={error}, stage={KernelRuntime.LastBlendCornerStage}, event={KernelRuntime.LastBlendCornerEvent}, pair={KernelRuntime.LastBlendCornerPair}, certificate={KernelRuntime.LastCornerCertificate}/{KernelRuntime.LastCornerCertificateBand}/{KernelRuntime.LastCornerCertificateFace}, roots={KernelRuntime.LastBlendCornerRoots}, status={KernelRuntime.LastBlendCornerRootStatus}");Check(error,"corner fix_blends");Require(n==expectedProducts,"surviving bands and spherical corner products");
  Free(true,blends);Free(true,replaced);for(var i=0;i<n;i++)Free(true,under[i].array);Free(true,under);
 }
 else{var fix=new PK_BODY_fix_blends_o_t{update=PK_blend_edge_update_v261_c,tolerance=1e-7};int fault,edge,topol;int* blends;PK_FACE_array_t* unders;int* replaced;Check(PK_BODY_fix_blends(body,&fix,&n,&blends,&unders,&replaced,&fault,&edge,&topol),"native corner fix");Require(n==expectedProducts,"surviving edge blends plus a vertex blend");PK_MEMORY_free(blends);PK_MEMORY_free(replaced);for(var i=0;i<n;i++)PK_MEMORY_free(unders[i].array);PK_MEMORY_free(unders);}
 Console.WriteLine($"TIME {(own?"self":"native")} {name} ms={Stopwatch.GetElapsedTime(began).TotalMilliseconds:F3}");return body;
}
static unsafe void CheckExtinctionReload(ref int own,int reference,string name)
{
 var reloaded=ReceiveManaged(Transmit(own));var previous=own;Check(KernelRuntime.EntityDelete(1,&previous),"delete pre-reload extinction body");own=reloaded;
 var height=name=="zero-all"?1:2;var points=new[]{new PK_VECTOR_t(.9,.9,height-.9),new PK_VECTOR_t(.05,.05,height-.05),new PK_VECTOR_t(3,3,3)};
 foreach(var point in points){int actual,expected,topology;Check(KernelRuntime.BodyContainsVector(own,*(M.PK_VECTOR_s*)&point,&actual,&topology),"self reloaded extinction material");Check(PK_BODY_contains_vector(reference,point,&expected,&topology),"native extinction material");Require(actual==expected,$"extinction finite region {point.coord[0]},{point.coord[1]},{point.coord[2]} self={actual} native={expected}");}
 Console.WriteLine("PASS extinction XT reload/delete and interior/removed-corner/outside classification");
}
static unsafe void ConsumeCorner(ref int own,ref int reference,string label,string directory,bool refillet)
{
 var bytes=Transmit(own);var reloaded=ReceiveManaged(bytes);var old=own;Check(KernelRuntime.EntityDelete(1,&old),"delete pre-reload corner");own=reloaded;
 var ownTool=Block(true,-.475,.5,-.1,1.05,2,2);var referenceTool=Block(false,-.475,.5,-.1,1.05,2,2);
 own=CutCorner(true,own,ownTool);reference=CutCorner(false,reference,referenceTool);
 File.WriteAllBytes(Path.Combine(directory,label+"-cut-self.x_t"),Transmit(own));File.WriteAllBytes(Path.Combine(directory,label+"-cut-reference.x_t"),TransmitNative(reference));
 var received=Receive(Transmit(own));try{CheckBody(received,label+" cut received");CheckBody(reference,label+" cut reference");Compare(reference,received,label+" sphere-band-cut");}finally{Check(PK_ENTITY_delete(1,&received),"delete cut received");}
 var points=new[]{new PK_VECTOR_t(.5,.5,.5),new PK_VECTOR_t(.02,.5,.5),new PK_VECTOR_t(.07,.1,.95),new PK_VECTOR_t(.07,.02,.99),new PK_VECTOR_t(2,2,2)};
 foreach(var point in points){int a,b,t;Check(KernelRuntime.BodyContainsVector(own,*(M.PK_VECTOR_s*)&point,&a,&t),"self corner cut point classification");Check(PK_BODY_contains_vector(reference,point,&b,&t),"native corner cut point classification");Require(a==b,$"cut region point ({point.coord[0]},{point.coord[1]},{point.coord[2]}) self={a} PK={b}");}
 Console.WriteLine("PASS reloaded Sphere/Blend crossing consumer and 5 material classifications "+label);
 if(!refillet)return;
 own=CutCorner(true,own,Block(true,.5,1.1,.7,.5,1,1));reference=CutCorner(false,reference,Block(false,.5,1.1,.7,.5,1,1));
 File.WriteAllBytes(Path.Combine(directory,label+"-refillet-input-self.x_t"),Transmit(own));File.WriteAllBytes(Path.Combine(directory,label+"-refillet-input-reference.x_t"),TransmitNative(reference));
 RefilletCornerCut(false,reference);File.WriteAllBytes(Path.Combine(directory,label+"-refillet-reference.x_t"),TransmitNative(reference));RefilletCornerCut(true,own);
 File.WriteAllBytes(Path.Combine(directory,label+"-refillet-self.x_t"),Transmit(own));File.WriteAllBytes(Path.Combine(directory,label+"-refillet-reference.x_t"),TransmitNative(reference));
 received=Receive(Transmit(own));try{CheckBody(received,label+" refillet received");CheckBody(reference,label+" refillet reference");Compare(reference,received,label+" cut-new-edge-refillet");}finally{Check(PK_ENTITY_delete(1,&received),"delete re-fillet received");}
 Console.WriteLine("PASS corner -> reload -> Sphere/Blend cut -> second cut -> new edge fillet "+label);
}
static unsafe void RefilletCornerCut(bool own,int body)
{
 int n;int* edges;Check(own?KernelRuntime.BodyAskEdges(body,&n,&edges):PK_BODY_ask_edges(body,&n,&edges),"re-fillet result edges");var selected=0;
 try{for(var e=0;e<n;e++)
 {
  int nf;int* faces;Check(own?KernelRuntime.EdgeAskFins(edges[e],&nf,&faces):PK_EDGE_ask_fins(edges[e],&nf,&faces),"new cut edge supports");var spline=false;var knife=false;
  try{for(var f=0;f<nf;f++){int face,surface,type;Check(own?KernelRuntime.FinAskFace(faces[f],&face):PK_FIN_ask_face(faces[f],&face),"new edge incident face");Check(own?KernelRuntime.FaceAskSurf(face,&surface):PK_FACE_ask_surf(face,&surface),"new cut support");Check(own?KernelRuntime.EntityAskClass(surface,&type):PK_ENTITY_ask_class(surface,&type),"new cut support class");if(type==PK_CLASS_bsurf)spline=true;if(type==PK_CLASS_plane){PK_PLANE_sf_t plane;Check(own?KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&plane):PK_PLANE_ask(surface,&plane),"knife support plane");if(Math.Abs(plane.basis_set.location.coord[1]-.6)<1e-9&&Math.Abs(plane.basis_set.axis.coord[1])>.99)knife=true;}}}finally{Free(own,faces);}
  if(spline&&knife){Require(selected==0,"one generated roof/knife edge");selected=edges[e];}
 }}finally{Free(own,edges);}Require(selected!=0,"generated curved edge available for re-fillet");
 if(own){var bs=KernelRuntime.ResolveTagRecord(body).Slot;var selectedRecord=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(selected).Slot];var cv=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(selectedRecord.CurveTag)];Console.WriteLine($"REFILLET source regions={KernelRuntime.Bodies[bs].RegionCount},shells={KernelRuntime.Bodies[bs].ShellCount},edge={selected},class={cv.Class},finite={selectedRecord.HasParameterRange},range=[{selectedRecord.ParameterStart},{selectedRecord.ParameterEnd}],vertices={selectedRecord.StartVertex}/{selectedRecord.EndVertex}");}
 var mark=new PK_EDGE_set_blend_constant_o_t();mark.properties.tolerance=1e-7;int count;int* marked;Check(own?KernelRuntime.EdgeSetBlendConstant(1,&selected,.025,(M.PK_EDGE_set_blend_constant_o_s*)&mark,&count,&marked):PK_EDGE_set_blend_constant(1,&selected,.025,&mark,&count,&marked),"set generated curved edge radius");Free(own,marked);
 var options=new PK_BODY_fix_blends_o_t{update=PK_blend_edge_update_v261_c,tolerance=1e-7};int fault,edge,topol;var start=Stopwatch.GetTimestamp();var error=own?KernelRuntime.BodyFixBlends(body,(M.PK_BODY_fix_blends_o_s*)&options,&count,null,null,null,&fault,&edge,&topol):PK_BODY_fix_blends(body,&options,&count,null,null,null,&fault,&edge,&topol);
 Console.WriteLine($"TIME {(own?"self":"native")} new curved edge r=.025 {Stopwatch.GetElapsedTime(start).TotalMilliseconds:F1}ms error={error},count={count},fault={fault},stage={KernelRuntime.LastRegularBlendStage}");Check(error,"re-fillet generated curved edge");Require(fault==PK_blend_fault_no_fault_c&&count==1,"one new real Blend without fault");
}
static unsafe int CutCorner(bool own,int target,int tool)
{
 var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};int n;int* results;var started=Stopwatch.GetTimestamp();int error;
 fixed(KernelRuntime.RegularBooleanStage* stage=&KernelRuntime.LastRegularBooleanStage)
 fixed(int* face=&KernelRuntime.LastRegularBooleanFace)
 {
  var stageAddress=(nint)stage;var faceAddress=(nint)face;
  var progress=new Timer(_=>Console.WriteLine($"PROGRESS {(own?"self":"PK")} Sphere/Blend cut {Stopwatch.GetElapsedTime(started).TotalSeconds:F1}s stage={*(KernelRuntime.RegularBooleanStage*)stageAddress},face={*(int*)faceAddress}"),null,TimeSpan.FromSeconds(10),TimeSpan.FromSeconds(10));
  try{error=own?KernelRuntime.BodyBoolean(target,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&results):PK_BODY_boolean(target,1,&tool,&options,&n,&results);}
  finally{progress.DisposeAsync().AsTask().GetAwaiter().GetResult();}
 }
 Console.WriteLine($"TIME {(own?"self":"PK")} Sphere/Blend cut {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}ms,error={error},stage={KernelRuntime.LastRegularBooleanStage},face={KernelRuntime.LastRegularBooleanFace},sheet={KernelRuntime.LastSheetPartitionStage}");Check(error,"Sphere/Blend crossing subtract");
 try{Require(n==1,"one connected corner cut result");return results[0];}finally{Free(own,results);}
}
static unsafe int ReceiveManaged(byte[] source)
{
 fixed(byte* data=source)
 {
  var block=new M.PK_MEMORY_block_s{bytes=data,n_bytes=(nuint)source.Length};var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;int* parts;
  Check(KernelRuntime.PartReceiveB(block,(M.PK_PART_receive_o_s*)&options,&count,&parts),"self unmodified source receive");try{Require(count==1,"one source body");return parts[0];}finally{Free(true,parts);}
 }
}
static unsafe void ValidateCorner(int own,int reference,double radius)
{
 int n;int* nativeFaces;Check(PK_BODY_ask_faces(reference,&n,&nativeFaces),"native sphere faces");var expected=new PK_SPHERE_sf_t();var sphereCount=0;
 try{for(var i=0;i<n;i++){int surface,type;Check(PK_FACE_ask_surf(nativeFaces[i],&surface),"native corner support");Check(PK_ENTITY_ask_class(surface,&type),"native corner support class");if(type==PK_CLASS_sphere){Check(PK_SPHERE_ask(surface,&expected),"native rolling sphere");sphereCount++;}}}finally{Free(false,nativeFaces);}Require(sphereCount==1,"one independent native spherical corner");
 Require(KernelRuntime.TryResolveBodySlot(own,out var body),"own corner body");var face=KernelRuntime.Bodies[body].FirstFaceBody;var sphereFace=-1;
 for(var f=0;f<KernelRuntime.Bodies[body].FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag)].Class==SurfaceClass.Sphere){Require(sphereFace<0,"unique self sphere");sphereFace=face;}
 Require(sphereFace>=0,"analytic self spherical corner");var sphere=new M.PK_SPHERE_sf_s();Check(KernelRuntime.SphereAsk(KernelRuntime.Faces[sphereFace].SurfTag,&sphere),"self sphere");
 var center=*(PK_VECTOR_t*)&sphere.basis_set.location;Require(Distance(center,expected.basis_set.location)<1e-7,"independent rolling center");Require(Math.Abs(sphere.radius-radius)<1e-10&&Math.Abs(expected.radius-radius)<1e-10,"constant corner radius");
 var loop=KernelRuntime.Loops[KernelRuntime.Faces[sphereFace].FirstLoop];Require(loop.FinCount==3,"three shared spherical boundary arcs");var fin=loop.FirstFin;var interfaceSamples=0;var tangentSamples=0;
 M.PK_VECTOR_s* jet=stackalloc M.PK_VECTOR_s[4];
 for(var e=0;e<3;e++,fin=KernelRuntime.Fins[fin].NextInLoop)
 {
  var edge=KernelRuntime.Edges[KernelRuntime.Fins[fin].Edge];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(edge.CurveTag)];Require(curve.Class==CurveClass.Circle,"exact spherical interface circle");
  var other=KernelRuntime.Fins[fin].Other;Require(other>=0,"shared manifold sphere edge");var supportFace=KernelRuntime.Loops[KernelRuntime.Fins[other].Loop].Face;var support=KernelRuntime.Faces[supportFace].SurfTag;
  for(var k=1;k<=9;k++)
  {
   var t=edge.ParameterStart+(edge.ParameterEnd-edge.ParameterStart)*k/10;var point=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(edge.CurveTag,t,0,&point),"sphere boundary evaluation");
   interfaceSamples++;var p=*(PK_VECTOR_t*)&point;Require(Math.Abs(Distance(p,center)-radius)<1e-8,"whole-circle radius sample");var uv=new M.PK_UV_s();Check(KernelRuntime.SurfParameteriseVector(support,point,&uv),"adjacent band boundary inverse");Check(KernelRuntime.SurfEval(support,uv,1,1,0,jet),"adjacent band G1 jet");Require(Distance(p,*(PK_VECTOR_t*)jet)<1e-8,"shared arc position");
   var a=*(PK_VECTOR_t*)(jet+2);var b=*(PK_VECTOR_t*)(jet+1);var normal=new PK_VECTOR_t(a.coord[1]*b.coord[2]-a.coord[2]*b.coord[1],a.coord[2]*b.coord[0]-a.coord[0]*b.coord[2],a.coord[0]*b.coord[1]-a.coord[1]*b.coord[0]);
   if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(support)].Class==SurfaceClass.Plane)continue;
   var cosine=0.0;var length=0.0;for(var d=0;d<3;d++){cosine+=(p.coord[d]-center.coord[d])*normal.coord[d];length+=normal.coord[d]*normal.coord[d];}Require(Math.Abs(cosine)/(radius*Math.Sqrt(length))>1-1e-7,"spherical corner to band G1");tangentSamples++;
  }
 }
 Console.WriteLine($"PASS radius/center and {interfaceSamples} spherical interface position samples; {tangentSamples} live-band G1 samples");
 ValidateBandBoundaries(own,reference);
}
static unsafe void ValidateBandBoundaries(int own,int reference)
{
 int n;int* faces;Check(PK_BODY_ask_faces(reference,&n,&faces),"native band candidates");int* candidates=stackalloc int[n];int* classes=stackalloc int[n];var count=0;
 try{for(var f=0;f<n;f++){int surface,type;Check(PK_FACE_ask_surf(faces[f],&surface),"native band geometry");Check(PK_ENTITY_ask_class(surface,&type),"native band class");if(type is PK_CLASS_blendsf or PK_CLASS_cyl){candidates[count]=surface;classes[count++]=type;}}}finally{Free(false,faces);}
 Require(KernelRuntime.TryResolveBodySlot(own,out var body),"self band body");var face=KernelRuntime.Bodies[body].FirstFaceBody;var maximum=0.0;var samples=0;var contacts=0;
 M.PK_VECTOR_s* firstJet=stackalloc M.PK_VECTOR_s[4];M.PK_VECTOR_s* secondJet=stackalloc M.PK_VECTOR_s[4];PK_VECTOR_t* nativePoint=stackalloc PK_VECTOR_t[1];
 for(var f=0;f<KernelRuntime.Bodies[body].FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)
 {
  var surface=KernelRuntime.Faces[face].SurfTag;var kind=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)].Class;if(kind is not (SurfaceClass.BlendSurface or SurfaceClass.Cylinder))continue;
  var type=kind==SurfaceClass.BlendSurface?PK_CLASS_blendsf:PK_CLASS_cyl;var loop=KernelRuntime.Loops[KernelRuntime.Faces[face].FirstLoop];var fin=loop.FirstFin;
  for(var e=0;e<loop.FinCount;e++,fin=KernelRuntime.Fins[fin].NextInLoop)
  {
   var edge=KernelRuntime.Edges[KernelRuntime.Fins[fin].Edge];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(edge.CurveTag)];var isContact=curve.Class==CurveClass.Line||curve.Class==CurveClass.ICurve&&KernelRuntime.ICurveDataPool[curve.DataIndex].IsBlendContact!=0;
   var other=KernelRuntime.Fins[fin].Other;Require(other>=0,"band boundary shared edge");var otherSurface=KernelRuntime.Faces[KernelRuntime.Loops[KernelRuntime.Fins[other].Loop].Face].SurfTag;
   if(isContact&&kind==SurfaceClass.Cylinder&&KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(otherSurface)].Class==SurfaceClass.Plane)
   {
    var cylinder=new M.PK_CYL_sf_s();var plane=new M.PK_PLANE_sf_s();Check(KernelRuntime.CylAsk(surface,&cylinder),"band cylinder tangent certificate");Check(KernelRuntime.PlaneAsk(otherSurface,&plane),"adjacent plane tangent certificate");
    var distance=0.0;for(var d=0;d<3;d++)distance+=(cylinder.basis_set.location.coord[d]-plane.basis_set.location.coord[d])*plane.basis_set.axis.coord[d];
    if(Math.Abs(Math.Abs(distance)-cylinder.radius)>1e-8){Require(Math.Abs(distance)<1e-8,"collapsed support leaves a center-plane sharp cap");isContact=false;}
   }
   for(var k=1;k<=9;k++)
   {
    var t=edge.ParameterStart+(edge.ParameterEnd-edge.ParameterStart)*k/10;var point=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(edge.CurveTag,t,0,&point),"active boundary point");var p=*(PK_VECTOR_t*)&point;var best=double.PositiveInfinity;
    for(var c=0;c<count;c++)if(classes[c]==type){var uv=new PK_UV_t();if(PK_SURF_parameterise_vector(candidates[c],p,&uv)!=0)continue;if(PK_SURF_eval(candidates[c],uv,0,0,PK_LOGICAL_false,nativePoint)!=0)continue;best=Math.Min(best,Distance(p,nativePoint[0]));}
    maximum=Math.Max(maximum,best);Require(best<1e-7,"independent native geometry on actual finite band boundary");samples++;
    if(!isContact)continue;var a=new M.PK_UV_s();var b=new M.PK_UV_s();Check(KernelRuntime.SurfParameteriseVector(surface,point,&a),"band contact inverse");Check(KernelRuntime.SurfParameteriseVector(otherSurface,point,&b),"source support contact inverse");
    Check(KernelRuntime.SurfEval(surface,a,1,1,0,firstJet),"band contact jet");Check(KernelRuntime.SurfEval(otherSurface,b,1,1,0,secondJet),"source contact jet");Require(Distance(p,*(PK_VECTOR_t*)firstJet)<1e-8&&Distance(p,*(PK_VECTOR_t*)secondJet)<1e-8,"contact geometry position");
    var na=Cross(*(PK_VECTOR_t*)(firstJet+2),*(PK_VECTOR_t*)(firstJet+1));var nb=Cross(*(PK_VECTOR_t*)(secondJet+2),*(PK_VECTOR_t*)(secondJet+1));Require(Math.Abs(Dot(na,nb))/Math.Sqrt(Dot(na,na)*Dot(nb,nb))>1-1e-7,"band/source support G1");contacts++;
   }
  }
 }
 Console.WriteLine($"PASS independent finite band geometry samples={samples}, max distance={maximum:R}; source contact G1 samples={contacts}");
}
static unsafe PK_VECTOR_t Cross(PK_VECTOR_t a,PK_VECTOR_t b)=>new(a.coord[1]*b.coord[2]-a.coord[2]*b.coord[1],a.coord[2]*b.coord[0]-a.coord[0]*b.coord[2],a.coord[0]*b.coord[1]-a.coord[1]*b.coord[0]);
static unsafe double Dot(PK_VECTOR_t a,PK_VECTOR_t b)=>a.coord[0]*b.coord[0]+a.coord[1]*b.coord[1]+a.coord[2]*b.coord[2];
static unsafe int Roof(bool own,bool extended=false,bool reverse=false,bool scaled=false)
{
    var body=Block(own,.5,.5,0,1,1,1);int n;int* faces;
    Check(own?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"input faces");var top=0;var detached=0;
    try
    {
        for(var i=0;i<n;i++)
        {
            int surface;Check(own?KernelRuntime.FaceAskSurf(faces[i],&surface):PK_FACE_ask_surf(faces[i],&surface),"face support");PK_PLANE_sf_t plane;
            Check(own?KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&plane):PK_PLANE_ask(surface,&plane),"input plane");
            if(Math.Abs(plane.basis_set.axis.coord[2])>.9&&Math.Abs(plane.basis_set.location.coord[2]-1)<1e-10){top=faces[i];detached=surface;}
        }
    }
    finally{Free(own,faces);}
    var nv=extended?3:5;Require(top!=0,"top plane");double* poles=stackalloc double[3*5*nv];double* knots=stackalloc double[2]{scaled?-2:0,scaled?3:1};double* vKnots=stackalloc double[2]{scaled?4:0,scaled?7:1};int* um=stackalloc int[2]{5,5};int* vm=stackalloc int[2]{nv,nv};
    for(var i=0;i<5;i++)for(var j=0;j<nv;j++){var at=3*(i*nv+j);poles[at]=reverse?1-i/4.0:i/4.0;poles[at+1]=j/(double)(nv-1);poles[at+2]=1+(i==2&&j==nv/2?.4:0);}
    var sf=new PK_BSURF_sf_t{u_degree=4,v_degree=nv-1,n_u_vertices=5,n_v_vertices=nv,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,
        u_knot=knots,v_knot=vKnots,u_knot_mult=um,v_knot_mult=vm,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,
        self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};int roof;byte sense=reverse?(byte)0:(byte)1;
    Check(own?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&roof):PK_BSURF_create(&sf,&roof),"roof surface");
    Check(own?KernelRuntime.TopologyDetachGeometry(top):PK_TOPOL_detach_geom(top),"detach top support");
    Check(own?KernelRuntime.FaceAttachSurfaces(1,&top,&roof,&sense):PK_FACE_attach_surfs(1,&top,&roof,(PK_LOGICAL_t*)&sense),"attach freeform roof");
    if(own&&KernelRuntime.IsValidTag(detached))Check(KernelRuntime.EntityDelete(1,&detached),"delete detached author plane");
    return body;
}
static unsafe int SkewPrism(bool own)
{
 var basis=new PK_AXIS2_sf_t{location=new(0,0,0),axis=new(0,0,1),ref_direction=new(1,0,0)};int body;
 Check(own?KernelRuntime.BodyCreateSolidPrism(1.2,1,6,(M.PK_AXIS2_sf_s*)&basis,&body):PK_BODY_create_solid_prism(1.2,1,6,&basis,&body),"non-orthogonal prism source");return body;
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

static unsafe void Compare(int reference,int actual,string name)
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
            Console.WriteLine($"{(chart?"CHART":"STRUCTURE")} face={f}, diff={d.diff}, entity={d.master_entity}/{d.similar_entity}, class={d.master_class}/{d.similar_class}, int={d.master_int}/{d.similar_int}, value={d.master_double:R}/{d.similar_double:R}");if(!chart){structural++;DumpCurveUse(reference,d.master_entity);DumpCurveUse(actual,d.similar_entity);}
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
}
static unsafe void DumpCurveUse(int body,int requested)
{
 int ne;int* edges;Check(PK_BODY_ask_edges(body,&ne,&edges),"diff edges");PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
 try{for(var e=0;e<ne;e++){int curve,type;PK_INTERVAL_t interval;PK_LOGICAL_t sense;Check(PK_EDGE_ask_geometry(edges[e],PK_LOGICAL_true,&curve,&type,ends,&interval,&sense),"diff geometry");if(curve!=requested)continue;Console.WriteLine($"CURVEUSE body={body}, edge={edges[e]}, curve={curve}, class={type}, edgeSense={(sense==PK_LOGICAL_true?1:0)}, range=[{interval.value[0]:R},{interval.value[1]:R}], ends=({ends[0].coord[0]:R},{ends[0].coord[1]:R},{ends[0].coord[2]:R}) / ({ends[1].coord[0]:R},{ends[1].coord[1]:R},{ends[1].coord[2]:R})");if(type==PK_CLASS_circle){PK_CIRCLE_sf_t sf;Check(PK_CIRCLE_ask(curve,&sf),"diff circle");Console.WriteLine($"CIRCLE center=({sf.basis_set.location.coord[0]:R},{sf.basis_set.location.coord[1]:R},{sf.basis_set.location.coord[2]:R}), axis=({sf.basis_set.axis.coord[0]:R},{sf.basis_set.axis.coord[1]:R},{sf.basis_set.axis.coord[2]:R}), ref=({sf.basis_set.ref_direction.coord[0]:R},{sf.basis_set.ref_direction.coord[1]:R},{sf.basis_set.ref_direction.coord[2]:R})");}}}finally{PK_MEMORY_free(edges);}
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
static unsafe double Distance(PK_VECTOR_t a,PK_VECTOR_t b){var sum=0.0;for(var d=0;d<3;d++)sum+=Math.Pow(a.coord[d]-b.coord[d],2);return Math.Sqrt(sum);}
static unsafe void Free(bool own,void* memory){if(memory!=null)Check(own?KernelRuntime.MemoryFree(memory):PK_MEMORY_free(memory),"return buffer");}
static void Check(int error,string name){if(error!=0)throw new Exception($"{name}: error={error}, blend={KernelRuntime.LastRegularBlendStage}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, sheet={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, parameter={KernelRuntime.LastSheetParameter:R}, algorithm={KernelRuntime.LastSheetAlgorithmStatus}");}
static void Require(bool value,string message){if(!value)throw new Exception(message);}
static string ScriptPath([CallerFilePath]string path="")=>path;
static unsafe byte[] TransmitNative(int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;Check(PK_PART_transmit_b(1,&body,&options,&block),"native text XT");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}finally{Check(PK_MEMORY_block_f(&block),"native XT memory");}
}

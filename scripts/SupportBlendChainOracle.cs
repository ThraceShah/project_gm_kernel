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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using M=ProjectGmKernel.Native.Generated;

KernelRuntime.ForbidLegacyRegularBlendAssembly=args.Contains("--event-arrangement",StringComparer.Ordinal);
var remoteBore=args.Contains("--remote-bore",StringComparer.Ordinal);
var reversePlaneChart=args.Contains("--reverse-plane-chart",StringComparer.Ordinal);
var outputName=args.Single(x=>x.StartsWith("--output-name=",StringComparison.Ordinal))["--output-name=".Length..];
Require(outputName.Length>0&&outputName.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'),"one new output directory name is required");
var directory=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","support-blend-chain-oracle",outputName);
Require(!Directory.Exists(directory),"Existing execution evidence is immutable");Directory.CreateDirectory(directory);
ParasolidMassProperties.BeginEvidence(directory);
Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
if(!ParasolidScriptHost.TryStartSession("Independent smooth chain support transitions",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"self session");
    try
    {
        foreach(var fixture in new[]{(Name:"asymmetric43",Location:.43,Slope:0.0,CurvedWall:false),(Name:"asymmetric61",Location:.61,Slope:0.0,CurvedWall:false),(Name:"oblique43",Location:.43,Slope:.2,CurvedWall:false),(Name:"nurbs43",Location:.43,Slope:0.0,CurvedWall:true)})foreach(var reflected in new[]{false,true})
        {
            var name=$"{fixture.Name}-{(reflected?"reflected":"base")}";if(args.Any(a=>!a.StartsWith("--",StringComparison.Ordinal))&&!args.Contains(name,StringComparer.Ordinal))continue;
            if(remoteBore){Require(!fixture.CurvedWall,"the bore fixture requires a planar wall");name+="-remote-bore";}
            if(reversePlaneChart){Require(!fixture.CurvedWall,"the chart fixture requires a planar wall");name+="-reversed-chart";}
            // Author a genuine partition seam in PK, then both modifiers
            // consume the identical finite input, independently of their
            // primitive creators' LINE parameterization conventions.
            var native=Roof(false,fixture.CurvedWall);if(remoteBore)BoreNative(native);
            var scale=1/Math.Sqrt(1+fixture.Slope*fixture.Slope);
            var planeDefinition=new PK_PLANE_sf_t{basis_set=new(){location=new(fixture.Location,0,0),axis=new(scale,fixture.Slope*scale,0),ref_direction=new(0,0,1)}};int plane;
            Check(PK_PLANE_create(&planeDefinition,&plane),"seam plane");int n;int* imprinted;
            Check(PK_BODY_imprint_plane(native,plane,1e-8,&n,&imprinted),"author finite input seam");Free(false,imprinted);
            if(reflected)Transform(false,native);
            var input=TransmitNative(native);File.WriteAllBytes(Path.Combine(directory,name+"-input.x_t"),input);var own=ReceiveSelf(input);
            var a=Select(true,own,reflected);var b=Select(false,native,reflected);Require(a.Length==2&&b.Length==2,"two selected segments across real face seam");
            var boredFace=0;
            if(remoteBore)foreach(var edge in a)
            {
                var fin=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(edge).Slot].FirstFinEdge;
                for(var side=0;side<2;side++,fin=KernelRuntime.Fins[fin].Other)
                {
                    var face=KernelRuntime.Faces[KernelRuntime.Loops[KernelRuntime.Fins[fin].Loop].Face];
                    if(face.LoopCount==2)boredFace=face.Header.Tag;
                }
            }
            Require(!remoteBore||boredFace!=0,"bore belongs to an actually selected support");
            if(args.Contains("--separate-support",StringComparer.Ordinal)){SeparateSupport(true,a[0]);SeparateSupport(false,b[0]);}
            var changedChartFace=0;var reversedBasis=new PK_PLANE_sf_t();
            if(reversePlaneChart)
            {
                changedChartFace=SeparateSupport(true,a[0],true);SeparateSupport(false,b[0],true);
                reversedBasis=PlaneBasis(changedChartFace);
            }
            Check(KernelRuntime.BodyAskFaces(own,&n,&imprinted),"original self faces");int[] retained;
            try{retained=new ReadOnlySpan<int>(imprinted,n).ToArray();}finally{Free(true,imprinted);}Require(retained.Length==(remoteBore?11:10),"source faces include the bore wall");
            var radius=reflected?.17:.1;var selfMade=Fix(true,own,a,radius);var nativeMade=Fix(false,native,b,radius);
            Require(selfMade==nativeMade&&selfMade==3,"two independent seam transports create three Blend faces");
            foreach(var face in retained)Require(KernelRuntime.IsValidTag(face),"surviving source face identity");
            if(remoteBore)Require(KernelRuntime.Faces[KernelRuntime.ResolveTagRecord(boredFace).Slot].LoopCount==2,"modified selected support retains its bore loop");
            if(reversePlaneChart)
            {
                var basis=PlaneBasis(changedChartFace);
                for(var axis=0;axis<3;axis++)Require(basis.basis_set.ref_direction.coord[axis]==reversedBasis.basis_set.ref_direction.coord[axis],
                    "modified support retains its native chart");
            }
            var bytes=Transmit(own);File.WriteAllBytes(Path.Combine(directory,name+"-result.x_t"),bytes);var received=Receive(bytes);
            if(WebProgressCapture.Enabled)WebProgressCapture.BindQuality(name,own,native,received,bytes,TransmitNative(native));
            try
            {
                CheckBody(native,name+" native");CheckBody(received,name+" received");Compare(native,received,name);
            }
            finally{Check(PK_ENTITY_delete(1,&received),"delete receive");}
            var restored=ReceiveSelf(bytes);Check(KernelRuntime.EntityDelete(1,&own),"delete before reload");own=restored;
            var reloaded=Receive(Transmit(own));
            if(WebProgressCapture.Enabled)WebProgressCapture.BindQuality(name+" reload",own,native,reloaded,Transmit(own),TransmitNative(native));
            try{CheckBody(reloaded,name+" reload");Compare(native,reloaded,name+" reload");}
            finally{Check(PK_ENTITY_delete(1,&reloaded),"delete reload check");}
            if(args.Contains("--lifecycle",StringComparer.Ordinal))
            {
                int copied=0,nativeCopy=0;Check(KernelRuntime.EntityCopy(own,&copied),"copy reloaded self Blend");Check(PK_ENTITY_copy(native,&nativeCopy),"copy native Blend");
                Check(KernelRuntime.EntityDelete(1,&own),"delete self Blend source");Check(PK_ENTITY_delete(1,&native),"delete native Blend source");own=copied;native=nativeCopy;
                Transform(true,own);Transform(false,native);
                bytes=Transmit(own);File.WriteAllBytes(Path.Combine(directory,name+"-lifecycle.x_t"),bytes);
                File.WriteAllBytes(Path.Combine(directory,name+"-lifecycle-reference.x_t"),TransmitNative(native));
                reloaded=Receive(bytes);
                if(WebProgressCapture.Enabled)WebProgressCapture.BindQuality(name+" lifecycle",own,native,reloaded,bytes,TransmitNative(native));
                try{CheckBody(reloaded,name+" lifecycle");CheckBody(native,name+" lifecycle native");Compare(native,reloaded,name+" lifecycle");}
                finally{Check(PK_ENTITY_delete(1,&reloaded),"delete lifecycle check");}
            }
            if(!args.Contains("--blend-only",StringComparer.Ordinal))
            {
            Cut(true,own,reflected);Cut(false,native,reflected);bytes=Transmit(own);File.WriteAllBytes(Path.Combine(directory,name+"-cut.x_t"),bytes);received=Receive(bytes);
            if(WebProgressCapture.Enabled)WebProgressCapture.BindQuality(name+" cut",own,native,received,bytes,TransmitNative(native));
            if(remoteBore)
            {
                var body=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(own).Slot];var f=body.FirstFaceBody;var innerRings=0;
                for(var i=0;i<body.FaceCountBody;i++,f=KernelRuntime.Faces[f].NextInBody)
                {
                    var face=KernelRuntime.Faces[f];
                    if(face.LoopCount==2&&KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(face.SurfTag)].Class==SurfaceClass.Plane)innerRings++;
                }
                Require(innerRings==2,"both finite bore rings survive the reloaded cut");
            }
            try
            {
                CheckBody(native,name+" cut native");CheckBody(received,name+" cut received");Compare(native,received,name+" cut");
            }
            finally{Check(PK_ENTITY_delete(1,&received),"delete cut receive");}
            }
            Check(KernelRuntime.EntityDelete(1,&own),"delete self");Check(PK_ENTITY_delete(1,&native),"delete native");
            Require(KernelRuntime.Curves.AliveCount==0&&KernelRuntime.Surfaces.AliveCount==0&&KernelRuntime.Points.AliveCount==0,"complete self geometry ownership cleanup");
            Console.WriteLine("PASS "+name);
        }
    }
    finally{Check(KernelRuntime.SessionStop(),"self session stop");}
}

if(ParasolidMassProperties.UnresolvedCount!=0)Environment.ExitCode=2;

static unsafe void BoreNative(int body)
{
    var basis=new PK_AXIS2_sf_t{location=new(.3,-1,.35),axis=new(0,1,0),ref_direction=new(1,0,0)};int tool,n;int* bodies;
    Check(PK_BODY_create_solid_cyl(.06,3,&basis,&tool),"author remote bore cutter");
    var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};
    Check(PK_BODY_boolean(body,1,&tool,&options,&n,&bodies),"author remote bore on the selected support");
    try{Require(n==1&&bodies[0]==body,"one bored seed");}finally{Free(false,bodies);}
    CheckBody(body,"bored seed");
}

static unsafe PK_PLANE_sf_t PlaneBasis(int face)
{
    int surface;PK_PLANE_sf_t basis;
    Check(KernelRuntime.FaceAskSurf(face,&surface),"read managed plane support");
    Check(KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&basis),"read managed plane chart");return basis;
}

static unsafe int SeparateSupport(bool own,int edge,bool reversePlaneChart=false)
{
    Span<int> faces=stackalloc int[2];
    if(own)
    {
        var fin=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(edge).Slot].FirstFinEdge;
        for(var i=0;i<2;i++,fin=KernelRuntime.Fins[fin].Other)faces[i]=KernelRuntime.Faces[KernelRuntime.Loops[KernelRuntime.Fins[fin].Loop].Face].Header.Tag;
    }
    else
    {
        int n;int* adjacent;Check(PK_EDGE_ask_faces(edge,&n,&adjacent),"separate support participants");
        try{Require(n==2,"two support participants");new ReadOnlySpan<int>(adjacent,n).CopyTo(faces);}finally{Free(false,adjacent);}
    }
    foreach(var faceTag in faces)
    {
        int surface;byte sense;
        Check(own?KernelRuntime.FaceAskOrientedSurf(faceTag,&surface,&sense):PK_FACE_ask_oriented_surf(faceTag,&surface,(PK_LOGICAL_t*)&sense),"separate support geometry");
        bool matches;
        if(own)matches=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)].Class==(reversePlaneChart?SurfaceClass.Plane:SurfaceClass.BSurface);
        else{int type;Check(PK_ENTITY_ask_class(surface,&type),"support class");matches=type==(reversePlaneChart?PK_CLASS_plane:PK_CLASS_bsurf);}
        if(!matches)continue;
        int copy;
        if(reversePlaneChart)
        {
            PK_PLANE_sf_t basis;Check(own?KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&basis):PK_PLANE_ask(surface,&basis),"read original plane chart");
            for(var axis=0;axis<3;axis++)basis.basis_set.ref_direction.coord[axis]= -basis.basis_set.ref_direction.coord[axis];
            Check(own?KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&basis,&copy):PK_PLANE_create(&basis,&copy),"create equivalent reversed plane chart");
        }
        else Check(own?KernelRuntime.EntityCopy(surface,&copy):PK_ENTITY_copy(surface,&copy),"independent support record copy");
        var face=faceTag;Check(own?KernelRuntime.TopologyDetachGeometry(face):PK_TOPOL_detach_geom(face),"detach support record");
        Check(own?KernelRuntime.FaceAttachSurfaces(1,&face,&copy,&sense):PK_FACE_attach_surfs(1,&face,&copy,(PK_LOGICAL_t*)&sense),"attach equivalent support record");
        Console.WriteLine($"SUPPORT CERTIFICATE own={own} face={face} source={surface} copy={copy} map={(reversePlaneChart?"(-u,-v)":"identity")}");return face;
    }
    throw new Exception("selected edge has no requested support class");
}

static unsafe int[] Select(bool own,int body,bool reflected)
{
    int n;int* edges;Check(own?KernelRuntime.BodyAskEdges(body,&n,&edges):PK_BODY_ask_edges(body,&n,&edges),"select edges");var selected=new List<int>();int* vertices=stackalloc int[2];
    try{for(var i=0;i<n;i++){Check(own?KernelRuntime.EdgeAskVertices(edges[i],vertices):PK_EDGE_ask_vertices(edges[i],vertices),"select vertices");if(vertices[0]==0||vertices[1]==0)continue;var candidate=true;for(var j=0;j<2;j++){int point;PK_POINT_sf_t p;Check(own?KernelRuntime.VertexAskPoint(vertices[j],&point):PK_VERTEX_ask_point(vertices[j],&point),"select point");Check(own?KernelRuntime.PointAsk(point,(M.PK_POINT_sf_s*)&p):PK_POINT_ask(point,&p),"select position");var q=OriginalFrame(p.position,reflected);candidate&=Math.Abs(q.coord[1])<1e-8&&Math.Abs(q.coord[2]-1)<1e-8;}if(candidate)selected.Add(edges[i]);}}finally{Free(own,edges);}return selected.ToArray();
}
static unsafe int Fix(bool own,int body,int[] edges,double radius)
{
    var set=new PK_EDGE_set_blend_constant_o_t();set.properties.tolerance=1e-7;int n;int* marked;fixed(int* input=edges)Check(own?KernelRuntime.EdgeSetBlendConstant(edges.Length,input,radius,(M.PK_EDGE_set_blend_constant_o_s*)&set,&n,&marked):PK_EDGE_set_blend_constant(edges.Length,input,radius,&set,&n,&marked),"mark selected chain");Free(own,marked);
    var fix=new PK_BODY_fix_blends_o_t{tolerance=1e-7,update=PK_blend_edge_update_v261_c,local_check=PK_LOGICAL_true};int fault,fe,ft;int* output;
    WebProgressCapture.Before(own,body,edges);
    WebProgressCapture.BlendParameters(own,fix,radius,set.properties.tolerance);
    PK_FACE_array_t* webUnders=null;int* webTopols=null;
    var started=Stopwatch.GetTimestamp();var error=own?KernelRuntime.BodyFixBlends(body,(M.PK_BODY_fix_blends_o_s*)&fix,&n,&output,WebProgressCapture.Enabled?(M.PK_FACE_array_s**)&webUnders:null,WebProgressCapture.Enabled?&webTopols:null,&fault,&fe,&ft):PK_BODY_fix_blends(body,&fix,&n,&output,WebProgressCapture.Enabled?&webUnders:null,WebProgressCapture.Enabled?&webTopols:null,&fault,&fe,&ft);
    var webMs=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    WebProgressCapture.After(own,body,webMs,error,n,output,fault,webUnders,webTopols);
    WebProgressCapture.FreeBlendTracking(own,n,webUnders,webTopols);
    Console.WriteLine($"TIME FixBlends own={own} seconds={Stopwatch.GetElapsedTime(started).TotalSeconds:F6} error={error} faces={n}");Check(error,"fix support chain");Free(own,output);return n;
}
static unsafe void Transform(bool own,int body)
{
    var f=new M.PK_TRANSF_sf_s();var a=.37;var c=Math.Cos(a)*1.7;var s=Math.Sin(a)*1.7;f.matrix[0]=-c;f.matrix[1]=-s;f.matrix[4]=-s;f.matrix[5]=c;f.matrix[10]=1.7;f.matrix[15]=1;f.matrix[3]=1.3;f.matrix[7]=-.4;f.matrix[11]=.2;int transform;
    Check(own?KernelRuntime.TransfCreate(&f,&transform):PK_TRANSF_create((PK_TRANSF_sf_t*)&f,&transform),"transform");int n;int* replaced;
    if(own){byte* exact;Check(KernelRuntime.BodyTransform(body,transform,0,&n,&replaced,&exact),"transform self");Free(true,replaced);Free(true,exact);}else{PK_LOGICAL_t* exact;Check(PK_BODY_transform(body,transform,0,&n,&replaced,&exact),"transform native");Free(false,replaced);Free(false,exact);}Check(own?KernelRuntime.EntityDelete(1,&transform):PK_ENTITY_delete(1,&transform),"delete transform");
}
static unsafe void Cut(bool own,int target,bool reflected)
{
    var tool=Block(own,1.05,.5,-.1,1,2,2);if(reflected)Transform(own,tool);var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};int n;int* bodies;
    if(own&&reflected)
    {
        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","support-blend-chain-oracle","reflected-tool.x_t"),Transmit(tool));
        DumpPlanes(target,"target");DumpPlanes(tool,"tool");
    }
    var started=Stopwatch.GetTimestamp();Check(own?KernelRuntime.BodyBoolean(target,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&bodies):PK_BODY_boolean(target,1,&tool,&options,&n,&bodies),"cut reloaded chain");
    Console.WriteLine($"TIME Boolean own={own} seconds={Stopwatch.GetElapsedTime(started).TotalSeconds:F6}");
    try{Require(n==1&&bodies[0]==target,"one chain Boolean result");}finally{Free(own,bodies);}
}
static unsafe void DumpPlanes(int body,string label)
{
    int n;int* faces;Check(KernelRuntime.BodyAskFaces(body,&n,&faces),"diagnostic faces");
    try{for(var i=0;i<n;i++){var record=KernelRuntime.Faces[KernelRuntime.ResolveTagRecord(faces[i]).Slot];var support=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(record.SurfTag)];if(support.Class!=SurfaceClass.Plane)continue;var plane=new M.PK_PLANE_sf_s();Check(KernelRuntime.PlaneAsk(record.SurfTag,&plane),"diagnostic plane");Console.WriteLine($"PLANE {label} index={i} surface={record.SurfTag} origin={plane.basis_set.location.coord[0]:R}/{plane.basis_set.location.coord[1]:R}/{plane.basis_set.location.coord[2]:R} normal={plane.basis_set.axis.coord[0]:R}/{plane.basis_set.axis.coord[1]:R}/{plane.basis_set.axis.coord[2]:R}");}}finally{Free(true,faces);}
}
static unsafe PK_VECTOR_t OriginalFrame(PK_VECTOR_t p,bool transformed)
{
    if(!transformed)return p;var c=Math.Cos(.37)*1.7;var s=Math.Sin(.37)*1.7;var a=p.coord[0]-1.3;var b=p.coord[1]+.4;
    return new((-c*a-s*b)/2.89,(-s*a+c*b)/2.89,(p.coord[2]-.2)/1.7);
}

static unsafe int Roof(bool own,bool curvedWall=false)
{
    var body=Block(own,.5,.5,0,1,1,1);int n;int* faces;
    Check(own?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"input faces");var top=0;var wall=0;
    try
    {
        for(var i=0;i<n;i++)
        {
            int surface;Check(own?KernelRuntime.FaceAskSurf(faces[i],&surface):PK_FACE_ask_surf(faces[i],&surface),"face support");PK_PLANE_sf_t plane;
            Check(own?KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&plane):PK_PLANE_ask(surface,&plane),"input plane");
            if(Math.Abs(plane.basis_set.axis.coord[2])>.9&&Math.Abs(plane.basis_set.location.coord[2]-1)<1e-10)top=faces[i];
            if(Math.Abs(plane.basis_set.axis.coord[1])>.9&&Math.Abs(plane.basis_set.location.coord[1])<1e-10)wall=faces[i];
        }
    }
    finally{Free(own,faces);}
    Require(top!=0,"top plane");double* poles=stackalloc double[45];double* knots=stackalloc double[2]{0,1};int* um=stackalloc int[2]{5,5};int* vm=stackalloc int[2]{3,3};
    for(var i=0;i<5;i++)for(var j=0;j<3;j++){var at=3*(i*3+j);poles[at]=i/4.0;poles[at+1]=j/2.0;poles[at+2]=1+(i==2&&j==1?.4:0);}
    var sf=new PK_BSURF_sf_t{u_degree=4,v_degree=2,n_u_vertices=5,n_v_vertices=3,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,
        u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,
        self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};int roof;byte sense=1;
    Check(own?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&roof):PK_BSURF_create(&sf,&roof),"roof surface");
    int previous;Check(own?KernelRuntime.FaceAskSurf(top,&previous):PK_FACE_ask_surf(top,&previous),"old roof support");
    Check(own?KernelRuntime.TopologyDetachGeometry(top):PK_TOPOL_detach_geom(top),"detach top support");
    Check(own?KernelRuntime.FaceAttachSurfaces(1,&top,&roof,&sense):PK_FACE_attach_surfs(1,&top,&roof,(PK_LOGICAL_t*)&sense),"attach freeform roof");
    Check(own?KernelRuntime.EntityDelete(1,&previous):PK_ENTITY_delete(1,&previous),"release detached support");
    if(curvedWall)
    {
        Require(wall!=0,"front plane");
        for(var i=0;i<5;i++)for(var j=0;j<3;j++){var at=3*(i*3+j);poles[at]=i/4.0;poles[at+1]=i==2&&j==1?-.3:0;poles[at+2]=j/2.0;}
        int surface;Check(own?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&surface):PK_BSURF_create(&sf,&surface),"wall surface");
        Check(own?KernelRuntime.FaceAskSurf(wall,&previous):PK_FACE_ask_surf(wall,&previous),"old wall support");
        Check(own?KernelRuntime.TopologyDetachGeometry(wall):PK_TOPOL_detach_geom(wall),"detach wall support");
        Check(own?KernelRuntime.FaceAttachSurfaces(1,&wall,&surface,&sense):PK_FACE_attach_surfs(1,&wall,&surface,(PK_LOGICAL_t*)&sense),"attach freeform wall");
        Check(own?KernelRuntime.EntityDelete(1,&previous):PK_ENTITY_delete(1,&previous),"release detached wall");
    }
    return body;
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

static unsafe void Compare(int reference,int actual,string name)
{
    for(var kind=0;kind<5;kind++){var a=Count(reference,kind);var b=Count(actual,kind);Console.WriteLine($"{name} topology {kind}: native={a} own={b}");Require(a==b,name+" topology count "+kind);}
    var nativeRegions=RegionSignature(reference);var ownRegions=RegionSignature(actual);Console.WriteLine($"{name} material regions: native={nativeRegions}, own={ownRegions}");
    Require(nativeRegions==ownRegions,name+" material and shell semantics");
    ParasolidBodyCompare.AssertEquivalent(reference,actual,name);
    var agreement=ParasolidMassProperties.Compare(reference,actual,name);
    Require(agreement!=MassAgreement.Different,name+" mass intervals prove a difference");
    Console.WriteLine($"GEOMETRY_TOPOLOGY_PASS {name} mass={agreement}");
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

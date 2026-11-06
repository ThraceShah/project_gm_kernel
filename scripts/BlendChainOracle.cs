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
using ProjectGmKernel.Native.Geometry.Evaluation;
using M=ProjectGmKernel.Native.Generated;

var directory=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","blend-chain-oracle");Directory.CreateDirectory(directory);
ModelingPerformance.Enabled=args.Contains("--profile",StringComparer.Ordinal);ModelingPerformance.CacheEnabled=!args.Contains("--no-bound-cache",StringComparer.Ordinal);
JointNestedBlendEvaluation.ReuseSplineContactBounds=!args.Contains("--no-spline-reuse",StringComparer.Ordinal);
var repeat=1;var repeatAt=Array.IndexOf(args,"--repeat");if(repeatAt>=0){Require(repeatAt+1<args.Length&&int.TryParse(args[repeatAt+1],out repeat)&&repeat is >=1 and <=10,"--repeat needs 1..10");}
var names=args.Where(a=>a.StartsWith("chain-",StringComparison.Ordinal)).ToArray();
if(!ParasolidScriptHost.TryStartSession("Independent smooth blend chains",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"self session");
    try
    {
        for(var round=0;round<repeat;round++)foreach(var authored in new[]{true,false})foreach(var segments in args.Contains("nested-support",StringComparer.Ordinal)?new[]{1}:new[]{2,3})foreach(var transformed in new[]{false,true})
        {
            var name=$"chain-{segments}-{(transformed?"reflected":"base")}-{(authored?"self":"pk")}-source";if(names.Length>0&&!names.Contains(name,StringComparer.Ordinal))continue;
            Console.WriteLine($"ROUND {round+1} {name} bound-cache={ModelingPerformance.CacheEnabled} spline-reuse={JointNestedBlendEvaluation.ReuseSplineContactBounds}");
            // Both modifiers start with identical authored finite B-reps.
            // Primitive creators choose different directed LINE carriers;
            // PK transports chain subdivisions through an ordered contact,
            // so merely matching the solids' loci is insufficient input.
            var own=0;var native=0;
            if(authored){own=Roof(true);native=Receive(Transmit(own));}
            else{native=Roof(false);own=ReceiveSelf(TransmitNative(native));}
            var a=SplitChain(true,own,segments);var b=SplitChain(false,native,segments);
            if(transformed){Transform(true,own);Transform(false,native);}
            if(args.Contains("diagnostics",StringComparer.Ordinal)){DumpSource(true,a[0]);DumpSource(false,b[0]);}
            var radius=transformed?.17:.1;FixChain(true,own,a,radius);FixChain(false,native,b,radius);
            var bytes=Transmit(own);File.WriteAllBytes(Path.Combine(directory,name+".x_t"),bytes);var received=Receive(bytes);
            try{CheckBody(native,name+" native");CheckBody(received,name+" received");if(args.Contains("diagnostics",StringComparer.Ordinal)){Console.WriteLine("NATIVE");Dump(native,transformed);Console.WriteLine("SELF");Dump(received,transformed);}Compare(native,received,name);}finally{Check(PK_ENTITY_delete(1,&received),"delete received");}
            if(segments==1||!transformed)
            {
                // Reload before the consumer so finite ranges and shared
                // procedural support ownership participate in the closure.
                var restored=ReceiveSelf(bytes);Check(KernelRuntime.EntityDelete(1,&own),"delete pre-reload chain");own=restored;
                if(segments==1){RefilletNested(true,own,transformed);RefilletNested(false,native,transformed);}else{Cut(true,own);Cut(false,native);}received=Receive(Transmit(own));
                try{CheckBody(native,name+" cut native");CheckBody(received,name+" cut received");Compare(native,received,name+" cut");}finally{Check(PK_ENTITY_delete(1,&received),"delete cut receive");}
            }
            Check(KernelRuntime.EntityDelete(1,&own),"delete self");Check(PK_ENTITY_delete(1,&native),"delete native");
            Require(KernelRuntime.Curves.AliveCount==0&&KernelRuntime.Surfaces.AliveCount==0&&KernelRuntime.Points.AliveCount==0,$"chain ownership cleanup curves={KernelRuntime.Curves.AliveCount}, surfaces={KernelRuntime.Surfaces.AliveCount}, points={KernelRuntime.Points.AliveCount}");
            Console.WriteLine("PASS "+name);
        }
    }
    finally{Check(KernelRuntime.SessionStop(),"self session stop");}
}

static unsafe int[] SplitChain(bool own,int body,int segments)
{
    int n;int* edges;Check(own?KernelRuntime.BodyAskEdges(body,&n,&edges):PK_BODY_ask_edges(body,&n,&edges),"source edges");var selected=0;int* vertices=stackalloc int[2];
    try{for(var i=0;i<n;i++){Check(own?KernelRuntime.EdgeAskVertices(edges[i],vertices):PK_EDGE_ask_vertices(edges[i],vertices),"vertices");var sum=0.0;for(var j=0;j<2;j++){int point;PK_POINT_sf_t p;Check(own?KernelRuntime.VertexAskPoint(vertices[j],&point):PK_VERTEX_ask_point(vertices[j],&point),"point tag");Check(own?KernelRuntime.PointAsk(point,(M.PK_POINT_sf_s*)&p):PK_POINT_ask(point,&p),"point");sum+=Math.Abs(p.position.coord[1])+Math.Abs(p.position.coord[2]-1);}if(sum<1e-10)selected=edges[i];}}
    finally{Free(own,edges);}Require(selected!=0,"selected chain source");var chain=new int[segments];chain[0]=selected;int sourceCurve;
    Check(own?KernelRuntime.EdgeAskCurve(selected,&sourceCurve):PK_EDGE_ask_curve(selected,&sourceCurve),"source curve");
    for(var i=1;i<segments;i++)
    {
        // Primitive kernels may choose opposite natural line parameters.
        // The shared input is this manufactured point; both kernels recover
        // its parameter independently before applying the identical split.
        var fraction=segments==2?.37:i==1?.21:.68;var point=new PK_VECTOR_t(fraction,0,1);double parameter;
        Check(own?KernelRuntime.CurveParameteriseVector(sourceCurve,*(M.PK_VECTOR_s*)&point,&parameter):PK_CURVE_parameterise_vector(sourceCurve,point,&parameter),"split point parameter");var found=-1;
        for(var j=0;j<i;j++){var range=Range(own,chain[j]);if(parameter>range.value[0]&&parameter<range.value[1])found=j;}Require(found>=0,"finite split interval");
        int vertex,edge;Check(own?KernelRuntime.EdgeSplitAtParam(chain[found],parameter,&vertex,&edge):PK_EDGE_split_at_param(chain[found],parameter,&vertex,&edge),"split source edge");chain[i]=edge;
        int geometry;PK_POINT_sf_t definition;Check(own?KernelRuntime.VertexAskPoint(vertex,&geometry):PK_VERTEX_ask_point(vertex,&geometry),"split vertex point");
        Check(own?KernelRuntime.PointAsk(geometry,(M.PK_POINT_sf_s*)&definition):PK_POINT_ask(geometry,&definition),"split vertex definition");Require(Distance(point,definition.position)<1e-8,"shared manufactured split position");
    }
    Array.Reverse(chain);return chain;
}
static unsafe PK_INTERVAL_t Range(bool own,int edge)
{
    PK_INTERVAL_t range;if(!own){Check(PK_EDGE_find_interval(edge,&range),"native range");return range;}
    var e=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(edge).Slot];if(e.HasParameterRange!=0){range.value[0]=e.ParameterStart;range.value[1]=e.ParameterEnd;return range;}
    int* vertices=stackalloc int[2];Check(KernelRuntime.EdgeAskVertices(edge,vertices),"range vertices");
    for(var j=0;j<2;j++){int point;M.PK_POINT_sf_s p;double t;Check(KernelRuntime.VertexAskPoint(vertices[j],&point),"range point");Check(KernelRuntime.PointAsk(point,&p),"range point data");Check(KernelRuntime.CurveParameteriseVector(e.CurveTag,p.position,&t),"source range");range.value[j]=t;}
    if(range.value[1]<range.value[0]){var t=range.value[0];range.value[0]=range.value[1];range.value[1]=t;}return range;
}
static unsafe void FixChain(bool own,int body,int[] edges,double radius)
{
    var set=new PK_EDGE_set_blend_constant_o_t();set.properties.tolerance=1e-7;int n;int* marked;fixed(int* input=edges)Check(own?KernelRuntime.EdgeSetBlendConstant(edges.Length,input,radius,(M.PK_EDGE_set_blend_constant_o_s*)&set,&n,&marked):PK_EDGE_set_blend_constant(edges.Length,input,radius,&set,&n,&marked),"mark chain");Free(own,marked);
    var fix=new PK_BODY_fix_blends_o_t{tolerance=1e-7,update=PK_blend_edge_update_v261_c,local_check=PK_LOGICAL_true};int fault,fe,ft;int* output;
    var started=Stopwatch.GetTimestamp();var error=own?KernelRuntime.BodyFixBlends(body,(M.PK_BODY_fix_blends_o_s*)&fix,&n,&output,null,null,&fault,&fe,&ft):PK_BODY_fix_blends(body,&fix,&n,&output,null,null,&fault,&fe,&ft);
    Console.WriteLine($"TIME FixBlends own={own} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}");
    if(own&&ModelingPerformance.Enabled)PrintProfile("FixBlends");
    if(own&&error!=0&&KernelRuntime.IsValidTag(KernelRuntime.LastRegularBlendDomainFace))
    {
        var face=KernelRuntime.Faces[KernelRuntime.ResolveTagRecord(KernelRuntime.LastRegularBlendDomainFace).Slot];var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(face.SurfTag)];Console.WriteLine($"obstacle class={surface.Class}, orientation={face.Orientation}, surfaceSense={surface.Sense}");
        if(surface.Class==SurfaceClass.Plane){var p=new M.PK_PLANE_sf_s();Check(KernelRuntime.PlaneAsk(surface.Header.Tag,&p),"obstacle plane");Console.WriteLine($"obstacle origin={p.basis_set.location.coord[0]:R},{p.basis_set.location.coord[1]:R},{p.basis_set.location.coord[2]:R} axis={p.basis_set.axis.coord[0]:R},{p.basis_set.axis.coord[1]:R},{p.basis_set.axis.coord[2]:R}");}
    }
    Console.WriteLine($"fix own={own} error={error} stage={KernelRuntime.LastRegularBlendStage} rolling={KernelRuntime.LastRollingBuildStage} domain={KernelRuntime.LastRegularBlendDomainStage}/{KernelRuntime.LastRegularBlendDomainFace}/{KernelRuntime.LastRegularBlendDomainCells}/{KernelRuntime.LastRegularBlendDomainStatus} classification={KernelRuntime.LastRegularClassificationStage}/{KernelRuntime.LastRegularClassificationEntity} sheet={KernelRuntime.LastSheetPartitionStage}");Check(error,"fix chain");try{Require(n==edges.Length,"one face per prescribed chain segment");}finally{Free(own,output);}
}
static unsafe void Transform(bool own,int body)
{
    var f=new M.PK_TRANSF_sf_s();var a=.37;var c=Math.Cos(a)*1.7;var s=Math.Sin(a)*1.7;f.matrix[0]=-c;f.matrix[1]=-s;f.matrix[4]=-s;f.matrix[5]=c;f.matrix[10]=1.7;f.matrix[15]=1;f.matrix[3]=1.3;f.matrix[7]=-.4;f.matrix[11]=.2;int transform;
    Check(own?KernelRuntime.TransfCreate(&f,&transform):PK_TRANSF_create((PK_TRANSF_sf_t*)&f,&transform),"transform");int n;int* replaced;
    if(own){byte* exact;Check(KernelRuntime.BodyTransform(body,transform,0,&n,&replaced,&exact),"transform self");Free(true,replaced);Free(true,exact);}else{PK_LOGICAL_t* exact;Check(PK_BODY_transform(body,transform,0,&n,&replaced,&exact),"transform native");Free(false,replaced);Free(false,exact);}Check(own?KernelRuntime.EntityDelete(1,&transform):PK_ENTITY_delete(1,&transform),"delete transform");
}
static unsafe void Cut(bool own,int target)
{
    var tool=Block(own,1.05,.5,-.1,1,2,2);var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};int n;int* bodies;
    var started=Stopwatch.GetTimestamp();Check(own?KernelRuntime.BodyBoolean(target,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&bodies):PK_BODY_boolean(target,1,&tool,&options,&n,&bodies),"cut reloaded chain");
    Console.WriteLine($"TIME Boolean own={own} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}");
    if(own&&ModelingPerformance.Enabled)PrintProfile("Boolean");
    try{Require(n==1&&bodies[0]==target,"one chain Boolean result");}finally{Free(own,bodies);}
}
static void PrintProfile(string operation)
{
    var p=ModelingPerformance.Last;var milliseconds=1000.0/Stopwatch.Frequency;
    Console.WriteLine($"PROFILE operation={operation} elapsed-ms={p.ElapsedTicks*milliseconds:F3}, pairs={p.SurfacePairs}, ssi-cells={p.SsiCells}, root-cells={p.RootCells}, roots={p.Roots}, budget-failures={p.BudgetFailures}, winding={p.WindingCalls}, bound-requests={p.BoundRequests}, bound-evaluations={p.BoundEvaluations}, cache-hits={p.BoundCacheHits}, bound-ms={p.BoundTicks*milliseconds:F3}");
}
static unsafe void RefilletNested(bool own,int target,bool transformed)
{
    var tool=Block(own,.5,-.46,.7,2,1,1);if(transformed)Transform(own,tool);var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};int n;int* bodies;
    Check(own?KernelRuntime.BodyBoolean(target,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&bodies):PK_BODY_boolean(target,1,&tool,&options,&n,&bodies),"cut through existing Blend");
    try{Require(n==1&&bodies[0]==target,"nested support one cut result");}finally{Free(own,bodies);}
    int* edges;Check(own?KernelRuntime.BodyAskEdges(target,&n,&edges):PK_BODY_ask_edges(target,&n,&edges),"nested support edges");var selected=0;int* vertices=stackalloc int[2];
    try{for(var i=0;i<n;i++){Check(own?KernelRuntime.EdgeAskVertices(edges[i],vertices):PK_EDGE_ask_vertices(edges[i],vertices),"nested edge vertices");var candidate=true;for(var j=0;j<2;j++){int point;PK_POINT_sf_t p;Check(own?KernelRuntime.VertexAskPoint(vertices[j],&point):PK_VERTEX_ask_point(vertices[j],&point),"nested vertex");Check(own?KernelRuntime.PointAsk(point,(M.PK_POINT_sf_s*)&p):PK_POINT_ask(point,&p),"nested point");var q=OriginalFrame(p.position,transformed);candidate&=Math.Abs(q.coord[1]-.04)<1e-8&&q.coord[2]>.9;}if(candidate){Require(selected==0,"unique nested edge");selected=edges[i];}}}
    finally{Free(own,edges);}Require(selected!=0,"Blend/Plane new edge");FixChain(own,target,new[]{selected},transformed?.017:.01);
}
static unsafe PK_VECTOR_t OriginalFrame(PK_VECTOR_t p,bool transformed)
{
    if(!transformed)return p;var c=Math.Cos(.37)*1.7;var s=Math.Sin(.37)*1.7;var a=p.coord[0]-1.3;var b=p.coord[1]+.4;
    return new((-c*a-s*b)/2.89,(-s*a+c*b)/2.89,(p.coord[2]-.2)/1.7);
}

static unsafe void Dump(int body,bool transformed)
{
    int n;int* tags;Check(PK_BODY_ask_faces(body,&n,&tags),"debug faces");
    try{for(var i=0;i<n;i++){int s,t;Check(PK_FACE_ask_surf(tags[i],&s),"debug surf");Check(PK_ENTITY_ask_class(s,&t),"debug class");if(t==PK_CLASS_blendsf){PK_BLENDSF_sf_t b;Check(PK_BLENDSF_ask(s,&b),"debug blend");int a,c;Check(PK_ENTITY_ask_class(b.geom_1,&a),"first");Check(PK_ENTITY_ask_class(b.geom_2,&c),"second");Console.WriteLine($"blend supports={a}/{c}");}}}finally{Free(false,tags);}
    Check(PK_BODY_ask_edges(body,&n,&tags),"debug edges");PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
    try{for(var i=0;i<n;i++){int curve,t;PK_INTERVAL_t range;PK_LOGICAL_t sense;Check(PK_EDGE_ask_geometry(tags[i],PK_LOGICAL_true,&curve,&t,ends,&range,&sense),"debug edge");if(t!=PK_CLASS_circle)continue;PK_CIRCLE_sf_t circle;Check(PK_CIRCLE_ask(curve,&circle),"debug circle");var p=circle.basis_set.location;Write("center",p);Write("first",ends[0]);Write("second",ends[1]);}}
    finally{Free(false,tags);}
    void Write(string name,PK_VECTOR_t p){var x=p.coord[0];var y=p.coord[1];var z=p.coord[2];if(transformed){var c=Math.Cos(.37)*1.7;var s=Math.Sin(.37)*1.7;var a=x-1.3;var b=y+.4;x=(-c*a-s*b)/2.89;y=(-s*a+c*b)/2.89;z=(z-.2)/1.7;}Console.WriteLine($"{name}=({x:R},{y:R},{z:R})");}
}
static unsafe void DumpSource(bool own,int edge)
{
    Console.WriteLine($"SOURCE own={own}, edge={edge}");
    if(own)
    {
        var e=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(edge).Slot];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(e.CurveTag)];
        Console.WriteLine($"curve sense={curve.Sense}");var fin=e.FirstFinEdge;
        for(var i=0;i<2;i++,fin=KernelRuntime.Fins[fin].Other){var face=KernelRuntime.Loops[KernelRuntime.Fins[fin].Loop].Face;var s=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag)];Console.WriteLine($"fin positive={KernelRuntime.Fins[fin].Sense}, surface={s.Class}");}
    }
    else
    {
        int n;int* fins;Check(PK_EDGE_ask_fins(edge,&n,&fins),"debug fins");
        try{for(var i=0;i<n;i++){int face,s,type;PK_LOGICAL_t positive;Check(PK_FIN_ask_face(fins[i],&face),"debug fin face");Check(PK_FACE_ask_surf(face,&s),"debug fin support");Check(PK_ENTITY_ask_class(s,&type),"debug support class");Check(PK_FIN_is_positive(fins[i],&positive),"debug fin sense");Console.WriteLine($"fin positive={positive==PK_LOGICAL_true}, surface={type}");}}
        finally{Free(false,fins);}
    }
}
static unsafe int Roof(bool own)
{
    var body=Block(own,.5,.5,0,1,1,1);int n;int* faces;
    Check(own?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"input faces");var top=0;
    try
    {
        for(var i=0;i<n;i++)
        {
            int surface;Check(own?KernelRuntime.FaceAskSurf(faces[i],&surface):PK_FACE_ask_surf(faces[i],&surface),"face support");PK_PLANE_sf_t plane;
            Check(own?KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&plane):PK_PLANE_ask(surface,&plane),"input plane");
            if(Math.Abs(plane.basis_set.axis.coord[2])>.9&&Math.Abs(plane.basis_set.location.coord[2]-1)<1e-10)top=faces[i];
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

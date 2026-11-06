#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=CurvedSheetPartitionOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

var directory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "curved-sheet-partition-oracle"));
Directory.CreateDirectory(directory); var report = new StringBuilder();
void Log(string message) { Console.WriteLine(message); report.AppendLine(message); }
try
{
    if (!ParasolidScriptHost.TryStartSession("Independent curved sheet partition oracle", out var host, out var message)) throw new Exception(message);
    using (host)
    unsafe
    {
        var start = new M.PK_SESSION_start_o_s { o_t_version = 1 }; Check(KernelRuntime.SessionStart(&start), "self start");
        try
        {
            var failures = 0; var passed = 0;
            foreach (var kind in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })
            foreach (var operation in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })
            {
                if(kind<8&&operation>=6)continue;
                if(kind==10&&operation>8)continue;
                var name = (kind==10?"open-nurbs-blend":kind>=8?(kind==9?"rational":"polynomial")+"-closed-loop":kind == 6 ? "isoparametric-line" : kind == 7 ? "tensor-isoparametric" : (kind % 2 == 1 ? "rational" : "polynomial") + (kind < 2 ? "-bilinear" : kind <4 ? "-cubic" : "-multispan-cubic"))
                    + "-" + new[] { "imprint", "trim-keep", "trim-remove", "section-front", "section-back", "section-both", "second-plane", "repeat-plane", "annulus-front", "annulus-back", "annulus-both" }[operation];
                if(kind==10&&operation>=7)name="open-nurbs-blend-"+(operation==7?"trim-second-plane":"repeat-second-plane");
                if(args.Length!=0&&!name.Contains(args[0],StringComparison.Ordinal))continue;
                try
                {
                    int[] ours,reference;
                    if(kind==10)(ours,reference)=ProduceBlendPair(operation);
                    else {ours=Produce(true,kind,operation);reference=Produce(false,kind,operation);}
                    Require(ours.Length == reference.Length, name + " result count");
                    for(var j=0;j<reference.Length;j++)File.WriteAllBytes(Path.Combine(directory,name+"-"+j+"-reference.x_t"),TransmitReference(reference[j]));
                    var matched = new bool[reference.Length];
                    for (var i = 0; i < ours.Length; i++)
                    {
                        var bytes = Transmit(ours[i]); File.WriteAllBytes(Path.Combine(directory, name + "-" + i + "-self.x_t"), bytes);
                        var received = Receive(bytes); CheckBody(received, name, Log);
                        var candidate = -1; var a = VertexCenter(received);
                        for (var j = 0; j < reference.Length; j++) if (!matched[j])
                        {
                            var b = VertexCenter(reference[j]);
                            var same = true; for (var d = 0; d < 3; d++) same &= Math.Abs(a.coord[d] - b.coord[d]) < 1e-6;
                            if (same) { candidate = j; break; }
                        }
                        Require(candidate >= 0, name + " body matching"); matched[candidate] = true;
                        CheckBody(reference[candidate], name + " reference", Log); Compare(reference[candidate], received, name, Log);
                    }
                    passed++; Log("PASS " + name);
                }
                catch (Exception ex) { failures++; Log("FAIL " + name + ": " + ex.Message); }
            }
            Require(failures == 0, $"{failures} curved sheet oracle cases failed"); Log($"PASS all {passed} curved sheet cases");
        }
        finally { Check(KernelRuntime.SessionStop(), "self stop"); }
    }
}
catch (Exception ex) { Log("FAIL: " + ex.Message); Environment.ExitCode = 1; }
finally { File.WriteAllText(Path.Combine(directory, "report.txt"), report.ToString()); }

static unsafe int[] Produce(bool self, int kind, int operation)
{
    // The homogeneous bilinear patch has a curved hyperbola intersection and
    // exact rational support. Each kernel constructs its own inputs.
    var rational = kind < 6 && kind % 2 == 1||kind==9; var nu = kind == 6 ? 2 : kind >= 7 ? 3 : kind < 2 ? 2 : kind < 4 ? 4 : 5;
    var nv = kind >= 7 ? 3 : 2;
    double* poles = stackalloc double[56]; var dim = rational ? 4 : 3;
    double* heights = stackalloc double[7] { -.08, .14, -.14, .08, -.14, .14, -.08 };
    if (kind >= 4) { heights[2] = heights[3] = .14; heights[4] = -.08; }
    for (var u = 0; u < nu; u++) for (var v = 0; v < nv; v++)
    {
        var i = (u * nv + v) * dim; var w = rational ? 1 + .25 * u + .5 * v : 1;
        poles[i] = (kind is < 2 or 6 ? u : kind >= 7 ? u * .5 : u / 3.0) * w; poles[i + 1] = (kind >= 7 ? v * .5 : v) * w;
        poles[i + 2] = kind>=8?((u==1?-.25:.25)+(v==1?-.25:.25)-.0625)*w:kind is < 2 or 6 ? u * v * w : kind == 7 ? (u == 2 ? 1 : 0) + (v == 2 ? 1 : 0) : heights[u] + v * w; if (rational) poles[i + 3] = w;
    }
    double* knots = stackalloc double[2] { 0, 1 }; int* mult = stackalloc int[2] { 2, 2 };
    double* uk = stackalloc double[3] { 0, 1, 2 }; int* um = stackalloc int[3] { 4, kind < 4 ? 4 : 1, 4 };
    var definition = new PK_BSURF_sf_t { u_degree = kind is < 2 or 6 ? 1 : kind >= 7 ? 2 : 3, v_degree = kind >= 7 ? 2 : 1, n_u_vertices = nu, n_v_vertices = nv, vertex_dim = dim,
        is_rational = rational ? PK_LOGICAL_true : PK_LOGICAL_false, vertex = poles, n_u_knots = 2, n_v_knots = 2,
        u_knot = knots, v_knot = knots, u_knot_mult = mult, v_knot_mult = mult,
        form = PK_BSURF_form_arbitrary_c, u_knot_type = PK_knot_non_uniform_c, v_knot_type = PK_knot_non_uniform_c,
        self_intersecting = PK_self_intersect_false_c, convexity = PK_convexity_unset_c };
    if (kind is >= 2 and < 6) { definition.u_knot = uk; definition.u_knot_mult = um; definition.n_u_knots = kind < 4 ? 2 : 3; }
    if (kind >= 7) { mult[0] = mult[1] = 3; }
    int surface = 0; Check(self ? KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&definition, &surface) : PK_BSURF_create(&definition, &surface), "create NURBS");
    var box = new PK_UVBOX_t(); box.param[2] = kind is 4 or 5 ? 2 : 1; box.param[3] = 1; int body = 0;
    Check(self ? KernelRuntime.SurfMakeSheetBody(surface, *(M.PK_UVBOX_s*)&box, &body) : PK_SURF_make_sheet_body(surface, box, &body), "make sheet");
    var planeDefinition = new PK_PLANE_sf_t(); planeDefinition.basis_set.location = new(0, 0, kind < 2 ? .25 : 0);
    planeDefinition.basis_set.axis = new(0, 0, 1); planeDefinition.basis_set.ref_direction = new(1, 0, 0);
    if (kind is 6 or 7) { planeDefinition.basis_set.location = new(.5, 0, 0); planeDefinition.basis_set.axis = new(1, 0, 0); planeDefinition.basis_set.ref_direction = new(0, 1, 0); }
    int plane = 0; Check(self ? KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&planeDefinition, &plane) : PK_PLANE_create(&planeDefinition, &plane), "create plane");
    if(operation>=6)
    {
        int n=0;int* barriers=null;
        Check(self?KernelRuntime.BodyImprintPlane(body,plane,1e-8,&n,&barriers):PK_BODY_imprint_plane(body,plane,1e-8,&n,&barriers),"first closed imprint");
        try
        {
            Require(n==1,"closed barrier count");
            if(operation>=8)
            {
                var inner=InnerFace(self,body);
                Check(self?KernelRuntime.BodyTrim(body,n,barriers,1,&inner,PK_LOGICAL_false):PK_BODY_trim(body,n,barriers,1,&inner,PK_LOGICAL_false),"annulus trim");
            }
        }
        finally{Check(self?KernelRuntime.MemoryFree(barriers):PK_MEMORY_free(barriers),"free barrier");}
        planeDefinition.basis_set.location=new(.5,0,0);planeDefinition.basis_set.axis=new(1,0,0);planeDefinition.basis_set.ref_direction=new(0,1,0);
        Check(self?KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&planeDefinition,&plane):PK_PLANE_create(&planeDefinition,&plane),"second plane");
        if(operation is 6 or 7)
        {
            for(var repeat=0;repeat<(operation==7?2:1);repeat++)
            {
                n=0;barriers=null;
                Check(self?KernelRuntime.BodyImprintPlane(body,plane,1e-8,&n,&barriers):PK_BODY_imprint_plane(body,plane,1e-8,&n,&barriers),"second/repeated imprint");
                try{Require(n==3,"all second-plane edges");}finally{Check(self?KernelRuntime.MemoryFree(barriers):PK_MEMORY_free(barriers),"free second edges");}
            }
            return [body];
        }
        operation-=5;
    }
    if (operation >= 3)
    {
        var options = new PK_BODY_section_o_t { fence = operation == 3 ? PK_section_fence_front_c : operation == 4 ? PK_section_fence_back_c : PK_section_fence_both_c, default_tol = 1e-8 };
        PK_section_r_t result = default;
        Check(self ? KernelRuntime.BodySectionWithSurf(body, plane, (M.PK_BODY_section_o_s*)&options, (M.PK_section_r_s*)&result)
            : PK_BODY_section_with_surf(body, plane, &options, &result), "section");
        try
        {
            Require(result.front_faces.length == 0 && result.back_faces.length == 0, "sheet section has no caps");
            var output = new int[result.front_bodies.length + result.back_bodies.length];
            for (var i = 0; i < result.front_bodies.length; i++) output[i] = result.front_bodies.array[i];
            for (var i = 0; i < result.back_bodies.length; i++) output[result.front_bodies.length + i] = result.back_bodies.array[i];
            return output;
        }
        finally { Check(self ? KernelRuntime.SectionResultFree((M.PK_section_r_s*)&result) : PK_section_r_f(&result), "free section"); }
    }
    int count = 0; int* edges = null;
    Check(self ? KernelRuntime.BodyImprintPlane(body, plane, 1e-8, &count, &edges) : PK_BODY_imprint_plane(body, plane, 1e-8, &count, &edges), "imprint");
    try
    {
        Require(count == (kind < 2 || kind >= 6 ? 1 : 2), "complete exact cuts");
        if (operation != 0)
        {
            int nf = 0; int* faces = null; Check(self ? KernelRuntime.BodyAskFaces(body, &nf, &faces) : PK_BODY_ask_faces(body, &nf, &faces), "faces");
            int seed = 0;
            try
            {
                for (var i = 0; i < nf; i++)
                {
                    int ne = 0; int* boundary = null; Check(self ? KernelRuntime.FaceAskEdges(faces[i], &ne, &boundary) : PK_FACE_ask_edges(faces[i], &ne, &boundary), "face edges");
                    if (ne == (kind>=8?1:kind >= 6 ? 4 : 3))
                    {
                        if (kind < 6||kind>=8) seed = faces[i];
                        else
                        {
                            int* vertices = stackalloc int[2]; var high = false;
                            for (var k = 0; k < ne; k++)
                            {
                                Check(self ? KernelRuntime.EdgeAskVertices(boundary[k], vertices) : PK_EDGE_ask_vertices(boundary[k], vertices), "edge vertices");
                                int point = 0; Check(self ? KernelRuntime.VertexAskPoint(vertices[0], &point) : PK_VERTEX_ask_point(vertices[0], &point), "point tag");
                                PK_POINT_sf_t sf; Check(self ? KernelRuntime.PointAsk(point, (M.PK_POINT_sf_s*)&sf) : PK_POINT_ask(point, &sf), "seed point"); high |= sf.position.coord[0] > .75;
                            }
                            if (high) seed = faces[i];
                        }
                    }
                    Check(self ? KernelRuntime.MemoryFree(boundary) : PK_MEMORY_free(boundary), "free face edges");
                }
            }
            finally { Check(self ? KernelRuntime.MemoryFree(faces) : PK_MEMORY_free(faces), "free faces"); }
            Require(seed != 0, "front seed");
            var keep = operation == 1 ? PK_LOGICAL_true : PK_LOGICAL_false;
            Check(self ? KernelRuntime.BodyTrim(body, count, edges, 1, &seed, keep) : PK_BODY_trim(body, count, edges, 1, &seed, keep), "trim");
        }
        return [body];
    }
    finally { Check(self ? KernelRuntime.MemoryFree(edges) : PK_MEMORY_free(edges), "free imprint"); }
}

static unsafe (int[],int[]) ProduceBlendPair(int operation)
{
    double* poles=stackalloc double[18];for(var i=0;i<3;i++)for(var j=0;j<2;j++){var at=3*(2*i+j);poles[at]=i-1;poles[at+1]=2*j-1;poles[at+2]=(i==1?-.3:.3)+.1*(i-1)*(2*j-1);}
    double* knots=stackalloc double[2]{-1,1};int* um=stackalloc int[2]{3,3};int* vm=stackalloc int[2]{2,2};
    var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=1,n_u_vertices=3,n_v_vertices=2,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
    int ownSource,nativeSource;Check(KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&ownSource),"own Blend support");Check(PK_BSURF_create(&sf,&nativeSource),"native Blend support");
    var wall=new PK_PLANE_sf_t();wall.basis_set.axis=new(0,1,0);wall.basis_set.ref_direction=new(1,0,0);int ownWall,nativeWall;
    Check(KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&wall,&ownWall),"own Blend wall");Check(PK_PLANE_create(&wall,&nativeWall),"native Blend wall");
    var options=new PK_SURF_create_blend_o_t();var a=new PK_VECTOR_t(.5,0,.075);var b=new PK_VECTOR_t(-.5,0,.075);int own,native;
    Check(KernelRuntime.SurfCreateBlend(ownSource,.1,ownWall,.1,*(M.PK_VECTOR_s*)&a,*(M.PK_VECTOR_s*)&b,(M.PK_SURF_create_blend_o_s*)&options,&own),"own independent Blend");
    Check(PK_SURF_create_blend(nativeSource,.1,nativeWall,.1,a,b,&options,&native),"native independent Blend");
    var ownDefinition=new M.PK_BLENDSF_sf_s();var nativeDefinition=new PK_BLENDSF_sf_t();
    Check(KernelRuntime.BlendSfAsk(own,&ownDefinition),"own spine");Check(PK_BLENDSF_ask(native,&nativeDefinition),"native spine");
    var ownBox=new M.PK_UVBOX_s();Check(KernelRuntime.SurfAskUvbox(own,&ownBox),"own Blend domain");
    var lower=ownBox.param[0];var width=ownBox.param[2]-lower;ownBox.param[0]=lower+.4*width;ownBox.param[2]=lower+.6*width;ownBox.param[1]=.15;ownBox.param[3]=.85;
    var nativeBox=new PK_UVBOX_t();nativeBox.param[1]=.15;nativeBox.param[3]=.85;
    for(var end=0;end<2;end++)
    {
        var p=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(ownDefinition.spine,ownBox.param[end==0?0:2],0,&p),"own endpoint spine");double t;
        Check(PK_CURVE_parameterise_vector(nativeDefinition.spine,*(PK_VECTOR_t*)&p,&t),"native endpoint correspondence");nativeBox.param[end==0?0:2]=t;
    }
    if(nativeBox.param[0]>nativeBox.param[2])(nativeBox.param[0],nativeBox.param[2])=(nativeBox.param[2],nativeBox.param[0]);
    int ownBody,nativeBody;Check(KernelRuntime.SurfMakeSheetBody(own,ownBox,&ownBody),"own Blend sheet");Check(PK_SURF_make_sheet_body(native,nativeBox,&nativeBody),"native Blend sheet");
    Check(KernelRuntime.EntityDelete(1,&own),"own source deletion");
    var uv=new M.PK_UV_s();uv.param[0]=(ownBox.param[0]+ownBox.param[2])*.5;uv.param[1]=.5;var point=new M.PK_VECTOR_s();
    Require(KernelRuntime.TryResolveBodySlot(ownBody,out var ownSlot),"own sheet slot");
    Check(KernelRuntime.SurfEval(KernelRuntime.Faces[KernelRuntime.Bodies[ownSlot].FirstFaceBody].SurfTag,uv,0,0,0,&point),"Blend cutter witness");
    var first=new PK_PLANE_sf_t();first.basis_set.location=*(PK_VECTOR_t*)&point;first.basis_set.axis=new(1,0,0);first.basis_set.ref_direction=new(0,1,0);
    var second=first;second.basis_set.axis=new(0,1,0);second.basis_set.ref_direction=new(1,0,0);
    return (BlendOperations(true,ownBody,first,second,operation),BlendOperations(false,nativeBody,first,second,operation));
}

static unsafe int[] BlendOperations(bool self,int body,PK_PLANE_sf_t first,PK_PLANE_sf_t second,int operation)
{
    int plane;Check(self?KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&first,&plane):PK_PLANE_create(&first,&plane),"Blend first plane");
    if(operation is >=3 and <=5)
    {
        var options=new PK_BODY_section_o_t{fence=operation==3?PK_section_fence_front_c:operation==4?PK_section_fence_back_c:PK_section_fence_both_c,default_tol=1e-8};PK_section_r_t result;
        Check(self?KernelRuntime.BodySectionWithSurf(body,plane,(M.PK_BODY_section_o_s*)&options,(M.PK_section_r_s*)&result):PK_BODY_section_with_surf(body,plane,&options,&result),"Blend section");
        try
        {
            var bodies=new int[result.front_bodies.length+result.back_bodies.length];
            for(var i=0;i<result.front_bodies.length;i++)bodies[i]=result.front_bodies.array[i];for(var i=0;i<result.back_bodies.length;i++)bodies[result.front_bodies.length+i]=result.back_bodies.array[i];return bodies;
        }
        finally{Check(self?KernelRuntime.SectionResultFree((M.PK_section_r_s*)&result):PK_section_r_f(&result),"Blend section buffers");}
    }
    int n;int* edges;Check(self?KernelRuntime.BodyImprintPlane(body,plane,1e-8,&n,&edges):PK_BODY_imprint_plane(body,plane,1e-8,&n,&edges),"Blend first imprint");
    Console.WriteLine($"Blend operation={operation} self={self} first imprint complete");
    try
    {
        Require(n==1,"Blend first barrier");
        if(operation is 1 or 2 or 7)
        {
            int nf;int* faces;Check(self?KernelRuntime.BodyAskFaces(body,&nf,&faces):PK_BODY_ask_faces(body,&nf,&faces),"Blend seed faces");int seed=0;
            try
            {
                int* vertices=stackalloc int[2];
                for(var f=0;f<nf;f++)
                {
                    int ne;int* boundary;Check(self?KernelRuntime.FaceAskEdges(faces[f],&ne,&boundary):PK_FACE_ask_edges(faces[f],&ne,&boundary),"Blend face edges");
                    try
                    {
                        for(var e=0;e<ne;e++)
                        {
                            Check(self?KernelRuntime.EdgeAskVertices(boundary[e],vertices):PK_EDGE_ask_vertices(boundary[e],vertices),"Blend edge vertices");
                            for(var v=0;v<2;v++)
                            {
                                int tag;PK_POINT_sf_t p;Check(self?KernelRuntime.VertexAskPoint(vertices[v],&tag):PK_VERTEX_ask_point(vertices[v],&tag),"Blend seed point");
                                Check(self?KernelRuntime.PointAsk(tag,(M.PK_POINT_sf_s*)&p):PK_POINT_ask(tag,&p),"Blend seed position");if(p.position.coord[0]>first.basis_set.location.coord[0]+.01)seed=faces[f];
                            }
                        }
                    }
                    finally{Check(self?KernelRuntime.MemoryFree(boundary):PK_MEMORY_free(boundary),"Blend boundary buffer");}
                }
            }
            finally{Check(self?KernelRuntime.MemoryFree(faces):PK_MEMORY_free(faces),"Blend face buffer");}
            Require(seed!=0,"Blend seed found");Check(self?KernelRuntime.BodyTrim(body,n,edges,1,&seed,operation==2?PK_LOGICAL_false:PK_LOGICAL_true):PK_BODY_trim(body,n,edges,1,&seed,operation==2?PK_LOGICAL_false:PK_LOGICAL_true),"Blend trim");
        }
    }
    finally{Check(self?KernelRuntime.MemoryFree(edges):PK_MEMORY_free(edges),"Blend first barriers");}
    if(operation>=6)
    {
        Check(self?KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&second,&plane):PK_PLANE_create(&second,&plane),"Blend second plane");
        for(var repeat=0;repeat<(operation==8?2:1);repeat++)
        {
            Console.WriteLine($"Blend operation={operation} self={self} second imprint start repeat={repeat}");
            Check(self?KernelRuntime.BodyImprintPlane(body,plane,1e-8,&n,&edges):PK_BODY_imprint_plane(body,plane,1e-8,&n,&edges),"Blend second imprint");
            try{Require(n==(operation==7?1:2),"Blend second barriers");}finally{Check(self?KernelRuntime.MemoryFree(edges):PK_MEMORY_free(edges),"Blend second barriers buffer");}
        }
    }
    return [body];
}

static unsafe byte[] Transmit(int body)
{
    var options = new M.PK_PART_transmit_o_s { o_t_version = 4, transmit_format = PK_transmit_format_text_c, transmit_version = 371, transmit_meshes = PK_transmit_meshes_separate_c };
    M.PK_MEMORY_block_s block; Check(KernelRuntime.PartTransmitB(1, &body, &options, &block), "self text transmit");
    try { using var stream = new MemoryStream(); for (var b = &block; b != null; b = b->next) stream.Write(new ReadOnlySpan<byte>(b->bytes, checked((int)b->n_bytes))); return stream.ToArray(); }
    finally { Check(KernelRuntime.MemoryBlockFree(&block), "free text"); }
}
static unsafe byte[] TransmitReference(int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1,&body,&options,&block),"reference text transmit");
    try{using var stream=new MemoryStream();for(var b=&block;b!=null;b=b->next)stream.Write(new ReadOnlySpan<byte>(b->bytes,checked((int)b->n_bytes)));return stream.ToArray();}
    finally{Check(PK_MEMORY_block_f(&block),"free reference text");}
}
static unsafe int Receive(byte[] bytes)
{
    fixed (byte* p = bytes)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, p); var options = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int n = 0; int* parts = null; Check(PK_PART_receive_b(block, &options, &n, &parts), "real PK receive");
        try { Require(n == 1, "one received body"); return parts[0]; } finally { if (parts != null) PK_MEMORY_free(parts); }
    }
}
static unsafe void CheckBody(int body, string name, Action<string> log)
{
    int type = 0; Check(PK_BODY_ask_type(body, &type), "body type"); Require(type == PK_BODY_type_sheet_c, "sheet body type");
    int nr = 0; int* regions = null; Check(PK_BODY_ask_regions(body, &nr, &regions), "regions");
    try
    {
        for (var i = 0; i < nr; i++) { var solid = PK_LOGICAL_false; Check(PK_REGION_is_solid(regions[i], &solid), "region material"); Require(solid == PK_LOGICAL_false, "sheet void region"); }
    }
    finally { if (regions != null) PK_MEMORY_free(regions); }
    var options = new PK_BODY_check_o_t { max_faults = 100 }; int n = 0; PK_check_fault_t* faults = null;
    Check(PK_BODY_check(body, &options, &n, &faults), "BODY_check");
    try
    {
        for (var i = 0; i < n; i++)
        {
            log($"{name} fault={faults[i].state}, entity={faults[i].entity_1}/{faults[i].entity_2}");
            if(faults[i].state==PK_EDGE_state_bad_vertex_c)
            {
                int curve=0,point=0;PK_POINT_sf_t position;double t=0;PK_VECTOR_t evaluated;PK_INTERVAL_t range;
                var ec=PK_EDGE_ask_curve(faults[i].entity_1,&curve);var ep=PK_VERTEX_ask_point(faults[i].entity_2,&point);
                if(ec==0&&ep==0&&PK_POINT_ask(point,&position)==0)
                {
                    var inverse=PK_CURVE_parameterise_vector(curve,position.position,&t);var eval=PK_CURVE_eval(curve,t,0,&evaluated);PK_CURVE_ask_interval(curve,&range);
                    var distance=0.0;for(var d=0;d<3;d++)distance+=Math.Pow(evaluated.coord[d]-position.position.coord[d],2);
                    log($"  vertex=({position.position.coord[0]:R},{position.position.coord[1]:R},{position.position.coord[2]:R}), curve={curve}, native_inverse={inverse}, t={t:R}, interval=[{range.value[0]:R},{range.value[1]:R}], eval={eval}, distance={Math.Sqrt(distance):R}");
                }
            }
        }
        Require(n == 0, "body faults=" + n);
    }
    finally { if (faults != null) PK_MEMORY_free(faults); }
}
static unsafe void Compare(int reference, int actual, string name, Action<string> log)
{
    for (var kind = 0; kind < 5; kind++)
    {
        var expected = Count(reference, kind); var received = Count(actual, kind);
        log($"{name} topology={kind} reference={expected} self={received}"); Require(expected == received, "topology count " + kind);
    }
    var options = new PK_DEBUG_BODY_compare_o_t { max_diffs = 64, all_tests = PK_LOGICAL_false, num_tests = PK_LOGICAL_true,
        tol_dev_tests = PK_LOGICAL_true, tol_tests = PK_LOGICAL_true, class_tests = PK_LOGICAL_true, sense_tests = PK_LOGICAL_true,
        missing_tests = PK_LOGICAL_true, acc_dev_tests = PK_LOGICAL_false, non_match_tests = PK_LOGICAL_false };
    PK_DEBUG_BODY_compare_r_t result; Check(PK_DEBUG_BODY_compare(reference, actual, &options, &result), "compare");
    try
    {
        log($"{name} global={result.global_result}, local={result.local_result}, diffs={result.n_global_diffs}, face_pairs={result.n_face_pairs}");
        for (var i = 0; i < result.n_global_diffs; i++) log($"  global diff={result.global_diffs[i].diff} masters={result.global_diffs[i].n_masters} similars={result.global_diffs[i].n_similars}");
        var structuralDiffs = 0;
        for (var f = 0; f < result.n_face_pairs; f++) for (var i = 0; i < result.face_pairs[f].n_local_diffs; i++)
        {
            var d = result.face_pairs[f].local_diffs[i];
            // Chart anchors define an internal parameterization, not model
            // vertices. All actual topology counts remain hard gates above.
            var chart = d.diff == PK_DEBUG_diff_n_cht_pts_c;
            log($"  {(chart ? "PARAMETERIZATION" : "STRUCTURE")} face={f} diff={d.diff} entity={d.master_entity}/{d.similar_entity} class={d.master_class}/{d.similar_class} int={d.master_int}/{d.similar_int} value={d.master_double:R}/{d.similar_double:R}");
            if (!chart) structuralDiffs++;
        }
        Require(result.n_face_pairs == Count(actual, 2), "complete face matching");
        Require(result.global_result == PK_DEBUG_global_res_no_diffs_c && structuralDiffs == 0, "structural comparison");
    }
    finally { PK_DEBUG_BODY_compare_r_f(&result); }
}
static unsafe int Count(int body, int kind)
{
    int n = 0; int* entities = null;
    Check(kind switch { 0 => PK_BODY_ask_regions(body, &n, &entities), 1 => PK_BODY_ask_shells(body, &n, &entities),
        2 => PK_BODY_ask_faces(body, &n, &entities), 3 => PK_BODY_ask_edges(body, &n, &entities), _ => PK_BODY_ask_vertices(body, &n, &entities) }, "topology count");
    try { return n; } finally { if (entities != null) PK_MEMORY_free(entities); }
}
static unsafe PK_VECTOR_t VertexCenter(int body)
{
    int n = 0; int* vertices = null; Check(PK_BODY_ask_vertices(body, &n, &vertices), "vertices");
    PK_VECTOR_t center = default;
    try
    {
        if(n==0)return center;
        for (var i = 0; i < n; i++)
        {
            int point = 0; Check(PK_VERTEX_ask_point(vertices[i], &point), "vertex point"); PK_POINT_sf_t sf;
            Check(PK_POINT_ask(point, &sf), "point"); for (var d = 0; d < 3; d++) center.coord[d] += sf.position.coord[d] / n;
        }
        return center;
    }
    finally { if (vertices != null) PK_MEMORY_free(vertices); }
}
static unsafe int InnerFace(bool self,int body)
{
    int n=0;int* faces=null;Check(self?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"inner faces");
    try
    {
        for(var i=0;i<n;i++)
        {
            int ne=0;int* edges=null;Check(self?KernelRuntime.FaceAskEdges(faces[i],&ne,&edges):PK_FACE_ask_edges(faces[i],&ne,&edges),"inner face edges");
            Check(self?KernelRuntime.MemoryFree(edges):PK_MEMORY_free(edges),"free inner edges");if(ne==1)return faces[i];
        }
        throw new Exception("missing closed inner face");
    }
    finally{Check(self?KernelRuntime.MemoryFree(faces):PK_MEMORY_free(faces),"free inner faces");}
}
static void Check(int error, string operation) { if (error != 0) throw new Exception(operation + ": error=" + error+$", stage={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, t={KernelRuntime.LastSheetParameter:R}, algorithm={KernelRuntime.LastSheetAlgorithmStatus}, chart={KernelRuntime.LastChartBuildFailure}, eval_kind={KernelRuntime.LastICurveEvalReport.Kind}, detail={KernelRuntime.LastICurveEvalReport.Detail}, segment={KernelRuntime.LastICurveEvalReport.Segment}, residual={KernelRuntime.LastICurveEvalReport.Residual:R}"); }
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static string ScriptPath([CallerFilePath] string path = "") => path;

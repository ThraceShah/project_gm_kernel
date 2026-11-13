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

var outputName=args.Single(x=>x.StartsWith("--output-name=",StringComparison.Ordinal))["--output-name=".Length..];
if(outputName.Length==0||!outputName.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'))throw new ArgumentException("one new output directory name is required");
var directory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "curved-sheet-partition-oracle",outputName));
if(Directory.Exists(directory))throw new InvalidOperationException("Existing execution evidence is immutable");
Directory.CreateDirectory(directory); var report = new StringBuilder();
var filter=args.FirstOrDefault(x=>!x.StartsWith("--",StringComparison.Ordinal));
var blendLifecycle=args.Contains("--blend-lifecycle",StringComparer.Ordinal);
var reverseBlendSupports=args.Contains("--reverse-blend-supports",StringComparer.Ordinal);
var scaleOption=args.SingleOrDefault(x=>x.StartsWith("--blend-scale=",StringComparison.Ordinal));
var blendScale=scaleOption==null?1:double.Parse(scaleOption["--blend-scale=".Length..],System.Globalization.CultureInfo.InvariantCulture);
var radiusOption=args.SingleOrDefault(x=>x.StartsWith("--blend-radius=",StringComparison.Ordinal));
var secondRadiusOption=args.SingleOrDefault(x=>x.StartsWith("--blend-second-radius=",StringComparison.Ordinal));
var firstRadius=radiusOption==null?.2:double.Parse(radiusOption["--blend-radius=".Length..],System.Globalization.CultureInfo.InvariantCulture);
var secondRadius=secondRadiusOption==null?firstRadius:double.Parse(secondRadiusOption["--blend-second-radius=".Length..],System.Globalization.CultureInfo.InvariantCulture);
if(!double.IsFinite(firstRadius)||firstRadius==0||Math.Abs(firstRadius)!=Math.Abs(secondRadius))throw new ArgumentException("equal finite nonzero absolute Blend radii");
if(!double.IsFinite(blendScale)||blendScale<=0||blendScale!=1&&!blendLifecycle)throw new ArgumentException("positive scale requires Blend lifecycle");
if((blendLifecycle||reverseBlendSupports||radiusOption!=null||secondRadiusOption!=null)&&filter?.StartsWith("periodic-ellipse-blend",StringComparison.Ordinal)!=true)throw new ArgumentException("periodic Blend family filter required for its lifecycle, radii and support-order controls");
void Log(string message) { Console.WriteLine(message); report.AppendLine(message); }
var periodicCrossings=0;
unsafe
{
KernelRuntime.TraceFiniteBoundary=trace=>
{
    if(trace.Stage!=KernelRuntime.BoundaryTraceStage.NativePeriodicWinding)return;
    periodicCrossings++;
    if(periodicCrossings<=3)Log($"NATIVE_PERIODIC_WINDING curve={trace.Curve}/{trace.Generation} surface={trace.Surface}/{trace.SurfaceGeneration} t={trace.First:R}/{trace.Last:R} query={trace.UvStart.param[0]:R}/{trace.UvStart.param[1]:R} hit={trace.UvEnd.param[0]:R}/{trace.UvEnd.param[1]:R}");
};
}
try
{
    if (!ParasolidScriptHost.TryStartSession("Independent curved sheet partition oracle", out var host, out var message)) throw new Exception(message);
    using (host)
    unsafe
    {
        var start = new M.PK_SESSION_start_o_s { o_t_version = 1 }; Check(KernelRuntime.SessionStart(&start), "self start");
        try
        {
            if(args.Contains("--precision-lifecycle",StringComparer.Ordinal)){PrecisionLifecycle(directory,Log);return;}
            var failures = 0; var passed = 0;
            Log("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
            foreach (var kind in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 })
            foreach (var operation in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })
            {
                if(kind<8&&operation>=6)continue;
                if(kind>=10&&operation>8)continue;
                var name = (kind==11?"periodic-ellipse-blend":kind==10?"open-nurbs-blend":kind>=8?(kind==9?"rational":"polynomial")+"-closed-loop":kind == 6 ? "isoparametric-line" : kind == 7 ? "tensor-isoparametric" : (kind % 2 == 1 ? "rational" : "polynomial") + (kind < 2 ? "-bilinear" : kind <4 ? "-cubic" : "-multispan-cubic"))
                    + "-" + new[] { "imprint", "trim-keep", "trim-remove", "section-front", "section-back", "section-both", "second-plane", "repeat-plane", "annulus-front", "annulus-back", "annulus-both" }[operation];
                if(kind==10&&operation>=7)name="open-nurbs-blend-"+(operation==7?"trim-second-plane":"repeat-second-plane");
                if(kind==11&&operation>=7)name="periodic-ellipse-blend-"+(operation==7?"trim-second-plane":"repeat-second-plane");
                if(filter!=null&&!name.Contains(filter,StringComparison.Ordinal))continue;
                try
                {
                    int[] ours,reference;
                    if(kind>=10)(ours,reference)=ProduceBlendPair(operation,kind==11,directory,reverseBlendSupports,blendLifecycle,blendScale,firstRadius,secondRadius);
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
            if(filter==null||filter.StartsWith("periodic-ellipse-blend",StringComparison.Ordinal))
            {Log($"NATIVE_PERIODIC_WINDING crossings={periodicCrossings}");Require(periodicCrossings>0,"real periodic native-parameter winding consumer");}
        }
        finally { KernelRuntime.TraceFiniteBoundary=null;Check(KernelRuntime.SessionStop(), "self stop"); }
    }
}
catch (Exception ex) { Log("FAIL: " + ex.Message); Environment.ExitCode = 1; }
finally { File.WriteAllText(Path.Combine(directory, "report.txt"), report.ToString()); }

static unsafe void PrecisionLifecycle(string directory,Action<string> log)
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"native version");
    log($"PK_VERSION {version.major_revision}.{version.minor_revision}.{version.build_number}");
    log("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
    foreach(var scale in new[]{.125,1.7,8.0})
    {
        var label="native-precision-"+scale.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
        var source=Produce(false,0,0)[0];int n;int* vertices;Check(PK_BODY_ask_vertices(source,&n,&vertices),"native precision vertices");
        try{for(var v=0;v<n;v++){int point;PK_POINT_sf_t data;Check(PK_VERTEX_ask_point(vertices[v],&point),"native precision point");Check(PK_POINT_ask(point,&data),"native precision location");Check(PK_VERTEX_set_precision(vertices[v],1e-6+data.position.coord[0]*1e-7),"set actual native local precision");}}
        finally{if(vertices!=null)PK_MEMORY_free(vertices);}
        CheckBody(source,label+" native input",log);var input=TransmitReference(source);File.WriteAllBytes(Path.Combine(directory,label+"-input.x_t"),input);
        log($"INPUT label={label} sha256={Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(input))}");
        var managed=ReceiveManaged(input);var reference=Receive(input);Check(PK_ENTITY_delete(1,&source),"delete native input source");
        int copy;Check(KernelRuntime.EntityCopy(managed,&copy),"managed copy before trim");Check(KernelRuntime.EntityDelete(1,&managed),"delete managed source before trim");managed=copy;
        Check(PK_ENTITY_copy(reference,&copy),"native copy before trim");Check(PK_ENTITY_delete(1,&reference),"delete native source before trim");reference=copy;
        Reattach(true,managed);Reattach(false,reference);Verify(managed,reference,label+"-reattached");
        Trim(true,managed);Trim(false,reference);Verify(managed,reference,label+"-trim");
        Transform(true,managed,scale);Transform(false,reference,scale);Verify(managed,reference,label+"-transformed");
        var managedBytes=Transmit(managed);var nativeBytes=TransmitReference(reference);
        Check(KernelRuntime.EntityDelete(1,&managed),"delete managed product before reload");Check(PK_ENTITY_delete(1,&reference),"delete native product before reload");
        managed=ReceiveManaged(managedBytes);reference=Receive(nativeBytes);
        Require(KernelRuntime.TryResolveBodySlot(managed,out var slot)&&!KernelRuntime.Xt.IsOpaque(slot),"editable precision reload");
        var faceRecord=KernelRuntime.Faces[KernelRuntime.Bodies[slot].FirstFaceBody];var supportRecord=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(faceRecord.SurfTag)];
        log($"RELOADED body_type={KernelRuntime.Bodies[slot].BodyType} faces={KernelRuntime.Bodies[slot].FaceCountBody} support={supportRecord.Class}");
        if(supportRecord.Class==SurfaceClass.BSurface){var data=KernelRuntime.BSurfaceDataStore[supportRecord.DataIndex];log($"RELOADED_SPLINE closed={data.IsUClosed}/{data.IsVClosed} periodic={data.IsUPeriodic}/{data.IsVPeriodic} degrees={data.UDegree}/{data.VDegree} vertices={data.NUVertices}/{data.NVVertices} knots={string.Join(',',data.UExpandedKnotsSpan().ToArray())}/{string.Join(',',data.VExpandedKnotsSpan().ToArray())}");}
        var reloadEdge=KernelRuntime.Bodies[slot].FirstEdgeBody;
        for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,reloadEdge=KernelRuntime.Edges[reloadEdge].NextInBody)
        {
            var edge=KernelRuntime.Edges[reloadEdge];var curve=KernelRuntime.GetCurveSlotByTag(edge.CurveTag);
            log($"RELOADED_EDGE curve={(curve<0?"missing":KernelRuntime.Curves[curve].Class)} explicit_range={edge.HasParameterRange} range={edge.ParameterStart:R}/{edge.ParameterEnd:R} finite={KernelRuntime.TryProfileEdgeRange(edge,out _,out _)} precision={edge.Tolerance:R}");
            if(curve>=0&&KernelRuntime.Curves[curve].Class==CurveClass.TRCurve)
            {
                var trim=KernelRuntime.TrCurveDataPool[KernelRuntime.Curves[curve].DataIndex];var basis=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(trim.BasisCurveTag)];
                log($"RELOADED_TR_BASIS kind={basis.Class} limits={trim.Parm1:R}/{trim.Parm2:R}");
            }
        }
        var plane=new PK_PLANE_sf_t();plane.basis_set.location=new(0,0,scale*.5+.125);plane.basis_set.axis=new(0,0,1);plane.basis_set.ref_direction=new(1,0,0);
        int cutter;Check(KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&plane,&cutter),"managed post-reload plane");
        int* cuts;Check(KernelRuntime.BodyImprintPlane(managed,cutter,1e-8,&n,&cuts),"managed model after reload");Require(n==1,"managed finite reloaded cut");if(cuts!=null)KernelRuntime.MemoryFree(cuts);
        Check(KernelRuntime.EntityDelete(1,&cutter),"delete managed post-reload plane");Check(PK_PLANE_create(&plane,&cutter),"native post-reload plane");
        Check(PK_BODY_imprint_plane(reference,cutter,1e-8,&n,&cuts),"native model after reload");Require(n==1,"native finite reloaded cut");if(cuts!=null)PK_MEMORY_free(cuts);
        Check(PK_ENTITY_delete(1,&cutter),"delete native post-reload plane");Verify(managed,reference,label+"-reloaded-model");
        Check(KernelRuntime.EntityDelete(1,&managed),"delete final managed precision body");Check(PK_ENTITY_delete(1,&reference),"delete final native precision body");
        log("PASS "+label+" copy/delete/reattach/trim/transform/reload/model");
    }

    void Verify(int managed,int reference,string label)
    {
        var bytes=Transmit(managed);var fresh=Receive(bytes);File.WriteAllBytes(Path.Combine(directory,label+"-gm.x_t"),bytes);File.WriteAllBytes(Path.Combine(directory,label+"-pk.x_t"),TransmitReference(reference));
        try{CheckBody(fresh,label+" fresh",log);CheckBody(reference,label+" reference",log);ParasolidBodyCompare.AssertEquivalent(reference,fresh,label);}
        finally{Check(PK_ENTITY_delete(1,&fresh),"delete precision verification body");}
        log("GEOMETRY_TOPOLOGY_PRECISION_PASS "+label);
    }
    void Trim(bool own,int body)
    {
        int n;int* faces;Check(own?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"trim finite faces");var seed=0;
        int count=0;int* boundary=null;
        try
        {
            for(var f=0;f<n;f++)
            {
                Check(own?KernelRuntime.FaceAskEdges(faces[f],&count,&boundary):PK_FACE_ask_edges(faces[f],&count,&boundary),"trim finite boundary");
                if(count==3){seed=faces[f];break;}
                if(boundary!=null)Check(own?KernelRuntime.MemoryFree(boundary):PK_MEMORY_free(boundary),"free other face boundary");boundary=null;
            }
            Require(seed!=0,"native finite triangular cell");
            Check(own?KernelRuntime.BodyTrim(body,count,boundary,1,&seed,PK_LOGICAL_true):PK_BODY_trim(body,count,boundary,1,&seed,PK_LOGICAL_true),"local-precision whole-face trim");
        }
        finally{if(boundary!=null)Check(own?KernelRuntime.MemoryFree(boundary):PK_MEMORY_free(boundary),"free trim boundary");if(faces!=null)Check(own?KernelRuntime.MemoryFree(faces):PK_MEMORY_free(faces),"free trim faces");}
    }
    void Reattach(bool own,int body)
    {
        int count;int* faces;Check(own?KernelRuntime.BodyAskFaces(body,&count,&faces):PK_BODY_ask_faces(body,&count,&faces),"reattach actual faces");
        try
        {
            for(var f=0;f<count;f++)
            {
                int surface;byte sense;PK_LOGICAL_t nativeSense;
                if(own){Check(KernelRuntime.FaceAskOrientedSurf(faces[f],&surface,&sense),"managed native support and sense");nativeSense=sense;}
                else{Check(PK_FACE_ask_oriented_surf(faces[f],&surface,&nativeSense),"native support and sense");sense=(byte)nativeSense;}
                Check(own?KernelRuntime.TopologyDetachGeometry(faces[f]):PK_TOPOL_detach_geom(faces[f]),"detach actual support");
                Check(own?KernelRuntime.FaceAttachSurfaces(1,faces+f,&surface,&sense):PK_FACE_attach_surfs(1,faces+f,&surface,&nativeSense),"reattach original native support");
            }
        }
        finally{if(faces!=null)Check(own?KernelRuntime.MemoryFree(faces):PK_MEMORY_free(faces),"free reattached faces");}
    }
    void Transform(bool own,int body,double scale)
    {
        M.PK_TRANSF_sf_s form=default;form.matrix[0]=-scale;form.matrix[5]=form.matrix[10]=scale;form.matrix[15]=1;form.matrix[3]=.25;form.matrix[7]=-.375;form.matrix[11]=.125;
        int tag;Check(own?KernelRuntime.TransfCreate(&form,&tag):PK_TRANSF_create((PK_TRANSF_sf_t*)&form,&tag),"precision mirror and scale");
        int count;int* replacements;PK_LOGICAL_t* exact;
        Check(own?KernelRuntime.BodyTransform(body,tag,0,&count,&replacements,(byte**)&exact):PK_BODY_transform(body,tag,0,&count,&replacements,&exact),"transform local precision body");
        if(replacements!=null)Check(own?KernelRuntime.MemoryFree(replacements):PK_MEMORY_free(replacements),"free transform replacements");
        if(exact!=null)Check(own?KernelRuntime.MemoryFree(exact):PK_MEMORY_free(exact),"free transform exactness");
        Check(own?KernelRuntime.EntityDelete(1,&tag):PK_ENTITY_delete(1,&tag),"delete precision transform");
    }
}

static unsafe int ReceiveManaged(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new M.PK_MEMORY_block_s{bytes=p,n_bytes=(nuint)bytes.Length};var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(KernelRuntime.PartReceiveB(block,&options,&n,&parts),"managed receive native precision fixture");
        try{Require(n==1&&KernelRuntime.TryResolveBodySlot(parts[0],out var body)&&!KernelRuntime.Xt.IsOpaque(body),"one editable native sheet");return parts[0];}finally{if(parts!=null)KernelRuntime.MemoryFree(parts);}
    }
}

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

static unsafe (int[],int[]) ProduceBlendPair(int operation,bool periodic,string directory,bool reverse,bool lifecycle,double scale,double firstRadius,double secondRadius)
{
    double* poles=stackalloc double[18];for(var i=0;i<3;i++)for(var j=0;j<2;j++){var at=3*(2*i+j);poles[at]=i-1;poles[at+1]=2*j-1;poles[at+2]=(i==1?-.3:.3)+.1*(i-1)*(2*j-1);}
    double* knots=stackalloc double[2]{-1,1};int* um=stackalloc int[2]{3,3};int* vm=stackalloc int[2]{2,2};
    var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=1,n_u_vertices=3,n_v_vertices=2,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
    int ownSource,nativeSource;
    if(periodic)
    {
        var cylinder=new PK_CYL_sf_t{radius=1};cylinder.basis_set.axis=new(0,0,1);cylinder.basis_set.ref_direction=new(1,0,0);
        Check(KernelRuntime.CylCreate((M.PK_CYL_sf_s*)&cylinder,&ownSource),"own periodic Blend support");Check(PK_CYL_create(&cylinder,&nativeSource),"native periodic Blend support");
    }
    else{Check(KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&ownSource),"own Blend support");Check(PK_BSURF_create(&sf,&nativeSource),"native Blend support");}
    var wall=new PK_PLANE_sf_t();wall.basis_set.axis=periodic?new(0,.6,.8):new(0,1,0);wall.basis_set.ref_direction=new(1,0,0);int ownWall,nativeWall;
    Check(KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&wall,&ownWall),"own Blend wall");Check(PK_PLANE_create(&wall,&nativeWall),"native Blend wall");
    var options=new PK_SURF_create_blend_o_t();var a=periodic?new PK_VECTOR_t(1.2,0,.25):new PK_VECTOR_t(.5,0,.075);var b=periodic?new PK_VECTOR_t(0,1.2,-.65):new PK_VECTOR_t(-.5,0,.075);var radius=periodic?firstRadius:.1;var secondRange=periodic?secondRadius:.1;int own,native;
    Console.WriteLine($"BLEND_INPUT periodic={periodic} first_radius={radius:R} second_radius={secondRange:R} reverse_supports={reverse} lifecycle={lifecycle} scale={scale:R}");
    Check(PK_SURF_create_blend(reverse?nativeWall:nativeSource,radius,reverse?nativeSource:nativeWall,secondRange,a,b,&options,&native),"native independent Blend");
    Check(KernelRuntime.SurfCreateBlend(reverse?ownWall:ownSource,radius,reverse?ownSource:ownWall,secondRange,*(M.PK_VECTOR_s*)&a,*(M.PK_VECTOR_s*)&b,(M.PK_SURF_create_blend_o_s*)&options,&own),"own independent Blend");
    var ownDefinition=new M.PK_BLENDSF_sf_s();var nativeDefinition=new PK_BLENDSF_sf_t();
    Check(KernelRuntime.BlendSfAsk(own,&ownDefinition),"own spine");Check(PK_BLENDSF_ask(native,&nativeDefinition),"native spine");
    var ownBox=new M.PK_UVBOX_s();Check(KernelRuntime.SurfAskUvbox(own,&ownBox),"own Blend domain");
    var lower=ownBox.param[0];var width=ownBox.param[2]-lower;ownBox.param[0]=lower+(periodic?.08:.4)*width;ownBox.param[2]=lower+(periodic?.18:.6)*width;ownBox.param[1]=.15;ownBox.param[3]=.85;
    var nativeBox=new PK_UVBOX_t();nativeBox.param[1]=.15;nativeBox.param[3]=.85;
    double nativeMiddle=0;
    if(periodic)
    {
        var p=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(ownDefinition.spine,(ownBox.param[0]+ownBox.param[2])*.5,0,&p),"periodic midpoint spine");
        Check(PK_CURVE_parameterise_vector(nativeDefinition.spine,*(PK_VECTOR_t*)&p,&nativeMiddle),"periodic native lift");
    }
    for(var end=0;end<2;end++)
    {
        var p=new M.PK_VECTOR_s();Check(KernelRuntime.CurveEval(ownDefinition.spine,ownBox.param[end==0?0:2],0,&p),"own endpoint spine");double t;
        Check(PK_CURVE_parameterise_vector(nativeDefinition.spine,*(PK_VECTOR_t*)&p,&t),"native endpoint correspondence");
        if(periodic)t+=Math.Tau*Math.Round((nativeMiddle-t)/Math.Tau);
        nativeBox.param[end==0?0:2]=t;
    }
    if(nativeBox.param[0]>nativeBox.param[2])(nativeBox.param[0],nativeBox.param[2])=(nativeBox.param[2],nativeBox.param[0]);
    int ownBody,nativeBody;Check(KernelRuntime.SurfMakeSheetBody(own,ownBox,&ownBody),"own Blend sheet");Check(PK_SURF_make_sheet_body(native,nativeBox,&nativeBody),"native Blend sheet");
    if(periodic)
    {
        VerifyPeriodicSheet(ownBody,nativeBody,directory,$"periodic-input-{operation}");
    }
    var uv=new M.PK_UV_s();uv.param[0]=(ownBox.param[0]+ownBox.param[2])*.5;uv.param[1]=.5;var point=new M.PK_VECTOR_s();
    Require(KernelRuntime.TryResolveBodySlot(ownBody,out var ownSlot),"own sheet slot");
    Check(KernelRuntime.SurfEval(KernelRuntime.Faces[KernelRuntime.Bodies[ownSlot].FirstFaceBody].SurfTag,uv,0,0,0,&point),"Blend cutter witness");
    var first=new PK_PLANE_sf_t();first.basis_set.location=*(PK_VECTOR_t*)&point;first.basis_set.axis=new(1,0,0);first.basis_set.ref_direction=new(0,1,0);
    var second=first;second.basis_set.axis=new(0,1,0);second.basis_set.ref_direction=new(1,0,0);
    if(periodic&&lifecycle)
    {
        int ownCopy,nativeCopy;Check(KernelRuntime.EntityCopy(ownBody,&ownCopy),"copy periodic sheet");Check(PK_ENTITY_copy(nativeBody,&nativeCopy),"native copy periodic sheet");
        Check(KernelRuntime.EntityDelete(1,&ownBody),"delete original periodic sheet");Check(PK_ENTITY_delete(1,&nativeBody),"delete native original periodic sheet");ownBody=ownCopy;nativeBody=nativeCopy;
        TransformPeriodicSheet(true,ownBody,scale);TransformPeriodicSheet(false,nativeBody,scale);
        MapPlane(ref first,scale);MapPlane(ref second,scale);
        var ownBytes=Transmit(ownBody);var nativeBytes=TransmitReference(nativeBody);
        File.WriteAllBytes(Path.Combine(directory,$"periodic-lifecycle-{operation}-before-reload-self.x_t"),ownBytes);File.WriteAllBytes(Path.Combine(directory,$"periodic-lifecycle-{operation}-before-reload-reference.x_t"),nativeBytes);
        Check(KernelRuntime.EntityDelete(1,&ownBody),"delete transformed periodic sheet");Check(PK_ENTITY_delete(1,&nativeBody),"delete native transformed periodic sheet");
        ownBody=ReceiveManaged(ownBytes);nativeBody=Receive(nativeBytes);VerifyPeriodicSheet(ownBody,nativeBody,directory,$"periodic-lifecycle-{operation}-reloaded");
        Console.WriteLine($"PERIODIC_LIFECYCLE_PASS operation={operation} reverse_supports={reverse} mirror_scale={scale:R} copy_source_delete=True editable_GM_XT_reload=True");
    }
    return (BlendOperations(true,ownBody,first,second,operation),BlendOperations(false,nativeBody,first,second,operation));
}

static unsafe void VerifyPeriodicSheet(int own,int native,string directory,string label)
{
    var bytes=Transmit(own);File.WriteAllBytes(Path.Combine(directory,label+"-self.x_t"),bytes);File.WriteAllBytes(Path.Combine(directory,label+"-reference.x_t"),TransmitReference(native));
    var received=Receive(bytes);
    try{CheckBody(native,label+" reference",Console.WriteLine);CheckBody(received,label+" received",Console.WriteLine);Compare(native,received,label,Console.WriteLine);}
    finally{Check(PK_ENTITY_delete(1,&received),"delete periodic sheet verification");}
}

static unsafe void TransformPeriodicSheet(bool own,int body,double scale)
{
    M.PK_TRANSF_sf_s form=default;form.matrix[0]=-scale;form.matrix[5]=form.matrix[10]=scale;form.matrix[15]=1;form.matrix[3]=.25;form.matrix[7]=-.375;form.matrix[11]=.125;
    int transform;Check(own?KernelRuntime.TransfCreate(&form,&transform):PK_TRANSF_create((PK_TRANSF_sf_t*)&form,&transform),"periodic mirror scale");
    int n;int* replacements;PK_LOGICAL_t* exact;
    Check(own?KernelRuntime.BodyTransform(body,transform,0,&n,&replacements,(byte**)&exact):PK_BODY_transform(body,transform,0,&n,&replacements,&exact),"transform periodic sheet");
    if(replacements!=null)Check(own?KernelRuntime.MemoryFree(replacements):PK_MEMORY_free(replacements),"periodic transform replacements");
    if(exact!=null)Check(own?KernelRuntime.MemoryFree(exact):PK_MEMORY_free(exact),"periodic transform exactness");
    Check(own?KernelRuntime.EntityDelete(1,&transform):PK_ENTITY_delete(1,&transform),"delete periodic transform");
}

static unsafe void MapPlane(ref PK_PLANE_sf_t plane,double scale)
{
    plane.basis_set.location=new(.25-scale*plane.basis_set.location.coord[0],-.375+scale*plane.basis_set.location.coord[1],.125+scale*plane.basis_set.location.coord[2]);
    plane.basis_set.axis.coord[0]=-plane.basis_set.axis.coord[0];plane.basis_set.ref_direction.coord[0]=-plane.basis_set.ref_direction.coord[0];
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
    var options = new PK_BODY_check_o_t { max_faults = 100,extra_faults=PK_check_extra_faults_latest_c }; int n = 0; PK_check_fault_t* faults = null;
    Check(PK_BODY_check(body, &options, &n, &faults), "BODY_check");
    try
    {
        for (var i = 0; i < n; i++)
        {
            log($"{name} fault={faults[i].state}, entity={faults[i].entity_1}/{faults[i].entity_2}");
            int faultClass;var classError=PK_ENTITY_ask_class(faults[i].entity_1,&faultClass);log($"  class_error={classError} class={faultClass}");
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
    ParasolidBodyCompare.AssertEquivalent(reference,actual,name);
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

#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

// Phase A: compare the public geometry ABI, complete NURBS result buffers,
// parameter domains and dependency lifecycle against the installed Parasolid.
// The existing writer oracle then checks the seven geometry classes through
// managed body creation -> text transmit -> real PK receive -> body compare.

global using ManagedGeometryTag = System.Int32;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Runtime;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

var scriptDirectory = Path.GetDirectoryName(ScriptPath())!;
var outputName=args.Single(x=>x.StartsWith("--output-name=",StringComparison.Ordinal))["--output-name=".Length..];
Require(outputName.Length>0&&outputName.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'),"one new output name");
var outputDirectory=Path.GetFullPath(Path.Combine(scriptDirectory,"..","temp_docs","geometry-public-api",outputName));
Require(!Directory.Exists(outputDirectory),"immutable execution evidence");Directory.CreateDirectory(outputDirectory);
var report = new StringBuilder();
void Log(string line) { Console.WriteLine(line); report.AppendLine(line); }
Log("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
try
{
    if (!ParasolidScriptHost.TryStartSession("Public geometry API oracle", out var host, out var message))
        throw new InvalidOperationException(message);
    using (host)
    {
        unsafe
        {
            var start = new M.PK_SESSION_start_o_s { o_t_version = 1 };
            Check(KernelRuntime.SessionStart(&start), "managed session start");
            try { Run(Log); }
            finally { Check(KernelRuntime.SessionStop(), "managed session stop"); }
        }
    }
    if (!args.Contains("--contracts-only"))
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add(Path.Combine(scriptDirectory, "XtGeometryWriterOracle.cs"));
        start.ArgumentList.Add("-c");start.ArgumentList.Add("Release");start.ArgumentList.Add("--");
        start.ArgumentList.Add("--output-name="+outputName);
        NativeRuntimePolicy.Prepare(start);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the geometry body oracle.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Log(await output); var diagnostics = await error;
        if (diagnostics.Length != 0) Log(diagnostics);
        Require(process.ExitCode == 0, $"Geometry body/transmit/receive/compare oracle exited {process.ExitCode}.");
    }
    Log("PASS: public geometry create/ask, full NURBS arrays, native domains, dependency lifecycle and selected oracle checks.");
}
catch (Exception ex) { Log("FAIL: " + ex); Environment.ExitCode = 1; }
finally
{
    File.WriteAllText(Path.Combine(outputDirectory, "oracle.txt"), report.ToString());
}

static string ScriptPath([CallerFilePath] string path = "") => path;
static void Check(int error, string label) { if (error != 0) throw new InvalidOperationException($"{label}: {error}"); }
static void Require(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); }
static void Equal(double actual, double expected, string label)
    => Require(double.IsFinite(actual) && double.IsFinite(expected)
        && Math.Abs(actual - expected) <= 2e-11 * Math.Max(1, Math.Abs(expected)),
        $"{label}: actual={actual:R}, expected={expected:R}");

static unsafe void Run(Action<string> log)
{
    Require(sizeof(M.PK_BCURVE_sf_s) == sizeof(PK_BCURVE_sf_t)
        && sizeof(M.PK_BSURF_sf_s) == sizeof(PK_BSURF_sf_t), "Generated standard-form ABI sizes differ.");
    var axis = new M.PK_AXIS2_sf_s(); axis.axis.coord[2] = 1; axis.ref_direction.coord[0] = 1;
    var pointSf = new M.PK_POINT_sf_s(); pointSf.position.coord[0] = 1; pointSf.position.coord[1] = -2; pointSf.position.coord[2] = 3;
    int point, pkPoint;
    Check(KernelRuntime.PointCreate(&pointSf, &point), "managed point create");
    Check(PK_POINT_create((PK_POINT_sf_t*)&pointSf, &pkPoint), "PK point create");
    var askedPoint = new M.PK_POINT_sf_s(); PK_POINT_sf_t pkPointSf;
    delegate* unmanaged<int, M.PK_POINT_sf_s*, int> pointAsk = &KernelExports.PK_POINT_ask;
    Check(pointAsk(point, &askedPoint), "managed point ask"); Check(PK_POINT_ask(pkPoint, &pkPointSf), "PK point ask");
    for (var i = 0; i < 3; i++) Equal(askedPoint.position.coord[i], pkPointSf.position.coord[i], "point coordinate");

    var ellipseSf = new M.PK_ELLIPSE_sf_s { basis_set = axis, R1 = 3, R2 = 2 };
    int ellipse, pkEllipse;
    delegate* unmanaged<M.PK_ELLIPSE_sf_s*, int*, int> ellipseCreate = &KernelExports.PK_ELLIPSE_create;
    delegate* unmanaged<int, M.PK_ELLIPSE_sf_s*, int> ellipseAsk = &KernelExports.PK_ELLIPSE_ask;
    Check(ellipseCreate(&ellipseSf, &ellipse), "managed ellipse create");
    Check(PK_ELLIPSE_create((PK_ELLIPSE_sf_t*)&ellipseSf, &pkEllipse), "PK ellipse create");
    var askedEllipse = new M.PK_ELLIPSE_sf_s(); PK_ELLIPSE_sf_t pkEllipseSf;
    Check(ellipseAsk(ellipse, &askedEllipse), "managed ellipse ask"); Check(PK_ELLIPSE_ask(pkEllipse, &pkEllipseSf), "PK ellipse ask");
    Equal(askedEllipse.R1, pkEllipseSf.R1, "ellipse R1"); Equal(askedEllipse.R2, pkEllipseSf.R2, "ellipse R2");
    CompareCurve(ellipse, pkEllipse, .73); CompareInterval(ellipse, pkEllipse);
    log("PASS point + ellipse public forms and native jets.");

    var lineSf = new M.PK_LINE_sf_s(); lineSf.basis_set.location.coord[0] = 2; lineSf.basis_set.axis.coord[2] = 1;
    int line, pkLine;
    Check(KernelRuntime.LineCreate(&lineSf, &line), "managed line create");
    Check(PK_LINE_create((PK_LINE_sf_t*)&lineSf, &pkLine), "PK line create");
    CompareInterval(line, pkLine);
    var start = -1.25; var end = 2.75; int trimmed, pkTrimmed, error;
    delegate* unmanaged<int*, double*, double*, int*, int*, void> trimCreate = &KernelExports.CRTRCU;
    trimCreate(&line, &start, &end, &trimmed, &error); Check(error, "managed CRTRCU");
    CRTRCU(&pkLine, &start, &end, &pkTrimmed, &error); Check(error, "PK CRTRCU");
    var trim = new M.PK_TRCURVE_sf_s(); PK_TRCURVE_sf_t pkTrim;
    Check(KernelRuntime.TrCurveAsk(trimmed, &trim), "managed trim ask"); Check(PK_TRCURVE_ask(pkTrimmed, &pkTrim), "PK trim ask");
    Require(trim.basis_curve == line && pkTrim.basis_curve == pkLine, "Trimmed curve basis identity changed.");
    Equal(trim.t_int.value[0], pkTrim.t_int.value[0], "trim lower"); Equal(trim.t_int.value[1], pkTrim.t_int.value[1], "trim upper");
    CompareInterval(trimmed, pkTrimmed); CompareCurve(trimmed, pkTrimmed, .63);
    log("PASS CRTRCU + PK_TRCURVE_ask, basis identity, retained basis interval, rebased native domain and jets.");

    var plain = CreateCurvePair(planar: false, rational: false);
    var rational = CreateCurvePair(planar: false, rational: true);
    var uv = CreateCurvePair(planar: true, rational: false);
    CompareBCurveForm(plain.Managed, plain.Pk); CompareCurve(plain.Managed, plain.Pk, 4.3);
    CompareBCurveForm(rational.Managed, rational.Pk); CompareCurve(rational.Managed, rational.Pk, 4.3);
    CompareBCurveForm(uv.Managed, uv.Pk);
    var patch = CreateSurfacePair(); CompareBSurfaceForm(patch.Managed, patch.Pk);
    CompareSurface(patch.Managed, patch.Pk, 2.6, -.3);
    CompareInterval(plain.Managed, plain.Pk); CompareBox(patch.Managed, patch.Pk);
    log("PASS polynomial/rational/2D B-curve and B-surface metadata, every pole/knot/multiplicity, domains and jets.");

    int plane, pkPlane;
    var planeSf = new M.PK_PLANE_sf_s { basis_set = axis };
    Check(KernelRuntime.PlaneCreate(&planeSf, &plane), "managed plane"); Check(PK_PLANE_create((PK_PLANE_sf_t*)&planeSf, &pkPlane), "PK plane");
    var spSf = new M.PK_SPCURVE_sf_s { surf = plane, curve = uv.Managed };
    var pkSpSf = new PK_SPCURVE_sf_t(pkPlane, uv.Pk); int sp, pkSp;
    delegate* unmanaged<M.PK_SPCURVE_sf_s*, int*, int> spCreate = &KernelExports.PK_SPCURVE_create;
    Check(spCreate(&spSf, &sp), "managed SP create"); Check(PK_SPCURVE_create(&pkSpSf, &pkSp), "PK SP create");
    var spAsk = new M.PK_SPCURVE_sf_s(); PK_SPCURVE_sf_t pkSpAsk;
    Check(KernelRuntime.SpCurveAsk(sp, &spAsk), "managed SP ask"); Check(PK_SPCURVE_ask(pkSp, &pkSpAsk), "PK SP ask");
    Require(spAsk.surf == plane && spAsk.curve == uv.Managed && pkSpAsk.surf == pkPlane && pkSpAsk.curve == uv.Pk, "SP support identity changed.");
    CompareInterval(sp, pkSp); CompareCurve(sp, pkSp, 4.3);

    var sweptSf = new M.PK_SWEPT_sf_s { curve = ellipse }; sweptSf.direction.coord[2] = 1;
    var pkSweptSf = new PK_SWEPT_sf_t(pkEllipse, new PK_VECTOR1_t(0,0,1)); int swept, pkSwept;
    delegate* unmanaged<M.PK_SWEPT_sf_s*, int*, int> sweptCreate = &KernelExports.PK_SWEPT_create;
    Check(sweptCreate(&sweptSf, &swept), "managed swept create"); Check(PK_SWEPT_create(&pkSweptSf, &pkSwept), "PK swept create");
    var sweptAsk = new M.PK_SWEPT_sf_s(); PK_SWEPT_sf_t pkSweptAsk;
    Check(KernelRuntime.SweptAsk(swept, &sweptAsk), "managed swept ask"); Check(PK_SWEPT_ask(pkSwept, &pkSweptAsk), "PK swept ask");
    Require(sweptAsk.curve == ellipse && pkSweptAsk.curve == pkEllipse, "Swept profile identity changed.");
    for (var i = 0; i < 3; i++) Equal(sweptAsk.direction.coord[i], pkSweptAsk.direction.coord[i], "sweep direction");
    CompareBox(swept, pkSwept); CompareSurface(swept, pkSwept, .73, .3);

    var spunSf = new M.PK_SPUN_sf_s { curve = line }; spunSf.axis.axis.coord[2] = 1;
    var pkSpunSf = new PK_SPUN_sf_t(pkLine, new PK_AXIS1_sf_t(new PK_VECTOR_t(0,0,0),new PK_VECTOR1_t(0,0,1))); int spun, pkSpun;
    delegate* unmanaged<M.PK_SPUN_sf_s*, int*, int> spunCreate = &KernelExports.PK_SPUN_create;
    Check(spunCreate(&spunSf, &spun), "managed spun create"); Check(PK_SPUN_create(&pkSpunSf, &pkSpun), "PK spun create");
    var spunAsk = new M.PK_SPUN_sf_s(); PK_SPUN_sf_t pkSpunAsk;
    Check(KernelRuntime.SpunAsk(spun, &spunAsk), "managed spun ask"); Check(PK_SPUN_ask(pkSpun, &pkSpunAsk), "PK spun ask");
    Require(spunAsk.curve == line && pkSpunAsk.curve == pkLine, "Spun profile identity changed.");
    CompareBox(spun, pkSpun); CompareSurface(spun, pkSpun, .4, .73);

    var offsetSf = new M.PK_OFFSET_sf_s { underlying_surface = patch.Managed, offset_distance = .15 };
    var pkOffsetSf = new PK_OFFSET_sf_t(patch.Pk,.15); int offset, pkOffset;
    delegate* unmanaged<M.PK_OFFSET_sf_s*, int*, int> offsetCreate = &KernelExports.PK_OFFSET_create;
    Check(offsetCreate(&offsetSf, &offset), "managed offset create"); Check(PK_OFFSET_create(&pkOffsetSf, &pkOffset), "PK offset create");
    var offsetAsk = new M.PK_OFFSET_sf_s(); PK_OFFSET_sf_t pkOffsetAsk;
    Check(KernelRuntime.OffsetAsk(offset, &offsetAsk), "managed offset ask"); Check(PK_OFFSET_ask(pkOffset, &pkOffsetAsk), "PK offset ask");
    Require(offsetAsk.underlying_surface == patch.Managed && pkOffsetAsk.underlying_surface == patch.Pk, "Offset support identity changed.");
    Equal(offsetAsk.offset_distance, pkOffsetAsk.offset_distance, "offset distance");
    CompareBox(offset, pkOffset); CompareSurface(offset, pkOffset, 2.6, -.3);
    log("PASS SP/swept/spun/offset public constructors and asks, input identity, native domains and D0-D2 jets.");

    CompareBox(plane, pkPlane);
    var cylinderSf = new M.PK_CYL_sf_s { basis_set = axis, radius = 2 }; int surface, pkSurface;
    Check(KernelRuntime.CylCreate(&cylinderSf, &surface), "managed cylinder"); Check(PK_CYL_create((PK_CYL_sf_t*)&cylinderSf, &pkSurface), "PK cylinder"); CompareBox(surface, pkSurface);
    var coneSf = new M.PK_CONE_sf_s { basis_set = axis, radius = 2, semi_angle = Math.PI / 6 };
    Check(KernelRuntime.ConeCreate(&coneSf, &surface), "managed cone"); Check(PK_CONE_create((PK_CONE_sf_t*)&coneSf, &pkSurface), "PK cone"); CompareBox(surface, pkSurface);
    var sphereSf = new M.PK_SPHERE_sf_s { basis_set = axis, radius = 2 };
    Check(KernelRuntime.SphereCreate(&sphereSf, &surface), "managed sphere"); Check(PK_SPHERE_create((PK_SPHERE_sf_t*)&sphereSf, &pkSurface), "PK sphere"); CompareBox(surface, pkSurface);
    var torusSf = new M.PK_TORUS_sf_s { basis_set = axis, major_radius = 4, minor_radius = 1 };
    Check(KernelRuntime.TorusCreate(&torusSf, &surface), "managed torus"); Check(PK_TORUS_create((PK_TORUS_sf_t*)&torusSf, &pkSurface), "PK torus"); CompareBox(surface, pkSurface);
    log("PASS all existing analytic surface natural UV boxes.");

    var managedDelete = KernelRuntime.EntityDelete(1,&ellipse);
    var nativeDelete = PK_ENTITY_delete(1,&pkEllipse);
    Require(managedDelete == nativeDelete && nativeDelete == PK_ERROR_still_referenced,
        $"Referenced source deletion differs: managed={managedDelete}, native={nativeDelete}.");
    int swept2, pkSwept2;
    Check(sweptCreate(&sweptSf,&swept2),"managed shared owner"); Check(PK_SWEPT_create(&pkSweptSf,&pkSwept2),"PK shared owner");
    Check(KernelRuntime.EntityDelete(1,&swept),"managed delete first owner"); Check(PK_ENTITY_delete(1,&pkSwept),"PK delete first owner");
    int kind; PK_CLASS_t pkKind;
    Check(KernelRuntime.EntityAskClass(ellipse,&kind),"managed retained profile"); Check(PK_ENTITY_ask_class(pkEllipse,&pkKind),"PK retained profile");
    Check(KernelRuntime.EntityDelete(1,&swept2),"managed delete last owner"); Check(PK_ENTITY_delete(1,&pkSwept2),"PK delete last owner");
    Require(KernelRuntime.EntityAskClass(ellipse,&kind) != 0 && PK_ENTITY_ask_class(pkEllipse,&pkKind) != 0,"Last owner did not delete orphan profile.");
    log("PASS referenced-source rejection, multiple owners, last-owner cascade.");
    CompareClampedPeriodicSurfaces(log);
    CompareBodyAttachmentParts(log);
    CompareConstructionParts(log);
}

static unsafe void CompareBodyAttachmentParts(Action<string> log)
{
    for(var arrangement=0;arrangement<4;arrangement++)
    {
        int bodyA,bodyB,pkBodyA,pkBodyB;
        Check(KernelRuntime.BodyCreateSolidBlock(1,2,3,null,&bodyA),"managed part A");
        Check(KernelRuntime.BodyCreateSolidBlock(1,2,3,null,&bodyB),"managed part B");
        Check(PK_BODY_create_solid_block(1,2,3,null,&pkBodyA),"PK part A");
        Check(PK_BODY_create_solid_block(1,2,3,null,&pkBodyB),"PK part B");
        var faceA=ManagedFirstFace(bodyA);var faceB=ManagedFirstFace(bodyB);
        var pkFaceA=NativeFirstFace(pkBodyA);var pkFaceB=NativeFirstFace(pkBodyB);
        int originalA;Check(KernelRuntime.FaceAskSurf(faceA,&originalA),"managed original support A");
        int pkOriginalA;Check(PK_FACE_ask_surf(pkFaceA,&pkOriginalA),"PK original support A");
        var surface=originalA;var pkSurface=pkOriginalA;
        Check(KernelRuntime.TopologyDetachGeometry(faceB),"managed detach target");Check(PK_TOPOL_detach_geom(pkFaceB),"PK detach target");
        byte sense=1;PK_LOGICAL_t pkSense=PK_LOGICAL_true;
        if(arrangement!=0)
        {
            var profile=CreateCurvePair(false,false);var curve=profile.Managed;var pkCurve=profile.Pk;
            if(arrangement==1)
            {
                var bounds=new M.PK_INTERVAL_s();bounds.value[0]=3;bounds.value[1]=7;int wire,pkWire;
                Check(KernelRuntime.CurveMakeWireBody(curve,bounds,&wire),"managed profile wire");
                Check(PK_CURVE_make_wire_body(pkCurve,new PK_INTERVAL_t(3,7),&pkWire),"PK profile wire");
                Require(KernelRuntime.TryResolveBodySlot(wire,out var slot),"managed wire slot");
                curve=KernelRuntime.Edges[KernelRuntime.Bodies[slot].FirstEdgeBody].CurveTag;
                int n;PK_EDGE_t* edges;Check(PK_BODY_ask_edges(pkWire,&n,&edges),"PK wire edges");
                try {Check(PK_EDGE_ask_curve(edges[0],&pkCurve),"PK wire profile");}
                finally {Check(PK_MEMORY_free(edges),"free PK wire edges");}
            }
            var definition=new M.PK_SWEPT_sf_s{curve=curve};definition.direction.coord[2]=1;
            var pkDefinition=new PK_SWEPT_sf_t(pkCurve,new PK_VECTOR1_t(0,0,1));
            Check(KernelRuntime.SweptCreate(&definition,&surface),"managed dependent surface");
            Check(PK_SWEPT_create(&pkDefinition,&pkSurface),"PK dependent surface");
            if(arrangement>=2)
            {
                originalA=surface;pkOriginalA=pkSurface;
                Check(KernelRuntime.TopologyDetachGeometry(faceA),"managed detach A");Check(PK_TOPOL_detach_geom(pkFaceA),"PK detach A");
                if(arrangement==2)
                {
                    Check(KernelRuntime.FaceAttachSurfaces(1,&faceA,&originalA,&sense),"managed first owner");
                    Check(PK_FACE_attach_surfs(1,&pkFaceA,&pkOriginalA,&pkSense),"PK first owner");
                    Require(ManagedPartGeometry(bodyA).Length==0&&NativePartGeometry(pkBodyA).Length==0,
                        "An attached surface's defining profile was misclassified as construction geometry.");
                }
                Check(KernelRuntime.SweptCreate(&definition,&surface),"managed second dependent");
                Check(PK_SWEPT_create(&pkDefinition,&pkSurface),"PK second dependent");
                if(arrangement==2)
                    Require(ManagedPartGeometry(bodyA).SequenceEqual(new[]{surface})&&NativePartGeometry(pkBodyA).SequenceEqual(new[]{pkSurface}),
                        "A new reverse owner must be the only construction member of the attached defining component.");
            }
        }
        var managedFaces=stackalloc int[2]{faceA,faceB};var nativeFaces=stackalloc PK_FACE_t[2]{pkFaceA,pkFaceB};
        var managedSurfaces=stackalloc int[2]{originalA,surface};var nativeSurfaces=stackalloc PK_SURF_t[2]{pkOriginalA,pkSurface};
        var senses=stackalloc byte[2]{1,1};var nativeSenses=stackalloc PK_LOGICAL_t[2]{PK_LOGICAL_true,PK_LOGICAL_true};
        var managed=arrangement==3?KernelRuntime.FaceAttachSurfaces(2,managedFaces,managedSurfaces,senses):KernelRuntime.FaceAttachSurfaces(1,&faceB,&surface,&sense);
        var native=arrangement==3?PK_FACE_attach_surfs(2,nativeFaces,nativeSurfaces,nativeSenses):PK_FACE_attach_surfs(1,&pkFaceB,&pkSurface,&pkSense);
        Require(native==(arrangement==3?PK_ERROR_bad_shared_entity:PK_ERROR_has_parent)&&managed==native,$"Part attachment arrangement {arrangement}: managed={managed}, native={native}.");
        int managedAfter;Check(KernelRuntime.FaceAskSurf(faceB,&managedAfter),"managed target after rejected attach");Require(managedAfter==0,"Failed attachment changed managed target.");
        int after;Check(PK_FACE_ask_surf(pkFaceB,&after),"PK target after rejected attach");Require(after==0,"Rejected PK attachment changed target.");
        log($"PASS body attachment part ownership arrangement {arrangement}: error={native}, target unchanged.");
    }
}

static int ManagedFirstFace(int body)
{
    Require(KernelRuntime.TryResolveBodySlot(body,out var slot),"managed body slot");
    return KernelRuntime.TagOf(PoolKind.Face,KernelRuntime.Bodies[slot].FirstFaceBody);
}

static unsafe int NativeFirstFace(int body)
{
    int count;PK_FACE_t* faces;Check(PK_BODY_ask_faces(body,&count,&faces),"PK first face");
    try {Require(count>0,"PK body has no faces");return faces[0];}
    finally {Check(PK_MEMORY_free(faces),"free PK faces");}
}

static unsafe void CompareClampedPeriodicSurfaces(Action<string> log)
{
    var failures=0;
    var poles=stackalloc double[108];var weights=stackalloc double[3]{1,.5,2};
    var pathKnots=stackalloc double[5]{0,.25,.5,.75,1};var pathMult=stackalloc int[5]{3,2,2,2,3};
    var profileKnots=stackalloc double[2]{0,1};var profileMult=stackalloc int[2]{3,3};
    for(var axis=0;axis<2;axis++)
    for(var defect=0;defect<7;defect++)
    {
        var alongU=axis==0;
        for(var i=0;i<9;i++)
        {
            var cosine=i==8?1:Math.Cos(i*Math.PI/4);var sine=i==8?0:Math.Sin(i*Math.PI/4);
            var pathWeight=(i&1)==0?1:Math.Sqrt(.5);
            for(var j=0;j<3;j++)
            {
                var offset=(alongU?i*3+j:j*9+i)*4;
                poles[offset]=weights[j]*(j+2)*cosine;poles[offset+1]=weights[j]*(j+2)*sine;
                poles[offset+2]=weights[j]*(j==1?1.5:0)*pathWeight;poles[offset+3]=weights[j]*pathWeight;
            }
        }
        if(defect>=4)for(var offset=0;offset<108;offset+=4)poles[offset]+=1e6*poles[offset+3];
        if(defect!=0&&defect!=6)
        {
            var row=defect is 2 or 5?7:8;var offset=(alongU?row*3+1:9+row)*4;
            poles[offset+(defect==3?3:0)]+=defect>=4?1e-7:.01;
        }
        var definition=new M.PK_BSURF_sf_s
        {
            u_degree=2,v_degree=2,n_u_vertices=alongU?9:3,n_v_vertices=alongU?3:9,
            vertex_dim=4,is_rational=1,vertex=poles,n_u_knots=alongU?5:2,n_v_knots=alongU?2:5,
            u_knot=alongU?pathKnots:profileKnots,v_knot=alongU?profileKnots:pathKnots,
            u_knot_mult=alongU?pathMult:profileMult,v_knot_mult=alongU?profileMult:pathMult,
            is_u_periodic=(byte)(alongU?1:0),is_v_periodic=(byte)(alongU?0:1),
            is_u_closed=(byte)(alongU?1:0),is_v_closed=(byte)(alongU?0:1),
            form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,
            convexity=PK_convexity_unset_c,self_intersecting=PK_self_intersect_false_c,
        };
        int managed=0,native=0;
        var actual=KernelRuntime.BSurfCreate(&definition,&managed);var expected=PK_BSURF_create((PK_BSURF_sf_t*)&definition,&native);
        if(actual!=expected||actual!=(defect is 0 or 6?0:PK_ERROR_periodic_not_smooth))
        {
            failures++;log($"FAIL clamped periodic axis{axis} defect{defect}: managed={actual}, PK={expected}.");
        }
        else
        {
            if(defect==0)
            {
                CompareSurface(managed,native,.37,.43);CompareSurface(managed,native,alongU?1.37:.37,alongU?.43:1.43);
                CompareBox(managed,native);
            }
            if(defect==6)ComparePeriodicParameterReuse(managed,native,alongU,log);
            log($"PASS clamped periodic B-surface axis={axis} defect={defect}: error={actual}.");
        }
        if(actual==0)Check(KernelRuntime.EntityDelete(1,&managed),"delete managed periodic surface");
        if(expected==0)Check(PK_ENTITY_delete(1,&native),"delete PK periodic surface");
    }
    Require(failures==0,$"Clamped periodic constructor contract mismatches: {failures}.");
}

static unsafe void ComparePeriodicParameterReuse(int managed,int native,bool alongU,Action<string> log)
{
    var a=stackalloc M.PK_VECTOR_s[9];var b=stackalloc M.PK_VECTOR_s[9];
    var x=stackalloc PK_VECTOR_t[9];var y=stackalloc PK_VECTOR_t[9];
    // A dyadic parameter survives adding/removing the period exactly. Decimal
    // .37 does not: its wrapped value differs by one ULP, and both kernels can
    // expose that perturbation in translated homogeneous mixed derivatives.
    const double p=.375,q=.4375;
    var uv=new M.PK_UV_s();uv.param[0]=alongU?p:q;uv.param[1]=alongU?q:p;
    var shifted=uv;shifted.param[alongU?0:1]+=1;
    Require(shifted.param[alongU?0:1]%1==p,"Periodic test inputs must represent exactly the same native parameter.");
    Check(KernelRuntime.SurfEval(managed,uv,2,2,0,a),"periodic source jet");
    Check(KernelRuntime.SurfEval(managed,shifted,2,2,0,b),"periodic shifted jet");
    Check(PK_SURF_eval(native,new PK_UV_t(uv.param[0],uv.param[1]),2,2,PK_LOGICAL_false,x),"PK periodic source jet");
    Check(PK_SURF_eval(native,new PK_UV_t(shifted.param[0],shifted.param[1]),2,2,PK_LOGICAL_false,y),"PK periodic shifted jet");
    for(var i=0;i<9;i++)for(var axis=0;axis<3;axis++)
    {
        Require(double.IsFinite(a[i].coord[axis])&&a[i].coord[axis]==b[i].coord[axis],$"Managed periodic jet{i}[{axis}] differs.");
        Require(double.IsFinite(x[i].coord[axis])&&x[i].coord[axis]==y[i].coord[axis],$"PK periodic jet{i}[{axis}] differs.");
    }
    log($"PASS translated periodic native parameter reuse axis={(alongU?0:1)}: all D0-D2 entries identical in both kernels.");
}

static unsafe void CompareConstructionParts(Action<string> log)
{
    var curveCount=KernelRuntime.Curves.AliveCount;var surfaceCount=KernelRuntime.Surfaces.AliveCount;
    var pointCount=KernelRuntime.Points.AliveCount;var bodyCount=KernelRuntime.Bodies.AliveCount;
    var poles=stackalloc double[9]{-2,0,0,0,3,0,2,0,0};var knots=stackalloc double[2]{0,1};var mult=stackalloc int[2]{3,3};
    var definition=new M.PK_BCURVE_sf_s{degree=2,n_vertices=3,vertex_dim=3,vertex=poles,n_knots=2,knot=knots,knot_mult=mult,
        form=PK_BCURVE_form_arbitrary_c,knot_type=PK_knot_bezier_ends_c,self_intersecting=PK_self_intersect_false_c};
    int input,nativeInput;Check(KernelRuntime.BCurveCreate(&definition,&input),"managed construction profile");Check(PK_BCURVE_create((PK_BCURVE_sf_t*)&definition,&nativeInput),"PK construction profile");
    var bounds=new M.PK_INTERVAL_s();bounds.value[1]=1;int body,nativeBody;
    Check(KernelRuntime.CurveMakeWireBody(input,bounds,&body),"managed construction wire");Check(PK_CURVE_make_wire_body(nativeInput,new PK_INTERVAL_t(0,1),&nativeBody),"PK construction wire");
    Require(KernelRuntime.TryResolveBodySlot(body,out var bodySlot),"construction body slot");var profile=KernelRuntime.Edges[KernelRuntime.Bodies[bodySlot].FirstEdgeBody].CurveTag;
    if(input!=profile)Check(KernelRuntime.EntityDelete(1,&input),"delete unused managed input");
    int edgeCount;PK_EDGE_t* edges;Check(PK_BODY_ask_edges(nativeBody,&edgeCount,&edges),"PK construction edges");int nativeProfile;
    try {Check(PK_EDGE_ask_curve(edges[0],&nativeProfile),"PK construction edge profile");}finally {Check(PK_MEMORY_free(edges),"free PK construction edges");}
    Require(ManagedPartGeometry(body).Length==0&&NativePartGeometry(nativeBody).Length==0,"A wire's attached geometry is not construction geometry.");
    var sweptForm=new M.PK_SWEPT_sf_s{curve=profile};sweptForm.direction.coord[2]=1;
    var nativeSweptForm=new PK_SWEPT_sf_t(nativeProfile,new PK_VECTOR1_t(0,0,1));int swept,nativeSwept;
    Check(KernelRuntime.SweptCreate(&sweptForm,&swept),"managed body-owned swept");Check(PK_SWEPT_create(&nativeSweptForm,&nativeSwept),"PK body-owned swept");
    var offsetForm=new M.PK_OFFSET_sf_s{underlying_surface=swept,offset_distance=.1};var nativeOffsetForm=new PK_OFFSET_sf_t(nativeSwept,.1);int offset,nativeOffset;
    Check(KernelRuntime.OffsetCreate(&offsetForm,&offset),"managed body-owned offset");Check(PK_OFFSET_create(&nativeOffsetForm,&nativeOffset),"PK body-owned offset");
    var start=.1;var end=.8;int trimmed,nativeTrimmed,error;
    var trimForm=new M.PK_TRCURVE_sf_s{basis_curve=profile};trimForm.t_int.value[0]=start;trimForm.t_int.value[1]=end;
    Check(KernelRuntime.TrCurveCreate(&trimForm,&trimmed),"managed body-owned TR");CRTRCU(&nativeProfile,&start,&end,&nativeTrimmed,&error);Check(error,"PK body-owned TR");
    var managedMembers=ManagedPartGeometry(body);var nativeMembers=NativePartGeometry(nativeBody);
    Require(managedMembers.Length==3&&nativeMembers.Length==3,"Nested construction intermediate or root missing.");
    CompareSurface(swept,nativeSwept,.37,.43);CompareSurface(offset,nativeOffset,.37,.43);CompareCurve(trimmed,nativeTrimmed,.3);
    int copy,nativeCopy;Check(KernelRuntime.EntityCopy(body,&copy),"managed construction copy");Check(PK_ENTITY_copy(nativeBody,&nativeCopy),"PK construction copy");
    var copiedMembers=ManagedPartGeometry(copy);var nativeCopiedMembers=NativePartGeometry(nativeCopy);
    Require(copiedMembers.Length==3&&nativeCopiedMembers.Length==3,"Construction copy changed membership.");
    int copiedOffset=0,nativeCopiedOffset=0,copiedSwept=0,nativeCopiedSwept=0;
    foreach(var tag in copiedMembers)
    {
        int cls;Check(KernelRuntime.EntityAskClass(tag,&cls),"managed member class");var reference=FindNativeClass(nativeCopiedMembers,cls);
        Require(!managedMembers.Contains(tag),"Copied construction aliases source tag.");
        if(cls==PK_CLASS_trcurve)CompareCurve(tag,reference,.3);else CompareSurface(tag,reference,.37,.43);
        if(cls==PK_CLASS_offset){copiedOffset=tag;nativeCopiedOffset=reference;}
        if(cls==PK_CLASS_swept){copiedSwept=tag;nativeCopiedSwept=reference;}
    }
    Check(KernelRuntime.EntityDelete(1,&body),"delete managed source part");Check(PK_ENTITY_delete(1,&nativeBody),"delete PK source part");
    foreach(var tag in managedMembers)Require(!KernelRuntime.IsValidTag(tag),"Source construction member survived body deletion.");
    CompareSurface(copiedOffset,nativeCopiedOffset,.37,.43);
    Check(KernelRuntime.EntityDelete(1,&copiedOffset),"delete managed nested parent");Check(PK_ENTITY_delete(1,&nativeCopiedOffset),"delete PK nested parent");
    int ignored;PK_CLASS_t nativeIgnored;Require(KernelRuntime.EntityAskClass(copiedSwept,&ignored)!=0&&PK_ENTITY_ask_class(nativeCopiedSwept,&nativeIgnored)!=0,"Nested orphan survived parent deletion.");
    Require(ManagedPartGeometry(copy).Length==1&&NativePartGeometry(nativeCopy).Length==1,"Parent cascade did not repair construction chain.");
    Check(KernelRuntime.EntityDelete(1,&copy),"delete managed copied part");Check(PK_ENTITY_delete(1,&nativeCopy),"delete PK copied part");
    Require(KernelRuntime.Curves.AliveCount==curveCount&&KernelRuntime.Surfaces.AliveCount==surfaceCount
        &&KernelRuntime.Points.AliveCount==pointCount&&KernelRuntime.Bodies.AliveCount==bodyCount,"Construction lifecycle leaked entities.");
    log("PASS public PART_ask_geoms, nested construction membership, independent body copy, source deletion, nested-parent cascade and complete part cleanup.");
}

static unsafe int[] ManagedPartGeometry(int body)
{
    int count;int* tags;delegate* unmanaged<int,int*,int**,int> ask=&KernelExports.PK_PART_ask_geoms;
    Check(ask(body,&count,&tags),"managed PART_ask_geoms");
    try {return new ReadOnlySpan<int>(tags,count).ToArray();}finally {if(tags!=null)Check(KernelRuntime.MemoryFree(tags),"free managed part geometry");}
}
static unsafe int[] NativePartGeometry(int body)
{
    int count;PK_GEOM_t* tags;Check(PK_PART_ask_geoms(body,&count,&tags),"PK PART_ask_geoms");
    try {return new ReadOnlySpan<int>(tags,count).ToArray();}finally {if(tags!=null)Check(PK_MEMORY_free(tags),"free PK part geometry");}
}
static unsafe int FindNativeClass(int[] geometry,int expected)
{
    foreach(var tag in geometry){PK_CLASS_t actual;Check(PK_ENTITY_ask_class(tag,&actual),"PK member class");if(actual==expected)return tag;}
    throw new InvalidOperationException("Missing native construction class "+expected);
}

static unsafe (ManagedGeometryTag Managed,PK_GEOM_t Pk) CreateCurvePair(bool planar, bool rational)
{
    double* poles = stackalloc double[8]; new Span<double>(poles,8).Clear();
    var dim=planar?2:rational?4:3;
    if(rational) {poles[3]=1;poles[4]=2;poles[5]=1;poles[7]=2;}
    else {poles[dim]=1;poles[dim+1]=.5;}
    double* knots=stackalloc double[2]{3,7}; int* mult=stackalloc int[2]{2,2};
    var sf=new M.PK_BCURVE_sf_s {degree=1,n_vertices=2,vertex_dim=dim,is_rational=(byte)(rational?1:0),vertex=poles,
        form=PK_BCURVE_form_arbitrary_c,n_knots=2,knot=knots,knot_mult=mult,knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c};
    int managed,pk; Check(KernelRuntime.BCurveCreate(&sf,&managed),"managed B-curve create");
    Check(PK_BCURVE_create((PK_BCURVE_sf_t*)&sf,&pk),"PK B-curve create"); return(managed,pk);
}

static unsafe (ManagedGeometryTag Managed,PK_GEOM_t Pk) CreateSurfacePair()
{
    double* poles=stackalloc double[12]{0,0,0,1,0,.3,0,1,.3,1,1,0};
    double* u=stackalloc double[2]{2,4}; double* v=stackalloc double[2]{-2,3}; int* mult=stackalloc int[2]{2,2};
    var sf=new M.PK_BSURF_sf_s {u_degree=1,v_degree=1,n_u_vertices=2,n_v_vertices=2,vertex_dim=3,vertex=poles,
        form=PK_BSURF_form_arbitrary_c,n_u_knots=2,n_v_knots=2,u_knot=u,v_knot=v,u_knot_mult=mult,v_knot_mult=mult,
        u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_arbitrary_c};
    int managed,pk; Check(KernelRuntime.BSurfCreate(&sf,&managed),"managed B-surface create");
    Check(PK_BSURF_create((PK_BSURF_sf_t*)&sf,&pk),"PK B-surface create"); return(managed,pk);
}

static unsafe void CompareBCurveForm(int curve,int pkCurve)
{
    var actual=new M.PK_BCURVE_sf_s(); PK_BCURVE_sf_t expectedNative;
    Check(KernelRuntime.BCurveAsk(curve,&actual),"managed B-curve ask");
    try
    {
        Check(PK_BCURVE_ask(pkCurve,&expectedNative),"PK B-curve ask");
        var expected=*(M.PK_BCURVE_sf_s*)&expectedNative;
        try
        {
            Require(actual.degree==expected.degree&&actual.n_vertices==expected.n_vertices&&actual.vertex_dim==expected.vertex_dim
                &&actual.is_rational==expected.is_rational&&actual.n_knots==expected.n_knots
                &&actual.form==expected.form&&actual.knot_type==expected.knot_type
                &&actual.is_periodic==expected.is_periodic&&actual.is_closed==expected.is_closed
                &&actual.self_intersecting==expected.self_intersecting,
                $"B-curve metadata differ: managed degree={actual.degree}, vertices={actual.n_vertices}, dimension={actual.vertex_dim}, rational={actual.is_rational}, knots={actual.n_knots}, form={actual.form}, knotType={actual.knot_type}, periodic={actual.is_periodic}, closed={actual.is_closed}, self={actual.self_intersecting}; PK degree={expected.degree}, vertices={expected.n_vertices}, dimension={expected.vertex_dim}, rational={expected.is_rational}, knots={expected.n_knots}, form={expected.form}, knotType={expected.knot_type}, periodic={expected.is_periodic}, closed={expected.is_closed}, self={expected.self_intersecting}.");
            CompareArray(actual.vertex,expected.vertex,actual.n_vertices*actual.vertex_dim,"B-curve poles");
            CompareArray(actual.knot,expected.knot,actual.n_knots,"B-curve knots");
            for(var i=0;i<actual.n_knots;i++) Require(actual.knot_mult[i]==expected.knot_mult[i],"B-curve multiplicity differs.");
        }
        finally {Check(PK_MEMORY_free(expected.vertex),"free PK poles");Check(PK_MEMORY_free(expected.knot),"free PK knots");Check(PK_MEMORY_free(expected.knot_mult),"free PK multiplicities");}
    }
    finally {Check(KernelRuntime.MemoryFree(actual.vertex),"free managed poles");Check(KernelRuntime.MemoryFree(actual.knot),"free managed knots");Check(KernelRuntime.MemoryFree(actual.knot_mult),"free managed multiplicities");}
}

static unsafe void CompareBSurfaceForm(int surface,int pkSurface)
{
    var actual=new M.PK_BSURF_sf_s(); PK_BSURF_sf_t expectedNative;
    Check(KernelRuntime.BSurfAsk(surface,&actual),"managed B-surface ask");
    try
    {
        Check(PK_BSURF_ask(pkSurface,&expectedNative),"PK B-surface ask"); var expected=*(M.PK_BSURF_sf_s*)&expectedNative;
        try
        {
            Require(actual.u_degree==expected.u_degree&&actual.v_degree==expected.v_degree&&actual.n_u_vertices==expected.n_u_vertices
                &&actual.n_v_vertices==expected.n_v_vertices&&actual.vertex_dim==expected.vertex_dim&&actual.is_rational==expected.is_rational
                &&actual.n_u_knots==expected.n_u_knots&&actual.n_v_knots==expected.n_v_knots&&actual.form==expected.form
                &&actual.u_knot_type==expected.u_knot_type&&actual.v_knot_type==expected.v_knot_type
                &&actual.is_u_periodic==expected.is_u_periodic&&actual.is_v_periodic==expected.is_v_periodic
                &&actual.is_u_closed==expected.is_u_closed&&actual.is_v_closed==expected.is_v_closed
                &&actual.self_intersecting==expected.self_intersecting&&actual.convexity==expected.convexity,
                $"B-surface metadata differ: managed degrees={actual.u_degree}/{actual.v_degree}, vertices={actual.n_u_vertices}/{actual.n_v_vertices}, dimension={actual.vertex_dim}, rational={actual.is_rational}, knots={actual.n_u_knots}/{actual.n_v_knots}, form={actual.form}, knotTypes={actual.u_knot_type}/{actual.v_knot_type}, periodic={actual.is_u_periodic}/{actual.is_v_periodic}, closed={actual.is_u_closed}/{actual.is_v_closed}, self={actual.self_intersecting}, convexity={actual.convexity}; PK degrees={expected.u_degree}/{expected.v_degree}, vertices={expected.n_u_vertices}/{expected.n_v_vertices}, dimension={expected.vertex_dim}, rational={expected.is_rational}, knots={expected.n_u_knots}/{expected.n_v_knots}, form={expected.form}, knotTypes={expected.u_knot_type}/{expected.v_knot_type}, periodic={expected.is_u_periodic}/{expected.is_v_periodic}, closed={expected.is_u_closed}/{expected.is_v_closed}, self={expected.self_intersecting}, convexity={expected.convexity}.");
            CompareArray(actual.vertex,expected.vertex,actual.n_u_vertices*actual.n_v_vertices*actual.vertex_dim,"B-surface poles");
            CompareArray(actual.u_knot,expected.u_knot,actual.n_u_knots,"u knots"); CompareArray(actual.v_knot,expected.v_knot,actual.n_v_knots,"v knots");
            for(var i=0;i<actual.n_u_knots;i++) Require(actual.u_knot_mult[i]==expected.u_knot_mult[i],"u multiplicity differs.");
            for(var i=0;i<actual.n_v_knots;i++) Require(actual.v_knot_mult[i]==expected.v_knot_mult[i],"v multiplicity differs.");
        }
        finally {Check(PK_MEMORY_free(expected.vertex),"free PK poles");Check(PK_MEMORY_free(expected.u_knot),"free PK u knots");Check(PK_MEMORY_free(expected.v_knot),"free PK v knots");Check(PK_MEMORY_free(expected.u_knot_mult),"free PK u multiplicities");Check(PK_MEMORY_free(expected.v_knot_mult),"free PK v multiplicities");}
    }
    finally {Check(KernelRuntime.MemoryFree(actual.vertex),"free managed poles");Check(KernelRuntime.MemoryFree(actual.u_knot),"free managed u knots");Check(KernelRuntime.MemoryFree(actual.v_knot),"free managed v knots");Check(KernelRuntime.MemoryFree(actual.u_knot_mult),"free managed u multiplicities");Check(KernelRuntime.MemoryFree(actual.v_knot_mult),"free managed v multiplicities");}
}

static unsafe void CompareArray(double* actual,double* expected,int count,string name)
{for(var i=0;i<count;i++)Equal(actual[i],expected[i],$"{name}[{i}]");}
static unsafe void CompareInterval(int curve,int pkCurve)
{var actual=new M.PK_INTERVAL_s();PK_INTERVAL_t expected;Check(KernelRuntime.CurveAskInterval(curve,&actual),"managed interval");Check(PK_CURVE_ask_interval(pkCurve,&expected),"PK interval");for(var i=0;i<2;i++)Equal(actual.value[i],expected.value[i],"curve interval");}
static unsafe void CompareBox(int surface,int pkSurface)
{var actual=new M.PK_UVBOX_s();PK_UVBOX_t expected;Check(KernelRuntime.SurfAskUvbox(surface,&actual),"managed UV box");Check(PK_SURF_ask_uvbox(pkSurface,&expected),"PK UV box");for(var i=0;i<4;i++)Equal(actual.param[i],expected.param[i],"surface UV box");}
static unsafe void CompareCurve(int curve,int pkCurve,double t)
{var actual=stackalloc M.PK_VECTOR_s[3];var expected=stackalloc PK_VECTOR_t[3];Check(KernelRuntime.CurveEval(curve,t,2,actual),"managed curve jets");Check(PK_CURVE_eval(pkCurve,t,2,expected),"PK curve jets");for(var j=0;j<3;j++)for(var i=0;i<3;i++)Equal(actual[j].coord[i],expected[j].coord[i],$"curve D{j}[{i}]");}
static unsafe void CompareSurface(int surface,int pkSurface,double u,double v)
{var actual=stackalloc M.PK_VECTOR_s[9];var expected=stackalloc PK_VECTOR_t[9];var uv=new M.PK_UV_s();uv.param[0]=u;uv.param[1]=v;Check(KernelRuntime.SurfEval(surface,uv,2,2,0,actual),"managed surface jets");Check(PK_SURF_eval(pkSurface,new PK_UV_t(u,v),2,2,PK_LOGICAL_false,expected),"PK surface jets");for(var j=0;j<9;j++)for(var i=0;i<3;i++)Equal(actual[j].coord[i],expected[j].coord[i],$"surface jet{j}[{i}]");}

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
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using M=ProjectGmKernel.Native.Generated;

var benchRepeat=1;var benchOption=args.SingleOrDefault(x=>x.StartsWith("--bench-repeat=",StringComparison.Ordinal));
if(benchOption!=null)benchRepeat=int.Parse(benchOption["--bench-repeat=".Length..],CultureInfo.InvariantCulture);
Require(benchRepeat>=1&&benchRepeat<=64,"bench repeat must be in 1..64");
ModelingPerformance.Enabled=args.Contains("--bench-profile",StringComparer.Ordinal);
KernelRuntime.ForbidLegacyRegularBlendAssembly=args.Contains("--event-arrangement",StringComparer.Ordinal);
var cutArg=args.SingleOrDefault(x=>x.StartsWith("--cut-x=",StringComparison.Ordinal));
var cutX=cutArg==null?.5:double.Parse(cutArg[8..],CultureInfo.InvariantCulture);
Require(cutX>0&&cutX<1,"the cut station must be inside the seed's X range");
var requireCutSection=args.Contains("--require-cut-section",StringComparer.Ordinal);
var remoteCapBore=args.Contains("--remote-cap-bore",StringComparer.Ordinal);
var coincidentCylinderWell=args.Contains("--coincident-cylinder-well",StringComparer.Ordinal);
Require(!remoteCapBore||args.Contains("plane-nurbs",StringComparer.Ordinal)&&!args.Contains("--remote-bore",StringComparer.Ordinal),
    "cap bore uses the plane/NURBS seed and is separate from the side bore");
Require(!coincidentCylinderWell||KernelRuntime.ForbidLegacyRegularBlendAssembly&&args.Contains("affine-plane-nurbs",StringComparer.Ordinal),
    "the coincident cylindrical well needs the guarded affine fixture");
Require(!requireCutSection||KernelRuntime.ForbidLegacyRegularBlendAssembly&&args.Any(x=>x is "affine-plane-nurbs" or "plane-nurbs")
    &&!args.Any(x=>x is "rounding-only" or "lifecycle-only" or "native-only"),"cut section check needs a guarded plane/NURBS fillet and Boolean");
var sectionWitnesses=0;var traceCutSection=false;
var wellTrace=coincidentCylinderWell?new List<KernelRuntime.BoundaryTrace>():null;
var cutCharts=new HashSet<(int Curve,int Generation)>();var cutCrossings=new HashSet<(int Curve,int Generation)>();
var cutProducts=new HashSet<(int Curve,int Generation)>();var cutEnds=new HashSet<(int Curve,int Event)>();
if(KernelRuntime.ForbidLegacyRegularBlendAssembly)
    KernelRuntime.TraceFiniteBoundary=trace=>
    {
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.AnalyticSectionEnds)sectionWitnesses++;
        if(wellTrace!=null&&trace.Stage is KernelRuntime.BoundaryTraceStage.CarrierReuse or KernelRuntime.BoundaryTraceStage.Product)wellTrace.Add(trace);
        if(traceCutSection)
        {
            var key=(trace.Curve,trace.Generation);
            if(trace.Stage==KernelRuntime.BoundaryTraceStage.NativeSectionChart)cutCharts.Add(key);
            if(trace.Stage==KernelRuntime.BoundaryTraceStage.NativeSectionWinding)cutCrossings.Add(key);
            if(trace.Stage==KernelRuntime.BoundaryTraceStage.Product&&trace.ProductCurve>0)cutProducts.Add(key);
            if(trace.Stage==KernelRuntime.BoundaryTraceStage.NativeCutLocator&&trace.First==trace.Last)cutEnds.Add((trace.ProductCurve,trace.StartEvent));
        }
        Console.WriteLine($"BOUNDARY {trace.Stage} source={trace.Source} face={trace.Face} product={trace.Product} surface={trace.Surface} edge={trace.Edge} carrier={trace.Curve}/{trace.Generation} product_curve={trace.ProductCurve} t=[{trace.First:R},{trace.Last:R}] events={trace.StartEvent}->{trace.EndEvent} forward={trace.Forward} source_edge={trace.SourceEdge} source_fin={trace.SourceFin}");
    };
var outputName=args.Single(x=>x.StartsWith("--output-name=",StringComparison.Ordinal))["--output-name=".Length..];
Require(outputName.Length>0&&outputName.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'),"one new output directory name is required");
var directory=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","regular-solid-blend-oracle",outputName);
Require(!Directory.Exists(directory),"Existing execution evidence is immutable");Directory.CreateDirectory(directory);
ParasolidMassProperties.BeginEvidence(directory);
Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
var oracleStarted=Stopwatch.GetTimestamp();
#if DEBUG
Console.WriteLine("CONFIG Debug managed oracle/kernel; independent native PK runtime");
#else
Console.WriteLine("CONFIG Release managed oracle/kernel; independent native PK runtime");
#endif
if(!ParasolidScriptHost.TryStartSession("Independent regular freeform solid fillet",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"own session");
    try
    {
        if(wellTrace!=null)CheckCoincidentCylinderWell(directory,wellTrace);
        else if(args.Contains("native-only",StringComparer.Ordinal))
        {
            var concave=args.Contains("concave",StringComparer.Ordinal);var wall=args.Contains("wall",StringComparer.Ordinal);
            var native=Roof(false,.4,wall,concave);CheckBody(native,"native input");var blend=Fillet(false,native,.1,concave);CheckBody(native,"native fillet");
            int type;Check(PK_ENTITY_ask_class(blend,&type),"native fillet surface class");Console.WriteLine($"native fillet support class={type}");DumpEdges(native);
            var stem="native-v261-"+(concave?"concave-":"")+(wall?"nurbs-nurbs-":"")+"fillet";DumpNativeXt(native,directory,stem);
            native=Cut(false,native,cutX);CheckBody(native,"native fillet cut");Console.WriteLine("NATIVE CUT EDGES");DumpEdges(native);DumpNativeXt(native,directory,stem+"-cut");Console.WriteLine("PASS native fixture and subsequent cutting");
        }
        else
        {
            var failures=0;var cases=0;var roundingOnly=args.Contains("rounding-only",StringComparer.Ordinal);var lifecycleOnly=args.Contains("lifecycle-only",StringComparer.Ordinal);
            foreach(var (name,bulge,radius,wall,concave) in new[]{("affine-plane-nurbs",0.0,.1,false,false),("plane-nurbs",.4,.1,false,false),("depressed-roof",-.2,.15,false,false),("nurbs-nurbs",.4,.1,true,false),("concave-plane-nurbs",.4,.1,false,true),("concave-nurbs-nurbs",.4,.1,true,true)})
            {
                if(name=="affine-plane-nurbs"&&!args.Contains(name,StringComparer.Ordinal))continue;
                if(args.Length!=0&&!args.Contains(name,StringComparer.Ordinal))continue;
                if(lifecycleOnly&&name!="plane-nurbs")continue;
                for(var benchRep=0;benchRep<benchRepeat;benchRep++)
                {
                cases++;
                Console.WriteLine($"CASE {name}, radius={radius:R}, fix.update=v261");
                File.Delete(Path.Combine(directory,name+"-fillet.x_t"));File.Delete(Path.Combine(directory,name+"-fillet-cut.x_t"));
                var caseStarted=Stopwatch.GetTimestamp();
                try
                {
                    var own=Roof(true,bulge,wall,concave);var reference=Roof(false,bulge,wall,concave);CheckBody(reference,name+" reference input");
                    if(args.Contains("--remote-bore",StringComparer.Ordinal)){own=Bore(true,own);reference=Bore(false,reference);CheckBody(reference,name+" bored reference input");}
                    if(remoteCapBore){own=Bore(true,own,true);reference=Bore(false,reference,true);CheckCapBoreRings(own,reference);}
                    var nativeBlend=Fillet(false,reference,radius,concave);CheckBody(reference,name+" reference fillet");
                    sectionWitnesses=0;
                    var ownBlend=Fillet(true,own,radius,concave);
                    if(remoteCapBore)CheckCapBoreRings(own,reference);
                    if(name=="affine-plane-nurbs"&&KernelRuntime.ForbidLegacyRegularBlendAssembly)
                        Require(sectionWitnesses==2,"both affine cap boundaries have known native section limits");
                    if(!lifecycleOnly)
                    {
                        try{ValidateBlend(own,ownBlend,nativeBlend,radius);}catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+" geometry: "+ex.Message);}
                        try{CompareFresh(own,reference,name+"-fillet",directory);}catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+" structure: "+ex.Message);}
                        if(args.Contains("--contact-query-controls",StringComparer.Ordinal))
                            try{ContactQueries(own,reference,directory,name);}catch(Exception ex){failures++;Console.WriteLine("FAIL finite contact queries: "+ex.Message);}
                    }
                    if(name=="plane-nurbs"&&(!roundingOnly||lifecycleOnly))
                    {
                        try{CheckLifecycle(own,reference,directory);}catch(Exception ex){failures++;Console.WriteLine("FAIL independent lifecycle: "+ex.Message);}
                    }
                    if(!roundingOnly&&!lifecycleOnly)
                    {
                        try
                        {
                            if(remoteCapBore)
                            {
                                var reloaded=ReceiveManaged(Transmit(own));Check(KernelRuntime.EntityDelete(1,&own),"delete the pre-reload bored fillet");own=reloaded;
                                CheckCapBoreRings(own,reference);
                            }
                            reference=Cut(false,reference,cutX);CheckBody(reference,name+" reference fillet cut");
                            var verifySection=requireCutSection&&(name is "affine-plane-nurbs" or "plane-nurbs");
                            traceCutSection=verifySection;
                            if(traceCutSection){cutCharts.Clear();cutCrossings.Clear();cutProducts.Clear();cutEnds.Clear();}
                            var savedProofGuard=KernelRuntime.ForbidSpatialBoundaryReuse;
                            if(verifySection)KernelRuntime.ForbidSpatialBoundaryReuse=true;
                            try{own=Cut(true,own,cutX);}
                            finally{KernelRuntime.ForbidSpatialBoundaryReuse=savedProofGuard;traceCutSection=false;}
                            if(remoteCapBore)CheckCapBoreRings(own,reference);
                            if(verifySection)
                                Require(cutCharts.Any(key=>cutCrossings.Contains(key)&&cutProducts.Contains(key)
                                    &&cutEnds.Count(end=>end.Curve==key.Curve)==2),
                                    "the managed Boolean cut must use one section chart for both exact endpoints, halfedges, winding and the published product");
                            CompareFresh(own,reference,name+"-fillet-cut",directory);
                        }
                        catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+" subsequent cut: "+ex.Message);}
                    }
                }
                catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+" construction: "+ex.Message);}
                Console.WriteLine($"TIME case {name} ms={Stopwatch.GetElapsedTime(caseStarted).TotalMilliseconds:F3}");
                }
            }
            Require(cases>0,"no matching freeform solid fillet cases");Require(failures==0,$"{failures} freeform solid fillet checks failed");
            Console.WriteLine(lifecycleOnly?"PASS independent freeform solid fillet lifecycle":$"PASS all {cases} freeform solid fillet{(roundingOnly?"":" and subsequent cutting")} cases");
        }
    }
    finally{Check(KernelRuntime.SessionStop(),"own session stop");}
}
Console.WriteLine($"TIME total ms={Stopwatch.GetElapsedTime(oracleStarted).TotalMilliseconds:F3}");
if(ParasolidMassProperties.UnresolvedCount!=0)Environment.ExitCode=2;

static unsafe void ContactQueries(int source,int reference,string output,string label)
{
    var results=new JsonArray();
    foreach(var scale in new[]{100.0,200.0})
    {
        int own,independent;Check(KernelRuntime.EntityCopy(source,&own),"copy query product");Check(PK_ENTITY_copy(reference,&independent),"copy independent query product");
        var matrix=new M.PK_TRANSF_sf_s();matrix.matrix[0]=matrix.matrix[5]=matrix.matrix[10]=scale;matrix.matrix[15]=1;int transform,nativeTransform;
        Check(KernelRuntime.TransfCreate(&matrix,&transform),"query scale transform");Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&matrix,&nativeTransform),"independent query scale transform");
        try
        {
            int count;int* replaced;byte* exact;PK_LOGICAL_t* nativeExact;
            Check(KernelRuntime.BodyTransform(own,transform,0,&count,&replaced,&exact),"scale query product");Free(true,replaced);Free(true,exact);
            Check(PK_BODY_transform(independent,nativeTransform,0,&count,&replaced,&nativeExact),"scale independent product");Free(false,replaced);Free(false,nativeExact);
            var actualBytes=Transmit(own);var fresh=Receive(actualBytes);
            File.WriteAllBytes(Path.Combine(output,$"{label}-query-scale-{scale:R}-gm.x_t"),actualBytes);
            File.WriteAllBytes(Path.Combine(output,$"{label}-query-scale-{scale:R}-pk.x_t"),TransmitNative(independent));
            try
            {
                CheckBody(independent,"independent scaled query body");CheckBody(fresh,"fresh scaled query body");
                ParasolidBodyCompare.AssertEquivalent(independent,fresh,"scaled query product");
                var body=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(own).Slot];var edge=body.FirstEdgeBody;
                for(var i=0;i<body.EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
                {
                    var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[edge].CurveTag)];if(curve.Class!=CurveClass.ICurve)continue;
                    var contact=KernelRuntime.ICurveDataPool[curve.DataIndex];if(contact.IsBlendContact==0)continue;
                    var sf=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(contact.Surface1Tag)];var data=KernelRuntime.BlendedEdgeDataPool[sf.DataIndex];
                    var root=KernelRuntime.GetNestedBlendProgram(data).Surfaces[data.ProgramRoot];var side=root.ReverseSection?contact.BlendContactSide:1-contact.BlendContactSide;
                    var uv=new M.PK_UV_s();uv.param[0]=(sf.UMin+sf.UMax)*.5;uv.param[1]=side==0?5e-9:1-5e-9;var point=new M.PK_VECTOR_s();
                    Check(KernelRuntime.SurfEval(sf.Header.Tag,uv,0,0,0,&point),"actual finite face query point");
                    int ownCode,ownTop,ownClass,nativeCode,nativeTop,nativeClass,referenceCode,referenceTop,referenceClass;
                    var ownError=KernelRuntime.BodyContainsVector(own,point,&ownCode,&ownTop);var nativeError=PK_BODY_contains_vector(fresh,*(PK_VECTOR_t*)&point,&nativeCode,&nativeTop);
                    var referenceError=PK_BODY_contains_vector(independent,*(PK_VECTOR_t*)&point,&referenceCode,&referenceTop);
                    Check(ownError,"GM contact-near face query");Check(nativeError,"PK same-product contact-near face query");Check(referenceError,"PK independent contact-near face query");
                    Check(KernelRuntime.EntityAskClass(ownTop,&ownClass),"GM query topology class");Check(PK_ENTITY_ask_class(nativeTop,&nativeClass),"PK same-product query topology class");Check(PK_ENTITY_ask_class(referenceTop,&referenceClass),"PK independent query topology class");
                    results.Add((JsonNode)new JsonObject{["scale"]=scale,["surface"]=sf.Header.Tag,["curve"]=curve.Header.Tag,["x"]=point.coord[0],["y"]=point.coord[1],["z"]=point.coord[2],
                        ["gmCode"]=ownCode,["gmTopologyClass"]=ownClass,["sameProductPkCode"]=nativeCode,["sameProductPkTopologyClass"]=nativeClass,["independentPkCode"]=referenceCode,["independentPkTopologyClass"]=referenceClass});
                    Require(ownCode==PK_enclosure_on_c&&nativeCode==ownCode&&referenceCode==ownCode&&ownClass==PK_CLASS_face&&nativeClass==ownClass&&referenceClass==ownClass,"finite contact query face topology agreement");
                }
            }
            finally{Check(PK_ENTITY_delete(1,&fresh),"delete fresh scaled query body");}
        }
        finally{Check(KernelRuntime.EntityDelete(1,&own),"delete scaled query body");Check(PK_ENTITY_delete(1,&independent),"delete scaled independent body");Check(KernelRuntime.EntityDelete(1,&transform),"delete query transform");Check(PK_ENTITY_delete(1,&nativeTransform),"delete independent query transform");}
    }
    Require(results.Count>0,"finite contact query cases required");
    File.WriteAllText(Path.Combine(output,label+"-contact-query-controls.json"),results.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"FINITE_CONTACT_QUERY_PASS cases={results.Count} actual finite face topology matches independent and same-product PK");
}

static unsafe int Roof(bool own,double bulge=.4,bool curvedWall=false,bool concave=false)
{
    var body=Block(own,.5,.5,0,1,1,1);if(concave)body=Notch(own,body);int n;int* faces;
    Check(own?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"input faces");var top=0;var wall=0;
    try
    {
        for(var i=0;i<n;i++)
        {
            int surface;Check(own?KernelRuntime.FaceAskSurf(faces[i],&surface):PK_FACE_ask_surf(faces[i],&surface),"face support");PK_PLANE_sf_t plane;
            Check(own?KernelRuntime.PlaneAsk(surface,(M.PK_PLANE_sf_s*)&plane):PK_PLANE_ask(surface,&plane),"input plane");
            if(Math.Abs(plane.basis_set.axis.coord[2])>.9&&Math.Abs(plane.basis_set.location.coord[2]-(concave?.5:1))<1e-10)top=faces[i];
            if(Math.Abs(plane.basis_set.axis.coord[1])>.9&&Math.Abs(plane.basis_set.location.coord[1]-(concave?.5:0))<1e-10)wall=faces[i];
        }
    }
    finally{Free(own,faces);}
    Require(top!=0,"top plane");double* poles=stackalloc double[45];double* knots=stackalloc double[2]{0,1};int* um=stackalloc int[2]{5,5};int* vm=stackalloc int[2]{3,3};
    for(var i=0;i<5;i++)for(var j=0;j<3;j++){var at=3*(i*3+j);poles[at]=i/4.0;poles[at+1]=(concave?.5:0)+(concave?.5:1)*j/2.0;poles[at+2]=(concave?.5:1)+(i==2&&j==1?bulge:0);}
    var sf=new PK_BSURF_sf_t{u_degree=4,v_degree=2,n_u_vertices=5,n_v_vertices=3,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,
        u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,
        self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};int roof;byte sense=1;
    Check(own?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&roof):PK_BSURF_create(&sf,&roof),"roof surface");
    Check(own?KernelRuntime.TopologyDetachGeometry(top):PK_TOPOL_detach_geom(top),"detach top support");
    Check(own?KernelRuntime.FaceAttachSurfaces(1,&top,&roof,&sense):PK_FACE_attach_surfs(1,&top,&roof,(PK_LOGICAL_t*)&sense),"attach freeform roof");
    if(curvedWall)
    {
        for(var i=0;i<5;i++)for(var j=0;j<3;j++){var at=3*(i*3+j);poles[at]=i/4.0;poles[at+1]=(concave?.5:0)+(i==2&&j==1?(concave?-.15:-.3):0);poles[at+2]=(concave?.5:0)+(concave?.5:1)*j/2.0;}
        int surface;Check(own?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&surface):PK_BSURF_create(&sf,&surface),"curved wall surface");
        Check(own?KernelRuntime.TopologyDetachGeometry(wall):PK_TOPOL_detach_geom(wall),"detach wall support");
        var wallSense=concave?(byte)0:(byte)1;
        Check(own?KernelRuntime.FaceAttachSurfaces(1,&wall,&surface,&wallSense):PK_FACE_attach_surfs(1,&wall,&surface,(PK_LOGICAL_t*)&wallSense),"attach curved wall");
    }
    return body;
}

static unsafe int Fillet(bool own,int body,double radius,bool concave=false)
{
    int n;int* edges;Check(own?KernelRuntime.BodyAskEdges(body,&n,&edges):PK_BODY_ask_edges(body,&n,&edges),"input edges");var selected=0;
    int* vertices=stackalloc int[2];PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
    try
    {
        for(var e=0;e<n;e++)
        {
            Check(own?KernelRuntime.EdgeAskVertices(edges[e],vertices):PK_EDGE_ask_vertices(edges[e],vertices),"edge vertices");
            if(vertices[0]==0||vertices[1]==0)continue;
            for(var v=0;v<2;v++)
            {
                int point;Check(own?KernelRuntime.VertexAskPoint(vertices[v],&point):PK_VERTEX_ask_point(vertices[v],&point),"vertex point");PK_POINT_sf_t sf;
                Check(own?KernelRuntime.PointAsk(point,(M.PK_POINT_sf_s*)&sf):PK_POINT_ask(point,&sf),"point definition");ends[v]=sf.position;
            }
            var y=concave?.5:0;var z=concave?.5:1;
            if(Math.Abs(ends[0].coord[1]-y)+Math.Abs(ends[1].coord[1]-y)+Math.Abs(ends[0].coord[2]-z)+Math.Abs(ends[1].coord[2]-z)<1e-10)
            {Require(selected==0,"unambiguous selected edge");selected=edges[e];}
        }
    }
    finally{Free(own,edges);}
    Require(selected!=0,"selected freeform edge");var options=new PK_EDGE_set_blend_constant_o_t();options.properties.tolerance=1e-7;int count;int* marked;
    Check(own?KernelRuntime.EdgeSetBlendConstant(1,&selected,radius,(M.PK_EDGE_set_blend_constant_o_s*)&options,&count,&marked):PK_EDGE_set_blend_constant(1,&selected,radius,&options,&count,&marked),"set constant blend");
    try{Require(count==1,"one marked edge");}finally{Free(own,marked);}
    // Pin a comparable procedural representation. Modern default fixing fits
    // this case to a B-surface; v261 preserves the dependent rolling Blend.
    var fix=new PK_BODY_fix_blends_o_t{tolerance=1e-7,local_check=PK_LOGICAL_true,update=PK_blend_edge_update_v261_c};int* blends;int fault,fe,ft;
    WebProgressCapture.Before(own,body,[selected]);
    WebProgressCapture.BlendParameters(own,fix,radius,options.properties.tolerance);
    var started=Stopwatch.GetTimestamp();
    PK_FACE_array_t* webUnders=null;int* webTopols=null;
    var error=own?KernelRuntime.BodyFixBlends(body,(M.PK_BODY_fix_blends_o_s*)&fix,&count,&blends,WebProgressCapture.Enabled?(M.PK_FACE_array_s**)&webUnders:null,WebProgressCapture.Enabled?&webTopols:null,&fault,&fe,&ft):PK_BODY_fix_blends(body,&fix,&count,&blends,WebProgressCapture.Enabled?&webUnders:null,WebProgressCapture.Enabled?&webTopols:null,&fault,&fe,&ft);
    var webElapsed=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    WebProgressCapture.After(own,body,webElapsed,error,count,blends,fault,webUnders,webTopols);
    WebProgressCapture.FreeBlendTracking(own,count,webUnders,webTopols);
    if(own&&ModelingPerformance.Enabled)PrintProfile("FixBlends",webElapsed);
    Console.WriteLine($"TIME {(own?"self":"PK")} BODY_fix_blends radius={radius:R} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}, error={error}");
    Console.WriteLine($"{(own?"own":"native")} fix error={error}, count={count}, fault={fault}, edge={fe}, topol={ft}");Check(error,"fix constant blend");
    try
    {
        Require(fault==PK_blend_fault_no_fault_c&&count==1,"one fixed blend without fault");int surface;
        Check(own?KernelRuntime.FaceAskSurf(blends[0],&surface):PK_FACE_ask_surf(blends[0],&surface),"result Blend support");return surface;
    }
    finally{Free(own,blends);}
}

static unsafe void ValidateBlend(int body,int own,int native,double radius)
{
    var sf=new M.PK_BLENDSF_sf_s();Check(KernelRuntime.BlendSfAsk(own,&sf),"own procedural Blend definition");
    Require(Math.Abs(Math.Abs(sf.radii[0])-radius)<1e-12&&Math.Abs(Math.Abs(sf.radii[1])-radius)<1e-12,"constant radius definition");
    M.PK_VECTOR_s* blendJet=stackalloc M.PK_VECTOR_s[4];M.PK_VECTOR_s* supportJet=stackalloc M.PK_VECTOR_s[4];
    PK_VECTOR_t* expectedJet=stackalloc PK_VECTOR_t[1];var lowSupport=0;var highSupport=0;
    var finite=FiniteBlendRange(body,own);
    for(var i=1;i<=9;i++)
    {
        var u=finite.First+(finite.Last-finite.First)*i/10;var center=new M.PK_VECTOR_s();
        Check(KernelRuntime.CurveEval(sf.spine,u,0,&center),"rolling center");var firstContactSupport=0;
        for(var side=0;side<=2;side++)
        {
            var uv=new M.PK_UV_s();uv.param[0]=u;uv.param[1]=side*.5;
            Check(KernelRuntime.SurfEval(own,uv,1,1,0,blendJet),"own Blend jet");
            Require(Math.Abs(Distance(*(PK_VECTOR_t*)&center,*(PK_VECTOR_t*)blendJet)-radius)<1e-8,"rolling section radius");
            var nativeUV=new PK_UV_t();Check(PK_SURF_parameterise_vector(native,*(PK_VECTOR_t*)blendJet,&nativeUV),"independent native Blend contact");
            Check(PK_SURF_eval(native,nativeUV,0,0,PK_LOGICAL_false,expectedJet),"native corresponding Blend position");
            Require(Distance(*(PK_VECTOR_t*)blendJet,expectedJet[0])<1e-7,"independent Blend geometry correspondence");
            if(side==1)continue;
            var supportUV=new M.PK_UV_s();var support=side==0?lowSupport:highSupport;
            // Resolve the section's support assignment once, then verify the
            // position and normal on both real supports at every section.
            if(support==0)for(var candidate=0;candidate<2;candidate++)
            {
                var tag=candidate==0?sf.geom_1:sf.geom_2;var error=ContactParameters(tag,blendJet[0],out var at);
                if(error==PK_ERROR_not_on_surface)continue;Check(error,"contact support inverse");
                support=tag;supportUV=at;lowSupport=tag;highSupport=tag==sf.geom_1?sf.geom_2:sf.geom_1;break;
            }
            else Check(ContactParameters(support,blendJet[0],out supportUV),"contact support parameters");
            Require(support!=0,"contact on actual support");
            if(side==0)firstContactSupport=support;else Require(support!=firstContactSupport,"contacts belong to opposite supports");
            Check(KernelRuntime.SurfEval(support,supportUV,1,1,0,supportJet),"contact support jet");
            Require(Distance(*(PK_VECTOR_t*)blendJet,*(PK_VECTOR_t*)supportJet)<1e-8,"contact position");
            var a=Cross(*(PK_VECTOR_t*)(blendJet+2),*(PK_VECTOR_t*)(blendJet+1));var b=Cross(*(PK_VECTOR_t*)(supportJet+2),*(PK_VECTOR_t*)(supportJet+1));
            var cosine=Dot(a,b)/Math.Sqrt(Dot(a,a)*Dot(b,b));Require(Math.Abs(cosine)>1-1e-7,"tangent continuity at contact");
        }
    }
    Console.WriteLine("PASS 9 sections: radius, both contact positions and tangent continuity, independent native geometry");
}
static unsafe (double First,double Last) FiniteBlendRange(int body,int surface)
{
    var owner=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];var face=owner.FirstFaceBody;var found=-1;
    for(var f=0;f<owner.FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)if(KernelRuntime.Faces[face].SurfTag==surface)found=face;
    Require(found>=0,"actual finite Blend face");Span<double> ranges=stackalloc double[4];var contacts=0;var loop=KernelRuntime.Faces[found].FirstLoop;
    for(var l=0;l<KernelRuntime.Faces[found].LoopCount;l++,loop=KernelRuntime.Loops[loop].NextInFace)
    {
        var fin=KernelRuntime.Loops[loop].FirstFin;
        for(var e=0;e<KernelRuntime.Loops[loop].FinCount;e++,fin=KernelRuntime.Fins[fin].NextInLoop)
        {
            var edgeSlot=KernelRuntime.Fins[fin].Edge;if(edgeSlot<0)continue;var edge=KernelRuntime.Edges[edgeSlot];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(edge.CurveTag)];
            if(curve.Class!=CurveClass.ICurve)continue;var data=KernelRuntime.ICurveDataPool[curve.DataIndex];
            if(data.IsBlendContact==0)continue;
            Require(contacts<2&&edge.HasParameterRange!=0,"two finite contact edges");
            for(var side=0;side<2;side++)
            {
                M.PK_VECTOR_s point;M.PK_UV_s uv;
                Check(KernelRuntime.CurveEval(edge.CurveTag,side==0?edge.ParameterStart:edge.ParameterEnd,0,&point),"actual finite contact endpoint");
                Check(KernelRuntime.SurfParameteriseVector(surface,point,&uv),"finite contact on own Blend");ranges[2*contacts+side]=uv.param[0];
            }
            if(ranges[2*contacts]>ranges[2*contacts+1])(ranges[2*contacts],ranges[2*contacts+1])=(ranges[2*contacts+1],ranges[2*contacts]);contacts++;
        }
    }
    Require(contacts==2&&Math.Abs(ranges[0]-ranges[2])<1e-8&&Math.Abs(ranges[1]-ranges[3])<1e-8,$"this fixture has constant-u finite cap sections: contacts={contacts}, intervals=[{ranges[0]:R},{ranges[1]:R}]/[{ranges[2]:R},{ranges[3]:R}]");
    var first=Math.Max(ranges[0],ranges[2]);var last=Math.Min(ranges[1],ranges[3]);Require(last>first,"positive finite Blend extent");
    Console.WriteLine($"FINITE Blend sampling extent=[{first:R},{last:R}] from actual contact edges");return(first,last);
}
static unsafe int ContactParameters(int surface,M.PK_VECTOR_s point,out M.PK_UV_s uv)
{
    uv=default;
    if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)].Class!=SurfaceClass.BSurface)
    {fixed(M.PK_UV_s* result=&uv)return KernelRuntime.SurfParameteriseVector(surface,point,result);}
    // These manufactured supports have x=u and a linear y(v) for roofs or
    // z(v) for walls. Verify the actual surface point after recovering UV,
    // independently of the rolling solver's stored contact parameters.
    var aUV=new M.PK_UV_s();var bUV=new M.PK_UV_s();bUV.param[1]=1;M.PK_VECTOR_s a,b;
    var error=KernelRuntime.SurfEval(surface,aUV,0,0,0,&a);if(error!=0)return error;
    error=KernelRuntime.SurfEval(surface,bUV,0,0,0,&b);if(error!=0)return error;
    var axis=Math.Abs(b.coord[1]-a.coord[1])>Math.Abs(b.coord[2]-a.coord[2])?1:2;
    uv.param[0]=point.coord[0];uv.param[1]=(point.coord[axis]-a.coord[axis])/(b.coord[axis]-a.coord[axis]);
    if(uv.param[0]<0||uv.param[0]>1||uv.param[1]<0||uv.param[1]>1)return PK_ERROR_not_on_surface;
    M.PK_VECTOR_s evaluated;error=KernelRuntime.SurfEval(surface,uv,0,0,0,&evaluated);if(error!=0)return error;
    return Distance(*(PK_VECTOR_t*)&point,*(PK_VECTOR_t*)&evaluated)<1e-8?0:PK_ERROR_not_on_surface;
}

static unsafe void CheckCoincidentCylinderWell(string directory,List<KernelRuntime.BoundaryTrace> trace)
{
    foreach(var (name,x,y,bottom,radius,phase) in new[]{("centered",.5,.5,.25,.18,false),("offset",.375,.625,.375,.125,true)})
    {
        var label="affine-cylinder-well-"+name;var own=Roof(true,0);var reference=Roof(false,0);
        var basis=new PK_AXIS2_sf_t{location=new(x,y,bottom),axis=new(0,0,1),ref_direction=phase?new(0,1,0):new(1,0,0)};
        int ownTool,nativeTool;Check(KernelRuntime.BodyCreateSolidCyl(radius,1-bottom,(M.PK_AXIS2_sf_s*)&basis,&ownTool),"self well tool");
        Check(PK_BODY_create_solid_cyl(radius,1-bottom,&basis,&nativeTool),"independent well tool");
        if(phase){own=Reload(own);ownTool=Reload(ownTool);}
        var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};int n;int* bodies;
        Check(PK_BODY_boolean(reference,1,&nativeTool,&options,&n,&bodies),"independent well subtraction");Require(n==1,"one native well");
        reference=bodies[0];Free(false,bodies);
        trace.Clear();var guard=KernelRuntime.ForbidSpatialBoundaryReuse;KernelRuntime.ForbidSpatialBoundaryReuse=true;
        try{Check(KernelRuntime.BodyBoolean(own,1,&ownTool,(M.PK_BODY_boolean_o_s*)&options,&n,&bodies),"guarded self well subtraction");}
        finally{KernelRuntime.ForbidSpatialBoundaryReuse=guard;}
        Require(n==1&&KernelRuntime.LastRegularBooleanStage==KernelRuntime.RegularBooleanStage.Publish,"one arranged well");
        own=bodies[0];Free(true,bodies);
        var reuse=trace.Single(t=>t.Stage==KernelRuntime.BoundaryTraceStage.CarrierReuse&&t.SourceEdge>=0&&t.StartEvent<0&&t.EndEvent<0);
        var uses=trace.Where(t=>t.Stage==KernelRuntime.BoundaryTraceStage.Product&&t.Curve==reuse.Curve&&t.Generation==reuse.Generation
            &&t.First==reuse.First&&t.Last==reuse.Last&&t.StartEvent<0&&t.EndEvent<0).ToArray();
        Require(uses.Length==2&&uses[0].ProductCurve>0&&uses[0].ProductCurve==uses[1].ProductCurve
            &&uses[0].Forward!=uses[1].Forward&&uses.Any(t=>t.Source==reuse.Source),"two opposite published uses of the original vertexless ring");
        CompareFresh(own,reference,label,directory);
        foreach(var (z,classification) in new[]{((1+bottom)*.5,PK_enclosure_outside_c),(bottom*.5,PK_enclosure_inside_c)})
        {
            var query=new PK_VECTOR_t(x,y,z);int expected,actual,entity;
            Check(PK_BODY_contains_vector(reference,query,&expected,&entity),"native well material");
            Check(KernelRuntime.BodyContainsVector(own,*(M.PK_VECTOR_s*)&query,&actual,&entity),"self well material");
            Require(expected==classification&&actual==expected,"well material above and below the floor");
        }
        Check(KernelRuntime.EntityDelete(1,&own),"delete self well");Check(PK_ENTITY_delete(1,&reference),"delete native well");
        Console.WriteLine("PASS "+label);
    }

    static int Reload(int body)
    {
        var received=ReceiveManaged(Transmit(body));Check(KernelRuntime.EntityDelete(1,&body),"delete the pre-reload well input");return received;
    }
}

static unsafe int Bore(bool own,int body,bool acrossCaps=false)
{
    var basis=acrossCaps
        ?new PK_AXIS2_sf_t{location=new(-1,.45,.35),axis=new(1,0,0),ref_direction=new(0,1,0)}
        :new PK_AXIS2_sf_t{location=new(.3,-1,.35),axis=new(0,1,0),ref_direction=new(1,0,0)};
    int tool;
    Check(own?KernelRuntime.BodyCreateSolidCyl(.06,3,(M.PK_AXIS2_sf_s*)&basis,&tool):PK_BODY_create_solid_cyl(.06,3,&basis,&tool),"remote bore tool");
    var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};int n;int* bodies;
    Check(own?KernelRuntime.BodyBoolean(body,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&bodies):PK_BODY_boolean(body,1,&tool,&options,&n,&bodies),"remote support bore");
    try{Require(n==1,"one bored input");return bodies[0];}finally{Free(own,bodies);}
}

static unsafe void CheckCapBoreRings(int own,int reference)
{
    foreach(var managed in new[]{true,false})
    {
        int count;int* faces;var body=managed?own:reference;
        Check(managed?KernelRuntime.BodyAskFaces(body,&count,&faces):PK_BODY_ask_faces(body,&count,&faces),"cap bore faces");
        var rings=0;
        try{for(var i=0;i<count;i++)
        {
            int support,type;Check(managed?KernelRuntime.FaceAskSurf(faces[i],&support):PK_FACE_ask_surf(faces[i],&support),"cap bore support");
            Check(managed?KernelRuntime.EntityAskClass(support,&type):PK_ENTITY_ask_class(support,&type),"cap bore support class");
            if(type!=PK_CLASS_plane)continue;
            PK_PLANE_sf_t plane;Check(managed?KernelRuntime.PlaneAsk(support,(M.PK_PLANE_sf_s*)&plane):PK_PLANE_ask(support,&plane),"cap bore plane");
            if(Math.Abs(plane.basis_set.axis.coord[0])<.99)continue;
            int loops;int* tags;Check(managed?KernelRuntime.FaceAskLoops(faces[i],&loops,&tags):PK_FACE_ask_loops(faces[i],&loops,&tags),"cap bore loops");
            Free(managed,tags);rings+=loops-1;
        }}finally{Free(managed,faces);}
        Require(rings==2,"both finite cap bore rings are present");
    }
}

static unsafe int Cut(bool own,int body,double x)
{
    // The default station is the symmetry plane of both supports.
    var tool=Block(own,x+.5,.5,-.1,1,2,2);var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};int n;int* bodies;
    WebProgressCapture.Before(own,body,[],"PK_BODY_boolean",[tool]);WebProgressCapture.BooleanParameters(own,options);
    var started=Stopwatch.GetTimestamp();
    var error=own?KernelRuntime.BodyBoolean(body,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&bodies):PK_BODY_boolean(body,1,&tool,&options,&n,&bodies);
    var webMs=Stopwatch.GetElapsedTime(started).TotalMilliseconds;WebProgressCapture.BooleanAfter(own,error==0?new ReadOnlySpan<int>(bodies,n).ToArray():[],webMs,error,0,null);
    Console.WriteLine($"TIME {(own?"self":"PK")} BODY_boolean x={x:R} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}, error={error}");Check(error,"cut filleted solid");
    try{Require(n==1,"one cut solid");return bodies[0];}finally{Free(own,bodies);}
}
static void PrintProfile(string operation,double apiMs)
{
    var p=ModelingPerformance.Last;var milliseconds=1000.0/System.Diagnostics.Stopwatch.Frequency;
    Console.WriteLine($"PROFILE operation={operation} api-ms={apiMs:F3} elapsed-ms={p.ElapsedTicks*milliseconds:F3} pairs={p.SurfacePairs} ssi-cells={p.SsiCells} root-cells={p.RootCells} roots={p.Roots} budget-failures={p.BudgetFailures} winding={p.WindingCalls} bound-requests={p.BoundRequests} bound-evaluations={p.BoundEvaluations} cache-hits={p.BoundCacheHits} failure-cache-hits={p.BoundFailureCacheHits} bound-ms={p.BoundTicks*milliseconds:F3}");
    Console.WriteLine($"WORK jacobians={p.JacobianCalls} directions={p.JacobianDirections} residual-directions={p.ResidualDirections} record-builds={p.JacobianRecordBuilds} record-nodes={p.JacobianRecordNodes} record-rebinds={p.JacobianRecordRebinds} direct-spline-point-columns={p.DirectSplinePointColumns}");
    foreach(var use in Enum.GetValues<RootIsolationUse>())if(p.RootCalls[(int)use]!=0||p.RootCellsByUse[(int)use]!=0)Console.WriteLine($"ROOT_WORK use={use} calls={p.RootCalls[(int)use]} cells={p.RootCellsByUse[(int)use]} bound-failures={p.RootBoundFailures[(int)use]} exclusions={p.RootLevelExclusions[(int)use]} nonmonotone={p.RootNonmonotone[(int)use]}");
    foreach(var stage in Enum.GetValues<KernelRuntime.RegularBooleanStage>())if(p.Stages[(int)stage]!=0)Console.WriteLine($"PROFILE stage={stage} ms={p.Stages[(int)stage]*milliseconds:F3}");
}
static unsafe int Notch(bool own,int body)
{
    var tool=Block(own,.5,.85,.5,2,.7,1);var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};int n;int* bodies;
    Check(own?KernelRuntime.BodyBoolean(body,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&bodies):PK_BODY_boolean(body,1,&tool,&options,&n,&bodies),"make concave L solid");
    try{Require(n==1,"one concave input solid");return bodies[0];}finally{Free(own,bodies);}
}

static unsafe int Block(bool own,double x,double y,double z,double width,double depth,double height)
{
    var basis=new PK_AXIS2_sf_t{location=new(x,y,z),axis=new(0,0,1),ref_direction=new(1,0,0)};int body;
    Check(own?KernelRuntime.BodyCreateSolidBlock(width,depth,height,(M.PK_AXIS2_sf_s*)&basis,&body):PK_BODY_create_solid_block(width,depth,height,&basis,&body),"block");return body;
}

static unsafe void CompareFresh(int own,int reference,string name,string directory)
{
    var bytes=Transmit(own);File.WriteAllBytes(Path.Combine(directory,name+".x_t"),bytes);var received=Receive(bytes);
    if(WebProgressCapture.Enabled)WebProgressCapture.BindQuality(name,own,reference,received,bytes,WebProgressCapture.NativeSnapshotBytes(reference));
    try{CheckBody(reference,name+" reference");CheckBody(received,name+" received");Compare(reference,received,name);}
    finally{Check(PK_ENTITY_delete(1,&received),"delete independent received solid");}
}
static unsafe int ReceiveManaged(byte[] bytes)
{
    fixed(byte* pointer=bytes)
    {
        var block=new M.PK_MEMORY_block_s{bytes=pointer,n_bytes=(nuint)bytes.Length};var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(KernelRuntime.PartReceiveB(block,(M.PK_PART_receive_o_s*)&options,&n,&parts),"self receive fillet XT");
        try{Require(n==1,"one self received fillet solid");return parts[0];}finally{Free(true,parts);}
    }
}
static unsafe void CheckLifecycle(int own,int reference,string directory)
{
    var started=Stopwatch.GetTimestamp();
    var before=(KernelRuntime.Bodies.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Points.AliveCount,KernelRuntime.ReturnOutstandingBlocks);
    int imported=0,copy=0,referenceCopy=0,transform=0,referenceTransform=0;
    try
    {
        imported=ReceiveManaged(Transmit(own));Require(KernelRuntime.TryResolveBodySlot(imported,out var body)&&!KernelRuntime.Xt.IsOpaque(body),"materialized fillet receive");
        var edge=KernelRuntime.Bodies[body].FirstEdgeBody;
        for(var i=0;i<KernelRuntime.Bodies[body].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[edge].CurveTag)];
            var record=KernelRuntime.Edges[edge];
            if(curve.Class!=CurveClass.Line&&record.HasParameterRange==0)
                Require(record.StartVertex<0&&record.EndVertex<0&&curve.Class is CurveClass.Circle or CurveClass.Ellipse
                    &&Math.Abs(curve.TMax-curve.TMin-Math.Tau)<1e-12,"received complete vertexless conic ring domain");
        }
        Check(KernelRuntime.EntityCopy(imported,&copy),"copy self received fillet");Check(KernelRuntime.EntityDelete(1,&imported),"delete self received source");imported=0;
        Check(PK_ENTITY_copy(reference,&referenceCopy),"copy independent PK fillet");
        var form=new M.PK_TRANSF_sf_s();form.matrix[0]=-2;form.matrix[5]=form.matrix[10]=2;form.matrix[15]=1;form.matrix[3]=3;form.matrix[7]=5;form.matrix[11]=7;
        Check(KernelRuntime.TransfCreate(&form,&transform),"self mirror-scale transform");Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&form,&referenceTransform),"PK mirror-scale transform");
        int n;int* replaced;byte* exact;Check(KernelRuntime.BodyTransform(copy,transform,0,&n,&replaced,&exact),"self copied fillet mirror-scale");Free(true,replaced);Free(true,exact);
        PK_LOGICAL_t* nativeExact;Check(PK_BODY_transform(referenceCopy,referenceTransform,0,&n,&replaced,&nativeExact),"independent PK copied fillet mirror-scale");Free(false,replaced);Free(false,nativeExact);
        DumpNativeXt(referenceCopy,directory,"plane-nurbs-lifecycle-reference");
        var query=new PK_VECTOR_t(2.2,5.8,8);int actual,expected,topology;
        Check(KernelRuntime.BodyContainsVector(copy,*(M.PK_VECTOR_s*)&query,&actual,&topology),"self lifecycle material classification");
        Check(PK_BODY_contains_vector(referenceCopy,query,&expected,&topology),"PK lifecycle material classification");Require(actual==expected&&actual==PK_enclosure_inside_c,"lifecycle material classification");
        CompareFresh(copy,referenceCopy,"plane-nurbs-lifecycle",directory);
    }
    finally
    {
        if(imported!=0&&KernelRuntime.IsValidTag(imported))Check(KernelRuntime.EntityDelete(1,&imported),"delete lifecycle imported source");
        if(copy!=0&&KernelRuntime.IsValidTag(copy))Check(KernelRuntime.EntityDelete(1,&copy),"delete lifecycle self copy");
        if(transform!=0&&KernelRuntime.IsValidTag(transform))Check(KernelRuntime.EntityDelete(1,&transform),"delete lifecycle self transform");
        if(referenceCopy!=0)Check(PK_ENTITY_delete(1,&referenceCopy),"delete lifecycle PK copy");
        if(referenceTransform!=0)Check(PK_ENTITY_delete(1,&referenceTransform),"delete lifecycle PK transform");
    }
    Require(before==(KernelRuntime.Bodies.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Points.AliveCount,KernelRuntime.ReturnOutstandingBlocks),"lifecycle geometry and return memory ownership");
    Console.WriteLine($"TIME independent lifecycle ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}");
    Console.WriteLine("PASS independent lifecycle: self XT receive, copy, source deletion, mirror-scale, PK full check/strict compare/mass, cleanup");
}

static unsafe byte[] Transmit(int body)
{
    var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=PK_transmit_format_text_c,transmit_version=371,transmit_meshes=PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s block;
    Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"own text XT transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(KernelRuntime.MemoryBlockFree(&block),"XT buffers");}
}
static unsafe void DumpNativeXt(int body,string directory,string stem)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1,&body,&options,&block),"native text XT transmit");byte[] bytes;
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));bytes=stream.ToArray();}
    finally{Check(PK_MEMORY_block_f(&block),"native XT buffers");}
    File.WriteAllBytes(Path.Combine(directory,stem+".x_t"),bytes);var document=XtText.DecodeDocument(System.Text.Encoding.ASCII.GetString(bytes));
    foreach(var node in document.Nodes)
    {
        var name=document.Schema.GetNode(node.Type).Name;
        if(name is not ("INTERSECTION" or "BLEND_BOUND" or "BLENDED_EDGE" or "CHART" or "LIMIT"))continue;
        Console.WriteLine($"XT {name} #{node.Index}, fields={node.Fields.Length}");
        for(var i=0;i<node.Fields.Length;i++)
        {
            var field=node.Fields[i];Console.WriteLine($"  [{i}] {field.Kind}: ptr={field.Pointer}, int={field.Integer}, real={field.Real:R}, char={(field.Character=='\0'?'-':field.Character)}, vector=({field.Vector.X:R},{field.Vector.Y:R},{field.Vector.Z:R})");
        }
    }
}

static unsafe int Receive(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(PK_PART_receive_b(block,&options,&n,&parts),"real PK receive");try{Require(n==1,"one received solid");return parts[0];}finally{PK_MEMORY_free(parts);}
    }
}
static unsafe byte[] TransmitNative(int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1,&body,&options,&block),"independent query product transmit");
    try{using var bytes=new MemoryStream();for(var p=&block;p!=null;p=p->next)bytes.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return bytes.ToArray();}
    finally{Check(PK_MEMORY_block_f(&block),"free independent query product bytes");}
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
static unsafe void DumpEdges(int body)
{
    int n;int* edges;Check(PK_BODY_ask_edges(body,&n,&edges),"native edge classes");PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
    try
    {
        for(var i=0;i<n;i++)
        {
            int curve,type;PK_LOGICAL_t sense;PK_INTERVAL_t interval;
            Check(PK_EDGE_ask_geometry(edges[i],PK_LOGICAL_true,&curve,&type,ends,&interval,&sense),"native edge geometry");
            Console.WriteLine($"native edge={edges[i]}, class={type}, interval=[{interval.value[0]:R},{interval.value[1]:R}], from=({ends[0].coord[0]:R},{ends[0].coord[1]:R},{ends[0].coord[2]:R}), to=({ends[1].coord[0]:R},{ends[1].coord[1]:R},{ends[1].coord[2]:R})");
        }
    }
    finally{PK_MEMORY_free(edges);}
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
static unsafe PK_VECTOR_t Cross(PK_VECTOR_t a,PK_VECTOR_t b)=>new(a.coord[1]*b.coord[2]-a.coord[2]*b.coord[1],a.coord[2]*b.coord[0]-a.coord[0]*b.coord[2],a.coord[0]*b.coord[1]-a.coord[1]*b.coord[0]);
static unsafe double Dot(PK_VECTOR_t a,PK_VECTOR_t b)=>a.coord[0]*b.coord[0]+a.coord[1]*b.coord[1]+a.coord[2]*b.coord[2];
static unsafe double Distance(PK_VECTOR_t a,PK_VECTOR_t b){var sum=0.0;for(var d=0;d<3;d++)sum+=Math.Pow(a.coord[d]-b.coord[d],2);return Math.Sqrt(sum);}
static unsafe void Free(bool own,void* memory){if(memory!=null)Check(own?KernelRuntime.MemoryFree(memory):PK_MEMORY_free(memory),"return buffer");}
static void Check(int error,string name){if(error!=0)throw new Exception($"{name}: error={error}, blend={KernelRuntime.LastRegularBlendStage}, stage={KernelRuntime.LastRegularBooleanStage}, face={KernelRuntime.LastRegularBooleanFace}, sheet={KernelRuntime.LastSheetPartitionStage}, curve={KernelRuntime.LastSheetCurve}, parameter={KernelRuntime.LastSheetParameter:R}, algorithm={KernelRuntime.LastSheetAlgorithmStatus}");}
static void Require(bool value,string message){if(!value)throw new Exception(message);}
static string ScriptPath([CallerFilePath]string path="")=>path;

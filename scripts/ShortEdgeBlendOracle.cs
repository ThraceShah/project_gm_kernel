#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property UseWebProgressCapture=true
#:property UseRootSectionTrace=true
using static parasolid;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using XtFieldKind=ProjectGmKernel.Xt.XtFieldKind;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using EdgeSlot=System.Int32;
using FaceSlot=System.Int32;
using FinSlot=System.Int32;
using VertexSlot=System.Int32;
Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
RootSectionTrace.Install();
KernelRuntime.ForbidLegacyRegularBlendAssembly=args.Contains("--event-arrangement",StringComparer.Ordinal);
if(KernelRuntime.ForbidLegacyRegularBlendAssembly)
    KernelRuntime.TraceRollingNetwork=trace=>
    {
        if(trace.Stage==KernelRuntime.RollingNetworkStage.ProvedEndpointReuse)
        {Console.WriteLine($"ROLLING PROVED REUSE event={trace.Cell} supports={trace.Surface}/{trace.Other} curve={trace.Curve} t={trace.First:R}/{trace.Last:R}");return;}
        if(trace.Stage==KernelRuntime.RollingNetworkStage.BoundaryIncidence)
        {
            Console.WriteLine($"ROLLING THIRD FACE source={trace.CellSource} surface={trace.Surface} bands={trace.Other}/{trace.OtherBand} curve={trace.Curve} t=[{trace.First:R},{trace.Last:R}] relation={trace.Relation} status={KernelRuntime.LastSheetAlgorithmStatus}");
            return;
        }
        if(trace.Stage is KernelRuntime.RollingNetworkStage.Select or KernelRuntime.RollingNetworkStage.Build)
        {Console.WriteLine($"ROLLING CELL {trace.Stage} cell={trace.Cell} source={trace.CellSource} surface={trace.Surface} material={trace.Material}");return;}
        Console.WriteLine($"ROLLING {trace.Stage} source={trace.Source} supports={trace.FirstSupport}/{trace.SecondSupport} surface={trace.Surface} other={trace.Other} curve={trace.Curve} relation={trace.Relation} vertex={trace.Vertex} t=[{trace.First:R},{trace.Last:R}]");
        if(args.Contains("--diagnostics")&&trace.Stage==KernelRuntime.RollingNetworkStage.BandEvents&&trace.Curve==0)ScalarBoundaryEvidence(trace.Surface,trace.Other);
        if(args.Contains("--diagnostics")&&trace.Stage==KernelRuntime.RollingNetworkStage.Clip&&trace.Curve!=0)
        {
            var record=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(trace.Curve)];
            if(record.Class==CurveClass.Line)
            {
                var line=KernelRuntime.LineDataPool[record.DataIndex];
                Console.WriteLine($"ROLLING LINE origin=({line.LocationX:R},{line.LocationY:R},{line.LocationZ:R}) axis=({line.AxisX:R},{line.AxisY:R},{line.AxisZ:R})");
            }
        }
        var other=KernelRuntime.GetSurfaceSlotByTag(trace.Other);
        if(other>=0&&KernelRuntime.Surfaces[other].Class==SurfaceClass.Plane)
        {
            var plane=KernelRuntime.PlaneDataPool[KernelRuntime.Surfaces[other].DataIndex];
            Console.WriteLine($"ROLLING PLANE origin=({plane.LocationX:R},{plane.LocationY:R},{plane.LocationZ:R}) normal=({plane.NormalX:R},{plane.NormalY:R},{plane.NormalZ:R})");
        }
    };
if(KernelRuntime.ForbidLegacyRegularBlendAssembly)
    KernelRuntime.TraceFiniteBoundary=trace=>
    {
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.CoincidentLineStation)
        {Console.WriteLine($"COINCIDENT LINE event={trace.StartEvent} curve={trace.Curve}/{trace.Generation} occurrence={trace.Edge} t={trace.First:R}");return;}
        if(trace.Stage is KernelRuntime.BoundaryTraceStage.ThirdFaceStation or KernelRuntime.BoundaryTraceStage.ThirdFaceOutsideTrim)
        {Console.WriteLine($"BOUNDARY {trace.Stage} event={trace.StartEvent} curve={trace.Curve}/{trace.Generation} support={trace.Surface}/{trace.OtherSurface} t=[{trace.First:R},{trace.Last:R}] witness={trace.Witness}");return;}
        var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(trace.Surface)];
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.Successor)
        {Console.WriteLine($"SUCCESSOR surface={trace.Surface} edge={trace.Edge} forward={trace.Forward} end={trace.EndEvent} next={trace.NextEdge} angles={trace.StartAngle:R}/{trace.EndAngle:R}");return;}
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.CarrierReuse)
        {Console.WriteLine($"BOUNDARY REUSE surface={trace.Surface} edge={trace.Edge} implicit={trace.Curve} source_line={trace.ProductCurve} t=[{trace.First:R},{trace.Last:R}] events={trace.StartEvent}->{trace.EndEvent} bound={trace.SpatialError:R}");return;}
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.UnresolvedOrder)
        {Console.WriteLine($"BOUNDARY ORDER event={trace.StartEvent} surface={trace.Surface} curves={trace.Curve}/{trace.ProductCurve} angles={trace.StartAngle:R}/{trace.EndAngle:R} curvature=[{trace.First:R},{trace.Last:R}]");return;}
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.NativeCutLocator)
        {Console.WriteLine($"BOUNDARY NATIVE LOCATOR event={trace.StartEvent} source_curve={trace.Curve} cut_curve={trace.ProductCurve} supports={trace.Surface}/{trace.OtherSurface} cut_t=[{trace.First:R},{trace.Last:R}] error={trace.SpatialError:R}");return;}
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.SpatialEndpointReuse)
        {Console.WriteLine($"BOUNDARY SPATIAL REUSE event={trace.StartEvent} supports={trace.Surface}/{trace.OtherSurface} carrier={trace.Curve} t={trace.First:R} xyz=({trace.StartPoint.X:R},{trace.StartPoint.Y:R},{trace.StartPoint.Z:R}) old=({trace.EndPoint.X:R},{trace.EndPoint.Y:R},{trace.EndPoint.Z:R})");return;}
        if(trace.Stage is KernelRuntime.BoundaryTraceStage.NativeSectionChart or KernelRuntime.BoundaryTraceStage.NativeSectionWinding)
        {
            unsafe{Console.WriteLine($"SECTION {trace.Stage} surface={trace.Surface} curve={trace.Curve} t=[{trace.First:R},{trace.Last:R}] events={trace.StartEvent}/{trace.EndEvent} forward={trace.Forward} uv=({trace.UvStart.param[0]:R},{trace.UvStart.param[1]:R})->({trace.UvEnd.param[0]:R},{trace.UvEnd.param[1]:R})");}
            return;
        }
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.CertifiedRoot)
        {
            var carrier=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(trace.Curve)];
            Console.WriteLine($"BOUNDARY CertifiedRoot event={trace.StartEvent} edge={trace.Edge} coedge={trace.InputCoedge} carrier={trace.Curve}/{trace.Generation}/{carrier.Class} supports={trace.Surface}/{trace.OtherSurface} cut={trace.ProductCurve} t=[{trace.First:R},{trace.Last:R}] error={trace.SpatialError:R} witness={trace.Witness}");return;
        }
        Console.WriteLine($"BOUNDARY {trace.Stage} source={trace.Source} face={trace.Face} product={trace.Product} surface={trace.Surface}/{surface.Class} edge={trace.Edge} carrier={trace.Curve}/{trace.Generation}/{KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(trace.Curve)].Class} product_curve={trace.ProductCurve} t=[{trace.First:R},{trace.Last:R}] events={trace.StartEvent}->{trace.EndEvent} forward={trace.Forward}");
        if(args.Contains("--diagnostics")&&trace.Stage==KernelRuntime.BoundaryTraceStage.Input)
        {
            Console.WriteLine($"BOUNDARY OCCURRENCE source_edge={trace.SourceEdge} source_fin={trace.SourceFin} support_generation={trace.SurfaceGeneration} sense_face_surface_curve={trace.FaceSense}/{trace.SurfaceSense}/{trace.CurveSense}");
            Console.WriteLine($"BOUNDARY XYZ start=({trace.StartPoint.X:R},{trace.StartPoint.Y:R},{trace.StartPoint.Z:R}) end=({trace.EndPoint.X:R},{trace.EndPoint.Y:R},{trace.EndPoint.Z:R})");
        }
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.Input&&surface.Class==SurfaceClass.BlendSurface)
        {
            var blend=KernelRuntime.BlendedEdgeDataPool[surface.DataIndex];var spine=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(blend.SpineTag)];
            Console.WriteLine($"BOUNDARY SUPPORT blend={trace.Surface} uv=[{surface.UMin:R},{surface.UMax:R}]x[{surface.VMin:R},{surface.VMax:R}] spine={blend.SpineTag}/{spine.Class} natural=[{spine.TMin:R},{spine.TMax:R}] supports={blend.Surface0Tag}/{blend.Surface1Tag} radii={blend.Range0:R}/{blend.Range1:R}");
        }
    };

var benchRepeat=1;var benchOption=args.SingleOrDefault(x=>x.StartsWith("--bench-repeat=",StringComparison.Ordinal));
if(benchOption!=null)benchRepeat=int.Parse(benchOption["--bench-repeat=".Length..],System.Globalization.CultureInfo.InvariantCulture);
Require(benchRepeat>=1&&benchRepeat<=64,"bench repeat must be in 1..64");
ModelingPerformance.Enabled=args.Contains("--bench-profile",StringComparer.Ordinal);
if(ModelingPerformance.Enabled)PrintKernelIdentity("start");
var researchPrecision=NumberOption(args,"--research-precision=",0);
Require(researchPrecision==0||double.IsFinite(researchPrecision)&&researchPrecision>0&&args.Contains("--native-only")&&args.Contains("--diagnostics")&&!args.Contains("--record-fixtures"),
    "research precision requires native-only diagnostics and cannot update fixtures");
var output=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs",researchPrecision==0?"short-edge-blend-oracle":"short-edge-native-research");
var outputName=args.FirstOrDefault(x=>x.StartsWith("--output-name=",StringComparison.Ordinal));
if(outputName!=null)
{
    var name=outputName["--output-name=".Length..];
    Require(name.Length>0&&name.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'),"output name must be one local directory name");
    output=Path.Combine(output,name);Require(!Directory.Exists(output),"existing execution artifacts are immutable");
}
var shardOption=args.FirstOrDefault(x=>x.StartsWith("--case-shard=",StringComparison.Ordinal));
var shardIndex=0;var shardCount=1;
if(shardOption!=null)
{
    var parts=shardOption["--case-shard=".Length..].Split('/');
    Require(parts.Length==2&&int.TryParse(parts[0],out shardIndex)&&int.TryParse(parts[1],out shardCount)
        &&shardCount>0&&shardCount<=48*(args.Contains("--both-updates")?2:1)&&shardIndex>=0&&shardIndex<shardCount
        &&args.Contains("--matrix")&&outputName!=null,"case shard requires index/count, matrix and a new output name");
}
var floor=NumberOption(args,"--seed-floor=",0);
var phase=NumberOption(args,"--support-phase=",0);
var splitRemote=args.Contains("--split-remote-edge");
var splitSelected=args.Contains("--split-selected-edge");
var splitEllipse=args.Contains("--split-selected-ellipse");
Require(!splitSelected||!splitEllipse,"choose one selected carrier to split");
var splitFraction=NumberOption(args,"--selected-split-fraction=",.5);
Require(double.IsFinite(splitFraction)&&splitFraction>0&&splitFraction<1&&(splitFraction==.5||splitSelected||splitEllipse),"selected split fraction requires a split and an interior value");
var rotateMarks=NumberOption(args,"--rotate-marks=",0);
Require(rotateMarks is 0 or 1 or 2&&(rotateMarks==0||splitSelected||splitEllipse),"mark rotation requires a split and a rotation of zero, one or two");
var transverseCut=args.Contains("--transverse-local-cut");
var localCut=args.Contains("--local-cut")||transverseCut;
var overlappingTools=args.Contains("--overlapping-tools");
var singleToolJunction=args.Contains("--overlapping-tools-single");
const double defaultTransverseOffset=3.8e-5;
var transverseOffset=NumberOption(args,"--transverse-offset=",defaultTransverseOffset);
Require(double.IsFinite(transverseOffset)&&transverseOffset>0&&(transverseOffset==defaultTransverseOffset||transverseCut),
    "transverse offset must be positive and requires the transverse cutter");
var materialOwnership=args.Contains("--material-ownership");
var requireNativeFinBoxes=args.Contains("--require-native-fin-uvbox");
var requireNativeFaceDomains=args.Contains("--require-native-face-domains");
var requireNativeCutWitness=args.Contains("--require-native-cut-witness");
var requireCutterChartWitness=args.Contains("--require-cutter-chart-witness");
var proofOnlyLifecycle=args.Contains("--proof-only-lifecycle");
var blendOnlyLifecycle=args.Contains("--blend-only-lifecycle");
var reloadBeforeLocalCut=args.Contains("--reload-before-local-cut");
var savedProbes=args.Contains("--saved-probes");
var selfSavedProbes=args.Contains("--self-saved-probes");
var replayProbes=savedProbes||selfSavedProbes;
Require(double.IsFinite(floor)&&floor<=0,"seed floor must be finite and no higher than the original floor");
Require(double.IsFinite(phase),"support phase must be finite");
Require(floor==0&&phase==0&&!splitRemote&&!splitSelected&&!splitEllipse||!args.Contains("--record-fixtures")&&!replayProbes,"modified seeds cannot replace fixed fixtures or probes");
M.PK_TRANSF_sf_s inputTransform;double inputScale;bool mappedInput;
unsafe{inputTransform=InputTransform(args,out inputScale,out mappedInput);}
Directory.CreateDirectory(output);
ParasolidMassProperties.BeginEvidence(output);
Require(!args.Contains("--record-fixtures")||!args.Contains("--matrix")&&!args.Contains("--both-updates"),"record fixtures uses the single default seed");
Require(!replayProbes||!args.Contains("--matrix")&&!args.Contains("--both-updates")&&!args.Contains("--native-only"),"saved probes replay the default seed and exact saved coordinates");
Require(!(overlappingTools||singleToolJunction)||KernelRuntime.ForbidLegacyRegularBlendAssembly&&!mappedInput&&!replayProbes&&!splitRemote&&!splitSelected&&!splitEllipse
    &&floor==0&&phase==0&&!args.Any(x=>x is "--native-only" or "--import-only" or "--matrix" or "--record-fixtures"),
    "overlapping tools require the guarded independent construction of the original seed");
Require(!selfSavedProbes||KernelRuntime.ForbidLegacyRegularBlendAssembly&&!savedProbes&&!args.Contains("--import-only")&&!args.Contains("--record-fixtures"),
    "self probe replay requires guarded independent construction and a separate run from imported probes");
Require(!args.Contains("--native-only")||!args.Any(x=>x is "--import-only" or "--probes" or "--geometry" or "--lifecycle"),"managed checks require a managed-import mode");
Require(!materialOwnership||!args.Contains("--native-only")&&!args.Contains("--import-only"),"material ownership checks the self-constructed product");
Require(!requireNativeFinBoxes||!args.Contains("--native-only")&&!args.Contains("--import-only"),"native fin UV boxes check the fresh self-constructed product");
Require(!requireNativeFaceDomains||KernelRuntime.ForbidLegacyRegularBlendAssembly&&!args.Contains("--native-only")&&!args.Contains("--import-only"),
    "native finite face domains require guarded self construction");
Require(!localCut||args.Contains("--lifecycle")&&!args.Contains("--record-fixtures")&&!replayProbes,"local cutter requires lifecycle mode");
Require(!requireNativeCutWitness||transverseCut&&KernelRuntime.ForbidLegacyRegularBlendAssembly&&!args.Contains("--import-only"),
    "native cut witness requires the guarded transverse self lifecycle");
Require(!requireCutterChartWitness||requireNativeCutWitness,"cutter chart witness requires the guarded native cut witness");
Require(!proofOnlyLifecycle||args.Contains("--lifecycle")&&KernelRuntime.ForbidLegacyRegularBlendAssembly&&!args.Contains("--import-only")
    &&!replayProbes&&!args.Contains("--record-fixtures"),"proof-only cuts require a guarded self lifecycle");
Require(!reloadBeforeLocalCut||requireNativeCutWitness,"early XT reload requires a guarded transverse cut with native witnesses");
Require(!blendOnlyLifecycle||args.Contains("--lifecycle")&&!localCut&&!requireNativeCutWitness&&!requireCutterChartWitness&&!proofOnlyLifecycle&&!reloadBeforeLocalCut,
    "Blend-only lifecycle requires lifecycle mode and no Boolean cut options");
Require(!mappedInput||!args.Contains("--record-fixtures")&&!replayProbes,"transformed inputs cannot replace fixed fixtures or historical probe inputs");
Require(!args.Any(x=>x.StartsWith("--angle=",StringComparison.Ordinal)||x.StartsWith("--radius=",StringComparison.Ordinal)||x.StartsWith("--tolerance=",StringComparison.Ordinal))
    ||!replayProbes&&!args.Contains("--record-fixtures"),"parameter overrides cannot replace fixed fixtures or historical probe inputs");
if(!ParasolidScriptHost.TryStartSession("Generated short-edge fillet",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    PK_SESSION_kernel_version_t version;double precision;
    Check(PK_SESSION_ask_kernel_version(&version),"version");Check(PK_SESSION_ask_precision(&precision),"precision");
    Console.WriteLine($"PK {version.major_revision}.{version.minor_revision}.{version.build_number}, precision={precision:R}");
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed session");
    var failures=0;var cases=0;
    try
    {
        var matrix=args.Contains("--matrix");
        var matrixIndex=0;
        foreach(var angle in matrix?new[]{.25,.5,1,2}:new[]{NumberOption(args,"--angle=",1)})
        foreach(var radius in matrix?new[]{.25,.5,.6}:new[]{NumberOption(args,"--radius=",.5)})
        foreach(var tolerance in matrix?new[]{1e-7,1e-5}:new[]{NumberOption(args,"--tolerance=",1e-7)})
        foreach(var reverse in matrix?new[]{false,true}:new[]{args.Contains("--reverse-selection")})
        foreach(var procedural in args.Contains("--both-updates")?new[]{false,true}:new[]{args.Contains("--web-procedural")})
        {
            if(matrixIndex++%shardCount!=shardIndex)continue;
            var name=$"a{angle:R}-r{radius:R}-t{tolerance:R}-reverse{reverse}-"+(procedural?"v261":"default");
            if(floor!=0)name+=$"-floor{floor:R}";
            if(splitRemote)name+="-split-remote";
            if(splitSelected)name+="-split-selected";
            if(splitEllipse)name+="-split-selected-ellipse";
            if(splitFraction!=.5)name+=$"-at{splitFraction:R}";
            if(rotateMarks!=0)name+=$"-marks{rotateMarks:R}";
            if(localCut)name+=transverseCut?"-transverse-local-cut":"-local-cut";
            if(transverseOffset!=defaultTransverseOffset)name+=$"-offset{transverseOffset:R}";
            if(reloadBeforeLocalCut)name+="-reload-before-cut";
            if(proofOnlyLifecycle)name+="-proof-only-cuts";
            if(phase!=0)name+=$"-support-phase{phase:R}";
            if(mappedInput)name+=$"-input-{(args.Contains("--input-mirror")?"mirror":"rigid")}-s{inputScale:R}";
                if(researchPrecision!=0)name+=$"-pk-precision{researchPrecision:R}";
            // The two short contacts start at y-z=-7 for radius .5 in
            // seed units. The planar contact shifts that limit by Δradius.
            var transversePlaneDifference=-7+(radius-.5)-transverseOffset;
            cases++;
            Console.WriteLine("CASE "+name);
            for(var benchRep=0;benchRep<benchRepeat;benchRep++)
            {
            double prepareMs=0,pkCallMs=0,midMs=0,gmCallMs=0;var benchStart=Stopwatch.GetTimestamp();
            try
            {
                var reference=Seed(angle,floor,splitRemote,phase);CheckBody(reference,"generated input");Counts(reference,8,splitRemote?18:17,splitRemote?12:11);
                var bytes=Transmit(false,reference);File.WriteAllBytes(Path.Combine(output,name+"-input.x_t"),bytes);
                var own=Receive(true,bytes);
                if(mappedInput){ApplyInput(true,own,inputTransform);ApplyInput(false,reference,inputTransform);}
                Require(KernelRuntime.TryResolveBodySlot(own,out var body)&&!KernelRuntime.Xt.IsOpaque(body),"editable input");
                var fresh=Transmit(true,own);File.WriteAllBytes(Path.Combine(output,name+"-input-fresh.x_t"),fresh);
                WebProgressCapture.ContextSource=new JsonObject{["kind"]="PK-seed-GM-receive",["pkSeedXtSha256"]=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)),
                    ["gmFreshInputXtSha256"]=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(fresh)),["mappedInput"]=mappedInput,["inputScale"]=inputScale,
                    ["angle"]=angle,["radius"]=radius*inputScale,["tolerance"]=tolerance*inputScale};
                var freshBody=Receive(false,fresh);CheckBody(freshBody,"fresh input");ParasolidBodyCompare.AssertEquivalent(reference,freshBody,"fresh input");
                Check(PK_ENTITY_delete(1,&freshBody),"delete input roundtrip");
                if(args.Contains("--native-only")&&args.Contains("--diagnostics"))ProductEdges(reference,"native-before-fix");
                if(researchPrecision!=0)Check(PK_SESSION_set_precision(researchPrecision),"set research precision");
                int nativeError;
                prepareMs=Stopwatch.GetElapsedTime(benchStart).TotalMilliseconds;
                var pkCallStart=Stopwatch.GetTimestamp();
                try{nativeError=Fillet(false,reference,angle,radius*inputScale,tolerance*inputScale,reverse,procedural,mappedInput?&inputTransform:null,inputScale,splitSelected,splitEllipse,splitFraction,(int)rotateMarks);}
                finally{if(researchPrecision!=0)Check(PK_SESSION_set_precision(precision),"restore default precision");}
                pkCallMs=Stopwatch.GetElapsedTime(pkCallStart).TotalMilliseconds;
                Check(nativeError,"native fix");CheckBody(reference,"native fillet");
                if(args.Contains("--native-only")&&args.Contains("--diagnostics"))ProductEdges(reference,"native-after-fix");
                var result=Transmit(false,reference);File.WriteAllBytes(Path.Combine(output,name+"-native.x_t"),result);
                Describe(reference);DescribeXt(result);
                if(args.Contains("--record-fixtures"))
                {
                    var fixtures=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","tests","KernelTests","Fixtures");
                    File.WriteAllText(Path.Combine(fixtures,"generated-short-edge-input.x_t"),FixtureText(bytes));
                    File.WriteAllText(Path.Combine(fixtures,"generated-short-edge-native.x_t"),FixtureText(result));
                }
                if(!args.Contains("--native-only"))try
                {
                    var imported=Receive(true,result);
                    Require(KernelRuntime.TryResolveBodySlot(imported,out var importedSlot)&&!KernelRuntime.Xt.IsOpaque(importedSlot),"editable native fillet");
                    if(args.Contains("--diagnostics"))
                    {
                        var face=KernelRuntime.Bodies[importedSlot].FirstFaceBody;
                        for(var i=0;i<KernelRuntime.Bodies[importedSlot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
                        {
                            var record=KernelRuntime.Faces[face];var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(record.SurfTag)];
                            Console.WriteLine($"EDITABLE face={record.Header.Tag} surface={record.SurfTag} class={surface.Class} domain=({surface.UMin:R},{surface.UMax:R},{surface.VMin:R},{surface.VMax:R})");
                        }
                        var edge=KernelRuntime.Bodies[importedSlot].FirstEdgeBody;
                        for(var i=0;i<KernelRuntime.Bodies[importedSlot].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
                        {
                            var record=KernelRuntime.Edges[edge];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.CurveTag)];
                            Console.WriteLine($"EDITABLE edge={record.Header.Tag} curve={record.CurveTag} class={curve.Class}");
                            if(curve.Class==CurveClass.ICurve)
                            {
                                var definition=KernelRuntime.ICurveDataPool[curve.DataIndex];
                                var owns=KernelRuntime.EnsureCommandScratch();
                                try
                                {
                                    var status=KernelRuntime.CertifiedIntersectionCurveExtent(record.CurveTag,record.ParameterStart,record.ParameterEnd,out var extent);
                                    Console.WriteLine($"CURVE TUBE curve={record.CurveTag} charts={definition.ChartCount} limits={definition.StartLimit.Type}/{definition.EndLimit.Type} status={status} extent=({extent.XLo:R},{extent.XHi:R},{extent.YLo:R},{extent.YHi:R},{extent.ZLo:R},{extent.ZHi:R})");
                                }
                                finally{if(owns)KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session);}
                            }
                        }
                    }
                    var reexport=Transmit(true,imported);File.WriteAllBytes(Path.Combine(output,name+"-native-fresh.x_t"),reexport);
                    var reloaded=Receive(false,reexport);CheckBody(reloaded,"native via managed");
                    WebProgressCapture.BindQuality("native via managed",imported,reference,reloaded,reexport,result);
                    ParasolidBodyCompare.AssertEquivalent(reference,reloaded,"native via managed");
                    Mass(reference,reloaded,"native via managed");
                    if(args.Contains("--geometry"))Geometry(imported,reference);
                    if(args.Contains("--probes"))Probes(reference,imported,reloaded,result,output,name);
                    if(savedProbes)SavedProbes(reference,imported,reloaded,result,output,name+"-imported");
                    if(args.Contains("--lifecycle"))failures+=Lifecycle(ref imported,reference,output,name+"-imported-product",mappedInput?&inputTransform:null,inputScale,localCut,transverseCut,transversePlaneDifference,requireNativeFaceDomains:requireNativeFaceDomains,blendOnly:blendOnlyLifecycle);
                    Check(PK_ENTITY_delete(1,&reloaded),"delete native roundtrip");if(imported!=0)Check(KernelRuntime.EntityDelete(1,&imported),"delete managed import");
                }
                catch(Exception ex){failures++;Console.WriteLine("FAIL native editable roundtrip: "+ex.Message);}
                if(!args.Contains("--native-only")&&!args.Contains("--import-only"))
                {
                    midMs=Stopwatch.GetElapsedTime(benchStart).TotalMilliseconds-prepareMs-pkCallMs;
                    var gmCallStart=Stopwatch.GetTimestamp();
                    Check(Fillet(true,own,angle,radius*inputScale,tolerance*inputScale,reverse,procedural,mappedInput?&inputTransform:null,inputScale,splitSelected,splitEllipse,splitFraction,(int)rotateMarks),"managed fix");
                    gmCallMs=Stopwatch.GetElapsedTime(gmCallStart).TotalMilliseconds;
                    var ownResult=Transmit(true,own);File.WriteAllBytes(Path.Combine(output,name+"-self.x_t"),ownResult);
                    var received=Receive(false,ownResult);CheckBody(received,"managed fillet");
                    WebProgressCapture.BindQuality("independent fillet",own,reference,received,ownResult,result);
                    if(args.Contains("--diagnostics")){ProductEdges(reference,"reference");ProductEdges(received,"self-constructed-product");}
                    ParasolidBodyCompare.AssertEquivalent(reference,received,"independent fillet");
                    CompareFiniteIntersections(reference,received,"independent fillet",inputScale);
                    if(requireNativeFinBoxes)CompareNativeBlendFinBoxes(reference,received,"independent fillet",required:true);
                    if(requireNativeFaceDomains)CompareNativeFaceDomains(reference,received,"independent fillet",inputScale);
                    if(selfSavedProbes)
                    {
                        Console.WriteLine("SAVED PROBE PRODUCT self-constructed-product");
                        try{SavedProbes(reference,own,received,result,output,name+"-self");}
                        catch(Exception ex){failures++;Console.WriteLine("FAIL self-constructed-product fixed probes: "+ex.Message);}
                    }
                    if(materialOwnership)CompareShortEdgeMaterial(reference,own,received,"self-constructed-product",inputScale);
                    if(args.Contains("--geometry")){CheckSelfNativeTrims(own,"self-constructed-product",inputScale);SelfGeometry(own,reference);}
                    try{WebProgressCapture.BindQuality("self-constructed-product",own,reference,received,ownResult,result);Mass(reference,received,"self-constructed-product");}
                    catch(Exception ex){failures++;Console.WriteLine("FAIL self-constructed-product mass: "+ex.Message);}
                    if(overlappingTools)failures+=OverlappingToolJunctions(own,reference,output,name+"-overlapping-tools",false);
                    if(singleToolJunction)failures+=OverlappingToolJunctions(own,reference,output,name+"-overlapping-tools-single",true);
                    if(args.Contains("--lifecycle"))failures+=Lifecycle(ref own,reference,output,name+"-self-constructed-product",mappedInput?&inputTransform:null,inputScale,localCut,transverseCut,transversePlaneDifference,materialOwnership,requireNativeCutWitness,reloadBeforeLocalCut,args.Contains("--geometry"),proofOnlyLifecycle,requireCutterChartWitness,requireNativeFinBoxes,requireNativeFaceDomains,blendOnlyLifecycle);
                    Check(PK_ENTITY_delete(1,&received),"delete managed roundtrip");
                }
                if(own!=0)Check(KernelRuntime.EntityDelete(1,&own),"delete managed seed");Check(PK_ENTITY_delete(1,&reference),"delete reference");
                Console.WriteLine($"BENCH case={name} rep={benchRep} prepare-ms={prepareMs:F3} pk-call-ms={pkCallMs:F3} mid-ms={midMs:F3} gm-call-ms={gmCallMs:F3} tail-ms={Stopwatch.GetElapsedTime(benchStart).TotalMilliseconds-prepareMs-pkCallMs-midMs-gmCallMs:F3} case-ms={Stopwatch.GetElapsedTime(benchStart).TotalMilliseconds:F3}");
            }
            catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+": "+ex.Message);}
            }
        }
    }
    finally{Check(KernelRuntime.SessionStop(),"managed stop");if(ModelingPerformance.Enabled)PrintKernelIdentity("end");}
    Require(failures==0,$"{failures} short-edge acceptance checks failed");
    var unresolved=ParasolidMassProperties.UnresolvedCount;
    var probesPending=ParasolidProbeAgreement.UnresolvedCount;
    Console.WriteLine($"GEOMETRY_TOPOLOGY_PASS cases={cases} mode={(args.Contains("--native-only")?"native-only":args.Contains("--import-only")?"import-only":"independent-construction")} mass_unresolved={unresolved} probes_unresolved={probesPending}");
    if(unresolved!=0||probesPending!=0)Environment.ExitCode=2;
}

static void PrintKernelIdentity(string phase)
{
    var assembly=typeof(KernelRuntime).Assembly;using var file=File.OpenRead(assembly.Location);
    Console.WriteLine($"KERNEL_IDENTITY phase={phase} mvid={assembly.ManifestModule.ModuleVersionId} bytes={file.Length} sha256={Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(file))}");
}

static unsafe void ScalarBoundaryEvidence(int surface,int other)
{
    var first=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)];var second=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(other)];
    if(first.Class!=SurfaceClass.BlendSurface||second.Class!=SurfaceClass.Cylinder)return;
    var data=KernelRuntime.BlendedEdgeDataPool[first.DataIndex];var program=KernelRuntime.GetNestedBlendProgram(data);var cylinder=KernelRuntime.CylinderDataPool[second.DataIndex];
    var support=new AnalyticSurface(SurfaceClass.Cylinder,new(){X=cylinder.LocationX,Y=cylinder.LocationY,Z=cylinder.LocationZ},new(){X=cylinder.AxisX,Y=cylinder.AxisY,Z=cylinder.AxisZ},new(){X=cylinder.RefDirX,Y=cylinder.RefDirY,Z=cylinder.RefDirZ},cylinder.Radius);
    Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];var u=first.UMin+(first.UMax-first.UMin)*.5;
    foreach(var v in new[]{0,1e-8,1e-6,3e-6,1-1e-6,1})
    {
        var patch=new SurfaceIntersectionPatch(program,data.ProgramRoot,new SurfaceParameterBox(u,u,v,v));var status=patch.Bound(new(u,u,v,v),bounds);
        if(status!=ProjectGmKernel.Native.Computation.AlgorithmStatus.Success){Console.WriteLine($"SCALAR boundary u={u:R} v={v:R} status={status}");continue;}
        var f=AnalyticLevelSet.Value(bounds,support);var d=AnalyticLevelSet.Derivative(bounds,support,6);
        Console.WriteLine($"SCALAR boundary u={u:R} v={v:R} f=[{f.Lower:R},{f.Upper:R}] dv=[{d.Lower:R},{d.Upper:R}]");
    }
}

static double NumberOption(string[] args,string prefix,double fallback)
{
    var option=args.SingleOrDefault(x=>x.StartsWith(prefix,StringComparison.Ordinal));
    return option==null?fallback:double.Parse(option[prefix.Length..],System.Globalization.CultureInfo.InvariantCulture);
}

static unsafe M.PK_TRANSF_sf_s InputTransform(string[] args,out double scale,out bool mapped)
{
    scale=NumberOption(args,"--input-scale=",1);Require(double.IsFinite(scale)&&scale>0,"positive finite input scale");
    mapped=args.Contains("--input-rigid")||args.Contains("--input-mirror")||scale!=1;
    var result=new M.PK_TRANSF_sf_s();result.matrix[0]=result.matrix[5]=result.matrix[10]=result.matrix[15]=1;if(!mapped)return result;
    var a=.37;var b=-.29;var c=.23;var ca=Math.Cos(a);var sa=Math.Sin(a);var cb=Math.Cos(b);var sb=Math.Sin(b);var cc=Math.Cos(c);var sc=Math.Sin(c);
    result.matrix[0]=ca*cb;result.matrix[1]=ca*sb*sc-sa*cc;result.matrix[2]=ca*sb*cc+sa*sc;
    result.matrix[4]=sa*cb;result.matrix[5]=sa*sb*sc+ca*cc;result.matrix[6]=sa*sb*cc-ca*sc;
    result.matrix[8]=-sb;result.matrix[9]=cb*sc;result.matrix[10]=cb*cc;
    for(var row=0;row<3;row++)for(var column=0;column<3;column++)result.matrix[4*row+column]*=scale*(args.Contains("--input-mirror")&&column==0?-1:1);
    result.matrix[3]=-.7*scale;result.matrix[7]=1.1*scale;result.matrix[11]=2.3*scale;return result;
}
static unsafe void ApplyInput(bool own,int body,M.PK_TRANSF_sf_s matrix)
{
    int transform;Check(own?KernelRuntime.TransfCreate(&matrix,&transform):PK_TRANSF_create((PK_TRANSF_sf_t*)&matrix,&transform),"input similarity");
    try{Move(own,body,transform);}finally{Check(own?KernelRuntime.EntityDelete(1,&transform):PK_ENTITY_delete(1,&transform),"delete input transform");}
}
static unsafe PK_VECTOR_t MapPoint(M.PK_TRANSF_sf_s* matrix,PK_VECTOR_t point,bool inverse)
{
    var result=new PK_VECTOR_t();
    if(!inverse){for(var r=0;r<3;r++){result.coord[r]=matrix->matrix[4*r+3];for(var c=0;c<3;c++)result.coord[r]+=matrix->matrix[4*r+c]*point.coord[c];}return result;}
    var square=matrix->matrix[0]*matrix->matrix[0]+matrix->matrix[4]*matrix->matrix[4]+matrix->matrix[8]*matrix->matrix[8];
    for(var r=0;r<3;r++)for(var c=0;c<3;c++)result.coord[r]+=matrix->matrix[4*c+r]*(point.coord[c]-matrix->matrix[4*c+3])/square;
    return result;
}

static unsafe int Lifecycle(ref int source,int reference,string output,string name,M.PK_TRANSF_sf_s* input,double inputScale,bool localCut,bool transverseCut,double transversePlaneDifference,bool materialOwnership=false,bool requireNativeCutWitness=false,bool reloadBeforeLocalCut=false,bool checkNativeTrims=false,bool proofOnly=false,bool requireCutterChartWitness=false,bool requireNativeFinBoxes=false,bool requireNativeFaceDomains=false,bool blendOnly=false)
{
    WebProgressCapture.Context=name;
    var upstreamSource=WebProgressCapture.ContextSource;
    WebProgressCapture.ContextSource=new JsonObject{["kind"]="lifecycle-products",["stage"]=name,["gmUpstreamLiveBody"]=source,["referenceUpstreamNativeBody"]=reference,["upstreamSource"]=upstreamSource?.DeepClone()};
    int own,native;Check(KernelRuntime.EntityCopy(source,&own),"copy fillet product");Check(PK_ENTITY_copy(reference,&native),"copy PK fillet");
    var matrix=new M.PK_TRANSF_sf_s();var a=.37;var s=1.7;
    matrix.matrix[0]=-s*Math.Cos(a);matrix.matrix[1]=-s*Math.Sin(a);matrix.matrix[4]=-s*Math.Sin(a);matrix.matrix[5]=s*Math.Cos(a);matrix.matrix[10]=s;matrix.matrix[15]=1;
    matrix.matrix[3]=3;matrix.matrix[7]=5;matrix.matrix[11]=7;
    int selfTransform,pkTransform;Check(KernelRuntime.TransfCreate(&matrix,&selfTransform),"self transform");Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&matrix,&pkTransform),"PK transform");
    var failures=0;
    try
    {
        var original=source;Check(KernelRuntime.EntityDelete(1,&original),"delete fillet source before copy modelling");source=0;
        Console.WriteLine($"LIFECYCLE {name} source_deleted=True copy={own}");
        Move(true,own,selfTransform);Move(false,native,pkTransform);
        failures+=CompareFresh(own,native,output,name+"-copy-reflected",s*inputScale,materialOwnership,checkNativeTrims,requireNativeFinBoxes,requireNativeFaceDomains);
        if(blendOnly)
        {
            ReloadEditable(ref own,name+" independent Blend reload");
            failures+=CompareFresh(own,native,output,name+"-blend-reloaded",s*inputScale,materialOwnership,checkNativeTrims,requireNativeFinBoxes,requireNativeFaceDomains);
            Console.WriteLine($"BLEND_LIFECYCLE {name} copy/source deletion/mirror-scale/editable reload complete; Boolean not selected");
            return failures;
        }
        if(reloadBeforeLocalCut)ReloadEditable(ref own,name+" before local cut");
        // The local variant crosses the native short ICurve interiors. Its
        // lower cap leaves the bottom connected; it is a notch, not a slab
        // cutting the whole solid into disconnected components.
        var toolAxis=new PK_AXIS2_sf_t(localCut?new PK_VECTOR_t(-1.5999998,0,4.1):new PK_VECTOR_t(3,0,-1),
            new PK_VECTOR1_t(0,0,1),new PK_VECTOR1_t(1,0,0));
        var width=localCut?.2:2;var depth=10.0;var height=10.0;int ownTool,pkTool;
        if(transverseCut)
        {
            var q=1/Math.Sqrt(2);var center=transversePlaneDifference*q-.075;
            toolAxis=new(new PK_VECTOR_t(-1.5,(1+center)*q,(1-center)*q),
                new PK_VECTOR1_t(0,q,q),new PK_VECTOR1_t(0,q,-q));
            width=.15;depth=.4;height=2.5;
        }
        Check(KernelRuntime.BodyCreateSolidBlock(width,depth,height,(M.PK_AXIS2_sf_s*)&toolAxis,&ownTool),"self cutter");
        Check(PK_BODY_create_solid_block(width,depth,height,&toolAxis,&pkTool),"PK cutter");
        if(input!=null){ApplyInput(true,ownTool,*input);ApplyInput(false,pkTool,*input);}
        Move(true,ownTool,selfTransform);Move(false,pkTool,pkTransform);
        if(localCut)
        {
            native=Cut(false,native,pkTool);CheckBody(native,name+" native local cut");
            File.WriteAllBytes(Path.Combine(output,name+"-independent-local-reference.x_t"),Transmit(false,native));
            if(KernelRuntime.TraceFiniteBoundary!=null)ProductEdges(native,"native-local-cut");
            own=requireNativeCutWitness?CutWithNativeWitness(own,ownTool,s*inputScale,requireCutterChartWitness):proofOnly?CutWithoutSpatialReuse(own,ownTool):Cut(true,own,ownTool);
        }
        else{own=proofOnly?CutWithoutSpatialReuse(own,ownTool):Cut(true,own,ownTool);native=Cut(false,native,pkTool);}
        failures+=CompareFresh(own,native,output,name+"-copy-reflected-cut",s*inputScale,materialOwnership,checkNativeTrims,requireNativeFinBoxes,requireNativeFaceDomains);
        ReloadEditable(ref own,name+" before second cut");
        var secondAxis=new PK_AXIS2_sf_t(new PK_VECTOR_t(0,2.5,-1),new PK_VECTOR1_t(0,0,1),new PK_VECTOR1_t(1,0,0));
        Check(KernelRuntime.BodyCreateSolidBlock(10,1,10,(M.PK_AXIS2_sf_s*)&secondAxis,&ownTool),"self reload cutter");Check(PK_BODY_create_solid_block(10,1,10,&secondAxis,&pkTool),"PK reload cutter");
        if(input!=null){ApplyInput(true,ownTool,*input);ApplyInput(false,pkTool,*input);}
        Move(true,ownTool,selfTransform);Move(false,pkTool,pkTransform);
        own=requireNativeCutWitness||proofOnly?CutWithoutSpatialReuse(own,ownTool):Cut(true,own,ownTool);
        native=Cut(false,native,pkTool);failures+=CompareFresh(own,native,output,name+"-reloaded-second-cut",s*inputScale,materialOwnership,checkNativeTrims,requireNativeFinBoxes,requireNativeFaceDomains);
    }
    finally
    {
        WebProgressCapture.ContextSource=upstreamSource;
        if(own!=0)Check(KernelRuntime.EntityDelete(1,&own),"delete own lifecycle");Check(PK_ENTITY_delete(1,&native),"delete PK lifecycle");
        Check(KernelRuntime.EntityDelete(1,&selfTransform),"delete self transform");Check(PK_ENTITY_delete(1,&pkTransform),"delete PK transform");
    }
    return failures;
}
static unsafe void ReloadEditable(ref int own,string label)
{
    var bytes=Transmit(true,own);var original=own;
    Check(KernelRuntime.EntityDelete(1,&original),"delete product before managed XT reload");
    own=0;own=Receive(true,bytes);
    Require(KernelRuntime.TryResolveBodySlot(own,out var slot)&&!KernelRuntime.Xt.IsOpaque(slot),label+" reload must be editable");
    Console.WriteLine($"LIFECYCLE {label} managed_reload=True body={own}");
}
static int CutWithNativeWitness(int body,int tool,double unitScale,bool requireCutterChartWitness)
{
    // Identify the original short edges on the actual editable input, so
    // certificates on long cut carriers cannot satisfy this assertion.
    var input=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];
    var shortEdges=new List<(int Curve,int Generation,double First,double Last)>();
    var edgeSlot=input.FirstEdgeBody;
    for(var i=0;i<input.EdgeCountBody;i++,edgeSlot=KernelRuntime.Edges[edgeSlot].NextInBody)
    {
        var edge=KernelRuntime.Edges[edgeSlot];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(edge.CurveTag)];
        if(curve.Class!=CurveClass.ICurve||edge.StartVertex<0||edge.EndVertex<0||edge.HasParameterRange==0)continue;
        var a=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[edge.StartVertex].PointTag)].Position;
        var b=KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[edge.EndVertex].PointTag)].Position;
        var dx=a.X-b.X;var dy=a.Y-b.Y;var dz=a.Z-b.Z;
        var chord=Math.Sqrt(dx*dx+dy*dy+dz*dz)/unitScale;
        if(chord>0&&chord<1e-3)shortEdges.Add((edge.CurveTag,curve.Header.Generation,edge.ParameterStart,edge.ParameterEnd));
    }
    Require(shortEdges.Count==2,$"the transverse seed must have two positive short ICurves; found {shortEdges.Count}");
    var previous=KernelRuntime.TraceFiniteBoundary;
    var witnesses=new List<KernelRuntime.BoundaryTrace>();
    var roots=new List<KernelRuntime.BoundaryTrace>();
    var cutterRoots=new List<KernelRuntime.BoundaryTrace>();
    var conicRoots=new List<KernelRuntime.BoundaryTrace>();
    var cylinderRoots=new List<KernelRuntime.BoundaryTrace>();
    var arranged=new List<KernelRuntime.BoundaryTrace>();
    var published=new HashSet<(int Carrier,int Event)>();
    var productUses=new List<(KernelRuntime.BoundaryTrace Trace,EdgeSlot Edge,FinSlot Fin,char Sense)>();
    var spatialReuses=0;
    KernelRuntime.TraceFiniteBoundary=trace=>
    {
        previous?.Invoke(trace);
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.NativeCutLocator)witnesses.Add(trace);
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.CertifiedRoot)
        {
            roots.Add(trace);
            if(requireCutterChartWitness)
            {
                var source=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(trace.Curve)].Class;
                var kind=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(trace.ProductCurve)].Class;
                var contact=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(trace.Surface)].Class;
                var support=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(trace.OtherSurface)].Class;
                if(source==CurveClass.Line&&contact==SurfaceClass.Plane)
                {
                    if(kind==CurveClass.ICurve&&support==SurfaceClass.BlendSurface)cutterRoots.Add(trace);
                    if(kind is CurveClass.Circle or CurveClass.Ellipse&&support==SurfaceClass.Cylinder)conicRoots.Add(trace);
                }
                if(source==CurveClass.ICurve&&contact==SurfaceClass.Cylinder&&kind is CurveClass.Circle or CurveClass.Ellipse)cylinderRoots.Add(trace);
            }
        }
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.SpatialEndpointReuse)spatialReuses++;
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.NativeCutEndpoints)
            arranged.Add(trace);
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.Product)
        {
            published.Add((trace.Curve,trace.StartEvent));published.Add((trace.Curve,trace.EndEvent));
            if(witnesses.Any(w=>w.ProductCurve==trace.Curve&&(w.StartEvent==trace.StartEvent||w.StartEvent==trace.EndEvent)))
            {
                var face=KernelRuntime.Faces[trace.Product];var loop=face.FirstLoop;
                for(var l=0;l<face.LoopCount;l++,loop=KernelRuntime.Loops[loop].NextInFace)
                {
                    var ring=KernelRuntime.Loops[loop];var fin=ring.FirstFin;
                    for(var f=0;f<ring.FinCount;f++,fin=KernelRuntime.Fins[fin].NextInLoop)
                    {
                        var use=KernelRuntime.Fins[fin];if(use.Edge<0)continue;
                        var edge=KernelRuntime.Edges[use.Edge];
                        if(edge.CurveTag==trace.ProductCurve&&edge.HasParameterRange!=0&&edge.ParameterStart==trace.First&&edge.ParameterEnd==trace.Last)
                            productUses.Add((trace,use.Edge,fin,use.Sense));
                    }
                }
            }
        }
    };
    int result;
    try{result=CutWithoutSpatialReuse(body,tool);}
    finally{KernelRuntime.TraceFiniteBoundary=previous;}
    var consumed=witnesses.Where(PublishedOnBothFaces).ToArray();
    var shortEvents=new HashSet<int>();
    var certifiedCoedges=0;
    foreach(var edge in shortEdges)
    {
        var uses=roots.Where(r=>r.Curve==edge.Curve&&r.Generation==edge.Generation&&r.First>edge.First&&r.Last<edge.Last
            &&published.Contains((r.ProductCurve,r.StartEvent))).ToArray();
        var coedges=uses.Select(r=>r.InputCoedge).Distinct().Count();
        Require(coedges==2,$"short ICurve {edge.Curve} must reach the published cut on both incident coedges; found {coedges}");
        var proved=uses.Where(Transferred).ToArray();
        Require(proved.Length==uses.Length,$"short ICurve {edge.Curve} must transfer every published coedge station; proved {proved.Length}/{uses.Length}");
        certifiedCoedges+=coedges;
        var evidence=proved.Select(r=>r.StartEvent).Distinct().ToArray();
        Require(evidence.Length==1,$"short ICurve {edge.Curve} must have one interior root reaching a published cut edge; found {evidence.Length}");
        shortEvents.Add(evidence[0]);
    }
    Require(shortEvents.Count==2,"the two actual short ICurves must contribute distinct published cut events");
    Require(spatialReuses==0,$"the guarded self cut must use certified endpoint joins; found {spatialReuses} position-only reuses");
    Console.WriteLine($"NATIVE CUT WITNESSES short_occurrences={shortEdges.Count} published_events={shortEvents.Count} spatial_reuses={spatialReuses} certified_coedges={certifiedCoedges}");
    if(requireCutterChartWitness)
    {
        CheckCutterCharts(cutterRoots,"CHART");
        CheckCutterCharts(conicRoots,"CONIC");
        var cylinderTrimRoots=cylinderRoots.Where(r=>published.Contains((r.ProductCurve,r.StartEvent))).ToArray();
        foreach(var root in cylinderTrimRoots)
        {
            var uses=roots.Where(r=>r.Curve==root.Curve&&r.Generation==root.Generation&&r.Edge==root.Edge&&r.Witness==root.Witness
                &&r.StartEvent==root.StartEvent&&published.Contains((r.ProductCurve,r.StartEvent))).ToArray();
            Require(uses.Select(r=>r.InputCoedge).Distinct().Count()==2&&uses.All(Transferred),
                $"inherited cylinder ICurve event {root.StartEvent} must transfer its certified root on both coedges");
        }
        if(cylinderTrimRoots.Length>0)Console.WriteLine($"CYLINDER TRIM WITNESSES events={cylinderTrimRoots.Select(r=>r.StartEvent).Distinct().Count()}");
    }
    return result;

    bool Transferred(KernelRuntime.BoundaryTrace root)=>consumed.Any(w=>w.Curve==root.Curve&&w.Generation==root.Generation
        &&w.ProductCurve==root.ProductCurve&&w.InputCoedge==root.InputCoedge&&w.StartEvent==root.StartEvent&&w.Witness==root.Witness);

    bool PublishedOnBothFaces(KernelRuntime.BoundaryTrace witness)
    {
        if(!double.IsFinite(witness.First)||!double.IsFinite(witness.Last)||witness.Last<witness.First||!(witness.SpatialError>0))return false;
        var pairs=0;
        foreach(var left in productUses)
        {
            if(left.Trace.Surface!=witness.Surface||!Arrived(left.Trace,out var station))continue;
            foreach(var right in productUses)
                if(right.Trace.Surface==witness.OtherSurface&&right.Trace.Product!=left.Trace.Product&&right.Edge==left.Edge
                    &&right.Fin!=left.Fin&&right.Sense!=left.Sense&&Arrived(right.Trace,out var other)&&other==station)
                    pairs++;
        }
        return pairs==1;

        bool Arrived(KernelRuntime.BoundaryTrace product,out double station)
        {
            station=default;
            if(product.Curve!=witness.ProductCurve||!Endpoint(product,out var end)||end<witness.First||end>witness.Last)return false;
            station=end;
            return arranged.Any(a=>a.Curve==product.Curve&&a.Generation==product.Generation&&a.Source==product.Source
                &&a.Surface==product.Surface&&a.SurfaceGeneration==product.SurfaceGeneration&&Endpoint(a,out var at)&&at==end);
        }

        bool Endpoint(KernelRuntime.BoundaryTrace trace,out double t)
        {
            // Arrangement traces name lower/upper directly. Product traces
            // follow the coedge, whose native direction also uses carrier sense.
            var increasing=trace.Stage==KernelRuntime.BoundaryTraceStage.NativeCutEndpoints
                ||(trace.Forward!=0)==(trace.CurveSense!=PK_TOPOL_sense_negative_c);
            if(trace.StartEvent==witness.StartEvent){t=increasing?trace.First:trace.Last;return true;}
            if(trace.EndEvent==witness.StartEvent){t=increasing?trace.Last:trace.First;return true;}
            t=default;return false;
        }
    }

    void CheckCutterCharts(List<KernelRuntime.BoundaryTrace> candidates,string kind)
    {
        var uses=candidates.Where(r=>published.Contains((r.ProductCurve,r.StartEvent))).ToArray();
        var events=uses.GroupBy(r=>r.StartEvent).ToArray();
        var cutterEdges=events.GroupBy(g=>g.First().Edge).ToArray();
        Require(cutterEdges.Length>0&&cutterEdges.All(g=>g.Count()==2)
            &&events.All(g=>g.Select(r=>r.Edge).Distinct().Count()==1&&g.Select(r=>r.InputCoedge).Distinct().Count()==2),
            $"each intersected finite cutter edge must supply two separate {kind} events on both of its coedges");
        var certified=uses.Count(Transferred);
        Require(certified==uses.Length,$"cutter edge stations must transfer to both published {kind} cuts; proved {certified}/{uses.Length}");
        Console.WriteLine($"CUTTER {kind} WITNESSES events={events.Length} certified_coedges={certified} cutter_edges={cutterEdges.Length}");
    }
}
static int CutWithoutSpatialReuse(int body,int tool)
{
    var previous=KernelRuntime.ForbidSpatialBoundaryReuse;
    KernelRuntime.ForbidSpatialBoundaryReuse=true;
    try{return Cut(true,body,tool);}
    finally{KernelRuntime.ForbidSpatialBoundaryReuse=previous;}
}
static unsafe void Move(bool own,int body,int transform)
{
    int n;int* replaced;PK_LOGICAL_t* exact;
    Check(own?KernelRuntime.BodyTransform(body,transform,0,&n,&replaced,(byte**)&exact):PK_BODY_transform(body,transform,0,&n,&replaced,&exact),"body transform");Free(own,replaced);Free(own,exact);
}
static unsafe int Cut(bool own,int body,int tool)
{
    var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};int n;int* bodies;
    WebProgressCapture.Before(own,body,[],"PK_BODY_boolean",[tool]);
    WebProgressCapture.BooleanParameters(own,options);
    var started=Stopwatch.GetTimestamp();var error=own?KernelRuntime.BodyBoolean(body,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&bodies):PK_BODY_boolean(body,1,&tool,&options,&n,&bodies);
    var webMs=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    WebProgressCapture.BooleanAfter(own,error==0?new ReadOnlySpan<int>(bodies,n).ToArray():[],webMs,error,0,null);
    Console.WriteLine($"TIME {(own?"self":"PK")} Boolean error={error} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3} stage={KernelRuntime.LastRegularBooleanStage} face={KernelRuntime.LastRegularBooleanFace} other={KernelRuntime.LastRegularBooleanOtherFace} sheet={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} t={KernelRuntime.LastSheetParameter:R} status={KernelRuntime.LastSheetAlgorithmStatus} uv=({KernelRuntime.LastSheetRayU:R},{KernelRuntime.LastSheetRayV:R}) proof_only={KernelRuntime.ForbidSpatialBoundaryReuse}");
    if(own&&ModelingPerformance.Enabled)PrintProfile("Boolean",webMs);
    if(own&&error!=0&&KernelRuntime.TraceFiniteBoundary!=null)
    {
        var target=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];var cutter=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(tool).Slot];
        foreach(var index in new[]{KernelRuntime.LastRegularBooleanFace,KernelRuntime.LastRegularBooleanOtherFace})
        {
            var owner=index<target.FaceCountBody?target:cutter;var local=index<target.FaceCountBody?index:index-target.FaceCountBody;
            if(local<0||local>=owner.FaceCountBody)continue;
            var face=owner.FirstFaceBody;for(var f=0;f<local;f++)face=KernelRuntime.Faces[face].NextInBody;
            var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag)];
            Console.WriteLine($"FAILED CUT FACE index={index} tag={KernelRuntime.Faces[face].Header.Tag} surface={surface.Header.Tag}/{surface.Class}");
            if(surface.Class==SurfaceClass.Plane)
            {
                var plane=KernelRuntime.PlaneDataPool[surface.DataIndex];
                Console.WriteLine($"FAILED CUT PLANE origin=({plane.LocationX:R},{plane.LocationY:R},{plane.LocationZ:R}) normal=({plane.NormalX:R},{plane.NormalY:R},{plane.NormalZ:R})");
            }
            if(surface.Class==SurfaceClass.Cylinder)
            {
                var cylinder=KernelRuntime.CylinderDataPool[surface.DataIndex];
                Console.WriteLine($"FAILED CUT CYLINDER origin=({cylinder.LocationX:R},{cylinder.LocationY:R},{cylinder.LocationZ:R}) axis=({cylinder.AxisX:R},{cylinder.AxisY:R},{cylinder.AxisZ:R}) radius={cylinder.Radius:R}");
            }
        }
    }
    Check(error,"cut fillet product");
    try{Require(n==1,"one cut result");return bodies[0];}finally{Free(own,bodies);}
}
static unsafe int CompareFresh(int own,int reference,string directory,string name,double unitScale,bool materialOwnership,bool checkNativeTrims,bool requireNativeFinBoxes,bool requireNativeFaceDomains,
    KernelRuntime.BoundaryTrace? projected=null)
{
    var fresh=Transmit(true,own);File.WriteAllBytes(Path.Combine(directory,name+".x_t"),fresh);var received=Receive(false,fresh);
    var referenceBytes=Transmit(false,reference);File.WriteAllBytes(Path.Combine(directory,name+"-native.x_t"),referenceBytes);
    var referenceReload=Receive(false,referenceBytes);
    WebProgressCapture.BindQuality(name,own,reference,received,fresh,referenceBytes);
    try
    {
        CheckBody(reference,name+" native");
        CheckBody(referenceReload,name+" native own roundtrip");
        try{ParasolidBodyCompare.AssertEquivalent(reference,received,name);}catch(Exception ex){Console.WriteLine("COMPARE DIAGNOSTIC "+ex.Message);throw;}
        CheckBody(received,name);
        CompareFiniteIntersections(reference,received,name,unitScale);
        if(requireNativeFaceDomains)CompareNativeFaceDomains(reference,received,name,unitScale);
        if(checkNativeTrims)CheckSelfNativeTrims(own,name,unitScale);
        if(materialOwnership)CompareShortEdgeMaterial(reference,own,received,name,unitScale);
        if(projected.HasValue)CheckProjectedMaterial(reference,own,received,projected.Value,name);
        var failures=0;
        try{Mass(reference,received,name);}
        catch(Exception ex){Console.WriteLine("FAIL "+name+" mass: "+ex.Message);failures++;}
        if(requireNativeFinBoxes)CompareNativeBlendFinBoxes(reference,received,name);
        return failures;
    }
    finally{Check(PK_ENTITY_delete(1,&received),"delete lifecycle reload");Check(PK_ENTITY_delete(1,&referenceReload),"delete native lifecycle reload");}
}

static unsafe int OverlappingToolJunctions(int selfSource,int nativeSource,string directory,string name,bool projectedCell)
{
    int own,native;Check(KernelRuntime.EntityCopy(selfSource,&own),"copy live self Fix for overlapping tools");
    Check(PK_ENTITY_copy(nativeSource,&native),"copy independent PK Fix for overlapping tools");
    var q=1/Math.Sqrt(2);var center=(-7-3.8e-5)*q-.075;
    var first=new PK_AXIS2_sf_t(new(-1.5,(1+center)*q,(1-center)*q),new(0,q,q),new(0,q,-q));
    var second=new PK_AXIS2_sf_t(new(projectedCell?-1.55:-1.60,-2.03,4.8),new(0,0,1),new(1,0,0));
    var width=projectedCell?.10:.14;
    int* ownTools=stackalloc int[2];int* nativeTools=stackalloc int[2];
    Check(KernelRuntime.BodyCreateSolidBlock(.15,.4,2.5,(M.PK_AXIS2_sf_s*)&first,ownTools),"self transverse tool");
    Check(KernelRuntime.BodyCreateSolidBlock(width,.16,1.3,(M.PK_AXIS2_sf_s*)&second,ownTools+1),"self overlapping tool");
    var firstWalls=ToolPlanes(ownTools[0]);var secondWalls=ToolPlanes(ownTools[1]);
    Check(PK_BODY_create_solid_block(.15,.4,2.5,&first,nativeTools),"native transverse tool");
    Check(PK_BODY_create_solid_block(width,.16,1.3,&second,nativeTools+1),"native overlapping tool");
    var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c};
    PK_TOPOL_track_r_t track=default;PK_boolean_r_t result=default;
    M.PK_TOPOL_track_r_s ownTrack=default;M.PK_boolean_r_s ownResult=default;
    var previous=KernelRuntime.TraceFiniteBoundary;var oldGuard=KernelRuntime.ForbidSpatialBoundaryReuse;
    var stations=new Dictionary<int,int>();var rolling=new HashSet<int>();
    var transfers=new List<(KernelRuntime.BoundaryTrace Trace,int Tool)>();var products=new List<KernelRuntime.BoundaryTrace>();
    var arranged=new List<KernelRuntime.BoundaryTrace>();var projected=new List<KernelRuntime.BoundaryTrace>();
    try
    {
        Check(PK_BODY_boolean_2(native,2,nativeTools,&options,&track,&result),"native overlapping subtraction");
        Require(result.n_bodies==1,"one independent two-tool product");native=result.bodies[0];
        KernelRuntime.ForbidSpatialBoundaryReuse=false;
        KernelRuntime.TraceFiniteBoundary=t=>
        {
            if(t.Stage==KernelRuntime.BoundaryTraceStage.ThirdFaceStation)
            {
                stations[t.StartEvent]=stations.GetValueOrDefault(t.StartEvent)+1;
                var support=KernelRuntime.GetSurfaceSlotByTag(t.OtherSurface);
                if(support>=0&&KernelRuntime.Surfaces[support].Class==SurfaceClass.BlendSurface)
                    rolling.Add(t.StartEvent);
            }
            if(t.Stage==KernelRuntime.BoundaryTraceStage.NativeCutLocator&&SurfaceKind(t.OtherSurface)==SurfaceClass.BlendSurface
                &&SurfaceKind(t.Surface)==SurfaceClass.Plane&&KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(t.Curve)].Class==CurveClass.Line)
            {
                var wall=KernelRuntime.PlaneDataPool[KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(t.Surface)].DataIndex];
                var owner=(firstWalls.Any(p=>SamePlane(p,wall))?1:0)|(secondWalls.Any(p=>SamePlane(p,wall))?2:0);
                transfers.Add((t,owner));
            }
            if(t.Stage==KernelRuntime.BoundaryTraceStage.Arrangement)arranged.Add(t);
            if(t.Stage==KernelRuntime.BoundaryTraceStage.Product)products.Add(t);
            if(t.Stage==KernelRuntime.BoundaryTraceStage.ProjectedCellReplacement)projected.Add(t);
            previous?.Invoke(t);
        };
        var error=KernelRuntime.BodyBoolean2(own,2,ownTools,(M.PK_BODY_boolean_o_s*)&options,&ownTrack,&ownResult);
        Check(error,"guarded self overlapping subtraction");
        Require(ownResult.n_bodies==1,"one self two-tool product");own=ownResult.bodies[0];
        foreach(var group in transfers.GroupBy(t=>(t.Trace.StartEvent,t.Trace.Curve)))
        {
            // After tool union the common finite line is a source edge.
            // Both wall coedges must transfer the same root into distinct
            // rolling cuts, and the product must retain all three uses.
            if(!group.Any(t=>t.Tool==1)||!group.Any(t=>t.Tool==2)||group.Select(t=>t.Trace.Surface).Distinct().Count()!=2
                ||group.Select(t=>t.Trace.ProductCurve).Distinct().Count()!=2)continue;
            var incident=products.Where(t=>t.StartEvent==group.Key.StartEvent||t.EndEvent==group.Key.StartEvent).ToArray();
            if(incident.Select(t=>t.Edge).Distinct().Count()==3&&group.All(t=>incident.Any(p=>p.Curve==t.Trace.ProductCurve)))rolling.Add(group.Key.StartEvent);
        }
        Console.WriteLine($"OVERLAPPING TOOLS self={error} stage={KernelRuntime.LastRegularBooleanStage}/{KernelRuntime.LastSheetPartitionStage} rolling_events={rolling.Count} projected_cells={projected.Count}");
        Require(rolling.Count==(projectedCell?1:2)&&rolling.All(e=>!stations.TryGetValue(e,out var n)||n==3),"each surviving rolling junction is shared by three finite occurrences");
        Require(projected.Count==(projectedCell?1:0),"only the certified projected cell is replaced");
        foreach(var change in projected)
        {
            var uses=products.Where(t=>t.Edge==change.Edge&&t.Curve==change.ProductCurve).ToArray();
            Require(uses.Length==2&&uses[0].Forward!=uses[1].Forward&&uses.All(use=>arranged.Any(a=>a.Edge==use.Edge&&a.Curve==use.Curve&&a.Surface==use.Surface)),
                "projected boundary reaches two opposite product uses through shared arrangement");
        }
        return CompareFresh(own,native,directory,name,1,false,true,true,true,projectedCell?projected[0]:null);
    }
    finally
    {
        KernelRuntime.TraceFiniteBoundary=previous;KernelRuntime.ForbidSpatialBoundaryReuse=oldGuard;
        Check(PK_TOPOL_track_r_f(&track),"free native overlapping tracking");Check(PK_boolean_r_f(&result),"free native overlapping result");
        Check(KernelRuntime.TopolTrackResultFree(&ownTrack),"free self overlapping tracking");Check(KernelRuntime.BooleanResultFree(&ownResult),"free self overlapping result");
        Check(PK_ENTITY_delete(1,&native),"delete native overlapping copy");Check(KernelRuntime.EntityDelete(1,&own),"delete self overlapping copy");
    }

    static SurfaceClass SurfaceKind(int surface)
    {
        var slot=KernelRuntime.GetSurfaceSlotByTag(surface);
        return slot<0?default:KernelRuntime.Surfaces[slot].Class;
    }

    static PlaneData[] ToolPlanes(int body)
    {
        var record=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];var face=record.FirstFaceBody;
        var planes=new PlaneData[record.FaceCountBody];
        for(var i=0;i<planes.Length;i++,face=KernelRuntime.Faces[face].NextInBody)
            planes[i]=KernelRuntime.PlaneDataPool[KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag)].DataIndex];
        return planes;
    }

    static bool SamePlane(PlaneData a,PlaneData b)
    {
        const double arithmetic=5e-13;
        var sense=a.NormalX*b.NormalX+a.NormalY*b.NormalY+a.NormalZ*b.NormalZ>0?1:-1;
        return Math.Abs(a.NormalX-sense*b.NormalX)+Math.Abs(a.NormalY-sense*b.NormalY)+Math.Abs(a.NormalZ-sense*b.NormalZ)<arithmetic
            &&Math.Abs((a.LocationX-b.LocationX)*a.NormalX+(a.LocationY-b.LocationY)*a.NormalY+(a.LocationZ-b.LocationZ)*a.NormalZ)<arithmetic;
    }
}

static unsafe void CheckProjectedMaterial(int native,int own,int received,in KernelRuntime.BoundaryTrace trace,string label)
{
    var start=new PK_VECTOR_t(trace.StartPoint.X,trace.StartPoint.Y,trace.StartPoint.Z);
    var end=new PK_VECTOR_t(trace.EndPoint.X,trace.EndPoint.Y,trace.EndPoint.Z);
    int n;int* edges;Check(PK_BODY_ask_edges(native,&n,&edges),"projected native edges");
    var match=0;var ends=stackalloc PK_VECTOR_t[2];
    try
    {
        for(var i=0;i<n;i++)
        {
            int curve,kind;PK_INTERVAL_t range;PK_LOGICAL_t sense;
            Check(PK_EDGE_ask_geometry(edges[i],PK_LOGICAL_true,&curve,&kind,ends,&range,&sense),"projected native edge use");
            if(kind!=PK_CLASS_line||!Pair(start,end,ends[0],ends[1],5e-9))continue;
            Require(match==0,"unique independent projected edge");match=edges[i];
        }
    }
    finally{Free(false,edges);}
    Require(match!=0,"independent projected line is present");
    int count;int* faces;Check(PK_EDGE_ask_faces(match,&count,&faces),"projected native adjacent faces");
    var midpoint=new PK_VECTOR_t((start.coord[0]+end.coord[0])*.5,(start.coord[1]+end.coord[1])*.5,(start.coord[2]+end.coord[2])*.5);
    var outward=new PK_VECTOR_t();var jet=stackalloc PK_VECTOR_t[4];
    try
    {
        Require(count==2,"projected line has two native uses");
        for(var i=0;i<count;i++)
        {
            int support;PK_LOGICAL_t sense;PK_UV_t uv;
            Check(PK_FACE_ask_oriented_surf(faces[i],&support,&sense),"projected material support");
            Check(PK_SURF_parameterise_vector(support,midpoint,&uv),"projected material parameter");
            Check(PK_SURF_eval(support,uv,1,1,PK_LOGICAL_false,jet),"projected material support normal");
            var normal=Cross(jet[1],jet[2]);var scale=(sense==PK_LOGICAL_true?1:-1)/Math.Sqrt(Dot(normal,normal));
            for(var axis=0;axis<3;axis++)outward.coord[axis]+=normal.coord[axis]*scale;
        }
    }
    finally{Free(false,faces);}
    var length=Distance(start,end);var magnitude=Math.Sqrt(Dot(outward,outward));
    Require(length>64*5e-9&&magnitude>0,"projected material witness is separated from the endpoints");
    foreach(var fraction in new[]{.25,.5,.75})foreach(var side in new[]{-1,1})
    {
        var point=new PK_VECTOR_t();var displacement=side*Math.Min(length/32,64*5e-9)/magnitude;
        for(var axis=0;axis<3;axis++)point.coord[axis]=start.coord[axis]+fraction*(end.coord[axis]-start.coord[axis])+displacement*outward.coord[axis];
        int expected,actual,fresh,entity;
        Check(PK_BODY_contains_vector(native,point,&expected,&entity),"projected native material");
        Check(KernelRuntime.BodyContainsVector(own,*(M.PK_VECTOR_s*)&point,&actual,&entity),"projected self material");
        Check(PK_BODY_contains_vector(received,point,&fresh,&entity),"projected fresh material");
        var enclosure=side<0?PK_enclosure_inside_c:PK_enclosure_outside_c;
        Require(expected==enclosure&&actual==enclosure&&fresh==enclosure,$"{label} projected material native/self/fresh={expected}/{actual}/{fresh}");
    }
    Console.WriteLine($"PROJECTED MATERIAL {label} probes=6 native/self/fresh agree");
}

static unsafe void CompareNativeFaceDomains(PK_BODY_t reference,PK_BODY_t actual,string label,double unitScale)
{
    var tolerance=5e-9*unitScale;
    int na,nb;PK_FACE_t* a;PK_FACE_t* b=null;
    Check(PK_BODY_ask_faces(reference,&na,&a),label+" reference faces");
    try
    {
        Check(PK_BODY_ask_faces(actual,&nb,&b),label+" self faces");
        Require(na==nb&&na>0,label+" finite face counts differ");
        Span<byte> used=nb<=256?stackalloc byte[nb]:new byte[nb];used.Clear();
        var options=new PK_FACE_is_coincident_o_t();
        for(var i=0;i<na;i++)
        {
            var matched=-1;
            for(var j=0;j<nb;j++)
            {
                if(used[j]!=0)continue;
                PK_FACE_coi_t result;PK_VECTOR_t point;
                Check(PK_FACE_is_coincident(a[i],b[j],tolerance,&options,&result,&point),$"{label} finite faces {a[i]}/{b[j]}");
                if(result!=PK_FACE_coi_yes_c)continue;
                Require(matched<0,$"{label} ambiguous finite face {a[i]}");
                matched=j;
            }
            if(matched<0)
            {
                for(var j=0;j<nb;j++)if(used[j]==0)
                {
                    PK_FACE_coi_t result;PK_VECTOR_t p;
                    Check(PK_FACE_is_coincident(a[i],b[j],tolerance,&options,&result,&p),$"{label} finite face diagnostic");
                    Console.WriteLine($"FINITE FACE MISS {label} faces={a[i]}/{b[j]} result={result} point=({p.coord[0]:R},{p.coord[1]:R},{p.coord[2]:R})");
                }
                throw new InvalidOperationException($"{label} no same-oriented finite match for face {a[i]}");
            }
            PK_FACE_coi_t reverse;PK_VECTOR_t witness;
            Check(PK_FACE_is_coincident(b[matched],a[i],tolerance,&options,&reverse,&witness),$"{label} reverse finite face comparison");
            Require(reverse==PK_FACE_coi_yes_c,$"{label} reverse finite face {b[matched]}/{a[i]} result={reverse} witness=({witness.coord[0]:R},{witness.coord[1]:R},{witness.coord[2]:R})");
            used[matched]=1;
        }
        Console.WriteLine($"NATIVE FINITE FACE DOMAINS {label} matched={na} both_directions=True tolerance={tolerance:R}");
    }
    finally{Free(false,b);Free(false,a);}
}
static unsafe void CheckSelfNativeTrims(int body,string label,double unitScale)
{
    var scratch=KernelRuntime.EnsureCommandScratch();
    try
    {
        var record=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(body).Slot];var slot=record.FirstEdgeBody;
        var count=0;var maximum=0.0;
        for(var i=0;i<record.EdgeCountBody;i++,slot=KernelRuntime.Edges[slot].NextInBody)
        {
            var edge=KernelRuntime.Edges[slot];var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(edge.CurveTag)];
            if(curve.Class!=CurveClass.ICurve)continue;
            var data=KernelRuntime.ICurveDataPool[curve.DataIndex];
            Require(data.ProgramBlock>0&&data.UvType==IntersectionUvType.Both&&data.ChartCount>=2&&data.UvValueCount>=4*data.ChartCount,
                $"{label} ICurve {edge.Header.Tag} requires native paired CHART");
            Require(edge.HasParameterRange!=0&&edge.ParameterStart>=curve.TMin&&edge.ParameterEnd<=curve.TMax&&edge.ParameterStart<edge.ParameterEnd,
                $"{label} ICurve {edge.Header.Tag} finite native trim");
            var first=edge.FirstFinEdge;var second=KernelRuntime.Fins[first].Other;
            Require(edge.FinCount==2&&second>=0&&KernelRuntime.Fins[second].Other==first&&KernelRuntime.Fins[first].Sense!=KernelRuntime.Fins[second].Sense,
                $"{label} ICurve {edge.Header.Tag} needs two opposite fin uses");
            var use=new KernelRuntime.ModelingEdge{Curve=edge.CurveTag,Start=edge.ParameterStart,End=edge.ParameterEnd,StartVertex=edge.StartVertex,EndVertex=edge.EndVertex};
            for(var side=0;side<2;side++)
            {
                var fin=KernelRuntime.Fins[side==0?first:second];var face=KernelRuntime.Loops[fin.Loop].Face;var surface=KernelRuntime.Faces[face].SurfTag;
                for(var station=0;station<=4;station++)
                {
                    Check(KernelRuntime.SheetCoedgePoint(surface,use,fin.Sense=='+'?(byte)1:(byte)0,station*.25,out var uv,out _,out var point),"native coedge UV");
                    M.PK_VECTOR_s evaluated;Check(KernelRuntime.SurfEval(surface,uv,0,0,0,&evaluated),"evaluate actual supporting face");
                    var dx=evaluated.coord[0]-point.X;var dy=evaluated.coord[1]-point.Y;var dz=evaluated.coord[2]-point.Z;
                    var distance=Math.Sqrt(dx*dx+dy*dy+dz*dz);maximum=Math.Max(maximum,distance);
                    Require(distance<=Math.Max(edge.Tolerance,5e-9*unitScale),$"{label} edge={edge.Header.Tag} side={side} station={station} XYZ/UV={distance:R}");
                }
            }
            count++;
        }
        Require(count>0,label+" native finite ICurve checks");
        Console.WriteLine($"FINITE SELF UV {label} native_ICurves={count} paired_checks={10*count} max={maximum:R}");
    }
    finally{if(scratch)KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session);}
}
static unsafe void CompareShortEdgeMaterial(int reference,int managed,int fresh,string label,double unitScale)
{
    int count;int* edges;Check(PK_BODY_ask_edges(reference,&count,&edges),"material reference edges");var tested=0;
    PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];PK_VECTOR_t* jet=stackalloc PK_VECTOR_t[4];
    try
    {
        for(var e=0;e<count;e++)
        {
            int curve,type;PK_INTERVAL_t range;PK_LOGICAL_t sense;
            Check(PK_EDGE_ask_geometry(edges[e],PK_LOGICAL_true,&curve,&type,ends,&range,&sense),"material finite edge");
            if(type!=PK_CLASS_icurve)continue;
            var length=ParasolidBodyCompare.FiniteLength(curve,range,1).Value;
            if(length/unitScale>=1e-3)continue;
            double precision;Check(PK_EDGE_ask_precision(edges[e],&precision),"material edge precision");
            var resolution=Math.Max(precision,5e-9*unitScale);
            var preferred=Math.Min(length/32,64*resolution);
            if(preferred<=16*resolution)preferred=64*resolution;
            var before=tested;
            int adjacent;int* faces;Check(PK_EDGE_ask_faces(edges[e],&adjacent,&faces),"material edge face occurrences");
            try
            {
                Require(adjacent==2,"material probe at a manifold edge");
                foreach(var fraction in new[]{.25,.5,.75})
                {
                    PK_VECTOR_t p;Check(PK_CURVE_eval(curve,range.value[0]+fraction*(range.value[1]-range.value[0]),0,&p),"material edge witness");
                    // Use the actual clearance of this native station. On
                    // a very short remnant the midpoint can have room even
                    // when the whole-edge offset heuristic finds none.
                    var clearance=Math.Min(Distance(p,ends[0]),Distance(p,ends[1]));
                    var offset=Math.Min(preferred,clearance/4);
                    if(!(offset>16*resolution))continue;
                    var n=new PK_VECTOR_t();
                    for(var i=0;i<adjacent;i++)
                    {
                        int surface;PK_LOGICAL_t orientation;PK_UV_t uv;
                        Check(PK_FACE_ask_oriented_surf(faces[i],&surface,&orientation),"material oriented support");
                        Check(PK_SURF_parameterise_vector(surface,p,&uv),"material UV witness");
                        Check(PK_SURF_eval(surface,uv,1,1,PK_LOGICAL_false,jet),"material support jet");
                        var normal=Cross(jet[1],jet[2]);var norm=Math.Sqrt(Dot(normal,normal));
                        Require(norm>0,"regular material germ");
                        for(var axis=0;axis<3;axis++)n.coord[axis]+=(orientation==PK_LOGICAL_true?1:-1)*normal.coord[axis]/norm;
                    }
                    var magnitude=Math.Sqrt(Dot(n,n));Require(magnitude>0,"transverse material directions");
                    for(var sign=-1;sign<=1;sign+=2)
                    {
                        var q=p;for(var axis=0;axis<3;axis++)q.coord[axis]+=sign*offset*n.coord[axis]/magnitude;
                        int expected,actual,reloaded,entity;
                        Check(PK_BODY_contains_vector(reference,q,&expected,&entity),"material native classification");
                        Check(KernelRuntime.BodyContainsVector(managed,*(M.PK_VECTOR_s*)&q,&actual,&entity),"material self classification");
                        Check(PK_BODY_contains_vector(fresh,q,&reloaded,&entity),"material fresh classification");
                        var side=sign<0?PK_enclosure_inside_c:PK_enclosure_outside_c;
                        Require(expected==side&&actual==side&&reloaded==side,$"{label} material edge={edges[e]} fraction={fraction:R} offset={sign*offset:R} native/self/fresh={expected}/{actual}/{reloaded} stage={KernelRuntime.LastRegularClassificationStage}");
                        tested++;
                    }
                }
            }
            finally{Free(false,faces);}
            Require(tested>before,$"{label} edge={edges[e]} material probes with finite separation from both endpoints");
        }
    }
    finally{Free(false,edges);}
    Require(tested>0,label+" material probes with finite separation from both endpoints");
    Console.WriteLine($"MATERIAL {label} probes={tested} native/self/fresh agree on both sides of the short ICurves");
}
static unsafe void Mass(int reference,int actual,string label)
{
    Require(ParasolidMassProperties.Compare(reference,actual,label)!=MassAgreement.Different,"mass intervals prove a difference at the required precision");
}
static unsafe void Probes(int reference,int imported,int fresh,byte[] nativeBytes,string output,string name)
{
    var nativeReload=Receive(false,nativeBytes);CheckBody(nativeReload,"probe native roundtrip");
    var csv=new System.Text.StringBuilder("edge,fraction,axis,offset,edge_precision,x,y,z,PK_native,PK_roundtrip,self_import,PK_self_fresh\n");
    var mismatches=0;var transport=0;var total=0;int n;int* edges;Check(PK_BODY_ask_edges(reference,&n,&edges),"probe edges");
    PK_VECTOR_t* endpoints=stackalloc PK_VECTOR_t[2];int* results=stackalloc int[4];int* topols=stackalloc int[4];
    try
    {
        for(var e=0;e<n;e++)
        {
            int curve,type;PK_LOGICAL_t sense;PK_INTERVAL_t range,lengthRange;double length,precision;
            Check(PK_EDGE_ask_geometry(edges[e],PK_LOGICAL_true,&curve,&type,endpoints,&range,&sense),"probe geometry");
            Check(PK_CURVE_find_length(curve,range,&length,&lengthRange),"probe arc length");if(length>=1e-3)continue;
            Check(PK_EDGE_ask_precision(edges[e],&precision),"probe precision");
            foreach(var fraction in new[]{0,.001,.5,.999,1})
            {
                PK_VECTOR_t point;Check(PK_CURVE_eval(curve,range.value[0]+fraction*(range.value[1]-range.value[0]),0,&point),"probe seed");
                for(var axis=0;axis<3;axis++)foreach(var delta in new[]{0,2e-9,-2e-9,2e-8,-2e-8,1e-7,-1e-7,.5*precision,-.5*precision,.99*precision,-.99*precision,precision,-precision,1.01*precision,-1.01*precision,2*precision,-2*precision})
                {
                    var query=point;query.coord[axis]+=delta;
                    Check(PK_BODY_contains_vector(reference,query,results,topols),"native classify");
                    Check(PK_BODY_contains_vector(nativeReload,query,results+1,topols+1),"native roundtrip classify");
                    var selfError=KernelRuntime.BodyContainsVector(imported,*(M.PK_VECTOR_s*)&query,results+2,topols+2);
                    if(selfError!=0){results[2]=-selfError;Console.WriteLine($"CLASSIFY ERROR {selfError} stage={KernelRuntime.LastRegularClassificationStage} entity={KernelRuntime.LastRegularClassificationEntity} p=({query.coord[0]:R},{query.coord[1]:R},{query.coord[2]:R})");}
                    Check(PK_BODY_contains_vector(fresh,query,results+3,topols+3),"self XT classify");total++;
                    csv.AppendLine(FormattableString.Invariant($"{edges[e]},{fraction:R},{axis},{delta:R},{precision:R},{query.coord[0]:R},{query.coord[1]:R},{query.coord[2]:R},{results[0]},{results[1]},{results[2]},{results[3]}"));
                    if(results[0]!=results[1])transport++;
                    if(results[0]!=results[2]||results[0]!=results[3])
                    {
                        mismatches++;
                        Console.WriteLine($"PROBE DIFF edge={edges[e]} fraction={fraction:R} p=({query.coord[0]:R},{query.coord[1]:R},{query.coord[2]:R}) offset={delta:R} precision={precision:R} native/roundtrip/self/fresh={results[0]}/{results[1]}/{results[2]}/{results[3]} nearest={topols[0]}/{topols[1]}/{topols[2]}/{topols[3]}");
                    }
                }
            }
        }
    }
    finally
    {
        File.WriteAllText(Path.Combine(output,name+"-probes.csv"),csv.ToString());Free(false,edges);Check(PK_ENTITY_delete(1,&nativeReload),"delete probe native reload");
    }
    Console.WriteLine($"PROBES total={total} mismatches={mismatches} native_roundtrip_differences={transport}");Require(mismatches==0,"classification differences; exact coordinates saved");
}
static unsafe void SelfGeometry(int own,int reference)
{
    var started=Stopwatch.GetTimestamp();
    int count;int* nativeFaces;Check(PK_BODY_ask_faces(reference,&count,&nativeFaces),"self geometry native faces");
    var body=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(own).Slot];var face=body.FirstFaceBody;var checkedFaces=0;
    PK_VECTOR_t* native=stackalloc PK_VECTOR_t[9];PK_VECTOR_t* actual=stackalloc PK_VECTOR_t[9];
    PK_VECTOR_t* plus=stackalloc PK_VECTOR_t[4];PK_VECTOR_t* minus=stackalloc PK_VECTOR_t[4];
    PK_VECTOR_t* finePlus=stackalloc PK_VECTOR_t[4];PK_VECTOR_t* fineMinus=stackalloc PK_VECTOR_t[4];
    var checkpoint=KernelRuntime.EnsureCommandScratch();
    try
    {
        // The finite membership stations share this immutable B-rep. Reuse
        // exact interval bounds for the same native CHART cells; each ray
        // still isolates its own roots and performs the full winding test.
        ModelingPerformance.Context proof=default;using var scope=new ModelingPerformance.Scope(&proof,immutableGeometry:true);
        for(var f=0;f<body.FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var source=KernelRuntime.Faces[face];var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(source.SurfTag)];
            if(surface.Class is not (SurfaceClass.BlendSurface or SurfaceClass.Cylinder))continue;
            var vertices=ManagedFaceVertices(face);
            var matched=0;var matchedFace=0;PK_CURVE_t mappedSpine=0;ParametricSpineCurve mappedConic=default;
            double shiftU=0,scaleU=0,shiftV=0,scaleV=0;
            for(var n=0;n<count;n++)
            {
                int candidate,type;Check(PK_FACE_ask_surf(nativeFaces[n],&candidate),"self geometry surface");Check(PK_ENTITY_ask_class(candidate,&type),"self geometry class");
                if(surface.Class==SurfaceClass.Cylinder)
                {
                    if(type!=PK_CLASS_cyl)continue;var a=new M.PK_CYL_sf_s();var b=new PK_CYL_sf_t();
                    Check(KernelRuntime.CylAsk(source.SurfTag,&a),"self cylinder definition");Check(PK_CYL_ask(candidate,&b),"native cylinder definition");
                    if(!CylinderMap(*(PK_CYL_sf_t*)&a,b,out var su,out var du,out var sv,out var dv))continue;
                    if(!SameFiniteFaceVertices(vertices,nativeFaces[n]))continue;
                    Require(matched==0,"unique complete cylinder support correspondence");matched=candidate;matchedFace=nativeFaces[n];shiftU=su;scaleU=du;shiftV=sv;scaleV=dv;
                }
                else
                {
                    if(type!=PK_CLASS_blendsf)continue;var data=KernelRuntime.BlendedEdgeDataPool[surface.DataIndex];var program=KernelRuntime.GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];
                    var a=program.Spines[root.Spine].Curve;PK_BLENDSF_sf_t definition;Check(PK_BLENDSF_ask(candidate,&definition),"native rolling definition");
                    if(Math.Abs(Math.Abs(definition.radii[0])-Math.Abs(data.Range0))>1e-12||Math.Abs(Math.Abs(definition.radii[1])-Math.Abs(data.Range1))>1e-12)continue;
                    var own0=new M.PK_CYL_sf_s();var own1=new M.PK_CYL_sf_s();var pk0=new PK_CYL_sf_t();var pk1=new PK_CYL_sf_t();
                    Check(KernelRuntime.CylAsk(data.Surface0Tag,&own0),"first rolling support");Check(KernelRuntime.CylAsk(data.Surface1Tag,&own1),"second rolling support");
                    Check(PK_CYL_ask(definition.geom_1,&pk0),"native first rolling support");Check(PK_CYL_ask(definition.geom_2,&pk1),"native second rolling support");
                    var direct=CylinderMap(*(PK_CYL_sf_t*)&own0,pk0,out _,out _,out _,out _)&&CylinderMap(*(PK_CYL_sf_t*)&own1,pk1,out _,out _,out _,out _);
                    var swapped=CylinderMap(*(PK_CYL_sf_t*)&own0,pk1,out _,out _,out _,out _)&&CylinderMap(*(PK_CYL_sf_t*)&own1,pk0,out _,out _,out _,out _);
                    if(!direct&&!swapped)continue;
                    if(!SameFiniteFaceVertices(vertices,nativeFaces[n]))continue;
                    int spineClass;Check(PK_ENTITY_ask_class(definition.spine,&spineClass),"native rolling carrier class");
                    double phase,sign=0;var rational=spineClass==PK_CLASS_bcurve;
                    if(rational)
                    {
                        if(!CertifyNativeRollingGauge(definition.spine,a,surface.UMin,surface.UMax,out phase))continue;
                    }
                    else
                    {
                        if(spineClass!=PK_CLASS_ellipse)continue;
                        var ellipse=new PK_ELLIPSE_sf_t();Check(PK_ELLIPSE_ask(definition.spine,&ellipse),"native rolling carrier");
                        var x=AsVector(ellipse.basis_set.ref_direction);var y=Cross(AsVector(ellipse.basis_set.axis),x);
                        var b=new ParametricSpineCurve{Kind=CurveClass.Ellipse,Origin=Vector(ellipse.basis_set.location),
                            First=Vector(ScaleVector(x,ellipse.R1)),Second=Vector(ScaleVector(y,ellipse.R2))};
                        if(!AnalyticSpineContact.TryParameterMap(a,b,1e-10,definition.spine_extent.value[0],definition.spine_extent.value[1],out phase,out sign))continue;
                        phase+=Math.Tau*Math.Round((surface.UMin+(surface.UMax-surface.UMin)*.5-phase-sign*(definition.spine_extent.value[0]+definition.spine_extent.value[1])*.5)/Math.Tau);
                    }
                    var box=new PK_UVBOX_t();Check(PK_FACE_find_uvbox(nativeFaces[n],&box),"native rolling finite box");
                    var test=new PK_UV_t();test.param[0]=(box.param[0]+box.param[2])*.5;test.param[1]=.25;
                    Check(PK_SURF_eval(candidate,test,0,0,PK_LOGICAL_false,native),"native rolling chart discriminator");
                    var same=0;double sectionSign=0,sectionShift=0;
                    for(var reverse=0;reverse<2;reverse++)
                    {
                        var uv=new M.PK_UV_s();uv.param[0]=rational?NativeRollingAngleJet(definition.spine,a,phase,test.param[0]).U:phase+sign*test.param[0];
                        uv.param[1]=reverse==0?.25:.75;
                        Check(KernelRuntime.SurfEval(source.SurfTag,uv,0,0,0,(M.PK_VECTOR_s*)actual),"self rolling chart discriminator");
                        if(Distance(native[0],actual[0])<1e-10){same++;sectionSign=reverse==0?1:-1;sectionShift=reverse;}
                    }
                    Require(same==1,"unique circular section map after complete spine/support certificates");
                    Require(matched==0,"unique rolling support correspondence");matched=candidate;matchedFace=nativeFaces[n];shiftU=phase;scaleU=sign;shiftV=sectionShift;scaleV=sectionSign;
                    if(rational){mappedSpine=definition.spine;mappedConic=a;}
                }
            }
            Require(matched!=0,"every curved face has a certified carrier/support correspondence");
            var bounds=new PK_UVBOX_t();Check(PK_FACE_find_uvbox(matchedFace,&bounds),"matched finite UV box");var samples=0;double d0=0,d1=0,d2=0;
            for(var i=1;i<=9;i++)for(var side=0;side<3;side++)
            {
                var uv=new PK_UV_t();uv.param[0]=bounds.param[0]+(bounds.param[2]-bounds.param[0])*(mappedSpine==0?i:i-.25)/10;
                uv.param[1]=bounds.param[1]+(bounds.param[3]-bounds.param[1])*(.1+side*.4);
                var gauge=mappedSpine==0?(U:shiftU+scaleU*uv.param[0],D1:scaleU,D2:0d):NativeRollingAngleJet(mappedSpine,mappedConic,shiftU,uv.param[0]);
                var ownUv=new M.PK_UV_s();ownUv.param[0]=gauge.U;ownUv.param[1]=shiftV+scaleV*uv.param[1];
                if(surface.Class==SurfaceClass.Cylinder)LiftUv(ref ownUv,surface);
                var containment=KernelRuntime.SheetSourceContains(face,ownUv,out var inside);Check(containment,"self finite face geometry membership");
                int on;var contains=new PK_FACE_contains_vectors_o_t{n_uvs=1,uvs=&uv};Check(PK_FACE_contains_vectors(matchedFace,&contains,&on),"native finite face membership");
                Require(inside==(on!=0),"finite face memberships agree under the certified map");if(!inside)continue;
                Check(PK_SURF_eval(matched,uv,2,2,PK_LOGICAL_false,native),"native mapped geometry jet");Check(KernelRuntime.SurfEval(source.SurfTag,ownUv,2,2,0,(M.PK_VECTOR_s*)actual),"self mapped geometry jet");samples++;
                d0=Math.Max(d0,Distance(native[0],actual[0]));d1=Math.Max(d1,Math.Max(Distance(native[1],ScaleVector(actual[1],gauge.D1)),Distance(native[3],ScaleVector(actual[3],scaleV))));
                if(side!=1)continue;
                const double h=1e-4;
                for(var axis=0;axis<2;axis++)
                {
                    var p=uv;var m=uv;var fp=uv;var fm=uv;p.param[axis]+=h;m.param[axis]-=h;fp.param[axis]+=h*.5;fm.param[axis]-=h*.5;
                    Check(PK_SURF_eval(matched,p,1,1,PK_LOGICAL_false,plus),"mapped D2 coarse plus");Check(PK_SURF_eval(matched,m,1,1,PK_LOGICAL_false,minus),"mapped D2 coarse minus");
                    Check(PK_SURF_eval(matched,fp,1,1,PK_LOGICAL_false,finePlus),"mapped D2 fine plus");Check(PK_SURF_eval(matched,fm,1,1,PK_LOGICAL_false,fineMinus),"mapped D2 fine minus");
                    for(var derivative=0;derivative<2;derivative++)for(var d=0;d<3;d++)
                    {
                        var index=derivative==0?1:2;var target=axis==0?(derivative==0?2:4):(derivative==0?4:6);
                        var chain=axis==0?(derivative==0?gauge.D1*gauge.D1:gauge.D1*scaleV):(derivative==0?gauge.D1*scaleV:scaleV*scaleV);
                        var expected=(4*(finePlus[index].coord[d]-fineMinus[index].coord[d])/h-(plus[index].coord[d]-minus[index].coord[d])/(2*h))/3;
                        var mapped=chain*actual[target].coord[d]+(axis==0&&derivative==0?gauge.D2*actual[1].coord[d]:0);
                        d2=Math.Max(d2,Math.Abs(expected-mapped)/(1+Math.Abs(expected)));
                    }
                }
            }
            Console.WriteLine($"SELF GEOMETRY face={source.Header.Tag} native_face={matchedFace} type={surface.Class} map_u={(mappedSpine==0?$"{shiftU:R}+{scaleU:R}*u":"certified rational angular gauge")} map_v={shiftV:R}+{scaleV:R}*v samples={samples} D0={d0:R} D1={d1:R} D2_Richardson={d2:R}");
            Require(samples>0&&d0<1e-10&&d1<1e-8&&d2<1e-6,"self finite geometry under certified parameter map");checkedFaces++;
        }
        Require(checkedFaces>0,"self curved geometry was checked");
        Console.WriteLine($"TIME SELF GEOMETRY ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}");
    }
    finally{if(checkpoint)KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session);Free(false,nativeFaces);}
}
// Certify the complete finite native spline before evaluating its angular map.
static unsafe bool CertifyNativeRollingGauge(PK_CURVE_t spine,in ParametricSpineCurve conic,double first,double last,out double anchor)
{
    anchor=0;var form=new PK_BCURVE_sf_t();Check(PK_BCURVE_ask(spine,&form),"native rational rolling carrier");
    try
    {
        if(form.is_rational==PK_LOGICAL_false||form.is_periodic!=PK_LOGICAL_false||form.vertex_dim!=4
            ||form.degree is <1 or >3||form.n_vertices>RationalConicGauge.MaxSpans*3+1)return false;
        Span<double> knots=stackalloc double[form.n_vertices+form.degree+1];var count=0;
        for(var k=0;k<form.n_knots;k++)for(var j=0;j<form.knot_mult[k];j++)
        {if(count==knots.Length)return false;knots[count++]=form.knot[k];}
        if(count!=knots.Length)return false;
        var view=new BCurveView(form.degree,4,true,false,new(form.vertex,4*form.n_vertices),knots);
        Span<RationalConicArc> arcs=stackalloc RationalConicArc[RationalConicGauge.MaxSpans];
        Span<IntervalNumber> x=stackalloc IntervalNumber[RationalConicGauge.MaxSpans*RationalConicGauge.Stride];
        Span<IntervalNumber> y=stackalloc IntervalNumber[x.Length];
        if(!RationalConicGauge.Certify(view,conic,1e-10,arcs,x,y,out var spans)
            ||!RationalConicGauge.NativeAngleLift(arcs[..spans],first,last,out var lift))return false;
        anchor=(arcs[0].FirstAngle.Midpoint+arcs[spans-1].LastAngle.Midpoint)*.5-lift.Midpoint;
        return true;
    }
    finally{Free(false,form.vertex);Free(false,form.knot);Free(false,form.knot_mult);}
}

static unsafe (double U,double D1,double D2) NativeRollingAngleJet(PK_CURVE_t native,in ParametricSpineCurve conic,double anchor,double t)
{
    PK_VECTOR_t* jet=stackalloc PK_VECTOR_t[3];Check(PK_CURVE_eval(native,t,2,jet),"native rolling angular jet");
    var a=EvaluationMath.Dot(conic.First,conic.First);var b=EvaluationMath.Dot(conic.Second,conic.Second);var c=EvaluationMath.Dot(conic.First,conic.Second);
    var determinant=a*b-c*c;
    var p=Coordinates(EvaluationMath.Sub(Vector(jet[0]),conic.Origin),conic,a,b,c,determinant);
    var d=Coordinates(Vector(jet[1]),conic,a,b,c,determinant);var e=Coordinates(Vector(jet[2]),conic,a,b,c,determinant);
    var angle=Math.Atan2(p.Y,p.X);angle+=Math.Tau*Math.Round((anchor-angle)/Math.Tau);
    var squared=p.X*p.X+p.Y*p.Y;var numerator=p.X*d.Y-p.Y*d.X;
    return (angle,numerator/squared,(p.X*e.Y-p.Y*e.X)/squared-2*numerator*(p.X*d.X+p.Y*d.Y)/(squared*squared));

    static (double X,double Y) Coordinates(in KernelVector3 v,in ParametricSpineCurve k,double a,double b,double c,double determinant)
    {
        var x=EvaluationMath.Dot(k.First,v);var y=EvaluationMath.Dot(k.Second,v);
        return ((b*x-c*y)/determinant,(a*y-c*x)/determinant);
    }
}
static unsafe List<PK_VECTOR_t> ManagedFaceVertices(FaceSlot face)
{
    var result=new List<PK_VECTOR_t>();var seen=new HashSet<VertexSlot>();var source=KernelRuntime.Faces[face];var loop=source.FirstLoop;
    for(var l=0;l<source.LoopCount;l++,loop=KernelRuntime.Loops[loop].NextInFace)
    {
        var fin=KernelRuntime.Loops[loop].FirstFin;
        for(var k=0;k<KernelRuntime.Loops[loop].FinCount;k++,fin=KernelRuntime.Fins[fin].NextInLoop)
        {
            var vertex=KernelRuntime.Fins[fin].Vertex;if(vertex<0||!seen.Add(vertex))continue;
            M.PK_POINT_sf_s point;Check(KernelRuntime.PointAsk(KernelRuntime.Vertices[vertex].PointTag,&point),"self face boundary vertex");
            result.Add(*(PK_VECTOR_t*)&point.position);
        }
    }
    return result;
}
static unsafe bool SameFiniteFaceVertices(List<PK_VECTOR_t> vertices,PK_FACE_t face)
{
    int count;PK_VERTEX_t* tags;Check(PK_FACE_ask_vertices(face,&count,&tags),"native face boundary vertices");
    try
    {
        if(count!=vertices.Count)return false;
        Span<byte> used=stackalloc byte[count];used.Clear();
        for(var i=0;i<count;i++)
        {
            var position=NativeVertexPosition(tags[i]);var match=-1;
            for(var j=0;j<vertices.Count;j++)if(used[j]==0&&Distance(position,vertices[j])<1e-8)
            {if(match>=0)return false;match=j;}
            if(match<0)return false;used[match]=1;
        }
        return true;
    }
    finally{Free(false,tags);}
}
static unsafe PK_VECTOR_t NativeVertexPosition(PK_VERTEX_t vertex)
{
    PK_POINT_t point;PK_POINT_sf_t definition;
    Check(PK_VERTEX_ask_point(vertex,&point),"native vertex point");
    Check(PK_POINT_ask(point,&definition),"native vertex position");return definition.position;
}
static unsafe KernelVector3 Vector(PK_VECTOR_t value)=>new(){X=value.coord[0],Y=value.coord[1],Z=value.coord[2]};
static unsafe PK_VECTOR_t ScaleVector(PK_VECTOR_t value,double scale)=>new(value.coord[0]*scale,value.coord[1]*scale,value.coord[2]*scale);
static unsafe PK_VECTOR_t AsVector(PK_VECTOR1_t value)=>new(value.coord[0],value.coord[1],value.coord[2]);
static unsafe void LiftUv(ref M.PK_UV_s uv,SurfaceRecord surface)
    =>uv.param[0]+=Math.Tau*Math.Round((surface.UMin+(surface.UMax-surface.UMin)*.5-uv.param[0])/Math.Tau);
// Complete cylinder coefficients give an affine angular/axial chart map.
static unsafe bool CylinderMap(PK_CYL_sf_t own,PK_CYL_sf_t native,out double shiftU,out double scaleU,out double shiftV,out double scaleV)
{
    shiftU=shiftV=scaleU=scaleV=0;var a=AsVector(own.basis_set.axis);var b=AsVector(native.basis_set.axis);var cosine=Dot(a,b);
    if(Math.Abs(Math.Abs(cosine)-1)>1e-12||Math.Abs(own.radius-native.radius)>1e-10)return false;
    var delta=new PK_VECTOR_t(own.basis_set.location.coord[0]-native.basis_set.location.coord[0],own.basis_set.location.coord[1]-native.basis_set.location.coord[1],own.basis_set.location.coord[2]-native.basis_set.location.coord[2]);
    if(Math.Sqrt(Dot(Cross(delta,b),Cross(delta,b)))>1e-10)return false;
    scaleU=scaleV=cosine>=0?1:-1;var ownX=AsVector(own.basis_set.ref_direction);var y=Cross(a,ownX);var nx=AsVector(native.basis_set.ref_direction);
    // This maps native coordinates to the managed cylinder chart.
    shiftU=Math.Atan2(Dot(nx,y),Dot(nx,ownX));shiftV=-Dot(delta,a);return true;
}

static unsafe void Geometry(int imported,int reference)
{
    int n;int* faces;Check(PK_BODY_ask_faces(reference,&n,&faces),"geometry faces");
    try
    {
        var native=new List<(int Face,int Surface)>();
        for(var i=0;i<n;i++)
        {
            int surface,type;Check(PK_FACE_ask_surf(faces[i],&surface),"geometry support");Check(PK_ENTITY_ask_class(surface,&type),"geometry class");
            if(type==PK_CLASS_blendsf)native.Add((faces[i],surface));
        }
        Require(native.Count!=0,"native procedural faces");
        var matched=new HashSet<int>();var body=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(imported).Slot];var face=body.FirstFaceBody;
        for(var i=0;i<body.FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var own=KernelRuntime.Faces[face].SurfTag;
            if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(own)].Class!=SurfaceClass.BlendSurface)continue;
            var vertices=ManagedFaceVertices(face);var candidate=(Face:0,Surface:0);
            foreach(var item in native)
            {
                if(!SameFiniteFaceVertices(vertices,item.Face))continue;
                Require(candidate.Face==0,"unique native match for imported procedural finite boundary");candidate=item;
            }
            Require(candidate.Face!=0&&matched.Add(candidate.Face),"one-to-one imported procedural face correspondence");
            GeometryOnNativeCharts(own,candidate.Face,candidate.Surface);
        }
        Require(matched.Count==native.Count,"all native procedural faces were compared after import");
    }
    finally{Free(false,faces);}
}

static unsafe void GeometryOnNativeCharts(int ownSurface,int nativeFace,int nativeSurface)
{
    var box=new PK_UVBOX_t();Check(PK_FACE_find_uvbox(nativeFace,&box),"finite UV box");
    var sf=new M.PK_BLENDSF_sf_s();Check(KernelRuntime.BlendSfAsk(ownSurface,&sf),"spine definition");
    var nativeDefinition=new PK_BLENDSF_sf_t();Check(PK_BLENDSF_ask(nativeSurface,&nativeDefinition),"native roundtrip spine definition");
    int nativeClass;Check(PK_ENTITY_ask_class(nativeDefinition.spine,&nativeClass),"native spine class");
    if(nativeClass==PK_CLASS_bcurve)CompareImportedSplineSpine(sf.spine,nativeDefinition.spine);
    else
    {
        var ownEllipse=new M.PK_ELLIPSE_sf_s();Check(KernelRuntime.EllipseAsk(sf.spine,&ownEllipse),"imported ellipse");
        var nativeEllipse=new PK_ELLIPSE_sf_t();Check(PK_ELLIPSE_ask(nativeDefinition.spine,&nativeEllipse),"native ellipse");
        for(var axis=0;axis<3;axis++)
            Require(Math.Abs(ownEllipse.basis_set.location.coord[axis]-nativeEllipse.basis_set.location.coord[axis])<1e-12
                &&Math.Abs(ownEllipse.basis_set.axis.coord[axis]-nativeEllipse.basis_set.axis.coord[axis])<1e-12
                &&Math.Abs(ownEllipse.basis_set.ref_direction.coord[axis]-nativeEllipse.basis_set.ref_direction.coord[axis])<1e-12,"imported spine retains native chart frame");
        Require(Math.Abs(ownEllipse.R1-nativeEllipse.R1)<1e-12&&Math.Abs(ownEllipse.R2-nativeEllipse.R2)<1e-12,"imported ellipse retains native dimensions");
    }
    Require(Math.Abs(sf.radii[0]-nativeDefinition.radii[0])<1e-12&&Math.Abs(sf.radii[1]-nativeDefinition.radii[1])<1e-12,
        "imported spine retains native rolling radii");
    PK_VECTOR_t* a=stackalloc PK_VECTOR_t[9];PK_VECTOR_t* b=stackalloc PK_VECTOR_t[9];PK_VECTOR_t* supportJet=stackalloc PK_VECTOR_t[4];
    PK_VECTOR_t* plus=stackalloc PK_VECTOR_t[4];PK_VECTOR_t* minus=stackalloc PK_VECTOR_t[4];PK_VECTOR_t* finePlus=stackalloc PK_VECTOR_t[4];PK_VECTOR_t* fineMinus=stackalloc PK_VECTOR_t[4];
    double d0=0,d1=0,d2=0,d2Math=0,radiusError=0,g1=0;var timer=Stopwatch.GetTimestamp();
    for(var i=1;i<=9;i++)for(var side=0;side<3;side++)
    {
        var uv=new PK_UV_t();uv.param[0]=box.param[0]+(box.param[2]-box.param[0])*i/10;uv.param[1]=side*.5;
        Check(PK_SURF_eval(nativeSurface,uv,2,2,PK_LOGICAL_false,a),"native jet");Check(KernelRuntime.SurfEval(ownSurface,*(M.PK_UV_s*)&uv,2,2,0,(M.PK_VECTOR_s*)b),"own jet");
        d0=Math.Max(d0,Distance(a[0],b[0]));d1=Math.Max(d1,Math.Max(Distance(a[1],b[1]),Distance(a[3],b[3])));d2=Math.Max(d2,Math.Max(Distance(a[2],b[2]),Math.Max(Distance(a[4],b[4]),Distance(a[6],b[6]))));
        var center=new PK_VECTOR_t();Check(KernelRuntime.CurveEval(sf.spine,uv.param[0],0,(M.PK_VECTOR_s*)&center),"exact spine center");radiusError=Math.Max(radiusError,Math.Abs(Distance(center,b[0])-Math.Abs(sf.radii[0])));
        if(side==1)
        {
            var h=1e-4;
            for(var axis=0;axis<2;axis++)
            {
                var p=uv;var m=uv;var fp=uv;var fm=uv;p.param[axis]+=h;m.param[axis]-=h;fp.param[axis]+=h*.5;fm.param[axis]-=h*.5;
                Check(PK_SURF_eval(nativeSurface,p,1,1,PK_LOGICAL_false,plus),"D2 coarse plus");Check(PK_SURF_eval(nativeSurface,m,1,1,PK_LOGICAL_false,minus),"D2 coarse minus");
                Check(PK_SURF_eval(nativeSurface,fp,1,1,PK_LOGICAL_false,finePlus),"D2 fine plus");Check(PK_SURF_eval(nativeSurface,fm,1,1,PK_LOGICAL_false,fineMinus),"D2 fine minus");
                for(var derivative=0;derivative<2;derivative++)for(var d=0;d<3;d++)
                {
                    var index=derivative==0?1:2;var target=axis==0?(derivative==0?2:4):(derivative==0?4:6);
                    var richardson=(4*(finePlus[index].coord[d]-fineMinus[index].coord[d])/h-(plus[index].coord[d]-minus[index].coord[d])/(2*h))/3;
                    d2Math=Math.Max(d2Math,Math.Abs(richardson-b[target].coord[d])/(1+Math.Abs(richardson)));
                }
            }
            continue;
        }
        var best=double.PositiveInfinity;
        for(var s=0;s<2;s++)
        {
            var tag=s==0?sf.geom_1:sf.geom_2;var at=new M.PK_UV_s();var error=KernelRuntime.SurfParameteriseVector(tag,*(M.PK_VECTOR_s*)b,&at);if(error==PK_ERROR_not_on_surface)continue;Check(error,"contact inverse");
            Check(KernelRuntime.SurfEval(tag,at,1,1,0,(M.PK_VECTOR_s*)supportJet),"contact support jet");var na=Cross(b[1],b[3]);var nb=Cross(supportJet[1],supportJet[2]);var cosine=Dot(na,nb)/Math.Sqrt(Dot(na,na)*Dot(nb,nb));best=Math.Min(best,Math.Abs(1-Math.Abs(cosine)));
        }
        Require(double.IsFinite(best),"contact lies on a true support");g1=Math.Max(g1,best);
    }
    Console.WriteLine($"GEOMETRY samples=27 D0={d0:R} D1={d1:R} raw_PK_D2_diagnostic={d2:R} D2_Richardson_normalized={d2Math:R} radius={radiusError:R} G1={g1:R} ms={Stopwatch.GetElapsedTime(timer).TotalMilliseconds:F3}");
    Require(d0<1e-10&&d1<1e-8&&d2Math<1e-6&&radiusError<1e-10&&g1<1e-10,"geometry correspondence");
}
static unsafe void CompareImportedSplineSpine(int own,int native)
{
    var a=new M.PK_BCURVE_sf_s();Check(KernelRuntime.BCurveAsk(own,&a),"imported spline spine");
    try
    {
        var b=new PK_BCURVE_sf_t();Check(PK_BCURVE_ask(native,&b),"native spline spine");
        try
        {
            var expected=*(M.PK_BCURVE_sf_s*)&b;
            Require(a.degree==expected.degree&&a.n_vertices==expected.n_vertices&&a.vertex_dim==expected.vertex_dim
                &&a.is_rational==expected.is_rational&&a.is_periodic==expected.is_periodic&&a.is_closed==expected.is_closed
                &&a.n_knots==expected.n_knots,"imported spline spine representation");
            for(var k=0;k<a.n_knots;k++)Require(a.knot[k]==expected.knot[k]&&a.knot_mult[k]==expected.knot_mult[k],
                "imported spline spine native knot and multiplicity");
            for(var k=0;k<a.n_vertices*a.vertex_dim;k++)
                Require(Math.Abs(a.vertex[k]-expected.vertex[k])<=1e-12,"imported spline spine homogeneous coefficient");
            Console.WriteLine($"GEOMETRY native spline spine degree={a.degree} poles={a.n_vertices} distinct_knots={a.n_knots}");
        }
        finally{Free(false,b.vertex);Free(false,b.knot);Free(false,b.knot_mult);}
    }
    finally{Free(true,a.vertex);Free(true,a.knot);Free(true,a.knot_mult);}
}
static unsafe void SavedProbes(int reference,int imported,int fresh,byte[] nativeBytes,string output,string name)
{
    var path=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","docs","reviews","advanced-modeling","short-edge-spine-probes.csv");
    var rows=File.ReadAllLines(path);var nativeReload=Receive(false,nativeBytes);var failures=0;var changedNative=0;var pending=0;
    var evidence=new List<(PK_VECTOR_t Point,int Row)>();
    var nativeRoundtrip=new List<int>();var gmNative=new List<int>();var gmFresh=new List<int>();var freshNative=new List<int>();
    using var classifications=new StreamWriter(new FileStream(Path.Combine(output,name+"-fixed-classifications.csv"),FileMode.CreateNew));
    classifications.WriteLine("row,x,y,z,PK_native,PK_roundtrip,GM_actual,PK_GM_fresh,status");
    int* results=stackalloc int[4];int* topols=stackalloc int[4];
    try
    {
        for(var i=1;i<rows.Length;i++)
        {
            var fields=rows[i].Split(',');Require(fields.Length==12,"saved probe columns");
            // Replay the discovered double coordinates verbatim. The old
            // native edge tag labels evidence only; it never selects geometry.
            var p=new PK_VECTOR_t(double.Parse(fields[5],System.Globalization.CultureInfo.InvariantCulture),double.Parse(fields[6],System.Globalization.CultureInfo.InvariantCulture),double.Parse(fields[7],System.Globalization.CultureInfo.InvariantCulture));
            Check(PK_BODY_contains_vector(reference,p,results,topols),"saved native classify");Check(PK_BODY_contains_vector(nativeReload,p,results+1,topols+1),"saved native roundtrip classify");
            var error=KernelRuntime.BodyContainsVector(imported,*(M.PK_VECTOR_s*)&p,results+2,topols+2);if(error!=0)results[2]=-error;
            Check(PK_BODY_contains_vector(fresh,p,results+3,topols+3),"saved fresh classify");
            if(results[0]!=int.Parse(fields[8],System.Globalization.CultureInfo.InvariantCulture))changedNative++;
            if(results[0]!=results[1])nativeRoundtrip.Add(i);
            if(results[0]!=results[2])gmNative.Add(i);
            if(results[2]!=results[3])gmFresh.Add(i);
            if(results[0]!=results[3])freshNative.Add(i);
            var status=ParasolidProbeAgreement.Classify(new ReadOnlySpan<int>(results,4));
            if(status==ProbeAgreement.Different)failures++;
            if(status==ProbeAgreement.Unresolved)pending++;
            classifications.WriteLine(FormattableString.Invariant($"{i},{p.coord[0]:R},{p.coord[1]:R},{p.coord[2]:R},{results[0]},{results[1]},{results[2]},{results[3]},{status}"));
            if(results[0]!=results[1]||results[0]!=results[2]||results[0]!=results[3])
            {
                Console.WriteLine($"SAVED PROBE DIFF row={i} p=({p.coord[0]:R},{p.coord[1]:R},{p.coord[2]:R}) native/roundtrip/self/fresh={results[0]}/{results[1]}/{results[2]}/{results[3]} agreement={status} error={error} stage={KernelRuntime.LastRegularClassificationStage} entity={KernelRuntime.LastRegularClassificationEntity} sheet={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} status={KernelRuntime.LastSheetAlgorithmStatus} uv=({KernelRuntime.LastSheetRayU:R},{KernelRuntime.LastSheetRayV:R})");
                if(error!=0)Console.WriteLine($"SAVED PROBE STATION row={i} curve={KernelRuntime.LastSheetCurve} t={KernelRuntime.LastSheetParameter:R}");
                evidence.Add((p,i));
            }
        }
        // Finish every classification before range/projection diagnostics:
        // warming a PK projection cache can change its exact-threshold path.
        foreach(var item in evidence)
        {NativeProbeEvidence(reference,item.Point,$"row{item.Row}-native");NativeProbeEvidence(nativeReload,item.Point,$"row{item.Row}-native-roundtrip");NativeProbeEvidence(fresh,item.Point,$"row{item.Row}-self-fresh");}
    }
    finally{Check(PK_ENTITY_delete(1,&nativeReload),"delete saved native roundtrip");}
    Console.WriteLine($"SAVED PROBE SETS native_roundtrip=[{string.Join(',',nativeRoundtrip)}] gm_native=[{string.Join(',',gmNative)}] gm_fresh=[{string.Join(',',gmFresh)}] fresh_native=[{string.Join(',',freshNative)}]");
    Console.WriteLine($"SAVED PROBES total={rows.Length-1} different={failures} unresolved={pending} native_class_changed={changedNative} finite_distance_bounds=Unresolved");
    ParasolidProbeAgreement.UnresolvedCount+=pending;
    Require(failures==0,"fixed classification API error or Inside/Outside disagreement");
}

static unsafe void NativeProbeEvidence(int body,PK_VECTOR_t query,string label)
{
    PK_VECTOR_t* jets=stackalloc PK_VECTOR_t[4];
    foreach(var kind in new[]{PK_CLASS_face,PK_CLASS_edge,PK_CLASS_vertex})
    {
        int count;int* entities;var error=kind==PK_CLASS_face?PK_BODY_ask_faces(body,&count,&entities):kind==PK_CLASS_edge?PK_BODY_ask_edges(body,&count,&entities):PK_BODY_ask_vertices(body,&count,&entities);
        Check(error,"probe nearest entities");var closest=new PK_range_1_r_t{distance=double.PositiveInfinity};var found=0;var spatialDistance=double.PositiveInfinity;
        try
        {
            var options=new PK_TOPOL_range_vector_o_t{opt_level=PK_range_opt_accuracy_c};
            for(var i=0;i<count;i++)
            {
                PK_range_result_t result;PK_range_1_r_t range;error=PK_TOPOL_range_vector(entities[i],query,&options,&result,&range);
                if(error!=0||result!=PK_range_result_found_c)continue;
                var distance=Distance(query,range.end.vector);
                if(distance<spatialDistance){closest=range;found=entities[i];spatialDistance=distance;}
            }
        }
        finally{Check(PK_MEMORY_free(entities),"free probe nearest entities");}
        if(found==0){Console.WriteLine($"PROBE EVIDENCE {label} class={kind} unresolved");continue;}
        double precision=double.NaN;if(kind==PK_CLASS_edge)Check(PK_EDGE_ask_precision(found,&precision),"nearest edge precision");
        if(kind==PK_CLASS_vertex)Check(PK_VERTEX_ask_precision(found,&precision),"nearest vertex precision");
        var uv=new PK_UV_t();var normal=new PK_VECTOR_t();var signed=double.NaN;var inverseError=0;var evalError=0;
        if(kind==PK_CLASS_face)
        {
            int surface;PK_LOGICAL_t orientation;Check(PK_FACE_ask_oriented_surf(found,&surface,&orientation),"nearest oriented face");
            inverseError=PK_SURF_parameterise_vector(surface,closest.end.vector,&uv);
            if(inverseError==0)
            {
                evalError=PK_SURF_eval(surface,uv,1,1,PK_LOGICAL_false,jets);
                if(evalError==0)
                {
                    normal=Cross(jets[1],jets[2]);
                    var length=Math.Sqrt(normal.coord[0]*normal.coord[0]+normal.coord[1]*normal.coord[1]+normal.coord[2]*normal.coord[2]);
                    if(length>0){signed=0;for(var d=0;d<3;d++){normal.coord[d]*=(orientation==PK_LOGICAL_true?1:-1)/length;signed+=(query.coord[d]-closest.end.vector.coord[d])*normal.coord[d];}}
                }
            }
        }
        Console.WriteLine($"PROBE EVIDENCE {label} class={kind} entity={found} PK_distance={closest.distance:R} spatial_distance={spatialDistance:R} precision={precision:R} closest=({closest.end.vector.coord[0]:R},{closest.end.vector.coord[1]:R},{closest.end.vector.coord[2]:R}) uv=({uv.param[0]:R},{uv.param[1]:R}) face_normal=({normal.coord[0]:R},{normal.coord[1]:R},{normal.coord[2]:R}) signed={signed:R} inverse_error={inverseError} eval_error={evalError}");
    }
}

// Finite face loops define the solid. Each undirected pair is one shared edge.
static unsafe int Seed(double degrees,double floor,bool splitRemote,double phase)
{
    var theta=degrees*Math.PI/180;var t=Math.Tan(theta);var f=-2.5+4*t;var h=5-4*t;
    PK_VECTOR_t[] p=[new(-2.5,2.5,floor),new(2.5,2.5,floor),new(2.5,f,floor),new(-1.5,-2.5,floor),new(-2.5,-1.5,floor),
        new(-2.5,2.5,4),new(-1.5,2.5,5),new(2.5,2.5,h),new(2.5,f,h),new(-1.5,-2.5,5),new(-2.5,-1.5,4)];
    int[][] loops=[[0,1,2,3,4],[0,5,6,7,1],[1,7,8,2],[2,8,9,3],[6,9,8,7],[0,4,10,5],[5,10,9,6],[4,3,9,10]];
    if(splitRemote)
    {
        // Subdivide the common bottom/back line in both directed loops.
        // The rolling neighbourhood and the two selection occurrences are
        // identical; the bodies supplied to the two kernels have 18 edges.
        p=[..p,new(0,2.5,floor)];
        loops[0]=[0,11,1,2,3,4];loops[1]=[0,5,6,7,1,11];
    }
    var classes=new List<int>();var parents=new List<int>();var children=new List<int>();var senses=new List<int>();
    int Add(int type){var i=classes.Count;classes.Add(type);return i;}
    void Rel(int a,int b,int s=PK_TOPOL_sense_none_c){parents.Add(a);children.Add(b);senses.Add(s);}
    var root=Add(PK_CLASS_body);var outside=Add(PK_CLASS_region);var inside=Add(PK_CLASS_region);var outerShell=Add(PK_CLASS_shell);var innerShell=Add(PK_CLASS_shell);
    Rel(root,outside,PK_TOPOL_sense_negative_c);Rel(root,inside,PK_TOPOL_sense_positive_c);Rel(outside,outerShell);Rel(inside,innerShell);
    var vertices=p.Select(_=>Add(PK_CLASS_vertex)).ToArray();var faces=new int[8];
    var edgeMap=new Dictionary<(int,int),int>();var edgeEnds=new Dictionary<int,(int,int)>();
    for(var i=0;i<loops.Length;i++)
    {
        faces[i]=Add(PK_CLASS_face);var loop=Add(PK_CLASS_loop);Rel(innerShell,faces[i],PK_TOPOL_sense_negative_c);Rel(outerShell,faces[i],PK_TOPOL_sense_positive_c);Rel(faces[i],loop);
        for(var j=0;j<loops[i].Length;j++)
        {
            var a=loops[i][j];var b=loops[i][(j+1)%loops[i].Length];var key=(Math.Min(a,b),Math.Max(a,b));
            if(!edgeMap.TryGetValue(key,out var edge))
            {
                edge=Add(PK_CLASS_edge);edgeMap.Add(key,edge);edgeEnds.Add(edge,(a,b));Rel(edge,vertices[a]);Rel(edge,vertices[b]);
            }
            var fin=Add(PK_CLASS_fin);var direction=edgeEnds[edge]==(a,b)?PK_TOPOL_sense_positive_c:PK_TOPOL_sense_negative_c;
            Rel(loop,fin);Rel(fin,edge,direction);Rel(edge,fin,direction);
        }
    }
    var options=new PK_BODY_create_topology_2_o_t{body_type=PK_BODY_type_solid_c};PK_BODY_create_topology_2_r_t creation;
    Check(PK_BODY_create_topology_2(classes.Count,classes.ToArray(),parents.Count,parents.ToArray(),children.ToArray(),senses.ToArray(),&options,&creation),"create finite topology");
    try
    {
        for(var fault=0;fault<creation.n_create_faults;fault++)Console.WriteLine($"CREATE STATUS {creation.create_faults[fault].state}: "+string.Join(",",new ReadOnlySpan<int>(creation.create_faults[fault].indices,creation.create_faults[fault].n_indices).ToArray()));
        for(var fault=0;fault<creation.n_create_faults;fault++)Require(creation.create_faults[fault].state==PK_BODY_state_ok_c,"topology creation faults");
        for(var i=0;i<p.Length;i++){var sf=new PK_POINT_sf_t(p[i]);int point;Check(PK_POINT_create(&sf,&point),"point");var vertex=creation.topols[vertices[i]];Check(PK_VERTEX_attach_points(1,&vertex,&point),"attach point");}
        foreach(var (index,ends) in edgeEnds)
        {
            var (a,b)=ends;int curve;
            var key=(Math.Min(a,b),Math.Max(a,b));
            if(key==(3,4)||key==(5,6))
            {
                var center=key==(3,4)?new PK_VECTOR_t(-1.5,-1.5,floor):new PK_VECTOR_t(-1.5,2.5,4);
                var axis=key==(3,4)?new PK_VECTOR1_t(0,0,1):new PK_VECTOR1_t(0,1,0);
                var sf=new PK_CIRCLE_sf_t(new PK_AXIS2_sf_t(center,axis,new PK_VECTOR1_t(-1,0,0)),1);
                Check(PK_CIRCLE_create(&sf,&curve),"circle");
            }
            else if(key==(9,10))
            {
                var q=1/Math.Sqrt(2);var sf=new PK_ELLIPSE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(-1.5,-1.5,4),new PK_VECTOR1_t(0,-q,-q),new PK_VECTOR1_t(0,-q,q)),Math.Sqrt(2),1);
                Check(PK_ELLIPSE_create(&sf,&curve),"ellipse");
            }
            else
            {
                var d=Subtract(p[b],p[a]);var length=Math.Sqrt(Dot(d,d));
                var sf=new PK_LINE_sf_t(new PK_AXIS1_sf_t(p[a],new PK_VECTOR1_t(d.coord[0]/length,d.coord[1]/length,d.coord[2]/length)));
                Check(PK_LINE_create(&sf,&curve),"line");
            }
            var edge=creation.topols[index];
            var interval=new PK_INTERVAL_t(0,key is (3,4) or (5,6) or (9,10)?Math.PI/2:Distance(p[a],p[b]));
            var forward=key==(3,4)?a==4:key==(9,10)?a==9:true;var curveSense=forward?PK_LOGICAL_true:PK_LOGICAL_false;var intervalIndex=0;
            var attach=new PK_EDGE_attach_curves_o_t{n_intervals=1,intervals=&interval,interval_map=&intervalIndex,have_senses=PK_LOGICAL_true,senses=&curveSense};PK_ENTITY_track_r_t tracking;
            Check(PK_EDGE_attach_curves_2(1,&edge,&curve,&attach,&tracking),"attach curve");Check(PK_ENTITY_track_r_f(&tracking),"free attach tracking");
        }
        PK_VECTOR_t[] origins=[new(0,0,floor),new(0,2.5,0),new(2.5,0,0),new(-1.5,-2.5,0),new(-1.5,0,5),new(-2.5,0,0)];
        PK_VECTOR1_t[] normals=[new(0,0,-1),new(0,1,0),new(1,0,0),new(Math.Sin(theta),-Math.Cos(theta),0),new(Math.Sin(theta),0,Math.Cos(theta)),new(-1,0,0)];
        for(var i=0;i<8;i++)
        {
            int surface;
            if(i<6)
            {
                var normal=normals[i];var reference=Math.Abs(normal.coord[0])<.9?new PK_VECTOR1_t(1,0,0):new PK_VECTOR1_t(0,1,0);
                if(i==3||i==4)reference=new PK_VECTOR1_t(Math.Cos(theta),i==3?Math.Sin(theta):0,i==4?-Math.Sin(theta):0);
                var sf=new PK_PLANE_sf_t(new PK_AXIS2_sf_t(origins[i],normal,reference));Check(PK_PLANE_create(&sf,&surface),"plane");
            }
            else
            {
                var reference=i==6?new PK_VECTOR1_t(-Math.Cos(phase),0,Math.Sin(phase)):new PK_VECTOR1_t(-1,0,0);
                var sf=new PK_CYL_sf_t(new PK_AXIS2_sf_t(i==6?new PK_VECTOR_t(-1.5,0,4):new PK_VECTOR_t(-1.5,-1.5,0),i==6?new PK_VECTOR1_t(0,1,0):new PK_VECTOR1_t(0,0,1),reference),1);
                Check(PK_CYL_create(&sf,&surface),"cylinder");
            }
            var face=creation.topols[faces[i]];var sense=PK_LOGICAL_true;Check(PK_FACE_attach_surfs(1,&face,&surface,&sense),"attach surface");
        }
        return creation.body;
    }
    finally{Check(PK_BODY_create_topology_2_r_f(&creation),"free topology result");}
}

static unsafe void ProductEdges(int body,string label)
{
    int count;int* edges;Check(PK_BODY_ask_edges(body,&count,&edges),label+" edges");
    var points=stackalloc PK_VECTOR_t[2];
    try
    {
        for(var i=0;i<count;i++)
        {
            int curve,type;PK_INTERVAL_t range;PK_LOGICAL_t sense;
            Check(PK_EDGE_ask_geometry(edges[i],PK_LOGICAL_true,&curve,&type,points,&range,&sense),label+" edge geometry");
            Console.WriteLine($"PRODUCT EDGE {label} edge={edges[i]} curve={curve} class={type} sense={sense==PK_LOGICAL_true} t=[{range.value[0]:R},{range.value[1]:R}] start=({points[0].coord[0]:R},{points[0].coord[1]:R},{points[0].coord[2]:R}) end=({points[1].coord[0]:R},{points[1].coord[1]:R},{points[1].coord[2]:R})");
        }
    }
    finally{Free(false,edges);}
}

static unsafe void CompareFiniteIntersections(PK_BODY_t reference,PK_BODY_t actual,string label,double unitScale)
{
    var expected=IntersectionEdges(reference);var found=IntersectionEdges(actual);
    Require(expected.Count==found.Count,label+" finite ICurve count");
    var maxEndpoint=0.0;var maxLength=0.0;
    foreach(var edge in expected)
    {
        var match=-1;var reversed=false;
        for(var i=0;i<found.Count;i++)
        {
            var candidate=found[i];
            if(!Pair(edge.Start,edge.End,candidate.Start,candidate.End,1e-8))continue;
            Require(match<0,label+" ambiguous finite ICurve match");match=i;
            reversed=Distance(edge.Start,candidate.End)<Distance(edge.Start,candidate.Start);
        }
        Require(match>=0,label+$" unmatched finite ICurve edge={edge.Edge}");
        var other=found[match];found.RemoveAt(match);
        var endpoint=Math.Max(Distance(edge.Low,reversed?other.High:other.Low),Distance(edge.High,reversed?other.Low:other.High));
        var length=Math.Abs(edge.Length-other.Length);
        // Arc length has units of length; compare the nominal quadrature
        // estimates in the seed's units after an explicit similarity. The
        // oriented endpoint/model precision and strict body gates stay in
        // product coordinates.
        Require(endpoint<=1e-8,
            label+$" finite ICurve edge={edge.Edge}/{other.Edge} endpoint={endpoint:R} length={length:R} scale={unitScale:R}");
        if(length/unitScale>2e-8)
        {
            var proved=false;
            for(var pieces=2;pieces<=32;pieces*=2)
            {
                var a=ParasolidBodyCompare.FiniteLength(edge.Curve,edge.Range,pieces);
                var b=ParasolidBodyCompare.FiniteLength(other.Curve,other.Range,pieces);
                var bound=Math.BitIncrement(Math.Max(a.Upper-b.Lower,b.Upper-a.Lower));
                if(bound/unitScale>2e-8)continue;
                Console.WriteLine($"FINITE ICurve refined edge={edge.Edge}/{other.Edge} pieces={pieces} raw_delta={length:R} bounded_delta={bound:R}");
                length=bound;proved=true;break;
            }
            Require(proved,label+$" finite ICurve edge={edge.Edge}/{other.Edge} length={length:R} scale={unitScale:R}");
        }
        maxEndpoint=Math.Max(maxEndpoint,endpoint);maxLength=Math.Max(maxLength,length);
    }
    Console.WriteLine($"FINITE ICurve {label} count={expected.Count} endpoint_max={maxEndpoint:R} length_delta_max={maxLength:R} length_delta_seed_max={maxLength/unitScale:R}");

    static List<(PK_EDGE_t Edge,PK_CURVE_t Curve,PK_INTERVAL_t Range,PK_VECTOR_t Start,PK_VECTOR_t End,PK_VECTOR_t Low,PK_VECTOR_t High,double Length)> IntersectionEdges(PK_BODY_t body)
    {
        int count;int* edges;Check(PK_BODY_ask_edges(body,&count,&edges),"finite ICurve edges");
        var result=new List<(PK_EDGE_t,PK_CURVE_t,PK_INTERVAL_t,PK_VECTOR_t,PK_VECTOR_t,PK_VECTOR_t,PK_VECTOR_t,double)>();
        PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
        PK_VERTEX_t* vertices=stackalloc PK_VERTEX_t[2];
        try
        {
            for(var e=0;e<count;e++)
            {
                PK_CURVE_t curve;PK_CLASS_t kind;PK_INTERVAL_t range,used;PK_LOGICAL_t sense;
                Check(PK_EDGE_ask_geometry(edges[e],PK_LOGICAL_true,&curve,&kind,ends,&range,&sense),"finite ICurve geometry");
                if(kind!=PK_CLASS_icurve)continue;
                Require(double.IsFinite(range.value[0])&&double.IsFinite(range.value[1])&&range.value[1]>range.value[0],"increasing native ICurve limits");
                PK_VECTOR_t low,high;double length;
                Check(PK_CURVE_eval(curve,range.value[0],0,&low),"finite ICurve lower limit");
                Check(PK_CURVE_eval(curve,range.value[1],0,&high),"finite ICurve upper limit");
                Check(PK_CURVE_find_length(curve,range,&length,&used),"finite ICurve length");
                Check(PK_EDGE_ask_vertices(edges[e],vertices),"finite ICurve vertices");
                var start=vertices[0]!=0?NativeVertexPosition(vertices[0]):ends[0];
                var end=vertices[1]!=0?NativeVertexPosition(vertices[1]):ends[1];
                if(sense!=PK_LOGICAL_true)(low,high)=(high,low);
                var error=Math.Max(Distance(low,start),Distance(high,end));
                Require(length>0&&error<=1e-8,$"ICurve finite oriented endpoints edge={edges[e]} t=[{range.value[0]:R},{range.value[1]:R}] error={error:R}");
                result.Add((edges[e],curve,range,start,end,low,high,length));
            }
        }
        finally{Free(false,edges);}
        return result;

    }
}

static void PrintProfile(string operation,double apiMs)
{
    var p=ModelingPerformance.Last;var milliseconds=1000.0/Stopwatch.Frequency;
    Console.WriteLine($"PROFILE operation={operation} api-ms={apiMs:F3} elapsed-ms={p.ElapsedTicks*milliseconds:F3} pairs={p.SurfacePairs} ssi-cells={p.SsiCells} root-cells={p.RootCells} roots={p.Roots} budget-failures={p.BudgetFailures} winding={p.WindingCalls} bound-requests={p.BoundRequests} bound-evaluations={p.BoundEvaluations} cache-hits={p.BoundCacheHits} failure-cache-hits={p.BoundFailureCacheHits} bound-ms={p.BoundTicks*milliseconds:F3}");
    Console.WriteLine($"WORK jacobians={p.JacobianCalls} directions={p.JacobianDirections} residual-directions={p.ResidualDirections} record-builds={p.JacobianRecordBuilds} record-nodes={p.JacobianRecordNodes} record-rebinds={p.JacobianRecordRebinds} direct-spline-point-columns={p.DirectSplinePointColumns}");
    foreach(var use in Enum.GetValues<RootIsolationUse>())if(p.RootCalls[(int)use]!=0||p.RootCellsByUse[(int)use]!=0)Console.WriteLine($"ROOT_WORK use={use} calls={p.RootCalls[(int)use]} cells={p.RootCellsByUse[(int)use]} bound-failures={p.RootBoundFailures[(int)use]} exclusions={p.RootLevelExclusions[(int)use]} nonmonotone={p.RootNonmonotone[(int)use]}");
    foreach(var stage in Enum.GetValues<KernelRuntime.RegularBooleanStage>())if(p.Stages[(int)stage]!=0)Console.WriteLine($"PROFILE stage={stage} ms={p.Stages[(int)stage]*milliseconds:F3}");
}
static unsafe int Fillet(bool own,int body,double angle,double radius,double tolerance,bool reverse,bool procedural,M.PK_TRANSF_sf_s* input,double unitScale,bool splitSelected,bool splitEllipse,double splitFraction,int rotateMarks)
{
    int n;int* edges;Check(own?KernelRuntime.BodyAskEdges(body,&n,&edges):PK_BODY_ask_edges(body,&n,&edges),"edges");
    var split=splitEllipse?0:splitSelected?1:-1;var selected=new int[split>=0?3:2];PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];var midpoint=double.NaN;
    try
    {
        var t=Math.Tan(angle*Math.PI/180);var j=new PK_VECTOR_t(-1.5,-2.5,5);var k=new PK_VECTOR_t(-2.5,-1.5,4);var i=new PK_VECTOR_t(2.5,-2.5+4*t,5-4*t);
        if(input!=null){j=MapPoint(input,j,false);k=MapPoint(input,k,false);i=MapPoint(input,i,false);}
        for(var e=0;e<n;e++)
        {
            int curve,type;PK_INTERVAL_t range;PK_LOGICAL_t sense;
            if(own)
            {
                var edge=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(edges[e]).Slot];curve=edge.CurveTag;
                var record=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(curve)];
                type=record.Class==CurveClass.Ellipse?PK_CLASS_ellipse:record.Class==CurveClass.Line?PK_CLASS_line:0;
                range=new(edge.ParameterStart,edge.ParameterEnd);sense=PK_LOGICAL_true;
                for(var endpoint=0;endpoint<2;endpoint++)
                {
                    var vertex=endpoint==0?edge.StartVertex:edge.EndVertex;
                    var point=new M.PK_POINT_sf_s();Check(KernelRuntime.PointAsk(KernelRuntime.Vertices[vertex].PointTag,&point),"selection endpoint");
                    ends[endpoint]=*(PK_VECTOR_t*)&point.position;
                }
                if(edge.HasParameterRange==0)
                {
                    double first,last;Check(KernelRuntime.CurveParameteriseVector(curve,*(M.PK_VECTOR_s*)ends,&first),"finite selection start");
                    Check(KernelRuntime.CurveParameteriseVector(curve,*(M.PK_VECTOR_s*)(ends+1),&last),"finite selection end");
                    range=new(Math.Min(first,last),Math.Max(first,last));
                }
            }
            else Check(PK_EDGE_ask_geometry(edges[e],PK_LOGICAL_true,&curve,&type,ends,&range,&sense),"edge geometry");
            for(var s=0;s<2;s++)if(type==(s==0?PK_CLASS_ellipse:PK_CLASS_line)&&Pair(ends[0],ends[1],j,s==0?k:i,1e-8*unitScale))
            {
                PK_VECTOR_t middle;var u=(range.value[0]+range.value[1])*.5;
                Check(own?KernelRuntime.CurveEval(curve,u,0,(M.PK_VECTOR_s*)&middle):PK_CURVE_eval(curve,u,0,&middle),"selection midpoint");
                if(input!=null)middle=MapPoint(input,middle,true);
                Require(s==0?Math.Abs(middle.coord[1]+middle.coord[2]-2.5)<1e-8:Math.Abs(middle.coord[1]+2.5-t*(middle.coord[0]+1.5))<1e-8,"selection midpoint geometry");
                Require(selected[s]==0,"unique edge selection");selected[s]=edges[e];
                if(s==split)midpoint=range.value[0]+(range.value[1]-range.value[0])*splitFraction;
            }
        }
    }
    finally{Free(own,edges);}
    Require(selected[0]!=0&&selected[1]!=0,"both selected edges found");
    if(split>=0)
    {
        PK_VERTEX_t vertex;PK_EDGE_t fragment;
        Check(own?KernelRuntime.EdgeSplitAtParam(selected[split],midpoint,&vertex,&fragment):PK_EDGE_split_at_param(selected[split],midpoint,&vertex,&fragment),"split selected native carrier");
        selected[2]=fragment;
        Console.WriteLine($"SELECTED SPLIT {(own?"self":"PK")} edge={selected[split]} fragment={fragment} vertex={vertex} t={midpoint:R}");
    }
    if(reverse)Array.Reverse(selected);
    for(var rotation=0;rotation<rotateMarks;rotation++)(selected[0],selected[1],selected[2])=(selected[1],selected[2],selected[0]);
    if(rotateMarks!=0)Console.WriteLine($"MARK ORDER {(own?"self":"PK")}: {string.Join(',',selected)}");
    var mark=new PK_EDGE_set_blend_constant_o_t();mark.properties.tolerance=1e-7*unitScale;int count;int* marked;
    fixed(int* selection=selected)Check(own?KernelRuntime.EdgeSetBlendConstant(selected.Length,selection,radius,(M.PK_EDGE_set_blend_constant_o_s*)&mark,&count,&marked):PK_EDGE_set_blend_constant(selected.Length,selection,radius,&mark,&count,&marked),"set blend");Free(own,marked);
    var fix=new PK_BODY_fix_blends_o_t{local_check=PK_LOGICAL_true,tolerance=tolerance};if(procedural)fix.update=PK_blend_edge_update_v261_c;
    WebProgressCapture.Context="";
    WebProgressCapture.Before(own,body,selected);
    WebProgressCapture.BlendParameters(own,fix,radius,mark.properties.tolerance);
    int* made;int fault,edgeFault,topolFault;var started=Stopwatch.GetTimestamp();
    PK_FACE_array_t* webUnders=null;int* webTopols=null;
    var error=own?KernelRuntime.BodyFixBlends(body,(M.PK_BODY_fix_blends_o_s*)&fix,&count,&made,WebProgressCapture.Enabled?(M.PK_FACE_array_s**)&webUnders:null,WebProgressCapture.Enabled?&webTopols:null,&fault,&edgeFault,&topolFault):PK_BODY_fix_blends(body,&fix,&count,&made,WebProgressCapture.Enabled?&webUnders:null,WebProgressCapture.Enabled?&webTopols:null,&fault,&edgeFault,&topolFault);
    var webElapsed=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    WebProgressCapture.After(own,body,webElapsed,error,count,made,fault,webUnders,webTopols);
    WebProgressCapture.FreeBlendTracking(own,count,webUnders,webTopols);
    Console.WriteLine($"FIX {(own?"self":"PK")} error={error} faces={count} fault={fault} edge={edgeFault} topol={topolFault} stage={KernelRuntime.LastRegularBlendStage} network={KernelRuntime.LastRollingNetworkStage} SSI={KernelRuntime.LastGeneralIntersectionStage}/{KernelRuntime.LastGeneralIntersectionStatus}/{KernelRuntime.LastGeneralIntersectionNodes}/{KernelRuntime.LastGeneralIntersectionSegments} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}");
    if(own&&ModelingPerformance.Enabled)PrintProfile("FixBlends",webElapsed);
    if(own&&error!=0&&KernelRuntime.ForbidLegacyRegularBlendAssembly)
    {
        Console.WriteLine($"ROLLING FAILURE faces={KernelRuntime.LastRollingFirstFace}/{KernelRuntime.LastRollingSecondFace} sheet={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} t={KernelRuntime.LastSheetParameter:R} status={KernelRuntime.LastSheetAlgorithmStatus} uv=({KernelRuntime.LastSheetRayU:R},{KernelRuntime.LastSheetRayV:R})");
        if(KernelRuntime.LastGeneralIntersectionStage==KernelRuntime.GeneralIntersectionStage.Limits)
        {var a=KernelRuntime.LastGeneralLimitFirst;var b=KernelRuntime.LastGeneralLimitLast;Console.WriteLine($"SSI LIMIT first=({a.U:R},{a.V:R},{a.S:R},{a.T:R}) last=({b.U:R},{b.V:R},{b.S:R},{b.T:R})");}
        var cell=ParametricSurfaceIntersection.LastUnresolvedCell;
        Console.WriteLine($"UNRESOLVED reason={ParametricSurfaceIntersection.LastGraphFailure} depth={cell.Depth} graph_axis={ParametricSurfaceIntersection.LastGraphAxis} box=[{cell.Lower.U:R},{cell.Upper.U:R}]x[{cell.Lower.V:R},{cell.Upper.V:R}]x[{cell.Lower.S:R},{cell.Upper.S:R}]x[{cell.Lower.T:R},{cell.Upper.T:R}]");
        var box=SurfacePlaneIntersection.LastUnresolvedBox;
        Console.WriteLine($"UNRESOLVED scalar budget={SurfacePlaneIntersection.LastUnresolvedBudget} depth={SurfacePlaneIntersection.LastUnresolvedDepth} free={SurfacePlaneIntersection.LastUnresolvedFreeAxis} box=[{box.U0:R},{box.U1:R}]x[{box.V0:R},{box.V1:R}] boundary={SurfacePlaneIntersection.LastBoundaryFailure} residual=[{SurfacePlaneIntersection.LastBoundaryResidual.Lower:R},{SurfacePlaneIntersection.LastBoundaryResidual.Upper:R}]");
        Console.WriteLine($"UNRESOLVED iso reason={SurfaceIsoPlaneRoots.LastFailure} requested=[{SurfaceIsoPlaneRoots.LastRequestedInterval.Lower:R},{SurfaceIsoPlaneRoots.LastRequestedInterval.Upper:R}] cell=[{SurfaceIsoPlaneRoots.LastUnresolvedInterval.Lower:R},{SurfaceIsoPlaneRoots.LastUnresolvedInterval.Upper:R}] f0=[{SurfaceIsoPlaneRoots.LastLeft.Lower:R},{SurfaceIsoPlaneRoots.LastLeft.Upper:R}] f1=[{SurfaceIsoPlaneRoots.LastRight.Lower:R},{SurfaceIsoPlaneRoots.LastRight.Upper:R}] slope=[{SurfaceIsoPlaneRoots.LastDerivative.Lower:R},{SurfaceIsoPlaneRoots.LastDerivative.Upper:R}]");
        var rootCell=ParametricSurfaceIntersection.LastRootCell;
        Console.WriteLine($"UNRESOLVED isolated reason={ParametricSurfaceIntersection.LastRootFailure} fixed={ParametricSurfaceIntersection.LastRootFixedAxis} depth={rootCell.Depth} box=[{rootCell.Lower.U:R},{rootCell.Upper.U:R}]x[{rootCell.Lower.V:R},{rootCell.Upper.V:R}]x[{rootCell.Lower.S:R},{rootCell.Upper.S:R}]x[{rootCell.Lower.T:R},{rootCell.Upper.T:R}]");
        var conflict=KernelRuntime.LastBoundaryParameterConflict;
        Console.WriteLine($"ASSOCIATION conflict event={conflict.Event} carrier={conflict.Carrier} previous={conflict.Previous:R}[{conflict.PreviousLower:R},{conflict.PreviousUpper:R}] incoming={conflict.Incoming:R}[{conflict.Lower:R},{conflict.Upper:R}]");
    }
    Free(own,made);return error;
}

static unsafe byte[] Transmit(bool own,int body)
{
    if(own)SessionMemoryOwner.DropBodyXt(KernelRuntime.ResolveTagRecord(body).Slot);
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(own?KernelRuntime.PartTransmitB(1,&body,(M.PK_PART_transmit_o_s*)&options,(M.PK_MEMORY_block_s*)&block):PK_PART_transmit_b(1,&body,&options,&block),"transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(own?KernelRuntime.MemoryBlockFree((M.PK_MEMORY_block_s*)&block):PK_MEMORY_block_f(&block),"free XT");}
}
static unsafe int Receive(bool own,byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&options,&n,&parts):PK_PART_receive_b(block,&options,&n,&parts),"receive");
        try{Require(n==1,"one received part");return parts[0];}finally{Free(own,parts);}
    }
}
static unsafe void CheckBody(int body,string label)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&n,&faults),"check body");
    try
    {
        for(var i=0;i<n;i++)
        {
            int entityClass;Check(PK_ENTITY_ask_class(faults[i].entity_1,&entityClass),"fault class");
            Console.WriteLine($"FAULT {label} state={faults[i].state} entities={faults[i].entity_1}/{faults[i].entity_2} class={entityClass}");
            if(entityClass==PK_CLASS_face){int surface,type;Check(PK_FACE_ask_surf(faults[i].entity_1,&surface),"fault support");Check(PK_ENTITY_ask_class(surface,&type),"fault support class");Console.WriteLine($"FAULT support={surface} type={type}");}
        }
        WebProgressCapture.RecordBodyCheck(label,body,n,faults);
        Require(n==0,label+" faults="+n);Console.WriteLine(label+" full check: 0 faults");
    }
    finally{Free(false,faults);}
}
static unsafe int CheckNativeBlendFinBoxes(int body,string label)
{
    int n;int* faces;Check(PK_BODY_ask_faces(body,&n,&faces),label+" faces");var boxes=0;
    try
    {
        for(var i=0;i<n;i++)
        {
            int surface,kind;Check(PK_FACE_ask_surf(faces[i],&surface),label+" support");Check(PK_ENTITY_ask_class(surface,&kind),label+" support class");
            if(kind!=PK_CLASS_blendsf)continue;
            int count;int* loops;Check(PK_FACE_ask_loops(faces[i],&count,&loops),label+" loops");
            try
            {
                for(var l=0;l<count;l++)
                {
                    int size;int* fins;Check(PK_LOOP_ask_fins(loops[l],&size,&fins),label+" fins");
                    try
                    {
                        for(var f=0;f<size;f++)
                        {
                            int edge,curve;Check(PK_FIN_ask_edge(fins[f],&edge),label+" fin edge");if(edge==0)continue;
                            Check(PK_EDGE_ask_curve(edge,&curve),label+" edge curve");if(curve==0)continue;
                            Check(PK_ENTITY_ask_class(curve,&kind),label+" fin curve class");if(kind!=PK_CLASS_icurve)continue;
                            PK_UVBOX_t box;Check(PK_FIN_find_uvbox(fins[f],&box),$"{label} face {faces[i]} fin {fins[f]} UV box");
                            Require(double.IsFinite(box.param[0])&&double.IsFinite(box.param[1])&&double.IsFinite(box.param[2])&&double.IsFinite(box.param[3])
                                &&box.param[0]<=box.param[2]&&box.param[1]<=box.param[3],label+" finite fin UV box");
                            boxes++;
                        }
                    }
                    finally{Free(false,fins);}
                }
            }
            finally{Free(false,loops);}
        }
    }
    finally{Free(false,faces);}
    Console.WriteLine($"NATIVE FIN UV BOXES {label} count={boxes}");return boxes;
}
static void CompareNativeBlendFinBoxes(int reference,int received,string label,bool required=false)
{
    var expected=CheckNativeBlendFinBoxes(reference,label+" reference");var actual=CheckNativeBlendFinBoxes(received,label+" fresh self");
    Require((!required||expected>0)&&actual==expected,$"{label} native fin UV boxes {actual}/{expected}");
}
static unsafe void Counts(int body,int faces,int edges,int vertices)
{
    for(var kind=0;kind<5;kind++)
    {
        int n;int* tags;Check(kind switch{0=>PK_BODY_ask_regions(body,&n,&tags),1=>PK_BODY_ask_shells(body,&n,&tags),2=>PK_BODY_ask_faces(body,&n,&tags),3=>PK_BODY_ask_edges(body,&n,&tags),_=>PK_BODY_ask_vertices(body,&n,&tags)},"count");
        Free(false,tags);Require(n==(kind<2?2:kind==2?faces:kind==3?edges:vertices),"topology count "+kind+"="+n);
    }
}
static unsafe void Describe(int body)
{
    int n;int* edges;Check(PK_BODY_ask_edges(body,&n,&edges),"describe edges");var lengths=new List<double>();PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
    try{for(var e=0;e<n;e++){int curve,type;PK_INTERVAL_t range,lengthRange;PK_LOGICAL_t sense;double length,precision;Check(PK_EDGE_ask_geometry(edges[e],PK_LOGICAL_true,&curve,&type,ends,&range,&sense),"edge geometry");Check(PK_CURVE_find_length(curve,range,&length,&lengthRange),"edge arc length");Check(PK_EDGE_ask_precision(edges[e],&precision),"edge precision");lengths.Add(length);Console.WriteLine($"EDGE tag={edges[e]} class={type} length={length:R} precision={precision:R} range=[{range.value[0]:R},{range.value[1]:R}]");}}finally{Free(false,edges);}
    Console.WriteLine("SHORTEST "+string.Join(", ",lengths.Order().Take(4).Select(x=>x.ToString("R"))));
}
static void DescribeXt(byte[] bytes)
{
    var doc=XtText.DecodeDocument(System.Text.Encoding.ASCII.GetString(bytes));
    foreach(var node in doc.Nodes.Where(x=>doc.Schema.GetNode(x.Type).Name is "BLENDED_EDGE" or "LIMIT" or "ELLIPSE"))
        Console.WriteLine($"XT {doc.Schema.GetNode(node.Type).Name} #{node.Index} "+string.Join(" ",node.Fields.Select((x,i)=>$"[{i}]={x.Kind}:"+(x.Kind==XtFieldKind.Vector?$"({x.Vector.X:R},{x.Vector.Y:R},{x.Vector.Z:R})":x.Kind==XtFieldKind.Pointer?x.Pointer.ToString():x.Kind==XtFieldKind.Real?x.Real.ToString("R"):x.Kind==XtFieldKind.Character?x.Character.ToString():x.Integer.ToString()))));
}
static unsafe PK_VECTOR_t Subtract(PK_VECTOR_t a,PK_VECTOR_t b)=>new(a.coord[0]-b.coord[0],a.coord[1]-b.coord[1],a.coord[2]-b.coord[2]);
static unsafe PK_VECTOR_t Cross(PK_VECTOR_t a,PK_VECTOR_t b)=>new(a.coord[1]*b.coord[2]-a.coord[2]*b.coord[1],a.coord[2]*b.coord[0]-a.coord[0]*b.coord[2],a.coord[0]*b.coord[1]-a.coord[1]*b.coord[0]);
static unsafe double Dot(PK_VECTOR_t a,PK_VECTOR_t b)=>a.coord[0]*b.coord[0]+a.coord[1]*b.coord[1]+a.coord[2]*b.coord[2];
static double Distance(PK_VECTOR_t a,PK_VECTOR_t b){var d=Subtract(a,b);return Math.Sqrt(Dot(d,d));}
static bool Pair(PK_VECTOR_t a,PK_VECTOR_t b,PK_VECTOR_t c,PK_VECTOR_t d,double tolerance=1e-8)=>Distance(a,c)<tolerance&&Distance(b,d)<tolerance||Distance(a,d)<tolerance&&Distance(b,c)<tolerance;
static unsafe void Free(bool own,void* p){if(p!=null)Check(own?KernelRuntime.MemoryFree(p):PK_MEMORY_free(p),"free");}
static void Check(int error,string label){if(error!=0)throw new Exception(label+": error="+error);}
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static string FixtureText(byte[] bytes)=>string.Join('\n',System.Text.Encoding.ASCII.GetString(bytes).Split('\n').Select(line=>line.TrimEnd()));
static string ScriptPath([CallerFilePath]string path="")=>path;

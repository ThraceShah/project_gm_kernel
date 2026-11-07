#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using XtFieldKind=ProjectGmKernel.Xt.XtFieldKind;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
KernelRuntime.ForbidLegacyRegularBlendAssembly=args.Contains("--event-arrangement",StringComparer.Ordinal);
if(KernelRuntime.ForbidLegacyRegularBlendAssembly)
    KernelRuntime.TraceRollingNetwork=trace=>
    {
        if(trace.Stage is KernelRuntime.RollingNetworkStage.Select or KernelRuntime.RollingNetworkStage.Build)
        {Console.WriteLine($"ROLLING CELL {trace.Stage} cell={trace.Cell} source={trace.CellSource} surface={trace.Surface} material={trace.Material}");return;}
        Console.WriteLine($"ROLLING {trace.Stage} source={trace.Source} supports={trace.FirstSupport}/{trace.SecondSupport} surface={trace.Surface} other={trace.Other} curve={trace.Curve} relation={trace.Relation} vertex={trace.Vertex} t=[{trace.First:R},{trace.Last:R}]");
        if(args.Contains("--diagnostics")&&trace.Stage==KernelRuntime.RollingNetworkStage.BandEvents&&trace.Curve==0)ScalarBoundaryEvidence(trace.Surface,trace.Other);
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
        var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(trace.Surface)];
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.Successor)
        {Console.WriteLine($"SUCCESSOR surface={trace.Surface} edge={trace.Edge} forward={trace.Forward} end={trace.EndEvent} next={trace.NextEdge} angles={trace.StartAngle:R}/{trace.EndAngle:R}");return;}
        Console.WriteLine($"BOUNDARY {trace.Stage} source={trace.Source} face={trace.Face} product={trace.Product} surface={trace.Surface}/{surface.Class} edge={trace.Edge} carrier={trace.Curve}/{trace.Generation}/{KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(trace.Curve)].Class} product_curve={trace.ProductCurve} t=[{trace.First:R},{trace.Last:R}] events={trace.StartEvent}->{trace.EndEvent} forward={trace.Forward}");
        if(trace.Stage==KernelRuntime.BoundaryTraceStage.Input&&surface.Class==SurfaceClass.BlendSurface)
        {
            var blend=KernelRuntime.BlendedEdgeDataPool[surface.DataIndex];var spine=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(blend.SpineTag)];
            Console.WriteLine($"BOUNDARY SUPPORT blend={trace.Surface} uv=[{surface.UMin:R},{surface.UMax:R}]x[{surface.VMin:R},{surface.VMax:R}] spine={blend.SpineTag}/{spine.Class} natural=[{spine.TMin:R},{spine.TMax:R}] supports={blend.Surface0Tag}/{blend.Surface1Tag} radii={blend.Range0:R}/{blend.Range1:R}");
        }
    };

var output=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","short-edge-blend-oracle");
M.PK_TRANSF_sf_s inputTransform;double inputScale;bool mappedInput;
unsafe{inputTransform=InputTransform(args,out inputScale,out mappedInput);}
Directory.CreateDirectory(output);
Require(!args.Contains("--record-fixtures")||!args.Contains("--matrix")&&!args.Contains("--both-updates"),"record fixtures uses the single default seed");
Require(!args.Contains("--saved-probes")||!args.Contains("--matrix")&&!args.Contains("--both-updates")&&!args.Contains("--native-only"),"saved probes replay the default seed and exact saved coordinates");
Require(!args.Contains("--native-only")||!args.Any(x=>x is "--import-only" or "--probes" or "--geometry" or "--lifecycle"),"managed checks require a managed-import mode");
Require(!mappedInput||!args.Contains("--record-fixtures")&&!args.Contains("--saved-probes"),"transformed inputs cannot replace fixed fixtures or historical probe inputs");
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
        foreach(var angle in matrix?new[]{.25,.5,1,2}:new[]{NumberOption(args,"--angle=",1)})
        foreach(var radius in matrix?new[]{.25,.5,.6}:new[]{NumberOption(args,"--radius=",.5)})
        foreach(var tolerance in matrix?new[]{1e-7,1e-5}:new[]{NumberOption(args,"--tolerance=",1e-7)})
        foreach(var reverse in matrix?new[]{false,true}:new[]{args.Contains("--reverse-selection")})
        foreach(var procedural in args.Contains("--both-updates")?new[]{false,true}:new[]{false})
        {
            var name=$"a{angle:R}-r{radius:R}-t{tolerance:R}-reverse{reverse}-"+(procedural?"v261":"default");
            if(mappedInput)name+=$"-input-{(args.Contains("--input-mirror")?"mirror":"rigid")}-s{inputScale:R}";
            cases++;
            Console.WriteLine("CASE "+name);
            try
            {
                var reference=Seed(angle);CheckBody(reference,"generated input");Counts(reference,8,17,11);
                var bytes=Transmit(false,reference);File.WriteAllBytes(Path.Combine(output,name+"-input.x_t"),bytes);
                var own=Receive(true,bytes);
                if(mappedInput){ApplyInput(true,own,inputTransform);ApplyInput(false,reference,inputTransform);}
                Require(KernelRuntime.TryResolveBodySlot(own,out var body)&&!KernelRuntime.Xt.IsOpaque(body),"editable input");
                var fresh=Transmit(true,own);File.WriteAllBytes(Path.Combine(output,name+"-input-fresh.x_t"),fresh);
                var freshBody=Receive(false,fresh);CheckBody(freshBody,"fresh input");ParasolidBodyCompare.AssertEquivalent(reference,freshBody,"fresh input");
                Check(PK_ENTITY_delete(1,&freshBody),"delete input roundtrip");
                var nativeError=Fillet(false,reference,angle,radius*inputScale,tolerance*inputScale,reverse,procedural,mappedInput?&inputTransform:null,inputScale);
                Check(nativeError,"native fix");CheckBody(reference,"native fillet");
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
                    ParasolidBodyCompare.AssertEquivalent(reference,reloaded,"native via managed");
                    Mass(reference,reloaded,"native via managed");
                    if(args.Contains("--geometry"))Geometry(imported,reference);
                    if(args.Contains("--probes"))Probes(reference,imported,reloaded,result,output,name);
                    if(args.Contains("--saved-probes"))SavedProbes(reference,imported,reloaded,result,args.Contains("--diagnostics"));
                    if(args.Contains("--lifecycle"))failures+=Lifecycle(imported,reference,output,name+"-imported-product",mappedInput?&inputTransform:null);
                    Check(PK_ENTITY_delete(1,&reloaded),"delete native roundtrip");Check(KernelRuntime.EntityDelete(1,&imported),"delete managed import");
                }
                catch(Exception ex){failures++;Console.WriteLine("FAIL native editable roundtrip: "+ex.Message);}
                if(!args.Contains("--native-only")&&!args.Contains("--import-only"))
                {
                    Check(Fillet(true,own,angle,radius*inputScale,tolerance*inputScale,reverse,procedural,mappedInput?&inputTransform:null,inputScale),"managed fix");
                    var ownResult=Transmit(true,own);File.WriteAllBytes(Path.Combine(output,name+"-self.x_t"),ownResult);
                    var received=Receive(false,ownResult);CheckBody(received,"managed fillet");
                    if(args.Contains("--diagnostics")){ProductEdges(reference,"reference");ProductEdges(received,"self-constructed-product");}
                    ParasolidBodyCompare.AssertEquivalent(reference,received,"independent fillet");
                    if(args.Contains("--geometry"))SelfGeometry(own,reference);
                    try{Mass(reference,received,"self-constructed-product");}
                    catch(Exception ex){failures++;Console.WriteLine("FAIL self-constructed-product mass: "+ex.Message);}
                    if(args.Contains("--lifecycle"))failures+=Lifecycle(own,reference,output,name+"-self-constructed-product",mappedInput?&inputTransform:null);
                    Check(PK_ENTITY_delete(1,&received),"delete managed roundtrip");
                }
                Check(KernelRuntime.EntityDelete(1,&own),"delete managed seed");Check(PK_ENTITY_delete(1,&reference),"delete reference");
            }
            catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+": "+ex.Message);}
        }
    }
    finally{Check(KernelRuntime.SessionStop(),"managed stop");}
    Require(failures==0,$"{failures} short-edge acceptance checks failed");
    Console.WriteLine($"PASS cases={cases} mode={(args.Contains("--native-only")?"native-only":args.Contains("--import-only")?"import-only":"independent-construction")}");
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

static unsafe int Lifecycle(int imported,int reference,string output,string name,M.PK_TRANSF_sf_s* input=null)
{
    int own,native;Check(KernelRuntime.EntityCopy(imported,&own),"copy imported fillet");Check(PK_ENTITY_copy(reference,&native),"copy PK fillet");
    var matrix=new M.PK_TRANSF_sf_s();var a=.37;var s=1.7;
    matrix.matrix[0]=-s*Math.Cos(a);matrix.matrix[1]=-s*Math.Sin(a);matrix.matrix[4]=-s*Math.Sin(a);matrix.matrix[5]=s*Math.Cos(a);matrix.matrix[10]=s;matrix.matrix[15]=1;
    matrix.matrix[3]=3;matrix.matrix[7]=5;matrix.matrix[11]=7;
    int selfTransform,pkTransform;Check(KernelRuntime.TransfCreate(&matrix,&selfTransform),"self transform");Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&matrix,&pkTransform),"PK transform");
    var failures=0;
    try
    {
        Move(true,own,selfTransform);Move(false,native,pkTransform);
        failures+=CompareFresh(own,native,output,name+"-copy-reflected");
        var toolAxis=new PK_AXIS2_sf_t(new PK_VECTOR_t(3,0,-1),new PK_VECTOR1_t(0,0,1),new PK_VECTOR1_t(1,0,0));int ownTool,pkTool;
        Check(KernelRuntime.BodyCreateSolidBlock(2,10,10,(M.PK_AXIS2_sf_s*)&toolAxis,&ownTool),"self cutter");Check(PK_BODY_create_solid_block(2,10,10,&toolAxis,&pkTool),"PK cutter");
        if(input!=null){ApplyInput(true,ownTool,*input);ApplyInput(false,pkTool,*input);}
        Move(true,ownTool,selfTransform);Move(false,pkTool,pkTransform);
        own=Cut(true,own,ownTool);native=Cut(false,native,pkTool);failures+=CompareFresh(own,native,output,name+"-copy-reflected-cut");
    }
    finally
    {
        Check(KernelRuntime.EntityDelete(1,&own),"delete own lifecycle");Check(PK_ENTITY_delete(1,&native),"delete PK lifecycle");
        Check(KernelRuntime.EntityDelete(1,&selfTransform),"delete self transform");Check(PK_ENTITY_delete(1,&pkTransform),"delete PK transform");
    }
    return failures;
}
static unsafe void Move(bool own,int body,int transform)
{
    int n;int* replaced;PK_LOGICAL_t* exact;
    Check(own?KernelRuntime.BodyTransform(body,transform,0,&n,&replaced,(byte**)&exact):PK_BODY_transform(body,transform,0,&n,&replaced,&exact),"body transform");Free(own,replaced);Free(own,exact);
}
static unsafe int Cut(bool own,int body,int tool)
{
    var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};int n;int* bodies;
    var started=Stopwatch.GetTimestamp();var error=own?KernelRuntime.BodyBoolean(body,1,&tool,(M.PK_BODY_boolean_o_s*)&options,&n,&bodies):PK_BODY_boolean(body,1,&tool,&options,&n,&bodies);
    Console.WriteLine($"TIME {(own?"self":"PK")} Boolean error={error} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3} stage={KernelRuntime.LastRegularBooleanStage} face={KernelRuntime.LastRegularBooleanFace} other={KernelRuntime.LastRegularBooleanOtherFace} sheet={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} t={KernelRuntime.LastSheetParameter:R} status={KernelRuntime.LastSheetAlgorithmStatus} uv=({KernelRuntime.LastSheetRayU:R},{KernelRuntime.LastSheetRayV:R})");Check(error,"cut fillet product");
    try{Require(n==1,"one cut result");return bodies[0];}finally{Free(own,bodies);}
}
static unsafe int CompareFresh(int own,int reference,string directory,string name)
{
    var fresh=Transmit(true,own);File.WriteAllBytes(Path.Combine(directory,name+".x_t"),fresh);var received=Receive(false,fresh);
    var referenceBytes=Transmit(false,reference);File.WriteAllBytes(Path.Combine(directory,name+"-native.x_t"),referenceBytes);
    var referenceReload=Receive(false,referenceBytes);
    try
    {
        CheckBody(reference,name+" native");
        CheckBody(referenceReload,name+" native own roundtrip");
        try{ParasolidBodyCompare.AssertEquivalent(reference,received,name);}catch(Exception ex){Console.WriteLine("COMPARE DIAGNOSTIC "+ex.Message);throw;}
        CheckBody(received,name);
        try{Mass(reference,received,name);return 0;}
        catch(Exception ex){Console.WriteLine("FAIL "+name+" mass: "+ex.Message);return 1;}
    }
    finally{Check(PK_ENTITY_delete(1,&received),"delete lifecycle reload");Check(PK_ENTITY_delete(1,&referenceReload),"delete native lifecycle reload");}
}
static unsafe void Mass(int reference,int actual,string label)
{
    var options=new PK_TOPOL_eval_mass_props_o_t();double* a=stackalloc double[15];double* b=stackalloc double[15];
    var error=double.PositiveInfinity;
    foreach(var accuracy in new[]{.99999999,.99999999999})
    {
        Check(PK_TOPOL_eval_mass_props(1,&reference,accuracy,&options,a,a+1,a+2,a+5,a+14),"reference mass");
        Check(PK_TOPOL_eval_mass_props(1,&actual,accuracy,&options,b,b+1,b+2,b+5,b+14),"actual mass");
        error=0;for(var i=0;i<15;i++)error=Math.Max(error,Math.Abs(a[i]-b[i])/(1+Math.Abs(a[i])));
        Console.WriteLine($"MASS {label} accuracy={accuracy:R} volume={a[0]:R}/{b[0]:R} area={a[14]:R}/{b[14]:R} normalized15={error:R}");
        if(error<1e-7)break;
    }
    Require(error<1e-7,"mass mismatch");
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
    int count;int* nativeFaces;Check(PK_BODY_ask_faces(reference,&count,&nativeFaces),"self geometry native faces");
    var body=KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(own).Slot];var face=body.FirstFaceBody;var checkedFaces=0;
    PK_VECTOR_t* native=stackalloc PK_VECTOR_t[9];PK_VECTOR_t* actual=stackalloc PK_VECTOR_t[9];
    PK_VECTOR_t* plus=stackalloc PK_VECTOR_t[4];PK_VECTOR_t* minus=stackalloc PK_VECTOR_t[4];
    PK_VECTOR_t* finePlus=stackalloc PK_VECTOR_t[4];PK_VECTOR_t* fineMinus=stackalloc PK_VECTOR_t[4];
    var checkpoint=KernelRuntime.EnsureCommandScratch();
    try
    {
        for(var f=0;f<body.FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var source=KernelRuntime.Faces[face];var surface=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(source.SurfTag)];
            if(surface.Class is not (SurfaceClass.BlendSurface or SurfaceClass.Cylinder))continue;
            var matched=0;var matchedFace=0;double shiftU=0,scaleU=0,shiftV=0,scaleV=0;
            for(var n=0;n<count;n++)
            {
                int candidate,type;Check(PK_FACE_ask_surf(nativeFaces[n],&candidate),"self geometry surface");Check(PK_ENTITY_ask_class(candidate,&type),"self geometry class");
                if(surface.Class==SurfaceClass.Cylinder)
                {
                    if(type!=PK_CLASS_cyl)continue;var a=new M.PK_CYL_sf_s();var b=new PK_CYL_sf_t();
                    Check(KernelRuntime.CylAsk(source.SurfTag,&a),"self cylinder definition");Check(PK_CYL_ask(candidate,&b),"native cylinder definition");
                    if(!CylinderMap(*(PK_CYL_sf_t*)&a,b,out var su,out var du,out var sv,out var dv))continue;
                    Require(matched==0,"unique complete cylinder support correspondence");matched=candidate;matchedFace=nativeFaces[n];shiftU=su;scaleU=du;shiftV=sv;scaleV=dv;
                }
                else
                {
                    if(type!=PK_CLASS_blendsf)continue;var data=KernelRuntime.BlendedEdgeDataPool[surface.DataIndex];var program=KernelRuntime.GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];
                    var a=program.Spines[root.Spine].Curve;PK_BLENDSF_sf_t definition;Check(PK_BLENDSF_ask(candidate,&definition),"native rolling definition");
                    if(Math.Abs(Math.Abs(definition.radii[0])-Math.Abs(data.Range0))>1e-12||Math.Abs(Math.Abs(definition.radii[1])-Math.Abs(data.Range1))>1e-12)continue;
                    var ellipse=new PK_ELLIPSE_sf_t();Check(PK_ELLIPSE_ask(definition.spine,&ellipse),"native rolling carrier");
                    var x=AsVector(ellipse.basis_set.ref_direction);var y=Cross(AsVector(ellipse.basis_set.axis),x);
                    var b=new ParametricSpineCurve{Kind=CurveClass.Ellipse,Origin=Vector(ellipse.basis_set.location),First=Vector(ScaleVector(x,ellipse.R1)),Second=Vector(ScaleVector(y,ellipse.R2))};
                    if(!AnalyticSpineContact.TryParameterMap(a,b,1e-10,definition.spine_extent.value[0],definition.spine_extent.value[1],out var phase,out var sign))continue;
                    var own0=new M.PK_CYL_sf_s();var own1=new M.PK_CYL_sf_s();var pk0=new PK_CYL_sf_t();var pk1=new PK_CYL_sf_t();
                    Check(KernelRuntime.CylAsk(data.Surface0Tag,&own0),"first rolling support");Check(KernelRuntime.CylAsk(data.Surface1Tag,&own1),"second rolling support");
                    Check(PK_CYL_ask(definition.geom_1,&pk0),"native first rolling support");Check(PK_CYL_ask(definition.geom_2,&pk1),"native second rolling support");
                    var direct=CylinderMap(*(PK_CYL_sf_t*)&own0,pk0,out _,out _,out _,out _)&&CylinderMap(*(PK_CYL_sf_t*)&own1,pk1,out _,out _,out _,out _);
                    var swapped=CylinderMap(*(PK_CYL_sf_t*)&own0,pk1,out _,out _,out _,out _)&&CylinderMap(*(PK_CYL_sf_t*)&own1,pk0,out _,out _,out _,out _);
                    if(!direct&&!swapped)continue;
                    phase+=Math.Tau*Math.Round((surface.UMin+(surface.UMax-surface.UMin)*.5-phase-sign*(definition.spine_extent.value[0]+definition.spine_extent.value[1])*.5)/Math.Tau);
                    var box=new PK_UVBOX_t();Check(PK_FACE_find_uvbox(nativeFaces[n],&box),"native rolling finite box");
                    var test=new PK_UV_t();test.param[0]=(box.param[0]+box.param[2])*.5;test.param[1]=.25;
                    Check(PK_SURF_eval(candidate,test,0,0,PK_LOGICAL_false,native),"native rolling chart discriminator");
                    var same=0;double sectionSign=0,sectionShift=0;
                    for(var reverse=0;reverse<2;reverse++)
                    {
                        var uv=new M.PK_UV_s();uv.param[0]=phase+sign*test.param[0];uv.param[1]=reverse==0?.25:.75;
                        Check(KernelRuntime.SurfEval(source.SurfTag,uv,0,0,0,(M.PK_VECTOR_s*)actual),"self rolling chart discriminator");
                        if(Distance(native[0],actual[0])<1e-10){same++;sectionSign=reverse==0?1:-1;sectionShift=reverse;}
                    }
                    Require(same==1,"unique circular section map after complete spine/support certificates");
                    Require(matched==0,"unique rolling support correspondence");matched=candidate;matchedFace=nativeFaces[n];shiftU=phase;scaleU=sign;shiftV=sectionShift;scaleV=sectionSign;
                }
            }
            Require(matched!=0,"every curved face has a certified carrier/support correspondence");
            var bounds=new PK_UVBOX_t();Check(PK_FACE_find_uvbox(matchedFace,&bounds),"matched finite UV box");var samples=0;double d0=0,d1=0,d2=0;
            for(var i=1;i<=9;i++)for(var side=0;side<3;side++)
            {
                var uv=new PK_UV_t();uv.param[0]=bounds.param[0]+(bounds.param[2]-bounds.param[0])*i/10;uv.param[1]=bounds.param[1]+(bounds.param[3]-bounds.param[1])*(.1+side*.4);
                var ownUv=new M.PK_UV_s();ownUv.param[0]=shiftU+scaleU*uv.param[0];ownUv.param[1]=shiftV+scaleV*uv.param[1];
                if(surface.Class==SurfaceClass.Cylinder)LiftUv(ref ownUv,surface);
                var containment=KernelRuntime.SheetSourceContains(face,ownUv,out var inside);Check(containment,"self finite face geometry membership");if(!inside)continue;
                int on;var contains=new PK_FACE_contains_vectors_o_t{n_uvs=1,uvs=&uv};Check(PK_FACE_contains_vectors(matchedFace,&contains,&on),"native finite face membership");if(on==0)continue;
                Check(PK_SURF_eval(matched,uv,2,2,PK_LOGICAL_false,native),"native mapped geometry jet");Check(KernelRuntime.SurfEval(source.SurfTag,ownUv,2,2,0,(M.PK_VECTOR_s*)actual),"self mapped geometry jet");samples++;
                d0=Math.Max(d0,Distance(native[0],actual[0]));d1=Math.Max(d1,Math.Max(Distance(native[3],ScaleVector(actual[3],scaleU)),Distance(native[1],ScaleVector(actual[1],scaleV))));
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
                        var chain=axis==0?(derivative==0?scaleU*scaleU:scaleU*scaleV):(derivative==0?scaleU*scaleV:scaleV*scaleV);
                        var expected=(4*(finePlus[index].coord[d]-fineMinus[index].coord[d])/h-(plus[index].coord[d]-minus[index].coord[d])/(2*h))/3;
                        d2=Math.Max(d2,Math.Abs(expected-chain*actual[target].coord[d])/(1+Math.Abs(expected)));
                    }
                }
            }
            Console.WriteLine($"SELF GEOMETRY face={source.Header.Tag} native_face={matchedFace} type={surface.Class} map_u={shiftU:R}+{scaleU:R}*u map_v={shiftV:R}+{scaleV:R}*v samples={samples} D0={d0:R} D1={d1:R} D2_Richardson={d2:R}");
            Require(samples>0&&d0<1e-10&&d1<1e-8&&d2<1e-6,"self finite geometry under certified parameter map");checkedFaces++;
        }
        Require(checkedFaces>0,"self curved geometry was checked");
    }
    finally{if(checkpoint)KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session);Free(false,nativeFaces);}
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
    int n;int* faces;Check(PK_BODY_ask_faces(reference,&n,&faces),"geometry faces");var nativeSurface=0;var box=new PK_UVBOX_t();
    try{for(var i=0;i<n;i++){int surface,type;Check(PK_FACE_ask_surf(faces[i],&surface),"geometry support");Check(PK_ENTITY_ask_class(surface,&type),"geometry class");if(type!=PK_CLASS_blendsf)continue;Require(nativeSurface==0,"unique procedural surface");nativeSurface=surface;Check(PK_FACE_find_uvbox(faces[i],&box),"finite UV box");}}finally{Free(false,faces);}
    Require(nativeSurface!=0,"procedural surface");var slot=KernelRuntime.ResolveTagRecord(imported).Slot;var face=KernelRuntime.Bodies[slot].FirstFaceBody;var ownSurface=0;
    for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody){var surface=KernelRuntime.Faces[face].SurfTag;if(KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)].Class==SurfaceClass.BlendSurface)ownSurface=surface;}
    Require(ownSurface!=0,"imported procedural surface");var sf=new M.PK_BLENDSF_sf_s();Check(KernelRuntime.BlendSfAsk(ownSurface,&sf),"spine definition");
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
static unsafe void SavedProbes(int reference,int imported,int fresh,byte[] nativeBytes,bool diagnostics)
{
    var path=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","docs","reviews","advanced-modeling","short-edge-spine-probes.csv");
    var rows=File.ReadAllLines(path);var nativeReload=Receive(false,nativeBytes);var failures=0;var changedNative=0;
    var evidence=diagnostics?new List<(PK_VECTOR_t Point,int Row)>():null;
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
            if(results[0]!=results[1]||results[0]!=results[2]||results[0]!=results[3])
            {
                failures++;Console.WriteLine($"SAVED PROBE DIFF row={i} p=({p.coord[0]:R},{p.coord[1]:R},{p.coord[2]:R}) native/roundtrip/self/fresh={results[0]}/{results[1]}/{results[2]}/{results[3]} error={error} stage={KernelRuntime.LastRegularClassificationStage} entity={KernelRuntime.LastRegularClassificationEntity} sheet={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} status={KernelRuntime.LastSheetAlgorithmStatus} uv=({KernelRuntime.LastSheetRayU:R},{KernelRuntime.LastSheetRayV:R})");
                evidence?.Add((p,i));
            }
        }
        // Finish every classification before range/projection diagnostics:
        // warming a PK projection cache can change its exact-threshold path.
        if(evidence!=null)foreach(var item in evidence)
        {NativeProbeEvidence(reference,item.Point,$"row{item.Row}-native");NativeProbeEvidence(nativeReload,item.Point,$"row{item.Row}-native-roundtrip");NativeProbeEvidence(fresh,item.Point,$"row{item.Row}-self-fresh");}
    }
    finally{Check(PK_ENTITY_delete(1,&nativeReload),"delete saved native roundtrip");}
    Console.WriteLine($"SAVED PROBES total={rows.Length-1} mismatches={failures} native_class_changed={changedNative}");Require(failures==0,"saved exact classification regression failed");
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
static unsafe int Seed(double degrees)
{
    var theta=degrees*Math.PI/180;var t=Math.Tan(theta);var f=-2.5+4*t;var h=5-4*t;
    PK_VECTOR_t[] p=[new(-2.5,2.5,0),new(2.5,2.5,0),new(2.5,f,0),new(-1.5,-2.5,0),new(-2.5,-1.5,0),
        new(-2.5,2.5,4),new(-1.5,2.5,5),new(2.5,2.5,h),new(2.5,f,h),new(-1.5,-2.5,5),new(-2.5,-1.5,4)];
    int[][] loops=[[0,1,2,3,4],[0,5,6,7,1],[1,7,8,2],[2,8,9,3],[6,9,8,7],[0,4,10,5],[5,10,9,6],[4,3,9,10]];
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
                var center=key==(3,4)?new PK_VECTOR_t(-1.5,-1.5,0):new PK_VECTOR_t(-1.5,2.5,4);
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
        PK_VECTOR_t[] origins=[new(0,0,0),new(0,2.5,0),new(2.5,0,0),new(-1.5,-2.5,0),new(-1.5,0,5),new(-2.5,0,0)];
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
                var sf=new PK_CYL_sf_t(new PK_AXIS2_sf_t(i==6?new PK_VECTOR_t(-1.5,0,4):new PK_VECTOR_t(-1.5,-1.5,0),i==6?new PK_VECTOR1_t(0,1,0):new PK_VECTOR1_t(0,0,1),new PK_VECTOR1_t(-1,0,0)),1);
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

static unsafe int Fillet(bool own,int body,double angle,double radius,double tolerance,bool reverse,bool procedural,M.PK_TRANSF_sf_s* input=null,double unitScale=1)
{
    int n;int* edges;Check(own?KernelRuntime.BodyAskEdges(body,&n,&edges):PK_BODY_ask_edges(body,&n,&edges),"edges");
    var selected=new int[2];PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
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
            }
        }
    }
    finally{Free(own,edges);}
    Require(selected.All(x=>x!=0),"both selected edges found");if(reverse)Array.Reverse(selected);
    var mark=new PK_EDGE_set_blend_constant_o_t();mark.properties.tolerance=1e-7*unitScale;int count;int* marked;
    fixed(int* selection=selected)Check(own?KernelRuntime.EdgeSetBlendConstant(2,selection,radius,(M.PK_EDGE_set_blend_constant_o_s*)&mark,&count,&marked):PK_EDGE_set_blend_constant(2,selection,radius,&mark,&count,&marked),"set blend");Free(own,marked);
    var fix=new PK_BODY_fix_blends_o_t{local_check=PK_LOGICAL_true,tolerance=tolerance};if(procedural)fix.update=PK_blend_edge_update_v261_c;
    int* made;int fault,edgeFault,topolFault;var started=Stopwatch.GetTimestamp();
    var error=own?KernelRuntime.BodyFixBlends(body,(M.PK_BODY_fix_blends_o_s*)&fix,&count,&made,null,null,&fault,&edgeFault,&topolFault):PK_BODY_fix_blends(body,&fix,&count,&made,null,null,&fault,&edgeFault,&topolFault);
    Console.WriteLine($"FIX {(own?"self":"PK")} error={error} faces={count} fault={fault} edge={edgeFault} topol={topolFault} stage={KernelRuntime.LastRegularBlendStage} network={KernelRuntime.LastRollingNetworkStage} SSI={KernelRuntime.LastGeneralIntersectionStage}/{KernelRuntime.LastGeneralIntersectionStatus}/{KernelRuntime.LastGeneralIntersectionNodes}/{KernelRuntime.LastGeneralIntersectionSegments} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}");
    if(own&&error!=0&&KernelRuntime.ForbidLegacyRegularBlendAssembly)
    {
        Console.WriteLine($"ROLLING FAILURE faces={KernelRuntime.LastRollingFirstFace}/{KernelRuntime.LastRollingSecondFace} sheet={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} t={KernelRuntime.LastSheetParameter:R} status={KernelRuntime.LastSheetAlgorithmStatus} uv=({KernelRuntime.LastSheetRayU:R},{KernelRuntime.LastSheetRayV:R})");
        if(KernelRuntime.LastGeneralIntersectionStage==KernelRuntime.GeneralIntersectionStage.Limits)
        {var a=KernelRuntime.LastGeneralLimitFirst;var b=KernelRuntime.LastGeneralLimitLast;Console.WriteLine($"SSI LIMIT first=({a.U:R},{a.V:R},{a.S:R},{a.T:R}) last=({b.U:R},{b.V:R},{b.S:R},{b.T:R})");}
        var cell=ParametricSurfaceIntersection.LastUnresolvedCell;
        Console.WriteLine($"UNRESOLVED reason={ParametricSurfaceIntersection.LastGraphFailure} depth={cell.Depth} graph_axis={ParametricSurfaceIntersection.LastGraphAxis} box=[{cell.Lower.U:R},{cell.Upper.U:R}]x[{cell.Lower.V:R},{cell.Upper.V:R}]x[{cell.Lower.S:R},{cell.Upper.S:R}]x[{cell.Lower.T:R},{cell.Upper.T:R}]");
        var box=SurfacePlaneIntersection.LastUnresolvedBox;
        Console.WriteLine($"UNRESOLVED scalar budget={SurfacePlaneIntersection.LastUnresolvedBudget} depth={SurfacePlaneIntersection.LastUnresolvedDepth} free={SurfacePlaneIntersection.LastUnresolvedFreeAxis} box=[{box.U0:R},{box.U1:R}]x[{box.V0:R},{box.V1:R}]");
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
        Require(n==0,label+" faults="+n);Console.WriteLine(label+" full check: 0 faults");
    }
    finally{Free(false,faults);}
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

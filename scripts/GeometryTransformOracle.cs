#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=GeometryTransformOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

global using ManagedEntityTag=System.Int32;
using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var outputName=args.Single(x=>x.StartsWith("--output-name=",StringComparison.Ordinal))["--output-name=".Length..];
if(outputName.Length==0||!outputName.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'))throw new ArgumentException("One new output name");
var directory=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","geometry-transform-oracle",outputName));
if(Directory.Exists(directory))throw new ArgumentException("Existing execution evidence is immutable");
Directory.CreateDirectory(directory);
var log=new StringBuilder();
void Log(string message) {Console.WriteLine(message);log.AppendLine(message);}
try
{
    if(!ParasolidScriptHost.TryStartSession("Geometry copy and transform oracle",out var host,out var reason))throw new Exception(reason);
    using(host)
    unsafe
    {
        var options=new M.PK_SESSION_start_o_s {o_t_version=1};Check(KernelRuntime.SessionStart(&options),"managed session start");
        Log("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
        PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"native kernel version");Log($"PK_VERSION {version.major_revision}.{version.minor_revision}.{version.build_number}");
        try {Run(directory,Log,args.Contains("--warm-only",StringComparer.Ordinal));}
        finally {Check(KernelRuntime.SessionStop(),"managed session stop");}
    }
}
catch(Exception ex) {Log("FAIL: "+ex);Environment.ExitCode=1;}
finally {File.WriteAllText(Path.Combine(directory,"report.txt"),log.ToString());}

static string ScriptPath([CallerFilePath] string path="")=>path;
static void Check(int error,string label) {if(error!=0)throw new Exception($"{label}: {error}");}
static void Require(bool condition,string label) {if(!condition)throw new Exception(label);}
static void Equal(double actual,double expected,string label)
    =>Require(double.IsFinite(actual)&&double.IsFinite(expected)&&Math.Abs(actual-expected)<=2e-8*Math.Max(1,Math.Abs(expected)),$"{label}: actual={actual:R}, expected={expected:R}");

static unsafe void Run(string directory,Action<string> log,bool warmOnly)
{
    var failures=0;var passes=0;
    foreach(var operation in new[]{"translation","rotation","scale","reflection"})
    {
        var (managedTransform,nativeTransform)=CreateTransform(operation,log);
        try {CompareWarmIcurveModify(managedTransform,nativeTransform,operation,directory,log);passes++;log("PASS "+operation+" cached native ICurve modification");}
        catch(Exception ex) {failures++;log("FAIL "+operation+" cached native ICurve modification: "+ex.Message);}
        if(warmOnly)continue;
        if(operation=="translation")
        {
            try {CompareModifyClosure(managedTransform,nativeTransform,log);passes++;log("PASS explicit modify dependency closure");}
            catch(Exception ex) {failures++;log("FAIL explicit modify dependency closure: "+ex.Message);}
            foreach(var includeBasis in new[]{false,true})
            {
                var label=includeBasis?"new shared surfaces with explicit basis":"new shared surfaces";
                try {CompareNewSharing(managedTransform,nativeTransform,includeBasis,log);passes++;log("PASS "+label);}
                catch(Exception ex) {failures++;log("FAIL "+label+": "+ex.Message);}
            }
        }
        foreach(var kind in new[]{"line","circle","ellipse","trim","rational-curve","plane","cylinder","cone","sphere","torus","bsurface","offset","swept","spun"})
        {
            var label=operation+"-"+kind;
            try {CompareGeometry(kind,managedTransform,nativeTransform,label,log);passes++;log("PASS "+label);}
            catch(Exception ex) {failures++;log("FAIL "+label+": "+ex.Message);}
        }
        foreach(var shape in new[]{"block","cylinder","sphere","rational-wire","construction-swept-wire","construction-offset-trim-wire"})
        {
            var label=operation+"-body-"+shape;
            try {CompareBody(shape,managedTransform,nativeTransform,label,directory,log);passes++;log("PASS "+label);}
            catch(Exception ex) {failures++;log("FAIL "+label+": "+ex.Message);}
        }
    }
    log($"Transform Oracle: {passes} passed, {failures} failed.");
    Require(failures==0,"Transform Oracle has failed cases.");
}

static unsafe (int Managed,int Native) CreateTransform(string kind,Action<string> log)
{
    PK_TRANSF_t native=0;
    var error=kind switch
    {
        "translation"=>PK_TRANSF_create_translation(new PK_VECTOR_t(2,-3,5),&native),
        "rotation"=>PK_TRANSF_create_rotation(new PK_VECTOR_t(1,2,3),new PK_VECTOR1_t(0,0,1),.7,&native),
        "scale"=>PK_TRANSF_create_equal_scale(2,new PK_VECTOR_t(1,2,3),&native),
        _=>PK_TRANSF_create_reflection(new PK_VECTOR_t(1,0,0),new PK_VECTOR1_t(1,0,0),&native),
    };
    Check(error,"PK create "+kind);PK_TRANSF_sf_t sf;Check(PK_TRANSF_ask(native,&sf),"PK transform ask");
    Require(sizeof(PK_TRANSF_sf_t)==sizeof(M.PK_TRANSF_sf_s),"transform ABI size");
    int managed;Check(KernelRuntime.TransfCreate((M.PK_TRANSF_sf_s*)&sf,&managed),"managed transform create from SDK form");
    var actual=new M.PK_TRANSF_sf_s();Check(KernelRuntime.TransfAsk(managed,&actual),"managed transform ask");
    var values=&sf.matrixI0J0;for(var i=0;i<16;i++)Equal(actual.matrix[i],values[i],kind+" matrix field "+i);
    log($"SDK {kind}: w={sf.matrixI3J3:R}, translation=({sf.matrixI0J3:R},{sf.matrixI1J3:R},{sf.matrixI2J3:R}).");
    return(managed,native);
}

static unsafe void CompareWarmIcurveModify(int transform,int pkTransform,string label,string directory,Action<string> log)
{
    // This native XT definition is the common input to the modification
    // consumer. It is not an independent PK construction of the upstream curve.
    var planeForm=new M.PK_PLANE_sf_s();planeForm.basis_set.axis.coord[2]=1;planeForm.basis_set.ref_direction.coord[0]=1;
    var sphereForm=new M.PK_SPHERE_sf_s{radius=1};sphereForm.basis_set=planeForm.basis_set;
    int plane,sphere;Check(KernelRuntime.PlaneCreate(&planeForm,&plane),"input plane");Check(KernelRuntime.SphereCreate(&sphereForm,&sphere),"input sphere");
    double[] chart=[1,0,0,Math.Cos(.4),Math.Sin(.4),0,Math.Cos(.8),Math.Sin(.8),0];
    var definition=new IcurveDecodeInput{Surface0Tag=plane,Surface1Tag=sphere,BaseParameter=-.25,BaseScale=1,ChartCount=3,ChartHvecs=chart,
        Start=new IcurveLimitInput{Type=LimitType.Artificial,TermUse=LimitTermUse.Unset,Hvecs=chart.AsSpan(0,3).ToArray()},
        End=new IcurveLimitInput{Type=LimitType.Artificial,TermUse=LimitTermUse.Unset,Hvecs=chart.AsSpan(6,3).ToArray()},
        ChordalError=Math.BitIncrement(1-Math.Cos(.2)),AngularError=Math.BitIncrement(.4),UvType=IntersectionUvType.None};
    Require(KernelRuntime.DecodeIcurve(definition,out var data,out _,out _)==AlgorithmStatus.Success,"input native ICurve decode");
    Require(KernelRuntime.TryBindICurveEntity(data,out var inputCurve)==AlgorithmStatus.Success,"input native ICurve binding");
    var sourceRecord=KernelRuntime.GetCurveByTag(inputCurve);var interval=new M.PK_INTERVAL_s();interval.value[0]=sourceRecord.TMin;interval.value[1]=sourceRecord.TMax;
    int inputBody;Check(KernelRuntime.CurveMakeWireBody(inputCurve,interval,&inputBody),"input wire");
    var inputBytes=Transmit(inputBody);File.WriteAllBytes(Path.Combine(directory,label+"-warm-icurve-input.x_t"),inputBytes);
    var nativeInput=Receive(inputBytes);CheckBody(nativeInput,label+" common input",log);
    int count;int* edges;Check(PK_BODY_ask_edges(nativeInput,&count,&edges),"input native edges");
    int nativeCurve=0;
    try{Require(count==1,"one input edge");int curve;Check(PK_EDGE_ask_curve(edges[0],&curve),"input native curve");Check(PK_ENTITY_copy(curve,&nativeCurve),"independent native free curve copy");}
    finally{PK_MEMORY_free(edges);}
    int own;Check(KernelRuntime.EntityCopy(inputCurve,&own),"managed free curve copy");
    Check(KernelRuntime.EntityDelete(1,&inputBody),"delete managed input body");Check(PK_ENTITY_delete(1,&nativeInput),"delete native input body");
    var record=KernelRuntime.GetCurveByTag(own);var ownData=KernelRuntime.ICurveDataPool[record.DataIndex];
    int* roots=stackalloc int[3]{own,ownData.Surface0Tag,ownData.Surface1Tag};
    int* pkRoots=stackalloc int[3];pkRoots[0]=nativeCurve;
    int* dependencies;Check(PK_GEOM_ask_dependents(nativeCurve,&count,&dependencies),"native defining geometry");
    try{Require(count==2,"two native ICurve supports");for(var i=0;i<count;i++)pkRoots[i+1]=dependencies[i];}
    finally{PK_MEMORY_free(dependencies);}
    var parameter=record.TMin+(record.TMax-record.TMin)*.37;
    PK_INTERVAL_t nativeBeforeInterval;Check(PK_CURVE_ask_interval(nativeCurve,&nativeBeforeInterval),"native input interval");
    var actual=stackalloc M.PK_VECTOR_s[3];var expected=stackalloc PK_VECTOR_t[3];
    Check(KernelRuntime.CurveEval(own,parameter,2,actual),"warm native ICurve");Check(PK_CURVE_eval(nativeCurve,parameter,2,expected),"independent input ICurve");
    for(var order=0;order<=1;order++)for(var axis=0;axis<3;axis++)Equal(actual[order].coord[axis],expected[order].coord[axis],label+$" common input D{order}");
    // Off-centre raw PK ICurve D2 differs from d(D1)/dt on the established
    // Case-F family. Transform covariance is checked for each actual backend
    // output separately; retain that raw query difference as its own result.
    var ownBefore=new double[9];var nativeBefore=new double[9];var rawD2=0.0;
    for(var order=0;order<=2;order++)for(var axis=0;axis<3;axis++)
    {ownBefore[3*order+axis]=actual[order].coord[axis];nativeBefore[3*order+axis]=expected[order].coord[axis];if(order==2)rawD2=Math.Max(rawD2,Math.Abs(actual[order].coord[axis]-expected[order].coord[axis]));}
    log($"QUERY_D2_RAW stage=input operation={label} maximum_difference={rawD2:R} status={(rawD2<=2e-8?"Agree":"Different")} transform_acceptance_separate=True");
    var finiteDifference=new double[6];var plus=stackalloc PK_VECTOR_t[2];var minus=stackalloc PK_VECTOR_t[2];
    for(var level=0;level<2;level++)
    {
        var h=level==0?1e-5:5e-6;
        Check(PK_CURVE_eval(nativeCurve,parameter+h,1,plus),"native D1 difference plus");Check(PK_CURVE_eval(nativeCurve,parameter-h,1,minus),"native D1 difference minus");
        for(var axis=0;axis<3;axis++)finiteDifference[3*level+axis]=(plus[1].coord[axis]-minus[1].coord[axis])/(2*h);
    }
    var nativeFdGap=0.0;var reportedFdGap=0.0;
    for(var axis=0;axis<3;axis++)
    {
        var derivative=(4*finiteDifference[3+axis]-finiteDifference[axis])/3;
        nativeFdGap=Math.Max(nativeFdGap,Math.Abs(actual[2].coord[axis]-derivative));reportedFdGap=Math.Max(reportedFdGap,Math.Abs(expected[2].coord[axis]-derivative));
        Equal(actual[2].coord[axis],derivative,label+" mathematical D2 against native D1 finite difference");
    }
    log($"QUERY_D2_FD operation={label} h=1e-5/5e-6 managed_vs_native_D1_difference={nativeFdGap:R} native_reported_vs_native_D1_difference={reportedFdGap:R} sampled_check=True");
    var identity=new GeometryIdentity(record.Header.Tag,record.Header.Generation);
    Require(GeometryEvaluationCache.TryGetExact(identity,parameter,ICurveQueryKind.RegularChartInterval,ChartSide.Right,2,1e-9,out _),"actual L3 sample before modification");
    var options=new M.PK_GEOM_transform_o_s{o_t_version=1,modify=M.ParasolidConstants.PK_GEOM_transform_modify_c,want_out_geoms=1,want_exact=1};
    var pkOptions=new PK_GEOM_transform_o_t{modify=PK_GEOM_transform_modify_c};
    int* outputs=stackalloc int[3];int* pkOutputs=stackalloc int[3];byte* exact=stackalloc byte[3];PK_LOGICAL_t* pkExact=stackalloc PK_LOGICAL_t[3];
    Check(PK_GEOM_transform_2(3,pkRoots,pkTransform,&pkOptions,pkOutputs,pkExact),"native in-place ICurve transform");
    PK_INTERVAL_t nativeAfterInterval;Check(PK_CURVE_ask_interval(nativeCurve,&nativeAfterInterval),"native modified interval");
    log($"NATIVE_PARAMETER_MAP operation={label} before={nativeBeforeInterval.value[0]:R}/{nativeBeforeInterval.value[1]:R} after={nativeAfterInterval.value[0]:R}/{nativeAfterInterval.value[1]:R}");
    int referenceBody;Check(PK_CURVE_make_wire_body(nativeCurve,nativeAfterInterval,&referenceBody),"native modified definition wire");
    File.WriteAllBytes(Path.Combine(directory,label+"-warm-icurve-reference.x_t"),TransmitReference(referenceBody));
    Check(KernelRuntime.GeomTransform2(3,roots,transform,&options,outputs,exact),"managed in-place ICurve transform");
    for(var i=0;i<3;i++)Require(outputs[i]==roots[i]&&pkOutputs[i]==pkRoots[i]&&exact[i]==1&&pkExact[i]==PK_LOGICAL_true,"stable modify handles and exact flags");
    Check(KernelRuntime.CurveAskInterval(own,&interval),"managed modified native interval");
    Equal(interval.value[0],nativeAfterInterval.value[0],label+" native interval start");Equal(interval.value[1],nativeAfterInterval.value[1],label+" native interval end");
    var parameterScale=(nativeAfterInterval.value[1]-nativeAfterInterval.value[0])/(nativeBeforeInterval.value[1]-nativeBeforeInterval.value[0]);
    var mappedParameter=nativeAfterInterval.value[0]+(parameter-nativeBeforeInterval.value[0])*parameterScale;
    Check(KernelRuntime.CurveEval(own,mappedParameter,2,actual),"modified warm ICurve");Check(PK_CURVE_eval(nativeCurve,mappedParameter,2,expected),"independent modified ICurve");
    var form=new M.PK_TRANSF_sf_s();Check(KernelRuntime.TransfAsk(transform,&form),"actual transform for derivative covariance");
    for(var order=0;order<=2;order++)for(var row=0;row<3;row++)
    {
        var ownValue=order==0?form.matrix[4*row+3]/form.matrix[15]:0;var nativeValue=ownValue;
        for(var axis=0;axis<3;axis++){var factor=form.matrix[4*row+axis]/form.matrix[15]/Math.Pow(parameterScale,order);ownValue+=factor*ownBefore[3*order+axis];nativeValue+=factor*nativeBefore[3*order+axis];}
        Equal(actual[order].coord[row],ownValue,label+$" managed native D{order} covariance[{row}]");
        Equal(expected[order].coord[row],nativeValue,label+$" PK native D{order} covariance[{row}]");
        if(order<=1)Equal(actual[order].coord[row],expected[order].coord[row],label+$" modified native D{order}[{row}]");
    }
    rawD2=0;for(var axis=0;axis<3;axis++)rawD2=Math.Max(rawD2,Math.Abs(actual[2].coord[axis]-expected[2].coord[axis]));
    log($"QUERY_D2_RAW stage=modified operation={label} maximum_difference={rawD2:R} status={(rawD2<=2e-8?"Agree":"Different")} transform_acceptance_separate=True");
    Check(KernelRuntime.CurveAskInterval(own,&interval),"modified interval");
    int outputBody;Check(KernelRuntime.CurveMakeWireBody(own,interval,&outputBody),"modified managed finite wire");
    var bytes=Transmit(outputBody);File.WriteAllBytes(Path.Combine(directory,label+"-warm-icurve-output.x_t"),bytes);
    File.WriteAllBytes(Path.Combine(directory,label+"-warm-icurve-reference.x_t"),TransmitReference(referenceBody));
    var fresh=Receive(bytes);CheckBody(fresh,label+" fresh modified wire",log);CheckBody(referenceBody,label+" independent modified wire",log);
    ParasolidBodyCompare.AssertEquivalent(referenceBody,fresh,label+" modified native wire");
    var wrong=Receive(inputBytes);var rejected=false;
    try{ParasolidBodyCompare.AssertWireGeometry(wrong,fresh,label+" original wire negative");}
    catch(InvalidOperationException){rejected=true;}
    Require(rejected,"untransformed same-topology wire must fail");
    int* nativeBodies=stackalloc int[3]{fresh,referenceBody,wrong};Check(PK_ENTITY_delete(3,nativeBodies),"native wire cleanup");
    Check(KernelRuntime.EntityDelete(1,&outputBody),"managed wire cleanup");
    log(label+" native ICurve input, actual warm sample, in-place modify, D0/D1 and per-backend D2 covariance, full/strict finite wire and wrong-position control passed");
}

static unsafe void CompareGeometry(string kind,int transform,int pkTransform,string label,Action<string> log)
{
    var pair=CreateGeometry(kind);int actual,native;byte exact;PK_LOGICAL_t pkExact;
    Check(KernelRuntime.GeomTransform(pair.Managed,transform,0,&actual,&exact),label+" managed GEOM_transform");
    Check(PK_GEOM_transform(pair.Native,pkTransform,0,&native,&pkExact),label+" PK GEOM_transform");
    Require(exact==1&&pkExact==PK_LOGICAL_true,label+" exact flags");
    PK_CLASS_t expectedClass;Check(PK_ENTITY_ask_class(native,&expectedClass),label+" PK output class");
    int actualClass;Check(KernelRuntime.EntityAskClass(actual,&actualClass),label+" managed output class");
    var classesMatch=actualClass==expectedClass;
    if(!classesMatch)log(label+$" output classes: managed={actualClass}, PK={expectedClass}; checking jets before reporting this failure.");
    if(kind is "line" or "circle" or "ellipse" or "trim" or "rational-curve")
    {
        var t=kind=="rational-curve"?.37:.63;var ours=stackalloc M.PK_VECTOR_s[3];var reference=stackalloc PK_VECTOR_t[3];
        Check(KernelRuntime.CurveEval(actual,t,2,ours),label+" managed curve jet");Check(PK_CURVE_eval(native,t,2,reference),label+" PK curve jet");
        for(var n=0;n<3;n++)for(var axis=0;axis<3;axis++)Equal(ours[n].coord[axis],reference[n].coord[axis],label+$" D{n}[{axis}]");
        var interval=new M.PK_INTERVAL_s();PK_INTERVAL_t pkInterval;
        Check(KernelRuntime.CurveAskInterval(actual,&interval),label+" managed interval");Check(PK_CURVE_ask_interval(native,&pkInterval),label+" PK interval");
        Equal(interval.value[0],pkInterval.value[0],label+" interval start");Equal(interval.value[1],pkInterval.value[1],label+" interval end");
    }
    else
    {
        var uv=new M.PK_UV_s();uv.param[0]=.3;uv.param[1]=.4;
        var ours=stackalloc M.PK_VECTOR_s[9];var reference=stackalloc PK_VECTOR_t[9];
        Check(KernelRuntime.SurfEval(actual,uv,2,2,0,ours),label+" managed surface jet");Check(PK_SURF_eval(native,new PK_UV_t(.3,.4),2,2,PK_LOGICAL_false,reference),label+" PK surface jet");
        for(var n=0;n<9;n++)for(var axis=0;axis<3;axis++)Equal(ours[n].coord[axis],reference[n].coord[axis],label+$" jet{n}[{axis}]");
        if(kind=="offset")
        {var sf=new M.PK_OFFSET_sf_s();PK_OFFSET_sf_t pkSf;Check(KernelRuntime.OffsetAsk(actual,&sf),"managed offset ask");Check(PK_OFFSET_ask(native,&pkSf),"PK offset ask");Equal(sf.offset_distance,pkSf.offset_distance,label+" offset sign");}
    }
    // A public copy must be independent of the source and its defining geometry.
    Check(KernelRuntime.EntityDelete(1,&pair.Managed),label+" delete managed source");
    Check(PK_ENTITY_delete(1,&pair.Native),label+" delete PK source");
    Require(KernelRuntime.IsValidTag(actual),label+" copied geometry disappeared after source deletion");
    Require(classesMatch,label+$" output classes: managed={actualClass}, PK={expectedClass}; native jets passed");
}

static unsafe void CompareModifyClosure(int transform,int pkTransform,Action<string> log)
{
    var curve=CreateGeometry("ellipse");int first,second,pkFirst,pkSecond;
    var sf=new M.PK_SWEPT_sf_s {curve=curve.Managed};sf.direction.coord[2]=1;
    Check(KernelRuntime.SweptCreate(&sf,&first),"managed first owner");Check(KernelRuntime.SweptCreate(&sf,&second),"managed second owner");
    sf.curve=curve.Native;Check(PK_SWEPT_create((PK_SWEPT_sf_t*)&sf,&pkFirst),"PK first owner");Check(PK_SWEPT_create((PK_SWEPT_sf_t*)&sf,&pkSecond),"PK second owner");
    var options=new M.PK_GEOM_transform_o_s {o_t_version=1,modify=M.ParasolidConstants.PK_GEOM_transform_modify_c,want_out_geoms=1,want_exact=1};
    var pkOptions=new PK_GEOM_transform_o_t {modify=PK_GEOM_transform_modify_c};
    int* roots=stackalloc int[3] {first,second,curve.Managed};int* pkRoots=stackalloc int[3] {pkFirst,pkSecond,curve.Native};int* outputs=stackalloc int[3];int* pkOutputs=stackalloc int[3];byte* exact=stackalloc byte[3];PK_LOGICAL_t* pkExact=stackalloc PK_LOGICAL_t[3];
    for(var count=1;count<=2;count++)
    {
        var expected=PK_GEOM_transform_2(count,pkRoots,pkTransform,&pkOptions,pkOutputs,pkExact);var actual=KernelRuntime.GeomTransform2(count,roots,transform,&options,outputs,exact);
        Require(expected==PK_ERROR_bad_sharing&&actual==expected,$"incomplete closure ({count}) errors: managed={actual}, PK={expected}");
    }
    Check(PK_GEOM_transform_2(3,pkRoots,pkTransform,&pkOptions,pkOutputs,pkExact),"PK complete explicit closure");
    Check(KernelRuntime.GeomTransform2(3,roots,transform,&options,outputs,exact),"managed complete explicit closure");
    var managedJet=stackalloc M.PK_VECTOR_s[3];var nativeJet=stackalloc PK_VECTOR_t[3];Check(KernelRuntime.CurveEval(curve.Managed,.4,2,managedJet),"managed shared curve after modify");Check(PK_CURVE_eval(curve.Native,.4,2,nativeJet),"PK shared curve after modify");
    for(var n=0;n<3;n++)for(var axis=0;axis<3;axis++)Equal(managedJet[n].coord[axis],nativeJet[n].coord[axis],$"shared dependency D{n}[{axis}]");
    log("Modify incomplete closures returned 921; complete explicit shared closure transformed once in both kernels.");
}

static unsafe void CompareNewSharing(int transform,int pkTransform,bool includeBasis,Action<string> log)
{
    var curve=CreateGeometry("rational-curve");
    int first,second,pkFirst,pkSecond;
    var definition=new M.PK_SWEPT_sf_s {curve=curve.Managed};definition.direction.coord[2]=1;
    Check(KernelRuntime.SweptCreate(&definition,&first),"managed first shared owner");
    definition.curve=curve.Native;
    Check(PK_SWEPT_create((PK_SWEPT_sf_t*)&definition,&pkFirst),"PK first shared owner");
    definition.direction.coord[0]=.6;definition.direction.coord[2]=.8;
    Check(PK_SWEPT_create((PK_SWEPT_sf_t*)&definition,&pkSecond),"PK second shared owner");
    definition.curve=curve.Managed;
    Check(KernelRuntime.SweptCreate(&definition,&second),"managed second shared owner");
    int* inputs=stackalloc int[3] {first,second,curve.Managed};
    int* pkInputs=stackalloc int[3] {pkFirst,pkSecond,curve.Native};
    int* outputs=stackalloc int[3];int* pkOutputs=stackalloc int[3];
    byte* exact=stackalloc byte[3];PK_LOGICAL_t* pkExact=stackalloc PK_LOGICAL_t[3];
    var options=new M.PK_GEOM_transform_o_s
    {
        o_t_version=1,modify=M.ParasolidConstants.PK_GEOM_transform_new_c,want_out_geoms=1,want_exact=1,
    };
    var pkOptions=new PK_GEOM_transform_o_t {modify=PK_GEOM_transform_new_c};
    var count=includeBasis?3:2;
    Check(KernelRuntime.GeomTransform2(count,inputs,transform,&options,outputs,exact),"managed new shared transform");
    Check(PK_GEOM_transform_2(count,pkInputs,pkTransform,&pkOptions,pkOutputs,pkExact),"PK new shared transform");
    var copiedFirst=new M.PK_SWEPT_sf_s();var copiedSecond=new M.PK_SWEPT_sf_s();
    PK_SWEPT_sf_t pkCopiedFirst,pkCopiedSecond;
    Check(KernelRuntime.SweptAsk(outputs[0],&copiedFirst),"managed first copied section");
    Check(KernelRuntime.SweptAsk(outputs[1],&copiedSecond),"managed second copied section");
    Check(PK_SWEPT_ask(pkOutputs[0],&pkCopiedFirst),"PK first copied section");
    Check(PK_SWEPT_ask(pkOutputs[1],&pkCopiedSecond),"PK second copied section");
    Require(pkCopiedFirst.curve==pkCopiedSecond.curve,"PK independent baseline must retain its shared section");
    Require(copiedFirst.curve==copiedSecond.curve,"managed new transform split a shared defining curve");
    Require(copiedFirst.curve!=curve.Managed&&pkCopiedFirst.curve!=curve.Native,"new transform must copy the original basis");
    if(includeBasis)
    {
        Require(pkOutputs[2]==pkCopiedFirst.curve,"PK explicit basis is the transformed shared section");
        Require(outputs[2]==copiedFirst.curve,"managed explicit basis must be the transformed shared section");
    }
    for(var i=0;i<count;i++)
        Require(outputs[i]!=inputs[i]&&pkOutputs[i]!=pkInputs[i]&&exact[i]==1&&pkExact[i]==PK_LOGICAL_true,"new transform output and exact contract");
    Check(KernelRuntime.EntityDelete(1,&first),"delete managed first source");
    Check(KernelRuntime.EntityDelete(1,&second),"delete managed second source");
    Check(PK_ENTITY_delete(1,&pkFirst),"delete PK first source");
    Check(PK_ENTITY_delete(1,&pkSecond),"delete PK second source");
    var ours=stackalloc M.PK_VECTOR_s[3];var reference=stackalloc PK_VECTOR_t[3];
    Check(KernelRuntime.CurveEval(copiedFirst.curve,.4,2,ours),"managed independent shared curve jet");
    Check(PK_CURVE_eval(pkCopiedFirst.curve,.4,2,reference),"PK independent shared curve jet");
    for(var order=0;order<3;order++)for(var axis=0;axis<3;axis++)
        Equal(ours[order].coord[axis],reference[order].coord[axis],$"new shared basis D{order}[{axis}]");
    log($"New transform retained one shared defining curve for {count} explicit inputs; copied jets survive source deletion.");
}

static unsafe (int Managed,int Native) CreateGeometry(string kind)
{
    int managed=0,native=0;var axis=new M.PK_AXIS2_sf_s();axis.axis.coord[2]=1;axis.ref_direction.coord[0]=1;
    switch(kind)
    {
        case "line":
            var line=new M.PK_LINE_sf_s();line.basis_set.location.coord[0]=2;line.basis_set.axis.coord[2]=1;
            Check(KernelRuntime.LineCreate(&line,&managed),"managed line");Check(PK_LINE_create((PK_LINE_sf_t*)&line,&native),"PK line");break;
        case "circle":
            var circle=new M.PK_CIRCLE_sf_s {basis_set=axis,radius=2};Check(KernelRuntime.CircleCreate(&circle,&managed),"managed circle");Check(PK_CIRCLE_create((PK_CIRCLE_sf_t*)&circle,&native),"PK circle");break;
        case "ellipse":
            var ellipse=new M.PK_ELLIPSE_sf_s {basis_set=axis,R1=3,R2=2};Check(KernelRuntime.EllipseCreate(&ellipse,&managed),"managed ellipse");Check(PK_ELLIPSE_create((PK_ELLIPSE_sf_t*)&ellipse,&native),"PK ellipse");break;
        case "trim":
            var basis=CreateGeometry("ellipse");var start=.2;var end=1.4;int error;
            var trim=new M.PK_TRCURVE_sf_s {basis_curve=basis.Managed};trim.t_int.value[0]=start;trim.t_int.value[1]=end;
            Check(KernelRuntime.TrCurveCreate(&trim,&managed),"managed TR");CRTRCU(&basis.Native,&start,&end,&native,&error);Check(error,"PK TR");break;
        case "rational-curve":
            double* poles=stackalloc double[12] {0,0,0,1, 2,2,0,2, 2,0,1,1};double* knots=stackalloc double[2] {0,1};int* mult=stackalloc int[2] {3,3};
            var curve=new M.PK_BCURVE_sf_s {degree=2,n_vertices=3,vertex_dim=4,is_rational=1,vertex=poles,n_knots=2,knot=knots,knot_mult=mult,
                form=M.ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=M.ParasolidConstants.PK_knot_bezier_ends_c,self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c};
            Check(KernelRuntime.BCurveCreate(&curve,&managed),"managed rational curve");Check(PK_BCURVE_create((PK_BCURVE_sf_t*)&curve,&native),"PK rational curve");break;
        case "plane":
            var plane=new M.PK_PLANE_sf_s {basis_set=axis};Check(KernelRuntime.PlaneCreate(&plane,&managed),"managed plane");Check(PK_PLANE_create((PK_PLANE_sf_t*)&plane,&native),"PK plane");break;
        case "cylinder":
            var cylinder=new M.PK_CYL_sf_s {basis_set=axis,radius=2};Check(KernelRuntime.CylCreate(&cylinder,&managed),"managed cylinder");Check(PK_CYL_create((PK_CYL_sf_t*)&cylinder,&native),"PK cylinder");break;
        case "cone":
            var cone=new M.PK_CONE_sf_s {basis_set=axis,radius=2,semi_angle=.3};Check(KernelRuntime.ConeCreate(&cone,&managed),"managed cone");Check(PK_CONE_create((PK_CONE_sf_t*)&cone,&native),"PK cone");break;
        case "sphere":
            var sphere=new M.PK_SPHERE_sf_s {basis_set=axis,radius=2};Check(KernelRuntime.SphereCreate(&sphere,&managed),"managed sphere");Check(PK_SPHERE_create((PK_SPHERE_sf_t*)&sphere,&native),"PK sphere");break;
        case "torus":
            var torus=new M.PK_TORUS_sf_s {basis_set=axis,major_radius=3,minor_radius=1};Check(KernelRuntime.TorusCreate(&torus,&managed),"managed torus");Check(PK_TORUS_create((PK_TORUS_sf_t*)&torus,&native),"PK torus");break;
        case "bsurface":
            double* vertices=stackalloc double[18] {0,-1,0,0,1,0, .5,-1,0,.5,1,0, 1,-1,1,1,1,1};
            double* ku=stackalloc double[2] {0,1};int* um=stackalloc int[2] {3,3};int* vm=stackalloc int[2] {2,2};
            var surface=new M.PK_BSURF_sf_s {u_degree=2,v_degree=1,n_u_vertices=3,n_v_vertices=2,vertex_dim=3,vertex=vertices,n_u_knots=2,n_v_knots=2,
                u_knot=ku,v_knot=ku,u_knot_mult=um,v_knot_mult=vm,u_knot_type=M.ParasolidConstants.PK_knot_bezier_ends_c,v_knot_type=M.ParasolidConstants.PK_knot_bezier_ends_c,
                form=M.ParasolidConstants.PK_BSURF_form_arbitrary_c,self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c,convexity=M.ParasolidConstants.PK_convexity_unset_c};
            Check(KernelRuntime.BSurfCreate(&surface,&managed),"managed Bsurface");Check(PK_BSURF_create((PK_BSURF_sf_t*)&surface,&native),"PK Bsurface");break;
        case "offset":
            var support=CreateGeometry("bsurface");var offset=new M.PK_OFFSET_sf_s {underlying_surface=support.Managed,offset_distance=.25};
            Check(KernelRuntime.OffsetCreate(&offset,&managed),"managed offset");offset.underlying_surface=support.Native;Check(PK_OFFSET_create((PK_OFFSET_sf_t*)&offset,&native),"PK offset");break;
        case "swept":
            var section=CreateGeometry("ellipse");var swept=new M.PK_SWEPT_sf_s {curve=section.Managed};swept.direction.coord[2]=1;
            Check(KernelRuntime.SweptCreate(&swept,&managed),"managed swept");swept.curve=section.Native;Check(PK_SWEPT_create((PK_SWEPT_sf_t*)&swept,&native),"PK swept");break;
        case "spun":
            var profile=CreateGeometry("line");var spun=new M.PK_SPUN_sf_s {curve=profile.Managed};spun.axis.axis.coord[2]=1;
            Check(KernelRuntime.SpunCreate(&spun,&managed),"managed spun");spun.curve=profile.Native;Check(PK_SPUN_create((PK_SPUN_sf_t*)&spun,&native),"PK spun");break;
    }
    return(managed,native);
}

static unsafe void CompareBody(string shape,int transform,int pkTransform,string label,string directory,Action<string> log)
{
    int source=0,reference=0;
    var construction=shape.StartsWith("construction-",StringComparison.Ordinal);
    switch(shape)
    {
        case "block":Check(KernelRuntime.BodyCreateSolidBlock(1,2,3,null,&source),"managed block");Check(PK_BODY_create_solid_block(1,2,3,null,&reference),"PK block");break;
        case "cylinder":Check(KernelRuntime.BodyCreateSolidCyl(2,3,null,&source),"managed cylinder body");Check(PK_BODY_create_solid_cyl(2,3,null,&reference),"PK cylinder body");break;
        case "sphere":Check(KernelRuntime.BodyCreateSolidSphere(2,null,&source),"managed sphere body");Check(PK_BODY_create_solid_sphere(2,null,&reference),"PK sphere body");break;
        case "construction-swept-wire":
        case "construction-offset-trim-wire":
            (source,reference)=CreateConstructionWire(shape=="construction-offset-trim-wire");break;
        default:
            var curve=CreateGeometry("rational-curve");var bounds=new M.PK_INTERVAL_s();bounds.value[1]=1;
            Check(KernelRuntime.CurveMakeWireBody(curve.Managed,bounds,&source),"managed rational wire");
            Check(PK_CURVE_make_wire_body(curve.Native,*(PK_INTERVAL_t*)&bounds,&reference),"PK rational wire");break;
    }
    var originalSamples=construction?PartGeometrySamples(reference,managed:false):null;
    if(construction)ComparePartSamples(originalSamples!,PartGeometrySamples(source,managed:true),label+" original construction");
    int copy,nativeCopy;Check(KernelRuntime.EntityCopy(source,&copy),label+" managed body copy");Check(PK_ENTITY_copy(reference,&nativeCopy),label+" PK body copy");
    int n;int* replaced;byte* exact;Check(KernelRuntime.BodyTransform(copy,transform,0,&n,&replaced,&exact),label+" managed body transform");
    if(replaced!=null)Check(KernelRuntime.MemoryFree(replaced),"free managed replace list");if(exact!=null)Check(KernelRuntime.MemoryFree(exact),"free managed exact list");
    PK_GEOM_t* pkReplaced;PK_LOGICAL_t* pkExact;Check(PK_BODY_transform(nativeCopy,pkTransform,0,&n,&pkReplaced,&pkExact),label+" PK body transform");
    if(pkReplaced!=null)Check(PK_MEMORY_free(pkReplaced),"free PK replace list");if(pkExact!=null)Check(PK_MEMORY_free(pkExact),"free PK exact list");
    var bytes=Transmit(copy);File.WriteAllBytes(Path.Combine(directory,label+".x_t"),bytes);
    File.WriteAllBytes(Path.Combine(directory,label+"-reference.x_t"),TransmitReference(nativeCopy));
    var received=Receive(bytes);
    for(var kind=0;kind<5;kind++)Require(Count(nativeCopy,kind)==Count(received,kind),label+$" topology count {kind}");
    CheckBody(nativeCopy,label+" reference",log);CheckBody(received,label+" received",log);
    CompareBodies(nativeCopy,received,label,log);CompareVertices(nativeCopy,received,label);
    if(construction)
    {
        var expected=PartGeometrySamples(nativeCopy,managed:false);
        ComparePartSamples(expected,PartGeometrySamples(copy,managed:true),label+" transformed construction");
        ComparePartSamples(expected,PartGeometrySamples(received,managed:false),label+" fresh XT construction");
        ComparePartSamples(originalSamples!,PartGeometrySamples(source,managed:true),label+" unchanged managed construction");
        ComparePartSamples(originalSamples!,PartGeometrySamples(reference,managed:false),label+" unchanged PK construction");
    }
    // The source body must remain independently usable after changing the copy.
    var sourceReceived=Receive(Transmit(source));CompareBodies(reference,sourceReceived,label+" unchanged source",log);
    if(construction)ComparePartSamples(originalSamples!,PartGeometrySamples(sourceReceived,managed:false),label+" source fresh XT construction");
    Check(KernelRuntime.EntityDelete(1,&source),label+" delete managed source body");Require(KernelRuntime.IsValidTag(copy),label+" copy lifetime");
    if(construction)
    {
        var expected=PartGeometrySamples(nativeCopy,managed:false);
        ComparePartSamples(expected,PartGeometrySamples(copy,managed:true),label+" construction after source deletion");
        var afterDeletion=Receive(Transmit(copy));
        ComparePartSamples(expected,PartGeometrySamples(afterDeletion,managed:false),label+" independent construction fresh XT");
        log($"  {expected.Count} construction members/definitions preserve membership, sharing, native D0-D2 jets and source independence.");
    }
}

static unsafe (int Managed,int Native) CreateConstructionWire(bool nested)
{
    double* poles=stackalloc double[9] {-2,0,0, 0,3,0, 2,0,0};
    double* knots=stackalloc double[2] {0,1};int* multiplicities=stackalloc int[2] {3,3};
    var definition=new M.PK_BCURVE_sf_s
    {
        degree=2,n_vertices=3,vertex_dim=3,vertex=poles,n_knots=2,knot=knots,knot_mult=multiplicities,
        form=M.ParasolidConstants.PK_BCURVE_form_arbitrary_c,knot_type=M.ParasolidConstants.PK_knot_bezier_ends_c,
        self_intersecting=M.ParasolidConstants.PK_self_intersect_false_c,
    };
    int managedCurve,nativeCurve,managedBody,nativeBody;
    Check(KernelRuntime.BCurveCreate(&definition,&managedCurve),"managed construction basis");
    Check(PK_BCURVE_create((PK_BCURVE_sf_t*)&definition,&nativeCurve),"PK construction basis");
    var bounds=new M.PK_INTERVAL_s();bounds.value[1]=1;
    Check(KernelRuntime.CurveMakeWireBody(managedCurve,bounds,&managedBody),"managed construction wire");
    Check(PK_CURVE_make_wire_body(nativeCurve,*(PK_INTERVAL_t*)&bounds,&nativeBody),"PK construction wire");
    var managedInput=managedCurve;var nativeInput=nativeCurve;
    int count;int* edges;
    Check(KernelRuntime.BodyAskEdges(managedBody,&count,&edges),"managed construction edges");
    try {Require(count==1,"managed construction edge count");Check(KernelRuntime.EdgeAskCurve(edges[0],&managedCurve),"managed attached section");}
    finally {Check(KernelRuntime.MemoryFree(edges),"free managed construction edges");}
    Check(PK_BODY_ask_edges(nativeBody,&count,&edges),"PK construction edges");
    try {Require(count==1,"PK construction edge count");Check(PK_EDGE_ask_curve(edges[0],&nativeCurve),"PK attached section");}
    finally {Check(PK_MEMORY_free(edges),"free PK construction edges");}
    if(managedInput!=managedCurve)Check(KernelRuntime.EntityDelete(1,&managedInput),"delete managed input section");
    if(nativeInput!=nativeCurve)Check(PK_ENTITY_delete(1,&nativeInput),"delete PK input section");
    var sweep=new M.PK_SWEPT_sf_s {curve=managedCurve};sweep.direction.coord[2]=1;
    int managedSwept,nativeSwept;
    Check(KernelRuntime.SweptCreate(&sweep,&managedSwept),"managed construction swept");
    sweep.curve=nativeCurve;Check(PK_SWEPT_create((PK_SWEPT_sf_t*)&sweep,&nativeSwept),"PK construction swept");
    if(nested)
    {
        int root;var offset=new M.PK_OFFSET_sf_s {underlying_surface=managedSwept,offset_distance=.1};
        Check(KernelRuntime.OffsetCreate(&offset,&root),"managed construction offset");
        offset.underlying_surface=nativeSwept;Check(PK_OFFSET_create((PK_OFFSET_sf_t*)&offset,&root),"PK construction offset");
        var trim=new M.PK_TRCURVE_sf_s {basis_curve=managedCurve};trim.t_int.value[0]=.1;trim.t_int.value[1]=.8;
        Check(KernelRuntime.TrCurveCreate(&trim,&root),"managed construction trim");
        var first=.1;var last=.8;int error;CRTRCU(&nativeCurve,&first,&last,&root,&error);Check(error,"PK construction trim");
    }
    return(managedBody,nativeBody);
}

static unsafe List<double[]> PartGeometrySamples(int body,bool managed)
{
    int count;int* geometry;
    Check(managed?KernelRuntime.PartAskGeoms(body,&count,&geometry):PK_PART_ask_geoms(body,&count,&geometry),"part construction members");
    var members=new HashSet<int>(new ReadOnlySpan<int>(geometry,count).ToArray());
    Require(members.Count==count,"construction member duplication");
    if(geometry!=null)Check(managed?KernelRuntime.MemoryFree(geometry):PK_MEMORY_free(geometry),"free construction members");
    var tags=new HashSet<int>(members);var pending=new Queue<int>(tags);
    while(pending.TryDequeue(out var tag))
    {
        int cls,dependency=0;
        Check(managed?KernelRuntime.EntityAskClass(tag,&cls):PK_ENTITY_ask_class(tag,&cls),"construction class");
        if(cls==PK_CLASS_swept)
        {
            var form=new M.PK_SWEPT_sf_s();
            Check(managed?KernelRuntime.SweptAsk(tag,&form):PK_SWEPT_ask(tag,(PK_SWEPT_sf_t*)&form),"construction swept definition");
            dependency=form.curve;
        }
        else if(cls==PK_CLASS_offset)
        {
            var form=new M.PK_OFFSET_sf_s();
            Check(managed?KernelRuntime.OffsetAsk(tag,&form):PK_OFFSET_ask(tag,(PK_OFFSET_sf_t*)&form),"construction offset definition");
            dependency=form.underlying_surface;
        }
        else if(cls==PK_CLASS_trcurve)
        {
            var form=new M.PK_TRCURVE_sf_s();
            Check(managed?KernelRuntime.TrCurveAsk(tag,&form):PK_TRCURVE_ask(tag,(PK_TRCURVE_sf_t*)&form),"construction trimmed definition");
            dependency=form.basis_curve;
        }
        else Require(cls==PK_CLASS_bcurve,"unexpected construction class "+cls);
        if(dependency!=0&&tags.Add(dependency))pending.Enqueue(dependency);
    }
    var rows=new List<double[]>();var values=stackalloc PK_VECTOR_t[9];
    foreach(var tag in tags)
    {
        int cls,owners;
        Check(managed?KernelRuntime.EntityAskClass(tag,&cls):PK_ENTITY_ask_class(tag,&cls),"sample construction class");
        var curve=cls is PK_CLASS_bcurve or PK_CLASS_trcurve;
        if(managed)owners=curve?KernelRuntime.GetCurveByTag(tag).GeometricOwnerCount:KernelRuntime.GetSurfaceByTag(tag).GeometricOwnerCount;
        else
        {
            int* ownerTags;Check(PK_GEOM_ask_geom_owners(tag,&owners,&ownerTags),"construction owners");
            if(ownerTags!=null)Check(PK_MEMORY_free(ownerTags),"free construction owners");
        }
        if(curve)Check(managed?KernelRuntime.CurveEval(tag,.37,2,(M.PK_VECTOR_s*)values):PK_CURVE_eval(tag,.37,2,values),"construction curve D0-D2");
        else
        {
            var uv=new M.PK_UV_s();uv.param[0]=.31;uv.param[1]=.27;
            Check(managed?KernelRuntime.SurfEval(tag,uv,2,2,0,(M.PK_VECTOR_s*)values):PK_SURF_eval(tag,new PK_UV_t(.31,.27),2,2,PK_LOGICAL_false,values),"construction surface D0-D2");
        }
        var length=curve?3:9;var row=new double[3+length*3];row[0]=cls;row[1]=members.Contains(tag)?1:0;row[2]=owners;
        for(var i=0;i<length;i++)for(var axis=0;axis<3;axis++)row[3+3*i+axis]=values[i].coord[axis];
        rows.Add(row);
    }
    rows.Sort((a,b)=>a[0].CompareTo(b[0]));
    Require(rows.Select(row=>row[0]).Distinct().Count()==rows.Count,"fixture construction classes must identify each geometry uniquely");
    return rows;
}

static void ComparePartSamples(List<double[]> expected,List<double[]> actual,string label)
{
    Require(expected.Count==actual.Count,label+" geometry count");
    for(var i=0;i<expected.Count;i++)
    {
        Require(expected[i].Length==actual[i].Length,label+" geometry class");
        for(var j=0;j<expected[i].Length;j++)Equal(actual[i][j],expected[i][j],label+$" geometry {i} value {j}");
    }
}

static unsafe byte[] Transmit(ManagedEntityTag body)
{
    var options=new M.PK_PART_transmit_o_s {o_t_version=4,transmit_format=M.ParasolidConstants.PK_transmit_format_text_c,transmit_version=371,transmit_meshes=M.ParasolidConstants.PK_transmit_meshes_separate_c};
    var block=new M.PK_MEMORY_block_s();Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"managed transmit");
    try {using var output=new MemoryStream();for(var part=&block;part!=null;part=part->next)output.Write(new ReadOnlySpan<byte>(part->bytes,checked((int)part->n_bytes)));return output.ToArray();}
    finally {Check(KernelRuntime.MemoryBlockFree(&block),"free managed transmit");}
}
static unsafe byte[] TransmitReference(int body)
{
    var options=new PK_PART_transmit_o_t {transmit_format=PK_transmit_format_text_c,transmit_version=371};
    var block=new PK_MEMORY_block_t();Check(PK_PART_transmit_b(1,&body,&options,&block),"reference transmit");
    try {using var output=new MemoryStream();for(var part=&block;part!=null;part=part->next)output.Write(new ReadOnlySpan<byte>(part->bytes,checked((int)part->n_bytes)));return output.ToArray();}
    finally {Check(PK_MEMORY_block_f(&block),"free reference transmit");}
}
static unsafe int Receive(byte[] bytes)
{
    fixed(byte* pointer=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t {transmit_format=PK_transmit_format_text_c};
        int count;int* parts;Check(PK_PART_receive_b(block,&options,&count,&parts),"PK receive managed XT");
        try {Require(count==1&&parts!=null,"receive part count");return parts[0];}finally {if(parts!=null)Check(PK_MEMORY_free(parts),"free PK receive list");}
    }
}
static unsafe int Count(int body,int kind)
{
    int count;int* entities;var error=kind switch {0=>PK_BODY_ask_regions(body,&count,&entities),1=>PK_BODY_ask_shells(body,&count,&entities),2=>PK_BODY_ask_faces(body,&count,&entities),3=>PK_BODY_ask_edges(body,&count,&entities),_=>PK_BODY_ask_vertices(body,&count,&entities)};
    Check(error,"PK body topology");try {return count;}finally {if(entities!=null)Check(PK_MEMORY_free(entities),"free topology");}
}
static unsafe void CompareBodies(int expected,int actual,string label,Action<string> log)
{
    ParasolidBodyCompare.AssertEquivalent(expected,actual,label);
    log(label+" strict native comparison and finite wire checks passed");
}
static unsafe void CheckBody(int body,string label,Action<string> log)
{
    var options=new PK_BODY_check_o_t {max_faults=100};int count;PK_check_fault_t* faults;
    Check(PK_BODY_check(body,&options,&count,&faults),label+" full PK_BODY_check");
    try {for(var i=0;i<count;i++)log($"{label} fault={faults[i].state}, entity={faults[i].entity_1}, other={faults[i].entity_2}");Require(count==0,label+" PK_BODY_check faults="+count);}
    finally {if(faults!=null)Check(PK_MEMORY_free(faults),"free body faults");}
}
static unsafe void CompareVertices(int expected,int actual,string label)
{
    int n,m;int* a;int* b;Check(PK_BODY_ask_vertices(expected,&n,&a),"PK expected vertices");Check(PK_BODY_ask_vertices(actual,&m,&b),"PK received vertices");
    try
    {
        Require(n==m,label+" vertex counts");var used=new bool[m];
        for(var i=0;i<n;i++)
        {
            int point;Check(PK_VERTEX_ask_point(a[i],&point),"expected vertex point");PK_POINT_sf_t p;Check(PK_POINT_ask(point,&p),"expected point");var found=false;
            for(var j=0;j<m;j++)
            {
                if(used[j])continue;Check(PK_VERTEX_ask_point(b[j],&point),"actual vertex point");PK_POINT_sf_t q;Check(PK_POINT_ask(point,&q),"actual point");
                var distance=0.0;for(var axis=0;axis<3;axis++)distance=Math.Max(distance,Math.Abs(p.position.coord[axis]-q.position.coord[axis]));
                if(distance>2e-8)continue;used[j]=true;found=true;break;
            }
            Require(found,label+$" missing transformed vertex ({p.position.coord[0]:R},{p.position.coord[1]:R},{p.position.coord[2]:R})");
        }
    }
    finally {if(a!=null)Check(PK_MEMORY_free(a),"free expected vertices");if(b!=null)Check(PK_MEMORY_free(b),"free received vertices");}
}

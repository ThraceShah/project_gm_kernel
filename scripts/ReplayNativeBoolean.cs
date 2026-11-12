#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property AssemblyName=BooleanOperationOracle
using static parasolid;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;

if(args.Length!=3)throw new ArgumentException("project-relative target XT, tool XT, new output name");
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
var inputs=args[..2].Select(p=>{if(Path.IsPathRooted(p))throw new ArgumentException("relative source");var resolved=Path.GetFullPath(Path.Combine(root,p));if(Path.GetRelativePath(root,resolved).StartsWith("..",StringComparison.Ordinal))throw new ArgumentException("inside project source");return File.ReadAllBytes(resolved);}).ToArray();
var name=args[2];if(name.Length==0||name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('-' or '_')))throw new ArgumentException("new output name");
var directory=Path.Combine(root,"temp_docs","native-boolean-replay",name);if(Directory.Exists(directory))throw new InvalidOperationException("Immutable evidence");Directory.CreateDirectory(directory);
for(var i=0;i<inputs.Length;i++){File.WriteAllBytes(Path.Combine(directory,$"input-{i}.x_t"),inputs[i]);Console.WriteLine($"INPUT {i} sha256={Hash(inputs[i])}");}
Console.WriteLine("GM_ASSEMBLY sha256="+Hash(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location)));
if(!ParasolidScriptHost.TryStartSession("Same-byte native Boolean replay",out var host,out var reason))throw new Exception(reason);
using(host)unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"version");Console.WriteLine($"PK_VERSION {version.major_revision}.{version.minor_revision}.{version.build_number}");
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed session");
    try
    {
        KernelRuntime.TraceFiniteBoundary=trace=>
        {
            if(trace.Stage!=KernelRuntime.BoundaryTraceStage.UnresolvedEvaluation)return;
            var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(trace.Curve)];
            Console.WriteLine($"LIVE_EVALUATION_FAILURE curve={trace.Curve} class={curve.Class} t={trace.First:R} domain={curve.TMin:R}/{curve.TMax:R} sense={curve.Sense}");
            var report=KernelRuntime.LastICurveEvalReport;
            Console.WriteLine($"LIVE_EVALUATION_REPORT kind={report.Kind} status={report.Status} plan={report.Plan} segment={report.Segment} iterations={report.NewtonIterations} residual={report.Residual:R} detail={report.Detail} nondefining={report.NonDefiningResidual:R} quality={report.QualityError:R} chart-build={KernelRuntime.LastChartBuildFailure}");
            if(curve.Class==CurveClass.ICurve)
            {
                var data=KernelRuntime.ICurveDataPool[curve.DataIndex];
                Console.WriteLine($"LIVE_ICURVE supports={data.Surface0Tag}/{data.Surface1Tag} types={KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(data.Surface0Tag)].Class}/{KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(data.Surface1Tag)].Class} contact={data.IsBlendContact} program={data.ProgramBlock} charts={data.ChartCount} uv={data.UvType} base={data.BaseParameter:R}/{data.BaseScale:R} limits={data.StartLimit.Type}/{data.EndLimit.Type}");
            }
        };
        var actualTarget=Receive(true,inputs[0]);var actualTool=Receive(true,inputs[1]);
        var nativeTarget=Receive(false,inputs[0]);var nativeTool=Receive(false,inputs[1]);CheckBody(nativeTarget,"same-byte native target");CheckBody(nativeTool,"same-byte native tool");
        var options=new PK_BODY_boolean_o_t{function=PK_boolean_subtract_c,default_tol=1e-7,tracking=PK_LOGICAL_false};int nc=0,gc=0;int* native=null;int* actual=null;
        var nativeError=PK_BODY_boolean(nativeTarget,1,&nativeTool,&options,&nc,&native);
        Console.WriteLine($"OPERATION PK error={nativeError} count={nc}");
        var ownError=KernelRuntime.BodyBoolean(actualTarget,1,&actualTool,(M.PK_BODY_boolean_o_s*)&options,&gc,&actual);
        Console.WriteLine($"OPERATION GM error={ownError} count={gc} stage={KernelRuntime.LastRegularBooleanStage} clip={KernelRuntime.LastRegularClipStage} face={KernelRuntime.LastRegularBooleanFace} sheet={KernelRuntime.LastSheetPartitionStage} curve={KernelRuntime.LastSheetCurve} parameter={KernelRuntime.LastSheetParameter:R} algorithm={KernelRuntime.LastSheetAlgorithmStatus}");
        if(ownError!=0)
        {
            var slot=KernelRuntime.GetCurveSlotByTag(KernelRuntime.LastSheetCurve);
            if(slot>=0){var curve=KernelRuntime.Curves[slot];Console.WriteLine($"FAILURE_CURVE class={curve.Class} sense={curve.Sense} t=[{curve.TMin:R},{curve.TMax:R}]");}
        }
        try
        {
            for(var i=0;i<nc;i++){File.WriteAllBytes(Path.Combine(directory,$"pk-result-{i}.x_t"),Transmit(false,native[i]));CheckBody(native[i],"native result");}
            for(var i=0;i<gc;i++)File.WriteAllBytes(Path.Combine(directory,$"gm-result-{i}.x_t"),Transmit(true,actual[i]));
            Check(nativeError,"native operation");Check(ownError,"managed operation");Require(nc==1&&gc==1,"this replay requests one solid product");
            var fresh=Receive(false,Transmit(true,actual[0]));CheckBody(fresh,"fresh GM result");ParasolidBodyCompare.AssertEquivalent(native[0],fresh,"same-byte replay result");
            Check(PK_ENTITY_delete(1,&fresh),"delete fresh result");Console.WriteLine("PASS same-byte Boolean and fresh XT full/strict comparison; independent input construction is outside this replay");
        }
        finally{if(native!=null)PK_MEMORY_free(native);if(actual!=null)KernelRuntime.MemoryFree(actual);}
    }
    finally{KernelRuntime.TraceFiniteBoundary=null;Check(KernelRuntime.SessionStop(),"managed stop");}
}
static unsafe int Receive(bool own,byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&options,&n,&parts):PK_PART_receive_b(block,&options,&n,&parts),"receive");
        try{Require(n==1,"one actual input body");return parts[0];}finally{if(own)KernelRuntime.MemoryFree(parts);else PK_MEMORY_free(parts);}
    }
}
static unsafe byte[] Transmit(bool own,int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(own?KernelRuntime.PartTransmitB(1,&body,(M.PK_PART_transmit_o_s*)&options,(M.PK_MEMORY_block_s*)&block):PK_PART_transmit_b(1,&body,&options,&block),"transmit");
    try{using var output=new MemoryStream();for(var p=&block;p!=null;p=p->next)output.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return output.ToArray();}
    finally{if(own)KernelRuntime.MemoryBlockFree((M.PK_MEMORY_block_s*)&block);else PK_MEMORY_block_f(&block);}
}
static unsafe void CheckBody(int body,string label)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&n,&faults),"full body check");
    try{for(var i=0;i<n;i++)Console.WriteLine($"FAULT {label} {faults[i].state} {faults[i].entity_1}/{faults[i].entity_2}");Require(n==0,label+" full check");}finally{PK_MEMORY_free(faults);}
}
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Check(int error,string label){if(error!=0)throw new InvalidOperationException(label+" error="+error);}
static void Require(bool value,string label){if(!value)throw new InvalidOperationException(label);}
static string Source([CallerFilePath]string file="")=>file;

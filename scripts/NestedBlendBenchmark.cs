#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=IcurveEvalBenchmark
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;

if(!ParasolidScriptHost.TryStartSession("nested Blend paired benchmark",out var session,out var message))throw new Exception(message);
using(session)
unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"self start");
    try
    {
        foreach(var name in new[]{"nested-blend-native/depth3.x_t","nurbs-blend.x_t"})
        {
            var path=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs",name);var bytes=File.ReadAllBytes(path);int nativeBody,ownBody;
            fixed(byte* p=bytes)
            {
                var input=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var receive=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;int* parts;
                Check(PK_PART_receive_b(input,&receive,&count,&parts),"native receive");try{if(count!=1)throw new Exception("native part count");nativeBody=parts[0];}finally{Check(PK_MEMORY_free(parts),"native free");}
                var ownInput=new M.PK_MEMORY_block_s{n_bytes=(nuint)bytes.Length,bytes=p};Check(KernelRuntime.PartReceiveB(ownInput,(M.PK_PART_receive_o_s*)&receive,&count,&parts),"self receive");
                try{if(count!=1)throw new Exception("own part count");ownBody=parts[0];}finally{Check(KernelRuntime.MemoryFree(parts),"own free");}
            }
            int nf;int* faces;Check(PK_BODY_ask_faces(nativeBody,&nf,&faces),"native faces");int native;
            try{if(nf!=1)throw new Exception("face count");Check(PK_FACE_ask_surf(faces[0],&native),"native surface");}finally{Check(PK_MEMORY_free(faces),"native faces free");}
            if(!KernelRuntime.TryResolveBodySlot(ownBody,out var slot)||KernelRuntime.Bodies[slot].FirstFaceBody<0)throw new Exception("opaque own body");
            var own=KernelRuntime.Faces[KernelRuntime.Bodies[slot].FirstFaceBody].SurfTag;PK_UVBOX_t box;Check(PK_SURF_ask_uvbox(native,&box),"native range");
            Console.WriteLine($"fixture={name}, calls/batch=512, batches=31, warmup=2048, tiering={Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")}");
            if(args.Contains("--probe"))
            {
                PK_VECTOR_t* probe=stackalloc PK_VECTOR_t[6];Console.WriteLine($"box {box.param[0]:R}..{box.param[2]:R}");
                for(var i=0;i<=20;i++)
                {
                    var u=box.param[0]+(box.param[2]-box.param[0])*(i+.5)/21;var errors="";
                    foreach(var v in new[]{.1,.5,.9})for(var order=0;order<=2;order++)errors+=$" {PK_SURF_eval(native,new(u,v),order,order,PK_LOGICAL_true,probe)}";
                    Console.WriteLine($"probe fraction={(i+.5)/21:F3} u={u:R}: {errors}");
                }
                continue;
            }
            for(var pattern=0;pattern<2;pattern++)
            {
                var lower=name.StartsWith("nurbs",StringComparison.Ordinal)?.1:.35;var width=name.StartsWith("nurbs",StringComparison.Ordinal)?.2:.3;
                Console.WriteLine($"regular evaluation window: U fractions {lower:R}..{lower+width:R}, V=.1...9");
                var samples=new PK_UV_t[512];for(var i=0;i<samples.Length;i++)samples[i]=new(box.param[0]+(box.param[2]-box.param[0])*(lower+width*(i+.5)/samples.Length),.1+.8*((i*239)%samples.Length+.5)/samples.Length);
                if(pattern==1){var random=new Random(7801);random.Shuffle(samples);}
                Validate(native,own,samples);
                ValidateSecondDerivatives(native,own,samples,box.param[2]-box.param[0]);
                for(var order=0;order<=2;order++)
                {
                    var pk=new double[31];var self=new double[31];double checksum=0;
                    for(var i=0;i<4;i++){Run(native,own,samples,order,false,ref checksum);Run(native,own,samples,order,true,ref checksum);}
                    long allocated=0;
                    for(var batch=0;batch<31;batch++)
                    {
                        for(var turn=0;turn<2;turn++)
                        {
                            var isOwn=((batch+turn)&1)!=0;var before=GC.GetAllocatedBytesForCurrentThread();var ticks=Run(native,own,samples,order,isOwn,ref checksum);var used=GC.GetAllocatedBytesForCurrentThread()-before;
                            if(isOwn){self[batch]=ticks*1e9/Stopwatch.Frequency/samples.Length;allocated+=used;}else pk[batch]=ticks*1e9/Stopwatch.Frequency/samples.Length;
                        }
                    }
                    Array.Sort(pk);Array.Sort(self);var ratio=pk[15]/self[15];
                    Console.WriteLine($"{(pattern==0?"sequential":"shuffled")} D{order} triangular: PK p50={pk[15]:F1} p95={pk[29]:F1} ns; self p50={self[15]:F1} p95={self[29]:F1} ns; throughput={ratio:P2}; allocated={allocated}; checksum={checksum:R}");
                    if(allocated!=0||ratio<.70)throw new Exception("nested Blend performance gate failed");
                }
            }
        }
    }
    finally{Check(KernelRuntime.SessionStop(),"self stop");}
}
Console.WriteLine(args.Contains("--probe")?"Native range diagnostic complete; no performance gate run.":"PASS: three-level paired evaluation throughput >= 70% of real PK");
return 0;

static unsafe long Run(int native,int own,PK_UV_t[] samples,int order,bool isOwn,ref double checksum)
{
    PK_VECTOR_t* values=stackalloc PK_VECTOR_t[6];var begin=Stopwatch.GetTimestamp();
    foreach(var uv in samples)
    {
        var error=isOwn?KernelRuntime.SurfEval(own,*(M.PK_UV_s*)&uv,order,order,1,(M.PK_VECTOR_s*)values):PK_SURF_eval(native,uv,order,order,PK_LOGICAL_true,values);
        if(error!=0)throw new Exception($"timed eval own={isOwn} error={error}");checksum+=values[0].coord[0];
    }
    return Stopwatch.GetTimestamp()-begin;
}
static unsafe void Validate(int native,int own,PK_UV_t[] samples)
{
    PK_VECTOR_t* expected=stackalloc PK_VECTOR_t[3];M.PK_VECTOR_s* actual=stackalloc M.PK_VECTOR_s[3];var max=0.0;
    foreach(var uv in samples)
    {
        var status=PK_SURF_eval(native,uv,1,1,PK_LOGICAL_true,expected);if(status!=0)Console.WriteLine($"native failure at u={uv.param[0]:R},v={uv.param[1]:R}");Check(status,"PK correctness");Check(KernelRuntime.SurfEval(own,*(M.PK_UV_s*)&uv,1,1,1,actual),"own correctness");
        for(var i=0;i<3;i++)for(var d=0;d<3;d++){var delta=Math.Abs(expected[i].coord[d]-actual[i].coord[d]);max=Math.Max(max,delta);if(delta>1e-8*(1+Math.Abs(expected[i].coord[d])))throw new Exception($"D0/D1 error {delta:R}");}
    }
    Console.WriteLine($"512 varying-point D0/D1 comparison max absolute error={max:R}");
}
static unsafe void ValidateSecondDerivatives(int native,int own,PK_UV_t[] samples,double span)
{
    M.PK_VECTOR_s* actual=stackalloc M.PK_VECTOR_s[6];PK_VECTOR_t* raw=stackalloc PK_VECTOR_t[6];PK_VECTOR_t* left=stackalloc PK_VECTOR_t[3];PK_VECTOR_t* right=stackalloc PK_VECTOR_t[3];
    Span<double> coarse=stackalloc double[9];var maxFd=0.0;var maxRaw=0.0;
    for(var sample=0;sample<16;sample++)
    {
        var uv=samples[sample*31];Check(KernelRuntime.SurfEval(own,*(M.PK_UV_s*)&uv,2,2,1,actual),"own D2");Check(PK_SURF_eval(native,uv,2,2,PK_LOGICAL_true,raw),"PK raw D2");
        foreach(var index in new[]{2,4,5})for(var d=0;d<3;d++)maxRaw=Math.Max(maxRaw,Math.Abs(raw[index].coord[d]-actual[index].coord[d]));
        for(var axis=0;axis<2;axis++)for(var scale=0;scale<2;scale++)
        {
            var h=(axis==0?span:1)*1e-4/(scale+1);var low=uv;var high=uv;low.param[axis]-=h;high.param[axis]+=h;
            Check(PK_SURF_eval(native,low,1,1,PK_LOGICAL_true,left),"native FD left");Check(PK_SURF_eval(native,high,1,1,PK_LOGICAL_true,right),"native FD right");
            for(var derivative=1;derivative<=2;derivative++)
            {
                if(axis==1&&derivative==1)continue;var target=axis==0?(derivative==1?2:4):5;
                for(var d=0;d<3;d++)
                {
                    var fd=(right[derivative].coord[d]-left[derivative].coord[d])/(2*h);var key=(target==2?0:target==4?1:2)*3+d;
                    if(scale==0){coarse[key]=fd;continue;}var reference=(4*fd-coarse[key])/3;var relative=Math.Abs(actual[target].coord[d]-reference)/(1+Math.Abs(reference));
                    maxFd=Math.Max(maxFd,relative);if(relative>1e-6)throw new Exception($"D2 native-D1 finite-difference gate failed: {relative:R}");
                }
            }
        }
    }
    Console.WriteLine($"D2 native-D1 Richardson relative error={maxFd:R}; raw native-D2 max absolute difference={maxRaw:R} (diagnostic)");
}
static void Check(int error,string context){if(error!=0)throw new Exception(context+" error="+error);}
static string ScriptPath([CallerFilePath]string path="")=>path;

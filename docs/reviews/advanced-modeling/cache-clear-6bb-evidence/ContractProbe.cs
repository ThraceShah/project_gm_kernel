#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

// BCL only. Compiles the fixed attribution source with a two-field Occurrence
// scaffold, NOT the kernel. This tests its real predicate and diagnostic state;
// it does not execute numerical readers, PK, a production guard, or a benchmark.
var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(SourcePath())!);
var source=Path.Combine(root,"src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendBoundsAttribution.cs");
var bytes=File.ReadAllBytes(source);
var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
if(hash!="7eb2f433b3a171dd819951292c21c52c93ab89db8a6cbea060c827b19c9d4fd7")
    throw new InvalidOperationException("Fixed 6bb attribution source differs; do not test a moving target.");
var output=Path.Combine(root,"temp_docs/cache-clear-6bb-review/managed-probe");
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output,"Probe.csproj"),"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><PublishAot>false</PublishAot></PropertyGroup>
  <ItemGroup><Compile Include="../../../src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendBoundsAttribution.cs" Link="FixedAttribution.cs" /></ItemGroup>
</Project>
""");
File.WriteAllText(Path.Combine(output,"Probe.cs"),"""
global using BufferOffset=System.Int32;
global using BufferCount=System.Int32;
global using NodeIndex=System.Int32;
using System.Text;
namespace ProjectGmKernel.Native.Geometry.Evaluation;
internal static class Program
{
    private static int Main()=>JointNestedBlendEvaluation.RunContractProbe();
}
internal static partial class JointNestedBlendEvaluation
{
    // Only the fields consumed by the fixed predicate; not a kernel model.
    private struct Occurrence
    {
        internal BufferOffset FirstUv,SecondUv;
        internal Occurrence(BufferOffset a,BufferOffset b){FirstUv=a;SecondUv=b;}
    }
    internal static int RunContractProbe()
    {
        BufferCount checks=0;
        void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks++;Console.WriteLine("PASS "+label);}
        Check(!HasAnyUv([]),"empty nodes => no UV");
        Occurrence[] nodes=new Occurrence[16];Array.Fill(nodes,new(-1,-1));
        Check(!HasAnyUv(nodes),"all 16 occurrences negative => no UV");
        nodes[15]=new(-1,61);
        Check(HasAnyUv(nodes),"late SecondUv/non-prefix slot => full clear required");
        nodes[15]=new(61,-1);
        Check(HasAnyUv(nodes),"late FirstUv => full clear required");
        nodes[0]=new(3,3);nodes[1]=new(3,3);
        Check(HasAnyUv(nodes),"shared/duplicate UV indices => full clear required");
        Array.Fill(nodes,new(-1,-1));nodes[9]=new(0,-1);
        Check(HasAnyUv(nodes),"any nonnegative index including zero => no false empty set");
        Array.Fill(nodes,new(-7,-2));
        Check(!HasAnyUv(nodes),"negative sentinels not restricted to -1");
        // Executable proposed contract only: full span, never sparse Ready reset.
        // Poison is observable here; numerical consumers are proved in review.
        Span<byte> cache=stackalloc byte[63*352];cache.Fill(0xa5);
        if(HasAnyUv(nodes))cache.Clear();
        Check(cache.IndexOfAnyExcept((byte)0xa5)<0,"no-UV proposed branch performs no cache writes");
        nodes[15]=new(-1,61);
        if(HasAnyUv(nodes))cache.Clear();
        Check(cache.IndexOfAnyExcept((byte)0)<0,"UV proposed branch clears entire 22176-byte span including tail");
        Array.Fill(nodes,new(-1,-1));cache.Fill(0x5a);
        if(HasAnyUv(nodes))cache.Clear();
        Check(cache.IndexOfAnyExcept((byte)0x5a)<0,"UV to no-UV history leaves poison untouched");
        nodes[2]=new(27,27);
        if(HasAnyUv(nodes))cache.Clear();
        Check(cache.IndexOfAnyExcept((byte)0)<0,"no-UV to different-UV history always invalidates full span");
        attribution=default;
        Check(AttributionAllowed(),"first thread owns fixed diagnostic state");
        var allowed=true;var thread=new Thread(()=>allowed=AttributionAllowed());thread.Start();thread.Join();
        Check(!allowed&&attribution.ForeignThreadTouches==1,"foreign thread rejected, not TLS-aggregated");
        Check(AttributionIndex(63,true)==127,"last legal bucket");
        Check(AttributionIndex(64,false)==-1&&AttributionIndex(-1,false)==-1&&attribution.OutOfRange==2,"out-of-range preserved, not clamped");
        attribution.JacEntered=11;attribution.ClearReached=11;
        var original=Console.Out;var capture=new StringWriter(new StringBuilder());
        try{Console.SetOut(capture);DumpAttribution();DumpAttribution();}finally{Console.SetOut(original);}
        var text=capture.ToString();
        Check(text.Split("ATTRIBUTION total",StringSplitOptions.None).Length-1==2,"actual dump repeats; it is not one-shot");
        Check(attribution.JacEntered==11&&attribution.ClearReached==11,"dump does not reset session counters");
        Console.WriteLine($"PASS checks={checks}; no kernel/PK/math/speed claim");return 0;
    }
}
""");
Console.WriteLine("FIXED_SOURCE_SHA256 "+hash);
var start=new ProcessStartInfo("dotnet"){WorkingDirectory=output,UseShellExecute=false};
start.ArgumentList.Add("run");start.ArgumentList.Add("--project");start.ArgumentList.Add("Probe.csproj");
start.ArgumentList.Add("-p:UseSharedCompilation=false");
start.Environment["MSBUILDDISABLENODEREUSE"]="1";
using var process=Process.Start(start)!;process.WaitForExit();return process.ExitCode;
static string SourcePath([CallerFilePath]string path="")=>path;

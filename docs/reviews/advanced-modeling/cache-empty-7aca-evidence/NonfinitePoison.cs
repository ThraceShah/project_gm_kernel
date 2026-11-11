#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);
var evidence=Path.Combine(root,"temp_docs/cache-empty-7aca-review");var output=Path.Combine(evidence,"ff-probe");Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output,"Probe.csproj"),"""
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><AssemblyName>PerfBaseline</AssemblyName><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><PublishAot>false</PublishAot></PropertyGroup><ItemGroup><Reference Include="ProjectGmKernel.Native"><HintPath>../production/ProjectGmKernel.Native.dll</HintPath></Reference><Reference Include="ProjectGmKernel.Xt"><HintPath>../production/ProjectGmKernel.Xt.dll</HintPath></Reference></ItemGroup></Project>
""");
// Reuse an existing friend assembly name solely for compile-time access to
// actual interval/program value types; no visibility or production hook edit.
File.WriteAllText(Path.Combine(output,"Program.cs"),"""
global using BufferOffset=System.Int32;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
var file=Path.GetFullPath(args[0],AppContext.BaseDirectory);
var actual=AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
var actualHash=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(actual.Location)));
if(actualHash!=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))))throw new Exception("selected and actually loaded Native DLL differ");
Console.WriteLine("LOADED_NATIVE sha256="+actualHash+" MVID="+actual.ManifestModule.ModuleVersionId);
return Runner.Run(args[1]=="skip");
internal static class Runner
{
    private delegate AlgorithmStatus Jac<TNode,TCache>(in NestedBlendProgram p,ReadOnlySpan<TNode> nodes,ReadOnlySpan<IntervalNumber> boxes,IntervalNumber parameter,Span<IntervalNumber> matrix,Span<IntervalDual> variables,Span<IntervalDual> equations,ReadOnlySpan<BufferOffset> charts,Span<TCache> cache);
    internal static int Run(bool skip)
    {
        try
        {
            var type=typeof(JointNestedBlendEvaluation);typeof(Runner).GetMethod(nameof(Check),BindingFlags.NonPublic|BindingFlags.Static)!.MakeGenericMethod(type.GetNestedType("Occurrence",BindingFlags.NonPublic)!,type.GetNestedType("IntervalSplineContact",BindingFlags.NonPublic)!).Invoke(null,[skip]);return 0;
        }
        catch(Exception ex){Console.WriteLine("FAIL "+(ex is TargetInvocationException t?t.InnerException:ex));return 1;}
    }
    private static void Set<T>(ref T value,string key,BufferOffset data) where T:struct {object box=value;typeof(T).GetField(key,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(box,data);value=(T)box;}
    private static void Check<TNode,TCache>(bool skip) where TNode:unmanaged where TCache:unmanaged
    {
        var jac=typeof(JointNestedBlendEvaluation).GetMethod("IntervalJacobian",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<Jac<TNode,TCache>>();
        var nodes=new TNode[1];foreach(var field in new[]{"Parent","FirstChild","SecondChild","FirstUv","SecondUv"})Set(ref nodes[0],field,-1);
        var x=new KernelVector3{X=1};var y=new KernelVector3{Y=1};var z=new KernelVector3{Z=1};
        BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Plane,default,x,y),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,z,x),NormalSense=1}];
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=2}];BlendChartInstruction[] charts=[new(){Chord=y,Scale=1},new(){Position=y,Parameter=1,Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);var cache=new TCache[63];MemoryMarshal.AsBytes(cache.AsSpan()).Fill(0xff);
        var ready=(bool)typeof(TCache).GetField("Ready",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(cache[0])!;
        var values=typeof(TCache).GetField("Values",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(cache[0])!;
        var first=(IntervalNumber)values.GetType().GetField("value",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(values)!;
        if(!ready||!double.IsNaN(first.Lower))throw new Exception("poison must contain Ready=true and NaN Values, not fresh zero memory");
        IntervalNumber[] boxes=[0,.37,0];var matrix=new IntervalNumber[9];
        if(jac(program,nodes,boxes,.37,matrix,new IntervalDual[3],new IntervalDual[3],[0],cache)!=AlgorithmStatus.Success)throw new Exception("actual analytic Jacobian failed");
        var bytes=MemoryMarshal.AsBytes(cache.AsSpan());if(bytes.IndexOfAnyExcept(skip?(byte)0xff:(byte)0)>=0)throw new Exception("actual clear/skip behavior differs");
        double[] expected=[1,0,0,0,0,1,0,1,0];for(var i=0;i<9;i++)if(!matrix[i].Finite||expected[i]<matrix[i].Lower||expected[i]>matrix[i].Upper)throw new Exception("independent derivative row mismatch");
        Console.WriteLine($"PASS actual-Jac NaN/Ready poison skip={skip} cache-bytes={bytes.Length} matrix-independent=true");
        Console.WriteLine("MATRIX "+string.Join(" ",matrix.Select(v=>$"{BitConverter.DoubleToInt64Bits(v.Lower):x16},{BitConverter.DoubleToInt64Bits(v.Upper):x16}")));
    }
}
""");
var build=new ProcessStartInfo("dotnet"){WorkingDirectory=output};
foreach(var a in new[]{"build","Probe.csproj","-p:UseSharedCompilation=false","--nologo"})build.ArgumentList.Add(a);
build.Environment["MSBUILDDISABLENODEREUSE"]="1";
using(var compiler=Process.Start(build)!){compiler.WaitForExit();if(compiler.ExitCode!=0)return compiler.ExitCode;}
foreach(var item in new[]{("base","original/base-Native.dll","clear"),("guard","runtime/ProjectGmKernel.Native.dll","skip"),("production","production/ProjectGmKernel.Native.dll","skip")})
{
    var bin=Path.Combine(output,"bin/Debug/net10.0");
    // Swap only this probe's private output after compilation and before a
    // fresh child. Otherwise its deps.json can preload the reference DLL.
    File.Copy(Path.Combine(evidence,item.Item2),Path.Combine(bin,"ProjectGmKernel.Native.dll"),true);
    File.Copy(Path.ChangeExtension(Path.Combine(evidence,item.Item2),".pdb"),Path.Combine(bin,"ProjectGmKernel.Native.pdb"),true);
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=output,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var a in new[]{Path.GetRelativePath(output,Path.Combine(bin,"PerfBaseline.dll")),Path.GetRelativePath(bin,Path.Combine(evidence,item.Item2)),item.Item3})start.ArgumentList.Add(a);
    start.Environment["MSBUILDDISABLENODEREUSE"]="1";using var p=Process.Start(start)!;var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();p.WaitForExit();
    var log=await stdout+await stderr;File.WriteAllText(Path.Combine(evidence,"ff-"+item.Item1+".log"),log);Console.WriteLine("CASE "+item.Item1+" exit="+p.ExitCode);Console.Write(log);if(p.ExitCode!=0)return p.ExitCode;
}
return 0;
static string Here([CallerFilePath]string path="")=>path;

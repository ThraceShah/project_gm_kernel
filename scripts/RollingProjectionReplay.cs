#!/usr/bin/env dotnet run
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property AllowUnsafeBlocks=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;

if(args.Length==0)throw new ArgumentException("Provide a project-relative projection snapshot path");
var path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,"..",args[0]));
using var file=File.OpenRead(path);using var reader=new BinaryReader(file);
Console.WriteLine($"SNAPSHOT sha256={Convert.ToHexStringLower(SHA256.HashData(file))}");file.Position=0;
if(reader.ReadString()!="GM finite projection v1")throw new Exception("snapshot version");
var spine=reader.ReadInt32();var first=reader.ReadDouble();var last=reader.ReadDouble();
var witness=Read<IntervalNumber>(reader);var surfaces=Read<BlendSurfaceInstruction>(reader);
var spines=Read<BlendSpineInstruction>(reader);var charts=Read<BlendChartInstruction>(reader);var scalars=Read<double>(reader);
var negative=args.Contains("--wrong-phase")||args.Contains("--exclude-root");
if(args.Contains("--wrong-phase"))witness[6]+=.01;
if(args.Contains("--exclude-root"))last=first+(last-first)*.5;
var program=new NestedBlendProgram(surfaces,spines,charts,scalars);var watch=Stopwatch.StartNew();
var definition=spines[spine];var anchors=charts.AsSpan(definition.ChartOffset,definition.ChartCount);
var closed=anchors[0].Position.X==anchors[^1].Position.X&&anchors[0].Position.Y==anchors[^1].Position.Y&&anchors[0].Position.Z==anchors[^1].Position.Z;
Console.WriteLine($"CARRIER charts={definition.ChartCount} start={definition.StartType} end={definition.EndType} same_terminal_position={closed} chordal_error={definition.ChordalError:R}");
var error=KernelRuntime.TestLocateSharedChart(program,spine,first,last,witness,out var result);
Console.WriteLine($"ACTUAL projection error={error} station=[{result.Lower:R},{result.Upper:R}] ms={watch.Elapsed.TotalMilliseconds:R} domain=[{first:R},{last:R}] expected_rejection={negative}");
for(var i=0;i<witness.Length;i++)Console.WriteLine($"WITNESS {i} [{witness[i].Lower:R},{witness[i].Upper:R}]");
if(args.Contains("--details"))
{
    var data=spines[spine];var native=charts.AsSpan(data.ChartOffset,data.ChartCount);
    Span<IntervalNumber> bounds=stackalloc IntervalNumber[9],captured=stackalloc IntervalNumber[7];
    var point=new IntervalVector(witness[0],witness[1],witness[2]);
    for(var i=0;i<native.Length-1;i++)
    {
        var frame=native[i];var a=Math.Max(first,frame.Parameter);var b=Math.Min(last,native[i+1].Parameter);
        var projected=frame.Parameter+frame.Scale*IntervalVector.Dot(new IntervalVector(frame.Chord),point-new IntervalVector(frame.Position)).Value;
        Console.WriteLine($"FRAME {i} native=[{a:R},{b:R}] position=({frame.Position.X:R},{frame.Position.Y:R},{frame.Position.Z:R}) chord=({frame.Chord.X:R},{frame.Chord.Y:R},{frame.Chord.Z:R}) scale={frame.Scale:R} projected=[{projected.Lower:R},{projected.Upper:R}]");
        var lo=Math.Max(a,projected.Lower);var hi=Math.Min(b,projected.Upper);if(hi<lo)continue;
        var box=new SurfaceParameterBox(lo,hi,0,0);
        var status=JointNestedBlendEvaluation.BoundSpine(program,spine,box,bounds);
        Console.WriteLine($"CELL {i} status={status}");if(status!=AlgorithmStatus.Success)continue;
        for(var j=0;j<3;j++)Console.WriteLine($"POSITION {j} [{bounds[j].Lower:R},{bounds[j].Upper:R}] source=[{witness[j].Lower:R},{witness[j].Upper:R}] separated={bounds[j].Upper<witness[j].Lower||witness[j].Upper<bounds[j].Lower}");
        status=JointNestedBlendEvaluation.BoundSharedSpineWitness(program,spine,box,bounds,capture:captured);
        Console.WriteLine($"CAPTURE status={status}");
        if(status==AlgorithmStatus.Success)for(var j=3;j<7;j++)Console.WriteLine($"STATE {j} [{captured[j].Lower:R},{captured[j].Upper:R}] source=[{witness[j].Lower:R},{witness[j].Upper:R}]");
    }
}
Environment.ExitCode=negative?(error!=0?0:1):(error==0?0:1);
static string Source([CallerFilePath]string value="")=>value;
static T[] Read<T>(BinaryReader reader)where T:unmanaged
{
    var size=reader.ReadInt32();var count=reader.ReadInt32();if(size!=Unsafe.SizeOf<T>())throw new Exception("snapshot layout mismatch");
    return MemoryMarshal.Cast<byte,T>(reader.ReadBytes(checked(size*count))).ToArray();
}

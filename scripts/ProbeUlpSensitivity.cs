#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
// Diagnostic: classify every fixed short-edge probe with real PK on a
// received XT body, then again with each coordinate moved by up to N ulps.
// Reports rows whose PK enclosure changes under those perturbations, and
// rows where the received body differs from the CSV native baseline.
// usage: dotnet run scripts/ProbeUlpSensitivity.cs -- BODY.x_t [ULPS]
using static parasolid;

if(!ParasolidScriptHost.TryStartSession("probe ulp sensitivity",out var session,out var skip)){Console.WriteLine(skip);return 0;}
using(session)unsafe
{
    var ulps=args.Length>1?int.Parse(args[1]):4;
    var bytes=File.ReadAllBytes(args[0]);int body;
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var o=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        C(PK_PART_receive_b(block,&o,&n,&parts));body=parts[0];
    }
    var csv=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","docs","reviews","advanced-modeling","short-edge-spine-probes.csv");
    var rows=File.ReadAllLines(csv);var unstable=0;var baselineDiff=0;var inv=System.Globalization.CultureInfo.InvariantCulture;
    for(var i=1;i<rows.Length;i++)
    {
        var f=rows[i].Split(',');var x=new[]{double.Parse(f[5],inv),double.Parse(f[6],inv),double.Parse(f[7],inv)};
        var baseClass=Classify(body,x);var csvNative=int.Parse(f[8],inv);var seen=new SortedSet<int>{baseClass};
        for(var axis=0;axis<3;axis++)
        for(var k=-ulps;k<=ulps;k++)
        {
            if(k==0)continue;var y=(double[])x.Clone();
            for(var s=0;s<Math.Abs(k);s++)y[axis]=k>0?Math.BitIncrement(y[axis]):Math.BitDecrement(y[axis]);
            seen.Add(Classify(body,y));
        }
        if(baseClass!=csvNative)baselineDiff++;
        if(seen.Count>1||baseClass!=csvNative)
        {
            if(seen.Count>1)unstable++;
            Console.WriteLine($"ROW {i} csv_native={csvNative} received={baseClass} within_{ulps}ulp={string.Join('/',seen)} p=({f[5]},{f[6]},{f[7]})");
        }
    }
    Console.WriteLine($"SUMMARY rows={rows.Length-1} ulps={ulps} unstable={unstable} received_vs_csv_native={baselineDiff}");
}
return 0;
static unsafe int Classify(int body,double[] x)
{
    var v=new PK_VECTOR_t(x[0],x[1],x[2]);int e,t;C(PK_BODY_contains_vector(body,v,&e,&t));return e;
}
static void C(int e){if(e!=0)throw new Exception("PK error "+e);}
static string ScriptPath([System.Runtime.CompilerServices.CallerFilePath]string path="")=>path;

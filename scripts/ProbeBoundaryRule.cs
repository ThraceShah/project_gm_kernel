#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
// Diagnostic: receive one XT in real PK and in the managed kernel, then
// classify base + d*direction for a sweep of d, reporting both enclosures.
// usage: dotnet run scripts/ProbeBoundaryRule.cs -- BODY.x_t BX BY BZ DX DY DZ [D0 D1 STEPS]
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;

if(!ParasolidScriptHost.TryStartSession("probe boundary rule",out var session,out var skip)){Console.WriteLine(skip);return 0;}
using(session)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};C(KernelRuntime.SessionStart(&start));
    var inv=System.Globalization.CultureInfo.InvariantCulture;
    var bytes=File.ReadAllBytes(args[0]);
    var b=new double[3];var d=new double[3];for(var i=0;i<3;i++){b[i]=double.Parse(args[1+i],inv);d[i]=double.Parse(args[4+i],inv);}
    var d0=args.Length>7?double.Parse(args[7],inv):2e-9;var d1=args.Length>8?double.Parse(args[8],inv):1.2e-8;var steps=args.Length>9?int.Parse(args[9]):20;
    int native,own;
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var o=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        C(PK_PART_receive_b(block,&o,&n,&parts));native=parts[0];
        C(KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&o,&n,&parts));own=parts[0];
    }
    for(var k=0;k<=steps;k++)
    {
        var s=d0+(d1-d0)*k/steps;var v=new PK_VECTOR_t(b[0]+s*d[0],b[1]+s*d[1],b[2]+s*d[2]);
        int pe,pt,ge,gt;C(PK_BODY_contains_vector(native,v,&pe,&pt));
        var error=KernelRuntime.BodyContainsVector(own,*(M.PK_VECTOR_s*)&v,&ge,&gt);if(error!=0)ge=-error;
        int pc=0,gc=0;if(pt!=0)PK_ENTITY_ask_class(pt,&pc);
        Console.WriteLine($"D {s:E6} PK={pe}/{pc} GM={ge} stage={KernelRuntime.LastRegularClassificationStage} {(pe!=ge?"DIFF":"")}");
    }
}
return 0;
static void C(int e){if(e!=0)throw new Exception("error "+e);}

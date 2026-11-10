#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
// Diagnostic: apply the short-edge oracle's input similarity (rigid or
// mirror) to one XT in real PK and in the managed kernel, and transmit both
// results as text XT, so the transformed carrier representations can be
// compared node by node.
// usage: dotnet run scripts/XtTransformCarrier.cs -- INPUT.x_t OUTPUT_PREFIX rigid|mirror
using static parasolid;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;

if(!ParasolidScriptHost.TryStartSession("transform carrier",out var session,out var skip)){Console.WriteLine(skip);return 0;}
using(session)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};C(KernelRuntime.SessionStart(&start));
    var mirror=args[2]=="mirror";var bytes=File.ReadAllBytes(args[0]);
    // Same matrix as ShortEdgeBlendOracle.InputTransform with scale 1.
    var m=new M.PK_TRANSF_sf_s();var a=.37;var b=-.29;var c=.23;var ca=Math.Cos(a);var sa=Math.Sin(a);var cb=Math.Cos(b);var sb=Math.Sin(b);var cc=Math.Cos(c);var sc=Math.Sin(c);
    double[] r=[ca*cb,ca*sb*sc-sa*cc,ca*sb*cc+sa*sc,-.7, sa*cb,sa*sb*sc+ca*cc,sa*sb*cc-ca*sc,1.1, -sb,cb*sc,cb*cc,2.3, 0,0,0,1];
    for(var row=0;row<3;row++)for(var column=0;column<3;column++)if(mirror&&column==0)r[4*row+column]=-r[4*row+column];
    for(var i=0;i<16;i++)m.matrix[i]=r[i];
    foreach(var own in new[]{false,true})
    {
        int body;
        fixed(byte* p=bytes)
        {
            var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var o=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
            C(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&o,&n,&parts):PK_PART_receive_b(block,&o,&n,&parts));body=parts[0];
        }
        int transform;C(own?KernelRuntime.TransfCreate(&m,&transform):PK_TRANSF_create((PK_TRANSF_sf_t*)&m,&transform));
        // Same call as the oracle's Move.
        int nm;int* replaced;PK_LOGICAL_t* exact;
        C(own?KernelRuntime.BodyTransform(body,transform,0,&nm,&replaced,(byte**)&exact):PK_BODY_transform(body,transform,0,&nm,&replaced,&exact));
        var to=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c};PK_MEMORY_block_t output;
        if(own)C(KernelRuntime.PartTransmitB(1,&body,(M.PK_PART_transmit_o_s*)&to,(M.PK_MEMORY_block_s*)&output));else C(PK_PART_transmit_b(1,&body,&to,&output));
        var text=new byte[(int)output.n_bytes];new ReadOnlySpan<byte>(output.bytes,text.Length).CopyTo(text);
        File.WriteAllBytes(args[1]+(own?"-gm.x_t":"-pk.x_t"),text);
        Console.WriteLine($"{(own?"GM":"PK")} transmitted {text.Length} bytes");
    }
}
return 0;
static void C(int e){if(e!=0)throw new Exception("error "+e);}

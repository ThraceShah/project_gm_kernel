#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property AssemblyName=ProfileModelingOracle
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var name=args.Single(x=>x.StartsWith("--output-name=",StringComparison.Ordinal))[14..];
Require(name.Length>0&&name.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'),"one output directory name");
var output=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","temp_docs","planar-sheet-ownership",name);
Require(!Directory.Exists(output),"immutable execution evidence");Directory.CreateDirectory(output);ParasolidMassProperties.BeginEvidence(output);
Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
Require(ParasolidScriptHost.TryStartSession("Planar sheet and oblique extrusion ownership",out var host,out var reason),reason);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"GM start");
    try
    {
        foreach(var extrude in new[]{false,true})
        {
            var label=extrude?"oblique-extrude":"planar-sheet";var before=Counts();
            PK_VECTOR_t* points=stackalloc PK_VECTOR_t[4]{new(-2,-3,0),new(2,-3,0),new(2,3,0),new(-2,3,0)};
            var sheet=new PK_BODY_create_sheet_planar_o_t();sheet.plane.axis=new(0,0,1);int own,native;
            Check(KernelRuntime.BodyCreateSheetPlanar(4,(M.PK_VECTOR_s*)points,(M.PK_BODY_create_sheet_planar_o_s*)&sheet,&own),"GM sheet");
            Check(PK_BODY_create_sheet_planar(4,points,&sheet,&native),"PK sheet");
            File.WriteAllBytes(Path.Combine(output,label+"-gm-seed.x_t"),Transmit(own));
            File.WriteAllBytes(Path.Combine(output,label+"-pk-seed.x_t"),TransmitNative(native));
            if(extrude)
            {
                var options=new PK_BODY_extrude_o_t();options.end_bound.distance=8;var path=new PK_VECTOR1_t(.35,0,Math.Sqrt(1-.35*.35));
                int result;var tracking=new M.PK_TOPOL_track_r_s();var local=new M.PK_TOPOL_local_r_s();
                var ownError=KernelRuntime.BodyExtrude(own,*(M.PK_VECTOR_s*)&path,(M.PK_BODY_extrude_o_s*)&options,&result,&tracking,&local);
                Console.WriteLine($"API side=gm operation=extrude error={ownError} status={local.status}");Check(ownError,"GM extrusion");Require(local.status==PK_local_status_ok_c,"GM local status");
                Check(KernelRuntime.TopolTrackResultFree(&tracking),"GM tracking free");Check(KernelRuntime.TopolLocalResultFree(&local),"GM local free");
                Check(KernelRuntime.EntityDelete(1,&own),"delete GM source profile");own=result;
                PK_TOPOL_track_r_t pkTracking=default;PK_TOPOL_local_r_t pkLocal=default;
                var nativeError=PK_BODY_extrude(native,path,&options,&result,&pkTracking,&pkLocal);
                Console.WriteLine($"API side=pk operation=extrude error={nativeError} status={pkLocal.status}");Check(nativeError,"PK extrusion");Require(pkLocal.status==PK_local_status_ok_c,"PK local status");
                Check(PK_TOPOL_track_r_f(&pkTracking),"PK tracking free");Check(PK_TOPOL_local_r_f(&pkLocal),"PK local free");
                Check(PK_ENTITY_delete(1,&native),"delete PK source profile");native=result;
            }
            var bytes=Transmit(own);File.WriteAllBytes(Path.Combine(output,label+"-gm-product.x_t"),bytes);
            File.WriteAllBytes(Path.Combine(output,label+"-pk-product.x_t"),TransmitNative(native));var received=Receive(bytes);
            FullCheck(native,label+" independent");FullCheck(received,label+" fresh GM XT");ParasolidBodyCompare.AssertEquivalent(native,received,label);
            Require(ParasolidMassProperties.Compare(native,received,label)!=MassAgreement.Different,label+" mass separated");
            Check(KernelRuntime.EntityDelete(1,&own),"delete GM product");Check(PK_ENTITY_delete(1,&native),"delete PK product");Check(PK_ENTITY_delete(1,&received),"delete fresh receive");
            var after=Counts();Console.WriteLine($"OWNERSHIP {label} before={before} after={after}");Require(before==after,label+" releases actual geometry owners");
            Console.WriteLine("GEOMETRY_TOPOLOGY_OWNERSHIP_PASS "+label);
        }
        if(ParasolidMassProperties.UnresolvedCount!=0)Environment.ExitCode=2;
    }
    finally{Check(KernelRuntime.SessionStop(),"GM stop");}
}
static (int,int,int,int,ulong) Counts()=>(KernelRuntime.Bodies.AliveCount,KernelRuntime.Curves.AliveCount,KernelRuntime.Surfaces.AliveCount,KernelRuntime.Points.AliveCount,KernelRuntime.ReturnOutstandingBlocks);
static unsafe void FullCheck(int body,string name)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&count,&faults),"full check");
    try{for(var i=0;i<count;i++)Console.WriteLine($"FAULT {name} state={faults[i].state} entities={faults[i].entity_1}/{faults[i].entity_2}");Require(count==0,name+" full BODY_check");}
    finally{if(faults!=null)PK_MEMORY_free(faults);}
}
static void Check(int error,string name){if(error!=0)throw new Exception(name+" error="+error);}
static void Require(bool value,string name){if(!value)throw new Exception(name);}
static string ScriptPath([CallerFilePath]string path="")=>path;
static unsafe byte[] Transmit(int body)
{
    var options=new M.PK_PART_transmit_o_s{o_t_version=4,transmit_format=PK_transmit_format_text_c,transmit_version=371,transmit_meshes=PK_transmit_meshes_separate_c};M.PK_MEMORY_block_s block;
    Check(KernelRuntime.PartTransmitB(1,&body,&options,&block),"own text XT transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(KernelRuntime.MemoryBlockFree(&block),"XT buffers");}
}
static unsafe byte[] TransmitNative(PK_BODY_t body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1,&body,&options,&block),"native text XT transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(PK_MEMORY_block_f(&block),"native XT buffers");}
}
static unsafe int Receive(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(PK_PART_receive_b(block,&options,&n,&parts),"real PK receive");try{Require(n==1,"one received solid");return parts[0];}finally{PK_MEMORY_free(parts);}
    }
}

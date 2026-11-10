#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
// Diagnostic: receive XT files with real PK, run the full check, and
// dump every cylinder face (all faces with ALLFACES=1) with loop types and fin geometry.
using static parasolid;

if(!ParasolidScriptHost.TryStartSession("loop diag",out var session,out var skip)){Console.WriteLine(skip);return 0;}
using(session)unsafe
{
    foreach(var path in args)
    {
        Console.WriteLine("=== "+Path.GetFileName(path));
        var bytes=File.ReadAllBytes(path);int body;
        fixed(byte* p=bytes)
        {
            var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var o=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
            C(PK_PART_receive_b(block,&o,&n,&parts));body=parts[0];
        }
        {var co=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* f;C(PK_BODY_check(body,&co,&n,&f));
         for(var i=0;i<n;i++){int fc;PK_ENTITY_ask_class(f[i].entity_1,&fc);Console.WriteLine($"FAULT state={f[i].state} e1={f[i].entity_1} class={fc} e2={f[i].entity_2}");}}
        int nf;int* faces;C(PK_BODY_ask_faces(body,&nf,&faces));
        for(var fi=0;fi<nf;fi++)
        {
            int face=faces[fi];int surf,sc;C(PK_FACE_ask_surf(face,&surf));C(PK_ENTITY_ask_class(surf,&sc));
            if(sc!=PK_CLASS_cyl&&!(args.Length>0&&Environment.GetEnvironmentVariable("ALLFACES")=="1"))continue;
            var box=new PK_UVBOX_t();var e=PK_FACE_find_uvbox(face,&box);
            Console.WriteLine($"FACE {face} surf={surf} class={sc} uvbox_err={e} uv=[{box.param[0]:R},{box.param[2]:R}]x[{box.param[1]:R},{box.param[3]:R}]");
            int nl;int* loops;C(PK_FACE_ask_loops(face,&nl,&loops));
            for(var li=0;li<nl;li++)
            {
                int lt;C(PK_LOOP_ask_type(loops[li],&lt));int nfin;int* fins;C(PK_LOOP_ask_fins(loops[li],&nfin,&fins));
                Console.WriteLine($"  LOOP {loops[li]} type={lt} fins={nfin}");
                for(var k=0;k<nfin;k++)
                {
                    int edge;C(PK_FIN_ask_edge(fins[k],&edge));PK_LOGICAL_t pos;C(PK_FIN_is_positive(fins[k],&pos));
                    int curve,cc;var ends=new PK_VECTOR_t[2];var iv=new PK_INTERVAL_t();PK_LOGICAL_t sense;
                    C(PK_EDGE_ask_geometry(edge,PK_LOGICAL_true,&curve,&cc,ends,&iv,&sense));
                    var fb=new PK_UVBOX_t();var fe=PK_FIN_find_uvbox(fins[k],&fb);
                    Console.WriteLine($"    FIN {fins[k]} edge={edge} pos={pos} curve={curve} class={cc} sense={sense} t=[{iv.value[0]:R},{iv.value[1]:R}] start=({ends[0].coord[0]:R},{ends[0].coord[1]:R},{ends[0].coord[2]:R}) end=({ends[1].coord[0]:R},{ends[1].coord[1]:R},{ends[1].coord[2]:R}) finuv_err={fe} uv=[{fb.param[0]:R},{fb.param[2]:R}]x[{fb.param[1]:R},{fb.param[3]:R}]");
                }
            }
        }
    }
}
return 0;
static void C(int e){if(e!=0)throw new Exception("PK error "+e);}

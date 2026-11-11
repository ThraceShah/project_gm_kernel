#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property AssemblyName=BooleanOperationOracle
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,".."));
var input=args.Single(x=>x.StartsWith("--input-set="))[12..];var name=args.Single(x=>x.StartsWith("--output-name="))[14..];
Require(new[]{input,name}.All(s=>s.Length>0&&s.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_')),"single directory names");
var output=Path.Combine(root,"temp_docs","native-finite-ssi-boundary",name);Require(!Directory.Exists(output),"immutable output");Directory.CreateDirectory(output);
Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
Require(ParasolidScriptHost.TryStartSession("Native finite SSI boundary parameters",out var host,out var reason),reason);
using(host)unsafe
{
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"GM start");
    try
    {
        var bodies=new int[2];
        for(var i=0;i<2;i++)
        {
            var file="continuous-15903-gm-"+(i==0?"target":"tool1")+"-before.x_t";
            var bytes=File.ReadAllBytes(Path.Combine(root,"temp_docs","regular-curved-boolean-oracle",input,file));
            Console.WriteLine($"INPUT file={file} sha256={Convert.ToHexStringLower(SHA256.HashData(bytes))}");File.WriteAllBytes(Path.Combine(output,file),bytes);bodies[i]=ReceiveOwn(bytes);
        }
        foreach(var body in bodies)
        {
            Require(KernelRuntime.TryResolveBodySlot(body,out var bs),"actual input body");var edge=KernelRuntime.Bodies[bs].FirstEdgeBody;
            for(var i=0;i<KernelRuntime.Bodies[bs].EdgeCountBody;i++,edge=KernelRuntime.Edges[edge].NextInBody)
            {var tag=KernelRuntime.Edges[edge].CurveTag;Console.WriteLine($"INPUT_CURVE body={body} curve={tag} class={KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(tag)].Class}");}
        }
        var sourceFirst=Roof(bodies[0]);var sourceSecond=Roof(bodies[1]);var side=LowerYSide(bodies[1]);
        int first,second;Check(KernelRuntime.EntityCopy(sourceFirst,&first),"copy literal first support");Check(KernelRuntime.EntityCopy(sourceSecond,&second),"copy literal second support");
        M.PK_BSURF_sf_s definition;Check(KernelRuntime.BSurfAsk(side,&definition),"source boundary definition");int nativeSide;
        try{Check(PK_BSURF_create((PK_BSURF_sf_t*)&definition,&nativeSide),"same native BSurface boundary");}finally{Free(definition);}
        var options=new M.PK_SURF_intersect_surf_o_s{o_t_version=3,tolerance=1e-8,mixed_curve_category=PK_mixed_intersection_pline_c};
        int np,nc;M.PK_VECTOR_s* points;int* curves;M.PK_INTERVAL_s* ranges;int* kinds;
        Check(KernelRuntime.SurfIntersectSurf(first,second,&options,&np,&points,&nc,&curves,&ranges,&kinds),"actual source SSI");
        try
        {
            Console.WriteLine($"SSI points={np} curves={nc}");Require(nc>0,"actual SSI curve");
            for(var i=0;i<nc;i++)
            {
                var curve=curves[i];var range=ranges[i];int wire;Check(KernelRuntime.CurveMakeWireBody(curve,range,&wire),"native curve wire");
                var bytes=Transmit(wire);File.WriteAllBytes(Path.Combine(output,$"ssi-{i}.x_t"),bytes);var received=Receive(bytes);
                var check=new PK_BODY_check_o_t{max_faults=100};int faults;PK_check_fault_t* issues;
                Check(PK_BODY_check(received,&check,&faults,&issues),"fresh native wire check");try{Console.WriteLine($"WIRE curve={i} faults={faults}");Require(faults==0,"fresh wire faults");}finally{if(issues!=null)PK_MEMORY_free(issues);}
                int ne;int* edges;Check(PK_BODY_ask_edges(received,&ne,&edges),"fresh wire edges");Require(ne==1,"one finite edge");
                int nativeCurve;PK_CLASS_t cls;PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];PK_INTERVAL_t nativeRange;PK_LOGICAL_t sense;
                try{Check(PK_EDGE_ask_geometry(edges[0],PK_LOGICAL_true,&nativeCurve,&cls,ends,&nativeRange,&sense),"fresh native edge geometry");}finally{PK_MEMORY_free(edges);}
                Console.WriteLine($"RANGE gm={range.value[0]:R}/{range.value[1]:R} native={nativeRange.value[0]:R}/{nativeRange.value[1]:R}");
                foreach(var t in new[]{range.value[0],Math.BitDecrement(range.value[0]),range.value[0]-(range.value[1]-range.value[0])*1e-6,range.value[1],Math.BitIncrement(range.value[1])})
                {
                    M.PK_VECTOR_s gm;PK_VECTOR_t pk;var ge=KernelRuntime.CurveEval(curve,t,0,&gm);var pe=PK_CURVE_eval(nativeCurve,t,0,&pk);
                    var ownPoint=ge==0?$"{gm.coord[0]:R}/{gm.coord[1]:R}/{gm.coord[2]:R}":"unavailable";
                    var nativePoint=pe==0?$"{pk.coord[0]:R}/{pk.coord[1]:R}/{pk.coord[2]:R}":"unavailable";
                    Console.WriteLine($"EVAL t={t:R} gm_error={ge} pk_error={pe} gm={ownPoint} pk={nativePoint}");
                }
                for(var extension=0;extension<2;extension++)
                {
                    var interval=nativeRange;if(extension!=0){interval.value[0]-=(nativeRange.value[1]-nativeRange.value[0])*1e-6;interval.value[1]+=(nativeRange.value[1]-nativeRange.value[0])*1e-6;}
                    var opt=new PK_SURF_intersect_curve_o_t();int n;PK_VECTOR_t* vectors;PK_UV_t* uv;double* ts;int* types;
                    var error=PK_SURF_intersect_curve(nativeSide,nativeCurve,interval,&opt,&n,&vectors,&uv,&ts,&types);
                    Console.WriteLine($"NATIVE_BOUNDARY extension={extension} error={error} count={(error==0?n.ToString():"unavailable")}");
                    if(error!=0)continue;
                    try{for(var j=0;j<n;j++){double parameter=0;var inverse=PK_CURVE_parameterise_vector(nativeCurve,vectors[j],&parameter);Console.WriteLine($"NATIVE_ROOT t={ts[j]:R} inverse_error={inverse} inverse={(inverse==0?parameter.ToString("R"):"unavailable")} point={vectors[j].coord[0]:R}/{vectors[j].coord[1]:R}/{vectors[j].coord[2]:R} type={types[j]}");}}
                    finally{if(vectors!=null)PK_MEMORY_free(vectors);if(uv!=null)PK_MEMORY_free(uv);if(ts!=null)PK_MEMORY_free(ts);if(types!=null)PK_MEMORY_free(types);}
                }
            }
        }
        finally{if(points!=null)KernelRuntime.MemoryFree(points);if(curves!=null)KernelRuntime.MemoryFree(curves);if(ranges!=null)KernelRuntime.MemoryFree(ranges);if(kinds!=null)KernelRuntime.MemoryFree(kinds);}
        Console.WriteLine("DIAGNOSTIC_COMPLETE; no Boolean acceptance asserted");
    }
    finally{Check(KernelRuntime.SessionStop(),"GM stop");}
}
static unsafe int Roof(int body)
{
    Require(KernelRuntime.TryResolveBodySlot(body,out var slot),"own body");var face=KernelRuntime.Bodies[slot].FirstFaceBody;var answer=0;
    for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
    {
        var tag=KernelRuntime.Faces[face].SurfTag;var record=KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(tag)];
        if(record.Class!=SurfaceClass.BSurface)continue;var data=KernelRuntime.BSurfaceDataStore[record.DataIndex];
        if(data.UDegree<=1||data.VDegree<=1)continue;Require(answer==0,"one curved support");answer=tag;
    }
    Require(answer!=0,"curved native BSurface");return answer;
}
static unsafe int LowerYSide(int body)
{
    Require(KernelRuntime.TryResolveBodySlot(body,out var slot),"own body");var face=KernelRuntime.Bodies[slot].FirstFaceBody;var answer=0;var lower=double.PositiveInfinity;
    for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
    {
        var tag=KernelRuntime.Faces[face].SurfTag;M.PK_BSURF_sf_s s;Check(KernelRuntime.BSurfAsk(tag,&s),"source BSurface");
        try{var constant=true;var y=s.vertex[1];for(var k=0;k<s.n_u_vertices*s.n_v_vertices;k++)constant&=s.vertex[s.vertex_dim*k+1]==y;if(constant&&y<lower){lower=y;answer=tag;}}
        finally{Free(s);}
    }
    Require(answer!=0,"native finite side support");return answer;
}
static unsafe void Free(M.PK_BSURF_sf_s s)
{if(s.vertex!=null)KernelRuntime.MemoryFree(s.vertex);if(s.u_knot!=null)KernelRuntime.MemoryFree(s.u_knot);if(s.v_knot!=null)KernelRuntime.MemoryFree(s.v_knot);if(s.u_knot_mult!=null)KernelRuntime.MemoryFree(s.u_knot_mult);if(s.v_knot_mult!=null)KernelRuntime.MemoryFree(s.v_knot_mult);}
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
static unsafe int Receive(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(PK_PART_receive_b(block,&options,&n,&parts),"real PK receive");try{Require(n==1,"one received solid");return parts[0];}finally{PK_MEMORY_free(parts);}
    }
}
static unsafe int ReceiveOwn(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new M.PK_MEMORY_block_s{bytes=p,n_bytes=(nuint)bytes.Length};var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(KernelRuntime.PartReceiveB(block,&options,&n,&parts),"self receive");try{Require(n==1,"one self-received solid");return parts[0];}finally{KernelRuntime.MemoryFree(parts);}
    }
}

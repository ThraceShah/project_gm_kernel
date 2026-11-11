#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property TargetFramework=net10.0
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

// Diagnostic native API behavior only: batch wall time includes result
// logging and returned-buffer frees and is not a complete-operation benchmark.
var root=Path.GetDirectoryName(ThisFile())!;
var input=Path.GetFullPath(Path.Combine(root,args.Length==0?"../temp_docs/short-edge-blend-oracle/integrated-root-shape/a1-r0.5-t1E-07-reverseFalse-default-self.x_t":args[0]));
var bytes=File.ReadAllBytes(input);
if(!ParasolidScriptHost.TryStartSession("Native finite ICurve / plane query",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"version");
    Console.WriteLine($"PK={version.major_revision}.{version.minor_revision}.{version.build_number} input={Path.GetRelativePath(root,input)} sha256={Convert.ToHexStringLower(SHA256.HashData(bytes))}");
    int body;
    fixed(byte* data=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,data);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(PK_PART_receive_b(block,&options,&n,&parts),"receive");
        try{if(n!=1)throw new Exception("Expected one actual body");body=parts[0];}finally{Check(PK_MEMORY_free(parts),"free parts");}
    }
    int count;int* edges;Check(PK_BODY_ask_edges(body,&count,&edges),"edges");
    try
    {
        PK_VECTOR_t* ends=stackalloc PK_VECTOR_t[2];
        for(var i=0;i<count;i++)
        {
            int curve,kind;PK_INTERVAL_t interval;PK_LOGICAL_t sense;
            Check(PK_EDGE_ask_geometry(edges[i],PK_LOGICAL_true,&curve,&kind,ends,&interval,&sense),"edge geometry");
            if(kind!=PK_CLASS_icurve)continue;
            var middle=new PK_VECTOR_t();Check(PK_CURVE_eval(curve,(interval.value[0]+interval.value[1])*.5,0,&middle),"native midpoint");
            for(var axis=0;axis<3;axis++)foreach(var shift in new[]{-.25,0,.25})
            {
                var basis=new PK_AXIS2_sf_t();for(var c=0;c<3;c++)basis.location.coord[c]=middle.coord[c];basis.location.coord[axis]+=shift;
                basis.axis.coord[axis]=1;basis.ref_direction.coord[axis==0?1:0]=1;
                var definition=new PK_PLANE_sf_t(basis);int plane;Check(PK_PLANE_create(&definition,&plane),"plane");
                try
                {
                    var options=new PK_SURF_intersect_curve_o_t();var total=0;var completed=0;var queryError=0;var started=Stopwatch.GetTimestamp();
                    for(var repeat=0;repeat<8;repeat++)
                    {
                        int n=0;PK_VECTOR_t* points=null;PK_UV_t* uv=null;double* t=null;PK_intersect_vector_t* types=null;
                        queryError=PK_SURF_intersect_curve(plane,curve,interval,&options,&n,&points,&uv,&t,&types);
                        if(queryError!=0)break;
                        try
                        {
                            completed++;total+=n;
                            if(repeat==0)for(var k=0;k<n;k++)Console.WriteLine($"NATIVE_ROOT curve={curve} axis={axis} shift={shift:R} t={t[k]:R} type={types[k]}");
                        }
                        finally{if(points!=null)Check(PK_MEMORY_free(points),"free points");if(uv!=null)Check(PK_MEMORY_free(uv),"free uv");if(t!=null)Check(PK_MEMORY_free(t),"free parameters");if(types!=null)Check(PK_MEMORY_free(types),"free classifications");}
                    }
                    Console.WriteLine($"FINITE_NATIVE edge={edges[i]} curve={curve} axis={axis} shift={shift:R} range=[{interval.value[0]:R},{interval.value[1]:R}] calls={completed} error={queryError} roots={total} batch-wall-ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F4}");
                }
                finally{Check(PK_ENTITY_delete(1,&plane),"delete plane");}
            }
        }
    }
    finally{Check(PK_MEMORY_free(edges),"free edges");Check(PK_ENTITY_delete(1,&body),"delete actual receive");}
}
static void Check(int error,string operation){if(error!=0)throw new Exception(operation+" error="+error);}
static string ThisFile([CallerFilePath]string path="")=>path;

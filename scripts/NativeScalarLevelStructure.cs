#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property TargetFramework=net10.0
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
var script=Path.GetDirectoryName(ThisFile())!;var root=Path.GetFullPath(Path.Combine(script,".."));var output=Path.Combine(root,"temp_docs/pk-native-scalar-intersections.json");if(File.Exists(output))throw new IOException("Preserve original observation");
var inputs=new[]{("gm","../../gm-pk-isoparametric-bound-work/temp_docs/isoparam-controlled/window/17-shortedge-candidate/products/a1-r0.5-t1E-07-reverseFalse-default-self.x_t"),("pk","../../gm-pk-isoparametric-bound-work/temp_docs/isoparam-controlled/window/17-shortedge-candidate/products/a1-r0.5-t1E-07-reverseFalse-default-native.x_t")};var rows=new JsonArray();var sources=new JsonArray();
if(!ParasolidScriptHost.TryStartSession("Finite PK native section intersections",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"version");PK_VECTOR_t* eval=stackalloc PK_VECTOR_t[1];
    foreach(var (label,relative) in inputs)
    {
        var input=Path.GetFullPath(Path.Combine(script,relative));var bytes=File.ReadAllBytes(input);int body;
        fixed(byte* ptr=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,ptr);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;int* parts;Check(PK_PART_receive_b(block,&options,&count,&parts),"receive");try{if(count!=1)throw new Exception("one body required");body=parts[0];}finally{Check(PK_MEMORY_free(parts),"parts");}}
        int nf;int* faces;Check(PK_BODY_ask_faces(body,&nf,&faces),"faces");var surfaces=new List<(int Surf,int Kind)>();
        try
        {
            for(var i=0;i<nf;i++){int surface,kind;Check(PK_FACE_ask_surf(faces[i],&surface),"surface");Check(PK_ENTITY_ask_class(surface,&kind),"class");surfaces.Add((surface,kind));}
            for(var i=0;i<nf;i++)
            {
                var (blend,kind)=surfaces[i];if(kind!=PK_CLASS_blendsf)continue;PK_UVBOX_t box;Check(PK_FACE_find_uvbox(faces[i],&box),"UV box");
                foreach(var fraction in new[]{.25,.5,.75})foreach(var preference in new[]{PK_isoparam_prefer_simple_c,PK_isoparam_prefer_spcurve_c})
                {
                    var at=box.param[0]+fraction*(box.param[2]-box.param[0]);var make=new PK_SURF_make_curve_isoparam_o_t(preference,PK_LOGICAL_true);int curve=0;PK_INTERVAL_t range;var create=PK_SURF_make_curve_isoparam(blend,at,PK_PARAM_direction_u_c,&make,&curve,&range);int curveClass=0;if(create==0)Check(PK_ENTITY_ask_class(curve,&curveClass),"curve class");
                    foreach(var (surface,targetKind) in surfaces)
                    {
                        if(targetKind is not (PK_CLASS_plane or PK_CLASS_cyl or PK_CLASS_sphere))continue;
                        int count=0;PK_VECTOR_t* points=null;PK_UV_t* uv=null;double* t=null;PK_intersect_vector_t* types=null;var options=new PK_SURF_intersect_curve_o_t();var error=create==0?PK_SURF_intersect_curve(surface,curve,range,&options,&count,&points,&uv,&t,&types):-1;var intersections=new JsonArray();
                        try
                        {
                            if(error==0)for(var j=0;j<count;j++)
                            {
                                var curveError=PK_CURVE_eval(curve,t[j],0,eval);double? curveResidual=curveError==0?Distance(points[j],eval[0]):null;int? blendError=null;double? nativeResidual=null;
                                if(preference==PK_isoparam_prefer_spcurve_c){var native=new PK_UV_t(at,t[j]);blendError=PK_SURF_eval(blend,native,0,0,PK_LOGICAL_false,eval);if(blendError==0)nativeResidual=Distance(points[j],eval[0]);}
                                intersections.Add((JsonNode)new JsonObject{["curveParameter"]=t[j],["classification"]=types[j],["point"]=Numbers(points[j].coord[0],points[j].coord[1],points[j].coord[2]),["curveEvalError"]=curveError,["curvePointResidual"]=curveResidual,["nativeBlendEvalError"]=blendError,["nativeBlendPointResidual"]=nativeResidual,["surfaceUV"]=types[j] is PK_intersect_vector_simple_c or PK_intersect_vector_tangent_c?Numbers(uv[j].param[0],uv[j].param[1]):null});
                            }
                        }
                        finally{if(points!=null)Check(PK_MEMORY_free(points),"points");if(uv!=null)Check(PK_MEMORY_free(uv),"uv");if(t!=null)Check(PK_MEMORY_free(t),"parameters");if(types!=null)Check(PK_MEMORY_free(types),"types");}
                        rows.Add((JsonNode)new JsonObject{["source"]=label,["blend"]=blend,["fixedNativeU"]=at,["preference"]=preference,["curveClass"]=curveClass,["createError"]=create,["returnedCurvePortion"]=create==0?Numbers(range.value[0],range.value[1]):null,["targetSurface"]=surface,["targetClass"]=targetKind,["intersectionError"]=error,["count"]=error==0?count:null,["intersections"]=intersections});
                    }
                }
            }
        }
        finally{Check(PK_MEMORY_free(faces),"faces memory");Check(PK_ENTITY_delete(1,&body),"delete source");}
        sources.Add((JsonNode)new JsonObject{["source"]=label,["path"]=Path.GetRelativePath(root,input),["sha256"]=Convert.ToHexStringLower(SHA256.HashData(bytes)),["faceCount"]=nf});
    }
    File.WriteAllText(output,new JsonObject{["scope"]="Real PK finite returned curve portions against original body analytic surfaces. Individual API errors, counts, classes and parameters retained. Driver0 only records observations. SP native-v mapping is checked at each returned root; no sampled proof, replacement model or performance claim.",["version"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}",["inputs"]=sources,["rows"]=rows}.ToJsonString());
}
Console.WriteLine($"Recorded {rows.Count} finite section/surface API requests");
static unsafe double Distance(PK_VECTOR_t a,PK_VECTOR_t b){var x=a.coord[0]-b.coord[0];var y=a.coord[1]-b.coord[1];var z=a.coord[2]-b.coord[2];return Math.Sqrt(x*x+y*y+z*z);}
static JsonArray Numbers(params double[] values)=>new(values.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray());
static void Check(int error,string label){if(error!=0)throw new Exception(label+" error="+error);}
static string ThisFile([CallerFilePath]string path="")=>path;

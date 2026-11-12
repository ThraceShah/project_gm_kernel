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
var script=Path.GetDirectoryName(ThisFile())!;var root=Path.GetFullPath(Path.Combine(script,".."));var output=Path.Combine(root,"temp_docs/native-sections/pk-isoparam-corrected.json");if(File.Exists(output))throw new IOException("Preserve original PK observation");Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var inputs=new[]{("gm-short","../../gm-pk-projection-bound-work/temp_docs/projection-controlled/window/17-shortedge-candidate/products/a1-r0.5-t1E-07-reverseFalse-default-self.x_t"),("pk-short","../../gm-pk-projection-bound-work/temp_docs/projection-controlled/window/17-shortedge-candidate/products/a1-r0.5-t1E-07-reverseFalse-default-native.x_t")};var rows=new JsonArray();var bodies=new JsonArray();
if(!ParasolidScriptHost.TryStartSession("Native section type and original parameter correspondence",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"version");
    foreach(var (label,relative) in inputs)
    {
        var input=Path.GetFullPath(Path.Combine(script,relative));var bytes=File.ReadAllBytes(input);int body;
        fixed(byte* ptr=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,ptr);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;int* parts;Check(PK_PART_receive_b(block,&options,&count,&parts),"receive");try{if(count!=1)throw new Exception("expected one real source body");body=parts[0];}finally{Check(PK_MEMORY_free(parts),"free parts");}}
        int nf;int* faces;Check(PK_BODY_ask_faces(body,&nf,&faces),"faces");var classes=new Dictionary<int,int>();
        try
        {
            PK_VECTOR_t* surfaceValues=stackalloc PK_VECTOR_t[2];PK_VECTOR_t* curveValues=stackalloc PK_VECTOR_t[2];
            for(var fi=0;fi<nf;fi++)
            {
                int surf,kind;Check(PK_FACE_ask_surf(faces[fi],&surf),"face surface");Check(PK_ENTITY_ask_class(surf,&kind),"surface type");classes[kind]=classes.GetValueOrDefault(kind)+1;if(kind!=PK_CLASS_blendsf)continue;
                PK_UVBOX_t faceBox,surfBox;Check(PK_FACE_find_uvbox(faces[fi],&faceBox),"face UV");Check(PK_SURF_ask_uvbox(surf,&surfBox),"surface UV");
                for(var axis=0;axis<2;axis++)foreach(var fraction in new[]{.25,.5,.75})foreach(var preference in new[]{PK_isoparam_prefer_simple_c,PK_isoparam_prefer_spcurve_c})
                {
                    var at=faceBox.param[axis]+fraction*(faceBox.param[axis+2]-faceBox.param[axis]);var options=new PK_SURF_make_curve_isoparam_o_t(preference,PK_LOGICAL_true);var direction=axis==0?PK_PARAM_direction_u_c:PK_PARAM_direction_v_c;int curve=0;PK_INTERVAL_t range;var error=PK_SURF_make_curve_isoparam(surf,at,direction,&options,&curve,&range);var samples=new JsonArray();int curveClass=0;
                    if(error==0)
                    {
                        Check(PK_ENTITY_ask_class(curve,&curveClass),"curve type");
                        foreach(var f in new[]{0.0,.25,.5,.75,1.0})
                        {
                            var free=faceBox.param[1-axis]+f*(faceBox.param[3-axis]-faceBox.param[1-axis]);var uv=new PK_UV_t();uv.param[axis]=at;uv.param[1-axis]=free;
                            var eval=PK_SURF_eval(surf,uv,axis==1?1:0,axis==0?1:0,PK_LOGICAL_false,surfaceValues);double t=0;var inverse=eval==0?PK_CURVE_parameterise_vector(curve,surfaceValues[0],&t):-1;var mapped=inverse==0?PK_CURVE_eval(curve,t,1,curveValues):-1;double? distance=null,scale=null,derivativeResidual=null;
                            if(mapped==0)
                            {
                                distance=Norm(Sub(surfaceValues[0],curveValues[0]));var denominator=Dot(curveValues[1],curveValues[1]);if(denominator>0){scale=Dot(surfaceValues[1],curveValues[1])/denominator;derivativeResidual=Norm(Sub(surfaceValues[1],Scale(curveValues[1],scale.Value)));}
                            }
                            var same=eval==0?PK_CURVE_eval(curve,free,0,curveValues):-1;double? sameParameterDistance=same==0?Norm(Sub(surfaceValues[0],curveValues[0])):null;
                            samples.Add((JsonNode)new JsonObject{["nativeFreeParameter"]=free,["surfaceError"]=eval,["inverseError"]=inverse,["curveEvalError"]=mapped,["curveParameter"]=inverse==0?t:null,["positionDistance"]=distance,["dtDnativeFree"]=scale,["firstDerivativeResidual"]=derivativeResidual,["sameParameterEvalError"]=same,["sameParameterDistance"]=sameParameterDistance});
                        }
                    }
                    rows.Add((JsonNode)new JsonObject{["source"]=label,["face"]=faces[fi],["surface"]=surf,["axisConstant"]=axis,["at"]=at,["faceUV"]=Numbers(faceBox.param[0],faceBox.param[1],faceBox.param[2],faceBox.param[3]),["surfaceUV"]=Numbers(surfBox.param[0],surfBox.param[1],surfBox.param[2],surfBox.param[3]),["preference"]=preference,["createError"]=error,["curve"]=curve,["curveClass"]=curveClass,["curvePortion"]=error==0?Numbers(range.value[0],range.value[1]):null,["samples"]=samples});
                }
            }
        }
        finally{Check(PK_MEMORY_free(faces),"free faces");Check(PK_ENTITY_delete(1,&body),"delete source body");}
        var histogram=new JsonObject();foreach(var pair in classes)histogram[pair.Key.ToString()]=pair.Value;bodies.Add((JsonNode)new JsonObject{["source"]=label,["path"]=Path.GetRelativePath(root,input),["sha256"]=Convert.ToHexStringLower(SHA256.HashData(bytes)),["surfaceClasses"]=histogram,["faces"]=nf});
    }
    File.WriteAllText(output,new JsonObject{["scope"]="Real PK on accepted cc original GM and independent PK final XT. Finite returned curve portions and original UV kept separately. Point/derivative samples are observations, not full equivalence, a parameter definition or performance evidence. No model modification or fitted substitute.",["version"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}",["inputs"]=bodies,["rows"]=rows}.ToJsonString());
}
Console.WriteLine($"Recorded {rows.Count} real PK finite isoparametric curve constructions");
static JsonArray Numbers(params double[] values)=>new(values.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray());
static unsafe PK_VECTOR_t Sub(PK_VECTOR_t a,PK_VECTOR_t b){var x=new PK_VECTOR_t();for(var i=0;i<3;i++)x.coord[i]=a.coord[i]-b.coord[i];return x;}
static unsafe PK_VECTOR_t Scale(PK_VECTOR_t a,double factor){for(var i=0;i<3;i++)a.coord[i]*=factor;return a;}
static unsafe double Dot(PK_VECTOR_t a,PK_VECTOR_t b)=>a.coord[0]*b.coord[0]+a.coord[1]*b.coord[1]+a.coord[2]*b.coord[2];
static unsafe double Norm(PK_VECTOR_t a)=>Math.Sqrt(Dot(a,a));
static void Check(int error,string operation){if(error!=0)throw new Exception(operation+" error="+error);}
static string ThisFile([CallerFilePath]string path="")=>path;

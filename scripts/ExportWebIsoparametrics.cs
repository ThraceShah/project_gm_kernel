#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using static parasolid;
using BodyTag = System.Int32;
using FaceTag = System.Int32;
using SurfaceTag = System.Int32;
using PersistentIdentifier = System.Int32;
using EntityClass = System.Int32;

var script = ScriptPath();
var scriptDirectory = Path.GetDirectoryName(script)!;
var root = Path.GetFullPath("..", scriptDirectory);
try
{
    if (args.Contains("--help"))
    {
        Console.WriteLine("dotnet run scripts/ExportWebIsoparametrics.cs -- --snapshot <receipt.json> --output <new.iso.json> [--chord-error 0.001] [--count 5]\nPaths resolve relative to scripts/. Receipt paths resolve relative to its directory. Requires same-capture XT/mesh receipt; never reconstructs bodies or writes historical runs. Only PK_FACE_is_uvbox-confirmed nonplanar faces are supported. Other trims are explicitly unsupported.");
        return 0;
    }
    var parsed = new Dictionary<string,string>(StringComparer.Ordinal);
    for (var i=0;i<args.Length;i+=2)
    {
        if(i+1==args.Length || args[i] is not ("--snapshot" or "--output" or "--chord-error" or "--count") || !parsed.TryAdd(args[i],args[i+1]))
            throw new ArgumentException("Expected unique --snapshot, --output, --chord-error, --count options.");
    }
    var receiptPath=Path.GetFullPath(parsed["--snapshot"],scriptDirectory);
    var output=Path.GetFullPath(parsed["--output"],scriptDirectory);
    var tolerance=double.Parse(parsed.GetValueOrDefault("--chord-error","0.001"),CultureInfo.InvariantCulture);
    var count=int.Parse(parsed.GetValueOrDefault("--count","5"),CultureInfo.InvariantCulture);
    if(!double.IsFinite(tolerance)||tolerance<=0||count is <1 or >64)throw new ArgumentException("Positive finite chord error and count 1..64 required.");
    if(File.Exists(output)||output.StartsWith(Path.Combine(root,"web","data","runs")+Path.DirectorySeparatorChar,StringComparison.Ordinal))
        throw new IOException("Output must be a new render-only derivative outside immutable runs/.");
    var receipt=JsonNode.Parse(File.ReadAllBytes(receiptPath))!.AsObject();
    var receiptDirectory=Path.GetDirectoryName(receiptPath)!;
    var meshPath=Resolve(receiptDirectory,Required(receipt,"meshPath"));
    var xtPath=Resolve(receiptDirectory,Required(receipt,"xtPath"));
    var meshBytes=File.ReadAllBytes(meshPath);var xtBytes=File.ReadAllBytes(xtPath);
    if(Hash(meshBytes)!=Required(receipt,"meshSha256")||Hash(xtBytes)!=Required(receipt,"xtSha256"))
        throw new InvalidDataException("Snapshot receipt SHA does not match actual mesh/XT bytes.");
    var mesh=JsonNode.Parse(meshBytes)!.AsObject();
    var kernel=Required(receipt,"kernel");
    if(kernel is not ("GM" or "PK")||mesh["source"]?["kernel"]?.GetValue<string>()!=kernel)
        throw new InvalidDataException("Snapshot kernel identity mismatch.");
    foreach(var key in new[]{"sourceRun","sourceSnapshot","caseId","stepId","parameterHash","captureCodeSha256"})_ = Required(receipt,key);
    // The capture-time link is required in the mesh as well as the receipt. A name/identifier alone is never provenance.
    if(mesh["source"]?["snapshotXtSha256"]?.GetValue<string>()!=Hash(xtBytes))
        throw new InvalidDataException("Mesh has no matching capture-time snapshotXtSha256 binding.");
    if(!ParasolidScriptHost.TryStartSession("verified web UV display",out var session,out var reason))
        throw new InvalidOperationException("ENV_NOT_READY: "+reason);
    using(session)unsafe
    {
        var body=Receive(xtBytes);
        try
        {
            var mapped=ValidateMesh(body,mesh);
            var faces=new JsonArray();var unsupported=new JsonArray();
            foreach(var (face,meshFace) in mapped)
            {
                SurfaceTag surface=0;Check(PK_FACE_ask_surf(face,&surface),"face surface");
                EntityClass surfaceClass=0;Check(PK_ENTITY_ask_class(surface,&surfaceClass),"surface class");
                PK_LOGICAL_t rectangular=PK_LOGICAL_false;PK_UVBOX_t bounds=default;
                Check(PK_FACE_is_uvbox(face,&rectangular,&bounds),"exact rectangular trim classification");
                if(surfaceClass==PK_CLASS_plane||rectangular!=PK_LOGICAL_true)
                {
                    unsupported.Add((JsonNode)new JsonObject{["faceId"]=meshFace["id"]!.DeepClone(),["identifier"]=meshFace["identifier"]!.DeepClone(),["reason"]=surfaceClass==PK_CLASS_plane?"Planar face omitted":"Nonrectangular trim unsupported; no approximate trim or outline substituted"});
                    continue;
                }
                // PK_UVBOX order: u min, v min, u max, v max. The API confirms the trimmed face itself equals this rectangle.
                var u0=bounds.param[0];var v0=bounds.param[1];var u1=bounds.param[2];var v1=bounds.param[3];
                if(!(u1>u0&&v1>v0)||!new[]{u0,v0,u1,v1}.All(double.IsFinite))throw new InvalidDataException("Invalid finite UV rectangle.");
                var uLines=new JsonArray();var vLines=new JsonArray();var evidence=new JsonArray();
                var evaluatedSurface=surface;
                foreach(var direction in new[]{"u","v"})
                for(var k=1;k<=count;k++)
                {
                    var parameter=(direction=="u"?u0:v0)+(direction=="u"?u1-u0:v1-v0)*k/(count+1.0);
                    var low=direction=="u"?v0:u0;var high=direction=="u"?v1:u1;
                    Point At(double varying)=>Evaluate(evaluatedSurface,face,direction=="u"?parameter:varying,direction=="u"?varying:parameter);
                    var samples=new List<(double T,Point P)>{(low,At(low))};var maxError=0.0;
                    void Subdivide(double a,Point pa,double b,Point pb,int depth)
                    {
                        var mid=(a+b)/2;var pm=At(mid);
                        var quarter=At(a+(b-a)*.25);var threeQuarter=At(a+(b-a)*.75);
                        var error=Math.Max(DistanceToSegment(pm,pa,pb),Math.Max(DistanceToSegment(quarter,pa,pb),DistanceToSegment(threeQuarter,pa,pb)));
                        if(error>tolerance)
                        {
                            if(depth>=24)throw new InvalidDataException("Chord-error subdivision limit reached.");
                            Subdivide(a,pa,mid,pm,depth+1);Subdivide(mid,pm,b,pb,depth+1);
                        }
                        else {maxError=Math.Max(maxError,error);samples.Add((b,pb));}
                    }
                    // Initial subdivisions protect against a whole-span symmetric sample alias; still a sampled display error, not an analytic bound.
                    for(var segment=0;segment<16;segment++)
                    {
                        var a=low+(high-low)*segment/16;var b=low+(high-low)*(segment+1)/16;
                        Subdivide(a,At(a),b,At(b),0);
                    }
                    var xyz=new JsonArray();var uv=new JsonArray();
                    foreach(var (t,p) in samples)
                    {
                        xyz.Add((JsonNode?)JsonValue.Create(p.X));xyz.Add((JsonNode?)JsonValue.Create(p.Y));xyz.Add((JsonNode?)JsonValue.Create(p.Z));
                        uv.Add((JsonNode?)JsonValue.Create(direction=="u"?parameter:t));uv.Add((JsonNode?)JsonValue.Create(direction=="u"?t:parameter));
                    }
                    (direction=="u"?uLines:vLines).Add((JsonNode)xyz);
                    evidence.Add((JsonNode)new JsonObject{["direction"]=direction,["constantParameter"]=parameter,["varyingRange"]=Numbers(low,high),["uvSamples"]=uv,["sampledChordError"]=maxError,["trimCheck"]="PK_FACE_is_uvbox=true; every evaluated UV including endpoints and chord probes checked by PK_FACE_contains_vectors"});
                }
                faces.Add((JsonNode)new JsonObject{["faceId"]=meshFace["id"]!.DeepClone(),["identifier"]=meshFace["identifier"]!.DeepClone(),["meshEntityTag"]=meshFace["entityTag"]!.DeepClone(),["receivedFaceTag"]=face,["surfaceClass"]=surfaceClass,["trimUvBox"]=Numbers(u0,v0,u1,v1),["trimMethod"]="PK_FACE_is_uvbox confirmed exact face rectangle",["uLines"]=uLines,["vLines"]=vLines,["curves"]=evidence,["source"]="PK_SURF_eval on capture XT; U means constant u, V means constant v"});
            }
            PK_SESSION_kernel_version_t version=default;Check(PK_SESSION_ask_kernel_version(&version),"PK runtime version");
            var result=new JsonObject
            {
                ["schemaVersion"]=2,["type"]="render-only-isoparametrics-derivative",["status"]=faces.Count>0?"available":"unsupported",
                ["sourceRun"]=receipt["sourceRun"]!.DeepClone(),["sourceSnapshot"]=receipt["sourceSnapshot"]!.DeepClone(),["kernel"]=kernel,
                ["caseId"]=receipt["caseId"]!.DeepClone(),["stepId"]=receipt["stepId"]!.DeepClone(),["parameterHash"]=receipt["parameterHash"]!.DeepClone(),
                ["sourceMeshSha256"]=Hash(meshBytes),["inputXtSha256"]=Hash(xtBytes),["snapshotReceiptSha256"]=Hash(File.ReadAllBytes(receiptPath)),
                ["captureCodeSha256"]=receipt["captureCodeSha256"]!.DeepClone(),["exporterSha256"]=Hash(File.ReadAllBytes(script)),
                ["pkVersion"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}",["generatedAt"]=DateTimeOffset.UtcNow.ToString("O"),
                ["chordTolerance"]=tolerance,["errorMethod"]="Quarter/midpoint sampled chord deviation with adaptive subdivision; no global analytic error bound claimed",
                ["source"]="scripts/ExportWebIsoparametrics.cs; display only, excluded from primary acceptance",["faces"]=faces,["unsupportedFaces"]=unsupported
            };
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);
            using var writer=new Utf8JsonWriter(stream);result.WriteTo(writer);
            Console.WriteLine($"DISPLAY_ONLY {kernel}: {faces.Count} supported faces, {unsupported.Count} omitted; XT {Hash(xtBytes)} mesh {Hash(meshBytes)} output {output}");
        }
        finally{Check(PK_ENTITY_delete(1,&body),"delete received snapshot");}
    }
    return 0;
}
catch(Exception ex){Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message);return 1;}

static string ScriptPath([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static string Required(JsonObject obj,string name)=>obj[name]?.GetValue<string>() is {Length:>0} value?value:throw new InvalidDataException("Required snapshot field: "+name);
static string Resolve(string directory,string relative)
{
    if(Path.IsPathRooted(relative))throw new InvalidDataException("Snapshot paths must be relative.");
    return Path.GetFullPath(relative,directory);
}
static JsonArray Numbers(params double[] values)=>new(values.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray());
static void Check(PK_ERROR_code_t error,string operation){if(error!=0)throw new InvalidOperationException($"{operation}: PK error {error}");}
static unsafe BodyTag Receive(byte[] bytes)
{
    fixed(byte* data=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,data);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};
        var count=0;BodyTag* parts=null;Check(PK_PART_receive_b(block,&options,&count,&parts),"receive actual snapshot XT");
        try{if(count!=1||parts==null)throw new InvalidDataException("Expected exactly one snapshot body.");return parts[0];}finally{PK_MEMORY_free(parts);}
    }
}
static unsafe List<(FaceTag Face,JsonObject MeshFace)> ValidateMesh(BodyTag body,JsonObject mesh)
{
    var count=0;FaceTag* faces=null;Check(PK_BODY_ask_faces(body,&count,&faces),"snapshot faces");
    try
    {
        var originals=mesh["faces"]!.AsArray();if(originals.Count!=count)throw new InvalidDataException("Snapshot/mesh face count mismatch.");
        var byIdent=new Dictionary<PersistentIdentifier,FaceTag>();
        for(var i=0;i<count;i++){PersistentIdentifier ident=0;Check(PK_ENTITY_ask_identifier(faces[i],&ident),"face persistent ID");if(ident<=0||!byIdent.TryAdd(ident,faces[i]))throw new InvalidDataException("Ambiguous XT face identity.");}
        var seen=new HashSet<PersistentIdentifier>();var mapped=new List<(FaceTag,JsonObject)>();
        foreach(var node in originals)
        {
            var face=node!.AsObject();var ident=face["identifier"]!.GetValue<PersistentIdentifier>();
            if(!seen.Add(ident)||!byIdent.TryGetValue(ident,out var tag))throw new InvalidDataException("Missing or duplicate mesh face identity.");
            SurfaceTag surf=0;Check(PK_FACE_ask_surf(tag,&surf),"mapping surface");
            var positions=face["positions"]!.AsArray();if(positions.Count<9||positions.Count%3!=0)throw new InvalidDataException("Invalid mesh vertices.");
            for(var i=0;i<positions.Count;i+=3)
            {
                var p=new PK_VECTOR_t(positions[i]!.GetValue<double>(),positions[i+1]!.GetValue<double>(),positions[i+2]!.GetValue<double>());PK_UV_t uv=default;
                Check(PK_SURF_parameterise_vector(surf,p,&uv),"mesh vertex parameterisation");
                var actual=Evaluate(surf,tag,uv.param[0],uv.param[1]);
                // DisplayBridge stores float32 mesh positions; this checks float conversion only, never a geometric acceptance threshold.
                var scale=1+Math.Max(Math.Abs(p.coord[0]),Math.Max(Math.Abs(p.coord[1]),Math.Abs(p.coord[2])));
                if(Distance(actual,new(p.coord[0],p.coord[1],p.coord[2]))>scale*2e-6)throw new InvalidDataException("Mesh vertex does not match capture XT face.");
            }
            mapped.Add((tag,face));
        }
        return mapped;
    }
    finally{PK_MEMORY_free(faces);}
}
static unsafe Point Evaluate(SurfaceTag surface,FaceTag face,double u,double v)
{
    var uv=new PK_UV_t(u,v);var options=new PK_FACE_contains_vectors_o_t{n_uvs=1,uvs=&uv};var topol=0;
    Check(PK_FACE_contains_vectors(face,&options,&topol),"trim containment");
    if(topol==0)throw new InvalidDataException($"UV ({u:R},{v:R}) outside trimmed face {face}.");
    PK_VECTOR_t p=default;Check(PK_SURF_eval(surface,uv,0,0,PK_LOGICAL_false,&p),"true UV surface evaluation");
    if(!double.IsFinite(p.coord[0])||!double.IsFinite(p.coord[1])||!double.IsFinite(p.coord[2]))throw new InvalidDataException("Nonfinite surface point.");
    return new(p.coord[0],p.coord[1],p.coord[2]);
}
static double Distance(Point a,Point b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)+(a.Z-b.Z)*(a.Z-b.Z));
static double DistanceToSegment(Point p,Point a,Point b)
{
    var x=b.X-a.X;var y=b.Y-a.Y;var z=b.Z-a.Z;var length=x*x+y*y+z*z;
    var t=length>0?Math.Clamp(((p.X-a.X)*x+(p.Y-a.Y)*y+(p.Z-a.Z)*z)/length,0,1):0;
    return Distance(p,new(a.X+t*x,a.Y+t*y,a.Z+t*z));
}
readonly record struct Point(double X,double Y,double Z);

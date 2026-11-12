using System.IO.Compression;
using System.Text.Json.Nodes;

// One security contract for the validator and every static copy. The boundary
// is the canonical root: paths are normalized with the platform's canonical
// rules (GetFullPath/GetRelativePath, never string-prefix comparisons) and
// every filesystem component from the volume root down to the referenced
// entry — the root itself, its ancestors, intermediate directories and the
// leaf — must be a real entry: symlinks and other reparse points are rejected
// everywhere, so source roots, destination roots and referenced files can
// never escape through links.
internal static class WebProgressSafePath
{
    internal static bool IsSafeChain(string fullPath, out string? offending)
    {
        offending = null;
        var normalized = Path.GetFullPath(fullPath);
        var volume = Path.GetPathRoot(normalized) ?? "";
        var current = volume;
        foreach (var segment in Path.GetRelativePath(volume, normalized).Split(new[] { '/', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileAttributes attributes;
            try { attributes = File.GetAttributes(current); }
            catch (FileNotFoundException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            if ((attributes & FileAttributes.ReparsePoint) != 0) { offending = current; return false; }
        }
        return true;
    }

    internal static bool IsWithin(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(fullPath));
        return !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith("../", StringComparison.Ordinal) && !relative.StartsWith("..\\", StringComparison.Ordinal);
    }

    internal static bool TryResolve(string root, string relative, out string path, out string? offending)
    {
        path = "";
        offending = null;
        var canonicalRoot = Path.GetFullPath(root);
        if (!IsSafeChain(canonicalRoot, out offending)) return false;
        var combined = Path.GetFullPath(relative, canonicalRoot);
        if (!IsWithin(canonicalRoot, combined)) { offending = relative; return false; }
        if (!IsSafeChain(combined, out offending)) return false;
        path = combined;
        return true;
    }
}

internal static class WebProgressDataValidation
{
    private static readonly HashSet<string> Statuses=["pass","fail","missing","unsupported","timeout","diagnostic"];
    private static readonly HashSet<string> Roles=["unchanged","selected","support","newBlend","modified","deleted"];
    internal static JsonObject Validate(string data) => Validate(data, null);
    // indexOverride validates a candidate destination index before it is
    // atomically switched in, without weakening any check.
    internal static JsonObject Validate(string data, JsonNode? indexOverride)
    {
        var errors=new JsonArray();var meshCount=0;var triangles=0;var caseCount=0;var seenMeshes=new HashSet<string>();
        // Errors are additionally attributed per run (runErrors) so callers can
        // isolate a bad run without weakening any check or losing any error.
        var runErrors=new JsonObject();string? currentRun=null;
        void Error(string message){errors.Add((JsonNode?)JsonValue.Create(message));if(currentRun!=null)((JsonArray)(runErrors[currentRun]??=new JsonArray())).Add((JsonNode?)JsonValue.Create(message));}
        // The canonical root itself and its ancestors are part of the security
        // boundary: a root reached through symlinks is rejected outright.
        if(!WebProgressSafePath.IsSafeChain(data,out var rootOffender)){Error("root: symlink/reparse root rejected "+rootOffender);return Result();}
        void Fields(JsonNode? node,string names,string context)
        {if(node is not JsonObject obj){Error(context+": expected object");return;}foreach(var key in names.Split(' '))if(!obj.ContainsKey(key))Error(context+": missing "+key);}
        string? Resolve(string root,JsonNode? reference,string context)
        {
            if(reference is null)return null;
            if(reference is not JsonValue value||!value.TryGetValue<string>(out var relative)||Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Contains(':')){Error(context+": invalid relative path");return null;}
            var path=Path.GetFullPath(relative,root);
            // Accumulated manifests reference evidence of other measurement runs
            // as "../<runId>/..."; such references must stay inside the data
            // root's runs tree. Anything else must stay inside the run itself.
            var underRoot=WebProgressSafePath.IsWithin(root,path);
            var underRuns=WebProgressSafePath.IsWithin(Path.Combine(data,"runs"),path);
            if((!underRoot&&!underRuns)||!File.Exists(path)){Error(context+": missing or escaping path "+relative);return null;}
            // Canonical security boundary: the root, its ancestors, intermediate
            // directories and the leaf must all be real entries; symlink/reparse
            // escapes are rejected so external files can never validate or enter
            // static bundles.
            if(!WebProgressSafePath.IsSafeChain(path,out var offender)){Error(context+": symlink/reparse escape rejected "+relative);return null;}
            return path;
        }
        // Current contract: a reference ending in .gz is a standard gzip member whose
        // decompressed bytes must be the exact JSON the producer wrote.
        string ReadText(string path){if(!path.EndsWith(".gz",StringComparison.Ordinal))return File.ReadAllText(path);using var gzip=new GZipStream(File.OpenRead(path),CompressionMode.Decompress);using var buffer=new MemoryStream();gzip.CopyTo(buffer);return new StreamReader(new MemoryStream(buffer.ToArray())).ReadToEnd();}
        JsonNode? Read(string path){try{return JsonNode.Parse(ReadText(path));}catch(Exception e){Error(Path.GetFileName(path)+": "+e.Message);return null;}}
        void Mesh(string path)
        {
            if(!seenMeshes.Add(path))return;var mesh=Read(path);if(mesh==null)return;meshCount++;
            Fields(mesh,"schemaVersion bodyId source faces edges",path);
            if(mesh["schemaVersion"]?.GetValue<int>()!=1)Error(path+": schemaVersion");
            Fields(mesh["source"],"kernel geometryAuthor tessellator commit chordTolerance",path+" source");
            if(mesh["source"]?["chordTolerance"]?.GetValue<double>() is not >0)Error(path+": chord tolerance");
            var ids=new HashSet<string>();
            foreach(var kind in new[]{"faces","edges"})
            {
                if(mesh[kind] is not JsonArray entities){Error(path+": missing "+kind);continue;}
                foreach(var entity in entities)
                {
                    Fields(entity,kind=="faces"?"id positions indices role":"id positions role",path+" "+kind);
                    if(entity?["id"] is not JsonValue id||!id.TryGetValue<string>(out var tag)||!ids.Add(tag))Error(path+": duplicate/invalid entity ID");
                    if(entity?["role"] is not JsonValue role||!role.TryGetValue<string>(out var roleName)||!Roles.Contains(roleName))Error(path+": invalid role");
                    if(entity?["positions"] is not JsonArray positions||positions.Count%3!=0||positions.Any(p=>p is not JsonValue v||!v.TryGetValue<double>(out var n)||!double.IsFinite(n))){Error(path+": invalid coordinates");continue;}
                    if(kind=="edges"&&positions.Count<6)Error(path+": edge polyline has fewer than two points");
                    if(kind=="faces")
                    {
                        if(entity!["indices"] is not JsonArray indices||indices.Count%3!=0||indices.Any(i=>i is not JsonValue v||!v.TryGetValue<int>(out var index)||index<0||index>=positions.Count/3))Error(path+": invalid face-local triangle indices");
                        else triangles+=indices.Count/3;
                        if(entity!["normals"] is JsonArray normals&&(normals.Count!=positions.Count||normals.Any(p=>p is not JsonValue v||!v.TryGetValue<double>(out var n)||!double.IsFinite(n))))Error(path+": invalid normals");
                    }
                }
            }
        }
        var indexPath=Path.Combine(data,"index.json");
        if(indexOverride==null&&!File.Exists(indexPath)){Error("Missing index.json");return Result();}
        var index=indexOverride??Read(indexPath);Fields(index,"schemaVersion generatedAt latestRunId runs","index");
        if(index?["schemaVersion"]?.GetValue<int>()!=1)Error("index schemaVersion");
        var runIds=new HashSet<string>();
        foreach(var descriptor in index?["runs"]?.AsArray()??[])
        {
            Fields(descriptor,"id commit generatedAt label manifest","index run");
            var id=descriptor!["id"]!.GetValue<string>();currentRun=id;if(!runIds.Add(id))Error("duplicate run ID");
            var path=Resolve(data,descriptor["manifest"],id);if(path==null)continue;
            var manifest=Read(path);var runRoot=Path.GetDirectoryName(path)!;
            Fields(manifest,"schemaVersion run cases",id);
            if(manifest?["schemaVersion"]?.GetValue<int>()!=1)Error(id+": manifest schemaVersion");
            Fields(manifest?["run"],"id commit generatedAt pkVersion host configuration scope provenance",id+" run");
            if(manifest?["run"]?["id"]?.GetValue<string>()!=id)Error(id+": descriptor mismatch");
            var caseIds=new HashSet<string>();
            foreach(var c in manifest?["cases"]?.AsArray()??[])
            {
                caseCount++;Fields(c,"id title family operation status parameters apis steps checks performance evidence notes source",id+" case");
                var caseId=c!["id"]!.GetValue<string>();if(!caseIds.Add(caseId))Error(id+": duplicate case ID");
                if(c["family"]?.GetValue<string>() is not ("blend" or "boolean"))Error(caseId+": invalid family");
                var status=c["status"]!.GetValue<string>();if(!Statuses.Contains(status))Error(caseId+": invalid status");
                Fields(c["source"],"path revision",caseId+" source");
                foreach(var side in new[]{"gm","pk"})Fields(c["apis"]?[side],"name parameters",caseId+" API "+side);
                foreach(var step in c["steps"]?.AsArray()??[])
                {
                    Fields(step,"id title gm pk",caseId+" step");
                    foreach(var side in new[]{"gm","pk"})
                    {
                        Fields(step?[side],"before after",caseId+" step "+side);
                        var encoding=step?[side]?["encoding"]?.GetValue<string>();
                        if(encoding!=null&&encoding!="gzip")Error(caseId+": unsupported step encoding "+encoding);
                        bool Mismatch(JsonNode? reference)=>reference is JsonValue rv&&rv.TryGetValue<string>(out var rel)&&rel.EndsWith(".gz",StringComparison.Ordinal)!=(encoding=="gzip");
                        foreach(var key in new[]{"before","after"})
                        {var reference=step?[side]?[key];if(reference==null){if(status=="pass")Error(caseId+": passing step lacks "+side+" "+key);continue;}if(Mismatch(reference))Error(caseId+": transport encoding/member mismatch");var file=Resolve(runRoot,reference,caseId);if(file!=null)Mesh(file);}
                        foreach(var key in new[]{"tools","afterBodies"})foreach(var reference in step?[side]?[key]?.AsArray()??[]){if(Mismatch(reference))Error(caseId+": transport encoding/member mismatch");var file=Resolve(runRoot,reference,caseId);if(file!=null)Mesh(file);}
                    }
                }
                var checks=c["checks"]?.AsArray()??[];
                foreach(var check in checks)
                {
                    Fields(check,"id label status gm pk absDelta normalizedDelta threshold unit evidence note",caseId+" check");
                    var checkStatus=check!["status"]!.GetValue<string>();if(!Statuses.Contains(checkStatus))Error(caseId+": invalid check status");
                    if(status=="pass"&&checkStatus is not ("pass" or "diagnostic"))Error(caseId+": passing case has nonpassing required check");
                    if(check["evidence"]!=null)Resolve(runRoot,check["evidence"],caseId+" check evidence");
                }
                if(status=="pass"&&checks.Count==0)Error(caseId+": pass without acceptance evidence");
                foreach(var evidence in c["evidence"]?.AsArray()??[]){Fields(evidence,"label path",caseId+" evidence");Resolve(runRoot,evidence?["path"],caseId+" evidence");}
                var perf=c["performance"];Fields(perf,"gmMs pkMs ratioGmOverPk samples warmup scope correctnessQualified host configuration note",caseId+" performance");
                if(perf?["ratioGmOverPk"]!=null)
                {
                    var gm=perf["gmMs"]?.GetValue<double>();var pk=perf["pkMs"]?.GetValue<double>();var ratio=perf["ratioGmOverPk"]!.GetValue<double>();
                    if(status!="pass"||perf["correctnessQualified"]?.GetValue<bool>()!=true||gm is null||pk is not >0||!double.IsFinite(ratio)||Math.Abs(ratio-gm.Value/pk.Value)>1e-12*Math.Max(1,ratio))Error(caseId+": invalid/unqualified ratio");
                }
            }
        }
        currentRun=null;
        if(index?["latestRunId"] is JsonValue latest&&!runIds.Contains(latest.GetValue<string>()))Error("latestRunId is not registered");
        return Result();
        JsonObject Result()=>new(){["schemaVersion"]=1,["cases"]=caseCount,["meshes"]=meshCount,["triangles"]=triangles,["errors"]=errors,["runErrors"]=runErrors};
    }
}

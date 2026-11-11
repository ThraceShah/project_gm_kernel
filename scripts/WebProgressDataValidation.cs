using System.Text.Json.Nodes;

internal static class WebProgressDataValidation
{
    private static readonly HashSet<string> Statuses=["pass","fail","missing","unsupported","timeout","diagnostic"];
    private static readonly HashSet<string> OperationStatuses=["pass","fail","not-run","unverified","timeout"];
    private static readonly HashSet<string> OperationRoles=["primary","precondition","lifecycle","auxiliary"];
    private static readonly HashSet<string> Roles=["unchanged","selected","support","newBlend","modified","deleted"];
    internal static JsonObject Validate(string data)
    {
        var errors=new JsonArray();var meshCount=0;var triangles=0;var caseCount=0;var seenMeshes=new HashSet<string>();
        void Error(string message)=>errors.Add((JsonNode?)JsonValue.Create(message));
        void Fields(JsonNode? node,string names,string context)
        {if(node is not JsonObject obj){Error(context+": expected object");return;}foreach(var key in names.Split(' '))if(!obj.ContainsKey(key))Error(context+": missing "+key);}
        string? Resolve(string root,JsonNode? reference,string context)
        {
            if(reference is null)return null;
            if(reference is not JsonValue value||!value.TryGetValue<string>(out var relative)||Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Contains(':')){Error(context+": invalid relative path");return null;}
            var path=Path.GetFullPath(relative,root);
            if(!path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.Ordinal)||!File.Exists(path)){Error(context+": missing or escaping path "+relative);return null;}
            return path;
        }
        JsonNode? Read(string path){try{return JsonNode.Parse(File.ReadAllText(path));}catch(Exception e){Error(Path.GetFileName(path)+": "+e.Message);return null;}}
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
        if(!File.Exists(indexPath)){Error("Missing index.json");return Result();}
        var index=Read(indexPath);Fields(index,"schemaVersion generatedAt latestRunId runs","index");
        if(index?["schemaVersion"]?.GetValue<int>()!=1)Error("index schemaVersion");
        var runIds=new HashSet<string>();
        foreach(var descriptor in index?["runs"]?.AsArray()??[])
        {
            Fields(descriptor,"id commit generatedAt label manifest","index run");
            var id=descriptor!["id"]!.GetValue<string>();if(!runIds.Add(id))Error("duplicate run ID");
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
                        foreach(var key in new[]{"before","after"})
                        {var reference=step?[side]?[key];if(reference==null){if(status=="pass")Error(caseId+": passing step lacks "+side+" "+key);continue;}var file=Resolve(runRoot,reference,caseId);if(file!=null)Mesh(file);}
                        foreach(var key in new[]{"tools","afterBodies"})foreach(var reference in step?[side]?[key]?.AsArray()??[]){var file=Resolve(runRoot,reference,caseId);if(file!=null)Mesh(file);}
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
                if(c["operations"] is JsonArray operations)
                {
                    var opIds=new HashSet<string>();
                    var ordinals=new HashSet<double>();
                    foreach(var op in operations)
                    {
                        Fields(op,"operationId ordinal role api status primaryChecks auxiliaryChecks representationContracts timing",caseId+" operation");
                        var opId=op?["operationId"]?.GetValue<string>()??"unknown";
                        if(opId=="unknown"||!opIds.Add(opId))Error(caseId+": duplicate/invalid operation ID");
                        var ordinal=OperationAcceptanceRules.Number(op?["ordinal"]);
                        if(ordinal is null||ordinal<0||ordinal!=Math.Truncate(ordinal.Value)||!ordinals.Add(ordinal.Value))Error(caseId+" "+opId+": duplicate/invalid ordinal");
                        var opStatus=op?["status"]?.GetValue<string>();
                        if(opStatus==null||!OperationStatuses.Contains(opStatus))Error(caseId+" "+opId+": invalid operation status "+opStatus);
                        var opRole=op?["role"]?.GetValue<string>();
                        if(opRole==null||!OperationRoles.Contains(opRole))Error(caseId+" "+opId+": invalid operation role "+opRole);
                        foreach(var cat in new[]{"primaryChecks","auxiliaryChecks","representationContracts"})
                        {
                            if(op?[cat] is not JsonArray checkArr){Error(caseId+" "+opId+": missing "+cat);continue;}
                            foreach(var ch in checkArr)
                            {
                                Fields(ch,"id label status",caseId+" "+opId+" check");
                                var chOwner=ch?["owner"]?.GetValue<string>();
                                if(chOwner!=opId)Error(caseId+" "+opId+": check owner mismatch "+chOwner);
                                var category=cat=="primaryChecks"?"primary":cat=="auxiliaryChecks"?"auxiliary":"representation";
                                if(OperationAcceptanceRules.Text(ch?["category"])!=category)Error(caseId+" "+opId+": check category mismatch");
                                var chStatus=ch?["status"]?.GetValue<string>();
                                if(chStatus==null||!Statuses.Contains(chStatus))Error(caseId+" "+opId+": invalid check status "+chStatus);
                            }
                        }
                        var timing=op?["timing"];
                        Fields(timing,"boundary gmMs pkMs coldKind warmup samples correctnessQualified ratio",caseId+" "+opId+" timing");
                        if(op is JsonObject operation)
                        {
                            var derived=OperationAcceptanceRules.Evaluate(operation);
                            if(derived.Reasons.Any(r=>OperationAcceptanceRules.Text(r)=="not-run-contradicts-execution"))Error(caseId+" "+opId+": not-run contradicts recorded execution");
                            if(opStatus!=derived.Status)Error(caseId+" "+opId+": status disagrees with recorded facts; expected "+derived.Status);
                            if(op["primaryStatus"]!=null&&OperationAcceptanceRules.Text(op["primaryStatus"])!=derived.PrimaryStatus)Error(caseId+" "+opId+": primaryStatus disagrees with recorded facts");
                            if(OperationAcceptanceRules.IsTrue(timing?["correctnessQualified"])!=derived.Qualified)Error(caseId+" "+opId+": qualification disagrees with recorded facts "+derived.Reasons.ToJsonString());
                            if(timing?["unqualifiedReasons"]!=null&&!JsonNode.DeepEquals(timing["unqualifiedReasons"],derived.Reasons))Error(caseId+" "+opId+": incorrect unqualified reasons");
                            if(op["parameterHash"]!=null&&(!OperationAcceptanceRules.IsHash(op["parameterHash"])||op["options"]==null||OperationAcceptanceRules.Text(op["parameterHash"])!=OperationAcceptanceLedger.HashString(op["options"]!.ToJsonString())))Error(caseId+" "+opId+": invalid parameter hash");
                            foreach(var side in new[]{"gm","pk"})
                            {
                                if(op["inputHashes"]?[side] is JsonObject input)
                                    foreach(var field in input)
                                        if(field.Key=="target"?field.Value!=null&&!OperationAcceptanceRules.IsHash(field.Value):field.Key!="tools"||field.Value is not JsonArray hashes||hashes.Any(h=>!OperationAcceptanceRules.IsHash(h)))Error(caseId+" "+opId+": invalid input hash field "+field.Key);
                                if(op["outputHashes"]?[side] is JsonObject output)
                                    foreach(var field in output)
                                        if(field.Key!="bodies"||field.Value is not JsonArray hashes||hashes.Any(h=>!OperationAcceptanceRules.IsHash(h)))Error(caseId+" "+opId+": invalid output hash field "+field.Key);
                            }
                        }
                        if(timing?["ratio"]!=null)
                        {
                            var gm=timing["gmMs"]?.GetValue<double>();var pk=timing["pkMs"]?.GetValue<double>();var ratio=timing["ratio"]!.GetValue<double>();
                            var qualified=timing["correctnessQualified"]?.GetValue<bool>()==true;
                            if(opStatus!="pass"||!qualified||gm is not >0||pk is not >0||!double.IsFinite(gm.Value)||!double.IsFinite(pk.Value)||!double.IsFinite(ratio)||ratio<=0||Math.Abs(ratio-gm.Value/pk.Value)>1e-6*Math.Max(1,ratio))Error(caseId+" "+opId+": invalid/unqualified operation ratio");
                        }
                        else if(timing?["correctnessQualified"]?.GetValue<bool>()==true)
                        {
                            Error(caseId+" "+opId+": correctnessQualified without ratio");
                        }
                    }
                }
                var perf=c["performance"];Fields(perf,"gmMs pkMs ratioGmOverPk samples warmup scope correctnessQualified host configuration note",caseId+" performance");
                var perfKind=perf?["kind"]?.GetValue<string>();
                if(perfKind=="scenario-aggregate")
                {
                    if(perf?["ratioGmOverPk"]!=null)Error(caseId+": scenario-aggregate must not specify ratioGmOverPk");
                    if(perf?["correctnessQualified"]?.GetValue<bool>()==true)Error(caseId+": scenario-aggregate must not be correctnessQualified");
                }
                else if(perf?["ratioGmOverPk"]!=null)
                {
                    var gm=perf["gmMs"]?.GetValue<double>();var pk=perf["pkMs"]?.GetValue<double>();var ratio=perf["ratioGmOverPk"]!.GetValue<double>();
                    if(status!="pass"||perf["correctnessQualified"]?.GetValue<bool>()!=true||gm is not >0||pk is not >0||!double.IsFinite(gm.Value)||!double.IsFinite(pk.Value)||!double.IsFinite(ratio)||ratio<=0||Math.Abs(ratio-gm.Value/pk.Value)>1e-12*Math.Max(1,ratio))Error(caseId+": invalid/unqualified ratio");
                    if(c["operations"] is JsonArray caseOps&&caseOps.Count>0&&caseOps.Any(o=>!OperationAcceptanceRules.IsTrue(o?["timing"]?["correctnessQualified"])))Error(caseId+": case ratio contains an unqualified operation");
                }
            }
        }
        if(index?["latestRunId"] is JsonValue latest&&!runIds.Contains(latest.GetValue<string>()))Error("latestRunId is not registered");
        return Result();
        JsonObject Result()=>new(){["schemaVersion"]=1,["cases"]=caseCount,["meshes"]=meshCount,["triangles"]=triangles,["errors"]=errors};
    }
}

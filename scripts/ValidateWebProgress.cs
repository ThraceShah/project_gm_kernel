#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
var directory=Path.GetDirectoryName(ScriptPath())!;
if(args.Contains("--self-test"))
{
    var root=Path.GetFullPath("../temp_docs/web-progress/validator-tests",directory);Directory.CreateDirectory(root);
    void AssertRejected(JsonObject index,string label){File.WriteAllText(Path.Combine(root,"index.json"),index.ToJsonString());if(WebProgressDataValidation.Validate(root)["errors"]!.AsArray().Count==0)throw new Exception("Validator accepted "+label);Console.WriteLine("PASS rejects "+label);}
    AssertRejected(new JsonObject{["schemaVersion"]=1,["generatedAt"]="now",["latestRunId"]="absent",["runs"]=new JsonArray()},"missing latest run");
    AssertRejected(new JsonObject{["schemaVersion"]=1,["generatedAt"]="now",["latestRunId"]="escape",["runs"]=new JsonArray((JsonNode)new JsonObject{["id"]="escape",["commit"]="test",["generatedAt"]="now",["label"]="negative",["manifest"]="../../scripts/RefreshWebProgress.cs"})},"path traversal");
    return 0;
}
var data=Path.GetFullPath(args.Length>0?args[0]:"../web/data",directory);var result=WebProgressDataValidation.Validate(data);Console.WriteLine(result.ToJsonString());return result["errors"]!.AsArray().Count==0?0:1;
static string ScriptPath([CallerFilePath]string path="")=>path;

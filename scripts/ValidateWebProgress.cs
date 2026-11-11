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
    var runDirectory=Path.Combine(root,"runs","test");Directory.CreateDirectory(runDirectory);
    var mesh=new JsonObject{["schemaVersion"]=1,["bodyId"]="fixture",["source"]=new JsonObject{["kernel"]="GM",["geometryAuthor"]="fixture",["tessellator"]="test-only",["commit"]="test",["chordTolerance"]=.01},["faces"]=new JsonArray((JsonNode)new JsonObject{["id"]="face",["positions"]=new JsonArray(0,0,0,1,0,0,0,1,0),["indices"]=new JsonArray(0,1,2),["role"]="selected"}),["edges"]=new JsonArray()};
    var c=new JsonObject{["id"]="case",["title"]="contract test only",["family"]="blend",["operation"]="test",["status"]="pass",["parameters"]=new JsonObject(),["apis"]=new JsonObject{["gm"]=new JsonObject{["name"]="fixture",["parameters"]=new JsonObject()},["pk"]=new JsonObject{["name"]="fixture",["parameters"]=new JsonObject()}},["steps"]=new JsonArray((JsonNode)new JsonObject{["id"]="step",["title"]="fixture",["gm"]=new JsonObject{["before"]="mesh.json",["after"]="mesh.json"},["pk"]=new JsonObject{["before"]="mesh.json",["after"]="mesh.json"}}),["checks"]=new JsonArray((JsonNode)new JsonObject{["id"]="check",["label"]="fixture",["status"]="pass",["gm"]=0,["pk"]=0,["absDelta"]=0,["normalizedDelta"]=0,["threshold"]=0,["unit"]="count",["evidence"]=null,["note"]="test"}),["performance"]=new JsonObject{["gmMs"]=2,["pkMs"]=1,["ratioGmOverPk"]=2,["samples"]=1,["warmup"]=0,["scope"]="test only",["correctnessQualified"]=true,["host"]=new JsonObject(),["configuration"]=new JsonObject(),["note"]="test"},["evidence"]=new JsonArray(),["notes"]=new JsonArray(),["source"]=new JsonObject{["path"]="fixture",["revision"]="test"}};
    var manifest=new JsonObject{["schemaVersion"]=1,["run"]=new JsonObject{["id"]="test",["commit"]="test",["generatedAt"]="now",["pkVersion"]=null,["host"]=new JsonObject(),["configuration"]=new JsonObject(),["scope"]="test",["provenance"]=new JsonObject()},["cases"]=new JsonArray((JsonNode)c)};
    var validIndex=new JsonObject{["schemaVersion"]=1,["generatedAt"]="now",["latestRunId"]="test",["runs"]=new JsonArray((JsonNode)new JsonObject{["id"]="test",["commit"]="test",["generatedAt"]="now",["label"]="test",["manifest"]="runs/test/manifest.json"})};
    void WriteFixture(){File.WriteAllText(Path.Combine(root,"index.json"),validIndex.ToJsonString());File.WriteAllText(Path.Combine(runDirectory,"manifest.json"),manifest.ToJsonString());File.WriteAllText(Path.Combine(runDirectory,"mesh.json"),mesh.ToJsonString());}
    void Expect(bool valid,string label){WriteFixture();var result=WebProgressDataValidation.Validate(root);if((result["errors"]!.AsArray().Count==0)!=valid)throw new Exception(label+": "+result.ToJsonString());Console.WriteLine("PASS "+label);}
    Expect(true,"zero-valued valid contract");
    mesh["faces"]![0]!["indices"]![2]=3;Expect(false,"reject out-of-range face-local index");mesh["faces"]![0]!["indices"]![2]=2;
    c["performance"]!["correctnessQualified"]=false;Expect(false,"reject unqualified numeric ratio");c["performance"]!["correctnessQualified"]=true;
    c["checks"]![0]!["status"]="missing";Expect(false,"reject unknown acceptance as pass");c["checks"]![0]!["status"]="pass";
    mesh["faces"]![0]!["positions"]![0]="NaN";Expect(false,"reject non-numeric coordinates");mesh["faces"]![0]!["positions"]![0]=0;
    WriteFixture();var destination=root+"-published";WebProgressPublishing.Publish(root,"test",destination);Console.WriteLine("PASS atomic safe-data publish");
    try{WebProgressPublishing.Publish(root,"test",Path.Combine(root,"nested"));throw new Exception("Publisher accepted overlapping roots");}catch(IOException){Console.WriteLine("PASS reject overlapping source/destination");}
    File.WriteAllText(Path.Combine(runDirectory,"forbidden.dll"),"test-only");
    try{WebProgressPublishing.Publish(root,"test",root+"-rejected");throw new Exception("Publisher accepted binary extension");}catch(IOException){Console.WriteLine("PASS static allowlist rejects binaries");}finally{File.Delete(Path.Combine(runDirectory,"forbidden.dll"));}
    // symlink/reparse escapes never validate and never enter static bundles
    var outside=Path.GetFullPath(Path.Combine(root,"..","validator-tests-outside.log"));File.WriteAllText(outside,"outside public root");
    File.CreateSymbolicLink(Path.Combine(runDirectory,"escape.log"),Path.GetRelativePath(runDirectory,outside));
    c["evidence"]=new JsonArray((JsonNode)new JsonObject{["label"]="escaped",["path"]="escape.log"});Expect(false,"reject file symlink escape");
    Directory.CreateSymbolicLink(Path.Combine(runDirectory,"linked"),Path.GetDirectoryName(outside)!);
    File.WriteAllText(Path.Combine(root,"outside-link.log"),"outside public root");
    c["evidence"]=new JsonArray((JsonNode)new JsonObject{["label"]="escaped",["path"]="linked/validator-tests-outside.log"});Expect(false,"reject directory symlink escape at intermediate component");
    Directory.Delete(Path.Combine(runDirectory,"linked"));c["evidence"]=new JsonArray();WriteFixture();
    try{WebProgressPublishing.Publish(root,"test",root+"-rejected-link");throw new Exception("Publisher followed a symlink");}catch(IOException){Console.WriteLine("PASS static allowlist rejects symlinks");}
    File.Delete(Path.Combine(runDirectory,"escape.log"));File.Delete(outside);File.Delete(Path.Combine(root,"outside-link.log"));
    return 0;
}
var data=Path.GetFullPath(args.Length>0?args[0]:"../web/data",directory);var result=WebProgressDataValidation.Validate(data);Console.WriteLine(result.ToJsonString());return result["errors"]!.AsArray().Count==0?0:1;
static string ScriptPath([CallerFilePath]string path="")=>path;

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

    // Operation-layered contracts and negative tests
    var op1=new JsonObject{
        ["operationId"]="op-1",
        ["ordinal"]=0,
        ["name"]="PK_BODY_fix_blends",
        ["role"]="primary",
        ["api"]="PK_BODY_fix_blends",
        ["status"]="pass",
        ["primaryChecks"]=new JsonArray((JsonNode)new JsonObject{["id"]="0-compare",["owner"]="op-1",["category"]="primary",["label"]="compare",["status"]="pass"}),
        ["auxiliaryChecks"]=new JsonArray(),
        ["representationContracts"]=new JsonArray(),
        ["timing"]=new JsonObject{["boundary"]="api-only",["gmMs"]=2.0,["pkMs"]=1.0,["coldKind"]="fresh-process",["warmup"]=0,["samples"]=1,["correctnessQualified"]=true,["ratio"]=2.0}
    };
    c["operations"]=new JsonArray((JsonNode)op1);
    c["operationSchemaVersion"]="operation-layered-v1";
    c["scenarioId"]="case";
    c["scenarioStatus"]="pass";
    c["performance"]!["kind"]="single-operation";
    Expect(true,"valid operation-layered single-operation contract");

    // Negative: check owner mismatch
    op1["primaryChecks"]![0]!["owner"]="different-op";Expect(false,"reject check owner mismatch");op1["primaryChecks"]![0]!["owner"]="op-1";

    // Negative: failed operation with qualified ratio
    op1["status"]="fail";Expect(false,"reject failed operation with qualified ratio");op1["status"]="pass";

    // Negative: unverified operation with qualified ratio
    op1["status"]="unverified";Expect(false,"reject unverified operation with qualified ratio");op1["status"]="pass";

    // Negative: correctnessQualified true without ratio
    op1["timing"]!["ratio"]=null;Expect(false,"reject correctnessQualified without ratio");op1["timing"]!["ratio"]=2.0;

    // Negative: multi-step scenario-aggregate with case ratioGmOverPk
    var op2=new JsonObject{
        ["operationId"]="op-2",
        ["ordinal"]=1,
        ["name"]="PK_BODY_boolean",
        ["role"]="primary",
        ["api"]="PK_BODY_boolean",
        ["status"]="fail",
        ["primaryChecks"]=new JsonArray((JsonNode)new JsonObject{["id"]="1-compare",["owner"]="op-2",["category"]="primary",["label"]="compare",["status"]="fail"}),
        ["auxiliaryChecks"]=new JsonArray(),
        ["representationContracts"]=new JsonArray(),
        ["timing"]=new JsonObject{["boundary"]="api-only",["gmMs"]=3.0,["pkMs"]=1.0,["coldKind"]="fresh-process",["warmup"]=0,["samples"]=1,["correctnessQualified"]=false,["ratio"]=null}
    };
    c["operations"]!.AsArray().Add((JsonNode)op2);
    c["performance"]!["kind"]="scenario-aggregate";
    Expect(false,"reject scenario-aggregate with case ratioGmOverPk");

    // Positive: valid multi-step scenario-aggregate (case ratio is null, status fail, step 0 pass retained, step 1 fail)
    c["performance"]!["ratioGmOverPk"]=null;
    c["performance"]!["correctnessQualified"]=false;
    c["status"]="fail";
    c["scenarioStatus"]="fail";
    c["firstFailedOperationId"]="op-2";
    c["checks"]![0]!["status"]="fail";
    Expect(true,"valid multi-step scenario-aggregate with prior step pass retained");

    // Positive: unreached step with status not-run
    var op3=new JsonObject{
        ["operationId"]="unreached-2",
        ["ordinal"]=2,
        ["name"]="PK_BODY_boolean",
        ["role"]="lifecycle",
        ["api"]="PK_BODY_boolean",
        ["status"]="not-run",
        ["primaryChecks"]=new JsonArray(),
        ["auxiliaryChecks"]=new JsonArray(),
        ["representationContracts"]=new JsonArray(),
        ["timing"]=new JsonObject{["boundary"]="api-only",["gmMs"]=null,["pkMs"]=null,["coldKind"]="fresh-process",["warmup"]=0,["samples"]=0,["correctnessQualified"]=false,["ratio"]=null}
    };
    c["operations"]!.AsArray().Add((JsonNode)op3);
    Expect(true,"valid unreached step marked not-run");

    // Positive: multiple same-name APIs with unique stable IDs
    var op4=new JsonObject{
        ["operationId"]="boolean-cut-2",
        ["ordinal"]=3,
        ["name"]="PK_BODY_boolean",
        ["role"]="lifecycle",
        ["api"]="PK_BODY_boolean",
        ["status"]="not-run",
        ["primaryChecks"]=new JsonArray(),
        ["auxiliaryChecks"]=new JsonArray(),
        ["representationContracts"]=new JsonArray(),
        ["timing"]=new JsonObject{["boundary"]="api-only",["gmMs"]=null,["pkMs"]=null,["coldKind"]="fresh-process",["warmup"]=0,["samples"]=0,["correctnessQualified"]=false,["ratio"]=null}
    };
    c["operations"]!.AsArray().Add((JsonNode)op4);
    Expect(true,"valid multiple same-name APIs with unique stable operation IDs");

    // Cleanup for publish tests
    c["operations"]=new JsonArray();
    c["performance"]!["kind"]="single-operation";
    c["performance"]!["gmMs"]=2;
    c["performance"]!["pkMs"]=1;
    c["performance"]!["ratioGmOverPk"]=2;
    c["performance"]!["correctnessQualified"]=true;
    c["status"]="pass";
    c["scenarioStatus"]="pass";
    c["checks"]![0]!["status"]="pass";
    WriteFixture();var destination=Path.Combine(root,"published");if(Directory.Exists(destination))Directory.Delete(destination,true);WebProgressPublishing.Publish(root,"test",destination);Console.WriteLine("PASS atomic safe-data publish");
    File.WriteAllText(Path.Combine(runDirectory,"forbidden.dll"),"test-only");
    try{WebProgressPublishing.Publish(root,"test",Path.Combine(root,"rejected"));throw new Exception("Publisher accepted binary extension");}catch(IOException){Console.WriteLine("PASS static allowlist rejects binaries");}finally{File.Delete(Path.Combine(runDirectory,"forbidden.dll"));}
    return 0;
}
var data=Path.GetFullPath(args.Length>0?args[0]:"../web/data",directory);var result=WebProgressDataValidation.Validate(data);Console.WriteLine(result.ToJsonString());return result["errors"]!.AsArray().Count==0?0:1;
static string ScriptPath([CallerFilePath]string path="")=>path;

using System.Text.Json.Nodes;

internal static class WebProgressMetadata
{
    internal static void Apply(JsonObject manifest,JsonObject registry)
    {
        var entries=registry["cases"]!.AsArray().OfType<JsonObject>().ToDictionary(c=>c["id"]!.GetValue<string>(),StringComparer.Ordinal);
        manifest["run"]!["capabilityScope"]="Declared finite web recipes; export adapter coverage is separate from kernel API capability.";
        manifest["run"]!["provenance"]!["caseStatusDefinitions"]=new JsonObject
        {
            ["pass"]="Current required checks passed for these parameters only.",
            ["fail"]="A current required check failed; original oracle exit is recorded independently.",
            ["missing"]="Not executed, unreached, or current artifact unavailable.",
            ["unsupported"]="Web export adapter not implemented; no kernel capability conclusion.",
            ["timeout"]="Owned execution exceeded its configured budget.",
            ["diagnostic"]="Diagnostic scope; not a qualified acceptance pass."
        };
        foreach(var c in manifest["cases"]!.AsArray().OfType<JsonObject>())
        {
            var ready=entries.TryGetValue(c["id"]!.GetValue<string>(),out var entry)&&entry["meshAdapter"]!.GetValue<bool>();
            c["capabilityScope"]="web-export-adapter";c["adapterStatus"]=ready?"implemented":"not-implemented";c["kernelCapability"]=null;
            var perf=c["performance"]!;var samples=perf["samples"]!.GetValue<int>();var warmup=perf["warmup"]!.GetValue<int>();
            perf["measurementKind"]=samples==0?"not-run":warmup==0?(samples==1?"single-cold-sample":"fresh-process-cold-median"):"warmed-samples";
            perf["stableBenchmark"]=false;perf["qualifiedScope"]="Current correctness gates only; no steady-state performance certification.";
            var notes=c["notes"]!.AsArray();
            if(c["execution"]?["attempted"]?.GetValue<bool>()==true)Note(notes,"输出中性 unchanged 角色只代表未取得修改标注；roleKnown=false 时不能断言该面/边保持不变。选择与新 blend 的标注仍以精确 ID 和 API 返回记录为准。");
            if(c["id"]!.GetValue<string>().StartsWith("short-edge.",StringComparison.Ordinal)&&c["id"]!.GetValue<string>().Contains("lifecycle",StringComparison.Ordinal))Note(notes,"imported-product 步骤中的圆角由 PK 创建后导入 GM，随后复制/变换/Boolean 由 GM 执行；这些步骤不计作 GM 自建圆角，self-constructed-product 是另一路独立 GM Fix。source.kernel 表示快照所属运行时，不能代替原始几何作者。");
            if(!ready)Note(notes,"数据导出适配未实现；未断言内核 API 是否支持，不能显示为“内核暂不支持”。");
            if(c["steps"]!.AsArray().Any(step=>new[]{"gm","pk"}.Any(side=>step?[side]?["semanticCoverage"]?["status"]?.GetValue<string>()=="missing")))
                Note(notes,"部分操作未返回 generic topology tracking；roleKnown=false 的中性显示不代表面保持不变，准确删项与原始 ID 仍保留。");
        }
    }
    private static void Note(JsonArray notes,string note){if(!notes.Any(n=>n?.GetValue<string>()==note))notes.Add((JsonNode?)JsonValue.Create(note));}
}

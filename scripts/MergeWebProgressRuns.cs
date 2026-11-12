#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Merges two disjoint capture shards of the same registry into one run
// directory. Each shard is a complete 463-case manifest where non-selected
// cases are `missing`; the merge only takes a case record from the secondary
// shard when the primary did not actually execute it, and copies that case's
// own evidence directory verbatim. No result, exit code or note is synthesised.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var primary = Path.GetFullPath(Option("--primary", ""), scripts);
var secondary = Path.GetFullPath(Option("--secondary", ""), scripts);
var data = Path.GetFullPath(Option("--data", ""), scripts);
if (primary.Length == 0 || secondary.Length == 0 || data.Length == 0) throw new ArgumentException("--primary, --secondary and --data are required");
var primaryManifestPath = Path.Combine(primary, "manifest.json");
var secondaryManifestPath = Path.Combine(secondary, "manifest.json");
var primaryManifest = JsonNode.Parse(File.ReadAllText(primaryManifestPath))!.AsObject();
var secondaryManifest = JsonNode.Parse(File.ReadAllText(secondaryManifestPath))!.AsObject();
var primaryCases = primaryManifest["cases"]!.AsArray().OfType<JsonObject>().ToDictionary(c => c["id"]!.GetValue<string>(), StringComparer.Ordinal);
var secondaryCases = secondaryManifest["cases"]!.AsArray().OfType<JsonObject>().ToDictionary(c => c["id"]!.GetValue<string>(), StringComparer.Ordinal);
if (primaryCases.Count != secondaryCases.Count || !primaryCases.Keys.All(secondaryCases.ContainsKey))
    throw new InvalidDataException("shards must cover the same registry case set");

static bool Attempted(JsonObject c) => c["execution"]?["attempted"]?.GetValue<bool>() == true;
static bool HasSteps(JsonObject c) => c["steps"] is JsonArray s && s.Count > 0;

var adopted = new JsonArray();
var conflicts = new JsonArray();
foreach (var (id, fromSecondary) in secondaryCases)
{
    var target = primaryCases[id];
    var take = Attempted(fromSecondary) && (!Attempted(target) || (!HasSteps(target) && HasSteps(fromSecondary)));
    if (Attempted(fromSecondary) && Attempted(target) && HasSteps(target) && HasSteps(fromSecondary))
        conflicts.Add((JsonNode?)JsonValue.Create(id));
    if (!take) continue;
    // Copy this case's own evidence tree, then adopt its manifest record.
    var sourceCase = Path.Combine(secondary, "cases", id);
    var targetCase = Path.Combine(primary, "cases", id);
    if (Directory.Exists(sourceCase))
    {
        if (Directory.Exists(targetCase)) Directory.Delete(targetCase, true);
        CopyTree(sourceCase, targetCase);
    }
    var index = primaryManifest["cases"]!.AsArray().IndexOf(target);
    primaryManifest["cases"]![index] = fromSecondary.DeepClone();
    adopted.Add((JsonNode?)JsonValue.Create(id));
}

if (conflicts.Count > 0) throw new InvalidDataException("both shards executed the same case: " + string.Join(",", conflicts.Select(c => c!.GetValue<string>())));
if (!File.Exists(Path.Combine(primary, "registry.json")) && File.Exists(Path.Combine(secondary, "registry.json")))
    File.Copy(Path.Combine(secondary, "registry.json"), Path.Combine(primary, "registry.json"));

var provenance = primaryManifest["run"]!["provenance"] as JsonObject ?? new JsonObject();
provenance["merge"] = new JsonObject
{
    ["shards"] = new JsonArray(
        (JsonNode)new JsonObject { ["dir"] = Path.GetFileName(primary), ["id"] = primaryManifest["run"]!["id"]!.GetValue<string>(), ["adoptedFromOther"] = 0 },
        (JsonNode)new JsonObject { ["dir"] = Path.GetFileName(secondary), ["id"] = secondaryManifest["run"]!["id"]!.GetValue<string>(), ["adopted"] = adopted.Count }),
    ["adoptedCases"] = adopted.DeepClone(),
    ["note"] = "Disjoint shards of the same registry merged for one workbench run; per-case evidence directories copied byte-for-byte, no re-execution.",
};
primaryManifest["run"]!["provenance"] = provenance;
File.WriteAllText(primaryManifestPath + ".tmp", primaryManifest.ToJsonString());
File.Move(primaryManifestPath + ".tmp", primaryManifestPath, true);

var validation = WebProgressDataValidation.Validate(data);
File.WriteAllText(Path.Combine(primary, "validation.json"), validation.ToJsonString());
Console.WriteLine("MERGED adopted=" + adopted.Count + " cases=" + validation["cases"] + " meshes=" + validation["meshes"] + " errors=" + validation["errors"]!.AsArray().Count);
return validation["errors"]!.AsArray().Count == 0 ? 0 : 2;

void CopyTree(string source, string target)
{
    Directory.CreateDirectory(target);
    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(source, file);
        var to = Path.Combine(target, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Copy(file, to, true);
    }
}

string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : at + 1 < args.Length ? args[at + 1] : throw new ArgumentException(name + " requires a value"); }
static string ScriptPath([CallerFilePath] string path = "") => path;

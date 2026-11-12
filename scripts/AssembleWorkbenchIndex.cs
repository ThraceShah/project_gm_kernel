#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Builds the single served workbench data set from already validated run
// directories. One run is the default homepage (every registered modelling
// case); other runs stay listed as explicit version/evidence sources. The
// destination must be a fresh directory so no run is silently dropped: any
// existing destination content is reported and refused.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var source = Path.GetFullPath(Option("--source", "../temp_docs/web-progress/data"), scripts);
var destination = Path.GetFullPath(Option("--destination", ""), scripts);
var latest = Option("--latest", "");
var keep = new List<string>();
for (var i = 0; i < args.Length; i++) if (args[i] == "--run" && i + 1 < args.Length) keep.Add(args[i + 1]);
if (destination.Length == 0 || latest.Length == 0 || keep.Count == 0) throw new ArgumentException("--destination, --latest and at least one --run are required");
if (!keep.Contains(latest, StringComparer.Ordinal)) throw new ArgumentException("--latest must be one of --run");
if (keep.Distinct(StringComparer.Ordinal).Count() != keep.Count) throw new ArgumentException("duplicate --run");
if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
    throw new IOException("destination must be empty; move the previous served data aside instead of overwriting it");
Directory.CreateDirectory(destination);

// Copy the kept runs with the validated atomic publisher (immutable run bytes).
var descriptors = new List<JsonObject>();
foreach (var runId in keep)
{
    WebProgressPublishing.Publish(source, runId, destination);
    var sourceIndex = JsonNode.Parse(File.ReadAllText(Path.Combine(source, "index.json")))!.AsObject();
    var descriptor = sourceIndex["runs"]!.AsArray().Single(r => r!["id"]!.GetValue<string>() == runId)!.DeepClone().AsObject();
    descriptors.Add(descriptor);
}

// Current workbench data companions.
foreach (var name in new[] { "project-overview.json", "overview-events.json" })
{
    var from = Path.Combine(source, name);
    if (!File.Exists(from)) throw new FileNotFoundException("missing current data companion " + name);
    File.Copy(from, Path.Combine(destination, name));
}

var index = new JsonObject
{
    ["schemaVersion"] = 1,
    ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
    ["latestRunId"] = latest,
    ["runs"] = new JsonArray(descriptors.Select(d => (JsonNode)d.DeepClone()).ToArray()),
};
var indexPath = Path.Combine(destination, "index.json");
File.WriteAllText(indexPath + ".tmp", index.ToJsonString());
File.Move(indexPath + ".tmp", indexPath, true);

var validation = WebProgressDataValidation.Validate(destination);
if (validation["errors"]!.AsArray().Count > 0) throw new InvalidDataException(validation.ToJsonString());
Console.WriteLine("WORKBENCH_INDEX latest=" + latest + " runs=" + keep.Count + " cases=" + validation["cases"] + " meshes=" + validation["meshes"] + " triangles=" + validation["triangles"]);
return 0;

string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : at + 1 < args.Length ? args[at + 1] : throw new ArgumentException(name + " requires a value"); }
static string ScriptPath([CallerFilePath] string path = "") => path;

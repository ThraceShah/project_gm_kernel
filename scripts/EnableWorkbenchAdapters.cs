#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Enables the display/structured-capture adapter for registry families whose
// oracle already implements the observer, and corrects stale CLI argument names
// so a registered case really executes its own fixture. Idempotent: it only
// touches the listed ids and never changes ids, titles, parameters or sources.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var path = Path.GetFullPath(Option("--registry", "web-progress-registry.json"), scripts);
var registry = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
var byId = registry["cases"]!.AsArray().OfType<JsonObject>().ToDictionary(c => c["id"]!.GetValue<string>(), StringComparer.Ordinal);

// Planar-special: the oracle reads --three-plane-events / --three-plane-hole /
// --three-face-cylinder / --plane-conic-cut / --conic-corner / --coplanar-tools /
// --finite-axial-contact / --axial-contact-imprint; the registry still carried
// the older "-only" spellings, so those cases ran zero fixtures.
var flagByFixture = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["three-plane-cuts"] = "--three-plane-events",
    ["guarded-three-plane-cuts"] = "--three-plane-events",
    ["guarded-three-plane-hole"] = "--three-plane-hole",
    ["cylindrical-third-face"] = "--three-face-cylinder",
    ["guarded-cylindrical-third-face"] = "--three-face-cylinder",
    ["guarded-plane-conic-cut"] = "--plane-conic-cut",
    ["guarded-trimmed-conic-cut"] = "--plane-conic-cut",
    ["guarded-oblique-conic-cut"] = "--plane-conic-cut",
    ["guarded-tilted-conic-cut"] = "--plane-conic-cut",
    ["guarded-periodic-seam-cut"] = "--plane-conic-cut",
    ["guarded-conic-corner"] = "--conic-corner",
    ["guarded-coplanar-tools"] = "--coplanar-tools",
    ["guarded-coplanar-tools-reversed"] = "--coplanar-tools",
    ["guarded-coplanar-tools-mapped"] = "--coplanar-tools",
    ["guarded-axial-contact-outside"] = "--finite-axial-contact",
    ["guarded-axial-contact-mapped"] = "--finite-axial-contact",
    ["guarded-axial-imprint"] = "--axial-contact-imprint",
    ["guarded-axial-imprint-negative"] = "--axial-contact-imprint",
    ["guarded-axial-imprint-yside"] = "--axial-contact-imprint",
    ["guarded-axial-imprint-mapped"] = "--axial-contact-imprint",
    ["guarded-axial-imprint-axis-reversed"] = "--axial-contact-imprint",
};
var enabled = new JsonArray();
var corrected = new JsonArray();
foreach (var (id, entry) in byId)
{
    if (!id.StartsWith("planar-special.", StringComparison.Ordinal)) continue;
    var fixture = id["planar-special.".Length..];
    if (!flagByFixture.TryGetValue(fixture, out var flag)) continue;
    entry["arguments"] = new JsonArray(flag);
    entry["meshAdapter"] = true;
    enabled.Add((JsonNode?)JsonValue.Create(id));
    corrected.Add((JsonNode?)JsonValue.Create(id));
}

// Nested evaluation: NestedBlendBenchmark now emits a native-evaluation display
// step per fixture (real received surface + actually evaluated samples/normals).
// All six pattern registrations of one fixture share that fixture's display.
foreach (var entry in byId.Values.Where(c => c["suite"]?.GetValue<string>() == "nested-evaluation" && c["meshAdapter"]?.GetValue<bool>() != true))
{
    var argument = entry["arguments"] is JsonArray a && a.Count > 0 ? a[0]!.GetValue<string>() : "";
    var fixture = argument.StartsWith("--fixture=", StringComparison.Ordinal) ? argument["--fixture=".Length..] : "";
    if (fixture.Length == 0) continue;
    var stem = Path.GetFileNameWithoutExtension(fixture.Replace("nested-blend-native/", ""));
    entry["selector"] = stem;
    entry["meshAdapter"] = true;
    enabled.Add((JsonNode?)JsonValue.Create(entry["id"]!.GetValue<string>()));
}

// PK-native XT corpus fixtures: the original case is a PK corpus/schema check
// plus a managed transmit round-trip, so the display is the real PK fixture body
// with an explicit pk-only scope (no GM modelling equivalence is implied).
foreach (var entry in byId.Values.Where(c => c["suite"]?.GetValue<string>() == "pk-corpus" && c["meshAdapter"]?.GetValue<bool>() != true))
{
    entry["meshAdapter"] = true;
    enabled.Add((JsonNode?)JsonValue.Create(entry["id"]!.GetValue<string>()));
}

// Families whose oracle already calls the observer for every fixture: only the
// adapter flag was missing.
foreach (var source in new[] { "scripts/BlendCoverageOracle.cs" })
    foreach (var entry in byId.Values.Where(c => c["source"]?.GetValue<string>() == source && c["meshAdapter"]?.GetValue<bool>() != true))
    {
        entry["meshAdapter"] = true;
        enabled.Add((JsonNode?)JsonValue.Create(entry["id"]!.GetValue<string>()));
    }

registry["version"] = JsonValue.Create("2026-11-12.default-recipes-v6");
File.WriteAllText(path + ".tmp", registry.ToJsonString());
File.Move(path + ".tmp", path, true);
Console.WriteLine($"ENABLED {enabled.Count} (args-corrected {corrected.Count}) registry={Path.GetFileName(path)} version={registry["version"]}");
return 0;

string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : at + 1 < args.Length ? args[at + 1] : throw new ArgumentException(name + " requires a value"); }
static string ScriptPath([CallerFilePath] string path = "") => path;

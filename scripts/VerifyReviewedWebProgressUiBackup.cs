#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// Real local backup CLI only: no portal client, network, unit or producer.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath("..", scripts);
var work = Path.Combine(root, "temp_docs", "publishing-integration", "ui-backup-" + Guid.NewGuid().ToString("N"));
var source = Path.Combine(work, "source"); Directory.CreateDirectory(Path.Combine(source, "data", "runs", "r1"));
File.WriteAllText(Path.Combine(source, "index.html"), "<html>OLD_EXACT_UI</html>");
File.WriteAllText(Path.Combine(source, "data", "index.json"), """
{"schemaVersion":1,"generatedAt":"fixture","latestRunId":"r1","runs":[{"id":"r1","commit":"old","generatedAt":"fixture","label":"fixture","manifest":"runs/r1/manifest.json"}]}
""");
File.WriteAllText(Path.Combine(source, "data", "runs", "r1", "manifest.json"), """
{"schemaVersion":1,"run":{"id":"r1","commit":"old","generatedAt":"fixture","pkVersion":null,"host":{},"configuration":{},"scope":"BCL backup fixture","provenance":{}},"cases":[]}
""");
var checks = 0;
void Check(bool ok, string label) { Console.WriteLine((ok ? "PASS " : "FAIL ") + label); if (!ok) throw new IOException(label); checks++; }
Dictionary<string, string> Hashes(string path) => Directory.GetFiles(path, "*", SearchOption.AllDirectories).ToDictionary(f => Path.GetRelativePath(path, f), f => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f))), StringComparer.Ordinal);
bool Equal(string path, Dictionary<string, string> expected) { var actual = Hashes(path); return actual.Count == expected.Count && expected.All(p => actual.TryGetValue(p.Key, out var h) && h == p.Value); }
void Copy(string from, string to) { foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) { var target = Path.Combine(to, Path.GetRelativePath(from, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); } }
async Task<(int Exit, string Text)> Backup(string label, string release, bool verify = false)
{
    var info = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in new[] { "run", "--file", "scripts/PublishReviewedWebProgressUi.cs", "--", "--mode", "backup", "--work", Path.GetRelativePath(scripts, release), "--previous-bundle", Path.GetRelativePath(scripts, source) }) info.ArgumentList.Add(arg);
    if (verify) info.ArgumentList.Add("--verify-served");
    info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    using var process = Process.Start(info)!;
    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
    var text = await stdout + "\n" + await stderr;
    File.WriteAllText(Path.Combine(work, label + ".log"), "exit=" + process.ExitCode + "\n" + text);
    Console.WriteLine("CLI " + label + " exit=" + process.ExitCode); return (process.ExitCode, text);
}
var expected = Hashes(source);
var normal = Path.Combine(work, "normal");
Check((await Backup("normal", normal)).Exit == 0, "fresh snapshot commits");
var complete = Path.Combine(normal, "previous-snapshot");
Check(Equal(Path.Combine(complete, "bundle"), expected) && File.Exists(Path.Combine(complete, "metadata.json")), "complete old content and metadata share committed directory");
var before = Hashes(complete);
Check((await Backup("normal-retry", normal)).Exit == 0 && Equal(complete, before), "normal retry never rewrites true old backup");
var unattested = await Backup("no-legacy-backfill", normal, true);
Check(unattested.Exit != 0 && unattested.Text.Contains("UI_BACKUP_NOT_SERVED_VERIFIED") && Equal(complete, before), "legacy/unattested metadata cannot be backfilled or verified over network in place");
var partial = Path.Combine(work, "partial");
Directory.CreateDirectory(Path.Combine(partial, "previous-bundle"));
File.WriteAllText(Path.Combine(partial, "previous-bundle", "index.html"), "INTERRUPTED_COPY");
Check((await Backup("legacy-partial", partial)).Exit == 0 && Equal(Path.Combine(partial, "previous-snapshot", "bundle"), expected), "legacy interrupted copy recovers complete snapshot");
var retainedPartial = Directory.GetDirectories(partial, "previous-bundle.incomplete-*").Single();
Check(File.ReadAllText(Path.Combine(retainedPartial, "index.html")) == "INTERRUPTED_COPY", "interrupted evidence is preserved");
var missing = Path.Combine(work, "missing-meta"); Copy(source, Path.Combine(missing, "previous-snapshot", "bundle"));
Check((await Backup("missing-metadata", missing)).Exit == 0 && Equal(Path.Combine(missing, "previous-snapshot", "bundle"), expected), "full directory without metadata is recoverable");
Check(Directory.GetDirectories(missing, "previous-snapshot.incomplete-*").Length == 1, "missing-metadata original directory retained");
File.WriteAllText(Path.Combine(complete, "bundle", "index.html"), "CORRUPTED_BACKUP");
Check((await Backup("damaged-backup", normal)).Exit == 0 && Equal(Path.Combine(complete, "bundle"), expected), "damaged snapshot is repaired only from exact old source");
Check(Directory.GetDirectories(normal, "previous-snapshot.incomplete-*").Any(p => File.ReadAllText(Path.Combine(p, "bundle", "index.html")) == "CORRUPTED_BACKUP"), "damaged original evidence retained");
File.WriteAllText(Path.Combine(complete, "bundle", "index.html"), "CORRUPTED_AGAIN");
File.WriteAllText(Path.Combine(source, "index.html"), "WRONG_NEW_UI");
var damagedBefore = Hashes(complete);
var refused = await Backup("wrong-repair-source", normal);
Check(refused.Exit != 0 && refused.Text.Contains("UI_BACKUP_SOURCE_MISMATCH") && Equal(complete, damagedBefore), "different new source cannot replace real old backup");
Check(!File.Exists(Path.Combine(normal, "published.json")), "backup failure cannot claim publication success");
File.WriteAllText(Path.Combine(source, "index.html"), "<html>OLD_EXACT_UI</html>");
var interrupted = Path.Combine(work, "staging-interruption");
Directory.CreateDirectory(Path.Combine(interrupted, "previous-snapshot.staging-abandoned", "bundle"));
File.WriteAllText(Path.Combine(interrupted, "previous-snapshot.staging-abandoned", "bundle", "index.html"), "STAGED_INTERRUPT");
Check((await Backup("staging-interruption", interrupted)).Exit == 0 && Equal(Path.Combine(interrupted, "previous-snapshot", "bundle"), expected), "abandoned staging never becomes committed rollback state");
Check(File.ReadAllText(Path.Combine(interrupted, "previous-snapshot.staging-abandoned", "bundle", "index.html")) == "STAGED_INTERRUPT", "staging interruption evidence retained");
Check(Equal(source, expected), "all tests preserve original source snapshot");
Console.WriteLine("EVIDENCE " + Path.GetRelativePath(root, work));
Console.WriteLine("ALL PASS " + checks + " UI backup checks; no portal or unit operations executed");
return 0;
static string ScriptPath([CallerFilePath] string path = "") => path;

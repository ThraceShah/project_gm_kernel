#:property TargetFramework=net10.0
global using CliExit = System.Int32;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

var here = Path.GetDirectoryName(Source())!; var repo = Path.GetFullPath("../..", here);
var work = Path.Combine(repo, "temp_docs", "owned-ui-d9-review", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
Console.WriteLine("EVIDENCE " + Path.GetRelativePath(repo, work));
const string tools = "d9fd0605f9beb3e23630c6b54fed3642b2d842da";
const string oldTools = "35498ac5e72eda303883120d8b58cbb6c15fdb33";
const string oldUi = "424e85d6aac32c2afe7cad12cfd04bea4f0037ab";
const string oldReview = "f1fdc36553e9132c88d78a933e503160950460f2";
var nextUi = new string('a', 40); var nextReview = new string('b', 40); var checks = 0;
void Require(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
string Sha(string file) => Hash(File.ReadAllBytes(file));
Dictionary<string, string> Files(string dir) => Directory.Exists(dir) ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories).ToDictionary(f => Path.GetRelativePath(dir, f).Replace('\\', '/'), Sha, StringComparer.Ordinal) : new(StringComparer.Ordinal);
string Tree(Dictionary<string, string> files) { using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); foreach (var pair in files.OrderBy(p => p.Key, StringComparer.Ordinal)) { hash.AppendData(Encoding.UTF8.GetBytes(pair.Key)); hash.AppendData(Encoding.UTF8.GetBytes(pair.Value)); } return Convert.ToHexStringLower(hash.GetHashAndReset()); }
string DropHash(Dictionary<string, string> files) => Hash(Encoding.UTF8.GetBytes(string.Join("\n", files.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value))));
bool Equal(Dictionary<string, string> left, Dictionary<string, string> right) => left.Count == right.Count && left.All(p => right.TryGetValue(p.Key, out var v) && p.Value == v);
JsonObject Read(string path) => JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
void Put(string path, string value) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, value); }
void Save(string path, JsonNode value) => Put(path, value.ToJsonString());
void Copy(string from, string to) { foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) { var target = Path.Combine(to, Path.GetRelativePath(from, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); } }
void Frozen(string dir) { foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead); foreach (var folder in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length).Append(dir)) File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute); }
async Task<(CliExit Code, string Text)> Run(string label, string cwd, string executable, string[] arguments, string? stub = null)
{
    var info = new ProcessStartInfo(executable) { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in arguments) info.ArgumentList.Add(arg); info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    if (stub is not null) { info.Environment["OWNED_REVIEW_STATE"] = stub; info.Environment["PATH"] = Path.Combine(work, "stub", "bin", "Debug", "net10.0") + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"); }
    using var process = Process.Start(info)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)); try { await process.WaitForExitAsync(timeout.Token); } catch { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); throw; }
    var text = await output + "\n" + await error; Put(Path.Combine(work, "logs", label + ".log"), "exit=" + process.ExitCode + "\n" + text); Console.WriteLine("CLI " + label + " exit=" + process.ExitCode); return (process.ExitCode, text);
}
async Task<byte[]> GitFile(string commit, string name)
{
    var info = new ProcessStartInfo("git") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true }; foreach (var arg in new[] { "show", commit + ":scripts/" + name }) info.ArgumentList.Add(arg);
    using var process = Process.Start(info)!; using var bytes = new MemoryStream(); var output = process.StandardOutput.BaseStream.CopyToAsync(bytes); var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); await output; if (process.ExitCode != 0) throw new Exception(await error); return bytes.ToArray();
}
var names = new[] { "Directory.Build.props", "PublishReviewedWebProgressUi.cs", "ConfigureReviewedWebProgressUiHook.cs", "ReviewedWebProgressRelease.cs", "WebProgressDataValidation.cs", "WebProgressPublishing.cs", "WebProgressMetadata.cs" };
var fixedFiles = new Dictionary<string, byte[]>(); var legacyFiles = new Dictionary<string, byte[]>();
foreach (var name in names) { fixedFiles[name] = await GitFile(tools, name); if (name != "ReviewedWebProgressRelease.cs") legacyFiles[name] = await GitFile(oldTools, name); }
Save(Path.Combine(work, "fixed-source-hashes.json"), new JsonObject(fixedFiles.Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key, JsonValue.Create(Hash(p.Value))))));
var stubProject = Path.Combine(work, "stub");
Put(Path.Combine(stubProject, "Stub.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>");
Put(Path.Combine(stubProject, "Program.cs"), """
using System.Text.Json.Nodes;
var path=Environment.GetEnvironmentVariable("OWNED_REVIEW_STATE")??throw new Exception("stub state required");var s=JsonNode.Parse(File.ReadAllBytes(path))!;
File.AppendAllText(path+".calls",string.Join(" ",args)+"\n");
if(args[0]=="static"){var exit=s["portalExit"]!.GetValue<int>();if(exit!=0)return exit;File.WriteAllText(s["servedFile"]!.GetValue<string>(),args[Array.IndexOf(args,"--dir")+1]);return 0;}
var a=args.Where(x=>x!="--user").ToArray();
switch(a[0]){
case "show":if(a.Contains("--property=LoadState")){Console.WriteLine("loaded");return 0;}
if(a.Contains("--property=ExecStartPost")){var command=File.ReadAllLines(s["hook"]!.GetValue<string>()).Single(x=>x.StartsWith("ExecStartPost=dotnet "))["ExecStartPost=".Length..].Replace("%h/",Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)+"/");Console.WriteLine(s["postOverride"]?.GetValue<string>()??("{ path=dotnet ; argv[]="+command+" ; ignore_errors=no ; }"));return 0;}
Console.WriteLine("ActiveState="+s["writer"]+"\nSubState="+s["sub"]+"\nMainPID="+s["pid"]+"\nJob="+s["job"]+"\nTasksCurrent="+s["tasks"]+"\nControlGroup="+s["group"]);return 0;
case "cat":Console.WriteLine("[Service]\nExecStart=fixture-producer");return 0;
case "is-active":Console.WriteLine(s["active"]!.GetValue<bool>()?"active":"inactive");return s["active"]!.GetValue<bool>()?0:3;
case "is-enabled":Console.WriteLine(s["enabled"]);return s["enabled"]!.GetValue<string>()=="enabled"?0:1;
case "stop":case "start":if(!a[1].EndsWith(".timer"))return 99;s["active"]=a[0]=="start";File.WriteAllText(path,s.ToJsonString());return 0;
case "daemon-reload":return 0;
default:return 99;}
""");
Require((await Run("stub-build", repo, "dotnet", ["build", Path.Combine(stubProject, "Stub.csproj"), "-m:1", "-p:UseSharedCompilation=false"])).Code == 0, "private managed stubs built");
var bin = Path.Combine(stubProject, "bin", "Debug", "net10.0"); foreach (var name in new[] { "systemctl", "portalctl" }) { File.Copy(Path.Combine(bin, "Stub"), Path.Combine(bin, name)); File.SetUnixFileMode(Path.Combine(bin, name), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
using var portPicker = new TcpListener(IPAddress.Loopback, 0); portPicker.Start(); var port = ((IPEndPoint)portPicker.LocalEndpoint).Port; portPicker.Stop();
using var listener = new HttpListener(); listener.Prefixes.Add("http://127.0.0.1:" + port + "/"); listener.Start(); using var cancel = new CancellationTokenSource();
var servedFile = Path.Combine(work, "served.txt"); var requests = new JsonArray(); var corruptAsset = false;
var server = Task.Run(async () => { while (!cancel.IsCancellationRequested) { HttpListenerContext c; try { c = await listener.GetContextAsync().WaitAsync(cancel.Token); } catch (OperationCanceledException) { break; } var relative = c.Request.Url!.AbsolutePath.TrimStart('/'); var root = File.ReadAllText(servedFile); var path = Path.GetFullPath(relative, root); if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(path)) c.Response.StatusCode = 404; else { var bytes = corruptAsset && relative.EndsWith(".js", StringComparison.Ordinal) ? Encoding.UTF8.GetBytes("WRONG_SERVED_ASSET") : File.ReadAllBytes(path); c.Response.ContentLength64 = bytes.Length; await c.Response.OutputStream.WriteAsync(bytes); requests.Add((JsonNode)new JsonObject { ["path"] = relative, ["sha256"] = Hash(bytes), ["length"] = bytes.Length }); } c.Response.Close(); } });
Console.WriteLine("OWN_LOOPBACK port=" + port);
void Bundle(string dir, string label)
{
    Put(Path.Combine(dir, "index.html"), "<script src=\"/apps/gm-kernel-progress/assets/app.js\"></script>" + label); Put(Path.Combine(dir, "assets", "app.js"), "BCL_" + label);
    Put(Path.Combine(dir, "data", "index.json"), "{\"schemaVersion\":1,\"generatedAt\":\"fixture\",\"latestRunId\":\"r\",\"runs\":[{\"id\":\"r\",\"commit\":\"fixture\",\"generatedAt\":\"fixture\",\"manifest\":\"runs/r/manifest.json\",\"label\":\"fixture\"}]}");
    Put(Path.Combine(dir, "data", "runs", "r", "manifest.json"), "{\"schemaVersion\":1,\"run\":{\"id\":\"r\",\"commit\":\"fixture\",\"generatedAt\":\"fixture\",\"pkVersion\":null,\"host\":{},\"configuration\":{},\"scope\":\"BCL fixture\",\"provenance\":{}},\"cases\":[]}");
}
string Manifest(string release, string commit, string ui, string review)
{
    var files = new JsonObject(); foreach (var dir in new[] { "tools", "ui", "previous-static" }) foreach (var pair in Files(Path.Combine(release, dir))) files[dir + "/" + pair.Key] = pair.Value;
    Save(Path.Combine(release, "release.json"), new JsonObject { ["schemaVersion"] = 1, ["toolsCommit"] = commit, ["uiCommit"] = ui, ["review"] = review, ["uiHash"] = Tree(Files(Path.Combine(release, "ui"))), ["oldBundleHash"] = Tree(Files(Path.Combine(release, "previous-static"))), ["files"] = files });
    foreach (var dir in new[] { "tools", "ui", "previous-static" }) Frozen(Path.Combine(release, dir)); return Sha(Path.Combine(release, "release.json"));
}
var scenario = Path.Combine(work, "scenario"); var prior = Path.Combine(scenario, "prior"); var next = Path.Combine(scenario, "next"); var units = Path.Combine(scenario, "units"); var dropins = Path.Combine(units, "gm-kernel-progress-refresh.service.d"); var hook = Path.Combine(dropins, "zz-reviewed-ui.conf"); var stubState = Path.Combine(scenario, "stub.json");
var oldScripts = Path.Combine(prior, "tools", "scripts"); var nextScripts = Path.Combine(next, "tools", "scripts"); var nextWork = Path.Combine(next, "work"); var producer = Path.Combine(scenario, "producer");
foreach (var p in legacyFiles) { Directory.CreateDirectory(oldScripts); File.WriteAllBytes(Path.Combine(oldScripts, p.Key), p.Value); }
foreach (var p in fixedFiles) { Directory.CreateDirectory(nextScripts); var bytes = p.Value; if (p.Key == "ReviewedWebProgressRelease.cs") bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/", "http://127.0.0.1:" + port + "/", StringComparison.Ordinal)); File.WriteAllBytes(Path.Combine(nextScripts, p.Key), bytes); }
Bundle(Path.Combine(prior, "previous-static"), "PRE424"); Bundle(Path.Combine(scenario, "current"), "UI424"); Copy(Path.Combine(scenario, "current"), Path.Combine(prior, "ui")); Directory.Delete(Path.Combine(prior, "ui", "data"), true); Copy(Path.Combine(scenario, "current"), Path.Combine(next, "previous-static")); Copy(Path.Combine(scenario, "current", "data"), producer); Bundle(Path.Combine(next, "ui"), "NEXT"); Directory.Delete(Path.Combine(next, "ui", "data"), true);
var priorPin = Manifest(prior, oldTools, oldUi, oldReview); var nextPin = Manifest(next, tools, nextUi, nextReview);
Put(Path.Combine(dropins, "publish.conf"), "[Service]\nExecStartPost=fixture-static\n"); var priorBackup = Path.Combine(prior, "work", "hook-backup"); Copy(dropins, Path.Combine(priorBackup, "dropins"));
string OldRel(string path) => Path.GetRelativePath(oldScripts, path); string Rel(string path) => Path.GetRelativePath(nextScripts, path);
var oldCommand = "dotnet run --file %h/" + Path.GetRelativePath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Path.Combine(oldScripts, "PublishReviewedWebProgressUi.cs")) + " -- --mode publish --execute --dist ../../ui --data " + OldRel(producer) + " --baseline-data ../../previous-static/data --work ../../work --portalctl " + OldRel(Path.Combine(bin, "portalctl")) + " --previous-bundle ../../previous-static --ui-commit " + oldUi + " --dist-hash " + Read(Path.Combine(prior, "release.json"))["uiHash"] + " --review " + oldReview;
Put(hook, "[Service]\nExecStartPost=\nExecStartPost=" + oldCommand + "\n");
var oldState = new JsonObject { ["schemaVersion"] = 1, ["unitDir"] = OldRel(units), ["uiCommit"] = oldUi, ["uiHash"] = Read(Path.Combine(prior, "release.json"))["uiHash"]!.DeepClone(), ["review"] = oldReview, ["timerActive"] = true, ["timerEnabled"] = "enabled", ["dropinsExisted"] = true, ["originalDropinsHash"] = DropHash(Files(Path.Combine(priorBackup, "dropins"))), ["hookHash"] = Sha(hook) };
Save(Path.Combine(priorBackup, "state.json"), oldState); Save(Path.Combine(prior, "work", "published.json"), new JsonObject { ["uiCommit"] = oldUi, ["uiHash"] = oldState["uiHash"]!.DeepClone(), ["review"] = oldReview });
var previousStatePin = Sha(Path.Combine(priorBackup, "state.json")); var previousFiles = Files(prior); var originalDropins = Files(dropins);
Save(stubState, new JsonObject { ["active"] = true, ["enabled"] = "enabled", ["writer"] = "inactive", ["sub"] = "dead", ["pid"] = "0", ["job"] = "", ["tasks"] = "0", ["group"] = "", ["hook"] = hook, ["portalExit"] = 0, ["servedFile"] = servedFile }); File.WriteAllText(servedFile, Path.Combine(scenario, "current"));
var arguments = new[] { "--work", "../../work", "--unit-dir", Rel(units), "--dist", "../../ui", "--data", Rel(producer), "--baseline-data", "../../previous-static/data", "--portalctl", Rel(Path.Combine(bin, "portalctl")), "--previous-bundle", "../../previous-static", "--ui-commit", nextUi, "--dist-hash", Read(Path.Combine(next, "release.json"))["uiHash"]!.GetValue<string>(), "--review", nextReview, "--release-sha256", nextPin, "--previous-release", Rel(prior), "--previous-release-sha256", priorPin, "--previous-state-sha256", previousStatePin };
Task<(CliExit Code, string Text)> Cli(string label, string file, string mode, params string[] extra) => Run(label, repo, "dotnet", ["run", "--no-cache", "--file", Path.Combine(nextScripts, file + ".cs"), "--", "--mode", mode, "--execute", .. arguments, .. extra], stubState);
async Task RefuseInstall(string label, string marker)
{
    var before = Files(dropins); var calls = File.Exists(stubState + ".calls") ? File.ReadAllLines(stubState + ".calls").Length : 0; var result = await Cli(label, "ConfigureReviewedWebProgressUiHook", "install"); var added = File.ReadAllLines(stubState + ".calls").Skip(calls);
    Require(result.Code == 134 && result.Text.Contains(marker) && Equal(before, Files(dropins)) && !Directory.Exists(Path.Combine(nextWork, "hook-backup")) && !added.Any(x => x.Contains(" stop ") || x.Contains(" start ") || x.Contains("daemon-reload")), label + " rejects before control mutation and keeps exact dropins");
}
try
{
    Require((await Cli("prepare", "PublishReviewedWebProgressUi", "prepare")).Code == 0, "prepare real entry");
    Put(Path.Combine(nextWork, "previous-snapshot.staging-abandoned", "interrupted.log"), "DO_NOT_PROMOTE");
    Require((await Cli("verified-backup", "PublishReviewedWebProgressUi", "backup", "--verify-served")).Code == 0 && File.ReadAllText(Path.Combine(nextWork, "previous-snapshot.staging-abandoned", "interrupted.log")) == "DO_NOT_PROMOTE", "whole served backup commits while incomplete staging stays evidence");
    var snapshot = Path.Combine(nextWork, "previous-snapshot"); var metadata = Path.Combine(snapshot, "metadata.json"); var metaBytes = File.ReadAllBytes(metadata);
    arguments[Array.IndexOf(arguments, "--previous-release-sha256") + 1] = new string('0', 64); await RefuseInstall("bad-previous-pin", "UI_TRUST_RECORD_MISMATCH"); arguments[Array.IndexOf(arguments, "--previous-release-sha256") + 1] = priorPin;
    var sibling = Path.Combine(dropins, "publish.conf"); var siblingBytes = File.ReadAllBytes(sibling); File.AppendAllText(sibling, "# EXTERNAL\n"); await RefuseInstall("foreign-sibling", "drop-ins changed externally"); File.WriteAllBytes(sibling, siblingBytes);
    var stub = Read(stubState); stub["postOverride"] = "{ path=dotnet ; argv[]=foreign ; ignore_errors=no ; }"; Save(stubState, stub); await RefuseInstall("effective-argv", "UI_EFFECTIVE_POST_MISMATCH"); stub.Remove("postOverride"); Save(stubState, stub);
    File.Delete(metadata); await RefuseInstall("missing-backup-metadata", "UI_BACKUP_INVALID"); File.WriteAllBytes(metadata, metaBytes);
    var savedBundle = Path.Combine(snapshot, "bundle"); var savedHtml = File.ReadAllBytes(Path.Combine(savedBundle, "index.html")); var savedJs = File.ReadAllBytes(Path.Combine(savedBundle, "assets", "app.js"));
    File.Copy(Path.Combine(prior, "previous-static", "index.html"), Path.Combine(savedBundle, "index.html"), true); File.Copy(Path.Combine(prior, "previous-static", "assets", "app.js"), Path.Combine(savedBundle, "assets", "app.js"), true); var wrong = Read(metadata); wrong["treeHash"] = Tree(Files(savedBundle)); Save(metadata, wrong);
    Require(Sha(Path.Combine(savedBundle, "data", "index.json")) == Sha(Path.Combine(next, "previous-static", "data", "index.json")), "wrong UI control keeps same index and valid recomputed tree metadata"); await RefuseInstall("wrong-ui-same-index-rehashed", "UI_PREVIOUS_BUNDLE_IDENTITY_MISMATCH"); foreach (var file in new[] { Path.Combine(savedBundle, "index.html"), Path.Combine(savedBundle, "assets", "app.js") }) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite); File.WriteAllBytes(Path.Combine(savedBundle, "index.html"), savedHtml); File.WriteAllBytes(Path.Combine(savedBundle, "assets", "app.js"), savedJs); File.WriteAllBytes(metadata, metaBytes);
    var pristine = Path.Combine(scenario, "next-pristine"); Copy(next, pristine); foreach (var dir in new[] { "tools", "ui", "previous-static" }) Frozen(Path.Combine(pristine, dir));
    stub["job"] = "queued-123"; Save(stubState, stub); var queued = await Cli("queued-writer", "ConfigureReviewedWebProgressUiHook", "install"); Require(queued.Code == 134 && queued.Text.Contains("UI_WRITER_NOT_IDLE") && Equal(originalDropins, Files(dropins)) && Directory.Exists(Path.Combine(nextWork, "hook-backup")) && !Read(stubState)["active"]!.GetValue<bool>(), "queued writer refuses swap and retains committed backup with timer stopped");
    // Fixture fork only, not an operational retry: retain the WHOLE failed release,
    // then use the pristine pre-install fixture for the separate success scenario.
    Directory.Move(next, Path.Combine(scenario, "next-queued-evidence")); Directory.Move(pristine, next); stub["job"] = ""; stub["active"] = true; Save(stubState, stub);
    Require((await Cli("install", "ConfigureReviewedWebProgressUiHook", "install")).Code == 0 && !Read(stubState)["active"]!.GetValue<bool>() && !Equal(originalDropins, Files(dropins)), "proven owned hook replaced with timer stopped");
    Require((await Cli("resume-before-publish", "ConfigureReviewedWebProgressUiHook", "resume")).Code == 134 && !Read(stubState)["active"]!.GetValue<bool>(), "no publication cannot resume");
    stub = Read(stubState); stub["portalExit"] = 23; Save(stubState, stub); var upload = await Cli("upload-failure", "PublishReviewedWebProgressUi", "publish"); Require(upload.Code == 134 && upload.Text.Contains("original exit=23") && !File.Exists(Path.Combine(nextWork, "published.json")), "upload original exit23 retained without successful record");
    stub["portalExit"] = 0; Save(stubState, stub); corruptAsset = true; var verify = await Cli("asset-verify-failure", "PublishReviewedWebProgressUi", "publish"); corruptAsset = false; Require(verify.Code == 134 && verify.Text.Contains("UI_SERVED_BUNDLE_MISMATCH assets/app.js") && !File.Exists(Path.Combine(nextWork, "published.json")), "portal success with wrong served asset refuses publication claim");
    Require((await Cli("publish-recovery", "PublishReviewedWebProgressUi", "publish")).Code == 0, "publication recovers with whole bundle verified");
    var publication = Path.Combine(nextWork, "published.json"); var pubBytes = File.ReadAllBytes(publication); var pub = Read(publication); pub["review"] = new string('e', 40); Save(publication, pub); Require((await Cli("wrong-review-resume", "ConfigureReviewedWebProgressUiHook", "resume")).Code == 134 && !Read(stubState)["active"]!.GetValue<bool>(), "different publication review cannot resume"); File.WriteAllBytes(publication, pubBytes);
    Require((await Cli("resume", "ConfigureReviewedWebProgressUiHook", "resume")).Code == 0 && Read(stubState)["active"]!.GetValue<bool>() && Read(stubState)["enabled"]!.GetValue<string>() == "enabled", "matched publication restores actual original active enabled preferences");
    // Rollback's runbook precondition: only the private stub timer is stopped here.
    stub = Read(stubState); stub["active"] = false; Save(stubState, stub); File.Delete(metadata); Require((await Cli("partial-rollback", "PublishReviewedWebProgressUi", "rollback")).Code == 134 && !File.Exists(Path.Combine(nextWork, "ROLLED_BACK")), "incomplete backup cannot claim rollback"); File.WriteAllBytes(metadata, metaBytes);
    Require((await Cli("rollback", "PublishReviewedWebProgressUi", "rollback")).Code == 0 && File.ReadAllText(servedFile) == savedBundle && Equal(Files(Path.Combine(scenario, "current")), Files(savedBundle)), "rollback restores exact current UI424 complete bundle not pre424 same-index bundle");
    File.AppendAllText(sibling, "# KEEP_USER_EDIT\n"); var edited = Sha(sibling); Require((await Cli("edited-restore", "ConfigureReviewedWebProgressUiHook", "restore")).Code == 134 && Sha(sibling) == edited && !Read(stubState)["active"]!.GetValue<bool>(), "restore preserves external edit without resume"); File.WriteAllBytes(sibling, siblingBytes);
    Require((await Cli("restore", "ConfigureReviewedWebProgressUiHook", "restore")).Code == 0 && Equal(originalDropins, Files(dropins)) && Read(stubState)["active"]!.GetValue<bool>(), "restore recovers all original dropins and timer active bit");
    Require(Equal(previousFiles, Files(prior)) && Sha(Path.Combine(priorBackup, "state.json")) == previousStatePin && !File.ReadAllLines(stubState + ".calls").Any(x => x.Contains(" enable ") || x.Contains(" disable ")), "legacy manifest state tools unchanged and no enable disable command");
}
finally { cancel.Cancel(); listener.Stop(); await server; Save(Path.Combine(work, "http.json"), requests); Console.WriteLine("OWN_LOOPBACK_CLOSED port=" + port); }
Console.WriteLine("SUMMARY " + checks + "/" + checks + " independent owned UI controls; no real unit or portal operation");
static string Source([CallerFilePath] string path = "") => path;

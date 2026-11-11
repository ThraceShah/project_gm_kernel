using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// Shared, UI-only release identity checks. No service control or user config.
internal static class ReviewedWebProgressRelease
{
    internal static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static void Safe(string path)
    {
        if (!WebProgressSafePath.IsSafeChain(path, out _)) throw new IOException("UI_PATH_UNSAFE");
        if (Directory.Exists(path)) foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new IOException("UI_PATH_UNSAFE");
    }
    internal static Dictionary<string, string> Hashes(string path, bool uiOnly = false)
    {
        Safe(path);
        return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .Select(f => (File: f, Relative: Path.GetRelativePath(path, f).Replace('\\', '/')))
            .Where(p => !uiOnly || !p.Relative.StartsWith("data/", StringComparison.Ordinal))
            .ToDictionary(p => p.Relative, p => Sha(File.ReadAllBytes(p.File)), StringComparer.Ordinal);
    }
    internal static string TreeHash(Dictionary<string, string> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (path, sha) in files.OrderBy(p => p.Key, StringComparer.Ordinal))
        { hash.AppendData(Encoding.UTF8.GetBytes(path)); hash.AppendData(Encoding.UTF8.GetBytes(sha)); }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    internal static JsonObject Pinned(string path, string expected)
    {
        Safe(path);
        if (expected.Length != 64 || Sha(File.ReadAllBytes(path)) != expected) throw new IOException("UI_TRUST_RECORD_MISMATCH");
        return JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
    }
    internal static JsonObject Frozen(string release, string expected)
    {
        Safe(release);
        var record = Pinned(Path.Combine(release, "release.json"), expected);
        var files = record["files"]!.AsObject();
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var dir in new[] { "tools", "ui", "previous-static" })
        {
            var path = Path.Combine(release, dir);
            foreach (var (name, sha) in Hashes(path)) actual.Add(dir + "/" + name, sha);
            foreach (var entry in Directory.GetFiles(path, "*", SearchOption.AllDirectories).Prepend(path).Concat(Directory.GetDirectories(path, "*", SearchOption.AllDirectories)))
                if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(entry) & (UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
                    throw new IOException("UI_RELEASE_NOT_FROZEN");
        }
        if (actual.Count != files.Count || actual.Any(p => files[p.Key]?.GetValue<string>() != p.Value)
            || TreeHash(Hashes(Path.Combine(release, "ui"))) != record["uiHash"]!.GetValue<string>()
            || TreeHash(Hashes(Path.Combine(release, "previous-static"))) != record["oldBundleHash"]!.GetValue<string>())
            throw new IOException("UI_RELEASE_CONTENT_MISMATCH");
        foreach (var name in new[] { "PublishReviewedWebProgressUi.cs", "ConfigureReviewedWebProgressUiHook.cs" })
            if (!files.ContainsKey("tools/scripts/" + name)) throw new IOException("UI_RELEASE_TOOLS_MISSING");
        return record;
    }
    internal static void CompleteBundle(string bundle)
    {
        Safe(bundle);
        var html = File.ReadAllText(Path.Combine(bundle, "index.html"));
        foreach (Match match in Regex.Matches(html, "(?:src|href)=[\"']([^\"']+)[\"']"))
        {
            var asset = match.Groups[1].Value;
            const string prefix = "/apps/gm-kernel-progress/";
            if (asset.StartsWith(prefix, StringComparison.Ordinal)) asset = asset[prefix.Length..];
            if (asset.StartsWith("assets/", StringComparison.Ordinal) && (!WebProgressSafePath.IsWithin(bundle, Path.Combine(bundle, asset)) || !File.Exists(Path.Combine(bundle, asset))))
                throw new IOException("UI_BACKUP_ASSET_MISSING");
        }
        if (WebProgressDataValidation.Validate(Path.Combine(bundle, "data"))["errors"]!.AsArray().Count != 0)
            throw new IOException("UI_BACKUP_CLOSURE_INVALID");
    }
    internal static bool ValidBackup(string snapshot, bool served = false)
    {
        var bundle = Path.Combine(snapshot, "bundle"); var metadata = Path.Combine(snapshot, "metadata.json");
        if (!Directory.Exists(bundle) || !File.Exists(metadata)) return false;
        Safe(snapshot);
        try
        {
            var record = JsonNode.Parse(File.ReadAllBytes(metadata))!;
            CompleteBundle(bundle);
            return record["schemaVersion"]?.GetValue<int>() == 1
                && record["treeHash"]?.GetValue<string>() == TreeHash(Hashes(bundle))
                && (!served || record["servedVerified"]?.GetValue<bool>() == true);
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or InvalidOperationException or IOException) { return false; }
    }
    internal static async Task VerifyServed(string bundle)
    {
        CompleteBundle(bundle);
        var before = Hashes(bundle);
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(20) };
        foreach (var (path, sha) in before)
            if (Sha(await http.GetByteArrayAsync("https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/" + path)) != sha)
                throw new IOException("UI_SERVED_BUNDLE_MISMATCH " + path);
        if (TreeHash(Hashes(bundle)) != TreeHash(before)) throw new IOException("UI_SERVED_SOURCE_CHANGED");
    }
    internal static string HookCommand(string hook)
    {
        var lines = hook.Split('\n');
        if (lines.Length != 4 || lines[0] != "[Service]" || lines[1] != "ExecStartPost=" || !lines[2].StartsWith("ExecStartPost=dotnet run --file ", StringComparison.Ordinal) || lines[3] != "")
            throw new IOException("UI_UNKNOWN_OWNED_HOOK");
        return lines[2]["ExecStartPost=".Length..].Replace("%h/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/", StringComparison.Ordinal);
    }
    internal static void Effective(string text, string command)
    {
        var argv = Regex.Matches(text, @"argv\[\]=(.*?) ;", RegexOptions.Singleline);
        if (Regex.Matches(text, @"\bpath=").Count != 1 || argv.Count != 1 || argv[0].Groups[1].Value != command || !text.Contains("ignore_errors=no", StringComparison.Ordinal))
            throw new IOException("UI_EFFECTIVE_POST_MISMATCH");
    }
}

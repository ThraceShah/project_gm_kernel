#!/usr/bin/env dotnet

using System.IO.Compression;
using System.Text;
using System.Runtime.CompilerServices;

var scriptDirectory = GetScriptDirectory();
var inputs = args.Length == 0 ? [Path.GetFullPath(Path.Combine(scriptDirectory, "..", "bin"))] : args.Select(Path.GetFullPath).ToArray();
var forbiddenNames = new[] { ".sch_txt" };
var forbiddenText = new[] { ": SCHEMA FILE created by modeller version", "** end of schema", "third_party/parasolid/schema", "third_party\\parasolid\\schema" };
var violations = new List<string>();

foreach (var input in inputs)
{
    if (Directory.Exists(input))
    {
        foreach (var path in Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories)) InspectFile(path, Path.GetRelativePath(input, path));
    }
    else if (File.Exists(input))
    {
        if (Path.GetExtension(input).Equals(".nupkg", StringComparison.OrdinalIgnoreCase)) InspectArchive(input);
        else InspectFile(input, Path.GetFileName(input));
    }
    else violations.Add($"artifact does not exist: {input}");
}

if (violations.Count != 0)
{
    Console.Error.WriteLine("XT artifact schema-leak scan failed:");
    foreach (var violation in violations) Console.Error.WriteLine("  " + violation);
    return 1;
}
Console.WriteLine($"XT artifact schema-leak scan passed for {inputs.Length} input(s).");
return 0;

void InspectArchive(string path)
{
    using var archive = ZipFile.OpenRead(path);
    foreach (var entry in archive.Entries)
    {
        if (entry.Length == 0) continue;
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        Inspect(entry.FullName, memory.GetBuffer().AsSpan(0, checked((int)memory.Length)));
    }
}

void InspectFile(string path, string display)
{
    var info = new FileInfo(path);
    if (info.Length > int.MaxValue) { violations.Add($"too large to scan: {display}"); return; }
    Inspect(display, File.ReadAllBytes(path));
}

void Inspect(string display, ReadOnlySpan<byte> bytes)
{
    var lower = display.ToLowerInvariant();
    if (forbiddenNames.Any(lower.Contains)) violations.Add($"forbidden entry name: {display}");
    var text = Encoding.Latin1.GetString(bytes);
    if (forbiddenText.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase))) violations.Add($"raw schema text or private schema path embedded in: {display}");
}

static string GetScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

#!/usr/bin/env dotnet run
#:property AssemblyName=GenerateParasolidXtCorpus

using System.Diagnostics;
using System.Text.Json;

var repositoryRoot = FindRepositoryRoot(Directory.GetCurrentDirectory());
var caseDirectory = Path.Combine(repositoryRoot, "scripts", "ParasolidXtCorpusCases");
var groupScripts = Directory.Exists(caseDirectory)
    ? Directory.EnumerateFiles(caseDirectory, "*.cs", SearchOption.TopDirectoryOnly)
        .Where(static path => !string.Equals(Path.GetFileNameWithoutExtension(path), "TemplateSmoke", StringComparison.Ordinal))
        .OrderBy(static path => path, StringComparer.Ordinal)
        .ToArray()
    : [];

var options = ParseArguments(args, repositoryRoot, Path.Combine(repositoryRoot, "scripts", "GenerateParasolidXtCorpus.cs"));
if (options.Invalid)
{
    PrintUsage();
    return 2;
}

var groups = new List<CaseGroup>(groupScripts.Length);
foreach (var script in groupScripts)
{
    var groupName = Path.GetFileNameWithoutExtension(script);
    var result = await InvokeAsync(script, ["--list-json"], repositoryRoot);
    if (result.ExitCode != 0)
    {
        Console.Error.WriteLine($"failed to list case group {groupName}:");
        if (result.StandardError.Length != 0)
            Console.Error.Write(result.StandardError);
        if (result.StandardOutput.Length != 0)
            Console.Error.Write(result.StandardOutput);
        return 1;
    }

    CaseListEntry[] entries;
    try
    {
        entries = JsonSerializer.Deserialize(result.StandardOutput, CorpusJsonContext.Default.CaseListEntryArray) ?? [];
    }
    catch (JsonException exception)
    {
        Console.Error.WriteLine($"invalid --list-json output from {groupName}: {exception.Message}");
        return 1;
    }

    groups.Add(new CaseGroup(groupName, script, entries.OrderBy(static entry => entry.CaseId, StringComparer.Ordinal).ToArray()));
}

var duplicateIds = groups
    .SelectMany(static group => group.Cases.Select(entry => (entry.CaseId, group.Name)))
    .GroupBy(static item => item.CaseId, StringComparer.Ordinal)
    .Where(static grouping => grouping.Count() > 1)
    .ToArray();
if (duplicateIds.Length != 0)
{
    foreach (var duplicate in duplicateIds)
        Console.Error.WriteLine("duplicate case ID: " + duplicate.Key + " in " + string.Join(", ", duplicate.Select(static item => item.Name)));
    return 1;
}

if (options.List)
{
    foreach (var group in groups)
    {
        Console.WriteLine(group.Name);
        foreach (var entry in group.Cases)
            Console.WriteLine("  " + entry.CaseId + "\t" + entry.Producer + "\t" + entry.Description);
    }

    return 0;
}

var selectedGroups = SelectGroups(groups, options.Group, options.CaseId);
if (selectedGroups.Count == 0)
{
    Console.Error.WriteLine("no case-group or case matched the requested selection");
    return 2;
}

var failures = 0;
foreach (var group in selectedGroups)
{
    var childArguments = new List<string>();
    if (options.Check)
        childArguments.Add("--check");
    if (options.CaseId is not null)
    {
        childArguments.Add("--case");
        childArguments.Add(options.CaseId);
    }

    childArguments.Add("--output");
    childArguments.Add(Path.Combine(options.OutputRoot, group.Name));

    Console.WriteLine($"RUN {group.Name} ({group.Cases.Length} case(s))");
    var result = await InvokeAsync(group.ScriptPath, childArguments, repositoryRoot);
    if (result.StandardOutput.Length != 0)
        Console.Write(result.StandardOutput);
    if (result.StandardError.Length != 0)
        Console.Error.Write(result.StandardError);
    if (result.ExitCode != 0)
        failures++;
}

if (options.Check)
{
    var bodyGroup = groups.FirstOrDefault(static group => string.Equals(group.Name, "BodyBasics", StringComparison.Ordinal));
    if (bodyGroup is not null)
    {
        Console.WriteLine("RUN " + bodyGroup.Name + " rejection contracts");
        var result = await InvokeAsync(bodyGroup.ScriptPath, ["--check-rejections"], repositoryRoot);
        if (result.StandardOutput.Length != 0)
            Console.Write(result.StandardOutput);
        if (result.StandardError.Length != 0)
            Console.Error.Write(result.StandardError);
        if (result.ExitCode != 0)
            failures++;
    }
}

Console.WriteLine($"corpus groups={selectedGroups.Count} failures={failures} mode={(options.Check ? "check" : "generate")}");
return failures == 0 ? 0 : 1;

static string FindRepositoryRoot(string startDirectory)
{
    var current = Path.GetFullPath(startDirectory);
    while (true)
    {
        if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, "AGENTS.md")))
            return current;

        var parent = Directory.GetParent(current)?.FullName;
        if (parent is null || string.Equals(parent, current, StringComparison.Ordinal))
            return current;
        current = parent;
    }
}

static CorpusOptions ParseArguments(string[] args, string repositoryRoot, string scriptPath)
{
    var list = false;
    var check = false;
    string? group = null;
    string? caseId = null;
    var output = Path.Combine(repositoryRoot, "bin", "parasolid-xt-corpus");

    for (var index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--list":
                list = true;
                break;
            case "--check":
                check = true;
                break;
            case "--group" when index + 1 < args.Length:
                group = args[++index];
                break;
            case "--case" when index + 1 < args.Length:
                caseId = args[++index];
                break;
            case "--output" when index + 1 < args.Length:
                output = ResolvePath(args[++index], scriptPath);
                break;
            default:
                return new CorpusOptions(list, check, group, caseId, output, Invalid: true);
        }
    }

    if (list && (check || group is not null || caseId is not null))
        return new CorpusOptions(list, check, group, caseId, output, Invalid: true);
    if (group is not null && caseId is not null)
        return new CorpusOptions(list, check, group, caseId, output, Invalid: true);

    return new CorpusOptions(list, check, group, caseId, output, Invalid: false);
}

static string ResolvePath(string path, string scriptPath)
{
    if (Path.IsPathRooted(path))
        return Path.GetFullPath(path);

    return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(scriptPath) ?? ".", path));
}

static List<CaseGroup> SelectGroups(IReadOnlyList<CaseGroup> groups, string? groupName, string? caseId)
{
    var selected = new List<CaseGroup>();
    foreach (var group in groups)
    {
        if (groupName is not null && !string.Equals(group.Name, groupName, StringComparison.Ordinal))
            continue;
        if (caseId is not null && !group.Cases.Any(entry => string.Equals(entry.CaseId, caseId, StringComparison.Ordinal)))
            continue;
        selected.Add(group);
    }

    return selected;
}

static void PrintUsage()
{
    Console.Error.WriteLine("usage: dotnet run scripts/GenerateParasolidXtCorpus.cs -- [--list] [--check] [--group GROUP | --case CASE_ID] [--output PATH]");
}

static async Task<ProcessResult> InvokeAsync(string scriptPath, IReadOnlyList<string> arguments, string repositoryRoot)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        WorkingDirectory = repositoryRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    startInfo.ArgumentList.Add("run");
    startInfo.ArgumentList.Add(scriptPath);
    startInfo.ArgumentList.Add("--");
    foreach (var argument in arguments)
        startInfo.ArgumentList.Add(argument);

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("could not start dotnet");
    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
}

internal readonly record struct CorpusOptions(
    bool List,
    bool Check,
    string? Group,
    string? CaseId,
    string OutputRoot,
    bool Invalid);

internal sealed record CaseGroup(string Name, string ScriptPath, CaseListEntry[] Cases);

internal sealed record CaseListEntry(string CaseId, string Producer, string Description, string[] Coverage, string[]? TypeCoverage);

internal readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);

[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
[System.Text.Json.Serialization.JsonSerializable(typeof(CaseListEntry[]))]
internal partial class CorpusJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}

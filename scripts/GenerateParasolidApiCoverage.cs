#!/usr/bin/env dotnet run
// Generate the machine-readable Parasolid API/type/token inventory used by the
// parallel XT corpus workers.
//
// Usage:
//   dotnet run scripts/GenerateParasolidApiCoverage.cs
//   dotnet run scripts/GenerateParasolidApiCoverage.cs -- --check
//
// The generator deliberately uses text parsing instead of a compiler frontend.
// The checked-in C headers and PKToy generated binding are the inputs we need to
// inventory, and this keeps the script runnable on hosts without libclang.

global using ItemCount = System.Int32;
global using SourceLine = System.Int32;
global using ItemIndex = System.Int32;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Linq;

var arguments = ParseArguments(args);
if (arguments.Help)
{
    PrintUsage();
    return 0;
}

var scriptDirectory = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repositoryRoot = Path.GetFullPath(Path.Combine(scriptDirectory, ".."));
var input = InputPaths.Create(repositoryRoot);
var result = BuildManifest(input);

var generatedDirectory = ResolveOutputDirectory(repositoryRoot, arguments.OutputDirectory);
var generated = CreateGeneratedFiles(result, generatedDirectory);

if (arguments.Check)
{
    var failed = false;
    foreach (var file in generated)
    {
        if (!File.Exists(file.AbsolutePath))
        {
            Console.Error.WriteLine($"Generated coverage file is missing: {file.RelativePath}");
            failed = true;
            continue;
        }

        var existing = File.ReadAllText(file.AbsolutePath);
        if (!StringComparer.Ordinal.Equals(existing, file.Content))
        {
            Console.Error.WriteLine($"Generated coverage file is out of date: {file.RelativePath}");
            failed = true;
        }
    }

    if (failed)
    {
        Console.Error.WriteLine("Run dotnet run scripts/GenerateParasolidApiCoverage.cs to regenerate the inventory.");
        return 1;
    }

    PrintSummary(result, generatedDirectory, check: true);
    return result.Diagnostics.Any(d => d.Severity == "error") ? 2 : 0;
}

Directory.CreateDirectory(generatedDirectory);
foreach (var file in generated)
    File.WriteAllText(file.AbsolutePath, file.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

PrintSummary(result, generatedDirectory, check: false);
return result.Diagnostics.Any(d => d.Severity == "error") ? 2 : 0;

static string GetScriptPath([CallerFilePath] string path = "") => path;

static void PrintUsage()
{
    Console.WriteLine("GenerateParasolidApiCoverage.cs [--check] [--output <relative-directory>]");
    Console.WriteLine("  --check       Verify generated files without changing them.");
    Console.WriteLine("  --output DIR  Override the repository-relative output directory.");
}

static void PrintSummary(CoverageResult result, string outputDirectory, bool check)
{
    var mode = check ? "Checked" : "Generated";
    Console.WriteLine($"{mode} Parasolid API coverage: {Path.GetRelativePath(Environment.CurrentDirectory, outputDirectory)}");
    Console.WriteLine($"  APIs: {result.Apis.Length} ({result.Summary.BindingOnlyApiCount} binding-only, {result.Summary.HeaderOnlyApiCount} header-only)");
    Console.WriteLine($"  Types: {result.Types.Length} ({result.Summary.StructCount} structs, {result.Summary.AliasCount} aliases)");
    Console.WriteLine($"  Tokens: {result.Tokens.Length}");
    Console.WriteLine($"  Diagnostics: {result.Diagnostics.Length}");
    foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity != "info").Take(8))
        Console.WriteLine($"  {diagnostic.Severity}: {diagnostic.Code}: {diagnostic.Message}");
}

static string ResolveOutputDirectory(string repositoryRoot, string? requested)
{
    var relative = string.IsNullOrWhiteSpace(requested)
        ? Path.Combine("tests", "ParasolidXtCorpus", "coverage", "generated")
        : requested;

    if (Path.IsPathRooted(relative))
        throw new ArgumentException("--output must be relative to the repository root.", nameof(requested));

    return Path.GetFullPath(Path.Combine(repositoryRoot, relative));
}

static CoverageResult BuildManifest(InputPaths input)
{
    var diagnostics = new List<DiagnosticRecord>();
    var sources = new List<SourceRecord>();
    var headerFunctions = new List<HeaderFunction>();
    var headerTypes = new List<HeaderType>();
    var headerTokens = new List<TokenRecord>();

    foreach (var path in input.HeaderPaths)
    {
        if (!File.Exists(path.AbsolutePath))
        {
            diagnostics.Add(new DiagnosticRecord("warning", "missing_source", $"Header is unavailable: {path.RelativePath}", path.RelativePath, null));
            sources.Add(SourceRecord.Missing(path.RelativePath, "header"));
            continue;
        }

        var text = File.ReadAllText(path.AbsolutePath);
        var functions = ParseHeaderFunctions(text, path.RelativePath);
        var types = ParseHeaderTypes(text, path.RelativePath);
        var headerTokensInFile = ParseHeaderTokens(text, path.RelativePath);
        headerFunctions.AddRange(functions);
        headerTypes.AddRange(types);
        headerTokens.AddRange(headerTokensInFile);
        sources.Add(SourceRecord.Read(path.RelativePath, "header", text, functions.Length, types.Length + headerTokensInFile.Length));
    }

    var bindingMethods = new List<BindingMethod>();
    var bindingTypes = new List<BindingType>();
    var bindingTokens = new List<TokenRecord>();
    foreach (var path in input.BindingPaths)
    {
        if (!File.Exists(path.AbsolutePath))
        {
            diagnostics.Add(new DiagnosticRecord("warning", "missing_source", $"PKToy binding is unavailable: {path.RelativePath}", path.RelativePath, null));
            sources.Add(SourceRecord.Missing(path.RelativePath, "binding"));
            continue;
        }

        var text = File.ReadAllText(path.AbsolutePath);
        var methods = ParseBindingMethods(text, path.RelativePath);
        var types = ParseBindingTypes(text, path.RelativePath);
        var bindingTokensInFile = ParseBindingTokens(text, path.RelativePath);
        bindingMethods.AddRange(methods);
        bindingTypes.AddRange(types);
        bindingTokens.AddRange(bindingTokensInFile);
        sources.Add(SourceRecord.Read(path.RelativePath, "binding", text, methods.Length, types.Length + bindingTokensInFile.Length));
    }

    var apis = MergeApis(headerFunctions, bindingMethods, diagnostics);
    var typesMerged = MergeTypes(headerTypes, bindingTypes, bindingTokens, diagnostics);
    var mergedTokens = MergeTokens(headerTokens, bindingTokens);
    var sourceArray = sources.OrderBy(s => s.Path, StringComparer.Ordinal).ToArray();

    var summary = new CoverageSummary(
        apis.Length,
        apis.Count(a => a.BindingSignatures.Length == 0),
        apis.Count(a => a.HeaderLine is null),
        typesMerged.Count(t => t.Kind == "struct"),
        typesMerged.Count(t => t.Kind is "alias" or "callback" or "enum"),
        mergedTokens.Length,
        diagnostics.Count(d => d.Severity == "error"),
        diagnostics.Count(d => d.Severity == "warning"));
    summary = summary with { TypeCount = typesMerged.Length };

    return new CoverageResult(
        "parasolid-api-coverage-v1",
        "SCH_3701000_37102",
        sourceArray,
        apis,
        typesMerged,
        mergedTokens,
        summary,
        diagnostics.OrderBy(d => d.Severity, StringComparer.Ordinal)
                   .ThenBy(d => d.Source, StringComparer.Ordinal)
                   .ThenBy(d => d.Line ?? 0)
                   .ThenBy(d => d.Code, StringComparer.Ordinal)
                   .ThenBy(d => d.Message, StringComparer.Ordinal)
                   .ToArray());
}

static ApiRecord[] MergeApis(
    IReadOnlyList<HeaderFunction> headerFunctions,
    IReadOnlyList<BindingMethod> bindingMethods,
    List<DiagnosticRecord> diagnostics)
{
    var headers = headerFunctions
        .GroupBy(f => f.Name, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Source, StringComparer.Ordinal).ThenBy(f => f.Line).First(), StringComparer.Ordinal);
    var bindings = bindingMethods
        .GroupBy(f => f.EntryPoint, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Source, StringComparer.Ordinal).ThenBy(f => f.Line).ToArray(), StringComparer.Ordinal);

    foreach (var header in headers.Values.OrderBy(f => f.Name, StringComparer.Ordinal))
    {
        if (!bindings.ContainsKey(header.Name))
            diagnostics.Add(new DiagnosticRecord("warning", "header_only_api", $"No PKToy P/Invoke binding was found for {header.Name}.", header.Source, header.Line));
    }

    foreach (var binding in bindings.Keys.OrderBy(n => n, StringComparer.Ordinal))
    {
        if (!headers.ContainsKey(binding))
            diagnostics.Add(new DiagnosticRecord("warning", "binding_only_api", $"PKToy binding has no matching header declaration for {binding}.", null, null));
    }

    return headers.Keys.Union(bindings.Keys, StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal)
        .Select(name =>
        {
            headers.TryGetValue(name, out var header);
            bindings.TryGetValue(name, out var binding);
            var parameters = header?.Parameters ?? Array.Empty<ApiParameter>();
            var returnType = header?.ReturnType ?? binding?.FirstOrDefault()?.ReturnType ?? "";
            return new ApiRecord(
                name,
                GetApiFamily(name),
                ClassifyApi(name),
                ClassificationReason(name),
                returnType,
                parameters,
                header?.Source,
                header?.Line,
                binding?.Select(b => new BindingSignature(b.Signature, b.Source, b.Line)).ToArray() ?? Array.Empty<BindingSignature>());
        })
        .ToArray();
}

static TypeRecord[] MergeTypes(
    IReadOnlyList<HeaderType> headerTypes,
    IReadOnlyList<BindingType> bindingTypes,
    IReadOnlyList<TokenRecord> bindingTokens,
    List<DiagnosticRecord> diagnostics)
{
    var headerByName = headerTypes
        .GroupBy(t => t.Name, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Source, StringComparer.Ordinal).ThenBy(t => t.Line).First(), StringComparer.Ordinal);
    var bindingByName = bindingTypes
        .GroupBy(t => t.Name, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Source, StringComparer.Ordinal).ThenBy(t => t.Line).First(), StringComparer.Ordinal);

    foreach (var header in headerByName.Values.OrderBy(t => t.Name, StringComparer.Ordinal))
    {
        if (!bindingByName.ContainsKey(header.Name))
            diagnostics.Add(new DiagnosticRecord("info", "header_only_type", $"No PKToy type binding was found for {header.Name}.", header.Source, header.Line));
    }

    return headerByName.Keys.Union(bindingByName.Keys, StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal)
        .Select(name =>
        {
            headerByName.TryGetValue(name, out var header);
            bindingByName.TryGetValue(name, out var binding);
            var fields = binding?.Fields.Length > 0 ? binding.Fields : header?.Fields ?? Array.Empty<TypeField>();
            var kind = binding?.Kind ?? header?.Kind ?? "alias";
            if (kind == "alias" && bindingTokens.Any(token => StringComparer.Ordinal.Equals(token.DeclaredType, name)))
                kind = "enum";
            return new TypeRecord(
                name,
                kind,
                binding?.UnderlyingType ?? header?.UnderlyingType,
                header?.Source ?? binding?.Source ?? "",
                header?.Line ?? binding?.Line ?? 0,
                fields,
                header is not null,
                binding is not null);
        })
        .ToArray();
}

static TokenRecord[] MergeTokens(IReadOnlyList<TokenRecord> headerTokens, IReadOnlyList<TokenRecord> bindingTokens)
{
    return headerTokens.Concat(bindingTokens)
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ThenBy(t => t.Source, StringComparer.Ordinal)
        .ThenBy(t => t.Line)
        .ThenBy(t => t.Value, StringComparer.Ordinal)
        .ToArray();
}

static HeaderFunction[] ParseHeaderFunctions(string text, string source)
{
    var declaration = new Regex(
        @"(?m)^[ \t]*PK_linkage_m\s+(?<return>[A-Za-z_][A-Za-z0-9_]*(?:\s*\*)?)\s+(?<name>PK_[A-Za-z0-9_]+)[ \t]*$",
        RegexOptions.Compiled);
    var result = new List<HeaderFunction>();
    foreach (Match match in declaration.Matches(text))
    {
        var open = text.IndexOf('(', match.Index + match.Length);
        if (open < 0)
            continue;
        var close = text.IndexOf(");", open, StringComparison.Ordinal);
        if (close < 0)
            continue;

        var parameterText = text[(open + 1)..close];
        var line = GetLineNumber(text, match.Index);
        result.Add(new HeaderFunction(
            match.Groups["name"].Value,
            NormalizeType(match.Groups["return"].Value),
            ParseHeaderParameters(parameterText),
            source,
            line));
    }

    return result.OrderBy(f => f.Name, StringComparer.Ordinal).ThenBy(f => f.Line).ToArray();
}

static ApiParameter[] ParseHeaderParameters(string text)
{
    var parameters = new List<ApiParameter>();
    var direction = "received";
    var position = 0;
    foreach (var raw in text.Split('\n'))
    {
        var line = raw.Trim().TrimEnd('\r').Trim();
        if (line.Length == 0 || line == ")")
            continue;

        var section = Regex.Match(line, @"^/\*\s*(received|returned|modified|released|created)\s*\*/$", RegexOptions.IgnoreCase);
        if (section.Success)
        {
            direction = section.Groups[1].Value.ToLowerInvariant();
            continue;
        }

        var comments = Regex.Matches(line, @"/\*\s*(.*?)\s*\*/");
        var commentName = comments.Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Select(value => Regex.Match(value, @"^([A-Za-z_][A-Za-z0-9_]*)$").Groups[1].Value)
            .FirstOrDefault(value => value.Length > 0) ?? "";
        var withoutComments = Regex.Replace(line, @"/\*.*?\*/", "").Trim().TrimEnd(',').Trim();
        if (withoutComments.Length == 0)
            continue;

        var name = commentName;
        var type = withoutComments;
        if (name.Length == 0)
        {
            var actualName = Regex.Match(withoutComments, @"(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\s*\[[^\]]*\])?$");
            if (actualName.Success && !withoutComments.EndsWith('*') && !withoutComments.EndsWith("const", StringComparison.Ordinal))
            {
                name = actualName.Groups["name"].Value;
                type = withoutComments[..actualName.Index].Trim();
            }
        }

        if (name.Length == 0)
            name = $"parameter_{position}";
        if (type.Length == 0)
            type = withoutComments;

        parameters.Add(new ApiParameter(name, NormalizeType(type), direction, position));
        position++;
    }

    return AnnotateParameters(parameters);
}

static HeaderType[] ParseHeaderTypes(string text, string source)
{
    var result = new List<HeaderType>();
    var typedefStatement = new Regex(@"(?ms)^[ \t]*typedef\b.*?;", RegexOptions.Compiled);
    foreach (Match match in typedefStatement.Matches(text))
    {
        var statement = StripCComments(match.Value).Replace('\r', ' ').Replace('\n', ' ');
        statement = Regex.Replace(statement, @"\s+", " ").Trim();
        if (statement.EndsWith(';'))
            statement = statement[..^1].TrimEnd();
        var line = GetLineNumber(text, match.Index);
        var structDefinition = Regex.Match(statement, @"typedef\s+struct\s+(?<tag>PK_[A-Za-z0-9_]+_s)\s*\{.*\}\s*(?<name>PK_[A-Za-z0-9_]+)", RegexOptions.IgnoreCase);
        if (structDefinition.Success)
        {
            result.Add(new HeaderType(structDefinition.Groups["name"].Value, "struct", "struct " + structDefinition.Groups["tag"].Value, source, line, Array.Empty<TypeField>()));
            continue;
        }

        var callback = Regex.Match(statement, @"typedef\s+.*?\(\s*\*\s*(?<name>PK_[A-Za-z0-9_]+)\s*\)\s*\(", RegexOptions.IgnoreCase);
        if (callback.Success)
        {
            result.Add(new HeaderType(callback.Groups["name"].Value, "callback", statement, source, line, Array.Empty<TypeField>()));
            continue;
        }

        var forward = Regex.Match(statement, @"typedef\s+(?<underlying>struct\s+PK_[A-Za-z0-9_]+_s|union\s+PK_[A-Za-z0-9_]+_s)\s+(?<name>PK_[A-Za-z0-9_]+)\s*$", RegexOptions.IgnoreCase);
        if (forward.Success)
        {
            result.Add(new HeaderType(forward.Groups["name"].Value, "alias", NormalizeType(forward.Groups["underlying"].Value), source, line, Array.Empty<TypeField>()));
            continue;
        }

        var scalar = Regex.Match(statement, @"typedef\s+(?<underlying>.+?)\s+(?<name>PK_[A-Za-z0-9_]+)\s*$", RegexOptions.IgnoreCase);
        if (scalar.Success)
            result.Add(new HeaderType(scalar.Groups["name"].Value, "alias", NormalizeType(scalar.Groups["underlying"].Value), source, line, Array.Empty<TypeField>()));
    }

    return result.GroupBy(t => t.Name, StringComparer.Ordinal)
        .Select(g => g.OrderBy(t => t.Line).First())
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToArray();
}

static TokenRecord[] ParseHeaderTokens(string text, string source)
{
    var result = new List<TokenRecord>();
    var macro = new Regex(
        @"(?m)^[ \t]*#define\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<function>\([^\r\n]*\))?\s+(?<value>[^\r\n]+)",
        RegexOptions.Compiled);
    foreach (Match match in macro.Matches(text))
    {
        if (match.Groups["function"].Success)
            continue;

        var name = match.Groups["name"].Value;
        var rawValue = match.Groups["value"].Value.Trim();
        var comment = "";
        var commentStart = rawValue.IndexOf("/*", StringComparison.Ordinal);
        if (commentStart >= 0)
        {
            comment = rawValue[(commentStart + 2)..].Replace("*/", "", StringComparison.Ordinal).Trim();
            rawValue = rawValue[..commentStart].Trim();
        }

        if (!LooksLikeTokenValue(rawValue))
            continue;
        result.Add(new TokenRecord(name, rawValue, comment, source, GetLineNumber(text, match.Index), "header", null));
    }

    return result.ToArray();
}

static ApiParameter[] AnnotateParameters(IReadOnlyList<ApiParameter> parameters)
{
    var names = parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);
    return parameters.Select(parameter =>
    {
        var count = FindCountParameter(parameter.Name, names);
        var pointerDepth = parameter.Type.Count(character => character == '*');
        var isArray = parameter.Type.Contains("[]", StringComparison.Ordinal);
        return parameter with
        {
            PointerDepth = pointerDepth,
            IsArray = isArray,
            CountParameter = count,
        };
    }).ToArray();
}

static string? FindCountParameter(string name, IReadOnlySet<string> names)
{
    var candidates = new[] { "n_" + name, "number_" + name, "count_" + name, name + "_count" };
    return candidates.FirstOrDefault(names.Contains);
}

static BindingMethod[] ParseBindingMethods(string text, string source)
{
    var method = new Regex(
        @"(?m)^[ \t]*public\s+(?:(?:unsafe)\s+)?static\s+extern\s+(?:(?:unsafe)\s+)?(?<return>[A-Za-z_][A-Za-z0-9_]*(?:\s*\*)?)\s+(?<name>PK_[A-Za-z0-9_]+)\s*\(",
        RegexOptions.Compiled);
    var entryPoint = new Regex(@"EntryPoint\s*=\s*\""(?<name>PK_[A-Za-z0-9_]+)\""", RegexOptions.Compiled);
    var result = new List<BindingMethod>();
    foreach (Match match in method.Matches(text))
    {
        var open = text.IndexOf('(', match.Index + match.Length - 1);
        var close = open < 0 ? -1 : FindMatchingParenthesis(text, open);
        if (close < 0)
            continue;
        var after = close + 1;
        while (after < text.Length && char.IsWhiteSpace(text[after]))
            after++;
        if (after >= text.Length || text[after] != ';')
            continue;

        var before = text[Math.Max(0, match.Index - 300)..match.Index];
        var entry = entryPoint.Matches(before).Cast<Match>().LastOrDefault()?.Groups["name"].Value;
        var name = string.IsNullOrEmpty(entry) ? match.Groups["name"].Value : entry;
        var parameters = NormalizeBindingParameters(text[(open + 1)..close]);
        result.Add(new BindingMethod(
            name,
            NormalizeType(match.Groups["return"].Value),
            parameters,
            NormalizeType($"{match.Groups["return"].Value} {match.Groups["name"].Value}({text[(open + 1)..close]})"),
            source,
            GetLineNumber(text, match.Index)));
    }

    return result.OrderBy(m => m.EntryPoint, StringComparer.Ordinal).ThenBy(m => m.Signature, StringComparer.Ordinal).ToArray();
}

static ApiParameter[] NormalizeBindingParameters(string text)
{
    if (string.IsNullOrWhiteSpace(text))
        return Array.Empty<ApiParameter>();

    var result = new List<ApiParameter>();
    foreach (var (raw, position) in SplitCommaSeparated(text).Select((value, index) => (value, index)))
    {
        var parameter = Regex.Replace(raw.Trim(), @"\[[^\]]*\]\s*", "").Trim();
        var match = Regex.Match(parameter, @"^(?<type>.+?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)$");
        var type = match.Success ? match.Groups["type"].Value : parameter;
        var name = match.Success ? match.Groups["name"].Value : $"parameter_{position}";
        result.Add(new ApiParameter(name, NormalizeType(type), "binding", position));
    }

    return AnnotateParameters(result);
}

static BindingType[] ParseBindingTypes(string text, string source)
{
    var result = new List<BindingType>();
    var globalUsing = new Regex(
        @"(?m)^[ \t]*global\s+using\s+(?:unsafe\s+)?(?<name>PK_[A-Za-z0-9_]+)\s*=\s*(?<underlying>[^;]+);",
        RegexOptions.Compiled);
    foreach (Match match in globalUsing.Matches(text))
    {
        var underlying = NormalizeType(match.Groups["underlying"].Value);
        var kind = underlying.StartsWith("delegate*", StringComparison.Ordinal) ? "callback" : "alias";
        result.Add(new BindingType(match.Groups["name"].Value, kind, underlying, source, GetLineNumber(text, match.Index), Array.Empty<TypeField>()));
    }

    var structDeclaration = new Regex(@"(?m)^public\s+(?:unsafe\s+)?struct\s+(?<name>PK_[A-Za-z0-9_]+)\s*$", RegexOptions.Compiled);
    foreach (Match match in structDeclaration.Matches(text))
    {
        var open = text.IndexOf('{', match.Index + match.Length);
        if (open < 0)
            continue;
        var close = FindMatchingBrace(text, open);
        if (close < 0)
            continue;
        var body = text[(open + 1)..close];
        result.Add(new BindingType(
            match.Groups["name"].Value,
            "struct",
            null,
            source,
            GetLineNumber(text, match.Index),
            ParseBindingFields(body)));
    }

    return result.GroupBy(t => t.Name, StringComparer.Ordinal)
        .Select(g => g.OrderByDescending(t => t.Kind == "struct").ThenBy(t => t.Line).First())
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToArray();
}

static TypeField[] ParseBindingFields(string body)
{
    var field = new Regex(@"(?m)^\s*public\s+(?:(?:unsafe)\s+)?(?<type>.+?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*;\s*$", RegexOptions.Compiled);
    var fixedField = new Regex(@"(?m)^\s*public\s+(?:(?:unsafe)\s+)?fixed\s+(?<type>[A-Za-z_][A-Za-z0-9_<>]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\[(?<length>[^\]]+)\]\s*;\s*$", RegexOptions.Compiled);
    var fields = field.Matches(body).Cast<Match>()
        .Select(match => (Index: match.Index, Name: match.Groups["name"].Value, Type: NormalizeType(match.Groups["type"].Value)))
        .Concat(fixedField.Matches(body).Cast<Match>()
            .Select(match => (Index: match.Index, Name: match.Groups["name"].Value, Type: NormalizeType(match.Groups["type"].Value) + "[" + match.Groups["length"].Value.Trim() + "]")))
        .OrderBy(field => field.Index)
        .ToArray();
    return fields.Select((item, position) => new TypeField(item.Name, item.Type, position)).ToArray();
}

static TokenRecord[] ParseBindingTokens(string text, string source)
{
    var constant = new Regex(@"(?m)^[ \t]*public\s+const\s+(?<type>[A-Za-z_][A-Za-z0-9_]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>[^;]+);", RegexOptions.Compiled);
    return constant.Matches(text).Cast<Match>()
        .Select(match => new TokenRecord(
            match.Groups["name"].Value,
            match.Groups["value"].Value.Trim(),
            "",
            source,
            GetLineNumber(text, match.Index),
            "binding",
            match.Groups["type"].Value))
        .ToArray();
}

static string GetApiFamily(string name)
{
    var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
    return parts.Length > 1 ? parts[1] : name;
}

static string ClassifyApi(string name)
{
    var upper = name.ToUpperInvariant();
    if (upper.StartsWith("PK_SESSION_", StringComparison.Ordinal) ||
        upper.StartsWith("PK_MEMORY_", StringComparison.Ordinal) ||
        upper.StartsWith("PK_MARK_", StringComparison.Ordinal) ||
        upper.StartsWith("PK_APPITEM_", StringComparison.Ordinal) ||
        upper.StartsWith("PK_PARTITION_", StringComparison.Ordinal) ||
        upper.StartsWith("PK_PMARK_", StringComparison.Ordinal) ||
        upper.StartsWith("PK_BB_", StringComparison.Ordinal) ||
        upper.StartsWith("PK_DELTA_", StringComparison.Ordinal) ||
        upper.StartsWith("PK_THREAD_", StringComparison.Ordinal) ||
        upper.StartsWith("PK_DEBUG_", StringComparison.Ordinal) ||
        upper.StartsWith("PK_REPORT_", StringComparison.Ordinal))
        return "ExcludedRuntimeOnly";

    // Transformation handles and view transforms are ephemeral helpers used
    // as inputs to entity mutations; the transform object itself is not part
    // state transmitted in the corpus.
    if (upper.StartsWith("PK_TRANSF_", StringComparison.Ordinal) ||
        string.Equals(upper, "PK_VECTOR_MAKE_VIEW_TRANSF", StringComparison.Ordinal) ||
        string.Equals(upper, "PK_VECTOR_TRANSFORM", StringComparison.Ordinal) ||
        string.Equals(upper, "PK_VECTOR_TRANSFORM_DIRECTION", StringComparison.Ordinal) ||
        string.Equals(upper, "PK_ATTDEF_SET_CALLBACK_FLAGS", StringComparison.Ordinal) ||
        string.Equals(upper, "PK_ATTRIB_SET_NO_ROLL", StringComparison.Ordinal))
        return "RequiredHelper";

    if (upper.Contains("_TRANSMIT", StringComparison.Ordinal) || upper.Contains("_RECEIVE", StringComparison.Ordinal) ||
        upper.Contains("_REGISTER_CALLBACK", StringComparison.Ordinal))
        return "RequiredHelper";

    if (upper.Contains("_ASK", StringComparison.Ordinal) || upper.Contains("_IS", StringComparison.Ordinal) ||
        upper.Contains("_FIND", StringComparison.Ordinal) || upper.Contains("_CHECK", StringComparison.Ordinal) ||
        upper.Contains("_COMPARE", StringComparison.Ordinal) || upper.Contains("_EVALUATE", StringComparison.Ordinal) ||
        upper.Contains("_MEASURE", StringComparison.Ordinal))
        return "ExcludedPureQuery";

    // Result-freeing entry points only release temporary API result buffers;
    // they do not create or mutate persistent part state.
    if (upper.EndsWith("_R_F", StringComparison.Ordinal))
        return "RequiredHelper";

    if (upper.Contains("_CREATE", StringComparison.Ordinal) || upper.Contains("_MAKE_", StringComparison.Ordinal) ||
        upper.EndsWith("_MAKE", StringComparison.Ordinal) || upper.Contains("_NEW_", StringComparison.Ordinal))
        return "Producer";

    if (upper.Contains("_DELETE", StringComparison.Ordinal) || upper.Contains("_REMOVE", StringComparison.Ordinal) ||
        upper.Contains("_SET_", StringComparison.Ordinal) || upper.Contains("_TRANSFORM", StringComparison.Ordinal) ||
        upper.Contains("_REVERSE", StringComparison.Ordinal) || upper.Contains("_REPLACE", StringComparison.Ordinal) ||
        upper.Contains("_ADD_", StringComparison.Ordinal) || upper.Contains("_ATTACH", StringComparison.Ordinal) ||
        upper.Contains("_DETACH", StringComparison.Ordinal) || upper.Contains("_BOOLEAN", StringComparison.Ordinal) ||
        upper.Contains("_BLEND", StringComparison.Ordinal) || upper.Contains("_IMPRINT", StringComparison.Ordinal) ||
        upper.Contains("_OFFSET", StringComparison.Ordinal) || upper.Contains("_SWEEP", StringComparison.Ordinal) ||
        upper.Contains("_SPIN", StringComparison.Ordinal) || upper.Contains("_TAPER", StringComparison.Ordinal) ||
        upper.Contains("_THICKEN", StringComparison.Ordinal) || upper.Contains("_SEW", StringComparison.Ordinal) ||
        upper.Contains("_KNIT", StringComparison.Ordinal) || upper.Contains("_HEAL", StringComparison.Ordinal) ||
        upper.Contains("_COPY", StringComparison.Ordinal))
        return "Mutator";

    return "RequiredHelper";
}

static string ClassificationReason(string name)
{
    return ClassifyApi(name) switch
    {
        "ExcludedRuntimeOnly" => "session, memory, partition, application-item, rollback, reporting, threading, or debug lifecycle API",
        "RequiredHelper" => "transmit/receive, callback registration, or supporting API",
        "ExcludedPureQuery" => "read-only ask/is/find/check/compare/evaluate/measure API",
        "Producer" => "create/make/new API produces a persistent entity or state",
        "Mutator" => "delete/set/transform/modify API changes persistent state",
        _ => "fallback support classification",
    };
}

static string NormalizeType(string value)
{
    var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
    normalized = Regex.Replace(normalized, @"\s*\*\s*", "*");
    return normalized;
}

static string StripCComments(string value) => Regex.Replace(value, @"/\*.*?\*/", "", RegexOptions.Singleline);

static bool LooksLikeTokenValue(string value)
{
    if (value.Length == 0 || value.Contains('\\'))
        return false;
    return Regex.IsMatch(value, @"^[()\s+\-*/0-9A-Za-z_']+$");
}

static ItemCount GetLineNumber(string text, ItemIndex index)
{
    var line = 1;
    for (var i = 0; i < index && i < text.Length; i++)
    {
        if (text[i] == '\n')
            line++;
    }

    return line;
}

static ItemIndex FindMatchingBrace(string text, ItemIndex open)
{
    var depth = 0;
    for (var i = open; i < text.Length; i++)
    {
        if (text[i] == '{')
            depth++;
        else if (text[i] == '}' && --depth == 0)
            return i;
    }

    return -1;
}

static ItemIndex FindMatchingParenthesis(string text, ItemIndex open)
{
    var depth = 0;
    for (var i = open; i < text.Length; i++)
    {
        if (text[i] == '(')
            depth++;
        else if (text[i] == ')' && --depth == 0)
            return i;
    }

    return -1;
}

static IEnumerable<string> SplitCommaSeparated(string text)
{
    var start = 0;
    var depth = 0;
    for (var i = 0; i < text.Length; i++)
    {
        switch (text[i])
        {
            case '<':
            case '(': depth++; break;
            case '>':
            case ')': depth--; break;
            case ',':
                if (depth == 0)
                {
                    yield return text[start..i];
                    start = i + 1;
                }
                break;
        }
    }

    if (start < text.Length)
        yield return text[start..];
}

static GeneratedFile[] CreateGeneratedFiles(CoverageResult result, string outputDirectory)
{
    var manifestJson = JsonSerializer.Serialize(result, CoverageJsonContext.Default.CoverageResult) + "\n";
    var diagnosticsJson = JsonSerializer.Serialize(result.Diagnostics, CoverageJsonContext.Default.DiagnosticRecordArray) + "\n";
    var markdown = RenderMarkdown(result);

    return
    [
        new GeneratedFile(Path.Combine(outputDirectory, "manifest.json"), manifestJson),
        new GeneratedFile(Path.Combine(outputDirectory, "manifest.md"), markdown),
        new GeneratedFile(Path.Combine(outputDirectory, "diagnostics.json"), diagnosticsJson),
    ];
}

static string RenderMarkdown(CoverageResult result)
{
    var sb = new StringBuilder();
    sb.AppendLine("# Parasolid API coverage inventory");
    sb.AppendLine();
    sb.AppendLine("> Generated from the checked-in Parasolid headers and PKToy P/Invoke binding. Do not edit this file by hand.");
    sb.AppendLine();
    sb.AppendLine($"- Schema: `{result.Schema}`");
    sb.AppendLine($"- APIs: {result.Summary.ApiCount} (header-only: {result.Summary.HeaderOnlyApiCount}; binding-only: {result.Summary.BindingOnlyApiCount})");
    sb.AppendLine($"- Types: {result.Summary.TypeCount} (structs: {result.Summary.StructCount}; aliases/enums/callbacks: {result.Summary.AliasCount})");
    sb.AppendLine($"- Tokens: {result.Summary.TokenCount}");
    sb.AppendLine($"- Diagnostics: {result.Summary.WarningCount} warnings, {result.Summary.ErrorCount} errors");
    sb.AppendLine();

    sb.AppendLine("## APIs by classification");
    sb.AppendLine();
    sb.AppendLine("| Classification | Count |");
    sb.AppendLine("| --- | ---: |");
    foreach (var group in result.Apis.GroupBy(a => a.Classification, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        sb.AppendLine($"| `{group.Key}` | {group.Count()} |");
    sb.AppendLine();

    sb.AppendLine("## API inventory");
    sb.AppendLine();
    sb.AppendLine("| API | Family | Classification | Return | Parameters | Header | Binding signatures |");
    sb.AppendLine("| --- | --- | --- | --- | ---: | --- | ---: |");
    foreach (var api in result.Apis)
    {
        var location = api.Header is null ? "—" : $"`{api.Header}:{api.HeaderLine}`";
        sb.AppendLine($"| `{api.Name}` | `{api.Family}` | `{api.Classification}` | `{api.ReturnType}` | {api.Parameters.Length} | {location} | {api.BindingSignatures.Length} |");
    }
    sb.AppendLine();

    sb.AppendLine("## Types");
    sb.AppendLine();
    sb.AppendLine("| Type | Kind | Underlying type | Fields | Source |");
    sb.AppendLine("| --- | --- | --- | ---: | --- |");
    foreach (var type in result.Types)
        sb.AppendLine($"| `{type.Name}` | `{type.Kind}` | `{type.UnderlyingType ?? ""}` | {type.Fields.Length} | `{type.Source}:{type.Line}` |");
    sb.AppendLine();

    sb.AppendLine("## Tokens");
    sb.AppendLine();
    sb.AppendLine("| Token | Value | Declared type | Source | Comment |");
    sb.AppendLine("| --- | --- | --- | --- | --- |");
    foreach (var token in result.Tokens)
        sb.AppendLine($"| `{token.Name}` | `{EscapeMarkdown(token.Value)}` | `{token.DeclaredType ?? ""}` | `{token.Source}:{token.Line}` | {EscapeMarkdown(token.Comment)} |");
    sb.AppendLine();

    sb.AppendLine("## Diagnostics");
    sb.AppendLine();
    if (result.Diagnostics.Length == 0)
        sb.AppendLine("No diagnostics.");
    else
    {
        sb.AppendLine("| Severity | Code | Source | Message |");
        sb.AppendLine("| --- | --- | --- | --- |");
        foreach (var diagnostic in result.Diagnostics)
            sb.AppendLine($"| `{diagnostic.Severity}` | `{diagnostic.Code}` | `{diagnostic.Source ?? ""}{(diagnostic.Line is null ? "" : ":" + diagnostic.Line)}` | {EscapeMarkdown(diagnostic.Message)} |");
    }

    return sb.ToString();
}

static string EscapeMarkdown(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

static Arguments ParseArguments(string[] args)
{
    var check = false;
    var help = false;
    string? output = null;
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--check": check = true; break;
            case "--help":
            case "-h": help = true; break;
            case "--output":
                if (++i >= args.Length)
                    throw new ArgumentException("--output requires a repository-relative directory.");
                output = args[i];
                break;
            default:
                throw new ArgumentException($"Unknown argument: {args[i]}");
        }
    }

    return new Arguments(check, help, output);
}

sealed record Arguments(bool Check, bool Help, string? OutputDirectory);

sealed record InputPath(string AbsolutePath, string RelativePath);

sealed class InputPaths
{
    private InputPaths(string repositoryRoot)
    {
        var include = Path.Combine(repositoryRoot, "third_party", "parasolid", "include");
        var binding = Path.Combine(repositoryRoot, "third_party", "PKToy", "PskernelSharp");
        HeaderPaths =
        [
            PathOf(repositoryRoot, Path.Combine(include, "parasolid_kernel.h")),
            PathOf(repositoryRoot, Path.Combine(include, "parasolid_debug.h")),
            PathOf(repositoryRoot, Path.Combine(include, "parasolid_typedefs.h")),
            PathOf(repositoryRoot, Path.Combine(include, "parasolid_tokens.h")),
            PathOf(repositoryRoot, Path.Combine(include, "parasolid_ifails.h")),
            PathOf(repositoryRoot, Path.Combine(include, "frustrum_tokens.h")),
            PathOf(repositoryRoot, Path.Combine(include, "frustrum_ifails.h")),
        ];
        BindingPaths =
        [
            PathOf(repositoryRoot, Path.Combine(binding, "parasolid.g.cs")),
            PathOf(repositoryRoot, Path.Combine(binding, "Types.g.cs")),
            PathOf(repositoryRoot, Path.Combine(binding, "Using.cs")),
        ];
    }

    public InputPath[] HeaderPaths { get; }
    public InputPath[] BindingPaths { get; }

    public static InputPaths Create(string repositoryRoot) => new(repositoryRoot);

    private static InputPath PathOf(string repositoryRoot, string path) =>
        new(path, Path.GetRelativePath(repositoryRoot, path).Replace(Path.DirectorySeparatorChar, '/'));
}

sealed record SourceRecord(string Path, string Kind, bool Exists, string Sha256, ItemCount ParsedPrimaryCount, ItemCount ParsedSecondaryCount)
{
    public static SourceRecord Missing(string path, string kind) => new(path, kind, false, "", 0, 0);

    public static SourceRecord Read(string path, string kind, string text, ItemCount primary, ItemCount secondary) =>
        new(path, kind, true, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant(), primary, secondary);
}

sealed record HeaderFunction(string Name, string ReturnType, ApiParameter[] Parameters, string Source, SourceLine Line);

sealed record HeaderType(string Name, string Kind, string? UnderlyingType, string Source, SourceLine Line, TypeField[] Fields);

sealed record BindingMethod(string EntryPoint, string ReturnType, ApiParameter[] Parameters, string Signature, string Source, SourceLine Line);

sealed record BindingType(string Name, string Kind, string? UnderlyingType, string Source, SourceLine Line, TypeField[] Fields);

sealed record ApiParameter(string Name, string Type, string Direction, ItemIndex Position)
{
    public ItemCount PointerDepth { get; init; }
    public bool IsArray { get; init; }
    public string? CountParameter { get; init; }
}

sealed record BindingSignature(string Signature, string Source, SourceLine Line);

sealed record TypeField(string Name, string Type, ItemIndex Position);

sealed record TokenRecord(string Name, string Value, string Comment, string Source, SourceLine Line, string DefinitionKind, string? DeclaredType);

sealed record ApiRecord(
    string Name,
    string Family,
    string Classification,
    string ClassificationReason,
    string ReturnType,
    ApiParameter[] Parameters,
    string? Header,
    SourceLine? HeaderLine,
    BindingSignature[] BindingSignatures);

sealed record TypeRecord(
    string Name,
    string Kind,
    string? UnderlyingType,
    string Source,
    SourceLine Line,
    TypeField[] Fields,
    bool InHeader,
    bool InBinding);

sealed record DiagnosticRecord(string Severity, string Code, string Message, string? Source, SourceLine? Line);

sealed record CoverageSummary(
    ItemCount ApiCount,
    ItemCount BindingOnlyApiCount,
    ItemCount HeaderOnlyApiCount,
    ItemCount StructCount,
    ItemCount AliasCount,
    ItemCount TokenCount,
    ItemCount ErrorCount,
    ItemCount WarningCount)
{
    public ItemCount TypeCount { get; init; }
}

sealed record CoverageResult(
    string Generator,
    string Schema,
    SourceRecord[] Sources,
    ApiRecord[] Apis,
    TypeRecord[] Types,
    TokenRecord[] Tokens,
    CoverageSummary Summary,
    DiagnosticRecord[] Diagnostics);

sealed record GeneratedFile(string AbsolutePath, string Content)
{
    public string RelativePath => Path.GetRelativePath(Environment.CurrentDirectory, AbsolutePath).Replace(Path.DirectorySeparatorChar, '/');
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(CoverageResult))]
[JsonSerializable(typeof(DiagnosticRecord[]))]
partial class CoverageJsonContext : JsonSerializerContext
{
}

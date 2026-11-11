using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

internal sealed class OperationAcceptanceLedger
{
    private static OperationAcceptanceLedger? _current;
    public static OperationAcceptanceLedger Current => _current ??= new OperationAcceptanceLedger("default-scenario", "Default Scenario");

    public string ScenarioId { get; set; }
    public string Title { get; set; }
    public string? OutputDirectory { get; set; }
    public string? CheckVersion { get; set; }
    public string OperationSchemaVersion { get; set; } = "operation-layered-v1";
    public int LegacyExitCode { get; set; }
    public double? ScenarioWallElapsedMs { get; set; }
    public double? ScenarioElapsedMs => ScenarioWallElapsedMs;
    public JsonObject? ScenarioCapturedApiMs { get; set; }
    public string ScenarioStatus { get; set; } = "missing";
    public string? FirstFailedOperationId { get; set; }

    public List<OperationRecord> Operations { get; } = [];
    public List<JsonObject> AllChecks { get; } = [];
    private readonly Dictionary<string, OperationRecord> _byId = [];

    public OperationAcceptanceLedger(string scenarioId, string title, string? outputDir = null)
    {
        ScenarioId = scenarioId;
        Title = title;
        OutputDirectory = outputDir;
    }

    public static OperationAcceptanceLedger BeginScenario(string scenarioId, string title, string? outputDir = null)
    {
        _current = new OperationAcceptanceLedger(scenarioId, title, outputDir);
        return _current;
    }

    public OperationRecord BeginOperation(
        string id,
        string name,
        string role = "primary",
        string api = "unknown",
        JsonObject? options = null,
        JsonNode? inputProvenance = null,
        bool sameInput = true)
    {
        if (_byId.TryGetValue(id, out var existing))
            return existing;

        var ordinal = Operations.Count;
        var op = new OperationRecord(id, ordinal, name, role, api, options, inputProvenance, sameInput);
        Operations.Add(op);
        _byId[id] = op;
        return op;
    }

    public OperationRecord? GetOperation(string id) => _byId.GetValueOrDefault(id);

    public void RecordReturn(string opId, string kernel, int errorCode)
    {
        var op = GetOperation(opId);
        if (op == null) return;
        op.Returns[kernel.ToLowerInvariant()] = errorCode;
    }

    public void RecordTiming(string opId, string kernel, double ms)
    {
        var op = GetOperation(opId);
        if (op == null) return;
        if (kernel.Equals("gm", StringComparison.OrdinalIgnoreCase)) op.GmMs = ms;
        else if (kernel.Equals("pk", StringComparison.OrdinalIgnoreCase)) op.PkMs = ms;
    }

    public void RecordInputHash(string opId, string kernel, string targetHash, string[]? toolHashes = null)
    {
        var op = GetOperation(opId);
        if (op == null) return;
        var side = kernel.ToLowerInvariant();
        var node = op.InputHashes[side]?.AsObject() ?? [];
        node["target"] = targetHash;
        if (toolHashes != null)
        {
            var arr = new JsonArray();
            foreach (var h in toolHashes) arr.Add((JsonNode?)JsonValue.Create(h));
            node["tools"] = arr;
        }
        op.InputHashes[side] = node;
    }

    public void RecordOutputHashes(string opId, string kernel, string[] bodyHashes)
    {
        var op = GetOperation(opId);
        if (op == null) return;
        var side = kernel.ToLowerInvariant();
        var node = op.OutputHashes[side]?.AsObject() ?? [];
        var arr = new JsonArray();
        foreach (var h in bodyHashes) arr.Add((JsonNode?)JsonValue.Create(h));
        node["bodies"] = arr;
        op.OutputHashes[side] = node;
    }

    public JsonObject RecordCheck(
        string opId,
        string category,
        string checkId,
        string label,
        string status,
        double? gm = null,
        double? pk = null,
        double? threshold = null,
        string unit = "",
        string note = "",
        string? evidence = null)
    {
        var checkObj = new JsonObject
        {
            ["id"] = checkId,
            ["owner"] = opId,
            ["category"] = category,
            ["label"] = label,
            ["status"] = status,
            ["gm"] = Finite(gm),
            ["pk"] = Finite(pk),
            ["absDelta"] = gm.HasValue && pk.HasValue ? Finite(Math.Abs(gm.Value - pk.Value)) : null,
            ["normalizedDelta"] = gm.HasValue && pk.HasValue ? Finite(Math.Abs(gm.Value - pk.Value) / (1 + Math.Abs(pk.Value))) : null,
            ["threshold"] = threshold,
            ["unit"] = unit,
            ["evidence"] = evidence,
            ["note"] = note
        };

        AllChecks.Add(checkObj);

        var op = GetOperation(opId);
        if (op != null)
        {
            op.AddCheck(category, checkObj);
        }

        return checkObj;
    }

    public void AddUnreachedOperation(string id, int ordinal, string name, string role = "primary")
    {
        if (_byId.ContainsKey(id)) return;
        var op = new OperationRecord(id, ordinal, name, role, "unknown", null, null, false)
        {
            Status = "not-run"
        };
        Operations.Add(op);
        _byId[id] = op;
    }

    public void EndOperation(string opId, string? statusOverride = null)
    {
        var op = GetOperation(opId);
        if (op == null) return;
        if (!string.IsNullOrEmpty(statusOverride))
            op.Status = statusOverride;
        else
            op.EvaluateStatus();
    }

    public void EndScenario(int legacyExitCode, double? elapsedMs = null)
    {
        LegacyExitCode = legacyExitCode;
        ScenarioWallElapsedMs = elapsedMs;

        double sumGm = 0, sumPk = 0;
        bool hasGm = false, hasPk = false;
        foreach (var op in Operations)
        {
            op.EvaluateStatus();
            if (op.GmMs.HasValue) { sumGm += op.GmMs.Value; hasGm = true; }
            if (op.PkMs.HasValue) { sumPk += op.PkMs.Value; hasPk = true; }
        }

        ScenarioCapturedApiMs = new JsonObject
        {
            ["gm"] = hasGm ? sumGm : null,
            ["pk"] = hasPk ? sumPk : null
        };

        var anyFail = Operations.Any(o => o.PrimaryStatus == "fail" || o.Returns["gm"]?.GetValue<int?>() != 0) || legacyExitCode != 0;
        var anyTimeout = Operations.Any(o => o.Status == "timeout");

        FirstFailedOperationId = Operations.FirstOrDefault(o => o.PrimaryStatus == "fail" || o.Status == "fail")?.Id;

        if (anyTimeout)
            ScenarioStatus = "timeout";
        else if (anyFail)
            ScenarioStatus = "fail";
        else if (Operations.Count == 0)
            ScenarioStatus = "missing";
        else
            ScenarioStatus = "pass";
    }

    public JsonObject ToJsonObject()
    {
        var ops = new JsonArray();
        foreach (var op in Operations)
            ops.Add((JsonNode)op.ToJsonObject());

        var checks = new JsonArray();
        foreach (var c in AllChecks)
            checks.Add((JsonNode)c.DeepClone());

        return new JsonObject
        {
            ["operationSchemaVersion"] = OperationSchemaVersion,
            ["scenarioId"] = ScenarioId,
            ["scenarioStatus"] = ScenarioStatus,
            ["firstFailedOperationId"] = FirstFailedOperationId,
            ["legacyExitCode"] = LegacyExitCode,
            ["checkVersion"] = CheckVersion,
            ["scenarioWallElapsedMs"] = ScenarioWallElapsedMs,
            ["scenarioElapsedMs"] = ScenarioElapsedMs,
            ["scenarioCapturedApiMs"] = ScenarioCapturedApiMs?.DeepClone(),
            ["operations"] = ops,
            ["checks"] = checks
        };
    }

    public void SaveSidecar(string? filePath = null)
    {
        var path = filePath;
        if (string.IsNullOrEmpty(path))
        {
            if (string.IsNullOrEmpty(OutputDirectory)) return;
            path = Path.Combine(OutputDirectory, "ledger.json");
        }
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, ToJsonObject().ToJsonString());
    }

    public void PrintConsoleSummary(Action<string>? log = null)
    {
        var write = log ?? Console.WriteLine;
        write($"--- OPERATION LEDGER [{ScenarioId}]: {ScenarioStatus} (legacy exit={LegacyExitCode}) ---");
        foreach (var op in Operations)
        {
            var ratioStr = op.Timing["ratio"] != null ? $"{op.Timing["ratio"]!.GetValue<double>():F2}x" : "n/a";
            var timings = op.GmMs.HasValue && op.PkMs.HasValue ? $"GM={op.GmMs.Value:F3}ms PK={op.PkMs.Value:F3}ms (ratio={ratioStr})" : "no-timing";
            var primaryPass = op.PrimaryChecks.Count(c => c?["status"]?.GetValue<string>() == "pass");
            var primaryFail = op.PrimaryChecks.Count(c => c?["status"]?.GetValue<string>() == "fail");
            write($"  [{op.Ordinal}] {op.Id} ({op.Role}/{op.Api}): {op.Status} | {timings} | primary: {primaryPass} pass, {primaryFail} fail");
        }
        write("--------------------------------------------------");
    }

    public static string HashBytes(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string HashString(string text) => HashBytes(Encoding.UTF8.GetBytes(text));

    private static JsonNode? Finite(double? value) =>
        value.HasValue && double.IsFinite(value.Value) ? JsonValue.Create(value.Value) : null;
}

internal sealed class OperationRecord
{
    private JsonObject? _options;
    public string Id { get; }
    public int Ordinal { get; }
    public string Name { get; }
    public string Role { get; }
    public string Api { get; }
    public JsonObject? Options
    {
        get => _options;
        set
        {
            _options = value;
            ParameterHash = value != null ? OperationAcceptanceLedger.HashString(value.ToJsonString()) : null;
        }
    }
    public string? ParameterHash { get; private set; }
    public JsonNode? InputProvenance { get; set; }
    public bool SameInput { get; set; }

    public JsonObject InputHashes { get; } = new() { ["gm"] = new JsonObject(), ["pk"] = new JsonObject() };
    public JsonArray Preconditions { get; } = [];
    public JsonObject OutputHashes { get; } = new() { ["gm"] = new JsonObject(), ["pk"] = new JsonObject() };
    public JsonObject Returns { get; } = new() { ["gm"] = 0, ["pk"] = 0 };

    public string Status { get; set; } = "unverified";
    public string PrimaryStatus { get; set; } = "unverified";
    public string AuxiliaryStatus { get; set; } = "none";
    public string RepresentationStatus { get; set; } = "none";
    public bool HasAuxiliaryFailure { get; set; }

    public JsonArray PrimaryChecks { get; } = [];
    public JsonArray AuxiliaryChecks { get; } = [];
    public JsonArray RepresentationContracts { get; } = [];
    public JsonObject Timing { get; } = new();
    public JsonNode? StepRef { get; set; }

    public double? GmMs { get; set; }
    public double? PkMs { get; set; }

    public OperationRecord(
        string id,
        int ordinal,
        string name,
        string role,
        string api,
        JsonObject? options,
        JsonNode? inputProvenance,
        bool sameInput)
    {
        Id = id;
        Ordinal = ordinal;
        Name = name;
        Role = role;
        Api = api;
        Options = options;
        InputProvenance = inputProvenance;
        SameInput = sameInput;

        Timing["boundary"] = "api-only";
        Timing["measurementKind"] = "api-elapsed-wall";
        Timing["gmMs"] = null;
        Timing["pkMs"] = null;
        Timing["coldKind"] = ordinal == 0 ? "fresh-process-first-api" : "subsequent-in-process";
        Timing["warmup"] = 0;
        Timing["samples"] = 0;
        Timing["correctnessQualified"] = false;
        Timing["ratio"] = null;
        Timing["diagnosticRatio"] = null;
    }

    public void AddCheck(string category, JsonObject check)
    {
        var target = category switch
        {
            "primary" => PrimaryChecks,
            "auxiliary" => AuxiliaryChecks,
            "representation" => RepresentationContracts,
            _ => PrimaryChecks
        };
        target.Add((JsonNode)check.DeepClone());
    }

    public string EvaluateStatus()
    {
        if (Status == "not-run")
        {
            PrimaryStatus = "not-run";
            return Status;
        }

        var gmErr = Returns["gm"]?.GetValue<int?>() ?? 0;
        var pkErr = Returns["pk"]?.GetValue<int?>() ?? 0;

        // Primary status
        if (gmErr != 0 || pkErr != 0)
        {
            PrimaryStatus = "fail";
        }
        else if (PrimaryChecks.Any(c => c?["status"]?.GetValue<string>() == "fail"))
        {
            PrimaryStatus = "fail";
        }
        else if (PrimaryChecks.Count == 0)
        {
            PrimaryStatus = "unverified";
        }
        else if (PrimaryChecks.All(c => c?["status"]?.GetValue<string>() is "pass" or "diagnostic"))
        {
            PrimaryStatus = "pass";
        }
        else
        {
            PrimaryStatus = "fail";
        }

        // Auxiliary status
        if (AuxiliaryChecks.Count > 0)
        {
            AuxiliaryStatus = AuxiliaryChecks.Any(c => c?["status"]?.GetValue<string>() == "fail") ? "fail" : "pass";
            HasAuxiliaryFailure = AuxiliaryStatus == "fail";
        }
        else
        {
            AuxiliaryStatus = "none";
            HasAuxiliaryFailure = false;
        }

        // Representation status
        if (RepresentationContracts.Count > 0)
        {
            RepresentationStatus = RepresentationContracts.Any(c => c?["status"]?.GetValue<string>() == "fail") ? "fail" : "pass";
        }
        else
        {
            RepresentationStatus = "none";
        }

        Status = PrimaryStatus;

        var isPass = PrimaryStatus == "pass";
        var qualified = isPass && SameInput && GmMs.HasValue && PkMs.HasValue && GmMs.Value > 0 && PkMs.Value > 0;

        Timing["boundary"] = "api-only";
        Timing["measurementKind"] = "api-elapsed-wall";
        Timing["gmMs"] = GmMs.HasValue ? JsonValue.Create(GmMs.Value) : null;
        Timing["pkMs"] = PkMs.HasValue ? JsonValue.Create(PkMs.Value) : null;
        Timing["coldKind"] = Ordinal == 0 ? "fresh-process-first-api" : "subsequent-in-process";
        Timing["warmup"] = 0;
        Timing["samples"] = GmMs.HasValue && PkMs.HasValue ? 1 : 0;
        Timing["correctnessQualified"] = qualified;
        Timing["ratio"] = qualified ? JsonValue.Create(GmMs!.Value / PkMs!.Value) : null;
        Timing["diagnosticRatio"] = GmMs.HasValue && PkMs.HasValue && PkMs.Value > 0 ? JsonValue.Create(GmMs.Value / PkMs.Value) : null;

        return Status;
    }

    public JsonObject ToJsonObject()
    {
        EvaluateStatus();
        return new JsonObject
        {
            ["operationId"] = Id,
            ["ordinal"] = Ordinal,
            ["name"] = Name,
            ["role"] = Role,
            ["api"] = Api,
            ["options"] = Options?.DeepClone(),
            ["parameterHash"] = ParameterHash,
            ["inputProvenance"] = InputProvenance?.DeepClone(),
            ["inputHashes"] = InputHashes.DeepClone(),
            ["preconditions"] = Preconditions.DeepClone(),
            ["outputHashes"] = OutputHashes.DeepClone(),
            ["returns"] = Returns.DeepClone(),
            ["status"] = Status,
            ["primaryStatus"] = PrimaryStatus,
            ["auxiliaryStatus"] = AuxiliaryStatus,
            ["representationStatus"] = RepresentationStatus,
            ["hasAuxiliaryFailure"] = HasAuxiliaryFailure,
            ["primaryChecks"] = PrimaryChecks.DeepClone(),
            ["auxiliaryChecks"] = AuxiliaryChecks.DeepClone(),
            ["representationContracts"] = RepresentationContracts.DeepClone(),
            ["timing"] = Timing.DeepClone(),
            ["stepRef"] = StepRef?.DeepClone()
        };
    }
}

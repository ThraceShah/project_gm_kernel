global using OperationReturnCode = System.Int32;
global using OperationProcessExitCode = System.Int32;
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
    public string OperationSchemaVersion { get; set; } = "operation-layered-v2";
    public OperationProcessExitCode? LegacyExitCode { get; set; }
    public OperationProcessExitCode? InProcessExitCode { get; set; }
    public string? ScenarioFailure { get; set; }
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
        bool sameInput = false,
        string interpretationVersion = "operation-rules-v2")
    {
        if (_byId.ContainsKey(id))
            throw new InvalidOperationException($"Duplicate operation ID: {id}");

        var ordinal = Operations.Count;
        var op = new OperationRecord(id, ordinal, name, role, api, options, inputProvenance, sameInput,interpretationVersion);
        Operations.Add(op);
        _byId[id] = op;
        return op;
    }

    public OperationRecord? GetOperation(string id) => _byId.GetValueOrDefault(id);

    public void RecordReturn(string opId, string kernel, OperationReturnCode errorCode)
    {
        var op = GetOperation(opId);
        if (op == null) return;
        op.Returns[kernel.ToLowerInvariant()] = errorCode;
        var side=kernel.ToLowerInvariant();
        if(!OperationAcceptanceRules.IsAcceptedReturn(op.InterpretationVersion,op.AcceptanceKind,op.Api,op.PrimaryContract,op.ExpectedReturns[side],errorCode))RecordCheck(opId,"primary",opId+"-api-return-"+side,"Recorded API return","fail",gm:side=="gm"?errorCode:null,pk:side=="pk"?errorCode:null,note:"Unexpected API return from "+side);
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
        op.SetInputHashes(side, node);
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
        op.SetOutputHashes(side, node);
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
            ["threshold"] = Finite(threshold),
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
        {
            if (statusOverride == "not-run" && (op.Returns.Any(p => p.Value != null) || op.PrimaryChecks.Count != 0 || op.GmMs.HasValue || op.PkMs.HasValue))
                statusOverride = "fail";
            op.SetTerminalStatus(statusOverride);
            if (statusOverride is "fail" or "timeout")
                RecordCheck(opId, "primary", opId + "-terminal", "Explicit operation termination", "fail", note: statusOverride);
        }
        op.EvaluateStatus();
    }

    public void EndScenario(OperationProcessExitCode? legacyExitCode, double? elapsedMs = null)
    {
        LegacyExitCode = legacyExitCode;
        ScenarioWallElapsedMs = elapsedMs;

        double sumGm = 0, sumPk = 0;
        bool hasGm = false, hasPk = false;
        foreach (var op in Operations)
        {
            op.EvaluateStatus();
            if (op.GmMs is > 0 && double.IsFinite(op.GmMs.Value)) { sumGm += op.GmMs.Value; hasGm = true; }
            if (op.PkMs is > 0 && double.IsFinite(op.PkMs.Value)) { sumPk += op.PkMs.Value; hasPk = true; }
        }

        ScenarioCapturedApiMs = new JsonObject
        {
            ["gm"] = hasGm ? Finite(sumGm) : null,
            ["pk"] = hasPk ? Finite(sumPk) : null
        };

        var anyFail = Operations.Any(o => o.Status == "fail") || legacyExitCode.HasValue && legacyExitCode != 0 || ScenarioFailure != null;
        var anyTimeout = Operations.Any(o => o.Status == "timeout");

        var failedParents=Operations.Where(o=>o.Status is "fail" or "timeout").Select(o=>o.ParentOperationId).Where(id=>id!=null).ToHashSet();
        FirstFailedOperationId = Operations.FirstOrDefault(o => (o.Status is "fail" or "timeout") && !failedParents.Contains(o.Id))?.Id;

        if (anyTimeout)
            ScenarioStatus = "timeout";
        else if (anyFail)
            ScenarioStatus = "fail";
        else if (Operations.Count == 0)
            ScenarioStatus = "missing";
        else
            ScenarioStatus = legacyExitCode == 0 && Operations.All(o => o.Status == "pass") ? "pass" : "unverified";
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
            ["legacyExitCodeSource"] = LegacyExitCode.HasValue ? "caller-reported" : "not-observed-in-process",
            ["inProcessExitCode"] = InProcessExitCode,
            ["scenarioFailure"] = ScenarioFailure,
            ["checkVersion"] = CheckVersion,
            ["scenarioWallElapsedMs"] = Finite(ScenarioWallElapsedMs),
            ["scenarioElapsedMs"] = Finite(ScenarioElapsedMs),
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

    internal static JsonNode? Finite(double? value) =>
        value.HasValue && double.IsFinite(value.Value) ? JsonValue.Create(value.Value) : null;
}

// API scopes start before the call. Dispose records an owned failure if a raw
// call, verification or cleanup throws before Complete, without catching it.
internal sealed class OperationStepScope : IDisposable
{
    private readonly OperationAcceptanceLedger? _ledger;
    private readonly OperationRecord? _operation;
    private bool _completed;
    internal string? Id => _operation?.Id;
    internal OperationRecord? Record => _operation;
    internal OperationStepScope(OperationAcceptanceLedger? ledger,string? parent,string suffix,string api,JsonObject? options=null,string interpretationVersion="operation-rules-v2")
    {
        _ledger=ledger;
        if(ledger==null||parent==null)return;
        _operation=ledger.BeginOperation(parent+"/"+suffix,suffix,"lifecycle",api,options,new JsonObject{["scope"]="integration-step",["parentOperationId"]=parent},interpretationVersion:interpretationVersion);
        _operation.ParentOperationId=parent;
    }
    internal OperationReturnCode Return(string side,OperationReturnCode error)
    {
        if(_operation!=null)_ledger!.RecordReturn(_operation.Id,side,error);
        return error;
    }
    internal void Complete()
    {
        if(_operation!=null)_ledger!.EndOperation(_operation.Id);
        _completed=true;
    }
    public void Dispose()
    {
        if(!_completed&&_operation!=null)_ledger!.EndOperation(_operation.Id,"fail");
    }
}

internal sealed class OperationRecord
{
    private JsonObject? _options;
    public string Id { get; }
    public int Ordinal { get; }
    public string Name { get; }
    public string Role { get; }
    public string Api { get; }
    public string InterpretationVersion { get; }
    public string? ParentOperationId { get; set; }
    public string AcceptanceKind { get; set; } = "normal";
    public JsonObject ExpectedReturns { get; } = new();
    public JsonObject? Options
    {
        get => (JsonObject?)_options?.DeepClone();
        set
        {
            _options = (JsonObject?)value?.DeepClone();
            ParameterHash = _options != null ? OperationAcceptanceLedger.HashString(_options.ToJsonString()) : null;
        }
    }
    public string? ParameterHash { get; private set; }
    private JsonNode? _inputProvenance;
    public JsonNode? InputProvenance { get => _inputProvenance?.DeepClone(); set => _inputProvenance = value?.DeepClone(); }
    public bool SameInput { get; set; }
    public string? PrimaryContract { get; set; }
    public string? TerminalStatus { get; private set; }
    public void SetTerminalStatus(string status)
    {
        if (TerminalStatus is not ("fail" or "timeout")) TerminalStatus = status;
    }

    private readonly JsonObject _inputHashes = new() { ["gm"] = new JsonObject(), ["pk"] = new JsonObject() };
    public JsonObject InputHashes => (JsonObject)_inputHashes.DeepClone();
    public JsonObject EntityReferences { get; } = new();
    internal void SetInputHashes(string side, JsonObject hashes) => _inputHashes[side] = hashes.DeepClone();
    public JsonArray Preconditions { get; } = [];
    private readonly JsonObject _outputHashes = new() { ["gm"] = new JsonObject(), ["pk"] = new JsonObject() };
    public JsonObject OutputHashes => (JsonObject)_outputHashes.DeepClone();
    internal void SetOutputHashes(string side, JsonObject hashes) => _outputHashes[side] = hashes.DeepClone();
    public JsonObject Returns { get; } = new() { ["gm"] = null, ["pk"] = null };

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
        bool sameInput,
        string interpretationVersion="operation-rules-v2")
    {
        Id = id;
        Ordinal = ordinal;
        Name = name;
        Role = role;
        Api = api;
        InterpretationVersion=interpretationVersion;
        Options = options;
        InputProvenance = inputProvenance;
        SameInput = sameInput;

        Timing["boundary"] = "api-only";
        Timing["measurementKind"] = "api-elapsed-wall";
        Timing["gmMs"] = null;
        Timing["pkMs"] = null;
        Timing["coldKind"] = "unknown";
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

        Timing["gmMs"] = OperationAcceptanceLedger.Finite(GmMs);
        Timing["pkMs"] = OperationAcceptanceLedger.Finite(PkMs);
        Timing["samples"] = GmMs.HasValue && PkMs.HasValue ? 1 : 0;
        Timing["invalidMeasurement"] = (GmMs.HasValue && (!double.IsFinite(GmMs.Value) || GmMs.Value <= 0)) || (PkMs.HasValue && (!double.IsFinite(PkMs.Value) || PkMs.Value <= 0));
        var result = OperationAcceptanceRules.Evaluate(BuildJsonObject());
        PrimaryStatus = result.PrimaryStatus;
        Status = result.Status;
        Timing["correctnessQualified"] = result.Qualified;
        Timing["unqualifiedReasons"] = result.Reasons.DeepClone();
        Timing["ratio"] = result.Qualified ? OperationAcceptanceLedger.Finite(GmMs!.Value / PkMs!.Value) : null;
        Timing["diagnosticRatio"] = GmMs is > 0 && PkMs is > 0 && double.IsFinite(GmMs.Value) && double.IsFinite(PkMs.Value) ? OperationAcceptanceLedger.Finite(GmMs.Value / PkMs.Value) : null;

        return Status;
    }

    public JsonObject ToJsonObject()
    {
        EvaluateStatus();
        return BuildJsonObject();
    }

    private JsonObject BuildJsonObject()
    {
        return new JsonObject
        {
            ["operationId"] = Id,
            ["ordinal"] = Ordinal,
            ["name"] = Name,
            ["role"] = Role,
            ["api"] = Api,
            ["interpretationVersion"] = InterpretationVersion,
            ["parentOperationId"] = ParentOperationId,
            ["acceptanceKind"] = AcceptanceKind,
            ["expectedReturns"] = ExpectedReturns.DeepClone(),
            ["primaryContract"] = PrimaryContract,
            ["terminalStatus"] = TerminalStatus ?? (Status == "not-run" ? "not-run" : null),
            ["sameInput"] = SameInput,
            ["options"] = Options?.DeepClone(),
            ["parameterHash"] = ParameterHash,
            ["inputProvenance"] = InputProvenance?.DeepClone(),
            ["inputHashes"] = InputHashes.DeepClone(),
            ["entityReferences"] = EntityReferences.DeepClone(),
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

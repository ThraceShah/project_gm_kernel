using System.Text.Json.Nodes;

// Shared interpretation of recorded facts. Callers must supply the evidence;
// neither a successful API return nor an API name declares a primary contract.
internal static class OperationAcceptanceRules
{
    internal sealed record Result(string Status, string PrimaryStatus, bool Qualified, JsonArray Reasons);
    internal static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    internal static double? Number(JsonNode? n) => n is not JsonValue v ? null : v.TryGetValue<double>(out var d) ? d : v.TryGetValue<int>(out var i) ? i : v.TryGetValue<long>(out var l) ? l : null;
    internal static bool IsTrue(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    internal static bool IsHash(JsonNode? n) => Text(n) is { Length: 64 } s && s.All(Uri.IsHexDigit);

    internal static Result Evaluate(JsonObject op)
    {
        var reasons = new JsonArray();
        void Reason(string s) => reasons.Add((JsonNode?)JsonValue.Create(s));
        var id = Text(op["operationId"]);
        var terminal = Text(op["terminalStatus"]);
        var primary = op["primaryChecks"] as JsonArray ?? [];
        var ownerValid = true;
        foreach (var category in new[] { "primaryChecks", "auxiliaryChecks", "representationContracts" })
        {
            var expected = category == "primaryChecks" ? "primary" : category == "auxiliaryChecks" ? "auxiliary" : "representation";
            foreach (var check in op[category] as JsonArray ?? [])
                if (Text(check?["owner"]) != id || Text(check?["category"]) != expected) ownerValid = false;
        }
        if (!ownerValid) Reason("invalid-check-owner-or-category");

        bool HasReturn(string side, out bool failed)
        {
            var node = op["returns"]?[side];
            var known = node is JsonValue v && v.TryGetValue<int>(out _);
            failed = known && node!.GetValue<int>() != 0;
            return known;
        }
        var gmReturn = HasReturn("gm", out var gmFailed);
        var pkReturn = HasReturn("pk", out var pkFailed);
        if (!gmReturn || !pkReturn) Reason("unknown-api-return");
        if (gmFailed || pkFailed) Reason("api-return-failure");
        var explicitFailure = terminal is "fail" or "timeout" || gmFailed || pkFailed || primary.Any(c => Text(c?["status"]) == "fail");
        var contract = Text(op["primaryContract"]);
        var api = Text(op["api"]);
        // These requirements are the existing Compare capture checks: body count,
        // both BODY_check calls, five topology counts, strict compare and 15 mass values.
        var supported = contract == "capture-body-result-v1" && api is "PK_BODY_boolean" or "PK_BODY_boolean_2" or "PK_BODY_fix_blends" or "PK_SURF_create_blend";
        var complete = supported;
        var ordinal = Number(op["ordinal"]);
        if (ordinal is null || ordinal < 0 || ordinal != Math.Truncate(ordinal.Value)) { Reason("invalid-ordinal"); complete = false; }
        if (supported)
        {
            var prefix = (Number(op["stepRef"]) ?? ordinal)?.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-";
            var required = new List<string> { "body-count", "True-body-check", "False-body-check", "equivalence" };
            for (var i = 0; i < 5; i++) required.Add("count-" + i);
            for (var i = 0; i < 15; i++) required.Add("mass-" + i);
            foreach (var suffix in required)
                if (primary.Count(c => Text(c?["id"]) == prefix + suffix && Text(c?["owner"]) == id && Text(c?["category"]) == "primary" && Text(c?["status"]) == "pass") != 1) complete = false;
        }
        else Reason("undeclared-or-unsupported-primary-contract");
        if (!complete) Reason("missing-required-primary");
        if (primary.Any(c => Text(c?["status"]) is not ("pass" or "diagnostic"))) complete = false;
        var hasExecution = gmReturn || pkReturn || primary.Count != 0 || Number(op["timing"]?["gmMs"]).HasValue || Number(op["timing"]?["pkMs"]).HasValue;
        if (terminal == "not-run" && hasExecution) Reason("not-run-contradicts-execution");
        var primaryStatus = explicitFailure ? "fail" : terminal == "not-run" && !hasExecution ? "not-run" : terminal != "not-run" && complete && ownerValid && gmReturn && pkReturn ? "pass" : "unverified";
        var status = terminal == "timeout" ? "timeout" : primaryStatus;

        var options = op["options"];
        if (options is null || !IsHash(op["parameterHash"]) || Text(op["parameterHash"]) != OperationAcceptanceLedger.HashString(options.ToJsonString())) Reason("missing-or-invalid-parameter-snapshot");
        if (options is not JsonObject pair || pair["gm"] is null || pair["pk"] is null || !JsonNode.DeepEquals(pair["gm"], pair["pk"])) Reason("unpaired-parameters");
        foreach (var side in new[] { "gm", "pk" })
        {
            var input = op["inputHashes"]?[side];
            if (!IsHash(input?["target"]) || input?["tools"] is not JsonArray tools || tools.Any(h => !IsHash(h))) Reason("missing-input-hashes-" + side);
        }
        var provenance = op["inputProvenance"] as JsonObject;
        if (!IsTrue(op["sameInput"]) || Text(provenance?["scope"]) != "isolated-same-input" || string.IsNullOrWhiteSpace(Text(provenance?["sameInputEvidence"]))) Reason("same-input-unproven");
        var timing = op["timing"];
        var gm = Number(timing?["gmMs"]); var pk = Number(timing?["pkMs"]);
        if (gm is not > 0 || pk is not > 0 || !double.IsFinite(gm.Value) || !double.IsFinite(pk.Value) || !double.IsFinite(gm.Value / pk.Value) || gm.Value / pk.Value <= 0 || IsTrue(timing?["invalidMeasurement"])) Reason("invalid-or-missing-measurement");
        if (Text(timing?["boundary"]) != "api-only" || Text(timing?["measurementKind"]) != "api-elapsed-wall" || Number(timing?["samples"]) is not > 0 || Number(timing?["warmup"]) is not >= 0) Reason("invalid-timing-boundary");
        if (primaryStatus != "pass") Reason("primary-not-pass");
        return new(status, primaryStatus, reasons.Count == 0, reasons);
    }
}

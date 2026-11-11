#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// Pure PK controls for comparator scope; no GM assembly or modeling acceptance.
unsafe
{
    if (!ParasolidScriptHost.TryStartSession("Compare audit control", out var host, out var message))
        throw new InvalidOperationException(message);
    using (host)
    {
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name!.StartsWith("ProjectGmKernel", StringComparison.Ordinal)))
            throw new InvalidOperationException("Control loaded a GM assembly");
        PK_BODY_t reference, equal, wrong;
        Check(PK_BODY_create_solid_block(1, 1, 1, null, &reference), "reference block");
        Check(PK_BODY_create_solid_block(1, 1, 1, null, &equal), "equal block");
        Check(PK_BODY_create_solid_block(1, 1, 1.01, null, &wrong), "wrong block");
        var defaults = new PK_DEBUG_BODY_compare_o_t();
        PK_DEBUG_BODY_compare_r_t result;
        Check(PK_DEBUG_BODY_compare(reference, wrong, &defaults, &result), "default compare");
        try
        {
            Console.WriteLine($"default wrong-height: max_diffs={defaults.max_diffs} global={result.global_result} local={result.local_result}");
            if (result.global_result != PK_DEBUG_global_res_no_diffs_c)
                throw new InvalidOperationException("Unexpected global topology difference");
        }
        finally { Check(PK_DEBUG_BODY_compare_r_f(&result), "free default compare"); }
        ParasolidBodyCompare.AssertEquivalent(reference, equal, "equal block");
        Reject(reference, wrong, "wrong-height block", "body geometry");
        var line = new PK_LINE_sf_t(new(new(0, 0, 0), new(1, 0, 0)));
        PK_CURVE_t curve;
        Check(PK_LINE_create(&line, &curve), "reference line");
        Check(PK_CURVE_make_wire_body(curve, new(0, 1), &reference), "reference wire");
        Check(PK_CURVE_make_wire_body(curve, new(0, 2), &wrong), "wrong trim wire");
        Reject(reference, wrong, "wrong trim wire", "unmatched wire edge");
        line.basis_set.location.coord[1] = 1;
        Check(PK_LINE_create(&line, &curve), "translated line");
        Check(PK_CURVE_make_wire_body(curve, new(0, 1), &wrong), "translated wire");
        Reject(reference, wrong, "translated wire", "unmatched wire edge");
        Console.WriteLine("PASS pure-PK comparator scope: equal positive, wrong solid/trim/translation negatives; no GM assembly");
    }
}

static void Reject(PK_BODY_t reference, PK_BODY_t actual, string label, string reason)
{
    try { ParasolidBodyCompare.AssertEquivalent(reference, actual, label); }
    catch (InvalidOperationException exception) when (exception.Message.Contains(reason, StringComparison.Ordinal))
    { Console.WriteLine("PASS rejected " + label); return; }
    throw new InvalidOperationException(label + " incorrectly passed");
}

static void Check(PK_ERROR_code_t error, string label) => ParasolidScriptHost.Check(error, label);

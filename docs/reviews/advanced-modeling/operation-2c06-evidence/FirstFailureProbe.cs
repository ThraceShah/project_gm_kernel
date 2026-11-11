#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseOperationAcceptanceLedger=true
using System.Runtime.CompilerServices;

// Mirrors the ordering of the real CompareFresh verification/cleanup boundary,
// using actual ledger/child scope code and synthetic failure facts, no PK.
var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);
var ledger=new OperationAcceptanceLedger("first-failure-control","synthetic comparison followed by cleanup failure");
ledger.BeginOperation("verify","verification integration",api:"product-verification-integration");
try
{
    try
    {
        ledger.RecordCheck("verify","primary","strict-compare","synthetic strict comparison failure","fail");
        throw new InvalidOperationException("earlier comparison failure");
    }
    finally
    {
        using var cleanup=new OperationStepScope(ledger,"verify","delete-received","PK_ENTITY_delete");
        cleanup.Return("pk",22);throw new InvalidOperationException("later cleanup failure");
    }
}
catch(InvalidOperationException){ledger.EndOperation("verify","fail");}
finally{ledger.EndOperation("verify");}
ledger.EndScenario(134);
var output=Path.Combine(root,"temp_docs/operation-2c06-review");Directory.CreateDirectory(output);ledger.SaveSidecar(Path.Combine(output,"first-failure.json"));
Console.WriteLine("earlier owned failure=verify/strict-compare; later cleanup=verify/delete-received; actual firstFailedOperationId="+ledger.FirstFailedOperationId);
Console.WriteLine("Both checks remain; this control is about first-failure attribution, not model correctness.");
return ledger.FirstFailedOperationId=="verify"?0:1;
static string Here([CallerFilePath]string p="")=>p;

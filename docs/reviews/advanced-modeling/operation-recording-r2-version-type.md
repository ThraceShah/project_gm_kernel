# Reject explicit invalid interpretation versions

Independent fixed-a0 review supplied a complete synthetic pass with numeric `interpretationVersion=99`. The original public validator accepted it because failed string conversion fell back to missing-version v1. This follow-up distinguishes an absent property from any explicitly invalid/null/unknown version. Only absence selects legacy v1; explicit non-string, null, and unknown string values remain unverified and cannot qualify. Existing missing-version historical interpretation and the accepted a2 failure/snapshot rules are unchanged.

Original dataset copied byte-for-byte to `temp_docs/operation-version-type/numeric-version-forged-pass`; manifest SHA256 `ba1ac0f08a69b4fdc09c28d2826d740fbf4ec5d7c524de88e9ed88b266fc7d17`. A before driver compiles exact a0 ledger/rules/public-validator sources and accepts the original dataset (exit 0). The current public disk validator rejects those same bytes (exit 1, four discrepancies); the record is not rewritten to make the test pass. Numeric, explicit null and object versions also have public disk-entry negative controls in the shared self-test. The original missing-version historical manifest `57117536c174211f85be9a2c20d2560c6925b768491ff50d831340f7dd20dca0` still validates with errors empty and retains unverified.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-version-type/before/ValidateOriginal.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- ../temp_docs/operation-version-type/numeric-version-forged-pass
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- ../temp_docs/operation-version-compatibility/old-only-data
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/OperationAcceptanceSelfTest.cs -c Release -p:UseSharedCompilation=false
```

Evidence: `temp_docs/operation-version-type/{original-input-before,original-input-after,missing-version-legacy,self-test}.log`. This commit excludes pending leaf changes and grants no new geometry or timing acceptance.

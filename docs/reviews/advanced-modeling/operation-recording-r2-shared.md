# R2 shared recording correction

Base: `5d45dad0b92bd28276f88c1f2cb4fcf35deda6ef`. This is an implementation checkpoint for independent review, not acceptance of A1–A7.

The shared state interpreter now retains explicit failure/timeout, leaves unrecorded returns null, refuses duplicate Begin IDs, clones option/provenance snapshots, and withholds ratios for incomplete contracts or invalid measurements. Required primary checks for `capture-body-result-v1` come from the existing capture Compare routine: result count, both BODY_check calls, five topology counts, strict equivalence and fifteen mass values. Other contracts remain unverified; auxiliary and representation failures retain their own owners and do not erase complete primary success. Parameter snapshots, paired sides, input SHA256 evidence and an explicit isolated same-input provenance are additionally required for timing qualification. Input/output entity tags cannot masquerade as hash fields.

The validator derives status and qualification from recorded facts using the same interpreter. Its disk-based negative tests independently mutate actual serialized records and call the public validation entry point. Existing validator test expectations were retained; its former incomplete positive operation fixture now supplies the complete synthetic contract. These fixtures prove recording semantics only, not geometry correctness or real same-input experiments.

Read-only reviewer probes were copied unchanged into `temp_docs/operation-review-5d/`, retaining these SHA256 values:

- `LedgerProbe.cs`: `c76d37a2ad92e5b642bc21b870f1d03dbb5fbda5acf8116445c0a66cfbaf3e85`
- `ValidationProbe.cs`: `fbdeeb3a8415a7d0c598af337cfaf613461a914c5516c58d0950b8a3a315d622`

Commands (managed/BCL only, no NativeAOT or PK session):

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-review-5d/LedgerProbe.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-review-5d/ValidationProbe.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/OperationAcceptanceSelfTest.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- --self-test
```

Before correction, the original ledger probe reproduced pass resurrection, default zero returns, silent duplicate merge, Infinity qualification/export crash and mutable hash mismatch. After correction it prints `before=fail after=fail qualified=false returns={"gm":null,"pk":null}`, then exits 134 at the explicitly rejected duplicate ID. Its unchanged program cannot reach subsequent statements under the new duplicate contract; the new self-test separately covers all nonfinite timing values, mutation through original and returned nested objects, timeout, unknown scenario and auxiliary failure coexistence. Original validation mutations now all reject their operation pseudo-evidence, including previously rejected wrong-owner/missing-PK/duplicate-ID cases. Its two legacy cases still validate without manufactured operations; legacy interpretation is a remaining Refresh task.

Evidence logs: `temp_docs/operation-review-5d/{ledger-before,ledger-after,validation-before,validation-after,contract-self-test,validator-self-test}.log`. Capture/Refresh adaptation, scenario measurements/check-source versions, repetition identity, leaf exception attribution/real returns and optional display receipts are subsequent work. No historical run bytes, kernel source, acceptance thresholds, original process exits or auxiliary failures were changed. No real case is claimed qualified by this checkpoint.

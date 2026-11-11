# First recorded failure attribution (2c06 narrow correction)

Base: `2c06b9da80737f2d501bd5e1886c3ea1494908c7`, branch `fix/operation-recording-r2`. This correction addresses the independent `reviewctx_f68` counterexample only. The author of this correction is Codex; prior shared acceptance and the five limited real leaf cases are not repeated or expanded.

`EndScenario` previously excluded every failed parent that had a failed child. In ShortEdge's existing verification/finally ordering, an owned strict comparison failure precedes cleanup, but a later failed delete hid that earlier owner. The ledger already appends `AllChecks` in recording order, including unexpected returns and explicit termination checks. Selection now takes the owner of its first recorded primary failure whose operation is failed or timed out. This also keeps an earlier child first when its parent later records propagation or an independent owned failure; operation begin ordinals do not override event order. If there is no recorded failure-order evidence, the existing fallback remains.

No operation interpretation/schema version or reasons signature changes. Historical JSON is not reinterpreted or rewritten. Geometry, kernel code, ShortEdge checks and execution order, cleanup behavior, caller exit codes, receipts, registry, UI, hosts and Native code are unchanged. In particular, A7's original exit 134 / PK22 / GM5002, the correct surface receipt and existing failed runs remain evidence, without a new acceptance claim.

## Original counterexample

The reviewer tree was read only. Its probe was copied byte-for-byte to `temp_docs/operation-first-failure/before/probe/FirstFailureProbe.cs`, alongside frozen base ledger/rules, before editing production source. The original probe SHA256 is `8ee9dc3e339c90164128a18bdc7b218bdac4e3b49451270f0579aed7d304b48c`; the frozen ledger SHA256 is `a780760600aa775daa44bd9725ca771d7363d53333c3edc6e749274a13930f05`.

The before run exits 1 and names `verify/delete-received`. Its JSON SHA256 is `db8e77e95fa32e6354b912789662606036d88b477d59b2bcf9a29a389bb38988`, identical to the reviewer artifact preserved as `temp_docs/operation-first-failure/original-first-failure.json`. The exact same probe bytes with corrected ledger/rules, in `temp_docs/operation-first-failure/after/probe`, exit 0 and name `verify`. The after JSON SHA256 is `6c7c3624223cc30311f6ad7ede61560b2062d7e2f207a8a6d2ae65fa7a1fe81c`. Structural comparison confirms that only `firstFailedOperationId` differs; every original failure fact and `legacyExitCode=134` remains. Separate before/after JSON and logs are retained under `temp_docs/operation-first-failure/`.

## Limited verification

The existing recording self-test now exercises actual Ledger/StepScope calls for five controls: parent owned verification before cleanup, child before parent propagation, child before a later independent parent owned check, passing parent primary with failed cleanup, and normal passing recording. Assertions check first cause, retained failure owners and raw PK22, unchanged caller exit 0, scenario failure with preserved parent primary pass, and absence of duplicate operations. Each control persists ledger/manifest JSON, checks the loaded first cause and failure facts, and invokes the existing public disk validator on that directory. The validator checks operation facts; the explicit disk assertions check first-cause attribution.

Commands (all exit 0 unless specified):

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-first-failure/before/probe/FirstFailureProbe.cs -c Release -p:UseSharedCompilation=false
# Expected before exit 1.
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-first-failure/after/probe/FirstFailureProbe.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- --self-test
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/OperationAcceptanceSelfTest.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- ../temp_docs/operation-version-type/numeric-version-forged-pass
# Expected rejection: exit 1, the same four discrepancies.
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- ../temp_docs/operation-version-compatibility/old-only-data
```

The numeric-version manifest remains SHA256 `ba1ac0f08a69b4fdc09c28d2826d740fbf4ec5d7c524de88e9ed88b266fc7d17`; the missing-version legacy manifest remains `57117536c174211f85be9a2c20d2560c6925b768491ff50d831340f7dd20dca0`, with disk validation errors empty. Logs are `before.log`, `after.log`, `validator-self-test.log`, `recording-self-test.log`, `numeric-version.log`, `missing-version-legacy.log` and `evidence-check.log` under `temp_docs/operation-first-failure/`. Builds emit the existing ILCompiler package advisory warning; no NativeAOT compile/publish was invoked. No PK/modeling run, full suite or benchmark was required for this BCL recording-only fix.

This is self-verification of a narrow candidate. Independent replay by the original reviewer with the original input, and overall acceptance of the earlier recording candidates, remain outstanding. Nothing was merged into main, deployed or changed live.

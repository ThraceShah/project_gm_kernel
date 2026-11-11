# ShortEdge / Capture final recording checkpoint

Capture now sends actual raw returns through the shared RecordReturn contract, preserving unexpected API failure checks rather than directly overwriting a returns object. ShortEdge keeps the same observed operation object for its manual compare/mass/auxiliary checks, records actual raw GM/PK returns when Capture is disabled, and stores SHA256 of real saved output XT bytes. Native output hashing occurs when that snapshot is saved. Entity references retain Capture's existing input/output arrays; no input identity or isolated same-input claim is inferred from tags. Manual evidence paths are relative in the standalone ledger; when Capture does not copy that XT into its own root, its manual check evidence is null rather than a false cross-root link.

CompareFresh is labeled product-verification integration, not a Boolean API which it does not call. Its explicit failure boundary covers transmit/receive, finite/trim/material checks and cleanup, preserving existing failure counts, assertions and exceptions. Cleanup API children retain actual PK returns. Per-invocation IDs include an automatic ordinal instead of silently merging repeated verification calls. Finally saves unknown process exit separately from in-process diagnostics; the temporary supervisor records the actual exit for newly collected standalone runs.

The real Capture check exposed an implementation bug in the earlier fallback: two auxiliary checks before the first Before both attempted `BeginOperation(capture-internal-0)`, creating an observer contract error even though the original oracle exited 0 and the actual fillet's 26 primary checks passed. That failed run is preserved unchanged at `temp_docs/operation-short-capture-final/data/runs/r2-short-edge-owner-final`. The corrected owner for preparation checks before any modeled operation is explicitly scenario/auxiliary, not a fictitious observer API or a missing owner. No auxiliary failure was deleted to make the test pass.

ShortEdge also opens an explicit case scope around each recipe. Logical capture keys include the case occurrence and nested scope, while preserving the original key format when there is no case scope. This prevents a native-only/failed prior case with a missing GM peer from pairing with a later repetition's GM call. Scope exit restores the previous case/observer context, and preparation checks in a new case start at scenario ownership. Explicit duplicate Begin IDs remain contract errors.

Validation:

| Entry | Result | Evidence |
| --- | --- | --- |
| Standalone default ShortEdge, one recipe | raw exit 0 | `temp_docs/operation-leaf-r2/short-edge-final` |
| Final Capture default ShortEdge, one recipe/sample | original oracle 0, Refresh 0, validation errors empty | `temp_docs/operation-short-case-final/data/runs/r2-short-edge-case-scope-final` |
| Shared-return Capture Boolean, one recipe/sample | original oracle 0, Refresh 0, validation errors empty | `temp_docs/operation-final-capture/data/runs/r2-capture-shared-returns-final` |
| Controlled real BeginObservedOperation / case/context entry | exit 0, four assertions | `temp_docs/operation-review-5d/context-case-owner-final.log` |

The final ShortEdge capture has one modeled operation, actual GM/PK returns 0/0, 26 passing primary checks with the same owner, and preserved input/output references. Preparation checks have explicit per-sample scenario owners. The summary remains single-operation; main operation and case ratios are null because input hashes/same-input evidence are unknown. This is not an isolated same-GM-target experiment or an all-96 acceptance claim.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-review-5d/RunLeaf.cs -c Release -p:UseSharedCompilation=false -- ShortEdgeBlendOracle.cs default ../temp_docs/operation-leaf-r2/short-edge-final
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/RefreshWebProgress.cs -c Release -p:UseSharedCompilation=false -- --case short-edge.a1-r0.5-t1e-07-reversefalse-default --samples 1 --seconds 120 --case-seconds 110 --run-id r2-short-edge-case-scope-final --output ../temp_docs/operation-short-case-final/data
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-review-5d/ContextOwnerProbe.cs -c Release -p:UseSharedCompilation=false
```

The controlled probe seeds a documented native-only dictionary state and tests real owner-entry/scope code, including repeated and nested case identity; it invokes no Before, mesh, PK initializer or model API. The real default recipe does not exercise saved-probe failures or the full ShortEdge lifecycle. A first standalone compile attempt caught `received` scoped inside a new try block but cleaned up outside it; that compile failure remains in `temp_docs/operation-leaf-r2/short-edge-default`, and the corrected runs above compile and execute successfully. No original assertion, geometry threshold, auxiliary failure, native/host code, historical run or UI file was changed.

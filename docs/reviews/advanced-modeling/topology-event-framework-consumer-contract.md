# Finite-root consumption by the two-tool Boolean

This bounded repair starts from `9b2f4c6c989a393fd2cbaa3e66d6ea5b52035017`.
It preserves the independently accepted default uncut blend scope. The subsequent
Boolean, exact stationary-end consumer, and saved classifications have separate
results. The rejected `ec393598` public trigonometric changes are absent.

## Reproducer and identical inputs

`scripts/RollingBooleanConsumerOracle.cs` imports the unchanged
`tests/KernelTests/Fixtures/generated-short-edge-input.x_t`, selects the
cylinder/cylinder ellipse and the adjacent plane/plane line by incidence, then
marks radius `.5` with tolerance `1e-7` and fixes with version 31. It constructs
the two original `ProjectedRollingCellFailureRestoresTargetAndBothToolsAfterUnion`
tools through the managed API. It saves each actual GM-produced input before
either Boolean and supplies those same bytes individually to genuine PK.

The script uses PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9` and the existing
`ParasolidScriptHost` / `ParasolidBodyCompare`. The two generated version-19
Boolean option layouts are checked for equal size. The genuine binding's
default options with `function=subtract` match the original test options;
the identical bytes are used for GM. No handwritten ABI or session host is used.
Each input is received and full-checked by real PK before the native Boolean.
Output full checks, strict comparison and the unchanged normalized-15 mass
threshold `1e-7` run when both operations return bodies. An injection run
additionally requires reaching the intended callback and byte-identical rollback
of all three inputs.

Run from the project root, choosing a **new** output name for each experiment:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/RollingBooleanConsumerOracle.cs -c Release \
  -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false \
  -p:GmInProcessLinkTasks=true -- consumer-unique-name
```

Add `--inject` for the original projected-cell allocation failure and `--reverse`
for the other mark order. Evidence is saved in
`temp_docs/contract-sol/<output-name>/`. Existing evidence directories are
rejected rather than overwritten. `inputs.json` records the three SHA-256 hashes,
API, function, option version/tolerance and the hash of `boolean-options.bin`.
Input/output XT and binary option snapshots remain ignored evidence, not SDK data.

## Actual 9b baseline

The first run, `consumer-baseline.log` with artifacts in `baseline/`, returned
process exit **134**, because the GM Boolean contract was not fulfilled.
It did not report a passing experiment merely because diagnostics completed.

| Operation | Actual result |
| --- | --- |
| Managed Fix | error 0, two products, Publish |
| Real PK receive/full check of all three actual inputs | error 0, zero faults each |
| GM two-tool subtract | error 157, Clip / BoundaryIntersections, curve 208, NotConverged; callback not reached |
| Failed GM operation rollback | all three input XT byte streams unchanged |
| Real PK two-tool subtract on identical serialized inputs/options | error 0, result 21650, one body |
| Real PK output full check | zero faults |
| GM output comparison / mass | not executed: no GM output |

Input SHA-256 hashes of this fixed baseline:

| Artifact | SHA-256 |
| --- | --- |
| target.x_t | `7de009139ea2dde494d83859adeb80141871dd61d7d8ca0e4354e0022f5a16dc` |
| tool-1.x_t | `ad885c46aeb0c22a384cc4f3c567cbdc0030ed55c30a57ee99af98d4d186aec2` |
| tool-2.x_t | `8056a1bf6862831b1d4de1da36af94f6bd5164fcaf019b5f5fc8d51644c40792` |

The initial all-19 attempt in this new worktree stopped at SessionStart 970
because its private schema directory was missing: **zero geometry cases
executed**. After configuring the authorized private runtime/schema and an
independent fixed PKToy checkout, the baseline completed **16 passed / 3 failed /
19 total**, process exit **1**, duration 6m41s. The three observed failures are
the original stationary endpoint lookup, row 289 outside/on result and the
two-tool Boolean returning 157 before the intended allocation injection.
Logs: `temp_docs/contract-sol/baseline-rolling.log` and
`temp_docs/contract-sol/baseline-rolling-runtime.{log,trx}`. No residual testhost
from this run remained. No NativeAOT build or full kernel suite was run.

## Repair hypothesis and boundary

The finite source root retains its original line station and error enclosure.
Clipping currently chooses the cut curve's rounded position unless the root is
an original seed vertex. The generated interior source-line root is therefore
associated with the correct event but loses its exact source constraint in the
event position. The first Boolean stationary-end certificate later requires
that constraint. A local repair must establish the same contact locus, finite
root and chart ownership before retaining that root's position; distance alone
does not identify an event. The independently observed second curve-214 failure
and saved classification row 289 remain separate open observations.

This entry commits a failing reproducible baseline, not a completed repair or
M0–M5 qualification. Later repair and independent review results must name their
own fixed source and evidence directories.

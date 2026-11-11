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

The initial all-19 attempt in this new worktree was **ENV_NOT_READY** at SessionStart 970
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

## Increment 1: retain a certified contact root's source constraint

`TryCertifiedContactRootPoint` only handles an already witnessed finite root of
an explicit source line. `RegularBoundarySurfaceRoots` has already checked the
finite provider's carrier/surface generations and exact input trim. Before
retaining its position, the new helper requires:

- The target is an analytic contact of the root's rolling surface; the section
  map identifies which original support defines it.
- That support has a current support-chart certificate to the root occurrence's
  support. The root point satisfies the support and source-line equations exactly
  in outward arithmetic.
- The target station is in the actual clipped interval. The existing
  `CertifiedRootMatchesCutAnchor` check projects that station into the finite
  source-root enclosure and checks the arithmetic-scale representation error.

These checks retain a source constraint of an identified event. They do not
identify events by distance or change public Circle/Ellipse/Taylor/Interval
semantics. Original seed vertices still take precedence. The existing root
error enclosure and native parameter association remain attached to the event.
The root trace now carries both the selected event position and the source root
position so the retained constraint is observable.

The new unit regression
`InteriorSourceRootPreservesTheStationaryPlaneConstraintAndItsFiniteBranch`
checks the existing stationary-end and displaced-plane assertions in full, both
before and after managed XT reload. Its local negatives reject an excluded cut
trim, an antipodal target station, a disjoint source-root interval, a missing
witness, the wrong support, and an incorrect position. It does not change the
old test's exact endpoint lookup or its unrelated frame-dependent meridian
assertion.

Actual candidate evidence in `temp_docs/contract-sol/`:

| Gate | Result |
| --- | --- |
| `contact-root-unit` | exit 0, 1/1; initial negatives and complete stationary/reload check |
| `contact-root-unit-final` | exit 0, 1/1; includes the additional wrong-support negative |
| `contact-root-focused` | exit 0, 151/151; contact, finite-plane, regular solid blend and analytic-contact regressions |
| `contact-root-primary` (`--reference-target=baseline --blend-only`) | exit 0; fresh candidate and frozen accepted 9b target zero full-check faults; strict compare passes; normalized15 `5.036881582931127e-15` |
| `contact-root-native-primary` | exit 0; frozen independent native target versus fresh candidate: both full checks zero faults, strict compare passes, normalized15 `2.1627000794607434e-10` |
| `contact-root-allocation-testhooks` | exit 0, 2932 emitted methods CLEAN |
| `contact-root-1` same-input two-tool operation | exit 134; Fix succeeds; GM Boolean now reaches Components before error 157; callback not reached; all three inputs roll back unchanged; same-input PK Boolean succeeds with one zero-fault body |

The fresh candidate target hash is
`399e580b74e050cc454aca4879e90d692a6639b159710e166a1cf2ea364f8dde`.
Both tool hashes remain the baseline hashes above. The independent native target
is the retained nominal default/radius-.5 construction from the prior oracle,
SHA-256 `bd75b7926956c1506e00c172816a0bfc1bb07db1660d74da65aaf1734f219ef1`.
Its bytes were copied read-only from
`../gm-topology-opus/temp_docs/short-edge-blend-oracle/a1-r0.5-t1E-07-reverseFalse-default-native.x_t`
to ignored `temp_docs/contract-sol/native-reference/target.x_t`. This is reuse of
a frozen independently constructed PK reference, not a new native Fix run.
`CompareTargets.cs` and its command/log are retained in the ignored evidence
directory. The tracked oracle can repeat that primary check with
`--reference-target=native-reference --blend-only` after preparing those bytes.

The local root repair advances past the original curve-208 Clip failure and the
previously observed curve-214 successor failure on this exact operation. The
remaining 157 is at `Components`, last reported face `36/-1`; the inherited sheet
fields are `RayIntersections`, curve 547 and status Success. These fields do not
yet identify whether Merge or projected-cell rearrangement returned the error.
That first return must be located before another behavioral repair. Neither this
result nor the passing blend checks qualify the unfinished Boolean or its
unreached injected-901 rollback path. Row 289 and the general spine/carrier
parameter-map issue remain separate; no full RollingNetwork rerun or full
lifecycle was used to qualify this increment.

An initial allocation-check invocation assumed a normal-build assembly path,
which is absent in this testhooks-only build and exited 134 before loading IL.
The explicit testhooks assembly path above ran the actual guard successfully.

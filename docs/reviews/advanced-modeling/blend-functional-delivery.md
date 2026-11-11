# Blend functional delivery

Development base: `089939392bf2d756e22bcdaa97910e12023584c5`, branch
`feat/pk-native-blend-functional`. This is ongoing functional work, not a whole
candidate acceptance. Boolean and Mac performance remain separate operations
and owners. Native environment preparation actually returned ENV_READY for
Linux x64 / PK 38.0 / PKToy `d820719` before execution. No C ABI or AOT work.

## Acceptance runner correction

`RegularSolidBlendOracle` and `SupportBlendChainOracle` now use the existing
shared strict comparator and mass estimate/modulus contract. Only chart-point
counts are diagnostic; sense, finite geometry, matching and structural gates
remain hard conditions. Different fails; Unresolved preserves external exit 2
after remaining operations. Each invocation requires a new `--output-name` and
refuses an existing evidence directory. Both print the loaded GM assembly hash.

The support-chain runner additionally verifies the actual GM-reloaded product
against PK before any subsequent Boolean. Its explicit `--lifecycle` copies
both products, deletes their source bodies, mirrors/scales the copies and
rechecks fresh GM XT with real PK. `--blend-only` selects this independent Blend
quality without making a later Boolean a precondition.

Actual narrow executions used `MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache`,
`-c Release -p:BuildInParallel=false -p:UseSharedCompilation=false
-p:GmInProcessLinkTasks=true`, followed by:

| Runner | Arguments after `--` | Actual result |
| --- | --- | --- |
| `scripts/RegularSolidBlendOracle.cs` | `plane-nurbs rounding-only --event-arrangement --output-name=blend-contract-regular-v1` | strict/full geometry passes; mass Unresolved; external exit 2 |
| `scripts/SupportBlendChainOracle.cs` | `asymmetric43-base --event-arrangement --separate-support --blend-only --lifecycle --output-name=blend-contract-chain-v1` | construction/reload/copy/source deletion/mirror-scale strict/full geometry passes; mass Unresolved; external exit 2 |

Logs and original exits: `temp_docs/blend-functional/{regular-contract-v1,chain-contract-v1}.{log,exit}`.
The exact runner diff is `temp_docs/blend-functional/contracts-v1.diff`, SHA256
`5a074cb6a17bd687037b3d515f4beeb7de690fbfb3954ea476a361cd888de2b6`. Actual XT remains private in each runner's named output
directory. These executions do not qualify all Blend families or Boolean.

## Active functional inventory

The unmodified-base local-operation Blend catalog (`--blend-arrangement`) ran
17 cases: 7 strict geometry passes, 10 hard failures, external exit 1. The five
axial cylinder fillet/chamfer cases pass. Planar all-edge fillets and planar
chamfers still have native curve/surface sense differences. Original evidence
is `temp_docs/blend-functional/local-base.{log,exit}` and is retained unchanged.

Native calls exposed different top-face LINE gauges between independently
created GM and PK blocks. This does not establish a primitive defect. The local
oracle now supplies the same physical all-edge mark order and offers explicitly
labeled GM/PK-authored identical-seed diagnostics, preserving both real products
and original input bytes. Those diagnostics do not replace independent creator
qualification or canonicalize either output. Native sense work remains active.

Previously accepted f9 short-edge matrix/lifecycle evidence is reused read-only
as scoped by the dispatch; no full 96/1885/347 rerun has been performed here.

# API child scopes: Freeform and LocalTopology

This checkpoint records lifecycle API children before execution and marks an unfinished child failed when an exception leaves its scope. It does not catch or suppress the original exception. Parent integration failure and completed construction evidence remain distinct, and first-failed selection prefers the actual failed child over its failed container. Unexpected recorded API returns create durable owned failure checks, so a later zero return cannot erase them. Missing sides remain null. In-process exit diagnostics are separate from nullable, externally observed process exits.

Freeform's two required XT lifecycle branches now have distinct receive, copy, source-delete, transform-create, transform, and cleanup children, with actual raw returns. Their parent catches identify the active lifecycle rather than failing an already completed construct. A sibling lifecycle which was never reached is explicitly not-run. Existing API names were corrected to the actual swept-body, insertion/degree-edit and B-surface reparameterization calls. Produced entity tags are entity references; actual saved GM/PK text XT bytes supply output SHA256 values. Produce's compound construction calls still lack a complete generic primary contract/raw call inventory and remain unverified, rather than claiming all A4 construction APIs qualified.

LocalTopology records actual `_2` calls and returns, options per side, existing pre-call XT input hashes and saved output hashes. Imported lifecycles have the same explicit API children and active failure ownership. Analytic hollow returns are also captured at the actual call. The separated-walls rejection keeps real GM/PK `13` (`PK_ERROR_not_unique`), declares the exact API/scenario/source SHA256, records both expected-return and empty-tracking checks, and preserves the existing unchanged-GM-XT assertion with before/after SHA256. Its comparison still uses the independent pre-call PK baseline because PK may leave intermediate state; no PK byte-rollback claim is invented. Its thirteen primary checks pass, but the operation remains **unverified / unqualified** as an expected rejection, not a successful body edit.

Expected-return interpretation is explicitly `operation-rules-v3`, confined to the declared hollow rejection and expected-missing-delete scopes. It adds no modeling primary pass or timing qualification. Existing v1/v2 return interpretation is unchanged; a caller cannot authorize a modeling ratio merely by declaring an arbitrary expected error. Public disk-validator negative controls cover this and reject applying v3's return policy under an older interpretation. All existing geometry assertions, masks, thresholds and original process error behavior remain unchanged.

Finite real checks (managed, real PK, no NativeAOT or full matrix):

| Entry | Actual selected case | Raw exit | Artifact |
| --- | --- | --- | --- |
| Freeform | parallel-nurbs-path-solid, including its two required XT lifecycles | 0 | `temp_docs/operation-leaf-r2/freeform-final` |
| LocalTopology | native-import-face-elimination | 0 | `temp_docs/operation-leaf-r2/local-import` |
| LocalTopology | sphere-outward-hollow-identities | 0 | `temp_docs/operation-leaf-r2/local-analytic` |
| LocalTopology | separated-walls-in | 0, actual API returns 13 / 13 | `temp_docs/operation-leaf-r2/local-final-rejection` |

Commands:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-review-5d/RunLeaf.cs -c Release -p:UseSharedCompilation=false -- FreeformModelingOracle.cs parallel-nurbs-path-solid ../temp_docs/operation-leaf-r2/freeform-final
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/LocalTopologyChangeOracle.cs -c Release -p:UseSharedCompilation=false -- --case=native-import-face-elimination --output=../temp_docs/operation-leaf-r2/local-import
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/LocalTopologyChangeOracle.cs -c Release -p:UseSharedCompilation=false -- --case=sphere-outward-hollow-identities --output=../temp_docs/operation-leaf-r2/local-analytic
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-review-5d/RunLeaf.cs -c Release -p:UseSharedCompilation=false -- LocalTopologyChangeOracle.cs separated-walls-in ../temp_docs/operation-leaf-r2/local-final-rejection
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/OperationAcceptanceSelfTest.cs -c Release -p:UseSharedCompilation=false
```

`RunLeaf.cs` is a temporary supervisor for these explicit checks: it retains the original stdout/stderr, raw exit and source hashes in `execution.json`, copies the untouched producer checkpoint to `ledger.in-process.json`, then finalizes only that newly created run's `ledger.json` with the actual exit and wall time. It returns the original child exit. Direct runs label exits caller-reported. Existing output directories are refused by the supervisor. Self-tests also inject an exception through the real child-scope entry point and check prior primary retention, exact child failure owner, missing-side null and in-process zero distinct from observed exit.

Logs are in `temp_docs/operation-review-5d/`: `freeform-final-supervisor.log`, `local-import-leaf.log`, `local-analytic-leaf.log`, `local-final-rejection-supervisor.log`, and `leaf-shared-self-test-final.log`. Historical files were not overwritten. Geo/Evaluation/ShortEdge changes and their acceptance remain separate checkpoints.

Known compatibility boundary: unqualified-reason strings are diagnostic snapshots of the producer's interpreter. The current validator still compares them exactly; an older synthetic unknown-version record can differ only in reason wording while remaining unverified and unqualified. This is a documented remaining compatibility issue, not a claim that every unknown-version history validates. Supported historical v1 records, including the fixed `57117536...` manifest, retain their original interpretation and bytes.

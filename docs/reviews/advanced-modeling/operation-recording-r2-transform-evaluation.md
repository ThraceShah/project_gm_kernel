# Transform and Evaluation failure attribution

Transform records actual GM/PK body-transform returns and exact saved output XT hashes, with entity/transform tags separated from hashes. A failure after partial topology checks now ends the active transform/lifecycle owner explicitly. Source deletion has its own API child with GM's actual return and PK null when not called. The source lifecycle is labeled an integration scope, not a claimed single receive/delete API. A downstream lifecycle which was never reached is not-run. Other geometry operations retain their existing unverified status and no general full-wire/geometry primary contract is invented.

Evaluation captures primitive constructor returns at the actual call, keeps active failure ownership, and separates memory-review final deletion from the completed constructor verification. Dependent checks are explicitly construction/evaluation/cleanup integration scopes, not mislabeled create APIs. Offset creation, both sampled numerical evaluations and individual deletions have separate owners. Failed cleanup cannot erase the earlier positive/negative sampled evaluation pass checks. Unreached dependent branches are not-run. Numerical-only checks do not qualify body construction.

Finally checkpoints no longer turn `Environment.ExitCode=0` into an observed process success during exception propagation: producer `legacyExitCode=null`, `legacyExitCodeSource=not-observed-in-process`, with diagnostic `inProcessExitCode=0` stored separately. The validation supervisor preserves that original checkpoint as `ledger.in-process.json`, retains original stderr/exit, and finalizes only the new run with its true process exit. It returns the original exit; the exception is neither swallowed nor converted.

Finite real checks:

| Entry | Selection | Actual raw process exit | Artifact |
| --- | --- | --- | --- |
| Transform | translation-body-block | 0 | `temp_docs/operation-leaf-r2/geometry-body` and `geometry-body-final` |
| Evaluation | offset only | **134**, preserved | `temp_docs/operation-leaf-r2/evaluation-offset-provenance` |

The offset run records the same cubic Bezier numeric definition being independently passed to PK/GM constructors, namespace-separated base/positive/negative entity tags, actual deletion order and exact source SHA256. Calls delete negative PK, negative GM, positive PK, positive GM, PK base, then GM base. The PK base delete actually returns **22** (`PK_ERROR_not_a_tag`), which the unchanged original oracle allows along with 0; GM base delete actually returns **5002**, which the unchanged original assertion rejects. Both prior sampled evaluation checks remain pass. The failure owner and first-failed ID are `op-dep-offset/delete-base-gm`, with PK null on that GM-only child. PK's actual 22 is stored in its own child, not replaced with an assumed zero.

This records a cleanup-contract difference and the existing oracle's assertions. It does **not** prove a GM kernel defect, establish complete ownership equivalence, or authorize ignoring either result; that modeling/ownership investigation is outside this recording task. No Native/host/private-runtime changes or tolerance/expected-result changes were made.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-review-5d/RunLeaf.cs -c Release -p:UseSharedCompilation=false -- GeometryTransformOracle.cs translation-body-block ../temp_docs/operation-leaf-r2/geometry-body
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/GeometryTransformOracle.cs -c Release -p:UseSharedCompilation=false -- --case=translation-body-block --output=../temp_docs/operation-leaf-r2/geometry-body-final
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-review-5d/RunLeaf.cs -c Release -p:UseSharedCompilation=false -- ParasolidEvaluationOracle.cs offset ../temp_docs/operation-leaf-r2/evaluation-offset-provenance
```

Logs are `temp_docs/operation-review-5d/{geometry-body-supervisor,geometry-body-final,evaluation-offset-provenance-supervisor}.log`; complete raw outputs and source-hash/exit evidence are in each new artifact directory. An earlier offset checkpoint located the failure only at its integration parent; it remains unchanged in `evaluation-offset`. Subsequent runs added the exact base-delete child and then source/dependency evidence, preserving the original 134 in every run. No full 19/96/corpus/AOT run was performed.

The earlier Freeform metadata's synthetic `PK_BSURF_insert_knots` label was corrected to the actual source call names `PK_BSURF_add_u_knot`, `PK_BSURF_add_v_knot` and `PK_BSURF_raise_degree`; the edited-surface integration remains unverified. This is a source-name correction, not a claim that the edited-surface cases were executed in this finite check.

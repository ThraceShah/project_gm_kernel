# Fixed 12e review follow-up

Independent review of `12e0043a87f612f54b7e7d5d9541e1371ea61adc` found that not-run took precedence over actual API/primary failure, and input/output hash getters still exposed live nested objects. This follow-up changes only that shared state/snapshot contract and its negative tests; pending Capture/Refresh/leaf work is excluded.

Known failures now precede not-run. The recording entry point converts a not-run request after recorded execution into an explicit owned failure; the validator independently rejects serialized not-run claims that contain actual execution evidence, including records which already claim failure but retain a contradictory not-run terminal. Hash recording methods retain private deep copies, and getters return detached copies, matching options/provenance behavior.

Original independent JSON inputs were copied byte-for-byte into `temp_docs/operation-shared-12e-review/original-inputs/`:

| Input | SHA256 | Public disk validator errors |
| --- | --- | --- |
| known-error-not-run.json | `faaf4fddfadd8c79b352afb3738778cf3ee4263d72733ef0c75a6066d09fed59` | 4 |
| not-run-failed-primary.json | `6d8604796a78063e7e656492354d0f4fa8fee9f97add2f1d063deb5415b38c07` | 3 |

Executed managed commands, all exit 0:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-shared-12e-review/ReplayInputs.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/OperationAcceptanceSelfTest.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- --self-test
```

Logs are `temp_docs/operation-shared-12e-review/{replay-inputs,self-test,validator-self-test}.log`. The self-test additionally mutates nested input and output getter results after recording through the actual APIs, verifies unchanged stored values, and exercises both known-return and failed-primary not-run paths through EndOperation and serialization. Original JSON files were not rewritten. The original independent program was preserved as evidence but not claimed fully rerun: its output setup writes through the now-detached getter and then assumes that write persisted, so its later dereference is incompatible with the corrected getter contract. No PK run, NativeAOT build, historical artifact change or acceptance claim is part of this follow-up.

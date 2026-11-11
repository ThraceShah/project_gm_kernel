# Versioned operation interpretation

Independent fixed-bd7 review correctly identified one historical operation being reinterpreted, not three bad cases. The exact old manifest SHA256 is `57117536c174211f85be9a2c20d2560c6925b768491ff50d831340f7dd20dca0`. Its Boolean-v2 operation was recorded unverified before that API was supported; adding support without an interpretation version caused three discrepancies (status, primary status, reasons) for that one record. Copying a newer run into a separate validation dataset only isolated the symptom and did not fix compatibility.

New records explicitly carry `interpretationVersion=operation-rules-v2`; new ledgers/captures/manifests use `operationSchemaVersion=operation-layered-v2`. Missing interpretation versions resolve to the original `operation-rules-v1` supported API set, which does not include `PK_BODY_boolean_2`. Boolean-v2 support requires explicit v2 recording. Unknown future interpretation versions remain unverified and cannot qualify. The underlying 24 body-result primary checks retain their existing contract name because those checks did not change. Original oracle `checkVersion` remains unknown/null; the interpretation version is not an oracle revalidation signature. Actual check-definition/source hashes continue to be recorded for new runs.

The original dataset was copied unchanged to `temp_docs/operation-version-compatibility/old-only-data`. The real disk-validation entry point was run before and after using the same manifest bytes. The before driver compiles exact ledger/rules/validator sources extracted from fixed `732dad8`, with no source modifications; exit 1, three errors for that same unverified operation. The current public validator exits 0, errors empty, preserving the original unverified status and null ratio. No records were deleted, filtered, rewritten or upgraded to pass.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-version-compatibility/before/ValidateOld.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- ../temp_docs/operation-version-compatibility/old-only-data
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/OperationAcceptanceSelfTest.cs -c Release -p:UseSharedCompilation=false
```

Logs are `temp_docs/operation-version-compatibility/{old-before,old-after,self-test}.log`. Self-tests also cover explicitly versioned new Boolean-v2 support, rejection of an unknown interpretation, and rejection of a claimed Boolean-v2 pass under legacy interpretation. This narrow change does not accept the pending leaf scopes, fix ambient Context ownership, or grant timing qualification to integration inputs. Intermediate bd7/1b/732 artifacts which claimed a Boolean-v2 pass without an explicit rules version remain unversioned candidates, not evidence to retroactively sign; their raw bytes remain unchanged.

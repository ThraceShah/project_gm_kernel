# Fixed 1b original-counterexample replay

This narrow review preserves the original d9 report, `MissingOwnedProbe.cs` and both original receipts. It requires their retained evidence in this review workspace; it fails their fixed hashes rather than regenerating or overwriting them.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/owned-ui-intent-1b-review/ReplayOriginal.cs
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file scripts/VerifyReviewedWebProgressUiHook.cs -- --transition-only
```

The first command copies a fresh private input, changes only its Configure source to fixed 1b, updates that new manifest's pin, and executes the original d9 probe **verbatim**. The original diagnostic asserts the old bug, so its outer exit134 is expected after the fix: inspect the inner CLI134 with `UI_OWNED_TRANSITION_HOOK_MISSING`, zero systemctl calls, absent hook/lease/backup and preserved timer state. The wrapper then records complete before/after file SHA and directory vectors for six independent missing-hook/partial-intent cases. Its own final exit is0.

The second is the deliberately limited 31-check transition regression, including genuine first install/restore and full owned prepare/verified-backup/install/rollback/restore. It does not execute the 173-check suite. Its publication and service calls use private loopback and BCL stubs; no real unit, portal or live operation occurs.

The accepted range is fixed 1b's intent guard together with the previously verified finite d9 behavior. This does not accept a new UI candidate or authorize deployment, main integration or hosted/proxy migration. The d9 blocked result remains historical fact.

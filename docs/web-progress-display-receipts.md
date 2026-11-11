# Optional display snapshot receipts

Enable only for new render-only collection runs:

```sh
GM_WEB_DISPLAY_RECEIPTS=1 MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/RefreshWebProgress.cs -c Release -p:UseSharedCompilation=false -- --case analytic-boolean.equal-overlap-subtract --samples 1 --seconds 140 --case-seconds 130 --run-id unique-display-run --display-receipts --output ../temp_docs/display-receipts/data
```

`--display-receipts` sets the environment option in child processes; the explicit environment prefix above is equivalent. Without it, Capture performs no additional XT transmit or receipt output. This is local evidence collection, not publication; the existing static publisher still rejects `.x_t` files. No acceptance checks, model returns, API timing boundary or default geometry execution were changed.

Each mesh JSON has `displayReceipt`, relative to that mesh file, e.g. `../display/snapshot-0.receipt.json`. Receipts and their actual XT files live in the sample's `display/` directory. Example shape:

```json
{
  "schemaVersion": 1,
  "status": "available",
  "sourceRun": "unique-display-run",
  "sourceSnapshot": "snapshot-0",
  "kernel": "gm",
  "caseId": "analytic-boolean.equal-overlap-subtract",
  "sampleId": "sample-0",
  "stepId": "operation-1",
  "stage": "before",
  "parameterHash": null,
  "captureSource": "scripts/WebProgressDisplayReceipts.cs",
  "captureCodeSha256": "actual SHA256 of that source file",
  "capturedAt": "actual UTC timestamp",
  "sourceEntityTag": 7,
  "meshPath": "../meshes/gm-0-before.json",
  "meshSha256": "actual SHA256 of exact saved mesh bytes",
  "xtPath": "snapshot-0.x_t",
  "xtSha256": "actual SHA256 of exact saved XT bytes",
  "bindingStatus": "identifier-verified",
  "surfaceTrimVerification": "required-by-consumer",
  "faceBindings": [
    {"meshFaceId":"gm:face:7","entityTag":7,"xtFaceIdentifier":7,"bindingKind":"persistent-identifier"}
  ],
  "reason": null
}
```

Both `meshPath` and `xtPath` resolve relative to the receipt directory. Tags are only entity references, never hashes. A missing parameter snapshot remains null. Snapshot IDs are scoped by run/case/sample; no cross-run or cross-kernel identity is implied. Capture checks face identifier uniqueness and resolves every binding against the exact DisplayBody PK body. The GM XT is the immutable `DisplayBody.XtText` used to create that display snapshot; the PK XT is an actual text transmit of the same real PK body, with no reconstructed fallback. Missing source/run identity, original GM XT or reliable identifier bindings produce `status=unavailable` and a reason, with no substitute model. Consumers must reject unavailable receipts and verify hashes, persisted identifier mapping, and mesh vertex agreement with surfaces/trims before generating display geometry. Identifier verification alone is not a surface/trim equivalence claim.

When an existing saved before-mesh receives later role/deletion annotations, its receipt mesh hash is updated to the exact rewritten mesh bytes; the geometry snapshot, XT and bindings remain unchanged. The optional evidence never qualifies API performance or proves same input. Original runs are not modified or backfilled.

Initial actual run: `temp_docs/operation-receipt-r2/data/runs/r2-display-receipt-single`, exactly one selected oracle, raw exit 0, Refresh exit 0 and validation errors empty. It produced six available receipts (GM 3 / PK 3). `temp_docs/operation-review-5d/ReceiptAudit.cs` verified every exact mesh/XT SHA256, reloaded all six XT files into real PK, matched all face identifiers and checked the mesh identifiers; exit 0, log `temp_docs/operation-review-5d/receipt-audit.log`. The normal preceding run produced zero receipts. The initial run preceded the final relative mesh-to-receipt link adjustment and addition of the helper to Refresh's source hash list; its own `captureCodeSha256` records the actual initial helper source, and its bytes remain unchanged. A new fixed-commit collection is required for the final interface, rather than grafting this receipt onto historical meshes.

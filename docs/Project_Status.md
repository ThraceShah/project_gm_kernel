# Project Status

Generated: 2026-05-15

## Current State

The project is an early kernel runtime prototype, not a complete Parasolid replacement yet.

Verified capabilities:

- `.NET 10` NativeAOT shared-library project builds and publishes with an explicit RID.
- Session start/stop works.
- Point and transform creation work.
- Minimal body topology creation works.
- Solid block creation works.
- Basic topology queries work for body, face, loop, edge, fin, and vertex links.
- Entity delete works for the implemented entity pools.
- Single active mark rollback works for the covered delete and allocation cases.
- Return arena query results stay valid across repeated queries until session reset or rollback.

Current implementation surface:

- Manual implemented exports: 25.
- Generated export stubs: 1006.
- Generated stubs currently return `PK_ERROR_not_implemented`.

## Verification

Preferred full check:

```sh
dotnet run scripts/VerifyKernel.cs
```

Manual checks:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests && pkill -f testhost.dll 2>/dev/null; true
MSBUILDDISABLENODEREUSE=1 dotnet publish src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj -c Release -r osx-arm64
```

Use the host RID for the publish command. On this machine the verified RID is `osx-arm64`.

## Fixed Runtime Risks

- Tag validation now checks both handle-table state and target pool slot alive/generation state.
- Deleted tags stay invalid after their old pool slot is reused.
- Deletes inside an active mark retire slots instead of putting them back on the free list.
- `MarkGoto` restores retired slots; `MarkDelete` recycles retired slots.
- Mark pool-count snapshots use inline fixed storage instead of allocating a managed array on first mark.
- Return arena block metadata uses a fixed table instead of resizing managed arrays during repeated queries.

## Remaining Gaps

- API coverage is intentionally small. Most generated `PK_*` exports are ABI stubs only.
- The dispatch layer serializes with a lock and records command descriptors, but it is not yet a real session command queue with concurrent/local scheduling.
- Only one active mark is supported.
- Rollback is still a minimal slot-state rollback, not a full transaction delta system for complex topology edits.
- No OCC algorithm translation has started.
- No cylinder primitive, C host ABI smoke test, topology dump, visual debug bridge, or allocation profiler baseline has been added yet.
- NativeAOT publish requires an explicit RID unless the project later defines one.

## Recommended Next Work

1. Add a small external ABI smoke test that loads the published native library and calls implemented `PK_*` exports.
2. Replace `Func<int>` dispatch with a zero-allocation command execution shape.
3. Add allocation measurement around implemented API paths.
4. Promote the dispatch layer from lock-serialized execution to an explicit bounded session command queue.
5. Implement the next narrow API cluster instead of broadening the generated stub surface.

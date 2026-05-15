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
- `PK_BODY_ask_topology` exposes the implemented topology graph for body debugging.
- Entity delete works for the implemented entity pools.
- Single active mark rollback works for the covered delete and allocation cases.
- Return arena query results stay valid across repeated queries until session reset or rollback.
- External ABI smoke coverage loads the published NativeAOT library and calls implemented exports.
- Allocation baseline reporting covers the implemented hot paths.

Current implementation surface:

- Manual implemented exports: 26.
- Generated export stubs: 1005.
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
- Manual exports use struct commands instead of per-call `Func<int>` closures.
- Dispatch now has an explicit bounded enqueue/run/complete queue shape while preserving serial execution.
- Solid block shell linkage now terminates its sibling chain, so topology traversal does not walk stale slots.

## Remaining Gaps

- API coverage is intentionally small. Most generated `PK_*` exports are ABI stubs only.
- The dispatch layer still serializes execution with a lock; concurrent/local scheduling is not implemented.
- Only one active mark is supported.
- Rollback is still a minimal slot-state rollback, not a full transaction delta system for complex topology edits.
- No OCC algorithm translation has started.
- No cylinder primitive, visual debug bridge, or enforcing allocation profiler threshold has been added yet.
- NativeAOT publish requires an explicit RID unless the project later defines one.

## Recommended Next Work

1. Add an enforcing allocation threshold once the baseline is stable across machines.
2. Promote the dispatch queue from serial execution to real concurrent/local scheduling.
3. Implement cylinder primitive as the next narrow geometry cluster.
4. Add a visual debug bridge or topology text dump command for developer workflows.
5. Continue implementing narrow API clusters instead of broadening the generated stub surface.

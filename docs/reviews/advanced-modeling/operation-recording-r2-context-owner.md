# Explicit capture identity and Context lifetime

Fixed-bd7 review established a source path where an imported-product Lifecycle leaked its ambient Context, the later manual fillet owner was allocated using that stale value, and Fillet then reset Context before Before paired with the original native owner. Plain single/multi runs did not cover this path. This is recorded as a source-proven path, not as a newly executed PK lifecycle reproduction.

BeginObservedOperation now receives the intended API context explicitly and never derives pending identity from ambient Context. ShortEdge requests the exact empty context used by its real fillet Before call. Lifecycle and Fillet use a disposable Context scope which restores the previous value on normal, nested and exception exits. Context, API name and per-side occurrence still identify independent calls; duplicate explicit IDs remain contract errors.

The controlled probe compiles the actual Capture entry point and shared ledger, seeds only the source-proven post-native dictionary state, sets a stale imported Context, then calls the real BeginObservedOperation. It confirms the existing native object is reused, tests nested and exception restoration, and confirms a different explicit context creates a distinct object. No Before/mesh/PK initializer/model API or complete lifecycle was executed. Its AssemblyName uses the repository's existing friend assembly so the unchanged Capture dependencies compile; no Native source or visibility changes were made.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/operation-review-5d/ContextOwnerProbe.cs -c Release -p:UseSharedCompilation=false
```

Exit 0, log `temp_docs/operation-review-5d/context-owner-probe.log`, three controlled assertions. The first attempt lacked the required friend AssemblyName and failed compilation; no runtime owner claim is based on that attempt. This narrow fix and the prior explicit-version fix are ready for independent incremental review; neither constitutes full A3–A7 acceptance or dynamic PK lifecycle coverage.

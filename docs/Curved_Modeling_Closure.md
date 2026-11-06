# Regular curved modeling closure

## Scope and sequence

The work follows three ordered milestones:

1. Public regular surface intersection: correct interest-box semantics,
   complete branches across chart boundaries, and explicit failure when a
   procedural artificial limit prevents a completeness claim.
2. NURBS/Blend sheet modeling: exact intersection, face partition, trim and
   a second cut, preserving shared topology, ownership and atomic rollback.
3. Regular transverse curved-solid Boolean construction, building on the
   verified face-partition and classification paths.

Initial inputs are finite regular manifold geometry. Tangent branches,
coincident areas and singular crossings require separate algorithms and must
fail atomically when unresolved. Variable-radius blends remain deferred.

## Acceptance

- Construct operands independently through the same public APIs and parameters
  in this kernel and real Parasolid.
- Check complete branch counts, endpoints, closedness, support membership and
  tangents; interest regions must not silently become trimming boundaries.
- Export every modeling result using our text `PK_PART_transmit_b`; load it
  in real Parasolid, require full `PK_BODY_check` without faults and structural
  `PK_DEBUG_BODY_compare` agreement with independently constructed references.
- Exercise repeated operations, trimming, source deletion, geometry ownership,
  failed-operation rollback and return-buffer cleanup.
- Run relevant existing regressions, allocation checks, NativeAOT publication
  and native ABI smoke checks before committing and pushing each milestone.
- Record public intersection and distance timings separately from evaluation
  throughput. Optimization must preserve certified exclusion and completeness.

## Progress

Milestones 1/2 have their finite regular implementation and native modeling
closure, with the verification evidence below.

### Public intersection implementation

Nondegenerate UV and spatial boxes are interest regions. The implementation
searches the complete supported surface domain and may return branches outside
the requested region. It does not trim the curves at interest-box edges.
Nonfinite and inverted UV boxes are rejected; zero-area UV regions are
explicitly unsupported because native Parasolid can change the output to point
intersections for those requests. The native probe is recorded in
`temp_docs/interest-contract-probe.log`.

Finite modeling domains remain private to the sheet pipeline. An artificial
procedural spine extent is not promoted to a geometric endpoint by a public
interest box. A certified branch reaching an unresolved artificial extent
fails atomically; arbitrary continuation beyond such extents is still open.

Analytic opponents of Blend charts are built over the corresponding certified
local spatial enclosure. The procedural regular-graph admission retains its
contracting Jacobian and complete boundary-root isolation; it does not require
unrelated affine opponent coordinates to reach a fixed subdivision fraction.
The discovery-cell budget is shared across a Blend operation's patch pairs;
exhaustion fails atomically instead of resetting the budget for every pair.

### Blend sheet implementation

The existing plane-imprint, seed-trim and plane-section entry points now accept
regular finite Blend sheets. Exact isoparametric circles and SPCURVEs remain
exact. Procedural bounds also support isoparametric rays and auxiliary ruled
surfaces, allowing certified UV winding without a sampled polygon boundary.
Winding can use either parameter direction. Local jets only select a promising
direction; complete interval root isolation decides crossings. A failed or
coincident ray is retried in the other direction, including queries on the
extension of an existing trimming edge outside its finite interval.
Existing Blend ICURVEs are split against a second plane using their actual
support surfaces and certified root isolation.

Multiple faces on the same support reuse the intersection geometry within a
single partition command. Unused cuts are released after the last consumer.
Sheet construction binds parameter-curve edges to its cloned support, so the
source Blend can be deleted independently.

The independent curved-sheet oracle adds nine open NURBS Blend scenarios:
imprint, keep/remove trim, front/back/both section, two cuts, trim followed by a
second cut, and repetition of the second cut. Each kernel constructs its own
NURBS, wall and Blend with identical construction parameters. Spine-space
correspondence maps the rectangular sheet domains between their independent
chart parameterizations; V ranges are identical. No PK result geometry is used
to generate our Blend or its cuts.

### Verification evidence

- Full kernel tests: **1362/1362**, no skipped cases; XT tests: **12/12**.
- `VerifyAdvancedModeling`: all 11 modeling oracle steps, the complete public
  modeling regression and native advanced smoke passed.
- The full curved-sheet oracle passed **79/79** cases, including all nine
  independent NURBS Blend sheet cases. The latest bidirectional winding path
  additionally passed the real repeated-second-cut scenario independently.
- NativeAOT publication, native ABI, native display and all 14 public modeling
  verification steps passed.
- Initial depth-three construction/inversion/XT/similarity, open/signed/swapped
  support coverage, negative sense, dual Blend supports and scaled independent
  XT oracles passed.
- All **42** paired depth-three evaluation performance gates passed the 70%
  target, with zero managed allocation. The logs are in
  `temp_docs/curved-closure-nested-benchmark.log`.
- Hot Blend partition/deletion allocates zero managed bytes after warmup.
  Allocation-failure injection at multiple construction depths restores the
  first-cut B-rep, geometry pools and return buffers; a subsequent retry works.
- Public Blend/plane interest regions, Blend/NURBS and closed Blend/Blend
  intersection outputs passed complete counts, endpoint/space/tangent checks,
  real receive, full body checking and structural comparison. Chart-point
  counts remain parameterization diagnostics, not model vertices.

Evidence: `temp_docs/curved-closure-advanced-final.log`,
`temp_docs/public-modeling-verification/`,
`temp_docs/curved-sheet-partition-oracle/`,
`temp_docs/curved-closure-repeat-native.log`,
`temp_docs/curved-closure-interest-final.log`,
`temp_docs/curved-closure-intersections-final.log`, and
`temp_docs/curved-closure-allocation-final.log`.

Public SSI and global distances remain substantially slower than real PK.
Their diagnostic timings are separate from the paired nested-evaluation
benchmark and do not inherit its 70% throughput target.

Reproduce the modeling closure with:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/VerifyAdvancedModeling.cs
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/BlendOperationOracle.cs -- --intersections-only
```

Stage 3 starts after milestones 1/2 are committed and pushed. General curved
cutters, general curved-solid classification and Boolean assembly are not
implemented by these plane-cut sheet increments.

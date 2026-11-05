# General regular surface pipeline validation — 2026-11-05

Scope: intersection foundations and complete regular branches through sheet
partition, trimming, classification and result B-rep. Environment: Linux x64,
.NET SDK 10.0.302, real Parasolid through the shared ParasolidScriptHost.

## Implemented behavior

- Outward-rounded homogeneous Bezier extraction, restriction and position/jet
  bounds; positive-weight scaling; bounded global closest-point search and
  public point-to-parameter APIs.
- Regular NURBS/NURBS and analytic/NURBS intersection fallback, complete-domain
  discovery of internal closed loops, adjacent-span joining and paired UV.
  Plane, cylinder, cone, sphere and ring-torus rational charts participate.
- Regular curve/curve and surface/curve boundary roots, support-defined
  ICURVEs, independent geometry ownership and membership-based seed selection.
- B-surface sheet arrangement using actual boundary loops, exact UV ray
  winding, hole nesting, plane-side classification and shared B-rep topology.
  Closed cuts, smaller source domains, repeated planes and annular trimmed
  inputs are covered.

The previous rectangular/open-branch partition implementation was replaced;
its exact intersection and ICURVE chart helpers remain in use.

## Executed checks

| Check | Result |
| --- | --- |
| Final full KernelTests run | 1,250 passed, 0 failed, 0 skipped |
| XT tests | 12 passed, 0 failed |
| Independent intersection contract oracle | 59 cases passed |
| Independent curved-sheet partition oracle | 70 cases passed |
| VerifyAdvancedModeling | All 14 top-level steps passed |
| Nested VerifyPublicModeling | All 14 steps passed |
| Function catalog | 163 implemented dispatch descriptors verified |
| Release allocation IL guard | 1,935 methods checked, CLEAN |
| Managed hot-path measurement | General intersection + deletion: 0 allocated bytes after warmup |
| Linux x64 NativeAOT / native ABI / native display | Passed |
| Native advanced modeling C-export smoke | Passed |
| git diff --check | Passed |

The combined suite ran 1,249 kernel tests before the last explicit unsupported
spindle-torus rejection regression was added. That regression and the final
source then passed all 1,250 kernel tests. NativeAOT publication included the
final production source. Testhost cleanup followed the project process rules.

Reproduction:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyAdvancedModeling.cs
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests/KernelTests.csproj -m:1 -p:UseSharedCompilation=false
```

Primary logs:

- `temp_docs/advanced-modeling-verification/summary.txt`
- `temp_docs/public-modeling-verification/summary.txt`
- `temp_docs/general-final-kernel-tests.log`
- `temp_docs/advanced-modeling-verification/IntersectionOperationOracle.log`
- `temp_docs/advanced-modeling-verification/CurvedSheetPartitionOracle.log`
- `temp_docs/public-modeling-verification/native-publish.log`

## Independent geometry verification

Each positive oracle constructs operands and performs the operation separately
in both kernels. Our result is transmitted as text XT, received by real
Parasolid, checked with full PK_BODY_check and compared with PK_DEBUG_BODY_compare.
Topology counts, region/shell semantics, geometry classes, senses, tolerances
and missing entities remain hard gates. Native ICURVE chart counts and internal
face parameterization differences are diagnostic, rather than geometry
substitutes. Wire samples also verify native inverse parameters and residuals.

New manufactured cases include a paraboloid with a wholly internal closed
intersection; positive rational variants; two nonplanar NURBS supports with
different UV domains; a loop crossing a knot boundary; sphere latitude and
equator seams; cylinder, cone and torus crossings; three B-curve crossings;
global interior closest points; closed-loop keep/remove trimming; a second
plane through the closed edge; repeating that plane; and front/back/both
sections of an already trimmed annulus. Injected allocation failures verify
geometry/topology/return-buffer rollback. Original operand deletion verifies
independent result support ownership.

## Defects exposed and fixed

- ICURVE endpoint Newton corrections could leave the natural domain by a
  rounding-sized amount. Boundary clamping now retains the full residual and
  UV correction acceptance gates.
- A near-isoparametric graph coordinate could vary by less than the global UV
  threshold while tracing a long spatial arc. Such arcs are no longer dropped;
  open components must also terminate on a real natural boundary.
- Analytic seam bounds now preserve exact zero/one endpoints, enabling regular
  equator and torus-seam branches without weakening interval exclusion.
- Closed ICURVE end limits share the same XT node, and sheet dummy fins have
  null loop links. Received trimmed sheets now pass native body validation.
- General chart error bounds cover the admitted cells, allowing real Parasolid
  inverse evaluation between anchors. Evaluation scratch is reclaimed per
  call, including nested trimming operations.

## Remaining boundaries

The general fallback is for regular transverse loci with positive weights and
degrees at most 16, within explicit capacities and numerical budgets. Singular
contacts, tangent branches, coincident regions, C0 crease topology, complete
pole/periodic face-domain decomposition, mixed analytic/B-surface sheet
arrangements, curved-solid Boolean assembly and blend construction remain
outside this stage. Unsupported spindle tori and unresolved cells return
errors, not successful empty results.

Extreme-weight native failures remain separate BOUNDARY diagnostics and do not
increase compatibility counts. Existing SDK NU1903 and test-analyzer warnings
remain visible in logs; they were not changed by this task. No AOT analyzer or
allocation guard failure was suppressed.

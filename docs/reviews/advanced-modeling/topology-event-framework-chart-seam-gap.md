# Frozen finite-root projection at an internal CHART seam

The source-root repair `3344c539` has independent narrow acceptance in review
`285e93ca174dd307801ec16dce0f8cad48605e96`. The subsequent two-tool Boolean still
fails. The operation provenance and first missing finite split are recorded in
`docs/reviews/advanced-modeling/topology-event-framework-consumer-contract.md`.
This note freezes the next contract question; it does not claim a second
behavioral repair.

## Actual source and target evidence

The exact source P is supplied by finite `PlanePairBlendLineCertificate` 5 on
line 517 generation 2, occurrence 34/62. It has native parameter enclosure
`[.15550974970337608,.1555097497034054]`, rolling station enclosure
`[3.1415926535897927,3.1415926535897936]`, and spatial error upper bound
`2.7006503118386178e-14`. The source line is certified on the current tool-plane
pair; the rolling-root solver proves the finite branch. Its representative is
`(-1.5,-2.0000380014441093,4.999999998555889)`.

The actual seven-coordinate witness passed into `LocateCertifiedChart`, together
with its actual compiled target program, is saved by the synchronous
`TraceCutProjection` hook. The copied data consists of this project's own
surface/spine/chart instructions and scalar arrays, with element sizes checked
by the replay reader. It contains no PK binary or handwritten native ABI. The
capture is complete before the original Boolean continues; the replay calls
the unchanged `TestLocateSharedChart` entry on the frozen program.

The target provider's finite domain is `[0,2.653063227875798]`. It has 40 charts,
Help/Help limits, and identical first/last stored positions. Its internal knot
is **K = `2.5498703951611956`**, strictly inside the finite domain. The actual
outward projections of the entire source-position enclosure are:

| Native cell | Declared native range | Source projection in that cell's gauge |
| --- | --- | --- |
| 37 | `[2.4549933296261197,K]` | `[2.5498703951613213,2.5498703951613275]` |
| 38 | `[K,2.653063227875798]` | `[2.549870395161052,2.549870395161059]` |

The left projection is strictly above its cell end; the right projection is
strictly below its cell start. Both are excluded before target contraction.
The other projected candidate cells in the complete scan have certified spatial
bounds disjoint from the source-position enclosure. The unchanged locator
therefore returns **67 / PK_ERROR_not_on_curve**, in approximately **51 ms** in
the standalone replay. This is not an exclusion by the outer finite trim.

Stored anchor 38 is
`(-1.5,-2.000038001446803,4.99999999855589)`: its Y coordinate differs from the
isolated source root by approximately `2.694e-12`. Thus the two affine CHART
gauges leave a gap in the current strict per-cell inverse coverage at P.
This is a finding about the exercised program and consumer contract. A target
native parameter equal to K cannot silently be presented as an exact inverse
of P under those original cell equations.

Static follow-up: `CreateGeneralIcurve` currently calls
`RefinePlaneRollingChart` only for open curves. The captured curve is closed.
That observation identifies a producer path to examine, not proof that simply
enabling refinement repairs continuity or the full Boolean.

## Reproduction and controls

Capture the actual same-input operation once, with a new output name:

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/RollingBooleanConsumerOracle.cs -c Release \
  -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false \
  -p:GmInProcessLinkTasks=true -- frozen-projection --inject --trace --capture-projection
```

The completed capture process exits **134** on the still-failing GM Boolean.
GM remains at `ProductOpenEdge`, while the same serialized inputs/options in
genuine PK return success / one body / zero full-check faults. All three GM
inputs still roll back unchanged; the injected projected replacement is not
reached. No repeated full regression or lifecycle is needed for the replay.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/RollingProjectionReplay.cs -c Release \
  -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false \
  -p:GmInProcessLinkTasks=true -- \
  temp_docs/contract-sol/frozen-projection/projection-801-517-8.bin --details
```

This exact source-P replay exits **1** and retains the actual locator error 67.
The capture filenames use that run's allocated carrier tags; the source
certificate/provenance trace identifies the case if allocation tags change.
The binary reader is an internal same-layout diagnostic, not a portable model
format. The replay performs no genuine-PK API or modeling operation.

| Frozen input / control | Actual result | Process exit |
| --- | --- | --- |
| P, `projection-801-517-8.bin` | error 67, no station | 1 |
| S, `projection-801-208-3.bin` | error 0, `[2.5498703126402273,2.549870312640359]` | 0 |
| S plus `--wrong-phase` | error 157, inconsistent rolling witness rejected | 0, expected rejection |
| S plus `--exclude-root` | error 67, excluded finite target trim rejected | 0, expected rejection |

P snapshot SHA-256:
`f6dacaa07c99bc467c7915718d8e4f9ff20280362e001377f3c466963c768d79`.
S snapshot SHA-256:
`eff6fabced8f5c63088226b892b1c9d9e1537ee05b03c8b085e6bc2986f28b09`.
Logs are `temp_docs/contract-sol/projection-case-red.log`,
`projection-control-positive-fixed.log`, `projection-control-phase.log` and
`projection-control-trim.log` in that directory. An initial tracked-replay
invocation omitted the global `KernelTestHooks` build property and failed to
compile; it executed no modeling or projection and is retained separately as
`projection-control-positive.log`. All successful commands above pass that
property explicitly.

## Contract requiring independent review before a behavioral patch

There are two materially different repair scopes:

1. Require the newly constructed closed SSI carrier to supply continuous,
   complete finite CHART coverage before it is consumed, with a new whole-domain
   certificate and controls for its cyclic seam.
2. Admit a specifically typed, bounded **seam representative** correspondence
   under the existing geometric precision. Its identity would have to come
   from the shared support equations, full source-root state, finite branch,
   occurrence and generation. Both adjacent terminal root tubes must be
   certified and their spatial link bounded over the whole relevant interval.
   The result must retain that nonzero error and source constraint, and must
   not claim to be an exact native preimage. Wrong phase, antipodal branch,
   wrong support, wrong size and excluded finite trims must still reject.

No implementation of either scope is included here. A proximity join, wider
global interval/tolerance, scalar quadrant snap, arbitrary K insertion, missing
edge exemption or removal of a positive finite interval would not establish
either contract. The first source-root repair remains accepted within its
independent scope, and the Boolean remains explicitly incomplete.

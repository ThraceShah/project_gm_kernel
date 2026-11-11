# Blend functional delivery

Development base: `089939392bf2d756e22bcdaa97910e12023584c5`, branch
`feat/pk-native-blend-functional`. This is ongoing functional work, not a whole
candidate acceptance. Boolean and Mac performance remain separate operations
and owners. Native environment preparation actually returned ENV_READY for
Linux x64 / PK 38.0 / PKToy `d820719` before execution. No C ABI or AOT work.

## Acceptance runner correction

`RegularSolidBlendOracle` and `SupportBlendChainOracle` now use the existing
shared strict comparator and mass estimate/modulus contract. Chart-point
counts remain diagnostic; raw curve-sense differences additionally require the
complete finite analytic proof described below. Other sense, finite geometry,
matching and structural gates remain hard conditions. Different fails; Unresolved preserves external exit 2
after remaining operations. Each invocation requires a new `--output-name` and
refuses an existing evidence directory. Both print the loaded GM assembly hash.

The support-chain runner additionally verifies the actual GM-reloaded product
against PK before any subsequent Boolean. Its explicit `--lifecycle` copies
both products, deletes their source bodies, mirrors/scales the copies and
rechecks fresh GM XT with real PK. `--blend-only` selects this independent Blend
quality without making a later Boolean a precondition.

Actual narrow executions used `MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache`,
`-c Release -p:BuildInParallel=false -p:UseSharedCompilation=false
-p:GmInProcessLinkTasks=true`, followed by:

| Runner | Arguments after `--` | Actual result |
| --- | --- | --- |
| `scripts/RegularSolidBlendOracle.cs` | `plane-nurbs rounding-only --event-arrangement --output-name=blend-contract-regular-v1` | strict/full geometry passes; mass Unresolved; external exit 2 |
| `scripts/SupportBlendChainOracle.cs` | `asymmetric43-base --event-arrangement --separate-support --blend-only --lifecycle --output-name=blend-contract-chain-v1` | construction/reload/copy/source deletion/mirror-scale strict/full geometry passes; mass Unresolved; external exit 2 |

Logs and original exits: `temp_docs/blend-functional/{regular-contract-v1,chain-contract-v1}.{log,exit}`.
The exact runner diff is `temp_docs/blend-functional/contracts-v1.diff`, SHA256
`5a074cb6a17bd687037b3d515f4beeb7de690fbfb3954ea476a361cd888de2b6`. Actual XT remains private in each runner's named output
directory. These executions do not qualify all Blend families or Boolean.

## Active functional inventory

The unmodified-base local-operation Blend catalog (`--blend-arrangement`) ran
17 cases: 7 strict geometry passes, 10 hard failures, external exit 1. The five
axial cylinder fillet/chamfer cases pass. Planar all-edge fillets and planar
chamfers still have native curve/surface sense differences. Original evidence
is `temp_docs/blend-functional/local-base.{log,exit}` and is retained unchanged.

Native calls exposed different top-face LINE gauges between independently
created GM and PK blocks. This does not establish a primitive defect. The local
oracle now supplies the same physical all-edge mark order and offers explicitly
labeled GM/PK-authored identical-seed diagnostics, preserving both real products
and original input bytes. Those diagnostics do not replace independent creator
qualification or canonicalize either output. Native sense work remains active.

## Planar chamfer direction increment

Real PK's generated planar chamfer uses an inward natural plane normal,
negative surface sense and positive face sense. Negating only the normal and
face sense has the correct material normal but fails both separate native
sense checks; the XT fields were inspected through actual native transmit and
GM receive. Construction now preserves the native combination directly.
Chamfer contacts inherit the source face's oriented native line gauge: a
negative contact reverses both its direction coefficients and station signs,
with positive edge/curve sense. Finite cap sections use negative curve sense
with both coedges and endpoint association changed together. Fillet contact
line behavior remains the accepted parent-gauge rule. All-edge contact lines
also use this source occurrence, rather than generated polygon direction.

`local-chamfer-contract-v2.{log,exit}` records 11/17 geometry passes, six hard
failures and original exit 1. All four separate planar chamfer cases now pass
the unchanged comparator: single, parallel, unequal and oblique unequal.
`chamfer-lifecycle-v2.{log,exit}` additionally has actual exit 0 for the single
chamfer's copy, deletion of the original body, reflection/scale and editable GM
XT reload, with full native checks, strict comparison, material boundary
direction and mass Verified. Both remain private under
`temp_docs/blend-functional/`; corresponding XT is in the named local-operation
output directory. `directions-lifecycle-v1.{log,exit}` subsequently executes the
complete 17-case Blend catalog and the independent lifecycle of every one of
the 11 passing constructions; six construction comparisons still fail, external
exit 1, one mass comparison Unresolved. This is not overall acceptance.

An exploratory all-edge circular-normal change did not establish native
direction equivalence and was removed. Its original failing logs remain; it is
not part of the committed kernel increment. The 140/140 focused development
run (`focused-v1.{log,exit}`, actual exit 0) covered LocalOperation,
TrivalentSolidBlend, BlendMarkOrder, RollingNetwork, RegularSolidBlend,
BlendSupportChain and BlendChain. This run precedes the final raw planar sense
assignment and removal of the exploratory circular-normal code; it is recorded
as development regression, not final-source qualification.

## Native gauge control and unresolved acceptance

`LocalOperationOracle --native-gauge-control`, filtered to block-all-fillet,
creates the two original blocks independently with the same parameters, saves
both inputs, then performs **both** blends in real PK. The two input bodies
pass full checks and strict comparison before modification. Each native
operation receives the same physical all-edge mark order and radius `.4`.
Both real native results pass fresh receive/full checks, oriented material and
mass Verified, but strict BODY_compare reports curve-sense differences. The
actual process returns 1; no GM Blend product is qualified by this control.

Evidence: `temp_docs/blend-functional/native-gauge-control.{log,exit}`;
log SHA256 `01eee3eb28ced76561e3b4f57be7fa33e6b85813a0a13f91f3f60263aa2000d3`.
The two untouched seeds and two actual native results are in
`temp_docs/local-operation-oracle/blend-native-gauge-control/`. This establishes
a limitation of applying raw sense equality across different native input
gauges, not a primitive defect or a geometric acceptance waiver. The conflict
between that gate and preserving each input's native gauge was escalated to
the coordinator. The comparator, primitive constructors and sense whitelist
remain unchanged. All-edge fillet/chamfer and oblique single fillet remain
unaccepted while their direct strict differences are unresolved.

The coordinator subsequently authorized a narrow, evidence-based adjudication
of each analytic finite curve-sense difference, without changing the native
mask, canonicalizing outputs or permitting an unconditional sense exception.
That work remains ongoing. The installed official `PK_DEBUG_diff_t` reference
defines 21959 as matching curves with different senses; XT 5.2.9 separately
defines the curve sense as the edge/curve orientation. Neither definition by
itself certifies finite trim or incident directed uses.

## Ownership and operation-specific handoff

The final native ownership check found an introduced temporary-carrier leak in
parallel chamfer construction. Binding all linear draft carriers while creating
one cap section made later contact replacements orphan those temporary curves.
The cap now allocates only its own carrier. The retained failed run is
`parallel-chamfer-ownership.{log,exit}`; the corrected
`parallel-chamfer-ownership-v2.{log,exit}` passes construction and the complete
self lifecycle with original exit 0 and complete geometry/return-memory cleanup.
The managed positive edge/corner test now also deletes the result and requires
all topology/geometry pools restored; `local-tests-ownership-v2.{log,exit}` is
46/46, actual exit 0, on the corrected computation source.

The complete ownership-enabled local catalog
`directions-lifecycle-ownership-v2.{log,exit}` reports 10 fully passing cases,
six strict direction failures and one seed-ownership failure (external exit 1).
Oblique unequal chamfer still passes its geometry and lifecycle comparisons,
but its original extrusion fixture itself leaks four curves and four points:
`--seed-ownership-control`, without any Blend operation, reproduces
`before=(0,0,0,0,0), after=(0,4,0,4,0)` in
`oblique-seed-ownership.{log,exit}`, external exit 1. This is kept separate from
Blend construction acceptance and will be handed to the shared owner.

`ShortEdgeBlendOracle --blend-only-lifecycle` now explicitly selects copy,
source deletion, reflection/scale and editable XT reload without either later
Boolean. The default combined lifecycle remains available as its own operation
selection. Actual execution `shortedge-single-v1.{log,exit}` used
`--output-name=blend-single-operation-v1 --event-arrangement --geometry
--lifecycle --blend-only-lifecycle --material-ownership --require-native-fin-uvbox
--require-native-face-domains`. Construction, finite ICurves, paired UV,
bidirectional finite face domains, native fin boxes, material and both self
lifecycle comparisons pass; six mass comparisons are Unresolved, external exit
2. Loaded assembly hash is
`814ec86f42fc2604ac3a7faaa5ddb7b37487609300a49aba1719cb8cf0ebb1ce`;
the later change only removes temporary planar-chamfer carriers, a path not
entered by this heterogeneous rolling network.

Actual private product directory:
`temp_docs/short-edge-blend-oracle/blend-single-operation-v1/`.
The self-constructed `a1-r0.5-t1E-07-reverseFalse-default-self.x_t` SHA256 is
`37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`.
The transformed self copy and its editable reload both hash to
`57eecc0294734c805d3ded1ab034fc4c78dec6da6d2300a6ce85a0e553981ad2`.
All input/product/mass hashes are in
`temp_docs/blend-functional/shortedge-products.sha256`, manifest hash
`b055abaea9c88446d0983d911de087dbd406bcf25d647a9bf85bcc813919e56d`.
This is a fixed Blend-to-Boolean input handoff with layered status, not a
qualification of any later Boolean or overall Blend completion.

## Finite analytic direction adjudication

The current helper retains the original native comparison mask, all raw local
differences and their entities/faces. A 21959 difference can pass only for a
unique Line, Circle or Ellipse boundary edge on each solid, with exactly two
incident uses whose corresponding faces are supplied by native BODY_compare.
Every other local/global difference remains a hard condition, apart from the
existing chart-count diagnostic. Unsupported classes, tolerant fin overrides,
ambiguous edge/face associations or unproved bounds fail; this is not a general
curve exception or a procedural-curve equivalence claim.

For a line, the proof uses an explicit affine native map `s=alpha*t+beta`.
The complete residual `A0-B0-beta*Bd + t*(Ad-alpha*Bd)` is enclosed over the
entire actual source trim. An interval dot product certifies tangent sign.
For a conic `C+X*cos(t)+Y*sin(t)`, the map is `s=theta+sigma*t+2*pi*k`, with
`sigma` equal to +1 or -1. Outward coefficient residuals bound the difference
at **every** parameter. A Gram-matrix lower bound and the complete derivative
coefficient residual prove consistent tangent direction, including small-radius
or poorly conditioned inputs. The selected finite intervals map both ways;
endpoint coverage errors are bounded by the complete target speed bound.
Integer periodic lifts are enclosed using the true 2*pi interval, not an
assumed exact binary64 period. The short Taylor enclosure for sin/cos rounds
every operation outward; its omitted remainder is bounded for the full phase
range. No fitted or sampled curve establishes any of these results.

The complete coefficient-plus-trim distance bound must fit both the actual
reported edge precision and the stricter working cap 1e-9. Corresponding
topological endpoints and their unchanged precisions must also agree within
the stricter edge/vertex bound. Each matched face's directed fin use is checked
through `PK_FIN_is_positive`, edge/curve sense and the certified native tangent
map. Any separate fin curve must report the same carrier and orientation.
Thus reversing a raw edge/curve orientation is accepted only when both incident
uses and endpoint association compensate correctly. The original geometry,
trims, gauges and topology are never rewritten for comparison.

The original two-real-PK control is preserved as external exit 1. Its new
execution `native-gauge-certified-v1.{log,exit}` proves the actual 16 raw
curve-sense records under this contract and returns 0; it still qualifies no GM
construction. `certified-directions-v1.{log,exit}` and
`certified-directions-reverse-v2.{log,exit}` each execute all 17 independent GM/PK
Blend constructions and their copy/delete/transform/reload lifecycles. Both
have 332 proof records (the same edge is reported on each incident face and
again in lifecycle comparisons), 15 complete passes, two original oblique seed
cleanup failures, two mass comparisons Unresolved and original external exit 1.
All 17 constructions and lifecycle geometry comparisons pass; neither process
is described as fully passing while seed ownership remains unresolved.

The comparator control runner now requires a new output name and retains every
actual XT. It covers native edge reversal, freshly received raw Line/Circle
sense reversal with both fin uses compensated (full BODY_check zero), and the
existing degree-elevated/knot-inserted positives. Wrong dimensions, translation,
finite wire trim, spline interior, surface pole, uncompensated curve sense,
one wrong incident fin, material orientation and a compensated-but-wrong
circle radius remain rejection controls. `direction-controls-v4.{log,exit}`
executes 10 positives and 10 negatives, original exit 0. The first new control's
null-free error (906) is separately preserved in `direction-controls-v2` and is
not counted as successful; `v3` has the earlier 10-positive/9-negative catalog.
The final tightening to each endpoint's own precision is covered by the new
`direction-controls-v5` execution. These are comparator controls and cannot
replace construction, ownership or mass evidence.

Previously accepted f9 short-edge matrix/lifecycle evidence is reused read-only
as scoped by the dispatch; no full 96/1885/347 rerun has been performed here.

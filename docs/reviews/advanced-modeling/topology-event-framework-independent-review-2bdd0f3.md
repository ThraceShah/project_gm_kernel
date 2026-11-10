# Independent R2 re-review and R4 native control — 2bdd0f3

**R2a/R2b are accepted for the existing provider with finite, nonnegative error budgets. R1's previous standard-lifecycle acceptance is retained. R4's original all-four-equal gate remains failed: a pure PK control independently reproduces seven native-versus-native-XT differences without any GM dependency.** A user decision about reference pairing is required before changing that gate; this review changes no acceptance code or fixture.

## Fixed snapshot and review boundary

- Candidate `2bdd0f30c2cddca64ac8b1197ac66f39e210894d`; fetched and checked out on independent branch `ThraceShah/gm-opus-r2-r4-review`. Increment against `c301b40d18d4874af1862a0da80d64894a8d7692` contains only `CompactBranchCertificate.cs` and its tests. Original main comparison remains `610e3ce844bf99c85af6069905f9f3deaab08133`.
- Previous report commit `4e8f0ce14463446c0c9734fb569830407c3fac9e` remains on `origin/ThraceShah/gm-opus-r1r2-review`; its original ignored harness and evidence were preserved. No production/test source, CSV coordinate, tolerance, allowlist, other worktree, main or deployment was changed. No subagents, Windows or NativeAOT runs.
- .NET SDK `10.0.302`; PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`; genuine PK `38.0.150`, session precision `1E-08`. Used the existing ParasolidScriptHost and PskernelSharp after rechecking the local parasolid-research skill and query/binding signatures. The pure native process never references or loads a ProjectGmKernel assembly.
- R2 code inspection and managed runs use only the fixed candidate. The implementer's uncommitted `ProbeUlpSensitivity.cs` was read as research input, not as a candidate change. Its reported eight sensitive rows are distinguished from our exact-coordinate control below.

## R2a — spatial endpoint certificate repaired

`src/ProjectGmKernel.Native/Geometry/Evaluation/CompactBranchCertificate.cs:185` captures the complete original station root and nested cells. Lines 189–192 enclose its compact gauge level and the nominal compact station parameter, then require that original state inside a compact Krawczyk uniqueness box for that whole interval. `NestedBlendBounds.cs:214`–233 checks containment against the initial contracting box and refines the enclosure without discarding any root of the enclosed parameter family. Consequently both the original root at its projected level and the compact root at the published station lie in the returned spatial tube.

Lines 197–199 now compute a Euclidean box diameter, rather than parameter displacement divided by scale. For each coordinate interval `[L,U]`, the outward interval `U−[L,U]` encloses `[0,U−L]`; squaring, summing and applying the upward square root encloses the spatial diameter. Nonfinite diameter rejects. This is conservative even if dependency causes a wider result. It requires complete-root containment on identical nested equations, not just intersecting XYZ boxes.

The corrected comment at lines 45–60 distinguishes exact branch correspondence at the projected level `p*` from the bounded displacement of the nominal published endpoint. It no longer concludes that unequal nominal station levels have identical endpoints. On the common level interval the regular, monotone continuation/uniqueness argument applies; the endpoint tube supplies the finite-use error bound. The production bound remains `1e-10`, a small fraction of the `1e-8` model resolution; no chordal/angular budget is substituted for this bound. This is a tolerance-bounded finite representation contract, not bitwise equality of endpoint coordinates.

The original `CertificateProbe.cs` was run **unchanged** against `2bdd0f3`:

| Original adversarial proposal | Actual endpoint distance | New LinkDeviation | Result |
| --- | --- | --- | --- |
| `ε=.01`, endpoint angle shift about `1e-9` | `1.0000000857943934E-09` | `1.0004609126535642E-09` | NotConverged |
| `ε=.001`, shift about `1e-8` | `9.999999939103819E-09` | `1.0004271379213153E-08` | NotConverged |
| `ε=.0001`, shift `1e-7` | about `1e-7` | compact coverage fails | NotConverged |

Both formerly successful false endpoint claims are rejected without changing the link threshold. Equivalent near-semicircle controls in the new tests pass. The unchanged harness also prints its old half-width calculation; that final line is a historical arithmetic counterexample, not a call to the repaired helper.

## R2b — enclosing scalar bounds and analytic cosine decision

The acceptance arithmetic is now conservatively directed for valid provider inputs:

- `MeanValueRadius`, lines 203–207, steps up the larger distance from the **rounded** centre to the two ends. Each subtraction has one rounding; an upward step encloses it. The previous three-ulp example now gives `0.00024414062500000005`, exceeding the required `0.000244140625`. Exact BigInteger rational checks also cover negative large coordinates, `1e-300`, `.5` and subnormals.
- `MeasureChordBox`, lines 228–240, uses outward interval arithmetic, divides the projection by `e·e`, and separately encloses perpendicular offset and derivative drift times the complete parameter radius. Upper square roots and an upward-rounded sum enclose the distance. A non-unit chord therefore does not silently move the projection plane. Invalid/nonfinite offset or drift rejects with infinite distance; zero chord and NaN controls were exercised.
- Lines 241–245 bound `cos(D,e)` from below using a lower `D·e` and upper bounds on both norms. Outward division can weaken this bound but cannot make it too high. Normalization uncertainty is not hidden in a unit-chord assumption. An independent exact rational `D=(3,4,0), e=(1,0,0)` control gives lower cosine `0.5999999999999991 <= 3/5`.
- At line 151, for `0 <= angularLimit < π`, acceptance requires `cosineLower >= CosineUpper(angularLimit)`. Since cosine decreases on `[0,π]`, that proves the requested angular bound. For a limit at least π the angular constraint is vacuous. `Math.Acos` at line 153 only reports a diagnostic; it is no longer an acceptance operation.

The Taylor bound at lines 249–258 has an analytic justification beyond the 4001-point test. Let

```text
P8(x) = 1 − x²/2 + x⁴/24 − x⁶/720 + x⁸/40320,
Q(x)  = P8(x) − cos(x).
```

Then `Q^(8)(x)=1−cos(x)>=0`, and derivatives of orders 0 through 7 vanish at zero. Integrating eight times gives `Q(x)>=0` for `x>=0`; evenness gives the same for negative x. Thus `P8` is an upper bound for every real x. The code encloses polynomial arithmetic in intervals and caps with the independent upper bound `cos(x)<=1`. Exact rational arithmetic verified floating-point polynomial evaluation at production-sized angles, the two adjacent doubles around `.3`, and wider values through π. These checks test the implementation; the integration argument supplies the mathematical bound. Overflow/NaN cannot pass the valid finite-angle comparison. Outside the needed small domain the bound can become loose and cause conservative rejection.

The independent focused run passes **26/26**, comprising 18 compact-certificate cases and eight NestedBlendBounds cases. The scalar probe also verifies the former linear mean-value counterexample is now enclosed, and NaN angular input rejects. No new valid-provider counterexample was found.

Two nonblocking limits are explicit. First, the internal overload does not validate a negative angular budget: the scalar probe's regular short unit-circle arc returns Success for both `+.3` and `−.3` (`CompactBranchCertificate.cs:87,151`). This is a regression for invalid internal inputs because cosine is even; it does not affect the current producer, which uses `Math.Max(maxAngular,1e-6)` at `KernelRuntime.NestedBlendCompilation.cs:186`. Scoped acceptance therefore requires the stated finite, nonnegative budget precondition; the function must not be advertised as accepting arbitrary error budgets without validation. Second, line 62 and the report-wide “rigorous upper bounds” wording still overstate the diagnostic Acos value: that diagnostic has no outward step. Acceptance uses cosine, so this wording issue does not invalidate the repaired gate.

## R4 — independent pure PK exact-coordinate control

The control reconstructs the default seed using the **verbatim pure-PK `Seed` function** from the fixed candidate and genuine PK topology/geometry calls. It selects the same ellipse and line, sets radius `.5` and marking tolerance `1e-7`, and invokes native Fix with `local_check=true`, tolerance `1e-7`, and default update. It has only a PskernelSharp project reference and imports the existing Host through the same build flags. It performs no GM construction, session start, receive, export or classification, and asserts no loaded assembly name starts with `ProjectGmKernel`.

The native seed, resulting native product, and its own XT receive each pass a full BODY_check with zero faults. Existing strict body comparison succeeds (`global=21921 local=21931 face_pairs=0`, interpreted by the shared compare helper rather than by `face_pairs` alone). Transmit uses text, version 371; receive uses text and the same defaults as the original oracle. There is no precision override or coordinate perturbation in the control replay. API signatures were checked for `PK_BODY_contains_vector`, transmit/receive and the native construction calls; there is no hand-written ABI.

The generated native product hash is **exactly identical** to the native XT from the previous independently run standard lifecycle:

```text
native-product.x_t SHA-256
bd75b7926956c1506e00c172816a0bfc1bb07db1660d74da65aaf1734f219ef1

native-seed.x_t SHA-256
262963ce0b899972272970ace0eff5da8b9161dbf007bd01f20a9d3037a8d1a2

unchanged original CSV SHA-256
90b5dc64507142a4303a8e4263d4253e187a8920d780dd7818934aaeb4fd41dc
```

The same native-product bytes, with the first hash above, are the receive input; this is a native producer's own XT roundtrip. Thus the control does not depend on previously GM-produced geometry or a merely asserted model match. Its result is **510 coordinates completed, seven native/roundtrip differences, zero changed native baseline classes and zero API errors**. `--strict-equality` returns **exit 1**, intentionally preserving the failed native equality gate rather than calling it a pass.

| Row | Exact original `(x,y,z)` strings | Native / native own XT |
| --- | --- | --- |
| 102 | `(-1.4999999999996692,-2.0000000761480714,4.999999989999994)` | `5703 / 5701` |
| 153 | `(-1.4999999169520075,-2.000038073674117,4.999999988550392)` | `5703 / 5701` |
| 204 | `(-1.4999996684760124,-2.000076070478266,4.999999984213227)` | `5703 / 5701` |
| 254 | `(-1.4999996678119865,-2.000076146623441,5.000000004201636)` | `5701 / 5703` |
| 289 | `(-1.499999667811987,-2.500000004201637,4.50007614662344)` | `5702 / 5703` |
| 390 | `(-1.4999999169520075,-2.499999988550392,4.500038073674117)` | `5703 / 5701` |
| 441 | `(-1.4999999999996692,-2.4999999899999943,4.500000076148071)` | `5703 / 5701` |

Bindings identify `5701=inside`, `5702=outside`, `5703=on`. Every exact-coordinate difference here involves on-boundary classification, not an inside/outside exchange. That fact does not make the unchanged strict comparison pass. All 503 other rows agree. The data match the seven rows from both original independent a1e4500 replays, including each original native/roundtrip classification, not just the number seven.

## Changed-coordinate sensitivity is separate evidence

The implementer's uncommitted sensitivity script classifies a **received** native XT at original coordinates and at each axis ±1…4 ulps. Its eight-row summary is plausible and includes row 339 in addition to the exact-coordinate seven. It is a diagnostic about conditioning under changed inputs; it is neither the same experiment nor a proof that eight original coordinates have an undefined acceptance result.

The independent control repeats only rows 102 and 339, after finishing all 510 original queries, and records every perturbed coordinate and result. Both show classes `{5701,5703}` under these perturbations. Concrete examples:

- Row 102 originally has native/roundtrip `5703/5701`; changing z upward one ulp to `4.999999989999995` yields `5703/5703`.
- Row 339 at the **unchanged** coordinate `(-1.4999996684760124,-2.4999999842132272,4.500076070478264)` has native/roundtrip **5701/5701**. Moving y downward three ulps to `−2.4999999842132286` yields `5703/5703`. This is evidence for coordinate sensitivity, not an own-XT discrepancy at row 339 and not a GM exemption.

No dense sampling is used as a geometry equivalence proof. The original CSV is unchanged. No observation about these two perturbed rows independently verifies the implementer's full eight-row sweep, or proves that the self fresh row 339 discrepancy lacks a GM geometry/export cause.

## What the frozen gate requires and what remains GM-specific

At fixed candidate `scripts/ShortEdgeBlendOracle.cs:1352`–1376, `SavedProbes` requires all four **current** classifications `N/R/M/F` to equal the native result, where N is live native, R is native own XT receive, M is live GM, F is GM XT received by PK. Its changed-native counter checks the original CSV's native column; the other three CSV classification columns are historical evidence, not current expected values.

The pure native counterexample establishes `N!=R` independently of GM, with unchanged N and exact native bytes. Therefore no choice of GM M/F can satisfy that all-four-equal conjunction on this fixture/PK version/options. This is an external acceptance blocker, not something an implementation of GM classification can repair. It does not establish a universal Parasolid defect or unavoidable differences for other fixtures, versions, precisions, call histories or formats.

The actual previous **a1e4500** independent replays still have material GM mismatches. They were not rerun as new 2bdd0f3 GM replays in this bounded task; their snapshot and original exit **134** are preserved explicitly. The following counts come from their complete 510-point evidence, reconstructed only where the recorded review states that absent mismatch rows agreed in all four columns with the unchanged native baseline:

| Pair / old snapshot | Self | Imported |
| --- | --- | --- |
| N versus R | 7 | 7 |
| N versus M | 9 | 11 |
| N versus F (original comparator) | 1 (`339`) | 7 |
| R versus F (proposed same-path comparator) | 8 (`102,153,204,254,289,339,390,441`) | 0 |
| N versus M restricted to the 503 native-stable rows | 8 | 9 |

The eight stable-row self GM failures are `50,101,152,203,339,340,442,493`; imported additionally fails `391`. Row 339's self fresh discrepancy remains at unchanged coordinates with N=R=5701 but F=5703. Row 50 is another concrete independent GM classifier failure: N/R/F=5702 while M=5703. These cannot be attributed to the seven-point native transport control. Direct GM differences at 254, and imported 289, also remain failures under the original hard expectation, but their reference-side transport ambiguity must be reported separately from those stable-row counterexamples.

## Reviewable user decision; no change authorized here

Two concrete choices preserve the evidence without silently exempting sensitive coordinates:

1. **Keep all-four equality.** R4 remains failed with a demonstrated native-only external blocker at seven exact coordinates. Continue correcting GM-specific failures, but do not require the implementer to change independent PK's uncontrollable behaviour or report the combined gate as passed.
2. **Adopt same-path pairing, only on user authorization.** For all **510 original coordinates**, require live GM M=N and GM fresh F=R; retain N-versus-R as a separately reported native transport diagnostic. Keep exact CSV coordinates, unchanged native baseline monitoring, zero API-error requirements, all geometric/structural checks and original raw mass threshold. There is no row allowlist or removal. This makes the references coherent with the compared lifecycle and removes the logically impossible native equality conjunct.

The second choice does **not** automatically accept GM direct or exported results: under the preserved old evidence, self M still fails nine rows and self F now fails eight, while imported M fails eleven. The seven newly failing self F pairs must each be investigated under that chosen contract; row 339 is never waived. Same path also does not guarantee identical CHART parameterization or identical classifications for every mathematically equivalent representation. It is a clear numerical compatibility requirement, not a proof that representation details cannot matter. Neither “inside/on/outside union” acceptance nor ignoring all eight ulp-sensitive rows is authorized or established by this report. The coordinator has presented a decision to the user; this document records the pending choice, not an approval.

## Executed checks, persisted evidence and remaining gates

All executed dotnet commands used `MSBUILDDISABLENODEREUSE=1`. Commands below run from the project root; the reproducer appendix contains the native/script build imports and full source. After the test run, process inspection found no remaining testhost; no global kill targeted other workers.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~CompactBranchCertificateTests|FullyQualifiedName~NestedBlendBoundsTests' --logger 'console;verbosity=normal'
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/opus-r1-r2-review/CertificateProbe.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/opus-r2-r4-review/ScalarBoundsProbe.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/opus-r2-r4-review/NativeRoundtripControl.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --strict-equality
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/AllocationBanCheck.cs -c Release
```

| Raw log basename under `temp_docs/opus-r2-r4-review/` | Actual exit | Result |
| --- | --- | --- |
| `focused` | 0 | 26/26 |
| `old-certificate-probe` | 0 | Both previous false-success controls now reject with enclosing spatial bounds |
| `scalar-bounds-probe` | 0 | Exact-rational radius/polynomial/cosine checks; invalid-input observations retained |
| `native-control` | **1** | Strict native equality fails at exactly seven of 510; zero baseline changes/API errors |
| `allocation` | 0 | 2888 methods, CLEAN |

The implementation worker's `temp_docs/opus-fix/r2ab-standard` log was read in its own worktree: exit 0, 23 full checks zero faults and the original standard raw mass values equal the previous independent c301 run. Its transverse log still exits 134 for R3. These are **implementation-worker results**, not new independent managed lifecycle runs in this task. The previous independently established R1 standard result remains separate evidence. Reported cost remains material: about 2.5–3.64 seconds and up to 10930 boxes for a 10-station curve, plus a roughly 99 ms 257-box two-station case in that implementation log. The allocation IL guard does not prove every BCL call path allocation-free.

Durable review evidence is in `docs/reviews/advanced-modeling/topology-event-framework-2bdd0f3-evidence/`:

- `native-control-all510.csv`: all exact-coordinate rows, original four CSV classification columns, actual pure-native N/R, both API errors and input double bits. Historical `−157` values in the original CSV columns are not current API errors.
- `historical-a1e4500-{self,imported}-all510.csv`: all four actual old replay results and each pair predicate, clearly labelled with their historical snapshot. Their N/R columns are checked row-for-row against the new pure-native control.
- `ulp-representatives.csv`: all 54 representative queries, including repeated unperturbed controls, with exact changed doubles and N/R.
- `reproducer-sources.md`: standalone native control with the verbatim Seed, build imports and scalar probe; `evidence.sha256`: hashes of result/source files, raw logs/exits and native artifacts. Raw logs/native artifacts remain in the ignored evidence directory, without SDK/license material.

R2 is accepted only for the declared provider and valid budget domain; the negative-budget internal-overload observation is an explicit follow-up, not hidden universal input acceptance. R3, the final R4 user decision and subsequent full 510-point GM replay, R5/R6, self-construction 96 cases, transformed lifecycles and full regression remain open. This review neither completes M0–M5 nor accepts an implementation merely because native-only equality cannot pass.

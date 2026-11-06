# Trivalent corner consumer review

This review preserves additional diagnostics separately from the accepted
corner producer and base lifecycle matrices. It does not lower any gate.

## Preserved cut and re-fillet inputs

The source is the existing degree-four/four roof with three equal-radius `.1`
incident edge blends and its real spherical corner. A subtracting box with
centre `(-.475,.5,-.1)` and size `(1.05,2,2)` cuts through that Sphere and Blend
bands at `x=.05`. Fresh receive, complete native body checks, strict structural
comparison, mass and independent material classification pass for this step.

Two second-cut inputs are kept independently:

- The wide notch uses centre `(.5,1.1,.7)`, size `(2,1,1)`. Its new roof/knife
  edge ends on an existing Blend, not a planar cap. The observed first endpoint
  is approximately `(.08901059,.6,1.00545375)`. This is a genuine curved
  termination event; inventing a planar cap would change the problem.
- The middle notch uses the same centre and size `(.5,1,1)`. The new edge has
  endpoints `(.25,.6,1.02916)` and `(.75,.6,1.02916)`. The roof is one eight-fin
  BSurface loop; the knife is one four-fin Plane loop. Both caps are Plane,
  each with four fins comprising **three Lines and one ICurve**, not four Lines.
  Native `fix_blends` radius `.025` succeeds and its saved result passes complete
  native `BODY_check` with zero faults. The old implementation failed at Ends.

The preserved pairs are:

- `temp_docs/trivalent-corner-oracle/base-native-author-wide-notch-input-self.x_t`
- `temp_docs/trivalent-corner-oracle/base-native-author-wide-notch-input-reference.x_t`
- `temp_docs/trivalent-corner-oracle/base-native-author-middle-notch-input-self.x_t`
- `temp_docs/trivalent-corner-oracle/base-native-author-middle-notch-input-reference.x_t`
- `temp_docs/trivalent-corner-oracle/base-native-author-middle-notch-result-reference.x_t`

`temp_docs/corner-consumer-review-inputs.json` records sizes and SHA-256 values.
The coordinates above describe manufactured oracle inputs only; none selects a
production algorithm branch.

## Cap growth and completeness

A valid new end curve need not be contained in the old finite cap. In the
middle notch the new cap can expand beyond an incident edge's original finite
interval. The exact original curve must first be shown to cover the extension;
a fitted continuation or unproved parameter clamp is not equivalent.

A useful sufficient certificate is exclusion from every unchanged opposite
boundary, with controlled replacement of the two incident boundaries. For a
Line boundary, complete Circle/ICurve interval projection onto its supporting
halfplane can establish a strong exclusion. Applying **all** old cap halfplanes
would reject legitimate growth. General curved opposite boundaries require
complete curve/curve exclusion and component/winding evidence, not a four-fin
count or midpoint test. Unchanged finite cap edges also lie on their other
adjacent faces, so existing whole-band separation can supply this exclusion
where those other faces are already covered.

Extra crossings of an end curve with the two incident supports are tangential
at the prescribed contacts; a transverse root finder is insufficient. The
conditional global-foot certificate in `docs/Rolling_Ball_Reach.md` proves a
moving rolling sphere cannot meet either complete primitive support elsewhere.
Combined with complete cap/contact roots and a regular circular section, this
excludes additional end-curve/support intersections. Local foot Hessians alone
cannot provide this result across a C0 crease or a remote second foot.

The complete end curve's centre parameter range must be covered. Taking only
the two contact stations is not generally sufficient. For the manufactured
cylinder `P(u,theta)=(u,cos(theta),sin(theta))`, `0<=theta<=pi/2`, the cap
`x+y+z=d` has both contact stations `u=d-1`, but its interior reaches
`u=d-sqrt(2)`. Rejecting the false isoparametric Circle is necessary but does not
fix a later domain assembled from contact extrema alone. Use certified Blend
UV bounds for the actual bounded ICurve, or certify that `u(theta)` has no
interior extrema; if the cap is a regular graph, extrema satisfy the cap
residual and its section derivative simultaneously.

Each end curve should also be excluded from the *other* cap using complete
curve bounds and branch-outward termination normals. The cap material normal
can face into a notch and is not a substitute. This guards intersecting cap
planes and short-band events that global support reach does not address.
Curve simplicity, complete local topology and a consistently oriented manifold
replacement remain separate obligations; the reach certificate alone is not a
general no-self-intersection theorem for a Blend.

## Extended post-modeling lifecycle diagnostic

The accepted base lifecycle still passes for both author families. A further
unaccepted probe takes the original degree-four/two continued corner, reloads
and copies it, deletes the reloaded source, then reflects/scales it by
`diag(-2,2,2)` with translation `(3,5,7)`.

Its fresh candidate and reference have zero complete native body faults and
pass the strict structural comparison. Independent checks report:

- 108 finite boundary points: maximum distance about `3.29e-12`.
- 54 source-contact and 27 live spherical-interface G1 samples: pass.
- 162 internal Blend points versus independent native construction: maximum
  distance about `3.69e-15`.
- The same 162 internal points versus fresh own XT received by PK: maximum
  distance about `6.90e-15`.

The mass point-value comparison nevertheless reaches `1.1831661e-7`, above the
unchanged `1e-7` gate, in small off-diagonal central inertia components. This
probe is **not included in the passing lifecycle count**. Example values are:

| Quantity | Native reference | Fresh candidate | Reported native error moduli |
|---|---:|---:|---:|
| Volume | 8.17060942944 | 8.17060964270 | 1.2720e-6 / 9.2373e-7 |
| Inertia component | -0.00972028489 | -0.00972040436 | 5.1780e-6 / 3.9523e-6 |
| Area | 23.57718747781 | 23.57718803773 | 5.0304e-6 / 4.1891e-6 |

The official Parasolid Functional Description, chapter 31, sections 31.1.1 and
31.4.1, specifies that `accuracy` controls integration work rather than a
percentage error, and model mass comparisons must consider both error bounds.
The reported intervals overlap. Increasing `accuracy` from `.99999999` to
`.999999999999`, requesting a common rigid integration frame, and rigidly
centering actual query copies do not remove this point-value difference. None
of these diagnostics changes the model acceptance threshold or excuses a
structural difference.

The immutable received inputs are preserved as
`temp_docs/trivalent-corner-oracle/review-extended-lifecycle-native-author-reference.x_t`
and `temp_docs/trivalent-corner-oracle/review-extended-lifecycle-native-author-self.x_t`.
The diagnostic below reports all 15 point values, their error moduli and
interval overlap, and deliberately exits nonzero when the existing point gate
fails. It does not construct a reference model or replace the actual modeling
oracle.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/TrivalentCornerMassDiagnostic.cs -c Release -p:UseSharedCompilation=false
```

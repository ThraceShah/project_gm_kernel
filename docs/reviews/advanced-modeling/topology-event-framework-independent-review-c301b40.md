# Independent R1/R2 review — c301b40

Status: **R1 accepted for the standard lifecycle; R2 rigorous finite-certificate acceptance blocked by R2a/R2b below**. The existing sampled-proof defect has been substantially repaired, and the standard target passes independently, but this is not acceptance of the whole topology-event framework or of R3–R6.

## Snapshot and isolation

- Fixed candidate: `c301b40d18d4874af1862a0da80d64894a8d7692`, on `origin/fix/topology-event-framework-opus` at dispatch; against main `610e3ce844bf99c85af6069905f9f3deaab08133`.
- Read `AGENTS.md`, the original plan, independent review at `a3e9a489`, the candidate response and all eight changed files. The original Native algorithm and review findings come from `a1e4500`; Web/hosting/scheduling work is outside this review.
- Review branch `ThraceShah/gm-opus-r1r2-review`. No production code, tests, fixture coordinates, thresholds, candidate branch, main, other worktree or deployment was changed. Counterexamples live only in ignored `temp_docs/opus-r1-r2-review/`.
- .NET SDK `10.0.302`; PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`; independently loaded real PK `38.0.150`, precision `1E-08`. Used the existing managed project reference, PskernelSharp and ParasolidScriptHost; genuine `pskernel` was not redirected to GM. Checked the BODY_check query signature and DEBUG_BODY_compare binding before the native run.
- No C ABI change; no NativeAOT build or publish. SDK/schema files remain ignored. No subagents were used.

## R1 — accepted within the exercised standard lifecycle

`src/ProjectGmKernel.Native/Runtime/KernelRuntime.NestedCurveArc.cs:183` requires matching program/published chart counts; lines 219–235 anchor each station UV to the published CHART witness at that node or the start of its original cell. The surrounding caller in `KernelRuntime.Copy.cs:44` first requires identity of the actual support charts, including normal relation; the curve's original support order and sense are retained.

Independent rerun of the original standard command exits **0**, with **23 full checks and zero faults**, and **both paths complete both cuts**. The imported lifecycle uses its normal Boolean path; only the self lifecycle receives `--proof-only-lifecycle`, despite both artifact names containing `proof-only-cuts`.

| Original raw normalized15 gate | Independent result |
| --- | --- |
| Self uncut | `3.965655017106088E-09` |
| Self reflected copy | `4.544547029326304E-08` |
| Self first / second cut | `2.3886265011558517E-08` / `2.2404188966449608E-08` |
| Imported first / second cut | `4.90888562409547E-09` / `4.736870552867567E-09` |

The command also reaches source deletion, editable receive, finite ICurve checks, paired UV checks, finite face domains, fin UV boxes and material probes with the original gates. This independently closes the previous standard `13826` failure. It establishes the exercised cylinder seam/copy/receive path; it does not establish every torus seam, reverse neighbour ambiguity, transformed lifecycle or self matrix. No additional UV counterexample was established in this bounded review.

## R2a — endpoint linkage proves a gauge bound, not the claimed exact finite endpoints

Locations: `src/ProjectGmKernel.Native/Geometry/Evaluation/CompactBranchCertificate.cs:44`–54, 99–106 and 179–188; production tolerance at `src/ProjectGmKernel.Native/Runtime/KernelRuntime.NestedCurveArc.cs:109`–111.

The original endpoint root is enclosed, its compact **gauge level** is computed, and it is contained in a compact uniqueness box for an interval spanning that level and the nominal compact parameter. This can identify the same regular geometric branch. It does **not** make the root at the nominal compact endpoint equal to the original endpoint. Line 188 measures `(parameter deviation)/Scale`, which is displacement along the chord. It lacks the speed/gauge-derivative factor needed to bound full spatial endpoint displacement. The comment at line 47, “share both end states exactly,” therefore does not follow. An open/closed argument can apply on common gauge levels of continuous arcs, but cannot make unequal nominal endpoint levels identical.

Executable counterexample: `temp_docs/opus-r1-r2-review/CertificateProbe.cs`, source retained below. Both supports are the same regular plane `z=0` and unit cylinder. Original: 64 cells over angles `[-π/2+ε, π/2−ε]`; compact: two stations, same start and final angle shifted by `δ=1e-11/ε`; positive chart scale maps the compact interval to the same numerical endpoints. The chordal/angular limits are finite (`4`, `π`), all roots are regular and monotone in the chord gauge. With `linkTolerance=1e-10`:

| ε / δ (radians) | Certificate | Evaluated finite endpoint distance | Reported LinkDeviation |
| --- | --- | --- | --- |
| `0.01` / about `1e-9` | Success, 59 boxes | `1.0000000857943934E-09` | `1.000191147764078E-11` |
| `0.001` / about `1e-8` | Success, 71 boxes | `9.999999939103819E-09` | `1.000165677812012E-11` |
| `0.0001` / `1e-7` | NotConverged | about `1e-7` | rejected during compact coverage |

This is a false exact-finite-endpoint claim and an amplification of the link tolerance, **not a demonstrated different-root jump**. These near-semicircle proposals are outside the usual short-cell proposal heuristic in `NestedCurveArc.cs:142`–166. The standard successful 10-station cells report angular bounds around `0.18`–`0.337`; the large amplification above is not evidence that their endpoints are wrong. Nevertheless the certificate has no enforced short-cell precondition corresponding to the unrestricted theorem it documents. Acceptance needs either proved endpoint spatial bounds on the actual linking domain and an accurate tolerance contract, or a sufficient enforced domain restriction plus proof; adding more samples or widening the original oracle threshold would not resolve it.

## R2b — rounded-midpoint radius is not an enclosure

Locations: `src/ProjectGmKernel.Native/Geometry/Evaluation/CompactBranchCertificate.cs:135`–148.

The code evaluates at `midpoint = lo + (hi-lo)*.5`, then uses `(hi-lo)*.5` as the distance to both ends. The rounded midpoint need not be the exact interval midpoint. The same executable probe prints:

```text
lo=1000000000000
hi=1000000000000.0004        (lo plus 3 ulps)
mid=1000000000000.0002       (lo plus 2 ulps)
radius=0.00018310546875      (1.5 ulps)
needed=0.000244140625        (2 ulps)
```

Thus the mean-value radius misses one quarter of the necessary radius in this entirely finite interval. For the linear scalar component `q(t)=hi-t`, the exact centre value plus this radius is `0.00030517578125`, while the true endpoint maximum is `0.0003662109375`. This directly refutes the asserted enclosing mean-value construction; it is not an appeal to uncertain general interval theory. No full certificate false acceptance on the current target was demonstrated from this arithmetic example.

The square roots, multiply/add for distance and speed, division and `Acos` in lines 144–147 also use ordinary scalar rounding for quantities described as rigorous upper bounds. In particular a rounded-up cosine can round the angle down. Existing `IntervalNumber` operations outwardly enclose the operands, but do not prove the final scalar upper-bound arithmetic outward. Acceptance requires a radius enclosing both distances from the actual rounded centre, outward evaluation of the derived bounds, and negative controls at representable boundaries. This is separate from the end-root issue.

## Other finite-certificate and BoundSegment checks

- The removed station-map sampling is no longer the publication gate. `NestedCurveArc.cs:199`–207 appends a verification-only original chart on the shared compiled supports, requires `CertifyCompactBranch`, then publishes the prefix excluding that verification spine.
- Increasing finite station ends are checked; every consecutive original and compact interval enters the covering DFS. Splitting retains both closed halves. Nonconverged boxes refine; unresolved/singular work, depth 40 or 262144 attempts reject. No empty-loop success from the former one-cell trim was found. The eight existing tests include the required one-cell wrong endpoint, different supports, opposite long-arc choice, reversal, wrong trim, understated bounds and tangency cases, and all passed independently.
- A successful Krawczyk box has strict interior inclusion and a weighted contraction check. `requireWitness` is checked against that **uniqueness box**, before interval refinement; the complete captured root state is required, rather than just XYZ proximity. Endpoint links also compare nested occurrence counts and every non-root spine/cell. Root equations share the same compiled support indices. This is materially stronger than the former sampled check; R2a concerns nominal finite endpoints, not a claim that containment is merely box overlap.
- The root-chart change at `NestedBlendBounds.cs:150`–155 only changes the numerical point/preconditioner. Interval residuals and Jacobians are still assembled for the chosen fixed cell over the entire requested domain. A preconditioner need not itself be the exact midpoint inverse; no independent soundness regression in this change was found.
- `BoundSegmentProbe.cs` independently tests `BoundSpineProjectedAcceleration` with a captured right-cell root at a unit-circle knot while forcing the left cell. Both the exact knot and `[knot−1e-6,knot+1e-6]` succeed, enclose the analytic acceleration `−1.0228418541375282`, and terminate. A displaced required witness and coincident/singular supports both return NotConverged. The existing eight NestedBlendBounds tests also pass. This is bounded evidence for the affected shared-root/projected-acceleration consumers, not all possible extension domains.
- On rejection, the caller in `KernelRuntime.Copy.cs:53`–61 retains the original compiled/native UV representation via CloneEntity. The scratch checkpoint is restored in the compaction `finally`. The standard lifecycle exercises copy/delete/receive and both guarded and normal cuts; existing generation/rollback contracts were not replaced. No unproved fallback was newly identified in R1/R2, and the broader normal Boolean proof limitation remains R6.

## Commands and evidence

All commands ran from the project root with `MSBUILDDISABLENODEREUSE=1`. No original test, fixed CSV or `1e-7` gate was changed. Native results below come from this review worktree, not the implementation worker's logs. Complete logs and original `.exit` files are under `temp_docs/opus-r1-r2-review/`; emitted XT products remain under `temp_docs/short-edge-blend-oracle/`.

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~CompactBranchCertificateTests|FullyQualifiedName~NestedBlendBoundsTests' --logger 'console;verbosity=normal'
PGM_XT_DIAGNOSTICS=1 MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ShortEdgeBlendOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --event-arrangement --geometry --lifecycle --proof-only-lifecycle --material-ownership --require-native-fin-uvbox --require-native-face-domains
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/opus-r1-r2-review/CertificateProbe.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/opus-r1-r2-review/BoundSegmentProbe.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/AllocationBanCheck.cs -c Release
```

| Log basename | Exit | Result |
| --- | --- | --- |
| `focused` | 0 | 16/16, including all 8 compact-certificate tests |
| `standard-lifecycle` | 0 | 23 full checks, zero faults, both second cuts, original later gates reached |
| `certificate-probe` | 0 | Reproduces R2a/R2b; success here means probe completed, not that certificate contract is valid |
| `bound-segment-probe` | 0 | Knot/straddling positives and wrong-witness/singular negatives |
| `allocation` | 0 | 2883 methods, CLEAN |

After the test run, process inspection found no remaining `testhost.dll`; no global kill was issued against other workers. Build output included the existing NU1903 ILCompiler package advisory; no dependency was changed and no native compilation was invoked. The IL guard is scoped evidence, not a proof of zero allocations in every BCL path.

Independent runtime cost confirms the disclosed debt: 10-station curves took **2.507–3.792 seconds** and **8828–10930 accepted boxes**. Two-station examples took **1.9–6.2 ms** except the 257-box near-boundary example at **103.8 ms**; reporting all two-station curves as 2–6 ms would omit that independently observed case. Counts are accepted boxes in the report; the 262144 work budget counts attempts. This review does not set a new performance acceptance limit.

## Acceptance boundary and remaining work

R1 is accepted for the reproduced standard target. R2 remains blocked as a rigorous finite certificate until R2a/R2b receive corrected bounds/contracts and targeted controls; the fixed candidate's successful standard result remains valid empirical evidence. Re-review must pin the subsequent implementation SHA rather than changing this snapshot report.

R3 transverse raw inertia still fails in the candidate's source logs (`cert4-transverse`, exit 134); it was not rerun here. R4 immutable 510-probe replay, R5 documentation reconciliation, R6 normal Boolean proof scope, the real self-construction 96-case matrix, transformed lifecycles and full regression remain implementation gates. The implementer's 254 targeted/19 rolling/catalog results are reported in its response; this review independently reran only the commands listed above and does not promote those other results into fresh independent evidence. No conclusion here accepts or completes M0–M5 as a whole.

## Review-only reproducer sources

These are standalone .NET 10 scripts saved at the paths used by the commands above; their project references are relative to those script locations. They do not modify production or test sources.

### CertificateProbe.cs

```csharp
#!/usr/bin/env dotnet run
#:project ../../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=KernelTests
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

BlendSurfaceInstruction[] surfaces=[
    new(){Analytic=new(SurfaceClass.Plane,default,new(){Z=1},new(){X=1}),NormalSense=1},
    new(){Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1),NormalSense=1}];
foreach(var epsilon in new[]{.01,.001,.0001})
{
    var start=-Math.PI/2+epsilon;var end=Math.PI/2-epsilon;
    var original=Arc(start,end,64,0,1);
    var upper=original[^1].Parameter;
    var shift=1e-11/epsilon;
    var compact=Arc(start,end+shift,1,0,upper/(2*Math.Sin((end+shift-start)/2)));
    compact[^1].Parameter=upper;
    BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=original.Length},
        new(){First=0,Second=1,ChartOffset=original.Length,ChartCount=2,ChordalError=4,AngularError=Math.PI}];
    var program=new NestedBlendProgram(surfaces,spines,original.Concat(compact).ToArray());
    var status=JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,new[]{0.0,upper},0,upper,1e-10,out var report);
    Span<KernelVector3> a=stackalloc KernelVector3[1],b=stackalloc KernelVector3[1];
    var sa=JointNestedBlendEvaluation.EvaluateSpine(program,0,upper,0,a);
    var sb=JointNestedBlendEvaluation.EvaluateSpine(program,1,upper,0,b);
    var distance=Math.Sqrt(Math.Pow(a[0].X-b[0].X,2)+Math.Pow(a[0].Y-b[0].Y,2));
    Console.WriteLine($"ENDPOINT epsilon={epsilon:R} shift={shift:R} status={status} actualDistance={distance:R} eval={sa}/{sb} link={report.LinkDeviation:R} boxes={report.Boxes} fail={report.FailedStage} [{report.FailedLower:R},{report.FailedUpper:R}]");
}
// The new mean-value box radius must reach both endpoints, even when the
// rounded midpoint lies closer to one end of an odd-ulp-width interval.
var lo=1e12;var hi=Math.BitIncrement(Math.BitIncrement(Math.BitIncrement(lo)));
var mid=lo+(hi-lo)*.5;var half=(hi-lo)*.5;
Console.WriteLine($"MIDPOINT lo={lo:R} hi={hi:R} mid={mid:R} radius={half:R} needed={Math.Max(mid-lo,hi-mid):R}");

static BlendChartInstruction[] Arc(double start,double end,int cells,double lower,double scale)
{
    var points=new KernelVector3[cells+1];var tangents=new KernelVector3[cells+1];
    for(var i=0;i<=cells;i++)
    {
        var angle=start+(end-start)*i/cells;
        points[i]=new(){X=Math.Cos(angle),Y=Math.Sin(angle)};
        tangents[i]=new(){X=-Math.Sin(angle),Y=Math.Cos(angle)};
    }
    var parameters=new double[cells+1];var scales=new double[cells];var chords=new KernelVector3[cells];
    var status=OriginalChartParameterMap.Build(points,tangents,lower,scale,parameters,scales,chords,out _,out _);
    if(status!=AlgorithmStatus.Success)throw new Exception("chart "+status);
    var result=new BlendChartInstruction[cells+1];
    for(var i=0;i<=cells;i++)result[i]=new(){Position=points[i],Parameter=parameters[i],Scale=i<cells?scales[i]:1,Chord=i<cells?chords[i]:default};
    return result;
}
```

### BoundSegmentProbe.cs

```csharp
#!/usr/bin/env dotnet run
#:project ../../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=KernelTests
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

BlendSurfaceInstruction[] surfaces=[
    new(){Analytic=new(SurfaceClass.Plane,default,new(){Z=1},new(){X=1}),NormalSense=1},
    new(){Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1),NormalSense=1}];
KernelVector3[] positions=[new(){X=Math.Cos(.3),Y=-Math.Sin(.3)},new(){X=1},new(){X=Math.Cos(.3),Y=Math.Sin(.3)}];
KernelVector3[] tangents=[new(){X=Math.Sin(.3),Y=Math.Cos(.3)},new(){Y=1},new(){X=-Math.Sin(.3),Y=Math.Cos(.3)}];
var parameters=new double[3];var scales=new double[2];var chords=new KernelVector3[2];
Require(OriginalChartParameterMap.Build(positions,tangents,0,1,parameters,scales,chords,out _,out _)==AlgorithmStatus.Success,"build");
BlendChartInstruction[] charts=[new(){Position=positions[0],Parameter=parameters[0],Scale=scales[0],Chord=chords[0]},
    new(){Position=positions[1],Parameter=parameters[1],Scale=scales[1],Chord=chords[1]},new(){Position=positions[2],Parameter=parameters[2],Scale=1}];
BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=3}];
var program=new NestedBlendProgram(surfaces,spines,charts);
Span<IntervalNumber> bounds=stackalloc IntervalNumber[9],witness=stackalloc IntervalNumber[3];
var knot=parameters[1];
var capture=JointNestedBlendEvaluation.BoundSharedSpineWitness(program,0,new(knot,knot,0,0),bounds,capture:witness);
Require(capture==AlgorithmStatus.Success,"capture");
foreach(var delta in new[]{0.0,1e-6})
{
    var domain=new SurfaceParameterBox(knot-delta,knot+delta,0,0);
    var status=JointNestedBlendEvaluation.BoundSpineProjectedAcceleration(program,0,domain,new(){X=1},out var acceleration,terminalChart:0,terminalWitness:witness);
    Require(status==AlgorithmStatus.Success,"boundary extension");
    var e=chords[0];var s=(1-positions[0].X)*e.X-positions[0].Y*e.Y+(knot-parameters[0])/scales[0]-(knot-parameters[0])/scales[0];
    // On the left gauge: e dot p = e dot P0 + (t-t0)/scale.
    s=positions[0].X*e.X+positions[0].Y*e.Y+(knot-parameters[0])/scales[0];
    var exact=-e.Y/(scales[0]*scales[0]*Math.Pow(1-s*s,1.5));
    Require(acceleration.Lower<=exact&&exact<=acceleration.Upper,"exact acceleration enclosure");
    Console.WriteLine($"BOUNDARY delta={delta:R} status={status} acceleration=[{acceleration.Lower:R},{acceleration.Upper:R}] exact={exact:R}");
}
witness[0]=new(2);
var wrong=JointNestedBlendEvaluation.BoundSpineProjectedAcceleration(program,0,new(knot-1e-6,knot+1e-6,0,0),new(){X=1},out _,terminalChart:0,terminalWitness:witness);
Require(wrong!=AlgorithmStatus.Success,"wrong witness rejected");
Console.WriteLine($"WRONG_WITNESS {wrong}");
surfaces[1]=surfaces[0];
var singular=JointNestedBlendEvaluation.BoundSpineProjectedAcceleration(program,0,new(knot-1e-6,knot+1e-6,0,0),new(){X=1},out _,terminalChart:0,terminalWitness:witness);
Require(singular!=AlgorithmStatus.Success,"singular supports rejected");
Console.WriteLine($"SINGULAR {singular}");
static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
```

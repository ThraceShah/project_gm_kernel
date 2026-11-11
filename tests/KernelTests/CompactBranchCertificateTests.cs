using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using Xunit;

namespace KernelTests;

// Finite compact-branch certificate on a plane z=0 / unit cylinder circle.
// The original CHART is a fine chart of the circle; a compact CHART takes its
// stations at original parameters, as the production recharting does.
public class CompactBranchCertificateTests
{
    private const double Link=1e-10;

    private static BlendSurfaceInstruction[] Supports(double radius=1)=>
    [
        new(){Analytic=new(SurfaceClass.Plane,default,new(){Z=1},new(){X=1}),NormalSense=1},
        new(){Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},radius),NormalSense=1},
    ];

    private static KernelVector3 Circle(double angle,double radius=1)=>new(){X=radius*Math.Cos(angle),Y=radius*Math.Sin(angle)};
    private static KernelVector3 Tangent(double angle)=>new(){X=-Math.Sin(angle),Y=Math.Cos(angle)};

    private static BlendChartInstruction[] Chart(ReadOnlySpan<KernelVector3> positions,ReadOnlySpan<KernelVector3> tangents,double lower,double upper)
    {
        var n=positions.Length;var parameters=new double[n];var scales=new double[n-1];var chords=new KernelVector3[n-1];
        Assert.Equal(AlgorithmStatus.Success,OriginalChartParameterMap.Build(positions,tangents,0,1,parameters,scales,chords,out _,out _));
        var baseScale=(upper-lower)/parameters[^1];
        for(var attempt=0;attempt<64;attempt++)
        {
            Assert.Equal(AlgorithmStatus.Success,OriginalChartParameterMap.Build(positions,tangents,lower,baseScale,parameters,scales,chords,out _,out _));
            if(parameters[^1]>=upper)break;baseScale=Math.BitIncrement(baseScale);
        }
        var charts=new BlendChartInstruction[n];
        for(var i=0;i<n;i++)charts[i]=new(){Position=positions[i],Parameter=parameters[i],Scale=i<n-1?scales[i]:1,Chord=i<n-1?chords[i]:default};
        return charts;
    }

    // Original: the arc of angles [0, sweep] with `cells` equal CHART cells,
    // parameterized from 0 to its chord-sum length.
    private static BlendChartInstruction[] OriginalArc(double sweep,int cells,double start=0)
    {
        var positions=new KernelVector3[cells+1];var tangents=new KernelVector3[cells+1];
        for(var i=0;i<=cells;i++){var a=start+sweep*i/cells;positions[i]=Circle(a);tangents[i]=Tangent(a);}
        var n=positions.Length;var parameters=new double[n];var scales=new double[n-1];var chords=new KernelVector3[n-1];
        Assert.Equal(AlgorithmStatus.Success,OriginalChartParameterMap.Build(positions,tangents,0,1,parameters,scales,chords,out _,out _));
        var charts=new BlendChartInstruction[n];
        for(var i=0;i<n;i++)charts[i]=new(){Position=positions[i],Parameter=parameters[i],Scale=i<n-1?scales[i]:1,Chord=i<n-1?chords[i]:default};
        return charts;
    }

    private static AlgorithmStatus Certify(BlendSurfaceInstruction[] surfaces,BlendChartInstruction[] original,BlendChartInstruction[] compact,
        double[] stations,out CompactBranchReport report,BlendSurfaceIndex compactFirst=0,BlendSurfaceIndex compactSecond=1)
    {
        BlendSpineInstruction[] spines=
        [
            new(){First=0,Second=1,ChartOffset=0,ChartCount=original.Length},
            new(){First=compactFirst,Second=compactSecond,ChartOffset=original.Length,ChartCount=compact.Length,
                ChordalError=Declared(compact,out var angular),AngularError=angular},
        ];
        var charts=original.Concat(compact).ToArray();
        var program=new NestedBlendProgram(surfaces,spines,charts);
        return JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,stations,stations[0],stations[^1],Link,out report);
    }

    // The production compact CHART declares these chordal and angular
    // errors from its anchors; the certificate must prove both.
    private static double Declared(BlendChartInstruction[] compact,out double angular)
    {
        var chordal=0.0;angular=0.0;
        for(var i=0;i<compact.Length-1;i++)
        {
            var a=compact[i].Position;var b=compact[i+1].Position;
            var length=Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y)+(b.Z-a.Z)*(b.Z-a.Z));
            var turn=Math.Abs(Math.Atan2(b.Y,b.X)-Math.Atan2(a.Y,a.X));
            if(turn>Math.PI)turn=2*Math.PI-turn;
            angular=Math.Max(angular,turn);chordal=Math.Max(chordal,.5*length*turn);
        }
        chordal=Math.Max(chordal,1e-11);angular=Math.Max(angular,1e-6);return chordal;
    }

    // Evaluate the original spine at each station, as production does.
    private static KernelVector3[] Evaluate(BlendSurfaceInstruction[] surfaces,BlendChartInstruction[] original,double[] stations)
    {
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartOffset=0,ChartCount=original.Length}];
        var program=new NestedBlendProgram(surfaces,spines,original);var result=new KernelVector3[stations.Length];
        Span<KernelVector3> jet=stackalloc KernelVector3[2];
        for(var i=0;i<stations.Length;i++)
        {
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.EvaluateSpine(program,0,stations[i],0,jet));
            result[i]=jet[0];
        }
        return result;
    }

    private static KernelVector3[] Tangents(KernelVector3[] points)=>points.Select(p=>Tangent(Math.Atan2(p.Y,p.X))).ToArray();

    [Fact]
    public void EquivalentRechartOfTrimmedArcIsCertifiedOverTheWholeInterval()
    {
        var surfaces=Supports();var original=OriginalArc(1.2,24);
        var lower=original[3].Parameter+.3*(original[4].Parameter-original[3].Parameter);var upper=original[20].Parameter+.6*(original[21].Parameter-original[20].Parameter);
        double[] stations=[lower,lower+.2*(upper-lower),lower+.55*(upper-lower),upper];
        var points=Evaluate(surfaces,original,stations);var compact=Chart(points,Tangents(points),lower,upper);
        var status=Certify(surfaces,original,compact,stations,out var report);
        Assert.Equal(AlgorithmStatus.Success,status);
        Assert.True(report.Boxes>=2*(stations.Length-1));
        Assert.True(report.LinkDeviation<=Link);
        // A unit circle cell of angle a deviates 1-cos(a/2) from its chord;
        // the interval bound must not be smaller than the true deviation.
        var widest=0.0;
        for(var i=0;i<points.Length-1;i++)widest=Math.Max(widest,Math.Atan2(points[i+1].Y,points[i+1].X)-Math.Atan2(points[i].Y,points[i].X));
        Assert.True(report.ChordalBound>=1-Math.Cos(widest/2)-1e-12,$"chordal={report.ChordalBound}");
        Assert.True(report.ChordalBound<=2*Math.Sin(widest/2),$"chordal={report.ChordalBound}");
        Assert.True(report.AngularBound>=widest/2-1e-12&&report.AngularBound<Math.PI/2,$"angular={report.AngularBound}");
    }

    [Fact]
    public void UnderstatedChordalOrAngularErrorIsRejected()
    {
        var surfaces=Supports();var original=OriginalArc(1.2,24);
        double[] stations=[original[2].Parameter,original[14].Parameter];
        var points=Evaluate(surfaces,original,stations);var compact=Chart(points,Tangents(points),stations[0],stations[1]);
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartOffset=0,ChartCount=original.Length},new(){First=0,Second=1,ChartOffset=original.Length,ChartCount=compact.Length}];
        var program=new NestedBlendProgram(surfaces,spines,original.Concat(compact).ToArray());
        var chordal=Declared(compact,out var angular);
        // The true sagitta of a 0.6 rad unit arc is 1-cos(0.3).
        var sagitta=1-Math.Cos(.3);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,stations,stations[0],stations[1],Link,chordal,angular,out var report));
        Assert.True(report.ChordalBound>=sagitta-1e-12&&report.ChordalBound<=chordal);
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,stations,stations[0],stations[1],Link,.9*sagitta,angular,out _));
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,stations,stations[0],stations[1],Link,chordal,.29,out _));
    }

    [Fact]
    public void TrimInsideOneOriginalCellIsStillEvaluatedAndRejectsAWrongCompactPoint()
    {
        // No original CHART node lies inside [lower, upper].
        var surfaces=Supports();var original=OriginalArc(1.2,6);
        var lower=original[2].Parameter+.1*(original[3].Parameter-original[2].Parameter);var upper=original[2].Parameter+.8*(original[3].Parameter-original[2].Parameter);
        double[] stations=[lower,upper];
        var points=Evaluate(surfaces,original,stations);
        var good=Certify(surfaces,original,Chart(points,Tangents(points),lower,upper),stations,out var report);
        Assert.Equal(AlgorithmStatus.Success,good);Assert.True(report.Boxes>=2);
        // Same supports, but the end station is a different point of the circle.
        var shifted=(KernelVector3[])points.Clone();shifted[1]=Circle(Math.Atan2(points[1].Y,points[1].X)+1e-4);
        Assert.Equal(AlgorithmStatus.NotConverged,Certify(surfaces,original,Chart(shifted,Tangents(shifted),lower,upper),stations,out _));
    }

    [Fact]
    public void CompactChartOnDifferentSupportsIsRejected()
    {
        var surfaces=Supports().Concat([new BlendSurfaceInstruction{Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1+1e-6),NormalSense=1}]).ToArray();
        var original=OriginalArc(1.2,12);
        double[] stations=[original[1].Parameter,original[7].Parameter];
        var points=Evaluate(surfaces,original,stations);
        Assert.Equal(AlgorithmStatus.Unsupported,Certify(surfaces,original,Chart(points,Tangents(points),stations[0],stations[1]),stations,out _,0,2));
    }

    [Fact]
    public void TwoStationChartBetweenTheEndsOfALongArcTakesTheOtherBranchAndIsRejected()
    {
        // The original runs the long way, 3π/2, from angle 0 to -π/2. A
        // two-station CHART between the same end points follows the short
        // arc between them: same ends, different finite branch.
        var surfaces=Supports();var original=OriginalArc(1.5*Math.PI,36);
        double[] stations=[original[0].Parameter,original[^1].Parameter];
        var points=Evaluate(surfaces,original,stations);
        var tangents=Tangents(points);
        // The short-arc chart is traversed clockwise.
        for(var i=0;i<tangents.Length;i++)tangents[i]=new(){X=-tangents[i].X,Y=-tangents[i].Y};
        Assert.Equal(AlgorithmStatus.NotConverged,Certify(surfaces,original,Chart(points,tangents,stations[0],stations[1]),stations,out _));
    }

    [Fact]
    public void ReversedCompactSenseIsRejected()
    {
        var surfaces=Supports();var original=OriginalArc(1.2,12);
        double[] stations=[original[2].Parameter,original[6].Parameter,original[10].Parameter];
        var points=Evaluate(surfaces,original,stations);Array.Reverse(points);
        var tangents=Tangents(points);for(var i=0;i<tangents.Length;i++)tangents[i]=new(){X=-tangents[i].X,Y=-tangents[i].Y};
        Assert.Equal(AlgorithmStatus.NotConverged,Certify(surfaces,original,Chart(points,tangents,stations[0],stations[^1]),stations,out _));
    }

    [Fact]
    public void WrongFiniteTrimIsRejected()
    {
        var surfaces=Supports();var original=OriginalArc(1.2,12);
        double[] stations=[original[2].Parameter,original[9].Parameter];
        var points=Evaluate(surfaces,original,stations);var compact=Chart(points,Tangents(points),stations[0],stations[1]);
        // The compact CHART covers [2,9]; the requested trim is [2,10].
        double[] longer=[original[2].Parameter,original[10].Parameter];
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartOffset=0,ChartCount=original.Length},new(){First=0,Second=1,ChartOffset=original.Length,ChartCount=compact.Length,ChordalError=Declared(compact,out var angular),AngularError=angular}];
        var program=new NestedBlendProgram(surfaces,spines,original.Concat(compact).ToArray());
        Assert.Equal(AlgorithmStatus.InvalidInput,JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,longer,longer[0],longer[1],Link,out _));
        // Stations shifted inside the original, compact positions unchanged.
        double[] shifted=[original[3].Parameter,original[9].Parameter];
        Assert.Equal(AlgorithmStatus.InvalidInput,JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,shifted,shifted[0],shifted[1],Link,out _));
    }

    [Fact]
    public void TangentialSupportsHaveNoCertificate()
    {
        // A plane tangent to the cylinder along a line: the intersection is
        // singular, and the Krawczyk test cannot isolate a regular root.
        BlendSurfaceInstruction[] surfaces=
        [
            new(){Analytic=new(SurfaceClass.Plane,new(){X=1},new(){X=1},new(){Y=1}),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1),NormalSense=1},
        ];
        BlendChartInstruction[] original=
        [
            new(){Position=new(){X=1},Chord=new(){Z=1},Parameter=0,Scale=1},
            new(){Position=new(){X=1,Z=1},Chord=new(){Z=1},Parameter=1,Scale=1},
        ];
        BlendChartInstruction[] compact=
        [
            new(){Position=new(){X=1,Z=.25},Chord=new(){Z=1},Parameter=.25,Scale=1},
            new(){Position=new(){X=1,Z=.75},Chord=new(){Z=1},Parameter=.75,Scale=1},
        ];
        Assert.Equal(AlgorithmStatus.NotConverged,Certify(surfaces,original,compact,[.25,.75],out _));
    }

    [Theory]
    [InlineData(.01)]
    [InlineData(.001)]
    public void CompactEndpointOffsetAlongALowCosineChordIsRejected(double epsilon)
    {
        // Independent review R2a: a half-circle chord meets the arc at angle
        // ~π/2-ε, so a 1e-11 gauge offset is a 1e-11/ε spatial end offset.
        var surfaces=Supports();var start=-Math.PI/2+epsilon;var end=Math.PI/2-epsilon;
        var original=ArcChart(start,end,64,0,1);var upper=original[^1].Parameter;
        var shift=1e-11/epsilon;
        var compact=ArcChart(start,end+shift,1,0,upper/(2*Math.Sin((end+shift-start)/2)));
        compact[^1].Parameter=upper;
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=original.Length},
            new(){First=0,Second=1,ChartOffset=original.Length,ChartCount=2,ChordalError=4,AngularError=Math.PI}];
        var program=new NestedBlendProgram(surfaces,spines,original.Concat(compact).ToArray());
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,[0.0,upper],0,upper,Link,out var report));
        Assert.True(report.LinkDeviation>=.9*shift,$"link={report.LinkDeviation}");
        // Without the offset the same chord is certified.
        var exact=ArcChart(start,end,1,0,upper/(2*Math.Sin((end-start)/2)));exact[^1].Parameter=upper;
        program=new NestedBlendProgram(surfaces,spines,original.Concat(exact).ToArray());
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,[0.0,upper],0,upper,Link,out report));
        Assert.True(report.LinkDeviation<=Link);
    }

    [Theory]
    [InlineData(1e12,3)]
    [InlineData(1e12,1)]
    [InlineData(-1e12,3)]
    [InlineData(1e-300,5)]
    [InlineData(.5,7)]
    public void MeanValueRadiusReachesBothEndsOfOddUlpBoxes(double lower,int ulps)
    {
        // Independent review R2b: half of the rounded width does not reach
        // the farther end from a rounded midpoint.
        var upper=lower;for(var i=0;i<ulps;i++)upper=Math.BitIncrement(upper);
        var centre=lower+(upper-lower)*.5;
        // Sterbenz: differences of these same-sign values within a factor of
        // two are exact, so the comparison below is exact.
        var radius=JointNestedBlendEvaluation.MeanValueRadius(lower,centre,upper);
        Assert.True(radius>=centre-lower,$"lower {radius} {centre-lower}");
        Assert.True(radius>=upper-centre,$"upper {radius} {upper-centre}");
        if(lower==1e12&&ulps==3)Assert.True(radius>=0.000244140625);
    }

    [Fact]
    public void CosineUpperBoundsCosineAndIsTightAtProductionAngles()
    {
        for(var i=0;i<=4000;i++)
        {
            var x=Math.PI*i/4000;
            Assert.True(JointNestedBlendEvaluation.CosineUpper(x)>=Math.Cos(x),$"x={x}");
        }
        Assert.Equal(1,JointNestedBlendEvaluation.CosineUpper(0));
        foreach(var x in new[]{1e-6,.0087,.3368,.4})Assert.True(JointNestedBlendEvaluation.CosineUpper(x)-Math.Cos(x)<1e-10,$"x={x}");
    }

    [Fact]
    public void ChordBoxBoundUsesTheTrueLineDistanceForANonUnitChord()
    {
        // Point (1,1,0) with zero drift, chord along x scaled by (1+1e-9):
        // distance from the line is exactly 1, cosine of D=(1,0,0) is 1.
        Span<IntervalNumber> centre=[1,1,0,0,0,0,0,0,0];
        Span<IntervalNumber> box=[1,1,0,1,0,0,0,0,0];
        var cell=new BlendChartInstruction{Chord=new(){X=1+1e-9}};
        var along=box[3]*new IntervalNumber(cell.Chord.X);
        var bound=JointNestedBlendEvaluation.MeasureChordBox(centre,box,along,cell,.5);
        Assert.True(bound.Distance>=1&&bound.Distance<1+1e-14,$"d={bound.Distance}");
        Assert.True(bound.CosineLower<=1&&bound.CosineLower>1-1e-14,$"c={bound.CosineLower}");
        // A derivative off the chord adds drift times the radius.
        box[4]=new IntervalNumber(-.25,.25);
        bound=JointNestedBlendEvaluation.MeasureChordBox(centre,box,along,cell,.5);
        Assert.True(bound.Distance>=1.125,$"d={bound.Distance}");
        Assert.True(bound.CosineLower<=1/Math.Sqrt(1.0625),$"c={bound.CosineLower}");
    }

    [Fact]
    public void AngularErrorAtTheExactHalfTurnBoundary()
    {
        // Unit arc of 0.6 rad: the tangent-chord angle peaks at exactly 0.3.
        var surfaces=Supports();var original=OriginalArc(1.2,24);
        double[] stations=[original[2].Parameter,original[14].Parameter];
        var points=Evaluate(surfaces,original,stations);var compact=Chart(points,Tangents(points),stations[0],stations[1]);
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartOffset=0,ChartCount=original.Length},new(){First=0,Second=1,ChartOffset=original.Length,ChartCount=compact.Length}];
        var program=new NestedBlendProgram(surfaces,spines,original.Concat(compact).ToArray());
        var chordal=Declared(compact,out _);
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,stations,stations[0],stations[1],Link,chordal,.3*(1-1e-9),out _));
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,stations,stations[0],stations[1],Link,chordal,.3*(1+1e-6),out var report));
        Assert.True(report.AngularBound>=.3-1e-12);
    }

    private static BlendChartInstruction[] ArcChart(double start,double end,int cells,double lower,double scale)
    {
        var points=new KernelVector3[cells+1];var tangents=new KernelVector3[cells+1];
        for(var i=0;i<=cells;i++){var a=start+(end-start)*i/cells;points[i]=Circle(a);tangents[i]=Tangent(a);}
        var parameters=new double[cells+1];var scales=new double[cells];var chords=new KernelVector3[cells];
        Assert.Equal(AlgorithmStatus.Success,OriginalChartParameterMap.Build(points,tangents,lower,scale,parameters,scales,chords,out _,out _));
        var result=new BlendChartInstruction[cells+1];
        for(var i=0;i<=cells;i++)result[i]=new(){Position=points[i],Parameter=parameters[i],Scale=i<cells?scales[i]:1,Chord=i<cells?chords[i]:default};
        return result;
    }

    [Theory]
    [InlineData(-.3,1.0,Link)]
    [InlineData(double.NaN,1.0,Link)]
    [InlineData(4.0,1.0,Link)]
    [InlineData(.5,-1e-3,Link)]
    [InlineData(.5,double.PositiveInfinity,Link)]
    [InlineData(.5,double.NaN,Link)]
    [InlineData(.5,1.0,-1e-10)]
    [InlineData(.5,1.0,double.PositiveInfinity)]
    public void InvalidBudgetsAreRejectedAtEntry(double angular,double chordal,double link)
    {
        var surfaces=Supports();var original=OriginalArc(1.2,24);
        double[] stations=[original[2].Parameter,original[14].Parameter];
        var points=Evaluate(surfaces,original,stations);var compact=Chart(points,Tangents(points),stations[0],stations[1]);
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartOffset=0,ChartCount=original.Length},new(){First=0,Second=1,ChartOffset=original.Length,ChartCount=compact.Length}];
        var program=new NestedBlendProgram(surfaces,spines,original.Concat(compact).ToArray());
        Assert.Equal(AlgorithmStatus.InvalidInput,JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,stations,stations[0],stations[1],link,chordal,angular,out _));
        // The same certificate with valid budgets succeeds, and the full π
        // angular budget is a valid boundary value.
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,stations,stations[0],stations[1],Link,1.0,Math.PI,out _));
    }
}

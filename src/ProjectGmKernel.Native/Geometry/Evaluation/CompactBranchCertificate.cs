using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Result of a finite compact-branch certificate. Bounds are
/// rigorous interval upper bounds, not sampled estimates.</summary>
internal struct CompactBranchReport
{
    /// <summary>Largest distance of the original branch from a compact chord.</summary>
    internal double ChordalBound;
    /// <summary>Largest angle between the original tangent and a compact chord.</summary>
    internal double AngularBound;
    /// <summary>Upper bound of the spatial distance between the original root
    /// at a station and the compact root at its station parameter: the spatial
    /// diameter of the compact uniqueness box holding both.</summary>
    internal double LinkDeviation;
    internal BufferCount Boxes;
    internal BufferOffset FailedCell;
    /// <summary>1: original cover, 2: compact cover, 3: start link, 4: end link;
    /// plus 10 when a link fails on the compact root box.</summary>
    internal byte FailedStage;
    internal double FailedLower,FailedUpper;
}

internal static partial class JointNestedBlendEvaluation
{
    private const BufferCount CompactBranchBoxBudget=262144,CompactBranchDepth=40;

    /// <summary>
    /// Certify that a chart spine <paramref name="compact"/>, whose stations
    /// are the original parameters <paramref name="stations"/>, describes the
    /// same finite branch as <paramref name="original"/> on [lower, upper].
    /// Both spines must use the same compiled support equations. For each
    /// compact cell k, with unit chord e and gauge g(x)=(x-P_k)·e:
    /// 1. Interval Krawczyk boxes cover the closed original interval
    ///    [t_k,t_k+1]. Each proves, for every t in it, a unique regular root
    ///    of the full nested system, and encloses its derivative D with
    ///    D·e&gt;0. They also bound the chord distance and tangent angle by
    ///    the compact spine's declared chordal and angular errors.
    /// 2. The same covers the closed compact interval [p_k,p_k+1]; there g is
    ///    affine in p, so the compact arc is also strictly increasing in g.
    /// 3. At t_k and t_k+1 the complete original root state lies inside a
    ///    compact Krawczyk uniqueness box, on the same nested CHART cells,
    ///    over a parameter interval containing both that root's compact gauge
    ///    level p* and the station parameter. So the original end state is
    ///    exactly the compact root at p*, and the compact root at the station
    ///    is within the box's spatial diameter of it. That diameter, rounded
    ///    up, must not exceed the link tolerance.
    /// Proof of equality: parametrize both arcs by the level s=g. The set of
    /// levels between the two p* where they agree contains both ends and is
    /// closed. At such a level the solution set is regular with D·e&gt;0, so
    /// by the implicit function theorem it meets the nearby level planes in
    /// exactly one point near the common state; both arcs are continuous in
    /// s, so they agree on a neighbourhood. The agreement set is open and
    /// closed in a connected interval: the arcs are the same branch, with the
    /// same sense, and the published ends are within the link bound of the
    /// original ends. Since D·e&gt;0 holds on closed boxes including the
    /// ends, a complementary arc of a closed component cannot qualify.
    /// Scalar bounds (mean-value radius, square roots, arccosine) are rounded
    /// outward. Any uncertain, singular or unbounded box, or an exhausted
    /// depth/box budget, rejects the
    /// certificate. No conclusion is drawn from samples.
    /// </summary>
    internal static AlgorithmStatus CertifyCompactBranch(in NestedBlendProgram program,BlendSpineIndex original,BlendSpineIndex compact,
        ReadOnlySpan<double> stations,double lower,double upper,double linkTolerance,out CompactBranchReport report)
    {
        var definition=program.Spines[compact];
        return CertifyCompactBranch(program,original,compact,stations,lower,upper,linkTolerance,definition.ChordalError,definition.AngularError,out report);
    }

    /// <summary>As above, also proving that the original branch lies within
    /// <paramref name="chordalLimit"/> of each compact chord and that its
    /// tangent makes at most <paramref name="angularLimit"/> with that chord.</summary>
    internal static AlgorithmStatus CertifyCompactBranch(in NestedBlendProgram program,BlendSpineIndex original,BlendSpineIndex compact,
        ReadOnlySpan<double> stations,double lower,double upper,double linkTolerance,double chordalLimit,double angularLimit,out CompactBranchReport report)
    {
        report=default;report.FailedCell=-1;
        if(original<0||original>=program.Spines.Length||compact<0||compact>=program.Spines.Length||original==compact)return AlgorithmStatus.InvalidInput;
        var a=program.Spines[original];var b=program.Spines[compact];
        if(SpineParameterMap.Explicit(a)||SpineParameterMap.Explicit(b))return AlgorithmStatus.Unsupported;
        if(a.First!=b.First||a.Second!=b.Second)return AlgorithmStatus.Unsupported;
        var originalCharts=Chart(program,original);var compactCharts=Chart(program,compact);
        if(compactCharts.Length<2||stations.Length!=compactCharts.Length)return AlgorithmStatus.InvalidInput;
        if(!double.IsFinite(lower)||!double.IsFinite(upper)||!(upper>lower)||!(linkTolerance>=0))return AlgorithmStatus.InvalidInput;
        if(stations[0]!=lower||stations[^1]!=upper||compactCharts[0].Parameter!=lower||!(compactCharts[^1].Parameter>=upper))return AlgorithmStatus.InvalidInput;
        if(lower<originalCharts[0].Parameter||upper>originalCharts[^1].Parameter)return AlgorithmStatus.InvalidInput;
        for(var i=0;i<stations.Length-1;i++)
            if(!(stations[i+1]>stations[i])||!(compactCharts[i+1].Parameter>compactCharts[i].Parameter))return AlgorithmStatus.InvalidInput;
        Span<BufferCount> budget=stackalloc BufferCount[1];
        for(var k=0;k<compactCharts.Length-1;k++)
        {
            report.FailedCell=k;
            var cell=compactCharts[k];var chord=cell.Chord;
            var length=Math.Sqrt(chord.X*chord.X+chord.Y*chord.Y+chord.Z*chord.Z);
            if(!(Math.Abs(length-1)<=1e-12)||!(cell.Scale>0)||!double.IsFinite(cell.Scale))return AlgorithmStatus.InvalidInput;
            report.FailedStage=1;
            var status=CoverMonotoneBranch(program,original,stations[k],stations[k+1],cell,true,chordalLimit,angularLimit,budget,ref report);
            if(status!=AlgorithmStatus.Success)return status;
            report.FailedStage=2;
            status=CoverMonotoneBranch(program,compact,cell.Parameter,compactCharts[k+1].Parameter,cell,false,chordalLimit,angularLimit,budget,ref report);
            if(status!=AlgorithmStatus.Success)return status;
            report.FailedStage=3;
            status=LinkCompactRoot(program,original,compact,k,stations[k],k==0?lower:cell.Parameter,ref report);
            if(status!=AlgorithmStatus.Success)return status;
            report.FailedStage=4;
            status=LinkCompactRoot(program,original,compact,k,stations[k+1],k==compactCharts.Length-2?upper:compactCharts[k+1].Parameter,ref report);
            if(status!=AlgorithmStatus.Success)return status;
        }
        report.FailedCell=-1;report.FailedStage=0;
        return report.LinkDeviation<=linkTolerance?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus CoverMonotoneBranch(in NestedBlendProgram program,BlendSpineIndex spine,double first,double last,
        in BlendChartInstruction cell,bool measure,double chordalLimit,double angularLimit,Span<BufferCount> budget,ref CompactBranchReport report)
    {
        Span<IntervalNumber> centre=stackalloc IntervalNumber[9];
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[CompactBranchDepth+2];Span<BufferCount> depth=stackalloc BufferCount[CompactBranchDepth+2];
        Span<IntervalNumber> output=stackalloc IntervalNumber[9];
        var e0=new IntervalNumber(cell.Chord.X);var e1=new IntervalNumber(cell.Chord.Y);var e2=new IntervalNumber(cell.Chord.Z);
        pending[0]=new(first,last,0,0);depth[0]=0;var count=1;
        while(count>0)
        {
            var at=--count;var box=pending[at];var level=depth[at];
            if(++budget[0]>CompactBranchBoxBudget){report.FailedLower=box.U0;report.FailedUpper=box.U1;return AlgorithmStatus.NotConverged;}
            var status=BoundGeometry(program,-1,spine,box,output);
            var accepted=false;
            if(status==AlgorithmStatus.Success)
            {
                var along=output[3]*e0+output[4]*e1+output[5]*e2;
                if(along.Finite&&along.Lower>0)
                {
                    if(!measure)accepted=true;
                    else
                    {
                        // Mean-value bounds over the whole box: the root at
                        // its midpoint, plus the derivative enclosure times
                        // half its width. Refine until the declared CHART
                        // chordal and angular errors are proved.
                        var midpoint=box.U0+(box.U1-box.U0)*.5;
                        var found=BoundGeometry(program,-1,spine,new(midpoint,midpoint,0,0),centre);
                        if(found==AlgorithmStatus.Success)
                        {
                            // Radius from the rounded midpoint to the farther
                            // end, rounded up: the box is not symmetric about it.
                            var half=MeanValueRadius(box.U0,midpoint,box.U1);
                            var bound=MeasureChordBox(centre,output,along,cell,half);
                            var distance=bound.Distance;var cosine=bound.CosineLower;
                            var angleAccepted=angularLimit>=Math.PI||cosine>=CosineUpper(angularLimit);
                            // Reported angle only; acceptance is the cosine test.
                            var angle=Math.Acos(Math.Clamp(cosine,-1,1));
                            if(double.IsFinite(distance)&&distance<=chordalLimit&&angleAccepted)
                            {
                                accepted=true;
                                report.ChordalBound=Math.Max(report.ChordalBound,distance);
                                report.AngularBound=Math.Max(report.AngularBound,angle);
                            }
                        }
                        else if(found!=AlgorithmStatus.NotConverged)return found;
                    }
                    if(accepted)report.Boxes++;
                }
            }
            else if(status!=AlgorithmStatus.NotConverged)return status;
            if(accepted)continue;
            if(level>=CompactBranchDepth||count+2>pending.Length){report.FailedLower=box.U0;report.FailedUpper=box.U1;return AlgorithmStatus.NotConverged;}
            var middle=box.U0+(box.U1-box.U0)*.5;if(!(middle>box.U0&&middle<box.U1)){report.FailedLower=box.U0;report.FailedUpper=box.U1;return AlgorithmStatus.NotConverged;}
            pending[count]=new(middle,box.U1,0,0);depth[count++]=level+1;
            pending[count]=new(box.U0,middle,0,0);depth[count++]=level+1;
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus LinkCompactRoot(in NestedBlendProgram program,BlendSpineIndex original,BlendSpineIndex compact,BufferOffset cell,
        double station,double reference,ref CompactBranchReport report)
    {
        var originalCharts=Chart(program,original);var compactCharts=Chart(program,compact);
        var originalCell=0;while(originalCell<originalCharts.Length-2&&originalCharts[originalCell+1].Parameter<=station)originalCell++;
        var n=RootStateCount(program,original,station,out var occurrences);if(n<=0)return AlgorithmStatus.NotConverged;
        if(RootStateCount(program,compact,reference,out var compactOccurrences)!=n||compactOccurrences!=occurrences)return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> witness=stackalloc IntervalNumber[n];Span<IntervalNumber> output=stackalloc IntervalNumber[9];
        Span<BufferOffset> charts=stackalloc BufferOffset[2*occurrences];
        var status=BoundSegment(program,-1,original,new(station,station,0,0),output,captureWitness:witness,rootChart:originalCell,captureCharts:charts);
        if(status!=AlgorithmStatus.Success){report.FailedLower=report.FailedUpper=station;return status==AlgorithmStatus.Unsupported?status:AlgorithmStatus.NotConverged;}
        // Gauge level of the original root on this compact cell.
        var c=compactCharts[cell];
        var level=new IntervalNumber(c.Parameter)+((output[0]-c.Position.X)*c.Chord.X+(output[1]-c.Position.Y)*c.Chord.Y+(output[2]-c.Position.Z)*c.Chord.Z)*c.Scale;
        if(!level.Finite)return AlgorithmStatus.NotConverged;
        var lo=Math.BitDecrement(Math.Min(level.Lower,reference));var hi=Math.BitIncrement(Math.Max(level.Upper,reference));
        status=BoundSegment(program,-1,compact,new(lo,hi,0,0),output,requireWitness:witness,rootChart:cell,requireCharts:charts);
        if(status!=AlgorithmStatus.Success){report.FailedStage+=10;report.FailedLower=lo;report.FailedUpper=hi;return status==AlgorithmStatus.Unsupported?status:AlgorithmStatus.NotConverged;}
        // The box holds the unique compact root for every parameter in
        // [lo,hi], including the reference station, and the original root.
        // Its spatial diameter bounds their distance.
        var diameter=IntervalNumber.Square(output[0].Upper-output[0])+IntervalNumber.Square(output[1].Upper-output[1])+IntervalNumber.Square(output[2].Upper-output[2]);
        if(!diameter.Finite)return AlgorithmStatus.NotConverged;
        report.LinkDeviation=Math.Max(report.LinkDeviation,UpperSqrt(diameter.Upper));
        return AlgorithmStatus.Success;
    }

    // Upper bound of the distance from a rounded centre to both box ends.
    // Each difference is rounded to nearest (error at most half an ulp of the
    // result); stepping up one ulp gives an upper bound.
    internal static double MeanValueRadius(double lower,double centre,double upper)=>
        Math.BitIncrement(Math.Max(centre-lower,upper-centre));

    internal readonly struct ChordBoxBound(double distance,double cosineLower)
    {
        /// <summary>Upper bound of the distance from the chord line.</summary>
        internal readonly double Distance=distance;
        /// <summary>Lower bound of the cosine between tangent and chord.</summary>
        internal readonly double CosineLower=cosineLower;
    }

    /// <summary>
    /// Mean-value bounds on one parameter box: <paramref name="centre"/>
    /// encloses the root at a centre parameter, <paramref name="box"/>
    /// encloses root and derivative D over the box, <paramref name="along"/>
    /// encloses D·e and <paramref name="radius"/> bounds the parameter distance
    /// to the centre. The chord need not be exactly unit. All operations are
    /// outward interval arithmetic or explicit upward/downward steps.
    /// </summary>
    internal static ChordBoxBound MeasureChordBox(ReadOnlySpan<IntervalNumber> centre,ReadOnlySpan<IntervalNumber> box,IntervalNumber along,
        in BlendChartInstruction cell,double radius)
    {
        var e0=new IntervalNumber(cell.Chord.X);var e1=new IntervalNumber(cell.Chord.Y);var e2=new IntervalNumber(cell.Chord.Z);
        var ee=IntervalNumber.Square(e0)+IntervalNumber.Square(e1)+IntervalNumber.Square(e2);
        if(!ee.Finite||!(ee.Lower>0))return new(double.PositiveInfinity,-1);
        var d0=centre[0]-cell.Position.X;var d1=centre[1]-cell.Position.Y;var d2=centre[2]-cell.Position.Z;
        var s=(d0*e0+d1*e1+d2*e2)/ee;
        var q0=d0-s*e0;var q1=d1-s*e1;var q2=d2-s*e2;
        var a=along/ee;
        var w0=box[3]-a*e0;var w1=box[4]-a*e1;var w2=box[5]-a*e2;
        var offset=IntervalNumber.Square(q0)+IntervalNumber.Square(q1)+IntervalNumber.Square(q2);
        var drift=(IntervalNumber.Square(w0)+IntervalNumber.Square(w1)+IntervalNumber.Square(w2))*IntervalNumber.Square(new IntervalNumber(radius));
        if(!offset.Finite||!drift.Finite)return new(double.PositiveInfinity,-1);
        // Sum of two upper bounds, rounded to nearest, then one ulp up.
        var distance=Math.BitIncrement(UpperSqrt(offset.Upper)+UpperSqrt(drift.Upper));
        var speed=IntervalNumber.Square(box[3])+IntervalNumber.Square(box[4])+IntervalNumber.Square(box[5]);
        if(!speed.Finite||!(along.Lower>0))return new(distance,-1);
        // cos(D,e) = D·e/(|D||e|) >= along.Lower/(upper|D| upper|e|).
        var denominator=new IntervalNumber(UpperSqrt(speed.Upper))*new IntervalNumber(UpperSqrt(ee.Upper));
        var cosine=(new IntervalNumber(along.Lower)/new IntervalNumber(denominator.Upper)).Lower;
        return new(distance,cosine);
    }

    /// <summary>
    /// Upper bound of cos(x): the Taylor sum through x^8. For every real x,
    /// a partial sum ending with a positive term bounds cos from above
    /// (repeated integration of cos t &lt;= 1). Evaluated in outward intervals.
    /// </summary>
    internal static double CosineUpper(double x)
    {
        var x2=IntervalNumber.Square(new IntervalNumber(x));
        var sum=new IntervalNumber(1)-x2/2+IntervalNumber.Square(x2)/24-IntervalNumber.Square(x2)*x2/720+IntervalNumber.Square(IntervalNumber.Square(x2))/40320;
        return Math.Min(1,sum.Upper);
    }

    // IEEE 754 square root is correctly rounded; one step up bounds it.
    private static double UpperSqrt(double value)=>value<=0?0:Math.BitIncrement(Math.Sqrt(value));

    private static BufferCount RootStateCount(in NestedBlendProgram program,BlendSpineIndex spine,double parameter,out BufferCount occurrenceCount)
    {
        occurrenceCount=0;
        Span<TaylorJet> snapshot=stackalloc TaylorJet[MaxVariables];Span<Occurrence> occurrences=stackalloc Occurrence[MaxOccurrences];Span<BufferCount> counts=stackalloc BufferCount[2];
        Span<KernelVector3> point=stackalloc KernelVector3[2];SurfaceDerivativeLayout.TryCreate(1,0,out var layout);
        var status=EvaluateCore(program,spine,-1,parameter,0,layout,point,false,default,out _,out _,snapshot:snapshot,nodeSnapshot:occurrences,counts:counts);
        if(status!=AlgorithmStatus.Success)return -1;
        occurrenceCount=counts[1];return counts[0];
    }
}

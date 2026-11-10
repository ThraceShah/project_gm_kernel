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
    /// <summary>Largest spatial distance, along the compact chord, between a
    /// shared root and the compact parameter used at that station.</summary>
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
    ///    whose parameter interval contains that root's compact gauge level.
    ///    The two arcs therefore share both end states exactly.
    /// Proof of equality: parametrize both arcs by the level s=g. The set of
    /// levels where they agree contains both ends and is closed. At such a
    /// level the solution set is regular with D·e&gt;0, so by the implicit
    /// function theorem it meets the nearby level planes in exactly one point
    /// near the common state; both arcs are continuous in s, so they agree on
    /// a neighbourhood. The agreement set is open and closed in a connected
    /// interval: the arcs are the same branch, with the same sense. Since
    /// D·e&gt;0 holds on closed boxes including the ends, a complementary arc
    /// of a closed component cannot qualify. Any uncertain, singular or
    /// unbounded box, or an exhausted depth/box budget, rejects the
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
                            var half=new IntervalNumber(0,(box.U1-box.U0)*.5);
                            var d0=centre[0]-cell.Position.X;var d1=centre[1]-cell.Position.Y;var d2=centre[2]-cell.Position.Z;
                            var s=d0*e0+d1*e1+d2*e2;
                            var q0=d0-s*e0;var q1=d1-s*e1;var q2=d2-s*e2;
                            var w0=output[3]-along*e0;var w1=output[4]-along*e1;var w2=output[5]-along*e2;
                            var distance=Math.Sqrt((IntervalNumber.Square(q0)+IntervalNumber.Square(q1)+IntervalNumber.Square(q2)).Upper)
                                +Math.Sqrt((IntervalNumber.Square(w0)+IntervalNumber.Square(w1)+IntervalNumber.Square(w2)).Upper)*half.Upper;
                            var speed=Math.Sqrt((IntervalNumber.Square(output[3])+IntervalNumber.Square(output[4])+IntervalNumber.Square(output[5])).Upper);
                            var cosine=Math.Min(1,along.Lower/speed);var angle=cosine>0?Math.Acos(cosine):double.PositiveInfinity;
                            if(double.IsFinite(distance)&&distance<=chordalLimit&&angle<=angularLimit)
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
        report.LinkDeviation=Math.Max(report.LinkDeviation,Math.Max(reference-lo,hi-reference)/c.Scale);
        return AlgorithmStatus.Success;
    }

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

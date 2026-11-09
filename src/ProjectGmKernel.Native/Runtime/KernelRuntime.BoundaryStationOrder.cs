using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static readonly IntervalNumber UncertifiedStation=new(double.NaN,double.NaN);

    private static bool HasBoundedStation(in BoundaryParameter anchor)
        =>anchor.Surface!=0||anchor.SourceOccurrence>=0||anchor.Lower!=anchor.Upper;

    private static void ChooseClosedConicCutWindow(ref SheetCutCurve cut,ReadOnlySpan<IntervalNumber> roots)
    {
        if(cut.Closed==0||cut.Last-cut.First!=Math.Tau
            ||Curves[GetCurveSlotByTag(cut.Curve)].Class is not (CurveClass.Circle or CurveClass.Ellipse)
            ||Fits(cut.First,cut.Last,roots,strict:false))return;
        // A full analytic period has no physical endpoint. Pick a different
        // seam only if every certified enclosure has one lift inside it.
        // Partial finite uses still require their actual endpoint evidence.
        for(var i=1;i<=roots.Length;i++)
        {
            var first=cut.First+Math.Tau*(i/(roots.Length+1.0));var last=first+Math.Tau;
            if(!Fits(first,last,roots,strict:true))continue;
            cut.First=first;cut.Last=last;return;
        }

        static bool Fits(double first,double last,ReadOnlySpan<IntervalNumber> roots,bool strict)
        {
            var period=new IntervalNumber(Math.Tau,Math.BitIncrement(Math.Tau));
            foreach(var root in roots)
            {
                if(!root.Finite)continue;
                var lift=root;
                if(root.Upper<first||root.Lower>last)
                {
                    var candidates=(new IntervalNumber(first,last)-root)/period;
                    var turns=Math.Ceiling(candidates.Lower);
                    if(!candidates.Finite||turns!=Math.Floor(candidates.Upper))return false;
                    lift+=period*turns;
                }
                if(!lift.Finite||(strict?lift.Lower<=first||lift.Upper>=last:lift.Lower<first||lift.Upper>last))return false;
            }
            return true;
        }
    }

    private static int LocateBoundedStation(CurveTag carrier,BoundaryEventId endpoint,in BoundaryParameter anchor,
        double first,double last,BoundaryEventId lowerEvent,BoundaryEventId upperEvent,
        out double parameter,out IntervalNumber bound,out bool inside)
    {
        parameter=anchor.Parameter;bound=new(anchor.Lower,anchor.Upper);inside=false;
        var isEnd=endpoint>=0&&(endpoint==lowerEvent||endpoint==upperEvent);
        var kind=Curves[GetCurveSlotByTag(carrier)].Class;
        if((kind is CurveClass.Circle or CurveClass.Ellipse)&&(bound.Upper<first||bound.Lower>last))
        {
            // Use the declared finite lift, carrying its rounding into the
            // complete root interval. An in-range occurrence keeps its lift.
            var period=new IntervalNumber(Math.Tau,Math.BitIncrement(Math.Tau)); // Encloses exact 2*pi.
            var candidates=(new IntervalNumber(first,last)-bound)/period;
            if(!candidates.Finite)return UnresolvedBoundaryStation(carrier,endpoint,parameter,bound,first,new(first,last));
            var turns=Math.Ceiling(candidates.Lower);var maxTurns=Math.Floor(candidates.Upper);
            if(turns>maxTurns&&!isEnd)return 0;
            if(turns!=maxTurns)
                return UnresolvedBoundaryStation(carrier,endpoint,parameter,bound,first,new(first,last));
            bound+=period*turns;parameter=Math.FusedMultiplyAdd(Math.Tau,turns,parameter);
        }
        if(!bound.Finite)return UnresolvedBoundaryStation(carrier,endpoint,parameter,bound,first,new(first,last));
        if(isEnd)
        {
            var limit=endpoint==lowerEvent?first:last;
            if(limit<bound.Lower||limit>bound.Upper)
                return UnresolvedBoundaryStation(carrier,endpoint,parameter,bound,limit,new(limit));
            parameter=limit;bound=new(limit);inside=true;return 0;
        }
        if(bound.Upper<first||bound.Lower>last)return 0;
        if(bound.Lower<first||bound.Upper>last)
            return UnresolvedBoundaryStation(carrier,endpoint,parameter,bound,first,new(first,last));
        parameter=Math.Clamp(parameter,bound.Lower,bound.Upper);inside=true;return 0;
    }

    private static int SortBoundaryStations(CurveTag carrier,Span<double> parameters,Span<BoundaryEventId> events,Span<IntervalNumber> bounds)
    {
        // Representatives can suggest the order, but never settle the order
        // of two certified roots. Disjoint closed enclosures are required.
        for(var i=1;i<parameters.Length;i++)for(var j=i;j>0&&parameters[j]<parameters[j-1];j--)
        {
            (parameters[j],parameters[j-1])=(parameters[j-1],parameters[j]);
            (events[j],events[j-1])=(events[j-1],events[j]);
            (bounds[j],bounds[j-1])=(bounds[j-1],bounds[j]);
        }
        var previous=-1;
        for(var i=0;i<bounds.Length;i++)
        {
            if(!bounds[i].Finite)continue;
            if(previous>=0&&bounds[previous].Upper>=bounds[i].Lower)
                return UnresolvedBoundaryStation(carrier,events[i],parameters[i],bounds[i],parameters[previous],bounds[previous]);
            previous=i;
        }
        return 0;
    }

    internal static bool TryInteriorBoundaryStation(double first,double last,double chartFirst,double chartLast,bool wrap,
        double fraction,out double parameter)
    {
        if(!wrap)return TryOpenStation(first,last,fraction,out parameter);
        var tail=chartLast-first;var head=last-chartFirst;var offset=(tail+head)*fraction;
        if(offset<tail)
            return TryOpenStation(first,chartLast,offset/tail,out parameter)||TryOpenStation(chartFirst,last,fraction,out parameter);
        return TryOpenStation(chartFirst,last,head>0?(offset-tail)/head:0,out parameter)||TryOpenStation(first,chartLast,fraction,out parameter);

        static bool TryOpenStation(double a,double b,double f,out double at)
        {
            at=a+(b-a)*f;
            if(at>a&&at<b)return true;
            at=Math.BitIncrement(a);return at<b;
        }
    }

    private static int UnresolvedBoundaryStation(CurveTag carrier,BoundaryEventId endpoint,double parameter,IntervalNumber bound,
        double previous,IntervalNumber previousBound)
    {
        LastBoundaryParameterConflict=new(){Event=endpoint,Carrier=carrier,Previous=previous,PreviousLower=previousBound.Lower,
            PreviousUpper=previousBound.Upper,Incoming=parameter,Lower=bound.Lower,Upper=bound.Upper};
        LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;LastSheetCurve=carrier;LastSheetParameter=parameter;
        return ParasolidConstants.PK_ERROR_cant_do_intersect;
    }

#if KERNEL_TEST_HOOKS
    internal static int TestClosedConicCutWindow(CurveTag carrier,bool closed,ReadOnlySpan<IntervalNumber> roots,
        Span<IntervalNumber> lifted,out double first)
    {
        var cut=new SheetCutCurve{Curve=carrier,First=0,Last=Math.Tau,Closed=closed?(byte)1:(byte)0};
        ChooseClosedConicCutWindow(ref cut,roots);first=cut.First;
        for(var i=0;i<roots.Length;i++)
        {
            var anchor=new BoundaryParameter{Parameter=roots[i].Midpoint,Lower=roots[i].Lower,Upper=roots[i].Upper};
            var error=LocateBoundedStation(carrier,-1,anchor,cut.First,cut.Last,-1,-1,out _,out lifted[i],out var inside);
            if(error!=0)return error;
            if(!inside)return ParasolidConstants.PK_ERROR_not_on_curve;
        }
        return 0;
    }

    internal static int TestLocatePeriodicBoundaryStation(CurveTag carrier,IntervalNumber root,double first,double last,
        bool isLowerEvent,out double parameter,out IntervalNumber bound,out bool inside)
    {
        var anchor=new BoundaryParameter{Lower=root.Lower,Upper=root.Upper,Parameter=root.Midpoint};
        return LocateBoundedStation(carrier,2,anchor,first,last,isLowerEvent?2:-1,-1,out parameter,out bound,out inside);
    }
#endif
}

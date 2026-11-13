using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static readonly IntervalNumber UncertifiedStation=new(double.NaN,double.NaN);

    internal static void PrepareRegularSourceRingWindows(Span<ModelingEdge> edges,in RegularBoundaryView input)
    {
        var haveRing=false;
        foreach(ref readonly var edge in edges)haveRing|=edge.StartVertex<0&&edge.EndVertex<0&&edge.End-edge.Start==Math.Tau
            &&Curves[GetCurveSlotByTag(edge.Curve)].Class is CurveClass.Circle or CurveClass.Ellipse;
        if(!haveRing)return;
        var faces=input.Faces;
        Span<AnalyticSurface> planes=stackalloc AnalyticSurface[faces.Length];var planeCount=0;
        Span<BufferOffset> planeFaces=stackalloc BufferOffset[faces.Length];
        for(var f=0;f<faces.Length;f++)
            if(TryBoundaryPlane(GetSurfaceSlotByTag(faces[f].Surface),out var plane)){planeFaces[planeCount]=f;planes[planeCount++]=plane;}
        Span<IntervalNumber> coefficients=stackalloc IntervalNumber[3*planeCount];
        for(var e=0;e<edges.Length;e++)
        {
            ref var edge=ref edges[e];
            var curve=Curves[GetCurveSlotByTag(edge.Curve)];
            if(edge.StartVertex>=0||edge.EndVertex>=0||edge.End-edge.Start!=Math.Tau
                ||curve.Class is not (CurveClass.Circle or CurveClass.Ellipse)||!TryReadExplicitSpineCurve(curve,out var conic))continue;
            var equations=0;
            for(var p=0;p<planeCount;p++)
            {
                var face=faces[planeFaces[p]];var incident=false;
                for(var l=0;l<face.LoopCount;l++)
                {
                    var loop=input.Loops[face.FirstLoop+l];
                    foreach(ref readonly var use in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))incident|=use.Edge==e;
                }
                // Native incidence already defines the edge on this support;
                // it is not a crossing equation to solve again. This matters
                // after a rigid transform, where coefficient roundoff alone
                // need not prove an identically zero level expression.
                if(incident)continue;
                ConicPlaneCoefficients(conic,planes[p],out var x,out var y,out var h);
                if(x.Magnitude==0&&y.Magnitude==0&&h.Magnitude==0)continue;
                coefficients[3*equations]=x;coefficients[3*equations+1]=y;coefficients[3*equations+2]=h;equations++;
            }
            if(equations==0)continue;
            // Each noncoincident plane has at most two phases on one turn.
            // A finite grid supplies candidates only; interval level bounds
            // must prove the chosen seam free of every such equation.
            for(var trial=0;trial<=2*equations;trial++)
            {
                var first=edge.Start+Math.Tau*(trial/(2.0*equations+1));var last=first+Math.Tau;var clear=true;
                var seam=new IntervalNumber(first)+new IntervalNumber(Math.Tau,Math.BitIncrement(Math.Tau));
                seam=new(Math.Min(seam.Lower,last),Math.Max(seam.Upper,last));
                IntervalDual.SinCos(new(first,0),out var sin,out var cos);
                IntervalDual.SinCos(new(seam,0),out var endSin,out var endCos);
                for(var p=0;p<equations;p++)
                {
                    var x=coefficients[3*p];var y=coefficients[3*p+1];var h=coefficients[3*p+2];
                    if((x*cos.Value+y*sin.Value-h).ContainsZero||(x*endCos.Value+y*endSin.Value-h).ContainsZero){clear=false;break;}
                }
                if(!clear)continue;
                edge.Start=first;edge.End=last;break;
            }
        }
    }

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

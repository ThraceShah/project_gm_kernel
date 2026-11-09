using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool SameCertifiedLineCylinderPlaneRoot(in BoundaryParameter a,in BoundaryParameter b)
    {
        var carrier=GetCurveSlotByTag(a.Carrier);var first=GetSurfaceSlotByTag(a.Surface);var second=GetSurfaceSlotByTag(b.Surface);
        if(carrier<0||first<0||second<0||Curves[carrier].Class!=CurveClass.Line)return false;
        if(Surfaces[first].Class==SurfaceClass.Cylinder)(first,second)=(second,first);
        if(Surfaces[first].Class!=SurfaceClass.Plane||Surfaces[second].Class!=SurfaceClass.Cylinder
            ||!TryReadExplicitSpineCurve(Curves[carrier],out var line)||!TryPrepareSurface(Surfaces[first],out var plane)
            ||!TryPrepareSurface(Surfaces[second],out var cylinder)||!TryLinePlaneRatio(line,plane,out var n,out var d))return false;
        var overlap=IntersectStationBounds(n/d,new(Math.Max(a.Lower,b.Lower),Math.Min(a.Upper,b.Upper)));
        if(!overlap.Finite)return false;
        var origin=new IntervalVector(line.Origin);var axis=new IntervalVector(line.First);
        // Both equations already have an isolated root on this occurrence.
        // A representable station satisfying both exactly identifies that
        // root. The small search budget never turns overlap into equality.
        var station=overlap.Lower;
        for(var i=0;i<32&&station<=overlap.Upper;i++,station=Math.BitIncrement(station))
        {
            var point=origin+axis*station;
            if(ExactPointOnPlaneLevel(point,plane)&&ExactCylinderRadial(point,cylinder,0,out _))return true;
        }
        return false;
    }

    private static bool TryExactCoincidentLineRoots(in ModelingEdge a,in ModelingEdge b,
        Span<OperationIntersectionPoint> roots,out BufferCount count)
    {
        count=0;
        if(!double.IsFinite(a.Start)||!double.IsFinite(a.End)||a.Start>a.End
            ||!double.IsFinite(b.Start)||!double.IsFinite(b.End)||b.Start>b.End)return false;
        var ca=Curves[GetCurveSlotByTag(a.Curve)];var cb=Curves[GetCurveSlotByTag(b.Curve)];
        if(ca.Class!=CurveClass.Line||cb.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(ca,out var la)||!TryReadExplicitSpineCurve(cb,out var lb))
            return false;
        return TryExactFiniteLineRoots(la,a.Start,a.End,lb,b.Start,b.End,roots,out count);
    }

    private static BufferOffset FiniteLineBoundarySource(in RegularBoundaryView input,BufferOffset face,in SheetCutCurve cut)
    {
        if(Curves[GetCurveSlotByTag(cut.Curve)].Class!=CurveClass.Line)return -1;
        var domain=input.Faces[face];BufferOffset source=-1;
        for(var l=0;l<domain.LoopCount;l++)
        {
            var loop=input.Loops[domain.FirstLoop+l];
            foreach(var use in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
            {
                var edge=input.Edges[use.Edge];
                if(!BoundCoincidentLineStations(cut.Curve,edge.Curve,cut.First,cut.Last,out var first,out var last,out _)
                    ||Math.Min(first.Lower,last.Lower)<edge.Start||Math.Max(first.Upper,last.Upper)>edge.End)continue;
                if(source>=0&&source!=use.Edge)return -1;
                source=use.Edge;
            }
        }
        return source;
    }

    private static bool BoundCoincidentLineStations(CurveTag cut,CurveTag source,double first,double last,
        out IntervalNumber firstOnSource,out IntervalNumber lastOnSource,out bool increasing)
    {
        firstOnSource=lastOnSource=default;increasing=false;
        var a=Curves[GetCurveSlotByTag(cut)];var b=Curves[GetCurveSlotByTag(source)];
        if(a.Class!=CurveClass.Line||b.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(a,out var from)||!TryReadExplicitSpineCurve(b,out var to)
            ||!ExactParallelSupportAxes(from.First,to.First))return false;
        var axis=to.First;
        if(!ExactProjectionEqual(from.Origin,to.Origin,Vector(0,axis.Z,-axis.Y))
            ||!ExactProjectionEqual(from.Origin,to.Origin,Vector(-axis.Z,0,axis.X))
            ||!ExactProjectionEqual(from.Origin,to.Origin,Vector(axis.Y,-axis.X,0)))return false;
        // One nonzero component gives an exact affine station relation;
        // its two representatives need not be equal in floating point.
        var x=Math.Abs(axis.X);var y=Math.Abs(axis.Y);var z=Math.Abs(axis.Z);
        var coordinate=x>=y&&x>=z?0:y>=z?1:2;
        var d=Component(axis,coordinate);var q=Component(from.First,coordinate);
        if(d==0||q==0)return false;
        var shift=new IntervalNumber(Component(from.Origin,coordinate))-Component(to.Origin,coordinate);
        firstOnSource=At(first);lastOnSource=At(last);
        increasing=(d>0)==(q>0);
        return firstOnSource.Finite&&lastOnSource.Finite;

        IntervalNumber At(double t)
        {
            var numerator=shift+new IntervalNumber(q)*t;
            return d==-1?-numerator:numerator/d;
        }
        static double Component(in KernelVector3 p,BufferOffset i)=>i==0?p.X:i==1?p.Y:p.Z;
    }

    private static bool FindInheritedLineFragment(in SheetCutCurve cut,in SheetSourceEdge source,ReadOnlySpan<ModelingEdge> edges,
        double first,double last,BoundaryEventId lower,BoundaryEventId upper,out BufferOffset fragment)
    {
        fragment=-1;if(lower<0||upper<0||lower==upper)return false;
        for(var i=source.First;i<source.First+source.Count;i++)
        {
            ref readonly var edge=ref edges[i];
            if(edge.StartVertex!=lower&&edge.EndVertex!=lower||edge.StartVertex!=upper&&edge.EndVertex!=upper)continue;
            if(!BoundCoincidentLineStations(cut.Curve,edge.Curve,first,last,out var a,out var b,out var increasing))continue;
            var same=increasing==(LocalSenseSign(Curves[GetCurveSlotByTag(edge.Curve)].Sense)>0);
            var lo=increasing?edge.Start:edge.End;var hi=increasing?edge.End:edge.Start;
            if(lower!=(same?edge.StartVertex:edge.EndVertex)||upper!=(same?edge.EndVertex:edge.StartVertex)
                ||lo<a.Lower||lo>a.Upper||hi<b.Lower||hi>b.Upper)continue;
            if(fragment>=0)return false;fragment=i;
        }
        return fragment>=0;
    }

    private static bool TryExactFiniteLineRoots(in ParametricSpineCurve la,double aStart,double aEnd,
        in ParametricSpineCurve lb,double bStart,double bEnd,Span<OperationIntersectionPoint> roots,out BufferCount count)
    {
        count=0;
        var p=new IntervalVector(la.Origin);var q=new IntervalVector(lb.Origin);
        var d=new IntervalVector(la.First);var e=new IntervalVector(lb.First);
        var r=q-p;var cross=IntervalVector.Cross(d,e);
        if(!IsZero(cross))
        {
            if(!TryComponent(cross,out var component,out var divisor))return false;
            var t=Value(IntervalVector.Cross(r,e),component)/divisor;
            var u=Value(IntervalVector.Cross(r,d),component)/divisor;
            if(!t.Finite||!u.Finite)return false;
            if(t.Upper<aStart||t.Lower>aEnd||u.Upper<bStart||u.Lower>bEnd)return true;
            if(!Single(t)||!Single(u))return false;
            var x=p+d*t;var y=q+e*u;
            if(TryComponent(x-y,out _,out _))return true;
            if(!SetRoot(roots,0,t,u,x,y,IntersectionVectorKind.Simple))return false;
            count=1;return true;
        }
        var separation=IntervalVector.Cross(r,d);
        if(!IsZero(separation))return TryComponent(separation,out _,out _);
        if(!TryComponent(d,out var axis,out var first)||!Single(first))return false;
        var second=Value(e,axis);if(!Single(second)||second.ContainsZero)return false;
        var shift=Value(r,axis);
        var lo=(shift+second*bStart)/first;var hi=(shift+second*bEnd)/first;
        if(!Single(lo)||!Single(hi))return false;
        var lower=Math.Max(aStart,Math.Min(lo.Lower,hi.Lower));
        var upper=Math.Min(aEnd,Math.Max(lo.Lower,hi.Lower));
        if(upper<lower)return true;
        var ul=(first*lower-shift)/second;
        if(!SetRoot(roots,0,new(lower),ul,p+d*lower,q+e*ul,upper==lower?IntersectionVectorKind.Tangent:IntersectionVectorKind.Start))return false;
        if(upper==lower){count=1;return true;}
        var uh=(first*upper-shift)/second;
        if(!SetRoot(roots,1,new(upper),uh,p+d*upper,q+e*uh,IntersectionVectorKind.End))return false;
        count=2;return true;

        static bool Single(IntervalNumber value)=>value.Finite&&value.Lower==value.Upper;
        static IntervalNumber Value(in IntervalVector v,BufferOffset axis)=>axis==0?v.X.Value:axis==1?v.Y.Value:v.Z.Value;
        static bool IsZero(in IntervalVector v)=>v.X.Value.Magnitude==0&&v.Y.Value.Magnitude==0&&v.Z.Value.Magnitude==0;
        static bool TryComponent(in IntervalVector v,out BufferOffset axis,out IntervalNumber value)
        {
            for(axis=0;axis<3;axis++){value=Value(v,axis);if(value.Finite&&!value.ContainsZero)return true;}
            value=default;return false;
        }
        static bool SetRoot(Span<OperationIntersectionPoint> output,BufferOffset index,IntervalNumber t,IntervalNumber u,
            in IntervalVector x,in IntervalVector y,IntersectionVectorKind kind)
        {
            if(!Single(u)||!IsZero(x-y)||!Single(x.X.Value)||!Single(x.Y.Value)||!Single(x.Z.Value))return false;
            output[index]=new(){Point=Vector(x.X.Value.Lower,x.Y.Value.Lower,x.Z.Value.Lower),First=t.Lower,Second=u.Lower,Kind=kind};
            return true;
        }
    }

    private static int AssociateCoincidentLineRoot(in RegularBoundaryView input,scoped Span<RegularFaceCut> cuts,
        BufferOffset first,BufferOffset second,in OperationIntersectionPoint root,ref SheetArrangement model)
        =>AssociateCoincidentLineRoot(input,cuts,input.Edges[first],first,input.Edges[second],second,root,ref model);

    private static int AssociateCoincidentLineRoot(in RegularBoundaryView input,scoped Span<RegularFaceCut> cuts,
        in ModelingEdge a,BufferOffset first,in ModelingEdge b,BufferOffset second,in OperationIntersectionPoint root,ref SheetArrangement model)
    {
        LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;
        Span<BoundaryEventId> ends=stackalloc BoundaryEventId[2];
        ends[0]=NativeLineEnd(a,root.First);ends[1]=NativeLineEnd(b,root.Second);
        BoundaryEventId original=-1;
        foreach(var end in ends)
            if(end>=0&&model.OriginalVertices[end]>=0)
            {
                if(original>=0&&original!=end)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                original=end;
            }
        var error=JoinProvenBoundaryEndpoints(input,cuts,ends,root.Point,ref model,out var endpoint,original);
        if(error!=0)return error;
        for(var j=0;j<model.NP;j++)
        {
            if(!model.LiveBoundaryPoint(j)||j==endpoint)continue;
            if(!KnownLinePoint(ref model,j,a.Curve,first,root.First)&&!KnownLinePoint(ref model,j,b.Curve,second,root.Second))continue;
            if(!(ProfileDistance(model.Points[j],root.Point)<=model.Tolerance))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            if(endpoint<0){endpoint=j;continue;}
            ends[0]=endpoint;ends[1]=j;
            var joined=JoinProvenBoundaryEndpoints(input,cuts,ends,root.Point,ref model,out endpoint,original);
            if(joined!=0)return joined;
        }
        if(endpoint<0)
        {
            var added=model.AddPoint(root.Point,-1,out endpoint,requireProof:true);if(added!=0)return added;
        }
        else if(!(ProfileDistance(model.Points[endpoint],root.Point)<=model.Tolerance))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        error=model.AssociateParameter(endpoint,a.Curve,root.First,lineOccurrence:first);
        if(error!=0)return error;
        error=model.AssociateParameter(endpoint,b.Curve,root.Second,lineOccurrence:second);if(error!=0)return error;
        TraceCoincidentLineStation(endpoint,a.Curve,first,root.First);
        TraceCoincidentLineStation(endpoint,b.Curve,second,root.Second);
        return 0;

        static bool KnownLinePoint(ref SheetArrangement graph,BoundaryEventId point,CurveTag carrier,BufferOffset edge,double station)
            =>graph.KnownSourceBoundaryParameter(point,carrier,edge,out var known,out _)
                &&known.SourceOccurrence==edge&&known.Lower==station&&known.Upper==station;
    }

    private static void TraceCoincidentLineStation(BoundaryEventId endpoint,CurveTag carrier,BufferOffset occurrence,double station)
        =>TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.CoincidentLineStation,Curve=carrier,
            Generation=Curves[GetCurveSlotByTag(carrier)].Header.Generation,Edge=occurrence,
            StartEvent=endpoint,EndEvent=endpoint,First=station,Last=station});

    private static BoundaryEventId NativeLineEnd(in ModelingEdge edge,double station)
    {
        var forward=LocalSenseSign(Curves[GetCurveSlotByTag(edge.Curve)].Sense)>0;
        return station==edge.Start?(forward?edge.StartVertex:edge.EndVertex)
            :station==edge.End?(forward?edge.EndVertex:edge.StartVertex):-1;
    }

    private static ModelingEdge LineCutUse(in SheetCutCurve cut)
    {
        var forward=LocalSenseSign(Curves[GetCurveSlotByTag(cut.Curve)].Sense)>0;
        return new(){Curve=cut.Curve,Start=cut.First,End=cut.Last,
            StartVertex=(forward?cut.LowerEvent:cut.UpperEvent)-1,EndVertex=(forward?cut.UpperEvent:cut.LowerEvent)-1};
    }

    private static bool ExactCoincidentImprintStation(ref SheetArrangement model,BoundaryEventId point,CurveTag carrier,
        BufferOffset edge,double station)
    {
        var record=Curves[GetCurveSlotByTag(carrier)];
        if(record.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(record,out var line))return false;
        foreach(ref readonly var row in model.Parameters[..model.ParameterCount])
        {
            if(row.Event!=point||row.Carrier!=carrier||row.Generation!=record.Header.Generation
                ||row.LineOccurrence!=edge+1||row.Lower!=station||row.Upper!=station)continue;
            var delta=new IntervalVector(line.Origin)+new IntervalVector(line.First)*station-new IntervalVector(model.Points[point]);
            return delta.X.Value.Magnitude==0&&delta.Y.Value.Magnitude==0&&delta.Z.Value.Magnitude==0;
        }
        return false;
    }

    private static int ConnectCoincidentLineCuts(in RegularBoundaryView input,scoped Span<RegularFaceCut> cuts,ref SheetArrangement model)
    {
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[2];
        for(var a=0;a<cuts.Length;a++)
        for(var b=a+1;b<cuts.Length;b++)
        {
            ref readonly var first=ref cuts[a];ref readonly var second=ref cuts[b];
            if(first.Cut.KeepImprint==0&&second.Cut.KeepImprint==0
                ||first.First!=second.First&&first.First!=second.Second&&first.Second!=second.First&&first.Second!=second.Second)
                continue;
            if(!TryExactCoincidentLineRoots(LineCutUse(first.Cut),LineCutUse(second.Cut),roots,out var count)||count!=2)continue;
            // The positive interval occurs on the same face. Align both
            // native endpoints before halfedges and source fragments exist.
            for(var r=0;r<count;r++)
            {
                var error=AssociateCoincidentLineRoot(input,cuts,LineCutUse(first.Cut),RegularInputEdges+a,
                    LineCutUse(second.Cut),RegularInputEdges+b,roots[r],ref model);
                if(error!=0)return error;
            }
            MarkKnownLineEnds(ref cuts[a].Cut,ref model);MarkKnownLineEnds(ref cuts[b].Cut,ref model);
        }
        return 0;

        static void MarkKnownLineEnds(ref SheetCutCurve cut,ref SheetArrangement model)
        {
            if(HasCutEnd(ref model,cut.Curve,cut.LowerEvent-1,cut.First)
                &&HasCutEnd(ref model,cut.Curve,cut.UpperEvent-1,cut.Last))cut.NativeLocatorEnds=3;
        }
    }

    private static int ReadAcceptedThirdLineEnd(in RegularBoundaryView input,BufferOffset face,scoped Span<RegularFaceCut> cuts,
        BufferOffset current,ref SheetArrangement model,out bool joined)
    {
        joined=false;
        var cut=cuts[current].Cut;var support=input.Faces[face].Surface;
        if(!TryFiniteLinePlaneEvent(new(){Curve=cut.Curve,Start=cut.First,End=cut.Last},-1,support,out var proof,allowExactEnd:true)
            ||proof.Count!=1||proof.FirstRoot.NativeEnd==FiniteNativeEnd.None)return 0;
        var hit=proof.FirstRoot;
        var endpoint=(hit.NativeEnd==FiniteNativeEnd.First?cut.LowerEvent:cut.UpperEvent)-1;
        if(!HasCutEnd(ref model,cut.Curve,endpoint,hit.Parameter))return 0;
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[2];
        var domain=input.Faces[face];
        // With one transverse root on the whole line, only boundary uses
        // containing that exact station can supply incident trim witnesses.
        var pointUse=new ModelingEdge{Curve=cut.Curve,Start=hit.Parameter,End=hit.Parameter,StartVertex=endpoint,EndVertex=endpoint};
        for(var l=0;l<domain.LoopCount;l++)
        {
            var loop=input.Loops[domain.FirstLoop+l];
            for(var k=0;k<loop.CoedgeCount;k++)
            {
                var e=input.Coedges[loop.FirstCoedge+k].Edge;
                if(!TryExactCoincidentLineRoots(pointUse,input.Edges[e],roots,out var count)){joined=false;return 0;}
                if(count==0)continue;
                var error=AssociateCoincidentLineRoot(input,cuts,pointUse,RegularInputEdges+current,input.Edges[e],e,roots[0],ref model);
                if(error!=0)return error;
                endpoint=(hit.NativeEnd==FiniteNativeEnd.First?cuts[current].Cut.LowerEvent:cuts[current].Cut.UpperEvent)-1;
                pointUse.StartVertex=pointUse.EndVertex=endpoint;joined=true;
            }
        }
        return 0;
    }

    private static int ReuseImprintThirdLineRoot(in RegularBoundaryView input,scoped ReadOnlySpan<RegularFaceCut> cuts,BufferOffset current,
        SurfTag support,in OperationIntersectionPoint root,in ScalarRootIsolation isolation,ref SheetArrangement model,out bool joined)
    {
        joined=false;
        var cut=cuts[current].Cut;
        if(!isolation.Certified||!isolation.Root.Finite)return 0;
        BufferOffset source=-1;
        for(var i=0;i<input.Edges.Length;i++)
        {
            ref readonly var edge=ref input.Edges[i];
            if(edge.Curve!=cut.Curve||edge.Start!=cut.First||edge.End!=cut.Last)continue;
            if(source>=0)return 0;
            source=i;
        }
        if(source<0)return 0;
        var generation=Curves[GetCurveSlotByTag(cut.Curve)].Header.Generation;
        BoundaryEventId endpoint=-1;
        foreach(ref readonly var row in model.Parameters[..model.ParameterCount])
        {
            if(row.Carrier!=cut.Curve||row.Generation!=generation||row.SourceOccurrence!=source
                ||row.Witness==0||row.Surface!=support)continue;
            // Both providers solve the same transverse plane equation on
            // the same finite source line, which has at most one root.
            if(!IntersectStationBounds(isolation.Root,new(row.Lower,row.Upper)).Finite
                ||endpoint>=0&&endpoint!=row.Event||!(ProfileDistance(model.Points[row.Event],root.Point)<=model.Tolerance))
                return UnresolvedBoundaryStation(cut.Curve,row.Event,root.First,isolation.Root,row.Parameter,new(row.Lower,row.Upper));
            endpoint=row.Event;
        }
        if(endpoint<0)return 0;
        var error=model.AssociateParameter(endpoint,cut.Curve,root.First,isolation.Root.Lower,isolation.Root.Upper,
            lineOccurrence:RegularInputEdges+current);
        joined=error==0;return error;
    }

    private static int ReadCoplanarThirdLine(in RegularBoundaryView input,BufferOffset face,scoped Span<RegularFaceCut> cuts,
        BufferOffset current,in CurveRecord record,ref SheetArrangement model,out SurfacePairRelation relation)
    {
        relation=SurfacePairRelation.Unresolved;
        var cut=cuts[current].Cut;var line=LineCutUse(cut);
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[2];
        Span<double> stations=stackalloc double[SheetPointLimit];
        stations[0]=cut.First;stations[1]=cut.Last;var count=2;
        var boundaryContact=SurfacePairRelation.Empty;var domain=input.Faces[face];
        for(var l=0;l<domain.LoopCount;l++)
        {
            var loop=input.Loops[domain.FirstLoop+l];
            for(var k=0;k<loop.CoedgeCount;k++)
            {
                var edge=input.Coedges[loop.FirstCoedge+k].Edge;
                if(!TryExactCoincidentLineRoots(line,input.Edges[edge],roots,out var found))return ParasolidConstants.PK_ERROR_not_implemented;
                if(found==2)boundaryContact=SurfacePairRelation.CurveContact;
                else if(found==1&&boundaryContact==SurfacePairRelation.Empty)boundaryContact=SurfacePairRelation.PointContact;
                for(var r=0;r<found;r++)
                {
                    // Earlier roots may have reattached one of this cut's ends.
                    line=LineCutUse(cuts[current].Cut);
                    var error=AssociateCoincidentLineRoot(input,cuts,line,RegularInputEdges+current,input.Edges[edge],edge,roots[r],ref model);
                    if(error!=0)return error;
                    var station=roots[r].First;
                    if(stations[..count].Contains(station))continue;
                    if(count==stations.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                    stations[count++]=station;
                }
            }
        }
        if(boundaryContact!=SurfacePairRelation.CurveContact)
        {
            stations[..count].Sort();
            Span<KernelVector3> point=stackalloc KernelVector3[1];
            for(var s=1;s<count;s++)
            {
                if(stations[s]==stations[s-1])continue;
                if(!TryInteriorBoundaryStation(stations[s-1],stations[s],0,0,false,.5,out var t))
                    return ParasolidConstants.PK_ERROR_cant_do_intersect;
                var status=EvaluateCurveCore(in record,t,0,false,point,out _);
                if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                status=IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(domain.Surface)],point[0],out var uv);
                if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                var error=RegularFaceContains(input,face,uv,out var inside);if(error!=0)return error;
                if(inside){boundaryContact=SurfacePairRelation.CurveContact;break;}
            }
        }
        relation=boundaryContact;LastSheetAlgorithmStatus=AlgorithmStatus.Success;return 0;
    }

#if KERNEL_TEST_HOOKS
    internal static bool TestInheritedLineFragment(CurveTag cut,CurveTag source,double first,double last,
        double sourceFirst,double sourceLast,bool sameEnd)
    {
        Span<ModelingEdge> edges=stackalloc ModelingEdge[1]{new(){Curve=source,Start=sourceFirst,End=sourceLast,StartVertex=1,EndVertex=0}};
        return FindInheritedLineFragment(new(){Curve=cut},new(){Count=1},edges,first,last,0,sameEnd?1:2,out _);
    }

    internal static bool TestSameCertifiedLineCylinderPlaneRoot(CurveTag curve,SurfTag cylinder,SurfTag plane,out bool overlapping)
    {
        overlapping=false;
        var edge=new ModelingEdge{Curve=curve,Start=0,End=1};
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[2];
        Span<ScalarRootIsolation> intervals=stackalloc ScalarRootIsolation[2];Span<double> errors=stackalloc double[2];
        if(!TryFiniteLinePlaneEvent(edge,0,plane,out var flat)||flat.Count!=1
            ||IsolateFiniteExplicitCylinder(edge,cylinder,roots,intervals,errors,out var count)!=AlgorithmStatus.Success||count!=1)return false;
        var a=flat.FirstRoot;var b=intervals[0].Root;
        overlapping=IntersectStationBounds(new(a.Lower,a.Upper),b).Finite;
        return SameCertifiedLineCylinderPlaneRoot(new(){Carrier=curve,Surface=plane,Lower=a.Lower,Upper=a.Upper},
            new(){Carrier=curve,Surface=cylinder,Lower=b.Lower,Upper=b.Upper});
    }

    internal static bool TestExactCoincidentLineOverlap(CurveTag first,CurveTag second,double length,double end,
        out BufferCount count,out double lastFirst,out double lastSecond)
    {
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[2];
        var exact=TryExactCoincidentLineRoots(new(){Curve=first,Start=0,End=end},new(){Curve=second,Start=0,End=length},roots,out count);
        lastFirst=count==0?0:roots[count-1].First;lastSecond=count==0?0:roots[count-1].Second;
        return exact;
    }

    internal static int TestAdoptCoincidentLineRoot(CurveTag first,CurveTag second,SurfTag equation,bool differentOriginal,
        out bool adopted,out bool twoUses)
    {
        adopted=twoUses=false;
        Span<ModelingEdge> edges=stackalloc ModelingEdge[2]
        {
            new(){Curve=first,Start=0,End=2,StartVertex=0,EndVertex=-1},
            new(){Curve=second,Start=differentOriginal?.25:0,End=2,StartVertex=differentOriginal?1:-1,EndVertex=-1},
        };
        var input=new RegularBoundaryView(edges,default,default,default);
        Span<RegularFaceCut> cuts=stackalloc RegularFaceCut[1]{new(){Cut=new(){UpperEvent=2}}};
        var model=new SheetArrangement{Points=stackalloc KernelVector3[2],OriginalVertices=stackalloc VertexSlot[2]{0,differentOriginal?1:-1},
            Parameters=stackalloc BoundaryParameter[3],NP=2,Tolerance=1e-9};
        model.Points.Clear();
        var error=model.AssociateParameter(1,second,.25,equation:equation,witness:1+IntersectionPointCapacity);if(error!=0)return error;
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[2];
        if(!TryExactCoincidentLineRoots(edges[0],edges[1],roots,out var count)||count!=1)return ParasolidConstants.PK_ERROR_bad_parameter;
        error=AssociateCoincidentLineRoot(input,cuts,0,1,roots[0],ref model);if(error!=0)return error;
        adopted=cuts[0].Cut.UpperEvent==1&&!model.LiveBoundaryPoint(1);
        twoUses=model.KnownSourceBoundaryParameter(0,first,0,out var a,out _)&&a.Parameter==0
            &&model.KnownSourceBoundaryParameter(0,second,1,out var b,out _)&&b.Parameter==.25
            &&!model.KnownSourceBoundaryParameter(0,second,2,out _,out var foreign)&&foreign;
        return 0;
    }
#endif
}

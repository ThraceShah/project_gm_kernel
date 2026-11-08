using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private enum FiniteIntersectionState:byte{Unresolved,Empty,Complete,Unsupported,BudgetExceeded}
    private enum FiniteNativeEnd:byte{None,First,Last}
    private struct FiniteCurveSurfaceRoot
    {
        internal KernelVector3 Point;
        internal VertexSlot Vertex;
        internal IntersectionVectorKind Kind;
        internal FiniteNativeEnd NativeEnd;
        internal double Parameter,Lower,Upper,SpatialError;
    }
    private struct FiniteCurveSurfaceResult
    {
        internal BufferOffset InputEdge;
        internal CurveTag Curve;
        internal SurfTag Surface,TangentSupport;
        internal EntityGeneration CurveGeneration,SurfaceGeneration;
        internal double First,Last;
        internal FiniteIntersectionState State;
        internal SurfacePairRelation Relation;
        internal BufferCount Count;
        internal byte TangentBoundary;
        internal FiniteCurveSurfaceRoot FirstRoot,SecondRoot;
    }

    internal static AlgorithmStatus IsolateFiniteExplicitCylinder(in ModelingEdge edge,SurfTag cylinderTag,Span<OperationIntersectionPoint> roots,
        Span<ScalarRootIsolation> isolation,Span<double> spatialErrors,out BufferCount count)
    {
        count=0;
        if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(cylinderTag)],out var cylinder)||cylinder.Kind!=SurfaceClass.Cylinder)
            return AlgorithmStatus.Unsupported;
        return IsolateFiniteExplicitLevelSet(edge,cylinder,roots,isolation,spatialErrors,out count);
    }

    internal static AlgorithmStatus IsolateFiniteExplicitLevelSet(in ModelingEdge edge,in AnalyticSurface surface,
        Span<OperationIntersectionPoint> roots,Span<ScalarRootIsolation> isolation,Span<double> spatialErrors,out BufferCount count,
        scoped ReadOnlySpan<IntervalNumber> linePoles=default,BufferCount estimateCount=0)
    {
        count=0;
        if(isolation.IsEmpty||!(edge.End>edge.Start))return AlgorithmStatus.InvalidInput;
        var record=Curves[GetCurveSlotByTag(edge.Curve)];
        if(!TryReadExplicitSpineCurve(record,out var geometry)||surface.Kind is not (SurfaceClass.Plane or SurfaceClass.Cylinder))
            return AlgorithmStatus.Unsupported;
        // In native t, a plane uses n·(C(t)-O)=0 and a cylinder uses
        // |(C(t)-O) x axis|²-r²=0. Keep the full finite branch and do
        // not promote an unresolved stationary cell to an empty result.
        Span<BlendSpineInstruction> spine=stackalloc BlendSpineInstruction[1]{new(){Curve=geometry,ChartCount=2}};
        Span<BlendChartInstruction> domain=stackalloc BlendChartInstruction[2]{new(){Parameter=edge.Start},new(){Parameter=edge.End}};
        var program=new NestedBlendProgram(default,spine,domain);
        var corrected=!linePoles.IsEmpty;
        var patch=corrected?new SurfaceIntersectionPatch(new BezierSurfacePatch(linePoles,1,0,new(edge.Start,edge.End,0,1)))
            :new SurfaceIntersectionPatch(program,0,edge.Start,edge.End);
        Span<double> estimates=stackalloc double[estimateCount];
        for(var i=0;i<estimates.Length;i++)estimates[i]=roots[i].First;
        var status=SurfaceIsoPlaneRoots.Find(patch,surface,edge.Start,edge.End,roots,ref count,strictSeparation:true,isolation:isolation);
        if(status!=AlgorithmStatus.Success)return status;
        if(count>spatialErrors.Length)return AlgorithmStatus.OutputTooSmall;
        for(var i=1;i<count;i++)for(var j=i;j>0&&isolation[j].Root.Lower<isolation[j-1].Root.Lower;j--)
        {
            (roots[j],roots[j-1])=(roots[j-1],roots[j]);
            (isolation[j],isolation[j-1])=(isolation[j-1],isolation[j]);
        }
        Span<IntervalNumber> enclosure=stackalloc IntervalNumber[9];
        for(var i=0;i<count;i++)
        {
            var root=isolation[i].Root;var at=roots[i].First;
            if(i>0&&isolation[i-1].Root.Upper>=root.Lower)return AlgorithmStatus.NotConverged;
            foreach(var estimate in estimates)if(estimate>=root.Lower&&estimate<=root.Upper){at=estimate;break;}
            status=SpineParameterMap.Series(geometry,new(at,0),out var point,out _);if(status!=AlgorithmStatus.Success)return status;
            IntervalVector box;
            if(corrected)
            {
                status=patch.Bound(new(root.Lower,root.Upper,0,0),enclosure);if(status!=AlgorithmStatus.Success)return status;
                box=new(enclosure[0],enclosure[1],enclosure[2]);
            }
            else
            {status=SpineParameterMap.Interval(geometry,new(root,0),out box,out _);if(status!=AlgorithmStatus.Success)return status;}
            roots[i].First=at;roots[i].Point=point.Value;spatialErrors[i]=(box-new IntervalVector(roots[i].Point)).Norm().Value.Upper;
            if(!double.IsFinite(spatialErrors[i]))return AlgorithmStatus.NotConverged;
        }
        return status;
    }

    private static bool SameCertifiedConicPlaneRoot(in BoundaryParameter first,in BoundaryParameter second)
    {
        // The caller has checked one common source occurrence and overlapping
        // isolated roots. Their combined window must exclude another lift.
        if(!(Math.Max(first.Upper,second.Upper)-Math.Min(first.Lower,second.Lower)<Math.PI))return false;
        var carrier=GetCurveSlotByTag(first.Carrier);var a=GetSurfaceSlotByTag(first.Surface);var b=GetSurfaceSlotByTag(second.Surface);
        if(carrier<0||a<0||b<0||Curves[carrier].Class is not (CurveClass.Circle or CurveClass.Ellipse)
            ||Surfaces[a].Class!=SurfaceClass.Plane||Surfaces[b].Class!=SurfaceClass.Plane
            ||!TryReadExplicitSpineCurve(Curves[carrier],out var conic)
            ||!TryPrepareSurface(Surfaces[a],out var p)||!TryPrepareSurface(Surfaces[b],out var q))return false;

        // Write each equation as x*cos(t)+y*sin(t)=h. Cramer's rule gives
        // the only common phase (c/d,s/d). Prove it is on the unit circle
        // without dividing or converting the phase to an approximate angle.
        ConicPlaneCoefficients(conic,p,out var x0,out var y0,out var h0);
        ConicPlaneCoefficients(conic,q,out var x1,out var y1,out var h1);
        var d=x0*y1-y0*x1;if(!d.Finite||d.ContainsZero)return false;
        var c=h0*y1-h1*y0;var s=x0*h1-x1*h0;
        if(!OnConicPhase(c,s,d))return false;

        // Each transverse sign occurs at most once per plane equation per
        // turn. Both certified roots must select the sign of the common phase.
        return SameConicBranch(new(first.Lower,first.Upper),x0,y0,c,s,d)&&SameConicBranch(new(second.Lower,second.Upper),x1,y1,c,s,d);
    }

    private static bool OnConicPhase(IntervalNumber c,IntervalNumber s,IntervalNumber d)=>(c*c+s*s-d*d).Magnitude==0;

    private static bool SameConicBranch(IntervalNumber root,IntervalNumber x,IntervalNumber y,IntervalNumber c,IntervalNumber s,IntervalNumber d)
    {
        IntervalDual.SinCos(new(root,0),out var sin,out var cos);
        var actual=y*cos.Value-x*sin.Value;var common=(y*c-x*s)/d;
        return actual.Lower>0&&common.Lower>0||actual.Upper<0&&common.Upper<0;
    }

    private static void ConicPlaneCoefficients(in ParametricSpineCurve conic,in AnalyticSurface plane,
        out IntervalNumber x,out IntervalNumber y,out IntervalNumber h)
    {
        var normal=new IntervalVector(plane.Axis);
        x=IntervalVector.Dot(new IntervalVector(conic.First),normal).Value;
        y=IntervalVector.Dot(new IntervalVector(conic.Second),normal).Value;
        h=IntervalVector.Dot(new IntervalVector(plane.Origin)-new IntervalVector(conic.Origin),normal).Value;
    }

    private static bool SameCertifiedExplicitCutRoot(CurveTag cut,in RegularBoundaryRoot first,in RegularBoundaryRoot second)
    {
        if(first.Certified!=ExplicitPlaneCertificate||second.Certified!=ExplicitPlaneCertificate||first.Witness==0||second.Witness==0)return false;
        var target=GetCurveSlotByTag(cut);var a=GetCurveSlotByTag(first.Curve);var b=GetCurveSlotByTag(second.Curve);
        if(target<0||a<0||b<0||Curves[target].Class!=CurveClass.Line)return false;
        var aLine=Curves[a].Class==CurveClass.Line;
        ref readonly var straight=ref (aLine?ref first:ref second);
        ref readonly var curved=ref (aLine?ref second:ref first);
        ref readonly var straightRecord=ref Curves[aLine?a:b];
        ref readonly var curvedRecord=ref Curves[aLine?b:a];
        if(!TrySingleLineRootPoint(straightRecord,straight.Root,out var point))return false;
        // Both original locators map to the same injective finite line.
        // Exact spatial equality lets both native intervals describe one
        // station without merging either original edge occurrence.
        if(curvedRecord.Class==CurveClass.Line)
        {
            if(!TrySingleLineRootPoint(curvedRecord,curved.Root,out var otherPoint))return false;
            return ZeroVector(point-otherPoint);
        }
        var bound=new IntervalNumber(curved.Root.Lower,curved.Root.Upper);
        var plane=GetSurfaceSlotByTag(curved.CuttingSurface);
        if(curvedRecord.Class is not (CurveClass.Circle or CurveClass.Ellipse)||!(bound.Upper-bound.Lower<Math.PI)
            ||plane<0||Surfaces[plane].Class!=SurfaceClass.Plane||!TryPrepareSurface(Surfaces[plane],out var other)
            ||!TryReadExplicitSpineCurve(curvedRecord,out var conic))return false;

        // An exact line root yields a point box. Solve the conic's two
        // coefficient equations in a nonzero minor of its 3D basis, then
        // verify all three coordinates and the isolated plane-root sign.
        if(IntervalVector.Dot(point-new IntervalVector(other.Origin),new IntervalVector(other.Axis)).Value.Magnitude!=0)return false;
        var relative=point-new IntervalVector(conic.Origin);var u=new IntervalVector(conic.First);var v=new IntervalVector(conic.Second);
        var determinant=IntervalVector.Cross(u,v);var cNumerator=IntervalVector.Cross(relative,v);var sNumerator=IntervalVector.Cross(u,relative);
        for(var axis=0;axis<3;axis++)
        {
            var d=Coordinate(determinant,axis);if(!d.Finite||d.ContainsZero)continue;
            var c=Coordinate(cNumerator,axis);var s=Coordinate(sNumerator,axis);
            if(!OnConicPhase(c,s,d))return false;
            if(!ZeroVector(u*c+v*s-relative*d))return false;
            ConicPlaneCoefficients(conic,other,out var x,out var y,out _);
            // The original locators mapped both finite roots to this one
            // injective target line. Equality of the source roots makes
            // their target enclosures independent bounds of one station.
            return SameConicBranch(bound,x,y,c,s,d);
        }
        return false;

        static IntervalNumber Coordinate(in IntervalVector value,int axis)=>axis==0?value.X.Value:axis==1?value.Y.Value:value.Z.Value;
        static bool ZeroVector(in IntervalVector value)=>value.X.Value.Magnitude==0&&value.Y.Value.Magnitude==0&&value.Z.Value.Magnitude==0;
        static bool TrySingleLineRootPoint(in CurveRecord record,in FiniteCurveSurfaceRoot root,out IntervalVector point)
        {
            point=default;
            if(record.Class!=CurveClass.Line||!double.IsFinite(root.Lower)||root.Lower!=root.Upper||!TryReadExplicitSpineCurve(record,out var line))return false;
            point=new IntervalVector(line.Origin)+new IntervalVector(line.First)*root.Lower;
            return point.Finite;
        }
    }

    private static int ReadRollingFiniteEvents(in RegularBoundaryView input,ReadOnlySpan<EdgeSlot> original,
        ReadOnlySpan<VertexSlot> vertices,ReadOnlySpan<RollingBandCandidate> bands,ReadOnlySpan<RollingSubdivision> subdivisions,ReadOnlySpan<RollingPointContact> points,
        Span<FiniteCurveSurfaceResult> results,out BufferCount count)
    {
        count=0;
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        for(var bandIndex=0;bandIndex<bands.Length;bandIndex++)
        {
            var band=bands[bandIndex];
            var source=original.IndexOf(band.Source);if(source<0)return Generated.ParasolidConstants.PK_ERROR_bad_loop;
            var curve=Curves[GetCurveSlotByTag(input.Edges[source].Curve)];
            if(curve.Class==CurveClass.Ellipse&&Surfaces[GetSurfaceSlotByTag(band.Surface)].Class==SurfaceClass.BlendSurface)
            for(var e=0;e<original.Length;e++)
            {
                var sourceSlot=original[e];if(sourceSlot<0)continue;
                var member=sourceSlot==band.Source;
                foreach(ref readonly var split in subdivisions)
                    member|=split.Band==bandIndex&&(split.Before==sourceSlot||split.After==sourceSlot);
                if(!member)continue;
                if(count==results.Length)return Generated.ParasolidConstants.PK_ERROR_not_implemented;
                var edge=input.Edges[e];var sourceEdge=Edges[sourceSlot];
                var certificate=NewFiniteEventResult(edge,e,band.Surface);
                for(var end=0;end<2;end++)
                {
                    var parameter=end==0?band.First:band.Last;
                    LiftPeriodicEdgeParameter(edge.Curve,edge.Start,edge.End,ref parameter);
                    foreach(var contact in points)if(contact.Band==band.Surface&&parameter>=contact.First&&parameter<=contact.Last)
                    {
                        var positive=LocalSenseSign(curve.Sense)>0;
                        if(sourceEdge.StartVertex==contact.Vertex)parameter=positive?edge.Start:edge.End;
                        else if(sourceEdge.EndVertex==contact.Vertex)parameter=positive?edge.End:edge.Start;
                    }
                    if(parameter<edge.Start||parameter>edge.End)continue;
                    var error=FiniteRollingEndpointRoot(sourceSlot,parameter,out var root);if(error!=0)return error;
                    if(certificate.Count++==0)certificate.FirstRoot=root;else certificate.SecondRoot=root;
                }
                certificate.State=certificate.Count==0?FiniteIntersectionState.Empty:FiniteIntersectionState.Complete;
                certificate.Relation=certificate.Count==0?SurfacePairRelation.Empty:SurfacePairRelation.PointContact;
                results[count++]=certificate;
            }
            foreach(var contact in points)if(contact.Band==band.Surface)
            {
                for(var e=0;e<original.Length;e++)
                {
                    var edge=original[e];if(edge<0||!LocalEdgeFaces(edge,out var a,out var b)||a!=contact.Face&&b!=contact.Face)continue;
                    if(count==results.Length)return Generated.ParasolidConstants.PK_ERROR_not_implemented;
                    var certificate=NewFiniteEventResult(input.Edges[e],e,band.Surface);var record=Edges[edge];
                    if(record.StartVertex==contact.Vertex||record.EndVertex==contact.Vertex)
                    {
                        var positive=LocalSenseSign(Curves[GetCurveSlotByTag(record.CurveTag)].Sense)>0;
                        var first=(record.StartVertex==contact.Vertex)==positive;var parameter=first?input.Edges[e].Start:input.Edges[e].End;
                        var error=FiniteRollingEndpointRoot(edge,parameter,out certificate.FirstRoot);if(error!=0)return error;
                        certificate.Count=1;certificate.State=FiniteIntersectionState.Complete;
                        certificate.Relation=SurfacePairRelation.PointContact;
                    }
                    else{certificate.State=FiniteIntersectionState.Empty;certificate.Relation=SurfacePairRelation.Empty;}
                    results[count++]=certificate;
                }
            }
            var patchFace=-1;for(var f=0;f<input.Faces.Length;f++)if(input.Faces[f].Surface==band.Surface){patchFace=f;break;}
            if(patchFace<0)return Generated.ParasolidConstants.PK_ERROR_bad_loop;
            var patch=input.Faces[patchFace];
            for(var side=0;side<2;side++)for(var l=0;l<patch.LoopCount;l++)
            {
                var support=Faces[side==0?band.FirstSupport:band.SecondSupport].SurfTag;var loop=input.Loops[patch.FirstLoop+l];
                foreach(var coedge in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                {
                    var index=coedge.Edge;var edge=input.Edges[index];var own=edge.Curve==(side==0?band.FirstContact:band.SecondContact);
                    var cylinder=Surfaces[GetSurfaceSlotByTag(band.Surface)].Class==SurfaceClass.Cylinder;
                    var cap=cylinder&&Curves[GetCurveSlotByTag(edge.Curve)].Class==CurveClass.Circle;
                    if(count==results.Length)return Generated.ParasolidConstants.PK_ERROR_not_implemented;
                    var certificate=NewFiniteEventResult(edge,index,support);
                    certificate.TangentBoundary=own?(byte)1:(byte)0;
                    certificate.Count=own||!cylinder?2:cap?1:0;
                    certificate.State=certificate.Count==0?FiniteIntersectionState.Empty:FiniteIntersectionState.Complete;
                    certificate.Relation=certificate.Count==0?SurfacePairRelation.Empty:own?SurfacePairRelation.CurveContact:SurfacePairRelation.PointContact;
                    for(var end=0;end<certificate.Count;end++)
                    {
                        var parameter=cap?(side==0?0:band.FirstSection==0?band.LastSection:band.FirstSection):end==0?edge.Start:edge.End;
                        var a=edge.StartVertex>=0?vertices[edge.StartVertex]:-1;var b=edge.EndVertex>=0?vertices[edge.EndVertex]:-1;
                        var error=FiniteEndpointRoot(edge.Curve,edge.Start,edge.End,a,b,0,parameter,out var root);if(error!=0)return error;
                        root.Kind=own?(end==0?IntersectionVectorKind.Start:IntersectionVectorKind.End):IntersectionVectorKind.Tangent;
                        if(end==0)certificate.FirstRoot=root;else certificate.SecondRoot=root;
                    }
                    results[count++]=certificate;
                }
            }
            // A rolling ball inside an analytic plane/cylinder support can
            // meet that support only at its exact contact locus. Restricting
            // source boundary curves to that locus removes the rank loss of
            // a tangential curve/Blend solve without fitting either curve.
            for(var side=0;side<2;side++)
            {
                var support=side==0?band.FirstSupport:band.SecondSupport;var sourceFace=-1;
                for(var f=0;f<input.Faces.Length;f++)if(input.Faces[f].Surface==Faces[support].SurfTag){sourceFace=f;break;}
                if(sourceFace<0)return Generated.ParasolidConstants.PK_ERROR_bad_loop;
                var contactCurve=side==0?band.FirstContact:band.SecondContact;
                var rangeError=RollingContactRange(band,contactCurve,out var lo,out var hi);if(rangeError!=0)return rangeError;
                var interval=new PK_INTERVAL_s();interval.value[0]=lo;interval.value[1]=hi;
                var resolved=ResolveIntersectionCurve(contactCurve,interval,out var contactSpan);if(resolved!=0)return resolved;
                var face=input.Faces[sourceFace];
                for(var l=0;l<face.LoopCount;l++)
                {
                    var loop=input.Loops[face.FirstLoop+l];
                    foreach(var coedge in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                    {
                        var e=coedge.Edge;var known=false;for(var c=0;c<count;c++)known|=results[c].InputEdge==e&&results[c].Surface==band.Surface;if(known)continue;
                        if(original[e]<0)return Generated.ParasolidConstants.PK_ERROR_bad_loop;
                        var edge=input.Edges[e];interval.value[0]=edge.Start;interval.value[1]=edge.End;
                        resolved=ResolveIntersectionCurve(edge.Curve,interval,out var span);if(resolved!=0)return resolved;
                        LastSheetPartitionStage=SheetPartitionStage.BoundaryIntersections;LastSheetCurve=edge.Curve;LastSheetParameter=edge.Start;
                        var status=IntersectCurveSpans(span,contactSpan,roots,out var n);LastSheetAlgorithmStatus=status;if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                        if(n>2||count==results.Length)return Generated.ParasolidConstants.PK_ERROR_not_implemented;
                        var certificate=NewFiniteEventResult(edge,e,band.Surface);certificate.TangentSupport=input.Faces[sourceFace].Surface;
                        certificate.Count=n;certificate.State=n==0?FiniteIntersectionState.Empty:FiniteIntersectionState.Complete;
                        certificate.Relation=n==0?SurfacePairRelation.Empty:n==2&&roots[0].Kind==IntersectionVectorKind.Start&&roots[1].Kind==IntersectionVectorKind.End?SurfacePairRelation.CurveContact:SurfacePairRelation.PointContact;
                        for(var r=0;r<n;r++)
                        {
                            var error=FiniteRollingEndpointRoot(original[e],(roots[r].First-span.Offset)/span.Scale,out var root);if(error!=0)return error;
                            // The boundary curve lies in a tangent support
                            // plane/cylinder. Its contact-locus crossing may
                            // be transverse, but its contact with the rolling
                            // surface is tangent.
                            root.Kind=certificate.Relation==SurfacePairRelation.CurveContact?roots[r].Kind:IntersectionVectorKind.Tangent;
                            if(r==0)certificate.FirstRoot=root;else certificate.SecondRoot=root;
                        }
                        results[count++]=certificate;
                    }
                }
            }
        }
        return 0;
    }

    private static FiniteCurveSurfaceResult NewFiniteEventResult(in ModelingEdge edge,BufferOffset index,SurfTag surface)
        =>new(){InputEdge=index,Curve=edge.Curve,CurveGeneration=Curves[GetCurveSlotByTag(edge.Curve)].Header.Generation,
            Surface=surface,SurfaceGeneration=Surfaces[GetSurfaceSlotByTag(surface)].Header.Generation,First=edge.Start,Last=edge.End};

    private static int FiniteRollingEndpointRoot(EdgeSlot edge,double parameter,out FiniteCurveSurfaceRoot root)
    {
        root=default;var record=Edges[edge];if(!TryProfileEdgeRange(record,out var first,out var last))return Generated.ParasolidConstants.PK_ERROR_not_implemented;
        return FiniteEndpointRoot(record.CurveTag,first,last,record.StartVertex,record.EndVertex,record.Tolerance,parameter,out root);
    }

    private static int FiniteEndpointRoot(CurveTag tag,double first,double last,VertexSlot startVertex,VertexSlot endVertex,double precision,double parameter,out FiniteCurveSurfaceRoot root)
    {
        root=default;var curve=Curves[GetCurveSlotByTag(tag)];
        if(!TryReadExplicitSpineCurve(curve,out var exact))return Generated.ParasolidConstants.PK_ERROR_not_implemented;
        var a=new IntervalVector(exact.First);var b=new IntervalVector(exact.Second);var speedBound=curve.Class==CurveClass.Line?a.Norm().Value
            :IntervalVector.Cross(a,b).Norm().Value/(a.Norm().Value+b.Norm().Value);
        var speed=speedBound.Lower;
        if(!(speed>0))return Generated.ParasolidConstants.PK_ERROR_cant_do_intersect;
        var error=SpineParameterMap.Series(exact,new(parameter,0),out var evaluated,out _);if(error!=AlgorithmStatus.Success)return EvaluationError(error);
        var point=evaluated.Value;precision=Math.Max(precision,Math.Max(startVertex>=0?Vertices[startVertex].Tolerance:0,endVertex>=0?Vertices[endVertex].Tolerance:0));
        precision=(new IntervalNumber(precision)+32*SmallLinearSolve.MachineEpsilon*(new IntervalVector(point).Norm().Value+new IntervalVector(exact.Origin).Norm().Value+a.Norm().Value+b.Norm().Value)).Upper;
        var delta=(new IntervalNumber(precision)/speed+32*SmallLinearSolve.MachineEpsilon*Math.Max(1,Math.Abs(parameter))).Upper;
        VertexSlot vertex=-1;var positive=LocalSenseSign(curve.Sense)>0;
        if(Math.Abs(parameter-first)<=delta)vertex=positive?startVertex:endVertex;
        if(Math.Abs(parameter-last)<=delta)
        {var other=positive?endVertex:startVertex;if(vertex>=0&&vertex!=other)return Generated.ParasolidConstants.PK_ERROR_cant_do_intersect;vertex=other;}
        if(vertex>=0){var found=SheetOriginalVertex(vertex,out point);if(found!=0)return found;}
        // The native station is exact when the provider explicitly
        // evaluated the declared trim endpoint. Spatial error still applies.
        var nativeEnd=first==last?FiniteNativeEnd.None:parameter==first?FiniteNativeEnd.First:parameter==last?FiniteNativeEnd.Last:FiniteNativeEnd.None;
        root=new(){Point=point,Vertex=vertex,Kind=IntersectionVectorKind.Tangent,NativeEnd=nativeEnd,
            Parameter=parameter,Lower=Math.BitDecrement(parameter-delta),Upper=Math.BitIncrement(parameter+delta),SpatialError=precision};return 0;
    }
}

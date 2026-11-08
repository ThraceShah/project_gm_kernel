using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
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
        count=0;var record=Curves[GetCurveSlotByTag(edge.Curve)];
        if(!TryReadExplicitSpineCurve(record,out var geometry)||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(cylinderTag)],out var cylinder)
            ||cylinder.Kind!=SurfaceClass.Cylinder)
            return AlgorithmStatus.Unsupported;
        // In the native curve parameter, solve |(C(t)-O) x axis|²-r²=0.
        // Interval position and derivative bounds cover the entire trim;
        // non-simple or unresolved roots cannot certify branch identity.
        Span<BlendSpineInstruction> spine=stackalloc BlendSpineInstruction[1]{new(){Curve=geometry,ChartCount=2}};
        Span<BlendChartInstruction> domain=stackalloc BlendChartInstruction[2]{new(){Parameter=edge.Start},new(){Parameter=edge.End}};
        var program=new NestedBlendProgram(default,spine,domain);
        var patch=new SurfaceIntersectionPatch(program,0,edge.Start,edge.End);
        var status=SurfaceIsoPlaneRoots.Find(patch,cylinder,edge.Start,edge.End,roots,ref count,strictSeparation:true,isolation:isolation);
        if(status!=AlgorithmStatus.Success)return status;
        for(var i=1;i<count;i++)for(var j=i;j>0&&isolation[j].Root.Lower<isolation[j-1].Root.Lower;j--)
        {
            (roots[j],roots[j-1])=(roots[j-1],roots[j]);
            (isolation[j],isolation[j-1])=(isolation[j-1],isolation[j]);
        }
        for(var i=0;i<count;i++)
        {
            var root=isolation[i].Root;var at=roots[i].First;
            if(i>0&&isolation[i-1].Root.Upper>=root.Lower)return AlgorithmStatus.NotConverged;
            status=SpineParameterMap.Series(geometry,new(at,0),out var point,out _);if(status!=AlgorithmStatus.Success)return status;
            status=SpineParameterMap.Interval(geometry,new(root,0),out var box,out _);if(status!=AlgorithmStatus.Success)return status;
            roots[i].Point=point.Value;spatialErrors[i]=(box-new IntervalVector(roots[i].Point)).Norm().Value.Upper;
            if(!double.IsFinite(spatialErrors[i]))return AlgorithmStatus.NotConverged;
        }
        return status;
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

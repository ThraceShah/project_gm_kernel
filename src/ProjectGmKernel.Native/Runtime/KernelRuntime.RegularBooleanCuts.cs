using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct RegularCutFamily{internal SurfTag First,Second;internal BufferOffset Start,PointStart;internal BufferCount Count,PointCount;internal SurfacePairRelation Relation;}
    private struct RegularFaceCut{internal BufferOffset First,Second;internal SheetCutCurve Cut;}

    private static int ClipRegularContactPoints(in RegularBoundaryView input,BufferOffset first,BufferOffset second,
        ReadOnlySpan<SurfaceIntersectionNode> points,Span<SurfaceIntersectionNode> accepted,out BufferCount count)
    {
        count=0;
        foreach(ref readonly var point in points)
        {
            var both=true;
            for(var side=0;side<2;side++)
            {
                var face=side==0?first:second;var uv=new PK_UV_s();uv.param[0]=side==0?point.U:point.S;uv.param[1]=side==0?point.V:point.T;
                var error=RegularFiniteFaceContains(input,face,uv,point.Point,out var inside,out _);if(error!=0)return error;
                error=RegularPointOnFiniteBoundary(input,face,point.Point,out var boundary);if(error!=0)return error;
                if(boundary)return ParasolidConstants.PK_ERROR_not_implemented;
                both&=inside;
            }
            if(both){if(count==accepted.Length)return ParasolidConstants.PK_ERROR_not_implemented;accepted[count++]=point;}
        }
        return 0;
    }

    private static int PrepareRegularSupportCuts(SurfTag first,SurfTag second,Span<SheetCutCurve> cuts,out BufferCount count)
    {
        count=0;var a=GetSurfaceSlotByTag(first);var b=GetSurfaceSlotByTag(second);
        if(!RegularPlanarSupport(a,out var av,out var errorA)||!RegularPlanarSupport(b,out var bv,out var errorB))return PrepareSheetCutCurves(first,second,cuts,out count);
        // Geometrically planar NURBS need not have an affine parameter map.
        // Preserve their real intersection support instead of replacing the
        // resulting native ICurve by a line on two surrogate planes.
        if(Surfaces[a].Class==SurfaceClass.BSurface&&!TryAffinePlanarSurface(a,out _,out _,out _)
            ||Surfaces[b].Class==SurfaceClass.BSurface&&!TryAffinePlanarSurface(b,out _,out _,out _))return PrepareSheetCutCurves(first,second,cuts,out count);
        var sine=OperationIntersection.Norm(ProfileCross(av.Axis,bv.Axis));
        if(errorA+errorB>0&&(sine<=1e-14||(errorA+errorB)/(sine-1e-14)>1e-10))return PrepareSheetCutCurves(first,second,cuts,out count);
        Span<OperationIntersectionBranch> branches=stackalloc OperationIntersectionBranch[64];Span<KernelVector3> points=stackalloc KernelVector3[64];
        var status=OperationIntersection.Surfaces(av,bv,branches,out var n,points,out var np);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        if(np!=0||n>cuts.Length)return ParasolidConstants.PK_ERROR_not_implemented;
        for(var i=0;i<n;i++)
        {
            if(branches[i].Kind!=IntersectionCurveKind.Simple)return ParasolidConstants.PK_ERROR_not_implemented;
            CurveTag curve=0;var error=CreateIntersectionBranch(branches[i],&curve);if(error!=0)return error;
            cuts[count++]=new(){Curve=curve,First=branches[i].Start,Last=branches[i].End,
                Closed=branches[i].Class is CurveClass.Circle or CurveClass.Ellipse&&(branches[i].End-branches[i].Start)>=Math.Tau-1e-10?(byte)1:(byte)0};
        }
        return 0;
    }

    private static bool RegularPlanarSupport(SurfaceSlot slot,out AnalyticSurface plane,out double maximumDeviation)
    {
        plane=default;maximumDeviation=0;var record=Surfaces[slot];
        if(TryPrepareSurface(record,out var analytic)){plane=analytic;return analytic.Kind==SurfaceClass.Plane;}
        if(record.Class!=SurfaceClass.BSurface)return false;
        var view=GetBSurfaceView(BSurfaceDataStore[record.DataIndex]);if(view.UVertexCount<2||view.VVertexCount<2)return false;
        var dimension=view.Dimension;var poles=view.Vertices;
        var w=view.Rational?poles[3]:1;var origin=new KernelVector3{X=poles[0]/w,Y=poles[1]/w,Z=poles[2]/w};
        var u=dimension*view.VVertexCount;var uw=view.Rational?poles[u+3]:1;var vw=view.Rational?poles[dimension+3]:1;
        var du=new KernelVector3{X=poles[u]/uw-origin.X,Y=poles[u+1]/uw-origin.Y,Z=poles[u+2]/uw-origin.Z};
        var dv=new KernelVector3{X=poles[dimension]/vw-origin.X,Y=poles[dimension+1]/vw-origin.Y,Z=poles[dimension+2]/vw-origin.Z};
        var normal=ProfileCross(du,dv);if(!ProfileNormalize(ref normal)||!ProfileNormalize(ref du))return false;
        // Positive rational basis functions preserve this control-hull distance
        // bound. This is a certified plane recognition tolerance, not samples.
        for(var i=0;i<poles.Length;i+=dimension)
        {
            IntervalNumber weight=view.Rational?poles[i+3]:1;if(weight.Lower<=0)return false;
            var x=new IntervalNumber(poles[i])/weight-origin.X;var y=new IntervalNumber(poles[i+1])/weight-origin.Y;var z=new IntervalNumber(poles[i+2])/weight-origin.Z;
            var deviation=x*normal.X+y*normal.Y+z*normal.Z;
            if(deviation.Magnitude>1e-12*Math.Max(1,Math.Max(x.Magnitude,Math.Max(y.Magnitude,z.Magnitude))))return false;
            maximumDeviation=Math.Max(maximumDeviation,deviation.Magnitude);
        }
        plane=new(SurfaceClass.Plane,origin,normal,du);return true;
    }

    private static int RegularBoundarySurfaceRoots(in RegularBoundaryView input,BufferOffset face,SurfTag surface,
        Span<KernelVector3> points,ref BufferCount count)
    {
        var record=input.Faces[face];Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        for(var l=0;l<record.LoopCount;l++)
        {
            var loop=input.Loops[record.FirstLoop+l];
            for(var k=0;k<loop.CoedgeCount;k++)
            {
                var edge=input.Edges[input.Coedges[loop.FirstCoedge+k].Edge];
                LastSheetPartitionStage=SheetPartitionStage.BoundaryIntersections;LastSheetCurve=edge.Curve;LastSheetParameter=edge.Start;
                var status=IntersectRegularSurfaceCurve(surface,edge.Curve,edge.Start,edge.End,roots,out var n);LastSheetAlgorithmStatus=status;if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                for(var r=0;r<n;r++)
                {
                    if(roots[r].Kind is not (IntersectionVectorKind.Simple or IntersectionVectorKind.Start or IntersectionVectorKind.End or IntersectionVectorKind.Tangent))return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    var duplicate=false;for(var i=0;i<count;i++)duplicate|=ProfileDistance(points[i],roots[r].Point)<1e-8;
                    if(!duplicate){if(count==points.Length)return ParasolidConstants.PK_ERROR_not_implemented;points[count++]=roots[r].Point;}
                }
            }
        }
        return 0;
    }

    private static int ClipRegularFacePair(in RegularBoundaryView input,BufferOffset first,BufferOffset second,
        ReadOnlySpan<SheetCutCurve> curves,Span<RegularFaceCut> cuts,ref BufferCount cutCount,
        Span<SheetCutCurve> owned,ref BufferCount ownedCount,ref SheetArrangement model)
    {
        Span<KernelVector3> roots=stackalloc KernelVector3[SheetPointLimit];var nRoots=0;
        var error=RegularBoundarySurfaceRoots(input,first,input.Faces[second].Surface,roots,ref nRoots);if(error!=0)return error;
        error=RegularBoundarySurfaceRoots(input,second,input.Faces[first].Surface,roots,ref nRoots);if(error!=0)return error;
        LastSheetPartitionStage=SheetPartitionStage.CutClip;
        Span<double> parameters=stackalloc double[SheetPointLimit+2];Span<KernelVector3> point=stackalloc KernelVector3[1];
        foreach(ref readonly var curve in curves)
        {
            var n=0;
            // A line's bounded material intervals begin and end at actual
            // face-boundary crossings. Artificial infinite support bounds
            // are not finite-domain witnesses for a planar NURBS patch.
            if(curve.Closed==0&&Curves[GetCurveSlotByTag(curve.Curve)].Class!=CurveClass.Line){parameters[n++]=curve.First;parameters[n++]=curve.Last;}
            for(var i=0;i<nRoots;i++)
            {
                var p=new PK_VECTOR_s();WriteVector(&p,roots[i]);double t=0;error=CurveParameteriseVectorCore(curve.Curve,p,&t);
                if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return error;
                LiftPeriodicEdgeParameter(curve.Curve,curve.First,curve.Last,ref t);
                if(!OperationIntersection.InInterval(t,curve.First,curve.Last))continue;
                var duplicate=false;for(var j=0;j<n;j++)duplicate|=Math.Abs(parameters[j]-t)<1e-10;
                if(!duplicate){if(n==parameters.Length)return ParasolidConstants.PK_ERROR_not_implemented;parameters[n++]=Math.Clamp(t,curve.First,curve.Last);}
            }
            parameters[..n].Sort();var intervals=curve.Closed!=0?Math.Max(1,n):n-1;
            for(var i=0;i<intervals;i++)
            {
                var closed=curve.Closed!=0&&n==0;var wrap=curve.Closed!=0&&n>0&&i==n-1;
                var lo=closed?curve.First:parameters[i];var hi=closed?curve.Last:parameters[(i+1)%n];
                var width=wrap?curve.Last-lo+hi-curve.First:hi-lo;if(!(width>1e-12))continue;
                var at=lo+width*.3819660112501051;if(at>curve.Last)at=curve.First+at-curve.Last;
                var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(curve.Curve)],at,0,false,point,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                if(RegularPointOutsideSupport(input.Faces[first].Surface,point[0])||RegularPointOutsideSupport(input.Faces[second].Surface,point[0]))continue;
                status=IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(input.Faces[first].Surface)],point[0],out var a);
                if(status==AlgorithmStatus.InvalidInput)continue;if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                status=IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(input.Faces[second].Surface)],point[0],out var b);
                if(status==AlgorithmStatus.InvalidInput)continue;if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                error=RegularFiniteFaceContains(input,first,a,point[0],out var insideA,out var boundaryA);if(error!=0)return error;if(!insideA)continue;
                error=RegularFiniteFaceContains(input,second,b,point[0],out var insideB,out var boundaryB);if(error!=0)return error;if(!insideB)continue;
                if(boundaryA&&boundaryB)continue;
                var cut=curve;cut.First=lo;cut.Last=hi;cut.Closed=closed?(byte)1:(byte)0;
                if(wrap)
                {
                    error=CloneSheetCurveArc(curve.Curve,lo,hi,true,out cut.Curve,out cut.First,out cut.Last);if(error!=0)return error;
                    if(ownedCount==owned.Length)return ParasolidConstants.PK_ERROR_not_implemented;owned[ownedCount++]=cut;
                }
                if(cutCount==cuts.Length)return ParasolidConstants.PK_ERROR_not_implemented;cuts[cutCount++]=new(){First=first,Second=second,Cut=cut};
                if(cut.Closed==0)for(var end=0;end<2;end++)
                {
                    status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(cut.Curve)],end==0?cut.First:cut.Last,0,false,point,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                    error=model.AddPoint(point[0],-1,out _);if(error!=0)return error;
                }
            }
        }
        return 0;
    }

    private static bool RegularPointOutsideSupport(SurfTag surface,in KernelVector3 point)
    {
        if(RegularSurfaceExtent(GetSurfaceSlotByTag(surface),out var box)!=AlgorithmStatus.Success)return false;
        return point.X<box.XLo-1e-8||point.X>box.XHi+1e-8||point.Y<box.YLo-1e-8||point.Y>box.YHi+1e-8||point.Z<box.ZLo-1e-8||point.Z>box.ZHi+1e-8;
    }

    private static int SplitRegularSourceEdges(in RegularBoundaryView input,ReadOnlySpan<EdgeSlot> original,
        Span<SheetSourceEdge> sources,Span<SheetCutCurve> owned,ref BufferCount ownedCount,ref SheetArrangement model,bool coincident=false)
    {
        Span<double> parameters=stackalloc double[SheetPointLimit+2];Span<BufferOffset> vertices=stackalloc BufferOffset[SheetPointLimit+2];
        for(var e=0;e<input.Edges.Length;e++)
        {
            var edge=input.Edges[e];var slot=GetCurveSlotByTag(edge.Curve);var positive=LocalSenseSign(Curves[slot].Sense)>0;var ring=edge.StartVertex<0;var n=0;
            if(!ring){parameters[n]=edge.Start;vertices[n++]=positive?edge.StartVertex:edge.EndVertex;parameters[n]=edge.End;vertices[n++]=positive?edge.EndVertex:edge.StartVertex;}
            for(var p=0;p<model.NP;p++)
            {
                var vector=new PK_VECTOR_s();WriteVector(&vector,model.Points[p]);double t=0;var error=CurveParameteriseVectorCore(edge.Curve,vector,&t);
                if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return error;
                LiftPeriodicEdgeParameter(edge.Curve,edge.Start,edge.End,ref t);
                if(!OperationIntersection.InInterval(t,edge.Start,edge.End))continue;
                if(coincident&&!model.VertexPrecisions.IsEmpty&&model.OriginalVertices[p]>=0
                    &&Vertices[model.OriginalVertices[p]].Body!=Edges[original[e]].Body
                    &&t>edge.Start+1e-10&&t<edge.End-1e-10)
                {
                    // A planar vertex-on-edge imprint is a local-precision
                    // merge only if surviving cells use both source bodies.
                    if(RegularPlanarImprintJunction(input,p,e))model.VertexMergeCandidates[p]=1;
                }
                var duplicate=false;for(var k=0;k<n;k++)duplicate|=Math.Abs(parameters[k]-t)<1e-10||vertices[k]==p;
                if(!duplicate){if(n==parameters.Length)return ParasolidConstants.PK_ERROR_not_implemented;parameters[n]=Math.Clamp(t,edge.Start,edge.End);vertices[n++]=p;}
            }
            for(var i=1;i<n;i++)for(var j=i;j>0&&parameters[j]<parameters[j-1];j--){(parameters[j],parameters[j-1])=(parameters[j-1],parameters[j]);(vertices[j],vertices[j-1])=(vertices[j-1],vertices[j]);}
            var start=model.NE;
            if(ring&&n==0){var error=model.AddEdge(edge.Curve,edge.Start,edge.End,-1,-1,false,out _);if(error!=0)return error;}
            else for(var i=0;i<(ring?n:n-1);i++)
            {
                var next=(i+1)%n;var wrap=ring&&next==0;var curve=edge.Curve;var lo=parameters[i];var hi=parameters[next];
                if(wrap)
                {
                    var error=CloneSheetCurveArc(edge.Curve,lo,hi,true,out curve,out lo,out hi);if(error!=0)return error;
                    if(ownedCount==owned.Length)return ParasolidConstants.PK_ERROR_not_implemented;owned[ownedCount++]=new(){Curve=curve};
                }
                var add=model.AddEdge(curve,lo,hi,positive?vertices[i]:vertices[next],positive?vertices[next]:vertices[i],false,out _);if(add!=0)return add;
            }
            sources[e]=new(){Source=original[e],First=start,Count=model.NE-start};
        }
        return 0;
    }
}

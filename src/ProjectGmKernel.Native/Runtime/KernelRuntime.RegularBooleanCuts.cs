using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct RegularCutFamily{internal SurfTag First,Second;internal BufferOffset Start,PointStart;internal BufferCount Count,PointCount;internal SurfacePairRelation Relation;}
    private struct RegularFaceCut{internal BufferOffset First,Second;internal SheetCutCurve Cut;internal byte SecondIsBoundary;}
    private struct RegularBoundaryRoot
    {
        internal CurveTag Curve;
        internal SurfTag TangentSupport;
        internal SurfTag ContactSurface,ContactSupport;
        internal FiniteCurveSurfaceRoot Root;
        internal byte Certified;
    }

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
        Span<RegularBoundaryRoot> points,ref BufferCount count,ReadOnlySpan<FiniteCurveSurfaceResult> certificates=default)
    {
        var record=input.Faces[face];Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        for(var l=0;l<record.LoopCount;l++)
        {
            var loop=input.Loops[record.FirstLoop+l];
            for(var k=0;k<loop.CoedgeCount;k++)
            {
                var inputEdge=input.Coedges[loop.FirstCoedge+k].Edge;var edge=input.Edges[inputEdge];
                LastSheetPartitionStage=SheetPartitionStage.BoundaryIntersections;LastSheetCurve=edge.Curve;LastSheetParameter=edge.Start;
                var n=0;var known=false;FiniteCurveSurfaceResult proof=default;
                foreach(var certificate in certificates)
                {
                    if(certificate.InputEdge!=inputEdge||certificate.Surface!=surface||certificate.Curve!=edge.Curve)continue;
                    if(certificate.CurveGeneration!=Curves[GetCurveSlotByTag(edge.Curve)].Header.Generation||certificate.SurfaceGeneration!=Surfaces[GetSurfaceSlotByTag(surface)].Header.Generation
                        ||certificate.First!=edge.Start||certificate.Last!=edge.End)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    if(certificate.State is not (FiniteIntersectionState.Complete or FiniteIntersectionState.Empty))return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    known=true;proof=certificate;n=certificate.Count;
                    for(var r=0;r<n;r++){var root=r==0?certificate.FirstRoot:certificate.SecondRoot;roots[r]=new(){Point=root.Point,First=root.Parameter,Kind=root.Kind};}
                    break;
                }
                var status=known?AlgorithmStatus.Success:IntersectRegularSurfaceCurve(surface,edge.Curve,edge.Start,edge.End,roots,out n);LastSheetAlgorithmStatus=status;if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                for(var r=0;r<n;r++)
                {
                    if(roots[r].Kind is not (IntersectionVectorKind.Simple or IntersectionVectorKind.Start or IntersectionVectorKind.End or IntersectionVectorKind.Tangent))return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    var duplicate=false;for(var i=0;i<count;i++)duplicate|=points[i].Curve==edge.Curve&&points[i].Root.Parameter==roots[r].First;
                    if(!duplicate)
                    {
                        if(count==points.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                        SurfTag contactSupport=0;
                        foreach(var certificate in certificates)if(certificate.InputEdge==inputEdge&&certificate.TangentBoundary!=0)
                        {contactSupport=certificate.Surface;break;}
                        points[count++]=new(){Curve=edge.Curve,TangentSupport=proof.TangentSupport,ContactSurface=record.Surface,ContactSupport=contactSupport,Certified=known?(byte)1:(byte)0,
                            Root=known?(r==0?proof.FirstRoot:proof.SecondRoot):new(){Point=roots[r].Point,Vertex=-1,Parameter=roots[r].First,Kind=roots[r].Kind}};
                    }
                }
            }
        }
        return 0;
    }

    private static int ClipRegularFacePair(in RegularBoundaryView input,BufferOffset first,BufferOffset second,
        ReadOnlySpan<SheetCutCurve> curves,Span<RegularFaceCut> cuts,ref BufferCount cutCount,
        Span<SheetCutCurve> owned,ref BufferCount ownedCount,ref SheetArrangement model,ReadOnlySpan<FiniteCurveSurfaceResult> certificates=default)
    {
        Span<RegularBoundaryRoot> roots=stackalloc RegularBoundaryRoot[SheetPointLimit];var nRoots=0;
        var error=RegularBoundarySurfaceRoots(input,first,input.Faces[second].Surface,roots,ref nRoots,certificates);if(error!=0)return error;
        error=RegularBoundarySurfaceRoots(input,second,input.Faces[first].Surface,roots,ref nRoots,certificates);if(error!=0)return error;
        LastSheetPartitionStage=SheetPartitionStage.CutClip;
        Span<double> parameters=stackalloc double[SheetPointLimit+2];Span<double> rootParameters=stackalloc double[SheetPointLimit];Span<KernelVector3> point=stackalloc KernelVector3[1];
        foreach(ref readonly var curve in curves)
        {
            var n=0;
            // A line's bounded material intervals begin and end at actual
            // face-boundary crossings. Artificial infinite support bounds
            // are not finite-domain witnesses for a planar NURBS patch.
            if(curve.Closed==0&&Curves[GetCurveSlotByTag(curve.Curve)].Class!=CurveClass.Line){parameters[n++]=curve.First;parameters[n++]=curve.Last;}
            for(var i=0;i<nRoots;i++)
            {
                rootParameters[i]=double.NaN;
                var p=new PK_VECTOR_s();WriteVector(&p,roots[i].Root.Point);double t=roots[i].Root.Parameter;
                error=roots[i].Curve==curve.Curve?0:CurveParameteriseVectorCore(curve.Curve,p,&t);
                if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return error;
                LiftPeriodicEdgeParameter(curve.Curve,curve.First,curve.Last,ref t);
                if(!OperationIntersection.InInterval(t,curve.First,curve.Last))continue;
                t=Math.Clamp(t,curve.First,curve.Last);rootParameters[i]=t;
                var duplicate=false;
                for(var j=0;j<n;j++)
                {
                    if(Math.Abs(parameters[j]-t)>=1e-10)continue;
                    if(model.RequireBoundaryAssociations&&roots[i].Certified!=0&&parameters[j]!=t)
                    {
                        if(!CertifiedRootMatchesCutAnchor(roots[i],curve.Curve,parameters[j],point))continue;
                        // Keep the source-root proof on the retained native
                        // anchor. Losing it leaves a rounded tangent looking
                        // like a transverse halfedge in the arrangement.
                        rootParameters[i]=parameters[j];
                    }
                    duplicate=true;break;
                }
                if(!duplicate){if(n==parameters.Length)return ParasolidConstants.PK_ERROR_not_implemented;parameters[n++]=t;}
            }
            parameters[..n].Sort();var intervals=curve.Closed!=0?Math.Max(1,n):n-1;
            for(var i=0;i<intervals;i++)
            {
                var closed=curve.Closed!=0&&n==0;var wrap=curve.Closed!=0&&n>0&&i==n-1;
                var lo=closed?curve.First:parameters[i];var hi=closed?curve.Last:parameters[(i+1)%n];
                var width=wrap?curve.Last-lo+hi-curve.First:hi-lo;if(!(width>(model.RequireBoundaryAssociations?0:1e-12)))continue;
                var at=lo+width*.3819660112501051;if(at>curve.Last)at=curve.First+at-curve.Last;
                var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(curve.Curve)],at,0,false,point,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                if(RegularPointOutsideSupport(input.Faces[first].Surface,point[0])||RegularPointOutsideSupport(input.Faces[second].Surface,point[0]))continue;
                error=FiniteBoundarySurfaceParameters(input.Faces[first].Surface,curve.Curve,at,point[0],out var a);
                if(error==ParasolidConstants.PK_ERROR_not_on_surface)continue;if(error!=0)return error;
                error=FiniteBoundarySurfaceParameters(input.Faces[second].Surface,curve.Curve,at,point[0],out var b);
                if(error==ParasolidConstants.PK_ERROR_not_on_surface)continue;if(error!=0)return error;
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
                error=AssociateBoundaryCarrierSurface(ref model,cut.Curve,input.Faces[first].Surface);if(error!=0)return error;
                error=AssociateBoundaryCarrierSurface(ref model,cut.Curve,input.Faces[second].Surface);if(error!=0)return error;
                if(cut.Closed==0)for(var end=0;end<2;end++)
                {
                    status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(cut.Curve)],end==0?cut.First:cut.Last,0,false,point,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                    error=model.AddPoint(point[0],-1,out var endpoint);if(error!=0)return error;
                    error=model.AssociateParameter(endpoint,cut.Curve,end==0?cut.First:cut.Last);if(error!=0)return error;
                    var sourceParameter=end==0?lo:hi;
                    for(var r=0;r<nRoots;r++)if(rootParameters[r]==sourceParameter)
                    {
                        var root=roots[r];
                        if(root.Certified!=0&&root.Root.Vertex>=0)
                        {
                            error=model.AddPoint(root.Root.Point,root.Root.Vertex,out var original);if(error!=0)return error;
                            if(original!=endpoint)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                        }
                        if(root.Certified!=0)
                        {error=model.AssociateParameter(endpoint,root.Curve,root.Root.Parameter,root.Root.Lower,root.Root.Upper);if(error!=0)return error;}
                        else if(model.RequireBoundaryAssociations)
                        {
                            var carrier=Curves[GetCurveSlotByTag(root.Curve)];
                            if(TryReadExplicitSpineCurve(carrier,out _))
                            {
                                // Bound the analytic locator's floating
                                // evaluation/inversion error independently
                                // of modeling and mark/fix tolerances.
                                error=FiniteEndpointRoot(root.Curve,carrier.TMin,carrier.TMax,-1,-1,0,root.Root.Parameter,out var locator);if(error!=0)return error;
                                error=model.AssociateParameter(endpoint,root.Curve,root.Root.Parameter,locator.Lower,locator.Upper);
                            }
                            else error=model.AssociateParameter(endpoint,root.Curve,root.Root.Parameter);
                            if(error!=0)return error;
                        }
                        if(root.Certified!=0&&root.Root.Kind==IntersectionVectorKind.Tangent&&root.TangentSupport!=0
                            &&input.Faces[first].Surface!=root.TangentSupport&&input.Faces[second].Surface!=root.TangentSupport)
                        {error=model.AssociateTangency(endpoint,root.Curve,cut.Curve);if(error!=0)return error;}
                        if(root.ContactSupport!=0)
                        {error=model.AssociateSurfaceContact(endpoint,root.ContactSurface,root.ContactSupport);if(error!=0)return error;}
                    }
                }
            }
        }
        return 0;
    }

    private static bool CertifiedRootMatchesCutAnchor(in RegularBoundaryRoot root,CurveTag cut,double parameter,Span<KernelVector3> point)
    {
        if(root.Curve==cut)return parameter>=root.Root.Lower&&parameter<=root.Root.Upper;
        if(EvaluateCurveCore(in Curves[GetCurveSlotByTag(cut)],parameter,0,false,point,out _)!=AlgorithmStatus.Success
            ||!TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(root.Curve)],out var source))return false;
        var p=new IntervalVector(point[0]);var q=new IntervalVector(root.Root.Point);
        var roundoff=(32*SmallLinearSolve.MachineEpsilon*(p.Norm().Value+q.Norm().Value+new IntervalVector(source.Origin).Norm().Value
            +new IntervalVector(source.First).Norm().Value+new IntervalVector(source.Second).Norm().Value)).Upper;
        if((p-q).Norm().Value.Upper>roundoff)return false;
        var vector=new PK_VECTOR_s();WriteVector(&vector,point[0]);double native;
        if(CurveParameteriseVectorCore(root.Curve,vector,&native)!=0)return false;
        LiftPeriodicEdgeParameter(root.Curve,root.Root.Lower,root.Root.Upper,ref native);
        return native>=root.Root.Lower&&native<=root.Root.Upper;
    }

    private static int FiniteBoundarySurfaceParameters(SurfTag surface,CurveTag curve,double parameter,in KernelVector3 point,out PK_UV_s uv)
    {
        var record=Curves[GetCurveSlotByTag(curve)];
        if(TryAnalyticBlendContactMap(surface,record,record.TMin,record.TMax,out _,out _,out _)
            ||record.Class==CurveClass.ICurve&&(SameSheetSupport(surface,ICurveDataPool[record.DataIndex].Surface0Tag)||SameSheetSupport(surface,ICurveDataPool[record.DataIndex].Surface1Tag)))
            return SheetCurveUV(surface,curve,parameter,true,out uv,out _,out _);
        var found=IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(surface)],point,out uv);
        return found==AlgorithmStatus.InvalidInput?ParasolidConstants.PK_ERROR_not_on_surface:IntersectionError(found);
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
                var vector=new PK_VECTOR_s();WriteVector(&vector,model.Points[p]);double t=0;var known=model.KnownParameter(p,edge.Curve,out t);
                if(!known&&model.RequireBoundaryAssociations)continue;
                var error=known?0:CurveParameteriseVectorCore(edge.Curve,vector,&t);
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
                var duplicate=false;for(var k=0;k<n;k++)duplicate|=(model.RequireBoundaryAssociations?parameters[k]==t:Math.Abs(parameters[k]-t)<1e-10)||vertices[k]==p;
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
            sources[e]=new(){Source=original[e],InputEdge=e,First=start,Count=model.NE-start};
        }
        return 0;
    }
}

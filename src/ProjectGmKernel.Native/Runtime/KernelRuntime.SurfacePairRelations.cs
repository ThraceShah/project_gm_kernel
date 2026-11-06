using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool RegularPlanarImprintJunction(in RegularBoundaryView input,BufferOffset point,BufferOffset edge)
    {
        for(var f=0;f<input.Faces.Length;f++)
        {
            var face=input.Faces[f];var incident=false;
            for(var l=0;l<face.LoopCount&&!incident;l++)
            {
                var loop=input.Loops[face.FirstLoop+l];for(var k=0;k<loop.CoedgeCount;k++)
                {
                    var e=input.Coedges[loop.FirstCoedge+k].Edge;var value=input.Edges[e];
                    if(e==edge||value.StartVertex==point||value.EndVertex==point){incident=true;break;}
                }
            }
            if(incident&&!RegularPlanarSupport(GetSurfaceSlotByTag(face.Surface),out _,out _))return false;
        }
        return true;
    }

    private static int RegularFiniteFaceContains(in RegularBoundaryView input,BufferOffset face,PK_UV_s uv,in KernelVector3 point,out bool inside,out bool boundary)
    {
        boundary=false;var error=RegularFaceContains(input,face,uv,out inside);if(error==0)return 0;
        if(error is not (ParasolidConstants.PK_ERROR_eval_failure or ParasolidConstants.PK_ERROR_cant_do_intersect))return error;
        var fallback=RegularPointOnFiniteBoundary(input,face,point,out boundary);if(fallback!=0)return fallback;
        if(!boundary)return error;inside=true;return 0;
    }

    private static int RegularPointOnFiniteBoundary(in RegularBoundaryView input,BufferOffset face,in KernelVector3 point,out bool on)
    {
        on=false;var record=input.Faces[face];var vector=new PK_VECTOR_s();WriteVector(&vector,point);
        for(var l=0;l<record.LoopCount;l++)
        {
            var loop=input.Loops[record.FirstLoop+l];
            for(var k=0;k<loop.CoedgeCount;k++)
            {
                var edge=input.Edges[input.Coedges[loop.FirstCoedge+k].Edge];double t=0;var error=CurveParameteriseVectorCore(edge.Curve,vector,&t);
                if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return error;LiftPeriodicEdgeParameter(edge.Curve,edge.Start,edge.End,ref t);
                if(OperationIntersection.InInterval(t,edge.Start,edge.End)){on=true;return 0;}
            }
        }
        return 0;
    }

    private static bool RegularCoincidentSupports(SurfTag first,SurfTag second)
    {
        if(SameSheetSupport(first,second))return true;
        var a=GetSurfaceSlotByTag(first);var b=GetSurfaceSlotByTag(second);
        if(RegularPlanarSupport(a,out var p,out _)&&RegularPlanarSupport(b,out var q,out _))
            return OperationIntersection.Norm(ProfileCross(p.Axis,q.Axis))==0&&Dot(Sub(p.Origin,q.Origin),p.Axis)==0;
        if(!TryPrepareSurface(Surfaces[a],out p)||!TryPrepareSurface(Surfaces[b],out q)||p.Kind!=q.Kind)return false;
        if(p.Kind==SurfaceClass.Sphere)return p.Origin.X==q.Origin.X&&p.Origin.Y==q.Origin.Y&&p.Origin.Z==q.Origin.Z&&p.Radius==q.Radius;
        if(p.Kind==SurfaceClass.Cylinder)
        {
            if(p.Radius!=q.Radius||OperationIntersection.Norm(ProfileCross(p.Axis,q.Axis))!=0)return false;
            var offset=Sub(p.Origin,q.Origin);return OperationIntersection.Norm(ProfileCross(offset,p.Axis))==0;
        }
        return false;
    }

    private static int ArrangeRegularCoincidentDomains(in RegularBoundaryView input,BufferOffset first,BufferOffset second,
        Span<RegularFaceCut> cuts,ref BufferCount cutCount,ref SheetArrangement model)
    {
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        var a=input.Faces[first];var b=input.Faces[second];
        for(var al=0;al<a.LoopCount;al++)
        {
            var loopA=input.Loops[a.FirstLoop+al];
            for(var ai=0;ai<loopA.CoedgeCount;ai++)
            {
                var edgeA=input.Edges[input.Coedges[loopA.FirstCoedge+ai].Edge];
                var intervalA=new PK_INTERVAL_s();intervalA.value[0]=edgeA.Start;intervalA.value[1]=edgeA.End;
                var error=ResolveIntersectionCurve(edgeA.Curve,intervalA,out var spanA);if(error!=0)return error;
                for(var bl=0;bl<b.LoopCount;bl++)
                {
                    var loopB=input.Loops[b.FirstLoop+bl];
                    for(var bi=0;bi<loopB.CoedgeCount;bi++)
                    {
                        var edgeB=input.Edges[input.Coedges[loopB.FirstCoedge+bi].Edge];
                        var intervalB=new PK_INTERVAL_s();intervalB.value[0]=edgeB.Start;intervalB.value[1]=edgeB.End;
                        error=ResolveIntersectionCurve(edgeB.Curve,intervalB,out var spanB);if(error!=0)return error;
                        var status=IntersectCurveSpans(spanA,spanB,roots,out var nr);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                        for(var r=0;r<nr;r++){error=model.AddPoint(roots[r].Point,-1,out _);if(error!=0)return error;}
                    }
                }
            }
        }
        for(var side=0;side<2;side++)
        {
            var face=side==0?a:b;
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=input.Loops[face.FirstLoop+l];
                for(var k=0;k<loop.CoedgeCount;k++)
                {
                    var edge=input.Edges[input.Coedges[loop.FirstCoedge+k].Edge];
                    if(cutCount==cuts.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                    cuts[cutCount++]=new(){First=first,Second=second,Cut=new(){Curve=edge.Curve,First=edge.Start,Last=edge.End,Closed=edge.StartVertex<0?(byte)1:(byte)0,KeepImprint=1}};
                }
            }
        }
        return 0;
    }

    private static int RegularCoincidentMaterial(in RegularBoundaryView input,SurfTag support,in KernelVector3 point,
        in KernelVector3 outward,BufferOffset first,BufferOffset last,out bool handled,out bool minus,out bool plus)
    {
        handled=minus=plus=false;SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[4];
        for(var f=first;f<last;f++)
        {
            var face=input.Faces[f];if(!RegularCoincidentSupports(support,face.Surface))continue;
            var slot=GetSurfaceSlotByTag(face.Surface);var status=IntersectionSurfaceParameters(Surfaces[slot],point,out var uv);
            if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var error=RegularFaceContains(input,f,uv,out var inside);if(error!=0)return error;if(!inside)continue;
            if(handled)return ParasolidConstants.PK_ERROR_non_manifold;
            status=EvaluateSurfaceCore(in Surfaces[slot],uv.param[0],uv.param[1],layout,jet,0);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var normal=Scale(ProfileCross(jet[layout.GetIndex(1,0)],jet[layout.GetIndex(0,1)]),LocalSenseSign(face.Sense)*LocalSenseSign(Surfaces[slot].Sense));
            var agreement=Dot(normal,outward);if(agreement==0)return ParasolidConstants.PK_ERROR_at_singularity;
            handled=true;minus=agreement>0;plus=!minus;
        }
        return 0;
    }

    private static int EquivalentRegularEdges(in ModelingEdge edge,in ModelingEdge previous,Span<OperationIntersectionPoint> roots,out bool equivalent,out byte reverse)
    {
        equivalent=false;reverse=0;var same=edge.StartVertex==previous.StartVertex&&edge.EndVertex==previous.EndVertex;
        var opposite=edge.StartVertex==previous.EndVertex&&edge.EndVertex==previous.StartVertex;if(!same&&!opposite)return 0;
        var ia=new PK_INTERVAL_s();ia.value[0]=edge.Start;ia.value[1]=edge.End;
        var ib=new PK_INTERVAL_s();ib.value[0]=previous.Start;ib.value[1]=previous.End;
        if(ResolveIntersectionCurve(edge.Curve,ia,out var a)!=0||ResolveIntersectionCurve(previous.Curve,ib,out var b)!=0)return 0;
        var status=IntersectCurveSpans(a,b,roots,out var n);if(status==AlgorithmStatus.Unsupported||status==AlgorithmStatus.NotConverged)return 0;
        if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        if(n!=2||roots[0].Kind!=IntersectionVectorKind.Start||roots[1].Kind!=IntersectionVectorKind.End)return 0;
        var epsilon=1e-10*Math.Max(1,Math.Max(a.Upper-a.Lower,b.Upper-b.Lower));
        if(Math.Abs(roots[0].First-a.Lower)>epsilon||Math.Abs(roots[1].First-a.Upper)>epsilon
            ||Math.Abs(Math.Min(roots[0].Second,roots[1].Second)-b.Lower)>epsilon||Math.Abs(Math.Max(roots[0].Second,roots[1].Second)-b.Upper)>epsilon)return 0;
        var parameterDirection=roots[1].Second-roots[0].Second;if(parameterDirection==0)return 0;
        var flip=parameterDirection*LocalSenseSign(Curves[GetCurveSlotByTag(edge.Curve)].Sense)*LocalSenseSign(Curves[GetCurveSlotByTag(previous.Curve)].Sense)<0;
        if(edge.StartVertex>=0&&edge.StartVertex!=edge.EndVertex&&flip==same)return ParasolidConstants.PK_ERROR_bad_loop;
        equivalent=true;reverse=flip?(byte)1:(byte)0;return 0;
    }

    private static int UnifyRegularCoincidentEdges(ref SheetArrangement model)
    {
        Span<BufferOffset> canonical=stackalloc BufferOffset[model.NE];Span<byte> reverse=stackalloc byte[model.NE];reverse.Clear();
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        for(var e=0;e<model.NE;e++)
        {
            canonical[e]=e;var edge=model.Edges[e];
            for(var p=0;p<e;p++)
            {
                if(canonical[p]!=p)continue;var error=EquivalentRegularEdges(edge,model.Edges[p],roots,out var equivalent,out var flip);if(error!=0)return error;if(!equivalent)continue;
                canonical[e]=p;reverse[e]=flip;break;
            }
        }
        for(var c=0;c<model.NC;c++){ref var coedge=ref model.Coedges[c];coedge.Forward^=reverse[coedge.Edge];coedge.Edge=canonical[coedge.Edge];}
        return 0;
    }

    private static int ReparameterizeRegularEdge(in ModelingEdge carrier,in ModelingEdge edge,Span<OperationIntersectionPoint> roots,
        out bool handled,out ModelingEdge mapped,out byte reverse)
    {
        handled=false;mapped=edge;reverse=0;var record=Curves[GetCurveSlotByTag(carrier.Curve)];
        var ia=new PK_INTERVAL_s();ia.value[0]=record.TMin;ia.value[1]=record.TMax;var ib=new PK_INTERVAL_s();ib.value[0]=edge.Start;ib.value[1]=edge.End;
        if(ResolveIntersectionCurve(carrier.Curve,ia,out var a)!=0||ResolveIntersectionCurve(edge.Curve,ib,out var b)!=0)return 0;
        var status=IntersectCurveSpans(a,b,roots,out var n);if(status is AlgorithmStatus.Unsupported or AlgorithmStatus.NotConverged)return 0;if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        if(n!=2||roots[0].Kind!=IntersectionVectorKind.Start||roots[1].Kind!=IntersectionVectorKind.End)return 0;
        var epsilon=1e-10*Math.Max(1,b.Upper-b.Lower);
        if(Math.Abs(Math.Min(roots[0].Second,roots[1].Second)-b.Lower)>epsilon||Math.Abs(Math.Max(roots[0].Second,roots[1].Second)-b.Upper)>epsilon)return 0;
        var direction=roots[1].Second-roots[0].Second;if(direction==0)return 0;
        var flip=direction*LocalSenseSign(record.Sense)*LocalSenseSign(Curves[GetCurveSlotByTag(edge.Curve)].Sense)<0;
        mapped.Curve=carrier.Curve;mapped.Start=roots[0].First;mapped.End=roots[1].First;
        if(flip)(mapped.StartVertex,mapped.EndVertex)=(mapped.EndVertex,mapped.StartVertex);reverse=flip?(byte)1:(byte)0;handled=true;return 0;
    }
}

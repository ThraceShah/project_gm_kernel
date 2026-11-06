using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private readonly ref struct RegularBoundaryView(ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> coedges,
        ReadOnlySpan<ModelingLoop> loops,ReadOnlySpan<ModelingFace> faces)
    {
        internal ReadOnlySpan<ModelingEdge> Edges{get;}=edges;
        internal ReadOnlySpan<ModelingCoedge> Coedges{get;}=coedges;
        internal ReadOnlySpan<ModelingLoop> Loops{get;}=loops;
        internal ReadOnlySpan<ModelingFace> Faces{get;}=faces;
    }

    private static int ReadRegularBoundaries(ReadOnlySpan<BodyTag> bodies,Span<KernelVector3> points,Span<VertexSlot> vertices,
        Span<ModelingEdge> edges,Span<EdgeSlot> sourceEdges,Span<ModelingCoedge> coedges,Span<ModelingLoop> loops,
        Span<ModelingFace> faces,Span<FaceSlot> sources,Span<BufferOffset> owners,Span<BufferOffset> offsets,
        out BufferCount np,out BufferCount ne,out BufferCount nc,out BufferCount nl,out BufferCount nf)
    {
        np=ne=nc=nl=nf=0;
        for(var b=0;b<bodies.Length;b++)
        {
            offsets[b]=nf;var slot=TagRec(bodies[b]).Slot;if(Xt.IsOpaque(slot))return ParasolidConstants.PK_ERROR_not_implemented;
            var face=Bodies[slot].FirstFaceBody;
            for(var f=0;f<Bodies[slot].FaceCountBody;f++,face=Faces[face].NextInBody)
            {
                if(nf==faces.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                var record=Faces[face];var sf=GetSurfaceSlotByTag(record.SurfTag);if(sf<0)return ParasolidConstants.PK_ERROR_missing_geom;
                if(record.LoopCount==0||Surfaces[sf].Class is not (SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Sphere or SurfaceClass.BSurface or SurfaceClass.BlendSurface))return ParasolidConstants.PK_ERROR_not_implemented;
                if(Surfaces[sf].Class==SurfaceClass.BSurface)
                {
                    var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[sf].DataIndex]);
                    if(view.UPeriodic||view.VPeriodic||!GeneralSurfaceContinuity(view)||view.UDegree>16||view.VDegree>16)return ParasolidConstants.PK_ERROR_not_implemented;
                }
                sources[nf]=face;owners[nf]=b;faces[nf++]=new(){Surface=record.SurfTag,Sense=NormalizeLocalSense(record.Orientation),FirstLoop=nl,LoopCount=record.LoopCount};
                var loop=record.FirstLoop;
                for(var l=0;l<record.LoopCount;l++,loop=Loops[loop].NextInFace)
                {
                    if(nl==loops.Length||nc>coedges.Length-Loops[loop].FinCount)return ParasolidConstants.PK_ERROR_not_implemented;
                    loops[nl++]=new(){FirstCoedge=nc,CoedgeCount=Loops[loop].FinCount};var fin=Loops[loop].FirstFin;
                    for(var k=0;k<Loops[loop].FinCount;k++,fin=Fins[fin].NextInLoop)
                    {
                        var old=Fins[fin].Edge;var index=sourceEdges[..ne].IndexOf(old);
                        if(index<0)
                        {
                            if(ne==edges.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                            var edge=Edges[old];if(edge.CurveTag<=0||!TryProfileEdgeRange(edge,out var lo,out var hi))return ParasolidConstants.PK_ERROR_not_implemented;
                            BufferOffset start=-1,end=-1;
                            for(var side=0;side<2;side++)
                            {
                                var oldVertex=side==0?edge.StartVertex:edge.EndVertex;var v=-1;
                                if(oldVertex>=0)
                                {
                                    v=vertices[..np].IndexOf(oldVertex);
                                    if(v<0){if(np==points.Length)return ParasolidConstants.PK_ERROR_not_implemented;var error=SheetOriginalVertex(oldVertex,out var p);if(error!=0)return error;v=np;vertices[np]=oldVertex;points[np++]=p;}
                                }
                                if(side==0)start=v;else end=v;
                            }
                            index=ne;sourceEdges[ne]=old;edges[ne++]=new(){Curve=edge.CurveTag,Start=lo,End=hi,StartVertex=start,EndVertex=end};
                        }
                        coedges[nc++]=new(){Edge=index,Forward=Fins[fin].Sense=='+'?(byte)1:(byte)0};
                    }
                }
                if(Surfaces[sf].Class==SurfaceClass.Sphere)
                {
                    var error=ValidateSphericalFaceDomain(face);if(error!=0)return error;
                }
            }
        }
        offsets[bodies.Length]=nf;return 0;
    }

    private static int RegularFaceContains(in RegularBoundaryView view,BufferOffset face,PK_UV_s query,out bool inside)
    {
        inside=false;var record=view.Faces[face];var winding=0;
        for(var i=0;i<record.LoopCount;i++)
        {
            var loop=view.Loops[record.FirstLoop+i];var error=SheetLoopWinding(record.Surface,view.Edges,view.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount),query,out var w);
            if(error!=0)return error;winding+=w;
        }
        inside=winding!=0;return 0;
    }

    private static int RegularClassify(in RegularBoundaryView view,ReadOnlySpan<BufferOffset> faces,ReadOnlySpan<byte> reversed,
        in KernelVector3 point,out BufferCount winding,out BufferOffset nearest)
    {
        winding=0;nearest=-1;
        for(var attempt=0;attempt<6;attempt++)
        {
            var direction=Vector(1,.371+attempt*.173,.619+attempt*.137);ProfileNormalize(ref direction);
            var error=RegularRayWinding(view,faces,reversed,point,direction,false,out winding,out nearest);
            if(error==0)return 0;if(error is not (ParasolidConstants.PK_ERROR_cant_do_intersect or ParasolidConstants.PK_ERROR_eval_failure))return error;
        }
        return ParasolidConstants.PK_ERROR_cant_do_intersect;
    }

    private static int RegularRayWinding(in RegularBoundaryView view,ReadOnlySpan<BufferOffset> faces,ReadOnlySpan<byte> reversed,
        in KernelVector3 point,in KernelVector3 direction,bool ignoreOrigin,out BufferCount winding,out BufferOffset nearest)
    {
        winding=0;nearest=-1;var closest=double.PositiveInfinity;
            Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
            Span<double> hits=stackalloc double[SheetEdgeLimit];var hitCount=0;
            SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[4];
            foreach(var face in faces)
            {
                var record=view.Faces[face];var slot=GetSurfaceSlotByTag(record.Surface);BufferCount n;var bounded=false;
                if(TryPrepareSurface(Surfaces[slot],out var analytic)&&analytic.Kind==SurfaceClass.Plane)
                {
                    var denominator=Dot(direction,analytic.Axis);var distance=Dot(Sub(analytic.Origin,point),analytic.Axis);
                    if(Math.Abs(denominator)<1e-12){if(Math.Abs(distance)<1e-8)return ParasolidConstants.PK_ERROR_cant_do_intersect;continue;}
                    var t=distance/denominator;if(t< -1e-9)continue;n=1;roots[0]=new(){Point=Add(point,Scale(direction,t)),First=t,Kind=IntersectionVectorKind.Simple};
                }
                else if(analytic.Kind==SurfaceClass.Cylinder)
                {
                    var radial=Sub(point,analytic.Origin);radial=Sub(radial,Scale(analytic.Axis,Dot(radial,analytic.Axis)));
                    var transverse=Sub(direction,Scale(analytic.Axis,Dot(direction,analytic.Axis)));var speed=OperationIntersection.Norm(transverse);
                    if(speed<1e-12)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    var upper=(OperationIntersection.Norm(radial)+analytic.Radius)/speed;
                    var status=OperationIntersection.AnalyticLine(analytic,point,direction,0,upper+1e-8*Math.Max(1,upper),roots,out n);
                    if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                }
                else if(analytic.Kind==SurfaceClass.Sphere)
                {
                    var upper=OperationIntersection.Norm(Sub(point,analytic.Origin))+analytic.Radius;
                    var status=OperationIntersection.AnalyticLine(analytic,point,direction,0,upper+1e-8*Math.Max(1,upper),roots,out n);
                    if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                }
                else
                {
                    var status=RegularSurfaceExtent(slot,out var extent);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    var maximum=(new IntervalNumber(extent.XLo,extent.XHi)-point.X)*direction.X+(new IntervalNumber(extent.YLo,extent.YHi)-point.Y)*direction.Y+(new IntervalNumber(extent.ZLo,extent.ZHi)-point.Z)*direction.Z;
                    if(maximum.Upper<0)continue;var upper=Math.Max(1,maximum.Upper+1e-8*Math.Max(1,maximum.Magnitude));
                    status=IntersectRegularRay(slot,point,direction,upper,roots,out n);if(status!=AlgorithmStatus.Success)return IntersectionError(status);bounded=true;
                }
                for(var r=0;r<n;r++)
                {
                    var root=roots[r];if(root.Kind!=IntersectionVectorKind.Simple)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    var uv=new PK_UV_s();AlgorithmStatus status;
                    if(bounded){uv.param[0]=root.U;uv.param[1]=root.V;}
                    else{status=IntersectionSurfaceParameters(Surfaces[slot],root.Point,out uv);if(status!=AlgorithmStatus.Success)return IntersectionError(status);}
                    var error=RegularFaceContains(view,face,uv,out var inside);if(error!=0)return error;if(!inside)continue;
                    if(root.First<1e-9){if(ignoreOrigin)continue;return ParasolidConstants.PK_ERROR_cant_do_intersect;}
                    for(var k=0;k<hitCount;k++)if(Math.Abs(hits[k]-root.First)<1e-8)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    if(hitCount==hits.Length)return ParasolidConstants.PK_ERROR_not_implemented;hits[hitCount++]=root.First;
                    status=EvaluateSurfaceCore(in Surfaces[slot],uv.param[0],uv.param[1],in layout,jet,0);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                    var normal=ProfileCross(jet[layout.GetIndex(1,0)],jet[layout.GetIndex(0,1)]);var norm=OperationIntersection.Norm(normal);
                    var crossing=Dot(normal,direction)*LocalSenseSign(record.Sense)*LocalSenseSign(Surfaces[slot].Sense)*(reversed.IsEmpty||reversed[face]==0?1:-1);
                    if(Math.Abs(crossing)<1e-10*norm)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    winding+=crossing>0?1:-1;if(root.First<closest){closest=root.First;nearest=face;}
                }
            }
            return 0;
    }
}

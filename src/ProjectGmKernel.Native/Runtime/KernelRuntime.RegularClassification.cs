using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal enum RegularClassificationStage:byte{Read,Vertices,Edges,Faces,Ray,Complete}
    [ThreadStatic] internal static RegularClassificationStage LastRegularClassificationStage;
    [ThreadStatic] internal static EntityTag LastRegularClassificationEntity;
    private static int TryClassifyRegularBody(BodySlot body,in KernelVector3 point,KernelClassCode* enclosure,EntityTag* topology,out bool handled)
    {
        handled=false;if(Xt.IsOpaque(body)||Bodies[body].BodyType!=ParasolidConstants.PK_BODY_type_solid_c)return ParasolidConstants.PK_ERROR_not_implemented;
        var f=Bodies[body].FirstFaceBody;var curved=false;
        for(var i=0;i<Bodies[body].FaceCountBody;i++,f=Faces[f].NextInBody)
        {
            var slot=GetSurfaceSlotByTag(Faces[f].SurfTag);if(slot<0||Surfaces[slot].Class is not (SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Sphere or SurfaceClass.BSurface or SurfaceClass.BlendSurface))return ParasolidConstants.PK_ERROR_not_implemented;
            if(Surfaces[slot].Class==SurfaceClass.Sphere&&Faces[f].LoopCount==0)return ParasolidConstants.PK_ERROR_not_implemented;
            curved|=Surfaces[slot].Class!=SurfaceClass.Plane;
        }
        if(!curved)return ParasolidConstants.PK_ERROR_not_implemented;handled=true;
        var scratch=CommandScratch.Current;var memory=scratch.Take(80000);if(memory.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes=MemoryMarshal.AsBytes(memory);var cursor=0;
            var points=ModelSpan<KernelVector3>(bytes,ref cursor,SheetPointLimit);var vertices=ModelSpan<VertexSlot>(bytes,ref cursor,SheetPointLimit);
            var edges=ModelSpan<ModelingEdge>(bytes,ref cursor,SheetEdgeLimit);var sourceEdges=ModelSpan<EdgeSlot>(bytes,ref cursor,SheetEdgeLimit);
            var coedges=ModelSpan<ModelingCoedge>(bytes,ref cursor,SheetCoedgeLimit);var loops=ModelSpan<ModelingLoop>(bytes,ref cursor,SheetLoopLimit);
            var faces=ModelSpan<ModelingFace>(bytes,ref cursor,SheetFaceLimit);var sources=ModelSpan<FaceSlot>(bytes,ref cursor,SheetFaceLimit);var owners=ModelSpan<BufferOffset>(bytes,ref cursor,SheetFaceLimit);
            if(cursor>bytes.Length)return ParasolidConstants.PK_ERROR_memory_full;
            Span<BodyTag> input=stackalloc BodyTag[1]{Bodies[body].Header.Tag};Span<BufferOffset> offsets=stackalloc BufferOffset[2];
            LastRegularClassificationStage=RegularClassificationStage.Read;
            var error=ReadRegularBoundaries(input,points,vertices,edges,sourceEdges,coedges,loops,faces,sources,owners,offsets,out var np,out var ne,out var nc,out var nl,out var nf);if(error!=0)return error;
            LastRegularClassificationStage=RegularClassificationStage.Vertices;
            for(var v=0;v<np;v++)if(ProfileDistance(points[v],point)<=1e-8)
            {*enclosure=ParasolidConstants.PK_enclosure_on_c;*topology=Vertices[vertices[v]].Header.Tag;return 0;}
            var vector=new PK_VECTOR_s();WriteVector(&vector,point);
            LastRegularClassificationStage=RegularClassificationStage.Edges;
            for(var e=0;e<ne;e++)
            {
                LastRegularClassificationEntity=edges[e].Curve;
                if(RegularPointAwayFromIntersectionCurve(edges[e].Curve,point))continue;
                double t=0;error=CurveParameteriseVectorCore(edges[e].Curve,vector,&t);if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return error;
                LiftPeriodicEdgeParameter(edges[e].Curve,edges[e].Start,edges[e].End,ref t);
                if(t>=edges[e].Start-1e-10&&t<=edges[e].End+1e-10){*enclosure=ParasolidConstants.PK_enclosure_on_c;*topology=Edges[sourceEdges[e]].Header.Tag;return 0;}
            }
            var view=new RegularBoundaryView(edges[..ne],coedges[..nc],loops[..nl],faces[..nf]);
            LastRegularClassificationStage=RegularClassificationStage.Faces;
            for(var i=0;i<nf;i++)
            {
                LastRegularClassificationEntity=Faces[sources[i]].Header.Tag;
                var support=faces[i].Surface;if(RegularPointOutsideSupport(support,point))continue;
                if(RegularPointAwayFromBSurface(GetSurfaceSlotByTag(support),point))continue;
                var uv=new PK_UV_s();
                if(Surfaces[GetSurfaceSlotByTag(support)].Class==SurfaceClass.Sphere&&TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(support)],out var sphere))
                {
                    if(Math.Abs(Geometry.Intersection.OperationIntersection.Norm(Geometry.Evaluation.EvaluationMath.Sub(point,sphere.Origin))-sphere.Radius)>1e-8)continue;
                    var status=Geometry.Intersection.OperationIntersection.Parameters(sphere,point,out var u,out var v);if(status!=Computation.AlgorithmStatus.Success)return IntersectionError(status);
                    uv.param[0]=u;uv.param[1]=v;error=0;
                }
                else error=SurfParameteriseVectorCore(support,vector,&uv);
                if(error==ParasolidConstants.PK_ERROR_not_on_surface)continue;if(error!=0)return error;
                error=RegularFaceContains(view,i,uv,out var inside);if(error!=0)return error;
                if(inside){*enclosure=ParasolidConstants.PK_enclosure_on_c;*topology=Faces[sources[i]].Header.Tag;return 0;}
            }
            Span<BufferOffset> indices=stackalloc BufferOffset[nf];for(var i=0;i<nf;i++)indices[i]=i;
            LastRegularClassificationStage=RegularClassificationStage.Ray;
            error=RegularClassify(view,indices,default,point,out var winding,out var nearest);if(error!=0)return error;
            if(winding is not (0 or 1))return ParasolidConstants.PK_ERROR_bad_loop;
            *enclosure=winding==1?ParasolidConstants.PK_enclosure_inside_c:ParasolidConstants.PK_enclosure_outside_c;
            if(nearest>=0)
            {
                var face=Faces[sources[nearest]];var shell=winding==1?face.BackShell:face.FrontShell;
                if(shell<0)return ParasolidConstants.PK_ERROR_bad_loop;*topology=Regions[Shells[shell].Region].Header.Tag;
            }
            else
            {
                var region=Bodies[body].FirstRegion;
                for(var i=0;i<Bodies[body].RegionCount;i++,region=Regions[region].NextInBody)if(Regions[region].IsSolid==0){*topology=Regions[region].Header.Tag;break;}
            }
            LastRegularClassificationStage=RegularClassificationStage.Complete;return 0;
        }
        finally{scratch.Return(memory);}
    }

    // An intersection curve is a subset of each of its defining supports.
    // A certified plane residual excludes a point before an unnecessary
    // procedural inverse; an unproved residual leaves the original solver.
    private static bool RegularPointAwayFromIntersectionCurve(CurveTag curve,in KernelVector3 point)
    {
        var slot=GetCurveSlotByTag(curve);if(slot<0||Curves[slot].Class!=CurveClass.ICurve)return false;
        var data=ICurveDataPool[Curves[slot].DataIndex];
        Span<SurfTag> supports=stackalloc SurfTag[2]{data.Surface0Tag,data.Surface1Tag};
        foreach(var support in supports)
        {
            var surface=GetSurfaceSlotByTag(support);if(surface<0||Surfaces[surface].Class!=SurfaceClass.Plane||!TryPrepareSurface(Surfaces[surface],out var plane))continue;
            var residual=(new IntervalNumber(point.X)-plane.Origin.X)*plane.Axis.X+(new IntervalNumber(point.Y)-plane.Origin.Y)*plane.Axis.Y+(new IntervalNumber(point.Z)-plane.Origin.Z)*plane.Axis.Z;
            const double tolerance=2*Geometry.Intersection.OperationIntersection.DistanceTolerance;
            if(residual.Lower>tolerance||residual.Upper< -tolerance)return true;
        }
        return false;
    }

    // Membership only needs a separating bound, not a high-accuracy closest
    // point. A bounded search excludes off-surface vectors cheaply; unresolved
    // boxes still go through the public global projection contract.
    private static bool RegularPointAwayFromBSurface(SurfaceSlot slot,in KernelVector3 point)
    {
        if(Surfaces[slot].Class!=SurfaceClass.BSurface)return false;
        if(RegularSurfaceExtent(slot,out var extent)!=AlgorithmStatus.Success)return false;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<IntervalNumber> singleton=stackalloc IntervalNumber[4]{point.X,point.Y,point.Z,1};var domain=new SurfaceParameterBox(0,1,0,1);
        var query=new Geometry.Intersection.SurfaceIntersectionPatch(new BezierSurfacePatch(singleton,0,0,domain));var visited=0;
        for(var i=0;i<RegularSurfacePatchCount(slot);i++)
        {
            if(RegularSurfacePatch(slot,i,extent,poles,out var patch)!=AlgorithmStatus.Success)return false;
            if(patch.Domain.U1<=patch.Domain.U0||patch.Domain.V1<=patch.Domain.V0)continue;
            if(ParametricPatchSeparation.CertifyDisjoint(query,patch,domain,patch.Domain,2e-8,ref visited,2048)!=AlgorithmStatus.Success)return false;
        }
        return true;
    }
}

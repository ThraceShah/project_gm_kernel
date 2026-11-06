using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int TryClassifyRegularBody(BodySlot body,in KernelVector3 point,KernelClassCode* enclosure,EntityTag* topology,out bool handled)
    {
        handled=false;if(Xt.IsOpaque(body)||Bodies[body].BodyType!=ParasolidConstants.PK_BODY_type_solid_c)return ParasolidConstants.PK_ERROR_not_implemented;
        var f=Bodies[body].FirstFaceBody;var curved=false;
        for(var i=0;i<Bodies[body].FaceCountBody;i++,f=Faces[f].NextInBody)
        {
            var slot=GetSurfaceSlotByTag(Faces[f].SurfTag);if(slot<0||Surfaces[slot].Class is not (SurfaceClass.Plane or SurfaceClass.BSurface or SurfaceClass.BlendSurface))return ParasolidConstants.PK_ERROR_not_implemented;
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
            var error=ReadRegularBoundaries(input,points,vertices,edges,sourceEdges,coedges,loops,faces,sources,owners,offsets,out var np,out var ne,out var nc,out var nl,out var nf);if(error!=0)return error;
            for(var v=0;v<np;v++)if(ProfileDistance(points[v],point)<=1e-8)
            {*enclosure=ParasolidConstants.PK_enclosure_on_c;*topology=Vertices[vertices[v]].Header.Tag;return 0;}
            var vector=new PK_VECTOR_s();WriteVector(&vector,point);
            for(var e=0;e<ne;e++)
            {
                double t=0;error=CurveParameteriseVectorCore(edges[e].Curve,vector,&t);if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return error;
                if(t>=edges[e].Start-1e-10&&t<=edges[e].End+1e-10){*enclosure=ParasolidConstants.PK_enclosure_on_c;*topology=Edges[sourceEdges[e]].Header.Tag;return 0;}
            }
            var view=new RegularBoundaryView(edges[..ne],coedges[..nc],loops[..nl],faces[..nf]);
            for(var i=0;i<nf;i++)
            {
                var support=faces[i].Surface;if(RegularPointOutsideSupport(support,point))continue;
                if(RegularPointAwayFromBSurface(GetSurfaceSlotByTag(support),point))continue;
                var uv=new PK_UV_s();error=SurfParameteriseVectorCore(support,vector,&uv);
                if(error==ParasolidConstants.PK_ERROR_not_on_surface)continue;if(error!=0)return error;
                error=RegularFaceContains(view,i,uv,out var inside);if(error!=0)return error;
                if(inside){*enclosure=ParasolidConstants.PK_enclosure_on_c;*topology=Faces[sources[i]].Header.Tag;return 0;}
            }
            Span<BufferOffset> indices=stackalloc BufferOffset[nf];for(var i=0;i<nf;i++)indices[i]=i;
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
            return 0;
        }
        finally{scratch.Return(memory);}
    }

    // Membership only needs a separating bound, not a high-accuracy closest
    // point. A bounded search excludes off-surface vectors cheaply; unresolved
    // boxes still go through the public global projection contract.
    private static bool RegularPointAwayFromBSurface(SurfaceSlot slot,in KernelVector3 point)
    {
        if(Surfaces[slot].Class!=SurfaceClass.BSurface)return false;
        if(RegularSurfaceExtent(slot,out var extent)!=AlgorithmStatus.Success)return false;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bounds=stackalloc IntervalNumber[9];
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[16];Span<BufferCount> depths=stackalloc BufferCount[16];
        for(var i=0;i<RegularSurfacePatchCount(slot);i++)
        {
            if(RegularSurfacePatch(slot,i,extent,poles,out var patch)!=AlgorithmStatus.Success)return false;
            if(patch.Domain.U1<=patch.Domain.U0||patch.Domain.V1<=patch.Domain.V0)continue;
            pending[0]=patch.Domain;depths[0]=0;var n=1;
            while(n>0)
            {
                var box=pending[--n];var depth=depths[n];if(patch.Bound(box,bounds)!=AlgorithmStatus.Success)return false;
                if(point.X<bounds[0].Lower-1e-8||point.X>bounds[0].Upper+1e-8||point.Y<bounds[1].Lower-1e-8||point.Y>bounds[1].Upper+1e-8||point.Z<bounds[2].Lower-1e-8||point.Z>bounds[2].Upper+1e-8)continue;
                if(depth==8)return false;var u=(box.U1-box.U0)/(patch.Domain.U1-patch.Domain.U0);var v=(box.V1-box.V0)/(patch.Domain.V1-patch.Domain.V0);
                if(u>=v){var mid=box.U0+(box.U1-box.U0)*.5;pending[n]=new(box.U0,mid,box.V0,box.V1);pending[n+1]=new(mid,box.U1,box.V0,box.V1);}
                else{var mid=box.V0+(box.V1-box.V0)*.5;pending[n]=new(box.U0,box.U1,box.V0,mid);pending[n+1]=new(box.U0,box.U1,mid,box.V1);}
                depths[n]=depths[n+1]=depth+1;n+=2;
            }
        }
        return true;
    }
}

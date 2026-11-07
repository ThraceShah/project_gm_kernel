using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal enum SheetPartitionStage:byte { None,BoundaryIntersections,BoundaryFragment,CurveDiscovery,CutClip,CurveEvaluation,SurfaceInverse,UVTangent,RayIntersections,RayEndpoint,RayTangency,CycleTrace,CycleWitness,CycleNesting,CellClassification,TopologyBuild,BoundaryAssociation,PeriodicArc }
    [ThreadStatic] internal static SheetPartitionStage LastSheetPartitionStage;
    [ThreadStatic] internal static CurveTag LastSheetCurve;
    [ThreadStatic] internal static double LastSheetParameter;
    [ThreadStatic] internal static Computation.AlgorithmStatus LastSheetAlgorithmStatus;
    [ThreadStatic] internal static double LastSheetRayU,LastSheetRayV;
    private static int PartitionGeneralSheet(BodySlot body,SurfTag plane,double tolerance,out BodyTag partition,
        Span<FaceSlot> products,Span<FaceSlot> sourceFaces,Span<sbyte> sides,out BufferCount faceCount,
        Span<EdgeSlot> cutEdges,out BufferCount cutCount,out bool changed)
    {
        partition=0;faceCount=cutCount=0;changed=false;
        LastSheetPartitionStage=SheetPartitionStage.None;
        if(Xt.IsOpaque(body)||Bodies[body].BodyType!=ParasolidConstants.PK_BODY_type_sheet_c||Bodies[body].FaceCountBody>SheetFaceLimit
            ||Bodies[body].EdgeCountBody>SheetEdgeLimit)return ParasolidConstants.PK_ERROR_not_implemented;
        var source=Bodies[body].FirstFaceBody;
        for(var i=0;i<Bodies[body].FaceCountBody;i++,source=Faces[source].NextInBody)
        {
            var support=GetSurfaceSlotByTag(Faces[source].SurfTag);
            if(support<0)return ParasolidConstants.PK_ERROR_missing_geom;
            if(Surfaces[support].Class==SurfaceClass.Sphere)
            {var domainError=ValidateSphericalFaceDomain(source);if(domainError!=0)return domainError;continue;}
            if(Surfaces[support].Class==SurfaceClass.BlendSurface)continue;
            if(Surfaces[support].Class!=SurfaceClass.BSurface)return ParasolidConstants.PK_ERROR_not_implemented;
            var data=BSurfaceDataStore[Surfaces[support].DataIndex];
            if(data.IsUClosed!=0||data.IsVClosed!=0)return ParasolidConstants.PK_ERROR_not_implemented;
            var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[support].DataIndex]);
            if(!GeneralSurfaceContinuity(view))return ParasolidConstants.PK_ERROR_not_implemented;
        }
        var scratch=CommandScratch.Current;var memory=scratch.Take(160000);
        if(memory.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes=MemoryMarshal.AsBytes(memory);var cursor=0;
            var model=new SheetArrangement
            {
                Points=ModelSpan<KernelVector3>(bytes,ref cursor,SheetPointLimit),OriginalVertices=ModelSpan<VertexSlot>(bytes,ref cursor,SheetPointLimit),
                Edges=ModelSpan<ModelingEdge>(bytes,ref cursor,SheetEdgeLimit),CutEdges=ModelSpan<byte>(bytes,ref cursor,SheetEdgeLimit),
                Coedges=ModelSpan<ModelingCoedge>(bytes,ref cursor,SheetCoedgeLimit),Loops=ModelSpan<ModelingLoop>(bytes,ref cursor,SheetLoopLimit),
                Faces=ModelSpan<ModelingFace>(bytes,ref cursor,SheetFaceLimit),Sources=ModelSpan<FaceSlot>(bytes,ref cursor,SheetFaceLimit),
                Sides=ModelSpan<sbyte>(bytes,ref cursor,SheetFaceLimit),Tolerance=tolerance,
            };
            var sourceEdges=ModelSpan<SheetSourceEdge>(bytes,ref cursor,SheetEdgeLimit);
            var halfedges=ModelSpan<SheetHalfedge>(bytes,ref cursor,2*SheetEdgeLimit);
            var cycleCoedges=ModelSpan<ModelingCoedge>(bytes,ref cursor,2*SheetEdgeLimit);
            var cycles=ModelSpan<SheetCycle>(bytes,ref cursor,SheetLoopLimit);
            if(cursor>bytes.Length)return ParasolidConstants.PK_ERROR_memory_full;
            var error=ReadSheetBoundaryEdges(body,plane,ref model,sourceEdges,out var ns);if(error!=0)return error;
            for(var i=0;i<ns;i++)changed|=sourceEdges[i].Count>1;
            var boundaryCount=model.NE;
            Span<SheetCutCurve> cuts=stackalloc SheetCutCurve[GeneralIntersectionBranchLimit];
            SurfTag preparedSurface=0;BufferCount preparedCount=0;
            source=Bodies[body].FirstFaceBody;
            for(var i=0;i<Bodies[body].FaceCountBody;i++,source=Faces[source].NextInBody)
            {
                if(Faces[source].SurfTag!=preparedSurface)
                {
                    error=ReleaseUnusedSheetCuts(cuts[..preparedCount],ref model);if(error!=0)return error;
                    preparedSurface=Faces[source].SurfTag;
                    error=PrepareSheetCutCurves(preparedSurface,plane,cuts,out preparedCount);if(error!=0)return error;
                }
                error=ArrangeSheetFace(source,plane,sourceEdges[..ns],cuts[..preparedCount],ref model,halfedges,cycleCoedges,cycles);if(error!=0)return error;
            }
            error=ReleaseUnusedSheetCuts(cuts[..preparedCount],ref model);if(error!=0)return error;
            changed|=model.NE>boundaryCount||model.NF!=Bodies[body].FaceCountBody;
            if(model.NF>products.Length||model.NF>sourceFaces.Length||model.NF>sides.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            LastSheetPartitionStage=SheetPartitionStage.TopologyBuild;
            error=BuildModelingBrep(model.Points[..model.NP],model.Edges[..model.NE],model.Coedges[..model.NC],model.Loops[..model.NL],model.Faces[..model.NF],false,
                out partition,products[..model.NF]);if(error!=0)return error;
            model.Sources[..model.NF].CopyTo(sourceFaces);model.Sides[..model.NF].CopyTo(sides);faceCount=model.NF;
            var edge=Bodies[TagRec(partition).Slot].FirstEdgeBody;
            for(var i=0;i<model.NE;i++,edge=Edges[edge].NextInBody)if(model.CutEdges[i]!=0)
            { if(cutCount==cutEdges.Length)return ParasolidConstants.PK_ERROR_not_implemented;cutEdges[cutCount++]=edge; }
            return 0;
        }
        finally{scratch.Return(memory);}
    }

    private static int ReleaseUnusedSheetCuts(ReadOnlySpan<SheetCutCurve> cuts,ref SheetArrangement model)
    {
        foreach(ref readonly var cut in cuts)
        {
            var used=false;for(var e=0;e<model.NE;e++)if(model.Edges[e].Curve==cut.Curve)used=true;
            if(!used){var curve=cut.Curve;var error=EntityDelete(1,&curve);if(error!=0)return error;}
        }
        return 0;
    }

    private static int ImprintGeneralSheet(BodySlot body,SurfTag plane,double tolerance,int* count,EntityTag** returned)
    {
        Span<FaceSlot> products=stackalloc FaceSlot[SheetFaceLimit],sources=stackalloc FaceSlot[SheetFaceLimit];
        Span<sbyte> sides=stackalloc sbyte[SheetFaceLimit];Span<EdgeSlot> cuts=stackalloc EdgeSlot[SheetEdgeLimit];
        var error=PartitionGeneralSheet(body,plane,tolerance,out var partition,products,sources,sides,out var nf,cuts,out var nc,out var changed);if(error!=0)return error;
        if(!changed)
        {
            error=EntityDelete(1,&partition);if(error!=0)return error;
            var edge=Bodies[body].FirstEdgeBody;var found=0;
            Span<Geometry.Intersection.OperationIntersectionPoint> roots=stackalloc Geometry.Intersection.OperationIntersectionPoint[IntersectionPointCapacity];
            for(var i=0;i<Bodies[body].EdgeCountBody;i++,edge=Edges[edge].NextInBody)
            {
                if(!TryProfileEdgeRange(Edges[edge],out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
                var status=IntersectSheetCurvePlane(Edges[edge].CurveTag,a,b,plane,roots,out var n);if(status!=Computation.AlgorithmStatus.Success)return IntersectionError(status);
                if(n==2&&roots[0].Kind==Geometry.Intersection.IntersectionVectorKind.Start&&roots[1].Kind==Geometry.Intersection.IntersectionVectorKind.End)cuts[found++]=edge;
            }
            var kept=found==0?null:AllocateReturnSlice(found);if(found!=0&&kept==null)return ParasolidConstants.PK_ERROR_memory_full;
            for(var i=0;i<found;i++)kept[i]=Edges[cuts[i]].Header.Tag;*count=found;*returned=kept;return 0;
        }
        var edges=nc==0?null:AllocateReturnSlice(nc);if(nc!=0&&edges==null)return ParasolidConstants.PK_ERROR_memory_full;
        for(var i=0;i<nc;i++)edges[i]=Edges[cuts[i]].Header.Tag;
        Span<FaceSlot> originals=stackalloc FaceSlot[SheetFaceLimit],replacements=stackalloc FaceSlot[SheetFaceLimit];var nOriginal=0;
        for(var i=0;i<nf;i++)if(!originals[..nOriginal].Contains(sources[i])){originals[nOriginal]=sources[i];replacements[nOriginal++]=products[i];}
        error=PreserveModelingFaceHandles(body,TagRec(partition).Slot,originals[..nOriginal],replacements[..nOriginal]);if(error!=0)return error;
        error=ReplaceProfileBody(body,TagRec(partition).Slot);if(error!=0)return error;
        *count=nc;*returned=edges;return 0;
    }

    private static int SectionGeneralSheet(BodySlot body,SurfTag plane,PK_BODY_section_o_s* options,PK_section_r_s* result)
    {
        Span<FaceSlot> faces=stackalloc FaceSlot[SheetFaceLimit],sources=stackalloc FaceSlot[SheetFaceLimit];
        Span<sbyte> sides=stackalloc sbyte[SheetFaceLimit];Span<EdgeSlot> cuts=stackalloc EdgeSlot[SheetEdgeLimit];
        var error=PartitionGeneralSheet(body,plane,options->default_tol,out var partition,faces,sources,sides,out var nf,cuts,out _,out var changed);if(error!=0)return error;
        var oneSide=true;for(var f=1;f<nf;f++)if(sides[f]!=sides[0])oneSide=false;
        if(!changed&&oneSide)
        {
            error=EntityDelete(1,&partition);if(error!=0)return error;var tag=Bodies[body].Header.Tag;
            var front=sides[0]>0;
            if(front&&options->fence==ParasolidConstants.PK_section_fence_back_c||!front&&options->fence==ParasolidConstants.PK_section_fence_front_c)return EntityDelete(1,&tag);
            var output=AllocateReturnSlice(1);if(output==null)return ParasolidConstants.PK_ERROR_memory_full;output[0]=tag;
            if(front)result->front_bodies=new(){length=1,array=output};else result->back_bodies=new(){length=1,array=output};return 0;
        }
        Span<byte> selected=stackalloc byte[nf];selected.Clear();Span<FaceSlot> component=stackalloc FaceSlot[nf],products=stackalloc FaceSlot[nf];
        Span<BodyTag> frontBodies=stackalloc BodyTag[nf],backBodies=stackalloc BodyTag[nf],all=stackalloc BodyTag[nf];
        Span<FaceSlot> original=stackalloc FaceSlot[nf],replacement=stackalloc FaceSlot[nf];var nOriginal=0;var nFront=0;var nBack=0;var nAll=0;
        for(var side=1;side>=-1;side-=2)
        {
            if(side>0&&options->fence==ParasolidConstants.PK_section_fence_back_c||side<0&&options->fence==ParasolidConstants.PK_section_fence_front_c)continue;
            for(var seed=0;seed<nf;seed++)
            {
                if(selected[seed]!=0||sides[seed]!=side)continue;
                component[0]=faces[seed];selected[seed]=1;var n=1;
                for(var current=0;current<n;current++)
                {
                    var loop=Faces[component[current]].FirstLoop;
                    for(var l=0;l<Faces[component[current]].LoopCount;l++,loop=Loops[loop].NextInFace)
                    {
                        var fin=Loops[loop].FirstFin;
                        for(var k=0;k<Loops[loop].FinCount;k++,fin=Fins[fin].NextInLoop)
                        {
                            var other=Fins[fin].Other;if(other<0||Fins[other].Loop<0)continue;
                            var adjacent=Loops[Fins[other].Loop].Face;var index=faces[..nf].IndexOf(adjacent);
                            if(index>=0&&selected[index]==0&&sides[index]==side){selected[index]=1;component[n++]=adjacent;}
                        }
                    }
                }
                error=CopyModelingFaces(component[..n],false,out var output,products[..n]);if(error!=0)return error;
                for(var i=0;i<n;i++)
                {
                    var source=sources[faces[..nf].IndexOf(component[i])];if(original[..nOriginal].Contains(source))continue;
                    original[nOriginal]=source;replacement[nOriginal++]=products[i];
                }
                all[nAll++]=output;if(side>0)frontBodies[nFront++]=output;else backBodies[nBack++]=output;
            }
        }
        Span<FaceSlot> originals=stackalloc FaceSlot[nOriginal],replacements=stackalloc FaceSlot[nOriginal];
        for(var b=0;b<nAll;b++)
        {
            var slot=TagRec(all[b]).Slot;var n=0;
            for(var i=0;i<nOriginal;i++)if(Shells[Faces[replacement[i]].BackShell].Body==slot){originals[n]=original[i];replacements[n++]=replacement[i];}
            if(n==0)continue;error=PreserveModelingFaceHandles(body,slot,originals[..n],replacements[..n]);if(error!=0)return error;
        }
        var tagBody=Bodies[body].Header.Tag;
        if(nAll==0){error=EntityDelete(1,&tagBody);if(error!=0)return error;}
        else
        {
            var first=all[0];error=ReplaceProfileBody(body,TagRec(first).Slot);if(error!=0)return error;
            for(var i=0;i<nFront;i++)if(frontBodies[i]==first)frontBodies[i]=tagBody;
            for(var i=0;i<nBack;i++)if(backBodies[i]==first)backBodies[i]=tagBody;
        }
        error=EntityDelete(1,&partition);if(error!=0)return error;
        var frontOutput=nFront==0?null:AllocateReturnSlice(nFront);var backOutput=nBack==0?null:AllocateReturnSlice(nBack);
        if(nFront!=0&&frontOutput==null||nBack!=0&&backOutput==null)return ParasolidConstants.PK_ERROR_memory_full;
        frontBodies[..nFront].CopyTo(new Span<BodyTag>(frontOutput,nFront));backBodies[..nBack].CopyTo(new Span<BodyTag>(backOutput,nBack));
        result->front_bodies=new(){length=nFront,array=frontOutput};result->back_bodies=new(){length=nBack,array=backOutput};return 0;
    }
}

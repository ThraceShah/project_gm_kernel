using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int BuildRollingArrangement(BodySlot body,ReadOnlySpan<FaceSlot> sourceFaces,ReadOnlySpan<EdgeSlot> selected,ReadOnlySpan<RollingBandCandidate> bands,
        ReadOnlySpan<RollingSubdivision> subdivisions,ReadOnlySpan<RollingPointContact> contacts,ReadOnlySpan<RollingCutFamily> families,
        ReadOnlySpan<SheetCutCurve> curves,Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;
        // Existing geometry stays immutable through clipping/arrangement;
        // construction clones have fresh identities. The command-local proof
        // cache is released before another modeling command can run.
        ModelingPerformance.Context proof=default;using var geometryBounds=new ModelingPerformance.Scope(&proof,immutableGeometry:true);
        var oldFaces=Bodies[body].FaceCountBody;var loopCount=0;var face=Bodies[body].FirstFaceBody;
        for(var f=0;f<oldFaces;f++,face=Faces[face].NextInBody)loopCount+=Faces[face].LoopCount;
        if(oldFaces+bands.Length>RegularInputFaces||Bodies[body].EdgeCountBody+4*bands.Length>RegularInputEdges
            ||loopCount+bands.Length>RegularInputLoops||2*Bodies[body].EdgeCountBody+4*bands.Length>RegularInputCoedges)return ParasolidConstants.PK_ERROR_not_implemented;
        var scratch=CommandScratch.Current;var memory=scratch.Take(249000);if(memory.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes=MemoryMarshal.AsBytes(memory);var cursor=0;
            var model=new SheetArrangement
            {
                Points=ModelSpan<KernelVector3>(bytes,ref cursor,SheetPointLimit),OriginalVertices=ModelSpan<VertexSlot>(bytes,ref cursor,SheetPointLimit),
                VertexPrecisions=ModelSpan<double>(bytes,ref cursor,SheetPointLimit),Parameters=ModelSpan<BoundaryParameter>(bytes,ref cursor,2*SheetPointLimit),
                CarrierSurfaces=ModelSpan<BoundaryCarrierSurface>(bytes,ref cursor,4*RegularInputEdges),RequireBoundaryAssociations=true,
                Tangencies=ModelSpan<BoundaryTangency>(bytes,ref cursor,2*SheetPointLimit),
                SurfaceContacts=ModelSpan<BoundarySurfaceContact>(bytes,ref cursor,2*SheetPointLimit),
                Edges=ModelSpan<ModelingEdge>(bytes,ref cursor,SheetEdgeLimit),CutEdges=ModelSpan<byte>(bytes,ref cursor,SheetEdgeLimit),
                Coedges=ModelSpan<ModelingCoedge>(bytes,ref cursor,SheetCoedgeLimit),Loops=ModelSpan<ModelingLoop>(bytes,ref cursor,SheetLoopLimit),
                Faces=ModelSpan<ModelingFace>(bytes,ref cursor,SheetFaceLimit),Sources=ModelSpan<FaceSlot>(bytes,ref cursor,SheetFaceLimit),
                Witnesses=ModelSpan<PK_UV_s>(bytes,ref cursor,SheetFaceLimit),Tolerance=1e-9,
            };
            var inputEdges=ModelSpan<ModelingEdge>(bytes,ref cursor,RegularInputEdges);var originalEdges=ModelSpan<EdgeSlot>(bytes,ref cursor,RegularInputEdges);originalEdges.Fill(-1);
            var inputCoedges=ModelSpan<ModelingCoedge>(bytes,ref cursor,RegularInputCoedges);var inputLoops=ModelSpan<ModelingLoop>(bytes,ref cursor,RegularInputLoops);
            var inputFaces=ModelSpan<ModelingFace>(bytes,ref cursor,RegularInputFaces);var sources=ModelSpan<FaceSlot>(bytes,ref cursor,RegularInputFaces);sources.Fill(-1);
            var owners=ModelSpan<BufferOffset>(bytes,ref cursor,RegularInputFaces);var offsets=ModelSpan<BufferOffset>(bytes,ref cursor,2);
            var fragments=ModelSpan<SheetSourceEdge>(bytes,ref cursor,RegularInputEdges);var owned=ModelSpan<SheetCutCurve>(bytes,ref cursor,RegularCurveLimit);var ownedCount=0;
            var cuts=ModelSpan<RegularFaceCut>(bytes,ref cursor,RegularCutLimit);var cutCount=0;
            var certificates=ModelSpan<FiniteCurveSurfaceResult>(bytes,ref cursor,RegularInputEdges);
            var halfedges=ModelSpan<SheetHalfedge>(bytes,ref cursor,2*SheetEdgeLimit);var cycleCoedges=ModelSpan<ModelingCoedge>(bytes,ref cursor,2*SheetEdgeLimit);
            var cycles=ModelSpan<SheetCycle>(bytes,ref cursor,SheetLoopLimit);if(cursor>bytes.Length)return ParasolidConstants.PK_ERROR_memory_full;
            Span<BodyTag> inputs=stackalloc BodyTag[1]{Bodies[body].Header.Tag};
            var error=ReadRegularBoundaries(inputs,model.Points,model.OriginalVertices,inputEdges,originalEdges,inputCoedges,inputLoops,inputFaces,sources,owners,offsets,
                out model.NP,out var ne,out var nc,out var nl,out var nf,vertexPrecisions:model.VertexPrecisions);if(error!=0)return error;
            LastRollingNetworkStage=RollingNetworkStage.PatchDomain;
            foreach(var band in bands)
            {error=AppendRollingPatchDomain(band,contacts,ref model,inputEdges,originalEdges,inputCoedges,inputLoops,inputFaces,ref ne,ref nc,ref nl,ref nf);if(error!=0)return error;}
            var view=new RegularBoundaryView(inputEdges[..ne],inputCoedges[..nc],inputLoops[..nl],inputFaces[..nf]);
            LastRollingNetworkStage=RollingNetworkStage.SourceAssociations;
            error=ReadRollingBoundaryAssociations(view,originalEdges[..ne],families,curves,ref model);if(error!=0)return error;
            LastRollingNetworkStage=RollingNetworkStage.BoundaryProofs;
            error=ReadRollingFiniteEvents(view,originalEdges[..ne],model.OriginalVertices[..model.NP],bands,subdivisions,contacts,certificates,out var certificateCount);if(error!=0)return error;
            LastRollingNetworkStage=RollingNetworkStage.Clip;
            foreach(var family in families)
            {
                LastRollingFirstFace=family.FirstFace;LastRollingSecondFace=family.SecondFace;var before=cutCount;
                error=ClipRegularFacePair(view,family.FirstFace,family.SecondFace,curves.Slice(family.FirstCurve,family.Count),cuts,ref cutCount,owned,ref ownedCount,ref model,certificates[..certificateCount]);
                if(error!=0)
                {
                    TraceRollingNetwork?.Invoke(new(){Stage=LastRollingNetworkStage,Surface=view.Faces[family.FirstFace].Surface,Other=view.Faces[family.SecondFace].Surface,Curve=LastSheetCurve,First=LastSheetParameter});
                    return error;
                }
                for(var c=before;c<cutCount;c++)cuts[c].SecondIsBoundary=family.SecondIsBoundary;
            }
            LastRollingNetworkStage=RollingNetworkStage.BoundaryIncidence;
            error=ReadRollingThirdSurfaceEvents(sourceFaces,bands,families,curves,ref model);if(error!=0)return error;
            if(!subdivisions.IsEmpty)
            {
                LastRollingNetworkStage=RollingNetworkStage.Subdivision;
                error=ImprintRollingSubdivisions(sourceFaces.Length,bands,subdivisions,cuts,ref cutCount,owned,ref ownedCount,ref model);if(error!=0)return error;
            }
            LastRollingNetworkStage=RollingNetworkStage.Split;
            error=SplitRegularSourceEdges(view,originalEdges[..ne],fragments,owned,ref ownedCount,ref model);if(error!=0)return error;
            Span<SheetCutCurve> faceCuts=stackalloc SheetCutCurve[RegularCutLimit];
            LastRollingNetworkStage=RollingNetworkStage.Arrange;
            for(var f=0;f<nf;f++)
            {
                LastRollingFirstFace=f;var n=0;
                for(var c=0;c<cutCount;c++)if(cuts[c].First==f||cuts[c].Second==f&&cuts[c].SecondIsBoundary==0)faceCuts[n++]=cuts[c].Cut;
                error=ArrangeFiniteInputFace(view,f,sources[f],fragments[..ne],faceCuts[..n],ref model,halfedges,cycleCoedges,cycles);if(error!=0)return error;
            }
            // Cell selection must establish a unique oriented manifold and
            // material replacement before any B-rep can be published.
            LastRollingNetworkStage=RollingNetworkStage.Select;
            error=SelectRollingReplacement(body,sourceFaces,selected,bands,subdivisions,view,fragments[..ne],ref model,descriptions,products,out made);if(error!=0)return error;
            foreach(var curve in curves){error=ReleaseRollingCurve(curve.Curve);if(error!=0)return error;}
            foreach(var curve in owned[..ownedCount]){error=ReleaseRollingCurve(curve.Curve);if(error!=0)return error;}
            for(var e=0;e<ne;e++)if(originalEdges[e]<0){error=ReleaseRollingCurve(inputEdges[e].Curve);if(error!=0)return error;}
            foreach(var band in bands)
            {
                error=ReleaseRollingCurve(band.FirstContact);if(error!=0)return error;
                error=ReleaseRollingCurve(band.SecondContact);if(error!=0)return error;
                if(IsValidTag(band.Surface)&&GeometricOwnerCount(PoolKind.Surface,GetSurfaceSlotByTag(band.Surface))==0)
                {var surface=band.Surface;error=EntityDelete(1,&surface);if(error!=0)return error;}
            }
            return 0;
        }
        finally{scratch.Return(memory);}
    }

    private static int ReleaseRollingCurve(CurveTag curve)
        =>IsValidTag(curve)&&GeometricOwnerCount(PoolKind.Curve,GetCurveSlotByTag(curve))==0?EntityDelete(1,&curve):0;
}

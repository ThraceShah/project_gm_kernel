using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int BuildRegularBooleanProducts(ReadOnlySpan<BodyTag> inputs,ref SheetArrangement model,
        ReadOnlySpan<byte> selected,ReadOnlySpan<byte> reversed,PK_BODY_boolean_o_s* options,PK_boolean_r_s* result)
    {
        Span<BufferOffset> sourceOwners=stackalloc BufferOffset[model.NF];sourceOwners.Fill(-1);
        for(var f=0;f<model.NF;f++)
        {
            var sourceBody=Shells[Faces[model.Sources[f]].BackShell].Body;
            for(var b=0;b<inputs.Length;b++)if(TagRec(inputs[b]).Slot==sourceBody){sourceOwners[f]=b;break;}
            if(sourceOwners[f]<0)return ParasolidConstants.PK_ERROR_bad_loop;
        }
        Span<BufferOffset> first=stackalloc BufferOffset[model.NE],second=stackalloc BufferOffset[model.NE];first.Fill(-1);second.Fill(-1);
        Span<byte> senses=stackalloc byte[model.NE];Span<BufferOffset> components=stackalloc BufferOffset[model.NF],queue=stackalloc BufferOffset[model.NF];components.Fill(-1);
        for(var f=0;f<model.NF;f++)if(selected[f]!=0)
        {
            var face=model.Faces[f];
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=model.Loops[face.FirstLoop+l];
                for(var k=0;k<loop.CoedgeCount;k++)
                {
                    var coedge=model.Coedges[loop.FirstCoedge+k];var sense=(byte)(coedge.Forward^reversed[f]);var e=coedge.Edge;
                    if(first[e]<0){first[e]=f;senses[e]=sense;}
                    else {if(second[e]>=0||senses[e]==sense||first[e]==f)return ParasolidConstants.PK_ERROR_non_manifold;second[e]=f;}
                }
            }
        }
        for(var e=0;e<model.NE;e++)if(first[e]>=0&&second[e]<0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var componentCount=0;
        for(var seed=0;seed<model.NF;seed++)
        {
            if(selected[seed]==0||components[seed]>=0)continue;var head=0;var tail=1;queue[0]=seed;components[seed]=componentCount;
            while(head<tail)
            {
                var current=queue[head++];var face=model.Faces[current];
                for(var l=0;l<face.LoopCount;l++)
                {
                    var loop=model.Loops[face.FirstLoop+l];
                    for(var k=0;k<loop.CoedgeCount;k++)
                    {
                        var e=model.Coedges[loop.FirstCoedge+k].Edge;var next=first[e]==current?second[e]:first[e];
                        if(next<0)return ParasolidConstants.PK_ERROR_non_manifold;
                        if(components[next]<0){components[next]=componentCount;queue[tail++]=next;}
                    }
                }
            }
            componentCount++;
        }
        if(componentCount==0)
        {
            fixed(BodyTag* tags=inputs){var error=EntityDelete(inputs.Length,tags);if(error!=0)return error;}
            *result=new(){result=ParasolidConstants.PK_boolean_result_success_c};return 0;
        }
        var view=new RegularBoundaryView(model.Edges[..model.NE],model.Coedges[..model.NC],model.Loops[..model.NL],model.Faces[..model.NF]);
        Span<byte> cavity=stackalloc byte[componentCount];Span<KernelVector3> witnesses=stackalloc KernelVector3[componentCount];
        Span<BufferOffset> members=stackalloc BufferOffset[model.NF],parents=stackalloc BufferOffset[componentCount];parents.Fill(-1);
        Span<BufferCount> depth=stackalloc BufferCount[componentCount];depth.Clear();
        Span<KernelVector3> jet=stackalloc KernelVector3[4];
        for(var c=0;c<componentCount;c++)
        {
            var n=RegularComponentMembers(components,c,members);var face=members[0];var uv=model.Witnesses[face];var surface=GetSurfaceSlotByTag(model.Faces[face].Surface);
            SurfaceDerivativeLayout.TryCreate(1,1,out var layout);
            var status=EvaluateSurfaceCore(in Surfaces[surface],uv.param[0],uv.param[1],in layout,jet,0);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            witnesses[c]=jet[0];var normal=ProfileCross(jet[layout.GetIndex(1,0)],jet[layout.GetIndex(0,1)]);
            var sign=LocalSenseSign(model.Faces[face].Sense)*LocalSenseSign(Surfaces[surface].Sense)*(reversed[face]==0?1:-1);
            normal=Scale(normal,sign);if(!ProfileNormalize(ref normal))return ParasolidConstants.PK_ERROR_at_singularity;
            var error=ParasolidConstants.PK_ERROR_cant_do_intersect;var winding=0;
            for(var attempt=0;attempt<4&&error!=0;attempt++)
            {
                var tangent=jet[layout.GetIndex(1,0)];ProfileNormalize(ref tangent);
                var other=ProfileCross(normal,tangent);var direction=Add(Scale(normal,-1),Add(Scale(tangent,.17*attempt),Scale(other,.23*attempt)));ProfileNormalize(ref direction);
                error=RegularRayWinding(view,members[..n],reversed,jet[0],direction,true,out winding,out _);
            }
            if(error!=0)return error;if(winding is not (0 or 1))return ParasolidConstants.PK_ERROR_bad_loop;cavity[c]=winding==0?(byte)1:(byte)0;
        }
        for(var c=0;c<componentCount;c++)if(cavity[c]==0)for(var other=0;other<componentCount;other++)if(other!=c&&cavity[other]==0)
        {
            var n=RegularComponentMembers(components,other,members);var error=RegularClassify(view,members[..n],reversed,witnesses[c],out var w,out _);if(error!=0)return error;
            if(w==1)depth[c]++;else if(w!=0)return ParasolidConstants.PK_ERROR_bad_loop;
        }
        for(var c=0;c<componentCount;c++)if(cavity[c]!=0)
        {
            var best=-1;var bestDepth=-1;
            for(var outer=0;outer<componentCount;outer++)if(cavity[outer]==0)
            {
                var n=RegularComponentMembers(components,outer,members);var error=RegularClassify(view,members[..n],reversed,witnesses[c],out var w,out _);if(error!=0)return error;
                if(w==1&&depth[outer]>bestDepth){best=outer;bestDepth=depth[outer];}else if(w is not (0 or 1))return ParasolidConstants.PK_ERROR_bad_loop;
            }
            if(best<0)return ParasolidConstants.PK_ERROR_bad_loop;parents[c]=best;
        }
        Span<BodyTag> products=stackalloc BodyTag[componentCount],retained=stackalloc BodyTag[componentCount];retained.Clear();
        Span<FaceSlot> faceProducts=stackalloc FaceSlot[model.NF];faceProducts.Fill(-1);
        Span<FaceSlot> builtFaces=stackalloc FaceSlot[model.NF];Span<byte> cavityFaces=stackalloc byte[model.NF];cavityFaces.Clear();var productCount=0;
        LastRegularBooleanStage=RegularBooleanStage.Assemble;
        for(var c=0;c<componentCount;c++)if(cavity[c]==0)
        {
            var n=0;for(var f=0;f<model.NF;f++)if(selected[f]!=0&&(components[f]==c||parents[components[f]]==c))
            {members[n++]=f;cavityFaces[f]=cavity[components[f]];}
            var owner=sourceOwners[members[0]];
            var reuse=(productCount==0?owner==0:options->function==ParasolidConstants.PK_boolean_unite_c)
                &&RegularBoundaryUnchanged(ref model,members[..n],reversed,sourceOwners,owner,TagRec(inputs[owner]).Slot);
            if(reuse)
            {
                products[productCount]=retained[productCount]=inputs[owner];for(var i=0;i<n;i++)builtFaces[i]=model.Sources[members[i]];
            }
            else
            {
                var error=CloneRegularBoundaryBody(ref model,members[..n],reversed,cavityFaces,out products[productCount],builtFaces[..n]);if(error!=0)return error;
                if(productCount==0)retained[productCount]=inputs[0];
                else if(options->function==ParasolidConstants.PK_boolean_unite_c)
                    for(var b=1;b<inputs.Length;b++)
                    {
                        if(retained[..productCount].Contains(inputs[b]))continue;var involved=false;
                        for(var i=0;i<n;i++)involved|=sourceOwners[members[i]]==b;
                        if(involved){retained[productCount]=inputs[b];break;}
                    }
            }
            productCount++;
            for(var i=0;i<n;i++)faceProducts[members[i]]=builtFaces[i];
        }
        LastRegularBooleanStage=RegularBooleanStage.Publish;
        Span<FaceSlot> originals=stackalloc FaceSlot[model.NF],replacements=stackalloc FaceSlot[model.NF],moved=stackalloc FaceSlot[model.NF];var movedCount=0;
        for(var p=0;p<productCount;p++)for(var b=0;b<inputs.Length;b++)
        {
            var targetBody=TagRec(products[p]).Slot;var sourceBody=TagRec(inputs[b]).Slot;var n=0;
            if(targetBody==sourceBody)continue;
            for(var f=0;f<model.NF;f++)
            {
                if(selected[f]==0||faceProducts[f]<0||moved[..movedCount].Contains(model.Sources[f]))continue;
                if(Shells[Faces[faceProducts[f]].BackShell].Body!=targetBody||Shells[Faces[model.Sources[f]].BackShell].Body!=sourceBody)continue;
                if(originals[..n].Contains(model.Sources[f]))continue;originals[n]=model.Sources[f];replacements[n++]=faceProducts[f];
            }
            if(n==0)continue;var error=PreserveModelingFaceHandles(sourceBody,targetBody,originals[..n],replacements[..n]);if(error!=0)return error;
            originals[..n].CopyTo(moved[movedCount..]);movedCount+=n;
        }
        var returned=AllocateReturnSlice(productCount);if(returned==null)return ParasolidConstants.PK_ERROR_memory_full;
        for(var p=0;p<productCount;p++)if(retained[p]!=0&&retained[p]!=products[p])
        {var replace=ReplaceProfileBody(TagRec(retained[p]).Slot,TagRec(products[p]).Slot);if(replace!=0)return replace;products[p]=retained[p];}
        Span<BodyTag> consumed=stackalloc BodyTag[inputs.Length];var consumedCount=0;
        foreach(var input in inputs)if(!products[..productCount].Contains(input))consumed[consumedCount++]=input;
        if(consumedCount>0)fixed(BodyTag* tags=consumed){var error=EntityDelete(consumedCount,tags);if(error!=0)return error;}
        products[..productCount].CopyTo(new Span<BodyTag>(returned,productCount));
        *result=new(){result=ParasolidConstants.PK_boolean_result_success_c,n_bodies=productCount,bodies=returned};return 0;
    }

    private static BufferCount RegularComponentMembers(ReadOnlySpan<BufferOffset> labels,BufferOffset component,Span<BufferOffset> members)
    {var n=0;for(var i=0;i<labels.Length;i++)if(labels[i]==component)members[n++]=i;return n;}

    private static bool RegularBoundaryUnchanged(scoped ref SheetArrangement model,scoped ReadOnlySpan<BufferOffset> indices,
        scoped ReadOnlySpan<byte> reversed,scoped ReadOnlySpan<BufferOffset> owners,BufferOffset owner,BodySlot body)
    {
        if(indices.Length!=Bodies[body].FaceCountBody)return false;
        Span<FaceSlot> seen=stackalloc FaceSlot[indices.Length];var count=0;
        foreach(var index in indices)
        {
            var source=model.Sources[index];if(owners[index]!=owner||reversed[index]!=0||seen[..count].Contains(source))return false;seen[count++]=source;
            var face=model.Faces[index];var original=Faces[source];if(face.LoopCount!=original.LoopCount)return false;
            var n=0;var loop=original.FirstLoop;for(var l=0;l<original.LoopCount;l++,loop=Loops[loop].NextInFace)n+=Loops[loop].FinCount;
            var m=0;
            for(var l=0;l<face.LoopCount;l++)
            {
                var modelLoop=model.Loops[face.FirstLoop+l];m+=modelLoop.CoedgeCount;
                for(var k=0;k<modelLoop.CoedgeCount;k++)
                {
                    var coedge=model.Coedges[modelLoop.FirstCoedge+k];var edge=model.Edges[coedge.Edge];var found=false;loop=original.FirstLoop;
                    for(var q=0;q<original.LoopCount;q++,loop=Loops[loop].NextInFace)
                    {
                        var fin=Loops[loop].FirstFin;
                        for(var t=0;t<Loops[loop].FinCount;t++,fin=Fins[fin].NextInLoop)
                        {
                            var old=Edges[Fins[fin].Edge];if(old.CurveTag!=edge.Curve||!TryProfileEdgeRange(old,out var lo,out var hi)||lo!=edge.Start||hi!=edge.End)continue;
                            var start=edge.StartVertex<0?-1:model.OriginalVertices[edge.StartVertex];var end=edge.EndVertex<0?-1:model.OriginalVertices[edge.EndVertex];
                            found|=start==old.StartVertex&&end==old.EndVertex&&(coedge.Forward!=0)==(Fins[fin].Sense=='+');
                        }
                    }
                    if(!found)return false;
                }
            }
            if(n!=m)return false;
        }
        return true;
    }

    private static int CloneRegularBoundaryBody(scoped ref SheetArrangement model,scoped ReadOnlySpan<BufferOffset> indices,scoped ReadOnlySpan<byte> reversed,
        scoped ReadOnlySpan<byte> cavityFaces,out BodyTag body,scoped Span<FaceSlot> products,scoped Span<EdgeSlot> edgeProducts=default)
    {
        body=0;if(!edgeProducts.IsEmpty&&edgeProducts.Length<model.NE)return ParasolidConstants.PK_ERROR_bad_parameter;
        var scratch=CommandScratch.Current;var memory=scratch.Take(70000);if(memory.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes=MemoryMarshal.AsBytes(memory);var cursor=0;
            var points=ModelSpan<KernelVector3>(bytes,ref cursor,model.NP);var vertexMap=ModelSpan<BufferOffset>(bytes,ref cursor,model.NP);vertexMap.Fill(-1);
            var vertexPrecisions=ModelSpan<double>(bytes,ref cursor,model.NP);
            var vertexOwners=ModelSpan<BodySlot>(bytes,ref cursor,model.NP);vertexOwners.Fill(-1);
            var edges=ModelSpan<ModelingEdge>(bytes,ref cursor,model.NE);var edgeMap=ModelSpan<BufferOffset>(bytes,ref cursor,model.NE);edgeMap.Fill(-1);
            var coedges=ModelSpan<ModelingCoedge>(bytes,ref cursor,model.NC);var loops=ModelSpan<ModelingLoop>(bytes,ref cursor,model.NL);
            var faces=ModelSpan<ModelingFace>(bytes,ref cursor,indices.Length);var cavities=ModelSpan<byte>(bytes,ref cursor,indices.Length);
            if(cursor>bytes.Length)return ParasolidConstants.PK_ERROR_memory_full;var np=0;var ne=0;var nc=0;var nl=0;
            BodySlot commonOwner=-1;
            foreach(var index in indices)
            {
                var source=model.Sources[index];if(source<0)continue;
                var owner=TopologyBody(PoolKind.Face,source);
                if(commonOwner==-1)commonOwner=owner;else if(commonOwner!=owner){commonOwner=-2;break;}
            }
            for(var i=0;i<indices.Length;i++)
            {
                var index=indices[i];var face=model.Faces[index];var reverse=reversed[index]!=0;var surface=0;
                var sourceOwner=model.Sources[index]>=0?TopologyBody(PoolKind.Face,model.Sources[index]):commonOwner;
                for(var previous=0;previous<i;previous++)if(model.Faces[indices[previous]].Surface==face.Surface){surface=faces[previous].Surface;break;}
                if(surface==0){var error=TryCloneEntityCore(face.Surface,out surface);if(error!=0)return error;}
                faces[i]=new(){Surface=surface,Sense=reverse?ToggleModelingSense(face.Sense):face.Sense,FirstLoop=nl,LoopCount=face.LoopCount};cavities[i]=cavityFaces[index];
                for(var l=0;l<face.LoopCount;l++)
                {
                    var loop=model.Loops[face.FirstLoop+l];loops[nl++]=new(){FirstCoedge=nc,CoedgeCount=loop.CoedgeCount};
                    for(var k=0;k<loop.CoedgeCount;k++)
                    {
                        var coedge=model.Coedges[loop.FirstCoedge+(reverse?loop.CoedgeCount-1-k:k)];var e=edgeMap[coedge.Edge];
                        if(e<0)
                        {
                            var source=model.Edges[coedge.Edge];
                            var start=-1;var end=-1;
                            for(var side=0;side<2;side++)
                            {
                                var v=side==0?source.StartVertex:source.EndVertex;var mapped=-1;
                                if(v>=0)
                                {
                                    mapped=vertexMap[v];
                                    if(mapped<0){mapped=np;vertexMap[v]=np;vertexOwners[np]=sourceOwner;vertexPrecisions[np]=model.VertexPrecisions.IsEmpty?0:model.VertexPrecisions[v];points[np++]=model.Points[v];}
                                    else if(vertexOwners[mapped]!=sourceOwner)vertexOwners[mapped]=-2;
                                }
                                if(side==0)start=mapped;else end=mapped;
                            }
                            e=ne;edgeMap[coedge.Edge]=ne;edges[ne++]=new(){Curve=source.Curve,Start=source.Start,End=source.End,StartVertex=start,EndVertex=end};
                        }
                        var sourceEdge=model.Edges[coedge.Edge];
                        for(var side=0;side<2;side++)
                        {
                            var v=side==0?sourceEdge.StartVertex:sourceEdge.EndVertex;if(v<0)continue;var mapped=vertexMap[v];
                            if(vertexOwners[mapped]!=sourceOwner)vertexOwners[mapped]=-2;
                        }
                        coedges[nc++]=new(){Edge=e,Forward=(byte)(coedge.Forward^(reverse?1:0))};
                    }
                }
            }
            if(!model.VertexMergeCandidates.IsEmpty)for(var v=0;v<model.NP;v++)
            {
                var mapped=vertexMap[v];var original=model.OriginalVertices[v];
                if(mapped>=0&&original>=0&&(vertexOwners[mapped]==-2||vertexOwners[mapped]!=Vertices[original].Body)&&model.VertexMergeCandidates[v]!=0)
                    vertexPrecisions[mapped]=Math.Max(vertexPrecisions[mapped],RegularPlanarImprintMinimumPrecision);
            }
            Span<EdgeSlot> builtEdges=edgeProducts.IsEmpty&&TraceFiniteBoundary==null?default:stackalloc EdgeSlot[ne];
            var built=BuildModelingBrep(points[..np],edges[..ne],coedges[..nc],loops[..nl],faces,true,out body,products,cavities,builtEdges,
                cloneBoundaryCurves:true);if(built!=0)return built;
            if(!edgeProducts.IsEmpty)
                for(var i=0;i<edgeMap.Length;i++)edgeProducts[i]=edgeMap[i]<0?-1:builtEdges[edgeMap[i]];
            if(TraceFiniteBoundary!=null)
                for(var i=0;i<indices.Length;i++)
                {
                    var index=indices[i];var face=model.Faces[index];var reverse=reversed[index]!=0;
                    for(var l=0;l<face.LoopCount;l++)
                    {
                        var loop=model.Loops[face.FirstLoop+l];
                        foreach(var coedge in model.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                        {
                            var use=FiniteBoundaryUse.Create(face.Surface,model.Edges[coedge.Edge],coedge.Edge,(byte)(coedge.Forward^(reverse?1:0)),
                                model.Sources[index],faceSense:faces[i].Sense);
                            TraceBoundary(BoundaryTraceStage.Product,use,index,products[i],Edges[builtEdges[edgeMap[coedge.Edge]]].CurveTag);
                        }
                    }
                }
            var vertex=Bodies[TagRec(body).Slot].FirstVertexBody;
            for(var i=0;i<np;i++,vertex=Vertices[vertex].NextInBody)Vertices[vertex].Tolerance=vertexPrecisions[i];
            return TransferRegularPointLoops(ref model,indices,body);
        }
        finally{scratch.Return(memory);}
    }

    private static KernelSense ToggleModelingSense(KernelSense sense)
        =>LocalSenseSign(sense)>0?ParasolidConstants.PK_TOPOL_sense_negative_c:ParasolidConstants.PK_TOPOL_sense_positive_c;
}

using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal enum RegularBooleanStage:byte{None,Read,Cuts,Clip,Split,Arrange,Classify,Components,Assemble,Publish}
    [ThreadStatic] internal static RegularBooleanStage LastRegularBooleanStage;
    [ThreadStatic] internal static BufferOffset LastRegularBooleanFace;
    [ThreadStatic] internal static BufferOffset LastRegularBooleanOtherFace;
    private const BufferCount RegularInputFaces=128,RegularInputEdges=1024,RegularInputLoops=512,RegularInputCoedges=4096,RegularCurveLimit=1024,RegularCutLimit=2048;

    private static int RegularCurvedBoolean(ReadOnlySpan<BodyTag> inputs,PK_BODY_boolean_o_s* options,PK_TOPOL_track_r_s* tracking,PK_boolean_r_s* result)
    {
        LastRegularBooleanStage=RegularBooleanStage.Read;LastRegularBooleanFace=LastRegularBooleanOtherFace=-1;
        if(options->merge_imprinted!=0||options->tracking!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        var scratch=CommandScratch.Current;var memory=scratch.Take(180000);if(memory.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
        ModelingPerformance.Context performance=default;using var measurement=new ModelingPerformance.Scope(&performance,immutableGeometry:true);ModelingPerformance.Stage(RegularBooleanStage.Read);
        var ownedCount=0;
        try
        {
            var bytes=MemoryMarshal.AsBytes(memory);var cursor=0;
            var model=new SheetArrangement
            {
                Points=ModelSpan<KernelVector3>(bytes,ref cursor,SheetPointLimit),OriginalVertices=ModelSpan<VertexSlot>(bytes,ref cursor,SheetPointLimit),VertexPrecisions=ModelSpan<double>(bytes,ref cursor,SheetPointLimit),VertexMergeCandidates=ModelSpan<byte>(bytes,ref cursor,SheetPointLimit),
                Edges=ModelSpan<ModelingEdge>(bytes,ref cursor,SheetEdgeLimit),CutEdges=ModelSpan<byte>(bytes,ref cursor,SheetEdgeLimit),
                Coedges=ModelSpan<ModelingCoedge>(bytes,ref cursor,SheetCoedgeLimit),Loops=ModelSpan<ModelingLoop>(bytes,ref cursor,SheetLoopLimit),
                Faces=ModelSpan<ModelingFace>(bytes,ref cursor,SheetFaceLimit),Sources=ModelSpan<FaceSlot>(bytes,ref cursor,SheetFaceLimit),
                Sides=ModelSpan<sbyte>(bytes,ref cursor,SheetFaceLimit),Witnesses=ModelSpan<PK_UV_s>(bytes,ref cursor,SheetFaceLimit),Tolerance=Math.Min(options->default_tol,1e-8),
            };
            var inputEdges=ModelSpan<ModelingEdge>(bytes,ref cursor,RegularInputEdges);var originalEdges=ModelSpan<EdgeSlot>(bytes,ref cursor,RegularInputEdges);
            var inputCoedges=ModelSpan<ModelingCoedge>(bytes,ref cursor,RegularInputCoedges);var inputLoops=ModelSpan<ModelingLoop>(bytes,ref cursor,RegularInputLoops);
            var inputFaces=ModelSpan<ModelingFace>(bytes,ref cursor,RegularInputFaces);var sources=ModelSpan<FaceSlot>(bytes,ref cursor,RegularInputFaces);
            var owners=ModelSpan<BufferOffset>(bytes,ref cursor,RegularInputFaces);var offsets=ModelSpan<BufferOffset>(bytes,ref cursor,inputs.Length+1);
            var sourceEdges=ModelSpan<SheetSourceEdge>(bytes,ref cursor,RegularInputEdges);
            var families=ModelSpan<RegularCutFamily>(bytes,ref cursor,RegularInputFaces*4);
            var contactPoints=ModelSpan<SurfaceIntersectionNode>(bytes,ref cursor,RegularCurveLimit);var contactPointCount=0;var finiteContact=false;
            var pointLoops=ModelSpan<RegularPointContact>(bytes,ref cursor,RegularCurveLimit);var pointLoopCount=0;
            var owned=ModelSpan<SheetCutCurve>(bytes,ref cursor,RegularCurveLimit);var cuts=ModelSpan<RegularFaceCut>(bytes,ref cursor,RegularCutLimit);
            var halfedges=ModelSpan<SheetHalfedge>(bytes,ref cursor,2*SheetEdgeLimit);var cycleCoedges=ModelSpan<ModelingCoedge>(bytes,ref cursor,2*SheetEdgeLimit);
            var cycles=ModelSpan<SheetCycle>(bytes,ref cursor,SheetLoopLimit);if(cursor>bytes.Length)return ParasolidConstants.PK_ERROR_memory_full;
            var error=ReadRegularBoundaries(inputs,model.Points,model.OriginalVertices,inputEdges,originalEdges,inputCoedges,inputLoops,inputFaces,sources,owners,offsets,
                out model.NP,out var ne,out var nc,out var nl,out var nf,model.Tolerance,model.VertexPrecisions);if(error!=0)return error;
            model.VertexMergeCandidates[..model.NP].Clear();
            var input=new RegularBoundaryView(inputEdges[..ne],inputCoedges[..nc],inputLoops[..nl],inputFaces[..nf]);
            Span<SheetCutCurve> supportCuts=stackalloc SheetCutCurve[GeneralIntersectionBranchLimit];var familyCount=0;var cutCount=0;
            Span<SurfaceIntersectionNode> supportPoints=stackalloc SurfaceIntersectionNode[64];
            for(var a=0;a<nf;a++)for(var b=a+1;b<nf;b++)
            {
                if(owners[a]==owners[b])continue;ModelingPerformance.Pair();LastRegularBooleanStage=RegularBooleanStage.Cuts;ModelingPerformance.Stage(RegularBooleanStage.Cuts);LastRegularBooleanFace=a;LastRegularBooleanOtherFace=b;
                if(RegularPlaneFacesSeparated(input,a,b,model.Tolerance))continue;
                if(RegularNaturalNurbsSeparatedFromPlane(input,a,b,model.Tolerance)||RegularNaturalNurbsSeparatedFromPlane(input,b,a,model.Tolerance))continue;
                var first=inputFaces[a].Surface;var second=inputFaces[b].Surface;var family=-1;
                for(var i=0;i<familyCount;i++)if(families[i].First==first&&families[i].Second==second){family=i;break;}
                if(family<0)
                {
                    var relation=RegularCoincidentSupports(first,second)?SurfacePairRelation.CoincidentArea:SurfacePairRelation.Unresolved;var n=0;var npContact=0;
                    if(relation!=SurfacePairRelation.CoincidentArea)
                    {
                        error=TryPrepareRegularContactSupportCuts(first,second,supportCuts,out n,supportPoints,out npContact,out relation,out var handled);if(error!=0)return error;
                        if(!handled){error=PrepareRegularSupportCuts(first,second,supportCuts,out n);if(error!=0)return error;relation=n==0?SurfacePairRelation.Empty:SurfacePairRelation.RegularCurves;}
                    }
                    if(familyCount==families.Length||ownedCount>owned.Length-n||contactPointCount>contactPoints.Length-npContact)return ParasolidConstants.PK_ERROR_not_implemented;
                    family=familyCount;families[familyCount++]=new(){First=first,Second=second,Start=ownedCount,Count=n,PointStart=contactPointCount,PointCount=npContact,Relation=relation};
                    supportPoints[..npContact].CopyTo(contactPoints[contactPointCount..]);contactPointCount+=npContact;
                    supportCuts[..n].CopyTo(owned[ownedCount..]);ownedCount+=n;
                }
                var data=families[family];
                if(data.Relation==SurfacePairRelation.PointContact)
                {
                    error=ClipRegularContactPoints(input,a,b,contactPoints.Slice(data.PointStart,data.PointCount),supportPoints,out var accepted);if(error!=0)return error;
                    finiteContact|=accepted!=0;
                    if(owners[a]==0||owners[b]==0)for(var p=0;p<accepted;p++)
                    {
                        if(pointLoopCount==pointLoops.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                        pointLoops[pointLoopCount++]=new(){Face=sources[owners[a]==0?a:b],Point=supportPoints[p].Point};
                    }
                    continue;
                }
                if(data.Relation==SurfacePairRelation.CoincidentArea)
                {
                    error=ArrangeRegularCoincidentDomains(input,a,b,cuts,ref cutCount,ref model);if(error!=0)return error;
                    continue;
                }
                if(data.Count==0)continue;
                LastRegularBooleanStage=RegularBooleanStage.Clip;ModelingPerformance.Stage(RegularBooleanStage.Clip);
                error=ClipRegularFacePair(input,a,b,owned.Slice(data.Start,data.Count),cuts,ref cutCount,owned,ref ownedCount,ref model);if(error!=0)return error;
            }
            LastRegularBooleanOtherFace=-1;
            // With several tools, intersections of three different bodies
            // also split the curves on the target. The final classification
            // applies BooleanMaterial to the union of all tools.
            if(inputs.Length>2)
            {
                Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
                for(var c=0;c<cutCount;c++)for(var f=0;f<nf;f++)
                {
                    var cut=cuts[c];if(owners[f]==owners[cut.First]||owners[f]==owners[cut.Second])continue;
                    var status=IntersectRegularSurfaceCurve(inputFaces[f].Surface,cut.Cut.Curve,cut.Cut.First,cut.Cut.Last,roots,out var n);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    for(var r=0;r<n;r++)
                    {
                        if(roots[r].Kind!=IntersectionVectorKind.Simple)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                        status=IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(inputFaces[f].Surface)],roots[r].Point,out var uv);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                        error=RegularFaceContains(input,f,uv,out var inside);if(error!=0)return error;if(!inside)continue;
                        error=model.AddPoint(roots[r].Point,-1,out _);if(error!=0)return error;
                    }
                }
            }
            LastRegularBooleanStage=RegularBooleanStage.Split;ModelingPerformance.Stage(RegularBooleanStage.Split);
            var coincident=false;for(var i=0;i<familyCount;i++)coincident|=families[i].Relation==SurfacePairRelation.CoincidentArea;
            error=SplitRegularSourceEdges(input,originalEdges[..ne],sourceEdges,owned,ref ownedCount,ref model,coincident);if(error!=0)return error;
            Span<SheetCutCurve> faceCuts=stackalloc SheetCutCurve[RegularCutLimit];
            for(var f=0;f<nf;f++)
            {
                LastRegularBooleanStage=RegularBooleanStage.Arrange;ModelingPerformance.Stage(RegularBooleanStage.Arrange);LastRegularBooleanFace=f;var n=0;
                for(var c=0;c<cutCount;c++)if(cuts[c].First==f||cuts[c].Second==f)faceCuts[n++]=cuts[c].Cut;
                error=ArrangeSheetFace(sources[f],0,sourceEdges[..ne],faceCuts[..n],ref model,halfedges,cycleCoedges,cycles,true,coincident);if(error!=0)return error;
            }
            LastRegularBooleanStage=RegularBooleanStage.Classify;ModelingPerformance.Stage(RegularBooleanStage.Classify);
            if(coincident){error=UnifyRegularCoincidentEdges(ref model);if(error!=0)return error;}
            Span<byte> selected=stackalloc byte[model.NF],reversed=stackalloc byte[model.NF];selected.Clear();reversed.Clear();
            Span<BufferOffset> bodyFaces=stackalloc BufferOffset[RegularInputFaces];Span<bool> minus=stackalloc bool[inputs.Length],plus=stackalloc bool[inputs.Length];
            SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> point=stackalloc KernelVector3[4];var hasClash=cutCount>0;var targetClash=false;
            for(var c=0;c<cutCount;c++)targetClash|=owners[cuts[c].First]==0||owners[cuts[c].Second]==0;
            for(var f=0;f<model.NF;f++)
            {
                LastRegularBooleanFace=f;var sourceIndex=sources[..nf].IndexOf(model.Sources[f]);if(sourceIndex<0)return ParasolidConstants.PK_ERROR_bad_loop;
                var owner=owners[sourceIndex];var uv=model.Witnesses[f];var surface=GetSurfaceSlotByTag(model.Faces[f].Surface);
                var status=EvaluateSurfaceCore(in Surfaces[surface],uv.param[0],uv.param[1],in layout,point,0);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                var outward=Geometry.Evaluation.EvaluationMath.Scale(ProfileCross(point[layout.GetIndex(1,0)],point[layout.GetIndex(0,1)]),LocalSenseSign(model.Faces[f].Sense)*LocalSenseSign(Surfaces[surface].Sense));
                var canonicalOwner=owner;
                for(var b=0;b<inputs.Length;b++)
                {
                    if(b==owner){minus[b]=true;plus[b]=false;continue;}
                    if(coincident)
                    {
                        error=RegularCoincidentMaterial(input,model.Faces[f].Surface,point[0],outward,offsets[b],offsets[b+1],out var on,out minus[b],out plus[b]);if(error!=0)return error;
                        if(on){hasClash=true;if(owner==0||b==0)targetClash=true;canonicalOwner=Math.Min(canonicalOwner,b);continue;}
                    }
                    var n=offsets[b+1]-offsets[b];for(var i=0;i<n;i++)bodyFaces[i]=offsets[b]+i;
                    error=RegularClassify(input,bodyFaces[..n],default,point[0],out var winding,out _);if(error!=0)return error;
                    if(winding is <0 or >1)return ParasolidConstants.PK_ERROR_bad_loop;
                    minus[b]=plus[b]=winding==1;if(winding==1){hasClash=true;if(owner==0||b==0)targetClash=true;}
                }
                var inside=BooleanMaterial(minus,options->function);var outside=BooleanMaterial(plus,options->function);
                if(inside!=outside&&canonicalOwner==owner){selected[f]=1;reversed[f]=inside?(byte)0:(byte)1;}
            }
            // An isolated exterior touch has no material volume to remove
            // or intersect. Tangent cavity/union topology is not represented
            // by a zero-dimensional event alone, so those cases fail closed.
            if(finiteContact&&(hasClash||options->function==ParasolidConstants.PK_boolean_unite_c))return ParasolidConstants.PK_ERROR_not_implemented;
            if(!hasClash)
            {
                if(options->function==ParasolidConstants.PK_boolean_subtract_c)for(var p=0;p<pointLoopCount;p++)
                {error=AppendRegularPointLoop(pointLoops[p].Face,pointLoops[p].Point);if(error!=0)return error;}
                var count=options->function==ParasolidConstants.PK_boolean_unite_c?inputs.Length:options->function==ParasolidConstants.PK_boolean_subtract_c?1:0;
                var returned=count==0?null:AllocateReturnSlice(count);if(count>0&&returned==null)return ParasolidConstants.PK_ERROR_memory_full;
                inputs[..count].CopyTo(new Span<BodyTag>(returned,count));
                if(count<inputs.Length){fixed(BodyTag* delete=inputs[count..]){error=EntityDelete(inputs.Length-count,delete);if(error!=0)return error;}}
                *result=new(){result=ParasolidConstants.PK_boolean_result_no_clash_c,n_bodies=count,bodies=returned};
            }
            else
            {
                LastRegularBooleanStage=RegularBooleanStage.Components;ModelingPerformance.Stage(RegularBooleanStage.Components);
                error=MergeRegularBooleanCells(ref model,selected,reversed,coincident);if(error!=0)return error;
                error=BuildRegularBooleanProducts(inputs,ref model,selected[..model.NF],reversed[..model.NF],options,result);if(error!=0)return error;
                if(!targetClash)result->result=ParasolidConstants.PK_boolean_result_no_clash_c;
            }
            for(var i=0;i<ownedCount;i++){var curve=owned[i].Curve;error=EntityDelete(1,&curve);if(error!=0)return error;}
            return 0;
        }
        finally{scratch.Return(memory);}
    }
}

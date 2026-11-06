using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal enum RegularBlendDomainStage:byte{Face,Extent,Band,Material,ContactSeparation,ContactPlacement,Complete}
    [ThreadStatic] internal static RegularBlendDomainStage LastRegularBlendDomainStage;
    [ThreadStatic] internal static FaceTag LastRegularBlendDomainFace;
    [ThreadStatic] internal static BufferCount LastRegularBlendDomainCells;
    [ThreadStatic] internal static AlgorithmStatus LastRegularBlendDomainStatus;
    [ThreadStatic] internal static BufferOffset LastRegularContactPiece,LastRegularContactOtherPiece;
    [ThreadStatic] internal static BufferCount LastRegularContactPieces,LastRegularContactOtherPieces;
    // Certify whole-band clearance, including both contacts. Finite support
    // placement is checked after proving separation from all other faces;
    // interacting fillets and self-intersections still require local repair.
    private static int ValidateRegularBlendDomain(BodySlot body,ReadOnlySpan<FaceSlot> supports,
        ReadOnlySpan<FaceSlot> caps,SurfTag blend,double first,double last,bool removesMaterial)
    {
        const double tolerance=1e-9;
        if(supports.Length!=2||caps.Length!=2||!double.IsFinite(first)||!double.IsFinite(last)||!(last>first))
            return ParasolidConstants.PK_ERROR_not_implemented;
        foreach(var support in supports)if(RegularBlendAreaLoopCount(support)!=1)return ParasolidConstants.PK_ERROR_not_implemented;
        var slot=GetSurfaceSlotByTag(blend);
        if(slot<0||Surfaces[slot].Class!=SurfaceClass.BlendSurface)return ParasolidConstants.PK_ERROR_not_implemented;
        var record=Surfaces[slot];
        if(first<record.UMin||last>record.UMax)return ParasolidConstants.PK_ERROR_not_implemented;
        var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);
        // Construction has finished; all support/program data is immutable
        // until this proof returns. The scope ends before B-rep publication.
        ModelingPerformance.Context profile=default;using var evidence=new ModelingPerformance.Scope(&profile,immutableGeometry:true);
        var bandPatch=new Geometry.Intersection.SurfaceIntersectionPatch(program,data.ProgramRoot,new(first,last,0,1));
        bandPatch=new(bandPatch,record.Header.Tag,record.Header.Generation,record.Sense);
        var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];
        var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
        if(charts.Length<2||first<charts[0].Parameter||last>charts[^1].Parameter)
            return ParasolidConstants.PK_ERROR_not_implemented;
        Span<SurfaceParameterBox> cells=stackalloc SurfaceParameterBox[128];
        Span<BufferCount> depths=stackalloc BufferCount[128];Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        Span<IntervalNumber> splinePoles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        var visited=0;var capCount=0;var finiteObstacles=false;var face=Bodies[body].FirstFaceBody;
        for(var f=0;f<Bodies[body].FaceCountBody;f++,face=Faces[face].NextInBody)
        {
            LastRegularBlendDomainStage=RegularBlendDomainStage.Face;LastRegularBlendDomainFace=Faces[face].Header.Tag;
            if(supports.Contains(face))continue;
            var surface=GetSurfaceSlotByTag(Faces[face].SurfTag);
            if(surface<0)return ParasolidConstants.PK_ERROR_missing_geom;
            var planar=RegularPlanarSupport(surface,out var plane,out var deviation);var halfspace=planar;
            var normal=Scale(plane.Axis,LocalSenseSign(Faces[face].Orientation)*LocalSenseSign(Surfaces[surface].Sense));
            var vertex=Bodies[body].FirstVertexBody;
            for(var v=0;v<Bodies[body].VertexCountBody;v++,vertex=Vertices[vertex].NextInBody)
            {
                var pointSlot=GetPointSlotByTag(Vertices[vertex].PointTag);
                if(pointSlot<0)return ParasolidConstants.PK_ERROR_missing_geom;
                var point=Points[pointSlot].Position;
                var distance=(new IntervalNumber(point.X)-plane.Origin.X)*normal.X
                    +(new IntervalNumber(point.Y)-plane.Origin.Y)*normal.Y+(new IntervalNumber(point.Z)-plane.Origin.Z)*normal.Z
                    +new IntervalNumber(-deviation,deviation);
                if(!distance.Finite||distance.Upper>tolerance)halfspace=false;
            }
            if(caps.Contains(face)){capCount++;continue;}
            IntervalBox3 obstacle=default;
            if(!halfspace)
            {
                // A previously cut or blended body need not lie in every
                // face's infinite halfspace. Certify separation from that
                // finite face instead, then check the material component.
                if(!removesMaterial)return ParasolidConstants.PK_ERROR_not_implemented;
                LastRegularBlendDomainStage=RegularBlendDomainStage.Extent;
                var status=RegularFiniteFaceExtent(face,out obstacle);LastRegularBlendDomainStatus=status;if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                finiteObstacles=true;
            }
            var covered=first;
            for(var chart=0;chart<charts.Length-1;chart++)
            {
                var lo=Math.Max(first,charts[chart].Parameter);var hi=Math.Min(last,charts[chart+1].Parameter);
                if(!(hi>lo))continue;
                if(lo>covered)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                covered=hi;cells[0]=new(lo,hi,0,1);depths[0]=0;var pending=1;
                while(pending>0)
                {
                    LastRegularBlendDomainStage=RegularBlendDomainStage.Band;LastRegularBlendDomainCells=visited+1;
                    if(++visited>32768)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    var index=--pending;var cell=cells[index];var depth=depths[index];
                    var status=bandPatch.Bound(cell,bounds);LastRegularBlendDomainStatus=status;
                    if(status==AlgorithmStatus.Success)
                    {
                        if(halfspace)
                        {
                            var distance=(bounds[0]-plane.Origin.X)*normal.X+(bounds[1]-plane.Origin.Y)*normal.Y
                                +(bounds[2]-plane.Origin.Z)*normal.Z+new IntervalNumber(-deviation,deviation);
                            if(!distance.Finite)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                            if(distance.Upper< -tolerance)continue;
                            if(distance.Lower>tolerance)return ParasolidConstants.PK_ERROR_radius_too_large;
                        }
                        else if(bounds[0].Upper<obstacle.XLo-tolerance||bounds[0].Lower>obstacle.XHi+tolerance
                            ||bounds[1].Upper<obstacle.YLo-tolerance||bounds[1].Lower>obstacle.YHi+tolerance
                            ||bounds[2].Upper<obstacle.ZLo-tolerance||bounds[2].Lower>obstacle.ZHi+tolerance)continue;
                        else if(Surfaces[surface].Class==SurfaceClass.BSurface)
                        {
                            // A global support AABB can overlap a nearby
                            // fillet although the whole curved support stays
                            // clear. Prove separation of both full patches;
                            // do not infer overlap from that coarse box.
                            var separated=true;var extent=new IntervalBox3(obstacle.XLo,obstacle.XHi,obstacle.YLo,obstacle.YHi,obstacle.ZLo,obstacle.ZHi);
                            for(var piece=0;piece<RegularSurfacePatchCount(surface);piece++)
                            {
                                var found=RegularSurfacePatch(surface,piece,extent,splinePoles,out var patch);if(found!=AlgorithmStatus.Success)return IntersectionError(found);
                                if(patch.Domain.U1<=patch.Domain.U0||patch.Domain.V1<=patch.Domain.V0)continue;
                                found=ParametricPatchSeparation.CertifyDisjoint(bandPatch,patch,cell,patch.Domain,tolerance,ref visited,32768);
                                LastRegularBlendDomainCells=visited;LastRegularBlendDomainStatus=found;
                                if(found!=AlgorithmStatus.Success){separated=false;break;}
                            }
                            if(separated)continue;
                            // Both-patch refinement has already consumed the
                            // shared proof budget; an unresolved pair fails.
                            return ParasolidConstants.PK_ERROR_cant_do_intersect;
                        }
                    }
                    else if(status!=AlgorithmStatus.NotConverged)return IntersectionError(status);
                    if(depth>=32||pending+2>cells.Length)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    // A failed implicit tube needs a smaller spine interval;
                    // otherwise split the longer normalized surface side.
                    var alongU=status==AlgorithmStatus.NotConverged||(cell.U1-cell.U0)/(hi-lo)>=cell.V1-cell.V0;
                    var lower=alongU?cell.U0:cell.V0;var upper=alongU?cell.U1:cell.V1;
                    var middle=lower+(upper-lower)*.5;
                    if(middle==lower||middle==upper)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    cells[pending]=alongU?new(cell.U0,middle,cell.V0,cell.V1):new(cell.U0,cell.U1,cell.V0,middle);
                    depths[pending++]=depth+1;
                    cells[pending]=alongU?new(middle,cell.U1,cell.V0,cell.V1):new(cell.U0,cell.U1,middle,cell.V1);
                    depths[pending++]=depth+1;
                }
            }
            if(covered<last)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        }
        if(capCount!=2)return ParasolidConstants.PK_ERROR_not_implemented;
        if(finiteObstacles)
        {
            LastRegularBlendDomainStage=RegularBlendDomainStage.Material;
            SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> point=stackalloc KernelVector3[1];
            var status=JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,first+(last-first)*.5,.5,layout,point);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var vector=new PK_VECTOR_s();WriteVector(&vector,point[0]);int enclosure;EntityTag topology;
            var error=BodyContainsVectorCore(Bodies[body].Header.Tag,vector,&enclosure,&topology);if(error!=0)return error;
            if(enclosure!=ParasolidConstants.PK_enclosure_inside_c)return ParasolidConstants.PK_ERROR_radius_too_large;
        }
        LastRegularBlendDomainStage=RegularBlendDomainStage.Complete;return 0;
    }

    private static BufferCount RegularBlendAreaLoopCount(FaceSlot face)
    {
        var count=0;var loop=Faces[face].FirstLoop;
        for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)if(!RegularVertexLoop(loop,out _))count++;
        return count;
    }

    // Local nearest-foot uniqueness does not imply global reach. Explicitly
    // separate each complete contact from the other support before using a
    // finite-face witness: otherwise a contact could cross their old sharp
    // intersection while the solver followed another local nearest foot.
    private static int ValidateRegularContactPlacement(EdgeSlot selected,ReadOnlySpan<FaceSlot> supports,
        ReadOnlySpan<CurveTag> contacts,ReadOnlySpan<double> first,ReadOnlySpan<double> last)
    {
        Span<IntervalNumber> bandPoles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],supportPoles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<IntervalNumber> contactBounds=stackalloc IntervalNumber[9];Span<SurfaceBoundCacheEntry> certified=stackalloc SurfaceBoundCacheEntry[1];
        BufferCount visited=0;ModelingPerformance.Context profile=default;using var evidence=new ModelingPerformance.Scope(&profile,immutableGeometry:true);
        for(var side=0;side<2;side++)
        {
            LastRegularBlendDomainStage=RegularBlendDomainStage.ContactSeparation;LastRegularBlendDomainCells=0;
            var contact=ICurveDataPool[Curves[GetCurveSlotByTag(contacts[side])].DataIndex];
            var status=BlendContactInterval(contact,Math.Min(first[side],last[side]),Math.Max(first[side],last[side]),out var lo,out var hi);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var band=GetSurfaceSlotByTag(contact.Surface1Tag);var other=GetSurfaceSlotByTag(Faces[supports[1-side]].SurfTag);
            LastRegularBlendDomainFace=Faces[supports[1-side]].Header.Tag;
            status=RegularSurfaceExtent(band,out var extent);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            LastRegularContactPieces=RegularSurfacePatchCount(band);LastRegularContactOtherPieces=RegularSurfacePatchCount(other);
            for(var p=0;p<RegularSurfacePatchCount(band);p++)
            {
                status=RegularSurfacePatch(band,p,extent,bandPoles,out var patch);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                var a=Math.Max(lo,patch.Domain.U0);var b=Math.Min(hi,patch.Domain.U1);if(!(b>a))continue;
                var iso=new SurfaceIntersectionPatch(patch,false,BlendContactV(contact),a,b);
                status=BoundRegularContactChart(iso,contactBounds,ref visited);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                var parent=new SurfaceBoundCacheEntry{Box=iso.Domain,Status=AlgorithmStatus.Success};contactBounds.CopyTo(parent.Values);
                var bounded=new SurfaceIntersectionPatch(iso,certified,pinned:true);
                for(var q=0;q<RegularSurfacePatchCount(other);q++)
                {
                    LastRegularContactPiece=p;LastRegularContactOtherPiece=q;
                    certified[0]=parent;
                    status=RegularSurfacePatch(other,q,extent,supportPoles,out var support);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    if(!(support.Domain.U1>support.Domain.U0&&support.Domain.V1>support.Domain.V0))continue;
                    status=ParametricPatchSeparation.CertifyDisjoint(bounded,support,iso.Domain,support.Domain,1e-9,ref visited,32768);
                    LastRegularBlendDomainStatus=status;LastRegularBlendDomainCells=visited;
                    if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                }
            }
            var face=supports[side];var loop=Faces[face].FirstLoop;
            for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)
            {
                if(RegularVertexLoop(loop,out _))continue;var fin=Loops[loop].FirstFin;
                for(var k=0;k<Loops[loop].FinCount;k++,fin=Fins[fin].NextInLoop)
                {
                    var edge=Fins[fin].Edge;if(edge==selected)continue;
                    if(!LocalEdgeFaces(edge,out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
                    if(a==supports[1-side]||b==supports[1-side])return ParasolidConstants.PK_ERROR_not_implemented;
                }
            }
            LastRegularBlendDomainStage=RegularBlendDomainStage.ContactPlacement;
            var error=SheetCurveUV(Faces[face].SurfTag,contacts[side],first[side]+(last[side]-first[side])*.5,true,out var uv,out _,out _);if(error!=0)return error;
            error=SheetSourceContains(face,uv,out var inside);if(error!=0)return error;
            if(!inside)return ParasolidConstants.PK_ERROR_radius_too_large;
        }
        LastRegularBlendDomainStage=RegularBlendDomainStage.Complete;return 0;
    }

    // A nested chart may need several local implicit tubes before it has a
    // useful bound. Certify their complete hull once, then reuse that parent
    // bound for every other support chart; never repeat the same failed tube
    // refinement for spatially unrelated candidate pairs.
    private static AlgorithmStatus BoundRegularContactChart(in SurfaceIntersectionPatch contact,Span<IntervalNumber> output,ref BufferCount visited)
    {
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[64];Span<BufferCount> depths=stackalloc BufferCount[64];
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];pending[0]=contact.Domain;depths[0]=0;var count=1;var first=true;
        while(count>0)
        {
            if(++visited>32768)return AlgorithmStatus.NotConverged;
            var at=--count;var box=pending[at];var depth=depths[at];var status=contact.Bound(box,bounds);
            if(status==AlgorithmStatus.Success)
            {for(var d=0;d<9;d++)output[d]=first?bounds[d]:IntervalNumber.Hull(output[d],bounds[d]);first=false;continue;}
            if(status!=AlgorithmStatus.NotConverged)return status;
            if(depth>=32||count+2>pending.Length)return AlgorithmStatus.NotConverged;
            var middle=box.U0+(box.U1-box.U0)*.5;if(!(middle>box.U0&&middle<box.U1))return AlgorithmStatus.NotConverged;
            pending[count]=new(box.U0,middle,box.V0,box.V1);depths[count++]=depth+1;
            pending[count]=new(middle,box.U1,box.V0,box.V1);depths[count++]=depth+1;
        }
        return first?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
    }
}

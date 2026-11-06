using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Certify whole-band clearance for four-sided supports, including both
    // contacts. Existing finite obstacles may be separated by interval boxes;
    // interacting fillets and self-intersections still require local repair.
    private static int ValidateRegularBlendDomain(BodySlot body,ReadOnlySpan<FaceSlot> supports,
        ReadOnlySpan<FaceSlot> caps,SurfTag blend,double first,double last,bool removesMaterial)
    {
        const double tolerance=1e-9;
        if(supports.Length!=2||caps.Length!=2||!double.IsFinite(first)||!double.IsFinite(last)||!(last>first))
            return ParasolidConstants.PK_ERROR_not_implemented;
        foreach(var support in supports)
            if(Faces[support].LoopCount!=1||Loops[Faces[support].FirstLoop].FinCount!=4)
                return ParasolidConstants.PK_ERROR_not_implemented;
        var slot=GetSurfaceSlotByTag(blend);
        if(slot<0||Surfaces[slot].Class!=SurfaceClass.BlendSurface)return ParasolidConstants.PK_ERROR_not_implemented;
        var record=Surfaces[slot];
        if(first<record.UMin||last>record.UMax)return ParasolidConstants.PK_ERROR_not_implemented;
        var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);
        var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];
        var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
        if(charts.Length<2||first<charts[0].Parameter||last>charts[^1].Parameter)
            return ParasolidConstants.PK_ERROR_not_implemented;
        Span<SurfaceParameterBox> cells=stackalloc SurfaceParameterBox[128];
        Span<BufferCount> depths=stackalloc BufferCount[128];Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        var visited=0;var capCount=0;var finiteObstacles=false;var face=Bodies[body].FirstFaceBody;
        for(var f=0;f<Bodies[body].FaceCountBody;f++,face=Faces[face].NextInBody)
        {
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
                var status=RegularFiniteFaceExtent(face,out obstacle);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
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
                    if(++visited>32768)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    var index=--pending;var cell=cells[index];var depth=depths[index];
                    var status=JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,cell,bounds);
                    if(status==AlgorithmStatus.Success)
                    {
                        if(halfspace)
                        {
                            var distance=(bounds[0]-plane.Origin.X)*normal.X+(bounds[1]-plane.Origin.Y)*normal.Y
                                +(bounds[2]-plane.Origin.Z)*normal.Z+new IntervalNumber(-deviation,deviation);
                            if(!distance.Finite)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                            if(distance.Upper<=tolerance)continue;
                            if(distance.Lower>tolerance)return ParasolidConstants.PK_ERROR_radius_too_large;
                        }
                        else if(bounds[0].Upper<obstacle.XLo-tolerance||bounds[0].Lower>obstacle.XHi+tolerance
                            ||bounds[1].Upper<obstacle.YLo-tolerance||bounds[1].Lower>obstacle.YHi+tolerance
                            ||bounds[2].Upper<obstacle.ZLo-tolerance||bounds[2].Lower>obstacle.ZHi+tolerance)continue;
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
            SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> point=stackalloc KernelVector3[1];
            var status=JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,first+(last-first)*.5,.5,layout,point);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var vector=new PK_VECTOR_s();WriteVector(&vector,point[0]);int enclosure;EntityTag topology;
            var error=BodyContainsVectorCore(Bodies[body].Header.Tag,vector,&enclosure,&topology);if(error!=0)return error;
            if(enclosure!=ParasolidConstants.PK_enclosure_inside_c)return ParasolidConstants.PK_ERROR_radius_too_large;
        }
        return 0;
    }
}

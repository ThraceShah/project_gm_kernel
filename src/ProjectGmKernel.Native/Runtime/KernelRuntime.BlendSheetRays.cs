using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus IntersectParametricBoundaryRay(SurfTag surface,PK_UV_s query,double du,double dv,in ModelingEdge edge,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var surfaceSlot=GetSurfaceSlotByTag(surface);var curveSlot=GetCurveSlotByTag(edge.Curve);var record=Curves[curveSlot];
        if(record.Class==CurveClass.TRCurve)
        {
            var interval=new PK_INTERVAL_s();interval.value[0]=edge.Start;interval.value[1]=edge.End;
            if(ResolveIntersectionCurve(edge.Curve,interval,out var native)!=0)return AlgorithmStatus.Unsupported;
            var mapped=edge;mapped.Curve=Curves[native.Slot].Header.Tag;mapped.Start=native.Lower;mapped.End=native.Upper;
            if(LocalSenseSign(record.Sense)*native.Scale*LocalSenseSign(Curves[native.Slot].Sense)<0)(mapped.StartVertex,mapped.EndVertex)=(mapped.EndVertex,mapped.StartVertex);
            var found=IntersectParametricBoundaryRay(surface,query,du,dv,mapped,output,out count);if(found!=AlgorithmStatus.Success)return found;
            for(var i=0;i<count;i++)output[i].Second=(output[i].Second-native.Offset)/native.Scale;
            return AlgorithmStatus.Success;
        }
        if(record.Class==CurveClass.ICurve&&ICurveDataPool[record.DataIndex].IsBlendContact!=0)
            return IntersectBlendContactRay(surface,query,du,dv,edge,output,out count);
        if(TryAnalyticBlendContactMap(surface,record,edge.Start,edge.End,out var section,out var shift,out var scale))
            return IntersectAnalyticBlendContactRay(surface,query,du,dv,edge,section,shift,scale,output,out count);
        if(Surfaces[surfaceSlot].Class==SurfaceClass.BlendSurface&&record.Class is CurveClass.Circle or CurveClass.Ellipse)
        {
            var rational=IntersectRationalContactRay(surface,record,query,du,dv,edge,output,out count,out var handled);
            if(handled||rational!=AlgorithmStatus.Success)return rational;
        }
        if((du!=0||dv!=0)&&record.Class==CurveClass.ICurve&&ICurveDataPool[record.DataIndex].ProgramBlock>0
            &&Surfaces[surfaceSlot].Class==SurfaceClass.BlendSurface)
        {
            var nativeStatus=IntersectNativeBlendParameterRay(surface,query,du,dv,edge,output,out count,out var handled);
            if(handled)return nativeStatus;
        }
        if(record.Class==CurveClass.Circle&&Surfaces[surfaceSlot].Class==SurfaceClass.BlendSurface
            &&TryBlendCircleStation(surface,record,edge.Start,edge.End,out var station))
            return IntersectBlendCircleRay(surface,query,du,dv,edge,station,output,out count);
        if(TryBlendIsoparam(curveSlot,out var isoSurface,out var isoV)&&SameSheetSupport(surface,Surfaces[isoSurface].Header.Tag))
        {
            if(dv==0)return Math.Abs(query.param[1]-isoV)<1e-12?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
            var along=(isoV-query.param[1])/dv;var parameter=query.param[0]+du*along;
            if(along< -1e-10||!OperationIntersection.InInterval(parameter,edge.Start,edge.End))return AlgorithmStatus.Success;
            Span<KernelVector3> point=stackalloc KernelVector3[1];var evaluated=EvaluateCurveCore(in record,parameter,0,false,point,out _);if(evaluated!=AlgorithmStatus.Success)return evaluated;
            return OperationIntersection.AddPoint(output,ref count,point[0],along,parameter,IntersectionVectorKind.Simple)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
        }
        var status=RegularSurfaceExtent(surfaceSlot,out var extent);if(status!=AlgorithmStatus.Success)return status;
        SurfaceSlot other=-1;IntersectionCurveSpan span=default;var pieces=0;
        if(record.Class==CurveClass.ICurve)
        {
            var data=ICurveDataPool[record.DataIndex];
            var tag=SameSheetSupport(surface,data.Surface0Tag)?data.Surface1Tag:SameSheetSupport(surface,data.Surface1Tag)?data.Surface0Tag:0;
            other=GetSurfaceSlotByTag(tag);if(other<0)return AlgorithmStatus.Unsupported;pieces=RegularSurfacePatchCount(other);
        }
        else
        {
            var range=new PK_INTERVAL_s();range.value[0]=edge.Start;range.value[1]=edge.End;
            if(ResolveIntersectionCurve(edge.Curve,range,out span)!=0)return AlgorithmStatus.Unsupported;
            pieces=GeneralCurvePieceCount(span);
        }
        if(pieces<=0)return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[64];
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        for(var i=0;i<RegularSurfacePatchCount(surfaceSlot);i++)
        {
            status=RegularSurfacePatch(surfaceSlot,i,extent,ap,out var source);if(status!=AlgorithmStatus.Success)return status;
            if(source.Domain.U1<=source.Domain.U0||source.Domain.V1<=source.Domain.V0)continue;
            var lo=0.0;var hi=double.PositiveInfinity;
            if(!ClipParameterRay(query.param[0],du,source.Domain.U0,source.Domain.U1,ref lo,ref hi)
                ||!ClipParameterRay(query.param[1],dv,source.Domain.V0,source.Domain.V1,ref lo,ref hi)||!(hi>lo)||!double.IsFinite(hi))continue;
            var ray=new SurfaceIntersectionPatch(source,query.param[0],query.param[1],du,dv,lo,hi);
            for(var j=0;j<pieces;j++)
            {
                var nr=0;
                if(other>=0)
                {
                    status=RegularSurfacePatch(other,j,extent,bp,out var support);if(status!=AlgorithmStatus.Success)return status;
                    var box=support.Domain;if(box.U1<=box.U0||box.V1<=box.V0)continue;
                    var cell=new ParameterCell{Lower=new(){U=box.U0,V=box.V0,S=ray.Domain.U0},Upper=new(){U=box.U1,V=box.V1,S=ray.Domain.U1}};
                    status=ParametricSurfaceIntersection.Isolate(support,ray,cell,3,nodes,out var n);if(status!=AlgorithmStatus.Success)return status;
                    for(var k=0;k<n;k++)
                    {
                        var vector=new PK_VECTOR_s();WriteVector(&vector,nodes[k].Point);double parameter=0;
                        var error=CurveParameteriseVectorCore(edge.Curve,vector,&parameter);
                        if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return AlgorithmStatus.NotConverged;
                        if(OperationIntersection.InInterval(parameter,edge.Start,edge.End)
                            &&!OperationIntersection.AddPoint(roots,ref nr,nodes[k].Point,nodes[k].S,parameter,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
                    }
                }
                else
                {
                    status=GeneralCurvePiece(span,j,bp,out var degree,out var domain,out var lower,out var upper);if(status!=AlgorithmStatus.Success)return status;
                    if(!(upper>lower))continue;var boundary=new SurfaceIntersectionPatch(new BezierSurfacePatch(bp[..(4*(degree+1))],degree,0,domain));
                    status=BezierCurveIntersection.Find(ray,boundary,ray.Domain.U0,ray.Domain.U1,lower,upper,roots,ref nr);if(status!=AlgorithmStatus.Success)return status;
                    for(var k=0;k<nr;k++)
                    {
                        if(!GeneralCurveNativeParameter(span,roots[k].Point,roots[k].Second,out var parameter))return AlgorithmStatus.NumericalFailure;
                        roots[k].Second=parameter;
                    }
                }
                for(var k=0;k<nr;k++)if(!OperationIntersection.AddPoint(output,ref count,roots[k].Point,roots[k].First,roots[k].Second,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
            }
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntersectNativeBlendParameterRay(SurfTag surface,PK_UV_s query,double du,double dv,in ModelingEdge edge,
        Span<OperationIntersectionPoint> output,out BufferCount count,out bool handled)
    {
        count=0;handled=true;var data=ICurveDataPool[Curves[GetCurveSlotByTag(edge.Curve)].DataIndex];
        var side=SameSheetSupport(surface,data.Surface0Tag)?0:SameSheetSupport(surface,data.Surface1Tag)?1:-1;if(side<0)return AlgorithmStatus.Unsupported;
        var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
        var definition=program.Spines[data.ProgramSpine];var charts=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);
        var support=program.Surfaces[side==0?definition.First:definition.Second];
        if(!support.IsBlend)return AlgorithmStatus.Unsupported;
        var periodic=SpineParameterMap.Periodic(program.Spines[support.Spine]);
        if(periodic&&dv==0)
        {
            var finite=Surfaces[GetSurfaceSlotByTag(surface)];
            // One angular lift is meaningful only on a single native period.
            // Wider finite uses need multiple occurrences, not a nearest lift.
            if(!(finite.UMax>finite.UMin)||finite.UMax-finite.UMin>Math.Tau)
                return AlgorithmStatus.Unsupported;
        }
        var level=periodic?new AnalyticSurface(SurfaceClass.Plane,default,new(){X=1},new(){Y=1})
            :new AnalyticSurface(SurfaceClass.Plane,new(){X=query.param[0],Y=query.param[1]},new(){X=dv,Y=-du},new(){Z=1});
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        Span<ScalarRootIsolation> isolated=stackalloc ScalarRootIsolation[IntersectionPointCapacity],accepted=stackalloc ScalarRootIsolation[IntersectionPointCapacity];
        Span<SurfaceBoundCacheEntry> cache=stackalloc SurfaceBoundCacheEntry[128];cache.Clear();
        Span<IntervalNumber> phaseBounds=stackalloc IntervalNumber[9];
        Span<KernelVector3> point=stackalloc KernelVector3[1];Span<double> paired=stackalloc double[4];var covered=edge.Start;
        for(var chart=0;chart<charts.Length-1;chart++)
        {
            var first=Math.Max(edge.Start,charts[chart].Parameter);var last=Math.Min(edge.End,charts[chart+1].Parameter);if(!(last>first))continue;
            if(first>covered)return AlgorithmStatus.NotConverged;covered=last;
            var nativeCoordinate=periodic?new SurfaceIntersectionPatch(program,data.ProgramSpine,side,first,last,query.param[0],query.param[1],du,dv)
                :new SurfaceIntersectionPatch(program,data.ProgramSpine,side,first,last,true);
            var coordinate=new SurfaceIntersectionPatch(nativeCoordinate,cache);
            var record=Curves[GetCurveSlotByTag(edge.Curve)];coordinate=new(coordinate,edge.Curve,record.Header.Generation,record.Sense);var n=0;
            var status=SurfaceIsoPlaneRoots.Find(coordinate,level,first,last,roots,ref n,0,true,isolated,use:RootIsolationUse.NativeSupportCoordinate);
            if(status!=AlgorithmStatus.Success)
            {
                LastSheetAlgorithmStatus=status;
                return status;
            }
            for(var root=0;root<n;root++)
            {
                if(periodic)
                {
                    var station=isolated[root].Root;
                    status=coordinate.Bound(new(station.Lower,station.Upper,0,0),phaseBounds);if(status!=AlgorithmStatus.Success)return status;
                    if(phaseBounds[1].Upper<0)continue;
                    if(!(phaseBounds[1].Lower>0))return AlgorithmStatus.NotConverged;
                }
                var duplicate=false;for(var previous=0;previous<count;previous++)duplicate|=accepted[previous].SameRoot(isolated[root]);if(duplicate)continue;
                status=JointNestedBlendEvaluation.EvaluateSpine(program,data.ProgramSpine,roots[root].First,0,point,paired);if(status!=AlgorithmStatus.Success)return status;
                var along=dv!=0?(paired[2*side+1]-query.param[1])/dv:(paired[2*side]-query.param[0])/du;
                if(periodic&&dv==0)
                {
                    var recordSurface=Surfaces[GetSurfaceSlotByTag(surface)];var u=paired[2*side];
                    u+=Math.Tau*Math.Round(((recordSurface.UMin+recordSurface.UMax)*.5-u)/Math.Tau);
                    along=(u-query.param[0])/du;
                }
                var uHit=Math.FusedMultiplyAdd(along,du,query.param[0]);var vHit=Math.FusedMultiplyAdd(along,dv,query.param[1]);
                var finite=Surfaces[GetSurfaceSlotByTag(surface)];
                if(uHit<finite.UMin||uHit>finite.UMax||vHit<finite.VMin||vHit>finite.VMax)continue;
                if(count==output.Length||count==accepted.Length)return AlgorithmStatus.OutputTooSmall;
                accepted[count]=isolated[root];output[count++]=new(){Point=point[0],First=along,Second=roots[root].First,Kind=IntersectionVectorKind.Simple};
            }
        }
        if(covered!=edge.End)return AlgorithmStatus.NotConverged;
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }

    private static bool ClipParameterRay(double origin,double direction,double lower,double upper,ref double start,ref double end)
    {
        if(direction==0)return origin>=lower&&origin<=upper;
        var a=(new IntervalNumber(lower)-origin)/direction;var b=(new IntervalNumber(upper)-origin)/direction;if(direction<0)(a,b)=(b,a);
        start=Math.Max(start,a.Lower);end=Math.Min(end,b.Upper);return end>=start;
    }
}

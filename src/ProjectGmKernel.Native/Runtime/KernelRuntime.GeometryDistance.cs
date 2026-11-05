using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.Projection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int GeomRangeVector(GeomTag geometry,PK_VECTOR_s vector,PK_GEOM_range_vector_o_s* options,
        PK_range_result_t* status,PK_range_1_r_s* result)
    {
        if(Dispatcher.IsExecuting)return GeomRangeVectorCore(geometry,vector,options,status,result);
        var command=new GeometryDistanceCommand{First=geometry,Vector=vector,VectorOptions=options,Status=status,PointResult=result};
        return Dispatch(ApiId.GeomRangeVector,ConcurrencyKind.Concurrent,AccessKind.ReadOnly,ref command,geometry);
    }

    public static int GeomRange(GeomTag first,GeomTag second,PK_GEOM_range_o_s* options,
        PK_range_result_t* status,PK_range_2_r_s* result)
    {
        if(Dispatcher.IsExecuting)return GeomRangeCore(first,second,options,status,result);
        var command=new GeometryDistanceCommand{First=first,Second=second,Options=options,Status=status,PairResult=result};
        return Dispatch(ApiId.GeomRange,ConcurrencyKind.Concurrent,AccessKind.ReadOnly,ref command,first);
    }

    private struct GeometryDistanceCommand:IKernelCommand
    {
        internal GeomTag First,Second;
        internal PK_VECTOR_s Vector;
        internal PK_GEOM_range_vector_o_s* VectorOptions;
        internal PK_GEOM_range_o_s* Options;
        internal PK_range_result_t* Status;
        internal PK_range_1_r_s* PointResult;
        internal PK_range_2_r_s* PairResult;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try{return Options!=null?GeomRangeCore(First,Second,Options,Status,PairResult):GeomRangeVectorCore(First,Vector,VectorOptions,Status,PointResult);}
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    private static int ValidateRangeOptions(KernelLogical haveTolerance,double tolerance,in PK_range_bound_s bound,
        in PK_range_guess_s guess,PK_range_opt_t optimization,out double accuracy)
    {
        accuracy=haveTolerance!=0?tolerance:1e-8;
        if(haveTolerance>1||!(accuracy>0)||!double.IsFinite(accuracy)||bound.have_lower_bound>1||bound.have_upper_bound>1
            ||bound.have_lower_bound!=0&&(!double.IsFinite(bound.lower_bound)||bound.lower_bound<0)
            ||bound.have_upper_bound!=0&&(!double.IsFinite(bound.upper_bound)||bound.upper_bound<0)
            ||bound.have_lower_bound!=0&&bound.have_upper_bound!=0&&bound.lower_bound>bound.upper_bound
            ||optimization is not (ParasolidConstants.PK_range_opt_accuracy_c or ParasolidConstants.PK_range_opt_performance_c))return ParasolidConstants.PK_ERROR_bad_parameter;
        if(guess.type is not (ParasolidConstants.PK_range_guess_no_c or ParasolidConstants.PK_range_guess_param_c or ParasolidConstants.PK_range_guess_vector_c))return ParasolidConstants.PK_ERROR_bad_parameter;
        if(guess.type==ParasolidConstants.PK_range_guess_vector_c&&!IsFinite(Vector(guess.vector.coord[0],guess.vector.coord[1],guess.vector.coord[2])))return ParasolidConstants.PK_ERROR_bad_parameter;
        if(guess.type==ParasolidConstants.PK_range_guess_param_c&&(!double.IsFinite(guess.parameters[0])||!double.IsFinite(guess.parameters[1])))return ParasolidConstants.PK_ERROR_bad_parameter;
        return 0;
    }

    private static PK_range_result_t RangeBoundResult(double distance,in PK_range_bound_s bound)
        =>bound.have_lower_bound!=0&&distance<bound.lower_bound?ParasolidConstants.PK_range_result_lower_c
            :bound.have_upper_bound!=0&&distance>bound.upper_bound?ParasolidConstants.PK_range_result_upper_c:ParasolidConstants.PK_range_result_found_c;

    private static int DistanceGeometry(GeomTag tag,out PoolKind pool,out DataSlot slot)
    {
        pool=default;slot=-1;
        if(!IsValidTag(tag))return ParasolidConstants.PK_ERROR_not_a_tag;
        var entry=TagRec(tag);pool=(PoolKind)entry.Pool;slot=entry.Slot;
        if(pool is not (PoolKind.Point or PoolKind.Curve or PoolKind.Surface))return ParasolidConstants.PK_ERROR_bad_class;
        if(PoolPartitionOf(pool,slot)!=CurrentPartition)return ParasolidConstants.PK_ERROR_not_in_same_partition;
        return 0;
    }

    private static int GeomRangeVectorCore(GeomTag geometry,PK_VECTOR_s vector,PK_GEOM_range_vector_o_s* options,
        PK_range_result_t* status,PK_range_1_r_s* result)
    {
        if(status!=null)*status=ParasolidConstants.PK_range_result_not_found_c;if(result!=null)*result=default;
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(options==null||status==null||result==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=2)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        var point=Vector(vector.coord[0],vector.coord[1],vector.coord[2]);if(!IsFinite(point))return ParasolidConstants.PK_ERROR_bad_parameter;
        var error=ValidateRangeOptions(options->have_tolerance,options->tolerance,options->bound,options->guess,options->opt_level,out var accuracy);if(error!=0)return error;
        error=ClosestGeometryVector(geometry,point,accuracy,out var end,out var distance);if(error!=0)return error;
        var found=RangeBoundResult(distance,options->bound);*status=found;
        if(found==ParasolidConstants.PK_range_result_found_c)*result=new(){distance=distance,end=end};return 0;
    }

    private static int ClosestGeometryVector(GeomTag geometry,in KernelVector3 point,double tolerance,
        out PK_range_end_s end,out double distance)
    {
        end=default;distance=0;var error=DistanceGeometry(geometry,out var pool,out var slot);if(error!=0)return error;
        KernelVector3 foot;double u=0,v=0;var region=false;
        if(pool==PoolKind.Point)foot=Points[slot].Position;
        else if(pool==PoolKind.Surface&&TryPrepareSurface(Surfaces[slot],out var analytic))
        {
            var status=ClosestAnalyticVector(analytic,point,out foot);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            status=OperationIntersection.Parameters(analytic,foot,out u,out v);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var offset=Sub(point,analytic.Origin);var z=Dot(offset,analytic.Axis);var radial=Sub(offset,Scale(analytic.Axis,z));var rho=OperationIntersection.Norm(radial);
            var footOffset=Sub(foot,analytic.Origin);var footRho=OperationIntersection.Norm(Sub(footOffset,Scale(analytic.Axis,Dot(footOffset,analytic.Axis))));
            region=analytic.Kind==SurfaceClass.Sphere&&OperationIntersection.Norm(offset)==0
                ||analytic.Kind==SurfaceClass.Cylinder&&rho==0
                ||analytic.Kind==SurfaceClass.Cone&&rho==0&&footRho>0
                ||analytic.Kind==SurfaceClass.Torus&&(rho==0&&footRho>0||rho==analytic.Radius&&z==0);
        }
        else if(pool==PoolKind.Surface&&Surfaces[slot].Class==SurfaceClass.BSurface)
        {
            var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};
            var status=ClosestBSurface(GetBSurfaceView(BSurfaceDataStore[Surfaces[slot].DataIndex]),point,tolerance,ref best);
            if(status!=AlgorithmStatus.Success)return IntersectionError(status);u=best.U;v=best.V;foot=best.Point;
        }
        else if(pool==PoolKind.Surface&&Surfaces[slot].Class==SurfaceClass.BlendSurface||pool==PoolKind.Curve&&Curves[slot].Class==CurveClass.ICurve&&ICurveDataPool[Curves[slot].DataIndex].ProgramBlock>0)
        {
            error=ClosestProceduralVector(geometry,point,tolerance,out foot,out u,out v);if(error!=0)return error;
        }
        else if(pool==PoolKind.Curve)
        {
            var curve=Curves[slot];var interval=new PK_INTERVAL_s();interval.value[0]=curve.TMin;interval.value[1]=curve.TMax;
            error=ResolveIntersectionCurve(geometry,interval,out var span);if(error!=0)return error;
            if(Curves[span.Slot].Class==CurveClass.Line)
            {
                GetIntersectionLine(Curves[span.Slot],out var origin,out var direction);u=Dot(Sub(point,origin),direction)/Dot(direction,direction);
                if(curve.Class==CurveClass.TRCurve)u=Math.Clamp(u,span.Lower,span.Upper);
                foot=Add(origin,Scale(direction,u));u=(u-span.Offset)/span.Scale;
            }
            else if(TryGetIntersectionConic(Curves[span.Slot],out var conic)&&conic.Radius==conic.Minor&&span.Upper-span.Lower==Math.Tau)
            {
                var offset=Sub(point,conic.Origin);var z=Dot(offset,conic.Axis);var radial=Sub(offset,Scale(conic.Axis,z));var length=OperationIntersection.Norm(radial);
                foot=Add(conic.Origin,Scale(length>0?Scale(radial,1/length):conic.Reference,conic.Radius));region=length==0;
                if(!GeneralCurveNativeParameter(span,foot,0,out u))return ParasolidConstants.PK_ERROR_eval_failure;u=(u-span.Offset)/span.Scale;
            }
            else
            {
                var count=GeneralCurvePieceCount(span);if(count<=0)return ParasolidConstants.PK_ERROR_not_implemented;
                Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];Span<IntervalNumber> bounded=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
                var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};
                for(var i=0;i<count;i++)
                {
                    var status=GeneralCurvePiece(span,i,poles,out var degree,out var domain,out var lo,out var hi);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    if(!(hi>lo))continue;
                    var patch=new BezierSurfacePatch(poles[..(4*(degree+1))],degree,0,domain);
                    var box=new SurfaceParameterBox(lo,hi,0,1);status=BezierSurfaceBounds.Restrict(patch,box,bounded);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    status=BSplineProjection.Closest(new(bounded[..(4*(degree+1))],degree,0,box),point,tolerance,ref best);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                }
                if(!double.IsFinite(best.DistanceUpper)||!GeneralCurveNativeParameter(span,best.Point,best.U,out u))return ParasolidConstants.PK_ERROR_eval_failure;
                foot=best.Point;u=(u-span.Offset)/span.Scale;
            }
        }
        else return ParasolidConstants.PK_ERROR_not_implemented;
        end.entity=geometry;end.region=region?(byte)1:(byte)0;end.vector.coord[0]=foot.X;end.vector.coord[1]=foot.Y;end.vector.coord[2]=foot.Z;end.parameters[0]=u;end.parameters[1]=v;
        distance=OperationIntersection.Norm(Sub(foot,point));return double.IsFinite(distance)?0:ParasolidConstants.PK_ERROR_eval_failure;
    }

    private static AlgorithmStatus ClosestAnalyticVector(in AnalyticSurface surface,in KernelVector3 point,out KernelVector3 foot)
    {
        foot=default;var offset=Sub(point,surface.Origin);var z=Dot(offset,surface.Axis);
        var radial=Sub(offset,Scale(surface.Axis,z));var rho=OperationIntersection.Norm(radial);var direction=rho>0?Scale(radial,1/rho):surface.X;
        switch(surface.Kind)
        {
            case SurfaceClass.Plane:foot=Sub(point,Scale(surface.Axis,z));break;
            case SurfaceClass.Sphere:
                var length=OperationIntersection.Norm(offset);foot=Add(surface.Origin,Scale(length>0?Scale(offset,1/length):surface.X,surface.Radius));break;
            case SurfaceClass.Cylinder:foot=Add(surface.Origin,Add(Scale(surface.Axis,z),Scale(direction,surface.Radius)));break;
            case SurfaceClass.Cone:
                var k=surface.Secondary;
                if(k==0){foot=Add(surface.Origin,Add(Scale(surface.Axis,z),Scale(direction,surface.Radius)));break;}
                var apex=-surface.Radius/k;var t=(rho*k+(z-apex))/(1+k*k);t=k>0?Math.Max(0,t):Math.Min(0,t);
                foot=Add(surface.Origin,Add(Scale(surface.Axis,apex+t),Scale(direction,k*t)));break;
            case SurfaceClass.Torus:
                var delta=rho-surface.Radius;var length2=Math.Sqrt(delta*delta+z*z);
                var r=length2>0?surface.Radius+surface.Secondary*delta/length2:surface.Radius+surface.Secondary;
                var h=length2>0?surface.Secondary*z/length2:0;
                if(r<0){r=0;h=Math.CopySign(Math.Sqrt(Math.Max(0,(surface.Secondary-surface.Radius)*(surface.Secondary+surface.Radius))),z);}
                foot=Add(surface.Origin,Add(Scale(surface.Axis,h),Scale(direction,r)));break;
            default:return AlgorithmStatus.Unsupported;
        }
        return IsFinite(foot)?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }
}

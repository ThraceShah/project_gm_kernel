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
    private struct DistanceOperand
    {
        internal GeomTag Tag;
        internal PoolKind Pool;
        internal DataSlot Slot;
        internal IntersectionCurveSpan Curve;
        internal AnalyticSurface Analytic;
        internal BufferCount Pieces;
        internal bool Bounded,IsAnalytic,IsBlend,IsProceduralCurve;
        internal IntervalBox3 Extent;
        internal KernelVector3 Seed;
    }

    private static int GeomRangeCore(GeomTag first,GeomTag second,PK_GEOM_range_o_s* options,
        PK_range_result_t* status,PK_range_2_r_s* result)
    {
        if(status!=null)*status=ParasolidConstants.PK_range_result_not_found_c;if(result!=null)*result=default;
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(options==null||status==null||result==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=3)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        var guesses=(PK_range_guess_s*)options->guesses;var bounds=(PK_range_param_bound_s*)options->param_bound;
        var error=ValidateRangeOptions(options->have_tolerance,options->tolerance,options->bound,guesses[0],options->opt_level,out var tolerance);if(error!=0)return error;
        error=ValidateRangeOptions(options->have_tolerance,options->tolerance,options->bound,guesses[1],options->opt_level,out _);if(error!=0)return error;
        if(options->range_type!=ParasolidConstants.PK_range_type_minimum_c||bounds[0].have_param_bound!=0||bounds[1].have_param_bound!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        error=PrepareDistanceOperand(first,out var a);if(error!=0)return error;
        error=PrepareDistanceOperand(second,out var b);if(error!=0)return error;
        PK_range_end_s ea=default,eb=default;double distance;
        var exact=ExactGeometryDistance(a,b,out ea,out eb,out distance);
        if(exact==AlgorithmStatus.Unsupported)
        {
            error=SeparatedProceduralSphereDistance(a,b,tolerance,out var handled,out ea,out eb,out distance);if(error!=0)return error;
            if(handled)exact=AlgorithmStatus.Success;
        }
        if(exact==AlgorithmStatus.Success){}
        else if(exact!=AlgorithmStatus.Unsupported)return IntersectionError(exact);
        else if(a.Pool==PoolKind.Point||b.Pool==PoolKind.Point)
        {
            var pointFirst=a.Pool==PoolKind.Point;var point=pointFirst?a:b;var other=pointFirst?b:a;
            error=ClosestGeometryVector(other.Tag,point.Seed,tolerance,out var foot,out distance);if(error!=0)return error;
            var end=new PK_range_end_s{entity=point.Tag};WriteVector(&end.vector,point.Seed);
            if(pointFirst){ea=end;eb=foot;}else{ea=foot;eb=end;}
        }
        else
        {
            if(!a.Bounded&&!b.Bounded)return ParasolidConstants.PK_ERROR_not_implemented;
            var upper=OperationIntersection.Norm(Sub(a.Seed,b.Seed));if(!double.IsFinite(upper))return ParasolidConstants.PK_ERROR_eval_failure;
            var region=ExpandDistanceBox(a.Bounded?a.Extent:b.Extent,Math.BitIncrement(upper+tolerance));
            var best=new GeometryDistancePair{Upper=double.PositiveInfinity};
            Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
            if((long)a.Pieces*b.Pieces>200000)return ParasolidConstants.PK_ERROR_not_implemented;
            for(var i=0;i<a.Pieces;i++)
            {
                var found=DistancePatch(a,region,i,ap,out var patch,out var empty);if(found!=AlgorithmStatus.Success)return IntersectionError(found);if(empty)continue;
                for(var j=0;j<b.Pieces;j++)
                {
                    found=DistancePatch(b,region,j,bp,out var other,out empty);if(found!=AlgorithmStatus.Success)return IntersectionError(found);if(empty)continue;
                    found=BezierPairDistance.MinimumProcedural(patch,other,tolerance,ref best);if(found!=AlgorithmStatus.Success)return IntersectionError(found);
                }
            }
            if(!double.IsFinite(best.Upper))return ParasolidConstants.PK_ERROR_eval_failure;
            error=DistanceGeometryEnd(a,best.First,best.Parameters.U,best.Parameters.V,out ea);if(error!=0)return error;
            error=DistanceGeometryEnd(b,best.Second,best.Parameters.S,best.Parameters.T,out eb);if(error!=0)return error;
            distance=OperationIntersection.Norm(Sub(best.First,best.Second));
        }
        var outcome=RangeBoundResult(distance,options->bound);*status=outcome;
        if(outcome==ParasolidConstants.PK_range_result_found_c){result->distance=distance;var ends=(PK_range_end_s*)result->ends;ends[0]=ea;ends[1]=eb;}
        return 0;
    }

    private static int SeparatedProceduralSphereDistance(in DistanceOperand first,in DistanceOperand second,double tolerance,
        out bool handled,out PK_range_end_s a,out PK_range_end_s b,out double distance)
    {
        handled=false;a=b=default;distance=0;var reverse=first.IsAnalytic&&first.Analytic.Kind==SurfaceClass.Sphere;
        var sphere=reverse?first:second;var other=reverse?second:first;
        if(!sphere.IsAnalytic||sphere.Analytic.Kind!=SurfaceClass.Sphere||!other.IsBlend&&!other.IsProceduralCurve)return 0;
        var error=ClosestProceduralVector(other.Tag,sphere.Analytic.Origin,tolerance,out var foot,out var u,out var v);if(error!=0)return error;
        var radial=Sub(foot,sphere.Analytic.Origin);var length=OperationIntersection.Norm(radial);
        // The certified center minimum must place the entire procedural
        // domain outside the sphere. Intersecting/contained cases retain the
        // general pair solver.
        if(length<=sphere.Analytic.Radius+tolerance)return 0;
        var sphereFoot=Add(sphere.Analytic.Origin,Scale(radial,sphere.Analytic.Radius/length));
        error=DistanceGeometryEnd(other,foot,u,v,out var proceduralEnd);if(error!=0)return error;
        error=DistanceGeometryEnd(sphere,sphereFoot,0,0,out var sphereEnd);if(error!=0)return error;
        a=reverse?sphereEnd:proceduralEnd;b=reverse?proceduralEnd:sphereEnd;distance=length-sphere.Analytic.Radius;handled=true;return 0;
    }

    private static int PrepareDistanceOperand(GeomTag tag,out DistanceOperand operand)
    {
        operand=default;var error=DistanceGeometry(tag,out operand.Pool,out operand.Slot);if(error!=0)return error;operand.Tag=tag;
        if(operand.Pool==PoolKind.Point)
        {
            operand.Seed=Points[operand.Slot].Position;operand.Bounded=true;operand.Pieces=1;
            operand.Extent=AnalyticBallBounds(operand.Seed,0);return 0;
        }
        Span<KernelVector3> point=stackalloc KernelVector3[1];
        if(operand.Pool==PoolKind.Curve)
        {
            var record=Curves[operand.Slot];var interval=new PK_INTERVAL_s();interval.value[0]=record.TMin;interval.value[1]=record.TMax;
            if(record.Class==CurveClass.ICurve&&ICurveDataPool[record.DataIndex].ProgramBlock>0)
            {
                var data=ICurveDataPool[record.DataIndex];var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);var spine=program.Spines[data.ProgramSpine];
                operand.IsProceduralCurve=operand.Bounded=true;operand.Pieces=spine.ChartCount-1;
                var found=EvaluateCurveCore(in record,record.TMin+(record.TMax-record.TMin)*.5,0,false,point,out _);if(found!=AlgorithmStatus.Success)return EvaluationError(found);operand.Seed=point[0];
                found=BlendExtent(program,data.ProgramSpine,new(record.TMin,record.TMax,0,1),out operand.Extent,spineOnly:true);return found==AlgorithmStatus.Success?0:IntersectionError(found);
            }
            error=ResolveIntersectionCurve(tag,interval,out operand.Curve);if(error!=0)return error;
            operand.Pieces=GeneralCurvePieceCount(operand.Curve);if(operand.Pieces<=0)return ParasolidConstants.PK_ERROR_not_implemented;
            var evaluated=EvaluateCurveCore(in record,record.TMin+(record.TMax-record.TMin)*.5,0,false,point,out _);if(evaluated!=AlgorithmStatus.Success)return EvaluationError(evaluated);operand.Seed=point[0];
            var source=Curves[operand.Curve.Slot];
            if(source.Class==CurveClass.BCurve)
            {
                var view=GetBCurveView(BCurveDataStore[source.DataIndex]);if(view.Dimension!=3&&view.Dimension!=4)return ParasolidConstants.PK_ERROR_not_implemented;
                IntervalNumber x=new(double.PositiveInfinity,double.NegativeInfinity),y=x,z=x;
                for(var i=0;i<view.VertexCount;i++)
                {
                    var at=i*view.Dimension;IntervalNumber w=view.Rational?view.Vertices[at+3]:1;
                    x=IntervalNumber.Hull(x,new IntervalNumber(view.Vertices[at])/w);y=IntervalNumber.Hull(y,new IntervalNumber(view.Vertices[at+1])/w);z=IntervalNumber.Hull(z,new IntervalNumber(view.Vertices[at+2])/w);
                }
                operand.Extent=new(x.Lower,x.Upper,y.Lower,y.Upper,z.Lower,z.Upper);operand.Bounded=true;
            }
            else if(TryGetIntersectionConic(source,out var conic)){operand.Extent=AnalyticBallBounds(conic.Origin,Math.Max(conic.Radius,conic.Minor)*2);operand.Bounded=true;}
            else if(record.Class==CurveClass.TRCurve){operand.Bounded=true;var box=AnalyticBallBounds(point[0],(record.TMax-record.TMin)*2);operand.Extent=box;}
            return 0;
        }
        var support=Surfaces[operand.Slot];
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);
        var analytic=TryPrepareSurface(support,out operand.Analytic);
        var status=EvaluateSurfaceCore(in support,analytic?0:support.UMin+(support.UMax-support.UMin)*.5,analytic?0:support.VMin+(support.VMax-support.VMin)*.5,layout,point,0);
        if(status!=AlgorithmStatus.Success)return EvaluationError(status);operand.Seed=point[0];
        if(support.Class==SurfaceClass.BlendSurface)
        {
            var data=BlendedEdgeDataPool[support.DataIndex];var program=GetNestedBlendProgram(data);var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];
            operand.IsBlend=true;operand.Bounded=true;operand.Pieces=spine.ChartCount-1;
            status=RegularSurfaceExtent(operand.Slot,out operand.Extent);return status==AlgorithmStatus.Success?0:IntersectionError(status);
        }
        if(support.Class==SurfaceClass.BSurface)
        {
            var view=GetBSurfaceView(BSurfaceDataStore[support.DataIndex]);operand.Pieces=(view.UVertexCount-view.UDegree)*(view.VVertexCount-view.VDegree);
            status=NaturalSurfaceExtent(view,out operand.Extent);operand.Bounded=status==AlgorithmStatus.Success;return status==AlgorithmStatus.Success?0:IntersectionError(status);
        }
        if(!analytic)return ParasolidConstants.PK_ERROR_not_implemented;
        operand.IsAnalytic=true;operand.Pieces=AnalyticBezierPatches.Count(operand.Analytic);if(operand.Pieces<=0)return ParasolidConstants.PK_ERROR_not_implemented;
        if(support.Class is SurfaceClass.Sphere or SurfaceClass.Torus)
        {
            operand.Bounded=true;var radius=support.Class==SurfaceClass.Sphere?operand.Analytic.Radius:operand.Analytic.Radius+operand.Analytic.Secondary;
            operand.Extent=AnalyticBallBounds(operand.Analytic.Origin,Math.BitIncrement(radius*2));
        }
        return 0;
    }

    private static IntervalBox3 ExpandDistanceBox(in IntervalBox3 box,double distance)
    {
        var delta=new IntervalNumber(-distance,distance);var x=new IntervalNumber(box.XLo,box.XHi)+delta;var y=new IntervalNumber(box.YLo,box.YHi)+delta;var z=new IntervalNumber(box.ZLo,box.ZHi)+delta;
        return new(x.Lower,x.Upper,y.Lower,y.Upper,z.Lower,z.Upper);
    }

    private static AlgorithmStatus DistancePatch(in DistanceOperand operand,in IntervalBox3 region,BufferOffset index,Span<IntervalNumber> poles,
        out SurfaceIntersectionPatch patch,out bool empty)
    {
        patch=default;empty=false;
        if(operand.IsBlend)
        {
            var record=Surfaces[operand.Slot];var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);var spine=program.Spines[program.Surfaces[data.ProgramRoot].Spine];var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
            var lo=Math.Max(record.UMin,charts[index].Parameter);var hi=Math.Min(record.UMax,charts[index+1].Parameter);empty=hi<=lo;
            patch=new(program,data.ProgramRoot,new(lo,hi,record.VMin,record.VMax));return AlgorithmStatus.Success;
        }
        if(operand.IsProceduralCurve)
        {
            var record=Curves[operand.Slot];var data=ICurveDataPool[record.DataIndex];var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);var spine=program.Spines[data.ProgramSpine];var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);
            var lo=Math.Max(record.TMin,charts[index].Parameter);var hi=Math.Min(record.TMax,charts[index+1].Parameter);empty=hi<=lo;patch=new(program,data.ProgramSpine,lo,hi);return AlgorithmStatus.Success;
        }
        var status=DistanceGeometryPiece(operand,region,index,poles,out var p,out var q,out var domain,out empty);if(status!=AlgorithmStatus.Success||empty)return status;
        patch=new(new BezierSurfacePatch(poles[..(4*(p+1)*(q+1))],p,q,domain));return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus DistanceGeometryPiece(in DistanceOperand operand,in IntervalBox3 region,BufferOffset index,
        Span<IntervalNumber> poles,out SplineDegree p,out SplineDegree q,out SurfaceParameterBox domain,out bool empty)
    {
        p=q=0;domain=new(0,1,0,1);empty=false;
        if(operand.Pool==PoolKind.Point){poles[0]=operand.Seed.X;poles[1]=operand.Seed.Y;poles[2]=operand.Seed.Z;poles[3]=1;return AlgorithmStatus.Success;}
        if(operand.IsAnalytic)return AnalyticBezierPatches.Create(operand.Analytic,region,index,poles,out p,out q,out domain);
        if(operand.Pool==PoolKind.Surface)
        {
            var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[operand.Slot].DataIndex]);p=view.UDegree;q=view.VDegree;
            var u=index/(view.VVertexCount-q)+p;var v=index%(view.VVertexCount-q)+q;
            domain=new(view.UKnots[u],view.UKnots[u+1],view.VKnots[v],view.VKnots[v+1]);empty=!(domain.U1>domain.U0&&domain.V1>domain.V0);
            return empty?AlgorithmStatus.Success:BezierSurfaceBounds.Extract(view,u,v,poles);
        }
        var span=operand.Curve;
        if(Curves[span.Slot].Class==CurveClass.Line)
        {
            GetIntersectionLine(Curves[span.Slot],out var origin,out var direction);
            var parameter=((new IntervalNumber(region.XLo,region.XHi)-origin.X)*direction.X+(new IntervalNumber(region.YLo,region.YHi)-origin.Y)*direction.Y+(new IntervalNumber(region.ZLo,region.ZHi)-origin.Z)*direction.Z)/Dot(direction,direction);
            span.Lower=operand.Bounded?Math.Max(span.Lower,parameter.Lower):parameter.Lower;span.Upper=operand.Bounded?Math.Min(span.Upper,parameter.Upper):parameter.Upper;
            if(!(span.Upper>span.Lower)){empty=true;return AlgorithmStatus.Success;}
        }
        Span<IntervalNumber> raw=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        var status=GeneralCurvePiece(span,index,raw,out p,out var full,out var lo,out var hi);if(status!=AlgorithmStatus.Success)return status;
        if(!(hi>lo)){empty=true;return AlgorithmStatus.Success;}
        domain=new(lo,hi,0,1);return BezierSurfaceBounds.Restrict(new(raw[..(4*(p+1))],p,0,full),domain,poles);
    }

    private static int DistanceGeometryEnd(in DistanceOperand operand,in KernelVector3 point,double u,double v,out PK_range_end_s end)
    {
        end=default;end.entity=operand.Tag;
        end.vector.coord[0]=point.X;end.vector.coord[1]=point.Y;end.vector.coord[2]=point.Z;
        if(operand.IsAnalytic)
        {var status=OperationIntersection.Parameters(operand.Analytic,point,out u,out v);if(status!=AlgorithmStatus.Success)return IntersectionError(status);}
        else if(operand.Pool==PoolKind.Curve&&!operand.IsProceduralCurve)
        {if(!GeneralCurveNativeParameter(operand.Curve,point,u,out u))return ParasolidConstants.PK_ERROR_eval_failure;u=(u-operand.Curve.Offset)/operand.Curve.Scale;v=0;}
        end.parameters[0]=u;end.parameters[1]=v;return 0;
    }
}

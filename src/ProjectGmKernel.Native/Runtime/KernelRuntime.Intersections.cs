using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private const BufferCount IntersectionPointCapacity=256;

    public static int CurveIntersectCurve(CurveTag first,PK_INTERVAL_s firstInterval,CurveTag second,PK_INTERVAL_s secondInterval,
        PK_CURVE_intersect_curve_o_s* options,BufferCount* count,PK_VECTOR_s** points,double** firstParameters,double** secondParameters,int** kinds)
    {
        if(count!=null)*count=0;if(points!=null)*points=null;if(firstParameters!=null)*firstParameters=null;if(secondParameters!=null)*secondParameters=null;if(kinds!=null)*kinds=null;
        if(Dispatcher.IsExecuting)return CurveIntersectCurveCore(first,firstInterval,second,secondInterval,options,count,points,firstParameters,secondParameters,kinds);
        var command=new CurveIntersectionCommand{First=first,Second=second,FirstInterval=firstInterval,SecondInterval=secondInterval,
            Options=options,Count=count,Points=points,FirstParameters=firstParameters,SecondParameters=secondParameters,Kinds=kinds};
        return Dispatch(ApiId.CurveIntersectCurve,ConcurrencyKind.Concurrent,AccessKind.ReadOnly,ref command,first);
    }
    private struct CurveIntersectionCommand:IKernelCommand
    {
        internal CurveTag First,Second;internal PK_INTERVAL_s FirstInterval,SecondInterval;
        internal PK_CURVE_intersect_curve_o_s* Options;internal BufferCount* Count;internal PK_VECTOR_s** Points;
        internal double** FirstParameters,SecondParameters;internal PK_intersect_vector_t** Kinds;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try{return CurveIntersectCurveCore(First,FirstInterval,Second,SecondInterval,Options,Count,Points,FirstParameters,SecondParameters,Kinds);}
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    public static int SurfIntersectCurve(SurfTag surface,CurveTag curve,PK_INTERVAL_s interval,PK_SURF_intersect_curve_o_s* options,
        BufferCount* count,PK_VECTOR_s** points,PK_UV_s** parameters,double** curveParameters,int** kinds)
    {
        if(count!=null)*count=0;if(points!=null)*points=null;if(parameters!=null)*parameters=null;if(curveParameters!=null)*curveParameters=null;if(kinds!=null)*kinds=null;
        if(Dispatcher.IsExecuting)return SurfIntersectCurveCore(surface,curve,interval,options,count,points,parameters,curveParameters,kinds);
        var command=new SurfaceCurveIntersectionCommand{Surface=surface,Curve=curve,Interval=interval,Options=options,Count=count,
            Points=points,Parameters=parameters,CurveParameters=curveParameters,Kinds=kinds};
        return Dispatch(ApiId.SurfIntersectCurve,ConcurrencyKind.Concurrent,AccessKind.ReadOnly,ref command,surface);
    }
    private struct SurfaceCurveIntersectionCommand:IKernelCommand
    {
        internal SurfTag Surface;internal CurveTag Curve;internal PK_INTERVAL_s Interval;internal PK_SURF_intersect_curve_o_s* Options;
        internal BufferCount* Count;internal PK_VECTOR_s** Points;internal PK_UV_s** Parameters;internal double** CurveParameters;internal PK_intersect_vector_t** Kinds;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try{return SurfIntersectCurveCore(Surface,Curve,Interval,Options,Count,Points,Parameters,CurveParameters,Kinds);}
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    public static int SurfIntersectSurf(SurfTag first,SurfTag second,PK_SURF_intersect_surf_o_s* options,
        BufferCount* pointCount,PK_VECTOR_s** points,BufferCount* curveCount,CurveTag** curves,PK_INTERVAL_s** intervals,int** kinds)
    {
        if(pointCount!=null)*pointCount=0;if(points!=null)*points=null;if(curveCount!=null)*curveCount=0;if(curves!=null)*curves=null;if(intervals!=null)*intervals=null;if(kinds!=null)*kinds=null;
        if(Dispatcher.IsExecuting)return SurfIntersectSurfCore(first,second,options,pointCount,points,curveCount,curves,intervals,kinds);
        var command=new SurfaceIntersectionCommand{First=first,Second=second,Options=options,PointCount=pointCount,Points=points,
            CurveCount=curveCount,Curves=curves,Intervals=intervals,Kinds=kinds};
        return Dispatch(ApiId.SurfIntersectSurf,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,first);
    }
    private struct SurfaceIntersectionCommand:IKernelCommand
    {
        internal SurfTag First,Second;internal PK_SURF_intersect_surf_o_s* Options;
        internal BufferCount* PointCount;internal PK_VECTOR_s** Points;internal BufferCount* CurveCount;internal CurveTag** Curves;
        internal PK_INTERVAL_s** Intervals;internal PK_intersect_curve_t** Kinds;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try{return SurfIntersectSurfCore(First,Second,Options,PointCount,Points,CurveCount,Curves,Intervals,Kinds);}
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    private struct IntersectionCurveSpan
    {
        internal CurveSlot Slot;
        internal double Offset,Scale,Lower,Upper;
    }

    private static int ResolveIntersectionCurve(CurveTag tag,PK_INTERVAL_s interval,out IntersectionCurveSpan span)
    {
        span=default;span.Scale=1;span.Slot=GetCurveSlotByTag(tag);
        if(span.Slot<0)return ParasolidConstants.PK_ERROR_bad_class;
        var a=interval.value[0];var b=interval.value[1];
        if(!double.IsFinite(a)||!double.IsFinite(b)||b<a)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(a==b)return ParasolidConstants.PK_ERROR_curve_too_short;
        for(var depth=0;Curves[span.Slot].Class==CurveClass.TRCurve;depth++)
        {
            if(depth>=16)return ParasolidConstants.PK_ERROR_not_implemented;
            ref readonly var trim=ref TrCurveDataPool[Curves[span.Slot].DataIndex];
            var sense=trim.Parm2>=trim.Parm1?1:-1;
            span.Offset=trim.Parm1+sense*span.Offset;span.Scale*=sense;
            span.Slot=GetCurveSlotByTag(trim.BasisCurveTag);if(span.Slot<0)return ParasolidConstants.PK_ERROR_bad_class;
        }
        span.Lower=Math.Min(span.Offset+span.Scale*a,span.Offset+span.Scale*b);
        span.Upper=Math.Max(span.Offset+span.Scale*a,span.Offset+span.Scale*b);
        var kind=Curves[span.Slot].Class;
        if(kind is CurveClass.Circle or CurveClass.Ellipse&&span.Upper-span.Lower>Math.Tau+1e-12)return ParasolidConstants.PK_ERROR_bad_interval;
        if(kind==CurveClass.BCurve)
        {
            var view=GetBCurveView(BCurveDataStore[Curves[span.Slot].DataIndex]);
            if(span.Lower<view.Start||span.Upper>view.End)return ParasolidConstants.PK_ERROR_not_implemented;
        }
        return 0;
    }

    private static int ValidateIntersectionBox(KernelLogical haveBox,in PK_BOX_s box)
    {
        if(haveBox>1)return ParasolidConstants.PK_ERROR_bad_parameter;if(haveBox==0)return 0;
        for(var i=0;i<3;i++)if(!double.IsFinite(box.coord[i])||!double.IsFinite(box.coord[i+3])||box.coord[i]>box.coord[i+3])return ParasolidConstants.PK_ERROR_bad_box;
        return 0;
    }
    private static int IntersectionError(AlgorithmStatus status)=>status switch
    {AlgorithmStatus.Success=>0,AlgorithmStatus.Unsupported=>ParasolidConstants.PK_ERROR_not_implemented,
        AlgorithmStatus.InvalidInput=>ParasolidConstants.PK_ERROR_bad_parameter,_=>ParasolidConstants.PK_ERROR_cant_do_intersect};

    private static int CurveIntersectCurveCore(CurveTag first,PK_INTERVAL_s firstInterval,CurveTag second,PK_INTERVAL_s secondInterval,
        PK_CURVE_intersect_curve_o_s* options,BufferCount* count,PK_VECTOR_s** points,double** firstParameters,double** secondParameters,int** kinds)
    {
        if(count!=null)*count=0;if(points!=null)*points=null;if(firstParameters!=null)*firstParameters=null;if(secondParameters!=null)*secondParameters=null;if(kinds!=null)*kinds=null;
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(options==null||count==null||points==null||firstParameters==null||secondParameters==null||kinds==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=1)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        var error=ValidateIntersectionBox(options->have_box,options->box);if(error!=0)return error;
        if(options->common_surf!=0&&GetSurfaceSlotByTag(options->common_surf)<0)return ParasolidConstants.PK_ERROR_bad_class;
        error=ResolveIntersectionCurve(first,firstInterval,out var a);if(error!=0)return error;
        error=ResolveIntersectionCurve(second,secondInterval,out var b);if(error!=0)return error;
        if(Curves[a.Slot].Header.Partition!=Curves[b.Slot].Header.Partition)return ParasolidConstants.PK_ERROR_not_in_same_partition;
        Span<OperationIntersectionPoint> found=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        var status=IntersectCurveSpans(a,b,found,out var n);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        for(var i=0;i<n;i++)
        {
            found[i].First=(found[i].First-a.Offset)/a.Scale;found[i].Second=(found[i].Second-b.Offset)/b.Scale;
            if(a.Scale<0)found[i].Kind=ReverseIntersectionKind(found[i].Kind);
            // PK_CURVE_intersect_curve reports isolated curve contacts as
            // simple, including tangent contacts and a shared coincidence
            // endpoint. The tangent token is used by surface/curve results.
            if(found[i].Kind==IntersectionVectorKind.Tangent)found[i].Kind=IntersectionVectorKind.Simple;
        }
        OperationIntersection.Sort(found[..n]);
        return PublishPointIntersections(found[..n],false,count,points,null,firstParameters,secondParameters,kinds);
    }

    private static IntersectionVectorKind ReverseIntersectionKind(IntersectionVectorKind kind)=>kind switch
    {IntersectionVectorKind.Start=>IntersectionVectorKind.End,IntersectionVectorKind.End=>IntersectionVectorKind.Start,_=>kind};

    private static AlgorithmStatus IntersectCurveSpans(in IntersectionCurveSpan a,in IntersectionCurveSpan b,
        Span<OperationIntersectionPoint> found,out BufferCount count)
    {
        count=0;ref readonly var ca=ref Curves[a.Slot];ref readonly var cb=ref Curves[b.Slot];
        if(ca.Class==CurveClass.Line)
        {
            GetIntersectionLine(ca,out var p,out var d);
            if(cb.Class==CurveClass.Line){GetIntersectionLine(cb,out var q,out var e);return OperationIntersection.LineLine(p,d,a.Lower,a.Upper,q,e,b.Lower,b.Upper,found,out count);}
            if(TryGetIntersectionConic(cb,out var conic))return OperationIntersection.LineConic(p,d,a.Lower,a.Upper,conic,b.Lower,b.Upper,found,out count);
            if(cb.Class==CurveClass.BCurve)return IntersectLineBCurve(p,d,a.Lower,a.Upper,cb,b.Lower,b.Upper,found,out count);
        }
        if(cb.Class==CurveClass.Line)
        {
            var status=IntersectCurveSpans(b,a,found,out count);
            for(var i=0;i<count;i++)(found[i].First,found[i].Second)=(found[i].Second,found[i].First);
            OperationIntersection.Sort(found[..count]);
            if(count==2&&found[0].Kind==IntersectionVectorKind.End&&found[1].Kind==IntersectionVectorKind.Start)
            {found[0].Kind=IntersectionVectorKind.Start;found[1].Kind=IntersectionVectorKind.End;}
            return status;
        }
        if(TryGetIntersectionConic(ca,out var first)&&TryGetIntersectionConic(cb,out var second))
            return IntersectConics(first,a.Lower,a.Upper,second,b.Lower,b.Upper,found,out count);
        if(a.Slot==b.Slot&&ca.Class==CurveClass.BCurve)
        {
            var lo=Math.Max(a.Lower,b.Lower);var hi=Math.Min(a.Upper,b.Upper);if(hi<lo)return AlgorithmStatus.Success;
            Span<KernelVector3> jet=stackalloc KernelVector3[1];
            var status=EvaluateCurveCore(in ca,lo,0,false,jet,out _);if(status!=AlgorithmStatus.Success)return status;
            if(!OperationIntersection.AddPoint(found,ref count,jet[0],lo,lo,hi==lo?IntersectionVectorKind.Tangent:IntersectionVectorKind.Start))return AlgorithmStatus.OutputTooSmall;
            if(hi>lo)
            {status=EvaluateCurveCore(in ca,hi,0,false,jet,out _);if(status!=AlgorithmStatus.Success)return status;if(!OperationIntersection.AddPoint(found,ref count,jet[0],hi,hi,IntersectionVectorKind.End))return AlgorithmStatus.OutputTooSmall;}
            return AlgorithmStatus.Success;
        }
        return AlgorithmStatus.Unsupported;
    }

    private static void GetIntersectionLine(in CurveRecord curve,out KernelVector3 p,out KernelVector3 d)
    {ref readonly var line=ref LineDataPool[curve.DataIndex];p=Vector(line.LocationX,line.LocationY,line.LocationZ);d=Vector(line.AxisX,line.AxisY,line.AxisZ);}
    private static bool TryGetIntersectionConic(in CurveRecord curve,out OperationIntersectionBranch result)
    {
        result=default;
        if(curve.Class==CurveClass.Circle)
        {
            ref readonly var c=ref CircleDataPool[curve.DataIndex];result=OperationIntersection.Circle(Vector(c.CenterX,c.CenterY,c.CenterZ),Vector(c.AxisX,c.AxisY,c.AxisZ),Vector(c.RefDirX,c.RefDirY,c.RefDirZ),c.Radius);return true;
        }
        if(curve.Class==CurveClass.Ellipse)
        {
            ref readonly var c=ref EllipseDataPool[curve.DataIndex];result=OperationIntersection.Circle(Vector(c.CenterX,c.CenterY,c.CenterZ),Vector(c.AxisX,c.AxisY,c.AxisZ),Vector(c.RefDirX,c.RefDirY,c.RefDirZ),c.R1);result.Class=CurveClass.Ellipse;result.Minor=c.R2;return true;
        }
        return false;
    }

    private static AlgorithmStatus IntersectLineBCurve(KernelVector3 p,KernelVector3 d,double lo,double hi,
        in CurveRecord curve,double a,double b,Span<OperationIntersectionPoint> found,out BufferCount count)
    {
        count=0;var normal=OperationIntersection.Reference(d);var plane=new AnalyticSurface(SurfaceClass.Plane,p,normal,d);
        var view=GetBCurveView(BCurveDataStore[curve.DataIndex]);
        var status=BCurvePlaneIntersection.Find(view,plane,a,b,found,out var n);if(status!=AlgorithmStatus.Success)return status;
        var coincident=false;for(var i=0;i<n;i++)if(found[i].Kind is IntersectionVectorKind.Start or IntersectionVectorKind.End)coincident=true;
        if(coincident)
        {
            plane=new AnalyticSurface(SurfaceClass.Plane,p,EvaluationMath.Cross(d,normal),d);
            status=BCurvePlaneIntersection.Find(view,plane,a,b,found,out n);if(status!=AlgorithmStatus.Success)return status;
            for(var i=0;i<n;i++)if(found[i].Kind is IntersectionVectorKind.Start or IntersectionVectorKind.End)return AlgorithmStatus.Unsupported;
        }
        Span<KernelVector3> jet=stackalloc KernelVector3[2];
        for(var i=0;i<n;i++)
        {
            var point=found[i];var t=Dot(Sub(point.Point,p),d);
            if(!OperationIntersection.InInterval(t,lo,hi)||OperationIntersection.Norm(EvaluationMath.Cross(Sub(point.Point,p),d))>OperationIntersection.DistanceTolerance)continue;
            status=EvaluateCurveCore(in curve,point.First,1,false,jet,out _);if(status!=AlgorithmStatus.Success)return status;
            point.Second=point.First;point.First=Math.Clamp(t,lo,hi);
            point.Kind=OperationIntersection.Norm(EvaluationMath.Cross(Unit(jet[1]),d))<=1e-8?IntersectionVectorKind.Tangent:IntersectionVectorKind.Simple;
            found[count++]=point;
        }
        OperationIntersection.Sort(found[..count]);return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntersectConics(in OperationIntersectionBranch a,double al,double ah,
        in OperationIntersectionBranch b,double bl,double bh,Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var parallel=OperationIntersection.Norm(EvaluationMath.Cross(a.Axis,b.Axis))<=OperationIntersection.AngularTolerance;
        var delta=Sub(b.Origin,a.Origin);
        if(parallel&&Math.Abs(Dot(delta,a.Axis))>OperationIntersection.DistanceTolerance)return AlgorithmStatus.Success;
        if(parallel&&a.Class==CurveClass.Circle&&b.Class==CurveClass.Circle)
        {
            var distance=OperationIntersection.Norm(delta);
            if(distance<=OperationIntersection.DistanceTolerance&&Math.Abs(a.Radius-b.Radius)<=OperationIntersection.DistanceTolerance)
            {
                var offset=Math.Atan2(Dot(b.Reference,EvaluationMath.Cross(a.Axis,a.Reference)),Dot(b.Reference,a.Reference));var sense=Dot(a.Axis,b.Axis)>0?1:-1;
                for(var k=-2;k<=2;k++)
                {
                    var start=Math.Min(offset+sense*bl,offset+sense*bh)+k*Math.Tau;var end=Math.Max(offset+sense*bl,offset+sense*bh)+k*Math.Tau;
                    var lo=Math.Max(al,start);var hi=Math.Min(ah,end);if(hi<lo-1e-12)continue;
                    if(hi-lo<=1e-12){if(!OperationIntersection.AddPoint(output,ref count,OperationIntersection.ConicPoint(a,lo),lo,(lo-offset-k*Math.Tau)/sense,IntersectionVectorKind.Tangent))return AlgorithmStatus.OutputTooSmall;continue;}
                    if(!OperationIntersection.AddPoint(output,ref count,OperationIntersection.ConicPoint(a,lo),lo,(lo-offset-k*Math.Tau)/sense,IntersectionVectorKind.Start)
                        ||!OperationIntersection.AddPoint(output,ref count,OperationIntersection.ConicPoint(a,hi),hi,(hi-offset-k*Math.Tau)/sense,IntersectionVectorKind.End))return AlgorithmStatus.OutputTooSmall;
                }
                OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
            }
            if(distance<=OperationIntersection.DistanceTolerance||distance>a.Radius+b.Radius+OperationIntersection.DistanceTolerance||distance<Math.Abs(a.Radius-b.Radius)-OperationIntersection.DistanceTolerance)return AlgorithmStatus.Success;
            var x=Scale(delta,1/distance);var y=EvaluationMath.Cross(a.Axis,x);var h=(distance*distance+(a.Radius-b.Radius)*(a.Radius+b.Radius))/(2*distance);
            var lateral=Math.Sqrt(Math.Max(0,(a.Radius-h)*(a.Radius+h)));var center=Add(a.Origin,Scale(x,h));
            for(var i=0;i<(lateral<=OperationIntersection.DistanceTolerance?1:2);i++)
            {
                var point=Add(center,Scale(y,i==0?lateral:-lateral));
                if(!ConicParameter(a,point,al,ah,out var t)||!ConicParameter(b,point,bl,bh,out var u))continue;
                if(!OperationIntersection.AddPoint(output,ref count,point,t,u,lateral<=OperationIntersection.DistanceTolerance?IntersectionVectorKind.Tangent:IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
            }
            OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
        }
        if(!parallel)
        {
            var plane=new AnalyticSurface(SurfaceClass.Plane,b.Origin,b.Axis,b.Reference);
            var status=OperationIntersection.PlaneConic(plane,a,al,ah,output,out var n);if(status!=AlgorithmStatus.Success)return status;
            for(var i=0;i<n;i++)
            {var item=output[i];if(!ConicParameter(b,item.Point,bl,bh,out item.Second))continue;item.Kind=IntersectionVectorKind.Simple;output[count++]=item;}
            return AlgorithmStatus.Success;
        }
        // A circular support gives a cylinder implicit even when the other
        // curve is an ellipse. Two general ellipses need a different quadric.
        if(b.Class==CurveClass.Circle)
        {
            var cylinder=new AnalyticSurface(SurfaceClass.Cylinder,b.Origin,b.Axis,b.Reference,b.Radius);
            var status=OperationIntersection.AnalyticConic(cylinder,a,al,ah,output,out var n);if(status!=AlgorithmStatus.Success)return status;
            for(var i=0;i<n;i++){var item=output[i];if(ConicParameter(b,item.Point,bl,bh,out item.Second))output[count++]=item;}
            return AlgorithmStatus.Success;
        }
        if(a.Class==CurveClass.Circle)
        {
            var status=IntersectConics(b,bl,bh,a,al,ah,output,out count);
            for(var i=0;i<count;i++)(output[i].First,output[i].Second)=(output[i].Second,output[i].First);
            OperationIntersection.Sort(output[..count]);return status;
        }
        return AlgorithmStatus.Unsupported;
    }
    private static bool ConicParameter(in OperationIntersectionBranch conic,KernelVector3 point,double lo,double hi,out double parameter)
    {
        parameter=0;var r=Sub(point,conic.Origin);var x=Dot(r,conic.Reference)/conic.Radius;var y=Dot(r,EvaluationMath.Cross(conic.Axis,conic.Reference))/conic.Minor;
        if(Math.Abs(Dot(r,conic.Axis))>OperationIntersection.DistanceTolerance||Math.Abs(x*x+y*y-1)>OperationIntersection.DistanceTolerance/Math.Max(OperationIntersection.DistanceTolerance,Math.Min(conic.Radius,conic.Minor)))return false;
        return OperationIntersection.PeriodicParameter(Math.Atan2(y,x),lo,hi,out parameter);
    }

    private static int SurfIntersectCurveCore(SurfTag surface,CurveTag curve,PK_INTERVAL_s interval,PK_SURF_intersect_curve_o_s* options,
        BufferCount* count,PK_VECTOR_s** points,PK_UV_s** parameters,double** curveParameters,int** kinds)
    {
        if(count!=null)*count=0;if(points!=null)*points=null;if(parameters!=null)*parameters=null;if(curveParameters!=null)*curveParameters=null;if(kinds!=null)*kinds=null;
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(options==null||count==null||points==null||parameters==null||curveParameters==null||kinds==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=1)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        var error=ValidateIntersectionBox(options->have_box,options->box);if(error!=0)return error;
        var surfaceSlot=GetSurfaceSlotByTag(surface);if(surfaceSlot<0)return ParasolidConstants.PK_ERROR_bad_class;
        error=ResolveIntersectionCurve(curve,interval,out var span);if(error!=0)return error;
        if(Surfaces[surfaceSlot].Header.Partition!=Curves[span.Slot].Header.Partition)return ParasolidConstants.PK_ERROR_not_in_same_partition;
        if(!TryPrepareSurface(Surfaces[surfaceSlot],out var prepared))return ParasolidConstants.PK_ERROR_not_implemented;
        Span<OperationIntersectionPoint> found=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        var status=IntersectSurfaceCurve(prepared,span,found,out var n);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        for(var i=0;i<n;i++)
        {
            found[i].First=(found[i].First-span.Offset)/span.Scale;if(span.Scale<0)found[i].Kind=ReverseIntersectionKind(found[i].Kind);
            if(found[i].Kind is IntersectionVectorKind.Start or IntersectionVectorKind.End){found[i].U=-1;found[i].V=-1;}
            else if(OperationIntersection.Parameters(prepared,found[i].Point,out found[i].U,out found[i].V)!=AlgorithmStatus.Success)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        }
        OperationIntersection.Sort(found[..n]);
        return PublishPointIntersections(found[..n],true,count,points,parameters,curveParameters,null,kinds);
    }

    private static AlgorithmStatus IntersectSurfaceCurve(in AnalyticSurface surface,in IntersectionCurveSpan span,
        Span<OperationIntersectionPoint> found,out BufferCount count)
    {
        ref readonly var curve=ref Curves[span.Slot];count=0;
        if(curve.Class==CurveClass.Line){GetIntersectionLine(curve,out var p,out var d);return OperationIntersection.AnalyticLine(surface,p,d,span.Lower,span.Upper,found,out count);}
        if(TryGetIntersectionConic(curve,out var conic))return OperationIntersection.AnalyticConic(surface,conic,span.Lower,span.Upper,found,out count);
        if(curve.Class==CurveClass.BCurve)
            return BCurveAnalyticIntersection.Find(GetBCurveView(BCurveDataStore[curve.DataIndex]),surface,span.Lower,span.Upper,found,out count);
        return AlgorithmStatus.Unsupported;
    }

    private static int PublishPointIntersections(ReadOnlySpan<OperationIntersectionPoint> found,bool surface,
        BufferCount* count,PK_VECTOR_s** points,PK_UV_s** uv,double** first,double** second,int** kinds)
    {
        if(found.IsEmpty)return 0;var n=found.Length;
        var vectors=(PK_VECTOR_s*)State.Session->Returns.TryAllocate((nuint)n*(nuint)sizeof(PK_VECTOR_s));
        var ts=(double*)State.Session->Returns.TryAllocate((nuint)n*sizeof(double));
        var parameters=surface?State.Session->Returns.TryAllocate((nuint)n*(nuint)sizeof(PK_UV_s)):State.Session->Returns.TryAllocate((nuint)n*sizeof(double));
        var types=(int*)State.Session->Returns.TryAllocate((nuint)n*sizeof(int));
        if(vectors==null||ts==null||parameters==null||types==null)
        {State.Session->Returns.TryFree(vectors);State.Session->Returns.TryFree(ts);State.Session->Returns.TryFree(parameters);State.Session->Returns.TryFree(types);return ParasolidConstants.PK_ERROR_memory_full;}
        for(var i=0;i<n;i++)
        {
            WriteVector(vectors+i,found[i].Point);ts[i]=found[i].First;types[i]=(int)found[i].Kind;
            if(surface){((PK_UV_s*)parameters)[i].param[0]=found[i].U;((PK_UV_s*)parameters)[i].param[1]=found[i].V;}
            else ((double*)parameters)[i]=found[i].Second;
        }
        *points=vectors;*first=ts;*kinds=types;if(surface)*uv=(PK_UV_s*)parameters;else *second=(double*)parameters;*count=n;
        return 0;
    }

    private static int SurfIntersectSurfCore(SurfTag first,SurfTag second,PK_SURF_intersect_surf_o_s* options,
        BufferCount* pointCount,PK_VECTOR_s** points,BufferCount* curveCount,CurveTag** curves,PK_INTERVAL_s** intervals,int** kinds)
    {
        if(pointCount!=null)*pointCount=0;if(points!=null)*points=null;if(curveCount!=null)*curveCount=0;if(curves!=null)*curves=null;if(intervals!=null)*intervals=null;if(kinds!=null)*kinds=null;
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(options==null||pointCount==null||points==null||curveCount==null||curves==null||intervals==null||kinds==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=3)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        var error=ValidateIntersectionBox(options->have_box,options->box);if(error!=0)return error;
        if(options->have_uvbox_1>1||options->have_uvbox_2>1||options->have_vector>1||!double.IsFinite(options->tolerance)||options->tolerance<=0
            ||options->mixed_curve_category<ParasolidConstants.PK_mixed_intersection_pline_c||options->mixed_curve_category>ParasolidConstants.PK_mixed_intersection_both_c)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        var sa=GetSurfaceSlotByTag(first);var sb=GetSurfaceSlotByTag(second);if(sa<0||sb<0)return ParasolidConstants.PK_ERROR_bad_class;
        if(Surfaces[sa].Header.Partition!=CurrentPartition||Surfaces[sb].Header.Partition!=CurrentPartition)return ParasolidConstants.PK_ERROR_not_in_same_partition;
        error=IntersectionGeometryBody(first,sa,out var ba);if(error!=0)return error;
        error=IntersectionGeometryBody(second,sb,out var bb);if(error!=0)return error;
        if(ba!=bb)return ParasolidConstants.PK_ERROR_not_in_same_body;
        var analyticA=TryPrepareSurface(Surfaces[sa],out var a);var analyticB=TryPrepareSurface(Surfaces[sb],out var b);
        Span<OperationIntersectionBranch> branches=stackalloc OperationIntersectionBranch[64];Span<KernelVector3> isolated=stackalloc KernelVector3[64];
        AlgorithmStatus status;BufferCount nc,np;
        if(analyticA&&analyticB)status=OperationIntersection.Surfaces(a,b,branches,out nc,isolated,out np);
        else if(analyticA&&a.Kind==SurfaceClass.Plane&&Surfaces[sb].Class==SurfaceClass.BSurface)
            status=RuledBSurfacePlaneIntersection.Find(GetBSurfaceView(BSurfaceDataStore[Surfaces[sb].DataIndex]),a,branches,out nc,isolated,out np);
        else if(analyticB&&b.Kind==SurfaceClass.Plane&&Surfaces[sa].Class==SurfaceClass.BSurface)
            status=RuledBSurfacePlaneIntersection.Find(GetBSurfaceView(BSurfaceDataStore[Surfaces[sa].DataIndex]),b,branches,out nc,isolated,out np);
        else return ParasolidConstants.PK_ERROR_not_implemented;
        if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        if(options->have_vector!=0)
        {
            var seed=Vector(options->vector.coord[0],options->vector.coord[1],options->vector.coord[2]);if(!IsFinite(seed))return ParasolidConstants.PK_ERROR_bad_parameter;
            var selected=-1;var best=double.PositiveInfinity;
            for(var i=0;i<nc;i++)
            {
                if(branches[i].Class==CurveClass.BCurve)return ParasolidConstants.PK_ERROR_not_implemented;
                var distance=IntersectionBranchDistance(branches[i],seed);if(distance<best){best=distance;selected=i;}
            }
            if(selected<0||best>OperationIntersection.DistanceTolerance)return ParasolidConstants.PK_ERROR_not_on_curve;
            branches[0]=branches[selected];nc=1;np=0;
        }
        var vectors=np>0?(PK_VECTOR_s*)State.Session->Returns.TryAllocate((nuint)np*(nuint)sizeof(PK_VECTOR_s)):null;
        var tags=nc>0?(CurveTag*)State.Session->Returns.TryAllocate((nuint)nc*sizeof(CurveTag)):null;
        var ranges=nc>0?(PK_INTERVAL_s*)State.Session->Returns.TryAllocate((nuint)nc*(nuint)sizeof(PK_INTERVAL_s)):null;
        var types=nc>0?(int*)State.Session->Returns.TryAllocate((nuint)nc*sizeof(int)):null;
        if(np>0&&vectors==null||nc>0&&(tags==null||ranges==null||types==null))error=ParasolidConstants.PK_ERROR_memory_full;
        else
        {
            for(var i=0;i<np;i++)WriteVector(vectors+i,isolated[i]);
            for(var i=0;i<nc;i++)
            {
                tags[i]=0;error=CreateIntersectionBranch(branches[i],tags+i);if(error!=0)break;
                if(ba>=0){error=TryRegisterConstructionGeometry(ba,tags[i]);if(error!=0)break;}
                ranges[i].value[0]=branches[i].Start;ranges[i].value[1]=branches[i].End;types[i]=(int)branches[i].Kind;
            }
        }
        if(error!=0)
        {State.Session->Returns.TryFree(vectors);State.Session->Returns.TryFree(tags);State.Session->Returns.TryFree(ranges);State.Session->Returns.TryFree(types);return error;}
        *points=vectors;*pointCount=np;*curves=tags;*intervals=ranges;*kinds=types;*curveCount=nc;return 0;
    }

    private static int IntersectionGeometryBody(SurfTag tag,SurfaceSlot slot,out BodySlot body)
    {
        body=GeometryOwnerCount(PoolKind.Surface,slot)!=0?GeometryBody(PoolKind.Surface,slot):GetConstructionBody(tag);
        if(body>=0||Surfaces[slot].GeometricOwnerCount==0)return 0;
        // A support of an attached offset/swept/spun surface belongs to the
        // same part even though it has no direct face owner of its own.
        Span<GeomTag> component=stackalloc GeomTag[512];
        return CollectGeometryPart(tag,component,out _,out body);
    }

    private static int CreateIntersectionBranch(in OperationIntersectionBranch branch,CurveTag* tag)
    {
        if(branch.Class==CurveClass.BCurve)
        {
            Span<double> poles=stackalloc double[68];
            for(var i=0;i<4*(branch.Degree+1);i++)poles[i]=branch.Poles[i];
            double* knots=stackalloc double[2]{branch.Start,branch.End};int* mults=stackalloc int[2]{branch.Degree+1,branch.Degree+1};
            fixed(double* vertices=poles)
            {
                var definition=new PK_BCURVE_sf_s{degree=branch.Degree,n_vertices=branch.Degree+1,vertex_dim=4,is_rational=1,
                    vertex=vertices,n_knots=2,knot=knots,knot_mult=mults,form=ParasolidConstants.PK_BCURVE_form_arbitrary_c,
                    knot_type=ParasolidConstants.PK_knot_bezier_ends_c,self_intersecting=ParasolidConstants.PK_self_intersect_unset_c};
                return BCurveCreate(&definition,tag);
            }
        }
        if(branch.Class==CurveClass.Line)
        {
            var sf=new PK_LINE_sf_s();WriteVector(&sf.basis_set.location,branch.Origin);
            sf.basis_set.axis.coord[0]=branch.Axis.X;sf.basis_set.axis.coord[1]=branch.Axis.Y;sf.basis_set.axis.coord[2]=branch.Axis.Z;
            return LineCreate(&sf,tag);
        }
        var basis=new PK_AXIS2_sf_s();WriteVector(&basis.location,branch.Origin);
        basis.axis.coord[0]=branch.Axis.X;basis.axis.coord[1]=branch.Axis.Y;basis.axis.coord[2]=branch.Axis.Z;
        basis.ref_direction.coord[0]=branch.Reference.X;basis.ref_direction.coord[1]=branch.Reference.Y;basis.ref_direction.coord[2]=branch.Reference.Z;
        if(branch.Class==CurveClass.Circle){var sf=new PK_CIRCLE_sf_s{basis_set=basis,radius=branch.Radius};return CircleCreate(&sf,tag);}
        var ellipse=new PK_ELLIPSE_sf_s{basis_set=basis,R1=branch.Radius,R2=branch.Minor};return EllipseCreate(&ellipse,tag);
    }

    private static double IntersectionBranchDistance(in OperationIntersectionBranch branch,KernelVector3 point)
    {
        var r=Sub(point,branch.Origin);if(branch.Class==CurveClass.Line)return OperationIntersection.Norm(EvaluationMath.Cross(r,branch.Axis));
        var t=Math.Atan2(Dot(r,EvaluationMath.Cross(branch.Axis,branch.Reference))/branch.Minor,Dot(r,branch.Reference)/branch.Radius);
        return OperationIntersection.Norm(Sub(point,OperationIntersection.ConicPoint(branch,t)));
    }

    /// <summary>Independent parameters on both supports; no assumption that the two charts share a parameterization.</summary>
    internal static AlgorithmStatus TryIntersectionPairedParameters(SurfTag first,SurfTag second,in KernelVector3 point,
        out PK_UV_s firstUv,out PK_UV_s secondUv)
    {
        firstUv=default;secondUv=default;var sa=GetSurfaceSlotByTag(first);var sb=GetSurfaceSlotByTag(second);
        if(sa<0||sb<0)return AlgorithmStatus.InvalidInput;
        var owns=EnsureCommandScratch();
        try
        {
            var status=IntersectionSurfaceParameters(Surfaces[sa],point,out firstUv);
            return status==AlgorithmStatus.Success?IntersectionSurfaceParameters(Surfaces[sb],point,out secondUv):status;
        }
        finally{if(owns)ReleaseCommandScratch(State.Session);}
    }

    private static AlgorithmStatus IntersectionSurfaceParameters(in SurfaceRecord record,in KernelVector3 point,out PK_UV_s uv)
    {
        uv=default;
        if(TryPrepareSurface(record,out var surface))
        {
            if(AnalyticImplicitEvaluation.GeometricDeviation(surface,point,out var error)!=AlgorithmStatus.Success||error>OperationIntersection.DistanceTolerance)return AlgorithmStatus.InvalidInput;
            var status=OperationIntersection.Parameters(surface,point,out var u,out var v);uv.param[0]=u;uv.param[1]=v;return status;
        }
        if(record.Class!=SurfaceClass.BSurface)return AlgorithmStatus.Unsupported;
        // Inversion only accepts a checked point on the bounded patch. Failure
        // to find a parameter is explicit; this routine does not discover or
        // certify branches and therefore cannot turn a missing seed into empty.
        var view=GetBSurfaceView(BSurfaceDataStore[record.DataIndex]);
        SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[4];
        for(var seed=0;seed<25;seed++)
        {
            var u=view.UStart+(view.UEnd-view.UStart)*(seed%5)/4;var v=view.VStart+(view.VEnd-view.VStart)*(seed/5)/4;
            for(var iteration=0;iteration<35;iteration++)
            {
                var status=EvaluateSurfaceCore(in record,u,v,layout,jet,0);if(status!=AlgorithmStatus.Success)return status;
                var r=Sub(point,jet[0]);var error=OperationIntersection.Norm(r);
                if(error<=OperationIntersection.DistanceTolerance){uv.param[0]=u;uv.param[1]=v;return AlgorithmStatus.Success;}
                var du=jet[layout.GetIndex(1,0)];var dv=jet[layout.GetIndex(0,1)];var aa=Dot(du,du);var ab=Dot(du,dv);var bb=Dot(dv,dv);var determinant=aa*bb-ab*ab;
                if(!(determinant>1e-28*aa*bb))break;
                var ru=Dot(r,du);var rv=Dot(r,dv);var su=(ru*bb-rv*ab)/determinant;var sv=(rv*aa-ru*ab)/determinant;
                var nextU=Math.Clamp(u+su,view.UStart,view.UEnd);var nextV=Math.Clamp(v+sv,view.VStart,view.VEnd);
                if(nextU==u&&nextV==v)break;u=nextU;v=nextV;
            }
        }
        return AlgorithmStatus.NotConverged;
    }

    internal static void UnwrapIntersectionParameters(SurfTag surface,in PK_UV_s previous,ref PK_UV_s current)
    {
        var slot=GetSurfaceSlotByTag(surface);if(slot<0)return;
        if(Surfaces[slot].Class is SurfaceClass.Cylinder or SurfaceClass.Cone or SurfaceClass.Sphere or SurfaceClass.Torus)
            current.param[0]+=Math.Tau*Math.Round((previous.param[0]-current.param[0])/Math.Tau);
        if(Surfaces[slot].Class==SurfaceClass.Torus)current.param[1]+=Math.Tau*Math.Round((previous.param[1]-current.param[1])/Math.Tau);
    }
}

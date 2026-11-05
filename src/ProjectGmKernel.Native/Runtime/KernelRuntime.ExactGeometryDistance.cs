using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus ExactGeometryDistance(in DistanceOperand a,in DistanceOperand b,
        out PK_range_end_s first,out PK_range_end_s second,out double distance)
    {
        first=second=default;distance=0;KernelVector3 pa,pb;var region=false;
        if(a.Pool==PoolKind.Curve&&b.Pool==PoolKind.Curve&&!a.Bounded&&!b.Bounded
            &&Curves[a.Curve.Slot].Class==CurveClass.Line&&Curves[b.Curve.Slot].Class==CurveClass.Line)
        {
            GetIntersectionLine(Curves[a.Curve.Slot],out var p,out var d);GetIntersectionLine(Curves[b.Curve.Slot],out var q,out var e);
            var cross=ProfileCross(d,e);var det=Dot(cross,cross);var offset=Sub(p,q);double s,t;
            if(det==0){s=0;t=Dot(offset,e)/Dot(e,e);region=true;}
            else{s=-Dot(ProfileCross(offset,e),cross)/det;t=-Dot(ProfileCross(offset,d),cross)/det;}
            if(!double.IsFinite(s)||!double.IsFinite(t))return AlgorithmStatus.NumericalFailure;
            pa=Add(p,Scale(d,s));pb=Add(q,Scale(e,t));
            first.entity=a.Tag;second.entity=b.Tag;
            first.vector.coord[0]=pa.X;first.vector.coord[1]=pa.Y;first.vector.coord[2]=pa.Z;
            second.vector.coord[0]=pb.X;second.vector.coord[1]=pb.Y;second.vector.coord[2]=pb.Z;
            first.parameters[0]=s;second.parameters[0]=t;first.region=second.region=region?(byte)1:(byte)0;
            distance=OperationIntersection.Norm(Sub(pa,pb));return double.IsFinite(distance)?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
        }
        if(!a.IsAnalytic||!b.IsAnalytic)return AlgorithmStatus.Unsupported;
        var x=a.Analytic;var y=b.Analytic;
        if(x.Kind==SurfaceClass.Sphere&&y.Kind==SurfaceClass.Sphere)
        {
            var delta=Sub(y.Origin,x.Origin);var length=OperationIntersection.Norm(delta);var normal=length>0?Scale(delta,1/length):x.X;
            if(length==0){pa=Add(x.Origin,Scale(normal,x.Radius));pb=Add(y.Origin,Scale(normal,y.Radius));region=true;}
            else if(length>=x.Radius+y.Radius){pa=Add(x.Origin,Scale(normal,x.Radius));pb=Sub(y.Origin,Scale(normal,y.Radius));}
            else if(length<=Math.Abs(x.Radius-y.Radius))
            {
                var sign=x.Radius>=y.Radius?1:-1;pa=Add(x.Origin,Scale(normal,sign*x.Radius));pb=Add(y.Origin,Scale(normal,sign*y.Radius));
            }
            else
            {
                var h=(length*length+(x.Radius-y.Radius)*(x.Radius+y.Radius))/(2*length);
                var radius=Math.Sqrt(Math.Max(0,(x.Radius-h)*(x.Radius+h)));
                pa=pb=Add(Add(x.Origin,Scale(normal,h)),Scale(OperationIntersection.Reference(normal),radius));region=true;
            }
        }
        else if(x.Kind==SurfaceClass.Plane&&y.Kind==SurfaceClass.Plane)
        {
            var cross=ProfileCross(x.Axis,y.Axis);var square=Dot(cross,cross);var offset=Dot(Sub(x.Origin,y.Origin),y.Axis);
            if(square==0){pa=x.Origin;pb=Sub(pa,Scale(y.Axis,offset/Dot(y.Axis,y.Axis)));}
            else
            {
                var direction=ProfileCross(cross,x.Axis);var denominator=Dot(direction,y.Axis);
                pa=pb=Sub(x.Origin,Scale(direction,offset/denominator));
            }
            region=true;
        }
        else return AlgorithmStatus.Unsupported;
        if(!IsFinite(pa)||!IsFinite(pb))return AlgorithmStatus.NumericalFailure;
        var error=DistanceGeometryEnd(a,pa,0,0,out first);if(error!=0)return AlgorithmStatus.NumericalFailure;
        error=DistanceGeometryEnd(b,pb,0,0,out second);if(error!=0)return AlgorithmStatus.NumericalFailure;
        first.region=second.region=region?(byte)1:(byte)0;distance=OperationIntersection.Norm(Sub(pa,pb));return AlgorithmStatus.Success;
    }
}

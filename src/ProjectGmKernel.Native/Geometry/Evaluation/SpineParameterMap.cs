using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>The third equation fixes a native station on a rolling branch.
/// CHART planes retain their original map. Explicit curves use their exact
/// position and derivative, never a piecewise chord parameterisation.</summary>
internal static class SpineParameterMap
{
    internal static bool Explicit(in BlendSpineInstruction spine)=>spine.Curve.Kind!=CurveClass.None;
    internal static bool Periodic(in BlendSpineInstruction spine)=>spine.Curve.Kind is CurveClass.Circle or CurveClass.Ellipse
        &&spine.StartType==LimitType.Help&&spine.EndType==LimitType.Help;

    // Coefficient identities cover the complete conic, including periodic
    // lifts. Unlike point tests they also reject a wrong-sized same-topology
    // spine between any two seed stations.
    internal static AlgorithmStatus Certify(in ParametricSpineCurve curve,in AnalyticSurface surface,double first,double last)
    {
        var origin=new IntervalVector(curve.Origin)-new IntervalVector(surface.Origin);
        var a=new IntervalVector(curve.First);var b=new IntervalVector(curve.Second);var axis=new IntervalVector(surface.Axis);
        var line=curve.Kind==CurveClass.Line;var t=new IntervalNumber(first,last);
        var tolerance=Geometry.Intersection.OperationIntersection.DistanceTolerance;
        if(surface.Kind==SurfaceClass.Plane)
        {
            var constant=IntervalVector.Dot(origin,axis).Value;var cosine=IntervalVector.Dot(a,axis).Value;var sine=IntervalVector.Dot(b,axis).Value;
            var error=line?(constant+cosine*t).Magnitude:constant.Magnitude+cosine.Magnitude+sine.Magnitude;
            return error<=tolerance?AlgorithmStatus.Success:AlgorithmStatus.InvalidInput;
        }
        if(surface.Kind==SurfaceClass.Cylinder)
        {
            origin-=axis*IntervalVector.Dot(origin,axis);a-=axis*IntervalVector.Dot(a,axis);b-=axis*IntervalVector.Dot(b,axis);
        }
        else if(surface.Kind!=SurfaceClass.Sphere)return AlgorithmStatus.Unsupported;
        var oo=IntervalVector.Dot(origin,origin).Value;var aa=IntervalVector.Dot(a,a).Value;var bb=IntervalVector.Dot(b,b).Value;
        var oa=IntervalVector.Dot(origin,a).Value;var ob=IntervalVector.Dot(origin,b).Value;var ab=IntervalVector.Dot(a,b).Value;
        var radius=new IntervalNumber(surface.Radius);var radiusSquare=IntervalNumber.Square(radius);
        var bound=line?(oo-radiusSquare+2*oa*t+aa*IntervalNumber.Square(t)).Magnitude
            :(oo+(aa+bb)*.5-radiusSquare).Magnitude+2*oa.Magnitude+2*ob.Magnitude+((aa-bb)*.5).Magnitude+ab.Magnitude;
        return bound<=tolerance*(2*surface.Radius-tolerance)?AlgorithmStatus.Success:AlgorithmStatus.InvalidInput;
    }

    internal static AlgorithmStatus Series(in NestedBlendProgram program,in BlendSpineInstruction spine,TaylorJet t,
        out TaylorVector point,out TaylorVector tangent)
        =>spine.Curve.Kind==CurveClass.BCurve?RationalSpine.Series(program.SplineSpine(spine),t,out point,out tangent)
            :Series(spine.Curve,t,out point,out tangent);

    internal static AlgorithmStatus Interval(in NestedBlendProgram program,in BlendSpineInstruction spine,IntervalDual t,
        out IntervalVector point,out IntervalVector tangent)
        =>spine.Curve.Kind==CurveClass.BCurve?RationalSpine.Bound(program.SplineSpine(spine),t,out point,out tangent)
            :Interval(spine.Curve,t,out point,out tangent);

    internal static AlgorithmStatus Series(in ParametricSpineCurve curve,TaylorJet t,out TaylorVector point,out TaylorVector tangent)
    {
        var origin=TaylorVector.Constant(curve.Origin,t.Order);var first=TaylorVector.Constant(curve.First,t.Order);
        var second=TaylorVector.Constant(curve.Second,t.Order);
        if(curve.Kind==CurveClass.Line){point=origin+first*t;tangent=first;return AlgorithmStatus.Success;}
        point=tangent=default;
        if(curve.Kind is not (CurveClass.Circle or CurveClass.Ellipse))return AlgorithmStatus.Unsupported;
        TaylorJet.SinCos(t,out var sin,out var cos);point=origin+first*cos+second*sin;tangent=second*cos-first*sin;
        return point.Finite&&tangent.Finite?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }

    internal static AlgorithmStatus Interval(in ParametricSpineCurve curve,IntervalDual t,out IntervalVector point,out IntervalVector tangent)
    {
        var origin=new IntervalVector(curve.Origin);var first=new IntervalVector(curve.First);var second=new IntervalVector(curve.Second);
        if(curve.Kind==CurveClass.Line){point=origin+first*t;tangent=first;return AlgorithmStatus.Success;}
        point=tangent=default;
        if(curve.Kind is not (CurveClass.Circle or CurveClass.Ellipse))return AlgorithmStatus.Unsupported;
        IntervalDual.SinCos(t,out var sin,out var cos);point=origin+first*cos+second*sin;tangent=second*cos-first*sin;
        return point.Finite&&tangent.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    internal static AlgorithmStatus Scalar(in BlendSpineInstruction spine,in BlendChartInstruction chart,double t,in KernelVector3 point,
        out double value,out KernelVector3 gradient,out double parameterDerivative)
    {
        if(!Explicit(spine))
        {
            value=Dot(chart.Chord,Sub(point,chart.Position))-(t-chart.Parameter)/chart.Scale;
            gradient=chart.Chord;parameterDerivative=-1/chart.Scale;return AlgorithmStatus.Success;
        }
        value=parameterDerivative=0;gradient=default;
        var status=Series(spine.Curve,TaylorJet.Variable(t,1),out var exact,out var tangent);if(status!=AlgorithmStatus.Success)return status;
        gradient=tangent.Value;var delta=Sub(point,exact.Value);
        value=Dot(delta,gradient);parameterDerivative=delta.X*tangent.X[1]+delta.Y*tangent.Y[1]+delta.Z*tangent.Z[1]-Dot(gradient,gradient);
        return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus Series(in BlendSpineInstruction spine,in BlendChartInstruction chart,TaylorJet t,in TaylorVector point,out TaylorJet value)
    {
        if(!Explicit(spine)){value=TaylorVector.Dot(point-TaylorVector.Constant(chart.Position,t.Order),TaylorVector.Constant(chart.Chord,t.Order))-(t-chart.Parameter)/chart.Scale;return AlgorithmStatus.Success;}
        value=default;var status=Series(spine.Curve,t,out var exact,out var tangent);if(status!=AlgorithmStatus.Success)return status;
        value=TaylorVector.Dot(point-exact,tangent);return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus Interval(in BlendSpineInstruction spine,in BlendChartInstruction chart,IntervalDual t,in IntervalVector point,out IntervalDual value)
    {
        if(!Explicit(spine)){value=IntervalVector.Dot(point-new IntervalVector(chart.Position),new IntervalVector(chart.Chord))-(t-chart.Parameter)/chart.Scale;return AlgorithmStatus.Success;}
        value=default;var status=Interval(spine.Curve,t,out var exact,out var tangent);if(status!=AlgorithmStatus.Success)return status;
        value=IntervalVector.Dot(point-exact,tangent);return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus Gradient(in BlendSpineInstruction spine,in BlendChartInstruction chart,IntervalDual t,out IntervalVector gradient)
    {
        if(!Explicit(spine)){gradient=new(chart.Chord);return AlgorithmStatus.Success;}
        return Interval(spine.Curve,t,out _,out gradient);
    }
}

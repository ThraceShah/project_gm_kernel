using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Exact affine images of an explicit rolling spine on analytic
/// supports. Recognition compares complete curve coefficients, not points.</summary>
internal static class AnalyticSpineContact
{
    internal static AlgorithmStatus Curve(in ParametricSpineCurve spine,in BlendSurfaceInstruction support,double range,out ParametricSpineCurve contact)
    {
        contact=spine;if(support.IsBlend||support.IsBSpline)return AlgorithmStatus.Unsupported;
        var surface=support.Analytic;var offset=range*support.NormalSense;
        if(surface.Kind==SurfaceClass.Plane)
        {contact.Origin=Sub(spine.Origin,Scale(surface.Axis,offset));return AlgorithmStatus.Success;}
        if(surface.Kind is not (SurfaceClass.Cylinder or SurfaceClass.Sphere))return AlgorithmStatus.Unsupported;
        var radius=surface.Radius+offset;if(!(radius>0))return AlgorithmStatus.InvalidInput;
        var scale=surface.Radius/radius;
        contact.Origin=Add(surface.Origin,Map(Sub(spine.Origin,surface.Origin),surface,scale));
        contact.First=Map(spine.First,surface,scale);contact.Second=Map(spine.Second,surface,scale);
        return AlgorithmStatus.Success;
    }

    private static KernelVector3 Map(in KernelVector3 vector,in AnalyticSurface surface,double scale)
    {
        var along=surface.Kind==SurfaceClass.Cylinder?Scale(surface.Axis,Dot(vector,surface.Axis)):default;
        return Add(along,Scale(Sub(vector,along),scale));
    }

    internal static bool Equivalent(in ParametricSpineCurve first,in ParametricSpineCurve second,double tolerance,double lower=0,double upper=1)
    {
        if(first.Kind==CurveClass.Line||second.Kind==CurveClass.Line)
        {
            if(first.Kind!=second.Kind)return false;
            var square=Dot(first.First,first.First);if(!(square>0))return false;
            var delta=Sub(second.Origin,first.Origin);
            var shift=Dot(delta,first.First)/square;var scale=Dot(second.First,first.First)/square;
            return scale!=0&&double.IsFinite(lower)&&double.IsFinite(upper)
                &&Norm(Sub(delta,Scale(first.First,shift)))+Norm(Sub(second.First,Scale(first.First,scale)))*Math.Max(Math.Abs(lower),Math.Abs(upper))<=tolerance;
        }
        if(first.Kind is not (CurveClass.Circle or CurveClass.Ellipse)||second.Kind is not (CurveClass.Circle or CurveClass.Ellipse))return false;
        if(Norm(Sub(first.Origin,second.Origin))>tolerance)return false;
        var aa=Dot(first.First,first.First);var ab=Dot(first.First,first.Second);var bb=Dot(first.Second,first.Second);
        var determinant=aa*bb-ab*ab;if(!(determinant>0))return false;
        var ca=Dot(second.First,first.First);var cb=Dot(second.First,first.Second);
        var cosine=(ca*bb-cb*ab)/determinant;var sine=(cb*aa-ca*ab)/determinant;
        var phase=Math.Atan2(sine,cosine);cosine=Math.Cos(phase);sine=Math.Sin(phase);
        var position=Add(Scale(first.First,cosine),Scale(first.Second,sine));
        var tangent=Sub(Scale(first.Second,cosine),Scale(first.First,sine));
        // The sum of coefficient errors bounds every point on the conic,
        // regardless of phase, reversal, periodic lift or finite trim.
        var baseError=Norm(Sub(first.Origin,second.Origin))+Norm(Sub(position,second.First));
        return baseError+Math.Min(Norm(Sub(tangent,second.Second)),Norm(Add(tangent,second.Second)))<=tolerance;
    }

    private static double Norm(in KernelVector3 value)=>Math.Sqrt(Dot(value,value));
}

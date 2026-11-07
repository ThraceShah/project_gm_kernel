using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool TryReadExplicitSpineCurve(in CurveRecord source,out ParametricSpineCurve curve)
    {
        curve=new(){Kind=source.Class};
        switch(source.Class)
        {
            case CurveClass.Line:
                var line=LineDataPool[source.DataIndex];curve.Origin=Vector(line.LocationX,line.LocationY,line.LocationZ);curve.First=Vector(line.AxisX,line.AxisY,line.AxisZ);return true;
            case CurveClass.Circle:
                var circle=CircleDataPool[source.DataIndex];curve.Origin=Vector(circle.CenterX,circle.CenterY,circle.CenterZ);
                curve.First=Scale(Vector(circle.RefDirX,circle.RefDirY,circle.RefDirZ),circle.Radius);
                curve.Second=EvaluationMath.Cross(Vector(circle.AxisX,circle.AxisY,circle.AxisZ),curve.First);return true;
            case CurveClass.Ellipse:
                var ellipse=EllipseDataPool[source.DataIndex];curve.Origin=Vector(ellipse.CenterX,ellipse.CenterY,ellipse.CenterZ);
                curve.First=Scale(Vector(ellipse.RefDirX,ellipse.RefDirY,ellipse.RefDirZ),ellipse.R1);
                curve.Second=Scale(EvaluationMath.Cross(Vector(ellipse.AxisX,ellipse.AxisY,ellipse.AxisZ),Vector(ellipse.RefDirX,ellipse.RefDirY,ellipse.RefDirZ)),ellipse.R2);return true;
            default:return false;
        }
    }
}

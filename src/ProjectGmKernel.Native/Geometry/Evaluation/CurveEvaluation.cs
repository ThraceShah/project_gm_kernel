using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static class CurveEvaluation
{
    internal static AlgorithmStatus Evaluate(in LineData line, double t, DerivativeOrder order,
        Span<KernelVector3> output, out KernelVector3 tangent)
    {
        tangent = default;
        if (!double.IsFinite(t) || order < 0) return AlgorithmStatus.InvalidInput;
        if (order >= output.Length) return AlgorithmStatus.OutputTooSmall;
        var axis = Vector(line.AxisX, line.AxisY, line.AxisZ);
        output[..(order + 1)].Clear();
        output[0] = Add(Vector(line.LocationX, line.LocationY, line.LocationZ), Scale(axis, t));
        if (order > 0) output[1] = axis;
        tangent = Unit(axis);
        return IsFinite(output[0]) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    internal static AlgorithmStatus Evaluate(in CircleData circle, double t, DerivativeOrder order,
        Span<KernelVector3> output, out KernelVector3 tangent)
    {
        tangent = default;
        if (!double.IsFinite(t) || order < 0) return AlgorithmStatus.InvalidInput;
        if (order >= output.Length) return AlgorithmStatus.OutputTooSmall;
        var x = Vector(circle.RefDirX, circle.RefDirY, circle.RefDirZ);
        var y = Cross(Vector(circle.AxisX, circle.AxisY, circle.AxisZ), x);
        var (sin, cos) = Math.SinCos(t);
        tangent = Unit(Add(Scale(x, -sin), Scale(y, cos)));
        for (DerivativeOrder i = 0; i <= order; i++)
        {
            var (s, c) = Differentiate(sin, cos, i);
            output[i] = Scale(Add(Scale(x, c), Scale(y, s)), circle.Radius);
            if (i == 0) output[i] = Add(Vector(circle.CenterX, circle.CenterY, circle.CenterZ), output[i]);
            if (!IsFinite(output[i])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }

    /// <summary>P(t) = C + R1·cos(t)·X + R2·sin(t)·Y with the major axis along the reference direction.</summary>
    internal static AlgorithmStatus Evaluate(in EllipseData ellipse, double t, DerivativeOrder order,
        Span<KernelVector3> output, out KernelVector3 tangent)
    {
        tangent = default;
        if (!double.IsFinite(t) || order < 0) return AlgorithmStatus.InvalidInput;
        if (order >= output.Length) return AlgorithmStatus.OutputTooSmall;
        var x = Vector(ellipse.RefDirX, ellipse.RefDirY, ellipse.RefDirZ);
        var y = Cross(Vector(ellipse.AxisX, ellipse.AxisY, ellipse.AxisZ), x);
        var (sin, cos) = Math.SinCos(t);
        tangent = Unit(Add(Scale(x, -ellipse.R1 * sin), Scale(y, ellipse.R2 * cos)));
        for (DerivativeOrder i = 0; i <= order; i++)
        {
            var (s, c) = Differentiate(sin, cos, i);
            output[i] = Add(Scale(x, ellipse.R1 * c), Scale(y, ellipse.R2 * s));
            if (i == 0) output[i] = Add(Vector(ellipse.CenterX, ellipse.CenterY, ellipse.CenterZ), output[i]);
            if (!IsFinite(output[i])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Lift a failed local-implicit elimination (§10.6 Singular / scaled-D) into
/// the six-unknown joint system (spec §12.2–§12.4, task T14). Seeded from the
/// query point and an approximate spine point C(s); does not wire into Auto
/// plan selection — callers opt in after <see cref="BlendImplicitEvaluation.SolveLocalImplicit"/>
/// returns <see cref="AlgorithmStatus.Singular"/>.
/// </summary>
internal static class BlendJointLift
{
    internal const int MaxNewtonIterations = 16;

    /// <summary>
    /// Attempt joint Newton from (x, c≈C(sSeed)). On success writes the
    /// converged (x,c) into <paramref name="state"/> as
    /// (x.X,x.Y,x.Z,c.X,c.Y,c.Z).
    /// </summary>
    internal static AlgorithmStatus TryLiftFromLocalSingular(
        in AnalyticSurface supportA, in AnalyticSurface supportD,
        in AnalyticSurface outerSurface,
        in KernelVector3 planeAnchor, in KernelVector3 planeNormal,
        double radiusSq, in KernelVector3 point, double spineSeed,
        double spineRadius, Span<double> state,
        out BufferOffset iterations, out double residual)
    {
        iterations = 0;
        residual = 0;
        if (state.Length < 6) return AlgorithmStatus.WorkspaceTooSmall;
        if (!(spineRadius > 0) || !(radiusSq > 0)) return AlgorithmStatus.InvalidInput;

        var (sinS, cosS) = Math.SinCos(spineSeed);
        var c0 = Vector(spineRadius * cosS, spineRadius * sinS, 0);
        state[0] = point.X; state[1] = point.Y; state[2] = point.Z;
        state[3] = c0.X; state[4] = c0.Y; state[5] = c0.Z;

        Span<double> f = stackalloc double[6];
        Span<double> j = stackalloc double[36];
        Span<double> jCopy = stackalloc double[36];
        Span<double> step = stackalloc double[6];
        Span<double> model = stackalloc double[6];
        Span<int> pivots = stackalloc int[6];

        for (BufferOffset iter = 0; iter < MaxNewtonIterations; iter++)
        {
            iterations = iter + 1;
            var x = Vector(state[0], state[1], state[2]);
            var c = Vector(state[3], state[4], state[5]);
            JointBlendResidual.AssembleResidual(in supportA, in supportD, in outerSurface,
                in planeAnchor, in planeNormal, radiusSq, in x, in c, f);
            residual = MaxAbs(f);
            if (residual <= BlendEnvelopeSolve.ResidualTolerance)
                return AlgorithmStatus.Success;

            var jacStatus = JointBlendResidual.AssembleJacobian(in supportA, in supportD,
                in outerSurface, in planeNormal, in x, in c, j);
            if (jacStatus != AlgorithmStatus.Success) return jacStatus;

            var status = NewtonStep.ComputeStep(j, jCopy, f, 6, pivots, model, step, out var predicted);
            if (status != AlgorithmStatus.Success || !(predicted > 0))
                return status == AlgorithmStatus.Success ? AlgorithmStatus.NotConverged : status;
            for (BufferOffset i = 0; i < 6; i++)
            {
                state[i] += step[i];
                if (!double.IsFinite(state[i])) return AlgorithmStatus.NumericalFailure;
            }
        }
        residual = MaxAbs(f);
        return AlgorithmStatus.NotConverged;
    }

    private static double MaxAbs(ReadOnlySpan<double> values)
    {
        var max = 0.0;
        for (BufferOffset i = 0; i < values.Length; i++)
        {
            var a = Math.Abs(values[i]);
            if (a > max) max = a;
        }
        return max;
    }
}

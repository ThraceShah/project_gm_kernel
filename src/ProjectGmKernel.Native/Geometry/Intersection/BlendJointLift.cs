using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Opt-in recovery after local-implicit Singular / scaled-D failure (spec §10.6,
/// §12.2–§12.4, tasks T13–T14). Tries envelope Newton before the six-unknown
/// joint lift. Does not wire into Auto plan selection.
/// </summary>
internal static class BlendJointLift
{
    internal const int MaxNewtonIterations = 16;

    /// <summary>
    /// Occurrence identity for nested lift bookkeeping (§12.1): same NodeId at
    /// different parameter positions must not merge.
    /// </summary>
    internal readonly struct Occurrence
    {
        internal readonly int NodeId;
        internal readonly long ParameterBits;
        internal readonly int Instance;

        internal Occurrence(int nodeId, double parameter, int instance)
        {
            NodeId = nodeId;
            ParameterBits = BitConverter.DoubleToInt64Bits(parameter);
            Instance = instance;
        }

        internal bool Equals(in Occurrence other)
            => NodeId == other.NodeId && ParameterBits == other.ParameterBits && Instance == other.Instance;
    }

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
        => TryLiftFromLocalSingular(in supportA, in supportD, in outerSurface,
            in planeAnchor, in planeNormal, radiusSq, in point, spineSeed, spineRadius,
            isTerminatorSpine: false, state, out iterations, out residual);

    /// <summary>
    /// Terminator / ChartPoint spine intervals must not be lowered to the
    /// regular two-support six-equation system (§12.2).
    /// </summary>
    internal static AlgorithmStatus TryLiftFromLocalSingular(
        in AnalyticSurface supportA, in AnalyticSurface supportD,
        in AnalyticSurface outerSurface,
        in KernelVector3 planeAnchor, in KernelVector3 planeNormal,
        double radiusSq, in KernelVector3 point, double spineSeed,
        double spineRadius, bool isTerminatorSpine, Span<double> state,
        out BufferOffset iterations, out double residual)
    {
        iterations = 0;
        residual = 0;
        if (isTerminatorSpine) return AlgorithmStatus.Unsupported;
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

            // Prefer Schur when H_z is stable; otherwise full 6×6 (§12.3–12.4).
            var stepStatus = TrySchurOrFullStep(j, f, step, jCopy, model, pivots, out var predicted);
            if (stepStatus != AlgorithmStatus.Success || !(predicted > 0))
                return stepStatus == AlgorithmStatus.Success ? AlgorithmStatus.NotConverged : stepStatus;
            for (BufferOffset i = 0; i < 6; i++)
            {
                state[i] += step[i];
                if (!double.IsFinite(state[i])) return AlgorithmStatus.NumericalFailure;
            }
        }
        residual = MaxAbs(f);
        return AlgorithmStatus.NotConverged;
    }

    /// <summary>
    /// Migrate between elimination (4: x,s) and joint (6: x,c) plan states
    /// (§12.4). Resets nothing about trust — callers begin a fresh corrector.
    /// </summary>
    internal static AlgorithmStatus TryMigratePlanState(
        ReadOnlySpan<double> from, bool fromIsJoint, double spineRadius,
        Span<double> to, bool toIsJoint)
    {
        if (fromIsJoint == toIsJoint)
        {
            if (to.Length < from.Length) return AlgorithmStatus.WorkspaceTooSmall;
            from.CopyTo(to);
            return AlgorithmStatus.Success;
        }
        if (fromIsJoint)
        {
            // (x,c) → (x,s) with s = atan2(c.y, c.x) for circular spine in XY.
            if (from.Length < 6 || to.Length < 4) return AlgorithmStatus.WorkspaceTooSmall;
            to[0] = from[0]; to[1] = from[1]; to[2] = from[2];
            to[3] = Math.Atan2(from[4], from[3]);
            return AlgorithmStatus.Success;
        }
        // (x,s) → (x,c)
        if (from.Length < 4 || to.Length < 6 || !(spineRadius > 0))
            return AlgorithmStatus.InvalidInput;
        var (sinS, cosS) = Math.SinCos(from[3]);
        to[0] = from[0]; to[1] = from[1]; to[2] = from[2];
        to[3] = spineRadius * cosS; to[4] = spineRadius * sinS; to[5] = 0;
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Schur step with F=(‖q‖²−r², φ_S, p) on x and H=(A,D,q·w) on c.
    /// Falls back to full LU when H_z is Singular.
    /// </summary>
    private static AlgorithmStatus TrySchurOrFullStep(Span<double> jacobian6, ReadOnlySpan<double> residual6,
        Span<double> step, Span<double> jCopy, Span<double> model, Span<int> pivots, out double predicted)
    {
        predicted = 0;
        Span<double> fX = stackalloc double[9];
        Span<double> fZ = stackalloc double[9];
        Span<double> hX = stackalloc double[9];
        Span<double> hZ = stackalloc double[9];
        Span<double> f = stackalloc double[3];
        Span<double> h = stackalloc double[3];
        ExtractOuterInner(jacobian6, fX, fZ, hX, hZ, residual6, f, h);

        Span<double> schurWs = stackalloc double[32];
        Span<double> schurStep = stackalloc double[6];
        var schur = BlockSchurSolve.ComputeStep(fX, fZ, hX, hZ, f, h, 3, 3, 3, schurWs, schurStep,
            out var innerScale);
        if (schur == AlgorithmStatus.Success)
        {
            schurStep.CopyTo(step);
            jacobian6.CopyTo(jCopy);
            SmallLinearSolve.Multiply(jCopy, 6, 6, step, model);
            var baseNormSq = SmallLinearSolve.Dot(residual6, residual6);
            var linNormSq = 0.0;
            for (BufferOffset i = 0; i < 6; i++)
            {
                var v = residual6[i] + model[i];
                linNormSq += v * v;
            }
            predicted = 0.5 * (baseNormSq - linNormSq);
            _ = innerScale;
            return AlgorithmStatus.Success;
        }
        return NewtonStep.ComputeStep(jacobian6, jCopy, residual6, 6, pivots, model, step, out predicted);
    }

    private static void ExtractOuterInner(ReadOnlySpan<double> j, Span<double> fX, Span<double> fZ,
        Span<double> hX, Span<double> hZ, ReadOnlySpan<double> residual, Span<double> f, Span<double> h)
    {
        // F rows 2,4,5 → f rows 0,1,2; H rows 0,1,3 → h rows 0,1,2
        for (var r = 0; r < 3; r++)
        {
            var fRow = r == 0 ? 2 : (r == 1 ? 4 : 5);
            var hRow = r == 0 ? 0 : (r == 1 ? 1 : 3);
            f[r] = residual[fRow];
            h[r] = residual[hRow];
            for (var c = 0; c < 3; c++)
            {
                fX[r * 3 + c] = j[fRow * 6 + c];
                fZ[r * 3 + c] = j[fRow * 6 + 3 + c];
                hX[r * 3 + c] = j[hRow * 6 + c];
                hZ[r * 3 + c] = j[hRow * 6 + 3 + c];
            }
        }
    }

    /// <summary>
    /// §10.6 recovery ladder: try circular-spine envelope 4×4, then joint lift.
    /// Writes either 4-state (x,s) or 6-state (x,c) depending on which path wins;
    /// <paramref name="usedJoint"/> reports the path.
    /// </summary>
    internal static AlgorithmStatus TryRecoverAfterLocalSingular(
        double spineRadius, double tubeRadius,
        in AnalyticSurface outerSurface,
        in AnalyticSurface supportA, in AnalyticSurface supportD,
        in KernelVector3 planeAnchor, in KernelVector3 planeNormal,
        in KernelVector3 point, double spineSeed,
        Span<double> state4, Span<double> state6,
        out bool usedJoint, out BufferOffset iterations, out double residual)
    {
        usedJoint = false;
        iterations = 0;
        residual = 0;
        if (state4.Length < 4 || state6.Length < 6) return AlgorithmStatus.WorkspaceTooSmall;

        state4[0] = point.X; state4[1] = point.Y; state4[2] = point.Z; state4[3] = spineSeed;
        var env = BlendEnvelopeSolve.SolveFourByFour(spineRadius, tubeRadius, in outerSurface,
            in planeAnchor, in planeNormal, state4, out iterations, out residual);
        if (env == AlgorithmStatus.Success) return AlgorithmStatus.Success;

        usedJoint = true;
        return TryLiftFromLocalSingular(in supportA, in supportD, in outerSurface,
            in planeAnchor, in planeNormal, tubeRadius * tubeRadius, in point, spineSeed,
            spineRadius, state6, out iterations, out residual);
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

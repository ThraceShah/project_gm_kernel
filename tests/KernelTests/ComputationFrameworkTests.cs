global using BufferCount = int;

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

/// <summary>
/// Executable composition examples. ProbeSurface is deliberately not a geometry implementation.
/// </summary>
public class ComputationFrameworkTests
{
    [Fact]
    public void WorkspaceSlices_AreDisjoint_AndFailedReservationDoesNotConsumeStorage()
    {
        Span<double> storage = stackalloc double[5];
        storage.Fill(-1);
        var workspace = new WorkBuffer<double>(storage);

        Assert.True(workspace.TryAllocate(2, out var first));
        first.Fill(7);
        Assert.False(workspace.TryAllocate(-1, out _));
        Assert.False(workspace.TryAllocate(int.MaxValue, out _));
        Assert.Equal(2, workspace.Count);
        Assert.True(workspace.TryAllocate(3, out var second));
        second.Fill(9);

        Assert.Equal(7, first[1]);
        Assert.Equal(9, storage[2]);
        Assert.Equal(0, workspace.Remaining);
        Assert.False(workspace.TryAllocate(1, out _));
    }

    [Fact]
    public void DerivativeLayout_HasDefinedMixedDerivativePositions_AndChecksOverflow()
    {
        Assert.True(SurfaceDerivativeLayout.TryCreate(2, 3, out var layout));
        Assert.Equal(12, layout.Count);
        Assert.Equal(6, layout.GetIndex(1, 2));
        Assert.Equal(11, layout.GetIndex(2, 3));
        Assert.Equal(1, default(SurfaceDerivativeLayout).Count);
        Assert.False(SurfaceDerivativeLayout.TryCreate(-1, 0, out _));
        Assert.False(SurfaceDerivativeLayout.TryCreate(int.MaxValue, 0, out _));
        Assert.Equal(-1, layout.GetIndex(0, 4));
    }

    [Fact]
    public void IndependentStages_ComposeThroughCallerOwnedResults()
    {
        var surface = new ProbeSurface();
        ReadOnlySpan<double> parameters = [2, 5, 8];
        Span<KernelVector3> intermediate = stackalloc KernelVector3[3];
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        Span<double> scratch = stackalloc double[2];

        var status = RunPipeline(ref surface, parameters, intermediate, scratch, output, default, out var written);

        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.Equal(3, written);
        Assert.Equal(3, surface.Calls);
        Assert.Equal(8, output[2].X);
        Assert.Equal(73, output[2].Z);
    }

    [Fact]
    public void PartialFailure_DoesNotPublishIntermediateData()
    {
        var surface = new ProbeSurface { FailAt = 2, Failure = AlgorithmStatus.NotConverged };
        ReadOnlySpan<double> parameters = [2, 5, 8];
        Span<KernelVector3> intermediate = stackalloc KernelVector3[3];
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        output.Fill(new KernelVector3 { X = -1 });
        Span<double> scratch = stackalloc double[2];

        var status = RunPipeline(ref surface, parameters, intermediate, scratch, output, default, out var written);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.Equal(0, written);
        Assert.Equal(2, surface.Calls);
        Assert.Equal(-1, output[0].X);
        Assert.Equal(-1, output[2].X);
        Assert.Equal(2, intermediate[0].X);
    }

    [Theory]
    [InlineData((byte)AlgorithmStatus.Unsupported, true)]
    [InlineData((byte)AlgorithmStatus.NotConverged, false)]
    [InlineData((byte)AlgorithmStatus.NumericalFailure, false)]
    public void Orchestrator_ExplicitlyChoosesWhichFailuresAllowFallback(byte failureValue, bool shouldFallback)
    {
        var preferred = new ProbeSurface { FailAt = 2, Failure = (AlgorithmStatus)failureValue };
        var fallback = new ProbeSurface();
        ReadOnlySpan<double> parameters = [2, 5, 8];
        Span<KernelVector3> intermediate = stackalloc KernelVector3[3];
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        Span<double> scratch = stackalloc double[2];

        var status = RunWithFallback(ref preferred, ref fallback, parameters, intermediate, scratch, output, out var written);

        Assert.Equal(shouldFallback ? AlgorithmStatus.Success : (AlgorithmStatus)failureValue, status);
        Assert.Equal(shouldFallback ? 3 : 0, fallback.Calls);
        Assert.Equal(shouldFallback ? 3 : 0, written);
    }

    [Fact]
    public void CapacityAndCancellation_FailWithoutPublishing()
    {
        var surface = new ProbeSurface();
        ReadOnlySpan<double> parameters = [1];
        Span<KernelVector3> intermediate = stackalloc KernelVector3[1];
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        Span<double> scratch = stackalloc double[2];

        Assert.Equal(AlgorithmStatus.OutputTooSmall,
            RunPipeline(ref surface, parameters, intermediate, scratch, [], default, out _));
        Assert.Equal(AlgorithmStatus.WorkspaceTooSmall,
            RunPipeline(ref surface, parameters, [], scratch, output, default, out _));
        Assert.Equal(0, surface.Calls);
        Assert.Equal(AlgorithmStatus.WorkspaceTooSmall,
            RunPipeline(ref surface, parameters, intermediate, [], output, default, out var written));
        Assert.Equal(0, written);

        var callsBeforeCancellation = surface.Calls;
        Assert.Equal(AlgorithmStatus.Cancelled,
            RunPipeline(ref surface, parameters, intermediate, scratch, output, new CancellationToken(true), out written));
        Assert.Equal(callsBeforeCancellation, surface.Calls);
        Assert.Equal(0, written);
    }

    [Fact]
    public void EmptySuccess_IsDifferentFromNotRun()
    {
        var surface = new ProbeSurface();
        Assert.NotEqual(AlgorithmStatus.Success, default(AlgorithmStatus));
        Assert.Equal(AlgorithmStatus.Success, RunPipeline(ref surface, [], [], [], [], default, out var written));
        Assert.Equal(0, written);
        Assert.Equal(0, surface.Calls);
    }

    [Fact]
    public void PreparedComposition_DoesNotAllocateManagedMemoryAfterWarmup()
    {
        var surface = new ProbeSurface();
        ReadOnlySpan<double> parameters = [2, 5, 8];
        Span<KernelVector3> intermediate = stackalloc KernelVector3[3];
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        Span<double> scratch = stackalloc double[2];
        for (var i = 0; i < 100; i++)
            RunPipeline(ref surface, parameters, intermediate, scratch, output, default, out _);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var lastStatus = AlgorithmStatus.NotRun;
        for (var i = 0; i < 1000; i++)
            lastStatus = RunPipeline(ref surface, parameters, intermediate, scratch, output, default, out _);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(AlgorithmStatus.Success, lastStatus);
        Assert.Equal(0, allocated);
        Assert.Equal(3300, surface.Calls);
    }

    // This is a reference orchestration pattern, not a production sampling algorithm.
    // A real operation would validate geometric results between prepare and publish.
    private static AlgorithmStatus RunPipeline(
        ref ProbeSurface surface,
        ReadOnlySpan<double> parameters,
        Span<KernelVector3> intermediate,
        Span<double> scratch,
        Span<KernelVector3> output,
        CancellationToken cancellation,
        out BufferCount written)
    {
        written = 0;
        if (cancellation.IsCancellationRequested)
            return AlgorithmStatus.Cancelled;
        if (output.Length < parameters.Length)
            return AlgorithmStatus.OutputTooSmall;

        var buffer = new WorkBuffer<KernelVector3>(intermediate);
        if (!buffer.TryAllocate(parameters.Length, out var candidates))
            return AlgorithmStatus.WorkspaceTooSmall;

        var status = PrepareCandidates(ref surface, parameters, candidates, scratch, cancellation);
        if (status != AlgorithmStatus.Success)
            return status;
        if (cancellation.IsCancellationRequested)
            return AlgorithmStatus.Cancelled;

        PublishCandidates(candidates, output);
        written = candidates.Length;
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus PrepareCandidates(
        ref ProbeSurface surface,
        ReadOnlySpan<double> parameters,
        Span<KernelVector3> candidates,
        Span<double> scratch,
        CancellationToken cancellation)
    {
        var layout = default(SurfaceDerivativeLayout);
        for (var i = 0; i < parameters.Length; i++)
        {
            if (cancellation.IsCancellationRequested)
                return AlgorithmStatus.Cancelled;
            var status = ProbeSurfaceEvaluation.Evaluate(ref surface, parameters[i], 0, in layout, candidates.Slice(i, 1), scratch);
            if (status != AlgorithmStatus.Success)
                return status;
        }
        return AlgorithmStatus.Success;
    }

    private static void PublishCandidates(ReadOnlySpan<KernelVector3> candidates, Span<KernelVector3> output)
        => candidates.CopyTo(output);

    private static AlgorithmStatus RunWithFallback(
        ref ProbeSurface preferred,
        ref ProbeSurface fallback,
        ReadOnlySpan<double> parameters,
        Span<KernelVector3> intermediate,
        Span<double> scratch,
        Span<KernelVector3> output,
        out BufferCount written)
    {
        var status = RunPipeline(ref preferred, parameters, intermediate, scratch, output, default, out written);
        return status == AlgorithmStatus.Unsupported
            ? RunPipeline(ref fallback, parameters, intermediate, scratch, output, default, out written)
            : status;
    }

    private struct ProbeSurface
    {
        public BufferCount Calls;
        public BufferCount FailAt;
        public AlgorithmStatus Failure;
    }

    private static class ProbeSurfaceEvaluation
    {
        public static AlgorithmStatus Evaluate(ref ProbeSurface surface, double u, double v, in SurfaceDerivativeLayout layout,
            Span<KernelVector3> derivatives, Span<double> workspace)
        {
            surface.Calls++;
            if (surface.FailAt == surface.Calls)
                return surface.Failure;
            if (derivatives.Length < layout.Count)
                return AlgorithmStatus.OutputTooSmall;
            if (workspace.Length < 2)
                return AlgorithmStatus.WorkspaceTooSmall;

            workspace[0] = u;
            workspace[1] = v;
            derivatives[..layout.Count].Fill(new KernelVector3 { X = workspace[0], Y = workspace[1], Z = 73 });
            return AlgorithmStatus.Success;
        }
    }
}

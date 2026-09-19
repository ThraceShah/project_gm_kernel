using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

/// <summary>L0 residual memo (spec §13.3, task T09): trial generation + exact keys.</summary>
public class ResidualMemoTests
{
    [Fact]
    public void InsertFind_RoundTripsExactKey()
    {
        var memo = new ResidualMemo();
        memo.BeginTrial();
        Span<double> residual = [0.1, -0.2, 0.3];
        Assert.True(memo.TryInsert(ICurveConstraintPlan.I3, 0.5, 1, 2, residual));
        Span<double> found = stackalloc double[3];
        Assert.True(memo.TryFind(ICurveConstraintPlan.I3, 0.5, 1, 2, found));
        Assert.Equal(0.1, found[0], 15);
        Assert.Equal(-0.2, found[1], 15);
        Assert.Equal(0.3, found[2], 15);
        Assert.Equal(1, memo.Count);
    }

    [Fact]
    public void BeginTrial_InvalidatesPriorEntries()
    {
        var memo = new ResidualMemo();
        memo.BeginTrial();
        Assert.True(memo.TryInsert(ICurveConstraintPlan.P2, 1.0, 0, 1, [1, 2]));
        memo.BeginTrial();
        Span<double> found = stackalloc double[2];
        Assert.False(memo.TryFind(ICurveConstraintPlan.P2, 1.0, 0, 1, found));
        Assert.Equal(0, memo.Count);
        Assert.True(memo.Generation >= 2);
    }

    [Fact]
    public void DifferentParameterBits_DoNotCollide()
    {
        var memo = new ResidualMemo();
        memo.BeginTrial();
        Assert.True(memo.TryInsert(ICurveConstraintPlan.I1, 0.25, 0, 0, [7]));
        Assert.True(memo.TryInsert(ICurveConstraintPlan.I1, 0.2500000000000001, 0, 0, [9]));
        Span<double> found = stackalloc double[1];
        Assert.True(memo.TryFind(ICurveConstraintPlan.I1, 0.25, 0, 0, found));
        Assert.Equal(7, found[0], 15);
        Assert.True(memo.TryFind(ICurveConstraintPlan.I1, 0.2500000000000001, 0, 0, found));
        Assert.Equal(9, found[0], 15);
    }

    [Fact]
    public void RingOverwrite_KeepsCapacityBound()
    {
        var memo = new ResidualMemo();
        memo.BeginTrial();
        for (var i = 0; i < ResidualMemo.Capacity + 3; i++)
            Assert.True(memo.TryInsert(ICurveConstraintPlan.P4, i, 0, 0, [i]));
        Assert.Equal(ResidualMemo.Capacity, memo.Count);
        Span<double> found = stackalloc double[1];
        // Oldest entries before the ring wrap are gone; a late key remains.
        Assert.True(memo.TryFind(ICurveConstraintPlan.P4, ResidualMemo.Capacity + 2, 0, 0, found));
        Assert.Equal(ResidualMemo.Capacity + 2, found[0], 15);
    }
}

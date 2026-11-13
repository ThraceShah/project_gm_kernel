using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Runtime.KernelRuntime;

namespace KernelTests;

public class SheetCycleSpurTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PairedCutTreeDoesNotChangeClosedFaceBoundary(bool reverse)
    {
        ModelingEdge[] edges=[new(){StartVertex=0,EndVertex=1},new(){StartVertex=1,EndVertex=2},new(){StartVertex=2,EndVertex=0},
            new(){StartVertex=1,EndVertex=3},new(){StartVertex=3,EndVertex=4},new(){StartVertex=3,EndVertex=5}];
        ModelingCoedge[] uses=[new(){Edge=0,Forward=1},new(){Edge=1,Forward=1},new(){Edge=2,Forward=1},
            new(){Edge=3,Forward=1},new(){Edge=3,Forward=0},new(){Edge=4,Forward=1},new(){Edge=4,Forward=0},new(){Edge=5,Forward=1},new(){Edge=5,Forward=0}];
        if(reverse){Array.Reverse(uses);for(var i=0;i<uses.Length;i++)uses[i].Forward^=1;}
        Span<byte> removed=stackalloc byte[uses.Length];KernelRuntime.TestSheetCycleSpurs(edges,uses,removed);
        for(var i=0;i<uses.Length;i++)Assert.Equal(uses[i].Edge>=3?(byte)1:(byte)0,removed[i]);
    }

    [Fact]
    public void ClosedCutsAndUnpairedSourceUsesCannotBePeeledAsSpurs()
    {
        ModelingEdge[] edges=[new(){StartVertex=0,EndVertex=1},new(){StartVertex=1,EndVertex=2},new(){StartVertex=2,EndVertex=0},new(){StartVertex=2,EndVertex=3}];
        ModelingCoedge[] uses=[new(){Edge=0,Forward=1},new(){Edge=0,Forward=0},new(){Edge=1,Forward=1},new(){Edge=1,Forward=0},
            new(){Edge=2,Forward=1},new(){Edge=2,Forward=0},new(){Edge=3,Forward=1}];
        Span<byte> removed=stackalloc byte[uses.Length];KernelRuntime.TestSheetCycleSpurs(edges,uses,removed);
        foreach(var flag in removed)Assert.Equal(0,flag);
    }
}

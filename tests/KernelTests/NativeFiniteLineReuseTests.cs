using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class NativeFiniteLineReuseTests:IDisposable
{
    public NativeFiniteLineReuseTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose()=>KernelRuntime.SessionStop();

    [Theory]
    [InlineData(2.0,true,true,false)]
    [InlineData(2.0,false,false,false)]
    [InlineData(2.0,false,true,true)]
    [InlineData(1000.0,false,true,false)]
    public void AWholeFiniteAffineCorrespondenceNeedsTheSameEventsAndOneSource(double length,bool expected,bool sameEvents,bool duplicate)
    {
        var line=new PK_LINE_sf_s();line.basis_set.axis.coord[0]=1;int source,cut;
        Assert.Equal(0,KernelRuntime.LineCreate(&line,&source));
        line.basis_set.axis.coord[1]=1e-10;line.basis_set.location.coord[1]=1e-10;
        Assert.Equal(0,KernelRuntime.LineCreate(&line,&cut));
        Assert.Equal(expected,KernelRuntime.TestNativeFiniteLineFragment(cut,source,length,sameEvents,duplicate));
        var before=GC.GetAllocatedBytesForCurrentThread();var result=KernelRuntime.TestNativeFiniteLineFragment(cut,source,length,sameEvents,duplicate);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(expected,result);Assert.Equal(0,allocated);
    }
}

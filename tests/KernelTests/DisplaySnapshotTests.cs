using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class DisplaySnapshotTests : IDisposable
{
    public DisplaySnapshotTests()
    {
        KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};
        Assert.Equal(0,KernelRuntime.SessionStart(&options));
    }
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Fact]
    public void SnapshotUsesExportedPersistentIdentifiersAndIndependentReturnBuffers()
    {
        var body=Block();var before=KernelRuntime.State.Session->Returns.OutstandingBlocks;
        delegate* unmanaged<int,PgmDisplaySnapshot*,int> capture=&KernelExports.PGM_BODY_export_display;
        delegate* unmanaged<PgmDisplaySnapshot*,int> release=&KernelExports.PGM_DISPLAY_snapshot_free;
        PgmDisplaySnapshot first=default,second=default;
        Assert.Equal(0,capture(body,&first));Assert.Equal(0,capture(body,&second));
        try
        {
            Assert.Equal(26,first.BindingCount);Assert.NotEqual((nint)first.Bindings,(nint)second.Bindings);
            Assert.NotEqual((nint)first.Xt.bytes,(nint)second.Xt.bytes);
            Assert.Equal(before+4,KernelRuntime.State.Session->Returns.OutstandingBlocks);
            var identifiers=new HashSet<int>();
            for(var i=0;i<first.BindingCount;i++)
            {
                var item=first.Bindings[i];Assert.True(identifiers.Add(item.Identifier));
                int actualClass=0;Assert.Equal(0,KernelRuntime.EntityAskClass(item.Entity,&actualClass));Assert.Equal(actualClass,item.Class);
                var binding=new XtWriter.DisplayBinding(body,item.Entity,item.Class,item.Identifier,item.Generation);
                Assert.True(XtWriter.IsCurrentDisplayBinding(binding,first.SessionGeneration,first.BodyGeneration));
                Assert.False(XtWriter.IsCurrentDisplayBinding(binding,first.SessionGeneration+1,first.BodyGeneration));
                Assert.False(XtWriter.IsCurrentDisplayBinding(binding,first.SessionGeneration,first.BodyGeneration+1));
            }
            Assert.Equal(0,release(&first));Assert.Equal(0,release(&first));
            Assert.Equal(26,second.BindingCount);Assert.True(second.Xt.n_bytes>0);
        }
        finally {release(&first);release(&second);}
        Assert.Equal(before,KernelRuntime.State.Session->Returns.OutstandingBlocks);
    }

    [Fact]
    public void DeleteAndRollbackInvalidateOrRestoreTheSameSourceIdentity()
    {
        var body=Block();Assert.Equal(0,XtWriter.CaptureDisplay(body,out _,out var bindings,out var session,out var generation));
        var binding=bindings[0];int mark;Assert.Equal(0,KernelRuntime.MarkCreate(&mark));
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.False(XtWriter.IsCurrentDisplayBinding(binding,session,generation));
        Assert.Equal(0,KernelRuntime.MarkGoto(mark));Assert.True(XtWriter.IsCurrentDisplayBinding(binding,session,generation));
        Assert.False(XtWriter.IsCurrentDisplayBinding(binding with{Class=ParasolidConstants.PK_CLASS_point},session,generation));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(5)]
    public void AllocationFailureDoesNotLeakOrPublishPartialSnapshot(int after)
    {
        var body=Block();var bytes=KernelRuntime.State.Session->Returns.OutstandingBytes;var blocks=KernelRuntime.State.Session->Returns.OutstandingBlocks;
        delegate* unmanaged<int,PgmDisplaySnapshot*,int> capture=&KernelExports.PGM_BODY_export_display;
        delegate* unmanaged<PgmDisplaySnapshot*,int> release=&KernelExports.PGM_DISPLAY_snapshot_free;
        PgmDisplaySnapshot snapshot=default;int error;
        KernelRuntime.InjectAllocationFailure(after);
        try {error=capture(body,&snapshot);}
        finally {KernelRuntime.InjectAllocationFailure(-1);}
        if(error==0)Assert.Equal(0,release(&snapshot));
        else
        {
            Assert.Equal(ParasolidConstants.PK_ERROR_memory_full,error);
            Assert.Equal(0,snapshot.BindingCount);Assert.Equal(nint.Zero,(nint)snapshot.Bindings);Assert.Equal(nint.Zero,(nint)snapshot.Xt.bytes);
        }
        Assert.Equal(bytes,KernelRuntime.State.Session->Returns.OutstandingBytes);Assert.Equal(blocks,KernelRuntime.State.Session->Returns.OutstandingBlocks);
        Assert.True(KernelRuntime.IsValidTag(body));
    }

    private static int Block(){int body;Assert.Equal(0,KernelRuntime.BodyCreateSolidBlock(1,2,3,null,&body));return body;}
}

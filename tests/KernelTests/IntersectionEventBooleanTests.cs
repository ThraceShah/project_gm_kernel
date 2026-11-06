using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class IntersectionEventBooleanTests:IDisposable
{
    public IntersectionEventBooleanTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(15903)] [InlineData(15901)] [InlineData(15902)]
    public void UnresolvedPrecisionOfANewPlanarImprintJunctionRejectsAtomically(int function)
    {
        var target=RegularCurvedBooleanTests.RoofSolid();var tool=RegularCurvedBooleanTests.RoofSolid();
        Subtract(target,BooleanOperationTests.Block(.1,.5,.25,.6,2,2));Subtract(tool,BooleanOperationTests.Block(.9,.5,.25,.6,2,2));
        var beforeTarget=XtBrepImportTests.Transmit(target);var beforeTool=XtBrepImportTests.Transmit(tool);
        var curves=KernelRuntime.Curves.AliveCount;var surfaces=KernelRuntime.Surfaces.AliveCount;
        var options=BooleanOperationTests.Options(function);int count;int* products;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented,KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&products));
        Assert.Equal(0,count);Assert.True(products==null);
        Assert.Equal(beforeTarget,XtBrepImportTests.Transmit(target));Assert.Equal(beforeTool,XtBrepImportTests.Transmit(tool));
        Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(surfaces,KernelRuntime.Surfaces.AliveCount);
        int* bodies=stackalloc int[2]{target,tool};Assert.Equal(0,KernelRuntime.EntityDelete(2,bodies));
        Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }

    private static void Subtract(int target,int tool)
    {
        var options=BooleanOperationTests.Options(15902);int n;int* products;
        Assert.Equal(0,KernelRuntime.BodyBoolean(target,1,&tool,&options,&n,&products));Assert.Equal(1,n);KernelRuntime.MemoryFree(products);
    }

    [Theory]
    [InlineData(15903,false,1)] [InlineData(15901,false,1)] [InlineData(15902,false,0)]
    [InlineData(15903,true,1)] [InlineData(15901,true,1)] [InlineData(15902,true,0)]
    public void CoincidentFreeformDomainsHaveTwoSidedMaterialSemantics(int function,bool reload,int expected)
    {
        var target=RegularCurvedBooleanTests.RoofSolid();int tool;
        if(reload)Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(target),out tool));
        else Assert.Equal(0,KernelRuntime.EntityCopy(target,&tool));
        var options=BooleanOperationTests.Options(function);int count;int* products;
        var error=KernelRuntime.BodyBoolean(target,1,&tool,&options,&count,&products);
        Assert.True(error==0,$"error={error}, stage={KernelRuntime.LastRegularBooleanStage}, pair={KernelRuntime.LastRegularBooleanFace}/{KernelRuntime.LastRegularBooleanOtherFace}, curve={KernelRuntime.LastSheetCurve}, t={KernelRuntime.LastSheetParameter}, sheet={KernelRuntime.LastSheetPartitionStage}");
        try{Assert.Equal(expected,count);if(expected!=0){Assert.Equal(target,products[0]);BooleanOperationTests.Counts(target,6,12,8);Assert.Equal(0,KernelRuntime.EntityDelete(1,&target));}}
        finally{if(products!=null)KernelRuntime.MemoryFree(products);}
        Assert.Equal(0,KernelRuntime.Bodies.AliveCount);Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Surfaces.AliveCount);
    }
}

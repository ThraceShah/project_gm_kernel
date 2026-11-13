global using MaterialCellIndex=System.Int32;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class RegularMaterialComponentsTests
{
    [Fact]
    public void OnlyOppositeUncutSourceUsesOfTheSameBodyConnectMaterialCells()
    {
        var uses=new KernelRuntime.ModelingCoedge[]{new(){Edge=0,Forward=1},new(){Edge=0},new(){Edge=1,Forward=1},new(){Edge=1}};
        var loops=new KernelRuntime.ModelingLoop[]{new(){FirstCoedge=0,CoedgeCount=1},new(){FirstCoedge=1,CoedgeCount=2},new(){FirstCoedge=3,CoedgeCount=1}};
        var labels=new MaterialCellIndex[3];
        KernelRuntime.TestRegularMaterialComponents([0,0,0],uses,loops,[0,0],2,labels);
        Assert.Equal([0,0,0],labels);
        KernelRuntime.TestRegularMaterialComponents([0,0,0],uses,loops,[0,1],2,labels);
        Assert.Equal([0,0,2],labels);
        KernelRuntime.TestRegularMaterialComponents([0,0,1],uses,loops,[0,0],2,labels);
        Assert.Equal([0,0,2],labels);
        KernelRuntime.TestRegularMaterialComponents([0,0,0],uses,loops,[0,0],1,labels);
        Assert.Equal([0,0,2],labels);
        uses[1].Forward=1;
        KernelRuntime.TestRegularMaterialComponents([0,0,0],uses,loops,[0,0],2,labels);
        Assert.Equal([0,1,1],labels);
        KernelRuntime.TestRegularMaterialComponents([0,0,0],uses,loops,[0,0],2,labels,enabled:false);
        Assert.Equal([0,1,2],labels);
    }

    [Fact]
    public void AThirdUseCannotSupplyAManifoldMaterialConnection()
    {
        var uses=new KernelRuntime.ModelingCoedge[]{new(){Edge=0,Forward=1},new(){Edge=0},new(){Edge=1,Forward=1},new(){Edge=1},new(){Edge=0,Forward=1}};
        var loops=new KernelRuntime.ModelingLoop[]{new(){FirstCoedge=0,CoedgeCount=1},new(){FirstCoedge=1,CoedgeCount=2},new(){FirstCoedge=3,CoedgeCount=1},new(){FirstCoedge=4,CoedgeCount=1}};
        var labels=new MaterialCellIndex[4];
        KernelRuntime.TestRegularMaterialComponents([0,0,0,0],uses,loops,[0,0],2,labels);
        Assert.Equal([0,1,1,3],labels);
    }
}

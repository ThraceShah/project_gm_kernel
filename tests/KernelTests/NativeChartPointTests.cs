using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class NativeChartPointTests:IDisposable
{
    public NativeChartPointTests(){KernelRuntime.SessionStop();var start=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&start));}
    public void Dispose()=>KernelRuntime.SessionStop();

    [Fact]
    public void ProceduralCurvePublishesItsStoredNativeChartPoint()
    {
        var text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","generated-short-edge-native.x_t"));
        Assert.Equal(0,XtBrepImportTests.Receive(text,out var body));Assert.True(KernelRuntime.TryResolveBodySlot(body,out var slot));
        var edge=KernelRuntime.Bodies[slot].FirstEdgeBody;var count=0;PK_VECTOR_s point;
        for(var e=0;e<KernelRuntime.Bodies[slot].EdgeCountBody;e++,edge=KernelRuntime.Edges[edge].NextInBody)
        {
            var tag=KernelRuntime.Edges[edge].CurveTag;var curve=KernelRuntime.GetCurveByTag(tag);
            if(curve.Class!=CurveClass.ICurve)continue;var data=KernelRuntime.ICurveDataPool[curve.DataIndex];
            if(data.IsBlendContact!=0||data.ProgramBlock<=0)continue;
            var program=KernelRuntime.GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
            var definition=program.Spines[data.ProgramSpine];if(SpineParameterMap.Explicit(definition))continue;
            foreach(var chart in program.Charts.Slice(definition.ChartOffset,definition.ChartCount))
            {
                if(chart.Parameter<curve.TMin||chart.Parameter>curve.TMax)continue;
                Assert.Equal(0,KernelRuntime.CurveEval(tag,chart.Parameter,0,&point));
                Assert.Equal(chart.Position.X,point.coord[0]);Assert.Equal(chart.Position.Y,point.coord[1]);Assert.Equal(chart.Position.Z,point.coord[2]);count++;
            }
        }
        Assert.True(count>0);
    }
}

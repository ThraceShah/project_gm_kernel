using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class EdgeSubdivisionTests:IDisposable
{
    public EdgeSubdivisionTests(){KernelRuntime.SessionStop();var options=new PK_SESSION_start_o_s{o_t_version=1};Assert.Equal(0,KernelRuntime.SessionStart(&options));}
    public void Dispose(){KernelRuntime.InjectAllocationFailure(-1);KernelRuntime.SessionStop();}

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SplitPreservesOrientedLoopsAndFiniteRanges(bool negative)
    {
        var body=RegularSolidBlendTests.Roof();var tag=RegularSolidBlendTests.SelectedEdge(body);var slot=KernelRuntime.ResolveTagRecord(tag).Slot;
        var curve=KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[slot].CurveTag)];
        if(negative)
        {
            KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(curve.Header.Tag)].Sense=ParasolidConstants.PK_TOPOL_sense_negative_c;
            var end=KernelRuntime.Edges[slot].EndVertex;KernelRuntime.Edges[slot].EndVertex=KernelRuntime.Edges[slot].StartVertex;KernelRuntime.Edges[slot].StartVertex=end;
            var fin=KernelRuntime.Edges[slot].FirstFinEdge;for(var i=0;i<2;i++,fin=KernelRuntime.Fins[fin].NextOfEdge)KernelRuntime.Fins[fin].Sense=KernelRuntime.Fins[fin].Sense=='+'?'-':'+';
        }
        int vertex,tail;Assert.Equal(0,KernelRuntime.EdgeSplitAtParam(tag,.37,&vertex,&tail));Assert.True(KernelRuntime.IsValidTag(tag));
        Assert.NotEqual(0,vertex);Assert.NotEqual(0,tail);BooleanOperationTests.Counts(body,6,13,9);
        var extra=KernelRuntime.Edges[KernelRuntime.ResolveTagRecord(tail).Slot];var original=KernelRuntime.Edges[slot];
        Assert.Equal(0,original.ParameterStart,12);Assert.Equal(.37,original.ParameterEnd,12);Assert.Equal(.37,extra.ParameterStart,12);Assert.Equal(1,extra.ParameterEnd,12);
        Assert.Equal(original.CurveTag,extra.CurveTag);
        KernelRuntime.TryResolveBodySlot(body,out var bodySlot);var face=KernelRuntime.Bodies[bodySlot].FirstFaceBody;
        for(var f=0;f<KernelRuntime.Bodies[bodySlot].FaceCountBody;f++,face=KernelRuntime.Faces[face].NextInBody)
        {
            var loop=KernelRuntime.Faces[face].FirstLoop;var fin=KernelRuntime.Loops[loop].FirstFin;
            for(var i=0;i<KernelRuntime.Loops[loop].FinCount;i++,fin=KernelRuntime.Fins[fin].NextInLoop)
            {
                var next=KernelRuntime.Fins[fin].NextInLoop;var nextEdge=KernelRuntime.Edges[KernelRuntime.Fins[next].Edge];
                Assert.Equal(KernelRuntime.Fins[fin].Vertex,KernelRuntime.Fins[next].Sense=='+'?nextEdge.StartVertex:nextEdge.EndVertex);
            }
        }
        Assert.Equal(0,XtBrepImportTests.Receive(XtBrepImportTests.Transmit(body),out var received));BooleanOperationTests.Counts(received,6,13,9);
        Assert.Equal(0,KernelRuntime.EntityDelete(1,&body));Assert.Equal(0,KernelRuntime.EntityDelete(1,&received));Assert.Equal(0,KernelRuntime.Curves.AliveCount);Assert.Equal(0,KernelRuntime.Points.AliveCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(5)] [InlineData(8)]
    public void FailedSplitRestoresTheEntireSource(int budget)
    {
        var body=RegularSolidBlendTests.Roof();var edge=RegularSolidBlendTests.SelectedEdge(body);var before=XtBrepImportTests.Transmit(body);
        var blocks=KernelRuntime.BlocksLiveBytes;var curves=KernelRuntime.Curves.AliveCount;var points=KernelRuntime.Points.AliveCount;
        KernelRuntime.InjectAllocationFailure(budget);int vertex,tail;int error;
        try{error=KernelRuntime.EdgeSplitAtParam(edge,.37,&vertex,&tail);}finally{KernelRuntime.InjectAllocationFailure(-1);}
        if(error==0){BooleanOperationTests.Counts(body,6,13,9);return;}
        Assert.Equal(0,vertex);Assert.Equal(0,tail);Assert.Equal(before,XtBrepImportTests.Transmit(body));Assert.Equal(blocks,KernelRuntime.BlocksLiveBytes);
        Assert.Equal(curves,KernelRuntime.Curves.AliveCount);Assert.Equal(points,KernelRuntime.Points.AliveCount);
    }
}

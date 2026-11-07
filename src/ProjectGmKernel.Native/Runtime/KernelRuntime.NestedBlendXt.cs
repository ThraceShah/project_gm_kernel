using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus MaterializeNestedBlendSpine(XtDocument document,XtNode node,SurfTag first,SurfTag second,out CurveTag curve)
    {
        curve=0;var owns=EnsureCommandScratch();var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var fields=node.Fields;
            var chart=FindNodeByIndex(document.Nodes,fields[9].Pointer);var start=FindNodeByIndex(document.Nodes,fields[10].Pointer);var end=FindNodeByIndex(document.Nodes,fields[11].Pointer);
            if(chart==null||start==null||end==null)return AlgorithmStatus.InvalidInput;
            if(chart.VariableLength<2||chart.VariableLength>NestedChartLimit)return AlgorithmStatus.Unsupported;
            var chartBuffer=NestedWorkspace<double>(chart.VariableLength*3);
            if(chartBuffer.IsEmpty||!TryReadChartNode(chart,chartBuffer,out var count,out var baseParameter,out var baseScale,out var ctol,out var atol,
                out var error0,out var hasError0,out var error1,out var hasError1))return AlgorithmStatus.Unsupported;
            Span<double> startBuffer=stackalloc double[6],endBuffer=stackalloc double[6];
            if(!TryReadLimitNode(start,startBuffer,out var startType,out var startTerm,out var startCount)||!TryReadLimitNode(end,endBuffer,out var endType,out var endTerm,out var endCount))return AlgorithmStatus.InvalidInput;
            if(startCount!=1||endCount!=1||startTerm!=LimitTermUse.Unset||endTerm!=LimitTermUse.Unset)return AlgorithmStatus.Unsupported;
            if(!startBuffer[..3].SequenceEqual(chartBuffer[..3])||!endBuffer[..3].SequenceEqual(chartBuffer.Slice(3*(count-1),3)))return AlgorithmStatus.Unsupported;
            var dataNode=FindNodeByIndex(document.Nodes,fields[^1].Pointer);
            if(dataNode==null&&fields[^1].Pointer!=0)return AlgorithmStatus.InvalidInput;
            var uvType=IntersectionUvType.None;var uvCount=0;Span<double> uv=default,knownUv=default;
            if(dataNode!=null)
            {
                // The procedural compiler already bounds the CHART count.
                // Its paired witness payload is four values per native row,
                // not the smaller analytic-reader fixed stack capacity.
                if(dataNode.VariableLength<0||dataNode.VariableLength>4*count)return AlgorithmStatus.InvalidInput;
                uv=NestedWorkspace<double>(dataNode.VariableLength);if(uv.Length!=dataNode.VariableLength)return AlgorithmStatus.WorkspaceTooSmall;
                if(!TryReadIntersectionDataNode(dataNode,uv,out uvType,out uvCount,4*count)||uvCount!=ExpectedUvValueCount(uvType,count,0))return AlgorithmStatus.InvalidInput;
                var stride=UvStrideOf(uvType);
                for(var row=0;stride>0&&row<uvCount;row+=stride)
                {
                    if(uvType is IntersectionUvType.First or IntersectionUvType.Both)LiftImportedBlendChart(first,ref uv[row]);
                    if(uvType is IntersectionUvType.Second or IntersectionUvType.Both)LiftImportedBlendChart(second,ref uv[row+(stride==4?2:0)]);
                }
                if(uvType==IntersectionUvType.Both)knownUv=uv;
                else if(uvType is IntersectionUvType.First or IntersectionUvType.Second)
                {
                    knownUv=NestedWorkspace<double>(4*count);if(knownUv.IsEmpty)return AlgorithmStatus.WorkspaceTooSmall;
                    knownUv.Fill(double.NaN);var side=uvType==IntersectionUvType.First?0:2;
                    for(var row=0;row<count;row++)uv.Slice(2*row,2).CopyTo(knownUv.Slice(4*row+side,2));
                }
            }
            var compiler=new NestedProgramCompiler();if(!compiler.Ready)return AlgorithmStatus.WorkspaceTooSmall;
            var status=compiler.Surface(first,out var a);if(status!=AlgorithmStatus.Success)
            {if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTICS")=="1")Console.Error.WriteLine($"XT ICurve node={node.Index} first={first} compile={status}");return status;}
            status=compiler.Surface(second,out var b);if(status!=AlgorithmStatus.Success)
            {if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTICS")=="1")Console.Error.WriteLine($"XT ICurve node={node.Index} second={second} compile={status}");return status;}
            status=compiler.ChartSpine(a,b,MemoryMarshal.Cast<double,KernelVector3>(chartBuffer[..(count*3)]),baseParameter,baseScale,ctol,atol,startType,endType,out var root,XtSense(fields[6].Character),knownUv);if(status!=AlgorithmStatus.Success)
            {if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTICS")=="1")Console.Error.WriteLine($"XT ICurve node={node.Index} ChartSpine={status} charts={count} supports={first}/{second}");return status;}
            CurveTag result=0;var error=BindNestedBlendSpine(first,second,compiler.View,root,&result,uvType==IntersectionUvType.Both?uv:default);
            if(error!=0)
            {if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTICS")=="1")Console.Error.WriteLine($"XT nested ICurve node={node.Index} supports={first}/{second} bind={error}");return error==Generated.ParasolidConstants.PK_ERROR_memory_full?AlgorithmStatus.WorkspaceTooSmall:AlgorithmStatus.InvalidInput;}
            ref var data=ref ICurveDataPool[Curves[GetCurveSlotByTag(result)].DataIndex];
            data.ParameterError=error0;data.ParameterErrorProvided=hasError0;data.ParameterError1=error1;data.ParameterError1Provided=hasError1;data.SourceSchema=document.Schema.SchemaNumber;
            if(fields.Length==14&&fields[12].Kind==XtFieldKind.Real){data.Scale=fields[12].Real;data.ScaleProvided=1;}
            if(uvType is IntersectionUvType.First or IntersectionUvType.Second)
            {
                var payload=State.Session->Blocks.TryAllocate((nuint)(uvCount*8));if(payload==null)return AlgorithmStatus.WorkspaceTooSmall;
                data.UvValueBlock=State.Session->Blocks.HandleOf(payload);uv[..uvCount].CopyTo(new Span<double>(payload,uvCount));
                data.UvType=uvType;data.UvValueCount=uvCount;
            }
            curve=result;return AlgorithmStatus.Success;
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;if(owns)ReleaseCommandScratch(State.Session);}
    }

    // Explicit conic LIMITs are spatial records. On receive the same finite
    // half-turn can be lifted by 2π when the first limit is on the seam.
    // Transport the native witness into that lift, without solving a new
    // spatial inverse or changing the section coordinate.
    private static void LiftImportedBlendChart(SurfTag surface,ref double u)
    {
        var slot=GetSurfaceSlotByTag(surface);if(slot<0)return;var record=Surfaces[slot];
        if(record.Class!=SurfaceClass.BlendSurface)return;
        LiftFiniteConicBlendParameter(record,BlendedEdgeDataPool[record.DataIndex].SpineTag,ref u);
    }
}

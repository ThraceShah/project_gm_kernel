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
            var compiler=new NestedProgramCompiler();if(!compiler.Ready)return AlgorithmStatus.WorkspaceTooSmall;
            var status=compiler.Surface(first,out var a);if(status!=AlgorithmStatus.Success)return status;
            status=compiler.Surface(second,out var b);if(status!=AlgorithmStatus.Success)return status;
            status=compiler.ChartSpine(a,b,MemoryMarshal.Cast<double,KernelVector3>(chartBuffer[..(count*3)]),baseParameter,baseScale,ctol,atol,startType,endType,out var root,XtSense(fields[6].Character));if(status!=AlgorithmStatus.Success)return status;
            CurveTag result=0;var error=BindNestedBlendSpine(first,second,compiler.View,root,&result);if(error!=0)return error==Generated.ParasolidConstants.PK_ERROR_memory_full?AlgorithmStatus.WorkspaceTooSmall:AlgorithmStatus.InvalidInput;
            ref var data=ref ICurveDataPool[Curves[GetCurveSlotByTag(result)].DataIndex];
            data.ParameterError=error0;data.ParameterErrorProvided=hasError0;data.ParameterError1=error1;data.ParameterError1Provided=hasError1;data.SourceSchema=document.Schema.SchemaNumber;
            if(fields.Length==14&&fields[12].Kind==XtFieldKind.Real){data.Scale=fields[12].Real;data.ScaleProvided=1;}
            var dataNode=FindNodeByIndex(document.Nodes,fields[fields.Length-1].Pointer);
            if(dataNode!=null)
            {
                var uvBuffer=NestedWorkspace<double>(dataNode.VariableLength);
                if(!TryReadIntersectionDataNode(dataNode,uvBuffer,out var uvType,out var uvCount))return AlgorithmStatus.Unsupported;
                if(uvCount!=ExpectedUvValueCount(uvType,count,0))return AlgorithmStatus.InvalidInput;
                data.UvType=uvType;data.UvValueCount=uvCount;
                if(uvCount>0)
                {
                    var payload=State.Session->Blocks.TryAllocate((nuint)(uvCount*8));if(payload==null)return AlgorithmStatus.WorkspaceTooSmall;
                    data.UvValueBlock=State.Session->Blocks.HandleOf(payload);uvBuffer[..uvCount].CopyTo(new Span<double>(payload,uvCount));
                }
            }
            curve=result;return AlgorithmStatus.Success;
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;if(owns)ReleaseCommandScratch(State.Session);}
    }
}

using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A BLEND_BOUND is an implicit transverse support for the tangency curve,
    // not the tangent Blend surface itself and not a public parametric entity.
    private static AlgorithmStatus MaterializeBlendContactXt(XtDocument document,XtNode node,XtNode boundary,
        SurfTag support,SurfTag blend,out CurveTag curve)
    {
        curve=0;
        if(boundary.Type!=(int)XtNodeTypes.BlendBound||boundary.Fields.Length!=9
            ||boundary.Fields[7].Kind!=XtFieldKind.Integer||boundary.Fields[7].Integer is <0 or >1)
            return AlgorithmStatus.InvalidInput;
        if(boundary.Fields[6].Character is not ('+' or '-'))return AlgorithmStatus.InvalidInput;
        if(boundary.Fields[6].Character!='+')return AlgorithmStatus.Unsupported;
        var reversed=node.Fields[7].Pointer==boundary.Index;
        var blendSlot=GetSurfaceSlotByTag(blend);
        if(blendSlot<0||Surfaces[blendSlot].Class!=SurfaceClass.BlendSurface)return AlgorithmStatus.InvalidInput;
        var side=1-(BufferOffset)boundary.Fields[7].Integer;var definition=BlendedEdgeDataPool[Surfaces[blendSlot].DataIndex];
        if(support!=(side==0?definition.Surface0Tag:definition.Surface1Tag))return AlgorithmStatus.Unsupported;
        var owns=EnsureCommandScratch();var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var fields=node.Fields;var chart=FindNodeByIndex(document.Nodes,fields[9].Pointer);
            var start=FindNodeByIndex(document.Nodes,fields[10].Pointer);var end=FindNodeByIndex(document.Nodes,fields[11].Pointer);
            if(chart==null||start==null||end==null)return AlgorithmStatus.InvalidInput;
            if(chart.VariableLength<2||chart.VariableLength>1024)return AlgorithmStatus.Unsupported;
            var positions=NestedWorkspace<double>(chart.VariableLength*3);
            if(positions.IsEmpty)return AlgorithmStatus.WorkspaceTooSmall;
            if(!TryReadChartNode(chart,positions,out var count,out var baseParameter,out var baseScale,out var chordal,out var angular,
                out var error0,out var hasError0,out var error1,out var hasError1))return AlgorithmStatus.Unsupported;
            Span<double> first=stackalloc double[6],last=stackalloc double[6];
            if(!TryReadLimitNode(start,first,out var startType,out var startTerm,out var startCount)
                ||!TryReadLimitNode(end,last,out var endType,out var endTerm,out var endCount))return AlgorithmStatus.InvalidInput;
            if(startCount!=1||endCount!=1||startTerm!=LimitTermUse.Unset||endTerm!=LimitTermUse.Unset
                ||startType is not (LimitType.Artificial or LimitType.Help)||endType is not (LimitType.Artificial or LimitType.Help))
                return AlgorithmStatus.Unsupported;
            // Terminator continuations need a different map; never shorten an
            // imported contact silently to the available chart anchors.
            if(!first[..3].SequenceEqual(positions[..3])||!last[..3].SequenceEqual(positions.Slice(3*(count-1),3)))
                return AlgorithmStatus.Unsupported;
            var dataNode=FindNodeByIndex(document.Nodes,fields[^1].Pointer);
            if(dataNode==null&&fields[^1].Pointer!=0)return AlgorithmStatus.InvalidInput;
            var uvType=IntersectionUvType.None;var uvCount=0;Span<double> uv=default;
            if(dataNode!=null)
            {
                if(dataNode.VariableLength<0||dataNode.VariableLength>2*count)return AlgorithmStatus.Unsupported;
                uv=NestedWorkspace<double>(dataNode.VariableLength);
                if(uv.Length<dataNode.VariableLength)return AlgorithmStatus.WorkspaceTooSmall;
                if(!TryReadIntersectionDataNode(dataNode,uv,out uvType,out uvCount,2*count))return AlgorithmStatus.InvalidInput;
                if(uvType!=IntersectionUvType.None&&uvType!=(reversed?IntersectionUvType.Second:IntersectionUvType.First))
                    return AlgorithmStatus.Unsupported;
                if(uvCount!=ExpectedUvValueCount(uvType,count,0))return AlgorithmStatus.InvalidInput;
            }
            var error=BindBlendContactChart(blend,side,MemoryMarshal.Cast<double,KernelVector3>(positions[..(3*count)]),
                baseParameter,baseScale,XtSense(fields[6].Character),out var result);
            if(error!=0)return error==ParasolidConstants.PK_ERROR_memory_full?AlgorithmStatus.WorkspaceTooSmall:AlgorithmStatus.InvalidInput;
            ref var data=ref ICurveDataPool[Curves[GetCurveSlotByTag(result)].DataIndex];
            data.BlendContactReversed=reversed?(byte)1:(byte)0;
            data.StartLimit.Type=startType;data.EndLimit.Type=endType;data.ChordalError=chordal;data.AngularError=angular;
            data.ParameterError=reversed?error1:error0;data.ParameterErrorProvided=reversed?hasError1:hasError0;
            data.ParameterError1=reversed?error0:error1;data.ParameterError1Provided=reversed?hasError0:hasError1;
            data.SourceSchema=document.Schema.SchemaNumber;
            if(fields.Length==14&&fields[12].Kind==XtFieldKind.Real){data.Scale=fields[12].Real;data.ScaleProvided=1;}
            if(uvType!=IntersectionUvType.None)
            {
                var payload=DereferenceBlock(data.UvValueBlock);
                if(payload==null||data.UvValueCount!=uvCount)return AlgorithmStatus.InvalidInput;
                uv[..uvCount].CopyTo(new Span<double>(payload,uvCount));
            }
            curve=result;return AlgorithmStatus.Success;
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;if(owns)ReleaseCommandScratch(State.Session);}
    }
}

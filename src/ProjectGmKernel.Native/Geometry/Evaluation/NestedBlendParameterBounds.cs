using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    // The child station is the supporting Blend's native u, not an inverse
    // of a sampled XYZ point. Each admitted box has the same contraction and
    // nearest-branch certificate as the spatial tube.
    internal static AlgorithmStatus BoundSpineSupportU(in NestedBlendProgram program,BlendSpineIndex spine,double first,double last,
        BufferOffset side,Span<IntervalNumber> result)
        =>result.Length==2?BoundSpineSupportParameters(program,spine,first,last,side,result):AlgorithmStatus.InvalidInput;

    // Output is (u, du/dt, v, dv/dt) in the actual supporting native chart.
    internal static AlgorithmStatus BoundSpineSupportUv(in NestedBlendProgram program,BlendSpineIndex spine,double first,double last,
        BufferOffset side,Span<IntervalNumber> result)
        =>result.Length==4?BoundSpineSupportParameters(program,spine,first,last,side,result):AlgorithmStatus.InvalidInput;

    private static AlgorithmStatus BoundSpineSupportParameters(in NestedBlendProgram program,BlendSpineIndex spine,double first,double last,
        BufferOffset side,Span<IntervalNumber> result)
    {
        if(spine<0||spine>=program.Spines.Length||side is <0 or >1||last<first)return AlgorithmStatus.InvalidInput;
        var definition=program.Spines[spine];var support=program.Surfaces[side==0?definition.First:definition.Second];
        if(SpineParameterMap.Explicit(definition)||!support.IsBlend&&!support.IsBSpline
            ||support.IsBlend&&SpineParameterMap.Periodic(program.Spines[support.Spine]))return AlgorithmStatus.Unsupported;
        var charts=Chart(program,spine);if(first<charts[0].Parameter||last>charts[^1].Parameter)return AlgorithmStatus.InvalidInput;
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[64];Span<BufferCount> depths=stackalloc BufferCount[64];
        Span<IntervalNumber> output=stackalloc IntervalNumber[9],parameter=stackalloc IntervalNumber[result.Length];
        result.Fill(new(double.PositiveInfinity,double.NegativeInfinity));var covered=first;var visited=0;
        for(var chart=0;chart<charts.Length-1;chart++)
        {
            var lo=Math.Max(first,charts[chart].Parameter);var hi=Math.Min(last,charts[chart+1].Parameter);if(hi<lo||hi==lo&&first!=last)continue;
            if(lo>covered)return AlgorithmStatus.NotConverged;covered=hi;pending[0]=new(lo,hi,0,0);depths[0]=0;var count=1;
            while(count>0)
            {
                if(++visited>8192)return AlgorithmStatus.NotConverged;var index=--count;var box=pending[index];var depth=depths[index];
                var status=BoundSegment(program,-1,spine,box,output,supportParameter:parameter,parameterSide:side);
                if(status==AlgorithmStatus.Success)
                {for(var k=0;k<result.Length;k++)result[k]=IntervalNumber.Hull(result[k],parameter[k]);continue;}
                if(status!=AlgorithmStatus.NotConverged)return status;
                if(depth>=28||count+2>pending.Length)return AlgorithmStatus.NotConverged;
                var middle=box.U0+(box.U1-box.U0)*.5;if(!(middle>box.U0&&middle<box.U1))return AlgorithmStatus.NotConverged;
                pending[count]=new(box.U0,middle,0,0);depths[count++]=depth+1;
                pending[count]=new(middle,box.U1,0,0);depths[count++]=depth+1;
            }
        }
        if(covered!=last)return AlgorithmStatus.NotConverged;
        foreach(var value in result)if(!value.Finite)return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus BoundSpineSupportSection(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<IntervalDual> variables,BufferOffset child,in BlendSurfaceInstruction support,ReadOnlySpan<IntervalSplineContact> cache,
        out IntervalDual v)
    {
        v=default;
        var status=IntervalNormal(program,support.First,nodes,child,variables,out _,out var n0,cache);if(status!=AlgorithmStatus.Success)return status;
        status=IntervalNormal(program,support.Second,nodes,child,variables,out _,out var n1,cache);if(status!=AlgorithmStatus.Success)return status;
        var radius=Math.Abs(support.Range0);if(!(radius>0))return AlgorithmStatus.Unsupported;
        var start=n1*(-support.Range1/radius);var end=n0*(-support.Range0/radius);if(support.ReverseSection)(start,end)=(end,start);
        var cosine=IntervalVector.Dot(start,end);var transverse=end-start*cosine;var sine=transverse.Norm();
        if(!(sine.Value.Lower>0))return AlgorithmStatus.NotConverged;
        var offset=IntervalPoint(variables,0)-IntervalPoint(variables,nodes[child].Offset);
        var longitudinal=IntervalVector.Dot(offset,start);var lateral=IntervalVector.Dot(offset,transverse/sine);
        status=SupportingSectionAngle(longitudinal,lateral,out var angle);if(status!=AlgorithmStatus.Success)return status;
        v=angle/IntervalDual.Atan2(sine,cosine);
        return v.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    // Two strict half-planes cover the native [0,theta] branch, theta<pi.
    // x>0 admits the v=0 end; y>0 admits x=0 and the second quadrant without
    // crossing atan2's negative-x branch cut or dividing by an interval at 0.
    internal static AlgorithmStatus SupportingSectionAngle(IntervalDual longitudinal,IntervalDual lateral,out IntervalDual angle)
    {
        angle=default;var vertical=!(longitudinal.Value.Lower>0);
        if(vertical&&!(lateral.Value.Lower>0))return AlgorithmStatus.NotConverged;
        var ratio=vertical?longitudinal/lateral:lateral/longitudinal;if(!ratio.Finite)return AlgorithmStatus.NotConverged;
        angle=new(new(Math.BitDecrement(Math.BitDecrement(Math.Atan(ratio.Value.Lower))),
            Math.BitIncrement(Math.BitIncrement(Math.Atan(ratio.Value.Upper)))),ratio.Derivative/(1+IntervalNumber.Square(ratio.Value)));
        if(vertical)angle=new IntervalDual(new(Math.BitDecrement(Math.PI*.5),Math.BitIncrement(Math.PI*.5)),0)-angle;
        return angle.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }
}

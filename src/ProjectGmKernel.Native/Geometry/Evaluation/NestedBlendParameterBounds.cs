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
    {
        if(spine<0||spine>=program.Spines.Length||side is <0 or >1||last<first||result.Length!=2)return AlgorithmStatus.InvalidInput;
        var definition=program.Spines[spine];var support=program.Surfaces[side==0?definition.First:definition.Second];
        if(SpineParameterMap.Explicit(definition)||!support.IsBlend||SpineParameterMap.Periodic(program.Spines[support.Spine]))return AlgorithmStatus.Unsupported;
        var charts=Chart(program,spine);if(first<charts[0].Parameter||last>charts[^1].Parameter)return AlgorithmStatus.InvalidInput;
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[64];Span<BufferCount> depths=stackalloc BufferCount[64];
        Span<IntervalNumber> output=stackalloc IntervalNumber[9],parameter=stackalloc IntervalNumber[2];
        var hull=new IntervalNumber(double.PositiveInfinity,double.NegativeInfinity);var derivative=hull;var covered=first;var visited=0;
        for(var chart=0;chart<charts.Length-1;chart++)
        {
            var lo=Math.Max(first,charts[chart].Parameter);var hi=Math.Min(last,charts[chart+1].Parameter);if(hi<lo||hi==lo&&first!=last)continue;
            if(lo>covered)return AlgorithmStatus.NotConverged;covered=hi;pending[0]=new(lo,hi,0,0);depths[0]=0;var count=1;
            while(count>0)
            {
                if(++visited>8192)return AlgorithmStatus.NotConverged;var index=--count;var box=pending[index];var depth=depths[index];
                var status=BoundSegment(program,-1,spine,box,output,supportParameter:parameter,parameterSide:side);
                if(status==AlgorithmStatus.Success){hull=IntervalNumber.Hull(hull,parameter[0]);derivative=IntervalNumber.Hull(derivative,parameter[1]);continue;}
                if(status!=AlgorithmStatus.NotConverged)return status;
                if(depth>=28||count+2>pending.Length)return AlgorithmStatus.NotConverged;
                var middle=box.U0+(box.U1-box.U0)*.5;if(!(middle>box.U0&&middle<box.U1))return AlgorithmStatus.NotConverged;
                pending[count]=new(box.U0,middle,0,0);depths[count++]=depth+1;
                pending[count]=new(middle,box.U1,0,0);depths[count++]=depth+1;
            }
        }
        if(covered!=last||!hull.Finite||!derivative.Finite)return AlgorithmStatus.NotConverged;
        result[0]=hull;result[1]=derivative;return AlgorithmStatus.Success;
    }
}

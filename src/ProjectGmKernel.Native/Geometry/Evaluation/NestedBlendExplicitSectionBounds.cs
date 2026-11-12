using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
namespace ProjectGmKernel.Native.Geometry.Evaluation;
internal static partial class JointNestedBlendEvaluation
{
    internal static bool CanBoundExplicitSection(in NestedBlendProgram program,BlendSurfaceIndex surface)
    {
        var root=program.Surfaces[surface];if(!root.IsBlend||!SpineParameterMap.Explicit(program.Spines[root.Spine]))return false;
        var first=program.Surfaces[root.First];var second=program.Surfaces[root.Second];
        return !first.IsBlend&&!first.IsBSpline&&!second.IsBlend&&!second.IsBSpline;
    }
    // The caller carries the complete successful parent-cell surface proof.
    // This computes the same native position and its one free derivative;
    // it cannot be used to certify an unrelated two-dimensional cell.
    internal static AlgorithmStatus BoundExplicitSection(in NestedBlendProgram program,BlendSurfaceIndex surface,bool constantU,double at,IntervalNumber free,Span<IntervalNumber> output)
    {
        if(output.Length<9)return AlgorithmStatus.WorkspaceTooSmall;
        var root=program.Surfaces[surface];var parameter=constantU?new IntervalNumber(at):free;var charts=Chart(program,root.Spine);var first=true;
        Span<IntervalNumber> local=stackalloc IntervalNumber[9];
        for(var i=0;i<charts.Length-1;i++)
        {
            var lo=i==0?parameter.Lower:Math.Max(parameter.Lower,charts[i].Parameter);var hi=i==charts.Length-2?parameter.Upper:Math.Min(parameter.Upper,charts[i+1].Parameter);
            if(lo>hi||lo==hi&&parameter.Lower!=parameter.Upper)continue;
            var status=BoundExplicitSectionCell(program,surface,constantU,at,free,new(lo,hi),local);if(status!=AlgorithmStatus.Success)return status;
            for(var k=0;k<9;k++)output[k]=first?local[k]:IntervalNumber.Hull(output[k],local[k]);first=false;
        }
        return first?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
    }
    private static AlgorithmStatus BoundExplicitSectionCell(in NestedBlendProgram program,BlendSurfaceIndex surface,bool constantU,double at,IntervalNumber free,IntervalNumber parameter,Span<IntervalNumber> output)
    {
        var root=program.Surfaces[surface];var spine=root.Spine;
        var status=SpineParameterMap.Interval(program,program.Spines[spine],new(parameter,1),out var center,out _);if(status!=AlgorithmStatus.Success)return status;
        Span<Occurrence> nodes=stackalloc Occurrence[1];nodes[0]=new(){Spine=spine,Offset=0,Parent=-1,FirstChild=-1,SecondChild=-1,FirstUv=-1,SecondUv=-1};
        Span<IntervalDual> variables=stackalloc IntervalDual[3]{center.X,center.Y,center.Z};
        if(constantU){variables[0]=center.X.Value;variables[1]=center.Y.Value;variables[2]=center.Z.Value;}
        var v=constantU?new IntervalDual(free,1):new IntervalDual(at,0);
        status=IntervalFrame(program,surface,nodes,variables,v,out var frame);if(status!=AlgorithmStatus.Success)return status;
        output[0]=frame.X.Value;output[1]=frame.Y.Value;output[2]=frame.Z.Value;output[3]=frame.X.Derivative;output[4]=frame.Y.Derivative;output[5]=frame.Z.Derivative;output.Slice(6,3).Clear();return AlgorithmStatus.Success;
    }
}

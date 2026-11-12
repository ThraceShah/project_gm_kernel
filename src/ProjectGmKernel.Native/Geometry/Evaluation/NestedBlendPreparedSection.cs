using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal struct PreparedNativeSection
{internal IntervalVector Center;internal RollingSectionEvaluation.PreparedSection Section;internal double Radius;}
internal static partial class JointNestedBlendEvaluation
{
    // Storage belongs to one scalar Find, with the same immutable source and
    // fixed native u. The caller must carry the complete covering cell proof.
    internal static AlgorithmStatus PrepareExplicitSection(in NestedBlendProgram program,BlendSurfaceIndex surface,double at,
        Span<PreparedNativeSection> output,out BufferCount count)
    {
        count=0;var root=program.Surfaces[surface];var charts=Chart(program,root.Spine);
        Span<Occurrence> nodes=stackalloc Occurrence[1];nodes[0]=new(){Spine=root.Spine,Offset=0,Parent=-1,FirstChild=-1,SecondChild=-1,FirstUv=-1,SecondUv=-1};
        Span<IntervalDual> variables=stackalloc IntervalDual[3];
        for(var i=0;i<charts.Length-1;i++)
        {
            var lo=i==0?at:Math.Max(at,charts[i].Parameter);var hi=i==charts.Length-2?at:Math.Min(at,charts[i+1].Parameter);if(lo>hi)continue;
            if(count==output.Length)return AlgorithmStatus.WorkspaceTooSmall;
            var status=SpineParameterMap.Interval(program,program.Spines[root.Spine],new(new IntervalNumber(at),1),out var center,out _);if(status!=AlgorithmStatus.Success)return status;
            variables[0]=center.X.Value;variables[1]=center.Y.Value;variables[2]=center.Z.Value;
            status=IntervalNormal(program,root.First,nodes,0,variables,out _,out var n0);if(status!=AlgorithmStatus.Success)return status;
            status=IntervalNormal(program,root.Second,nodes,0,variables,out _,out var n1);if(status!=AlgorithmStatus.Success)return status;
            var radius=Math.Abs(root.Range0);var x=n1*(-root.Range1/radius);var end=n0*(-root.Range0/radius);if(root.ReverseSection)(x,end)=(end,x);
            status=RollingSectionEvaluation.Prepare(x,end,out var section);if(status!=AlgorithmStatus.Success)return status;
            output[count++]=new(){Center=IntervalPoint(variables,0),Radius=radius,Section=section};
        }
        return count==0?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
    }
    internal static AlgorithmStatus BoundPreparedSection(ReadOnlySpan<PreparedNativeSection> sections,IntervalNumber free,Span<IntervalNumber> output)
    {
        if(output.Length<9)return AlgorithmStatus.WorkspaceTooSmall;
        var first=true;Span<IntervalNumber> local=stackalloc IntervalNumber[9];
        foreach(ref readonly var section in sections)
        {
            var status=RollingSectionEvaluation.Bound(section.Section,new(free,1),out var radial);if(status!=AlgorithmStatus.Success)return status;
            var point=section.Center+radial*section.Radius;if(!point.Finite)return AlgorithmStatus.NotConverged;
            local[0]=point.X.Value;local[1]=point.Y.Value;local[2]=point.Z.Value;local[3]=point.X.Derivative;local[4]=point.Y.Derivative;local[5]=point.Z.Derivative;local.Slice(6,3).Clear();
            for(var i=0;i<9;i++)output[i]=first?local[i]:IntervalNumber.Hull(output[i],local[i]);first=false;
        }
        return first?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
    }
}

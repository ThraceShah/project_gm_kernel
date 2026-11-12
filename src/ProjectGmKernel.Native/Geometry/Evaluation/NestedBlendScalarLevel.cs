using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
namespace ProjectGmKernel.Native.Geometry.Evaluation;
internal static partial class JointNestedBlendEvaluation
{
    internal static NativeScalarLevel NativeSectionLevel(in NestedBlendProgram program,BlendSurfaceIndex surface,double at,
        in AnalyticSurface target,IntervalNumber free)
    {
        var root=program.Surfaces[surface];if(Chart(program,root.Spine).Length<2)return default;
        var status=SpineParameterMap.Interval(program,program.Spines[root.Spine],new(new IntervalNumber(at),1),out var center,out _);if(status!=AlgorithmStatus.Success)return default;
        Span<Occurrence> nodes=stackalloc Occurrence[1];nodes[0]=new(){Spine=root.Spine,Offset=0,Parent=-1,FirstChild=-1,SecondChild=-1,FirstUv=-1,SecondUv=-1};
        Span<IntervalDual> variables=stackalloc IntervalDual[3]{center.X.Value,center.Y.Value,center.Z.Value};
        status=IntervalNormal(program,root.First,nodes,0,variables,out _,out var n0);if(status!=AlgorithmStatus.Success)return default;
        status=IntervalNormal(program,root.Second,nodes,0,variables,out _,out var n1);if(status!=AlgorithmStatus.Success)return default;
        var radius=Math.Abs(root.Range0);var x=n1*(-root.Range1/radius);var end=n0*(-root.Range0/radius);if(root.ReverseSection)(x,end)=(end,x);
        var cosine=IntervalVector.Dot(x,end);var q=1-cosine;if(!(q.Value.Lower>.125))return default;
        var delta=end-x*cosine;var sine=delta.Norm();var transverse=delta/sine;var angle=IntervalDual.Atan2(sine,cosine);
        if(!transverse.Finite||!angle.Finite)return default;
        return NativeScalarLevel.Create(IntervalPoint(variables,0),x,transverse,angle.Value,radius,target,free);
    }
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    // These are the support coordinates already solved jointly with the
    // rolling spine. A bounded global inverse would clamp an extended spline
    // foot back onto its original knot rectangle and lose this information.
    internal static AlgorithmStatus SplineContactParameters(in NestedBlendProgram program,BlendSurfaceIndex surface,
        double parameter,BufferOffset side,out double u,out double v)
    {
        u=v=0;if(side is <0 or >1)return AlgorithmStatus.InvalidInput;
        var root=program.Surfaces[surface];if(!root.IsBlend)return AlgorithmStatus.Unsupported;
        var spine=program.Spines[root.Spine];var support=program.Surfaces[side==0?spine.First:spine.Second];
        if(!support.IsBSpline)return AlgorithmStatus.Unsupported;
        Span<double> contacts=stackalloc double[4];Span<KernelVector3> point=stackalloc KernelVector3[1];SurfaceDerivativeLayout.TryCreate(0,0,out var layout);
        var status=EvaluateCore(program,root.Spine,-1,parameter,0,layout,point,false,default,out _,out _,contacts);if(status!=AlgorithmStatus.Success)return status;
        u=contacts[2*side];v=contacts[2*side+1];return double.IsFinite(u)&&double.IsFinite(v)?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }
}

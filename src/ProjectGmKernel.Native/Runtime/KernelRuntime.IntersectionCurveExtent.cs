using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus IntersectFiniteIntersectionCurvePlane(CurveTag curve,double first,double last,in AnalyticSurface plane,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var compiler=new NestedProgramCompiler();if(!compiler.Ready)return AlgorithmStatus.WorkspaceTooSmall;
            var status=compiler.Spine(curve,out var spine);if(status!=AlgorithmStatus.Success)return status;
            var program=compiler.View;var definition=program.Spines[spine];
            var tolerance=8*JointNestedBlendEvaluation.ResidualAccuracy(program,spine,program.Charts[definition.ChartOffset].Position);
            var patch=new SurfaceIntersectionPatch(program,spine,first,last);
            status=SurfaceIsoPlaneRoots.Find(patch,plane,first,last,output,ref count,tolerance,strictSeparation:true);
            if(status==AlgorithmStatus.Success)OperationIntersection.Sort(output[..count]);return status;
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }

    // Reuse the implicit curve's actual CHART map and support constraints.
    // A certified finite curve tube can reduce a downstream search; CHART
    // anchors, chord errors or sampled extrema cannot substitute for it.
    internal static AlgorithmStatus CertifiedIntersectionCurveExtent(CurveTag curve,double first,double last,out IntervalBox3 extent)
    {
        extent=default;if(!(last>first))return AlgorithmStatus.InvalidInput;
        var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var compiler=new NestedProgramCompiler();if(!compiler.Ready)return AlgorithmStatus.WorkspaceTooSmall;
            var status=compiler.Spine(curve,out var spine);if(status!=AlgorithmStatus.Success)return status;
            return BlendExtent(compiler.View,spine,new(first,last,0,0),out extent,spineOnly:true);
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }
}

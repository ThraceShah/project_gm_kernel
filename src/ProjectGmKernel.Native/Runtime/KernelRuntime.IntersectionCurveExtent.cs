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
            var charts=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);var covered=first;
            var record=Curves[GetCurveSlotByTag(curve)];
            for(var chart=0;chart<charts.Length-1;chart++)
            {
                var lo=Math.Max(first,charts[chart].Parameter);var hi=Math.Min(last,charts[chart+1].Parameter);if(!(hi>lo))continue;
                if(lo>covered)return AlgorithmStatus.NotConverged;covered=hi;
                // Every native CHART interval has its own gauge. Isolate in
                // that interval instead of repeatedly bounding a union of
                // different parameter maps as one scalar branch.
                var patch=new SurfaceIntersectionPatch(program,spine,lo,hi);patch=new(patch,curve,record.Header.Generation,record.Sense);
                status=SurfaceIsoPlaneRoots.Find(patch,plane,lo,hi,output,ref count,tolerance,strictSeparation:true);if(status!=AlgorithmStatus.Success)return status;
            }
            if(covered!=last){SurfaceIsoPlaneRoots.LastFailure=SurfaceIsoPlaneRoots.IsolationFailure.DomainCoverage;SurfaceIsoPlaneRoots.LastUnresolvedInterval=new(covered,last);return AlgorithmStatus.NotConverged;}
            if(status==AlgorithmStatus.Success)OperationIntersection.Sort(output[..count]);return status;
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }

    // Reuse the implicit curve's actual CHART map and support constraints.
    // A certified finite curve tube can reduce a downstream search; CHART
    // anchors, chord errors or sampled extrema cannot substitute for it.
    internal static AlgorithmStatus CertifiedIntersectionCurveExtent(CurveTag curve,double first,double last,out IntervalBox3 extent)
    {
        extent=default;if(!(last>=first))return AlgorithmStatus.InvalidInput;
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

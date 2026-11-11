using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus IntersectFiniteIntersectionCurvePlane(CurveTag curve,double first,double last,in AnalyticSurface plane,
        Span<OperationIntersectionPoint> output,out BufferCount count,Span<ScalarRootIsolation> isolation=default,Span<double> spatialErrors=default)
    {
        count=0;var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var compiler=new NestedProgramCompiler();if(!compiler.Ready)return AlgorithmStatus.WorkspaceTooSmall;
            var status=compiler.Spine(curve,out var spine);if(status!=AlgorithmStatus.Success)return status;
            var program=compiler.View;var definition=program.Spines[spine];
            var rootUse=RootIsolationUse.SpatialCurve;
            var a=program.Surfaces[definition.First];var b=program.Surfaces[definition.Second];
            if(a.IsBlend!=b.IsBlend)
            {
                var child=program.Spines[(a.IsBlend?a:b).Spine];var kind=child.Curve.Kind;
                if(kind==CurveClass.Line)rootUse=RootIsolationUse.SpatialLineBlend;
                else if(kind is CurveClass.Circle or CurveClass.Ellipse)rootUse=RootIsolationUse.SpatialConicBlend;
                else if(!SpineParameterMap.Explicit(child)&&!program.Surfaces[child.First].IsBlend&&!program.Surfaces[child.First].IsBSpline
                    &&!program.Surfaces[child.Second].IsBlend&&!program.Surfaces[child.Second].IsBSpline
                    &&program.Surfaces[child.First].Analytic.Kind==SurfaceClass.Plane&&program.Surfaces[child.Second].Analytic.Kind==SurfaceClass.Plane)
                    rootUse=RootIsolationUse.SpatialLineBlend;
                else rootUse=RootIsolationUse.SpatialImplicitBlend;
            }
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
                status=SurfaceIsoPlaneRoots.Find(patch,plane,lo,hi,output,ref count,tolerance,strictSeparation:true,isolation:isolation,use:rootUse);if(status!=AlgorithmStatus.Success)return status;
            }
            if(covered!=last){SurfaceIsoPlaneRoots.LastFailure=SurfaceIsoPlaneRoots.IsolationFailure.DomainCoverage;SurfaceIsoPlaneRoots.LastUnresolvedInterval=new(covered,last);return AlgorithmStatus.NotConverged;}
            if(isolation.IsEmpty){OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;}
            // Keep each root's native parameter box tied to its point even
            // when discoveries from multiple CHART cells have to be sorted.
            Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
            if(spatialErrors.Length<count)return AlgorithmStatus.OutputTooSmall;
            for(var i=0;i<count;i++)
            {
                var root=isolation[i].Root;
                status=JointNestedBlendEvaluation.BoundSpine(program,spine,new(root.Lower,root.Upper,0,0),bounds);if(status!=AlgorithmStatus.Success)return status;
                spatialErrors[i]=(new IntervalVector(bounds[0],bounds[1],bounds[2])-new IntervalVector(output[i].Point)).Norm().Value.Upper;
                if(!double.IsFinite(spatialErrors[i]))return AlgorithmStatus.NotConverged;
                for(var j=i;j>0&&output[j].First<output[j-1].First;j--)
                {
                    (output[j],output[j-1])=(output[j-1],output[j]);
                    (isolation[j],isolation[j-1])=(isolation[j-1],isolation[j]);
                    (spatialErrors[j],spatialErrors[j-1])=(spatialErrors[j-1],spatialErrors[j]);
                }
            }
            return AlgorithmStatus.Success;
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

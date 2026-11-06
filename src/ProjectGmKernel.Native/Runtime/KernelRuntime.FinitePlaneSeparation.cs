using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool RegularPlaneFacesSeparated(in RegularBoundaryView view,BufferOffset first,BufferOffset second,double tolerance)
    {
        if(Surfaces[GetSurfaceSlotByTag(view.Faces[first].Surface)].Class!=SurfaceClass.Plane||Surfaces[GetSurfaceSlotByTag(view.Faces[second].Surface)].Class!=SurfaceClass.Plane)return false;
        for(var attempt=0;attempt<2;attempt++)
        {
            var face=view.Faces[attempt==0?first:second];var other=view.Faces[attempt==0?second:first];
            if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(other.Surface)],out var plane)||face.LoopCount==0)continue;
            var side=0;var proved=true;
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=view.Loops[face.FirstLoop+l];
                if(!PlanarLoopSeparatedFromPlane(face.Surface,view.Edges,view.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount),plane,tolerance,out var contribution)||side!=0&&contribution!=side){proved=false;break;}
                side=contribution;
            }
            if(proved&&side!=0)return true;
        }
        return false;
    }

    // On a bounded planar region a linear functional attains its extrema on
    // the complete boundary, including holes. No normal-angle comparison
    // decides separation; a far-away intersection of the infinite supports
    // remains irrelevant only when every boundary interval proves this sign.
    internal static bool PlanarLoopSeparatedFromPlane(SurfTag source,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,
        in AnalyticSurface plane,double tolerance,out KernelSense side)
    {
        side=0;if(loop.IsEmpty||Surfaces[GetSurfaceSlotByTag(source)].Class!=SurfaceClass.Plane||plane.Kind!=SurfaceClass.Plane)return false;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[68],restricted=stackalloc IntervalNumber[68];
        foreach(ref readonly var coedge in loop)
        {
            var edge=edges[coedge.Edge];var interval=new PK_INTERVAL_s();interval.value[0]=edge.Start;interval.value[1]=edge.End;
            if(ResolveIntersectionCurve(edge.Curve,interval,out var span)!=0)return false;var count=GeneralCurvePieceCount(span);if(count<=0)return false;
            for(var piece=0;piece<count;piece++)
            {
                var status=GeneralCurvePiece(span,piece,poles,out var degree,out var domain,out var lo,out var hi);if(status!=AlgorithmStatus.Success)return false;if(!(hi>lo))continue;
                status=BezierSurfaceBounds.Restrict(new(poles[..(4*(degree+1))],degree,0,domain),new(lo,hi,0,1),restricted);if(status!=AlgorithmStatus.Success)return false;
                for(var p=0;p<=degree;p++)
                {
                    var at=4*p;var w=restricted[at+3];if(w.Lower<=0)return false;
                    var distance=((restricted[at]-plane.Origin.X*w)*plane.Axis.X+(restricted[at+1]-plane.Origin.Y*w)*plane.Axis.Y+(restricted[at+2]-plane.Origin.Z*w)*plane.Axis.Z)/w;
                    var sign=distance.Lower>tolerance?1:distance.Upper< -tolerance?-1:0;
                    if(sign==0||side!=0&&side!=sign)return false;side=sign;
                }
            }
        }
        return side!=0;
    }
}

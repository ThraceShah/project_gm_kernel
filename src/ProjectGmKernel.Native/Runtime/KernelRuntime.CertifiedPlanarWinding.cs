using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Use the same oriented winding as PlanarLoopWinding, with isolated
    // native curve roots. The cut root and ray origin must belong to the
    // same component of the complement of every actual finite face loop.
    private static AlgorithmStatus CertifiedPlanarRootOwnership(in RegularBoundaryView input,BufferOffset face,in SurfaceRecord surface,
        in KernelVector3 point,in IntervalBox3 root,double precision,out bool inside)
    {
        inside=false;
        var record=input.Faces[face];
        if(record.LoopCount==0||!TryPrepareSurface(surface,out var plane)||plane.Kind!=SurfaceClass.Plane)return AlgorithmStatus.Unsupported;
        for(var attempt=0;attempt<6;attempt++)
        {
            SheetWindingDirection(attempt,false,out var du,out var dv);
            var direction=Add(Scale(plane.X,du),Scale(plane.Y,dv));
            if(!ProfileNormalize(ref direction))return AlgorithmStatus.InvalidInput;
            var cutter=new AnalyticSurface(SurfaceClass.Plane,point,ProfileCross(plane.Axis,direction),plane.Axis);
            var forward=new IntervalVector(direction);var normal=new IntervalVector(plane.Axis);
            var transverse=IntervalVector.Cross(forward,new IntervalVector(cutter.Axis));
            var orientation=IntervalVector.Dot(normal,transverse).Value;
            if(!(orientation.Lower>0))continue;
            // The ray origin is the common point of the face, cutter and
            // along=0 planes. Its enclosure accounts for the fact that the
            // represented root point need not lie exactly on the face.
            var p=new IntervalVector(point);
            var position=p+transverse*(IntervalVector.Dot(new IntervalVector(plane.Origin)-p,normal).Value/orientation);
            if(!position.Finite)continue;
            var domain=new IntervalBox3(Math.Min(root.XLo,position.X.Value.Lower),Math.Max(root.XHi,position.X.Value.Upper),
                Math.Min(root.YLo,position.Y.Value.Lower),Math.Max(root.YHi,position.Y.Value.Upper),
                Math.Min(root.ZLo,position.Z.Value.Lower),Math.Max(root.ZHi,position.Z.Value.Upper));
            var status=CertifiedPlanarWinding(input,record,plane,cutter,forward,domain,precision,out var winding);
            if(status==AlgorithmStatus.Success){inside=winding!=0;return status;}
            if(status!=AlgorithmStatus.NotConverged)return status;
        }
        return AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus CertifiedPlanarWinding(in RegularBoundaryView input,in ModelingFace face,in AnalyticSurface plane,
        in AnalyticSurface cutter,in IntervalVector forward,in IntervalBox3 domain,double precision,out BufferCount winding)
    {
        winding=0;
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[4];
        Span<ScalarRootIsolation> intervals=stackalloc ScalarRootIsolation[4];
        Span<double> errors=stackalloc double[4];
        var origin=new IntervalVector(cutter.Origin);var scale=forward.Norm().Value;
        for(var l=0;l<face.LoopCount;l++)
        {
            var loop=input.Loops[face.FirstLoop+l];
            foreach(var coedge in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
            {
                var edge=input.Edges[coedge.Edge];var curve=Curves[GetCurveSlotByTag(edge.Curve)];
                if(!TryReadExplicitSpineCurve(curve,out var explicitCurve)||!ExplicitCurveExactlyOnPlane(explicitCurve,plane))
                    return AlgorithmStatus.Unsupported;
                var status=SeparatePlanarBoundaryFromRoot(explicitCurve,edge.Start,edge.End,domain,precision);
                if(status!=AlgorithmStatus.Success)return status;
                status=IsolateFiniteExplicitLevelSet(edge,cutter,roots,intervals,errors,out var n);
                if(status!=AlgorithmStatus.Success)return status;
                var increasing=(coedge.Forward!=0)==(LocalSenseSign(curve.Sense)>0);
                for(var r=0;r<n;r++)
                {
                    var error=(new IntervalNumber(errors[r])*scale).Upper;
                    var along=IntervalVector.Dot(new IntervalVector(roots[r].Point)-origin,forward).Value+new IntervalNumber(-error,error);
                    if(along.Upper<0)continue;
                    var root=intervals[r];
                    if(!(along.Lower>0)||!root.Certified||root.Root.Lower<=edge.Start||root.Root.Upper>=edge.End)return AlgorithmStatus.NotConverged;
                    winding+=increasing==root.Increasing?1:-1;
                }
            }
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus SeparatePlanarBoundaryFromRoot(in ParametricSpineCurve curve,double first,double last,in IntervalBox3 root,double precision)
    {
        Span<IntervalNumber> pending=stackalloc IntervalNumber[32];var n=1;var visited=0;pending[0]=new(first,last);
        while(n>0)
        {
            var t=pending[--n];
            if(++visited>512)
            {
                ModelingPerformance.BudgetFailure();return AlgorithmStatus.NotConverged;
            }
            var status=SpineParameterMap.Interval(curve,new(t,0),out var box,out _);
            if(status!=AlgorithmStatus.Success)return status;
            if(!box.Finite)return AlgorithmStatus.NotConverged;
            var extent=new IntervalBox3(box.X.Value.Lower,box.X.Value.Upper,box.Y.Value.Lower,box.Y.Value.Upper,box.Z.Value.Lower,box.Z.Value.Upper);
            if(ThirdFaceBoxesDisjoint(extent,root,precision))continue;
            var mid=t.Midpoint;if(!(mid>t.Lower&&mid<t.Upper)||n+2>pending.Length)return AlgorithmStatus.NotConverged;
            pending[n++]=new(t.Lower,mid);pending[n++]=new(mid,t.Upper);
        }
        return AlgorithmStatus.Success;
    }
}

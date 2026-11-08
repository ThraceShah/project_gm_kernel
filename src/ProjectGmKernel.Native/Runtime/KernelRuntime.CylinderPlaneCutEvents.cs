using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool IsCylinderPlaneTriple(in RegularBoundaryView input,in RegularFaceCut cut,BufferOffset third)
    {
        if(cut.First<0||cut.Second<0)return false;
        var a=Surfaces[GetSurfaceSlotByTag(input.Faces[cut.First].Surface)].Class;
        var b=Surfaces[GetSurfaceSlotByTag(input.Faces[cut.Second].Surface)].Class;
        var c=Surfaces[GetSurfaceSlotByTag(input.Faces[third].Surface)].Class;
        return a==SurfaceClass.Cylinder&&b==SurfaceClass.Plane&&c==SurfaceClass.Plane
            ||a==SurfaceClass.Plane&&b==SurfaceClass.Cylinder&&c==SurfaceClass.Plane
            ||a==SurfaceClass.Plane&&b==SurfaceClass.Plane&&c==SurfaceClass.Cylinder;
    }

    private static int JoinCylinderPlaneCutRoots(in RegularBoundaryView input,BufferOffset third,scoped ReadOnlySpan<RegularFaceCut> cuts,
        BufferOffset current,in OperationIntersectionPoint source,in ScalarRootIsolation sourceProof,double sourceError,
        ref SheetArrangement model,out bool joined,out AlgorithmStatus proofStatus)
    {
        joined=false;proofStatus=AlgorithmStatus.Unsupported;
        if(!sourceProof.Certified||!double.IsFinite(sourceError)||sourceError<0)return 0;
        var pair=cuts[current];
        Span<BufferOffset> occurrences=stackalloc BufferOffset[3]{pair.First,pair.Second,third};
        BufferOffset round=-1,first=-1,second=-1;
        foreach(var f in occurrences)
        {
            if(f<0)return 0;
            var kind=Surfaces[GetSurfaceSlotByTag(input.Faces[f].Surface)].Class;
            if(kind==SurfaceClass.Cylinder&&round<0)round=f;
            else if(kind!=SurfaceClass.Plane)return 0;
            else if(first<0)first=f;
            else second=f;
        }
        if(round<0||second<0)return 0;
        if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(input.Faces[round].Surface)],out var cylinder)
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(input.Faces[first].Surface)],out var a)
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(input.Faces[second].Surface)],out var b))return 0;
        var carrier=Curves[GetCurveSlotByTag(pair.Cut.Curve)];
        if(!TryReadExplicitSpineCurve(carrier,out var known))return 0;
        if(third==round)
        {
            if(known.Kind!=CurveClass.Line||!ExplicitCurveExactlyOnPlane(known,a)||!ExplicitCurveExactlyOnPlane(known,b))return 0;
        }
        else if(!ExplicitCurveExactlyOnCylinder(known,cylinder)||!ExplicitCurveExactlyOnPlane(known,third==first?b:a))return 0;

        Span<BoundaryParameter> anchors=stackalloc BoundaryParameter[3];
        Span<FiniteCurveSurfaceRoot> proofs=stackalloc FiniteCurveSurfaceRoot[3];
        proofStatus=FindCylinderJointLine(input,cuts,first,second,round,a,b,cylinder,source.Point,sourceError,
            out var line,out var lineProof,out anchors[0],out proofs[0]);
        if(proofStatus!=AlgorithmStatus.Success)return 0;
        proofStatus=FindCylinderPlaneJoint(input,cuts,round,first,second,cylinder,a,b,line,lineProof,out anchors[1],out proofs[1]);
        if(proofStatus!=AlgorithmStatus.Success)return 0;
        proofStatus=FindCylinderPlaneJoint(input,cuts,round,second,first,cylinder,b,a,line,lineProof,out anchors[2],out proofs[2]);
        if(proofStatus!=AlgorithmStatus.Success)return 0;
        foreach(ref readonly var anchor in anchors)
            if(anchor.SourceOccurrence==RegularInputEdges+current&&anchor.Carrier==pair.Cut.Curve
                &&anchor.Upper>=sourceProof.Root.Lower&&anchor.Lower<=sourceProof.Root.Upper)
                return AssociateThirdFaceStations(anchors,proofs,ref model,out joined);
        proofStatus=AlgorithmStatus.NotConverged;return 0;
    }

    private static AlgorithmStatus FindCylinderJointLine(in RegularBoundaryView input,ReadOnlySpan<RegularFaceCut> cuts,
        BufferOffset first,BufferOffset second,BufferOffset round,in AnalyticSurface a,in AnalyticSurface b,in AnalyticSurface cylinder,
        in KernelVector3 point,double spatialError,out ParametricSpineCurve line,out ScalarRootIsolation isolation,
        out BoundaryParameter anchor,out FiniteCurveSurfaceRoot proof)
    {
        line=default;isolation=default;anchor=default;proof=default;var found=false;
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[8];
        Span<ScalarRootIsolation> bounds=stackalloc ScalarRootIsolation[8];
        Span<double> errors=stackalloc double[8];
        if(IntervalVector.Cross(new(a.Axis),new(b.Axis)).Norm().Value.Lower<=0)return AlgorithmStatus.Singular;
        for(var i=0;i<cuts.Length;i++)
        {
            ref readonly var cut=ref cuts[i];if(!ThirdCutHasPair(cut,first,second))continue;
            var record=Curves[GetCurveSlotByTag(cut.Cut.Curve)];
            if(!TryReadExplicitSpineCurve(record,out var candidate)||candidate.Kind!=CurveClass.Line
                ||!ExplicitCurveExactlyOnPlane(candidate,a)||!ExplicitCurveExactlyOnPlane(candidate,b))return AlgorithmStatus.Unsupported;
            var projected=ProjectThirdRootToLine(candidate,point,spatialError);
            if(!projected.Finite)return AlgorithmStatus.NotConverged;
            if(projected.Upper<cut.Cut.First||projected.Lower>cut.Cut.Last)continue;
            if(!(projected.Lower>cut.Cut.First&&projected.Upper<cut.Cut.Last))return AlgorithmStatus.NotConverged;
            var edge=new ModelingEdge{Curve=cut.Cut.Curve,Start=cut.Cut.First,End=cut.Cut.Last};
            var status=IsolateFiniteExplicitLevelSet(edge,cylinder,roots,bounds,errors,out var count);
            if(status!=AlgorithmStatus.Success)return status;
            for(var r=0;r<count;r++)
            {
                var station=bounds[r];
                if(!IntersectStationBounds(projected,station.Root).Finite)continue;
                if(found||!station.Certified||!ContainsThirdStation(station.MonotoneDomain,projected)
                    ||station.Root.Lower<=edge.Start||station.Root.Upper>=edge.End)return AlgorithmStatus.NotConverged;
                line=candidate;isolation=station;
                proof=ThirdFiniteRoot(roots[r],station,errors[r]);anchor=ThirdCutAnchor(input,cut,round,i,r,proof);found=true;
            }
        }
        return found?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus FindCylinderPlaneJoint(in RegularBoundaryView input,ReadOnlySpan<RegularFaceCut> cuts,
        BufferOffset round,BufferOffset support,BufferOffset third,in AnalyticSurface cylinder,in AnalyticSurface plane,in AnalyticSurface opposing,
        in ParametricSpineCurve line,in ScalarRootIsolation lineProof,out BoundaryParameter anchor,out FiniteCurveSurfaceRoot proof)
    {
        anchor=default;proof=default;var found=false;
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[8];
        Span<ScalarRootIsolation> bounds=stackalloc ScalarRootIsolation[8];
        Span<double> errors=stackalloc double[8];
        for(var i=0;i<cuts.Length;i++)
        {
            ref readonly var cut=ref cuts[i];if(!ThirdCutHasPair(cut,round,support))continue;
            var record=Curves[GetCurveSlotByTag(cut.Cut.Curve)];
            if(!TryReadExplicitSpineCurve(record,out var curve)||!ExplicitCurveExactlyOnPlane(curve,plane)
                ||!ExplicitCurveExactlyOnCylinder(curve,cylinder))return AlgorithmStatus.Unsupported;
            var edge=new ModelingEdge{Curve=cut.Cut.Curve,Start=cut.Cut.First,End=cut.Cut.Last};
            var status=IsolateFiniteExplicitLevelSet(edge,opposing,roots,bounds,errors,out var count);
            if(status!=AlgorithmStatus.Success)return status;
            for(var r=0;r<count;r++)
            {
                var projected=ProjectThirdRootToLine(line,roots[r].Point,errors[r]);
                if(!projected.Finite)return AlgorithmStatus.NotConverged;
                if(!IntersectStationBounds(projected,lineProof.Root).Finite)continue;
                // The root satisfies the same exact pair equations and the
                // third equation. The common line's monotone branch proves
                // this is its same root, not just an overlapping spatial box.
                var station=bounds[r];
                if(found||!station.Certified||!ContainsThirdStation(lineProof.MonotoneDomain,projected)
                    ||station.Root.Lower<=edge.Start||station.Root.Upper>=edge.End)return AlgorithmStatus.NotConverged;
                proof=ThirdFiniteRoot(roots[r],station,errors[r]);anchor=ThirdCutAnchor(input,cut,third,i,r,proof);found=true;
            }
        }
        return found?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    private static IntervalNumber ProjectThirdRootToLine(in ParametricSpineCurve line,in KernelVector3 point,double error)
    {
        var radius=new IntervalNumber(-error,error);
        var position=new IntervalVector(new IntervalNumber(point.X)+radius,new IntervalNumber(point.Y)+radius,new IntervalNumber(point.Z)+radius);
        var direction=new IntervalVector(line.First);
        return IntervalVector.Dot(position-new IntervalVector(line.Origin),direction).Value/IntervalVector.Dot(direction,direction).Value;
    }

    private static bool ContainsThirdStation(IntervalNumber domain,IntervalNumber station)=>station.Lower>=domain.Lower&&station.Upper<=domain.Upper;
    private static bool ThirdCutHasPair(in RegularFaceCut cut,BufferOffset first,BufferOffset second)
        =>cut.First==first&&cut.Second==second||cut.First==second&&cut.Second==first;
    private static FiniteCurveSurfaceRoot ThirdFiniteRoot(in OperationIntersectionPoint point,in ScalarRootIsolation isolation,double error)
        =>new(){Point=point.Point,Vertex=-1,Kind=point.Kind,Parameter=point.First,Lower=isolation.Root.Lower,Upper=isolation.Root.Upper,SpatialError=error};

    private static bool ExplicitCurveExactlyOnCylinder(in ParametricSpineCurve curve,in AnalyticSurface cylinder)
    {
        var axis=new IntervalVector(cylinder.Axis);
        if((IntervalVector.Dot(axis,axis).Value-1).Magnitude!=0)return false;
        var q=Radial(new IntervalVector(curve.Origin)-new IntervalVector(cylinder.Origin),axis);
        var a=Radial(new IntervalVector(curve.First),axis);
        var radius=new IntervalNumber(cylinder.Radius);var r2=radius*radius;var qq=IntervalVector.Dot(q,q).Value;
        var aa=IntervalVector.Dot(a,a).Value;
        if(curve.Kind==CurveClass.Line)return aa.Magnitude==0&&(qq-r2).Magnitude==0;
        var b=Radial(new IntervalVector(curve.Second),axis);var bb=IntervalVector.Dot(b,b).Value;
        return (qq+aa-r2).Magnitude==0&&(aa-bb).Magnitude==0&&IntervalVector.Dot(a,b).Value.Magnitude==0
            &&IntervalVector.Dot(q,a).Value.Magnitude==0&&IntervalVector.Dot(q,b).Value.Magnitude==0;

        static IntervalVector Radial(IntervalVector value,IntervalVector axis)=>value-axis*IntervalVector.Dot(value,axis);
    }
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool IsBlendPlaneLineTriple(in RegularBoundaryView input,in RegularFaceCut pair,BufferOffset third)
        =>pair.Cut.Construction==CutConstruction.AnalyticPlanePair&&pair.First>=0&&pair.Second>=0
            &&RegularCutSupportClass(input,third) is SurfaceClass.BlendSurface or SurfaceClass.BSurface
            &&RegularCutSupportClass(input,pair.First)==SurfaceClass.Plane&&RegularCutSupportClass(input,pair.Second)==SurfaceClass.Plane
            &&Curves[GetCurveSlotByTag(pair.Cut.Curve)].Class==CurveClass.Line;

    private static SurfaceClass RegularCutSupportClass(in RegularBoundaryView input,BufferOffset face)
        =>Surfaces[GetSurfaceSlotByTag(input.Faces[face].Surface)].Class;

    private static int ReadBlendPlaneLineJoint(in RegularBoundaryView input,BufferOffset third,FaceSlot source,
        scoped ReadOnlySpan<RegularFaceCut> cuts,BufferOffset current,ref ThirdFaceBounds cutBounds,ref ThirdFaceBounds faceBounds,
        ref SheetArrangement model)
    {
        ref readonly var pair=ref cuts[current];ref readonly var cut=ref pair.Cut;
        var carrier=Curves[GetCurveSlotByTag(cut.Curve)];
        var blend=input.Faces[third].Surface;
        LastSheetPartitionStage=SheetPartitionStage.BoundaryIntersections;LastSheetCurve=cut.Curve;LastSheetParameter=cut.First;
        if(!faceBounds.Checked)
        {
            faceBounds.Checked=true;faceBounds.Margin=model.Tolerance;
            faceBounds.Available=source>=0&&TryThirdFacePrecision(source,ref faceBounds.Margin)
                &&RegularFiniteFaceExtent(source,out faceBounds.Box)==AlgorithmStatus.Success;
        }
        if(faceBounds.Available)
        {
            if(!cutBounds.Checked)
            {
                cutBounds.Checked=true;cutBounds.Available=TryFiniteCarrierExtent(cut,carrier,out cutBounds.Box);
            }
            if(cutBounds.Available&&ThirdFaceBoxesDisjoint(cutBounds.Box,faceBounds.Box,faceBounds.Margin))return 0;
        }
        var first=input.Faces[pair.First].Surface;var second=input.Faces[pair.Second].Surface;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[8];
        if(!TryReadExplicitSpineCurve(carrier,out var line)
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(first)],out var a)
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(second)],out var b)
            ||!PlanePairLineCertified(cut,line,a,b)||!BoundRoundedPlaneLine(cut,line,a,b,poles))
            return IntersectionError(LastSheetAlgorithmStatus=AlgorithmStatus.Unsupported);

        // The line solver uses the two actual plane equations, in the
        // stored line's station gauge. It retains both a spatial enclosure
        // and the native rolling U box from the same joint root.
        Span<OperationIntersectionPoint> points=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        Span<ScalarRootIsolation> stations=stackalloc ScalarRootIsolation[IntersectionPointCapacity];
        Span<double> errors=stackalloc double[IntersectionPointCapacity];
        Span<IntervalNumber> blendStations=stackalloc IntervalNumber[IntersectionPointCapacity];
        Span<SurfaceParameterBox> splineDomains=stackalloc SurfaceParameterBox[IntersectionPointCapacity];
        var spline=RegularCutSupportClass(input,third)==SurfaceClass.BSurface;
        var status=IsolateFiniteSurfaceLine(new(){Curve=cut.Curve,Start=cut.First,End=cut.Last},GetSurfaceSlotByTag(blend),
            points,stations,errors,out var count,poles,blendStations,spline?splineDomains:default);
        LastSheetAlgorithmStatus=status;if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        Span<BoundaryParameter> anchors=stackalloc BoundaryParameter[3];Span<FiniteCurveSurfaceRoot> roots=stackalloc FiniteCurveSurfaceRoot[3];
        var exactPositionAxes=ExactCutLineCoordinateMask(cut.Curve,poles);
        for(var i=0;i<count;i++)
        {
            LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;LastSheetParameter=points[i].First;
            roots[0]=ThirdFiniteRoot(points[i],stations[i],errors[i]);
            anchors[0]=ThirdCutAnchor(input,pair,third,current,i,roots[0]);
            var sourceRoot=new RegularBoundaryRoot{Curve=cut.Curve,Root=roots[0],ContactSurface=blend,BlendStation=blendStations[i],
                Certified=spline?PlanePairSplineLineCertificate:PlanePairBlendLineCertificate,ExactPositionAxes=exactPositionAxes,
                SplineDomain=spline?splineDomains[i]:default};
            status=FindBlendPlaneJointArc(input,cuts,third,pair.First,pair.Second,b,sourceRoot,out anchors[1],out roots[1]);
            if(status==AlgorithmStatus.Success)
                status=FindBlendPlaneJointArc(input,cuts,third,pair.Second,pair.First,a,sourceRoot,out anchors[2],out roots[2]);
            LastSheetAlgorithmStatus=status;if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var error=AssociateThirdFaceStations(anchors,roots,ref model,out _);if(error!=0)return error;
        }
        return 0;
    }

    private static AlgorithmStatus FindBlendPlaneJointArc(in RegularBoundaryView input,ReadOnlySpan<RegularFaceCut> cuts,
        BufferOffset blend,BufferOffset support,BufferOffset opposing,in AnalyticSurface opposingPlane,RegularBoundaryRoot source,
        out BoundaryParameter anchor,out FiniteCurveSurfaceRoot proof)
    {
        anchor=default;proof=default;var found=false;
        source.CuttingSurface=input.Faces[support].Surface;
        Span<OperationIntersectionPoint> points=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        Span<ScalarRootIsolation> stations=stackalloc ScalarRootIsolation[IntersectionPointCapacity];
        Span<double> errors=stackalloc double[IntersectionPointCapacity];
        for(var i=0;i<cuts.Length;i++)
        {
            ref readonly var pair=ref cuts[i];if(!ThirdCutHasPair(pair,blend,support))continue;
            var cut=pair.Cut;
            var error=LocateCertifiedCut(cut,source,out var parameter,out var transferred,out var handled);
            if(!handled)return AlgorithmStatus.Unsupported;
            if(error==Generated.ParasolidConstants.PK_ERROR_not_on_curve)continue;
            if(error!=0||!transferred.Finite)return AlgorithmStatus.NotConverged;
            var status=IntersectFiniteIntersectionCurvePlane(cut.Curve,cut.First,cut.Last,opposingPlane,points,out var count,stations,errors);
            if(status!=AlgorithmStatus.Success)return status;
            var matched=false;
            for(var r=0;r<count;r++)
            {
                var scalar=stations[r];var common=IntersectStationBounds(transferred,scalar.Root);if(!common.Finite)continue;
                // The transferred point satisfies the opposing plane too.
                // The scalar branch's uniqueness interval must contain its
                // entire transferred station, not just overlap the root box.
                if(found||!scalar.Certified||!ContainsThirdStation(scalar.MonotoneDomain,transferred))return AlgorithmStatus.NotConverged;
                scalar.Root=common;var point=points[r];point.First=Math.Clamp(parameter,common.Lower,common.Upper);
                proof=ThirdFiniteRoot(point,scalar,errors[r]);anchor=ThirdCutAnchor(input,pair,opposing,i,r,proof);found=matched=true;
            }
            if(!matched)return AlgorithmStatus.NotConverged;
        }
        return found?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    private static bool HasBlendPlaneLineJoint(in RegularBoundaryView input,in RegularFaceCut pair,BufferOffset third,
        BufferOffset cut,BufferOffset ordinal,in ScalarRootIsolation root,ref SheetArrangement model)
    {
        if(!root.Certified||pair.First<0||pair.Second<0||RegularCutSupportClass(input,third)!=SurfaceClass.Plane)return false;
        var a=RegularCutSupportClass(input,pair.First);var b=RegularCutSupportClass(input,pair.Second);
        if(!((a is SurfaceClass.BlendSurface or SurfaceClass.BSurface)&&b==SurfaceClass.Plane
            ||a==SurfaceClass.Plane&&(b is SurfaceClass.BlendSurface or SurfaceClass.BSurface)))return false;
        var carrier=Curves[GetCurveSlotByTag(pair.Cut.Curve)];var surface=input.Faces[third].Surface;
        var witness=1+(RegularInputEdges+cut)*IntersectionPointCapacity+ordinal;
        foreach(ref readonly var anchor in model.Parameters[..model.ParameterCount])
            if(anchor.Witness==witness&&anchor.Carrier==pair.Cut.Curve&&anchor.Generation==carrier.Header.Generation&&anchor.Surface==surface
                &&anchor.Lower>=root.Root.Lower&&anchor.Upper<=root.Root.Upper&&model.LiveBoundaryPoint(anchor.Event))return true;
        return false;
    }
}

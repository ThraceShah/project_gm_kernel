using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal enum RegularClipStage:byte{FirstBoundary,SecondBoundary,RootLocation,Witness,FirstUv,SecondUv,FirstTrim,SecondTrim,Publish}
    [ThreadStatic] internal static RegularClipStage LastRegularClipStage;
    private struct RegularCutFamily{internal SurfTag First,Second;internal BufferOffset Start,PointStart;internal BufferCount Count,PointCount;internal SurfacePairRelation Relation;}
    private struct RegularFaceCut{internal BufferOffset First,Second;internal SheetCutCurve Cut;internal byte SecondIsBoundary;}
    private const byte NativePlaneCertificate=2;
    private const byte LineSurfaceCertificate=3;
    private const byte ExplicitPlaneCertificate=4;
    private const byte PlanePairBlendLineCertificate=5;
    private const byte PlanePairCylinderLineCertificate=6;
    private const byte PlanePairSplineLineCertificate=7;
    private const byte NativeTripleCertificate=8;
    private struct RegularBoundaryRoot
    {
        internal CurveTag Curve;
        internal BufferOffset InputEdge;
        internal SurfTag TangentSupport;
        internal SurfTag ContactSurface,ContactSupport,CuttingSurface;
        internal FiniteCurveSurfaceRoot Root;
        internal IntervalNumber BlendStation;
        internal ScalarRootIsolation ContactIsolation;
        internal SurfaceParameterBox SplineDomain,CuttingDomain,SplineUniqueDomain,CuttingUniqueDomain;
        internal byte NativeBoundaryAxis;
        internal BufferOffset InputCoedge;
        internal BoundaryRootWitnessId Witness;
        internal byte Certified;
        internal byte ExactPositionAxes;
    }

    private static int ClipRegularContactPoints(in RegularBoundaryView input,BufferOffset first,BufferOffset second,
        ReadOnlySpan<SurfaceIntersectionNode> points,Span<SurfaceIntersectionNode> accepted,out BufferCount count)
    {
        count=0;
        foreach(ref readonly var point in points)
        {
            var both=true;
            for(var side=0;side<2;side++)
            {
                var face=side==0?first:second;var uv=new PK_UV_s();uv.param[0]=side==0?point.U:point.S;uv.param[1]=side==0?point.V:point.T;
                var error=RegularFiniteFaceContains(input,face,uv,point.Point,out var inside,out _);if(error!=0)return error;
                error=RegularPointOnFiniteBoundary(input,face,point.Point,out var boundary);if(error!=0)return error;
                if(boundary)return ParasolidConstants.PK_ERROR_not_implemented;
                both&=inside;
            }
            if(both){if(count==accepted.Length)return ParasolidConstants.PK_ERROR_not_implemented;accepted[count++]=point;}
        }
        return 0;
    }

    private static int PrepareRegularSupportCuts(SurfTag first,SurfTag second,Span<SheetCutCurve> cuts,
        out BufferCount count,out FiniteSectionChart section)
    {
        count=0;section=default;var a=GetSurfaceSlotByTag(first);var b=GetSurfaceSlotByTag(second);
        if(!RegularPlanarSupport(a,out var av,out var errorA)||!RegularPlanarSupport(b,out var bv,out var errorB))return PrepareSheetCutCurves(first,second,cuts,out count,out section);
        // Geometrically planar NURBS need not have an affine parameter map.
        // Preserve their real intersection support instead of replacing the
        // resulting native ICurve by a line on two surrogate planes.
        if(Surfaces[a].Class==SurfaceClass.BSurface&&!TryAffinePlanarSurface(a,out _,out _,out _)
            ||Surfaces[b].Class==SurfaceClass.BSurface&&!TryAffinePlanarSurface(b,out _,out _,out _))return PrepareSheetCutCurves(first,second,cuts,out count,out section);
        var sine=OperationIntersection.Norm(ProfileCross(av.Axis,bv.Axis));
        if(errorA+errorB>0&&(sine<=1e-14||(errorA+errorB)/(sine-1e-14)>1e-10))return PrepareSheetCutCurves(first,second,cuts,out count,out section);
        Span<OperationIntersectionBranch> branches=stackalloc OperationIntersectionBranch[64];Span<KernelVector3> points=stackalloc KernelVector3[64];
        var status=OperationIntersection.Surfaces(av,bv,branches,out var n,points,out var np);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        if(np!=0||n>cuts.Length)return ParasolidConstants.PK_ERROR_not_implemented;
        for(var i=0;i<n;i++)
        {
            if(branches[i].Kind!=IntersectionCurveKind.Simple)return ParasolidConstants.PK_ERROR_not_implemented;
            CurveTag curve=0;var error=CreateIntersectionBranch(branches[i],&curve);if(error!=0)return error;
            cuts[count++]=new(){Curve=curve,First=branches[i].Start,Last=branches[i].End,
                Construction=branches[i].Class==CurveClass.Line&&Surfaces[a].Class==SurfaceClass.Plane&&Surfaces[b].Class==SurfaceClass.Plane
                    ?CutConstruction.AnalyticPlanePair:CutConstruction.None,
                Closed=branches[i].Class is CurveClass.Circle or CurveClass.Ellipse&&(branches[i].End-branches[i].Start)>=Math.Tau-1e-10?(byte)1:(byte)0};
        }
        return 0;
    }

    private static bool RegularPlanarSupport(SurfaceSlot slot,out AnalyticSurface plane,out double maximumDeviation)
    {
        plane=default;maximumDeviation=0;var record=Surfaces[slot];
        if(TryPrepareSurface(record,out var analytic)){plane=analytic;return analytic.Kind==SurfaceClass.Plane;}
        if(record.Class!=SurfaceClass.BSurface)return false;
        var view=GetBSurfaceView(BSurfaceDataStore[record.DataIndex]);if(view.UVertexCount<2||view.VVertexCount<2)return false;
        var dimension=view.Dimension;var poles=view.Vertices;
        var w=view.Rational?poles[3]:1;var origin=new KernelVector3{X=poles[0]/w,Y=poles[1]/w,Z=poles[2]/w};
        var u=dimension*view.VVertexCount;var uw=view.Rational?poles[u+3]:1;var vw=view.Rational?poles[dimension+3]:1;
        var du=new KernelVector3{X=poles[u]/uw-origin.X,Y=poles[u+1]/uw-origin.Y,Z=poles[u+2]/uw-origin.Z};
        var dv=new KernelVector3{X=poles[dimension]/vw-origin.X,Y=poles[dimension+1]/vw-origin.Y,Z=poles[dimension+2]/vw-origin.Z};
        var normal=ProfileCross(du,dv);if(!ProfileNormalize(ref normal)||!ProfileNormalize(ref du))return false;
        // Positive rational basis functions preserve this control-hull distance
        // bound. This is a certified plane recognition tolerance, not samples.
        for(var i=0;i<poles.Length;i+=dimension)
        {
            IntervalNumber weight=view.Rational?poles[i+3]:1;if(weight.Lower<=0)return false;
            var x=new IntervalNumber(poles[i])/weight-origin.X;var y=new IntervalNumber(poles[i+1])/weight-origin.Y;var z=new IntervalNumber(poles[i+2])/weight-origin.Z;
            var deviation=x*normal.X+y*normal.Y+z*normal.Z;
            if(deviation.Magnitude>1e-12*Math.Max(1,Math.Max(x.Magnitude,Math.Max(y.Magnitude,z.Magnitude))))return false;
            maximumDeviation=Math.Max(maximumDeviation,deviation.Magnitude);
        }
        plane=new(SurfaceClass.Plane,origin,normal,du);return true;
    }

    private static int RegularBoundarySurfaceRoots(in RegularBoundaryView input,BufferOffset face,SurfTag surface,
        Span<RegularBoundaryRoot> points,ref BufferCount count,ReadOnlySpan<FiniteCurveSurfaceResult> certificates=default,bool rollingSurface=false,
        bool transferCylinderConic=false,scoped ReadOnlySpan<VertexSlot> originals=default)
    {
        var record=input.Faces[face];Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        Span<ScalarRootIsolation> nativeRoots=stackalloc ScalarRootIsolation[IntersectionPointCapacity];
        Span<ScalarRootIsolation> contactRoots=stackalloc ScalarRootIsolation[IntersectionPointCapacity];
        Span<double> spatialErrors=stackalloc double[IntersectionPointCapacity];
        Span<IntervalNumber> linePoles=stackalloc IntervalNumber[8],blendStations=stackalloc IntervalNumber[IntersectionPointCapacity];
        Span<SurfaceParameterBox> commonUv=stackalloc SurfaceParameterBox[IntersectionPointCapacity],targetUv=stackalloc SurfaceParameterBox[IntersectionPointCapacity];
        Span<IsolatedRootCertificate> splineProofs=stackalloc IsolatedRootCertificate[IntersectionPointCapacity];
        for(var l=0;l<record.LoopCount;l++)
        {
            var loop=input.Loops[record.FirstLoop+l];
            for(var k=0;k<loop.CoedgeCount;k++)
            {
                var inputCoedge=loop.FirstCoedge+k;var inputEdge=input.Coedges[inputCoedge].Edge;var edge=input.Edges[inputEdge];
                LastSheetPartitionStage=SheetPartitionStage.BoundaryIntersections;LastSheetCurve=edge.Curve;LastSheetParameter=edge.Start;
                var n=0;var known=false;var cached=false;var linePlane=false;FiniteCurveSurfaceResult proof=default;
                foreach(var certificate in certificates)
                {
                    if(certificate.InputEdge!=inputEdge||certificate.Surface!=surface||certificate.Curve!=edge.Curve)continue;
                    if(certificate.CurveGeneration!=Curves[GetCurveSlotByTag(edge.Curve)].Header.Generation||certificate.SurfaceGeneration!=Surfaces[GetSurfaceSlotByTag(surface)].Header.Generation
                        ||certificate.First!=edge.Start||certificate.Last!=edge.End)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    if(certificate.State is not (FiniteIntersectionState.Complete or FiniteIntersectionState.Empty))return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    known=cached=true;proof=certificate;n=certificate.Count;
                    for(var r=0;r<n;r++){var root=r==0?certificate.FirstRoot:certificate.SecondRoot;roots[r]=new(){Point=root.Point,First=root.Parameter,Kind=root.Kind};}
                    break;
                }
                if(!known&&certificates.IsEmpty&&TryFiniteLinePlaneEvent(edge,inputEdge,surface,out proof,allowExactEnd:true))
                {
                    known=linePlane=true;n=proof.Count;
                    if(n!=0)roots[0]=new(){Point=proof.FirstRoot.Point,First=proof.FirstRoot.Parameter,Kind=proof.FirstRoot.Kind};
                }
                var curveClass=Curves[GetCurveSlotByTag(edge.Curve)].Class;
                var surfaceSlot=GetSurfaceSlotByTag(surface);var surfaceClass=Surfaces[surfaceSlot].Class;var cylinder=surfaceClass==SurfaceClass.Cylinder;
                var splineContact=!known&&certificates.IsEmpty&&!originals.IsEmpty&&ExactNativeSplineBoundaryContact(edge,surfaceSlot);
                if(splineContact)
                {
                    known=true;n=2;proof.Relation=SurfacePairRelation.CurveContact;
                    var firstEvent=NativeLineEnd(edge,edge.Start);var lastEvent=NativeLineEnd(edge,edge.End);
                    if(firstEvent<0||lastEvent<0||originals[firstEvent]<0||originals[lastEvent]<0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    var firstError=SheetOriginalVertex(originals[firstEvent],out var firstPoint);if(firstError!=0)return firstError;
                    var lastError=SheetOriginalVertex(originals[lastEvent],out var lastPoint);if(lastError!=0)return lastError;
                    proof.FirstRoot=new(){Vertex=originals[firstEvent],Point=firstPoint,Parameter=edge.Start,Lower=edge.Start,Upper=edge.Start,Kind=IntersectionVectorKind.Start,NativeEnd=FiniteNativeEnd.First};
                    proof.SecondRoot=new(){Vertex=originals[lastEvent],Point=lastPoint,Parameter=edge.End,Lower=edge.End,Upper=edge.End,Kind=IntersectionVectorKind.End,NativeEnd=FiniteNativeEnd.Last};
                    roots[0]=new(){Point=firstPoint,First=edge.Start,Kind=IntersectionVectorKind.Start};
                    roots[1]=new(){Point=lastPoint,First=edge.End,Kind=IntersectionVectorKind.End};
                }
                var conicProof=!known&&rollingSurface&&curveClass==CurveClass.Ellipse&&cylinder;
                var lineProof=false;var splineBoundaryProof=false;var planePairLine=false;var planePairCylinder=false;byte exactPositionAxes=0;
                if(!known&&certificates.IsEmpty&&curveClass==CurveClass.Line&&cylinder)
                {
                    lineProof=TryIsolateInteriorLineCylinder(edge,surface,roots,nativeRoots,spatialErrors,out n);
                    // Endpoint and contact cases retain their own providers.
                    if(lineProof&&transferCylinderConic&&TryPrepareSurface(Surfaces[surfaceSlot],out var cylinderSurface)
                        &&TryPlanePairSourceLine(input,face,inputCoedge,edge,linePoles))
                    {
                        planePairCylinder=true;
                        var estimates=n;
                        var isolated=IsolateFiniteExplicitLevelSet(edge,cylinderSurface,roots,nativeRoots,spatialErrors,out n,linePoles,estimates);
                        if(isolated==AlgorithmStatus.Success&&!AllStrictlyInterior(edge,nativeRoots[..n]))isolated=AlgorithmStatus.NotConverged;
                        if(isolated!=AlgorithmStatus.Success){LastSheetAlgorithmStatus=isolated;return IntersectionError(isolated);}
                    }
                }
                if(!known&&certificates.IsEmpty&&curveClass==CurveClass.Line&&surfaceClass==SurfaceClass.BlendSurface)
                {
                    var blend=BlendedEdgeDataPool[Surfaces[surfaceSlot].DataIndex];
                    planePairLine=TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(blend.SpineTag)],out _)
                        &&TryPlanePairSourceLine(input,face,inputCoedge,edge,linePoles);
                    var isolated=IsolateFiniteSurfaceLine(edge,surfaceSlot,roots,nativeRoots,spatialErrors,out n,
                        planePairLine?linePoles:default,blendStations);
                    lineProof=isolated==AlgorithmStatus.Success;
                    if(planePairLine&&!lineProof){LastSheetAlgorithmStatus=isolated;return IntersectionError(isolated);}
                    if(planePairLine)exactPositionAxes=ExactCutLineCoordinateMask(edge.Curve,linePoles);
                }
                if(!known&&!lineProof&&certificates.IsEmpty&&curveClass==CurveClass.Line&&surfaceClass==SurfaceClass.BSurface
                    &&!TryAffinePlanarSurface(surfaceSlot,out _,out _,out _))
                    lineProof=IsolateFiniteSurfaceLine(edge,surfaceSlot,roots,nativeRoots,spatialErrors,out n,default,default)==AlgorithmStatus.Success;
                if(!known&&!lineProof&&certificates.IsEmpty&&curveClass==CurveClass.BCurve&&surfaceClass==SurfaceClass.BSurface
                    &&!TryAffinePlanarSurface(surfaceSlot,out _,out _,out _))
                {
                    var isolated=IsolateFiniteSplineSurface(edge,surfaceSlot,roots,nativeRoots,spatialErrors,out n,splineProofs);
                    if(isolated!=AlgorithmStatus.Success){LastSheetAlgorithmStatus=isolated;return IntersectionError(isolated);}
                    lineProof=true;splineBoundaryProof=true;
                }
                var conicPlaneProof=!known&&certificates.IsEmpty&&(curveClass is CurveClass.Circle or CurveClass.Ellipse or CurveClass.BCurve)
                    &&TryBoundaryPlane(surfaceSlot,out var plane)
                    &&(curveClass==CurveClass.BCurve?IsolateFiniteSplinePlane(edge,plane,roots,nativeRoots,spatialErrors,out n)
                        :IsolateFiniteExplicitLevelSet(edge,plane,roots,nativeRoots,spatialErrors,out n))==AlgorithmStatus.Success
                    &&AllNativePlaneRoots(edge,nativeRoots[..n]);
                var tripleProof=!known&&!lineProof&&!conicPlaneProof&&certificates.IsEmpty&&NativeTripleSurfaceApplicable(edge.Curve,surfaceSlot);
                if(tripleProof)
                {
                    var isolated=IsolateFiniteNativeTriple(edge,record.Surface,surfaceSlot,roots,nativeRoots,spatialErrors,commonUv,targetUv,out n);
                    if(isolated!=AlgorithmStatus.Success){LastSheetAlgorithmStatus=isolated;return IntersectionError(isolated);}
                }
                contactRoots.Clear();
                var nativeProof=!tripleProof&&!known&&!lineProof&&!conicPlaneProof&&certificates.IsEmpty&&TryNativeCurvePlaneEvents(edge,surface,roots,nativeRoots,spatialErrors,out n,contactRoots);
                FiniteCurveSurfaceRoot endpoint=default;
                if(!tripleProof&&!nativeProof&&!known&&!lineProof&&!conicPlaneProof&&certificates.IsEmpty&&!originals.IsEmpty&&curveClass==CurveClass.ICurve)
                    nativeProof=TryStationaryNativePlaneEnd(edge,surface,originals,roots,nativeRoots,spatialErrors,out n,out endpoint);
                if(!tripleProof&&!nativeProof&&!known&&!lineProof&&!conicPlaneProof&&certificates.IsEmpty&&!originals.IsEmpty)
                    nativeProof=TryExplicitNativePlaneEnd(edge,surface,originals,roots,nativeRoots,spatialErrors,out n,out endpoint);
                var status=known||tripleProof||nativeProof||lineProof||conicPlaneProof?AlgorithmStatus.Success:conicProof?IsolateFiniteExplicitCylinder(edge,surface,roots,nativeRoots,spatialErrors,out n)
                    :IntersectRegularSurfaceCurve(surface,edge.Curve,edge.Start,edge.End,roots,out n);
                LastSheetAlgorithmStatus=status;if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                for(var r=0;r<n;r++)
                {
                    if(roots[r].Kind is not (IntersectionVectorKind.Simple or IntersectionVectorKind.Start or IntersectionVectorKind.End or IntersectionVectorKind.Tangent))return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    // Repeated chart discoveries on this coedge may be the
                    // same root. A different use of the carrier retains its
                    // own support and provenance even when the event joins.
                    var duplicate=false;for(var i=count-1;i>=0&&points[i].InputCoedge==inputCoedge;i--)
                        duplicate|=points[i].Root.Parameter==roots[r].First;
                    if(duplicate&&(known||tripleProof||nativeProof||conicProof||lineProof||conicPlaneProof))return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    if(!duplicate)
                    {
                        if(count==points.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                        SurfTag contactSupport=0;
                        foreach(var certificate in certificates)if(certificate.InputEdge==inputEdge&&certificate.TangentBoundary!=0)
                        {contactSupport=certificate.Surface;break;}
                        var witness=known?(r==0?proof.FirstRoot:proof.SecondRoot):new FiniteCurveSurfaceRoot
                            {Point=roots[r].Point,Vertex=-1,Parameter=roots[r].First,Kind=roots[r].Kind};
                        if(tripleProof||nativeProof||conicProof||lineProof||conicPlaneProof)
                        {
                            witness.Lower=nativeRoots[r].Root.Lower;witness.Upper=nativeRoots[r].Root.Upper;witness.SpatialError=spatialErrors[r];
                        }
                        if((linePlane||conicPlaneProof||lineProof&&roots[r].Kind is (IntersectionVectorKind.Start or IntersectionVectorKind.End))&&witness.Lower==witness.Upper
                            &&(witness.Parameter==edge.Start||witness.Parameter==edge.End))
                        {
                            var upper=witness.Parameter==edge.End;var positive=LocalSenseSign(Curves[GetCurveSlotByTag(edge.Curve)].Sense)>0;
                            var eventId=upper==positive?edge.EndVertex:edge.StartVertex;
                            witness.NativeEnd=upper?FiniteNativeEnd.Last:FiniteNativeEnd.First;
                            if(eventId>=0&&eventId<originals.Length&&originals[eventId]>=0)
                            {
                                var endpointError=SheetOriginalVertex(originals[eventId],out var position);if(endpointError!=0)return endpointError;
                                var discrepancy=ProfileDistance(position,witness.Point);if(discrepancy>OperationIntersection.DistanceTolerance)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                                witness.Point=position;witness.Vertex=originals[eventId];witness.SpatialError+=discrepancy;
                            }
                        }
                        if(endpoint.NativeEnd!=FiniteNativeEnd.None&&endpoint.Parameter==witness.Parameter)witness=endpoint;
                        var domain=default(SurfaceParameterBox);var uniqueDomain=default(SurfaceParameterBox);byte boundaryAxis=0;
                        var mappedSplineBoundary=splineBoundaryProof&&BoundNativeSplineBoundaryRoot(edge,GetSurfaceSlotByTag(record.Surface),nativeRoots[r].Root,
                            new(splineProofs[r].SecondDomain.U0,splineProofs[r].SecondDomain.U1),out domain,out uniqueDomain,out boundaryAxis);
                        if(splineBoundaryProof&&ExactNativeSplineBoundaryContact(edge,GetSurfaceSlotByTag(record.Surface))&&!mappedSplineBoundary)
                        {
                            return ParasolidConstants.PK_ERROR_cant_do_intersect;
                        }
                        points[count++]=new(){Curve=edge.Curve,InputEdge=inputEdge,InputCoedge=inputCoedge,TangentSupport=proof.TangentSupport,ContactSurface=record.Surface,ContactSupport=contactSupport,CuttingSurface=surface,
                            Certified=tripleProof?NativeTripleCertificate:linePlane||conicPlaneProof?ExplicitPlaneCertificate:nativeProof?NativePlaneCertificate:planePairLine?PlanePairBlendLineCertificate
                                :planePairCylinder?PlanePairCylinderLineCertificate
                                :lineProof?LineSurfaceCertificate:known||conicProof?(byte)1:(byte)0,Root=witness,
                            BlendStation=planePairLine?blendStations[r]:default,
                            ContactIsolation=nativeProof?contactRoots[r]:default,
                            SplineDomain=tripleProof?commonUv[r]:domain,CuttingDomain=tripleProof?targetUv[r]:mappedSplineBoundary?splineProofs[r].First:default,
                            SplineUniqueDomain=uniqueDomain,CuttingUniqueDomain=mappedSplineBoundary?splineProofs[r].FirstDomain:default,NativeBoundaryAxis=boundaryAxis,
                            ExactPositionAxes=exactPositionAxes,
                            Witness=cached||tripleProof||splineContact||conicProof||lineProof||linePlane||conicPlaneProof||nativeProof?1+inputEdge*IntersectionPointCapacity+r:0};
                    }
                }
            }
        }
        return 0;
    }

    private static bool TryPlanePairSourceLine(in RegularBoundaryView input,BufferOffset face,BufferOffset coedge,in ModelingEdge edge,Span<IntervalNumber> poles)
    {
        if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(input.Faces[face].Surface)],out var first)||first.Kind!=SurfaceClass.Plane
            ||!TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(edge.Curve)],out var line))return false;
        var source=input.Coedges[coedge];var mate=-1;
        for(var f=0;f<input.Faces.Length;f++)
        {
            if(f==face)continue;
            var candidate=input.Faces[f];
            for(var l=0;l<candidate.LoopCount;l++)
            {
                var loop=input.Loops[candidate.FirstLoop+l];
                foreach(ref readonly var use in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                {
                    if(use.Edge!=source.Edge)continue;
                    if(mate>=0||use.Forward==source.Forward)return false;
                    mate=f;
                }
            }
        }
        if(mate<0||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(input.Faces[mate].Surface)],out var second)||second.Kind!=SurfaceClass.Plane)return false;
        var window=new SheetCutCurve{Curve=edge.Curve,First=edge.Start,Last=edge.End};
        // Both coedges produce the same bounded equations in the same order.
        return face<mate?BoundRoundedPlaneLine(window,line,first,second,poles):BoundRoundedPlaneLine(window,line,second,first,poles);
    }

    private static bool TryIsolateInteriorLineCylinder(in ModelingEdge edge,SurfTag cylinder,Span<OperationIntersectionPoint> roots,
        Span<ScalarRootIsolation> isolation,Span<double> spatialErrors,out BufferCount count)
    {
        var status=IsolateFiniteExplicitCylinder(edge,cylinder,roots,isolation,spatialErrors,out count);
        return status==AlgorithmStatus.Success&&count!=0&&AllStrictlyInterior(edge,isolation[..count]);
    }

    private static bool AllStrictlyInterior(in ModelingEdge edge,ReadOnlySpan<ScalarRootIsolation> isolation)
    {
        // A real or uncertain root on an original endpoint still needs its
        // vertex occurrence or a contact proof. Certify the interior only.
        foreach(ref readonly var root in isolation)if(!root.Certified||!(root.Root.Lower>edge.Start&&root.Root.Upper<edge.End))return false;
        return true;
    }

    private static bool AllNativePlaneRoots(in ModelingEdge edge,ReadOnlySpan<ScalarRootIsolation> isolation)
    {
        foreach(ref readonly var root in isolation)
            if(!root.Certified||!(root.Root.Lower>edge.Start&&root.Root.Upper<edge.End)
                &&!(root.Root.Lower==root.Root.Upper&&(root.Root.Lower==edge.Start||root.Root.Lower==edge.End)))return false;
        return true;
    }

    private static bool TryNativeCurvePlaneEvents(in ModelingEdge edge,SurfTag surface,Span<OperationIntersectionPoint> roots,
        Span<ScalarRootIsolation> isolation,Span<double> spatialErrors,out BufferCount count,Span<ScalarRootIsolation> contactRoots)
    {
        count=0;var curve=Curves[GetCurveSlotByTag(edge.Curve)];var support=Surfaces[GetSurfaceSlotByTag(surface)];
        if(curve.Class!=CurveClass.ICurve||support.Class!=SurfaceClass.Plane||!TryPrepareSurface(support,out var plane))return false;
        var data=ICurveDataPool[curve.DataIndex];
        if(data.ProgramBlock<=0&&data.IsBlendContact==0)return false;
        if(RegularCoincidentSupports(surface,data.Surface0Tag)||RegularCoincidentSupports(surface,data.Surface1Tag))return false;
        if(data.IsBlendContact!=0)
        {
            var complete=TryBlendContactPlaneEvents(data,edge,plane,roots,isolation,spatialErrors,out count,contactRoots);
            if(!complete)contactRoots.Clear();
            return complete;
        }
        var status=IntersectFiniteIntersectionCurvePlane(edge.Curve,edge.Start,edge.End,plane,roots,out count,isolation,spatialErrors);
        // Endpoint contacts or a nonmonotone unresolved cell retain the
        // existing explicit endpoint/general solver. No partial root list
        // or failed isolation is reported as a complete certificate.
        return status==AlgorithmStatus.Success;
    }

    private static bool TryFiniteLinePlaneEvent(in ModelingEdge edge,BufferOffset inputEdge,SurfTag surface,out FiniteCurveSurfaceResult result,
        bool allowExactEnd=false)
    {
        result=default;
        if(!double.IsFinite(edge.Start)||!double.IsFinite(edge.End)||edge.Start>=edge.End)return false;
        var record=Curves[GetCurveSlotByTag(edge.Curve)];var planeSlot=GetSurfaceSlotByTag(surface);
        if(!TryExactNativeAffineCurve(record,out var line)
            ||!TryBoundaryPlane(planeSlot,out var plane))return false;
        if(!TryLinePlaneRatio(line,plane,out var numerator,out var denominator))
        {
            if(!denominator.Finite||denominator.Magnitude!=0||!numerator.Finite||numerator.ContainsZero)return false;
            result=NewFiniteEventResult(edge,inputEdge,surface);result.State=FiniteIntersectionState.Empty;result.Relation=SurfacePairRelation.Empty;return true;
        }
        var axis=new IntervalVector(line.First);
        var t=numerator/denominator;if(!t.Finite)return false;
        result=NewFiniteEventResult(edge,inputEdge,surface);
        if(t.Upper<edge.Start||t.Lower>edge.End)
        {result.State=FiniteIntersectionState.Empty;result.Relation=SurfacePairRelation.Empty;return true;}
        // A box that touches a trim limit needs an endpoint association.
        // A disjoint box has a finite-domain proof at any positive distance.
        var nativeEnd=FiniteNativeEnd.None;
        if(t.Lower<=edge.Start||t.Upper>=edge.End)
        {
            if(!allowExactEnd||t.Lower!=t.Upper)return false;
            nativeEnd=t.Lower==edge.Start?FiniteNativeEnd.First:FiniteNativeEnd.Last;
        }
        var parameter=Math.Clamp(Dot(Sub(plane.Origin,line.Origin),plane.Axis)/Dot(line.First,plane.Axis),t.Lower,t.Upper);
        var point=Add(line.Origin,Scale(line.First,parameter));
        var enclosure=new IntervalVector(line.Origin)+axis*new IntervalDual(t,0);
        var error=(enclosure-new IntervalVector(point)).Norm().Value.Upper;
        if(!double.IsFinite(error))return false;
        result.State=FiniteIntersectionState.Complete;result.Relation=SurfacePairRelation.PointContact;result.Count=1;
        result.FirstRoot=new(){Point=point,Vertex=-1,Kind=IntersectionVectorKind.Simple,NativeEnd=nativeEnd,
            Parameter=parameter,Lower=t.Lower,Upper=t.Upper,SpatialError=error};
        return true;
    }

    private static bool TryLinePlaneRatio(in ParametricSpineCurve line,in AnalyticSurface plane,out IntervalNumber numerator,out IntervalNumber denominator)
    {
        var normal=new IntervalVector(plane.Axis);
        denominator=IntervalVector.Dot(new IntervalVector(line.First),normal).Value;
        numerator=IntervalVector.Dot(new IntervalVector(plane.Origin)-new IntervalVector(line.Origin),normal).Value;
        return numerator.Finite&&denominator.Finite&&!denominator.ContainsZero;
    }

    private static bool TryBoundaryPlane(SurfaceSlot slot,out AnalyticSurface plane)
    {
        if(TryPrepareSurface(Surfaces[slot],out plane)&&plane.Kind==SurfaceClass.Plane)return true;
        if(!TryAffinePlanarSurface(slot,out plane,out _,out _))return false;
        // The affine view retains the native NURBS. Its rounded normal is
        // usable as an exact root equation only when every defining pole
        // lies on that equation without an arithmetic uncertainty.
        var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[slot].DataIndex]);
        for(var i=0;i<view.UVertexCount*view.VVertexCount;i++)
            if(!ExactPointOnPlaneLevel(new IntervalVector(ProfileEuclideanPole(view.Vertices,3,i)),plane))return false;
        return true;
    }

    private static bool SameCertifiedLinePlaneRoot(CurveTag carrier,SurfTag first,SurfTag second)
    {
        var line=GetCurveSlotByTag(carrier);var a=GetSurfaceSlotByTag(first);var b=GetSurfaceSlotByTag(second);
        if(line<0||a<0||b<0||Curves[line].Class!=CurveClass.Line
            ||!TryReadExplicitSpineCurve(Curves[line],out var geometry)
            ||!TryBoundaryPlane(a,out var p)||!TryBoundaryPlane(b,out var q)
            ||!TryLinePlaneRatio(geometry,p,out var n0,out var d0)||!TryLinePlaneRatio(geometry,q,out var n1,out var d1))return false;
        // The quotient need not be a representable parameter. Compare the
        // two exact coefficient pairs, or prove their cross difference zero.
        if(n0.Lower==n0.Upper&&n1.Lower==n1.Upper&&d0.Lower==d0.Upper&&d1.Lower==d1.Upper
            &&(n0.Lower==n1.Lower&&d0.Lower==d1.Lower||n0.Lower== -n1.Lower&&d0.Lower== -d1.Lower))return true;
        var difference=n0*d1-n1*d0;
        return difference.Lower==0&&difference.Upper==0;
    }

#if KERNEL_TEST_HOOKS
    internal static int TestFiniteConicPlaneEventIdentities(CurveTag carrier,double first,double last,ReadOnlySpan<SurfTag> planes,
        Span<BoundaryEventId> ids,out BufferCount associations,bool requireProof)
    {
        associations=0;
        var edge=new ModelingEdge{Curve=carrier,Start=first,End=last};
        var model=new SheetArrangement{Points=stackalloc KernelVector3[2],OriginalVertices=stackalloc VertexSlot[2],
            Parameters=stackalloc BoundaryParameter[2],Tolerance=OperationIntersection.DistanceTolerance};
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[2];
        Span<ScalarRootIsolation> isolation=stackalloc ScalarRootIsolation[2];
        Span<double> errors=stackalloc double[2];
        Span<BoundaryParameter> anchors=stackalloc BoundaryParameter[1];
        var generation=Curves[GetCurveSlotByTag(carrier)].Header.Generation;
        for(var i=0;i<planes.Length;i++)
        {
            if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(planes[i])],out var plane))return ParasolidConstants.PK_ERROR_bad_parameter;
            var status=IsolateFiniteExplicitLevelSet(edge,plane,roots,isolation,errors,out var count);
            if(status!=AlgorithmStatus.Success||count!=1||!AllStrictlyInterior(edge,isolation[..count]))return ParasolidConstants.PK_ERROR_bad_parameter;
            var root=isolation[0].Root;
            anchors[0]=new(){Carrier=carrier,Generation=generation,Surface=planes[i],Witness=1,Lower=root.Lower,Upper=root.Upper};
            var error=model.AddPoint(roots[0].Point,-1,out ids[i],anchors,requireProof);if(error!=0)return error;
            error=model.AssociateParameter(ids[i],carrier,roots[0].First,root.Lower,root.Upper,planes[i],witness:1);if(error!=0)return error;
        }
        associations=model.ParameterCount;return 0;
    }

    internal static int TestFiniteRootWitnessIdentity(CurveTag carrier,SurfTag equation,bool separateOccurrence,out BoundaryEventId first,out BoundaryEventId repeated)
    {
        first=repeated=-1;
        var model=new SheetArrangement{Points=stackalloc KernelVector3[3],OriginalVertices=stackalloc VertexSlot[3],
            Parameters=stackalloc BoundaryParameter[3],Tolerance=OperationIntersection.DistanceTolerance};
        Span<BoundaryParameter> anchor=stackalloc BoundaryParameter[1];
        anchor[0]=new(){Carrier=carrier,Generation=Curves[GetCurveSlotByTag(carrier)].Header.Generation,Surface=equation,Witness=1,
            Lower=separateOccurrence?.5:.499999999,Upper=separateOccurrence?.5:.500000001};
        var error=model.AddPoint(default,-1,out first,anchor,requireProof:true);if(error!=0)return error;
        error=model.AssociateParameter(first,carrier,.5,anchor[0].Lower,anchor[0].Upper,equation,anchor[0].Witness);if(error!=0)return error;
        error=model.AddPoint(Vector(1e-10,0,0),-1,out repeated,anchor,requireProof:true);if(error!=0)return error;
        anchor[0].Witness=separateOccurrence?1+IntersectionPointCapacity:2;
        return model.AddPoint(default,-1,out _,anchor,requireProof:true);
    }

    internal static int TestFiniteWitnessRediscovery(CurveTag carrier,SurfTag equation,double lower,double upper,double offset,
        bool requireProof,bool associateOnly,out BufferCount events)
    {
        events=0;
        var model=new SheetArrangement{Points=stackalloc KernelVector3[2],OriginalVertices=stackalloc VertexSlot[2],
            Parameters=stackalloc BoundaryParameter[2],Tolerance=OperationIntersection.DistanceTolerance};
        var error=model.AddPoint(Vector(.5,0,0),-1,out var original,requireProof:true);if(error!=0)return error;
        error=model.AssociateParameter(original,carrier,.5,.499,.501,equation,witness:1);if(error!=0)return error;
        if(associateOnly)
        {
            error=model.AddPoint(Vector(.5+offset,0,0),-1,out var other,requireProof:true);if(error!=0)return error;
            error=model.AssociateParameter(other,carrier,(lower+upper)*.5,lower,upper,equation,witness:1);
        }
        else
        {
            Span<BoundaryParameter> anchors=stackalloc BoundaryParameter[1]{new(){Carrier=carrier,
                Generation=Curves[GetCurveSlotByTag(carrier)].Header.Generation,Surface=equation,Witness=1,Lower=lower,Upper=upper}};
            error=model.AddPoint(Vector(.5+offset,0,0),-1,out _,anchors,requireProof);
        }
        events=model.NP;return error;
    }

    internal static int TestFiniteLinePlaneEventIdentities(CurveTag line,double first,double last,ReadOnlySpan<SurfTag> planes,Span<BoundaryEventId> ids,out BufferCount associations,bool requireProof=false)
    {
        associations=0;
        var edge=new ModelingEdge{Curve=line,Start=first,End=last};
        var model=new SheetArrangement{Points=stackalloc KernelVector3[8],OriginalVertices=stackalloc VertexSlot[8],
            Parameters=stackalloc BoundaryParameter[8],Tolerance=OperationIntersection.DistanceTolerance};
        Span<BoundaryParameter> anchor=stackalloc BoundaryParameter[1];
        var generation=Curves[GetCurveSlotByTag(line)].Header.Generation;
        for(var i=0;i<planes.Length;i++)
        {
            if(!TryFiniteLinePlaneEvent(edge,0,planes[i],out var result)||result.Count!=1)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            var root=result.FirstRoot;
            anchor[0]=new(){Carrier=line,Generation=generation,Surface=planes[i],Witness=1,Lower=root.Lower,Upper=root.Upper};
            var error=model.AddPoint(root.Point,-1,out ids[i],anchor,requireProof);if(error!=0)return error;
            error=model.AssociateParameter(ids[i],line,root.Parameter,root.Lower,root.Upper,planes[i],witness:1);if(error!=0)return error;
        }
        associations=model.ParameterCount;return 0;
    }

    internal static int TestCoincidentSourceFragments(CurveTag carrier,Span<BufferCount> counts,out int ambiguousCut,bool sourceRoot=false,bool requireProof=true)
    {
        ambiguousCut=0;
        var finite=new ModelingEdge{Curve=carrier,Start=0,End=1,StartVertex=0,EndVertex=1};
        Span<ModelingEdge> input=stackalloc ModelingEdge[2]{finite,finite};
        Span<EdgeSlot> original=stackalloc EdgeSlot[2]{-1,-1};
        Span<SheetSourceEdge> fragments=stackalloc SheetSourceEdge[2];
        var graph=new SheetArrangement{Edges=stackalloc ModelingEdge[3],CutEdges=stackalloc byte[3],
            Points=stackalloc KernelVector3[3]{default,Vector(1,0,0),Vector(.5,0,0)},
            Parameters=stackalloc BoundaryParameter[1],NP=sourceRoot?3:2,RequireBoundaryAssociations=requireProof};
        if(sourceRoot)
        {
            var associate=graph.AssociateParameter(2,carrier,.5,witness:1);if(associate!=0)return associate;
        }
        var ownedCount=0;
        var error=SplitRegularSourceEdges(new(input,default,default,default),original,fragments,default,ref ownedCount,ref graph);
        if(error!=0)return error;
        counts[0]=fragments[0].Count;counts[1]=fragments[1].Count;
        ambiguousCut=graph.AddEdge(carrier,0,1,0,1,true,out _,exact:true);
        return 0;
    }

    internal static int TestBoundedSourceStationOrder(CurveTag carrier,ReadOnlySpan<IntervalNumber> roots,out BufferCount fragments,out double middle,
        IntervalNumber? firstRefinement=null,double? declaredEnd=null)
    {
        fragments=0;middle=0;
        Span<ModelingEdge> input=stackalloc ModelingEdge[1]{new(){Curve=carrier,Start=0,End=1,StartVertex=0,EndVertex=1}};
        Span<EdgeSlot> original=stackalloc EdgeSlot[1]{-1};
        Span<SheetSourceEdge> sources=stackalloc SheetSourceEdge[1];
        var graph=new SheetArrangement{Edges=stackalloc ModelingEdge[3],CutEdges=stackalloc byte[3],
            Points=stackalloc KernelVector3[4],Parameters=stackalloc BoundaryParameter[3],NP=roots.Length+2,RequireBoundaryAssociations=true};
        graph.Points[1]=Vector(1,0,0);
        for(var i=0;i<roots.Length;i++)
        {
            var bound=roots[i];graph.Points[i+2]=Vector(bound.Midpoint,0,0);
            var error=graph.AssociateParameter(i+2,carrier,bound.Midpoint,bound.Lower,bound.Upper,witness:i+1);if(error!=0)return error;
        }
        if(firstRefinement is IntervalNumber refinement)
        {
            var error=graph.AssociateParameter(2,carrier,refinement.Midpoint,refinement.Lower,refinement.Upper,witness:roots.Length+1);if(error!=0)return error;
        }
        if(declaredEnd is double end&&!HasCutEnd(ref graph,carrier,2,end))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var ownedCount=0;var split=SplitRegularSourceEdges(new(input,default,default,default),original,sources,default,ref ownedCount,ref graph);
        if(split!=0)return split;
        fragments=sources[0].Count;
        if(fragments==3)middle=graph.Edges[1].End-graph.Edges[1].Start;
        return 0;
    }
#endif

    private static int ClipRegularFacePair(in RegularBoundaryView input,BufferOffset first,BufferOffset second,
        ReadOnlySpan<SheetCutCurve> curves,Span<RegularFaceCut> cuts,ref BufferCount cutCount,
        Span<SheetCutCurve> owned,ref BufferCount ownedCount,ref SheetArrangement model,ReadOnlySpan<FiniteCurveSurfaceResult> certificates=default,
        BufferOffset firstRollingFace=-1,scoped ReadOnlySpan<FaceSlot> sourceFaces=default)
    {
        Span<RegularBoundaryRoot> roots=stackalloc RegularBoundaryRoot[SheetPointLimit];var nRoots=0;
        var transferCylinderConic=false;
        foreach(ref readonly var curve in curves)transferCylinderConic|=curve.Construction==CutConstruction.AnalyticCylinderPlaneConic;
        var originals=model.OriginalVertices[..model.NP];
        LastRegularClipStage=RegularClipStage.FirstBoundary;
        var error=RegularBoundarySurfaceRoots(input,first,input.Faces[second].Surface,roots,ref nRoots,certificates,
            firstRollingFace>=0&&first<firstRollingFace&&second>=firstRollingFace,transferCylinderConic,originals);if(error!=0)return error;
        LastRegularClipStage=RegularClipStage.SecondBoundary;
        error=RegularBoundarySurfaceRoots(input,second,input.Faces[first].Surface,roots,ref nRoots,certificates,
            firstRollingFace>=0&&second<firstRollingFace&&first>=firstRollingFace,transferCylinderConic,originals);if(error!=0)return error;
        LastSheetPartitionStage=SheetPartitionStage.CutClip;
        Span<double> parameters=stackalloc double[SheetPointLimit+2];Span<double> rootParameters=stackalloc double[SheetPointLimit];Span<KernelVector3> point=stackalloc KernelVector3[1];
        Span<IntervalNumber> cutLocators=stackalloc IntervalNumber[nRoots];
        Span<IntervalNumber> stations=stackalloc IntervalNumber[SheetPointLimit+2];
        Span<BoundaryParameter> anchors=stackalloc BoundaryParameter[IntersectionPointCapacity];
        var sectionCharts=model.SectionCharts[..model.SectionChartCount];
        var firstSurface=input.Faces[first].Surface;var secondSurface=input.Faces[second].Surface;
        foreach(var provider in curves)
        {
            LastRegularClipStage=RegularClipStage.RootLocation;
            var curve=provider;
            var n=0;
            var hasSection=TryFiniteSectionChart(sectionCharts,firstSurface,curve.Curve,curve.First,curve.Last,out var section)
                ||TryFiniteSectionChart(sectionCharts,secondSurface,curve.Curve,curve.First,curve.Last,out section);
            var sectionPlane=section.Surface==firstSurface?secondSurface:firstSurface;
            // A line's bounded material intervals begin and end at actual
            // face-boundary crossings. Artificial infinite support bounds
            // are not finite-domain witnesses for a planar NURBS patch.
            if(curve.Closed==0&&Curves[GetCurveSlotByTag(curve.Curve)].Class!=CurveClass.Line){parameters[n++]=curve.First;parameters[n++]=curve.Last;}
            for(var i=0;i<nRoots;i++)
            {
                rootParameters[i]=double.NaN;cutLocators[i]=new(double.NaN,double.NaN);
                var p=new PK_VECTOR_s();WriteVector(&p,roots[i].Root.Point);double t=roots[i].Root.Parameter;
                if(roots[i].Curve==curve.Curve)
                {
                    error=0;
                    if(roots[i].Certified!=0&&roots[i].Witness!=0)
                        cutLocators[i]=new(roots[i].Root.Lower,roots[i].Root.Upper);
                }
                else
                {
                    error=LocateCertifiedCut(curve,roots[i],out t,out cutLocators[i],out var handled);
                    if(!handled&&hasSection&&TryFiniteSectionContactEnd(section,sectionPlane,roots[i],out var sectionEnd))
                    {handled=true;t=sectionEnd;cutLocators[i]=new(t);}
                    if(!handled)error=CurveParameteriseVectorCore(curve.Curve,p,&t);
                }
                if(error==ParasolidConstants.PK_ERROR_not_on_curve){cutLocators[i]=UncertifiedStation;continue;}
                if(error!=0)return error;
                rootParameters[i]=t;
            }
            ChooseClosedConicCutWindow(ref curve,cutLocators);
            for(var i=0;i<nRoots;i++)
            {
                var t=rootParameters[i];if(double.IsNaN(t))continue;
                rootParameters[i]=double.NaN;
                if(cutLocators[i].Finite)
                {
                    var anchor=new BoundaryParameter{Parameter=t,Lower=cutLocators[i].Lower,Upper=cutLocators[i].Upper};
                    var lowerEnd=false;var upperEnd=false;
                    if(roots[i].Curve==curve.Curve&&roots[i].Witness!=0)
                    {
                        var source=input.Edges[roots[i].InputEdge];
                        lowerEnd=roots[i].Root.NativeEnd==FiniteNativeEnd.First&&t==source.Start&&t==curve.First;
                        upperEnd=roots[i].Root.NativeEnd==FiniteNativeEnd.Last&&t==source.End&&t==curve.Last;
                    }
                    error=LocateBoundedStation(curve.Curve,0,anchor,curve.First,curve.Last,lowerEnd?0:-1,upperEnd?0:-1,
                        out t,out cutLocators[i],out var inside);
                    if(error!=0)return error;if(!inside){cutLocators[i]=UncertifiedStation;continue;}
                    rootParameters[i]=t;
                    error=ReconcileCertifiedCutRoot(curve.Curve,i,roots[..nRoots],rootParameters,cutLocators,parameters[..n],out var joined);
                    if(error!=0)return error;if(joined)continue;
                }
                else
                {
                    LiftPeriodicEdgeParameter(curve.Curve,curve.First,curve.Last,ref t);
                    if(!OperationIntersection.InInterval(t,curve.First,curve.Last))continue;
                    t=Math.Clamp(t,curve.First,curve.Last);rootParameters[i]=t;
                }
                var duplicate=false;
                for(var j=0;j<n;j++)
                {
                    if(cutLocators[i].Finite&&curve.Closed==0&&(parameters[j]==curve.First||parameters[j]==curve.Last))
                    {
                        if(parameters[j]<cutLocators[i].Lower||parameters[j]>cutLocators[i].Upper)continue;
                        if(cutLocators[i].Lower!=cutLocators[i].Upper)
                            return UnresolvedBoundaryStation(curve.Curve,-1,t,cutLocators[i],parameters[j],new(parameters[j]));
                    }
                    if(Math.Abs(parameters[j]-t)>=1e-10)continue;
                    var distinct=false;
                    if(cutLocators[i].Finite)
                    {
                        // Certified pairs were settled above, without the
                        // station epsilon. A point inverse cannot move one.
                        for(var r=0;r<i;r++)distinct|=rootParameters[r]==parameters[j]&&cutLocators[r].Finite;
                        if(distinct||parameters[j]!=t)continue;
                    }
                    if(roots[i].Certified!=0)for(var r=0;r<i;r++)
                    {
                        var other=roots[r];var current=roots[i];
                        if(rootParameters[r]!=parameters[j]||other.Certified==0||other.Curve!=current.Curve
                            ||!(other.Root.Upper<current.Root.Lower||current.Root.Upper<other.Root.Lower))continue;
                        // A native proof prevents the chart-anchor epsilon
                        // from erasing two events. Keep their cut order only
                        // when it follows from disjoint projected enclosures.
                        if(!TryCutLineParameterBox(curve.Curve,other.Root,out var a)||!TryCutLineParameterBox(curve.Curve,current.Root,out var b)
                            ||!(a.Upper<b.Lower&&parameters[j]<t||b.Upper<a.Lower&&t<parameters[j]))
                            return ParasolidConstants.PK_ERROR_cant_do_intersect;
                        distinct=true;
                    }
                    if(distinct)continue;
                    if(roots[i].Certified!=0&&parameters[j]!=t)
                    {
                        // Retaining the native anchor must also retain the
                        // certified source occurrence that reaches it.
                        if(CertifiedRootMatchesCutAnchor(roots[i],curve.Curve,parameters[j],point))
                            rootParameters[i]=parameters[j];
                        else if(model.RequireBoundaryAssociations||ForbidSpatialBoundaryReuse)continue;
                    }
                    duplicate=true;break;
                }
                if(!duplicate){if(n==parameters.Length)return ParasolidConstants.PK_ERROR_not_implemented;parameters[n++]=t;}
            }
            parameters[..n].Sort();stations[..n].Fill(UncertifiedStation);
            for(var r=0;r<nRoots;r++)if(cutLocators[r].Finite&&double.IsFinite(rootParameters[r]))
            {
                var at=parameters[..n].BinarySearch(rootParameters[r]);if(at<0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                stations[at]=stations[at].Finite?IntersectStationBounds(stations[at],cutLocators[r]):cutLocators[r];
            }
            var intervals=curve.Closed!=0?Math.Max(1,n):n-1;
            for(var i=0;i<intervals;i++)
            {
                var closed=curve.Closed!=0&&n==0;var wrap=curve.Closed!=0&&n>0&&i==n-1;
                var lo=closed?curve.First:parameters[i];var hi=closed?curve.Last:parameters[(i+1)%n];
                var width=wrap?curve.Last-lo+hi-curve.First:hi-lo;if(!(width>0))continue;
                var aBound=closed?UncertifiedStation:stations[i];var bBound=closed?UncertifiedStation:stations[(i+1)%n];
                var witnessFirst=aBound.Finite?aBound.Upper:lo;var witnessLast=bBound.Finite?bBound.Lower:hi;double at;
                if(witnessFirst!=lo||witnessLast!=hi)
                {
                    if(!TryInteriorBoundaryStation(witnessFirst,witnessLast,curve.First,curve.Last,wrap,.3819660112501051,out at))
                        return UnresolvedBoundaryStation(curve.Curve,-1,hi,bBound,lo,aBound);
                }
                else{at=lo+width*.3819660112501051;if(at>curve.Last)at=curve.First+at-curve.Last;}
                LastRegularClipStage=RegularClipStage.Witness;LastSheetPartitionStage=SheetPartitionStage.CurveEvaluation;LastSheetCurve=curve.Curve;LastSheetParameter=at;
                var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(curve.Curve)],at,0,false,point,out _);LastSheetAlgorithmStatus=status;
                if(status!=AlgorithmStatus.Success)
                {
                    TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.UnresolvedEvaluation,Curve=curve.Curve,First=at,Last=at,
                        Surface=input.Faces[first].Surface,OtherSurface=input.Faces[second].Surface});
                    return EvaluationError(status);
                }
                var firstSource=first<sourceFaces.Length?sourceFaces[first]:-1;
                var secondSource=second<sourceFaces.Length?sourceFaces[second]:-1;
                if(RegularPointOutsideNativeSupport(firstSource,point[0],model.Tolerance)||RegularPointOutsideNativeSupport(secondSource,point[0],model.Tolerance))continue;
                LastRegularClipStage=RegularClipStage.FirstUv;
                error=FiniteBoundarySurfaceParameters(input.Faces[first].Surface,curve,at,point[0],sectionCharts,out var a);
                if(error==ParasolidConstants.PK_ERROR_not_on_surface)continue;if(error!=0)return error;
                LastRegularClipStage=RegularClipStage.SecondUv;
                error=FiniteBoundarySurfaceParameters(input.Faces[second].Surface,curve,at,point[0],sectionCharts,out var b);
                if(error==ParasolidConstants.PK_ERROR_not_on_surface)continue;if(error!=0)return error;
                LastRegularClipStage=RegularClipStage.FirstTrim;
                error=RegularClipFaceContains(input,first,a,point[0],curve.Curve,at,sourceFaces,model.Tolerance,out var insideA,out var boundaryA);
                if(error!=0)return error;if(!insideA)continue;
                LastRegularClipStage=RegularClipStage.SecondTrim;
                error=RegularClipFaceContains(input,second,b,point[0],curve.Curve,at,sourceFaces,model.Tolerance,out var insideB,out var boundaryB);
                if(error!=0)return error;if(!insideB)continue;
                if(boundaryA&&boundaryB)continue;
                var cut=curve;cut.First=lo;cut.Last=hi;cut.Closed=closed?(byte)1:(byte)0;
                if(!sourceFaces.IsEmpty&&(boundaryA||boundaryB)&&!wrap&&!closed)
                    cut.InheritedSourceEdge=FiniteLineBoundarySource(input,boundaryA?first:second,cut)+1;
                WrappedCutCharts wrappedCharts=default;
                if(wrap)
                {
                    error=CloneSheetCurveArc(curve.Curve,lo,hi,true,out cut.Curve,out cut.First,out cut.Last,out wrappedCharts);if(error!=0)return error;
                    if(ownedCount==owned.Length)return ParasolidConstants.PK_ERROR_not_implemented;owned[ownedCount++]=cut;
                }
                if(cutCount==cuts.Length)return ParasolidConstants.PK_ERROR_not_implemented;cuts[cutCount++]=new(){First=first,Second=second,Cut=cut};
                error=AssociateBoundaryCarrierSurface(ref model,cut.Curve,cut.First,cut.Last,input.Faces[first].Surface);if(error!=0)return error;
                error=AssociateBoundaryCarrierSurface(ref model,cut.Curve,cut.First,cut.Last,input.Faces[second].Surface);if(error!=0)return error;
                if(cut.Closed==0)for(var end=0;end<2;end++)
                {
                    LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;LastSheetCurve=cut.Curve;
                    LastSheetParameter=end==0?cut.First:cut.Last;
                    var sourceParameter=end==0?lo:hi;
                    var anchorCount=0;
                    var cutLocator=new IntervalNumber(double.NaN,double.NaN);
                    for(var r=0;r<nRoots;r++)if(rootParameters[r]==sourceParameter&&cutLocators[r].Finite)
                    {
                        var box=cutLocators[r];cutLocator=cutLocator.Finite?new(Math.Max(cutLocator.Lower,box.Lower),Math.Min(cutLocator.Upper,box.Upper)):box;
                        if(!cutLocator.Finite||sourceParameter<cutLocator.Lower||sourceParameter>cutLocator.Upper)
                            return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    }
                    if(cutLocator.Finite)
                        anchors[anchorCount++]=new(){Carrier=curve.Curve,Generation=Curves[GetCurveSlotByTag(curve.Curve)].Header.Generation,
                            Lower=cutLocator.Lower,Upper=cutLocator.Upper};
                    var lineSurfaceWitness=false;
                    for(var r=0;r<nRoots;r++)
                    {
                        var root=roots[r];if(root.Certified==0||rootParameters[r]!=sourceParameter)continue;
                        var carrier=Curves[GetCurveSlotByTag(root.Curve)];
                        if(model.RequireBoundaryAssociations&&!cutLocator.Finite&&root.Witness==0
                            ||root.Witness==0&&carrier.Class is not (CurveClass.Line or CurveClass.ICurve))continue;
                        if(anchorCount==anchors.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                        anchors[anchorCount++]=new(){Carrier=root.Curve,Generation=carrier.Header.Generation,Surface=root.CuttingSurface,
                            Witness=root.Witness,Lower=root.Root.Lower,Upper=root.Root.Upper};
                        lineSurfaceWitness|=root.Certified is LineSurfaceCertificate or PlanePairBlendLineCertificate or PlanePairCylinderLineCertificate;
                    }
                    status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(cut.Curve)],end==0?cut.First:cut.Last,0,false,point,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                    VertexSlot sourceVertex=-1;var sourcePoint=point[0];
                    for(var r=0;r<nRoots;r++)if(rootParameters[r]==sourceParameter&&roots[r].Certified!=0&&roots[r].Root.Vertex>=0)
                    {
                        var root=roots[r].Root;
                        if(sourceVertex>=0&&sourceVertex!=root.Vertex||ProfileDistance(point[0],root.Point)>model.Tolerance)
                            return ParasolidConstants.PK_ERROR_cant_do_intersect;
                        sourceVertex=root.Vertex;sourcePoint=root.Point;
                    }
                    if(sourceVertex<0)for(var r=0;r<nRoots;r++)
                    {
                        if(rootParameters[r]!=sourceParameter
                            ||!TryCertifiedContactRootPoint(cut,roots[r],end==0?cut.First:cut.Last,point,out var retained))continue;
                        sourcePoint=retained;break;
                    }
                    // Certified original incidence selects the event;
                    // position is only a check of the cut against it.
                    var priorEvents=model.NP;
                    error=model.AddPoint(sourcePoint,sourceVertex,out var endpoint,out var reusedByPosition,anchors[..anchorCount],
                        requireProof:model.RequireBoundaryAssociations||cutLocator.Finite||lineSurfaceWitness||ForbidSpatialBoundaryReuse);if(error!=0)return error;
                    if(reusedByPosition)TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.SpatialEndpointReuse,Surface=input.Faces[first].Surface,
                        OtherSurface=input.Faces[second].Surface,Curve=curve.Curve,StartEvent=endpoint,First=sourceParameter,StartPoint=sourcePoint,
                        EndPoint=model.Points[endpoint]});
                    if(model.RequireBoundaryAssociations&&endpoint<priorEvents&&sourceVertex<0&&!cutLocator.Finite)
                        TraceRollingNetwork?.Invoke(new(){Stage=RollingNetworkStage.ProvedEndpointReuse,Cell=endpoint,Surface=input.Faces[first].Surface,
                            Other=input.Faces[second].Surface,Curve=curve.Curve,First=sourceParameter,Last=end==0?cut.First:cut.Last});
                    ref var finite=ref cuts[cutCount-1].Cut;
                    if(end==0)finite.LowerEvent=endpoint+1;else finite.UpperEvent=endpoint+1;
                    if(cutLocator.Finite)finite.NativeLocatorEnds|=(byte)(1<<end);
                    var t=end==0?cut.First:cut.Last;
                    var targetLocator=cutLocator;
                    if(wrap&&cutLocator.Finite)
                    {
                        error=model.AssociateParameter(endpoint,curve.Curve,sourceParameter,cutLocator.Lower,cutLocator.Upper);if(error!=0)return error;
                        // An analytic arc clone keeps the same angular chart;
                        // only its upper endpoint moves by one exact turn.
                        // Rebuilt ICurve charts do not have that mapping.
                        if(Curves[GetCurveSlotByTag(curve.Curve)].Class is CurveClass.Circle or CurveClass.Ellipse)
                        {
                            if(end!=0)targetLocator+=new IntervalNumber(Math.Tau,Math.BitIncrement(Math.Tau));
                            if(!targetLocator.Finite)return UnresolvedBoundaryStation(cut.Curve,endpoint,t,targetLocator,sourceParameter,cutLocator);
                        }
                        else
                        {
                            var chart=end==0?wrappedCharts.First:wrappedCharts.Last;
                            if(!chart.Available)targetLocator=UncertifiedStation;
                            else if(!chart.TryTransfer(cutLocator,out targetLocator))
                                return UnresolvedBoundaryStation(cut.Curve,endpoint,t,targetLocator,sourceParameter,cutLocator);
                        }
                        if(targetLocator.Finite&&(t<targetLocator.Lower||t>targetLocator.Upper))
                            return UnresolvedBoundaryStation(cut.Curve,endpoint,t,targetLocator,sourceParameter,cutLocator);
                    }
                    // The event was selected above by its finite source
                    // witness. A line inverse supplies a representative,
                    // not an exact station: retain the certified source
                    // position enclosure in the target's native gauge.
                    // This does not grant NativeLocatorEnds or join roots.
                    var registration=targetLocator;
                    if(!registration.Finite)for(var r=0;r<nRoots;r++)
                    {
                        if(rootParameters[r]!=sourceParameter||roots[r].Certified==0||roots[r].Witness==0
                            ||!TryCutLineParameterBox(cut.Curve,roots[r].Root,out var projected))continue;
                        registration=registration.Finite?IntersectStationBounds(registration,projected):projected;
                        if(!registration.Finite)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    }
                    error=registration.Finite?model.AssociateParameter(endpoint,cut.Curve,Math.Clamp(t,registration.Lower,registration.Upper),registration.Lower,registration.Upper)
                        :model.AssociateParameter(endpoint,cut.Curve,t);
                    if(error!=0)return error;
                    for(var r=0;r<nRoots;r++)if(rootParameters[r]==sourceParameter)
                    {
                        var root=roots[r];
                        if(root.Certified!=0)
                        {
                            error=model.AssociateParameter(endpoint,root.Curve,root.Root.Parameter,root.Root.Lower,root.Root.Upper,
                                model.RequireBoundaryAssociations&&root.Witness==0?0:root.CuttingSurface,root.Witness);if(error!=0)return error;
                            if(cutLocators[r].Finite&&targetLocator.Finite)
                                TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.NativeCutLocator,Curve=root.Curve,ProductCurve=cut.Curve,
                                    Surface=root.ContactSurface,OtherSurface=root.CuttingSurface,Edge=root.InputEdge,InputCoedge=root.InputCoedge,
                                    StartEvent=endpoint,EndEvent=endpoint,Source=-1,Face=-1,Product=-1,Witness=root.Witness,
                                    Generation=Curves[GetCurveSlotByTag(root.Curve)].Header.Generation,
                                    First=targetLocator.Lower,Last=targetLocator.Upper,SpatialError=root.Root.SpatialError});
                            TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.CertifiedRoot,Curve=root.Curve,ProductCurve=cut.Curve,
                                Surface=root.ContactSurface,OtherSurface=root.CuttingSurface,Edge=root.InputEdge,InputCoedge=root.InputCoedge,StartEvent=endpoint,EndEvent=endpoint,
                                Source=-1,Face=-1,Product=-1,Witness=root.Witness,
                                Generation=Curves[GetCurveSlotByTag(root.Curve)].Header.Generation,
                                First=root.Root.Lower,Last=root.Root.Upper,SpatialError=root.Root.SpatialError,
                                StartPoint=model.Points[endpoint],EndPoint=root.Root.Point});
                        }
                        else if(model.RequireBoundaryAssociations)
                        {
                            var carrier=Curves[GetCurveSlotByTag(root.Curve)];
                            if(TryReadExplicitSpineCurve(carrier,out _))
                            {
                                // Bound the analytic locator's floating
                                // evaluation/inversion error independently
                                // of modeling and mark/fix tolerances.
                                error=FiniteEndpointRoot(root.Curve,carrier.TMin,carrier.TMax,-1,-1,0,root.Root.Parameter,out var locator);if(error!=0)return error;
                                error=model.AssociateParameter(endpoint,root.Curve,root.Root.Parameter,locator.Lower,locator.Upper);
                            }
                            else error=model.AssociateParameter(endpoint,root.Curve,root.Root.Parameter);
                            if(error!=0)return error;
                        }
                        // Start/End describes incidence. A transverse
                        // stationary section also supplies the common ray
                        // of the two curves on the rolling face.
                        var stationary=root.Root.TransverseStationary!=0&&OtherBoundarySupport(root.ContactSurface,cut.Curve,out var sectionSupport)
                            &&SameSheetSupport(sectionSupport,root.CuttingSurface);
                        if(stationary||root.Certified!=0&&root.Root.Kind==IntersectionVectorKind.Tangent&&root.TangentSupport!=0
                            &&input.Faces[first].Surface!=root.TangentSupport&&input.Faces[second].Surface!=root.TangentSupport)
                        {error=model.AssociateTangency(endpoint,root.Curve,cut.Curve);if(error!=0)return error;}
                        if(root.ContactSupport!=0)
                        {error=model.AssociateSurfaceContact(endpoint,root.ContactSurface,root.ContactSupport);if(error!=0)return error;}
                    }
                }
            }
        }
        return 0;
    }

    private static int RegularClipFaceContains(in RegularBoundaryView input,BufferOffset face,PK_UV_s uv,in KernelVector3 point,
        CurveTag cut,double station,ReadOnlySpan<FaceSlot> sources,double precision,out bool inside,out bool boundary)
    {
        var error=RegularFiniteFaceContains(input,face,uv,point,out inside,out boundary);
        if(error is not (ParasolidConstants.PK_ERROR_eval_failure or ParasolidConstants.PK_ERROR_cant_do_intersect)||sources.IsEmpty)return error;
        var surface=Surfaces[GetSurfaceSlotByTag(input.Faces[face].Surface)];
        if(surface.Class!=SurfaceClass.Cylinder||sources[face]<0||!TryThirdFacePrecision(sources[face],ref precision))return error;
        // An increasing meridian can run into a finite trim vertex or
        // follow a generator. Test the opposite ray using the same cut
        // station enclosure; a sample offset could move across the trim.
        if(!TryFiniteCarrierExtent(new(){Curve=cut,First=station,Last=station},Curves[GetCurveSlotByTag(cut)],out var box)
            ||CertifyCylindricalRootOutsideTrim(input,face,surface,box,precision,increasing:false)!=AlgorithmStatus.Success)return error;
        inside=false;return 0;
    }

    private static bool CertifiedRootMatchesCutAnchor(in RegularBoundaryRoot root,CurveTag cut,double parameter,Span<KernelVector3> point)
    {
        if(root.Curve==cut)return parameter>=root.Root.Lower&&parameter<=root.Root.Upper;
        if(EvaluateCurveCore(in Curves[GetCurveSlotByTag(cut)],parameter,0,false,point,out _)!=AlgorithmStatus.Success
            ||!TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(root.Curve)],out var source))return false;
        var p=new IntervalVector(point[0]);var q=new IntervalVector(root.Root.Point);
        var roundoff=(32*SmallLinearSolve.MachineEpsilon*(p.Norm().Value+q.Norm().Value+new IntervalVector(source.Origin).Norm().Value
            +new IntervalVector(source.First).Norm().Value+new IntervalVector(source.Second).Norm().Value)).Upper;
        if((p-q).Norm().Value.Upper>roundoff)return false;
        var vector=new PK_VECTOR_s();WriteVector(&vector,point[0]);double native;
        if(CurveParameteriseVectorCore(root.Curve,vector,&native)!=0)return false;
        LiftPeriodicEdgeParameter(root.Curve,root.Root.Lower,root.Root.Upper,ref native);
        return native>=root.Root.Lower&&native<=root.Root.Upper;
    }

    private static bool TryCutLineParameterBox(CurveTag cut,in FiniteCurveSurfaceRoot root,out IntervalNumber parameter)
    {
        parameter=default;var record=Curves[GetCurveSlotByTag(cut)];
        if(record.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(record,out var line)||!double.IsFinite(root.SpatialError))return false;
        var axis=new IntervalVector(line.First);var speed=axis.Norm().Value;
        if(!(speed.Lower>0))return false;
        var projection=IntervalVector.Dot(new IntervalVector(root.Point)-new IntervalVector(line.Origin),axis).Value;
        var error=(speed*root.SpatialError).Upper;
        parameter=(projection+new IntervalNumber(-error,error))/IntervalVector.Dot(axis,axis).Value;
        return parameter.Finite;
    }

    private static int FiniteBoundarySurfaceParameters(SurfTag surface,in SheetCutCurve cut,double parameter,in KernelVector3 point,
        ReadOnlySpan<FiniteSectionChart> sectionCharts,out PK_UV_s uv)
    {
        var curve=cut.Curve;
        var record=Curves[GetCurveSlotByTag(curve)];
        if(TryFiniteSectionChart(sectionCharts,surface,curve,cut.First,cut.Last,out var section))
            return EvaluateFiniteSectionChart(section,record,parameter,true,out uv,out _,out _);
        var contactError=IsRationalContact(surface,record,cut.First,cut.Last,out var rationalContact);
        if(contactError!=0)return contactError;
        if(rationalContact)return SheetCurveUV(surface,curve,parameter,true,out uv,out _,out _,cut.First,cut.Last);
        if(TryAnalyticBlendContactMap(surface,record,record.TMin,record.TMax,out _,out _,out _)
            ||record.Class==CurveClass.ICurve&&(TryCertifySupportChart(ICurveDataPool[record.DataIndex].Surface0Tag,surface,out _)
                ||TryCertifySupportChart(ICurveDataPool[record.DataIndex].Surface1Tag,surface,out _)))
            return SheetCurveUV(surface,curve,parameter,true,out uv,out _,out _);
        var found=IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(surface)],point,out uv);
        return found==AlgorithmStatus.InvalidInput?ParasolidConstants.PK_ERROR_not_on_surface:IntersectionError(found);
    }

    private static int SplitRegularSourceEdges(in RegularBoundaryView input,ReadOnlySpan<EdgeSlot> original,
        Span<SheetSourceEdge> sources,Span<SheetCutCurve> owned,ref BufferCount ownedCount,ref SheetArrangement model,bool coincident=false)
    {
        Span<double> parameters=stackalloc double[SheetPointLimit+2];Span<BufferOffset> vertices=stackalloc BufferOffset[SheetPointLimit+2];
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[SheetPointLimit+2];
        for(var e=0;e<input.Edges.Length;e++)
        {
            var edge=input.Edges[e];var slot=GetCurveSlotByTag(edge.Curve);var positive=LocalSenseSign(Curves[slot].Sense)>0;var ring=edge.StartVertex<0;var n=0;
            var lowerEvent=ring?-1:positive?edge.StartVertex:edge.EndVertex;var upperEvent=ring?-1:positive?edge.EndVertex:edge.StartVertex;
            if(!ring)
            {
                bounds[n]=new(edge.Start);parameters[n]=edge.Start;vertices[n++]=lowerEvent;
                bounds[n]=new(edge.End);parameters[n]=edge.End;vertices[n++]=upperEvent;
            }
            for(var p=0;p<model.NP;p++)
            {
                if(!model.LiveBoundaryPoint(p))continue;
                var vector=new PK_VECTOR_s();WriteVector(&vector,model.Points[p]);
                var known=model.KnownSourceBoundaryParameter(p,edge.Curve,e,out var anchor,out var otherOccurrence);var t=anchor.Parameter;
                if(!known&&(model.RequireBoundaryAssociations||otherOccurrence))continue;
                var error=known?0:CurveParameteriseVectorCore(edge.Curve,vector,&t);
                if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return error;
                var bound=UncertifiedStation;
                if(known&&HasBoundedStation(anchor))
                {
                    error=LocateBoundedStation(edge.Curve,p,anchor,edge.Start,edge.End,lowerEvent,upperEvent,out t,out bound,out var inside);
                    if(error!=0)return error;if(!inside)continue;
                }
                else
                {
                    LiftPeriodicEdgeParameter(edge.Curve,edge.Start,edge.End,ref t);
                    if(!OperationIntersection.InInterval(t,edge.Start,edge.End))continue;
                }
                if(coincident&&!model.VertexPrecisions.IsEmpty&&model.OriginalVertices[p]>=0
                    &&Vertices[model.OriginalVertices[p]].Body!=Edges[original[e]].Body
                    &&t>edge.Start+1e-10&&t<edge.End-1e-10)
                {
                    // A planar vertex-on-edge imprint is a local-precision
                    // merge only if surviving cells use both source bodies.
                    if(RegularPlanarImprintJunction(input,p,e)&&!ExactCoincidentImprintStation(ref model,p,edge.Curve,e,t))
                        model.VertexMergeCandidates[p]=1;
                }
                var duplicate=false;for(var k=0;k<n;k++)
                {
                    if(vertices[k]==p){duplicate=true;break;}
                    if(bound.Finite&&bounds[k].Finite)continue;
                    if(model.RequireBoundaryAssociations?parameters[k]!=t:Math.Abs(parameters[k]-t)>=1e-10)continue;
                    duplicate=true;break;
                }
                if(!duplicate){if(n==parameters.Length)return ParasolidConstants.PK_ERROR_not_implemented;bounds[n]=bound;parameters[n]=Math.Clamp(t,edge.Start,edge.End);vertices[n++]=p;}
            }
            var order=SortBoundaryStations(edge.Curve,parameters[..n],vertices[..n],bounds[..n]);if(order!=0)return order;
            var start=model.NE;
            // Every original edge has its own contiguous fragment range.
            // Its coedges share that range; a different original edge must
            // not lose its occurrence to geometric carrier deduplication.
            if(ring&&n==0){var error=model.AppendEdge(edge.Curve,edge.Start,edge.End,-1,-1,false,out _);if(error!=0)return error;}
            else for(var i=0;i<(ring?n:n-1);i++)
            {
                var next=(i+1)%n;var wrap=ring&&next==0;var curve=edge.Curve;var lo=parameters[i];var hi=parameters[next];
                if(wrap)
                {
                    var error=CloneSheetCurveArc(edge.Curve,lo,hi,true,out curve,out lo,out hi);if(error!=0)return error;
                    if(ownedCount==owned.Length)return ParasolidConstants.PK_ERROR_not_implemented;owned[ownedCount++]=new(){Curve=curve};
                }
                var add=model.AppendEdge(curve,lo,hi,positive?vertices[i]:vertices[next],positive?vertices[next]:vertices[i],false,out _);if(add!=0)return add;
            }
            sources[e]=new(){Source=original[e],InputEdge=e,First=start,Count=model.NE-start};
        }
        return 0;
    }
}

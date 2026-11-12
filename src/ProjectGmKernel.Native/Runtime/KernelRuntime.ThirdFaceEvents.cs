using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Command local: one clipped use and one actual source face stay
    // immutable through this entire pass.
    private struct ThirdFaceBounds
    {
        internal IntervalBox3 Box;
        internal double Margin;
        internal bool Checked,Available;
    }

    // The cut has already passed the two original finite face trims.
    // Test a third occurrence against that interval, not the mathematical
    // extension used by the pairwise intersection provider.
    private static int ReadThirdFaceEvents(in RegularBoundaryView input,BufferOffset face,FaceSlot source,
        scoped Span<RegularFaceCut> cuts,BufferOffset cutIndex,ref ThirdFaceBounds cutBounds,ref ThirdFaceBounds faceBounds,
        ref SheetArrangement model,out SurfacePairRelation relation)
    {
        relation=SurfacePairRelation.Unresolved;
        ref readonly var cut=ref cuts[cutIndex].Cut;
        LastSheetPartitionStage=SheetPartitionStage.BoundaryIntersections;LastSheetCurve=cut.Curve;LastSheetParameter=cut.First;
        var surface=input.Faces[face].Surface;var surfaceRecord=Surfaces[GetSurfaceSlotByTag(surface)];
        var curve=Curves[GetCurveSlotByTag(cut.Curve)];
        if(!faceBounds.Checked)
        {
            faceBounds.Checked=true;faceBounds.Margin=model.Tolerance;
            faceBounds.Available=(surfaceRecord.Class is SurfaceClass.Plane or SurfaceClass.Cylinder)&&source>=0&&TryThirdFacePrecision(source,ref faceBounds.Margin)
                &&RegularFiniteFaceExtent(source,out faceBounds.Box)==AlgorithmStatus.Success;
        }
        var havePlane=surfaceRecord.Class==SurfaceClass.Plane&&faceBounds.Available;
        var haveCylinder=surfaceRecord.Class==SurfaceClass.Cylinder&&faceBounds.Available;var margin=faceBounds.Margin;
        if(havePlane||haveCylinder)
        {
            if(!cutBounds.Checked)
            {
                cutBounds.Checked=true;
                cutBounds.Available=TryFiniteCarrierExtent(cut,curve,out cutBounds.Box);
            }
            if(cutBounds.Available&&ThirdFaceBoxesDisjoint(cutBounds.Box,faceBounds.Box,margin))
            {
                LastSheetAlgorithmStatus=AlgorithmStatus.Success;relation=SurfacePairRelation.Empty;
                return 0;
            }
        }

        if(curve.Class==CurveClass.Line&&surfaceRecord.Class==SurfaceClass.Plane
            &&TryPrepareSurface(surfaceRecord,out var plane)&&TryReadExplicitSpineCurve(curve,out var line)
            &&ExplicitCurveExactlyOnPlane(line,plane))
            return ReadCoplanarThirdLine(input,face,cuts,cutIndex,curve,ref model,out relation);

        var canJoin=surfaceRecord.Class==SurfaceClass.Plane&&curve.Class==CurveClass.Line&&cuts[cutIndex].First>=0&&cuts[cutIndex].Second>=0;
        var mixedTriple=IsCylinderPlaneTriple(input,cuts[cutIndex],face);
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        Span<ScalarRootIsolation> isolation=stackalloc ScalarRootIsolation[IntersectionPointCapacity];
        Span<double> spatialErrors=stackalloc double[IntersectionPointCapacity];
        var count=0;
        var proofStatus=CertifyThirdSurfaceRoots(surface,surfaceRecord,cut,curve,roots,isolation,spatialErrors,out count);
        {
            LastSheetAlgorithmStatus=proofStatus;
            if(proofStatus==AlgorithmStatus.NotConverged&&canJoin&&(cut.LowerEvent!=0||cut.UpperEvent!=0))
            {
                var endpointJoin=JoinThreePlaneCutRoots(input,face,cuts,cutIndex,ref model,out var joined);
                if(endpointJoin!=0)return endpointJoin;
                if(joined){LastSheetAlgorithmStatus=AlgorithmStatus.Success;relation=SurfacePairRelation.PointContact;return 0;}
                endpointJoin=ReadAcceptedThirdLineEnd(input,face,cuts,cutIndex,ref model,out joined);
                if(endpointJoin!=0)return endpointJoin;
                if(joined){LastSheetAlgorithmStatus=AlgorithmStatus.Success;relation=SurfacePairRelation.PointContact;return 0;}
            }
            if(proofStatus!=AlgorithmStatus.Success)return IntersectionError(proofStatus);
            var resolved=SurfacePairRelation.Empty;
            for(var i=0;i<count;i++)
            {
                var point=roots[i].Point;var error=spatialErrors[i];
                var radius=new IntervalNumber(-error,error);
                var x=new IntervalNumber(point.X)+radius;var y=new IntervalNumber(point.Y)+radius;var z=new IntervalNumber(point.Z)+radius;
                var rootBox=new IntervalBox3(x.Lower,x.Upper,y.Lower,y.Upper,z.Lower,z.Upper);
                if((havePlane||haveCylinder)&&ThirdFaceBoxesDisjoint(rootBox,faceBounds.Box,margin))continue;
                if(HasBlendPlaneLineJoint(input,cuts[cutIndex],face,cutIndex,i,isolation[i],ref model))
                {resolved=SurfacePairRelation.PointContact;continue;}
                if(canJoin)
                {
                    var errorJoin=JoinThreePlaneCutRoots(input,face,cuts,cutIndex,ref model,out var joined);
                    if(errorJoin!=0)return errorJoin;
                    if(joined){resolved=SurfacePairRelation.PointContact;continue;}
                }
                var mixedProof=AlgorithmStatus.Unsupported;
                if(mixedTriple)
                {
                    var mixed=JoinCylinderPlaneCutRoots(input,face,cuts,cutIndex,roots[i],isolation[i],error,ref model,out var mixedJoined,out mixedProof);
                    if(mixed!=0)return mixed;
                    if(mixedJoined){resolved=SurfacePairRelation.PointContact;continue;}
                }
                var insideTrim=false;
                var ownership=havePlane?CertifiedPlanarRootOwnership(input,face,surfaceRecord,point,rootBox,margin,out insideTrim)
                    :haveCylinder?CertifyCylindricalRootOutsideTrim(input,face,surfaceRecord,rootBox,margin)
                    :AlgorithmStatus.Unsupported;
                if(ownership==AlgorithmStatus.Success&&!insideTrim)
                {
                    TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.ThirdFaceOutsideTrim,Curve=cut.Curve,Generation=curve.Header.Generation,
                        Surface=surface,Source=source,Face=face,First=isolation[i].Root.Lower,Last=isolation[i].Root.Upper,SpatialError=error});
                    continue;
                }
                if(havePlane&&ownership==AlgorithmStatus.Success&&insideTrim&&cut.KeepImprint!=0&&curve.Class==CurveClass.Line)
                {
                    var reuse=ReuseImprintThirdLineRoot(input,cuts,cutIndex,surface,roots[i],isolation[i],ref model,out var joined);
                    if(reuse!=0)return reuse;
                    if(joined){resolved=SurfacePairRelation.PointContact;continue;}
                }
                // No uncorrelated point is admitted to a guarded graph.
                LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;LastSheetParameter=roots[i].First;
                LastSheetAlgorithmStatus=mixedProof!=AlgorithmStatus.Unsupported?mixedProof
                    :ownership==AlgorithmStatus.Success?AlgorithmStatus.Unsupported:ownership;
                return IntersectionError(LastSheetAlgorithmStatus);
            }
            relation=resolved;return 0;
        }

    }

    private static AlgorithmStatus CertifyThirdSurfaceRoots(SurfTag surface,in SurfaceRecord surfaceRecord,in SheetCutCurve cut,in CurveRecord curve,
        Span<OperationIntersectionPoint> roots,Span<ScalarRootIsolation> isolation,Span<double> spatialErrors,out BufferCount count)
    {
        count=0;
        if(surfaceRecord.Class==SurfaceClass.Cylinder&&TryReadExplicitSpineCurve(curve,out _))
        {
            var edge=new ModelingEdge{Curve=cut.Curve,Start=cut.First,End=cut.Last};
            return IsolateFiniteExplicitCylinder(edge,surface,roots,isolation,spatialErrors,out count);
        }
        if(surfaceRecord.Class!=SurfaceClass.Plane||!TryPrepareSurface(surfaceRecord,out var plane))return AlgorithmStatus.Unsupported;
        if(TryReadExplicitSpineCurve(curve,out var spine))
        {
            var normal=new IntervalVector(plane.Axis);
            var offset=IntervalVector.Dot(new IntervalVector(spine.Origin)-new IntervalVector(plane.Origin),normal).Value;
            var u=IntervalVector.Dot(new IntervalVector(spine.First),normal).Value;
            var t=new IntervalNumber(cut.First,cut.Last);
            var distance=offset+u*t;
            if(curve.Class!=CurveClass.Line)
            {
                IntervalDual.SinCos(new(t,0),out var sine,out var cosine);
                distance=offset+u*cosine.Value+IntervalVector.Dot(new IntervalVector(spine.Second),normal).Value*sine.Value;
            }
            if(distance.Finite&&!distance.ContainsZero)return AlgorithmStatus.Success;
        }
        if(curve.Class==CurveClass.Line)
        {
            var edge=new ModelingEdge{Curve=cut.Curve,Start=cut.First,End=cut.Last};
            if(!TryFiniteLinePlaneEvent(edge,-1,surface,out var result))return AlgorithmStatus.NotConverged;
            count=result.Count;if(count==0)return AlgorithmStatus.Success;
            var root=result.FirstRoot;
            roots[0]=new(){Point=root.Point,First=root.Parameter,Kind=root.Kind};spatialErrors[0]=root.SpatialError;
            isolation[0]=new(){Root=new(root.Lower,root.Upper),MonotoneDomain=new(edge.Start,edge.End),Certified=true,
                Increasing=IntervalVector.Dot(new(spine.First),new(plane.Axis)).Value.Lower>0};
            return AlgorithmStatus.Success;
        }
        if(curve.Class is CurveClass.Circle or CurveClass.Ellipse)
            return IsolateFiniteExplicitLevelSet(new(){Curve=cut.Curve,Start=cut.First,End=cut.Last},plane,roots,isolation,spatialErrors,out count);
        if(curve.Class!=CurveClass.ICurve)return AlgorithmStatus.Unsupported;
        var data=ICurveDataPool[curve.DataIndex];
        if(data.IsBlendContact!=0||RegularCoincidentSupports(surface,data.Surface0Tag)
            ||RegularCoincidentSupports(surface,data.Surface1Tag))return AlgorithmStatus.Unsupported;
        return IntersectFiniteIntersectionCurvePlane(cut.Curve,cut.First,cut.Last,plane,roots,out count,isolation,spatialErrors);
    }

    private static bool TryFiniteCarrierExtent(in SheetCutCurve cut,in CurveRecord curve,out IntervalBox3 extent)
    {
        extent=default;
        if(curve.Class==CurveClass.ICurve)
            return CertifiedIntersectionCurveExtent(cut.Curve,cut.First,cut.Last,out extent)==AlgorithmStatus.Success;
        if(!TryReadExplicitSpineCurve(curve,out var spine)
            ||SpineParameterMap.Interval(spine,new(new(cut.First,cut.Last),0),out var box,out _)!=AlgorithmStatus.Success||!box.Finite)return false;
        extent=new(box.X.Value.Lower,box.X.Value.Upper,box.Y.Value.Lower,box.Y.Value.Upper,box.Z.Value.Lower,box.Z.Value.Upper);
        return true;
    }

    private static bool TryThirdFacePrecision(FaceSlot face,ref double precision)
    {
        precision=Math.Max(precision,Faces[face].Tolerance);
        var loop=Faces[face].FirstLoop;
        for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            var fin=Loops[loop].FirstFin;
            for(var f=0;f<Loops[loop].FinCount;f++,fin=Fins[fin].NextInLoop)
            {
                var slot=Fins[fin].Edge;if(slot<0)return false;
                var edge=Edges[slot];precision=Math.Max(precision,edge.Tolerance);
                if(edge.StartVertex>=0)precision=Math.Max(precision,Vertices[edge.StartVertex].Tolerance);
                if(edge.EndVertex>=0)precision=Math.Max(precision,Vertices[edge.EndVertex].Tolerance);
            }
        }
        return double.IsFinite(precision);
    }

    private static bool ThirdFaceBoxesDisjoint(in IntervalBox3 curve,in IntervalBox3 face,double tolerance)
        =>ThirdFaceAxisSeparated(curve.XLo,curve.XHi,face.XLo,face.XHi,tolerance)
            ||ThirdFaceAxisSeparated(curve.YLo,curve.YHi,face.YLo,face.YHi,tolerance)||ThirdFaceAxisSeparated(curve.ZLo,curve.ZHi,face.ZLo,face.ZHi,tolerance);

    private static bool ThirdFaceAxisSeparated(double firstLow,double firstHigh,double secondLow,double secondHigh,double tolerance)
        =>firstHigh<(new IntervalNumber(secondLow)-tolerance).Lower||(new IntervalNumber(secondHigh)+tolerance).Upper<firstLow;

#if KERNEL_TEST_HOOKS
    internal static int TestFiniteThirdFaceEvents(BodyTag body,FaceSlot face,CurveTag carrier,double first,double last,out BufferCount events)
        =>TestFiniteThirdFaceEvents(body,face,carrier,first,last,out events,out _);

    internal static int TestFiniteThirdFaceEvents(BodyTag body,FaceSlot face,CurveTag carrier,double first,double last,
        out BufferCount events,out SurfacePairRelation relation,bool clipWitness=false)
    {
        events=0;relation=SurfacePairRelation.Unresolved;
        Span<BodyTag> bodies=stackalloc BodyTag[1]{body};
        Span<KernelVector3> points=stackalloc KernelVector3[64];Span<VertexSlot> vertices=stackalloc VertexSlot[64];
        Span<ModelingEdge> edges=stackalloc ModelingEdge[64];Span<EdgeSlot> originalEdges=stackalloc EdgeSlot[64];
        Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[128];Span<ModelingLoop> loops=stackalloc ModelingLoop[64];
        Span<ModelingFace> faces=stackalloc ModelingFace[64];Span<FaceSlot> originals=stackalloc FaceSlot[64];
        Span<BufferOffset> owners=stackalloc BufferOffset[64],offsets=stackalloc BufferOffset[2];
        var error=ReadRegularBoundaries(bodies,points,vertices,edges,originalEdges,coedges,loops,faces,originals,owners,offsets,
            out var np,out var ne,out var nc,out var nl,out var nf);
        if(error!=0)return error;
        var occurrence=originals[..nf].IndexOf(face);if(occurrence<0)return ParasolidConstants.PK_ERROR_bad_loop;
        var view=new RegularBoundaryView(edges[..ne],coedges[..nc],loops[..nl],faces[..nf]);
        if(clipWitness)
        {
            Span<KernelVector3> point=stackalloc KernelVector3[1];
            var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(carrier)],first,0,false,point,out _);
            if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            status=IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(faces[occurrence].Surface)],point[0],out var uv);
            if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            error=RegularClipFaceContains(view,occurrence,uv,point[0],carrier,first,originals[..nf],1e-9,out var inside,out var boundary);
            if(error==0)relation=boundary?SurfacePairRelation.PointContact:inside?SurfacePairRelation.RegularCurves:SurfacePairRelation.Empty;
            return error;
        }
        var model=new SheetArrangement{Points=points,OriginalVertices=vertices,NP=np,
            Parameters=stackalloc BoundaryParameter[128],Tolerance=1e-9};
        Span<RegularFaceCut> cuts=stackalloc RegularFaceCut[1]{new(){First=-1,Second=-1,Cut=new(){Curve=carrier,First=first,Last=last}}};
        ThirdFaceBounds cutBounds=default,faceBounds=default;
        error=ReadThirdFaceEvents(view,occurrence,face,cuts,0,
            ref cutBounds,ref faceBounds,ref model,out relation);
        events=model.NP-np;return error;
    }
#endif
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool TryExplicitNativePlaneEnd(in ModelingEdge edge,SurfTag cuttingSurface,ReadOnlySpan<VertexSlot> originals,
        Span<OperationIntersectionPoint> roots,Span<ScalarRootIsolation> stations,Span<double> errors,out BufferCount count,out FiniteCurveSurfaceRoot endpoint)
    {
        count=0;endpoint=default;var curve=Curves[GetCurveSlotByTag(edge.Curve)];
        if(curve.Class is not (CurveClass.Circle or CurveClass.Ellipse)
            ||!TryReadExplicitSpineCurve(curve,out var conic)||!TryBoundaryPlane(GetSurfaceSlotByTag(cuttingSurface),out var plane))return false;
        ConicPlaneCoefficients(conic,plane,out var a,out var b,out var h);
        var middle=edge.Start+(edge.End-edge.Start)*.5;
        for(var end=0;end<2;end++)
        {
            var upper=end!=0;var at=upper?edge.End:edge.Start;
            IntervalDual.SinCos(new(new IntervalNumber(at),0),out var sine,out var cosine);
            // sin(0)=0 and cos(0)=1 are exact identities at the declared
            // native parameter. No nearby station or quadrant is replaced.
            var value=at==0?a-h:a*cosine.Value+b*sine.Value-h;
            var derivative=at==0?b:-a*sine.Value+b*cosine.Value;
            if(value.Magnitude!=0)continue;
            var local=upper?new IntervalNumber(middle,edge.End):new IntervalNumber(edge.Start,middle);
            IntervalDual.SinCos(new(local,0),out sine,out cosine);
            var acceleration=-a*cosine.Value-b*sine.Value;
            var slope=-a*sine.Value+b*cosine.Value;
            var transverse=slope.Finite&&!slope.ContainsZero;
            if(!transverse&&(derivative.Magnitude!=0||!acceleration.Finite||acceleration.ContainsZero))continue;
            // f=0 with separated f', or f=f'=0 with separated f'', proves
            // this endpoint is the only root in this half. Isolate the rest.
            var remaining=edge;remaining.Start=upper?edge.Start:middle;remaining.End=upper?middle:edge.End;
            if(IsolateFiniteExplicitLevelSet(remaining,plane,roots,stations,errors,out count)!=AlgorithmStatus.Success){count=0;continue;}
            var first=edge.StartVertex>=0&&edge.StartVertex<originals.Length?originals[edge.StartVertex]:-1;
            var last=edge.EndVertex>=0&&edge.EndVertex<originals.Length?originals[edge.EndVertex]:-1;
            if(FiniteEndpointRoot(edge.Curve,edge.Start,edge.End,first,last,0,at,out endpoint)!=0){count=0;continue;}
            endpoint.Lower=endpoint.Upper=at;endpoint.Kind=upper?IntersectionVectorKind.End:IntersectionVectorKind.Start;
            if(count==roots.Length||count==stations.Length||count==errors.Length){count=0;return false;}
            var index=count++;
            if(!upper){roots[..index].CopyTo(roots[1..]);stations[..index].CopyTo(stations[1..]);errors[..index].CopyTo(errors[1..]);index=0;}
            roots[index]=new(){Point=endpoint.Point,First=at,Kind=endpoint.Kind};
            stations[index]=new(){Root=new(at),MonotoneDomain=local,Certified=true,Increasing=transverse&&slope.Lower>0};errors[index]=endpoint.SpatialError;
            return true;
        }
        return false;
    }

    private static bool TryStationaryNativePlaneEnd(in ModelingEdge edge,SurfTag cuttingSurface,
        ReadOnlySpan<VertexSlot> originals,Span<OperationIntersectionPoint> roots,Span<ScalarRootIsolation> stations,
        Span<double> errors,out BufferCount count,out FiniteCurveSurfaceRoot endpoint)
        =>TryExactStationaryNativePlaneEnd(edge,cuttingSurface,originals,roots,stations,errors,out count,out endpoint)
            ||TryPrecisionStationaryNativePlaneEnd(edge,cuttingSurface,originals,roots,stations,errors,out count,out endpoint);

    // FD 17.2.2 treats an unresolved pair at a tangent as one contact when
    // its normal separation is within native linear precision. This is a
    // finite endpoint event, not a certificate that f(endpoint) is exactly
    // zero. The existing ScalarRootIsolation.Certified flag stays false.
    private static bool TryPrecisionStationaryNativePlaneEnd(in ModelingEdge edge,SurfTag cuttingSurface,
        ReadOnlySpan<VertexSlot> originals,Span<OperationIntersectionPoint> roots,Span<ScalarRootIsolation> stations,
        Span<double> errors,out BufferCount count,out FiniteCurveSurfaceRoot endpoint)
    {
        count=0;endpoint=default;var curve=Curves[GetCurveSlotByTag(edge.Curve)];
        if(curve.Class!=CurveClass.ICurve||!TryBoundaryPlane(GetSurfaceSlotByTag(cuttingSurface),out var plane))return false;
        var data=ICurveDataPool[curve.DataIndex];if(data.ProgramBlock<=0||data.IsBlendContact!=0)return false;
        var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
        var spine=program.Spines[data.ProgramSpine];if(SpineParameterMap.Explicit(spine))return false;
        var charts=program.Charts.Slice(spine.ChartOffset,spine.ChartCount);var normal=new IntervalVector(plane.Axis);
        var normalLength=normal.Norm().Value;if(!(normalLength.Lower>0))return false;
        Span<IntervalNumber> atBounds=stackalloc IntervalNumber[9],bounds=stackalloc IntervalNumber[9];
        for(var end=0;end<2;end++)
        {
            var upper=end!=0;var source=upper==(LocalSenseSign(curve.Sense)>0)?edge.EndVertex:edge.StartVertex;
            if(source<0||source>=originals.Length||originals[source]<0||SheetOriginalVertex(originals[source],out var vertex)!=0)continue;
            var precision=Math.Max(OperationIntersection.DistanceTolerance,Vertices[originals[source]].Tolerance);
            var vertexGap=IntervalVector.Dot(new IntervalVector(vertex)-new IntervalVector(plane.Origin),normal).Value;
            if(!double.IsFinite(precision)||!vertexGap.Finite||vertexGap.Magnitude>(normalLength*precision).Lower)continue;
            var at=upper?edge.End:edge.Start;var chart=0;
            while(chart<charts.Length-2&&(charts[chart+1].Parameter<at||!upper&&charts[chart+1].Parameter==at))chart++;
            if(at<charts[chart].Parameter||at>charts[chart+1].Parameter)continue;
            var inner=upper?Math.Max(edge.Start,charts[chart].Parameter):Math.Min(edge.End,charts[chart+1].Parameter);
            var middle=at+(inner-at)*.5;if(middle==at)continue;
            var domain=upper?new SurfaceParameterBox(middle,at,0,0):new(at,middle,0,0);
            if(JointNestedBlendEvaluation.BoundSpine(program,data.ProgramSpine,new(at,at,0,0),atBounds)!=AlgorithmStatus.Success
                ||JointNestedBlendEvaluation.BoundSpineProjectedAcceleration(program,data.ProgramSpine,domain,plane.Axis,out var acceleration)!=AlgorithmStatus.Success
                ||!acceleration.Finite||acceleration.ContainsZero
                ||JointNestedBlendEvaluation.BoundSpine(program,data.ProgramSpine,domain,bounds)!=AlgorithmStatus.Success)continue;
            var junction=(new IntervalVector(atBounds[0],atBounds[1],atBounds[2])-new IntervalVector(vertex)).Norm().Value.Upper;
            var gap=AnalyticLevelSet.Value(atBounds,plane);var slope=AnalyticLevelSet.Derivative(atBounds,plane,3);
            if(!double.IsFinite(junction)||junction>precision||!gap.Finite||!slope.Finite)continue;
            var curvature=Math.Min(Math.Abs(acceleration.Lower),Math.Abs(acceleration.Upper));
            var critical=new IntervalNumber(slope.Magnitude)/curvature;
            var speed=new IntervalVector(bounds[3],bounds[4],bounds[5]).Norm().Value;
            if(!(speed*critical+junction).Finite||(speed*critical+junction).Upper>precision)continue;
            var minimum=new IntervalNumber(gap.Magnitude)+slope.Magnitude*critical+.5*acceleration.Magnitude*critical*critical;
            if(!minimum.Finite||minimum.Upper>(normalLength*precision).Lower)continue;
            // Taylor's inequality excludes all other zeros on the terminal
            // half beyond this one tangent cluster. No XYZ root deduplication
            // or shortened unresolved remainder is used.
            var d=new IntervalNumber(slope.Magnitude);var e=new IntervalNumber(gap.Magnitude);
            var rootWidth=(d+IntervalDual.Sqrt(new(d*d+2*curvature*e,0)).Value)/curvature;
            if(!rootWidth.Finite||rootWidth.Upper>=Math.Abs(middle-at))continue;
            var lo=upper?edge.Start:middle;var hi=upper?middle:edge.End;
            if(lo<hi&&IntersectFiniteIntersectionCurvePlane(edge.Curve,lo,hi,plane,roots,out count,stations,errors)!=AlgorithmStatus.Success){count=0;continue;}
            if(count>=roots.Length||count>=stations.Length||count>=errors.Length){count=0;return false;}
            var kind=upper?IntersectionVectorKind.End:IntersectionVectorKind.Start;
            var transverse=NativePrecisionStationaryRay(program,data,plane,originals[source],vertex,precision);
            endpoint=new(){Point=vertex,Vertex=originals[source],Kind=kind,NativeEnd=upper?FiniteNativeEnd.Last:FiniteNativeEnd.First,
                TransverseStationary=transverse?(byte)2:(byte)0,Parameter=at,Lower=at,Upper=at,SpatialError=junction};
            var index=count++;if(!upper){roots[..index].CopyTo(roots[1..]);stations[..index].CopyTo(stations[1..]);errors[..index].CopyTo(errors[1..]);index=0;}
            roots[index]=new(){Point=vertex,First=at,Kind=kind};stations[index]=new(){Root=new(at)};errors[index]=junction;
            return true;
        }
        return false;
    }

    private static bool NativePrecisionStationaryRay(in NestedBlendProgram program,in ICurveData source,
        in AnalyticSurface cutting,VertexSlot original,in KernelVector3 point,double precision)
    {
        // FD 17.2's native angular precision is distinct from a numerical
        // solver threshold. Flag 2 records a precision contact; it never
        // changes ScalarRootIsolation.Certified to an exact zero proof.
        const double nativeAngularPrecision=1e-11;
        var definition=program.Spines[source.ProgramSpine];var side=program.Surfaces[definition.First].IsBlend?0:1;
        var blend=side==0?definition.First:definition.Second;if(!program.Surfaces[blend].IsBlend)return false;
        var section=program.Surfaces[side==0?definition.Second:definition.First];
        if(section.IsBlend||section.IsBSpline||section.Analytic.Kind!=SurfaceClass.Plane)return false;
        var tag=Surfaces[GetSurfaceSlotByTag(source.Surface0Tag)].Class==SurfaceClass.BlendSurface?source.Surface0Tag:source.Surface1Tag;
        var record=Surfaces[GetSurfaceSlotByTag(tag)];if(record.Class!=SurfaceClass.BlendSurface)return false;
        var band=BlendedEdgeDataPool[record.DataIndex];var found=false;var transverse=false;
        for(var contact=0;contact<2;contact++)
        {
            var support=contact==0?band.Surface0Tag:band.Surface1Tag;
            if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(support)],out var cylinder)||cylinder.Kind!=SurfaceClass.Cylinder)continue;
            var body=Vertices[original].Body;var face=Bodies[body].FirstFaceBody;var incident=false;
            for(var f=0;f<Bodies[body].FaceCountBody;f++,face=Faces[face].NextInBody)
                if(SameSheetSupport(Faces[face].SurfTag,support)&&FaceUsesRollingVertex(face,original)){incident=true;break;}
            if(!incident)continue;if(found)return false;found=true;
            // Native incidence identifies which supporting cylinder supplies
            // the contact normal at the stored vertex. A corrected implicit
            // CHART foot is not substituted for that original endpoint.
            var axis=new IntervalVector(cylinder.Axis);var norm=IntervalVector.Dot(axis,axis).Value;if(!(norm.Lower>0))return false;
            var offset=new IntervalVector(point)-new IntervalVector(cylinder.Origin);
            var normal=offset-axis*(IntervalVector.Dot(offset,axis)/norm);var length=normal.Norm().Value;
            if(!(length.Lower>0)||(length-cylinder.Radius).Magnitude>precision)return false;
            var tangent=IntervalVector.Cross(normal,new IntervalVector(section.Analytic.Axis));var planeNormal=new IntervalVector(cutting.Axis);
            var scale=tangent.Norm().Value*planeNormal.Norm().Value;
            if(!(scale.Lower>0)||IntervalVector.Dot(tangent,planeNormal).Value.Magnitude>(scale*nativeAngularPrecision).Lower)return false;
            var ray=IntervalVector.Cross(normal,planeNormal);
            transverse=ray.Norm().Value.Lower>(normal.Norm().Value*planeNormal.Norm().Value*nativeAngularPrecision).Upper;
        }
        return found&&transverse;
    }

    private static bool TryExactStationaryNativePlaneEnd(in ModelingEdge edge,SurfTag cuttingSurface,
        ReadOnlySpan<VertexSlot> originals,Span<OperationIntersectionPoint> roots,Span<ScalarRootIsolation> stations,
        Span<double> errors,out BufferCount count,out FiniteCurveSurfaceRoot endpoint)
    {
        count=0;endpoint=default;
        var curve=Curves[GetCurveSlotByTag(edge.Curve)];
        if(curve.Class!=CurveClass.ICurve||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(cuttingSurface)],out var cutting)
            ||cutting.Kind!=SurfaceClass.Plane)return false;
        var data=ICurveDataPool[curve.DataIndex];if(data.ProgramBlock<=0||data.IsBlendContact!=0)return false;
        var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
        var implicitCurve=program.Spines[data.ProgramSpine];
        if(SpineParameterMap.Explicit(implicitCurve))return false;
        var first=program.Surfaces[implicitCurve.First];var second=program.Surfaces[implicitCurve.Second];
        var rolling=first.IsBlend?first:second;var support=first.IsBlend?second:first;
        if(!rolling.IsBlend||support.IsBlend||support.IsBSpline||support.Analytic.Kind!=SurfaceClass.Plane)return false;
        var centerSpine=program.Spines[rolling.Spine];
        if(centerSpine.Curve.Kind is not (CurveClass.Circle or CurveClass.Ellipse))return false;
        var charts=program.Charts.Slice(implicitCurve.ChartOffset,implicitCurve.ChartCount);
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9],witness=stackalloc IntervalNumber[7],exact=stackalloc IntervalNumber[7];
        var nativePositive=LocalSenseSign(curve.Sense)>0;
        for(var end=0;end<2;end++)
        {
            var upper=end!=0;
            var source=(upper==nativePositive)?edge.EndVertex:edge.StartVertex;
            if(source<0||source>=originals.Length||originals[source]<0||SheetOriginalVertex(originals[source],out var vertex)!=0)continue;
            var p=new IntervalVector(vertex);
            if(!ExactPointOnPlaneLevel(p,cutting)||!ExactPointOnPlaneLevel(p,support.Analytic))continue;
            var at=upper?edge.End:edge.Start;var chart=0;
            while(chart<charts.Length-2&&(charts[chart+1].Parameter<at||!upper&&charts[chart+1].Parameter==at))chart++;
            ref readonly var frame=ref charts[chart];
            if(at<frame.Parameter||at>charts[chart+1].Parameter)continue;
            var gauge=frame.Parameter+frame.Scale*IntervalVector.Dot(new IntervalVector(frame.Chord),p-new IntervalVector(frame.Position)).Value;
            if(!gauge.Finite||at<gauge.Lower||at>gauge.Upper)continue;
            if(JointNestedBlendEvaluation.BoundSharedSpineWitness(program,data.ProgramSpine,new(at,at,0,0),bounds,capture:witness)!=AlgorithmStatus.Success
                ||!TryExactStationaryContact(program,rolling,centerSpine.Curve,p,support.Analytic,cutting,witness[6],exact,out var transverse))continue;
            var junctionError=(new IntervalVector(bounds[0],bounds[1],bounds[2])-p).Norm().Value.Upper;
            if(!double.IsFinite(junctionError))continue;
            var interior=upper?Math.Max(edge.Start,frame.Parameter):Math.Min(edge.End,charts[chart+1].Parameter);
            if(upper?interior>=gauge.Lower:interior<=gauge.Upper)continue;
            var domain=upper?new SurfaceParameterBox(interior,gauge.Upper,0,0):new SurfaceParameterBox(gauge.Lower,interior,0,0);
            // The algebraic endpoint can extend the terminal chart by its
            // outward gauge interval only. Its original vertex still owns
            // the finite end. Strict curvature excludes another root here.
            if(JointNestedBlendEvaluation.BoundSpineProjectedAcceleration(program,data.ProgramSpine,domain,cutting.Axis,
                out var acceleration,terminalChart:chart,terminalWitness:exact)!=AlgorithmStatus.Success||acceleration.ContainsZero)continue;
            var a=upper?edge.Start:interior;var b=upper?interior:edge.End;
            if(a<b&&IntersectFiniteIntersectionCurvePlane(edge.Curve,a,b,cutting,roots,out count,stations,errors)!=AlgorithmStatus.Success)
            {count=0;continue;}
            if(count>=roots.Length||count>=stations.Length||count>=errors.Length){count=0;return false;}
            var kind=upper?IntersectionVectorKind.End:IntersectionVectorKind.Start;
            endpoint=new(){Point=vertex,Vertex=originals[source],Kind=kind,NativeEnd=upper?FiniteNativeEnd.Last:FiniteNativeEnd.First,
                TransverseStationary=transverse?(byte)1:(byte)0,Parameter=at,Lower=at,Upper=at,SpatialError=junctionError};
            var i=count++;
            if(!upper)
            {
                roots[..i].CopyTo(roots[1..]);stations[..i].CopyTo(stations[1..]);errors[..i].CopyTo(errors[1..]);i=0;
            }
            roots[i]=new(){Point=vertex,First=at,Kind=kind};
            stations[i]=new(){Root=new(at)};errors[i]=junctionError;
            return true;
        }
        return false;
    }

    private static bool ExactPointOnPlaneLevel(in IntervalVector point,in AnalyticSurface plane)
        =>IntervalVector.Dot(point-new IntervalVector(plane.Origin),new IntervalVector(plane.Axis)).Value.Magnitude==0;

    private static bool TryExactStationaryContact(in NestedBlendProgram program,in BlendSurfaceInstruction rolling,in ParametricSpineCurve spine,
        in IntervalVector point,in AnalyticSurface section,in AnalyticSurface cutting,IntervalNumber rollingStation,Span<IntervalNumber> witness,out bool transverse)
    {
        transverse=false;
        var pi=new IntervalNumber(Math.BitDecrement(Math.PI),Math.BitIncrement(Math.PI));
        if(!rollingStation.Finite||rollingStation.Upper-rollingStation.Lower>=Math.PI/2)return false;
        for(var side=0;side<2;side++)
        {
            var contact=program.Surfaces[side==0?rolling.First:rolling.Second];
            var other=program.Surfaces[side==0?rolling.Second:rolling.First];
            var offset=new IntervalNumber(side==0?rolling.Range0:rolling.Range1)*contact.NormalSense;
            var otherOffset=new IntervalNumber(side==0?rolling.Range1:rolling.Range0)*other.NormalSense;
            if(contact.IsBlend||contact.IsBSpline||contact.Analytic.Kind!=SurfaceClass.Cylinder||other.IsBlend||other.IsBSpline
                ||!ExactCylinderRadial(point,contact.Analytic,0,out var normal))continue;
            normal/=contact.Analytic.Radius;
            var tangent=IntervalVector.Cross(normal,new IntervalVector(section.Axis));
            if(IntervalVector.Dot(tangent,tangent).Value.Lower<=0
                ||IntervalVector.Dot(tangent,new IntervalVector(cutting.Axis)).Value.Magnitude!=0)continue;
            var center=point+normal*offset;
            if(other.Analytic.Kind==SurfaceClass.Cylinder)
            {
                if(!ExactCylinderRadial(center,other.Analytic,otherOffset,out _))continue;
            }
            else if(other.Analytic.Kind!=SurfaceClass.Plane
                ||(IntervalVector.Dot(center-new IntervalVector(other.Analytic.Origin),new IntervalVector(other.Analytic.Axis)).Value-otherOffset).Magnitude!=0)
                continue;
            var origin=new IntervalVector(spine.Origin);
            for(var quadrant=0;quadrant<4;quadrant++)
            {
                var axis=new IntervalVector((quadrant&1)==0?spine.First:spine.Second);
                if(quadrant>=2)axis*= -1;
                var difference=origin+axis-center;
                if(IntervalVector.Dot(difference,difference).Value.Magnitude!=0)continue;
                var lift=Math.Round((rollingStation.Midpoint-quadrant*Math.PI*.5)/Math.Tau);
                var station=pi*(quadrant*.5+2*lift);
                if(station.Lower<rollingStation.Lower||station.Upper>rollingStation.Upper)continue;
                witness[0]=point.X.Value;witness[1]=point.Y.Value;witness[2]=point.Z.Value;
                witness[3]=center.X.Value;witness[4]=center.Y.Value;witness[5]=center.Z.Value;witness[6]=station;
                var cutTangent=IntervalVector.Cross(normal,new IntervalVector(cutting.Axis));
                transverse=IntervalVector.Dot(cutTangent,cutTangent).Value.Lower>0;
                return true;
            }
        }
        return false;
    }

    private static bool ExactCylinderRadial(in IntervalVector point,in AnalyticSurface cylinder,IntervalNumber offset,out IntervalVector radial)
    {
        radial=default;var axis=new IntervalVector(cylinder.Axis);
        if((IntervalVector.Dot(axis,axis).Value-1).Magnitude!=0)return false;
        var radius=cylinder.Radius+offset;if(radius.Lower<=0)return false;
        var p=point-new IntervalVector(cylinder.Origin);radial=p-axis*IntervalVector.Dot(p,axis);
        return (IntervalVector.Dot(radial,radial).Value-(radius*radius)).Magnitude==0;
    }

#if KERNEL_TEST_HOOKS
    internal static int TestNativePlaneEdge(EdgeSlot edge,SurfTag plane,out BufferCount count,out VertexSlot vertex,out IntervalNumber station,
        out bool transverseStationary)
    {
        count=0;vertex=-1;station=UncertifiedStation;transverseStationary=false;
        var record=Edges[edge];if(!TryProfileEdgeRange(record,out var a,out var b))return ParasolidConstants.PK_ERROR_bad_parameter;
        var use=new ModelingEdge{Curve=record.CurveTag,Start=a,End=b,StartVertex=0,EndVertex=1};
        Span<VertexSlot> originals=stackalloc VertexSlot[2]{record.StartVertex,record.EndVertex};
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        Span<ScalarRootIsolation> stations=stackalloc ScalarRootIsolation[IntersectionPointCapacity];
        Span<double> errors=stackalloc double[IntersectionPointCapacity];
        var owns=EnsureCommandScratch();
        try
        {
            var kind=Curves[GetCurveSlotByTag(use.Curve)].Class;
            if(kind is CurveClass.Circle or CurveClass.Ellipse)
            {
                if(TryExplicitNativePlaneEnd(use,plane,originals,roots,stations,errors,out count,out var end))vertex=end.Vertex;
                else
                {
                    if(!TryBoundaryPlane(GetSurfaceSlotByTag(plane),out var flat))return ParasolidConstants.PK_ERROR_not_implemented;
                    var status=IsolateFiniteExplicitLevelSet(use,flat,roots,stations,errors,out count);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                }
            }
            else if(!TryNativeCurvePlaneEvents(use,plane,roots,stations,errors,out count,stackalloc ScalarRootIsolation[IntersectionPointCapacity]))
            {
                if(!TryStationaryNativePlaneEnd(use,plane,originals,roots,stations,errors,out count,out var endpoint))
                    return ParasolidConstants.PK_ERROR_cant_do_intersect;
                vertex=endpoint.Vertex;transverseStationary=endpoint.TransverseStationary!=0;
            }
            if(count==1)station=stations[0].Root;
            return 0;
        }
        finally{if(owns)ReleaseCommandScratch(State.Session);}
    }
#endif
}

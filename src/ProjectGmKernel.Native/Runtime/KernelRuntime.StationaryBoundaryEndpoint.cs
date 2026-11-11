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
            else if(!TryNativeCurvePlaneEvents(use,plane,roots,stations,errors,out count))
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

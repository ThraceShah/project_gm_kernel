using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct RollingPointContact
    {
        internal VertexSlot Vertex;
        internal EntityGeneration Generation;
        internal FaceSlot Face;
        internal SurfTag Band;
        internal double First,Last,SpatialError;
        internal PK_UV_s FaceUv;
    }

    // A common tangent plane meets the intersection of two nonparallel
    // cylinder solids in at most one point: each individual cylinder meets
    // it on an axial generator, and the two generators are nonparallel.
    // Inward rolling balls are contained in both solids. Thus an existing
    // source vertex on both generators is a complete point-contact event,
    // not an empty SSI and not an untraced curve near a collapsed section.
    private static bool TryRollingCylinderPointContact(in RollingBandCandidate band,FaceSlot face,out RollingPointContact contact)
    {
        contact=default;
        if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(Faces[face].SurfTag)],out var plane)||plane.Kind!=SurfaceClass.Plane)return false;
        var record=Surfaces[GetSurfaceSlotByTag(band.Surface)];if(record.Class!=SurfaceClass.BlendSurface)return false;
        var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];
        var left=program.Surfaces[root.First];var right=program.Surfaces[root.Second];
        if(left.IsBlend||right.IsBlend||left.IsBSpline||right.IsBSpline||left.Analytic.Kind!=SurfaceClass.Cylinder||right.Analytic.Kind!=SurfaceClass.Cylinder
            ||root.Range0*left.NormalSense>=0||root.Range1*right.NormalSense>=0)return false;
        var normal=new IntervalVector(plane.Axis);var norm2=IntervalVector.Dot(normal,normal).Value;
        var firstHeight=IntervalVector.Dot(new IntervalVector(left.Analytic.Origin)-new IntervalVector(plane.Origin),normal).Value;
        var secondHeight=IntervalVector.Dot(new IntervalVector(right.Analytic.Origin)-new IntervalVector(plane.Origin),normal).Value;
        var exact=ExactIntervalZero(IntervalVector.Dot(new IntervalVector(left.Analytic.Axis),normal).Value)
            &&ExactIntervalZero(IntervalVector.Dot(new IntervalVector(right.Analytic.Axis),normal).Value)
            &&ExactIntervalZero(firstHeight*firstHeight-new IntervalNumber(left.Analytic.Radius)*left.Analytic.Radius*norm2)
            &&ExactIntervalZero(secondHeight*secondHeight-new IntervalNumber(right.Analytic.Radius)*right.Analytic.Radius*norm2);
        if(firstHeight.Lower*secondHeight.Lower<=0)return false;
        var cross=ProfileCross(left.Analytic.Axis,right.Analytic.Axis);if(!(Dot(cross,cross)>1e-20))return false;
        var edge=Edges[band.Source];Span<VertexSlot> endpoints=stackalloc VertexSlot[2]{edge.StartVertex,edge.EndVertex};var found=false;
        foreach(var vertex in endpoints)
        {
            if(vertex<0||!FaceUsesRollingVertex(face,vertex))continue;
            var pointSlot=GetPointSlotByTag(Vertices[vertex].PointTag);if(pointSlot<0)return false;var point=Points[pointSlot].Position;
            var roundoff=0.0;
            if(!exact||!ExactIntervalZero(IntervalVector.Dot(new IntervalVector(point)-new IntervalVector(plane.Origin),normal).Value)
                ||!PointExactlyOnCylinder(left.Analytic,point)||!PointExactlyOnCylinder(right.Analytic,point))
            {
                if(!TryReferenceMeridianContact(left.Analytic,plane,point,out var leftError)
                    ||!TryReferenceMeridianContact(right.Analytic,plane,point,out var rightError)
                    ||!HasSourceMeridianUse(face,band.FirstSupport,vertex,left.Analytic)
                    ||!HasSourceMeridianUse(face,band.SecondSupport,vertex,right.Analytic))continue;
                roundoff=Math.Max(leftError,rightError);
            }
            var unit=Scale(plane.Axis,1/OperationIntersection.Norm(plane.Axis));
            var center=Add(point,Scale(unit,Math.Sign(firstHeight.Lower)*band.Radius));var vector=new PK_VECTOR_s();WriteVector(&vector,center);double t;
            if(CurveParameteriseVectorCore(data.SpineTag,vector,&t)!=0)return false;
            LiftPeriodicEdgeParameter(data.SpineTag,band.First,band.Last,ref t);
            var ellipse=EllipseDataPool[Curves[GetCurveSlotByTag(data.SpineTag)].DataIndex];
            var error=Vertices[vertex].Tolerance+roundoff;var parameterError=error/Math.Min(ellipse.R1,ellipse.R2)+32*2.2204460492503131e-16*Math.Max(1,Math.Abs(t));
            var atFirst=Math.Abs(t-band.First)<=parameterError;var atLast=Math.Abs(t-band.Last)<=parameterError;
            if(atFirst==atLast||found)return false;
            if(IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(Faces[face].SurfTag)],point,out var uv)!=Computation.AlgorithmStatus.Success)return false;
            contact=new(){Vertex=vertex,Generation=Vertices[vertex].Header.Generation,Face=face,Band=band.Surface,First=Math.BitDecrement(t-parameterError),Last=Math.BitIncrement(t+parameterError),SpatialError=error,FaceUv=uv};found=true;
        }
        return found;
    }

    private static bool ExactIntervalZero(IntervalNumber value)=>value.Lower==0&&value.Upper==0;

    // A shared reference meridian is a discrete analytic-frame identity.
    // Rigidly transported frame vectors and generator incidences retain it
    // even though their binary64 dot products no longer cancel exactly.
    // The residual bound covers frame/location arithmetic only, independently
    // of entity precision and mark/fix tolerances. Source incidence is also
    // required by the caller; a small geometric residual alone is insufficient.
    internal static bool TryReferenceMeridianContact(in AnalyticSurface cylinder,in AnalyticSurface plane,in KernelVector3 point,out double error)
    {
        error=0;if(cylinder.Kind!=SurfaceClass.Cylinder||plane.Kind!=SurfaceClass.Plane)return false;
        var sign=ExactVector(cylinder.X,plane.Axis)?1:ExactVector(Scale(cylinder.X,-1),plane.Axis)?-1:0;if(sign==0)return false;
        var unitError=32*SmallLinearSolve.MachineEpsilon;
        if(Math.Abs(Dot(cylinder.Axis,cylinder.Axis)-1)>unitError||Math.Abs(Dot(cylinder.X,cylinder.X)-1)>unitError
            ||Math.Abs(Dot(cylinder.Axis,cylinder.X))>unitError)return false;
        error=(32*SmallLinearSolve.MachineEpsilon*(new IntervalVector(cylinder.Origin).Norm().Value
            +new IntervalVector(plane.Origin).Norm().Value+new IntervalVector(point).Norm().Value+cylinder.Radius)).Upper;
        if(!(error>0)||!double.IsFinite(error))return false;
        var height=Dot(Sub(cylinder.Origin,plane.Origin),plane.Axis);if(height==0)return false;
        var radial=Scale(cylinder.X,-Math.Sign(height)*sign*cylinder.Radius);var generator=Add(cylinder.Origin,radial);
        if(Math.Abs(Dot(Sub(generator,plane.Origin),plane.Axis))>error)return false;
        var delta=Sub(point,generator);var along=Dot(delta,cylinder.Axis)/Dot(cylinder.Axis,cylinder.Axis);
        return OperationIntersection.Norm(Sub(delta,Scale(cylinder.Axis,along)))<=error;
    }

    private static bool HasSourceMeridianUse(FaceSlot face,FaceSlot support,VertexSlot vertex,in AnalyticSurface cylinder)
    {
        var loop=Faces[face].FirstLoop;
        for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            var fin=Loops[loop].FirstFin;
            for(var k=0;k<Loops[loop].FinCount;k++,fin=Fins[fin].NextInLoop)
            {
                var edge=Edges[Fins[fin].Edge];if(edge.StartVertex!=vertex&&edge.EndVertex!=vertex)continue;
                if(!LocalEdgeFaces(Fins[fin].Edge,out var a,out var b)||a!=support&&b!=support)continue;
                var curve=Curves[GetCurveSlotByTag(edge.CurveTag)];if(curve.Class!=CurveClass.Line)continue;
                var line=LineDataPool[curve.DataIndex];var axis=Vector(line.AxisX,line.AxisY,line.AxisZ);
                if(ExactVector(axis,cylinder.Axis)||ExactVector(axis,Scale(cylinder.Axis,-1)))return true;
            }
        }
        return false;
    }

    private static bool PointExactlyOnCylinder(in AnalyticSurface cylinder,in KernelVector3 point)
    {
        var delta=new IntervalVector(point)-new IntervalVector(cylinder.Origin);var axis=new IntervalVector(cylinder.Axis);
        var radial=delta-axis*IntervalVector.Dot(delta,axis);
        return ExactIntervalZero(IntervalVector.Dot(radial,radial).Value-new IntervalNumber(cylinder.Radius)*cylinder.Radius);
    }

    private static bool FaceUsesRollingVertex(FaceSlot face,VertexSlot vertex)
    {
        var loop=Faces[face].FirstLoop;
        for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            var fin=Loops[loop].FirstFin;
            for(var k=0;k<Loops[loop].FinCount;k++,fin=Fins[fin].NextInLoop)
            {var edge=Edges[Fins[fin].Edge];if(edge.StartVertex==vertex||edge.EndVertex==vertex)return true;}
        }
        return false;
    }
}

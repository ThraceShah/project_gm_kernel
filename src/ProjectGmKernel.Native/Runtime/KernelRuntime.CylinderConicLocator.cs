using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int LocateCertifiedCylinderConic(in SheetCutCurve cut,in RegularBoundaryRoot root,
        ref double parameter,ref IntervalNumber isolation,out bool handled)
    {
        handled=false;
        if(cut.Construction!=CutConstruction.AnalyticCylinderPlaneConic)return 0;
        var nativeTrim=root.Certified==NativePlaneCertificate;
        var planeTag=nativeTrim?root.CuttingSurface:root.ContactSurface;
        var cylinderTag=nativeTrim?root.ContactSurface:root.CuttingSurface;
        var record=Curves[GetCurveSlotByTag(cut.Curve)];
        if(!TryGetIntersectionConic(record,out var native)||!TryReadExplicitSpineCurve(record,out var conic)
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(planeTag)],out var plane)||plane.Kind!=SurfaceClass.Plane
            ||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(cylinderTag)],out var cylinder)||cylinder.Kind!=SurfaceClass.Cylinder)return 0;
        if(nativeTrim)
        {
            var source=Curves[GetCurveSlotByTag(root.Curve)];
            if(source.Class!=CurveClass.ICurve||root.Root.Vertex>=0||root.Root.Kind!=IntersectionVectorKind.Simple
                ||!(root.Root.SpatialError>0)||!double.IsFinite(root.Root.SpatialError))return 0;
            // The finite CHART solver proves the cutting plane equation.
            // The other equation follows from the exact cylinder support,
            // not from the root's proximity to the stored ellipse.
            var data=ICurveDataPool[source.DataIndex];
            if(data.ProgramBlock<=0||data.IsBlendContact!=0
                ||!(TryCertifySupportChart(data.Surface0Tag,cylinderTag,out _)||TryCertifySupportChart(data.Surface1Tag,cylinderTag,out _)))return 0;
        }
        handled=true;
        if(!BoundCylinderPlaneConic(conic,plane,cylinder))return ParasolidConstants.PK_ERROR_cant_do_intersect;

        var point=root.Root.Point;var error=root.Root.SpatialError;var radius=new IntervalNumber(-error,error);
        var r=new IntervalVector(point.X+radius,point.Y+radius,point.Z+radius)-new IntervalVector(conic.Origin);
        var a=new IntervalVector(conic.First);var b=new IntervalVector(conic.Second);
        var aa=IntervalVector.Dot(a,a).Value;var ab=IntervalVector.Dot(a,b).Value;var bb=IntervalVector.Dot(b,b).Value;
        var ar=IntervalVector.Dot(a,r).Value;var br=IntervalVector.Dot(b,r).Value;var det=aa*bb-IntervalNumber.Square(ab);
        if(!(det.Lower>0))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var x=(bb*ar-ab*br)/det;var y=(aa*br-ab*ar)/det;
        var period=new IntervalNumber(Math.Tau,Math.BitIncrement(Math.Tau));var quarter=period/4;
        if(y.Lower>0)isolation=IntervalDual.Atan2(y,x).Value;
        else if(y.Upper<0)isolation=-IntervalDual.Atan2(-y,x).Value;
        else if(x.Lower>0)isolation=quarter-IntervalDual.Atan2(x,y).Value;
        else if(x.Upper<0)isolation=quarter+IntervalDual.Atan2(-x,y).Value;
        else return ParasolidConstants.PK_ERROR_cant_do_intersect;
        if(!isolation.Finite)return ParasolidConstants.PK_ERROR_cant_do_intersect;

        // The forward projection supplies only a representative; the
        // certified interval still has to fit one finite native lift.
        var delta=Sub(point,native.Origin);
        var t=Math.Atan2(Dot(delta,ProfileCross(native.Axis,native.Reference))/native.Minor,Dot(delta,native.Reference)/native.Radius);
        if(t<0)t+=Math.Tau;
        isolation+=period*Math.Round((t-isolation.Midpoint)/Math.Tau);
        parameter=Math.Clamp(t,isolation.Lower,isolation.Upper);
        return 0;
    }

    private static bool BoundCylinderPlaneConic(in ParametricSpineCurve conic,in AnalyticSurface plane,in AnalyticSurface cylinder)
    {
        var origin=new IntervalVector(conic.Origin);var a=new IntervalVector(conic.First);var b=new IntervalVector(conic.Second);
        var n=new IntervalVector(plane.Axis);var oPlane=new IntervalVector(plane.Origin);
        var axis=new IntervalVector(cylinder.Axis);var oCylinder=new IntervalVector(cylinder.Origin);
        var normal=IntervalVector.Cross(a,b);var alignment=IntervalVector.Dot(n,normal);
        if(!alignment.Value.Finite||alignment.Value.ContainsZero)return false;
        // Project along the represented conic normal. The plane
        // correction does not change either represented chart coordinate.
        var centerMove=normal*(IntervalVector.Dot(n,oPlane-origin)/alignment);
        var da=normal*(-IntervalVector.Dot(n,a)/alignment);var db=normal*(-IntervalVector.Dot(n,b)/alignment);
        var ap=a+da;var bp=b+db;
        var p=Radial(origin+centerMove-oCylinder);var u=Radial(ap);var v=Radial(bp);
        var aa=IntervalVector.Dot(u,u).Value;var bb=IntervalVector.Dot(v,v).Value;var ab=IntervalVector.Dot(u,v).Value;
        var k=new IntervalNumber(Math.Min(aa.Lower,bb.Lower))-ab.Magnitude;
        var linear=(2*(new IntervalNumber(IntervalVector.Dot(p,u).Value.Magnitude)+IntervalVector.Dot(p,v).Value.Magnitude)).Upper;
        var radius=new IntervalNumber(cylinder.Radius);var constant=IntervalVector.Dot(p,p).Value-IntervalNumber.Square(radius);
        var residual=(new IntervalNumber((constant+(aa+bb)/2).Magnitude)+((aa-bb)/2).Magnitude+ab.Magnitude+linear).Upper;
        var slope=(k-linear).Lower;
        // The radial quadratic has exactly one positive root per angle.
        // Its derivative on [1/2, 3/2] bounds the drift from radius 1.
        if(!(constant.Upper<0&&slope>0&&residual<slope*.5))return false;
        var planeDrift=centerMove.Norm().Value+da.Norm().Value+db.Norm().Value;
        var positionScale=origin.Norm().Value+oPlane.Norm().Value+oCylinder.Norm().Value+a.Norm().Value+b.Norm().Value;
        var arithmetic=(64*SmallLinearSolve.MachineEpsilon*positionScale).Upper;
        // Check squared radial error in its own coordinates. The major
        // axis amplifies the resulting fractional drift in space.
        var radialArithmetic=(arithmetic*(p.Norm().Value+u.Norm().Value+v.Norm().Value+radius)).Upper;
        var drift=planeDrift+new IntervalNumber(residual)/slope*(ap.Norm().Value+bp.Norm().Value);
        return planeDrift.Finite&&planeDrift.Upper<=arithmetic&&residual<=radialArithmetic
            &&drift.Finite&&drift.Upper<=OperationIntersection.DistanceTolerance/16;

        IntervalVector Radial(IntervalVector x)=>x-axis*IntervalVector.Dot(x,axis);
    }

#if KERNEL_TEST_HOOKS
    internal static bool TestCylinderPlaneConic(CurveTag curve,SurfTag plane,SurfTag cylinder)
        =>TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(curve)],out var conic)
            &&TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(plane)],out var planar)
            &&TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(cylinder)],out var cylindrical)
            &&BoundCylinderPlaneConic(conic,planar,cylindrical);
#endif
}

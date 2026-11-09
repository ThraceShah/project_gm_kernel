using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus IsolateRollingMeridianLine(in SurfaceRecord surface,in ParametricSpineCurve line,
        in SurfaceIntersectionPatch path,ReadOnlySpan<IntervalNumber> poles,double first,double last,
        Span<OperationIntersectionPoint> roots,Span<ScalarRootIsolation> stations,Span<double> errors,
        Span<IntervalNumber> rollingStations,out BufferCount count)
    {
        count=0;
        if(surface.Class!=SurfaceClass.BlendSurface||surface.VMin!=0||surface.VMax!=1)return AlgorithmStatus.Unsupported;
        var data=BlendedEdgeDataPool[surface.DataIndex];var program=GetNestedBlendProgram(data);
        var rolling=program.Surfaces[data.ProgramRoot];var radius=Math.Abs(rolling.Range0);
        if(!(radius>0)||radius!=Math.Abs(rolling.Range1))return AlgorithmStatus.Unsupported;
        var spine=program.Spines[rolling.Spine].Curve;
        if(spine.Kind is not (CurveClass.Circle or CurveClass.Ellipse))return AlgorithmStatus.Unsupported;
        var origin=new IntervalVector(spine.Origin);var pi=new IntervalNumber(Math.BitDecrement(Math.PI),Math.BitIncrement(Math.PI));
        var uDomain=new IntervalNumber(surface.UMin,surface.UMax);
        if(!uDomain.Finite||!(uDomain.Upper>uDomain.Lower))return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        var status=path.Bound(new(first,last,0,0),bounds);if(status!=AlgorithmStatus.Success)return AlgorithmStatus.Unsupported;
        var lineBox=new IntervalVector(bounds[0],bounds[1],bounds[2])-origin;
        for(var quadrant=0;quadrant<4;quadrant++)
        {
            var a=new IntervalVector((quadrant&1)==0?spine.First:spine.Second);
            var b=new IntervalVector((quadrant&1)==0?spine.Second:spine.First);
            if(quadrant>=2)a*= -1;
            if(quadrant is 1 or 2)b*= -1;
            if(IntervalVector.Dot(a,b).Value.Magnitude!=0||!LineInMeridian(line,poles,origin,b))continue;
            // On this plane the nearest-center equation factors into
            // sin(u) times -(P-O)·A+(A²-B²)cos(u), in the shifted gauge.
            // A strict sign of the second factor eliminates every other
            // normal section on the entire finite line and rolling range.
            var coefficient=IntervalVector.Dot(a,a).Value-IntervalVector.Dot(b,b).Value;
            var branch=-IntervalVector.Dot(lineBox,a).Value+coefficient*new IntervalNumber(-1,1);
            if(!branch.Finite||branch.ContainsZero)continue;
            var turn=Math.Round((uDomain.Midpoint-quadrant*Math.PI*.5)/Math.Tau);
            var station=pi*(quadrant*.5+2*turn);
            if(!(station.Lower>uDomain.Lower&&station.Upper<uDomain.Upper)
                ||(station-pi).Upper>=uDomain.Lower||(station+pi).Lower<=uDomain.Upper)continue;
            var center=origin+a;
            if(!ExactValue(center.X.Value)||!ExactValue(center.Y.Value)||!ExactValue(center.Z.Value))continue;
            if(!TryMeridianContactDirection(program.Surfaces[rolling.Second],rolling.Range1,center,radius,out var start)
                ||!TryMeridianContactDirection(program.Surfaces[rolling.First],rolling.Range0,center,radius,out var end))continue;
            if(rolling.ReverseSection)(start,end)=(end,start);
            var normal=IntervalVector.Cross(start,end);
            if(!(IntervalVector.Dot(normal,normal).Value.Lower>0)
                ||IntervalVector.Dot(start,b).Value.Magnitude!=0||IntervalVector.Dot(end,b).Value.Magnitude!=0)continue;
            var c=Vector(center.X.Value.Lower,center.Y.Value.Lower,center.Z.Value.Lower);
            var sphere=new AnalyticSurface(SurfaceClass.Sphere,c,default,default,radius);
            status=SurfaceIsoPlaneRoots.Find(path,sphere,first,last,roots,ref count,strictSeparation:true,isolation:stations);
            if(status!=AlgorithmStatus.Success)return status;
            if(count>errors.Length||!rollingStations.IsEmpty&&count>rollingStations.Length)return AlgorithmStatus.OutputTooSmall;
            var retained=0;
            for(var i=0;i<count;i++)
            {
                var proof=stations[i];var t=proof.Root;
                if(!proof.Certified||t.Lower<=first||t.Upper>=last)return AlgorithmStatus.NotConverged;
                status=path.Bound(new(t.Lower,t.Upper,0,0),bounds);if(status!=AlgorithmStatus.Success)return status;
                var pointBox=new IntervalVector(bounds[0],bounds[1],bounds[2]);var radial=pointBox-center;
                var fromStart=IntervalVector.Dot(IntervalVector.Cross(start,radial),normal).Value;
                var toEnd=IntervalVector.Dot(IntervalVector.Cross(radial,end),normal).Value;
                if(fromStart.Upper<0||toEnd.Upper<0)continue;
                if(!(fromStart.Lower>0&&toEnd.Lower>0))return AlgorithmStatus.NotConverged;
                var point=roots[i];var error=(pointBox-new IntervalVector(point.Point)).Norm().Value.Upper;
                if(!double.IsFinite(error)||error>OperationIntersection.DistanceTolerance)return AlgorithmStatus.NotConverged;
                point.U=station.Midpoint;point.V=MeridianAngle(start,radial)/MeridianAngle(start,end);
                if(!double.IsFinite(point.V))return AlgorithmStatus.NotConverged;
                var at=retained++;
                while(at>0&&stations[at-1].Root.Lower>t.Lower)
                {
                    roots[at]=roots[at-1];stations[at]=stations[at-1];errors[at]=errors[at-1];
                    if(!rollingStations.IsEmpty)rollingStations[at]=rollingStations[at-1];
                    at--;
                }
                if(at>0&&stations[at-1].Root.Upper>=t.Lower||at<retained-1&&t.Upper>=stations[at+1].Root.Lower)
                    return AlgorithmStatus.NotConverged;
                roots[at]=point;stations[at]=proof;errors[at]=error;if(!rollingStations.IsEmpty)rollingStations[at]=station;
            }
            count=retained;return AlgorithmStatus.Success;
        }
        return AlgorithmStatus.Unsupported;

        static bool ExactValue(IntervalNumber x)=>x.Finite&&x.Lower==x.Upper;
        static double MeridianAngle(in IntervalVector x,in IntervalVector y)
            =>Math.Atan2(IntervalVector.Cross(x,y).Norm().Value.Midpoint,IntervalVector.Dot(x,y).Value.Midpoint);
    }

    private static bool LineInMeridian(in ParametricSpineCurve line,ReadOnlySpan<IntervalNumber> poles,in IntervalVector origin,in IntervalVector normal)
    {
        if(poles.IsEmpty)return IntervalVector.Dot(new IntervalVector(line.Origin)-origin,normal).Value.Magnitude==0
            &&IntervalVector.Dot(new IntervalVector(line.First),normal).Value.Magnitude==0;
        for(var i=0;i<2;i++)
        {
            var at=4*i;var w=poles[at+3];
            if(!(w.Lower>0)||IntervalVector.Dot(new IntervalVector(poles[at],poles[at+1],poles[at+2])-origin*w,normal).Value.Magnitude!=0)
                return false;
        }
        return true;
    }

    private static bool TryMeridianContactDirection(in BlendSurfaceInstruction support,double range,in IntervalVector center,double radius,out IntervalVector radial)
    {
        radial=default;if(support.IsBlend||support.IsBSpline)return false;
        var surface=support.Analytic;var offset=new IntervalNumber(range)*support.NormalSense;
        if(surface.Kind==SurfaceClass.Cylinder)
        {
            if(!ExactCylinderRadial(center,surface,offset,out radial))return false;
            radial=radial/(surface.Radius+offset)*(-offset/radius);
            return radial.Finite;
        }
        var axis=new IntervalVector(surface.Axis);
        if(surface.Kind!=SurfaceClass.Plane||(IntervalVector.Dot(axis,axis).Value-1).Magnitude!=0
            ||(IntervalVector.Dot(center-new IntervalVector(surface.Origin),axis).Value-offset).Magnitude!=0)return false;
        radial=axis*(-offset/radius);return radial.Finite;
    }

#if KERNEL_TEST_HOOKS
    internal static AlgorithmStatus TestRollingMeridianLine(SurfTag surface,CurveTag carrier,double first,double last,
        out BufferCount count,out IntervalNumber native,out IntervalNumber rolling)
    {
        count=0;native=rolling=UncertifiedStation;
        if(!TryReadExplicitSpineCurve(Curves[GetCurveSlotByTag(carrier)],out var line))return AlgorithmStatus.Unsupported;
        Span<BlendSpineInstruction> spine=stackalloc BlendSpineInstruction[1]{new(){Curve=line,ChartCount=2}};
        Span<BlendChartInstruction> chart=stackalloc BlendChartInstruction[2]{new(){Parameter=first},new(){Parameter=last}};
        var program=new NestedBlendProgram(default,spine,chart);var path=new SurfaceIntersectionPatch(program,0,first,last);
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[2];
        Span<ScalarRootIsolation> intervals=stackalloc ScalarRootIsolation[2];Span<double> errors=stackalloc double[2];
        Span<IntervalNumber> stations=stackalloc IntervalNumber[2];
        var status=IsolateRollingMeridianLine(Surfaces[GetSurfaceSlotByTag(surface)],line,path,default,first,last,roots,intervals,errors,stations,out count);
        if(status==AlgorithmStatus.Success&&count==1){native=intervals[0].Root;rolling=stations[0];}
        return status;
    }
#endif
}

using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Positive-weight B-splines stay inside their Cartesian control hull.
    // Monotone control projections prove each spline arc simple, and separating
    // hulls prove that different arcs do not cross. Ambiguous hulls are rejected
    // explicitly; a sampled polyline is never the sole spline validity proof.
    private static int CertifyProfileCurves(ReadOnlySpan<int> edges,KernelVector3 normal)
    {
        Span<KernelVector3> first=stackalloc KernelVector3[512];
        Span<KernelVector3> second=stackalloc KernelVector3[512];
        for(var i=0;i<edges.Length;i++)
        {
            ref readonly var a=ref Edges[edges[i]];
            var ca=Curves[GetCurveSlotByTag(a.CurveTag)].Class;
            if(ca==CurveClass.BCurve&&!ProfileSplineMonotone(in a))return ParasolidConstants.PK_ERROR_not_implemented;
            for(var j=0;j<i;j++)
            {
                ref readonly var b=ref Edges[edges[j]];
                var cb=Curves[GetCurveSlotByTag(b.CurveTag)].Class;
                if(ca==CurveClass.Line&&cb==CurveClass.Line)continue; // exact segment test in loop validation
                if(ca is CurveClass.Line or CurveClass.Circle&&cb is CurveClass.Line or CurveClass.Circle)
                {
                    if(ProfileAnalyticArcsIntersect(in a,in b,normal))return ParasolidConstants.PK_ERROR_bad_loop;
                    continue;
                }
                var n=ProfileControlHull(in a,first);var m=ProfileControlHull(in b,second);
                if(n==0||m==0)return ParasolidConstants.PK_ERROR_not_implemented;
                var joint=ProfileCommonVertex(in a,in b);
                var common=joint>=0?Points[GetPointSlotByTag(Vertices[joint].PointTag)].Position:default;
                if(!ProfileHullsSeparate(first[..n],second[..m],normal,joint>=0,common))
                    return ParasolidConstants.PK_ERROR_not_implemented;
            }
        }
        return 0;
    }

    private static bool ProfileSplineMonotone(in EdgeRecord edge)
    {
        ref readonly var data=ref BCurveDataStore[Curves[GetCurveSlotByTag(edge.CurveTag)].DataIndex];
        if(data.IsPeriodic!=0||data.IsClosed!=0||data.NVertices<2)return false;
        var poles=data.PolesSpan();var last=(data.NVertices-1)*data.VertexDim;
        var w0=data.IsRational!=0?poles[3]:1;var wn=data.IsRational!=0?poles[last+3]:1;
        var origin=Vector(poles[0]/w0,poles[1]/w0,poles[2]/w0);
        var chord=ProfileSubtract(Vector(poles[last]/wn,poles[last+1]/wn,poles[last+2]/wn),origin);
        if(!ProfileNormalize(ref chord))return false;
        double previous=double.NegativeInfinity;
        for(var i=0;i<data.NVertices;i++)
        {
            var index=i*data.VertexDim;var weight=data.IsRational!=0?poles[index+3]:1;
            if(!(weight>0))return false;
            var point=Vector(poles[index]/weight,poles[index+1]/weight,poles[index+2]/weight);
            var projection=ProfileDot(ProfileSubtract(point,origin),chord);
            if(projection<previous-1e-12)return false;
            previous=projection;
        }
        return true;
    }

    private static int ProfileControlHull(in EdgeRecord edge,Span<KernelVector3> hull)
    {
        ref readonly var curve=ref Curves[GetCurveSlotByTag(edge.CurveTag)];
        if(curve.Class==CurveClass.BCurve)
        {
            ref readonly var data=ref BCurveDataStore[curve.DataIndex];
            if(data.NVertices>hull.Length)return 0;
            var poles=data.PolesSpan();
            for(var i=0;i<data.NVertices;i++)
            {
                var index=i*data.VertexDim;var w=data.IsRational!=0?poles[index+3]:1;
                if(!(w>0))return 0;
                hull[i]=Vector(poles[index]/w,poles[index+1]/w,poles[index+2]/w);
            }
            return data.NVertices;
        }
        if(curve.Class==CurveClass.Line)
        {
            if(!TryProfileEdgeRange(in edge,out var a,out var b))return 0;
            ref readonly var line=ref LineDataPool[curve.DataIndex];
            hull[0]=Vector(line.LocationX+a*line.AxisX,line.LocationY+a*line.AxisY,line.LocationZ+a*line.AxisZ);
            hull[1]=Vector(line.LocationX+b*line.AxisX,line.LocationY+b*line.AxisY,line.LocationZ+b*line.AxisZ);
            return 2;
        }
        KernelVector3 center,axis,x;double r1,r2;
        if(curve.Class==CurveClass.Circle)
        {
            ref readonly var circle=ref CircleDataPool[curve.DataIndex];
            center=Vector(circle.CenterX,circle.CenterY,circle.CenterZ);axis=Vector(circle.AxisX,circle.AxisY,circle.AxisZ);
            x=Vector(circle.RefDirX,circle.RefDirY,circle.RefDirZ);r1=r2=circle.Radius;
        }
        else if(curve.Class==CurveClass.Ellipse)
        {
            ref readonly var ellipse=ref EllipseDataPool[curve.DataIndex];
            center=Vector(ellipse.CenterX,ellipse.CenterY,ellipse.CenterZ);axis=Vector(ellipse.AxisX,ellipse.AxisY,ellipse.AxisZ);
            x=Vector(ellipse.RefDirX,ellipse.RefDirY,ellipse.RefDirZ);r1=ellipse.R1;r2=ellipse.R2;
        }
        else return 0;
        var y=ProfileScale(ProfileCross(axis,x),r2);x=ProfileScale(x,r1);
        hull[0]=ProfileAdd(center,ProfileAdd(x,y));hull[1]=ProfileAdd(center,ProfileSubtract(x,y));
        hull[2]=ProfileSubtract(center,ProfileAdd(x,y));hull[3]=ProfileAdd(center,ProfileSubtract(y,x));
        return 4;
    }

    private static bool ProfileHullsSeparate(ReadOnlySpan<KernelVector3> a,ReadOnlySpan<KernelVector3> b,
        KernelVector3 normal,bool hasJoint,KernelVector3 joint)
    {
        // A separating axis of two convex polygons is normal to an edge of one
        // hull; considering every point pair also includes every hull edge.
        for(var set=0;set<2;set++)
        {
            var points=set==0?a:b;
            for(var i=0;i<points.Length;i++)for(var j=0;j<i;j++)
            {
                var axis=ProfileCross(ProfileSubtract(points[i],points[j]),normal);
                if(!ProfileNormalize(ref axis))continue;
                var amin=double.PositiveInfinity;var amax=double.NegativeInfinity;
                var bmin=double.PositiveInfinity;var bmax=double.NegativeInfinity;
                for(var k=0;k<a.Length;k++){var value=ProfileDot(ProfileSubtract(a[k],joint),axis);amin=Math.Min(amin,value);amax=Math.Max(amax,value);}
                for(var k=0;k<b.Length;k++){var value=ProfileDot(ProfileSubtract(b[k],joint),axis);bmin=Math.Min(bmin,value);bmax=Math.Max(bmax,value);}
                if(amin>bmax+1e-10||bmin>amax+1e-10)return true;
                if(!hasJoint)continue;
                var contact=amin>=bmax-1e-12?0:bmin>=amax-1e-12?1:-1;
                if(contact<0)continue;
                var boundary=contact==0?.5*(amin+bmax):.5*(bmin+amax);var onlyJoint=true;
                for(var k=0;k<a.Length;k++)
                    if(Math.Abs(ProfileDot(ProfileSubtract(a[k],joint),axis)-boundary)<1e-10&&ProfileDistance(in a[k],in joint)>1e-9)onlyJoint=false;
                for(var k=0;k<b.Length;k++)
                    if(Math.Abs(ProfileDot(ProfileSubtract(b[k],joint),axis)-boundary)<1e-10&&ProfileDistance(in b[k],in joint)>1e-9)onlyJoint=false;
                if(onlyJoint)return true;
            }
        }
        // A spline and its closing line have two shared endpoints. A common
        // supporting line is safe when all other spline poles are strictly on
        // one side and its monotone projection excludes an interior return.
        if(a.Length==2||b.Length==2)
        {
            var line=a.Length==2?a:b;var curve=a.Length==2?b:a;
            var axis=ProfileCross(ProfileSubtract(line[1],line[0]),normal);
            if(!ProfileNormalize(ref axis))return false;
            var sign=0;var interior=false;
            for(var k=0;k<curve.Length;k++)
            {
                var d=ProfileDot(ProfileSubtract(curve[k],line[0]),axis);
                if(Math.Abs(d)<=1e-12)
                {
                    if(ProfileDistance(in curve[k],in line[0])>1e-9&&ProfileDistance(in curve[k],in line[1])>1e-9)return false;
                    continue;
                }
                var next=d>0?1:-1;if(sign!=0&&next!=sign)return false;sign=next;interior=true;
            }
            return interior;
        }
        return false;
    }

    private static VertexSlot ProfileCommonVertex(in EdgeRecord a,in EdgeRecord b)
        =>a.StartVertex>=0&&(a.StartVertex==b.StartVertex||a.StartVertex==b.EndVertex)?a.StartVertex
            :a.EndVertex>=0&&(a.EndVertex==b.StartVertex||a.EndVertex==b.EndVertex)?a.EndVertex:-1;

    private static bool ProfileAnalyticArcsIntersect(in EdgeRecord a,in EdgeRecord b,KernelVector3 normal)
    {
        ref readonly var ca=ref Curves[GetCurveSlotByTag(a.CurveTag)];
        ref readonly var cb=ref Curves[GetCurveSlotByTag(b.CurveTag)];
        if(ca.Class==CurveClass.Line)return ProfileLineCircleIntersect(in a,in b);
        if(cb.Class==CurveClass.Line)return ProfileLineCircleIntersect(in b,in a);
        ref readonly var x=ref CircleDataPool[ca.DataIndex];ref readonly var y=ref CircleDataPool[cb.DataIndex];
        var c0=Vector(x.CenterX,x.CenterY,x.CenterZ);var c1=Vector(y.CenterX,y.CenterY,y.CenterZ);
        var delta=ProfileSubtract(c1,c0);var d=Math.Sqrt(ProfileDot(delta,delta));
        if(d<1e-10)
        {
            if(Math.Abs(x.Radius-y.Radius)>1e-10)return false;
            // Coincident supports are accepted only for disjoint native arcs.
            if(Math.Abs(x.RefDirX-y.RefDirX)+Math.Abs(x.RefDirY-y.RefDirY)+Math.Abs(x.RefDirZ-y.RefDirZ)
                +Math.Abs(x.AxisX-y.AxisX)+Math.Abs(x.AxisY-y.AxisY)+Math.Abs(x.AxisZ-y.AxisZ)>1e-10)return true;
            TryProfileEdgeRange(in a,out var a0,out var a1);TryProfileEdgeRange(in b,out var b0,out var b1);
            for(var k=-2;k<=2;k++)if(Math.Min(a1,b1+k*Math.Tau)-Math.Max(a0,b0+k*Math.Tau)>1e-12)return true;
            return false;
        }
        if(d>x.Radius+y.Radius+1e-10||d<Math.Abs(x.Radius-y.Radius)-1e-10)return false;
        var along=(x.Radius*x.Radius-y.Radius*y.Radius+d*d)/(2*d);
        var heightSquared=x.Radius*x.Radius-along*along;if(heightSquared< -1e-10)return false;
        var unit=ProfileScale(delta,1/d);var center=ProfileAdd(c0,ProfileScale(unit,along));
        var offset=ProfileScale(ProfileCross(normal,unit),Math.Sqrt(Math.Max(0,heightSquared)));
        for(var sign=-1;sign<=1;sign+=2)
        {
            var point=ProfileAdd(center,ProfileScale(offset,sign));
            if(ProfilePointOnArc(in a,point)&&ProfilePointOnArc(in b,point)&&!ProfileAllowedJoint(in a,in b,point))return true;
        }
        return false;
    }

    private static bool ProfileLineCircleIntersect(in EdgeRecord lineEdge,in EdgeRecord circleEdge)
    {
        ref readonly var line=ref LineDataPool[Curves[GetCurveSlotByTag(lineEdge.CurveTag)].DataIndex];
        ref readonly var circle=ref CircleDataPool[Curves[GetCurveSlotByTag(circleEdge.CurveTag)].DataIndex];
        var origin=Vector(line.LocationX,line.LocationY,line.LocationZ);var axis=Vector(line.AxisX,line.AxisY,line.AxisZ);
        var delta=ProfileSubtract(Vector(circle.CenterX,circle.CenterY,circle.CenterZ),origin);
        var centerParameter=ProfileDot(delta,axis);var perpendicular=ProfileSubtract(delta,ProfileScale(axis,centerParameter));
        var discriminant=circle.Radius*circle.Radius-ProfileDot(perpendicular,perpendicular);
        if(discriminant< -1e-12)return false;
        var root=Math.Sqrt(Math.Max(0,discriminant));TryProfileEdgeRange(in lineEdge,out var a,out var b);
        for(var sign=-1;sign<=1;sign+=2)
        {
            var t=centerParameter+sign*root;if(t<a-1e-10||t>b+1e-10)continue;
            var point=ProfileAdd(origin,ProfileScale(axis,t));
            if(ProfilePointOnArc(in circleEdge,point)&&!ProfileAllowedJoint(in lineEdge,in circleEdge,point))return true;
        }
        return false;
    }

    private static bool ProfilePointOnArc(in EdgeRecord edge,KernelVector3 point)
    {
        ref readonly var circle=ref CircleDataPool[Curves[GetCurveSlotByTag(edge.CurveTag)].DataIndex];
        var x=Vector(circle.RefDirX,circle.RefDirY,circle.RefDirZ);var axis=Vector(circle.AxisX,circle.AxisY,circle.AxisZ);
        var p=ProfileSubtract(point,Vector(circle.CenterX,circle.CenterY,circle.CenterZ));
        var angle=Math.Atan2(ProfileDot(p,ProfileCross(axis,x)),ProfileDot(p,x));
        TryProfileEdgeRange(in edge,out var a,out var b);angle+=Math.Ceiling((a-angle)/Math.Tau)*Math.Tau;
        return angle<=b+1e-10||angle-Math.Tau>=a-1e-10;
    }

    private static bool ProfileAllowedJoint(in EdgeRecord a,in EdgeRecord b,KernelVector3 point)
    {
        var first=a.StartVertex;var last=a.EndVertex;
        if(first>=0&&(first==b.StartVertex||first==b.EndVertex))
        {var p=Points[GetPointSlotByTag(Vertices[first].PointTag)].Position;if(ProfileDistance(in p,in point)<=1e-8)return true;}
        if(last>=0&&(last==b.StartVertex||last==b.EndVertex))
        {var p=Points[GetPointSlotByTag(Vertices[last].PointTag)].Position;if(ProfileDistance(in p,in point)<=1e-8)return true;}
        return false;
    }
}

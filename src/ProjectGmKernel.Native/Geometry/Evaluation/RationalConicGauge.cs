using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

// Finite angular chart of a rational Bezier spine on a conic. Coefficients
// are homogeneous: the positive weight cancels from angle and its slope.
internal struct RationalConicArc
{
    internal double First,Last,Phase;
    internal IntervalNumber FirstAngle,LastAngle;
    internal IntervalNumber Lift;
    internal double MinimumSpeed;
    internal sbyte Direction;
}

internal static class RationalConicGauge
{
    internal const BufferCount MaxSpans=8,Stride=4;

    internal static bool Certify(in BCurveView spline,in ParametricSpineCurve conic,double tolerance,
        Span<RationalConicArc> arcs,Span<IntervalNumber> x,Span<IntervalNumber> y,out BufferCount count)
    {
        count=0;var n=RationalSpine.SpanCount(spline);
        if(n<=0||n>arcs.Length||n*Stride>x.Length||n*Stride>y.Length
            ||conic.Kind is not(CurveClass.Circle or CurveClass.Ellipse))return false;
        var o=new IntervalVector(conic.Origin);var a=new IntervalVector(conic.First);var b=new IntervalVector(conic.Second);
        var aa=IntervalVector.Dot(a,a).Value;var ab=IntervalVector.Dot(a,b).Value;var bb=IntervalVector.Dot(b,b).Value;
        var area=aa*bb-ab*ab;if(!area.Finite||area.Lower<=0)return false;
        var size=a.Norm().Value+b.Norm().Value;var p=spline.Degree;var direction=0;
        Span<IntervalNumber> w=stackalloc IntervalNumber[Stride];
        for(var span=0;span<n;span++)
        {
            var sx=x.Slice(span*Stride,p+1);var sy=y.Slice(span*Stride,p+1);
            var poles=spline.Vertices.Slice(4*span*p,4*(p+1));var minimumWeight=double.PositiveInfinity;var outside=0.0;
            for(var i=0;i<=p;i++)
            {
                var k=4*i;w[i]=poles[k+3];minimumWeight=Math.Min(minimumWeight,poles[k+3]);
                var q=new IntervalVector(poles[k],poles[k+1],poles[k+2])-o*w[i];
                var ax=IntervalVector.Dot(q,a).Value;var by=IntervalVector.Dot(q,b).Value;
                sx[i]=(ax*bb-by*ab)/area;sy[i]=(by*aa-ax*ab)/area;
                var planeError=(q-a*sx[i]-b*sy[i]).Norm().Value;
                if(!planeError.Finite)return false;outside=Math.Max(outside,planeError.Upper);
            }
            var radial=0.0;
            for(var k=0;k<=2*p;k++)
            {
                IntervalNumber value=0;
                for(var i=Math.Max(0,k-p);i<=Math.Min(p,k);i++)
                {
                    var j=k-i;
                    value+=Factor(p,i,p,j)*(sx[i]*sx[j]+sy[i]*sy[j]-w[i]*w[j]);
                }
                if(!value.Finite)return false;radial=Math.Max(radial,value.Magnitude);
            }
            var denominator=new IntervalNumber(minimumWeight);
            if((outside/denominator+radial*size/(denominator*denominator)).Upper>tolerance)return false;
            IntervalNumber angular=default;
            for(var k=0;k<2*p;k++)
            {
                IntervalNumber value=0;
                for(var i=Math.Max(0,k-(p-1));i<=Math.Min(p,k);i++)
                {
                    var j=k-i;
                    value+=Factor(p,i,p-1,j)*(sx[i]*(sy[j+1]-sy[j])-sy[i]*(sx[j+1]-sx[j]));
                }
                if(!value.Finite)return false;angular=k==0?value:IntervalNumber.Hull(angular,value);
            }
            var sign=angular.Lower>0?1:angular.Upper<0?-1:0;
            if(sign==0||direction!=0&&sign!=direction)return false;direction=sign;
            // Each span occupies one open angular half-plane. Its endpoint
            // lift is therefore unambiguous, including across the native seam.
            var hx=(sx[0]+sx[p]).Midpoint;var hy=(sy[0]+sy[p]).Midpoint;
            for(var i=0;i<=p;i++)if((sx[i]*hx+sy[i]*hy).Lower<=0)return false;
            var phase=Math.Atan2(hy,hx);
            if(!Angle(sx[0],sy[0],phase,out var first)||!Angle(sx[p],sy[p],phase,out var last))return false;
            IntervalNumber lift=0;
            if(span>0)
            {
                var turn=Math.Round((arcs[span-1].LastAngle.Midpoint-first.Midpoint)/Math.Tau);
                lift=FullTurn*turn;
                first+=lift;last+=lift;
                if(first.Lower>arcs[span-1].LastAngle.Upper||first.Upper<arcs[span-1].LastAngle.Lower)return false;
                first=IntervalNumber.Hull(first,arcs[span-1].LastAngle);
            }
            if(sign>0?first.Upper>=last.Lower:last.Upper>=first.Lower)return false;
            var lo=RationalSpine.Boundary(spline,span);var hi=RationalSpine.Boundary(spline,span+1);
            var maxX=0.0;var maxY=0.0;
            for(var i=0;i<=p;i++){maxX=Math.Max(maxX,sx[i].Magnitude);maxY=Math.Max(maxY,sy[i].Magnitude);}
            var speed=angular*p/((new IntervalNumber(hi)-lo)*(IntervalNumber.Square(new(maxX))+IntervalNumber.Square(new(maxY))));
            if(!speed.Finite||speed.ContainsZero)return false;
            arcs[span]=new(){First=lo,Last=hi,Phase=phase,Lift=lift,FirstAngle=first,LastAngle=last,MinimumSpeed=sign>0?speed.Lower:-speed.Upper,Direction=(sbyte)sign};
        }
        if((arcs[n-1].LastAngle-arcs[0].FirstAngle).Magnitude>=Math.BitDecrement(Math.Tau))return false;
        count=n;return true;
    }

    private static IntervalNumber FullTurn=>new(Math.BitDecrement(Math.Tau),Math.BitIncrement(Math.Tau));

    internal static bool NativeAngleLift(ReadOnlySpan<RationalConicArc> arcs,double first,double last,out IntervalNumber lift)
    {
        lift=default;if(arcs.IsEmpty||!(last>first))return false;
        var a=arcs[0].FirstAngle;var b=arcs[^1].LastAngle;var domain=IntervalNumber.Hull(a,b);
        var turn=Math.Round((domain.Midpoint-(first+(last-first)*.5))/Math.Tau);
        var found=false;
        for(var k=-1;k<=1;k++)
        {
            var candidate=FullTurn*(turn+k);
            var lo=new IntervalNumber(first)+candidate;var hi=new IntervalNumber(last)+candidate;
            var low=a.Midpoint<b.Midpoint?a:b;var high=a.Midpoint<b.Midpoint?b:a;
            if(lo.Upper<low.Lower||hi.Lower>high.Upper)continue;
            if(found)return false;lift=candidate;found=true;
        }
        return found;
    }

    internal static bool LocateStation(SplineDegree degree,ReadOnlySpan<RationalConicArc> arcs,ReadOnlySpan<IntervalNumber> x,
        ReadOnlySpan<IntervalNumber> y,IntervalNumber target,out IntervalNumber station,out double inverseSlope,out bool interior)
    {
        station=default;inverseSlope=0;interior=false;var found=false;
        for(var i=0;i<arcs.Length;i++)
        {
            var arc=arcs[i];var domain=IntervalNumber.Hull(arc.FirstAngle,arc.LastAngle);
            if(target.Upper<domain.Lower||target.Lower>domain.Upper)continue;
            var sx=x.Slice(i*Stride,degree+1);var sy=y.Slice(i*Stride,degree+1);
            var lo=arc.First;var hi=arc.Last;var direction=arc.Direction;
            var low=direction>0?arc.FirstAngle:arc.LastAngle;var high=direction>0?arc.LastAngle:arc.FirstAngle;
            var certified=target.Lower>low.Upper&&target.Upper<high.Lower;
            for(var step=0;step<64;step++)
            {
                var mid=lo+(hi-lo)*.5;if(!(mid>lo&&mid<hi))break;
                if(!Evaluate(arc,sx,sy,mid,out var angle,out _))return false;
                var delta=(angle-target)*direction;
                if(delta.Upper<0)lo=mid;else if(delta.Lower>0)hi=mid;
                else
                {
                    // The whole angular derivative has one sign. Enclose
                    // the root relative to this evaluation before selecting
                    // a representable native station.
                    var bound=(new IntervalNumber(delta.Magnitude)/arc.MinimumSpeed).Upper;
                    lo=Math.Max(lo,(new IntervalNumber(mid)-bound).Lower);hi=Math.Min(hi,(new IntervalNumber(mid)+bound).Upper);
                    break;
                }
            }
            if(lo>hi)return false;
            var interval=new IntervalNumber(lo,hi);
            // A root whose station box straddles a knot has no unique
            // one-sided derivative on the current finite occurrence.
            if(found)return false;
            station=interval;found=true;interior=certified;
            if(!Evaluate(arc,sx,sy,station,out _,out var slope)||slope.ContainsZero)return false;
            inverseSlope=(new IntervalNumber(1)/slope).Midpoint;
        }
        return found&&double.IsFinite(inverseSlope);
    }

    internal static bool LocateAngle(SplineDegree degree,ReadOnlySpan<RationalConicArc> arcs,ReadOnlySpan<IntervalNumber> x,
        ReadOnlySpan<IntervalNumber> y,IntervalNumber station,out IntervalNumber angle)
    {
        angle=default;if(arcs.IsEmpty||station.Lower<arcs[0].First||station.Upper>arcs[^1].Last)return false;
        var span=0;while(span<arcs.Length-1&&station.Lower>=arcs[span].Last)span++;
        return station.Upper<=arcs[span].Last
            &&Evaluate(arcs[span],x.Slice(span*Stride,degree+1),y.Slice(span*Stride,degree+1),station,out angle,out _);
    }

    private static bool Evaluate(in RationalConicArc arc,ReadOnlySpan<IntervalNumber> x,ReadOnlySpan<IntervalNumber> y,
        IntervalNumber station,out IntervalNumber angle,out IntervalNumber slope)
    {
        angle=slope=default;var p=x.Length-1;var width=new IntervalNumber(arc.Last)-arc.First;
        var t=station.Lower==arc.First&&station.Upper==arc.First?new IntervalNumber(0)
            :station.Lower==arc.Last&&station.Upper==arc.Last?new IntervalNumber(1):(station-arc.First)/width;
        Span<IntervalNumber> a=stackalloc IntervalNumber[Stride],b=stackalloc IntervalNumber[Stride];x.CopyTo(a);y.CopyTo(b);
        for(var k=p;k>1;k--)for(var i=0;i<k;i++){a[i]=(1-t)*a[i]+t*a[i+1];b[i]=(1-t)*b[i]+t*b[i+1];}
        var dx=(a[1]-a[0])*p/width;var dy=(b[1]-b[0])*p/width;
        var px=(1-t)*a[0]+t*a[1];var py=(1-t)*b[0]+t*b[1];
        if(!Angle(px,py,arc.Phase,out angle))return false;angle+=arc.Lift;
        slope=(px*dy-py*dx)/(IntervalNumber.Square(px)+IntervalNumber.Square(py));
        return angle.Finite&&slope.Finite;
    }

    private static IntervalNumber Factor(SplineDegree p,ControlPointIndex i,SplineDegree q,ControlPointIndex j)
        =>new IntervalNumber(BSurfaceEvaluation.Binomial(p,i)*BSurfaceEvaluation.Binomial(q,j))
            /BSurfaceEvaluation.Binomial(p+q,i+j);

    private static bool Angle(IntervalNumber x,IntervalNumber y,double phase,out IntervalNumber result)
    {
        result=default;IntervalDual.SinCos(new(phase,0),out var sine,out var cosine);
        var a=x*cosine.Value+y*sine.Value;var b=y*cosine.Value-x*sine.Value;
        if(!a.Finite||!b.Finite||a.Lower<=0)return false;
        var ratio=b/a;if(!ratio.Finite)return false;
        result=new IntervalNumber(Math.BitDecrement(Math.BitDecrement(Math.Atan(ratio.Lower))),
            Math.BitIncrement(Math.BitIncrement(Math.Atan(ratio.Upper))))+phase;
        return result.Finite;
    }
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

// An open, clamped chain of positive-weight Bezier arcs. The same native
// knots and homogeneous poles supply position, station equations and bounds.
internal static class RationalSpine
{
    internal static BufferCount SpanCount(in BCurveView curve)
    {
        var p=curve.Degree;
        if(!curve.Rational||curve.Periodic||curve.Dimension!=4||p is <1 or >3
            ||curve.VertexCount<=p||(curve.VertexCount-1)%p!=0)return 0;
        var count=(curve.VertexCount-1)/p;var knots=curve.Knots;
        if(knots.Length!=curve.VertexCount+p+1||!(curve.End>curve.Start))return 0;
        for(var i=0;i<=p;i++)if(knots[i]!=curve.Start||knots[^(i+1)]!=curve.End)return 0;
        for(var i=1;i<count;i++)
        {
            var knot=knots[i*p+1];if(!(knot>knots[(i-1)*p+1]&&knot<curve.End))return 0;
            for(var j=2;j<=p;j++)if(knots[i*p+j]!=knot)return 0;
        }
        for(var i=0;i<curve.Vertices.Length;i++)
            if(!double.IsFinite(curve.Vertices[i])||i%4==3&&!(curve.Vertices[i]>0))return 0;
        return count;
    }

    internal static double Boundary(in BCurveView curve,BufferOffset span)=>curve.Knots[span*curve.Degree+1];

    private static BufferOffset Locate(in BCurveView curve,double parameter)
    {
        var last=(curve.VertexCount-1)/curve.Degree-1;var first=0;
        while(first<last)
        {
            var middle=(first+last+1)/2;
            if(parameter<Boundary(curve,middle))last=middle-1;else first=middle;
        }
        return first;
    }

    internal static AlgorithmStatus Certify(in BCurveView curve,in AnalyticSurface support)
    {
        if(support.Kind is not(SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Sphere))return AlgorithmStatus.Unsupported;
        var count=SpanCount(curve);if(count==0)return AlgorithmStatus.Unsupported;
        var p=curve.Degree;var origin=new IntervalVector(support.Origin);var axis=new IntervalVector(support.Axis);
        var radius=new IntervalNumber(support.Radius);var radius2=radius*radius;
        var tolerance=Geometry.Intersection.OperationIntersection.DistanceTolerance;
        var limit=support.Kind==SurfaceClass.Plane?new IntervalNumber(tolerance):new IntervalNumber(tolerance)*(2*radius-tolerance);
        Span<IntervalVector> centered=stackalloc IntervalVector[4];Span<IntervalNumber> weights=stackalloc IntervalNumber[4];
        for(var span=0;span<count;span++)
        {
            var poles=curve.Vertices.Slice(4*span*p,4*(p+1));var weight=double.PositiveInfinity;
            for(var i=0;i<=p;i++)
            {
                var at=4*i;weights[i]=poles[at+3];weight=Math.Min(weight,poles[at+3]);
                var q=new IntervalVector(Vector(poles[at],poles[at+1],poles[at+2]))-origin*weights[i];
                if(support.Kind==SurfaceClass.Plane)
                {
                    if(IntervalVector.Dot(q,axis).Value.Magnitude>(limit*weights[i]).Lower)return AlgorithmStatus.InvalidInput;
                }
                else centered[i]=support.Kind==SurfaceClass.Cylinder?q-axis*IntervalVector.Dot(q,axis):q;
            }
            if(support.Kind==SurfaceClass.Plane)continue;
            // Squared implicit residual has degree 2p in the homogeneous
            // numerator. Bound every Bernstein coefficient before dividing
            // by the positive square of the actual weight function.
            var allowed=(limit*weight*weight).Lower;
            for(var k=0;k<=2*p;k++)
            {
                IntervalNumber sum=0;
                for(var i=Math.Max(0,k-p);i<=Math.Min(p,k);i++)
                {
                    var j=k-i;var factor=new IntervalNumber(BSurfaceEvaluation.Binomial(p,i)*BSurfaceEvaluation.Binomial(p,j))
                        /BSurfaceEvaluation.Binomial(2*p,k);
                    sum+=factor*(IntervalVector.Dot(centered[i],centered[j]).Value-radius2*weights[i]*weights[j]);
                }
                if(!sum.Finite||sum.Magnitude>allowed)return AlgorithmStatus.InvalidInput;
            }
        }
        return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus CertifyRegular(in BCurveView curve,BufferCount count)
    {
        Span<IntervalNumber> work=stackalloc IntervalNumber[32],jet=stackalloc IntervalNumber[9];
        for(var span=0;span<count;span++)
        {
            work[0]=new(Boundary(curve,span),Boundary(curve,span+1));var pending=1;var budget=512;
            while(pending>0)
            {
                if(--budget<0)return AlgorithmStatus.NotConverged;
                var range=work[--pending];
                var status=SpanBounds(curve,span,range,jet,false);if(status!=AlgorithmStatus.Success)return status;
                if(!jet[3].ContainsZero||!jet[4].ContainsZero||!jet[5].ContainsZero)continue;
                var middle=range.Midpoint;
                if(middle<=range.Lower||middle>=range.Upper||pending+2>work.Length)return AlgorithmStatus.NotConverged;
                work[pending++]=new(middle,range.Upper);work[pending++]=new(range.Lower,middle);
            }
        }
        return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus Series(in BCurveView curve,TaylorJet parameter,out TaylorVector point,out TaylorVector tangent)
    {
        point=tangent=default;if(!parameter.Finite)return AlgorithmStatus.InvalidInput;
        var span=Locate(curve,parameter[0]);var degree=curve.Degree;
        var width=Boundary(curve,span+1)-Boundary(curve,span);var t=(parameter-Boundary(curve,span))/width;
        var poles=curve.Vertices.Slice(4*span*degree,4*(degree+1));
        Span<TaylorJet> work=stackalloc TaylorJet[4],value=stackalloc TaylorJet[4],derivative=stackalloc TaylorJet[4];
        for(var d=0;d<4;d++)
        {
            for(var i=0;i<=degree;i++)work[i]=new(poles[4*i+d],t.Order);
            for(var k=degree;k>1;k--)for(var i=0;i<k;i++)work[i]+=t*(work[i+1]-work[i]);
            derivative[d]=(work[1]-work[0])*(degree/width);value[d]=work[0]+t*(work[1]-work[0]);
        }
        var w=value[3];if(!(w[0]>0))return AlgorithmStatus.NotConverged;
        point=new(value[0]/w,value[1]/w,value[2]/w);
        var dw=derivative[3];tangent=new((derivative[0]-point.X*dw)/w,(derivative[1]-point.Y*dw)/w,(derivative[2]-point.Z*dw)/w);
        return point.Finite&&tangent.Finite?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }

    internal static AlgorithmStatus Bound(in BCurveView curve,IntervalDual parameter,out IntervalVector point,out IntervalVector tangent)
    {
        point=tangent=default;if(!parameter.Finite)return AlgorithmStatus.InvalidInput;
        var range=parameter.Value;var span=Locate(curve,range.Midpoint);var count=(curve.VertexCount-1)/curve.Degree;
        var first=Boundary(curve,span);var last=Boundary(curve,span+1);
        // The finite carrier may be certified on a small mathematical
        // extension of its first or last arc, but never across an inner knot.
        if(span>0&&range.Lower<first||span+1<count&&range.Upper>last)return AlgorithmStatus.NotConverged;
        Span<IntervalNumber> jet=stackalloc IntervalNumber[18];
        var status=SpanBounds(curve,span,range,jet,true);if(status!=AlgorithmStatus.Success)return status;
        var d=parameter.Derivative;
        point=new(new(jet[0],d*jet[3]),new(jet[1],d*jet[4]),new(jet[2],d*jet[5]));
        tangent=new(new(jet[3],d*jet[9]),new(jet[4],d*jet[10]),new(jet[5],d*jet[11]));
        return point.Finite&&tangent.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus SpanBounds(in BCurveView curve,BufferOffset span,IntervalNumber range,Span<IntervalNumber> jet,bool second)
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16];
        var size=4*(curve.Degree+1);var input=curve.Vertices.Slice(4*span*curve.Degree,size);
        for(var i=0;i<size;i++)poles[i]=input[i];
        var patch=new BezierSurfacePatch(poles[..size],curve.Degree,0,new(Boundary(curve,span),Boundary(curve,span+1),0,1));
        var box=new SurfaceParameterBox(range.Lower,range.Upper,0,0);
        var status=second?BezierSurfaceBounds.BoundSecond(patch,box,jet):BezierSurfaceBounds.Bound(patch,box,jet);
        if(status!=AlgorithmStatus.Success)return status;
        for(var i=0;i<(second?18:9);i++)if(!jet[i].Finite)return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

}

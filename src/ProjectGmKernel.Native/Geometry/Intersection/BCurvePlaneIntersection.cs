using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// All bounded roots of a positive-weight B-spline against a plane. Each knot
/// span is converted by polynomial de Boor interpolation to its homogeneous
/// scalar numerator. The complete polynomial is isolated, never a sample grid.
/// </summary>
internal static class BCurvePlaneIntersection
{
    internal static AlgorithmStatus Find(in BCurveView curve,in AnalyticSurface plane,double lo,double hi,
        Span<OperationIntersectionPoint> points,out BufferCount count)
    {
        count=0;
        if(curve.Degree>16||curve.Dimension!=(curve.Rational?4:3)||lo<curve.Start||hi>curve.End)
            return AlgorithmStatus.Unsupported;
        Span<double> coefficients=stackalloc double[17*17];Span<double> roots=stackalloc double[17];
        Span<double> evaluationWorkspace=stackalloc double[17*2*4];Span<KernelVector3> jet=stackalloc KernelVector3[2];
        var p=curve.Degree;var stride=p+1;
        for(var span=p;span<curve.VertexCount;span++)
        {
            var a=curve.Knots[span];var b=curve.Knots[span+1];
            if(!(b>a)||b<lo||a>hi)continue;
            var start=Math.Max(a,lo);var end=Math.Min(b,hi);if(!(end>start))continue;
            coefficients.Clear();
            for(var j=0;j<=p;j++)
            {
                var at=(span-p+j)*curve.Dimension;var weight=curve.Rational?curve.Vertices[at+3]:1;
                coefficients[j*stride]=Dot(plane.Axis,Vector(curve.Vertices[at]-plane.Origin.X*weight,
                    curve.Vertices[at+1]-plane.Origin.Y*weight,curve.Vertices[at+2]-plane.Origin.Z*weight));
            }
            for(var r=1;r<=p;r++)
            for(var j=p;j>=r;j--)
            {
                var index=span-p+j;var denominator=curve.Knots[index+p-r+1]-curve.Knots[index];
                if(!(denominator>0))return AlgorithmStatus.NumericalFailure;
                var alpha=(a-curve.Knots[index])/denominator;var beta=(b-a)/denominator;
                var target=j*stride;var left=target-stride;
                for(var k=r;k>=0;k--)
                {
                    var value=coefficients[left+k]+alpha*(coefficients[target+k]-coefficients[left+k]);
                    if(k>0)value+=beta*(coefficients[target+k-1]-coefficients[left+k-1]);
                    coefficients[target+k]=value;
                }
            }
            var polynomial=coefficients.Slice(p*stride,stride);var allZero=true;
            for(var k=0;k<polynomial.Length;k++)
            {if(!double.IsFinite(polynomial[k]))return AlgorithmStatus.NumericalFailure;if(polynomial[k]!=0)allZero=false;}
            if(allZero)
            {
                var status=EvaluateAndAdd(curve,start,IntersectionVectorKind.Start,plane,evaluationWorkspace,jet,points,ref count);
                if(status!=AlgorithmStatus.Success)return status;
                status=EvaluateAndAdd(curve,end,IntersectionVectorKind.End,plane,evaluationWorkspace,jet,points,ref count);
                if(status!=AlgorithmStatus.Success)return status;
                continue;
            }
            var rootStatus=BoundedPolynomialRoots.Find(polynomial,(start-a)/(b-a),(end-a)/(b-a),roots,out var n);
            if(rootStatus!=AlgorithmStatus.Success)return rootStatus;
            for(var k=0;k<n;k++)
            {
                var t=Math.FusedMultiplyAdd(roots[k],b-a,a);
                var status=EvaluateAndAdd(curve,t,IntersectionVectorKind.Simple,plane,evaluationWorkspace,jet,points,ref count);
                if(status!=AlgorithmStatus.Success)return status;
            }
        }
        OperationIntersection.Sort(points[..count]);
        // Adjacent coincident knot spans form one interval. Preserve isolated
        // tangent/crossing roots and only remove the shared coincidence marker.
        for(var i=1;i+1<count;)
        {
            if(points[i-1].Kind==IntersectionVectorKind.Start&&points[i].Kind==IntersectionVectorKind.Start)
            {points[(i+1)..count].CopyTo(points[i..]);count--;}
            else i++;
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus EvaluateAndAdd(in BCurveView curve,double t,IntersectionVectorKind kind,
        in AnalyticSurface plane,Span<double> workspace,Span<KernelVector3> jet,
        Span<OperationIntersectionPoint> points,ref BufferCount count)
    {
        var status=BCurveEvaluation.Evaluate(curve,t,1,jet,workspace,out var tangent);
        if(status!=AlgorithmStatus.Success)return status;
        if(Math.Abs(Dot(Sub(jet[0],plane.Origin),plane.Axis))>OperationIntersection.DistanceTolerance)
            return AlgorithmStatus.NumericalFailure;
        if(kind==IntersectionVectorKind.Simple&&Math.Abs(Dot(tangent,plane.Axis))<=1e-8)kind=IntersectionVectorKind.Tangent;
        return OperationIntersection.AddPoint(points,ref count,jet[0],t,0,kind)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
    }
}

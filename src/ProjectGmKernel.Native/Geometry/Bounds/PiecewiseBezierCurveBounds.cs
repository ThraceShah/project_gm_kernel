using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Geometry.Bounds;

// Prepared coefficients of one C1 native curve, with consecutive original-t
// domains. Knot joins are not artificial endpoints of the scalar problem.
internal static class PiecewiseBezierCurveBounds
{
    internal static AlgorithmStatus Bound(ReadOnlySpan<IntervalNumber> poles,ReadOnlySpan<SurfaceParameterBox> domains,
        SplineDegree degree,in SurfaceParameterBox box,Span<IntervalNumber> output)
    {
        if(output.Length<9)return AlgorithmStatus.WorkspaceTooSmall;
        if(degree is <1 or >16||domains.IsEmpty||poles.Length!=4*(degree+1)*domains.Length||box.U1<box.U0)return AlgorithmStatus.InvalidInput;
        Span<IntervalNumber> local=stackalloc IntervalNumber[9];var first=true;var covered=box.U0;var width=4*(degree+1);
        for(var i=0;i<domains.Length;i++)
        {
            var domain=domains[i];var lo=Math.Max(box.U0,domain.U0);var hi=Math.Min(box.U1,domain.U1);if(lo>hi)continue;
            if(!(domain.U1>domain.U0)||lo>covered)return AlgorithmStatus.Unsupported;
            var status=BezierSurfaceBounds.Bound(new(poles.Slice(i*width,width),degree,0,domain),new(lo,hi,0,0),local);
            if(status!=AlgorithmStatus.Success)return status;
            for(var d=0;d<9;d++)output[d]=first?local[d]:IntervalNumber.Hull(output[d],local[d]);
            covered=Math.Max(covered,hi);first=false;
        }
        return !first&&covered==box.U1?AlgorithmStatus.Success:AlgorithmStatus.Unsupported;
    }
}

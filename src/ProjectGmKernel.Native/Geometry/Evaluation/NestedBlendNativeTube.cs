using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
#if KERNEL_TEST_HOOKS
    [ThreadStatic] internal static long TestParentTubeHits;
#endif
    // A complete parent tube proves existence, uniqueness, nearest-branch
    // membership and a derivative enclosure throughout its native interval.
    // Its uniform weighted contraction bounds the error of a new numerical
    // point by ||R F(z)||/(1-c). A mean-value enclosure then covers the whole
    // requested subinterval. Neither a numerical point nor a stored CHART
    // position alone supplies any of these certificates.
    private static bool ReadParentTube(in NestedBlendProgram program,BlendSurfaceIndex surface,BlendSpineIndex spine,
        in SurfaceParameterBox domain,ReadOnlySpan<double> state,ReadOnlySpan<double> pointInverse,double parameter,BufferOffset requestedChart,
        Span<IntervalNumber> output,out AlgorithmStatus status,Span<IntervalNumber> supportParameter=default,BufferOffset parameterSide=0)
    {
        status=AlgorithmStatus.NotRun;if(program.TubeSource.Tag<=0||program.TubeWork.IsEmpty)return false;
        BufferOffset selected=-1;var width=double.PositiveInfinity;
        for(var i=0;i<program.TubeWork.Length;i++)
        {
            ref var candidate=ref program.TubeWork[i];
            if(candidate.NativeDimension is not (2 or 4)||candidate.NativeDimension!=state.Length||candidate.NativeChart!=requestedChart||candidate.Nodes!=1||candidate.N!=candidate.NativeDimension+3
                ||!(candidate.NativeContraction>=0&&candidate.NativeContraction<.9))continue;
            var first=BitConverter.Int64BitsToDouble(candidate.First);var last=BitConverter.Int64BitsToDouble(candidate.Last);
            if(!(first<=domain.U0&&domain.U1<=last&&first<=parameter&&parameter<=last&&last>first&&last-first<width)
                ||!SameTube(candidate,program,spine,new(first,last,0,0)))continue;
            selected=i;width=last-first;
        }
        if(selected<0)return false;
        ref var entry=ref program.TubeWork[selected];var n=entry.NativeDimension;
        var definition=program.Spines[spine];if(SpineParameterMap.Explicit(definition))return false;
        var charts=Chart(program,spine);
        if(entry.NativeChart<0||entry.NativeChart>=charts.Length-1)return false;
        var chart=charts[entry.NativeChart];var firstSurface=program.Surfaces[definition.First];var secondSurface=program.Surfaces[definition.Second];
        if(!firstSurface.IsBSpline)(firstSurface,secondSurface)=(secondSurface,firstSurface);
        for(var i=0;i<n;i++)if(state[i]<entry.NativeBox[i].Lower||state[i]>entry.NativeBox[i].Upper)return false;
        Span<IntervalDual> variables=stackalloc IntervalDual[n],equations=stackalloc IntervalDual[n];
        Span<IntervalSplineContact> cache=stackalloc IntervalSplineContact[7];cache.Clear();
        for(var i=0;i<n;i++)variables[i]=state[i];
        if(NativeUvIntervalEquations(program,firstSurface,secondSurface,definition,chart,variables,parameter,equations,cache)!=AlgorithmStatus.Success)return false;
        var error=0.0;
        for(var row=0;row<n;row++)
        {
            if(!(entry.NativeWeights[row]>0))return false;
            IntervalNumber residual=0;for(var k=0;k<n;k++)residual+=entry.NativeInverse[row*n+k]*equations[k].Value;
            error=Math.Max(error,(residual/entry.NativeWeights[row]).Magnitude);
        }
        var pointError=new IntervalNumber(error)/(1-new IntervalNumber(entry.NativeContraction));
        if(!pointError.Finite)return false;
        Span<IntervalNumber> boxes=stackalloc IntervalNumber[n+3],derivative=stackalloc IntervalNumber[n+3];
        ((ReadOnlySpan<IntervalNumber>)entry.Derivative)[..derivative.Length].CopyTo(derivative);
        var delta=new IntervalNumber(domain.U0,domain.U1)-parameter;
        for(var i=0;i<n;i++)
        {
            var radius=(pointError*entry.NativeWeights[i]).Upper;
            var anchor=new IntervalNumber(state[i])+new IntervalNumber(-radius,radius);
            anchor=new(Math.Max(anchor.Lower,entry.Boxes[i+3].Lower),Math.Min(anchor.Upper,entry.Boxes[i+3].Upper));
            if(!anchor.Finite)return false;variables[i]=anchor;
            var range=anchor+derivative[i+3]*delta;
            boxes[i+3]=new(Math.Max(range.Lower,entry.Boxes[i+3].Lower),Math.Min(range.Upper,entry.Boxes[i+3].Upper));
            if(!boxes[i+3].Finite)return false;
        }
        if(NativeUvOffset(program,firstSurface,variables[0],variables[1],3,cache,out var anchorPosition)!=AlgorithmStatus.Success)return false;
        // A parent derivative bounds variation, but need not resolve the
        // sign on a smaller interval. Re-enclose this interval's Jacobian
        // and solve its differential equations on the certified root box.
        Span<IntervalNumber> jacobian=stackalloc IntervalNumber[n*n],velocity=stackalloc IntervalNumber[n],jacobianBox=stackalloc IntervalNumber[n];velocity.Clear();
        // The candidate can lie outside the tighter root box while remaining
        // inside the parent's initial certificate. Include its whole secant
        // to every root in the local mean-value Jacobian domain.
        for(var i=0;i<n;i++)jacobianBox[i]=IntervalNumber.Hull(boxes[i+3],new(state[i]));
        if(NativeUvJacobian(program,firstSurface,secondSurface,chart,jacobianBox,jacobian,cache)!=AlgorithmStatus.Success)return false;
        // The uniform scalar error can mix unrelated native UV units. One
        // local Krawczyk image removes that excess without weakening the
        // already established parent existence or increasing refinement limits.
        Span<IntervalNumber> remainder=stackalloc IntervalNumber[n*n],image=stackalloc IntervalNumber[n];
        Precondition(pointInverse,jacobian,n,remainder);
        for(var row=0;row<n;row++)
        {
            IntervalNumber value=state[row];
            for(var k=0;k<n;k++)
            {
                var forcing=equations[k].Value;if(k==n-1)forcing-=delta/chart.Scale;
                value-=pointInverse[row*n+k]*forcing;
                value+=remainder[row*n+k]*(boxes[k+3]-state[k]);
            }
            image[row]=new(Math.Max(boxes[row+3].Lower,value.Lower),Math.Min(boxes[row+3].Upper,value.Upper));
            if(!image[row].Finite)return false;
        }
        image.CopyTo(boxes[3..]);
        if(ReuseSplineContactBounds)
        {
            if(CacheSplineContact(program,firstSurface,boxes[3],boxes[4],ref cache[3],surface<0&&firstSurface.Offset==0)!=AlgorithmStatus.Success)return false;
            if(n==4&&CacheSplineContact(program,secondSurface,boxes[5],boxes[6],ref cache[5],surface<0&&secondSurface.Offset==0)!=AlgorithmStatus.Success)return false;
        }
        velocity[^1]=new IntervalNumber(1)/chart.Scale;
        if(SolveIntervals(jacobian,velocity,n)!=AlgorithmStatus.Success)return false;
        for(var i=0;i<n;i++)
        {
            derivative[i+3]=new(Math.Max(derivative[i+3].Lower,velocity[i].Lower),Math.Min(derivative[i+3].Upper,velocity[i].Upper));
            if(!derivative[i+3].Finite)return false;variables[i]=new(boxes[i+3],derivative[i+3]);
        }
        if(NativeUvOffset(program,firstSurface,variables[0],variables[1],3,cache,out var position)!=AlgorithmStatus.Success)return false;
        for(var axis=0;axis<3;axis++)
        {
            var anchor=axis==0?anchorPosition.X.Value:axis==1?anchorPosition.Y.Value:anchorPosition.Z.Value;
            var local=axis==0?position.X:axis==1?position.Y:position.Z;
            var range=anchor+derivative[axis]*delta;
            boxes[axis]=new(Math.Max(local.Value.Lower,Math.Max(range.Lower,entry.Boxes[axis].Lower)),Math.Min(local.Value.Upper,Math.Min(range.Upper,entry.Boxes[axis].Upper)));
            derivative[axis]=new(Math.Max(derivative[axis].Lower,local.Derivative.Lower),Math.Min(derivative[axis].Upper,local.Derivative.Upper));
            if(!boxes[axis].Finite||!derivative[axis].Finite)return false;
        }
        if(!supportParameter.IsEmpty)
        {
            var source=program.Surfaces[parameterSide==0?definition.First:definition.Second];
            var uv=parameterSide==0?entry.Occurrences[0].FirstUv:entry.Occurrences[0].SecondUv;
            if(!source.IsBSpline||uv<0||uv+1>=boxes.Length)return false;
            supportParameter[0]=boxes[uv];supportParameter[1]=derivative[uv];
            if(supportParameter.Length==4){supportParameter[2]=boxes[uv+1];supportParameter[3]=derivative[uv+1];}
            status=AlgorithmStatus.Success;
        }
        else status=TubeFrame(program,surface,domain,((ReadOnlySpan<Occurrence>)entry.Occurrences)[..entry.Nodes],boxes,derivative,output,cache);
        if(status==AlgorithmStatus.Success)ModelingPerformance.ParentTube();
#if KERNEL_TEST_HOOKS
        if(status==AlgorithmStatus.Success)TestParentTubeHits++;
#endif
        return status==AlgorithmStatus.Success;
    }
}

using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    // A regular offset foot defines XYZ: C=S(U,V)+h*n(U,V).
    // Two spline feet leave four UV unknowns; a spline foot and an analytic
    // plane leave two. Keep the original native CHART station equation.
    private static AlgorithmStatus BoundNativeUvSegment(in NestedBlendProgram program,BlendSurfaceIndex surface,BlendSpineIndex spine,
        in SurfaceParameterBox domain,Span<IntervalNumber> output,out bool handled,Span<BufferCount> reachBudget=default,
        Span<IntervalNumber> curvature=default,BufferOffset curvatureSide=0,Span<IntervalNumber> supportParameter=default,BufferOffset parameterSide=0,
        Span<double> anchorContacts=default,Span<IntervalNumber> acceleration=default,KernelVector3 accelerationNormal=default)
    {
        handled=false;var definition=program.Spines[spine];if(SpineParameterMap.Explicit(definition))return AlgorithmStatus.Unsupported;
        var first=program.Surfaces[definition.First];var second=program.Surfaces[definition.Second];
        var swapped=!first.IsBSpline;if(swapped)(first,second)=(second,first);
        if(!first.IsBSpline||!NativeUvRegularSheet(program,first))return AlgorithmStatus.Unsupported;
        if(second.IsBSpline)
        {if(second.SplineBasis==first.SplineBasis||!NativeUvRegularSheet(program,second))return AlgorithmStatus.Unsupported;}
        else if(second.IsBlend||second.Analytic.Kind!=SurfaceClass.Plane||second.NormalSense==0)return AlgorithmStatus.Unsupported;
        handled=true;BufferCount n=second.IsBSpline?4:2,fullCount=n+3;
        var charts=Chart(program,spine);var u=Math.Clamp(domain.U0+(domain.U1-domain.U0)*.5,charts[0].Parameter,charts[^1].Parameter);
        var chart=0;while(chart<charts.Length-2&&charts[chart+1].Parameter<=u)chart++;
        var lower=charts[chart];var upper=charts[chart+1];var fraction=(u-lower.Parameter)/(upper.Parameter-lower.Parameter);
        Span<double> state=stackalloc double[n],seed=stackalloc double[n],matrix=stackalloc double[n*n],inverse=stackalloc double[n*n],rhs=stackalloc double[n],velocity=stackalloc double[n];
        Span<BufferOffset> pivots=stackalloc BufferOffset[n];
        state[0]=swapped?lower.SecondU+fraction*(upper.SecondU-lower.SecondU):lower.FirstU+fraction*(upper.FirstU-lower.FirstU);
        state[1]=swapped?lower.SecondV+fraction*(upper.SecondV-lower.SecondV):lower.FirstV+fraction*(upper.FirstV-lower.FirstV);
        if(n==4){state[2]=lower.SecondU+fraction*(upper.SecondU-lower.SecondU);state[3]=lower.SecondV+fraction*(upper.SecondV-lower.SecondV);}
        state.CopyTo(seed);
        var pointStatus=RefineNativeUvPoint(program,first,second,spine,lower,state,u,matrix,pivots);
        if(pointStatus!=AlgorithmStatus.Success)return pointStatus;
        rhs.Clear();rhs[^1]=1/lower.Scale;
        var found=SmallLinearSolve.LuSolveInPlace(matrix,n,pivots,rhs);if(found!=AlgorithmStatus.Success)return found;rhs.CopyTo(velocity);
        for(var column=0;column<n;column++)
        {
            rhs.Clear();rhs[column]=1;found=SmallLinearSolve.LuSolveInPlace(matrix,n,pivots,rhs);if(found!=AlgorithmStatus.Success)return found;
            for(var row=0;row<n;row++)inverse[row*n+column]=rhs[row];
        }
        // The original point solve and its unchanged budget supply a proposal
        // to either certificate. A rejected parent does not repeat that solve.
        if(reachBudget.IsEmpty&&curvature.IsEmpty&&supportParameter.IsEmpty&&anchorContacts.IsEmpty&&acceleration.IsEmpty
            &&ReadParentTube(program,surface,spine,domain,state,inverse,u,chart,output,out var parentStatus))return parentStatus;
        Span<IntervalNumber> boxes=stackalloc IntervalNumber[n],image=stackalloc IntervalNumber[n],centerF=stackalloc IntervalNumber[n],jacobian=stackalloc IntervalNumber[n*n],remainder=stackalloc IntervalNumber[n*n];
        Span<IntervalDual> variables=stackalloc IntervalDual[n],equations=stackalloc IntervalDual[n];
        Span<IntervalSplineContact> cache=stackalloc IntervalSplineContact[7];cache.Clear();
        for(var i=0;i<n;i++)variables[i]=state[i];
        var t=new IntervalNumber(domain.U0,domain.U1);
        found=NativeUvIntervalEquations(program,first,second,definition,lower,variables,t,equations,cache);if(found!=AlgorithmStatus.Success)return found;
        for(var i=0;i<n;i++)centerF[i]=equations[i].Value;
        var certified=false;
        Span<double> radii=stackalloc double[n],nextRadii=stackalloc double[n];
        // A stationary native coordinate can have zero midpoint velocity.
        // Its deviation from the native CHART secant supplies an anisotropic
        // box hint, never a curve bound. Uniform strict inclusion below must
        // still certify every box independently of that hint.
        var stationSpan=(u-lower.Parameter)*(upper.Parameter-u);var half=Math.Max(Math.Abs(domain.U1-u),Math.Abs(domain.U0-u));
        for(var i=0;i<n;i++)
        {
            var bend=stationSpan>0?8*Math.Abs(state[i]-seed[i])*half*half/stationSpan:0;
            IntervalNumber forcing=0;for(var k=0;k<n;k++)forcing+=inverse[i*n+k]*centerF[k];
            var rounding=64*Math.Max(Math.BitIncrement(state[i])-state[i],state[i]-Math.BitDecrement(state[i]));
            radii[i]=Math.Max(2*Math.Abs(velocity[i])*half+bend,2*forcing.Magnitude)+rounding;
        }
        for(var expansion=0;expansion<6&&!certified;expansion++)
        {
            for(var i=0;i<n;i++)
            {
                boxes[i]=new(Math.BitDecrement(state[i]-radii[i]),Math.BitIncrement(state[i]+radii[i]));
            }
            found=NativeUvJacobian(program,first,second,lower,boxes,jacobian,cache);
            if(found!=AlgorithmStatus.Success){for(var i=0;i<n;i++)radii[i]*=2;continue;}
            Precondition(inverse,jacobian,n,remainder);certified=true;
            radii.CopyTo(nextRadii);
            for(var row=0;row<n;row++)
            {
                IntervalNumber value=state[row];for(var k=0;k<n;k++)value-=inverse[row*n+k]*centerF[k];
                var contraction=0.0;var offDiagonal=0.0;
                for(var k=0;k<n;k++)
                {
                    value+=remainder[row*n+k]*(boxes[k]-state[k]);
                    contraction+=remainder[row*n+k].Magnitude*(boxes[k].Upper-boxes[k].Lower)/(boxes[row].Upper-boxes[row].Lower);
                    if(k!=row)offDiagonal+=remainder[row*n+k].Magnitude*radii[k];
                }
                image[row]=value;var accepted=value.Lower>boxes[row].Lower&&value.Upper<boxes[row].Upper&&contraction<.9;certified&=accepted;
                if(!accepted)
                {
                    // Balance this coordinate using the actual comparison
                    // row. Uniform expansion mixes unrelated native UV units
                    // and cannot repair a stationary coordinate's thin box.
                    // This is only the next candidate; strict inclusion and
                    // the unchanged <.9 contraction still prove acceptance.
                    var required=1.25*(value-state[row]).Magnitude;var self=remainder[row*n+row].Magnitude;
                    if(self<.8)required=Math.Max(required,1.05*offDiagonal/(.8-self));
                    nextRadii[row]=Math.Max(radii[row],required);
                }
            }
            nextRadii.CopyTo(radii);
        }
        if(!certified)return AlgorithmStatus.NotConverged;
        Span<IntervalNumber> certificateBox=stackalloc IntervalNumber[n];boxes.CopyTo(certificateBox);
        var contractionBound=double.PositiveInfinity;
        if(program.TubeSource.Tag>0&&!program.TubeWork.IsEmpty&&domain.U0<domain.U1)
        {
            contractionBound=0;
            for(var row=0;row<n;row++)
            {
                IntervalNumber sum=0;for(var k=0;k<n;k++)sum+=new IntervalNumber(remainder[row*n+k].Magnitude)*radii[k]/radii[row];
                contractionBound=Math.Max(contractionBound,sum.Upper);
            }
        }
        for(var iteration=0;iteration<6;iteration++)
        {
            for(var i=0;i<n;i++)boxes[i]=new(Math.Max(boxes[i].Lower,image[i].Lower),Math.Min(boxes[i].Upper,image[i].Upper));
            found=NativeUvJacobian(program,first,second,lower,boxes,jacobian,cache);if(found!=AlgorithmStatus.Success)return found;
            Precondition(inverse,jacobian,n,remainder);
            for(var row=0;row<n;row++)
            {
                IntervalNumber value=state[row];for(var k=0;k<n;k++)value-=inverse[row*n+k]*centerF[k];
                for(var k=0;k<n;k++)value+=remainder[row*n+k]*(boxes[k]-state[k]);image[row]=value;
            }
        }
        for(var i=0;i<n;i++)variables[i]=boxes[i];
        found=NativeUvIntervalEquations(program,first,second,definition,lower,variables,new(t,1),equations,cache);if(found!=AlgorithmStatus.Success)return found;
        for(var i=0;i<n;i++)image[i]=-equations[i].Derivative;
        found=SolveIntervals(jacobian,image,n);if(found!=AlgorithmStatus.Success)return found;
        for(var i=0;i<n;i++)variables[i]=new(boxes[i],image[i]);
        found=NativeUvOffset(program,first,variables[0],variables[1],3,cache,out var position);if(found!=AlgorithmStatus.Success)return found;
        Span<IntervalNumber> fullBoxes=stackalloc IntervalNumber[fullCount],fullDerivative=stackalloc IntervalNumber[fullCount];
        Span<IntervalDual> full=stackalloc IntervalDual[fullCount];
        full[0]=position.X;full[1]=position.Y;full[2]=position.Z;
        for(var i=0;i<n;i++)full[i+3]=variables[i];
        for(var i=0;i<fullCount;i++){fullBoxes[i]=full[i].Value;fullDerivative[i]=full[i].Derivative;}
        Span<Occurrence> nodes=stackalloc Occurrence[1];nodes[0]=new(){Spine=spine,Offset=0,Parent=-1,FirstChild=-1,SecondChild=-1,
            FirstUv=swapped?-1:3,SecondUv=n==4?5:swapped?3:-1};
        Span<BufferOffset> chartIndices=stackalloc BufferOffset[1];chartIndices[0]=chart;
        found=CertifyNearestBoxes(program,nodes,fullBoxes,full,default,chartIndices,cache);if(found!=AlgorithmStatus.Success)return found;
        if(!reachBudget.IsEmpty)
        {
            var centers=new IntervalBox3(fullBoxes[0].Lower,fullBoxes[0].Upper,fullBoxes[1].Lower,fullBoxes[1].Upper,fullBoxes[2].Lower,fullBoxes[2].Upper);
            for(var side=0;side<2;side++)
            {
                var source=program.Surfaces[side==0?definition.First:definition.Second];
                if(source.IsBSpline)
                {
                    var at=(side==0?nodes[0].FirstUv:nodes[0].SecondUv)-3;
                    var feet=new SurfaceParameterBox(boxes[at].Lower,boxes[at].Upper,boxes[at+1].Lower,boxes[at+1].Upper);
                    found=RollingBallReach.CertifySpline(program.Spline(source),centers,feet,ref reachBudget[0],reachBudget[1],out _);
                }
                else found=RollingBallReach.CertifyPlane(source.Analytic,out _);
                if(found!=AlgorithmStatus.Success)return found;
            }
        }
        if(!supportParameter.IsEmpty)
        {
            var at=(parameterSide==0?nodes[0].FirstUv:nodes[0].SecondUv)-3;
            if(at<0)return AlgorithmStatus.Unsupported;
            supportParameter[0]=boxes[at];supportParameter[1]=image[at];
            if(supportParameter.Length==4){supportParameter[2]=boxes[at+1];supportParameter[3]=image[at+1];}
            return AlgorithmStatus.Success;
        }
        if(!curvature.IsEmpty||!acceleration.IsEmpty)
            return BoundSpineDifferential(program,spine,nodes,full,fullDerivative,chart,curvature,curvatureSide,acceleration,accelerationNormal,cache);
        if(!anchorContacts.IsEmpty)
        {
            Span<TaylorJet> anchor=stackalloc TaylorJet[fullCount];for(var i=0;i<fullCount;i++)anchor[i]=new(fullBoxes[i].Midpoint,0);
            for(var side=0;side<2;side++)
            {found=SpineSupportParameters(program,nodes,anchor,side,u,out anchorContacts[2*side],out anchorContacts[2*side+1]);if(found!=AlgorithmStatus.Success)return found;}
        }
        if(reachBudget.IsEmpty&&anchorContacts.IsEmpty)
        {
            var saved=WriteTube(program,spine,domain,nodes,fullBoxes,fullDerivative);
            if(saved>=0&&domain.U0<domain.U1&&contractionBound<.9)
            {
                ref var entry=ref program.TubeWork[saved];entry.NativeDimension=n;entry.NativeChart=chart;entry.NativeContraction=contractionBound;
                inverse.CopyTo(entry.NativeInverse);radii.CopyTo(entry.NativeWeights);certificateBox.CopyTo(entry.NativeBox);
            }
        }
        if(surface<0)
        {
            for(var i=0;i<3;i++){output[i]=fullBoxes[i];output[i+3]=fullDerivative[i];output[i+6]=0;}return AlgorithmStatus.Success;
        }
        found=IntervalFrame(program,surface,nodes,full,new IntervalNumber(domain.V0,domain.V1),out var frame,cache);if(found!=AlgorithmStatus.Success)return found;
        output[0]=frame.X.Value;output[1]=frame.Y.Value;output[2]=frame.Z.Value;
        output[3]=frame.X.Derivative;output[4]=frame.Y.Derivative;output[5]=frame.Z.Derivative;
        for(var i=0;i<fullCount;i++)full[i]=full[i].Value;
        found=IntervalFrame(program,surface,nodes,full,new(new(domain.V0,domain.V1),1),out frame,cache);if(found!=AlgorithmStatus.Success)return found;
        output[6]=frame.X.Derivative;output[7]=frame.Y.Derivative;output[8]=frame.Z.Derivative;return AlgorithmStatus.Success;
    }

    private static bool NativeUvRegularSheet(in NestedBlendProgram program,in BlendSurfaceInstruction source)
    {
        if(source.NormalSense==0)return false;
        var spline=program.Spline(source);
        return !spline.UPeriodic&&!spline.VPeriodic&&CartesianSplineSmoothKnots(spline.UKnots,spline.UDegree,spline.UVertexCount)
            &&CartesianSplineSmoothKnots(spline.VKnots,spline.VDegree,spline.VVertexCount);
    }

    private static AlgorithmStatus RefineNativeUvPoint(in NestedBlendProgram program,in BlendSurfaceInstruction first,in BlendSurfaceInstruction second,
        BlendSpineIndex spine,in BlendChartInstruction chart,Span<double> state,double parameter,Span<double> matrix,Span<BufferOffset> pivots)
    {
        var n=state.Length;Span<double> residual=stackalloc double[n],rhs=stackalloc double[n],trial=stackalloc double[n];
        for(var iteration=0;iteration<20;iteration++)
        {
            var status=NativeUvPointEquations(program,first,second,program.Spines[spine],chart,state,parameter,residual,matrix,out var center);if(status!=AlgorithmStatus.Success)return AlgorithmStatus.NotConverged;
            var error=Maximum(residual);var tolerance=ResidualAccuracy(program,spine,center);
            status=SmallLinearSolve.LuFactorize(matrix,n,pivots);if(status!=AlgorithmStatus.Success)return AlgorithmStatus.NotConverged;
            if(error<tolerance)return AlgorithmStatus.Success;
            for(var i=0;i<n;i++)rhs[i]=-residual[i];
            status=SmallLinearSolve.LuSolveInPlace(matrix,n,pivots,rhs);if(status!=AlgorithmStatus.Success)return status;
            var accepted=false;
            for(var attempt=0;attempt<10;attempt++)
            {
                var factor=Math.ScaleB(1,-attempt);for(var i=0;i<n;i++)trial[i]=state[i]+factor*rhs[i];
                status=NativeUvPointEquations(program,first,second,program.Spines[spine],chart,trial,parameter,residual,default,out _);
                if(status!=AlgorithmStatus.Success||!(Maximum(residual)<error))continue;
                trial.CopyTo(state);accepted=true;break;
            }
            if(!accepted)return AlgorithmStatus.NotConverged;
        }
        return AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus NativeUvPointEquations(in NestedBlendProgram program,in BlendSurfaceInstruction first,in BlendSurfaceInstruction second,
        in BlendSpineInstruction spine,in BlendChartInstruction chart,ReadOnlySpan<double> uv,double parameter,Span<double> residual,Span<double> matrix,out KernelVector3 center)
    {
        center=default;var n=uv.Length;matrix.Clear();
        var status=NestedSplineEvaluation.Contact(program,first,uv[0],uv[1],out var a);if(status!=AlgorithmStatus.Success)return status;
        center=Add(a.Point,Scale(a.Normal,first.Offset));var au=Add(a.Du,Scale(a.NormalU,first.Offset));var av=Add(a.Dv,Scale(a.NormalV,first.Offset));
        if(second.IsBSpline)
        {
            status=NestedSplineEvaluation.Contact(program,second,uv[2],uv[3],out var b);if(status!=AlgorithmStatus.Success)return status;
            var delta=Sub(center,Add(b.Point,Scale(b.Normal,second.Offset)));residual[0]=delta.X;residual[1]=delta.Y;residual[2]=delta.Z;
            if(!matrix.IsEmpty)
            {
                var bu=Scale(Add(b.Du,Scale(b.NormalU,second.Offset)),-1);var bv=Scale(Add(b.Dv,Scale(b.NormalV,second.Offset)),-1);
                matrix[0]=au.X;matrix[1]=av.X;matrix[2]=bu.X;matrix[3]=bv.X;matrix[4]=au.Y;matrix[5]=av.Y;matrix[6]=bu.Y;matrix[7]=bv.Y;matrix[8]=au.Z;matrix[9]=av.Z;matrix[10]=bu.Z;matrix[11]=bv.Z;
            }
        }
        else
        {
            residual[0]=Dot(Sub(center,second.Analytic.Origin),second.Analytic.Axis);
            if(!matrix.IsEmpty){matrix[0]=Dot(au,second.Analytic.Axis);matrix[1]=Dot(av,second.Analytic.Axis);}
        }
        status=SpineParameterMap.Scalar(spine,chart,parameter,center,out residual[n-1],out var gradient,out _);if(status!=AlgorithmStatus.Success)return status;
        if(!matrix.IsEmpty){matrix[(n-1)*n]=Dot(gradient,au);matrix[(n-1)*n+1]=Dot(gradient,av);}
        foreach(var value in residual)if(!double.IsFinite(value))return AlgorithmStatus.NumericalFailure;return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus NativeUvOffset(in NestedBlendProgram program,in BlendSurfaceInstruction source,IntervalDual u,IntervalDual v,
        BufferOffset cacheIndex,ReadOnlySpan<IntervalSplineContact> cache,out IntervalVector position)
    {
        if(source.Offset==0)return IntervalSplinePoint(program,source,u,v,out position,cache,cacheIndex);
        position=default;var status=IntervalSpline(program,source,u,v,out var point,out var su,out var sv,cache,cacheIndex);if(status!=AlgorithmStatus.Success)return status;
        var cross=IntervalVector.Cross(su,sv);var length=cross.Norm();if(!(length.Value.Lower>0))return AlgorithmStatus.NotConverged;
        position=point+cross/length*source.Offset;return position.Finite?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus NativeUvIntervalEquations(in NestedBlendProgram program,in BlendSurfaceInstruction first,in BlendSurfaceInstruction second,
        in BlendSpineInstruction spine,in BlendChartInstruction chart,ReadOnlySpan<IntervalDual> uv,IntervalDual parameter,Span<IntervalDual> residual,ReadOnlySpan<IntervalSplineContact> cache)
    {
        var status=NativeUvOffset(program,first,uv[0],uv[1],3,cache,out var point);if(status!=AlgorithmStatus.Success)return status;
        if(second.IsBSpline)
        {
            status=NativeUvOffset(program,second,uv[2],uv[3],5,cache,out var other);if(status!=AlgorithmStatus.Success)return status;
            var delta=point-other;residual[0]=delta.X;residual[1]=delta.Y;residual[2]=delta.Z;
        }
        else residual[0]=IntervalVector.Dot(point-new IntervalVector(second.Analytic.Origin),new IntervalVector(second.Analytic.Axis));
        status=SpineParameterMap.Interval(spine,chart,parameter,point,out residual[uv.Length-1]);if(status!=AlgorithmStatus.Success)return status;
        foreach(var value in residual)if(!value.Finite)return AlgorithmStatus.NotConverged;return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus NativeUvJacobian(in NestedBlendProgram program,in BlendSurfaceInstruction first,in BlendSurfaceInstruction second,
        in BlendChartInstruction chart,ReadOnlySpan<IntervalNumber> boxes,Span<IntervalNumber> matrix,Span<IntervalSplineContact> cache)
    {
        var n=boxes.Length;ModelingPerformance.Jacobian(n);
        AlgorithmStatus status;
        if(ReuseSplineContactBounds)
        {
            status=CacheSplineContact(program,first,boxes[0],boxes[1],ref cache[3],first.Offset==0);if(status!=AlgorithmStatus.Success)return status;
            if(second.IsBSpline){status=CacheSplineContact(program,second,boxes[2],boxes[3],ref cache[5],second.Offset==0);if(status!=AlgorithmStatus.Success)return status;}
        }
        else cache.Clear();
        // Each native foot depends only on its own two UV coordinates.
        // Differentiate that support, rather than evaluating both offset maps
        // in every residual direction. The native gauge uses the first foot.
        matrix.Clear();
        Span<IntervalNumber> derivatives=stackalloc IntervalNumber[6];
        for(var side=0;side<(n==4?2:1);side++)
        {
            var at=2*side;
            status=NativeUvOffsetDerivatives(program,side==0?first:second,boxes[at],boxes[at+1],side==0?3:5,cache,derivatives);if(status!=AlgorithmStatus.Success)return status;
            for(var axis=0;axis<2;axis++)
            {
                var column=at+axis;var d=derivatives.Slice(3*axis,3);ModelingPerformance.ResidualDirection();
                if(second.IsBSpline)
                {var sign=side==0?1:-1;for(var row=0;row<3;row++)matrix[row*n+column]=sign*d[row];}
                else matrix[column]=d[0]*second.Analytic.Axis.X+d[1]*second.Analytic.Axis.Y+d[2]*second.Analytic.Axis.Z;
                if(side==0)matrix[(n-1)*n+column]=d[0]*chart.Chord.X+d[1]*chart.Chord.Y+d[2]*chart.Chord.Z;
            }
        }
        foreach(var value in matrix)if(!value.Finite)return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus NativeUvOffsetDerivatives(in NestedBlendProgram program,in BlendSurfaceInstruction source,
        IntervalNumber u,IntervalNumber v,BufferOffset cacheIndex,ReadOnlySpan<IntervalSplineContact> cache,Span<IntervalNumber> derivatives)
    {
        Span<IntervalNumber> local=stackalloc IntervalNumber[18];scoped ReadOnlySpan<IntervalNumber> jet;
        if(!cache.IsEmpty&&SplineContactMatches(cache[cacheIndex],source,u,v,source.Offset==0?1:2))jet=cache[cacheIndex].Values;
        else{var status=IntervalSplineBounds(program,source,u,v,local[..(source.Offset==0?9:18)]);if(status!=AlgorithmStatus.Success)return status;jet=local;}
        if(source.Offset==0){jet.Slice(3,6).CopyTo(derivatives);return AlgorithmStatus.Success;}
        Span<IntervalNumber> cross=stackalloc IntervalNumber[3],normal=stackalloc IntervalNumber[3],change=stackalloc IntervalNumber[3];
        for(var axis=0;axis<3;axis++)
        {var a=(axis+1)%3;var b=(axis+2)%3;cross[axis]=jet[3+a]*jet[6+b]-jet[3+b]*jet[6+a];}
        var length=IntervalDual.Sqrt(IntervalNumber.Square(cross[0])+IntervalNumber.Square(cross[1])+IntervalNumber.Square(cross[2])).Value;
        if(!(length.Lower>0))return AlgorithmStatus.NotConverged;
        for(var axis=0;axis<3;axis++)normal[axis]=cross[axis]/length;
        for(var direction=0;direction<2;direction++)
        {
            for(var axis=0;axis<3;axis++)
            {
                var a=(axis+1)%3;var b=(axis+2)%3;var uu=direction==0?9:12;var uv=direction==0?12:15;
                change[axis]=jet[uu+a]*jet[6+b]-jet[uu+b]*jet[6+a]+jet[3+a]*jet[uv+b]-jet[3+b]*jet[uv+a];
            }
            var projection=normal[0]*change[0]+normal[1]*change[1]+normal[2]*change[2];
            for(var axis=0;axis<3;axis++)derivatives[3*direction+axis]=jet[3+3*direction+axis]+source.Offset*(change[axis]-normal[axis]*projection)/length;
        }
        foreach(var value in derivatives)if(!value.Finite)return AlgorithmStatus.NotConverged;return AlgorithmStatus.Success;
    }

#if KERNEL_TEST_HOOKS
    internal static AlgorithmStatus TestNativeUvOffsetDerivatives(in NestedBlendProgram program,BlendSurfaceIndex surface,
        in SurfaceParameterBox domain,Span<IntervalNumber> derivatives)
        =>NativeUvOffsetDerivatives(program,program.Surfaces[surface],new(domain.U0,domain.U1),new(domain.V0,domain.V1),-1,default,derivatives);
#endif
}
